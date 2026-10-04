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
    /// accumulator, automationTick every 1000 ms; region unlock state is mirrored onto the scene's
    /// <see cref="Region"/> objects (veils + camera bounds). Ascension swaps the run in place (RunReset).
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

        readonly Dictionary<string, Region> regionObjects = new Dictionary<string, Region>();
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
            try { Sim.Boot(); }
            catch (System.Exception e) { Debug.LogException(e); }
            // TODO(ADR 0003): read the Island GameObjects' positions and call Sim.SetIslandOffsets(...) here once
            // the floating-island scene exists; until then the sim uses the config default offsets (WORLD.islands).
            Space = new AreaSpace(Config);

            foreach (var r in FindObjectsByType<Region>(FindObjectsSortMode.None)) regionObjects[r.regionKey] = r;
            if (cameraController == null) cameraController = FindFirstObjectByType<CameraController>();
            SyncRegions(force: true);
        }

        void OnDestroy()
        {
            if (Sim != null) Sim.Events.RunReset -= OnRunReset;
            if (Instance == this) Instance = null;
        }

        public Region RegionObject(string key) => regionObjects.TryGetValue(key, out var r) ? r : null;
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

            SyncRegions(force: false);
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
            SyncRegions(force: true);
            if (cameraController != null) cameraController.ResetToStart();
        }

        /// <summary>Mirror sim unlock flags onto Region objects; re-clamp the camera when anything changed.</summary>
        void SyncRegions(bool force)
        {
            bool changed = false;
            foreach (var kv in regionObjects)
            {
                bool u = Sim.World.IsAreaUnlocked(kv.Key);
                if (force || kv.Value.unlocked != u) { kv.Value.SetUnlocked(u); changed = true; }
            }
            if (changed && cameraController != null) cameraController.RecomputeBoundsFromRegions();
        }
    }
}
