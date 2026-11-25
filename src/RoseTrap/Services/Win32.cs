using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace RoseTrap.Services;

/// <summary>The few Win32 calls the overlay needs.</summary>
[SupportedOSPlatform("windows")]
internal static partial class Win32
{
    const int GWL_EXSTYLE = -20;
    const nint WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000, WS_EX_TOPMOST = 0x8;

    /// <summary>Lets mouse clicks pass straight through a window and keeps it out of Alt+Tab.</summary>
    public static void MakeClickThrough(nint hwnd)
    {
        var ex = GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
        SetWindowLongPtrW(hwnd, GWL_EXSTYLE, ex | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST);
    }

    public static bool IsClickThrough(nint hwnd) => (GetWindowLongPtrW(hwnd, GWL_EXSTYLE) & WS_EX_TRANSPARENT) != 0;

    /// <summary>
    /// Centre of the Roblox game area in screen pixels, or null when Roblox isn't showing.
    /// With <paramref name="foregroundOnly"/>, only while Roblox is the window in front.
    /// </summary>
    public static (int X, int Y)? RobloxCentre(bool foregroundOnly)
    {
        nint hwnd = 0;
        var fg = GetForegroundWindow();
        if (fg != 0 && IsRobloxWindow(fg)) hwnd = fg;
        else if (!foregroundOnly)
        {
            foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta"))
            {
                using (p) if (hwnd == 0 && p.MainWindowHandle != 0) hwnd = p.MainWindowHandle;
            }
        }
        if (hwnd == 0 || IsIconic(hwnd) || !GetClientRect(hwnd, out var rc)) return null;
        var pt = new POINT { X = (rc.Right - rc.Left) / 2, Y = (rc.Bottom - rc.Top) / 2 };
        return ClientToScreen(hwnd, ref pt) ? (pt.X, pt.Y) : null;
    }

    static bool IsRobloxWindow(nint hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName.Equals("RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }
    }

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    [LibraryImport("user32.dll")] private static partial nint GetWindowLongPtrW(nint hwnd, int index);
    [LibraryImport("user32.dll")] private static partial nint SetWindowLongPtrW(nint hwnd, int index, nint value);
    [LibraryImport("user32.dll")] private static partial nint GetForegroundWindow();
    [LibraryImport("user32.dll")] private static partial uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool IsIconic(nint hwnd);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool GetClientRect(nint hwnd, out RECT rect);
    [LibraryImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static partial bool ClientToScreen(nint hwnd, ref POINT point);
}
