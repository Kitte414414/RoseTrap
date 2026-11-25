namespace RoseTrap.Core;

internal static class FileUtil
{
    /// <summary>Write via a temp file so a crash never leaves a half-written file behind.</summary>
    public static void WriteAtomic(string path, string contents)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, contents);
        File.Move(tmp, path, overwrite: true);
    }
}
