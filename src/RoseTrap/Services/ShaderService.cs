using System.Diagnostics;
using System.Runtime.Versioning;
using Avalonia.Threading;
using RoseTrap.Core.Shaders;

namespace RoseTrap.Services;

/// <summary>
/// Keeps the screen colour grade in sync with the game: on while Roblox is in front (or running),
/// off otherwise. Polls cheaply; nothing is injected into Roblox.
/// </summary>
public sealed class ShaderService
{
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    DateTime _testUntil;
    ColorGrade? _test;

    public bool Enabled { get; set; }
    public bool OnlyWhenRobloxFocused { get; set; } = true;
    public ColorGrade Grade { get; set; } = ColorGrade.Neutral;

    /// <summary>False when the grade could not be applied (non-Windows or Windows refused).</summary>
    public bool LastApplyOk { get; private set; } = true;

    public static bool IsSupported => OperatingSystem.IsWindows() && ScreenColorFilter.IsSupported;

    public ShaderService()
    {
        _timer.Tick += (_, _) => Update();
    }

    public void Start()
    {
        if (!IsSupported) return;
        _timer.Start();
        Update();
    }

    /// <summary>Shows a grade on the whole screen for a few seconds, without Roblox.</summary>
    public void Test(ColorGrade grade, TimeSpan duration)
    {
        if (!IsSupported) return;
        _test = grade;
        _testUntil = DateTime.UtcNow + duration;
        Update();
    }

    public void Update()
    {
        if (!OperatingSystem.IsWindows() || !IsSupported) return;
        ColorGrade? want = null;
        if (_test != null && DateTime.UtcNow < _testUntil) want = _test;
        else
        {
            _test = null;
            if (Enabled && !Grade.IsNeutral && ShouldGradeNow()) want = Grade;
        }
        LastApplyOk = Apply(want);
    }

    [SupportedOSPlatform("windows")]
    static bool Apply(ColorGrade? grade) => ScreenColorFilter.Apply(grade);

    bool ShouldGradeNow()
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (OnlyWhenRobloxFocused) return ForegroundWindow.IsRoblox();
        return RobloxRunning();
    }

    public static bool RobloxRunning()
    {
        var procs = Process.GetProcessesByName("RobloxPlayerBeta");
        foreach (var p in procs) p.Dispose();
        return procs.Length > 0;
    }

    public void Shutdown()
    {
        _timer.Stop();
        if (OperatingSystem.IsWindows() && IsSupported) ScreenColorFilter.Shutdown();
    }
}
