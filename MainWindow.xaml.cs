using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace CrosshairAlt
{
    public partial class MainWindow : Window
    {
        private enum ActiveTab { Crosshair, Options }

        private const int HotkeyToggleCrosshair = 1;
        private const int HotkeyToggleWindow = 2;
        private const int HotkeyOpenOptions = 3;
        private const int HotkeyNextProfile = 4;
        private const int HotkeyExitApp = 5;

        private static readonly Func<double, string> Px = v => $"{v:0} px";
        private static readonly Func<double, string> Pct = v => $"{v * 100:0} %";

        private readonly AppState _state;
        private readonly OverlayWindow _overlay;
        private System.Windows.Forms.NotifyIcon _tray;
        private readonly DispatcherTimer _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        private readonly Dictionary<int, bool> _hotkeyStatus = new Dictionary<int, bool>();

        private IntPtr _hwnd = IntPtr.Zero;
        private bool _exiting;
        private int _zoom = 4;
        private double _dpi = 1.0;
        private ActiveTab _activeTab = ActiveTab.Crosshair;
        private int? _capturingHotkeyId;
        private bool _suppressOptionsEvents;

        public bool StartMinimizedOnBoot => _state.StartMinimized;

        public MainWindow()
        {
            InitializeComponent();
            _state = Store.Load();

            ThemeManager.Apply(_state.ThemeName, _state.BackdropMode, _state.TransparencyPercent);

            _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); Store.Save(_state); };
            SourceInitialized += OnSourceInitialized;
            Loaded += (s, e) => ThemeManager.ApplyBackdropToWindow(this);
            ContentRendered += (s, e) => ThemeManager.ApplyBackdropToWindow(this);
            SizeChanged += (s, e) => ThemeManager.ApplyBackdropToWindow(this);
            IsVisibleChanged += (s, e) => { if (IsVisible) ThemeManager.ApplyBackdropToWindow(this); };
            StateChanged += OnWindowStateChanged;
            Closing += OnClosing;
            PreviewKeyDown += OnWindowPreviewKeyDown;

            BuildSettings();
            BuildProfiles();
            BuildOptionsControls();

            VisibleSwitch.IsChecked = _state.Visible;
            VisibleSwitch.Checked += (s, e) => SetOverlayVisible(true);
            VisibleSwitch.Unchecked += (s, e) => SetOverlayVisible(false);
            UpdateCrosshairPill();

            BgSky.Checked += (s, e) => SetPreviewBackdrop("Sky");
            BgDark.Checked += (s, e) => SetPreviewBackdrop("Dark");
            BgLight.Checked += (s, e) => SetPreviewBackdrop("Light");

            Zoom1.Checked += (s, e) => { _zoom = 1; RenderPreview(); };
            Zoom4.Checked += (s, e) => { _zoom = 4; RenderPreview(); };
            Zoom8.Checked += (s, e) => { _zoom = 8; RenderPreview(); };
            PreviewCanvas.SizeChanged += (s, e) => RenderPreview();

            SaveProfileButton.Click += OnSaveProfile;
            DeleteProfileButton.Click += OnDeleteProfile;
            ImportButton.Click += OnImport;
            TrayButton.Click += (s, e) => Hide();

            ResetHotkeysButton.Click += (s, e) =>
            {
                _state.Hotkeys = HotkeySettings.CreateDefault();
                RegisterAllHotkeys();
                BuildHotkeysList();
                UpdateHotkeyHints();
                QueueSave();
                SetStatus("Hotkeys reset to defaults.");
            };

            OpenConfigFolderButton.Click += (s, e) =>
            {
                try
                {
                    Directory.CreateDirectory(Store.Dir);
                    Process.Start(new ProcessStartInfo("explorer.exe", Store.Dir) { UseShellExecute = true });
                }
                catch { }
            };
            ExitAppButton.Click += (s, e) => ExitApp();

            CreateTray();

            _overlay = new OverlayWindow();
            _overlay.Apply(_state.Current);
            _overlay.SetOverlayVisible(_state.Visible);

            // Ensure HWND exists immediately so global hotkeys work even when started minimized
            new WindowInteropHelper(this).EnsureHandle();

            UpdateHotkeyHints();
            SetStatus(_state.MinimizeToTrayOnClose
                ? "Running. Closing minimizes to the system tray; right-click tray icon to exit."
                : "Running.");
        }

        // --------------------------------------------------------------- window & hotkeys

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            Native.UseDarkTitleBar(_hwnd);

            var source = HwndSource.FromHwnd(_hwnd);
            source.AddHook(WndProc);
            if (source.CompositionTarget != null)
                _dpi = source.CompositionTarget.TransformToDevice.M11;

            ThemeManager.ApplyBackdropToWindow(this);
            RegisterAllHotkeys();
            BuildHotkeysList();
            RenderPreview();
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            bool max = WindowState == WindowState.Maximized;
            BtnMaximize.Content = max ? "\uE923" : "\uE922";
            var radius = max ? new CornerRadius(0) : new CornerRadius(12);
            RootFrame.CornerRadius = radius;
            OuterGlassRim.CornerRadius = radius;
            TopBarBorder.CornerRadius = max ? new CornerRadius(0) : new CornerRadius(12, 12, 0, 0);
            FooterBorder.CornerRadius = max ? new CornerRadius(0) : new CornerRadius(0, 0, 12, 12);
            ThemeManager.ApplyBackdropToWindow(this);
        }

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                ToggleMaximizeRestore();
            else if (e.LeftButton == MouseButtonState.Pressed)
                ThemeManager.DragWindow(this);
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => ToggleMaximizeRestore();
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void ToggleMaximizeRestore()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void BtnTabCrosshair_Click(object sender, RoutedEventArgs e) => SwitchTab(ActiveTab.Crosshair);
        private void BtnTabOptions_Click(object sender, RoutedEventArgs e) => SwitchTab(ActiveTab.Options);

        private void SwitchTab(ActiveTab tab)
        {
            _activeTab = tab;
            bool isCrosshair = tab == ActiveTab.Crosshair;
            CrosshairView.Visibility = isCrosshair ? Visibility.Visible : Visibility.Collapsed;
            OptionsView.Visibility = isCrosshair ? Visibility.Collapsed : Visibility.Visible;
            BtnTabCrosshair.Style = (Style)FindResource(isCrosshair ? "ActiveNavTabButtonStyle" : "NavTabButtonStyle");
            BtnTabOptions.Style = (Style)FindResource(isCrosshair ? "NavTabButtonStyle" : "ActiveNavTabButtonStyle");
        }

        private void BtnTopbarCrosshairPill_Click(object sender, RoutedEventArgs e)
        {
            VisibleSwitch.IsChecked = VisibleSwitch.IsChecked != true;
        }

        private void RegisterAllHotkeys()
        {
            if (_hwnd == IntPtr.Zero) return;
            UnregisterAllHotkeys();

            _hotkeyStatus.Clear();
            var hk = _state.Hotkeys;
            bool anyConflict = false;

            anyConflict |= !TryRegisterOne(HotkeyToggleCrosshair, hk.ToggleCrosshair);
            anyConflict |= !TryRegisterOne(HotkeyToggleWindow, hk.ToggleWindow);
            anyConflict |= !TryRegisterOne(HotkeyOpenOptions, hk.OpenOptions);
            anyConflict |= !TryRegisterOne(HotkeyNextProfile, hk.NextProfile);
            anyConflict |= !TryRegisterOne(HotkeyExitApp, hk.ExitApp);

            if (anyConflict)
                SetStatus("One or more hotkeys could not be registered (already used by another app). Check Options.", true);
        }

        private bool TryRegisterOne(int id, HotkeyBinding binding)
        {
            if (binding == null || !binding.IsEnabled)
            {
                _hotkeyStatus[id] = true;
                return true;
            }
            bool ok = Native.RegisterHotKey(_hwnd, id, binding.Modifiers | Native.MOD_NOREPEAT, binding.VirtualKey);
            _hotkeyStatus[id] = ok;
            return ok;
        }

        private void UnregisterAllHotkeys()
        {
            if (_hwnd == IntPtr.Zero) return;
            for (int id = HotkeyToggleCrosshair; id <= HotkeyExitApp; id++)
                Native.UnregisterHotKey(_hwnd, id);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Native.WM_HOTKEY)
            {
                if (_capturingHotkeyId.HasValue)
                {
                    handled = true;
                    return IntPtr.Zero;
                }

                int id = wParam.ToInt32();
                switch (id)
                {
                    case HotkeyToggleCrosshair:
                        VisibleSwitch.IsChecked = VisibleSwitch.IsChecked != true;
                        break;
                    case HotkeyToggleWindow:
                        ToggleWindowVisibility();
                        break;
                    case HotkeyOpenOptions:
                        ShowMenu();
                        SwitchTab(ActiveTab.Options);
                        break;
                    case HotkeyNextProfile:
                        CycleNextProfile();
                        break;
                    case HotkeyExitApp:
                        ExitApp();
                        break;
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_capturingHotkeyId.HasValue) return;

            e.Handled = true;
            Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            int targetId = _capturingHotkeyId.Value;
            _capturingHotkeyId = null;

            if (key == Key.Escape)
            {
                RegisterAllHotkeys();
                BuildHotkeysList();
                SetStatus("Hotkey capture canceled.");
                return;
            }

            uint mods = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods |= Native.MOD_CONTROL;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods |= Native.MOD_ALT;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods |= Native.MOD_SHIFT;

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            if (vk == 0)
            {
                RegisterAllHotkeys();
                BuildHotkeysList();
                return;
            }

            var binding = GetHotkeyBinding(targetId);
            binding.Modifiers = mods;
            binding.VirtualKey = vk;

            RegisterAllHotkeys();
            BuildHotkeysList();
            UpdateHotkeyHints();
            QueueSave();

            if (_hotkeyStatus.TryGetValue(targetId, out bool ok) && ok)
                SetStatus($"Hotkey bound to {binding.DisplayText()}.");
        }

        private HotkeyBinding GetHotkeyBinding(int id)
        {
            switch (id)
            {
                case HotkeyToggleCrosshair: return _state.Hotkeys.ToggleCrosshair;
                case HotkeyToggleWindow: return _state.Hotkeys.ToggleWindow;
                case HotkeyOpenOptions: return _state.Hotkeys.OpenOptions;
                case HotkeyNextProfile: return _state.Hotkeys.NextProfile;
                case HotkeyExitApp: return _state.Hotkeys.ExitApp;
                default: return _state.Hotkeys.ToggleCrosshair;
            }
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (_exiting) return;
            if (_state.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            ExitApp();
        }

        public void ShowMenu()
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            ThemeManager.ApplyBackdropToWindow(this);
        }

        private void ToggleWindowVisibility()
        {
            if (IsVisible && WindowState != WindowState.Minimized)
                Hide();
            else
                ShowMenu();
        }

        private void ExitApp()
        {
            if (_exiting) return;
            _exiting = true;
            Store.Save(_state);
            UnregisterAllHotkeys();
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            _overlay.Close();
            Close();
            Application.Current.Shutdown();
        }

        private void CreateTray()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open Crosshair Alt", null, (o, e) => ShowMenu());
            menu.Items.Add("Toggle Crosshair", null, (o, e) => VisibleSwitch.IsChecked = VisibleSwitch.IsChecked != true);
            menu.Items.Add("Options & Hotkeys", null, (o, e) => { ShowMenu(); SwitchTab(ActiveTab.Options); });
            menu.Items.Add("Next Profile", null, (o, e) => CycleNextProfile());
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (o, e) => ExitApp());

            var res = Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
            _tray = new System.Windows.Forms.NotifyIcon
            {
                Icon = new System.Drawing.Icon(res.Stream),
                Text = "Crosshair Alt",
                ContextMenuStrip = menu,
                Visible = true
            };
            _tray.DoubleClick += (o, e) => ToggleWindowVisibility();
        }

        // ------------------------------------------------------------- crosshair settings UI

        private StackPanel AddSettingsCard(string title, bool first = false)
        {
            var card = new Border
            {
                Margin = new Thickness(0, first ? 0 : 12, 0, 0),
                Padding = new Thickness(16, 14, 16, 16)
            };
            card.SetResourceReference(Border.StyleProperty, "CardBorderStyle");

            var stack = new StackPanel();
            var header = new TextBlock
            {
                Text = title,
                Style = (Style)FindResource("Section"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            stack.Children.Add(header);
            card.Child = stack;
            SettingsPanel.Children.Add(card);
            return stack;
        }

        private void BuildSettings()
        {
            SettingsPanel.Children.Clear();
            var s = _state.Current;

            var colorCard = AddSettingsCard("COLOR", true);
            AddColorRow(colorCard, s);

            var outlineCard = AddSettingsCard("OUTLINE");
            AddSwitch(outlineCard, "Enable Outline", () => s.Outline, v => s.Outline = v);
            AddSlider(outlineCard, "Thickness", 1, 4, 1, () => s.OutlineThickness, v => s.OutlineThickness = (int)Math.Round(v), Px);
            AddSlider(outlineCard, "Opacity", 0, 1, 0.05, () => s.OutlineOpacity, v => s.OutlineOpacity = v, Pct);

            var dotCard = AddSettingsCard("CENTER DOT");
            AddSwitch(dotCard, "Enable Center Dot", () => s.CenterDot, v => s.CenterDot = v);
            AddSlider(dotCard, "Size", 1, 10, 1, () => s.DotSize, v => s.DotSize = (int)Math.Round(v), Px);
            AddSlider(dotCard, "Opacity", 0, 1, 0.05, () => s.DotOpacity, v => s.DotOpacity = v, Pct);

            var innerCard = AddSettingsCard("INNER LINES");
            AddSwitch(innerCard, "Enable Inner Lines", () => s.InnerLines, v => s.InnerLines = v);
            AddSlider(innerCard, "Length", 0, 30, 1, () => s.InnerLength, v => s.InnerLength = (int)Math.Round(v), Px);
            AddSlider(innerCard, "Thickness", 1, 10, 1, () => s.InnerThickness, v => s.InnerThickness = (int)Math.Round(v), Px);
            AddSlider(innerCard, "Gap", 0, 40, 1, () => s.InnerGap, v => s.InnerGap = (int)Math.Round(v), Px);
            AddSlider(innerCard, "Opacity", 0, 1, 0.05, () => s.InnerOpacity, v => s.InnerOpacity = v, Pct);

            var outerCard = AddSettingsCard("OUTER LINES");
            AddSwitch(outerCard, "Enable Outer Lines", () => s.OuterLines, v => s.OuterLines = v);
            AddSlider(outerCard, "Length", 0, 30, 1, () => s.OuterLength, v => s.OuterLength = (int)Math.Round(v), Px);
            AddSlider(outerCard, "Thickness", 1, 10, 1, () => s.OuterThickness, v => s.OuterThickness = (int)Math.Round(v), Px);
            AddSlider(outerCard, "Gap", 0, 40, 1, () => s.OuterGap, v => s.OuterGap = (int)Math.Round(v), Px);
            AddSlider(outerCard, "Opacity", 0, 1, 0.05, () => s.OuterOpacity, v => s.OuterOpacity = v, Pct);

            var shapeCard = AddSettingsCard("STYLE & POSITION OFFSET");
            AddSwitch(shapeCard, "T-Style (hide top line)", () => s.TStyle, v => s.TStyle = v);
            AddSlider(shapeCard, "Offset X", -50, 50, 1, () => s.OffsetX, v => s.OffsetX = (int)Math.Round(v), Px);
            AddSlider(shapeCard, "Offset Y", -50, 50, 1, () => s.OffsetY, v => s.OffsetY = (int)Math.Round(v), Px);
        }

        private void AddSwitch(Panel parent, string label, Func<bool> get, Action<bool> set)
        {
            var cb = new CheckBox
            {
                Content = label,
                Style = (Style)FindResource("Switch"),
                IsChecked = get(),
                Margin = new Thickness(0, 4, 0, 4)
            };
            cb.Checked += (o, e) => { set(true); Changed(); };
            cb.Unchecked += (o, e) => { set(false); Changed(); };
            parent.Children.Add(cb);
        }

        private void AddSlider(Panel parent, string label, double min, double max, double step,
                               Func<double> get, Action<double> set, Func<double, string> format)
        {
            var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });

            var name = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12.5
            };
            name.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");

            var valueText = new TextBlock
            {
                Text = format(get()),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold
            };
            valueText.SetResourceReference(TextBlock.ForegroundProperty, "TextNormal");

            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                TickFrequency = step,
                IsSnapToTickEnabled = true,
                SmallChange = step,
                LargeChange = step,
                Value = get(),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            slider.ValueChanged += (o, e) =>
            {
                set(e.NewValue);
                valueText.Text = format(e.NewValue);
                Changed();
            };

            Grid.SetColumn(slider, 1);
            Grid.SetColumn(valueText, 2);
            grid.Children.Add(name);
            grid.Children.Add(slider);
            grid.Children.Add(valueText);
            parent.Children.Add(grid);
        }

        private void AddColorRow(Panel parent, CrosshairSettings s)
        {
            var hex = new TextBox
            {
                Width = 108,
                MaxLength = 7,
                Text = ColorUtil.ToHex(ColorUtil.Parse(s.ColorHex)),
                VerticalAlignment = VerticalAlignment.Top
            };
            hex.TextChanged += (o, e) =>
            {
                if (ColorUtil.TryParse(hex.Text, out Color c))
                {
                    hex.ClearValue(Control.BorderBrushProperty);
                    s.ColorHex = ColorUtil.ToHex(c);
                    Changed();
                }
                else
                {
                    hex.SetResourceReference(Control.BorderBrushProperty, "DangerBrush");
                }
            };

            var dots = new WrapPanel { Margin = new Thickness(0, 0, 12, 0) };
            foreach (var nc in Presets.Colors)
            {
                string h = nc.Hex;
                var dot = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(12),
                    Margin = new Thickness(0, 0, 8, 6),
                    Background = new SolidColorBrush(ColorUtil.Parse(h)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1.2),
                    Cursor = Cursors.Hand,
                    ToolTip = nc.Name + " (" + h + ")"
                };
                dot.MouseLeftButtonUp += (o, e) => hex.Text = h;
                dots.Children.Add(dot);
            }

            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
            DockPanel.SetDock(hex, Dock.Right);
            row.Children.Add(hex);
            row.Children.Add(dots);
            parent.Children.Add(row);
        }

        // ------------------------------------------------------------- options & hotkeys UI

        private void BuildOptionsControls()
        {
            _suppressOptionsEvents = true;

            BuildHotkeysList();

            ThemeChipsPanel.Children.Clear();
            foreach (string name in ThemeManager.ThemeNames)
            {
                string themeName = name;
                var chip = new RadioButton
                {
                    Style = (Style)FindResource("Chip"),
                    GroupName = "ThemePreset",
                    Content = themeName,
                    IsChecked = string.Equals(_state.ThemeName, themeName, StringComparison.OrdinalIgnoreCase),
                    Margin = new Thickness(0, 0, 8, 8)
                };
                chip.Checked += (s, e) =>
                {
                    if (_suppressOptionsEvents) return;
                    _state.ThemeName = themeName;
                    ApplyThemeNow();
                    QueueSave();
                };
                ThemeChipsPanel.Children.Add(chip);
            }

            BackdropChipsPanel.Children.Clear();
            foreach (string mode in ThemeManager.BackdropModes)
            {
                string modeName = mode;
                var chip = new RadioButton
                {
                    Style = (Style)FindResource("Chip"),
                    GroupName = "BackdropMode",
                    Content = modeName,
                    IsChecked = string.Equals(_state.BackdropMode, modeName, StringComparison.OrdinalIgnoreCase)
                };
                chip.Checked += (s, e) =>
                {
                    if (_suppressOptionsEvents) return;
                    _state.BackdropMode = modeName;
                    ApplyThemeNow();
                    QueueSave();
                };
                BackdropChipsPanel.Children.Add(chip);
            }

            TransparencySlider.Value = _state.TransparencyPercent;
            TransparencyValueText.Text = $"{_state.TransparencyPercent}%";
            TransparencySlider.ValueChanged += (s, e) =>
            {
                if (_suppressOptionsEvents) return;
                _state.TransparencyPercent = (int)Math.Round(e.NewValue);
                TransparencyValueText.Text = $"{_state.TransparencyPercent}%";
                ApplyThemeNow();
                QueueSave();
            };

            MinimizeToTraySwitch.IsChecked = _state.MinimizeToTrayOnClose;
            MinimizeToTraySwitch.Checked += (s, e) => { _state.MinimizeToTrayOnClose = true; QueueSave(); };
            MinimizeToTraySwitch.Unchecked += (s, e) => { _state.MinimizeToTrayOnClose = false; QueueSave(); };

            StartMinimizedSwitch.IsChecked = _state.StartMinimized;
            StartMinimizedSwitch.Checked += (s, e) => { _state.StartMinimized = true; QueueSave(); };
            StartMinimizedSwitch.Unchecked += (s, e) => { _state.StartMinimized = false; QueueSave(); };

            LaunchAtStartupSwitch.IsChecked = StartupHelper.IsEnabled();
            LaunchAtStartupSwitch.Checked += (s, e) =>
            {
                if (_suppressOptionsEvents) return;
                if (!StartupHelper.SetEnabled(true))
                {
                    LaunchAtStartupSwitch.IsChecked = false;
                    SetStatus("Could not update Windows startup entry.", true);
                }
            };
            LaunchAtStartupSwitch.Unchecked += (s, e) =>
            {
                if (_suppressOptionsEvents) return;
                StartupHelper.SetEnabled(false);
            };

            ConfigPathText.Text = "Config: " + Path.Combine(Store.Dir, "settings.json");

            _suppressOptionsEvents = false;
        }

        private void BuildHotkeysList()
        {
            HotkeysListPanel.Children.Clear();
            var hk = _state.Hotkeys;

            AddHotkeyRow(HotkeyToggleCrosshair, "Toggle Crosshair", "Show or hide the crosshair overlay", hk.ToggleCrosshair);
            AddHotkeyRow(HotkeyToggleWindow, "Show / Hide App", "Toggle the Crosshair Alt window to and from the tray", hk.ToggleWindow);
            AddHotkeyRow(HotkeyOpenOptions, "Open Options", "Show the window and jump straight to Options & Hotkeys", hk.OpenOptions);
            AddHotkeyRow(HotkeyNextProfile, "Next Profile", "Cycle through built-in and custom crosshair profiles", hk.NextProfile);
            AddHotkeyRow(HotkeyExitApp, "Exit Application", "Quit Crosshair Alt completely", hk.ExitApp, last: true);
        }

        private void AddHotkeyRow(int id, string title, string description, HotkeyBinding binding, bool last = false)
        {
            var rowBorder = new Border
            {
                CornerRadius = new CornerRadius(9),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, last ? 0 : 8)
            };
            rowBorder.SetResourceReference(Border.BackgroundProperty, "BgControlBrush");
            rowBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrushMain");

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var titleTxt = new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold };
            titleTxt.SetResourceReference(TextBlock.ForegroundProperty, "TextNormal");
            var descTxt = new TextBlock { Text = description, FontSize = 11.5, Margin = new Thickness(0, 2, 0, 0) };
            descTxt.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
            info.Children.Add(titleTxt);
            info.Children.Add(descTxt);
            Grid.SetColumn(info, 0);
            grid.Children.Add(info);

            // Status pill
            bool isCapturing = _capturingHotkeyId == id;
            bool isEnabled = binding != null && binding.IsEnabled;
            bool isOk = !_hotkeyStatus.TryGetValue(id, out bool regOk) || regOk;

            var statusBadge = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(12, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var statusLabel = new TextBlock { FontSize = 10.5, FontWeight = FontWeights.Bold };
            if (isCapturing)
            {
                statusBadge.SetResourceReference(Border.BorderBrushProperty, "AccentHighlightBrush");
                statusLabel.SetResourceReference(TextBlock.ForegroundProperty, "AccentHighlightBrush");
                statusLabel.Text = "LISTENING";
            }
            else if (!isEnabled)
            {
                statusBadge.SetResourceReference(Border.BorderBrushProperty, "BorderBrushMain");
                statusLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
                statusLabel.Text = "DISABLED";
            }
            else if (isOk)
            {
                statusBadge.SetResourceReference(Border.BorderBrushProperty, "SuccessBrush");
                statusLabel.SetResourceReference(TextBlock.ForegroundProperty, "SuccessBrush");
                statusLabel.Text = "ACTIVE";
            }
            else
            {
                statusBadge.SetResourceReference(Border.BorderBrushProperty, "DangerBrush");
                statusLabel.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
                statusLabel.Text = "CONFLICT";
            }
            statusBadge.Child = statusLabel;
            Grid.SetColumn(statusBadge, 1);
            grid.Children.Add(statusBadge);

            // Capture key button
            var keyBtn = new Button
            {
                Style = (Style)FindResource(isCapturing ? "PrimaryButton" : "FlatButton"),
                MinWidth = 145,
                Height = 32,
                Content = isCapturing ? "Press key..." : (binding?.DisplayText() ?? "None"),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                Margin = new Thickness(0, 0, 8, 0)
            };
            keyBtn.Click += (s, e) =>
            {
                _capturingHotkeyId = id;
                UnregisterAllHotkeys();
                BuildHotkeysList();
                SetStatus($"Listening for new key for \"{title}\" (Esc to cancel)...");
            };
            Grid.SetColumn(keyBtn, 2);
            grid.Children.Add(keyBtn);

            // Clear button
            var clearBtn = new Button
            {
                Style = (Style)FindResource("FlatButton"),
                Content = "Clear",
                Height = 32,
                Padding = new Thickness(10, 0, 10, 0),
                IsEnabled = isEnabled && !isCapturing
            };
            clearBtn.Click += (s, e) =>
            {
                binding.Modifiers = 0;
                binding.VirtualKey = 0;
                RegisterAllHotkeys();
                BuildHotkeysList();
                UpdateHotkeyHints();
                QueueSave();
                SetStatus($"Cleared hotkey for \"{title}\".");
            };
            Grid.SetColumn(clearBtn, 3);
            grid.Children.Add(clearBtn);

            rowBorder.Child = grid;
            HotkeysListPanel.Children.Add(rowBorder);
        }

        private void ApplyThemeNow()
        {
            ThemeManager.Apply(_state.ThemeName, _state.BackdropMode, _state.TransparencyPercent, this);
            UpdateCrosshairPill();
        }

        private void UpdateHotkeyHints()
        {
            var hk = _state.Hotkeys;
            var parts = new List<string>();
            if (hk.ToggleCrosshair.IsEnabled) parts.Add($"{hk.ToggleCrosshair.DisplayText()} Crosshair");
            if (hk.ToggleWindow.IsEnabled) parts.Add($"{hk.ToggleWindow.DisplayText()} Window");
            if (hk.OpenOptions.IsEnabled) parts.Add($"{hk.OpenOptions.DisplayText()} Options");
            if (hk.NextProfile.IsEnabled) parts.Add($"{hk.NextProfile.DisplayText()} Profile");
            HotkeyHintText.Text = string.Join("  ·  ", parts);
        }

        // ------------------------------------------------------------- preview, overlay, status

        private void SetPreviewBackdrop(string kind)
        {
            if (PreviewBackdrop == null) return;
            if (kind == "Dark")
            {
                PreviewBackdrop.Background = new SolidColorBrush(Color.FromRgb(18, 20, 28));
            }
            else if (kind == "Light")
            {
                PreviewBackdrop.Background = new SolidColorBrush(Color.FromRgb(225, 232, 240));
            }
            else
            {
                var lg = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
                lg.GradientStops.Add(new GradientStop(Color.FromRgb(59, 74, 92), 0));
                lg.GradientStops.Add(new GradientStop(Color.FromRgb(110, 114, 100), 0.55));
                lg.GradientStops.Add(new GradientStop(Color.FromRgb(46, 43, 38), 1));
                PreviewBackdrop.Background = lg;
            }
        }

        private void Changed()
        {
            _overlay?.Apply(_state.Current);
            RenderPreview();
            QueueSave();
        }

        private void QueueSave()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void RenderPreview()
        {
            if (PreviewCanvas.ActualWidth <= 0 || PreviewCanvas.ActualHeight <= 0) return;
            double k = _zoom / _dpi;
            double cx = Math.Round(PreviewCanvas.ActualWidth * _dpi / 2) / _dpi;
            double cy = Math.Round(PreviewCanvas.ActualHeight * _dpi / 2) / _dpi;
            CrosshairRenderer.Draw(PreviewCanvas, _state.Current, cx, cy, k);
        }

        private void SetOverlayVisible(bool visible)
        {
            _state.Visible = visible;
            _overlay.SetOverlayVisible(visible);
            UpdateCrosshairPill();
            QueueSave();
        }

        private void UpdateCrosshairPill()
        {
            if (BtnTopbarCrosshairPill == null) return;
            if (_state.Visible)
            {
                BtnTopbarCrosshairPill.Content = "CROSSHAIR ON";
                BtnTopbarCrosshairPill.SetResourceReference(Button.BorderBrushProperty, "SuccessBrush");
                BtnTopbarCrosshairPill.SetResourceReference(Button.ForegroundProperty, "SuccessBrush");
            }
            else
            {
                BtnTopbarCrosshairPill.Content = "CROSSHAIR OFF";
                BtnTopbarCrosshairPill.SetResourceReference(Button.BorderBrushProperty, "BorderBrushMain");
                BtnTopbarCrosshairPill.SetResourceReference(Button.ForegroundProperty, "TextMutedBrush");
            }
        }

        private void SetStatus(string text, bool error = false)
        {
            StatusText.Text = text;
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, error ? "DangerBrush" : "TextMutedBrush");
        }

        // ------------------------------------------------------------- profiles & import

        private void BuildProfiles()
        {
            ProfilePanel.Children.Clear();
            foreach (var p in Presets.BuiltIn) ProfilePanel.Children.Add(ProfileButton(p, false));
            foreach (var p in _state.Profiles) ProfilePanel.Children.Add(ProfileButton(p, true));
        }

        private Button ProfileButton(Profile p, bool own)
        {
            var b = new Button
            {
                Content = p.Name,
                Style = (Style)FindResource("FlatButton"),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 6, 6)
            };
            if (own) b.SetResourceReference(Button.BackgroundProperty, "AccentSoftBrush");
            b.Click += (o, e) =>
            {
                LoadSettings(p.Settings, !own);
                if (own) ProfileName.Text = p.Name;
                SetStatus($"Loaded profile \"{p.Name}\".");
            };
            return b;
        }

        private void CycleNextProfile()
        {
            var all = new List<Profile>(Presets.BuiltIn);
            all.AddRange(_state.Profiles);
            if (all.Count == 0) return;

            string currentJson = System.Text.Json.JsonSerializer.Serialize(_state.Current);
            int idx = all.FindIndex(p => System.Text.Json.JsonSerializer.Serialize(p.Settings) == currentJson);
            int nextIdx = (idx + 1) % all.Count;
            var next = all[nextIdx];
            bool isBuiltIn = nextIdx < Presets.BuiltIn.Length;
            LoadSettings(next.Settings, isBuiltIn);
            SetStatus($"Switched to profile \"{next.Name}\".");
        }

        /// <summary>Built-in presets and imports keep your current position offset.</summary>
        private void LoadSettings(CrosshairSettings source, bool keepOffset)
        {
            var n = source.Clone();
            if (keepOffset)
            {
                n.OffsetX = _state.Current.OffsetX;
                n.OffsetY = _state.Current.OffsetY;
            }
            _state.Current = n;
            BuildSettings();
            Changed();
        }

        private void OnSaveProfile(object sender, RoutedEventArgs e)
        {
            string name = (ProfileName.Text ?? "").Trim();
            if (name.Length == 0) { SetStatus("Please enter a profile name.", true); return; }

            var existing = _state.Profiles.Find(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) existing.Settings = _state.Current.Clone();
            else _state.Profiles.Add(new Profile { Name = name, Settings = _state.Current.Clone() });

            BuildProfiles();
            Store.Save(_state);
            SetStatus($"Saved profile \"{name}\".");
        }

        private void OnDeleteProfile(object sender, RoutedEventArgs e)
        {
            string name = (ProfileName.Text ?? "").Trim();
            int removed = _state.Profiles.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) { SetStatus("No custom profile found with that name.", true); return; }

            ProfileName.Text = "";
            BuildProfiles();
            Store.Save(_state);
            SetStatus($"Deleted profile \"{name}\".");
        }

        private void OnImport(object sender, RoutedEventArgs e)
        {
            if (ValorantCode.TryImport(ValorantCodeBox.Text, out var imported, out string error))
            {
                LoadSettings(imported, true);
                SetStatus("Imported Valorant crosshair profile.");
            }
            else SetStatus(error, true);
        }
    }
}
