using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace RoseTrap.Controls;

/// <summary>
/// A stemless 3D rose drawn with text characters. Petal surfaces are sampled once,
/// then every frame is rotated, depth-buffered into a character grid and shaded.
/// </summary>
public sealed class AsciiRose : Control
{
    const int Cols = 92, Rows = 46;
    const double CellW = 6, CellH = 9.2, GridW = Cols * CellW, GridH = Rows * CellH;
    const double Tilt = -0.8;
    const string Ramp = " .,:;irsXA253hMHGS#9B&@";
    const int Levels = 48;

    static readonly Lazy<float[]> Model = new(BuildModel);
    static readonly (double X, double Y, double Z) Light = Normalize(-.42, .6, .68);

    readonly float[] _zbuf = new float[Cols * Rows];
    readonly float[] _lbuf = new float[Cols * Rows];
    readonly Dictionary<int, FormattedText> _glyphs = new();
    readonly IBrush[] _palette = BuildPalette();
    double _scale, _cx, _cy;

    double _spin = .6, _momentum;
    TimeSpan? _last;
    bool _running, _dragging;
    Point _dragFrom;
    long _dragTicks;

    public AsciiRose()
    {
        ClipToBounds = false;
        Cursor = new Cursor(StandardCursorType.Hand);
        FitToGrid();
    }

