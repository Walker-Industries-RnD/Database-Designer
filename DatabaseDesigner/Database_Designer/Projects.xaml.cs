
using DatabaseDesigner;
using Org.BouncyCastle.Asn1.X509;
using Pariah_Cybersecurity;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Walker.Crypto;
using WISecureData;
using static Database_Designer.MainPage;
using static DatabaseDesigner.DBDesigner;
using static DatabaseDesigner.Index;
using static DatabaseDesigner.Reference;
using static DatabaseDesigner.Row;
using static DatabaseDesigner.SessionStorage;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;
using Application = System.Windows.Application;
using File = System.IO.File;
using Image = System.Windows.Controls.Image;
using Path = System.IO.Path;
using Reference = DatabaseDesigner.Reference;
using ReferenceOptions = DatabaseDesigner.SessionStorage.ReferenceOptions;


namespace Database_Designer
{
    public partial class Projects : Page
    {
        private static MainPage mainPage;
        public UIWindowEntry WindowInfo { get; private set; }

        string SearchText = "";

        public void RefreshProjects()
        {
            page = 1;                    // Reset to first page on refresh
            SearchText = "";
            SearchBar.Text = "";
            HandlePage();
        }


        public static RowCreation ConvertRowOptionsToCreation(RowOptions options)
        {
            string? arrayLimit = options.ArrayLimit.HasValue ? options.ArrayLimit.Value.ToString() : null;

            return new RowCreation
            {
                Name = options.FieldName,
                Description = options.Description,
                RowType = options.PostgresType,
                Limit = options.Limit,
                IsArray = options.IsArray,
                ArrayLimit = arrayLimit,
                EncryptedAndNOTMedia = options.IsEncrypted,
                Media = options.IsMedia,
                IsPrimary = options.IsPrimary,
                IsUnique = options.IsUnique,
                IsNotNull = options.IsNotNull,
                DefaultValue = options.DefaultValue,
                Check = options.Check,
                DefaultIsPostgresFunction = options.DefaultIsKeyword
            };
        }

        public static IndexCreation ConvertIndexDefinition(IndexDefinition indexDef)
        {
            return new IndexCreation
            {
                TableName = indexDef.TableName,
                IndexName = indexDef.IndexName,
                UseJsonbPathOps = indexDef.UseJsonbPathOps,
                ColumnNames = indexDef.ColumnNames?.ToList() ?? new List<string>(),
                IndexType = indexDef.IndexType.ToString(),
                Condition = indexDef.Condition,
                Expression = indexDef.Expression,
                IndexTypeCustom = indexDef.IndexTypeCustom
            };
        }



        //10 Projects Per Page
        int page = 1;

        Dictionary<int, (ProjectsPreviews, string)> Previews = new Dictionary<int, (ProjectsPreviews, string)>();


