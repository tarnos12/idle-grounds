using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>
    /// The non-allocating view queries (Simulation.ConverterFace(.., into), BuildingStatus(.., into), …)
    /// return exactly what the allocating forms return, and allocate ~nothing once warmed up.
    /// </summary>
    public class NonAllocQueryTests
    {
        const string C = "center", M = "mine";

        /// <summary>Starter network played for a while, Mine unlocked, an unpaired bridge on each Island, fuel in burners, items in hand.</summary>
        static Simulation Played(bool gatePhase = false)
        {
            var sim = SimTestUtil.NewSim(out var clock);
            sim.State.world.SetUnlocked(M, true);
            PlaceSomewhere(sim, C, "spirit_bridge");
            PlaceSomewhere(sim, M, "spirit_bridge");
            var kiln = PlaceSomewhere(sim, M, "kiln");
            foreach (var a in sim.State.areas)
                foreach (var b in a.buildings)
                    if (b.built && sim.Converters.IsBurner(b) && sim.Config.FuelKeys.Count > 0) sim.Ctx.Fuel.AddItem(b, sim.Config.FuelKeys[0]);
            sim.Hand.Add("wood", 5);
            sim.Hand.Add("stone", 3);
            if (gatePhase) sim.State.dragon.stage = sim.Config.dragonStages.Count;   // awakened → Raise-the-Gate milestone
            for (int t = 0; t < 30_000; t += 50) { clock.Advance(50); sim.Tick(); }
            Assert.IsNotNull(kiln);
            return sim;
        }

        static Building PlaceSomewhere(Simulation sim, string area, string type)
        {
            for (int r = 6; r < 90; r += 4)
                for (int c = 6; c < 90; c += 4)
                {
                    var b = sim.Buildings.PlaceBuilt(area, type, r, c, starter: false);
                    if (b != null) return b;
                }
            Assert.Fail("could not place " + type + " on " + area);
            return null;
        }

        static IEnumerable<(string area, Building b)> AllBuildings(Simulation sim)
        {
            foreach (var a in sim.State.areas) foreach (var b in a.buildings) yield return (a.key, b);
        }

        // ---- string forms for equality ----

        static string S(BuildingStatusInfo s) => s == null ? "null" : $"{s.state}|{s.item}|{s.label}|{s.pile}";
        static string S(ConverterFace f)
        {
            if (f == null) return "null";
            var sb = new StringBuilder();
            sb.Append(f.recipeIndex).Append('|').Append(f.recipe?.name).Append('|').Append(f.craftable).Append('|').Append(f.running)
              .Append('|').Append(f.progress.ToString("R")).Append('|').Append(f.batchMs.ToString("R")).Append('|').Append(f.isBurner)
              .Append('|').Append(f.fuelSlots).Append('|').Append(f.fuelTotal.ToString("R")).Append('|').Append(S(f.status))
              .Append('|').Append(f.craftsPerMin.ToString("R")).Append("|in:");
            foreach (var i in f.inputs) sb.Append(i.item).Append(',').Append(i.have).Append(',').Append(i.need).Append(',').Append(i.cap).Append(';');
            sb.Append("|fuel:");
            foreach (var s in f.fuel) sb.Append(s.item).Append(',').Append(s.rem.ToString("R")).Append(',').Append(s.total.ToString("R")).Append(';');
            return sb.ToString();
        }
        static string S(List<string> l) => l == null ? "null" : string.Join(",", l);
        static string S(ItemCounts c) => c == null ? "null" : string.Join(",", c.entries.Select(e => e.item + ":" + e.qty));
        static string S(LinkStatusInfo s) => s == null ? "null" : $"{s.dot}|{s.fail}|{s.text}";
        static string S(QuestTargetInfo t) => t == null ? "null" : $"{t.area}|{t.kind}|{t.node?.id}|{t.building?.id}|{t.zone}";
        static string S(AutomationStatus a) => a == null ? "null" : $"{a.level}|{a.budget}|{a.paused}|{S(a.skipped)}";
        static string S(List<BridgeCandidate> l) =>
            string.Join(";", l.Select(c => $"{c.island},{c.building.id},{c.x:R},{c.y:R},{c.distancePx:R}"));
        static string S(List<UpgradeNodeState> l) => string.Join(";", l.Select(s =>
            $"{s.node.id},{s.distance},{s.tier},{s.visible},{s.selectable},{s.owned},{s.level},{s.max},{s.maxed},{S(s.nextCost)},{s.selected},{S(s.neighbours)}"));

        // ---- equality: old vs new ----

        [TestCase(false)]
        [TestCase(true)]
        public void NonAllocQueries_MatchAllocatingForms(bool gatePhase)
        {
            var sim = Played(gatePhase);
            // reused buffers, deliberately dirty between calls
            var st = new BuildingStatusInfo(); var face = new ConverterFace(); var acc = new List<string> { "junk" };
            var ls = new LinkStatusInfo(); var qt = new QuestTargetInfo(); var aut = new AutomationStatus();
            var bt = new List<string> { "junk" }; var ut = new List<UpgradeNodeState>(); var pb = new List<BridgeCandidate>();
            var cost = new ItemCounts(); var paid = new ItemCounts();

            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var (area, b) in AllBuildings(sim))
                {
                    Assert.AreEqual(S(sim.BuildingStatus(area, b)), sim.BuildingStatus(area, b, st) ? S(st) : "null", "status " + b.type);
                    Assert.AreEqual(S(sim.ConverterFace(area, b)), sim.ConverterFace(area, b, face) ? S(face) : "null", "face " + b.type);
                    foreach (bool ever in new[] { false, true })
                        Assert.AreEqual(S(sim.Logistics.StoneAccepts(area, b, ever)), sim.StoneAccepts(area, b, acc, ever) ? S(acc) : "null", "accepts " + b.type);
                    if (b.links != null)
                        for (int i = 0; i < b.links.Count; i++)
                            Assert.AreEqual(S(sim.LinkStatus(area, b.id, i)), sim.LinkStatus(area, b.id, i, ls) ? S(ls) : "null", "link");
                    if (sim.Logistics.IsBridge(b))
                    {
                        sim.PairableBridges(area, b.id, pb);
                        Assert.AreEqual(S(sim.PairableBridges(area, b.id)), S(pb), "bridges");
                        Assert.Greater(pb.Count, 0, "the other Island's bridge is pairable");
                    }
                }
                foreach (var r in sim.Config.regions)
                {
                    Assert.AreEqual(S(sim.AreaUnlockCost(r.key)), sim.AreaUnlockCost(r.key, cost) ? S(cost) : "null", "cost " + r.key);
                    sim.UnlockPaid(r.key, paid);
                    Assert.AreEqual(S(sim.UnlockPaid(r.key)), S(paid), "paid " + r.key);
                    Assert.AreEqual(S(sim.AutomationStatus(r.key)), sim.AutomationStatus(r.key, aut) ? S(aut) : "null", "auto " + r.key);
                }
                Assert.AreEqual(S(sim.QuestTarget()), sim.QuestTarget(qt) ? S(qt) : "null", "quest target");
                sim.BuildTargets(bt);
                Assert.AreEqual(S(sim.BuildTargets()), S(bt), "build targets");
                sim.Progression.MilestoneBuilds(bt);
                Assert.AreEqual(S(sim.Milestone().builds), S(bt), "milestone builds");
                sim.UpgradeTree(ut);
                Assert.AreEqual(S(sim.UpgradeTree()), S(ut), "upgrade tree");
                // change state between passes: partial unlock payment + some more play
                sim.Hand.Add("stone", 2);
                sim.UnlockArea("farm");
            }
        }

        [Test]
        public void BuildTargets_MatchAcrossQuestChain()
        {
            var sim = Played();
            var bt = new List<string>();
            for (int qi = 0; qi <= sim.Config.quests.Count; qi++)
            {
                sim.State.quest.idx = qi;
                sim.BuildTargets(bt);
                Assert.AreEqual(S(sim.BuildTargets()), S(bt), "quest " + qi);
                var qt = new QuestTargetInfo();
                Assert.AreEqual(S(sim.QuestTarget()), sim.QuestTarget(qt) ? S(qt) : "null", "target quest " + qi);
            }
            for (int stage = 0; stage <= sim.Config.dragonStages.Count; stage++)
            {
                sim.State.dragon.stage = stage;
                sim.Progression.MilestoneBuilds(bt);
                Assert.AreEqual(S(sim.Milestone().builds), S(bt), "stage " + stage);
            }
        }

        // ---- allocation ----

        [Test]
        public void NonAllocQueries_DoNotAllocate()
        {
            var sim = Played();
            var all = AllBuildings(sim).ToList();
            var conv = all.First(x => sim.Config.Building(x.b.type).IsConverter);
            var burner = all.FirstOrDefault(x => x.b.built && sim.Converters.IsBurner(x.b));
            var stone = all.First(x => x.b.type == "gathering_stone" && sim.Logistics.StoneAccepts(x.area, x.b) != null);
            var lantern = all.First(x => x.b.links != null && x.b.links.Count > 0);
            var bridge = all.First(x => sim.Logistics.IsBridge(x.b));
            Assert.IsNotNull(burner.b, "a burner converter exists");

            var st = new BuildingStatusInfo(); var face = new ConverterFace(); var acc = new List<string>();
            var ls = new LinkStatusInfo(); var qt = new QuestTargetInfo(); var aut = new AutomationStatus();
            var bt = new List<string>(); var ut = new List<UpgradeNodeState>(); var pb = new List<BridgeCandidate>();
            var cost = new ItemCounts(); var paid = new ItemCounts();
            sim.State.world.unlockPaid.Clear();
            sim.Hand.Add("stone", 1);
            sim.UnlockArea("farm");   // leaves an installment so UnlockPaid copies something

            var queries = new (string name, Action call)[]
            {
                ("BuildingStatus", () => { foreach (var (a, b) in all) sim.BuildingStatus(a, b, st); }),
                ("ConverterFace", () => { sim.ConverterFace(conv.area, conv.b, face); sim.ConverterFace(burner.area, burner.b, face); }),
                ("StoneAccepts", () => { sim.StoneAccepts(stone.area, stone.b, acc); sim.StoneAccepts(stone.area, stone.b, acc, true); }),
                ("LinkStatus", () => { for (int i = 0; i < lantern.b.links.Count; i++) sim.LinkStatus(lantern.area, lantern.b.id, i, ls); }),
                ("QuestTarget", () => sim.QuestTarget(qt)),
                ("BuildTargets", () => sim.BuildTargets(bt)),
                ("UpgradeTree", () => sim.UpgradeTree(ut)),
                ("AreaUnlockCost", () => { sim.AreaUnlockCost("farm", cost); sim.AreaUnlockCost(M, cost); }),
                ("UnlockPaid", () => sim.UnlockPaid("farm", paid)),
                ("PairableBridges", () => sim.PairableBridges(bridge.area, bridge.b.id, pb)),
                ("AutomationStatus", () => sim.AutomationStatus(C, aut)),
                ("Enemies", () => sim.Enemies(C)),
            };

            // the counter works: the allocating form of one query does register
            Assert.Greater(Measure(() => sim.BuildingStatus(conv.area, conv.b)), 1000, "GC counter sanity");

            var fails = new List<string>();
            foreach (var (name, call) in queries)
            {
                long bytes = Measure(call);
                TestContext.Out.WriteLine($"{name}: {bytes} B / 1000 calls");
                if (bytes > 512) fails.Add($"{name} allocated {bytes} B over 1000 calls");
            }
            // Raise-the-Gate milestone (StepToward walk) too
            var gateSim = Played(gatePhase: true);
            var gbt = new List<string>();
            long gb = Measure(() => gateSim.BuildTargets(gbt));
            TestContext.Out.WriteLine($"BuildTargets (gate phase): {gb} B / 1000 calls");
            if (gb > 512) fails.Add($"BuildTargets (gate phase) allocated {gb} B over 1000 calls");
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }

        /// <summary>
        /// Managed bytes allocated by 1000 calls. GC.GetAllocatedBytesForCurrentThread / GC.GetTotalMemory
        /// read 0 under the editor's Mono, so this uses the profiler's live "GC Allocated In Frame" counter
        /// (a synchronous test never crosses a frame boundary, so the delta is exact).
        /// </summary>
        static long Measure(Action call)
        {
            for (int i = 0; i < 5; i++) call();   // warm-up: JIT, buffer growth, pools
            using (var rec = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                Assert.IsTrue(rec.Valid, "GC Allocated In Frame recorder");
                long before = rec.CurrentValue;
                for (int i = 0; i < 1000; i++) call();
                return rec.CurrentValue - before;
            }
        }
    }
}
