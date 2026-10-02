using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DatabaseDesigner;
using static DatabaseDesigner.SessionStorage;
using PT = DatabaseDesigner.DBDesigner.PostgresType;

namespace Database_Designer
{
    public static class SqlImporter
    {
        public sealed class Result
        {
            public List<TableObject> Tables { get; } = new();
            public List<string> Warnings { get; } = new();
            public List<string> Skipped { get; } = new();
        }

        public static Result Parse(string sql, string defaultSchema = "public")
        {
            var result = new Result();
            var tables = new Dictionary<string, TableObject>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();

            foreach (var stmt in SplitStatements(sql ?? ""))
            {
                var tokens = Tokenize(stmt);
                if (tokens.Count == 0) continue;
                var head = string.Join(" ", tokens.Take(3).Select(t => t.Upper));
                try
                {
                    if (Is(tokens, 0, "CREATE") && FindKeyword(tokens, "TABLE", 1, 4) is int ti && !IsAny(tokens, 1, "VIEW", "FUNCTION"))
                    {
                        if (ContainsKeyword(tokens, "AS") && !tokens.Any(t => t.Text == "("))
                        { result.Skipped.Add(Short(stmt)); continue; }
                        var t = ParseCreateTable(tokens, ti, defaultSchema, result);
                        if (t is TableObject table)
                        {
                            var key = Key(table);
                            if (tables.ContainsKey(key)) result.Warnings.Add($"{key} is created twice; the second definition wins.");
                            else order.Add(key);
                            tables[key] = table;
                        }
                    }
                    else if (Is(tokens, 0, "CREATE") && (FindKeyword(tokens, "INDEX", 1, 3) is int ii))
                        ParseCreateIndex(tokens, ii, defaultSchema, tables, result);
                    else if (Is(tokens, 0, "ALTER") && Is(tokens, 1, "TABLE"))
                        ParseAlterTable(tokens, defaultSchema, tables, result);
                    else if (Is(tokens, 0, "COMMENT") && Is(tokens, 1, "ON"))
                        ParseComment(tokens, defaultSchema, tables);
                    else if (Is(tokens, 0, "CREATE") && Is(tokens, 1, "SCHEMA")) { /* schemas are implied by table names */ }
                    else if (head.StartsWith("SET ") || head.StartsWith("SELECT PG_CATALOG") || head.StartsWith("BEGIN") || head.StartsWith("COMMIT")) { }
                    else result.Skipped.Add(Short(stmt));
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Couldn't read \"{Short(stmt)}\": {ex.Message}");
                }
            }

            foreach (var key in order) result.Tables.Add(tables[key]);
            foreach (var t in result.Tables)
                if (!t.Rows.Any(r => r.IsPrimary == true))
                    result.Warnings.Add($"{Key(t)} has no primary key; add one before exporting (the API needs it).");
            return result;
        }


