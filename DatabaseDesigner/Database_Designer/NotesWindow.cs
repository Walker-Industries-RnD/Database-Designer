using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Database_Designer
{
    public class ProjectNote
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "Untitled note";
        public string Body { get; set; } = "";
        public string LinkedTable { get; set; }
        public bool Pinned { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime Updated { get; set; } = DateTime.Now;

        public (int done, int total) Checklist()
        {
            var lines = (Body ?? "").Split('\n').Select(l => l.TrimStart());
            int total = 0, done = 0;
            foreach (var l in lines)
            {
                if (l.StartsWith("[ ]")) total++;
                else if (l.StartsWith("[x]", StringComparison.OrdinalIgnoreCase)) { total++; done++; }
            }
            return (done, total);
        }
    }

    public class NotesData
    {
        public List<ProjectNote> Notes { get; set; } = new();

        public static NotesData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new NotesData();
            try { return JsonSerializer.Deserialize<NotesData>(json) ?? new NotesData(); }
            catch { return new NotesData(); }
        }

        public string ToJson() => JsonSerializer.Serialize(this);
    }

    public class NotesWindow : Page
    {
        public UIWindowEntry WindowInfo { get; set; }

        private readonly MainPage _host;
        private readonly NotesData _data;
        private ProjectNote _current;
        private bool _loading;

        private readonly StackPanel _list = new StackPanel();
        private readonly TextBox _search;
        private readonly TextBox _title;
        private readonly TextBox _body;
        private readonly ComboBox _table;
        private readonly Button _pin;
        private readonly Button _delete;
        private readonly TextBlock _status;
        private readonly Grid _editor;
        private readonly TextBlock _empty;
        private readonly DispatcherTimer _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };

        private static readonly Color CardBg = Color.FromRgb(0x1B, 0x1A, 0x17);
        private static readonly Color CardBorder = Color.FromRgb(0x35, 0x32, 0x2B);
        private static readonly Color PanelBg = Color.FromRgb(0x22, 0x20, 0x1C);
        private static readonly Color RowBg = Color.FromRgb(0x26, 0x24, 0x20);
        private static readonly Color RowSelected = Color.FromRgb(0x3A, 0x36, 0x2E);
        private static readonly Color Cream = Color.FromRgb(0xF0, 0xE9, 0xD2);
        private static readonly Color Muted = Color.FromRgb(0xA8, 0xA2, 0x92);
        private static readonly Color Accent = Color.FromRgb(0x9D, 0x97, 0x85);
        private static readonly Color AccentText = Color.FromRgb(0x1B, 0x1A, 0x17);
        private static readonly Color Danger = Color.FromRgb(0xE0, 0x6C, 0x5C);
        private static readonly FontFamily Inter = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf");
        private const string NoTable = "(no table)";

        public NotesWindow(MainPage host)
        {
            _host = host;
            _data = NotesData.FromJson(host.NotesJson);
            Width = 980;
            Height = 640;
            Background = new SolidColorBrush(Colors.Transparent);

            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var left = new Grid { Background = new SolidColorBrush(PanelBg) };
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new Grid { Margin = new Thickness(18, 18, 14, 10) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock { Text = "Notes", FontSize = 22, FontFamily = Inter, Foreground = new SolidColorBrush(Cream), VerticalAlignment = VerticalAlignment.Center });
            var add = MakeButton("+ New", Accent, AccentText, 70);
            add.Click += (s, e) => NewNote();
            Grid.SetColumn(add, 1);
            header.Children.Add(add);
            left.Children.Add(header);

            _search = new TextBox { Margin = new Thickness(18, 0, 14, 10), Height = 30, FontSize = 12, PlaceholderText = "Search notes…" };
            _search.TextChanged += (s, e) => RebuildList();
            Grid.SetRow(_search, 1);
            left.Children.Add(_search);

            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(10, 0, 6, 14),
                Content = _list
            };
            Grid.SetRow(scroller, 2);
            left.Children.Add(scroller);
            root.Children.Add(left);

            var right = new Grid();
            Grid.SetColumn(right, 1);

            _editor = new Grid { Margin = new Thickness(22, 18, 22, 16) };
            _editor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _editor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _editor.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _editor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _title = new TextBox { FontSize = 20, Height = 40, Margin = new Thickness(0, 0, 44, 10), FontFamily = Inter };
            _title.TextChanged += (s, e) => Edited(n => n.Title = string.IsNullOrWhiteSpace(_title.Text) ? "Untitled note" : _title.Text);
            _editor.Children.Add(_title);

            var tools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            tools.Children.Add(new TextBlock { Text = "Table", FontSize = 12, Foreground = new SolidColorBrush(Muted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            _table = new ComboBox { Width = 220, Height = 28, FontSize = 12 };
            _table.SelectionChanged += (s, e) => Edited(n => n.LinkedTable = _table.SelectedItem as string == NoTable ? null : _table.SelectedItem as string);
            tools.Children.Add(_table);
            _pin = MakeButton("Pin", Color.FromRgb(0x33, 0x2F, 0x29), Cream, 70);
            _pin.Margin = new Thickness(12, 0, 0, 0);
            _pin.Click += (s, e) => { if (_current == null) return; _current.Pinned = !_current.Pinned; UpdatePin(); Edited(_ => { }); };
            tools.Children.Add(_pin);
            _delete = MakeButton("Delete", Color.FromRgb(0x33, 0x2F, 0x29), Danger, 76);
            _delete.Margin = new Thickness(8, 0, 0, 0);
            _delete.Click += (s, e) => DeleteCurrent();
            tools.Children.Add(_delete);
            var tip = new TextBlock
            {
                Text = "Lines starting with [ ] or [x] are checklist items.",
                FontSize = 11, Foreground = new SolidColorBrush(Muted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0),
                TextWrapping = TextWrapping.Wrap, MaxWidth = 200
            };
            tools.Children.Add(tip);
            Grid.SetRow(tools, 1);
            _editor.Children.Add(tools);

            _body = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                FontSize = 14,
                // The theme's TextBox style centres a box vertically; stretch it
                // so the note body fills the editor and scrolls inside.
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top,
                Height = double.NaN,
                MinHeight = 160,
                Padding = new Thickness(10, 8, 10, 8)
            };
            _body.TextChanged += (s, e) => Edited(n => n.Body = _body.Text);
            var bodyHost = LayoutHelpers.Fill(_body, 160);
            Grid.SetRow(bodyHost, 2);
            _editor.Children.Add(bodyHost);

            _status = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Muted), Margin = new Thickness(0, 8, 0, 0) };
            Grid.SetRow(_status, 3);
            _editor.Children.Add(_status);
            right.Children.Add(_editor);

            _empty = new TextBlock
            {
                Text = "No notes yet.\nPress + New to jot down decisions, TODOs, or anything about this project.",
                FontSize = 14, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Muted), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(40)
            };
            right.Children.Add(_empty);

            var close = new Button
            {
                Content = "✕", Width = 30, Height = 30,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 14, 14, 0),
                Background = new SolidColorBrush(Colors.Transparent), Foreground = new SolidColorBrush(Muted),
                BorderThickness = new Thickness(0), FontSize = 14, Cursor = Cursors.Hand
            };
            close.Click += (s, e) =>
            {
                FlushSave();
                if (_host.IntroPage.Children.Contains(this)) _host.IntroPage.Children.Remove(this);
            };
            right.Children.Add(close);
            root.Children.Add(right);

            Content = new Border
            {
                Background = new SolidColorBrush(CardBg),
                BorderBrush = new SolidColorBrush(CardBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Child = root
            };

            _saveTimer.Tick += (s, e) => FlushSave();
            Unloaded += (s, e) => FlushSave();

            RefreshTables();
            Select(Ordered().FirstOrDefault());
        }


        private IEnumerable<ProjectNote> Ordered() =>
            _data.Notes.OrderByDescending(n => n.Pinned).ThenByDescending(n => n.Updated);

        private void RebuildList()
        {
            _list.Children.Clear();
            var q = (_search?.Text ?? "").Trim();
            var notes = Ordered().Where(n => q.Length == 0
                || (n.Title ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (n.Body ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (n.LinkedTable ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

            if (notes.Count == 0)
            {
                _list.Children.Add(new TextBlock
                {
                    Text = q.Length == 0 ? "No notes yet." : "No matches.",
                    FontSize = 12, Foreground = new SolidColorBrush(Muted), Margin = new Thickness(10)
                });
                return;
            }
            foreach (var n in notes) _list.Children.Add(BuildRow(n));
        }

        private UIElement BuildRow(ProjectNote n)
        {
            var stack = new StackPanel();
            var titleLine = new StackPanel { Orientation = Orientation.Horizontal };
            if (n.Pinned) titleLine.Children.Add(new TextBlock { Text = "⧯ ", FontSize = 12 });
            titleLine.Children.Add(new TextBlock
            {
                Text = n.Title, FontSize = 14, FontFamily = Inter, Foreground = new SolidColorBrush(Cream),
                TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 230
            });
            stack.Children.Add(titleLine);

            var snippet = (n.Body ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (snippet.Length > 70) snippet = snippet.Substring(0, 70) + "…";
            if (snippet.Length > 0)
                stack.Children.Add(new TextBlock { Text = snippet, FontSize = 11, Foreground = new SolidColorBrush(Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });

            var meta = new List<string> { n.Updated.ToString("MMM d, HH:mm") };
            if (!string.IsNullOrEmpty(n.LinkedTable)) meta.Add(n.LinkedTable);
            var (done, total) = n.Checklist();
            if (total > 0) meta.Add($"{done}/{total} done");
            stack.Children.Add(new TextBlock { Text = string.Join("  ·  ", meta), FontSize = 10, Foreground = new SolidColorBrush(Accent), Margin = new Thickness(0, 4, 0, 0) });

            var row = new Border
            {
                Background = new SolidColorBrush(n == _current ? RowSelected : RowBg),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand,
                Child = stack
            };
            row.MouseLeftButtonUp += (s, e) => Select(n);
            return row;
        }


        private void Select(ProjectNote n)
        {
            FlushSave();
            _current = n;
            _loading = true;
            try
            {
                bool has = n != null;
                _editor.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
                _empty.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
                if (has)
                {
                    _title.Text = n.Title ?? "";
                    _body.Text = n.Body ?? "";
                    RefreshTables();
                    _table.SelectedItem = !string.IsNullOrEmpty(n.LinkedTable) && _table.Items.Contains(n.LinkedTable) ? n.LinkedTable : NoTable;
                    UpdatePin();
                    _status.Text = $"Created {n.Created:MMM d, yyyy} · saved";
                }
            }
            finally { _loading = false; }
            RebuildList();
        }

        private void RefreshTables()
        {
            var selected = _table.SelectedItem as string;
            _table.Items.Clear();
            _table.Items.Add(NoTable);
            var tables = _host.MainSessionInfo.Tables;
            if (tables != null)
                foreach (var t in tables)
                    _table.Items.Add(string.IsNullOrEmpty(t.SchemaName) ? t.TableName : $"{t.SchemaName}.{t.TableName}");
            if (_current?.LinkedTable != null && !_table.Items.Contains(_current.LinkedTable))
                _table.Items.Add(_current.LinkedTable);
            if (selected != null && _table.Items.Contains(selected)) _table.SelectedItem = selected;
        }

        private void UpdatePin() => _pin.Content = _current?.Pinned == true ? "Unpin" : "Pin";

        private void NewNote()
        {
            var n = new ProjectNote();
            _data.Notes.Add(n);
            Select(n);
            _title.Focus();
            _title.SelectAll();
            QueueSave();
        }

        private void DeleteCurrent()
        {
            if (_current == null) return;
            var result = MessageBox.Show($"Delete \"{_current.Title}\"?", "Delete note", MessageBoxButton.OKCancel);
            if (result != MessageBoxResult.OK) return;
            _data.Notes.Remove(_current);
            _current = null;
            QueueSave();
            Select(Ordered().FirstOrDefault());
        }

        private void Edited(Action<ProjectNote> apply)
        {
            if (_loading || _current == null) return;
            apply(_current);
            _current.Updated = DateTime.Now;
            QueueSave();
        }

        private void QueueSave()
        {
            _status.Text = "Saving…";
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private bool _dirty => _saveTimer.IsEnabled;

        private async void FlushSave()
        {
            if (!_dirty) return;
            _saveTimer.Stop();
            try
            {
                await _host.SaveNotes(_data.ToJson());
                if (_current != null) _status.Text = $"Created {_current.Created:MMM d, yyyy} · saved {DateTime.Now:HH:mm:ss}";
                RebuildList();
            }
            catch (Exception ex)
            {
                _status.Text = "Could not save: " + ex.Message;
            }
        }

        private static Button MakeButton(string label, Color bg, Color fg, double width) => new Button
        {
            Content = label, Width = width, Height = 28, FontSize = 12, FontFamily = Inter,
            Background = new SolidColorBrush(bg), Foreground = new SolidColorBrush(fg),
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
    }
}
