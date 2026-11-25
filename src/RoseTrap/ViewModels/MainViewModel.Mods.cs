using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoseTrap.Core.Imaging;
using RoseTrap.Core.Mods;
using RoseTrap.Core.Settings;
using RoseTrap.Services;

namespace RoseTrap.ViewModels;

/// <summary>One selectable cursor or shift-lock style, with its preview images.</summary>
public sealed partial class StyleChoice(string name, string description, Bitmap? normal, Bitmap? hover, Func<bool> isSelected, Action select)
    : ObservableObject
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public Bitmap? Normal { get; } = normal;
    public Bitmap? Hover { get; } = hover;
    public bool HasPreview => Normal != null;
    public bool NoPreview => Normal == null;

    public bool IsSelected
    {
        get => isSelected();
        set { if (value) select(); }
    }

    public void Refresh() => OnPropertyChanged(nameof(IsSelected));
}

/// <summary>Mods tab: crosshair overlay, cursors, shift-lock icon, death sound, font, mod folder.</summary>
public sealed partial class MainViewModel
{
    public CrosshairService Crosshair { get; } = new();
    ModSettings Md => _s.Mods;
    CrosshairSettings Ch => _s.Mods.Crosshair;

    public static readonly string[] CrosshairStyles = ["Cross", "Cross + dot", "Dot", "Circle", "T"];
    public static readonly string[] CrosshairColors = ["#F3ADBD", "#FFFFFF", "#4DFF6A", "#29E0FF", "#FFE14D", "#FF4D5E"];
    public static readonly string[] DeathSounds = ModSet.DeathSounds;

    void InitMods()
    {
        Crosshair.Configure(Ch);
    }

    // ---------- crosshair ----------

    /// <summary>A fresh copy on every change, so bound previews redraw.</summary>
    public CrosshairSettings CrosshairPreview => Ch.Clone();

    void CrosshairChanged(string? name = null)
    {
        if (name != null) OnPropertyChanged(name);
        OnPropertyChanged(nameof(CrosshairPreview));
        _saveTimer.Change(400, Timeout.Infinite);
        Crosshair.Configure(Ch);
    }

    public bool CrosshairEnabled
    {
        get => Ch.Enabled;
        set
        {
            Ch.Enabled = value;
            CrosshairChanged(nameof(CrosshairEnabled));
            if (value && !CrosshairService.IsSupported) Toast?.Invoke("The crosshair overlay needs Windows.");
        }
    }

    public int CrosshairStyleIndex { get => (int)Ch.Style; set { Ch.Style = (CrosshairStyle)value; CrosshairChanged(nameof(CrosshairStyleIndex)); } }

