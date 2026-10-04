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

            double cap = sim.Offline.CapMs;
            sim.BuyPerk("slumber");
            Assert.AreEqual(8 * 3600e3, cap);
            Assert.AreEqual(10 * 3600e3, sim.Offline.CapMs);

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

    /// <summary>M7: offline catch-up (§15) + boot (§2.1).</summary>
    public class OfflineTests
    {
        const double Hour = 3600e3;

        /// <summary>Save the sim now, then load it into a fresh Simulation whose clock reads now + awayMs.</summary>
        static Simulation Reopen(Simulation sim, ManualClock clock, double awayMs, out ManualClock clock2, ulong seed = 3)
        {
            string json = sim.SaveJson();
            clock2 = new ManualClock(clock.NowMs + (long)awayMs);
            var s = SaveCodec.Deserialize(json, sim.Config, clock2.NowMs);
            Assert.IsNotNull(s);
            return new Simulation(sim.Config, s, clock2, new XorShiftRng(seed));
        }

        [Test]
        public void Begin_NoStampOrShortGap_ReturnsNull_CapRespected()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            sim.State.lastSeen = clock.NowMs - 90000;
            Assert.IsNull(sim.Offline.Begin(), "≤ 90 s = reload");
            sim.State.lastSeen = double.NaN;
            Assert.IsNull(sim.Offline.Begin(), "no stamp");

            sim.State.perks.Set("slumber", 1);
            sim.State.lastSeen = clock.NowMs - 20 * Hour;
            var job = sim.Offline.Begin();
            Assert.AreEqual(10 * Hour, job.elapsedMs);
            Assert.AreEqual(10 * Hour, job.capMs);
            Assert.IsTrue(job.capped);
            Assert.AreEqual(20 * Hour, job.awayMs);
            Assert.AreEqual(Math.Ceiling(10 * Hour / 45000), job.step);
            Assert.AreEqual(OfflineTier.Full, job.Tier);
            Assert.AreEqual(sim.State.lastSeen, sim.State.offlineAwayFrom);
            sim.Offline.Step(job, 0);   // one tick
            sim.Offline.Skip(job);
            var sum = sim.Offline.Finish(job);
            Assert.AreSame(sum, sim.Offline.Finish(job), "idempotent");
            Assert.AreEqual(10 * Hour - sum.simulatedMs, sum.skippedMs);
            Assert.AreEqual(0, sum.saturatedMs);
            Assert.IsTrue(sum.capped);
            Assert.IsTrue(double.IsNaN(sim.State.offlineAwayFrom));
            Assert.IsFalse(sim.Offline.Active);
        }

        [Test]
        public void Boot_Tiers()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            for (int i = 0; i < 100; i++) { clock.Advance(50); sim.Tick(); }

            var a = Reopen(sim, clock, 60e3, out _);
            Assert.AreEqual(OfflineTier.None, a.Boot().tier);

            var b = Reopen(sim, clock, 5 * 60e3, out _);
            var bb = b.Boot();
            Assert.AreEqual(OfflineTier.Toast, bb.tier);
            Assert.IsNotNull(bb.summary);
            Assert.AreEqual(5 * 60e3, bb.summary.simulatedMs, 300);
            Assert.IsFalse(b.Offline.Active);

            var c = Reopen(sim, clock, 2 * Hour, out _);
            var cb = c.Boot();
            Assert.AreEqual(OfflineTier.Full, cb.tier);
            Assert.IsNotNull(cb.job);
            Assert.IsTrue(c.Offline.Active);
            int guard = 0;
            while (!c.Offline.Step(cb.job, 50) && guard++ < 100000) { }
            var sum = c.Offline.Finish(cb.job);
            Assert.AreEqual(OfflineTier.Full, sum.tier);
            Assert.AreEqual(1, OfflineReplay.Progress(cb.job));
        }

        [Test]
        public void Replay_MutesEvents_RestoresFlagsAndClock()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            for (int i = 0; i < 100; i++) { clock.Advance(50); sim.Tick(); }
            var r = Reopen(sim, clock, 30 * 60e3, out var clock2);
            int drops = 0, sounds = 0, progress = 0;
            r.Events.GroundDropped += (_, __, ___, ____, _____) => drops++;
            r.Events.SoundRequested += (_, __) => sounds++;
            r.Events.OfflineProgress += _ => progress++;
            r.InitAllAreas();
            drops = 0; sounds = 0;
            var job = r.Offline.Begin();
            r.Offline.Step(job);
            Assert.AreEqual(0, drops); Assert.AreEqual(0, sounds);
            Assert.Greater(progress, 0);
            Assert.IsFalse(r.Muted); Assert.IsFalse(r.OfflineSim);
            Assert.AreEqual(clock2.NowMs, r.Ctx.Now, "virtual clock removed");
            var sum = r.Offline.Finish(job);
            Assert.Greater(sum.gained.Total(), 0, "the starter network produced while away");
        }

        [Test]
        public void ResumeStamp_InterruptedReplayResumesWithOriginalAwayStart()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var r = Reopen(sim, clock, 3 * Hour, out var clock2);
            r.InitAllAreas();
            double from = r.State.lastSeen;
            var job = r.Offline.Begin();
            r.Offline.Step(job, 0);                         // the tab closes mid-replay
            double expect = clock2.NowMs - (job.end - job.virt);
            Assert.AreEqual(expect, r.Offline.ResumeAt().Value, 1e-6);
            string json = r.SaveJson();
            var s = SaveCodec.Deserialize(json, r.Config, clock2.NowMs);
            Assert.AreEqual(expect, s.lastSeen, 1e-6);
            Assert.AreEqual(from, s.offlineAwayFrom, 1e-6);

            var clock3 = new ManualClock(clock2.NowMs + (long)(10 * 60e3));
            var r2 = new Simulation(r.Config, s, clock3, new XorShiftRng(1));
            r2.InitAllAreas();
            var job2 = r2.Offline.Begin();
            Assert.IsTrue(job2.resumed);
            Assert.AreEqual(clock3.NowMs - from, job2.awayMs, 1e-6);
            Assert.AreEqual(clock3.NowMs - expect, job2.elapsedMs, 1e-6);
        }

        [Test]
        public void Plateau_StopsEarly_AsSaturated()
        {
            // no starter network, no automation: fields fill to cap, then nothing changes
            var cfg = SimTestUtil.LoadConfig();
            var clock = new ManualClock(1_000_000);
            var st = GameState.CreateInitial(cfg, clock.NowMs);
            st.starterPlaced = true;
            var sim = new Simulation(cfg, st, clock, new XorShiftRng(4));
            var r = Reopen(sim, clock, 8 * Hour, out _);
            var boot = r.Boot();
            Assert.AreEqual(OfflineTier.Full, boot.tier);
            Assert.IsTrue(r.Offline.Step(boot.job));
            Assert.IsTrue(boot.job.saturated);
            var sum = r.Offline.Finish(boot.job);
            Assert.Less(sum.simulatedMs, 8 * Hour);
            Assert.Greater(sum.saturatedMs, 0);
            Assert.AreEqual(0, sum.skippedMs);
            Assert.IsNotNull(sum.flatAtMs);
            Assert.GreaterOrEqual(sum.simulatedMs - sum.flatAtMs.Value, 15 * 60e3);
            Assert.AreEqual(1, OfflineReplay.Progress(boot.job));
        }

        [Test]
        public void HeldTally_ExcludesHandAndPavilionBuns()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            var r = Reopen(sim, clock, 20 * 60e3, out _);
            r.InitAllAreas();
            var pav = r.Buildings.PlaceBuilt("center", "meditation_pavilion", 45, 70);
            Assert.IsNotNull(pav);
            long t0 = r.Offline.CountHeldItems().Total();
            pav.buns = 12;
            r.State.hand.Add(new HandStack("plank", 3));
            Assert.AreEqual(t0, r.Offline.CountHeldItems().Total());

            var job = r.Offline.Begin();
            pav.buns = 40;                                  // e.g. fed while away
            r.State.hand.Add(new HandStack("cloth", 3));
            r.Offline.Step(job);
            var sum = r.Offline.Finish(job);
            Assert.IsFalse(sum.gained.Has("spirit_buns"));
            Assert.IsFalse(sum.gained.Has("cloth"));
        }

        [Test]
        public void Replay_TwoHours_MatchesLiveTicking()
        {
            var cfg = SimTestUtil.LoadConfig();
            var sim = SimTestUtil.NewSim(out var clock, seed: 21, cfg: cfg);
            for (int i = 0; i < 1200; i++) { clock.Advance(50); sim.Tick(); if (i % 20 == 19) sim.AutomationTick(); }
            string json = SaveCodec.Serialize(sim.State, clock.NowMs);
            long t0 = clock.NowMs;
            const long span = 2 * 3600 * 1000;

            // live: 50 ms ticks, automation every second
            var liveClock = new ManualClock(t0);
            var live = new Simulation(cfg, SaveCodec.Deserialize(json, cfg, t0), liveClock, new XorShiftRng(8));
            Assert.AreEqual(OfflineTier.None, live.Boot().tier);
            for (long t = 0; t < span; t += 50)
            {
                liveClock.Advance(50);
                live.Tick();
                if ((t + 50) % 1000 == 0) live.AutomationTick();
            }

            // replay: the same span on the virtual clock
            var rClock = new ManualClock(t0 + span);
            var rep = new Simulation(cfg, SaveCodec.Deserialize(json, cfg, t0 + span), rClock, new XorShiftRng(8));
            var boot = rep.Boot();
            Assert.AreEqual(OfflineTier.Full, boot.tier);
            rep.Offline.Step(boot.job);
            var sum = rep.Offline.Finish(boot.job);

            var a = live.Offline.CountHeldItems();
            var b = rep.Offline.CountHeldItems();
            long ta = a.Total(), tb = b.Total();
            long ca = live.State.stats.totalCrafted, cb = rep.State.stats.totalCrafted;
            string detail = $"held live {ta} replay {tb}; crafted live {ca} replay {cb}; saturated={boot.job.saturated} sim={sum.simulatedMs}";
            UnityEngine.Debug.Log("[OfflineTests] " + detail);
            Assert.AreEqual(ta, tb, Math.Max(10, ta * 0.03), "held items — " + detail);
            Assert.AreEqual(ca, cb, Math.Max(5, ca * 0.03), "crafted — " + detail);
        }
    }
}
