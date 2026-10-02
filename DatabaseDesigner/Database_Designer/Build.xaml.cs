 
using DatabaseDesigner;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Walker.Crypto;
using NodeCompiler   = Database_Designer.NodeWalker.NodeWalker.Compiler;
using NodeOperations = Database_Designer.NodeWalker.NodeWalker.Operations;
using WISecureData;
using static Database_Designer.MainPage;
using static DatabaseDesigner.DBDesigner;
using static DatabaseDesigner.Index;
using static DatabaseDesigner.Row;
using static DatabaseDesigner.SessionStorage;
using static OpenSilver.Features;
using static System.Net.Mime.MediaTypeNames;
using Image = System.Windows.Controls.Image;
namespace Database_Designer
{
    public partial class Build : Page
    {
        MainPage mainPage;
        public UIWindowEntry WindowInfo { get; private set; }
        static string baseUrl;
        public ObservableCollection<Descriptions> DescInfo { get; set; } = new ObservableCollection<Descriptions>();
        public Build(MainPage mainPaged)
        {
            this.InitializeComponent();
            Home.Visibility = Visibility.Visible;
            BuildProject.Visibility = Visibility.Collapsed;
            mainPage = mainPaged;
            ClearScreen.Click += (s, e) =>
            {
                mainPage.IntroPage.Children.Clear();
                mainPage.LowerAppBar.Children.Clear();
            };
            ViewBuilds.Click += (s, e) =>
            {
                var CurrentDirectory = Path.Combine(mainPage.SeshDirectory.ConvertToString(), mainPage.SeshUsername.ConvertToString(), "Projects", mainPaged.ProjectName);


                if (!Directory.Exists(CurrentDirectory))
                    Directory.CreateDirectory(CurrentDirectory);

                // Open the directory in File Explorer
                Process.Start(new ProcessStartInfo
                {
                    FileName = CurrentDirectory,
                    UseShellExecute = true,
                    Verb = "open"
                });

            };
            ProjectErrorCheck.Errors.Clear();
            ValidationCheck();
            BuildCheck.Click += (s, e) =>
            {
                ValidationCheck();
            };
            void ValidationCheck()
            {
                ProjectErrorCheck.Errors.Clear();
                var issues = ProjectValidator.Validate(mainPage.MainSessionInfo.Tables, mainPage.RLSJson);
                int errors = issues.Count(i => i.Severity == ProjectValidator.Severity.Error);
                int warnings = issues.Count(i => i.Severity == ProjectValidator.Severity.Warning);

                ProjectErrorCheck.Errors.Add(new ValidationSummaryItem
                {
                    Message = errors == 0 && warnings == 0
                        ? "Congrats, the project is good to go! Export as you please."
                        : $"{errors} error(s), {warnings} warning(s), {issues.Count - errors - warnings} tip(s). " +
                          (errors > 0 ? "Fix the errors before exporting — the generated SQL or API would fail." : "You can export, but check the warnings.")
                });

                foreach (var issue in issues)
                {
                    var prefix = issue.Severity switch
                    {
                        ProjectValidator.Severity.Error => "⛔ ",
                        ProjectValidator.Severity.Warning => "⚠ ",
                        _ => "ℹ "
                    };
                    var errorButton = new Button
                    {
                        Content = new TextBlock { Text = prefix + issue.Message, TextWrapping = TextWrapping.Wrap },
                        Margin = new Thickness(2),
                        Background = new SolidColorBrush(Colors.Transparent),
                        BorderThickness = new Thickness(0),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Colors.White),
                        Cursor = issue.Table != null ? Cursors.Hand : Cursors.Arrow
                    };
                    var target = issue.Table;
                    if (target != null)
                        errorButton.Click += (s, e) =>
                            mainPage.CreateWindow(() => new DatabaseViewer(mainPage, target), "Database Viewer", true);
                    ProjectErrorCheck.Errors.Add(new ValidationSummaryItem
                    {
                        Context = errorButton,
                        Message = prefix + issue.Message
                    });
                }
            }
            Export.Click += (s, e) =>
            {
                BuildProject.Visibility = Visibility.Visible;
                Home.Visibility = Visibility.Collapsed;
            };
            BackBtn.Click += (s, e) =>
            {
                Home.Visibility = Visibility.Visible;
                BuildProject.Visibility = Visibility.Collapsed;
            };

