using DatabaseDesigner;
using Microsoft.VisualBasic;
using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using static DatabaseDesigner.Index;
using static DatabaseDesigner.Reference;
using static DatabaseDesigner.Row;
using static OpenSilver.Features;

namespace Database_Designer
{
    public partial class DatabaseViewer : Page
    {
        public MainPage mainPaged;
        string mainTableName;

        public UIWindowEntry WindowInfo { get; private set; }


        public DatabaseViewer(MainPage mainPaged, string TableName)
        {


            this.InitializeComponent();
            this.mainPaged = mainPaged;
            this.mainTableName = TableName;

            PreviewPanel.Visibility = Visibility.Visible;
            DetailsPanel.Visibility = Visibility.Collapsed;

            PopulatePage();

            MainPage.IndexChanged += (sender, e) =>
            {
                PopulatePage();
            };

            MainPage.TableChanged += (sender, e) =>
            {
                PopulatePage();
            };

            MainPage.RefChanged += (sender, e) =>
            {
                PopulatePage();
            };
            ExitButton.Click += (s, e) => { try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };


            DeleteTable.Click += (s,e) => mainPaged.CreateWindow(() => new ConfirmDeleteTable(mainPaged, mainTableName, this), "Delete Table", true);




            EditData.Click += (s, e) =>
            {
                if (selectedItem.GetType() == typeof(SessionStorage.RowCreation))
                {

                    var selectedRow = (SessionStorage.RowCreation)selectedItem;

                    var newWindow = mainPaged.CreateWindow(
    () => new BasicDatabaseDesigner(mainPaged, this, null, BasicDatabaseDesigner.DesignMode.Design, selectedRow, null, null, tableData),
    "Database Viewer", false
);




                }


                if (selectedItem.GetType() == typeof(SessionStorage.IndexCreation))
                {

                    var Index = (SessionStorage.IndexCreation)selectedItem;
                    var newWindow = mainPaged.CreateWindow(
() => new BasicDatabaseDesigner(mainPaged, this, null, BasicDatabaseDesigner.DesignMode.Index, null, null, Index, tableData),
"Database Viewer", false
);

                }


                if (selectedItem.GetType() == typeof(SessionStorage.ReferenceOptions))
                {
                    var Ref = (SessionStorage.ReferenceOptions)selectedItem;
                    var newWindow = mainPaged.CreateWindow(
    () => new BasicDatabaseDesigner(mainPaged, this, null, BasicDatabaseDesigner.DesignMode.Ref, null, Ref, null, tableData),
    "Database Viewer", false
);
                    

                }
            };


            DeleteData.Click += (s, e) =>
            {


                if (selectedItem.GetType() == typeof(SessionStorage.RowCreation))
                {

                    var selectedRow = (SessionStorage.RowCreation)selectedItem;

                    foreach (var item in tableData.Rows)
                    {
                        if (item.GetHashCode() == selectedRow.GetHashCode())
                        {
                            tableData.Rows.Remove(item);
                            PopulatePage();
                            break;
                        }
                    }

                }



                if (selectedItem.GetType() == typeof(SessionStorage.IndexCreation))
                {

                    var Index = (SessionStorage.IndexCreation)selectedItem;

                    foreach (var item in tableData.Indexes)
                    {
                        if (item.GetHashCode() == Index.GetHashCode())
                        {
                            tableData.Indexes.Remove(item);
                            PopulatePage();
                            break;
                        }
                    }

                }


                if (selectedItem.GetType() == typeof(SessionStorage.ReferenceOptions))
                {
                    var Ref = (SessionStorage.ReferenceOptions)selectedItem;

                    foreach (var item in tableData.References)
                    {
                        if (item.GetHashCode() == Ref.GetHashCode())
                        {
                            tableData.References.Remove(item);
                            PopulatePage();
                            break;
                        }
                    }
                }

                ResetViewer();

                mainPaged.ForceCollectionChangeUpate();

            };





            string schemaName = null;
            string tableName = null;

            if (!string.IsNullOrEmpty(mainTableName))
            {
                mainTableName = mainTableName.Trim();

                if (mainTableName.StartsWith("\""))
                {
                    // Quoted schema
                    int endQuoteIndex = mainTableName.IndexOf('"', 1);
                    if (endQuoteIndex > 0 && endQuoteIndex < mainTableName.Length - 1 && mainTableName[endQuoteIndex + 1] == '.')
                    {
                        schemaName = mainTableName.Substring(0, endQuoteIndex + 1); // keep quotes
                        tableName = mainTableName.Substring(endQuoteIndex + 2); // after dot
                    }
                    else
                    {
                        // No dot after quoted schema, treat everything as table
                        tableName = mainTableName;
                    }
                }
                else
                {
                    // Unquoted schema
                    var parts = mainTableName.Split('.');
                    if (parts.Length > 1)
                    {
                        schemaName = string.Join(".", parts.Take(parts.Length - 1));
                        tableName = parts.Last();
                    }
                    else
                    {
                        tableName = mainTableName;
                    }
                }
            }
            var selectedTable = mainPaged.MainSessionInfo.Tables
                .FirstOrDefault(s =>
                {
                    // Normalize schema for comparison: remove quotes and trim
                    string normalizedSchema = (s.SchemaName ?? "public").Trim().Trim('"');
                    string targetSchema = (schemaName ?? "public").Trim().Trim('"');

                    // Compare schema and table name
                    return normalizedSchema.Equals(targetSchema, StringComparison.OrdinalIgnoreCase)
                           && string.Equals(s.TableName, tableName, StringComparison.OrdinalIgnoreCase);
                });




            TableNameUI.Text = mainTableName;
            AddColumn.Click += (s, e) =>
            {

                var newWindow = mainPaged.CreateWindow(
                    () => new BasicDatabaseDesigner(mainPaged, this, null, BasicDatabaseDesigner.DesignMode.Design, null, null, null, selectedTable),
                    "Database Viewer", false
                );
                this.IsVisibleChanged += (s, e) =>
                {
                    mainPaged.MirrorParentVisibility(newWindow as FrameworkElement, this);

                };


            };

            AddNewIndex.Click += (s, e) =>
            {


                var newWindow = mainPaged.CreateWindow(
                    () => new BasicDatabaseDesigner(mainPaged, this, null, BasicDatabaseDesigner.DesignMode.Index, null, null, null, selectedTable),
                    "Database Viewer", false
                );
                this.IsVisibleChanged += (s, e) =>
                {
                    mainPaged.MirrorParentVisibility(newWindow as FrameworkElement, this);

                };
            };

            AddNewRef.Click += (s, e) =>
            {


                var newWindow = mainPaged.CreateWindow(
                    () => new BasicDatabaseDesigner(mainPaged, this, null, BasicDatabaseDesigner.DesignMode.Ref, null, null, null, selectedTable),
                    "Database Viewer", false
                );
                this.IsVisibleChanged += (s, e) =>
                {
                    mainPaged.MirrorParentVisibility(newWindow as FrameworkElement, this);

                };
            };






            this.Unloaded += (s, e) =>
            {
                RemoveWindow();
            };



        }


