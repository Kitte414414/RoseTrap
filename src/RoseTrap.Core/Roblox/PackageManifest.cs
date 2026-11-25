using System.Globalization;

namespace RoseTrap.Core.Roblox;

public sealed record Package(string Name, string Md5, long PackedSize, long Size);

/// <summary>Parses {version}-rbxPkgManifest.txt: "v0", then four lines per package.</summary>
public static class PackageManifest
{
    /// <summary>Where each player package is extracted, relative to the version folder.</summary>
    public static readonly IReadOnlyDictionary<string, string> Folders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["RobloxApp.zip"] = "",
        ["redist.zip"] = "",
        ["WebView2.zip"] = "",
        ["shaders.zip"] = "shaders",
        ["ssl.zip"] = "ssl",
        ["WebView2RuntimeInstaller.zip"] = "WebView2RuntimeInstaller",
        ["content-avatar.zip"] = "content/avatar",
        ["content-configs.zip"] = "content/configs",
        ["content-fonts.zip"] = "content/fonts",
        ["content-sky.zip"] = "content/sky",
        ["content-sounds.zip"] = "content/sounds",
        ["content-textures2.zip"] = "content/textures",
        ["content-models.zip"] = "content/models",
        ["content-platform-fonts.zip"] = "PlatformContent/pc/fonts",
        ["content-platform-dictionaries.zip"] = "PlatformContent/pc/shared_compression_dictionaries",
        ["content-terrain.zip"] = "PlatformContent/pc/terrain",
        ["content-textures3.zip"] = "PlatformContent/pc/textures",
        ["extracontent-luapackages.zip"] = "ExtraContent/LuaPackages",
        ["extracontent-translations.zip"] = "ExtraContent/translations",
        ["extracontent-models.zip"] = "ExtraContent/models",
        ["extracontent-textures.zip"] = "ExtraContent/textures",
        ["extracontent-places.zip"] = "ExtraContent/places",
    };

    public static IReadOnlyList<Package> Parse(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0 || lines[0] != "v0")
            throw new FormatException($"Unknown package manifest version '{lines.FirstOrDefault()}'.");

        if ((lines.Length - 1) % 4 != 0)
            throw new FormatException("Package manifest is truncated.");

        var list = new List<Package>((lines.Length - 1) / 4);
        for (int i = 1; i < lines.Length; i += 4)
        {
            list.Add(new Package(lines[i], lines[i + 1],
                long.Parse(lines[i + 2], CultureInfo.InvariantCulture),
                long.Parse(lines[i + 3], CultureInfo.InvariantCulture)));
        }
        return list;
    }
}
