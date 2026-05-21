using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WPF_Translator_Screen.Views
{
    public class TranslatingIndicatorOverlay : Window
    {
        public TranslatingIndicatorOverlay()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            IsHitTestVisible = false;

            // คลุมทั้งจอ
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            // Border กระพริบรอบจอ
            var border = new System.Windows.Controls.Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, 80, 160, 255)),
                BorderThickness = new Thickness(6),
                Background = new SolidColorBrush(Color.FromArgb(30, 80, 160, 255)),
                IsHitTestVisible = false
            };

            Content = border;

            Loaded += (s, e) => StartPulseAnimation(border);
        }

        private void StartPulseAnimation(System.Windows.Controls.Border border)
        {
            // กระพริบ opacity
            var animation = new DoubleAnimation
            {
                From = 0.2,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(500),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            border.BeginAnimation(OpacityProperty, animation);
        }
    }
}