    /// <summary>Drag to spin; the rose keeps the flick's momentum.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _dragging = true; _momentum = 0;
        _dragFrom = e.GetPosition(this); _dragTicks = Environment.TickCount64;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetPosition(this);
        long now = Environment.TickCount64;
        double dx = (p.X - _dragFrom.X) * .011;
        _spin += dx;
        _momentum = dx / Math.Max(8, now - _dragTicks) * .9;
        _dragFrom = p; _dragTicks = now;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        _dragging = false;
        e.Pointer.Capture(null);
    }

    bool _attached, _active = true;

    /// <summary>True while the animation loop is running (attached and not paused).</summary>
    public bool IsRunning => _running;

    /// <summary>Pauses the loop while the welcome page is hidden: zero cost off-screen.</summary>
    public bool IsActive
    {
        get => _active;
        set { _active = value; UpdateLoop(); }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        UpdateLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        UpdateLoop();
        base.OnDetachedFromVisualTree(e);
    }

    void UpdateLoop()
    {
        bool run = _attached && _active;
        if (run == _running) return;
        _running = run;
        if (!run) return;
        _last = null;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Tick);
    }

    void Tick(TimeSpan now)
    {
        if (!_running) return;
        var top = TopLevel.GetTopLevel(this);
        // Text art doesn't need more than ~33 fps.
        if (_last is null || (now - _last.Value).TotalMilliseconds >= 30)
        {
            Advance(_last is null ? 0 : Math.Min(64, (now - _last.Value).TotalMilliseconds));
            _last = now;
            InvalidateVisual();
        }
        top?.RequestAnimationFrame(Tick);
    }

    /// <summary>Moves the rotation forward by <paramref name="ms"/> milliseconds.</summary>
    public void Advance(double ms)
    {
        if (_dragging) return;
        _spin += ms * (.00055 + _momentum);
        _momentum *= Math.Exp(-ms / 700);
    }

    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size;
        if (size.Width < 1 || size.Height < 1) return;
        Rasterize();

        double k = Math.Min(size.Width / GridW, size.Height / GridH);
        var offset = new Point((size.Width - GridW * k) / 2, (size.Height - GridH * k) / 2);
        using var _ = ctx.PushTransform(Matrix.CreateScale(k, k) * Matrix.CreateTranslation(offset.X, offset.Y));

        for (int i = 0; i < _zbuf.Length; i++)
        {
            if (_zbuf[i] < -1e8f) continue;
            double s = _lbuf[i];
            int ci = Math.Clamp((int)(Math.Clamp((s - .03) / .96, 0, 1) * (Ramp.Length - 1)), 1, Ramp.Length - 1);
            int li = (int)(s * (Levels - 1));
            var text = Glyph(ci, li);
            double x = (i % Cols) * CellW + CellW / 2 - text.Width / 2;
            double y = (i / Cols) * CellH + CellH / 2 - text.Height / 2;
            ctx.DrawText(text, new Point(x, y));
        }
    }

    FormattedText Glyph(int ci, int level)
    {
        int key = ci * Levels + level;
        if (!_glyphs.TryGetValue(key, out var t))
        {
            t = new FormattedText(Ramp[ci].ToString(), System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 9.2, _palette[level]);
            _glyphs[key] = t;
        }
        return t;
    }

    void Rasterize()
    {
        var p = Model.Value;
        double ca = Math.Cos(_spin), sa = Math.Sin(_spin);
        double tilt = Tilt + .035 * Math.Sin(Environment.TickCount64 * .0007);
        double ct = Math.Cos(tilt), st = Math.Sin(tilt);
        Array.Fill(_zbuf, -1e9f);

        for (int b = 0; b < p.Length; b += 7)
        {
            double x = p[b], y = p[b + 1], z = p[b + 2];
            double rx = x * ca - y * sa, ry0 = x * sa + y * ca;
            double ry = ry0 * ct - z * st, depth = ry0 * st + z * ct;
            int col = (int)((_cx + rx * _scale) / CellW), row = (int)((_cy - ry * _scale) / CellH);
            if ((uint)col >= Cols || (uint)row >= Rows) continue;
            int idx = row * Cols + col;
            if (depth < _zbuf[idx]) continue;

            double nx = p[b + 3], ny = p[b + 4], nz = p[b + 5];
            double rnx = nx * ca - ny * sa, rny0 = nx * sa + ny * ca;
            double rny = rny0 * ct - nz * st, rnz = rny0 * st + nz * ct;
            if (rnz < 0) { rnx = -rnx; rny = -rny; rnz = -rnz; } // petals are two-sided
            // Half-Lambert keeps petals seen edge-on readable instead of collapsing to dots.
            double diffuse = Math.Pow((rnx * Light.X + rny * Light.Y + rnz * Light.Z) * .5 + .5, 1.7);
            double spec = Math.Pow(Math.Max(0, rnz * .85 + rny * .22), 20) * .35;
            double rim = (1 - rnz) * (1 - rnz) * .11;
            _zbuf[idx] = (float)depth;
            _lbuf[idx] = (float)Math.Clamp(.16 + diffuse * .9 + spec * .6 + rim + p[b + 6], 0, 1);
        }
    }

    /// <summary>Scale and centre the model so it fills the grid at every spin angle.</summary>
    void FitToGrid()
    {
        var p = Model.Value;
        double ct = Math.Cos(Tilt), st = Math.Sin(Tilt);
        double x0 = 1e9, x1 = -1e9, y0 = 1e9, y1 = -1e9;
        for (double a = 0; a < Math.PI * 2; a += Math.PI / 8)
        {
            double ca = Math.Cos(a), sa = Math.Sin(a);
            for (int b = 0; b < p.Length; b += 7 * 5)
            {
                double rx = p[b] * ca - p[b + 1] * sa, ry = (p[b] * sa + p[b + 1] * ca) * ct - p[b + 2] * st;
                x0 = Math.Min(x0, rx); x1 = Math.Max(x1, rx); y0 = Math.Min(y0, ry); y1 = Math.Max(y1, ry);
            }
        }
        const double margin = 10;
        _scale = Math.Min((GridW - 2 * margin) / (x1 - x0), (GridH - 2 * margin) / (y1 - y0 + .08));
        _cx = GridW / 2 - (x0 + x1) / 2 * _scale;
        _cy = GridH / 2 + (y0 + y1) / 2 * _scale;
    }

    static IBrush[] BuildPalette()
    {
        Color lo = Color.FromRgb(77, 22, 39), mid = Color.FromRgb(176, 65, 96), hi = Color.FromRgb(255, 215, 226);
        var brushes = new IBrush[Levels];
        for (int i = 0; i < Levels; i++)
        {
            double s = i / (double)(Levels - 1);
            var (a, b, t) = s < .52 ? (lo, mid, s / .52) : (mid, hi, (s - .52) / .48);
            byte L(byte u, byte v) => (byte)Math.Round(u + (v - u) * t);
            brushes[i] = new ImmutableSolidColorBrush(Color.FromRgb(L(a.R, b.R), L(a.G, b.G), L(a.B, b.B)));
        }
        return brushes;
    }

    static (double, double, double) Normalize(double x, double y, double z)
    {
        double l = Math.Sqrt(x * x + y * y + z * z);
        return (x / l, y / l, z / l);
    }

    // ---- Model: four rings of cupped petals around a spiral bud. x y z nx ny nz tone per sample. ----

    record struct Layer(int Count, double Base, double Len, double Width, double Z, double Belly, double Edge,
        double Tip, double Twist, double Tone, double Phase, double Rise);

    const double RadiusScale = .72, CupTaper = .55, CupHeight = 2, CentreHeight = .95;

    static float[] BuildModel()
    {
        var s = new List<float>(48_000 * 7);
        Layer[] layers =
        [
            new(8, .17, .94, .32, -.105, .10, .40, .075, .15, -.055, .16, .32),
            new(7, .12, .69, .30, .015, .14, .32, .09, .18, .02, .55, .52),
            new(6, .065, .49, .235, .12, .15, .26, .125, .21, .085, .22, .6),
            new(5, .018, .29, .18, .17, .12, .20, .15, .23, .12, .72, .52),
        ];

        int id = 0;
        for (int li = 0; li < layers.Length; li++)
        {
            var c = layers[li];
            for (int i = 0; i < c.Count; i++)
            {
                double theta = Math.PI * 2 * i / c.Count + c.Phase + .045 * Math.Sin(i * 13.1 + li * 7.2);
                var v = c with { Len = c.Len * (.92 + .12 * Math.Sin(i * 4.7 + li)), Width = c.Width * (.91 + .15 * Math.Cos(i * 3.1)) };
                int pid = id++;
                Surface(s, (u, w) => Petal(v, theta, u, w, pid), 47, 29,
                    (u, w) => v.Tone - .5 * Math.Pow(Math.Max(0, (Math.Abs(w) - .68) / .32), 1.15)
                              + .05 * Math.Sin(u * 13 + pid) + .025 * Math.Sin(theta * 4 + u * 8),
                    1.0 / 46);
            }
        }

        const double turns = Math.PI * 4.3;
        Surface(s, (u, w) =>
        {
            double t = u * turns, r = .018 + .19 * u + w * .045 * (1 - .35 * u), a = t + .15 * Math.Sin(t * 1.4);
            return (RadiusScale * r * Math.Cos(a), RadiusScale * r * Math.Sin(a), CentreHeight + .075 * Math.Sin(t * .75) + .035 * w);
        }, 270, 13, (u, _) => -.015 + .035 * Math.Sin(u * turns * 1.7), 2.0 / 269);

        return s.ToArray();
    }

    static (double, double, double) Petal(Layer c, double theta, double u, double v, int id)
    {
        double s = Math.Sin(Math.PI * u);
        double taper = .22 + .78 * Math.Pow(Math.Sin(Math.PI * (.045 + .79 * u)), .58);
        // Rise cups the petals upward so the bloom reads as a goblet from the side.
        double r = RadiusScale * (1 - CupTaper * c.Rise * u) * (c.Base + c.Len * u)
                   - .045 * c.Len * Math.Pow(Math.Abs(v), 2.6) * u * u * u + .014 * Math.Sin(theta * 3 + u * 5 + id) * s;
        double a = theta + c.Width / Math.Max(r, .12) * taper * .91 * v + c.Twist * v * u + .026 * Math.Sin(u * 7 + id) * v;
        double edgeLift = c.Edge * v * v * Math.Sin(Math.PI * (.08 + .78 * u));
        double z = c.Z + CupHeight * c.Rise * Math.Pow(u, 1.25) + c.Belly * Math.Sin(Math.PI * (.08 + .8 * u))
                   + .052 * (1 - v * v) * s + edgeLift + c.Tip * Math.Pow(u, 3.5) + .038 * Math.Cos(v * Math.PI * 1.4 + u * 4 + id) * s;
        return (r * Math.Cos(a), r * Math.Sin(a), z);
    }

    static void Surface(List<float> s, Func<double, double, (double X, double Y, double Z)> f, int nu, int nv,
        Func<double, double, double> tone, double u0)
    {
        const double e = 1e-3;
        for (int iu = 0; iu < nu; iu++)
        {
            double u = u0 + (1 - u0) * iu / (nu - 1);
            for (int iv = 0; iv < nv; iv++)
            {
                double v = -1 + 2.0 * iv / (nv - 1);
                var p = f(u, v);
                var a0 = f(Math.Max(0, u - e), v); var a1 = f(Math.Min(1, u + e), v);
                var b0 = f(u, Math.Max(-1, v - e)); var b1 = f(u, Math.Min(1, v + e));
                double ax = a1.X - a0.X, ay = a1.Y - a0.Y, az = a1.Z - a0.Z;
                double bx = b1.X - b0.X, by = b1.Y - b0.Y, bz = b1.Z - b0.Z;
                double nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx;
                double n = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (n == 0) n = 1;
                s.Add((float)p.X); s.Add((float)p.Y); s.Add((float)p.Z);
                s.Add((float)(nx / n)); s.Add((float)(ny / n)); s.Add((float)(nz / n));
                s.Add((float)tone(u, v));
            }
        }
    }
}
