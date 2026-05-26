using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using static Database_Designer.NodeWalker.NodeWalker.SessionData;
using static Database_Designer.NodeWalker.NodeWalker.SessionData.Chunk;

namespace Database_Designer.NodeWalker
{
    public partial class ChunkControl : UserControl
    {
        public event Action OnDragStarted;
        public event Action<Point> OnDragDelta;

        private Point _dragStartPosition = new Point(double.NaN, double.NaN);
        private Point _elementStartPosition;
        private bool _isDragging;
        private Point _lastDragPosition;

        private bool _isSelected;
        public bool Selected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                MainBorder.BorderBrush = value 
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 132, 255)) 
                    : ChunkBorderBrush;
            }
        }

        private System.Windows.Media.Color _chunkColor = System.Windows.Media.Color.FromRgb(255, 76, 76);
        public System.Windows.Media.Color ChunkBorderColor
        {
            get => _chunkColor;
            set
            {
                _chunkColor = value;
                ChunkBorderBrush = new System.Windows.Media.SolidColorBrush(value);
                MainBorder.BorderBrush = _isSelected 
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 132, 255)) 
                    : ChunkBorderBrush;
            }
        }

        private System.Windows.Media.Brush ChunkBorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 76, 76));

        private string _chunkTitle = "Does A Cool Thing";
        public string ChunkTitle
        {
            get => _chunkTitle;
            set
            {
                _chunkTitle = value;
                TitleText.Text = value;
            }
        }

        public ChunkControl()
        {
            InitializeComponent();
            ResizeThumb.DragDelta += ResizeThumb_DragDelta;

            MouseLeftButtonDown += ChunkControl_MouseLeftButtonDown;
            MouseMove += ChunkControl_MouseMove;
            MouseLeftButtonUp += ChunkControl_MouseLeftButtonUp;

            TitleText.MouseLeftButtonDown += TitleText_MouseLeftButtonDown;
        }

        private void TitleText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                var textBox = new System.Windows.Controls.TextBox
                {
                    Text = _chunkTitle,
                    FontSize = 19,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)),
                    BorderThickness = new Thickness(0, 0, 0, 2),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 76, 76)),
                    MinWidth = 200
                };

                textBox.LostFocus += (s, ev) =>
                {
                    _chunkTitle = textBox.Text;
                    TitleText.Text = textBox.Text;
                };

                textBox.KeyDown += (s, ev) =>
                {
                    if (ev.Key == Key.Enter || ev.Key == Key.Escape)
                    {
                        _chunkTitle = textBox.Text;
                        TitleText.Text = textBox.Text;
                    }
                };

                var parent = TitleText.Parent as System.Windows.Controls.Panel;
                if (parent != null)
                {
                    int index = parent.Children.IndexOf(TitleText);
                    parent.Children.Remove(TitleText);
                    parent.Children.Insert(index, textBox);
                    textBox.Focus();
                }
                e.Handled = true;
            }
        }

        private void ChunkControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                _dragStartPosition = e.GetPosition(Parent as Canvas);
                _lastDragPosition = _dragStartPosition;
                _elementStartPosition = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));
                if (double.IsNaN(_elementStartPosition.X)) _elementStartPosition.X = 0;
                if (double.IsNaN(_elementStartPosition.Y)) _elementStartPosition.Y = 0;
                _isDragging = false;
                e.Handled = true;
            }
        }

        private void ChunkControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentPos = e.GetPosition(Parent as Canvas);
                var offset = currentPos - _dragStartPosition;
                Canvas.SetLeft(this, _elementStartPosition.X + offset.X);
                Canvas.SetTop(this, _elementStartPosition.Y + offset.Y);
                
                var delta = currentPos - _lastDragPosition;
                _lastDragPosition = currentPos;
                OnDragDelta?.Invoke(new Point(delta.X, delta.Y));
            }
            else
            {
                var currentPos = e.GetPosition(Parent as Canvas);
                var distance = Math.Sqrt(Math.Pow(currentPos.X - _dragStartPosition.X, 2) + Math.Pow(currentPos.Y - _dragStartPosition.Y, 2));
                
                if (distance > 3)
                {
                    _isDragging = true;
                    CaptureMouse();
                    OnDragStarted?.Invoke();
                }
            }
        }

        private void ChunkControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }
            
            _dragStartPosition = new Point(double.NaN, double.NaN);
        }

        public void LoadFromChunk(Chunk chunkData)
        {
            ChunkTitle = chunkData.Name ?? "Chunk A";
            if (chunkData.BorderColor != 0)
            {
                ChunkBorderColor = System.Windows.Media.Color.FromRgb(
                    (byte)((chunkData.BorderColor >> 16) & 0xFF),
                    (byte)((chunkData.BorderColor >> 8) & 0xFF),
                    (byte)(chunkData.BorderColor & 0xFF));
            }
        }

        public uint GetBorderColorValue()
        {
            return (uint)((_chunkColor.R << 16) | (_chunkColor.G << 8) | _chunkColor.B);
        }

        public Canvas InnerCanvasControl => this.InnerCanvas;
        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            ApplyResize(e.HorizontalChange, e.VerticalChange);
        }

        private void ApplyResize(double dx, double dy)
        {
            double newWidth  = this.Width  + dx;
            double newHeight = this.Height + dy;

            if (newWidth  >= 240) this.Width  = newWidth;
            if (newHeight >= 160) this.Height = newHeight;

            InnerCanvasControl.Width  = Math.Max(200, this.Width  - 40);
            InnerCanvasControl.Height = Math.Max(120, this.Height - 110);
        }

        public void SetInitialSize(double width, double height)
        {
            this.Width = width;
            this.Height = height;
            InnerCanvasControl.Width = width - 40;
            InnerCanvasControl.Height = height - 110;
        }

        // External pointer interaction. ChunksCanvas sits below WorkspaceCanvas
        // in z-order so left-clicks on a chunk are intercepted by the workspace
        // selection layer; the host forwards them here via these methods.
        // _externalMode 0 = idle, 1 = drag, 2 = resize.
        private int _externalMode;
        private Point _externalLastCanvasPos;

        public void BeginPointerInteraction(Point canvasPos, bool isResize)
        {
            _externalMode = isResize ? 2 : 1;
            _externalLastCanvasPos = canvasPos;

            if (!isResize)
            {
                _elementStartPosition = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));
                if (double.IsNaN(_elementStartPosition.X)) _elementStartPosition.X = 0;
                if (double.IsNaN(_elementStartPosition.Y)) _elementStartPosition.Y = 0;
                OnDragStarted?.Invoke();
            }
        }

        public bool UpdatePointerInteraction(Point canvasPos)
        {
            if (_externalMode == 0) return false;
            var dx = canvasPos.X - _externalLastCanvasPos.X;
            var dy = canvasPos.Y - _externalLastCanvasPos.Y;
            _externalLastCanvasPos = canvasPos;

            if (_externalMode == 1)
            {
                var l = Canvas.GetLeft(this); var t = Canvas.GetTop(this);
                Canvas.SetLeft(this, (double.IsNaN(l) ? 0 : l) + dx);
                Canvas.SetTop(this,  (double.IsNaN(t) ? 0 : t) + dy);
                OnDragDelta?.Invoke(new Point(dx, dy));
            }
            else if (_externalMode == 2)
            {
                ApplyResize(dx, dy);
            }
            return true;
        }

        public void EndPointerInteraction()
        {
            _externalMode = 0;
        }

        public bool IsExternallyInteracting => _externalMode != 0;
    }
}