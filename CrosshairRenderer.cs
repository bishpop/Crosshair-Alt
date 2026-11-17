using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CrosshairAlt
{
    /// <summary>
    /// Draws the crosshair pixel-exact. All math is in whole screen pixels relative to the center
    /// pixel (0,0); k converts one screen pixel to WPF units (1/dpi for the overlay, zoom/dpi for the preview).
    /// </summary>
    public static class CrosshairRenderer
    {
        public static void Draw(Canvas canvas, CrosshairSettings s, double cx, double cy, double k)
        {
            canvas.Children.Clear();
            RenderOptions.SetEdgeMode(canvas, EdgeMode.Aliased);

            var dot = new List<Int32Rect>();
            var inner = new List<Int32Rect>();
            var outer = new List<Int32Rect>();

            if (s.CenterDot && s.DotSize > 0)
            {
                int o = -((s.DotSize - 1) / 2);
                dot.Add(new Int32Rect(o, o, s.DotSize, s.DotSize));
            }
            if (s.InnerLines) AddArms(inner, s.InnerLength, s.InnerThickness, s.InnerGap, s.TStyle);
            if (s.OuterLines) AddArms(outer, s.OuterLength, s.OuterThickness, s.OuterGap, s.TStyle);

            if (s.Outline && s.OutlineThickness > 0 && s.OutlineOpacity > 0)
            {
                var all = new List<Int32Rect>();
                if (s.InnerOpacity > 0) all.AddRange(inner);
                if (s.OuterOpacity > 0) all.AddRange(outer);
                if (s.DotOpacity > 0) all.AddRange(dot);
                if (all.Count > 0)
                    canvas.Children.Add(MakePath(all, s.OutlineThickness, Brushes.Black, s.OutlineOpacity, cx, cy, k));
            }

            var fill = new SolidColorBrush(ColorUtil.Parse(s.ColorHex));
            fill.Freeze();
            if (outer.Count > 0) canvas.Children.Add(MakePath(outer, 0, fill, s.OuterOpacity, cx, cy, k));
            if (inner.Count > 0) canvas.Children.Add(MakePath(inner, 0, fill, s.InnerOpacity, cx, cy, k));
            if (dot.Count > 0) canvas.Children.Add(MakePath(dot, 0, fill, s.DotOpacity, cx, cy, k));
        }

        /// <summary>Four arms around the center pixel; "gap" free pixels between center and each arm.</summary>
        private static void AddArms(List<Int32Rect> list, int length, int thickness, int gap, bool tStyle)
        {
            if (length <= 0 || thickness <= 0) return;
            int a = -((thickness - 1) / 2);
            list.Add(new Int32Rect(1 + gap, a, length, thickness));                    // right
            list.Add(new Int32Rect(-gap - length, a, length, thickness));              // left
            list.Add(new Int32Rect(a, 1 + gap, thickness, length));                    // bottom
            if (!tStyle) list.Add(new Int32Rect(a, -gap - length, thickness, length)); // top
        }

        /// <summary>One Path per part, so overlapping rectangles don't double their opacity.</summary>
        private static Path MakePath(List<Int32Rect> rects, int expand, Brush brush, double opacity,
                                     double cx, double cy, double k)
        {
            var group = new GeometryGroup { FillRule = FillRule.Nonzero };
            foreach (var r in rects)
            {
                group.Children.Add(new RectangleGeometry(new Rect(
                    cx + (r.X - expand) * k,
                    cy + (r.Y - expand) * k,
                    (r.Width + 2 * expand) * k,
                    (r.Height + 2 * expand) * k)));
            }
            group.Freeze();
            return new Path { Data = group, Fill = brush, Opacity = opacity, SnapsToDevicePixels = true };
        }
    }
}
