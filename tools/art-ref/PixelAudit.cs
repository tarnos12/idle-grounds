// Pixel-art audit + reference-sheet helpers (loaded by tools/art-ref/buildings-sheet.ps1 via Add-Type).
// Windows PowerShell 5.1 / .NET Framework System.Drawing; no other dependencies.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

public static class PixelAudit
{
    // ART-SPEC 1.3, P01..P32
    public static readonly int[] Pal = {
        0x0D0F14,0x1B1F2B,0x2E3345,0x4A5068,0x7A809A,0xB7BCCD,0xE8ECF3,0x0F3B34,
        0x1F6B57,0x2F9A7A,0x5FCF9C,0xA8ECC0,0x4F7A2F,0x86B34A,0xC3D67A,0x4A1417,
        0x8C1D22,0xC8322B,0xEE6A4A,0x3D2616,0x6E4524,0xA8703A,0xD9A45C,0xF2C94C,
        0xFFF0A0,0x14405A,0x2B86B3,0x5ED4E8,0xC4F6FF,0xFF8A1F,0x3A2A63,0x7D5FC4 };

    public static int[] Load(string path, out int w, out int h)
    {
        using (var src = new Bitmap(path))
        {
            w = src.Width; h = src.Height;
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(src, 0, 0, w, h); }
                var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var px = new int[w * h];
                Marshal.Copy(data.Scan0, px, 0, px.Length);
                bmp.UnlockBits(data);
                return px;
            }
        }
    }

    static int PalDist(int rgb)
    {
        int r = (rgb >> 16) & 255, g = (rgb >> 8) & 255, b = rgb & 255, best = int.MaxValue;
        foreach (var p in Pal)
        {
            int dr = r - ((p >> 16) & 255), dg = g - ((p >> 8) & 255), db = b - (p & 255);
            int d = dr * dr + dg * dg + db * db;
            if (d < best) best = d;
        }
        return (int)Math.Round(Math.Sqrt(best));
    }

    // Returns "key=value;..." metrics for one sprite (first frame only when frameW > 0).
    public static string Analyze(string path, int frameW)
    {
        int W, H; var all = Load(path, out W, out H);
        int w = frameW > 0 ? Math.Min(frameW, W) : W, h = H;
        Func<int, int, int> at = (x, y) => all[y * W + x];
        int opaque = 0, semi = 0, onPal = 0, nearPal = 0, isolated = 0, interior = 0, boundary = 0, darkBoundary = 0;
        int blocks = 0, uniformBlocks = 0, runs = 0, runPx = 0;
        int minX = w, minY = h, maxX = -1, maxY = -1;
        var colours = new HashSet<int>();
        for (int y = 0; y < h; y++)
        {
            int prev = -1;
            for (int x = 0; x < w; x++)
            {
                int c = at(x, y); int a = (c >> 24) & 255; int rgb = c & 0xFFFFFF;
                if (a == 0) { prev = -1; continue; }
                if (a < 255) semi++;
                opaque++; colours.Add(rgb);
                if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
                int d = PalDist(rgb); if (d == 0) onPal++; if (d <= 12) nearPal++;
                if (rgb != prev) { runs++; } runPx++; prev = rgb;
                bool edge = false; int same = 0, n = 0;
                int[,] nb = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + nb[k, 0], ny = y + nb[k, 1];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) { edge = true; continue; }
                    int nc = at(nx, ny);
                    if (((nc >> 24) & 255) == 0) { edge = true; continue; }
                    n++; if ((nc & 0xFFFFFF) == rgb) same++;
                }
                if (edge)
                {
                    boundary++;
                    int r = (rgb >> 16) & 255, g = (rgb >> 8) & 255, b = rgb & 255;
                    if (0.299 * r + 0.587 * g + 0.114 * b < 70) darkBoundary++;
                }
                else { interior++; if (same == 0) isolated++; }
            }
        }
        for (int y = 0; y + 1 < h; y += 2)
            for (int x = 0; x + 1 < w; x += 2)
            {
                int a = at(x, y), b = at(x + 1, y), c = at(x, y + 1), d = at(x + 1, y + 1);
                if (((a >> 24) & 255) == 0) continue;
                blocks++; if (a == b && a == c && a == d) uniformBlocks++;
            }
        Func<double, string> f = v => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        return string.Format("w={0};h={1};frames={2};opaque={3};colours={4};onPalPct={5};nearPalPct={6};semiAlpha={7};noisePct={8};runLen={9};darkOutlinePct={10};block2x2Pct={11};bbox={12}x{13};fillPct={14}",
            w, h, W / w, opaque, colours.Count,
            f(100.0 * onPal / Math.Max(1, opaque)), f(100.0 * nearPal / Math.Max(1, opaque)), semi,
            f(100.0 * isolated / Math.Max(1, interior)), f((double)runPx / Math.Max(1, runs)),
            f(100.0 * darkBoundary / Math.Max(1, boundary)), f(100.0 * uniformBlocks / Math.Max(1, blocks)),
            maxX - minX + 1, maxY - minY + 1, f(100.0 * opaque / (w * h)));
    }

    // ---- sheet composition -------------------------------------------------------------
    public static Bitmap NewSheet(int w, int h, int bg)
    {
        var b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(b)) g.Clear(Color.FromArgb(255, Color.FromArgb(bg)));
        return b;
    }

    static Graphics G(Bitmap b)
    {
        var g = Graphics.FromImage(b);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.SmoothingMode = SmoothingMode.None;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        return g;
    }

    // Draws the first frame (frameW) of a sprite at integer scale with its bottom-left at (x, yBottom).
    public static void DrawSprite(Bitmap sheet, string path, int frameW, int x, int yBottom, int scale)
    {
        using (var src = new Bitmap(path))
        using (var g = G(sheet))
        {
            int fw = frameW > 0 ? Math.Min(frameW, src.Width) : src.Width, fh = src.Height;
            g.DrawImage(src, new Rectangle(x, yBottom - fh * scale, fw * scale, fh * scale), new Rectangle(0, 0, fw, fh), GraphicsUnit.Pixel);
        }
    }

    // Draws a region of another image (e.g. an in-game capture) scaled by an integer factor.
    public static void DrawRegion(Bitmap sheet, string path, int sx, int sy, int sw, int sh, int x, int y, int scale)
    {
        using (var src = new Bitmap(path))
        using (var g = G(sheet))
            g.DrawImage(src, new Rectangle(x, y, sw * scale, sh * scale), new Rectangle(sx, sy, sw, sh), GraphicsUnit.Pixel);
    }

    public static void Rect(Bitmap sheet, int x, int y, int w, int h, int argb, bool fill)
    {
        using (var g = G(sheet))
        {
            if (fill) using (var br = new SolidBrush(Color.FromArgb(argb))) g.FillRectangle(br, x, y, w, h);
            else using (var p = new Pen(Color.FromArgb(argb), 1)) g.DrawRectangle(p, x, y, w - 1, h - 1);
        }
    }

    public static void Text(Bitmap sheet, string s, int x, int y, float px, int argb, bool bold)
    {
        using (var g = G(sheet))
        using (var font = new Font("Consolas", px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
        using (var br = new SolidBrush(Color.FromArgb(argb)))
            g.DrawString(s, font, br, x, y);
    }

    public static void Save(Bitmap sheet, string path) { sheet.Save(path, ImageFormat.Png); }

    // ---- mock-up helpers (density question pictures) -------------------------------------
    static int NearestPal(int r, int g, int b)
    {
        int best = int.MaxValue, pick = 0;
        foreach (var p in Pal)
        {
            int dr = r - ((p >> 16) & 255), dg = g - ((p >> 8) & 255), db = b - (p & 255);
            int d = dr * dr + dg * dg + db * db;
            if (d < best) { best = d; pick = p; }
        }
        return pick;
    }

    // Box-filter a region of src down to ow x oh (alpha coverage >= 50% = opaque), optionally snapped to the palette.
    public static Bitmap BoxDown(string path, int rx, int ry, int rw, int rh, int ow, int oh, bool quantize)
    {
        int W, H; var px = Load(path, out W, out H);
        var dst = new Bitmap(ow, oh, PixelFormat.Format32bppArgb);
        for (int y = 0; y < oh; y++)
            for (int x = 0; x < ow; x++)
            {
                int x0 = rx + x * rw / ow, x1 = rx + (x + 1) * rw / ow, y0 = ry + y * rh / oh, y1 = ry + (y + 1) * rh / oh;
                double r = 0, g = 0, b = 0, a = 0; int n = 0;
                for (int yy = y0; yy < Math.Max(y1, y0 + 1); yy++)
                    for (int xx = x0; xx < Math.Max(x1, x0 + 1); xx++)
                    {
                        int c = px[Math.Min(H - 1, yy) * W + Math.Min(W - 1, xx)]; double al = ((c >> 24) & 255) / 255.0;
                        r += ((c >> 16) & 255) * al; g += ((c >> 8) & 255) * al; b += (c & 255) * al; a += al; n++;
                    }
                if (a / n < 0.5) { dst.SetPixel(x, y, Color.Transparent); continue; }
                int ri = (int)(r / a), gi = (int)(g / a), bi = (int)(b / a);
                int rgb = quantize ? NearestPal(ri, gi, bi) : ((ri << 16) | (gi << 8) | bi);
                dst.SetPixel(x, y, Color.FromArgb(255, Color.FromArgb(rgb)));
            }
        return dst;
    }

    // Smooth (bicubic) resize of a region: the "painted, no visible pixels" stand-in.
    public static Bitmap Smooth(string path, int rx, int ry, int rw, int rh, int ow, int oh)
    {
        var dst = new Bitmap(ow, oh, PixelFormat.Format32bppArgb);
        using (var src = new Bitmap(path))
        using (var g = Graphics.FromImage(dst))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, new Rectangle(0, 0, ow, oh), new Rectangle(rx, ry, rw, rh), GraphicsUnit.Pixel);
        }
        return dst;
    }

    public static Bitmap Frame(string path, int frameW, int frame)
    {
        using (var src = new Bitmap(path))
        {
            int fw = frameW > 0 ? frameW : src.Width;
            var dst = new Bitmap(fw, src.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dst)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(src, new Rectangle(0, 0, fw, src.Height), new Rectangle(frame * fw, 0, fw, src.Height), GraphicsUnit.Pixel); }
            return dst;
        }
    }

    // Draw a bitmap at (x, y) top-left; scale may be fractional (nearest) to show uneven pixels on purpose.
    public static void DrawBmp(Bitmap sheet, Bitmap bmp, int x, int y, double scale)
    {
        using (var g = G(sheet))
            g.DrawImage(bmp, new Rectangle(x, y, (int)Math.Round(bmp.Width * scale), (int)Math.Round(bmp.Height * scale)), new Rectangle(0, 0, bmp.Width, bmp.Height), GraphicsUnit.Pixel);
    }

    // Tile frames of a 32x32 strip over cols x rows cells (seeded random frame per cell).
    public static Bitmap Tiles(string path, int tile, int frames, int cols, int rows, int seed)
    {
        var dst = new Bitmap(cols * tile, rows * tile, PixelFormat.Format32bppArgb);
        var rnd = new Random(seed);
        using (var src = new Bitmap(path))
        using (var g = G(dst))
            for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++)
                    g.DrawImage(src, new Rectangle(c * tile, r * tile, tile, tile), new Rectangle(rnd.Next(frames) * tile, 0, tile, tile), GraphicsUnit.Pixel);
        return dst;
    }

    public static void Paste(Bitmap dst, Bitmap src, int x, int y)
    {
        using (var g = G(dst)) g.DrawImage(src, new Rectangle(x, y, src.Width, src.Height), new Rectangle(0, 0, src.Width, src.Height), GraphicsUnit.Pixel);
    }

    // Downscale a whole image by an integer factor with nearest sampling (for small Hub pictures).
    public static void Shrink(string src, string dst, int factor)
    {
        using (var s = new Bitmap(src))
        using (var d = new Bitmap(s.Width / factor, s.Height / factor, PixelFormat.Format32bppArgb))
        {
            using (var g = G(d)) g.DrawImage(s, new Rectangle(0, 0, d.Width, d.Height), new Rectangle(0, 0, d.Width * factor, d.Height * factor), GraphicsUnit.Pixel);
            d.Save(dst, ImageFormat.Png);
        }
    }
}
