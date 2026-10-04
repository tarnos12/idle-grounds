using System.Collections.Generic;
using System.Linq;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>M4 wisp logistics (engine-systems §11, §18.6 golden tests).</summary>
    public class LogisticsSystemTests
    {
        const string A = "center";

        /// <summary>Center with no spawners / field generators; no starter network unless <paramref name="init"/>.</summary>
        static Simulation Bare(out ManualClock clock, bool init = false)
        {
            var cfg = SimTestUtil.LoadConfig();
            foreach (var r in cfg.regions) { r.spawners.Clear(); r.generators.Clear(); }
            var sim = SimTestUtil.NewSim(out clock, cfg: cfg, init: init);
            sim.State.quest.idx = cfg.quests.Count;
            if (!init) sim.OfflineSim = true;   // no ground physics: positions stay put
            return sim;
        }

        static void Run(Simulation sim, ManualClock clock, int ms, int step = 50)
        {
            for (int t = 0; t < ms; t += step) { clock.Advance(step); sim.Tick(); }
        }

        static Building Put(Simulation sim, string type, int r, int c, string lockItem = null)
        {
            var b = sim.Buildings.PlaceBuilt(A, type, r, c, lockItem, starter: false);
            Assert.IsNotNull(b, type + " placed");
            return b;
        }

        static void Fill(Building gs, string item, int n)
        {
            gs.inv ??= new List<HandStack>();
            gs.inv.Add(new HandStack(item, n));
        }

        static void Link(Simulation sim, Building lan, Building from, Building to) =>
            Assert.IsNull(sim.AddLink(A, lan.id, from.id, to.id), "link " + from.type + ">" + to.type);

        static int Inv(Building b, string item) => b.inv?.Where(s => s.item == item).Sum(s => s.qty) ?? 0;

        sealed class Log
        {
            public readonly List<(int to, string item)> arrived = new List<(int, string)>();
            public int launched, returned, dropped;
            public Log(Simulation sim)
            {
                sim.Events.WispLaunched += (a, w) => launched++;
                sim.Events.WispArrived += (a, w) => arrived.Add((w.toId, w.item));
                sim.Events.WispReturned += (a, w) => returned++;
                sim.Events.WispDropped += (a, w) => dropped++;
            }
            public int To(Building b, string item) => arrived.Count(x => x.to == b.id && x.item == item);
        }

        [Test]
        public void StarterNetwork_MovesSeedItems()
        {
            var sim = Bare(out var clock, init: true);
            var c = sim.State.Area(A);
            var log = new Log(sim);
            var bs = c.buildings;
            var kiln = bs.Single(b => b.type == "kiln");
            var array = bs.Single(b => b.type == "infusion_array");
            var stores = bs.Where(b => b.type == "storehouse").ToList();
            var shJade = stores.Single(b => b.item == "jade_shard");
            var shStone = stores.Single(b => b.item == "stone");
            var shBamboo = stores.Single(b => b.item == "bamboo");
            var seals = bs.Where(b => b.type == "warding_seal").ToList();
            var sealStone = seals.Single(b => b.item == "stone");
            var sealWood = seals.Single(b => b.item == "wood");

            Run(sim, clock, 90_000);

            foreach (var it in new[] { "stone", "jade_shard", "wood", "bamboo", "clay", "spirit_essence" })
                Assert.AreEqual(0, SimTestUtil.CountGround(c, it), it + " vacuumed off the ground");
            Assert.AreEqual(6, log.To(kiln, "clay"), "clay stone -> kiln");
            Assert.AreEqual(4, log.To(array, "spirit_essence"), "fox stone -> array");
            Assert.AreEqual(2, log.To(shJade, "jade_shard"));
            Assert.AreEqual(2, shJade.qty);
            Assert.AreEqual(8, log.To(sealStone, "stone") + log.To(shStone, "stone"), "every stone reaches seal or store");
            Assert.AreEqual(8, log.To(sealWood, "wood"));
            Assert.AreEqual(2, log.To(shBamboo, "bamboo"));
            Assert.AreEqual(0, c.wisps.Count, "no wisps left in flight");
            Assert.AreEqual(0, log.returned + log.dropped, "reservations: no refused deliveries");
            Assert.AreEqual(0, sim.State.stats.linksAdded, "starter links don't count");
        }

        [Test]
        public void Lantern_LeastRecentlyServed_SharedSealFeedsBothTargets()
        {
            var sim = Bare(out var clock);
            var log = new Log(sim);
            var gs = Put(sim, "gathering_stone", 80, 13);
            var seal = Put(sim, "warding_seal", 74, 22, "wood");
            var lan = Put(sim, "wisp_lantern", 76, 20);
            var bench = Put(sim, "workbench", 70, 30);
            var kiln = Put(sim, "kiln", 66, 18);
            Fill(gs, "wood", 50);
            Link(sim, lan, gs, seal); Link(sim, lan, seal, bench); Link(sim, lan, seal, kiln);

            Run(sim, clock, 20_000);

            int toBench = log.To(bench, "wood"), toKiln = log.To(kiln, "wood");
            Assert.Greater(toBench, 2, "workbench served");
            Assert.AreEqual(6, toKiln, "kiln's fuel link is not starved: rack (6) filled");
            Assert.AreEqual(6, kiln.fuelQ.Count, "rack full of wood");
            Assert.AreEqual(0, log.returned + log.dropped);
        }

        [Test]
        public void InFlightReservation_PreventsOverDelivery()
        {
            var sim = Bare(out var clock);
            var c = sim.State.Area(A);
            var log = new Log(sim);
            var gs = Put(sim, "gathering_stone", 80, 13);
            var seal = Put(sim, "warding_seal", 30, 44, "wood");   // ~11 s flight
            var lan = Put(sim, "wisp_lantern", 76, 20);
            seal.qty = 18;
            Fill(gs, "wood", 10);
            Link(sim, lan, gs, seal);

            int maxFly = 0;
            for (int t = 0; t < 30_000; t += 50) { clock.Advance(50); sim.Tick(); maxFly = System.Math.Max(maxFly, c.wisps.Count); }

            Assert.AreEqual(2, maxFly, "only the 2 free seal slots are reserved");
            Assert.AreEqual(20, seal.qty);
            Assert.AreEqual(8, Inv(gs, "wood"));
            Assert.AreEqual(2, log.launched);
            Assert.AreEqual(0, log.returned + log.dropped);
            Assert.AreEqual("refused", sim.LinkStatus(A, lan.id, 0).fail);
            Assert.AreEqual(LinkDot.Red, sim.LinkStatus(A, lan.id, 0).dot);
        }

        [Test]
        public void ReturnOnRefuse_TargetFilledMidFlight_FliesHome()
        {
            var sim = Bare(out var clock);
            var c = sim.State.Area(A);
            var log = new Log(sim);
            var gs = Put(sim, "gathering_stone", 80, 13);
            var seal = Put(sim, "warding_seal", 30, 44, "wood");
            var lan = Put(sim, "wisp_lantern", 76, 20);
            Fill(gs, "wood", 1);
            Link(sim, lan, gs, seal);
            Run(sim, clock, 100);
            Assert.AreEqual(1, c.wisps.Count);
            var w = c.wisps[0];
            Assert.AreEqual(0, Inv(gs, "wood"));
            seal.qty = 20;                                   // filled by hand mid-flight
            bool sawReturning = false;
            for (int t = 0; t < 40_000 && c.wisps.Count > 0; t += 50)
            { clock.Advance(50); sim.Tick(); sawReturning |= w.returning; }
            Assert.IsTrue(sawReturning, "refused wisp flagged returning (red glow)");
            Assert.AreEqual(1, log.returned);
            Assert.AreEqual(1, log.To(gs, "wood"), "delivered back home");
            Assert.AreEqual(1, Inv(gs, "wood"));
            Assert.AreEqual(0, log.dropped);
            Assert.AreEqual(0, SimTestUtil.CountGround(c, "wood"));
        }

        [Test]
        public void Drop_WhenTargetGone_OrHomeGoneAfterRefusal()
        {
            // target demolished mid-flight -> dropped where the wisp is
            var sim = Bare(out var clock);
            var c = sim.State.Area(A);
            var log = new Log(sim);
            var gs = Put(sim, "gathering_stone", 80, 13);
            var seal = Put(sim, "warding_seal", 30, 44, "wood");
            var lan = Put(sim, "wisp_lantern", 76, 20);
            Fill(gs, "wood", 1);
            Link(sim, lan, gs, seal);
            Run(sim, clock, 1000);
            var w = c.wisps.Single();
            double wx = w.x, wy = w.y;
            Assert.IsTrue(sim.Demolish(A, seal.id));
            int woodBefore = SimTestUtil.CountGround(c, "wood");   // seal refund includes wood
            Run(sim, clock, 50);
            Assert.AreEqual(0, c.wisps.Count);
            Assert.AreEqual(1, log.dropped);
            Assert.AreEqual(woodBefore + 1, SimTestUtil.CountGround(c, "wood"));
            Assert.Less(System.Math.Abs(c.ground.Last(g => g.item == "wood").y - wy), 200);

            // target full on arrival and home demolished -> dropped below the arrival point
            var sim2 = Bare(out var clock2);
            var c2 = sim2.State.Area(A);
            var log2 = new Log(sim2);
            var gs2 = Put(sim2, "gathering_stone", 80, 13);
            var seal2 = Put(sim2, "warding_seal", 74, 22, "wood");
            var lan2 = Put(sim2, "wisp_lantern", 76, 20);
            Fill(gs2, "wood", 1);
            Link(sim2, lan2, gs2, seal2);
            Run(sim2, clock2, 100);
            seal2.qty = 20;
            Assert.IsTrue(sim2.Demolish(A, gs2.id));
            int before2 = SimTestUtil.CountGround(c2, "wood");
            Run(sim2, clock2, 10_000);
            Assert.AreEqual(0, c2.wisps.Count);
            Assert.AreEqual(0, log2.returned);
            Assert.AreEqual(1, log2.dropped);
            Assert.AreEqual(before2 + 1, SimTestUtil.CountGround(c2, "wood"));
        }

        static (int launched, int delivered, int left) CatchUpRun(int step, int ms)
        {
            var sim = Bare(out var clock);
            var log = new Log(sim);
            var src = Put(sim, "storehouse", 78, 27, "wood");
            var dst = Put(sim, "storehouse", 26, 44, "wood");
            var lan = Put(sim, "wisp_lantern", 76, 20);
            src.qty = 100;
            Link(sim, lan, src, dst);
            sim.Tick();                       // both runs start with a tick at t = 0
            Run(sim, clock, ms, step);
            return (log.launched, dst.qty, src.qty);
        }

        [Test]
        public void CatchUp_FineVsCoarseTicks_DeliverSameCounts()
        {
            var fine = CatchUpRun(50, 12_800);
            var coarse = CatchUpRun(640, 12_800);
            Assert.Greater(fine.delivered, 10);
            Assert.AreEqual(fine.launched, coarse.launched, "same beats fire");
            Assert.AreEqual(fine.left, coarse.left);
            Assert.LessOrEqual(System.Math.Abs(fine.delivered - coarse.delivered), 1, "back-dated wisps arrive on time");
        }

        [Test]
        public void GatheringStone_PullsOnlyLinkedTypes_AndEjectsJunk()
        {
            var sim = Bare(out var clock);
            var c = sim.State.Area(A);
            var gs = Put(sim, "gathering_stone", 80, 13);
            var seal = Put(sim, "warding_seal", 74, 22, "wood");
            var lan = Put(sim, "wisp_lantern", 76, 20);
            Link(sim, lan, gs, seal);
            Fill(gs, "clay", 3);                               // junk vacuumed before the link
            var (cx, cy) = M3Util.Px(sim, gs);
            c.ground.Add(new GroundItem { id = c.nextGroundId++, item = "wood", x = cx + 100, y = cy });
            var stone = new GroundItem { id = c.nextGroundId++, item = "stone", x = cx + 100, y = cy + 10 };
            c.ground.Add(stone);

            Run(sim, clock, 50);
            Assert.AreEqual(0, Inv(gs, "clay"), "unwanted stock ejected");
            Assert.AreEqual(3, SimTestUtil.CountGround(c, "clay"));
            Assert.IsTrue(c.ground.Where(g => g.item == "clay").All(g => g.manualAt > 0), "ejected as manual drops");
            Run(sim, clock, 10_000);
            Assert.AreEqual(0, SimTestUtil.CountGround(c, "wood"), "linked type pulled in");
            Assert.AreEqual((cx + 100, cy + 10), (stone.x, stone.y), "unlinked type untouched");
            Assert.AreEqual(3, SimTestUtil.CountGround(c, "clay"), "ejected junk not re-collected");
        }

        [Test]
        public void GatheringStone_UnlinkedCollectsAnything_AfterGrace()
        {
            var sim = Bare(out var clock);
            var c = sim.State.Area(A);
            var gs = Put(sim, "gathering_stone", 80, 13);
            var (cx, cy) = M3Util.Px(sim, gs);
            double now = clock.NowMs;
            var manual = new GroundItem { id = c.nextGroundId++, item = "stone", x = cx + 5, y = cy, manualAt = now };            // 4 s grace
            var fixture = new GroundItem { id = c.nextGroundId++, item = "clay", x = cx - 5, y = cy, manualAt = now + 4000 };     // 8 s grace
            var plain = new GroundItem { id = c.nextGroundId++, item = "wood", x = cx, y = cy + 5 };
            c.ground.Add(manual); c.ground.Add(fixture); c.ground.Add(plain);

            Run(sim, clock, 50);
            Assert.AreEqual(1, Inv(gs, "wood"), "untagged drop taken at once");
            Run(sim, clock, 3850);                             // t = 3.9 s
            Assert.AreEqual(0, Inv(gs, "stone"), "manual drop in grace");
            Run(sim, clock, 200);                              // t = 4.1 s
            Assert.AreEqual(1, Inv(gs, "stone"), "manual drop collected after 4 s");
            Assert.AreEqual(0, Inv(gs, "clay"), "fixture drop still in grace");
            Run(sim, clock, 3800);                             // t = 7.9 s
            Assert.AreEqual(0, Inv(gs, "clay"));
            Run(sim, clock, 200);                              // t = 8.1 s
            Assert.AreEqual(1, Inv(gs, "clay"), "fixture drop collected after 8 s");
        }

        [Test]
        public void AddLink_RefusesImpossibleLinks()
        {
            var sim = Bare(out _, init: true);
            var c = sim.State.Area(A);
            var bs = c.buildings;
            var lan = bs.First(b => b.type == "wisp_lantern");
            var kiln = bs.Single(b => b.type == "kiln");
            var shJade = bs.Single(b => b.type == "storehouse" && b.item == "jade_shard");
            var gs = bs.First(b => b.type == "gathering_stone");
            int links = lan.links.Count;

            Assert.AreEqual("Not a link source", sim.AddLink(A, lan.id, lan.id, kiln.id));
            Assert.AreEqual("source", sim.LinkRefusal(A, kiln.id, gs.id).code, "converters are not sources");
            Assert.AreEqual("Not a link target", sim.AddLink(A, lan.id, gs.id, lan.id));
            Assert.AreEqual("A building can't feed itself", sim.AddLink(A, lan.id, gs.id, gs.id));
            var why = sim.LinkRefusal(A, shJade.id, kiln.id);
            Assert.AreEqual("types", why.code);
            Assert.AreEqual("Kiln can't use " + sim.Config.Item("jade_shard").name, why.text);
            Assert.IsNotNull(sim.AddLink(A, kiln.id, gs.id, kiln.id), "links live on lanterns only");
            Assert.AreEqual(links, lan.links.Count);
            Assert.AreEqual(0, sim.State.stats.linksAdded, "refused links don't count");

            Assert.IsNull(sim.AddLink(A, lan.id, gs.id, kiln.id));
            Assert.AreEqual(links + 1, lan.links.Count);
            Assert.AreEqual(1, sim.State.stats.linksAdded);
            Assert.IsTrue(sim.LinkTargets(A, shJade.id).All(b => b.type != "kiln"));
            Assert.IsTrue(sim.LinkTargets(A, gs.id).Contains(kiln));
            Assert.IsTrue(sim.LinkSources(A).Contains(shJade) && !sim.LinkSources(A).Contains(kiln));
            Assert.IsTrue(sim.RemoveLink(A, lan.id, links));
            Assert.IsFalse(sim.RemoveLink(A, lan.id, links));
            Assert.AreEqual(links, lan.links.Count);
        }

        [Test]
        public void WispPos_IsTimeParametric()
        {
            var sim = Bare(out var clock);
            var c = sim.State.Area(A);
            var src = Put(sim, "storehouse", 78, 27, "wood");
            var dst = Put(sim, "storehouse", 26, 44, "wood");
            var lan = Put(sim, "wisp_lantern", 76, 20);
            src.qty = 1;
            Link(sim, lan, src, dst);
            Run(sim, clock, 50);
            var w = c.wisps.Single();
            var p0 = sim.WispPos(A, w, w.t0);
            var p1 = sim.WispPos(A, w, w.t0 + 1000);
            var (sx, sy) = M3Util.Px(sim, src);
            Assert.AreEqual((sx, sy), (p0.x, p0.y));
            double moved = System.Math.Sqrt((p1.x - sx) * (p1.x - sx) + (p1.y - sy) * (p1.y - sy));
            Assert.AreEqual(170, moved, 1e-6, "170 px/s");
            Assert.AreEqual(1, sim.WispPos(A, w, w.t0 + 1e7).frac);
        }

        [Test]
        public void WispHaste_Swiftwind_Gale_ScaleBeatAndSpeed()
        {
            var sim = Bare(out var clock);
            var c = sim.State.Area(A);
            var def = sim.Config.Building("wisp_lantern");
            double now = clock.NowMs;
            double baseBeat = sim.Logistics.BeatMs(c, def, now);
            Assert.AreEqual(1000 * sim.Timing.TimeScale, baseBeat, 1e-9);
            c.upgrades.wispRate = 2;
            sim.State.perks.Set("gale", 1);
            sim.State.buff = new BuffState { kind = "swiftwind_pill", until = now + 1000 };
            Assert.AreEqual(baseBeat * 0.85 * 0.85 * 0.9 * 0.5, sim.Logistics.BeatMs(c, def, now), 1e-9);
            Assert.AreEqual(170 * 1.5 / 0.5, sim.Logistics.WispSpeed(c, def, now), 1e-9);
        }
    }
}
