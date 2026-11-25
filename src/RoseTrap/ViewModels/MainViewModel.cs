using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoseTrap.Core;
using RoseTrap.Core.Flags;
using RoseTrap.Core.Roblox;
using RoseTrap.Core.Settings;
using RoseTrap.Services;

namespace RoseTrap.ViewModels;

public sealed partial class FlagRow(CustomFlag model, Action changed) : ObservableObject
{
    public CustomFlag Model { get; } = model;
    public string Name => Model.Name;
    public string Type => FlagCatalog.TypeOf(Model.Name).ToString();
    public bool IsAllowed => FlagCatalog.IsAllowed(Model.Name);

    public string Value
    {
        get => Model.Value;
        set
        {
            if (Model.Value == value) return;
            Model.Value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValueValid));
            changed();
        }
    }

    public bool IsValueValid => FlagCatalog.NormalizeValue(Model.Name, Model.Value) is not null;

    public bool Enabled
    {
        get => Model.Enabled;
        set { if (Model.Enabled == value) return; Model.Enabled = value; OnPropertyChanged(); changed(); }
    }
}

public sealed partial class MainViewModel : ObservableObject
{
    public static readonly int[] FpsStops = [30, 60, 72, 90, 120, 144, 165, 240, 360, GlobalBasicSettings.Unlimited];
    public static readonly string[] AccentNames = ["Rose", "Crimson", "Lilac", "Peach", "Mint"];
    static readonly int[] MsaaValues = [0, 1, 2, 4, 8];

    readonly AppPaths _paths;
    readonly Installer _installer;
    readonly Launcher _launcher;
    readonly AppSettings _s;
    readonly Timer _saveTimer;

    public event Action<string>? Toast;
    public event Action? CloseRequested;
    /// <summary>RoseTrap has to stay running (shaders, multi-instance) but should get out of the way.</summary>
    public event Action? MinimizeRequested;
    readonly Avalonia.Threading.DispatcherTimer _robloxWatch;
    public event Action? AppearanceChanged;

    public MainViewModel() : this(AppPaths.Default, null) { }

    public MainViewModel(AppPaths paths, AppSettings? settings)
    {
        _paths = paths;
        _s = settings ?? SettingsStore.Load(paths.SettingsFile);
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("RoseTrap/0.1");
        _installer = new Installer(http, paths);
        _launcher = new Launcher(_installer, paths);
        _saveTimer = new Timer(_ => SaveNow());

        foreach (var f in _s.CustomFlags) Flags.Add(new FlagRow(f, FlagsChanged));
        RefreshFilter();
        _installed = _installer.FindLocal();
        InitShaders();
        _robloxWatch = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _robloxWatch.Tick += (_, _) =>
        {
            // Stayed open after launching only for shaders: leave once the game is closed.
            if (!RoseTrap.Services.ShaderService.RobloxRunning()) { _robloxWatch.Stop(); CloseRequested?.Invoke(); }
        };
    }

    public AppSettings Settings => _s;
    GraphicsSettings G => _s.Graphics;

    // ---------- persistence ----------

    void Changed([CallerMemberName] string? name = null)
    {
        OnPropertyChanged(name);
        _saveTimer.Change(400, Timeout.Infinite);
    }

    public void SaveNow()
    {
        try { SettingsStore.Save(_paths.SettingsFile, _s); }
        catch (IOException) { /* retried on the next change */ }
    }

    /// <summary>Touching any graphics option by hand turns the preset into Custom.</summary>
    void GraphicsChanged([CallerMemberName] string? name = null)
    {
        Changed(name);
        if (_applyingPreset) return;
        if (_s.Preset != Preset.Custom && name != nameof(Potato)) SetPreset(Preset.Custom);
        RefreshSummary();
    }

    // ---------- General ----------

    public int FpsIndex
    {
        get { var i = Array.IndexOf(FpsStops, G.FpsCap); return i < 0 ? 5 : i; }
        set { G.FpsCap = FpsStops[Math.Clamp(value, 0, FpsStops.Length - 1)]; Changed(); OnPropertyChanged(nameof(FpsLabel)); RefreshSummary(); }
    }
    public string FpsLabel => G.FpsCap >= GlobalBasicSettings.Unlimited ? "Unlimited" : $"{G.FpsCap} FPS";

