using System;
using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game.Data
{
    [CreateAssetMenu(menuName = "Idle Grounds/Perk")]
    public class PerkAsset : ScriptableObject { public PerkDef def = new PerkDef(); public Sprite icon; }
}
