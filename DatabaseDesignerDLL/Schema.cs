using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseDesigner
{
    internal static class Schema
    {
        // This script creates a schema for the database, pretty simple
        public static (string? schema, string table) GetSchema(string qualifiedName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedName))
                throw new ArgumentException("Name cannot be empty", nameof(qualifiedName));

            var parts = qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                // No schema, just table
                return (null, parts[0].Trim());
            }
            else if (parts.Length == 2)
            {
                // Exactly one dot → keep schema and table as-is
                return (parts[0].Trim(), parts[1].Trim());
            }
            else
            {
                // More than one dot → replace intermediate dots with underscores
                string table = parts.Last().Trim();
                string schema = string.Join("_", parts.Take(parts.Length - 1).Select(p => p.Trim()));
                return (schema, table);
            }
        }


        // Create schema SQL statement
        public static string CreateSchema(string schemaName)
        {
            // return empty string when no schema specified
            if (string.IsNullOrWhiteSpace(schemaName))
                return string.Empty;

            // escape double quotes by doubling them and quote the identifier
            var escaped = schemaName.Replace("\"", "\"\"");
            return $"CREATE SCHEMA IF NOT EXISTS \"{escaped}\";";
        }



    }
}
