using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DatabaseDesigner
{
    public static class Reference
    {
        public enum ReferentialAction
        {
            NoAction,
            Restrict,
            Cascade,
            SetNull,
            SetDefault
        }

        public readonly struct ReferenceOptions
        {
            public string MainTable { get; }
            public string RefTable { get; }
            public string ForeignKey { get; }
            public string RefTableKey { get; }
            public ReferentialAction OnDeleteAction { get; }
            public ReferentialAction OnUpdateAction { get; }

            public ReferenceOptions(
                string mainTable,
                string refTable,
                string foreignKey,
                string refTableKey,
                ReferentialAction onDeleteAction = ReferentialAction.NoAction,
                ReferentialAction onUpdateAction = ReferentialAction.NoAction)
            {
                MainTable = mainTable ?? throw new ArgumentNullException(nameof(mainTable));
                RefTable = refTable ?? throw new ArgumentNullException(nameof(refTable));
                ForeignKey = foreignKey ?? throw new ArgumentNullException(nameof(foreignKey));
                RefTableKey = refTableKey ?? throw new ArgumentNullException(nameof(refTableKey));
                OnDeleteAction = onDeleteAction;
                OnUpdateAction = onUpdateAction;
            }
        }

        private static string QuoteIdent(string ident)
            => $"\"{ident.Replace("\"", "\"\"")}\"";

        private static string QuoteTable(string table)
            => string.Join(".", table.Split('.').Select(QuoteIdent));

        private static string SqlAction(ReferentialAction action) => action switch
        {
            ReferentialAction.Cascade => "CASCADE",
            ReferentialAction.SetNull => "SET NULL",
            ReferentialAction.SetDefault => "SET DEFAULT",
            ReferentialAction.Restrict => "RESTRICT",
            _ => "NO ACTION"
        };

        public static (string sql, string doc) ReferenceCreator(ReferenceOptions o)
        {
            string constraintName =
                $"fk_{o.MainTable.Replace(".", "_")}_{o.ForeignKey}_to_{o.RefTable.Replace(".", "_")}";

            string sql =
    $@"ALTER TABLE {QuoteTable(o.MainTable)}
ADD CONSTRAINT {QuoteIdent(constraintName)}
FOREIGN KEY ({QuoteIdent(o.ForeignKey)})
REFERENCES {QuoteTable(o.RefTable)} ({QuoteIdent(o.RefTableKey)})
ON DELETE {SqlAction(o.OnDeleteAction)}
ON UPDATE {SqlAction(o.OnUpdateAction)};";

            string doc =
    $@"### Foreign Key Constraint
- **Table:** {o.MainTable}
- **Column:** {o.ForeignKey}
- **References:** {o.RefTable}({o.RefTableKey})
- **On Delete:** {o.OnDeleteAction}
- **On Update:** {o.OnUpdateAction}";

            return (sql.Trim(), doc.Trim());
        }
    }
}
