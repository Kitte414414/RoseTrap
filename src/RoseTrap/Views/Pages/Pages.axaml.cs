using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using RoseTrap.ViewModels;

namespace RoseTrap.Views.Pages;

public partial class WelcomePage : UserControl { public WelcomePage() => InitializeComponent(); }
public partial class GeneralPage : UserControl { public GeneralPage() => InitializeComponent(); }
public partial class GlobalPage : UserControl { public GlobalPage() => InitializeComponent(); }
public partial class PresetPage : UserControl { public PresetPage() => InitializeComponent(); }
public partial class ShadersPage : UserControl { public ShadersPage() => InitializeComponent(); }

public partial class FlagsPage : UserControl
{
    public FlagsPage()
    {
        InitializeComponent();
        ImportButton.Click += async (_, _) =>
        {
            if (Clipboard is { } cb && DataContext is MainViewModel vm)
                vm.ImportJson(await cb.TryGetTextAsync() ?? "");
        };
        ExportButton.Click += async (_, _) =>
        {
            if (Clipboard is { } cb && DataContext is MainViewModel vm)
            {
                await cb.SetTextAsync(vm.ExportJson());
                (TopLevel.GetTopLevel(this) as MainWindow)?.ShowToast("Copied the flags RoseTrap will write");
            }
        };
    }

    IClipboard? Clipboard => TopLevel.GetTopLevel(this)?.Clipboard;
}

public partial class FlagSettingsPage : UserControl
{
    public FlagSettingsPage()
    {
        InitializeComponent();
        OpenFolderButton.Click += (_, _) => { if (DataContext is MainViewModel vm) Shell.OpenFolder(vm.ClientSettingsFolder); };
    }
}

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
        OpenRootButton.Click += (_, _) => { if (DataContext is MainViewModel vm) Shell.OpenFolder(vm.RoseTrapFolder); };
    }
}

public partial class AppearancePage : UserControl
{
    public AppearancePage()
    {
        InitializeComponent();
        var swatches = Swatches.Children.OfType<RadioButton>().ToList();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm) swatches[vm.AccentIndex].IsChecked = true;
        };
        foreach (var s in swatches)
            s.IsCheckedChanged += (_, _) =>
            {
                if (s.IsChecked == true && DataContext is MainViewModel vm) vm.AccentIndex = int.Parse((string)s.Tag!);
            };
    }
}

internal static class Shell
{
    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true })?.Dispose();
    }
}
