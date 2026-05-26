using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Database_Designer
{
    public partial class RLSPage1Roles : Page
    {
        public RLSData Data { get; }
        public event Action<RLSData> NavigateToTables;
        public event Action CloseRequested;
        public event Action SaveRequested;
        public MainPage HostPage { get; }

        public RLSPage1Roles(RLSData data, MainPage host = null)
        {
            Data = data;
            HostPage = host;
            InitializeComponent();
            Loaded += (s, e) => OnNavigatedTo();
            CloseBtn.Click += (s, e) => CloseRequested?.Invoke();
            SaveBtn.Click += (s, e) => SaveRequested?.Invoke();
            CreateNewRoleBtn.Click += (s, e) => CreateRoleFromInput();
            NextBtn.Click += (s, e) => NavigateToTables?.Invoke(Data);
            RebuildRoles();
            UpdateStatus();
        }

        public void OnNavigatedTo()
        {
            RebuildRoles();
            UpdateStatus();
        }

        private void RebuildRoles()
        {
            RoleCardsHost.Children.Clear();
            foreach (var role in Data.Roles)
            {
                var captured = role;
                var card = BuildRoleCard(role, isSelected: role == Data.SelectedRole, () =>
                {
                    if (Data.SelectedRole == captured)
                    {
                        Data.SelectedRoleIndex = -1;
                        Data.SelectedTableIndex = -1;
                    }
                    Data.Roles.Remove(captured);
                    RebuildRoles();
                    UpdateStatus();
                }, bytes =>
                {
                    captured.IconBytes = bytes;
                    RebuildRoles();
                });
                card.MouseLeftButtonUp += (s, e) =>
                {
                    Data.SelectedRoleIndex = Data.Roles.IndexOf(captured);
                    Data.SelectedTableIndex = -1;
                    RebuildRoles();
                    UpdateStatus();
                };
                RoleCardsHost.Children.Add(card);
            }
        }

        private Border BuildRoleCard(RLSData.PolicyRole role, bool isSelected, Action deleteAction, Action<byte[]> onIconChanged)
        {
            var inner = new StackPanel();
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconBorder = new Border
            {
                Width = 40, Height = 40, CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
            if (role.IconBytes != null && role.IconBytes.Length > 0)
            {
                try
                {
                    var bitmap = ImageHelper.BytesToBitmapImage(role.IconBytes);
                    iconBorder.Child = new Image { Source = bitmap, Stretch = Stretch.Uniform };
                }
                catch { }
            }
            var capturedRole = role;
            iconBorder.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                ImageHelper.SelectAndLoadBytes(bytes =>
                {
                    capturedRole.IconBytes = bytes;
                    onIconChanged?.Invoke(bytes);
                });
            };
            Grid.SetColumn(iconBorder, 0);
            headerGrid.Children.Add(iconBorder);

            headerGrid.Children.Add(new TextBlock
            {
                Text = $"{Math.Min(role.TotalPolicies, 999)} Policies",
                Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)),
                FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            });
            inner.Children.Add(headerGrid);

            var titleEl = EditableTextHelpers.EditableText(
                role.Name, v => { role.Name = v; RebuildRoles(); },
                fontSize: 15, weight: FontWeights.Bold);
            ((Grid)titleEl).Margin = new Thickness(0, 12, 0, 4);
            inner.Children.Add(titleEl);

            var descEl = EditableTextHelpers.EditableText(
                role.Description, v => { role.Description = v; },
                fontSize: 11, foreground: Color.FromRgb(0x88, 0x88, 0x88),
                wrap: true, maxWidth: 180);
            ((Grid)descEl).Margin = new Thickness(0, 0, 0, 8);
            inner.Children.Add(descEl);

            var bottomRow = new Grid();
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tableCount = new TextBlock
            {
                Text = $"{role.Tables.Count} Tables",
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x4C, 0x4C)),
                FontSize = 11, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tableCount, 0);
            bottomRow.Children.Add(tableCount);

            var deleteBtn = new Button
            {
                Content = "X",
                Width = 24, Height = 24,
                Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x4C, 0x4C)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x4C, 0x4C)),
                BorderThickness = new Thickness(1),
                FontSize = 11, FontWeight = FontWeights.Bold,
                Margin = new Thickness(4, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            deleteBtn.Click += (s, e) => { e.Handled = true; deleteAction?.Invoke(); };
            Grid.SetColumn(deleteBtn, 1);
            bottomRow.Children.Add(deleteBtn);

            inner.Children.Add(bottomRow);

            return new Border
            {
                Width = 200, Margin = new Thickness(0, 0, 12, 12),
                Padding = new Thickness(16),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                BorderBrush = isSelected
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0x4C, 0x4C))
                    : new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                CornerRadius = new CornerRadius(6),
                Cursor = Cursors.Hand,
                Child = new Border { Background = Brushes.Transparent, Child = inner }
            };
        }

        private void CreateRoleFromInput()
        {
            var n = (NewRoleNameBox.Text ?? "").Trim();
            if (n.Length == 0) return;
            Data.Roles.Add(new RLSData.PolicyRole { Name = n });
            NewRoleNameBox.Text = "";
            Data.SelectedRoleIndex = Data.Roles.Count - 1;
            Data.SelectedTableIndex = -1;
            RebuildRoles();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (Data.SelectedRole != null)
            {
                StatusText.Text = $"Selected: {Data.SelectedRole.Name} ({Data.SelectedRole.Tables.Count} tables, {Data.SelectedRole.TotalPolicies} policies)";
                NextBtn.IsEnabled = true;
            }
            else
            {
                StatusText.Text = "Select a role to continue";
                NextBtn.IsEnabled = false;
            }
        }
    }
}