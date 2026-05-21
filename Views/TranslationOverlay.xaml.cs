using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace WPF_Translator_Screen.Views
{
    public partial class TranslationOverlay : Window
    {
        private readonly DispatcherTimer _closeTimer;
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TRANSPARENT = 0x00000020L;
        private const long WS_EX_LAYERED = 0x00080000L;


        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);


        public TranslationOverlay(string text, Rect screenArea, bool clickThrough = true, TimeSpan? duration = null)
        {
            InitializeComponent();

            ContentText.Text = text ?? string.Empty;

            Left = screenArea.Left;
            Top = screenArea.Top;
            Width = Math.Max(1, screenArea.Width);
            Height = Math.Max(1, screenArea.Height);

            Topmost = true;
            ShowInTaskbar = true;

            if (clickThrough)
            {
                MakeWindowClickThrough();
            }

            if (duration.HasValue)
            {
                _closeTimer = new DispatcherTimer { Interval = duration.Value };
                _closeTimer.Tick += (s, e) =>
                {
                    _closeTimer.Stop();
                    this.Close();
                };
                _closeTimer.Start();
            }
        }


        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            else
                return new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        private void MakeWindowClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            var newEx = new IntPtr(ex | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, newEx);
        }
    }
}
