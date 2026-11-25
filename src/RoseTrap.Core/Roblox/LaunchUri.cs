using System.Text.RegularExpressions;

namespace RoseTrap.Core.Roblox;

/// <summary>Recognises the links the website opens Roblox with.</summary>
public static partial class LaunchUri
{
    public static readonly string[] Schemes = ["roblox-player", "roblox"];

    public static bool IsRobloxLink(string? arg) =>
        !string.IsNullOrWhiteSpace(arg) &&
        Schemes.Any(s => arg.StartsWith(s + ":", StringComparison.OrdinalIgnoreCase));

    public static string? FindInArgs(IEnumerable<string> args) => args.FirstOrDefault(IsRobloxLink);

    /// <summary>Place id from either "roblox-player:1+launchmode:play+...placeId%3D123" or "roblox://placeId=123".</summary>
    public static long? PlaceId(string link)
    {
        var m = PlaceIdRegex().Match(Uri.UnescapeDataString(link));
        return m.Success && long.TryParse(m.Groups[1].Value, out var id) ? id : null;
    }

    [GeneratedRegex(@"placeId=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceIdRegex();
}
