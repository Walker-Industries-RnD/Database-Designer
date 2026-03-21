using DatabaseDesigner;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace Database_Designer
{
    public partial class MainDesigner : Page
    {
        internal static BasicDatabaseDesigner Designer;
        internal static MainPage mainPaged;
        public UIWindowEntry WindowInfo { get; private set; }

        // Stores: preview data, card, list, select button, collapse button
        private readonly List<(List<TableData> PreviewData, WrapPanel Card, ScrollViewer List, Button SelectButton, Button CollapseButton)> _listedPacks = new();

        public MainDesigner(BasicDatabaseDesigner BDD, MainPage mainpage, string mainPath)
        {
            mainPaged = mainpage;
            this.InitializeComponent();
            Designer = BDD;
            BDD.Designers.Add(this);

            // Loading placeholder
            var loadingTitle = BasicDatabaseDesigner.UIHelpers.CreateBasicTitle("Loading...");
            var loadingDesc = BasicDatabaseDesigner.UIHelpers.CreateParagraph("This may take a few moments...");
            loadingTitle.Foreground = new SolidColorBrush(Colors.Black);
            loadingDesc.Foreground = new SolidColorBrush(Colors.Black);
            TemplatesHolder.Children.Add(loadingTitle);
            TemplatesHolder.Children.Add(loadingDesc);

            // Search setup
            SearchBar.Text = "";
            SearchBar.PlaceholderText = "Enter Text To Search!";
            TableViewSearch.Click += OnSearchClicked;

            // Exit
            ExitButton.Click += (s, e) =>
            {
                try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); }
                catch (ArgumentOutOfRangeException) { }
            };

            this.Unloaded += (s, e) => RemoveWindow();

            // Load on page loaded
            Loaded += async (s, e) =>
            {
                TemplatesHolder.Children.Clear();
                await LoadRealRowTemplates(mainPath);
            };
        }

        private void OnSearchClicked(object? sender, RoutedEventArgs e)
        {
            var searchTerm = SearchBar.Text.Trim().ToLower();

            foreach (var pack in _listedPacks)
            {
                var searchable = new StringBuilder();
                foreach (var td in pack.PreviewData)
                {
                    searchable.Append(td.TableName.ToLower()).Append('|')
                               .Append(td.Description.ToLower()).Append('|')
                               .Append(td.TableInfo.ToLower()).AppendLine();
                }

                bool matches = string.IsNullOrEmpty(searchTerm) || searchable.ToString().Contains(searchTerm);

                pack.Card.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                pack.List.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                pack.SelectButton.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                pack.CollapseButton.Content = matches ? "Collapse" : "Expand";
            }

            // If search cleared, collapse all by default
            if (string.IsNullOrEmpty(searchTerm))
            {
                foreach (var pack in _listedPacks)
                {
                    pack.List.Visibility = Visibility.Collapsed;
                    pack.CollapseButton.Content = "Expand";
                }
            }
        }

        private async Task LoadRealRowTemplates(string mainPath)
        {
            var rowTemplatesRoot = Path.Combine(mainPath, "Row Templates");

            if (!Directory.Exists(rowTemplatesRoot))
            {
                Directory.CreateDirectory(rowTemplatesRoot);
                var noTemplates = BasicDatabaseDesigner.UIHelpers.CreateBasicTitle("No Row Templates Found");
                noTemplates.Foreground = new SolidColorBrush(Colors.Black);
                TemplatesHolder.Children.Add(noTemplates);
                return;
            }

            var packFolders = Directory.GetDirectories(rowTemplatesRoot).ToList();

            if (!packFolders.Any())
            {
                var noTemplates = BasicDatabaseDesigner.UIHelpers.CreateBasicTitle("No Row Template Packs Installed");
                noTemplates.Foreground = new SolidColorBrush(Colors.Black);
                TemplatesHolder.Children.Add(noTemplates);
                return;
            }

            foreach (var packFolder in packFolders)
            {
                string packName = Path.GetFileName(packFolder);

                // Find highest version
                var versionFolders = Directory.GetDirectories(packFolder)
                    .Where(d => Path.GetFileName(d).StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
                                int.TryParse(Path.GetFileName(d).AsSpan(1), out _))
                    .OrderByDescending(d => int.Parse(Path.GetFileName(d).AsSpan(1)))
                    .ToList();

                if (!versionFolders.Any()) continue;

                string latestVersionPath = versionFolders.First();
                string templateFile = Directory.GetFiles(latestVersionPath, "Template.DsgnRowTmplate")
                    .FirstOrDefault(f => Path.GetFileName(f).Equals("Template.DsgnRowTmplate", StringComparison.OrdinalIgnoreCase));

                if (templateFile == null) continue;

                string jsonContent = File.ReadAllText(templateFile);
                var json = JsonObject.Parse(jsonContent).AsObject();
                string authorName = json["AuthorName"]?.GetValue<string>() ?? "Unknown Author";

                // Preview data
                var tableDataList = new List<TableData>();
                var dataArray = json["Data"]?.AsArray();
                if (dataArray != null)
                {
                    foreach (var item in dataArray)
                    {
                        var t = item.AsObject();
                        tableDataList.Add(new TableData
                        {
                            TableName = t["TableName"]?.GetValue<string>() ?? "Unnamed Table",
                            Description = t["Description"]?.GetValue<string>() ?? "No description.",
                            TableInfo = "Rows, Constraints, Indexes"
                        });
                    }
                }

                if (tableDataList.Count == 0) continue;

                // Convert full JSON to actual TableObjects
                var tableObjects = ConvertTemplateJsonToTableObjects(jsonContent);

                var scrollViewer = CreateScrollableItems(tableDataList);
                var (collapseButton, card, selectButton) = CreateStatusCard(scrollViewer, packFolder, packName, tableObjects, authorName);

                // Toggle select/deselect logic
                selectButton.Click += (s, e) =>
                {
                    bool isSelected = Designer.TemplateNames.Contains(packName);

                    if (isSelected)
                    {
                        Designer.TemplateNames.Remove(packName);
                        Designer.TemplatePaths.Remove(latestVersionPath);
                        Designer.TemplateProjectRows.Remove(latestVersionPath);
                        selectButton.Content = "Select Template";
                    }
                    else
                    {
                        Designer.TemplateNames.Add(packName);
                        Designer.TemplatePaths.Add(latestVersionPath);
                        Designer.TemplateProjectRows[latestVersionPath] = tableObjects;
                        selectButton.Content = "Deselect Template";
                    }
                };

                // Initial button state
                if (Designer.TemplateNames.Contains(packName))
                {
                    selectButton.Content = "Deselect Template";
                }

                // Store for search
                _listedPacks.Add((tableDataList, card, scrollViewer, selectButton, collapseButton));

                // Add to UI
                TemplatesHolder.Children.Add(card);
                TemplatesHolder.Children.Add(scrollViewer);
                TemplatesHolder.Children.Add(selectButton);
            }
        }

        public List<SessionStorage.TableObject> ConvertTemplateJsonToTableObjects(string templateJsonContent)
        {
            var tableObjects = new List<SessionStorage.TableObject>();
            try
            {
                using var doc = JsonDocument.Parse(templateJsonContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("Data", out JsonElement dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tableElement in dataArray.EnumerateArray())
                    {
                        var tableObject = ParseTableObjectFromTemplate(tableElement);
                        if (tableObject != null)
                        {
                            tableObjects.Add((SessionStorage.TableObject)tableObject);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing template JSON: {ex.Message}");
            }
            return tableObjects;
        }

        private SessionStorage.TableObject? ParseTableObjectFromTemplate(JsonElement tableElement)
        {
            try
            {
                string fullTableName = tableElement.GetProperty("TableName").GetString() ?? "Unnamed Table";
                string schemaName = "public";
                string tableNameOnly = fullTableName;

                if (fullTableName.Contains('.'))
                {
                    var parts = fullTableName.Split('.');
                    schemaName = parts[0];
                    tableNameOnly = parts[1];
                }

                var tableObject = new SessionStorage.TableObject
                {
                    TableName = tableNameOnly,
                    SchemaName = schemaName,
                    Description = tableElement.TryGetProperty("Description", out var d) ? d.GetString() ?? "" : "",
                    Rows = new List<SessionStorage.RowCreation>(),
                    References = new List<SessionStorage.ReferenceOptions>(),
                    Indexes = new List<SessionStorage.IndexCreation>(),
                    CustomRows = new List<string>()
                };

                if (tableElement.TryGetProperty("Rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var rowEl in rowsEl.EnumerateArray())
                    {
                        tableObject.Rows.Add(MainPage.ReadRow(rowEl));
                    }
                }

                if (tableElement.TryGetProperty("References", out var refsEl) && refsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var refEl in refsEl.EnumerateArray())
                    {
                        tableObject.References.Add(MainPage.ReadReference(refEl));
                    }
                }

                if (tableElement.TryGetProperty("Indexes", out var idxEl) && idxEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var idxElItem in idxEl.EnumerateArray())
                    {
                        tableObject.Indexes.Add(MainPage.ReadIndex(idxElItem));
                    }
                }

                if (tableElement.TryGetProperty("CustomRows", out var crEl) && crEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var crItem in crEl.EnumerateArray())
                    {
                        tableObject.CustomRows.Add(crItem.GetString() ?? "");
                    }
                }

                return tableObject;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing table: {ex.Message}");
                return null;
            }
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

        protected override void OnNavigatedTo(NavigationEventArgs e) { }

        public static (Button CollapseButton, WrapPanel Card, Button SelectButton) CreateStatusCard(
            ScrollViewer miniPanel,
            string packRootPath,
            string templateName,
            List<SessionStorage.TableObject> data,
            string authorName = "Author Name")
        {
            var wrap = new WrapPanel
            {
                Width = 1270,
                Height = 195,
                Margin = new Thickness(0, 10, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(0x1C, 0x00, 0x00, 0x00))
            };

            var leftStack = new StackPanel { Width = 128, Height = 167 };
            var pfp = new Image
            {
                Width = 130,
                Height = 130,
                Stretch = Stretch.UniformToFill,
                Source = new BitmapImage(new Uri("/Database_Designer;component/assets/images/default%20pfp.png", UriKind.RelativeOrAbsolute))
            };
            var nameBlock = new TextBlock
            {
                Text = authorName,
                FontSize = 14,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Height = 17,
                Margin = new Thickness(0, 10, 0, 0),
                Foreground = new SolidColorBrush(Colors.Black)
            };
            leftStack.Children.Add(pfp);
            leftStack.Children.Add(nameBlock);

            var rightStack = new StackPanel { Width = 1139, Height = 191 };
            var innerStack = new StackPanel();

            var banner = new Image
            {
                Width = 1137,
                Height = 158,
                Stretch = Stretch.UniformToFill,
                Source = new BitmapImage(new Uri("/Database_Designer;component/assets/images/non-descript%20row%20banner.png", UriKind.RelativeOrAbsolute))
            };

            var collapseButton = new Button
            {
                Content = "Expand",
                Height = 32,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x9D, 0x97, 0x85)),
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = new FontFamily("Assets/Fonts/Inter/Inter_28pt-Light")
            };

            var selectButton = new Button
            {
                Content = "Select Template",
                Height = 32,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x9D, 0x97, 0x85)),
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = new FontFamily("Assets/Fonts/Inter/Inter_28pt-Light"),
                Margin = new Thickness(0, 10, 0, 0)
            };

            miniPanel.Visibility = Visibility.Collapsed;
            collapseButton.Click += (s, e) =>
            {
                if (miniPanel.Visibility == Visibility.Visible)
                {
                    collapseButton.Content = "Expand";
                    miniPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    collapseButton.Content = "Collapse";
                    miniPanel.Visibility = Visibility.Visible;
                }
            };

            innerStack.Children.Add(banner);
            innerStack.Children.Add(collapseButton);
            rightStack.Children.Add(innerStack);

            wrap.Children.Add(leftStack);
            wrap.Children.Add(rightStack);

            // Load images from PACK ROOT (not version folder)
            var bannerPath = Directory.GetFiles(packRootPath)
                .FirstOrDefault(f => Path.GetFileName(f).StartsWith("Banner.", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(bannerPath))
            {
                try { banner.Source = new BitmapImage(new Uri(bannerPath, UriKind.Absolute)); }
                catch { /* fallback to default */ }
            }

            var pfpPath = Directory.GetFiles(packRootPath)
                .FirstOrDefault(f => Path.GetFileName(f).StartsWith("PFP.", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(pfpPath))
            {
                try { pfp.Source = new BitmapImage(new Uri(pfpPath, UriKind.Absolute)); }
                catch { /* fallback to default */ }
            }

            return (collapseButton, wrap, selectButton);
        }

        public struct TableData
        {
            public string TableName;
            public string Description;
            public string TableInfo;
        }

        public static ScrollViewer CreateScrollableItems(List<TableData> data)
        {
            var scroll = new ScrollViewer
            {
                Width = 1269,
                Height = 429,
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Visible
            };

            var wrapPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Width = 1251,
                Background = new SolidColorBrush(Color.FromArgb(0x00, 0xD4, 0xD4, 0xD4))
            };

            foreach (var item in data)
            {
                var gridItem = new Grid { Width = 407, Height = 198, Margin = new Thickness(4) };
                var overlay = new StackPanel
                {
                    Background = Brushes.Transparent,
                    Margin = new Thickness(0, 0, 0, -8),
                    IsHitTestVisible = false
                };

                overlay.Children.Add(new Rectangle
                {
                    Width = 370,
                    Height = 2,
                    Fill = Brushes.Black,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                });

                var usersBox = new Grid
                {
                    Width = 380,
                    Height = 33,
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x51, 0x4F, 0x46)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 14, 0, 0)
                };
                usersBox.Children.Add(new TextBlock
                {
                    Text = item.TableName,
                    FontFamily = new FontFamily("Assets/Fonts/Inter/Inter_28pt-Light"),
                    FontSize = 16,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(14, 0, 10, 0)
                });

                var innerScroll = new ScrollViewer
                {
                    Width = 377,
                    Height = 126,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                };
                var innerStack = new StackPanel();
                innerStack.Children.Add(new TextBlock
                {
                    Text = item.Description,
                    Width = 354,
                    FontSize = 16,
                    FontFamily = new FontFamily("Assets/Fonts/Inter/Inter_28pt-Light"),
                    Foreground = Brushes.Black,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0)
                });
                innerStack.Children.Add(new TextBlock
                {
                    Text = item.TableInfo,
                    Width = 354,
                    FontSize = 16,
                    FontFamily = new FontFamily("Assets/Fonts/Inter/Inter_28pt-Light"),
                    Foreground = Brushes.Black,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
                innerScroll.Content = innerStack;

                overlay.Children.Add(usersBox);
                overlay.Children.Add(innerScroll);
                gridItem.Children.Add(overlay);
                wrapPanel.Children.Add(gridItem);
            }

            scroll.Content = wrapPanel;
            return scroll;
        }
    }
}