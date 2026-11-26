using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RoseTrap.Core.Roblox;
using RoseTrap.Services;
using RoseTrap.ViewModels;

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
            var shell = new AppShell(desktop, vm);
            desktop.ShutdownRequested += (_, _) => { vm.SaveNow(); vm.Crosshair.Shutdown(); };
            vm.RegisterLinks();

            var args = desktop.Args ?? [];
            int pi = Array.IndexOf(args, "--page");
            if (LaunchUri.FindInArgs(args) is { } link)
            {
                // Opened from a "Play" button on the website: straight to the loading panel.
                _ = shell.LaunchAsync(link);
            }
            else if (pi >= 0 && pi + 1 < args.Length)
            {
                // "--page Global [--expanded] [--test-crosshair] [--test-loading placeId]" opens a settings
                // page directly (used by the screenshot CI job).
                var page = args[pi + 1];
                int li = Array.IndexOf(args, "--test-loading");
                long? loadingPlace = li >= 0 && li + 1 < args.Length && long.TryParse(args[li + 1], out var lp) ? lp : null;
                // Show() has already raised Opened, so the hooks run right after it.
                var window = shell.ShowSettings(page);
                if (args.Contains("--expanded")) window.ShowSidebarExpanded();
                if (args.Contains("--test-crosshair")) vm.Crosshair.Test(TimeSpan.FromSeconds(30));
                if (li >= 0) vm.PreviewLoading(loadingPlace, hold: true);
            }
            else shell.ShowStart();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
