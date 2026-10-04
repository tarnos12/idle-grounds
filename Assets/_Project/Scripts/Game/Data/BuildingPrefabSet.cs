using System;
using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game.Data
{
    /// <summary>
    /// Data-driven building type → view prefab table (ADR 0001: prefab variants per building family).
    /// Resolution order: <see cref="BuildingAsset.prefabOverride"/> → explicit <see cref="entries"/> row
    /// → family fallback from the def flags → <see cref="fallback"/> (the base Building prefab).
    /// </summary>
    [CreateAssetMenu(menuName = "Idle Grounds/Building Prefab Set")]
    public class BuildingPrefabSet : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string type;
            public GameObject prefab;
        }

        [Tooltip("Base Building prefab (default face).")]
        public GameObject fallback;
        public GameObject converter, burner, storehouse, wardingSeal, gatheringStone, furnaceSpirit,
            wispLantern, generator, pavilion, ascensionGate, altar, dragon;
        [Tooltip("Per-type rows (filled by the editor builder; edit freely).")]
        public List<Entry> entries = new List<Entry>();

        public GameObject Resolve(GameDatabase db, BuildingDef def)
        {
            if (def == null) return fallback;
            var asset = db != null ? db.FindBuilding(def.key) : null;
            if (asset != null && asset.prefabOverride != null) return asset.prefabOverride;
            foreach (var e in entries) if (e.type == def.key && e.prefab != null) return e.prefab;
            return Or(Family(def));
        }

        GameObject Or(GameObject g) => g != null ? g : fallback;

        /// <summary>Family prefab from the def's flags.</summary>
        public GameObject Family(BuildingDef def)
        {
            if (def.key == "center") return altar;
            if (def.key == "dragon") return dragon;
            if (def.gate) return ascensionGate;
            if (def.IsConverter) return def.fuel ? burner : converter;
            if (def.key == "storehouse") return storehouse;
            if (def.seal.enabled) return wardingSeal;
            if (def.gather.enabled) return gatheringStone;
            if (def.stoker.enabled) return furnaceSpirit;
            if (def.lantern.enabled) return wispLantern;
            if (def.gen.enabled) return generator;
            if (def.roster.enabled) return pavilion;
            return fallback;
        }
    }
}
