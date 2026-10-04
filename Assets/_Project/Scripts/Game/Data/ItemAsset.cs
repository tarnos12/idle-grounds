using System;
using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game.Data
{
    [CreateAssetMenu(menuName = "Idle Grounds/Item")]
    public class ItemAsset : ScriptableObject
    {
        public ItemDef def = new ItemDef();
        public Sprite icon;
    }
}
