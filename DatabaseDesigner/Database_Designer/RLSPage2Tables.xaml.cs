using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Database_Designer
{
    public partial class RLSPage2Tables : Page
    {
        public RLSData Data { get; }
        public event Action BackToRoles;
        public event Action<RLSData> NavigateToPolicies;
        public event Action CloseRequested;
        public event Action SaveRequested;
        public MainPage HostPage { get; }

        public RLSPage2Tables(RLSData data, MainPage host = null)
        {
            Data = data;
            HostPage = host;
            InitializeComponent();
            Loaded += (s, e) => OnNavigatedTo();
            CloseBtn.Click += (s, e) => CloseRequested?.Invoke();
            SaveBtn.Click += (s, e) => SaveRequested?.Invoke();
            CreateNewTableBtn.Click += (s, e) => CreateTableFromInput();
            BackBtn.Click += (s, e) => BackToRoles?.Invoke();
            NextBtn.Click += (s, e) => NavigateToPolicies?.Invoke(Data);
            LoadRoleInfo();
            RebuildTables();
            UpdateStatus();
        }

        public void OnNavigatedTo()
        {
            LoadRoleInfo();
            RebuildTables();
            UpdateStatus();
        }

        private void LoadRoleInfo()
        {
            var role = Data.SelectedRole;
            if (role != null)
            {
                RoleNameText.Text = role.Name;
                RoleDescText.Text = role.Description;
                RoleStatsText.Text = $"{role.Tables.Count} tables, {role.TotalPolicies} policies";
            }
            else
            {
                RoleNameText.Text = "No role selected";
                RoleDescText.Text = "Select a role from page 1";
                RoleStatsText.Text = "0 tables, 0 policies";
            }
        }

        private void RebuildTables()
        {
            TableCardsHost.Children.Clear();
            var role = Data.SelectedRole;
            if (role == null || role.Tables.Count == 0)
            {
                EmptyStateText.Visibility = Visibility.Visible;
                return;
            }
            EmptyStateText.Visibility = Visibility.Collapsed;

            foreach (var table in role.Tables)
            {
                var captured = table;
                var card = BuildTableCard(table, isSelected: table == Data.SelectedTable);
                card.MouseLeftButtonUp += (s, e) =>
                {
                    Data.SelectedTableIndex = role.Tables.IndexOf(captured);
                    RebuildTables();
                    UpdateStatus();
                };
                TableCardsHost.Children.Add(card);
            }
        }

        private Border BuildTableCard(RLSData.TablePolicies table, bool isSelected)
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
                Text = $"{Math.Min(table.Policies.Count, 999)} Policies",
                Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)),
                FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            });
            inner.Children.Add(headerGrid);

            var capturedT = table;
            var titleEl = EditableTextHelpers.EditableText(
                table.TableName, v => { capturedT.TableName = v; RebuildTables(); },
                fontSize: 14, weight: FontWeights.Bold);
            ((Grid)titleEl).Margin = new Thickness(0, 10, 0, 4);
            inner.Children.Add(titleEl);

            var descEl = EditableTextHelpers.EditableText(
                table.Description, v => { capturedT.Description = v; },
                fontSize: 11, foreground: Color.FromRgb(0x88, 0x88, 0x88),
                wrap: true, maxWidth: 160);
            ((Grid)descEl).Margin = new Thickness(0, 0, 0, 8);
            inner.Children.Add(descEl);

            inner.Children.Add(BuildCategoryBar(table));

            return new Border
            {
                Width = 190, Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(14),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                BorderBrush = isSelected
                    ? new SolidColorBrush(Color.FromRgb(0x7B, 0x3C, 0xFF))
                    : new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                CornerRadius = new CornerRadius(6),
                Cursor = Cursors.Hand,
                Child = new Border { Background = Brushes.Transparent, Child = inner }
            };
        }

        private StackPanel BuildCategoryBar(RLSData.TablePolicies t)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            if (t.Policies.Count == 0)
            {
                row.Children.Add(new Border
                {
                    Width = 160, Height = 4,
                    Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A))
                });
                return row;
            }
            foreach (var grp in t.Policies.GroupBy(p => p.Category))
            {
                var color = RLSData.CategoryColors.TryGetValue(grp.Key, out var c)
                    ? c : Color.FromRgb(0xFF, 0x4C, 0x4C);
                row.Children.Add(new Border
                {
                    Width = Math.Max(1, 160 * grp.Count() / Math.Max(1, t.Policies.Count)),
                    Height = 4, Background = new SolidColorBrush(color)
                });
            }
            return row;
        }

        private void CreateTableFromInput()
        {
            if (Data.SelectedRole == null) return;
            var n = (NewTableNameBox.Text ?? "").Trim();
            if (n.Length == 0) return;
            var t = new RLSData.TablePolicies { TableName = n };
            Data.SelectedRole.Tables.Add(t);
            NewTableNameBox.Text = "";
            Data.SelectedTableIndex = Data.SelectedRole.Tables.Count - 1;
            LoadRoleInfo();
            RebuildTables();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (Data.SelectedTable != null)
            {
                StatusText.Text = $"Selected: {Data.SelectedTable.TableName} ({Data.SelectedTable.Policies.Count} policies)";
                NextBtn.IsEnabled = true;
            }
            else
            {
                StatusText.Text = "Select a table to continue";
                NextBtn.IsEnabled = false;
            }
        }
    }
}