        private static TableObject? ParseCreateTable(List<Tok> tk, int tableKw, string defaultSchema, Result result)
        {
            int i = tableKw + 1;
            if (Is(tk, i, "IF")) i += 3;
            var (schema, name) = QualifiedName(tk, ref i, defaultSchema);
            if (i >= tk.Count || tk[i].Text != "(") throw new FormatException("expected ( after the table name");
            var body = Group(tk, ref i);

            var table = new TableObject
            {
                SchemaName = schema,
                TableName = name,
                Description = "",
                Rows = new List<RowCreation>(),
                References = new List<ReferenceOptions>(),
                Indexes = new List<IndexCreation>(),
                CustomRows = new List<string>()
            };
            var qualified = $"{schema}.{name}";

            foreach (var item in SplitTopLevel(body))
            {
                if (item.Count == 0) continue;
                int j = 0;
                string constraintName = null;
                if (Is(item, 0, "CONSTRAINT")) { constraintName = Ident(item[1]); j = 2; }

                if (Is(item, j, "PRIMARY") && Is(item, j + 1, "KEY"))
                {
                    int k = j + 2;
                    foreach (var col in ColumnList(item, ref k)) SetRow(table, col, r => { r.IsPrimary = true; r.IsNotNull = true; return r; });
                }
                else if (Is(item, j, "UNIQUE"))
                {
                    int k = j + 1;
                    var cols = ColumnList(item, ref k);
                    if (cols.Count == 1) SetRow(table, cols[0], r => { r.IsUnique = true; return r; });
                    else table.Indexes.Add(new IndexCreation { TableName = qualified, IndexName = constraintName ?? $"{name}_{string.Join("_", cols)}_key", ColumnNames = cols, IndexType = "Unique" });
                    if (cols.Count > 1) result.Warnings.Add($"{qualified}: multi-column UNIQUE ({string.Join(", ", cols)}) was imported as an index; check it.");
                }
                else if (Is(item, j, "FOREIGN") && Is(item, j + 1, "KEY"))
                {
                    int k = j + 2;
                    var cols = ColumnList(item, ref k);
                    AddForeignKey(table, qualified, cols, item, k, defaultSchema, result);
                }
                else if (Is(item, j, "CHECK"))
                {
                    int k = j + 1;
                    var expr = Text(Group(item, ref k));
                    var target = table.Rows.FirstOrDefault(r => Regex.IsMatch(expr, $@"\b{Regex.Escape(r.Name)}\b"));
                    if (target.Name != null && table.Rows.Count(r => Regex.IsMatch(expr, $@"\b{Regex.Escape(r.Name)}\b")) == 1)
                        SetRow(table, target.Name, r => { r.Check = Combine(r.Check, expr); return r; });
                    else result.Warnings.Add($"{qualified}: table CHECK ({expr}) spans several columns and was not imported.");
                }
                else if (Is(item, j, "EXCLUDE") || Is(item, j, "LIKE"))
                    result.Warnings.Add($"{qualified}: \"{Text(item)}\" isn't supported and was skipped.");
                else
                    ParseColumn(table, qualified, item, defaultSchema, result);
            }
            return table;
        }

        private static void ParseColumn(TableObject table, string qualified, List<Tok> item, string defaultSchema, Result result)
        {
            var name = Ident(item[0]);
            int i = 1;
            var typeToks = new List<Tok>();
            while (i < item.Count && !IsAny(item, i, "PRIMARY", "NOT", "NULL", "UNIQUE", "DEFAULT", "REFERENCES", "CHECK", "CONSTRAINT", "COLLATE", "GENERATED"))
                typeToks.Add(item[i++]);
            var (type, limit, isArray, typeWarning) = MapType(typeToks);
            if (typeWarning != null) result.Warnings.Add($"{qualified}.{name}: {typeWarning}");

            var row = new RowCreation
            {
                Name = name,
                Description = "",
                RowType = type,
                Limit = limit,
                IsArray = isArray,
                IsPrimary = false,
                IsUnique = false,
                IsNotNull = false,
                EncryptedAndNOTMedia = false,
                Media = false,
                DefaultIsPostgresFunction = false
            };

            while (i < item.Count)
            {
                if (Is(item, i, "CONSTRAINT")) { i += 2; continue; }
                if (Is(item, i, "PRIMARY")) { row.IsPrimary = true; row.IsNotNull = true; i += 2; continue; }
                if (Is(item, i, "NOT") && Is(item, i + 1, "NULL")) { row.IsNotNull = true; i += 2; continue; }
                if (Is(item, i, "NULL")) { i++; continue; }
                if (Is(item, i, "UNIQUE")) { row.IsUnique = true; i++; continue; }
                if (Is(item, i, "COLLATE")) { i += 2; continue; }
                if (Is(item, i, "CHECK")) { i++; row.Check = Combine(row.Check, Text(Group(item, ref i))); continue; }
                if (Is(item, i, "DEFAULT"))
                {
                    i++;
                    var def = new List<Tok>();
                    while (i < item.Count && !IsAny(item, i, "PRIMARY", "NOT", "NULL", "UNIQUE", "REFERENCES", "CHECK", "CONSTRAINT", "COLLATE", "GENERATED"))
                    {
                        if (item[i].Text == "(") def.AddRange(WithParens(item, ref i));
                        else def.Add(item[i++]);
                    }
                    (row.DefaultValue, row.DefaultIsPostgresFunction) = MapDefault(def);
                    continue;
                }
                if (Is(item, i, "REFERENCES"))
                {
                    AddForeignKey(table, qualified, new List<string> { name }, item, i, defaultSchema, result, rowOverride: true);
                    i = SkipReferences(item, i);
                    continue;
                }
                if (Is(item, i, "GENERATED"))
                {
                    int asAt = i;
                    while (asAt < item.Count && !Is(item, asAt, "AS")) asAt++;
                    if (Is(item, asAt + 1, "IDENTITY"))
                    {
                        row.RowType = row.RowType == PT.SmallInt ? PT.SmallSerial : row.RowType == PT.Integer ? PT.Serial : PT.BigSerial;
                        i = asAt + 2;
                        if (i < item.Count && item[i].Text == "(") Group(item, ref i);
                    }
                    else
                    {
                        result.Warnings.Add($"{qualified}.{name}: generated column expression was not imported.");
                        i = asAt + 1;
                        if (i < item.Count && item[i].Text == "(") Group(item, ref i);
                        if (Is(item, i, "STORED")) i++;
                    }
                    continue;
                }
                i++;
            }
            if (row.IsPrimary == true) { row.IsNotNull = true; }
            table.Rows.Add(row);
        }

