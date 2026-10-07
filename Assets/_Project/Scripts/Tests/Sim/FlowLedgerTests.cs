using System.Linq;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    public class FlowLedgerTests
    {
        const string V = "volcano";

        static (long p, long c, long l) F(Simulation sim, string item)
        {
            var e = sim.State.flow.Entry(item);
            return e == null ? (0, 0, 0) : (e.produced, e.consumed, e.lost);
        }

        static long TotalProduced(Simulation sim) => sim.State.flow.items.Sum(e => e.produced);
        static long TotalAll(Simulation sim) => sim.State.flow.items.Sum(e => e.produced + e.consumed + e.lost);

        [Test]
        public void Harvest_CountsProduced_MatchingGatheredStat()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            sim.State.flow = new FlowLedger();          // ignore the starter seeds
            long before = sim.State.stats.totalGathered;
            var c = sim.State.Area("center");
            foreach (var bush in c.nodes.Where(n => n.spawnerKind == "bush").Take(6).ToList())
                for (int k = 0; k < 8; k++) sim.Harvest("center", bush.id);   // no Tick: generators would add produce of their own
            long gathered = sim.State.stats.totalGathered - before;
            Assert.Greater(gathered, 0);
            Assert.AreEqual(gathered, TotalProduced(sim));
            Assert.AreEqual(0, sim.State.flow.items.Sum(e => e.consumed + e.lost));
            foreach (var e in sim.State.flow.items) Assert.AreEqual(e.produced, e.runProduced);
        }

        [Test]
        public void ConverterBatch_ConsumesInputsAtStart_ProducesOutputAtEnd()
        {
            var sim = M3Util.Volcano(out var clock);
            var b = sim.Buildings.PlaceBuilt(V, "workbench", 5, 30, starter: false);
            b.stock = new ItemCounts(); b.stock.Set("wood", 6);
            sim.Tick();
            Assert.AreEqual(3, F(sim, "wood").c);   // (the volcano field generators may add produced wood)
            Assert.AreEqual((0L, 0L, 0L), F(sim, "plank"));
            clock.Advance(800); sim.Tick();
            Assert.AreEqual((1L, 0L, 0L), F(sim, "plank"));
            Assert.AreEqual(6, F(sim, "wood").c, "second batch started");
        }

        [Test]
        public void SetRecipe_CancelledBatch_UnconsumesInputs()
        {
            var sim = M3Util.Volcano(out _);
            var b = sim.Buildings.PlaceBuilt(V, "workbench", 5, 30, starter: false);
            b.stock = new ItemCounts(); b.stock.Set("wood", 3);
            sim.Tick();
            Assert.AreEqual(3, F(sim, "wood").c);
            Assert.IsTrue(sim.SetRecipe(V, b.id, 1));
            Assert.AreEqual(0, F(sim, "wood").c);
        }

        [Test]
        public void FuelBurn_ConsumesTheFuelUnit()
        {
            var sim = M3Util.Volcano(out _);
            var k = sim.Buildings.PlaceBuilt(V, "kiln", 5, 40, starter: false);
            Assert.IsTrue(sim.Fuel.AddItem(k, "wood"));
            Assert.AreEqual((0L, 0L, 0L), F(sim, "wood"), "loading the rack is a transfer");
            sim.Fuel.Burn(k, 1.0);
            Assert.AreEqual(0, F(sim, "wood").c, "partly burnt: not gone yet");
            sim.Fuel.Burn(k, 1e9);
            Assert.AreEqual((0L, 1L, 0L), F(sim, "wood"));
        }

        [Test]
        public void BuildingCost_Consumed_DemolishUnconsumes()
        {
            var sim = M3Util.Volcano(out _);
            sim.State.handCap = 500;
            var g = sim.PlaceGhost(V, "workbench", 5, 50);
            var cost = sim.BuildingNeeds(g).Clone();
            Assert.Greater(cost.Count, 0);
            foreach (var e in cost) sim.Hand.Add(e.item, e.qty);
            for (int i = 0; i < 200 && sim.BuildingNeeds(g).Count > 0; i++) M3Util.Click(sim, V, g);
            Assert.IsTrue(g.built);
            foreach (var e in cost) Assert.AreEqual(e.qty, F(sim, e.item).c, e.item);
            Assert.IsTrue(sim.Demolish(V, g.id));
            foreach (var e in cost) Assert.AreEqual(0, F(sim, e.item).c, e.item + " refunded");
        }

        [Test]
        public void DragonTribute_CountsConsumed()
        {
            var sim = SimTestUtil.NewSim(out _);
            sim.State.handCap = 500;
            sim.State.flow = new FlowLedger();
            var dragon = M5Util.Dragon(sim);
            var need = sim.DragonRemaining().Clone();
            M5Util.Give(sim, need);
            M5Util.ClickUntil(sim, "center", dragon, () => sim.State.dragon.stage != 0);
            Assert.AreEqual(1, sim.State.dragon.stage);
            foreach (var e in need) Assert.AreEqual(e.qty, F(sim, e.item).c, e.item);
        }

        [Test]
        public void Eviction_CountsLost()
        {
            var cfg = SimTestUtil.LoadConfig();
            cfg.balance.groundCap = 20; cfg.balance.groundHardCap = 40;
            var sim = SimTestUtil.NewSim(out _, cfg: cfg);
            sim.State.flow = new FlowLedger();
            var a = sim.State.Area("center");
            int before = a.ground.Count;
            sim.Ground.DropGround("center", "stone", 100, 500, 500);
            int evicted = before + 100 - a.ground.Count;
            Assert.Greater(evicted, 0);
            Assert.AreEqual(evicted, sim.State.flow.items.Sum(e => e.lost));
            Assert.AreEqual(0, TotalProduced(sim), "dropping existing items on the ground is not production");
        }

        [Test]
        public void Transfers_DoNotCount()
        {
            var sim = M3Util.Volcano(out _);
            sim.State.handCap = 100;
            var a = sim.State.Area(V);
            var st = sim.Buildings.PlaceBuilt(V, "storehouse", 5, 30, starter: false);
            sim.Hand.Add("wood", 10);
            M3Util.Click(sim, V, st);                             // hand -> storehouse
            sim.Withdraw(V, st.id, 3);                            // storehouse -> hand
            var (x, y) = M3Util.Px(sim, st);
            sim.DropFromHand(V, x + 200, y + 200, false);         // hand -> ground
            sim.Suction(V, x + 200, y + 200, 300);                // ground -> hand
            var gs = sim.Buildings.PlaceBuilt(V, "gathering_stone", 5, 60, starter: false);
            M3Util.Click(sim, V, gs);                             // hand -> gathering stone
            sim.Withdraw(V, gs.id, 1);
            Assert.AreEqual(0, TotalAll(sim));
        }

        [Test]
        public void StarterNetwork_30sOfTicks_CountsProductionAndConsumption()
        {
            var sim = SimTestUtil.NewSim(out var clock);          // real starter network (TEST scaling as in the game data)
            sim.State.flow = new FlowLedger();
            for (int i = 0; i < 300; i++) { clock.Advance(100); sim.Tick(); }   // 30 s
            var f = sim.State.flow;
            string dump = string.Join(", ", f.items.Select(e => e.item + " P" + e.produced + "/C" + e.consumed + "/L" + e.lost));
            foreach (var it in new[] { "brick", "plank", "spirit_stone" }) Assert.Greater(F(sim, it).p, 0, it + " produced; " + dump);
            foreach (var it in new[] { "clay", "wood", "stone", "spirit_essence" }) Assert.Greater(F(sim, it).c, 0, it + " consumed; " + dump);
            foreach (var it in new[] { "clay", "wood", "stone" }) Assert.Greater(F(sim, it).p, 0, it + " generated; " + dump);
        }

        [Test]
        public void SaveRoundTrip_KeepsCounts_AndSanitises()
        {
            var sim = SimTestUtil.NewSim(out _);
            var f = sim.State.flow = new FlowLedger();
            f.Produce("wood", 7); f.Consume("wood", 3); f.Lose("stone", 2);
            string json = sim.SaveJson();
            var back = Simulation.LoadJson(json, sim.Config, 1_000_000, out var why);
            Assert.IsNotNull(back, why);
            var e = back.flow.Entry("wood");
            Assert.AreEqual((7L, 3L, 0L, 7L, 3L), (e.produced, e.consumed, e.lost, e.runProduced, e.runConsumed));
            Assert.AreEqual(2, back.flow.Entry("stone").lost);
            // junk is scrubbed
            f.items.Add(new FlowEntry { item = "not_an_item", produced = 5 });
            f.items.Add(new FlowEntry { item = "wood", produced = 99 });                       // duplicate
            f.items.Add(new FlowEntry { item = "clay", produced = -4, runProduced = 50 });     // negative / run > lifetime
            var again = Simulation.LoadJson(sim.SaveJson(), sim.Config, 1_000_000, out why);
            Assert.IsNotNull(again, why);
            Assert.IsNull(again.flow.Entry("not_an_item"));
            Assert.AreEqual(7, again.flow.Entry("wood").produced);
            Assert.AreEqual((0L, 0L), (again.flow.Entry("clay").produced, again.flow.Entry("clay").runProduced));
        }

        [Test]
        public void Ascend_KeepsLifetime_ResetsRun()
        {
            var sim = SimTestUtil.NewSim(out _);
            sim.State.flow = new FlowLedger();
            sim.State.flow.Produce("wood", 10);
            sim.State.flow.Consume("wood", 4);
            sim.Ascend();
            var e = sim.State.flow.Entry("wood");
            Assert.AreEqual(4L, e.consumed, "lifetime survives");
            Assert.GreaterOrEqual(e.produced, 10L);
            Assert.AreEqual(0L, e.runConsumed, "run section reset");
            Assert.AreEqual(e.produced - 10L, e.runProduced, "only the new run's starter seeds remain");
        }

        [Test]
        public void RollingRate_WindowsAndExpires()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var f = sim.State.flow = new FlowLedger();
            f.Advance(clock.NowMs);
            f.Produce("wood", 60);
            clock.Advance(60_000); f.Advance(clock.NowMs);
            f.Consume("wood", 30);
            var e = f.Entry("wood");
            Assert.Greater(f.ProducedPerMin(e), 0);
            Assert.AreEqual(f.ProducedPerMin(e) - f.ConsumedPerMin(e), f.NetPerMin(e), 1e-9);
            clock.Advance(6 * 60_000); f.Advance(clock.NowMs);
            Assert.AreEqual(0, f.NetPerMin(e), 1e-9, "old buckets expired");
            Assert.AreEqual(60, e.produced, "lifetime untouched");
        }
    }
}
