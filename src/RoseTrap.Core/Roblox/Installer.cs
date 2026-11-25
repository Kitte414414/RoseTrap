using System.IO.Compression;
using System.Security.Cryptography;

namespace RoseTrap.Core.Roblox;

public sealed record InstallProgress(string Stage, double Fraction);

public sealed record RobloxInstall(string Version, string Directory, bool Official)
{
    public string Executable => Path.Combine(Directory, Installer.PlayerExe);
}

/// <summary>Keeps an up-to-date copy of the Roblox player in %LocalAppData%\RoseTrap\Versions.</summary>
public sealed class Installer(HttpClient http, AppPaths paths)
{
    public const string PlayerExe = "RobloxPlayerBeta.exe";
    const string AppSettingsXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Settings>\r\n\t<ContentFolder>content</ContentFolder>\r\n\t<BaseUrl>http://www.roblox.com</BaseUrl>\r\n</Settings>\r\n";

    /// <summary>Newest complete install RoseTrap or the official launcher has, without touching the network.</summary>
    public RobloxInstall? FindLocal()
    {
        static IEnumerable<RobloxInstall> Scan(string root, bool official) =>
            Directory.Exists(root)
                ? new DirectoryInfo(root).EnumerateDirectories("version-*")
                    .Where(d => File.Exists(Path.Combine(d.FullName, PlayerExe)))
                    .OrderByDescending(d => File.GetLastWriteTimeUtc(Path.Combine(d.FullName, PlayerExe)))
                    .Select(d => new RobloxInstall(d.Name, d.FullName, official))
                : [];

        return Scan(paths.Versions, false).FirstOrDefault() ?? Scan(paths.OfficialVersions, true).FirstOrDefault();
    }

    public async Task<RobloxInstall> EnsureLatestAsync(string channel, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        progress?.Report(new("Checking for updates", 0));
        ClientVersion latest;
        try
        {
            latest = await Deployment.GetLatestAsync(http, channel, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Offline: any working install beats none.
            return FindLocal() ?? throw new InvalidOperationException("Can't reach Roblox and no install was found.", e);
        }

        var dir = Path.Combine(paths.Versions, latest.ClientVersionUpload);
        if (File.Exists(Path.Combine(dir, PlayerExe)) && File.Exists(Path.Combine(dir, "AppSettings.xml")))
            return new RobloxInstall(latest.ClientVersionUpload, dir, false);

        await InstallAsync(channel, latest.ClientVersionUpload, dir, progress, ct);
        RemoveOldVersions(keep: latest.ClientVersionUpload);
        return new RobloxInstall(latest.ClientVersionUpload, dir, false);
    }

    async Task InstallAsync(string channel, string version, string dir, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        progress?.Report(new($"Downloading {version}", 0));
        var manifest = PackageManifest.Parse(
            await http.GetStringAsync(Deployment.PackageUrl(channel, version, "rbxPkgManifest.txt"), ct));
        var packages = manifest.Where(p => PackageManifest.Folders.ContainsKey(p.Name)).ToList();

        // Extract into a staging folder and swap it in at the end, so a failed update never breaks the old one.
        var staging = dir + ".partial";
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(paths.Downloads);

        long total = packages.Sum(p => p.PackedSize), done = 0;
        using var gate = new SemaphoreSlim(4);
        await Task.WhenAll(packages.Select(async p =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var zip = await DownloadAsync(channel, version, p, ct);
                ExtractPackage(zip, Path.Combine(staging, PackageManifest.Folders[p.Name]));
                var now = Interlocked.Add(ref done, p.PackedSize);
                progress?.Report(new($"Installing {version}", total == 0 ? 1 : (double)now / total));
            }
            finally { gate.Release(); }
        }));

        await File.WriteAllTextAsync(Path.Combine(staging, "AppSettings.xml"), AppSettingsXml, ct);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Directory.Move(staging, dir);
        progress?.Report(new("Ready", 1));
    }

    async Task<string> DownloadAsync(string channel, string version, Package p, CancellationToken ct)
    {
        // Packages are content-addressed by MD5, so a cached file can be reused across versions.
        var path = Path.Combine(paths.Downloads, p.Md5);
        if (File.Exists(path) && await Md5Async(path, ct) == p.Md5) return path;

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await using (var src = await http.GetStreamAsync(Deployment.PackageUrl(channel, version, p.Name), ct))
                await using (var dst = File.Create(path))
                    await src.CopyToAsync(dst, ct);

                if (await Md5Async(path, ct) == p.Md5) return path;
                throw new InvalidDataException($"{p.Name} failed its checksum.");
            }
            catch (Exception) when (attempt < 3 && !ct.IsCancellationRequested)
            {
                await Task.Delay(500 * attempt, ct);
            }
        }
    }

    static async Task<string> Md5Async(string path, CancellationToken ct)
    {
        await using var s = File.OpenRead(path);
        return Convert.ToHexString(await MD5.HashDataAsync(s, ct)).ToLowerInvariant();
    }

    internal static void ExtractPackage(string zipPath, string destination)
    {
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry
            var target = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('\\', '/')));
            if (!target.StartsWith(root, StringComparison.Ordinal)) continue; // zip-slip guard
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    void RemoveOldVersions(string keep)
    {
        foreach (var d in new DirectoryInfo(paths.Versions).EnumerateDirectories("version-*"))
        {
            if (d.Name == keep) continue;
            try { d.Delete(recursive: true); }
            catch (IOException) { /* still running; cleaned up next time */ }
            catch (UnauthorizedAccessException) { }
        }
    }
}
