using System;
using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game.Data
{
    [CreateAssetMenu(menuName = "Idle Grounds/Dragon Stage")]
    public class DragonStageAsset : ScriptableObject { public DragonStageDef def = new DragonStageDef(); public Sprite icon; }
}
