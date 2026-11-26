using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RoseTrap.Core.Mods;
using RoseTrap.ViewModels;

namespace RoseTrap.Views.Pages;

public partial class WelcomePage : UserControl
{
    public WelcomePage() => InitializeComponent();
    public void SetActive(bool active) => Rose.IsActive = active;
}
public partial class GeneralPage : UserControl { public GeneralPage() => InitializeComponent(); }
public partial class GlobalPage : UserControl { public GlobalPage() => InitializeComponent(); }
public partial class PresetPage : UserControl { public PresetPage() => InitializeComponent(); }
public partial class ShadersPage : UserControl { public ShadersPage() => InitializeComponent(); }

public partial class GameplayPage : UserControl
{
    public GameplayPage()
    {
        InitializeComponent();
        ResetButton.Click += (_, _) => (DataContext as MainViewModel)?.ResetGameplay();
        PreviewLoadingButton.Click += (_, _) => (DataContext as MainViewModel)?.PreviewLoading();
        // Roblox may have changed its settings while RoseTrap was in the background.
        AttachedToVisualTree += (_, _) => (DataContext as MainViewModel)?.LoadGameplay();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible) (DataContext as MainViewModel)?.LoadGameplay();
        };
    }
}

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

public partial class ModsPage : UserControl
{
    public ModsPage()
    {
        InitializeComponent();
        ChooseSoundButton.Click += async (_, _) =>
        {
            if (await PickFile("Choose a death sound", "Sound", ModSet.SoundExtensions) is { } path && DataContext is MainViewModel vm)
                vm.SetDeathSoundFile(path);
        };
        ChooseFontButton.Click += async (_, _) =>
        {
            if (await PickFile("Choose a font", "Font", ModSet.FontExtensions) is { } path && DataContext is MainViewModel vm)
                vm.SetFontFile(path);
        };
        OpenModFolderButton.Click += (_, _) =>
        {
            if (DataContext is not MainViewModel vm) return;
            Shell.OpenFolder(vm.ModFolder);
            vm.RefreshModFolder();
        };
        // The folder may have changed while RoseTrap was in the background.
        AttachedToVisualTree += (_, _) => (DataContext as MainViewModel)?.RefreshModFolder();
    }

    async Task<string?> PickFile(string title, string kind, string[] extensions)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(kind) { Patterns = extensions.Select(e => "*" + e).ToList() }],
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
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