        public void ResetViewer()
        {
            DetailsPanel.Visibility = Visibility.Collapsed;

            PreviewPanel.Visibility = Visibility.Visible;
        }
        public void RemoveWindow()
        {
            try
            {
                //if (WindowInfo == null) return;

                if (mainPaged?.LowerAppBar != null && WindowInfo.Shortcut != null)
                {
                    try
                    {
                        if (mainPaged.LowerAppBar.Children.Contains(WindowInfo.Shortcut))
                            mainPaged.LowerAppBar.Children.Remove(WindowInfo.Shortcut);
                        if (WindowInfo.Shortcut is FrameworkElement sh) sh.Visibility = Visibility.Collapsed;
                    }
                    catch { }
                }

                if (WindowInfo.Elements != null && mainPaged?.IntroPage != null)
                {
                    for (int i = WindowInfo.Elements.Count - 1; i >= 0; i--)
                    {
                        var item = WindowInfo.Elements[i];
                        try
                        {
                            if (item is FrameworkElement fe)
                            {
                                fe.Visibility = Visibility.Collapsed;
                                fe.DataContext = null;
                            }
                            if (mainPaged.IntroPage.Children.Contains(item))
                                mainPaged.IntroPage.Children.Remove(item);
                        }
                        catch { }
                    }
                    try { WindowInfo.Elements.Clear(); } catch { }
                }

                try
                {
                    if (this is FrameworkElement me) me.Visibility = Visibility.Collapsed;
                    if (mainPaged?.IntroPage != null && mainPaged.IntroPage.Children.Contains(this))
                        mainPaged.IntroPage.Children.Remove(this);
                }
                catch { }
            }
            catch { }

        }

