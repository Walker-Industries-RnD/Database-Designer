using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Database_Designer
{
    public partial class APIPage2Endpoints : Page
    {
        public APIData Data { get; }
        public event Action BackToModules;
        public event Action<APIData> NavigateToFunctions;
        public event Action CloseRequested;
        public event Action SaveRequested;
        public MainPage HostPage { get; }

        public APIPage2Endpoints(APIData data, MainPage host = null)
        {
            Data = data;
            HostPage = host;
            InitializeComponent();
            Loaded += (s, e) => OnNavigatedTo();
            CloseBtn.Click += (s, e) => CloseRequested?.Invoke();
            SaveBtn.Click += (s, e) => SaveRequested?.Invoke();
            CreateNewEndpointBtn.Click += (s, e) => CreateEndpointFromInput();
            BackBtn.Click += (s, e) => BackToModules?.Invoke();
            NextBtn.Click += (s, e) => NavigateToFunctions?.Invoke(Data);
            LoadModuleInfo();
            RebuildEndpoints();
            UpdateStatus();
        }

        public void OnNavigatedTo()
        {
            LoadModuleInfo();
            RebuildEndpoints();
            UpdateStatus();
        }

        private void LoadModuleInfo()
        {
            var module = Data.SelectedModule;
            if (module != null)
            {
                ModuleNameText.Text = module.Name;
                ModuleDescText.Text = module.Description;
                ModuleStatsText.Text = $"{module.Endpoints.Count} endpoints, {module.TotalFunctions} functions";
            }
            else
            {
                ModuleNameText.Text = "No module selected";
                ModuleDescText.Text = "Select a module from page 1";
                ModuleStatsText.Text = "0 endpoints, 0 functions";
            }
        }

        private void RebuildEndpoints()
        {
            EndpointCardsHost.Children.Clear();
            var module = Data.SelectedModule;
            if (module == null || module.Endpoints.Count == 0)
            {
                EmptyStateText.Visibility = Visibility.Visible;
                return;
            }
            EmptyStateText.Visibility = Visibility.Collapsed;

            foreach (var endpoint in module.Endpoints)
            {
                var captured = endpoint;
                var card = BuildEndpointCard(endpoint, isSelected: endpoint == Data.SelectedEndpoint);
                card.MouseLeftButtonUp += (s, e) =>
                {
                    Data.SelectedEndpointIndex = module.Endpoints.IndexOf(captured);
                    RebuildEndpoints();
                    UpdateStatus();
                };
                EndpointCardsHost.Children.Add(card);
            }
        }

        private Border BuildEndpointCard(APIData.APIEndpoint endpoint, bool isSelected)
        {
            var inner = new StackPanel();
            var headerGrid = new Grid();
            headerGrid.Children.Add(new Border
            {
                Width = 32, Height = 32, CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                HorizontalAlignment = HorizontalAlignment.Left
            });
            headerGrid.Children.Add(new TextBlock
            {
                Text = $"{Math.Min(endpoint.Functions.Count, 999)} Functions",
                Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)),
                FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            });
            inner.Children.Add(headerGrid);

            var capturedE = endpoint;
            var titleEl = EditableTextHelpers.EditableText(
                endpoint.Name, v => { capturedE.Name = v; RebuildEndpoints(); },
                fontSize: 14, weight: FontWeights.Bold);
            ((Grid)titleEl).Margin = new Thickness(0, 10, 0, 4);
            inner.Children.Add(titleEl);

            var descEl = EditableTextHelpers.EditableText(
                endpoint.Description, v => { capturedE.Description = v; },
                fontSize: 11, foreground: Color.FromRgb(0x88, 0x88, 0x88),
                wrap: true, maxWidth: 160);
            ((Grid)descEl).Margin = new Thickness(0, 0, 0, 8);
            inner.Children.Add(descEl);

            inner.Children.Add(BuildVerbBar(endpoint));

            return new Border
            {
                Width = 190, Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(14),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                BorderBrush = isSelected
                    ? new SolidColorBrush(Color.FromRgb(0x4C, 0x9C, 0xFF))
                    : new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                CornerRadius = new CornerRadius(6),
                Cursor = Cursors.Hand,
                Child = new Border { Background = Brushes.Transparent, Child = inner }
            };
        }

        private StackPanel BuildVerbBar(APIData.APIEndpoint e)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            if (e.Functions.Count == 0)
            {
                row.Children.Add(new Border
                {
                    Width = 160, Height = 4,
                    Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A))
                });
                return row;
            }
            foreach (var grp in e.Functions.GroupBy(f => f.Verb))
            {
                var color = APIData.VerbColors.TryGetValue(grp.Key, out var c)
                    ? c : Color.FromRgb(0x39, 0xA9, 0x5F);
                row.Children.Add(new Border
                {
                    Width = Math.Max(1, 160 * grp.Count() / Math.Max(1, e.Functions.Count)),
                    Height = 4, Background = new SolidColorBrush(color)
                });
            }
            return row;
        }

        private void CreateEndpointFromInput()
        {
            if (Data.SelectedModule == null) return;
            var n = (NewEndpointNameBox.Text ?? "").Trim();
            if (n.Length == 0) return;
            var e = new APIData.APIEndpoint { Name = n };
            Data.SelectedModule.Endpoints.Add(e);
            NewEndpointNameBox.Text = "";
            Data.SelectedEndpointIndex = Data.SelectedModule.Endpoints.Count - 1;
            LoadModuleInfo();
            RebuildEndpoints();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (Data.SelectedEndpoint != null)
            {
                StatusText.Text = $"Selected: {Data.SelectedEndpoint.Name} ({Data.SelectedEndpoint.Functions.Count} functions)";
                NextBtn.IsEnabled = true;
            }
            else
            {
                StatusText.Text = "Select an endpoint to continue";
                NextBtn.IsEnabled = false;
            }
        }
    }
}