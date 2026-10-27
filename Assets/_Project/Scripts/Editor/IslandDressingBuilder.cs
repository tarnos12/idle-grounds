using System.Collections.Generic;
using System.IO;
using IdleGrounds.Game;
using IdleGrounds.Game.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// ADR 0005 "islands are dressed, not flat": paints each Island's <c>Dressing</c> child - a handful of overlay
    /// Tilemaps on the Ground sorting layer between the ground fill (0) and the zone tints (10+):
    /// <list type="bullet">
    /// <item>Macro (1): large organic light / dark ground patches (generated 160/256 px blob decals, one tile cell each,
    ///   tinted per biome from the delivered fill palette) - the stand-in for ART-SPEC §3.6 <c>island_&lt;biome&gt;_macro</c>.</item>
    /// <item>PatchA/PatchB (2/3): <c>zone_&lt;item&gt;_patch</c> blotches over the generator fields (second layer offset half a cell).</item>
    /// <item>Water (3): Fishing pond (generated from <c>island_fishing_water</c> frames, animated).</item>
    /// <item>Path / Plaza (4/5): Center flagstone plaza around the Altar + paths to the work fields.</item>
    /// <item>Scatter (6): sparse <c>island_&lt;biome&gt;_scatter</c> decorations; Glow (7): volcano lava cracks / celestial twinkle.</item>
    /// </list>
    /// Deterministic (seeded by the Island key), tile cells only (scene size), integer pixel scale. Called from
    /// <see cref="IslandCoastBuilder.RebuildDerived"/> and the menu below.
    /// </summary>
    public static class IslandDressingBuilder
    {
        const string Inc = "Assets/_Project/Art/Incoming/islands/";
        const string ArtDir = IslandArtBuilder.IslandDir + "/Dressing";
        const string TileDir = IslandArtBuilder.TileDir + "/Dressing";
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";
        const string Layer = "Ground";
        const int Cells = Island.Cells;
        public const string RootName = "Dressing";

        static readonly string[] Biomes = { "center", "farm", "mine", "fishing", "volcano", "grove", "celestial" };

        [MenuItem("Idle Grounds/World/Rebuild Island Dressing")]
        public static void RebuildMenu()
        {
            int n = BuildAll();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[IdleGrounds] Island dressing rebuilt for " + n + " Islands.");
        }

        public static int BuildAll()
        {
            using var edit = IslandPrefabs.Edit();
            int n = 0;
            foreach (var isl in Object.FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!string.IsNullOrEmpty(isl.islandKey) && isl.name == "Island_" + isl.islandKey) { BuildIsland(isl); n++; }
            return n;
        }

        // ------------------------------------------------------------------ assets

        class Kit
        {
            public Tile[] macroBig, macroSmall;
            public Tile[] scatter, patch, path, plaza;
            public Tile[] macroArt, flowers, lip;      // ART-SPEC §3.6 delivered macro patches, flower clusters, coast grass lip (recoloured per biome)
            public Tile[] sideW, sideE;                // W / E 3/4 side faces: straight variants (delivered, Islands-034)
            public Tile[] sideWEnd, sideEEnd;          // ... and their feet above the south wall's free end (composed, per variant)
            public AnimatedTile pond;
            public AnimatedTile[] glow;
            public Color light, dark;
            public Color fillBase;     // the fill's most common colour (VP-14 value layer)
            public bool textured;
            public bool busy;          // textured or pale fill: fewer macro decals
            public bool pale;
        }

        static Tile[] _macroBig, _macroSmall, _path, _plaza;

        static Kit LoadKit(string biome)
        {
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(TileDir);
            var k = new Kit();
            if (_macroBig == null || _macroBig.Length == 0 || _macroBig[0] == null)
            {
                _macroBig = Tiles("Macro256", EnsureMacro(256, 4, 101));
                _macroSmall = Tiles("Macro160", EnsureMacro(160, 4, 202));
                _path = Tiles("CenterPath", EnsureWarmStone(Inc + "island_center_path_32x32_15f.png", "dress_center_path_warm_32x32_15f.png", 15, 0.84f, 1.07f));
                _plaza = Tiles("CenterPlaza", EnsureWarmStone(Inc + "island_center_plaza_32x32_3f.png", "dress_center_plaza_warm_32x32_3f.png", 3, 0.9f, 1.04f));
            }
            k.macroBig = _macroBig; k.macroSmall = _macroSmall; k.path = _path; k.plaza = _plaza;
            k.scatter = Tiles("Scatter_" + biome, IslandArtBuilder.Frames($"{Inc}island_{biome}_scatter_32x32_4f.png"));
            // every biome macro (incl. fishing v4, Islands-039: P05 damp / P07 dry patches, too hard-edged at full strength)
            // is softened toward its fill
            var mf = EnsureSoftened($"{Inc}island_{biome}_macro_96x96_4f.png", $"dress_macro_{biome}_96x96_4f.png", 4, 96, MacroStrength, $"{Inc}island_{biome}_ground_fill_32x32_3f.png");
            k.macroArt = mf.Length >= 4 ? Tiles("MacroArt_" + biome, mf) : null;
            var ff = IslandArtBuilder.Frames($"{Inc}island_{biome}_flowers_32x32_4f.png");
            k.flowers = ff.Length > 0 ? Tiles("Flowers_" + biome, ff) : null;
            k.lip = EdgeLipTiles(biome);
            var sw = EnsureSideWall(biome);
            if (sw != null)
            {
                var all = new List<Sprite>(sw.w); all.AddRange(sw.e); all.AddRange(sw.wEnd); all.AddRange(sw.eEnd);
                var t = Tiles("SideWall_" + biome, all.ToArray());
                int nw = sw.w.Length, ne = sw.e.Length;
                k.sideW = new Tile[nw]; k.sideE = new Tile[ne]; k.sideWEnd = new Tile[nw]; k.sideEEnd = new Tile[ne];
                System.Array.Copy(t, 0, k.sideW, 0, nw);
                System.Array.Copy(t, nw, k.sideE, 0, ne);
                System.Array.Copy(t, nw + ne, k.sideWEnd, 0, nw);
                System.Array.Copy(t, nw + ne + nw, k.sideEEnd, 0, ne);
            }
            FillTones(biome, out k.light, out k.dark, out k.textured);
            k.fillBase = FillBase(biome);
            k.pale = k.light.r + k.light.g + k.light.b > 2.0f;
            k.busy = k.textured || k.pale;
            if (biome == "fishing") k.pond = Animated("Pond_fishing", EnsurePond(), 0, 3f);
            var glowFrames = biome == "volcano" ? IslandArtBuilder.Frames(Inc + "island_volcano_lavacrack_32x32_4f.png")
                : biome == "celestial" ? IslandArtBuilder.Frames(Inc + "island_celestial_twinkle_32x32_4f.png") : null;
            if (glowFrames != null && glowFrames.Length > 0)
            {
                k.glow = new AnimatedTile[4];
                for (int i = 0; i < 4; i++) k.glow[i] = Animated($"Glow_{biome}_{i}", glowFrames, i + 1, biome == "volcano" ? 4f : 5f);
            }
            return k;
        }

        static Tile[] PatchTiles(string item)
        {
            var f = IslandArtBuilder.Frames($"{Inc}zone_{item}_patch_32x32_3f.png");
            return f.Length > 0 ? Tiles("Patch_" + item, f) : null;
        }

        /// <summary>Worn-earth tone under a generator field: the patch art's mean colour pulled toward the biome's shade tone.</summary>
        static Color FieldEarth(string item, Kit kit)
        {
            var px = ReadPng($"{Inc}zone_{item}_patch_32x32_3f.png", out _, out _);
            Color sum = Color.black;
            int n = 0;
            if (px != null) foreach (var c in px) if (c.a > 200) { sum += (Color)c; n++; }
            Color mean = n > 0 ? sum / n : kit.dark;
            var e = Color.Lerp(kit.dark, mean, 0.42f) * 0.92f;
            e.a = 1f;
            return e;
        }

        /// <summary>
        /// Weathered-stone copy of a delivered Center path / plaza strip: the pale blue-grey flagstone palette
        /// (base #b7bccd, crack #7a809a, highlight #e8ecf3) is remapped to a muted warm olive stone only a step lighter
        /// than the grass, with the crack / grout contrast softened; grass, earth seam and gold glyph pixels are kept.
        /// </summary>
        static Sprite[] EnsureWarmStone(string src, string dstName, int frames, float crack, float highlight)
        {
            string path = $"{ArtDir}/{dstName}";
            var px = ReadPng(src, out int w, out int h);
            if (px == null) return IslandArtBuilder.Frames(src);
            var stone = new Color32(0xa6, 0xa5, 0x80, 255);
            Color32 Mul(float k) => new Color32((byte)Mathf.Clamp(stone.r * k, 0, 255), (byte)Mathf.Clamp(stone.g * k, 0, 255), (byte)Mathf.Clamp(stone.b * k, 0, 255), 255);
            var map = new Dictionary<Color32, Color32>
            {
                { new Color32(0xb7, 0xbc, 0xcd, 255), stone },
                { new Color32(0x7a, 0x80, 0x9a, 255), Mul(crack) },
                { new Color32(0xe8, 0xec, 0xf3, 255), Mul(highlight) },
            };
            for (int i = 0; i < px.Length; i++)
                if (px[i].a > 0 && map.TryGetValue(new Color32(px[i].r, px[i].g, px[i].b, 255), out var m)) px[i] = m;
            WriteIfChanged(path, w, h, px);
            Import(path, frames, 32);
            return IslandArtBuilder.Frames(path);
        }

        /// <summary>Share of each cliff-wall frame's left-to-right brightness ramp removed at build time (0 = as delivered).</summary>
        const float DetrendStrength = 0.75f;

        /// <summary>
        /// Integration copy of the delivered 3/4 cliff wall (<c>island_&lt;biome&gt;_cliffwall_32x96_8f</c>) with its seam defects
        /// repaired for the tilemap: in the mid frames (0-2, 7) the 1-2 px edge columns hold holes (fully transparent
        /// columns) and an identical flat "normalised seam" column that drew a hard vertical line every 32 px. Holes in the
        /// wall body are filled from the nearest opaque pixel toward the frame's interior and the outer columns copy their
        /// inner neighbours. The ragged bottom 16 px (fade into the sky) and the end / inner frames are left untouched.
        /// </summary>
        public static Sprite[] RepairedCliffWall(string biome)
        {
            string src = IslandArtBuilder.CliffWallPath(biome);
            var px = ReadPng(src, out int w, out int h);
            if (px == null || w != 256 || h != 96) return IslandArtBuilder.Frames(src);
            Directory.CreateDirectory(ArtDir);
            foreach (int f in new[] { 0, 1, 2, 7 })
                for (int r = 0; r < 80; r++)             // r = rows from the top; leave the 16 px fade alone
                {
                    int y = h - 1 - r;
                    Color32 At(int x) => px[y * w + f * 32 + x];
                    void Put(int x, Color32 c) => px[y * w + f * 32 + x] = c;
                    // only rows where the wall body exists (some interior pixel opaque)
                    if (At(8).a == 0 && At(16).a == 0 && At(24).a == 0) continue;
                    foreach (int x in new[] { 1, 2, 29, 30 })
                        if (At(x).a == 0)
                        {
                            int dir = x < 16 ? 1 : -1;
                            for (int k = 1; k <= 6; k++) { var c = At(x + dir * k); if (c.a > 0) { Put(x, c); break; } }
                        }
                    if (At(1).a > 0) Put(0, At(1));
                    if (At(30).a > 0) Put(31, At(30));
                }
            // v2 (2026-10-11) still ships holes / a shared seam column on the INNER sides of the end frames (3: left end,
            // 4: right end, 5: inner left, 6: inner right) where they meet a mid frame: same fill on that side only
            foreach (var (f, xs, edge, from) in new[] { (3, new[] { 29, 30 }, 31, 30), (5, new[] { 29, 30 }, 31, 30), (4, new[] { 1, 2 }, 0, 1), (6, new[] { 1, 2 }, 0, 1) })
                for (int r = 0; r < 80; r++)
                {
                    int y = h - 1 - r;
                    if (px[y * w + f * 32 + 16].a == 0 && px[y * w + f * 32 + 8].a == 0 && px[y * w + f * 32 + 24].a == 0) continue;
                    foreach (int x in xs)
                        if (px[y * w + f * 32 + x].a == 0)
                        {
                            int dir = x < 16 ? 1 : -1;
                            for (int k = 1; k <= 6; k++) { var c = px[y * w + f * 32 + x + dir * k]; if (c.a > 0) { px[y * w + f * 32 + x] = c; break; } }
                        }
                    if (px[y * w + f * 32 + from].a > 0) px[y * w + f * 32 + edge] = px[y * w + f * 32 + from];
                }
            // the ragged 16 px fade: the edge columns hold the same "normalised" mist pixels in every frame (light stubs
            // every 32 px under the wall) -> they copy their inner neighbour, transparency included
            for (int f = 0; f < 8; f++)
                for (int r = 80; r < 96; r++)
                {
                    int y = h - 1 - r;
                    if (f != 3) px[y * w + f * 32] = px[y * w + f * 32 + 1];
                    if (f != 4) px[y * w + f * 32 + 31] = px[y * w + f * 32 + 30];
                }
            // picket-fence rhythm: every frame is lit light-left / dark-right, so a run of cells reads as a fence. Remove
            // most of each frame's left-to-right brightness ramp (least-squares slope of the column mean luma over the
            // body rows), so neighbouring cells meet at the same brightness.
            for (int f = 0; f < 8; f++)
            {
                var colL = new float[32];
                var colN = new int[32];
                for (int r = 0; r < 80; r++)
                    for (int x = 0; x < 32; x++)
                    {
                        var c = px[(h - 1 - r) * w + f * 32 + x];
                        if (c.a == 0) continue;
                        colL[x] += c.r * 0.3f + c.g * 0.59f + c.b * 0.11f; colN[x]++;
                    }
                float sx = 0, sy = 0, sxx = 0, sxy = 0; int n = 0;
                for (int x = 0; x < 32; x++)
                {
                    if (colN[x] < 20) continue;
                    float l = colL[x] / colN[x];
                    sx += x; sy += l; sxx += x * x; sxy += x * l; n++;
                }
                if (n < 16) continue;
                float slope = (n * sxy - sx * sy) / (n * sxx - sx * sx), mean = sy / n, cx = sx / n;
                if (mean < 1f) continue;
                for (int r = 0; r < h; r++)
                    for (int x = 0; x < 32; x++)
                    {
                        int i = (h - 1 - r) * w + f * 32 + x;
                        var c = px[i];
                        if (c.a == 0) continue;
                        float k = 1f - DetrendStrength * slope * (x - cx) / mean;
                        px[i] = new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), c.a);
                    }
            }
            // the ragged grass top dips up to ~10 px deep in places; the wall tucks its top 6 px behind the coast edge
            // (IslandCoastBuilder.WallDrop), so deeper dips showed sky specks between the coast edge and the wall's grass.
            // Each body column's first opaque pixel is repeated up to that tuck line.
            const int Tuck = 6;
            for (int f = 0; f < 8; f++)
                for (int x = 0; x < 32; x++)
                {
                    int t = -1;
                    for (int r = 0; r < 16 && t < 0; r++) if (px[(h - 1 - r) * w + f * 32 + x].a > 0) t = r;
                    if (t <= Tuck) continue;
                    bool body = px[(h - 1 - 20) * w + f * 32 + x].a > 0;       // a wall column, not the open side of an end frame
                    if (!body) continue;
                    var c = px[(h - 1 - t) * w + f * 32 + x];
                    for (int r = Tuck; r < t; r++) px[(h - 1 - r) * w + f * 32 + x] = c;
                }
            string path = $"{ArtDir}/dress_cliffwall_{biome}_32x96_8f.png";
            WriteIfChanged(path, w, h, px);
            Import(path, 8, 32);
            return IslandArtBuilder.Frames(path);
        }

        /// <summary>Px the free wall ends reach past their cell: out to the W / E side face's outer column, so the face's
        /// tapering foot lands on the wall's end instead of beside it (the blob coast edge is 3 px inside the cell).</summary>
        public const int WallEndReach = 3;
        /// <summary>Px a tucked wall end reaches under the land beside it: fills the blob tiles' 3 px transparent inset,
        /// so no sky shows between that land's grass edge and the wall behind it.</summary>
        public const int WallTuckReach = 4;

        /// <summary>
        /// The south cliff wall as explicit per-joint tiles (IslandCoastBuilder.BuildCliff picks one per rim cell from the
        /// LAND around it, not just the neighbouring wall cells): 3 mid variants (0-2) and joint pieces composed at build
        /// time from the delivered frames (<c>dress_cliffwall_&lt;biome&gt;_joints</c>, custom-pivot sprites wider than a cell):
        ///   endL / endR - free end (frame 3 / 4) at an outer corner, reaching <see cref="WallEndReach"/> px outward so it
        ///                 meets the side face above it (a mid column fills the 3 px it leaves on the inner side);
        ///   tuckL / tuckR - inner frame (5 / 6) where the wall runs on behind land further south (a concave step) or a
        ///                 higher wall, reaching <see cref="WallTuckReach"/> px under that land (mid columns);
        ///   pair[l * 2 + r] - a cell special on BOTH sides (l, r: 0 end, 1 tuck): left half of one, right half of the other
        ///                 (art request: a delivered narrow wall piece).
        /// </summary>
        public static WallSet EnsureWallSet(string biome)
        {
            if (!File.Exists(IslandArtBuilder.CliffWallPath(biome))) return null;
            var f = RepairedCliffWall(biome);
            if (f.Length < 7) return null;
            var pieces = WallPieces(biome, false);
            if (pieces == null) return null;
            int total = 0;
            foreach (var p in pieces) total += p.u1 - p.u0 + 1;
            var o = new Color32[total * 96];
            var rects = new List<(int x, int w, float pivot)>();
            int x0 = 0;
            foreach (var p in pieces)
            {
                int pw = p.u1 - p.u0 + 1;
                for (int y = 0; y < 96; y++)
                    for (int u = p.u0; u <= p.u1; u++) o[y * total + x0 + u - p.u0] = p.at(u, y);
                rects.Add((x0, pw, (16f - p.u0) / pw));
                x0 += pw;
            }
            string path = $"{ArtDir}/dress_cliffwall_{biome}_joints.png";
            WriteIfChanged(path, total, 96, o);
            var js = ImportRects(path, rects);
            if (js.Length < 8) return null;
            var t = Tiles("CliffWall_" + biome, new[] { f[0], f[1], f[2], js[0], js[1], js[2], js[3], js[4], js[5], js[6], js[7] });
            return new WallSet { mid = new[] { t[0], t[1], t[2] }, endL = t[3], endR = t[4], tuckL = t[5], tuckR = t[6], pair = new[] { t[7], t[8], t[9], t[10] } };
        }

        /// <summary>
        /// The joint pieces of the repaired cliff wall (see <see cref="EnsureWallSet"/>): u range (px relative to the cell's
        /// left edge) and the source pixel (u, y bottom-up) for every column. Order: endL, endR, tuckL, tuckR, pair[0..3];
        /// with <paramref name="withMids"/> the 3 mid frames come first.
        /// </summary>
        static List<(int u0, int u1, System.Func<int, int, Color32> at)> WallPieces(string biome, bool withMids)
        {
            string src = $"{ArtDir}/dress_cliffwall_{biome}_32x96_8f.png";
            var px = ReadPng(src, out int w, out int h);
            if (px == null || w != 256 || h != 96) return null;
            Color32 C(int frame, int col, int y) => px[y * w + frame * 32 + col];
            int E = WallEndReach, T = WallTuckReach;
            var pieces = new List<(int u0, int u1, System.Func<int, int, Color32> at)>
            {
                (-E, 31, (u, y) => u <= 31 - E ? C(3, u + E, y) : C(0, u, y)),          // endL
                (0, 31 + E, (u, y) => u < E ? C(0, u, y) : C(4, u - E, y)),             // endR
                (-T, 31, (u, y) => u < 0 ? C(0, u + 32, y) : C(5, u, y)),               // tuckL
                (0, 31 + T, (u, y) => u <= 31 ? C(6, u, y) : C(0, u - 32, y)),          // tuckR
            };
            for (int l = 0; l < 2; l++)
                for (int r = 0; r < 2; r++)
                {
                    var L = pieces[l == 0 ? 0 : 2];
                    var R = pieces[r == 0 ? 1 : 3];
                    pieces.Add((L.u0, R.u1, (u, y) => u < 16 ? L.at(u, y) : R.at(u, y)));
                }
            if (withMids)
                for (int v = 2; v >= 0; v--) { int fv = v; pieces.Insert(0, (0, 31, (u, y) => C(fv, u, y))); }
            return pieces;
        }

        // ------------------------------------------------------------------ 45° diagonal coast (owner 2026-10-12: "we can't have 90 degrees angles")

        /// <summary>Diagonal coast cell codes (which corner of the cell is sky): the cell's other three corners are land and
        /// the coast runs along the diagonal between them. Tile index = code - 1.</summary>
        public const int SkyNE = 1, SkyNW = 2, SkySE = 3, SkySW = 4;
        const string DiagTileName = "CoastDiag_";

        /// <summary>Diagonal code of a Coast tilemap tile (0 = not a diagonal tile).</summary>
        public static int DiagCode(TileBase t)
        {
            if (t == null) return 0;
            string n = t.name;
            if (!n.StartsWith(DiagTileName) || n.Length < 2) return 0;
            int k = n[n.Length - 1] - '0';
            return k >= 0 && k <= 3 ? k + 1 : 0;
        }

        /// <summary>Signed px distance of pixel (x, y bottom-up) from the visible diagonal coast line of a cell with sky
        /// corner <paramref name="code"/>: k &lt; 0 sky, k = 0 the outer rim colour, 1 the inner rim colour, &gt;= 2 ground. The
        /// line sits 3 px inside the true diagonal (the blob tiles' transparent inset), so it meets the straight edges'
        /// rims and the blob inner-corner notches pixel for pixel.</summary>
        public static int DiagK(int code, int x, int y)
        {
            switch (code)
            {
                case SkySE: return y - x - 3;
                case SkyNW: return x - y - 3;
                case SkyNE: return 28 - x - y;
                default: return x + y - 34;          // SkySW
            }
        }

        /// <summary>
        /// The 4 diagonal coast tiles of a biome (index = code - 1: sky NE, NW, SE, SW): a half cell of ground cut along the
        /// diagonal with the blob rim's two colours drawn on the cut. Prefers the delivered
        /// <c>island_&lt;biome&gt;_ground_diag_32x32_4f</c> (same frame order); until then composed at build time from the blob's
        /// rim colours (frame 0, column 16) and the ground fill (frame 0) - palette-faithful, pixel-crisp
        /// (<c>dress_ground_diag_&lt;biome&gt;_32x32_4f</c>).
        /// </summary>
        public static Tile[] EnsureDiagGround(string biome)
        {
            var del = IslandArtBuilder.Frames($"{Inc}island_{biome}_ground_diag_32x32_4f.png");
            if (del.Length >= 4) return Tiles(DiagTileName + biome, new[] { del[0], del[1], del[2], del[3] });
            var blob = ReadPng(IslandArtBuilder.BlobPath(biome), out int bw, out int bh);
            if (blob == null || bh != 32) blob = ReadPng($"{Inc}island_{biome}_ground_blob_32x32_47f.png", out bw, out bh);
            if (blob == null || bh != 32) return null;
            var fill = ReadPng($"{Inc}island_{biome}_ground_fill_32x32_3f.png", out int fw, out int fh);
            int interior = System.Array.IndexOf(IslandArtBuilder.BlobMasks(), 255);
            Color32 Fill(int x, int y) => fill != null && fh == 32 ? fill[y * fw + x] : blob[y * bw + interior * 32 + x];
            Color32 r1 = blob[(31 - 3) * bw + 16], r2 = blob[(31 - 4) * bw + 16];
            var o = new Color32[128 * 32];
            for (int f = 0; f < 4; f++)
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        int k = DiagK(f + 1, x, y);
                        o[y * 128 + f * 32 + x] = k < 0 ? new Color32(0, 0, 0, 0) : k == 0 ? r1 : k == 1 ? r2 : Fill(x, y);
                    }
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(TileDir);
            string path = $"{ArtDir}/dress_ground_diag_{biome}_32x32_4f.png";
            WriteIfChanged(path, 128, 32, o);
            Import(path, 4, 32);
            var s = IslandArtBuilder.Frames(path);
            return s.Length >= 4 ? Tiles(DiagTileName + biome, s) : null;
        }

        /// <summary>South walls under the two south-facing diagonals: <c>rise</c> = sky SE (the coast rises to the right),
        /// <c>fall</c> = sky SW (it falls to the right). Same joint layout as <see cref="WallSet"/>.</summary>
        public class DiagWallSet { public WallSet rise, fall; }

        /// <summary>Canvas height of a sheared diagonal wall piece: the 96 px wall + up to 39 px of shear + 1.</summary>
        const int DiagWallH = 136, DiagWallBase = 4;

        /// <summary>
        /// 45° south cliff walls under the SE / SW-facing diagonals. Prefers the delivered
        /// <c>island_&lt;biome&gt;_cliffwall_diag_32x96_4f</c> (frame 0 rises to the right, 1 falls; 2/3 transitions not used yet).
        /// Until then every straight joint piece (3 mids, free ends, tucked ends, pairs) is SHEARED in 1 px steps: column u
        /// moves up u px (rise) or 31 - u px (fall), so the wall's grass top follows the diagonal coast 3 px behind its
        /// visible rim (like the straight wall's 6 px tuck) and one piece tiles into the next one cell right + one cell up.
        /// Pixel-for-pixel copies of the repaired delivered frames (no new colours). Custom pivots put column 31 (rise) /
        /// column 0 (fall) exactly where the straight wall's column would be, so straight and diagonal runs join seamlessly.
        /// </summary>
        public static DiagWallSet EnsureDiagWallSet(string biome)
        {
            if (EnsureWallSet(biome) == null) return null;
            string dp = $"{Inc}island_{biome}_cliffwall_diag_32x96_4f.png";
            var del = ReadPng(dp, out int dw, out int dh);
            if (del != null && dw >= 64)
            {
                // delivered: its top row sits where the composed piece's highest grass row is (79 px above the pivot)
                string copy = $"{ArtDir}/dress_cliffwall_diag_{biome}_delivered.png";
                WriteIfChanged(copy, dw, dh, del);
                var rs = new List<(int x, int w, float pivot)> { (0, 32, 0.5f), (32, 32, 0.5f) };
                var ds = ImportRects(copy, rs, 1f - 79f / dh);
                if (ds.Length >= 2)
                {
                    WallSet One(string n, Sprite sp)
                    {
                        var t = Tiles(n, new[] { sp })[0];
                        return new WallSet { mid = new[] { t, t, t }, endL = t, endR = t, tuckL = t, tuckR = t, pair = new[] { t, t, t, t } };
                    }
                    return new DiagWallSet { rise = One("CliffWallDiagRiseD_" + biome, ds[0]), fall = One("CliffWallDiagFallD_" + biome, ds[1]) };
                }
            }
            var pieces = WallPieces(biome, true);
            if (pieces == null) return null;
            WallSet Build(bool rise)
            {
                int total = 0;
                foreach (var p in pieces) total += p.u1 - p.u0 + 1;
                var o = new Color32[total * DiagWallH];
                var rects = new List<(int x, int w, float pivot)>();
                int x0 = 0;
                foreach (var p in pieces)
                {
                    int pw = p.u1 - p.u0 + 1;
                    for (int u = p.u0; u <= p.u1; u++)
                    {
                        int s = rise ? u : 31 - u;
                        for (int y = 0; y < 96; y++) o[(y + s + DiagWallBase) * total + x0 + u - p.u0] = p.at(u, y);
                    }
                    rects.Add((x0, pw, (16f - p.u0) / pw));
                    x0 += pw;
                }
                string path = $"{ArtDir}/dress_cliffwall_diag_{(rise ? "rise" : "fall")}_{biome}.png";
                WriteIfChanged(path, total, DiagWallH, o);
                var js = ImportRects(path, rects, (48f + DiagWallBase) / DiagWallH);
                if (js.Length < 11) return null;
                var t = Tiles((rise ? "CliffWallDiagRise_" : "CliffWallDiagFall_") + biome, js);
                return new WallSet { mid = new[] { t[0], t[1], t[2] }, endL = t[3], endR = t[4], tuckL = t[5], tuckR = t[6], pair = new[] { t[7], t[8], t[9], t[10] } };
            }
            var r = Build(true);
            var f = Build(false);
            return r != null && f != null ? new DiagWallSet { rise = r, fall = f } : null;
        }

        /// <summary>Imports a horizontal strip of sprites with their own widths and horizontal pivots (vertical pivot centred,
        /// PPU 32); returns them in strip order.</summary>
        static Sprite[] ImportRects(string path, List<(int x, int w, float pivot)> rects, float py = 0.5f)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return new Sprite[0];
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Multiple;
            imp.spritePixelsPerUnit = 32;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 4096;
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            st.spriteGenerateFallbackPhysicsShape = false;
            imp.SetTextureSettings(st);
            imp.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(imp);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects();
            bool same = existing != null && existing.Length == rects.Count;
            for (int i = 0; same && i < rects.Count; i++)
                same = existing[i].rect == new Rect(rects[i].x, 0, rects[i].w, tex.height) && Mathf.Abs(existing[i].pivot.x - rects[i].pivot) < 1e-4f && Mathf.Abs(existing[i].pivot.y - py) < 1e-4f;
            if (!same)
            {
                string baseName = Path.GetFileNameWithoutExtension(path);
                var list = new SpriteRect[rects.Count];
                for (int i = 0; i < rects.Count; i++)
                    list[i] = new SpriteRect
                    {
                        name = baseName + "_" + i,
                        rect = new Rect(rects[i].x, 0, rects[i].w, tex.height),
                        alignment = SpriteAlignment.Custom,
                        pivot = new Vector2(rects[i].pivot, py),
                        spriteID = existing != null && i < existing.Length ? existing[i].spriteID : GUID.Generate(),
                    };
                provider.SetSpriteRects(list);
                provider.Apply();
                imp.SaveAndReimport();
            }
            var all = new List<Sprite>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Sprite s) all.Add(s);
            all.Sort((a, b) => a.rect.x.CompareTo(b.rect.x));
            return all.ToArray();
        }

        public class WallSet { public Tile[] mid, pair; public Tile endL, endR, tuckL, tuckR; }

        /// <summary>Transparent margin (px) of the delivered 47-blob coast tiles on their W / E / N edges (all 7 biomes):
        /// the visible coast edge sits this far inside the cell.</summary>
        const int CoastInset = 3;

        static readonly Color SideEastShade = new Color(0.82f, 0.82f, 0.88f, 1f);

        /// <summary>
        /// W / E 3/4 side faces (ART-SPEC §3.6, Islands-034): the delivered vertical faces, used as delivered (no rotation,
        /// no cut ends). Prefers <c>island_&lt;biome&gt;_sidewall_variants_32x32_8f</c> (0-2 W straight, 3-5 E straight,
        /// 6 W end, 7 E end) and falls back to <c>island_&lt;biome&gt;_sidewall_32x32_4f</c> (0 W, 1 E, 2 W end, 3 E end).
        /// Each face is 11 px wide: W frames sit in the LEFT columns (grass column 10 against the island, dark outer
        /// column 0), E frames in the RIGHT columns (grass column 21). Returned as a <see cref="SideSet"/>.
        /// </summary>
        public static SideSet EnsureSideWall(string biome)
        {
            string vp = $"{Inc}island_{biome}_sidewall_variants_32x32_8f.png", fp = $"{Inc}island_{biome}_sidewall_32x32_4f.png";
            var v = IslandArtBuilder.Frames(vp);
            SideSet s = null;
            if (v.Length >= 8) s = InwardEnds(biome, vp, new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, 6, 7, new[] { v[0], v[1], v[2] }, new[] { v[3], v[4], v[5] });
            else
            {
                var f = IslandArtBuilder.Frames(fp);
                if (f.Length >= 4) s = InwardEnds(biome, fp, new[] { 0 }, new[] { 1 }, 2, 3, new[] { f[0] }, new[] { f[1] });
            }
            return s;
        }

        /// <summary>
        /// The foot of a W / E face (the coast cell that also faces south, where the south wall's free end starts below it).
        /// The delivered end frames taper OUTWARD (toward the sky), so the face ended in a dark stub beside the wall's end
        /// cap. Composed instead from each straight variant: the same per-row width as the delivered end frame, but kept on
        /// the grass side, so the band narrows INTO the corner and lands on the wall's end cap (which reaches out to meet
        /// it, <see cref="WallEndReach"/>). Strip <c>dress_sidewall_&lt;biome&gt;_feet_32x32_&lt;2n&gt;f</c>: W feet, then E feet.
        /// </summary>
        static SideSet InwardEnds(string biome, string src, int[] wf, int[] ef, int wEndF, int eEndF, Sprite[] w, Sprite[] e)
        {
            var px = ReadPng(src, out int iw, out int ih);
            if (px == null || ih != 32) return new SideSet { w = w, e = e, wEnd = w, eEnd = e };
            int n = wf.Length, fw = 2 * n * 32;
            var o = new Color32[fw * 32];
            for (int y = 0; y < 32; y++)
            {
                int wdW = 0, wdE = 0;
                for (int x = 0; x < 32; x++)
                {
                    if (px[y * iw + wEndF * 32 + x].a > 0) wdW++;
                    if (px[y * iw + eEndF * 32 + x].a > 0) wdE++;
                }
                for (int i = 0; i < n; i++)
                {
                    for (int x = SideGrassCol + 1 - wdW; x <= SideGrassCol; x++) if (x >= 0) o[y * fw + i * 32 + x] = px[y * iw + wf[i] * 32 + x];
                    int ge = 31 - SideGrassCol;
                    for (int x = ge; x < ge + wdE && x < 32; x++) o[y * fw + (n + i) * 32 + x] = px[y * iw + ef[i] * 32 + x];
                }
            }
            string path = $"{ArtDir}/dress_sidewall_{biome}_feet_32x32_{2 * n}f.png";
            WriteIfChanged(path, fw, 32, o);
            Import(path, 2 * n, 32);
            var ft = IslandArtBuilder.Frames(path);
            if (ft.Length < 2 * n) return new SideSet { w = w, e = e, wEnd = w, eEnd = e };
            var we = new Sprite[n]; var ee = new Sprite[n];
            System.Array.Copy(ft, 0, we, 0, n); System.Array.Copy(ft, n, ee, 0, n);
            return new SideSet { w = w, e = e, wEnd = we, eEnd = ee };
        }

        public class SideSet { public Sprite[] w, e, wEnd, eEnd; }

        static readonly Dictionary<Sprite, int> _topMargin = new Dictionary<Sprite, int>();

        /// <summary>Fully transparent rows (px) above the first opaque pixel of a sprite (read from its PNG; cached).</summary>
        public static int OpaqueTopMargin(Sprite s)
        {
            if (s == null) return 0;
            if (_topMargin.TryGetValue(s, out var m)) return m;
            m = 0;
            var px = ReadPng(AssetDatabase.GetAssetPath(s.texture), out int w, out int h);
            if (px != null)
            {
                var r = s.rect;
                int x0 = (int)r.x, x1 = (int)r.xMax, yTop = (int)r.yMax - 1, y0 = (int)r.y;
                for (int y = yTop; y >= y0; y--, m++)
                {
                    bool any = false;
                    for (int x = x0; x < x1 && !any; x++) any = px[y * w + x].a > 0;
                    if (any) break;
                }
            }
            _topMargin[s] = m;
            return m;
        }

        /// <summary>
        /// Integration copy of <c>island_underside_roots_3q_192x160</c> (Islands-037 touch-up) with the leftover 4-px speck
        /// in its bottom-left corner (x &lt; 24, y &gt;= 145, top-down) cleared. The incoming file is never edited.
        /// </summary>
        public static Sprite MaskedUndersideRoots()
        {
            string src = Inc + "island_underside_roots_3q_192x160.png";
            var px = ReadPng(src, out int w, out int h);
            if (px == null) return IslandArtBuilder.Single(src);
            for (int y = 145; y < h; y++)
                for (int x = 0; x < 24 && x < w; x++) px[(h - 1 - y) * w + x] = new Color32(0, 0, 0, 0);
            Directory.CreateDirectory(ArtDir);
            string path = $"{ArtDir}/dress_underside_roots_3q_192x160.png";
            WriteIfChanged(path, w, h, px);
            Import(path, 1, w);
            var f = IslandArtBuilder.Frames(path);
            return f.Length > 0 ? f[0] : null;
        }

        /// <summary>Grass column of the delivered W face (the E face mirrors it at 31 - this).</summary>
        const int SideGrassCol = 10;

        /// <summary>How far the delivered macro patches keep their own light / dark tones (1 = as delivered, 0 = the fill base).</summary>
        const float MacroStrength = 0.4f;

        /// <summary>
        /// Copy of a delivered strip with every colour pulled toward the island's ground-fill base colour (the most common
        /// colour of <paramref name="baseFrom"/>) by <paramref name="k"/> (k = 1: identical copy). Lets the macro patches sit
        /// calmly ON the fill (a step lighter / darker, never a darker sticker) without editing the artist's file.
        /// </summary>
        static Sprite[] EnsureSoftened(string src, string dstName, int frames, int frameW, float k, string baseFrom)
        {
            var px = ReadPng(src, out int w, out int h);
            if (px == null) return new Sprite[0];
            if (k >= 0.999f) return IslandArtBuilder.Frames(src);
            var basePx = ReadPng(baseFrom, out _, out _) ?? px;
            var count = new Dictionary<Color32, int>();
            foreach (var c in basePx) if (c.a > 200) { count.TryGetValue(c, out var v); count[c] = v + 1; }
            Color32 b = new Color32(0, 0, 0, 255); int best = -1;
            foreach (var kv in count) if (kv.Value > best) { best = kv.Value; b = kv.Key; }
            for (int i = 0; i < px.Length; i++)
                if (px[i].a > 0) { var c = Color32.Lerp(b, px[i], k); c.a = px[i].a; px[i] = c; }
            string path = $"{ArtDir}/{dstName}";
            WriteIfChanged(path, w, h, px);
            Import(path, frames, frameW);
            return IslandArtBuilder.Frames(path);
        }

        /// <summary>
        /// ART-SPEC §3.6 <c>island_edge_lip_32x32_4f</c> (neutral green: W, E, N, outer NW corner) recoloured to the biome's
        /// delivered fill: the lip's darkest tone (the 1 px rim) -> the fill's shade tone darkened, the others by
        /// brightness rank onto the fill's shade / base / light tones. Center's fill shares the lip palette (identity).
        /// An Island with its own delivered lip (island-decor.json <c>lip</c>, e.g. the §3.8 Fishing snow cornice, same
        /// frames as the shared lip) uses that file as drawn.
        /// </summary>
        static Tile[] EdgeLipTiles(string biome)
        {
            string own = IslandDecor.IncomingFile(IslandDecor.For(biome)?.lip, "islands");
            int ow = 0, oh = 0;
            var ownPx = own != null ? ReadPng(own, out ow, out oh) : null;
            if (ownPx != null && oh == 32 && ow >= 128)
            {
                string op = $"{ArtDir}/dress_edge_lip_{biome}_32x32_4f.png";
                WriteIfChanged(op, ow, oh, ownPx);
                Import(op, 4, 32);
                var of = IslandArtBuilder.Frames(op);
                return of.Length >= 4 ? Tiles("EdgeLip_" + biome, of) : null;
            }
            var px = ReadPng(IslandArtBuilder.EdgeLipPath, out int w, out int h);
            var fill = ReadPng($"{Inc}island_{biome}_ground_fill_32x32_3f.png", out _, out _);
            if (px == null || fill == null) return null;
            float L(Color32 c) => c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            var fc = new Dictionary<Color32, int>();
            foreach (var c in fill) if (c.a > 200) { fc.TryGetValue(c, out var v); fc[c] = v + 1; }
            var fl = new List<KeyValuePair<Color32, int>>(fc);
            fl.Sort((a, b) => b.Value.CompareTo(a.Value));
            Color32 fBase = fl[0].Key, fDark = fBase, fLight = fBase;
            for (int i = 1; i < Mathf.Min(6, fl.Count); i++)
            {
                var c = fl[i].Key;
                if (L(c) < L(fDark) && fDark.Equals(fBase)) fDark = c;
                if (L(c) > L(fLight) && fLight.Equals(fBase)) fLight = c;
            }
            if (fDark.Equals(fBase)) fDark = Color32.Lerp(fBase, new Color32(0, 0, 0, 255), 0.35f);
            if (fLight.Equals(fBase)) fLight = Color32.Lerp(fBase, new Color32(255, 255, 255, 255), 0.2f);
            var lc = new List<Color32>();
            foreach (var c in px) if (c.a > 0) { var o = new Color32(c.r, c.g, c.b, 255); if (!lc.Contains(o)) lc.Add(o); }
            lc.Sort((a, b) => L(a).CompareTo(L(b)));
            // the delivered lip palette is the Center fill's (dark / base / light): an exact 3-colour lip maps 1:1
            var map = new Dictionary<Color32, Color32>();
            for (int i = 0; i < lc.Count; i++)
            {
                float t = lc.Count > 1 ? i / (float)(lc.Count - 1) : 0.5f;
                map[lc[i]] = t < 0.34f ? fDark : t < 0.67f ? fBase : fLight;
            }
            for (int i = 0; i < px.Length; i++)
                if (px[i].a > 0) { var m = map[new Color32(px[i].r, px[i].g, px[i].b, 255)]; m.a = px[i].a; px[i] = m; }
            string path = $"{ArtDir}/dress_edge_lip_{biome}_32x32_4f.png";
            WriteIfChanged(path, w, h, px);
            Import(path, 4, 32);
            var f = IslandArtBuilder.Frames(path);
            return f.Length >= 4 ? Tiles("EdgeLip_" + biome, f) : null;
        }

        /// <summary>
        /// Grass lip along the north-facing 45° diagonals (sky NE / NW; frames 0 / 1): the biome's N lip frame (frame 2 of
        /// <c>dress_edge_lip_&lt;biome&gt;</c>) re-laid along the diagonal - lip row r lands r - 4 px inside the visible diagonal
        /// line, exactly as on a straight north edge (whose lip tilemap sits 1 px up), so the lip runs on unbroken from a
        /// north run onto the diagonal. Pixel copies, no new colours.
        /// </summary>
        static Tile[] DiagLipTiles(string biome)
        {
            var px = ReadPng($"{ArtDir}/dress_edge_lip_{biome}_32x32_4f.png", out int w, out int h);
            if (px == null || h != 32 || w < 128) return null;
            var o = new Color32[64 * 32];
            for (int f = 0; f < 2; f++)
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        int r = DiagK(f == 0 ? SkyNE : SkyNW, x, y) + 4;     // lip row (from the top of the N lip frame)
                        if (r < 0 || r > 31) continue;
                        var c = px[(31 - r) * w + 2 * 32 + x];
                        if (c.a > 0) o[y * 64 + f * 32 + x] = c;
                    }
            string path = $"{ArtDir}/dress_edge_lip_diag_{biome}_32x32_2f.png";
            WriteIfChanged(path, 64, 32, o);
            Import(path, 2, 32);
            var s = IslandArtBuilder.Frames(path);
            return s.Length >= 2 ? Tiles("EdgeLipDiag_" + biome, s) : null;
        }

        static Tile[] Tiles(string name, Sprite[] sprites)
        {
            var list = new Tile[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                string p = $"{TileDir}/{name}_{i}.asset";
                var t = AssetDatabase.LoadAssetAtPath<Tile>(p);
                if (t == null) { t = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(t, p); }
                // flags None: the per-cell colour (macro / field tints) and flips survive the tilemap refresh on scene load
                if (t.sprite != sprites[i] || t.colliderType != Tile.ColliderType.None || t.flags != TileFlags.None)
                {
                    t.sprite = sprites[i];
                    t.colliderType = Tile.ColliderType.None;
                    t.flags = TileFlags.None;
                    EditorUtility.SetDirty(t);
                }
                list[i] = t;
            }
            return list;
        }

        static AnimatedTile Animated(string name, Sprite[] frames, int startFrame, float fps)
        {
            string p = $"{TileDir}/{name}.asset";
            var t = AssetDatabase.LoadAssetAtPath<AnimatedTile>(p);
            if (t == null) { t = ScriptableObject.CreateInstance<AnimatedTile>(); AssetDatabase.CreateAsset(t, p); }
            t.m_AnimatedSprites = frames;
            t.m_MinSpeed = fps; t.m_MaxSpeed = fps;            // Tilemap.animationFrameRate = 1 -> speed = frames per second
            t.m_AnimationStartFrame = startFrame;
            t.m_TileColliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(t);
            return t;
        }

        /// <summary>Mean colour of the delivered fill (opaque pixels).</summary>
        static Color FillBase(string biome)
        {
            var px = ReadPng($"{Inc}island_{biome}_ground_fill_32x32_3f.png", out _, out _);
            if (px == null) return new Color(0.5f, 0.7f, 0.3f, 1f);
            float r = 0f, g = 0f, b = 0f; int n = 0;
            foreach (var c in px) if (c.a > 200) { r += c.r; g += c.g; b += c.b; n++; }
            return n > 0 ? new Color(r / n / 255f, g / n / 255f, b / n / 255f, 1f) : new Color(0.5f, 0.7f, 0.3f, 1f);
        }

        /// <summary>
        /// Base / light-fleck / dark-fleck colours of the delivered centre fill -> the macro light and dark tones.
        /// Flat fills (one dominant colour: center, farm, fishing, grove, celestial) get OPAQUE decals in a colour one
        /// step off the base (overlaps merge seamlessly); textured fills (mine, volcano) get a translucent tint so their
        /// plates / cracks stay visible (alpha in the returned colours).
        /// </summary>
        static void FillTones(string biome, out Color light, out Color dark, out bool textured)
        {
            var px = ReadPng($"{Inc}island_{biome}_ground_fill_32x32_3f.png", out _, out _);
            var count = new Dictionary<Color32, int>();
            int total = 0;
            if (px != null) foreach (var c in px) if (c.a > 200) { count.TryGetValue(c, out var v); count[c] = v + 1; total++; }
            var sorted = new List<KeyValuePair<Color32, int>>(count);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            Color baseC = sorted.Count > 0 ? (Color)sorted[0].Key : new Color(0.5f, 0.7f, 0.3f);
            textured = sorted.Count > 0 && sorted[0].Value < total * 0.8f;
            float L(Color c) => c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            Color lightF = baseC * 1.2f, darkF = baseC * 0.7f;
            bool hasL = false, hasD = false;
            float bl = L(baseC);
            for (int i = 1; i < Mathf.Min(6, sorted.Count); i++)
            {
                Color c = sorted[i].Key;
                if (L(c) > bl && !hasL) { lightF = c; hasL = true; }
                if (L(c) < bl && !hasD) { darkF = c; hasD = true; }
            }
            if (textured)
            {
                light = Color.Lerp(baseC, lightF, 0.8f); light.a = 0.10f;
                dark = baseC * 0.45f; dark.a = 0.25f;
                return;
            }
            // one palette step: part way toward the fleck colours (warm sunlit / cool shade); pale fills step less
            light = bl > 0.6f ? baseC * 1.035f : Color.Lerp(baseC, lightF, 0.30f);
            light = new Color(light.r * 1.02f, light.g, light.b * 0.94f, 1f);
            dark = Color.Lerp(baseC, darkF, 0.32f);
            dark = new Color(dark.r * 0.96f, dark.g, dark.b * 1.04f, 1f);
            // keep it calm: 70% of that step (full strength read as camouflage at far zoom)
            light = Color.Lerp(baseC, light, 0.7f); light.a = 1f;
            dark = Color.Lerp(baseC, dark, 0.7f); dark.a = 1f;
        }

        // decal pixel greys: the tile colour multiplies them (base = MacroBase, flecks lighter / darker)
        const float MacroBase = 0.85f;

        static Color32[] ReadPng(string path, out int w, out int h)
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

        static readonly float[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        static float Dither(int x, int y) => (Bayer4[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519u);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static float Noise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0, seed), Hash(x0 + 1, y0, seed), fx),
                Mathf.Lerp(Hash(x0, y0 + 1, seed), Hash(x0 + 1, y0 + 1, seed), fx), fy);
        }

        static float Fbm(float x, float y, int seed)
        {
            float s = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < 3; o++) { int k = 1 << o; s += amp * Noise(x * k, y * k, seed + o * 31); norm += amp; amp *= 0.5f; }
            return s / norm;
        }

        /// <summary>Irregular radius of a blob at angle a (periodic sum of a few harmonics).</summary>
        static float BlobRadius(float a, int seed)
        {
            float r = 1f;
            for (int k = 2; k <= 5; k++)
                r += (0.16f / (k - 1)) * Mathf.Sin(k * a + Hash(k, seed, 7) * 6.283f) * (0.5f + Hash(k, seed, 9));
            return r;
        }

        /// <summary>Neutral grey organic blob decals (binary alpha, Bayer-dithered rim, sparse flecks): ART-SPEC §3.6 macro stand-in.</summary>
        static Sprite[] EnsureMacro(int size, int frames, int seed)
        {
            string path = $"{ArtDir}/dress_macro_{size}x{size}_{frames}f.png";
            {
                var px = new Color32[size * frames * size];
                float R = size * 0.42f;
                for (int f = 0; f < frames; f++)
                {
                    int s = seed + f * 17;
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                            // squash a little so blobs read wider than tall (ground seen from 3/4 above)
                            float d = Mathf.Sqrt(dx * dx + dy * dy * 1.35f) / (R * BlobRadius(Mathf.Atan2(dy, dx), s));
                            d += (Noise(x / 7f, y / 7f, s) - 0.5f) * 0.16f;
                            const float band = 0.12f;
                            bool inside = d < 1f - band || (d < 1f && Dither(x, y) > (d - (1f - band)) / band);
                            var c = new Color32(0, 0, 0, 0);
                            if (inside)
                            {
                                float h = Hash(x, y, s + 5);
                                float g = MacroBase;
                                if (h < 0.025f) g = 1f;
                                else if (h < 0.05f) g = 0.68f;
                                byte v = (byte)Mathf.RoundToInt(g * 255f);
                                c = new Color32(v, v, v, 255);
                            }
                            px[y * size * frames + f * size + x] = c;
                        }
                }
                WriteIfChanged(path, size * frames, size, px);
            }
            Import(path, frames, size);
            return IslandArtBuilder.Frames(path);
        }

        const int PondW = 448, PondH = 320;

        /// <summary>Fishing pond: the delivered animated water tile, masked to an organic shore with a dark waterline,
        /// a shaded north bank (3/4 view) and a lit south lip; lotus / koi detail kept in ~1 of 4 tiles so it does not wallpaper.</summary>
        static Sprite[] EnsurePond()
        {
            string path = $"{ArtDir}/dress_pond_fishing_{PondW}x{PondH}_4f.png";
            var water = ReadPng(Inc + "island_fishing_water_32x32_4f.png", out int ww, out int wh);
            var fill = ReadPng(Inc + "island_fishing_ground_fill_32x32_3f.png", out _, out _);
            Color32 bank = fill != null ? fill[16 * 96 + 16] : new Color32(60, 110, 80, 255);
            Color32 rim = new Color32((byte)(bank.r * 0.42f), (byte)(bank.g * 0.44f), (byte)(bank.b * 0.52f), 255);
            Color32 shore = new Color32((byte)(bank.r * 0.72f), (byte)(bank.g * 0.74f), (byte)(bank.b * 0.8f), 255);
            // the plain water colour = the most common blue-dominant pixel of the delivered tile
            var counts = new Dictionary<Color32, int>();
            if (water != null) foreach (var c in water) if (c.a > 200 && c.b > c.r && c.b >= c.g) { counts.TryGetValue(c, out var v); counts[c] = v + 1; }
            Color32 plain = new Color32(40, 110, 160, 255);
            int best = -1;
            foreach (var kv in counts) if (kv.Value > best) { best = kv.Value; plain = kv.Key; }
            const int frames = 4;
            var px = new Color32[PondW * frames * PondH];
            const int seed = 4242;
            float Rad(float a) => 1f + 0.12f * Mathf.Sin(2 * a + 1.3f) + 0.07f * Mathf.Sin(3 * a + 4.1f) + 0.04f * Mathf.Sin(5 * a + 2.2f);
            float hx = (PondW * 0.5f - 8f) / 1.26f, hy = (PondH * 0.5f - 8f) / 1.26f;
            float D(int x, int y)
            {
                float dx = (x + 0.5f - PondW * 0.5f) / hx, dy = (y + 0.5f - PondH * 0.5f) / hy;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / Rad(Mathf.Atan2(dy, dx));
                return d + (Noise(x / 9f, y / 9f, seed) - 0.5f) * 0.08f;
            }
            float p1 = 1f / hy;      // ~one pixel in d units
            // VP-14: lotus / koi are stamped at seeded free positions (min 40 px apart, random flip), not kept on the
            // delivered tile's 32-px grid: the water itself is the plain tile with its detail pixels removed
            var stamps = new List<(int x, int y, bool flip)>();
            {
                var srng = new System.Random(seed + 9);
                for (int tries = 0; tries < 400 && stamps.Count < 16; tries++)
                {
                    int sx = srng.Next(8, PondW - 40), sy = srng.Next(8, PondH - 40);
                    bool ok = D(sx + 16, sy + 16) < 0.78f && D(sx, sy) < 0.95f && D(sx + 31, sy + 31) < 0.95f && D(sx, sy + 31) < 0.95f && D(sx + 31, sy) < 0.95f;
                    foreach (var q in stamps) if ((q.x - sx) * (q.x - sx) + (q.y - sy) * (q.y - sy) < 40 * 40) { ok = false; break; }
                    if (ok) stamps.Add((sx, sy, srng.NextDouble() < 0.5));
                }
            }
            bool Plain(Color32 c) => c.b > c.r && c.b >= c.g;
            // ART-SPEC §3.8 snowy Fishing: the delivered ice shelf (island_fishing_ice_edge_32x16_4f, rows bank -> water) is
            // laid along the shore curve: row = pixel distance from the shore, column = arc length along the shore, one of
            // the 4 variants per 32-px run (static: the variants tile in any order, they are not animation frames). It
            // replaces the dark waterline and the wet shore; the water just inside the shelf's north edge is shaded (3/4 view).
            var ice = ReadPng(Inc + "island_fishing_ice_edge_32x16_4f.png", out int iw, out int ih);
            if (ice != null && (ih != 16 || iw < 128)) ice = null;
            float[] dist = null, arc = null;
            int arcRuns = 1;
            if (ice != null)
            {
                // distance (px) of every water pixel from the nearest land pixel (8-neighbour chamfer, two passes)
                dist = new float[PondW * PondH];
                for (int y = 0; y < PondH; y++) for (int x = 0; x < PondW; x++) dist[y * PondW + x] = D(x, y) < 1f ? 1e6f : 0f;
                const float dg = 1.41421356f;
                for (int y = 0; y < PondH; y++)
                    for (int x = 0; x < PondW; x++)
                    {
                        int i = y * PondW + x; float v = dist[i];
                        if (v == 0f) continue;
                        if (x > 0) v = Mathf.Min(v, dist[i - 1] + 1f);
                        if (y > 0) v = Mathf.Min(v, dist[i - PondW] + 1f);
                        if (x > 0 && y > 0) v = Mathf.Min(v, dist[i - PondW - 1] + dg);
                        if (x < PondW - 1 && y > 0) v = Mathf.Min(v, dist[i - PondW + 1] + dg);
                        if (x == 0 || y == 0 || x == PondW - 1) v = Mathf.Min(v, 1f);
                        dist[i] = v;
                    }
                for (int y = PondH - 1; y >= 0; y--)
                    for (int x = PondW - 1; x >= 0; x--)
                    {
                        int i = y * PondW + x; float v = dist[i];
                        if (v == 0f) continue;
                        if (x < PondW - 1) v = Mathf.Min(v, dist[i + 1] + 1f);
                        if (y < PondH - 1) v = Mathf.Min(v, dist[i + PondW] + 1f);
                        if (x < PondW - 1 && y < PondH - 1) v = Mathf.Min(v, dist[i + PondW + 1] + dg);
                        if (x > 0 && y < PondH - 1) v = Mathf.Min(v, dist[i + PondW - 1] + dg);
                        if (y == PondH - 1) v = Mathf.Min(v, 1f);
                        dist[i] = v;
                    }
                // arc length of the shore (noise-free outline) per angle, scaled so a whole number of 32-px runs closes the loop
                const int A = 2048;
                arc = new float[A + 1];
                Vector2 Pt(float a) { float r = Rad(a); return new Vector2(Mathf.Cos(a) * r * hx, Mathf.Sin(a) * r * hy); }
                var prev = Pt(-Mathf.PI);
                for (int s = 1; s <= A; s++) { var p = Pt(-Mathf.PI + 2f * Mathf.PI * s / A); arc[s] = arc[s - 1] + Vector2.Distance(prev, p); prev = p; }
                arcRuns = Mathf.Max(1, Mathf.RoundToInt(arc[A] / 32f));
                float k = arcRuns * 32f / arc[A];
                for (int s = 0; s <= A; s++) arc[s] *= k;
            }
            Color32 IceAt(int x, int y, int row)
            {
                float dx = (x + 0.5f - PondW * 0.5f) / hx, dy = (y + 0.5f - PondH * 0.5f) / hy;
                float a = Mathf.Atan2(dy, dx);
                float u = arc[Mathf.Clamp(Mathf.RoundToInt((a + Mathf.PI) / (2f * Mathf.PI) * 2048f), 0, 2048)];
                int ui = Mathf.FloorToInt(u), run = (ui / 32) % arcRuns;
                int variant = (int)(Hash(run, 7, seed + 11) * 4f) & 3;
                return ice[(ih - 1 - row) * iw + variant * 32 + (ui & 31)];
            }
            // ART-SPEC §3.8 loose ice floes (island-decor.json "floes", 4 shapes of 32 x 32): a few stamped on the open water
            // just past the ice shelf, at seeded positions (every opaque floe pixel lies 2+ px beyond the shelf, at most
            // ~40 px from it), at least 48 px apart and clear of the koi / lotus stamps; static in all 4 water frames.
            var floeStamps = new List<(int x, int y, int v)>();
            int flw = 0, flh = 0;
            var floe = ice != null ? ReadPng(IslandDecor.IncomingFile(IslandDecor.For("fishing")?.floes, "islands") ?? "", out flw, out flh) : null;
            if (floe != null && (flh != 32 || flw < 128)) floe = null;
            if (floe != null)
            {
                var frng = new System.Random(seed + 21);
                for (int tries = 0; tries < 2000 && floeStamps.Count < 7; tries++)
                {
                    int sx = frng.Next(0, PondW - 32), sy = frng.Next(0, PondH - 32), v = frng.Next(4);
                    bool ok = true; float near = 1e6f;
                    for (int ly = 0; ly < 32 && ok; ly++)
                        for (int lx = 0; lx < 32; lx++)
                        {
                            if (floe[ly * flw + v * 32 + lx].a == 0) continue;
                            int qx = sx + lx, qy = sy + ly;
                            if (D(qx, qy) >= 1f) { ok = false; break; }
                            float t2 = dist[qy * PondW + qx] - 1f - ih;
                            if (t2 < 2f) { ok = false; break; }
                            near = Mathf.Min(near, t2);
                        }
                    if (!ok || near > 40f) continue;
                    foreach (var q in floeStamps) if ((q.x - sx) * (q.x - sx) + (q.y - sy) * (q.y - sy) < 48 * 48) { ok = false; break; }
                    foreach (var q in stamps) if ((q.x - sx) * (q.x - sx) + (q.y - sy) * (q.y - sy) < 36 * 36) { ok = false; break; }
                    if (ok) floeStamps.Add((sx, sy, v));
                }
            }
            for (int f = 0; f < frames; f++)
                for (int y = 0; y < PondH; y++)
                    for (int x = 0; x < PondW; x++)
                    {
                        float d = D(x, y);
                        Color32 c = new Color32(0, 0, 0, 0);
                        float t = ice != null ? dist[y * PondW + x] - 1f : 0f;        // 0 = the first water pixel at the shore
                        if (ice != null && d < 1f && t < ih)
                            c = IceAt(x, y, Mathf.Clamp(Mathf.FloorToInt(t), 0, ih - 1));
                        else if (ice != null && d >= 1f) { }                                  // land: no rim, no wet shore on snow
                        else if (d < 1f)
                        {
                            if (ice == null && d > 1f - 1.2f * p1) c = rim;                    // 1 px dark waterline
                            else
                            {
                                // the water motif is sampled with a different offset in each irregular (noise-warped,
                                // ~40 px) region, so its crests no longer line up on the 32-px grid
                                int wx = x + (int)((Noise(x / 23f, y / 23f, seed + 3) - 0.5f) * 30f), wy = y + (int)((Noise(x / 23f + 7.1f, y / 23f, seed + 4) - 0.5f) * 30f);
                                uint ro = (uint)(Hash(Mathf.FloorToInt(wx / 40f), Mathf.FloorToInt(wy / 40f), seed + 5) * 1024f);
                                int ox = (int)(ro & 31u), oy = (int)((ro >> 5) & 31u);
                                c = water != null ? water[((y + oy) % 32) * ww + f * 32 + ((x + ox) % 32)] : plain;
                                if (!Plain(c)) c = plain;                                          // the tile's own lotus / koi go
                                if (water != null)
                                    foreach (var st in stamps)
                                    {
                                        int lx = x - st.x, ly = y - st.y;
                                        if (lx < 0 || ly < 0 || lx > 31 || ly > 31) continue;
                                        var sc = water[ly * ww + f * 32 + (st.flip ? 31 - lx : lx)];
                                        if (sc.a > 0 && !Plain(sc)) c = sc;
                                    }
                                bool northShade = ice != null
                                    ? (y + 3 >= PondH || dist[(y + 3) * PondW + x] - 1f < ih)      // the 3 px of water under the shelf's north edge
                                    : D(x, y + 5) >= 1f;
                                if (northShade) c = new Color32((byte)(c.r * 0.7f), (byte)(c.g * 0.76f), (byte)(c.b * 0.86f), 255);   // north bank shade
                                foreach (var fs in floeStamps)
                                {
                                    int lx = x - fs.x, ly = y - fs.y;
                                    if (lx < 0 || ly < 0 || lx > 31 || ly > 31) continue;
                                    var fc = floe[ly * flw + fs.v * 32 + lx];
                                    if (fc.a > 0) c = fc;
                                }
                            }
                        }
                        else if (ice == null && d < 1f + 1.2f * p1) c = rim;
                        else if (ice == null && d < 1f + 3.2f * p1 && y < PondH / 2) c = shore;      // wet south shore
                        px[y * PondW * frames + f * PondW + x] = c;
                    }
            WriteIfChanged(path, PondW * frames, PondH, px);
            Import(path, 4, PondW);
            return IslandArtBuilder.Frames(path);
        }

        /// <summary>Generated art is deterministic: (re)write the PNG only when its pixels changed (stable git diff, no reimport churn).</summary>
        static void WriteIfChanged(string path, int w, int h, Color32[] px)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            if (File.Exists(path))
            {
                var old = ReadPng(path, out int ow, out int oh);
                bool same = old != null && ow == w && oh == h;
                if (same) for (int i = 0; i < px.Length; i++) if (!old[i].Equals(px[i]) && (old[i].a != 0 || px[i].a != 0)) { same = false; break; }
                if (same) return;
            }
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        static void Import(string path, int frames, int frameW)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Multiple;
            imp.spritePixelsPerUnit = 32;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 4096;
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            st.spriteGenerateFallbackPhysicsShape = false;
            imp.SetTextureSettings(st);
            imp.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(imp);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects();
            if (existing != null && existing.Length == frames && existing[0].rect.width == frameW) return;
            string baseName = Path.GetFileNameWithoutExtension(path);
            var rects = new SpriteRect[frames];
            for (int i = 0; i < frames; i++)
                rects[i] = new SpriteRect
                {
                    name = baseName + "_" + i,
                    rect = new Rect(i * frameW, 0, frameW, tex.height),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = GUID.Generate(),
                };
            provider.SetSpriteRects(rects);
            provider.Apply();
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ painting

        static int KeySeed(string key)
        {
            unchecked { uint h = 2166136261; foreach (char c in key) h = (h ^ c) * 16777619; return (int)(h & 0x7FFFFFFF); }
        }

        /// <summary>Island cell (row, col) -> tilemap cell (col, -row-1).</summary>
        static Vector3Int C(int row, int col) => new Vector3Int(col, -row - 1, 0);

        static Tilemap NewMap(Transform root, string name, int order, Vector3 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = offset;
            var tm = go.AddComponent<Tilemap>();
            var tr = go.AddComponent<TilemapRenderer>();
            tr.sortingLayerName = Layer;
            tr.sortingOrder = order;
            return tm;
        }

        /// <summary>
        /// ART-SPEC §3.8 ice-fishing holes (island-decor.json "iceHoles", 2 frames of 32 x 32): a few laid on the pond's ice shelf
        /// (the 16 px band just inside the shore, d = 0.93 of the pond ellipse, the same shape function as <see cref="EnsurePond"/>),
        /// at fixed angles so a rebuild never moves them; frame 1 (a fish tail) flicks rarely (<see cref="FrameFlicker"/>).
        /// Decor only: fish spawning and the nodes are not touched.
        /// </summary>
        static void BuildIceHoles(Transform root, Vector2 pondCentre)
        {
            var e = IslandDecor.For("fishing");
            string file = IslandDecor.IncomingFile(e?.iceHoles, "islands");
            if (file == null) return;
            var fr = IslandArtBuilder.Frames(file);
            if (fr.Length < 2 || fr[0] == null || fr[1] == null) return;
            float hx = (PondW * 0.5f - 8f) / 1.26f, hy = (PondH * 0.5f - 8f) / 1.26f;
            float[] deg = { -78f, -152f, 28f };
            var holes = new GameObject("IceHoles").transform;
            holes.SetParent(root, false);
            for (int i = 0; i < deg.Length; i++)
            {
                float a = deg[i] * Mathf.Deg2Rad;
                float rad = 1f + 0.12f * Mathf.Sin(2 * a + 1.3f) + 0.07f * Mathf.Sin(3 * a + 4.1f) + 0.04f * Mathf.Sin(5 * a + 2.2f);
                var off = new Vector2(Mathf.Cos(a) * hx * rad, Mathf.Sin(a) * hy * rad) * 0.93f / 32f;
                var pos = pondCentre + off;
                pos = new Vector2(Mathf.Round(pos.x * 32f) / 32f, Mathf.Round(pos.y * 32f) / 32f);
                var sr = WorldBuilder.Deco(holes, "IceHole" + i, fr[0], new Vector3(pos.x, pos.y, 0f), 1f, Color.white, 4, Layer);
                sr.flipX = i == 1;
                var ff = sr.gameObject.AddComponent<FrameFlicker>();
                ff.rest = fr[0]; ff.flicked = fr[1]; ff.period = 6.5f + i * 1.7f; ff.flick = 0.45f; ff.phase = i * 2.9f;
            }
        }

        /// <summary>ART-SPEC §3.8 snowfall (island-decor.json "snowfall" = an fx key) over the Island's landmass + margin: a 4-frame loop of
        /// 256 x 256 plays its frames; a single wrap-safe still (fx_snowfall_256x256_1f, FX-010) is scrolled down-right in code (SnowfallOverlay).</summary>
        static void BuildSnowfall(Transform root, string biome)
        {
            var e = IslandDecor.For(biome);
            string file = IslandDecor.IncomingFile(e?.snowfall, "fx");
            if (file == null) return;
            var fr = IslandArtBuilder.Frames(file);
            if (fr.Length != 1 && fr.Length < 4) return;
            var go = new GameObject("Snowfall");
            go.transform.SetParent(root, false);
            var sn = go.AddComponent<SnowfallOverlay>();
            sn.frames = fr;
            sn.area = new Rect(-24f, -Cells - 24f, Cells + 48f, Cells + 48f);
            sn.fps = 6f;
        }

        static void Place(Tilemap tm, Vector3Int p, TileBase t, bool flipX = false, bool flipY = false, Color? color = null)
        {
            tm.SetTile(p, t);
            if (color.HasValue || flipX || flipY) tm.SetTileFlags(p, TileFlags.None);
            if (flipX || flipY) tm.SetTransformMatrix(p, Matrix4x4.Scale(new Vector3(flipX ? -1 : 1, flipY ? -1 : 1, 1)));
            if (color.HasValue) tm.SetColor(p, color.Value);
        }

        /// <summary>
        /// VP-17: the Center plaza + paths as curves (see the call site). Geometry in Island cells, x = col, y = row (down).
        /// Fills <paramref name="busy"/> (no scatter on stone), <paramref name="tufts"/> (clover between stones) and
        /// <paramref name="steps"/> (loose stones past each path end).
        /// </summary>
        static void BuildCenterPath(Transform root, int seed, HashSet<Vector2Int> busy, List<Vector2Int> tufts, List<Vector2Int> steps)
        {
            var src = ReadPng($"{ArtDir}/dress_center_path_warm_32x32_15f.png", out int sw, out int sh);
            if (src == null || sh != 32) return;
            const int P = 32;
            var plazaC = new Vector2(46.5f, 46.5f);
            const float PlazaR = 4.55f;
            // spurs: quadratic Beziers from the plaza toward the work areas (end points as before), bent by a seeded side offset
            var spurs = new List<(Vector2[] pts, float[] hw)>();
            void Spur(Vector2 end, float bend, int salt)
            {
                var dir = (end - plazaC).normalized;
                var nrm = new Vector2(-dir.y, dir.x);
                var start = plazaC + dir * (PlazaR - 1f);
                var mid = (start + end) * 0.5f + nrm * bend * (0.6f + Hash(salt, 3, seed) * 0.8f);
                const int N = 48;
                var pts = new Vector2[N + 1];
                var hw = new float[N + 1];
                for (int i = 0; i <= N; i++)
                {
                    float t = i / (float)N, u = 1f - t;
                    pts[i] = u * u * start + 2f * u * t * mid + t * t * end;
                    float taper = t < 0.62f ? 1f : Mathf.Lerp(1f, 0.32f, (t - 0.62f) / 0.38f);
                    hw[i] = (1.12f + (Noise(t * 5f, salt, seed + 21) - 0.5f) * 0.22f) * taper;
                }
                spurs.Add((pts, hw));
                // three loose stones continuing the last direction
                var tdir = (pts[N] - pts[N - 2]).normalized;
                for (int j = 1; j <= 3; j++)
                {
                    var p = pts[N] + tdir * (1.7f * j) + new Vector2(-tdir.y, tdir.x) * ((Hash(j, salt, seed) - 0.5f) * 1.4f);
                    steps.Add(new Vector2Int(Mathf.FloorToInt(p.x), -Mathf.FloorToInt(p.y) - 1));
                }
            }
            Spur(new Vector2(45.5f, 29.5f), 2.2f, 1);      // north, toward the wood field - stops well short
            Spur(new Vector2(30.5f, 61.5f), -2.6f, 2);     // south-west, toward the quarry field
            Spur(new Vector2(62.5f, 61.5f), 2.4f, 3);      // south-east, toward the clay field

            // signed distance (cells, > 0 inside) of an Island point (x = col, y = row) to the stone
            float Inside(float x, float y)
            {
                var q = new Vector2(x, y);
                var d0 = q - plazaC;
                float ang = Mathf.Atan2(d0.y, d0.x);
                float best = PlazaR * (1f + 0.035f * Mathf.Sin(3f * ang + 1.1f) + 0.025f * Mathf.Sin(5f * ang + 4.2f)) - d0.magnitude;
                foreach (var (pts, hw) in spurs)
                    for (int i = 0; i < pts.Length - 1; i++)
                    {
                        var a = pts[i]; var ab = pts[i + 1] - a;
                        float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                        float r = Mathf.Lerp(hw[i], hw[i + 1], t);
                        float v = r - Vector2.Distance(q, a + ab * t);
                        if (v > best) best = v;
                    }
                return best;
            }

            var pieces = new Dictionary<ulong, CoastCurves.Piece>();
            var at = new List<(Vector3Int c, ulong h)>();
            var dark = new Color32(0x3D, 0x26, 0x16, 255);      // P20 dark wood: the south-facing shadow line only
            for (int row = 24; row <= 68; row++)
                for (int col = 24; col <= 68; col++)
                {
                    // quick reject: the cell centre is far from any stone
                    if (Inside(col + 0.5f, row + 0.5f) < -0.9f) continue;
                    Color32[] px = null;
                    int frame = (int)(Hash(col, row, seed + 61) * 3f) % 3;
                    int stoneN = 0;
                    for (int ly = 0; ly < P; ly++)          // texture rows, y up
                        for (int lx = 0; lx < P; lx++)
                        {
                            float fx = col + (lx + 0.5f) / P, fy = row + (P - 1 - ly + 0.5f) / P;
                            float s = Inside(fx, fy) * P;            // px, > 0 inside
                            if (s <= 0f) continue;
                            int gx = col * P + lx, gy = row * P + (P - 1 - ly);
                            // the pixel just south of this one is grass: a south-facing edge -> one continuous P20 line
                            bool south = s < 1.5f && Inside(fx, fy + 1f / P) <= 0f;
                            if (!south && s < 1.8f && Hash(gx, gy, seed + 71) > s / 1.8f * 0.75f + 0.15f) continue;     // frayed rim
                            var c = src[ly * sw + frame * P + lx];
                            if (c.a == 0) continue;
                            if (south) c = dark;
                            if (px == null) px = new Color32[P * P];
                            px[ly * P + lx] = c;
                            stoneN++;
                        }
                    if (px == null) continue;
                    var cell = C(row, col);
                    var v = new Vector2Int(cell.x, cell.y);
                    if (stoneN > P * P / 3) busy.Add(v);
                    if (stoneN > P * P * 3 / 4 && Hash(col, row, seed + 31) < 0.035f) tufts.Add(v);
                    ulong h = CoastCurves.ContentHash(px);
                    if (!pieces.ContainsKey(h)) pieces[h] = new CoastCurves.Piece { key = "p" + h.ToString("X16"), px = px };
                    at.Add((cell, h));
                }
            var tiles = CoastCurves.Atlas("dress_path_center", P, P, 0.5f, new List<CoastCurves.Piece>(pieces.Values));
            var pathMap = NewMap(root, "Path", 4, Vector3.zero);
            foreach (var (c, h) in at) if (tiles.TryGetValue(pieces[h].key, out var t)) pathMap.SetTile(c, t);
            foreach (var v in steps) busy.Add(v);
            foreach (var v in tufts) busy.Add(v);
        }

        public static void BuildIsland(Island isl)
        {
            var old = isl.transform.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            if (isl.coast == null || System.Array.IndexOf(Biomes, isl.islandKey) < 0) return;
            string key = isl.islandKey;
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            var cfg = db != null ? db.BuildConfig() : null;
            var def = cfg?.Region(key);
            var kit = LoadKit(key);
            int seed = KeySeed(key);
            // VP-14: hashed fill variants on the coast + play-area ground tiles; re-run the tile rules so the tilemaps'
            // stored sprites follow
            IslandArtBuilder.EnsureFillVariety(key);
            if (isl.tilemap != null) isl.tilemap.RefreshAllTiles();
            isl.coast.RefreshAllTiles();
            var rng = new System.Random(seed);

            var root = new GameObject(RootName).transform;
            root.SetParent(isl.transform, false);
            var ground = isl.transform.Find("Ground");
            if (ground != null) root.SetSiblingIndex(ground.GetSiblingIndex() + 1);

            // land = painted coast + the play square; interior = land with all 8 neighbours land (no edge tiles)
            isl.coast.CompressBounds();
            var b = isl.coast.cellBounds;
            var land = new HashSet<Vector2Int>();
            var triAt = new Dictionary<Vector2Int, int>();         // 45° diagonal half cells (land for every neighbour test)
            bool curved = false;                                   // curved coast (CoastCurves): its cells carry the edge art themselves
            foreach (var p in b.allPositionsWithin)
            {
                var tt = isl.coast.GetTile(p);
                if (tt == null) continue;
                if (tt is CoastShapeTile) { curved = true; continue; }      // partial cells: never "land" for decals / scatter
                land.Add(new Vector2Int(p.x, p.y));
                int dc = DiagCode(tt);
                if (dc > 0) triAt[new Vector2Int(p.x, p.y)] = dc;
            }
            for (int r = 0; r < Cells; r++) for (int c = 0; c < Cells; c++) land.Add(new Vector2Int(c, -r - 1));
            bool Interior(int x, int y)
            {
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) if (!land.Contains(new Vector2Int(x + dx, y + dy))) return false;
                return true;
            }
            bool InSquare(int x, int y) => x >= 0 && x < Cells && y <= -1 && y >= -Cells;
            int xMin = int.MaxValue, xMax = int.MinValue, yMin = int.MaxValue, yMax = int.MinValue;
            foreach (var v in land) { xMin = Mathf.Min(xMin, v.x); xMax = Mathf.Max(xMax, v.x); yMin = Mathf.Min(yMin, v.y); yMax = Mathf.Max(yMax, v.y); }

            var busy = new HashSet<Vector2Int>();          // cells covered by paths / plaza / patches / water (no scatter)
            var cornerRects = new List<IdleGrounds.Sim.ZoneRect>();
            if (def != null && cfg != null && key == "center")
                foreach (var z in def.noBuild) if (z == "corners") cornerRects.AddRange(cfg.ZoneRects(z));
            bool InCorner(int x, int y)
            {
                int row = -y - 1, col = x;
                foreach (var r in cornerRects) if (row >= r.r0 && row <= r.r1 && col >= r.c0 && col <= r.c1) return true;
                return false;
            }

            // ---- macro light / dark patches (big blob decals, one tile cell each)
            var macro = NewMap(root, "Macro", 1, Vector3.zero);
            var placed = new List<(Vector2 p, float r, bool lightTone)>();
            Color tintL = kit.light / MacroBase, tintD = kit.dark / MacroBase;
            tintL.a = kit.light.a; tintD.a = kit.dark.a;
            // distance (cells) from the play square's centre, normalised: 0 = build zone, ~1 = square edge, > 1 = coast margin
            float Outer(int x, int y) => Mathf.Max(Mathf.Abs(x + 0.5f - Cells * 0.5f), Mathf.Abs(y + 0.5f + Cells * 0.5f)) / (Cells * 0.5f);
            if (kit.macroArt != null)
            {
                // delivered 96x96 macro patches (3 cells, 1x), VP-14: scattered (not on a lattice), flipped X and / or Y at
                // random (no rotation: pixel rule), always >= 1 cell of plain ground between two patches, clustered by a
                // low-frequency noise and in two densities - denser around the build zone, sparse toward the coast - so the
                // meadow reads as sunlit / shaded stretches, never as wallpaper; whole footprint on interior land
                int area = (xMax - xMin + 1) * (yMax - yMin + 1);
                for (int tries = 0; tries < area / 5; tries++)
                {
                    int x = rng.Next(xMin, xMax + 1), y = rng.Next(yMin, yMax + 1);
                    float n = Fbm(x / 18f, y / 18f, seed);
                    if (InCorner(x, y)) n += 0.06f;
                    double p = n > 0.64f ? 0.55 : n > 0.57f ? 0.18 : 0.0;      // only in the noise clusters: stretches, not polka dots
                    p *= Outer(x, y) < 0.75f ? 1.0 : Outer(x, y) < 1.0f ? 0.55 : 0.25;
                    if (kit.busy) p *= 0.4;
                    if (rng.NextDouble() >= p) continue;
                    bool ok = true;
                    for (int dy = -1; dy <= 1 && ok; dy++) for (int dx = -1; dx <= 1 && ok; dx++) if (!Interior(x + dx, y + dy)) ok = false;
                    if (!ok) continue;
                    foreach (var q in placed) if ((q.p - new Vector2(x, y)).sqrMagnitude < 4.0f * 4.0f) { ok = false; break; }
                    if (!ok) continue;
                    placed.Add((new Vector2(x, y), 1.5f, true));
                    Place(macro, new Vector3Int(x, y, 0), kit.macroArt[rng.Next(kit.macroArt.Length)], rng.NextDouble() < 0.5, rng.NextDouble() < 0.5);
                }
            }

            // ---- VP-14 large-scale value layer: soft 4-7 cell blobs (the generated dithered-rim decals, overlapping into drifts), half a step lighter
            //      or darker than the ground, laid by a low-frequency noise mask so the fill has slow light / shade drifts
            //      instead of reading as one flat stamp. Equal light and dark coverage: the Island's mean brightness stays.
            //      Translucent tints keep the fill's own texture and colours underneath.
            {
                var value = NewMap(root, "Value", 0, Vector3.zero);
                var vrng = new System.Random(seed ^ 0x5A17);
                var vplaced = new List<Vector2>();
                Color baseC = kit.fillBase;
                Color lightT = new Color(Mathf.Min(1f, baseC.r * 1.35f + 0.08f), Mathf.Min(1f, baseC.g * 1.35f + 0.08f), Mathf.Min(1f, baseC.b * 1.25f + 0.06f), 1f) / MacroBase;
                Color darkT = new Color(baseC.r * 0.55f, baseC.g * 0.58f, baseC.b * 0.66f, 1f) / MacroBase;
                lightT.a = 0.07f; darkT.a = 0.12f;
                int area = (xMax - xMin + 1) * (yMax - yMin + 1);
                int nl = 0, nd = 0;
                for (int tries = 0; tries < area / 6; tries++)
                {
                    int x = vrng.Next(xMin, xMax + 1), y = vrng.Next(yMin, yMax + 1);
                    float n = Fbm(x / 13f, y / 13f, seed + 303);
                    bool light = n > 0.6f, dark = n < 0.4f;
                    if (!light && !dark) continue;
                    if (light && nl > nd + 2 || dark && nd > nl + 2) continue;      // keep the two balanced
                    bool big = vrng.NextDouble() < 0.4;
                    int ri = big ? 4 : 3;
                    bool ok = true;
                    for (int dy = -ri; dy <= ri && ok; dy++) for (int dx = -ri; dx <= ri && ok; dx++) if (!land.Contains(new Vector2Int(x + dx, y + dy))) ok = false;
                    if (!ok) continue;
                    foreach (var q in vplaced) if ((q - new Vector2(x, y)).sqrMagnitude < 3.5f * 3.5f) { ok = false; break; }
                    if (!ok) continue;
                    vplaced.Add(new Vector2(x, y));
                    if (light) nl++; else nd++;
                    var tiles = big ? kit.macroBig : kit.macroSmall;
                    Place(value, new Vector3Int(x, y, 0), tiles[vrng.Next(tiles.Length)], vrng.NextDouble() < 0.5, vrng.NextDouble() < 0.5, light ? lightT : darkT);
                }
            }
            const int step = 5;
            for (int gy = yMin; gy <= yMax && kit.macroArt == null; gy += step)
                for (int gx = xMin; gx <= xMax; gx += step)
                {
                    int x = gx + rng.Next(-2, 3), y = gy + rng.Next(-2, 3);
                    float n = Fbm(x / 24f, y / 24f, seed);
                    bool corner = InCorner(x, y);
                    if (corner) n -= 0.13f;                             // wild no-build corners: shadier grass
                    bool lightTone = !kit.pale && n > 0.565f, darkTone = n < 0.435f;     // pale fills: shade patches only (light ones read as snow)
                    if (!lightTone && !darkTone) continue;
                    if (rng.NextDouble() < (kit.busy ? 0.4 : 0.18)) continue;
                    bool big = Mathf.Abs(n - 0.5f) > 0.12f;
                    float rad = big ? 4.3f : 2.7f;
                    // whole decal footprint on interior land (never tints an edge tile or the sky)
                    bool ok = true;
                    int ri = Mathf.CeilToInt(rad);
                    for (int dy = -ri; dy <= ri && ok; dy++) for (int dx = -ri; dx <= ri && ok; dx++) if (!Interior(x + dx, y + dy)) ok = false;
                    if (!ok) continue;
                    foreach (var q in placed)
                    {
                        float min = q.lightTone == lightTone ? (q.r + rad) * (kit.textured ? 0.6f : 0.45f) : (q.r + rad) * 0.95f;
                        if ((q.p - new Vector2(x, y)).sqrMagnitude < min * min) { ok = false; break; }
                    }
                    if (!ok) continue;
                    placed.Add((new Vector2(x, y), rad, lightTone));
                    var tiles = big ? kit.macroBig : kit.macroSmall;
                    Place(macro, new Vector3Int(x, y, 0), tiles[rng.Next(tiles.Length)], rng.NextDouble() < 0.5, rng.NextDouble() < 0.5, lightTone ? tintL : tintD);
                }

            // ---- generator fields: a worn-earth base (big tinted blob decals) + the delivered patch blotches on top
            var patchA = (Tilemap)null;
            var patchB = (Tilemap)null;
            var fieldBase = (Tilemap)null;
            if (def != null && cfg != null)
                foreach (var gen in def.generators)
                {
                    var pt = gen.item != null ? PatchTiles(gen.item) : null;
                    if (pt == null) continue;
                    if (patchA == null)
                    {
                        fieldBase = NewMap(root, "FieldBase", 2, Vector3.zero);
                        patchA = NewMap(root, "PatchA", 3, Vector3.zero);
                        patchB = NewMap(root, "PatchB", 3, new Vector3(0.5f, -0.5f, 0f));
                    }
                    bool sparse = gen.item == "wood" || gen.item == "clay";      // these blotches read as clumps: keep them scattered on the earth
                    Color earth = FieldEarth(gen.item, kit) / MacroBase;
                    earth.a = 1f;
                    foreach (var r in cfg.ZoneRects(gen.zone))
                    {
                        int mr = (r.r0 + r.r1) / 2, mc = (r.c0 + r.c1) / 2;
                        Place(fieldBase, C(mr, mc), kit.macroBig[rng.Next(kit.macroBig.Length)], rng.NextDouble() < 0.5, rng.NextDouble() < 0.5, earth);
                        // VP-14: 3-5 more worn-earth decals at seeded spots inside the field (was a fixed X of 4 corners)
                        int extra = 3 + rng.Next(3);
                        for (int e = 0; e < extra; e++)
                        {
                            int er = rng.Next(r.r0, r.r1 + 1), ec = rng.Next(r.c0, r.c1 + 1);
                            Place(fieldBase, C(er, ec), kit.macroBig[rng.Next(kit.macroBig.Length)], rng.NextDouble() < 0.5, rng.NextDouble() < 0.5, earth);
                        }
                        for (int row = r.r0 - 1; row <= r.r1 + 1; row++)
                            for (int col = r.c0 - 1; col <= r.c1 + 1; col++)
                            {
                                bool inside = row >= r.r0 && row <= r.r1 && col >= r.c0 && col <= r.c1;
                                bool rim = row == r.r0 || row == r.r1 || col == r.c0 || col == r.c1;
                                double p = sparse ? (inside ? (rim ? 0.2 : 0.36) : 0.04) : inside ? (rim ? 0.4 : 0.62) : 0.1;
                                var cell = C(row, col);
                                if (rng.NextDouble() < p && land.Contains(new Vector2Int(cell.x, cell.y)))
                                    Place(patchA, cell, pt[rng.Next(pt.Length)], rng.NextDouble() < 0.5, false);
                                if (inside) busy.Add(new Vector2Int(cell.x, cell.y));
                                // offset layer fills some gaps between blotches (inner cells only)
                                if (!sparse && inside && row < r.r1 && col < r.c1 && rng.NextDouble() < 0.2)
                                    Place(patchB, cell, pt[rng.Next(pt.Length)], rng.NextDouble() < 0.5, false);
                            }
                    }
                }

            // ---- Fishing pond
            if (kit.pond != null)
            {
                var water = NewMap(root, "Water", 3, Vector3.zero);
                water.animationFrameRate = 1f;
                const int pr = 46, pc = 46;
                water.SetTile(C(pr, pc), kit.pond);
                int hw = PondW / 64, hh = PondH / 64;
                for (int row = pr - hh; row <= pr + hh; row++) for (int col = pc - hw; col <= pc + hw; col++) { var cc = C(row, col); busy.Add(new Vector2Int(cc.x, cc.y)); }
                BuildIceHoles(root, new Vector2(pc + 0.5f, -pr - 0.5f));
            }

            // ---- snowy Fishing: light snowfall over the whole Island (island-decor.json "snowfall"; hidden under Motion: calm)
            BuildSnowfall(root, key);

            // ---- Center courtyard: a round flagstone plaza under the Altar + three short paths that wind toward the work
            //      areas and fade out in a few loose stones. VP-17 (owner rule "no 90 degree angles", extended from the coasts):
            //      the stone is cut at PIXEL level from one distance field (plaza disc + three curved, tapering spurs), so
            //      every edge is a curve - no cell staircases, no right-angled notches, no second plaza edge. Interior pixels
            //      copy the warm stone fill frames; the last 1-2 px fray into the grass (seeded speckle, no outline) and only
            //      the south-facing edge gets a 1 px P20 shadow line (3/4 view). Pieces are composed per cell and
            //      de-duplicated into one atlas (CoastCurves.Atlas).
            var pathTufts = new List<Vector2Int>();      // grass growing between the stones (scatter clover on path cells)
            var stepStones = new List<Vector2Int>();     // where a path runs out: a few loose stones (scatter pebbles)
            if (key == "center" && kit.path.Length >= 15)
                BuildCenterPath(root, seed, busy, pathTufts, stepStones);

            // ---- sparse scatter (+ denser pebbles / tufts in the wild corners)
            if (kit.scatter.Length > 0)
            {
                var scatter = NewMap(root, "Scatter", 6, Vector3.zero);
                float[] weights = { 0.42f, 0.30f, 0.10f, 0.18f };
                for (int y = yMin; y <= yMax; y++)
                    for (int x = xMin; x <= xMax; x++)
                    {
                        if (!Interior(x, y) || busy.Contains(new Vector2Int(x, y))) continue;
                        bool sq = InSquare(x, y);
                        double p = InCorner(x, y) ? 0.035 : sq ? 0.018 : 0.006;
                        if (Hash(x, y, seed + 77) >= p) continue;
                        float h = Hash(x, y, seed + 78), acc = 0f;
                        int f = 0;
                        for (int i = 0; i < kit.scatter.Length && i < weights.Length; i++) { acc += weights[i]; if (h < acc) { f = i; break; } f = i; }
                        Place(scatter, new Vector3Int(x, y, 0), kit.scatter[f], Hash(x, y, seed + 79) < 0.5f, false);
                    }
                foreach (var v in pathTufts) Place(scatter, new Vector3Int(v.x, v.y, 0), kit.scatter[0], Hash(v.x, v.y, seed + 41) < 0.5f, false);
                // denser flower / tuft clusters (ART-SPEC §3.6 *_flowers), sparse, never on a scatter cell
                if (kit.flowers != null)
                {
                    var flowers = NewMap(root, "Flowers", 6, Vector3.zero);
                    for (int y = yMin; y <= yMax; y++)
                        for (int x = xMin; x <= xMax; x++)
                        {
                            if (!Interior(x, y) || busy.Contains(new Vector2Int(x, y)) || scatter.HasTile(new Vector3Int(x, y, 0))) continue;
                            float cl = Fbm(x / 9f, y / 9f, seed + 55);                 // clustered: meadows of flowers, not confetti
                            double p = (InCorner(x, y) ? 0.02 : InSquare(x, y) ? 0.007 : 0.005) * (cl > 0.58f ? 2.5 : cl < 0.45f ? 0.1 : 0.8) * (kit.busy ? 0.3 : 1.0);
                            if (Hash(x, y, seed + 66) >= p) continue;
                            Place(flowers, new Vector3Int(x, y, 0), kit.flowers[(int)(Hash(x, y, seed + 67) * kit.flowers.Length) % kit.flowers.Length], Hash(x, y, seed + 68) < 0.5f, false);
                        }
                }
                if (kit.scatter.Length > 1)
                    foreach (var v in stepStones) if (land.Contains(v)) Place(scatter, new Vector3Int(v.x, v.y, 0), kit.scatter[1], Hash(v.x, v.y, seed + 42) < 0.5f, false);
            }

            // ---- volcano lava cracks / celestial twinkle (animated, staggered start frames)
            if (kit.glow != null)
            {
                var glow = NewMap(root, "Glow", 7, Vector3.zero);
                glow.animationFrameRate = 1f;
                for (int y = -Cells; y <= -1; y++)
                    for (int x = 0; x < Cells; x++)
                    {
                        if (busy.Contains(new Vector2Int(x, y)) || Hash(x, y, seed + 91) >= 0.035f) continue;
                        glow.SetTile(new Vector3Int(x, y, 0), kit.glow[(int)(Hash(x, y, seed + 92) * 4) & 3]);
                    }
            }

            // ---- curved coast: lip + W / E side faces follow the curve (composed per cell, CoastCurves)
            if (curved) CoastCurves.BuildEdges(isl, root, CoastCurves.Map.Read(isl.coast, Cells));

            // ---- coast grass lip on the W / E / N coasts (ART-SPEC §3.6; the south coast has the 3/4 cliff wall)
            if (kit.lip != null && !curved)
            {
                // the 4 px overhang sticks OUT of the coast: each direction is its own tilemap nudged outward so the lip
                // starts at the blob tiles' visible edge (CoastInset px inside the cell) and overhangs 4 px past it
                const float o = (4f - CoastInset) / 32f;
                // with the delivered W / E side faces the face itself hangs past the coast; the W / E lips then move
                // 2 px inward instead and cover the blob tiles' brown rim (else: face, lip grass, brown line, grass)
                // (the blob's E rim sits 1 px further in than its W rim)
                var lipW = NewMap(root, "EdgeLipW", 8, new Vector3(kit.sideW != null ? 2f / 32f : -o, 0f, 0f));
                var lipE = NewMap(root, "EdgeLipE", 8, new Vector3(kit.sideW != null ? -3f / 32f : o, 0f, 0f));
                var lipN = NewMap(root, "EdgeLipN", 9, new Vector3(0f, o, 0f));
                var lipNW = NewMap(root, "EdgeLipNW", 9, new Vector3(-o, o, 0f));
                var lipNE = NewMap(root, "EdgeLipNE", 9, new Vector3(o, o, 0f));
                var diagLip = triAt.Count > 0 ? DiagLipTiles(key) : null;
                var lipD = diagLip != null ? NewMap(root, "EdgeLipDiag", 9, Vector3.zero) : null;
                foreach (var v in land)
                {
                    if (triAt.TryGetValue(v, out var dcode))
                    {
                        // north-facing diagonals carry the lip; south-facing ones meet the 45° wall's grass top
                        if (lipD != null && (dcode == SkyNE || dcode == SkyNW)) Place(lipD, new Vector3Int(v.x, v.y, 0), diagLip[dcode == SkyNE ? 0 : 1]);
                        continue;
                    }
                    bool n = !land.Contains(v + Vector2Int.up), w = !land.Contains(v + Vector2Int.left), e = !land.Contains(v + Vector2Int.right);
                    var cell = new Vector3Int(v.x, v.y, 0);
                    if (n && w) Place(lipNW, cell, kit.lip[3]);
                    else if (n && e) Place(lipNE, cell, kit.lip[3], true);
                    else if (n) Place(lipN, cell, kit.lip[2]);
                    if (w && !n) Place(lipW, cell, kit.lip[0]);
                    if (e && !n) Place(lipE, cell, kit.lip[1]);
                }
            }

            // ---- 3/4 side faces on the W / E coasts (ART-SPEC §3.6 *_sidewall): an earth / rock band hanging off the coast
            //      into the sky cell beside it (under the edge lip, which overhangs its grass column). Sorted above the south
            //      cliff wall (Ground -3): a side face belongs to land further south, in front of a recessed wall. The cell
            //      that also faces south (the wall starts under it) gets the tapering end frame. Sky-layer shadow copies keep
            //      the drop shadow matching the silhouette; the veil mask already covers one cell around the land.
            if (kit.sideW != null && !curved)
            {
                // the face is drawn in the sky cell beside the coast; the tilemap is nudged toward the island so the face's
                // grass column lands 1 px outside the blob tiles' visible edge (under the lip), the rock hanging past it
                float nudge = (32 - SideGrassCol + CoastInset - 1) / 32f;
                var sideW = NewMap(root, "SideWallW", 7, new Vector3(nudge, 0f, 0f));
                var sideE = NewMap(root, "SideWallE", 7, new Vector3(-nudge, 0f, 0f));
                sideE.color = SideEastShade;      // light from the top-left: the east faces sit in shade
                // straight variants picked per cell (seeded), never the same one twice in a row down a coast column
                var pickW = new Dictionary<Vector2Int, int>();
                var pickE = new Dictionary<Vector2Int, int>();
                int Variant(Dictionary<Vector2Int, int> memo, Vector2Int v, bool west, int n)
                {
                    if (memo.TryGetValue(v, out var got)) return got;
                    int r;
                    var up = v + Vector2Int.up;
                    bool upFace = land.Contains(up) && !land.Contains(up + (west ? Vector2Int.left : Vector2Int.right));
                    if (n <= 1) r = 0;
                    else if (upFace) r = (Variant(memo, up, west, n) + 1 + (Hash(v.x, v.y, seed + (west ? 211 : 212)) < 0.5f ? 0 : n - 2)) % n;
                    else r = (int)(Hash(v.x, v.y, seed + (west ? 213 : 214)) * n) % n;
                    memo[v] = r;
                    return r;
                }
                var sorted = new List<Vector2Int>(land);
                sorted.Sort((a, b) => b.y != a.y ? b.y.CompareTo(a.y) : a.x.CompareTo(b.x));     // north first: memo depth stays 1
                // a sky cell the 3-cell south wall of land above it hangs across: a face drawn there would stand in front
                // of that recessed wall like a post (a concave step). The land's grass edge meets the wall directly instead
                // (the wall's tucked end reaches under it, IslandCoastBuilder.BuildCliff).
                bool OverWall(int fx, int fy)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        if (land.Contains(new Vector2Int(fx, fy + k + 1))) return true;
                        if (land.Contains(new Vector2Int(fx, fy + k))) return false;
                    }
                    return false;
                }
                // ... but only when the wall spans the whole face run (a step of 1-3 rows). A deeper step shows sky beside
                // the run's lower part, so the run keeps its face from top to foot (in front of the recessed wall) rather
                // than a band starting in mid-air under the wall's ragged bottom.
                bool SkipRun(Vector2Int v, int dx)
                {
                    if (!OverWall(v.x + dx, v.y)) return false;
                    var p = v;
                    while (land.Contains(p + Vector2Int.down) && !land.Contains(p + Vector2Int.down + new Vector2Int(dx, 0))) p += Vector2Int.down;
                    return OverWall(p.x + dx, p.y);
                }
                // inner (concave) corner: the face's sky cell has land below it. That land's grass starts CoastInset px under
                // the cell edge, so a copy of the face reaching CoastInset px further down (under that land's N lip) closes
                // the sky notch at the corner
                var footW = NewMap(root, "SideWallWFoot", 7, new Vector3(nudge, -CoastInset / 32f, 0f));
                var footE = NewMap(root, "SideWallEFoot", 7, new Vector3(-nudge, -CoastInset / 32f, 0f));
                footE.color = SideEastShade;
                foreach (var v in sorted)
                {
                    if (triAt.ContainsKey(v)) continue;          // diagonal cells never carry a W / E face (their legs are land)
                    bool w = !land.Contains(v + Vector2Int.left), e = !land.Contains(v + Vector2Int.right), s = !land.Contains(v + Vector2Int.down);
                    // the foot frame also where a 45° south wall starts under the face (a diagonal falling away from it)
                    triAt.TryGetValue(v + Vector2Int.down, out int below);
                    bool sW = s || below == SkySW, sE = s || below == SkySE;
                    if (w && !SkipRun(v, -1))
                    {
                        int k = Variant(pickW, v, true, kit.sideW.Length);
                        var p = new Vector3Int(v.x - 1, v.y, 0);
                        sideW.SetTile(p, sW ? kit.sideWEnd[k] : kit.sideW[k]);
                        if (!s && land.Contains(new Vector2Int(v.x - 1, v.y - 1))) footW.SetTile(p, kit.sideW[k]);
                    }
                    if (e && !SkipRun(v, 1))
                    {
                        int k = Variant(pickE, v, false, kit.sideE.Length);
                        var p = new Vector3Int(v.x + 1, v.y, 0);
                        sideE.SetTile(p, sE ? kit.sideEEnd[k] : kit.sideE[k]);
                        if (!s && land.Contains(new Vector2Int(v.x + 1, v.y - 1))) footE.SetTile(p, kit.sideE[k]);
                    }
                }
                var shadow = isl.transform.Find("Shadow");
                if (shadow != null)
                {
                    var oldS = shadow.Find("Side");
                    if (oldS != null) Object.DestroyImmediate(oldS.gameObject);
                    var sRoot = new GameObject("Side").transform;
                    sRoot.SetParent(shadow, false);
                    foreach (var src in new[] { sideW, sideE })
                    {
                        var go = new GameObject(src.name);
                        go.transform.SetParent(sRoot, false);
                        go.transform.localPosition = src.transform.localPosition;
                        var tm = go.AddComponent<Tilemap>();
                        var tr = go.AddComponent<TilemapRenderer>();
                        tr.sortingLayerName = WorldBuilder.SkyLayer;
                        tr.sortingOrder = 20;
                        tm.color = new Color(0f, 0f, 0f, 0.35f);
                        src.CompressBounds();
                        foreach (var p in src.cellBounds.allPositionsWithin) { var t = src.GetTile(p); if (t != null) tm.SetTile(p, t); }
                        tm.CompressBounds();
                    }
                }
            }

            foreach (var tm in root.GetComponentsInChildren<Tilemap>()) tm.CompressBounds();
            EditorUtility.SetDirty(isl.gameObject);
        }
    }
}
