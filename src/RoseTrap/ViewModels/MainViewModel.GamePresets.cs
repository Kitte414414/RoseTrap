using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoseTrap.Core.Flags;
using RoseTrap.Core.Settings;

namespace RoseTrap.ViewModels;

public sealed partial class GamePresetRow(GamePreset preset, MainViewModel owner) : ObservableObject
{
    public GamePreset Preset { get; } = preset;
    public string Name => Preset.Name;
    public string Description => Preset.Description;
    public string Icon => Preset.Icon;
    public string Games => string.Join("  ·  ", Preset.Games.Select(g => g.Name));
    public string FlagSummary => $"{Preset.Flags.Count} flags";
    public IEnumerable<KeyValuePair<string, string>> Flags => Preset.Flags.OrderBy(f => f.Key, StringComparer.Ordinal);

    public bool IsAlways
    {
        get => owner.Settings.GamePresets.Always == Preset.Id;
        set => owner.SetAlwaysPreset(value ? Preset.Id : null);
    }

    public void Refresh() => OnPropertyChanged(nameof(IsAlways));

    [RelayCommand]
    void CopyToFlags() => owner.CopyPresetToFlags(Preset);
}

/// <summary>Presets tab: per-game fast flag sets.</summary>
public sealed partial class MainViewModel
{
    List<GamePresetRow>? _gamePresetRows;
    public IReadOnlyList<GamePresetRow> GamePresetRows => _gamePresetRows ??= GamePresets.All.Select(p => new GamePresetRow(p, this)).ToList();

    public bool AutoGamePresets
    {
        get => _s.GamePresets.AutoByGame;
        set { _s.GamePresets.AutoByGame = value; Changed(); RefreshSummary(); }
    }

    internal void SetAlwaysPreset(string? id)
    {
        if (_s.GamePresets.Always == id) return;
        _s.GamePresets.Always = id;
        foreach (var r in GamePresetRows) r.Refresh();
        _saveTimer.Change(400, Timeout.Infinite);
        RefreshSummary();
        Toast?.Invoke(id is null ? "No preset for unknown games" : $"“{GamePresets.Find(id)!.Name}” is used when a game has no preset of its own");
    }

    /// <summary>Copies a preset into the Fast flags list, where it can be edited flag by flag.</summary>
    internal void CopyPresetToFlags(GamePreset p)
    {
        int added = 0, updated = 0;
        foreach (var (name, value) in p.Flags)
        {
            var row = Flags.FirstOrDefault(f => f.Name == name);
            if (row != null) { row.Value = value; row.Enabled = true; updated++; continue; }
            var model = new CustomFlag { Name = name, Value = value };
            _s.CustomFlags.Add(model);
            Flags.Add(new FlagRow(model, FlagsChanged));
            added++;
        }
        RefreshFilter();
        FlagsChanged();
        Toast?.Invoke($"{p.Name}: added {added} flags, updated {updated} in Fast flags");
    }
}
