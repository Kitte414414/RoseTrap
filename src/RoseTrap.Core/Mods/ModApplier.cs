using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoseTrap.Core.Mods;

/// <summary>A file to place into a Roblox install: either bytes, or a file to copy.</summary>
public sealed record ModFile(byte[]? Bytes = null, string? Source = null)
{
    public static ModFile Of(byte[] bytes) => new(Bytes: bytes);
    public static ModFile Copy(string path) => new(Source: path);

    internal byte[] Read() => Bytes ?? File.ReadAllBytes(Source!);
}

public sealed record ModApplyResult(int Written, int Restored, int Unchanged);

/// <summary>
/// Places replacement files into a Roblox install and takes them out again cleanly.
/// The first time a file is replaced, Roblox's original goes to <c>.rosetrap-originals</c>;
/// when a mod is turned off, the original comes back (or the file is removed if it was new).
/// A manifest records which files RoseTrap owns, so nothing else in the install is touched.
/// </summary>
public static class ModApplier
{
    public const string ManifestName = ".rosetrap-mods.json";
    public const string OriginalsDir = ".rosetrap-originals";

    /// <param name="files">Install-relative path (either slash) → content. Empty = remove all mods.</param>
    public static ModApplyResult Apply(string installDir, IReadOnlyDictionary<string, ModFile> files)
    {
        var root = Path.GetFullPath(installDir);
        var manifestPath = Path.Combine(root, ManifestName);
        var applied = ReadManifest(manifestPath);
        var wanted = files.ToDictionary(kv => Normalize(kv.Key), kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        int written = 0, restored = 0, unchanged = 0;

        // Take out what's no longer wanted.
        foreach (var rel in applied.Where(r => !wanted.ContainsKey(r)).ToList())
        {
            var target = Resolve(root, rel);
            var original = Resolve(Path.Combine(root, OriginalsDir), rel);
            if (File.Exists(original))
            {
                FileUtil.CopyOver(original, target);
                File.SetAttributes(original, FileAttributes.Normal);
                File.Delete(original);
            }
            else if (File.Exists(target))
            {
                FileUtil.Unlock(target);
                File.Delete(target);
            }
            applied.Remove(rel);
            restored++;
        }

        // Put in what's wanted.
        foreach (var (rel, mod) in wanted)
        {
            var target = Resolve(root, rel);
            var bytes = mod.Read();
            if (!applied.Contains(rel))
            {
                if (File.Exists(target))
                {
                    var original = Resolve(Path.Combine(root, OriginalsDir), rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(original)!);
                    File.Copy(target, original, overwrite: true);
                    File.SetAttributes(original, FileAttributes.Normal);
                }
                applied.Add(rel);
            }
            else if (File.Exists(target) && File.ReadAllBytes(target).AsSpan().SequenceEqual(bytes))
            {
                unchanged++;
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            FileUtil.WriteAtomic(target, bytes);
            written++;
        }

        WriteManifest(manifestPath, applied);
        return new ModApplyResult(written, restored, unchanged);
    }

    /// <summary>Roblox's own version of a file, even while a mod has replaced it.</summary>
    public static byte[]? ReadOriginal(string installDir, string relativePath)
    {
        var root = Path.GetFullPath(installDir);
        var rel = Normalize(relativePath);
        var backup = Resolve(Path.Combine(root, OriginalsDir), rel);
        if (File.Exists(backup)) return File.ReadAllBytes(backup);
        if (ReadManifest(Path.Combine(root, ManifestName)).Contains(rel)) return null; // file was added by a mod
        var path = Resolve(root, rel);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    /// <summary>Install-relative paths RoseTrap has replaced or added.</summary>
    public static IReadOnlyCollection<string> AppliedFiles(string installDir) =>
        ReadManifest(Path.Combine(Path.GetFullPath(installDir), ManifestName));

    static string Normalize(string rel)
    {
        rel = rel.Replace('\\', '/').TrimStart('/');
        if (rel.Length == 0 || rel.Split('/').Any(p => p is ".." or "." or "")) throw new ArgumentException($"Bad mod path '{rel}'.");
        return rel;
    }

    static string Resolve(string root, string rel)
    {
        var full = Path.GetFullPath(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Mod path escapes the install: '{rel}'.");
        return full;
    }

    static HashSet<string> ReadManifest(string path)
    {
        try
        {
            if (File.Exists(path))
                return new HashSet<string>(JsonSerializer.Deserialize(File.ReadAllText(path), ModJson.Default.ListString) ?? [],
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException) { /* treated as empty; originals folder still protects Roblox's files */ }
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    static void WriteManifest(string path, HashSet<string> applied)
    {
        if (applied.Count == 0) { if (File.Exists(path)) File.Delete(path); return; }
        FileUtil.WriteAtomic(path, JsonSerializer.Serialize(applied.Order(StringComparer.OrdinalIgnoreCase).ToList(), ModJson.Default.ListString));
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class ModJson : JsonSerializerContext;
