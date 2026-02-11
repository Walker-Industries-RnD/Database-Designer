using DatabaseDesigner;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities;
using Pariah_Cybersecurity;
using rm.Trie;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Browser;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Resources;
using System.Windows.Threading;
using System.Xml;
using Walker.Crypto;
using WISecureData;
using static Database_Designer.FolderViewer;
using static DatabaseDesigner.DBDesigner;
using static DatabaseDesigner.Index;
using static DatabaseDesigner.Reference;
using static DatabaseDesigner.Row;
using static DatabaseDesigner.SessionStorage;
using static Pariah_Cybersecurity.DataHandler;
using static Pariah_Cybersecurity.DataHandler.AccountsWithSessions;
using static System.Net.Mime.MediaTypeNames;
using Application = System.Windows.Application;
using Image = System.Windows.Controls.Image;
using ReferenceOptions = DatabaseDesigner.SessionStorage.ReferenceOptions;
using SecuritySettings = Pariah_Cybersecurity.DataHandler.AccountsWithSessions.SecuritySettings;


//Note to self; some of the errors here are from working on the sim instead of MAUI, should work way better when I convert (which will take like 10 seconds)
//Also clean unused libs

namespace Database_Designer
{



    public struct UIWindowEntry
    {
        public List<UIElement> Elements;       // All children windows (root, overlays, etc.)
        public Button Shortcut;                // The shortcut in the bar

        public UIWindowEntry(List<UIElement> elements, Button shortcut)
        {
            Elements = elements;
            Shortcut = shortcut;
        }
    }

    public class AppDefinition
    {
        public string Name { get; set; }
        public string ImagePath { get; set; }
        public bool AllowMultipleInstances { get; set; }
        public Func<Page> CreateWindowFunc { get; set; }
    }




    //Ngl a very hard to traverse script, but it works
    public partial class MainPage : Page
    {

        public string baseUrl = "";






        public HttpClient DBDesignerClient = new HttpClient();


        public Canvas WindowsHolder;




        public static event EventHandler IndexChanged;
        public static event EventHandler TableChanged;
        public static event EventHandler RefChanged;



        // Safely invoke events on the UI thread when possible. Use reflection to access
        
        // on all targets (prevents compile errors in different XAML frameworks).
        private static void InvokeOnUiThread(Action action)
        {
            try
            {
                var app = Application.Current;
                if (app != null)
                {
                    var prop = app.GetType().GetProperty("Dispatcher");
                    if (prop != null)
                    {
                        var disp = prop.GetValue(app) as System.Windows.Threading.Dispatcher;
                        if (disp != null)
                        {
                            disp.BeginInvoke(action);
                            return;
                        }
                    }
                }
            }
            catch { }

            // Fallback to immediate invocation
            try { action(); } catch { }
        }

        public static void OnIndexChanged()
        {
            InvokeOnUiThread(() => IndexChanged?.Invoke(null, EventArgs.Empty));
        }

        public static void OnTableChanged()
        {
            InvokeOnUiThread(() => TableChanged?.Invoke(null, EventArgs.Empty));
        }

        public static void OnRefChanged()
        {
            InvokeOnUiThread(() => RefChanged?.Invoke(null, EventArgs.Empty));
        }







        public static Dictionary<string, (UIElement, UIElement)> DesktopApps = new();
        public DBDesignerSession MainSessionInfo; //This is our Database Designer Session

        //Function that turns DBDesignerSession into text and back, had this genned by GPT to save time, then checked manually

        #region SessionHelpers

        public string ToSessionString(DBDesignerSession session, SecureData password)
        {
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

            writer.WriteStartObject();

            writer.WriteString("sessionName", session.SessionName);
            writer.WriteString("sessionDescription", session.SessionDescription);
            writer.WriteString("lastEdited", session.LastEdited.ToString("o"));
            writer.WriteString("sessionLogo", session.SessionLogo);

            // Tables
            writer.WritePropertyName("tables");
            writer.WriteStartArray();
            if (session.Tables is not null)
            {
                foreach (var t in session.Tables)
                    WriteTable(writer, t);
            }
            writer.WriteEndArray();



            // Window statuses
            writer.WritePropertyName("windowStatuses");
            writer.WriteStartObject();
            if (session.WindowStatuses is not null)
            {
                foreach (var kv in session.WindowStatuses)
                {
                    writer.WritePropertyName(kv.Key);
                    WriteCoords(writer, kv.Value);
                }
            }
            writer.WriteEndObject();

            writer.WriteEndObject();
            writer.Flush();


            return Encoding.UTF8.GetString(stream.ToArray());
        }
        //either change this or remove password; it does nothing but I feel it was supposed to

        public DBDesignerSession FromSessionString(SimpleAESEncryption.AESEncryptedText json, SecureData password)
        {
            var session = new DBDesignerSession
            {
                Tables = new ObservableCollection<TableObject>(),
                WindowStatuses = new Dictionary<string, Coords>()
            };

            var encryptedData = Walker.Crypto.AsyncAESEncryption.DecryptAsync(json, password).GetAwaiter().GetResult();

            using var doc = JsonDocument.Parse(encryptedData);
            var root = doc.RootElement;

            if (root.TryGetProperty("sessionName", out var sn)) session.SessionName = sn.GetString();
            if (root.TryGetProperty("sessionDescription", out var sd)) session.SessionDescription = sd.GetString();
            if (root.TryGetProperty("lastEdited", out var le)) session.LastEdited = le.GetDateTime();
            if (root.TryGetProperty("sessionLogo", out var sl)) session.SessionLogo = sl.GetString();

            // Tables
            if (root.TryGetProperty("tables", out var tablesElem))
            {
                foreach (var t in tablesElem.EnumerateArray())
                    session.Tables.Add(ReadTable(t));
            }

            // Window statuses
            if (root.TryGetProperty("windowStatuses", out var ws))
            {
                foreach (var kv in ws.EnumerateObject())
                    session.WindowStatuses[kv.Name] = ReadCoords(kv.Value);
            }


            return session;
        }

