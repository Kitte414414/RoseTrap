using System.IO.Compression;

namespace RoseTrap.Core.Shaders;

public sealed record SkyboxPreset(string Name, string Description, uint Zenith, uint Horizon, uint Ground,
    uint Sun = 0, double SunSize = 0, double SunElevation = 0, double Stars = 0, double Aurora = 0);

/// <summary>
/// Generated skyboxes. Roblox reads PNG data from the six sky512_*.tex files in its install folder,
/// so RoseTrap draws its own skies instead of shipping images. Games with their own Sky keep it.
/// </summary>
public static class Skyboxes
{
    public const string Default = "Default";
    public const string BackupSuffix = ".rosetrap-original";
    internal static readonly string[] Faces = ["bk", "dn", "ft", "lf", "rt", "up"];

    public static readonly IReadOnlyList<SkyboxPreset> All =
    [
        new(Default, "Roblox's own sky.", 0, 0, 0),
        new("Rose Dusk", "Pink evening glow fading into violet, first stars above.",
            0x2a1533, 0xf2a0b5, 0x3a2430, Sun: 0xffe0e6, SunSize: 2.2, SunElevation: 4, Stars: .35),
        new("Golden Hour", "Low warm sun over a soft blue sky.",
            0x3c6fae, 0xffc27a, 0x5a4a3a, Sun: 0xfff4d0, SunSize: 2.6, SunElevation: 7),
        new("Clear Day", "Bright blue sky with a high sun.",
            0x2f74d0, 0xbfe0ff, 0x6c7f8f, Sun: 0xffffff, SunSize: 2, SunElevation: 32),
        new("Pastel", "Lavender to peach, very soft.",
            0x9fb6ff, 0xffd3e4, 0xcab8c8, Sun: 0xfffaf0, SunSize: 2.4, SunElevation: 18),
        new("Overcast", "Flat gray clouds. Calm, low contrast.",
            0x7f868f, 0xc4c8ce, 0x5d6168),
        new("Midnight", "Deep night with a pale moon and lots of stars.",
            0x04050d, 0x1b2342, 0x07080e, Sun: 0xdfe6ff, SunSize: 1.6, SunElevation: 26, Stars: 1),
        new("Aurora", "Green and teal northern lights over a dark sky.",
            0x030c14, 0x0c2e33, 0x05080a, Stars: .7, Aurora: 1),
    ];

    public static SkyboxPreset? Find(string name) => All.FirstOrDefault(p => p.Name == name);

    public static string SkyFolder(string installDir) => Path.Combine(installDir, "PlatformContent", "pc", "textures", "sky");

    /// <summary>Writes the skybox into an install (or restores Roblox's sky for <see cref="Default"/>).</summary>
    /// <param name="cacheDir">Where generated faces are kept so they're drawn once per preset.</param>
    public static void Apply(string installDir, string preset, string cacheDir)
    {
        var sky = SkyFolder(installDir);
        if (!Directory.Exists(sky)) throw new DirectoryNotFoundException("This Roblox version has no sky textures folder.");
        var backup = sky + BackupSuffix;

        if (preset == Default || Find(preset) is not { } p)
        {
            if (!Directory.Exists(backup)) return;
            foreach (var f in Directory.GetFiles(backup))
                FileUtil.CopyOver(f, Path.Combine(sky, Path.GetFileName(f)));
            return;
        }

        if (!Directory.Exists(backup))
        {
            Directory.CreateDirectory(backup);
            foreach (var face in Faces)
            {
                var src = Path.Combine(sky, $"sky512_{face}.tex");
                if (File.Exists(src)) File.Copy(src, Path.Combine(backup, Path.GetFileName(src)));
            }
        }

        var cache = Path.Combine(cacheDir, p.Name);
        foreach (var face in Faces)
        {
            var file = Path.Combine(cache, $"sky512_{face}.tex");
            if (!File.Exists(file))
            {
                Directory.CreateDirectory(cache);
                File.WriteAllBytes(file, RenderFace(p, face, 512));
            }
            FileUtil.CopyOver(file, Path.Combine(sky, $"sky512_{face}.tex"));
        }
    }

    public static string? Current(string installDir, string cacheDir)
    {
        var sky = SkyFolder(installDir);
        var up = Path.Combine(sky, "sky512_up.tex");
        if (!File.Exists(up)) return null;
        var bytes = File.ReadAllBytes(up);
        foreach (var p in All.Skip(1))
        {
            var c = Path.Combine(cacheDir, p.Name, "sky512_up.tex");
            if (File.Exists(c) && File.ReadAllBytes(c).AsSpan().SequenceEqual(bytes)) return p.Name;
        }
        return Default;
    }

    /// <summary>Renders one cube face as a PNG.</summary>
    public static byte[] RenderFace(SkyboxPreset p, string face, int size) => Png.Encode(RenderRgb(p, face, size), size, size);

