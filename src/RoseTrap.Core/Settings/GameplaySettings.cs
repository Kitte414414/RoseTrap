using System.Globalization;
using System.Xml.Linq;

namespace RoseTrap.Core.Settings;

public enum CameraMovement { Default = 0, Classic = 1, Follow = 2, CameraToggle = 4 }
public enum MovementMode { Default = 0, KeyboardMouse = 1, ClickToMove = 2 }

/// <summary>
/// The player's own in-game settings (the ones in Roblox's Esc menu), as stored by Roblox in
/// GlobalBasicSettings_13.xml. RoseTrap edits Roblox's file directly, so changes made inside
/// Roblox show up here too.
/// </summary>
public sealed record GameplaySettings
{
    /// <summary>Camera sensitivity, 0.1–4 (Roblox's menu shows the same number).</summary>
    public double MouseSensitivity { get; init; } = 1;
    public bool InvertCamera { get; init; }
    public CameraMovement CameraMode { get; init; }
    public MovementMode Movement { get; init; }
    /// <summary>0–1.</summary>
    public double Volume { get; init; } = 1;
    public bool Fullscreen { get; init; }
    public bool PerformanceStats { get; init; }
    public bool ReducedMotion { get; init; }
    /// <summary>Menu/chat background transparency, 0–1.</summary>
    public double UiTransparency { get; init; } = .3;
}

public static class GameplayStore
{
    /// <summary>Null when Roblox hasn't created its settings file yet (it does on first start).</summary>
    public static GameplaySettings? Read(string path)
    {
        if (!File.Exists(path)) return null;
        var props = Properties(XDocument.Load(path));
        if (props is null) return null;
        var d = new GameplaySettings();
        return new GameplaySettings
        {
            MouseSensitivity = Num(props, "MouseSensitivity") ?? d.MouseSensitivity,
            InvertCamera = Bool(props, "CameraYInverted") ?? d.InvertCamera,
            CameraMode = Enum.IsDefined(typeof(CameraMovement), (int)(Num(props, "ComputerCameraMovementMode") ?? 0))
                ? (CameraMovement)(int)(Num(props, "ComputerCameraMovementMode") ?? 0) : CameraMovement.Default,
            Movement = (MovementMode)Math.Clamp((int)(Num(props, "ComputerMovementMode") ?? 0), 0, 2),
            Volume = Num(props, "MasterVolume") ?? d.Volume,
            Fullscreen = Bool(props, "Fullscreen") ?? d.Fullscreen,
            PerformanceStats = Bool(props, "PerformanceStatsVisible") ?? d.PerformanceStats,
            ReducedMotion = Bool(props, "ReducedMotion") ?? d.ReducedMotion,
            UiTransparency = Num(props, "PreferredTransparency") ?? d.UiTransparency,
        };
    }

    /// <returns>False when Roblox hasn't created the file yet.</returns>
    public static bool Write(string path, GameplaySettings g)
    {
        if (!File.Exists(path)) return false;
        var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var props = Properties(doc);
        if (props is null) return false;

        double sens = Math.Clamp(g.MouseSensitivity, .1, 4);
        Set(props, "float", "MouseSensitivity", F(sens));
        // Roblox also keeps per-camera sensitivities; keep them in step with the main slider.
        SetVector(props, "MouseSensitivityFirstPerson", sens);
        SetVector(props, "MouseSensitivityThirdPerson", sens);
        Set(props, "bool", "CameraYInverted", B(g.InvertCamera));
        Set(props, "token", "ComputerCameraMovementMode", ((int)g.CameraMode).ToString(CultureInfo.InvariantCulture));
        Set(props, "bool", "ComputerCameraMovementChanged", B(g.CameraMode != CameraMovement.Default));
        Set(props, "token", "ComputerMovementMode", ((int)g.Movement).ToString(CultureInfo.InvariantCulture));
        Set(props, "bool", "ComputerMovementChanged", B(g.Movement != MovementMode.Default));
        Set(props, "float", "MasterVolume", F(Math.Clamp(g.Volume, 0, 1)));
        Set(props, "bool", "Fullscreen", B(g.Fullscreen));
        Set(props, "bool", "PerformanceStatsVisible", B(g.PerformanceStats));
        Set(props, "bool", "ReducedMotion", B(g.ReducedMotion));
        Set(props, "float", "PreferredTransparency", F(Math.Clamp(g.UiTransparency, 0, 1)));

        FileUtil.WriteAtomic(path, doc.Declaration + doc.ToString(SaveOptions.DisableFormatting));
        return true;
    }

    static XElement? Properties(XDocument doc) =>
        doc.Descendants("Item").FirstOrDefault(i => (string?)i.Attribute("class") == "UserGameSettings")?.Element("Properties");

    static XElement? Find(XElement props, string name) =>
        props.Elements().FirstOrDefault(e => (string?)e.Attribute("name") == name);

    static double? Num(XElement props, string name) =>
        double.TryParse(Find(props, name)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    static bool? Bool(XElement props, string name) =>
        bool.TryParse(Find(props, name)?.Value, out var v) ? v : null;

    static void Set(XElement props, string kind, string name, string value)
    {
        var el = Find(props, name);
        if (el is null) props.Add(el = new XElement(kind, new XAttribute("name", name)));
        el.Value = value;
    }

    static void SetVector(XElement props, string name, double v)
    {
        var el = Find(props, name);
        if (el is null) props.Add(el = new XElement("Vector2", new XAttribute("name", name)));
        el.ReplaceNodes(new XElement("X", F(v)), new XElement("Y", F(v)));
    }

    static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
    static string B(bool v) => v ? "true" : "false";
}
