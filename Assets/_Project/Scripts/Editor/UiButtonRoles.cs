using System.Collections.Generic;
using System.Text;
using IdleGrounds.Game;
using IdleGrounds.Game.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// "Idle Grounds/UI/Apply Button Skins": puts <see cref="UiButtonSkin"/> (Standard / Primary / Close) on every labelled uGUI Button in the UI prefabs
    /// and the open scene, applies the delivered art so the saved assets already show it, and saves. Builders call <see cref="AttachFor"/> so rebuilds
    /// keep the roles. Skipped on purpose: cards / grid cells / chips / pills (own look, art comes later) and icon-only buttons.
    /// </summary>
    public static class UiButtonRoles
    {
        const string UiPrefabDir = "Assets/_Project/Prefabs/UI/";
        const string DbPath = "Assets/_Project/Data/GameDatabase.asset";

        static readonly HashSet<string> Skip = new HashSet<string> { "CardTemplate", "CellTemplate", "Chip", "Minimise", "ShrinePill" };
        static readonly HashSet<string> Primary = new HashSet<string> { "Claim", "Ascend", "OK", "Begin", "Continue", "Buy", "Recruit" };

        /// <summary>null = skip this button.</summary>
        public static UiButtonVariant? Classify(Button b)
        {
            string n = b.name;
            if (Skip.Contains(n)) return null;
            if (b.targetGraphic == null || !(b.targetGraphic is Image) || b.targetGraphic.color.a < 0.05f) return null;   // invisible hit area
            if (n == "Close") return UiButtonVariant.Close;
            if (Primary.Contains(n)) return UiButtonVariant.Primary;
            if (b.GetComponentInChildren<TMPro.TMP_Text>(true) == null) return null;   // icon-only / unlabelled
            return UiButtonVariant.Standard;
        }

        /// <summary>Builder hook: attach the role matching the button's name (no art applied here).</summary>
        public static void AttachFor(Button b)
        {
            var v = Classify(b);
            if (v.HasValue) UiButtonSkin.Attach(b.gameObject, v.Value);
        }

        [MenuItem("Idle Grounds/UI/Apply Button Skins")]
        public static void Apply()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DbPath);
            int total = 0, art = 0;
            var counts = new Dictionary<string, int>();
            var skipped = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { UiPrefabDir.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool dirty = false;
                    foreach (var b in root.GetComponentsInChildren<Button>(true))
                        dirty |= Process(b, db, root.name, counts, skipped, ref total, ref art);
                    if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            // scene-only buttons (prefab instances inherit from the prefab edits above)
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!sc.isLoaded) continue;
                foreach (var go in sc.GetRootGameObjects())
                    foreach (var b in go.GetComponentsInChildren<Button>(true))
                    {
                        if (PrefabUtility.IsPartOfPrefabInstance(b.gameObject)) continue;
                        if (Process(b, db, "Game scene", counts, skipped, ref total, ref art))
                        {
                            EditorUtility.SetDirty(b); EditorUtility.SetDirty(b.targetGraphic);
                            EditorSceneManager.MarkSceneDirty(sc);
                        }
                    }
            }
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            var sb = new StringBuilder();
            foreach (var kv in counts) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            Debug.Log($"UiButtonRoles: {total} button(s) skinned ({art} with real art): {sb}| skipped: {string.Join(", ", skipped)}");
        }

        static bool Process(Button b, GameDatabase db, string owner, Dictionary<string, int> counts, List<string> skipped, ref int total, ref int art)
        {
            var v = Classify(b);
            if (!v.HasValue) { skipped.Add(owner + "/" + b.name); return false; }
            var s = UiButtonSkin.Attach(b.gameObject, v.Value);
            if (s.Apply(db)) art++;
            total++;
            string k = v.Value.ToString();
            counts[k] = counts.TryGetValue(k, out var c) ? c + 1 : 1;

            // bottom-bar buttons: tall enough for the 16 px (2x8) button borders
            if (b.transform.parent != null && b.transform.parent.name == "BottomBar" && v.Value == UiButtonVariant.Standard)
            {
                var le = b.GetComponent<LayoutElement>();
                if (le == null) le = b.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 34f;
            }
            return true;
        }
    }
}
