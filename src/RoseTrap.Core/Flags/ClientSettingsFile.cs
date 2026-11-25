using System.Text.Json;
using System.Text.Json.Nodes;

namespace RoseTrap.Core.Flags;

public sealed record ApplyResult(IReadOnlyList<string> Files, int Written, int Removed);

/// <summary>
/// Reads and writes ClientAppSettings.json. Flags RoseTrap doesn't own are left untouched,
/// so hand-edited entries survive.
/// </summary>
public static class ClientSettingsFile
{
    public const string FileName = "ClientAppSettings.json";
    public const string BackupSuffix = ".rosetrap.bak";
    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string ForVersion(string versionDir) => Path.Combine(versionDir, "ClientSettings", FileName);

    public static IReadOnlyList<string> Targets(string? versionDir, string? globalFile)
    {
        var list = new List<string>(2);
        if (!string.IsNullOrEmpty(versionDir)) list.Add(Path.GetFullPath(ForVersion(versionDir)));
        if (!string.IsNullOrEmpty(globalFile))
        {
            var full = Path.GetFullPath(globalFile);
            if (!list.Contains(full, StringComparer.OrdinalIgnoreCase)) list.Add(full);
        }
        return list;
    }

    public static JsonObject Read(string path)
    {
        if (!File.Exists(path)) return new JsonObject();
        try
        {
            return JsonNode.Parse(File.ReadAllText(path),
                       documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip })
                   as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject(); // a corrupt file is replaced, its backup is kept
        }
    }

    public static ApplyResult Apply(FlagPlan plan, IReadOnlyList<string> targets)
    {
        int written = 0, removed = 0;
        foreach (var path in targets)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            Backup(path);
            var json = Read(path);
            written = removed = 0;

            foreach (var name in plan.Remove)
                if (json.Remove(name)) removed++;

            foreach (var (name, value) in plan.Set)
            {
                // Roblox reads every value as a string; writing them that way avoids type surprises.
                json[name] = value;
                written++;
            }

            FileUtil.WriteAtomic(path, json.ToJsonString(Indented));
        }
        return new ApplyResult(targets, written, removed);
    }

    /// <summary>The first time RoseTrap touches a file, keep the user's original.</summary>
    static void Backup(string path)
    {
        var bak = path + BackupSuffix;
        if (File.Exists(path) && !File.Exists(bak)) File.Copy(path, bak);
    }

    /// <summary>Puts back the files as they were before RoseTrap first changed them.</summary>
    public static int Restore(IReadOnlyList<string> targets)
    {
        int restored = 0;
        foreach (var path in targets)
        {
            var bak = path + BackupSuffix;
            if (!File.Exists(bak)) continue;
            FileUtil.CopyOver(bak, path);
            File.Delete(bak);
            restored++;
        }
        return restored;
    }

    public static bool HasBackup(IReadOnlyList<string> targets) => targets.Any(t => File.Exists(t + BackupSuffix));
}
