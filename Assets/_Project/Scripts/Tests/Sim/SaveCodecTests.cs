using System;
using System.Linq;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>M7: save format, sanitising load (§1.4-1.5).</summary>
    public class SaveCodecTests
    {
        /// <summary>A state after a few minutes of play: starter network running, wisps in flight,
        /// converters mid-batch, plus prestige / quest / job / buff fields set.</summary>
        internal static Simulation PlayedSim(out ManualClock clock, int maxTicks = 20000)
        {
            var sim = SimTestUtil.NewSim(out clock, seed: 99);
            var S = sim.State;
            var cfg = sim.Config;
            for (int i = 0; i < maxTicks; i++)
            {
                clock.Advance(50);
                sim.Tick();
                if (i % 20 == 19) sim.AutomationTick();
                if (i > 2400 && AnyWisp(S) && AnySmelting(S)) break;
            }
            S.perks.Set("haste", 2); S.perks.Set("hall", 1);
            S.ascendPoints = 7; S.ascensions = 1;
            S.vows.done.Set("burden", 1);
            S.quest.idx = 3;
            S.hand.Add(new HandStack("wood", 4));
            S.upgradeJob = new UpgradeJob { area = "center", type = "harvestSpeed" };
            S.upgradeJob.needs.Set("wood", 5); S.upgradeJob.paid.Set("wood", 2);
            S.buff = new BuffState { kind = cfg.dragonBuffs[0].item, until = clock.NowMs + 30000 };
            S.combatBuff = new CombatBuffState { until = clock.NowMs + 5000 };
            S.world.unlockPaid.Add(new RegionPaid { region = "farm" });
            S.world.unlockPaid[0].paid.Set(cfg.Region("farm").unlockCost[0].item, 1);
            S.justAscended = new JustAscended { n = 1, ap = 3, speedFrom = 1, speedTo = 1.2 };
            return sim;
        }

        static bool AnyWisp(GameState s) => s.areas.Any(a => a.wisps.Count > 0);
        static bool AnySmelting(GameState s) => s.areas.Any(a => a.buildings.Any(b => b.smeltDoneAt > 0));

        [Test]
        public void RoundTrip_PlayedState_IsExact_AndDropsTransients()
        {
            var sim = PlayedSim(out var clock);
            var S = sim.State;
            Assert.IsTrue(AnyWisp(S), "wisps in flight");
            Assert.IsTrue(AnySmelting(S), "a converter mid-batch");
            Assert.IsTrue(S.areas.Any(a => a.buildings.Any(b => b.fuelQ != null && b.fuelQ.Count > 0)), "fuel racked");
            Assert.IsTrue(S.areas.Any(a => a.buildings.Any(b => b.links != null && b.links.Count > 0)), "links");
            Assert.Greater(S.Area("center").ground.Count, 0);

            double stamp = clock.NowMs;
            string j1 = SaveCodec.Serialize(S, stamp);
            Assert.IsTrue(SaveCodec.TryDeserialize(j1, sim.Config, clock.NowMs, out var s2, out var reason), reason);
            string j2 = SaveCodec.Serialize(s2, stamp);
            Assert.AreEqual(j1, j2, "save → load → save is exact");

            StringAssert.Contains("\"schemaVersion\":" + GameState.SchemaVersion, j1);
            foreach (var t in new[] { "manualAt", "autoPaused", "autoSkip", "accEver", "pileFull", "\"crafts\"", "\"stat\"" })
                StringAssert.DoesNotContain(t, j1, t + " is transient");
            Assert.AreEqual(stamp, s2.lastSeen);
            Assert.AreEqual(S.Area("center").wisps.Count, s2.Area("center").wisps.Count);
            Assert.AreEqual(2, s2.PerkLevel("haste"));
            Assert.AreEqual(5, s2.upgradeJob.needs.Get("wood"));
            Assert.IsNotNull(s2.buff);
            Assert.IsTrue(double.IsNaN(s2.offlineAwayFrom));
        }

        [Test]
        public void RoundTrip_LoadedStateKeepsTicking()
        {
            var sim = PlayedSim(out var clock, 4000);
            string j = sim.SaveJson();
            var s2 = Simulation.LoadJson(j, sim.Config, clock.NowMs, out var reason);
            Assert.IsNotNull(s2, reason);
            var sim2 = new Simulation(sim.Config, s2, clock, new XorShiftRng(5));
            var boot = sim2.Boot();
            Assert.AreEqual(OfflineTier.None, boot.tier);
            for (int i = 0; i < 400; i++) { clock.Advance(50); sim2.Tick(); }
            Assert.Pass();
        }

        [Test]
        public void Build_IsNeverPersisted()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            sim.State.build.open = true; sim.State.build.placing = "kiln";
            var s2 = SaveCodec.Deserialize(SaveCodec.Serialize(sim.State), sim.Config, clock.NowMs);
            Assert.IsFalse(s2.build.open);
            Assert.IsNull(s2.build.placing);
            Assert.IsTrue(sim.State.build.open, "serialize does not touch the live state");
        }

        [Test]
        public void Garbage_ReturnsNull_NeverThrows()
        {
            var cfg = SimTestUtil.LoadConfig();
            var sim = SimTestUtil.NewSim(out var clock, cfg: cfg);
            string good = SaveCodec.Serialize(sim.State);
            var bad = new[]
            {
                null, "", "   ", "{", "}", "[]", "null", "42", "\"x\"", "{\"a\":1}", "{\"areas\":{},\"world\":{}}",
                "{\"areas\":[],\"world\":5}", "{\"areas\":[1,2],", new string('[', 5000) + new string(']', 5000),
                "{\"schemaVersion\":999,\"areas\":[],\"world\":{}}", good.Substring(0, good.Length / 2), "﻿{,}",
            };
            foreach (var b in bad)
            {
                GameState s = null; string reason = null;
                Assert.DoesNotThrow(() => SaveCodec.TryDeserialize(b, cfg, clock.NowMs, out s, out reason), b);
                Assert.IsNull(s, "should refuse: " + (b == null ? "null" : b.Substring(0, Math.Min(40, b.Length))));
                Assert.IsNotNull(reason);
            }
            // fuzz: random byte flips / deletions of a valid save never throw
            var rng = new Random(1);
            for (int i = 0; i < 300; i++)
            {
                var chars = good.ToCharArray();
                int n = 1 + rng.Next(6);
                for (int k = 0; k < n; k++) chars[rng.Next(chars.Length)] = "{}[],:\"0a-.n"[rng.Next(12)];
                string m = new string(chars);
                Assert.DoesNotThrow(() => SaveCodec.Deserialize(m, cfg, clock.NowMs));
            }
        }

        [Test]
        public void PartialSave_MergesOntoDefaults()
        {
            var cfg = SimTestUtil.LoadConfig();
            var s = SaveCodec.Deserialize("{\"areas\":[],\"world\":{},\"ascensions\":2,\"stats\":{\"foxKills\":4}}", cfg, 5000);
            Assert.IsNotNull(s);
            Assert.AreEqual(cfg.regions.Count, s.areas.Count, "every area present, region order");
            Assert.AreEqual(cfg.regions[0].key, s.areas[0].key);
            Assert.AreEqual(2, s.ascensions);
            Assert.AreEqual(4, s.stats.foxKills);
            Assert.AreEqual(cfg.balance.handCap, s.handCap);
            Assert.IsTrue(s.world.IsUnlocked("center"));
            Assert.IsFalse(s.world.IsUnlocked("farm"));
            Assert.IsTrue(double.IsNaN(s.lastSeen), "no lastSeen ⇒ null ⇒ no offline credit");
            Assert.AreEqual(cfg.balance.questChain, s.quest.chain);
        }

        [Test]
        public void Sanitize_ScrubsUnknownIds_AndClamps_Idempotently()
        {
            var sim = PlayedSim(out var clock, 3000);
            var S = sim.State;
            var cfg = sim.Config;
            var c = S.Area("center");
            S.hand.Add(new HandStack("bogus_item", 3));
            S.hand.Add(new HandStack("stone", 0));
            S.perks.Set("bogus_perk", 2); S.perks.Set("haste", 99);
            S.vows.active.AddRange(new[] { "burden", "nope", "burden" });
            S.vows.done.Set("nope", 3); S.vows.done.Set("restless", 0);
            S.quest.idx = 999;
            S.builtTypes.Add("bogus_building");
            S.buildSeen.Set("kiln", 7); S.buildSeen.Set("bogus_building", 1);
            S.buff = new BuffState { kind = "not_a_pill", until = 5 };
            S.dragon.paid.Set("bogus_item", 3);
            S.world.unlockPaid.Add(new RegionPaid { region = "center" });     // already open ⇒ dropped
            S.world.unlockPaid[S.world.unlockPaid.Count - 1].paid.Set("wood", 2);
            S.world.unlockPaid.Add(new RegionPaid { region = "atlantis" });
            S.world.unlockPaid[S.world.unlockPaid.Count - 1].paid.Set("wood", 2);
            S.areas.Add(new AreaState("atlantis"));
            c.ground.Add(new GroundItem { id = 99999, item = "bogus_item", x = 5, y = 5 });
            c.ground.Add(new GroundItem { id = 99998, item = "wood", x = 5, y = 5, crafted = true, gen = true });
            c.buildings.Add(new Building { id = 88888, type = "bogus_building", row = 1, col = 1, built = true });
            var lantern = c.buildings.First(b => b.links != null && b.links.Count > 0);
            lantern.links.Add(new Link { from = 77777, to = lantern.links[0].to });
            var burner = c.buildings.First(b => cfg.Building(b.type).fuel);
            burner.fuelQ ??= new System.Collections.Generic.List<FuelSlot>();
            burner.fuelQ.Insert(0, new FuelSlot { item = "stone", rem = 100, total = 100 });
            burner.recipe = 99;
            var conv = c.buildings.First(b => cfg.Building(b.type).IsConverter);
            conv.stock ??= new ItemCounts();
            conv.stock.Set("bogus_item", 4);
            var gate = sim.Buildings.PlaceBuilt("center", "ascension_gate", 40, 70);
            gate.offered = new ItemCounts();
            gate.offered.Set("talisman", 5); gate.offered.Set("bogus_item", 3); gate.offered.Set("star_steel", 1);
            gate.offerings = 9;
            S.offlineAwayFrom = clock.NowMs + 10;   // after lastSeen ⇒ dropped

            string raw = SaveCodec.Serialize(S, clock.NowMs);
            var s = SaveCodec.Deserialize(raw, cfg, clock.NowMs);
            Assert.IsNotNull(s);
            var sc = s.Area("center");
            Assert.IsFalse(s.hand.Any(h => h.item == "bogus_item" || h.qty <= 0));
            Assert.AreEqual(0, s.PerkLevel("bogus_perk"));
            Assert.AreEqual(cfg.Perk("haste").max, s.PerkLevel("haste"));
            CollectionAssert.AreEqual(new[] { "burden" }, s.vows.active);
            Assert.IsFalse(s.vows.done.Has("nope"));
            Assert.IsFalse(s.vows.done.Has("restless"));
            Assert.AreEqual(cfg.quests.Count, s.quest.idx);
            Assert.IsFalse(s.builtTypes.Contains("bogus_building"));
            Assert.AreEqual(2, s.buildSeen.Get("kiln"));
            Assert.IsFalse(s.buildSeen.Has("bogus_building"));
            Assert.IsNull(s.buff);
            Assert.IsFalse(s.dragon.paid.Has("bogus_item"));
            Assert.IsFalse(s.world.unlockPaid.Any(e => e.region == "center" || e.region == "atlantis"));
            Assert.IsTrue(s.world.unlockPaid.Any(e => e.region == "farm"));
            Assert.IsNull(s.Area("atlantis"));
            Assert.AreEqual(cfg.regions.Count, s.areas.Count);
            Assert.IsFalse(sc.ground.Any(g => g.item == "bogus_item"));
            var cg = sc.ground.First(g => g.id == 99998);
            Assert.IsTrue(cg.crafted); Assert.IsFalse(cg.gen, "crafted ⇒ never gen");
            Assert.IsFalse(sc.buildings.Any(b => b.type == "bogus_building"));
            Assert.IsFalse(sc.buildings.First(b => b.id == lantern.id).links.Any(l => l.from == 77777));
            var sb = sc.buildings.First(b => b.id == burner.id);
            Assert.IsFalse(sb.fuelQ.Any(f => f.item == "stone"));
            Assert.IsTrue(sb.fuelQ.All(f => f.total == cfg.FuelMs(f.item)));
            Assert.AreEqual(0, sb.recipe);
            Assert.IsFalse(sc.buildings.First(b => b.id == conv.id).stock.Has("bogus_item"));
            var sg = sc.buildings.First(b => b.id == gate.id);
            Assert.AreEqual(2, sg.offered.Get("talisman"));
            Assert.AreEqual(1, sg.offered.Get("star_steel"));
            Assert.IsFalse(sg.offered.Has("bogus_item"));
            Assert.AreEqual(3, sg.offerings);
            Assert.IsTrue(double.IsNaN(s.offlineAwayFrom));
            Assert.IsTrue(sc.nextBuildId > sc.buildings.Max(b => b.id));

            // idempotence: load(save(load(x))) == load(x)
            string j1 = SaveCodec.Serialize(s);
            string j2 = SaveCodec.Serialize(SaveCodec.Deserialize(j1, cfg, clock.NowMs));
            Assert.AreEqual(j1, j2);
        }

        [Test]
        public void Sanitize_CountOnlyGate_AttributedFromLastItem_AndRegridClearsNodes()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var gate = sim.Buildings.PlaceBuilt("center", "ascension_gate", 40, 70);
            gate.offered = null; gate.offerings = 3;
            sim.State.gridCells = 50;                     // saved under another grid size
            var s = SaveCodec.Deserialize(SaveCodec.Serialize(sim.State), sim.Config, clock.NowMs);
            var g = s.Area("center").BuildingById(gate.id);
            var items = sim.Config.gateOfferings.items;
            Assert.AreEqual(2, g.offered.Get(items[items.Count - 1]));
            Assert.AreEqual(1, g.offered.Get(items[items.Count - 2]));
            Assert.AreEqual(3, g.offerings);
            Assert.AreEqual(0, s.Area("center").nodes.Count, "regrid ⇒ nodes re-rolled by initArea");
            Assert.Greater(s.Area("center").buildings.Count, 2, "buildings kept");
            Assert.AreEqual(sim.Config.grid.cells, s.gridCells);
        }

        [Test]
        public void NonFiniteAndNullFields_AreDefaulted()
        {
            var sim = PlayedSim(out var clock, 3000);
            var c = sim.State.Area("center");
            var w = sim.State.areas.SelectMany(a => a.wisps).First();
            w.t0 = double.NaN; w.sp = 10;
            c.enemies.Add(new Enemy { id = 5000, x = double.NaN, y = 1, hp = 3, maxHp = 3 });
            c.ground.Add(new GroundItem { id = 5001, item = "wood", x = double.PositiveInfinity, y = 1 });
            sim.State.lastSeen = double.NaN;
            sim.State.dragonScaleAt = double.NaN;
            var s = SaveCodec.Deserialize(SaveCodec.Serialize(sim.State), sim.Config, 123456);
            var w2 = s.areas.SelectMany(a => a.wisps).First(x => x.id == w.id);
            Assert.AreEqual(123456, w2.t0);
            Assert.AreEqual(w2.x, w2.x0);
            Assert.AreEqual(170, w2.sp);
            Assert.IsFalse(s.Area("center").enemies.Any(e => e.id == 5000));
            Assert.IsFalse(s.Area("center").ground.Any(g => g.id == 5001));
            Assert.IsTrue(double.IsNaN(s.lastSeen));
            Assert.AreEqual(0, s.dragonScaleAt);
        }
    }
}