        private static void AddForeignKey(TableObject table, string qualified, List<string> cols, List<Tok> tk, int k, string defaultSchema, Result result, bool rowOverride = false)
        {
            while (k < tk.Count && !Is(tk, k, "REFERENCES")) k++;
            if (k >= tk.Count) return;
            k++;
            var (rs, rn) = QualifiedName(tk, ref k, defaultSchema);
            var refCols = k < tk.Count && tk[k].Text == "(" ? ColumnList(tk, ref k) : new List<string> { "id" };
            var onDelete = Reference.ReferentialAction.NoAction;
            var onUpdate = Reference.ReferentialAction.NoAction;
            for (; k < tk.Count - 1; k++)
            {
                if (Is(tk, k, "ON") && Is(tk, k + 1, "DELETE")) onDelete = Action(tk, k + 2);
                if (Is(tk, k, "ON") && Is(tk, k + 1, "UPDATE")) onUpdate = Action(tk, k + 2);
            }
            if (cols.Count != refCols.Count || cols.Count == 0) { result.Warnings.Add($"{qualified}: foreign key column count mismatch; skipped."); return; }
            if (cols.Count > 1) result.Warnings.Add($"{qualified}: composite foreign key ({string.Join(", ", cols)}) was imported as {cols.Count} single-column references; check it.");
            for (int c = 0; c < cols.Count; c++)
            {
                if (table.References.Any(r => r.ForeignKey == cols[c] && string.Equals(r.RefTable, $"{rs}.{rn}", StringComparison.OrdinalIgnoreCase) && r.RefTableKey == refCols[c]))
                    continue;
                table.References.Add(new ReferenceOptions(qualified, $"{rs}.{rn}", cols[c], refCols[c], onDelete, onUpdate));
            }
        }

        private static int SkipReferences(List<Tok> tk, int i)
        {
            i++;
            while (i < tk.Count && !IsAny(tk, i, "PRIMARY", "NOT", "NULL", "UNIQUE", "DEFAULT", "CHECK", "CONSTRAINT", "COLLATE", "GENERATED")) i++;
            return i;
        }

