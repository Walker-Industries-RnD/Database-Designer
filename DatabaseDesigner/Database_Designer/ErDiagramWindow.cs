using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DatabaseDesigner;
using static DatabaseDesigner.SessionStorage;

namespace Database_Designer
{
    public class ErDiagramWindow : Page
    {
        public UIWindowEntry WindowInfo { get; set; }

        private const double CardWidth = 240, HeaderHeight = 34, RowHeight = 22, ColGap = 110, RowGap = 36, Margin0 = 40;
        private const string PosPrefix = "er:";

        private readonly MainPage _host;
        private readonly Canvas _canvas = new Canvas();
        private readonly Canvas _edges = new Canvas();
        private readonly Canvas _sizer = new Canvas();
        private readonly ScaleTransform _zoom = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        private readonly TextBlock _summary;
        private readonly Dictionary<string, (Border card, TableObject table)> _cards = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Point> _pos = new(StringComparer.OrdinalIgnoreCase);

        private static readonly Color CardBg = Color.FromRgb(0x1B, 0x1A, 0x17);
        private static readonly Color CardBorder = Color.FromRgb(0x35, 0x32, 0x2B);
        private static readonly Color Paper = Color.FromRgb(0xF7, 0xF4, 0xEC);
        private static readonly Color Ink = Color.FromRgb(0x2C, 0x2B, 0x28);
        private static readonly Color Header = Color.FromRgb(0x45, 0x41, 0x38);
        private static readonly Color Cream = Color.FromRgb(0xF0, 0xE9, 0xD2);
        private static readonly Color Muted = Color.FromRgb(0x8A, 0x84, 0x76);
        private static readonly Color PkColor = Color.FromRgb(0xC8, 0x8A, 0x1E);
        private static readonly Color FkColor = Color.FromRgb(0x3E, 0x7C, 0xB1);
        private static readonly Color EdgeColor = Color.FromRgb(0x6F, 0x9B, 0xC9);
        private static readonly FontFamily Inter = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf");

        public ErDiagramWindow(MainPage host)
        {
            _host = host;
            Width = 1100;
            Height = 700;
            Background = new SolidColorBrush(Colors.Transparent);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var toolbar = new Grid { Margin = new Thickness(20, 16, 20, 10) };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock { Text = "ER Diagram", FontSize = 22, FontFamily = Inter, Foreground = new SolidColorBrush(Cream) });
            _summary = new TextBlock { FontSize = 11, FontFamily = Inter, Foreground = new SolidColorBrush(Muted) };
            titleStack.Children.Add(_summary);
            toolbar.Children.Add(titleStack);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            buttons.Children.Add(Legend("PK", PkColor));
            buttons.Children.Add(Legend("FK", FkColor));
            buttons.Children.Add(Tool("−", 34, () => SetZoom(_zoom.ScaleX / 1.2)));
            buttons.Children.Add(Tool("100%", 56, () => SetZoom(1)));
            buttons.Children.Add(Tool("+", 34, () => SetZoom(_zoom.ScaleX * 1.2)));
            buttons.Children.Add(Tool("Auto layout", 100, () => { AutoLayout(force: true); SavePositions(); }));
            buttons.Children.Add(Tool("Refresh", 80, Rebuild));
            buttons.Children.Add(Tool("Export SVG", 90, () => Export(png: false)));
            buttons.Children.Add(Tool("Export PNG", 90, () => Export(png: true)));
            buttons.Children.Add(Tool("✕", 34, () => { if (_host.IntroPage.Children.Contains(this)) _host.IntroPage.Children.Remove(this); }));
            Grid.SetColumn(buttons, 2);
            toolbar.Children.Add(buttons);
            root.Children.Add(toolbar);

            _canvas.RenderTransform = _zoom;
            _canvas.Children.Add(_edges);
            _sizer.Children.Add(_canvas);
            var scroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _sizer,
                Margin = new Thickness(14, 0, 14, 14),
                Background = new SolidColorBrush(Color.FromRgb(0x24, 0x22, 0x1E))
            };
            Grid.SetRow(scroller, 1);
            root.Children.Add(scroller);

