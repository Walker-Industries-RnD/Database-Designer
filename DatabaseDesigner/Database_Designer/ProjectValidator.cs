using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DatabaseDesigner;
using static DatabaseDesigner.SessionStorage;
using PT = DatabaseDesigner.DBDesigner.PostgresType;

namespace Database_Designer
{
    public static class ProjectValidator
    {
        public enum Severity { Error, Warning, Info }

        public sealed record Issue(Severity Severity, string Table, string Message);

        private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        {
            "all","analyse","analyze","and","any","array","as","asc","asymmetric","authorization","binary","both",
            "case","cast","check","collate","collation","column","concurrently","constraint","create","cross",
            "current_catalog","current_date","current_role","current_schema","current_time","current_timestamp",
            "current_user","default","deferrable","desc","distinct","do","else","end","except","false","fetch",
            "for","foreign","freeze","from","full","grant","group","having","ilike","in","initially","inner",
            "intersect","into","is","isnull","join","lateral","leading","left","like","limit","localtime",
            "localtimestamp","natural","not","notnull","null","offset","on","only","or","order","outer","overlaps",
            "placing","primary","references","returning","right","select","session_user","similar","some",
            "symmetric","table","tablesample","then","to","trailing","true","union","unique","user","using",
            "variadic","verbose","when","where","window","with"
        };

        private static readonly Regex Identifier = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        public static string Qualified(TableObject t) =>
            string.IsNullOrEmpty(t.SchemaName) ? t.TableName : $"{t.SchemaName}.{t.TableName}";