        public void PopulatePage()
        {
            RowButtons.Children.Clear();

            string schemaName = null;
            string tableName = null;

            if (!string.IsNullOrEmpty(mainTableName))
            {
                var parts = mainTableName.Split('.');
                if (parts.Length > 1)
                {
                    tableName = parts[^1]; // last part
                    schemaName = string.Join(".", parts[..^1]); // all parts except last
                }
                else
                {
                    tableName = mainTableName;
                    schemaName = "public"; 
                }
            }

            var selectedTable = mainPaged.MainSessionInfo.Tables
                .FirstOrDefault(s =>
                {
                    // Normalize schema for comparison: remove quotes if present
                    string normalizedSchema = s.SchemaName?.Trim('"') ?? "public";
                    string targetSchema = schemaName?.Trim('"') ?? "public";

                    return normalizedSchema.Equals(targetSchema, StringComparison.OrdinalIgnoreCase)
                        && s.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase);
                });


            TableNameUI.Text = mainTableName;
         


            TableDetails.Text = selectedTable.Description ?? "No description provided.";

            if (selectedTable.Rows != null)
            {

                foreach (var row in selectedTable.Rows)
                {
                    var button = CreateDataButton("(Row) " + row.Name, row.Description ?? "No description provided.", row);
                    button.Click += (s, e) =>
                    {
                        selectedItem = row;
                        selectedItemChanged(selectedTable);
                        LoadItemView((SessionStorage.RowCreation)row);

                        TableDetails.Text = selectedTable.Description != null ? selectedTable.Description : "No description provided.";
                    };
                    RowButtons.Children.Add(button);
                }
            }


            int indexInt = 0;

            if (selectedTable.Indexes != null)

            {
                foreach (var index in selectedTable.Indexes)
                {

                    string indexTypeStr;

                    if (index.IndexType != default)
                    {
                        indexTypeStr = index.IndexType;
                    }

                    else if (index.IndexTypeCustom != default)
                    {
                        indexTypeStr = index.IndexTypeCustom;
                    }

                    else
                    {
                        indexTypeStr = "This is corrupted, please delete!";
                    }

                    var button = CreateDataButton("(Index) Index No. " + indexInt.ToString(), indexTypeStr ?? "No description provided.", index);
                    button.Click += (s, e) =>
                    {
                        selectedItem = index;
                        selectedItemChanged(selectedTable);
                        LoadItemView((SessionStorage.IndexCreation)index);

                        TableDetails.Text = selectedTable.Description != null ? selectedTable.Description : "No description provided.";
                    };
                    RowButtons.Children.Add(button);


                    indexInt++;
                }
            }
       