        private static Reference.ReferentialAction Action(List<Tok> tk, int i) =>
            Is(tk, i, "CASCADE") ? Reference.ReferentialAction.Cascade
            : Is(tk, i, "RESTRICT") ? Reference.ReferentialAction.Restrict
            : Is(tk, i, "SET") && Is(tk, i + 1, "NULL") ? Reference.ReferentialAction.SetNull
            : Is(tk, i, "SET") && Is(tk, i + 1, "DEFAULT") ? Reference.ReferentialAction.SetDefault
            : Reference.ReferentialAction.NoAction;


        private static void ParseAlterTable(List<Tok> tk, string defaultSchema, Dictionary<string, TableObject> tables, Result result)
        {
            int i = 2;
            if (Is(tk, i, "IF")) i += 2;
            if (Is(tk, i, "ONLY")) i++;
            var (s, n) = QualifiedName(tk, ref i, defaultSchema);
            if (!tables.TryGetValue($"{s}.{n}", out var table))
            {
                result.Skipped.Add(Short(Text(tk)));
                return;
            }
            var qualified = $"{s}.{n}";
            if (!Is(tk, i, "ADD")) { result.Skipped.Add(Short(Text(tk))); return; }
            i++;
            if (Is(tk, i, "CONSTRAINT")) i += 2;
            if (Is(tk, i, "FOREIGN") && Is(tk, i + 1, "KEY"))
            {
                int k = i + 2;
                AddForeignKey(table, qualified, ColumnList(tk, ref k), tk, k, defaultSchema, result);
            }
            else if (Is(tk, i, "PRIMARY") && Is(tk, i + 1, "KEY"))
            {
                int k = i + 2;
                foreach (var col in ColumnList(tk, ref k)) SetRow(table, col, r => { r.IsPrimary = true; r.IsNotNull = true; return r; });
            }
            else if (Is(tk, i, "UNIQUE"))
            {
                int k = i + 1;
                var cols = ColumnList(tk, ref k);
                if (cols.Count == 1) SetRow(table, cols[0], r => { r.IsUnique = true; return r; });
                else table.Indexes.Add(new IndexCreation { TableName = qualified, IndexName = $"{n}_{string.Join("_", cols)}_key", ColumnNames = cols, IndexType = "Unique" });
            }
            else if (Is(tk, i, "COLUMN") || (i < tk.Count && tk[i].Kind == TokKind.Word && !IsAny(tk, i, "CHECK")))
            {
                if (Is(tk, i, "COLUMN")) i++;
                if (Is(tk, i, "IF")) i += 3;
                ParseColumn(table, qualified, tk.Skip(i).ToList(), defaultSchema, result);
            }
            else result.Skipped.Add(Short(Text(tk)));
            tables[qualified] = table;
        }

