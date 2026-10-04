using System;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// Presentation events (engine-systems §17-§18.4). Everything is suppressed
    /// while <see cref="Muted"/>. Wisp events also carry Spirit Bridge sky
    /// wisps (<see cref="SkyWisp"/>, world px): Launched/Returned/Dropped name the
    /// sending Island, Arrived the Island it landed on. M1 raises: GroundDropped, SoundRequested,
    /// NodeSpawned, NodeHit, NodeDepleted. The rest are declared for M2+.
    /// </summary>
    public sealed class SimEvents
    {
        public bool Muted;

        public event Action<string, string, int, double, double> GroundDropped;   // area, item, qty, x, y
        public event Action<string, string> SoundRequested;                       // name, area (may be null)
        public event Action<string, Node> NodeSpawned;                            // area, node
        public event Action<string, Node, bool> NodeHit;                          // area, node, isAuto
        public event Action<string, Node> NodeDepleted;                           // area, node

        // ---- M2+ (declared, not yet raised) ----
#pragma warning disable 67
        public event Action<string, Building> BuildingPlaced;
        public event Action<string, Building> BuildingCompleted;
        public event Action<string, Building> BuildingDemolished;
        public event Action<string, Building, string, int> BatchStarted;
        public event Action<string, Building, string, int> BatchFinished;
        public event Action<string, Wisp> WispLaunched;
        public event Action<string, Wisp> WispArrived;
        public event Action<string, Wisp> WispReturned;
        public event Action<string, Wisp> WispDropped;
        public event Action<string, Enemy> EnemySpawned;
        public event Action<string, Enemy> EnemyHit;
        public event Action<string, Enemy> EnemyKilled;
        public event Action<int, string> DragonStageAdvanced;
        public event Action DragonAwakened;
        public event Action<string, double> BlessingStarted;
        public event Action<double> CombatBuffStarted;
        public event Action<string, string> UpgradeApplied;
        public event Action<string> RegionUnlocked;
        public event Action<string> QuestClaimed;
        public event Action AscendPromptRequested;
        public event Action RunReset;
#pragma warning restore 67
        /// <summary>M5: (area, pavilion) after a disciple joined.</summary>
        public event Action<string, Building> DiscipleRecruited;

        internal void RaiseGroundDropped(string area, string item, int qty, double x, double y)
        { if (!Muted) GroundDropped?.Invoke(area, item, qty, x, y); }
        internal void RaiseSound(string name, string area)
        { if (!Muted) SoundRequested?.Invoke(name, area); }
        internal void RaiseNodeSpawned(string area, Node n)
        { if (!Muted) NodeSpawned?.Invoke(area, n); }
        internal void RaiseNodeHit(string area, Node n, bool isAuto)
        { if (!Muted) NodeHit?.Invoke(area, n, isAuto); }
        internal void RaiseNodeDepleted(string area, Node n)
        { if (!Muted) NodeDepleted?.Invoke(area, n); }
        // M3
        internal void RaiseBuildingPlaced(string area, Building b)
        { if (!Muted) BuildingPlaced?.Invoke(area, b); }
        internal void RaiseBuildingCompleted(string area, Building b)
        { if (!Muted) BuildingCompleted?.Invoke(area, b); }
        internal void RaiseBuildingDemolished(string area, Building b)
        { if (!Muted) BuildingDemolished?.Invoke(area, b); }
        internal void RaiseBatchStarted(string area, Building b, string item, int qty)
        { if (!Muted) BatchStarted?.Invoke(area, b, item, qty); }
        internal void RaiseBatchFinished(string area, Building b, string item, int qty)
        { if (!Muted) BatchFinished?.Invoke(area, b, item, qty); }
        // M4
        internal void RaiseWispLaunched(string area, Wisp w)
        { if (!Muted) WispLaunched?.Invoke(area, w); }
        internal void RaiseWispArrived(string area, Wisp w)
        { if (!Muted) WispArrived?.Invoke(area, w); }
        internal void RaiseWispReturned(string area, Wisp w)
        { if (!Muted) WispReturned?.Invoke(area, w); }
        internal void RaiseWispDropped(string area, Wisp w)
        { if (!Muted) WispDropped?.Invoke(area, w); }
        internal void RaiseAscendPrompt()
        { if (!Muted) AscendPromptRequested?.Invoke(); }
        // M7 — RunReset: the state object was replaced (ascension / load); views rebuild from scratch.
        internal void RaiseRunReset()
        { if (!Muted) RunReset?.Invoke(); }
        // M5
        internal void RaiseDragonStageAdvanced(int stage, string text)
        { if (!Muted) DragonStageAdvanced?.Invoke(stage, text); }
        internal void RaiseDragonAwakened()
        { if (!Muted) DragonAwakened?.Invoke(); }
        internal void RaiseBlessingStarted(string kind, double until)
        { if (!Muted) BlessingStarted?.Invoke(kind, until); }
        internal void RaiseCombatBuffStarted(double until)
        { if (!Muted) CombatBuffStarted?.Invoke(until); }
        internal void RaiseUpgradeApplied(string area, string type)
        { if (!Muted) UpgradeApplied?.Invoke(area, type); }
        internal void RaiseRegionUnlocked(string area)
        { if (!Muted) RegionUnlocked?.Invoke(area); }
        internal void RaiseQuestClaimed(string id)
        { if (!Muted) QuestClaimed?.Invoke(id); }
        // M6 — EnemySpawned (fox spawn / boar lure), EnemyHit (struck, survived), EnemyKilled (removed, loot dropped)
        internal void RaiseEnemySpawned(string area, Enemy e)
        { if (!Muted) EnemySpawned?.Invoke(area, e); }
        internal void RaiseEnemyHit(string area, Enemy e)
        { if (!Muted) EnemyHit?.Invoke(area, e); }
        internal void RaiseEnemyKilled(string area, Enemy e)
        { if (!Muted) EnemyKilled?.Invoke(area, e); }
        internal void RaiseDiscipleRecruited(string area, Building b)
        { if (!Muted) DiscipleRecruited?.Invoke(area, b); }
    }
}