            BuildProjectBtn.Click += async (s, e) =>
            {

                //This is the code from the DLL, but I needed to put it like this for my own purposes!
                //Pretty hypocritical as the developer of both, right?
                try
                {
                    List<DatabaseDesign> DatabaseDesignerList = new List<DatabaseDesign>();
                    var tableSnapshots = new Dictionary<string, MigrationGenerator.TableSnap>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in mainPaged.MainSessionInfo.Tables)
                    {
                        if (item.Rows.Count == 0)
                        {
                            //Skip to next item
                            continue;
                        }
                        string TableName = item.TableName;
                        string TableDescription = item.Description;
                        var TableRows = new List<RowOptions>();

                        for (int i = 0; i < item.Rows.Count; i++)
                        {
                            var row = item.Rows[i];

                            // Only keep scalar limit if the type supports it
                            if (!SupportsScalarLimit((PostgresType)row.RowType))
                                row.Limit = null;

                            // Only keep array limit if it's actually an array
                            if (row.IsArray != true)
                                row.ArrayLimit = null;

                            var finalizedRow = ConvertRowCreationToOptions(row);
                            TableRows.Add(finalizedRow);
                        }

                        // Helper function
                        bool SupportsScalarLimit(PostgresType type)
                        {
                            return type == PostgresType.Char ||
                                   type == PostgresType.VarChar ||
                                   type == PostgresType.Numeric ||
                                   type == PostgresType.Time ||
                                   type == PostgresType.Timestamp;
                        }


                        //Custom rows will always be empty for now!
                        var References = new List<Reference.ReferenceOptions>();
                        if (item.References != null)
                        {
                            foreach (var reference in item.References)
                            {
                                var referenceOption = new Reference.ReferenceOptions
                                (
                                    reference.MainTable,
                                    reference.RefTable,
                                    reference.ForeignKey,
                                    reference.RefTableKey,
                                    reference.OnDeleteAction,
                                    reference.OnUpdateAction
                                );
                                References.Add(referenceOption);
                            }
                        }
                        var Indexes = new List<IndexDefinition>();
                        if (item.Indexes != null)
                        {
                            foreach (var index in item.Indexes)
                            {
                                Indexes.Add(ConvertIndexCreation(index));
                            }
                        }
                        var DBDesignOutput = DBDesigner.DatabaseDesigner((item.SchemaName + "." + item.TableName), TableDescription, TableRows, null, References, Indexes);
                        DatabaseDesignerList.Add(DBDesignOutput);
                        tableSnapshots[DBDesignOutput.TableName] = MigrationGenerator.SnapshotTable(
                            DBDesignOutput.TableName, TableRows, References, Indexes, DBDesignOutput.SQL);
                    }
                    
                    var CurrentDirectory = Path.Combine(mainPaged.SeshDirectory.ConvertToString(), mainPaged.SeshUsername.ConvertToString(), "Projects", mainPaged.ProjectName);
                    StringBuilder sql = new StringBuilder();
                    StringBuilder doc = new StringBuilder();
                    StringBuilder classes = new StringBuilder();

                    doc.AppendLine("# Project Database");
                    doc.AppendLine("## Generated by DatabaseDesigner");

                    // SecureMedia tables (REAL tables, not composite types)
                    sql.AppendLine("""
CREATE TABLE IF NOT EXISTS SecureMedia (
    id BIGSERIAL PRIMARY KEY,
    is_public BOOLEAN NOT NULL,
    secret_key TEXT NOT NULL,
    public_key TEXT NOT NULL,
    path TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
""");

                    sql.AppendLine("""
CREATE TABLE IF NOT EXISTS SecureMediaSession (
    id BIGSERIAL PRIMARY KEY,
    media_id BIGINT NOT NULL REFERENCES SecureMedia(id) ON DELETE CASCADE,
    user_id BIGINT NOT NULL,
    referenced_media TEXT,
    allowed_user TEXT,
    public_key TEXT,
    last_used TIMESTAMPTZ
);
""");

                    // EF models for SecureMedia
                    classes.AppendLine("""
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System;

public class SecureMedia
{
    [Key]
    public long Id { get; set; }

    public bool IsPublic { get; set; }

    [Required]
    public string SecretKey { get; set; } = null!;

    [Required]
    public string PublicKey { get; set; } = null!;

    [Required]
    public string Path { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<SecureMediaSession> Sessions { get; set; } = new List<SecureMediaSession>();
}

public class SecureMediaSession
{
    [Key]
    public long Id { get; set; }

    [ForeignKey(nameof(Media))]
    public long MediaId { get; set; }

    public SecureMedia Media { get; set; } = null!;

    public long UserId { get; set; }

    public string? ReferencedMedia { get; set; }
    public string? AllowedUser { get; set; }
    public string? PublicKey { get; set; }
    public DateTimeOffset? LastUsed { get; set; }
}
""");

                    // Topologically sort the designs so a table that references
                    // another comes *after* its referent in the SQL output.
                    // Without this, `psql -f SQL.sql` fails on a fresh DB with
                    // "relation does not exist" if the user authored their
                    // tables in the wrong order.
                    var allReferences = mainPaged.MainSessionInfo.Tables
                        .Where(t => t.References != null)
                        .SelectMany(t => t.References.Select(r => new Reference.ReferenceOptions(
                            r.MainTable, r.RefTable, r.ForeignKey, r.RefTableKey,
                            r.OnDeleteAction, r.OnUpdateAction)))
                        .ToList();
                    DatabaseDesignerList = TopoSortDesignsByReferences(DatabaseDesignerList, allReferences);

                    // Append each design's outputs
                    foreach (var item in DatabaseDesignerList)
                    {
                        sql.AppendLine(item.SQL);
                        doc.AppendLine(item.Documentation);
                        classes.AppendLine(item.CsClass);
                    }

                    // Build DbContext source
                    StringBuilder dbContext = new StringBuilder();
                    dbContext.AppendLine("using Microsoft.EntityFrameworkCore;");
                    dbContext.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
                    dbContext.AppendLine();
                    dbContext.AppendLine("public class AppDbContext : DbContext");
                    dbContext.AppendLine("{");
                    dbContext.AppendLine("    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }");
                    dbContext.AppendLine();

                    foreach (var item in DatabaseDesignerList)
                    {
                        // item.ClassName now contains the CLR class name (safe)
                        string classNameSafe = item.ClassName;
                        string dbSetName = classNameSafe + "s"; // naive pluralization
                        dbContext.AppendLine($"    public DbSet<{classNameSafe}> {dbSetName} {{ get; set; }}");
                        dbContext.AppendLine();
                    }

                    dbContext.AppendLine("}");

                    string? GetSchemaFromTable(string tableName)
                    {
                        if (string.IsNullOrEmpty(tableName)) return null;
                        var parts = tableName.Split('.');
                        return parts.Length == 2 ? parts[0] : null;
                    }

                    string GetTableNameWithoutSchema(string tableName)
                    {
                        if (string.IsNullOrEmpty(tableName)) return tableName ?? "";
                        var parts = tableName.Split('.');
                        return parts.Length == 2 ? parts[1] : tableName;
                    }

                    // Write outputs
                    string generatedDBPath = Path.Combine(CurrentDirectory, "GeneratedDB");
           
                        if (!Directory.Exists(generatedDBPath)) Directory.CreateDirectory(generatedDBPath);
                        string[] folders = Directory.GetDirectories(generatedDBPath, "*", SearchOption.TopDirectoryOnly);
                        var versionFolders = folders.Select(f => Path.GetFileName(f))
                            .Where(name => name != null && name.StartsWith("v") && int.TryParse(name.Substring(1), out _))
                            .ToList();

                        string incremental = versionFolders.Count == 0 ? "v1" : "v" + (versionFolders.Max(name => int.Parse(name.Substring(1))) + 1);
                        string generatedDbRoot = generatedDBPath;
                        generatedDBPath = Path.Combine(generatedDBPath, incremental);
                    

                    Directory.CreateDirectory(generatedDBPath);
                    File.WriteAllText(Path.Combine(generatedDBPath, "SQL.sql"), sql.ToString());
                    File.WriteAllText(Path.Combine(generatedDBPath, "Documentation.md"), doc.ToString());
                    File.WriteAllText(Path.Combine(generatedDBPath, "Classes.cs"), classes.ToString());
                    File.WriteAllText(Path.Combine(generatedDBPath, "Models.cs"), dbContext.ToString());

                    var snapshot = new MigrationGenerator.Snapshot
                    {
                        Tables = DatabaseDesignerList
                            .Where(d => tableSnapshots.ContainsKey(d.TableName))
                            .Select(d => tableSnapshots[d.TableName])
                            .ToList()
                    };
                    var (previousSnapshot, previousVersion) = MigrationGenerator.LoadPrevious(generatedDbRoot, incremental);
                    MigrationGenerator.Save(snapshot, generatedDBPath);
                    File.WriteAllText(Path.Combine(generatedDBPath, "Migration.sql"),
                        MigrationGenerator.Generate(previousSnapshot, snapshot, previousVersion, incremental));

                    // --- Generate RLS SQL ---
                    string rlsSql = GenerateRLSSQL(mainPaged.RLSJson, mainPaged.MainSessionInfo.Tables);
                    File.WriteAllText(Path.Combine(generatedDBPath, "RLS.sql"), rlsSql);

                    // --- Generate API (C# Controllers) ---
                    string apiPath = Path.Combine(generatedDBPath, "API");
                    Directory.CreateDirectory(apiPath);
                    ApiGenerator.Generate(
                        apiPath,
                        DatabaseDesignerList.Select(d => new ApiGenerator.ModelInfo(d.ClassName, d.TableName)).ToList(),
                        classes.ToString(),
                        CollectApiFunctions(mainPaged.APIJson),
                        Path.Combine(CurrentDirectory, "Scripts"),
                        Path.Combine(AppContext.BaseDirectory, "PariahCybersecurity.dll"));

                    // --- Generate SpacetimeDB module ---
                    string spacetimePath = Path.Combine(generatedDBPath, "SpacetimeDB");
                    Directory.CreateDirectory(spacetimePath);
                    GenerateSpacetimeDBFiles(spacetimePath, mainPaged.RLSJson, mainPaged.APIJson);


                    // --- Success UI ---
                    ShowBuildSuccess(BuildProjectBtn);
                }

