using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static DatabaseDesigner.Index;
using static DatabaseDesigner.Reference;
using static DatabaseDesigner.Row;

namespace Database_Designer
{
    public static class MigrationGenerator
    {
        public const string SnapshotFile = "schema.snapshot.json";

        public sealed class Snapshot
        {
            public int Format { get; set; } = 1;
            public List<TableSnap> Tables { get; set; } = new();
        }

        public sealed class TableSnap
        {
            public string Schema { get; set; }
            public string Name { get; set; }
            public List<ColumnSnap> Columns { get; set; } = new();
            public List<NamedSql> ForeignKeys { get; set; } = new();
            public List<NamedSql> Indexes { get; set; } = new();
            public string CreateSql { get; set; }
            public string Key => $"{Schema}.{Name}".ToLowerInvariant();
            public string Sql => string.IsNullOrEmpty(Schema) ? Q(Name) : $"{Q(Schema)}.{Q(Name)}";
        }

        public sealed class ColumnSnap
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public bool Primary { get; set; }
            public bool NotNull { get; set; }
            public bool Unique { get; set; }
            public string Default { get; set; }
            public string Check { get; set; }
            public string Definition { get; set; }
        }

        public sealed class NamedSql
        {
            public string Name { get; set; }
            public string Sql { get; set; }
        }


        public static TableSnap SnapshotTable(string qualifiedName, List<RowOptions> rows, List<ReferenceOptions> references, List<IndexDefinition> indexes, string createSql)
        {
            var parts = qualifiedName.Split('.');
            var snap = new TableSnap
            {
                Schema = parts.Length > 1 ? parts[0] : null,
                Name = parts.Last(),
                CreateSql = createSql
            };
            foreach (var r in rows ?? new List<RowOptions>())
                snap.Columns.Add(SnapshotColumn(r));
            foreach (var rf in references ?? new List<ReferenceOptions>())
            {
                var sql = ReferenceCreator(rf).sql;
                snap.ForeignKeys.Add(new NamedSql { Name = $"fk_{rf.MainTable.Replace(".", "_")}_{rf.ForeignKey}_to_{rf.RefTable.Replace(".", "_")}", Sql = sql });
            }
            foreach (var idx in indexes ?? new List<IndexDefinition>())
            {
                try
                {
                    var sql = CreateIndex(idx, idx.IndexName).Sql;
                    var m = Regex.Match(sql, @"INDEX\s+(\S+)\s+ON", RegexOptions.IgnoreCase);
                    snap.Indexes.Add(new NamedSql { Name = m.Success ? m.Groups[1].Value : idx.IndexName, Sql = sql });
                }
                catch { /* malformed index definitions are reported by the validator */ }
            }
            return snap;
        }

        private static ColumnSnap SnapshotColumn(RowOptions r)
        {
            var type = (r.PostgresType is { } pt ? SqlTypeName(pt) : null) ?? (string.IsNullOrEmpty(r.CustomType) ? null : r.CustomType)
                       ?? (r.IsEncrypted ? "TEXT" : r.IsMedia ? "SecureMedia" : "TEXT");
            type = TypeWithLimit(type, r.Limit);
            var checks = new List<string>();
            if (r.IsArray)
            {
                type += "[]";
                if (r.ArrayLimit.HasValue) checks.Add($"cardinality({r.FieldName}) <= {r.ArrayLimit}");
            }
            if (!string.IsNullOrWhiteSpace(r.Check)) checks.Add(r.Check);

            string def = null;
            if (r.DefaultValue != null)
                def = r.DefaultIsKeyword == true ? r.DefaultValue : "'" + r.DefaultValue.Replace("'", "''") + "'";

            string definition;
            try { definition = RowCreator(r).Trim(); }
            catch { definition = $"{r.FieldName} {type}"; }

            return new ColumnSnap
            {
                Name = r.FieldName,
                Type = type,
                Primary = r.IsPrimary,
                NotNull = !r.IsPrimary && r.IsNotNull,
                Unique = !r.IsPrimary && r.IsUnique,
                Default = def,
                Check = checks.Count == 0 ? null : string.Join(" AND ", checks),
                Definition = definition
            };
        }

