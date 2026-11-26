using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RoseTrap.Core.Roblox;
using RoseTrap.ViewModels;
using RoseTrap.Views;

namespace RoseTrap;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Theme.Icons.Register(Resources);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            var window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;
            desktop.ShutdownRequested += (_, _) => { vm.SaveNow(); vm.Shader.Shutdown(); vm.Crosshair.Shutdown(); };
            vm.RegisterLinks();

            // Opened from a "Play" button on the website: go straight to launching.
            var args = desktop.Args ?? [];
            var link = LaunchUri.FindInArgs(args);
            if (link != null) window.Opened += async (_, _) => await vm.LaunchAsync(link);

            // "--page Global [--hover]" opens a page directly (used by the screenshot CI job).
            int pi = Array.IndexOf(args, "--page");
            if (pi >= 0 && pi + 1 < args.Length)
            {
                var page = args[pi + 1];
                bool hover = args.Contains("--expanded");
                bool crosshair = args.Contains("--test-crosshair");
                // "--test-loading [placeId]" shows the loading screen held at its last stage.
                int li = Array.IndexOf(args, "--test-loading");
                long? loadingPlace = li >= 0 && li + 1 < args.Length && long.TryParse(args[li + 1], out var lp) ? lp : null;
                window.Opened += (_, _) =>
                {
                    window.Navigate(page, instant: true);
                    if (hover) window.ShowSidebarExpanded();
                    if (crosshair) vm.Crosshair.Test(TimeSpan.FromSeconds(30));
                    if (li >= 0) vm.PreviewLoading(loadingPlace, hold: true);
                };
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