        private static void ParseCreateIndex(List<Tok> tk, int indexKw, string defaultSchema, Dictionary<string, TableObject> tables, Result result)
        {
            bool unique = tk.Take(indexKw).Any(t => t.Upper == "UNIQUE");
            int i = indexKw + 1;
            if (Is(tk, i, "CONCURRENTLY")) i++;
            if (Is(tk, i, "IF")) i += 3;
            string indexName = null;
            if (!Is(tk, i, "ON")) indexName = Ident(tk[i++]);
            if (!Is(tk, i, "ON")) throw new FormatException("expected ON");
            i++;
            if (Is(tk, i, "ONLY")) i++;
            var (s, n) = QualifiedName(tk, ref i, defaultSchema);
            if (!tables.TryGetValue($"{s}.{n}", out var table)) { result.Skipped.Add(Short(Text(tk))); return; }
            string method = null;
            if (Is(tk, i, "USING")) { method = tk[i + 1].Text.ToLowerInvariant(); i += 2; }
            var inner = Group(tk, ref i);
            string where = null;
            if (Is(tk, i, "WHERE")) where = Text(tk.Skip(i + 1).ToList());

            var parts = SplitTopLevel(inner);
            bool plainColumns = parts.All(p => p.Count >= 1 && p[0].Kind is TokKind.Word or TokKind.Quoted && p.Skip(1).All(x => IsAny(new List<Tok> { x }, 0, "ASC", "DESC", "NULLS", "FIRST", "LAST") || x.Kind == TokKind.Word && x.Upper.EndsWith("_OPS")));
            var cols = parts.Select(p => Ident(p[0])).ToList();
            bool pathOps = parts.Any(p => p.Any(x => x.Upper == "JSONB_PATH_OPS"));
            var name = indexName ?? $"{n}_{string.Join("_", cols)}_idx";
            var idx = new IndexCreation { TableName = $"{s}.{n}", IndexName = name, ColumnNames = cols, UseJsonbPathOps = pathOps };

            if (!plainColumns)
            {
                idx.IndexType = "Expression";
                idx.Expression = Text(inner);
                idx.ColumnNames = new List<string>();
            }
            else if (unique) idx.IndexType = "Unique";
            else if (where != null) { idx.IndexType = "Partial"; idx.Condition = where; }
            else if (method == "gin") idx.IndexType = "Gin";
            else if (method == "hash") idx.IndexType = "Hash";
            else if (method != null && method != "btree") { idx.IndexType = "Custom"; idx.IndexTypeCustom = method; }
            else idx.IndexType = cols.Count > 1 ? "Composite" : "Basic";

            if (unique && cols.Count == 1 && where == null && plainColumns)
                SetRow(table, cols[0], r => { r.IsUnique = true; return r; });
            else
                table.Indexes.Add(idx);
            tables[$"{s}.{n}"] = table;
        }

        private static void ParseComment(List<Tok> tk, string defaultSchema, Dictionary<string, TableObject> tables)
        {
            int isIdx = tk.FindIndex(t => t.Upper == "IS");
            if (isIdx < 0 || isIdx + 1 >= tk.Count || tk[isIdx + 1].Kind != TokKind.String) return;
            var text = tk[isIdx + 1].Text;
            int i = 3;
            if (Is(tk, 2, "TABLE"))
            {
                var (s, n) = QualifiedName(tk, ref i, defaultSchema);
                if (tables.TryGetValue($"{s}.{n}", out var t)) { t.Description = text; tables[$"{s}.{n}"] = t; }
            }
            else if (Is(tk, 2, "COLUMN"))
            {
                var parts = new List<string>();
                while (i < isIdx) { if (tk[i].Text != ".") parts.Add(Ident(tk[i])); i++; }
                if (parts.Count < 2) return;
                var col = parts[^1];
                var tname = parts[^2];
                var schema = parts.Count >= 3 ? parts[^3] : defaultSchema;
                if (tables.TryGetValue($"{schema}.{tname}", out var t))
                {
                    SetRow(t, col, r => { r.Description = text; return r; });
                    tables[$"{schema}.{tname}"] = t;
                }
            }
        }