        //Eventually I need to switch to use this instead of System.IO but i'm tired bro
        public Projects(MainPage mainPaged)
        {
            this.InitializeComponent();
            mainPage = mainPaged;

            NewProjectBtn.Click += (s, e) =>
            {
                BasicDatabaseDesigner DBT = default;
                mainPage.CreateWindows("Template", () => {
                    DBT = new BasicDatabaseDesigner(mainPage, null, this, BasicDatabaseDesigner.DesignMode.Project, null, null, null);
                    return DBT;
                }, false);

                this.RefreshProjects();  // Refresh immediately

            };


            HandlePage();


            Back.Click += (s, e) =>
            {
                page -= 1;
                HandlePage();
            };

            Forward.Click += (s, e) =>
            {
                page += 1;
                HandlePage();
            };


            TableViewSearch.Click += (s, e) =>
            {
                SearchText = SearchBar.Text;
                page = 1;
                HandlePage();
            };




            GLORYTOMANKIND.Click += async (s, e) =>
            {
                string schema = "Yorha";

                #region Generate Settings

                TableObject MakeTable(string name, string desc, RowOptions[] rows, ReferenceOptions[]? refs)
                {
                    return new TableObject
                    {
                        TableName = name,
                        Description = desc,
                        Rows = rows.Select(ConvertRowOptionsToCreation).ToList(),
                        References = refs?.ToList(),
                        CustomRows = null,
                        Indexes = new List<IndexCreation>
                        {
                            ConvertIndexDefinition(new IndexDefinition($"{schema}.{name}", $"{name}_idx", new[] { "id" }, IndexType.Basic)
)
                        },
                        SchemaName = schema
                    };
                }

                // ---------- Define Tables ----------

                // 1. operator
                var operatorTable = MakeTable("operator", "Registered Yorha operators", new[]
                {
                    new RowOptions("id", "Primary key", PostgresType.Uuid, isPrimary: true),
                    new RowOptions("codename", "Operator codename", PostgresType.Text, isNotNull: true),
                    new RowOptions("rank", "Operator rank", PostgresType.Text),
                    new RowOptions("role", "Assigned role", PostgresType.Text),
                    new RowOptions("status", "Operational status", PostgresType.Text)
                }, null);

                // 2. mission
                var missionTable = MakeTable("mission", "Mission details and assignments", new[]
                {
                    new RowOptions("id", "Primary key", PostgresType.Uuid, isPrimary: true),
                    new RowOptions("title", "Mission title", PostgresType.Text, isNotNull: true),
                    new RowOptions("description", "Mission description", PostgresType.Text),
                    new RowOptions("assigned_operator", "Assigned operator", PostgresType.Uuid),
                    new RowOptions("start_time", "Mission start time", PostgresType.Timestamp),
                    new RowOptions("end_time", "Mission end time", PostgresType.Timestamp)
                }, new[]
                {
                    new ReferenceOptions(schema + ".mission", schema + ".operator", "assigned_operator", "id", ReferentialAction.SetNull, ReferentialAction.NoAction)
                });

                // 3. combat_log
                var combatLogTable = MakeTable("combat_log", "Combat log events", new[]
                {
                    new RowOptions("id", "Primary key", PostgresType.Uuid, isPrimary: true),
                    new RowOptions("operator_id", "Operator involved", PostgresType.Uuid, isNotNull: true),
                    new RowOptions("mission_id", "Mission associated", PostgresType.Uuid),
                    new RowOptions("event_time", "Event timestamp", PostgresType.Timestamp, isNotNull: true),
                    new RowOptions("event_data", "Detailed JSON event data", PostgresType.Jsonb)
                }, new[]
                {
                    new ReferenceOptions(schema + ".combat_log", schema + ".operator", "operator_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction),
                    new ReferenceOptions(schema + ".combat_log", schema + ".mission", "mission_id", "id", ReferentialAction.SetNull, ReferentialAction.NoAction)
                });

                // 4. equipment
                var equipmentTable = MakeTable("equipment", "Equipment used by operators", new[]
                {
                    new RowOptions("id", "Primary key", PostgresType.Uuid, isPrimary: true),
                    new RowOptions("name", "Equipment name", PostgresType.Text, isNotNull: true),
                    new RowOptions("type", "Equipment type", PostgresType.Text),
                    new RowOptions("operator_id", "Assigned operator", PostgresType.Uuid)
                }, new[]
                {
                    new ReferenceOptions(schema + ".equipment", schema + ".operator", "operator_id", "id", ReferentialAction.SetNull, ReferentialAction.NoAction)
                });

                // 5. maintenance_log
                var maintenanceTable = MakeTable("maintenance_log", "Maintenance activity records", new[]
                {
                    new RowOptions("id", "Primary key", PostgresType.Uuid, isPrimary: true),
                    new RowOptions("equipment_id", "Equipment serviced", PostgresType.Uuid, isNotNull: true),
                    new RowOptions("performed_by", "Operator who performed maintenance", PostgresType.Uuid),
                    new RowOptions("timestamp", "Time of maintenance", PostgresType.Timestamp, isNotNull: true),
                    new RowOptions("details", "Details of maintenance work", PostgresType.Text)
                }, new[]
                {
                    new ReferenceOptions(schema + ".maintenance_log", schema + ".equipment", "equipment_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction),
                    new ReferenceOptions(schema + ".maintenance_log", schema + ".operator", "performed_by", "id", ReferentialAction.SetNull, ReferentialAction.NoAction)
                });

                // 6. mission_objective
                var objectiveTable = MakeTable("mission_objective", "Objectives for missions", new[]
                {
                    new RowOptions("id", "Primary key", PostgresType.Uuid, isPrimary: true),
                    new RowOptions("mission_id", "Related mission", PostgresType.Uuid, isNotNull: true),
                    new RowOptions("objective_text", "Objective description", PostgresType.Text),
                    new RowOptions("is_completed", "Objective completion status", PostgresType.Boolean, isNotNull: true)
                }, new[]
                {
                    new ReferenceOptions(schema + ".mission_objective", schema + ".mission", "mission_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction)
                });

                // 7. mission_report
                var reportTable = MakeTable("mission_report", "Post-mission report submissions", new[]
                {
                    new RowOptions("id", "Primary key", PostgresType.Uuid, isPrimary: true),
                    new RowOptions("mission_id", "Mission associated", PostgresType.Uuid, isNotNull: true),
                    new RowOptions("submitted_by", "Operator submitting report", PostgresType.Uuid),
                    new RowOptions("report_text", "Detailed mission report", PostgresType.Text)
                }, new[]
                {
                    new ReferenceOptions(schema + ".mission_report", schema + ".mission", "mission_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction),
                    new ReferenceOptions(schema + ".mission_report", schema + ".operator", "submitted_by", "id", ReferentialAction.SetNull, ReferentialAction.NoAction)
                });

                // 8. squad
                var squadTable = MakeTable("squad", "Operator groups", new[]
                {
                    new RowOptions("id", "Primary key", PostgresType.Uuid, isPrimary: true),
                    new RowOptions("name", "Squad name", PostgresType.Text, isNotNull: true)
                }, null);

                // 9. squad_member
                var squadMemberTable = MakeTable("squad_member", "Links operators to squads", new[]
                {
                        new RowOptions("id", "Squad", PostgresType.Uuid, isNotNull: true, isPrimary: true),
    new RowOptions("squad_id", "Squad", PostgresType.Uuid, isNotNull: true, isPrimary: false),
    new RowOptions("operator_id", "Member operator", PostgresType.Uuid, isNotNull: true, isPrimary: false)
}, new[]
                {

    new ReferenceOptions(schema + ".squad_member", schema + ".squad", "squad_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction),
    new ReferenceOptions(schema + ".squad_member", schema + ".operator", "operator_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction)
});


                // 10. alert_log
                var alertLogTable = MakeTable("alert_log", "System alerts", new[]
                {
                    new RowOptions("id", "Primary key", PostgresType.Uuid, isPrimary: true),
                    new RowOptions("timestamp", "Alert time", PostgresType.Timestamp, isNotNull: true),
                    new RowOptions("severity", "Alert severity", PostgresType.Text),
                    new RowOptions("details", "Alert details", PostgresType.Text)
                }, null);

                // 11. supply_request
                var supplyRequestTable = MakeTable("supply_request", "Requests for supplies", new[]
                {
                    new RowOptions("id", "Primary key", PostgresType.Uuid, isPrimary: true),
                    new RowOptions("requested_by", "Operator requesting", PostgresType.Uuid, isNotNull: true),
                    new RowOptions("item_name", "Item requested", PostgresType.Text, isNotNull: true),
                    new RowOptions("quantity", "Quantity requested", PostgresType.BigInt)
                }, new[]
                {
                    new ReferenceOptions(schema + ".supply_request", schema + ".operator", "requested_by", "id", ReferentialAction.Cascade, ReferentialAction.NoAction)
                });

                // 12. training_session
                var trainingSessionTable = MakeTable("training_session", "Operator training records", new[]
                {
                    new RowOptions("id", "Primary key", PostgresType.Uuid, isPrimary: true),
                    new RowOptions("operator_id", "Operator trained", PostgresType.Uuid, isNotNull: true),
                    new RowOptions("session_date", "Training date", PostgresType.Timestamp, isNotNull: true),
                    new RowOptions("skill", "Skill trained", PostgresType.Text)
                }, new[]
                {
                    new ReferenceOptions(schema + ".training_session", schema + ".operator", "operator_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction)
                });

                // 13. communication_log
                var communicationLogTable = MakeTable("communication_log", "Operator communications", new[]
                {
                    new RowOptions("id", "Primary key", PostgresType.Uuid, isPrimary: true),
                    new RowOptions("operator_id", "Operator involved", PostgresType.Uuid, isNotNull: true),
                    new RowOptions("message_time", "Message timestamp", PostgresType.Timestamp, isNotNull: true),
                    new RowOptions("message_text", "Message content", PostgresType.Text)
                }, new[]
                {
                    new ReferenceOptions(schema + ".communication_log", schema + ".operator", "operator_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction)
                });

                // 14. repair_ticket
                var repairTicketTable = MakeTable("repair_ticket", "Equipment repair tickets", new[]
                {
                    new RowOptions("id", "Primary key", PostgresType.Uuid, isPrimary: true),
                    new RowOptions("equipment_id", "Equipment to repair", PostgresType.Uuid, isNotNull: true),
                    new RowOptions("reported_by", "Operator reporting", PostgresType.Uuid),
                    new RowOptions("description", "Issue description", PostgresType.Text),
                    new RowOptions("status", "Repair status", PostgresType.Text)
                }, new[]
                {
                    new ReferenceOptions(schema + ".repair_ticket", schema + ".equipment", "equipment_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction),
                    new ReferenceOptions(schema + ".repair_ticket", schema + ".operator", "reported_by", "id", ReferentialAction.SetNull, ReferentialAction.NoAction)
                });

                // 15. deployment_record
                var deploymentRecordTable = MakeTable("deployment_record", "Operator deployments", new[]
                {
                    new RowOptions("id", "Primary key", PostgresType.Uuid, isPrimary: true),
                    new RowOptions("operator_id", "Operator deployed", PostgresType.Uuid, isNotNull: true),
                    new RowOptions("mission_id", "Mission deployed to", PostgresType.Uuid),
                    new RowOptions("deployment_time", "Deployment timestamp", PostgresType.Timestamp)
                }, new[]
                {
                    new ReferenceOptions(schema + ".deployment_record", schema + ".operator", "operator_id", "id", ReferentialAction.Cascade, ReferentialAction.NoAction),
                    new ReferenceOptions(schema + ".deployment_record", schema + ".mission", "mission_id", "id", ReferentialAction.SetNull, ReferentialAction.NoAction)
                });

                #endregion

                // ---------- Combine All Tables ----------
                var allTables = new ObservableCollection<TableObject>
                {
                    operatorTable,
                    missionTable,
                    combatLogTable,
                    equipmentTable,
                    maintenanceTable,
                    objectiveTable,
                    reportTable,
                    squadTable,
                    squadMemberTable,
                    alertLogTable,
                    supplyRequestTable,
                    trainingSessionTable,
                    communicationLogTable,
                    repairTicketTable,
                    deploymentRecordTable
                };
                var uuid = Utilities.CreateUUID();
                string ProjectTitle = $"YORHA-TEST-SITE-{uuid}";
                string DescriptionName = "A systems test. Glory to Mankind.";

                string baseUrl = mainPage.baseUrl;


                if (ProjectTitle != default && DescriptionName != default)
                {
                    var CurrentDirectory = Path.Combine(mainPage.SeshDirectory.ConvertToString(), mainPage.SeshUsername.ConvertToString(), "Projects");

                    string fileName = ProjectTitle;
                    string extension = "secdbdesign";
                    string existsUrl = $"{baseUrl}File/Exists?directory={Uri.EscapeDataString(CurrentDirectory)}&fileName={Uri.EscapeDataString(fileName)}&extension={extension}";
                    var existsResponse = mainPage.DBDesignerClient.GetAsync(existsUrl).GetAwaiter().GetResult();

                    var existsResultJson = existsResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    using var doc = System.Text.Json.JsonDocument.Parse(existsResultJson);
                    bool exists = doc.RootElement.GetProperty("exists").GetBoolean();





                    mainPage.MainSessionInfo = new SessionStorage.DBDesignerSession()
                    {
                        SessionName = ProjectTitle,
                        SessionDescription = DescriptionName,
                        LastEdited = DateTime.Now,
                        SessionLogo = default,
                        Tables = allTables, //!
                        WindowStatuses = new Dictionary<string, SessionStorage.Coords>() //We don;t use this anymore, will be removed if I don't change my mind by next major update
                    };

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true, // pretty print
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    string json = JsonSerializer.Serialize(mainPage.MainSessionInfo, options);

                    // Encryption
                    var securedJson = json.ToSecureData();

                    var SaveDirectory = Path.Combine(
                        mainPage.SeshDirectory.ConvertToString(),
                        mainPage.SeshUsername.ConvertToString(),
                        "Projects",
                        ProjectTitle
                    );

                    // Convert securedJson to Base64
                    var contentBase64 = Convert.ToBase64String(securedJson.ConvertToBytes());

                    // Prepare JSON payload
                    var payload = new
                    {
                        Directory = SaveDirectory,
                        FileName = ProjectTitle,
                        Extension = "secdbdesign",
                        Content = contentBase64
                    };

                    var createUrl = $"{baseUrl}File/Create";

                    var json2 = JsonSerializer.Serialize(payload);
                    var httpContent = new StringContent(json2, Encoding.UTF8, "application/json");

                    // Send POST request with JSON body
                    var createdResponse = await mainPage.DBDesignerClient.PostAsync(createUrl, httpContent);





                    mainPage.ProjectName = ProjectTitle;


                    mainPage.ForceCollectionChangeUpate();

                    //    Step.Visibility = Visibility.Collapsed;
                    //     Finalized.Visibility = Visibility.Visible;


                    var templateWindow = mainPage.IntroPage.Children
                        .OfType<BasicDatabaseDesigner>()
                        .FirstOrDefault();

                    if (templateWindow != null)
                    {
                        mainPage.IntroPage.Children.Remove(templateWindow);
                    }


                    string previewPath = Path.Combine(
                        SaveDirectory,
                        $"{ProjectTitle}.PREVIEW"
                    );

                    var preview = new ProjectsPreviews(
                        ProjectTitle,
                        DescriptionName,
                        DateTime.UtcNow.ToString(),
                        null
                    );

                    File.WriteAllText(previewPath, preview.ToString());

                    this.RefreshProjects();




                }
            };

            ProjectsListViewer.MouseWheel += (sender, e) =>
            {

                double newOffset = ProjectsListViewer.VerticalOffset - e.Delta;
                ProjectsListViewer.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            };

            ExitButton.Click += (s, e) => { try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); } catch (ArgumentOutOfRangeException) { } };

            this.Unloaded += (s, e) =>
            {
                RemoveWindow();
            };



        }


        public void HandleLogic()
        {
            if (page == 1)
            {
                Back.Visibility = Visibility.Collapsed;
            }

            else
            {
                Back.Visibility = Visibility.Visible;
            }


        }



        public void HandlePage()
        {

            for (int i = ProjectsList.Children.Count - 1; i > 0; i--)
            {
                ProjectsList.Children.RemoveAt(i);
            }


            var mainPath = mainPage.SeshDirectory.ConvertToString();
            var ProjectsFolder = Path.Combine(mainPath, mainPage.SeshUsername.ConvertToString(), "Projects");

            var Preview = new ProjectsPreviews(mainPage.MainSessionInfo.SessionName, mainPage.MainSessionInfo.SessionDescription, DateTime.UtcNow.ToString(), null);

            var folders = Directory
            .GetDirectories(ProjectsFolder, "*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(d => Directory.GetLastWriteTime(d))  // Newest modified first
            .ToArray();


            ProjectsCountUI.Text = $"{folders.Count()} Projects";


            void RefreshPreviewsList()
            {
                Previews.Clear();
                foreach (var folder in folders)
                {
                    var DBFile = Directory
                        .EnumerateFiles(folder, "*.PREVIEW")
                        .FirstOrDefault();

                    if (DBFile == null)
                        continue;

                    var IMG = Directory
                        .EnumerateFiles(folder)
                        .FirstOrDefault(f =>
                        {
                            var name = Path.GetFileNameWithoutExtension(f);
                            var ext = Path.GetExtension(f);
                            return name.Equals("projectIcon", StringComparison.OrdinalIgnoreCase)
                                && (ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                                 || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase));
                        });




                    var PreviewInfo = File.ReadAllText(DBFile);

                    var previewData = ProjectsPreviews.FromString(PreviewInfo);

                    Previews.Add(previewData.GetHashCode(), (previewData, folder));

                }

            }

            Dictionary<int, (ProjectsPreviews, string)> PreviewsLoadList = new Dictionary<int, (ProjectsPreviews, string)>();

            void SearchVIATerm()
            {
                Dictionary<int, (ProjectsPreviews, string)> tempList = new Dictionary<int, (ProjectsPreviews, string)>();

                tempList = Previews
    .Where(kvp => kvp.Value.Item1.SessionName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                  kvp.Value.Item1.SessionDescription.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                Previews = tempList;


            }

            HandleProjectsUI();




            //12 per page
            void HandleProjectsUI()
            {
                var folders = Directory
.GetDirectories(ProjectsFolder, "*", SearchOption.TopDirectoryOnly)
.OrderByDescending(d => Directory.GetLastWriteTime(d))  // Newest modified first
.ToArray();

                ProjectsCountUI.Text = $"{folders.Count()} Projects";

                for (int i = ProjectsList.Children.Count - 1; i > 0; i--)
                {
                    ProjectsList.Children.RemoveAt(i);
                }

                if (SearchText != null && SearchText != "")
                {
                    RefreshPreviewsList();
                    SearchVIATerm();
                    page = 1;
                }

                else
                {
                    RefreshPreviewsList();
                }
                double MaxPages = Previews.Count / 12;

                ProjectsIndicator.Text = $"{page}/{Math.Ceiling(MaxPages) + 1}";

                if (page == 1)
                {
                    Back.IsEnabled = false;
                }

                else
                {
                    Back.IsEnabled = true;
                }

                if (page == Math.Ceiling(MaxPages) + 1)
                {
                    Forward.IsEnabled = false;
                }

                else
                {
                    Forward.IsEnabled = true;
                }

                var skipAmount = (page - 1) * 12;

                PreviewsLoadList = Previews
                    .Skip(skipAmount)
                    .Take(12)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                foreach (var item in PreviewsLoadList)
                {


                    var newProjectsCard = CreateProjectCard("Project Hash:" + item.Key.ToString(), item.Value.Item1.SessionName, item.Value.Item1.SessionDescription, null, item.Value.Item2, this);
                    ProjectsList.Children.Add(newProjectsCard.Item1);

                    newProjectsCard.Item2.Click += (s, e) =>
                    {

                        mainPage.RemoveTableUpdater();

                        var ProjectFile = Directory.GetFiles(item.Value.Item2, "*.SECDBDESIGN").First();

                        var ProjectBytes = File.ReadAllText(ProjectFile);

                        var ProjectAESBytes = Convert.FromBase64String(ProjectBytes);

                        var ProjectUTF8 = Encoding.UTF8.GetString(ProjectAESBytes);

                        var ProjectAESToDecrypt = SimpleAESEncryption.AESEncryptedText.FromString(ProjectUTF8);

                        var ProjectData = Task.Run(() =>
                        {
                            return mainPage.FromSessionString(ProjectAESToDecrypt, mainPage.Password);
                        }).Result;

                        Console.WriteLine(ProjectData.SessionName);

                        mainPage.MainSessionInfo = ProjectData;

                        mainPage.ProjectName = ProjectData.SessionName;
                        mainPage.LoadProject(mainPage.ProjectName);

                        mainPage.ForceCollectionChangeUpate();

                        mainPage.LowerAppBar.Children.Clear();
                        mainPage.IntroPage.Children.Clear();

                        mainPage.AddTableUpdater();

                        var introMessage = new List<(string Text, MiraMiniPopup.MiraStates Expression)>
                        {
                            ("The concept of losing your work all at once is really scary...", MiraMiniPopup.MiraStates.Neutral),
                            ("That's why I automatically save your progress whenever you make a change.", MiraMiniPopup.MiraStates.Happy),
                            ("THAT ISN'T EASY FOR YOUR INFORMATION!", MiraMiniPopup.MiraStates.Angry),
                            ("Jeez I want to go to sleep...", MiraMiniPopup.MiraStates.Ummm),
                        };

                        mainPage.CreateWindow(
                            () => new MiraMiniPopup(introMessage, mainPage),
                            "Mira",
                            true,
                            null,
                            600,
                            600
                        );}; //I'm not even gonna try
            }

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

            }

            catch
            {

            }
            }

        

        // Executes when the user navigates to this page.
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
        }

        public struct AuthorData
        {
            public string Name;
            public string? Bio;
            public string? Org;
            public string Website;
            public string? LogoPath;
            public string? BannerPath;
            public string? License;

        }

        public struct TableFile
        {
            public string Name { get; init; }
            public string Details { get; init; }
            public string UUID { get; init; }
            public string Image { get; init; }
            public AuthorData Author { get; init; }
            public List<TableObject> Columns { get; init; }
        }

        //Read this from the result of an async filestream

        public async Task<TableFile> GetTableData(string data)
        {
            var JsonData = System.Text.Json.JsonSerializer.Deserialize<JsonObject>(data);

            var name = JsonData?["name"]?.GetValue<string>();
            var details = JsonData?["details"]?.GetValue<string>();
            var identifier = JsonData?["uuid"]?.GetValue<string>();
            var image = JsonData?["image"]?.GetValue<string>();

            List<TableObject> tables = new List<TableObject>();

            var AuthorObj = JsonData["author_data"].AsObject();

            var Metadata = new AuthorData
            {
                Name = AuthorObj["author_name"]?.ToString(),
                Bio = AuthorObj["author_bio"]?.ToString(),
                Org = AuthorObj["author_org"]?.ToString(),
                Website = AuthorObj["author_website"]?.ToString(),
                LogoPath = AuthorObj["author_logopath"]?.ToString(),
                BannerPath = AuthorObj["author_bannerpath"]?.ToString(),
                License = AuthorObj["license"]?.ToString()
            };

            foreach (var item in JsonData["table_templates"].AsArray())
            {
                try
                {
                    var tableObj = item.AsObject();

                    var TableName = tableObj["TableName"];
                    var Description = tableObj["Description"];
                    var SchemaName = tableObj["SchemaName"];
                    var Logo = tableObj["Logo"];

                    List<RowCreation> Rows = new List<RowCreation>();
                    foreach (var row in tableObj["Rows"].AsArray())
                    {
                        var rowObj = row.AsObject();

                        var rowName = rowObj["Name"];
                        var rowDescription = rowObj["Description"];
                        var rowEncryptedAndNOTMedia = rowObj["EncryptedAndNOTMedia"];
                        var rowMedia = rowObj["Media"];
                        var rowRowType = rowObj["RowType"];
                        var rowLimit = rowObj["Limit"];
                        var rowIsArray = rowObj["IsArray"];
                        var rowArrayLimit = rowObj["ArrayLimit"];
                        var rowIsPrimary = rowObj["IsPrimary"];
                        var rowIsUnique = rowObj["IsUnique"];
                        var rowIsNotNull = rowObj["IsNotNull"];
                        var rowDefaultValue = rowObj["DefaultValue"];
                        var rowDefaultIsPostgresFunction = rowObj["DefaultIsPostgresFunction"];
                        var rowCheck = rowObj["Check"];

                        if (rowName != null &&
                            rowDescription != null &&
                            rowName != null &&
                            rowName != null)
                        {
                            var newRowCreationItem = new RowCreation
                            {
                                Name = rowName.ToString(),
                                Description = rowDescription?.ToString(),
                                EncryptedAndNOTMedia = rowEncryptedAndNOTMedia?.GetValue<bool>(),
                                Media = rowMedia?.GetValue<bool>(),
                                RowType = rowRowType != null
                                    ? Enum.Parse<DBDesigner.PostgresType>(rowRowType.ToString())
                                    : null,
                                Limit = rowLimit?.GetValue<int>(),
                                IsArray = rowIsArray?.GetValue<bool>(),
                                ArrayLimit = rowArrayLimit?.GetValue<string>(),
                                IsPrimary = rowIsPrimary?.GetValue<bool>(),
                                IsUnique = rowIsUnique?.GetValue<bool>(),
                                IsNotNull = rowIsNotNull?.GetValue<bool>(),
                                DefaultValue = rowDefaultValue?.ToString(),
                                DefaultIsPostgresFunction = rowDefaultIsPostgresFunction?.GetValue<bool>(),
                                Check = rowCheck?.ToString()
                            };

                            Rows.Add(newRowCreationItem);
                        }
                    }

                    if (Rows.Count == 0)
                    {
                        throw new Exception("Please Add Rows.");
                    }

                    List<ReferenceOptions> References = new List<ReferenceOptions>();
                    if (tableObj["References"] != null)
                    {
                        foreach (var reference in tableObj["References"].AsArray())
                        {
                            var refObj = reference.AsObject();

                            var refMainTable = refObj["MainTable"];
                            var refRefTable = refObj["RefTable"];
                            var refForeignKey = refObj["ForeignKey"];
                            var refRefTableKey = refObj["RefTableKey"];
                            var refOnDeleteAction = refObj["OnDeleteAction"];
                            var refOnUpdateAction = refObj["OnUpdateAction"];

                            if (refMainTable != null ||
                                refRefTable != null ||
                                refForeignKey != null ||
                                refRefTableKey != null ||
                                refOnUpdateAction != null ||
                                refOnDeleteAction != null)
                            {
                                var referenceOption = new ReferenceOptions
                                {
                                    MainTable = refMainTable?.ToString(),
                                    RefTable = refRefTable?.ToString(),
                                    ForeignKey = refForeignKey?.ToString(),
                                    RefTableKey = refRefTableKey?.ToString(),
                                    OnDeleteAction = refOnDeleteAction != null
                                        ? Enum.TryParse(refOnDeleteAction.ToString(), true, out Reference.ReferentialAction deleteAction)
                                            ? deleteAction
                                            : Reference.ReferentialAction.NoAction
                                        : Reference.ReferentialAction.NoAction,
                                    OnUpdateAction = refOnUpdateAction != null
                                        ? Enum.TryParse(refOnUpdateAction.ToString(), true, out Reference.ReferentialAction updateAction)
                                            ? updateAction
                                            : Reference.ReferentialAction.NoAction
                                        : Reference.ReferentialAction.NoAction
                                };

                                References.Add(referenceOption);
                            }
                        }
                    }

                    List<IndexCreation> Indexes = new List<IndexCreation>();
                    if (tableObj["Indexes"] != null)
                    {
                        foreach (var index in tableObj["Indexes"].AsArray())
                        {
                            var indexObj = index.AsObject();

                            var indexTableName = indexObj["TableName"];
                            var indexIndexType = indexObj["IndexType"];
                            var indexCondition = indexObj["Condition"];
                            var indexExpression = indexObj["Expression"];
                            var indexIndexTypeCustom = indexObj["IndexTypeCustom"];

                            var indexColumnNames = indexObj["ColumnNames"].AsArray();

                            List<string> columnNames = new List<string>();
                            foreach (var columnName in indexColumnNames)
                            {
                                if (columnName != null)
                                    columnNames.Add(columnName.ToString());
                            }

                            var IndexItem = new IndexCreation
                            {
                                TableName = indexTableName.ToString(),
                                ColumnNames = columnNames,
                                IndexType = indexIndexType.ToString(),
                                Condition = indexCondition.ToString(),
                                Expression = indexExpression.ToString(),
                                IndexTypeCustom = indexIndexTypeCustom.ToString(),
                            };

                            Indexes.Add(IndexItem);
                        }
                    }

                    var newTableObject = new TableObject
                    {
                        TableName = TableName.ToString(),
                        Description = Description.ToString(),
                        Rows = Rows,
                        CustomRows = null,
                        References = References,
                        Indexes = Indexes,
                        SchemaName = SchemaName.ToString()
                    };

                    tables.Add(newTableObject);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                catch
                {
                    continue;
                }
            }

            return new TableFile
            {
                Name = identifier,
                Details = details,
                UUID = identifier,
                Image = image,
                Author = Metadata,
                Columns = tables
            };
        }

        //Templates and Projects are very similar in structure
        public async Task<List<TableFile>> GetTemplates(string TemplatesFolder)
        {
            List<TableFile> templates = new List<TableFile>();

            var JsonFilePaths = Directory.GetFiles(TemplatesFolder, "*.json");

            foreach (var item in JsonFilePaths)
            {
                try
                {

                    using var reader = new StreamReader(item);
                    var jsonString = await reader.ReadToEndAsync();

                    var tableData = await GetTableData(jsonString);
                    templates.Add(tableData);
                }
                catch
                {
                    continue;
                }
            }

            return templates;

        }

        public async Task<List<(TableFile, DateTime, DateTime)>> GetProjects(string ProjectsFolder)
        {
            List<(TableFile, DateTime, DateTime)> projects = new List<(TableFile, DateTime, DateTime)>();

            var folders = Directory.GetDirectories(ProjectsFolder, "*", SearchOption.TopDirectoryOnly);

            foreach (var folder in folders)
            {
                var DBFile = Directory.GetFiles(folder, "*.PREVIEW").First();

                var IMG = Directory
                    .EnumerateFiles(folder)
                    .FirstOrDefault(f =>
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        var ext = Path.GetExtension(f);
                        return name.Equals("projectIcon", StringComparison.OrdinalIgnoreCase)
                            && (ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                             || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase));
                    });




                var DBInfo = File.ReadAllText(DBFile);

                var newAESObject = SimpleAESEncryption.AESEncryptedText.FromString(DBFile);

                var rrr = mainPage.FromSessionString(newAESObject, mainPage.Password);


            }

            return projects;

        }



        //We will populate the projects UI list

        public static (Grid, Button, Button) CreateProjectCard(string ProjectUUID, string MainTitle, string Subtitle, string? ImagePath, string ProjectPath, Projects ProjectUI)
        {

            if (ImagePath == null)
            {
                ImagePath = "/Database_Designer;component/assets/images/bg.png";
            }


            // Root Grid
            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 786,
                Height = 264,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0))
            };

            // Background image
            var bgImage = new Image
            {
                Source = new BitmapImage(new Uri("/Database_Designer;component/assets/images/bg.png", UriKind.Relative)),
                Margin = new Thickness(0),
                Stretch = Stretch.Fill
            };
            grid.Children.Add(bgImage);

            // Top StackPanel
            var headerPanel = new StackPanel
            {
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 784,
                Height = 40,
                Orientation = Orientation.Horizontal,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x45, 0x41, 0x38))
            };

            var rect = new Rectangle
            {
                Width = 28,
                Height = 28,
                Stroke = new SolidColorBrush(Colors.Gray),
                StrokeThickness = 1,
                Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x2C, 0x2B, 0x28)),
                Margin = new Thickness(10, 0, 0, 0)
            };
            headerPanel.Children.Add(rect);

