using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace CrosshairAlt
{
    /// <summary>
    /// Small transparent, click-through, always-on-top window in the middle of the primary screen.
    /// It only draws on top of the screen - nothing is read from or injected into any game.
    /// </summary>
    public sealed class OverlayWindow : Window
    {
        private const int SizePx = 400;

        private readonly Canvas _canvas = new Canvas();
        private readonly DispatcherTimer _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        private CrosshairSettings _settings = new CrosshairSettings();
        private double _scale = 1.0;
        private IntPtr _hwnd = IntPtr.Zero;

        public OverlayWindow()
        {
            Title = "Crosshair Alt Overlay";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Focusable = false;
            IsHitTestVisible = false;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            Content = _canvas;

            SourceInitialized += OnSourceInitialized;
            _topmostTimer.Tick += (s, e) => Native.KeepOnTop(_hwnd);
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            var source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
                _scale = source.CompositionTarget.TransformToDevice.M11;
            Native.MakeClickThrough(_hwnd);
            Redraw();
            if (IsVisible) _topmostTimer.Start();
        }

        public void Apply(CrosshairSettings settings)
        {
            _settings = settings.Clone();
            Redraw();
        }

        public void SetOverlayVisible(bool visible)
        {
            if (visible)
            {
                if (!IsVisible) Show();
                Native.KeepOnTop(_hwnd);
                _topmostTimer.Start();
            }
            else
            {
                _topmostTimer.Stop();
                if (IsVisible) Hide();
            }
        }

        private void Redraw()
        {
            double k = 1.0 / _scale;
            Width = SizePx * k;
            Height = SizePx * k;
            Left = Math.Round(Native.ScreenWidth / 2.0 - SizePx / 2.0 + _settings.OffsetX) * k;
            Top = Math.Round(Native.ScreenHeight / 2.0 - SizePx / 2.0 + _settings.OffsetY) * k;
            CrosshairRenderer.Draw(_canvas, _settings, (SizePx / 2) * k, (SizePx / 2) * k, k);
        }

        protected override void OnClosed(EventArgs e)
        {
            _topmostTimer.Stop();
            base.OnClosed(e);
        }
    }
}