                catch (Exception ex)
                {
                    Console.WriteLine($"Build failed: {ex}");
                    // Show the error in the validation summary so the button
                    // doesn't look like it did nothing.
                    try
                    {
                        ProjectErrorCheck.Errors.Add(new ValidationSummaryItem
                        {
                            Message = "Build failed: " + ex.Message
                        });
                    }
                    catch { /* validation summary may not be in tree yet */ }
                }


            };
            
            
            ExportSpecificRows.Click += (s, e) =>
            {
                BuildProject.Visibility = Visibility.Collapsed;
                //Create reset value system
                ExportRow.Visibility = Visibility.Visible;
            };
            //Build to Tables Templates format
            // - Author (Folder)
            // - - Author Image
            // - - Author Info JSON (Name, Company, Website, License, Note)
            // - Tables Template Pack (Folder)
            // - - Tables
            // - - Banner Image
            // - - Tables Dictionary (Table, string) for descriptions
            // = = Overview (text file)
            CancelRow.Click += (s, e) =>
            {
                ExportRow.Visibility = Visibility.Collapsed;
                //Create reset value system
                BuildProject.Visibility = Visibility.Visible;
            };
            //Later on add a "ADD ALL and DELETE ALL" button, in future update add regex
            foreach (var item in mainPage.MainSessionInfo.Tables)
            {
                var txt = new TextBlock
                {
                    Text = ProjectValidator.Qualified(item),
                    Margin = new Thickness(2),
                    Cursor = Cursors.Hand,
                    FontSize = 14,
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf")
                };
                TablesList.Items.Add(txt);
            }
            var nameColumn = new DataGridTextColumn
            {
                Header = "Table Name",
                Binding = new Binding("TableName"),
                IsReadOnly = true,
            };
            var descColumn = new DataGridTextColumn
            {
                Header = "Description (Optional)",
                Binding = new Binding("Description"),
                IsReadOnly = false,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star) // fills remaining space
            };
            ObservableCollection<Descriptions> SelectedItems = new ObservableCollection<Descriptions>();
            Descriptionse.ItemsSource = SelectedItems;
            TablesList.SelectionChanged += (s, e) =>
            {
                var selectedTableNames = TablesList.SelectedItems
                    .OfType<TextBlock>()
                    .Select(tb => tb.Text)
                    .ToList();
                // Remove deselected
                for (int i = SelectedItems.Count - 1; i >= 0; i--)
                    if (!selectedTableNames.Contains(SelectedItems[i].TableName))
                        SelectedItems.RemoveAt(i);
                // Add newly selected
                foreach (var tableName in selectedTableNames)
                    if (!SelectedItems.Any(d => d.TableName == tableName))
                        SelectedItems.Add(new Descriptions(tableName, ""));
                Descriptionse.ItemsSource = SelectedItems;
            };

            byte[] _bannerBytes = default;
            byte[] _pfpBytes = default;
            BannerPreviewButton1.Click += (s, e) =>
            {
                ImageHelper.SelectAndPreviewImage(BannerPreview1, bytes =>
                {
                    _bannerBytes = bytes;
                    FinalBanner.Source = BannerPreview1.Source;
                });
            };
            PFPPreviewButton2.Click += (s, e) =>
            {
                ImageHelper.SelectAndPreviewImage(PFPPreview, bytes =>
                {
                    _pfpBytes = bytes;
                    PFPImage.Source = PFPPreview.Source;
                });
            };
            string PackName = default;
            T1.TextChanged += (s, e) =>
            {
                PackName = T1.Text;
                V1.Text = $"Pack Name: {(string.IsNullOrWhiteSpace(PackName) ? "No Pack Name Provided (Required)" : PackName)}";
            };
            string Overview = default;
            T2.TextChanged += (s, e) =>
            {
                Overview = T2.Text;
            };
            string AuthorName = default;
            U1.TextChanged += (s, e) =>
            {
                AuthorName = U1.Text;
                AuthorNameTxt.Text = AuthorName;
            };
            string Company = default;
            U2.TextChanged += (s, e) =>
            {
                Company = U2.Text;
            };
            string Website = default;
            U3.TextChanged += (s, e) =>
            {
                Website = U3.Text;
            };
            string License = default;
            U4.TextChanged += (s, e) =>
            {
                License = U4.Text;
            };
            string Note = default;
            U5.TextChanged += (s, e) =>
            {
                Note = U5.Text;
            };
            TabControl1.SelectionChanged += (s, e) =>
            {
                SetText();
                SetErrors();
            };
            void SetText()
            {
                static string Or(string value, string missing) => string.IsNullOrWhiteSpace(value) ? missing : value;
                string TemplateInfo =
                    $"Overview: {Or(Overview, "No Overview Provided (Required)")}\n" +
                    $"Author Name: {Or(AuthorName, "No Author Provided (Required)")}\n" +
                    $"Company: {Or(Company, "No Company Provided")}\n" +
                    $"Website: {Or(Website, "No Website Provided")}\n" +
                    $"License: {Or(License, "No License Provided")}\n" +
                    $"Note: {Or(Note, "No Note Provided")}";
                V2.Text = TemplateInfo;
            }
            void SetErrors()
            {
                ValidationCheck1.Errors.Clear();
                BuildRowTemplate.IsEnabled = false;
                if (string.IsNullOrWhiteSpace(PackName))
                {
                    ValidationCheck1.Errors.Add(new ValidationSummaryItem
                    {
                        Message = "A Template Name Must Be Provided"
                    });
                }
                else if (PackName.Trim().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || PackName.Trim().EndsWith("."))
                {
                    ValidationCheck1.Errors.Add(new ValidationSummaryItem
                    {
                        Message = "The template name can't contain \\ / : * ? \" < > | or end with a dot"
                    });
                }
                if (string.IsNullOrWhiteSpace(Overview))
                {
                    ValidationCheck1.Errors.Add(new ValidationSummaryItem
                    {
                        Message = "An Overview Must Be Provided"
                    });
                }
                if (string.IsNullOrWhiteSpace(AuthorName))
                {
                    ValidationCheck1.Errors.Add(new ValidationSummaryItem
                    {
                        Message = "An Author Must Be Provided"
                    });
                }
                if (TablesList.SelectedItems.Count == 0)
                {
                    ValidationCheck1.Errors.Add(new ValidationSummaryItem
                    {
                        Message = "At least one table must be selected"
                    });
                }
                BuildRowTemplate.IsEnabled = ValidationCheck1.Errors.Count == 0;
            }
            // Re-check as the form is filled in, not only when switching tabs.
            foreach (var box in new[] { T1, T2, U1, U2, U3, U4, U5 })
                box.TextChanged += (s, e) => { SetText(); SetErrors(); };
            TablesList.SelectionChanged += (s, e) => SetErrors();
            SetText();
            SetErrors();
            BuildRowTemplate.Click += async (s, e) =>
            {
                var creationValues = BuildJson();
                // Row templates are user-global: write them where the template
                // browsers actually read from (<user>/Row Templates), not inside
                // the current project folder (which no loader scans).
                var CurrentDirectory = Path.Combine(mainPaged.SeshDirectory.ConvertToString(), mainPaged.SeshUsername.ConvertToString(), "Row Templates");
                // --- Prepare paths ---
                string basePath = string.IsNullOrEmpty(CurrentDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : CurrentDirectory;
                string generatedDBPath = Path.Combine(basePath, PackName.Trim());
                string incremental = "";
                // --- Ensure GeneratedDB exists ---
                Directory.CreateDirectory(generatedDBPath);

                // --- Handle incremental versioning ---
                var folders = Directory.GetDirectories(generatedDBPath, "*", SearchOption.TopDirectoryOnly);

                var versionNumbers = folders
                    .Select(f => Path.GetFileName(f))
                    .Where(name => !string.IsNullOrEmpty(name) && name.StartsWith("v"))
                    .Select(name => int.TryParse(name.Substring(1), out var n) ? (int?)n : null)
                    .Where(n => n.HasValue)
                    .Select(n => n.Value)
                    .ToList();
                incremental = versionNumbers.Count == 0 ? "v1" : "v" + (versionNumbers.Max() + 1);
                generatedDBPath = Path.Combine(generatedDBPath, incremental);
                // Create incremental folder
                Directory.CreateDirectory(generatedDBPath);

// --- Create files ---
                async Task CreateFile(string fileName, string extension, string contentData)
                {
                    var secdbfile = Path.Combine(generatedDBPath, (fileName + $".{extension}"));
                    await File.WriteAllTextAsync(secdbfile, contentData);
  
                }
                await CreateFile("Template", "DsgnRowTmplate", creationValues.ToString());
                BundleProjectScripts(generatedDBPath);
                if (_bannerBytes != null)
                {
                    File.WriteAllBytes(Path.Combine(generatedDBPath, "Banner." + GetImageFormat(_bannerBytes)), _bannerBytes);
                }
                if (_pfpBytes != null)
                {
                    File.WriteAllBytes(Path.Combine(generatedDBPath, "PFP." + GetImageFormat(_pfpBytes)), _pfpBytes);
                }
                // Show a short countdown, then reset the form and go back to the start page.
                int countdown = 6;
                BuildRowTemplate.Content = $"Built! Returning to the start page in {countdown} seconds!";
                BuildRowTemplate.IsEnabled = false;
                CancelRow.Visibility = Visibility.Collapsed;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += (s, e) =>
                {
                    countdown--;
                    if (countdown > 0)
                    {
                        BuildRowTemplate.Content = $"Built! Returning to the start page in {countdown} seconds!";
                    }
                    else
                    {
                        timer.Stop();
                        BuildRowTemplate.Content = "Finalize";
                        ExportRow.Visibility = Visibility.Collapsed;
                        Home.Visibility = Visibility.Visible;
                        CancelRow.Visibility = Visibility.Visible;
                        foreach (var box in new[] { T1, T2, U1, U2, U3, U4, U5 }) box.Text = "";
                        TablesList.SelectedItems.Clear();
                        SelectedItems.Clear();
                        TabControl1.SelectedIndex = 0;
                        SetErrors();
                    }
                };
                timer.Start();
            };
            JObject BuildJson()
            {
                var TemplateData = new JObject();
                TemplateData["Name"] = PackName;
                TemplateData["Overview"] = Overview;
                TemplateData["AuthorName"] = AuthorName;
                TemplateData["Company"] = Company;
                TemplateData["Website"] = Website;
                TemplateData["License"] = License;
                TemplateData["Note"] = Note;

                var data = new JArray();

                foreach (var item in SelectedItems)
                {
                    // Find the table in the main session
                    TableObject? tempTableObject = mainPage.MainSessionInfo.Tables
                        .Cast<TableObject?>()
                        .FirstOrDefault(t => ProjectValidator.Qualified(t.Value) == item.TableName);

                    if (tempTableObject == null) continue;

                    var tableObject = (TableObject)tempTableObject;

                    var tableJson = new JObject();
                    // Bare table name only. SchemaName is written separately and
                    // the loaders re-combine them; prefixing the schema here made
                    // imports resolve to "schema.schema.table".
                    tableJson["TableName"] = tableObject.TableName;
                    tableJson["Description"] = item.Description;
                    tableJson["SchemaName"] = tableObject.SchemaName;

                    // Add rows
                    var rowsArray = new JArray();
                    foreach (var row in tableObject.Rows)
                    {
                        var rowJson = new JObject();
                        rowJson["Name"] = row.Name;
                        rowJson["Description"] = row.Description;
                        rowJson["RowType"] = row.RowType?.ToString();
                        rowJson["Limit"] = row.Limit;
                        rowJson["IsArray"] = row.IsArray;
                        rowJson["ArrayLimit"] = row.ArrayLimit;
                        rowJson["EncryptedAndNOTMedia"] = row.EncryptedAndNOTMedia;
                        rowJson["Media"] = row.Media;
                        rowJson["IsPrimary"] = row.IsPrimary;
                        rowJson["IsUnique"] = row.IsUnique;
                        rowJson["IsNotNull"] = row.IsNotNull;
                        rowJson["DefaultValue"] = row.DefaultValue;
                        rowJson["Check"] = row.Check;
                        rowJson["DefaultIsPostgresFunction"] = row.DefaultIsPostgresFunction;
                        rowsArray.Add(rowJson);
                    }
                    tableJson["Rows"] = rowsArray;

                    // Add references
                    if (tableObject.References != null && tableObject.References.Any())
                    {
                        var refsArray = new JArray();
                        foreach (var reference in tableObject.References)
                        {
                            var refJson = new JObject();
                            refJson["MainTable"] = reference.MainTable;
                            refJson["RefTable"] = reference.RefTable;
                            refJson["ForeignKey"] = reference.ForeignKey;
                            refJson["RefTableKey"] = reference.RefTableKey;
                            refJson["OnDeleteAction"] = reference.OnDeleteAction.ToString();
                            refJson["OnUpdateAction"] = reference.OnUpdateAction.ToString();
                            refsArray.Add(refJson);
                        }
                        tableJson["References"] = refsArray;
                    }
                    else
                    {
                        tableJson["References"] = new JArray();
                    }

                    // Add indexes
                    if (tableObject.Indexes != null && tableObject.Indexes.Any())
                    {
                        var indexesArray = new JArray();
                        foreach (var index in tableObject.Indexes)
                        {
                            var indexJson = new JObject();
                            indexJson["TableName"] = index.TableName;
                            indexJson["IndexName"] = index.IndexName;

                            var columnsArray = new JArray();
                            if (index.ColumnNames != null)
                            {
                                foreach (var colName in index.ColumnNames)
                                {
                                    columnsArray.Add(colName);
                                }
                            }
                            indexJson["ColumnNames"] = columnsArray;

                            indexJson["IndexType"] = index.IndexType;
                            indexJson["Condition"] = index.Condition;
                            indexJson["Expression"] = index.Expression;
                            indexJson["IndexTypeCustom"] = index.IndexTypeCustom;
                            indexJson["UseJsonbPathOps"] = index.UseJsonbPathOps;

                            indexesArray.Add(indexJson);
                        }
                        tableJson["Indexes"] = indexesArray;
                    }
                    else
                    {
                        tableJson["Indexes"] = new JArray();
                    }

                    // Add CustomRows (usually empty)
                    if (tableObject.CustomRows != null && tableObject.CustomRows.Any())
                    {
                        var customRowsArray = new JArray();
                        foreach (var customRow in tableObject.CustomRows)
                        {
                            customRowsArray.Add(customRow);
                        }
                        tableJson["CustomRows"] = customRowsArray;
                    }
                    else
                    {
                        tableJson["CustomRows"] = new JArray();
                    }

                    data.Add(tableJson);
                }

                TemplateData["Data"] = data;
                return TemplateData;
            }


            //Project Template
            // - Author (Folder)
            // - - Author Image
            // - - Author Info JSON (Name, Company, Website, License, Note)
            // - - Session Data (Encrypted with "PUBLIC"
            // - - Banner Image
            // = = Info (text file)
            // = = Overview (text file)
            byte[] _bannerBytes2 = default;
            byte[] _pfpBytes2 = default;
            BannerPreviewButton2.Click += (s, e) =>
            {
                ImageHelper.SelectAndPreviewImage(BannerPreview3, bytes =>
                {
                    _bannerBytes2 = bytes;
                    FinalBanner2.Source = BannerPreview3.Source;
                });
            };
            PFPPreviewButton3.Click += (s, e) =>
            {
                ImageHelper.SelectAndPreviewImage(PFPPreview2, bytes =>
                {
                    _pfpBytes2 = bytes;
                    PFPImage2.Source = PFPPreview2.Source;
                });
            };
            CancelRow2.Click += (s, e) =>
            {
                ExportTemplateUI.Visibility = Visibility.Collapsed;
                //Create reset value system
                BuildProject.Visibility = Visibility.Visible;
            };
            BackBtn3.Click += (s, e) =>
            {
                ExportRow.Visibility = Visibility.Collapsed;
                Home.Visibility = Visibility.Visible;
            };
            BackBtn4.Click += (s, e) =>
            {
                ExportTemplateUI.Visibility = Visibility.Collapsed;
                Home.Visibility = Visibility.Visible;
            };
            ExportTemplate.Click += (s, e) =>
            {
                ExportTemplateUI.Visibility = Visibility.Visible;
                BuildProject.Visibility = Visibility.Collapsed;
            };
            var mainPath = mainPage.SeshDirectory.ConvertToString();
            var ProjectsFolder = Path.Combine(mainPath, mainPage.SeshUsername.ConvertToString(), "Projects");
            var Preview = new ProjectsPreviews(mainPage.MainSessionInfo.SessionName, mainPage.MainSessionInfo.SessionDescription, DateTime.UtcNow.ToString(), null);
            var folders = Directory.GetDirectories(ProjectsFolder, "*", SearchOption.TopDirectoryOnly)
                                   .Select(path => Path.GetFileName(path))
                                   .ToArray();
            foreach (var item in folders)
            {
                var txt = new TextBlock
                {
                    Text = item,
                    Margin = new Thickness(2),
                    Cursor = Cursors.Hand,
                    FontSize = 14,
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf")
                };
                ProjectsListUI.Items.Add(txt);
            }
            string PackName2 = default;
            S1.TextChanged += (s, e) =>
            {
                PackName2 = S1.Text;
                Q1.Text = $"Pack Name: {(string.IsNullOrWhiteSpace(PackName2) ? "No Pack Name Provided (Required)" : PackName2)}";
            };
            string Overview2 = default;
            S2.TextChanged += (s, e) =>
            {
                Overview2 = S2.Text;
            };
            string AuthorName2 = default;
            R1.TextChanged += (s, e) =>
            {
                AuthorName2 = R1.Text;
                AuthorNameTxt2.Text = AuthorName2;
            };
            string Company2 = default;
            R2.TextChanged += (s, e) =>
            {
                Company2 = R2.Text;
            };
            string Website2 = default;
            R3.TextChanged += (s, e) =>
            {
                Website2 = R3.Text;
            };
            string License2 = default;
            R4.TextChanged += (s, e) =>
            {
                License2 = R4.Text;
            };
            string Note2 = default;
            R5.TextChanged += (s, e) =>
            {
                Note2 = R5.Text;
            };
            TabControl2.SelectionChanged += (s, e) =>
            {
                SetTextProject2();
                SetErrorsProject2();
            };
            void SetTextProject2()
            {
                static string Or(string value, string missing) => string.IsNullOrWhiteSpace(value) ? missing : value;
                string TemplateInfo =
                    $"Overview: {Or(Overview2, "No Overview Provided (Required)")}\n" +
                    $"Author Name: {Or(AuthorName2, "No Author Provided (Required)")}\n" +
                    $"Company: {Or(Company2, "No Company Provided")}\n" +
                    $"Website: {Or(Website2, "No Website Provided")}\n" +
                    $"License: {Or(License2, "No License Provided")}\n" +
                    $"Note: {Or(Note2, "No Note Provided")}";
                Q2.Text = TemplateInfo;
            }
            void SetErrorsProject2()
            {
                ValidationCheck2.Errors.Clear();
                FinalizeBuildProjectBtn.IsEnabled = false;
                if (string.IsNullOrWhiteSpace(PackName2))
                {
                    ValidationCheck2.Errors.Add(new ValidationSummaryItem
                    {
                        Message = "A Template Name Must Be Provided"
                    });
                }
                else if (PackName2.Trim().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || PackName2.Trim().EndsWith("."))
                {
                    ValidationCheck2.Errors.Add(new ValidationSummaryItem
                    {
                        Message = "The template name can't contain \\ / : * ? \" < > | or end with a dot"
                    });
                }
                if (string.IsNullOrWhiteSpace(Overview2))
                {
                    ValidationCheck2.Errors.Add(new ValidationSummaryItem
                    {
                        Message = "An Overview Must Be Provided"
                    });
                }
                if (string.IsNullOrWhiteSpace(AuthorName2))
                {
                    ValidationCheck2.Errors.Add(new ValidationSummaryItem
                    {
                        Message = "An Author Must Be Provided"
                    });
                }
                if (ProjectsListUI.SelectedItem == null)
                {
                    ValidationCheck2.Errors.Add(new ValidationSummaryItem
                    {
                        Message = "Pick the project to turn into a template"
                    });
                }
                FinalizeBuildProjectBtn.IsEnabled = ValidationCheck2.Errors.Count == 0;
            }
            foreach (var box in new[] { S1, S2, R1, R2, R3, R4, R5 })
                box.TextChanged += (s, e) => { SetTextProject2(); SetErrorsProject2(); };
            ProjectsListUI.SelectionChanged += (s, e) => SetErrorsProject2();
            SetTextProject2();
            SetErrorsProject2();
            FinalizeBuildProjectBtn.Click += async (s, e) =>
            {
                // Project templates are user-global too: DatabaseTemplates reads
                // from <user>/Project Templates, so write there instead of inside
                // the current project folder.
                var CurrentDirectory = Path.Combine(mainPaged.SeshDirectory.ConvertToString(), mainPaged.SeshUsername.ConvertToString(), "Project Templates");
                // --- Prepare paths ---
                string basePath = string.IsNullOrEmpty(CurrentDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : CurrentDirectory;
                string generatedDBPath = Path.Combine(basePath, PackName2.Trim());
                string incremental = "";
                // --- Ensure GeneratedDB exists ---
                Directory.CreateDirectory(generatedDBPath);
                // --- Handle incremental versioning ---
                var folders = Directory.GetDirectories(generatedDBPath, "*", SearchOption.TopDirectoryOnly);
                var versionNumbers = folders
                    .Select(f => Path.GetFileName(f))
                    .Where(name => !string.IsNullOrEmpty(name) && name.StartsWith("v"))
                    .Select(name => int.TryParse(name.Substring(1), out var n) ? (int?)n : null)
                    .Where(n => n.HasValue)
                    .Select(n => n.Value)
                    .ToList();
                incremental = versionNumbers.Count == 0 ? "v1" : "v" + (versionNumbers.Max() + 1);
                generatedDBPath = Path.Combine(generatedDBPath, incremental);
                // Create incremental folder
                Directory.CreateDirectory(generatedDBPath);
// --- Create files ---
                async Task CreateFile(string fileName, string extension, string contentData)
                {
                    var secdbfile = Path.Combine(generatedDBPath, (fileName + $".{extension}"));
                    await File.WriteAllTextAsync(secdbfile, contentData);
   
                }
                var projectTemplate = BuildJsonProject();
                await CreateFile("Template", "DsgnRowTmplate", projectTemplate.ToString());
                BundleProjectScripts(generatedDBPath);
                if (_bannerBytes2 != null)
                {
                    File.WriteAllBytes(Path.Combine(generatedDBPath, "Banner." + GetImageFormat(_bannerBytes2)), _bannerBytes2);
                }
                if (_pfpBytes2 != null)
                {
                    File.WriteAllBytes(Path.Combine(generatedDBPath, "PFP." + GetImageFormat(_pfpBytes2)), _pfpBytes2);
                }
                // --- Success UI ---
                int countdown = 6;
                FinalizeBuildProjectBtn.Content = $"Built! Returning in {countdown} seconds!";
                FinalizeBuildProjectBtn.IsEnabled = false;
                CancelRow2.Visibility = Visibility.Collapsed;

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += (sender, args) =>
                {
                    countdown--;
                    if (countdown > 0)
                    {
                        FinalizeBuildProjectBtn.Content = $"Built! Returning in {countdown} seconds!";
                    }
                    else
                    {
                        timer.Stop();
                        FinalizeBuildProjectBtn.IsEnabled = true;
                        FinalizeBuildProjectBtn.Content = "Build Project Template";
                        ExportTemplateUI.Visibility = Visibility.Collapsed;
                        Home.Visibility = Visibility.Visible;
                        CancelRow2.Visibility = Visibility.Visible;
                    }
                };
                timer.Start();
            };
            JObject BuildJsonProject()
            {
                var TemplateData = new JObject();
                TemplateData["Name"] = PackName2;
                TemplateData["Overview"] = Overview2;
                TemplateData["AuthorName"] = AuthorName2;
                TemplateData["Company"] = Company2;
                TemplateData["Website"] = Website2;
                TemplateData["License"] = License2;
                TemplateData["Note"] = Note2;

                // Serialize project tables as plain JSON (no encryption)
                var tablesArray = new JArray();
                foreach (var table in mainPage.MainSessionInfo.Tables)
                {
                    var tableObj = new JObject();
                    tableObj["TableName"] = table.TableName;
                    tableObj["Description"] = table.Description;
                    tableObj["SchemaName"] = table.SchemaName;

                    var rowsArray = new JArray();
                    foreach (var row in table.Rows)
                    {
                        var rowObj = new JObject();
                        rowObj["Name"] = row.Name;
                        rowObj["Description"] = row.Description;
                        rowObj["RowType"] = row.RowType?.ToString();
                        rowObj["Limit"] = row.Limit;
                        rowObj["IsArray"] = row.IsArray;
                        rowObj["ArrayLimit"] = row.ArrayLimit;
                        rowObj["EncryptedAndNOTMedia"] = row.EncryptedAndNOTMedia;
                        rowObj["Media"] = row.Media;
                        rowObj["IsPrimary"] = row.IsPrimary;
                        rowObj["IsUnique"] = row.IsUnique;
                        rowObj["IsNotNull"] = row.IsNotNull;
                        rowObj["DefaultValue"] = row.DefaultValue;
                        rowObj["Check"] = row.Check;
                        rowObj["DefaultIsPostgresFunction"] = row.DefaultIsPostgresFunction;
                        rowsArray.Add(rowObj);
                    }
                    tableObj["Rows"] = rowsArray;

                    // References
                    if (table.References != null && table.References.Any())
                    {
                        var refsArray = new JArray();
                        foreach (var reference in table.References)
                        {
                            var refObj = new JObject();
                            refObj["MainTable"] = reference.MainTable;
                            refObj["RefTable"] = reference.RefTable;
                            refObj["ForeignKey"] = reference.ForeignKey;
                            refObj["RefTableKey"] = reference.RefTableKey;
                            refObj["OnDeleteAction"] = reference.OnDeleteAction.ToString();
                            refObj["OnUpdateAction"] = reference.OnUpdateAction.ToString();
                            refsArray.Add(refObj);
                        }
                        tableObj["References"] = refsArray;
                    }

                    // Indexes
                    if (table.Indexes != null && table.Indexes.Any())
                    {
                        var indexesArray = new JArray();
                        foreach (var index in table.Indexes)
                        {
                            var idxObj = new JObject();
                            idxObj["IndexName"] = index.IndexName;
                            idxObj["ColumnNames"] = new JArray(index.ColumnNames ?? new List<string>());
                            idxObj["IndexType"] = index.IndexType;
                            idxObj["Condition"] = index.Condition;
                            idxObj["Expression"] = index.Expression;
                            idxObj["IndexTypeCustom"] = index.IndexTypeCustom;
                            idxObj["UseJsonbPathOps"] = index.UseJsonbPathOps;
                            indexesArray.Add(idxObj);
                        }
                        tableObj["Indexes"] = indexesArray;
                    }

                    tablesArray.Add(tableObj);
                }
                TemplateData["Data"] = tablesArray;

                return TemplateData;
            }
            this.Unloaded += (s, e) =>
            {
                RemoveWindow();
            };
            ExitButton.Click += (s, e) => { try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };
        }

        public class Descriptions
        {
            public string TableName { get; set; }
            public string Description { get; set; }

            public Descriptions(string name, string description)
            {
                TableName = name;
                Description = description;
            }
        }

        private static List<DatabaseDesign> TopoSortDesignsByReferences(
    List<DatabaseDesign> designs,
    List<Reference.ReferenceOptions> references)
        {
            // Build a lookup: tableName -> index in designs list
            var indexMap = designs
                .Select((d, i) => (d, i))
                .ToDictionary(x => x.d.TableName, x => x.i);

            // Build adjacency: RefTable must come BEFORE MainTable
            // So edge direction: RefTable -> MainTable (RefTable is a prerequisite)
            var inDegree = new int[designs.Count];
            var adj = new List<List<int>>(designs.Count);
            for (int i = 0; i < designs.Count; i++) adj.Add(new List<int>());

            foreach (var r in references)
            {
                if (!indexMap.TryGetValue(r.RefTable, out int refIdx)) continue;
                if (!indexMap.TryGetValue(r.MainTable, out int mainIdx)) continue;
                if (refIdx == mainIdx) continue;

                adj[refIdx].Add(mainIdx);
                inDegree[mainIdx]++;
            }

            // Kahn's algorithm
            var queue = new Queue<int>();
            for (int i = 0; i < designs.Count; i++)
                if (inDegree[i] == 0) queue.Enqueue(i);

            var sorted = new List<DatabaseDesign>(designs.Count);
            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                sorted.Add(designs[node]);
                foreach (int neighbor in adj[node])
                {
                    if (--inDegree[neighbor] == 0)
                        queue.Enqueue(neighbor);
                }
            }

            // If there's a cycle, append any remaining nodes as-is
            if (sorted.Count < designs.Count)
            {
                var emitted = new HashSet<string>(sorted.Select(d => d.TableName));
                sorted.AddRange(designs.Where(d => !emitted.Contains(d.TableName)));
            }

            return sorted;
        }
        public void RemoveWindow()
        {
            try
            {
                //if (WindowInfo == null) return;

                if (mainPage?.LowerAppBar != null && WindowInfo.Shortcut != null)
                {
                    try
                    {
                        if (mainPage.LowerAppBar.Children.Contains(WindowInfo.Shortcut))
                            mainPage.LowerAppBar.Children.Remove(WindowInfo.Shortcut);
                        if (WindowInfo.Shortcut is FrameworkElement sh) sh.Visibility = Visibility.Collapsed;
                    }
                    catch { }
                }

                if (WindowInfo.Elements != null && mainPage?.IntroPage != null)
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
                            if (mainPage.IntroPage.Children.Contains(item))
                                mainPage.IntroPage.Children.Remove(item);
                        }
                        catch { }
                    }
                    try { WindowInfo.Elements.Clear(); } catch { }
                }

                try
                {
                    if (this is FrameworkElement me) me.Visibility = Visibility.Collapsed;
                    if (mainPage?.IntroPage != null && mainPage.IntroPage.Children.Contains(this))
                        mainPage.IntroPage.Children.Remove(this);
                }
                catch { }
            }
            catch { }

        }
        private void BundleProjectScripts(string destFolder)
        {
            try
            {
                var scriptsSource = Path.Combine(
                    mainPage.SeshDirectory.ConvertToString(),
                    mainPage.SeshUsername.ConvertToString(),
                    "Projects", mainPage.ProjectName, "Scripts");

                if (!Directory.Exists(scriptsSource)) return;

                var scriptsDest = Path.Combine(destFolder, "Scripts");
                CopyDirectory(scriptsSource, scriptsDest);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BundleProjectScripts] Failed: {ex.Message}");
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }

        private static List<ApiGenerator.ApiFunction> CollectApiFunctions(string apiJson)
        {
            var list = new List<ApiGenerator.ApiFunction>();
            if (string.IsNullOrEmpty(apiJson)) return list;
            try
            {
                var apiData = System.Text.Json.JsonSerializer.Deserialize<APIData>(apiJson);
                foreach (var module in apiData?.Modules ?? new())
                    foreach (var endpoint in module.Endpoints ?? new())
                        foreach (var function in endpoint.Functions ?? new())
                        {
                            if (string.IsNullOrWhiteSpace(module.Name) || string.IsNullOrWhiteSpace(endpoint.Name)) continue;
                            list.Add(new ApiGenerator.ApiFunction(module.Name, endpoint.Name, function.Name, function.Verb, function.Description));
                        }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Build] Could not read API definitions: {ex.Message}");
            }
            return list;
        }

        string GetImageFormat(byte[] bytes)
        {
            if (bytes.Length < 8)
                return "Unknown";
            // PNG
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return "PNG";
            // JPEG
            if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return "JPEG";
            // GIF
            if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
                return "GIF";
            // BMP
            if (bytes[0] == 0x42 && bytes[1] == 0x4D)
                return "BMP";
            return "Unknown";
        }
        private void ShowBuildSuccess(Button Build)
        {
            Build.Content = "Built!";
            Build.IsEnabled = false;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) =>
            {
                Build.IsEnabled = true;
                timer.Stop();
                Build.Content = "Build";
            };
            timer.Start();
        }
        public static RowOptions ConvertRowCreationToOptions(RowCreation row)
        {
            int? arrayLimit = null;
            if (!string.IsNullOrEmpty(row.ArrayLimit))
            {
                if (int.TryParse(row.ArrayLimit, out int parsed))
                    arrayLimit = parsed;
            }
            return new RowOptions(
                fieldName: row.Name,
                description: row.Description,
                postgresType: row.RowType,
                customType: "", // assuming RowCreation has no custom type, not until v2 maybe
                elementLimit: row.Limit,
                isArray: row.IsArray ?? false,
                arrayLimit: arrayLimit,
                isEncrypted: row.EncryptedAndNOTMedia ?? false,
                isMedia: row.Media ?? false,
                isPrimary: row.IsPrimary ?? false,
                isUnique: row.IsUnique ?? false,
                isNotNull: row.IsNotNull ?? false,
                defaultValue: row.DefaultValue,
                check: row.Check,
                defaultIsKeyword: row.DefaultIsPostgresFunction
            );
        }
        public static IndexDefinition ConvertIndexCreation(IndexCreation index)
        {
            // Convert string to IndexType enum; fallback to default if parsing fails
            IndexType indexType = IndexType.Basic; // default
            if (!string.IsNullOrEmpty(index.IndexType) && Enum.TryParse(index.IndexType, true, out IndexType parsedType))
            {
                indexType = parsedType;
            }
            return new IndexDefinition(
                tableName: index.TableName,
                indexName: index.IndexName,
                columnNames: index.ColumnNames?.ToArray() ?? Array.Empty<string>(),
                indexType: indexType,
                condition: index.Condition ?? "",
                expression: index.Expression ?? "",
                indexTypeCustom: index.IndexTypeCustom ?? "",
                useJsonbPathOps: index.UseJsonbPathOps ?? false
            );
        }
        // Executes when the user navigates to this page.
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }

        private void GenerateSpacetimeDBFiles(string spacetimePath, string rlsJson, string apiJson)
        {
            // Not written yet. The folder is still created so the build layout stays the same.
        }

        private static string GenerateRLSSQL(string rlsJson, IEnumerable<TableObject> tables)
        {
            RLSData data = null;
            try
            {
                if (!string.IsNullOrEmpty(rlsJson))
                    data = System.Text.Json.JsonSerializer.Deserialize<RLSData>(rlsJson);
            }
            catch (Exception ex)
            {
                return $"-- Could not read the RLS Editor data: {ex.Message}\n";
            }
            return RlsSqlGenerator.Generate(data, ToRlsTables(tables));
        }

        internal static List<RlsSqlGenerator.Table> ToRlsTables(IEnumerable<TableObject> tables) =>
            RlsSqlGenerator.FromProject(tables);

        private string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unknown";
            var result = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    result.Append(char.IsLower(c) ? c : char.ToLower(c));
                else if (c == ' ')
                    result.Append('_');
            }
            var str = result.ToString().Trim('_');
            if (str.Length > 0 && char.IsDigit(str[0]))
                str = "_" + str;
            return string.IsNullOrEmpty(str) ? "Unknown" : str;
        }
                                private string GetCSharpType(string dataType)
        {
            if (string.IsNullOrEmpty(dataType)) return "string";
            var dt = dataType.ToUpper();
            if (dt.Contains("INT") || dt == "SERIAL" || dt == "BIGSERIAL") return "int";
            if (dt.Contains("BIGINT")) return "long";
            if (dt.Contains("FLOAT") || dt.Contains("DOUBLE") || dt.Contains("REAL")) return "double";
            if (dt.Contains("DECIMAL") || dt.Contains("NUMERIC")) return "decimal";
            if (dt.Contains("BOOL")) return "bool";
            if (dt.Contains("DATE") || dt.Contains("TIME")) return "DateTime";
            if (dt.Contains("UUID")) return "Guid";
            if (dt.Contains("JSON")) return "string";
            return "string";
        }
    }
}

