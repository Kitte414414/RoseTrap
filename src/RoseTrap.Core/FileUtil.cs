namespace RoseTrap.Core;

internal static class FileUtil
{
    /// <summary>
    /// Write via a temp file so a crash never leaves a half-written file behind.
    /// Read-only targets (people lock Roblox's settings so the game can't reset them)
    /// are unlocked for the write and locked again afterwards.
    /// </summary>
    public static void WriteAtomic(string path, string contents)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, contents);
        bool locked = Unlock(path);
        try
        {
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            if (locked) Lock(path);
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    /// <summary>File.Copy that also works when the destination is read-only; keeps the lock.</summary>
    public static void CopyOver(string from, string to)
    {
        bool locked = Unlock(to);
        try { File.Copy(from, to, overwrite: true); }
        finally { if (locked) Lock(to); }
    }

    /// <summary>Clears the read-only flag. Returns true if it was set.</summary>
    public static bool Unlock(string path)
    {
        if (!File.Exists(path)) return false;
        var attrs = File.GetAttributes(path);
        if (!attrs.HasFlag(FileAttributes.ReadOnly)) return false;
        File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
        return true;
    }

    static void Lock(string path)
    {
        if (File.Exists(path)) File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
    }

    /// <summary>Retries briefly: antivirus scanners hold freshly written files open for a moment.</summary>
    public static void Retry(Action action, int attempts = 5)
    {
        for (int i = 1; ; i++)
        {
            try { action(); return; }
            catch (Exception e) when (i < attempts && e is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(150 * i);
            }
        }
    }
}
