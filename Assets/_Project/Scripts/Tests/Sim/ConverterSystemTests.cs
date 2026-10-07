using System.Linq;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    public class ConverterSystemTests
    {
        const string V = "volcano";

        static Building Bench(Simulation sim, int wood)
        {
            var b = sim.Buildings.PlaceBuilt(V, "workbench", 5, 30, starter: false);
            b.stock = new ItemCounts(); if (wood > 0) b.stock.Set("wood", wood);
            return b;
        }

        [Test]
        public void Batch_TimingAndEvents()
        {
            var sim = M3Util.Volcano(out var clock);
            var a = sim.State.Area(V);
            var b = Bench(sim, 6);
            int started = 0, finished = 0, craftSfx = 0;
            sim.Events.BatchStarted += (ar, bb, it, q) => started++;
            sim.Events.BatchFinished += (ar, bb, it, q) => { finished++; Assert.AreEqual(("plank", 1), (it, q)); };
            sim.Events.SoundRequested += (n, ar) => { if (n == "craft") craftSfx++; };
            sim.Tick();
            double t0 = clock.NowMs;
            Assert.AreEqual(t0 + 800, b.smeltDoneAt, 1e-9, "4000 ms × timeScale 0.2");
            Assert.AreEqual(3, b.stock.Get("wood"));
            Assert.AreEqual(1, started);
            clock.Advance(799); sim.Tick();
            Assert.AreEqual(0, SimTestUtil.CountGround(a, "plank"));
            clock.Advance(1); sim.Tick();
            Assert.AreEqual(1, SimTestUtil.CountGround(a, "plank"));
            var p = a.ground.First(g => g.item == "plank");
            Assert.IsTrue(p.crafted);
            Assert.AreEqual(t0 + 1600, b.smeltDoneAt, 1e-9, "next batch re-armed from the due time");
            Assert.AreEqual(0, b.stock.Get("wood"));
            Assert.AreEqual((2, 1, 1), (started, finished, craftSfx));
            Assert.AreEqual(1, sim.State.stats.totalCrafted);
            var face = sim.ConverterFace(V, b);
            Assert.AreEqual(0.0, face.progress, 1e-9);
            clock.Advance(400);
            Assert.AreEqual(0.5, sim.ConverterFace(V, b).progress, 1e-9);
        }

        static (int planks, int wood, long crafted) Run(int stepMs, int totalMs)
        {
            var sim = M3Util.Volcano(out var clock);
            var b = Bench(sim, 18);
            sim.Tick();
            for (int t = 0; t < totalMs; t += stepMs) { clock.Advance(stepMs); sim.Tick(); }
            return (SimTestUtil.CountGround(sim.State.Area(V), "plank"), b.stock.Get("wood"), sim.State.stats.totalCrafted);
        }

        [Test]
        public void Batch_CatchUp_FineVsCoarseTicks()
        {
            var fine = Run(50, 6000);
            Assert.AreEqual((6, 0, 6L), fine);
            Assert.AreEqual(fine, Run(1000, 6000));
            Assert.AreEqual(fine, Run(640, 6400));
            Assert.AreEqual(fine, Run(6000, 6000), "one 6 s tick (gap < 10 s) catches every batch up");
        }

        [Test]
        public void StockCap_RefusesPastCap()
        {
            var sim = M3Util.Volcano(out _);
            var b = Bench(sim, 0);
            sim.State.handCap = 40;
            sim.Hand.Add("wood", 20);
            sim.Hand.Add("stone", 5);
            for (int i = 0; i < 20; i++) Assert.AreEqual(DropResultKind.Fed, M3Util.Click(sim, V, b, noGround: false).kind);
            Assert.AreEqual(20, b.stock.Get("wood"));
            Assert.IsNull(M3Util.Click(sim, V, b, noGround: false), "stone is no input: refused, never dropped");
            Assert.AreEqual(5, sim.Hand.Count("stone"));
            Assert.AreEqual(0, sim.State.Area(V).ground.Count);
            Assert.IsFalse(sim.Buildings.EndpointAccepts(b, "wood"));
            Assert.AreEqual(BuildingState.Working, sim.BuildingStatus(V, b).state);
        }

        [Test]
        public void FeedRatio_BalancesInputs()
        {
            var sim = M3Util.Volcano(out _);
            var c = sim.Buildings.PlaceBuilt(V, "cauldron", 5, 30, starter: false);   // Qi Elixir: herb1 water2 ess1
            sim.Hand.Add("water", 6); sim.Hand.Add("spirit_herb", 6); sim.Hand.Add("spirit_essence", 6);
            for (int i = 0; i < 4; i++) M3Util.Click(sim, V, c);
            Assert.AreEqual((1, 2, 1), (c.stock.Get("spirit_herb"), c.stock.Get("water"), c.stock.Get("spirit_essence")));
        }

        [Test]
        public void SetRecipe_RefundsAndKeepsShared()
        {
            var sim = M3Util.Volcano(out var clock);
            var a = sim.State.Area(V);
            var b = Bench(sim, 8);
            sim.Tick();                                   // batch running: stock 5
            Assert.Greater(b.smeltDoneAt, 0);
            Assert.IsTrue(sim.SetRecipe(V, b.id, 0), "same index = no-op");
            Assert.AreEqual(0, sim.State.stats.recipeSwitches);
            sim.Hand.Add("stone", 15);                    // only 5 hand space left
            Assert.IsTrue(sim.SetRecipe(V, b.id, 1));
            Assert.AreEqual(0, b.smeltDoneAt);
            Assert.AreEqual(1, b.recipe);
            Assert.AreEqual(5, sim.Hand.Count("wood"), "stock 5 + cancelled batch 3 → hand to capacity");
            Assert.AreEqual(3, SimTestUtil.CountGround(a, "wood"), "overflow dropped");
            Assert.AreEqual(0, b.stock.Count);
            Assert.AreEqual(1, sim.State.stats.recipeSwitches);
            Assert.IsFalse(sim.SetRecipe(V, b.id, 5));

            // shared inputs stay in stock
            var c = sim.Buildings.PlaceBuilt(V, "cauldron", 5, 40, starter: false);
            c.stock = new ItemCounts();
            c.stock.Set("spirit_herb", 4); c.stock.Set("water", 6); c.stock.Set("spirit_essence", 2);
            sim.Hand.Take("stone", 15); sim.Hand.Take("wood", 5);
            Assert.IsTrue(sim.SetRecipe(V, c.id, 1));    // Vitality: fish herb water
            Assert.AreEqual((4, 6, 0), (c.stock.Get("spirit_herb"), c.stock.Get("water"), c.stock.Get("spirit_essence")));
            Assert.AreEqual(2, sim.Hand.Count("spirit_essence"));
        }

        [Test]
        public void Fuel_FifoRack()
        {
            var sim = M3Util.Volcano(out _);
            var k = sim.Buildings.PlaceBuilt(V, "kiln", 5, 40, starter: false);
            Assert.IsTrue(sim.Fuel.AddItem(k, "wood"));
            Assert.IsTrue(sim.Fuel.AddItem(k, "charcoal"));
            Assert.IsFalse(sim.Fuel.AddItem(k, "stone"));
            Assert.AreEqual("charcoal", sim.Fuel.Queue(k)[0].item, "newest at the front");
            double W = sim.Config.FuelMs("wood"), C = sim.Config.FuelMs("charcoal");
            sim.Fuel.Burn(k, W / 2);
            Assert.AreEqual(W / 2, sim.Fuel.Queue(k)[1].rem, 1e-9, "oldest burns first");
            sim.Fuel.Burn(k, W / 2 + 1000);
            Assert.AreEqual(1, sim.Fuel.Queue(k).Count);
            Assert.AreEqual(C - 1000, sim.Fuel.Total(k), 1e-9);
            for (int i = 0; i < 5; i++) sim.Fuel.AddItem(k, "wood");
            Assert.AreEqual(0, sim.Fuel.Space(k));
            Assert.IsFalse(sim.Fuel.AddItem(k, "wood"));
            sim.State.vows.active.Add("coldhearth");
            sim.Fuel.Burn(k, 2500);
            Assert.AreEqual(C - 1000 - 5000, sim.Fuel.Queue(k)[5].rem, 1e-9, "Cold Hearth burns double — from the back (the old charcoal)");
        }

        [Test]
        public void Burner_StartsOnSliver_FinishesWithoutFuel_ThenNoFuel()
        {
            var sim = M3Util.Volcano(out var clock);
            var a = sim.State.Area(V);
            var k = sim.Buildings.PlaceBuilt(V, "kiln", 5, 40, starter: false);   // Brick: clay 2, 1000 ms
            k.stock = new ItemCounts(); k.stock.Set("clay", 4);
            sim.Tick();
            Assert.AreEqual(0, k.smeltDoneAt, "no fuel: no batch");
            Assert.AreEqual(BuildingState.NoFuel, sim.BuildingStatus(V, k).state);
            sim.Fuel.AddItem(k, "wood");
            sim.Fuel.Burn(k, sim.Config.FuelMs("wood") - 300);   // a 300 ms sliver
            clock.Advance(50); sim.Tick();
            Assert.Greater(k.smeltDoneAt, 0);
            for (int i = 0; i < 20; i++) { clock.Advance(50); sim.Tick(); }
            Assert.AreEqual(1, SimTestUtil.CountGround(a, "brick"), "the started batch completes for free");
            Assert.AreEqual(0, sim.Fuel.Total(k), 1e-9);
            Assert.AreEqual(0, k.smeltDoneAt);
            Assert.AreEqual(2, k.stock.Get("clay"));
            Assert.AreEqual(BuildingState.NoFuel, sim.BuildingStatus(V, k).state);
        }

        [Test]
        public void Burner_BurnsElapsedTimeOnly()
        {
            var sim = M3Util.Volcano(out var clock);
            var k = sim.Buildings.PlaceBuilt(V, "kiln", 5, 40, starter: false);
            sim.Fuel.AddItem(k, "charcoal");
            double C = sim.Config.FuelMs("charcoal");
            sim.Tick();
            for (int i = 0; i < 40; i++) { clock.Advance(50); sim.Tick(); }   // idle: nothing burns
            Assert.AreEqual(C, sim.Fuel.Total(k), 1e-9);
            k.stock = new ItemCounts(); k.stock.Set("clay", 2);
            clock.Advance(50); sim.Tick();                                      // batch starts (1000 ms)
            for (int i = 0; i < 40; i++) { clock.Advance(50); sim.Tick(); }
            Assert.AreEqual(C - 1000, sim.Fuel.Total(k), 1e-6, "exactly one batch duration burned");
            sim.State.perks.Set("ember", 2);
            k.stock.Set("clay", 2);
            for (int i = 0; i < 40; i++) { clock.Advance(50); sim.Tick(); }
            Assert.AreEqual(C - 1000 - 1000 * 0.85 * 0.85, sim.Fuel.Total(k), 1e-6, "Ember Heart perk");
        }

        [Test]
        public void Burner_HandFeed_FuelVsIngredient()
        {
            var sim = M3Util.Volcano(out _);
            var k = sim.Buildings.PlaceBuilt(V, "kiln", 5, 40, starter: false);
            sim.Hand.Add("wood", 2);
            var r = M3Util.Click(sim, V, k);
            Assert.AreEqual(DropResultKind.Fed, r.kind);
            Assert.AreEqual(1, sim.Fuel.Queue(k).Count, "wood is not a Brick input: rack");

            var pf = sim.Buildings.PlaceBuilt(V, "pill_furnace", 5, 60, starter: false);   // Ember Pill: qi_elixir + firestone
            sim.Hand.Add("firestone", 3);
            sim.Hand.MoveToFront("firestone");
            M3Util.Click(sim, V, pf);
            Assert.AreEqual(1, pf.stock.Get("firestone"), "input firestone goes to stock");
            Assert.AreEqual(0, sim.Fuel.Queue(pf).Count);
            // a wisp delivery (endpointGive) of an input firestone follows the hand rule: stock while it has room…
            Assert.IsTrue(sim.Buildings.EndpointGive(pf, "firestone"));
            Assert.AreEqual(0, sim.Fuel.Queue(pf).Count, "JS quirk fixed: not the rack");
            Assert.AreEqual(2, pf.stock.Get("firestone"));
            // …then the fuel rack once the input stock is full
            pf.stock.Set("firestone", sim.Converters.StockCap(sim.Converters.RecipeOf(pf)));
            Assert.IsTrue(sim.Buildings.EndpointAccepts(pf, "firestone"));
            Assert.IsTrue(sim.Buildings.EndpointGive(pf, "firestone"));
            Assert.AreEqual(1, sim.Fuel.Queue(pf).Count, "stock full: rack");
            pf.stock.Set("firestone", 1);
            // on a recipe without firestone the hand burns it
            sim.SetRecipe(V, pf.id, 1);
            sim.Hand.MoveToFront("firestone");
            M3Util.Click(sim, V, pf);
            Assert.AreEqual(2, sim.Fuel.Queue(pf).Count);
        }

        [Test]
        public void OutputPile_BackPressure()
        {
            var sim = M3Util.Volcano(out var clock);
            var a = sim.State.Area(V);
            var b = Bench(sim, 6);
            var (x, y) = sim.World.BuildingCenterPx(b);
            for (int i = 0; i < 12; i++) a.ground.Add(new GroundItem { id = a.nextGroundId++, item = "plank", x = x, y = y + 60 });
            sim.Tick();
            Assert.AreEqual(0, b.smeltDoneAt, "12 planks beside it: no new batch");
            var st = sim.BuildingStatus(V, b);
            Assert.IsTrue(st.pile); Assert.AreEqual("Output pile full", st.label);
            a.ground.RemoveAll(g => g.item == "plank");
            clock.Advance(100); sim.Tick();
            Assert.AreEqual(0, b.smeltDoneAt, "cached for 500 ms");
            clock.Advance(450); sim.Tick();
            Assert.Greater(b.smeltDoneAt, 0);
            // a finished batch always lands even on a full pile
            for (int i = 0; i < 12; i++) a.ground.Add(new GroundItem { id = a.nextGroundId++, item = "plank", x = x, y = y + 60 });
            clock.Advance(800); sim.Tick();
            Assert.AreEqual(13, SimTestUtil.CountGround(a, "plank"));
        }

        [Test]
        public void Status_IdleStarvedWorking()
        {
            var sim = M3Util.Volcano(out _);
            var b = Bench(sim, 0);
            var st = sim.BuildingStatus(V, b);
            Assert.AreEqual((BuildingState.Idle, "wood"), (st.state, st.item));
            b.stock.Set("wood", 1);
            st = sim.BuildingStatus(V, b);
            Assert.AreEqual((BuildingState.Starved, "Needs Wood"), (st.state, st.label));
            b.stock.Set("wood", 3);
            Assert.AreEqual(BuildingState.Working, sim.BuildingStatus(V, b).state);
            var face = sim.ConverterFace(V, b);
            Assert.AreEqual(1, face.craftable);
            Assert.AreEqual(("wood", 3, 3, 20), (face.inputs[0].item, face.inputs[0].have, face.inputs[0].need, face.inputs[0].cap));
            var ghost = sim.PlaceGhost(V, "workbench", 5, 50);
            Assert.IsNull(sim.BuildingStatus(V, ghost));
        }
    }
}
