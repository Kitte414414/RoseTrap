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
            desktop.ShutdownRequested += (_, _) => vm.SaveNow();
            vm.RegisterLinks();

            // Opened from a "Play" button on the website: go straight to launching.
            var link = LaunchUri.FindInArgs(desktop.Args ?? []);
            if (link != null) window.Opened += async (_, _) => await vm.LaunchAsync(link);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
