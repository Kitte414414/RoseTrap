using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace RoseTrap.Controls;

/// <summary>
/// A thorny rose cane that frames the welcome screen: tapered stems with hooked prickles,
/// serrated compound leaves and closed buds. Rebuilt at real pixel size so nothing stretches.
/// </summary>
public sealed class VineFrame : Control
{
    Geometry? _petiole, _leaf, _vein, _cane, _thorn, _bud, _hl;
    Size _builtFor;

    static readonly IPen PetiolePen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#5b6a4f")), 1.1, lineCap: PenLineCap.Round);
    static readonly IPen LeafPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#6d7d5f")), .5, lineJoin: PenLineJoin.Round);
    static readonly IPen VeinPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#bf7f9070")), .55, lineCap: PenLineCap.Round);
    static readonly IPen ThornPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#a87480")), .35, lineJoin: PenLineJoin.Round);
    static readonly IPen HighlightPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#73c99aa6")), .55, lineCap: PenLineCap.Round);
    static readonly IBrush ThornFill = new ImmutableSolidColorBrush(Color.Parse("#8a5560"));
    static readonly IBrush BudFill = new RadialGradientBrush
    {
        Center = new RelativePoint(.35, .35, RelativeUnit.Relative),
        GradientOrigin = new RelativePoint(.35, .35, RelativeUnit.Relative),
        RadiusX = new RelativeScalar(.8, RelativeUnit.Relative), RadiusY = new RelativeScalar(.8, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.Parse("#d48a9d"), 0), new GradientStop(Color.Parse("#7c3449"), 1) },
    }.ToImmutable();

    public VineFrame()
    {
        IsHitTestVisible = false;
        Opacity = .62;
    }

    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size;
        if (size.Width < 50 || size.Height < 50) return;
        if (size != _builtFor) Build(size);

        var diag = new RelativePoint(size.Width, size.Height, RelativeUnit.Absolute);
        var cane = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute), EndPoint = diag,
            GradientStops = { new(Color.Parse("#7a3f4f"), 0), new(Color.Parse("#5c4a46"), .55), new(Color.Parse("#4a5a43"), 1) },
        };
        var leaf = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute), EndPoint = diag,
            GradientStops = { new(Color.Parse("#4f5d45"), 0), new(Color.Parse("#3f5340"), 1) },
        };

        ctx.DrawGeometry(null, PetiolePen, _petiole!);
        ctx.DrawGeometry(leaf, LeafPen, _leaf!);
        ctx.DrawGeometry(null, VeinPen, _vein!);
        ctx.DrawGeometry(cane, null, _cane!);
        ctx.DrawGeometry(ThornFill, ThornPen, _thorn!);
        ctx.DrawGeometry(BudFill, null, _bud!);
        ctx.DrawGeometry(null, HighlightPen, _hl!);
    }

    sealed class Paths
    {
        public readonly StringBuilder Petiole = new(), Leaf = new(), Vein = new(), Cane = new(), Thorn = new(), Bud = new(), Hl = new();
    }

    void Build(Size size)
    {
        _builtFor = size;
        double W = size.Width, H = size.Height;
        var o = new Paths();
        var rng = new Lcg(20231);

        Cane(o, rng, W, H, [(-.01, .16), (.07, .07), (.19, .075), (.31, .035), (.45, .058), (.6, .03), (.74, .06), (.87, .04),
                (.965, .1), (.982, .26), (.962, .44), (.984, .62), (.968, .8), (.93, .945), (.82, .968), (.7, .95), (.6, .975)],
            4.2, 1, [(.12, 1), (.27, .85), (.41, 1), (.56, .9), (.68, .8), (.8, 1), (.92, .75)]);
        Cane(o, rng, W, H, [(-.01, .93), (.04, .84), (.065, .72), (.05, .6), (.075, .5)], 3.2, .8, [(.35, .85), (.7, .7)]);

        static Geometry G(StringBuilder sb) => sb.Length == 0 ? new StreamGeometry() : Geometry.Parse(sb.ToString());
        _petiole = G(o.Petiole); _leaf = G(o.Leaf); _vein = G(o.Vein); _cane = G(o.Cane);
        _thorn = G(o.Thorn); _bud = G(o.Bud); _hl = G(o.Hl);
    }

    /// <summary>Deterministic Park–Miller generator so the vine looks the same on every launch.</summary>
    sealed class Lcg(long seed)
    {
        long _s = seed;
        public double Next() => (_s = _s * 16807 % 2147483647) / 2147483647.0;
    }

    static string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
    static string P((double X, double Y) p) => F(p.X) + " " + F(p.Y);

    static List<(double X, double Y)> Spline((double X, double Y)[] pts, double W, double H)
    {
        var P = pts.Select(p => (X: p.X * W, Y: p.Y * H)).ToArray();
        var raw = new List<(double X, double Y)>();
        for (int i = 0; i < P.Length - 1; i++)
        {
            var p0 = P[Math.Max(0, i - 1)]; var p1 = P[i]; var p2 = P[i + 1]; var p3 = P[Math.Min(P.Length - 1, i + 2)];
            for (int s = 0; s < 24; s++)
            {
                double t = s / 24.0, t2 = t * t, t3 = t2 * t;
                double C(double a, double b, double c, double d) =>
                    .5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
                raw.Add((C(p0.X, p1.X, p2.X, p3.X), C(p0.Y, p1.Y, p2.Y, p3.Y)));
            }
        }
        raw.Add(P[^1]);

        // Resample by arc length so prickle spacing and taper are even.
        var res = new List<(double X, double Y)> { raw[0] };
        double acc = 0;
        for (int i = 1; i < raw.Count; i++)
        {
            acc += Math.Sqrt(Math.Pow(raw[i].X - raw[i - 1].X, 2) + Math.Pow(raw[i].Y - raw[i - 1].Y, 2));
            if (acc >= 3) { res.Add(raw[i]); acc = 0; }
        }
        return res;
    }

    static void Cane(Paths o, Lcg rng, double W, double H, (double, double)[] spec, double w0, double w1, (double U, double Size)[] leaves)
    {
        var pts = Spline(spec, W, H);
        int n = pts.Count;
        (double X, double Y) Tan(int i)
        {
            var a = pts[Math.Max(0, i - 1)]; var b = pts[Math.Min(n - 1, i + 1)];
            double d = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
            if (d == 0) d = 1;
            return ((b.X - a.X) / d, (b.Y - a.Y) / d);
        }
        double Width(int i) { double u = i / (double)(n - 1); return w1 + (w0 - w1) * Math.Pow(1 - u, .85) * (1 + .06 * Math.Sin(i * .21)); }

        var left = new List<(double, double)>(); var right = new List<(double, double)>(); var hl = new List<(double, double)>();
        for (int i = 0; i < n; i++)
        {
            var t = Tan(i); double nx = -t.Y, ny = t.X, w = Width(i) / 2; var p = pts[i];
            left.Add((p.X + nx * w, p.Y + ny * w));
            right.Add((p.X - nx * w, p.Y - ny * w));
            hl.Add((p.X + nx * w * .35, p.Y + ny * w * .35));
        }
        right.Reverse();
        o.Cane.Append('M').AppendJoin("L", left.Concat(right).Select(P)).Append('Z');
        o.Hl.Append('M').AppendJoin("L", hl.Select(P));

        // Prickles: broad base, hooked back toward the cane's origin, irregular spacing.
        double next = 18 + rng.Next() * 20;
        for (int i = 4; i < n - 6; i++)
        {
            if (i * 3 < next) continue;
            next = i * 3 + 34 + rng.Next() * 46;
            var t = Tan(i); double nx = -t.Y, ny = t.X; int side = rng.Next() < .5 ? 1 : -1; double hw = Width(i) / 2; var p = pts[i];
            double k = Math.Max(.55, Width(i) / w0), b = (5.5 + rng.Next() * 2) * k, L = (6 + rng.Next() * 3) * k;
            (double, double) Q(double along, double outward) => (p.X + t.X * along + nx * side * outward, p.Y + t.Y * along + ny * side * outward);
            var A = Q(-b * .6, hw * .7); var B = Q(b * .45, hw * .7); var T = Q(-L * .55, hw + L);
            var c1 = Q(-b * .55, hw + L * .45); var c2 = Q(b * .05, hw + L * .3);
            o.Thorn.Append($"M{P(A)}Q{P(c1)} {P(T)}Q{P(c2)} {P(B)}Z");
        }

        // Leaves mostly on the side facing the window centre.
        foreach (var (u, size) in leaves)
        {
            int i = (int)Math.Round(u * (n - 1)); var t = Tan(i); double nx = -t.Y, ny = t.X; var p = pts[i];
            int inward = (W / 2 - p.X) * nx + (H / 2 - p.Y) * ny > 0 ? 1 : -1;
            int side = rng.Next() < .8 ? inward : -inward;
            LeafGroup(o, rng, (p.X + nx * side * Width(i) * .4, p.Y + ny * side * Width(i) * .4), t, side, size);
        }
        Bud(o, pts[^1], Tan(n - 1));
    }

    /// <summary>Rose leaves are compound: a stalk with a terminal leaflet and opposite pairs.</summary>
    static void LeafGroup(Paths o, Lcg rng, (double X, double Y) p, (double X, double Y) t, int side, double size)
    {
        double baseAng = Math.Atan2(t.Y, t.X) + side * (.75 + rng.Next() * .35);
        double len = (22 + rng.Next() * 10) * size, bend = side * .35;
        double ex = p.X + Math.Cos(baseAng) * len, ey = p.Y + Math.Sin(baseAng) * len;
        double cx = p.X + Math.Cos(baseAng - bend) * len * .55, cy = p.Y + Math.Sin(baseAng - bend) * len * .55;
        o.Petiole.Append($"M{P(p)}Q{F(cx)} {F(cy)} {F(ex)} {F(ey)}");
        double L = (17 + rng.Next() * 5) * size, wd = L * .36;
        Leaflet(o, ex, ey, baseAng + bend * .4, L, wd);
        double[] pairs = size > .9 ? [.48, .8] : [.7];
        for (int k = 0; k < pairs.Length; k++)
        {
            double q = pairs[k];
            double qx = (1 - q) * (1 - q) * p.X + 2 * (1 - q) * q * cx + q * q * ex;
            double qy = (1 - q) * (1 - q) * p.Y + 2 * (1 - q) * q * cy + q * q * ey;
            double sc = .72 + .16 * k;
            Leaflet(o, qx, qy, baseAng + .95, L * sc, wd * sc);
            Leaflet(o, qx, qy, baseAng - .95, L * sc, wd * sc);
        }
    }

    static void Leaflet(Paths o, double x, double y, double ang, double L, double wd)
    {
        double c = Math.Cos(ang), s = Math.Sin(ang);
        const int N = 22;
        var left = new List<(double, double)>(); var right = new List<(double, double)>();
        for (int i = 0; i <= N; i++)
        {
            double u = i / (double)N;
            double hw = wd * Math.Pow(Math.Sin(Math.PI * Math.Pow(u, .78)), .9);
            if (u > .12 && u < .94) hw *= 1 + .13 * (1 - 2 * Math.Abs(u * 9 % 1 - .5)); // serrated edge
            double ax = x + c * u * L, ay = y + s * u * L;
            left.Add((ax - s * hw, ay + c * hw));
            right.Add((ax + s * hw * .92, ay - c * hw * .92));
        }
        right.Reverse();
        o.Leaf.Append('M').AppendJoin("L", left.Concat(right).Select(P)).Append('Z');
        o.Vein.Append($"M{F(x)} {F(y)}Q{F(x + c * L * .5 + s * 1.2)} {F(y + s * L * .5 - c * 1.2)} {F(x + c * L * .92)} {F(y + s * L * .92)}");
        for (int k = 1; k <= 3; k++)
        {
            double u = k * .22, bx = x + c * u * L, by = y + s * u * L, sl = wd * .7;
            o.Vein.Append($"M{F(bx)} {F(by)}l{F(c * sl * .7 - s * sl)} {F(s * sl * .7 + c * sl)}");
            o.Vein.Append($"M{F(bx)} {F(by)}l{F(c * sl * .7 + s * sl * .8)} {F(s * sl * .7 - c * sl * .8)}");
        }
    }

    static void Bud(Paths o, (double X, double Y) p, (double X, double Y) t)
    {
        double a = Math.Atan2(t.Y, t.X), c = Math.Cos(a), s = Math.Sin(a);
        const double Lb = 13;
        string Q(double u, double v) => P((p.X + c * u - s * v, p.Y + s * u + c * v));
        o.Bud.Append($"M{Q(0, 0)}C{Q(Lb * .35, -6.5)} {Q(Lb * .9, -4.5)} {Q(Lb * 1.15, 0)}C{Q(Lb * .9, 4.5)} {Q(Lb * .35, 6.5)} {Q(0, 0)}Z");
        o.Hl.Append($"M{Q(Lb * .2, -2)}Q{Q(Lb * .6, -4.2)} {Q(Lb * 1.02, -.6)}");
        foreach (var k in new[] { -1, 1, 0 })
            o.Petiole.Append($"M{Q(0, 0)}Q{Q(Lb * .45, k * 6.5)} {Q(Lb * (k != 0 ? .78 : .95), k * 3.2)}");
    }
}
