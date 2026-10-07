namespace IdleGrounds.Sim
{
    /// <summary>Tag of a dropGround call (§4.1). None = untagged.</summary>
    public enum GroundTag { None, Manual, Crafted, Gen }

    /// <summary>Set by NodeSystem.Harvest while a PLAYER swing drops its yield (engine.js manualSrc).</summary>
    public struct ManualSource
    {
        public int nodeId;
        public int grace;
    }

    /// <summary>
    /// Shared simulation context: config, state, injected clock/rng, events,
    /// engine-module flags, and the systems (wired by <see cref="Simulation"/>).
    /// </summary>
    public sealed class SimContext
    {
        public readonly GameConfig Config;
        public GameState State;
        public readonly IClock Clock;
        public readonly IRng Rng;
        public readonly SimEvents Events;
        public readonly Timing Timing;

        /// <summary>JS `autoHarvesting` — true only while automationTick harvests.</summary>
        public bool AutoHarvesting;
        /// <summary>Skip tick step 10 (ground settle / collider push) — tests keep item positions put.</summary>
        public bool NoGroundPhysics;
        /// <summary>JS `manualSrc` (null when not inside a player harvest).</summary>
        public ManualSource? ManualSrc;

        public World World;
        public OccupancyGrid Occupancy;
        public HandSystem Hand;
        public GroundSystem Ground;
        public NodeSystem Nodes;
        public FieldGeneratorSystem FieldGenerators;
        public FuelSystem Fuel;
        public ConverterSystem Converters;
        public BuildingSystem Buildings;
        public LogisticsSystem Logistics;
        public DragonSystem Dragon;
        public UpgradeSystem Upgrades;
        public ProgressionSystem Progression;
        public PavilionSystem Pavilions;
        public CombatSystem Combat;
        public AutomationSystem Automation;

        public SimContext(GameConfig cfg, GameState state, IClock clock, IRng rng)
        {
            Config = cfg.IsBuilt ? cfg : cfg.Build();
            State = state;
            Clock = clock;
            Rng = rng;
            Events = new SimEvents();
            Timing = new Timing(Config);
        }

        /// <summary>The flow ledger of the current state (never null: GameState initialises it, the codec sanitises it).</summary>
        public FlowLedger Flow => State.flow;

        public double Now => Clock.NowMs;
        public int Cell => Config.grid.cell;
        public int N => Config.grid.cells;
        public int PlayPx => Config.grid.PlayPx;
    }
}
