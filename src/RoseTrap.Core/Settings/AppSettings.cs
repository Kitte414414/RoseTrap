namespace RoseTrap.Core.Settings;

public enum RenderApi { Auto, Direct3D11, Vulkan, OpenGL }
public enum LightingTech { Default, Voxel, ShadowMap, Future }
public enum TextureQuality { Auto, Low, Medium, High }
public enum MeshDetail { Default, Lowest }
public enum Preset { Potato, Balanced, Quality, Custom }

public sealed class GraphicsSettings
{
    /// <summary>Frame-rate cap written to Roblox's own settings. 0 = leave Roblox's value alone.</summary>
    public int FpsCap { get; set; } = 144;
    /// <summary>Roblox graphics quality 1–10; 0 = automatic.</summary>
    public int Quality { get; set; }
    public bool Potato { get; set; }

    public RenderApi RenderApi { get; set; }
    /// <summary>MSAA samples: 0 = automatic, 1 = off, 2/4/8.</summary>
    public int Msaa { get; set; }
    public TextureQuality Textures { get; set; }
    public MeshDetail Mesh { get; set; }
    public bool Grass { get; set; } = true;
    public bool DisableDpiScale { get; set; }
    /// <summary>Lets Alt+Enter switch to true exclusive fullscreen.</summary>
    public bool ExclusiveFullscreen { get; set; }

    // "Shaders" page: the look of the game.
    public LightingTech Lighting { get; set; }
    public bool Shadows { get; set; } = true;
    public bool PostProcessing { get; set; } = true;
    public bool GraySky { get; set; }
    public bool PauseVoxelizer { get; set; }
    /// <summary>Render-quality override 1–21; 0 = automatic.</summary>
    public int FrmQuality { get; set; }

    public GraphicsSettings Clone() => (GraphicsSettings)MemberwiseClone();
}

public sealed class CustomFlag
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class FlagBehaviour
{
    public bool ApplyOnLaunch { get; set; } = true;
    /// <summary>Only write flags Roblox still honours; everything else is cleaned out of the file.</summary>
    public bool AllowlistOnly { get; set; } = true;
    /// <summary>Also write %LocalAppData%\Roblox\ClientSettings, used by the official launcher.</summary>
    public bool WriteGlobalFile { get; set; } = true;
}

public sealed class LauncherSettings
{
    public bool InterceptLinks { get; set; } = true;
    public bool MultiInstance { get; set; }
    public string Channel { get; set; } = "LIVE";
    public bool CloseAfterLaunch { get; set; } = true;
}

public sealed class AppearanceSettings
{
    public string Accent { get; set; } = "Rose";
    /// <summary>Icon-only sidebar that expands while hovered.</summary>
    public bool CompactSidebar { get; set; } = true;
    public bool Animations { get; set; } = true;
}

public sealed class AppSettings
{
    public GraphicsSettings Graphics { get; set; } = new();
    public Preset Preset { get; set; } = Preset.Balanced;
    public List<CustomFlag> CustomFlags { get; set; } = [];
    /// <summary>Custom flag names RoseTrap wrote before, so deleting one also removes it from Roblox.</summary>
    public List<string> WrittenCustomFlags { get; set; } = [];
    public FlagBehaviour Flags { get; set; } = new();
    public LauncherSettings Launcher { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
}
