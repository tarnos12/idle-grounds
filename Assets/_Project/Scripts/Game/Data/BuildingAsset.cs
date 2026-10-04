using System;
using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game.Data
{
    [CreateAssetMenu(menuName = "Idle Grounds/Building")]
    public class BuildingAsset : ScriptableObject
    {
        public BuildingDef def = new BuildingDef();
        public Sprite icon;
        /// <summary>Delivered animated/state variants (bld_KEY_working, _unpaired ...) by suffix; stored for later use.</summary>
        public List<SpriteEntry> variants = new List<SpriteEntry>();
        [Tooltip("Optional prefab override; null = use the family default.")]
        public GameObject prefabOverride;
    }
}
