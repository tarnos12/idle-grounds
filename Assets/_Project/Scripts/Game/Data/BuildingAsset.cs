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
        [Tooltip("icon is delivered pixel art (Art/Incoming): drawn as the building body at native size instead of a grey panel + small icon.")]
        public bool hasRealArt;
        [Tooltip("Animation frames of the body art when it is a strip (null = static); icon = frame 0.")]
        public Sprite[] frames;
        /// <summary>Delivered animated/state variants (bld_KEY_working, _unpaired ...) by suffix; stored for later use.</summary>
        public List<SpriteEntry> variants = new List<SpriteEntry>();
        [Tooltip("Optional prefab override; null = use the family default.")]
        public GameObject prefabOverride;
    }
}
