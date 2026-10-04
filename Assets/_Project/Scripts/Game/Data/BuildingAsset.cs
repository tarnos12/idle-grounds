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
        [Tooltip("Optional prefab override; null = use the family default.")]
        public GameObject prefabOverride;
    }
}
