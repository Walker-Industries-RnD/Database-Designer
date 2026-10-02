using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Database_Designer
{
    // Try the project on your own Postgres: build the schema from the latest
    // export, fill it with sample rows, run queries, and start the generated
    // API with Swagger open.
    public class LocalDatabaseWindow : Page
    {
        public UIWindowEntry WindowInfo { get; set; }

        private static Process _api;
        private static int _apiPort;
        private static bool _exitHooked;

        private readonly MainPage _host;
        private readonly TextBox _conn;
        private readonly TextBlock _connStatus;
        private readonly TextBlock _buildInfo;
        private readonly TextBox _rows;
        private readonly TextBox _query;
        private readonly CheckBox _keep;
        private readonly StackPanel _results = new StackPanel();
        private readonly TextBlock _resultInfo;
        private readonly TextBox _log;
        private readonly Button _startApi, _stopApi, _swagger;
        private readonly List<Button> _dbButtons = new();
        private readonly List<string> _logLines = new();

        private static readonly Color CardBg = Color.FromRgb(0x1B, 0x1A, 0x17);
        private static readonly Color CardBorder = Color.FromRgb(0x35, 0x32, 0x2B);
        private static readonly Color PanelBg = Color.FromRgb(0x22, 0x20, 0x1C);
        private static readonly Color Cream = Color.FromRgb(0xF0, 0xE9, 0xD2);
        private static readonly Color Muted = Color.FromRgb(0xA8, 0xA2, 0x92);
        private static readonly Color Accent = Color.FromRgb(0x9D, 0x97, 0x85);
        private static readonly Color ButtonBg = Color.FromRgb(0x33, 0x2F, 0x29);
        private static readonly Color Good = Color.FromRgb(0x6F, 0xC2, 0x8B);
        private static readonly Color Bad = Color.FromRgb(0xE0, 0x6C, 0x5F);
        private static readonly FontFamily Inter = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf");
        private static readonly FontFamily Mono = new FontFamily("Consolas");

        public LocalDatabaseWindow(MainPage host)
        {
            _host = host;
            Width = 1120;
            Height = 720;
            Background = new SolidColorBrush(Colors.Transparent);

            if (!_exitHooked)
            {
                _exitHooked = true;
                AppDomain.CurrentDomain.ProcessExit += (s, e) => DevRunner.Stop(_api);
            }

            var root = new Grid { Margin = new Thickness(22, 18, 22, 18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(430) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titles = new StackPanel();
            titles.Children.Add(Text("Local Database", 24, Cream));
            titles.Children.Add(Text("Try your project on your own Postgres before you ship it. Uses the latest Build Project export.", 12, Muted));
            header.Children.Add(titles);
            var close = MakeButton("✕", 34, ButtonBg, Cream);
            close.Click += (s, e) => { if (_host.IntroPage.Children.Contains(this)) _host.IntroPage.Children.Remove(this); };
            Grid.SetColumn(close, 1);
            header.Children.Add(close);
            Grid.SetColumnSpan(header, 3);
            root.Children.Add(header);

            // Left: connection, setup, API
            var left = new StackPanel();
            var leftScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = left };
            Grid.SetRow(leftScroll, 1);
            root.Children.Add(leftScroll);

            left.Children.Add(Section("1. Connection"));
            _conn = new TextBox
            {
                Text = string.IsNullOrWhiteSpace(_host.DevConnection) ? LocalDb.DefaultConnection : _host.DevConnection,
                FontFamily = Mono, FontSize = 12, Height = 30, VerticalContentAlignment = VerticalAlignment.Center
            };
            left.Children.Add(_conn);
            var connRow = Row();
            var test = MakeButton("Test & save", 110, Accent, CardBg);
            test.Click += async (s, e) => await TestConnection();
            connRow.Children.Add(test);
            _connStatus = Text("", 12, Muted);
            _connStatus.VerticalAlignment = VerticalAlignment.Center;
            _connStatus.Margin = new Thickness(10, 0, 0, 0);
            _connStatus.TextWrapping = TextWrapping.Wrap;
            _connStatus.MaxWidth = 300;
            connRow.Children.Add(_connStatus);
            left.Children.Add(connRow);
            left.Children.Add(Text("Saved encrypted inside this project. The database is created if it doesn't exist.", 11, Muted));

            left.Children.Add(Section("2. Set up the database"));
            _buildInfo = Text("", 12, Muted);
            _buildInfo.TextWrapping = TextWrapping.Wrap;
            left.Children.Add(_buildInfo);
            var setupRow = Row();
            var fresh = DbButton("Create from scratch", 160, async () => await CreateFromScratch());
            var migrate = DbButton("Apply Migration.sql", 150, async () => await ApplyMigration());
            setupRow.Children.Add(fresh);
            setupRow.Children.Add(migrate);
            left.Children.Add(setupRow);
            left.Children.Add(Text("From scratch drops this project's schemas, then runs SQL.sql and RLS.sql. Migration upgrades a database built from the previous export.", 11, Muted));

            var sampleRow = Row();
            var sample = DbButton("Add sample data", 150, async () => await AddSampleData());
            sampleRow.Children.Add(sample);
            sampleRow.Children.Add(Text("rows per table", 12, Muted, new Thickness(10, 0, 6, 0)));
            _rows = new TextBox { Text = "10", Width = 50, Height = 28, FontSize = 12 };
            sampleRow.Children.Add(_rows);
            left.Children.Add(sampleRow);

            left.Children.Add(Section("3. Run the API"));
            var apiRow = Row();
            _startApi = MakeButton("Start API", 100, Accent, CardBg);
            _startApi.Click += (s, e) => StartApi();
            _swagger = MakeButton("Open Swagger", 110, ButtonBg, Cream);
            _swagger.Click += (s, e) => DevRunner.OpenUrl($"http://localhost:{_apiPort}/swagger");
            _stopApi = MakeButton("Stop", 70, ButtonBg, Cream);
            _stopApi.Click += (s, e) => { DevRunner.Stop(_api); _api = null; Log("API stopped."); UpdateApiButtons(); };
            apiRow.Children.Add(_startApi);
            apiRow.Children.Add(_swagger);
            apiRow.Children.Add(_stopApi);
            left.Children.Add(apiRow);
            left.Children.Add(Text($"Runs `dotnet run` on the exported API (needs the .NET SDK) at http://localhost:{DevRunner.DefaultApiPort}.", 11, Muted));

            _log = new TextBox
            {
                IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontFamily = Mono, FontSize = 11,
                Height = 170, Margin = new Thickness(0, 10, 0, 0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalContentAlignment = VerticalAlignment.Top, VerticalAlignment = VerticalAlignment.Top
            };
            left.Children.Add(_log);

            // Right: query preview
            var right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(150) });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(right, 1);
            Grid.SetColumn(right, 2);
            root.Children.Add(right);

            right.Children.Add(Section("Preview a query"));
            _query = new TextBox
            {
                AcceptsReturn = true, AcceptsTab = true, FontFamily = Mono, FontSize = 12, TextWrapping = TextWrapping.NoWrap,
                VerticalContentAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Text = DefaultQuery(),
                VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch, Height = double.NaN
            };
            _query.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { e.Handled = true; await RunQuery(); }
            };
            var queryHost = LayoutHelpers.Fill(_query, 100);
            Grid.SetRow(queryHost, 1);
            right.Children.Add(queryHost);

            var queryRow = Row();
            var run = DbButton("Run (Ctrl+Enter)", 140, async () => await RunQuery());
            queryRow.Children.Add(run);
            _keep = new CheckBox
            {
                Content = "Keep changes (otherwise INSERT/UPDATE/DELETE are rolled back)", Foreground = new SolidColorBrush(Cream),
                FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0)
            };
            queryRow.Children.Add(_keep);
            Grid.SetRow(queryRow, 2);
            right.Children.Add(queryRow);

            var resultsHost = new Grid();
            resultsHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            resultsHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _resultInfo = Text("", 12, Muted, new Thickness(0, 6, 0, 6));
            resultsHost.Children.Add(_resultInfo);
            var resultsBorder = new Border
            {
                Background = new SolidColorBrush(PanelBg), CornerRadius = new CornerRadius(8), Padding = new Thickness(8),
                Child = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = _results
                }
            };
            Grid.SetRow(resultsBorder, 1);
            resultsHost.Children.Add(resultsBorder);
            Grid.SetRow(resultsHost, 3);
            right.Children.Add(resultsHost);

            Content = new Border
            {
                Background = new SolidColorBrush(CardBg), BorderBrush = new SolidColorBrush(CardBorder),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Child = root
            };

            RefreshBuildInfo();
            UpdateApiButtons();
        }

        private string ConnectionString => _conn.Text.Trim();
        private string BuildDir => DevRunner.LatestBuild(DevRunner.ProjectDir(_host.SeshDirectory.ConvertToString(), _host.SeshUsername.ConvertToString(), _host.ProjectName));

        private string DefaultQuery()
        {
            var tables = _host.MainSessionInfo.Tables;
            if (tables == null || tables.Count == 0 || string.IsNullOrEmpty(tables[0].TableName)) return "select now();";
            var t = tables[0];
            var name = string.IsNullOrEmpty(t.SchemaName) ? $"\"{t.TableName}\"" : $"\"{t.SchemaName}\".\"{t.TableName}\"";
            return $"select * from {name} limit 50;";
        }

        private void RefreshBuildInfo()
        {
            var dir = BuildDir;
            if (dir == null)
            {
                _buildInfo.Text = "No export yet. Open Build Project and export first.";
                _buildInfo.Foreground = new SolidColorBrush(Bad);
                return;
            }
            _buildInfo.Text = $"Using export {Path.GetFileName(dir)} from {Directory.GetLastWriteTime(dir):d MMM HH:mm}.";
            _buildInfo.Foreground = new SolidColorBrush(Muted);
        }

        private async Task TestConnection()
        {
            await Busy(async () =>
            {
                _connStatus.Text = "Connecting…";
                _connStatus.Foreground = new SolidColorBrush(Muted);
                try
                {
                    bool created = await LocalDb.EnsureDatabase(ConnectionString);
                    var version = await LocalDb.Test(ConnectionString);
                    _connStatus.Text = (created ? "Created the database. " : "") + "Connected: " + version.Split(',')[0];
                    _connStatus.Foreground = new SolidColorBrush(Good);
                    _host.DevConnection = ConnectionString;
                    if (_host.ProjectName != null) await MainPage.OnTables_Changed(_host);
                }
                catch (Exception ex)
                {
                    _connStatus.Text = Friendly(ex);
                    _connStatus.Foreground = new SolidColorBrush(Bad);
                }
            });
        }

        private async Task CreateFromScratch()
        {
            var dir = RequireBuild();
            if (dir == null) return;
            var schemas = (_host.MainSessionInfo.Tables ?? new()).Select(t => string.IsNullOrWhiteSpace(t.SchemaName) ? "public" : t.SchemaName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (MessageBox.Show($"This deletes everything in these schemas on {Database()}: {string.Join(", ", schemas)}.\n\nOnly use it on a test database. Continue?",
                    "Create from scratch", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            await Busy(async () =>
            {
                try
                {
                    await LocalDb.EnsureDatabase(ConnectionString);
                    await LocalDb.DropSchemas(ConnectionString, schemas);
                    Log($"Dropped schemas: {string.Join(", ", schemas)}");
                    await BuildTables(dir);
                }
                catch (Exception ex) { Log("✗ " + Friendly(ex)); }
            });
        }

        // Runs SQL.sql and then RLS.sql (if the export has policies).
        private async Task BuildTables(string dir)
        {
            await RunFile(dir, "SQL.sql", "Tables");
            await RunRlsIfAny(dir);
            Log("Database is ready.");
        }

        private async Task RunRlsIfAny(string dir)
        {
            var rls = Path.Combine(dir, "RLS.sql");
            if (File.Exists(rls) && File.ReadAllText(rls).Trim().Length > 0)
                await RunFile(dir, "RLS.sql", "Row level security");
        }

        // The tables an export creates, as "schema.table", from its schema
        // snapshot (or from the open project for exports that don't have one).
        private List<string> ExpectedTables(string buildDir)
        {
            var file = buildDir == null ? null : Path.Combine(buildDir, MigrationGenerator.SnapshotFile);
            if (file != null && File.Exists(file))
            {
                try
                {
                    var snap = System.Text.Json.JsonSerializer.Deserialize<MigrationGenerator.Snapshot>(File.ReadAllText(file));
                    if (snap?.Tables != null)
                        return snap.Tables.Select(t => Qualified(t.Schema, t.Name)).Distinct().ToList();
                }
                catch { }
            }
            return (_host.MainSessionInfo.Tables ?? new())
                .Where(t => t.Rows != null && t.Rows.Count > 0)
                .Select(t => Qualified(t.SchemaName, t.TableName)).Distinct().ToList();
        }

        private static string Qualified(string schema, string name) =>
            ((string.IsNullOrWhiteSpace(schema) ? "public" : schema.Trim()) + "." + (name ?? "").Trim()).ToLowerInvariant();

        // Migration.sql only upgrades a database built from the previous
        // export. Check that first, and build the tables instead when the
        // database doesn't have any of them yet.
        private async Task ApplyMigration()
        {
            var dir = RequireBuild();
            if (dir == null) return;
            await Busy(async () =>
            {
                try
                {
                    await LocalDb.EnsureDatabase(ConnectionString);
                    var have = await LocalDb.ExistingTables(ConnectionString);
                    var current = ExpectedTables(dir);
                    var (previous, previousVersion) = MigrationGenerator.LoadPrevious(Path.GetDirectoryName(dir), Path.GetFileName(dir));
                    var before = previous?.Tables?.Select(t => Qualified(t.Schema, t.Name)).Distinct().ToList() ?? new List<string>();

                    if (!current.Concat(before).Any(have.Contains))
                    {
                        Log($"{Database()} doesn't have this project's tables yet, so there's nothing for Migration.sql to upgrade. Building them from SQL.sql instead.");
                        await BuildTables(dir);
                        return;
                    }

                    var missing = before.Where(t => !have.Contains(t)).ToList();
                    if (missing.Count > 0)
                    {
                        Log($"✗ Migration.sql upgrades a database built from export {previousVersion}, but {Database()} is missing: {string.Join(", ", missing)}.");
                        Log("  Press Create from scratch to rebuild it (this deletes the rows in those schemas).");
                        return;
                    }

                    await RunFile(dir, "Migration.sql", "Migration");
                    await RunRlsIfAny(dir);

                    have = await LocalDb.ExistingTables(ConnectionString);
                    var stillMissing = current.Where(t => !have.Contains(t)).ToList();
                    if (stillMissing.Count > 0)
                        Log($"! Still missing after the migration: {string.Join(", ", stillMissing)}. Create from scratch will build everything.");
                }
                catch (Exception ex) { Log("✗ " + Friendly(ex)); }
            });
        }

        private async Task RunFile(string dir, string file, string label)
        {
            var path = Path.Combine(dir, file);
            if (!File.Exists(path)) throw new FileNotFoundException($"{file} isn't in export {Path.GetFileName(dir)}.");
            Log($"Running {file}…");
            try
            {
                await LocalDb.ExecuteScript(ConnectionString, File.ReadAllText(path));
                Log($"✓ {label} applied.");
            }
            catch (Exception ex) { throw new Exception($"{file} failed (nothing was changed): {Friendly(ex)}"); }
        }

        private async Task AddSampleData()
        {
            if (!int.TryParse(_rows.Text.Trim(), out var n) || n < 1) n = 10;
            await Busy(async () =>
            {
                var gen = SampleDataGenerator.Generate(_host.MainSessionInfo.Tables, n);
                foreach (var w in gen.Warnings) Log("! " + w);
                var dir = BuildDir;
                if (dir != null)
                {
                    try { File.WriteAllText(Path.Combine(dir, "SampleData.sql"), gen.Sql); } catch { }
                }
                int ok = 0;
                foreach (var (table, statement) in gen.Statements)
                {
                    try
                    {
                        await LocalDb.ExecuteScript(ConnectionString, statement);
                        ok++;
                    }
                    catch (Exception ex) { Log($"✗ {table}: {Friendly(ex)}"); }
                }
                Log($"✓ Sample rows added to {ok} of {gen.Statements.Count} tables." + (dir != null ? " (Also saved as SampleData.sql.)" : ""));
            });
        }

        private async Task RunQuery()
        {
            var sql = _query.Text;
            if (string.IsNullOrWhiteSpace(sql)) return;
            await Busy(async () =>
            {
                _results.Children.Clear();
                _resultInfo.Text = "Running…";
                var watch = Stopwatch.StartNew();
                try
                {
                    var r = await LocalDb.Query(ConnectionString, sql, 200, rollback: _keep.IsChecked != true);
                    watch.Stop();
                    ShowResults(r);
                    _resultInfo.Text = (r.Columns.Count > 0 ? $"{r.Rows.Count}{(r.Truncated ? "+ (showing first 200)" : "")} row(s)" : $"{Math.Max(0, r.Affected)} row(s) affected")
                        + $" · {watch.ElapsedMilliseconds} ms" + (_keep.IsChecked == true ? "" : " · rolled back");
                    _resultInfo.Foreground = new SolidColorBrush(Muted);
                }
                catch (Exception ex)
                {
                    _resultInfo.Text = Friendly(ex);
                    _resultInfo.Foreground = new SolidColorBrush(Bad);
                }
            });
        }

        private void ShowResults(LocalDb.QueryResult r)
        {
            _results.Children.Clear();
            if (r.Columns.Count == 0) return;
            var grid = new Grid();
            var widths = r.Columns.Select((c, i) => Math.Min(280, Math.Max(60, Math.Max(c.Length, r.Rows.Select(x => x[i]?.Length ?? 0).DefaultIfEmpty(0).Max()) * 7.2 + 16))).ToList();
            foreach (var w in widths) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });
            for (int row = 0; row <= r.Rows.Count; row++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                for (int c = 0; c < r.Columns.Count; c++)
                {
                    bool head = row == 0;
                    var value = head ? r.Columns[c] : r.Rows[row - 1][c];
                    var cell = new Border
                    {
                        BorderBrush = new SolidColorBrush(CardBorder), BorderThickness = new Thickness(0, 0, 1, 1),
                        Background = new SolidColorBrush(head ? ButtonBg : row % 2 == 0 ? PanelBg : CardBg),
                        Padding = new Thickness(6, 3, 6, 3),
                        Child = new TextBlock
                        {
                            Text = value, FontFamily = head ? Inter : Mono, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis,
                            Foreground = new SolidColorBrush(head ? Cream : value == "NULL" ? Muted : Cream)
                        }
                    };
                    ToolTipService.SetToolTip(cell, value);
                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, c);
                    grid.Children.Add(cell);
                }
            }
            _results.Children.Add(grid);
        }

        private async void StartApi()
        {
            var dir = RequireBuild();
            if (dir == null) return;
            if (_api != null && !_api.HasExited) { DevRunner.OpenUrl($"http://localhost:{_apiPort}/swagger"); return; }
            if (!await DevRunner.HasDotnetSdk())
            {
                Log("✗ The .NET SDK isn't installed (the `dotnet` command wasn't found). Get it from https://dot.net and try again.");
                return;
            }
            // The API needs the tables, so build them first if the database
            // doesn't have any yet, and say which ones are missing otherwise.
            try
            {
                var expected = ExpectedTables(dir);
                var have = await LocalDb.ExistingTables(ConnectionString);
                var missing = expected.Where(t => !have.Contains(t)).ToList();
                if (missing.Count > 0 && missing.Count == expected.Count)
                {
                    Log($"{Database()} doesn't have this project's tables yet. Building them from SQL.sql first.");
                    bool built = false;
                    await Busy(async () =>
                    {
                        try { await BuildTables(dir); built = true; }
                        catch (Exception ex) { Log("✗ " + Friendly(ex)); }
                    });
                    if (!built) return;
                }
                else if (missing.Count > 0)
                {
                    Log($"! {Database()} is missing: {string.Join(", ", missing)}. Requests that use them will fail.");
                    Log("  Press Apply Migration.sql, or Create from scratch to rebuild everything.");
                }
            }
            catch (Exception ex)
            {
                Log("✗ Couldn't reach the database: " + Friendly(ex));
                return;
            }

            _apiPort = DevRunner.DefaultApiPort;
            Log("Starting the API (the first start downloads packages and can take a minute)…");
            try
            {
                bool opened = false;
                _api = DevRunner.StartApi(dir, ConnectionString, _apiPort, line => Dispatcher.BeginInvoke(() =>
                {
                    Log(line);
                    if (!opened && line.Contains("Now listening on"))
                    {
                        opened = true;
                        Log($"✓ API is running. Swagger: http://localhost:{_apiPort}/swagger");
                        DevRunner.OpenUrl($"http://localhost:{_apiPort}/swagger");
                    }
                }));
                _api.Exited += (s, e) => Dispatcher.BeginInvoke(() => { Log("API stopped."); UpdateApiButtons(); });
            }
            catch (Exception ex) { Log("✗ " + ex.Message); }
            UpdateApiButtons();
        }

        private void UpdateApiButtons()
        {
            bool running = _api != null && !_api.HasExited;
            _startApi.Content = running ? "Running" : "Start API";
            _stopApi.IsEnabled = running;
            _swagger.IsEnabled = running;
        }

        private string RequireBuild()
        {
            RefreshBuildInfo();
            var dir = BuildDir;
            if (dir == null) Log("✗ Export the project first (Build Project > Export).");
            return dir;
        }

        private string Database()
        {
            try { return new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString).Database ?? "this database"; }
            catch { return "this database"; }
        }

        private async Task Busy(Func<Task> work)
        {
            foreach (var b in _dbButtons) b.IsEnabled = false;
            try { await work(); }
            finally { foreach (var b in _dbButtons) b.IsEnabled = true; }
        }

        private void Log(string line)
        {
            _logLines.Add(line);
            if (_logLines.Count > 300) _logLines.RemoveRange(0, _logLines.Count - 300);
            _log.Text = string.Join("\n", _logLines);
            _log.SelectionStart = _log.Text.Length;
        }

        private static string Friendly(Exception ex)
        {
            var msg = ex is Npgsql.PostgresException pg ? $"{pg.MessageText}{(pg.Detail != null ? ": " + pg.Detail : "")}" : ex.Message;
            if (ex is Npgsql.NpgsqlException && ex.InnerException is System.Net.Sockets.SocketException)
                msg = "Couldn't reach Postgres. Is it running, and are the host and port right?";
            return msg;
        }

        private Button DbButton(string label, double width, Func<Task> click)
        {
            var b = MakeButton(label, width, ButtonBg, Cream);
            b.Click += async (s, e) => await click();
            _dbButtons.Add(b);
            return b;
        }

        private static StackPanel Row() => new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 4) };

        private static TextBlock Section(string text) => Text(text, 15, Cream, new Thickness(0, 14, 0, 6));

        private static TextBlock Text(string text, double size, Color color, Thickness? margin = null) => new TextBlock
        {
            Text = text, FontSize = size, FontFamily = Inter, Foreground = new SolidColorBrush(color),
            TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0, 2, 0, 0)
        };

        private static Button MakeButton(string label, double width, Color bg, Color fg) => new Button
        {
            Content = label, Width = width, Height = 30, FontSize = 12, FontFamily = Inter, Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(bg), Foreground = new SolidColorBrush(fg),
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
    }
}
