using System;
using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game.Data
{
    [CreateAssetMenu(menuName = "Idle Grounds/Vow")]
    public class VowAsset : ScriptableObject { public VowDef def = new VowDef(); public Sprite icon; }
}
