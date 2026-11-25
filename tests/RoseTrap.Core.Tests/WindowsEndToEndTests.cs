using System.Diagnostics;
using RoseTrap.Core.Flags;
using RoseTrap.Core.Roblox;
using RoseTrap.Core.Settings;
using Xunit.Abstractions;

namespace RoseTrap.Core.Tests;

/// <summary>
/// Real install → apply → launch against Roblox's CDN. Windows only, opt-in (ROSETRAP_E2E=1),
/// sandboxed under a temp LocalAppData so it never touches a real Roblox setup.
/// </summary>
public class WindowsEndToEndTests(ITestOutputHelper log)
{
    static bool Enabled => OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("ROSETRAP_E2E") == "1";

    [Fact]
    public async Task InstallApplyAndLaunch()
    {
        if (!Enabled) return;
        var root = Directory.CreateTempSubdirectory("rosetrap-e2e-").FullName;
        var paths = new AppPaths(root);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var installer = new Installer(http, paths);

        // Users often lock Roblox's settings file read-only so the game can't reset it.
        Directory.CreateDirectory(paths.RobloxRoot);
        File.WriteAllText(paths.GlobalBasicSettings,
            """<roblox version="4"><Item class="UserGameSettings" referent="RBX0"><Properties><int name="FramerateCap">60</int></Properties></Item></roblox>""");
        File.SetAttributes(paths.GlobalBasicSettings, FileAttributes.ReadOnly);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.GlobalClientSettings)!);
        File.WriteAllText(paths.GlobalClientSettings, "{}");
        File.SetAttributes(paths.GlobalClientSettings, FileAttributes.ReadOnly);

        var sw = Stopwatch.StartNew();
        var progress = new Progress<InstallProgress>(p => log.WriteLine($"{sw.Elapsed:mm\\:ss} {p.Stage} {p.Fraction:P0}"));
        var settings = new AppSettings { Graphics = { FpsCap = 240, Potato = true }, Launcher = { CloseAfterLaunch = false } };

        var result = await new Launcher(installer, paths).LaunchAsync(settings, null, progress, CancellationToken.None);
        log.WriteLine($"installed {result.Install.Version} in {sw.Elapsed}, warnings: {string.Join(" | ", result.Warnings)}");

        Assert.True(File.Exists(result.Install.Executable));
        Assert.True(File.Exists(Path.Combine(result.Install.Directory, "AppSettings.xml")));
        Assert.Empty(result.Warnings);
        Assert.Equal(240, GlobalBasicSettings.ReadInt(paths.GlobalBasicSettings, "FramerateCap"));
        Assert.True(File.GetAttributes(paths.GlobalBasicSettings).HasFlag(FileAttributes.ReadOnly), "read-only lock should be kept");
        Assert.Contains("FIntDebugForceMSAASamples", File.ReadAllText(ClientSettingsFile.ForVersion(result.Install.Directory)));

        // Started: give it a moment, then clean up.
        await Task.Delay(4000);
        var running = Process.GetProcessesByName("RobloxPlayerBeta");
        log.WriteLine($"RobloxPlayerBeta processes: {running.Length}");
        foreach (var p in running) { try { p.Kill(entireProcessTree: true); } catch { } }

        // Second launch must reuse the install, not re-download.
        sw.Restart();
        var again = await installer.EnsureLatestAsync("LIVE", null, CancellationToken.None);
        Assert.Equal(result.Install.Directory, again.Directory);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20));

        // Mods against the real install: every replaced file must come back byte-for-byte.
        var launcher = new Launcher(installer, paths);
        string[] touched = [Mods.ModSet.PointerPath, Mods.ModSet.HoverPath, Mods.ModSet.ShiftLockPath, Mods.ModSet.DeathSoundPath,
                            Shaders.Skyboxes.FacePath("bk"), "content/fonts/families/BuilderSans.json"];
        string Abs(string rel) => Path.Combine(again.Directory, rel.Replace('/', Path.DirectorySeparatorChar));
        var before = touched.ToDictionary(r => r, r => File.ReadAllBytes(Abs(r)));
        var font = Path.Combine(again.Directory, "content", "fonts", "Arimo-Regular.ttf");
        var modded = new AppSettings
        {
            Shaders = { Skybox = "Rose Dusk" },
            Mods = { Cursor = "Rose", ShiftLock = "Rose ring", DeathSound = "Classic oof", FontFile = font },
        };
        var (applied, modWarnings) = launcher.ApplyMods(modded, again);
        log.WriteLine($"mods: {applied}, warnings: {string.Join(" | ", modWarnings)}");
        Assert.Empty(modWarnings);
        Assert.All(touched, r => Assert.NotEqual(before[r], File.ReadAllBytes(Abs(r))));
        Assert.Equal(File.ReadAllBytes(Abs("content/sounds/oof.ogg")), File.ReadAllBytes(Abs(Mods.ModSet.DeathSoundPath)));

        var (restored, _) = launcher.ApplyMods(new AppSettings(), again);
        Assert.True(restored.Restored >= touched.Length);
        Assert.All(touched, r => Assert.Equal(before[r], File.ReadAllBytes(Abs(r))));
        Assert.False(File.Exists(Abs("content/fonts/CustomFont.ttf")));
    }
}