        // --- Internal writers ---
        public static void WriteTable(Utf8JsonWriter writer, TableObject table)
        {
            writer.WriteStartObject();
            writer.WriteString("tableName", table.TableName);
            writer.WriteString("description", table.Description);
            writer.WriteString("schemaName", table.SchemaName);

            // Rows
            writer.WritePropertyName("rows");
            writer.WriteStartArray();
            if (table.Rows != null)
            {
                foreach (var row in table.Rows)
                    WriteRow(writer, row);
            }
            writer.WriteEndArray();

            // CustomRows
            writer.WritePropertyName("customRows");
            writer.WriteStartArray();
            if (table.CustomRows != null)
            {
                foreach (var cr in table.CustomRows)
                    writer.WriteStringValue(cr);
            }
            writer.WriteEndArray();

            // Indexes
            writer.WritePropertyName("indexes");
            writer.WriteStartArray();
            if (table.Indexes != null)
            {
                foreach (var idx in table.Indexes)
                    WriteIndex(writer, idx);
            }
            writer.WriteEndArray();

            // References
            writer.WritePropertyName("references");
            writer.WriteStartArray();
            if (table.References != null)
            {
                foreach (var rf in table.References)
                    WriteReference(writer, rf);
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        public static TableObject ReadTable(JsonElement elem)
        {
            var t = new TableObject
            {
                Rows = new List<RowCreation>(),
                CustomRows = new List<string>(),
                Indexes = new List<IndexCreation>(),
                References = new List<ReferenceOptions>()
            };

            if (elem.TryGetProperty("tableName", out var tn)) t.TableName = tn.GetString();
            if (elem.TryGetProperty("description", out var d)) t.Description = d.GetString();
            if (elem.TryGetProperty("schemaName", out var s)) t.SchemaName = s.GetString();

            if (elem.TryGetProperty("rows", out var rows))
            {
                foreach (var r in rows.EnumerateArray())
                    t.Rows.Add(ReadRow(r));
            }

            if (elem.TryGetProperty("customRows", out var crs))
            {
                foreach (var cr in crs.EnumerateArray())
                    t.CustomRows.Add(cr.GetString());
            }

            if (elem.TryGetProperty("indexes", out var idxs))
            {
                foreach (var idx in idxs.EnumerateArray())
                    t.Indexes.Add(ReadIndex(idx));
            }

            if (elem.TryGetProperty("references", out var refs))
            {
                foreach (var rf in refs.EnumerateArray())
                    t.References.Add(ReadReference(rf));
            }

            return t;
        }

        public static void WriteRow(Utf8JsonWriter writer, RowCreation row)
        {
            writer.WriteStartObject();
            writer.WriteString("name", row.Name);
            writer.WriteString("description", row.Description);
            writer.WriteBoolean("encryptedAndNOTMedia", row.EncryptedAndNOTMedia ?? false);
            writer.WriteBoolean("media", row.Media ?? false);
            writer.WriteString("rowType", row.RowType?.ToString());
            writer.WriteNumber("limit", row.Limit ?? 0);
            writer.WriteBoolean("isArray", row.IsArray ?? false);
            writer.WriteString("arrayLimit", row.ArrayLimit);
            writer.WriteBoolean("isPrimary", row.IsPrimary ?? false);
            writer.WriteBoolean("isUnique", row.IsUnique ?? false);
            writer.WriteBoolean("isNotNull", row.IsNotNull ?? false);
            writer.WriteString("defaultValue", row.DefaultValue);
            writer.WriteBoolean("defaultIsPostgresFunction", row.DefaultIsPostgresFunction ?? false);
            writer.WriteString("check", row.Check);
            writer.WriteEndObject();
        }

        public static RowCreation ReadRow(JsonElement elem)
        {
            var row = new RowCreation();
            if (elem.TryGetProperty("name", out var n)) row.Name = n.GetString();
            if (elem.TryGetProperty("description", out var d)) row.Description = d.GetString();
            if (elem.TryGetProperty("encryptedAndNOTMedia", out var e)) row.EncryptedAndNOTMedia = e.GetBoolean();
            if (elem.TryGetProperty("media", out var m)) row.Media = m.GetBoolean();
            if (elem.TryGetProperty("rowType", out var rt)) row.RowType = Enum.TryParse(rt.GetString(), out DBDesigner.PostgresType type) ? type : null;
            if (elem.TryGetProperty("limit", out var l)) row.Limit = l.GetInt32();
            if (elem.TryGetProperty("isArray", out var a)) row.IsArray = a.GetBoolean();
            if (elem.TryGetProperty("arrayLimit", out var al)) row.ArrayLimit = al.GetString();
            if (elem.TryGetProperty("isPrimary", out var p)) row.IsPrimary = p.GetBoolean();
            if (elem.TryGetProperty("isUnique", out var u)) row.IsUnique = u.GetBoolean();
            if (elem.TryGetProperty("isNotNull", out var nn)) row.IsNotNull = nn.GetBoolean();
            if (elem.TryGetProperty("defaultValue", out var dv)) row.DefaultValue = dv.GetString();
            if (elem.TryGetProperty("defaultIsPostgresFunction", out var pf)) row.DefaultIsPostgresFunction = pf.GetBoolean();
            if (elem.TryGetProperty("check", out var c)) row.Check = c.GetString();
            return row;
        }

        public static void WriteIndex(Utf8JsonWriter writer, IndexCreation index)
        {
            writer.WriteStartObject();
            writer.WriteString("tableName", index.TableName);

            writer.WriteStartArray("columnNames");
            if (index.ColumnNames != null)
            {
                foreach (var col in index.ColumnNames)
                    writer.WriteStringValue(col);
            }
            writer.WriteEndArray();

            writer.WriteString("indexType", index.IndexType);
            if (!string.IsNullOrEmpty(index.Condition)) writer.WriteString("condition", index.Condition);
            if (!string.IsNullOrEmpty(index.Expression)) writer.WriteString("expression", index.Expression);
            if (!string.IsNullOrEmpty(index.IndexTypeCustom)) writer.WriteString("indexTypeCustom", index.IndexTypeCustom);
            if (!string.IsNullOrEmpty(index.IndexName)) writer.WriteString("indexName", index.IndexName);
            if (index.UseJsonbPathOps.HasValue) writer.WriteBoolean("useJsonbPathOps", index.UseJsonbPathOps.Value);
            writer.WriteEndObject();
        }

        public static IndexCreation ReadIndex(JsonElement elem)
        {
            var index = new IndexCreation();
            if (elem.TryGetProperty("tableName", out var tn)) index.TableName = tn.GetString();

            if (elem.TryGetProperty("columnNames", out var cols) && cols.ValueKind == JsonValueKind.Array)
            {
                index.ColumnNames = new List<string>();
                foreach (var c in cols.EnumerateArray())
                    index.ColumnNames.Add(c.GetString());
            }

            if (elem.TryGetProperty("indexType", out var it)) index.IndexType = it.GetString();
            if (elem.TryGetProperty("condition", out var cond)) index.Condition = cond.GetString();
            if (elem.TryGetProperty("expression", out var expr)) index.Expression = expr.GetString();
            if (elem.TryGetProperty("indexTypeCustom", out var itc)) index.IndexTypeCustom = itc.GetString();
            if (elem.TryGetProperty("indexName", out var iname)) index.IndexName = iname.GetString();
            if (elem.TryGetProperty("useJsonbPathOps", out var jpo)) index.UseJsonbPathOps = jpo.GetBoolean();

            return index;
        }
        public static void WriteReference(Utf8JsonWriter writer, ReferenceOptions reference)
        {
            writer.WriteStartObject();
            writer.WriteString("mainTable", reference.MainTable);
            writer.WriteString("refTable", reference.RefTable);
            writer.WriteString("foreignKey", reference.ForeignKey);
            writer.WriteString("refTableKey", reference.RefTableKey);
            writer.WriteString("onDeleteAction", reference.OnDeleteAction.ToString());
            writer.WriteString("onUpdateAction", reference.OnUpdateAction.ToString());
            writer.WriteEndObject();
        }

        public static ReferenceOptions ReadReference(JsonElement elem)
        {
            var reference = new ReferenceOptions();
            if (elem.TryGetProperty("mainTable", out var mt)) reference.MainTable = mt.GetString();
            if (elem.TryGetProperty("refTable", out var rt)) reference.RefTable = rt.GetString();
            if (elem.TryGetProperty("foreignKey", out var fk)) reference.ForeignKey = fk.GetString();
            if (elem.TryGetProperty("refTableKey", out var rtk)) reference.RefTableKey = rtk.GetString();

            if (elem.TryGetProperty("onDeleteAction", out var oda) &&
                Enum.TryParse(oda.GetString(), out Reference.ReferentialAction deleteAction))
                reference.OnDeleteAction = deleteAction;

            if (elem.TryGetProperty("onUpdateAction", out var oua) &&
                Enum.TryParse(oua.GetString(), out Reference.ReferentialAction updateAction))
                reference.OnUpdateAction = updateAction;

            return reference;
        }



        public static void WriteCoords(Utf8JsonWriter writer, Coords coords)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", coords.X);
            writer.WriteNumber("y", coords.Y);
            writer.WriteBoolean("isEnabled", coords.IsEnabled);
            writer.WriteString("customLogic", coords.CustomLogic);
            writer.WriteEndObject();
        }

        public static Coords ReadCoords(JsonElement elem)
        {
            var c = new Coords();
            if (elem.TryGetProperty("x", out var x)) c.X = x.GetInt32();
            if (elem.TryGetProperty("y", out var y)) c.Y = y.GetInt32();
            if (elem.TryGetProperty("isEnabled", out var e)) c.IsEnabled = e.GetBoolean();
            if (elem.TryGetProperty("customLogic", out var cl)) c.CustomLogic = cl.GetString();
            return c;
        }

        #endregion









        internal SecureData SeshUsername;
        internal SecureData SeshDirectory;
        internal SecureData Password;
        internal ConnectedSessionReturn SessionReturn;
        public string? ProjectName = null;
        private bool isDragging = false;
        private Point lastPointerPosition;
        private Point pointerOffset;
        private Point targetPosition;
        private DispatcherTimer timer;
        private UIElement draggingElement;
        private const int MaxZIndexBeforeReset = 100;
        private int currentZIndex = 1;
        private int BottomBarIndex = 0;

        public static event EventHandler Tables_Changed;
            
        private static readonly ITrie _trie = new Trie();
        public static ITrie trie => _trie;

        private bool isInitialLoad = true;


        public class ProjectsPreviews
        {
            public string SessionName;
            public string SessionDescription;
            public string LastEdited;
            public string? SessionLogoPath;

            // Constructor
            public ProjectsPreviews(string sessionName, string sessionDescription, string lastEdited, string? sessionLogoPath = null)
            {
                SessionName = sessionName;
                SessionDescription = sessionDescription;
                LastEdited = lastEdited;
                SessionLogoPath = sessionLogoPath;
            }

            public ProjectsPreviews() { }

            // Manual serialize
            public override string ToString()
            {
                // Escape '|' in user data to avoid breaking the format
                static string E(string? s) => s?.Replace("|", "\\|") ?? "";

                return $"{E(SessionName)}|{E(SessionDescription)}|{E(LastEdited)}|{E(SessionLogoPath)}";
            }

            // Manual deserialize
            public static ProjectsPreviews FromString(string data)
            {
                var parts = new List<string>();
                var current = new System.Text.StringBuilder();
                bool escape = false;

                foreach (char c in data)
                {
                    if (escape)
                    {
                        current.Append(c);
                        escape = false;
                    }
                    else if (c == '\\')
                    {
                        escape = true;
                    }
                    else if (c == '|')
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                parts.Add(current.ToString());

                return new ProjectsPreviews(
                    parts.ElementAtOrDefault(0) ?? "",
                    parts.ElementAtOrDefault(1) ?? "",
                    parts.ElementAtOrDefault(2) ?? "",
                    string.IsNullOrEmpty(parts.ElementAtOrDefault(3)) ? null : parts.ElementAtOrDefault(3)
                );
            }
        }

        public void SetProjectImg()
        {
            //In the future will be used to customize project graphics, although there IS the theme system
        }
        //Get all previews
        //Update preview



        internal static async Task OnTables_Changed(MainPage mainPage)
        {
            Tables_Changed?.Invoke(null, EventArgs.Empty);

            //We auto save!

            if (mainPage.ProjectName != null)
            {
                var CurrentDirectory = Path.Combine(mainPage.SeshDirectory.ConvertToString(), mainPage.SeshUsername.ConvertToString(), "Projects", mainPage.ProjectName);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string sessionAsString = mainPage.ToSessionString(mainPage.MainSessionInfo, mainPage.Password);

                // Encrypt before saving
                var aesEncrypted = await AsyncAESEncryption.EncryptAsync(sessionAsString, mainPage.Password);

                var aesString = aesEncrypted.ToString();

                var aesBytes = Encoding.UTF8.GetBytes(aesString);


                var securedJson = Convert.ToBase64String(aesBytes);

                // Send to server


                var createUrl = $"{mainPage.baseUrl}File/Create";

                // Prepare payload
                var payload = new
                {
                    Directory = CurrentDirectory,
                    FileName = mainPage.ProjectName,
                    Extension = "secdbdesign",
                    Content = securedJson
                };

                // Serialize to JSON
                var json2 = JsonSerializer.Serialize(payload);
                var content = new StringContent(json2, Encoding.UTF8, "application/json");

                // Save (overwrite if exists)
                var createdResponse = await mainPage.DBDesignerClient.PostAsync(createUrl, content);
                createdResponse.EnsureSuccessStatusCode();


                //Also create/update file with basic info

                

                var Preview = new ProjectsPreviews(mainPage.MainSessionInfo.SessionName, mainPage.MainSessionInfo.SessionDescription, DateTime.UtcNow.ToString(), null);

                var ProjectPreviewData = Preview.ToString();


                var previewPayload = new
                {
                    Directory = CurrentDirectory,
                    FileName = mainPage.ProjectName,
                    Extension = "preview",
                    Content = ProjectPreviewData
                };

                var json3 = JsonSerializer.Serialize(previewPayload);
                var content2 = new StringContent(json3, Encoding.UTF8, "application/json");


                // Save (overwrite if exists)
                var previewUpdateResponse = await mainPage.DBDesignerClient.PostAsync(createUrl, content2);
                previewUpdateResponse.EnsureSuccessStatusCode();


            }

        }




                    public bool mpSpawned = false;
        public bool isShuffle = false;
        public List<string> defaultSongs = new List<string>();
        public List<string> customSongs = new List<string>();
        public List<string> currentPlaylist;
        public bool BGMLogicAdded = false;
        public MainPage()
        {

            this.InitializeComponent();

            if (SeshUsername != null && SeshDirectory != null && SessionReturn != null)
            {
                LogoutButton.Visibility = Visibility.Visible;
            }
            else
            {
                LogoutButton.Visibility = Visibility.Collapsed;
            }


            BtnWelcome.Click += (_, __) => Open("https://walker-industries-rnd.github.io/Database-Designer/welcome.html");
            BtnGeneralSetup.Click += (_, __) => Open("https://walker-industries-rnd.github.io/Database-Designer/tutorials-for-users/1.-general-setup.html");
            BtnLoginMusic.Click += (_, __) => Open("https://walker-industries-rnd.github.io/Database-Designer/tutorials-for-users/2.-logging-in-and-music.html");
            BtnFirstProject.Click += (_, __) => Open("https://walker-industries-rnd.github.io/Database-Designer/tutorials-for-users/3.-creating-your-first-project.html");
            BtnCustomizeProject.Click += (_, __) => Open("https://walker-industries-rnd.github.io/Database-Designer/tutorials-for-users/4.-customizing-your-project.html");
            BtnFirstTable.Click += (_, __) => Open("https://walker-industries-rnd.github.io/Database-Designer/tutorials-for-users/5.-creating-your-first-table-and-using-the-table-viewer.html");
            BtnAddData.Click += (_, __) => Open("https://walker-industries-rnd.github.io/Database-Designer/tutorials-for-users/6.-adding-data-to-your-first-table.html");
            BtnExporting.Click += (_, __) => Open("https://walker-industries-rnd.github.io/Database-Designer/tutorials-for-users/7.-exporting-your-project-and-creating-templates.html");
            BtnBestPractices.Click += (_, __) => Open("https://walker-industries-rnd.github.io/Database-Designer/tutorials-for-users/8.-best-practices.html");
            Btn3NF.Click += (_, __) => Open("https://walker-industries-rnd.github.io/Database-Designer/tutorials-for-users/9.-understanding-3nf.html");


            void Open(string url)
            {
                HtmlPage.Window.Navigate(new Uri(url), "_blank");
            }


            //Randomly play one of 3 songs at start
            List<string> defaultPaths = new List<string>
            {
                "Assets/Sounds/Album/Snow Covered Memories.mp3",
                "Assets/Sounds/Album/Memory Shaped Coffin - Sin Covered Longing.mp3",
                "Assets/Sounds/Album/Sanguine Cathedral.mp3",
                "Assets/Sounds/Album/The Loss of Personification.mp3",
            };


            BGM.Volume = 0.2;
            BGM.DataContext = BGM;
            BGM.IsLooping = true;

            var randomSong = new Random();

            var selectedSong = new Uri(defaultPaths[randomSong.Next(0, defaultPaths.Count)], UriKind.Relative);

            BGM.Stop();
            BGM.Source = selectedSong;
            BGM.AutoPlay = true;

            Console.WriteLine(selectedSong);




            this.KeyDown += MainPage_KeyDown;


            string exePath = Path.Combine(
               AppDomain.CurrentDomain.BaseDirectory,
               "Assets", "PariahAPI", "Pariah API.exe"
           );

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            var apiProcess = Process.Start(startInfo);

            if (apiProcess == null)
                throw new Exception("Failed to start process.");

            string apiIp = string.Empty;
            var regex = new Regex(@"Now listening on: (http://.+)");

            // Read output synchronously
            while (!apiProcess.StandardOutput.EndOfStream)
            {
                string? line = apiProcess.StandardOutput.ReadLine();
                if (line == null) continue;

                var match = regex.Match(line);
                if (match.Success)
                {
                    apiIp = match.Groups[1].Value;
                    baseUrl = apiIp + "/";

                    // OStop reading once found
                    break;
                }
            }












            WindowsHolder = IntroPage;


            EventManager.RegisterClassHandler(
          typeof(Button),
           Button.ClickEvent,
          new RoutedEventHandler(ButtonClickSFX)
            );


            SetProjectImg();


            void ButtonClickSFX(object sender, RoutedEventArgs e)
            {
                if (sender is Button btn)
                {
                    string text = btn.Content?.ToString() ?? "";

                    // Default click sound
                    string soundPath = "Assets/Sounds/SFX/ButtonClick.mp3";

                    // Special sounds based on button content
                    if (text == "X" || text == "x")
                        soundPath = "Assets/Sounds/SFX/SFX3.mp3";
                    else if (text.Contains("Finalize"))
                        soundPath = "Assets/Sounds/SFX/Build.mp3";

                    // Optional: override for buttons inside AppList
                    var parent = btn.Parent;
                    while (parent != null)
                    {
                        if (parent is StackPanel sp && sp.Name == "AppList")
                        {
                            soundPath = "";
                            break;
                        }
                        parent = (parent as FrameworkElement)?.Parent;
                    }


                    // Play the SFX
                    SFX.Stop();
                    SFX.Source = new Uri(soundPath, UriKind.Relative);
                    SFX.Play();
                }
            }
                ;



            DateTime lastKeySound = DateTime.MinValue;
            Random rnd = new Random();


            EventManager.RegisterClassHandler(
                typeof(TextBox),
                TextBox.TextInputEvent,
                new TextCompositionEventHandler(TypingSFX_TextBox)
            );

            EventManager.RegisterClassHandler(
                typeof(PasswordBox),
                PasswordBox.TextInputUpdateEvent,
                new RoutedEventHandler(TypingSFX_PasswordBox)
            );

            void TypingSFX_TextBox(object sender, TextCompositionEventArgs e) => PlaySFX();
            void TypingSFX_PasswordBox(object sender, RoutedEventArgs e) => PlaySFX();

            void PlaySFX()
            {

                List<string> keyboardSounds = new List<string>
                {
                    "Assets/Sounds/SFX/SFX4.mp3",
                    "Assets/Sounds/SFX/SFX5.mp3",
                    "Assets/Sounds/SFX/SFX6.mp3",
                    "Assets/Sounds/SFX/SFX8.mp3"
                };
                if ((DateTime.Now - lastKeySound).TotalMilliseconds < 50) return;
                lastKeySound = DateTime.Now;

                SFX.Stop();

                int index = rnd.Next(keyboardSounds.Count);
                SFX.Source = new Uri(keyboardSounds[index], UriKind.Relative);
                SFX.Play();
            }

            //Yo I am NOT organizing ts



            MainBG.Source = new BitmapImage(new Uri("Assets/Images/DBDesignerBG.png", UriKind.Relative));

            MusicPlayer.Click += (s, e) =>
            {
                if (mpSpawned)
                {

                }
                else
                {
                    CreateWindow(() => new MusicPlayer(this), "Music Player", true);
                    mpSpawned = true;
                }

            };

            SetImgVolume();

            UpdateVolText();

            BGM.DataContextChanged += (s, e) =>
            {
                SetImgVolume();
            };

            void SetImgVolume()
            {
                string vol0 = "Assets/Images/VolumeUI/Vol0.png";
                string vol1 = "Assets/Images/VolumeUI/Vol1.png";
                string vol2 = "Assets/Images/VolumeUI/Vol2.png";
                string vol3 = "Assets/Images/VolumeUI/Vol3.png";

                int fixedVal = (int)Math.Round(BGM.Volume * 100);

                string targetImg = vol0;

                if (fixedVal >= 1 && fixedVal <= 33)
                    targetImg = vol1;
                else if (fixedVal >= 34 && fixedVal <= 67)
                    targetImg = vol2;
                else if (fixedVal >= 68)
                    targetImg = vol3;


                // Only update if different
                if (VolumeImg1.Source is BitmapImage bmp1)
                {
                    if (bmp1.UriSource.ToString() != targetImg)
                        VolumeImg1.Source = new BitmapImage(new Uri(targetImg, UriKind.Relative));
                }
                else
                {
                    VolumeImg1.Source = new BitmapImage(new Uri(targetImg, UriKind.Relative));
                }

                if (VolumeImg2.Source is BitmapImage bmp2)
                {
                    if (bmp2.UriSource.ToString() != targetImg)
                        VolumeImg2.Source = new BitmapImage(new Uri(targetImg, UriKind.Relative));
                }
                else
                {
                    VolumeImg2.Source = new BitmapImage(new Uri(targetImg, UriKind.Relative));
                }
            }

            void UpdateVolText()
            {
                VolText.Text = $"VOL: {Math.Round(BGM.Volume * 100)}";
            }




            MuteBtn.Click += (s, e) =>
            {
                VolSlider.Value = 0;
                VolText.Text = "0";
            };

            VolSlider.Value = BGM.Volume * 100;

            VolSlider.ValueChanged += (s, e) =>
            {
                SetImgVolume();
                UpdateVolText();
                BGM.Volume = (double)VolSlider.Value / 100.0;


            };

            VolOption.Visibility = Visibility.Collapsed;

            VolOptionToggle.Click += (s, e) =>
            {
                if (VolOption.Visibility == Visibility.Visible)
                {
                    VolOption.Visibility = Visibility.Collapsed;
                }
                else
                {
                    VolOption.Visibility = Visibility.Visible;
                }
            };




            MainMenu.Visibility = Visibility.Collapsed;
            List<(string Display, Action OnClick)> searchableItems = new List<(string, Action)>();

            MainButton.Click += (s, e) =>
            {

                if (MainMenu.Visibility == Visibility.Visible)
                {
                    MainMenu.Visibility = Visibility.Collapsed;
                }

                else
                {
                    MainMenu.Visibility = Visibility.Visible;

                    GenerateSearchableItems();
                    HandleItemsList();
                }

            };

            SearchBar.PlaceholderText = "Type Here To Search";


            SearchBar.TextChanged += (s, e) =>
            {
                MainMenu.Visibility = Visibility.Visible;

                GenerateSearchableItems();
                HandleItemsList();
            };


            //Eventually i'll put GenerateSearchableItems on a table changed event but for now it works

            void GenerateSearchableItems()
            {
                searchableItems.Clear();

                if (MainSessionInfo.Tables == null) return;

                foreach (var item in MainSessionInfo.Tables)
                {
                    // Rows
                    if (item.Rows != null)
                    {
                        foreach (var row in item.Rows)
                        {
                            string display = $"(Row) {row.Name} in {item.SchemaName}.{item.TableName}";
                            Action onClick = () => CreateWindow(() => new DatabaseViewer(this, $"{item.SchemaName}.{item.TableName}"), "Database Viewer", true);
                            searchableItems.Add((display, onClick));
                        }
                    }

                    // Indexes
                    if (item.Indexes != null)
                    {
                        foreach (var index in item.Indexes)
                        {
                            string display = $"(Index) {index.IndexName} in {item.SchemaName}.{item.TableName}";
                            Action onClick = () => CreateWindow(() => new DatabaseViewer(this, $"{item.SchemaName}.{item.TableName}"), "Database Viewer", true);
                            searchableItems.Add((display, onClick));
                        }
                    }

                    // References
                    if (item.References != null)
                    {
                        foreach (var refr in item.References)
                        {
                            string display = $"(Ref) {refr.MainTable}/{refr.ForeignKey} in {item.SchemaName}.{item.TableName}";
                            Action onClick = () => CreateWindow(() => new DatabaseViewer(this, $"{item.SchemaName}.{item.TableName}"), "Database Viewer", true);
                            searchableItems.Add((display, onClick));
                        }
                    }
                }
            }


            //In future remove children.clear, do visibility instead for smoothness
            void HandleItemsList()
            {
                ItemsCollection.Children.Clear();

                string searchTerm = SearchBar.Text.Trim();
                var searchWords = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var item in searchableItems)
                {

                    if (searchWords.All(w => item.Display.Contains(w, StringComparison.OrdinalIgnoreCase)))
                    {
                        var newBtn = CreateExactButton(item.Display);
                        newBtn.Click += (s, e) => item.OnClick();
                        ItemsCollection.Children.Add(newBtn);
                    }
                }
            }


            Button CreateExactButton(string Value)
            {
                var button = new Button
                {
                    Height = 39,
                    Padding = new Thickness(10, 0, 10, 0),
                    Background = new SolidColorBrush(Color.FromArgb(0x4C, 0x8F, 0x8F, 0x8F)),
                    Margin = new Thickness(0, 10, 0, 0),
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-ExtraLight.ttf")
                };

                var stack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = 294
                };

                var image = new Image
                {
                    Source = new BitmapImage(
                        new Uri("/Database_Designer;component/assets/images/window.png", UriKind.Relative)),
                    Width = 20,
                    Height = 20,
                    Margin = new Thickness(10, 0, 0, 0)
                };

                var textBlock = new TextBlock
                {
                    Text = Value,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = 250,
                    Margin = new Thickness(6, 0, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };

                stack.Children.Add(image);
                stack.Children.Add(textBlock);
                button.Content = stack;

                return button;
            }



            LogoutButton.Click += (s, e) =>
            {
                CreateWindow(() => new LogoutConfirm(this), "Logging Out...", true);
            };




            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            timer.Tick += OnTimerTick;
            targetPosition = new Point();


            MainSessionInfo = new DBDesignerSession();
            MainSessionInfo.Tables = new ObservableCollection<SessionStorage.TableObject>();

            CreateInitialScreen();
            var introMessage = new List<(string Text, MiraMiniPopup.MiraStates Expression)>
{
    ("Screen too small? Hold CTRL and scroll (or use _ and +) to zoom in or out.", MiraMiniPopup.MiraStates.Ummm),
    ("If you were a tester for Database Designer, please delete the storage file!", MiraMiniPopup.MiraStates.Happy),
    ("...", MiraMiniPopup.MiraStates.Neutral),
    ($"It's somewhere like: Database Designer\\Database_Designer.Photino\\bin\\Release\\net9.0\\win-x64\\DatabaseDesignerData", MiraMiniPopup.MiraStates.Neutral),
    ("THIS IS HARD, OKAY?!", MiraMiniPopup.MiraStates.Angry),
    ("GODDAMN, YOU THINK THIS WAS MADE IN A DAY?!", MiraMiniPopup.MiraStates.Angry),
    ("I mean, at least you won’t have to nuke the folder anymore… probably.", MiraMiniPopup.MiraStates.Neutral),
    ("…Oh Yeah! Before I forget,", MiraMiniPopup.MiraStates.Ummm),
    ("(Offscreen) WELCOME TO DATABASE DESIGNER", MiraMiniPopup.MiraStates.Ummm),
    ("...", MiraMiniPopup.MiraStates.Neutral),
    ("YO MASUKI I'M ACTUALLY GONNA GET YOU WHEN I CATCH YOU", MiraMiniPopup.MiraStates.Angry),
    ("Anyways, welcome to Database Designer.", MiraMiniPopup.MiraStates.Neutral),
    ("You won't see too much of me because my -", MiraMiniPopup.MiraStates.Neutral),
    ("LAZY -", MiraMiniPopup.MiraStates.Angry),
    ("GOOD FOR NOTHING -", MiraMiniPopup.MiraStates.Angry),
    ("FREELOADING -", MiraMiniPopup.MiraStates.Angry),
    ("manager hasn't given me too much to do, but that will change next update.", MiraMiniPopup.MiraStates.Neutral),
    ("...", MiraMiniPopup.MiraStates.Neutral),
    ("...", MiraMiniPopup.MiraStates.Neutral),
    ("...", MiraMiniPopup.MiraStates.Neutral),
    ("...", MiraMiniPopup.MiraStates.Neutral),
    ("... I'm gonna go now. (Weirdo)", MiraMiniPopup.MiraStates.Neutral),



};

            CreateWindow(
                () => new MiraMiniPopup(introMessage, this),
                "Mira",
                true,
                null,
                600,
                600
            );

            AddTableUpdater();


        }
        
        //Block devtool stuff
        private void MainPage_KeyDown(object sender, KeyEventArgs e)
        {
            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool isAlt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

            // Ctrl+R (refresh)
            if (isCtrl && e.Key == Key.R)
            {
                e.Handled = true;
                return;
            }

            // Ctrl+P (print)
            if (isCtrl && e.Key == Key.P)
            {
                e.Handled = true;
                return;
            }

            // Ctrl+U (view source)
            if (isCtrl && e.Key == Key.U)
            {
                e.Handled = true;
                return;
            }

            // F12
            if (e.Key == Key.F12)
            {
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+I/J/C/K (common dev tools)
            if (isCtrl && isShift && (e.Key == Key.I || e.Key == Key.J || e.Key == Key.C || e.Key == Key.K))
            {
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+P (Chrome command menu)
            if (isCtrl && isShift && e.Key == Key.P)
            {
                e.Handled = true;
                return;
            }

            // Ctrl+L or F6 (focus address bar)
            if (isCtrl && e.Key == Key.L)
            {
                e.Handled = true;
                return;
            }
        }

        void Tables_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            trie.Clear();


            foreach (var table in MainSessionInfo.Tables)
            {
                List<string> pathParts = new List<string>();

                if (!string.IsNullOrEmpty(table.SchemaName))
                {
                    string schema = table.SchemaName.Trim();

                    // Remove *all surrounding quotes* if present
                    if ((schema.StartsWith("\"") && schema.EndsWith("\"")) ||
                        (schema.StartsWith("'") && schema.EndsWith("'")))
                    {
                        schema = schema.Substring(1, schema.Length - 2);
                    }

                    pathParts.AddRange(schema.Split('.'));
                }

                pathParts.Add(table.TableName);

                // Build hierarchical paths
                string currentPath = "";
                for (int i = 0; i < pathParts.Count; i++)
                {
                    currentPath = i == 0 ? pathParts[i] : currentPath + "." + pathParts[i];
                    trie.AddWord(currentPath);
                }
            }


            _ = Task.Run(() => OnTables_Changed(this));

            LoadProject(ProjectName);



        }


        public void RemoveTableUpdater()
        {
            MainSessionInfo.Tables.CollectionChanged -= Tables_CollectionChanged;
        }

        public void AddTableUpdater()
        {
            MainSessionInfo.Tables.CollectionChanged += Tables_CollectionChanged;
        }

        public void ForceCollectionChangeUpate()
        {
            trie.Clear();

            foreach (var table in MainSessionInfo.Tables)
            {
                List<string> pathParts = new List<string>();

                if (!string.IsNullOrEmpty(table.SchemaName))
                {
                    string schema = table.SchemaName.Trim();

                    // Remove *all surrounding quotes* if present
                    if ((schema.StartsWith("\"") && schema.EndsWith("\"")) ||
                        (schema.StartsWith("'") && schema.EndsWith("'")))
                    {
                        schema = schema.Substring(1, schema.Length - 2);
                    }

                    pathParts.AddRange(schema.Split('.'));
                }

                pathParts.Add(table.TableName);

                // Build hierarchical paths
                string currentPath = "";
                for (int i = 0; i < pathParts.Count; i++)
                {
                    currentPath = i == 0 ? pathParts[i] : currentPath + "." + pathParts[i];
                    trie.AddWord(currentPath);
                }
            }


            _ = Task.Run(() => OnTables_Changed(this));

            LoadProject(ProjectName);


        }



        public Button CreateCustomButton(string ImgPath)
        {
            // Create the Button
            var button = new Button
            {
                Width = 88,
                Background = new SolidColorBrush(Color.FromArgb(0x0A, 0x55, 0x55, 0x55)),
                Margin = new Thickness(6, 0, 6, 0)
            };

            // Create and set the Image
            var image = new Image
            {
                Source = new BitmapImage(new Uri(ImgPath, UriKind.Relative)),
                Stretch = Stretch.Uniform
            };
            button.Content = image;

            // Apply the CornerRadius style override for inner Border
            var borderStyle = new Style(typeof(Border));
            borderStyle.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(0)));
            button.Resources.Add(typeof(Border), borderStyle);

            LowerAppBar.Children.Add(button);
            return button;
        }

