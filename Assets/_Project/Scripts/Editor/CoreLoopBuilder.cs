using System.Collections.Generic;
using System.IO;
using IdleGrounds.Game;
using IdleGrounds.Game.Data;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// M2 "core loop": reproducible prefabs (Node, GroundItem, FloatingText, HandCursor, BottomBar),
    /// sorting layers, and the scene wiring (GameRunner, HandController, WorldViewSync, FxService, HUD).
    /// </summary>
    public static class CoreLoopBuilder
    {
        public const string WorldPrefabDir = "Assets/_Project/Prefabs/World";
        public const string UiPrefabDir = "Assets/_Project/Prefabs/UI";
        const string ShapesDir = "Assets/_Project/Art/Sprites/Shapes";
        const string CirclePath = ShapesDir + "/circle.png";
        const string EmojiAtlas = "Assets/_Project/Art/Sprites/Emoji/emoji_atlas.png";
        const string DatabasePath = "Assets/_Project/Data/GameDatabase.asset";
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        public static readonly string[] SortingLayers = { "Ground", "Buildings", "Entities", "Items", "Overlay", "FX" };

        // ------------------------------------------------------------------ sorting layers

        [MenuItem("Idle Grounds/Project/Ensure Sorting Layers")]
        public static void EnsureSortingLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");
            var have = new HashSet<string>();
            for (int i = 0; i < layers.arraySize; i++) have.Add(layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue);
            bool changed = false;
            foreach (var name in SortingLayers)
            {
                if (have.Contains(name)) continue;
                layers.InsertArrayElementAtIndex(layers.arraySize);
                var e = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                e.FindPropertyRelative("name").stringValue = name;
                e.FindPropertyRelative("uniqueID").uintValue = (uint)(Animator.StringToHash("SortingLayer_" + name) & 0x7FFFFFFF);
                e.FindPropertyRelative("locked").intValue = 0;
                changed = true;
            }
            if (changed) { tagManager.ApplyModifiedProperties(); AssetDatabase.SaveAssets(); }
        }

        // ------------------------------------------------------------------ prefabs

        [MenuItem("Idle Grounds/Prefabs/Build Core Prefabs")]
        public static void BuildCorePrefabs()
        {
            EnsureSortingLayers();
            Directory.CreateDirectory(WorldPrefabDir);
            Directory.CreateDirectory(UiPrefabDir);
            var circle = EnsureCircleSprite();
            BuildNodePrefab(circle);
            BuildGroundItemPrefab();
            BuildFloatingTextPrefab();
            BuildHandCursorPrefab();
            BuildBottomBarPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[IdleGrounds] Core prefabs built.");
        }

        internal static Sprite EnsureCircleSprite()
        {
            Directory.CreateDirectory(ShapesDir);
            if (!File.Exists(CirclePath))
            {
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x + 0.5f - n / 2f, dy = y + 0.5f - n / 2f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(n / 2f - d);
                        px[y * n + x] = new Color(1, 1, 1, a);
                    }
                tex.SetPixels(px);
                File.WriteAllBytes(CirclePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(CirclePath, ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(CirclePath);
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = n;       // 1 unit diameter
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
        }

        internal static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[IdleGrounds] {target.GetType().Name}.{field} not found"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static GameObject Save(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void BuildNodePrefab(Sprite circle)
        {
            var root = new GameObject("Node");
            var sg = root.AddComponent<SortingGroup>();
            sg.sortingLayerName = "Entities";
            var view = root.AddComponent<NodeView>();

            var pad = new GameObject("Pad").AddComponent<SpriteRenderer>();
            pad.transform.SetParent(root.transform, false);
            pad.sprite = circle;
            pad.color = new Color(74 / 255f, 222 / 255f, 128 / 255f, 0.16f);
            pad.sortingLayerName = "Entities";
            pad.sortingOrder = 0;

            var squash = new GameObject("Squash").transform;
            squash.SetParent(root.transform, false);
            var sprite = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            sprite.transform.SetParent(squash, false);
            sprite.sortingLayerName = "Entities";
            sprite.sortingOrder = 1;

            // M6: AUTO badge (gold 800 9 px) + M5: Spirit Tree sparkles (ui_sparkle), both inside the sorting group
            var autoGo = new GameObject("Auto");
            autoGo.transform.SetParent(root.transform, false);
            var auto = autoGo.AddComponent<TextMeshPro>();
            auto.text = "AUTO";
            auto.fontSize = 10f * ViewKit.FontPerPx;   // lblPx(9) never below 10
            auto.fontStyle = FontStyles.Bold;
            auto.color = UiPalette.Gold;
            auto.alignment = TextAlignmentOptions.Center;
            auto.textWrappingMode = TextWrappingModes.NoWrap;
            auto.rectTransform.sizeDelta = new Vector2(1.5f, 0.5f);
            auto.fontSharedMaterial = FloaterMaterial(auto.font != null ? auto.font : TMP_Settings.defaultFontAsset);
            var amr = autoGo.GetComponent<MeshRenderer>();
            amr.sortingLayerName = "Entities"; amr.sortingOrder = 4;
            autoGo.SetActive(false);
            // fish surface countdown "x.xs": #f87171 800 lblPx(11), 10 px under the sprite base
            var cdGo = new GameObject("Countdown");
            cdGo.transform.SetParent(root.transform, false);
            var cd = cdGo.AddComponent<TextMeshPro>();
            cd.text = "0.9s";
            cd.fontSize = 11f * ViewKit.FontPerPx;
            cd.fontStyle = FontStyles.Bold;
            cd.color = UiPalette.Hex("#f87171");
            cd.alignment = TextAlignmentOptions.Center;
            cd.textWrappingMode = TextWrappingModes.NoWrap;
            cd.rectTransform.sizeDelta = new Vector2(1.5f, 0.5f);
            cd.fontSharedMaterial = FloaterMaterial(cd.font != null ? cd.font : TMP_Settings.defaultFontAsset);
            var cmr = cdGo.GetComponent<MeshRenderer>();
            cmr.sortingLayerName = "Entities"; cmr.sortingOrder = 4;
            cdGo.SetActive(false);
            Set(view, "countdown", cd);
            var sparkles = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                var sp = new GameObject("Sparkle" + i).AddComponent<SpriteRenderer>();
                sp.transform.SetParent(squash, false);
                sp.sprite = Emoji("ui_sparkle");
                sp.sortingLayerName = "Entities";
                sp.sortingOrder = 2;
                sp.gameObject.SetActive(false);
                sparkles[i] = sp;
            }
            Set(view, "autoBadge", auto);
            BuildingsBuilder.SetArray(view, "sparkles", sparkles);

            Set(view, "squash", squash);
            Set(view, "spriteRenderer", sprite);
            Set(view, "pad", pad);
            Set(view, "sortingGroup", sg);
            Save(root, WorldPrefabDir + "/Node.prefab");
        }

        static void BuildGroundItemPrefab()
        {
            var root = new GameObject("GroundItem");
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Items";
            var view = root.AddComponent<GroundItemView>();
            Set(view, "spriteRenderer", sr);
            Save(root, WorldPrefabDir + "/GroundItem.prefab");
        }

        static void BuildFloatingTextPrefab()
        {
            var root = new GameObject("FloatingText");
            var ft = root.AddComponent<FloatingText>();
            var icon = new GameObject("Icon").AddComponent<SpriteRenderer>();
            icon.transform.SetParent(root.transform, false);
            icon.sortingLayerName = "FX";
            icon.sortingOrder = 0;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(root.transform, false);
            var t = textGo.AddComponent<TextMeshPro>();
            t.text = "+1";
            t.fontSize = 4.4f;                              // ~14 world px
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Left;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.rectTransform.sizeDelta = new Vector2(8f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            t.color = new Color(0.29f, 0.87f, 0.5f);
            t.fontSharedMaterial = FloaterMaterial(t.font != null ? t.font : TMP_Settings.defaultFontAsset);  // dark halo rgba(8,10,14,.85)
            var mr = textGo.GetComponent<MeshRenderer>();
            mr.sortingLayerName = "FX";
            mr.sortingOrder = 1;

            Set(ft, "text", t);
            Set(ft, "icon", icon);
            Save(root, WorldPrefabDir + "/FloatingText.prefab");
        }

        /// <summary>Font material preset with the floater outline baked in (no per-instance materials).</summary>
        static Material FloaterMaterial(TMP_FontAsset font)
        {
            const string path = "Assets/_Project/Art/Fonts/LiberationSans SDF - Floater.mat";
            Directory.CreateDirectory("Assets/_Project/Art/Fonts");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(font.material) { name = "LiberationSans SDF - Floater" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(8, 10, 14, 217));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---- UI helpers

        static Sprite uiSprite;
        internal static Sprite UiSprite => uiSprite != null ? uiSprite : uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        static Dictionary<string, Sprite> emoji;
        internal static Sprite Emoji(string key)
        {
            if (emoji == null)
            {
                emoji = new Dictionary<string, Sprite>();
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(EmojiAtlas))
                    if (o is Sprite s) emoji[s.name] = s;
            }
            return emoji.TryGetValue(key, out var sp) ? sp : null;
        }

        internal static RectTransform NewUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        internal static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color c, bool bold)
        {
            var rt = NewUi(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        internal static Image Icon(Transform parent, string name, Sprite s, float size)
        {
            var rt = NewUi(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size; le.preferredHeight = size;
            return img;
        }

        internal static HorizontalLayoutGroup Row(GameObject go, int padH, int padV, float spacing)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(padH, padH, padV, padV);
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return h;
        }

        /// <summary>Rounded panel chip: fill + 1 px border (Outline effect).</summary>
        internal static Image Chip(Transform parent, string name, Color fill, Color border, bool raycast = false)
        {
            var rt = NewUi(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiSprite;
            img.type = Image.Type.Sliced;
            img.color = fill;
            img.raycastTarget = raycast;
            var ol = rt.gameObject.AddComponent<Outline>();
            ol.effectColor = border;
            ol.effectDistance = new Vector2(1f, -1f);
            return img;
        }

        static void BuildHandCursorPrefab()
        {
            var root = NewUi("HandCursor", null);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
            var view = root.gameObject.AddComponent<HandCursorView>();

            var chip = Chip(root, "Chip", UiPalette.HandChip, UiPalette.Accent);
            UiSkin.Attach(chip.gameObject, "ui_handchip_frame");
            var crt = chip.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0f, 1f);
            Row(chip.gameObject, 10, 6, 12);
            var fit = chip.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var entry = NewUi("StackTemplate", crt);
            Row(entry.gameObject, 0, 0, 3);
            var qty = Label(entry, "Qty", "1", 18, UiPalette.Accent, true);
            var icon = Icon(entry, "Icon", Emoji("item_wood"), 27);
            var se = entry.gameObject.AddComponent<HandStackEntry>();
            Set(se, "qty", qty);
            Set(se, "icon", icon);
            Set(se, "iconLayout", icon.GetComponent<LayoutElement>());

            Set(view, "chip", crt);
            Set(view, "entryContainer", crt);
            Set(view, "entryTemplate", se);
            Save(root.gameObject, UiPrefabDir + "/HandCursor.prefab");
        }

        internal static Button BarButton(Transform parent, string name, string label, Sprite icon, Color textColour)
        {
            var img = Chip(parent, name, UiPalette.Panel, UiPalette.Line, raycast: true);
            Row(img.gameObject, 12, 6, 6);
            if (icon != null) Icon(img.transform, "Icon", icon, 22);
            Label(img.transform, "Label", label, 17, textColour, true);
            var b = img.gameObject.AddComponent<Button>();
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            b.colors = cb;
            b.targetGraphic = img;
            var le = img.gameObject.GetComponent<LayoutElement>() ?? img.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 34f;      // the 2x button art needs >= 32 px
            UiButtonRoles.AttachFor(b);
            return b;
        }

        static void BuildBottomBarPrefab()
        {
            var root = NewUi("BottomBar", null);
            root.anchorMin = new Vector2(0f, 0f); root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(0f, 60f);
            root.anchoredPosition = Vector2.zero;
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(UiPalette.Bg2.r, UiPalette.Bg2.g, UiPalette.Bg2.b, 0.96f);
            bg.raycastTarget = true;     // the bar blocks world clicks
            UiSkin.Attach(root.gameObject, "ui_bottombar_bg");
            var row = Row(root.gameObject, 16, 8, 12);
            var view = root.gameObject.AddComponent<BottomBarView>();

            // left: globe + title + version badge
            Icon(root, "Logo", Emoji("ui_area_center"), 26);
            Label(root, "Title", "Idle Grounds", 25, UiPalette.Text, true);
            var badge = Chip(root, "VersionBadge", UiPalette.Panel, UiPalette.Line);
            Row(badge.gameObject, 8, 2, 0);
            var version = Label(badge.transform, "Version", "v52", 16, UiPalette.Muted, true);

            var spacer = NewUi("Spacer", root).gameObject.AddComponent<LayoutElement>();
            spacer.flexibleWidth = 1f;

            // area pill
            var area = Chip(root, "AreaPill", UiPalette.Panel, UiPalette.Line);
            Row(area.gameObject, 12, 6, 6);
            var areaIcon = Icon(area.transform, "RegionIcon", Emoji("ui_area_center"), 22);
            var areaText = Label(area.transform, "RegionName", "Center", 19, UiPalette.Text, true);
            var areaLock = Icon(area.transform, "Lock", Emoji("ui_lock"), 18);
            var sprint = NewUi("Sprint", area.transform);
            Row(sprint.gameObject, 0, 0, 2);
            Icon(sprint, "Icon", Emoji("ui_sprint"), 18);
            Label(sprint, "Label", "2×", 17, UiPalette.Gold, true);
            areaLock.gameObject.SetActive(false);
            sprint.gameObject.SetActive(false);

            // hand pill
            var hand = Chip(root, "HandPill", UiPalette.Panel, UiPalette.Line);
            Row(hand.gameObject, 12, 6, 6);
            Icon(hand.transform, "Icon", Emoji("ui_hand"), 22);
            var handText = Label(hand.transform, "Count", "0/20", 19, UiPalette.Accent, true);

            // buttons (placeholders until M3/M4; Reset disabled until saves exist)
            BarButton(root, "HelpButton", "Help", null, UiPalette.Text);
            BarButton(root, "StatsButton", "Stats", Emoji("ui_stats"), UiPalette.Text);
            var buildBtn = BarButton(root, "BuildButton", "Build", Emoji("ui_build"), UiPalette.Text);
            var demolishBtn = BarButton(root, "DemolishButton", "Demolish", Emoji("ui_demolish"), UiPalette.Text);
            // gold "unseen revealed buildings" dot, top-right of the Build button (§4.1)
            var dot = NewUi("NewDot", buildBtn.transform);
            dot.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            dot.anchorMin = dot.anchorMax = new Vector2(1f, 1f);
            dot.pivot = new Vector2(0.5f, 0.5f);
            dot.sizeDelta = new Vector2(12f, 12f);
            dot.anchoredPosition = new Vector2(-4f, -4f);
            var dotImg = dot.gameObject.AddComponent<Image>();
            dotImg.sprite = EnsureCircleSprite();
            dotImg.color = UiPalette.Gold;
            dotImg.raycastTarget = false;
            dot.gameObject.SetActive(false);
            Set(view, "buildButton", buildBtn);
            Set(view, "demolishButton", demolishBtn);
            Set(view, "buildNewDot", dot.gameObject);
            var reset = BarButton(root, "ResetButton", "Reset", null, UiPalette.Danger);
            reset.interactable = false;

            Set(view, "versionText", version);
            Set(view, "areaText", areaText);
            Set(view, "areaIcon", areaIcon);
            Set(view, "areaLock", areaLock.gameObject);
            Set(view, "sprintTag", sprint.gameObject);
            Set(view, "handText", handText);
            Set(view, "handPill", hand);
            Set(view, "resetButton", reset);
            Save(root.gameObject, UiPrefabDir + "/BottomBar.prefab");
        }

        // ------------------------------------------------------------------ scene wiring

        [MenuItem("Idle Grounds/Scene/Install Core Loop (M2)")]
        public static void InstallMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildCorePrefabs();
            InstallIntoActiveScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IdleGrounds] Core loop installed into " + ScenePath);
        }

        internal static GameObject FindOrCreate(string name, Transform parent)
        {
            var t = parent != null ? parent.Find(name) : null;
            if (t == null && parent == null) { var go = GameObject.Find(name); if (go != null && go.transform.parent == null) t = go.transform; }
            if (t != null) return t.gameObject;
            var n = new GameObject(name);
            if (parent != null) n.transform.SetParent(parent, false);
            return n;
        }

        internal static T Ensure<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();

        /// <summary>Adds/refreshes every M2 object in the active (Game) scene. Idempotent.</summary>
        public static void InstallIntoActiveScene()
        {
            EnsureSortingLayers();
            ApplyWorldSortingLayers();
            PlayerSettings.runInBackground = true;   // an idle game keeps ticking unfocused (also keeps Play mode alive for MCP)

            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            WorldBuilder.BuildZoneOverlays(db);     // in-game zone tints + Island frames + veil 🔒 (H1 / L18)
            var cam = Object.FindFirstObjectByType<CameraController>();

            var systems = FindOrCreate("--- Systems", null).transform;
            var runnerGo = FindOrCreate("GameRunner", systems);
            var runner = Ensure<GameRunner>(runnerGo);
            Set(runner, "database", db);
            Set(runner, "cameraController", cam);

            var runtime = FindOrCreate("Runtime", null).transform;
            var nodes = FindOrCreate("Nodes", runtime).transform;
            var ground = FindOrCreate("Ground", runtime).transform;
            foreach (var n in new[] { "Buildings", "Enemies", "Wisps" }) FindOrCreate(n, runtime);
            var fxGo = FindOrCreate("FX", runtime);

            var fx = Ensure<FxService>(fxGo);
            Set(fx, "runner", runner);
            Set(fx, "floaterPrefab", AssetDatabase.LoadAssetAtPath<FloatingText>(WorldPrefabDir + "/FloatingText.prefab"));
            Set(fx, "worldCamera", cam != null ? cam.GetComponent<Camera>() : null);

            var sync = Ensure<WorldViewSync>(runtime.gameObject);
            Set(sync, "runner", runner);
            Set(sync, "nodePrefab", AssetDatabase.LoadAssetAtPath<NodeView>(WorldPrefabDir + "/Node.prefab"));
            Set(sync, "groundItemPrefab", AssetDatabase.LoadAssetAtPath<GroundItemView>(WorldPrefabDir + "/GroundItem.prefab"));
            Set(sync, "nodesRoot", nodes);
            Set(sync, "groundRoot", ground);
            Set(sync, "decoSprite", Emoji("fix_spirittree"));

            var handGo = FindOrCreate("HandController", systems);
            var hand = Ensure<HandController>(handGo);
            Set(hand, "runner", runner);
            Set(hand, "fx", fx);
            Set(hand, "worldCamera", cam != null ? cam.GetComponent<Camera>() : null);

            // HUD prefab instances under UI/Canvas
            var canvas = GameObject.Find("UI/Canvas");
            if (canvas == null) { Debug.LogError("[IdleGrounds] UI/Canvas missing — build the scene first."); return; }
            foreach (var n in new[] { "BottomBar", "HandCursor" })
            {
                var old = canvas.transform.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var bar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabDir + "/BottomBar.prefab"), canvas.transform);
            var barView = bar.GetComponent<BottomBarView>();
            Set(barView, "runner", runner);
            Set(barView, "cameraController", cam);
            Set(barView, "wildsIcon", Emoji("ui_wilds"));
            var cursor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabDir + "/HandCursor.prefab"), canvas.transform);
            var cursorView = cursor.GetComponent<HandCursorView>();
            Set(cursorView, "runner", runner);
            Set(cursorView, "hand", hand);
            cursor.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        /// <summary>Tilemaps on Ground; veils + Island labels on Overlay (above locked-Island nodes).</summary>
        public static void ApplyWorldSortingLayers()
        {
            foreach (var r in Object.FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var tr in r.GetComponentsInChildren<TilemapRenderer>(true)) tr.sortingLayerName = "Ground";
                if (r.veil != null) { var sr = r.veil.GetComponent<SpriteRenderer>(); if (sr != null) { sr.sortingLayerName = "Overlay"; sr.sortingOrder = 0; } }
                var label = r.transform.Find("Label");
                if (label != null) { var mr = label.GetComponent<MeshRenderer>(); if (mr != null) { mr.sortingLayerName = "Overlay"; mr.sortingOrder = 1; } }
                EditorUtility.SetDirty(r.gameObject);
            }
        }
    }
}
