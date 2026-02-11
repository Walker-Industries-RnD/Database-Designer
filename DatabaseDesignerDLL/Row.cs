using Npgsql.PostgresTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace DatabaseDesigner
{
    public static class Row
    {
        // This script handles creating custom rows
        public struct RowOptions
        {
            public string FieldName { get; set; }
            public string Description { get; set; }
            public DBDesigner.PostgresType? PostgresType { get; set; }

            public string CustomType { get; set; } // If you use a custom type, set PostgresType to null
            public int? Limit { get; set; } // Ensure the thing you're limiting supports limits, CAN NOT USE IF USING ARRAY, USE CHECK INSTEAD
            public bool IsArray { get; set; }
            public int? ArrayLimit { get; set; } // This is used to check if the array has a limit, if it does, it will add a check to the row creation script
            public bool IsEncrypted { get; set; } // You can either have encrypted or media; if you try setting both active, it will default to encrypted
            public bool IsMedia { get; set; }
            public bool IsPrimary { get; set; } // If primary, automatically ignored unique + not null since it should be in there
            public bool IsUnique { get; set; }
            public bool IsNotNull { get; set; }

            public string? DefaultValue { get; set; }
            public string? Check { get; set; }

            public bool? DefaultIsKeyword { get; set; }

            public RowOptions(
                string fieldName,
                string description,
                DBDesigner.PostgresType? postgresType = null,
                string customType = "", //This is for a future update
                int? elementLimit = null,
                bool isArray = false,
                int? arrayLimit = null,
                bool isEncrypted = false,
                bool isMedia = false, //Fix thing here (Eventually)
                bool isPrimary = false,
                bool isUnique = false,
                bool isNotNull = false,
                string? defaultValue = null,
                string? check = null,
                bool? defaultIsKeyword = false)
            {
                FieldName = fieldName;
                Description = description;
                PostgresType = postgresType;
                CustomType = customType;
                Limit = elementLimit;
                IsArray = isArray;
                ArrayLimit = arrayLimit;
                IsEncrypted = isEncrypted;
                IsMedia = isMedia;
                IsPrimary = isPrimary;
                IsUnique = isUnique;
                IsNotNull = isNotNull;
                DefaultValue = defaultValue;
                Check = check;
                DefaultIsKeyword = defaultIsKeyword;
            }
        }


        //A little helper function, I think i'm gonna use this more
        static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "Unnamed";

            return string.Concat(
                input
                    .Split('_', StringSplitOptions.RemoveEmptyEntries) // <-- skip empty strings
                    .Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1))
            );
        }


        static string SafeName(string name, bool pascalCase)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";

            var safe = Regex.Replace(name, @"\W", ""); // remove non-alphanumeric

            // Make safe for C# identifiers
            if (string.IsNullOrEmpty(safe) || char.IsDigit(safe[0]))
                safe = "_" + safe;

            if (pascalCase)
                safe = ToPascalCase(safe);

            return safe;
        }




        public static string RowCreator(RowOptions rowOption)
        {
            if (string.IsNullOrEmpty(rowOption.FieldName) ||
                string.IsNullOrEmpty(rowOption.Description) ||
                (rowOption.PostgresType == null && string.IsNullOrEmpty(rowOption.CustomType) && rowOption.IsEncrypted == false && rowOption.IsMedia == false))
            {
                throw new ArgumentException("Invalid profile option: FieldName, Description, and either PostgresType or CustomType must be provided.");
            }

            // Start script string with indentation
            string scriptstring = "    ";

            var checks = new List<string>();

            // Determine the PostgreSQL type name
            string typeName = rowOption.PostgresType?.ToString() ?? rowOption.CustomType ??
                              (rowOption.IsEncrypted ? "TEXT" : rowOption.IsMedia ? "SecureMedia" : null);

            if (string.IsNullOrEmpty(typeName))
                throw new Exception("Invalid type definition.");

            if (rowOption.Limit.HasValue && rowOption.Limit.Value > 0 &&
                (typeName == "CHAR" || typeName == "VARCHAR" || typeName == "NUMERIC" || typeName == "TIME" || typeName == "TIMESTAMP"))
            {
                typeName += $"({rowOption.Limit.Value})";
            }


            if (rowOption.IsArray)
            {
                typeName += "[]";
                if (rowOption.ArrayLimit.HasValue)
                {
                    checks.Add($"cardinality({SafeName(rowOption.FieldName, false)}) <= {rowOption.ArrayLimit}");
                }
            }

            // Append the field name and type once
            scriptstring += $"{SafeName(rowOption.FieldName, false)} {typeName}";

            // Handle primary key, uniqueness, not null
            if (rowOption.IsPrimary)
            {
                scriptstring += " PRIMARY KEY";
            }
            else
            {
                if (rowOption.IsUnique)
                    scriptstring += " UNIQUE";
                if (rowOption.IsNotNull)
                    scriptstring += " NOT NULL";
            }

            // Handle default values
            if (rowOption.DefaultValue != null)
            {
                if (rowOption.DefaultIsKeyword == true)
                    scriptstring += " DEFAULT " + rowOption.DefaultValue;
                else
                    scriptstring += " DEFAULT '" + rowOption.DefaultValue + "'";
            }

            // Handle check constraints
            if (!string.IsNullOrWhiteSpace(rowOption.Check))
                checks.Add(rowOption.Check);

            if (checks.Count >= 2)
                scriptstring += $" CHECK ({string.Join(" AND ", checks)})";
            else if (checks.Count == 1)
                scriptstring += $" CHECK ({checks[0]})";

            return scriptstring;
        }

        //Update
        public static string RowDocumentationGenerator(RowOptions rowOption)
        {
            var doc = new StringBuilder();

            doc.AppendLine($"### `{SafeName(rowOption.FieldName, false)}`");
            doc.AppendLine();
            doc.AppendLine($"**Description:** {rowOption.Description}");
            doc.AppendLine();

            // Data type info
            if (rowOption.IsEncrypted)
            {
                doc.AppendLine("- **Type:** Encrypted (TEXT)");
            }
            else if (rowOption.IsMedia)
            {
                doc.AppendLine("- **Type:** Media (SecureMedia)");
            }
            else if (rowOption.PostgresType != null)
            {
                doc.AppendLine($"- **Type:** {rowOption.PostgresType}");
            }
            else
            {
                doc.AppendLine($"- **Type:** Custom ({rowOption.CustomType})");
            }

            if (rowOption.IsArray)
            {
                doc.AppendLine("- **Array:** Yes");
                if (rowOption.ArrayLimit.HasValue)
                    doc.AppendLine($"- **Array Limit:** {rowOption.ArrayLimit}");
            }

            if (!rowOption.IsArray && rowOption.Limit.HasValue)
                doc.AppendLine($"- **Limit:** {rowOption.Limit}");

            if (rowOption.IsPrimary)
                doc.AppendLine("- **Primary Key:** Yes");
            else
            {
                if (rowOption.IsUnique)
                    doc.AppendLine("- **Unique:** Yes");
                if (rowOption.IsNotNull)
                    doc.AppendLine("- **Not Null:** Yes");
            }

            if (rowOption.DefaultValue != null)
            {
                if (rowOption.DefaultIsKeyword == true)
                    doc.AppendLine($"- **Default Value:** `{rowOption.DefaultValue}` (keyword)");
                else
                    doc.AppendLine($"- **Default Value:** '{rowOption.DefaultValue}'");
            }

            if (!string.IsNullOrWhiteSpace(rowOption.Check))
                doc.AppendLine($"- **Check:** {rowOption.Check}");

            return doc.ToString();
        }



        enum EncodedType : int { Int = 0, Double = 1, Tuple = 2 }

        public static class LimitEncoder
        {
            const int SCALE = 1000;

            public static int Encode(object v) => v switch
            {
                int i => (i << 2) | (int)EncodedType.Int,
                double d => ((int)(d * SCALE) << 2) | (int)EncodedType.Double,
                (int a, int b) => ((((a & 0xFFFF) << 16) | (b & 0xFFFF)) << 2) | (int)EncodedType.Tuple,
                _ => throw new NotSupportedException()
            };

            public static object Decode(int e)
            {
                var tag = (EncodedType)(e & 3);
                var data = e >> 2;
                return tag switch
                {
                    EncodedType.Int => data,
                    EncodedType.Double => data / (double)SCALE,
                    EncodedType.Tuple => ((short)(data >> 16), (short)(data & 0xFFFF)),
                    _ => throw new InvalidOperationException()
                };
            }
        }


    }
}