        // List of apps with Name and IconPath only
        Dictionary<string, string> AppIcon = new Dictionary<string, string>
{
    { "Designer Form", "Assets/Images/Logos/BasicDatabaseDesigner.png" },
    { "Control Panel", "Assets/Images/Logos/ControlPanel.png" },
    { "Create Table - Template Selection", "Assets/Images/Logos/CreateTableTemplate.png" },
    { "Database Composition", "Assets/Images/Logos/DatabaseComposition.png" },
    { "About", "Assets/Images/BlackLogo.png" },
        { "About2", "Assets/Images/WhiteLogo.png" },
    { "Database Templates", "Assets/Images/Logos/DatabaseTemplates.png" },
    { "Database Viewer", "Assets/Images/Logos/DatabaseViewer.png" },
    { "FAQ", "Assets/Images/Logos/FAQ.png" },
    { "Table Editor", "Assets/Images/Logos/FolderViewer.png" },
    { "Login", "Assets/Images/Logos/Login.png" },
    { "Logging Out...", "Assets/Images/Logos/LogoutConfirm.png" },
    { "Designer Options", "Assets/Images/Logos/MainDesigner.png" },
    { "Mira", "Assets/Images/Logos/MiraMiniPopupLogo.png" },
    { "Projects", "Assets/Images/Logos/Projects.png" },
    { "Roadmap", "Assets/Images/Logos/Templates(MainDesigner).png" },
    { "Folder", "Assets/Images/Logos/Folder.png" },
    { "Create New Table", "Assets/Images/Logos/MainDesigner.png" },
    { "General Project", "Assets/Images/Logos/ControlPanel.png" },
        { "Edit Project", "Assets/Images/Logos/EditProject.png" },
    { "Music Player", "Assets/Images/VolumeUI/Music.png" },



    
    { "Build Project", "Assets/Images/Logos/Login.png" },


};




