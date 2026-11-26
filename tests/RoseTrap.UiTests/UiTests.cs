using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RoseTrap.Controls;
using RoseTrap.Core;
using RoseTrap.Services;
using RoseTrap.ViewModels;
using RoseTrap.Views;

[assembly: AvaloniaTestApplication(typeof(RoseTrap.UiTests.TestApp))]

namespace RoseTrap.UiTests;

public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" })
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public class UiTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("rosetrap-ui-").FullName;
    static readonly string Out = Environment.GetEnvironmentVariable("ROSETRAP_SNAPSHOTS") ?? Path.Combine(Path.GetTempPath(), "rosetrap-snapshots");

    // The debounced settings save can still be writing; a leftover temp dir is harmless.
    public void Dispose() { try { Directory.Delete(_root, recursive: true); } catch (IOException) { } }

    (MainWindow Window, MainViewModel Vm) Open(bool animations = false)
    {
        var vm = new MainViewModel(new AppPaths(_root), new Core.Settings.AppSettings());
        vm.Animations = animations;
        var w = new MainWindow { DataContext = vm, Width = 1080, Height = 690 };
        w.Show();
        Pump();
        return (w, vm);
    }

    static void Pump(int frames = 3)
    {
        for (int i = 0; i < frames; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    /// <summary>Lets real-time transitions and timers run.</summary>
    static void Wait(int ms)
    {
        var end = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < end) { Pump(1); Thread.Sleep(16); }
        Pump();
    }

    static void Snap(Window w, string name)
    {
        Directory.CreateDirectory(Out);
        Pump();
        w.CaptureRenderedFrame()!.Save(Path.Combine(Out, name + ".png"));
    }

    [AvaloniaFact]
    public void EveryPageRenders()
    {
        var (w, _) = Open();
        foreach (var page in new[] { "Welcome", "General", "Gameplay", "Global", "FFlags", "FFlags Settings", "Preset", "Mods", "Settings", "Appearance" })
        {
            w.Navigate(page);
            Wait(700);
            Assert.Equal(page, w.CurrentPage);
            Snap(w, page.Replace(" ", ""));
        }
    }

    [AvaloniaFact]
    public void SidebarIsCompactAndExpandsOnHover()
    {
        var (w, vm) = Open();
        var sidebar = w.FindControl<Border>("Sidebar")!;
        Assert.True(vm.CompactSidebar);
        Assert.Equal(58, sidebar.Bounds.Width, 1);

        w.MouseMove(new Point(30, 200));
        Wait(500);
        Assert.Equal(190, sidebar.Bounds.Width, 1);
        Snap(w, "SidebarHover");

        w.MouseMove(new Point(700, 300));
        Wait(500);
        Assert.Equal(58, sidebar.Bounds.Width, 1);

        vm.CompactSidebar = false; // pinned open
        Wait(400);
        Assert.Equal(190, sidebar.Bounds.Width, 1);
    }

    [AvaloniaFact]
    public void TabSwitchFadesInAndRoseStopsOffWelcome()
    {
        var (w, _) = Open(animations: true);
        var rose = w.GetLogicalDescendants().OfType<LogoRose>().Single();
        Assert.True(rose.IsRunning);

        w.Navigate("General");
        var page = w.LoadedPages.Single(p => p.IsVisible);
        double minOpacity = 1;
        bool captured = false;
        var end = DateTime.UtcNow.AddMilliseconds(600);
        while (DateTime.UtcNow < end)
        {
            Pump(1);
            minOpacity = Math.Min(minOpacity, page.Opacity);
            if (!captured && page.Opacity is > .2 and < .8) { Snap(w, "TransitionMidway"); captured = true; }
            Thread.Sleep(10);
        }
        Assert.True(minOpacity < .3, $"page never faded in (min opacity {minOpacity})");
        Assert.Equal(1, page.Opacity, 3);
        Assert.Null(page.RenderTransform);
        Assert.False(rose.IsRunning); // paused while the welcome page is hidden

        w.Navigate("Welcome");
        Assert.True(rose.IsRunning);
    }

    [AvaloniaFact]
    public void RapidTabSwitchingLeavesOneCleanPage()
    {
        var (w, _) = Open(animations: true);
        string[] order = ["General", "Global", "FFlags", "General", "Mods", "Preset", "Global", "Settings"];
        foreach (var name in order)
        {
            w.Navigate(name);
            Pump(1);
            Thread.Sleep(30); // far quicker than the 200 ms fade
        }
        Wait(400);
        var visible = w.LoadedPages.Where(p => p.IsVisible).ToList();
        Assert.Single(visible);
        Assert.Equal("Settings", w.CurrentPage);
        Assert.All(w.LoadedPages, p => { Assert.Equal(1, p.Opacity, 3); Assert.Null(p.RenderTransform); });
        Snap(w, "AfterRapidSwitching");
    }

    [AvaloniaFact]
    public void AddingFlagsAndPotatoUpdateTheSession()
    {
        var (w, vm) = Open();
        vm.NewFlagName = "FFlagDebugSkyGray";
        vm.AddFlagCommand.Execute(null);
        Assert.Single(vm.Flags);
        Assert.Equal(1, vm.ActiveFlagCount);

        vm.NewFlagName = "not a flag";
        vm.AddFlagCommand.Execute(null);
        Assert.True(vm.NewFlagInvalid);

        vm.Potato = true;
        Assert.True(vm.IsPotatoPreset);
        Assert.Equal(1, vm.Quality);
        Assert.True(vm.ActiveFlagCount > 5);

        vm.Potato = false;
        vm.MsaaIndex = 3; // touching an option makes it Custom
        Assert.True(vm.IsCustomPreset);

        w.Navigate("FFlags");
        Wait(700);
        Snap(w, "FFlagsWithRow");
    }

    [AvaloniaFact]
    public void ModsPagePicksStylesAndPreviewsCrosshair()
    {
        var (w, vm) = Open();
        w.Navigate("Mods");
        Wait(400);
        Assert.Equal(6, vm.CursorChoices.Count);
        Assert.True(vm.CursorChoices.Skip(1).All(c => c.Normal != null && c.Hover != null));

        vm.CursorChoices.First(c => c.Name == "Rose").IsSelected = true;
        Assert.Equal("Rose", vm.Settings.Mods.Cursor);
        Assert.False(vm.CursorChoices[0].IsSelected);

        vm.ShiftLockChoices.First(c => c.Name == "Rose ring").IsSelected = true;
        vm.DeathSoundIndex = 1;
        Assert.Equal("Classic oof", vm.Settings.Mods.DeathSound);

        vm.CrosshairStyleIndex = 1;
        vm.CrosshairGap = 6;
        vm.SetCrosshairColorCommand.Execute("#4DFF6A");
        Assert.Equal("#4DFF6A", vm.CrosshairPreview.Color);
        Assert.Equal(6, vm.CrosshairPreview.Gap);
        vm.CrosshairColor = "not a colour";
        Assert.Equal("#4DFF6A", vm.CrosshairColor);
        Wait(100);
        Snap(w, "Mods");
        var sv = w.LoadedPages.Single(p => p.IsVisible).GetLogicalDescendants().OfType<ScrollViewer>().First();
        sv.Offset = new Vector(0, 560);
        Wait(100);
        Snap(w, "ModsBottom");
    }

    [AvaloniaFact]
    public void GameplayPageEditsRobloxSettings()
    {
        var file = new AppPaths(_root).GlobalBasicSettings;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """
            <roblox version="4"><Item class="UserGameSettings" referent="RBX0"><Properties>
            <float name="MouseSensitivity">0.8</float><float name="MasterVolume">1</float><int name="FramerateCap">60</int>
            </Properties></Item></roblox>
            """);
        var (w, vm) = Open();
        vm.IsRobloxRunning = () => false;
        w.Navigate("Gameplay");
        Wait(300);
        Assert.Equal(.8, vm.Sensitivity, 3);
        Assert.Equal("0.80", vm.SensitivityLabel);

        vm.Sensitivity = 1.5;
        vm.Volume = 4;
        vm.CameraModeIndex = 3;
        Wait(500); // debounce
        var saved = Core.Settings.GameplayStore.Read(file)!;
        Assert.Equal(1.5, saved.MouseSensitivity, 3);
        Assert.Equal(.4, saved.Volume, 3);
        Assert.Equal(Core.Settings.CameraMovement.CameraToggle, saved.CameraMode);
        Assert.Null(vm.Settings.Launcher.PendingGameplay);
        Snap(w, "Gameplay");

        // While Roblox is open its exit would overwrite the file, so the change waits for the next launch.
        vm.IsRobloxRunning = () => true;
        vm.Volume = 7;
        vm.SaveGameplay();
        Assert.Equal(.4, Core.Settings.GameplayStore.Read(file)!.Volume, 3);
        Assert.Equal(.7, vm.Settings.Launcher.PendingGameplay!.Volume, 3);
        Assert.Contains("next launch", vm.GameplayStatus);
    }

    [AvaloniaFact]
    public void LoadingScreenFollowsStagesAndCloses()
    {
        var s = new LoadingSession(joinsGame: true, placeId: 606849621);
        var win = new LoadingWindow { DataContext = s };
        win.Show();
        Pump();
        Assert.Equal(0, s.Progress);
        Assert.Equal("Loading", s.Heading);

        s.Report(new("Downloading Roblox", .4));
        Assert.Equal("Downloading Roblox", s.Detail);
        Wait(800);
        Snap(win, "LoadingScreen");
        // Creeping between events never reaches the next stage.
        for (int i = 0; i < 2000; i++) s.Creep();
        Assert.InRange(s.Progress, .3, .35);

        s.Report(new("Starting Roblox", 1));
        s.Started();
        s.OnJoin(new Core.Roblox.JoinEvent(Core.Roblox.JoinStage.Joining, 606849621));
        s.ShowGame(new Core.Roblox.GameInfo(606849621, 245662005, "Jailbreak", "Badimo", 0, null, null));
        Assert.InRange(s.Progress, .55, .65);
        Assert.Equal("Joining server", s.Detail);
        Assert.Equal("Jailbreak · by Badimo", s.Game);
        Wait(800);
        Assert.Equal($"{Math.Round(s.Progress * 100)}%", win.FindControl<TextBlock>("PercentText")!.Text);
        Snap(win, "LoadingScreenJoining");

        s.OnJoin(new Core.Roblox.JoinEvent(Core.Roblox.JoinStage.Connected));
        s.OnJoin(new Core.Roblox.JoinEvent(Core.Roblox.JoinStage.Loaded));
        Assert.True(s.IsDone);
        Assert.Equal(1, s.Progress, 3);
        Wait(1400);
        Assert.False(win.IsVisible); // shown full, faded, closed
    }

    [AvaloniaFact]
    public void LoadingScreenShowsFailure()
    {
        var s = new LoadingSession(joinsGame: false, placeId: null);
        var win = new LoadingWindow { DataContext = s };
        win.Show();
        s.Fail("Couldn't reach setup.rbxcdn.com");
        Wait(500);
        Assert.True(win.FindControl<Button>("RetryButton")!.IsEffectivelyVisible);
        Snap(win, "LoadingScreenFailed");
        Assert.True(win.IsVisible);
    }

    [AvaloniaFact]
    public void AppLaunchFinishesOnStart()
    {
        var app = new LoadingSession(joinsGame: false, placeId: null);
        app.Started();
        Assert.True(app.IsDone);
        Assert.Equal(1, app.Progress, 3);
    }

    [AvaloniaFact]
    public void StartPanelRendersAndAnimatesHover()
    {
        var w = new StartWindow();
        bool play = false, settings = false;
        w.PlayRequested += () => play = true;
        w.SettingsRequested += () => settings = true;
        w.Show();
        Wait(600);
        Assert.Equal(1, w.FindControl<Border>("Card")!.Opacity, 2);
        Snap(w, "StartPanel");

        var playButton = w.FindControl<Button>("PlayButton")!;
        var bg = playButton.GetVisualDescendants().OfType<Border>().First(b => b.Name == "bg");
        var rest = ((ISolidColorBrush)bg.Background!).Color;
        var centre = playButton.TranslatePoint(new Point(playButton.Bounds.Width / 2, playButton.Bounds.Height / 2), w)!.Value;
        w.MouseMove(centre);
        Pump(2);
        var mid = ((ISolidColorBrush)bg.Background!).Color;
        Wait(500);
        var hovered = ((ISolidColorBrush)bg.Background!).Color;
        Assert.NotEqual(rest, hovered);
        Assert.NotEqual(hovered, mid); // it faded rather than jumping
        Snap(w, "StartPanelHover");

        w.MouseDown(centre, MouseButton.Left);
        w.MouseUp(centre, MouseButton.Left);
        Assert.True(play);
        w.FindControl<Button>("SettingsButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.True(settings);
    }

    [AvaloniaFact]
    public async Task LaunchingClosesEveryWindowThenExits()
    {
        var vm = new MainViewModel(new AppPaths(_root), new Core.Settings.AppSettings());
        bool exited = false;
        var shell = new AppShell(vm, () => exited = true, _ => { });
        shell.ShowStart();
        shell.ShowSettings();
        Pump();
        Assert.Null(shell.OpenStart);
        Assert.NotNull(shell.OpenSettings);

        var started = new TaskCompletionSource<Core.Roblox.LaunchResult>();
        vm.StartRoblox = (_, _) => started.Task;
        await vm.LaunchCommand.ExecuteAsync(null); // the settings window's Launch button
        Pump();
        Assert.Null(shell.OpenSettings); // closed outright, not minimised
        Assert.NotNull(shell.OpenLoading);
        Assert.False(exited);

        started.SetResult(new Core.Roblox.LaunchResult(new Core.Roblox.RobloxInstall("version-x", _root, false), null, [], false)
        { LaunchedAt = DateTime.UtcNow });
        Wait(2200);
        Assert.Null(shell.OpenLoading);
        Assert.True(exited);
    }

    [AvaloniaFact]
    public async Task FailedLaunchKeepsThePanelWithTheReason()
    {
        var vm = new MainViewModel(new AppPaths(_root), new Core.Settings.AppSettings());
        bool exited = false;
        var shell = new AppShell(vm, () => exited = true, _ => { });
        shell.ShowStart();
        vm.StartRoblox = (_, _) => Task.FromException<Core.Roblox.LaunchResult>(new HttpRequestException("No internet"));
        await shell.LaunchAsync(null);
        Pump();
        var loading = shell.OpenLoading!;
        Assert.True(loading.Session!.Failed);
        Assert.Equal("No internet", loading.Session.Detail);
        Assert.False(exited);
        loading.FindControl<Button>("SettingsButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump();
        Assert.NotNull(shell.OpenSettings);
        Assert.Null(shell.OpenLoading);
        shell.OpenSettings!.Close();
        Assert.True(exited);
    }

    [AvaloniaFact]
    public void PreviewKeepsSettingsOpen()
    {
        var (w, vm) = Open();
        LoadingSession? shown = null;
        vm.LoadingStarted += s => shown = s;
        var s = vm.PreviewLoading(hold: true);
        Pump();
        Assert.Same(s, shown);
        Assert.True(s.IsPreview);
        Assert.True(w.IsVisible);
        Assert.Equal(WindowState.Normal, w.WindowState);
        s.Finish();
    }

    [AvaloniaFact]
    public void RoseDrawsCharacters()
    {
        var rose = new LogoRose { Width = 560, Height = 420 };
        var w = new Window { Content = rose, Width = 560, Height = 420, Background = Brushes.Black };
        w.Show();
        rose.Advance(1200);
        Pump();
        using var frame = w.CaptureRenderedFrame()!;
        Directory.CreateDirectory(Out);
        frame.Save(Path.Combine(Out, "Rose.png"));
        Assert.Equal(560, frame.PixelSize.Width);
    }
}
