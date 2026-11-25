using RoseTrap.Core.Imaging;

namespace RoseTrap.Core.Mods;

public sealed record CursorStyle(string Name, string Description);

/// <summary>
/// Draws cursor and shift-lock images. Roblox's cursors are 64×64 with the click point in the
/// centre; the shift-lock icon is 32×32. Shapes are signed-distance functions, 4×4 supersampled.
/// </summary>
public static class CursorArt
{
    public const string Default = "Default";

    public static readonly IReadOnlyList<CursorStyle> Cursors =
    [
        new(Default, "Roblox's own pointer and hand."),
        new("Rose", "Rose-pink arrow with a dark outline; turns white over buttons."),
        new("Classic", "The old white arrow with a black outline."),
        new("Midnight", "Black arrow with a white outline. Easy to see on bright maps."),
        new("Dot", "A small dot that grows into a ring over buttons. Great for aiming."),
        new("Crosshair", "A thin plus sign with a gap in the middle."),
    ];

    public static readonly IReadOnlyList<CursorStyle> ShiftLocks =
    [
        new(Default, "Roblox's own shift-lock icon."),
        new("Rose ring", "A thin rose ring with a dot."),
        new("Dot", "Just a dot."),
        new("Plus", "A small plus sign."),
    ];

    /// <summary>Normal pointer (ArrowFarCursor.png).</summary>
    public static byte[] Pointer(string style) => Png.Encode(Render(64, PointerLayers(style, hover: false)), 64, 64, 4);

    /// <summary>Pointer over something clickable (ArrowCursor.png).</summary>
    public static byte[] Hover(string style) => Png.Encode(Render(64, PointerLayers(style, hover: true)), 64, 64, 4);

    /// <summary>Shift-lock icon (MouseLockedCursor.png).</summary>
    public static byte[] ShiftLock(string style) => Png.Encode(Render(32, ShiftLockLayers(style)), 32, 32, 4);

    /// <summary>RGBA pixels for previews.</summary>
    public static byte[] PointerRgba(string style, bool hover) => Render(64, PointerLayers(style, hover));
    public static byte[] ShiftLockRgba(string style) => Render(32, ShiftLockLayers(style));

    delegate double Sdf(double x, double y);
    sealed record Layer(Sdf Shape, uint Argb, double Grow = 0);

    // Arrow outline in cursor units, tip at (0,0), pointing up-left (classic proportions).
    static readonly (double X, double Y)[] Arrow =
        [(0, 0), (0, 17), (4.2, 13.2), (7.2, 20), (10, 18.8), (7.1, 12.2), (12.4, 12.2)];

    static IEnumerable<Layer> PointerLayers(string style, bool hover)
    {
        const double c = 32;
        switch (style)
        {
            case "Rose":
                return ArrowLayers(1.25, hover ? 0xFFFFFFFF : 0xFFF3ADBD, hover ? 0xFFC2506E : 0xFF3A1520);
            case "Classic":
                return ArrowLayers(1.2, 0xFFFFFFFF, 0xFF000000);
            case "Midnight":
                return ArrowLayers(1.2, 0xFF141416, 0xFFFFFFFF);
            case "Dot":
                return hover
                    ? [new(Ring(c, c, 6, 1.4), 0xFF1A1A1A, 1.1), new(Ring(c, c, 6, 1.4), 0xFFFFFFFF), new(Circle(c, c, 1.6), 0xFFFFFFFF)]
                    : [new(Circle(c, c, 3), 0xFF1A1A1A, 1.1), new(Circle(c, c, 3), 0xFFFFFFFF)];
            case "Crosshair":
            {
                var plus = Plus(c, c, hover ? 9 : 7, 1, 3);
                return [new(plus, 0xFF101010, 1), new(plus, hover ? 0xFFF3ADBD : 0xFFFFFFFF)];
            }
            default:
                throw new ArgumentException($"Unknown cursor style '{style}'.");
        }
    }

    static IEnumerable<Layer> ArrowLayers(double scale, uint fill, uint outline)
    {
        var pts = Arrow.Select(p => (32 + p.X * scale, 32 + p.Y * scale)).ToArray();
        var shadow = pts.Select(p => (p.Item1 + 1.4, p.Item2 + 1.8)).ToArray();
        return
        [
            new(Polygon(shadow), 0x50000000, 1.8),
            new(Polygon(pts), outline, 1.4),
            new(Polygon(pts), fill),
        ];
    }

