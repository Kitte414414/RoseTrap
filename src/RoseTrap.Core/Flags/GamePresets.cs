namespace RoseTrap.Core.Flags;

public sealed record Game(string Name, long PlaceId);

/// <param name="Flags">Only allowlisted flags: everything else is ignored by Roblox anyway.</param>
public sealed record GamePreset(string Id, string Name, string Description, string Icon,
    IReadOnlyList<Game> Games, IReadOnlyDictionary<string, string> Flags);

/// <summary>Ready-made fast flag sets tuned for kinds of games.</summary>
public static class GamePresets
{
    const string T = "True", F = "False";

    static Dictionary<string, string> Flags(params (string, string)[] f) => f.ToDictionary(p => p.Item1, p => p.Item2, StringComparer.Ordinal);

    static readonly (string, string)[] NoGrass =
        [("FIntFRMMinGrassDistance", "0"), ("FIntFRMMaxGrassDistance", "0"), ("FIntGrassMovementReducedMotionFactor", "0")];
    static readonly (string, string)[] LowLod =
    [
        ("DFIntCSGLevelOfDetailSwitchingDistance", "0"), ("DFIntCSGLevelOfDetailSwitchingDistanceL12", "0"),
        ("DFIntCSGLevelOfDetailSwitchingDistanceL23", "0"), ("DFIntCSGLevelOfDetailSwitchingDistanceL34", "0"),
    ];
    static (string, string)[] Textures(int q) => [("DFFlagTextureQualityOverrideEnabled", T), ("DFIntTextureQualityOverride", q.ToString())];

    public static readonly IReadOnlyList<GamePreset> All =
    [
        new("shooter", "Competitive shooters",
            "Clean, fast image for aiming: no anti-aliasing blur, no grass to hide in, true fullscreen for lower input delay.",
            "Shooter",
            [new("Arsenal", 286090429), new("Rivals", 17625359962), new("Phantom Forces", 292439477), new("Frontlines", 5938036553)],
            Flags([("FIntDebugForceMSAASamples", "1"), .. NoGrass, .. Textures(0), ("DFFlagDisableDPIScale", T),
                   ("FFlagHandleAltEnterFullscreenManually", F)])),

        new("battlegrounds", "Fighting & battlegrounds",
            "Keeps frame rate steady through effects-heavy fights.",
            "Fight",
            [new("The Strongest Battlegrounds", 10449761463), new("Da Hood", 2788229376), new("BedWars", 6872265039), new("Murder Mystery 2", 142823291)],
            Flags([("FIntDebugForceMSAASamples", "1"), .. NoGrass, .. Textures(1), ("DFIntDebugFRMQualityLevelOverride", "6"),
                   ("DFFlagDisableDPIScale", T)])),

        new("openworld", "Open worlds",
            "Simpler distant models and no grass for big maps you travel across.",
            "World",
            [new("Blox Fruits", 2753915549), new("Jailbreak", 606849621), new("Fisch", 16732694052), new("Dead Rails", 116495829188952)],
            Flags([.. LowLod, .. NoGrass, .. Textures(1), ("FIntDebugForceMSAASamples", "2")])),

        new("simulator", "Simulators & tycoons",
            "Cuts the cost of hundreds of pets, plants and particles on screen.",
            "Simulator",
            [new("Pet Simulator 99", 8737899170), new("Grow a Garden", 126884695634066), new("Adopt Me!", 920587237)],
            Flags([.. LowLod, .. NoGrass, .. Textures(0), ("DFIntDebugFRMQualityLevelOverride", "4"), ("DFFlagDebugPauseVoxelizer", T)])),

        new("atmosphere", "Horror & story",
            "Keeps lighting and detail where the mood matters; only smooths edges.",
            "Atmosphere",
            [new("Doors", 6516141723), new("Brookhaven RP", 4924922222)],
            Flags([("FIntDebugForceMSAASamples", "4"), .. Textures(2)])),
    ];

    public static GamePreset? ForPlace(long? placeId) =>
        placeId is null ? null : All.FirstOrDefault(p => p.Games.Any(g => g.PlaceId == placeId));

    public static GamePreset? Find(string? id) => All.FirstOrDefault(p => p.Id == id);

    /// <summary>Every flag any preset can write, so switching presets cleans up after the old one.</summary>
    public static IEnumerable<string> AllFlagNames => All.SelectMany(p => p.Flags.Keys).Distinct();
}
