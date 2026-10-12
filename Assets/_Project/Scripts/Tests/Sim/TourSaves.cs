using System;
using System.IO;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>
    /// Tooling, not a regression test: when the env var IG_TOUR_SAVES names a folder, runs the playthrough bot
    /// (real balance, human pacing, seed 777) to several checkpoints and writes a game save for each, so
    /// tools/bg/shot.sh can boot the real game from a mid/late-game state (visual tour, art review).
    /// Without the env var the test is skipped.
    /// </summary>
    [Category("Slow")]
    public class TourSaves
    {
        static readonly (string name, long minutes)[] Checkpoints =
        {
            ("t05", 5), ("t12", 12), ("t20", 20), ("t30", 30), ("end", 8 * 60),
        };

        [Test]
        public void WriteTourSaves()
        {
            string dir = Environment.GetEnvironmentVariable("IG_TOUR_SAVES");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("IG_TOUR_SAVES not set");
            Directory.CreateDirectory(dir);
            foreach (var (name, minutes) in Checkpoints)
            {
                var cfg = SimTestUtil.LoadDataConfig();
                var sim = SimTestUtil.NewSim(out var clock, 777, cfg, init: false);
                sim.Boot();
                var bot = new PlaythroughBot(sim, clock);
                bot.UseHumanPacing();
                bot.CheckInvariants = false;
                bot.Run(minutes * 60000L);
                File.WriteAllText(Path.Combine(dir, name + ".json"), sim.SaveJson());
                TestContext.WriteLine($"{name}: bot time {PlaythroughBot.Fmt(bot.GameLength)} ended={bot.Ended} quest={sim.State.quest.idx} dragon={sim.State.dragon.stage}");
            }
        }
    }
}
