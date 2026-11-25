using System.Diagnostics;
using RoseTrap.Core.Flags;
using RoseTrap.Core.Settings;

namespace RoseTrap.Core.Roblox;

public sealed record LaunchResult(RobloxInstall Install, ApplyResult? Flags, IReadOnlyList<string> Skipped, bool BasicSettingsWritten)
{
    public IReadOnlyList<string> Warnings { get; init; } = [];
    /// <summary>Game preset that was applied for this launch, if any.</summary>
    public string? GamePreset { get; init; }
}

/// <summary>Install or update, apply settings, start the player. The whole "Launch" button.</summary>
public sealed class Launcher(Installer installer, AppPaths paths)
{
    // Roblox allows one client per user by holding this mutex; owning it first lets more clients start.
    const string RobloxSingletonMutex = "ROBLOX_singletonMutex";
    Mutex? _multiInstance;

    public ApplyResult ApplySettings(AppSettings s, RobloxInstall? install, out FlagPlan plan, long? placeId = null)
    {
        plan = FlagComposer.Compose(s, placeId);
        var targets = ClientSettingsFile.Targets(install?.Directory, s.Flags.WriteGlobalFile ? paths.GlobalClientSettings : null);
        var result = ClientSettingsFile.Apply(plan, targets);
        s.WrittenCustomFlags = s.CustomFlags.Where(c => c.Enabled).Select(c => c.Name).ToList();
        return result;
    }

    public async Task<LaunchResult> LaunchAsync(AppSettings s, string? link, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        var install = await installer.EnsureLatestAsync(s.Launcher.Channel, progress, ct);

        // A settings file we can't write must not stop the game from starting: report it instead.
        var warnings = new List<string>();
        ApplyResult? flags = null;
        string? gamePreset = null;
        IReadOnlyList<string> skipped = [];
        if (s.Flags.ApplyOnLaunch)
        {
            try
            {
                flags = ApplySettings(s, install, out var plan, link is null ? null : LaunchUri.PlaceId(link));
                gamePreset = plan.GamePreset;
                skipped = plan.Skipped;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Fast flags weren't saved: {e.Message}");
            }
        }

        bool gbs = false;
        try { gbs = GlobalBasicSettings.Apply(paths.GlobalBasicSettings, s.Graphics); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            warnings.Add($"FPS cap wasn't saved: {e.Message}");
        }

        try
        {
            if (!install.Official) Shaders.Skyboxes.Apply(install.Directory, s.Shaders.Skybox, paths.SkyboxCache);
            else if (s.Shaders.Skybox != Shaders.Skyboxes.Default) warnings.Add("Skybox needs RoseTrap's own Roblox install (you're offline on the official one).");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"Skybox wasn't applied: {e.Message}");
        }

        if (s.Launcher.MultiInstance && OperatingSystem.IsWindows())
            _multiInstance ??= new Mutex(initiallyOwned: true, RobloxSingletonMutex);

        progress?.Report(new("Starting Roblox", 1));
        Process.Start(new ProcessStartInfo
        {
            FileName = install.Executable,
            // No link means "open the app", the same as the desktop shortcut.
            Arguments = link is null ? "--app" : $"\"{link}\"",
            WorkingDirectory = install.Directory,
            UseShellExecute = false,
        })?.Dispose();

        return new LaunchResult(install, flags, skipped, gbs) { Warnings = warnings, GamePreset = gamePreset };
    }

    /// <summary>True while RoseTrap must stay open to keep multi-instance working.</summary>
    public bool HoldsMultiInstanceLock => _multiInstance is not null;
}