            if (selectedTable.References != null)
            {

                foreach (var reference in selectedTable.References)
                {

                    string Sanitize(string input) => input.Replace(".", "_");

                    string constraintName = $"(Ref) {Sanitize(reference.MainTable)}_{Sanitize(reference.ForeignKey)}_to_{Sanitize(reference.RefTable)}";


                    string deleteActionDescription = reference.OnDeleteAction switch
                    {
                        ReferentialAction.Cascade => "Cascading Update",
                        ReferentialAction.SetNull => "Nullify",
                        ReferentialAction.SetDefault => "Set Default Value",
                        ReferentialAction.Restrict => "Delete",
                        _ => "Nothing"
                    };

                    string updateActionDescription = reference.OnUpdateAction switch
                    {
                        ReferentialAction.Cascade => "Cascading Update",
                        ReferentialAction.SetNull => "Nullify",
                        ReferentialAction.SetDefault => "Set Default Value",
                        ReferentialAction.Restrict => "Delete",
                        _ => "Nothing"
                    };


                    var description = "OnDelete: " + deleteActionDescription + ", OnUpdate: " + updateActionDescription;


                    var button = CreateDataButton(constraintName, description ?? "No description provided.", reference);
                    button.Click += (s, e) =>
                    {
                        selectedItem = reference;
                        selectedItemChanged(selectedTable);
                        LoadItemView((SessionStorage.ReferenceOptions)reference);

                        TableDetails.Text = selectedTable.Description != null ? selectedTable.Description : "No description provided.";
                    };
                    RowButtons.Children.Add(button);

                    //Ref

                }

            }










        }


        public object selectedItem = default;
        private SessionStorage.TableObject tableData = default;
        void selectedItemChanged(SessionStorage.TableObject tObj)
        {

            ClearScreen.Visibility = Visibility.Collapsed;

            EditData.Visibility = Visibility.Visible;
            DeleteData.Visibility = Visibility.Visible;

            tableData = tObj;

        }

        



        public void LoadItemView(SessionStorage.RowCreation Row)
        {
            if (selectedItem == null)
            {
                return;
            }
            var selectedRow = (SessionStorage.RowCreation)selectedItem;


            RowDetailsList.Children.Clear();

            PreviewPanel.Visibility = Visibility.Collapsed;
            DetailsPanel.Visibility = Visibility.Visible;

            RowDetailsList.Children.Add(CreateLabeledStackPanel("Column Details", true));

            // Load row options
            RowDetailsList.Children.Add(CreateLabeledStackPanel("Column Name: " + selectedRow.Name, false));
            RowDetailsList.Children.Add(CreateLabeledStackPanel("Description: " + selectedRow.Description, false));

            RowName.Text = selectedRow.Name;
            RowDetails.Text = selectedRow.Description;

            if (selectedRow.EncryptedAndNOTMedia == true)
            {
                RowDetailsList.Children.Add(CreateLabeledStackPanel("Data Type: Secure String", false));
            }
            else if (selectedRow.Media == true)
            {
                RowDetailsList.Children.Add(CreateLabeledStackPanel("Data Type: Media", false));
            }
            else
            {
                RowDetailsList.Children.Add(CreateLabeledStackPanel("Data Type: " + selectedRow.RowType?.ToString() ?? "Data Corrupted, Please Fix Or Delete This Column!", false));
            }

            if (selectedRow.Limit != null)
            {
                RowDetailsList.Children.Add(CreateLabeledStackPanel("Limit: " + selectedRow.Limit.ToString(), false));
            }

            if (selectedRow.IsArray == true)
            {
                RowDetailsList.Children.Add(CreateLabeledStackPanel("This Is An Array!", false));
                if (!string.IsNullOrEmpty(selectedRow.ArrayLimit))
                {
                    RowDetailsList.Children.Add(CreateLabeledStackPanel("Array Limit: " + selectedRow.ArrayLimit, false));
                }
            }

            if (selectedRow.IsPrimary == true)
            {
                RowDetailsList.Children.Add(CreateLabeledStackPanel("This Is A Primary Key", true)); // Bold for emphasis
            }

            if (selectedRow.IsUnique == true)
            {
                RowDetailsList.Children.Add(CreateLabeledStackPanel("This Value Must Be Unique", true));
            }

            if (selectedRow.IsNotNull == true)
            {
                RowDetailsList.Children.Add(CreateLabeledStackPanel("This Value Can Not Be Null Or Empty", true));
            }

            if (!string.IsNullOrEmpty(selectedRow.DefaultValue))
            {
                if (selectedRow.DefaultIsPostgresFunction == true)
                {
                    RowDetailsList.Children.Add(CreateLabeledStackPanel("Default Value: " + selectedRow.DefaultValue + "()", false));
                }
                else
                {
                    RowDetailsList.Children.Add(CreateLabeledStackPanel("Default Value: " + selectedRow.DefaultValue, false));
                }
            }

            if (!string.IsNullOrEmpty(selectedRow.Check))
            {
                RowDetailsList.Children.Add(CreateLabeledStackPanel("Check: " + selectedRow.Check, false));
            }


        }


