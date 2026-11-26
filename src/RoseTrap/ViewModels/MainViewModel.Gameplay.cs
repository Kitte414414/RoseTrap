using Avalonia.Threading;
using RoseTrap.Core.Roblox;
using RoseTrap.Core.Settings;
using RoseTrap.Services;

namespace RoseTrap.ViewModels;

// Gameplay settings (Roblox's own Esc-menu options) and the custom loading screen.
public sealed partial class MainViewModel
{
    static readonly CameraMovement[] CameraModes = [CameraMovement.Default, CameraMovement.Classic, CameraMovement.Follow, CameraMovement.CameraToggle];

    GameplaySettings _gp = new();
    DispatcherTimer? _gpSave;
    bool _gpFileMissing;

    /// <summary>Raised by the loading-screen preview.</summary>
    public event Action<LoadingSession>? LoadingStarted;

    /// <summary>Overridable so tests don't depend on a real Roblox process.</summary>
    public Func<bool> IsRobloxRunning { get; set; } = RobloxProcess.IsRunning;

    /// <summary>Re-reads Roblox's settings file; called whenever the Gameplay page is shown.</summary>
    public void LoadGameplay()
    {
        GameplaySettings? fromFile = null;
        try { fromFile = GameplayStore.Read(_paths.GlobalBasicSettings); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Xml.XmlException) { }
        _gpFileMissing = fromFile is null;
        // Unsaved choices win: Roblox rewrites its file on exit, so a pending edit is newer than the file.
        _gp = _s.Launcher.PendingGameplay ?? fromFile ?? new GameplaySettings();
        foreach (var p in new[] { nameof(Sensitivity), nameof(SensitivityLabel), nameof(InvertCamera), nameof(CameraModeIndex),
                     nameof(MovementIndex), nameof(Volume), nameof(VolumeLabel), nameof(RobloxFullscreen), nameof(PerformanceStats),
                     nameof(ReducedMotion), nameof(UiTransparency), nameof(UiTransparencyLabel) })
            OnPropertyChanged(p);
        OnPropertyChanged(nameof(GameplayStatus));
    }

    void SetGameplay(GameplaySettings g, string name)
    {
        if (g == _gp) return;
        _gp = g;
        OnPropertyChanged(name);
        if (_gpSave is null)
        {
            _gpSave = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _gpSave.Tick += (_, _) => { _gpSave.Stop(); SaveGameplay(); };
        }
        _gpSave.Stop();
        _gpSave.Start();
    }

    /// <summary>Writes straight into Roblox's file, or holds the change until the next launch.</summary>
    public void SaveGameplay()
    {
        _gpSave?.Stop();
        bool written = false;
        if (!IsRobloxRunning())
        {
            try { written = GameplayStore.Write(_paths.GlobalBasicSettings, _gp); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                Toast?.Invoke($"Couldn't write Roblox's settings: {e.Message}");
            }
        }
        _gpFileMissing = !File.Exists(_paths.GlobalBasicSettings);
        _s.Launcher.PendingGameplay = written ? null : _gp;
        Changed(nameof(GameplayStatus));
    }

    public string GameplayStatus =>
        _s.Launcher.PendingGameplay is null ? "Saved in Roblox's settings. Changes made in Roblox's menu show up here too."
        : _gpFileMissing ? "Roblox hasn't made its settings file yet, so these apply after its first start."
        : "Roblox is open and would overwrite this on exit, so it's applied on your next launch.";

