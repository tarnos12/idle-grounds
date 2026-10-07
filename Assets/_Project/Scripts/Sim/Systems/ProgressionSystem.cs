using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>`questProgress(i)` result: cur clamped to need.</summary>
    public sealed class QuestProgressInfo
    {
        public int cur, need;
        public bool done;
    }

    /// <summary>`claimQuest()` result: {id, toHand, dropped, at}.</summary>
    public sealed class QuestClaim
    {
        public string id;
        public ItemCounts toHand = new ItemCounts();
        public ItemCounts dropped = new ItemCounts();
        /// <summary>Where the overflow dropped (center, area-local px); hasAt=false when nothing dropped.</summary>
        public bool hasAt;
        public string atArea;
        public double atX, atY;
    }

    /// <summary>World object the active quest rings (ui.js questTargetRect), area-local.</summary>
    public sealed class QuestTargetInfo
    {
        public string area;
        public string kind;         // fixture | enemyZone | dragon | altar | building
        public Node node;           // fixture
        public Building building;   // dragon / altar / building
        public ZoneRect zone;       // enemyZone
    }

    public enum UnlockResultKind { Refused, Paid, Unlocked }

    /// <summary>`unlockArea(k)` result: true ⇒ Unlocked, {paid:n} ⇒ Paid, false ⇒ Refused.</summary>
    public struct UnlockResult
    {
        public UnlockResultKind kind;
        public int paid;
        public override string ToString() => kind + (kind == UnlockResultKind.Paid ? "(" + paid + ")" : "");
    }

    public enum UnlockPayState { Cant, Partial, Afford }

    /// <summary>`gateOfferings(b)`.</summary>
    public sealed class GateOfferingInfo
    {
        public int count, cap, perType;
        public ItemCounts offered = new ItemCounts();
    }

    public enum MilestoneKind { DragonTribute, RaiseGate, Ascend }
    public enum MilestoneStepKind { Withdraw, Gather, SwitchRecipe, Build, Collect, Feed, FeedGhost, PlaceGate }

    /// <summary>One have/need row with its SOURCES hint.</summary>
    public sealed class MilestoneNeedRow
    {
        public string item;
        public int have, need;
        public string sourceHint;
        public bool Ok => have >= need;
    }

    /// <summary>`stepToward` result — the first missing sub-step (plain text, item names, no icons).</summary>
    public sealed class MilestoneStep
    {
        public MilestoneStepKind kind;
        public string item;
        /// <summary>Items walked to get here (outermost first).</summary>
        public List<string> path = new List<string>();
        /// <summary>Building type involved (Build / SwitchRecipe / Collect / Feed).</summary>
        public string building;
        public string recipe;
        public int storedQty;
        public string storedWhere;
        public string text;
    }

    /// <summary>`milestoneInfo()` as data.</summary>
    public sealed class MilestoneInfo
    {
        /// <summary>"Spend N AP at the Ascension Shrine" line.</summary>
        public bool spendAp;
        public int ap;
        public MilestoneKind kind;
        // dragon tribute
        public int tributeNumber, tributeTotal;
        public double tributeProgress;
        /// <summary>Have/need rows (dragon remaining or gate cost/needs).</summary>
        public List<MilestoneNeedRow> needs = new List<MilestoneNeedRow>();
        // gate
        public bool gatePlaced;
        public MilestoneStep step;
        // ascend
        public int ascendReward;
        public bool allRegionsOpen;
        public GateOfferingInfo offerings;
        /// <summary>Disciples are eating and no Mill/Brewery stands.</summary>
        public bool hungryDisciples;
        /// <summary>Primary building target (null = none).</summary>
        public string build;
        /// <summary>Every building the goal points at (build-menu 🎯 targets, in order).</summary>
        public List<string> builds = new List<string>();
    }

    /// <summary>
    /// Quests (§13.2), region unlocks with installments (§3.6), Ascension Gate
    /// offerings (§14.1-14.2) and the milestone tracker (ui.js milestoneInfo /
    /// stepToward, ported as pure logic).
    /// </summary>
    public sealed class ProgressionSystem
    {
        readonly SimContext _ctx;
        public ProgressionSystem(SimContext ctx) { _ctx = ctx; }

        GameConfig Cfg => _ctx.Config;
        GameState S => _ctx.State;
        HandSystem Hand => _ctx.Hand;

        string ItemName(string it) => Cfg.Item(it)?.name ?? it;
        string SrcHint(string it) => Cfg.Item(it)?.sourceHint ?? "";

        // ================================================================
        // quests
        // ================================================================

        public QuestDef CurrentQuest => S.quest.idx >= 0 && S.quest.idx < Cfg.quests.Count ? Cfg.quests[S.quest.idx] : null;
        public bool ChainFinished => S.quest.idx >= Cfg.quests.Count;

        /// <summary>`questProgress(i)` — live goal; null for an unknown index.</summary>
        public QuestProgressInfo Progress(int i)
        {
            var p = new QuestProgressInfo();
            return Progress(i, p) ? p : null;
        }

        /// <summary>Non-allocating <see cref="Progress(int)"/> into <paramref name="into"/>; false = unknown index (null).</summary>
        public bool Progress(int i, QuestProgressInfo into)
        {
            if (i < 0 || i >= Cfg.quests.Count) return false;
            var q = Cfg.quests[i];
            int cur = 0, need = Math.Max(1, q.goalNeed);
            switch (q.goalKind)
            {
                case QuestGoalKind.HandCount: cur = Hand.Count(q.goalItem); break;
                case QuestGoalKind.HandCountCapped:
                    foreach (var gi in q.goalItems) cur += Math.Min(Hand.Count(gi.item), gi.qty);
                    break;
                case QuestGoalKind.DragonStageAtLeast: need = 1; cur = S.dragon.stage >= q.goalStage ? 1 : 0; break;
                case QuestGoalKind.StatAtLeast: cur = (int)Math.Min(int.MaxValue, S.stats.Get(q.goalStat)); break;
                case QuestGoalKind.AnyRegionUnlocked:
                    need = 1;
                    foreach (var r in q.goalRegions) if (S.world.IsUnlocked(r)) { cur = 1; break; }
                    break;
                case QuestGoalKind.DragonTributeItem:
                {
                    int t = _ctx.Dragon.TributeOf(q.goalStage, q.goalItem);
                    need = Math.Max(1, t > 0 ? t : 1);
                    if (S.dragon.stage > q.goalStage) cur = need;
                    else
                    {
                        int paid = S.dragon.stage == q.goalStage ? S.dragon.paid.Get(q.goalItem) : 0;
                        cur = Math.Min(need, Hand.Count(q.goalItem) + paid);
                    }
                    break;
                }
                default: cur = 0; break;
            }
            into.cur = Math.Min(cur, need); into.need = need; into.done = cur >= need;
            return true;
        }

        readonly QuestProgressInfo _progressScratch = new QuestProgressInfo();

        /// <summary>Active quest exists and is unfinished (= CurrentProgress() is non-null and not done), without allocating.</summary>
        bool CurrentQuestUnfinished() => Progress(S.quest.idx, _progressScratch) && !_progressScratch.done;

        public QuestProgressInfo CurrentProgress() => Progress(S.quest.idx);

        /// <summary>`claimQuest()` — null when not done; advances the chain and pays item rewards (overflow drops below the Altar).</summary>
        public QuestClaim Claim()
        {
            var gq = S.quest;
            var p = Progress(gq.idx);
            if (p == null || !p.done) return null;
            var q = Cfg.quests[gq.idx];
            gq.idx++;
            var outp = new QuestClaim { id = q.id };
            foreach (var r in q.rewardItems)
            {
                if (!Cfg.IsItem(r.item) || !(r.qty > 0)) continue;
                _ctx.Flow.Produce(r.item, r.qty);
                int got = Hand.Add(r.item, r.qty);
                if (got > 0) outp.toHand.Set(r.item, got);
                if (r.qty - got > 0)
                {
                    var altar = S.Area("center")?.buildings.Find(b => b.type == "center");
                    double x, y;
                    if (altar != null)
                    {
                        x = _ctx.World.BuildingCenterPx(altar).x;
                        y = (altar.row + Cfg.BuildingSize(altar.type).h) * _ctx.Cell + 14;
                    }
                    else { x = _ctx.PlayPx / 2.0; y = _ctx.PlayPx / 2.0; }
                    _ctx.Ground.DropGround("center", r.item, r.qty - got, x, y);
                    outp.dropped.Set(r.item, r.qty - got);
                    outp.hasAt = true; outp.atArea = "center"; outp.atX = x; outp.atY = y;
                }
            }
            _ctx.Events.RaiseQuestClaimed(q.id);
            return outp;
        }

        /// <summary>Quest panel collapsed (GS.quest.hidden) — also hides the milestone block in the JS panel.</summary>
        public void SetHidden(bool hidden) => S.quest.hidden = hidden;

        /// <summary>"Unlocks:" preview of quest i (revealed building defs, reward items).</summary>
        public (List<BuildingDef> reveals, List<ItemQty> items) RewardPreview(int i)
        {
            var reveals = new List<BuildingDef>();
            var items = new List<ItemQty>();
            if (i < 0 || i >= Cfg.quests.Count) return (reveals, items);
            var q = Cfg.quests[i];
            foreach (var t in q.rewardReveal) { var d = Cfg.Building(t); if (d != null) reveals.Add(d); }
            foreach (var it in q.rewardItems) items.Add(it);
            return (reveals, items);
        }

        /// <summary>`questTargetRect` source — null when no target / done / region locked / missing.</summary>
        public QuestTargetInfo Target()
        {
            var t = new QuestTargetInfo();
            return Target(t) ? t : null;
        }

        /// <summary>
        /// Non-allocating <see cref="Target()"/>: overwrites every field of <paramref name="into"/> and
        /// returns true, or returns false where Target() would return null (into contents then undefined).
        /// </summary>
        public bool Target(QuestTargetInfo into)
        {
            var q = CurrentQuest;
            if (q?.target == null || string.IsNullOrEmpty(q.target.kind)) return false;
            string area = string.IsNullOrEmpty(q.target.area) ? "center" : q.target.area;
            if (Cfg.Region(area) == null || !S.world.IsUnlocked(area)) return false;
            if (!CurrentQuestUnfinished()) return false;
            var st = S.Area(area);
            var t = into;
            t.area = area; t.kind = q.target.kind; t.node = null; t.building = null; t.zone = null;
            if (q.target.kind == "fixture")
            {
                foreach (var n in st.nodes) if (n.isFixed && n.kind == q.target.id) { t.node = n; break; }
                return t.node != null;
            }
            if (q.target.kind == "enemyZone")
            {
                var ez = Cfg.Region(area).enemies;
                var rects = ez != null && ez.enabled ? Cfg.ZoneRects(ez.zone) : null;
                if (rects == null || rects.Count == 0) return false;
                t.zone = rects[0];
                return true;
            }
            string type = q.target.kind == "dragon" ? "dragon" : q.target.kind == "altar" ? "center" : q.target.id;
            foreach (var b in st.buildings) if (b.built && b.type == type) { t.building = b; break; }
            if (t.building == null) foreach (var b in st.buildings) if (b.type == type) { t.building = b; break; }
            return t.building != null;
        }

        /// <summary>`buildTargets()` — active quest's unbuilt `builds` (while unfinished), then milestone builds.</summary>
        public List<string> BuildTargets()
        {
            var res = new List<string>();
            var q = CurrentQuest;
            if (q != null && q.builds.Count > 0)
            {
                var p = CurrentProgress();
                if (p != null && !p.done) foreach (var t in q.builds) if (!S.builtTypes.Contains(t)) res.Add(t);
            }
            foreach (var mb in Milestone().builds) if (!res.Contains(mb)) res.Add(mb);
            return res;
        }

        readonly List<string> _mbScratch = new List<string>();

        /// <summary>Non-allocating <see cref="BuildTargets()"/>: clears and refills <paramref name="into"/> (same order).</summary>
        public void BuildTargets(List<string> into)
        {
            into.Clear();
            var q = CurrentQuest;
            if (q != null && q.builds.Count > 0 && CurrentQuestUnfinished())
                foreach (var t in q.builds) if (!S.builtTypes.Contains(t)) into.Add(t);
            MilestoneBuilds(_mbScratch);
            foreach (var mb in _mbScratch) if (!into.Contains(mb)) into.Add(mb);
        }

        // ================================================================
        // region unlocks (§3.6)
        // ================================================================

        /// <summary>`areaUnlockCost(k)` — null when the region has no cost (Center).</summary>
        public ItemCounts UnlockCost(string k)
        {
            var res = new ItemCounts();
            return UnlockCost(k, res) ? res : null;
        }

        /// <summary>Non-allocating <see cref="UnlockCost(string)"/> into <paramref name="into"/> (cleared with reuse); false = null (no cost).</summary>
        public bool UnlockCost(string k, ItemCounts into)
        {
            into.ClearReuse();
            var reg = Cfg.Region(k);
            if (reg == null || reg.unlockCost.Count == 0) return false;
            double frugal = Math.Pow(0.8, S.PerkLevel("frugal"));
            foreach (var c in reg.unlockCost) into.Set(c.item, Math.Max(1, (int)Math.Ceiling(_ctx.Timing.Scaled(c.qty) * frugal)));
            return true;
        }

        RegionPaid PaidEntry(string k, bool create)
        {
            foreach (var e in S.world.unlockPaid) if (e.region == k) return e;
            if (!create) return null;
            var n = new RegionPaid { region = k };
            S.world.unlockPaid.Add(n);
            return n;
        }

        void DeletePaid(string k) => S.world.unlockPaid.RemoveAll(e => e.region == k);

        /// <summary>`unlockPaidOf(k)` — installments paid so far (copy-free view; empty when none).</summary>
        public ItemCounts UnlockPaid(string k) => PaidEntry(k, false)?.paid ?? new ItemCounts();

        /// <summary>Non-allocating <see cref="UnlockPaid(string)"/>: copies the installments into <paramref name="into"/> (empty when none).</summary>
        public void UnlockPaid(string k, ItemCounts into) => into.CopyFromReuse(PaidEntry(k, false)?.paid);

        /// <summary>`unlockRemaining(k)` — null when no cost.</summary>
        public ItemCounts UnlockRemaining(string k)
        {
            var cost = UnlockCost(k);
            if (cost == null) return null;
            var paid = UnlockPaid(k);
            var rem = new ItemCounts();
            foreach (var e in cost) { int r = e.qty - paid.Get(e.item); if (r > 0) rem.Set(e.item, r); }
            return rem;
        }

        /// <summary>`canPayUnlock(k)`.</summary>
        public bool CanPayUnlock(string k)
        {
            var rem = UnlockRemaining(k);
            if (rem == null) return false;
            if (rem.Count == 0) return true;
            foreach (var e in rem) if (Hand.Count(e.item) > 0) return true;
            return false;
        }

        /// <summary>ui.js unlockPayState — afford / partial / cant + have/need for the unlock sign.</summary>
        public (UnlockPayState state, int have, int need) PayState(string k)
        {
            var rem = UnlockRemaining(k);
            if (rem == null) return (UnlockPayState.Cant, 0, 0);
            int have = 0, need = 0;
            foreach (var e in rem) { need += e.qty; have += Math.Min(e.qty, Hand.Count(e.item)); }
            var st = need > 0 && have >= need ? UnlockPayState.Afford : have > 0 ? UnlockPayState.Partial : UnlockPayState.Cant;
            return (st, have, need);
        }

        /// <summary>`openRegion(k)` — mark open; re-randomise surfaced nodes' dive times.</summary>
        public void OpenRegion(string k)
        {
            S.world.SetUnlocked(k, true);
            var reg = Cfg.Region(k);
            double win = reg != null && reg.surfaceWindow > 0 ? reg.surfaceWindow : Cfg.balance.defaultSurfaceWindowSec;
            var area = S.Area(k);
            if (area != null)
                foreach (var n in area.nodes)
                    if (n.surfaceUntil != 0) n.surfaceUntil = _ctx.Now + win * 1000 * (0.5 + _ctx.Rng.Next01());
            _ctx.Events.RaiseRegionUnlocked(k);
        }

        /// <summary>`unlockArea(k)` — pay installments from the hand; opens when nothing remains.</summary>
        public UnlockResult UnlockArea(string k)
        {
            var refused = new UnlockResult { kind = UnlockResultKind.Refused };
            if (Cfg.Region(k) == null || S.world.IsUnlocked(k)) return refused;
            var rem = UnlockRemaining(k);
            if (rem == null) return refused;
            int n = 0;
            foreach (var e in rem)
            {
                int take = Math.Min(e.qty, Hand.Count(e.item));
                if (take <= 0) continue;
                Hand.Take(e.item, take);
                _ctx.Flow.Consume(e.item, take);
                PaidEntry(k, true).paid.Add(e.item, take);
                n += take;
            }
            if (UnlockRemaining(k).Count > 0)
                return n > 0 ? new UnlockResult { kind = UnlockResultKind.Paid, paid = n } : refused;
            DeletePaid(k);
            OpenRegion(k);
            _ctx.Events.RaiseSound("unlock", k);
            return new UnlockResult { kind = UnlockResultKind.Unlocked, paid = n };
        }

        /// <summary>`refundToHand(items)` — overflow drops at Center (PLAY_PX/2, PLAY_PX/2 + 3 cells) tagged manual.</summary>
        public void RefundToHand(ItemCounts items)
        {
            if (items == null) return;
            foreach (var e in items)
            {
                if (!(e.qty > 0)) continue;
                _ctx.Flow.Unconsume(e.item, e.qty);   // installments handed back
                int left = e.qty - Hand.Add(e.item, e.qty);
                if (left > 0) _ctx.Ground.DropGround("center", e.item, left, _ctx.PlayPx / 2.0, _ctx.PlayPx / 2.0 + 3 * _ctx.Cell, GroundTag.Manual);
            }
        }

        /// <summary>
        /// The Frugal Frontier part of `buyPerk` (engine.js:183): after the
        /// unlock costs shrank, installments past the new cost go back to the
        /// hand, and regions with nothing left open on the spot (sfx unlock).
        /// Call right after raising perks["frugal"].
        /// </summary>
        public void ReconcileUnlockInstallments()
        {
            foreach (var entry in new List<RegionPaid>(S.world.unlockPaid))
            {
                string k = entry.region;
                if (Cfg.Region(k) == null || S.world.IsUnlocked(k)) continue;
                var cost = UnlockCost(k) ?? new ItemCounts();
                var over = new ItemCounts();
                foreach (var e in new List<ItemQty>(entry.paid.entries))
                {
                    int x = e.qty - cost.Get(e.item);
                    if (x > 0) { over.Set(e.item, x); entry.paid.Set(e.item, e.qty - x); }
                    if (!(entry.paid.Get(e.item) > 0)) entry.paid.Remove(e.item);
                }
                RefundToHand(over);
                var rem = UnlockRemaining(k);
                if (rem != null && rem.Count == 0)
                {
                    DeletePaid(k);
                    OpenRegion(k);
                    _ctx.Events.RaiseSound("unlock", k);
                }
            }
        }

        // ================================================================
        // Ascension Gate offerings (§14.1-14.2)
        // ================================================================

        /// <summary>`gateOfferings(b)`.</summary>
        public GateOfferingInfo GateOfferings(Building b)
        {
            var G = Cfg.gateOfferings;
            int cap = G.cap, per = G.perType > 0 ? G.perType : cap;
            var offered = b?.offered ?? new ItemCounts();
            int count = 0;
            foreach (var it in G.items) count += Math.Max(0, Math.Min(per, offered.Get(it)));
            return new GateOfferingInfo { count = Math.Min(cap, count), cap = cap, perType = per, offered = offered };
        }

        /// <summary>`gateTakes(b,item)`.</summary>
        public bool GateTakes(Building b, string item)
        {
            var off = GateOfferings(b);
            return Cfg.gateOfferings.items.Contains(item) && off.count < off.cap && off.offered.Get(item) < off.perType;
        }

        /// <summary>`builtGate()` — first built gate anywhere (area order).</summary>
        public (string area, Building b) BuiltGate()
        {
            foreach (var a in S.areas)
                foreach (var b in a.buildings)
                    if (b.built && Cfg.Building(b.type)?.gate == true) return (a.key, b);
            return (null, null);
        }

        /// <summary>`vowMult()`.</summary>
        public double VowMult()
        {
            var m = Cfg.balance.vowMult;
            if (m == null || m.Count == 0) return 1;
            return m[Math.Min(S.vows.active.Count, m.Count - 1)];
        }

        /// <summary>`ascendReward()` — AP for ascending now.</summary>
        public int AscendReward()
        {
            int regions = 0;
            foreach (var f in S.world.unlocked) if (f.unlocked) regions++;
            var (_, g) = BuiltGate();
            int offerings = g != null ? GateOfferings(g).count : 0;
            return (int)Math.Round((3 + 2 * Math.Max(0, regions - 1) + S.PerkLevel("apgain") + offerings) * VowMult(), MidpointRounding.AwayFromZero);
        }

        /// <summary>`perkCost(id)` — AP price of the next level; null when maxed/unknown.</summary>
        public int? PerkCost(string id)
        {
            var def = Cfg.Perk(id);
            if (def == null) return null;
            int lvl = S.PerkLevel(id);
            return lvl >= def.max || lvl >= def.cost.Count ? (int?)null : def.cost[lvl];
        }

        /// <summary>dropFromHand step 11 (engine.js:2036). false = fall through (front item is not an offering, nothing to reorder).</summary>
        public bool GateFeed(string areaKey, Building b, out DropResult result)
        {
            result = null;
            var items = Cfg.gateOfferings.items;
            var first = S.hand.Count > 0 ? S.hand[0] : null;
            if (first != null && items.Contains(first.item))
            {
                if (GateTakes(b, first.item))
                {
                    string it = first.item;
                    Hand.Take(it, 1);
                    _ctx.Flow.Consume(it, 1);
                    b.offered ??= new ItemCounts();
                    b.offered.Add(it, 1);
                    b.offerings = GateOfferings(b).count;
                    result = DropResult.Of(DropResultKind.Fed, it);
                    return true;
                }
                foreach (var it in items)
                    if (Hand.Count(it) > 0 && GateTakes(b, it)) { Hand.MoveToFront(it); result = DropResult.Of(DropResultKind.Reordered, it); return true; }
                return true;
            }
            foreach (var it in items)
                if (Hand.Count(it) > 0 && GateTakes(b, it)) { Hand.MoveToFront(it); result = DropResult.Of(DropResultKind.Reordered, it); return true; }
            return false;
        }

        // ================================================================
        // milestone tracker (ui.js:2012-2178)
        // ================================================================

        bool BuiltAnywhere(string type)
        {
            foreach (var a in S.areas) foreach (var b in a.buildings) if (b.built && b.type == type) return true;
            return false;
        }

        /// <summary>`storedCount(item)` — units in built Storehouses / Warding Seals anywhere.</summary>
        public (int qty, string where) StoredCount(string item)
        {
            int qty = 0, sh = 0, seal = 0;
            foreach (var a in S.areas)
                foreach (var b in a.buildings)
                {
                    if (!b.built || b.item != item || !(b.qty > 0)) continue;
                    if (b.type == "storehouse") { qty += b.qty; sh += b.qty; }
                    else if (Cfg.Building(b.type)?.seal.enabled == true) { qty += b.qty; seal += b.qty; }
                }
            return (qty, sh >= seal ? "Storehouse" : "Warding Seal");
        }

        /// <summary>`producerTypes(item)` — types with a recipe output or generator of item.</summary>
        public List<string> ProducerTypes(string item)
        {
            var res = new List<string>();
            foreach (var bc in Cfg.buildings)
            {
                bool p = bc.gen.enabled && bc.gen.item == item;
                foreach (var r in bc.recipes) if (r.output == item) p = true;
                if (p) res.Add(bc.key);
            }
            return res;
        }

        static string AAn(string w) => (w.Length > 0 && "aeiouAEIOU".IndexOf(w[0]) >= 0 ? "an " : "a ") + w;

        struct Prod { public BuildingDef bc; public RecipeDef r; public int ri; }

        /// <summary>`stepToward(item, path, need)` — the first missing sub-step toward need × item.</summary>
        public MilestoneStep StepToward(string item, List<string> path = null, int need = 1)
        {
            path ??= new List<string>();
            if (need <= 0) need = 1;
            string head = path.Count > 0 ? string.Join(" › ", path.ConvertAll(ItemName)) + " › " : "";
            string label = ItemName(item);
            var step = new MilestoneStep { item = item, path = new List<string>(path) };
            var st = StoredCount(item);
            if (st.qty > 0 && Hand.Count(item) + st.qty >= need)
            {
                step.kind = MilestoneStepKind.Withdraw; step.storedQty = st.qty; step.storedWhere = st.where;
                step.text = $"{head}{label}: {st.qty} in {AAn(st.where)} — left-click it to withdraw";
                return step;
            }
            var prods = new List<Prod>();
            foreach (var bc in Cfg.buildings)
                for (int ri = 0; ri < bc.recipes.Count; ri++)
                    if (bc.recipes[ri].output == item) prods.Add(new Prod { bc = bc, r = bc.recipes[ri], ri = ri });
            if (prods.Count == 0 || path.Count >= 4)
            {
                step.kind = MilestoneStepKind.Gather;
                string h = SrcHint(item);
                step.text = $"{head}{label} ← {(string.IsNullOrEmpty(h) ? "gather it" : h)}";
                return step;
            }
            var running = new List<(Building b, int p)>();
            var idle = new List<(Building b, int p)>();
            foreach (var a in S.areas)
                foreach (var b in a.buildings)
                {
                    if (!b.built) continue;
                    int firstMine = -1, on = -1;
                    for (int i = 0; i < prods.Count; i++)
                    {
                        if (prods[i].bc.key != b.type) continue;
                        if (firstMine < 0) firstMine = i;
                        if (on < 0 && prods[i].ri == b.recipe) on = i;
                    }
                    if (firstMine < 0) continue;
                    if (on >= 0) running.Add((b, on)); else idle.Add((b, firstMine));
                }
            if (running.Count == 0 && idle.Count > 0)
            {
                var p = prods[idle[0].p];
                step.kind = MilestoneStepKind.SwitchRecipe; step.building = p.bc.key; step.recipe = p.r.name;
                step.text = $"{head}{label} ← switch the {p.bc.name} to {p.r.name} (click it)";
                return step;
            }
            if (running.Count == 0)
            {
                var pick = prods[0];
                foreach (var p in prods) if (_ctx.Buildings.IsBuildingUnlocked(p.bc.key)) { pick = p; break; }
                var ins = new List<string>();
                foreach (var i in pick.r.inputs) ins.Add(ItemName(i.item));
                step.kind = MilestoneStepKind.Build; step.building = pick.bc.key; step.recipe = pick.r.name;
                step.text = $"{head}{label} ← build {AAn(pick.bc.name)} ({string.Join(" + ", ins)})";
                return step;
            }
            int pi = running[0].p;
            var pk = prods[pi];
            var names = new List<string>();
            foreach (var i in pk.r.inputs) names.Add(ItemName(i.item));
            bool stocked = true;
            foreach (var inp in pk.r.inputs)
            {
                int inStock = 0;
                foreach (var (b, p) in running) if (p == pi) inStock += b.stock?.Get(inp.item) ?? 0;
                if (inStock < inp.qty) stocked = false;
                int have = Hand.Count(inp.item) + StoredCount(inp.item).qty + inStock;
                if (have < inp.qty)
                {
                    var np = new List<string>(path) { item };
                    return StepToward(inp.item, np, inp.qty);
                }
            }
            step.building = pk.bc.key; step.recipe = pk.r.name;
            if (stocked)
            {
                step.kind = MilestoneStepKind.Collect;
                step.text = $"{head}{label} ← the {pk.bc.name} is making it — collect the output";
            }
            else
            {
                step.kind = MilestoneStepKind.Feed;
                step.text = $"{head}{label} ← feed the {pk.bc.name}: {string.Join(" + ", names)}";
            }
            return step;
        }

        List<MilestoneNeedRow> NeedRows(ItemCounts rem)
        {
            var rows = new List<MilestoneNeedRow>();
            foreach (var e in rem)
                rows.Add(new MilestoneNeedRow { item = e.item, have = Hand.Count(e.item), need = e.qty, sourceHint = SrcHint(e.item) });
            return rows;
        }

        // ---- non-allocating milestone build targets (mirrors Milestone().builds; strings never built) ----

        readonly ItemCounts _remScratch = new ItemCounts();
        readonly List<Prod> _prodScratch = new List<Prod>();
        readonly List<(Building b, int p)> _runScratch = new List<(Building b, int p)>();

        /// <summary>
        /// Non-allocating <c>Milestone().builds</c>: clears and refills <paramref name="into"/> with the same
        /// building types in the same order, without building the milestone text / need rows.
        /// </summary>
        public void MilestoneBuilds(List<string> into)
        {
            into.Clear();
            string build = null;
            var builds = into;
            var dragon = _ctx.Dragon;
            if (dragon.CurrentStage != null)
            {
                var rem = _remScratch;
                dragon.RemainingInto(rem);
                foreach (var e in rem)
                {
                    if (Hand.Count(e.item) >= e.qty) continue;
                    // ProducerTypes(e.item): Exists(BuiltAnywhere) → skip; else first unlocked type
                    bool anyBuilt = false;
                    string firstUnlocked = null;
                    foreach (var bc in Cfg.buildings)
                    {
                        if (!Produces(bc, e.item)) continue;
                        if (BuiltAnywhere(bc.key)) { anyBuilt = true; break; }
                        if (firstUnlocked == null && _ctx.Buildings.IsBuildingUnlocked(bc.key)) firstUnlocked = bc.key;
                    }
                    if (anyBuilt) continue;
                    if (firstUnlocked != null && !builds.Contains(firstUnlocked)) builds.Add(firstUnlocked);
                }
            }
            else
            {
                Building gate = null;
                foreach (var a in S.areas)
                    foreach (var b in a.buildings)
                        if (Cfg.Building(b.type)?.gate == true && (gate == null || b.built)) gate = b;
                if (gate == null || !gate.built)
                {
                    string gType = null;
                    foreach (var bc in Cfg.buildings) if (bc.gate) { gType = bc.key; break; }
                    var rem = _remScratch;
                    if (gate != null) _ctx.Buildings.NeedsInto(gate, rem);
                    else
                    {
                        rem.ClearReuse();
                        if (gType != null) foreach (var c in Cfg.Building(gType).cost) rem.Set(c.item, c.qty);
                    }
                    string miss = null;
                    foreach (var e in rem) if (Hand.Count(e.item) < e.qty) { miss = e.item; break; }
                    if (gate == null && miss == null) build = gType;
                    else if (miss != null) build = StepTowardBuild(miss, 0, rem.Get(miss));
                }
            }
            if (!BuiltAnywhere("mill") && !BuiltAnywhere("brewery"))
            {
                bool hungry = false;
                foreach (var a in S.areas)
                    foreach (var b in a.buildings)
                        if (b.built && Cfg.Building(b.type)?.roster.enabled == true && b.disciples > 0) hungry = true;
                if (hungry && !builds.Contains("mill")) builds.Add("mill");
            }
            if (build != null && !builds.Contains(build)) builds.Insert(0, build);
        }

        static bool Produces(BuildingDef bc, string item)
        {
            if (bc.gen.enabled && bc.gen.item == item) return true;
            foreach (var r in bc.recipes) if (r.output == item) return true;
            return false;
        }

        /// <summary>
        /// <c>StepToward(item, path, need)</c> reduced to its Build answer: the building type when the
        /// resulting step is <see cref="MilestoneStepKind.Build"/>, else null. <paramref name="depth"/> = path.Count.
        /// Scratch lists are only read before the (tail) recursive call, so sharing them is safe.
        /// </summary>
        string StepTowardBuild(string item, int depth, int need)
        {
            if (need <= 0) need = 1;
            var st = StoredCount(item);
            if (st.qty > 0 && Hand.Count(item) + st.qty >= need) return null;                 // Withdraw
            var prods = _prodScratch; prods.Clear();
            foreach (var bc in Cfg.buildings)
                for (int ri = 0; ri < bc.recipes.Count; ri++)
                    if (bc.recipes[ri].output == item) prods.Add(new Prod { bc = bc, r = bc.recipes[ri], ri = ri });
            if (prods.Count == 0 || depth >= 4) return null;                                    // Gather
            var running = _runScratch; running.Clear();
            bool anyIdle = false;
            foreach (var a in S.areas)
                foreach (var b in a.buildings)
                {
                    if (!b.built) continue;
                    int firstMine = -1, on = -1;
                    for (int i = 0; i < prods.Count; i++)
                    {
                        if (prods[i].bc.key != b.type) continue;
                        if (firstMine < 0) firstMine = i;
                        if (on < 0 && prods[i].ri == b.recipe) on = i;
                    }
                    if (firstMine < 0) continue;
                    if (on >= 0) running.Add((b, on)); else anyIdle = true;
                }
            if (running.Count == 0 && anyIdle) return null;                                     // SwitchRecipe
            if (running.Count == 0)
            {
                var pick = prods[0];
                foreach (var p in prods) if (_ctx.Buildings.IsBuildingUnlocked(p.bc.key)) { pick = p; break; }
                return pick.bc.key;                                                              // Build
            }
            int pi = running[0].p;
            var pk = prods[pi];
            foreach (var inp in pk.r.inputs)
            {
                int inStock = 0;
                foreach (var (b, p) in running) if (p == pi) inStock += b.stock?.Get(inp.item) ?? 0;
                int have = Hand.Count(inp.item) + StoredCount(inp.item).qty + inStock;
                if (have < inp.qty) return StepTowardBuild(inp.item, depth + 1, inp.qty);
            }
            return null;                                                                         // Collect / Feed
        }

        /// <summary>`milestoneInfo()` — the next big goal (dragon tribute → raise the Gate → ascend) + side lines.</summary>
        public MilestoneInfo Milestone()
        {
            var m = new MilestoneInfo();
            string build = null;
            var builds = m.builds;
            int ap = S.ascendPoints;
            m.ap = ap;
            if (!S.perkShopSeen && ap > 0)
            {
                int? cheapest = null;
                foreach (var pk in Cfg.perks) { var c = PerkCost(pk.id); if (c != null && (cheapest == null || c < cheapest)) cheapest = c; }
                m.spendAp = cheapest != null && ap >= cheapest;
            }
            var dragon = _ctx.Dragon;
            var st = dragon.CurrentStage;
            if (st != null)
            {
                m.kind = MilestoneKind.DragonTribute;
                m.tributeNumber = S.dragon.stage + 1;
                m.tributeTotal = Cfg.dragonStages.Count;
                var rem = dragon.Remaining();
                int paid = 0, all = 0;
                foreach (var n in st.needs) { int pd = S.dragon.paid.Get(n.item); paid += pd; all += pd + rem.Get(n.item); }
                foreach (var e in rem)
                {
                    if (Hand.Count(e.item) >= e.qty) continue;
                    var types = ProducerTypes(e.item);
                    if (types.Exists(BuiltAnywhere)) continue;
                    var t = types.Find(ty => _ctx.Buildings.IsBuildingUnlocked(ty));
                    if (t != null && !builds.Contains(t)) builds.Add(t);
                }
                m.needs = NeedRows(rem);
                m.tributeProgress = all > 0 ? (double)paid / all : 0;
            }
            else
            {
                Building gate = null;
                foreach (var a in S.areas)
                    foreach (var b in a.buildings)
                        if (Cfg.Building(b.type)?.gate == true && (gate == null || b.built)) gate = b;
                if (gate == null || !gate.built)
                {
                    m.kind = MilestoneKind.RaiseGate;
                    m.gatePlaced = gate != null;
                    string gType = null;
                    foreach (var bc in Cfg.buildings) if (bc.gate) { gType = bc.key; break; }
                    var rem = gate != null ? _ctx.Buildings.Needs(gate)
                        : gType != null ? new ItemCounts(Cfg.Building(gType).cost) : new ItemCounts();
                    string miss = null;
                    foreach (var e in rem) if (Hand.Count(e.item) < e.qty) { miss = e.item; break; }
                    MilestoneStep step;
                    if (miss != null) step = StepToward(miss, null, rem.Get(miss));
                    else step = gate != null
                        ? new MilestoneStep { kind = MilestoneStepKind.FeedGhost, building = gate.type, text = "Right-click the Gate ghost to feed it." }
                        : new MilestoneStep { kind = MilestoneStepKind.PlaceGate, building = gType, text = "Place it from the build menu (B)." };
                    if (gate == null && miss == null) build = gType;
                    else if (step.kind == MilestoneStepKind.Build) build = step.building;
                    m.needs = NeedRows(rem);
                    m.step = step;
                }
                else
                {
                    m.kind = MilestoneKind.Ascend;
                    bool allOpen = true;
                    foreach (var r in Cfg.regions) if (!S.world.IsUnlocked(r.key)) allOpen = false;
                    m.allRegionsOpen = allOpen;
                    m.offerings = GateOfferings(gate);
                    m.ascendReward = AscendReward();
                }
            }
            if (!BuiltAnywhere("mill") && !BuiltAnywhere("brewery"))
            {
                bool hungry = false;
                foreach (var a in S.areas)
                    foreach (var b in a.buildings)
                        if (b.built && Cfg.Building(b.type)?.roster.enabled == true && b.disciples > 0) hungry = true;
                if (hungry)
                {
                    m.hungryDisciples = true;
                    if (!builds.Contains("mill")) builds.Add("mill");
                }
            }
            if (build != null && !builds.Contains(build)) builds.Insert(0, build);
            m.build = build ?? (builds.Count > 0 ? builds[0] : null);
            return m;
        }
    }
}