    /// <summary>Renders one cube face as raw RGB8 (row-major, top row first).</summary>
    public static byte[] RenderRgb(SkyboxPreset p, string face, int size)
    {
        var rgb = new byte[size * size * 3];
        double sunEl = p.SunElevation * Math.PI / 180;
        // The sun sits in the middle of the back face so its glow never crosses a cube edge.
        var sun = (X: 0.0, Y: Math.Sin(sunEl), Z: Math.Cos(sunEl));
        double sunCos = Math.Cos(p.SunSize * Math.PI / 180);

        for (int py = 0; py < size; py++)
        for (int px = 0; px < size; px++)
        {
            double u = (px + .5) / size * 2 - 1, v = 1 - (py + .5) / size * 2;
            var (x, y, z) = face switch
            {
                "up" => (u, 1.0, -v),
                "dn" => (u, -1.0, v),
                "bk" => (-u, v, 1.0),
                "ft" => (u, v, -1.0),
                "lf" => (-1.0, v, -u),
                _ => (1.0, v, u), // rt
            };
            double len = Math.Sqrt(x * x + y * y + z * z);
            x /= len; y /= len; z /= len;
            var c = Shade(p, x, y, z, sun, sunCos, face == "bk");
            // Tiny dither hides 8-bit banding in the long gradients.
            double n = (Hash(px, py, face[0] * 31 + face[1]) - .5) / 255;
            int i = (py * size + px) * 3;
            rgb[i] = ToByte(c.R + n);
            rgb[i + 1] = ToByte(c.G + n);
            rgb[i + 2] = ToByte(c.B + n);
        }
        return rgb;
    }

    static (double R, double G, double B) Shade(SkyboxPreset p, double x, double y, double z,
        (double X, double Y, double Z) sun, double sunCos, bool sunFace)
    {
        var zen = Rgb(p.Zenith); var hor = Rgb(p.Horizon); var gnd = Rgb(p.Ground);
        (double R, double G, double B) c;
        if (y >= 0)
        {
            double t = Math.Pow(y, .45);
            c = Mix(hor, zen, t);
        }
        else
        {
            c = Mix(Mix(hor, gnd, .6), gnd, Math.Min(1, -y * 4));
        }

        if (p.Stars > 0 && y > .05)
        {
            // Stars from a hash of the direction, quantised so each is a crisp point.
            int qx = (int)Math.Floor(x * 700), qy = (int)Math.Floor(y * 700), qz = (int)Math.Floor(z * 700);
            double h = Hash(qx, qy, qz);
            double density = .0016 * p.Stars;
            if (h < density)
            {
                double b = (.5 + .5 * Hash(qz, qx, qy)) * Math.Min(1, (y - .05) * 4);
                c = Mix(c, (1, 1, 1), b * .9);
            }
        }

        if (p.Aurora > 0 && y > 0)
        {
            // Bands that only depend on elevation stay seamless across faces.
            double band = Math.Exp(-Math.Pow((y - .28) / .09, 2)) * (.6 + .4 * Math.Sin(y * 60))
                          + .6 * Math.Exp(-Math.Pow((y - .45) / .06, 2));
            c = Add(c, Mul((0.15, .85, .55), band * .55 * p.Aurora));
        }

        if (p.Sun != 0 && sunFace)
        {
            double d = x * sun.X + y * sun.Y + z * sun.Z;
            var sc = Rgb(p.Sun);
            double angle = Math.Acos(Math.Clamp(d, -1, 1));
            double disc = Math.Clamp((d - sunCos) / (1 - sunCos) * 6, 0, 1);
            double glow = Math.Exp(-angle * angle / .02) * .55 + Math.Exp(-angle * angle / .12) * .25;
            c = Add(Mix(c, sc, disc), Mul(sc, glow * (1 - disc)));
        }
        return c;
    }

    static (double, double, double) Rgb(uint c) => (((c >> 16) & 255) / 255.0, ((c >> 8) & 255) / 255.0, (c & 255) / 255.0);
    static (double R, double G, double B) Mix((double R, double G, double B) a, (double R, double G, double B) b, double t) =>
        (a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
    static (double R, double G, double B) Add((double R, double G, double B) a, (double R, double G, double B) b) => (a.R + b.R, a.G + b.G, a.B + b.B);
    static (double R, double G, double B) Mul((double R, double G, double B) a, double k) => (a.R * k, a.G * k, a.B * k);
    static byte ToByte(double v) => (byte)Math.Clamp((int)Math.Round(v * 255), 0, 255);

    static double Hash(int a, int b, int c)
    {
        unchecked
        {
            uint h = (uint)a * 374761393u + (uint)b * 668265263u + (uint)c * 2147483647u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffffff) / (double)0x1000000;
        }
    }
}

/// <summary>Minimal RGB8 PNG encoder (no dependencies).</summary>
internal static class Png
{
    static readonly uint[] Crc = BuildCrc();

    public static byte[] Encode(byte[] rgb, int w, int h)
    {
        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (int y = 0; y < h; y++)
            {
                z.WriteByte(0); // filter: none
                z.Write(rgb, y * w * 3, w * 3);
            }
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        WriteBe(ihdr, 0, (uint)w); WriteBe(ihdr, 4, (uint)h);
        ihdr[8] = 8; ihdr[9] = 2; // 8-bit truecolour
        Chunk(png, "IHDR", ihdr);
        Chunk(png, "IDAT", raw.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; WriteBe(len, 0, (uint)data.Length); s.Write(len);
        var t = System.Text.Encoding.ASCII.GetBytes(type); s.Write(t); s.Write(data);
        uint c = 0xffffffff;
        foreach (var b in t) c = Crc[(c ^ b) & 0xff] ^ (c >> 8);
        foreach (var b in data) c = Crc[(c ^ b) & 0xff] ^ (c >> 8);
        var crc = new byte[4]; WriteBe(crc, 0, c ^ 0xffffffff); s.Write(crc);
    }

    static void WriteBe(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

    static uint[] BuildCrc()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }
}
