using System;
using System.Windows;
using System.Windows.Media;

namespace CrosshairAlt
{
    public sealed class ThemePreset
    {
        public string Name { get; set; }
        public bool IsLight { get; set; }
        public string BgBase { get; set; }
        public string BgPanel { get; set; }
        public string BgCard { get; set; }
        public string BgControl { get; set; }
        public string Border { get; set; }
        public string AccentPrimary { get; set; }
        public string AccentSecondary { get; set; }
        public string AccentHighlight { get; set; }
        public string Success { get; set; }
        public string Warning { get; set; }
        public string Danger { get; set; }
        public string Text { get; set; }
        public string TextMuted { get; set; }
        public string ButtonText { get; set; }
    }

    public static class ThemeManager
    {
        public static readonly string[] ThemeNames =
        {
            "Mica Dark",
            "Catppuccin Mocha",
            "Nord",
            "Catppuccin Latte",
            "Cloud"
        };

        public static readonly string[] BackdropModes =
        {
            "Acrylic",
            "Blur",
            "Clear"
        };

        public static ThemePreset CurrentTheme { get; private set; } = GetPreset("Mica Dark");
        public static string CurrentBackdropMode { get; private set; } = "Acrylic";
        public static int CurrentTransparencyPercent { get; private set; } = 50;

        public static ThemePreset GetPreset(string name)
        {
            switch (name)
            {
                case "Catppuccin Mocha":
                    return new ThemePreset
                    {
                        Name = "Catppuccin Mocha", IsLight = false,
                        BgBase = "#181825", BgPanel = "#11111B", BgCard = "#1E1E2E", BgControl = "#262738",
                        Border = "#45475A", AccentPrimary = "#CBA6F7", AccentSecondary = "#F5C2E7", AccentHighlight = "#89DCEB",
                        Success = "#A6E3A1", Warning = "#F9E2AF", Danger = "#F38BA8",
                        Text = "#CDD6F4", TextMuted = "#9399B2", ButtonText = "#11111B"
                    };
                case "Nord":
                    return new ThemePreset
                    {
                        Name = "Nord", IsLight = false,
                        BgBase = "#242933", BgPanel = "#1E222A", BgCard = "#2E3440", BgControl = "#3B4252",
                        Border = "#4C566A", AccentPrimary = "#88C0D0", AccentSecondary = "#B48EAD", AccentHighlight = "#8FBCBB",
                        Success = "#A3BE8C", Warning = "#EBCB8B", Danger = "#BF616A",
                        Text = "#ECEFF4", TextMuted = "#9AA5B8", ButtonText = "#1E222A"
                    };
                case "Catppuccin Latte":
                    return new ThemePreset
                    {
                        Name = "Catppuccin Latte", IsLight = true,
                        BgBase = "#EFF1F5", BgPanel = "#E4E8F0", BgCard = "#FFFFFF", BgControl = "#F4F5F8",
                        Border = "#CBD0DC", AccentPrimary = "#8839EF", AccentSecondary = "#EA76CB", AccentHighlight = "#04A5E5",
                        Success = "#40A02B", Warning = "#DF8E1D", Danger = "#D20F39",
                        Text = "#3C3F58", TextMuted = "#6C6F85", ButtonText = "#FFFFFF"
                    };
                case "Cloud":
                    return new ThemePreset
                    {
                        Name = "Cloud", IsLight = true,
                        BgBase = "#F8FAFC", BgPanel = "#EEF2F7", BgCard = "#FFFFFF", BgControl = "#F1F5F9",
                        Border = "#CBD5E1", AccentPrimary = "#2563EB", AccentSecondary = "#7C3AED", AccentHighlight = "#0EA5E9",
                        Success = "#16A34A", Warning = "#D97706", Danger = "#EF4444",
                        Text = "#0F172A", TextMuted = "#64748B", ButtonText = "#FFFFFF"
                    };
                default:
                    return new ThemePreset
                    {
                        Name = "Mica Dark", IsLight = false,
                        BgBase = "#0D111C", BgPanel = "#161B2E", BgCard = "#1B2236", BgControl = "#252E47",
                        Border = "#445275", AccentPrimary = "#8B9DFF", AccentSecondary = "#A78BFA", AccentHighlight = "#67E8F9",
                        Success = "#34D399", Warning = "#F5B334", Danger = "#FB7185",
                        Text = "#F3F6FF", TextMuted = "#A8B5D6", ButtonText = "#090D18"
                    };
            }
        }

        public static BackdropKind GetBackdropKind()
        {
            if (CurrentTransparencyPercent <= 0 || CurrentBackdropMode == "Clear")
                return BackdropKind.None;
            if (CurrentBackdropMode == "Blur")
                return BackdropKind.Blur;
            return BackdropKind.Acrylic;
        }

