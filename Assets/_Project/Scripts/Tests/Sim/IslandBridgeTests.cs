using System;
using System.Collections.Generic;
using System.Linq;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>ADR 0003: Island world offsets + Spirit Bridges (one-way cross-Island wisp pairs).</summary>
    public class IslandBridgeTests
    {
        const string C = "center", M = "mine", F = "farm";

        /// <summary>No spawners / field generators, no starter network, no ground physics; Mine unlocked.</summary>
        static Simulation Bare(out ManualClock clock, bool unlockMine = true)
        {
            var cfg = SimTestUtil.LoadConfig();
            foreach (var r in cfg.regions) { r.spawners.Clear(); r.generators.Clear(); }
            var sim = SimTestUtil.NewSim(out clock, cfg: cfg, init: false);
            sim.State.quest.idx = cfg.quests.Count;
            sim.NoGroundPhysics = true;
            if (unlockMine) sim.State.world.SetUnlocked(M, true);
            return sim;
        }

        static Building Bridge(Simulation sim, string island, int r = 10, int c = 10)
        {
            var b = sim.Buildings.PlaceBuilt(island, "spirit_bridge", r, c, starter: false);
            Assert.IsNotNull(b, "bridge placed on " + island);
            return b;
        }

        static void Fill(Building b, string item, int n) { b.inv ??= new List<HandStack>(); b.inv.Add(new HandStack(item, n)); }
        static int Total(Building b) => BuildingSystem.GatherTotal(b);

        static (Building send, Building recv) Pair(Simulation sim)
        {
            var a = Bridge(sim, C);
            var b = Bridge(sim, M);
            Assert.IsNull(sim.PairBridges(C, a.id, M, b.id));
            return (a, b);
        }

        static double Dist(Simulation sim, Building a, string ia, Building b, string ib)
        {
            var (ax, ay) = sim.World.BuildingWorldCenterPx(ia, a);
            var (bx, by) = sim.World.BuildingWorldCenterPx(ib, b);
            return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
        }

        /// <summary>Tick in 50 ms steps until the predicate holds; returns the clock time it first held.</summary>
        static long RunUntil(Simulation sim, ManualClock clock, Func<bool> done, int maxMs = 300_000)
        {
            for (int t = 0; t < maxMs; t += 50)
            {
                clock.Advance(50); sim.Tick();
                if (done()) return clock.NowMs;
            }
            Assert.Fail("condition never held");
            return -1;
        }

        // ---------------------------------------------------------------- islands

        [Test]
        public void IslandOffsets_ConfigDefaults_AndSceneOverride()
        {
            var sim = Bare(out _);
            int cell = sim.Config.grid.cell;
            Assert.AreEqual((123.0 * cell, 0.0), sim.IslandOffsetPx(C));
            Assert.AreEqual((249.0 * cell, 4.0 * cell), sim.IslandOffsetPx(M));
            Assert.AreEqual((127.0 * cell, 252.0 * cell), sim.IslandOffsetPx("celestial"));
            // every pair of Islands is separated by sky (no overlap of the 93-cell squares)
            int n = sim.Config.grid.cells;
            var regs = sim.Config.regions;
            for (int i = 0; i < regs.Count; i++)
                for (int j = i + 1; j < regs.Count; j++)
                {
                    var a = regs[i]; var b = regs[j];
                    bool overlapX = a.islandCol < b.islandCol + n && b.islandCol < a.islandCol + n;
                    bool overlapY = a.islandRow < b.islandRow + n && b.islandRow < a.islandRow + n;
                    Assert.IsFalse(overlapX && overlapY, a.key + " overlaps " + b.key);
                }
            sim.SetIslandOffsets(new[] { new KeyValuePair<string, (double x, double y)>(M, (10000, 500)), new KeyValuePair<string, (double x, double y)>("nowhere", (1, 1)) });
            Assert.AreEqual((10000.0, 500.0), sim.IslandOffsetPx(M));
            Assert.AreEqual((10010.0, 520.0), sim.ToWorldPx(M, 10, 20));
        }

        [Test]
        public void Bridge_RevealedOnceTwoIslandsAreUnlocked()
        {
            var sim = Bare(out _, unlockMine: false);
            Assert.IsFalse(sim.IsBuildingUnlocked("spirit_bridge"), "only the Center is open (veteran or not)");
            Assert.AreEqual("Unlock a second island", sim.Buildings.UnlockReason("spirit_bridge"));
            Assert.IsNull(sim.PlaceGhost(C, "spirit_bridge", 10, 10));
            sim.State.world.SetUnlocked(F, true);
            Assert.IsTrue(sim.IsBuildingUnlocked("spirit_bridge"));
            var g = sim.PlaceGhost(C, "spirit_bridge", 10, 10);
            Assert.IsNotNull(g);
            Assert.AreEqual((2, 1), sim.World.BuildingSize("spirit_bridge"));
            Assert.AreEqual(10, sim.BuildingNeeds(g).Get("wood"));
            Assert.AreEqual(10, sim.BuildingNeeds(g).Get("stone"));
        }

        // ---------------------------------------------------------------- pairing

        [Test]
        public void Pairing_Rules_List_AndUnpair()
        {
            var sim = Bare(out _);
            var a = Bridge(sim, C);
            var a2 = Bridge(sim, C, 20, 20);
            var b = Bridge(sim, M);
            var lockedFarm = Bridge(sim, F);   // placed directly; the Farm is still locked

            var list = sim.PairableBridges(C, a.id);
            Assert.AreEqual(1, list.Count, "only unpaired bridges on OTHER unlocked islands");
            Assert.AreEqual(M, list[0].island);
            Assert.AreSame(b, list[0].building);
            Assert.AreEqual(Dist(sim, a, C, b, M), list[0].distancePx, 1e-6);

            Assert.IsNotNull(sim.PairBridges(C, a.id, C, a2.id), "same island");
            Assert.IsNotNull(sim.PairBridges(C, a.id, F, lockedFarm.id), "locked island");
            Assert.IsNull(sim.PairBridges(C, a.id, M, b.id));
            Assert.AreSame(b, sim.BridgePartner(C, a));
            Assert.AreSame(a, sim.BridgePartner(M, b));
            Assert.IsTrue(a.pairSends); Assert.IsFalse(b.pairSends);
            Assert.IsNotNull(sim.PairBridges(C, a2.id, M, b.id), "already paired");
            Assert.AreEqual(0, sim.PairableBridges(C, a2.id).Count);

            // link roles: the sender is a lantern TARGET only, the receiver a SOURCE only
            Assert.IsTrue(sim.CanBeLinkTarget(a)); Assert.IsFalse(sim.CanBeLinkSource(a));
            Assert.IsTrue(sim.CanBeLinkSource(b)); Assert.IsFalse(sim.CanBeLinkTarget(b));

            Assert.IsTrue(sim.UnpairBridge(M, b.id), "either end breaks the pair");
            Assert.IsNull(a.pairIsland); Assert.IsNull(b.pairIsland);
            Assert.IsNull(sim.BridgePartner(C, a));
            Assert.IsFalse(sim.UnpairBridge(C, a.id));
        }

        // ---------------------------------------------------------------- delivery

        [Test]
        public void Delivery_TravelTimeMatchesWorldDistance()
        {
            var sim = Bare(out var clock);
            var (send, recv) = Pair(sim);
            Fill(send, "wood", 1);
            var launches = new List<(string island, SkyWisp w, long at)>();
            var arrivals = new List<(string island, SkyWisp w, long at)>();
            sim.Events.WispLaunched += (i, w) => { if (w is SkyWisp s) launches.Add((i, s, clock.NowMs)); };
            sim.Events.WispArrived += (i, w) => { if (w is SkyWisp s) arrivals.Add((i, s, clock.NowMs)); };

            RunUntil(sim, clock, () => launches.Count > 0);
            Assert.AreEqual(C, launches[0].island);
            var w0 = launches[0].w;
            Assert.AreEqual(1, sim.SkyWisps.Count);
            Assert.AreEqual(170, w0.sp, 1e-9, "base wisp speed");
            double D = Dist(sim, send, C, recv, M);
            Assert.Greater(D, 120 * 30, "the sky gap makes this a long flight");
            double expectedArrive = w0.t0 + D / w0.sp * 1000;

            long at = RunUntil(sim, clock, () => arrivals.Count > 0);
            Assert.AreEqual(M, arrivals[0].island);
            Assert.GreaterOrEqual(at, expectedArrive - 1e-6, "not before the flight time");
            Assert.Less(at, expectedArrive + 50 + 1e-6, "lands on the first tick after it");
            Assert.AreEqual(1, Total(recv));
            Assert.AreEqual(0, Total(send));
            Assert.AreEqual(0, sim.SkyWisps.Count);

            // halfway: the smooth position is on the line between the two bridges (world px)
            var (sx, sy) = sim.World.BuildingWorldCenterPx(C, send);
            var (tx, ty) = sim.World.BuildingWorldCenterPx(M, recv);
            var half = sim.SkyWispPos(w0, w0.t0 + D / w0.sp * 500);
            Assert.AreEqual(0.5, half.frac, 1e-9);
            Assert.AreEqual((sx + tx) / 2, half.x, 1e-6);
            Assert.AreEqual((sy + ty) / 2, half.y, 1e-6);
        }

        [Test]
        public void Distance_FollowsSceneOffsets()
        {
            var sim = Bare(out var clock);
            sim.SetIslandOffset(M, sim.IslandOffsetPx(C).x + 200 * 32, sim.IslandOffsetPx(C).y);   // push the Mine far away
            var (send, recv) = Pair(sim);
            Fill(send, "stone", 1);
            RunUntil(sim, clock, () => sim.SkyWisps.Count > 0);
            var w = sim.SkyWisps[0];
            double D = Dist(sim, send, C, recv, M);
            Assert.AreEqual(D, Math.Sqrt((w.tx - w.sx) * (w.tx - w.sx) + (w.ty - w.sy) * (w.ty - w.sy)), 1e-6);
            Assert.Greater(D, 200 * 32 - 93 * 32);
            long at = RunUntil(sim, clock, () => Total(recv) == 1, 600_000);
            Assert.GreaterOrEqual(at, w.t0 + D / w.sp * 1000 - 1e-6);
        }

        [Test]
        public void Capacity_InFlightReservationsCountAgainstReceiver()
        {
            var sim = Bare(out var clock);
            var (send, recv) = Pair(sim);
            Fill(send, "wood", 30);
            Fill(recv, "stone", 18);
            int maxFly = 0;
            for (int t = 0; t < 120_000; t += 50) { clock.Advance(50); sim.Tick(); maxFly = Math.Max(maxFly, sim.SkyWisps.Count); }
            Assert.AreEqual(2, maxFly, "only the 2 free receiver slots are reserved");
            Assert.AreEqual(20, Total(recv));
            Assert.AreEqual(28, Total(send));
            Assert.AreEqual(BuildingState.Full, sim.BuildingStatus(M, recv).state);
        }

        [Test]
        public void IdleSender_FilledAfterLongIdle_LaunchesOncePerBeat_NoBurst()
        {
            var sim = Bare(out var clock);
            var (send, _) = Pair(sim);
            var launches = new List<(SkyWisp w, long at)>();
            sim.Events.WispLaunched += (i, w) => { if (w is SkyWisp s) launches.Add((s, clock.NowMs)); };
            double beat = sim.Logistics.BeatMs(sim.State.Area(C), sim.Config.Building("spirit_bridge"), clock.NowMs);

            for (int t = 0; t < 60_000; t += 50) { clock.Advance(50); sim.Tick(); }   // a minute paired but empty
            clock.Advance(8000);                                                       // + a long frame hitch (tick gap)
            Fill(send, "wood", 20);
            sim.Tick();
            Assert.AreEqual(1, launches.Count, "a stale/idle beat clock restarts at now and fires once");
            Assert.AreEqual(clock.NowMs, launches[0].w.t0, 1e-9, "not back-dated");

            long start = clock.NowMs;
            for (int t = 0; t < 2000; t += 50) { clock.Advance(50); sim.Tick(); }
            int expected = 1 + (int)Math.Floor(2000 / beat);
            Assert.That(launches.Count, Is.InRange(expected - 1, expected + 1), "then one launch per beat");
            for (int k = 1; k < launches.Count; k++)
                Assert.GreaterOrEqual(launches[k].w.t0 - launches[k - 1].w.t0, beat - 50 - 1e-6, "no catch-up burst");
            Assert.Greater(launches.Last().at, start);

            // re-pair after an idle spell: same rule
            Assert.IsTrue(sim.UnpairBridge(C, send.id));
            var recv2 = Bridge(sim, M, 20, 20);
            clock.Advance(9000); sim.Tick();
            Assert.IsNull(sim.PairBridges(C, send.id, M, recv2.id));
            int before = launches.Count;
            sim.Tick();
            Assert.AreEqual(before + 1, launches.Count, "fresh pair fires once, not a burst");
        }

        [Test]
        public void ArrivalRefused_ReturnsToSender()
        {
            var sim = Bare(out var clock);
            var (send, recv) = Pair(sim);
            Fill(send, "wood", 1);
            int returned = 0;
            sim.Events.WispReturned += (i, w) => { if (w is SkyWisp) returned++; };
            RunUntil(sim, clock, () => sim.SkyWisps.Count > 0);
            Fill(recv, "stone", 20);                    // full by the time it lands (the player stuffed it)
            RunUntil(sim, clock, () => returned > 0);
            Assert.IsTrue(sim.SkyWisps[0].returning);
            RunUntil(sim, clock, () => sim.SkyWisps.Count == 0);
            Assert.AreEqual(1, send.inv.Where(s => s.item == "wood").Sum(s => s.qty), "back in the sender's buffer");
        }

        [Test]
        public void BrokenPair_MidFlight_ReturnsToSender_OrDropsWhereItWas()
        {
            var sim = Bare(out var clock);
            var (send, recv) = Pair(sim);
            Fill(send, "wood", 2);
            RunUntil(sim, clock, () => sim.SkyWisps.Count > 0);
            clock.Advance(3000); sim.Tick();
            var w = sim.SkyWisps[0];
            Assert.IsFalse(w.returning);
            double outX = w.x;
            sim.UnpairBridge(C, send.id);
            sim.Tick();
            Assert.IsTrue(w.returning, "pair broken: turns back at once");
            Assert.AreEqual(C, w.toIsland);
            Assert.AreEqual(send.id, w.toId);
            Assert.AreEqual(0, sim.Logistics.SkyInFlightTo(M, recv.id), "no longer reserves the receiver");
            RunUntil(sim, clock, () => sim.SkyWisps.Count == 0);
            Assert.AreEqual(2, Total(send), "item back in the (now unpaired) sender");
            Assert.AreEqual(0, Total(recv));
            Assert.Greater(outX, 0);

            // sender demolished mid-flight: the wisp flies back and drops at the sender's spot
            Assert.IsNull(sim.PairBridges(C, send.id, M, recv.id));
            RunUntil(sim, clock, () => sim.SkyWisps.Count > 0);
            var (lx, ly) = sim.World.BuildingCenterPx(send);
            int dropped = 0;
            sim.Events.WispDropped += (i, x) => { if (x is SkyWisp && i == C) dropped++; };
            Assert.IsTrue(sim.Demolish(C, send.id));
            Assert.IsNull(recv.pairIsland, "demolishing one end unpairs the other");
            int woodBefore = SimTestUtil.CountGround(sim.State.Area(C), "wood");
            RunUntil(sim, clock, () => sim.SkyWisps.Count == 0);
            Assert.AreEqual(1, dropped);
            Assert.AreEqual(woodBefore + 1, SimTestUtil.CountGround(sim.State.Area(C), "wood"));
            var g = sim.State.Area(C).ground.Last(x => x.item == "wood");
            Assert.AreEqual(lx, g.x, 20);
            Assert.AreEqual(ly + 24, g.y, 20);
        }

        [Test]
        public void LocalLanterns_FeedSender_AndEmptyReceiver()
        {
            var sim = Bare(out var clock);
            var (send, recv) = Pair(sim);
            var gs = sim.Buildings.PlaceBuilt(C, "gathering_stone", 14, 10, starter: false);
            var lanC = sim.Buildings.PlaceBuilt(C, "wisp_lantern", 12, 14, starter: false);
            var store = sim.Buildings.PlaceBuilt(M, "storehouse", 16, 10, "wood", starter: false);
            var lanM = sim.Buildings.PlaceBuilt(M, "wisp_lantern", 12, 14, starter: false);
            Fill(gs, "wood", 5);
            Assert.IsNull(sim.AddLink(C, lanC.id, gs.id, send.id), "sending bridge = link target");
            Assert.IsNull(sim.AddLink(M, lanM.id, recv.id, store.id), "receiving bridge = link source");
            Assert.IsNotNull(sim.AddLink(M, lanM.id, store.id, recv.id), "receiver refuses to be a target");
            RunUntil(sim, clock, () => store.qty == 5);
            Assert.AreEqual(0, Total(gs)); Assert.AreEqual(0, Total(send)); Assert.AreEqual(0, Total(recv));
        }

        [Test]
        public void TirelessWisps_SpeedsBridgeBeatsAndFlights()
        {
            var sim = Bare(out var clock);
            var def = sim.Config.Building("spirit_bridge");
            var area = sim.State.Area(C);
            double beat0 = sim.Logistics.BeatMs(area, def, clock.NowMs);
            sim.State.perks.Set(LogisticsSystem.TirelessPerk, 2);
            Assert.AreEqual(beat0 / 1.3, sim.Logistics.BeatMs(area, def, clock.NowMs), 1e-9);
            var (send, _) = Pair(sim);
            Fill(send, "wood", 1);
            RunUntil(sim, clock, () => sim.SkyWisps.Count > 0);
            Assert.AreEqual(170 * 1.3, sim.SkyWisps[0].sp, 1e-9);
        }

        // ---------------------------------------------------------------- save

        [Test]
        public void SaveRoundTrip_KeepsPairsAndSkyWisps_AndTheyStillLand()
        {
            var sim = Bare(out var clock);
            var (send, recv) = Pair(sim);
            Fill(send, "wood", 3);
            RunUntil(sim, clock, () => sim.SkyWisps.Count > 0);
            clock.Advance(1000); sim.Tick();
            Assert.Greater(sim.SkyWisps.Count, 0);

            string j1 = sim.SaveJson();
            var s2 = Simulation.LoadJson(j1, sim.Config, clock.NowMs, out var reason);
            Assert.IsNotNull(s2, reason);
            Assert.AreEqual(j1, SaveCodec.Serialize(s2, clock.NowMs), "save → load → save is exact");
            var b2 = s2.Area(C).BuildingById(send.id);
            var r2 = s2.Area(M).BuildingById(recv.id);
            Assert.AreEqual(M, b2.pairIsland); Assert.AreEqual(recv.id, b2.pairId); Assert.IsTrue(b2.pairSends);
            Assert.AreEqual(C, r2.pairIsland); Assert.AreEqual(send.id, r2.pairId); Assert.IsFalse(r2.pairSends);
            Assert.AreEqual(sim.SkyWisps.Count, s2.skyWisps.Count);
            var w1 = sim.SkyWisps[0]; var w2 = s2.skyWisps[0];
            Assert.IsInstanceOf<SkyWisp>(w2);
            Assert.AreEqual(w1.fromIsland, w2.fromIsland); Assert.AreEqual(w1.toIsland, w2.toIsland);
            Assert.AreEqual(w1.t0, w2.t0); Assert.AreEqual(w1.tx, w2.tx); Assert.AreEqual(w1.item, w2.item);
            Assert.GreaterOrEqual(s2.nextSkyWispId, w2.id + 1);

            var sim2 = new Simulation(sim.Config, s2, clock, new XorShiftRng(7)) { NoGroundPhysics = true };
            sim2.Boot();
            RunUntil(sim2, clock, () => BuildingSystem.GatherTotal(r2) == 3);
            Assert.AreEqual(0, s2.skyWisps.Count);
        }

        [Test]
        public void Load_BrokenOrOneSidedPairs_AreCleared()
        {
            var sim = Bare(out var clock);
            var (send, recv) = Pair(sim);
            recv.pairId = 999;                                    // hand-edited: no longer points back
            var s2 = SaveCodec.Deserialize(sim.SaveJson(), sim.Config, clock.NowMs);
            Assert.IsNull(s2.Area(C).BuildingById(send.id).pairIsland);
            Assert.IsNull(s2.Area(M).BuildingById(recv.id).pairIsland);
        }
    }

    /// <summary>ADR 0002: no offline progress — loading a save hours later resumes the frozen world.</summary>
    public class NoOfflineProgressTests
    {
        static long Held(GameState s)
        {
            long n = 0;
            foreach (var a in s.areas)
            {
                n += a.ground.Count + a.wisps.Count;
                foreach (var b in a.buildings)
                {
                    if (b.item != null) n += b.qty;
                    if (b.inv != null) foreach (var st in b.inv) n += st.qty;
                    if (b.stock != null) foreach (var e in b.stock) n += e.qty;
                }
            }
            return n + s.skyWisps.Count;
        }

        [Test]
        public void LoadHoursLater_NoCatchUp_ClocksResumeFromNow()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            sim.State.world.SetUnlocked("farm", true);
            sim.InitAllAreas();
            for (int i = 0; i < 1200; i++) { clock.Advance(50); sim.Tick(); }   // a minute of live play
            string json = sim.SaveJson();

            var later = new ManualClock(clock.NowMs + (long)(5 * 3600e3));
            var s2 = Simulation.LoadJson(json, sim.Config, later.NowMs, out var reason);
            Assert.IsNotNull(s2, reason);
            long before = Held(s2);
            // every periodic producer may fire at most once on the first tick (stale clock restarts — no burst)
            int producers = 0;
            foreach (var a in s2.areas)
            {
                if (!s2.world.IsUnlocked(a.key)) continue;
                producers += 2 * a.genTimers.Count;                     // item + possible rare drop
                foreach (var b in a.buildings)
                {
                    var def = sim.Config.Building(b.type);
                    if (!b.built) continue;
                    if (def.gen.enabled || def.roster.enabled) producers += 1;
                    if (def.IsConverter) producers += def.recipes.Max(r => Math.Max(1, r.outputQty));
                }
            }
            var sim2 = new Simulation(sim.Config, s2, later, new XorShiftRng(9));
            sim2.Boot();
            later.Advance(50); sim2.Tick();
            long after = Held(s2);
            Assert.LessOrEqual(after - before, producers, "no hours of catch-up output");

            // and every periodic clock is armed in the future, relative to the new now
            foreach (var a in s2.areas)
                if (s2.world.IsUnlocked(a.key))
                    foreach (var t in a.genTimers) Assert.Greater(t, later.NowMs - 1, a.key + " field clock re-armed from now");
        }
    }
}
