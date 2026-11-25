using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace RoseTrap.Core.Shaders;

/// <summary>
/// Colour-grades the whole screen through the Windows Magnification API. Nothing touches the
/// Roblox process, so there's no anti-cheat risk. Windows drops the effect when RoseTrap exits.
/// Works in windowed and borderless fullscreen (Roblox's default); exclusive fullscreen bypasses it.
/// </summary>
[SupportedOSPlatform("windows")]
public static partial class ScreenColorFilter
{
    static bool _initialized;
    static float[]? _current;

    public static bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(6, 2);

    /// <summary>Applies the matrix (null or neutral clears it). Returns false if Windows refused.</summary>
    public static bool Apply(ColorGrade? grade)
    {
        if (!IsSupported) return false;
        var matrix = (grade ?? ColorGrade.Neutral).ToMagnifierMatrix();
        if (_current is not null && matrix.AsSpan().SequenceEqual(_current)) return true;
        if (!_initialized && !(_initialized = MagInitialize())) return false;
        if (!MagSetFullscreenColorEffect(matrix)) return false;
        _current = matrix;
        return true;
    }

    public static void Clear() => Apply(null);

    /// <summary>The effect Windows currently has set (for tests).</summary>
    public static float[]? Read()
    {
        if (!_initialized) return null;
        var m = new float[25];
        return MagGetFullscreenColorEffect(m) ? m : null;
    }

    public static void Shutdown()
    {
        if (!_initialized) return;
        Clear();
        MagUninitialize();
        _initialized = false;
        _current = null;
    }

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MagInitialize();

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MagUninitialize();

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MagSetFullscreenColorEffect([In] float[] effect);

    [LibraryImport("Magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MagGetFullscreenColorEffect([Out] float[] effect);
}

/// <summary>Tells whether the Roblox player is the window in front.</summary>
[SupportedOSPlatform("windows")]
public static partial class ForegroundWindow
{
    public static bool IsRoblox()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == 0) return false;
        GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return p.ProcessName.Equals("RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; } // exited
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, out uint processId);
}
