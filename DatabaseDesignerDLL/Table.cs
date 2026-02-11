using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DatabaseDesigner
{
    internal static class Table
    {


        public static (string Sql, string Description) TableCreator(
            string tableName,
            string? description,
            string rows,
            string[]? customRows = null)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Table name is required.", nameof(tableName));

            if (string.IsNullOrWhiteSpace(rows))
                throw new ArgumentException("Table must define at least one column.", nameof(rows));

            static string QuoteIdent(string ident)
                => $"\"{ident.Replace("\"", "\"\"")}\"";

            static string QuoteTable(string table)
                => string.Join(".", table.Split('.').Select(QuoteIdent));

            var tableBuilder = new StringBuilder();
            tableBuilder.AppendLine($"CREATE TABLE {QuoteTable(tableName)} (");

            // Split rows by line, preserve leading indentation, remove trailing junk
            var rowDefs = rows
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.TrimEnd());

            if (customRows is { Length: > 0 })
            {
                rowDefs = rowDefs.Concat(customRows.Select(r => r.TrimEnd()));
            }

            tableBuilder.AppendLine(string.Join(",\n", rowDefs));
            tableBuilder.Append(");");

            return (
                tableBuilder.ToString(),
                description ?? "No description provided."
            );
        }


    }
}
