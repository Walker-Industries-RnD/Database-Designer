using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Database_Designer
{
    /// <summary>
    /// Click-to-edit helpers shared by RLSPolicyCreator + FunctionCreator.
    /// A small TextBlock is rendered initially; clicking it swaps to a
    /// TextBox in place. Enter or losing focus commits, Escape reverts.
    /// </summary>
    internal static class EditableTextHelpers
    {
        public static UIElement EditableText(
            string initial,
            Action<string> commit,
            double fontSize = 12,
            FontWeight? weight = null,
            Color? foreground = null,
            bool wrap = false,
            int maxWidth = 0)
        {
            var grid = new Grid();
            var fg = foreground ?? Colors.White;
            var fgBrush = new SolidColorBrush(fg);

            var label = new TextBlock
            {
                Text = string.IsNullOrEmpty(initial) ? "(click to set)" : initial,
                Foreground = string.IsNullOrEmpty(initial)
                    ? new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55))
                    : fgBrush,
                FontSize = fontSize,
                FontWeight = weight ?? FontWeights.Normal,
                Cursor = Cursors.IBeam,
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis
            };
            if (maxWidth > 0) label.MaxWidth = maxWidth;

            var box = new TextBox
            {
                Text = initial ?? "",
                Foreground = fgBrush,
                Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                BorderThickness = new Thickness(1),
                FontSize = fontSize,
                FontWeight = weight ?? FontWeights.Normal,
                Padding = new Thickness(4, 1, 4, 1),
                Visibility = Visibility.Collapsed,
                AcceptsReturn = wrap,
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap
            };
            if (maxWidth > 0) box.MaxWidth = maxWidth;

            void EnterEdit()
            {
                box.Text = label.Text == "(click to set)" ? "" : label.Text;
                label.Visibility = Visibility.Collapsed;
                box.Visibility = Visibility.Visible;
                box.Focus();
                box.SelectAll();
            }

            void LeaveEdit(bool save)
            {
                if (save)
                {
                    var v = (box.Text ?? "").Trim();
                    label.Text = string.IsNullOrEmpty(v) ? "(click to set)" : v;
                    label.Foreground = string.IsNullOrEmpty(v)
                        ? new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55))
                        : fgBrush;
                    commit?.Invoke(v);
                }
                box.Visibility = Visibility.Collapsed;
                label.Visibility = Visibility.Visible;
            }

            label.MouseLeftButtonDown += (s, e) => { e.Handled = true; EnterEdit(); };
            box.LostFocus += (s, e) => LeaveEdit(true);
            box.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { LeaveEdit(false); e.Handled = true; }
                else if (e.Key == Key.Enter && !wrap) { LeaveEdit(true); e.Handled = true; }
            };
            // Block clicks from bubbling up to the parent card and re-selecting it.
            box.MouseLeftButtonDown += (s, e) => e.Handled = true;

            grid.Children.Add(label);
            grid.Children.Add(box);
            return grid;
        }

        // Compact pill-style "Open" button used at the right edge of policy /
        // function rows. Click to launch the NodeWalker graph for that entry.
        public static Button OpenInNodeWalkerButton(Action onClick)
        {
            var btn = new Button
            {
                Content = "Open ▸",
                Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x26)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 2, 8, 2),
                FontSize = 10,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            btn.Click += (s, e) => onClick?.Invoke();
            return btn;
        }
    }
}
