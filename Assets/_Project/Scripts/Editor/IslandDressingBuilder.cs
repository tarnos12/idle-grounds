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
            const int step = 5;
            for (int gy = yMin; gy <= yMax; gy += step)
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

            foreach (var tm in root.GetComponentsInChildren<Tilemap>()) tm.CompressBounds();
            EditorUtility.SetDirty(isl.gameObject);
        }
    }
}
