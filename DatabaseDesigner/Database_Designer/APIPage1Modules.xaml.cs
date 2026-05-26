using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Database_Designer
{
    public partial class APIPage1Modules : Page
    {
        public APIData Data { get; }
        public event Action<APIData> NavigateToEndpoints;
        public event Action CloseRequested;
        public event Action SaveRequested;
        public MainPage HostPage { get; }

        public APIPage1Modules(APIData data, MainPage host = null)
        {
            Data = data;
            HostPage = host;
            InitializeComponent();
            Loaded += (s, e) => OnNavigatedTo();
            CloseBtn.Click += (s, e) => CloseRequested?.Invoke();
            SaveBtn.Click += (s, e) => SaveRequested?.Invoke();
            CreateNewModuleBtn.Click += (s, e) => CreateModuleFromInput();
            NextBtn.Click += (s, e) => NavigateToEndpoints?.Invoke(Data);
            RebuildModules();
            UpdateStatus();
        }

        public void OnNavigatedTo()
        {
            RebuildModules();
            UpdateStatus();
        }

        private void RebuildModules()
        {
            ModuleCardsHost.Children.Clear();
            foreach (var module in Data.Modules)
            {
                var captured = module;
                var card = BuildModuleCard(module, isSelected: module == Data.SelectedModule, bytes =>
                {
                    captured.IconBytes = bytes;
                    RebuildModules();
                });
                card.MouseLeftButtonUp += (s, e) =>
                {
                    Data.SelectedModuleIndex = Data.Modules.IndexOf(captured);
                    Data.SelectedEndpointIndex = -1;
                    RebuildModules();
                    UpdateStatus();
                };
                ModuleCardsHost.Children.Add(card);
            }
        }

        private Border BuildModuleCard(APIData.APIModule module, bool isSelected, Action<byte[]> onIconChanged)
        {
            var inner = new StackPanel();
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBorder = new Border
            {
                Width = 40, Height = 40, CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
            if (module.IconBytes != null && module.IconBytes.Length > 0)
            {
                try
                {
                    var bitmap = ImageHelper.BytesToBitmapImage(module.IconBytes);
                    iconBorder.Child = new Image { Source = bitmap, Stretch = Stretch.Uniform };
                }
                catch { }
            }
            var capturedModule = module;
            iconBorder.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                ImageHelper.SelectAndLoadBytes(bytes =>
                {
                    capturedModule.IconBytes = bytes;
                    onIconChanged?.Invoke(bytes);
                });
            };
            Grid.SetColumn(iconBorder, 0);
            headerGrid.Children.Add(iconBorder);

            headerGrid.Children.Add(new TextBlock
            {
                Text = $"{Math.Min(module.TotalFunctions, 999)} Functions",
                Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)),
                FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            });
            inner.Children.Add(headerGrid);

            var titleEl = EditableTextHelpers.EditableText(
                module.Name, v => { module.Name = v; RebuildModules(); },
                fontSize: 15, weight: FontWeights.Bold);
            ((Grid)titleEl).Margin = new Thickness(0, 12, 0, 4);
            inner.Children.Add(titleEl);

            var descEl = EditableTextHelpers.EditableText(
                module.Description, v => { module.Description = v; },
                fontSize: 11, foreground: Color.FromRgb(0x88, 0x88, 0x88),
                wrap: true, maxWidth: 180);
            ((Grid)descEl).Margin = new Thickness(0, 0, 0, 8);
            inner.Children.Add(descEl);

            var endpointCount = new TextBlock
            {
                Text = $"{module.Endpoints.Count} Endpoints",
                Foreground = new SolidColorBrush(Color.FromRgb(0x39, 0xA9, 0x5F)),
                FontSize = 11, FontWeight = FontWeights.Bold
            };
            inner.Children.Add(endpointCount);

            return new Border
            {
                Width = 200, Margin = new Thickness(0, 0, 12, 12),
                Padding = new Thickness(16),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                BorderBrush = isSelected
                    ? new SolidColorBrush(Color.FromRgb(0x39, 0xA9, 0x5F))
                    : new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                CornerRadius = new CornerRadius(6),
                Cursor = Cursors.Hand,
                Child = new Border { Background = Brushes.Transparent, Child = inner }
            };
        }

        private void CreateModuleFromInput()
        {
            var n = (NewModuleNameBox.Text ?? "").Trim();
            if (n.Length == 0) return;
            Data.Modules.Add(new APIData.APIModule { Name = n });
            NewModuleNameBox.Text = "";
            Data.SelectedModuleIndex = Data.Modules.Count - 1;
            Data.SelectedEndpointIndex = -1;
            RebuildModules();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (Data.SelectedModule != null)
            {
                StatusText.Text = $"Selected: {Data.SelectedModule.Name} ({Data.SelectedModule.Endpoints.Count} endpoints, {Data.SelectedModule.TotalFunctions} functions)";
                NextBtn.IsEnabled = true;
            }
            else
            {
                StatusText.Text = "Select a module to continue";
                NextBtn.IsEnabled = false;
            }
        }
    }
}