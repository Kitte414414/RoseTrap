using System.Text.Json;
using System.Text.Json.Nodes;
using RoseTrap.Core.Settings;
using RoseTrap.Core.Shaders;

namespace RoseTrap.Core.Mods;

/// <summary>Turns the Mods and Shaders settings into the exact files to place into an install.</summary>
public static class ModSet
{
    public const string PointerPath = "content/textures/Cursors/KeyboardMouse/ArrowFarCursor.png";
    public const string HoverPath = "content/textures/Cursors/KeyboardMouse/ArrowCursor.png";
    public const string ShiftLockPath = "content/textures/MouseLockedCursor.png";
    public const string DeathSoundPath = "content/sounds/ouch.ogg";
    public const string ClassicOofPath = "content/sounds/oof.ogg"; // Roblox still ships the classic sound
    const string FamiliesDir = "content/fonts/families";

    public static readonly string[] DeathSounds = ["Default", "Classic oof", "Custom file"];
    public static readonly string[] SoundExtensions = [".ogg", ".mp3", ".wav"];
    public static readonly string[] FontExtensions = [".ttf", ".otf"];

    public sealed record Result(Dictionary<string, ModFile> Files, List<string> Warnings);

    public static Result Build(AppSettings s, string installDir, AppPaths paths)
    {
        var files = new Dictionary<string, ModFile>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var m = s.Mods;

        foreach (var (path, source) in Skyboxes.Files(s.Shaders.Skybox, paths.SkyboxCache))
            files[path] = ModFile.Copy(source);

        if (m.Cursor != CursorArt.Default && CursorArt.Cursors.Any(c => c.Name == m.Cursor))
        {
            files[PointerPath] = ModFile.Of(CursorArt.Pointer(m.Cursor));
            files[HoverPath] = ModFile.Of(CursorArt.Hover(m.Cursor));
        }

        if (m.ShiftLock != CursorArt.Default && CursorArt.ShiftLocks.Any(c => c.Name == m.ShiftLock))
            files[ShiftLockPath] = ModFile.Of(CursorArt.ShiftLock(m.ShiftLock));

        switch (m.DeathSound)
        {
            case "Classic oof":
                if (ModApplier.ReadOriginal(installDir, ClassicOofPath) is { } oof) files[DeathSoundPath] = ModFile.Of(oof);
                else warnings.Add("This Roblox version doesn't include the classic oof sound.");
                break;
            case "Custom file":
                if (IsUsable(m.DeathSoundFile, SoundExtensions)) files[DeathSoundPath] = ModFile.Copy(m.DeathSoundFile!);
                else if (!string.IsNullOrEmpty(m.DeathSoundFile)) warnings.Add($"Death sound file not found: {m.DeathSoundFile}");
                break;
        }

        if (IsUsable(m.FontFile, FontExtensions))
            AddFont(files, installDir, m.FontFile!);
        else if (!string.IsNullOrEmpty(m.FontFile))
            warnings.Add($"Font file not found: {m.FontFile}");

        // The mod folder goes last so hand-made mods win over the built-in options.
        if (m.UseModFolder && Directory.Exists(paths.Modifications))
            foreach (var file in Directory.EnumerateFiles(paths.Modifications, "*", SearchOption.AllDirectories))
                files[Path.GetRelativePath(paths.Modifications, file).Replace('\\', '/')] = ModFile.Copy(file);

        return new Result(files, warnings);
    }

    static bool IsUsable(string? path, string[] extensions) =>
        !string.IsNullOrEmpty(path) && File.Exists(path) && extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>
    /// Puts the font in as content/fonts/CustomFont.* and points every face of every font family at it,
    /// so all text in Roblox's UI and in games that use built-in fonts switches over.
    /// </summary>
    static void AddFont(Dictionary<string, ModFile> files, string installDir, string fontFile)
    {
        var ext = Path.GetExtension(fontFile).ToLowerInvariant();
        files["content/fonts/CustomFont" + ext] = ModFile.Copy(fontFile);
        var asset = "rbxasset://fonts/CustomFont" + ext;

        var dir = Path.Combine(installDir, "content", "fonts", "families");
        if (!Directory.Exists(dir)) return;
        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            var rel = $"{FamiliesDir}/{Path.GetFileName(path)}";
            var original = ModApplier.ReadOriginal(installDir, rel);
            if (original is null) continue;
            if (RewriteFamily(original, asset) is { } rewritten) files[rel] = ModFile.Of(rewritten);
        }
    }

    internal static byte[]? RewriteFamily(byte[] json, string asset)
    {
        try
        {
            if (JsonNode.Parse(json) is not JsonObject family || family["faces"] is not JsonArray faces) return null;
            foreach (var face in faces.OfType<JsonObject>()) face["assetId"] = asset;
            return System.Text.Encoding.UTF8.GetBytes(family.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (JsonException) { return null; }
    }
}