        private static (PT? type, int? limit, bool isArray, string warning) MapType(List<Tok> tk)
        {
            var words = new List<string>();
            var args = new List<int>();
            bool isArray = false;
            for (int i = 0; i < tk.Count; i++)
            {
                if (tk[i].Text == "(")
                {
                    var inner = Group(tk, ref i); i--;
                    foreach (var a in inner.Where(x => x.Kind == TokKind.Number)) args.Add(int.Parse(a.Text));
                }
                else if (tk[i].Text == "[") { isArray = true; while (i < tk.Count && tk[i].Text != "]") i++; }
                else if (tk[i].Upper == "ARRAY") isArray = true;
                else if (tk[i].Text != "." && tk[i].Text != "]") words.Add(tk[i].Upper);
            }
            if (words.Count > 1 && words[0] is "PG_CATALOG" or "PUBLIC") words.RemoveAt(0);
            var t = string.Join(" ", words);
            int? one = args.Count > 0 ? args[0] : null;

            (PT? type, int? limit) mapped = t switch
            {
                "SMALLINT" or "INT2" => (PT.SmallInt, null),
                "INTEGER" or "INT" or "INT4" => (PT.Integer, null),
                "BIGINT" or "INT8" => (PT.BigInt, null),
                "SMALLSERIAL" or "SERIAL2" => (PT.SmallSerial, null),
                "SERIAL" or "SERIAL4" => (PT.Serial, null),
                "BIGSERIAL" or "SERIAL8" => (PT.BigSerial, null),
                "REAL" or "FLOAT4" => (PT.Real, null),
                "DOUBLE PRECISION" or "FLOAT8" or "FLOAT" => (PT.DoublePrecision, null),
                "NUMERIC" or "DECIMAL" => (PT.Numeric, args.Count >= 2 ? Row.LimitEncoder.Encode((args[0], args[1])) : one),
                "MONEY" => (PT.Money, null),
                "BOOLEAN" or "BOOL" => (PT.Boolean, null),
                "CHAR" or "CHARACTER" or "BPCHAR" => (PT.Char, one),
                "VARCHAR" or "CHARACTER VARYING" => (PT.VarChar, one),
                "TEXT" or "CITEXT" or "NAME" => (PT.Text, null),
                "BYTEA" => (PT.Bytea, null),
                "DATE" => (PT.Date, null),
                "TIME" or "TIME WITHOUT TIME ZONE" => (PT.Time, one),
                "TIMETZ" or "TIME WITH TIME ZONE" => (PT.TimeTz, null),
                "TIMESTAMP" or "TIMESTAMP WITHOUT TIME ZONE" => (PT.Timestamp, one),
                "TIMESTAMPTZ" or "TIMESTAMP WITH TIME ZONE" => (PT.TimestampTz, null),
                "INTERVAL" => (PT.Interval, null),
                "JSON" => (PT.Json, null),
                "JSONB" => (PT.Jsonb, null),
                "INET" => (PT.Inet, null),
                "CIDR" => (PT.Cidr, null),
                "MACADDR" => (PT.MacAddr, null),
                "UUID" => (PT.Uuid, null),
                "XML" => (PT.Xml, null),
                "TSVECTOR" => (PT.TsVector, null),
                "TSQUERY" => (PT.TsQuery, null),
                "POINT" => (PT.Point, null),
                "INT4RANGE" => (PT.Int4RangeBase, null),
                "INT8RANGE" => (PT.Int8RangeBase, null),
                "NUMRANGE" => (PT.NumRangeBase, null),
                "TSRANGE" => (PT.TsRangeBase, null),
                "TSTZRANGE" => (PT.TstzRangeBase, null),
                "DATERANGE" => (PT.DateRangeBase, null),
                _ => (null, null)
            };
            if (mapped.type == null)
                return (PT.Text, null, isArray, $"type \"{t.ToLowerInvariant()}\" isn't supported by the designer; imported as text.");
            return (mapped.type, mapped.limit, isArray, null);
        }

        private static (string value, bool isFunction) MapDefault(List<Tok> def)
        {
            if (def.Count == 0) return (null, false);
            if (def[0].Kind == TokKind.String && (def.Count == 1 || def[1].Text == "::"))
                return (def[0].Text, false);
            if (def.Count == 1 && (def[0].Kind == TokKind.Number || def[0].Upper is "TRUE" or "FALSE"))
                return (def[0].Text.ToLowerInvariant(), false);
            if (def.Count == 2 && def[0].Text == "-" && def[1].Kind == TokKind.Number)
                return ("-" + def[1].Text, false);
            return (Text(def), true);
        }


        private static string Key(TableObject t) => $"{t.SchemaName}.{t.TableName}";

        private static string Ident(Tok t) => t.Kind == TokKind.Word ? t.Text.ToLowerInvariant() : t.Text;

