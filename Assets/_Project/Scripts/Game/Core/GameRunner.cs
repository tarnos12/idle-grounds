using System.Collections.Generic;
using IdleGrounds.Game.Data;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Owns the <see cref="Simulation"/>: builds config from the <see cref="GameDatabase"/>, loads the save
    /// (<see cref="SaveService.LoadOrNull"/>, fresh state otherwise) and runs <see cref="Simulation.Boot"/>
    /// (engine-systems §2.1; no offline catch-up — ADR 0002). Live: gameTick on a 50 ms
    /// accumulator, automationTick every 1000 ms; Island unlock state is mirrored onto the scene's
    /// <see cref="Island"/> objects (veils + camera bounds); Island positions come from the scene (ADR 0003). Ascension swaps the run in place (RunReset).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameRunner : MonoBehaviour
    {
        public const double TickMs = 50, AutomationMs = 1000;
        const int MaxTicksPerFrame = 4;           // the sim clamps longer gaps itself (TickGap)

        [SerializeField] GameDatabase database;
        [SerializeField] CameraController cameraController;
        [Tooltip("0 = seed from the clock.")]
        [SerializeField] ulong seed;

        public static GameRunner Instance { get; private set; }

        public GameDatabase Database => database;
        public Simulation Sim { get; private set; }
        public GameConfig Config { get; private set; }
        public GameState State => Sim?.State;
        public AreaSpace Space { get; private set; }
        /// <summary>Simulation clock (Unix ms) — the time base of node.hitAt, surfaceUntil, …</summary>
        public double SimNow => Sim != null ? Sim.Ctx.Now : 0;

        /// <summary>Fired after an ascension installed the fresh run (views already got Sim.Events.RunReset).</summary>
        public event System.Action Ascended;
        /// <summary>True when this boot started a brand-new run (no save file / a bad one).</summary>
        public bool FreshRun { get; private set; }

        readonly Dictionary<string, Island> islandObjects = new Dictionary<string, Island>();
        double tickAcc, autoAcc;

        void Awake()
        {
            Instance = this;
            if (database == null) { Debug.LogError("[GameRunner] No GameDatabase assigned."); enabled = false; return; }
            Config = database.BuildConfig();
            var clock = new SystemClock();
            var state = SaveService.LoadOrNull(Config, clock.NowMs);
            FreshRun = state == null;
            if (state == null) state = GameState.CreateInitial(Config, clock.NowMs);
            ulong s = seed != 0 ? seed : (ulong)clock.NowMs;
            Sim = new Simulation(Config, state, clock, new XorShiftRng(s));
            Sim.Events.RunReset += OnRunReset;
            // ADR 0003: the scene is the authority for where Islands float — read every Island transform
            foreach (var r in FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!string.IsNullOrEmpty(r.islandKey)) islandObjects[r.islandKey] = r;
            ApplyIslandOffsets();
            try { Sim.Boot(); }
            catch (System.Exception e) { Debug.LogException(e); }
            Space = new AreaSpace(Config, Sim);

            if (cameraController == null) cameraController = FindFirstObjectByType<CameraController>();
            SyncIslands(force: true);
        }

        /// <summary>Scene → sim: every Island GameObject's top-left corner becomes its world offset (px).</summary>
        void ApplyIslandOffsets()
        {
            if (Sim == null || islandObjects.Count == 0) return;
            int cell = Config.grid.cell;
            var list = new List<KeyValuePair<string, (double x, double y)>>();
            foreach (var kv in islandObjects) list.Add(new KeyValuePair<string, (double x, double y)>(kv.Key, kv.Value.OffsetPx(cell)));
            Sim.SetIslandOffsets(list);
        }

        void OnDestroy()
        {
            if (Sim != null) Sim.Events.RunReset -= OnRunReset;
            if (Instance == this) Instance = null;
        }

        public Island IslandObject(string key) => islandObjects.TryGetValue(key, out var r) ? r : null;
        public IEnumerable<Island> Islands => islandObjects.Values;
        public bool IsUnlocked(string area) => Sim != null && Sim.World.IsAreaUnlocked(area);

        void Update()
        {
            if (Sim == null)
            {
                // a script reload during Play mode drops the (non-serialized) simulation
                Debug.LogWarning("[GameRunner] Simulation lost (script reload during Play mode) — restart Play mode.");
                enabled = false;
                return;
            }
            double dt = Time.unscaledDeltaTime * 1000.0;
            tickAcc += dt;
            int n = 0;
            while (tickAcc >= TickMs && n < MaxTicksPerFrame) { Sim.Tick(); tickAcc -= TickMs; n++; }
            if (n == MaxTicksPerFrame && tickAcc >= TickMs) tickAcc = 0;   // drop the backlog; the sim handles the gap

            autoAcc += dt;
            if (autoAcc >= AutomationMs) { Sim.AutomationTick(); autoAcc = autoAcc >= 2 * AutomationMs ? 0 : autoAcc - AutomationMs; }

            SyncIslands(force: false);
        }

        // ------------------------------------------------------------------ prestige

        /// <summary>`ascend(nextVows)` then save (the JS saves + reloads; here the run is swapped in place).</summary>
        public JustAscended Ascend(IEnumerable<string> vows)
        {
            var ja = Sim.Ascend(vows);
            if (SaveService.Instance != null) SaveService.Instance.Save();
            Ascended?.Invoke();
            return ja;
        }

        void OnRunReset()
        {
            tickAcc = autoAcc = 0;
            ApplyIslandOffsets();
            SyncIslands(force: true);
            if (cameraController != null) cameraController.ResetToStart();
        }

        /// <summary>Mirror sim unlock flags onto Island objects; re-clamp the camera when anything changed.</summary>
        void SyncIslands(bool force)
        {
            bool changed = false;
            foreach (var kv in islandObjects)
            {
                bool u = Sim.World.IsAreaUnlocked(kv.Key);
                if (force || kv.Value.unlocked != u) { kv.Value.SetUnlocked(u); changed = true; }
            }
            if (changed && cameraController != null) cameraController.RecomputeBounds();
        }
    }
}
