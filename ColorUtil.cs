using System.Globalization;
using System.Windows.Media;

namespace CrosshairAlt
{
    public static class ColorUtil
    {
        /// <summary>Accepts "#rrggbb", "rrggbb", "#rgb" and "rgb".</summary>
        public static bool TryParse(string s, out Color c)
        {
            c = Colors.White;
            if (s == null) return false;
            s = s.Trim().TrimStart('#');
            if (s.Length == 3) s = new string(new[] { s[0], s[0], s[1], s[1], s[2], s[2] });
            if (s.Length != 6) return false;
            if (!int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return false;
            c = Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
            return true;
        }

        public static Color Parse(string s) => TryParse(s, out var c) ? c : Colors.White;

        public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
