using System.Runtime;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using RoseTrap.ViewModels;
using RoseTrap.Views;

namespace RoseTrap.Services;

/// <summary>
/// Decides which window is on screen: the start panel, the settings window, or the loading panel.
/// Launching closes the other windows outright (not minimise), so nothing is left open behind the game
/// and their memory is freed. RoseTrap then exits, or waits windowless while the crosshair or
/// multi-instance lock needs it, and exits when Roblox closes.
/// </summary>
public sealed partial class AppShell
{
    readonly Action _shutdown;
    readonly Action<Window> _setMain;
    readonly MainViewModel _vm;
    readonly DispatcherTimer _robloxWatch = new() { Interval = TimeSpan.FromSeconds(2) };
    StartWindow? _start;
    MainWindow? _settings;
    LoadingWindow? _loading;
    bool _background;

    internal Window? OpenStart => _start;
    internal Window? OpenSettings => _settings;
    internal LoadingWindow? OpenLoading => _loading;
    internal bool Background => _background;

    public AppShell(IClassicDesktopStyleApplicationLifetime desktop, MainViewModel vm)
        : this(vm, () => desktop.Shutdown(), w => desktop.MainWindow = w)
    {
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    internal AppShell(MainViewModel vm, Action shutdown, Action<Window> setMain)
    {
        _shutdown = shutdown;
        _setMain = setMain;
        _vm = vm;
        vm.LaunchRequested += link => _ = LaunchAsync(link);
        _robloxWatch.Tick += (_, _) => { if (!RobloxProcess.IsRunning()) Exit(); };
        MainWindow.ApplyAccent(vm.AccentIndex);
    }

    public StartWindow ShowStart()
    {
        if (_start != null) { _start.Activate(); return _start; }
        var w = _start = new StartWindow();
        w.PlayRequested += () => _ = LaunchAsync(null);
        w.SettingsRequested += () => ShowSettings();
        w.Closed += (_, _) => { _start = null; ExitIfIdle(); };
        _setMain(w);
        w.Show();
        return w;
    }

    public MainWindow ShowSettings(string? page = null)
    {
        if (_settings is null)
        {
            var w = _settings = new MainWindow { DataContext = _vm };
            w.Closed += (_, _) => { _settings = null; ExitIfIdle(); };
            _setMain(w);
            w.Show();
        }
        else _settings.Activate();
        if (page != null) _settings.Navigate(page, instant: true);
        // Open the settings first, so closing the panel never leaves the app without a window.
        _start?.Close();
        _loading?.Close();
        return _settings;
    }

    public async Task LaunchAsync(string? link)
    {
        if (_vm.IsLaunching) return;
        var session = _vm.NewLoadingSession(link);
        if (session != null)
        {
            _loading?.Close();
            var w = _loading = new LoadingWindow { DataContext = session };
            w.RetryRequested += () => _ = LaunchAsync(link);
            w.SettingsRequested += () => ShowSettings();
            w.Closed += (_, _) => { if (_loading == w) _loading = null; ExitIfIdle(); };
            _setMain(w);
            w.Show();
        }
        _start?.Close();
        _settings?.Close();

        var error = await _vm.RunLaunchAsync(link, session);
        if (error != null)
        {
            // Stay on screen with the reason: the loading panel offers Try again / Settings.
            if (session is null || _loading is null) ShowSettings().ShowToast(error);
            else session.Fail(error);
            return;
        }

        if (_vm.MustStayRunning)
        {
            _background = true;
            _robloxWatch.Start();
            TrimMemory();
        }
        if (session != null) await session.Done;
        ExitIfIdle();
    }

    void ExitIfIdle()
    {
        if (_start is null && _settings is null && _loading is null && !_vm.IsLaunching && !_background) Exit();
    }

    public void Exit()
    {
        _robloxWatch.Stop();
        _vm.SaveNow();
        _vm.Crosshair.Shutdown();
        _shutdown();
    }

    /// <summary>
    /// Waiting in the background: hand freed window memory back to the OS so RoseTrap stays small
    /// for the rest of the session.
    /// </summary>
    static void TrimMemory()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        if (!OperatingSystem.IsWindows()) return;
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        EmptyWorkingSet(self.Handle);
    }

    [LibraryImport("psapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyWorkingSet(nint process);
}
