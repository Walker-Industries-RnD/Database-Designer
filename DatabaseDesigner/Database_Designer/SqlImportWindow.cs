using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static DatabaseDesigner.SessionStorage;

namespace Database_Designer
{
    public class SqlImportWindow : Page
    {
        public UIWindowEntry WindowInfo { get; set; }

        private readonly MainPage _host;
        private readonly TextBox _sql;
        private readonly TextBox _schema;
        private readonly StackPanel _preview = new StackPanel();
        private readonly Button _import;
        private readonly CheckBox _replace;
        private SqlImporter.Result _result;

        private static readonly Color CardBg = Color.FromRgb(0x1B, 0x1A, 0x17);
        private static readonly Color CardBorder = Color.FromRgb(0x35, 0x32, 0x2B);
        private static readonly Color PanelBg = Color.FromRgb(0x22, 0x20, 0x1C);
        private static readonly Color Cream = Color.FromRgb(0xF0, 0xE9, 0xD2);
        private static readonly Color Muted = Color.FromRgb(0xA8, 0xA2, 0x92);
        private static readonly Color Accent = Color.FromRgb(0x9D, 0x97, 0x85);
        private static readonly Color Good = Color.FromRgb(0x6F, 0xC2, 0x8B);
        private static readonly Color Warn = Color.FromRgb(0xE8, 0xB0, 0x4B);
        private static readonly FontFamily Inter = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf");

        public SqlImportWindow(MainPage host)
        {
            _host = host;
            Width = 1040;
            Height = 660;
            Background = new SolidColorBrush(Colors.Transparent);

            var root = new Grid { Margin = new Thickness(24, 20, 24, 20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });

            var header = new StackPanel { Margin = new Thickness(0, 0, 40, 14) };
            header.Children.Add(new TextBlock { Text = "Import SQL", FontSize = 24, FontFamily = Inter, Foreground = new SolidColorBrush(Cream) });
            header.Children.Add(new TextBlock
            {
                Text = "Paste a Postgres schema (CREATE TABLE / ALTER TABLE / CREATE INDEX; a pg_dump --schema-only file works) " +
                       "and it becomes tables in this project. Functions, triggers, grants and data are skipped.",
                FontSize = 12, FontFamily = Inter, Foreground = new SolidColorBrush(Muted), TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumnSpan(header, 2);
            root.Children.Add(header);

            _sql = new TextBox
            {
                AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"), FontSize = 12, VerticalContentAlignment = VerticalAlignment.Top,
                VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = double.NaN, MinHeight = 200, Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 14, 0)
            };
            _sql.TextChanged += (s, e) => RefreshPreview();
            var sqlHost = LayoutHelpers.Fill(_sql, 200);
            Grid.SetRow(sqlHost, 1);
            root.Children.Add(sqlHost);

            var previewHost = new Border
            {
                Background = new SolidColorBrush(PanelBg), CornerRadius = new CornerRadius(10), Padding = new Thickness(14),
                Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _preview }
            };
            Grid.SetRow(previewHost, 1);
            Grid.SetColumn(previewHost, 1);
            root.Children.Add(previewHost);

            var footer = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel { Orientation = Orientation.Horizontal };
            var load = MakeButton("Load .sql file…", Color.FromRgb(0x33, 0x2F, 0x29), Cream, 140);
            load.Click += (s, e) => ImageHelper.SelectAndLoadBytes(obj =>
            {
                try
                {
                    var b64 = obj?.ToString();
                    if (string.IsNullOrEmpty(b64)) return;
                    var text = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                    Dispatcher.BeginInvoke(() => _sql.Text = text);
                }
                catch (Exception ex) { Console.WriteLine("[SqlImport] " + ex.Message); }
            }, ".sql,.txt,.ddl,.pgsql");
            left.Children.Add(load);
            left.Children.Add(new TextBlock { Text = "Default schema", FontSize = 12, Foreground = new SolidColorBrush(Muted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 6, 0) });
            _schema = new TextBox { Text = "public", Width = 110, Height = 28, FontSize = 12 };
            _schema.TextChanged += (s, e) => RefreshPreview();
            left.Children.Add(_schema);
            _replace = new CheckBox
            {
                Content = "Replace tables that already exist", Foreground = new SolidColorBrush(Cream), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0)
            };
            _replace.Checked += (s, e) => RefreshPreview();
            _replace.Unchecked += (s, e) => RefreshPreview();
            left.Children.Add(_replace);
            footer.Children.Add(left);

