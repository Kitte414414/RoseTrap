using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using RoseTrap.Controls;
using RoseTrap.Core.Settings;

namespace RoseTrap.Services;

/// <summary>
/// Draws the crosshair on top of the game: a small transparent, click-through, always-on-top window
/// kept centred on Roblox. Nothing is injected into Roblox.
/// </summary>
public sealed class CrosshairService
{
    const double OverlaySize = 160; // DIPs; big enough for the largest crosshair

    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    Window? _overlay;
    CrosshairView? _view;
    DateTime _testUntil;

    public CrosshairSettings Settings { get; private set; } = new();

    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>True while the overlay window is showing (for tests and the status line).</summary>
    public bool IsShowing => _overlay?.IsVisible == true;

    public CrosshairService()
    {
        _timer.Tick += (_, _) => Update();
    }

    public void Configure(CrosshairSettings settings)
    {
        Settings = settings.Clone();
        if (_view != null) _view.Settings = Settings;
        if (!IsSupported) return;
        if (Settings.Enabled || DateTime.UtcNow < _testUntil) _timer.Start();
        Update();
    }

    /// <summary>Shows the crosshair in the middle of the screen for a few seconds, without Roblox.</summary>
    public void Test(TimeSpan duration)
    {
        if (!IsSupported) return;
        _testUntil = DateTime.UtcNow + duration;
        _timer.Start();
        Update();
    }

    void Update()
    {
        if (!OperatingSystem.IsWindows()) return;
        bool testing = DateTime.UtcNow < _testUntil;
        (int X, int Y)? centre = testing ? ScreenCentre() : null;
        if (!testing && Settings.Enabled) centre = Win32.RobloxCentre(Settings.OnlyWhenRobloxFocused);

        if (centre is not { } c)
        {
            _overlay?.Hide();
            if (!testing && !Settings.Enabled) _timer.Stop();
            return;
        }

        var w = EnsureOverlay();
        double scale = w.Screens.ScreenFromPoint(new PixelPoint(c.X, c.Y))?.Scaling ?? w.RenderScaling;
        int px = (int)Math.Round(OverlaySize * scale);
        var pos = new PixelPoint(c.X - px / 2, c.Y - px / 2);
        if (w.Position != pos) w.Position = pos;
        if (!w.IsVisible) w.Show();
    }

    (int, int)? ScreenCentre()
    {
        var screen = EnsureOverlay().Screens.Primary;
        if (screen is null) return null;
        var b = screen.Bounds;
        return (b.X + b.Width / 2, b.Y + b.Height / 2);
    }

    Window EnsureOverlay()
    {
        if (_overlay != null) return _overlay;
        _view = new CrosshairView { Settings = Settings };
        _overlay = new Window
        {
            Width = OverlaySize,
            Height = OverlaySize,
            CanResize = false,
            SystemDecorations = SystemDecorations.None,
            Background = Brushes.Transparent,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false, // never steal focus from the game
            Focusable = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Title = "RoseTrap crosshair",
            Content = _view,
        };
        _overlay.Opened += (_, _) =>
        {
            if (OperatingSystem.IsWindows() && _overlay.TryGetPlatformHandle()?.Handle is { } h and not 0)
                Win32.MakeClickThrough(h);
        };
        return _overlay;
    }

    public void Shutdown()
    {
        _timer.Stop();
        _overlay?.Close();
        _overlay = null;
    }
}
