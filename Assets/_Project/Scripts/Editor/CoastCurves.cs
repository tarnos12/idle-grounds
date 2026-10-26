using System.Collections.Generic;
using System.IO;
using IdleGrounds.Game;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Curved Island coasts (owner 2026-10-12, coast redo #4: "round the corners into curves").
    ///
    /// MODEL. The coast is cut from a smooth scalar field sampled on the cell corners (the seed outline, blurred wide
    /// enough that corners become arcs of 4-8+ cells): a cell with all four corners on land is a full coast cell, a
    /// cell with none is sky, any other cell is crossed by the coastline as ONE straight segment between two points on
    /// its edges (marching squares). The crossing points are interpolated from the field and quantised to 2 px, so the
    /// outline is a continuous polyline that turns a little at every cell edge: straight runs, 1:16 .. 16:1 slopes and
    /// 45° all mix freely, which reads as a smooth curve at play zoom (no 90° corners, no stairs, no spikes).
    /// Neighbouring cells share their crossing points, so every joint is exact by construction.
    ///
    /// PIECES (composed at build time from the delivered frames, pixel copies, integer scale, no filtering):
    /// <list type="bullet">
    /// <item>Ground (<see cref="CoastShapeTile"/>, Coast tilemap): the cell's land part, ground fill + the blob's two rim
    ///   colours along the cut (1 px each, measured along the dominant axis so slopes stay crisp).</item>
    /// <item>Wall (CliffRim tilemap): under every SOUTH-facing segment, the repaired 3/4 cliff-wall frame columns, each
    ///   column hung from the coast pixel above it (a per-column shear), so the wall follows any slope or curve and its
    ///   silhouette narrows naturally where the coast turns to face east / west. Frame variant per cell COLUMN (frames
    ///   tile horizontally, so columns from different rows still join).</item>
    /// <item>Lip (CoastLip, Dressing): the recoloured grass lip re-laid along N / W / E-facing segments (4 px overhang).</item>
    /// <item>Side (CoastSide, Dressing): the W / E 3/4 side face as a horizontal band beside W / E-facing coast,
    ///   tapering to nothing as the coast turns north or south (behind the walls, which take over facing south).</item>
    /// </list>
    /// Lip and side pieces depend on the neighbours, so they are de-duplicated by content. Every set is one atlas PNG
    /// (Art/Islands/Coast) + one tile asset with sub-assets (Tiles/Coast) per biome.
    /// Art hooks: <c>island_&lt;biome&gt;_cliffwall_round_32x96_4f</c> (4 tileable wall frames drawn for curves) replaces the
    /// repaired straight mids when delivered.
    /// <c>island_&lt;biome&gt;_coast_edge_profile_32x8_2f</c> (Islands-052) replaces the recoloured lip (frame 0 on N / W / E, frame 1
    /// as the south rim over the wall top) and <c>island_&lt;biome&gt;_sidewall_round_32x32_4f</c> draws side faces of 11 px or less.
    /// </summary>
    public static class CoastCurves
    {
        public const int P = 32;
        const int Quantum = 2;                  // crossing positions snap to 2 px
        const int BlurR = 4, BlurPasses = 3;    // ~4.5-cell Gaussian: corners become arcs of 4-8+ cells
        const float SquareMargin = 2f;          // the field stays land up to 2 cells around the play square (rounded)
        const int SideWidth = 18;               // delivered W / E face width (px; Islands-038 widened 11 -> 18)
        const int SideGrassCol = 17;            // grass column of the W face (E: 31 - this)
        const int NarrowWidth = 11;             // Islands-052 sidewall_round face width (px)
        const int ProfileOverhang = 4;          // Islands-052 coast_edge_profile rows that lie outside the coast line
        const string Inc = "Assets/_Project/Art/Incoming/islands/";
        const string DressDir = IslandArtBuilder.IslandDir + "/Dressing";
        public const string ArtDir = IslandArtBuilder.IslandDir + "/Coast";
        public const string TileDir = IslandArtBuilder.TileDir + "/Coast";
        static readonly Color SideEastShade = new Color(0.82f, 0.82f, 0.88f, 1f);

        // ------------------------------------------------------------------ geometry

        public static int Pack(int mask, int s, int e, int n, int w) => mask | (s << 4) | (e << 10) | (n << 16) | (w << 22);

        /// <summary>A cell's coast segment in cell-local px (y up): from <see cref="p"/> to <see cref="q"/>, <see cref="n"/> the
        /// outward (sky-side) unit normal.</summary>
        public struct Seg { public Vector2 p, q, n; }

        static readonly Dictionary<int, Seg> _seg = new Dictionary<int, Seg>();

        public static bool Segment(int g, out Seg s)
        {
            if (_seg.TryGetValue(g, out s)) return s.n != Vector2.zero;
            int m = g & 15;
            bool c0 = (m & 1) != 0, c1 = (m & 2) != 0, c2 = (m & 4) != 0, c3 = (m & 8) != 0;
            Vector2 a = default, b = default;
            int k = 0;
            void Add(Vector2 v) { if (k == 0) a = v; else b = v; k++; }
            if (c0 != c1) Add(new Vector2((g >> 4) & 63, 0));
            if (c1 != c3) Add(new Vector2(P, (g >> 10) & 63));
            if (c2 != c3) Add(new Vector2((g >> 16) & 63, P));
            if (c0 != c2) Add(new Vector2(0, (g >> 22) & 63));
            s = default;
            if (k == 2 && (b - a).sqrMagnitude > 1e-6f)
            {
                var d = b - a;
                var n = new Vector2(d.y, -d.x).normalized;
                // the land side holds the land corners: pick the one farthest from the line
                float best = 0f;
                var corners = new[] { (c0, new Vector2(0, 0)), (c1, new Vector2(P, 0)), (c2, new Vector2(0, P)), (c3, new Vector2(P, P)) };
                foreach (var (on, cp) in corners)
                {
                    if (!on) continue;
                    float t = Vector2.Dot(n, cp - a);
                    if (Mathf.Abs(t) > Mathf.Abs(best)) best = t;
                }
                if (best > 0f) n = -n;
                s = new Seg { p = a, q = b, n = n };
            }
            _seg[g] = s;
            return s.n != Vector2.zero;
        }

        public static int FloorDiv(int v) => v >= 0 ? v / P : -((-v + P - 1) / P);
        static int Mod(int v) => ((v % P) + P) % P;

        static float PointSeg(Vector2 pt, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(pt - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(pt, a + ab * t);
        }

        /// <summary>The painted landmass at pixel resolution: full coast cells, curved cells (<see cref="CoastShapeTile"/>) and
        /// the play square. Global px = cell * 32 + local px (y up).</summary>
        public sealed class Map
        {
            public readonly Dictionary<Vector2Int, int> geo = new Dictionary<Vector2Int, int>();
            public readonly HashSet<Vector2Int> solid = new HashSet<Vector2Int>();
            public int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            public bool Curved => geo.Count > 0;

            public static Map Read(Tilemap tm, int cells)
            {
                var m = new Map();
                if (tm != null)
                {
                    tm.CompressBounds();
                    foreach (var p in tm.cellBounds.allPositionsWithin)
                    {
                        var t = tm.GetTile(p);
                        if (t == null) continue;
                        var v = new Vector2Int(p.x, p.y);
                        if (t is CoastShapeTile st && st.geo != 0) m.geo[v] = st.geo;
                        m.Add(v);
                    }
                }
                for (int x = 0; x < cells; x++) for (int y = -cells; y <= -1; y++) { var v = new Vector2Int(x, y); m.geo.Remove(v); m.Add(v); }
                return m;
            }

            void Add(Vector2Int v)
            {
                solid.Add(v);
                x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y);
            }

            public bool PixelLand(int gx, int gy)
            {
                int cx = FloorDiv(gx), cy = FloorDiv(gy);
                var c = new Vector2Int(cx, cy);
                if (!solid.Contains(c)) return false;
                if (!geo.TryGetValue(c, out var g) || !Segment(g, out var s)) return true;
                var lp = new Vector2(gx - cx * P + 0.5f, gy - cy * P + 0.5f);
                return Vector2.Dot(lp - s.p, s.n) <= 0f;
            }

            /// <summary>The coast segment nearest to global point <paramref name="gp"/> (searching <paramref name="r"/> cells around).</summary>
            public bool Nearest(Vector2 gp, int r, out Vector2 a, out Vector2 n, out float dist)
            {
                int cx = Mathf.FloorToInt(gp.x / P), cy = Mathf.FloorToInt(gp.y / P);
                a = n = Vector2.zero; dist = float.MaxValue;
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        var c = new Vector2Int(cx + dx, cy + dy);
                        if (!geo.TryGetValue(c, out var g) || !Segment(g, out var s)) continue;
                        var o = new Vector2(c.x * P, c.y * P);
                        float d = PointSeg(gp, o + s.p, o + s.q);
                        if (d < dist) { dist = d; a = o + s.p; n = s.n; }
                    }
                return dist < float.MaxValue;
            }

            /// <summary>Outward coast normal smoothed over ~1.5 cells (continuous along the curve, for facing decisions).</summary>
            public Vector2 SmoothNormal(Vector2 gp, float radius = 48f)
            {
                int cx = Mathf.FloorToInt(gp.x / P), cy = Mathf.FloorToInt(gp.y / P);
                var sum = Vector2.zero;
                int rc = Mathf.Max(2, Mathf.CeilToInt(radius / P) + 1);
                for (int dx = -rc; dx <= rc; dx++)
                    for (int dy = -rc; dy <= rc; dy++)
                    {
                        var c = new Vector2Int(cx + dx, cy + dy);
                        if (!geo.TryGetValue(c, out var g) || !Segment(g, out var s)) continue;
                        var o = new Vector2(c.x * P, c.y * P);
                        float d = PointSeg(gp, o + s.p, o + s.q);
                        float w = 1f - d / radius;
                        if (w <= 0f) continue;
                        sum += s.n * (w * w * Vector2.Distance(s.p, s.q));
                    }
                return sum.sqrMagnitude > 1e-8f ? sum.normalized : Vector2.zero;
            }
        }

        // ------------------------------------------------------------------ cutting the outline

        static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)x * 0x85EBCA77u ^ (uint)y * 0xC2B2AE3Du;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return h;
            }
        }

        const float Swell = 0.18f;
        static readonly int[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        /// <summary>Smooth value noise in [0, 1] (smoothstep-interpolated hashed lattice).</summary>
        static float VNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float H(int a, int b) => (Hash(a, b, seed) & 0xFFFF) / 65535f;
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), fx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx), fy);
        }

        static float[,] BoxBlur(bool[,] a)
        {
            int w = a.GetLength(0), h = a.GetLength(1);
            var f = new float[w, h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) f[x, y] = a[x, y] ? 1f : 0f;
            for (int pass = 0; pass < BlurPasses; pass++)
            {
                var t = new float[w, h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float s = 0f;
                        for (int k = -BlurR; k <= BlurR; k++) { int xx = x + k; if (xx >= 0 && xx < w) s += f[xx, y]; }
                        t[x, y] = s / (2 * BlurR + 1);
                    }
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        float s = 0f;
                        for (int k = -BlurR; k <= BlurR; k++) { int yy = y + k; if (yy >= 0 && yy < h) s += t[x, yy]; }
                        f[x, y] = s / (2 * BlurR + 1);
                    }
            }
            return f;
        }

        /// <summary>
        /// Raw cell mask <paramref name="a"/> over [ox, ox + w) x [oy, oy + h) -> curved coast: <paramref name="full"/> cells and
        /// <paramref name="geo"/> (0 = not a curved cell). Deterministic. Detached clumps of the raw outline become rounded
        /// superellipse islets (>= 7 x 7 cells, 3+ cells clear of the main land).
        /// </summary>
        public static void Cut(bool[,] a, int ox, int oy, int seed, int cells, out bool[,] full, out int[,] geo)
        {
            int w = a.GetLength(0), h = a.GetLength(1);
            var f = BoxBlur(a);
            int vw = w + 1, vh = h + 1;
            var val = new float[vw, vh];
            var locked = new bool[vw, vh];
            float F(int x, int y) => x >= 0 && y >= 0 && x < w && y < h ? f[x, y] : 0f;
            for (int i = 0; i < vw; i++)
                for (int j = 0; j < vh; j++)
                {
                    float v = (F(i - 1, j - 1) + F(i, j - 1) + F(i - 1, j) + F(i, j)) * 0.25f;
                    int wx = ox + i, wy = oy + j;
                    // gentle seeded swell (wavelength ~16 cells, <= ~1 cell of shift): long straight runs of the seed outline
                    // (its minimum margin along the play square) become slow curves instead of ruler lines
                    if (v > 0.05f && v < 0.95f) v += Swell * (VNoise(wx / 16f, wy / 16f, seed) * 0.7f + VNoise(wx / 7f, wy / 7f, seed + 7) * 0.3f - 0.5f);
                    float dx = Mathf.Max(0, Mathf.Max(-wx, wx - cells)), dy = Mathf.Max(0, Mathf.Max(-cells - wy, wy));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= 0f) { v = 1f; locked[i, j] = true; }
                    else v = Mathf.Max(v, 1f - d / (2f * SquareMargin));
                    val[i, j] = v;
                }
            var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            bool[,] MainOf()
            {
                var main = new bool[vw, vh];
                var q = new Queue<Vector2Int>();
                for (int i = 0; i < vw; i++) for (int j = 0; j < vh; j++) if (locked[i, j]) { main[i, j] = true; q.Enqueue(new Vector2Int(i, j)); }
                while (q.Count > 0)
                {
                    var p = q.Dequeue();
                    foreach (var dd in dirs)
                    {
                        var n = p + dd;
                        if (n.x < 0 || n.y < 0 || n.x >= vw || n.y >= vh || main[n.x, n.y] || val[n.x, n.y] < 0.5f) continue;
                        main[n.x, n.y] = true; q.Enqueue(n);
                    }
                }
                return main;
            }
            var mainV = MainOf();
            for (int i = 0; i < vw; i++) for (int j = 0; j < vh; j++) if (!mainV[i, j] && val[i, j] >= 0.5f) val[i, j] = 0.3f;     // detached blobs go (islets are redrawn below)

            // islets: rounded superellipses over each detached raw clump, added to the field (smooth outline)
            {
                var seen = new bool[w, h];
                var q = new Queue<Vector2Int>();
                int isletN = 0;
                for (int sx = 0; sx < w; sx++)
                    for (int sy = 0; sy < h; sy++)
                    {
                        if (!a[sx, sy] || seen[sx, sy]) continue;
                        var list = new List<Vector2Int>();
                        bool sq = false;
                        seen[sx, sy] = true; q.Enqueue(new Vector2Int(sx, sy));
                        while (q.Count > 0)
                        {
                            var p = q.Dequeue(); list.Add(p);
                            int wx = ox + p.x, wy = oy + p.y;
                            sq |= wx >= 0 && wx < cells && wy >= -cells && wy <= -1;
                            foreach (var dd in dirs)
                            {
                                var n = p + dd;
                                if (n.x >= 0 && n.y >= 0 && n.x < w && n.y < h && a[n.x, n.y] && !seen[n.x, n.y]) { seen[n.x, n.y] = true; q.Enqueue(n); }
                            }
                        }
                        if (sq || list.Count < 4) continue;
                        bool merged = false;
                        foreach (var c in list) if (mainV[c.x, c.y] && mainV[c.x + 1, c.y] && mainV[c.x, c.y + 1] && mainV[c.x + 1, c.y + 1]) { merged = true; break; }
                        if (merged) continue;
                        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
                        foreach (var c in list) { x0 = Mathf.Min(x0, c.x); x1 = Mathf.Max(x1, c.x); y0 = Mathf.Min(y0, c.y); y1 = Mathf.Max(y1, c.y); }
                        while (x1 - x0 + 1 < 8) { if (((x1 - x0) & 1) == 0) x0--; else x1++; }
                        while (y1 - y0 + 1 < 7) { if (((y1 - y0) & 1) == 0) y0--; else y1++; }
                        float cx = (x0 + x1 + 1) * 0.5f, cy = (y0 + y1 + 1) * 0.5f, rx = (x1 - x0 + 1) * 0.5f, ry = (y1 - y0 + 1) * 0.5f;
                        float pw = 2.0f + (Hash(isletN++, x0, seed + 31) % 100) / 100f * 0.5f;     // 2.0 (ellipse) .. 2.5
                        // keep 3 cells of sky to the main land: shrink, else drop
                        bool fits = false;
                        for (int tries = 0; tries < 3 && !fits; tries++)
                        {
                            fits = true;
                            for (int i = Mathf.Max(0, Mathf.FloorToInt(cx - rx) - 5); i <= Mathf.Min(vw - 1, Mathf.CeilToInt(cx + rx) + 5) && fits; i++)
                                for (int j = Mathf.Max(0, Mathf.FloorToInt(cy - ry) - 5); j <= Mathf.Min(vh - 1, Mathf.CeilToInt(cy + ry) + 5) && fits; j++)
                                {
                                    if (!mainV[i, j]) continue;
                                    float ddx = Mathf.Max(0f, Mathf.Abs(i - cx) - rx), ddy = Mathf.Max(0f, Mathf.Abs(j - cy) - ry);
                                    if (ddx * ddx + ddy * ddy < 20f) fits = false;
                                }
                            if (!fits) { rx -= 1f; ry -= 1f; if (rx < 3.5f || ry < 3f) break; }
                        }
                        if (!fits) continue;
                        // VP-17: not a perfect disc - the radius swells and dips in seeded lobes (3rd-5th harmonics, ~3-6
                        // cells of outline each, up to ~+-18%), so the islet reads as a broken-off rock, not a coin
                        int ls = seed + 101 + isletN * 7;
                        float ph3 = (Hash(3, ls, 41) % 1000) / 1000f * 6.283f, ph4 = (Hash(4, ls, 41) % 1000) / 1000f * 6.283f, ph5 = (Hash(5, ls, 41) % 1000) / 1000f * 6.283f;
                        float Lobe(float ang) => 1f + 0.09f * Mathf.Sin(3f * ang + ph3) + 0.06f * Mathf.Sin(4f * ang + ph4) + 0.04f * Mathf.Sin(5f * ang + ph5);
                        for (int i = Mathf.Max(0, Mathf.FloorToInt(cx - rx) - 2); i <= Mathf.Min(vw - 1, Mathf.CeilToInt(cx + rx) + 2); i++)
                            for (int j = Mathf.Max(0, Mathf.FloorToInt(cy - ry) - 2); j <= Mathf.Min(vh - 1, Mathf.CeilToInt(cy + ry) + 2); j++)
                            {
                                float lk = Lobe(Mathf.Atan2((j - cy) / ry, (i - cx) / rx));
                                float s = (Mathf.Pow(Mathf.Abs((i - cx) / rx), pw) + Mathf.Pow(Mathf.Abs((j - cy) / ry), pw)) / Mathf.Pow(lk, pw);
                                val[i, j] = Mathf.Max(val[i, j], 0.5f + 0.5f * (1f - s));
                            }
                    }
            }

            // clean-up on the field: specks go, 1-vertex spikes / notches are smoothed, saddles bridged
            for (int iter = 0; iter < 60; iter++)
            {
                bool changed = false;
                bool L(int i, int j) => i >= 0 && j >= 0 && i < vw && j < vh && val[i, j] >= 0.5f;
                for (int i = 0; i < vw; i++)
                    for (int j = 0; j < vh; j++)
                    {
                        if (locked[i, j]) continue;
                        int cnt = (L(i, j + 1) ? 1 : 0) + (L(i + 1, j) ? 1 : 0) + (L(i, j - 1) ? 1 : 0) + (L(i - 1, j) ? 1 : 0);
                        bool on = L(i, j);
                        bool thin = on && ((!L(i, j + 1) && !L(i, j - 1)) || (!L(i - 1, j) && !L(i + 1, j)));
                        if (on && (cnt <= 1 || thin)) { val[i, j] = 0.45f; changed = true; }
                        else if (!on && cnt >= 3) { val[i, j] = 0.55f; changed = true; }
                    }
                for (int i = 0; i < vw - 1; i++)
                    for (int j = 0; j < vh - 1; j++)
                    {
                        bool p00 = L(i, j), p10 = L(i + 1, j), p01 = L(i, j + 1), p11 = L(i + 1, j + 1);
                        if (p00 && p11 && !p10 && !p01) { if (val[i + 1, j] >= val[i, j + 1]) val[i + 1, j] = 0.55f; else val[i, j + 1] = 0.55f; changed = true; }
                        else if (p10 && p01 && !p00 && !p11) { if (val[i, j] >= val[i + 1, j + 1]) val[i, j] = 0.55f; else val[i + 1, j + 1] = 0.55f; changed = true; }
                    }
                // specks: land components (4-connected vertices) under 20 vertices that are not the main land
                {
                    var comp = new bool[vw, vh];
                    var q = new Queue<Vector2Int>();
                    for (int i = 0; i < vw; i++)
                        for (int j = 0; j < vh; j++)
                        {
                            if (comp[i, j] || !L(i, j)) continue;
                            var list = new List<Vector2Int>();
                            bool keep = false;
                            comp[i, j] = true; q.Enqueue(new Vector2Int(i, j));
                            while (q.Count > 0)
                            {
                                var p = q.Dequeue(); list.Add(p); keep |= locked[p.x, p.y];
                                foreach (var dd in dirs)
                                {
                                    var n = p + dd;
                                    if (!L(n.x, n.y) || comp[n.x, n.y]) continue;
                                    comp[n.x, n.y] = true; q.Enqueue(n);
                                }
                            }
                            if (!keep && list.Count < 20) { foreach (var p in list) val[p.x, p.y] = 0f; changed = true; }
                        }
                }
                if (!changed) break;
            }

            int Lv(float va, float vb)
            {
                float t = Mathf.Abs(vb - va) < 1e-6f ? 0.5f : (0.5f - va) / (vb - va);
                return Mathf.Clamp(Mathf.RoundToInt(t * P / Quantum) * Quantum, Quantum, P - Quantum);
            }
            full = new bool[w, h];
            geo = new int[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    float v00 = val[x, y], v10 = val[x + 1, y], v01 = val[x, y + 1], v11 = val[x + 1, y + 1];
                    bool c0 = v00 >= 0.5f, c1 = v10 >= 0.5f, c2 = v01 >= 0.5f, c3 = v11 >= 0.5f;
                    int m = (c0 ? 1 : 0) | (c1 ? 2 : 0) | (c2 ? 4 : 0) | (c3 ? 8 : 0);
                    if (m == 15) { full[x, y] = true; continue; }
                    if (m == 0 || m == 6 || m == 9) continue;       // saddles (SE+NW, SW+NE) were bridged above
                    int s = c0 != c1 ? Lv(v00, v10) : 0, e = c1 != c3 ? Lv(v10, v11) : 0;
                    int n = c2 != c3 ? Lv(v01, v11) : 0, wv = c0 != c2 ? Lv(v00, v01) : 0;
                    geo[x, y] = Pack(m, s, e, n, wv);
                }
        }

        // ------------------------------------------------------------------ pixel IO

        internal static Color32[] ReadPng(string path, out int w, out int h)
        {
            w = h = 0;
            if (!File.Exists(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            w = t.width; h = t.height;
            var px = t.GetPixels32();
            Object.DestroyImmediate(t);
            return px;
        }

        static void WriteIfChanged(string path, int w, int h, Color32[] px)
        {
            if (File.Exists(path))
            {
                var old = ReadPng(path, out int ow, out int oh);
                bool same = old != null && ow == w && oh == h;
                if (same) for (int i = 0; i < px.Length; i++) if (!old[i].Equals(px[i]) && (old[i].a != 0 || px[i].a != 0)) { same = false; break; }
                if (same) return;
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        internal sealed class Piece { public string key; public int geo; public Color32[] px; }

        internal static ulong ContentHash(Color32[] px)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                foreach (var c in px)
                {
                    uint v = c.a == 0 ? 0u : (uint)(c.r | (c.g << 8) | (c.b << 16) | (c.a << 24));
                    h = (h ^ v) * 1099511628211UL;
                }
                return h;
            }
        }

        /// <summary>
        /// Writes <paramref name="pieces"/> (all <paramref name="pw"/> x <paramref name="ph"/>) into one atlas PNG
        /// (<see cref="ArtDir"/>/<paramref name="name"/>.png, sprites named by key, pivot (0.5, <paramref name="pivotY"/>)) and
        /// keeps one tile per piece as a sub-asset of <see cref="TileDir"/>/<paramref name="name"/>.asset (existing tiles are
        /// reused by key, unused ones removed). Pieces with a geo become <see cref="CoastShapeTile"/>s.
        /// </summary>
        internal static Dictionary<string, Tile> Atlas(string name, int pw, int ph, float pivotY, List<Piece> pieces)
        {
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(TileDir);
            pieces.Sort((x, y) => string.CompareOrdinal(x.key, y.key));
            // every piece sits in a slot with a 2 px gutter of its own edge pixels (extruded), so the GPU never samples a
            // neighbouring piece at non-integer zoom (seams / 1-px lines along the tile edges)
            const int G = 2;
            int sw = pw + 2 * G, sh = ph + 2 * G;
            int n = Mathf.Max(1, pieces.Count);
            int cols = Mathf.Min(n, Mathf.Max(1, 4096 / sw)), rows = (n + cols - 1) / cols;
            int W = cols * sw, H = rows * sh;
            var o = new Color32[W * H];
            var rects = new List<(string name, Rect r)>();
            for (int i = 0; i < pieces.Count; i++)
            {
                int ox = (i % cols) * sw, oy = (i / cols) * sh;
                var px = pieces[i].px;
                for (int y = -G; y < ph + G; y++)
                    for (int x = -G; x < pw + G; x++)
                        o[(oy + G + y) * W + ox + G + x] = px[Mathf.Clamp(y, 0, ph - 1) * pw + Mathf.Clamp(x, 0, pw - 1)];
                rects.Add((pieces[i].key, new Rect(ox + G, oy + G, pw, ph)));
            }
            string path = $"{ArtDir}/{name}.png";
            WriteIfChanged(path, W, H, o);
            var sprites = ImportGrid(path, rects, pivotY);

            string tp = $"{TileDir}/{name}.asset";
            var main = AssetDatabase.LoadMainAssetAtPath(tp) as Tile;
            if (main == null)
            {
                if (File.Exists(tp)) AssetDatabase.DeleteAsset(tp);
                main = ScriptableObject.CreateInstance<Tile>();
                main.name = name;
                main.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(main, tp);
            }
            var existing = new Dictionary<string, Tile>();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(tp))
                if (obj is Tile t && obj != main) { if (existing.ContainsKey(t.name)) Object.DestroyImmediate(t, true); else existing[t.name] = t; }
            var result = new Dictionary<string, Tile>();
            foreach (var p in pieces)
            {
                bool shape = p.geo != 0;
                if (existing.TryGetValue(p.key, out var t) && (t is CoastShapeTile) != shape) { Object.DestroyImmediate(t, true); t = null; }
                if (t == null)
                {
                    t = shape ? ScriptableObject.CreateInstance<CoastShapeTile>() : ScriptableObject.CreateInstance<Tile>();
                    t.name = p.key;
                    AssetDatabase.AddObjectToAsset(t, main);
                }
                existing.Remove(p.key);
                sprites.TryGetValue(p.key, out var sp);
                bool dirty = t.sprite != sp || t.colliderType != Tile.ColliderType.None || t.flags != TileFlags.None;
                if (shape && ((CoastShapeTile)t).geo != p.geo) { ((CoastShapeTile)t).geo = p.geo; dirty = true; }
                if (dirty)
                {
                    t.sprite = sp;
                    t.colliderType = Tile.ColliderType.None;
                    t.flags = TileFlags.None;
                    EditorUtility.SetDirty(t);
                }
                result[p.key] = t;
            }
            foreach (var t in existing.Values) Object.DestroyImmediate(t, true);
            EditorUtility.SetDirty(main);
            AssetDatabase.SaveAssets();
            return result;
        }

        static Dictionary<string, Sprite> ImportGrid(string path, List<(string name, Rect r)> rects, float pivotY)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            var map = new Dictionary<string, Sprite>();
            if (imp == null) return map;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Multiple;
            imp.spritePixelsPerUnit = 32;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 8192;
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            st.spriteGenerateFallbackPhysicsShape = false;
            imp.SetTextureSettings(st);
            if (EditorUtility.IsDirty(imp)) imp.SaveAndReimport();
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(imp);
            provider.InitSpriteEditorDataProvider();
            var old = provider.GetSpriteRects() ?? new SpriteRect[0];
            bool same = old.Length == rects.Count;
            for (int i = 0; same && i < rects.Count; i++)
                same = old[i].name == rects[i].name && old[i].rect == rects[i].r && Mathf.Abs(old[i].pivot.y - pivotY) < 1e-4f && old[i].alignment == SpriteAlignment.Custom;
            if (!same)
            {
                var ids = new Dictionary<string, GUID>();
                foreach (var r in old) ids[r.name] = r.spriteID;
                var list = new SpriteRect[rects.Count];
                for (int i = 0; i < rects.Count; i++)
                    list[i] = new SpriteRect
                    {
                        name = rects[i].name,
                        rect = rects[i].r,
                        alignment = SpriteAlignment.Custom,
                        pivot = new Vector2(0.5f, pivotY),
                        spriteID = ids.TryGetValue(rects[i].name, out var id) ? id : GUID.Generate(),
                    };
                provider.SetSpriteRects(list);
                provider.Apply();
                imp.SaveAndReimport();
            }
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path)) if (obj is Sprite s) map[s.name] = s;
            return map;
        }

        // ------------------------------------------------------------------ ground pieces

        public static int GroundVariant(int x, int y) => (int)(Hash(x, y, 7177) % 3u);

        /// <summary>Ground tiles for the given (geo, variant) keys of a biome: <c>coast_ground_&lt;biome&gt;</c>.</summary>
        public static Dictionary<long, Tile> EnsureGround(string biome, HashSet<long> keys)
        {
            var blob = ReadPng(IslandArtBuilder.BlobPath(biome), out int bw, out int bh);
            if (blob == null || bh != 32) blob = ReadPng($"{Inc}island_{biome}_ground_blob_32x32_47f.png", out bw, out bh);
            var fill = ReadPng($"{Inc}island_{biome}_ground_fill_32x32_3f.png", out int fw, out int fh);
            if (blob == null) return null;
            int interior = System.Array.IndexOf(IslandArtBuilder.BlobMasks(), 255);
            int nf = fill != null && fh == 32 ? Mathf.Max(1, fw / 32) : 0;
            Color32 Fill(int v, int x, int y) => nf > 0 ? fill[y * fw + (v % nf) * 32 + x] : blob[y * bw + interior * 32 + x];
            Color32 r1 = blob[(31 - 3) * bw + 16], r2 = blob[(31 - 4) * bw + 16];
            var pieces = new List<Piece>();
            foreach (long key in keys)
            {
                int g = (int)(key >> 2), v = (int)(key & 3);
                if (!Segment(g, out var s)) continue;
                var px = new Color32[P * P];
                float m = Mathf.Max(Mathf.Abs(s.n.x), Mathf.Abs(s.n.y));
                for (int y = 0; y < P; y++)
                    for (int x = 0; x < P; x++)
                    {
                        var c = new Vector2(x + 0.5f, y + 0.5f);
                        float dt = Vector2.Dot(c - s.p, s.n);
                        if (dt > 0f) continue;
                        int k = Mathf.FloorToInt(-dt / m);
                        px[y * P + x] = k == 0 ? r1 : k == 1 ? r2 : Fill(v, x, y);
                    }
                pieces.Add(new Piece { key = $"g{g:X7}_{v}", geo = g, px = px });
            }
            var tiles = Atlas("coast_ground_" + biome, P, P, 0.5f, pieces);
            var res = new Dictionary<long, Tile>();
            foreach (long key in keys)
            {
                int g = (int)(key >> 2), v = (int)(key & 3);
                if (tiles.TryGetValue($"g{g:X7}_{v}", out var t)) res[key] = t;
            }
            return res;
        }

        // ------------------------------------------------------------------ walls

        const int WallH = 136, WallBase = 96, WallTuck = 6;

        /// <summary>Wall frames (bottom-up rows, 32 x 96 each) used for curved walls: the delivered round set when present,
        /// else the repaired straight mids (IslandDressingBuilder.RepairedCliffWall).</summary>
        static List<Color32[]> WallFrames(string biome)
        {
            var list = new List<Color32[]>();
            var round = ReadPng($"{Inc}island_{biome}_cliffwall_round_32x96_4f.png", out int rw, out int rh);
            int[] frames;
            Color32[] src; int sw;
            if (round != null && rh == 96 && rw >= 128) { src = round; sw = rw; frames = new[] { 0, 1, 2, 3 }; }
            else
            {
                if (!File.Exists(IslandArtBuilder.CliffWallPath(biome))) return list;
                IslandDressingBuilder.RepairedCliffWall(biome);
                src = ReadPng($"{DressDir}/dress_cliffwall_{biome}_32x96_8f.png", out sw, out int sh);
                if (src == null || sh != 96) return list;
                frames = new[] { 0, 1, 2 };
            }
            foreach (int f in frames)
            {
                var fr = new Color32[32 * 96];
                for (int y = 0; y < 96; y++) for (int x = 0; x < 32; x++) fr[y * 32 + x] = src[y * sw + f * 32 + x];
                list.Add(fr);
            }
            return list;
        }

        /// <summary>Share of the wall's brightness shifted by how far a steep stretch turns east (shade) or west (lit).</summary>
        const float WallTurnShade = 0.16f;

        /// <summary>
        /// The 3/4 south walls of a curved coast into <paramref name="cliff"/> (a tilemap at the Island origin): one piece per
        /// curved cell whose segment faces south. The south-facing segments are chained along the coast and the wall texture
        /// is laid by ARC LENGTH (u = px along the coast line), so a wall turning a corner keeps its strata and rock columns
        /// continuous and foreshortens where the coast steepens (no sheared streaks); the frame variant changes every 32 px
        /// of u (never the same twice in a row). Steep stretches are shaded by facing (light from the top-left). Pieces are
        /// de-duplicated by content. Returns the cells that carry a piece (the wall hangs up to 3 cells below).
        /// </summary>
        public static HashSet<Vector2Int> BuildWalls(string biome, Map map, Tilemap cliff)
        {
            var cells = new HashSet<Vector2Int>();
            var frames = WallFrames(biome);
            if (frames.Count == 0) return cells;
            int nv = frames.Count;
            int seed = (int)(Hash(biome.Length, biome[0], 517) & 0xFFFF);

            // south-facing segments, left -> right, keyed by their global left end point
            var segs = new List<(Vector2Int c, Vector2 a, Vector2 b, Vector2 n)>();
            var byLeft = new Dictionary<long, int>();
            var hasPrev = new HashSet<int>();
            long K(Vector2 v) => ((long)Mathf.RoundToInt(v.x) << 32) ^ (uint)Mathf.RoundToInt(v.y);
            foreach (var kv in map.geo)
            {
                if (!Segment(kv.Value, out var s) || s.n.y > -1e-3f) continue;
                var o = new Vector2(kv.Key.x * P, kv.Key.y * P);
                Vector2 a = o + s.p, b = o + s.q;
                if (a.x > b.x) { var t = a; a = b; b = t; }
                if (b.x - a.x < 1e-3f) continue;
                byLeft[K(a)] = segs.Count;
                segs.Add((kv.Key, a, b, s.n));
            }
            for (int i = 0; i < segs.Count; i++) if (byLeft.TryGetValue(K(segs[i].b), out var nx)) hasPrev.Add(nx);
            // arc length at each segment's left end: walk every chain from its start
            var u0 = new float[segs.Count];
            var chain = new int[segs.Count];
            var done = new bool[segs.Count];
            int chains = 0;
            void Walk(int start)
            {
                float u = (Hash(chains, start, seed) % 32u);
                for (int i = start; i >= 0 && !done[i];)
                {
                    done[i] = true; u0[i] = u; chain[i] = chains;
                    u += Vector2.Distance(segs[i].a, segs[i].b);
                    i = byLeft.TryGetValue(K(segs[i].b), out var nx) ? nx : -1;
                }
                chains++;
            }
            for (int i = 0; i < segs.Count; i++) if (!hasPrev.Contains(i)) Walk(i);
            for (int i = 0; i < segs.Count; i++) if (!done[i]) Walk(i);          // closed loops (islets)

            // VP-16: the wall texture is laid in RUNS of irregular length (18-41 px of u), each taking its own frame (never
            // the previous one) from its own column offset, so the frames' vertical accents (lava cracks, crystals, vines,
            // frame-edge joints) no longer repeat every 32 px like a barcode. Three of four runs pick the calmest window
            // (least column-to-column contrast) of a few seeded candidates. A new run dithers in over its first 5 px and
            // the top cap rows (grass / crust) always come from frame 0 (at the run offset), so the lip reads as one
            // strip instead of per-run slabs.
            float[][] energy = new float[nv][];
            for (int f = 0; f < nv; f++)
            {
                energy[f] = new float[32];
                var fr = frames[f];
                for (int x = 0; x < 32; x++)
                {
                    float e = 0f;
                    for (int y = 0; y < 96; y++)
                    {
                        var a = fr[y * 32 + x]; var b = fr[y * 32 + ((x + 1) & 31)];
                        if (a.a == 0 && b.a == 0) continue;
                        e += Mathf.Abs((a.r * 0.3f + a.g * 0.59f + a.b * 0.11f) - (b.r * 0.3f + b.g * 0.59f + b.b * 0.11f)) / 255f;
                    }
                    energy[f][x] = e;
                }
            }
            var runs = new Dictionary<int, List<(float start, int frame, int off)>>();
            List<(float start, int frame, int off)> Runs(int ch)
            {
                if (runs.TryGetValue(ch, out var list)) return list;
                list = new List<(float, int, int)>();
                float u = 0f;
                int prev = -1;
                for (int k = 0; k < 4096 && u < 1e5f; k++)
                {
                    int len = 18 + (int)(Hash(ch, k, seed + 3) % 24u);
                    int bestF = 0, bestO = 0;
                    float bestE = float.MaxValue;
                    bool calm = Hash(ch, k, seed + 5) % 4u != 0u;
                    for (int c = 0; c < (calm ? 8 : 1); c++)
                    {
                        int f = (int)(Hash(ch * 31 + c, k, seed + 7) % (uint)nv);
                        if (nv > 1 && f == prev) f = (f + 1) % nv;
                        int o = (int)(Hash(ch * 17 + c, k, seed + 11) & 31u);
                        float e = 0f;
                        for (int i = 0; i < len; i++) e += energy[f][(o + i) & 31];
                        if (e < bestE) { bestE = e; bestF = f; bestO = o; }
                    }
                    list.Add((u, bestF, bestO));
                    prev = bestF;
                    u += len;
                    if (list.Count > 1 && u > 1e5f) break;
                    if (u > 20000f) break;
                }
                runs[ch] = list;
                return list;
            }
            const int Blend = 5, CapRows = WallTuck + 6, CapFade = 3;
            Color32 Sample(int ch, float u, int tf, int dither)
            {
                // the cap rows always come from frame 0 (one continuous lip, no per-run slabs), at the run's own offset
                bool cap = tf < CapRows + CapFade && (tf < CapRows || ((tf - CapRows) * 16 + 8) / CapFade <= dither);
                var list = Runs(ch);
                int lo = 0, hi = list.Count - 1;
                while (lo < hi) { int mid = (lo + hi + 1) >> 1; if (list[mid].start <= u) lo = mid; else hi = mid - 1; }
                var r = list[lo];
                float local = u - r.start;
                if (lo > 0 && local < Blend && (int)(local * 16f / Blend) <= dither)
                {
                    var p = list[lo - 1];
                    return frames[cap ? 0 : p.frame][(95 - tf) * 32 + ((p.off + Mathf.FloorToInt(u - p.start)) & 31)];
                }
                return frames[cap ? 0 : r.frame][(95 - tf) * 32 + ((r.off + Mathf.FloorToInt(local)) & 31)];
            }
            var pieces = new Dictionary<ulong, Piece>();
            var at = new List<(Vector2Int c, ulong h)>();
            for (int i = 0; i < segs.Count; i++)
            {
                var (c, a, b, n) = segs[i];
                float len = Vector2.Distance(a, b), dx = b.x - a.x;
                // facing shade: only where the coast turns past ~45° toward east (darker) / west (lighter)
                float turn = Mathf.Clamp01((Mathf.Abs(n.x) - 0.6f) / 0.35f);
                float k = 1f + (n.x > 0f ? -WallTurnShade : WallTurnShade * 0.5f) * turn;
                var px = new Color32[P * WallH];
                int cx0 = c.x * P, cy0 = c.y * P;
                for (int x = 0; x < P; x++)
                {
                    float xc = cx0 + x + 0.5f;
                    if (xc < a.x || xc > b.x) continue;
                    float f = (xc - a.x) / dx;
                    float yl = a.y + (b.y - a.y) * f - cy0;              // coast line in cell-local px
                    int r0 = Mathf.CeilToInt(yl - 0.5f);                 // lowest land pixel row of this column
                    float u = u0[i] + f * len;
                    // where the coast turns to face west / east the visible face narrows AND shortens: the wall height
                    // follows the smoothed coast normal (full from ~25° off south-facing, nothing at the vertical tangent),
                    // compressing the frame below its tuck rows so the ragged bottom stays (no 1-px "posts"). The normal is
                    // smoothed over ~3.5 cells (VP-16), so a wall turning vertical tapers over 3-4 segments, not one.
                    var sn = map.SmoothNormal(new Vector2(xc, a.y + (b.y - a.y) * f), 112f);
                    int H = Mathf.RoundToInt(96f * Mathf.Clamp01((-Mathf.Min(sn.y, n.y * 0.5f + sn.y * 0.5f) - 0.1f) / 0.4f));
                    if (H < WallTuck + 6) continue;
                    for (int t = 0; t < H; t++)
                    {
                        int y = r0 + WallTuck - 1 - t;                   // frame row t (from the top): its top 6 rows tuck under the land
                        int tf = t < WallTuck ? t : WallTuck + (t - WallTuck) * (96 - WallTuck) / (H - WallTuck);
                        int gxp = cx0 + x, gyp = cy0 + y;
                        var cc = Sample(chain[i], u, tf, Bayer4[((gyp & 3) << 2) | (gxp & 3)]);
                        if (cc.a == 0) continue;
                        if (turn > 0f) cc = new Color32((byte)Mathf.Clamp(cc.r * k, 0, 255), (byte)Mathf.Clamp(cc.g * k, 0, 255), (byte)Mathf.Clamp(cc.b * k, 0, 255), cc.a);
                        int row = y + WallBase;
                        if (row >= 0 && row < WallH) px[row * P + x] = cc;
                    }
                }
                ulong h = ContentHash(px);
                if (!pieces.ContainsKey(h)) pieces[h] = new Piece { key = "w" + h.ToString("X16"), px = px };
                at.Add((c, h));
            }
            var tiles = Atlas("coast_wall_" + biome, P, WallH, (WallBase + 16f) / WallH, new List<Piece>(pieces.Values));
            foreach (var (c, h) in at)
                if (tiles.TryGetValue(pieces[h].key, out var t)) { cliff.SetTile(new Vector3Int(c.x, c.y, 0), t); cells.Add(c); }
            return cells;
        }

        // ------------------------------------------------------------------ lip + side faces

        /// <summary>
        /// Grass lip (CoastLip, order 9) and W / E side faces (CoastSide, order -4: behind the south walls) for a curved coast,
        /// under <paramref name="root"/>; side-face shadows into the Island's Shadow/Side.
        /// </summary>
        public static void BuildEdges(Island isl, Transform root, Map map)
        {
            string biome = isl.islandKey;
            var lip = ReadPng($"{DressDir}/dress_edge_lip_{biome}_32x32_4f.png", out int lw, out int lh);
            if (lip != null && (lh != 32 || lw < 96)) lip = null;
            // Islands-052: the delivered coast edge profile (8 rows, outward -> inward; frame 0 = N overhang, used on the
            // N / W / E coast, frame 1 = the south rim laid over the wall top) replaces the recoloured lip when present
            var prof = ReadPng($"{Inc}island_{biome}_coast_edge_profile_32x8_2f.png", out int pfw, out int pfh);
            if (prof != null && (pfh != 8 || pfw < 64)) prof = null;
            // Islands-052: narrow W / E faces (11 px, W-A E-A W-B E-B) used where the face is drawn 11 px or narrower
            var sideN = ReadPng($"{Inc}island_{biome}_sidewall_round_32x32_4f.png", out int nw, out int nh);
            if (sideN != null && (nh != 32 || nw < 128)) sideN = null;
            var side = ReadPng($"{Inc}island_{biome}_sidewall_variants_32x32_8f.png", out int sw, out int sh);
            int[] wf = { 0, 1, 2 }, ef = { 3, 4, 5 };
            if (side == null || sh != 32 || sw < 256)
            {
                side = ReadPng($"{Inc}island_{biome}_sidewall_32x32_4f.png", out sw, out sh);
                wf = new[] { 0 }; ef = new[] { 1 };
                if (side != null && (sh != 32 || sw < 64)) side = null;
            }
            var cellsSet = new HashSet<Vector2Int>();
            foreach (var c in map.geo.Keys)
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) cellsSet.Add(new Vector2Int(c.x + dx, c.y + dy));
            var cellList = new List<Vector2Int>(cellsSet);
            cellList.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

            var pieces = new Dictionary<ulong, Piece>();
            var lipAt = new List<(Vector2Int c, ulong h)>();
            var sideAt = new List<(Vector2Int c, ulong h)>();
            string Add(Color32[] px, string prefix, out ulong h)
            {
                h = ContentHash(px) ^ (prefix == "s" ? 0x5bd1e995UL : 0UL);
                if (!pieces.ContainsKey(h)) pieces[h] = new Piece { key = prefix + h.ToString("X16"), px = px };
                return pieces[h].key;
            }
            int vseed = (int)(Hash(biome.Length, biome[biome.Length - 1], 211) & 0xFFFF);
            foreach (var c in cellList)
            {
                Color32[] lp = null, sp = null;
                for (int y = 0; y < P; y++)
                    for (int x = 0; x < P; x++)
                    {
                        int gx = c.x * P + x, gy = c.y * P + y;
                        var gp = new Vector2(gx + 0.5f, gy + 0.5f);
                        bool land = map.PixelLand(gx, gy);
                        // ---- lip
                        if ((lip != null || prof != null) && map.Nearest(gp, 1, out var A, out var n, out var dist) && dist < 9f)
                        {
                            float m = Mathf.Max(Mathf.Abs(n.x), Mathf.Abs(n.y));
                            int k = Mathf.FloorToInt(-Vector2.Dot(gp - A, n) / m);
                            k = land ? Mathf.Max(k, 0) : Mathf.Min(k, -1);
                            int r = 2 + k;                 // lip rows 0-1 (solid) overhang 2 px, its ragged rows 2-3 lie over the rim
                            if (prof != null) r = ProfileOverhang + k;     // profile: ragged rows 0-3 hang over the sky / wall top, solid rows 4-7 cover the ground rim
                            if (r >= 0 && r < 8 && prof != null)
                            {
                                var sn = map.SmoothNormal(gp);
                                if (sn != Vector2.zero)
                                {
                                    // profile row r (top-down = outward -> inward) at the position along the coast
                                    int along = Mathf.Abs(sn.y) >= Mathf.Abs(sn.x) ? Mod(gx) : Mod(gy);
                                    var col = prof[(7 - r) * pfw + (sn.y >= -0.5f ? 0 : 32) + along];
                                    if (col.a > 0) { if (lp == null) lp = new Color32[P * P]; lp[y * P + x] = col; }
                                }
                            }
                            else if (r >= 0 && r < 8 && lip != null)
                            {
                                var sn = map.SmoothNormal(gp);
                                if (sn.y >= -0.5f && sn != Vector2.zero)
                                {
                                    Color32 col;
                                    if (Mathf.Abs(sn.y) >= Mathf.Abs(sn.x)) col = lip[(31 - r) * lw + 2 * 32 + x];
                                    else if (sn.x < 0f) col = lip[Mod(gy) * lw + 0 * 32 + r];
                                    else col = lip[Mod(gy) * lw + 1 * 32 + 31 - r];
                                    if (col.a > 0) { if (lp == null) lp = new Color32[P * P]; lp[y * P + x] = col; }
                                }
                            }
                        }
                        // ---- side face (sky pixels only)
                        if (side != null && !land)
                        {
                            int dl = 0, dr = 0;
                            for (int d = 1; d <= SideWidth && (dl == 0 || dr == 0); d++)
                            {
                                if (dl == 0 && map.PixelLand(gx - d, gy)) dl = d;
                                if (dr == 0 && map.PixelLand(gx + d, gy)) dr = d;
                            }
                            if (dl == 0 && dr == 0) continue;
                            bool east = dl > 0 && (dr == 0 || dl <= dr);
                            int dd = east ? dl : dr;
                            var src = new Vector2((east ? gx - dl : gx + dr) + 0.5f, gy + 0.5f);
                            var sn = map.SmoothNormal(src);
                            float nx = east ? sn.x : -sn.x;
                            float f = Mathf.Clamp01((nx - 0.6f) / (0.92f - 0.6f));
                            int wpx = Mathf.RoundToInt(SideWidth * f);
                            int h = dd - 1;
                            if (h >= wpx) continue;
                            Color32 cc;
                            if (sideN != null && wpx <= NarrowWidth)
                            {
                                // narrow face: the 11-px frames (grass column 17 on W, 14 on E), compressed to wpx
                                int s = wpx <= 1 ? 0 : Mathf.RoundToInt(h * (NarrowWidth - 1f) / (wpx - 1));
                                int vk = (int)(Hash(FloorDiv(gy), east ? 1 : 0, vseed) % 2u);
                                int col = east ? 31 - SideGrassCol + s : SideGrassCol - s;
                                cc = sideN[Mod(gy) * nw + (vk * 2 + (east ? 1 : 0)) * 32 + col];
                            }
                            else
                            {
                                int s = wpx <= 1 ? 0 : Mathf.RoundToInt(h * (SideWidth - 1f) / (wpx - 1));
                                var frs = east ? ef : wf;
                                int vk = (int)(Hash(FloorDiv(gy), east ? 1 : 0, vseed) % (uint)frs.Length);
                                int col = east ? 31 - SideGrassCol + s : SideGrassCol - s;
                                cc = side[Mod(gy) * sw + frs[vk] * 32 + col];
                            }
                            if (cc.a == 0) continue;
                            if (east) cc = new Color32((byte)(cc.r * SideEastShade.r), (byte)(cc.g * SideEastShade.g), (byte)(cc.b * SideEastShade.b), cc.a);
                            if (sp == null) sp = new Color32[P * P];
                            sp[y * P + x] = cc;
                        }
                    }
                if (lp != null) { Add(lp, "l", out var h); lipAt.Add((c, h)); }
                if (sp != null) { Add(sp, "s", out var h); sideAt.Add((c, h)); }
            }
            var tiles = Atlas("coast_edge_" + biome, P, P, 0.5f, new List<Piece>(pieces.Values));
            Tilemap Map(Transform parent, string name, int order, string layer)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var tm = go.AddComponent<Tilemap>();
                var tr = go.AddComponent<TilemapRenderer>();
                tr.sortingLayerName = layer;
                tr.sortingOrder = order;
                return tm;
            }
            var lipMap = Map(root, "CoastLip", 9, "Ground");
            var sideMap = Map(root, "CoastSide", -4, "Ground");
            foreach (var (c, h) in lipAt) if (tiles.TryGetValue(pieces[h].key, out var t)) lipMap.SetTile(new Vector3Int(c.x, c.y, 0), t);
            foreach (var (c, h) in sideAt) if (tiles.TryGetValue(pieces[h].key, out var t)) sideMap.SetTile(new Vector3Int(c.x, c.y, 0), t);
            lipMap.CompressBounds(); sideMap.CompressBounds();

            var shadow = isl.transform.Find("Shadow");
            if (shadow != null)
            {
                var oldS = shadow.Find("Side");
                if (oldS != null) Object.DestroyImmediate(oldS.gameObject);
                var sRoot = new GameObject("Side").transform;
                sRoot.SetParent(shadow, false);
                var tm = Map(sRoot, "CoastSide", 20, WorldBuilder.SkyLayer);
                tm.color = new Color(0f, 0f, 0f, 0.35f);
                foreach (var (c, h) in sideAt) if (tiles.TryGetValue(pieces[h].key, out var t)) tm.SetTile(new Vector3Int(c.x, c.y, 0), t);
                tm.CompressBounds();
            }
        }
    }
}