            var right = new StackPanel { Orientation = Orientation.Horizontal };
            var close = MakeButton("Close", Color.FromRgb(0x33, 0x2F, 0x29), Cream, 90);
            close.Click += (s, e) => { if (_host.IntroPage.Children.Contains(this)) _host.IntroPage.Children.Remove(this); };
            _import = MakeButton("Add to project", Accent, CardBg, 150);
            _import.Margin = new Thickness(8, 0, 0, 0);
            _import.Click += (s, e) => Import();
            right.Children.Add(close);
            right.Children.Add(_import);
            Grid.SetColumn(right, 2);
            footer.Children.Add(right);
            Grid.SetRow(footer, 2);
            Grid.SetColumnSpan(footer, 2);
            root.Children.Add(footer);

            Content = new Border
            {
                Background = new SolidColorBrush(CardBg), BorderBrush = new SolidColorBrush(CardBorder),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Child = root
            };
            RefreshPreview();
        }

        private string DefaultSchema => string.IsNullOrWhiteSpace(_schema?.Text) ? "public" : _schema.Text.Trim().ToLowerInvariant();

        private bool Exists(TableObject t) =>
            (_host.MainSessionInfo.Tables ?? new System.Collections.ObjectModel.ObservableCollection<TableObject>())
            .Any(x => string.Equals(x.SchemaName, t.SchemaName, StringComparison.OrdinalIgnoreCase) && string.Equals(x.TableName, t.TableName, StringComparison.OrdinalIgnoreCase));

        private void RefreshPreview()
        {
            if (_preview == null || _import == null) return;
            _preview.Children.Clear();
            if (string.IsNullOrWhiteSpace(_sql?.Text))
            {
                _result = null;
                _import.IsEnabled = false;
                _preview.Children.Add(Line("Nothing to preview yet; paste SQL on the left.", Muted));
                return;
            }

            _result = SqlImporter.Parse(_sql.Text, DefaultSchema);
            int adding = _result.Tables.Count(t => !Exists(t) || _replace.IsChecked == true);
            _import.IsEnabled = adding > 0;
            _preview.Children.Add(Line($"{_result.Tables.Count} table(s) found", Cream, 15));

            foreach (var t in _result.Tables)
            {
                var exists = Exists(t);
                var status = !exists ? "new" : _replace.IsChecked == true ? "will replace" : "exists, skipped";
                _preview.Children.Add(Line($"{t.SchemaName}.{t.TableName}  ·  {t.Rows.Count} columns, {t.References.Count} FKs, {t.Indexes.Count} indexes  ({status})",
                    exists && _replace.IsChecked != true ? Muted : Good, 12, new Thickness(0, 6, 0, 0)));
            }
            if (_result.Warnings.Count > 0)
            {
                _preview.Children.Add(Line("Check these", Warn, 13, new Thickness(0, 14, 0, 2)));
                foreach (var w in _result.Warnings) _preview.Children.Add(Line("⚠ " + w, Warn, 11));
            }
            if (_result.Skipped.Count > 0)
            {
                _preview.Children.Add(Line($"Skipped {_result.Skipped.Count} statement(s)", Muted, 13, new Thickness(0, 14, 0, 2)));
                foreach (var w in _result.Skipped.Take(12)) _preview.Children.Add(Line("· " + w, Muted, 11));
                if (_result.Skipped.Count > 12) _preview.Children.Add(Line($"… and {_result.Skipped.Count - 12} more", Muted, 11));
            }
        }

        private void Import()
        {
            if (_result == null) return;
            if (_host.ProjectName == null) { MessageBox.Show("Open a project first."); return; }
            int added = 0, replaced = 0;
            foreach (var t in _result.Tables)
            {
                if (Exists(t))
                {
                    if (_replace.IsChecked != true) continue;
                    var old = _host.MainSessionInfo.Tables.First(x =>
                        string.Equals(x.SchemaName, t.SchemaName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.TableName, t.TableName, StringComparison.OrdinalIgnoreCase));
                    _host.MainSessionInfo.Tables.Remove(old);
                    replaced++;
                }
                else added++;
                _host.MainSessionInfo.Tables.Add(t);
            }
            _host.ForceCollectionChangeUpate();
            MessageBox.Show($"Imported: {added} new table(s){(replaced > 0 ? $", {replaced} replaced" : "")}.\n\n" +
                            "Open Build Project > Build Check to see anything that still needs attention.");
            RefreshPreview();
        }

        private static TextBlock Line(string text, Color color, double size = 12, Thickness? margin = null) => new TextBlock
        {
            Text = text, FontSize = size, FontFamily = Inter, Foreground = new SolidColorBrush(color),
            TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0, 2, 0, 0)
        };

        private static Button MakeButton(string label, Color bg, Color fg, double width) => new Button
        {
            Content = label, Width = width, Height = 32, FontSize = 12, FontFamily = Inter,
            Background = new SolidColorBrush(bg), Foreground = new SolidColorBrush(fg),
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
    }
}