        public void MirrorParentVisibility(FrameworkElement child, FrameworkElement parent)
        {
            if (child == null || parent == null)
                return;
            void UpdateChildVisibility(object sender, DependencyPropertyChangedEventArgs e)
            {
                child.Visibility = parent.Visibility;
            }
            // Initial sync
            child.Visibility = parent.Visibility;
            // Subscribe to parent's IsVisibleChanged event
            parent.IsVisibleChanged += (s, e) => UpdateChildVisibility(s, null);

            //If the parent is destroyed, destroy the child
            parent.Unloaded += (s, e) =>
            {
                if (child.Parent is Panel panel)
                {
                    panel.Children.Remove(child);
                }
            };
        }


        public UIElement CreateWindow(
       Func<UIElement> createControl,
       string tag,
       bool appearsInBottom,
       Panel hostPanel = null,
       double? width = null,
       double? height = null)
        {
            // Default host to IntroPage if none provided
            if (hostPanel == null) hostPanel = IntroPage;
            if (createControl == null) throw new ArgumentNullException(nameof(createControl));

            var control = createControl();

            if (control is FrameworkElement element)
            {
                element.Margin = new Thickness(0);

                // Apply optional size if provided
                if (width.HasValue) element.Width = width.Value;
                if (height.HasValue) element.Height = height.Value;

                // Internal helper to perform the centering math
                void UpdatePosition()
                {
                    // We use hostPanel instead of IntroPage for flexibility
                    double hostW = hostPanel.ActualWidth;
                    double hostH = hostPanel.ActualHeight;
                    double elemW = element.ActualWidth;
                    double elemH = element.ActualHeight;

                    // Only position if we have valid dimensions to avoid jumping
                    if (hostW > 0 && hostH > 0 && elemW > 0 && elemH > 0)
                    {
                        Canvas.SetLeft(element, (hostW - elemW) / 2);
                        Canvas.SetTop(element, (hostH - elemH) / 2);
                    }
                }

                // Fix 1: Handle dynamic size changes (Auto width/height or content updates)
                element.SizeChanged += (s, e) => UpdatePosition();

                // Fix 2: Handle host panel resizing (e.g., browser window resize)
                hostPanel.SizeChanged += (s, e) => UpdatePosition();

                // Fix 3: Initial positioning trigger
                element.Loaded += (s, e) => UpdatePosition();

                // Dragging logic (Assumed existing)
                element.MouseLeftButtonDown += MiraH_MouseLeftButtonDown;
                element.MouseMove += MiraH_MouseMove;
                element.MouseLeftButtonUp += MiraH_MouseLeftButtonUp;
            }

            // Add to the visual tree if not already added
            if (!hostPanel.Children.Contains(control))
            {
                hostPanel.Children.Add(control);
            }



            BringToFront(control);

            // Shortcut setup (only if visible)
            Button shortcutBtn = null;

            if (LowBar.Visibility == Visibility.Visible || LowerAppBar.Visibility == Visibility.Visible)
            {
                // Pick image to use
                string usedImg = (AppIcon != null && AppIcon.ContainsKey(tag)) ? AppIcon[tag] : "Assets/Images/MiraGraphic1.png";

                shortcutBtn = CreateCustomButton(usedImg);

                if (!LowerAppBar.Children.Contains(shortcutBtn))
                    LowerAppBar.Children.Add(shortcutBtn);

                // Toggle window visibility on click
                shortcutBtn.Click += (s, e) =>
                {
                    if (control.Visibility == Visibility.Visible)
                    {
                        control.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        control.Visibility = Visibility.Visible;
                        BringToFront(control);
                    }
                    MoveChildToFront(shortcutBtn, LowerAppBar);
                };

                // Double-tap closes window (collapses)
                DateTime lastTap = DateTime.MinValue;
                shortcutBtn.Click += (s, e) =>
                {
                    var now = DateTime.Now;
                    if ((now - lastTap).TotalMilliseconds <= 300)
                    {
                        control.Visibility = Visibility.Collapsed;
                        e.Handled = true;
                    }
                    lastTap = now;
                };

                shortcutBtn.ToolTip = new ToolTip
                {
                    Content = tag,
                    FontSize = 12,
                    Background = new SolidColorBrush(Colors.Black),
                    Foreground = new SolidColorBrush(Colors.White),
                    Padding = new Thickness(4),
                    FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf")
                };
            }


            var type = control.GetType();
            var prop = type.GetProperty("WindowInfo");
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(control, new UIWindowEntry(new List<UIElement> { control }, shortcutBtn));
            }



