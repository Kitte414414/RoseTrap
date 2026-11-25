using System.IO.Compression;
using RoseTrap.Core.Flags;
using RoseTrap.Core.Settings;
using RoseTrap.Core.Shaders;

namespace RoseTrap.Core.Tests;

public class ColorGradeTests
{
    [Fact]
    public void NeutralIsIdentity()
    {
        var m = ColorGrade.Neutral.ToMagnifierMatrix();
        float[] id = [1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1];
        Assert.Equal(id, m.Select(v => MathF.Round(v, 5)).ToArray());
    }

    [Fact]
    public void ZeroSaturationIsGray()
    {
        var m = new ColorGrade { Saturation = 0 }.Matrix();
        var (r, g, b) = ColorGrade.Apply(m, 1, 0, 0);
        Assert.Equal(r, g, 6);
        Assert.Equal(g, b, 6);
        Assert.Equal(.2126, r, 3);
    }

    [Fact]
    public void WarmRaisesRedLowersBlue()
    {
        var (r, _, b) = ColorGrade.Apply(new ColorGrade { Temperature = 1 }.Matrix(), .5, .5, .5);
        Assert.True(r > .5 && b < .5);
    }

    [Fact]
    public void MagnifierLayoutIsRowVector()
    {
        // Brightness is a pure offset: it must land in row 4 (the constant row), columns 0..2.
        var m = new ColorGrade { Brightness = .2 }.ToMagnifierMatrix();
        Assert.Equal(.2f, m[20], 4); Assert.Equal(.2f, m[21], 4); Assert.Equal(.2f, m[22], 4);
        Assert.Equal(0f, m[23]); Assert.Equal(1f, m[24]); Assert.Equal(1f, m[18]);
    }

    [Fact]
    public void PresetsHaveUniqueNamesAndOffIsNeutral()
    {
        Assert.Equal(ShaderPresets.All.Count, ShaderPresets.All.Select(p => p.Name).Distinct().Count());
        Assert.True(ShaderPresets.Find("Off")!.Grade.IsNeutral);
        Assert.All(ShaderPresets.All.Skip(1), p => Assert.False(p.Grade.IsNeutral));
    }
}

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
    public void ApplyBacksUpAndDefaultRestores()
    {
        using var t = new TempDir();
        var sky = Skyboxes.SkyFolder(t.Path);
        Directory.CreateDirectory(sky);
        foreach (var f in Skyboxes.Faces) File.WriteAllText(Path.Combine(sky, $"sky512_{f}.tex"), "original-" + f);
        File.SetAttributes(Path.Combine(sky, "sky512_up.tex"), FileAttributes.ReadOnly);
        var cache = t.File("cache");

        Skyboxes.Apply(t.Path, "Midnight", cache);
        Assert.Equal("Midnight", Skyboxes.Current(t.Path, cache));
        Assert.Equal(0x89, File.ReadAllBytes(Path.Combine(sky, "sky512_bk.tex"))[0]);

        Skyboxes.Apply(t.Path, "Aurora", cache);   // switching keeps the first (real) backup
        Skyboxes.Apply(t.Path, Skyboxes.Default, cache);
        Assert.Equal("original-bk", File.ReadAllText(Path.Combine(sky, "sky512_bk.tex")));
        Assert.Equal(Skyboxes.Default, Skyboxes.Current(t.Path, cache));
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

public class ScreenColorFilterWindowsTests
{
    [Fact]
    public void AppliesAndClearsOnWindows()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("ROSETRAP_E2E") != "1") return;
        var grade = ShaderPresets.Find("Noir")!.Grade;
        Assert.True(ScreenColorFilter.Apply(grade), "MagSetFullscreenColorEffect failed");
        var read = ScreenColorFilter.Read();
        Assert.NotNull(read);
        Assert.Equal(grade.ToMagnifierMatrix(), read);
        ScreenColorFilter.Shutdown();
    }
}
