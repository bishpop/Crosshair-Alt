using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.Win32;

namespace CrosshairAlt
{
    /// <summary>All values in screen pixels, like the Valorant crosshair settings.</summary>
    public sealed class CrosshairSettings
    {
        public string ColorHex { get; set; } = "#00FF00";

        public bool Outline { get; set; } = true;
        public int OutlineThickness { get; set; } = 1;
        public double OutlineOpacity { get; set; } = 0.5;

        public bool CenterDot { get; set; } = false;
        public int DotSize { get; set; } = 2;
        public double DotOpacity { get; set; } = 1.0;

        public bool InnerLines { get; set; } = true;
        public int InnerLength { get; set; } = 4;
        public int InnerThickness { get; set; } = 2;
        public int InnerGap { get; set; } = 3;
        public double InnerOpacity { get; set; } = 1.0;

        public bool OuterLines { get; set; } = false;
        public int OuterLength { get; set; } = 2;
        public int OuterThickness { get; set; } = 2;
        public int OuterGap { get; set; } = 10;
        public double OuterOpacity { get; set; } = 0.35;

        public bool TStyle { get; set; } = false;
        public int OffsetX { get; set; } = 0;
        public int OffsetY { get; set; } = 0;

        public CrosshairSettings Clone() =>
            JsonSerializer.Deserialize<CrosshairSettings>(JsonSerializer.Serialize(this));
    }

    public sealed class Profile
    {
        public string Name { get; set; }
        public CrosshairSettings Settings { get; set; }
    }

    public sealed class HotkeyBinding
    {
        public uint Modifiers { get; set; }
        public uint VirtualKey { get; set; }

        public bool IsEnabled => VirtualKey != 0;

        public HotkeyBinding() { }

        public HotkeyBinding(uint modifiers, uint virtualKey)
        {
            Modifiers = modifiers;
            VirtualKey = virtualKey;
        }

        public HotkeyBinding Clone() => new HotkeyBinding(Modifiers, VirtualKey);