            return control;
        }




        private void MoveChildToFront(UIElement element, Panel panel)
        {
            if (element == null || panel == null) return;

            // If it's not a child, don't touch it.
            if (!panel.Children.Contains(element)) return;

            // If it's already first, nothing to do.
            if (panel.Children.Count > 0 && panel.Children[0] == element) return;

            // Remove then insert at 0 (moves the same instance).
            panel.Children.Remove(element);
            panel.Children.Insert(0, element);
        }


        public void BringToFront(UIElement element)
        {
            Canvas.SetZIndex(element, currentZIndex++);
        }


        private void UIFrame_Resize(object sender, NavigationEventArgs e, Frame UIFrame)
        {
            if (UIFrame.Content is FrameworkElement content)
            {
                content.Loaded += (s, args) =>
                {
                    UIFrame.Width = content.ActualWidth;
                    UIFrame.Height = content.ActualHeight;
                };
            }
        }

        #region Desktop Elements
        public void CreateInitialScreen()
        {


            var LoginUI = CreateDesktopAppButton("Login", Apps, () => new Login(this), "Login", false, true);
            var AboutUsUI = CreateDesktopAppButton("About", Apps, () => new DatabaseDesigner(this), "About2", false, true);

            DesktopApps.Add("Login", (LoginUI.Item1, LoginUI.Item2));
            DesktopApps.Add("About", (AboutUsUI.Item1, AboutUsUI.Item2));

            

        }

        public (StackPanel, Button) CreateDesktopAppButton(
            string ShortcutText, WrapPanel DesktopScreen, Func<UIElement> createControl, string WindowKey, bool appearsAtBottom, bool WhiteText = false)
        {
            // Create the Button
            var desktopAppButton = new Button
            {
                Name = ShortcutText,
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                Width = 132,
                Height = 162,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10),
                Padding = new Thickness(0)
            };

