namespace IdleGrounds.Sim
{
    /// <summary>
    /// The single interface the Unity layer talks to (ADR 0001). Owns the
    /// systems; <see cref="Tick"/> runs gameTick in the engine-systems §2.3
    /// order. M1 implements steps 1-3 and 10; steps 4-9 are ordered no-op
    /// hooks for M2+.
    /// </summary>
    public sealed class Simulation
    {
        public readonly SimContext Ctx;

        public GameConfig Config => Ctx.Config;
        public GameState State => Ctx.State;
        public SimEvents Events => Ctx.Events;
        public World World => Ctx.World;
        public HandSystem Hand => Ctx.Hand;
        public GroundSystem Ground => Ctx.Ground;
        public NodeSystem Nodes => Ctx.Nodes;
        public OccupancyGrid Occupancy => Ctx.Occupancy;
        public FieldGeneratorSystem FieldGenerators => Ctx.FieldGenerators;
        public Timing Timing => Ctx.Timing;

        /// <summary>Suppress every event (offline replay).</summary>
        public bool Muted { get => Ctx.Events.Muted; set => Ctx.Events.Muted = value; }

        /// <summary>Offline replay mode: skips ground physics (JS offlineSim/offlineReplay).</summary>
        public bool OfflineSim { get => Ctx.OfflineSim; set { Ctx.OfflineSim = value; Ctx.OfflineReplay = value; } }

        public Simulation(GameConfig cfg, GameState state, IClock clock, IRng rng)
        {
            Ctx = new SimContext(cfg, state, clock, rng);
            Ctx.World = new World(Ctx);
            Ctx.Occupancy = new OccupancyGrid(Ctx);
            Ctx.Hand = new HandSystem(Ctx);
            Ctx.Ground = new GroundSystem(Ctx);
            Ctx.Nodes = new NodeSystem(Ctx);
            Ctx.FieldGenerators = new FieldGeneratorSystem(Ctx);
        }

        /// <summary>Boot step 2 (§2.1): initArea for every area (locked ones too).</summary>
        public void InitAllAreas()
        {
            foreach (var r in Config.regions) Ctx.Nodes.InitArea(r.key);
            // TODO(M2): setupStarterNetwork() (§16) after initArea.
        }

        /// <summary>`gameTick()` engine.js:2325. Returns the repaint hint.</summary>
        public bool Tick()
        {
            double now = Ctx.Now;
            Ctx.Timing.BeginTick(now);
            bool changed = false;
            foreach (var reg in Config.regions)
            {
                string k = reg.key;
                if (!Ctx.World.IsAreaUnlocked(k)) continue;     // locked areas freeze
                changed |= Ctx.Nodes.TickSurfaceDives(k, now);  // 1
                changed |= Ctx.Nodes.TickRespawnQueue(k, now);  // 2
                changed |= Ctx.FieldGenerators.Tick(k, now);    // 3
                changed |= TickGeneratorBuildings(k, now);      // 4  M2+
                changed |= TickConverters(k, now);              // 5  M2+
                changed |= TickBuildingLogistics(k, now);       // 6  M2+
                changed |= TickWisps(k, now);                   // 7  M2+
                changed |= TickEnemies(k, now);                 // 8  M2+
                changed |= TickDragonScales(k, now);            // 9  M2+
                if (!Ctx.OfflineSim)                            // 10 ground physics
                {
                    if (Ctx.Ground.SettleGround(k) > 0) changed = true;
                    if (Ctx.Ground.PushOutOfColliders(k) > 0) changed = true;
                }
            }
            return changed;
        }

        // ---- M2+ hooks (ordered no-ops) ----
        // M2+: generator buildings §8.6
        bool TickGeneratorBuildings(string areaKey, double now) => false;
        // M2+: converters + burner fuel §9-10
        bool TickConverters(string areaKey, double now) => false;
        // M2+: per building — Gathering Stone eject+vacuum §11.2, lantern beat §11.5, stoker §10.4, pavilion §12.4
        bool TickBuildingLogistics(string areaKey, double now) => false;
        // M2+: wisp flights/arrivals §11.6
        bool TickWisps(string areaKey, double now) => false;
        // M2+: enemies spawn + wander §7
        bool TickEnemies(string areaKey, double now) => false;
        // M2+: dragon scales (center, after awakening) §12.2
        bool TickDragonScales(string areaKey, double now) => false;

        /// <summary>`automationTick()` (§13.1) — M2+.</summary>
        public int AutomationTick() => 0;

        // ---- commands ----

        /// <summary>Player/automation swing on a node (`harvestNode`).</summary>
        public bool Harvest(string areaKey, int nodeId, bool isAuto = false, bool held = false) =>
            Ctx.Nodes.Harvest(areaKey, nodeId, isAuto, held);

        /// <summary>Left-hold vacuum step (`suctionStep`); filter = type-lock item or null.</summary>
        public SuctionResult Suction(string areaKey, double x, double y, double radius, string itemFilter = null) =>
            Ctx.Ground.SuctionStep(areaKey, x, y, radius, itemFilter);

        /// <summary>Q (+1) / E (−1) hand rotation.</summary>
        public string RotateHand(int dir) => Ctx.Hand.Rotate(dir);

        /// <summary>Right-click dispatcher (`dropFromHand`) — building feeding is an M2 hook.</summary>
        public DropResult DropFromHand(string areaKey, double x, double y, bool noGround = false) =>
            Ctx.Hand.DropFromHand(areaKey, x, y, noGround);
    }
}
