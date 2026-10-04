using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace IdleGrounds.Sim
{
    /// <summary>How much of a welcome-back the UI shows (main.js boot tiers).</summary>
    public enum OfflineTier
    {
        /// <summary>&lt; 90 s away — a plain reload, nothing replayed.</summary>
        None,
        /// <summary>&lt; 10 min — replayed synchronously before the first frame; a toast.</summary>
        Toast,
        /// <summary>≥ 10 min — sliced replay behind a progress modal (Skip), then the full summary.</summary>
        Full,
    }

    /// <summary>One `offlineStalls()` entry: why passive output stopped in an area.</summary>
    public sealed class OfflineStall
    {
        /// <summary>ground | autopaused | autoskip | nofuel | outfull | nobuns | stonefull</summary>
        public string kind;
        public string area;
        public int count;
        /// <summary>Building / item display names (deduped), may be empty.</summary>
        public List<string> names = new List<string>();
        public override string ToString() => kind + "@" + area + "×" + count + (names.Count > 0 ? "(" + string.Join(",", names) + ")" : "");
    }

    /// <summary>`finishOfflineCatchup` summary — the welcome-back modal's data (§15.5).</summary>
    public sealed class OfflineSummary
    {
        /// <summary>Real time away (from the ORIGINAL away start when resumed).</summary>
        public double awayMs;
        /// <summary>The replay window = min(gap, cap).</summary>
        public double elapsedMs;
        public double simulatedMs;
        /// <summary>Forfeited by Skip / failure (0 when saturated).</summary>
        public double skippedMs;
        /// <summary>Remainder after a plateau stop (nothing more would have been produced).</summary>
        public double saturatedMs;
        /// <summary>Sim time output stopped changing (when saturated), else null.</summary>
        public double? flatAtMs;
        /// <summary>Skip pressed after output had already levelled off.</summary>
        public bool levelled;
        public bool resumed, capped, failed;
        public double capMs;
        /// <summary>Positive per-item deltas of held items (ground, wisps, building stores; not hand, not pavilion buns).</summary>
        public ItemCounts gained = new ItemCounts();
        public List<OfflineStall> stalls = new List<OfflineStall>();
        /// <summary>Sim time at which held items reached 99% of the final gain (when well before the end), else null.</summary>
        public double? plateauMs;
        public OfflineTier tier;
    }

    /// <summary>A replay in progress (JS `job`). Fields are read-only for callers.</summary>
    public sealed class OfflineJob
    {
        public double start, end, virt;
        internal double sinceAuto;
        public double step;
        public double awayMs, elapsedMs, capMs;
        public bool capped, resumed;
        internal ItemCounts before;
        /// <summary>[simMs, heldTotal] about once per sim-minute.</summary>
        public readonly List<(double sim, long held)> samples = new List<(double, long)>();
        internal string sig;
        internal double? flatSince;
        public bool stop, done, saturated, failed;
        public OfflineSummary summary;
        public OfflineTier Tier { get; internal set; }
    }

    /// <summary>What <see cref="Simulation.Boot"/> decided (main.js §2.1 step 4).</summary>
    public sealed class OfflineBoot
    {
        public OfflineTier tier;
        /// <summary>Sliced replay to drive (tier Full) — null otherwise.</summary>
        public OfflineJob job;
        /// <summary>Already-finished summary (tier Toast) — null otherwise.</summary>
        public OfflineSummary summary;
    }

    /// <summary>
    /// Offline catch-up (engine-systems §15, engine.js:2839-3155): replay the
    /// real Tick/AutomationTick on a virtual clock from state.lastSeen to now
    /// (capped at 8 h + 2 h per Long Slumber level), events muted, ground
    /// physics skipped; sliced by a wall-clock budget; stops early once the
    /// world plateaus. The live loop must not tick while a job is active.
    /// </summary>
    public sealed class OfflineReplay
    {
        readonly Simulation _sim;
        readonly VirtualClock _vclock = new VirtualClock();
        OfflineJob _job;

        public OfflineReplay(Simulation sim) { _sim = sim; }

        SimContext Ctx => _sim.Ctx;
        GameState S => Ctx.State;
        GameConfig Cfg => Ctx.Config;
        GameBalance Bal => Cfg.balance;

        /// <summary>The live job, or null.</summary>
        public OfflineJob Job => _job;
        /// <summary>`offlineActive()`.</summary>
        public bool Active => _job != null && !_job.done;

        /// <summary>`offlineCapMs()` = (8 + 2·slumber) h.</summary>
        public double CapMs => (8 + 2 * S.PerkLevel("slumber")) * 3600.0 * 1000.0;

        /// <summary>Boot tier for an away span (main.js): &lt;90 s none, &lt;10 min toast, else full.</summary>
        public OfflineTier TierFor(double awayMs) =>
            awayMs <= Bal.offlineMinMs ? OfflineTier.None : awayMs < Bal.offlineModalMs ? OfflineTier.Toast : OfflineTier.Full;

        // ---------------- held items ----------------

        /// <summary>`countHeldItems()` — units on the ground, in wisps and in every building store
        /// (storehouse qty, gatherer/stoker inv, converter stock). Hand and pavilion buns excluded.</summary>
        public static ItemCounts CountHeldItems(GameState s, GameConfig cfg)
        {
            var tally = new ItemCounts();
            void Add(string it, int q) { if (it != null && q > 0) tally.Add(it, q); }
            foreach (var r in cfg.regions)
            {
                var area = s.Area(r.key);
                if (area == null) continue;
                foreach (var g in area.ground) Add(g.item, 1);
                foreach (var w in area.wisps) Add(w.item, 1);
                foreach (var b in area.buildings)
                {
                    if (b.item != null) Add(b.item, b.qty);
                    if (b.inv != null) foreach (var st in b.inv) Add(st.item, st.qty);
                    if (b.stock != null) foreach (var e in b.stock) Add(e.item, e.qty);
                }
            }
            return tally;
        }

        public ItemCounts CountHeldItems() => CountHeldItems(S, Cfg);

        static long HeldTotal(ItemCounts t) { long n = 0; foreach (var e in t) n += e.qty; return n; }

        // ---------------- plateau ----------------

        (string sig, bool active) Signature(ItemCounts held)
        {
            bool active = false;
            var parts = new List<string>();
            var keys = new List<string>(held.Keys);
            keys.Sort(string.CompareOrdinal);
            foreach (var k in keys) parts.Add(k + ":" + held.Get(k));
            foreach (var r in Cfg.regions)
            {
                if (!Ctx.World.IsAreaUnlocked(r.key)) continue;
                var area = S.Area(r.key);
                parts.Add("|" + r.key + ":" + area.ground.Count + "/" + area.wisps.Count +
                          (area.autoPaused ? "p" : "") + string.Join(",", area.autoSkip ?? new List<string>()));
                foreach (var b in area.buildings)
                {
                    if (!b.built) continue;
                    var def = Cfg.Building(b.type);
                    if (def == null) continue;
                    var st = Ctx.Buildings.Status(r.key, b);
                    if (def.IsConverter && (b.smeltDoneAt > 0 || (st != null && st.state == BuildingState.Working))) active = true;
                    long hold = b.qty + BuildingSystem.GatherTotal(b) + b.buns + b.disciples;
                    if (b.stock != null) foreach (var e in b.stock) hold += e.qty;
                    parts.Add(b.id + (st != null ? st.state.ToString() : "-") + hold);
                }
            }
            return (string.Join(";", parts), active);
        }

        /// <summary>`offlinePlateauCheck` — true once the signature has been flat for OFFLINE_PLATEAU_MS of sim time.</summary>
        bool PlateauCheck(OfflineJob job, double sim, ItemCounts held)
        {
            var (sig, active) = Signature(held);
            if (active || job.flatSince == null || sig != job.sig)
            {
                job.sig = active ? null : sig;
                job.flatSince = active ? (double?)null : sim;
                return false;
            }
            return sim - job.flatSince.Value >= Bal.offlinePlateauMs;
        }

        /// <summary>`offlineLevelled(job)` — the current flat streak spans ≥ 1 sim-minute (Skip button copy).</summary>
        public static bool Levelled(OfflineJob job) =>
            job != null && job.flatSince != null && (job.virt - job.start) - job.flatSince.Value >= 60000;

        // ---------------- begin / step / finish ----------------

        /// <summary>
        /// `beginOfflineCatchup()` — a job for the gap since state.lastSeen, or
        /// null (no stamp, or a plain reload ≤ 90 s). Stamps offlineAwayFrom
        /// (the original away start) until <see cref="Finish"/>.
        /// </summary>
        public OfflineJob Begin()
        {
            double now = Ctx.RealNow;
            double last = S.lastSeen, af = S.offlineAwayFrom;
            S.offlineAwayFrom = double.NaN;
            if (double.IsNaN(last) || double.IsInfinity(last)) return null;
            double gap = now - last;
            if (gap <= Bal.offlineMinMs) return null;
            bool resumed = !double.IsNaN(af) && !double.IsInfinity(af) && af < last;
            double from = resumed ? af : last;
            double cap = CapMs;
            double elapsed = Math.Min(gap, cap);
            var job = new OfflineJob
            {
                start = last, end = last + elapsed, virt = last, sinceAuto = 0,
                step = Math.Max(250, Math.Ceiling(elapsed / Bal.offlineMaxTicks)),
                awayMs = now - from, elapsedMs = elapsed, capped = gap - elapsed > 1000, capMs = cap, resumed = resumed,
                before = CountHeldItems(),
            };
            job.Tier = TierFor(job.awayMs);
            job.samples.Add((0, HeldTotal(job.before)));
            PlateauCheck(job, 0, job.before);
            S.offlineAwayFrom = from;
            _job = job;
            return job;
        }

        /// <summary>
        /// `stepOfflineCatchup(job, budgetMs)` — replay until the end, a plateau,
        /// or <paramref name="budgetMs"/> of WALL time (∞ = to the end). True
        /// when nothing is left to run (then call <see cref="Finish"/>). The
        /// virtual clock, mute and OfflineSim flags are always restored; an
        /// exception marks the job failed+stopped and is rethrown.
        /// </summary>
        public bool Step(OfflineJob job, double budgetMs = double.PositiveInfinity)
        {
            if (job == null || job.done || job.stop || job.saturated || job.virt >= job.end) return true;
            var sw = Stopwatch.StartNew();
            bool wasMuted = Ctx.Events.Muted;
            var prevClock = Ctx.ClockOverride;
            try
            {
                Ctx.Events.Muted = true;
                _sim.OfflineSim = true;
                _vclock.Virt = job.virt;
                Ctx.ClockOverride = _vclock;
                while (job.virt < job.end)
                {
                    _vclock.Virt = job.virt;
                    _sim.Tick();
                    job.sinceAuto += job.step;
                    if (job.sinceAuto >= 1000) { _sim.AutomationTick(); job.sinceAuto -= 1000; }
                    job.virt += job.step;
                    double sim = job.virt - job.start;
                    if (sim - job.samples[job.samples.Count - 1].sim >= 60000)
                    {
                        _vclock.Virt = job.virt;
                        var held = CountHeldItems();
                        job.samples.Add((sim, HeldTotal(held)));
                        if (job.virt < job.end && PlateauCheck(job, sim, held)) { job.saturated = true; break; }
                    }
                    if (sw.Elapsed.TotalMilliseconds >= budgetMs) break;
                }
            }
            catch
            {
                job.failed = true;
                job.stop = true;
                throw;
            }
            finally
            {
                Ctx.ClockOverride = prevClock;
                _sim.OfflineSim = false;
                Ctx.Events.Muted = wasMuted;
            }
            Ctx.Events.RaiseOfflineProgress(Progress(job));
            return job.virt >= job.end || job.saturated;
        }

        /// <summary>`skipOfflineCatchup` — the unsimulated remainder is forfeited.</summary>
        public void Skip(OfflineJob job) { if (job != null && !job.done) job.stop = true; }

        /// <summary>`offlineProgress(job)` 0..1 (1 when saturated or no job).</summary>
        public static double Progress(OfflineJob job)
        {
            if (job == null || job.saturated) return 1;
            return Math.Max(0, Math.Min(1, (job.virt - job.start) / Math.Max(1, job.end - job.start)));
        }

        /// <summary>
        /// `offlineResumeAt()` — the lastSeen a save should stamp while a
        /// replay is unfinished (real now − unsimulated remainder); null = stamp now.
        /// </summary>
        public double? ResumeAt()
        {
            var j = _job;
            if (j == null || j.done || j.stop || j.saturated) return null;
            return Ctx.RealNow - Math.Max(0, j.end - j.virt);
        }

        /// <summary>`finishOfflineCatchup(job)` — idempotent; never throws.</summary>
        public OfflineSummary Finish(OfflineJob job)
        {
            if (job == null) return null;
            if (job.done) return job.summary;
            job.done = true;
            if (_job == job) _job = null;
            S.offlineAwayFrom = double.NaN;
            double simulated = Math.Max(0, Math.Min(job.virt, job.end) - job.start);
            double rest = Math.Max(0, job.elapsedMs - simulated);
            var sum = new OfflineSummary
            {
                awayMs = job.awayMs, elapsedMs = job.elapsedMs, simulatedMs = simulated,
                skippedMs = job.saturated ? 0 : rest, saturatedMs = job.saturated ? rest : 0,
                flatAtMs = job.saturated ? job.flatSince : null,
                levelled = !job.saturated && job.stop && !job.failed && Levelled(job),
                resumed = job.resumed, capped = job.capped, capMs = job.capMs, failed = job.failed,
                tier = job.Tier,
            };
            try
            {
                SettleAfterReplay();
                var after = CountHeldItems();
                foreach (var e in after)
                {
                    int d = e.qty - job.before.Get(e.item);
                    if (d > 0) sum.gained.Set(e.item, d);
                }
                long total0 = job.samples[0].held, total1 = HeldTotal(after);
                if (total1 > total0)
                {
                    foreach (var (simMs, n) in job.samples)
                    {
                        if (n < total0 + (total1 - total0) * 0.99) continue;
                        if (simMs >= 60000 && (job.saturated || simMs < simulated * 0.8)) sum.plateauMs = simMs;
                        break;
                    }
                }
                sum.stalls = Stalls();
            }
            catch (Exception)
            {
                sum.failed = true;
            }
            job.summary = sum;
            return sum;
        }

        /// <summary>`runOfflineCatchup()` — begin + step(∞) + finish; null when there was nothing to replay.</summary>
        public OfflineSummary RunSync()
        {
            var job = Begin();
            if (job == null) return null;
            try { Step(job); }
            finally { Finish(job); }
            return job.summary;
        }

        /// <summary>A few settle + collider passes per open area (ground piled up physics-free).</summary>
        void SettleAfterReplay()
        {
            foreach (var r in Cfg.regions)
            {
                if (!Ctx.World.IsAreaUnlocked(r.key)) continue;
                var area = S.Area(r.key);
                for (int k = 0; k < 6 && area.ground.Count > 1; k++) if (Ctx.Ground.SettleGround(r.key) == 0) break;
                for (int k = 0; k < 2; k++) if (Ctx.Ground.PushOutOfColliders(r.key) == 0) break;
            }
        }

        /// <summary>`offlineStalls()` — per open area, why passive output stopped.</summary>
        public List<OfflineStall> Stalls()
        {
            var outp = new List<OfflineStall>();
            double now = Ctx.Now;
            foreach (var r in Cfg.regions)
            {
                string k = r.key;
                if (!Ctx.World.IsAreaUnlocked(k)) continue;
                var area = S.Area(k);
                if (area.ground.Count >= Bal.groundCap * 0.95) outp.Add(new OfflineStall { kind = "ground", area = k, count = area.ground.Count });
                if (area.autoPaused) outp.Add(new OfflineStall { kind = "autopaused", area = k, count = area.ground.Count });
                if (area.autoSkip != null && area.autoSkip.Count > 0)
                {
                    var st = new OfflineStall { kind = "autoskip", area = k, count = area.autoSkip.Count };
                    foreach (var it in area.autoSkip) st.names.Add(Cfg.Item(it)?.name ?? it);
                    outp.Add(st);
                }
                // per kind: deduped names + the raw building count (JS count = list length before dedupe)
                var kinds = new[] { "nofuel", "outfull", "nobuns", "stonefull" };
                var names = new List<string>[4];
                var counts = new int[4];
                for (int i = 0; i < 4; i++) names[i] = new List<string>();
                void Hit(int i, string nm) { counts[i]++; if (!names[i].Contains(nm)) names[i].Add(nm); }
                foreach (var b in area.buildings)
                {
                    if (!b.built) continue;
                    var def = Cfg.Building(b.type);
                    if (def == null) continue;
                    string nm = def.name ?? def.key;
                    var rec = Ctx.Converters.RecipeOf(b);
                    if (def.fuel && rec != null && b.smeltDoneAt == 0 && Ctx.Converters.CanStartBatch(b)
                        && Ctx.Fuel.Total(b) < Ctx.Converters.BatchMs(b, now)) Hit(0, nm);
                    if (def.roster.enabled && b.disciples > 0 && b.buns <= 0) Hit(2, nm);
                    if (def.gather.enabled && BuildingSystem.GatherTotal(b) >= def.gather.cap) Hit(3, nm);
                    if (def.IsConverter)
                    {
                        var st = Ctx.Buildings.Status(k, b);
                        if (st != null && st.state == BuildingState.Full && st.pile) Hit(1, nm);
                    }
                }
                for (int i = 0; i < 4; i++)
                    if (counts[i] > 0) outp.Add(new OfflineStall { kind = kinds[i], area = k, count = counts[i], names = names[i] });
            }
            return outp;
        }
    }
}
