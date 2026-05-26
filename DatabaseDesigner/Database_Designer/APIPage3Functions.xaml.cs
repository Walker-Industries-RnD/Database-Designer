using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Database_Designer
{
    public partial class APIPage3Functions : Page
    {
        public APIData Data { get; }
        public event Action BackToEndpoints;
        public event Action CloseRequested;
        public event Action SaveRequested;
        public MainPage HostPage { get; }

        public APIPage3Functions(APIData data, MainPage host = null)
        {
            Data = data;
            HostPage = host;
            InitializeComponent();
            Loaded += (s, e) => OnNavigatedTo();
            CloseBtn.Click += (s, e) => CloseRequested?.Invoke();
            SaveBtn.Click += (s, e) => SaveRequested?.Invoke();
            CreateNewFunctionBtn.Click += (s, e) => CreateFunctionFromInput();
            BackBtn.Click += (s, e) => BackToEndpoints?.Invoke();
            LoadEndpointInfo();
            RebuildFunctions();
            RebuildVerbLegend();
        }

        public void OnNavigatedTo()
        {
            LoadEndpointInfo();
            RebuildFunctions();
            RebuildVerbLegend();
        }

        private void LoadEndpointInfo()
        {
            var endpoint = Data.SelectedEndpoint;
            if (endpoint != null)
            {
                EndpointNameText.Text = endpoint.Name;
                EndpointDescText.Text = endpoint.Description;
                EndpointStatsText.Text = $"{endpoint.Functions.Count} functions";
            }
            else
            {
                EndpointNameText.Text = "No endpoint selected";
                EndpointDescText.Text = "Select an endpoint from page 2";
                EndpointStatsText.Text = "0 functions";
            }
        }

        private void RebuildVerbLegend()
        {
            VerbLegendHost.Items.Clear();
            foreach (var verb in new[] { "GET", "POST", "PUT", "DELETE", "PATCH" })
            {
                var color = APIData.VerbColors.TryGetValue(verb, out var c)
                    ? c : Color.FromRgb(0x39, 0xA9, 0x5F);
                var stack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                stack.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 8, Height = 8, Fill = new SolidColorBrush(color),
                    Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center
                });
                stack.Children.Add(new TextBlock
                {
                    Text = verb, Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                    FontSize = 10, VerticalAlignment = VerticalAlignment.Center
                });
                VerbLegendHost.Items.Add(stack);
            }
        }

        private void RebuildFunctions()
        {
            FunctionRowsHost.Items.Clear();
            var endpoint = Data.SelectedEndpoint;
            if (endpoint == null || endpoint.Functions.Count == 0)
            {
                EmptyStateText.Visibility = Visibility.Visible;
                return;
            }
            EmptyStateText.Visibility = Visibility.Collapsed;

            foreach (var f in endpoint.Functions)
            {
                var captured = f;
                FunctionRowsHost.Items.Add(BuildFunctionRow(f, () =>
                {
                    endpoint.Functions.Remove(captured);
                    RebuildFunctions();
                    RebuildVerbLegend();
                    LoadEndpointInfo();
                }));
            }
        }

        private Border BuildFunctionRow(APIData.APIFunction f, Action deleteAction)
        {
            var color = APIData.VerbColors.TryGetValue(f.Verb, out var c)
                ? c : Color.FromRgb(0x39, 0xA9, 0x5F);
            var captured = f;

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var verbBadge = new Border
            {
                Background = new SolidColorBrush(color),
                Padding = new Thickness(8, 4, 8, 4),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            verbBadge.Child = new TextBlock
            {
                Text = f.Verb,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            Grid.SetColumn(verbBadge, 0);
            rowGrid.Children.Add(verbBadge);

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameEl = EditableTextHelpers.EditableText(
                f.Name, v => { captured.Name = v; }, fontSize: 13, weight: FontWeights.Bold);
            textStack.Children.Add(nameEl);
            var descEl = EditableTextHelpers.EditableText(
                f.Description, v => { captured.Description = v; },
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
                var slug = $"API_{Slug(Data.SelectedModule?.Name)}_{Slug(Data.SelectedEndpoint?.Name)}_{Slug(captured.Name)}";
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

        private void CreateFunctionFromInput()
        {
            if (Data.SelectedEndpoint == null) return;
            var n = (NewFunctionNameBox.Text ?? "").Trim();
            if (n.Length == 0) return;
            var verb = (VerbComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "GET";
            Data.SelectedEndpoint.Functions.Add(new APIData.APIFunction { Name = n, Verb = verb });
            NewFunctionNameBox.Text = "";
            LoadEndpointInfo();
            RebuildFunctions();
            RebuildVerbLegend();
        }
    }
}