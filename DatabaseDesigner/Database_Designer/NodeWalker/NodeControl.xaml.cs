using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using static Database_Designer.NodeWalker.NodeWalker.Node;

namespace Database_Designer.NodeWalker
{
    public class PortEventArgs : EventArgs
    {
        public string PortName { get; set; }
        public bool IsOutput { get; set; }
        public Point Position { get; set; }
    }

    public partial class NodeControl : UserControl
    {
        public BareNode Data { get; private set; }

        public event Action OnDragStarted;
        public event Action<Point> OnDragDelta;
        public event Action<NodeControl> OnNodeClicked;
        public event EventHandler<PortEventArgs> OnPortMouseDown;
        public event EventHandler<PortEventArgs> OnPortMouseUp;
        public event EventHandler<PortEventArgs> OnPortMouseMove;

        private Point _dragStartPosition = new(double.NaN, double.NaN);
        private Point _elementStartPosition;
        private bool _isDragging;
        private Point _lastDragPosition;

        private readonly Dictionary<(string name, bool isOutput), Ellipse> _portEllipses = new();

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        public bool HasWarning
        {
            get => (bool)GetValue(HasWarningProperty);
            set => SetValue(HasWarningProperty, value);
        }
        public string WarningText
        {
            get => (string)GetValue(WarningTextProperty);
            set => SetValue(WarningTextProperty, value);
        }
        public string ValuePreview
        {
            get => (string)GetValue(ValuePreviewProperty);
            set
            {
                SetValue(ValuePreviewProperty, value);
                SetValue(HasValuePreviewProperty, !string.IsNullOrEmpty(value));
            }
        }
        public bool HasValuePreview
        {
            get => (bool)GetValue(HasValuePreviewProperty);
            set => SetValue(HasValuePreviewProperty, value);
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(NodeControl),
                new PropertyMetadata("Node"));
        public static readonly DependencyProperty HasWarningProperty =
            DependencyProperty.Register(nameof(HasWarning), typeof(bool), typeof(NodeControl),
                new PropertyMetadata(false));
        public static readonly DependencyProperty WarningTextProperty =
            DependencyProperty.Register(nameof(WarningText), typeof(string), typeof(NodeControl),
                new PropertyMetadata("⚠ Required input not connected"));
        public static readonly DependencyProperty ValuePreviewProperty =
            DependencyProperty.Register(nameof(ValuePreview), typeof(string), typeof(NodeControl),
                new PropertyMetadata(null));
        public static readonly DependencyProperty HasValuePreviewProperty =
            DependencyProperty.Register(nameof(HasValuePreview), typeof(bool), typeof(NodeControl),
                new PropertyMetadata(false));

        public Brush NodeBorderBrush { get => NodeBorder.BorderBrush; set => NodeBorder.BorderBrush = value; }
        public Thickness NodeBorderThickness { get => NodeBorder.BorderThickness; set => NodeBorder.BorderThickness = value; }

        public ObservableCollection<Input> Inputs { get; } = new();
        public ObservableCollection<Output> Outputs { get; } = new();

        public NodeControl()
        {
            InitializeComponent();
            DataContext = this;
            Inputs.CollectionChanged += (_, _) => PopulatePorts();
            Outputs.CollectionChanged += (_, _) => PopulatePorts();

            MouseLeftButtonDown += NodeControl_MouseLeftButtonDown;
            MouseMove += NodeControl_MouseMove;
            MouseLeftButtonUp += NodeControl_MouseLeftButtonUp;
            LostMouseCapture += (s, e) =>
            {
                _isDragging = false;
                _dragStartPosition = new(double.NaN, double.NaN);
            };
        }

        private static bool IsInteractiveChild(object o) =>
            o is Ellipse || o is TextBox || o is Button || o is CheckBox || o is ComboBox;

        private void NodeControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsInteractiveChild(e.OriginalSource)) return;
            if (e.ClickCount != 1) return;

