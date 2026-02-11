using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DatabaseDesigner.Reference;

namespace DatabaseDesigner
{
    public class SessionStorage
    {

        public struct DBDesignerSession
        {
            //Let's imagine this is the actual Database Designer Nocode Software

            public string? SessionName;
            public string? SessionDescription;
            public DateTime LastEdited;
            public string? SessionLogo;

            public ObservableCollection<TableObject> Tables;

            public Dictionary<string, Coords> WindowStatuses;

        }


        public struct Coords
        {
            public int X;
            public int Y;
            public bool IsEnabled;
            public string? CustomLogic;
        }

    //This is what each table has going for it; this groups up the information from before

    public struct DBDesignerTable
        {
            public string TableName;
            public TableObject? SQLGen;
            public string? Documentation;
            public string? CSharpObj;
            public string? Logo;
        }



        public struct TableObject
        {
            public string TableName;
            public string? Description;
            public List<RowCreation> Rows;
            public List<string>? CustomRows;
            public List<ReferenceOptions>? References;
            public List<IndexCreation>? Indexes;
            public string? SchemaName;
        }



        //These hold the most basic information about the database

        public struct RowCreation
        {
            public string Name;
            public string Description;
            public bool? EncryptedAndNOTMedia;
            public bool? Media;
            public DBDesigner.PostgresType? RowType;
            public int? Limit;
            public bool? IsArray;
            public string? ArrayLimit;
            public bool? IsPrimary;
            public bool? IsUnique;
            public bool? IsNotNull;
            public string? DefaultValue;
            public bool? DefaultIsPostgresFunction;
            public string? Check;
        }

        public struct ReferenceOptions
        {
            public string MainTable { get; set; }
            public string RefTable { get; set; }
            public string ForeignKey { get; set; }
            public string RefTableKey { get; set; }
            public ReferentialAction OnDeleteAction { get; set; }
            public ReferentialAction OnUpdateAction { get; set; }

            public ReferenceOptions(
                string mainTable,
                string refTable,
                string foreignKey,
                string refTableKey,
                ReferentialAction onDeleteAction = ReferentialAction.NoAction,
                ReferentialAction onUpdateAction = ReferentialAction.NoAction)
            {
                MainTable = mainTable;
                RefTable = refTable;
                ForeignKey = foreignKey;
                RefTableKey = refTableKey;
                OnDeleteAction = onDeleteAction;
                OnUpdateAction = onUpdateAction;
            }
        }

        public struct IndexCreation
        {
            public string TableName;
            public List<string> ColumnNames;
            public string IndexType;
            public string? Condition;
            public string? Expression;
            public string? IndexTypeCustom;
            public string? IndexName;
            public bool? UseJsonbPathOps;
        }

        public struct TableCreation
        {
            public string TableName;
            public string? Description;
            public List<string> Rows;
            public List<string>? CustomRows;
        }

    }
}