        private static void SetRow(TableObject table, string col, Func<RowCreation, RowCreation> change)
        {
            var idx = table.Rows.FindIndex(r => string.Equals(r.Name, col, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) table.Rows[idx] = change(table.Rows[idx]);
        }

        private static string Combine(string a, string b) => string.IsNullOrWhiteSpace(a) ? b : $"({a}) AND ({b})";

        private static (string schema, string name) QualifiedName(List<Tok> tk, ref int i, string defaultSchema)
        {
            var first = Ident(tk[i++]);
            if (i < tk.Count && tk[i].Text == ".") { i++; return (first, Ident(tk[i++])); }
            return (defaultSchema, first);
        }

        private static List<string> ColumnList(List<Tok> tk, ref int i)
        {
            while (i < tk.Count && tk[i].Text != "(") i++;
            if (i >= tk.Count) return new List<string>();
            return SplitTopLevel(Group(tk, ref i)).Where(p => p.Count > 0).Select(p => Ident(p[0])).ToList();
        }

        private static List<Tok> Group(List<Tok> tk, ref int i)
        {
            if (tk[i].Text != "(") throw new FormatException("expected (");
            int depth = 0, start = i + 1;
            for (; i < tk.Count; i++)
            {
                if (tk[i].Text == "(") depth++;
                else if (tk[i].Text == ")" && --depth == 0) { var inner = tk.GetRange(start, i - start); i++; return inner; }
            }
            throw new FormatException("unbalanced parentheses");
        }

        private static List<Tok> WithParens(List<Tok> tk, ref int i)
        {
            int start = i;
            Group(tk, ref i);
            return tk.GetRange(start, i - start);
        }

        private static List<List<Tok>> SplitTopLevel(List<Tok> tk)
        {
            var parts = new List<List<Tok>> { new() };
            int depth = 0;
            foreach (var t in tk)
            {
                if (t.Text == "(") depth++;
                if (t.Text == ")") depth--;
                if (t.Text == "," && depth == 0) { parts.Add(new()); continue; }
                parts[^1].Add(t);
            }
            return parts.Where(p => p.Count > 0).ToList();
        }

        private static bool Is(List<Tok> tk, int i, string word) => i >= 0 && i < tk.Count && tk[i].Kind == TokKind.Word && tk[i].Upper == word;
        private static bool IsAny(List<Tok> tk, int i, params string[] words) => words.Any(w => Is(tk, i, w));
        private static bool ContainsKeyword(List<Tok> tk, string w) => tk.Any(t => t.Kind == TokKind.Word && t.Upper == w);
        private static int? FindKeyword(List<Tok> tk, string w, int from, int to)
        {
            for (int i = from; i <= to && i < tk.Count; i++) if (Is(tk, i, w)) return i;
            return null;
        }

        private static string Text(List<Tok> tk)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < tk.Count; i++)
            {
                var t = tk[i];
                var s = t.Kind switch
                {
                    TokKind.String => "'" + t.Text.Replace("'", "''") + "'",
                    TokKind.Quoted => "\"" + t.Text + "\"",
                    _ => t.Text
                };
                bool tight = i == 0 || t.Text is ")" or "," or "." or "::" or "]" || tk[i - 1].Text is "(" or "." or "::" or "[";
                if (t.Text == "(" && i > 0 && tk[i - 1].Kind == TokKind.Word) tight = true;
                if (!tight) sb.Append(' ');
                sb.Append(s);
            }
            return sb.ToString();
        }

        private static string Short(string s)
        {
            s = Regex.Replace(s.Trim(), @"\s+", " ");
            return s.Length > 80 ? s.Substring(0, 80) + "…" : s;
        }


        private enum TokKind { Word, Quoted, String, Number, Symbol }

        private sealed class Tok
        {
            public TokKind Kind;
            public string Text;
            public string Upper => Kind == TokKind.Word ? Text.ToUpperInvariant() : Text;
            public override string ToString() => Text;
        }