        public void LoadItemView(SessionStorage.IndexCreation Index)
        {

            if (selectedItem == null)
            {
                return;
            }

            RowDetailsList.Children.Clear();
            PreviewPanel.Visibility = Visibility.Collapsed;
            DetailsPanel.Visibility = Visibility.Visible;

            Dictionary<string, IndexType> IndexVals = new()
    {
        { "Basic", IndexType.Basic },
        { "Composite", IndexType.Composite },
        { "Partial", IndexType.Partial },
        { "Expression", IndexType.Expression },
        { "Gin", IndexType.Gin },
        { "Unique", IndexType.Unique },
        { "Hash", IndexType.Hash },
        { "Custom", IndexType.Custom }
    };


            RowDetailsList.Children.Add(CreateLabeledStackPanel("Index Details", true));

            // Safely resolve the index type
            if (!IndexVals.TryGetValue(Index.IndexType, out IndexType finalizedIndexType))
                throw new InvalidOperationException($"Unknown index type: {Index.IndexType}");

            string columns;

        
            // Use generated SQL name as fallback if IndexName is not set
            string valToUseAsName = !string.IsNullOrWhiteSpace(Index.IndexName) ? Index.IndexName : "Name will be auto generated on build!";

            RowDetailsList.Children.Add(CreateLabeledStackPanel("Name: " + valToUseAsName, false));
            RowDetailsList.Children.Add(CreateLabeledStackPanel("Column Count: " + Index.ColumnNames.Count, false));

            RowName.Text = valToUseAsName;
            RowDetails.Text = $"Column Count: {Index.ColumnNames.Count}";

            RowDetailsList.Children.Add(CreateLabeledStackPanel("Columns:", true));
            int columnIndex = 1;
            foreach (var column in Index.ColumnNames)
            {
                RowDetailsList.Children.Add(CreateLabeledStackPanel($"Column {columnIndex}: {column}", false));
                columnIndex++;
            }



            if (!string.IsNullOrWhiteSpace(Index.Condition))
                RowDetailsList.Children.Add(CreateLabeledStackPanel("Condition: " + Index.Condition, false));

            if (!string.IsNullOrWhiteSpace(Index.Expression))
                RowDetailsList.Children.Add(CreateLabeledStackPanel("Expression: " + Index.Expression, false));

        }


