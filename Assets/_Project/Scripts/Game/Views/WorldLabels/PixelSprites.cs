using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Small pixel-art sprites made in code at PPU 32 (1 texel = 1 world px), palette colours only
    /// (ART-SPEC 1.3): the world-label plate (9-slice pill), status shapes (VP-10: colour is always
    /// paired with a shape), have/need tick and cross, the 2-on / 2-off dash for overlay lines and
    /// 1-texel rings (VP-13). Everything is built once and cached; nothing allocates per frame.
    /// </summary>
    public static class PixelSprites
    {
        public const float Ppu = 32f;
        /// <summary>One texel in world units.</summary>
        public const float Texel = 1f / Ppu;

        // ---- palette (ART-SPEC 1.3) ----
        public static readonly Color32 P01 = Hex(0x0D0F14), P02 = Hex(0x1B1F2B), P04 = Hex(0x4A5068), P05 = Hex(0x7A809A),
            P06 = Hex(0xB7BCCD), P07 = Hex(0xE8ECF3), P10 = Hex(0x2F9A7A), P11 = Hex(0x5FCF9C), P18 = Hex(0xC8322B), P19 = Hex(0xEE6A4A),
            P23 = Hex(0xD9A45C), P24 = Hex(0xF2C94C), P25 = Hex(0xFFF0A0), P28 = Hex(0x5ED4E8), P30 = Hex(0xFF8A1F);

        static Color32 Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        public static Color C(Color32 c, float a = 1f) { Color k = c; k.a = a; return k; }

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); rings.Clear(); _plate = null; _pixel = null; _dash = null; _gear = null; _bowl = null; _blocked = null; _box = null; _idle = null; _tick = null; _cross = null; _ok = null; }

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PixelSprite" };
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        /// <summary>Sprite from rows of chars (top row first). Legend: '.' clear, 'o' P01 ink, others via <paramref name="map"/>.
        /// <paramref name="outline"/> adds a 1-texel P01 ring around every filled texel (canvas grows by 2).</summary>
        static Sprite FromRows(string key, string[] rows, System.Func<char, Color32> map, bool outline, Vector4 border = default, Vector2? pivot = null)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            int h = rows.Length, w = rows[0].Length, pad = outline ? 1 : 0;
            int W = w + 2 * pad, H = h + 2 * pad;
            var px = new Color32[W * H];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char ch = rows[y][x];
                    if (ch == '.' || ch == ' ') continue;
                    px[(H - 1 - (y + pad)) * W + x + pad] = ch == 'o' ? P01 : map(ch);
                }
            if (outline)
            {
                var src = (Color32[])px.Clone();
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        if (src[y * W + x].a != 0) continue;
                        bool near = false;
                        for (int dy = -1; dy <= 1 && !near; dy++)
                            for (int dx = -1; dx <= 1 && !near; dx++)
                            {
                                if ((dx != 0 && dy != 0)) continue;     // 4-neighbour ring keeps the corners soft
                                int nx = x + dx, ny = y + dy;
                                if (nx >= 0 && ny >= 0 && nx < W && ny < H && src[ny * W + nx].a != 0) near = true;
                            }
                        if (near) px[y * W + x] = P01;
                    }
            }
            var t = NewTex(W, H);
            t.SetPixels32(px); t.Apply(false, true);
            s = Sprite.Create(t, new Rect(0, 0, W, H), pivot ?? new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect, border);
            s.name = key;
            cache[key] = s;
            return s;
        }

        /// <summary>World-label plate: P01 at 80% with a 1-texel P04 border and clipped corners (9-slice, border 2).</summary>
        static Sprite _plate;
        public static Sprite Plate => _plate != null ? _plate : _plate = FromRows("plate", new[]
        {
            ".bbb.",
            "bfffb",
            "bfffb",
            "bfffb",
            ".bbb.",
        }, ch => ch == 'b' ? P04 : new Color32(P01.r, P01.g, P01.b, 230), false, new Vector4(2, 2, 2, 2));

        /// <summary>Filled 1x1 white texel (bars, cells, rims).</summary>
        static Sprite _pixel;
        public static Sprite Pixel => _pixel != null ? _pixel : _pixel = FromRows("pixel", new[] { "w" }, _ => P07Raw, false);
        static readonly Color32 P07Raw = new Color32(255, 255, 255, 255);

        /// <summary>Dash for overlay lines: 2 texels on, 2 off, white (tint with the renderer colour). Tiled along x.</summary>
        static Sprite _dash;
        public static Sprite Dash => _dash != null ? _dash : _dash = FromRows("dash", new[] { "ww.." }, _ => P07Raw, false, default, new Vector2(0.5f, 0.5f));

        // ---- status shapes (9x9 + 1-texel ink outline = 11x11) ----
        static Color32 Map(char ch, Color32 fill)
        {
            switch (ch)
            {
                case 'f': return fill;
                case 'w': return P07;
                case 'd': return P02;
                case 'g': return P24;
                default: return fill;
            }
        }

        /// <summary>Working: a jade gear.</summary>
        static Sprite _gear;
        public static Sprite Gear => _gear != null ? _gear : _gear = FromRows("st_gear", new[]
        {
            "...f.f...",
            ".f.fff.f.",
            "..fffff..",
            "fffdddfff",
            ".ffd.dff.",
            "fffdddfff",
            "..fffff..",
            ".f.fff.f.",
            "...f.f...",
        }, ch => Map(ch, P11), true);

        /// <summary>Starved (an input is missing): an empty bowl.</summary>
        static Sprite _bowl;
        public static Sprite Bowl => _bowl != null ? _bowl : _bowl = FromRows("st_bowl", new[]
        {
            ".........",
            ".........",
            "fffffffff",
            "fdddddddf",
            "fdddddddf",
            ".fdddddf.",
            "..fffff..",
            ".........",
            ".........",
        }, ch => Map(ch, P19), true);

        /// <summary>Blocked / no fuel: a red square with "!".</summary>
        static Sprite _blocked;
        public static Sprite Blocked => _blocked != null ? _blocked : _blocked = FromRows("st_block", new[]
        {
            "fffffffff",
            "ffffwffff",
            "ffffwffff",
            "ffffwffff",
            "ffffwffff",
            "fffffffff",
            "ffffwffff",
            "fffffffff",
            ".........",
        }, ch => Map(ch, P18), true);

        /// <summary>Output full: an amber crate.</summary>
        static Sprite _box;
        public static Sprite Box => _box != null ? _box : _box = FromRows("st_box", new[]
        {
            ".........",
            "fffffffff",
            "fdddddddf",
            "fffffffff",
            "ffffgffff",
            "fffffffff",
            "fffffffff",
            "fffffffff",
            ".........",
        }, ch => Map(ch, P30), true);

        /// <summary>Idle / nothing to do: a hollow grey ring.</summary>
        static Sprite _idle;
        public static Sprite Idle => _idle != null ? _idle : _idle = FromRows("st_idle", new[]
        {
            ".........",
            "...fff...",
            "..f...f..",
            ".f.....f.",
            ".f.....f.",
            ".f.....f.",
            "..f...f..",
            "...fff...",
            ".........",
        }, ch => Map(ch, P06), true);

        /// <summary>Have >= need: small tick.</summary>
        static Sprite _tick;
        public static Sprite Tick => _tick != null ? _tick : _tick = FromRows("tick", new[]
        {
            ".....f",
            "....ff",
            "f..ff.",
            "ffff..",
            ".ff...",
        }, ch => Map(ch, P11), true);

        /// <summary>Valid / ready: a jade disc with a white tick (9x9 + outline, same size as the status shapes).</summary>
        static Sprite _ok;
        public static Sprite Ok => _ok != null ? _ok : _ok = FromRows("st_ok", new[]
        {
            "..fffff..",
            ".fffffff.",
            "fffffffwf",
            "ffffffwwf",
            "fwfffwwff",
            "fwwfwwfff",
            "ffwwwffff",
            ".ffwfffff",
            "..fffff..",
        }, ch => Map(ch, P10), true);

        /// <summary>Have &lt; need: small cross.</summary>
        static Sprite _cross;
        public static Sprite Cross => _cross != null ? _cross : _cross = FromRows("cross", new[]
        {
            "f...f",
            ".f.f.",
            "..f..",
            ".f.f.",
            "f...f",
        }, ch => Map(ch, P19), true);

        /// <summary>Shape for a building state (never colour alone).</summary>
        public static Sprite ForState(BuildingState? s)
        {
            if (s == null) return Idle;
            switch (s.Value)
            {
                case BuildingState.Working: return Gear;
                case BuildingState.Starved: return Bowl;
                case BuildingState.NoFuel: return Blocked;
                case BuildingState.Full: return Box;
                default: return Idle;
            }
        }

        // ---- 1-texel rings (midpoint circle), solid or dashed 2 on / 2 off along the arc ----
        static readonly Dictionary<int, Sprite> rings = new Dictionary<int, Sprite>();

        /// <summary>A white ring of radius <paramref name="r"/> texels, <paramref name="thick"/> texels wide (centre pivot, PPU 32). Cached per (r, thick, dashed).</summary>
        public static Sprite Ring(int r, int thick = 1, bool dashed = true)
        {
            r = Mathf.Clamp(r, 2, 1024); thick = Mathf.Clamp(thick, 1, 4);
            int key = (r << 4) | (thick << 1) | (dashed ? 1 : 0);
            if (rings.TryGetValue(key, out var s) && s != null) return s;
            int size = 2 * (r + thick) + 3, c = size / 2;
            var px = new Color32[size * size];
            var on = new Color32(255, 255, 255, 255);
            for (int t = 0; t < thick; t++)
            {
                int rr = r + t;
                int x = rr, y = 0, err = 1 - rr;
                while (x >= y)
                {
                    Plot(px, size, c, x, y, rr, dashed, on); Plot(px, size, c, y, x, rr, dashed, on);
                    Plot(px, size, c, -x, y, rr, dashed, on); Plot(px, size, c, -y, x, rr, dashed, on);
                    Plot(px, size, c, x, -y, rr, dashed, on); Plot(px, size, c, y, -x, rr, dashed, on);
                    Plot(px, size, c, -x, -y, rr, dashed, on); Plot(px, size, c, -y, -x, rr, dashed, on);
                    y++;
                    if (err < 0) err += 2 * y + 1; else { x--; err += 2 * (y - x) + 1; }
                }
            }
            var tex = NewTex(size, size);
            tex.SetPixels32(px); tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            s.name = "ring" + r;
            rings[key] = s;
            return s;
        }

        static void Plot(Color32[] px, int size, int c, int dx, int dy, int r, bool dashed, Color32 on)
        {
            if (dashed)
            {
                float a = Mathf.Atan2(dy, dx); if (a < 0) a += 2f * Mathf.PI;
                int seg = (int)(a * r / 2f);
                if ((seg & 1) == 1) return;
            }
            int x = c + dx, y = c + dy;
            if (x >= 0 && y >= 0 && x < size && y < size) px[y * size + x] = on;
        }
    }
}
