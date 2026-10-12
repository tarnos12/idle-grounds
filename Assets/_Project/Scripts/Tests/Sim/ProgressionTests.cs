using System;
using System.Collections.Generic;
using System.Linq;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    static class M5Util
    {
        public static Building Dragon(Simulation sim) => sim.State.Area("center").buildings.Single(b => b.type == "dragon");
        public static Building Altar(Simulation sim) => sim.State.Area("center").buildings.Single(b => b.type == "center");

        public static void Give(Simulation sim, ItemCounts items)
        {
            foreach (var e in items) Assert.AreEqual(e.qty, sim.Hand.Add(e.item, e.qty), "hand room for " + e.item);
        }

        /// <summary>Right-click b until <paramref name="done"/> or the hand stops changing anything.</summary>
        public static int ClickUntil(Simulation sim, string area, Building b, Func<bool> done, int max = 500)
        {
            int fed = 0;
            for (int i = 0; i < max && !done(); i++)
            {
                var r = M3Util.Click(sim, area, b);
                if (r == null) break;
                if (r.kind == DropResultKind.Fed) fed++;
            }
            return fed;
        }

        /// <summary>Select an upgrade, give its exact cost, feed the Altar until the level rises.</summary>
        public static void Buy(Simulation sim, string area, string type)
        {
            var cost = sim.UpgradeCost(area, type);
            Assert.IsNotNull(cost, type + " maxed");
            int before = sim.UpgradeLevel(area, type).lvl;
            Assert.IsTrue(sim.SelectUpgrade(area, type));
            sim.State.hand.Clear();
            Give(sim, cost);
            int fed = ClickUntil(sim, "center", Altar(sim), () => sim.UpgradeLevel(area, type).lvl > before);
            Assert.AreEqual(before + 1, sim.UpgradeLevel(area, type).lvl, type);
            Assert.AreEqual(cost.Total(), fed);
            Assert.IsNull(sim.State.upgradeJob);
            Assert.AreEqual(0, sim.Hand.Total());
        }
    }

    public class DragonTests
    {
        static readonly string[] StageUnlocks = { "forge", "algae_farm", "herb_garden" };

        [Test]
        public void EachStage_ExactScaledTribute_UnlocksBuilding_ThenAwakens()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var S = sim.State;
            S.handCap = 5000;
            var dragon = M5Util.Dragon(sim);
            var advanced = new List<(int, string)>();
            int awakened = 0, sfx = 0;
            sim.Events.DragonStageAdvanced += (st, t) => advanced.Add((st, t));
            sim.Events.DragonAwakened += () => awakened++;
            sim.Events.SoundRequested += (n, a) => { if (n == "dragon") sfx++; };
            for (int i = 0; i < sim.Config.dragonStages.Count; i++)
            {
                var st = sim.Config.dragonStages[i];
                var expected = new ItemCounts();
                foreach (var n in st.needs)
                    expected.Set(n.item, Math.Max(1, (int)Math.Ceiling(n.qty * 0.5)));
                CollectionAssert.AreEqual(expected.entries.Select(e => e.ToString()), sim.DragonRemaining().entries.Select(e => e.ToString()), "stage " + i);
                if (i < StageUnlocks.Length) Assert.IsFalse(sim.IsBuildingUnlocked(StageUnlocks[i]));
                // one short: never advances
                var shortBy = expected.Clone();
                shortBy.Add(shortBy.entries[0].item, -1);
                S.hand.Clear();
                M5Util.Give(sim, shortBy);
                M5Util.ClickUntil(sim, "center", dragon, () => S.dragon.stage != i, 5000);
                Assert.AreEqual(i, S.dragon.stage);
                Assert.AreEqual(0, sim.Hand.Total());
                M5Util.Give(sim, new ItemCounts { { expected.entries[0].item, 1 } });
                clock.Advance(10);
                M3Util.Click(sim, "center", dragon);
                Assert.AreEqual(i + 1, S.dragon.stage, "stage " + i + " completes");
                Assert.AreEqual(0, S.dragon.paid.Count);
                Assert.AreEqual(st.text, S.dragon.dialog);
                Assert.AreEqual(st.text, sim.DragonMessage());
                Assert.AreEqual(clock.NowMs + 8000, S.dragon.msgUntil);
                if (i < StageUnlocks.Length) Assert.IsTrue(sim.IsBuildingUnlocked(StageUnlocks[i]), StageUnlocks[i]);
            }
            Assert.IsNull(sim.DragonStage);
            Assert.IsTrue(S.won, "awake");
            Assert.IsFalse(sim.EndingPending, "ADR 0007: the awakened dragon is a mid-game moment, not the ending");
            Assert.AreEqual(4, advanced.Count); Assert.AreEqual(1, awakened); Assert.AreEqual(4, sfx);
            Assert.AreEqual(4, advanced[3].Item1);
            // awake: tributes ignored, nothing taken
            sim.Hand.Add("leaves", 3);
            Assert.IsNull(M3Util.Click(sim, "center", dragon));
            Assert.AreEqual(3, sim.Hand.Count("leaves"));
            sim.DismissDragonDialog();
            Assert.IsNull(S.dragon.dialog);
        }

        [Test]
        public void Tribute_OtherStagesScaled_CurrentReadsPaidPlusRemaining()
        {
            var sim = SimTestUtil.NewSim(out _);
            var cfg = sim.Config;
            var s0 = cfg.dragonStages[0].needs[0];
            var s1 = cfg.dragonStages[1].needs[0];
            Assert.AreEqual(sim.Timing.Scaled(s0.qty), sim.DragonTribute(0).Get(s0.item));   // current stage: paid + remaining
            Assert.AreEqual(sim.Timing.Scaled(s1.qty), sim.DragonTribute(1).Get(s1.item));   // other stages: scaled only
        }

        [Test]
        public void PillBlessing_Durations()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var S = sim.State;
            var dragon = M5Util.Dragon(sim);
            string started = null; double until = 0;
            sim.Events.BlessingStarted += (k, u) => { started = k; until = u; };
            sim.Hand.Add("ember_pill", 1);
            var r = M3Util.Click(sim, "center", dragon);
            Assert.AreEqual(DropResultKind.Fed, r.kind); Assert.IsTrue(r.once);
            Assert.AreEqual("ember_pill", S.buff.kind);
            Assert.AreEqual(clock.NowMs + 60000, S.buff.until);
            Assert.AreEqual(("ember_pill", S.buff.until), (started, until));
            Assert.AreEqual(0, S.dragon.stage);   // pills never count as tribute

            // affinity 2 (+30 s each), shrine (+60 s), bless perk 1 (×1.2)
            S.Area("center").upgrades.affinity = 2;
            Assert.AreEqual(120000, sim.Dragon.BlessingDurationMs(), 1e-6);
            Assert.IsNotNull(sim.Buildings.PlaceBuilt("center", "dragon_shrine", 55, 36));
            Assert.IsTrue(sim.ShrineBuilt());
            Assert.AreEqual(180000, sim.Dragon.BlessingDurationMs(), 1e-6);
            S.bonuses.Set("bless", 1);
            Assert.AreEqual(216000, sim.Dragon.BlessingDurationMs(), 1e-6);
            // replaces the active blessing
            sim.Hand.Add("verdant_pill", 1);
            M3Util.Click(sim, "center", dragon);
            Assert.AreEqual("verdant_pill", S.buff.kind);
            Assert.AreEqual(clock.NowMs + 216000, S.buff.until, 1e-6);
            Assert.IsNotNull(sim.ActiveBlessing());
            clock.Advance(216001);
            Assert.IsNull(sim.ActiveBlessing());

            // deeper pill + no tribute carried ⇒ brought to front
            sim.Hand.Add("wood", 2); sim.Hand.Add("swiftwind_pill", 1);
            var rr = M3Util.Click(sim, "center", dragon);
            Assert.AreEqual(DropResultKind.Reordered, rr.kind);
            Assert.AreEqual("swiftwind_pill", sim.Hand.Front);

            // TEST off ⇒ buffScale 4
            var cfg = SimTestUtil.LoadConfig(); cfg.test.enabled = false;
            var sim2 = SimTestUtil.NewSim(out _, cfg: cfg);
            Assert.AreEqual(240000, sim2.Dragon.BlessingDurationMs(), 1e-6);
        }

        [Test]
        public void VitalityPill_GrantsCombatBuff_AndEvent()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            double until = 0;
            sim.Events.CombatBuffStarted += u => until = u;
            sim.Hand.Add("vitality_pill", 2);
            var r = sim.DropFromHand("center", 100, 100);
            Assert.AreEqual(DropResultKind.Used, r.kind);
            Assert.AreEqual(clock.NowMs + 45000, sim.State.combatBuff.until);
            Assert.AreEqual(sim.State.combatBuff.until, until);
            Assert.AreEqual(1, sim.Hand.Count("vitality_pill"));
            Assert.IsNotNull(sim.ActiveCombatBuff());
            Assert.AreEqual(1 + 2, sim.Upgrades.AttackDamage("center", clock.NowMs));
        }

        [Test]
        public void Scales_Cadence_PileCap_ShrineDoubles()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            sim.NoGroundPhysics = true;
            var S = sim.State;
            var c = S.Area("center");
            int Scales() => SimTestUtil.CountGround(c, "dragon_scale");
            sim.Tick();
            Assert.AreEqual(0, S.dragonScaleAt);           // asleep: nothing
            S.dragon.stage = 4;                            // awake (won not set ⇒ prestigeFactor 1)
            sim.Tick();
            double interval = 45000 * 0.2;
            Assert.AreEqual(clock.NowMs + interval, S.dragonScaleAt, 1e-6);
            Assert.AreEqual(0, Scales());
            clock.Advance((long)interval - 50); sim.Tick();
            Assert.AreEqual(0, Scales());
            clock.Advance(50); sim.Tick();
            Assert.AreEqual(1, Scales());
            Assert.AreEqual(clock.NowMs + interval, S.dragonScaleAt, 1e-6);
            // a long gap sheds ONE scale (no catch-up)
            clock.Advance((long)interval * 3); sim.Tick();
            Assert.AreEqual(2, Scales());
            for (int i = 0; i < 6; i++) { clock.Advance((long)interval); sim.Tick(); }
            Assert.AreEqual(5, Scales());                  // pile cap
            var (dx, dy) = sim.World.BuildingCenterPx(M5Util.Dragon(sim));
            foreach (var g in c.ground.Where(g => g.item == "dragon_scale"))
                Assert.LessOrEqual(Math.Sqrt((g.x - dx) * (g.x - dx) + (g.y - dy) * (g.y - dy)), 6 * 32);
            // shrine halves the interval
            c.ground.RemoveAll(g => g.item == "dragon_scale");
            sim.Buildings.PlaceBuilt("center", "dragon_shrine", 55, 36);
            Assert.AreEqual(interval / 2, sim.Dragon.ScaleIntervalNowMs(), 1e-6);
            clock.Advance((long)interval); sim.Tick();      // fires on the old schedule, re-arms at half
            Assert.AreEqual(clock.NowMs + interval / 2, S.dragonScaleAt, 1e-6);
            clock.Advance((long)(interval / 2)); sim.Tick();
            Assert.AreEqual(2, Scales());
        }
    }

    public class UpgradeTests
    {
        static Simulation NewSim(out ManualClock clock)
        {
            var sim = SimTestUtil.NewSim(out clock);
            sim.State.handCap = 1000;
            return sim;
        }

        [Test]
        public void EveryNodeType_AppliesItsEffect()
        {
            var sim = NewSim(out var clock);
            var S = sim.State;
            var center = S.Area("center");
            double now = clock.NowMs;
            int applied = 0;
            var appliedTypes = new List<string>();
            sim.Events.UpgradeApplied += (a, t) => { applied++; appliedTypes.Add(a + ":" + t); };

            // hand: +5 cap
            int cap = S.handCap;
            M5Util.Buy(sim, "center", "hand");
            Assert.AreEqual(cap + 5, S.handCap); Assert.AreEqual(1, S.handLevel);

            // speed: regrow ×0.8
            var node = new Node { regrowSec = 10 };
            double r0 = sim.Nodes.RegrowDelayMs("center", node);
            M5Util.Buy(sim, "center", "speed");
            Assert.AreEqual(r0 * 0.8, sim.Nodes.RegrowDelayMs("center", node), 1e-6);

            // harvestSpeed (fishing node act_fi): interval ×0.8
            var fishing = S.Area("fishing");
            var sw = new Node { swingMs = 350 };
            double h0 = sim.Timing.HarvestInterval(S, fishing, sw);
            M5Util.Buy(sim, "fishing", "harvestSpeed");
            Assert.AreEqual(h0 * 0.8, sim.Timing.HarvestInterval(S, fishing, sw), 1e-6);

            // automation: bot budget 0 → AUTOMATION_CLICKS[1]
            Assert.AreEqual(0, sim.Upgrades.AutomationBudget("center"));
            M5Util.Buy(sim, "center", "automation");
            Assert.AreEqual(2, sim.Upgrades.AutomationBudget("center"));

            // quarry: center stone generator interval ×0.8
            var gen = sim.Config.Region("center").generators.Single(g => g.kind == "stone");
            double q0 = sim.FieldGenerators.IntervalMs(center, gen);
            M5Util.Buy(sim, "center", "quarry");
            Assert.AreEqual(q0 * 0.8, sim.FieldGenerators.IntervalMs(center, gen), 1e-6);

            // enemyCap / damage / aoe
            int ec = sim.Upgrades.EnemyCap("center");
            M5Util.Buy(sim, "center", "enemyCap");
            Assert.AreEqual(ec + 1, sim.Upgrades.EnemyCap("center"));
            M5Util.Buy(sim, "center", "damage");
            Assert.AreEqual(2, sim.Upgrades.AttackDamage("center", now));
            M5Util.Buy(sim, "center", "aoe");
            Assert.AreEqual(48, sim.Upgrades.AoeRadiusPx("center"), 1e-6);

            // wispRate: beat ×0.85, speed ×1.25 (center lanterns)
            var lantern = sim.Config.Building("wisp_lantern");
            double b0 = sim.Logistics.BeatMs(center, lantern, now), s0 = sim.Logistics.WispSpeed(center, lantern, now);
            M5Util.Buy(sim, "center", "wispRate");
            Assert.AreEqual(b0 * 0.85, sim.Logistics.BeatMs(center, lantern, now), 1e-6);
            Assert.AreEqual(s0 * 1.25, sim.Logistics.WispSpeed(center, lantern, now), 1e-6);

            // affinity: blessing +30 s
            double d0 = sim.Dragon.BlessingDurationMs();
            M5Util.Buy(sim, "center", "affinity");
            Assert.AreEqual(d0 + 30000, sim.Dragon.BlessingDurationMs(), 1e-6);

            // discipleCap: roster +2 on every pavilion
            var pav = sim.Buildings.PlaceBuilt("center", "meditation_pavilion", 55, 36);
            Assert.AreEqual(3, sim.RosterCap(pav));
            M5Util.Buy(sim, "center", "discipleCap");
            Assert.AreEqual(5, sim.RosterCap(pav));

            Assert.AreEqual(11, applied);
            Assert.AreEqual(11, S.stats.upgradesApplied);
            // every tree node type was covered
            var types = sim.Config.upgradeTree.Select(n => n.type).Distinct().ToList();
            foreach (var t in types) Assert.IsTrue(appliedTypes.Any(x => x.EndsWith(":" + t)), "covered " + t);
        }

        [Test]
        public void Costs_Scaled_Maxed_AndSwitchRefunds()
        {
            var sim = NewSim(out _);
            var S = sim.State;
            Assert.AreEqual(sim.Timing.Scaled(30), sim.UpgradeCost("center", "hand").Get("wood"));     // scaled(node cost)
            for (int i = 0; i < 3; i++) M5Util.Buy(sim, "center", "hand");
            Assert.IsNull(sim.UpgradeCost("center", "hand"));
            Assert.IsFalse(sim.SelectUpgrade("center", "hand"));
            Assert.IsFalse(sim.SelectUpgrade("center", "nope"));

            // partial job, switch ⇒ paid drops at the Altar (manual)
            int spdCost = sim.Timing.Scaled(60);
            Assert.AreEqual(spdCost, sim.UpgradeCost("center", "speed").Get("wood"));
            Assert.IsTrue(sim.SelectUpgrade("center", "speed"));
            sim.Hand.Add("wood", 4);
            M5Util.ClickUntil(sim, "center", M5Util.Altar(sim), () => sim.Hand.Count("wood") == 0);
            Assert.AreEqual(4, S.upgradeJob.paid.Get("wood"));
            Assert.AreEqual(spdCost - 4, sim.UpgradeJobRemaining().Get("wood"));
            Assert.IsTrue(sim.SelectUpgrade("center", "speed"));   // same job: kept
            Assert.AreEqual(4, S.upgradeJob.paid.Get("wood"));
            var c = S.Area("center");
            int before = SimTestUtil.CountGround(c, "wood");
            Assert.IsTrue(sim.SelectUpgrade("center", "harvestSpeed"));
            Assert.AreEqual(before + 4, SimTestUtil.CountGround(c, "wood"));
            Assert.AreEqual("harvestSpeed", S.upgradeJob.type);
            Assert.AreEqual(0, S.upgradeJob.paid.Count);
            // no job ⇒ altar click does nothing
            sim.CancelUpgradeJob();
            sim.Hand.Add("wood", 1);
            Assert.IsNull(M3Util.Click(sim, "center", M5Util.Altar(sim), noGround: false));
            Assert.AreEqual(1, sim.Hand.Count("wood"));
        }

        [Test]
        public void Tree_Visibility_And_Selectability()
        {
            var sim = NewSim(out _);
            UpgradeNodeState N(string id) => sim.UpgradeTree().Single(s => s.node.id == id);
            Assert.IsTrue(N("hand").selectable);
            Assert.AreEqual(UpgradeNodeTier.Full, N("wisps").tier);
            Assert.IsFalse(N("wisps").selectable);
            Assert.AreEqual(UpgradeNodeTier.Mystery, N("affinity").tier);
            Assert.AreEqual(UpgradeNodeTier.Hidden, N("disciples").tier);
            Assert.IsFalse(sim.SelectUpgradeNode("wisps"));
            M5Util.Buy(sim, "center", "hand");
            Assert.IsTrue(N("hand").owned);
            Assert.IsTrue(N("wisps").selectable);
            Assert.IsFalse(N("affinity").selectable);
            Assert.IsTrue(sim.SelectUpgradeNode("wisps"));
            Assert.IsTrue(N("wisps").selected);
            Assert.AreEqual(sim.Timing.Scaled(120), N("wisps").nextCost.Get("wood"));
        }
    }

    public class QuestTests
    {
        [Test]
        public void EveryQuest_CompletesFromConstructedState_AndClaimPaysRewards()
        {
            var sim = SimTestUtil.NewSim(out _);
            var S = sim.State;
            S.handCap = 100;
            var Q = sim.Config.quests;
            Assert.AreEqual(17, Q.Count);
            var claimed = new List<string>();
            sim.Events.QuestClaimed += id => claimed.Add(id);
            var setups = new Dictionary<string, Action>
            {
                ["wood"] = () => sim.Hand.Add("wood", 5),
                ["leaves"] = () => sim.Hand.Add("leaves", 5),
                ["dragon1"] = () => S.dragon.stage = 1,
                ["fox"] = () => S.stats.foxKills = 1,
                ["build"] = () => S.stats.buildingsBuilt = 1,
                ["upgrade"] = () => S.stats.upgradesApplied = 1,
                ["link"] = () => S.stats.linksAdded = 1,
                ["explore"] = () => S.world.SetUnlocked("mine", true),
                ["dragon2"] = () => S.dragon.stage = 2,
                ["iron"] = () => { int n = sim.DragonTribute(2).Get("iron_bar"); sim.Hand.Add("iron_bar", n / 2); S.dragon.paid.Set("iron_bar", n - n / 2); },
                ["waters"] = () => S.world.SetUnlocked("fishing", true),
                ["dragon3"] = () => S.dragon.stage = 3,
                ["weaver"] = () => { sim.Hand.Add("rope", 5); sim.Hand.Add("cloth", 6); },
                ["cultivate"] = () => S.stats.disciplesRecruited = 1,
                ["bridge"] = () =>
                {
                    var bf = IslandQuestTests.PlaceBridge(sim, "fishing");
                    var bc = IslandQuestTests.PlaceBridge(sim, "center");
                    Assert.IsNull(sim.PairBridges("fishing", bf.id, "center", bc.id));
                },
                ["caravan"] = () => S.stats.bridgeDelivered = 30,
                ["wine"] = () => S.flow.Produce("spirit_wine", 3),
            };
            Assert.IsFalse(sim.IsBuildingUnlocked("storehouse"));
            for (int i = 0; i < Q.Count; i++)
            {
                var q = Q[i];
                Assert.AreEqual(i, S.quest.idx);
                S.hand.Clear();
                var p0 = sim.QuestProgress(i);
                Assert.IsFalse(p0.done, q.id + " not done before");
                Assert.IsNull(sim.ClaimQuest(), q.id + " unclaimable");
                setups[q.id]();
                var p = sim.QuestProgress(i);
                Assert.IsTrue(p.done, q.id + " done");
                Assert.AreEqual(p.need, p.cur);
                if (q.id == "iron") Assert.AreEqual(sim.DragonTribute(2).Get("iron_bar"), p.need);     // the third tribute's iron bars
                if (q.id == "weaver") Assert.AreEqual(8, p.need);
                var before = new ItemCounts();
                foreach (var r in q.rewardItems) before.Set(r.item, sim.Hand.Count(r.item));
                var res = sim.ClaimQuest();
                Assert.IsNotNull(res); Assert.AreEqual(q.id, res.id);
                Assert.AreEqual(i + 1, S.quest.idx);
                foreach (var r in q.rewardItems)
                {
                    Assert.AreEqual(before.Get(r.item) + r.qty, sim.Hand.Count(r.item), q.id + " reward " + r.item);
                    Assert.AreEqual(r.qty, res.toHand.Get(r.item));
                }
                foreach (var t in q.rewardReveal)
                    if (sim.Config.reveal.Any(rr => rr.building == t && rr.any.Any(c => c.kind == RevealKind.Quest && c.key == q.id)))
                        Assert.IsTrue(sim.IsBuildingUnlocked(t), q.id + " reveals " + t);
            }
            Assert.AreEqual(Q.Select(q => q.id), claimed);
            Assert.IsNull(sim.QuestProgress(Q.Count));
            Assert.IsNull(sim.ClaimQuest());
        }

        [Test]
        public void Claim_OverflowDropsBelowAltar()
        {
            var sim = SimTestUtil.NewSim(out _);
            var S = sim.State;
            S.quest.idx = sim.Config.QuestIndex("upgrade");
            S.stats.upgradesApplied = 1;
            sim.Hand.Add("stone", S.handCap - 3);
            var c = S.Area("center");
            int before = SimTestUtil.CountGround(c, "wood");
            var res = sim.ClaimQuest();
            Assert.AreEqual(3, res.toHand.Get("wood"));
            Assert.AreEqual(7, res.dropped.Get("wood"));
            Assert.IsTrue(res.hasAt);
            var altar = M5Util.Altar(sim);
            Assert.AreEqual((altar.row + 5) * 32 + 14, res.atY);
            Assert.AreEqual(before + 7, SimTestUtil.CountGround(c, "wood"));
        }

        [Test]
        public void Preview_Target_Hidden()
        {
            var sim = SimTestUtil.NewSim(out _);
            var (rev, items) = sim.QuestRewardPreview(sim.Config.QuestIndex("dragon2"));
            Assert.AreEqual("algae_farm", rev.Single().key);
            Assert.AreEqual(8, items.Single(i => i.item == "wood").qty);
            var t = sim.QuestTarget();
            Assert.AreEqual("fixture", t.kind); Assert.AreEqual("spirittree", t.node.kind);
            sim.State.quest.idx = sim.Config.QuestIndex("dragon1");
            Assert.AreSame(M5Util.Dragon(sim), sim.QuestTarget().building);
            sim.SetQuestPanelHidden(true);
            Assert.IsTrue(sim.State.quest.hidden);
        }

        [Test]
        public void Milestone_DragonThenCelestialThenGateStepThenAscend()
        {
            var sim = SimTestUtil.NewSim(out _, init: false);
            var S = sim.State;
            var m = sim.Milestone();
            Assert.AreEqual(MilestoneKind.DragonTribute, m.kind);
            Assert.AreEqual((1, 4), (m.tributeNumber, m.tributeTotal));
            Assert.AreEqual("leaves", m.needs.Single().item);
            Assert.AreEqual(sim.Timing.Scaled(sim.Config.dragonStages[0].needs[0].qty), m.needs.Single().need);
            Assert.IsFalse(string.IsNullOrEmpty(m.needs.Single().sourceHint));

            S.dragon.stage = 2;   // iron_bar wanted, forge revealed but not standing ⇒ build target
            m = sim.Milestone();
            Assert.Contains("forge", m.builds);

            S.dragon.stage = 4; S.quest.idx = sim.Config.quests.Count;
            Assert.AreEqual(MilestoneKind.OpenIsland, sim.Milestone().kind, "the Heavenly Gate needs Celestial Peak first");
            S.world.SetUnlocked("celestial", true);
            m = sim.Milestone();
            Assert.AreEqual(MilestoneKind.RaiseGate, m.kind);
            Assert.IsFalse(m.gatePlaced);
            Assert.AreEqual(MilestoneStepKind.Build, m.step.kind);
            Assert.AreEqual("talisman_atelier", m.step.building);
            Assert.AreEqual("talisman_atelier", m.build);

            // a standing atelier on its recipe, no stock ⇒ chase the first input (paper) ⇒ build a paper mill
            Assert.IsNotNull(sim.Buildings.PlaceBuilt("center", "talisman_atelier", 55, 36));
            m = sim.Milestone();
            Assert.AreEqual(MilestoneStepKind.Build, m.step.kind);
            Assert.AreEqual("paper_mill", m.step.building);
            CollectionAssert.AreEqual(new[] { "talisman" }, m.step.path);
            StringAssert.StartsWith("Talisman › Paper ← build a Paper Mill", m.step.text);

            // stored talismans cover it ⇒ withdraw hint
            var sh = sim.Buildings.PlaceBuilt("center", "storehouse", 60, 60, "talisman");
            sh.qty = sim.Config.Building("heavenly_gate").cost.Where(q => q.item == "talisman").Sum(q => q.qty);   // covers the Gate's talisman cost
            m = sim.Milestone();
            Assert.AreEqual(MilestoneStepKind.Withdraw, m.step.kind);

            // built gate ⇒ ascend to Immortality ⇒ free play
            var gate = sim.Buildings.PlaceBuilt("celestial", "heavenly_gate", 10, 60);
            Assert.IsNotNull(gate);
            m = sim.Milestone();
            Assert.AreEqual(MilestoneKind.Ascend, m.kind);
            Assert.IsTrue(sim.AscendToImmortality());
            Assert.AreEqual(MilestoneKind.FreePlay, sim.Milestone().kind);
        }
    }

    public class RegionUnlockTests
    {
        [Test]
        public void Installments_ThenOpen()
        {
            var sim = SimTestUtil.NewSim(out _);
            var S = sim.State;
            string opened = null; int sfx = 0;
            sim.Events.RegionUnlocked += k => opened = k;
            sim.Events.SoundRequested += (n, a) => { if (n == "unlock") sfx++; };
            Assert.IsNull(sim.AreaUnlockCost("center"));
            S.handCap = 500;
            var farmCfg = sim.Config.Region("farm");
            int farmCost = sim.Timing.Scaled(farmCfg.unlockCost.Single(c => c.item == "wood").qty);
            Assert.AreEqual(farmCost, sim.AreaUnlockCost("farm").Get("wood"));
            Assert.AreEqual(UnlockResultKind.Refused, sim.UnlockArea("farm").kind);
            Assert.IsFalse(sim.CanPayUnlock("farm"));
            sim.Hand.Add("wood", 3);
            Assert.AreEqual(UnlockPayState.Partial, sim.UnlockPayInfo("farm").state);
            var r = sim.UnlockArea("farm");
            Assert.AreEqual((UnlockResultKind.Paid, 3), (r.kind, r.paid));
            Assert.IsFalse(S.world.IsUnlocked("farm"));
            Assert.AreEqual(3, sim.UnlockPaid("farm").Get("wood"));
            Assert.AreEqual(farmCost - 3, sim.UnlockRemaining("farm").Get("wood"));
            sim.Hand.Add("wood", farmCost - 3 + 3);
            Assert.AreEqual(UnlockPayState.Afford, sim.UnlockPayInfo("farm").state);
            Assert.AreEqual(UnlockResultKind.Unlocked, sim.UnlockArea("farm").kind);
            Assert.IsTrue(S.world.IsUnlocked("farm"));
            Assert.AreEqual(3, sim.Hand.Count("wood"));
            Assert.IsFalse(S.world.unlockPaid.Any(e => e.region == "farm"));
            Assert.AreEqual(("farm", 1), (opened, sfx));
            Assert.AreEqual(UnlockResultKind.Refused, sim.UnlockArea("farm").kind);

            // multi-item grove: wood + algae
            var groveCost = sim.AreaUnlockCost("grove");
            sim.Hand.Add("wood", 1);
            Assert.AreEqual(UnlockResultKind.Paid, sim.UnlockArea("grove").kind);
            sim.Hand.Add("algae", groveCost.Get("algae"));
            sim.Hand.Add("wood", groveCost.Get("wood") - 1);
            Assert.AreEqual(UnlockResultKind.Unlocked, sim.UnlockArea("grove").kind);
        }
    }

    /// <summary>ADR 0007: the Heavenly Gate on Celestial Peak is the ending; free play continues after it.</summary>
    public class HeavenlyGateTests
    {
        const string G = "heavenly_gate", Cel = "celestial";

        [Test]
        public void Reveal_NeedsCelestialAndAwakenedDragon()
        {
            var sim = SimTestUtil.NewSim(out _, init: false);
            var S = sim.State;
            S.quest.idx = sim.Config.quests.Count;
            Assert.IsFalse(sim.IsBuildingUnlocked(G));
            S.dragon.stage = 4;
            Assert.IsFalse(sim.IsBuildingUnlocked(G), "awake dragon alone is not enough");
            Assert.AreEqual("Open Celestial Peak", sim.Buildings.UnlockReason(G));
            S.dragon.stage = 3;
            S.world.SetUnlocked(Cel, true);
            Assert.IsFalse(sim.IsBuildingUnlocked(G), "Celestial alone is not enough");
            S.dragon.stage = 4;
            Assert.IsTrue(sim.IsBuildingUnlocked(G));
        }

        [Test]
        public void Build_OnlyOnCelestial_RaisedEvent_Ascend_EndingThenFreePlay()
        {
            var sim = SimTestUtil.NewSim(out _, init: false);
            var S = sim.State;
            S.quest.idx = sim.Config.quests.Count;
            S.dragon.stage = 4; S.won = true;
            S.world.SetUnlocked(Cel, true);
            S.handCap = 500;
            Assert.AreEqual("Only on Celestial Peak", sim.PlaceReason("center", G, 30, 60));
            var ghost = sim.PlaceGhost(Cel, G, 10, 60);
            Assert.IsNotNull(ghost);
            Assert.IsFalse(sim.CanAscendToImmortality);
            Assert.IsFalse(sim.AscendToImmortality(), "no built gate yet");

            string raisedOn = null; int ascendedEv = 0, sfx = 0;
            sim.Events.HeavenlyGateRaised += (a, b) => raisedOn = a;
            sim.Events.AscendedToImmortality += () => ascendedEv++;
            sim.Events.SoundRequested += (n, a) => { if (n == "ascend") sfx++; };
            foreach (var c in sim.Config.Building(G).cost) sim.Hand.Add(c.item, c.qty);
            M5Util.ClickUntil(sim, Cel, ghost, () => ghost.built, 500);
            Assert.IsTrue(ghost.built);
            Assert.AreEqual(Cel, raisedOn);
            Assert.AreEqual((Cel, ghost), sim.BuiltHeavenlyGate());
            Assert.AreEqual(MilestoneKind.Ascend, sim.Milestone().kind);
            Assert.IsFalse(sim.EndingPending);

            int buildings = S.areas.Sum(a => a.buildings.Count);
            Assert.IsTrue(sim.AscendToImmortality());
            Assert.IsTrue(S.ascended);
            Assert.IsTrue(sim.EndingPending, "the ending card plays");
            Assert.AreEqual((1, 1), (ascendedEv, sfx));
            Assert.AreEqual(buildings, S.areas.Sum(a => a.buildings.Count), "nothing resets");
            Assert.AreEqual(4, S.dragon.stage);
            sim.MarkEndingSeen();
            Assert.IsFalse(sim.EndingPending);
            Assert.AreEqual(MilestoneKind.FreePlay, sim.Milestone().kind, "free play continues");

            // choosing it again replays the ending
            Assert.IsTrue(sim.AscendToImmortality());
            Assert.IsTrue(sim.EndingPending);

            // a built gate takes nothing: carried items fall through to the ground
            sim.State.hand.Clear();
            sim.Hand.Add("talisman", 1);
            Assert.AreEqual(DropResultKind.Dropped, M3Util.Click(sim, Cel, ghost, noGround: false).kind);
        }

        [Test]
        public void UnitySerializedEmptyStrings_MeanNoRestriction()
        {
            // Unity serializes null strings as "" (GameDatabase → BuildConfig): "" must behave like null
            var cfg = SimTestUtil.LoadConfig();
            foreach (var b in cfg.buildings) if (b.onlyArea == null) b.onlyArea = "";
            foreach (var r in cfg.reveal) foreach (var c in r.any) if (c.key == null) c.key = "";
            var sim = SimTestUtil.NewSim(out _, cfg: cfg);
            Assert.Greater(sim.State.Area("center").buildings.Count(b => b.starter), 10, "starter network placed");
            sim.State.quest.idx = cfg.quests.Count; sim.State.dragon.stage = 1;
            Assert.IsTrue(sim.IsBuildingUnlocked("forge"), "plain stage reveal");
            Assert.IsNull(sim.PlaceReason("center", "workbench", 30, 60));
        }

        [Test]
        public void Milestone_OpenCelestialBeforeTheGate()
        {
            var sim = SimTestUtil.NewSim(out _, init: false);
            var S = sim.State;
            S.dragon.stage = 4; S.quest.idx = sim.Config.quests.Count;
            var m = sim.Milestone();
            Assert.AreEqual(MilestoneKind.OpenIsland, m.kind);
            Assert.AreEqual(Cel, m.island);
            CollectionAssert.AreEquivalent(sim.AreaUnlockCost(Cel).entries.Select(e => e.item), m.needs.Select(n => n.item));
            Assert.IsEmpty(m.builds);
            S.world.SetUnlocked(Cel, true);
            Assert.AreEqual(MilestoneKind.RaiseGate, sim.Milestone().kind);
        }
    }

    public class PavilionTests
    {
        static (Simulation sim, ManualClock clock, Building pav) Setup(int disciples, int buns)
        {
            var sim = SimTestUtil.NewSim(out var clock, init: false);
            sim.NoGroundPhysics = true;
            var pav = sim.Buildings.PlaceBuilt("center", "meditation_pavilion", 55, 36);
            pav.disciples = disciples; pav.buns = buns;
            return (sim, clock, pav);
        }

        [TestCase(1000)]
        [TestCase(2500)]
        public void Cultivation_CatchUpEquivalence(int coarseStep)
        {
            int Run(int step, out int buns)
            {
                var (sim, clock, pav) = Setup(1, 20);
                for (int t = 0; t <= 10000; t += step) { sim.Tick(); clock.Advance(step); }
                buns = pav.buns;
                return SimTestUtil.CountGround(sim.State.Area("center"), "spirit_essence");
            }
            int fine = Run(50, out int bFine);
            int coarse = Run(coarseStep, out int bCoarse);
            Assert.AreEqual(9, fine);                 // cycle 1200 ms: t = 0, 1200 … 9600
            Assert.AreEqual(20 - 9, bFine);
            Assert.AreEqual(fine, coarse);
            Assert.AreEqual(bFine, bCoarse);
        }

        [Test]
        public void Cycle_EatsMinDisciplesBuns_StopsWhenHungry()
        {
            var (sim, clock, pav) = Setup(3, 4);
            var c = sim.State.Area("center");
            sim.Tick();
            Assert.AreEqual(3, SimTestUtil.CountGround(c, "spirit_essence"));
            Assert.AreEqual(1, pav.buns);
            clock.Advance(1200); sim.Tick();
            Assert.AreEqual(4, SimTestUtil.CountGround(c, "spirit_essence"));
            Assert.AreEqual(0, pav.buns);
            clock.Advance(1200); sim.Tick();
            Assert.AreEqual(4, SimTestUtil.CountGround(c, "spirit_essence"));
            Assert.IsTrue(c.ground.Where(g => g.item == "spirit_essence").All(g => g.crafted));
            Assert.AreEqual(BuildingState.Starved, sim.BuildingStatus("center", pav).state);
        }

        [Test]
        public void Recruit_Robe_Cap_MasteryAndHall_Food()
        {
            var (sim, _, pav) = Setup(0, 0);
            var S = sim.State;
            Assert.AreEqual("Needs Robe", sim.RecruitReason("center", pav.id));
            Assert.IsFalse(sim.RecruitDisciple("center", pav.id));
            sim.Hand.Add("robe", 8);
            for (int i = 0; i < 3; i++) Assert.IsTrue(sim.RecruitDisciple("center", pav.id));
            Assert.AreEqual("Full", sim.RecruitReason("center", pav.id));
            Assert.IsFalse(sim.RecruitDisciple("center", pav.id));
            S.Area("center").upgrades.discipleCap = 1;
            S.bonuses.Set("hall", 1);
            Assert.AreEqual(6, sim.RosterCap(pav));
            for (int i = 0; i < 3; i++) Assert.IsTrue(sim.RecruitDisciple("center", pav.id));
            Assert.AreEqual(6, pav.disciples);
            Assert.AreEqual(6, S.stats.disciplesRecruited);
            Assert.AreEqual(2, sim.Hand.Count("robe"));
            // food: wine has a configured value, capped at the pavilion's foodCap (never overfed)
            var pavDef = sim.Config.Building("meditation_pavilion");
            int wine = pavDef.roster.foodValues.Single(f => f.item == "spirit_wine").qty, foodCap = pavDef.roster.foodCap;
            int wines = foodCap / wine;
            Assert.Less(wines, 8, "test needs a spare wine");
            S.hand.Clear();
            sim.Hand.Add("spirit_wine", 8);
            M5Util.ClickUntil(sim, "center", pav, () => false, 20);
            Assert.AreEqual(wines * wine, pav.buns);
            Assert.AreEqual(8 - wines, sim.Hand.Count("spirit_wine"));
            // demolish refunds disciples as robes
            Assert.IsTrue(sim.Demolish("center", pav.id));
            Assert.AreEqual(6, SimTestUtil.CountGround(S.Area("center"), "robe"));
        }
    }
}
