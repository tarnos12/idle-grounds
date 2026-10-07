using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>
    /// Island onboarding in the quest chain (QUEST_CHAIN 3, ADR 0003): the Bridge-the-sky / Sky-caravan /
    /// Wine-of-three-shores quests, their goal kinds, stele / bridge quest targets, build-menu targets, hints,
    /// and the save migration from the 14-quest chain 2.
    /// </summary>
    public class IslandQuestTests
    {
        const string C = "center", F = "fishing";

        /// <summary>A built Spirit Bridge on the first free spot of <paramref name="island"/>.</summary>
        public static Building PlaceBridge(Simulation sim, string island, bool built = true)
        {
            for (int r = 8; r < 88; r += 5)
                for (int c = 8; c < 88; c += 5)
                {
                    var b = built ? sim.Buildings.PlaceBuilt(island, "spirit_bridge", r, c, starter: false)
                                  : sim.Buildings.PlaceGhost(island, "spirit_bridge", r, c);
                    if (b != null) return b;
                }
            Assert.Fail("no room for a bridge on " + island);
            return null;
        }

        /// <summary>No spawners / field generators; Mine + Fishing unlocked; the chain at <paramref name="questId"/>.</summary>
        static Simulation At(string questId, out ManualClock clock)
        {
            var cfg = SimTestUtil.LoadConfig();
            foreach (var r in cfg.regions) { r.spawners.Clear(); r.generators.Clear(); }
            var sim = SimTestUtil.NewSim(out clock, cfg: cfg);
            sim.NoGroundPhysics = true;
            sim.State.world.SetUnlocked("mine", true);
            sim.State.world.SetUnlocked(F, true);
            sim.State.quest.idx = cfg.QuestIndex(questId);
            Assert.GreaterOrEqual(sim.State.quest.idx, 0, questId);
            return sim;
        }

        static void Tick(Simulation sim, ManualClock clock, int ms) { for (int t = 0; t < ms; t += 50) { clock.Advance(50); sim.Tick(); } }

        // ---------------------------------------------------------------- chain

        [Test]
        public void Chain_IslandQuestsSitAfterTheFirstIslands_AndTranslate()
        {
            var cfg = SimTestUtil.LoadDataConfig();
            Assert.AreEqual(3, cfg.balance.questChain, "chain stamp bumped for the inserted quests");
            CollectionAssert.AreEqual(new[]
            {
                "wood", "leaves", "dragon1", "fox", "build", "upgrade", "link", "explore", "dragon2", "iron",
                "waters", "bridge", "caravan", "dragon3", "weaver", "cultivate", "wine",
            }, cfg.quests.Select(q => q.id).ToArray());
            var Q = cfg.quests.ToDictionary(q => q.id);
            Assert.AreEqual(QuestGoalKind.BridgePaired, Q["bridge"].goalKind);
            Assert.AreEqual(1, Q["bridge"].goalNeed, "one intact pair");
            Assert.AreEqual(QuestGoalKind.BridgeDelivered, Q["caravan"].goalKind);
            Assert.AreEqual(30, Q["caravan"].goalNeed);
            Assert.AreEqual(QuestGoalKind.ItemProducedThisRun, Q["wine"].goalKind);
            Assert.AreEqual("spirit_wine", Q["wine"].goalItem);
            Assert.AreEqual(3, Q["wine"].goalNeed);
            Assert.AreEqual("stele", Q["explore"].target.kind);
            Assert.AreEqual("stele", Q["waters"].target.kind);
            Assert.AreEqual("fishing", Q["waters"].target.area);
            Assert.AreEqual("bridge", Q["bridge"].target.kind);
            CollectionAssert.Contains(Q["bridge"].builds, "spirit_bridge");
            CollectionAssert.Contains(Q["wine"].builds, "brewery");
            // no text still speaks of regions / border buttons
            foreach (var q in cfg.quests)
            {
                StringAssert.DoesNotContain("border button", q.desc, q.id);
                StringAssert.DoesNotContain("region", q.desc.ToLowerInvariant(), q.id);
            }
            // the bridge is buildable by then (two Islands open ⇒ revealed), the Brewery too
            var reveal = cfg.reveal.First(r => r.building == "spirit_bridge");
            Assert.IsTrue(reveal.any.Any(c => c.kind == RevealKind.Islands && c.count <= 2));
        }

        // ---------------------------------------------------------------- goal kinds

        [Test]
        public void BridgePaired_CountsBridgeIslandsThenThePair()
        {
            var sim = At("bridge", out _);
            Assert.AreEqual("0/3", P(sim));
            var bf = PlaceBridge(sim, F);
            Assert.AreEqual("1/3", P(sim));
            var bf2 = PlaceBridge(sim, F);
            Assert.AreEqual("1/3", P(sim), "a second bridge on the same Island does not count");
            var bc = PlaceBridge(sim, C);
            Assert.AreEqual("2/3", P(sim));
            Assert.IsFalse(sim.CurrentQuestProgress().done);
            Assert.IsNull(sim.PairBridges(F, bf.id, C, bc.id));
            Assert.AreEqual("3/3", P(sim));
            Assert.IsTrue(sim.CurrentQuestProgress().done);
            Assert.AreEqual("bridge", sim.ClaimQuest().id);
            Assert.AreEqual("caravan", sim.Progression.CurrentQuest.id);
            Assert.IsNotNull(bf2);
        }

        [Test]
        public void BridgeDelivered_CountsItemsLandingAtTheReceiver()
        {
            var sim = At("caravan", out var clock);
            var send = PlaceBridge(sim, F);
            var recv = PlaceBridge(sim, C);
            Assert.IsNull(sim.PairBridges(F, send.id, C, recv.id));
            // hand-feed the sending bridge (right-click), like a player would
            sim.State.handCap = 100;
            sim.Hand.Add("water", 30);
            var (x, y) = sim.World.BuildingCenterPx(send);
            for (int i = 0; i < 30 && sim.Hand.Count("water") > 0; i++) sim.DropFromHand(F, x, y, true);
            Assert.AreEqual(0, sim.Hand.Count("water"), "the sender took all 30");
            Assert.AreEqual(0, sim.CurrentQuestProgress().cur);
            for (int t = 0; t < 300_000 && !sim.CurrentQuestProgress().done; t += 50) { clock.Advance(50); sim.Tick(); }
            Assert.IsTrue(sim.CurrentQuestProgress().done, "30 items crossed the sky");
            Assert.AreEqual(30, sim.State.stats.bridgeDelivered);
            Assert.AreEqual(30, BuildingSystem.GatherTotal(recv), "they sit in the receiving bridge");
        }

        [Test]
        public void ItemProducedThisRun_CountsBrewedWine()
        {
            var sim = At("wine", out var clock);
            sim.State.world.SetUnlocked("farm", true);
            var brewery = sim.Buildings.PlaceBuilt(C, "brewery", 40, 40, starter: false) ?? sim.Buildings.PlaceBuilt(C, "brewery", 60, 20, starter: false);
            Assert.IsNotNull(brewery);
            int ri = sim.Config.Building("brewery").recipes.FindIndex(r => r.output == "spirit_wine");
            brewery.recipe = ri;
            brewery.stock ??= new ItemCounts();
            brewery.stock.Set("wheat", 6); brewery.stock.Set("water", 6); brewery.stock.Set("leaves", 3);
            Assert.AreEqual("0/3", P(sim));
            for (int t = 0; t < 120_000 && !sim.CurrentQuestProgress().done; t += 50) { clock.Advance(50); sim.Tick(); }
            Assert.AreEqual("3/3", P(sim), "three batches of Spirit Wine");
            Assert.AreEqual("wine", sim.ClaimQuest().id);
            Assert.IsTrue(sim.Progression.ChainFinished);
        }

        static string P(Simulation sim) { var p = sim.CurrentQuestProgress(); return p.cur + "/" + p.need; }

        // ---------------------------------------------------------------- targets, build menu, hints

        [Test]
        public void UnlockQuests_RingALockedIslandsStele()
        {
            var sim = At("explore", out _);
            sim.State.world.SetUnlocked("mine", false);
            sim.State.world.SetUnlocked(F, false);
            var t = sim.QuestTarget();
            Assert.IsNotNull(t);
            Assert.AreEqual("stele", t.kind);
            Assert.AreEqual("farm", t.area, "the cheapest neighbouring Island");
            StringAssert.Contains("Farm unlock stele", sim.QuestHint());
            // an Island with installments paid wins
            sim.Hand.Add("wood", 1);
            Assert.AreEqual(UnlockResultKind.Paid, sim.UnlockArea("fishing").kind);
            Assert.AreEqual("fishing", sim.QuestTarget().area);
            sim.State.world.SetUnlocked("mine", true);
            Assert.IsNull(sim.QuestTarget(), "done ⇒ no ring");

            sim.State.quest.idx = sim.Config.QuestIndex("waters");
            t = sim.QuestTarget();
            Assert.AreEqual("stele", t.kind);
            Assert.AreEqual("fishing", t.area);
        }

        [Test]
        public void BridgeQuest_PointsBuildMenuAndRingAtTheBridges()
        {
            var sim = At("bridge", out _);
            Assert.AreEqual("spirit_bridge", sim.BuildTargets().FirstOrDefault(), "build menu: Spirit Bridge card");
            Assert.IsNull(sim.QuestTarget(), "nothing placed yet");
            StringAssert.Contains("Build a Spirit Bridge", sim.QuestHint());
            var ghost = PlaceBridge(sim, F, built: false);
            Assert.AreSame(ghost, sim.QuestTarget().building, "ring the ghost to feed");
            StringAssert.Contains("ghost", sim.QuestHint());
            sim.State.Area(F).buildings.Remove(ghost);
            var bf = PlaceBridge(sim, F);
            Assert.Contains("spirit_bridge", sim.BuildTargets(), "one Island holds a bridge: still a target");
            Assert.AreSame(bf, sim.QuestTarget().building, "ring the unpaired bridge");
            StringAssert.Contains("second Island", sim.QuestHint());
            var bc = PlaceBridge(sim, C);
            CollectionAssert.DoesNotContain(sim.BuildTargets(), "spirit_bridge", "two Islands hold one: pairing is next");
            StringAssert.Contains("pairing list", sim.QuestHint());
            Assert.IsNull(sim.PairBridges(F, bf.id, C, bc.id));
            Assert.IsNull(sim.QuestTarget(), "done");

            sim.ClaimQuest();
            Assert.AreEqual("caravan", sim.Progression.CurrentQuest.id);
            var t = sim.QuestTarget();
            Assert.AreEqual("bridge", t.kind);
            Assert.AreEqual(F, t.area);
            Assert.AreSame(bf, t.building, "caravan rings the sending bridge");
            StringAssert.Contains("Fishing bridge", sim.QuestHint());
            // allocating and non-allocating forms agree
            var into = new QuestTargetInfo();
            Assert.IsTrue(sim.QuestTarget(into));
            Assert.AreSame(t.building, into.building);
        }

        [Test]
        public void WineQuest_BuildMenuAndHintFollowTheMilestoneWalker()
        {
            var sim = At("wine", out _);
            sim.State.world.SetUnlocked("farm", true);
            CollectionAssert.Contains(sim.BuildTargets(), "brewery");
            StringAssert.Contains("Brewery", sim.QuestHint());
            var bt = new List<string>();
            sim.BuildTargets(bt);
            CollectionAssert.AreEqual(sim.BuildTargets(), bt);
            var t = sim.QuestTarget();
            Assert.IsNull(t, "no Brewery yet ⇒ nothing to ring");
            var brewery = sim.Buildings.PlaceBuilt(C, "brewery", 40, 40, starter: false) ?? sim.Buildings.PlaceBuilt(C, "brewery", 60, 20, starter: false);
            Assert.IsNotNull(brewery);
            sim.State.builtTypes.Add("brewery");   // as finishing its ghost would
            Assert.AreSame(brewery, sim.QuestTarget().building);
            CollectionAssert.DoesNotContain(sim.BuildTargets(), "brewery");
        }

        // ---------------------------------------------------------------- save migration

        static GameState Load(GameConfig cfg, int chain, int idx, int ascensions = 0) =>
            SaveCodec.Deserialize("{\"areas\":[],\"world\":{},\"ascensions\":" + ascensions + ",\"quest\":{\"idx\":" + idx + ",\"chain\":" + chain + "}}", cfg, 5000);

        [Test]
        public void SaveFromChain2_KeepsItsQuestById()
        {
            var cfg = SimTestUtil.LoadDataConfig();
            var old = SaveCodec.LegacyQuestChains[2];
            Assert.AreEqual(14, old.Length);
            for (int i = 0; i < old.Length; i++)
            {
                var s = Load(cfg, 2, i);
                Assert.AreEqual(cfg.QuestIndex(old[i]), s.quest.idx, "old idx " + i + " (" + old[i] + ")");
                Assert.AreEqual(3, s.quest.chain);
            }
            Assert.AreEqual(cfg.quests.Count, Load(cfg, 2, 14).quest.idx, "a finished old chain stays finished");
            Assert.AreEqual(cfg.QuestIndex("waters"), Load(cfg, 2, 10).quest.idx, "at waters: the bridge quests come next");
            Assert.AreEqual(cfg.QuestIndex("dragon3"), Load(cfg, 2, 11).quest.idx, "past waters: no rewind to the bridge quests");
            Assert.AreEqual(cfg.quests.Count, Load(cfg, 2, 3, ascensions: 1).quest.idx, "veterans skip to the end");
            // unknown old chain: clamp (old behaviour)
            Assert.AreEqual(5, Load(cfg, 1, 5).quest.idx);
            // current chain: untouched, and loading twice is stable
            var cur = Load(cfg, 3, cfg.QuestIndex("caravan"));
            Assert.AreEqual(cfg.QuestIndex("caravan"), cur.quest.idx);
            SaveCodec.Sanitize(cur, cfg, 5000);
            Assert.AreEqual(cfg.QuestIndex("caravan"), cur.quest.idx);
        }

        [Test]
        public void BridgeDeliveredStat_RoundTripsThroughASave()
        {
            var sim = At("caravan", out var clock);
            sim.State.stats.bridgeDelivered = 17;
            var s = SaveCodec.Deserialize(SaveCodec.Serialize(sim.State, clock.NowMs), sim.Config, clock.NowMs);
            Assert.AreEqual(17, s.stats.bridgeDelivered);
            Assert.AreEqual(sim.Config.QuestIndex("caravan"), s.quest.idx);
        }
    }
}