            // Create StackPanel inside Button
            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 132,
                Height = 162,
                Background = new SolidColorBrush(Color.FromArgb(0x00, 0xE6, 0xE6, 0xE6)),
                IsEnabled = false,
                Visibility = Visibility.Visible
            };


            string usedImg = AppIcon.ContainsKey(WindowKey) ? AppIcon[WindowKey] : "Assets/Images/MiraGraphic1.png";


 



            // Add Image
            var image = new Image
            {
                Source = new BitmapImage(new Uri(usedImg, UriKind.Relative)),
                Width = 92,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Visible,
                IsEnabled = false
            };
            stackPanel.Children.Add(image);

            var color = new SolidColorBrush(Colors.Black);

            if (WhiteText == true)
            {
             color = new SolidColorBrush(Colors.White);

            }


            // Add TextBlock
            var textBlock = new TextBlock
            {
                Text = ShortcutText,
                FontSize = 17,
                TextAlignment = TextAlignment.Center,
                Width = 116,
                Height = 44,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Visibility = Visibility.Visible,
                IsEnabled = false,
                Foreground = new SolidColorBrush(Colors.White)
            };

            // Add black outline using DropShadowEffect
            textBlock.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 15,
                ShadowDepth = 1,
                Opacity = 30
            };
            stackPanel.Children.Add(textBlock);

            // Add StackPanel to Button
            desktopAppButton.Content = stackPanel;
            DesktopScreen.Children.Add(desktopAppButton);

            // Set up click event handler
            desktopAppButton.Click += (s, e) =>
            {

                CreateWindow(createControl, ShortcutText, appearsAtBottom);
            };

            return (stackPanel, desktopAppButton);
        }

        public Func<UIElement> MakePageFactory<T>() where T : UIElement, new()
        {
            return () => new T();
        }

        public void CreateWindows(string ShortcutText, Func<UIElement> createControl, bool appearsAtBottom)
        {
            CreateWindow(createControl, ShortcutText, appearsAtBottom);
        }
        #endregion

        #region Mouse Events (Drag Drop, Z Handling)
        private bool isGridDragging = false;
        private double gridSizeX = 144;
        private double gridSizeY = 176;
        private Thickness gridSpacing = new Thickness(10, 10, 10, 10);

        private void MiraH_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement element)
            {
                // Removed check for DesktopAppShortcut and isGridDragging
                if (sender is Button || sender is TextBox || sender is ComboBox)
                    return;

                isDragging = true;
                draggingElement = element;

                if (currentZIndex >= MaxZIndexBeforeReset)
                {
                    RebalanceZIndexes();
                }

                currentZIndex++;
                // Bring to front
                Canvas.SetZIndex(draggingElement, currentZIndex);

                var transform = draggingElement.RenderTransform as TranslateTransform;
                if (transform == null)
                {
                    transform = new TranslateTransform();
                    draggingElement.RenderTransform = transform;
                }

                var position = e.GetPosition(LayoutRoot);
                pointerOffset = new Point(position.X - transform.X, position.Y - transform.Y);
                lastPointerPosition = position;
                targetPosition.X = transform.X;
                targetPosition.Y = transform.Y;

                draggingElement.CaptureMouse();
                timer.Start();
            }
        }

        private void MiraH_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && draggingElement != null)
            {
                var currentPointerPosition = e.GetPosition(LayoutRoot);
                double x = currentPointerPosition.X - pointerOffset.X;
                double y = currentPointerPosition.Y - pointerOffset.Y;

                // Removed grid snapping logic (isGridDragging block)
                targetPosition.X = x;
                targetPosition.Y = y;
            }
        }

        private void MiraH_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            // Removed isGridDragging reset
            timer.Stop();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (draggingElement == null)
                return;

            var transform = draggingElement.RenderTransform as TranslateTransform;
            if (transform == null)
            {
                transform = new TranslateTransform();
                draggingElement.RenderTransform = transform;
            }

            transform.X += (targetPosition.X - transform.X) * 0.1;
            transform.Y += (targetPosition.Y - transform.Y) * 0.1;
        }

        private void RebalanceZIndexes()
        {
            if (LayoutRoot is Panel panel)
            {
                var elementsWithZ = panel.Children
                    .OfType<UIElement>()
                    .Select(el => new { Element = el, Z = Canvas.GetZIndex(el) })
                    .OrderBy(x => x.Z)
                    .ToList();

                int z = 1;
                foreach (var item in elementsWithZ)
                {
                    Canvas.SetZIndex(item.Element, z++);
                }
                currentZIndex = z;
            }
        }
        #endregion

        public async Task FadeChangeBackground(string newPath, UriKind style, double durationSeconds = 0.5)
        {
            var tcs = new TaskCompletionSource<bool>();

            // Fade out
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(durationSeconds / 2),
                FillBehavior = FillBehavior.HoldEnd
            };

            fadeOut.Completed += async (s, e) =>
            {
                try
                {
                    if (style == UriKind.Relative)
                    {
                        MainBG.Source = new BitmapImage(new Uri(newPath, UriKind.Relative));
                    }
                    else
                    {
                        // Load the bytes off the UI thread
                        byte[] bytes = await Task.Run(() => File.ReadAllBytes(newPath));

                        // Set the image on the UI thread
                        Dispatcher.BeginInvoke(() =>
                        {
                            try
                            {
                                var bmp = new BitmapImage();
                                using (var ms = new MemoryStream(bytes))
                                {
                                    bmp.SetSource(ms); // OpenSilver-safe for GIFs and images
                                }
                                MainBG.Source = bmp;
                            }
                            catch
                            {
                                MainBG.Source = new BitmapImage(new Uri("Assets/Images/lightbg.png", UriKind.Relative));
                            }
                        });

                    }
                }
                catch
                {
                    MainBG.Source = new BitmapImage(new Uri("Assets/Images/lightbg.png", UriKind.Relative));
                }

                // Fade in
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(durationSeconds / 2),
                    FillBehavior = FillBehavior.HoldEnd
                };

                fadeIn.Completed += (_, __) => tcs.TrySetResult(true);
                MainBG.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };

            MainBG.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            await tcs.Task;
        }



        private void LayoutRoot_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                if (LowBar.Visibility == Visibility.Visible)
                {
                    LowBar.Visibility = Visibility.Collapsed;
                }
                else
                {
                    LowBar.Visibility = Visibility.Visible;
                }
            }

            if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                VolSlider.Value = 0;
                VolText.Text = "0";
                var targetImg = "Assets/Images/VolumeUI/Vol0.png";
                VolumeImg2.Source = new BitmapImage(new Uri(targetImg, UriKind.Relative));
            }

            if (SeshUsername != null && SeshDirectory != null && SessionReturn != null)
            {
                LogoutButton.Visibility = Visibility.Visible;
            }
            else
            {
                LogoutButton.Visibility = Visibility.Collapsed;
            }

        }


        public void UserLoggedIn(SecureData Username, SecureData Pass, SecureData DirectoryPath, ConnectedSessionReturn Sesh, bool resetScreen = false)
        {

            Apps.Children.Clear();

            var filePath = "Assets/Images/lightbg.png";
            UriKind uriKind = UriKind.Relative;

            FadeChangeBackground(filePath, uriKind);


            LowBar.Visibility = Visibility.Visible;

            DesktopApps.TryGetValue("Login", out var loginApp);
            DesktopCanvas.Children.Remove(loginApp.Item1);

            SeshUsername = Username;
            SeshDirectory = DirectoryPath;
            SessionReturn = Sesh;
            Password = Pass;

            if (resetScreen)
            {
                IntroPage.Children.Clear();
            }

            //Check if projects exist, if not initialize; also allow the UI to run for 2 seconds before deleting itself
            SetupDefaultScreen();
            // CreateDesktopAppButton("Basic Database Designer", Apps, () => new BasicDatabaseDesigner(this, null), "Basic Database Designer");
            var introMessage = new List<(string Text, MiraMiniPopup.MiraStates Expression)>
{
    ("I'm Mira; you won't see much of me but in the future you can click Ctrl + M for help!", MiraMiniPopup.MiraStates.Happy),
    ("Database Designer is still in beta, with many features coming out!", MiraMiniPopup.MiraStates.Neutral),
    ("The next major update will have a no-code code creator for example!", MiraMiniPopup.MiraStates.Happy),
    ("On behalf of the team, thank you for trying out Database Designer!", MiraMiniPopup.MiraStates.Happy),
    ("Oh yeah...", MiraMiniPopup.MiraStates.Ummm),
    ("Some UIs may not close properly, you can use the clear screen option under the project settings", MiraMiniPopup.MiraStates.Ummm),
    ("...or as Masuki would say you can just kinda toss them aside!", MiraMiniPopup.MiraStates.Happy),
        ("...", MiraMiniPopup.MiraStates.Ummm),
    ("IT'S EARLY RELEASE DON'T GIVE ME THAT LOOK.", MiraMiniPopup.MiraStates.Angry),

};

            CreateWindow(
                () => new MiraMiniPopup(introMessage, this),
                "Mira",
                true,
                null,
                600,
                600
            );

        }

        public void SetupDefaultScreen()
        {
            try { LowerAppBar.Children.Clear(); } catch { }



            CreateDesktopAppButton("Projects", Apps, () => new Projects(this), "My Projects", true);
            CreateDesktopAppButton("About", Apps, () => new DatabaseDesigner(this), "About", true);
            CreateDesktopAppButton("Logout", Apps, () => new LogoutConfirm(this), "Login", true);
        }


        public void UserLoggedOut()
        {
            ResetApp();
        }

        public void ResetApp()
        {
            // Stop timers and clear events
            timer?.Stop();
            IndexChanged = null;
            TableChanged = null;
            RefChanged = null;
            Tables_Changed = null;

            // Clear session info
            SeshUsername = "".ToSecureData();
            SeshDirectory = "".ToSecureData();
            Password = "".ToSecureData();
            SessionReturn = null;
            ProjectName = null;
            MainSessionInfo = new DBDesignerSession();
            DesktopApps.Clear();

            // Reload root page
            Application.Current.RootVisual = new MainPage();
        }


        private string _currentProjectName = null;           // Track the currently loaded project so we know when we actually switched