            Content = new Border
            {
                Background = new SolidColorBrush(CardBg), BorderBrush = new SolidColorBrush(CardBorder),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Child = root
            };
            Rebuild();
        }


        private List<TableObject> Tables() => (_host.MainSessionInfo.Tables ?? new System.Collections.ObjectModel.ObservableCollection<TableObject>()).ToList();

        private static string Q(TableObject t) => string.IsNullOrEmpty(t.SchemaName) ? t.TableName : $"{t.SchemaName}.{t.TableName}";

        private void Rebuild()
        {
            foreach (var (card, _) in _cards.Values) _canvas.Children.Remove(card);
            _cards.Clear();
            _pos.Clear();
            var tables = Tables();

            foreach (var t in tables)
            {
                var card = BuildCard(t);
                _cards[Q(t)] = (card, t);
                _canvas.Children.Add(card);
            }

            var saved = _host.MainSessionInfo.WindowStatuses ?? new Dictionary<string, Coords>();
            foreach (var key in _cards.Keys)
                if (saved.TryGetValue(PosPrefix + key, out var c)) _pos[key] = new Point(c.X, c.Y);
            AutoLayout(force: false);

            int fks = tables.Sum(t => t.References?.Count ?? 0);
            _summary.Text = tables.Count == 0
                ? "No tables yet; create some in the Table Editor."
                : $"{tables.Count} tables · {fks} relationships · drag cards to arrange, click a title to open the table";
        }

        private Border BuildCard(TableObject t)
        {
            var stack = new StackPanel();
            var header = new Border
            {
                Background = new SolidColorBrush(Header), Height = HeaderHeight, CornerRadius = new CornerRadius(8, 8, 0, 0),
                Padding = new Thickness(10, 0, 10, 0), Cursor = Cursors.SizeAll
            };
            var title = new TextBlock
            {
                Text = Q(t), FontSize = 13, FontFamily = Inter, Foreground = new SolidColorBrush(Cream),
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
            };
            header.Child = title;
            stack.Children.Add(header);

            var fkCols = new HashSet<string>((t.References ?? new()).Select(r => r.ForeignKey ?? ""), StringComparer.OrdinalIgnoreCase);
            foreach (var r in t.Rows ?? new List<RowCreation>())
            {
                var g = new Grid { Height = RowHeight, Margin = new Thickness(10, 0, 10, 0) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                string badge = r.IsPrimary == true ? "PK" : fkCols.Contains(r.Name ?? "") ? "FK" : r.IsUnique == true ? "UQ" : "";
                var badgeColor = r.IsPrimary == true ? PkColor : fkCols.Contains(r.Name ?? "") ? FkColor : Muted;
                g.Children.Add(new TextBlock { Text = badge, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(badgeColor), VerticalAlignment = VerticalAlignment.Center });
                var name = new TextBlock
                {
                    Text = (r.Name ?? "") + (r.IsNotNull == true || r.IsPrimary == true ? "" : "?"),
                    FontSize = 12, FontFamily = Inter, Foreground = new SolidColorBrush(Ink),
                    VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(name, 1);
                g.Children.Add(name);
                var type = new TextBlock
                {
                    Text = Row.TypeWithLimit(r.RowType?.ToString() ?? (r.Media == true ? "SecureMedia" : "Text"), r.Limit).ToLowerInvariant() + (r.IsArray == true ? "[]" : ""),
                    FontSize = 11, Foreground = new SolidColorBrush(Muted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0)
                };
                Grid.SetColumn(type, 2);
                g.Children.Add(type);
                stack.Children.Add(g);
            }
            if ((t.Rows?.Count ?? 0) == 0)
                stack.Children.Add(new TextBlock { Text = "no columns yet", FontSize = 11, Foreground = new SolidColorBrush(Muted), Margin = new Thickness(10, 3, 10, 3), Height = RowHeight });

            var card = new Border
            {
                Width = CardWidth,
                Background = new SolidColorBrush(Paper),
                BorderBrush = new SolidColorBrush(Header),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = stack,
                Padding = new Thickness(0, 0, 0, 6)
            };
            HookDrag(card, header, Q(t));
            return card;
        }

        private static double CardHeight(TableObject t) => HeaderHeight + Math.Max(1, t.Rows?.Count ?? 0) * RowHeight + 8;


        private void AutoLayout(bool force)
        {
            var tables = _cards.Values.Select(v => v.table).ToList();
            var names = new HashSet<string>(tables.Select(Q), StringComparer.OrdinalIgnoreCase);
            var depth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int Depth(TableObject t, HashSet<string> visiting)
            {
                var k = Q(t);
                if (depth.TryGetValue(k, out var d)) return d;
                if (!visiting.Add(k)) return 0;
                int best = 0;
                foreach (var r in t.References ?? new())
                {
                    if (r.RefTable == null || !names.Contains(r.RefTable) || string.Equals(r.RefTable, k, StringComparison.OrdinalIgnoreCase)) continue;
                    best = Math.Max(best, Depth(_cards[r.RefTable].table, visiting) + 1);
                }
                visiting.Remove(k);
                return depth[k] = best;
            }
            foreach (var t in tables) Depth(t, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            var columns = tables.GroupBy(t => depth[Q(t)]).OrderBy(g => g.Key).ToList();
            foreach (var col in columns)
            {
                double y = Margin0;
                double x = Margin0 + col.Key * (CardWidth + ColGap);
                foreach (var t in col.OrderBy(Q))
                {
                    var k = Q(t);
                    if (force || !_pos.ContainsKey(k)) _pos[k] = new Point(x, y);
                    y += CardHeight(t) + RowGap;
                }
            }
            ApplyPositions();
        }

        private void ApplyPositions()
        {
            double maxX = 0, maxY = 0;
            foreach (var (k, (card, t)) in _cards)
            {
                var p = _pos[k];
                Canvas.SetLeft(card, p.X);
                Canvas.SetTop(card, p.Y);
                maxX = Math.Max(maxX, p.X + CardWidth);
                maxY = Math.Max(maxY, p.Y + CardHeight(t));
            }
            _canvas.Width = maxX + Margin0;
            _canvas.Height = maxY + Margin0;
            UpdateExtent();
            DrawEdges();
        }

        private void SetZoom(double z)
        {
            z = Math.Max(0.3, Math.Min(2.5, z));
            _zoom.ScaleX = _zoom.ScaleY = z;
            UpdateExtent();
        }

        private void UpdateExtent()
        {
            _sizer.Width = (double.IsNaN(_canvas.Width) ? 0 : _canvas.Width) * _zoom.ScaleX;
            _sizer.Height = (double.IsNaN(_canvas.Height) ? 0 : _canvas.Height) * _zoom.ScaleY;
        }


        private void DrawEdges()
        {
            _edges.Children.Clear();
            foreach (var (k, (_, t)) in _cards)
            {
                foreach (var r in t.References ?? new())
                {
                    if (r.RefTable == null || !_cards.TryGetValue(r.RefTable, out var parent)) continue;
                    var from = Anchor(k, t, r.ForeignKey);
                    var to = Anchor(r.RefTable, parent.table, r.RefTableKey);
                    DrawEdge(from, to, string.Equals(k, r.RefTable, StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        private (Point left, Point right) Anchor(string key, TableObject t, string column)
        {
            var p = _pos[key];
            int idx = Math.Max(0, (t.Rows ?? new()).FindIndex(r => string.Equals(r.Name, column, StringComparison.OrdinalIgnoreCase)));
            double y = p.Y + HeaderHeight + idx * RowHeight + RowHeight / 2;
            return (new Point(p.X, y), new Point(p.X + CardWidth, y));
        }

        private void DrawEdge((Point left, Point right) child, (Point left, Point right) parent, bool self)
        {
            Point a, b;
            double dirA, dirB;
            if (self) { a = child.right; b = parent.right; dirA = 1; dirB = 1; }
            else if (parent.right.X <= child.left.X) { a = child.left; b = parent.right; dirA = -1; dirB = 1; }
            else if (parent.left.X >= child.right.X) { a = child.right; b = parent.left; dirA = 1; dirB = -1; }
            else { a = child.left; b = parent.left; dirA = -1; dirB = -1; }

            double bend = Math.Max(50, Math.Abs(b.X - a.X) / 2);
            var fig = new PathFigure { StartPoint = a };
            fig.Segments.Add(new BezierSegment
            {
                Point1 = new Point(a.X + dirA * bend, a.Y),
                Point2 = new Point(b.X + dirB * bend, b.Y),
                Point3 = b
            });
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            _edges.Children.Add(new Path { Data = geo, Stroke = new SolidColorBrush(EdgeColor), StrokeThickness = 1.6 });

            var head = new Polygon
            {
                Fill = new SolidColorBrush(EdgeColor),
                Points = new PointCollection
                {
                    b,
                    new Point(b.X + dirB * 9, b.Y - 5),
                    new Point(b.X + dirB * 9, b.Y + 5)
                }
            };
            _edges.Children.Add(head);
            var dot = new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(FkColor) };
            Canvas.SetLeft(dot, a.X - 3.5);
            Canvas.SetTop(dot, a.Y - 3.5);
            _edges.Children.Add(dot);
        }


        private void HookDrag(Border card, Border handle, string key)
        {
            bool dragging = false, moved = false;
            Point start = default, origin = default;
            handle.MouseLeftButtonDown += (s, e) =>
            {
                dragging = true; moved = false;
                start = e.GetPosition(_canvas);
                origin = _pos.TryGetValue(key, out var p) ? p : new Point(0, 0);
                handle.CaptureMouse();
                Canvas.SetZIndex(card, 1000);
                e.Handled = true;
            };
            handle.MouseMove += (s, e) =>
            {
                if (!dragging) return;
                var now = e.GetPosition(_canvas);
                var dx = now.X - start.X; var dy = now.Y - start.Y;
                if (Math.Abs(dx) + Math.Abs(dy) > 3) moved = true;
                _pos[key] = new Point(Math.Max(0, origin.X + dx), Math.Max(0, origin.Y + dy));
                Canvas.SetLeft(card, _pos[key].X);
                Canvas.SetTop(card, _pos[key].Y);
                DrawEdges();
            };
            handle.MouseLeftButtonUp += (s, e) =>
            {
                if (!dragging) return;
                dragging = false;
                handle.ReleaseMouseCapture();
                Canvas.SetZIndex(card, 0);
                if (moved) { ApplyPositions(); SavePositions(); }
                else _host.CreateWindow(() => new DatabaseViewer(_host, key), "Database Viewer", true);
            };
        }

        private async void SavePositions()
        {
            if (_host.MainSessionInfo.WindowStatuses == null)
                _host.MainSessionInfo.WindowStatuses = new Dictionary<string, Coords>();
            foreach (var (k, p) in _pos)
                _host.MainSessionInfo.WindowStatuses[PosPrefix + k] = new Coords { X = (int)p.X, Y = (int)p.Y, IsEnabled = true };
            if (_host.ProjectName != null)
            {
                try { await MainPage.OnTables_Changed(_host); }
                catch (Exception ex) { Console.WriteLine("[ER] Could not save layout: " + ex.Message); }
            }
        }


        private void Export(bool png)
        {
            try
            {
                var positions = _pos.ToDictionary(kv => kv.Key, kv => (kv.Value.X, kv.Value.Y), StringComparer.OrdinalIgnoreCase);
                var title = _host.MainSessionInfo.SessionName ?? _host.ProjectName ?? "Database";
                var svg = ErDiagramExporter.ToSvg(Tables(), positions, title);
                var dir = System.IO.Path.Combine(_host.ExportsFolder, "ER Diagrams");
                System.IO.Directory.CreateDirectory(dir);
                var safe = string.Concat(title.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_'));
                var file = System.IO.Path.Combine(dir, $"{safe}-{DateTime.Now:yyyyMMdd-HHmmss}");

                if (!png)
                {
                    System.IO.File.WriteAllText(file + ".svg", svg);
                    Saved(file + ".svg");
                    return;
                }

                // Rasterise in the web view: SVG -> <img> -> 2x canvas -> PNG.
                var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
                Action<object> done = result =>
                {
                    var data = result?.ToString();
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (string.IsNullOrEmpty(data)) { _summary.Text = "PNG export failed; try Export SVG instead."; return; }
                        System.IO.File.WriteAllBytes(file + ".png", Convert.FromBase64String(data));
                        Saved(file + ".png");
                    });
                };
                OpenSilver.Interop.ExecuteJavaScript(@"
(function(svg64, callback) {
    var img = new Image();
    img.onload = function() {
        try {
            var scale = 2;
            var c = document.createElement('canvas');
            c.width = img.naturalWidth * scale;
            c.height = img.naturalHeight * scale;
            var ctx = c.getContext('2d');
            ctx.scale(scale, scale);
            ctx.drawImage(img, 0, 0);
            var url = c.toDataURL('image/png');
            callback(url.substring(url.indexOf(',') + 1));
        } catch (e) { callback(null); }
    };
    img.onerror = function() { callback(null); };
    img.src = 'data:image/svg+xml;base64,' + svg64;
})($0, $1);", b64, done);
            }
            catch (Exception ex)
            {
                _summary.Text = "Export failed: " + ex.Message;
            }
        }

        private void Saved(string path)
        {
            _summary.Text = "Saved " + path;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = System.IO.Path.GetDirectoryName(path),
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch { }
        }

        private Button Tool(string label, double width, Action click)
        {
            var b = new Button
            {
                Content = label, Width = width, Height = 30, FontSize = 12, FontFamily = Inter, Margin = new Thickness(6, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x33, 0x2F, 0x29)), Foreground = new SolidColorBrush(Cream),
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            b.Click += (s, e) => click();
            return b;
        }

        private static UIElement Legend(string label, Color color)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            sp.Children.Add(new TextBlock { Text = label, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = label == "PK" ? " primary key" : " foreign key", FontSize = 11, Foreground = new SolidColorBrush(Muted), VerticalAlignment = VerticalAlignment.Center });
            return sp;
        }
    }
}
