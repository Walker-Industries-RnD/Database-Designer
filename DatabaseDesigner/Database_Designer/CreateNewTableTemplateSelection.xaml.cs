using DatabaseDesigner;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
    public partial class CreateNewTableTemplateSelection : Page
    {
        internal static CreateTable Designer;
        internal static MainPage mainPage;
        public UIWindowEntry WindowInfo { get; private set; }

        // Tracks UI elements for each template pack so we can filter them during search
        private readonly List<(List<TableData> Data, WrapPanel Card, ScrollViewer List, Button SelectButton, string JsonContent)> _listedPacks = new();

        public CreateNewTableTemplateSelection(CreateTable BDD, MainPage mainpage, string mainPath)
        {
            mainPage = mainpage;
            this.InitializeComponent();
            Designer = BDD;

            // Initial loading placeholder
            var loadingTitle = BasicDatabaseDesigner.UIHelpers.CreateBasicTitle("Loading Templates...");
            var loadingDesc = BasicDatabaseDesigner.UIHelpers.CreateParagraph("Scanning folder for template packs...");
            loadingTitle.Foreground = new SolidColorBrush(Colors.Black);
            loadingDesc.Foreground = new SolidColorBrush(Colors.Black);
            TemplatesHolder.Children.Add(loadingTitle);
            TemplatesHolder.Children.Add(loadingDesc);

            // Search functionality
            SearchBar.Text = "";
            SearchBar.PlaceholderText = "Enter Text To Search!";
            TableViewSearch.Click += OnSearchClicked;

            // Exit button
            ExitButton.Click += (s, e) =>
            {
                try { if (mainPage.IntroPage.Children.Contains(this)) mainPage.IntroPage.Children.Remove(this); }
                catch (ArgumentOutOfRangeException) { }
            };

            this.Unloaded += (s, e) => RemoveWindow();

            // Load templates asynchronously after page is loaded
            Loaded += async (s, e) =>
            {
                TemplatesHolder.Children.Clear();
                await LoadRealTemplates(mainPath);
            };
        }

        private void OnSearchClicked(object? s, RoutedEventArgs e)
        {
            var searchTerm = SearchBar.Text.Trim().ToLower();
            foreach (var pack in _listedPacks)
            {
                // Build searchable text from all table names + descriptions
                var searchable = new StringBuilder();
                foreach (var td in pack.Data)
                {
                    searchable.Append(td.TableName.ToLower()).Append('|')
                               .Append(td.Description.ToLower()).Append('|')
                               .Append(td.TableInfo.ToLower()).AppendLine();
                }

                bool matches = string.IsNullOrEmpty(searchTerm) || searchable.ToString().Contains(searchTerm);

                pack.Card.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                pack.List.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                pack.SelectButton.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private async Task LoadRealTemplates(string mainPath)
        {
            var templatesRoot = Path.Combine(mainPath, "Templates");

            if (!Directory.Exists(templatesRoot))
            {
                Directory.CreateDirectory(templatesRoot);
                var noTemplates = BasicDatabaseDesigner.UIHelpers.CreateBasicTitle("No Templates Found");
                noTemplates.Foreground = new SolidColorBrush(Colors.Black);
                TemplatesHolder.Children.Add(noTemplates);
                return;
            }

            var packFolders = Directory.GetDirectories(templatesRoot).ToList();

            if (!packFolders.Any())
            {
                var noTemplates = BasicDatabaseDesigner.UIHelpers.CreateBasicTitle("No Template Packs Found");
                noTemplates.Foreground = new SolidColorBrush(Colors.Black);
                TemplatesHolder.Children.Add(noTemplates);
                return;
            }

            foreach (var packFolder in packFolders)
            {
                string packName = Path.GetFileName(packFolder);

                // Find latest version folder
                var versionFolders = Directory.GetDirectories(packFolder)
                    .Where(d => Path.GetFileName(d).StartsWith("v") && int.TryParse(Path.GetFileName(d).AsSpan(1), out _))
                    .OrderByDescending(d => int.Parse(Path.GetFileName(d).AsSpan(1)))
                    .ToList();

                if (!versionFolders.Any()) continue;

                string latestVersionPath = versionFolders.First();
                string templateFile = Directory.GetFiles(latestVersionPath, "Template.DsgnRowTmplate").FirstOrDefault();
                if (templateFile == null) continue;

                string jsonContent = File.ReadAllText(templateFile);
                var json = JsonObject.Parse(jsonContent).AsObject();

                string authorName = json["AuthorName"]?.GetValue<string>() ?? "Unknown Author";

                // Parse table list for preview
                var tableDataList = new List<TableData>();
                var dataArray = json["Data"]?.AsArray();
                if (dataArray != null)
                {
                    foreach (var item in dataArray)
                    {
                        var obj = item.AsObject();
                        tableDataList.Add(new TableData
                        {
                            TableName = obj["TableName"]?.GetValue<string>() ?? "Unnamed Table",
                            Description = obj["Description"]?.GetValue<string>() ?? "No description.",
                            TableInfo = "Rows, Constraints, Indexes"
                        });
                    }
                }

                if (tableDataList.Count == 0) continue;

                // Create UI elements
                var scrollViewer = CreateScrollableItems(tableDataList);
                var statusCard = CreateStatusCard(scrollViewer, packFolder, packName, tableDataList, authorName);

                var selectButton = new Button
                {
                    Content = "Select Template Pack",
                    Height = 32,
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x9D, 0x97, 0x85)),
                    Foreground = new SolidColorBrush(Colors.White),
                    FontFamily = new FontFamily("Assets/Fonts/Inter/Inter_28pt-Light"),
                    Margin = new Thickness(0, 10, 0, 20)
                };

                selectButton.Click += (sender, args) =>
                {
                    try
                    {
                        var templateItems = MainPage.ParseTemplatePack(jsonContent);
                        if (templateItems.Count == 0) return;

                        foreach (var (fullTableName, description, rows, references, indexes) in templateItems)
                        {
                            DBDesigner.DatabaseDesigner(
                                fullTableName,
                                description,
                                rows,
                                null,
                                references,
                                indexes
                            );
                        }

                        Console.WriteLine($"Successfully imported {templateItems.Count} table(s) from '{packName}'!");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to import template pack '{packName}':\n{ex.Message}");
                    }
                };

                // Store for search filtering
                _listedPacks.Add((tableDataList, statusCard.Item2, scrollViewer, selectButton, jsonContent));

                // Add to UI
                TemplatesHolder.Children.Add(statusCard.Item2);     // Card (banner + author)
                TemplatesHolder.Children.Add(scrollViewer);          // Table preview list
                TemplatesHolder.Children.Add(selectButton);          // Select button
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

        protected override void OnNavigatedTo(NavigationEventArgs e) { }


        public static (Button CollapseButton, WrapPanel Card) CreateStatusCard(
            ScrollViewer miniPanel,
            string packRootPath,
            string templateName,
            List<TableData> data,
            string authorName = "Author Name")
        {
            var wrap = new WrapPanel
            {
                Width = 1270,
                Height = 195,
                Margin = new Thickness(0, 10, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(0x1C, 0x00, 0x00, 0x00))
            };

            // Left: Profile picture + name
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

            // Right: Banner + Expand/Collapse button
            var rightStack = new StackPanel { Width = 1139, Height = 191 };
            var innerStack = new StackPanel();
            var banner = new Image
            {
                Width = 1137,
                Height = 158,
                HorizontalAlignment = HorizontalAlignment.Left,
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

            // Load banner and PFP from pack root
            var finalBanner = Directory.GetFiles(packRootPath)
                .FirstOrDefault(f => Path.GetFileName(f).StartsWith("Banner.", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(finalBanner))
            {
                try
                {
                    banner.Source = new BitmapImage(new Uri(finalBanner, UriKind.Absolute));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load banner: {ex.Message}");
                }
            }

            var finalPfp = Directory.GetFiles(packRootPath)
                .FirstOrDefault(f => Path.GetFileName(f).StartsWith("PFP.", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(finalPfp))
            {
                try
                {
                    pfp.Source = new BitmapImage(new Uri(finalPfp, UriKind.Absolute));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load PFP: {ex.Message}");
                }
            }

            return (collapseButton, wrap);
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
                var gridItem = new Grid
                {
                    Width = 407,
                    Height = 198,
                    Margin = new Thickness(4)
                };

                var overlay = new StackPanel
                {
                    Background = new SolidColorBrush(Colors.Transparent),
                    Margin = new Thickness(0, 0, 0, -8),
                    IsHitTestVisible = false
                };

                var separator = new Rectangle
                {
                    Width = 370,
                    Height = 2,
                    Fill = new SolidColorBrush(Colors.Black),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                };

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
                    Foreground = new SolidColorBrush(Colors.White),
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
                    Foreground = new SolidColorBrush(Colors.Black),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0)
                });
                innerStack.Children.Add(new TextBlock
                {
                    Text = item.TableInfo,
                    Width = 354,
                    FontSize = 16,
                    FontFamily = new FontFamily("Assets/Fonts/Inter/Inter_28pt-Light"),
                    Foreground = new SolidColorBrush(Colors.Black),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
                innerScroll.Content = innerStack;

                overlay.Children.Add(separator);
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