        public void LoadItemView(SessionStorage.ReferenceOptions Ref)
        {

            if (selectedItem == null)
            {
                return;
            }

            RowDetailsList.Children.Clear();

            PreviewPanel.Visibility = Visibility.Collapsed;
            DetailsPanel.Visibility = Visibility.Visible;



            string IndexName = default;

            RowDetailsList.Children.Add(CreateLabeledStackPanel("Reference Details", true));



           

            RowDetailsList.Children.Add(CreateLabeledStackPanel("Columns:", true));




            string Sanitize(string input) => string.IsNullOrEmpty(input) ? "" : input.Replace(".", "_");

            string constraintName = $"fk_{Sanitize(Ref.MainTable)}_{Sanitize(Ref.ForeignKey)}_to_{Sanitize(Ref.RefTable)}";

            RowDetailsList.Children.Add(CreateLabeledStackPanel("Name: " + constraintName, false));

            RowDetailsList.Children.Add(CreateLabeledStackPanel("Reference Table: " + Ref.RefTable, false));
            RowDetailsList.Children.Add(CreateLabeledStackPanel("Reference Key: " + Ref.RefTableKey, false));
            RowDetailsList.Children.Add(CreateLabeledStackPanel("Referenced Key In This Table: " + Ref.ForeignKey, false));


            RowName.Text = constraintName;
            RowDetails.Text = $"Reference Table: {Ref.RefTable} Ref Key: {Ref.RefTableKey} ";



            string deleteActionDescription = Ref.OnDeleteAction switch
            {
                ReferentialAction.Cascade => "When a referenced row is deleted, all related rows in this table are also deleted.",
                ReferentialAction.SetNull => "When a referenced row is deleted, related foreign key columns in this table are set to NULL.",
                ReferentialAction.SetDefault => "When a referenced row is deleted, related foreign key columns in this table are set to their default values.",
                ReferentialAction.Restrict => "Deletion of the referenced row is blocked if it's still being used in this table.",
                _ => "No automatic changes happen when a referenced row is deleted.",
            };


            string updateActionDescription = Ref.OnUpdateAction switch
            {
                ReferentialAction.Cascade => "When the referenced key is updated, all related keys in this table are updated as well.",
                ReferentialAction.SetNull => "When the referenced key is updated, related foreign key columns in this table are set to NULL.",
                ReferentialAction.SetDefault => "When the referenced key is updated, related foreign key columns in this table are set to their default values.",
                ReferentialAction.Restrict => "Updating the referenced key is blocked if it's still being used in this table.",
                _ => "No automatic changes happen when the referenced key is updated.",

            };

                RowDetailsList.Children.Add(CreateLabeledStackPanel(deleteActionDescription, false));
            RowDetailsList.Children.Add(CreateLabeledStackPanel(updateActionDescription, false));



        }


  
        















