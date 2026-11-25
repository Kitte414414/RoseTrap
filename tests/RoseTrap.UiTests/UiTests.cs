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

    public void Dispose() => Directory.Delete(_root, recursive: true);

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
        foreach (var page in new[] { "Welcome", "General", "Global", "FFlags", "FFlags Settings", "Preset", "Shaders", "Settings", "Appearance" })
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
        Assert.Equal(178, sidebar.Bounds.Width, 1);
        Snap(w, "SidebarHover");

        w.MouseMove(new Point(700, 300));
        Wait(500);
        Assert.Equal(58, sidebar.Bounds.Width, 1);

        vm.CompactSidebar = false; // pinned open
        Wait(400);
        Assert.Equal(178, sidebar.Bounds.Width, 1);
    }

    [AvaloniaFact]
    public void TabSwitchAnimatesAndRoseStopsOffWelcome()
    {
        var (w, _) = Open(animations: true);
        var rose = w.GetLogicalDescendants().OfType<AsciiRose>().Single();
        Assert.True(rose.IsAttachedToVisualTree());

        w.Navigate("General");
        var panel = w.GetLogicalDescendants().OfType<Border>().First(b => b.Classes.Contains("panel"));
        double minOpacity = 1;
        bool captured = false;
        var end = DateTime.UtcNow.AddMilliseconds(1200);
        while (DateTime.UtcNow < end)
        {
            Pump(1);
            minOpacity = Math.Min(minOpacity, panel.Opacity);
            if (!captured && panel.Opacity is > .2 and < .8) { Snap(w, "TransitionMidway"); captured = true; }
            Thread.Sleep(16);
        }
        Assert.True(minOpacity < .2, $"panel never faded in (min opacity {minOpacity})");
        Assert.True(captured);
        Wait(300);
        Assert.Equal(1, panel.Opacity, 2);
        Assert.False(rose.IsAttachedToVisualTree()); // detached = render loop stopped
        Snap(w, "TransitionDone");
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
    public void RoseDrawsCharacters()
    {
        var rose = new AsciiRose { Width = 560, Height = 420 };
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
