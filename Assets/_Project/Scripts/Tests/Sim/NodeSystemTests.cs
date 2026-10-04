using System.Linq;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    public class NodeSystemTests
    {
        [Test]
        public void InitArea_PlacesAltarDragonFixturesDecoAndSpawners()
        {
            var sim = SimTestUtil.NewSim(out _);
            var c = sim.State.Area("center");
            var altar = c.buildings.First(b => b.type == "center");
            Assert.AreEqual((44, 44), (altar.row, altar.col));
            Assert.IsTrue(altar.built);
            var dragon = c.buildings.First(b => b.type == "dragon");
            Assert.AreEqual((10, 10), (dragon.row, dragon.col));
            var quarry = SimTestUtil.FirstOfKind(c, "quarry");
            Assert.AreEqual((79, 11), (quarry.row, quarry.col));
            Assert.IsTrue(quarry.isFixed);
            var tree = SimTestUtil.FirstOfKind(c, "spirittree");
            Assert.AreEqual((10, 44), (tree.row, tree.col));
            var spring = SimTestUtil.FirstOfKind(sim.State.Area("fishing"), "spring");
            Assert.AreEqual((11, 11), (spring.row, spring.col));
            Assert.Greater(c.nodes.Count(n => n.deco), 50);
            Assert.AreEqual(10, SimTestUtil.CountSpawner(c, "bush"));
            Assert.AreEqual(8, SimTestUtil.CountSpawner(sim.State.Area("farm"), "crop"));
            Assert.AreEqual(5, SimTestUtil.CountSpawner(sim.State.Area("farm"), "cotton"));
            Assert.AreEqual(150, SimTestUtil.CountSpawner(sim.State.Area("mine"), "ore"));
            Assert.AreEqual(30, SimTestUtil.CountSpawner(sim.State.Area("mine"), "ironvein"));
            Assert.AreEqual(3, c.genTimers.Count);
            // every spawner node sits inside its zone and no two nodes overlap
            foreach (var reg in sim.Config.regions)
            {
                var a = sim.State.Area(reg.key);
                var seen = new bool[93 * 93];
                foreach (var n in a.nodes)
                    for (int r = n.row; r < n.row + n.size; r++)
                        for (int cc = n.col; cc < n.col + n.size; cc++)
                        {
                            Assert.IsFalse(seen[r * 93 + cc], $"{reg.key} overlap at {r},{cc}");
                            seen[r * 93 + cc] = true;
                            if (n.spawnerKind != null)
                                Assert.IsTrue(sim.World.CellInZone(reg.spawners.First(s => s.kind == n.spawnerKind).zone, r, cc));
                        }
            }
            // bush spacing ≥ 6
            var bushes = c.nodes.Where(n => n.spawnerKind == "bush").ToList();
            for (int i = 0; i < bushes.Count; i++)
                for (int j = i + 1; j < bushes.Count; j++)
                    Assert.GreaterOrEqual(System.Math.Sqrt(System.Math.Pow(bushes[i].row - bushes[j].row, 2) + System.Math.Pow(bushes[i].col - bushes[j].col, 2)), 6);
        }

        [Test]
        public void InitArea_IsIdempotent_AndTrimsOvershoot()
        {
            var sim = SimTestUtil.NewSim(out _);
            string Snap() => string.Join("|", sim.State.areas.Select(a =>
                a.key + ":" + string.Join(",", a.nodes.Select(n => n.id + "@" + n.row + "/" + n.col)) + "#" +
                string.Join(",", a.buildings.Select(b => b.id + b.type))));
            var before = Snap();
            sim.InitAllAreas();
            Assert.AreEqual(before, Snap());

            // a config that lowers a target trims (keeps the first `target`)
            var c = sim.State.Area("center");
            var firstIds = c.nodes.Where(n => n.spawnerKind == "bush").Take(4).Select(n => n.id).ToArray();
            sim.Config.Region("center").spawners[0].target = 4;
            sim.Nodes.InitArea("center");
            CollectionAssert.AreEqual(firstIds, c.nodes.Where(n => n.spawnerKind == "bush").Select(n => n.id).ToArray());
        }

        [Test]
        public void Occupancy_IncrementalEqualsRecompute()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var c = sim.State.Area("center");
            for (int i = 0; i < 20; i++)
            {
                var bush = c.nodes.First(n => n.spawnerKind == "bush");
                sim.Harvest("center", bush.id);
                sim.Harvest("center", bush.id);
                clock.Advance(3000);
                sim.Tick();
            }
            var inc = sim.Occupancy.Snapshot(c);
            var full = sim.Occupancy.Recompute(c);
            CollectionAssert.AreEqual(full, inc);
            Assert.IsTrue(sim.Occupancy.IsOccupied(c, 44, 44));     // Altar
            Assert.IsTrue(sim.Occupancy.IsOccupied(c, 79, 11));     // quarry rock
        }

        [Test]
        public void Harvest_Chop_AccumulatesThenDropsOnFell()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var c = sim.State.Area("center");
            var bush = c.nodes.First(n => n.spawnerKind == "bush");
            Assert.IsTrue(sim.Harvest("center", bush.id));
            Assert.AreEqual(0, SimTestUtil.CountGround(c, "leaves"));
            Assert.AreEqual(1, bush.pending.Get("leaves"));
            Assert.AreEqual(1, bush.hitsLeft);
            Assert.IsTrue(sim.Harvest("center", bush.id));
            int leaves = SimTestUtil.CountGround(c, "leaves");
            Assert.That(leaves, Is.InRange(2, 3));                 // pending 1 + drops 1-2
            Assert.IsNull(c.NodeById(bush.id));
            Assert.AreEqual(leaves, sim.State.stats.totalGathered);
            var g = c.ground.First(x => x.item == "leaves");
            Assert.AreEqual(clock.NowMs, g.manualAt, 1e-9);          // non-fixture: grace starts now
            Assert.AreEqual(bush.id, g.src);
            Assert.AreEqual(1, c.spawnQueue.Count);
            Assert.AreEqual("bush", c.spawnQueue[0].kind);
            Assert.IsFalse(sim.Harvest("center", bush.id));         // gone
        }

        [Test]
        public void Harvest_Quarry_FixtureDropsEveryNClicks_WithFixtureGrace()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var c = sim.State.Area("center");
            var tree = SimTestUtil.FirstOfKind(c, "spirittree");
            sim.Harvest("center", tree.id);
            sim.Harvest("center", tree.id);
            Assert.AreEqual(0, SimTestUtil.CountGround(c, "wood"));
            sim.Harvest("center", tree.id);
            Assert.That(SimTestUtil.CountGround(c, "wood"), Is.InRange(2, 3));
            Assert.AreEqual(0, tree.clicks);
            Assert.IsNotNull(c.NodeById(tree.id));                  // fixtures never deplete
            var w = c.ground.First(x => x.item == "wood");
            Assert.AreEqual(clock.NowMs + 4000, w.manualAt, 1e-9);   // 8000 ms total grace

            var rock = SimTestUtil.FirstOfKind(c, "quarry");
            for (int i = 0; i < 5; i++) sim.Harvest("center", rock.id);
            Assert.AreEqual(1, SimTestUtil.CountGround(c, "stone"));

            // a later swing re-arms the grace of the node's earlier drops
            clock.Advance(3000);
            sim.Harvest("center", tree.id);
            Assert.AreEqual(clock.NowMs + 4000, w.manualAt, 1e-9);
        }

        [Test]
        public void Harvest_Break_DropsOnlyOnLastHit()
        {
            var sim = SimTestUtil.NewSim(out _);
            var m = sim.State.Area("mine");
            var ore = m.nodes.First(n => n.spawnerKind == "ore");
            Assert.AreEqual(2, ore.hitsLeft);
            sim.Harvest("mine", ore.id);
            Assert.AreEqual(0, m.ground.Count);
            sim.Harvest("mine", ore.id);
            Assert.AreEqual(3, SimTestUtil.CountGround(m, "stone"));
            Assert.AreEqual(1, SimTestUtil.CountGround(m, "clay"));
            Assert.IsNull(m.NodeById(ore.id));
            Assert.AreEqual(10 * 0.2 * 1000 + 1_000_000, m.spawnQueue[0].at, 1e-6);   // tier timer 10 s
        }

        [Test]
        public void Harvest_Break_StoneheartDoublesDrops()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            sim.State.buff = new BuffState { kind = "stoneheart_pill", until = clock.NowMs + 1000 };
            var m = sim.State.Area("mine");
            var ore = m.nodes.First(n => n.spawnerKind == "ore");
            sim.Harvest("mine", ore.id);
            sim.Harvest("mine", ore.id);
            Assert.AreEqual(6, SimTestUtil.CountGround(m, "stone"));
            Assert.AreEqual(2, SimTestUtil.CountGround(m, "clay"));
        }

        [Test]
        public void Harvest_InstantAndSurface()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var f = sim.State.Area("farm");
            var crop = f.nodes.First(n => n.spawnerKind == "crop");
            sim.Harvest("farm", crop.id);
            Assert.That(SimTestUtil.CountGround(f, "wheat"), Is.InRange(2, 3));
            Assert.IsNull(f.NodeById(crop.id));
            Assert.AreEqual(1_000_000 + 20 * 0.2 * 1000, f.spawnQueue[0].at, 1e-6);

            var fi = sim.State.Area("fishing");
            var fish = fi.nodes.First(n => n.spawnerKind == "fish");
            Assert.AreEqual(clock.NowMs + 3000, fish.surfaceUntil, 1e-9);
            sim.Harvest("fishing", fish.id);
            Assert.That(SimTestUtil.CountGround(fi, "fish"), Is.InRange(1, 2));
            Assert.IsNull(fi.NodeById(fish.id));
        }

        [Test]
        public void SurfaceNodes_DiveWhenWindowEnds_OnlyInUnlockedAreas()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var fi = sim.State.Area("fishing");
            int fish = SimTestUtil.CountSpawner(fi, "fish");
            clock.Advance(3000);
            sim.Tick();
            Assert.AreEqual(fish, SimTestUtil.CountSpawner(fi, "fish"), "locked area is frozen");
            sim.State.world.SetUnlocked("fishing", true);
            sim.Tick();
            Assert.AreEqual(0, SimTestUtil.CountSpawner(fi, "fish"));
            Assert.AreEqual(0, SimTestUtil.CountGround(fi, "fish"), "dives drop nothing");
            Assert.AreEqual(0, SimTestUtil.CountGround(fi, "algae"), "dives drop nothing");
            Assert.AreEqual(fish, fi.spawnQueue.Count(e => e.kind == "fish"));
        }

        [Test]
        public void Regrow_RespawnsAfterScaledDelay()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var c = sim.State.Area("center");
            var bush = c.nodes.First(n => n.spawnerKind == "bush");
            sim.Harvest("center", bush.id);
            sim.Harvest("center", bush.id);
            Assert.AreEqual(9, SimTestUtil.CountSpawner(c, "bush"));
            // 12 s * TEST 0.2 = 2400 ms
            clock.Advance(2399); sim.Tick();
            Assert.AreEqual(9, SimTestUtil.CountSpawner(c, "bush"));
            clock.Advance(1); sim.Tick();
            Assert.AreEqual(10, SimTestUtil.CountSpawner(c, "bush"));
            Assert.AreEqual(0, c.spawnQueue.Count);
        }

        [Test]
        public void Regrow_DelayFactors()
        {
            var sim = SimTestUtil.NewSim(out _);
            var c = sim.State.Area("center");
            var bush = c.nodes.First(n => n.spawnerKind == "bush");
            Assert.AreEqual(2400, sim.Nodes.RegrowDelayMs("center", bush), 1e-9);
            sim.State.ascensions = 1;                                   // prestige ×1/1.2
            Assert.AreEqual(2000, sim.Nodes.RegrowDelayMs("center", bush), 1e-9);
            c.upgrades.speed = 1;                                        // ×0.8
            sim.State.perks.Set("regrow", 1);                            // ×0.9
            Assert.AreEqual(2000 * 0.8 * 0.9, sim.Nodes.RegrowDelayMs("center", bush), 1e-9);
            sim.Config.test.enabled = false;                             // no TEST timeScale
            Assert.AreEqual(10000 * 0.8 * 0.9, sim.Nodes.RegrowDelayMs("center", bush), 1e-9);
        }

        [Test]
        public void RespawnQueue_RetriesWhenZoneFull()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var c = sim.State.Area("center");
            // fake a due entry for a spawner that can never fit (size bigger than the zone)
            sim.Config.Region("center").spawners[0].sizes = new System.Collections.Generic.List<int> { 50 };
            c.spawnQueue.Add(new SpawnQueueEntry(clock.NowMs, "bush"));
            sim.Tick();
            Assert.AreEqual(1, c.spawnQueue.Count);
            Assert.AreEqual(clock.NowMs + 500, c.spawnQueue[0].at, 1e-9);
        }

        [Test]
        public void Deco_NotHarvestable_AndEventsRaisedUnlessMuted()
        {
            var sim = SimTestUtil.NewSim(out _);
            var c = sim.State.Area("center");
            var deco = c.nodes.First(n => n.deco);
            Assert.IsFalse(sim.Harvest("center", deco.id));

            int drops = 0, sounds = 0, hits = 0, depleted = 0;
            sim.Events.GroundDropped += (a, i, q, x, y) => drops++;
            sim.Events.SoundRequested += (n, a) => sounds++;
            sim.Events.NodeHit += (a, n, auto) => hits++;
            sim.Events.NodeDepleted += (a, n) => depleted++;
            var crop = sim.State.Area("farm").nodes.First(n => n.spawnerKind == "crop");
            sim.Harvest("farm", crop.id);
            Assert.AreEqual(1, drops);
            Assert.AreEqual(1, sounds);
            Assert.AreEqual(1, hits);
            Assert.AreEqual(1, depleted);
            sim.Muted = true;
            var crop2 = sim.State.Area("farm").nodes.First(n => n.spawnerKind == "crop");
            sim.Harvest("farm", crop2.id);
            Assert.AreEqual(1, drops);
            Assert.AreEqual(1, sounds);
        }
    }
}
