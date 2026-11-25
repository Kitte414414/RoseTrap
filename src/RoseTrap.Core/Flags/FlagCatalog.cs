namespace RoseTrap.Core.Flags;

public enum FlagType { Bool, Int, String, Log }

/// <summary>Flag names RoseTrap knows about, and which of them Roblox still honours.</summary>
public static class FlagCatalog
{
    /// <summary>
    /// Since 29 Sep 2025 the Roblox player ignores local flags outside this list
    /// (devforum.roblox.com/t/3966569). Roblox can change it at any time.
    /// </summary>
    public static readonly IReadOnlySet<string> Allowlist = new HashSet<string>(StringComparer.Ordinal)
    {
        // Geometry
        "DFIntCSGLevelOfDetailSwitchingDistance",
        "DFIntCSGLevelOfDetailSwitchingDistanceL12",
        "DFIntCSGLevelOfDetailSwitchingDistanceL23",
        "DFIntCSGLevelOfDetailSwitchingDistanceL34",
        // Rendering
        "FFlagHandleAltEnterFullscreenManually",
        "DFFlagTextureQualityOverrideEnabled",
        "DFIntTextureQualityOverride",
        "FIntDebugForceMSAASamples",
        "DFFlagDisableDPIScale",
        "FFlagDebugGraphicsPreferD3D11",
        "FFlagDebugSkyGray",
        "DFFlagDebugPauseVoxelizer",
        "DFIntDebugFRMQualityLevelOverride",
        "FIntFRMMaxGrassDistance",
        "FIntFRMMinGrassDistance",
        "FFlagDebugGraphicsPreferVulkan",
        "FFlagDebugGraphicsPreferOpenGL",
        // UI
        "FIntGrassMovementReducedMotionFactor",
    };

    public static bool IsAllowed(string name) => Allowlist.Contains(name);

    /// <summary>Roblox encodes the value type in the prefix: [D|S]F{Flag|Int|String|Log}Name.</summary>
    public static FlagType TypeOf(string name)
    {
        var span = name.AsSpan();
        if (span.Length > 1 && (span[0] == 'D' || span[0] == 'S') && span[1] == 'F') span = span[1..];
        if (span.StartsWith("FFlag")) return FlagType.Bool;
        if (span.StartsWith("FInt")) return FlagType.Int;
        if (span.StartsWith("FString")) return FlagType.String;
        return FlagType.Log;
    }

    public static bool IsValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) return false;
        var span = name.AsSpan();
        if ((span[0] == 'D' || span[0] == 'S') && span.Length > 1 && span[1] == 'F') span = span[1..];
        ReadOnlySpan<string> prefixes = ["FFlag", "FInt", "FString", "FLog"];
        foreach (var p in prefixes)
        {
            if (!span.StartsWith(p) || span.Length == p.Length) continue;
            foreach (var c in span[p.Length..])
                if (!char.IsAsciiLetterOrDigit(c) && c != '_') return false;
            return true;
        }
        return false;
    }

    /// <summary>Normalises a user-typed value to what Roblox expects, or null if it doesn't fit the type.</summary>
    public static string? NormalizeValue(string name, string value)
    {
        value = value.Trim();
        switch (TypeOf(name))
        {
            case FlagType.Bool:
                if (bool.TryParse(value, out var b)) return b ? "True" : "False";
                return value switch { "1" => "True", "0" => "False", _ => null };
            case FlagType.Int:
                return long.TryParse(value, out var i) ? i.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
            default:
                return value;
        }
    }
}