        public static List<string> SplitStatements(string sql)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            for (int i = 0; i < sql.Length; i++)
            {
                char c = sql[i];
                if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-') { while (i < sql.Length && sql[i] != '\n') i++; sb.Append('\n'); continue; }
                if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
                {
                    int depth = 1; i += 2;
                    while (i < sql.Length && depth > 0)
                    {
                        if (sql[i] == '/' && i + 1 < sql.Length && sql[i + 1] == '*') { depth++; i++; }
                        else if (sql[i] == '*' && i + 1 < sql.Length && sql[i + 1] == '/') { depth--; i++; }
                        i++;
                    }
                    i--; sb.Append(' '); continue;
                }
                if (c == '\'' || c == '"')
                {
                    sb.Append(c); i++;
                    while (i < sql.Length)
                    {
                        sb.Append(sql[i]);
                        if (sql[i] == c) { if (i + 1 < sql.Length && sql[i + 1] == c) { sb.Append(sql[++i]); } else break; }
                        i++;
                    }
                    continue;
                }
                if (c == '$')
                {
                    var m = Regex.Match(sql.Substring(i), @"^\$[A-Za-z_]*\$");
                    if (m.Success)
                    {
                        var tag = m.Value;
                        var end = sql.IndexOf(tag, i + tag.Length, StringComparison.Ordinal);
                        end = end < 0 ? sql.Length : end + tag.Length;
                        sb.Append(sql, i, end - i);
                        i = end - 1;
                        continue;
                    }
                }
                if (c == ';') { if (sb.ToString().Trim().Length > 0) list.Add(sb.ToString()); sb.Clear(); continue; }
                sb.Append(c);
            }
            if (sb.ToString().Trim().Length > 0) list.Add(sb.ToString());
            return list;
        }

        private static List<Tok> Tokenize(string s)
        {
            var tk = new List<Tok>();
            for (int i = 0; i < s.Length;)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (c == '\'' || (c is 'E' or 'e' && i + 1 < s.Length && s[i + 1] == '\''))
                {
                    if (c != '\'') i++;
                    var sb = new StringBuilder(); i++;
                    while (i < s.Length) { if (s[i] == '\'') { if (i + 1 < s.Length && s[i + 1] == '\'') { sb.Append('\''); i += 2; continue; } i++; break; } sb.Append(s[i++]); }
                    tk.Add(new Tok { Kind = TokKind.String, Text = sb.ToString() });
                    continue;
                }
                if (c == '"')
                {
                    var sb = new StringBuilder(); i++;
                    while (i < s.Length) { if (s[i] == '"') { if (i + 1 < s.Length && s[i + 1] == '"') { sb.Append('"'); i += 2; continue; } i++; break; } sb.Append(s[i++]); }
                    tk.Add(new Tok { Kind = TokKind.Quoted, Text = sb.ToString() });
                    continue;
                }
                if (c == '$' && Regex.Match(s.Substring(i), @"^\$[A-Za-z_]*\$") is { Success: true } dm)
                {
                    var end = s.IndexOf(dm.Value, i + dm.Length, StringComparison.Ordinal);
                    end = end < 0 ? s.Length : end + dm.Length;
                    tk.Add(new Tok { Kind = TokKind.String, Text = s.Substring(i, end - i) });
                    i = end;
                    continue;
                }
                if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
                {
                    int st = i; while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] is 'e' or 'E')) i++;
                    tk.Add(new Tok { Kind = TokKind.Number, Text = s.Substring(st, i - st) });
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int st = i; while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] is '_' or '$')) i++;
                    tk.Add(new Tok { Kind = TokKind.Word, Text = s.Substring(st, i - st) });
                    continue;
                }
                if (c == ':' && i + 1 < s.Length && s[i + 1] == ':') { tk.Add(new Tok { Kind = TokKind.Symbol, Text = "::" }); i += 2; continue; }
                foreach (var op in new[] { "<=", ">=", "<>", "!=", "||" })
                    if (string.CompareOrdinal(s, i, op, 0, 2) == 0) { tk.Add(new Tok { Kind = TokKind.Symbol, Text = op }); i += 2; goto next; }
                tk.Add(new Tok { Kind = TokKind.Symbol, Text = c.ToString() });
                i++;
            next:;
            }
            return tk;
        }
    }
}
