using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static DatabaseDesigner.SessionStorage;

namespace Database_Designer
{
    // Two-column picker for a foreign key's target: tables on the left, that
    // table's columns on the right. Only columns a foreign key can point at
    // (the primary key or a Unique column) can be picked, and a type that
    // doesn't match the local column is flagged.
    public class ReferenceTargetPicker : Border
    {
        public event Action<string, string> TargetChanged;

        public string SelectedTable { get; private set; }
        public string SelectedColumn { get; private set; }

        private readonly List<TableObject> _tables;
        private readonly Func<RowCreation?> _localColumn;
        private readonly StackPanel _tableList = new StackPanel();
        private readonly StackPanel _columnList = new StackPanel();
        private readonly TextBlock _columnHeader;
        private readonly TextBox _search;

        private static readonly Color Panel = Color.FromArgb(0xFF, 0x45, 0x41, 0x38);
        private static readonly Color Row = Color.FromArgb(0xFF, 0x52, 0x4D, 0x43);
        private static readonly Color Selected = Color.FromArgb(0xFF, 0xF0, 0xE9, 0xD2);
        private static readonly Color Muted = Color.FromArgb(0xFF, 0xB5, 0xAE, 0x9C);
        private static readonly Color Warn = Color.FromArgb(0xFF, 0xE8, 0xB0, 0x4B);
        private static readonly FontFamily Inter = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf");

        public ReferenceTargetPicker(IEnumerable<TableObject> tables, string selectedTable, string selectedColumn, Func<RowCreation?> localColumn)
        {
            _tables = (tables ?? Enumerable.Empty<TableObject>()).Where(t => !string.IsNullOrEmpty(t.TableName)).ToList();
            _localColumn = localColumn;
            SelectedTable = selectedTable;
            SelectedColumn = selectedColumn;

            Background = new SolidColorBrush(Panel);
            CornerRadius = new CornerRadius(6);
            Padding = new Thickness(14);
            Margin = new Thickness(50, 20, 0, 20);
            MaxWidth = 900;
            HorizontalAlignment = HorizontalAlignment.Left;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(440) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(320) });

            grid.Children.Add(Label("Table it points to", 18));
            _columnHeader = Label("Column", 18);
            Grid.SetColumn(_columnHeader, 2);
            grid.Children.Add(_columnHeader);

            _search = new TextBox { FontFamily = Inter, FontSize = 14, Height = 32, Margin = new Thickness(0, 8, 0, 8), PlaceholderText = "Search tables…" };
            _search.TextChanged += (s, e) => RenderTables();
            Grid.SetRow(_search, 1);
            grid.Children.Add(_search);

            var hint = Label("Only the primary key or a Unique column can be the target.", 12, Muted);
            hint.TextWrapping = TextWrapping.Wrap;
            hint.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(hint, 1);
            Grid.SetColumn(hint, 2);
            grid.Children.Add(hint);

