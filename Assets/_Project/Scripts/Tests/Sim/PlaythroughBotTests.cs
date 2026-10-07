using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>
    /// Completability: a deterministic bot (<see cref="PlaythroughBot"/>) plays fresh runs through the
    /// Simulation API only — quests, dragon tributes, island unlocks, Spirit Bridges, Altar upgrades,
    /// disciples, pills, talismans, the Ascension Gate + offerings — and must ascend and buy a perk
    /// within 6 simulated hours (50 ms ticks, automation every 1 s). See docs/port/playthrough-report.md.
    /// </summary>
    public class PlaythroughBotTests
    {
        const long CapMs = 6L * 3600 * 1000;

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

        [Test]
        public void Playthrough_TestMode_ReachesAscension()
        {
            var bot = Play(12345, testMode: true, human: false);
            AssertCompleted(bot);
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
        public void Playthrough_RealBalance_HumanPaced_ReachesAscension()
        {
            AssertCompleted(Play(777, testMode: false, human: true));
        }

        [Test]
        public void Playthrough_ThreeAscensions_LastRunWithAllVows()
        {
            var bot = Play(4242, testMode: true, human: false, runs: 3);
            AssertCompleted(bot, 3);
        }

        [Test]
        public void Playthrough_IsDeterministic()
        {
            var a = Play(99, testMode: true, human: true);
            var b = Play(99, testMode: true, human: true);
            CollectionAssert.AreEqual(a.Timeline, b.Timeline);
            Assert.AreEqual(a.Ticks, b.Ticks);
        }
    }
}
