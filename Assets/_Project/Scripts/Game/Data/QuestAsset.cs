using System;
using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game.Data
{
    [CreateAssetMenu(menuName = "Idle Grounds/Quest")]
    public class QuestAsset : ScriptableObject { public QuestDef def = new QuestDef(); public Sprite icon; }
}
