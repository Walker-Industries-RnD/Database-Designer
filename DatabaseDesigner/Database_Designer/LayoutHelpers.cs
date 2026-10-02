using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Database_Designer
{
    public static class LayoutHelpers
    {
        // Puts a multi-line TextBox in a host that sizes it to the space it is
        // given. The theme's TextBox style otherwise shrinks it to one line and
        // centres it; this way it grows with the window and scrolls inside.
        public static Border Fill(TextBox box, double minHeight = 120)
        {
            box.VerticalAlignment = VerticalAlignment.Top;
            box.HorizontalAlignment = HorizontalAlignment.Left;
            box.VerticalContentAlignment = VerticalAlignment.Top;
            if (box.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled || box.VerticalScrollBarVisibility == ScrollBarVisibility.Hidden)
                box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            var host = new Border
            {
                Background = new SolidColorBrush(Colors.Transparent),
                MinHeight = minHeight,
                Child = box
            };
            host.SizeChanged += (s, e) =>
            {
                var margin = box.Margin;
                box.Width = System.Math.Max(0, e.NewSize.Width - margin.Left - margin.Right);
                box.Height = System.Math.Max(minHeight, e.NewSize.Height - margin.Top - margin.Bottom);
            };
            return host;
        }
    }
}
