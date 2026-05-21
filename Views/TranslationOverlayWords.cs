using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace WPF_Translator_Screen.Views
{
    // Single-box overlay that places a wrapped translated text over a screen rectangle (screen pixels)
    public class TranslationOverlayBox : Window
    {
        private System.Windows.Threading.DispatcherTimer? _controllerTimer;

        public TranslationOverlayBox(string translatedText, Rect screenAreaPixels, bool clickThrough = false, TimeSpan? duration = null)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;

            var canvas = new Canvas { Background = Brushes.Transparent };
            Content = canvas;

            // Cover whole virtual screen (in DIPs)
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            Loaded += (s, e) =>
            {
                try
                {
                    var dpi = VisualTreeHelper.GetDpi(this);
                    double scaleX = Math.Max(0.0001, dpi.DpiScaleX);
                    double scaleY = Math.Max(0.0001, dpi.DpiScaleY);

                    // Convert screen pixel rect -> DIPs relative to window
                    double leftDip = (screenAreaPixels.Left - SystemParameters.VirtualScreenLeft) / scaleX;
                    double topDip = (screenAreaPixels.Top - SystemParameters.VirtualScreenTop) / scaleY;
                    double wDip = Math.Max(24, screenAreaPixels.Width / scaleX);
                    double hDip = Math.Max(14, screenAreaPixels.Height / scaleY);

                    double paddingX = 8;
                    double paddingY = 8;
                    string text = translatedText ?? string.Empty;

                    double maxBoxWidth = Math.Max(wDip, Width - leftDip - 8);
                    double maxBoxHeight = Math.Max(hDip, Height - topDip - 8);
                    double initialAvailableW = Math.Max(10, wDip - paddingX);
                    double fontSize = CalculateFontSizeForOriginalBox(text, initialAvailableW, hDip, paddingY);
                    double desiredTextWidth = MeasureSingleLineTextWidth(text, fontSize) + paddingX;
                    double boxWidth = Math.Clamp(desiredTextWidth, wDip, maxBoxWidth);
                    double availableW = Math.Max(10, boxWidth - paddingX);
                    fontSize = CalculateFontSizeForOriginalBox(text, availableW, hDip, paddingY);

                    double textHeight = MeasureWrappedTextHeight(text, availableW, fontSize);
                    double desiredHeight = Math.Min(Math.Max(hDip, textHeight + paddingY), maxBoxHeight);

                    var border = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4),
                        Width = boxWidth,
                        Height = desiredHeight,
                        ClipToBounds = true
                    };

                    var tb = new TextBlock
                    {
                        Text = text,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.Black,
                        TextTrimming = TextTrimming.None,
                        VerticalAlignment = VerticalAlignment.Top,
                        TextAlignment = TextAlignment.Left,
                        FontSize = fontSize,
                        LineHeight = Math.Ceiling(fontSize * 1.18),
                        MaxWidth = availableW,
                        MaxHeight = Math.Max(8, desiredHeight - paddingY),
                        ClipToBounds = true
                    };

                    border.Child = tb;

                    border.IsHitTestVisible = !clickThrough;
                    if (!clickThrough)
                    {
                        // Mouse ซ้าย/ขวา
                        border.MouseLeftButtonDown += (_, __) => Close();
                        border.MouseRightButtonDown += (_, __) => Close();

                        // Keyboard
                        this.Focusable = true;
                        this.KeyDown += (_, __) => Close();

                        // Controller — ใช้ SharpDX ที่ติดตั้งอยู่แล้ว
                        StartControllerListener();
                    }
                    Canvas.SetLeft(border, leftDip);
                    Canvas.SetTop(border, topDip);
                    canvas.Children.Add(border);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"TranslationOverlayBox load error: {ex}");
                }
            };

            if (clickThrough)
            {
                try
                {
                    MakeWindowClickThrough();
                }
                catch { /* best-effort */ }
            }

            if (duration.HasValue)
            {
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = duration.Value };
                timer.Tick += (_, __) => { timer.Stop(); Close(); };
                timer.Start();
            }
        }

        // Best-effort: set WS_EX_LAYERED | WS_EX_TRANSPARENT
        private void MakeWindowClickThrough()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).EnsureHandle();
                const int GWL_EXSTYLE = -20;
                const long WS_EX_TRANSPARENT = 0x00000020L;
                const long WS_EX_LAYERED = 0x00080000L;
                var ex = NativeMethods.GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
                NativeMethods.SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex | WS_EX_LAYERED | WS_EX_TRANSPARENT));
            }
            catch { /* ignore */ }
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
            public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
            public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        }

        private void StartControllerListener()
        {
            _controllerTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };

            _controllerTimer.Tick += (_, __) =>
            {
                try
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var controller = new SharpDX.XInput.Controller(
                            (SharpDX.XInput.UserIndex)i);

                        if (!controller.IsConnected) continue;

                        var state = controller.GetState();
                        if (state.Gamepad.Buttons != SharpDX.XInput.GamepadButtonFlags.None)
                        {
                            _controllerTimer.Stop();
                            Close();
                            return;
                        }
                    }
                }
                catch { }
            };

            _controllerTimer.Start();
        }

        private static double MeasureWrappedTextHeight(string text, double maxWidth, double fontSize)
        {
            var ft = new FormattedText(
                text ?? string.Empty,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(System.Windows.Application.Current.MainWindow).PixelsPerDip);

            ft.MaxTextWidth = Math.Max(10, maxWidth);
            ft.MaxTextHeight = double.MaxValue;

            return ft.Height;
        }

        private static double MeasureSingleLineTextWidth(string text, double fontSize)
        {
            var ft = new FormattedText(
                text ?? string.Empty,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                fontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(System.Windows.Application.Current.MainWindow).PixelsPerDip);

            return ft.WidthIncludingTrailingWhitespace;
        }

        private static double CalculateFontSizeForOriginalBox(
            string text,
            double maxWidth,
            double originalHeight,
            double verticalPadding)
        {
            double originalContentHeight = Math.Max(8, originalHeight - verticalPadding);
            double readableMinimum = 16;
            double fontSize = Math.Clamp(originalContentHeight * 0.58, readableMinimum, 24);

            while (fontSize > readableMinimum)
            {
                double textHeight = MeasureWrappedTextHeight(text, maxWidth, fontSize);
                if (textHeight <= originalContentHeight)
                    return fontSize;

                fontSize -= 0.5;
            }

            return readableMinimum;
        }

        protected override void OnClosed(EventArgs e)
        {
            _controllerTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
