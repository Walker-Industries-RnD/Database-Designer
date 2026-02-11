using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static DatabaseDesigner.Index;
using static DatabaseDesigner.Reference;
using static DatabaseDesigner.Row;

namespace DatabaseDesigner
{
    public class DBDesigner
    {

        //GODDAMN i'm praying I never have to touch ts

        //I mean do you have any clue how hard making ts is? I had to remake it a few times and couldn't even "JuSt Ai CoDe It BrO" BECAUSE AI WOULD SCREW IT UP MORE 
        //Well not to say it was useless; it did pick up on errors that were there before I eventually got this to work
        public enum PostgresType
        {
            // Integer family
            SmallInt, Integer, BigInt, SmallSerial, Serial, BigSerial,
            // Floating-point & exact
            Real, DoublePrecision, Numeric, Decimal = Numeric,
            Money,
            Boolean,
            Char, VarChar, Text,
            Bytea,
            Date, Time, TimeTz, Timestamp, TimestampTz, Interval,
            Json, Jsonb,
            Inet, Cidr, MacAddr,
            Uuid,
            Xml,
            TsVector, TsQuery,
            // Geometric (PostGIS)
            Point,
            // Range base typess
            Int4RangeBase, Int8RangeBase, NumRangeBase, TsRangeBase, TstzRangeBase, DateRangeBase,
            // Custom / advanced
            CustomComposite, CustomEnum, CustomDomain,
        }

        public static readonly IReadOnlyDictionary<string, string> DefaultTypeMappings = new Dictionary<string, string>
{
    { "SmallInt", "short" },
    { "Integer", "int" },
    { "BigInt", "long" },
    { "SmallSerial", "short" },
    { "Serial", "int" },
    { "BigSerial", "long" },
    { "Real", "float" },
    { "DoublePrecision", "double" },
    { "Numeric", "decimal" },
    { "Decimal", "decimal" },
    { "Money", "decimal" },
    { "Boolean", "bool" },
    { "Char", "string" },
    { "VarChar", "string" },
    { "Text", "string" },
    { "Bytea", "byte[]" },
    { "Date", "DateOnly" },
    { "Time", "TimeOnly" },
    { "TimeTz", "DateTimeOffset" },
    { "Timestamp", "DateTime" },
    { "TimestampTz", "DateTimeOffset" },
    { "Interval", "TimeSpan" },
    { "Json", "System.Text.Json.JsonDocument" },
    { "Jsonb", "System.Text.Json.JsonDocument" },
    { "Inet", "string" },
    { "Cidr", "string" },
    { "MacAddr", "string" },
    { "Point", "NpgsqlTypes.NpgsqlPoint" },
    { "Circle", "object" }, // no native EF mapping
    { "Line", "object" },
    { "LSeg", "object" },
    { "Path", "object" },
    { "Polygon", "object" },
    { "Uuid", "Guid" },
    { "Xml", "string" },
    { "TsVector", "string" },
    { "TsQuery", "string" },
    { "DateRange", "object" },
    { "Int4Range", "object" },
    { "Int8Range", "object" },
    { "NumRange", "object" },
    { "TsRange", "object" },
    { "TstzRange", "object" },
    { "IntArray", "int[]" },
    { "TextArray", "string[]" },
    { "UuidArray", "Guid[]" },
    { "SecureMedia", "SecureMedia" },
    { "SecureMediaSession", "SecureMediaSession" },
    { "Enum", "string" }, // later map to C# enum type
    { "Domain", "object" },
};


        // Extended set of C# keywords (includes contextual / LINQ names that can collide in generated code)
        private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
        {
            "abstract","as","base","bool","break","byte","case","catch","char","checked",
            "class","const","continue","decimal","default","delegate","do","double","else",
            "enum","event","explicit","extern","false","finally","fixed","float","for",
            "foreach","goto","if","implicit","in","int","interface","internal","is","lock",
            "long","namespace","new","null","object","operator","out","override","params",
            "private","protected","public","readonly","ref","return","sbyte","sealed","short",
            "sizeof","stackalloc","static","string","struct","switch","this","throw","true",
            "try","typeof","uint","ulong","unchecked","unsafe","ushort","using","virtual",
            "void","volatile","while",
            // contextual / LINQ / other common identifiers that can collide
            "add","alias","ascending","async","await","by","dynamic","equals","from","get",
            "global","group","into","join","let","nameof","on","orderby","partial","remove",
            "select","set","unmanaged","value","var","when","where","yield"
        };

