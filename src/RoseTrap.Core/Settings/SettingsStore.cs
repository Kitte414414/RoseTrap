using System.Text.Json;

namespace RoseTrap.Core.Settings;

public static class SettingsStore
{
    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize(File.ReadAllText(path), CoreJson.Default.AppSettings) ?? new();
        }
        catch (JsonException)
        {
            // Keep the broken file for inspection instead of silently overwriting it.
            File.Copy(path, path + ".broken", overwrite: true);
        }
        return new();
    }

    public static void Save(string path, AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        FileUtil.WriteAtomic(path, JsonSerializer.Serialize(settings, CoreJson.Default.AppSettings));
    }
}
