using System.IO.Compression;
using RoseTrap.Core.Flags;
using RoseTrap.Core.Settings;
using RoseTrap.Core.Shaders;

namespace RoseTrap.Core.Tests;

public class SkyboxTests
{
    [Fact]
    public void RendersValidPngs()
    {
        var png = Skyboxes.RenderFace(Skyboxes.Find("Rose Dusk")!, "bk", 64);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
        Assert.Equal(64, (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19]);
    }

    [Fact]
    public void UpFaceIsBrighterSkyThanDownFace()
    {
        var p = Skyboxes.Find("Clear Day")!;
        double Avg(byte[] rgb) => rgb.Average(b => (double)b);
        Assert.True(Avg(Skyboxes.RenderRgb(p, "up", 32)) > Avg(Skyboxes.RenderRgb(p, "dn", 32)));
    }

    [Fact]
    public void SkyboxFilesAreCachedAndMigrationRestoresOldBackup()
    {
        using var t = new TempDir();
        var cache = t.File("cache");
        var files = Skyboxes.Files("Midnight", cache).ToList();
        Assert.Equal(6, files.Count);
        Assert.All(files, f => Assert.Equal(0x89, File.ReadAllBytes(f.Source)[0]));
        Assert.Empty(Skyboxes.Files(Skyboxes.Default, cache));

        // v0.2.0 layout: modified faces + "sky.rosetrap-original" with Roblox's originals.
        var sky = Path.Combine(t.Path, "PlatformContent", "pc", "textures", "sky");
        Directory.CreateDirectory(sky);
        Directory.CreateDirectory(sky + ".rosetrap-original");
        File.WriteAllText(Path.Combine(sky, "sky512_bk.tex"), "modded");
        File.WriteAllText(Path.Combine(sky + ".rosetrap-original", "sky512_bk.tex"), "original");
        Skyboxes.MigrateLegacyBackup(t.Path);
        Assert.Equal("original", File.ReadAllText(Path.Combine(sky, "sky512_bk.tex")));
        Assert.False(Directory.Exists(sky + ".rosetrap-original"));
    }
}

public class GamePresetTests
{
    [Fact]
    public void AllPresetFlagsAreAllowlisted() =>
        Assert.All(GamePresets.All.SelectMany(p => p.Flags.Keys), k => Assert.True(FlagCatalog.IsAllowed(k), k));

    [Fact]
    public void PlaceIdsAreUnique()
    {
        var ids = GamePresets.All.SelectMany(p => p.Games.Select(g => g.PlaceId)).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void LaunchedGamePicksItsPresetAndCustomFlagsStillWin()
    {
        var s = new AppSettings { CustomFlags = [new() { Name = "FIntDebugForceMSAASamples", Value = "8" }] };
        var plan = FlagComposer.Compose(s, placeId: 286090429); // Arsenal
        Assert.Equal("Competitive shooters", plan.GamePreset);
        Assert.Equal("0", plan.Set["FIntFRMMaxGrassDistance"]);
        Assert.Equal("8", plan.Set["FIntDebugForceMSAASamples"]);
    }

    [Fact]
    public void AlwaysPresetIsFallbackAndAutoCanBeOff()
    {
        var s = new AppSettings { GamePresets = { Always = "atmosphere" } };
        Assert.Equal("Horror & story", FlagComposer.Compose(s, placeId: 1).GamePreset);
        Assert.Equal("Competitive shooters", FlagComposer.Compose(s, placeId: 286090429).GamePreset);
        s.GamePresets.AutoByGame = false;
        Assert.Equal("Horror & story", FlagComposer.Compose(s, placeId: 286090429).GamePreset);
        s.GamePresets.Always = null;
        var plan = FlagComposer.Compose(s, placeId: 286090429);
        Assert.Null(plan.GamePreset);
        Assert.Contains("FIntFRMMaxGrassDistance", plan.Remove); // a previous preset's flags get cleaned up
    }
}
