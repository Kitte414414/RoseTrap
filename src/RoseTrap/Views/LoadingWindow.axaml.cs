using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using RoseTrap.ViewModels;

namespace RoseTrap.Views;

public partial class LoadingWindow : Window
{
    readonly DispatcherTimer _creep = new() { Interval = TimeSpan.FromMilliseconds(150) };
    LoadingSession? _session;

    public event Action? RetryRequested;
    public event Action? SettingsRequested;

    public LoadingWindow()
    {
        InitializeComponent();
        HideButton.Click += (_, _) => { if (_session is { Failed: true }) Close(); else _session?.Finish(); };
        RetryButton.Click += (_, _) => RetryRequested?.Invoke();
        SettingsButton.Click += (_, _) => SettingsRequested?.Invoke();
        Card.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.Source is not Controls.LogoRose) BeginMoveDrag(e);
        };
        _creep.Tick += (_, _) => _session?.Creep();
        Track.SizeChanged += (_, _) => PlaceBar();
        PercentText.SizeChanged += (_, _) => PlaceBar();
        Opened += (_, _) =>
        {
            Card.Opacity = 1;
            Card.RenderTransform = TransformOperations.Identity;
        };
        Closed += (_, _) =>
        {
            _creep.Stop();
            if (_session != null) _session.PropertyChanged -= OnSessionChanged;
        };
    }

    public LoadingSession? Session => _session;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_session != null) _session.PropertyChanged -= OnSessionChanged;
        _session = DataContext as LoadingSession;
        if (_session is not { } s) return;
        s.PropertyChanged += OnSessionChanged;
        _creep.Start();
        PlaceBar();
        _ = CloseWhenDone(s);
    }

    async Task CloseWhenDone(LoadingSession s)
    {
        await s.Done;
        if (s != _session) return;
        _creep.Stop();
        await Task.Delay(s.Progress >= 1 ? 650 : 0); // let a finished bar be seen before fading
        Card.Opacity = 0;
        await Task.Delay(360);
        Close();
    }

    void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoadingSession.Progress)) PlaceBar();
        else if (e.PropertyName == nameof(LoadingSession.Failed)) _creep.Stop();
    }

    // Composition-only transforms (scale and translate), so the bar glides without relayout every frame.
    void PlaceBar()
    {
        if (_session is not { } s) return;
        double w = Track.Bounds.Width, p = Math.Clamp(s.Progress, 0, 1);
        Fill.RenderTransform = TransformOperations.Parse($"scaleX({p.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)})");
        double label = PercentText.Bounds.Width;
        double x = Math.Clamp(w * p - label / 2, 0, Math.Max(0, w - label - EndText.Bounds.Width - 8));
        PercentText.RenderTransform = TransformOperations.Parse($"translateX({x.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}px)");
    }
}
