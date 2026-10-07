using System.Linq;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    public class GroundHandTests
    {
        static void Fill(AreaState a, string item, int n, bool crafted = false, double manualAt = 0)
        {
            for (int i = 0; i < n; i++)
                a.ground.Add(new GroundItem { id = a.nextGroundId++, item = item, x = 500, y = 500, crafted = crafted, manualAt = manualAt });
        }

        [Test]
        public void EvictClasses_FromData()
        {
            var sim = SimTestUtil.NewSim(out _, init: false);
            var m = sim.Ground.EvictClassMap;
            foreach (var it in new[] { "wood", "leaves", "stone", "clay", "wheat", "cotton", "sand", "iron_ore", "fish", "algae", "water", "spirit_herb", "bamboo" })
                Assert.AreEqual(0, m[it], it);
            foreach (var it in new[] { "spirit_essence", "beast_bone", "obsidian", "star_fragment", "moonpetal" })
                Assert.AreEqual(1, m[it], it);
            foreach (var it in new[] { "plank", "glass", "iron_bar", "dragon_scale", "jade_shard", "firestone", "charcoal" })
                Assert.AreEqual(2, m[it], it);
            Assert.AreEqual(1, sim.Ground.EvictClassOf(new GroundItem { item = "jade_shard", gen = true }));
            Assert.AreEqual(2, sim.Ground.EvictClassOf(new GroundItem { item = "wood", crafted = true }));
            Assert.AreEqual(1, sim.Ground.EvictClassOf(new GroundItem { item = "mystery" }));
        }

        [Test]
        public void Eviction_OldestRawCommonFirst_FreshItemsSafe()
        {
            var sim = SimTestUtil.NewSim(out _, init: false);
            var a = sim.State.Area("volcano");
            Fill(a, "stone", 300);
            Fill(a, "spirit_essence", 300);
            sim.Ground.DropGround("volcano", "wood", 5, 100, 100);
            Assert.AreEqual(600, a.ground.Count);
            Assert.AreEqual(295, SimTestUtil.CountGround(a, "stone"));
            Assert.AreEqual(300, SimTestUtil.CountGround(a, "spirit_essence"));
            Assert.AreEqual(5, SimTestUtil.CountGround(a, "wood"));
            Assert.AreEqual(6, a.ground[0].id, "the 5 oldest stones went");
        }

        [Test]
        public void Eviction_ProtectedShareThenClass0ThenClass1()
        {
            var sim = SimTestUtil.NewSim(out _, init: false);
            var a = sim.State.Area("volcano");
            Fill(a, "plank", 500, crafted: true);
            Fill(a, "spirit_essence", 50);
            Fill(a, "stone", 50);
            sim.Ground.DropGround("volcano", "wood", 1, 100, 100);
            Assert.AreEqual(499, SimTestUtil.CountGround(a, "plank"));
            sim.Ground.DropGround("volcano", "wood", 30, 100, 100);
            Assert.AreEqual(480, SimTestUtil.CountGround(a, "plank"), "protected trimmed to the share");
            Assert.AreEqual(39, SimTestUtil.CountGround(a, "stone"), "then class 0");
            Assert.AreEqual(50, SimTestUtil.CountGround(a, "spirit_essence"), "class 1 untouched");
            Assert.AreEqual(600, a.ground.Count);
        }

        [Test]
        public void Eviction_GraceItemsOnlyGoPastHardCap()
        {
            var sim = SimTestUtil.NewSim(out var clock, init: false);
            var a = sim.State.Area("volcano");
            Fill(a, "stone", 899, manualAt: clock.NowMs);
            sim.Ground.DropGround("volcano", "wood", 1, 100, 100, GroundTag.Manual);
            Assert.AreEqual(900, a.ground.Count, "grace items survive up to the hard cap");
            sim.Ground.DropGround("volcano", "wood", 3, 100, 100, GroundTag.Manual);
            Assert.AreEqual(900, a.ground.Count);
            Assert.AreEqual(4, a.ground[0].id, "oldest non-fresh go first");
            Assert.AreEqual(4, SimTestUtil.CountGround(a, "wood"));
            clock.Advance(4000);                                 // grace over: plain class-0 eviction
            sim.Ground.DropGround("volcano", "wood", 1, 100, 100);
            Assert.AreEqual(600, a.ground.Count);
        }

        [Test]
        public void DropGround_TagsJitterAndGenFlag()
        {
            var sim = SimTestUtil.NewSim(out _, init: false);
            var a = sim.State.Area("center");
            sim.Ground.DropGround("center", "plank", 3, 2, 2, GroundTag.Crafted);
            Assert.IsTrue(a.ground.All(g => g.crafted && g.x >= 4 && g.y >= 4 && g.x <= 18 && g.y <= 18));
            sim.Ground.DropGround("center", "jade_shard", 1, 500, 500, GroundTag.Gen);
            Assert.IsTrue(a.ground.Last().gen);
            sim.Ground.DropGround("center", "stone", 1, 500, 500, GroundTag.Gen);
            Assert.IsFalse(a.ground.Last().gen, "gen flag only for class-2 items");
            sim.Ctx.AutoHarvesting = true;
            sim.Ground.DropGround("center", "firestone", 1, 500, 500);
            Assert.IsTrue(a.ground.Last().gen, "untagged bot drops become gen");
            Assert.AreEqual(0, a.ground.Last().manualAt);
        }

        [Test]
        public void Hand_AddCapRotateTake()
        {
            var sim = SimTestUtil.NewSim(out _, init: false);
            var h = sim.Hand;
            Assert.AreEqual(5, h.Add("wood", 5));
            Assert.AreEqual(3, h.Add("stone", 3));
            Assert.AreEqual(12, h.Add("clay", 50));
            Assert.AreEqual(0, h.Add("wood", 1));
            Assert.AreEqual(20, h.Total());
            CollectionAssert.AreEqual(new[] { "wood", "stone", "clay" }, sim.State.hand.Select(s => s.item).ToArray());
            Assert.AreEqual("stone", sim.RotateHand(1));
            CollectionAssert.AreEqual(new[] { "stone", "clay", "wood" }, sim.State.hand.Select(s => s.item).ToArray());
            Assert.AreEqual("wood", sim.RotateHand(-1));
            Assert.IsTrue(h.MoveToFront("clay"));
            Assert.AreEqual("clay", h.Front);
            Assert.AreEqual(12, h.Take("clay", 99));
            Assert.AreEqual("wood", h.TakeFirst());
            Assert.AreEqual(4, h.Count("wood"));
            sim.State.vows.active.Add("burden");
            Assert.AreEqual(10, h.Cap());
        }

        [Test]
        public void Suction_RespectsCapFilterAndPulls()
        {
            var sim = SimTestUtil.NewSim(out _, init: false);
            var a = sim.State.Area("center");
            Fill(a, "wood", 30);
            var r = sim.Suction("center", 500, 500, 64);
            Assert.AreEqual(20, r.picked);
            Assert.AreEqual(10, a.ground.Count);
            Assert.AreEqual(0, sim.Suction("center", 500, 500, 64).picked, "full hand: nothing");

            sim.State.hand.Clear();
            a.ground.Clear();
            Fill(a, "wood", 5);
            Fill(a, "stone", 5);
            r = sim.Suction("center", 500, 500, 64, "stone");
            Assert.AreEqual(5, r.picked);
            Assert.AreEqual(5, sim.Hand.Count("stone"));
            Assert.AreEqual(0, sim.Hand.Count("wood"));

            a.ground.Clear();
            a.ground.Add(new GroundItem { id = 999, item = "clay", x = 550, y = 500 });
            a.ground.Add(new GroundItem { id = 998, item = "clay", x = 600, y = 500 });
            r = sim.Suction("center", 500, 500, 64);
            Assert.AreEqual(1, r.moved);
            Assert.AreEqual(550 - (1 + (1 - 50.0 / 64) * 3), a.ground[0].x, 1e-9);
            Assert.AreEqual(600, a.ground[1].x, "outside radius untouched");
        }

        [Test]
        public void PickupNear_NearestFirst()
        {
            var sim = SimTestUtil.NewSim(out _, init: false);
            var a = sim.State.Area("center");
            sim.State.handCap = 1;
            a.ground.Add(new GroundItem { id = 1, item = "stone", x = 530, y = 500 });
            a.ground.Add(new GroundItem { id = 2, item = "wood", x = 505, y = 500 });
            Assert.AreEqual(1, sim.Ground.PickupNear("center", 500, 500, 64));
            Assert.AreEqual(1, sim.Hand.Count("wood"));
        }

        [Test]
        public void DropFromHand_GroundPillAndBait()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var c = sim.State.Area("center");
            sim.Hand.Add("wood", 3);
            double x = 30 * 32 + 16, y = 30 * 32 + 16;
            int before = c.ground.Count;
            var r = sim.DropFromHand("center", x, y);
            Assert.AreEqual(DropResultKind.Dropped, r.kind);
            Assert.AreEqual(before + 1, c.ground.Count);
            Assert.AreEqual(clock.NowMs, c.ground.Last().manualAt, 1e-9);
            Assert.AreEqual(2, sim.Hand.Count("wood"));
            Assert.IsNull(sim.DropFromHand("center", 46 * 32, 46 * 32, noGround: true), "over the Altar, latched");

            sim.State.hand.Clear();
            sim.Hand.Add("vitality_pill", 2);
            r = sim.DropFromHand("center", x, y);
            Assert.AreEqual(DropResultKind.Used, r.kind);
            Assert.IsTrue(r.once);
            Assert.AreEqual(clock.NowMs + 45000, sim.State.combatBuff.until, 1e-9);
            Assert.AreEqual(1, sim.Hand.Count("vitality_pill"));

            sim.State.hand.Clear();
            sim.Hand.Add("beast_bait", 1);
            r = sim.DropFromHand("center", 80 * 32, 10 * 32);   // cornerTR
            Assert.IsTrue(r.lured);
            Assert.AreEqual("boss", c.enemies.Last().kind);
            Assert.AreEqual(8, c.enemies.Last().hp);
        }

        [Test]
        public void FieldGenerators_RespectCap_AndFireImmediately()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            sim.NoGroundPhysics = true;
            var c = sim.State.Area("center");
            c.ground.Clear();   // M3: drop the starter-network seeds (§16)
            foreach (var b in c.buildings) if (b.type == "gathering_stone") b.built = false;   // M4: keep starter stones from vacuuming the fields
            sim.Tick();
            Assert.AreEqual(1, SimTestUtil.CountGround(c, "clay"), "a zero clock fires once immediately");
            for (int i = 0; i < 400; i++) { clock.Advance(50); sim.Tick(); }
            Assert.AreEqual(10, SimTestUtil.CountGround(c, "clay"));
            Assert.AreEqual(10, SimTestUtil.CountGround(c, "wood"));
            int stone = SimTestUtil.CountGround(c, "stone"), jade = SimTestUtil.CountGround(c, "jade_shard");
            Assert.LessOrEqual(stone, 10);
            Assert.That(stone + jade, Is.InRange(10, 12), "rare finds may add up to genRareIdle past the cap");
            // every clay lies on a cell centre inside clayField
            foreach (var g in c.ground.Where(g => g.item == "clay"))
            {
                Assert.That(g.x, Is.InRange(76 * 32 - 16, 85 * 32 + 16));
                Assert.That(g.y, Is.InRange(76 * 32 - 16, 85 * 32 + 16));
            }
            // a coarse catch-up tick fires as many events as fine ticks would
            var sim2 = SimTestUtil.NewSim(out var clock2);
            sim2.NoGroundPhysics = true;
            var c2 = sim2.State.Area("center");
            foreach (var b in c2.buildings) if (b.type == "gathering_stone") b.built = false;
            sim2.Tick();
            c2.ground.Clear();
            clock2.Advance(7250); sim2.Tick();   // clay 1200 ms (6000 x TEST 0.2) ⇒ 6 events in one coarse gap
            Assert.AreEqual(6, SimTestUtil.CountGround(c2, "clay"));
        }

        [Test]
        public void FieldGenerator_IntervalFactors()
        {
            var sim = SimTestUtil.NewSim(out _);
            var c = sim.State.Area("center");
            var stoneGen = sim.Config.Region("center").generators[1];
            double stoneBase = stoneGen.intervalMs * 0.2;                // TEST timeScale
            Assert.AreEqual(stoneBase, sim.FieldGenerators.IntervalMs(c, stoneGen), 1e-9);
            c.upgrades.quarry = 1;
            sim.State.perks.Set("bounty", 1);
            Assert.AreEqual(stoneBase * 0.8 * 0.9, sim.FieldGenerators.IntervalMs(c, stoneGen), 1e-9);
            var clayGen = sim.Config.Region("center").generators[0];
            Assert.AreEqual("quarry", clayGen.upgrade);
            Assert.AreEqual(clayGen.intervalMs * 0.2 * 0.8 * 0.9, sim.FieldGenerators.IntervalMs(c, clayGen), 1e-9, "clay shares the quarry upgrade");
            var woodGen = sim.Config.Region("center").generators[2];
            Assert.AreEqual("speed", woodGen.upgrade);
            Assert.AreEqual(woodGen.intervalMs * 0.2 * 0.9, sim.FieldGenerators.IntervalMs(c, woodGen), 1e-9, "quarry upgrade only on its generators");
            c.upgrades.speed = 1;
            Assert.AreEqual(woodGen.intervalMs * 0.2 * 0.8 * 0.9, sim.FieldGenerators.IntervalMs(c, woodGen), 1e-9);
        }

        [Test]
        public void Placement_ReasonsAndRegions()
        {
            var sim = SimTestUtil.NewSim(out _);
            var w = sim.World;
            Assert.AreEqual("Off the edge", w.PlaceReason("center", 91, 30, "workbench"));
            Assert.AreEqual("Blocked", w.PlaceReason("center", 45, 45, "workbench"));          // Altar
            Assert.AreEqual("Build around the Altar clearing", w.PlaceReason("center", 2, 30, "workbench"));
            Assert.AreEqual("Build on the rim, outside the field", w.PlaceReason("mine", 40, 40, "workbench"));
            Assert.AreEqual("Water only", w.PlaceReason("center", 30, 30, "algae_farm"));
            Assert.AreEqual("Water only", w.PlaceReason("fishing", 2, 30, "algae_farm"));
            Assert.AreEqual("Fuel rack blocked", w.PlaceReason("center", 30, 1, "kiln"));
            Assert.IsNull(w.PlaceReason("center", 30, 5, "workbench"));
            Assert.IsNull(w.PlaceReason("center", 2, 30, "gathering_stone"), "anyZone may sit on wild land");
            Assert.IsTrue(w.InNoBuild("center", 0, 0));
            Assert.IsTrue(w.InNoBuild("center", 10, 40));
            Assert.IsFalse(w.InNoBuild("center", 30, 5));
            Assert.IsTrue(w.InNoBuild("farm", 30, 5));
            Assert.AreEqual((0, 98), w.RegionOrigin("center"));
            Assert.AreEqual((196, 98), w.RegionOrigin("celestial"));
            Assert.AreEqual("center", w.RegionAt(0, 98));
            Assert.AreEqual("farm", w.RegionAt(92, 92));
            Assert.IsNull(w.RegionAt(95, 50), "gap");
            Assert.IsNull(w.RegionAt(200, 10), "void slot");
            Assert.AreEqual("dragon", w.BuildingAt("center", 12, 12).type);
            // a ghost burner's rack reserves placement
            var c = sim.State.Area("center");
            c.buildings.Add(new Building { id = c.nextBuildId++, type = "kiln", row = 30, col = 15 });
            Assert.AreEqual("Blocked", w.PlaceReason("center", 30, 11, "workbench"));
            Assert.IsFalse(sim.Occupancy.IsOccupied(c, 30, 13), "a ghost's rack is not occupancy");
            Assert.IsTrue(sim.Occupancy.IsOccupied(c, 30, 15), "ghost footprint is");
        }
    }
}
