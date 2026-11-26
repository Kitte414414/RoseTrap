using System.Text.RegularExpressions;

namespace RoseTrap.Core.Roblox;

public enum JoinStage { Starting, Joining, Connected, Loaded, Left }

public sealed record JoinEvent(JoinStage Stage, long? PlaceId = null, string? Server = null);

/// <summary>
/// Follows Roblox's own log file to see how far a join has got (the same lines Bloxstrap reads).
/// Reading the log is passive: nothing is changed in Roblox.
/// </summary>
public static partial class RobloxLog
{
    public const string JoiningEntry = "[FLog::Output] ! Joining game";
    public const string LoadTimeEntry = "[FLog::GameJoinLoadTime] Report game_join_loadtime:";
    public const string ReplicatorEntry = "[FLog::Network] Replicator created:";
    public const string LeavingEntry = "[FLog::SingleSurfaceApp] leaveUGCGameInternal";
    public const string DisconnectEntry = "[FLog::Network] Time to disconnect replication data:";

    [GeneratedRegex(@"! Joining game '([0-9a-f\-]{36})' place ([0-9]+) at ([0-9\.]+)")]
    private static partial Regex JoiningRegex();

    /// <summary>Parses one log line; null for lines that aren't join milestones.</summary>
    public static JoinEvent? Parse(string line)
    {
        if (line.Contains(JoiningEntry, StringComparison.Ordinal))
        {
            var m = JoiningRegex().Match(line);
            return m.Success
                ? new JoinEvent(JoinStage.Joining, long.Parse(m.Groups[2].Value), m.Groups[3].Value)
                : new JoinEvent(JoinStage.Joining);
        }
        if (line.Contains(ReplicatorEntry, StringComparison.Ordinal)) return new JoinEvent(JoinStage.Connected);
        if (line.Contains(LoadTimeEntry, StringComparison.Ordinal)) return new JoinEvent(JoinStage.Loaded);
        if (line.Contains(LeavingEntry, StringComparison.Ordinal) || line.Contains(DisconnectEntry, StringComparison.Ordinal))
            return new JoinEvent(JoinStage.Left);
        return null;
    }

    /// <summary>The newest player log created at or after <paramref name="since"/>.</summary>
    public static string? FindLog(string logsDir, DateTime since)
    {
        if (!Directory.Exists(logsDir)) return null;
        return new DirectoryInfo(logsDir).EnumerateFiles("*.log")
            .Where(f => f.Name.Contains("Player", StringComparison.OrdinalIgnoreCase) && f.CreationTimeUtc >= since.ToUniversalTime().AddSeconds(-2))
            .OrderByDescending(f => f.CreationTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    /// <summary>
    /// Tails the log of the Roblox started at <paramref name="launchedAt"/> and reports join milestones
    /// until <paramref name="ct"/> is cancelled.
    /// </summary>
    public static async Task WatchAsync(string logsDir, DateTime launchedAt, Action<JoinEvent> onEvent, CancellationToken ct)
    {
        string? path = null;
        while (path is null)
        {
            path = FindLog(logsDir, launchedAt);
            if (path is null) await Task.Delay(400, ct);
        }

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fs);
        var partial = "";
        while (!ct.IsCancellationRequested)
        {
            var chunk = await reader.ReadToEndAsync(ct);
            if (chunk.Length == 0) { await Task.Delay(250, ct); continue; }
            var text = partial + chunk;
            int last = text.LastIndexOf('\n');
            partial = last < 0 ? text : text[(last + 1)..];
            if (last < 0) continue;
            foreach (var line in text[..last].Split('\n'))
                if (Parse(line) is { } e) onEvent(e);
        }
    }
}