            _dragStartPosition = e.GetPosition(Parent as Canvas);
            _lastDragPosition = _dragStartPosition;
            _elementStartPosition = new(Canvas.GetLeft(this), Canvas.GetTop(this));
            if (double.IsNaN(_elementStartPosition.X)) _elementStartPosition.X = 0;
            if (double.IsNaN(_elementStartPosition.Y)) _elementStartPosition.Y = 0;
            _isDragging = false;
            OnNodeClicked?.Invoke(this);
            CaptureMouse();
            e.Handled = true;
        }

        private void NodeControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var cur = e.GetPosition(Parent as Canvas);
                var offset = cur - _dragStartPosition;
                Canvas.SetLeft(this, _elementStartPosition.X + offset.X);
                Canvas.SetTop(this, _elementStartPosition.Y + offset.Y);
                var delta = cur - _lastDragPosition;
                _lastDragPosition = cur;
                OnDragDelta?.Invoke(new Point(delta.X, delta.Y));
            }
            else if (!double.IsNaN(_dragStartPosition.X))
            {
                var cur = e.GetPosition(Parent as Canvas);
                if (Math.Sqrt(Math.Pow(cur.X - _dragStartPosition.X, 2) + Math.Pow(cur.Y - _dragStartPosition.Y, 2)) > 5)
                {
                    _isDragging = true;
                    OnDragStarted?.Invoke();
                }
            }
        }

        private void NodeControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured) ReleaseMouseCapture();
            _isDragging = false;
            _dragStartPosition = new(double.NaN, double.NaN);
        }

        public void LoadFromBareNode(BareNode bareNode)
        {
            Data = bareNode;
            Title = bareNode.Title ?? "Untitled";

            if (bareNode.Logic?.StartsWith("ERROR:") == true)
            {
                HasWarning = true;
                WarningText = bareNode.Logic.Substring(6).Trim();
            }
            else
            {
                HasWarning = false;
            }

            Inputs.Clear();
            Outputs.Clear();

            foreach (var input in bareNode.Inputs) Inputs.Add(input);
            foreach (var output in bareNode.Outputs) Outputs.Add(output);
        }

        private void PopulatePorts()
        {
            InputsPanel.Children.Clear();
            OutputsPanel.Children.Clear();
            _portEllipses.Clear();

            int i = 0;
            foreach (var input in Inputs)
                InputsPanel.Children.Add(CreatePortElement(input, isInput: true, index: i++));

            int o2 = 0;
            foreach (var output in Outputs)
                OutputsPanel.Children.Add(CreatePortElement(output, isInput: false, index: o2++));
        }

        private FrameworkElement CreatePortElement(object port, bool isInput, int index)
        {
            string name = "", typeName = "", customType = "";
            bool required = false;

            if (port is Input inp)
            {
                name = inp.Name;
                typeName = inp.SemanticType ?? inp.Type?.Name ?? "object";
                required = inp.Required;
                customType = inp.CustomTypeName ?? "";
            }
            else if (port is Output outp)
            {
                name = outp.Name;
                typeName = outp.SemanticType ?? outp.Type?.Name ?? "object";
                customType = outp.CustomTypeName ?? "";
            }

            // Display "Custom: TypeName" for custom-typed ports
            var displayType = (typeName == "custom" && !string.IsNullOrEmpty(customType))
                ? $"custom:{customType}" : typeName;

            var portInfo = new PortInfo { Name = name, IsOutput = !isInput, Index = index };

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = portInfo
            };

            // Port dot colour: red by default, yellow for custom, grey for required-unconnected
            var dotColor = required
                ? Color.FromRgb(255, 200, 80)   // amber = required
                : typeName == "custom"
                    ? Color.FromRgb(180, 120, 255)  // purple = custom
                    : Color.FromRgb(255, 76, 76);   // red = normal

            var ellipse = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(dotColor),
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Cursor = Cursors.Cross,
                Tag = portInfo
            };

            ellipse.MouseLeftButtonDown += Port_MouseDown;
            ellipse.MouseLeftButtonUp += Port_MouseUp;
            ellipse.MouseMove += Port_MouseMove;

            _portEllipses[(name, !isInput)] = ellipse;

            // Required indicator
            var requiredTag = required
                ? new TextBlock { Text = "*", Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100)), FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) }
                : null;

            var label = new TextBlock
            {
                Text = name,
                FontFamily = new FontFamily("/Database_Designer;component/Assets/Fonts/Inter/Inter_28pt-ThinItalic.ttf#Inter 28pt Thin"),
                FontSize = 12,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 4, 0)
            };

            var typeLabel = new TextBlock
            {
                Text = displayType,
                FontFamily = new FontFamily("/Database_Designer;component/Assets/Fonts/Inter_28pt-BoldItalic.ttf#Inter 28pt"),
                FontSize = 10,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (isInput)
            {
                panel.Children.Add(ellipse);
                panel.Children.Add(label);
                panel.Children.Add(typeLabel);
                if (requiredTag != null) panel.Children.Add(requiredTag);
            }
            else
            {
                panel.Children.Add(typeLabel);
                if (requiredTag != null) panel.Children.Add(requiredTag);
                panel.Children.Add(label);
                panel.Children.Add(ellipse);
            }

            return panel;
        }

        public Point GetPortPosition(string portName, bool isOutput, Canvas targetCanvas)
        {
            if (_portEllipses.TryGetValue((portName, isOutput), out var ellipse))
            {
                try
                {
                    return ellipse.TransformToAncestor(targetCanvas).Transform(new Point(7, 7));
                }
                catch { }
            }

            // Fallback estimate
            var left = Canvas.GetLeft(this); var top = Canvas.GetTop(this);
            left = double.IsNaN(left) ? 0 : left; top = double.IsNaN(top) ? 0 : top;

            // Use FindIndex so a missing port name returns -1 instead of falling
            // off the end of the collection and producing a position below
            // the visible node.
            int idx;
            if (isOutput)
            {
                idx = Outputs.ToList().FindIndex(o => o.Name == portName);
                if (idx < 0) idx = Math.Max(0, Outputs.Count - 1);
                return new Point(left + ActualWidth, top + 55 + idx * 22);
            }
            else
            {
                idx = Inputs.ToList().FindIndex(i => i.Name == portName);
                if (idx < 0) idx = Math.Max(0, Inputs.Count - 1);
                return new Point(left, top + 55 + idx * 22);
            }
        }

        private void Port_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Ellipse el && el.Tag is PortInfo pi)
            {
                e.Handled = true;
                OnPortMouseDown?.Invoke(this, new PortEventArgs
                {
                    PortName = pi.Name,
                    IsOutput = pi.IsOutput,
                    Position = GetEllipseCanvasPos(el)
                });
            }
        }

        private void Port_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Ellipse el && el.Tag is PortInfo pi)
            {
                e.Handled = true;
                OnPortMouseUp?.Invoke(this, new PortEventArgs
                {
                    PortName = pi.Name,
                    IsOutput = pi.IsOutput,
                    Position = GetEllipseCanvasPos(el)
                });
            }
        }

        private void Port_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Ellipse el && el.Tag is PortInfo pi)
            {
                OnPortMouseMove?.Invoke(this, new PortEventArgs
                {
                    PortName = pi.Name,
                    IsOutput = pi.IsOutput,
                    Position = GetEllipseCanvasPos(el)
                });
            }
        }

        private Point GetEllipseCanvasPos(Ellipse ellipse)
        {
            try
            {
                var pos = ellipse.TransformToAncestor(this).Transform(new Point(7, 7));
                return this.TransformToAncestor(Parent as Canvas).Transform(pos);
            }
            catch
            {
                return new Point(Canvas.GetLeft(this), Canvas.GetTop(this));
            }
        }

        private class PortInfo
        {
            public string Name { get; set; }
            public bool IsOutput { get; set; }
            public int Index { get; set; }
        }
    }


}