    public int Quality
    {
        get => FlagComposer.Effective(G).Quality;
        set { G.Quality = value; GraphicsChanged(); OnPropertyChanged(nameof(QualityLabel)); }
    }
    public string QualityLabel => Quality == 0 ? "Auto" : Quality.ToString();

    public bool Potato
    {
        get => G.Potato;
        set
        {
            if (G.Potato == value) return;
            G.Potato = value;
            GraphicsChanged();
            SetPreset(value ? Preset.Potato : Preset.Balanced, apply: false);
            RefreshGraphics();
            Toast?.Invoke(value ? "Potato mode on" : "Potato mode off");
        }
    }
    public bool NotPotato => !G.Potato;

    [ObservableProperty] bool _isLaunching;
    [ObservableProperty] double _launchProgress;
    [ObservableProperty] string _launchStatus = "Ready";

    RobloxInstall? _installed;
    public string InstalledVersion => _installed is null ? "Not installed" : _installed.Version.Replace("version-", "");
    public string InstallSource => _installed is null ? "Downloads on first launch" : _installed.Official ? "Official Roblox install" : "RoseTrap install";

    public int ActiveFlagCount => FlagComposer.Compose(_s).Set.Count;
    public string PresetName => _s.Preset.ToString();

    void RefreshSummary()
    {
        OnPropertyChanged(nameof(ActiveFlagCount));
        OnPropertyChanged(nameof(PresetName));
        OnPropertyChanged(nameof(SkippedSummary));
        OnPropertyChanged(nameof(FlagSummary));
    }

    [RelayCommand]
    public async Task LaunchAsync(string? link = null)
    {
        if (IsLaunching) return;
        IsLaunching = true;
        try
        {
            var progress = new Progress<InstallProgress>(p => { LaunchStatus = p.Stage; LaunchProgress = p.Fraction; });
            var r = await _launcher.LaunchAsync(_s, link, progress, CancellationToken.None);
            _installed = r.Install;
            OnPropertyChanged(nameof(InstalledVersion));
            OnPropertyChanged(nameof(InstallSource));
            SaveNow();
            LaunchStatus = "Roblox started";
            foreach (var w in r.Warnings) Toast?.Invoke(w);
            if (r.GamePreset != null) Toast?.Invoke($"Game preset: {r.GamePreset}");
            Toast?.Invoke(r.BasicSettingsWritten || r.Warnings.Count > 0 ? "Roblox started" : "Roblox started · FPS cap applies after its first run");
            if (_s.Launcher.CloseAfterLaunch)
            {
                if (_launcher.HoldsMultiInstanceLock) MinimizeRequested?.Invoke();
                else if (Sh.Enabled && ShaderService.IsSupported) { MinimizeRequested?.Invoke(); _robloxWatch.Start(); }
                else CloseRequested?.Invoke();
            }
        }
        catch (Exception e)
        {
            LaunchStatus = "Launch failed";
            Toast?.Invoke(e.Message);
        }
        finally
        {
            IsLaunching = false;
        }
    }

    // ---------- Global ----------

    public int RenderApi { get => (int)G.RenderApi; set { G.RenderApi = (RenderApi)value; GraphicsChanged(); } }
    public int MsaaIndex
    {
        get => Math.Max(0, Array.IndexOf(MsaaValues, FlagComposer.Effective(G).Msaa));
        set { G.Msaa = MsaaValues[value]; GraphicsChanged(); }
    }
    public int Textures { get => (int)FlagComposer.Effective(G).Textures; set { G.Textures = (TextureQuality)value; GraphicsChanged(); } }
    public bool MeshLowest { get => FlagComposer.Effective(G).Mesh == MeshDetail.Lowest; set { G.Mesh = value ? MeshDetail.Lowest : MeshDetail.Default; GraphicsChanged(); } }
    public bool Grass { get => FlagComposer.Effective(G).Grass; set { G.Grass = value; GraphicsChanged(); } }
    public bool DisableDpiScale { get => G.DisableDpiScale; set { G.DisableDpiScale = value; GraphicsChanged(); } }
    public bool ExclusiveFullscreen { get => G.ExclusiveFullscreen; set { G.ExclusiveFullscreen = value; GraphicsChanged(); } }

