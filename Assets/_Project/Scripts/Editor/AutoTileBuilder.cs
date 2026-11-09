using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using IdleGrounds.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// ART-SPEC §3.10.3 auto tiles (dual grid, Wang-16 sheets). Sheets <c>island_&lt;biome&gt;_auto_&lt;terrain&gt;_128x160.png</c>
    /// (20 sprites of 32 x 32: <see cref="AutoTileGrid.Layout"/> + fills F0-F3) and <c>island_&lt;biome&gt;_auto_sidewall_64x128.png</c>
    /// (8 sprites: W face frames 0-3 = column 0-31, E face frames 0-3 = column 32-63, frame k = rows 32k..32k+31 from the top).
    /// <para>Art source: the delivered sheet in <c>Art/Incoming/islands/</c> when it is there, else a placeholder of the same file name
    /// in <c>Art/Islands/AutoTiles/</c>, made once from the template alpha (<c>docs/art/reference/autotile/</c>) tinted per terrain.
    /// ArtIntake.Integrate copies a delivered sheet over its same-named placeholder, so the Tile assets show the real art with no
    /// code change (sprite names / ids are the same grid either way).</para>
    /// Built Islands are not switched over (batch AT2); the Islands-071..074 Islands use it when they are built (ADR 0007 Phase 6).
    /// </summary>
    public static class AutoTileBuilder
    {
        public const string IncomingDir = "Assets/_Project/Art/Incoming/islands/";
        public const string PlaceholderDir = IslandArtBuilder.IslandDir + "/AutoTiles/";
        public const string TileDir = IslandArtBuilder.TileDir + "/Auto/";
        const string TemplateDir = "docs/art/reference/autotile/";
        const string Layer = "Ground";

        static readonly Regex SheetRx = new Regex(@"^island_[a-z0-9]+_auto_[a-z0-9]+_(128x160|64x128)\.png$", RegexOptions.Compiled);

        /// <summary>An auto-tile sheet anywhere under Assets/_Project/Art (incoming or placeholder).</summary>
        public static bool IsSheet(string assetPath) =>
            assetPath.StartsWith("Assets/_Project/Art/", System.StringComparison.OrdinalIgnoreCase) && SheetRx.IsMatch(Path.GetFileName(assetPath));

        // ------------------------------------------------------------------ the AT1 sheets (§3.10.2)

        public class SheetDef
        {
            public string biome, terrain;
            public Color32 tint;     // placeholder base colour (the real sheet brings its own palette)
            public SheetDef(string b, string t, Color32 c) { biome = b; terrain = t; tint = c; }
        }

        public static readonly SheetDef[] Sheets =
        {
            new SheetDef("lotus", "ground", new Color32(79, 122, 58, 255)),
            new SheetDef("lotus", "grass", new Color32(112, 156, 60, 255)),
            new SheetDef("lotus", "water", new Color32(63, 127, 154, 255)),
            new SheetDef("lotus", "path", new Color32(156, 144, 122, 255)),
            new SheetDef("thunder", "ground", new Color32(74, 84, 104, 255)),
            new SheetDef("thunder", "grit", new Color32(138, 144, 156, 255)),
            new SheetDef("thunder", "path", new Color32(52, 58, 76, 255)),
            new SheetDef("ravine", "ground", new Color32(85, 122, 58, 255)),
            new SheetDef("ravine", "earth", new Color32(176, 136, 80, 255)),
            new SheetDef("ravine", "path", new Color32(160, 136, 104, 255)),
            new SheetDef("tribulation", "ground", new Color32(169, 184, 168, 255)),
            new SheetDef("tribulation", "grass", new Color32(111, 165, 138, 255)),
            new SheetDef("tribulation", "path", new Color32(132, 146, 138, 255)),
        };

        /// <summary>Biomes with a new side-face sheet (the Ravine faces are validated already, no new file).</summary>
        public static readonly (string biome, Color32 rock, Color32 grass)[] SideSheets =
        {
            ("lotus", new Color32(70, 92, 70, 255), new Color32(112, 156, 60, 255)),
            ("thunder", new Color32(66, 72, 92, 255), new Color32(138, 144, 156, 255)),
            ("tribulation", new Color32(84, 116, 98, 255), new Color32(111, 165, 138, 255)),
        };

        static readonly string[] Overlays = { "grass", "grit", "earth" };

        /// <summary>§3.10.3 layer orders on the Ground sorting layer: ground 0 (fill slot), overlays 1 (Macro slot), water 3, path 4.</summary>
        public static int DefaultOrder(string terrain) =>
            terrain == "path" ? 4 : terrain == "water" ? 3 : terrain == "ground" ? 0 : 1;

        public static bool IsOverlay(string terrain) => System.Array.IndexOf(Overlays, terrain) >= 0;

        public static string SheetName(string biome, string terrain) => $"island_{biome}_auto_{terrain}_128x160.png";
        public static string SideName(string biome) => $"island_{biome}_auto_sidewall_64x128.png";

        /// <summary>Delivered sheet if present, else the placeholder (made on first use).</summary>
        public static string SheetPath(string biome, string terrain)
        {
            string inc = IncomingDir + SheetName(biome, terrain);
            if (File.Exists(inc)) return inc;
            string ph = PlaceholderDir + SheetName(biome, terrain);
            if (!File.Exists(ph)) WritePlaceholder(ph, Def(biome, terrain)?.tint ?? new Color32(128, 128, 128, 255), terrain);
            return ph;
        }

        public static string SidePath(string biome)
        {
            string inc = IncomingDir + SideName(biome);
            if (File.Exists(inc)) return inc;
            string ph = PlaceholderDir + SideName(biome);
            if (!File.Exists(ph))
            {
                foreach (var s in SideSheets) if (s.biome == biome) { WriteSidePlaceholder(ph, s.rock, s.grass); return ph; }
                return null;
            }
            return ph;
        }

        static SheetDef Def(string biome, string terrain)
        {
            foreach (var d in Sheets) if (d.biome == biome && d.terrain == terrain) return d;
            return null;
        }

        public static IEnumerable<string> Terrains(string biome)
        {
            foreach (var d in Sheets) if (d.biome == biome) yield return d.terrain;
        }

        // ------------------------------------------------------------------ import (Sprite Multiple, 32 x 32 grid, PPU 32, Point)

        /// <summary>Sprite rects of a sheet: slot n = r * cols + c (r from the top) for a terrain sheet; for a side sheet
        /// n = side * 4 + k (side 0 = W columns 0-31, 1 = E columns 32-63; k = piece from the top).</summary>
        public static SpriteMetaData[] Rects(string baseName, int w, int h)
        {
            var list = new List<SpriteMetaData>();
            const int T = AutoTileGrid.TileSize;
            bool side = baseName.Contains("_auto_sidewall_");
            int cols = w / T, rows = h / T;
            if (side)
            {
                for (int s = 0; s < cols; s++)
                    for (int k = 0; k < rows; k++)
                        list.Add(Meta(baseName, s * rows + k, s * T, h - (k + 1) * T));
            }
            else
            {
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        list.Add(Meta(baseName, r * cols + c, c * T, h - (r + 1) * T));
            }
            return list.ToArray();
        }

        static SpriteMetaData Meta(string bn, int n, int x, int y) => new SpriteMetaData
        {
            name = bn + "_" + n,
            rect = new Rect(x, y, AutoTileGrid.TileSize, AutoTileGrid.TileSize),
            alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f),
        };

        class Import : AssetPostprocessor
        {
            void OnPreprocessTexture()
            {
                if (!IsSheet(assetPath)) return;
                var ti = (TextureImporter)assetImporter;
                string bn = Path.GetFileNameWithoutExtension(assetPath);
                bool side = bn.EndsWith("_64x128");
                int w = side ? 64 : 128, h = side ? 128 : 160;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Multiple;
                ti.spritePixelsPerUnit = 32;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.sRGBTexture = true;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 2048;
                var st = new TextureImporterSettings();
                ti.ReadTextureSettings(st);
                st.spriteMeshType = SpriteMeshType.FullRect;
                st.spriteGenerateFallbackPhysicsShape = false;
                ti.SetTextureSettings(st);
#pragma warning disable CS0618
                ti.spritesheet = Rects(bn, w, h);
#pragma warning restore CS0618
            }
        }

        // ------------------------------------------------------------------ placeholders (template alpha, tinted)

        static Color32[] ReadTopDown(string path, out int w, out int h)
        {
            w = h = 0;
            if (!File.Exists(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!t.LoadImage(File.ReadAllBytes(path))) { Object.DestroyImmediate(t); return null; }
            w = t.width; h = t.height;
            var bu = t.GetPixels32();
            Object.DestroyImmediate(t);
            var td = new Color32[bu.Length];
            for (int y = 0; y < h; y++) System.Array.Copy(bu, (h - 1 - y) * w, td, y * w, w);
            return td;
        }

        static void WriteTopDown(string path, int w, int h, Color32[] td)
        {
            var bu = new Color32[td.Length];
            for (int y = 0; y < h; y++) System.Array.Copy(td, y * w, bu, (h - 1 - y) * w, w);
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(bu);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)seed * 0xCB1AB31Fu;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;   // lowbias32 finaliser
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static Color32 Tone(Color32 c, float k) =>
            new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), 255);

        /// <summary>Terrain placeholder: template alpha, base tint, the §3.10.1 edge band (lit 1 px on N / W edges, shaded on S / E),
        /// sparse one-tone detail away from the 3-px seam band, and T15 = F0.</summary>
        static void WritePlaceholder(string path, Color32 tint, string terrain)
        {
            var a = ReadTopDown(TemplateDir + "autotile_wang16_alpha_128x160.png", out int w, out int h);
            if (a == null || w != 128 || h != 160) { Debug.LogError("[AutoTile] template alpha missing: " + TemplateDir); return; }
            bool Solid(int x, int y) => x < 0 || y < 0 || x >= w || y >= h || a[y * w + x].a > 127;
            // the 4 x 4 corner picture (rows 0-3) and the fill row (row 4) are separate pictures: across their border, and past the
            // sheet edge, the terrain continues (no edge band)
            bool SolidFrom(int x0, int y0, int x, int y) => (y0 >= 128) != (y >= 128) || Solid(x, y);
            var px = new Color32[w * h];
            int seed = 17; foreach (char ch in terrain) seed = seed * 31 + ch;
            Color32 light = Tone(tint, 1.18f), dark = Tone(tint, 0.68f), dk2 = Tone(tint, 0.82f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Solid(x, y)) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    // edge band: an empty pixel within Manhattan distance 2; lit if it lies up / left
                    bool rim = false, lit = false; int dist = 9;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int m = System.Math.Abs(dx) + System.Math.Abs(dy);
                            if (m == 0 || m > 2 || SolidFrom(x, y, x + dx, y + dy)) continue;
                            rim = true; dist = System.Math.Min(dist, m);
                            if (dx <= 0 && dy <= 0) lit = true;
                        }
                    Color32 c = tint;
                    if (rim) c = lit ? (dist == 1 ? light : tint) : (dist == 1 ? dark : dk2);
                    else
                    {
                        int tx = x % 32, ty = y % 32;
                        bool inner = tx >= 3 && tx <= 28 && ty >= 3 && ty <= 28;
                        float r = Hash(x, y, seed);
                        if (inner && r < 0.022f) c = Hash(x, y, seed + 7) < 0.5f ? Tone(tint, 0.86f) : Tone(tint, 1.1f);
                        if (terrain == "path" && inner && ((x / 8 + y / 8) % 2 == 0) && (tx % 8 == 0 || ty % 8 == 0)) c = Tone(tint, 0.78f);   // stone joints
                    }
                    px[y * w + x] = c;
                }
            for (int y = 0; y < 32; y++)                               // T15 (row 1, column 1) = F0 (row 4, column 0), pixel for pixel
                for (int x = 0; x < 32; x++) px[(32 + y) * w + 32 + x] = px[(128 + y) * w + x];
            WriteTopDown(path, w, h, px);
            Debug.Log("[AutoTile] placeholder " + path);
        }

        /// <summary>Side-face placeholder: template alpha, slanted strata that wrap vertically (row 127 flows into row 0),
        /// a darker 4-px sky side and a 1-px ground column on the Island side (x17 / x46).</summary>
        static void WriteSidePlaceholder(string path, Color32 rock, Color32 grass)
        {
            var a = ReadTopDown(TemplateDir + "autotile_sidewall_alpha_64x128.png", out int w, out int h);
            if (a == null || w != 64 || h != 128) { Debug.LogError("[AutoTile] side template alpha missing: " + TemplateDir); return; }
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (a[y * w + x].a <= 127) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    bool west = x < 32;
                    int inward = west ? x : 63 - x;                    // 0 = sky side
                    float s = Mathf.Sin(2f * Mathf.PI * (3f * y / h) + inward * 0.35f) + 0.6f * Mathf.Sin(2f * Mathf.PI * (5f * y / h) - inward * 0.2f + 1.3f);
                    Color32 c = Tone(rock, 0.9f + 0.12f * Mathf.Sign(s) + 0.04f * s);
                    if (inward < 4) c = Tone(rock, 0.62f);
                    if (inward == 17) c = grass;
                    px[y * w + x] = c;
                }
            WriteTopDown(path, w, h, px);
            Debug.Log("[AutoTile] side placeholder " + path);
        }

        // ------------------------------------------------------------------ render strip (no seam bleed)

        // The camera pans and zooms freely, so a quad edge can land a fraction of a texel off: point sampling then reads the
        // neighbouring row / column of the source texture (a 1-px line of the tile above in a 4 x 5 grid sheet). The Tiles therefore
        // draw from a derived 1-row strip: slot n at x = n * 34 + 1, its edge columns repeated once on each side, clamped
        // vertically (the delivered tile art is all 1-row strips for the same reason). The strip is rebuilt from the current sheet
        // (delivered one first) on every sheet import, so a delivered sheet reaches the Tiles with no code change.
        public const string StripDir = PlaceholderDir + "Strips/";
        const int StripPitch = AutoTileGrid.TileSize + 2;

        static readonly Regex NameRx = new Regex(@"^island_([a-z0-9]+)_auto_([a-z0-9]+)_(128x160|64x128)$", RegexOptions.Compiled);

        public static string StripPath(string sheetPath) => StripDir + Path.GetFileNameWithoutExtension(sheetPath) + "_strip.png";

        static bool IsStrip(string assetPath) => assetPath.StartsWith(StripDir, System.StringComparison.OrdinalIgnoreCase) && assetPath.EndsWith("_strip.png");

        /// <summary>(Re)writes the padded strip of a sheet; returns its path (null if the sheet cannot be read).</summary>
        public static string MakeStrip(string sheetPath)
        {
            var src = ReadTopDown(sheetPath, out int w, out int h);
            if (src == null) return null;
            bool side = sheetPath.EndsWith("_64x128.png");
            int n = (w / 32) * (h / 32), T = AutoTileGrid.TileSize;
            var rects = Rects(Path.GetFileNameWithoutExtension(sheetPath), w, h);
            int sw = n * StripPitch;
            var px = new Color32[sw * T];
            for (int k = 0; k < n; k++)
            {
                var r = rects[k].rect;
                int sx = (int)r.x, syTop = h - (int)r.y - T;      // rect y is bottom-up
                for (int y = 0; y < T; y++)
                    for (int x = -1; x <= T; x++)
                    {
                        int cx = Mathf.Clamp(x, 0, T - 1);
                        px[y * sw + k * StripPitch + 1 + x] = src[(syTop + y) * w + sx + cx];
                    }
            }
            string path = StripPath(sheetPath);
            if (File.Exists(path))
            {
                var old = ReadTopDown(path, out int ow, out int oh);
                bool same = old != null && ow == sw && oh == T;
                if (same) for (int i = 0; i < px.Length; i++) if (!old[i].Equals(px[i])) { same = false; break; }
                if (same) return path;
            }
            WriteTopDown(path, sw, T, px);
            return path;
        }

        class StripImport : AssetPostprocessor
        {
            void OnPreprocessTexture()
            {
                if (!IsStrip(assetPath)) return;
                var ti = (TextureImporter)assetImporter;
                string bn = Path.GetFileNameWithoutExtension(assetPath);
                int n = bn.Contains("_64x128_") ? 8 : AutoTileGrid.Columns * AutoTileGrid.Rows;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Multiple;
                ti.spritePixelsPerUnit = 32;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 2048;
                var st = new TextureImporterSettings();
                ti.ReadTextureSettings(st);
                st.spriteMeshType = SpriteMeshType.FullRect;
                st.spriteGenerateFallbackPhysicsShape = false;
                ti.SetTextureSettings(st);
                var list = new SpriteMetaData[n];
                for (int k = 0; k < n; k++)
                    list[k] = new SpriteMetaData { name = bn + "_" + k, rect = new Rect(k * StripPitch + 1, 0, 32, 32), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) };
#pragma warning disable CS0618
                ti.spritesheet = list;
#pragma warning restore CS0618
            }

            // a (re)imported sheet -> rebuild its strip from the current source (delivered sheet first, so a reimported
            // placeholder never overrides delivered art)
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                foreach (var p in imported)
                {
                    if (!IsSheet(p)) continue;
                    var m = NameRx.Match(Path.GetFileNameWithoutExtension(p));
                    if (!m.Success) continue;
                    string b = m.Groups[1].Value, t = m.Groups[2].Value;
                    EditorApplication.delayCall += () =>
                    {
                        string src = t == "sidewall" ? SidePath(b) : SheetPath(b, t);
                        if (src != null && File.Exists(src)) MakeStrip(src);
                    };
                }
            }
        }

        // ------------------------------------------------------------------ tiles

        static Tile[] TilesFor(string sheetPath, string prefix, int expect)
        {
            string strip = MakeStrip(sheetPath);
            var sprites = strip != null ? IslandArtBuilder.Frames(strip) : new Sprite[0];
            if (sprites.Length != expect) { Debug.LogError($"[AutoTile] {strip ?? sheetPath}: {sprites.Length} sprites, expected {expect}"); return null; }
            Directory.CreateDirectory(TileDir);
            var list = new Tile[expect];
            for (int i = 0; i < expect; i++)
            {
                string p = $"{TileDir}{prefix}_{i}.asset";
                var t = AssetDatabase.LoadAssetAtPath<Tile>(p);
                if (t == null) { t = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(t, p); }
                if (t.sprite != sprites[i] || t.colliderType != Tile.ColliderType.None)
                {
                    t.sprite = sprites[i];
                    t.colliderType = Tile.ColliderType.None;
                    EditorUtility.SetDirty(t);
                }
                list[i] = t;
            }
            return list;
        }

        /// <summary>The 20 Tile assets of a terrain sheet, indexed by sprite slot (see <see cref="AutoTileGrid.Slot"/>).</summary>
        public static Tile[] EnsureTiles(string biome, string terrain) =>
            TilesFor(SheetPath(biome, terrain), $"{biome}_{terrain}", AutoTileGrid.Columns * AutoTileGrid.Rows);

        /// <summary>The 8 side-face Tile assets (0-3 W frames, 4-7 E frames), or null for a biome without a new side sheet.</summary>
        public static Tile[] EnsureSideTiles(string biome)
        {
            string p = SidePath(biome);
            return p == null ? null : TilesFor(p, $"{biome}_sidewall", 8);
        }

        public static int EnsureAll()
        {
            int n = 0;
            foreach (var d in Sheets) if (EnsureTiles(d.biome, d.terrain) != null) n++;
            foreach (var s in SideSheets) if (EnsureSideTiles(s.biome) != null) n++;
            AssetDatabase.SaveAssets();
            return n;
        }

        // ------------------------------------------------------------------ display maps

        /// <summary>
        /// Fills the display Tilemap <c>Auto_&lt;terrain&gt;</c> under <paramref name="parent"/> (created at local (-0.5, -0.5, 0) if
        /// missing; parent must be under a Grid) from the data cells (tilemap cell coordinates of the parent's grid).
        /// </summary>
        public static Tilemap BuildDisplay(Transform parent, string biome, string terrain, ICollection<Vector2Int> data, int order = -1)
        {
            var tiles = EnsureTiles(biome, terrain);
            if (tiles == null) return null;
            string name = "Auto_" + terrain;
            var tr = parent.Find(name);
            Tilemap tm;
            if (tr == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                tm = go.AddComponent<Tilemap>();
                go.AddComponent<TilemapRenderer>();
                tr = go.transform;
            }
            else
            {
                tm = tr.GetComponent<Tilemap>();
                if (tm == null) tm = tr.gameObject.AddComponent<Tilemap>();
            }
            tr.localPosition = new Vector3(-0.5f, -0.5f, 0f);
            var rend = tm.GetComponent<TilemapRenderer>();
            if (rend == null) rend = tm.gameObject.AddComponent<TilemapRenderer>();
            rend.sortingLayerName = Layer;
            rend.sortingOrder = order >= 0 ? order : DefaultOrder(terrain);
            tm.ClearAllTiles();
            var idx = AutoTileGrid.Display(data);
            var pos = new List<Vector3Int>(idx.Count);
            var til = new List<TileBase>(idx.Count);
            foreach (var kv in idx)
            {
                int slot = AutoTileGrid.Slot(kv.Value, kv.Key.x, kv.Key.y);
                if (slot < 0) continue;
                pos.Add(new Vector3Int(kv.Key.x, kv.Key.y, 0));
                til.Add(tiles[slot]);
            }
            tm.SetTiles(pos.ToArray(), til.ToArray());
            return tm;
        }

        /// <summary>Refreshes one hand-painted data layer's display map; the data map's renderer is switched off.</summary>
        public static Tilemap Refresh(AutoTileLayer layer)
        {
            var data = layer.GetComponent<Tilemap>();
            var cells = new HashSet<Vector2Int>();
            data.CompressBounds();
            foreach (var p in data.cellBounds.allPositionsWithin) if (data.HasTile(p)) cells.Add(new Vector2Int(p.x, p.y));
            var r = data.GetComponent<TilemapRenderer>();
            if (r != null) r.enabled = false;
            var parent = layer.transform.parent != null ? layer.transform.parent : layer.transform;
            return BuildDisplay(parent, layer.biome, layer.terrain, cells, layer.sortingOrder);
        }

        public static int RefreshAll()
        {
            int n = 0;
            foreach (var l in Object.FindObjectsByType<AutoTileLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (Refresh(l) != null) n++;
            return n;
        }

        [MenuItem("Idle Grounds/World/Rebuild Auto Tiles")]
        public static void RebuildMenu()
        {
            int sheets = EnsureAll();
            int layers = RefreshAll();
            if (layers > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[IdleGrounds] Auto tiles: {sheets} sheet(s) ready, {layers} painted layer(s) refreshed.");
        }

        /// <summary>Live refresh while hand painting a data map in the editor (not in Play mode).</summary>
        [InitializeOnLoad]
        static class LiveRefresh
        {
            static readonly HashSet<AutoTileLayer> pending = new HashSet<AutoTileLayer>();
            static LiveRefresh() { Tilemap.tilemapTileChanged += OnChanged; }

            static void OnChanged(Tilemap tm, Tilemap.SyncTile[] changes)
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || tm == null) return;
                var l = tm.GetComponent<AutoTileLayer>();
                if (l == null || !pending.Add(l)) return;
                EditorApplication.delayCall += () =>
                {
                    pending.Remove(l);
                    if (l != null) Refresh(l);
                };
            }
        }

        // ------------------------------------------------------------------ preview island (art review; never saved)

        const int PW = 36, PH = 24;

        static float ValueNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(x0, y0, seed), b = Hash(x0 + 1, y0, seed), c = Hash(x0, y0 + 1, seed), d = Hash(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static void Line(HashSet<Vector2Int> s, Vector2Int a, Vector2Int b, int thick)
        {
            // 4-connected walk (x first, then y alternately) so a one-cell path never joins by a corner only
            var p = a;
            void Put() { for (int t = 0; t < thick; t++) s.Add(new Vector2Int(p.x, p.y - t)); }
            Put();
            while (p != b)
            {
                int dx = b.x - p.x, dy = b.y - p.y;
                if (Mathf.Abs(dx) * 1f / Mathf.Max(1, Mathf.Abs(b.x - a.x)) >= Mathf.Abs(dy) * 1f / Mathf.Max(1, Mathf.Abs(b.y - a.y)) && dx != 0) p.x += System.Math.Sign(dx);
                else p.y += System.Math.Sign(dy);
                Put();
            }
        }

        /// <summary>
        /// A small preview Island for <paramref name="biome"/> (36 x 24 cells, centred on <paramref name="centre"/>): auto ground with
        /// a hole, overlay clusters (noise mask, 2 x 2 opening), a pond (biomes with a water sheet), a path (1 and 2 cells wide)
        /// and a W / E side-face column stacked by <see cref="AutoTileGrid.SideFaceFrame"/>. In memory only (review shots).
        /// </summary>
        public static GameObject BuildPreview(string biome, Vector2 centre)
        {
            string rootName = "AutoTilePreview_" + biome;
            var old = GameObject.Find(rootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(rootName, typeof(Grid));
            root.transform.position = new Vector3(Mathf.Round(centre.x - PW / 2f), Mathf.Round(centre.y - PH / 2f), 0f);
            int seed = biome.Length * 977 + biome[0];

            var ground = new HashSet<Vector2Int>();
            for (int y = 0; y < PH; y++)
                for (int x = 0; x < PW; x++)
                {
                    float dx = (x + 0.5f - PW / 2f) / (PW / 2f - 1.5f), dy = (y + 0.5f - PH / 2f) / (PH / 2f - 1.5f);
                    float ang = Mathf.Atan2(dy, dx);
                    float rad = 1f + 0.07f * Mathf.Sin(3 * ang + seed) + 0.05f * Mathf.Sin(5 * ang + 1.7f);
                    if (dx * dx + dy * dy <= rad * rad) ground.Add(new Vector2Int(x, y));
                }
            for (int y = 15; y < 17; y++) for (int x = 6; x < 9; x++) ground.Remove(new Vector2Int(x, y));   // a hole: inner ground edges

            var water = new HashSet<Vector2Int>();
            bool hasWater = Def(biome, "water") != null;
            if (hasWater)
                for (int y = 0; y < PH; y++)
                    for (int x = 0; x < PW; x++)
                    {
                        float dx = (x + 0.5f - 25.5f) / 4.6f, dy = (y + 0.5f - 8f) / 3.2f;
                        if (dx * dx + dy * dy <= 1f) water.Add(new Vector2Int(x, y));
                    }

            var path = new HashSet<Vector2Int>();
            Line(path, new Vector2Int(2, 12), new Vector2Int(10, 14), 1);
            Line(path, new Vector2Int(10, 14), new Vector2Int(17, 11), 2);
            Line(path, new Vector2Int(17, 11), new Vector2Int(33, 13), 1);
            Line(path, new Vector2Int(19, 11), new Vector2Int(21, hasWater ? 9 : 4), 1);
            path.RemoveWhere(p => water.Contains(p) || !ground.Contains(p));

            string overlay = null;
            foreach (var t in Terrains(biome)) if (IsOverlay(t)) overlay = t;
            var over = new HashSet<Vector2Int>();
            if (overlay != null)
            {
                foreach (var p in ground)
                {
                    if (path.Contains(p) || water.Contains(p)) continue;
                    bool nearWater = false;
                    foreach (var w in water) if (Mathf.Abs(w.x - p.x) <= 1 && Mathf.Abs(w.y - p.y) <= 1) { nearWater = true; break; }
                    if (nearWater) continue;
                    if (ValueNoise(p.x / 4.5f, p.y / 4.5f, seed + 31) > 0.52f) over.Add(p);
                }
                over = AutoTileGrid.Open2x2(over);
            }

            BuildDisplay(root.transform, biome, "ground", ground);
            if (overlay != null) BuildDisplay(root.transform, biome, overlay, over);
            if (hasWater) BuildDisplay(root.transform, biome, "water", water);
            BuildDisplay(root.transform, biome, "path", path);

            var side = EnsureSideTiles(biome);
            if (side != null)
            {
                var go = new GameObject("SideFaceDemo");
                go.transform.SetParent(root.transform, false);
                var tm = go.AddComponent<Tilemap>();
                var r = go.AddComponent<TilemapRenderer>();
                r.sortingLayerName = Layer; r.sortingOrder = 7;
                for (int y = 4; y < 20; y++)
                {
                    int k = AutoTileGrid.SideFaceFrameAtTilemapY(y);
                    tm.SetTile(new Vector3Int(PW + 1, y, 0), side[k]);
                    tm.SetTile(new Vector3Int(PW + 2, y, 0), side[4 + k]);
                }
            }
            Debug.Log($"[AutoTile] preview {biome} at {root.transform.position}: ground {ground.Count}, {overlay ?? "no overlay"} {over.Count}, water {water.Count}, path {path.Count}, side {(side != null ? "yes" : "no")}");
            return root;
        }
    }
}
