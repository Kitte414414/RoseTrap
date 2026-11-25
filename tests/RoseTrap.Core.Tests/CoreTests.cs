using System.IO.Compression;
using System.Text.Json.Nodes;
using RoseTrap.Core;
using RoseTrap.Core.Flags;
using RoseTrap.Core.Roblox;
using RoseTrap.Core.Settings;

namespace RoseTrap.Core.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("rosetrap-").FullName;
    public string File(string name) => System.IO.Path.Combine(Path, name);
    public void Dispose() => Directory.Delete(Path, recursive: true);
}

public class FlagCatalogTests
{
    [Theory]
    [InlineData("FFlagDebugSkyGray", FlagType.Bool)]
    [InlineData("DFIntTextureQualityOverride", FlagType.Int)]
    [InlineData("SFStringSomething", FlagType.String)]
    [InlineData("FLogNetwork", FlagType.Log)]
    public void TypeComesFromPrefix(string name, FlagType type) => Assert.Equal(type, FlagCatalog.TypeOf(name));

    [Theory]
    [InlineData("FFlagX", true)]
    [InlineData("DFIntCSGLevelOfDetailSwitchingDistance", true)]
    [InlineData("FFlag", false)]
    [InlineData("Banana", false)]
    [InlineData("FFlag Bad", false)]
    [InlineData("", false)]
    public void ValidatesNames(string name, bool ok) => Assert.Equal(ok, FlagCatalog.IsValidName(name));

    [Theory]
    [InlineData("FFlagA", "true", "True")]
    [InlineData("FFlagA", "0", "False")]
    [InlineData("FFlagA", "maybe", null)]
    [InlineData("FIntA", " 42 ", "42")]
    [InlineData("FIntA", "4.2", null)]
    [InlineData("FStringA", "hi", "hi")]
    public void NormalizesValues(string name, string input, string? expected) =>
        Assert.Equal(expected, FlagCatalog.NormalizeValue(name, input));

    [Fact]
    public void AllowlistHasEighteenFlags() => Assert.Equal(18, FlagCatalog.Allowlist.Count);
}

public class FlagComposerTests
{
    [Fact]
    public void DefaultsWriteOnlyTheFpsFlagWhichTheAllowlistDrops()
    {
        var plan = FlagComposer.Compose(new AppSettings());
        Assert.Empty(plan.Set);
        Assert.Equal(["DFIntTaskSchedulerTargetFps"], plan.Skipped);
    }

    [Fact]
    public void PotatoUsesCheapestAllowedFlags()
    {
        var s = new AppSettings { Graphics = { Potato = true } };
        var set = FlagComposer.Compose(s).Set;
        Assert.Equal("1", set["FIntDebugForceMSAASamples"]);
        Assert.Equal("0", set["DFIntTextureQualityOverride"]);
        Assert.Equal("0", set["FIntFRMMaxGrassDistance"]);
        Assert.Equal("0", set["DFIntCSGLevelOfDetailSwitchingDistanceL34"]);
        Assert.Equal("True", set["DFFlagDebugPauseVoxelizer"]);
        Assert.All(set.Keys, k => Assert.True(FlagCatalog.IsAllowed(k), k));
    }

