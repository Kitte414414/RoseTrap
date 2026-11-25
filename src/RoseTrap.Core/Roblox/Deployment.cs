using System.Net.Http.Json;

namespace RoseTrap.Core.Roblox;

public sealed record ClientVersion(string Version, string ClientVersionUpload);

public static class Deployment
{
    public const string SetupCdn = "https://setup.rbxcdn.com";

    public static string VersionUrl(string channel) =>
        IsLive(channel)
            ? "https://clientsettings.roblox.com/v2/client-version/WindowsPlayer"
            : $"https://clientsettings.roblox.com/v2/client-version/WindowsPlayer/channel/{Uri.EscapeDataString(channel)}";

    public static string PackageUrl(string channel, string version, string file) =>
        IsLive(channel)
            ? $"{SetupCdn}/{version}-{file}"
            : $"{SetupCdn}/channel/{Uri.EscapeDataString(channel.ToLowerInvariant())}/{version}-{file}";

    static bool IsLive(string channel) =>
        string.IsNullOrWhiteSpace(channel) || channel.Equals("LIVE", StringComparison.OrdinalIgnoreCase)
                                            || channel.Equals("production", StringComparison.OrdinalIgnoreCase);

    public static async Task<ClientVersion> GetLatestAsync(HttpClient http, string channel, CancellationToken ct = default)
    {
        var v = await http.GetFromJsonAsync(VersionUrl(channel), CoreJson.Default.ClientVersion, ct)
                ?? throw new InvalidOperationException("Roblox returned an empty version response.");
        if (string.IsNullOrWhiteSpace(v.ClientVersionUpload) || !v.ClientVersionUpload.StartsWith("version-", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected Roblox version '{v.ClientVersionUpload}'.");
        return v;
    }
}
