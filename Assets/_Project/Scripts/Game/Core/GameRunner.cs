using System.Collections.Generic;
using IdleGrounds.Game.Data;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Owns the <see cref="Simulation"/>: builds config + state from the <see cref="GameDatabase"/>,
    /// runs gameTick on a 50 ms accumulator and automationTick every 1000 ms, and mirrors
    /// region unlock state onto the scene's <see cref="Region"/> objects (veils + camera bounds).
    /// No save/load yet (M5).
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

        readonly Dictionary<string, Region> regionObjects = new Dictionary<string, Region>();
        double tickAcc, autoAcc;

        void Awake()
        {
            Instance = this;
            if (database == null) { Debug.LogError("[GameRunner] No GameDatabase assigned."); enabled = false; return; }
            Config = database.BuildConfig();
            var clock = new SystemClock();
            var state = GameState.CreateInitial(Config, clock.NowMs);
            ulong s = seed != 0 ? seed : (ulong)clock.NowMs;
            Sim = new Simulation(Config, state, clock, new XorShiftRng(s));
            Sim.InitAllAreas();
            Space = new AreaSpace(Config);

            foreach (var r in FindObjectsByType<Region>(FindObjectsSortMode.None)) regionObjects[r.regionKey] = r;
            if (cameraController == null) cameraController = FindFirstObjectByType<CameraController>();
            SyncRegions(force: true);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

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
