using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// The Fishing Island's frozen pond: ONE shape function shared by the editor painter
    /// (<c>IslandDressingBuilder.EnsurePond</c> draws the pond sprite with <see cref="D"/>) and the Sim (the derived
    /// <see cref="ZoneKey"/> zone = every cell whose 32 x 32 pixels are all pond water). Fish and algae spawn only there
    /// and the Algae Farm (<c>waterOnly</c>) places only there (owner pick 2026-11-09 fish_zone "Only in the pond").
    /// Pure float maths that reproduces the editor's former Mathf code bit for bit, so the sprite does not change.
    /// </summary>
    public static class PondShape
    {
        /// <summary>Zone key of the derived pond cells (spawner "zone" in game-data.json; Algae Farm water rule).</summary>
        public const string ZoneKey = "pond";
        /// <summary>Pond sprite size in px (one Island cell = 32 px).</summary>
        public const int W = 448, H = 320, Cell = 32;
        /// <summary>The cell whose centre the pond sprite is centred on (Fishing Island grid row / col).</summary>
        public const int CentreRow = 46, CentreCol = 46;
        public const int Seed = 4242;

        /// <summary>Half-axes of the base ellipse in px.</summary>
        public static readonly float HX = (W * 0.5f - 8f) / 1.26f, HY = (H * 0.5f - 8f) / 1.26f;

        /// <summary>Shore wobble per angle (radius factor).</summary>
        public static float Rad(float a) =>
            1f + 0.12f * (float)Math.Sin(2 * a + 1.3f) + 0.07f * (float)Math.Sin(3 * a + 4.1f) + 0.04f * (float)Math.Sin(5 * a + 2.2f);

        /// <summary>Normalised distance of sprite pixel (x, y) (y up = north) from the pond centre; &lt; 1 = water.</summary>
        public static float D(int x, int y)
        {
            float dx = (x + 0.5f - W * 0.5f) / HX, dy = (y + 0.5f - H * 0.5f) / HY;
            float d = (float)Math.Sqrt(dx * dx + dy * dy) / Rad((float)Math.Atan2(dy, dx));
            return d + (Noise(x / 9f, y / 9f, Seed) - 0.5f) * 0.08f;
        }

        public static bool IsWaterPx(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && D(x, y) < 1f;

        /// <summary>True when every pixel of Island cell (row, col) is pond water.</summary>
        public static bool CellIsPond(int row, int col)
        {
            // sprite px of the cell's bottom-left: the sprite centre sits on the centre of (CentreRow, CentreCol); rows grow south
            int x0 = (col - CentreCol) * Cell + W / 2 - Cell / 2;
            int y0 = (CentreRow - row) * Cell + H / 2 - Cell / 2;
            if (x0 < 0 || y0 < 0 || x0 + Cell > W || y0 + Cell > H) return false;
            for (int y = y0; y < y0 + Cell; y++)
                for (int x = x0; x < x0 + Cell; x++)
                    if (D(x, y) >= 1f) return false;
            return true;
        }

        static List<ZoneRect> _rects;

        /// <summary>The pond cells as one rect per row run (row-major), cached.</summary>
        public static List<ZoneRect> Rects()
        {
            if (_rects != null) return _rects;
            var list = new List<ZoneRect>();
            int hr = H / Cell / 2 + 1, hc = W / Cell / 2 + 1;
            for (int r = CentreRow - hr; r <= CentreRow + hr; r++)
            {
                int start = -1;
                for (int c = CentreCol - hc; c <= CentreCol + hc + 1; c++)
                {
                    bool w = c <= CentreCol + hc && CellIsPond(r, c);
                    if (w && start < 0) start = c;
                    else if (!w && start >= 0) { list.Add(new ZoneRect(r, start, r, c - 1)); start = -1; }
                }
            }
            return _rects = list;
        }

        /// <summary>A fresh zone definition for <see cref="ZoneKey"/>.</summary>
        public static ZoneDef Zone() => new ZoneDef { key = ZoneKey, rects = new List<ZoneRect>(Rects()) };

        // ---- value noise (identical to the editor's IslandDressingBuilder.Hash / Noise) ----

        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519u);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        public static float Noise(float x, float y, int seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            return Lerp(Lerp(Hash(x0, y0, seed), Hash(x0 + 1, y0, seed), fx),
                Lerp(Hash(x0, y0 + 1, seed), Hash(x0 + 1, y0 + 1, seed), fx), fy);
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * (t < 0f ? 0f : t > 1f ? 1f : t);
    }
}
