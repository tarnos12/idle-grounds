using System.Collections.Generic;
using System.IO;
using IdleGrounds.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Keeps Game.unity small (GitHub's 100 MB file limit): every Island_&lt;key&gt; object - with all its builder-generated
    /// content (Ground / Coast / CliffRim / Shadow / Dressing / Underside / Veil / Boundary / ZoneOverlay ...) - lives in
    /// its own prefab asset, <c>Assets/_Project/Prefabs/Islands/Generated/Island_&lt;key&gt;.prefab</c>, and the scene only
    /// holds the prefab instance (position + unlocked/veil state as overrides).
    ///
    /// Builders that restructure Islands wrap their work in <c>using (IslandPrefabs.Edit()) { ... }</c>: on entry the
    /// Island instances are unpacked into plain scene objects (DestroyImmediate / AddChild work as before, hand-painted
    /// overrides such as Coast tiles are kept), on exit every Island is saved back to its prefab and reconnected.
    /// Scopes nest (only the outermost one saves). In Play mode nothing is unpacked or saved.
    /// </summary>
    public static class IslandPrefabs
    {
        public const string Dir = "Assets/_Project/Prefabs/Islands/Generated";
        public static string PathFor(string key) => Dir + "/Island_" + key + ".prefab";

        static int depth;

        public sealed class Scope : System.IDisposable
        {
            bool done;
            public void Dispose() { if (done) return; done = true; End(); }
        }

        /// <summary>Unpack the Islands for editing; dispose to save them back into their prefabs.</summary>
        public static Scope Edit() { Begin(); return new Scope(); }

        static void Begin()
        {
            if (depth++ == 0 && !EditorApplication.isPlayingOrWillChangePlaymode) UnpackAll();
        }

        static void End()
        {
            if (depth <= 0) { depth = 0; return; }
            if (--depth == 0 && !EditorApplication.isPlayingOrWillChangePlaymode) SaveAll();
        }

        static List<Island> Islands()
        {
            var list = new List<Island>();
            foreach (var i in Object.FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!string.IsNullOrEmpty(i.islandKey) && i.name == "Island_" + i.islandKey && i.gameObject.scene.IsValid()) list.Add(i);
            list.Sort((a, b) => string.CompareOrdinal(a.islandKey, b.islandKey));
            return list;
        }

        /// <summary>Turns every Island prefab instance back into plain scene objects (overrides kept).</summary>
        public static void UnpackAll()
        {
            foreach (var isl in Islands())
            {
                var go = isl.gameObject;
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                    if (root != null) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
            }
        }

        /// <summary>Saves every Island to its prefab asset and replaces it in the scene by a connected instance.</summary>
        public static void SaveAll()
        {
            Directory.CreateDirectory(Dir);
            int n = 0;
            foreach (var isl in Islands())
            {
                var go = isl.gameObject;
                if (PrefabUtility.IsPartOfPrefabInstance(go)) continue;      // already a (connected) instance
                PrefabUtility.SaveAsPrefabAssetAndConnect(go, PathFor(isl.islandKey), InteractionMode.AutomatedAction, out bool ok);
                if (!ok) Debug.LogError("[IdleGrounds] Could not save " + PathFor(isl.islandKey));
                else n++;
            }
            if (n > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("Idle Grounds/World/Save Islands To Prefabs (keeps Game.unity small)")]
        public static void SaveMenu()
        {
            using (Edit()) { }        // unpack (keeps any scene overrides, e.g. hand-painted Coast tiles) + save + reconnect
            Debug.Log("[IdleGrounds] Islands saved to " + Dir + " - save the scene.");
        }
    }
}