    // ---------- Shaders (look) ----------

    public int Lighting { get => (int)FlagComposer.Effective(G).Lighting; set { G.Lighting = (LightingTech)value; GraphicsChanged(); } }
    public bool Shadows { get => FlagComposer.Effective(G).Shadows; set { G.Shadows = value; GraphicsChanged(); } }
    public bool PostProcessing { get => FlagComposer.Effective(G).PostProcessing; set { G.PostProcessing = value; GraphicsChanged(); } }
    public bool GraySky { get => G.GraySky; set { G.GraySky = value; GraphicsChanged(); } }
    public bool PauseVoxelizer { get => FlagComposer.Effective(G).PauseVoxelizer; set { G.PauseVoxelizer = value; GraphicsChanged(); } }
    public int FrmQuality
    {
        get => FlagComposer.Effective(G).FrmQuality;
        set { G.FrmQuality = value; GraphicsChanged(); OnPropertyChanged(nameof(FrmLabel)); }
    }
    public string FrmLabel => FrmQuality == 0 ? "Auto" : FrmQuality.ToString();
    public bool LegacyIgnored => _s.Flags.AllowlistOnly;

    void RefreshGraphics()
    {
        foreach (var p in new[] { nameof(Quality), nameof(QualityLabel), nameof(MsaaIndex), nameof(Textures), nameof(MeshLowest),
                     nameof(Grass), nameof(Lighting), nameof(Shadows), nameof(PostProcessing), nameof(PauseVoxelizer),
                     nameof(FrmQuality), nameof(FrmLabel), nameof(RenderApi), nameof(GraySky), nameof(Potato), nameof(NotPotato) })
            OnPropertyChanged(p);
        RefreshSummary();
    }

    // ---------- Presets ----------

    bool _applyingPreset;
    public bool IsPotatoPreset { get => _s.Preset == Preset.Potato; set { if (value) SetPreset(Preset.Potato); } }
    public bool IsBalancedPreset { get => _s.Preset == Preset.Balanced; set { if (value) SetPreset(Preset.Balanced); } }
    public bool IsQualityPreset { get => _s.Preset == Preset.Quality; set { if (value) SetPreset(Preset.Quality); } }
    public bool IsCustomPreset { get => _s.Preset == Preset.Custom; set { if (value) SetPreset(Preset.Custom); } }

    void SetPreset(Preset p, bool apply = true)
    {
        _s.Preset = p;
        if (apply && p != Preset.Custom)
        {
            _applyingPreset = true;
            _s.Graphics = Presets.Apply(p, G);
            _applyingPreset = false;
            RefreshGraphics();
        }
        Changed(nameof(PresetName));
        foreach (var n in new[] { nameof(IsPotatoPreset), nameof(IsBalancedPreset), nameof(IsQualityPreset), nameof(IsCustomPreset) })
            OnPropertyChanged(n);
        RefreshSummary();
    }

    // ---------- Fast flags editor ----------

    public ObservableCollection<FlagRow> Flags { get; } = [];
    public ObservableCollection<FlagRow> FilteredFlags { get; } = [];

    [ObservableProperty] string _search = "";
    [ObservableProperty] string _newFlagName = "";
    [ObservableProperty] string _newFlagValue = "";
    [ObservableProperty] bool _newFlagInvalid;

    partial void OnSearchChanged(string value) => RefreshFilter();
    partial void OnNewFlagNameChanged(string value) => NewFlagInvalid = false;

    public bool HasNoFlags => FilteredFlags.Count == 0;
    public string EmptyText => Flags.Count == 0 ? "No flags yet. Add one below." : "No flags match your search.";
    public string FlagSummary => $"{Flags.Count} custom flags · {Flags.Count(f => f.Enabled)} enabled · {ActiveFlagCount} will be written";