    public double CrosshairSize { get => Ch.Size; set { Ch.Size = Math.Round(value); CrosshairChanged(nameof(CrosshairSize)); OnPropertyChanged(nameof(CrosshairSizeLabel)); } }
    public string CrosshairSizeLabel => $"{Ch.Size:0} px";
    public double CrosshairThickness { get => Ch.Thickness; set { Ch.Thickness = Math.Round(value); CrosshairChanged(nameof(CrosshairThickness)); OnPropertyChanged(nameof(CrosshairThicknessLabel)); } }
    public string CrosshairThicknessLabel => $"{Ch.Thickness:0} px";
    public double CrosshairGap { get => Ch.Gap; set { Ch.Gap = Math.Round(value); CrosshairChanged(nameof(CrosshairGap)); OnPropertyChanged(nameof(CrosshairGapLabel)); } }
    public string CrosshairGapLabel => $"{Ch.Gap:0} px";
    public double CrosshairOpacity { get => Ch.Opacity; set { Ch.Opacity = Math.Round(value, 2); CrosshairChanged(nameof(CrosshairOpacity)); OnPropertyChanged(nameof(CrosshairOpacityLabel)); } }
    public string CrosshairOpacityLabel => (Ch.Opacity * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    public bool CrosshairOutline { get => Ch.Outline; set { Ch.Outline = value; CrosshairChanged(nameof(CrosshairOutline)); } }
    public bool CrosshairOnlyFocused { get => Ch.OnlyWhenRobloxFocused; set { Ch.OnlyWhenRobloxFocused = value; CrosshairChanged(nameof(CrosshairOnlyFocused)); } }

    public string CrosshairColor
    {
        get => Ch.Color;
        set
        {
            var v = value.Trim();
            if (!v.StartsWith('#')) v = "#" + v;
            if (!Avalonia.Media.Color.TryParse(v, out _)) return;
            Ch.Color = v.ToUpperInvariant();
            CrosshairChanged(nameof(CrosshairColor));
        }
    }

    [RelayCommand]
    void SetCrosshairColor(string hex) => CrosshairColor = hex;

    [RelayCommand]
    void TestCrosshair()
    {
        if (!CrosshairService.IsSupported) { Toast?.Invoke("The crosshair overlay needs Windows."); return; }
        Crosshair.Test(TimeSpan.FromSeconds(5));
        Toast?.Invoke("Crosshair in the middle of your screen for 5 seconds");
    }

    Bitmap? _modScene;
    public Bitmap ModScene => _modScene ??= new Bitmap(new MemoryStream(Png.Encode(PreviewScene.Build(480, 220, null), 480, 220)));

    // ---------- cursors ----------

    List<StyleChoice>? _cursorChoices, _shiftChoices;

    public IReadOnlyList<StyleChoice> CursorChoices => _cursorChoices ??= CursorArt.Cursors.Select(c =>
    {
        bool def = c.Name == CursorArt.Default;
        var normal = def ? OriginalImage(ModSet.PointerPath) : Load(CursorArt.Pointer(c.Name));
        var hover = def ? OriginalImage(ModSet.HoverPath) : Load(CursorArt.Hover(c.Name));
        return new StyleChoice(c.Name, c.Description, normal, hover, () => Md.Cursor == c.Name, () => SetCursor(c.Name));
    }).ToList();

    public IReadOnlyList<StyleChoice> ShiftLockChoices => _shiftChoices ??= CursorArt.ShiftLocks.Select(c =>
    {
        var img = c.Name == CursorArt.Default ? OriginalImage(ModSet.ShiftLockPath) : Load(CursorArt.ShiftLock(c.Name));
        return new StyleChoice(c.Name, c.Description, img, null, () => Md.ShiftLock == c.Name, () => SetShiftLock(c.Name));
    }).ToList();

    void SetCursor(string name)
    {
        Md.Cursor = name;
        foreach (var c in CursorChoices) c.Refresh();
        ModsChanged();
    }

    void SetShiftLock(string name)
    {
        Md.ShiftLock = name;
        foreach (var c in ShiftLockChoices) c.Refresh();
        ModsChanged();
    }

    static Bitmap Load(byte[] png) => new(new MemoryStream(png));

    /// <summary>Roblox's own image from the installed copy, for the "Default" preview.</summary>
    Bitmap? OriginalImage(string rel)
    {
        if (_installed is null) return null;
        try { return ModApplier.ReadOriginal(_installed.Directory, rel) is { } png ? Load(png) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    // ---------- sound & font ----------

    public int DeathSoundIndex
    {
        get => Math.Max(0, Array.IndexOf(DeathSounds, Md.DeathSound));
        set
        {
            if (value < 0 || value >= DeathSounds.Length) return;
            Md.DeathSound = DeathSounds[value];
            OnPropertyChanged(nameof(DeathSoundIndex));
            OnPropertyChanged(nameof(IsCustomDeathSound));
            ModsChanged();
        }
    }

    public bool IsCustomDeathSound => Md.DeathSound == "Custom file";
    public string DeathSoundFileName => string.IsNullOrEmpty(Md.DeathSoundFile) ? "No file chosen" : Path.GetFileName(Md.DeathSoundFile);

    public void SetDeathSoundFile(string path)
    {
        Md.DeathSoundFile = path;
        Md.DeathSound = "Custom file";
        OnPropertyChanged(nameof(DeathSoundIndex));
        OnPropertyChanged(nameof(IsCustomDeathSound));
        OnPropertyChanged(nameof(DeathSoundFileName));
        ModsChanged();
    }

    public bool HasFont => !string.IsNullOrEmpty(Md.FontFile);
    public string FontFileName => HasFont ? Path.GetFileName(Md.FontFile!) : "Roblox's own fonts";

    public void SetFontFile(string? path)
    {
        Md.FontFile = path;
        OnPropertyChanged(nameof(HasFont));
        OnPropertyChanged(nameof(FontFileName));
        ModsChanged();
    }

    [RelayCommand]
    void ClearFont() => SetFontFile(null);

    // ---------- mod folder & apply ----------

    public bool UseModFolder { get => Md.UseModFolder; set { Md.UseModFolder = value; Changed(); OnPropertyChanged(nameof(ModFolderSummary)); } }

    public string ModFolder => _paths.Modifications;

    public string ModFolderSummary
    {
        get
        {
            int n = Directory.Exists(_paths.Modifications) ? Directory.EnumerateFiles(_paths.Modifications, "*", SearchOption.AllDirectories).Count() : 0;
            return n == 0
                ? "Empty. Put files here in the same folders as inside Roblox, e.g. content\\textures\\…"
                : $"{n} file{(n == 1 ? "" : "s")} will be copied into Roblox.";
        }
    }

    public void RefreshModFolder() => OnPropertyChanged(nameof(ModFolderSummary));

    void ModsChanged()
    {
        _saveTimer.Change(400, Timeout.Infinite);
        OnPropertyChanged(nameof(ModsStatus));
    }

    public string ModsStatus => _installed is { Official: false }
        ? "Changes are applied the next time Roblox starts, or right away with Apply now."
        : "Roblox is downloaded on the first launch; mods are applied then.";

    [RelayCommand]
    void ApplyModsNow()
    {
        if (_installed is not { Official: false } install)
        {
            Toast?.Invoke("Launch Roblox once first: mods go into RoseTrap's own copy of Roblox.");
            return;
        }
        try
        {
            var (r, warnings) = _launcher.ApplyMods(_s, install);
            foreach (var w in warnings) Toast?.Invoke(w);
            SaveNow();
            Toast?.Invoke(r.Written + r.Restored == 0 ? "Mods are already up to date" : $"Mods applied: {r.Written} replaced, {r.Restored} restored");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Toast?.Invoke("Close Roblox first, then try again");
        }
    }
}
