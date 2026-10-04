using System.Collections.Generic;
using System.IO;
using IdleGrounds.Game;
using IdleGrounds.Game.Data;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Builds the static world (World/Grid/Region_* with tilemaps, zone markers, veils, labels) in the
    /// active scene. Idempotent: an existing "World" object is replaced.
    /// </summary>
    public static class WorldBuilder
    {
        const string TileDir = "Assets/_Project/Art/Tiles";
        const int TileTexSize = 32;

        struct RegionSpec
        {
            public string key, label;
            public int rx, ry;
            public string ground;
            public bool unlocked;
            public (string zone, string role)[] zones;
        }

        // Zone rectangles (inclusive r0,c0,r1,c1) from data-catalog section 4.
        static readonly Dictionary<string, CellRect> ZoneRects = new Dictionary<string, CellRect>
        {
            { "cornerTL", new CellRect(0, 0, 24, 24) },
            { "cornerTR", new CellRect(0, 68, 24, 92) },
            { "cornerBL", new CellRect(68, 0, 92, 24) },
            { "cornerBR", new CellRect(68, 68, 92, 92) },
            { "centre", new CellRect(25, 25, 67, 67) },
            { "midTop", new CellRect(0, 25, 24, 67) },
            { "midLeft", new CellRect(25, 0, 67, 24) },
            { "clayField", new CellRect(76, 76, 84, 84) },
            { "quarryField", new CellRect(76, 8, 84, 16) },
            { "springField", new CellRect(8, 8, 16, 16) },
            { "woodField", new CellRect(12, 40, 20, 48) },
            { "sandField", new CellRect(42, 14, 50, 22) },
        };

        static readonly RegionSpec[] Specs =
        {
            new RegionSpec { key = "farm", label = "Farm", rx = 0, ry = 0, ground = "#3a3318", zones = new[] {
                ("centre", "noBuild/spawner"), ("midLeft", "noBuild"), ("sandField", "generator:sand") } },
            new RegionSpec { key = "center", label = "Center", rx = 1, ry = 0, ground = "#25351f", unlocked = true, zones = new[] {
                ("cornerTL", "noBuild"), ("cornerTR", "noBuild/enemy:Fox Spirit"), ("cornerBL", "noBuild/fixture:quarry"),
                ("cornerBR", "noBuild"), ("midTop", "noBuild/fixture:spirittree"), ("centre", "spawner:bush"),
                ("clayField", "generator:clay"), ("quarryField", "generator:stone"), ("woodField", "generator:wood") } },
            new RegionSpec { key = "mine", label = "Mine", rx = 2, ry = 0, ground = "#2c2c33", zones = new[] {
                ("centre", "noBuild/spawner") } },
            new RegionSpec { key = "grove", label = "Spirit Grove", rx = 0, ry = 1, ground = "#1e3a2b", zones = new[] {
                ("centre", "noBuild/spawner") } },
            new RegionSpec { key = "fishing", label = "Fishing", rx = 1, ry = 1, ground = "#16323b", zones = new[] {
                ("centre", "noBuild/spawner"), ("cornerTL", "fixture:spring"), ("springField", "generator:water") } },
            new RegionSpec { key = "volcano", label = "Volcano", rx = 2, ry = 1, ground = "#3a1c17", zones = new[] {
                ("centre", "noBuild/spawner") } },
            new RegionSpec { key = "celestial", label = "Celestial Peak", rx = 1, ry = 2, ground = "#231d40", zones = new[] {
                ("centre", "noBuild/spawner") } },
        };

        [MenuItem("Idle Grounds/World/Build Regions")]
        public static void BuildRegions()
        {
            EnsureTiles();
            var whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TileDir + "/white.png");

            var existing = GameObject.Find("World");
            if (existing != null) Object.DestroyImmediate(existing);

            var world = new GameObject("World");
            var grid = new GameObject("Grid");
            grid.transform.SetParent(world.transform, false);
            var g = grid.AddComponent<Grid>();
            g.cellSize = Vector3.one;

            foreach (var s in Specs)
                BuildRegion(s, grid.transform, whiteSprite);
            CoreLoopBuilder.EnsureSortingLayers();
            CoreLoopBuilder.ApplyWorldSortingLayers();

            EditorUtility.SetDirty(world);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[IdleGrounds] World built: " + Specs.Length + " regions.");
        }

        static void BuildRegion(RegionSpec s, Transform parent, Sprite white)
        {
            var go = new GameObject("Region_" + s.key);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(s.rx * Region.Stride, -s.ry * Region.Stride, 0f);

            var region = go.AddComponent<Region>();
            region.regionKey = s.key;
            region.rx = s.rx;
            region.ry = s.ry;
            region.unlocked = s.unlocked;

            // Ground tilemap.
            var tmGo = new GameObject("Ground");
            tmGo.transform.SetParent(go.transform, false);
            var tilemap = tmGo.AddComponent<Tilemap>();
            var tr = tmGo.AddComponent<TilemapRenderer>();
            tr.sortingOrder = 0;
            var tile = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/Ground_{s.key}.asset");
            var tiles = new TileBase[Region.Cells * Region.Cells];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = tile;
            // Cell (col,row) -> tilemap cell (col, -row-1).
            tilemap.SetTilesBlock(new BoundsInt(0, -Region.Cells, 0, Region.Cells, Region.Cells, 1), tiles);
            region.tilemap = tilemap;

            // Zones.
            var zonesGo = new GameObject("Zones");
            zonesGo.transform.SetParent(go.transform, false);
            foreach (var (zone, role) in s.zones)
            {
                var zGo = new GameObject(zone);
                zGo.transform.SetParent(zonesGo.transform, false);
                var zm = zGo.AddComponent<ZoneMarker>();
                zm.zoneName = zone;
                zm.role = role;
                zm.color = ZoneColor(role);
                zm.rects.Add(ZoneRects[zone]);
            }

            // Veil (enabled when locked).
            var veil = new GameObject("Veil");
            veil.transform.SetParent(go.transform, false);
            veil.transform.localPosition = new Vector3(Region.Cells * 0.5f, -Region.Cells * 0.5f, 0f);
            veil.transform.localScale = new Vector3(Region.Cells, Region.Cells, 1f);
            var sr = veil.AddComponent<SpriteRenderer>();
            sr.sprite = white;
            sr.color = new Color(0f, 0f, 0f, 0.55f);
            sr.sortingOrder = 100;
            veil.SetActive(!s.unlocked);
            region.veil = veil;

            // Label.
            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(Region.Cells * 0.5f, 4f, 0f);
            var t = label.AddComponent<TextMeshPro>();
            t.text = s.label;
            t.fontSize = 48;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.9f, 0.93f, 0.95f);
            t.fontStyle = FontStyles.Bold;
            t.rectTransform.sizeDelta = new Vector2(40f, 8f);
            t.GetComponent<MeshRenderer>().sortingOrder = 101;
            t.ForceMeshUpdate();
        }

        // ---- in-game zone overlays (ui.js drawRegionGround U:767) ----

        static Color Rgba(int r, int g, int b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);
        static readonly Color ZoneFill = Rgba(74, 222, 128, 0.05f), ZoneEdge = Rgba(74, 222, 128, 0.18f);
        static readonly Color EnemyFill = Rgba(248, 113, 113, 0.07f), EnemyEdge = Rgba(248, 113, 113, 0.30f);
        static readonly Color FrameColour = Rgba(74, 222, 128, 0.30f);
        /// <summary>FIELD_TINT (ui.js:54): generator-field [fill, edge] per produced item; unknown = sand.</summary>
        static readonly Dictionary<string, (Color fill, Color edge)> FieldTint = new Dictionary<string, (Color, Color)>
        {
            { "clay", (Rgba(184, 115, 66, 0.30f), Rgba(220, 190, 120, 0.4f)) },
            { "sand", (Rgba(226, 201, 126, 0.28f), Rgba(220, 190, 120, 0.4f)) },
            { "stone", (Rgba(148, 163, 184, 0.22f), Rgba(148, 163, 184, 0.4f)) },
            { "water", (Rgba(96, 165, 250, 0.28f), Rgba(96, 165, 250, 0.45f)) },
        };
        public const string OverlayName = "ZoneOverlay";
        const string OverlayLayer = "Ground";
        const int FillOrder = 10, EdgeOrder = 11, FrameOrder = 12;

        /// <summary>
        /// Builds each Region's "ZoneOverlay" child from the GameDatabase config: no-build zones (green 5% +
        /// dashed 18% edge), generator fields (FIELD_TINT by item), the enemy zone (red 7% / 30%), all 1 px
        /// dashed edges, and the 2 px region frame rgba(74,222,128,.30) — SpriteRenderers on the Ground
        /// sorting layer above the tilemap (under link lines / reach circles). Also puts the 64 px 🔒 at the
        /// centre of every veil. Idempotent (old overlay / lock replaced).
        /// </summary>
        public static void BuildZoneOverlays(GameDatabase db)
        {
            if (db == null) { Debug.LogError("[IdleGrounds] BuildZoneOverlays: no GameDatabase."); return; }
            BuildingsBuilder.EnsureShapes();
            var square = BuildingsBuilder.square;
            var cfg = db.BuildConfig();
            int n = 0;
            foreach (var region in Object.FindObjectsByType<Region>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var old = region.transform.Find(OverlayName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var def = cfg.Region(region.regionKey);
                if (def == null) continue;
                var root = new GameObject(OverlayName).transform;
                root.SetParent(region.transform, false);

                foreach (var z in def.noBuild)
                    foreach (var r in cfg.ZoneRects(z)) AddZoneRect(root, "NoBuild_" + z, r, ZoneFill, ZoneEdge, square);
                foreach (var gen in def.generators)
                {
                    var tint = gen.item != null && FieldTint.TryGetValue(gen.item, out var t) ? t : FieldTint["sand"];
                    foreach (var r in cfg.ZoneRects(gen.zone)) AddZoneRect(root, "Field_" + gen.item + "_" + gen.zone, r, tint.fill, tint.edge, square);
                }
                var ez = def.enemies;
                if (ez != null && ez.enabled && !string.IsNullOrEmpty(ez.zone))
                    foreach (var r in cfg.ZoneRects(ez.zone)) AddZoneRect(root, "Enemy_" + ez.zone, r, EnemyFill, EnemyEdge, square);

                var frame = BuildingsBuilder.Frame(root, "RegionFrame", FrameOrder, OverlayLayer);
                frame.transform.localPosition = Vector3.zero;
                frame.Set(Region.Cells, Region.Cells, ViewKit.U(2f), FrameColour, false);

                if (region.veil != null) BuildVeilLock(region.veil.transform);
                EditorUtility.SetDirty(region.gameObject);
                n++;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[IdleGrounds] Zone overlays built for " + n + " regions.");
        }

        static void AddZoneRect(Transform root, string name, IdleGrounds.Sim.ZoneRect r, Color fill, Color edge, Sprite square)
        {
            float w = r.c1 - r.c0 + 1, h = r.r1 - r.r0 + 1;
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(r.c0, -r.r0, 0f);       // top-left of the rect
            var fillSr = new GameObject("Fill").AddComponent<SpriteRenderer>();
            fillSr.transform.SetParent(go.transform, false);
            fillSr.sprite = square;
            fillSr.color = fill;
            fillSr.sortingLayerName = OverlayLayer;
            fillSr.sortingOrder = FillOrder;
            fillSr.transform.localPosition = new Vector3(w * 0.5f, -h * 0.5f, 0f);
            fillSr.transform.localScale = new Vector3(w, h, 1f);
            var e = BuildingsBuilder.Frame(go.transform, "Edge", EdgeOrder, OverlayLayer);
            e.transform.localPosition = Vector3.zero;
            e.Set(w, h, ViewKit.U(1f), edge, true);                         // setLineDash([4, 4]), 1 px
        }

        /// <summary>drawRegionVeil: 64 px 🔒 at the region centre (child of the 93-unit veil quad).</summary>
        static void BuildVeilLock(Transform veil)
        {
            var old = veil.Find("Lock");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var s = CoreLoopBuilder.Emoji("ui_lock");
            if (s == null) return;
            var sr = new GameObject("Lock").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(veil, false);
            sr.sprite = s;
            sr.sortingLayerName = "Overlay";
            sr.sortingOrder = 2;
            float m = Mathf.Max(s.bounds.size.x, s.bounds.size.y);
            float k = ViewKit.U(64f) / Mathf.Max(0.0001f, m);
            var vs = veil.localScale;
            sr.transform.localPosition = Vector3.zero;
            sr.transform.localScale = new Vector3(k / Mathf.Max(0.0001f, vs.x), k / Mathf.Max(0.0001f, vs.y), 1f);
        }

        static Color ZoneColor(string role)
        {
            if (role.Contains("generator:clay")) return new Color(0.72f, 0.45f, 0.26f);
            if (role.Contains("generator:sand")) return new Color(0.89f, 0.79f, 0.49f);
            if (role.Contains("generator:stone")) return new Color(0.58f, 0.64f, 0.72f);
            if (role.Contains("generator:water")) return new Color(0.38f, 0.65f, 0.98f);
            if (role.Contains("generator:wood")) return new Color(0.76f, 0.59f, 0.35f);
            if (role.Contains("enemy")) return new Color(0.97f, 0.44f, 0.44f);
            if (role.Contains("fixture")) return new Color(0.98f, 0.75f, 0.14f);
            if (role.StartsWith("noBuild")) return new Color(0.29f, 0.87f, 0.50f);
            return new Color(0.6f, 0.9f, 0.9f);
        }

        // ---- tile assets ----

        public static void EnsureTiles()
        {
            Directory.CreateDirectory(TileDir);
            WritePng(TileDir + "/white.png", Color.white, 1, 1, 1, false);
            foreach (var s in Specs)
            {
                ColorUtility.TryParseHtmlString(s.ground, out var c);
                string png = $"{TileDir}/Ground_{s.key}.png";
                WritePng(png, c, TileTexSize, TileTexSize, TileTexSize, true);
                string assetPath = $"{TileDir}/Ground_{s.key}.asset";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, assetPath);
                }
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
            }
            AssetDatabase.SaveAssets();
        }

        static void WritePng(string path, Color c, int w, int h, int ppu, bool gridLine)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var line = Color.Lerp(c, Color.white, 0.05f); // faint cell grid like the original (white @ 5%)
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = (gridLine && (x == 0 || y == 0)) ? line : c;
            tex.SetPixels(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = ppu;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
    }
}