        public static List<Issue> Validate(IEnumerable<TableObject> tablesIn, string rlsJson = null)
        {
            var issues = new List<Issue>();
            var tables = (tablesIn ?? Enumerable.Empty<TableObject>()).ToList();
            if (tables.Count == 0)
            {
                issues.Add(new Issue(Severity.Warning, null, "The project has no tables yet; there's nothing to export."));
                return issues;
            }

            foreach (var dup in tables.GroupBy(t => Qualified(t), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                issues.Add(new Issue(Severity.Error, dup.Key, $"Table {dup.Key} is defined {dup.Count()} times; CREATE TABLE will fail on the second one."));

            foreach (var t in tables)
            {
                var q = Qualified(t);
                if (string.IsNullOrWhiteSpace(t.TableName) || !Identifier.IsMatch(t.TableName))
                    issues.Add(new Issue(Severity.Error, q, $"Table name \"{t.TableName}\" should only use letters, digits and _ and not start with a digit."));
                if (!string.IsNullOrEmpty(t.SchemaName) && !Identifier.IsMatch(t.SchemaName))
                    issues.Add(new Issue(Severity.Error, q, $"Schema name \"{t.SchemaName}\" should only use letters, digits and _."));
                if ((t.TableName ?? "").Length > 63)
                    issues.Add(new Issue(Severity.Error, q, "Table name is longer than Postgres' 63-character limit."));

                var rows = t.Rows ?? new List<RowCreation>();
                if (rows.Count == 0)
                {
                    issues.Add(new Issue(Severity.Warning, q, $"{q} has no columns and will be left out of the export."));
                    continue;
                }

                foreach (var dup in rows.GroupBy(r => r.Name ?? "", StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                    issues.Add(new Issue(Severity.Error, q, $"Column \"{dup.Key}\" appears {dup.Count()} times in {q}."));

                if (!rows.Any(r => r.IsPrimary == true))
                    issues.Add(new Issue(Severity.Error, q, $"{q} has no primary key. The generated API (Entity Framework) can't map a table without one; add an id column marked Primary."));

                foreach (var r in rows)
                {
                    var name = r.Name ?? "";
                    if (!Identifier.IsMatch(name))
                        issues.Add(new Issue(Severity.Error, q, $"Column \"{name}\" in {q} should only use letters, digits and _ and not start with a digit."));
                    else if (name != name.ToLowerInvariant())
                        issues.Add(new Issue(Severity.Error, q,
                            $"Column \"{name}\" in {q} has capital letters. Postgres folds it to \"{name.ToLowerInvariant()}\" but the API looks for \"{name}\"; rename it to snake_case (e.g. {ToSnake(name)})."));
                    if (Reserved.Contains(name))
                        issues.Add(new Issue(Severity.Error, q, $"Column \"{name}\" in {q} is a reserved SQL word; SQL.sql will fail. Try \"{name}_value\" or a more specific name."));
                    if (name.Length > 63)
                        issues.Add(new Issue(Severity.Error, q, $"Column \"{name}\" in {q} is longer than 63 characters."));
                    if (r.RowType == null && r.EncryptedAndNOTMedia != true && r.Media != true)
                        issues.Add(new Issue(Severity.Error, q, $"Column \"{name}\" in {q} has no type."));
                    if ((r.RowType == PT.VarChar || r.RowType == PT.Char) && (r.Limit == null || r.Limit <= 0))
                        issues.Add(new Issue(Severity.Info, q, $"Column \"{name}\" in {q} is {r.RowType} without a length; it behaves like Text. Set a limit or use Text."));
                    if (r.RowType == PT.Money)
                        issues.Add(new Issue(Severity.Info, q, $"Column \"{name}\" in {q} uses money, which depends on the server's locale. Numeric is the usual choice for prices."));
                    if (r.IsPrimary == true && r.IsArray == true)
                        issues.Add(new Issue(Severity.Error, q, $"Primary key \"{name}\" in {q} can't be an array."));
                }

                foreach (var idx in t.Indexes ?? new List<IndexCreation>())
                {
                    if (!string.IsNullOrWhiteSpace(idx.Expression)) continue;
                    foreach (var col in idx.ColumnNames ?? new List<string>())
                        if (!rows.Any(r => string.Equals(r.Name, col, StringComparison.OrdinalIgnoreCase)))
                            issues.Add(new Issue(Severity.Error, q, $"Index {idx.IndexName ?? "(unnamed)"} on {q} uses column \"{col}\", which doesn't exist."));
                }
            }

            var byName = tables.GroupBy(t => Qualified(t), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var edges = new List<(string from, string to)>();
            foreach (var t in tables)
            {
                var q = Qualified(t);
                foreach (var rf in t.References ?? new List<ReferenceOptions>())
                {
                    var label = $"{q}.{rf.ForeignKey} -> {rf.RefTable}.{rf.RefTableKey}";
                    var fkCol = (t.Rows ?? new()).FirstOrDefault(r => string.Equals(r.Name, rf.ForeignKey, StringComparison.OrdinalIgnoreCase));
                    if (fkCol.Name == null)
                        issues.Add(new Issue(Severity.Error, q, $"Reference {label}: {q} has no column \"{rf.ForeignKey}\"."));

                    if (string.IsNullOrWhiteSpace(rf.RefTable) || !byName.TryGetValue(rf.RefTable, out var target))
                    {
                        issues.Add(new Issue(Severity.Error, q, $"Reference {label}: table {rf.RefTable} doesn't exist."));
                        continue;
                    }
                    edges.Add((q, Qualified(target)));
                    var keyCol = (target.Rows ?? new()).FirstOrDefault(r => string.Equals(r.Name, rf.RefTableKey, StringComparison.OrdinalIgnoreCase));
                    if (keyCol.Name == null)
                    {
                        issues.Add(new Issue(Severity.Error, q, $"Reference {label}: {rf.RefTable} has no column \"{rf.RefTableKey}\"."));
                        continue;
                    }
                    if (!IsValidFkTarget(target, keyCol))
                        issues.Add(new Issue(Severity.Error, q, $"Reference {label}: {rf.RefTable}.{rf.RefTableKey} must be the primary key or Unique for a foreign key to point at it."));

                    if (fkCol.Name != null && fkCol.RowType != null && keyCol.RowType != null &&
                        StorageType(fkCol.RowType.Value) != StorageType(keyCol.RowType.Value))
                        issues.Add(new Issue(Severity.Error, q,
                            $"Reference {label}: types don't match ({fkCol.RowType} vs {keyCol.RowType}). The foreign key column should be {StorageType(keyCol.RowType.Value)}."));
                    if (fkCol.Name != null && fkCol.RowType is PT.Serial or PT.BigSerial or PT.SmallSerial)
                        issues.Add(new Issue(Severity.Warning, q, $"Reference {label}: \"{fkCol.Name}\" is a serial (auto-numbered) column; a foreign key should be a plain {StorageType(fkCol.RowType.Value)}."));
                    if (fkCol.Name != null && fkCol.IsNotNull == true && rf.OnDeleteAction == Reference.ReferentialAction.SetNull)
                        issues.Add(new Issue(Severity.Error, q, $"Reference {label}: ON DELETE SET NULL can't work because \"{fkCol.Name}\" is Not Null. Use Cascade/Restrict or allow nulls."));
                }
            }

            foreach (var cycle in FindCycles(edges))
                issues.Add(new Issue(Severity.Warning, cycle[0],
                    $"Foreign keys form a cycle ({string.Join(" -> ", cycle)} -> {cycle[0]}). Inserting the first row will need one of those columns to allow NULL."));

            if (!string.IsNullOrWhiteSpace(rlsJson))
            {
                try
                {
                    var data = System.Text.Json.JsonSerializer.Deserialize<RLSData>(rlsJson);
                    var rlsTables = RlsSqlGenerator.FromProject(tables);
                    foreach (var role in data?.Roles ?? new())
                        foreach (var tp in role.Tables ?? new())
                        {
                            if (tp.Policies == null || tp.Policies.Count == 0) continue;
                            var table = rlsTables.FirstOrDefault(x => x.Qualified.Equals(tp.TableName ?? "", StringComparison.OrdinalIgnoreCase))
                                     ?? rlsTables.FirstOrDefault(x => x.Name.Equals((tp.TableName ?? "").Split('.').Last(), StringComparison.OrdinalIgnoreCase));
                            if (table == null)
                            {
                                issues.Add(new Issue(Severity.Warning, null, $"RLS role \"{role.Name}\" has policies for \"{tp.TableName}\", which isn't a table in this project; RLS.sql will skip it."));
                                continue;
                            }
                            foreach (var p in tp.Policies)
                            {
                                var r = RlsSqlGenerator.Resolve(p, table);
                                bool needsOwner = r.Access is "Own rows" or "Public read, own writes";
                                if (needsOwner && (r.OwnerColumn == null || !table.Columns.Any(c => c.Name.Equals(r.OwnerColumn, StringComparison.OrdinalIgnoreCase))))
                                    issues.Add(new Issue(Severity.Warning, table.Qualified,
                                        $"RLS policy \"{p.Name}\" ({role.Name}) limits rows to their owner, but {table.Qualified} has no owner column; only admins will see rows. Set Owner Column in the RLS Editor."));
                            }
                        }
                }
                catch (Exception ex)
                {
                    issues.Add(new Issue(Severity.Warning, null, "Couldn't read the RLS Editor data: " + ex.Message));
                }
            }

            return issues
                .OrderBy(i => i.Severity)
                .ThenBy(i => i.Table ?? "")
                .ToList();
        }

        // A foreign key can only point at a column Postgres knows is unique:
        // the table's single-column primary key or a Unique column.
        public static bool IsValidFkTarget(TableObject target, RowCreation col) =>
            col.IsUnique == true || (col.IsPrimary == true && (target.Rows ?? new()).Count(r => r.IsPrimary == true) == 1);

        public static string StorageType(PT t) => t switch
        {
            PT.SmallSerial => "SmallInt",
            PT.Serial => "Integer",
            PT.BigSerial => "BigInt",
            _ => t.ToString()
        };

        private static string ToSnake(string s) =>
            Regex.Replace(s, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant();

        private static List<List<string>> FindCycles(List<(string from, string to)> edges)
        {
            var graph = edges.Where(e => !e.from.Equals(e.to, StringComparison.OrdinalIgnoreCase))
                .GroupBy(e => e.from, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(e => e.to).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var low = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<string>();
            var onStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<List<string>>();
            int counter = 0;

            void Visit(string v)
            {
                index[v] = low[v] = counter++;
                stack.Push(v); onStack.Add(v);
                foreach (var w in graph.TryGetValue(v, out var ws) ? ws : new List<string>())
                {
                    if (!index.ContainsKey(w)) { Visit(w); low[v] = Math.Min(low[v], low[w]); }
                    else if (onStack.Contains(w)) low[v] = Math.Min(low[v], index[w]);
                }
                if (low[v] != index[v]) return;
                var component = new List<string>();
                string x;
                do { x = stack.Pop(); onStack.Remove(x); component.Add(x); } while (!x.Equals(v, StringComparison.OrdinalIgnoreCase));
                if (component.Count > 1) { component.Reverse(); result.Add(component); }
            }

            foreach (var v in graph.Keys.ToList())
                if (!index.ContainsKey(v)) Visit(v);
            return result;
        }
    }
}
