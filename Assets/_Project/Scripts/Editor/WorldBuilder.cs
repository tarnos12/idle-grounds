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
    /// Builds the static world of floating Islands (ADR 0003) in the active scene: World/Grid/Island_&lt;key&gt;
    /// each with the ground Rule-Tile tilemap, the cliff-rim row under its bottom edge, underside rock
    /// decorations + base mist, zone markers, the veil (fog) and a small label. Positions: an existing
    /// Island_&lt;key&gt; keeps the position it has in the scene (the scene is the authority — designers move
    /// Islands in the Scene view); new ones start at the config default offsets (WORLD.islands col/row).
    /// Idempotent: the "World" object is replaced. Placeholder art comes from <see cref="IslandArtBuilder"/>.
    /// </summary>
    public static class WorldBuilder
    {
        const string TileDir = "Assets/_Project/Art/Tiles";
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";
        public const string SkyLayer = "Sky";
        const int TileTexSize = 32;

        struct IslandSpec
        {
            public string key, label;
            public int col, row;          // fallback default offset (cells) when the config has none
            public string ground;
            public string tint;           // underside rock tint
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

        struct CoastKeep
        {
            public BoundsInt bounds;
            public TileBase[] tiles;
            public int seed;
        }

        /// <summary>Underside rock tint of an Island (see <see cref="IslandCoastBuilder"/>).</summary>
        public static Color UndersideTint(string key)
        {
            foreach (var s in Specs)
                if (s.key == key) { ColorUtility.TryParseHtmlString(s.tint, out var c); return c; }
            return new Color(0.54f, 0.48f, 0.4f);
        }

        static readonly IslandSpec[] Specs =
        {
            new IslandSpec { key = "farm", label = "Farm", col = 0, row = 6, ground = "#3a3318", tint = "#8a7550", zones = new[] {
                ("centre", "noBuild/spawner"), ("midLeft", "noBuild"), ("sandField", "generator:sand") } },
            new IslandSpec { key = "center", label = "Center", col = 123, row = 0, ground = "#25351f", tint = "#8a7a66", unlocked = true, zones = new[] {
                ("cornerTL", "noBuild"), ("cornerTR", "noBuild/enemy:Fox Spirit"), ("cornerBL", "noBuild/fixture:quarry"),
                ("cornerBR", "noBuild"), ("midTop", "noBuild/fixture:spirittree"), ("centre", "spawner:bush"),
                ("clayField", "generator:clay"), ("quarryField", "generator:stone"), ("woodField", "generator:wood") } },
            new IslandSpec { key = "mine", label = "Mine", col = 249, row = 4, ground = "#2c2c33", tint = "#6d6d78", zones = new[] {
                ("centre", "noBuild/spawner") } },
            new IslandSpec { key = "grove", label = "Spirit Grove", col = 8, row = 131, ground = "#1e3a2b", tint = "#5f6650", zones = new[] {
                ("centre", "noBuild/spawner") } },
            new IslandSpec { key = "fishing", label = "Fishing", col = 125, row = 126, ground = "#16323b", tint = "#5d7480", zones = new[] {
                ("centre", "noBuild/spawner"), ("cornerTL", "fixture:spring"), ("springField", "generator:water") } },
            new IslandSpec { key = "volcano", label = "Volcano", col = 252, row = 122, ground = "#3a1c17", tint = "#4a3438", zones = new[] {
                ("centre", "noBuild/spawner") } },
            new IslandSpec { key = "celestial", label = "Celestial Peak", col = 127, row = 252, ground = "#231d40", tint = "#c9c4e6", zones = new[] {
                ("centre", "noBuild/spawner") } },
        };

        [MenuItem("Idle Grounds/World/Build Islands")]
        public static void BuildIslands()
        {
            EnsureTiles();
            EnsureSkySortingLayer();
            IslandArtBuilder.EnsureArt();

            // the scene is the authority: remember where existing Islands float before rebuilding
            var keep = new Dictionary<string, Vector3>();
            foreach (var isl in Object.FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!string.IsNullOrEmpty(isl.islandKey) && isl.name == "Island_" + isl.islandKey)     // not legacy Region_* grid objects
                    keep[isl.islandKey] = isl.transform.position;

            // the painted Coast is authored scene data: capture it so the rebuild restores it untouched
            var keepCoasts = new Dictionary<string, CoastKeep>();
            foreach (var isl in Object.FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(isl.islandKey) || isl.name != "Island_" + isl.islandKey || isl.coast == null) continue;
                isl.coast.CompressBounds();
                var b = isl.coast.cellBounds;
                keepCoasts[isl.islandKey] = new CoastKeep { bounds = b, tiles = b.size.x > 0 && b.size.y > 0 ? isl.coast.GetTilesBlock(b) : null, seed = isl.coastSeed };
            }

            var existing = GameObject.Find("World");
            if (existing != null) Object.DestroyImmediate(existing);

            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            var cfg = db != null ? db.BuildConfig() : null;

            var world = new GameObject("World");
            var grid = new GameObject("Grid");
            grid.transform.SetParent(world.transform, false);
            var g = grid.AddComponent<Grid>();
            g.cellSize = Vector3.one;

            foreach (var s in Specs)
            {
                var def = cfg?.Region(s.key);
                Vector3 pos = keep.TryGetValue(s.key, out var kp) ? kp
                    : def != null ? new Vector3(def.islandCol, -def.islandRow, 0f) : new Vector3(s.col, -s.row, 0f);
                BuildIsland(s, grid.transform, pos, keepCoasts.TryGetValue(s.key, out var kc) ? kc : (CoastKeep?)null);
            }
            CoreLoopBuilder.EnsureSortingLayers();
            CoreLoopBuilder.ApplyWorldSortingLayers();
            // paint a coast only where none exists, then derive rim / underside / veil / boundary from it
            IslandCoastBuilder.GenerateMissing();

            EditorUtility.SetDirty(world);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[IdleGrounds] World built: " + Specs.Length + " Islands.");
        }

        /// <summary>The "Sky" sorting layer, inserted at the very back (before Default): sky, clouds, undersides.</summary>
        public static void EnsureSkySortingLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == SkyLayer) return;
            layers.InsertArrayElementAtIndex(0);
            var e = layers.GetArrayElementAtIndex(0);
            e.FindPropertyRelative("name").stringValue = SkyLayer;
            e.FindPropertyRelative("uniqueID").uintValue = (uint)(Animator.StringToHash("SortingLayer_" + SkyLayer) & 0x7FFFFFFF);
            var locked = e.FindPropertyRelative("locked");
            if (locked != null && locked.propertyType == SerializedPropertyType.Boolean) locked.boolValue = false;
            else if (locked != null) locked.intValue = 0;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        static float H01(string key, int i)
        {
            unchecked
            {
                int h = 17;
                foreach (char ch in key) h = h * 31 + ch;
                h = h * 7919 + i * 104729;
                return (Mathf.Abs(h) % 1000) / 1000f;
            }
        }

        internal static SpriteRenderer Deco(Transform parent, string name, Sprite s, Vector3 local, float scale, Color tint, int order, string layer)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = local;
            sr.transform.localScale = new Vector3(scale, scale, 1f);
            sr.sprite = s;
            sr.color = tint;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            return sr;
        }

        static void BuildIsland(IslandSpec s, Transform parent, Vector3 position, CoastKeep? keepCoast)
        {
            int n = Island.Cells;
            var go = new GameObject("Island_" + s.key);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var island = go.AddComponent<Island>();
            island.islandKey = s.key;
            island.unlocked = s.unlocked;

            // Coast: the visual-only landmass (square + irregular margin, 47-blob Rule Tile), drawn UNDER the
            // playable ground. Painted by IslandCoastBuilder; kept as authored scene data on rebuilds.
            var coast = IslandCoastBuilder.EnsureCoastTilemap(island);

            // Ground tilemap: the 93x93 playable area, centre-fill Rule Tile only (the coast blob draws the edges).
            var tmGo = new GameObject("Ground");
            tmGo.transform.SetParent(go.transform, false);
            var tilemap = tmGo.AddComponent<Tilemap>();
            var tr = tmGo.AddComponent<TilemapRenderer>();
            tr.sortingOrder = 0;
            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(IslandArtBuilder.GroundTilePath(s.key));
            if (tile == null) tile = AssetDatabase.LoadAssetAtPath<Tile>($"{TileDir}/Ground_{s.key}.asset");
            var tiles = new TileBase[n * n];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = tile;
            // Cell (col,row) -> tilemap cell (col, -row-1).
            tilemap.SetTilesBlock(new BoundsInt(0, -n, 0, n, n, 1), tiles);
            island.tilemap = tilemap;

            if (keepCoast != null)
            {
                island.coastSeed = keepCoast.Value.seed;
                if (keepCoast.Value.tiles != null) coast.SetTilesBlock(keepCoast.Value.bounds, keepCoast.Value.tiles);
            }

            // Cliff rim + Underside (+ mist) are DERIVED from the Coast outline by IslandCoastBuilder.RebuildDerived.
            var cliffGo = new GameObject("CliffRim");
            cliffGo.transform.SetParent(go.transform, false);
            cliffGo.AddComponent<Tilemap>();
            cliffGo.AddComponent<TilemapRenderer>().sortingOrder = -1;
            new GameObject("Underside").transform.SetParent(go.transform, false);

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

            // Veil (enabled while locked): fog masked to the whole landmass; sized/masked by IslandCoastBuilder.RebuildDerived.
            var veil = new GameObject("Veil");
            veil.transform.SetParent(go.transform, false);
            veil.AddComponent<SpriteRenderer>();
            veil.SetActive(!s.unlocked);
            island.veil = veil;

            // Label: small and subtle, just above the Island's top edge.
            var label = new GameObject("Label");
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(n * 0.5f, 1.1f, 0f);
            var t = label.AddComponent<TextMeshPro>();
            t.text = s.label;
            t.fontSize = 14f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.9f, 0.93f, 0.95f, 0.6f);
            t.fontStyle = FontStyles.Bold;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(30f, 2f);
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
        /// Builds each Island's "ZoneOverlay" child from the GameDatabase config: no-build zones (green 5% +
        /// dashed 18% edge), generator fields (FIELD_TINT by item), the enemy zone (red 7% / 30%), all 1 px
        /// dashed edges, and the 2 px region frame rgba(74,222,128,.30) — SpriteRenderers on the Ground
        /// sorting layer above the tilemap (under link lines / reach circles). Also puts the 64 px 🔒 at the
        /// centre of every veil. The rects sit under a "Tints" child that <see cref="PlacementGuide"/> shows only while
        /// placing / demolishing (ADR 0005: at rest the painted dressing shows the zones). Idempotent (old overlay / lock replaced).
        /// </summary>
        public static void BuildZoneOverlays(GameDatabase db)
        {
            if (db == null) { Debug.LogError("[IdleGrounds] BuildZoneOverlays: no GameDatabase."); return; }
            BuildingsBuilder.EnsureShapes();
            var square = BuildingsBuilder.square;
            var cfg = db.BuildConfig();
            int n = 0;
            foreach (var region in Object.FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var old = region.transform.Find(OverlayName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var def = cfg.Region(region.islandKey);
                if (def == null) continue;
                var root = new GameObject(OverlayName).transform;
                root.SetParent(region.transform, false);
                // ADR 0005: the resting look is the painted dressing (IslandDressingBuilder); the flat tints + dashed
                // edges only show while placing / demolishing (PlacementGuide), when build limits matter
                var tints = new GameObject("Tints").transform;
                tints.SetParent(root, false);

                foreach (var z in def.noBuild)
                    foreach (var r in cfg.ZoneRects(z)) AddZoneRect(tints, "NoBuild_" + z, r, ZoneFill, ZoneEdge, square);
                foreach (var gen in def.generators)
                {
                    var tint = gen.item != null && FieldTint.TryGetValue(gen.item, out var t) ? t : FieldTint["sand"];
                    foreach (var r in cfg.ZoneRects(gen.zone)) AddZoneRect(tints, "Field_" + gen.item + "_" + gen.zone, r, tint.fill, tint.edge, square);
                }
                var ez = def.enemies;
                if (ez != null && ez.enabled && !string.IsNullOrEmpty(ez.zone))
                    foreach (var r in cfg.ZoneRects(ez.zone)) AddZoneRect(tints, "Enemy_" + ez.zone, r, EnemyFill, EnemyEdge, square);

                tints.gameObject.SetActive(false);
                root.gameObject.AddComponent<PlacementGuide>().guide = tints.gameObject;

                if (region.veil != null) BuildVeilLock(region.veil.transform);
                EditorUtility.SetDirty(region.gameObject);
                n++;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[IdleGrounds] Zone overlays built for " + n + " Islands.");
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
            // the veil spans the whole irregular landmass: keep the lock at the play area's centre
            var isl = veil.parent;
            sr.transform.position = isl != null ? isl.TransformPoint(new Vector3(Island.Cells * 0.5f, -Island.Cells * 0.5f, 0f)) : veil.position;
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
