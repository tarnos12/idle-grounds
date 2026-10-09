using System.Collections.Generic;
using IdleGrounds.Game;
using IdleGrounds.Game.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// "Idle Grounds/UI/Apply UI Skin Roles": attaches/updates <see cref="UiSkin"/> on the explicit panel Images below
    /// (jade = main modals / side panels, scroll = help/welcome parchment, dark = tooltips / chips / small popups),
    /// applies the delivered art to them (so the saved prefabs/scene already show it) and saves.
    /// The builders attach the same components when rebuilding prefabs, so this menu is for existing assets.
    /// </summary>
    public static class UiSkinRoles
    {
        const string UiPrefabDir = "Assets/_Project/Prefabs/UI/";
        const string DbPath = "Assets/_Project/Data/GameDatabase.asset";
        public const string Jade = "ui_panel_jade", Scroll = "ui_panel_scroll", Dark = "ui_panel_dark",
            Quest = "ui_questpanel_frame", Tip = "ui_tooltip_frame", HandChip = "ui_handchip_frame", Bar = "ui_bottombar_bg";

        /// <summary>prefab -> (path under the prefab root, "name:" prefix = deep search by name, skin key)</summary>
        public static readonly (string prefab, string path, string key)[] Roles =
        {
            ("AscendDialog", "Modal/Box", Jade),
            ("DragonDialog", "Modal/Box", Jade),
            ("UpgradeTree", "Modal/Box", Jade),
            ("UpgradeTree", "name:Tooltip", Tip),
            ("PerkShop", "Modal/Box", Jade),
            ("PostAscension", "Modal/Box", Jade),
            ("StatsPanel", "Modal/Box", Jade),
            ("ConfirmDialog", "Modal/Box", Jade),
            ("HelpModal", "Modal/Box", Scroll),
            ("WelcomeModal", "Modal/Box", Scroll),
            ("BuildMenu", "Panel", Jade),
            ("BridgePanel", "Panel", Jade),
            ("LinkEditor", "Panel", Jade),
            ("PavilionPanel", "Panel", Jade),
            ("QuestPanel", "Panel", Quest),
            ("QuestPanel", "Chip", Dark),
            ("BuildingTooltip", "Panel", Tip),
            ("Toast", "Chip", Dark),
            ("HandCursor", "Chip", HandChip),
            ("BottomBar", "", Bar),
        };

        [MenuItem("Idle Grounds/UI/Apply UI Skin Roles")]
        public static void Apply()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DbPath);
            int done = 0, art = 0, missing = 0;
            foreach (var group in Group())
            {
                string path = UiPrefabDir + group.Key + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning("UiSkinRoles: missing prefab " + path); missing++; continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var r in group.Value)
                    {
                        var t = r.path.Length == 0 ? root.transform : r.path.StartsWith("name:") ? FindDeep(root.transform, r.path.Substring(5)) : root.transform.Find(r.path);
                        if (t == null || t.GetComponent<Image>() == null) { Debug.LogWarning($"UiSkinRoles: {group.Key}/{r.path} not found / no Image"); missing++; continue; }
                        var s = UiSkin.Attach(t.gameObject, r.key);
                        if (s.Apply(db)) art++;
                        done++;
                    }
                    Fixups(group.Key, root.transform);
                    UiArtRoles.AttachAll(root.transform, db);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            // prefabs without a Roles entry still get pills / bars / checkbox / frames / scrollbars
            var named = new HashSet<string>();
            foreach (var r in Roles) named.Add(r.prefab);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { UiPrefabDir.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (named.Contains(System.IO.Path.GetFileNameWithoutExtension(path))) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try { if (UiArtRoles.AttachAll(root.transform, db) > 0) PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            // existing instances in the open scene(s): skin them too (prefab instances inherit; this covers overrides)
            int inScene = 0;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var sc0 = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!sc0.isLoaded) continue;
                foreach (var go in sc0.GetRootGameObjects())
                    UiArtRoles.AttachAll(go.transform, db, t => PrefabUtility.IsPartOfPrefabInstance(t.gameObject) && !PrefabUtility.IsAddedGameObjectOverride(t.gameObject));
            }
            var hc = Object.FindFirstObjectByType<HandController>();
            if (hc != null && hc.GetComponent<GameCursor>() == null) { hc.gameObject.AddComponent<GameCursor>(); EditorUtility.SetDirty(hc.gameObject); }
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!sc.isLoaded) continue;
                foreach (var go in sc.GetRootGameObjects())
                    foreach (var s in go.GetComponentsInChildren<UiSkin>(true))
                    { if (s.Apply(db)) { EditorUtility.SetDirty(s); EditorUtility.SetDirty(s.GetComponent<Image>()); } inScene++; }
            }
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"UiSkinRoles: {done} role(s) set on prefabs ({art} with real art), {inScene} UiSkin(s) refreshed in scene, {missing} problem(s).");
        }

        /// <summary>Keeps the existing prefabs in line with the builders: content clear of the 32 px jade frame.</summary>
        static void Fixups(string prefab, Transform root)
        {
            if (prefab == "HelpModal" || prefab == "PerkShop")
            {
                var box = root.Find("Modal/Box");
                var head = box != null ? box.Find("Header") as RectTransform : null;
                if (head != null) { head.sizeDelta = new Vector2(-72f, head.sizeDelta.y); head.anchoredPosition = new Vector2(0f, -34f); }
                var vp = box != null ? box.Find("Viewport") as RectTransform : null;
                if (vp != null)
                {
                    float top = prefab == "HelpModal" ? 100f : 168f;
                    vp.offsetMin = new Vector2(36f, 36f); vp.offsetMax = new Vector2(-36f, -top);
                }
            }
            if (prefab == "QuestPanel")
            {
                var lg = root.Find("Panel") != null ? root.Find("Panel").GetComponent<VerticalLayoutGroup>() : null;
                if (lg != null) lg.padding = new RectOffset(34, 34, 34, 34);
            }
        }

        static Dictionary<string, List<(string prefab, string path, string key)>> Group()
        {
            var d = new Dictionary<string, List<(string prefab, string path, string key)>>();
            foreach (var r in Roles)
            {
                if (!d.TryGetValue(r.prefab, out var l)) d[r.prefab] = l = new List<(string, string, string)>();
                l.Add(r);
            }
            return d;
        }

        static Transform FindDeep(Transform t, string name)
        {
            foreach (Transform c in t)
            {
                if (c.name == name) return c;
                var f = FindDeep(c, name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