            var titleText = new TextBlock
            {
                Text = ProjectUUID,
                FontSize = 18,
                Height = 23,
                Margin = new Thickness(6, 0, 0, 0),
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
            };
            headerPanel.Children.Add(titleText);

            grid.Children.Add(headerPanel);

            // Left Rectangle
            var rect2 = new Rectangle
            {
                Margin = new Thickness(24, 64, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 168,
                Height = 168,
                Stroke = new SolidColorBrush(Colors.Gray),
                StrokeThickness = 1,
                Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x45, 0x41, 0x38))
            };
            grid.Children.Add(rect2);

            // Logo image
            var logo = new Image
            {
                Source = new BitmapImage(new Uri("/Database_Designer;component/assets/images/whitelogo.png", UriKind.Relative)),
                Margin = new Thickness(40, 80, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 144,
                Height = 136
            };
            grid.Children.Add(logo);

            try
            {
                string? logoImg = Directory.GetFiles(ProjectPath, "ProjectIcon.*")[0];


                if (!string.IsNullOrEmpty(logoImg))
                {
                    try
                    {
                        byte[] imageBytes = File.ReadAllBytes(logoImg);
                        var bitmap = new BitmapImage();
                        using (var stream = new MemoryStream(imageBytes))
                        {
                            bitmap.SetSource(stream);
                        }
                        logo.Source = bitmap;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load banner: {ex.Message}");
                    }
                }
            }

            catch { }


            // Main title
            var mainTitle = new TextBlock
            {
                Text = MainTitle,
                FontSize = 26,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(-80, -96, -240, -8),
                Width = 528,
                Foreground = new SolidColorBrush(Colors.Black),
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
            };
            grid.Children.Add(mainTitle);

            // Subtitle
            var subtitle = new TextBlock
            {
                Text = Subtitle,
                FontSize = 20,
                Height = 64,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(168, 8, -8, -32),
                Width = 536,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = new SolidColorBrush(Colors.Black),
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf"),
            };
            grid.Children.Add(subtitle);

            // Open Project button
            var openButton = new Button
            {
                Content = "Open Project",
                Margin = new Thickness(208, 192, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 496,
                Height = 32,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x9D, 0x97, 0x85)),
                FontSize = 13,
                Foreground = (SolidColorBrush)Application.Current.Resources["Theme_BackgroundColor"]
            };
            grid.Children.Add(openButton);

            // Edit Project button, cancelled for now (At least this specific one)
            var editButton = new Button
            {
                Content = "Delete Project",
                Margin = new Thickness(464, 192, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Width = 248,
                Height = 32,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x9D, 0x97, 0x85)),
                FontSize = 13,
                Foreground = (SolidColorBrush)Application.Current.Resources["Theme_BackgroundColor"],
                Visibility = Visibility.Collapsed
            };
            grid.Children.Add(editButton);


            editButton.Click += (s, e) =>
            {
                mainPage.CreateWindow((() => new EditProjectData(mainPage)), "Edit Project", true);
                ProjectUI.RefreshProjects();
            };


            return (grid, openButton, editButton);
        }








        //For 1.2
        public async Task SetupDefaultTypes()
        {

        }

        public async Task GetAllTypes()
        {

        }

        public async Task GetCurrentTypes()
        {

        }
    }
}
