using System;
using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game.Data
{
    [CreateAssetMenu(menuName = "Idle Grounds/Upgrade Node")]
    public class UpgradeNodeAsset : ScriptableObject { public UpgradeNodeDef def = new UpgradeNodeDef(); public Sprite icon; }
}
