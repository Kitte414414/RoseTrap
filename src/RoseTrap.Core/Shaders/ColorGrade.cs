namespace RoseTrap.Core.Shaders;

/// <summary>
/// A colour grade for the game image. Applied as one 5×5 colour matrix, so it costs nothing at runtime.
/// </summary>
public sealed record ColorGrade
{
    /// <summary>Stops of exposure, -2..2.</summary>
    public double Exposure { get; init; }
    /// <summary>Added light, -0.5..0.5.</summary>
    public double Brightness { get; init; }
    /// <summary>1 = unchanged, 0.5..1.6.</summary>
    public double Contrast { get; init; } = 1;
    /// <summary>1 = unchanged, 0 = grayscale, up to 2.</summary>
    public double Saturation { get; init; } = 1;
    /// <summary>-1 cool (blue) .. 1 warm (orange).</summary>
    public double Temperature { get; init; }
    /// <summary>-1 green .. 1 magenta.</summary>
    public double Tint { get; init; }
    /// <summary>Raises blacks for a faded/film look, 0..0.3.</summary>
    public double Fade { get; init; }

    public static readonly ColorGrade Neutral = new();

    public bool IsNeutral => this == Neutral;

    /// <summary>
    /// The grade as a 4×4 affine colour transform, column-vector convention: out = M · (r, g, b, 1).
    /// Only the RGB rows matter; row 3 stays (0,0,0,1).
    /// </summary>
    public double[,] Matrix()
    {
        var m = Identity();
        // Order matters and mirrors a typical grading pipeline: white balance → exposure → saturation → contrast → brightness → fade.
        double t = Temperature, g = Tint;
        m = Mul(Scale(1 + .12 * t + .04 * g, 1 - .08 * g, 1 - .12 * t + .04 * g), m);
        double e = Math.Pow(2, Exposure);
        m = Mul(Scale(e, e, e), m);
        m = Mul(SaturationMatrix(Saturation), m);
        double c = Contrast;
        m = Mul(Affine(c, .5 * (1 - c)), m);
        m = Mul(Affine(1, Brightness), m);
        m = Mul(Affine(1 - Fade, Fade), m);
        return m;
    }

    /// <summary>The layout MagSetFullscreenColorEffect expects: row-vector convention, 25 floats row-major.</summary>
    public float[] ToMagnifierMatrix()
    {
        var m = Matrix();
        var o = new float[25];
        for (int row = 0; row < 4; row++)       // input channel (r, g, b, 1→row 4)
            for (int col = 0; col < 3; col++)   // output channel
                o[(row == 3 ? 4 : row) * 5 + col] = (float)m[col, row];
        o[3 * 5 + 3] = 1; // alpha passes through
        o[4 * 5 + 4] = 1;
        return o;
    }

    /// <summary>Applies the grade to one 0..1 RGB colour (for the in-app preview).</summary>
    public static (double R, double G, double B) Apply(double[,] m, double r, double g, double b) =>
        (Math.Clamp(m[0, 0] * r + m[0, 1] * g + m[0, 2] * b + m[0, 3], 0, 1),
         Math.Clamp(m[1, 0] * r + m[1, 1] * g + m[1, 2] * b + m[1, 3], 0, 1),
         Math.Clamp(m[2, 0] * r + m[2, 1] * g + m[2, 2] * b + m[2, 3], 0, 1));

    static double[,] Identity() => new double[,] { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } };

    static double[,] Scale(double r, double g, double b) => new double[,] { { r, 0, 0, 0 }, { 0, g, 0, 0 }, { 0, 0, b, 0 }, { 0, 0, 0, 1 } };

    static double[,] Affine(double k, double offset) =>
        new double[,] { { k, 0, 0, offset }, { 0, k, 0, offset }, { 0, 0, k, offset }, { 0, 0, 0, 1 } };

    static double[,] SaturationMatrix(double s)
    {
        const double lr = .2126, lg = .7152, lb = .0722; // Rec. 709 luma
        double i = 1 - s;
        return new double[,]
        {
            { lr * i + s, lg * i, lb * i, 0 },
            { lr * i, lg * i + s, lb * i, 0 },
            { lr * i, lg * i, lb * i + s, 0 },
            { 0, 0, 0, 1 },
        };
    }

    static double[,] Mul(double[,] a, double[,] b)
    {
        var r = new double[4, 4];
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                for (int k = 0; k < 4; k++)
                    r[i, j] += a[i, k] * b[k, j];
        return r;
    }
}

public sealed record ShaderPreset(string Name, string Description, ColorGrade Grade);

public static class ShaderPresets
{
    public const string Custom = "Custom";

    public static readonly IReadOnlyList<ShaderPreset> All =
    [
        new("Off", "The game exactly as Roblox draws it.", ColorGrade.Neutral),
        new("Rose Bloom", "Soft pink warmth with lifted shadows. RoseTrap's signature look.",
            new() { Temperature = .25, Tint = .35, Saturation = 1.1, Contrast = 1.05, Fade = .04, Brightness = .01 }),
        new("Sunset Golden Hour", "Warm golden light, rich oranges and deeper shadows.",
            new() { Temperature = .75, Tint = .1, Saturation = 1.2, Contrast = 1.12, Exposure = .1 }),
        new("Cinematic", "Muted colours and a gentle film fade, like a movie still.",
            new() { Saturation = .82, Contrast = 1.18, Fade = .06, Temperature = -.1, Exposure = -.05 }),
        new("Vivid", "Punchy colours that make bright worlds pop.",
            new() { Saturation = 1.45, Contrast = 1.12, Exposure = .05 }),
        new("Cool Night", "Blue moonlight for spooky and night maps.",
            new() { Temperature = -.7, Saturation = .85, Contrast = 1.1, Exposure = -.2, Fade = .03 }),
        new("Pastel", "Bright, airy and soft.",
            new() { Saturation = .75, Contrast = .85, Brightness = .05, Fade = .1, Tint = .1 }),
        new("Noir", "Black and white with strong contrast.",
            new() { Saturation = 0, Contrast = 1.35, Exposure = -.05 }),
        new("Clarity", "Slightly more contrast and colour. Hardly noticeable, easier to read.",
            new() { Saturation = 1.12, Contrast = 1.08 }),
    ];

    public static ShaderPreset? Find(string name) => All.FirstOrDefault(p => p.Name == name);
}
