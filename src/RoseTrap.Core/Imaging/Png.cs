using System.IO.Compression;

namespace RoseTrap.Core.Imaging;

/// <summary>Minimal 8-bit RGB / RGBA PNG encoder (no dependencies).</summary>
public static class Png
{
    static readonly uint[] Crc = BuildCrc();

    /// <param name="pixels">Row-major, top row first, <paramref name="channels"/> bytes per pixel.</param>
    /// <param name="channels">3 = RGB, 4 = RGBA (straight alpha).</param>
    public static byte[] Encode(byte[] pixels, int w, int h, int channels = 3)
    {
        if (channels is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(channels));
        if (pixels.Length != w * h * channels) throw new ArgumentException("Pixel buffer size doesn't match.", nameof(pixels));

        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (int y = 0; y < h; y++)
            {
                z.WriteByte(0); // filter: none
                z.Write(pixels, y * w * channels, w * channels);
            }
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        WriteBe(ihdr, 0, (uint)w); WriteBe(ihdr, 4, (uint)h);
        ihdr[8] = 8; ihdr[9] = (byte)(channels == 4 ? 6 : 2);
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
