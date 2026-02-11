using DatabaseDesigner;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using static DatabaseDesigner.DBDesigner;
using static DatabaseDesigner.SessionStorage;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Database_Designer
{
    public partial class CreateTable : Page
    {

        MainPage mainPaged;
        private List<SessionStorage.TableObject>? TemplateData = null;

        public UIWindowEntry WindowInfo { get; private set; }

        public CreateTable(MainPage mainPage)
        {
            this.InitializeComponent();
            mainPaged = mainPage;

            Default.Click += (s, e) =>
            {
                TemplateData = null;
            };

            Error.Visibility = Visibility.Collapsed;

            Cancel.Click += (s, e) =>
            {
                mainPaged.DesktopCanvas.Children.Remove(this);
            };

            Finalize.Click += (s, e) =>
            {
                var schemaInput = SchemaInput.Text.Trim(); // can be empty
                var tableInputTemp = TableInput.Text.Trim();

                // Step 1: Normalize table name
                // Replace all dots and spaces in table name with underscores
                var tableName = string.IsNullOrWhiteSpace(tableInputTemp)
                    ? ""
                    : tableInputTemp.Replace('.', '_').Replace(' ', '_');

                // Step 2: Normalize schema
                string normalizedSchema;
                if (string.IsNullOrEmpty(schemaInput))
                {
                    normalizedSchema = "public";
                }
                else
                {
                    var schemaParts = schemaInput.Split('.');
                    normalizedSchema = schemaParts.Length > 1
                        ? "\"" + schemaInput + "\"" // quote if multiple dots
                        : schemaInput;
                }

                // Step 3: Combine schema + table name
                string normalizedName = normalizedSchema + "." + tableName;

                // Step 4: Check for duplicate names
                var usedTitles = mainPaged.MainSessionInfo.Tables
                    .Select(item => (string.IsNullOrEmpty(item.SchemaName) ? "public" : item.SchemaName) + "." + item.TableName)
                    .ToList();

                if (usedTitles.Contains(normalizedName))
                {
                    SetError("The table name has already been used. Please choose a different name.");
                    return;
                }

                // Step 5: Validate allowed characters
                var allowedChars = new HashSet<char>(
                    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_()-=+,:;/\\!?@[]{} .\""
                );

                if (string.IsNullOrWhiteSpace(tableName) || !tableName.All(c => allowedChars.Contains(c)))
                {
                    SetError("Please enter a valid name for the table; only allowed characters can be used.");
                    return;
                }

                // Step 6: Create the new table object

                if (TemplateData != null)
                {
                    foreach (var item in TemplateData)
                    {
                        var prefixedTableName = string.IsNullOrWhiteSpace(tableName)
                            ? item.TableName
                            : tableName + "_" + item.TableName;

                        var newTable = new SessionStorage.TableObject
                        {
                            SchemaName = normalizedSchema,
                            TableName = prefixedTableName,
                            Description = Description.Text.Trim(),
                            Rows = item.Rows.ToList(),
                            References = item.References.ToList(),
                            Indexes = item.Indexes.ToList(),
                            CustomRows = item.CustomRows?.ToList() ?? new List<string>()
                        };

                        mainPaged.MainSessionInfo.Tables.Add(newTable);
                    }
                }

                else
                {

                    if (string.IsNullOrWhiteSpace(tableName))
                    {
                        SetError("Please enter a valid name for the table.");
                        return;
                    }

                    // Create a single table from user input
                    var newTable = new SessionStorage.TableObject
                    {
                        SchemaName = normalizedSchema,
                        TableName = tableName,
                        Description = Description.Text.Trim(),
                        Rows = new List<RowCreation>(), // Empty rows - user will add them later
                        References = new List<ReferenceOptions>(),
                        Indexes = new List<IndexCreation>(),
                        CustomRows = new List<string>()
                    };

                    mainPaged.MainSessionInfo.Tables.Add(newTable);
                }


                mainPaged.ForceCollectionChangeUpate();

                // Close the window
                mainPaged.DesktopCanvas.Children.Remove(this);

            };


            Templates.Click += (s, e) =>
            {

                mainPage.CreateWindows("Template",  () => new CreateNewTableTemplateSelection(this, mainPage, mainPage.SeshDirectory.ConvertToString() + "/" + mainPage.SeshUsername.ConvertToString()), true);

            };

            ExitButton.Click += (s, e) => { try { if (mainPage.IntroPage.Children.Contains(this)) mainPage.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };


            this.Unloaded += (s, e) =>
            {
                RemoveWindow();
            };



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



        // Executes when the user navigates to this page.
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }


        public void TemplateSelected(List<SessionStorage.TableObject>? template, string templateName)
        {
            TemplateData = template;
            NoTemplate.Text = "Using template: " + templateName;
        }

        public void SetSelectedText()
        {

        }

        public void SetError(string error)
        {
            Error.Visibility = Visibility.Visible;
            Error.Text = error;
        }


    }
}
