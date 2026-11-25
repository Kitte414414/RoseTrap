using System.Runtime.Versioning;
using Microsoft.Win32;

namespace RoseTrap.Core.Roblox;

/// <summary>Points roblox:// and roblox-player: links at RoseTrap (per-user, no admin needed).</summary>
[SupportedOSPlatform("windows")]
public static class ProtocolRegistrar
{
    public static void Register(string handlerExe, string iconExe)
    {
        foreach (var scheme in LaunchUri.Schemes)
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{scheme}");
            key.SetValue("", $"URL: {scheme} Protocol");
            key.SetValue("URL Protocol", "");
            using (var icon = key.CreateSubKey("DefaultIcon")) icon.SetValue("", iconExe);
            using var cmd = key.CreateSubKey(@"shell\open\command");
            cmd.SetValue("", $"\"{handlerExe}\" \"%1\"");
        }
    }

    public static bool IsRegisteredTo(string handlerExe)
    {
        using var cmd = Registry.CurrentUser.OpenSubKey(@"Software\Classes\roblox-player\shell\open\command");
        return cmd?.GetValue("") is string s && s.Contains(handlerExe, StringComparison.OrdinalIgnoreCase);
    }
}