    static IEnumerable<Layer> ShiftLockLayers(string style)
    {
        const double c = 16;
        return style switch
        {
            "Rose ring" => [new(Ring(c, c, 8, 1.6), 0xC0000000, 1), new(Ring(c, c, 8, 1.6), 0xFFF3ADBD), new(Circle(c, c, 1.8), 0xFFF3ADBD)],
            "Dot" => [new(Circle(c, c, 3), 0xC0000000, 1), new(Circle(c, c, 3), 0xFFFFFFFF)],
            "Plus" => [new(Plus(c, c, 6, .9, 0), 0xC0000000, 1), new(Plus(c, c, 6, .9, 0), 0xFFFFFFFF)],
            _ => throw new ArgumentException($"Unknown shift-lock style '{style}'."),
        };
    }

    // ---------- shapes (negative inside) ----------

    static Sdf Circle(double cx, double cy, double r) => (x, y) => Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

    static Sdf Ring(double cx, double cy, double r, double halfWidth) =>
        (x, y) => Math.Abs(Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r) - halfWidth;

    /// <summary>Plus sign: arms of length <paramref name="len"/>, half-thickness <paramref name="t"/>, centre gap.</summary>
    static Sdf Plus(double cx, double cy, double len, double t, double gap) => (x, y) =>
    {
        double dx = Math.Abs(x - cx), dy = Math.Abs(y - cy), mid = gap / 2 + len / 2;
        return Math.Min(Box(dx - mid, dy, len / 2, t), Box(dx, dy - mid, t, len / 2));
    };

    static double Box(double px, double py, double hx, double hy)
    {
        double qx = Math.Abs(px) - hx, qy = Math.Abs(py) - hy;
        return Math.Sqrt(Math.Max(qx, 0) * Math.Max(qx, 0) + Math.Max(qy, 0) * Math.Max(qy, 0)) + Math.Min(Math.Max(qx, qy), 0);
    }

    static Sdf Polygon((double X, double Y)[] p) => (x, y) =>
    {
        double d = double.MaxValue;
        bool inside = false;
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
        {
            var (ax, ay) = p[j]; var (bx, by) = p[i];
            double ex = bx - ax, ey = by - ay, wx = x - ax, wy = y - ay;
            double t = Math.Clamp((wx * ex + wy * ey) / (ex * ex + ey * ey), 0, 1);
            double qx = wx - ex * t, qy = wy - ey * t;
            d = Math.Min(d, qx * qx + qy * qy);
            if ((ay > y) != (by > y) && x < ax + (y - ay) * ex / ey) inside = !inside;
        }
        return inside ? -Math.Sqrt(d) : Math.Sqrt(d);
    };

    // ---------- rasteriser ----------

    static byte[] Render(int size, IEnumerable<Layer> layers)
    {
        var list = layers.ToList();
        var px = new byte[size * size * 4];
        const int ss = 4;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            double r = 0, g = 0, b = 0, a = 0; // premultiplied
            foreach (var layer in list)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                    if (layer.Shape(x + (sx + .5) / ss, y + (sy + .5) / ss) - layer.Grow <= 0) hits++;
                if (hits == 0) continue;
                double la = ((layer.Argb >> 24) & 255) / 255.0 * hits / (ss * ss);
                double lr = ((layer.Argb >> 16) & 255) / 255.0, lg = ((layer.Argb >> 8) & 255) / 255.0, lb = (layer.Argb & 255) / 255.0;
                r = lr * la + r * (1 - la); g = lg * la + g * (1 - la); b = lb * la + b * (1 - la); a = la + a * (1 - la);
            }
            int i = (y * size + x) * 4;
            if (a > 0)
            {
                px[i] = (byte)Math.Round(r / a * 255);
                px[i + 1] = (byte)Math.Round(g / a * 255);
                px[i + 2] = (byte)Math.Round(b / a * 255);
                px[i + 3] = (byte)Math.Round(a * 255);
            }
        }
        return px;
    }
}