            var left = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _tableList };
            Grid.SetRow(left, 2);
            grid.Children.Add(left);
            var right = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _columnList };
            Grid.SetRow(right, 2);
            Grid.SetColumn(right, 2);
            grid.Children.Add(right);

            Child = grid;
            RenderTables();
            RenderColumns();
        }

        // Call when the local (foreign key) column changes so type warnings update.
        public void Refresh() => RenderColumns();

        private TableObject? Find(string qualified) =>
            _tables.Cast<TableObject?>().FirstOrDefault(t => string.Equals(ProjectValidator.Qualified(t.Value), qualified, StringComparison.OrdinalIgnoreCase));

        private void RenderTables()
        {
            _tableList.Children.Clear();
            var filter = _search?.Text?.Trim() ?? "";
            foreach (var t in _tables.OrderBy(t => ProjectValidator.Qualified(t), StringComparer.OrdinalIgnoreCase))
            {
                var name = ProjectValidator.Qualified(t);
                if (filter.Length > 0 && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                int keys = (t.Rows ?? new()).Count(r => ProjectValidator.IsValidFkTarget(t, r));
                bool selected = string.Equals(name, SelectedTable, StringComparison.OrdinalIgnoreCase);
                var item = Item(name, keys == 0 ? "no key column yet" : keys == 1 ? "1 key column" : $"{keys} key columns", selected, true);
                item.MouseLeftButtonUp += (s, e) =>
                {
                    SelectedTable = name;
                    var eligible = (t.Rows ?? new()).Where(r => ProjectValidator.IsValidFkTarget(t, r)).ToList();
                    SelectedColumn = eligible.Count == 1 ? eligible[0].Name
                        : eligible.Any(r => string.Equals(r.Name, SelectedColumn, StringComparison.OrdinalIgnoreCase)) ? SelectedColumn : null;
                    RenderTables();
                    RenderColumns();
                    TargetChanged?.Invoke(SelectedTable, SelectedColumn);
                };
                _tableList.Children.Add(item);
            }
            if (_tableList.Children.Count == 0)
                _tableList.Children.Add(Label(_tables.Count == 0 ? "This project has no tables yet." : "No table matches.", 13, Muted));
        }

        private void RenderColumns()
        {
            _columnList.Children.Clear();
            var table = SelectedTable == null ? null : Find(SelectedTable);
            if (table == null)
            {
                _columnHeader.Text = "Column";
                _columnList.Children.Add(Label("Pick a table on the left.", 13, Muted));
                return;
            }
            _columnHeader.Text = "Column on " + SelectedTable;
            var local = _localColumn?.Invoke();
            foreach (var r in table.Value.Rows ?? new())
            {
                bool eligible = ProjectValidator.IsValidFkTarget(table.Value, r);
                var badges = new List<string>();
                if (r.IsPrimary == true) badges.Add("primary key");
                if (r.IsUnique == true) badges.Add("unique");
                var type = r.RowType?.ToString() ?? "?";
                string detail = type + (badges.Count > 0 ? " · " + string.Join(", ", badges) : "");
                string warning = null;
                if (!eligible)
                    warning = r.IsPrimary == true ? "part of a composite key, can't be a target on its own" : "not unique, can't be a target";
                else if (local?.RowType != null && r.RowType != null &&
                         ProjectValidator.StorageType(local.Value.RowType.Value) != ProjectValidator.StorageType(r.RowType.Value))
                    warning = $"type differs from {local.Value.Name} ({local.Value.RowType}); make {local.Value.Name} {ProjectValidator.StorageType(r.RowType.Value)}";
                bool selected = eligible && string.Equals(r.Name, SelectedColumn, StringComparison.OrdinalIgnoreCase);
                var item = Item(r.Name, detail, selected, eligible, warning);
                if (eligible)
                {
                    var name = r.Name;
                    item.MouseLeftButtonUp += (s, e) =>
                    {
                        SelectedColumn = name;
                        RenderColumns();
                        TargetChanged?.Invoke(SelectedTable, SelectedColumn);
                    };
                }
                _columnList.Children.Add(item);
            }
            if ((table.Value.Rows ?? new()).Count == 0)
                _columnList.Children.Add(Label("This table has no columns.", 13, Muted));
            else if (!(table.Value.Rows ?? new()).Any(r => ProjectValidator.IsValidFkTarget(table.Value, r)))
                _columnList.Children.Add(Label("Mark a column as the primary key or Unique on " + SelectedTable + " first.", 13, Warn));
        }

        private static Border Item(string title, string detail, bool selected, bool enabled, string warning = null)
        {
            var stack = new StackPanel();
            var fg = selected ? Colors.Black : enabled ? Colors.White : Muted;
            stack.Children.Add(new TextBlock { Text = title, FontFamily = Inter, FontSize = 16, Foreground = new SolidColorBrush(fg) });
            stack.Children.Add(new TextBlock { Text = detail, FontFamily = Inter, FontSize = 12, Foreground = new SolidColorBrush(selected ? Color.FromRgb(0x45, 0x41, 0x38) : Muted) });
            if (warning != null)
                stack.Children.Add(new TextBlock { Text = "⚠ " + warning, FontFamily = Inter, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(selected ? Color.FromRgb(0x8A, 0x55, 0x00) : Warn) });
            return new Border
            {
                Background = new SolidColorBrush(selected ? Selected : Row),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 6, 4),
                Opacity = enabled ? 1 : 0.6,
                Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
                Child = stack
            };
        }

        private static TextBlock Label(string text, double size, Color? color = null) => new TextBlock
        {
            Text = text, FontFamily = Inter, FontSize = size,
            Foreground = new SolidColorBrush(color ?? Colors.White)
        };
    }
}
