namespace IdleGrounds.Sim
{
    /// <summary>
    /// The single interface the Unity layer talks to (ADR 0001). Owns the
    /// systems; <see cref="Tick"/> runs gameTick in the engine-systems §2.3
    /// order. Steps 1-6 (step 6: stoker only) and 10 run; the logistics
    /// parts of step 6 and steps 7-9 are ordered hooks for M4/M5.
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
            Ctx.Fuel = new FuelSystem(Ctx);
            Ctx.Converters = new ConverterSystem(Ctx);
            Ctx.Buildings = new BuildingSystem(Ctx);
            Ctx.Hand.FeedBuilding = Ctx.Buildings.FeedBuilding;
        }

        public BuildingSystem Buildings => Ctx.Buildings;
        public ConverterSystem Converters => Ctx.Converters;
        public FuelSystem Fuel => Ctx.Fuel;

        /// <summary>Boot step 2 (§2.1): initArea for every area (locked ones too).</summary>
        public void InitAllAreas()
        {
            foreach (var r in Config.regions) Ctx.Nodes.InitArea(r.key);
            Ctx.Buildings.SetupStarterNetwork();     // §16 — no-op once starterPlaced
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

        // ---- tick steps 4-9 ----
        // generator buildings §8.6
        bool TickGeneratorBuildings(string areaKey, double now) => Ctx.Buildings.TickGenBuildings(areaKey, now);
        // converters + burner fuel §9-10
        bool TickConverters(string areaKey, double now) => Ctx.Converters.Tick(areaKey, now);
        // per building — Gathering Stone eject+vacuum §11.2 (M4), lantern beat §11.5 (M4), stoker §10.4, pavilion §12.4 (M5)
        bool TickBuildingLogistics(string areaKey, double now) => Ctx.Buildings.TickLogistics(areaKey, now);
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

        // ---- M3 buildings & converters ----

        /// <summary>Place an unpaid ghost (`placeBuilding`); null if locked / invalid spot.</summary>
        public Building PlaceGhost(string areaKey, string type, int row, int col) =>
            Ctx.Buildings.PlaceGhost(areaKey, type, row, col);

        /// <summary>Why PlaceGhost would refuse ("Locked", "Off the edge", "Blocked"…); null = allowed.</summary>
        public string PlaceReason(string areaKey, string type, int row, int col) =>
            Ctx.Buildings.PlaceGhostReason(areaKey, type, row, col);

        /// <summary>`demolishBuilding` — refunds drop at the footprint centre; false for Altar/Dragon/unknown.</summary>
        public bool Demolish(string areaKey, int buildingId) => Ctx.Buildings.Demolish(areaKey, buildingId);

        /// <summary>`setRecipe` — switch a converter's active recipe (refunds per §9.2).</summary>
        public bool SetRecipe(string areaKey, int buildingId, int recipeIndex) =>
            Ctx.Converters.SetRecipe(areaKey, buildingId, recipeIndex);

        /// <summary>Left-click withdraw from a storehouse / seal / gathering stone / stoker; returns items taken.</summary>
        public int Withdraw(string areaKey, int buildingId, int n = 1)
        {
            var b = State.Area(areaKey)?.BuildingById(buildingId);
            return b == null || !b.built ? 0 : Ctx.Hand.WithdrawFromBuilding(b, n);
        }

        /// <summary>Building whose footprint holds the area-local px point (racks excluded).</summary>
        public Building BuildingAt(string areaKey, double x, double y) => Ctx.Buildings.BuildingAtPx(areaKey, x, y);

        /// <summary>`buildingStatus` — null for ghosts / no status.</summary>
        public BuildingStatusInfo BuildingStatus(string areaKey, Building b) => Ctx.Buildings.Status(areaKey, b);

        /// <summary>Converter face/panel read-out (inputs have/need/cap, craftable, progress, fuel); null for non-converters.</summary>
        public ConverterFace ConverterFace(string areaKey, Building b) => Ctx.Converters.Face(areaKey, b);

        /// <summary>Remaining build cost of a ghost (`buildingNeeds`).</summary>
        public ItemCounts BuildingNeeds(Building b) => Ctx.Buildings.Needs(b);

        public bool IsBuildingUnlocked(string type) => Ctx.Buildings.IsBuildingUnlocked(type);
    }
}
