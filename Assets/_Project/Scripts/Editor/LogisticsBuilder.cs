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

namespace IdleGrounds.Editor
{
    /// <summary>
    /// M4 "Logistics" view side, reproducible: Wisp + LinkLine world prefabs, the LinkEditor UI prefab,
    /// and the scene wiring (WispViewSync under Runtime/Wisps, LinkLineSync + rubber band under
    /// Runtime/Links, LinkEditor under UI/Canvas, BuildController.linkEditor). The lantern's beat bar
    /// lives in the WispLantern building prefab variant (BuildingsBuilder).
    /// </summary>
    public static class LogisticsBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        [MenuItem("Idle Grounds/Prefabs/Build Logistics Prefabs (M4)")]
        public static void BuildAllPrefabs()
        {
            EnsureSortingLayers();
            EnsureShapes();
            Directory.CreateDirectory(WorldPrefabDir);
            Directory.CreateDirectory(UiPrefabDir);
            BuildWispPrefab();
            BuildLinkLinePrefab();
            BuildLinkEditorPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[IdleGrounds] M4 logistics prefabs built.");
        }

        [MenuItem("Idle Grounds/Scene/Install Logistics (M4)")]
        public static void InstallMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildAllPrefabs();
            InstallIntoActiveScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IdleGrounds] M4 logistics installed into " + ScenePath);
        }

        // ------------------------------------------------------------------ world prefabs

        static void BuildWispPrefab()
        {
            var root = new GameObject("Wisp");
            var v = root.AddComponent<WispView>();
            // Items layer, above ground items (spec 2.1 step 6: items, wisps on top of all objects)
            Set(v, "glow", Sr(root.transform, "Glow", 200, circle, "Items"));
            Set(v, "core", Sr(root.transform, "Core", 201, circle, "Items"));
            Set(v, "cargo", Sr(root.transform, "Cargo", 202, null, "Items"));
            Save(root, WorldPrefabDir + "/Wisp.prefab");
        }

        static void BuildLinkLinePrefab()
        {
            var root = new GameObject("LinkLine");
            var v = root.AddComponent<LinkLineView>();
            // under every sprite, like the reach circles (Ground layer, above the tilemap)
            var line = Sr(root.transform, "Line", 40, dash, "Ground");
            line.drawMode = SpriteDrawMode.Tiled;
            line.size = new Vector2(1f, LinkLineSync.WidthPx / ViewKit.Cell);
            line.color = LinkLineSync.Faint;
            var dot = Sr(root.transform, "Dot", 5, circle, "Overlay");
            dot.transform.localScale = Vector3.one * ViewKit.U(8f);
            dot.gameObject.SetActive(false);
            Set(v, "line", line);
            Set(v, "dot", dot);
            Save(root, WorldPrefabDir + "/LinkLine.prefab");
        }

        // ------------------------------------------------------------------ UI prefab

        static void SetPath(Object target, string path, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(path);
            if (p == null) { Debug.LogError($"[IdleGrounds] {target.GetType().Name}.{path} not found"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static LayoutElement Size(Component c, float w, float h)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w; le.preferredHeight = h;
            return le;
        }

        /// <summary>icon + (optional) item icon + label endpoint inside a row.</summary>
        static void EndpointUi(Transform row, LinkRow lr, string field, string name)
        {
            var box = NewUi(name, row);
            Row(box.gameObject, 0, 0, 4);
            var icon = Icon(box, "Icon", Emoji("bld_workbench"), 28);
            var item = Icon(box, "Item", Emoji("item_wood"), 22);
            var text = Label(box, "Text", "Workbench", 17f, UiPalette.Text, true);
            SetPath(lr, field + ".icon", icon);
            SetPath(lr, field + ".item", item);
            SetPath(lr, field + ".text", text);
        }

        static void BuildLinkEditorPrefab()
        {
            var root = Stretch(NewUi("LinkEditor", null));
            var view = root.gameObject.AddComponent<LinkEditorView>();

            var panel = Chip(root, "Panel", new Color(UiPalette.Bg2.r, UiPalette.Bg2.g, UiPalette.Bg2.b, 0.97f), UiPalette.Line, raycast: true);
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(1f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.sizeDelta = new Vector2(-24f, 100f);
            prt.anchoredPosition = new Vector2(0f, 68f);
            Col(panel.gameObject, 14, 8);
            Fit(panel.gameObject, false, true);

            var title = Label(prt, "Title", "Wisp Lantern - links run in order, one per beat", 19f, UiPalette.Text, true);

            var header = NewUi("Header", prt);
            Row(header.gameObject, 0, 0, 14);

            // Add link button
            var add = Chip(header, "AddLink", UiPalette.Panel, UiPalette.Line, raycast: true);
            Row(add.gameObject, 12, 6, 8);
            var abtn = add.gameObject.AddComponent<Button>();
            abtn.targetGraphic = add;
            var ac = abtn.colors; ac.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f); abtn.colors = ac;
            Icon(add.rectTransform, "Icon", Emoji("ui_add"), 24);
            Label(add.rectTransform, "Label", "Add link", 19f, UiPalette.Text, true);

            var hint = Label(header, "Hint", "", 18f, UiPalette.Muted, true);
            var warn = Label(header, "Warn", "", 18f, UiPalette.Danger, true);
            var tip = Label(prt, "Tip", "", 16f, UiPalette.Muted, false);

            var grid = NewUi("Grid", prt);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(480f, 44f);
            g.spacing = new Vector2(10f, 8f);
            g.constraint = GridLayoutGroup.Constraint.Flexible;
            g.childAlignment = TextAnchor.UpperLeft;

            // row template
            var rowImg = Chip(grid, "RowTemplate", UiPalette.Panel, UiPalette.Line, raycast: true);
            var rrt = rowImg.rectTransform;
            var hl = Row(rowImg.gameObject, 10, 4, 8);
            var lr = rowImg.gameObject.AddComponent<LinkRow>();
            var dotImg = Icon(rrt, "Dot", circle, 16);
            dotImg.color = LinkLineView.DotGrey;
            var idx = Label(rrt, "Index", "1.", 16f, UiPalette.Muted, true);
            Size(idx, 26, 24);
            EndpointUi(rrt, lr, "source", "Source");
            Label(rrt, "Arrow", ">", 18f, UiPalette.Muted, true);
            EndpointUi(rrt, lr, "target", "Target");
            var flex = NewUi("Spacer", rrt);
            flex.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var rm = Chip(rrt, "Remove", UiPalette.Panel2, UiPalette.Danger, raycast: true);
            Size(rm.rectTransform, 32, 32);
            var rbtn = rm.gameObject.AddComponent<Button>();
            rbtn.targetGraphic = rm;
            var rl = Label(rm.rectTransform, "X", "x", 20f, UiPalette.Danger, true);
            Stretch(rl.rectTransform);
            rl.alignment = TextAlignmentOptions.Center;
            SetPath(lr, "dot", dotImg);
            SetPath(lr, "indexText", idx);
            SetPath(lr, "remove", rbtn);
            rowImg.gameObject.SetActive(false);

            Set(view, "panel", panel.gameObject);
            Set(view, "titleText", title);
            Set(view, "addButton", abtn);
            Set(view, "hintText", hint);
            Set(view, "warnText", warn);
            Set(view, "tipText", tip);
            Set(view, "grid", grid);
            Set(view, "rowTemplate", lr);
            hint.gameObject.SetActive(false);
            warn.gameObject.SetActive(false);
            tip.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            Save(root.gameObject, UiPrefabDir + "/LinkEditor.prefab");
        }

        // ------------------------------------------------------------------ scene wiring

        public static void InstallIntoActiveScene()
        {
            var runner = Object.FindFirstObjectByType<GameRunner>();
            var hand = Object.FindFirstObjectByType<HandController>();
            var fx = Object.FindFirstObjectByType<FxService>();
            var bc = Object.FindFirstObjectByType<BuildController>();
            if (runner == null || hand == null || bc == null) { Debug.LogError("[IdleGrounds] Install the M2 core loop + M3 buildings first."); return; }

            var runtime = FindOrCreate("Runtime", null).transform;

            var wispsRoot = FindOrCreate("Wisps", runtime);
            var ws = Ensure<WispViewSync>(wispsRoot);
            Set(ws, "runner", runner);
            Set(ws, "prefab", AssetDatabase.LoadAssetAtPath<WispView>(WorldPrefabDir + "/Wisp.prefab"));
            Set(ws, "root", wispsRoot.transform);

            var linksRoot = FindOrCreate("Links", runtime);
            var ls = Ensure<LinkLineSync>(linksRoot);
            var oldBand = linksRoot.transform.Find("RubberBand");
            if (oldBand != null) Object.DestroyImmediate(oldBand.gameObject);
            var band = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WorldPrefabDir + "/LinkLine.prefab"), linksRoot.transform);
            band.name = "RubberBand";
            foreach (var sr in band.GetComponentsInChildren<SpriteRenderer>(true)) { sr.sortingLayerName = "Overlay"; sr.sortingOrder += 10; }
            band.SetActive(false);

            var canvas = GameObject.Find("UI/Canvas");
            if (canvas == null) { Debug.LogError("[IdleGrounds] UI/Canvas missing."); return; }
            var old = canvas.transform.Find("LinkEditor");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabDir + "/LinkEditor.prefab"), canvas.transform);
            var editor = go.GetComponent<LinkEditorView>();
            Set(editor, "runner", runner);
            Set(editor, "hand", hand);
            Set(editor, "fx", fx);
            Set(editor, "rubberBand", band.GetComponent<LinkLineView>());

            Set(ls, "runner", runner);
            Set(ls, "prefab", AssetDatabase.LoadAssetAtPath<LinkLineView>(WorldPrefabDir + "/LinkLine.prefab"));
            Set(ls, "build", bc);
            Set(ls, "editor", editor);
            Set(ls, "root", linksRoot.transform);

            Set(bc, "linkEditor", editor);

            var bar = Object.FindFirstObjectByType<BottomBarView>();
            if (bar != null) bar.transform.SetAsLastSibling();
            var cursor = canvas.transform.Find("HandCursor");
            if (cursor != null) cursor.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
