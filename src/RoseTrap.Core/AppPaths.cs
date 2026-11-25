namespace RoseTrap.Core;

/// <summary>Every file location RoseTrap reads or writes, in one place.</summary>
public sealed class AppPaths
{
    public AppPaths(string localAppData)
    {
        LocalAppData = localAppData;
        Root = Path.Combine(localAppData, "RoseTrap");
        Versions = Path.Combine(Root, "Versions");
        Downloads = Path.Combine(Root, "Downloads");
        SettingsFile = Path.Combine(Root, "settings.json");
        RobloxRoot = Path.Combine(localAppData, "Roblox");
        GlobalClientSettings = Path.Combine(RobloxRoot, "ClientSettings", Flags.ClientSettingsFile.FileName);
        GlobalBasicSettings = Path.Combine(RobloxRoot, "GlobalBasicSettings_13.xml");
        OfficialVersions = Path.Combine(RobloxRoot, "Versions");
    }

    public static AppPaths Default { get; } =
        new(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public string LocalAppData { get; }
    public string Root { get; }
    public string Versions { get; }
    public string Downloads { get; }
    public string SettingsFile { get; }
    public string RobloxRoot { get; }
    public string GlobalClientSettings { get; }
    public string GlobalBasicSettings { get; }
    public string OfficialVersions { get; }
}