private string _currentBackgroundPath = "Assets/Images/lightbg.png"; // Track current wallpaper to avoid unnecessary fades

        public void LoadProject(string? Proj)
        {
            ProjectName = Proj;

            var currentDirectory = Path.Combine(
                SeshDirectory.ConvertToString(),
                SeshUsername.ConvertToString(),
                "Projects",
                ProjectName
            );

            string filePath = "Assets/Images/lightbg.png"; // default wallpaper
            UriKind uriKind = UriKind.Relative;

            // Check if directory exists and has any "Wallpaper.*" files (case-insensitive)
            if (Directory.Exists(currentDirectory))
            {
                var existingWallpaper = Directory
                    .GetFiles(currentDirectory)
                    .FirstOrDefault(f => Path.GetFileName(f).StartsWith("Wallpaper.", StringComparison.OrdinalIgnoreCase));

                if (existingWallpaper != null)
                {
                    filePath = existingWallpaper;
                    uriKind = UriKind.Absolute; // local file
                }
            }

            // ONLY FADE IF WE SWITCHED TO A DIFFERENT PROJECT (or wallpaper changed in same project - rare but safe)
            bool projectChanged = _currentProjectName != ProjectName;
            bool wallpaperChanged = _currentBackgroundPath != filePath;

            // Always update the tracker so we know what project we're on
            _currentProjectName = ProjectName;

            // Run all UI updates on UI thread to avoid race conditions (trie/collection changes may occur concurrently)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (projectChanged || wallpaperChanged)
                {
                    _ = FadeChangeBackground(filePath, uriKind);
                    _currentBackgroundPath = filePath; // Update cache
                }

                // Clear and rebuild desktop apps (no fade here)
                Apps.Children.Clear();

                CreateDesktopAppButton("About", Apps, () => new DatabaseDesigner(this), "About", true);
                CreateDesktopAppButton("F&Q", Apps, () => new FAQ(this), "FAQ", true);
                CreateDesktopAppButton("General Project", Apps, () => new GeneralPanel(this), "Control Panel", true);
                CreateDesktopAppButton("Build Project", Apps, () => new Build(this), "Login", true);


                CreateDesktopAppButton("Projects", Apps, () => new Projects(this), "Projects", true);
                CreateDesktopAppButton("Table Editor", Apps, () => new FolderViewer(this), "Table Editor", true);

                // Use a snapshot of trie words to avoid concurrent modification issues
                var prefixWords = trie.GetWords("").ToList();
                var file = new HashSet<string>();
                var folder = new HashSet<string>();
                var prefixGroups = prefixWords
                    .GroupBy(item => item.Split('.')[0])
                    .ToList();

                foreach (var group in prefixGroups)
                {
                    var folderName = group.Key;
                    if (!folder.Contains(folderName))
                    {
                        folder.Add(folderName);
                    }
                    else
                    {
                        // Only one item with this prefix
                        var item = group.First();
                        if (!item.Contains('.') && !file.Contains(item))
                        {
                            file.Add(item);
                        }
                    }
                }

                foreach (var item in folder)
                {
                    var folderIconPath = "Logos/Folder.png";
                    var folderShortcut = CreateShortcut(item, folderIconPath, ShortcutType.Folder, Apps, this);
                }

                foreach (var item in file)
                {
                    var folderIconPath = "Logos/File.png";
                    var fileShortcut = CreateShortcut(item, folderIconPath, ShortcutType.File, Apps, this);
                }


            }));
        }


        public static (StackPanel, Button) CreateShortcut(string title, string imageName, ShortcutType sType, WrapPanel AppsMenu, MainPage mainPage)
        {
            var segments = title.Split('.');
            var shortcutTitle = segments.LastOrDefault();

                // Create the Button
                var desktopAppButton = new Button
            {
                Name = title,
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                Width = 132,
                Height = 162,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10),
                Padding = new Thickness(0)
            };

            // Create StackPanel inside Button
            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 132,
                Height = 162,
                Background = new SolidColorBrush(Color.FromArgb(0x00, 0xE6, 0xE6, 0xE6)),
                IsEnabled = false,
                Visibility = Visibility.Visible
            };

            string selectedImgPath = $"Assets/Images/{imageName}";

            // Add Image
            var image = new Image
            {
                Source = new BitmapImage(new Uri(selectedImgPath, UriKind.Relative)),
                Width = 92,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Visible,
                IsEnabled = false

            };
            stackPanel.Children.Add(image);

            // Add TextBlock
            var textBlock = new TextBlock
            {
                Text = shortcutTitle,
                FontSize = 17,
                TextAlignment = TextAlignment.Center,
                Width = 116,
                Height = 44,
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Visibility = Visibility.Visible,
                IsEnabled = false,
                Foreground = new SolidColorBrush(Colors.White)
            };

            // Add black outline using DropShadowEffect
            textBlock.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 15,
                ShadowDepth = 1,
                Opacity = 30
            };
            stackPanel.Children.Add(textBlock);

            // Add StackPanel to Button
            desktopAppButton.Content = stackPanel;
            AppsMenu.Children.Add(desktopAppButton);





            switch (sType)
            {
                case ShortcutType.Folder:
                    desktopAppButton.Click += (s, e) =>
                    {
                        var newFolderViewer = (FolderViewer)mainPage.CreateWindow(
                            (() => new FolderViewer(mainPage, title)),
                            "Table Editor",
                            false // appearsInBottom argument added
                        );

                    };
                    break;
                case ShortcutType.File:
                    desktopAppButton.Click += (s, e) =>
                    {
                        mainPage.CreateWindow((() => new DatabaseViewer(mainPage, title)), "Database Viewer", true, null, 1224, 790);
                    };
                    break;
                case ShortcutType.System:
                    desktopAppButton.Click += (s, e) => { /* System action logic, later this will open a renderer in OpenSilver */ };
                    break;
            }


            return (stackPanel, desktopAppButton);
        }



        // Parse a full template pack JSON string and return list of ready-to-build items
        public static List<(string fullTableName, string description, List<RowOptions> rows, List<Reference.ReferenceOptions> references, List<IndexDefinition> indexes)> ParseTemplatePack(string templateJsonContent)
        {
            var result = new List<(string, string, List<RowOptions>, List<Reference.ReferenceOptions>, List<IndexDefinition>)>();

            using var doc = JsonDocument.Parse(templateJsonContent);
            var root = doc.RootElement;

            var dataArray = root.GetProperty("Data").EnumerateArray();

            foreach (var tableItem in dataArray)
            {
                string tableName = tableItem.GetProperty("TableName").GetString() ?? "UntitledTable";
                string description = tableItem.GetProperty("Description").GetString() ?? "";
                string schemaName = "public"; // fallback - templates usually don't store schema per table

                if (tableItem.TryGetProperty("SchemaName", out var schemaProp))
                    schemaName = schemaProp.GetString() ?? schemaName;

                string fullTableName = $"{schemaName}.{tableName}";

                var rows = new List<RowOptions>();
                if (tableItem.TryGetProperty("Rows", out var rowsElement))
                {
                    foreach (var rowElem in rowsElement.EnumerateArray())
                    {
                        rows.Add(ParseTemplateRow(rowElem));
                    }
                }

                var references = new List<Reference.ReferenceOptions>();
                if (tableItem.TryGetProperty("References", out var refsElement))
                {
                    foreach (var refElem in refsElement.EnumerateArray())
                    {
                        references.Add(new Reference.ReferenceOptions
                        {
                            MainTable = refElem.TryGetProperty("MainTable", out var mt) ? mt.GetString() : null,
                            RefTable = refElem.TryGetProperty("RefTable", out var rt) ? rt.GetString() : null,
                            ForeignKey = refElem.TryGetProperty("ForeignKey", out var fk) ? fk.GetString() : null,
                            RefTableKey = refElem.TryGetProperty("RefTableKey", out var rtk) ? rtk.GetString() : null,
                            OnDeleteAction = refElem.TryGetProperty("OnDeleteAction", out var oda) &&
                                             Enum.TryParse<ReferentialAction>(oda.GetString(), true, out var deleteAction)
                                ? deleteAction
                                : ReferentialAction.NoAction,
                            OnUpdateAction = refElem.TryGetProperty("OnUpdateAction", out var oua) &&
                                             Enum.TryParse<ReferentialAction>(oua.GetString(), true, out var updateAction)
                                ? updateAction
                                : ReferentialAction.NoAction
                        });
                    }
                }

                // === Indexes ===
                var indexes = new List<IndexDefinition>();
                if (tableItem.TryGetProperty("Indexes", out var idxElement))
                {
                    foreach (var idxElem in idxElement.EnumerateArray())
                    {
                        var columnNames = new List<string>();
                        if (idxElem.TryGetProperty("ColumnNames", out var cols))
                        {
                            foreach (var col in cols.EnumerateArray())
                                columnNames.Add(col.GetString() ?? "");
                        }

                        IndexType idxType = IndexType.Basic;
                        if (idxElem.TryGetProperty("IndexType", out var it) &&
                            Enum.TryParse<IndexType>(it.GetString(), true, out var parsedType))
                            idxType = parsedType;

                        indexes.Add(new IndexDefinition(
                            tableName: fullTableName,
                            indexName: idxElem.TryGetProperty("IndexName", out var iname) ? iname.GetString() ?? $"{tableName}_idx" : $"{tableName}_idx",
                            columnNames: columnNames.ToArray(),
                            indexType: idxType,
                            condition: idxElem.TryGetProperty("Condition", out var cond) ? cond.GetString() ?? "" : "",
                            expression: idxElem.TryGetProperty("Expression", out var expr) ? expr.GetString() ?? "" : "",
                            indexTypeCustom: idxElem.TryGetProperty("IndexTypeCustom", out var itc) ? itc.GetString() ?? "" : "",
                            useJsonbPathOps: idxElem.TryGetProperty("UseJsonbPathOps", out var jpo) && jpo.GetBoolean()
                        ));
                    }
                }

                result.Add((fullTableName, description, rows, references, indexes));
            }

            return result;
        }

        // Helper: Parse a single row from template JSON into RowOptions
        private static RowOptions ParseTemplateRow(JsonElement rowElem)
        {
            return new RowOptions(
                fieldName: rowElem.TryGetProperty("Name", out var n) ? n.GetString() ?? "unnamed" : "unnamed",
                description: rowElem.TryGetProperty("Description", out var d) ? d.GetString() ?? "" : "",
                postgresType: rowElem.TryGetProperty("RowType", out var rt) &&
                              Enum.TryParse<PostgresType>(rt.GetString(), true, out var type)
                    ? type
                    : PostgresType.Text,
                customType: "",
                elementLimit: rowElem.TryGetProperty("Limit", out var l) ? l.GetInt32() : null,
                isArray: rowElem.TryGetProperty("IsArray", out var a) && a.GetBoolean(),
                arrayLimit: rowElem.TryGetProperty("ArrayLimit", out var al) ? al.GetInt32() : null,
                isEncrypted: rowElem.TryGetProperty("EncryptedAndNOTMedia", out var enc) && enc.GetBoolean(),
                isMedia: rowElem.TryGetProperty("Media", out var med) && med.GetBoolean(),
                isPrimary: rowElem.TryGetProperty("IsPrimary", out var p) && p.GetBoolean(),
                isUnique: rowElem.TryGetProperty("IsUnique", out var u) && u.GetBoolean(),
                isNotNull: rowElem.TryGetProperty("IsNotNull", out var nn) && nn.GetBoolean(),
                defaultValue: rowElem.TryGetProperty("DefaultValue", out var dv) ? dv.GetString() : null,
                check: rowElem.TryGetProperty("Check", out var chk) ? chk.GetString() : null,
                defaultIsKeyword: rowElem.TryGetProperty("DefaultIsPostgresFunction", out var kw) && kw.GetBoolean()
            );
        }



    }


    /* It's EXTREMELY important that Sumika from Muv Luv is here, trust me
.............==++++***+++++++++=-.......:+-.....=++*###%%%%%%%%%@%#+====*#*=....................-+==============
............-====*######**+++******+-....:*:..:=+*###%%%%%%%%%%%##%#*===-+###-:.................-+==============
...........:====*#%%%%%%###%%%##*+++***-..-+:=++*###%%%%##****##%%%%##=--:=####=................-+==============
...........====*#%%%%%%%%%%%%%#%%%%##*++**-+*++*#%*+++++++++++++++++###+::.:*####=..............-+==============
..........----+##%%%%%%%###*********#####*+##*#*++++++++++++++**######%#+:...+#####=............-+=====+========
..........-:-=##%%%*==+**#######**++++++++**+++++++++++++++++++++*#%%%#%#+:...+#####*=..........-+=====+========
.........::::+##==+######%%##*+++++++++++++++++++++++++++++++++++++++*#%%#*:...+#####*==........-+==============
.........:.:+++###%%%%%*+=+++++++++++++++++++++++++++++++++++++++*#%#*+=*##*....=#####+=+-......-+==+==+========
........-:++##%%%###+--=-===+++*++++++++++++++++++++++++++++++====+###%%#*##*:...+####*==++:....-+==+==+========
.......:**#*##%%#*::----=+####*++++++++++++++++++++++++++++=========*####%#%#*:...*####+==+=....-+==+++++=======
......-#=::=#%%+::==:=*#####+==++++++++++++++++++++++++++========---:+#%%##%%#*-..:*####+==*-...-+==+++++=======
....:::-:::*#*-=*-=*#####+-========+++++++++++++++++=========--+*=:...-##%%##%#*-::=####*==++:..-+==++==+=======
......--:-=#=+*++#####*-.:=================++=+==========+=--::-+##=...-*##%%%%##---+####++++-..-+==++++++======
::::::=--=**########*:.-+++------=================-------=**-...:*#%*=:.-*###%%%##===*###+++++:.-+==++=+++======
::::.-==+*#%#####%*:.=*##*=:.:.:::-*-----::=+--==-:::::--:-##+:..:*#%#*-:=####%%%#*===*##*+++++:-+==++++++=++===
.....-=*#%%####%#=:=#####+:....:::==:::::..=**-:=+=:....=+:-###=:.:##%##==+#####%%#*+++*##++++++=+==++++++++====
.....=#%%%###%%*-+#######-....::.-*-:-::...-###=.=**=...:+*--#%#*=:=##%##+=*#####%%#*+++***+++*+**==+++++++=====
....:#%%###%%#*+########*.....:..+#:---:....*###+.:##*-..:*#+-%%##*=*##%##*+######%##+++***++++***==+++++++++===
...:*%%##%%##+*#########=....=:.-##:=--+....+##+#*-.+##*-::*##=%%%##+*##%##**######%#*++++*+++++***=+++++++++===
...*%%##%%%#*##%######%#-...=*:.*#%:+=-#-...:*##=*#=.=*##*-=##%+%%%#####%%##+######%%#*+++++++++****+++++++++===
..+%%#%%%%#*####%%%####*:::=#*:-###:*+:#*-.:.+##*++#*:=*###*+##%*#%%#####%%#########%#*+++++++*++*#+*+++++++++++
.=%##%%%%#*####%%%###%#*-==##*:+###-**-#%*--=-###+==##*%%%@%%%%%%%#%%#####%####%#####%#*++++++**++#***+++++++++=
-####%%%####%##%%%###%##==*#%*=####*##**%%*=++*###%%%%%%%%#%%%%%%%@%%%%%%#%%###%%####%#*++++++*#++*#**++++++++++
*#*:+%%####%##%%%####%##++#%%#+#+*#*#***##%#+**%%%#+--+##+*=+######%+#%%##%%%##%%%###%%#+++++++#*++*#**+++++++++
#+::+%####%##%%%%###%%##+*%@%%%%*+%+##=*###%#*#####=----*#**==*####%*+#%##%%%##%%%####%#*++++++##*++*#+#++++++++
=.::*####%##%%%%####%###**%@%###++#+*#=+*#**%######*=-:-=*%##+=*%#%*%==%%#%#%%#%%%####%#*+++++=###++**#+#+++++++
::.:*###%%##%%%%#%##%##%**#%%###=-*+=#=:*#*++%######+=++*+=+##+=+#%***-=%%#*%%#%%%###%%#*+++++-+##*==**#+*++++++
..::*##%###%%%%%#%#%%%#%###%%###=--+-++--*#*=+%##%%##+==-=%@@@%%%@@@@@%**%#+#%#%%%###%%##+++++-=###==+***+*+++++
...:*###+##%%%#%#%##%%#%####%#%%*-:+--=---*#+=-*%#%%%*==%@@@%#..+%%%@@@@%##-*%%%%%###%%%#+++++--###*:-*+#+++++++
:::-###*+##%%%#%#%%%%%#%%###*%%%#==-=----:=#*=--=%%%%%+*%%**#@%%%%%%%%@*#@%=+%%%%%####%%#*+++=-:*#%*-:=**#=+++++
..:=##%*+##%%%#%#%%#%%##%##%+%@@@*#*--------#+=--:+%%%#=-=-+*@%%%%%%%%#%%@@@%%#%%%####%##*++===.*#%#=::++*#=++++
::.=###*+##%%###%%%#%%%#%%#%*@@%%*#%%+-------+=--:--+%%*=----#%%%%%%%%%%%%@@#+#%%%##%#%%#*++===.+###*:.=**#+++++
:::=##**+##%####%%%%%%%%#%##%%@%%@@%*#+-------------:-=#=--:.-%*#%%%%#%##%@%-+%%%##%%#%##*+=---.=##%#=.:#**#++++
:.:=##**+#%@+###%%%%%%%%%%%##*%%#@%%%%%+-------:---:-------:::%#++#%##+==%%=-#%%%#%%%#%##+==:--.=##%#+..***##*++
::.+##+#++%%+*###%%%%#%%%%%%#*+#-#%%%%%%---:----------------:.+%%#*++*##=%*-*%%%#%%%#%%##+=:.--.-##%%#:.=***%#*+
::.+*-+%++##+*###%%%%%%%%%%%%%+=-+##%%##=---:-------------:--=*%%@@@@@@@#-:+%%%%%%%#%@%##==..:-.-###%#=.-****##+
:.:=+-+**=*#=+####%%%%%%%%%%%%*=-:%*++==+---------::::-----:---------::...=%%%%%%@%%%%###--..--:-*##%#+.:#***#%+
:::-=:+=+=+#+=*####%%%%%%%%%%%%*+*+@###%*-----:-:....::----------:::.....+%%%%%%%%%%*%##*::.:=+:-*##%%*::**+=*#=
::::-:-+-+=+*=+#####%%%%%%%%%%%#**#%%#=:.::-----:.....::---------:-:-::-##%%%*+##%#++%##=:..:+*:=###%%#-:+-..=#+
:::::::+:+=+#++##%###%%%%%%%%%%%*+----::::::--=:.....:::--------------++*%%+:=#%%*--*###-...=**:+###%%#+-+:..:*+
:::::::=--+=#*=*#%%#*##%%%%%%%%@%#=-------------::..:::-------------==##*-::*%%*==*#%##*:..:+#*.*###%%#*=+:..:*=
::::::::=:=+##++#%%#**##%%%%%%%%%#%+-------------------------------+#+-:..-#%%%%%%%%###=.:.-*#+:*###%%%#=+:..:*:
::::::::-::+*##+*%+#+++###%%%%%%%%%+---------------------------------:::-**++=*%%%%%##*::::*#%=-=##%%%##+=:..-=:
:::::::::::-+##*+#*+*++*###%%%%%%*--==--------------==+++=---------::-+*=:=+=:+%%%%###+:--=###=+-##%%%%#+-..:=--
::::::::::::=#%#*#*-*+=+###%%%%%%%%+-.::---------+**++++++-------:-*+-::-+==-:*%%%%###==+=*#%#++:*#%%*##+:::-===
:::::::::::::+%%#*#-:+==+###%%%%%%%%%*=:::--------=+++++++--------::..:===+=:=#%%%####=+++#%%*+=:*#%%=##+--=====
::::::::::::::*####=:-==-*##%%%%%%%%%###*=-::-------=====--------::.:=+====-=**#@%###++*+##%#**::*%%#:*#+++++==-
::::::::::::::-#*##*:.===-###%%%%%%%%*-*##%#+-:---------::-----:::-====+===+***#%###*+#**#%%#+=.-#%%=-##+++++=--
:::::::::::::::-**##+::==-+##%%%#%%%%%==+###%%%*=-:---=+--------========+*#****#%###+##*#%%#**:.-#%%++%+++++=---
::::::::::::::::-=##%=--+=-*##%@%%%@%%*---+##%%%%##+=-------=+=====+==+#%%#*###%###**#*##%%#*=::=#%#++#+======--
::::::::::::::::::-##==-=*=-##%%%#%@@%%=----+###%%%#%%#*++#%%#*+==+*%%%%%%##*#%####*#**#%%##*:::=%#--==.......:=
:::::::::::::::::::-#*:-==+-+##%%%#%%%%*---====*##%%%#*##***#%%%%%%%%%%%%#####%###*##*#%%%##=--:+%+--+--:::.....
::::::::::::::::::::-#=::==+-*##%@#%#*%%=--::::--==+==++**=--##%%%%%%%%%%##+-#####%#*#%%%##+----#*---=----::....
:::::::::::::::::::::-*::--===##%#%#%+#%#-----:--=+++++*+=----*#%#%%%%%%#+--+####%#*#%%%%%#=----#=---------::...
::::::::::::::::::::::===:.:==*###*#%#=%%+------=+++++*+=-----=--=+###*----=%##%%%##%##%##+----++----------:::::
:::::::::::::::::::::--=-::::++###+##%=-%#=----=+++++*+=-------=-----------###%%###%%#%%#+=----*-----------::...
:::::::::::::::::::-.......:-=#*###+#%+-=%*----+++++++=-------------------*##%%%##%##%%#++=---==-------::.......
::::::::::::::::::::....:---:::+*#%=###--=#=--=+++++*+--::::-------------=##%%%##%###%*+++----+---====-:........
::::::::::::::::::-.....:-------*##+-##---=#-=++++++*=---::..::----------##%%%##%#**%*+=+=---+++++++=:.........:
::::::::::::::::::-.....:--------*#%=*#+=+==*+=+++++*=----:...::--------*%%%##%#***#*++=+=+++++++==-:...........
:::::::::::::::::-:....::---==---=##+=#*---=++++++++*+-----:::.::------+%%%%#%#**+**++++========--:.............
:::::::::::::::-:..::...:----=+=--=#*-=#=-------====+++===------------+%%%%#%*+++*++==------=---:...::::--::....
::::::::::::::-::-----==------+*+===#=-#=----------------------------=#%%%%*+===-------------:::-=++==---::.....
::::::::::::::-=-------**=-----+*+*==*-=+----------------------------*%%%#=--------------=-:-+++++=-----::......
:::::::::::::::--------****=----+*+*===-=--------------=+++++=======*%%%=-------------==-==++===-------::.......
:::::::::::::::=------=*****+----+**+------------------=************%%*---------------=-------------=--:........
::::::::::::::-=----=+********=--=++=------------------************@#=----------------=----==*********=.........
::::::::::::::-=====+***********=-=*=--------------------=+*******#=-----------------+==+************+-=-.......
--:-----::::--:..:---=+**********+-+=---------------------=++=--==------------------+***************+=----:.....
----:::::::--::.:-------=*********+==::.::-----------------==-----------::---------+***************+=----:-:....
---::::::..::.:-----------+*******+=:...:--------------------------:::..::--------=****************=------:--...
---::------:::.:-=---------=*******=....:------------------------:......::-------=****************=-----------..
---::-==-:::::.:.-=-====-----+*****:....:----------------------:.......::-------=**********+++==++=------------:
--::--:::-----::::-=:::-==----+***=....:---------------------:........:--------=*****+===-------:::----------:=+
-==:::-----------=:......-=----+**:...:----------------------......::----------+*=-=------------:...:=--------=#
--:::---=+==----=:.......:-+=--=*+:.:----------------==-----:....:------------==---------------:......--------+#
-----==--------=:........:--====+=------------------=++=----:.::-------------==----------------:.......:=-----#%
=-==-:--------=:::.......:---:...:=----------------+**+=--------------------==----------------:.........:=---=%%
==::---=+=---=-==::......:-::..::.:=-------------=+***+=-------------------=+--=======--------............=--+%%
-:---=+==----==+*===::....:..:--:..:===========-=+*****==-----------------=++=-::=++**+=-----:............:=-%%%
:-----------+++**##+-:::....:---:.............:::-==++++=---------------===::......:-+*+==---:.............-*%%%
-=-:-------=+++=**+=----:..-----:.......................:--===-=----==+=-::-===------::=+==--:.............-#%%%

     * */
















}