using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WPF_Translator_Screen.Views
{
    public partial class SelectionWindow : Window
    {
        private System.Windows.Point _start;
        private bool _isSelecting = false;

        public Rect? SelectedArea { get; private set; }

        public SelectionWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            RootCanvas.Width = this.Width;
            RootCanvas.Height = this.Height;

            //RootCanvas.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 0, 0, 0));
            UpdateOverlay(0, 0, 0, 0);
            this.Cursor = Cursors.Cross;

            // make sure window has keyboard/mouse focus
            this.Focusable = true;
            this.Focus();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isSelecting = true;
                _start = e.GetPosition(this);

                System.Windows.Controls.Canvas.SetLeft(SelectionRect, _start.X);
                System.Windows.Controls.Canvas.SetTop(SelectionRect, _start.Y);
                SelectionRect.Width = 0;
                SelectionRect.Height = 0;
                SelectionRect.Visibility = Visibility.Visible;

                try
                {
                    Mouse.Capture(this, CaptureMode.SubTree);
                } catch { /* ignore capture failur */}

                Mouse.OverrideCursor = Cursors.Cross;
                this.Focus();
            } 
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                this.DialogResult = false;
                this.Close();
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;
            var pos = e.GetPosition(this);

            var rect = NormalizeRect(pos, _start);

            System.Windows.Controls.Canvas.SetLeft(SelectionRect, rect.X);
            System.Windows.Controls.Canvas.SetTop(SelectionRect, rect.Y);
            SelectionRect.Width = rect.Width;
            SelectionRect.Height = rect.Height;

            UpdateOverlay(rect.X, rect.Y, rect.Width, rect.Height);
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            _isSelecting = false;

            try { Mouse.Capture(null); } catch { }
            Mouse.OverrideCursor = null;

            var end = e.GetPosition(this);
            var rect = NormalizeRect(end, _start);

            var dpi = VisualTreeHelper.GetDpi(this);
            double scaleX = dpi.DpiScaleX;
            double scaleY = dpi.DpiScaleY;

            var screenX = Left + rect.X * scaleX;
            var screenY = Top + rect.Y * scaleY;
            var pixelW = Math.Max(1.0, rect.Width * scaleX);
            var pixelH = Math.Max(1.0, rect.Height * scaleY);

            SelectedArea = new Rect(screenX, screenY, pixelW, pixelH);

            this.DialogResult = true;
            this.Close();
        }

        private static Rect NormalizeRect(Point p1, Point p2)
        {
            var x = Math.Min(p1.X, p2.X);
            var y = Math.Min(p1.Y, p2.Y);
            var w = Math.Abs(p2.X - p1.X);
            var h = Math.Abs(p2.Y - p1.Y);
            return new Rect(x, y, w, h);
        }


        private void UpdateOverlay(double x, double y, double w, double h)
        {
            try
            {
                var full = new RectangleGeometry(new Rect(0, 0, RootCanvas.Width, RootCanvas.Height));
                var hole = new RectangleGeometry(new Rect(x, y, Math.Max(0, w), Math.Max(0, h)));
                var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
                group.Children.Add(full);
                group.Children.Add(hole);

                OverlayPath.Data = group;
            }
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"UpdateOverlayGeometry error: {ex.Message}");
            }
        }
    }
}
