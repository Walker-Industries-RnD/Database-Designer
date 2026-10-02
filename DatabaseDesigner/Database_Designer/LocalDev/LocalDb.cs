using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;

namespace Database_Designer
{
    // Talks to the developer's own Postgres (desktop app only).
    public static class LocalDb
    {
        public const string DefaultConnection = "Host=localhost;Port=5432;Username=postgres;Password=;Database=dd_dev";

        public sealed class QueryResult
        {
            public List<string> Columns { get; } = new();
            public List<string[]> Rows { get; } = new();
            public int Affected { get; set; } = -1;
            public bool Truncated { get; set; }
        }

        public static async Task<string> Test(string connectionString)
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("select version()", conn);
            return Convert.ToString(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        }

        // Creates the database named in the connection string if it doesn't
        // exist yet (connecting to the "postgres" maintenance database).
        public static async Task<bool> EnsureDatabase(string connectionString)
        {
            var b = new NpgsqlConnectionStringBuilder(connectionString);
            var name = b.Database;
            if (string.IsNullOrWhiteSpace(name) || name == "postgres") return false;
            b.Database = "postgres";
            await using var conn = new NpgsqlConnection(b.ConnectionString);
            await conn.OpenAsync();
            await using (var check = new NpgsqlCommand("select 1 from pg_database where datname = @n", conn))
            {
                check.Parameters.AddWithValue("n", name);
                if (await check.ExecuteScalarAsync() != null) return false;
            }
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name.Replace("\"", "\"\"")}\"", conn);
            await create.ExecuteNonQueryAsync();
            return true;
        }

        // Runs a whole .sql file. With transaction=true it all succeeds or
        // nothing changes.
        public static async Task ExecuteScript(string connectionString, string sql, bool transaction = true)
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var tx = transaction ? await conn.BeginTransactionAsync() : null;
            await using var cmd = new NpgsqlCommand(sql, conn, tx) { CommandTimeout = 300 };
            await cmd.ExecuteNonQueryAsync();
            if (tx != null) await tx.CommitAsync();
        }

        // Drops the schemas the project uses (and everything in them) so the
        // next SQL.sql run starts clean.
        public static async Task DropSchemas(string connectionString, IEnumerable<string> schemas)
        {
            var list = schemas.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (list.Count == 0) return;
            var sql = string.Join("\n", list.Select(s => s.Equals("public", StringComparison.OrdinalIgnoreCase)
                ? "DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public;"
                : $"DROP SCHEMA IF EXISTS \"{s.Replace("\"", "\"\"")}\" CASCADE;"));
            await ExecuteScript(connectionString, sql);
        }

        // Every table in the database as "schema.table", lower-cased.
        public static async Task<HashSet<string>> ExistingTables(string connectionString)
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "select table_schema || '.' || table_name from information_schema.tables " +
                "where table_schema not in ('pg_catalog', 'information_schema')", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) found.Add(reader.GetString(0).ToLowerInvariant());
            return found;
        }

        public static async Task<QueryResult> Query(string connectionString, string sql, int maxRows = 200, bool rollback = true)
        {
            var result = new QueryResult();
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();
            await using var cmd = new NpgsqlCommand(sql, conn, tx) { CommandTimeout = 60 };
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                do
                {
                    if (reader.FieldCount == 0) continue;
                    result.Columns.Clear();
                    result.Rows.Clear();
                    for (int i = 0; i < reader.FieldCount; i++) result.Columns.Add(reader.GetName(i));
                    while (await reader.ReadAsync())
                    {
                        if (result.Rows.Count >= maxRows) { result.Truncated = true; continue; }
                        var row = new string[reader.FieldCount];
                        for (int i = 0; i < reader.FieldCount; i++)
                            row[i] = reader.IsDBNull(i) ? "NULL" : Format(reader.GetValue(i));
                        result.Rows.Add(row);
                    }
                } while (await reader.NextResultAsync());
                result.Affected = reader.RecordsAffected;
            }
            if (rollback) await tx.RollbackAsync(); else await tx.CommitAsync();
            return result;
        }

        private static string Format(object v) => v switch
        {
            DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset d => d.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            byte[] b => "\\x" + Convert.ToHexString(b.Length > 32 ? b.Take(32).ToArray() : b).ToLowerInvariant() + (b.Length > 32 ? "…" : ""),
            Array a => "{" + string.Join(",", a.Cast<object>().Select(x => x == null ? "NULL" : Format(x))) + "}",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => v.ToString()
        };
    }
}
