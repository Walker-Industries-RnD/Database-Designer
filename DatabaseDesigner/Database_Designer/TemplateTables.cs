using System;
using System.Collections.Generic;
using System.Linq;
using DatabaseDesigner;
using static DatabaseDesigner.Index;
using static DatabaseDesigner.Row;
using static DatabaseDesigner.SessionStorage;

namespace Database_Designer
{
    // Turns tables read from a template pack into project tables.
    public static class TemplateTables
    {
        public static TableObject ToTableObject(string fullTableName, string description, List<RowOptions> rows,
            List<Reference.ReferenceOptions> references, List<IndexDefinition> indexes)
        {
            int dot = (fullTableName ?? "").IndexOf('.');
            return new TableObject
            {
                SchemaName = dot > 0 ? fullTableName.Substring(0, dot) : "public",
                TableName = dot > 0 ? fullTableName.Substring(dot + 1) : fullTableName,
                Description = description,
                Rows = (rows ?? new()).Select(r => new RowCreation
                {
                    Name = r.FieldName,
                    Description = r.Description,
                    RowType = r.PostgresType,
                    Limit = r.Limit,
                    IsArray = r.IsArray,
                    ArrayLimit = r.ArrayLimit?.ToString(),
                    EncryptedAndNOTMedia = r.IsEncrypted,
                    Media = r.IsMedia,
                    IsPrimary = r.IsPrimary,
                    IsUnique = r.IsUnique,
                    IsNotNull = r.IsNotNull,
                    DefaultValue = r.DefaultValue,
                    Check = r.Check,
                    DefaultIsPostgresFunction = r.DefaultIsKeyword
                }).ToList(),
                References = (references ?? new()).Select(x => new ReferenceOptions(
                    x.MainTable, x.RefTable, x.ForeignKey, x.RefTableKey, x.OnDeleteAction, x.OnUpdateAction)).ToList(),
                Indexes = (indexes ?? new()).Select(x => new IndexCreation
                {
                    TableName = x.TableName ?? "",
                    IndexName = x.IndexName ?? "",
                    ColumnNames = x.ColumnNames?.ToList() ?? new List<string>(),
                    IndexType = x.IndexType.ToString(),
                    Condition = x.Condition ?? "",
                    Expression = x.Expression ?? "",
                    IndexTypeCustom = x.IndexTypeCustom ?? "",
                    UseJsonbPathOps = x.UseJsonbPathOps
                }).ToList(),
                CustomRows = new List<string>()
            };
        }

        // Adds the tables to the open project. Tables whose schema.name already
        // exists are left alone and reported back.
        public static (int Added, List<string> Skipped) AddToProject(MainPage host, IEnumerable<TableObject> tables)
        {
            var skipped = new List<string>();
            int added = 0;
            host.RemoveTableUpdater();
            try
            {
                foreach (var t in tables)
                {
                    bool exists = host.MainSessionInfo.Tables.Any(x =>
                        string.Equals(x.SchemaName ?? "public", t.SchemaName ?? "public", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.TableName, t.TableName, StringComparison.OrdinalIgnoreCase));
                    if (exists) { skipped.Add($"{t.SchemaName}.{t.TableName}"); continue; }
                    host.MainSessionInfo.Tables.Add(t);
                    added++;
                }
            }
            finally { host.AddTableUpdater(); }
            if (added > 0) host.ForceCollectionChangeUpate();
            return (added, skipped);
        }
    }
}
