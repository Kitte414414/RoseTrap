namespace RoseTrap.Services;

/// <summary>A small Roblox-like scene (sky, baseplate, blocks, a tree) for the shader preview.</summary>
public static class PreviewScene
{
    /// <param name="sky">Optional square RGB8 sky face (w×w) to use instead of the default blue sky.</param>
    public static byte[] Build(int w, int h, byte[]? sky)
    {
        var px = new byte[w * h * 3];
        int horizon = (int)(h * .58);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            (double r, double g, double b) c;
            if (y < horizon)
            {
                if (sky != null)
                {
                    // Map the scene's sky onto the upper half of the face, horizon to horizon.
                    int fy = Math.Clamp(w / 2 - (int)((horizon - y) * (w / 2.0) / horizon * .85), 0, w - 1);
                    int i = (fy * w + x) * 3;
                    c = (sky[i] / 255.0, sky[i + 1] / 255.0, sky[i + 2] / 255.0);
                }
                else
                {
                    double t = (double)y / horizon;
                    c = Mix((.36, .6, .92), (.78, .88, .98), t);
                    double dx = x - w * .78, dy = y - h * .16;
                    double sun = Math.Exp(-(dx * dx + dy * dy) / 260);
                    c = Mix(c, (1, .98, .9), Math.Min(1, sun * 1.4));
                }
            }
            else
            {
                // Green baseplate with faint stud grid, darker towards the bottom.
                double t = (double)(y - horizon) / (h - horizon);
                c = Mix((.39, .62, .3), (.25, .45, .2), t);
                double gx = x / (14 + t * 26.0), gy = (y - horizon) / (6 + t * 18.0);
                if (gx - Math.Floor(gx) < .06 || gy - Math.Floor(gy) < .08) c = Mul(c, .9);
            }
            Put(px, w, x, y, c);
        }

        Block(px, w, h, horizon, x: .12, width: .16, height: .32, (.84, .22, .24));   // red
        Block(px, w, h, horizon, x: .36, width: .11, height: .2, (.97, .8, .2));     // yellow
        Block(px, w, h, horizon, x: .62, width: .14, height: .42, (.2, .45, .85));   // blue
        Block(px, w, h, horizon, x: .83, width: .08, height: .14, (.95, .95, .95));  // white
        Tree(px, w, h, horizon, x: .52);
        return px;
    }

    static void Block(byte[] px, int w, int h, int horizon, double x, double width, double height, (double, double, double) col)
    {
        int x0 = (int)(x * w), bw = (int)(width * w), bh = (int)(height * h), side = bw / 3;
        int baseY = horizon + (int)(h * .18);
        for (int y = baseY - bh; y < baseY; y++)
        {
            for (int i = 0; i < bw; i++) Put(px, w, x0 + i, y, Mul(col, 1));                 // front, lit
            for (int i = 0; i < side; i++)                                                   // right side, shaded
            {
                int yy = y - i / 2;
                Put(px, w, x0 + bw + i, yy, Mul(col, .68));
            }
        }
        for (int i = 0; i < bw + side; i++)                                                  // top
            for (int j = 0; j < side / 2; j++)
            {
                int xx = x0 + i - j * 2 + side;
                if (xx >= x0 + j * 2 - 0 && xx < x0 + bw + side) Put(px, w, xx - side, baseY - bh - j, Mul(col, 1.15));
            }
        // contact shadow
        for (int i = -4; i < bw + side + 6; i++)
            for (int j = 0; j < 5; j++)
                Darken(px, w, h, x0 + i, baseY + j, .82);
    }

    static void Tree(byte[] px, int w, int h, int horizon, double x)
    {
        int cx = (int)(x * w), baseY = horizon + (int)(h * .12);
        for (int y = baseY - 34; y < baseY; y++)
            for (int i = -3; i <= 3; i++) Put(px, w, cx + i, y, (.45, .3, .18));
        for (int y = -28; y <= 28; y++)
            for (int i = -28; i <= 28; i++)
            {
                double d = (i * i + y * y) / 784.0;
                if (d > 1) continue;
                double shade = .75 + .35 * (1 - (i + y + 40) / 80.0);
                Put(px, w, cx + i, baseY - 52 + y, Mul((.25, .58, .25), shade));
            }
    }

    static void Put(byte[] px, int w, int x, int y, (double r, double g, double b) c)
    {
        int h = px.Length / 3 / w;
        if ((uint)x >= w || (uint)y >= h) return;
        int i = (y * w + x) * 3;
        px[i] = B(c.r); px[i + 1] = B(c.g); px[i + 2] = B(c.b);
    }

    static void Darken(byte[] px, int w, int h, int x, int y, double k)
    {
        if ((uint)x >= w || (uint)y >= h) return;
        int i = (y * w + x) * 3;
        px[i] = (byte)(px[i] * k); px[i + 1] = (byte)(px[i + 1] * k); px[i + 2] = (byte)(px[i + 2] * k);
    }

    static byte B(double v) => (byte)Math.Clamp((int)(v * 255), 0, 255);
    static (double, double, double) Mix((double r, double g, double b) a, (double r, double g, double b) b, double t) =>
        (a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t);
    static (double, double, double) Mul((double r, double g, double b) a, double k) => (a.r * k, a.g * k, a.b * k);
}
