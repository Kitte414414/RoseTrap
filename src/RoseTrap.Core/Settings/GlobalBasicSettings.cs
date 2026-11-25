using System.Globalization;
using System.Xml.Linq;

namespace RoseTrap.Core.Settings;

/// <summary>
/// Edits Roblox's own saved settings (GlobalBasicSettings_13.xml). The frame-rate cap and
/// quality level live here; since the flag allowlist this is the only way to set them.
/// </summary>
public static class GlobalBasicSettings
{
    /// <summary>Roblox's "unlimited" is just a very high cap.</summary>
    public const int Unlimited = 9999;

    /// <returns>False when Roblox hasn't created the file yet (it does on first run).</returns>
    public static bool Apply(string path, GraphicsSettings g)
    {
        if (!File.Exists(path)) return false;

        var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var props = doc.Descendants("Item")
            .FirstOrDefault(i => (string?)i.Attribute("class") == "UserGameSettings")
            ?.Element("Properties");
        if (props is null) return false;

        if (g.FpsCap > 0) Set(props, "int", "FramerateCap", Math.Clamp(g.FpsCap, 1, Unlimited));

        var quality = Flags.FlagComposer.Effective(g).Quality;
        Set(props, "token", "SavedQualityLevel", Math.Clamp(quality, 0, 10));

        FileUtil.WriteAtomic(path, doc.Declaration + doc.ToString(SaveOptions.DisableFormatting));
        return true;
    }

    public static int? ReadInt(string path, string name)
    {
        if (!File.Exists(path)) return null;
        var el = XDocument.Load(path).Descendants().FirstOrDefault(e => (string?)e.Attribute("name") == name);
        return int.TryParse(el?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    static void Set(XElement props, string kind, string name, int value)
    {
        var el = props.Elements().FirstOrDefault(e => (string?)e.Attribute("name") == name);
        if (el is null)
        {
            el = new XElement(kind, new XAttribute("name", name));
            props.Add(el);
        }
        el.Value = value.ToString(CultureInfo.InvariantCulture);
    }
}
