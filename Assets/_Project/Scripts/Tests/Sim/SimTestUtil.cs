using System.IO;
using IdleGrounds.Sim;

namespace IdleGrounds.Sim.Tests
{
    static class SimTestUtil
    {
        public const string DataPath = "Assets/_Project/Data/Source/game-data.json";
        static string _json;

        public static string Json => _json ??= File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), DataPath));

        /// <summary>
        /// Fresh config each call (tests may mutate it), with TEST scaling ON (timers ×0.2, costs ×0.5): the unit
        /// tests' expected numbers are written for it. The data itself ships with TEST off (ADR 0004) — use
        /// <see cref="LoadDataConfig"/> for the config exactly as the game loads it.
        /// </summary>
        public static GameConfig LoadConfig()
        {
            var cfg = GameConfigJson.Load(Json);
            cfg.test.enabled = true;
            return cfg;
        }

        /// <summary>Fresh config exactly as shipped (TEST off since balance pass 1).</summary>
        public static GameConfig LoadDataConfig() => GameConfigJson.Load(Json);

        public static Simulation NewSim(out ManualClock clock, ulong seed = 12345, GameConfig cfg = null, bool init = true)
        {
            cfg ??= LoadConfig();
            clock = new ManualClock(1_000_000);
            var state = GameState.CreateInitial(cfg, clock.NowMs);
            var sim = new Simulation(cfg, state, clock, new XorShiftRng(seed));
            if (init) sim.InitAllAreas();
            return sim;
        }

        public static int CountGround(AreaState a, string item)
        {
            int n = 0;
            foreach (var g in a.ground) if (g.item == item) n++;
            return n;
        }

        public static int CountSpawner(AreaState a, string kind)
        {
            int n = 0;
            foreach (var x in a.nodes) if (x.spawnerKind == kind) n++;
            return n;
        }

        public static Node FirstOfKind(AreaState a, string kind)
        {
            foreach (var x in a.nodes) if (x.kind == kind) return x;
            return null;
        }
    }
}
