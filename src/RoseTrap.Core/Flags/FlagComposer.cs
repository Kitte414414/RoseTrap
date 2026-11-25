using System.Globalization;
using RoseTrap.Core.Settings;

namespace RoseTrap.Core.Flags;

/// <param name="Set">Flags to write, name → value.</param>
/// <param name="Remove">Flags RoseTrap owns that must not be in the file.</param>
/// <param name="Skipped">Flags left out because Roblox no longer honours them.</param>
/// <param name="GamePreset">Name of the game preset that was layered in, if any.</param>
public sealed record FlagPlan(
    IReadOnlyDictionary<string, string> Set,
    IReadOnlySet<string> Remove,
    IReadOnlyList<string> Skipped,
    string? GamePreset = null);

/// <summary>Turns the settings the user picked into the exact fast flags to write.</summary>
public static class FlagComposer
{
    const string True = "True";

    /// <summary>Every flag the graphics options can produce. Used to clean up flags an option no longer wants.</summary>
    public static readonly IReadOnlySet<string> ManagedFlags = new HashSet<string>(StringComparer.Ordinal)
    {
        "FIntDebugForceMSAASamples", "DFFlagDisableDPIScale", "FFlagHandleAltEnterFullscreenManually",
        "DFFlagTextureQualityOverrideEnabled", "DFIntTextureQualityOverride",
        "DFIntCSGLevelOfDetailSwitchingDistance", "DFIntCSGLevelOfDetailSwitchingDistanceL12",
        "DFIntCSGLevelOfDetailSwitchingDistanceL23", "DFIntCSGLevelOfDetailSwitchingDistanceL34",
        "FFlagDebugGraphicsPreferD3D11", "FFlagDebugGraphicsPreferVulkan", "FFlagDebugGraphicsPreferOpenGL",
        "FIntFRMMinGrassDistance", "FIntFRMMaxGrassDistance", "FIntGrassMovementReducedMotionFactor",
        "FFlagDebugSkyGray", "DFFlagDebugPauseVoxelizer", "DFIntDebugFRMQualityLevelOverride",
        // Legacy: ignored by Roblox since the allowlist, still written when the allowlist filter is off.
        "FFlagDebugGraphicsDisableDirect3D11", "DFFlagDebugRenderForceTechnologyVoxel",
        "FFlagDebugForceFutureIsBrightPhase2", "FFlagDebugForceFutureIsBrightPhase3",
        "FFlagDebugForceDisableShadows", "FFlagDisablePostFx", "DFIntTaskSchedulerTargetFps",
    };

    /// <summary>Potato mode forces the cheapest value of every option.</summary>
    public static GraphicsSettings Effective(GraphicsSettings g)
    {
        if (!g.Potato) return g;
        var p = g.Clone();
        p.Quality = 1;
        p.Msaa = 1;
        p.Textures = TextureQuality.Low;
        p.Mesh = MeshDetail.Lowest;
        p.Grass = false;
        p.Shadows = false;
        p.PostProcessing = false;
        p.Lighting = LightingTech.Voxel;
        p.PauseVoxelizer = true;
        p.FrmQuality = 1;
        return p;
    }

