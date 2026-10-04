using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Placeholder art for the floating Islands, the sky and Spirit Bridges (ADR 0003), generated in code
    /// under the exact ART-SPEC §3 file names and layouts so the real art drops in by REPLACING the PNG
    /// (same size / frame order): ground + cliff strips become Rule Tiles, the sky gradient + 3 cloud
    /// strips + peaks feed the <see cref="IdleGrounds.Game.ParallaxLayer"/>s, plus underside rocks, base
    /// mist, the veil fog, unlock stele frames, the qi trail and the three bridge states.
    /// <see cref="EnsureArt"/> only writes MISSING files (real art is never overwritten);
    /// "Regenerate Placeholder Art" overwrites everything. All sprites: 32 px per unit (1 cell).
    /// </summary>
    public static class IslandArtBuilder
    {
        public const string IslandDir = "Assets/_Project/Art/Islands";
        public const string SkyDir = "Assets/_Project/Art/Sky";
        public const string BridgeDir = "Assets/_Project/Art/Bridges";
        public const string TileDir = IslandDir + "/Tiles";
        const int Ppu = 32;

        /// <summary>Island keys (= ART-SPEC biome names) and their placeholder ground colours.</summary>
        public static readonly (string key, string ground, string rock)[] Biomes =
        {
            ("center", "#2f4a27", "#5b4a3a"),
            ("farm", "#4a3f1f", "#6e5636"),
            ("mine", "#37373f", "#4b4b55"),
            ("fishing", "#1d3e48", "#3c5560"),
            ("volcano", "#45211b", "#2a1d22"),
            ("grove", "#234634", "#3a3a2c"),
            ("celestial", "#2c2550", "#b9b3d6"),
        };

        // ------------------------------------------------------------------ public entry points

        [MenuItem("Idle Grounds/Art/Regenerate Placeholder Island Art")]
        public static void RegenerateAll() { Generate(true); BuildRuleTiles(); AssetDatabase.SaveAssets(); }

        /// <summary>Writes only missing placeholder files, (re)slices, and (re)builds the Rule Tiles.</summary>
        public static void EnsureArt() { Generate(false); BuildRuleTiles(); AssetDatabase.SaveAssets(); }

        public static string GroundPath(string key) => $"{IslandDir}/island_{key}_ground_32x32_19f.png";
        public static string CliffPath(string key) => $"{IslandDir}/island_{key}_cliff_32x32_8f.png";
        /// <summary>ART-SPEC 3.0: 47-frame blob coastline sheet per biome (1504x32 strip).</summary>
        public static string BlobPath(string key) => $"{IslandDir}/island_{key}_ground_blob_32x32_47f.png";
        /// <summary>Playable-area fill Rule Tile (random centre variants only; the coast blob tile draws the edges).</summary>
        public static string GroundTilePath(string key) => $"{TileDir}/RuleTile_{key}_ground.asset";
        /// <summary>47-blob Rule Tile painted on each Island's Coast tilemap.</summary>
        public static string CoastTilePath(string key) => $"{TileDir}/RuleTile_{key}_coast.asset";
        public const string BoundaryProps = IslandDir + "/island_boundary_props_32x32_6f.png";     // placeholder-only (no spec row)
        public const string MaskDir = IslandDir + "/Masks";
        public static string CliffTilePath(string key) => $"{TileDir}/RuleTile_{key}_cliff.asset";

        public const string UndersideRock = IslandDir + "/island_underside_rock_256x192.png";
        public const string UndersideRoots = IslandDir + "/island_underside_roots_192x160.png";
        public const string UndersideStalactite = IslandDir + "/island_underside_stalactite_96x160.png";
        public const string BaseMist = IslandDir + "/island_base_mist_128x64.png";          // placeholder-only (no spec row)
        public const string VeilMist = IslandDir + "/island_veil_mist_256x256_4f.png";
        public const string SteleLocked = IslandDir + "/island_unlock_stele_locked.png";
        public const string StelePartial = IslandDir + "/island_unlock_stele_partial.png";
        public const string SteleReady = IslandDir + "/island_unlock_stele_ready_64x96_4f.png";
        public const string SkyGradient = SkyDir + "/sky_gradient_512x1024.png";
        public const string CloudsFar = SkyDir + "/sky_clouds_far_1024x256.png";
        public const string CloudsMid = SkyDir + "/sky_clouds_mid_1024x256.png";
        public const string CloudsNear = SkyDir + "/sky_clouds_near_1024x256.png";
        public const string Peaks = SkyDir + "/sky_peaks_1024x256.png";
        public const string QiTrail = BridgeDir + "/fx_qi_trail_64x16_4f.png";
        public const string BridgeUnpaired = BridgeDir + "/bld_spirit_bridge_unpaired.png";
        public const string BridgeSending = BridgeDir + "/bld_spirit_bridge_sending_64x64_4f.png";
        public const string BridgeReceiving = BridgeDir + "/bld_spirit_bridge_receiving_64x64_4f.png";

        /// <summary>Camera clear colour = the sky gradient's base (deep upper sky).</summary>
        public static readonly Color SkyBase = Hex("#16263f");

        public static Sprite Single(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        /// <summary>Frames of a sliced strip, in frame order (name suffix _0, _1, …).</summary>
        public static Sprite[] Frames(string path)
        {
            var list = new List<Sprite>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Sprite s) list.Add(s);
            list.Sort((a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));
            return list.ToArray();
        }

        static int FrameIndex(string n)
        {
            int i = n.LastIndexOf('_');
            return i >= 0 && int.TryParse(n.Substring(i + 1), out var k) ? k : 0;
        }

        // ------------------------------------------------------------------ generation

        enum Pivot { Center, TopCenter, BottomCenter }

        static void Generate(bool force)
        {
            Directory.CreateDirectory(IslandDir);
            Directory.CreateDirectory(SkyDir);
            Directory.CreateDirectory(BridgeDir);
            Directory.CreateDirectory(TileDir);
            Directory.CreateDirectory(MaskDir);
            foreach (var (key, ground, rock) in Biomes)
            {
                Make(GroundPath(key), force, () => GroundStrip(Hex(ground), key.GetHashCode()), 19, 32, Pivot.Center, true, false);
                Make(BlobPath(key), force, () => BlobStrip(Hex(ground), key.GetHashCode()), 47, 32, Pivot.Center, true, false);
                Make(CliffPath(key), force, () => CliffStrip(Hex(ground), Hex(rock), key.GetHashCode()), 8, 32, Pivot.Center, true, false);
            }
            Make(BoundaryProps, force, BoundaryPropsTex, 6, 32, Pivot.BottomCenter, true, false);
            Make(UndersideRock, force, () => Underside(256, 192, 1, 0.85f), 1, 256, Pivot.TopCenter, true, false);
            Make(UndersideRoots, force, Roots, 1, 192, Pivot.TopCenter, true, false);
            Make(UndersideStalactite, force, () => Underside(96, 160, 7, 0.55f), 1, 96, Pivot.TopCenter, true, false);
            Make(BaseMist, force, () => Puff(128, 64, new Color(0.93f, 0.95f, 1f), 0.75f, 3), 1, 128, Pivot.Center, false, false);
            Make(VeilMist, force, Veil, 4, 256, Pivot.Center, false, true);
            Make(SteleLocked, force, () => Stele(0, 0f), 1, 64, Pivot.BottomCenter, true, false);
            Make(StelePartial, force, () => Stele(1, 0.5f), 1, 64, Pivot.BottomCenter, true, false);
            Make(SteleReady, force, SteleReadyStrip, 4, 64, Pivot.BottomCenter, true, false);
            Make(SkyGradient, force, Gradient, 1, 512, Pivot.Center, false, false);
            Make(CloudsFar, force, () => Clouds(11, 26, 0.55f, 0.35f, new Color(0.80f, 0.87f, 0.93f), new Color(0.62f, 0.70f, 0.80f), 0.78f), 1, 1024, Pivot.Center, false, true);
            Make(CloudsMid, force, () => Clouds(23, 16, 0.85f, 0.45f, new Color(0.92f, 0.94f, 0.98f), new Color(0.66f, 0.62f, 0.76f), 0.70f), 1, 1024, Pivot.Center, false, true);
            Make(CloudsNear, force, () => Clouds(37, 7, 1.25f, 0.5f, new Color(1f, 1f, 1f), new Color(0.82f, 0.84f, 0.92f), 0.62f), 1, 1024, Pivot.Center, false, true);
            Make(Peaks, force, PeaksTex, 1, 1024, Pivot.Center, false, true);
            Make(QiTrail, force, Trail, 4, 64, Pivot.Center, false, true);
            Make(BridgeUnpaired, force, () => Bridge(0, 0), 1, 64, Pivot.Center, true, false);
            Make(BridgeSending, force, () => BridgeStrip(1), 4, 64, Pivot.Center, true, false);
            Make(BridgeReceiving, force, () => BridgeStrip(2), 4, 64, Pivot.Center, true, false);
        }

        /// <summary>Write the PNG when missing (or forced), then make sure the importer + slicing match.</summary>
        static void Make(string path, bool force, System.Func<Texture2D> draw, int frames, int frameW, Pivot pivot, bool point, bool tiled)
        {
            if (force || !File.Exists(path))
            {
                var tex = draw();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            Import(path, frames, frameW, pivot, point, tiled);
        }

        static Vector2 PivotOf(Pivot p) => p == Pivot.TopCenter ? new Vector2(0.5f, 1f) : p == Pivot.BottomCenter ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);

        static void Import(string path, int frames, int frameW, Pivot pivot, bool point, bool tiled)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) return;
            bool multi = frames > 1;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = multi ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            imp.spritePixelsPerUnit = Ppu;
            imp.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;      // Tiled draw mode + clean quads
            st.spriteGenerateFallbackPhysicsShape = false;
            if (!multi)
            {
                st.spriteAlignment = (int)(pivot == Pivot.TopCenter ? SpriteAlignment.TopCenter : pivot == Pivot.BottomCenter ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
                st.spritePivot = PivotOf(pivot);
            }
            imp.SetTextureSettings(st);
            imp.SaveAndReimport();
            if (!multi) return;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int h = tex.height;
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(imp);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects();
            if (existing != null && existing.Length == frames && existing[0].rect.width == frameW) return;   // already sliced (real art keeps its slicing)
            string baseName = Path.GetFileNameWithoutExtension(path);
            var rects = new SpriteRect[frames];
            for (int i = 0; i < frames; i++)
                rects[i] = new SpriteRect
                {
                    name = baseName + "_" + i,
                    rect = new Rect(i * frameW, 0, frameW, h),
                    alignment = pivot == Pivot.TopCenter ? SpriteAlignment.TopCenter : pivot == Pivot.BottomCenter ? SpriteAlignment.BottomCenter : SpriteAlignment.Center,
                    pivot = PivotOf(pivot),
                    spriteID = GUID.Generate(),
                };
            provider.SetSpriteRects(rects);
            provider.Apply();
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ Rule Tiles (ART-SPEC 3.1 / 3.2 frame order)

        const int T = RuleTile.TilingRuleOutput.Neighbor.This, N = RuleTile.TilingRuleOutput.Neighbor.NotThis, O = 0;

        /// <summary>Neighbour order: NW, N, NE, W, E, SW, S, SE.</summary>
        static RuleTile.TilingRule Rule(Sprite[] sprites, params int[] n)
        {
            var r = new RuleTile.TilingRule { m_Sprites = sprites, m_ColliderType = Tile.ColliderType.None };
            r.m_Neighbors = new List<int>(n);
            if (sprites.Length > 1) { r.m_Output = RuleTile.TilingRuleOutput.OutputSprite.Random; r.m_PerlinScale = 0.5f; }
            return r;
        }

        static void BuildRuleTiles()
        {
            foreach (var (key, _, _) in Biomes)
            {
                var g = Frames(GroundPath(key));
                if (g.Length >= 3)
                {
                    // playable-area fill: centre variants only (the coast blob underneath draws every edge)
                    var fill = LoadOrCreate(GroundTilePath(key));
                    fill.m_DefaultSprite = g[0];
                    fill.m_DefaultColliderType = Tile.ColliderType.None;
                    fill.m_TilingRules.Clear();
                    fill.m_TilingRules.Add(Rule(new[] { g[0], g[1], g[2] }, O, O, O, O, O, O, O, O));
                    EditorUtility.SetDirty(fill);

                    // 47-blob coast tile: one exact rule per reduced neighbour mask (corner bits are don't-care
                    // unless both adjacent edges are ground); the all-ground mask uses the random fill variants
                    var b = Frames(BlobPath(key));
                    var coast = LoadOrCreate(CoastTilePath(key));
                    coast.m_DefaultSprite = g[0];
                    coast.m_DefaultColliderType = Tile.ColliderType.None;
                    var rules = coast.m_TilingRules;
                    rules.Clear();
                    var masks = BlobMasks();
                    if (b.Length >= masks.Length)
                        for (int i = 0; i < masks.Length; i++)
                        {
                            int m = masks[i];
                            bool Has(int bit) => (m & bit) != 0;
                            int Edge(int bit) => Has(bit) ? T : N;
                            int Corner(int bit, int e1, int e2) => Has(e1) && Has(e2) ? (Has(bit) ? T : N) : O;
                            // order: NW, N, NE, W, E, SW, S, SE
                            var sprites = m == 255 ? new[] { g[0], g[1], g[2] } : new[] { b[i] };
                            rules.Add(Rule(sprites, Corner(128, 1, 64), Edge(1), Corner(2, 1, 4), Edge(64), Edge(4), Corner(32, 16, 64), Edge(16), Corner(8, 16, 4)));
                        }
                    EditorUtility.SetDirty(coast);
                }
                var c = Frames(CliffPath(key));
                if (c.Length >= 8)
                {
                    var tile = LoadOrCreate(CliffTilePath(key));
                    tile.m_DefaultSprite = c[0];
                    tile.m_DefaultColliderType = Tile.ColliderType.None;
                    var rules = tile.m_TilingRules;
                    rules.Clear();
                    // Cliff cells hang under every south-facing edge of the landmass; staircases give single cells
                    // with a higher neighbour diagonally (NW / NE). Order matters: first match wins.
                    rules.Add(Rule(new[] { c[0], c[1], c[2] }, N, O, N, N, N, O, O, O));      // lone cell
                    rules.Add(Rule(new[] { c[3] }, N, O, O, N, O, O, O, O));                  // left end (under the SW corner)
                    rules.Add(Rule(new[] { c[4] }, O, O, N, O, N, O, O, O));                  // right end (under the SE corner)
                    rules.Add(Rule(new[] { c[5] }, T, O, O, N, O, O, O, O));                  // inner left: steps down from a higher cliff
                    rules.Add(Rule(new[] { c[6] }, O, O, T, O, N, O, O, O));                  // inner right
                    rules.Add(Rule(new[] { c[0], c[1], c[2], c[0], c[1], c[2], c[7] }, O, O, O, O, O, O, O, O));   // mid, rare waterfall spout
                    EditorUtility.SetDirty(tile);
                }
            }
        }

        static RuleTile LoadOrCreate(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<RuleTile>(path);
            if (t != null) return t;
            t = ScriptableObject.CreateInstance<RuleTile>();
            AssetDatabase.CreateAsset(t, path);
            return t;
        }

        // ------------------------------------------------------------------ pixel helpers

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;
            t.SetPixels(px);
            return t;
        }

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

        /// <summary>Smooth value noise, wrapping every <paramref name="wrapX"/> / <paramref name="wrapY"/> grid cells (0 = no wrap).</summary>
        static float Noise(float x, float y, int seed, int wrapX = 0, int wrapY = 0)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int W(int v, int m) => m > 0 ? ((v % m) + m) % m : v;
            float a = Hash(W(x0, wrapX), W(y0, wrapY), seed), b = Hash(W(x0 + 1, wrapX), W(y0, wrapY), seed);
            float c = Hash(W(x0, wrapX), W(y0 + 1, wrapY), seed), d = Hash(W(x0 + 1, wrapX), W(y0 + 1, wrapY), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float x, float y, int seed, int wrapX = 0, int wrapY = 0, int oct = 4)
        {
            float s = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < oct; o++)
            {
                int k = 1 << o;
                s += amp * Noise(x * k, y * k, seed + o * 31, wrapX * k, wrapY * k);
                norm += amp; amp *= 0.5f;
            }
            return s / norm;
        }

        static Color Shade(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
        static Color A(Color c, float a) { c.a = a; return c; }

        static readonly float[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        static float Dither(int x, int y) => (Bayer4[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;

        // ------------------------------------------------------------------ 47-blob ground sheet (ART-SPEC 3.0)

        /// <summary>
        /// Neighbour bits of the 47-blob layout: N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128 (1 = ground there).
        /// A corner bit only counts when both adjacent edges are set. The 47 valid masks, in ASCENDING order, are
        /// the frame order of <c>island_&lt;biome&gt;_ground_blob_32x32_47f.png</c> (frame 0 = isolated cell, frame 46 = all
        /// eight neighbours).
        /// </summary>
        public static int[] BlobMasks()
        {
            var list = new List<int>();
            for (int m = 0; m < 256; m++)
            {
                bool n = (m & 1) != 0, e = (m & 4) != 0, s = (m & 16) != 0, w = (m & 64) != 0;
                if ((m & 2) != 0 && !(n && e)) continue;
                if ((m & 8) != 0 && !(s && e)) continue;
                if ((m & 32) != 0 && !(s && w)) continue;
                if ((m & 128) != 0 && !(n && w)) continue;
                list.Add(m);
            }
            return list.ToArray();
        }

        /// <summary>Is the pixel (float coords, y up, may lie outside the tile) ground in blob frame <paramref name="m"/>?</summary>
        static bool BlobInside(int m, float px, float py)
        {
            const float e = 4f, R = 12f, S = 32f;           // coast inset on N/E/W edges (the S edge runs to the cliff rim), corner radius
            bool mN = (m & 1) == 0, mE = (m & 4) == 0, mS = (m & 16) == 0, mW = (m & 64) == 0;
            if (mW && px < e) return false;
            if (mE && px > S - e) return false;
            if (mN && py > S - e) return false;
            if (mS && py < 0f) return false;
            bool Out(float cx, float cy, bool right, bool top)
            {
                if ((right ? px > cx : px < cx) && (top ? py > cy : py < cy)) { float dx = px - cx, dy = py - cy; return dx * dx + dy * dy > R * R; }
                return false;
            }
            if (mN && mE && Out(S - e - R, S - e - R, true, true)) return false;
            if (mN && mW && Out(e + R, S - e - R, false, true)) return false;
            if (mS && mE && Out(S - e - R, R, true, false)) return false;
            if (mS && mW && Out(e + R, R, false, false)) return false;
            // soft inner corners (north side only: the cliff rim fills the south diagonals)
            if (!mN && !mE && (m & 2) == 0 && (px - S) * (px - S) + (py - S) * (py - S) < e * e) return false;
            if (!mN && !mW && (m & 128) == 0 && px * px + (py - S) * (py - S) < e * e) return false;
            return true;
        }

        static Texture2D BlobStrip(Color baseC, int seed)
        {
            const int S = 32;
            var masks = BlobMasks();
            var t = NewTex(S * masks.Length, S);
            Color lip = Shade(baseC, 1.45f), outline = Shade(baseC, 0.5f), dark = Shade(baseC, 0.72f), grid = Color.Lerp(baseC, Color.white, 0.05f);
            for (int f = 0; f < masks.Length; f++)
            {
                int m = masks[f];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        if (!BlobInside(m, x + 0.5f, y + 0.5f)) continue;
                        float n = (Hash(x, y, seed + (f % 3)) - 0.5f) * 0.08f;
                        var c = Shade(baseC, 1f + n);
                        if (Hash(x, y, seed + 7 + (f % 3)) > 0.975f) c = Shade(baseC, 1.35f);
                        if (x == 0 || y == S - 1) c = grid;
                        for (int d = 1; d <= 3; d++)
                        {
                            bool up = !BlobInside(m, x + 0.5f, y + 0.5f + d), dn = !BlobInside(m, x + 0.5f, y + 0.5f - d);
                            bool side = !BlobInside(m, x + 0.5f - d, y + 0.5f) || !BlobInside(m, x + 0.5f + d, y + 0.5f);
                            if (!(up || dn || side)) continue;
                            c = d == 1 ? outline : (dn && !up && !side) ? dark : lip;
                            break;
                        }
                        t.SetPixel(f * S + x, y, c);
                    }
            }
            t.Apply();
            return t;
        }

        /// <summary>Boundary props (placeholder): frames 0-1 rocks, 2-3 shrubs, 4-5 grass tufts; bottom-centre pivot.</summary>
        static Texture2D BoundaryPropsTex()
        {
            const int S = 32;
            var t = NewTex(S * 6, S);
            var rockC = new Color(0.55f, 0.55f, 0.58f);
            var leaf = new Color(0.22f, 0.5f, 0.28f);
            var grass = new Color(0.5f, 0.78f, 0.35f);
            for (int f = 0; f < 6; f++)
            {
                int type = f / 2, v = f % 2;
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        Color? c = null;
                        float dx = x - 15.5f;
                        if (type == 0)
                        {
                            float rx = 11f - v * 2f, ry = 8f - v;
                            float d = dx * dx / (rx * rx) + (y - 3f) * (y - 3f) / (ry * ry);
                            if (d < 1f && y >= 0)
                            {
                                float lit = 1.05f - (x - 8f) * 0.012f + (y - 4f) * 0.015f + (Hash(x, y, 40 + f) - 0.5f) * 0.18f;
                                c = d > 0.8f ? Shade(rockC, 0.5f) : Shade(rockC, lit);
                            }
                        }
                        else if (type == 1)
                        {
                            for (int k = 0; k < 3; k++)
                            {
                                float bx = 15.5f + (k - 1) * (7f - v), by = 6f + (k == 1 ? 3f : 0f), br = 7f + (k == 1 ? 2f : 0f);
                                float d = ((x - bx) * (x - bx) + (y - by) * (y - by)) / (br * br);
                                if (d < 1f) c = d > 0.78f ? Shade(leaf, 0.55f) : Shade(leaf, 0.85f + (y - by) * 0.03f + (Hash(x, y, 50 + f) - 0.5f) * 0.25f);
                            }
                        }
                        else
                        {
                            for (int k = 0; k < 6; k++)
                            {
                                float bx = 6f + k * 4f + Hash(k, v, 60) * 2f, h = 8f + Hash(k, v, 61) * 12f;
                                float lean = (k - 2.5f) * 0.18f * (y / Mathf.Max(1f, h)) * 6f;
                                if (y < h && Mathf.Abs(x - (bx + lean)) < 1.1f - y / h * 0.8f) c = Shade(grass, 0.7f + y / h * 0.5f);
                            }
                        }
                        if (c.HasValue) t.SetPixel(f * S + x, y, c.Value);
                    }
            }
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ ground + cliff strips

        static Texture2D GroundStrip(Color baseC, int seed)
        {
            const int S = 32;
            var t = NewTex(S * 19, S);
            Color lip = Shade(baseC, 1.45f), outline = Shade(baseC, 0.55f), grid = Color.Lerp(baseC, Color.white, 0.05f);
            for (int f = 0; f < 19; f++)
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        int px = f * S + x;
                        if (f >= 15)
                        {
                            // scatter overlays: a few flecks on transparency
                            if (Hash(x, y, seed + f) > 0.985f) t.SetPixel(px, y, A(Shade(baseC, 1.9f), 1f));
                            continue;
                        }
                        float n = (Hash(x, y, seed + (f % 3)) - 0.5f) * 0.08f;
                        var c = Shade(baseC, 1f + n);
                        if (Hash(x, y, seed + 7 + (f % 3)) > 0.975f) c = Shade(baseC, 1.35f);       // light flecks
                        if (x == 0 || y == S - 1) c = grid;                                           // faint cell grid (original look)
                        // y = 0 is the BOTTOM row of the texture (south), y = S-1 the top (north)
                        bool n_ = f == 3 || f == 7 || f == 8, s_ = f == 5 || f == 9 || f == 10;
                        bool w_ = f == 6 || f == 7 || f == 10, e_ = f == 4 || f == 8 || f == 9;
                        int dn = S - 1 - y, ds = y, dw = x, de = S - 1 - x;
                        if (n_ && dn < 3) c = dn == 0 ? outline : lip;
                        if (s_ && ds < 3) c = ds == 0 ? outline : Shade(baseC, 0.8f);
                        if (w_ && dw < 3) c = dw == 0 ? outline : lip;
                        if (e_ && de < 3) c = de == 0 ? outline : Shade(baseC, 0.85f);
                        // inner corners: a small rim notch at that corner
                        if (f == 11 && dw < 3 && dn < 3) c = lip;
                        if (f == 12 && de < 3 && dn < 3) c = lip;
                        if (f == 13 && de < 3 && ds < 3) c = lip;
                        if (f == 14 && dw < 3 && ds < 3) c = lip;
                        t.SetPixel(px, y, c);
                    }
            t.Apply();
            return t;
        }

        static Texture2D CliffStrip(Color ground, Color rock, int seed)
        {
            const int S = 32;
            var t = NewTex(S * 8, S);
            for (int f = 0; f < 8; f++)
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        int px = f * S + x, top = S - 1 - y;
                        // jagged bottom edge (transparent below), tapering ends
                        float jag = 5f + 4f * Noise((f * S + x) * 0.25f, 0, seed + 3);
                        if (y < jag) continue;
                        if (f == 3 && x < (S - 1 - y) * 0.6f) continue;          // left end slopes in
                        if (f == 4 && (S - 1 - x) < (S - 1 - y) * 0.6f) continue; // right end slopes in
                        Color c;
                        if (top < 4) c = top == 3 ? Shade(ground, 0.6f) : Shade(ground, 0.9f);            // earth lip
                        else
                        {
                            float strata = Mathf.Sin((y + Noise(px * 0.15f, y * 0.1f, seed) * 6f) * 0.9f) * 0.08f;
                            float n = (Hash(px, y, seed) - 0.5f) * 0.12f;
                            c = Shade(rock, 0.95f + strata + n - (S - y) * 0.004f);
                            if (Hash(px, y, seed + 9) > 0.985f) c = Shade(rock, 1.4f);                      // glints
                        }
                        if (f == 7 && x >= 13 && x <= 18 && top >= 3) c = Color.Lerp(new Color(0.85f, 0.97f, 1f), c, (Mathf.Abs(x - 15.5f)) / 4f);   // waterfall spout
                        t.SetPixel(px, y, c);
                    }
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ underside + mist + veil

        static Texture2D Underside(int w, int h, int seed, float bulge)
        {
            var t = NewTex(w, h);
            var baseC = new Color(0.62f, 0.56f, 0.50f);       // neutral; tinted per Island
            for (int y = 0; y < h; y++)
            {
                float d = (h - 1 - y) / (float)h;                 // 0 at the top, 1 at the tip
                float half = w * 0.5f * Mathf.Pow(Mathf.Max(0f, 1f - d), bulge) * (0.95f + 0.05f * Mathf.Sin(d * 12f));
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - w * 0.5f + 0.5f);
                    float edge = half * (0.85f + 0.3f * Noise(x * 0.06f, y * 0.06f, seed));
                    if (dx > edge) continue;
                    float n = Fbm(x * 0.05f, y * 0.08f, seed + 5, 0, 0, 3);
                    float lightK = 1.05f - d * 0.5f - (x / (float)w) * 0.2f + (n - 0.5f) * 0.35f;
                    var c = Shade(baseC, lightK);
                    if (dx > edge - 1.5f) c = Shade(baseC, 0.45f);                          // outline
                    if (Mathf.Abs(Noise(x * 0.04f, y * 0.04f, seed + 11) - 0.5f) < 0.012f)      // glowing mineral veins
                        c = new Color(0.55f, 0.95f, 0.85f);
                    // dither the tip into mist
                    if (d > 0.7f && Dither(x, y) < (d - 0.7f) / 0.3f) continue;
                    t.SetPixel(x, y, c);
                }
            }
            t.Apply();
            return t;
        }

        static Texture2D Roots()
        {
            const int w = 192, h = 160;
            var t = NewTex(w, h);
            var bark = new Color(0.40f, 0.30f, 0.22f);
            for (int r = 0; r < 9; r++)
            {
                float x0 = 20 + r * 19 + Hash(r, 0, 5) * 10, amp = 6 + Hash(r, 1, 5) * 10, len = h * (0.45f + Hash(r, 2, 5) * 0.55f);
                float thick = 5f - r % 3;
                for (int yy = 0; yy < len; yy++)
                {
                    float k = yy / len;
                    float cx = x0 + Mathf.Sin(yy * 0.07f + r) * amp * k;
                    float th = Mathf.Max(1f, thick * (1f - k));
                    for (int x = Mathf.FloorToInt(cx - th); x <= cx + th; x++)
                        if (x >= 0 && x < w) t.SetPixel(x, h - 1 - yy, Shade(bark, 0.8f + 0.4f * Hash(x, yy, 9)));
                    if (Hash(r, yy, 13) > 0.97f)       // jade leaves
                        for (int lx = -2; lx <= 2; lx++) for (int ly = -1; ly <= 1; ly++)
                        {
                            int px = Mathf.RoundToInt(cx) + lx + 3, py = h - 1 - yy + ly;
                            if (px >= 0 && px < w && py >= 0 && py < h) t.SetPixel(px, py, new Color(0.35f, 0.75f, 0.5f));
                        }
                }
            }
            t.Apply();
            return t;
        }

        static Texture2D Puff(int w, int h, Color c, float alpha, int seed)
        {
            var t = NewTex(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - w * 0.5f) / (w * 0.5f), dy = (y - h * 0.5f) / (h * 0.5f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy) + (Fbm(x * 0.06f, y * 0.06f, seed, 0, 0, 3) - 0.5f) * 0.5f;
                    float a = Mathf.Clamp01(1f - r) * alpha * Mathf.Clamp01((1f - Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy))) * 5f);   // fades to 0 at the borders
                    if (a > 0.01f) t.SetPixel(x, y, A(c, a));
                }
            t.Apply();
            return t;
        }

        static Texture2D Veil()
        {
            const int S = 256;
            var t = NewTex(S * 4, S);
            var fog = new Color(0.78f, 0.75f, 0.86f);
            for (int f = 0; f < 4; f++)
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float sx = (x + f * 16) % S;
                        float n = Fbm(sx / 32f, y / 32f, 77, S / 32, S / 32, 4);
                        float a = Mathf.Lerp(0.5f, 0.8f, n);
                        if (n < 0.33f) a *= 0.6f;                 // a few holes
                        t.SetPixel(f * S + x, y, A(Shade(fog, 0.85f + n * 0.25f), a));
                    }
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ stele

        static void DrawStele(Texture2D t, int ox, int state, float glow)
        {
            const int W = 64, H = 96;
            var stone = new Color(0.55f, 0.56f, 0.58f);
            var gold = new Color(1f, 0.78f, 0.25f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color? c = null;
                    if (y < 10 && x >= 4 && x < 60) c = Shade(stone, 0.7f + (y == 9 ? 0.2f : 0f));                // plinth
                    else if (y >= 10 && x >= 12 && x < 52)
                    {
                        int top = 88 - (int)(Mathf.Pow(Mathf.Abs(x - 31.5f) / 20f, 2f) * 8f);                     // rounded top
                        if (y <= top)
                        {
                            float n = (Hash(x, y, 3) - 0.5f) * 0.1f;
                            c = Shade(stone, 1f + n - (x - 12) * 0.006f);
                            if (x == 12 || x == 51 || y == top) c = Shade(stone, 0.55f);
                            bool groove = (y == 30 || y == 50 || y == 70) && x > 18 && x < 46 || (x == 22 || x == 41) && y > 26 && y < 76;
                            if (groove)
                            {
                                bool lit = state == 2 || (state == 1 && y < 50);
                                c = lit ? Color.Lerp(Shade(stone, 0.6f), gold, 0.6f + 0.4f * glow) : Shade(stone, 0.6f);
                            }
                            if (state == 0 && x >= 29 && x <= 34 && y > 20 && y < 80) c = new Color(0.78f, 0.15f, 0.12f);   // red talisman strip
                            if (state == 0 && x >= 27 && x <= 36 && y >= 46 && y <= 54 && (x == 27 || x == 36 || y == 46 || y == 54)) c = new Color(0.25f, 0.25f, 0.28f); // lock glyph
                        }
                    }
                    if (c.HasValue) t.SetPixel(ox + x, y, c.Value);
                    else if (state == 2 && glow > 0)
                    {
                        // soft gold aura around the tablet
                        float dx = Mathf.Max(0, Mathf.Max(12 - x, x - 51)), dy = Mathf.Max(0, y - 88);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (y >= 10 && d < 8) t.SetPixel(ox + x, y, A(gold, (1 - d / 8f) * 0.45f * glow));
                    }
                }
        }

        static Texture2D Stele(int state, float glow)
        {
            var t = NewTex(64, 96);
            DrawStele(t, 0, state, glow);
            t.Apply();
            return t;
        }

        static Texture2D SteleReadyStrip()
        {
            var t = NewTex(64 * 4, 96);
            for (int f = 0; f < 4; f++) DrawStele(t, f * 64, 2, 0.5f + 0.5f * Mathf.Sin(f / 4f * Mathf.PI * 2f));
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ sky

        static Texture2D Gradient()
        {
            const int w = 512, h = 1024;
            var t = NewTex(w, h);
            // bottom (y = 0): pale cloud-sea mist → qi cyan → water blue → deep ink at the top
            var stops = new[] { (0f, Hex("#b7d3dc")), (0.18f, Hex("#6fb2c2")), (0.45f, Hex("#2f5f86")), (0.75f, Hex("#1a2f52")), (1f, SkyBase) };
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                Color c = stops[0].Item2;
                for (int i = 0; i < stops.Length - 1; i++)
                    if (v >= stops[i].Item1 && v <= stops[i + 1].Item1)
                    {
                        float k = (v - stops[i].Item1) / (stops[i + 1].Item1 - stops[i].Item1);
                        c = Color.Lerp(stops[i].Item2, stops[i + 1].Item2, k);
                    }
                for (int x = 0; x < w; x++)
                {
                    var p = c;     // smooth (the real art brings its own dithered bands; magnified dither reads as stripes)
                    // sun haze upper-left
                    float dx = (x - w * 0.2f) / w, dy = (y - h * 0.82f) / h;
                    float haze = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 4f) * 0.18f;
                    p = Color.Lerp(p, new Color(1f, 0.95f, 0.85f), haze);
                    if (v > 0.7f && Hash(x, y, 101) > 0.9992f) p = Color.Lerp(p, Color.white, 0.8f);      // faint stars
                    p.a = 1f;
                    t.SetPixel(x, y, p);
                }
            }
            t.Apply();
            return t;
        }

        /// <summary>Horizontally tileable cloud strip: wrapped soft blobs, lit tops, shadowed undersides.</summary>
        static Texture2D Clouds(int seed, int count, float scale, float yBand, Color lit, Color shadow, float alpha)
        {
            const int w = 1024, h = 256;
            var t = NewTex(w, h);
            var blobs = new List<(float x, float y, float rx, float ry)>();
            for (int i = 0; i < count; i++)
            {
                float rx = (40f + Hash(i, 1, seed) * 70f) * scale, ry = rx * (0.35f + Hash(i, 2, seed) * 0.2f);
                blobs.Add((Hash(i, 0, seed) * w, h * (yBand * 0.5f + Hash(i, 3, seed) * yBand), rx, ry));
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float best = 0f, under = 0f;
                    foreach (var b in blobs)
                    {
                        float dx = Mathf.Abs(x - b.x); dx = Mathf.Min(dx, w - dx);      // wrap horizontally
                        float dy = y - b.y;
                        float d = (dx * dx) / (b.rx * b.rx) + (dy * dy) / (b.ry * b.ry);
                        float v = 1f - d;
                        if (v > best) { best = v; under = Mathf.Clamp01(-dy / b.ry); }
                    }
                    if (best <= 0f) continue;
                    float n = Fbm(x / 64f, y / 64f, seed + 3, w / 64, 0, 3);
                    float a = Mathf.Clamp01(best * 2.2f + (n - 0.5f) * 0.8f);
                    if (a <= 0.02f) continue;
                    var c = Color.Lerp(lit, shadow, under * 0.8f);
                    // dithered alpha steps (pixel-art feel)
                    a = Mathf.Floor(a * 4f + Dither(x, y)) / 4f * alpha;
                    if (a <= 0f) continue;
                    t.SetPixel(x, y, A(c, a));
                }
            t.Apply();
            return t;
        }

        static Texture2D PeaksTex()
        {
            const int w = 1024, h = 256;
            var t = NewTex(w, h);
            var far = new Color(0.36f, 0.46f, 0.60f);
            var near = new Color(0.25f, 0.33f, 0.46f);
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w;
                // two ridges of spires; sums of wrapped sines + sharp peaks
                float r1 = 120 + 40 * Mathf.Sin(u * Mathf.PI * 2 * 3) + 30 * Mathf.Sin(u * Mathf.PI * 2 * 7 + 1) + 60 * Mathf.Pow(Mathf.Abs(Mathf.Sin(u * Mathf.PI * 5)), 8);
                float r2 = 80 + 25 * Mathf.Sin(u * Mathf.PI * 2 * 4 + 2) + 70 * Mathf.Pow(Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3 + 0.7f)), 14);
                for (int y = 0; y < h; y++)
                {
                    Color? c = null;
                    if (y < r2) c = near;
                    else if (y < r1) c = far;
                    if (!c.HasValue) continue;
                    float fade = Mathf.Clamp01(y / 90f);          // fade into mist at the base
                    float a = Mathf.Floor(fade * 4f + Dither(x, y)) / 4f;
                    if (a <= 0f) continue;
                    t.SetPixel(x, y, A(c.Value, a * 0.9f));
                }
            }
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ qi trail + bridges

        static Texture2D Trail()
        {
            const int W = 64, H = 16;
            var t = NewTex(W * 4, H);
            var core = new Color(0.75f, 1f, 1f);
            for (int f = 0; f < 4; f++)
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float dy = Mathf.Abs(y - (H - 1) * 0.5f);
                        float wave = Mathf.Sin((x + f * 16) / (float)W * Mathf.PI * 2f) * 1.2f;
                        float a = Mathf.Clamp01(1f - Mathf.Abs(dy - wave * 0.3f) / 3.5f) * 0.8f;
                        // sparkles move left → right one quarter tile per frame
                        int sx = (x - f * 16 + W) % W;
                        if ((sx == 6 || sx == 38) && Mathf.Abs(y - 7.5f) < 2.5f) a = 1f;
                        if (a > 0.03f) t.SetPixel(f * W + x, y, A(Color.Lerp(new Color(0.35f, 0.85f, 0.95f), core, a), a));
                    }
            t.Apply();
            return t;
        }

        static void DrawBridge(Texture2D t, int ox, int state, int f)
        {
            const int S = 64;
            var stone = new Color(0.5f, 0.52f, 0.55f);
            var jade = new Color(0.35f, 0.72f, 0.58f);
            var cinnabar = new Color(0.78f, 0.22f, 0.16f);
            Color pool = state == 1 ? new Color(0.45f, 0.95f, 1f) : state == 2 ? new Color(0.75f, 0.95f, 0.45f) : new Color(0.38f, 0.40f, 0.44f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    Color? c = null;
                    if (y < 10 && x >= 2 && x < 62) c = Shade(stone, 0.8f + (y == 9 ? 0.25f : 0f) - (Hash(x, y, 4) * 0.08f));   // platform
                    bool post = y >= 10 && y < 44 && (x >= 6 && x < 14 || x >= 50 && x < 58);
                    if (post) c = (y > 36 || y < 14) ? cinnabar : Shade(jade, 0.9f + (x % 8 == 0 ? -0.2f : 0f));
                    if (y >= 44 && y < 48 && x >= 3 && x < 61) c = cinnabar;                     // lintel
                    if (y >= 48 && y < 50 && x >= 1 && x < 63) c = Shade(cinnabar, 0.7f);
                    // pool / mirror between the posts
                    float dx = (x - 31.5f) / 16f, dy = (y - 22f) / 10f;
                    float r = dx * dx + dy * dy;
                    if (y >= 10 && r < 1f)
                    {
                        float shimmer = state == 0 ? 0.9f : 0.85f + 0.25f * Mathf.Sin((x + y) * 0.6f + f * 1.6f);
                        c = Shade(pool, shimmer * (1.1f - r * 0.4f));
                    }
                    if (state == 0 && x >= 29 && x <= 34 && y >= 30 && y <= 40) c = cinnabar;     // "unlinked" talisman
                    // qi streams: sending rises out, receiving falls in
                    if (state != 0 && x > 18 && x < 46 && y >= 26 && y < 62)
                    {
                        int lane = (x - 18) % 7;
                        int phase = state == 1 ? (y - f * 6) : (y + f * 6);
                        if (lane == 3 && ((phase % 12) + 12) % 12 < 4)
                            c = A(Color.Lerp(pool, Color.white, 0.4f), 0.85f);
                    }
                    if (c.HasValue) t.SetPixel(ox + x, y, c.Value);
                }
        }

        static Texture2D Bridge(int state, int f)
        {
            var t = NewTex(64, 64);
            DrawBridge(t, 0, state, f);
            t.Apply();
            return t;
        }

        static Texture2D BridgeStrip(int state)
        {
            var t = NewTex(64 * 4, 64);
            for (int f = 0; f < 4; f++) DrawBridge(t, f * 64, state, f);
            t.Apply();
            return t;
        }
    }
}
