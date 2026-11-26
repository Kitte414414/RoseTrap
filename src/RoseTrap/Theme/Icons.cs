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
        // Material Icons "sports_esports" (Apache 2.0).
        r["Icon.Gameplay"] = P("M21.58 16.09l-1.09-7.66C20.21 6.46 18.52 5 16.53 5H7.47C5.48 5 3.79 6.46 3.51 8.43l-1.09 7.66C2.2 17.63 3.39 19 4.94 19c.68 0 1.32-.27 1.8-.75L9 16h6l2.25 2.25c.48.48 1.13.75 1.8.75 1.56 0 2.75-1.37 2.53-2.91zM11 11H9v2H8v-2H6v-1h2V8h1v2h2v1zm4-1c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm2 3c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1z");
        r["Icon.Global"] = CutOut(P(IconData.Globe), P(IconData.Cursor), scale: .62, x: 10.6, y: 10.4, gap: 1.6);
        r["Icon.FFlags"] = P(IconData.FFlags);
        r["Icon.FFlagsSettings"] = Knockout(P(IconData.Square), P(IconData.Gear), scale: .58);
        r["Icon.Preset"] = P(IconData.Preset);
        r["Icon.Shaders"] = P(IconData.Shaders);
        r["Icon.Settings"] = Fit(P(IconData.Tools), IconData.ToolsWidth, IconData.ToolsHeight, inset: 2.5);
        r["Icon.Appearance"] = P(IconData.Appearance);
        r["Icon.Mods"] = P(IconData.Mods);
        foreach (var (key, data) in new[]
                 {
                     ("Potato", IconData.Potato), ("Balanced", IconData.Balanced), ("Quality", IconData.Quality),
                     ("Custom", IconData.Custom), ("Play", IconData.Play), ("Close", IconData.Close), ("Folder", IconData.Folder),
                     ("Shooter", IconData.Shooter), ("Fight", IconData.Fight), ("World", IconData.World),
                     ("Simulator", IconData.Simulator), ("Atmosphere", IconData.Atmosphere), ("Sparkle", IconData.Sparkle),
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