    public static Dictionary<string, string> GraphicsFlags(GraphicsSettings settings)
    {
        var g = Effective(settings);
        var f = new Dictionary<string, string>(StringComparer.Ordinal);
        static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        switch (g.RenderApi)
        {
            case RenderApi.Direct3D11: f["FFlagDebugGraphicsPreferD3D11"] = True; break;
            case RenderApi.Vulkan:
                f["FFlagDebugGraphicsPreferVulkan"] = True;
                f["FFlagDebugGraphicsDisableDirect3D11"] = True;
                break;
            case RenderApi.OpenGL:
                f["FFlagDebugGraphicsPreferOpenGL"] = True;
                f["FFlagDebugGraphicsDisableDirect3D11"] = True;
                break;
        }

        if (g.Msaa is 1 or 2 or 4 or 8) f["FIntDebugForceMSAASamples"] = I(g.Msaa);

        if (g.Textures != TextureQuality.Auto)
        {
            f["DFFlagTextureQualityOverrideEnabled"] = True;
            f["DFIntTextureQualityOverride"] = I((int)g.Textures - 1); // Low 0, Medium 1, High 2
        }

        if (g.Mesh == MeshDetail.Lowest)
        {
            f["DFIntCSGLevelOfDetailSwitchingDistance"] = "0";
            f["DFIntCSGLevelOfDetailSwitchingDistanceL12"] = "0";
            f["DFIntCSGLevelOfDetailSwitchingDistanceL23"] = "0";
            f["DFIntCSGLevelOfDetailSwitchingDistanceL34"] = "0";
        }

        if (!g.Grass)
        {
            f["FIntFRMMinGrassDistance"] = "0";
            f["FIntFRMMaxGrassDistance"] = "0";
            f["FIntGrassMovementReducedMotionFactor"] = "0";
        }

        if (g.DisableDpiScale) f["DFFlagDisableDPIScale"] = True;
        if (g.ExclusiveFullscreen) f["FFlagHandleAltEnterFullscreenManually"] = "False";
        if (g.GraySky) f["FFlagDebugSkyGray"] = True;
        if (g.PauseVoxelizer) f["DFFlagDebugPauseVoxelizer"] = True;
        if (g.FrmQuality is >= 1 and <= 21) f["DFIntDebugFRMQualityLevelOverride"] = I(g.FrmQuality);

        switch (g.Lighting)
        {
            case LightingTech.Voxel: f["DFFlagDebugRenderForceTechnologyVoxel"] = True; break;
            case LightingTech.ShadowMap: f["FFlagDebugForceFutureIsBrightPhase2"] = True; break;
            case LightingTech.Future: f["FFlagDebugForceFutureIsBrightPhase3"] = True; break;
        }
        if (!g.Shadows) f["FFlagDebugForceDisableShadows"] = True;
        if (!g.PostProcessing) f["FFlagDisablePostFx"] = True;
        if (g.FpsCap > 0) f["DFIntTaskSchedulerTargetFps"] = I(g.FpsCap);

        return f;
    }

    /// <param name="placeId">The game being launched, if known; picks its game preset.</param>
    public static FlagPlan Compose(AppSettings s, long? placeId = null)
    {
        var desired = GraphicsFlags(s.Graphics);

        // Layer order: graphics options < game preset < custom flags (an expert can override anything).
        var preset = ActiveGamePreset(s, placeId);
        if (preset != null)
            foreach (var (name, value) in preset.Flags) desired[name] = value;

        foreach (var c in s.CustomFlags)
            if (c.Enabled && FlagCatalog.IsValidName(c.Name))
                desired[c.Name] = FlagCatalog.NormalizeValue(c.Name, c.Value) ?? c.Value.Trim();

        var skipped = new List<string>();
        if (s.Flags.AllowlistOnly)
        {
            foreach (var name in desired.Keys.Where(n => !FlagCatalog.IsAllowed(n)).ToList())
            {
                desired.Remove(name);
                skipped.Add(name);
            }
        }

        var owned = new HashSet<string>(ManagedFlags, StringComparer.Ordinal);
        owned.UnionWith(GamePresets.AllFlagNames);
        owned.UnionWith(s.CustomFlags.Select(c => c.Name));
        owned.UnionWith(s.WrittenCustomFlags);
        owned.ExceptWith(desired.Keys);

        skipped.Sort(StringComparer.Ordinal);
        return new FlagPlan(desired, owned, skipped, preset?.Name);
    }

    /// <summary>The game's own preset when auto-detection is on and knows the game, else the "always" preset.</summary>
    public static GamePreset? ActiveGamePreset(AppSettings s, long? placeId) =>
        (s.GamePresets.AutoByGame ? GamePresets.ForPlace(placeId) : null) ?? GamePresets.Find(s.GamePresets.Always);
}
