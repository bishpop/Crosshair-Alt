using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CrosshairAlt
{
    public enum BackdropKind
    {
        None = 0,
        Blur = 3,
        Acrylic = 4
    }

    public static class BackdropHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public uint GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

        public static void ApplyBackdrop(Window window, bool isLight, int transparencyPercent, BackdropKind kind, int cornerRadiusDip = 12, Color? baseTintColor = null)
        {
            if (window == null) return;

            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                if (transparencyPercent <= 0 || kind == BackdropKind.None)
                {
                    UpdateRoundedRegion(window, hwnd, cornerRadiusDip);
                    SetAccent(hwnd, 0, 0, 0);
                    return;
                }

                UpdateRoundedRegion(window, hwnd, cornerRadiusDip);

                if (kind == BackdropKind.Blur)
                {
                    // ACCENT_ENABLE_BLURBEHIND (3): classic Gaussian frosted blur without dark Acrylic veil
                    SetAccent(hwnd, 3, 0, 0);
                    return;
                }

                // ACCENT_ENABLE_ACRYLICBLURBEHIND (4):
                // Must keep alpha >= 0x26 (38) in 0xAABBGGRR; ultra-low 0x01 alpha turns dark/corrupted on Win11 24H2/25H2
                double t = Math.Max(0.0, Math.Min(0.85, transparencyPercent / 100.0));
                byte acrylicAlpha = (byte)Clamp((int)Math.Round(135.0 * (1.0 - t * 0.68)), 38, 138);
                Color tint = baseTintColor ?? (isLight ? Color.FromRgb(244, 247, 255) : Color.FromRgb(13, 17, 28));
                uint gradientColor = ((uint)acrylicAlpha << 24) | ((uint)tint.B << 16) | ((uint)tint.G << 8) | tint.R;

                SetAccent(hwnd, 4, 2, gradientColor);
            }
            catch { }
        }

        public static void PauseBlurForDrag(Window window)
        {
            if (window == null) return;
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd != IntPtr.Zero)
                    SetAccent(hwnd, 0, 0, 0);
            }
            catch { }
        }

        public static void UpdateRoundedRegion(Window window, IntPtr hwnd, int cornerRadiusDip)
        {
            try
            {
                bool round = window.WindowState != WindowState.Maximized && cornerRadiusDip > 0;
                int pref = round ? 2 : 1; // DWMWCP_ROUND = 2, DWMWCP_DONOTROUND = 1
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));

                if (!round)
                {
                    SetWindowRgn(hwnd, IntPtr.Zero, true);
                    return;
                }

                double w = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
                double h = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
                if (w <= 0 || h <= 0) return;

                var dpi = VisualTreeHelper.GetDpi(window);
                int pxW = (int)Math.Round(w * dpi.DpiScaleX);
                int pxH = (int)Math.Round(h * dpi.DpiScaleY);
                int pxDia = (int)Math.Round(Math.Max(1.0, cornerRadiusDip - 0.8) * 2.0 * dpi.DpiScaleX);

                IntPtr rgn = CreateRoundRectRgn(0, 0, pxW, pxH, pxDia, pxDia);
                if (rgn != IntPtr.Zero)
                    SetWindowRgn(hwnd, rgn, true);
            }
            catch { }
        }

        private static void SetAccent(IntPtr hwnd, int accentState, int accentFlags, uint gradientColor)
        {
            var accent = new AccentPolicy
            {
                AccentState = accentState,
                AccentFlags = accentFlags,
                GradientColor = gradientColor,
                AnimationId = 0
            };

            int size = Marshal.SizeOf(accent);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = 19, // WCA_ACCENT_POLICY
                    Data = ptr,
                    SizeOfData = size
                };
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
