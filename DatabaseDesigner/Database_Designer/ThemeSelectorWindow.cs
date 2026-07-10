using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Database_Designer
{
    //Trying something new here
    public class ThemeSelectorWindow : Page
    {
        public UIWindowEntry WindowInfo { get; set; }
        private readonly MainPage _host;
        private readonly StackPanel _list;

        private static readonly Color CardBg      = Color.FromRgb(0x1B, 0x1A, 0x17);
        private static readonly Color CardBorder  = Color.FromRgb(0x35, 0x32, 0x2B);
        private static readonly Color LogoTile    = Color.FromRgb(0x45, 0x41, 0x38);
        private static readonly Color RowBg       = Color.FromRgb(0x26, 0x24, 0x20);
        private static readonly Color RowHover    = Color.FromRgb(0x30, 0x2D, 0x28);
        private static readonly Color Divider     = Color.FromRgb(0x35, 0x32, 0x2B);
        private static readonly Color Cream       = Color.FromRgb(0xF0, 0xE9, 0xD2);
        private static readonly Color Muted       = Color.FromRgb(0xA8, 0xA2, 0x92);
        private static readonly Color Accent      = Color.FromRgb(0x9D, 0x97, 0x85);
        private static readonly Color AccentText  = Color.FromRgb(0x1B, 0x1A, 0x17);
        private static readonly Color SecondaryBg = Color.FromRgb(0x33, 0x2F, 0x29);

        private static readonly FontFamily Inter = new FontFamily("Assets/Fonts/Inter_28pt-Light.ttf");

        public ThemeSelectorWindow(MainPage host)
        {
            _host = host;
            Width = 520;
            Height = 560;
            Background = new SolidColorBrush(Colors.Transparent);

            // Rounded card container
            var card = new Border
            {
                Background = new SolidColorBrush(CardBg),
                BorderBrush = new SolidColorBrush(CardBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16)
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // divider
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // list

            root.Children.Add(BuildHeader());

            var divider = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Divider),
                Margin = new Thickness(24, 0, 24, 0)
            };
            Grid.SetRow(divider, 1);
            root.Children.Add(divider);

            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(14, 8, 14, 16),
                Padding = new Thickness(0)
            };
            _list = new StackPanel { Margin = new Thickness(8, 6, 8, 0) };
            scroller.Content = _list;
            Grid.SetRow(scroller, 2);
            root.Children.Add(scroller);

            // Close button, overlaid top-right
            var close = new Button
            {
                Content = "✕",
                Width = 30,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 14, 14, 0),
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Muted),
                BorderThickness = new Thickness(0),
                FontSize = 14,
                Cursor = Cursors.Hand
            };
            close.MouseEnter += (s, e) => close.Foreground = new SolidColorBrush(Cream);
            close.MouseLeave += (s, e) => close.Foreground = new SolidColorBrush(Muted);
            close.Click += (s, e) =>
            {
                if (_host.IntroPage.Children.Contains(this)) _host.IntroPage.Children.Remove(this);
            };
            Grid.SetRowSpan(close, 3);
            root.Children.Add(close);

            card.Child = root;
            Content = card;
            Rebuild();
        }

        private UIElement BuildHeader()
        {
            var header = new Grid { Margin = new Thickness(24, 24, 24, 18) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Square logo tile
            var logoTile = new Border
            {
                Width = 76,
                Height = 76,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(LogoTile),
                VerticalAlignment = VerticalAlignment.Top
            };
            var logo = new Image
            {
                Width = 48,
                Height = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Stretch = Stretch.Uniform
            };
            try { logo.Source = new BitmapImage(new Uri("Assets/Images/WILogoNoText.png", UriKind.Relative)); }
            catch { }
            logoTile.Child = logo;
            Grid.SetColumn(logoTile, 0);
            header.Children.Add(logoTile);

            // Title + description
            var text = new StackPanel
            {
                Margin = new Thickness(16, 2, 34, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var titleRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };
            var sparkle = new Image
            {
                Width = 22,
                Height = 22,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            try { sparkle.Source = new BitmapImage(new Uri("Assets/Images/Y2K/sparkles.png", UriKind.Relative)); }
            catch { }
            titleRow.Children.Add(sparkle);
            titleRow.Children.Add(new TextBlock
            {
                Text = "Themes",
                FontSize = 24,
                FontFamily = Inter,
                Foreground = new SolidColorBrush(Cream),
                VerticalAlignment = VerticalAlignment.Center
            });
            text.Children.Add(titleRow);
            text.Children.Add(new TextBlock
            {
                Text = "Apply a theme instantly, or set one as your default for next launch. " +
                       "Drop theme folders (each with a theme.json) into your Themes\\ folder — a background image is optional.",
                FontSize = 12,
                FontFamily = Inter,
                Foreground = new SolidColorBrush(Muted),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 17
            });
            Grid.SetColumn(text, 1);
            header.Children.Add(text);

            return header;
        }

        private void Rebuild()
        {
            _list.Children.Clear();
            var userFolder = _host.UserFolder;
            var current = ThemeManager.GetDefault(userFolder);

            foreach (var name in ThemeManager.AvailableThemes(userFolder))
            {
                bool isDefault = name == current;
                _list.Children.Add(BuildRow(userFolder, name, isDefault));
            }
        }

        private UIElement BuildRow(string userFolder, string name, bool isDefault)
        {
            var rowBorder = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = new SolidColorBrush(RowBg),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(12, 10, 12, 10)
            };
            rowBorder.MouseEnter += (s, e) => rowBorder.Background = new SolidColorBrush(RowHover);
            rowBorder.MouseLeave += (s, e) => rowBorder.Background = new SolidColorBrush(RowBg);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // swatch
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // name
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // buttons

            // Color swatch
            var swatchColor = ThemeManager.GetPrimarySwatch(userFolder, name) ?? LogoTile;
            var swatch = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(swatchColor),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            Grid.SetColumn(swatch, 0);
            grid.Children.Add(swatch);

            // Name (+ default pill)
            var nameStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            nameStack.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 15,
                FontFamily = Inter,
                Foreground = new SolidColorBrush(Cream),
                VerticalAlignment = VerticalAlignment.Center
            });
            if (isDefault)
            {
                nameStack.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Accent),
                    Margin = new Thickness(8, 0, 0, 0),
                    Padding = new Thickness(7, 1, 7, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = "DEFAULT",
                        FontSize = 9,
                        FontFamily = Inter,
                        Foreground = new SolidColorBrush(AccentText)
                    }
                });
            }
            Grid.SetColumn(nameStack, 1);
            grid.Children.Add(nameStack);

            // Buttons
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var applyBtn = MakeButton("Apply", Accent, AccentText, 78);
            applyBtn.Click += (s, e) => _host.ApplyTheme(name);
            buttons.Children.Add(applyBtn);

            var defaultBtn = MakeButton(isDefault ? "Default" : "Set Default", SecondaryBg, Cream, 100);
            defaultBtn.Margin = new Thickness(8, 0, 0, 0);
            defaultBtn.IsEnabled = !isDefault;
            if (isDefault) defaultBtn.Opacity = 0.5;
            defaultBtn.Click += (s, e) =>
            {
                ThemeManager.SetDefault(userFolder, name);
                _host.ApplyTheme(name);
                Rebuild();
            };
            buttons.Children.Add(defaultBtn);

            Grid.SetColumn(buttons, 2);
            grid.Children.Add(buttons);

            rowBorder.Child = grid;
            return rowBorder;
        }

        private static Button MakeButton(string label, Color bg, Color fg, double width)
        {
            var btn = new Button
            {
                Content = label,
                Width = width,
                Height = 30,
                FontSize = 12.5,
                FontFamily = Inter,
                Background = new SolidColorBrush(bg),
                Foreground = new SolidColorBrush(fg),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            // Subtle hover lift
            var normal = new SolidColorBrush(bg);
            var hover = new SolidColorBrush(Lighten(bg, 0.10));
            btn.Background = normal;
            btn.MouseEnter += (s, e) => { if (btn.IsEnabled) btn.Background = hover; };
            btn.MouseLeave += (s, e) => { if (btn.IsEnabled) btn.Background = normal; };
            return btn;
        }

        private static Color Lighten(Color c, double amount)
        {
            byte L(byte v) => (byte)Math.Min(255, v + (255 - v) * amount);
            return Color.FromArgb(c.A, L(c.R), L(c.G), L(c.B));
        }
    }
}
