using System.Text;
using RoseTrap.Core.Mods;
using RoseTrap.Core.Settings;

namespace RoseTrap.Core.Tests;

public class ModApplierTests
{
    static string Read(string root, string rel) => File.ReadAllText(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
    static void Write(string root, string rel, string text)
    {
        var p = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, text);
    }
    static Dictionary<string, ModFile> Mods(params (string Path, string Text)[] f) =>
        f.ToDictionary(x => x.Path, x => ModFile.Of(Encoding.UTF8.GetBytes(x.Text)));

    [Fact]
    public void ReplacesBacksUpAndRestores()
    {
        using var t = new TempDir();
        Write(t.Path, "content/sounds/ouch.ogg", "roblox-ouch");
        Write(t.Path, "content/keep.txt", "untouched");
        File.SetAttributes(Path.Combine(t.Path, "content", "sounds", "ouch.ogg"), FileAttributes.ReadOnly);

        var r = ModApplier.Apply(t.Path, Mods(("content/sounds/ouch.ogg", "oof"), ("content/new/added.png", "new")));
        Assert.Equal(2, r.Written);
        Assert.Equal("oof", Read(t.Path, "content/sounds/ouch.ogg"));
        Assert.Equal("roblox-ouch", Encoding.UTF8.GetString(ModApplier.ReadOriginal(t.Path, "content/sounds/ouch.ogg")!));
        Assert.Null(ModApplier.ReadOriginal(t.Path, "content/new/added.png"));

        // Same request again changes nothing; a changed mod overwrites but keeps the real original.
        Assert.Equal(2, ModApplier.Apply(t.Path, Mods(("content/sounds/ouch.ogg", "oof"), ("content/new/added.png", "new"))).Unchanged);
        ModApplier.Apply(t.Path, Mods(("content/sounds/ouch.ogg", "other")));
        Assert.False(File.Exists(Path.Combine(t.Path, "content", "new", "added.png"))); // added file removed when no longer wanted
        Assert.Equal("other", Read(t.Path, "content/sounds/ouch.ogg"));

        var off = ModApplier.Apply(t.Path, new Dictionary<string, ModFile>());
        Assert.Equal(1, off.Restored);
        Assert.Equal("roblox-ouch", Read(t.Path, "content/sounds/ouch.ogg"));
        Assert.Equal("untouched", Read(t.Path, "content/keep.txt"));
        Assert.Empty(ModApplier.AppliedFiles(t.Path));
        Assert.False(File.Exists(Path.Combine(t.Path, ModApplier.ManifestName)));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("content/../../escape.txt")]
    [InlineData("")]
    public void RejectsPathsOutsideTheInstall(string rel)
    {
        using var t = new TempDir();
        Assert.Throws<ArgumentException>(() => ModApplier.Apply(t.Path, Mods((rel, "x"))));
    }
}

public class CursorArtTests
{
    static (int W, int H, byte Type) Header(byte[] png) =>
        ((png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19], (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23], png[25]);

    [Fact]
    public void EveryStyleRendersRgbaAtRobloxSizes()
    {
        foreach (var c in CursorArt.Cursors.Skip(1))
        {
            Assert.Equal((64, 64, (byte)6), Header(CursorArt.Pointer(c.Name)));
            Assert.Equal((64, 64, (byte)6), Header(CursorArt.Hover(c.Name)));
        }
        foreach (var s in CursorArt.ShiftLocks.Skip(1))
            Assert.Equal((32, 32, (byte)6), Header(CursorArt.ShiftLock(s.Name)));
    }

    [Fact]
    public void ArrowTipSitsOnTheClickPoint()
    {
        // Roblox's click point is the centre of the 64×64 image; the arrow tip must be drawn there
        // and nothing above-left of it (apart from the soft outline).
        var px = CursorArt.PointerRgba("Classic", hover: false);
        byte A(int x, int y) => px[(y * 64 + x) * 4 + 3];
        Assert.True(A(33, 34) > 200);
        Assert.Equal(0, A(28, 28));
        Assert.Equal(0, A(0, 0));
    }
}

public class ModSetTests
{
    [Fact]
    public void BuildsCursorSoundFontSkyAndModFolder()
    {
        using var t = new TempDir();
        var install = t.File("version-x");
        var paths = new AppPaths(t.File("lad"));
        void Put(string rel, string text)
        {
            var p = Path.Combine(install, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, text);
        }
        Put("content/sounds/oof.ogg", "classic-oof");
        Put("content/fonts/families/BuilderSans.json",
            """{"name":"Builder Sans","faces":[{"name":"Regular","weight":400,"style":"normal","assetId":"rbxasset://fonts/BuilderSans-Regular.otf"},{"name":"Bold","weight":700,"style":"normal","assetId":"rbxassetid://123"}]}""");
        var font = t.File("MyFont.ttf"); File.WriteAllText(font, "ttf");
        Directory.CreateDirectory(Path.Combine(paths.Modifications, "content", "textures"));
        File.WriteAllText(Path.Combine(paths.Modifications, "content", "textures", "custom.png"), "png");

        var s = new AppSettings
        {
            Shaders = { Skybox = "Aurora" },
            Mods = { Cursor = "Rose", ShiftLock = "Dot", DeathSound = "Classic oof", FontFile = font },
        };
        var r = ModSet.Build(s, install, paths);
        Assert.Empty(r.Warnings);
        Assert.Contains(ModSet.PointerPath, r.Files.Keys);
        Assert.Contains(ModSet.HoverPath, r.Files.Keys);
        Assert.Contains(ModSet.ShiftLockPath, r.Files.Keys);
        Assert.Equal("classic-oof", Encoding.UTF8.GetString(r.Files[ModSet.DeathSoundPath].Bytes!));
        Assert.Contains("content/fonts/CustomFont.ttf", r.Files.Keys);
        var family = Encoding.UTF8.GetString(r.Files["content/fonts/families/BuilderSans.json"].Bytes!);
        Assert.Equal(2, family.Split("rbxasset://fonts/CustomFont.ttf").Length - 1);
        Assert.Equal(6, r.Files.Keys.Count(k => k.Contains("/sky/")));
        Assert.Contains("content/textures/custom.png", r.Files.Keys);

        // Applying, then rebuilding, still reads the *original* font family (not our rewrite).
        ModApplier.Apply(install, r.Files);
        var again = ModSet.Build(s, install, paths);
        Assert.Equal(r.Files["content/fonts/families/BuilderSans.json"].Bytes, again.Files["content/fonts/families/BuilderSans.json"].Bytes);

        // Missing custom files become warnings, not crashes.
        s.Mods.DeathSound = "Custom file"; s.Mods.DeathSoundFile = t.File("nope.ogg"); s.Mods.FontFile = t.File("nope.ttf");
        Assert.Equal(2, ModSet.Build(s, install, paths).Warnings.Count);
    }
}