        public Button CreateCustomButton(string itemName, string subText, SessionStorage.TableObject tableData, MainPage mainPaged, string mainTableName)
        {
            var button = new Button
            {
                Height = 50,
                Margin = new Thickness(0, 6, 0, 6),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x45, 0x41, 0x38))
            };

            var outerStack = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var image = new Image
            {
                Source = new BitmapImage(new Uri("Assets/Images/WhiteLogo.png", UriKind.Relative)),
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var innerStack = new StackPanel
            {
                Width = 307
            };

            var titleText = new TextBlock
            {
                Text = itemName,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                FontSize = 14,
                Foreground = (Brush)Application.Current.Resources["TextLight"]
            };

            var subTextBlock = new TextBlock
            {
                Text = subText,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(0xDD, 0xDD, 0xDD, 0xDD))
            };

            innerStack.Children.Add(titleText);
            innerStack.Children.Add(subTextBlock);

            outerStack.Children.Add(image);
            outerStack.Children.Add(innerStack);

            button.Content = outerStack;

            button.Click += (s, e) =>
            {
                    var newWindow = mainPaged.CreateWindow(
                        () => new BasicDatabaseDesigner(mainPaged, this, null, BasicDatabaseDesigner.DesignMode.Design, null, null, null, tableData),
                        "Database Viewer", false
                    );

                this.IsVisibleChanged += (s, e) =>
                {
                    mainPaged.MirrorParentVisibility(newWindow as FrameworkElement, this);

                };
            };

            return button;
        }

        public Button CreateDataButton(string mainText, string subText, SessionStorage.RowCreation tableData)
        {
            // Create the button
            var button = new Button
            {
                Height = 50,
                Margin = new Thickness(0, 6, 0, 0),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x45, 0x41, 0x38))
            };

            // Horizontal stack for image + text
            var outerStack = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            // Image
            var image = new Image
            {
                Source = new BitmapImage(new Uri("Assets/Images/WhiteLogo.png", UriKind.Relative)),
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 8, 0)
            };

            // Inner stack for texts
            var innerStack = new StackPanel
            {
                Width = 307
            };

            // Main text
            var mainTextBlock = new TextBlock
            {
                Text = mainText,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                FontSize = 14,
                Foreground = new SolidColorBrush(Colors.White)
            };

            // Sub text
            var subTextBlock = new TextBlock
            {
                Text = subText,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(0xDD, 0xDD, 0xDD, 0xDD))
            };

            // Build hierarchy
            innerStack.Children.Add(mainTextBlock);
            innerStack.Children.Add(subTextBlock);

            outerStack.Children.Add(image);
            outerStack.Children.Add(innerStack);

            button.Content = outerStack;

            return button;
        }

        public Button CreateDataButton(string mainText, string subText, SessionStorage.IndexCreation indexData)
        {
            // Create the button
            var button = new Button
            {
                Height = 50,
                Margin = new Thickness(0, 6, 0, 0),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x45, 0x41, 0x38))
            };

            // Horizontal stack for image + text
            var outerStack = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            // Image
            var image = new Image
            {
                Source = new BitmapImage(new Uri("Assets/Images/WhiteLogo.png", UriKind.Relative)),
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 8, 0)
            };

            // Inner stack for texts
            var innerStack = new StackPanel
            {
                Width = 307
            };

            // Main text
            var mainTextBlock = new TextBlock
            {
                Text = mainText,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                FontSize = 14,
                Foreground = new SolidColorBrush(Colors.White)
            };

            // Sub text
            var subTextBlock = new TextBlock
            {
                Text = subText,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(0xDD, 0xDD, 0xDD, 0xDD))
            };

            // Build hierarchy
            innerStack.Children.Add(mainTextBlock);
            innerStack.Children.Add(subTextBlock);

            outerStack.Children.Add(image);
            outerStack.Children.Add(innerStack);

            button.Content = outerStack;

            return button;
        }

        public Button CreateDataButton(string mainText, string subText, SessionStorage.ReferenceOptions indexData)
        {
            // Create the button
            var button = new Button
            {
                Height = 50,
                Margin = new Thickness(0, 6, 0, 0),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x45, 0x41, 0x38))
            };

            // Horizontal stack for image + text
            var outerStack = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            // Image
            var image = new Image
            {
                Source = new BitmapImage(new Uri("Assets/Images/WhiteLogo.png", UriKind.Relative)),
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 8, 0)
            };

            // Inner stack for texts
            var innerStack = new StackPanel
            {
                Width = 307
            };

            // Main text
            var mainTextBlock = new TextBlock
            {
                Text = mainText,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                FontSize = 14,
                Foreground = new SolidColorBrush(Colors.White)
            };

            // Sub text
            var subTextBlock = new TextBlock
            {
                Text = subText,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(0xDD, 0xDD, 0xDD, 0xDD))
            };

            // Build hierarchy
            innerStack.Children.Add(mainTextBlock);
            innerStack.Children.Add(subTextBlock);

            outerStack.Children.Add(image);
            outerStack.Children.Add(innerStack);

            button.Content = outerStack;

            return button;
        }





        public static StackPanel CreateLabeledStackPanel(
        string labelText,
        bool bolded = false)
        {
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 6)
            };

            var image = new Image
            {
                Source = new BitmapImage(new Uri("Assets/Images/BlackLogo.png", UriKind.Relative)),
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var textBlock = new TextBlock
            {
                Text = labelText,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                FontSize = bolded ? 16 : 14,
                FontWeight = bolded ? FontWeights.Bold : FontWeights.Normal,
                Foreground = (Brush)Application.Current.Resources["TextDark"],
                VerticalAlignment = VerticalAlignment.Center
            };

            stackPanel.Children.Add(image);
            stackPanel.Children.Add(textBlock);

            return stackPanel;
        }

    }
}