        public static void Save(Snapshot snapshot, string folder) =>
            File.WriteAllText(Path.Combine(folder, SnapshotFile), JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));

        public static (Snapshot snapshot, string version) LoadPrevious(string generatedDbRoot, string currentVersion)
        {
            int Num(string v) => int.TryParse(v.TrimStart('v'), out var n) ? n : -1;
            var current = Num(currentVersion);
            if (!Directory.Exists(generatedDbRoot)) return (null, null);
            foreach (var dir in Directory.GetDirectories(generatedDbRoot)
                         .Select(Path.GetFileName)
                         .Where(n => n.StartsWith("v") && Num(n) >= 0 && Num(n) < current)
                         .OrderByDescending(Num))
            {
                var file = Path.Combine(generatedDbRoot, dir, SnapshotFile);
                if (!File.Exists(file)) continue;
                try { return (JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(file)), dir); }
                catch { }
            }
            return (null, null);
        }


        public static string Generate(Snapshot previous, Snapshot next, string fromVersion, string toVersion)
        {
            var sb = new StringBuilder();
            sb.AppendLine("-- ============================================================");
            if (previous == null)
            {
                sb.AppendLine($"-- Migration: none needed for {toVersion}");
                sb.AppendLine("-- This is the first export with a schema snapshot. For a new");
                sb.AppendLine("-- database run SQL.sql (then RLS.sql). The next export will");
                sb.AppendLine("-- contain a Migration.sql that upgrades a database built from this one.");
                sb.AppendLine("-- ============================================================");
                return sb.ToString();
            }

            var body = new StringBuilder();
            var post = new StringBuilder();
            var manual = new List<string>();
            int changes = 0;

            var prevByKey = previous.Tables.GroupBy(t => t.Key).ToDictionary(g => g.Key, g => g.First());
            var nextByKey = next.Tables.GroupBy(t => t.Key).ToDictionary(g => g.Key, g => g.First());

            foreach (var schema in next.Tables.Select(t => t.Schema).Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase)
                         .Where(s => !previous.Tables.Any(p => string.Equals(p.Schema, s, StringComparison.OrdinalIgnoreCase))))
            {
                body.AppendLine($"CREATE SCHEMA IF NOT EXISTS {Q(schema)};");
                changes++;
            }

            foreach (var t in next.Tables.Where(t => !prevByKey.ContainsKey(t.Key)))
            {
                body.AppendLine($"-- New table {t.Schema}.{t.Name}");
                body.AppendLine(StripSchemaCreate(t.CreateSql).Trim());
                body.AppendLine();
                changes++;
            }

            foreach (var t in next.Tables.Where(t => prevByKey.ContainsKey(t.Key)))
            {
                var p = prevByKey[t.Key];
                var tb = new StringBuilder();

                foreach (var fk in p.ForeignKeys.Where(f => !t.ForeignKeys.Any(n => n.Name == f.Name && Norm(n.Sql) == Norm(f.Sql))))
                    tb.AppendLine($"ALTER TABLE {t.Sql} DROP CONSTRAINT IF EXISTS {Q(fk.Name)};");
                foreach (var ix in p.Indexes.Where(i => !t.Indexes.Any(n => n.Name == i.Name && Norm(n.Sql) == Norm(i.Sql))))
                    tb.AppendLine($"DROP INDEX IF EXISTS {(string.IsNullOrEmpty(t.Schema) ? "" : Q(t.Schema) + ".")}{ix.Name};");

                foreach (var c in t.Columns)
                {
                    var old = p.Columns.FirstOrDefault(x => string.Equals(x.Name, c.Name, StringComparison.OrdinalIgnoreCase));
                    if (old == null)
                    {
                        tb.AppendLine($"ALTER TABLE {t.Sql} ADD COLUMN {c.Definition};");
                        if (c.NotNull && c.Default == null)
                            tb.AppendLine($"-- NOTE: \"{c.Name}\" is NOT NULL without a default; this fails if {t.Name} already has rows. Add a default, or add it nullable, fill it, then SET NOT NULL.");
                        if (c.Primary)
                            manual.Add($"{t.Schema}.{t.Name}: new column \"{c.Name}\" is a primary key; check the table's existing primary key.");
                        continue;
                    }
                    ColumnChanges(tb, t, old, c, manual);
                }

                foreach (var old in p.Columns.Where(o => !t.Columns.Any(c => string.Equals(c.Name, o.Name, StringComparison.OrdinalIgnoreCase))))
                    tb.AppendLine($"-- DESTRUCTIVE (uncomment to apply; if this was a rename, use RENAME COLUMN instead):\n-- ALTER TABLE {t.Sql} DROP COLUMN {old.Name};");

                foreach (var fk in t.ForeignKeys.Where(f => !p.ForeignKeys.Any(o => o.Name == f.Name && Norm(o.Sql) == Norm(f.Sql))))
                    post.AppendLine(fk.Sql);
                foreach (var ix in t.Indexes.Where(i => !p.Indexes.Any(o => o.Name == i.Name && Norm(o.Sql) == Norm(i.Sql))))
                    post.AppendLine(ix.Sql);

                if (tb.Length > 0)
                {
                    body.AppendLine($"-- Changes to {t.Schema}.{t.Name}");
                    body.Append(tb);
                    body.AppendLine();
                    changes++;
                }
            }

            foreach (var p in previous.Tables.Where(p => !nextByKey.ContainsKey(p.Key)))
            {
                body.AppendLine($"-- DESTRUCTIVE (uncomment to apply): {p.Schema}.{p.Name} was removed from the design.");
                body.AppendLine($"-- DROP TABLE IF EXISTS {p.Sql} CASCADE;");
                body.AppendLine();
                changes++;
            }

            if (post.Length > 0) changes++;

            sb.AppendLine($"-- Migration {fromVersion} to {toVersion}   (generated by Database Designer)");
            sb.AppendLine($"-- Upgrades a database built from {fromVersion} to match {toVersion}'s SQL.sql.");
            sb.AppendLine("-- Runs in one transaction: if any statement fails, nothing is changed.");
            sb.AppendLine("-- Lines marked DESTRUCTIVE are commented out on purpose; they delete data.");
            sb.AppendLine("-- Afterwards re-run RLS.sql (it is safe to run repeatedly).");
            sb.AppendLine("-- ============================================================");
            if (changes == 0)
            {
                sb.AppendLine("-- No schema changes since " + fromVersion + ".");
                return sb.ToString();
            }
            if (manual.Count > 0)
            {
                sb.AppendLine("-- Needs a manual look:");
                foreach (var m in manual) sb.AppendLine("--   * " + m);
            }
            sb.AppendLine();
            sb.AppendLine("BEGIN;");
            sb.AppendLine();
            sb.Append(body);
            if (post.Length > 0)
            {
                sb.AppendLine("-- New/changed foreign keys and indexes");
                sb.Append(post);
                sb.AppendLine();
            }
            sb.AppendLine("COMMIT;");
            return sb.ToString();
        }

        private static void ColumnChanges(StringBuilder tb, TableSnap t, ColumnSnap old, ColumnSnap c, List<string> manual)
        {
            var col = $"ALTER TABLE {t.Sql} ALTER COLUMN {c.Name}";
            if (!string.Equals(old.Type, c.Type, StringComparison.OrdinalIgnoreCase))
            {
                var target = SerialStorage(c.Type);
                if (IsSerial(c.Type) && !IsSerial(old.Type))
                    manual.Add($"{t.Schema}.{t.Name}.{c.Name}: changed to {c.Type}; a serial needs a sequence; create one and set the column default.");
                tb.AppendLine($"{col} TYPE {target} USING {c.Name}::{target};");
            }
            if (old.NotNull != c.NotNull)
                tb.AppendLine($"{col} {(c.NotNull ? "SET" : "DROP")} NOT NULL;");
            if (old.Default != c.Default)
                tb.AppendLine(c.Default == null ? $"{col} DROP DEFAULT;" : $"{col} SET DEFAULT {c.Default};");
            if (old.Unique != c.Unique)
                tb.AppendLine(c.Unique
                    ? $"ALTER TABLE {t.Sql} ADD CONSTRAINT {Q(ConstraintName(t.Name, c.Name, "key"))} UNIQUE ({c.Name});"
                    : $"ALTER TABLE {t.Sql} DROP CONSTRAINT IF EXISTS {Q(ConstraintName(t.Name, c.Name, "key"))};");
            if (old.Check != c.Check)
            {
                tb.AppendLine($"ALTER TABLE {t.Sql} DROP CONSTRAINT IF EXISTS {Q(ConstraintName(t.Name, c.Name, "check"))};");
                if (c.Check != null)
                    tb.AppendLine($"ALTER TABLE {t.Sql} ADD CONSTRAINT {Q(ConstraintName(t.Name, c.Name, "check"))} CHECK ({c.Check});");
            }
            if (old.Primary != c.Primary)
                manual.Add($"{t.Schema}.{t.Name}.{c.Name}: primary key {(c.Primary ? "added" : "removed")}; change the table's primary key by hand (DROP CONSTRAINT {ConstraintName(t.Name, null, "pkey")} / ADD PRIMARY KEY).");
        }

        private static string ConstraintName(string table, string column, string suffix)
        {
            var name = column == null ? $"{table}_{suffix}" : $"{table}_{column}_{suffix}";
            return name.Length > 63 ? name.Substring(0, 63) : name;
        }

        private static bool IsSerial(string type) => type != null && type.EndsWith("Serial", StringComparison.OrdinalIgnoreCase);

        private static string SerialStorage(string type) => type?.ToLowerInvariant() switch
        {
            "smallserial" => "SmallInt",
            "serial" => "Integer",
            "bigserial" => "BigInt",
            _ => type
        };

        private static string StripSchemaCreate(string sql) =>
            Regex.Replace(sql ?? "", @"^CREATE SCHEMA IF NOT EXISTS [^;]+;\s*", "", RegexOptions.Multiline);

        private static string Norm(string s) => Regex.Replace(s ?? "", @"\s+", " ").Trim();

        private static string Q(string ident) => "\"" + (ident ?? "").Replace("\"", "\"\"") + "\"";
    }
}
