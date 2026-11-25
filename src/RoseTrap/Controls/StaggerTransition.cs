using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.LogicalTree;
using Avalonia.Media.Transformation;

namespace RoseTrap.Controls;

/// <summary>
/// Tab switch: the old page fades and drifts away, the new page fades in while every element
/// marked <c>Classes="stagger"</c> rises into place one after another. Direction follows the tab order.
/// </summary>
/// <remarks>
/// Driven by a per-frame tween instead of keyframe <see cref="Animation"/>s: Avalonia 11's keyframe
/// animations have no animator for RenderTransform with transform operations.
/// </remarks>
public sealed class StaggerTransition : IPageTransition
{
    public static bool Enabled { get; set; } = true;

    static readonly Easing Out = new SplineEasing(.16, 1, .3, 1);
    static readonly Easing In = new SplineEasing(.4, 0, 1, 1);

    sealed record Track(Visual Target, double Delay, double Duration, Easing Easing,
        double O0, double O1, double Y0, double Y1, Snapshot Saved);

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken ct)
    {
        if (to != null) to.IsVisible = true;
        if (!Enabled || TopLevel.GetTopLevel(to ?? from) is not { } top)
        {
            if (from != null) from.IsVisible = false;
            return;
        }

        int dir = forward ? 1 : -1;
        var tracks = new List<Track>();
        if (from != null) tracks.Add(Make(from, 0, 170, In, 1, 0, 0, -dir * 8));
        if (to != null)
        {
            tracks.Add(Make(to, 80, 240, Out, 0, 1, 0, 0));
            var page = (to as ContentPresenter)?.Content as ILogical ?? to as ILogical;
            int i = 0;
            foreach (var el in page?.GetLogicalDescendants().OfType<Control>().Where(c => c.Classes.Contains("stagger")) ?? [])
                tracks.Add(Make(el, 90 + i++ * 45, 460, Out, 0, 1, dir * 18, 0));
        }

        foreach (var t in tracks) Apply(t, 0);
        var clock = Stopwatch.StartNew();
        var done = new TaskCompletionSource();

        void Frame(TimeSpan _)
        {
            double ms = clock.Elapsed.TotalMilliseconds;
            bool running = false;
            foreach (var t in tracks)
            {
                double p = Math.Clamp((ms - t.Delay) / t.Duration, 0, 1);
                Apply(t, t.Easing.Ease(p));
                running |= p < 1;
            }
            if (running && !ct.IsCancellationRequested) top.RequestAnimationFrame(Frame);
            else done.TrySetResult();
        }

        top.RequestAnimationFrame(Frame);
        await done.Task;

        foreach (var t in tracks) t.Saved.Restore();
        if (from != null && !ct.IsCancellationRequested) from.IsVisible = false;
    }

    static Track Make(Visual v, double delay, double duration, Easing e, double o0, double o1, double y0, double y1)
    {
        var saved = new Snapshot(v);
        // Style transitions (e.g. hover lifts) would smooth every frame and lag behind the tween.
        v.Transitions = null;
        return new Track(v, delay, duration, e, o0, o1, y0, y1, saved);
    }

    static void Apply(Track t, double k)
    {
        t.Target.Opacity = t.O0 + (t.O1 - t.O0) * k;
        double y = t.Y0 + (t.Y1 - t.Y0) * k;
        t.Target.RenderTransform = Math.Abs(y) < .01 ? null : TransformOperations.Parse($"translateY({y:0.##}px)");
    }

    /// <summary>Remembers local values so styles take over again once the tween ends.</summary>
    sealed class Snapshot
    {
        readonly Visual _v;
        readonly (AvaloniaProperty Prop, bool Local, object? Value)[] _values;

        public Snapshot(Visual v)
        {
            _v = v;
            AvaloniaProperty[] props = [Visual.OpacityProperty, Visual.RenderTransformProperty, Animatable.TransitionsProperty];
            _values = props.Select(p => (p, v.IsSet(p), v.GetValue(p))).ToArray();
        }

        public void Restore()
        {
            foreach (var (prop, local, value) in _values)
                if (local) _v.SetValue(prop, value); else _v.ClearValue(prop);
        }
    }
}