    [Fact]
    public void LegacyFlagsAreWrittenWhenAllowlistIsOff()
    {
        var s = new AppSettings { Flags = { AllowlistOnly = false }, Graphics = { Lighting = LightingTech.Future, Shadows = false } };
        var plan = FlagComposer.Compose(s);
        Assert.Equal("True", plan.Set["FFlagDebugForceFutureIsBrightPhase3"]);
        Assert.Equal("True", plan.Set["FFlagDebugForceDisableShadows"]);
        Assert.Equal("144", plan.Set["DFIntTaskSchedulerTargetFps"]);
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void CustomFlagsOverrideOptionsAndDisabledOnesAreRemoved()
    {
        var s = new AppSettings
        {
            Graphics = { Msaa = 4 },
            CustomFlags =
            [
                new() { Name = "FIntDebugForceMSAASamples", Value = "8" },
                new() { Name = "FFlagDebugSkyGray", Value = "true", Enabled = false },
            ],
            WrittenCustomFlags = ["FIntFRMMinGrassDistance"],
        };
        var plan = FlagComposer.Compose(s);
        Assert.Equal("8", plan.Set["FIntDebugForceMSAASamples"]);
        Assert.Contains("FFlagDebugSkyGray", plan.Remove);
        Assert.Contains("FIntFRMMinGrassDistance", plan.Remove);
        Assert.DoesNotContain("FIntDebugForceMSAASamples", plan.Remove);
    }

    [Fact]
    public void PresetsRoundTrip()
    {
        var g = Presets.Apply(Preset.Quality, new GraphicsSettings());
        Assert.Equal(10, g.Quality);
        Assert.Equal(4, g.Msaa);
        g = Presets.Apply(Preset.Balanced, g);
        Assert.Equal(0, g.Quality);
        Assert.False(g.Potato);
    }
}

public class ClientSettingsFileTests
{
    [Fact]
    public void MergesKeepsForeignFlagsRemovesOwnedAndBacksUpOnce()
    {
        using var t = new TempDir();
        var file = t.File("ClientAppSettings.json");
        File.WriteAllText(file, """{ "FFlagMine": true, "FFlagDebugSkyGray": "True" }""");

        var plan = new FlagPlan(new Dictionary<string, string> { ["FIntDebugForceMSAASamples"] = "4" },
            new HashSet<string> { "FFlagDebugSkyGray" }, []);
        var r = ClientSettingsFile.Apply(plan, [file]);

        var json = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        Assert.Equal(1, r.Removed);
        Assert.True(json["FFlagMine"]!.GetValue<bool>());
        Assert.Equal("4", json["FIntDebugForceMSAASamples"]!.GetValue<string>());
        Assert.False(json.ContainsKey("FFlagDebugSkyGray"));

        // Second write must not overwrite the original backup.
        ClientSettingsFile.Apply(plan with { Set = new Dictionary<string, string>() }, [file]);
        Assert.Equal(1, ClientSettingsFile.Restore([file]));
        Assert.Contains("FFlagDebugSkyGray", File.ReadAllText(file));
        Assert.False(File.Exists(file + ClientSettingsFile.BackupSuffix));
    }

    [Fact]
    public void CorruptFileIsReplaced()
    {
        using var t = new TempDir();
        var file = t.File("c.json");
        File.WriteAllText(file, "{ nope");
        ClientSettingsFile.Apply(new FlagPlan(new Dictionary<string, string> { ["FFlagA"] = "True" }, new HashSet<string>(), []), [file]);
        Assert.Equal("True", JsonNode.Parse(File.ReadAllText(file))!["FFlagA"]!.GetValue<string>());
    }

    [Fact]
    public void TargetsDedupe()
    {
        var dir = Path.Combine(Path.GetTempPath(), "v");
        var global = ClientSettingsFile.ForVersion(dir);
        Assert.Single(ClientSettingsFile.Targets(dir, global));
        Assert.Equal(2, ClientSettingsFile.Targets(dir, "/x/ClientAppSettings.json").Count);
    }
}

public class GlobalBasicSettingsTests
{
    const string Xml = """
        <roblox xmlns:xmime="http://www.w3.org/2005/05/xmlmime" version="4">
          <External>null</External>
          <Item class="UserGameSettings" referent="RBX0">
            <Properties>
              <int name="FramerateCap">60</int>
              <bool name="Fullscreen">false</bool>
            </Properties>
          </Item>
        </roblox>
        """;

    [Fact]
    public void WritesFramerateCapAndQuality()
    {
        using var t = new TempDir();
        var file = t.File("GlobalBasicSettings_13.xml");
        File.WriteAllText(file, Xml);
        Assert.True(GlobalBasicSettings.Apply(file, new GraphicsSettings { FpsCap = 240, Quality = 7 }));
        Assert.Equal(240, GlobalBasicSettings.ReadInt(file, "FramerateCap"));
        Assert.Equal(7, GlobalBasicSettings.ReadInt(file, "SavedQualityLevel"));
        Assert.Contains("<bool name=\"Fullscreen\">false</bool>", File.ReadAllText(file));
    }

