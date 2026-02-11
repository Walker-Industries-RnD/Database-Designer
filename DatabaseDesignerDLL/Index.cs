using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DatabaseDesigner
{
    public static class Index
    {
        public struct IndexDefinition
        {
            public string TableName { get; }
            public string[] ColumnNames { get; }
            public IndexType IndexType { get; }
            public string Condition { get; }
            public string Expression { get; }
            public string IndexTypeCustom { get; }
            public string? IndexName { get; }
            public bool? UseJsonbPathOps { get; }

            public IndexDefinition(
                string tableName,
                string? indexName,
                string[] columnNames,
                IndexType indexType,
                string condition = "",
                string expression = "",
                string indexTypeCustom = "",
                bool? useJsonbPathOps = null)
            {
                TableName = tableName;
                IndexName = indexName;
                ColumnNames = columnNames;
                IndexType = indexType;
                Condition = condition;
                Expression = expression;
                IndexTypeCustom = indexTypeCustom;
                UseJsonbPathOps = useJsonbPathOps;
            }
        }

        public enum IndexType
        {
            Basic,
            Composite,
            Partial,
            Expression,
            Gin,
            Unique,
            Hash,
            Custom
        }

        public static string CreateUUID()
        {
            var rnd = new Random();
            var sb = new StringBuilder(20);
            for (int i = 0; i < 20; i++)
                sb.Append(rnd.Next(0, 10));
            return sb.ToString();
        }

        public static (string Sql, string Doc) CreateIndex(IndexDefinition indexSetting, string? indexName)
        {
            string columnsDoc = string.Join(Environment.NewLine, indexSetting.ColumnNames.Select(c => c + "_"));
            string columnsSql = string.Join(", ", indexSetting.ColumnNames);

            string sql;
            string doc;

            switch (indexSetting.IndexType)
            {
                case IndexType.Basic:
                    sql = $"CREATE INDEX idx_{indexName} ON {indexSetting.TableName} ({indexSetting.ColumnNames[0]});";
                    doc = $@"### Basic Index
- **Index Name:** idx_{indexName}
- **Table:** {indexSetting.TableName}
- **Column:** {columnsDoc}";
                    break;

                case IndexType.Composite:
                    sql = $"CREATE INDEX idx_{string.Join("_", indexSetting.ColumnNames)} ON {indexSetting.TableName} ({columnsSql});";
                    doc = $@"### Composite Index
- **Index Name:** idx_{indexName}
- **Table:** {indexSetting.TableName}
- **Columns:** {columnsDoc}";
                    break;

                case IndexType.Partial:
                    sql = $"CREATE INDEX idx_{indexName}_partial ON {indexSetting.TableName} ({indexSetting.ColumnNames[0]}) WHERE {indexSetting.Condition};";
                    doc = $@"### Partial Index
- **Index Name:** idx_{indexName}
- **Table:** {indexSetting.TableName}
- **Column:** {columnsDoc}
- **Condition:** {indexSetting.Condition}";
                    break;

                case IndexType.Expression:
                    string exprName = indexSetting.Expression.Replace("(", "").Replace(")", "").Replace(" ", "_");
                    sql = $"CREATE INDEX idx_{exprName} ON {indexSetting.TableName} ({indexSetting.Expression});";
                    doc = $@"### Expression Index
- **Index Name:** idx_{indexName}
- **Table:** {indexSetting.TableName}
- **Column:** {columnsDoc}
- **Expression:** {indexSetting.Expression}";
                    break;

                case IndexType.Gin:
                    bool usePathOps = indexSetting.UseJsonbPathOps ?? false;
                    string ops = usePathOps ? " jsonb_path_ops" : "";
                    sql = $"CREATE INDEX idx_{indexName}_gin ON {indexSetting.TableName} USING gin ({indexSetting.ColumnNames[0]}{ops});";
                    doc = $@"### GIN Index
- **Index Name:** idx_{indexName}
- **Table:** {indexSetting.TableName}
- **Column:** {columnsDoc}
- **Using:** gin{(usePathOps ? " with jsonb_path_ops" : "")}";
                    break;

                case IndexType.Unique:
                    sql = $"CREATE UNIQUE INDEX idx_unique_{indexName} ON {indexSetting.TableName} ({indexSetting.ColumnNames[0]});";
                    doc = $@"### Unique Index
- **Index Name:** idx_unique_{indexName}
- **Table:** {indexSetting.TableName}
- **Column:** {columnsDoc}
- **Unique:** Yes";
                    break;

                case IndexType.Custom:
                    sql = $"CREATE INDEX idx_{indexName}_{indexSetting.IndexTypeCustom.ToLower()} ON {indexSetting.TableName} USING {indexSetting.IndexTypeCustom} ({indexSetting.ColumnNames[0]});";
                    doc = $@"### Custom Index
- **Index Name:** idx_{indexName}
- **Table:** {indexSetting.TableName}
- **Column:** {columnsDoc}
- **Index Type:** {indexSetting.IndexTypeCustom}";
                    break;

                case IndexType.Hash:
                    sql = $"CREATE INDEX idx_{indexName}_hash ON {indexSetting.TableName} USING hash ({indexSetting.ColumnNames[0]});";
                    doc = $@"### Hash Index
- **Index Name:** idx_{indexName}
- **Table:** {indexSetting.TableName}
- **Column:** {columnsDoc}
- **Using:** hash";
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(indexSetting.IndexType), $"Invalid index type: {indexSetting.IndexType}");
            }

            return (sql.Trim(), doc.Trim());
        }
    }
}