        public static void Apply(string themeName, string backdropMode, int transparencyPercent, Window targetWindow = null)
        {
            CurrentTheme = GetPreset(themeName);
            CurrentBackdropMode = Array.IndexOf(BackdropModes, backdropMode) >= 0 ? backdropMode : "Acrylic";
            CurrentTransparencyPercent = Math.Max(0, Math.Min(85, transparencyPercent));

            var res = Application.Current?.Resources;
            if (res != null)
            {
                Color cBase = ColorUtil.Parse(CurrentTheme.BgBase);
                Color cPanel = ColorUtil.Parse(CurrentTheme.BgPanel);
                Color cCard = ColorUtil.Parse(CurrentTheme.BgCard);
                Color cCtrl = ColorUtil.Parse(CurrentTheme.BgControl);
                Color cBorder = ColorUtil.Parse(CurrentTheme.Border);
                Color cAcc1 = ColorUtil.Parse(CurrentTheme.AccentPrimary);
                Color cAcc2 = ColorUtil.Parse(CurrentTheme.AccentSecondary);
                Color cHigh = ColorUtil.Parse(CurrentTheme.AccentHighlight);
                Color cSuccess = ColorUtil.Parse(CurrentTheme.Success);
                Color cWarning = ColorUtil.Parse(CurrentTheme.Warning);
                Color cDanger = ColorUtil.Parse(CurrentTheme.Danger);
                Color cText = ColorUtil.Parse(CurrentTheme.Text);
                Color cMuted = ColorUtil.Parse(CurrentTheme.TextMuted);
                Color cBtnText = ColorUtil.Parse(CurrentTheme.ButtonText);

                double t = CurrentTransparencyPercent / 100.0;
                bool usingAcrylic = CurrentTransparencyPercent > 0 && CurrentBackdropMode == "Acrylic";

                byte winAlpha = CurrentTransparencyPercent == 0
                    ? (byte)255
                    : (usingAcrylic
                        ? (byte)Clamp((int)Math.Round(130.0 * (1.0 - t * 0.82)), 16, 140)
                        : (byte)Clamp((int)Math.Round(255.0 * (1.0 - t * 0.92)), 24, 250));
                byte panelAlpha = CurrentTransparencyPercent == 0
                    ? (byte)255
                    : (byte)Clamp((int)Math.Round(190.0 * (1.0 - t * 0.78)), 42, 210);
                byte cardAlpha = CurrentTransparencyPercent == 0
                    ? (byte)255
                    : (byte)Clamp((int)Math.Round(195.0 * (1.0 - t * 0.80)), 52, 215);
                byte ctrlAlpha = CurrentTransparencyPercent == 0
                    ? (byte)255
                    : (byte)Clamp((int)Math.Round(215.0 * (1.0 - t * 0.68)), 78, 230);
                byte borderAlpha = CurrentTransparencyPercent == 0
                    ? (byte)255
                    : (byte)Clamp((int)Math.Round(255.0 * (1.0 - t * 0.28)), 150, 255);

                res["BgWindowBrush"] = new SolidColorBrush(WithAlpha(cBase, winAlpha));
                res["BgPanelBrush"] = new SolidColorBrush(WithAlpha(cPanel, panelAlpha));
                res["BgCardBrush"] = new SolidColorBrush(WithAlpha(cCard, cardAlpha));
                res["CardSurfaceBrush"] = new SolidColorBrush(WithAlpha(cCard, cardAlpha));
                res["BgControlBrush"] = new SolidColorBrush(WithAlpha(cCtrl, ctrlAlpha));
                res["BgPopupBrush"] = new SolidColorBrush(cCard);
                res["BorderBrushMain"] = new SolidColorBrush(WithAlpha(cBorder, borderAlpha));

                if (CurrentTransparencyPercent > 0)
                {
                    var winRim = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                    winRim.GradientStops.Add(new GradientStop(Color.FromArgb(CurrentTheme.IsLight ? (byte)185 : (byte)96, 255, 255, 255), 0.0));
                    winRim.GradientStops.Add(new GradientStop(WithAlpha(cHigh, CurrentTheme.IsLight ? (byte)110 : (byte)85), 0.50));
                    winRim.GradientStops.Add(new GradientStop(Color.FromArgb(CurrentTheme.IsLight ? (byte)150 : (byte)68, 255, 255, 255), 1.0));
                    res["WindowBorderBrush"] = winRim;

                    var cardRim = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                    cardRim.GradientStops.Add(new GradientStop(Color.FromArgb(CurrentTheme.IsLight ? (byte)175 : (byte)82, 255, 255, 255), 0.0));
                    cardRim.GradientStops.Add(new GradientStop(WithAlpha(cBorder, borderAlpha), 0.50));
                    cardRim.GradientStops.Add(new GradientStop(Color.FromArgb(CurrentTheme.IsLight ? (byte)135 : (byte)54, 255, 255, 255), 1.0));
                    res["CardBorderBrush"] = cardRim;
                }
                else
                {
                    res["WindowBorderBrush"] = new SolidColorBrush(cBorder);
                    res["CardBorderBrush"] = new SolidColorBrush(cBorder);
                }

                res["AccentPrimaryBrush"] = new SolidColorBrush(cAcc1);
                res["AccentSecondaryBrush"] = new SolidColorBrush(cAcc2);
                res["AccentHighlightBrush"] = new SolidColorBrush(cHigh);
                res["AccentSoftBrush"] = new SolidColorBrush(WithAlpha(cAcc1, 0x3A));
                res["SuccessBrush"] = new SolidColorBrush(cSuccess);
                res["WarningBrush"] = new SolidColorBrush(cWarning);
                res["DangerBrush"] = new SolidColorBrush(cDanger);
                res["TextNormal"] = new SolidColorBrush(cText);
                res["TextMutedBrush"] = new SolidColorBrush(cMuted);
                res["ButtonTextBrush"] = new SolidColorBrush(cBtnText);
            }

            if (targetWindow != null)
                ApplyBackdropToWindow(targetWindow);
        }

        public static void ApplyBackdropToWindow(Window window)
        {
            if (window == null) return;
            int radius = window.WindowState == WindowState.Maximized ? 0 : 12;
            Color tint = ColorUtil.Parse(CurrentTheme.BgBase);
            BackdropHelper.ApplyBackdrop(window, CurrentTheme.IsLight, CurrentTransparencyPercent, GetBackdropKind(), radius, tint);
        }

        public static void DragWindow(Window window)
        {
            if (window == null) return;
            BackdropHelper.PauseBlurForDrag(window);
            try { window.DragMove(); }
            catch { }
            finally { ApplyBackdropToWindow(window); }
        }

        private static Color WithAlpha(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);
        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
    }
}
