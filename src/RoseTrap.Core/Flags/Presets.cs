using RoseTrap.Core.Settings;

namespace RoseTrap.Core.Flags;

public static class Presets
{
    /// <summary>Graphics options for a preset. Custom keeps whatever the user has.</summary>
    public static GraphicsSettings Apply(Preset preset, GraphicsSettings current)
    {
        var g = current.Clone();
        switch (preset)
        {
            case Preset.Potato:
                g.Potato = true;
                break;
            case Preset.Balanced:
                g.Potato = false;
                g.Quality = 0; g.Msaa = 0; g.Textures = TextureQuality.Auto; g.Mesh = MeshDetail.Default;
                g.Grass = true; g.Shadows = true; g.PostProcessing = true; g.Lighting = LightingTech.Default;
                g.PauseVoxelizer = false; g.FrmQuality = 0;
                break;
            case Preset.Quality:
                g.Potato = false;
                g.Quality = 10; g.Msaa = 4; g.Textures = TextureQuality.High; g.Mesh = MeshDetail.Default;
                g.Grass = true; g.Shadows = true; g.PostProcessing = true; g.Lighting = LightingTech.Future;
                g.PauseVoxelizer = false; g.FrmQuality = 21;
                break;
        }
        return g;
    }
}
