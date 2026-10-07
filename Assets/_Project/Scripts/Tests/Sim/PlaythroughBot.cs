using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>
    /// A deterministic "reasonable player" that plays one run to Ascension through the
    /// <see cref="Simulation"/> API only (no state edits — the clock is the only thing it
    /// touches directly). Used by <c>PlaythroughBotTests</c> to prove the game is completable.
    ///
    /// Input model (mirrors the Game layer, HandController / GameRunner):
    ///  • gameTick every 50 ms, automationTick every 1000 ms;
    ///  • one mouse: per 50 ms tick the bot does ONE thing — a harvest swing (paced by
    ///    <see cref="Timing.HarvestInterval"/>), a vacuum burst (3 suction steps = 60 Hz,
    ///    radius 64 px, type-locked), one right-click feed (≤ 20/s), one withdraw (≤ 5/s),
    ///    one attack (enemies.attackMs), one hand rotation (Q), or a panel click;
    ///  • moving the camera to another Island costs 1 s; the cursor moves freely inside one.
    ///
    /// Tasks are C# iterators: <c>yield return null</c> ends the tick, <c>yield return
    /// SubTask()</c> runs a sub-task to completion first.
    /// </summary>
    public sealed class PlaythroughBot
    {
        public const long TickMs = 50, AutoMs = 1000;

        // ---- pacing profile (defaults = 'expert': only the game's own input limits) ----
        /// <summary>Camera pan to another Island.</summary>
        public long IslandMs = 1000;
        /// <summary>Cursor speed inside an Island (px/s); infinity = teleport.</summary>
        public double CursorPxPerSec = double.PositiveInfinity;
        /// <summary>Reaction / decision time added to every cursor move longer than 2 cells.</summary>
        public long ReactMs = 0;

        /// <summary>A human-paced profile: 2 s island pans, 1500 px/s cursor, 300 ms per new target.</summary>
        public void UseHumanPacing() { IslandMs = 2000; CursorPxPerSec = 1500; ReactMs = 300; }
        const double PickupR = 64;
        const string C = "center";

        public readonly Simulation Sim;
        readonly ManualClock _clock;
        public readonly long StartMs;

        /// <summary>(sim ms since start, text) — quests, dragon stages, unlocks, first builds, ascension…</summary>
        public readonly List<(long ms, string what)> Timeline = new List<(long, string)>();
        /// <summary>Points where a sub-goal timed out / could not proceed (with the task stack).</summary>
        public readonly List<string> Stuck = new List<string>();
        /// <summary>Free-form observations (time spent per phase, waits…).</summary>
        public readonly List<string> Notes = new List<string>();
        /// <summary>Accumulated sim ms per top-level phase.</summary>
        public readonly List<(string phase, long ms)> Phases = new List<(string, long)>();

        public bool Ascended { get; private set; }
        /// <summary>Ascensions to play (later runs are veteran runs: no tutorial chain).</summary>
        public int RunsWanted = 1;
        /// <summary>Vows chosen when ascending at the end of run n (only honoured from the 2nd ascension on).</summary>
        public Func<int, IEnumerable<string>> VowsForNextRun;
        public int RunIndex { get; private set; } = 1;
        public int RunsDone { get; private set; }
        public readonly List<long> AscendedAt = new List<long>();
        /// <summary>Items that crossed the sky on a Spirit Bridge (arrivals at a receiving bridge).</summary>
        public int SkyDelivered;
        /// <summary>Items the bot withdrew from the storehouse its Spirit Bridge fills.</summary>
        public int TakenFromBridgeStore;
        public string PerkBought { get; private set; }
        public bool Finished { get; private set; }
        public long Ticks { get; private set; }
        public int Actions { get; private set; }
        /// <summary>Optional per-sim-minute progress line (debugging).</summary>
        public Action<string> Trace;
        public int TraceEvery = 1200;

        readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
        readonly List<string> _names = new List<string>();
        readonly List<string> _keep = new List<string>();
        readonly HashSet<string> _firstBuilt = new HashSet<string>();
        long _nextAuto;
        string _cur = C;
        double _lastSwing = -1e12, _lastAttack = -1e12, _lastWithdraw = -1e12;
        double _cx, _cy;

        public PlaythroughBot(Simulation sim, ManualClock clock)
        {
            Sim = sim;
            _clock = clock;
            StartMs = clock.NowMs;
            var ev = sim.Events;
            ev.QuestClaimed += id => Log("quest claimed: " + id);
            ev.DragonStageAdvanced += (st, _) => Log("dragon stage " + st + (st >= Sim.Config.dragonStages.Count ? " — AWAKENED" : ""));
            ev.RegionUnlocked += k => Log("island unlocked: " + k);
            ev.BuildingCompleted += (a, b) => { if (_firstBuilt.Add(b.type)) Log("first " + b.type + " built (" + a + ")"); };
            ev.UpgradeApplied += (a, t) => Log("altar upgrade: " + a + "/" + t + " → " + Sim.UpgradeLevel(a, t).lvl);
            ev.DiscipleRecruited += (a, b) => Log("disciple recruited (" + b.disciples + ")");
            ev.BlessingStarted += (k, _) => Log("dragon blessing: " + k);
            ev.WispArrived += (a, w) => { if (w is SkyWisp sw && !sw.returning) SkyDelivered += Math.Max(1, sw.qty); };
        }

        // ================================================================ runner

        double Now => _clock.NowMs;
        long Elapsed => _clock.NowMs - StartMs;
        HandSystem Hand => Sim.Hand;
        GameState S => Sim.State;
        GameConfig Cfg => Sim.Config;

        public static string Fmt(long ms) { var t = TimeSpan.FromMilliseconds(ms); return $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"; }
        void Log(string what) => Timeline.Add((Elapsed, RunsWanted > 1 ? "[run " + RunIndex + "] " + what : what));
        void Note(string what) => Notes.Add(Fmt(Elapsed) + " " + what);
        string StackText() => string.Join(" › ", _names);

        readonly HashSet<string> _spins = new HashSet<string>();
        /// <summary>A goal loop iterated without doing anything (no tick used): it idles a tick; reported once per stack.</summary>
        void Spin() { if (_spins.Add(StackText())) Note("idling (no action possible) in [" + StackText() + "] hand: " + HandText() + (SpinCtx != null ? " — " + SpinCtx() : "")); }
        Func<string> SpinCtx;

        void StuckAt(string why)
        {
            var m = Sim.Milestone();
            string hint = m.step?.text ?? (m.needs.Count > 0 ? "needs " + string.Join(", ", m.needs.ConvertAll(n => n.item + " " + n.have + "/" + n.need)) : m.kind.ToString());
            Stuck.Add($"{Fmt(Elapsed)} [{StackText()}] {why} (hand: {HandText()}; milestone: {hint})");
        }

        public string HandText()
        {
            var sb = new StringBuilder();
            foreach (var h in S.hand) sb.Append(h.item).Append('×').Append(h.qty).Append(' ');
            return sb.Length == 0 ? "empty" : sb.ToString().TrimEnd();
        }

        /// <summary>Plays until ascended (and a perk bought) or the cap. Returns true when it ascended.</summary>
        public bool Run(long capMs)
        {
            _stack.Push(Main());
            _nextAuto = _clock.NowMs + AutoMs;
            string phase = null; long phaseAt = 0;
            while (!Finished && Elapsed < capMs)
            {
                _clock.Advance(TickMs);
                SnapFlow();
                Sim.Tick();
                Ticks++;
                if (_clock.NowMs >= _nextAuto) { Sim.AutomationTick(); _nextAuto += AutoMs; }
                CountFlow(false);
                if (Ticks % 10 == 0) TryClaim();
                if (CheckInvariants) Invariants();
                if (Trace != null && Ticks % TraceEvery == 0) Trace(Fmt(Elapsed) + " [" + StackText() + "] hand: " + HandText() + " stage " + S.dragon.stage + " quest " + S.quest.idx);
                SnapFlow();
                Step();
                CountFlow(true);
                string top = _names.Count > 0 ? _names[0] : null;
                if (top != phase)
                {
                    if (phase != null) Phases.Add((phase, Elapsed - phaseAt));
                    phase = top; phaseAt = Elapsed;
                }
            }
            if (phase != null) Phases.Add((phase, Elapsed - phaseAt));
            if (!Finished) StuckAt("time cap reached");
            return Ascended;
        }

        void Step()
        {
            int guard = 0;
            while (_stack.Count > 0)
            {
                if (++guard > 20000) throw new InvalidOperationException("bot spun without yielding: " + StackText());
                var top = _stack.Peek();
                if (!top.MoveNext()) { _stack.Pop(); if (_stack.Count == 0) Finished = true; continue; }
                if (top.Current is IEnumerator sub) { _stack.Push(sub); continue; }
                Actions++;
                return;
            }
        }

        void TryClaim()
        {
            var p = Sim.CurrentQuestProgress();
            if (p != null && p.done) Sim.ClaimQuest();
        }

        /// <summary>Named task wrapper (for stuck reports / phase timing).</summary>
        IEnumerator T(string name, IEnumerator body)
        {
            _names.Add(name);
            yield return body;
            _names.RemoveAt(_names.Count - 1);
        }

        sealed class Box<TV> { public TV v; }

        // ================================================================ production share (FlowLedger)

        /// <summary>Length of one production-share window.</summary>
        public const long ShareWindowMs = 5 * 60000;
        /// <summary>
        /// Items produced per window (FlowLedger "produced" deltas), split by who made them:
        /// <c>hand</c> = during the bot's own actions (harvest swings, attacks); <c>auto</c> = raw items made
        /// during the world tick (field generators, generator buildings, Automation, disciples, dragon
        /// scales); <c>crafted</c> = converter outputs made during the world tick.
        /// </summary>
        public sealed class ShareWindow { public int run; public long startMs; public long hand, auto, crafted; }
        public readonly List<ShareWindow> Share = new List<ShareWindow>();
        public long ProdHand, ProdAuto, ProdCrafted;
        /// <summary>Raw items made automatically, by item (whole session).</summary>
        public readonly Dictionary<string, long> AutoByItem = new Dictionary<string, long>();
        public readonly Dictionary<string, long> HandByItem = new Dictionary<string, long>();
        long[] _flowSnap = new long[64];
        int _flowSnapN;
        HashSet<string> _craftedItems;
        long _runStartMs;

        void SnapFlow()
        {
            var it = S.flow.items;
            if (_flowSnap.Length < it.Count) Array.Resize(ref _flowSnap, it.Count * 2);
            for (int i = 0; i < it.Count; i++) _flowSnap[i] = it[i].runProduced;
            _flowSnapN = it.Count;
        }

        void CountFlow(bool byHand)
        {
            if (_craftedItems == null)
            {
                _craftedItems = new HashSet<string>();
                foreach (var b in Cfg.buildings) foreach (var r in b.recipes) _craftedItems.Add(r.output);
            }
            var it = S.flow.items;
            for (int i = 0; i < it.Count; i++)
            {
                long d = it[i].runProduced - (i < _flowSnapN ? _flowSnap[i] : 0);
                if (d <= 0) continue;
                var w = CurWindow();
                string item = it[i].item;
                if (byHand) { w.hand += d; ProdHand += d; HandByItem[item] = (HandByItem.TryGetValue(item, out long h) ? h : 0) + d; }
                else if (_craftedItems.Contains(item)) { w.crafted += d; ProdCrafted += d; }
                else { w.auto += d; ProdAuto += d; AutoByItem[item] = (AutoByItem.TryGetValue(item, out long a) ? a : 0) + d; }
            }
        }

        ShareWindow CurWindow()
        {
            long t = Elapsed - _runStartMs;
            long start = _runStartMs + t / ShareWindowMs * ShareWindowMs;
            if (Share.Count == 0 || Share[Share.Count - 1].startMs != start || Share[Share.Count - 1].run != RunIndex)
                Share.Add(new ShareWindow { run = RunIndex, startMs = start });
            return Share[Share.Count - 1];
        }

        /// <summary>auto ÷ hand over the windows of run <paramref name="run"/> whose start lies in [from, to) of that run's length (fractions).</summary>
        public double AutoRatio(int run, double from, double to, bool withCrafted = false)
        {
            long h = 0, a = 0;
            long rs = RunStart(run), len = RunLength(run);
            foreach (var w in Share)
            {
                if (w.run != run) continue;
                double f = len > 0 ? (double)(w.startMs - rs) / len : 0;
                if (f < from || f >= to) continue;
                h += w.hand; a += w.auto + (withCrafted ? w.crafted : 0);
            }
            return h == 0 ? (a > 0 ? double.PositiveInfinity : 0) : (double)a / h;
        }

        public long RunStart(int run) => run <= 1 ? 0 : AscendedAt[run - 2];
        public long RunLength(int run) => run - 1 < AscendedAt.Count ? AscendedAt[run - 1] - RunStart(run) : Elapsed - RunStart(run);

        // ================================================================ invariants

        /// <summary>Check world invariants after every tick (violations go to <see cref="Violations"/>, first 50).</summary>
        public bool CheckInvariants = true;
        public readonly List<string> Violations = new List<string>();

        void Bad(string what) { if (Violations.Count < 50) Violations.Add(Fmt(Elapsed) + " " + what); }

        void Invariants()
        {
            if (Hand.Total() > Hand.Cap()) Bad("hand over cap: " + Hand.Total() + "/" + Hand.Cap());
            foreach (var h in S.hand) if (h.qty <= 0) Bad("empty hand stack " + h.item);
            double px = Sim.Ctx.PlayPx;
            int shCap = Sim.Buildings.StorehouseCap();
            foreach (var a in S.areas)
            {
                foreach (var g in a.ground)
                    if (double.IsNaN(g.x) || double.IsNaN(g.y) || g.x < 0 || g.y < 0 || g.x > px || g.y > px) { Bad("ground item off the island: " + a.key + " " + g.item + " " + g.x + "," + g.y); break; }
                if (a.ground.Count > Cfg.balance.groundHardCap) Bad("ground over hard cap on " + a.key);
                foreach (var b in a.buildings)
                {
                    var d = Cfg.Building(b.type);
                    if (b.stock != null) foreach (var e in b.stock) if (e.qty < 0) Bad("negative stock " + b.type + " " + e.item);
                    if (b.fuelQ != null) { if (b.fuelQ.Count > Sim.Fuel.Slots) Bad("fuel rack over slots " + b.type); foreach (var q in b.fuelQ) if (q.rem < 0) Bad("negative fuel"); }
                    if (b.inv != null)
                    {
                        int cap = d.gather.enabled ? d.gather.cap : d.bridge.enabled ? d.bridge.cap : d.stoker.enabled ? d.stoker.cap : int.MaxValue;
                        if (BuildingSystem.GatherTotal(b) > cap) Bad(b.type + " #" + b.id + " over cap " + BuildingSystem.GatherTotal(b) + "/" + cap);
                        foreach (var st in b.inv) if (st.qty <= 0) Bad("empty inv stack in " + b.type);
                    }
                    if (b.type == "storehouse" && b.qty > shCap) Bad("storehouse over cap");
                    if (d.seal.enabled && b.qty > Sim.Buildings.SealCap(d)) Bad("seal over cap");
                    if (b.qty < 0) Bad("negative qty " + b.type);
                    if (d.roster.enabled && (b.buns < 0 || b.buns > d.roster.foodCap)) Bad("pavilion food out of range " + b.buns);
                }
                foreach (var w in a.wisps) if (double.IsNaN(w.x) || double.IsNaN(w.y)) Bad("NaN wisp");
            }
            foreach (var w in S.skyWisps) if (double.IsNaN(w.x) || double.IsNaN(w.y)) Bad("NaN sky wisp");
            if (Sim.Dragon.CurrentStage != null)
            {
                var need = Sim.Dragon.Needs();
                foreach (var e in S.dragon.paid) if (e.qty > need.Get(e.item)) Bad("dragon overpaid " + e.item);
            }
        }

        // ================================================================ main plan

        IEnumerator Main()
        {
            for (int run = 1; run <= RunsWanted; run++)
            {
                RunIndex = run;
                _firstBuilt.Clear();
                _cur = C;
                int before = RunsDone;
                yield return OneRun();
                if (RunsDone == before) yield break;   // this run did not ascend
            }
        }

        IEnumerator OneRun()
        {
            Sim.MarkIntroSeen();
            // 1. the tutorial quest chain (it is also the natural path through dragon stages 1-3)
            while (S.quest.idx < Cfg.quests.Count)
            {
                var q = Cfg.quests[S.quest.idx];
                int idx = S.quest.idx;
                long st = Elapsed;
                yield return T("quest " + q.id, DoQuest(q.id));
                TryClaim();
                if (S.quest.idx == idx)
                {
                    if (Elapsed - st > 30 * 60000) { StuckAt("quest " + q.id + " never completes"); yield break; }
                    yield return null;
                }
            }
            // 2. post-tutorial: grow the base — bigger hands, then machines (Automation, generator buildings).
            //    Veterans skip the tutorial chain, so they wake the dragon to stage 3 first (Forge, Algae Farm, Herb Garden).
            if (S.dragon.stage < 3) yield return T("dragon stages 1-3", FeedDragon(3));
            yield return T("hand upgrades", Upgrades());
            yield return T("invest 1", Invest(1));
            yield return T("spirit bridge", BridgeSetup());
            yield return T("invest 2", Invest(2));
            // 3. awaken the dragon
            yield return T("dragon stage 4", FeedDragon(Cfg.dragonStages.Count));
            yield return T("invest 3", Invest(3));
            // 4. remaining islands (more AP on ascending; volcano firestone / celestial)
            foreach (var r in Cfg.regions)
                if (!Sim.World.IsAreaUnlocked(r.key)) yield return T("unlock " + r.key, Unlock(r.key));
            // 5. a dragon pill blessing (Pill Furnace) — exercises the firestone-as-ingredient path
            yield return T("pill", PillBlessing());
            yield return T("invest 4", Invest(4));
            // 6. raise the Ascension Gate, fill offerings, ascend, buy a perk
            yield return T("gate", RaiseGate());
            yield return T("offerings", Offerings());
            yield return T("ascend", AscendAndPerk());
        }

        IEnumerator DoQuest(string id)
        {
            switch (id)
            {
                case "wood": yield return Acquire("wood", 5); break;
                case "leaves": yield return Acquire("leaves", 5); break;
                case "dragon1": yield return FeedDragon(1); break;
                case "fox": yield return HuntFox(); break;
                case "build": { var b = new Box<Building>(); yield return BuildBuilding("storehouse", C, b); break; }
                case "upgrade": yield return BuyUpgrade("hand"); break;
                case "link": yield return LinkQuest(); break;
                case "explore": yield return Unlock("mine"); break;
                case "dragon2": yield return FeedDragon(2); break;
                case "iron":
                {
                    // bars already fed to the dragon count: when the tribute is bigger than the hand, feed as you go
                    _keep.Add("iron_bar");
                    var dragon = S.Area(C).buildings.Find(b => b.type == "dragon");
                    long st = Elapsed;
                    while (!(Sim.CurrentQuestProgress()?.done ?? true) && Elapsed - st < 30 * 60000)
                    {
                        int need = Math.Max(1, Sim.Dragon.TributeOf(2, "iron_bar")) - (S.dragon.stage == 2 ? S.dragon.paid.Get("iron_bar") : 0);
                        if (need <= Hand.Cap()) { yield return Acquire("iron_bar", need); break; }
                        yield return Acquire("iron_bar", Hand.Cap());
                        yield return RotateTo("iron_bar");
                        yield return Press(C, dragon, () => Hand.Count("iron_bar") == 0);
                    }
                    _keep.Remove("iron_bar");
                    break;
                }
                case "waters": yield return Unlock("fishing"); break;
                case "dragon3": yield return FeedDragon(3); break;
                case "weaver":
                {
                    _keep.Add("rope"); _keep.Add("cloth");
                    yield return Acquire("rope", 2);
                    yield return Acquire("cloth", 6);
                    yield return Acquire("rope", 2);
                    _keep.Remove("rope"); _keep.Remove("cloth");
                    break;
                }
                case "cultivate":
                {
                    var pav = FindBuilt("meditation_pavilion");
                    if (pav.b == null) { var bx = new Box<Building>(); yield return BuildBuilding("meditation_pavilion", C, bx); pav = (C, bx.v); }
                    if (pav.b == null) { StuckAt("no pavilion"); yield break; }
                    yield return Acquire("robe", 1);
                    yield return Travel(pav.area);
                    if (Sim.RecruitDisciple(pav.area, pav.b.id)) yield return null;
                    else StuckAt("recruit refused: " + Sim.RecruitReason(pav.area, pav.b.id));
                    break;
                }
                default: StuckAt("unknown quest " + id); yield return null; break;
            }
        }

        // ================================================================ quests / milestones

        IEnumerator FeedDragon(int untilStage)
        {
            var dragon = S.Area(C).buildings.Find(b => b.type == "dragon");
            yield return Deliver(C, dragon, () => Sim.DragonRemaining(), () => S.dragon.stage >= untilStage, "dragon tribute");
            if (S.dragon.dialog != null) Sim.DismissDragonDialog();
        }

        IEnumerator BuyUpgrade(string nodeId)
        {
            if (!Sim.SelectUpgradeNode(nodeId)) { Note("upgrade " + nodeId + " not selectable"); yield break; }
            yield return null;   // the tree click
            var altar = S.Area(C).buildings.Find(b => b.type == "center");
            int before = S.stats.upgradesApplied;
            yield return Deliver(C, altar, () => Sim.UpgradeJobRemaining(), () => S.upgradeJob == null || S.stats.upgradesApplied > before, "altar job " + nodeId);
        }

        IEnumerator Upgrades()
        {
            // Hand Size 2 and 3 (carry more), Regrow Speed (Center bushes)
            yield return BuyUpgrade("hand");
            yield return BuyUpgrade("hand");
            yield return BuyUpgrade("spd_c");
        }

        // ================================================================ investment (idle-hybrid player)

        /// <summary>
        /// Off = the old hands-only planner (Hand Size + Regrow Speed only). On (default) = at each phase boundary
        /// the bot buys the machines an idle-hybrid player would: Automation for the Islands it harvests, the
        /// speed nodes on the way, and generator buildings (Herb Gardens, an Algae Farm).
        /// </summary>
        public bool Invests = true;
        /// <summary>Altar upgrades / generator buildings bought by <see cref="Invest"/> (for the report).</summary>
        public readonly List<string> Investments = new List<string>();

        static readonly (int round, string what, int level)[] Plan =
        {
            (1, "spd_c", 1), (1, "auto_c", 1), (1, "spd_m", 1), (1, "spd_fi", 1), (1, "auto_m", 1),
            (1, "build:herb_garden", 2),
            (2, "auto_m", 2), (2, "auto_c", 2), (2, "spd_f", 1), (2, "act_f", 1), (2, "auto_f", 1), (2, "build:algae_farm", 1), (2, "disciples", 3),
            (3, "spd_m", 2), (3, "auto_f", 2), (3, "build:herb_garden", 3), (3, "act_c", 1), (3, "quarry", 1),
            (4, "spd_m", 3), (4, "auto_m", 3),
        };

        IEnumerator Invest(int round)
        {
            if (!Invests) yield break;
            foreach (var (r, what, level) in Plan)
            {
                if (r != round) continue;
                if (what == "disciples")
                {
                    var pav = FindBuilt("meditation_pavilion");
                    if (pav.b == null) continue;
                    while (pav.b.disciples < Math.Min(level, Sim.Pavilions.RosterCap(pav.b)))
                    {
                        int before = pav.b.disciples;
                        _keep.Add("robe");
                        yield return Acquire("robe", 1);
                        _keep.Remove("robe");
                        yield return Travel(pav.area);
                        if (!Sim.RecruitDisciple(pav.area, pav.b.id)) break;
                        yield return null;
                        Investments.Add(Fmt(Elapsed) + " disciple #" + pav.b.disciples);
                    }
                    continue;
                }
                if (what.StartsWith("build:"))
                {
                    string type = what.Substring(6);
                    var def = Cfg.Building(type);
                    string area = def != null && def.waterOnly ? "fishing" : C;
                    if (!Sim.World.IsAreaUnlocked(area) || !Sim.IsBuildingUnlocked(type)) continue;
                    while (CountBuilt(type) < level)
                    {
                        int before = CountBuilt(type);
                        var bx = new Box<Building>();
                        yield return BuildBuilding(type, area, bx);
                        if (CountBuilt(type) <= before) break;
                        Investments.Add(Fmt(Elapsed) + " " + type + " #" + CountBuilt(type));
                    }
                    continue;
                }
                var nd = Sim.Upgrades.NodeById(what);
                if (nd == null || !Sim.World.IsAreaUnlocked(nd.area) || !Selectable(what)) continue;
                while (Sim.UpgradeLevel(nd.area, nd.type).lvl < level)
                {
                    int before = Sim.UpgradeLevel(nd.area, nd.type).lvl;
                    yield return BuyUpgrade(what);
                    if (Sim.UpgradeLevel(nd.area, nd.type).lvl <= before) break;
                    Investments.Add(Fmt(Elapsed) + " " + what + " L" + (before + 1));
                }
            }
        }

        /// <summary>
        /// Spirit Essence: with ≥ 2 disciples the bot keeps their pavilion fed (Spirit Wine from a Brewery) and
        /// collects what they cultivate; otherwise it hunts a fox by hand.
        /// </summary>
        IEnumerator Cultivate()
        {
            var pav = FindBuilt("meditation_pavilion");
            if (pav.b == null || pav.b.disciples < 2) { yield return HuntFox(); yield break; }
            var roster = Cfg.Building(pav.b.type).roster;
            if (pav.b.buns < roster.foodCap / 3)
            {
                string food = "spirit_wine";
                int want = Math.Max(1, Math.Min(6, (roster.foodCap - pav.b.buns) / Math.Max(1, Sim.Buildings.FoodValue(pav.b, food))));
                _keep.Add(food);
                yield return Acquire(food, want);
                _keep.Remove(food);
                if (Hand.Count(food) > 0)
                {
                    yield return RotateTo(food);
                    var b = pav.b;
                    yield return Press(pav.area, b, () => Hand.Count(food) == 0 || b.buns + Sim.Buildings.FoodValue(b, food) > roster.foodCap);
                }
                yield break;
            }
            yield return Travel(pav.area);
            yield return Wait(500, "waiting for disciples");
        }

        bool Selectable(string id)
        {
            foreach (var st in Sim.Upgrades.TreeStates()) if (st.node.id == id) return st.selectable;
            return false;
        }

        int CountBuilt(string type)
        {
            int n = 0;
            foreach (var r in Cfg.regions) foreach (var b in S.Area(r.key).buildings) if (b.built && b.type == type) n++;
            return n;
        }

        IEnumerator LinkQuest()
        {
            // a useful link: the starter wood seal feeds the Forge's fuel rack
            var forge = FindBuilt("forge");
            if (forge.b == null) { var bx = new Box<Building>(); yield return BuildBuilding("forge", C, bx); forge = (C, bx.v); }
            var area = S.Area(C);
            Building lantern = null, seal = null;
            foreach (var b in area.buildings)
            {
                if (b.type == "wisp_lantern" && b.links != null && b.links.Exists(l => area.BuildingById(l.from)?.type == "warding_seal" && area.BuildingById(l.from)?.item == "wood")) lantern = b;
                if (b.type == "warding_seal" && b.item == "wood") seal = b;
            }
            if (lantern == null || seal == null || forge.b == null) { StuckAt("starter wood lantern/seal missing"); yield break; }
            yield return Travel(C);
            string why = Sim.AddLink(C, lantern.id, seal.id, forge.b.id);
            if (why != null) StuckAt("link refused: " + why);
            yield return null;
        }

        IEnumerator HuntFox()
        {
            yield return Travel(C);
            int kills = S.stats.foxKills;
            long st = Elapsed;
            while (S.stats.foxKills == kills)
            {
                if (Elapsed - st > 5 * 60000) { StuckAt("fox never died"); yield break; }
                Enemy en = null;
                foreach (var e in Sim.Enemies(C)) if (e.kind != "boss") { en = e; break; }
                if (en == null) Idle("waiting for a fox");
                else if (Now - _lastAttack < Sim.AttackIntervalMs(C)) Idle("attack cooldown");
                if (en != null && Now - _lastAttack >= Sim.AttackIntervalMs(C))
                {
                    if (Math.Abs(en.x - _cx) + Math.Abs(en.y - _cy) > 64) { yield return MoveCursor(en.x, en.y); continue; }
                    _cx = en.x; _cy = en.y;
                    var hit = Sim.EnemyAt(C, en.x, en.y);
                    if (hit != null) Sim.Attack(C, hit.id);
                    _lastAttack = Now;
                }
                yield return null;
            }
            yield return Pickup(C, "spirit_essence", Hand.Count("spirit_essence") + 4);
        }

        IEnumerator HuntBoar()
        {
            _keep.Add("beast_bait");
            yield return Acquire("beast_bait", 1);
            _keep.Remove("beast_bait");
            if (Hand.Count("beast_bait") <= 0) yield break;
            yield return Travel(C);
            var ez = Cfg.Region(C).enemies;
            var z = Cfg.ZoneRects(ez.zone)[0];
            double x = (z.c0 + z.c1 + 1) / 2.0 * Sim.Ctx.Cell, y = (z.r0 + z.r1 + 1) / 2.0 * Sim.Ctx.Cell;
            yield return MoveCursor(x, y);
            yield return RotateTo("beast_bait");
            var r = Sim.DropFromHand(C, x, y);
            yield return null;
            if (r == null || !r.lured) { StuckAt("bait did not lure"); yield break; }
            long st = Elapsed;
            while (true)
            {
                Enemy boar = null;
                foreach (var e in Sim.Enemies(C)) if (e.kind == "boss") { boar = e; break; }
                if (boar == null) break;
                if (Elapsed - st > 5 * 60000) { StuckAt("boar never died"); yield break; }
                if (Now - _lastAttack >= Sim.AttackIntervalMs(C))
                {
                    if (Math.Abs(boar.x - _cx) + Math.Abs(boar.y - _cy) > 64) { yield return MoveCursor(boar.x, boar.y); continue; }
                    _cx = boar.x; _cy = boar.y;
                    var hit = Sim.EnemyAt(C, boar.x, boar.y);
                    if (hit != null) Sim.Attack(C, hit.id);
                    _lastAttack = Now;
                }
                yield return null;
            }
            yield return Pickup(C, "beast_bone", Hand.Count("beast_bone") + 4);
        }

        /// <summary>ADR 0003 bridge: Fishing (stone at the spring field → lantern → bridge) sends Spring Water to a Center storehouse.</summary>
        IEnumerator BridgeSetup()
        {
            const string F = "fishing";
            if (!Sim.World.IsAreaUnlocked(F)) yield return Unlock(F);
            if (!Sim.IsBuildingUnlocked("spirit_bridge")) { StuckAt("Spirit Bridge not revealed with " + Sim.Buildings.UnlockedIslandCount() + " islands"); yield break; }
            var spring = Cfg.ZoneRects("springField")[0];
            var stone = new Box<Building>(); yield return BuildBuilding("gathering_stone", F, stone, (spring.r0 + spring.r1) / 2 + 2, (spring.c0 + spring.c1) / 2 + 2);
            var lan = new Box<Building>(); yield return BuildBuilding("wisp_lantern", F, lan, spring.r1 + 4, spring.c1 + 4);
            var bf = new Box<Building>(); yield return BuildBuilding("spirit_bridge", F, bf, spring.r1 + 6, spring.c1 + 2);
            var bc = new Box<Building>(); yield return BuildBuilding("spirit_bridge", C, bc);
            var sh = new Box<Building>(); yield return BuildBuilding("storehouse", C, sh);
            var lanC = new Box<Building>(); yield return BuildBuilding("wisp_lantern", C, lanC);
            if (stone.v == null || lan.v == null || bf.v == null || bc.v == null || sh.v == null || lanC.v == null) { StuckAt("bridge network not built"); yield break; }
            yield return Travel(F);
            string why = Sim.AddLink(F, lan.v.id, stone.v.id, bf.v.id);
            if (why != null) StuckAt("fishing link refused: " + why);
            yield return null;
            why = Sim.PairBridges(F, bf.v.id, C, bc.v.id);
            if (why != null) StuckAt("pairing refused: " + why);
            yield return null;
            yield return Travel(C);
            why = Sim.AddLink(C, lanC.v.id, bc.v.id, sh.v.id);
            if (why != null) StuckAt("center link refused: " + why);
            yield return null;
            Log("spirit bridge fishing→center paired");
            _bridgeStore = sh.v;
        }
        Building _bridgeStore;

        IEnumerator PillBlessing()
        {
            _keep.Add("ember_pill");
            yield return Acquire("ember_pill", 1);
            _keep.Remove("ember_pill");
            if (Hand.Count("ember_pill") <= 0) yield break;
            var dragon = S.Area(C).buildings.Find(b => b.type == "dragon");
            yield return Travel(C);
            yield return RotateTo("ember_pill");
            var (x, y) = Sim.World.BuildingCenterPx(dragon);
            yield return MoveCursor(x, y);
            Sim.DropFromHand(C, x, y, true);
            yield return null;
        }

        IEnumerator RaiseGate()
        {
            Building gate = null;
            foreach (var b in S.Area(C).buildings) if (Cfg.Building(b.type)?.gate == true) gate = b;
            if (gate == null)
            {
                var m = Sim.Milestone();
                if (m.kind != MilestoneKind.RaiseGate) Note("milestone is " + m.kind + " before the gate");
                var bx = new Box<Building>();
                yield return BuildBuilding("ascension_gate", C, bx);
                gate = bx.v;
            }
            else if (!gate.built) yield return Deliver(C, gate, () => Sim.BuildingNeeds(gate), () => gate.built, "gate ghost");
            if (gate == null || !gate.built) StuckAt("gate not built");
        }

        IEnumerator Offerings()
        {
            var (_, gate) = Sim.Progression.BuiltGate();
            if (gate == null) yield break;
            var G = Cfg.gateOfferings;
            foreach (var it in G.items)
            {
                _keep.Add(it);
                long st = Elapsed;
                while (Sim.GateOfferings(gate).offered.Get(it) < G.perType && Sim.GateOfferings(gate).count < G.cap)
                {
                    if (Elapsed - st > 30 * 60000) { StuckAt("offering " + it + " never filled"); break; }
                    int want = G.perType - Sim.GateOfferings(gate).offered.Get(it);
                    if (Hand.Count(it) < want) yield return Acquire(it, want);
                    yield return Press(C, gate, () => Sim.GateOfferings(gate).offered.Get(it) >= G.perType);
                }
                _keep.Remove(it);
            }
            Log("gate offerings " + Sim.GateOfferings(gate).count + "/" + G.cap + " (ascend reward " + Sim.AscendReward() + " AP)");
        }

        IEnumerator AscendAndPerk()
        {
            if (Sim.Progression.BuiltGate().b == null) { StuckAt("no gate to ascend through"); yield break; }
            int reward = Sim.AscendReward();
            yield return null;   // the ascend dialog
            var vows = VowsForNextRun?.Invoke(RunIndex);
            Sim.Ascend(vows);
            Ascended = true;
            RunsDone++;
            AscendedAt.Add(Elapsed);
            _runStartMs = Elapsed;
            Log($"ASCENDED (+{reward} AP, total {S.ascendPoints})" + (S.vows.active.Count > 0 ? " — next run vows: " + string.Join(",", S.vows.active) : ""));
            yield return null;
            // perk shop: spend AP on the most useful perks (one level per pass, preference order)
            string[] prefs = { "hands", "haste", "paths", "legacy", "slumber", "frugal", "hall", "apgain" };
            bool bought = true;
            while (bought)
            {
                bought = false;
                foreach (var id in prefs)
                {
                    var c = Sim.PerkCost(id);
                    if (c != null && S.ascendPoints >= c.Value && Sim.BuyPerk(id))
                    {
                        PerkBought ??= id; bought = true;
                        Log("perk bought: " + id + " L" + S.PerkLevel(id) + " (" + c.Value + " AP)");
                        yield return null;
                    }
                }
            }
            if (PerkBought == null) StuckAt("no affordable perk with " + S.ascendPoints + " AP");
            Sim.MarkPerkShopSeen();
            Sim.ClearJustAscended();
            yield return null;
        }

        // ================================================================ generic goals

        /// <summary>Feed a sink (dragon / altar job / ghost) from the hand until <paramref name="done"/>; acquires what it lacks.</summary>
        IEnumerator Deliver(string area, Building b, Func<ItemCounts> remaining, Func<bool> done, string what)
        {
            long st = Elapsed;
            long lt = -1; int spin = 0;
            while (!done())
            {
                if (Ticks == lt) { if (++spin == 3) Spin(); yield return null; } else spin = 0;
                lt = Ticks;
                if (Elapsed - st > 120 * 60000) { StuckAt(what + " never completed"); yield break; }
                var rem = remaining();
                if (rem == null || rem.Count == 0) { yield return null; continue; }
                bool carry = false;
                foreach (var e in rem) if (Hand.Count(e.item) > 0) carry = true;
                if (carry) { yield return Press(area, b, done); continue; }
                var first = rem.entries[0];
                foreach (var e in rem) _keep.Add(e.item);
                yield return Acquire(first.item, Math.Min(first.qty, Hand.Cap()));
                foreach (var e in rem) _keep.Remove(e.item);
            }
        }

        /// <summary>Right-click-hold on a building (one press per tick) until nothing more is accepted.</summary>
        IEnumerator Press(string area, Building b, Func<bool> done)
        {
            yield return Travel(area);
            var (x, y) = Sim.World.BuildingCenterPx(b);
            yield return MoveCursor(x, y);
            int presses = 0;
            while (!done() && presses++ < 400)
            {
                var r = Sim.DropFromHand(area, x, y, true);
                if (r == null) break;
                yield return null;
            }
        }

        int AcquireCap(string item) => Hand.Cap() - (Hand.Total() - Hand.Count(item));

        /// <summary>Make the hand hold at least n of item (clipped to the hand cap).</summary>
        IEnumerator Acquire(string item, int n)
        {
            n = Math.Min(n, Hand.Cap());
            if (Hand.Count(item) >= n) yield break;
            _keep.Add(item);
            _names.Add("get " + item + "×" + n);
            long st = Elapsed;
            long lt = -1; int spin = 0;
            while (Hand.Count(item) < n)
            {
                if (Ticks == lt) { if (++spin == 3) Spin(); yield return null; } else spin = 0;
                lt = Ticks;
                if (Elapsed - st > 40 * 60000) { StuckAt("could not get " + item); break; }
                if (Hand.Space() <= 0)
                {
                    yield return DropJunk(Math.Min(n - Hand.Count(item), 6));
                    if (Hand.Space() <= 0) { n = Hand.Count(item); break; }   // hand full of things we need: settle for what we hold
                }
                if (FindGround(item, out string ga, true)) { yield return Pickup(ga, item, n); continue; }
                if (FindStored(item, out string sa, out Building sb)) { yield return Withdraw(sa, sb, item, n); continue; }
                if (FindGround(item, out ga)) { yield return Pickup(ga, item, n); continue; }
                yield return Produce(item, n);
            }
            _names.RemoveAt(_names.Count - 1);
            _keep.RemoveAt(_keep.LastIndexOf(item));
        }

        IEnumerator Produce(string item, int target)   // target = total wanted in hand
        {
            if (item == DragonSystem.ScaleItem)
            {
                if (Sim.Dragon.CurrentStage != null) { StuckAt("dragon scales before awakening"); yield return Wait(1000); yield break; }
                yield return Travel(C);
                yield return Wait(500, "waiting for dragon scales");
                yield break;
            }
            if (item == "beast_bone") { yield return HuntBoar(); yield break; }
            if (item == "spirit_essence") { yield return Cultivate(); yield break; }
            if (IsCrafted(item)) { yield return Craft(item, target); yield break; }
            yield return Gather(item);
        }

        // ================================================================ raw gathering

        enum SrcKind { Node, Fixture, Field }
        sealed class RawSrc { public string area; public SrcKind kind; public string id; }

        Dictionary<string, List<RawSrc>> _raw;

        List<RawSrc> RawSources(string item)
        {
            if (_raw == null)
            {
                _raw = new Dictionary<string, List<RawSrc>>();
                void Add(string it, string area, SrcKind k, string id)
                {
                    if (!_raw.TryGetValue(it, out var l)) _raw[it] = l = new List<RawSrc>();
                    if (!l.Exists(s => s.area == area && s.kind == k && s.id == id)) l.Add(new RawSrc { area = area, kind = k, id = id });
                }
                foreach (var r in Cfg.regions)
                {
                    foreach (var sp in r.spawners)
                    {
                        var specs = new List<DropSpec>();
                        if (sp.useTiers && r.tiers.Count > 0) { specs.AddRange(r.tiers[0].perHit); specs.AddRange(r.tiers[0].drops); }
                        else { specs.AddRange(sp.perHit); specs.AddRange(sp.drops); }
                        foreach (var s in specs) Add(s.item, r.key, SrcKind.Node, sp.kind);
                    }
                    foreach (var fx in r.fixtures) if (!string.IsNullOrEmpty(fx.drop)) Add(fx.drop, r.key, SrcKind.Fixture, fx.kind);
                    foreach (var g in r.generators) Add(g.item, r.key, SrcKind.Field, g.zone);
                }
                foreach (var l in _raw.Values) l.Sort((a, b) => a.kind != b.kind ? a.kind.CompareTo(b.kind) : Cfg.RegionIndex(a.area).CompareTo(Cfg.RegionIndex(b.area)));
            }
            return _raw.TryGetValue(item, out var res) ? res : null;
        }

        IEnumerator Gather(string item)
        {
            var srcs = RawSources(item);
            if (srcs == null || srcs.Count == 0) { StuckAt("no source for raw item " + item); yield return Wait(5000); yield break; }
            RawSrc src = srcs.Find(s => Sim.World.IsAreaUnlocked(s.area));
            if (src == null) { yield return T("unlock " + srcs[0].area + " for " + item, Unlock(srcs[0].area)); yield break; }
            var area = S.Area(src.area);
            yield return Travel(src.area);
            if (src.kind == SrcKind.Field)
            {
                // wait at the field for the next well-up (Acquire's ground check collects it)
                var other = srcs.Find(s => s != src && Sim.World.IsAreaUnlocked(s.area) && s.kind != SrcKind.Field);
                if (other == null) yield return Wait(500, "waiting at a field");
                yield break;
            }
            Node node = null;
            double best = double.MaxValue;
            foreach (var n in area.nodes)
            {
                if (n.deco) continue;
                bool match = src.kind == SrcKind.Fixture ? n.isFixed && n.kind == src.id : n.spawnerKind == src.id;
                if (!match) continue;
                if (n.interaction == NodeInteraction.Surface && n.surfaceUntil != 0 && n.surfaceUntil - Now < 150) continue;
                var (nx, ny) = Sim.World.NodeCenterPx(n);
                double d = (nx - _cx) * (nx - _cx) + (ny - _cy) * (ny - _cy);
                if (d < best) { best = d; node = n; }
            }
            if (node == null)
            {
                // all regrowing: try another unlocked source, else wait
                var alt = srcs.Find(s => s != src && Sim.World.IsAreaUnlocked(s.area) && s.kind != SrcKind.Field && HasLive(s));
                if (alt == null) { yield return Wait(250, "waiting for nodes to regrow"); yield break; }
                src = alt; area = S.Area(src.area);
                yield return Travel(src.area);
                foreach (var n in area.nodes) if (!n.deco && (src.kind == SrcKind.Fixture ? n.isFixed && n.kind == src.id : n.spawnerKind == src.id)) { node = n; break; }
                if (node == null) yield break;
            }
            var (cx, cy) = Sim.World.NodeCenterPx(node);
            yield return MoveCursor(cx, cy);
            int swings = node.isFixed ? Math.Max(1, node.clicksPerDrop - node.clicks) : 50;
            for (int i = 0; i < swings && area.nodes.Contains(node); i++)
                yield return Swing(src.area, node);
            yield return Pickup(src.area, item, Hand.Count(item) + 12, cx, cy, 160);
        }

        bool HasLive(RawSrc s)
        {
            foreach (var n in S.Area(s.area).nodes)
                if (!n.deco && (s.kind == SrcKind.Fixture ? n.isFixed && n.kind == s.id : n.spawnerKind == s.id)) return true;
            return false;
        }

        IEnumerator Swing(string area, Node node)
        {
            var st = S.Area(area);
            double iv = Sim.Timing.HarvestInterval(S, st, node);
            while (Now - _lastSwing < iv) { Idle("swing cooldown"); yield return null; }
            if (Hand.Space() <= 0) yield return DropJunk(1);
            if (!st.nodes.Contains(node)) yield break;
            Sim.Harvest(area, node.id, false, true);
            _lastSwing = Now;
            yield return null;
        }

        // ================================================================ crafting

        bool IsCrafted(string item)
        {
            foreach (var b in Cfg.buildings) foreach (var r in b.recipes) if (r.output == item) return true;
            return false;
        }

        /// <summary>Recipe choice: the first recipe producing the item, with a few explicit preferences.</summary>
        (BuildingDef def, int ri) ChooseRecipe(string item)
        {
            string prefBuilding = null, prefRecipe = null;
            switch (item)
            {
                case "glass":
                    if (Sim.World.IsAreaUnlocked("volcano") && !Sim.World.IsAreaUnlocked("farm")) prefRecipe = "Obsidian Glass";
                    else prefRecipe = "Glass";
                    break;
                case "qi_elixir": prefRecipe = "Qi Elixir"; break;
                case "star_steel": prefRecipe = Sim.World.IsAreaUnlocked("celestial") ? "Astral Steel" : "Star Steel"; break;
            }
            foreach (var b in Cfg.buildings)
                for (int i = 0; i < b.recipes.Count; i++)
                    if (b.recipes[i].output == item && (prefRecipe == null || b.recipes[i].name == prefRecipe) && (prefBuilding == null || b.key == prefBuilding))
                        return (b, i);
            foreach (var b in Cfg.buildings)
                for (int i = 0; i < b.recipes.Count; i++)
                    if (b.recipes[i].output == item) return (b, i);
            return (null, -1);
        }

        /// <summary>A converter of this type to use for recipe ri: one already on it, else a free (non-network) one, else build.</summary>
        IEnumerator GetConverter(string type, int ri, Box<(string area, Building b)> res)
        {
            (string, Building) pick = (null, null);
            foreach (var r in Cfg.regions)
            {
                if (!Sim.World.IsAreaUnlocked(r.key)) continue;
                foreach (var b in S.Area(r.key).buildings)
                    if (b.built && b.type == type && b.recipe == ri && (pick.Item2 == null || r.key == C)) pick = (r.key, b);
            }
            if (pick.Item2 == null)
                foreach (var r in Cfg.regions)
                {
                    if (!Sim.World.IsAreaUnlocked(r.key)) continue;
                    foreach (var b in S.Area(r.key).buildings)
                        if (b.built && b.type == type && !b.starter && !Sim.Buildings.IsLinkTarget(r.key, b)) { pick = (r.key, b); break; }
                    if (pick.Item2 != null) break;
                }
            if (pick.Item2 == null)
            {
                var bx = new Box<Building>();
                yield return BuildBuilding(type, C, bx);
                if (bx.v != null) pick = (C, bx.v);
            }
            res.v = pick;
        }

        IEnumerator Craft(string item, int need)
        {
            var (def, ri) = ChooseRecipe(item);
            if (def == null) { StuckAt("no recipe for " + item); yield break; }
            var conv = new Box<(string area, Building b)>();
            yield return GetConverter(def.key, ri, conv);
            var (area, b) = conv.v;
            if (b == null) { StuckAt("no " + def.key + " for " + item); yield return Wait(2000); yield break; }
            var rec = def.recipes[ri];
            int outQ = Math.Max(1, rec.outputQty);
            var conS = Sim.Converters;
            long st = Elapsed;
            _names.Add("craft " + item + " @" + def.key);
            long lt = -1; int spin = 0;
            while (Hand.Count(item) < need)
            {
                if (Ticks == lt) { if (++spin == 3) Spin(); yield return null; } else spin = 0;
                lt = Ticks;
                if (Elapsed - st > 30 * 60000) { StuckAt("craft " + item + " stalled: " + Sim.BuildingStatus(area, b)); break; }
                if (!S.Area(area).buildings.Contains(b)) break;
                if (Hand.Space() <= 0) { yield return DropJunk(1); if (Hand.Space() <= 0) break; }
                if (FindGround(item, out string ga, true)) { yield return Pickup(ga, item, need); continue; }
                if (FindStored(item, out string sa, out Building sb)) { yield return Withdraw(sa, sb, item, need); continue; }
                if (FindGround(item, out ga)) { yield return Pickup(ga, item, need); continue; }
                if (b.recipe != ri)
                {
                    yield return Travel(area);
                    Sim.SetRecipe(area, b.id, ri);
                    yield return null;
                    continue;
                }
                int pending = (b.smeltDoneAt > 0 ? outQ : 0) + conS.CraftsPossible(b) * outQ;
                int missing = need - Hand.Count(item) - pending;
                if (conS.IsBurner(b) && !(Sim.Fuel.Total(b) > 0) && (conS.CanStartBatch(b) || b.smeltDoneAt > 0)) { yield return Refuel(area, b); continue; }
                if (missing > 0)
                {
                    long t0 = Ticks;
                    int cap = conS.StockCap(rec), batches = (missing + outQ - 1) / outQ;
                    foreach (var i in rec.inputs) batches = Math.Min(batches, Math.Max(1, cap / Math.Max(1, i.qty)));
                    batches = Math.Max(1, Math.Min(batches, 4));
                    foreach (var i in rec.inputs)
                    {
                        int want = batches * i.qty - conS.Stock(b).Get(i.item);
                        if (want <= 0) continue;
                        if (Hand.Count(i.item) < want)
                        {
                            _keep.Add(item);
                            yield return Acquire(i.item, Math.Max(1, Math.Min(want, AcquireCap(i.item))));
                            _keep.Remove(item);
                        }
                        yield return FeedConverter(area, b, i.item, want);
                    }
                    if (Ticks != t0) continue;
                    // stock already covers the batches queued (at most 4 at a time): wait for them
                }
                Idle("waiting for converter batches (" + item + ")"); yield return null;
            }
            _names.RemoveAt(_names.Count - 1);
        }

        IEnumerator FeedConverter(string area, Building b, string item, int want)
        {
            yield return Travel(area);
            var (x, y) = Sim.World.BuildingCenterPx(b);
            yield return MoveCursor(x, y);
            int target = Math.Min(Sim.Converters.Stock(b).Get(item) + want, Sim.Converters.StockCap(Sim.Converters.RecipeOf(b)));
            int guard = 0;
            while (Sim.Converters.Stock(b).Get(item) < target && Hand.Count(item) > 0 && guard++ < 200)
            {
                if (Hand.Front != item) { yield return RotateTo(item); continue; }
                var r = Sim.DropFromHand(area, x, y, true);
                yield return null;
                if (r == null || (r.kind != DropResultKind.Fed && r.kind != DropResultKind.Reordered)) break;
            }
        }

        IEnumerator Refuel(string area, Building b)
        {
            string fuel = Hand.Count("charcoal") > 0 ? "charcoal" : "wood";
            _keep.Add(fuel);
            if (Hand.Count(fuel) < 2) yield return Acquire(fuel, 2);
            _keep.Remove(fuel);
            yield return Travel(area);
            var (x, y) = Sim.World.BuildingCenterPx(b);
            yield return MoveCursor(x, y);
            for (int k = 0; k < 2 && Hand.Count(fuel) > 0 && Sim.Fuel.Space(b) > 0; k++)
            {
                yield return RotateTo(fuel);
                Sim.DropFromHand(area, x, y, true);
                yield return null;
            }
        }

        // ================================================================ buildings / islands

        (string area, Building b) FindBuilt(string type)
        {
            foreach (var r in Cfg.regions)
                foreach (var b in S.Area(r.key).buildings)
                    if (b.built && b.type == type && Sim.World.IsAreaUnlocked(r.key)) return (r.key, b);
            return (null, null);
        }

        IEnumerator EnsureBuildable(string type)
        {
            if (Sim.IsBuildingUnlocked(type)) yield break;
            foreach (var rule in Cfg.reveal)
            {
                if (rule.building != type) continue;
                foreach (var c in rule.any)
                    if (c.kind == RevealKind.Region && !Sim.World.IsAreaUnlocked(c.key))
                    {
                        yield return T("unlock " + c.key + " to reveal " + type, Unlock(c.key));
                        yield break;
                    }
            }
        }

        IEnumerator BuildBuilding(string type, string area, Box<Building> res, int anchorR = -1, int anchorC = -1)
        {
            yield return EnsureBuildable(type);
            if (!Sim.IsBuildingUnlocked(type)) { StuckAt(type + " locked: " + Sim.Buildings.UnlockReason(type)); yield break; }
            var spot = FindSpot(area, type, anchorR, anchorC);
            if (spot == null) { StuckAt("no room for " + type + " on " + area); yield break; }
            yield return Travel(area);
            var ghost = Sim.PlaceGhost(area, type, spot.Value.r, spot.Value.c);
            yield return null;
            for (int retry = 0; ghost == null && retry < 5; retry++)
            {
                // something moved in while the camera panned (a fish surfacing, a drop): look again
                spot = FindSpot(area, type, anchorR, anchorC);
                if (spot == null) break;
                ghost = Sim.PlaceGhost(area, type, spot.Value.r, spot.Value.c);
                yield return null;
            }
            if (ghost == null) { StuckAt("ghost refused: " + (spot == null ? "no spot" : Sim.PlaceReason(area, type, spot.Value.r, spot.Value.c))); yield break; }
            yield return Deliver(area, ghost, () => Sim.BuildingNeeds(ghost), () => ghost.built, "build " + type);
            res.v = ghost.built ? ghost : null;
        }

        (int r, int c)? FindSpot(string area, string type, int ar, int ac)
        {
            int N = Sim.Ctx.N;
            if (ar < 0)
            {
                if (area == C) { ar = 30; ac = 32; }
                else { ar = N - 12; ac = N / 2; }
            }
            var (w, h) = Cfg.BuildingSize(type);
            for (int rad = 0; rad < N; rad++)
                for (int dr = -rad; dr <= rad; dr++)
                    for (int dc = -rad; dc <= rad; dc++)
                    {
                        if (Math.Max(Math.Abs(dr), Math.Abs(dc)) != rad) continue;
                        int r = ar + dr, c = ac + dc;
                        if (r < 0 || c < 0 || r + h > N || c + w > N) continue;
                        if (Sim.PlaceReason(area, type, r, c) != null) continue;
                        // keep a one-cell lane around everything so later racks / footprints fit
                        if (!Clear(area, r - 1, c - 1, w + 2, h + 2)) continue;
                        return (r, c);
                    }
            return null;
        }

        bool Clear(string area, int r0, int c0, int w, int h)
        {
            var a = S.Area(area);
            for (int r = r0; r < r0 + h; r++)
                for (int c = c0; c < c0 + w; c++)
                    if (Sim.World.BuildingAt(area, r, c) != null) return false;
            return true;
        }

        IEnumerator Unlock(string k)
        {
            long st = Elapsed;
            long lt = -1; int spin = 0;
            while (!Sim.World.IsAreaUnlocked(k))
            {
                if (Ticks == lt) { if (++spin == 3) Spin(); yield return null; } else spin = 0;
                lt = Ticks;
                if (Elapsed - st > 120 * 60000) { StuckAt("unlock " + k + " never completed"); yield break; }
                var rem = Sim.UnlockRemaining(k);
                if (rem == null) { StuckAt(k + " has no unlock cost"); yield break; }
                bool carry = false;
                foreach (var e in rem) if (Hand.Count(e.item) > 0) carry = true;
                if (carry) { yield return Travel(k); Sim.UnlockArea(k); yield return null; continue; }
                var first = rem.entries[0];
                foreach (var e in rem) _keep.Add(e.item);
                yield return Acquire(first.item, Math.Min(first.qty, Hand.Cap()));
                foreach (var e in rem) _keep.Remove(e.item);
            }
        }

        // ================================================================ hand / cursor primitives

        /// <summary>Move the cursor inside the current Island (costs time under a paced profile).</summary>
        IEnumerator MoveCursor(double x, double y)
        {
            double d = Math.Sqrt((x - _cx) * (x - _cx) + (y - _cy) * (y - _cy));
            if (!double.IsPositiveInfinity(CursorPxPerSec) && d > 64)
                yield return Wait(ReactMs + (long)(d / CursorPxPerSec * 1000), "cursor moves");
            _cx = x; _cy = y;
        }

        IEnumerator Wait(long ms, string why = null)
        {
            long until = Elapsed + ms;
            while (Elapsed < until) { if (why != null) Idle(why); yield return null; }
        }

        /// <summary>Ticks the bot spent not acting, by reason (each = 50 ms).</summary>
        public readonly Dictionary<string, long> IdleTicks = new Dictionary<string, long>();
        void Idle(string why) => IdleTicks[why] = (IdleTicks.TryGetValue(why, out long v) ? v : 0) + 1;

        IEnumerator Travel(string area)
        {
            if (_cur == area) yield break;
            _cur = area;
            _cx = _cy = Sim.Ctx.PlayPx / 2.0;
            yield return Wait(IslandMs, "island pans");
        }

        IEnumerator RotateTo(string item)
        {
            int guard = 0;
            while (Hand.Count(item) > 0 && Hand.Front != item && guard++ < 64) { Sim.RotateHand(1); yield return null; }
        }

        /// <summary>Free hand space by dropping items no active goal wants (right-click on open ground).</summary>
        IEnumerator DropJunk(int need)
        {
            int guard = 0;
            while (Hand.Space() < need && guard++ < 200)
            {
                string junk = null;
                for (int i = S.hand.Count - 1; i >= 0; i--) if (!_keep.Contains(S.hand[i].item)) { junk = S.hand[i].item; break; }
                if (junk == null) yield break;
                yield return RotateTo(junk);
                var (x, y) = DumpSpot(_cur);
                yield return MoveCursor(x, y);
                var r = Sim.DropFromHand(_cur, x, y, false);
                yield return null;
                if (r == null || r.kind != DropResultKind.Dropped) yield break;
            }
        }

        (double x, double y) DumpSpot(string area)
        {
            int N = Sim.Ctx.N, cell = Sim.Ctx.Cell;
            int r0 = area == C ? 40 : N - 6, c0 = area == C ? 30 : N / 2;
            for (int rad = 0; rad < N; rad++)
                for (int dr = -rad; dr <= rad; dr++)
                    for (int dc = -rad; dc <= rad; dc++)
                    {
                        if (Math.Max(Math.Abs(dr), Math.Abs(dc)) != rad) continue;
                        int r = r0 + dr, c = c0 + dc;
                        if (r < 1 || c < 1 || r >= N - 1 || c >= N - 1) continue;
                        if (Clear(area, r - 1, c - 1, 3, 3)) return ((c + 0.5) * cell, (r + 0.5) * cell);
                    }
            return (cell * 1.5, cell * 1.5);
        }

        /// <summary>Is any of the item lying loose in an unlocked area? (prefers the current Island)</summary>
        bool FindGround(string item, out string area, bool hereOnly = false)
        {
            area = null;
            if (Sim.World.IsAreaUnlocked(_cur)) foreach (var g in S.Area(_cur).ground) if (g.item == item) { area = _cur; return true; }
            if (hereOnly) return false;
            foreach (var r in Cfg.regions)
            {
                if (!Sim.World.IsAreaUnlocked(r.key)) continue;
                foreach (var g in S.Area(r.key).ground) if (g.item == item) { area = r.key; return true; }
            }
            return false;
        }

        /// <summary>A building the item can be withdrawn from (storehouse / seal, or a stone/bridge whose first stack is it).</summary>
        bool FindStored(string item, out string area, out Building bld)
        {
            area = null; bld = null;
            foreach (var r in Cfg.regions)
            {
                if (!Sim.World.IsAreaUnlocked(r.key)) continue;
                foreach (var b in S.Area(r.key).buildings)
                {
                    if (!b.built) continue;
                    var def = Cfg.Building(b.type);
                    bool ok = (b.type == "storehouse" || def.seal.enabled) ? b.item == item && b.qty > 0
                        : (def.gather.enabled || def.bridge.enabled || def.stoker.enabled) && b.inv != null && b.inv.Count > 0 && b.inv[0].item == item;
                    if (ok) { area = r.key; bld = b; return true; }
                }
            }
            return false;
        }

        IEnumerator Pickup(string area, string item, int n, double ax = double.NaN, double ay = double.NaN, double within = double.MaxValue)
        {
            yield return Travel(area);
            var st = S.Area(area);
            for (int k = 0; k < 400; k++)
            {
                if (Hand.Count(item) >= n || Hand.Space() <= 0) yield break;
                GroundItem best = null;
                double bd = double.MaxValue;
                foreach (var g in st.ground)
                {
                    if (g.item != item) continue;
                    if (!double.IsNaN(ax) && Math.Sqrt((g.x - ax) * (g.x - ax) + (g.y - ay) * (g.y - ay)) > within) continue;
                    double d = (g.x - _cx) * (g.x - _cx) + (g.y - _cy) * (g.y - _cy);
                    if (d < bd) { bd = d; best = g; }
                }
                if (best == null) yield break;
                if (!double.IsPositiveInfinity(CursorPxPerSec) && bd > 48 * 48) { yield return MoveCursor(best.x, best.y); continue; }
                _cx = best.x; _cy = best.y;
                for (int s = 0; s < 3; s++) Sim.Suction(area, _cx, _cy, PickupR, item);
                yield return null;
            }
        }

        IEnumerator Withdraw(string area, Building b, string item, int n)
        {
            yield return Travel(area);
            var (x, y) = Sim.World.BuildingCenterPx(b);
            yield return MoveCursor(x, y);
            int guard = 0;
            while (Hand.Count(item) < n && Hand.Space() > 0 && guard++ < 2000)
            {
                var def = Cfg.Building(b.type);
                bool has = (b.type == "storehouse" || def.seal.enabled) ? b.item == item && b.qty > 0
                    : b.inv != null && b.inv.Count > 0 && b.inv[0].item == item;
                if (!has) yield break;
                if (Now - _lastWithdraw >= 200)
                {
                    if (Sim.Withdraw(area, b.id, 1) <= 0) yield break;
                    if (b == _bridgeStore) TakenFromBridgeStore++;
                    _lastWithdraw = Now;
                }
                yield return null;
            }
        }
    }
}
