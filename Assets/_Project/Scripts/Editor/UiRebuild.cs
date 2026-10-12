using UnityEditor;
using UnityEngine;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// "Idle Grounds/UI/Rebuild UI Prefabs + Skins": rebuilds every UI prefab from its builder (no world prefabs, no scene
    /// install) and re-applies the UI skin roles and button skins, so a UI builder edit shows up in the existing scene instances
    /// (prefab file IDs stay stable, so the scene wiring is untouched).
    /// </summary>
    public static class UiRebuild
    {
        [MenuItem("Idle Grounds/UI/Rebuild UI Prefabs + Skins")]
        public static void RebuildAll()
        {
            CoreLoopBuilder.BuildUiPrefabsOnly();
            BuildingsBuilder.BuildUiPrefabsOnly();
            LogisticsBuilder.BuildUiPrefabsOnly();
            ProgressionBuilder.BuildUiPrefabsOnly();
            IslandsBuilder.BuildUiPrefabsOnly();
            MetaBuilder.BuildAllPrefabs();
            UiSkinRoles.Apply();
            UiButtonRoles.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log("[IdleGrounds] UI prefabs rebuilt + skins applied.");
        }
    }
}
