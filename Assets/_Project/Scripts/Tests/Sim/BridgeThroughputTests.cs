using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>
    /// Balance pass 1 (ADR 0004): a Spirit Bridge pair must beat carrying by hand for bulk transfer between
    /// Fishing and the Center. Real balance (TEST off), shipped bridge data (carry per sky wisp, buffer, speed).
    /// </summary>
    public class BridgeThroughputTests
    {
        const string C = "center", F = "fishing";

        /// <summary>
        /// Items/s a human-paced player moves by hand between two Islands (the playthrough bot's human profile):
        /// withdraw at 5/s, two 2 s island pans, two cursor moves (300 ms reaction + ~1 s travel), feed at 20/s.
        /// </summary>
        static double HandCarryPerSec(int handCap) => handCap / (handCap / 5.0 + 2 * 2.0 + 2 * 1.3 + handCap / 20.0);

        [Test]
        public void BridgePair_FishingToCenter_BeatsHandCarrying()
        {
            var cfg = SimTestUtil.LoadDataConfig();
            Assert.IsFalse(cfg.test.enabled, "real balance");
            foreach (var r in cfg.regions) { r.spawners.Clear(); r.generators.Clear(); }
            var sim = SimTestUtil.NewSim(out var clock, cfg: cfg, init: false);
            sim.State.quest.idx = cfg.quests.Count;
            sim.NoGroundPhysics = true;
            sim.State.world.SetUnlocked(F, true);
            // where a player puts them: by the Fishing spring field, near the Center's middle
            var send = sim.Buildings.PlaceBuilt(F, "spirit_bridge", 22, 14, starter: false);
            var recv = sim.Buildings.PlaceBuilt(C, "spirit_bridge", 30, 32, starter: false);
            Assert.IsNull(sim.PairBridges(F, send.id, C, recv.id));
            var bridge = cfg.Building("spirit_bridge").bridge;

            int delivered = 0;
            sim.Events.WispArrived += (a, w) => { if (w is SkyWisp sw && !sw.returning) delivered += sw.qty; };
            const int seconds = 300;
            for (int t = 0; t < seconds * 1000; t += 50)
            {
                // an endless stock behind the sender, and a receiver emptied as fast as it fills
                send.inv ??= new List<HandStack>();
                int have = BuildingSystem.GatherTotal(send);
                if (have < bridge.cap) { if (send.inv.Count == 0) send.inv.Add(new HandStack("water", 0)); send.inv[0].qty += bridge.cap - have; }
                recv.inv?.Clear();
                clock.Advance(50);
                sim.Tick();
            }
            double bridgeRate = delivered / (double)seconds;
            double hand20 = HandCarryPerSec(cfg.balance.handCap), hand35 = HandCarryPerSec(cfg.balance.handCap + 15);
            TestContext.WriteLine($"bridge {bridgeRate:F2} items/s (carry {bridge.carry}, cap {bridge.cap}, speed {bridge.speed}); hand {hand20:F2}/s (cap 20), {hand35:F2}/s (Hand Size 3)");
            Assert.Greater(bridgeRate, 1.5 * hand35, "a bridge pair moves bulk well over the best hand-carrying rate");
            Assert.Greater(bridgeRate, 4.0, "≈5 items/s Fishing→Center");
        }

        [Test]
        public void SkyWisp_CarriesAStackOfItems_AndTheReceiverReservationCountsItems()
        {
            var cfg = SimTestUtil.LoadDataConfig();
            foreach (var r in cfg.regions) { r.spawners.Clear(); r.generators.Clear(); }
            var sim = SimTestUtil.NewSim(out var clock, cfg: cfg, init: false);
            sim.State.quest.idx = cfg.quests.Count;
            sim.NoGroundPhysics = true;
            sim.State.world.SetUnlocked(F, true);
            var send = sim.Buildings.PlaceBuilt(F, "spirit_bridge", 22, 14, starter: false);
            var recv = sim.Buildings.PlaceBuilt(C, "spirit_bridge", 30, 32, starter: false);
            Assert.IsNull(sim.PairBridges(F, send.id, C, recv.id));
            int carry = cfg.Building("spirit_bridge").bridge.carry, cap = cfg.Building("spirit_bridge").bridge.cap;
            Assert.Greater(carry, 1);
            // receiver almost full: only 2 slots left → the next wisp carries just 2
            recv.inv = new List<HandStack> { new HandStack("stone", cap - 2) };
            send.inv = new List<HandStack> { new HandStack("water", 30) };
            clock.Advance(50); sim.Tick();
            Assert.AreEqual(1, sim.State.skyWisps.Count);
            Assert.AreEqual(2, sim.State.skyWisps[0].qty, "a wisp takes only what the receiver has room for");
            Assert.AreEqual(28, BuildingSystem.GatherTotal(send));
            Assert.AreEqual(cap, BuildingSystem.GatherTotal(recv) + sim.Logistics.SkyInFlightTo(C, recv.id));
            // room opens: the following beat sends a full load
            recv.inv.Clear();
            for (int t = 0; t < 1100; t += 50) { clock.Advance(50); sim.Tick(); }
            bool full = false;
            foreach (var w in sim.State.skyWisps) if (w.qty == carry) full = true;
            Assert.IsTrue(full, "a full load of " + carry);
            // everything that left the sender arrives (nothing lost or duplicated)
            int sent = 30 - BuildingSystem.GatherTotal(send);
            for (int t = 0; t < 60000; t += 50) { recv.inv?.RemoveAll(h => h.item == "stone"); clock.Advance(50); sim.Tick(); if (sim.State.skyWisps.Count == 0 && BuildingSystem.GatherTotal(send) == 0) break; }
            int arrived = 0;
            if (recv.inv != null) foreach (var h in recv.inv) if (h.item == "water") arrived += h.qty;
            Assert.AreEqual(30, arrived + BuildingSystem.GatherTotal(send), "conservation: " + sent + " sent");
        }
    }
}
