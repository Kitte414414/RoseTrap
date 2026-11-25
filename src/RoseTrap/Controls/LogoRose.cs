using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace RoseTrap.Controls;

/// <summary>
/// The RoseTrap logo rose, drawn from its own dashes and brought to life: it slowly turns on its
/// vertical axis, a soft light sweeps across it, and it can be spun by dragging.
/// </summary>
public sealed class LogoRose : Control
{
    const double SwayAmplitude = .42; // radians either side while idle
    const double SwayPeriodMs = 9000;

    readonly ImmutableSolidColorBrush[] _base, _lit;
    double _angle, _velocity, _phase;
    TimeSpan? _last;
    bool _attached, _active = true, _running, _dragging;
    Point _dragFrom;
    long _dragTicks, _clockMs;

    public LogoRose()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        int n = LogoRoseData.Colors.Length;
        _base = new ImmutableSolidColorBrush[n];
        _lit = new ImmutableSolidColorBrush[n];
        for (int i = 0; i < n; i++)
        {
            var c = Color.FromUInt32(LogoRoseData.Colors[i]);
            _base[i] = new ImmutableSolidColorBrush(c);
            // Highlight: same hue pushed toward the logo's lightest pink.
            _lit[i] = new ImmutableSolidColorBrush(Color.FromArgb(255,
                (byte)(c.R + (255 - c.R) * .45), (byte)(c.G + (215 - c.G) * .35), (byte)(c.B + (226 - c.B) * .35)));
        }
    }

    public bool IsRunning => _running;

    /// <summary>Pauses the loop while the welcome page is hidden: zero cost off-screen.</summary>
    public bool IsActive
    {
        get => _active;
        set { _active = value; UpdateLoop(); }
    }

    /// <summary>Current turn in radians (for tests).</summary>
    public double Angle => _angle;

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
        if (_last is null || (now - _last.Value).TotalMilliseconds >= 16)
        {
            Advance(_last is null ? 0 : Math.Min(64, (now - _last.Value).TotalMilliseconds));
            _last = now;
            InvalidateVisual();
        }
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(Tick);
    }

    /// <summary>Moves the animation forward by <paramref name="ms"/> milliseconds.</summary>
    public void Advance(double ms)
    {
        _clockMs += (long)ms;
        if (_dragging) return;
        if (Math.Abs(_velocity) > 1e-5)
        {
            // A flick spins freely, then eases back into the idle sway.
            _angle += _velocity * ms;
            _velocity *= Math.Exp(-ms / 900);
            _phase = Math.Asin(Math.Clamp(Wrap(_angle) / SwayAmplitude, -1, 1));
            return;
        }
        _phase += ms / SwayPeriodMs * Math.PI * 2;
        double target = SwayAmplitude * Math.Sin(_phase);
        _angle = Wrap(_angle);
        _angle += (target - _angle) * Math.Min(1, ms / 400);
    }

    static double Wrap(double a) => Math.IEEERemainder(a, Math.PI * 2);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _dragging = true; _velocity = 0;
        _dragFrom = e.GetPosition(this); _dragTicks = Environment.TickCount64;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetPosition(this);
        long now = Environment.TickCount64;
        double d = (p.X - _dragFrom.X) * .012;
        _angle += d;
        _velocity = d / Math.Max(8, now - _dragTicks);
        _dragFrom = p; _dragTicks = now;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        _dragging = false;
        e.Pointer.Capture(null);
    }

    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size;
        if (size.Width < 1 || size.Height < 1) return;
        double scale = Math.Min(size.Width, size.Height) / LogoRoseData.Size;
        double cx = size.Width / 2, cy = size.Height / 2, half = LogoRoseData.Size / 2;

        // Turning on the vertical axis: x is foreshortened by cos(angle), and the far side (which
        // way depends on the turn) is pushed back and dimmed slightly for depth.
        double cos = Math.Cos(_angle), sin = Math.Sin(_angle);
        bool back = cos < 0; // past 90° we see the mirror image, like a real turning card
        double sweep = (_clockMs % 5200) / 5200.0 * 2.6 - .8; // light band position, -0.8..1.8 in view-box widths
        var d = LogoRoseData.Dashes;

        for (int i = 0, k = 0; k < d.Length; i++, k += 5)
        {
            double x = d[k] - half, y = d[k + 1] - half, w = d[k + 2], h = d[k + 3], r = d[k + 4];
            double xc = x + w / 2;
            double depth = xc * sin / half;                           // -1 (near) .. 1 (far)
            double persp = 1 / (1 + .18 * depth);
            double sx = xc * cos * persp, sw = Math.Max(h * .9, w * Math.Abs(cos) * persp);
            double sy = y * persp, sh = h * persp;

            var rect = new Rect(cx + (sx - sw / 2) * scale, cy + sy * scale, sw * scale, sh * scale);

            // Diagonal light band sweeping across the dashes.
            double u = (d[k] + d[k + 1] * .6) / LogoRoseData.Size;
            double lit = Math.Exp(-Math.Pow((u - sweep) / .09, 2));
            var brush = lit > .35 ? _lit[i] : _base[i];
            using (ctx.PushOpacity(Math.Clamp((.85 - .25 * Math.Max(0, depth)) * (back ? .8 : 1), .3, 1)))
                ctx.DrawRectangle(brush, null, new RoundedRect(rect, r * scale * persp));
        }
    }
}
