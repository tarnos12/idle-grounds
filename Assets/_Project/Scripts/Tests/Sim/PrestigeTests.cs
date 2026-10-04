using System;
using System.Linq;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>M7: ascension, perks, vows (§14).</summary>
    public class PrestigeTests
    {
        [Test]
        public void AscendReward_Formula_RegionsInsightOfferingsVows()
        {
            var sim = SimTestUtil.NewSim(out _);
            var S = sim.State;
            Assert.AreEqual(3, sim.AscendReward(), "center only");
            S.world.SetUnlocked("farm", true); S.world.SetUnlocked("mine", true);
            S.perks.Set("apgain", 2);
            var gate = sim.Buildings.PlaceBuilt("center", "ascension_gate", 40, 70);
            gate.offered = new ItemCounts(); gate.offered.Set("talisman", 2); gate.offered.Set("dragon_scale", 1);
            Assert.AreEqual(3 + 4 + 2 + 3, sim.AscendReward());
            S.vows.active.Add("burden");
            Assert.AreEqual((int)Math.Round(12 * 1.15, MidpointRounding.AwayFromZero), sim.AscendReward());
            S.vows.active.AddRange(new[] { "coldhearth", "restless", "solitude" });
            Assert.AreEqual(21, sim.AscendReward(), "×1.75 with four vows");
        }

        [Test]
        public void Ascend_ResetsRun_KeepsPrestige_AppliesLegacyPerks_AndVows()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var S = sim.State;
            var cfg = sim.Config;
            int resets = 0, sfx = 0;
            sim.Events.RunReset += () => resets++;
            sim.Events.SoundRequested += (n, _) => { if (n == "ascend") sfx++; };
            S.ascensions = 1; S.ascendPoints = 2;
            S.perks.Set("paths", 2); S.perks.Set("legacy", 1); S.perks.Set("hands", 2); S.perks.Set("haste", 1);
            S.vows.active.Add("burden");
            S.vows.done.Set("restless", 1);
            S.world.SetUnlocked("farm", true);
            S.won = true; S.endingSeen = true;
            S.stats.foxKills = 7;
            S.handLevel = 3; S.handCap = 45;
            S.hand.Add(new HandStack("wood", 5));
            S.dragon.stage = 2;
            S.builtTypes.Add("kiln");
            S.pavilionSeeded = true;
            var gate = sim.Buildings.PlaceBuilt("center", "ascension_gate", 40, 70);
            gate.offered = new ItemCounts(); gate.offered.Set("star_steel", 2);
            int expectedAp = (int)Math.Round((3 + 2 + 2) * 1.15, MidpointRounding.AwayFromZero);
            double speedFrom = 1 / sim.Timing.PrestigeFactor(S);
            var oldStats = S.stats;

            var ja = sim.Ascend(new[] { "solitude", "coldhearth", "solitude", "bogus" });

            var N = sim.State;
            Assert.AreNotSame(S, N);
            Assert.AreEqual(1, resets); Assert.AreEqual(1, sfx);
            Assert.AreEqual(2, N.ascensions);
            Assert.AreEqual(2 + expectedAp, N.ascendPoints);
            Assert.AreEqual(2, N.PerkLevel("paths"));
            Assert.AreEqual(cfg.balance.handCap + 10, N.handCap, "Fleet Hands kept, Hand Size lost");
            Assert.AreEqual(0, N.handLevel);
            Assert.AreEqual(0, N.hand.Count);
            Assert.AreEqual(cfg.quests.Count, N.quest.idx);
            Assert.IsTrue(N.dragonBlessed); Assert.IsFalse(N.won);
            Assert.IsTrue(N.endingSeen); Assert.IsTrue(N.introSeen);
            Assert.AreSame(oldStats, N.stats);
            Assert.AreEqual(0, N.dragon.stage);
            Assert.AreEqual(0, N.builtTypes.Count);
            Assert.IsFalse(N.pavilionSeeded);
            Assert.AreEqual(1, N.vows.done.Get("burden"));
            Assert.AreEqual(1, N.vows.done.Get("restless"));
            CollectionAssert.AreEqual(new[] { "solitude", "coldhearth" }, N.vows.active);
            Assert.IsTrue(N.starterPlaced, "solitude ⇒ no starter network");
            Assert.IsFalse(N.Area("center").buildings.Any(b => b.starter));
            Assert.IsTrue(N.Area("center").buildings.Any(b => b.type == "center"), "boot re-ran initArea");
            Assert.IsTrue(N.Area("center").nodes.Count > 0);
            Assert.IsTrue(N.world.IsUnlocked("mine")); Assert.IsTrue(N.world.IsUnlocked("fishing"));
            Assert.IsFalse(N.world.IsUnlocked("farm"));
            Assert.AreEqual(1, N.Area("center").upgrades.automation);
            Assert.AreEqual(0, N.Area("farm").upgrades.automation);
            Assert.AreEqual(2, ja.n); Assert.AreEqual(expectedAp, ja.ap);
            Assert.AreEqual(speedFrom, ja.speedFrom, 1e-9);
            Assert.AreEqual(1 / sim.Timing.PrestigeFactor(N), ja.speedTo, 1e-9);
            Assert.Greater(ja.speedTo, ja.speedFrom, "new burden mark + ascension");
            Assert.AreSame(ja, N.justAscended);
            for (int i = 0; i < 100; i++) { clock.Advance(50); sim.Tick(); }   // the fresh run ticks
        }

        [Test]
        public void FirstAscension_IgnoresVows_AndBuildsStarterNetwork()
        {
            var sim = SimTestUtil.NewSim(out _);
            sim.Ascend(new[] { "solitude" });
            Assert.AreEqual(1, sim.State.ascensions);
            Assert.AreEqual(0, sim.State.vows.active.Count);
            Assert.IsTrue(sim.State.Area("center").buildings.Any(b => b.starter));
            Assert.AreEqual(3, sim.State.ascendPoints);
        }

        [Test]
        public void BuyPerk_ChargesAp_StopsAtMax()
        {
            var sim = SimTestUtil.NewSim(out _);
            var S = sim.State;
            Assert.IsFalse(sim.BuyPerk("haste"), "no AP");
            Assert.IsFalse(sim.BuyPerk("bogus"));
            S.ascendPoints = 100;
            var def = sim.Config.Perk("haste");
            int spent = 0;
            for (int i = 0; i < def.max; i++) { Assert.AreEqual(def.cost[i], sim.PerkCost("haste")); spent += def.cost[i]; Assert.IsTrue(sim.BuyPerk("haste")); }
            Assert.IsNull(sim.PerkCost("haste"));
            Assert.IsFalse(sim.BuyPerk("haste"));
            Assert.AreEqual(100 - spent, S.ascendPoints);
            Assert.AreEqual(Math.Pow(0.95, def.max), sim.Timing.PrestigeFactor(S), 1e-12);
            // every configured perk is buyable once
            foreach (var p in sim.Config.perks) if (p.id != "haste") Assert.IsTrue(sim.BuyPerk(p.id), p.id);
        }

        [Test]
        public void BuyPerk_ImmediateEffects_Hands_Paths_Legacy_Slumber_Insight()
        {
            var sim = SimTestUtil.NewSim(out _);
            var S = sim.State;
            S.ascendPoints = 100;
            int cap0 = S.handCap;
            sim.BuyPerk("hands");
            Assert.AreEqual(cap0 + 5, S.handCap);

            // Remembered Paths L1 opens the Mine now and refunds its installments to the hand
            var mineItem = sim.Config.Region("mine").unlockCost[0].item;
            S.world.unlockPaid.Add(new RegionPaid { region = "mine" });
            S.world.unlockPaid[0].paid.Set(mineItem, 2);
            int had = sim.Hand.Count(mineItem);
            sim.BuyPerk("paths");
            Assert.IsTrue(S.world.IsUnlocked("mine"));
            Assert.IsFalse(S.world.unlockPaid.Any(e => e.region == "mine"));
            Assert.AreEqual(had + 2, sim.Hand.Count(mineItem));

            sim.BuyPerk("legacy");
            Assert.AreEqual(1, S.Area("center").upgrades.automation);

            // Tireless Wisps (save id "slumber"): +15% wisp speed and beat rate per level
            var lanternDef = sim.Config.Building("wisp_lantern");
            double sp0 = sim.Logistics.WispSpeed(S.Area("center"), lanternDef, 0), beat0 = sim.Logistics.BeatMs(S.Area("center"), lanternDef, 0);
            sim.BuyPerk("slumber");
            Assert.AreEqual(1, S.PerkLevel("slumber"));
            Assert.AreEqual(sp0 * 1.15, sim.Logistics.WispSpeed(S.Area("center"), lanternDef, 0), 1e-9);
            Assert.AreEqual(beat0 / 1.15, sim.Logistics.BeatMs(S.Area("center"), lanternDef, 0), 1e-9);

            int ap0 = sim.AscendReward();
            sim.BuyPerk("apgain");
            Assert.AreEqual(ap0 + 1, sim.AscendReward());
        }

        [Test]
        public void BuyPerk_Frugal_RefundsOverpaid_AndOpensFullyPaidRegion()
        {
            var sim = SimTestUtil.NewSim(out _);
            var S = sim.State;
            S.ascendPoints = 100;
            var cost = sim.AreaUnlockCost("farm");
            S.world.unlockPaid.Add(new RegionPaid { region = "farm", paid = cost.Clone() });   // fully paid at the old price
            S.hand.Clear();
            var ground0 = new ItemCounts();
            foreach (var e in cost) ground0.Set(e.item, SimTestUtil.CountGround(S.Area("center"), e.item));
            sim.BuyPerk("frugal");
            Assert.IsTrue(S.world.IsUnlocked("farm"), "nothing left ⇒ opens");
            var newCost = sim.Progression.UnlockCost("farm");
            foreach (var e in cost)
                Assert.AreEqual(e.qty - newCost.Get(e.item),
                    sim.Hand.Count(e.item) + SimTestUtil.CountGround(S.Area("center"), e.item) - ground0.Get(e.item), e.item);
        }
    }
}
