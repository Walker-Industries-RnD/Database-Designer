using rm.Trie;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Database_Designer
{
    public partial class FolderViewer : Page
    {
        public MainPage mainPaged;
        public UIWindowEntry WindowInfo { get; private set; }
        public ObservableCollection<string> NavHistory { get; private set; } = new ObservableCollection<string>();
        public int NavIndex { get; private set; } = -1;

        public FolderViewer(MainPage mainPaged, string directory = "")
        {
            InitializeComponent();
            this.mainPaged = mainPaged;
            mainPaged.IntroPage.Children.Remove(this);

            // Start with initial directory
            NavigateTo(directory);

            // Background opacity slider
            var fillBrush = BGPanel.Fill as SolidColorBrush;
            if (fillBrush != null)
            {
                double percentage = Math.Round(fillBrush.Color.A / 255.0 * 100);
                BGSliderValue.Text = "BG: " + percentage.ToString("F0");
                BGSlider.Value = percentage;
            }

            BGSlider.ValueChanged += (s, e) =>
            {
                byte newAlpha = (byte)Math.Round(BGSlider.Value / 100.0 * 255);
                fillBrush.Color = Color.FromArgb(newAlpha, fillBrush.Color.R, fillBrush.Color.G, fillBrush.Color.B);
                BGSliderValue.Text = "BG: " + Math.Round(BGSlider.Value).ToString("F0");
            };

            // Navigation buttons
            Back.Click += (s, e) => GoBack();
            Forward.Click += (s, e) => GoForward();
            Reload.Click += (s, e) => RefreshPage();

            SearchBtn.Click += (s, e) => Search();

            AddToPage.Click += (s, e) =>
            {
                this.mainPaged.CreateWindow(() => new CreateTable(this.mainPaged), "Create New Table", true);
            };

            MainPage.Tables_Changed += (s, e) =>
            {
                this.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    FilesHolderUI.Children.Clear();
                    RefreshPage();
                }));
            };

            ExitButton.Click += (s, e) =>
            {
                try { if (mainPaged.IntroPage.Children.Contains(this)) mainPaged.IntroPage.Children.Remove(this); }
                catch (ArgumentOutOfRangeException) { }
            };

            this.Unloaded += (s, e) => RemoveWindow();
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

        // === Navigation Core ===

        private void NavigateTo(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) path = "";

            // Truncate forward history if we're not at the end
            while (NavHistory.Count > NavIndex + 1)
            {
                NavHistory.RemoveAt(NavHistory.Count - 1);
            }

            NavHistory.Add(path);
            NavIndex = NavHistory.Count - 1;

            AddressBar.Text = path;
            LoadDirectory(path);
            UpdateNavButtons();
        }

        private void GoBack()
        {
            if (NavIndex > 0)
            {
                NavIndex--;
                AddressBar.Text = NavHistory[NavIndex];
                LoadDirectory(NavHistory[NavIndex]);
                UpdateNavButtons();
            }
        }

        private void GoForward()
        {
            if (NavIndex < NavHistory.Count - 1)
            {
                NavIndex++;
                AddressBar.Text = NavHistory[NavIndex];
                LoadDirectory(NavHistory[NavIndex]);
                UpdateNavButtons();
            }
        }

        private void RefreshPage()
        {
            if (NavIndex >= 0 && NavIndex < NavHistory.Count)
            {
                LoadDirectory(NavHistory[NavIndex]);
            }
        }

        private void Search()
        {
            string query = AddressBar.Text.Trim();
            NavigateTo(query);
        }

        private void UpdateNavButtons()
        {
            Back.IsEnabled = NavIndex > 0;
            Forward.IsEnabled = NavIndex < NavHistory.Count - 1;
        }

        // === UI Loading ===

        private void LoadDirectory(string currentPrefix)
        {
            FilesHolderUI.Children.Clear();

            var prefixWords = MainPage.trie.GetWords(currentPrefix).Distinct().ToList();
            int prefixLength = string.IsNullOrEmpty(currentPrefix) ? 0 : currentPrefix.Length + 1; // +1 for dot

            var children = prefixWords
                .Select(item => item.Length > prefixLength ? item.Substring(prefixLength) : "")
                .Where(item => !string.IsNullOrEmpty(item))
                .Distinct()
                .ToList();

            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var groups = children.GroupBy(item => item.Split('.')[0]);

            foreach (var group in groups)
            {
                bool hasDot = group.Any(x => x.Contains('.'));
                string fullName = string.IsNullOrEmpty(currentPrefix)
                    ? group.Key
                    : $"{currentPrefix}.{group.Key}";

                if (hasDot)
                {
                    folders.Add(fullName);
                }
                else
                {
                    files.Add(fullName);
                }
            }

            // Add folders
            foreach (var folderName in folders.OrderBy(f => f))
            {
                var shortcut = CreateShortcut(folderName, "/Database_Designer;component/assets/images/Logos/Folder.png", ShortcutType.Folder);
                FilesHolderUI.Children.Add(shortcut);
            }

            // Add files
            foreach (var fileName in files.OrderBy(f => f))
            {
                var shortcut = CreateShortcut(fileName, "/Database_Designer;component/assets/images/Logos/File.png", ShortcutType.File);
                FilesHolderUI.Children.Add(shortcut);
            }
        }

        public enum ShortcutType { Folder, File, System }

        private Canvas CreateShortcut(string fullPath, string imagePath, ShortcutType type)
        {
            var segments = fullPath.Split('.');
            var displayName = segments.LastOrDefault() ?? "";

            var canvas = new Canvas
            {
                Width = 120,
                Height = 141,
                Margin = new Thickness(6)
            };

            var button = new Button
            {
                Width = 120,
                Height = 140,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromArgb(0x00, 0xD4, 0xD4, 0xD4))
            };

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var image = new Image
            {
                Width = 70,
                Height = 70,
                Margin = new Thickness(0, 4, 0, 0),
                Source = new BitmapImage(new Uri(imagePath, UriKind.RelativeOrAbsolute))
            };

            var textBlock = new TextBlock
            {
                Text = displayName,
                FontSize = 13,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Assets/Fonts/Inter_28pt-SemiBoldItalic.ttf"),
                Foreground = (Brush)Application.Current.Resources["Theme_BackgroundColor"]
            };

            stackPanel.Children.Add(image);
            stackPanel.Children.Add(textBlock);
            button.Content = stackPanel;

            switch (type)
            {
                case ShortcutType.Folder:
                    button.Click += (s, e) => NavigateTo(fullPath);
                    break;

                case ShortcutType.File:
                    button.Click += (s, e) =>
                    {
                        mainPaged.CreateWindow(
                            () => new DatabaseViewer(mainPaged, fullPath),
                            "Folder",
                            true,
                            null,
                            1224,
                            780);
                    };
                    break;

                case ShortcutType.System:
                    break;
            }

            canvas.Children.Add(button);
            return canvas;
        }
    }
}