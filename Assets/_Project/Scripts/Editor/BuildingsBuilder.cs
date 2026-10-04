using System.Collections.Generic;
using System.IO;
using IdleGrounds.Game;
using IdleGrounds.Game.Data;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static IdleGrounds.Editor.CoreLoopBuilder;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// M3 "Buildings & converters" view side, reproducible: shape sprites, the base Building prefab +
    /// one prefab VARIANT per family (Converter → Burner, Storehouse, WardingSeal, GatheringStone,
    /// FurnaceSpirit, WispLantern, Generator, Pavilion, AscensionGate, Altar, Dragon), the
    /// BuildingPrefabSet table, the PlacementGhost, the BuildMenu / RecipePicker / BuildingTooltip UI
    /// prefabs, and the scene wiring (BuildingViewSync, BuildController, HUD instances).
    /// </summary>
    public static class BuildingsBuilder
    {
        public const string BuildingPrefabDir = "Assets/_Project/Prefabs/Buildings";
        const string ShapesDir = "Assets/_Project/Art/Sprites/Shapes";
        const string PrefabSetPath = "Assets/_Project/Data/BuildingPrefabSet.asset";
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const string Layer = "Buildings";

        internal static Sprite square, dash, rounded, ring, circle;

        // ------------------------------------------------------------------ menus

        [MenuItem("Idle Grounds/Prefabs/Build Building Prefabs (M3)")]
        public static void BuildAllPrefabs()
        {
            EnsureSortingLayers();
            EnsureShapes();
            Directory.CreateDirectory(BuildingPrefabDir);
            Directory.CreateDirectory(WorldPrefabDir);
            Directory.CreateDirectory(UiPrefabDir);
            BuildBuildingPrefabs();
            BuildPlacementGhost();
            BuildBuildMenuPrefab();
            BuildRecipePickerPrefab();
            BuildTooltipPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[IdleGrounds] M3 building + UI prefabs built.");
        }

        [MenuItem("Idle Grounds/Scene/Install Buildings (M3)")]
        public static void InstallMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildAllPrefabs();
            InstallIntoActiveScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IdleGrounds] M3 buildings installed into " + ScenePath);
        }

        [MenuItem("Idle Grounds/Scene/Rebuild Core Loop + Buildings (M2+M3)")]
        public static void RebuildAll()
        {
            CoreLoopBuilder.InstallMenu();
            InstallMenu();
            LogisticsBuilder.InstallMenu();
            ProgressionBuilder.InstallMenu();
            MetaBuilder.InstallMenu();
        }

        // ------------------------------------------------------------------ shapes

        internal static void EnsureShapes()
        {
            Directory.CreateDirectory(ShapesDir);
            circle = EnsureCircleSprite();
            square = Shape("square", 4, 4, 4, (x, y) => 1f, Vector4.zero, false);
            dash = Shape("dash", 10, 2, 32, (x, y) => x < 6 ? 1f : 0f, Vector4.zero, true);
            rounded = Shape("rounded", 32, 32, 64, (x, y) =>
            {
                const float r = 10f;
                float cx = Mathf.Clamp(x + 0.5f, r, 32 - r), cy = Mathf.Clamp(y + 0.5f, r, 32 - r);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                return Mathf.Clamp01(r - d + 0.5f);
            }, new Vector4(12, 12, 12, 12), false);
            ring = Shape("ring_dashed", 256, 256, 256, (x, y) =>
            {
                float dx = x + 0.5f - 128f, dy = y + 0.5f - 128f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1.6f - Mathf.Abs(d - 126f));
                float ang = Mathf.Atan2(dy, dx) / (2 * Mathf.PI) + 0.5f;
                return (ang * 64f) % 1f < 0.55f ? a : 0f;
            }, Vector4.zero, false);
        }

        static Sprite Shape(string name, int w, int h, float ppu, System.Func<int, int, float> alpha, Vector4 border, bool repeat)
        {
            string path = ShapesDir + "/" + name + ".png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = new Color(1, 1, 1, alpha(x, y));
                tex.SetPixels(px);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            bool dirty = imp.textureType != TextureImporterType.Sprite || imp.spritePixelsPerUnit != ppu || imp.spriteBorder != border;
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect) dirty = true;
            if (dirty)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = ppu;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.spriteBorder = border;
                imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                imp.filterMode = name == "dash" || name == "square" ? FilterMode.Point : FilterMode.Bilinear;
                imp.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ------------------------------------------------------------------ world helpers

        internal static T Child<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        internal static Transform Node(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        internal static SpriteRenderer Sr(Transform parent, string name, int order, Sprite s = null, string layer = Layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            return sr;
        }

        internal static TextMeshPro Text(Transform parent, string name, float px, Color c, bool bold, int order,
            TextAlignmentOptions align = TextAlignmentOptions.Center, float widthUnits = 3f, bool ellipsis = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            t.text = "";
            t.fontSize = px * ViewKit.FontPerPx;
            t.color = c;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = ellipsis ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;   // LiberationSans has no "…" glyph
            t.rectTransform.sizeDelta = new Vector2(widthUnits, ViewKit.U(px * 1.4f));
            var mr = go.GetComponent<MeshRenderer>();
            mr.sortingLayerName = Layer;
            mr.sortingOrder = order;
            return t;
        }

        internal static EdgeFrame Frame(Transform parent, string name, int order, string layer = Layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<EdgeFrame>();
            foreach (var e in new[] { "top", "bottom", "left", "right" })
            {
                var sr = Sr(go.transform, char.ToUpper(e[0]) + e.Substring(1), order, square, layer);
                sr.drawMode = SpriteDrawMode.Tiled;
                Set(f, e, sr);
            }
            Set(f, "solid", square);
            Set(f, "dashed", dash);
            return f;
        }

        static IconCount IconCountNode(Transform parent, string name, int order, float px, bool withA, bool withB, Color c)
        {
            var root = Node(parent, name);
            var ic = root.gameObject.AddComponent<IconCount>();
            ic.icon = Sr(root, "Icon", order);
            if (withA) ic.a = Text(root, "A", px, c, true, order + 2, TextAlignmentOptions.Center, 1f);
            if (withB) ic.b = Text(root, "B", px, c, true, order + 2, TextAlignmentOptions.Center, 1f);
            return ic;
        }

        internal static IconRow IconRowNode(Transform parent, string name, int order)
        {
            var root = Node(parent, name);
            var row = root.gameObject.AddComponent<IconRow>();
            var tpl = IconCountNode(root, "Template", order, 10f, true, false, UiPalette.Gold);
            tpl.gameObject.SetActive(false);
            Set(row, "template", tpl);
            return row;
        }

        internal static void SetColor(Object target, string field, Color c)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).colorValue = c;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetEnum(Object target, string field, int v)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).enumValueIndex = v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ building prefabs

        static string P(string n) => BuildingPrefabDir + "/" + n + ".prefab";

        static GameObject BuildBase()
        {
            var root = new GameObject("Building");
            var view = root.AddComponent<BuildingView>();
            var panel = Sr(root.transform, "Panel", 0, square);
            var border = Frame(root.transform, "Border", 1);
            var hl = Frame(root.transform, "Highlight", 8);
            var icon = Sr(root.transform, "Icon", 3);
            var name = Text(root.transform, "Name", 10f, UiPalette.Text, true, 4, TextAlignmentOptions.Center, 3f, true);
            var needs = IconRowNode(root.transform, "Needs", 3);
            Set(view, "panel", panel);
            Set(view, "border", border);
            Set(view, "highlight", hl);
            Set(view, "icon", icon);
            Set(view, "nameText", name);
            Set(view, "needsRow", needs);
            return Save(root, P("Building"));
        }

        /// <summary>Instantiate a prefab, let <paramref name="add"/> add children/faces, save as a variant.</summary>
        static GameObject Variant(GameObject parent, string name, System.Action<GameObject> add)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(parent);
            inst.name = name;
            add(inst);
            var prefab = PrefabUtility.SaveAsPrefabAsset(inst, P(name));
            Object.DestroyImmediate(inst);
            return prefab;
        }

        static void AddConverter(GameObject go)
        {
            var face = Child<ConverterFaceView>(go.transform, "Converter");
            var t = face.transform;
            var nameIcon = Sr(t, "NameIcon", 3);
            var nameText = Text(t, "NameText", 9.5f, UiPalette.Muted, true, 4, TextAlignmentOptions.MidlineLeft, 2.4f, true);
            var inputs = Node(t, "Inputs");
            var tpl = IconCountNode(inputs, "InputTemplate", 3, 9f, true, true, UiPalette.Gold);
            tpl.gameObject.SetActive(false);
            var result = IconCountNode(t, "Result", 3, 10f, false, true, UiPalette.Gold);
            var status = Text(t, "Status", 9.5f, UiPalette.Accent, true, 4, TextAlignmentOptions.Center, 2.8f, true);
            var statusIcon = Sr(t, "StatusIcon", 3);
            var track = Sr(t, "ProgressTrack", 3, square);
            var fill = Sr(t, "ProgressFill", 4, square);
            Set(face, "nameIcon", nameIcon);
            Set(face, "nameText", nameText);
            Set(face, "inputsRoot", inputs);
            Set(face, "inputTemplate", tpl);
            Set(face, "result", result);
            Set(face, "statusText", status);
            Set(face, "statusIcon", statusIcon);
            Set(face, "progressTrack", track);
            Set(face, "progressFill", fill);
        }

        static void AddFuelRack(GameObject go)
        {
            var rack = Child<FuelRackView>(go.transform, "FuelRack");
            var t = rack.transform;
            var panel = Sr(t, "Panel", 0, square);
            var frame = Frame(t, "Frame", 1);
            var divs = new Object[3];
            for (int i = 0; i < 3; i++) divs[i] = Sr(t, "Divider" + i, 1, square);
            var ghost = Sr(t, "BurningGhost", 2);
            var slots = new Object[FuelRackView.Cols * FuelRackView.Rows];
            for (int i = 0; i < slots.Length; i++)
            {
                var sr = Sr(t, "Slot" + i, 3);
                if (i == 0) sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                slots[i] = sr;
            }
            var maskGo = new GameObject("BurnMask");
            maskGo.transform.SetParent(t, false);
            var mask = maskGo.AddComponent<SpriteMask>();
            mask.sprite = square;
            mask.isCustomRangeActive = true;
            mask.frontSortingLayerID = SortingLayer.NameToID(Layer);
            mask.frontSortingOrder = 3;
            mask.backSortingLayerID = SortingLayer.NameToID(Layer);
            mask.backSortingOrder = 2;
            var noFuel = Text(t, "NoFuel", 10f, UiPalette.Danger, true, 4);
            noFuel.text = "No fuel";
            Set(rack, "panel", panel);
            Set(rack, "frame", frame);
            SetArray(rack, "dividers", divs);
            SetArray(rack, "slots", slots);
            Set(rack, "burningGhost", ghost);
            Set(rack, "burnMask", mask);
            Set(rack, "noFuelText", noFuel);
        }

        static void AddStorehouse(GameObject go)
        {
            var f = Child<StorehouseFaceView>(go.transform, "Storehouse");
            Set(f, "itemIcon", Sr(f.transform, "ItemIcon", 3));
            Set(f, "label", Text(f.transform, "Label", 10f, UiPalette.Text, true, 4, TextAlignmentOptions.Center, 2.8f, true));
            Set(f, "lockIcon", Sr(f.transform, "Lock", 5, Emoji("ui_lock")));
        }

        static System.Action<GameObject> AddFormation(FormationFaceView.Kind kind)
        {
            return go =>
            {
                var f = Child<FormationFaceView>(go.transform, "Formation");
                SetEnum(f, "kind", (int)kind);
                Set(f, "icon", Sr(f.transform, "Icon", 3));
                Set(f, "itemIcon", Sr(f.transform, "ItemIcon", 3));
                Set(f, "badge", Text(f.transform, "Badge", 9f, UiPalette.Gold, true, 5, TextAlignmentOptions.Center, 2f));
                Set(f, "badgeIcon", Sr(f.transform, "BadgeIcon", 5, Emoji("ui_link")));
                if (kind == FormationFaceView.Kind.WispLantern)
                {
                    Set(f, "beatTrack", Sr(f.transform, "BeatTrack", 5, square));
                    Set(f, "beatFill", Sr(f.transform, "BeatFill", 6, square));
                }
                if (kind == FormationFaceView.Kind.GatheringStone || kind == FormationFaceView.Kind.FurnaceSpirit)
                {
                    var reach = Node(f.transform, "Reach");
                    // reach circles sit under every sprite (§2.1 step 3): Ground layer, above the tilemap
                    Set(f, "reach", reach);
                    Set(f, "reachFill", Sr(reach, "Fill", 50, circle, "Ground"));
                    Set(f, "reachRing", Sr(reach, "Ring", 51, ring, "Ground"));
                    reach.gameObject.SetActive(false);
                }
            };
        }

        static void AddGenerator(GameObject go)
        {
            var f = Child<GeneratorFaceView>(go.transform, "Generator");
            Set(f, "itemIcon", Sr(f.transform, "ItemIcon", 3));
            Set(f, "count", Text(f.transform, "Count", 10f, UiPalette.Gold, true, 4, TextAlignmentOptions.Center, 1.5f));
        }

        static void AddPavilion(GameObject go)
        {
            var f = Child<PavilionFaceView>(go.transform, "Pavilion");
            Set(f, "icon", Sr(f.transform, "Icon", 3));
            Set(f, "discipleIcon", Sr(f.transform, "DiscipleIcon", 3, Emoji("ui_disciple")));
            Set(f, "disciples", Text(f.transform, "Disciples", 11f, UiPalette.Text, true, 4, TextAlignmentOptions.Center, 1.5f));
            Set(f, "status", Text(f.transform, "Status", 9.5f, UiPalette.Muted, true, 4, TextAlignmentOptions.Center, 2.8f, true));
            Set(f, "barTrack", Sr(f.transform, "BunTrack", 3, square));
            Set(f, "barFill", Sr(f.transform, "BunFill", 4, square));
        }

        static void AddGate(GameObject go)
        {
            var f = Child<GateFaceView>(go.transform, "Gate");
            Set(f, "icon", Sr(f.transform, "Icon", 3));
            Set(f, "title", Text(f.transform, "Title", 16f, UiPalette.Gold, true, 4, TextAlignmentOptions.Center, 5f));
            Set(f, "sub", Text(f.transform, "Sub", 12f, UiPalette.Gold, true, 4, TextAlignmentOptions.Center, 5f));
            Set(f, "glow", Frame(f.transform, "Glow", 0));
        }

        static void AddAltar(GameObject go)
        {
            var view = go.GetComponent<BuildingView>();
            SetColor(view, "builtFill", ViewKit.Rgba(74, 60, 30, 0.95f));
            SetColor(view, "builtBorder", UiPalette.Gold);
            var f = Child<AltarFaceView>(go.transform, "Altar");
            Set(f, "icon", Sr(f.transform, "Icon", 3));
            Set(f, "title", Text(f.transform, "Title", 24f, UiPalette.Gold, true, 4, TextAlignmentOptions.Center, 5f));
            Set(f, "sub", Text(f.transform, "Sub", 12f, UiPalette.Muted, true, 4, TextAlignmentOptions.Center, 5f));
            Set(f, "jobRow", IconRowNode(f.transform, "JobRow", 3));     // M5: active upgrade job remaining cost
        }

        static void AddDragon(GameObject go)
        {
            var view = go.GetComponent<BuildingView>();
            SetColor(view, "builtFill", ViewKit.Rgba(52, 36, 70, 0.95f));
            SetColor(view, "builtBorder", UiPalette.Purple);
            var f = Child<DragonFaceView>(go.transform, "Dragon");
            Set(f, "icon", Sr(f.transform, "Icon", 3));
            Set(f, "title", Text(f.transform, "Title", 18f, UiPalette.Hex("#d8b4fe"), true, 4, TextAlignmentOptions.Center, 5f));
            Set(f, "sub", Text(f.transform, "Sub", 12f, UiPalette.Muted, true, 4, TextAlignmentOptions.Center, 5f));
            Set(f, "feedRow", IconRowNode(f.transform, "FeedRow", 3));
            Set(f, "sleepingSprite", Emoji("dragon_sleeping"));
            Set(f, "awakeSprite", Emoji("dragon_awake"));
            // M5 murmur bubble: 14 px #e9d5ff with the dark floater halo, above every building
            var murmur = Text(f.transform, "Murmur", 14f, UiPalette.Hex("#e9d5ff"), true, 20, TextAlignmentOptions.Bottom, 9f);
            murmur.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Fonts/LiberationSans SDF - Floater.mat") ?? murmur.fontSharedMaterial;
            murmur.GetComponent<MeshRenderer>().sortingLayerName = "Overlay";
            murmur.gameObject.SetActive(false);
            Set(f, "murmur", murmur);
        }

        static void BuildBuildingPrefabs()
        {
            var bas = BuildBase();
            var conv = Variant(bas, "Converter", AddConverter);
            var burner = Variant(conv, "Burner", AddFuelRack);
            var store = Variant(bas, "Storehouse", AddStorehouse);
            var seal = Variant(bas, "WardingSeal", AddFormation(FormationFaceView.Kind.WardingSeal));
            var stone = Variant(bas, "GatheringStone", AddFormation(FormationFaceView.Kind.GatheringStone));
            var spirit = Variant(bas, "FurnaceSpirit", AddFormation(FormationFaceView.Kind.FurnaceSpirit));
            var lantern = Variant(bas, "WispLantern", AddFormation(FormationFaceView.Kind.WispLantern));
            var gen = Variant(bas, "Generator", AddGenerator);
            var pav = Variant(bas, "Pavilion", AddPavilion);
            var gate = Variant(bas, "AscensionGate", AddGate);
            var altar = Variant(bas, "Altar", AddAltar);
            var dragon = Variant(bas, "Dragon", AddDragon);

            var set = AssetDatabase.LoadAssetAtPath<BuildingPrefabSet>(PrefabSetPath);
            if (set == null) { set = ScriptableObject.CreateInstance<BuildingPrefabSet>(); AssetDatabase.CreateAsset(set, PrefabSetPath); }
            set.fallback = bas; set.converter = conv; set.burner = burner; set.storehouse = store; set.wardingSeal = seal;
            set.gatheringStone = stone; set.furnaceSpirit = spirit; set.wispLantern = lantern; set.generator = gen;
            set.pavilion = pav; set.ascensionGate = gate; set.altar = altar; set.dragon = dragon;
            set.entries.Clear();
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (db != null)
                foreach (var b in db.buildings)
                    if (b != null) set.entries.Add(new BuildingPrefabSet.Entry { type = b.def.key, prefab = set.Family(b.def) ?? bas });
            EditorUtility.SetDirty(set);
        }

        static void BuildPlacementGhost()
        {
            var root = new GameObject("PlacementGhost");
            var pv = root.AddComponent<PlacementPreview>();
            const string L = "Overlay";
            Set(pv, "fill", Sr(root.transform, "Fill", 10, square, L));
            var frame = Frame(root.transform, "Frame", 11);
            foreach (var sr in frame.GetComponentsInChildren<SpriteRenderer>(true)) sr.sortingLayerName = L;
            Set(pv, "frame", frame);
            Set(pv, "icon", Sr(root.transform, "Icon", 12, null, L));
            Set(pv, "rackFill", Sr(root.transform, "RackFill", 10, square, L));
            var rf = Frame(root.transform, "RackFrame", 11);
            foreach (var sr in rf.GetComponentsInChildren<SpriteRenderer>(true)) sr.sortingLayerName = L;
            Set(pv, "rackFrame", rf);
            var reach = Node(root.transform, "Reach");
            Set(pv, "reach", reach);
            Set(pv, "reachFill", Sr(reach, "Fill", 8, circle, L));
            Set(pv, "reachRing", Sr(reach, "Ring", 9, ring, L));
            var pill = Sr(root.transform, "ReasonPill", 13, rounded, L);
            pill.drawMode = SpriteDrawMode.Sliced;
            pill.color = ViewKit.Rgba(40, 10, 10, 0.9f);
            Set(pv, "pill", pill);
            var txt = Text(root.transform, "Reason", 11f, UiPalette.Hex("#fecaca"), true, 14, TextAlignmentOptions.Center, 8f);
            txt.GetComponent<MeshRenderer>().sortingLayerName = L;
            Set(pv, "reasonText", txt);
            Save(root, WorldPrefabDir + "/PlacementGhost.prefab");
        }

        // ------------------------------------------------------------------ UI prefabs (1.5x CSS px at 1080p)

        internal static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        internal static VerticalLayoutGroup Col(GameObject go, int pad, float spacing, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            return v;
        }

        internal static ContentSizeFitter Fit(GameObject go, bool h, bool v)
        {
            var f = go.AddComponent<ContentSizeFitter>();
            f.horizontalFit = h ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = v ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return f;
        }

        static UiIconCount CostEntry(Transform parent, string name, float px, float icon)
        {
            var rt = NewUi(name, parent);
            Row(rt.gameObject, 0, 0, 3);
            var e = rt.gameObject.AddComponent<UiIconCount>();
            e.qty = Label(rt, "Qty", "1", px, UiPalette.Gold, true);
            e.icon = Icon(rt, "Icon", Emoji("item_wood"), icon);
            return e;
        }

        static void BuildBuildMenuPrefab()
        {
            var root = Stretch(NewUi("BuildMenu", null));
            var view = root.gameObject.AddComponent<BuildMenuView>();

            var panel = Chip(root, "Panel", new Color(UiPalette.Bg2.r, UiPalette.Bg2.g, UiPalette.Bg2.b, 0.97f), UiPalette.Line, raycast: true);
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(1f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.sizeDelta = new Vector2(-24f, 178f);
            prt.anchoredPosition = new Vector2(0f, 68f);

            var hint = Label(prt, "Hint", "", 18f, UiPalette.Muted, true);
            var hrt = hint.rectTransform;
            hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0f, 1f);
            hrt.sizeDelta = new Vector2(-28f, 26f);
            hrt.anchoredPosition = new Vector2(14f, -6f);

            var scrollRt = NewUi("Scroll", prt);
            scrollRt.anchorMin = Vector2.zero; scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(6f, 6f); scrollRt.offsetMax = new Vector2(-6f, -32f);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            var viewport = Stretch(NewUi("Viewport", scrollRt));
            viewport.gameObject.AddComponent<RectMask2D>();
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);       // raycast target for wheel scrolling
            var content = NewUi("Content", viewport);
            content.anchorMin = new Vector2(0f, 0f); content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = Vector2.zero;
            var h = Row(content.gameObject, 8, 6, 12);
            h.childForceExpandHeight = true;
            Fit(content.gameObject, true, false);
            scroll.viewport = viewport;
            scroll.content = content;

            // card template (first child of Content)
            var card = Chip(content, "CardTemplate", UiPalette.Panel, UiPalette.Line, raycast: true);
            var cardRt = card.rectTransform;
            var cg = card.gameObject.AddComponent<CanvasGroup>();
            var le = card.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 165f; le.preferredHeight = 120f;
            Col(card.gameObject, 10, 6, TextAnchor.MiddleLeft);
            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card;
            var cb = btn.colors; cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f); btn.colors = cb;
            var top = NewUi("Top", cardRt);
            Row(top.gameObject, 0, 0, 8);
            var cIcon = Icon(top, "Icon", Emoji("bld_workbench"), 36);
            var cName = Label(top, "Name", "Workbench", 19.5f, UiPalette.Text, true);
            var costRow = NewUi("Cost", cardRt);
            Row(costRow.gameObject, 0, 0, 8);
            var costTpl = CostEntry(costRow, "CostTemplate", 16.5f, 21f);
            var locked = Label(cardRt, "Locked", "", 15f, UiPalette.Muted, true);
            locked.textWrappingMode = TextWrappingModes.Normal;
            var badge = Chip(cardRt, "NewBadge", UiPalette.Gold, UiPalette.Gold);
            var brt = badge.rectTransform;
            badge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(1f, 1f);
            brt.sizeDelta = new Vector2(48f, 22f);
            brt.anchoredPosition = new Vector2(-6f, -6f);
            var bl = Label(brt, "Label", "new", 14f, UiPalette.Bg, true);
            Stretch(bl.rectTransform);
            bl.alignment = TextAlignmentOptions.Center;
            // quest / milestone target: 🎯 (.bc-tgt: top 3px, left 5px, 12px) + gold glow (box-shadow 0 0 0 2px rgba(251,191,36,.28))
            var tgt = Icon(cardRt, "TargetBadge", Emoji("ui_target"), 18f);
            var trt = tgt.rectTransform;
            tgt.GetComponent<LayoutElement>().ignoreLayout = true;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.sizeDelta = new Vector2(18f, 18f);
            trt.anchoredPosition = new Vector2(7f, -4f);
            tgt.gameObject.SetActive(false);
            var border = card.GetComponent<Outline>();
            var glow = card.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(251 / 255f, 191 / 255f, 36 / 255f, 0.28f);
            glow.effectDistance = new Vector2(4f, -4f);
            glow.enabled = false;
            var bc = card.gameObject.AddComponent<BuildCard>();
            Set(bc, "button", btn);
            Set(bc, "background", card);
            Set(bc, "border", border);
            Set(bc, "targetBadge", tgt.gameObject);
            Set(bc, "targetGlow", glow);
            Set(bc, "icon", cIcon);
            Set(bc, "nameText", cName);
            Set(bc, "costRow", costRow);
            Set(bc, "costTemplate", costTpl);
            Set(bc, "lockedText", locked);
            Set(bc, "newBadge", badge.gameObject);
            Set(bc, "group", cg);
            card.gameObject.SetActive(false);

            var empty = Label(prt, "Empty", "Nothing to build yet — follow the quests to unlock buildings.", 19f, UiPalette.Muted, true);
            Stretch(empty.rectTransform);
            empty.alignment = TextAlignmentOptions.Center;
            empty.gameObject.SetActive(false);

            Set(view, "panel", panel.gameObject);
            Set(view, "content", content);
            Set(view, "cardTemplate", bc);
            Set(view, "emptyText", empty);
            Set(view, "hintText", hint);
            panel.gameObject.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/BuildMenu.prefab");
        }

        static void BuildRecipePickerPrefab()
        {
            var root = Stretch(NewUi("RecipePicker", null));
            var view = root.gameObject.AddComponent<RecipePickerView>();

            var popup = Chip(root, "Popup", UiPalette.Bg2, UiPalette.Line, raycast: true);
            var prt = popup.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0f);
            Col(popup.gameObject, 21, 12, TextAnchor.UpperCenter);
            Fit(popup.gameObject, true, true);
            Label(prt, "Title", "Recipes", 21f, UiPalette.Text, true);
            var grid = NewUi("Grid", prt);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(87f, 87f);
            g.spacing = new Vector2(12f, 12f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 3;
            g.childAlignment = TextAnchor.UpperLeft;

            var cell = Chip(grid, "CellTemplate", UiPalette.Panel, UiPalette.Line, raycast: true);
            var glow = cell.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(UiPalette.Gold.r, UiPalette.Gold.g, UiPalette.Gold.b, 0.45f);
            glow.effectDistance = new Vector2(4f, -4f);
            glow.enabled = false;
            var cbtn = cell.gameObject.AddComponent<Button>();
            cbtn.targetGraphic = cell;
            var ci = Icon(cell.rectTransform, "Icon", Emoji("item_plank"), 48);
            var cirt = ci.rectTransform;
            cirt.anchorMin = cirt.anchorMax = new Vector2(0.5f, 0.5f);
            cirt.sizeDelta = new Vector2(48f, 48f);
            var badge = Icon(cell.rectTransform, "Badge", Emoji("item_wood"), 21);
            var bdrt = badge.rectTransform;
            bdrt.anchorMin = bdrt.anchorMax = new Vector2(1f, 1f);
            bdrt.pivot = new Vector2(1f, 1f);
            bdrt.sizeDelta = new Vector2(21f, 21f);
            bdrt.anchoredPosition = new Vector2(-4f, -4f);
            var rc = cell.gameObject.AddComponent<RecipeCell>();
            Set(rc, "button", cbtn);
            Set(rc, "border", cell.GetComponents<Outline>()[0]);
            Set(rc, "glow", glow);
            Set(rc, "icon", ci);
            Set(rc, "badge", badge);
            cell.gameObject.SetActive(false);

            // detail panel, bottom-right above the bottom bar
            var detail = Chip(root, "Detail", UiPalette.Bg2, UiPalette.Line);
            var drt = detail.rectTransform;
            drt.anchorMin = drt.anchorMax = new Vector2(1f, 0f);
            drt.pivot = new Vector2(1f, 0f);
            drt.anchoredPosition = new Vector2(-16f, 76f);
            var dle = Col(detail.gameObject, 18, 8);
            dle.childControlWidth = true;
            var dfit = Fit(detail.gameObject, false, true);
            drt.sizeDelta = new Vector2(336f, 200f);
            var head = NewUi("Head", drt);
            Row(head.gameObject, 0, 0, 10);
            var dIcon = Icon(head, "Icon", Emoji("item_plank"), 63);
            var dName = Label(head, "Name", "Plank", 24f, UiPalette.Text, true);
            var dYield = Label(drt, "Yield", "Makes 1× Plank · 2.0s", 18f, UiPalette.Gold, true);
            var rows = NewUi("Rows", drt);
            Col(rows.gameObject, 0, 6);
            var rowRt = NewUi("RowTemplate", rows);
            Row(rowRt.gameObject, 0, 0, 10);
            var ric = rowRt.gameObject.AddComponent<UiIconCount>();
            var box = NewUi("IconBox", rowRt);
            var ble = box.gameObject.AddComponent<LayoutElement>();
            ble.preferredWidth = 39f; ble.preferredHeight = 39f;
            var rIcon = box.gameObject.AddComponent<Image>();
            rIcon.preserveAspect = true; rIcon.raycastTarget = false;
            var rq = Label(box, "Qty", "1", 15f, UiPalette.Gold, true);
            var rqrt = rq.rectTransform;
            rqrt.anchorMin = rqrt.anchorMax = new Vector2(1f, 0f);
            rqrt.pivot = new Vector2(1f, 0f);
            rqrt.sizeDelta = new Vector2(30f, 18f);
            rqrt.anchoredPosition = new Vector2(4f, -4f);
            rq.alignment = TextAlignmentOptions.BottomRight;
            rq.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Fonts/LiberationSans SDF - Floater.mat") ?? rq.fontSharedMaterial;
            var rl = Label(rowRt, "Name", "Wood", 19.5f, UiPalette.Text, false);
            ric.icon = rIcon; ric.qty = rq; ric.label = rl;
            rowRt.gameObject.SetActive(false);

            Set(view, "popup", prt);
            Set(view, "grid", grid);
            Set(view, "cellTemplate", rc);
            Set(view, "detail", drt);
            Set(view, "detailIcon", dIcon);
            Set(view, "detailName", dName);
            Set(view, "detailYield", dYield);
            Set(view, "detailRows", rows);
            Set(view, "rowTemplate", ric);
            popup.gameObject.SetActive(false);
            detail.gameObject.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/RecipePicker.prefab");
        }

        static void BuildTooltipPrefab()
        {
            var root = NewUi("BuildingTooltip", null);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 114f);
            root.sizeDelta = new Vector2(10f, 10f);
            var view = root.gameObject.AddComponent<BuildingTooltipView>();
            var panel = Chip(root, "Panel", UiPalette.HandChip, UiPalette.Line);
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            Col(panel.gameObject, 12, 2, TextAnchor.MiddleCenter);
            Fit(panel.gameObject, true, true);
            var nm = Label(prt, "Name", "Workbench", 24f, UiPalette.Text, true);
            nm.alignment = TextAlignmentOptions.Center;
            var st = Label(prt, "Status", "Working", 18f, UiPalette.Accent, true);
            st.alignment = TextAlignmentOptions.Center;
            Set(view, "panel", panel.gameObject);
            Set(view, "nameText", nm);
            Set(view, "statusText", st);
            panel.gameObject.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/BuildingTooltip.prefab");
        }

        // ------------------------------------------------------------------ scene wiring

        public static void InstallIntoActiveScene()
        {
            var runner = Object.FindFirstObjectByType<GameRunner>();
            var hand = Object.FindFirstObjectByType<HandController>();
            var fx = Object.FindFirstObjectByType<FxService>();
            var cam = Object.FindFirstObjectByType<CameraController>();
            if (runner == null || hand == null) { Debug.LogError("[IdleGrounds] Install the M2 core loop first."); return; }

            var runtime = FindOrCreate("Runtime", null).transform;
            var bRoot = FindOrCreate("Buildings", runtime);
            var sync = Ensure<BuildingViewSync>(bRoot);
            Set(sync, "runner", runner);
            Set(sync, "prefabSet", AssetDatabase.LoadAssetAtPath<BuildingPrefabSet>(PrefabSetPath));
            Set(sync, "root", bRoot.transform);

            var oldGhost = runtime.Find("PlacementGhost");
            if (oldGhost != null) Object.DestroyImmediate(oldGhost.gameObject);
            var ghost = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldPrefabDir + "/PlacementGhost.prefab"), runtime);
            ghost.SetActive(false);

            var systems = FindOrCreate("--- Systems", null).transform;
            var bc = Ensure<BuildController>(FindOrCreate("BuildController", systems));
            Set(bc, "runner", runner);
            Set(bc, "hand", hand);
            Set(bc, "fx", fx);
            Set(bc, "buildings", sync);
            Set(bc, "preview", ghost.GetComponent<PlacementPreview>());
            Set(hand, "build", bc);

            var canvas = GameObject.Find("UI/Canvas");
            if (canvas == null) { Debug.LogError("[IdleGrounds] UI/Canvas missing."); return; }
            foreach (var n in new[] { "BuildingTooltip", "BuildMenu", "RecipePicker" })
            {
                var old = canvas.transform.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            GameObject Inst(string n)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabDir + "/" + n + ".prefab"), canvas.transform);
                return go;
            }
            var tip = Inst("BuildingTooltip").GetComponent<BuildingTooltipView>();
            var menu = Inst("BuildMenu").GetComponent<BuildMenuView>();
            var picker = Inst("RecipePicker").GetComponent<RecipePickerView>();
            Set(tip, "runner", runner); Set(tip, "controller", bc);
            Set(menu, "runner", runner); Set(menu, "controller", bc);
            Set(picker, "runner", runner); Set(picker, "worldCamera", cam != null ? cam.GetComponent<Camera>() : null);
            Set(bc, "buildMenu", menu);
            Set(bc, "recipePicker", picker);

            var bar = Object.FindFirstObjectByType<BottomBarView>();
            if (bar != null) { Set(bar, "build", bc); bar.transform.SetAsLastSibling(); }
            var cursor = canvas.transform.Find("HandCursor");
            if (cursor != null) cursor.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
