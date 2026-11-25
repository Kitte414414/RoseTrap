using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RoseTrap.Theme;

/// <summary>
/// Filled 24×24 icon geometries, registered as "Icon.*" resources. Composite icons cut their
/// overlay out of the base shape, so they look right on any background (hover, active, etc.).
/// </summary>
internal static class Icons
{
    public static void Register(IResourceDictionary r)
    {
        r["Icon.General"] = P(IconData.General);
        r["Icon.Global"] = CutOut(P(IconData.Globe), P(IconData.Cursor), scale: .62, x: 10.6, y: 10.4, gap: 1.6);
        r["Icon.FFlags"] = P(IconData.FFlags);
        r["Icon.FFlagsSettings"] = Knockout(P(IconData.Square), P(IconData.Gear), scale: .58);
        r["Icon.Preset"] = P(IconData.Preset);
        r["Icon.Shaders"] = P(IconData.Shaders);
        r["Icon.Settings"] = Fit(P(IconData.Tools), IconData.ToolsWidth, IconData.ToolsHeight, inset: 2.5);
        r["Icon.Appearance"] = P(IconData.Appearance);
        foreach (var (key, data) in new[]
                 {
                     ("Potato", IconData.Potato), ("Balanced", IconData.Balanced), ("Quality", IconData.Quality),
                     ("Custom", IconData.Custom), ("Play", IconData.Play), ("Close", IconData.Close), ("Folder", IconData.Folder),
                 })
            r["Icon." + key] = P(data);
    }

    static Geometry P(string data) => Geometry.Parse(data);

    static Geometry Transformed(Geometry g, Matrix m)
    {
        var copy = g.Clone();
        copy.Transform = new MatrixTransform(m);
        return copy;
    }

    /// <summary>Base shape with an overlay placed on top, separated by a transparent gap.</summary>
    static Geometry CutOut(Geometry baseShape, Geometry overlay, double scale, double x, double y, double gap)
    {
        var placed = Transformed(overlay, Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y));
        var b = placed.Bounds;
        // The gap is a slightly larger copy of the overlay, scaled about its own centre.
        double k = (b.Width + 2 * gap) / b.Width;
        var halo = Transformed(overlay, Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)
                                        * Matrix.CreateTranslation(-b.Center.X, -b.Center.Y)
                                        * Matrix.CreateScale(k, k)
                                        * Matrix.CreateTranslation(b.Center.X, b.Center.Y));
        return new GeometryGroup
        {
            FillRule = FillRule.NonZero,
            Children = { new CombinedGeometry(GeometryCombineMode.Exclude, baseShape, halo), placed },
        };
    }

    /// <summary>Base shape with the overlay punched through it, centred.</summary>
    static Geometry Knockout(Geometry baseShape, Geometry overlay, double scale)
    {
        double o = 12 - 12 * scale;
        var placed = Transformed(overlay, Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(o, o));
        return new CombinedGeometry(GeometryCombineMode.Exclude, baseShape, placed);
    }

    /// <summary>Scales a non-24px icon into the 24×24 box, centred.</summary>
    static Geometry Fit(Geometry g, double w, double h, double inset)
    {
        double box = 24 - 2 * inset, s = box / Math.Max(w, h);
        return Transformed(g, Matrix.CreateScale(s, s) * Matrix.CreateTranslation(inset + (box - w * s) / 2, inset + (box - h * s) / 2));
    }
}
