using System.Collections.Generic;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>CPU pixel image with a top-left origin (the way ART-SPEC counts texels). Bake-time only: never per frame.</summary>
    public sealed class PixelImage
    {
        public readonly int w, h;
        public readonly Color32[] px;
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public PixelImage(int w, int h) { this.w = w; this.h = h; px = new Color32[w * h]; }
        public PixelImage(PixelImage src) { w = src.w; h = src.h; px = (Color32[])src.px.Clone(); }

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;
        public Color32 Get(int x, int y) => In(x, y) ? px[y * w + x] : Clear;
        public void Set(int x, int y, Color32 c) { if (In(x, y)) px[y * w + x] = c; }

        /// <summary>Pixels of a sprite's full rect. Readable textures are read directly; others go through a temporary
        /// RenderTexture (like GameCursor). Null without a graphics device (batch -nographics): callers skip animation.</summary>
        public static PixelImage FromSprite(Sprite s)
        {
            if (s == null || s.texture == null) return null;
            var tex = s.texture; var r = s.rect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height), rx = Mathf.RoundToInt(r.x), ry = Mathf.RoundToInt(r.y);
            Color32[] raw; int rw;
            if (tex.isReadable)
            {
                raw = tex.GetPixels32(); rw = tex.width;
            }
            else
            {
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
                var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var prev = RenderTexture.active;
                Texture2D tmp = null;
                try
                {
                    Graphics.Blit(tex, rt);
                    RenderTexture.active = rt;
                    tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    tmp.ReadPixels(new Rect(rx, ry, w, h), 0, 0);
                    tmp.Apply(false, false);
                    raw = tmp.GetPixels32(); rw = w; rx = 0; ry = 0;
                }
                finally
                {
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                    if (tmp != null) Object.Destroy(tmp);
                }
            }
            var img = new PixelImage(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    img.px[y * w + x] = raw[(ry + h - 1 - y) * rw + rx + x];
            return img;
        }

        /// <summary>Unity texture (point filter, clamp) of this image.</summary>
        public Texture2D ToTexture(string name)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
            var flip = new Color32[w * h];
            for (int y = 0; y < h; y++) System.Array.Copy(px, y * w, flip, (h - 1 - y) * w, w);
            t.SetPixels32(flip);
            t.Apply(false, true);
            return t;
        }

        /// <summary>Sprite with the given pivot in texels (top-left origin) and PPU; FullRect mesh so the texel grid is kept.</summary>
        public Sprite ToSprite(string name, float ppu, Vector2 pivotTexelTopLeft)
        {
            var t = ToTexture(name);
            var pv = new Vector2(pivotTexelTopLeft.x / w, 1f - pivotTexelTopLeft.y / h);
            var s = Sprite.Create(t, new Rect(0, 0, w, h), pv, ppu, 0, SpriteMeshType.FullRect);
            s.name = name;
            return s;
        }
    }

    /// <summary>Bake-time pixel operations of the code animation layer (deform frames, overlay sprites, layer rotations).</summary>
    public static class CodeAnimBake
    {
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public static RectInt Rect(CodeAnimEffect e, PixelImage img)
        {
            if (!e.HasRect) return new RectInt(0, 0, img.w, img.h);
            var r = new RectInt(e.rect[0], e.rect[1], e.rect[2], e.rect[3]);
            r.xMin = Mathf.Max(0, r.xMin); r.yMin = Mathf.Max(0, r.yMin);
            r.xMax = Mathf.Min(img.w, r.xMax); r.yMax = Mathf.Min(img.h, r.yMax);
            return r;
        }

        public static bool[] Mask(CodeAnimEffect e, PixelImage img, RectInt r)
        {
            var m = new bool[img.w * img.h];
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                    m[y * img.w + x] = e.InMask(img.Get(x, y));
            return m;
        }

        /// <summary>Offset of step s (array wraps; empty = 0).</summary>
        public static int Off(int[] offs, int s) => offs == null || offs.Length == 0 ? 0 : offs[((s % offs.Length) + offs.Length) % offs.Length];

        /// <summary>Apply one baked effect at step s onto img (in place). The source pixels are read from a copy taken first.</summary>
        public static void ApplyBaked(CodeAnimEffect e, PixelImage img, int s)
        {
            switch (e.kind)
            {
                case CodeAnimKind.Sway: Sway(e, img, s); break;
                case CodeAnimKind.Shift: Shift(e, img, s); break;
                case CodeAnimKind.Scroll: Scroll(e, img, s); break;
                case CodeAnimKind.Line: Line(e, img, s); break;
            }
        }

        /// <summary>Rows (axis x) or columns (axis y) of the masked part shift by offset·weight, weight growing linearly from
        /// the pivot row to the tip (the far end of the rect from the pivot). Pivot side stays put.</summary>
        static void Sway(CodeAnimEffect e, PixelImage img, int s)
        {
            int off = Off(e.offsets, s);
            if (off == 0) return;
            var r = Rect(e, img);
            bool alongX = e.axis != "y";
            int pv = e.pivot != null && e.pivot.Length >= 2 ? (alongX ? e.pivot[1] : e.pivot[0]) : (alongX ? r.yMax - 1 : r.xMin);
            int lo = alongX ? r.yMin : r.xMin, hi = (alongX ? r.yMax : r.xMax) - 1;
            int tip = Mathf.Abs(pv - lo) >= Mathf.Abs(hi - pv) ? lo : hi;
            float span = Mathf.Max(1, Mathf.Abs(tip - pv));
            Move(e, img, r, (x, y) =>
            {
                int k = alongX ? y : x;
                float wgt = (tip < pv ? pv - k : k - pv) / span;
                if (wgt <= 0f) return Vector2Int.zero;
                int d = Mathf.RoundToInt(off * Mathf.Min(1f, wgt));
                return alongX ? new Vector2Int(d, 0) : new Vector2Int(0, d);
            });
        }

        static void Shift(CodeAnimEffect e, PixelImage img, int s)
        {
            int off = Off(e.offsets, s);
            if (off == 0) return;
            var r = Rect(e, img);
            var d = e.axis == "x" ? new Vector2Int(off, 0) : new Vector2Int(0, off);
            Move(e, img, r, (x, y) => d);
        }

        /// <summary>Move masked pixels by a per-pixel displacement. Vacated pixels take the first non-part pixel found on the
        /// trailing side (above a part that moves down: the screw stretches; background above a nodding head: transparent).</summary>
        static void Move(CodeAnimEffect e, PixelImage img, RectInt r, System.Func<int, int, Vector2Int> disp)
        {
            var src = new PixelImage(img);
            var m = Mask(e, src, r);
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    if (!m[y * src.w + x]) continue;
                    var d = disp(x, y);
                    if (d == Vector2Int.zero) continue;
                    int sx = -System.Math.Sign(d.x), sy = -System.Math.Sign(d.y);
                    Color32 fill = Clear;
                    for (int k = 1; k <= 12; k++)
                    {
                        int qx = x + sx * k, qy = y + sy * k;
                        if (!src.In(qx, qy)) break;
                        if (!m[qy * src.w + qx]) { fill = src.Get(qx, qy); break; }
                    }
                    img.Set(x, y, fill);
                }
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    if (!m[y * src.w + x]) continue;
                    var d = disp(x, y);
                    if (d == Vector2Int.zero) { img.Set(x, y, src.Get(x, y)); continue; }
                    img.Set(x + d.x, y + d.y, src.Get(x, y));
                }
        }

        /// <summary>Masked pixels scroll along their column (axis y) or row (axis x) inside each unbroken run, wrapping.</summary>
        static void Scroll(CodeAnimEffect e, PixelImage img, int s)
        {
            int shift = s * (e.speed == 0 ? 1 : e.speed);
            if (shift == 0) return;
            var r = Rect(e, img);
            var src = new PixelImage(img);
            var m = Mask(e, src, r);
            bool col = e.axis != "x";
            int outer0 = col ? r.xMin : r.yMin, outer1 = col ? r.xMax : r.yMax, inner0 = col ? r.yMin : r.xMin, inner1 = col ? r.yMax : r.xMax;
            var run = new List<int>();
            for (int o = outer0; o < outer1; o++)
            {
                int i = inner0;
                while (i < inner1)
                {
                    run.Clear();
                    while (i < inner1 && m[(col ? i : o) * src.w + (col ? o : i)]) { run.Add(i); i++; }
                    if (run.Count > 1)
                        for (int k = 0; k < run.Count; k++)
                        {
                            int from = run[(((k - shift) % run.Count) + run.Count) % run.Count];
                            var c = col ? src.Get(o, from) : src.Get(from, o);
                            if (col) img.Set(o, run[k], c); else img.Set(run[k], o, c);
                        }
                    i++;
                }
            }
        }

        /// <summary>A 1-px line from points[0..1] to points[2..3]; its end moves sideways by the step offset, the bend growing
        /// with t² (the top hangs still, the lower half sways).</summary>
        static void Line(CodeAnimEffect e, PixelImage img, int s)
        {
            if (e.points == null || e.points.Length < 4) return;
            int x0 = e.points[0], y0 = e.points[1], x1 = e.points[2], y1 = e.points[3];
            int off = Off(e.offsets, s);
            Color32 c = e.colA;
            int n = Mathf.Max(Mathf.Abs(y1 - y0), Mathf.Abs(x1 - x0));
            int px = x0, py = y0;
            for (int i = 0; i <= n; i++)
            {
                float t = n == 0 ? 0f : i / (float)n;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t) + off * t * t), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                // keep the line connected when the bend jumps a column
                while (i > 0 && Mathf.Abs(x - px) > 1) { px += System.Math.Sign(x - px); img.Set(px, py, c); }
                img.Set(x, y, c);
                px = x; py = y;
            }
        }

        // ---- overlay sprites

        /// <summary>Masked still pixels as an overlay image: shaded white (tint carries the colour) or their own colours.</summary>
        public static PixelImage Cut(CodeAnimEffect e, PixelImage still, RectInt r, bool ownColours)
        {
            var o = new PixelImage(r.width, r.height);
            for (int y = 0; y < r.height; y++)
                for (int x = 0; x < r.width; x++)
                {
                    var c = still.Get(r.x + x, r.y + y);
                    if (!e.InMask(c)) continue;
                    if (ownColours) { c.a = 255; o.Set(x, y, c); continue; }
                    Color.RGBToHSV(c, out _, out _, out float v);
                    byte l = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * (0.6f + 0.4f * v)), 0, 255);
                    o.Set(x, y, new Color32(l, l, l, 255));
                }
            return o;
        }

        static Color32 C(int hex) => new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        // ART-SPEC §1.3 palette entries used by the generators
        static readonly Color32 P01 = C(0x0D0F14), P05 = C(0x7A809A), P06 = C(0xB7BCCD), P07 = C(0xE8ECF3), P17 = C(0x8C1D22), P19 = C(0xEE6A4A),
            P24 = C(0xF2C94C), P25 = C(0xFFF0A0), P30 = C(0xFF8A1F), P33 = C(0xC2521C), P40 = C(0xF4E6C8);

        /// <summary>Pixel-art flame tongue: w wide at the base, narrowing to a 1–2 px pointed tip with curved sides; core P25/P24
        /// at the base, P30/P33 mid, P19/P17 edge. variant bends the tip.</summary>
        public static PixelImage Flame(int w, int h, int variant)
        {
            var o = new PixelImage(w, h);
            float lean = (variant % 3 - 1) * 0.6f;
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)Mathf.Max(1, h - 1);        // top-left origin: 0 at the tip (top row), 1 at the base (bottom row)
                float half = (w * 0.5f) * Mathf.Sqrt(Mathf.Clamp01(t)) * (0.35f + 0.65f * Mathf.Clamp01(t * 1.6f));
                half = Mathf.Max(half, 0.5f);
                float cx = (w - 1) * 0.5f + lean * (1f - t) * 2f;
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - cx);
                    if (dx > half) continue;
                    float edge = dx / Mathf.Max(0.5f, half);
                    Color32 c = edge > 0.72f ? (t < 0.5f ? P17 : P19) : t > 0.55f && edge < 0.45f ? (t > 0.8f ? P25 : P24) : (t > 0.3f ? P30 : P33);
                    o.Set(x, y, c);
                }
            }
            return o;
        }

        /// <summary>Round, lumpy smoke puff in two greys (lit P06 upper left, P05 lower right), no straight edge.</summary>
        public static PixelImage Puff(int w, int h, int seed)
        {
            var o = new PixelImage(w, h);
            float rx = w * 0.5f, ry = h * 0.5f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - rx) / rx, dy = (y + 0.5f - ry) / ry;
                    float d = dx * dx + dy * dy;
                    float lump = 0.12f * Mathf.Sin(Mathf.Atan2(dy, dx) * 3f + seed);
                    if (d > 0.95f + lump) continue;
                    bool lit = dx + dy < -0.15f;
                    o.Set(x, y, lit ? P06 : (d > 0.6f && dx + dy > 0.3f ? P05 : P06));
                }
            return o;
        }

        /// <summary>4-point star glint (odd size): P25 core, P24 arms.</summary>
        public static PixelImage Star(int n)
        {
            n = Mathf.Max(3, n | 1);
            var o = new PixelImage(n, n); int c = n / 2;
            for (int i = 0; i < n; i++) { o.Set(c, i, P24); o.Set(i, c, P24); }
            o.Set(c, c, P25);
            if (n >= 5) { o.Set(c - 1, c, P25); o.Set(c + 1, c, P25); o.Set(c, c - 1, P25); o.Set(c, c + 1, P25); }
            return o;
        }

        /// <summary>Sheet of paper: fill (default P40) with a 1-px outline (default P05).</summary>
        public static PixelImage Sheet(int w, int h, Color fill, Color edge)
        {
            var o = new PixelImage(w, h);
            Color32 f = fill, k = edge; f.a = 255; k.a = 255;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    o.Set(x, y, x == 0 || y == 0 || x == w - 1 || y == h - 1 ? k : f);
            return o;
        }

        public static Color DefaultSheetFill => (Color)(Color32)P40;
        public static Color DefaultSheetEdge => (Color)(Color32)P05;
        public static Color DefaultLine => (Color)(Color32)P05;

        /// <summary>Nearest-neighbour rotation (degrees, counter-clockwise on screen) and horizontal squash of an image about a
        /// pivot texel, into the same canvas: texels stay on the grid (no smoothing, no sub-pixel shimmer).</summary>
        public static PixelImage Transform(PixelImage src, Vector2 pivot, float degrees, float scaleX)
        {
            var o = new PixelImage(src.w, src.h);
            float a = degrees * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            float sx = Mathf.Abs(scaleX) < 0.05f ? 0.05f : scaleX;
            for (int y = 0; y < o.h; y++)
                for (int x = 0; x < o.w; x++)
                {
                    // destination → source: undo squash, then rotate back (top-left y-down coordinates: screen CCW = -angle)
                    float dx = (x + 0.5f - pivot.x - 0.5f) / sx, dy = y + 0.5f - pivot.y - 0.5f;
                    float qx = cs * dx - sn * dy, qy = sn * dx + cs * dy;
                    int ix = Mathf.FloorToInt(qx + pivot.x + 0.5f), iy = Mathf.FloorToInt(qy + pivot.y + 0.5f);
                    var c = src.Get(ix, iy);
                    if (c.a >= 128) o.Set(x, y, c);
                }
            return o;
        }

        // ---- ellipse spin (a wheel seen from the side: its round face shows as an ellipse in the still)

        /// <summary>Circle space → screen matrix (row-major m00, m01, m10, m11 in x/y/z/w; y down) of an ellipse spec:
        /// <c>[radiusX, radiusY]</c> (axis-aligned, normalised so the long axis keeps its length) or <c>[m00, m01, m10, m11]</c>.
        /// False (identity) when the spec is missing or degenerate.</summary>
        public static bool EllipseMatrix(float[] spec, out Vector4 m)
        {
            m = new Vector4(1f, 0f, 0f, 1f);
            if (spec == null) return false;
            if (spec.Length >= 4) m = new Vector4(spec[0], spec[1], spec[2], spec[3]);
            else if (spec.Length >= 2 && spec[0] > 0f && spec[1] > 0f)
            {
                float r = Mathf.Max(spec[0], spec[1]);
                m = new Vector4(spec[0] / r, 0f, 0f, spec[1] / r);
            }
            else return false;
            if (Mathf.Abs(m.x * m.w - m.y * m.z) > 1e-4f) return true;
            m = new Vector4(1f, 0f, 0f, 1f);
            return false;
        }

        static Vector4 Inverse(Vector4 m)
        {
            float det = m.x * m.w - m.y * m.z;
            return new Vector4(m.w / det, -m.y / det, -m.z / det, m.x / det);
        }

        static Vector4 Mul(Vector4 a, Vector4 b) => new Vector4(a.x * b.x + a.y * b.z, a.x * b.y + a.y * b.w, a.z * b.x + a.w * b.z, a.z * b.y + a.w * b.w);

        /// <summary>Distance of texel (x, y) from the pivot in circle space (the un-squashed wheel) under the inverse matrix.</summary>
        static float CircleR(Vector4 inv, Vector2 pivot, float x, float y)
        {
            float dx = x - pivot.x, dy = y - pivot.y;
            float cx = inv.x * dx + inv.y * dy, cy = inv.z * dx + inv.w * dy;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }

        /// <summary>Radius (circle space) of the wheel: the farthest opaque texel of the layer from the pivot, + half a texel.
        /// The ellipse mask of a frame = every texel within this radius.</summary>
        public static float EllipseRadius(PixelImage src, Vector2 pivot, float[] spec)
        {
            EllipseMatrix(spec, out var m);
            var inv = Inverse(m);
            float r = 0f;
            for (int y = 0; y < src.h; y++)
                for (int x = 0; x < src.w; x++)
                    if (src.px[y * src.w + x].a >= 128) r = Mathf.Max(r, CircleR(inv, pivot, x, y));
            return r + 0.5f;
        }

        /// <summary>True when texel (x, y) lies inside the wheel's ellipse mask of the given circle-space radius.</summary>
        public static bool InEllipse(Vector2 pivot, float[] spec, float radius, float x, float y)
        {
            EllipseMatrix(spec, out var m);
            return CircleR(Inverse(m), pivot, x, y) <= radius;
        }

        /// <summary>
        /// Turn a side-view wheel layer by <paramref name="degrees"/> (counter-clockwise on screen) in the plane of its ellipse:
        /// un-squash to a circle about the pivot, rotate, squash back, as one nearest-neighbour lookup per destination texel
        /// (texels stay on the grid, no smoothing). 0° (and whole turns) return the source pixel for pixel. With
        /// <paramref name="clean"/>, transparent texels inside the ellipse mask that have at least 3 opaque 4-neighbours (the
        /// 1-px holes nearest-neighbour leaves in thin rims and spokes) take the commonest neighbour colour.
        /// </summary>
        public static PixelImage EllipseTurn(PixelImage src, Vector2 pivot, float[] spec, float degrees, bool clean, float radius = -1f)
        {
            float turns = degrees / 360f;
            if (Mathf.Abs(turns - Mathf.Round(turns)) < 1e-6f) return new PixelImage(src);
            EllipseMatrix(spec, out var m);
            var inv = Inverse(m);
            float a = degrees * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            // destination → source: M · Rs(-θ) · M⁻¹ (top-left y-down coordinates: screen CCW by θ is [[c, s], [-s, c]])
            var t = Mul(m, Mul(new Vector4(cs, -sn, sn, cs), inv));
            var o = new PixelImage(src.w, src.h);
            for (int y = 0; y < o.h; y++)
                for (int x = 0; x < o.w; x++)
                {
                    float dx = x - pivot.x, dy = y - pivot.y;
                    float qx = t.x * dx + t.y * dy, qy = t.z * dx + t.w * dy;
                    var c = src.Get(Mathf.FloorToInt(qx + pivot.x + 0.5f), Mathf.FloorToInt(qy + pivot.y + 0.5f));
                    if (c.a >= 128) o.Set(x, y, c);
                }
            if (!clean) return o;
            if (radius < 0f) radius = EllipseRadius(src, pivot, spec);
            var raw = new PixelImage(o);
            for (int y = 0; y < o.h; y++)
                for (int x = 0; x < o.w; x++)
                {
                    if (raw.px[y * o.w + x].a >= 128 || CircleR(inv, pivot, x, y) > radius) continue;
                    var l = raw.Get(x - 1, y); var r = raw.Get(x + 1, y); var u = raw.Get(x, y - 1); var d = raw.Get(x, y + 1);
                    int n = (l.a >= 128 ? 1 : 0) + (r.a >= 128 ? 1 : 0) + (u.a >= 128 ? 1 : 0) + (d.a >= 128 ? 1 : 0);
                    if (n < 3) continue;
                    o.px[y * o.w + x] = Commonest(l, r, u, d);
                }
            return o;
        }

        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        /// <summary>The opaque colour that occurs most often among four neighbours (ties: the first in l, r, u, d order).</summary>
        static Color32 Commonest(Color32 l, Color32 r, Color32 u, Color32 d)
        {
            Color32 best = Clear; int bestN = 0;
            var c4 = new[] { l, r, u, d };
            for (int i = 0; i < 4; i++)
            {
                if (c4[i].a < 128) continue;
                int n = 0;
                for (int j = 0; j < 4; j++) if (c4[j].a >= 128 && Same(c4[i], c4[j])) n++;
                if (n > bestN) { best = c4[i]; bestN = n; }
            }
            return best;
        }

        /// <summary>
        /// Bake the frames of an ellipse spin: frame k = <see cref="EllipseTurn"/> by 360°·k/<paramref name="frames"/> (frame 0 =
        /// the rest layer), all cropped to one rect (<paramref name="crop"/>, still texels, top-left origin) that holds every
        /// opaque texel of every frame. Deterministic; bake time only.
        /// </summary>
        public static PixelImage[] EllipseSpinFrames(PixelImage src, Vector2 pivot, float[] spec, int frames, bool clean, out RectInt crop)
        {
            frames = Mathf.Max(1, frames);
            float radius = EllipseRadius(src, pivot, spec);
            var full = new PixelImage[frames];
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int k = 0; k < frames; k++)
            {
                full[k] = EllipseTurn(src, pivot, spec, 360f * k / frames, clean, radius);
                var f = full[k];
                for (int y = 0; y < f.h; y++)
                    for (int x = 0; x < f.w; x++)
                        if (f.px[y * f.w + x].a >= 128) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            }
            crop = x1 < 0 ? new RectInt(0, 0, 1, 1) : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            var o = new PixelImage[frames];
            for (int k = 0; k < frames; k++) o[k] = Crop(full[k], crop);
            return o;
        }

        /// <summary>Copy of a rect of an image (top-left origin).</summary>
        public static PixelImage Crop(PixelImage src, RectInt r)
        {
            var o = new PixelImage(r.width, r.height);
            for (int y = 0; y < r.height; y++)
                for (int x = 0; x < r.width; x++)
                    o.px[y * r.width + x] = src.Get(r.x + x, r.y + y);
            return o;
        }
    }
}
