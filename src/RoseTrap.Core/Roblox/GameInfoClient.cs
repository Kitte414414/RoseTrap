using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoseTrap.Core.Roblox;

public sealed record GameInfo(long PlaceId, long UniverseId, string Name, string Creator, long Playing, string? IconUrl, string? ThumbnailUrl);

/// <summary>Looks up a game's name and pictures through Roblox's public web APIs (no login needed).</summary>
public static class GameInfoClient
{
    /// <param name="images">Also look up the icon and thumbnail URLs (two more requests).</param>
    public static async Task<GameInfo?> GetAsync(HttpClient http, long placeId, bool images = true, CancellationToken ct = default)
    {
        try
        {
            var u = await GetJson(http, $"https://apis.roblox.com/universes/v1/places/{placeId}/universe", ct);
            if (u?.GetProperty("universeId").GetInt64() is not { } universe) return null;

            var games = await GetJson(http, $"https://games.roblox.com/v1/games?universeIds={universe}", ct);
            if (games is not { } g || g.GetProperty("data").GetArrayLength() == 0) return null;
            var game = g.GetProperty("data")[0];

            var icon = !images ? null : await FirstImage(http, $"https://thumbnails.roblox.com/v1/games/icons?universeIds={universe}&size=256x256&format=Png&isCircular=false", nested: false, ct);
            var thumb = !images ? null : await FirstImage(http, $"https://thumbnails.roblox.com/v1/games/multiget/thumbnails?universeIds={universe}&countPerUniverse=1&size=768x432&format=Png", nested: true, ct);

            return new GameInfo(placeId, universe,
                game.GetProperty("name").GetString() ?? "Roblox game",
                game.TryGetProperty("creator", out var c) ? c.GetProperty("name").GetString() ?? "" : "",
                game.TryGetProperty("playing", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt64() : 0,
                icon, thumb);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null; // offline or API change: the play screen simply shows no picture
        }
    }

    static async Task<JsonElement?> GetJson(HttpClient http, string url, CancellationToken ct)
    {
        using var r = await http.GetAsync(url, ct);
        if (!r.IsSuccessStatusCode) return null;
        await using var s = await r.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);
        return doc.RootElement.Clone();
    }

    static async Task<string?> FirstImage(HttpClient http, string url, bool nested, CancellationToken ct)
    {
        var j = await GetJson(http, url, ct);
        if (j is not { } root || root.GetProperty("data").GetArrayLength() == 0) return null;
        var item = root.GetProperty("data")[0];
        if (nested)
        {
            if (item.GetProperty("thumbnails").GetArrayLength() == 0) return null;
            item = item.GetProperty("thumbnails")[0];
        }
        return item.GetProperty("state").GetString() == "Completed" ? item.GetProperty("imageUrl").GetString() : null;
    }
}
