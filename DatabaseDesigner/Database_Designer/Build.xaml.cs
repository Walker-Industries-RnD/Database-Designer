 
using DatabaseDesigner;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            baseUrl = mainPaged.baseUrl;
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
                var createUrl = $"{baseUrl}Utilities/OpenDirectory?directory={Uri.EscapeDataString(CurrentDirectory)}";
                var createdResponse = mainPage.DBDesignerClient.PostAsync(createUrl, new StringContent("")).GetAwaiter().GetResult();
                createdResponse.EnsureSuccessStatusCode();
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
                List<(ReferenceOptions, string)> BrokenRefs = new List<(ReferenceOptions, string)>();
                foreach (var table in mainPage.MainSessionInfo.Tables)
                {
                    if (table.References == null)
                    {
                        Console.WriteLine($"Skipping table {table.SchemaName}.{table.TableName} because References is null");
                        continue;
                    }
                    Console.WriteLine($"Checking table {table.SchemaName}.{table.TableName} with {table.References.Count} references");
                    for (int i = 0; i < table.References.Count; i++)
                    {
                        var reference = table.References[i];
                        Console.WriteLine($" Checking reference {i}: RefTable={reference.RefTable}, RefTableKey={reference.RefTableKey}, MainTable={reference.MainTable}");
                        TableObject targetTable = default;
                        try
                        {
                            // Find the table that this reference points to
                            targetTable = mainPage.MainSessionInfo.Tables
                                .First(t => $"{t.SchemaName}.{t.TableName}" == reference.RefTable);
                            Console.WriteLine($" Found target table: {targetTable.SchemaName}.{targetTable.TableName} with {targetTable.Rows.Count} rows");
                        }
                        catch
                        {
                            Console.WriteLine($" Could not find target table: {reference.MainTable}");
                            BrokenRefs.Add((reference, table.SchemaName + "." + table.TableName));
                            continue;
                        }
                        // Validate that the referenced key exists in the target table
                        bool keyExists = targetTable.Rows.Any(r => r.Name == reference.RefTableKey);
                        Console.WriteLine($" KeyExists={keyExists} for RefTableKey={reference.RefTableKey}");
                        if (!keyExists)
                        {
                            BrokenRefs.Add((reference, table.SchemaName + "." + table.TableName));
                        }
                    }
                }
                foreach (var item in BrokenRefs)
                {
                    // Create a button to put inside the validation item
                    var errorButton = new Button
                    {
                        Content = $"Broken Reference In {item.Item2}: Ref {item.Item1.RefTable}.{item.Item1.RefTableKey}",
                        Margin = new Thickness(2),
                        Background = new SolidColorBrush(Colors.Transparent),
                        BorderThickness = new Thickness(0),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
                        FontSize = 16,
                        Foreground = new SolidColorBrush(Colors.White),
                        Cursor = Cursors.Hand
                    };
                    errorButton.Click += (s, e) =>
                    {
                        mainPage.CreateWindow(() => new DatabaseViewer(mainPage, $"{item.Item1.RefTable}.{item.Item1.RefTableKey}"), "Database Viewer", true);
                    };
                    // Wrap it in a ValidationSummaryItem
                    var validationItem = new ValidationSummaryItem
                    {
                        Context = errorButton,
                        Message = $"Broken Reference In {item.Item2}: Ref {item.Item1.RefTable}.{item.Item1.RefTableKey}"
                    };
                    ProjectErrorCheck.Errors.Add(validationItem);
                }

                if (BrokenRefs.Count == 0)
                {
                    var validationItem = new ValidationSummaryItem
                    {
                        Message = $"Congrats, the project is good to go! Export as you please."
                    };
                    ProjectErrorCheck.Errors.Add(validationItem);
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
                                {
                                    MainTable = reference.MainTable,
                                    RefTable = reference.RefTable,
                                    ForeignKey = reference.ForeignKey,
                                    RefTableKey = reference.RefTableKey,
                                    OnDeleteAction = reference.OnDeleteAction,
                                    OnUpdateAction = reference.OnUpdateAction
                                };
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
                        string? schemaName = GetSchemaFromTable(item.TableName);
                        string tableOnly = GetTableNameWithoutSchema(item.TableName);

                        if (!string.IsNullOrEmpty(schemaName))
                            dbContext.AppendLine($"    [Table(\"{tableOnly}\", Schema = \"{schemaName}\")]");
                        else
                            dbContext.AppendLine($"    [Table(\"{tableOnly}\")]");

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
                        generatedDBPath = Path.Combine(generatedDBPath, incremental);
                    

                    Directory.CreateDirectory(generatedDBPath);
                    File.WriteAllText(Path.Combine(generatedDBPath, "SQL.sql"), sql.ToString());
                    File.WriteAllText(Path.Combine(generatedDBPath, "Documentation.md"), doc.ToString());
                    File.WriteAllText(Path.Combine(generatedDBPath, "Classes.cs"), classes.ToString());
                    File.WriteAllText(Path.Combine(generatedDBPath, "Models.cs"), dbContext.ToString());



                    // --- Success UI ---
                    ShowBuildSuccess(BuildProjectBtn);
                }

                catch (Exception ex)
                {
                    Console.WriteLine($"Build failed: {ex}");
  
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
                    Text = item.SchemaName + "." + item.TableName,
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
                V1.Text = $"Pack Name: {PackName ?? "No Pack Name Provided (Required)"}"
;
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
                string TemplateInfo =
                    $"Overview: {Overview ?? "No Overview Provided (Required)"} \n" +
                    $"Author Name: {AuthorName ?? "No Author Provided (Required)"}\n" +
                    $"Company: {Company ?? "No Company Provided"}\n" +
                    $"Website: {Website ?? "No Website Provided"}\n" +
                    $"License: {License ?? "No License Provided"}\n" +
                    $"Note: {Note ?? "No Note Provided"}";
                V2.Text = TemplateInfo;
            }
            void SetErrors()
            {
                ValidationCheck1.Errors.Clear();
                BuildRowTemplate.IsEnabled = false;
                if (string.IsNullOrEmpty(PackName))
                {
                    ValidationCheck1.Errors.Add(new ValidationSummaryItem
                    {
                        Message = "A Template Name Must Be Provided"
                    });
                }
                if (string.IsNullOrEmpty(Overview))
                {
                    ValidationCheck1.Errors.Add(new ValidationSummaryItem
                    {
                        Message = "An Overview Must Be Provided"
                    });
                }
                if (string.IsNullOrEmpty(AuthorName))
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
                if (PackName != null && Overview != null && AuthorName != null && TablesList.SelectedItems.Count >= 1) { BuildRowTemplate.IsEnabled = true; }
            }
            BuildRowTemplate.Click += async (s, e) =>
            {
                var creationValues = BuildJson();
                var CurrentDirectory = Path.Combine(mainPaged.SeshDirectory.ConvertToString(), mainPaged.SeshUsername.ConvertToString(), "Projects", mainPaged.ProjectName, "Row Templates");
                // --- Prepare paths ---
                string basePath = string.IsNullOrEmpty(CurrentDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : CurrentDirectory;
                string generatedDBPath = Path.Combine(basePath, PackName);
                string incremental = "";
                // --- Ensure GeneratedDB exists ---
                var createDirUrl = $"{baseUrl}Folder/CreateDirectory?directory={Uri.EscapeDataString(generatedDBPath)}";
                var createDirResponse = await mainPage.DBDesignerClient.PostAsync(createDirUrl, null);
                createDirResponse.EnsureSuccessStatusCode();
                // --- Handle incremental versioning ---
                var listUrlF = $"{baseUrl}File/ListFolders?directory={Uri.EscapeDataString(generatedDBPath)}";
                var listResponseF = await mainPage.DBDesignerClient.GetAsync(listUrlF);
                listResponseF.EnsureSuccessStatusCode();
                var folderJson = await listResponseF.Content.ReadAsStringAsync();
                var folders = JsonSerializer.Deserialize<string[]>(folderJson) ?? Array.Empty<string>();
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
                var createIncUrl = $"{baseUrl}Folder/CreateDirectory?directory={Uri.EscapeDataString(generatedDBPath)}";
                var createIncResponse = await mainPage.DBDesignerClient.PostAsync(createIncUrl, null);
                createIncResponse.EnsureSuccessStatusCode();
                // --- Create files ---
                async Task CreateFile(string fileName, string extension, string contentData)
                {
                    var url = $"{baseUrl}File/Create";
                    var payloadFile = new { Directory = generatedDBPath, FileName = fileName, Extension = extension, Content = contentData };
                    var json = JsonSerializer.Serialize(payloadFile);
                    var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await mainPage.DBDesignerClient.PostAsync(url, httpContent);
                    response.EnsureSuccessStatusCode();
                }
                await CreateFile("Template", "DsgnRowTmplate", creationValues.ToString());
                if (_bannerBytes != null)
                {
                    File.WriteAllBytes(Path.Combine(generatedDBPath, "Banner." + GetImageFormat(_bannerBytes)), _bannerBytes);
                }
                if (_pfpBytes != null)
                {
                    File.WriteAllBytes(Path.Combine(generatedDBPath, "PFP." + GetImageFormat(_pfpBytes)), _pfpBytes);
                }
                // --- Success UI ---
                int countdown = 6;
                BuildRowTemplate.Content = $"Built! Returning to homepage in {countdown} seconds!";
                BuildRowTemplate.IsEnabled = false;
                CancelRow.Visibility = Visibility.Collapsed;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += (s, e) =>
                {
                    countdown--;
                    if (countdown > 0)
                    {
                        BuildRowTemplate.Content = $"Built! Returning to page in {countdown} seconds!";
                    }
                    else
                    {
                        timer.Stop();
                        BuildRowTemplate.IsEnabled = true;
                        BuildRowTemplate.Content = "Build";
                        ExportRow.Visibility = Visibility.Collapsed;
                        Home.Visibility = Visibility.Visible;
                        CancelRow.Visibility = Visibility.Visible;
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
                        .FirstOrDefault(t => $"{t.SchemaName}.{t.TableName}" == item.TableName);

                    if (tempTableObject == null) continue;

                    var tableObject = (TableObject)tempTableObject;

                    var tableJson = new JObject();
                    tableJson["TableName"] = $"{tableObject.SchemaName}.{tableObject.TableName}";
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
            CancelRow.Click += (s, e) =>
            {
                ExportRow.Visibility = Visibility.Collapsed;
                //Create reset value system
                BuildProject.Visibility = Visibility.Visible;
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
                Q1.Text = $"Pack Name: {PackName2 ?? "No Pack Name Provided (Required)"}"
;
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
                AuthorNameTxt.Text = AuthorName2;
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
                string TemplateInfo =
                    $"Overview: {Overview2 ?? "No Overview Provided (Required)"} \n" +
                    $"Author Name: {AuthorName2 ?? "No Author Provided (Required)"}\n" +
                    $"Company: {Company2 ?? "No Company Provided"}\n" +
                    $"Website: {Website2 ?? "No Website Provided"}\n" +
                    $"License: {License2 ?? "No License Provided"}\n" +
                    $"Note: {Note2 ?? "No Note Provided"}";
                Q2.Text = TemplateInfo;
            }
            void SetErrorsProject2()
            {
                ValidationCheck2.Errors.Clear();
                FinalizeBuildProjectBtn.IsEnabled = false;
                if (string.IsNullOrEmpty(PackName2))
                {
                    ValidationCheck2.Errors.Add(new ValidationSummaryItem
                    {
                        Message = "A Template Name Must Be Provided"
                    });
                }
                if (string.IsNullOrEmpty(Overview2))
                {
                    ValidationCheck2.Errors.Add(new ValidationSummaryItem
                    {
                        Message = "An Overview Must Be Provided"
                    });
                }
                if (string.IsNullOrEmpty(AuthorName2))
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
                        Message = "At least one table must be selected"
                    });
                }
                if (PackName2 != null && Overview2 != null && AuthorName2 != null && ProjectsListUI.SelectedItem != null) { FinalizeBuildProjectBtn.IsEnabled = true; }
            }
            string templateData = default;
            FinalizeBuildProjectBtn.Click += async (s, e) =>
            {
                var CurrentDirectory = Path.Combine(mainPaged.SeshDirectory.ConvertToString(), mainPaged.SeshUsername.ConvertToString(), "Projects", mainPaged.ProjectName, "Project Templates");
                // --- Prepare paths ---
                string basePath = string.IsNullOrEmpty(CurrentDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : CurrentDirectory;
                string generatedDBPath = Path.Combine(basePath, PackName2);
                string incremental = "";
                // --- Ensure GeneratedDB exists ---
                var createDirUrl = $"{baseUrl}Folder/CreateDirectory?directory={Uri.EscapeDataString(generatedDBPath)}";
                var createDirResponse = await mainPage.DBDesignerClient.PostAsync(createDirUrl, null);
                createDirResponse.EnsureSuccessStatusCode();
                // --- Handle incremental versioning ---
                var listUrlF = $"{baseUrl}File/ListFolders?directory={Uri.EscapeDataString(generatedDBPath)}";
                var listResponseF = await mainPage.DBDesignerClient.GetAsync(listUrlF);
                listResponseF.EnsureSuccessStatusCode();
                var folderJson = await listResponseF.Content.ReadAsStringAsync();
                var folders = JsonSerializer.Deserialize<string[]>(folderJson) ?? Array.Empty<string>();
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
                var createIncUrl = $"{baseUrl}Folder/CreateDirectory?directory={Uri.EscapeDataString(generatedDBPath)}";
                var createIncResponse = await mainPage.DBDesignerClient.PostAsync(createIncUrl, null);
                createIncResponse.EnsureSuccessStatusCode();
                // --- Create files ---
                async Task CreateFile(string fileName, string extension, string contentData)
                {
                    var url = $"{baseUrl}File/Create";
                    var payloadFile = new { Directory = generatedDBPath, FileName = fileName, Extension = extension, Content = contentData };
                    var json = JsonSerializer.Serialize(payloadFile);
                    var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await mainPage.DBDesignerClient.PostAsync(url, httpContent);
                    response.EnsureSuccessStatusCode();
                }
                var ProjectPath = Path.Combine(mainPaged.SeshDirectory.ConvertToString(), mainPaged.SeshUsername.ConvertToString(), "Projects", mainPaged.ProjectName);
                var ProjectFile = Directory.GetFiles(ProjectPath, "*.SECDBDESIGN").First();
                var ProjectBytes = File.ReadAllText(ProjectFile);
                var ProjectAESBytes = Convert.FromBase64String(ProjectBytes);
                var ProjectUTF8 = Encoding.UTF8.GetString(ProjectAESBytes);
                var ProjectAESToDecrypt = SimpleAESEncryption.AESEncryptedText.FromString(ProjectUTF8);

                var ProjectTemplate = Task.Run(() =>
                {
                    return mainPage.FromSessionString(ProjectAESToDecrypt, mainPage.Password);
                }).Result;

                var dataToEncrypt = mainPage.ToSessionString(ProjectTemplate, "GLORYTOMANKIND".ToSecureData());
                var encryptedSessionString = SimpleAESEncryption.Encrypt(dataToEncrypt, "GLORYTOMANKIND".ToSecureData()).ToString();
                templateData = encryptedSessionString;

                // Now: build the JSON (now templateData is not null!)
                var creationValues = BuildJsonProject();
                Console.WriteLine(templateData);
                await CreateFile("Template", "DsgnRowTmplate", creationValues.ToString());
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
                TemplateData["Data"] = templateData;
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
    }
}



