using System.IO;
using IdleGrounds.Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static IdleGrounds.Editor.BuildingsBuilder;
using static IdleGrounds.Editor.CoreLoopBuilder;
using static IdleGrounds.Editor.ProgressionBuilder;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// ADR 0003 view side, reproducible: the Sky root (gradient + peaks + 3 parallax cloud layers, all
    /// <see cref="ParallaxLayer"/>s on the "Sky" sorting layer), camera clear colour = sky base, the QiTrail
    /// world prefab + <see cref="QiTrailSync"/> (Runtime/QiTrails), <see cref="SkyWispViewSync"/>
    /// (Runtime/SkyWisps, reusing the Wisp prefab), the BridgePanel UI prefab under UI/Canvas wired into
    /// BuildController, and the unlock steles' camera link. The Islands themselves: <see cref="WorldBuilder"/>.
    /// </summary>
    public static class IslandsBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const string Sky = WorldBuilder.SkyLayer;

        [MenuItem("Idle Grounds/Prefabs/Build Island + Bridge Prefabs (ADR 0003)")]
        public static void BuildAllPrefabs()
        {
            EnsureSortingLayers();
            WorldBuilder.EnsureSkySortingLayer();
            EnsureShapes();
            IslandArtBuilder.EnsureArt();
            Directory.CreateDirectory(WorldPrefabDir);
            Directory.CreateDirectory(UiPrefabDir);
            BuildQiTrailPrefab();
            BuildBridgePanelPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[IdleGrounds] Island + bridge prefabs built.");
        }

        [MenuItem("Idle Grounds/Scene/Install Islands, Sky + Bridges (ADR 0003)")]
        public static void InstallMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildAllPrefabs();
            InstallIntoActiveScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IdleGrounds] Islands, sky + bridges installed into " + ScenePath);
        }

        // ------------------------------------------------------------------ prefabs

        static void BuildQiTrailPrefab()
        {
            var root = new GameObject("QiTrail");
            var v = root.AddComponent<QiTrailView>();
            var frames = IslandArtBuilder.Frames(IslandArtBuilder.QiTrail);
            // under buildings, above every tilemap (Ground layer like the link threads)
            var line = Sr(root.transform, "Line", 45, frames.Length > 0 ? frames[0] : null, "Ground");
            line.drawMode = SpriteDrawMode.Tiled;
            line.tileMode = SpriteTileMode.Continuous;
            Set(v, "line", line);
            SetArray(v, "frames", frames);
            Save(root, WorldPrefabDir + "/QiTrail.prefab");
        }

        static void BuildBridgePanelPrefab()
        {
            var root = Stretch(NewUi("BridgePanel", null));
            var view = root.gameObject.AddComponent<BridgePanelView>();
            var panel = Chip(root, "Panel", new Color(UiPalette.Bg2.r, UiPalette.Bg2.g, UiPalette.Bg2.b, 0.97f), UiPalette.Line, raycast: true);
            UiSkin.Attach(panel.gameObject, "ui_panel_jade");
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(1f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.sizeDelta = new Vector2(-24f, 100f);
            prt.anchoredPosition = new Vector2(0f, 68f);
            ColW(panel.gameObject, 14, 8);
            Fit(panel.gameObject, false, true);

            var head = NewUi("Header", prt);
            Row(head.gameObject, 0, 0, 10);
            var icon = Icon(head, "Icon", Emoji("bld_wisp_lantern"), 33);
            var title = Label(head, "Title", "Spirit Bridge", 21f, UiPalette.Text, true);
            var status = Label(head, "Status", "Unpaired", 19f, UiPalette.Muted, true);
            var buffer = Label(head, "Buffer", "Buffer 0/20", 19f, UiPalette.Text, true);
            Le(NewUi("Spacer", head), flexW: 1);
            var unpair = SmallButton(head, "Unpair", "Unpair", UiPalette.Danger, 120, 36);
            var close = SmallButton(head, "Close", "x", UiPalette.Text, 42, 36);

            var hint = Wrap(Label(prt, "Hint", "", 18f, UiPalette.Muted, false));
            var empty = Wrap(Label(prt, "Empty", "", 18f, UiPalette.Amber, false));

            var rows = NewUi("Candidates", prt);
            ColW(rows.gameObject, 0, 6);
            Fit(rows.gameObject, false, true);

            // candidate row template: [island icon] "Mine bridge - 128 cells"  [Send to]  [Receive from]
            var rowImg = Chip(rows, "RowTemplate", UiPalette.Panel, UiPalette.Line, raycast: true);
            Row(rowImg.gameObject, 10, 4, 10);
            Le(rowImg, -1, 44);
            var br = rowImg.gameObject.AddComponent<BridgeRow>();
            br.icon = Icon(rowImg.rectTransform, "Icon", Emoji("ui_area_mine"), 26);
            br.label = Label(rowImg.rectTransform, "Label", "Mine bridge - 128 cells", 18f, UiPalette.Text, true);
            Le(NewUi("Spacer", rowImg.rectTransform), flexW: 1);
            br.sendButton = SmallButton(rowImg.rectTransform, "Send", "Send to >", UiPalette.Hex("#67e8f9"), 160, 34);
            br.receiveButton = SmallButton(rowImg.rectTransform, "Receive", "< Receive from", UiPalette.Hex("#bef264"), 200, 34);
            EditorUtility.SetDirty(br);

            var reason = Label(prt, "Reason", "", 18f, UiPalette.Danger, true);

            Set(view, "panel", panel.gameObject);
            Set(view, "icon", icon);
            Set(view, "titleText", title);
            Set(view, "statusText", status);
            Set(view, "bufferText", buffer);
            Set(view, "hintText", hint);
            Set(view, "rowsRoot", rows);
            Set(view, "rowTemplate", br);
            Set(view, "emptyText", empty);
            Set(view, "unpairButton", unpair);
            Set(view, "reasonText", reason);
            Set(view, "closeButton", close);
            rowImg.gameObject.SetActive(false);
            empty.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/BridgePanel.prefab");
        }

        // ------------------------------------------------------------------ scene

        static ParallaxLayer Layer(Transform parent, string name, Sprite s, int order, ParallaxLayer.Mode mode,
            float centre, float height, float scroll, float drift, Camera cam, Color tint)
        {
            var sr = Sr(parent, name, order, s, Sky);
            sr.color = tint;
            var p = sr.gameObject.AddComponent<ParallaxLayer>();
            p.mode = mode;
            p.bandCentre = centre;
            p.bandHeight = height;
            p.scroll = scroll;
            p.drift = drift;
            Set(p, "target", cam);
            return p;
        }

        /// <summary>Sky root: gradient (fill) → peaks → far / mid clouds (behind undersides) → near clouds (in front of them).</summary>
        public static void BuildSky(Camera cam)
        {
            var old = GameObject.Find("Sky");
            if (old != null && old.transform.parent == null) Object.DestroyImmediate(old);
            var root = new GameObject("Sky").transform;
            Layer(root, "Gradient", IslandArtBuilder.Single(IslandArtBuilder.SkyGradient), -100, ParallaxLayer.Mode.Fill, 0f, 1f, 0f, 0f, cam, Color.white);
            // moon: screen-anchored upper-left, native 3 units wide at the start view, scales with the view like the gradient
            var moon = Layer(root, "Moon", IslandArtBuilder.Single(IslandArtBuilder.Moon), -95, ParallaxLayer.Mode.Fixed, 0f, 1f, 0.01f, 0f, cam, Color.white);
            moon.anchorX = 0.18f; moon.anchorY = 0.15f; moon.refViewWidth = 35f;
            Layer(root, "Peaks", IslandArtBuilder.Single(IslandArtBuilder.Peaks), -90, ParallaxLayer.Mode.Band, -0.18f, 0.32f, 0.02f, 0f, cam, Color.white);
            Layer(root, "CloudsFar", IslandArtBuilder.Single(IslandArtBuilder.CloudsFar), -80, ParallaxLayer.Mode.Band, -0.30f, 0.30f, 0.05f, 0.15f, cam, Color.white);
            Layer(root, "CloudsMid", IslandArtBuilder.Single(IslandArtBuilder.CloudsMid), -70, ParallaxLayer.Mode.Band, -0.36f, 0.34f, 0.10f, 0.3f, cam, Color.white);
            // near clouds drift fast and pass in front of the island undersides (orders 39-45), still behind the ground
            Layer(root, "CloudsNear", IslandArtBuilder.Single(IslandArtBuilder.CloudsNear), 60, ParallaxLayer.Mode.Band, -0.44f, 0.26f, 0.22f, 0.6f, cam, new Color(1f, 1f, 1f, 0.8f));
        }

        public static void InstallIntoActiveScene()
        {
            WorldBuilder.EnsureSkySortingLayer();
            IslandArtBuilder.EnsureArt();
            var runner = Object.FindFirstObjectByType<GameRunner>();
            var camCtl = Object.FindFirstObjectByType<CameraController>();
            var cam = camCtl != null ? camCtl.GetComponent<Camera>() : Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = IslandArtBuilder.SkyBase;
                EditorUtility.SetDirty(cam);
            }
            BuildSky(cam);

            var runtime = FindOrCreate("Runtime", null).transform;

            var trailsRoot = FindOrCreate("QiTrails", runtime);
            var ts = Ensure<QiTrailSync>(trailsRoot);
            Set(ts, "runner", runner);
            Set(ts, "prefab", AssetDatabase.LoadAssetAtPath<QiTrailView>(WorldPrefabDir + "/QiTrail.prefab"));
            Set(ts, "root", trailsRoot.transform);

            var skyRoot = FindOrCreate("SkyWisps", runtime);
            var sw = Ensure<SkyWispViewSync>(skyRoot);
            Set(sw, "runner", runner);
            Set(sw, "prefab", AssetDatabase.LoadAssetAtPath<WispView>(WorldPrefabDir + "/Wisp.prefab"));
            Set(sw, "root", skyRoot.transform);

            var signs = Object.FindFirstObjectByType<UnlockSignSync>();
            if (signs != null && camCtl != null) Set(signs, "cameraController", camCtl);

            var canvas = GameObject.Find("UI/Canvas");
            var bc = Object.FindFirstObjectByType<BuildController>();
            if (canvas != null)
            {
                var old = canvas.transform.Find("BridgePanel");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabDir + "/BridgePanel.prefab"), canvas.transform);
                var panel = go.GetComponent<BridgePanelView>();
                Set(panel, "runner", runner);
                if (bc != null) Set(bc, "bridgePanel", panel);
                // keep the bottom bar + hand cursor on top
                var pav = canvas.transform.Find("PavilionPanel");
                if (pav != null) go.transform.SetSiblingIndex(pav.GetSiblingIndex() + 1);
                var bar = Object.FindFirstObjectByType<BottomBarView>();
                if (bar != null) bar.transform.SetAsLastSibling();
                var cursor = canvas.transform.Find("HandCursor");
                if (cursor != null) cursor.SetAsLastSibling();
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
