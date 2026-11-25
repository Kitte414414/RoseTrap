using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace RoseTrap.Controls;

/// <summary>
/// Fades a page in while it rises a few pixels. Only ever animates one page; starting a new
/// fade snaps the previous one to its final state, so rapid tab clicks can't leave anything half-drawn.
/// </summary>
public sealed class PageFade
{
    const double Duration = 200, Rise = 8;
    static readonly Easing Ease = new SplineEasing(.2, .8, .2, 1);

    Control? _target;
    long _token;

    public void Run(Control page, TopLevel top)
    {
        if (_target is { } prev && prev != page) Finish(prev);
        _target = page;
        long token = ++_token;
        // Started on the first rendered frame: building a page the first time can take a while,
        // and starting earlier would skip the fade and make the page pop in.
        Stopwatch? clock = null;
        var shift = new TranslateTransform();
        page.RenderTransform = shift;
        Apply(page, shift, 0);

        void Frame(TimeSpan _)
        {
            if (token != _token) return; // superseded; whoever replaced us already finished this page
            clock ??= Stopwatch.StartNew();
            double p = Math.Min(1, clock.Elapsed.TotalMilliseconds / Duration);
            Apply(page, shift, Ease.Ease(p));
            if (p < 1) top.RequestAnimationFrame(Frame);
            else Finish(page);
        }
        top.RequestAnimationFrame(Frame);
    }

    public void Finish(Control page)
    {
        if (page == _target) _token++;
        page.Opacity = 1;
        page.RenderTransform = null;
    }

    static void Apply(Control page, TranslateTransform shift, double k)
    {
        page.Opacity = k;
        shift.Y = Rise * (1 - k);
    }
}
