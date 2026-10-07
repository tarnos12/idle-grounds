using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>
    /// Completability + pacing: a deterministic bot (<see cref="PlaythroughBot"/>) plays fresh runs through the
    /// Simulation API only — quests, dragon tributes, island unlocks, Spirit Bridges, Altar upgrades, Automation,
    /// generator buildings, disciples, pills, talismans, the Ascension Gate + offerings — and must ascend and buy
    /// a perk (50 ms ticks, automation every 1 s). The real-balance runs are the ADR 0004 regression gauge
    /// (human first ascension 2–3 h, expert ≥ 1.5 h, hands first / machines 5×+ by mid-run).
    /// See docs/port/playthrough-report.md and docs/port/balance-pass-1.md.
    /// </summary>
    public class PlaythroughBotTests
    {
        const long CapMs = 8L * 3600 * 1000;
        const long Min = 60000, Hour = 60 * Min;

        static PlaythroughBot Play(ulong seed, bool testMode, bool human, int runs = 1)
        {
            var cfg = SimTestUtil.LoadConfig();
            cfg.test.enabled = testMode;
            var sim = SimTestUtil.NewSim(out var clock, seed, cfg, init: false);
            sim.Boot();
            var bot = new PlaythroughBot(sim, clock) { RunsWanted = runs };
            if (human) bot.UseHumanPacing();
            if (runs > 2) bot.VowsForNextRun = r => r == 2 ? new[] { "burden", "coldhearth", "restless", "solitude" } : null;
            bot.Run(CapMs);
            TestContext.WriteLine(Report(bot));
            return bot;
        }

        static string Report(PlaythroughBot bot)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ascended={bot.Ascended} runs={bot.RunsDone} perk={bot.PerkBought} sim={PlaythroughBot.Fmt(bot.AscendedAt.LastOrDefault())} ticks={bot.Ticks} sky={bot.SkyDelivered}");
            foreach (var t in bot.Timeline) sb.AppendLine("  " + PlaythroughBot.Fmt(t.ms) + "  " + t.what);
            foreach (var p in bot.Phases) sb.AppendLine("  phase " + p.phase + " " + PlaythroughBot.Fmt(p.ms));
            foreach (var kv in bot.IdleTicks) sb.AppendLine("  idle " + kv.Key + " " + PlaythroughBot.Fmt(kv.Value * PlaythroughBot.TickMs));
            foreach (var s in bot.Stuck) sb.AppendLine("  STUCK " + s);
            foreach (var s in bot.Violations) sb.AppendLine("  VIOLATION " + s);
            foreach (var s in bot.Notes) sb.AppendLine("  note " + s);
            foreach (var s in bot.Investments) sb.AppendLine("  invest " + s);
            sb.AppendLine($"  production: hand={bot.ProdHand} auto={bot.ProdAuto} crafted={bot.ProdCrafted}");
            foreach (var w in bot.Share)
                sb.AppendLine($"  share run{w.run} {PlaythroughBot.Fmt(w.startMs)} hand={w.hand} auto={w.auto} crafted={w.crafted} auto/hand={(w.hand == 0 ? double.PositiveInfinity : (double)w.auto / w.hand):F1}");
            for (int r = 1; r <= bot.RunsDone; r++)
                sb.AppendLine($"  run{r} {PlaythroughBot.Fmt(bot.RunLength(r))} auto/hand first 20%={bot.AutoRatio(r, 0, 0.2):F2} mid (40-80%)={bot.AutoRatio(r, 0.4, 0.8):F2} last 20%={bot.AutoRatio(r, 0.8, 1.01):F2}");
            return sb.ToString();
        }

        static void AssertCompleted(PlaythroughBot bot, int runs = 1)
        {
            Assert.IsEmpty(bot.Violations, "world invariants held every tick");
            Assert.IsEmpty(bot.Stuck, "bot never got stuck");
            Assert.IsTrue(bot.Ascended, "ascended within the cap");
            Assert.AreEqual(runs, bot.RunsDone, "ascensions");
            Assert.IsNotNull(bot.PerkBought, "bought a perk with the AP");
            Assert.Less(bot.AscendedAt.Last(), CapMs);
            var tl = bot.Timeline.Select(t => t.what).ToList();
            Assert.IsTrue(tl.Exists(t => t.Contains("AWAKENED")), "dragon awakened");
            Assert.IsTrue(tl.Exists(t => t.Contains("first ascension_gate built")), "gate raised");
            Assert.IsTrue(tl.Exists(t => t.Contains("gate offerings 6/6")), "offerings filled");
            Assert.IsTrue(tl.Exists(t => t.Contains("spirit bridge fishing→center paired")), "bridge paired");
            Assert.Greater(bot.SkyDelivered, 0, "items crossed the sky");
        }

        /// <summary>Hand-made items over the first <paramref name="ms"/> of run 1 vs automatic raw items.</summary>
        static (long hand, long auto) FirstMinutes(PlaythroughBot bot, long ms)
        {
            long h = 0, a = 0;
            foreach (var w in bot.Share) if (w.run == 1 && w.startMs < ms) { h += w.hand; a += w.auto; }
            return (h, a);
        }

        /// <summary>The ADR 0004 shape of a real-balance run: hands first, machines 5×+ by mid-run, the machines were bought.</summary>
        static void AssertIdleHybridShape(PlaythroughBot bot)
        {
            var (h, a) = FirstMinutes(bot, 20 * Min);
            Assert.Greater(h, a, "hand work dominates the first 20 minutes");
            double mid = bot.AutoRatio(1, 0.4, 0.8);
            Assert.GreaterOrEqual(mid, 5.0, "by mid-run automated production is ≥ 5× hand production");
            Assert.LessOrEqual(mid, 12.0, "…but hand work still matters mid-run (target 5–10×)");
            var tl = bot.Timeline.Select(t => t.what).ToList();
            Assert.GreaterOrEqual(tl.Count(t => t.Contains("/automation")), 3, "bought Automation (several Islands / levels)");
            Assert.IsTrue(tl.Exists(t => t.Contains("first herb_garden built")), "built a generator building");
            Assert.IsTrue(bot.Investments.Exists(t => t.Contains("disciple #3")), "filled a pavilion with disciples");
        }

        [Test]
        public void Playthrough_TestMode_ReachesAscension()
        {
            // TEST mode (dev toggle) smoke run: short, exercises every quest / island / building path
            var bot = Play(12345, testMode: true, human: false);
            AssertCompleted(bot);
            Assert.Less(bot.AscendedAt[0], 1 * Hour, "TEST mode stays a quick dev run");
            var tl = bot.Timeline.Select(t => t.what).ToList();
            var cfg = SimTestUtil.LoadConfig();
            foreach (var q in cfg.quests) Assert.Contains("quest claimed: " + q.id, tl, "quest " + q.id);
            foreach (var r in cfg.regions) if (r.key != "center") Assert.Contains("island unlocked: " + r.key, tl, "island " + r.key);
            for (int st = 1; st < cfg.dragonStages.Count; st++) Assert.Contains("dragon stage " + st, tl);
            Assert.IsTrue(tl.Exists(t => t.StartsWith("disciple recruited")), "disciple recruited");
            Assert.IsTrue(tl.Exists(t => t.StartsWith("dragon blessing")), "pill fed to the dragon");
            Assert.IsTrue(tl.Exists(t => t.StartsWith("altar upgrade")), "altar upgrade bought");
        }

        [Test]
        public void Playthrough_RealBalance_HumanPaced_FirstAscensionIn2To3Hours()
        {
            var bot = Play(777, testMode: false, human: true);
            AssertCompleted(bot);
            Assert.GreaterOrEqual(bot.AscendedAt[0], 2 * Hour, "ADR 0004: a typical first run is 2–3 h");
            Assert.LessOrEqual(bot.AscendedAt[0], 3 * Hour, "ADR 0004: a typical first run is 2–3 h");
            AssertIdleHybridShape(bot);
        }

        [Test, Category("Slow")]   // ~1–2 min in the Editor; filter out with the Slow category for quick runs
        public void Playthrough_RealBalance_Expert_FirstAscensionAtLeast90Minutes()
        {
            var bot = Play(12345, testMode: false, human: false);
            AssertCompleted(bot);
            Assert.GreaterOrEqual(bot.AscendedAt[0], 90 * Min, "even optimal play needs ≥ 1.5 h");
            Assert.LessOrEqual(bot.AscendedAt[0], 3 * Hour);
        }

        [Test, Category("Slow")]   // ~1–2 min in the Editor; filter out with the Slow category for quick runs
        public void Playthrough_ThreeAscensions_LastRunWithAllVows()
        {
            var bot = Play(4242, testMode: true, human: false, runs: 3);
            AssertCompleted(bot, 3);
            long r1 = bot.RunLength(1), r2 = bot.RunLength(2), r3 = bot.RunLength(3);
            // no world speed per ascension: a veteran run (tutorial skipped, QoL perks) is about as long as the first
            Assert.GreaterOrEqual(r2, (long)(0.7 * r1), "later runs stay about the same length");
            // the all-vows run pays for its AP with time
            Assert.GreaterOrEqual(r3, (long)(1.1 * r2), "vows cost real time");
        }

        [Test, Category("Slow")]   // ~1–2 min in the Editor; filter out with the Slow category for quick runs
        public void Playthrough_IsDeterministic()
        {
            var a = Play(99, testMode: true, human: true);
            var b = Play(99, testMode: true, human: true);
            CollectionAssert.AreEqual(a.Timeline, b.Timeline);
            Assert.AreEqual(a.Ticks, b.Ticks);
        }
    }
}
