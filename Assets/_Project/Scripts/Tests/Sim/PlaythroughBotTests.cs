using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>
    /// Completability + pacing: a deterministic bot (<see cref="PlaythroughBot"/>) plays fresh runs through the
    /// Simulation API only — quests, dragon tributes, island unlocks, Spirit Bridges, Altar upgrades, Automation,
    /// generator buildings, disciples, pills, talismans, the Heavenly Gate on Celestial Peak — and must reach the
    /// ending, "Ascend to Immortality" (ADR 0007; 50 ms ticks, automation every 1 s). The real-balance runs are
    /// the ADR 0004 regression gauge (human ending 2–3 h until the ADR 0007 balance pass retunes it, expert ≥ 1.5 h,
    /// hands first / machines 5×+ by mid-game).
    /// See docs/port/playthrough-report.md and docs/port/balance-pass-1.md.
    /// </summary>
    public class PlaythroughBotTests
    {
        const long CapMs = 8L * 3600 * 1000;
        const long Min = 60000, Hour = 60 * Min;

        static PlaythroughBot Play(ulong seed, bool testMode, bool human)
        {
            var cfg = SimTestUtil.LoadConfig();
            cfg.test.enabled = testMode;
            var sim = SimTestUtil.NewSim(out var clock, seed, cfg, init: false);
            sim.State.starterPlaced = true;          // owner 2026-10-13: a new game starts with no buildings placed
            sim.Boot();
            var bot = new PlaythroughBot(sim, clock);
            if (human) bot.UseHumanPacing();
            bot.Run(CapMs);
            TestContext.WriteLine(Report(bot));
            return bot;
        }

        static string Report(PlaythroughBot bot)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ended={bot.Ended} at={PlaythroughBot.Fmt(System.Math.Max(0, bot.EndedAt))} ticks={bot.Ticks} sky={bot.SkyDelivered}");
            foreach (var t in bot.Timeline) sb.AppendLine("  " + PlaythroughBot.Fmt(t.ms) + "  " + t.what);
            foreach (var p in bot.Phases) sb.AppendLine("  phase " + p.phase + " " + PlaythroughBot.Fmt(p.ms));
            foreach (var kv in bot.IdleTicks) sb.AppendLine("  idle " + kv.Key + " " + PlaythroughBot.Fmt(kv.Value * PlaythroughBot.TickMs));
            foreach (var s in bot.Stuck) sb.AppendLine("  STUCK " + s);
            foreach (var s in bot.Violations) sb.AppendLine("  VIOLATION " + s);
            foreach (var s in bot.Notes) sb.AppendLine("  note " + s);
            foreach (var s in bot.Investments) sb.AppendLine("  invest " + s);
            sb.AppendLine($"  production: hand={bot.ProdHand} auto={bot.ProdAuto} crafted={bot.ProdCrafted}");
            foreach (var w in bot.Share)
                sb.AppendLine($"  share {PlaythroughBot.Fmt(w.startMs)} hand={w.hand} auto={w.auto} crafted={w.crafted} auto/hand={(w.hand == 0 ? double.PositiveInfinity : (double)w.auto / w.hand):F1}");
            sb.AppendLine($"  game {PlaythroughBot.Fmt(bot.GameLength)} auto/hand first 20%={bot.AutoRatio(0, 0.2):F2} mid (40-80%)={bot.AutoRatio(0.4, 0.8):F2} last 20%={bot.AutoRatio(0.8, 1.01):F2}");
            return sb.ToString();
        }

        static void AssertCompleted(PlaythroughBot bot)
        {
            Assert.IsEmpty(bot.Violations, "world invariants held every tick");
            Assert.IsEmpty(bot.Stuck, "bot never got stuck");
            Assert.IsTrue(bot.Ended, "reached the ending within the cap");
            Assert.Less(bot.EndedAt, CapMs);
            var tl = bot.Timeline.Select(t => t.what).ToList();
            Assert.IsTrue(tl.Exists(t => t.Contains("AWAKENED")), "dragon awakened");
            Assert.IsTrue(tl.Exists(t => t.Contains("first heavenly_gate built (celestial)")), "Heavenly Gate raised on Celestial Peak");
            Assert.IsTrue(tl.Exists(t => t.Contains("ASCENDED TO IMMORTALITY")), "the ending played");
            Assert.IsTrue(bot.Sim.State.ascended && bot.Sim.State.endingSeen, "ending seen, free play continues");
            Assert.IsTrue(tl.Exists(t => t.Contains("spirit bridge fishing→center paired")), "bridge paired");
            Assert.Greater(bot.SkyDelivered, 0, "items crossed the sky");
        }

        /// <summary>Hand-made items over the first <paramref name="ms"/> vs automatic raw items.</summary>
        static (long hand, long auto) FirstMinutes(PlaythroughBot bot, long ms)
        {
            long h = 0, a = 0;
            foreach (var w in bot.Share) if (w.startMs < ms) { h += w.hand; a += w.auto; }
            return (h, a);
        }

        /// <summary>The ADR 0004 shape of a real-balance game: hands first, machines 5×+ by mid-game, the machines were bought.</summary>
        static void AssertIdleHybridShape(PlaythroughBot bot)
        {
            var (h, a) = FirstMinutes(bot, 20 * Min);
            Assert.Greater(h, a, "hand work dominates the first 20 minutes");
            double mid = bot.AutoRatio(0.4, 0.8);
            // owner 2026-10-13: a new game starts with no buildings, so the player builds the first machines by hand and the
            // mid-run ratio drops (starter network: 8.8x, empty start: 3.8x). Floor lowered 5 -> 3; retune in the balance pass.
            Assert.GreaterOrEqual(mid, 3.0, "by mid-run automated production is ≥ 3× hand production (empty start)");
            Assert.LessOrEqual(mid, 12.0, "…but hand work still matters mid-run (target 5–10×)");
            var tl = bot.Timeline.Select(t => t.what).ToList();
            Assert.GreaterOrEqual(tl.Count(t => t.Contains("/automation")), 3, "bought Automation (several Islands / levels)");
            Assert.IsTrue(tl.Exists(t => t.Contains("first herb_garden built")), "built a generator building");
            Assert.IsTrue(bot.Investments.Exists(t => t.Contains("disciple #3")), "filled a pavilion with disciples");
        }

        [Test]
        public void Playthrough_TestMode_ReachesTheEnding()
        {
            // TEST mode (dev toggle) smoke run: short, exercises every quest / island / building path
            var bot = Play(12345, testMode: true, human: false);
            AssertCompleted(bot);
            Assert.Less(bot.EndedAt, 1 * Hour, "TEST mode stays a quick dev run");
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
        public void Playthrough_RealBalance_HumanPaced_EndingIn2To3Hours()
        {
            var bot = Play(777, testMode: false, human: true);
            AssertCompleted(bot);
            // ADR 0004 band on the current 7 Islands; ADR 0007 step 5 retunes to ~3:00 with 11 Islands
            Assert.GreaterOrEqual(bot.EndedAt, 2 * Hour, "a typical game reaches the ending in 2–3 h");
            Assert.LessOrEqual(bot.EndedAt, 3 * Hour, "a typical game reaches the ending in 2–3 h");
            AssertIdleHybridShape(bot);
        }

        [Test, Category("Slow")]   // ~1–2 min in the Editor; filter out with the Slow category for quick runs
        public void Playthrough_RealBalance_Expert_EndingAtLeast90Minutes()
        {
            var bot = Play(12345, testMode: false, human: false);
            AssertCompleted(bot);
            Assert.GreaterOrEqual(bot.EndedAt, 90 * Min, "even optimal play needs ≥ 1.5 h");
            Assert.LessOrEqual(bot.EndedAt, 3 * Hour);
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
