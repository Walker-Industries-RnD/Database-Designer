using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Database_Designer
{
    public partial class RLSPage3Policies : Page
    {
        public RLSData Data { get; }
        public event Action BackToTables;
        public event Action CloseRequested;
        public event Action SaveRequested;
        public MainPage HostPage { get; }

        public RLSPage3Policies(RLSData data, MainPage host = null)
        {
            Data = data;
            HostPage = host;
            InitializeComponent();
            Loaded += (s, e) => OnNavigatedTo();
            CloseBtn.Click += (s, e) => CloseRequested?.Invoke();
            SaveBtn.Click += (s, e) => SaveRequested?.Invoke();
            CreateNewPolicyBtn.Click += (s, e) => CreatePolicyFromInput();
            BackBtn.Click += (s, e) => BackToTables?.Invoke();
            LoadTableInfo();
            RebuildPolicies();
            RebuildCategoryLegend();
        }

        public void OnNavigatedTo()
        {
            LoadTableInfo();
            RebuildPolicies();
            RebuildCategoryLegend();
        }

        private void LoadTableInfo()
        {
            var table = Data.SelectedTable;
            if (table != null)
            {
                TableNameText.Text = table.TableName;
                TableDescText.Text = table.Description;
                TableStatsText.Text = $"{table.Policies.Count} policies";
            }
            else
            {
                TableNameText.Text = "No table selected";
                TableDescText.Text = "Select a table from page 2";
                TableStatsText.Text = "0 policies";
            }
        }

        private void RebuildCategoryLegend()
        {
            CategoryLegendHost.Items.Clear();
            foreach (var (label, desc) in new (string, string)[]
            {
                ("Base Server",   "Required for server operation"),
                ("Communication", "Messages, chat, servers"),
                ("Profiles",      "Profile creation & management"),
                ("Economy",       "Marketplaces & payments"),
            })
            {
                var color = RLSData.CategoryColors.TryGetValue(label, out var c)
                    ? c : Color.FromRgb(0xFF, 0x4C, 0x4C);
                var stack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                stack.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 8, Height = 8, Fill = new SolidColorBrush(color),
                    Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center
                });
                stack.Children.Add(new TextBlock
                {
                    Text = label, Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                    FontSize = 10, VerticalAlignment = VerticalAlignment.Center
                });
                CategoryLegendHost.Items.Add(stack);
            }
        }

        private void RebuildPolicies()
        {
            PolicyRowsHost.Items.Clear();
            var table = Data.SelectedTable;
            if (table == null || table.Policies.Count == 0)
            {
                EmptyStateText.Visibility = Visibility.Visible;
                return;
            }
            EmptyStateText.Visibility = Visibility.Collapsed;

            foreach (var p in table.Policies)
            {
                var captured = p;
                PolicyRowsHost.Items.Add(BuildPolicyRow(p, () =>
                {
                    table.Policies.Remove(captured);
                    RebuildPolicies();
                    RebuildCategoryLegend();
                    LoadTableInfo();
                }));
            }
        }

        private Border BuildPolicyRow(RLSData.Policy p, Action deleteAction)
        {
            var color = RLSData.CategoryColors.TryGetValue(p.Category, out var c)
                ? c : Color.FromRgb(0xFF, 0x4C, 0x4C);
            var captured = p;

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var swatch = new Border
            {
                Width = 16, Height = 16, CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(swatch, 0);
            rowGrid.Children.Add(swatch);

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameEl = EditableTextHelpers.EditableText(
                p.Name, v => { captured.Name = v; }, fontSize: 13, weight: FontWeights.Bold);
            textStack.Children.Add(nameEl);
            var descEl = EditableTextHelpers.EditableText(
                p.Description, v => { captured.Description = v; },
                fontSize: 11, foreground: Color.FromRgb(0x88, 0x88, 0x88), wrap: true);
            ((Grid)descEl).Margin = new Thickness(0, 4, 0, 0);
            textStack.Children.Add(descEl);

            var tagEl = EditableTextHelpers.EditableText(
                string.IsNullOrEmpty(captured.Tag) ? "Add tag..." : captured.Tag,
                v => { captured.Tag = v; },
                fontSize: 10, foreground: Color.FromRgb(0x55, 0x55, 0x55), maxWidth: 200);
            ((Grid)tagEl).Margin = new Thickness(0, 4, 0, 0);
            textStack.Children.Add(tagEl);
            Grid.SetColumn(textStack, 1);
            rowGrid.Children.Add(textStack);

            var openBtn = EditableTextHelpers.OpenInNodeWalkerButton(() =>
            {
                if (HostPage == null) return;
                var slug = $"RLS_{Slug(Data.SelectedRole?.Name)}_{Slug(Data.SelectedTable?.TableName)}_{Slug(captured.Name)}";
                HostPage.CreateWindow(
                    () => new NodeWalker.NodeWalkerWindow(HostPage) { Tag = slug },
                    "NODEWLKR — " + captured.Name,
                    true, null,
                    Math.Max(900, HostPage.IntroPage.ActualWidth * 0.85),
                    Math.Max(600, HostPage.IntroPage.ActualHeight * 0.85));
            });
            Grid.SetColumn(openBtn, 2);
            rowGrid.Children.Add(openBtn);

            var deleteBtn = new Button
            {
                Content = "X",
                Width = 28, Height = 28,
                Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x4C, 0x4C)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x4C, 0x4C)),
                BorderThickness = new Thickness(1),
                FontSize = 12, FontWeight = FontWeights.Bold,
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            deleteBtn.Click += (s, e) => deleteAction?.Invoke();
            Grid.SetColumn(deleteBtn, 3);
            rowGrid.Children.Add(deleteBtn);

            return new Border
            {
                Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(16),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(0, 0, 0, 2),
                CornerRadius = new CornerRadius(4),
                Child = rowGrid
            };
        }

        private static string Slug(string s) =>
            string.IsNullOrWhiteSpace(s) ? "untitled" :
            new string(s.Where(ch => char.IsLetterOrDigit(ch)).ToArray()).ToLowerInvariant();

        private void CreatePolicyFromInput()
        {
            if (Data.SelectedTable == null) return;
            var n = (NewPolicyNameBox.Text ?? "").Trim();
            if (n.Length == 0) return;
            var category = (CategoryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Base Server";
            Data.SelectedTable.Policies.Add(new RLSData.Policy { Name = n, Category = category });
            NewPolicyNameBox.Text = "";
            LoadTableInfo();
            RebuildPolicies();
            RebuildCategoryLegend();
        }
    }
}