using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using RoseTrap.Core.Settings;

namespace RoseTrap.Controls;

/// <summary>Draws a crosshair in the centre of its bounds, pixel-snapped so lines stay crisp.</summary>
public sealed class CrosshairView : Control
{
    public static readonly StyledProperty<CrosshairSettings> SettingsProperty =
        AvaloniaProperty.Register<CrosshairView, CrosshairSettings>(nameof(Settings), new CrosshairSettings());

    static CrosshairView() => AffectsRender<CrosshairView>(SettingsProperty);

    public CrosshairSettings Settings
    {
        get => GetValue(SettingsProperty);
        set => SetValue(SettingsProperty, value);
    }

    public CrosshairView()
    {
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext ctx)
    {
        var s = Settings;
        var color = Color.TryParse(s.Color, out var c) ? c : Colors.White;
        var fill = new SolidColorBrush(color, Math.Clamp(s.Opacity, .1, 1));
        var outline = new SolidColorBrush(Colors.Black, Math.Clamp(s.Opacity, .1, 1) * .85);

        // Snap the centre to a pixel (or half pixel for odd thicknesses) so 1px lines don't blur.
        double t = Math.Max(1, Math.Round(s.Thickness));
        double cx = Math.Floor(Bounds.Width / 2) + (t % 2 == 1 ? .5 : 0);
        double cy = Math.Floor(Bounds.Height / 2) + (t % 2 == 1 ? .5 : 0);
        double gap = Math.Round(s.Gap), len = Math.Round(s.Size);

        var shapes = new List<Geometry>();
        void Arm(double x, double y, double w, double h) => shapes.Add(new RectangleGeometry(new Rect(x, y, w, h)));
        void Arms(bool top)
        {
            Arm(cx + gap, cy - t / 2, len, t);          // right
            Arm(cx - gap - len, cy - t / 2, len, t);    // left
            Arm(cx - t / 2, cy + gap, t, len);          // bottom
            if (top) Arm(cx - t / 2, cy - gap - len, t, len);
        }
        void Dot(double r) => shapes.Add(new EllipseGeometry(new Rect(cx - r, cy - r, r * 2, r * 2)));

        switch (s.Style)
        {
            case CrosshairStyle.Cross: Arms(top: true); break;
            case CrosshairStyle.CrossDot: Arms(top: true); Dot(Math.Max(1, t * .75)); break;
            case CrosshairStyle.T: Arms(top: false); break;
            case CrosshairStyle.Dot: Dot(Math.Max(1.5, t)); break;
            case CrosshairStyle.Circle:
            {
                double r = gap + len / 2;
                var ring = new EllipseGeometry(new Rect(cx - r, cy - r, r * 2, r * 2));
                if (s.Outline) ctx.DrawGeometry(null, new Pen(outline, t + 2), ring);
                ctx.DrawGeometry(null, new Pen(fill, t), ring);
                Dot(Math.Max(1, t * .6));
                break;
            }
        }

        if (s.Outline)
            foreach (var g in shapes)
                ctx.DrawGeometry(null, new Pen(outline, 2, lineJoin: PenLineJoin.Miter), g);
        foreach (var g in shapes) ctx.DrawGeometry(fill, null, g);
    }
}