        // Designer output container
        public struct DatabaseDesign
        {
            public string TableName;
            public string SQL;
            public string Documentation;
            public string CsClass;
            public string ClassName; // CLR class name used in DbContext
        }

        // Main API: produce SQL, docs, and C# class text for a single table
        public static DatabaseDesign DatabaseDesigner(string tableName, string? tableDescription, List<RowOptions> rows, string[]? customRows, List<ReferenceOptions>? references, List<IndexDefinition>? indexes)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("tableName required", nameof(tableName));
            DatabaseDesign design = new DatabaseDesign();
            StringBuilder sqlBuilder = new StringBuilder();
            StringBuilder docBuilder = new StringBuilder();
            StringBuilder classBuilder = new StringBuilder();

            // helpers
            string ToPascalCase(string input)
            {
                if (string.IsNullOrEmpty(input)) return input ?? "";
                return string.Concat(input.Split('_', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(s => char.ToUpperInvariant(s[0]) + (s.Length > 1 ? s.Substring(1) : "")));
            }

            string EscapePartIfKeyword(string part)
            {
                if (string.IsNullOrEmpty(part)) return part ?? "";
                return CSharpKeywords.Contains(part) ? part + "Data" : part;
            }

            string EscapeKeyword(string identifier)
            {
                if (string.IsNullOrEmpty(identifier)) return identifier ?? "";
                var parts = identifier.Split('.');
                if (parts.Length == 1) return EscapePartIfKeyword(parts[0]);
                if (parts.Length == 2) return EscapePartIfKeyword(parts[0]) + "." + EscapePartIfKeyword(parts[1]);
                throw new ArgumentException("EscapeKeyword expects at most one dot in the input.");
            }

            string SafeName(string name)
            {
                if (string.IsNullOrEmpty(name)) return "Unnamed";
                // remove non-alphanumeric (but keep underscore)
                var safe = Regex.Replace(name, @"[^\p{L}\p{Nd}_]", "");
                if (string.IsNullOrEmpty(safe)) return "Unnamed";
                if (char.IsDigit(safe[0])) safe = "_" + safe;
                return ToPascalCase(safe);
            }

            // Schema parse
            var schemaParts = GetSchema(tableName);
            var schemaRaw = schemaParts.schema;
            var tableOnlyRaw = schemaParts.table;
            if (!string.IsNullOrWhiteSpace(schemaRaw))
                sqlBuilder.AppendLine(Schema.CreateSchema(schemaRaw));

            // SQL table name used in SQL generation (schema.table or table)
            var sqlTableName = string.IsNullOrWhiteSpace(schemaRaw) ? tableOnlyRaw : $"{schemaRaw}.{tableOnlyRaw}";

            // Build rows SQL
            StringBuilder rowBuilder = new StringBuilder();
            foreach (var row in rows)
            {
                rowBuilder.AppendLine(RowCreator(row));
            }

            // Create the table SQL
            if (!string.IsNullOrEmpty(sqlTableName))
            {
                // Keep calling the same API you used before (Table.TableCreator)
                var tableData = Table.TableCreator(sqlTableName, tableDescription, rowBuilder.ToString(), customRows);
                sqlBuilder.AppendLine(tableData.Item1);
                docBuilder.AppendLine(tableData.Item2);
            }

            // References
            if (references != null && references.Count > 0)
            {
                foreach (var item in references)
                {
                    var refer = Reference.ReferenceCreator(item); // assumes existing API
                    sqlBuilder.AppendLine(refer.Item1);
                    docBuilder.AppendLine(refer.Item2);
                }
            }

            // Indexes — use Index.Generate (deconstruct tuple for safety)
            if (indexes != null && indexes.Count > 0)
            {
                foreach (var item in indexes)
                {
                    var (idxSql, idxDoc) = Index.CreateIndex(item, item.IndexName); // deconstruct whatever tuple Index.Generate returns
                    sqlBuilder.AppendLine(idxSql);
                    docBuilder.AppendLine(idxDoc);
                }
            }

            // Documentation for columns
            foreach (var row in rows)
                docBuilder.Append(RowDocumentationGenerator(row)); // assumes existing API

            // Build C# class
            StringBuilder tempRows = new StringBuilder();
            foreach (var row in rows)
            {
                if (row.IsPrimary) tempRows.AppendLine("    [Key]");
                if (row.IsNotNull) tempRows.AppendLine("    [Required]");
                tempRows.AppendLine($"    [Column(\"{row.FieldName}\")]");

                string csType = row.PostgresType.HasValue && DefaultTypeMappings.TryGetValue(row.PostgresType.ToString(), out var mapped)
                                ? mapped
                                : row.CustomType ?? "object";

                // ensure Pascal for property name
                string propName = ToPascalCase(SafeName(row.FieldName));
                tempRows.AppendLine($"    public {csType} {propName} {{ get; set; }}");
                tempRows.AppendLine();
            }

            // CLR class name should be a safe PascalCase name based on the table (without schema)
            string clrBase = SafeName(GetTableNameWithoutSchema(sqlTableName));
            string clrClassName = $"{clrBase}Item";

            classBuilder.AppendLine($"[Table(\"{GetTableNameWithoutSchema(sqlTableName)}\"{(string.IsNullOrEmpty(schemaRaw) ? "" : $", Schema = \"{schemaRaw}\"")})]");
            classBuilder.AppendLine($"public class {clrClassName}");
            classBuilder.AppendLine("{");
            classBuilder.AppendLine(tempRows.ToString());
            classBuilder.AppendLine("}");

            // Fill out design result
            design.TableName = tableName;
            design.SQL = sqlBuilder.ToString();
            design.Documentation = docBuilder.ToString();
            design.CsClass = ApplyNullableFixes(design.SQL, classBuilder.ToString(), sqlTableName);
            design.ClassName = clrClassName; // provide CLR class name for downstream uses

            return design;
        }

        // Main runner: collects multiple designs and writes files (keeps same signature)
        public static async Task RunDatabaseDesignerAsync(List<DatabaseDesign> designs, string userDocumentsPath, bool isIncremental = false, CancellationToken cancellationToken = default)
        {
            StringBuilder sql = new StringBuilder();
            StringBuilder doc = new StringBuilder();
            StringBuilder classes = new StringBuilder();

            doc.AppendLine("# Project Database");
            doc.AppendLine("## Generated by DatabaseDesigner");

            // SecureMedia tables (REAL tables, not composite types)
            sql.AppendLine("""
CREATE TABLE IF NOT EXISTS SecureMedia (
    id BIGSERIAL PRIMARY KEY,
    is_public BOOLEAN NOT NULL,
    secret_key TEXT NOT NULL,
    public_key TEXT NOT NULL,
    path TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
""");

            sql.AppendLine("""
CREATE TABLE IF NOT EXISTS SecureMediaSession (
    id BIGSERIAL PRIMARY KEY,
    media_id BIGINT NOT NULL REFERENCES SecureMedia(id) ON DELETE CASCADE,
    user_id BIGINT NOT NULL,
    referenced_media TEXT,
    allowed_user TEXT,
    public_key TEXT,
    last_used TIMESTAMPTZ
);
""");

            // EF models for SecureMedia
            classes.AppendLine("""
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System;

public class SecureMedia
{
    [Key]
    public long Id { get; set; }

    public bool IsPublic { get; set; }

    [Required]
    public string SecretKey { get; set; } = null!;

    [Required]
    public string PublicKey { get; set; } = null!;

    [Required]
    public string Path { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<SecureMediaSession> Sessions { get; set; } = new List<SecureMediaSession>();
}

public class SecureMediaSession
{
    [Key]
    public long Id { get; set; }

    [ForeignKey(nameof(Media))]
    public long MediaId { get; set; }

    public SecureMedia Media { get; set; } = null!;

    public long UserId { get; set; }

    public string? ReferencedMedia { get; set; }
    public string? AllowedUser { get; set; }
    public string? PublicKey { get; set; }
    public DateTimeOffset? LastUsed { get; set; }
}
""");

            // Append each design's outputs
            foreach (var item in designs)
            {
                sql.AppendLine(item.SQL);
                doc.AppendLine(item.Documentation);
                classes.AppendLine(item.CsClass);
            }

            // Build DbContext source
            StringBuilder dbContext = new StringBuilder();
            dbContext.AppendLine("using Microsoft.EntityFrameworkCore;");
            dbContext.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
            dbContext.AppendLine();
            dbContext.AppendLine("public class AppDbContext : DbContext");
            dbContext.AppendLine("{");
            dbContext.AppendLine("    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }");
            dbContext.AppendLine();

            foreach (var item in designs)
            {
                // item.ClassName now contains the CLR class name (safe)
                string classNameSafe = item.ClassName;
                string dbSetName = classNameSafe + "s"; // naive pluralization
                string? schemaName = GetSchemaFromTable(item.TableName);
                string tableOnly = GetTableNameWithoutSchema(item.TableName);

                if (!string.IsNullOrEmpty(schemaName))
                    dbContext.AppendLine($"    [Table(\"{tableOnly}\", Schema = \"{schemaName}\")]");
                else
                    dbContext.AppendLine($"    [Table(\"{tableOnly}\")]");

                dbContext.AppendLine($"    public DbSet<{classNameSafe}> {dbSetName} {{ get; set; }}");
                dbContext.AppendLine();
            }

            dbContext.AppendLine("}");

            // Write outputs
            string generatedDBPath = Path.Combine(userDocumentsPath, "GeneratedDB");
            if (isIncremental)
            {
                if (!Directory.Exists(generatedDBPath)) Directory.CreateDirectory(generatedDBPath);
                string[] folders = Directory.GetDirectories(generatedDBPath, "*", SearchOption.TopDirectoryOnly);
                var versionFolders = folders.Select(f => Path.GetFileName(f))
                    .Where(name => name != null && name.StartsWith("v") && int.TryParse(name.Substring(1), out _))
                    .ToList();

                string incremental = versionFolders.Count == 0 ? "v1" : "v" + (versionFolders.Max(name => int.Parse(name.Substring(1))) + 1);
                generatedDBPath = Path.Combine(generatedDBPath, incremental);
            }

            Directory.CreateDirectory(generatedDBPath);
            File.WriteAllText(Path.Combine(generatedDBPath, "SQL.sql"), sql.ToString());
            File.WriteAllText(Path.Combine(generatedDBPath, "Documentation.md"), doc.ToString());
            File.WriteAllText(Path.Combine(generatedDBPath, "Classes.cs"), classes.ToString());
            File.WriteAllText(Path.Combine(generatedDBPath, "Models.cs"), dbContext.ToString());

            await Task.CompletedTask;
        }

        // ---------------------------
        // Nullable-fix / helper logic
        // ---------------------------

        static readonly HashSet<string> ValueTypeNames = new(StringComparer.Ordinal)
        {
            "Guid","int","long","bool","DateTime","decimal","float","double","short","byte","TimeOnly","DateOnly","DateTimeOffset","TimeSpan"
        };

        public static string ApplyNullableFixes(string sql, string csClass, string? tableIdentifier = null)
        {
            if (string.IsNullOrEmpty(sql) || string.IsNullOrEmpty(csClass)) return csClass;

            var setNullFks = ExtractSetNullColumns(sql);
            if (setNullFks.Count == 0) return csClass;

            string classTable = tableIdentifier ?? GetTableNameFromClassText(csClass) ?? "";
            string classTableNorm = NormalizeTableName(classTable);

            var applicableCols = setNullFks
                .Where(t => string.IsNullOrEmpty(classTableNorm) || NormalizeTableName(t.table) == classTableNorm)
                .Select(t => t.column)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (applicableCols.Count == 0) return csClass;

            string patched = csClass;

            foreach (var col in applicableCols)
            {
                string pascal = ToPascal(col);

                // Prefer to handle simple patterns produced by our generator:
                // public <Type> <Pascal> { get; set; }
                foreach (var vt in ValueTypeNames)
                {
                    // match "public <vt> <Pascal> {"
                    string pattern = $@"public\s+{Regex.Escape(vt)}\s+{Regex.Escape(pascal)}\s*\{{";
                    if (Regex.IsMatch(patched, pattern))
                    {
                        string replacement = $"public {vt}? {pascal} {{";
                        patched = Regex.Replace(patched, pattern, replacement);
                    }

                    // fully-qualified like System.Guid
                    string fq = @"System\." + Regex.Escape(vt);
                    string patternFq = $@"public\s+{fq}\s+{Regex.Escape(pascal)}\s*\{{";
                    if (Regex.IsMatch(patched, patternFq))
                    {
                        string replacementFq = $"public {fq}? {pascal} {{";
                        patched = Regex.Replace(patched, patternFq, replacementFq);
                    }
                }

                // If the property uses nullable already (has ?), we leave it.
                // We intentionally skip reference types (string, byte[], JsonDocument...) — they are reference nullable by default.
            }

            return patched;
        }

        // Extracts FK columns with ON DELETE SET NULL. Returns entries like (table, column)
        private static List<(string table, string column)> ExtractSetNullColumns(string sql)
        {
            var results = new List<(string table, string column)>();
            if (string.IsNullOrWhiteSpace(sql)) return results;

            // ALTER TABLE pattern
            var pattern = new Regex(
                @"ALTER\s+TABLE\s+(?<table>[^\s;]+).*?FOREIGN\s+KEY\s*\(\s*(?<col>[^\)]+)\s*\).*?ON\s+DELETE\s+SET\s+NULL",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match m in pattern.Matches(sql))
            {
                var table = m.Groups["table"].Value.Trim().Trim('"');
                var col = m.Groups["col"].Value.Trim().Trim('"');
                var cols = col.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim());
                foreach (var c in cols) results.Add((table, c));
            }

            // Inline FK declarations inside CREATE TABLE blocks
            var createTablePattern = new Regex(
                @"CREATE\s+TABLE\s+(?<table>[^\s(]+)\s*\((?<body>.*?)\);",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var inlinePattern = new Regex(
                @"FOREIGN\s+KEY\s*\(\s*(?<col>[^\)]+)\s*\)\s*REFERENCES\s+(?<ref>[^\(]+)\([^\)]+\)\s*ON\s+DELETE\s+SET\s+NULL",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match m in createTablePattern.Matches(sql))
            {
                var table = m.Groups["table"].Value.Trim().Trim('"');
                var body = m.Groups["body"].Value;
                foreach (Match fk in inlinePattern.Matches(body))
                {
                    var col = fk.Groups["col"].Value.Trim().Trim('"');
                    var cols = col.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim());
                    foreach (var c in cols) results.Add((table, c));
                }
            }

            return results;
        }

        // Try to read table name from C# class [Table(...)] attribute
        private static string? GetTableNameFromClassText(string csClass)
        {
            if (string.IsNullOrEmpty(csClass)) return null;

            var p1 = new Regex(@"\[Table\(\s*""(?<full>[^""]+)""\s*\)\]", RegexOptions.IgnoreCase);
            var m1 = p1.Match(csClass);
            if (m1.Success) return m1.Groups["full"].Value.Trim();

            var p2 = new Regex(@"\[Table\(\s*""(?<table>[^""]+)""\s*,\s*Schema\s*=\s*""(?<schema>[^""]+)""\s*\)\]", RegexOptions.IgnoreCase);
            var m2 = p2.Match(csClass);
            if (m2.Success) return $"{m2.Groups["schema"].Value.Trim()}.{m2.Groups["table"].Value.Trim()}";

            return null;
        }

        private static string NormalizeTableName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? "";
            return raw.Trim().Trim('"').ToLowerInvariant();
        }

        private static string ToPascal(string name)
        {
            if (string.IsNullOrEmpty(name)) return name ?? "";
            return string.Concat(name.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => char.ToUpperInvariant(s[0]) + (s.Length > 1 ? s.Substring(1) : "")));
        }

        // Utility helpers for schema parsing
        private static (string? schema, string table) GetSchema(string qualifiedName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedName)) throw new ArgumentException("Name cannot be empty", nameof(qualifiedName));

            var parts = qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length switch
            {
                1 => (null, parts[0].Trim()),
                2 => (parts[0].Trim(), parts[1].Trim()),
                _ => (string.Join("_", parts.Take(parts.Length - 1).Select(p => p.Trim())), parts.Last().Trim())
            };
        }

        static string? GetSchemaFromTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return null;
            var parts = tableName.Split('.');
            return parts.Length == 2 ? parts[0] : null;
        }

        static string GetTableNameWithoutSchema(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return tableName ?? "";
            var parts = tableName.Split('.');
            return parts.Length == 2 ? parts[1] : tableName;
        }
    }
}