    public double Sensitivity
    {
        get => _gp.MouseSensitivity;
        set { SetGameplay(_gp with { MouseSensitivity = Math.Round(Math.Clamp(value, .1, 4), 2) }, nameof(Sensitivity)); OnPropertyChanged(nameof(SensitivityLabel)); }
    }
    public string SensitivityLabel => _gp.MouseSensitivity.ToString("0.00");
    public bool InvertCamera { get => _gp.InvertCamera; set => SetGameplay(_gp with { InvertCamera = value }, nameof(InvertCamera)); }
    public int CameraModeIndex
    {
        get => Math.Max(0, Array.IndexOf(CameraModes, _gp.CameraMode));
        set => SetGameplay(_gp with { CameraMode = CameraModes[Math.Clamp(value, 0, CameraModes.Length - 1)] }, nameof(CameraModeIndex));
    }
    public int MovementIndex { get => (int)_gp.Movement; set => SetGameplay(_gp with { Movement = (MovementMode)Math.Clamp(value, 0, 2) }, nameof(MovementIndex)); }
    /// <summary>0–10, the same steps as Roblox's volume slider.</summary>
    public double Volume
    {
        get => Math.Round(_gp.Volume * 10);
        set { SetGameplay(_gp with { Volume = Math.Clamp(Math.Round(value) / 10, 0, 1) }, nameof(Volume)); OnPropertyChanged(nameof(VolumeLabel)); }
    }
    public string VolumeLabel => _gp.Volume <= 0 ? "Muted" : $"{Math.Round(_gp.Volume * 100)}%";
    public bool RobloxFullscreen { get => _gp.Fullscreen; set => SetGameplay(_gp with { Fullscreen = value }, nameof(RobloxFullscreen)); }
    public bool PerformanceStats { get => _gp.PerformanceStats; set => SetGameplay(_gp with { PerformanceStats = value }, nameof(PerformanceStats)); }
    public bool ReducedMotion { get => _gp.ReducedMotion; set => SetGameplay(_gp with { ReducedMotion = value }, nameof(ReducedMotion)); }
    /// <summary>0–100%.</summary>
    public double UiTransparency
    {
        get => Math.Round(_gp.UiTransparency * 100);
        set { SetGameplay(_gp with { UiTransparency = Math.Clamp(Math.Round(value) / 100, 0, 1) }, nameof(UiTransparency)); OnPropertyChanged(nameof(UiTransparencyLabel)); }
    }
    public string UiTransparencyLabel => $"{Math.Round(_gp.UiTransparency * 100)}%";

    public void ResetGameplay()
    {
        _gp = new GameplaySettings();
        SaveGameplay();
        LoadGameplay();
        Toast?.Invoke("Gameplay settings reset to Roblox's defaults");
    }

    public bool CustomLoadingScreen { get => _s.Launcher.CustomLoadingScreen; set { _s.Launcher.CustomLoadingScreen = value; Changed(); } }

    // ---------- loading screen ----------

    /// <summary>A loading panel for this launch, or null when the custom loading screen is turned off.</summary>
    public LoadingSession? NewLoadingSession(string? link) =>
        _s.Launcher.CustomLoadingScreen ? new LoadingSession(joinsGame: link is not null, link is null ? null : LaunchUri.PlaceId(link)) : null;

    async Task ShowGameAsync(LoadingSession session, long placeId)
    {
        if (await GameInfoClient.GetAsync(_http, placeId, images: false) is { } g) session.ShowGame(g);
    }

    /// <summary>Follows Roblox's log until the game has loaded, Roblox closes, or two minutes pass.</summary>
    async Task FollowLoadingAsync(LoadingSession session, LaunchResult r)
    {
        session.Started();
        if (session.IsDone) { await Task.Delay(700); return; }

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var watch = Task.Run(() => RobloxLog.WatchAsync(_paths.RobloxLogs, r.LaunchedAt,
            e => Dispatcher.UIThread.Post(() =>
            {
                session.OnJoin(e);
                if (e.PlaceId is { } id && session.Game is null) _ = ShowGameAsync(session, id);
            }), cts.Token));

        while (!session.IsDone && !cts.IsCancellationRequested)
        {
            await Task.WhenAny(session.Done, Task.Delay(1000));
            if (r.ProcessId is { } pid && !ProcessAlive(pid)) session.Finish();
        }
        session.Finish();
        cts.Cancel();
        try { await watch; } catch (OperationCanceledException) { }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Plays the loading panel with made-up stages so it can be seen without starting Roblox.
    /// <paramref name="placeId"/> fetches a real game's name; <paramref name="hold"/> stops before the end.
    /// </summary>
    public LoadingSession PreviewLoading(long? placeId = null, bool hold = false)
    {
        var session = new LoadingSession(joinsGame: true, placeId) { IsPreview = true };
        LoadingStarted?.Invoke(session);
        if (placeId is { } id) _ = ShowGameAsync(session, id);
        Action[] script =
        [
            () => session.Report(new("Downloading Roblox", .6)),
            () => session.Report(new("Applying settings", 1)),
            () => session.Report(new("Starting Roblox", 1)),
            () => session.Started(),
            () => session.OnJoin(new JoinEvent(JoinStage.Joining, placeId)),
            () => session.OnJoin(new JoinEvent(JoinStage.Connected)),
            () => session.OnJoin(new JoinEvent(JoinStage.Loaded)),
        ];
        int i = 0, end = hold ? 5 : script.Length;
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
        t.Tick += (_, _) => { if (session.IsDone || i >= end) { t.Stop(); return; } script[i++](); };
        t.Start();
        return session;
    }

    static bool ProcessAlive(int pid)
    {
        try { using var p = System.Diagnostics.Process.GetProcessById(pid); return !p.HasExited; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return false; }
    }
}