    [Fact]
    public void PotatoForcesQualityOneAndMissingFileIsSkipped()
    {
        using var t = new TempDir();
        var file = t.File("g.xml");
        Assert.False(GlobalBasicSettings.Apply(file, new GraphicsSettings()));
        File.WriteAllText(file, Xml);
        GlobalBasicSettings.Apply(file, new GraphicsSettings { Potato = true, Quality = 9 });
        Assert.Equal(1, GlobalBasicSettings.ReadInt(file, "SavedQualityLevel"));
    }
}

public class RobloxTests
{
    [Fact]
    public void ParsesManifest()
    {
        var pkgs = PackageManifest.Parse("v0\r\nRobloxApp.zip\r\nabc\r\n10\r\n20\r\nssl.zip\r\ndef\r\n1\r\n2\r\n");
        Assert.Equal(2, pkgs.Count);
        Assert.Equal(new Package("ssl.zip", "def", 1, 2), pkgs[1]);
        Assert.Throws<FormatException>(() => PackageManifest.Parse("v1\n"));
        Assert.Throws<FormatException>(() => PackageManifest.Parse("v0\nRobloxApp.zip\nabc\n"));
    }

    [Theory]
    [InlineData("roblox-player:1+launchmode:play+placelauncherurl:https%3A%2F%2Fx%3FplaceId%3D1818%26r", 1818L)]
    [InlineData("roblox://placeId=920587237", 920587237L)]
    [InlineData("roblox-player:1+launchmode:app", null)]
    public void FindsPlaceId(string link, long? id) => Assert.Equal(id, LaunchUri.PlaceId(link));

    [Fact]
    public void DetectsLinksInArgs()
    {
        Assert.Equal("roblox-player:1", LaunchUri.FindInArgs(["--x", "roblox-player:1"]));
        Assert.Null(LaunchUri.FindInArgs(["https://roblox.com"]));
    }

    [Fact]
    public void BuildsChannelUrls()
    {
        Assert.Equal("https://setup.rbxcdn.com/version-1-ssl.zip", Deployment.PackageUrl("LIVE", "version-1", "ssl.zip"));
        Assert.Equal("https://setup.rbxcdn.com/channel/zbeta/version-1-ssl.zip", Deployment.PackageUrl("ZBeta", "version-1", "ssl.zip"));
        Assert.EndsWith("/channel/ZBeta", Deployment.VersionUrl("ZBeta"));
    }

    [Fact]
    public void ExtractionBlocksZipSlip()
    {
        using var t = new TempDir();
        var zipPath = t.File("p.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(zip.CreateEntry("ok\\a.txt").Open())) w.Write("a");
            using (var w = new StreamWriter(zip.CreateEntry("../evil.txt").Open())) w.Write("x");
        }
        var dest = t.File("out");
        Installer.ExtractPackage(zipPath, dest);
        Assert.True(File.Exists(Path.Combine(dest, "ok", "a.txt")));
        Assert.False(File.Exists(t.File("evil.txt")));
    }

    [Fact]
    public void FindLocalPrefersOwnInstall()
    {
        using var t = new TempDir();
        var paths = new AppPaths(t.Path);
        foreach (var root in new[] { paths.Versions, paths.OfficialVersions })
        {
            var d = Path.Combine(root, "version-abc");
            Directory.CreateDirectory(d);
            File.WriteAllText(Path.Combine(d, Installer.PlayerExe), "");
        }
        var found = new Installer(new HttpClient(), paths).FindLocal();
        Assert.NotNull(found);
        Assert.False(found.Official);
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void RoundTripsAndSurvivesCorruption()
    {
        using var t = new TempDir();
        var file = t.File("s.json");
        var s = new AppSettings { Preset = Preset.Potato, CustomFlags = [new() { Name = "FFlagA", Value = "True" }] };
        s.Graphics.RenderApi = RenderApi.Vulkan;
        SettingsStore.Save(file, s);
        Assert.Contains("\"vulkan\"", File.ReadAllText(file), StringComparison.OrdinalIgnoreCase);
        var back = SettingsStore.Load(file);
        Assert.Equal(Preset.Potato, back.Preset);
        Assert.Equal(RenderApi.Vulkan, back.Graphics.RenderApi);
        Assert.Equal("FFlagA", back.CustomFlags[0].Name);

        File.WriteAllText(file, "{{{");
        Assert.True(SettingsStore.Load(file).Appearance.CompactSidebar);
        Assert.True(File.Exists(file + ".broken"));
    }
}
