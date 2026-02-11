using Database_Designer;
using DatabaseDesigner;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Browser;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using WISecureData;
using static DatabaseDesigner.DBDesigner;
using static DatabaseDesigner.Index;
using static DatabaseDesigner.Row;
using static DatabaseDesigner.SessionStorage;
using static OpenSilver.Features;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Database_Designer
{
    public partial class DateTimeInfo : Page
    {
        MainPage mainPaged;

        public UIWindowEntry WindowInfo { get; private set; }


        private DispatcherTimer _timer;
        static string baseUrl;



        //Let's hope the SessionObject not being printed on the preview is just thanks to the way things are working on the Serializer side


        private void GenerateProjectDetails()
        {
            //Works similarly to the Linux preview widget, except it isn't; need to do UI for this + switch btn UI
        }


        // Named method to safely update UI without deleted lambda issues
        private void ShowBuildSuccess()
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


        public DateTimeInfo(MainPage mainPage)
        {
            this.InitializeComponent();

            baseUrl = mainPage.baseUrl;

            mainPaged = mainPage;

            mainPaged.IntroPage.Children.Remove(this);


            // Create and start a timer to update every second
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Initial display
            UpdateTime();

            Clean.Click += (s, e) =>
            {
                mainPage.IntroPage.Children.Clear();
                mainPage.LowerAppBar.Children.Clear();
            };

            Build.Click += async (s, e) =>
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

                    foreach (var row in item.Rows)
                    {
                        var finalizedRow = ConvertRowCreationToOptions(row);
                        TableRows.Add(finalizedRow);

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



                var ProjectPath = Path.Combine(CurrentDirectory);

                StringBuilder sql = new StringBuilder();
                StringBuilder doc = new StringBuilder();
                StringBuilder classes = new StringBuilder();



                //Before anything, we need to create the SecureMedia and SecureMediaSession types, also 


                sql.AppendLine("CREATE TYPE SecureMedia AS (");
                sql.AppendLine("    is_public BOOLEAN,");
                sql.AppendLine("    public_key TEXT,");
                sql.AppendLine("    path TEXT");
                sql.AppendLine(");");

                sql.AppendLine("CREATE TYPE SecureMediaSession AS (");
                sql.AppendLine("    media_id BIGINT,");
                sql.AppendLine("    user_id BIGINT,");
                sql.AppendLine("    referenced_media TEXT,");
                sql.AppendLine("    allowed_user TEXT,");
                sql.AppendLine("    public_key TEXT,");
                sql.AppendLine("    last_used TEXT");
                sql.AppendLine(");");


                //And of course the EFC classes for them

                classes.AppendLine("using System.ComponentModel.DataAnnotations;\r\n");
                classes.AppendLine("using System.ComponentModel.DataAnnotations.Schema;\r\n");
                classes.AppendLine("public class Models\r\n");
                classes.AppendLine("\n");
                classes.AppendLine("{");

                classes.AppendLine("// SecureMedia and SecureMediaSessions are custom types!");

                classes.AppendLine("public class SecureMedia");
                classes.AppendLine("{");
                classes.AppendLine("    [Key]");
                classes.AppendLine("    public long Id { get; set; }");
                classes.AppendLine("");
                classes.AppendLine("    public bool IsPublic { get; set; }");
                classes.AppendLine("");
                classes.AppendLine("    public string SecretKey { get; set; }");
                classes.AppendLine("");
                classes.AppendLine("    public string PublicKey { get; set; }");
                classes.AppendLine("");
                classes.AppendLine("    public string Path { get; set; }");
                classes.AppendLine("");
                classes.AppendLine("    public ICollection<SecureMediaSession> Sessions { get; set; }");
                classes.AppendLine("}");

                classes.AppendLine("");


                classes.AppendLine("public class SecureMediaSession");
                classes.AppendLine("{");
                classes.AppendLine("    [Key]");
                classes.AppendLine("    public long Id { get; set; }");
                classes.AppendLine("");
                classes.AppendLine("    [ForeignKey(nameof(Media))]");
                classes.AppendLine("    public long MediaId { get; set; }");
                classes.AppendLine("");
                classes.AppendLine("    public SecureMedia Media { get; set; }");
                classes.AppendLine("");
                classes.AppendLine("    [ForeignKey(\"User\")]");
                classes.AppendLine("    public long UserId { get; set; }");
                classes.AppendLine("");
                classes.AppendLine("    public string ReferencedMedia { get; set; }");
                classes.AppendLine("");
                classes.AppendLine("    public string AllowedUser { get; set; }");
                classes.AppendLine("");
                classes.AppendLine("    public string PublicKey { get; set; }");
                classes.AppendLine("");
                classes.AppendLine("    public string LastUsed { get; set; }");
                classes.AppendLine("}");












                foreach (DatabaseDesign item in DatabaseDesignerList)
                {

                    sql.Append(item.SQL);
                    doc.Append(item.Documentation);
                    classes.Append(item.CsClass);

                }

                classes.AppendLine("}");


                //And of course the AppDBContext

                StringBuilder dbContext = new StringBuilder();
                dbContext.AppendLine("using Microsoft.EntityFrameworkCore;");
                dbContext.AppendLine();
                dbContext.AppendLine("public class AppDbContext : DbContext");
                dbContext.AppendLine("{");
                dbContext.AppendLine("    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }");
                dbContext.AppendLine();






                foreach (DatabaseDesign item in DatabaseDesignerList)
                {
                    dbContext.AppendLine($"    public DbSet<{item.ClassName}> {item.ClassName}s {{ get; set; }}");
                }

                dbContext.AppendLine("}");


                // --- Prepare paths ---
                string basePath = string.IsNullOrEmpty(CurrentDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : CurrentDirectory;

                string generatedDBPath = Path.Combine(basePath, "GeneratedDB");
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

                await CreateFile("SQL", "sql", sql.ToString());
                await CreateFile("Documentation", "md", doc.ToString());
                await CreateFile("Classes", "cs", classes.ToString());
                await CreateFile("Models", "cs", dbContext.ToString());

                // --- Success UI ---
                ShowBuildSuccess();

               //For testing, I throw new Exception();
            };


            Exports.Click += (s, e) =>
            {
                var CurrentDirectory = Path.Combine(mainPage.SeshDirectory.ConvertToString(), mainPage.SeshUsername.ConvertToString(), "Projects", mainPaged.ProjectName);


                var createUrl = $"{baseUrl}Utilities/OpenDirectory?directory={Uri.EscapeDataString(CurrentDirectory)}";

                var createdResponse = mainPage.DBDesignerClient.PostAsync(createUrl, new StringContent("")).GetAwaiter().GetResult();
                createdResponse.EnsureSuccessStatusCode();


            };

            Logout.Click += (s, e) =>
            {
                mainPage.CreateWindow(() => new LogoutConfirm(mainPage), "Logging Out...", true);
            };

            //Add Logout eventually



        
           ExitButton.Click += (s, e) => { try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };

           
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

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateTime();
        }

        private void UpdateTime()
        {
            DateTime now = DateTime.Now;

            string time = now.ToString("hh:mm");
            TIme.Text = time;


            if (now.Hour >= 12)
            {
                TimeXM.Text = "PM";
            }

            else
            {
                TimeXM.Text = "AM";
            }

            string date = now.ToString("MMMM d yyyy");
            Date.Text = date;

            Seconds.Text = now.ToString("ss");

        }




        // Executes when the user navigates to this page.
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }

        //Eventually add settings here, might add music player as a HTML thing and implement the miniplayer I designed in a future update (Focusing on releasing ATP)




    }
}
