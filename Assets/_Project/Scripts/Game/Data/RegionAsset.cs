using System;
using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game.Data
{
    [CreateAssetMenu(menuName = "Idle Grounds/Region")]
    public class RegionAsset : ScriptableObject
    {
        public RegionDef def = new RegionDef();
        public Sprite icon;
        public Sprite actionIcon;
        public Color groundColor = new Color(0.2f, 0.35f, 0.2f, 1f);
        /// <summary>Keys: node_&lt;kind&gt;, fix_&lt;kind&gt;, enemy, enemy_bait.</summary>
        public List<SpriteEntry> sprites = new List<SpriteEntry>();

        public SpriteEntry FindEntry(string key)
        {
            foreach (var e in sprites) if (e != null && e.key == key) return e;
            return null;
        }

        public Sprite Find(string key)
        {
            foreach (var e in sprites) if (e != null && e.key == key) return e.sprite;
            return null;
        }
    }
}