    void RefreshFilter()
    {
        FilteredFlags.Clear();
        foreach (var f in Flags.Where(f => f.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase)))
            FilteredFlags.Add(f);
        OnPropertyChanged(nameof(HasNoFlags));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(FlagSummary));
    }

    void FlagsChanged()
    {
        _saveTimer.Change(400, Timeout.Infinite);
        RefreshSummary();
    }

    [RelayCommand]
    void AddFlag()
    {
        var name = NewFlagName.Trim();
        if (!FlagCatalog.IsValidName(name) || Flags.Any(f => f.Name == name))
        {
            NewFlagInvalid = true;
            Toast?.Invoke(name.Length == 0 ? "Enter a flag name" : Flags.Any(f => f.Name == name) ? "That flag is already in the list" : "Flag names look like FFlagName, FIntName, DFIntName…");
            return;
        }
        var value = NewFlagValue.Trim();
        if (value.Length == 0) value = FlagCatalog.TypeOf(name) == FlagType.Bool ? "True" : "0";
        var model = new CustomFlag { Name = name, Value = FlagCatalog.NormalizeValue(name, value) ?? value };
        _s.CustomFlags.Insert(0, model);
        Flags.Insert(0, new FlagRow(model, FlagsChanged));
        NewFlagName = NewFlagValue = "";
        Search = "";
        RefreshFilter();
        FlagsChanged();
        Toast?.Invoke(FlagCatalog.IsAllowed(name) ? $"Added {name}" : $"Added {name} · Roblox ignores it (not allowlisted)");
    }

    [RelayCommand]
    void RemoveFlag(FlagRow row)
    {
        _s.CustomFlags.Remove(row.Model);
        Flags.Remove(row);
        RefreshFilter();
        FlagsChanged();
    }

    [RelayCommand]
    void ClearFlags()
    {
        _s.CustomFlags.Clear();
        Flags.Clear();
        RefreshFilter();
        FlagsChanged();
        Toast?.Invoke("Custom flags cleared");
    }

    /// <summary>Accepts a ClientAppSettings-style JSON object, e.g. pasted from another bootstrapper.</summary>
    public void ImportJson(string json)
    {
        try
        {
            var obj = System.Text.Json.Nodes.JsonNode.Parse(json) as System.Text.Json.Nodes.JsonObject
                      ?? throw new FormatException();
            int added = 0;
            foreach (var (name, node) in obj)
            {
                if (!FlagCatalog.IsValidName(name) || node is null) continue;
                var value = node.ToString();
                var existing = Flags.FirstOrDefault(f => f.Name == name);
                if (existing != null) { existing.Value = value; continue; }
                var model = new CustomFlag { Name = name, Value = value };
                _s.CustomFlags.Add(model);
                Flags.Add(new FlagRow(model, FlagsChanged));
                added++;
            }
            RefreshFilter();
            FlagsChanged();
            Toast?.Invoke($"Imported {added} flags");
        }
        catch (Exception)
        {
            Toast?.Invoke("Clipboard doesn't contain a JSON object of flags");
        }
    }

    public string ExportJson() =>
        System.Text.Json.JsonSerializer.Serialize(
            new Dictionary<string, string>(FlagComposer.Compose(_s).Set), FlagsJson.Default.DictionaryStringString);

    // ---------- FFlags settings ----------

    public bool ApplyOnLaunch { get => _s.Flags.ApplyOnLaunch; set { _s.Flags.ApplyOnLaunch = value; Changed(); } }
    public bool AllowlistOnly
    {
        get => _s.Flags.AllowlistOnly;
        set { _s.Flags.AllowlistOnly = value; Changed(); OnPropertyChanged(nameof(LegacyIgnored)); RefreshSummary(); }
    }
    public bool WriteGlobalFile { get => _s.Flags.WriteGlobalFile; set { _s.Flags.WriteGlobalFile = value; Changed(); } }

    public string SkippedSummary
    {
        get
        {
            var skipped = FlagComposer.Compose(_s).Skipped;
            return skipped.Count == 0 ? "Nothing is being skipped." : $"Skipped (Roblox ignores them): {string.Join(", ", skipped)}";
        }
    }

    IReadOnlyList<string> Targets() =>
        ClientSettingsFile.Targets(_installed?.Directory, _s.Flags.WriteGlobalFile ? _paths.GlobalClientSettings : null);

    [RelayCommand]
    void ApplyNow()
    {
        try
        {
            var r = _launcher.ApplySettings(_s, _installed, out _);
            GlobalBasicSettings.Apply(_paths.GlobalBasicSettings, G);
            SaveNow();
            Toast?.Invoke($"Wrote {r.Written} flags, removed {r.Removed}");
        }
        catch (Exception e) { Toast?.Invoke(e.Message); }
    }

    [RelayCommand]
    void RestoreBackup()
    {
        try
        {
            var n = ClientSettingsFile.Restore(Targets());
            Toast?.Invoke(n == 0 ? "No backup to restore" : $"Restored {n} file(s) to how they were before RoseTrap");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Toast?.Invoke(e.Message); }
    }

    [RelayCommand]
    void RemoveAllFlags()
    {
        var plan = new FlagPlan(new Dictionary<string, string>(),
            new HashSet<string>(FlagComposer.ManagedFlags.Concat(_s.CustomFlags.Select(c => c.Name)).Concat(_s.WrittenCustomFlags)), []);
        try
        {
            var r = ClientSettingsFile.Apply(plan, Targets());
        _s.WrittenCustomFlags.Clear();
        SaveNow();
        Toast?.Invoke($"Removed {r.Removed} RoseTrap flags from Roblox");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Toast?.Invoke(e.Message); }
    }

    public string ClientSettingsFolder => Path.GetDirectoryName(Targets().FirstOrDefault() ?? _paths.GlobalClientSettings)!;
    public string RoseTrapFolder => _paths.Root;

    // ---------- Settings ----------

    public bool InterceptLinks
    {
        get => _s.Launcher.InterceptLinks;
        set { _s.Launcher.InterceptLinks = value; Changed(); RegisterLinks(); }
    }
    public bool MultiInstance { get => _s.Launcher.MultiInstance; set { _s.Launcher.MultiInstance = value; Changed(); } }
    public bool CloseAfterLaunch { get => _s.Launcher.CloseAfterLaunch; set { _s.Launcher.CloseAfterLaunch = value; Changed(); } }
    public string Channel
    {
        get => _s.Launcher.Channel;
        set { _s.Launcher.Channel = string.IsNullOrWhiteSpace(value) ? "LIVE" : value.Trim(); Changed(); }
    }

    public void RegisterLinks()
    {
        if (!OperatingSystem.IsWindows() || !_s.Launcher.InterceptLinks) return;
        var exe = Environment.ProcessPath;
        if (exe is null) return;
        try { ProtocolRegistrar.Register(exe, exe); }
        catch (Exception e) { Toast?.Invoke("Couldn't register Roblox links: " + e.Message); }
    }

    [RelayCommand]
    void Reinstall()
    {
        try
        {
            if (Directory.Exists(_paths.Versions)) Directory.Delete(_paths.Versions, recursive: true);
            _installed = _installer.FindLocal();
            OnPropertyChanged(nameof(InstalledVersion));
            OnPropertyChanged(nameof(InstallSource));
            Toast?.Invoke("Roblox will be downloaded fresh on the next launch");
        }
        catch (IOException) { Toast?.Invoke("Close Roblox first, then try again"); }
    }

    // ---------- Appearance ----------

    public int AccentIndex
    {
        get => Math.Max(0, Array.IndexOf(AccentNames, _s.Appearance.Accent));
        set { _s.Appearance.Accent = AccentNames[value]; Changed(); AppearanceChanged?.Invoke(); }
    }
    public bool CompactSidebar
    {
        get => _s.Appearance.CompactSidebar;
        set { _s.Appearance.CompactSidebar = value; Changed(); AppearanceChanged?.Invoke(); }
    }
    public bool Animations
    {
        get => _s.Appearance.Animations;
        set { _s.Appearance.Animations = value; Changed(); AppearanceChanged?.Invoke(); }
    }
}

[System.Text.Json.Serialization.JsonSourceGenerationOptions(WriteIndented = true)]
[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class FlagsJson : System.Text.Json.Serialization.JsonSerializerContext;