        public string DisplayText()
        {
            if (VirtualKey == 0) return "None";

            var parts = new List<string>();
            if ((Modifiers & Native.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((Modifiers & Native.MOD_ALT) != 0) parts.Add("Alt");
            if ((Modifiers & Native.MOD_SHIFT) != 0) parts.Add("Shift");

            var key = KeyInterop.KeyFromVirtualKey((int)VirtualKey);
            parts.Add(FormatKeyName(key));
            return string.Join(" + ", parts);
        }

        private static string FormatKeyName(Key key)
        {
            if (key >= Key.D0 && key <= Key.D9) return ((char)('0' + (key - Key.D0))).ToString();
            if (key >= Key.NumPad0 && key <= Key.NumPad9) return "Num " + (key - Key.NumPad0);
            switch (key)
            {
                case Key.OemPlus: return "+";
                case Key.OemMinus: return "-";
                case Key.OemComma: return ",";
                case Key.OemPeriod: return ".";
                case Key.OemQuestion: return "/";
                case Key.OemTilde: return "~";
                case Key.Next: return "PageDown";
                case Key.Prior: return "PageUp";
                case Key.Back: return "Backspace";
                case Key.Capital: return "CapsLock";
                default: return key.ToString();
            }
        }
    }

    public sealed class HotkeySettings
    {
        public HotkeyBinding ToggleCrosshair { get; set; } = new HotkeyBinding(0, 0x75); // F6
        public HotkeyBinding ToggleWindow { get; set; } = new HotkeyBinding(0, 0x76);    // F7
        public HotkeyBinding OpenOptions { get; set; } = new HotkeyBinding(0, 0x77);     // F8
        public HotkeyBinding NextProfile { get; set; } = new HotkeyBinding(0, 0x78);     // F9
        public HotkeyBinding ExitApp { get; set; } = new HotkeyBinding(0, 0);            // Unbound by default

        public static HotkeySettings CreateDefault() => new HotkeySettings();

        public void EnsureDefaults()
        {
            if (ToggleCrosshair == null) ToggleCrosshair = new HotkeyBinding(0, 0x75);
            if (ToggleWindow == null) ToggleWindow = new HotkeyBinding(0, 0x76);
            if (OpenOptions == null) OpenOptions = new HotkeyBinding(0, 0x77);
            if (NextProfile == null) NextProfile = new HotkeyBinding(0, 0x78);
            if (ExitApp == null) ExitApp = new HotkeyBinding(0, 0);
        }
    }

    public sealed class AppState
    {
        public CrosshairSettings Current { get; set; } = new CrosshairSettings();
        public List<Profile> Profiles { get; set; } = new List<Profile>();
        public bool Visible { get; set; } = true;

        public HotkeySettings Hotkeys { get; set; } = new HotkeySettings();
        public bool StartMinimized { get; set; } = false;
        public bool MinimizeToTrayOnClose { get; set; } = true;

        public string ThemeName { get; set; } = "Mica Dark";
        public string BackdropMode { get; set; } = "Acrylic";
        public int TransparencyPercent { get; set; } = 50;
    }

    public sealed class NamedColor
    {
        public string Name { get; }
        public string Hex { get; }
        public NamedColor(string name, string hex) { Name = name; Hex = hex; }
    }

    public static class Store
    {
        public static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CrosshairAlt");

        private static string LegacyDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Windrad", "Crosshair");

        private static string FilePath => Path.Combine(Dir, "settings.json");
        private static string LegacyFilePath => Path.Combine(LegacyDir, "settings.json");
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };

        public static AppState Load()
        {
            try
            {
                string source = File.Exists(FilePath) ? FilePath : (File.Exists(LegacyFilePath) ? LegacyFilePath : null);
                if (source != null)
                {
                    var st = JsonSerializer.Deserialize<AppState>(File.ReadAllText(source));
                    if (st != null)
                    {
                        if (st.Current == null) st.Current = new CrosshairSettings();
                        if (st.Profiles == null) st.Profiles = new List<Profile>();
                        st.Profiles.RemoveAll(p => p == null || p.Settings == null || string.IsNullOrWhiteSpace(p.Name));
                        if (st.Hotkeys == null) st.Hotkeys = HotkeySettings.CreateDefault();
                        st.Hotkeys.EnsureDefaults();
                        if (string.IsNullOrWhiteSpace(st.ThemeName)) st.ThemeName = "Mica Dark";
                        if (string.IsNullOrWhiteSpace(st.BackdropMode)) st.BackdropMode = "Acrylic";
                        st.TransparencyPercent = Math.Max(0, Math.Min(85, st.TransparencyPercent));
                        return st;
                    }
                }
            }
            catch { /* broken file: start fresh */ }
            return new AppState();
        }

        public static void Save(AppState state)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(state, Options));
            }
            catch { /* not critical */ }
        }
    }

    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "CrosshairAlt";

        public static bool IsEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                    return key?.GetValue(ValueName) != null;
            }
            catch { return false; }
        }

        public static bool SetEnabled(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return false;
                    if (enable)
                    {
                        string exe = Environment.ProcessPath;
                        if (string.IsNullOrWhiteSpace(exe)) return false;
                        key.SetValue(ValueName, $"\"{exe}\"");
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
                    return true;
                }
            }
            catch { return false; }
        }
    }

    public static class Presets
    {
        public static readonly NamedColor[] Colors =
        {
            new NamedColor("White", "#FFFFFF"),
            new NamedColor("Green", "#00FF00"),
            new NamedColor("Chartreuse", "#7FFF00"),
            new NamedColor("Lime Yellow", "#DFFF00"),
            new NamedColor("Yellow", "#FFFF00"),
            new NamedColor("Cyan", "#00FFFF"),
            new NamedColor("Magenta", "#FF00FF"),
            new NamedColor("Red", "#FF0000"),
            new NamedColor("Coral Pink", "#FF7A95"),
            new NamedColor("Lavender", "#B18CFF"),
            new NamedColor("Rose Pink", "#FF9CCF"),
            new NamedColor("Aurora", "#5FE0A8"),
            new NamedColor("Deep Sea", "#4CC9F0"),
        };

        public static CrosshairSettings ValorantDefault() => new CrosshairSettings
        {
            ColorHex = "#FFFFFF",
            Outline = true, OutlineThickness = 1, OutlineOpacity = 0.5,
            CenterDot = false, DotSize = 2, DotOpacity = 1.0,
            InnerLines = true, InnerLength = 6, InnerThickness = 2, InnerGap = 3, InnerOpacity = 0.8,
            OuterLines = true, OuterLength = 2, OuterThickness = 2, OuterGap = 10, OuterOpacity = 0.35,
        };

        public static readonly Profile[] BuiltIn =
        {
            new Profile { Name = "Classic", Settings = new CrosshairSettings() },
            new Profile { Name = "Valorant Default", Settings = ValorantDefault() },
            new Profile { Name = "CS Classic", Settings = new CrosshairSettings
            {
                ColorHex = "#00FF00", Outline = true, OutlineThickness = 1, OutlineOpacity = 1.0,
                InnerLines = true, InnerLength = 5, InnerThickness = 1, InnerGap = 2, InnerOpacity = 1.0,
            } },
            new Profile { Name = "Dot Only", Settings = new CrosshairSettings
            {
                ColorHex = "#00FFFF", Outline = true, OutlineThickness = 1, OutlineOpacity = 0.6,
                CenterDot = true, DotSize = 3, InnerLines = false,
            } },
            new Profile { Name = "T-Style", Settings = new CrosshairSettings
            {
                ColorHex = "#FFFF00", InnerLength = 6, InnerThickness = 2, InnerGap = 3, TStyle = true,
            } },
            new Profile { Name = "Alt Lavender", Settings = new CrosshairSettings
            {
                ColorHex = "#B18CFF", OutlineOpacity = 0.6, CenterDot = true, DotSize = 2,
                InnerLength = 4, InnerThickness = 2, InnerGap = 4,
            } },
            new Profile { Name = "Alt Pink", Settings = new CrosshairSettings
            {
                ColorHex = "#FF7A95", OutlineOpacity = 0.6, CenterDot = true, DotSize = 2,
                InnerLength = 4, InnerThickness = 2, InnerGap = 4,
            } },
        };
    }

    /// <summary>
    /// Imports the primary crosshair from a Valorant profile code like "0;P;c;5;h;0;0l;4;0o;2;0a;1;1b;0".
    /// Keys that are missing keep Valorant's defaults.
    /// </summary>
    public static class ValorantCode
    {
        private static readonly string[] ColorTable =
            { "#FFFFFF", "#00FF00", "#7FFF00", "#DFFF00", "#FFFF00", "#00FFFF", "#FF00FF", "#FF0000" };

        public static bool TryImport(string code, out CrosshairSettings result, out string error)
        {
            result = null;
            error = null;
            if (string.IsNullOrWhiteSpace(code)) { error = "Please enter a Valorant profile code."; return false; }

            var t = code.Trim().Split(';');
            if (t[0].Trim() != "0") { error = "Invalid Valorant code (must start with \"0;\")."; return false; }

            var s = Presets.ValorantDefault();
            int colorIndex = 0;
            string custom = null;
            bool inPrimary = false;

            int i = 1;
            while (i < t.Length)
            {
                string key = t[i].Trim();
                if (key.Length == 0) { i++; continue; }
                if (key == "P") { inPrimary = true; i++; continue; }
                if (key == "A" || key == "S")
                {
                    if (inPrimary) break;
                    i++;
                    continue;
                }
                if (i + 1 >= t.Length) break;
                string val = t[i + 1].Trim();
                i += 2;
                if (!inPrimary) continue;

                bool num = double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double d);
                if (key == "u") { custom = val; continue; }
                if (!num) continue;

                switch (key)
                {
                    case "c": colorIndex = (int)d; break;
                    case "h": s.Outline = d != 0; break;
                    case "t": s.OutlineThickness = Clamp(d, 1, 6); break;
                    case "o": s.OutlineOpacity = Clamp01(d); break;
                    case "d": s.CenterDot = d != 0; break;
                    case "z": s.DotSize = Clamp(d, 1, 10); break;
                    case "a": s.DotOpacity = Clamp01(d); break;
                    case "0b": s.InnerLines = d != 0; break;
                    case "0t": s.InnerThickness = Clamp(d, 1, 10); break;
                    case "0l": s.InnerLength = Clamp(d, 0, 30); break;
                    case "0o": s.InnerGap = Clamp(d, 0, 40); break;
                    case "0a": s.InnerOpacity = Clamp01(d); break;
                    case "1b": s.OuterLines = d != 0; break;
                    case "1t": s.OuterThickness = Clamp(d, 1, 10); break;
                    case "1l": s.OuterLength = Clamp(d, 0, 30); break;
                    case "1o": s.OuterGap = Clamp(d, 0, 40); break;
                    case "1a": s.OuterOpacity = Clamp01(d); break;
                }
            }

            if (colorIndex == 8 && custom != null && custom.Length >= 6 && ColorUtil.TryParse(custom.Substring(0, 6), out var c))
                s.ColorHex = ColorUtil.ToHex(c);
            else if (colorIndex >= 0 && colorIndex < ColorTable.Length)
                s.ColorHex = ColorTable[colorIndex];

            result = s;
            return true;
        }

        private static int Clamp(double v, int min, int max) => (int)Math.Max(min, Math.Min(max, Math.Round(v)));
        private static double Clamp01(double v) => Math.Max(0, Math.Min(1, v));
    }
}
