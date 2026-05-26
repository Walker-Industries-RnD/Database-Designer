using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using static Database_Designer.NodeWalker.NodeWalker;

namespace Database_Designer.NodeWalker
{
    public partial class NoteControl : UserControl
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
                NoteBorder.BorderBrush = value 
                    ? new SolidColorBrush(Color.FromRgb(76, 132, 255)) 
                    : new SolidColorBrush(Color.FromArgb(3, 255, 217, 0));
                NoteBorder.BorderThickness = value ? new Thickness(3) : new Thickness(3);
            }
        }

        public string NoteText
        {
            get => NoteTextBox.Text;
            set => NoteTextBox.Text = value;
        }

        public NoteControl()
        {
            InitializeComponent();

            MouseLeftButtonDown += NoteControl_MouseLeftButtonDown;
            MouseMove += NoteControl_MouseMove;
            MouseLeftButtonUp += NoteControl_MouseLeftButtonUp;
        }

        private void NoteControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

        private void NoteControl_MouseMove(object sender, MouseEventArgs e)
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

        private void NoteControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }
            
            _dragStartPosition = new Point(double.NaN, double.NaN);
        }

        public void LoadFromNote(SessionData.Note note)
        {
            NoteTextBox.Text = note.Description ?? "New Note";
        }

        private void NoteTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            NoteTextBox.Height = double.NaN;
            var size = new Size(NoteTextBox.ActualWidth, double.PositiveInfinity);
            NoteTextBox.Measure(size);
            double desired = NoteTextBox.DesiredSize.Height + 40;
            if (desired > NoteTextBox.MinHeight)
                NoteTextBox.Height = desired;
        }
    }

}
