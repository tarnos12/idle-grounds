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
            public Tile sideWEnd, sideEEnd;            // ... and the end pieces above the south cliff wall
            public AnimatedTile pond;
            public AnimatedTile[] glow;
            public Color light, dark;
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
            // fishing v3 macro (Islands-033): its patches are already the fill colour with sparse pebbles, so it is used
            // as delivered (softening would only erase the pebbles); the other biomes are softened toward their fill
            var mf = EnsureSoftened($"{Inc}island_{biome}_macro_96x96_4f.png", $"dress_macro_{biome}_96x96_4f.png", 4, 96, biome == "fishing" ? 1f : MacroStrength, $"{Inc}island_{biome}_ground_fill_32x32_3f.png");
            k.macroArt = mf.Length >= 4 ? Tiles("MacroArt_" + biome, mf) : null;
            var ff = IslandArtBuilder.Frames($"{Inc}island_{biome}_flowers_32x32_4f.png");
            k.flowers = ff.Length > 0 ? Tiles("Flowers_" + biome, ff) : null;
            k.lip = EdgeLipTiles(biome);
            var sw = EnsureSideWall(biome);
            if (sw != null)
            {
                var all = new List<Sprite>(sw.w); all.AddRange(sw.e); all.Add(sw.wEnd); all.Add(sw.eEnd);
                var t = Tiles("SideWall_" + biome, all.ToArray());
                int nw = sw.w.Length, ne = sw.e.Length;
                k.sideW = new Tile[nw]; k.sideE = new Tile[ne];
                System.Array.Copy(t, 0, k.sideW, 0, nw);
                System.Array.Copy(t, nw, k.sideE, 0, ne);
                k.sideWEnd = t[nw + ne]; k.sideEEnd = t[nw + ne + 1];
            }
            FillTones(biome, out k.light, out k.dark, out k.textured);
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
            string path = $"{ArtDir}/dress_cliffwall_{biome}_32x96_8f.png";
            WriteIfChanged(path, w, h, px);
            Import(path, 8, 32);
            return IslandArtBuilder.Frames(path);
        }

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
            var v = IslandArtBuilder.Frames($"{Inc}island_{biome}_sidewall_variants_32x32_8f.png");
            if (v.Length >= 8)
                return new SideSet { w = new[] { v[0], v[1], v[2] }, e = new[] { v[3], v[4], v[5] }, wEnd = v[6], eEnd = v[7] };
            var f = IslandArtBuilder.Frames($"{Inc}island_{biome}_sidewall_32x32_4f.png");
            if (f.Length >= 4)
                return new SideSet { w = new[] { f[0] }, e = new[] { f[1] }, wEnd = f[2], eEnd = f[3] };
            return null;
        }

        public class SideSet { public Sprite[] w, e; public Sprite wEnd, eEnd; }

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
        /// </summary>
        static Tile[] EdgeLipTiles(string biome)
        {
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
            for (int f = 0; f < frames; f++)
                for (int y = 0; y < PondH; y++)
                    for (int x = 0; x < PondW; x++)
                    {
                        float d = D(x, y);
                        Color32 c = new Color32(0, 0, 0, 0);
                        if (d < 1f)
                        {
                            if (d > 1f - 1.2f * p1) c = rim;                                       // 1 px dark waterline
                            else
                            {
                                c = water != null ? water[(y % 32) * ww + f * 32 + (x % 32)] : plain;
                                bool decor = Hash(x / 32, y / 32, seed) < 0.28f;
                                if (!decor && !(c.b > c.r && c.b >= c.g)) c = plain;              // drop lotus / koi in most tiles
                                if (D(x, y + 5) >= 1f) c = new Color32((byte)(c.r * 0.7f), (byte)(c.g * 0.76f), (byte)(c.b * 0.86f), 255);   // north bank shade
                            }
                        }
                        else if (d < 1f + 1.2f * p1) c = rim;
                        else if (d < 1f + 3.2f * p1 && y < PondH / 2) c = shore;                    // wet south shore
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

        static void Place(Tilemap tm, Vector3Int p, TileBase t, bool flipX = false, bool flipY = false, Color? color = null)
        {
            tm.SetTile(p, t);
            if (color.HasValue || flipX || flipY) tm.SetTileFlags(p, TileFlags.None);
            if (flipX || flipY) tm.SetTransformMatrix(p, Matrix4x4.Scale(new Vector3(flipX ? -1 : 1, flipY ? -1 : 1, 1)));
            if (color.HasValue) tm.SetColor(p, color.Value);
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
            var rng = new System.Random(seed);

            var root = new GameObject(RootName).transform;
            root.SetParent(isl.transform, false);
            var ground = isl.transform.Find("Ground");
            if (ground != null) root.SetSiblingIndex(ground.GetSiblingIndex() + 1);

            // land = painted coast + the play square; interior = land with all 8 neighbours land (no edge tiles)
            isl.coast.CompressBounds();
            var b = isl.coast.cellBounds;
            var land = new HashSet<Vector2Int>();
            foreach (var p in b.allPositionsWithin) if (isl.coast.HasTile(p)) land.Add(new Vector2Int(p.x, p.y));
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
            if (kit.macroArt != null)
            {
                // delivered 96x96 macro patches (3 cells, 1x): clustered by low-frequency noise so the meadow reads as
                // sunlit / shaded stretches, never an even wallpaper; whole footprint on interior land
                const int mstep = 3;
                for (int gy = yMin; gy <= yMax; gy += mstep)
                    for (int gx = xMin; gx <= xMax; gx += mstep)
                    {
                        int x = gx + rng.Next(-1, 2), y = gy + rng.Next(-1, 2);
                        float n = Fbm(x / 20f, y / 20f, seed);
                        if (InCorner(x, y)) n += 0.08f;
                        double p = n > 0.6f ? 0.9 : n > 0.55f ? 0.35 : 0.0;
                        if (kit.busy) p *= 0.35;
                        if (rng.NextDouble() >= p) continue;
                        bool ok = true;
                        for (int dy = -1; dy <= 1 && ok; dy++) for (int dx = -1; dx <= 1 && ok; dx++) if (!Interior(x + dx, y + dy)) ok = false;
                        if (!ok) continue;
                        foreach (var q in placed) if ((q.p - new Vector2(x, y)).sqrMagnitude < 2.2f * 2.2f) { ok = false; break; }
                        if (!ok) continue;
                        placed.Add((new Vector2(x, y), 1.5f, true));
                        Place(macro, new Vector3Int(x, y, 0), kit.macroArt[rng.Next(kit.macroArt.Length)], rng.NextDouble() < 0.5, false);
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
                        foreach (var (dr, dc) in new[] { (-2, -2), (-2, 2), (2, -2), (2, 2) })
                            Place(fieldBase, C(mr + dr, mc + dc), kit.macroBig[rng.Next(kit.macroBig.Length)], rng.NextDouble() < 0.5, rng.NextDouble() < 0.5, earth);
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
            }

            // ---- Center courtyard: a small round flagstone plaza around the Altar + short paths that fade out toward the
            //      work areas (weathered warm stone sitting IN the grass; carved glyph slabs only at the 4 corners)
            var pathTufts = new List<Vector2Int>();      // grass growing between the stones (scatter clover on path cells)
            var stepStones = new List<Vector2Int>();     // where a path runs out: a few loose stones (scatter pebbles)
            if (key == "center" && kit.path.Length >= 15)
            {
                var path = new HashSet<Vector2Int>();
                Vector2Int K(int row, int col) { var cc = C(row, col); return new Vector2Int(cc.x, cc.y); }
                                // Altar = 5x5 at rows/cols 44-48: a disc of ~9 cells across, its rim softened by the path edge frames
                const float cr0 = 46f, cc0 = 46f;
                for (int row = 40; row <= 52; row++)
                    for (int col = 40; col <= 52; col++)
                    {
                        float d = Mathf.Sqrt((row - cr0) * (row - cr0) + (col - cc0) * (col - cc0));
                        if (d <= 4.0f || (d <= 4.8f && Hash(row, col, seed + 5) < 0.35f)) path.Add(K(row, col));
                    }
                // carved slabs (gold sun glyph) just off the Altar's 4 corners: make sure stone surrounds them
                var glyphs = new[] { (43, 44), (43, 48), (49, 44), (49, 48) };
                foreach (var (gr, gc) in glyphs) foreach (var (dr, dc) in new[] { (0, 0), (-1, 0), (1, 0), (0, -1), (0, 1) }) path.Add(K(gr + dr, gc + dc));
                // short 2-wide spurs that wind toward a work area in orthogonal runs of 2-4 cells (no ruler-straight
                // diagonals or long roads), thin out over the last cells and end in a few loose stones
                void Spur(int r0, int c0, int r1, int c1, int fade, int salt)
                {
                    var cells = new List<(int r, int c)>();
                    int row = r0, col = c0, step = 0;
                    bool vertical = Mathf.Abs(r1 - r0) >= Mathf.Abs(c1 - c0);
                    while ((row != r1 || col != c1) && cells.Count < 200)
                    {
                        int dr = r1 - row, dc = c1 - col;
                        if (dr == 0) vertical = false; else if (dc == 0) vertical = true;
                        int run = 2 + (int)(Hash(step, salt, seed + 13) * 3f);
                        for (int k = 0; k < run && (vertical ? row != r1 : col != c1); k++)
                        {
                            if (vertical) row += System.Math.Sign(dr); else col += System.Math.Sign(dc);
                            cells.Add((row, col));
                        }
                        // switch axis, weighted by how far is left on each
                        float wr = Mathf.Abs(r1 - row), wc = Mathf.Abs(c1 - col);
                        vertical = wr + wc > 0 && Hash(step, salt, seed + 17) * (wr + wc) < wr;
                        step++;
                    }
                    int n = cells.Count;
                    for (int i = 0; i < n; i++)
                    {
                        var (r, c) = cells[i];
                        int left = n - 1 - i;
                        if (left < fade && Hash(r, c, seed + 11 + salt) > (float)left / fade + 0.2f) continue;     // thinning out
                        for (int a = 0; a < 2; a++) for (int b = 0; b < 2; b++) path.Add(K(r + a, c + b));
                    }
                    // beyond the end: three loose stones continuing the last direction
                    if (n >= 2)
                    {
                        int sr = System.Math.Sign(cells[n - 1].r - cells[n - 2].r), sc = System.Math.Sign(cells[n - 1].c - cells[n - 2].c);
                        for (int j = 1; j <= 3; j++)
                            stepStones.Add(K(cells[n - 1].r + sr * 2 * j + (Hash(j, salt, seed) < 0.5f ? 0 : 1), cells[n - 1].c + sc * 2 * j + (Hash(j, salt, seed + 1) < 0.5f ? 0 : 1)));
                    }
                }
                Spur(42, 45, 29, 45, 5, 1);        // north, toward the wood field (rows 12-20) - stops well short
                Spur(49, 42, 61, 30, 5, 2);        // south-west, toward the quarry field (rows 76-84, cols 8-16)
                Spur(49, 49, 61, 61, 5, 3);        // south-east, toward the clay field (rows 76-84, cols 76-84)
                // drop any cell left 1 wide (the tileset has no 1-wide frames)
                bool Has(Vector2Int v) => path.Contains(v);
                for (bool changed = true; changed;)
                {
                    changed = false;
                    foreach (var v in new List<Vector2Int>(path))
                        if ((!Has(v + Vector2Int.up) && !Has(v + Vector2Int.down)) || (!Has(v + Vector2Int.left) && !Has(v + Vector2Int.right)))
                        { path.Remove(v); changed = true; }
                }
                var pathMap = NewMap(root, "Path", 4, Vector3.zero);
                bool P(int x, int y) => path.Contains(new Vector2Int(x, y));
                foreach (var v in path)
                {
                    int x = v.x, y = v.y;
                    bool n = P(x, y + 1), e = P(x + 1, y), s = P(x, y - 1), w = P(x - 1, y);
                    int f;
                    if (!n && !w) f = 7; else if (!n && !e) f = 8; else if (!s && !e) f = 9; else if (!s && !w) f = 10;
                    else if (!n) f = 3; else if (!e) f = 4; else if (!s) f = 5; else if (!w) f = 6;
                    else if (!P(x - 1, y + 1)) f = 11; else if (!P(x + 1, y + 1)) f = 12; else if (!P(x + 1, y - 1)) f = 13; else if (!P(x - 1, y - 1)) f = 14;
                    else f = rng.Next(3);
                    if (Hash(x, y, seed + 31) < (f < 3 ? 0.015f : 0.04f)) pathTufts.Add(v);      // grass between / creeping over the stones
                    pathMap.SetTile(new Vector3Int(x, y, 0), kit.path[f]);
                    busy.Add(v);
                }
                // the 4 carved slabs (no grout grid / glyph anywhere else)
                if (kit.plaza.Length >= 3)
                {
                    var plaza = NewMap(root, "Plaza", 5, Vector3.zero);
                    foreach (var (gr, gc) in glyphs) if (path.Contains(K(gr, gc))) plaza.SetTile(C(gr, gc), kit.plaza[2]);
                }
                foreach (var v in stepStones) busy.Add(v);
                foreach (var v in pathTufts) busy.Add(v);
            }

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

            // ---- coast grass lip on the W / E / N coasts (ART-SPEC §3.6; the south coast has the 3/4 cliff wall)
            if (kit.lip != null)
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
                foreach (var v in land)
                {
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
            if (kit.sideW != null)
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
                foreach (var v in sorted)
                {
                    bool w = !land.Contains(v + Vector2Int.left), e = !land.Contains(v + Vector2Int.right), s = !land.Contains(v + Vector2Int.down);
                    if (w) sideW.SetTile(new Vector3Int(v.x - 1, v.y, 0), s ? kit.sideWEnd : kit.sideW[Variant(pickW, v, true, kit.sideW.Length)]);
                    if (e) sideE.SetTile(new Vector3Int(v.x + 1, v.y, 0), s ? kit.sideEEnd : kit.sideE[Variant(pickE, v, false, kit.sideE.Length)]);
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
