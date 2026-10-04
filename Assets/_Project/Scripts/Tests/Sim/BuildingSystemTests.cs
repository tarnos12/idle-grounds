using System.Collections.Generic;
using System.Linq;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    static class M3Util
    {
        /// <summary>Sim whose center has no random spawners (deterministic layout, like the JS golden run).</summary>
        public static Simulation NoSpawnCenter(out ManualClock clock, bool veteran = true)
        {
            var cfg = SimTestUtil.LoadConfig();
            cfg.Region("center").spawners.Clear();
            var sim = SimTestUtil.NewSim(out clock, cfg: cfg);
            if (veteran) sim.State.quest.idx = cfg.quests.Count;   // every quest/region reveal met
            return sim;
        }

        /// <summary>Uninitialised world (no nodes, no starter), volcano unlocked, all buildings revealed.</summary>
        public static Simulation Volcano(out ManualClock clock, ulong seed = 7)
        {
            var sim = SimTestUtil.NewSim(out clock, seed: seed, init: false);
            sim.State.world.SetUnlocked("volcano", true);
            sim.State.quest.idx = sim.Config.quests.Count;
            sim.State.dragon.stage = 4;
            sim.NoGroundPhysics = true;   // no ground physics: positions stay put
            return sim;
        }

        public static (double x, double y) Px(Simulation sim, Building b) => sim.World.BuildingCenterPx(b);

        public static DropResult Click(Simulation sim, string area, Building b, bool noGround = true)
        {
            var (x, y) = Px(sim, b);
            return sim.DropFromHand(area, x, y, noGround);
        }
    }

    public class BuildingSystemTests
    {
        [Test]
        public void AltarAndDragon_PrePlaced_Indestructible()
        {
            var sim = SimTestUtil.NewSim(out _);
            var c = sim.State.Area("center");
            var altar = c.buildings.Single(b => b.type == "center");
            var dragon = c.buildings.Single(b => b.type == "dragon");
            Assert.AreEqual((44, 44, true), (altar.row, altar.col, altar.built));
            Assert.AreEqual((10, 10, true), (dragon.row, dragon.col, dragon.built));
            Assert.AreEqual(1, altar.id); Assert.AreEqual(2, dragon.id);
            Assert.IsFalse(sim.Demolish("center", altar.id));
            Assert.IsFalse(sim.Demolish("center", dragon.id));
            Assert.AreSame(altar, sim.BuildingAt("center", 46 * 32 + 5, 46 * 32 + 5));
            // right-click on the altar / dragon never drops the item (M5 hooks unset)
            sim.Hand.Add("wood", 3);
            Assert.IsNull(MClick(sim, altar));
            Assert.IsNull(MClick(sim, dragon));
            Assert.AreEqual(3, sim.Hand.Count("wood"));
        }

        static DropResult MClick(Simulation sim, Building b) => M3Util.Click(sim, "center", b, noGround: false);

        [Test]
        public void StarterNetwork_MatchesJsGolden()
        {
            var sim = M3Util.NoSpawnCenter(out _, veteran: false);
            var c = sim.State.Area("center");
            // golden: node old-game/js (center spawners removed so placement is deterministic)
            var golden = new (string type, int r, int c, string item, string links)[]
            {
                ("center", 44, 44, null, ""), ("dragon", 10, 10, null, ""),
                ("workbench", 55, 36, null, ""), ("paper_mill", 55, 42, null, ""), ("kiln", 55, 48, null, ""),
                ("infusion_array", 55, 54, null, ""),
                ("storehouse", 78, 27, "jade_shard", ""), ("storehouse", 26, 44, "bamboo", ""), ("storehouse", 83, 27, "stone", ""),
                ("gathering_stone", 80, 13, null, ""), ("gathering_stone", 16, 44, null, ""), ("gathering_stone", 80, 80, null, ""),
                ("gathering_stone", 12, 80, null, ""),
                ("warding_seal", 74, 22, "stone", ""), ("warding_seal", 30, 44, "wood", ""),
                ("wisp_lantern", 76, 20, null, "10>14 14>6 10>7 10>9"),
                ("wisp_lantern", 22, 44, null, "11>15 15>3 15>4 15>5 11>8 8>4"),
                ("wisp_lantern", 52, 58, null, "12>5 13>6"),
                ("gathering_stone", 61, 43, null, ""), ("gathering_stone", 61, 55, null, ""),
                ("storehouse", 63, 36, "plank", ""), ("storehouse", 63, 45, "brick", ""), ("storehouse", 63, 57, "spirit_stone", ""),
                ("wisp_lantern", 62, 51, null, "19>21 19>22 20>23"),
            };
            Assert.AreEqual(golden.Length, c.buildings.Count);
            for (int i = 0; i < golden.Length; i++)
            {
                var b = c.buildings[i]; var g = golden[i];
                string links = b.links == null ? "" : string.Join(" ", b.links.Select(l => l.from + ">" + l.to));
                Assert.AreEqual((i + 1, g.type, g.r, g.c, g.item, g.links), (b.id, b.type, b.row, b.col, b.item, links), "building #" + (i + 1));
                Assert.IsTrue(b.built);
                Assert.AreEqual(i >= 2, b.starter);
                Assert.AreEqual(g.item != null, b.locked);
                if (b.type == "gathering_stone") Assert.IsNotNull(b.inv);
            }
            Assert.IsTrue(sim.State.starterPlaced);
            Assert.AreEqual(0, sim.State.stats.linksAdded);
            Assert.AreEqual(0, sim.State.stats.buildingsBuilt);
            Assert.AreEqual(0, sim.State.builtTypes.Count);
            Assert.AreEqual(30, c.ground.Count);
            Assert.AreEqual(8, SimTestUtil.CountGround(c, "stone"));
            Assert.AreEqual(4, SimTestUtil.CountGround(c, "spirit_essence"));
            // idempotent on the next boot
            sim.InitAllAreas();
            Assert.AreEqual(golden.Length, c.buildings.Count);
        }

        [Test]
        public void StarterNetwork_RandomWorld_AllPlacedLegally()
        {
            for (ulong seed = 1; seed <= 5; seed++)
            {
                var sim = SimTestUtil.NewSim(out _, seed: seed);
                var c = sim.State.Area("center");
                Assert.AreEqual(24, c.buildings.Count, "seed " + seed);
                Assert.AreEqual(4, c.buildings.Count(b => b.type == "wisp_lantern"));
                // no building overlaps a node
                foreach (var b in c.buildings)
                {
                    var (w, h) = sim.Config.BuildingSize(b.type);
                    foreach (var n in c.nodes)
                        Assert.IsFalse(n.row < b.row + h && n.row + n.size > b.row && n.col < b.col + w && n.col + n.size > b.col,
                            b.type + " overlaps node " + n.kind);
                }
            }
        }

        [Test]
        public void PlaceReasons()
        {
            var sim = M3Util.NoSpawnCenter(out _);
            Assert.AreEqual("Off the edge", sim.PlaceReason("center", "workbench", 91, 30));
            Assert.AreEqual("Off the edge", sim.PlaceReason("center", "workbench", -1, 30));
            Assert.AreEqual("Build around the Altar clearing", sim.PlaceReason("center", "workbench", 2, 30));   // midTop
            Assert.AreEqual("Build around the Altar clearing", sim.PlaceReason("center", "workbench", 3, 3));    // cornerTL
            Assert.AreEqual("Blocked", sim.PlaceReason("center", "workbench", 45, 45));                          // the Altar
            Assert.IsNull(sim.PlaceReason("center", "workbench", 30, 30));
            Assert.IsNull(sim.PlaceReason("center", "gathering_stone", 3, 3), "anyZone ignores noBuild");
            Assert.AreEqual("Blocked", sim.PlaceReason("center", "gathering_stone", 12, 12), "dragon");
            Assert.AreEqual("Build on the rim, outside the field", sim.PlaceReason("farm", "workbench", 40, 40));
            Assert.AreEqual("Fuel rack blocked", sim.PlaceReason("center", "kiln", 30, 2), "col-3 < 0");
            Assert.IsNull(sim.PlaceReason("center", "kiln", 30, 26), "midLeft is buildable in center");
            Assert.AreEqual("Locked", sim.PlaceReason("center", "algae_farm", 30, 30));   // stage-locked first
        }

        [Test]
        public void PlaceReasons_LockedAndWaterAndRack()
        {
            var fresh = SimTestUtil.NewSim(out _);
            Assert.AreEqual("Locked", fresh.PlaceReason("center", "kiln", 30, 30));
            Assert.IsNull(fresh.PlaceGhost("center", "kiln", 30, 30));
            Assert.IsFalse(fresh.IsBuildingUnlocked("storehouse"));
            fresh.State.quest.idx = fresh.Config.QuestIndex("wood") + 1;
            Assert.IsTrue(fresh.IsBuildingUnlocked("storehouse"), "revealed by the wood quest");

            var sim = M3Util.Volcano(out _);
            Assert.AreEqual("Water only", sim.PlaceReason("volcano", "algae_farm", 5, 30));
            Assert.AreEqual("Fuel rack blocked", sim.PlaceReason("volcano", "kiln", 5, 1));
            var kiln = sim.PlaceGhost("volcano", "kiln", 5, 40);
            Assert.IsNotNull(kiln);
            // a ghost burner's rack (cols 37-39, rows 5-6) is reserved against placement but not occupied
            var a = sim.State.Area("volcano");
            Assert.IsFalse(sim.Occupancy.IsOccupied(a, 5, 38));
            Assert.AreEqual("Blocked", sim.PlaceReason("volcano", "gathering_stone", 5, 38));
            Assert.AreEqual("Blocked", sim.PlaceReason("volcano", "kiln", 5, 42));
            // anyZone 1x1 inside volcano's centre (noBuild) is fine
            Assert.IsNull(sim.PlaceReason("volcano", "gathering_stone", 40, 40));
            Assert.AreEqual("Water only", sim.PlaceReason("fishing", "algae_farm", 5, 30));
            Assert.IsNull(sim.PlaceReason("fishing", "algae_farm", 40, 40));
            Assert.AreEqual("Only one Ascension Gate", PlaceGate(sim));
        }

        static string PlaceGate(Simulation sim)
        {
            Assert.IsNotNull(sim.PlaceGhost("volcano", "ascension_gate", 10, 60));
            return sim.PlaceReason("volcano", "ascension_gate", 10, 70);
        }

        [Test]
        public void Ghost_CompletesExactlyWhenCostPaid()
        {
            var sim = M3Util.NoSpawnCenter(out _);
            int placed = 0, completed = 0; var sfx = new List<string>();
            sim.Events.BuildingPlaced += (a, b) => placed++;
            sim.Events.BuildingCompleted += (a, b) => completed++;
            sim.Events.SoundRequested += (n, a) => sfx.Add(n);
            var g = sim.PlaceGhost("center", "workbench", 30, 30);
            Assert.IsNotNull(g); Assert.IsFalse(g.built); Assert.AreEqual(1, placed);
            Assert.IsTrue(sim.Occupancy.IsOccupied(sim.State.Area("center"), 31, 31), "ghosts occupy");
            sim.Hand.Add("stone", 2);
            sim.Hand.Add("wood", 10);
            var r = M3Util.Click(sim, "center", g);
            Assert.AreEqual(DropResultKind.Reordered, r.kind);
            Assert.AreEqual("wood", sim.Hand.Front);
            for (int i = 0; i < 7; i++)
            {
                r = M3Util.Click(sim, "center", g);
                Assert.AreEqual(DropResultKind.Fed, r.kind); Assert.AreEqual(g.id, r.buildingId);
            }
            Assert.IsFalse(g.built);
            Assert.AreEqual(1, sim.BuildingNeeds(g).Get("wood"));
            M3Util.Click(sim, "center", g);
            Assert.IsTrue(g.built);
            Assert.AreEqual(1, completed);
            Assert.AreEqual(1, sim.State.stats.buildingsBuilt);
            CollectionAssert.Contains(sim.State.builtTypes, "workbench");
            CollectionAssert.Contains(sfx, "build");
            Assert.AreEqual(2, sim.Hand.Count("wood"));
            // built workbench: wood now goes into Plank stock
            r = M3Util.Click(sim, "center", g);
            Assert.AreEqual(DropResultKind.Fed, r.kind);
            Assert.AreEqual(1, g.stock.Get("wood"));
            Assert.AreEqual(1, completed);
        }

        [Test]
        public void BurnerRack_OccupiesOnlyOnceBuilt()
        {
            var sim = M3Util.Volcano(out _);
            var a = sim.State.Area("volcano");
            var k = sim.PlaceGhost("volcano", "kiln", 5, 40);
            Assert.IsFalse(sim.Occupancy.IsOccupied(a, 6, 37));
            sim.Hand.Add("wood", 10); sim.Hand.Add("clay", 5);
            for (int i = 0; i < 20 && !k.built; i++) M3Util.Click(sim, "volcano", k);
            Assert.IsTrue(k.built);
            Assert.IsTrue(sim.Occupancy.IsOccupied(a, 6, 37));
            var snap = sim.Occupancy.Snapshot(a);
            CollectionAssert.AreEqual(sim.Occupancy.Recompute(a), snap);
        }

        [Test]
        public void Demolish_Refunds()
        {
            var sim = M3Util.NoSpawnCenter(out _);
            var c = sim.State.Area("center");
            c.ground.Clear();
            int demolished = 0; sim.Events.BuildingDemolished += (a, b) => demolished++;
            // ghost: only what was paid
            var g = sim.PlaceGhost("center", "paper_mill", 30, 30);
            sim.Hand.Add("wood", 3);
            for (int i = 0; i < 3; i++) M3Util.Click(sim, "center", g);
            Assert.IsTrue(sim.Demolish("center", g.id));
            Assert.AreEqual(3, c.ground.Count);
            Assert.IsTrue(c.ground.All(x => x.item == "wood" && x.manualAt > 0), "manual-tagged");
            c.ground.Clear();
            // starter workbench: full cost + stock + the running batch; its lantern link is severed
            var bench = c.buildings.First(b => b.type == "workbench");
            bench.stock = new ItemCounts(); bench.stock.Set("wood", 5);
            bench.smeltDoneAt = sim.Clock().NowMs + 500;
            var lanT = c.buildings.First(b => b.type == "wisp_lantern" && b.links.Any(l => l.to == bench.id));
            Assert.IsTrue(sim.Demolish("center", bench.id));
            Assert.AreEqual(8 + 5 + 3, SimTestUtil.CountGround(c, "wood"));
            Assert.IsFalse(lanT.links.Any(l => l.to == bench.id || l.from == bench.id));
            Assert.AreEqual(5, lanT.links.Count);
            Assert.IsNull(c.BuildingById(bench.id));
            c.ground.Clear();
            // storehouse contents; burner fuel NOT refunded
            var sh = c.buildings.First(b => b.type == "storehouse" && b.item == "stone");
            sh.qty = 7;
            sim.Demolish("center", sh.id);
            Assert.AreEqual(12, SimTestUtil.CountGround(c, "wood"));
            Assert.AreEqual(7, SimTestUtil.CountGround(c, "stone"));
            c.ground.Clear();
            var kiln = c.buildings.First(b => b.type == "kiln");
            sim.Fuel.AddItem(kiln, "charcoal");
            sim.Demolish("center", kiln.id);
            Assert.AreEqual(0, SimTestUtil.CountGround(c, "charcoal"));
            Assert.AreEqual(10, SimTestUtil.CountGround(c, "wood"));
            Assert.AreEqual(5, SimTestUtil.CountGround(c, "clay"));
            Assert.AreEqual(4, demolished);
        }

        [Test]
        public void Storehouse_Seal_Stone_HandFeedAndWithdraw()
        {
            var sim = M3Util.NoSpawnCenter(out _);
            var c = sim.State.Area("center");
            var sh = c.buildings.First(b => b.type == "storehouse" && b.item == "stone");
            sim.Hand.Add("wood", 2); sim.Hand.Add("stone", 3);
            Assert.AreEqual(DropResultKind.Reordered, M3Util.Click(sim, "center", sh).kind);
            Assert.AreEqual(DropResultKind.Deposited, M3Util.Click(sim, "center", sh).kind);
            Assert.AreEqual(1, sh.qty);
            Assert.AreEqual(1, sim.Withdraw("center", sh.id, 5));
            Assert.AreEqual("stone", sh.item, "locked starter store keeps its type");
            // seal: tuning destroys contents
            var seal = c.buildings.First(b => b.type == "warding_seal" && b.item == "wood");
            seal.qty = 4;
            sim.Hand.MoveToFront("stone");
            var r = M3Util.Click(sim, "center", seal);
            Assert.AreEqual(DropResultKind.Configured, r.kind);
            Assert.AreEqual(("stone", 0, true), (seal.item, seal.qty, seal.locked));
            Assert.AreEqual(3, sim.Hand.Count("stone"), "tuning consumes nothing");
            // gathering stone accepts anything (no _accEver yet)
            var gs = c.buildings.First(b => b.type == "gathering_stone");
            Assert.AreEqual(DropResultKind.Fed, M3Util.Click(sim, "center", gs).kind);
            Assert.AreEqual(1, BuildingSystem.GatherTotal(gs));
            Assert.AreEqual(1, sim.Withdraw("center", gs.id));
            // lantern has no feed behaviour: falls to the ground drop unless noGround
            var lan = c.buildings.First(b => b.type == "wisp_lantern");
            Assert.IsNull(M3Util.Click(sim, "center", lan, noGround: true));
            Assert.AreEqual(DropResultKind.Dropped, M3Util.Click(sim, "center", lan, noGround: false).kind);
        }

        [Test]
        public void GenBuilding_DropsImmediately_ThenOnInterval()
        {
            var sim = M3Util.Volcano(out var clock);
            var a = sim.State.Area("volcano");
            var hg = sim.Buildings.PlaceBuilt("volcano", "herb_garden", 5, 40, starter: false);
            sim.Tick();
            Assert.AreEqual(1, SimTestUtil.CountGround(a, "spirit_herb"));
            Assert.IsTrue(a.ground[0].crafted);
            // 2500 ms × 0.2 = 500 ms
            for (int i = 0; i < 10; i++) { clock.Advance(50); sim.Tick(); }
            Assert.AreEqual(2, SimTestUtil.CountGround(a, "spirit_herb"));
            for (int i = 0; i < 400; i++) { clock.Advance(50); sim.Tick(); }
            Assert.AreEqual(12, SimTestUtil.CountGround(a, "spirit_herb"), "output pile caps it at 12");
            Assert.IsTrue(sim.BuildingStatus("volcano", hg).pile);
        }

        [Test]
        public void FurnaceSpirit_StokesBestFuelFirst()
        {
            var sim = M3Util.Volcano(out var clock);
            var kiln = sim.Buildings.PlaceBuilt("volcano", "kiln", 5, 40, starter: false);
            var sp = sim.Buildings.PlaceBuilt("volcano", "furnace_spirit", 5, 44, starter: false);
            sim.Hand.Add("wood", 2); sim.Hand.Add("charcoal", 1); sim.Hand.Add("stone", 1);
            sim.Hand.MoveToFront("stone");
            Assert.AreEqual(DropResultKind.Reordered, M3Util.Click(sim, "volcano", sp).kind);
            for (int i = 0; i < 10 && sim.Hand.Count("wood") + sim.Hand.Count("charcoal") > 0; i++) M3Util.Click(sim, "volcano", sp);
            Assert.AreEqual(3, BuildingSystem.GatherTotal(sp));
            sim.Tick();
            Assert.AreEqual("charcoal", sim.Fuel.Queue(kiln)[0].item);
            clock.Advance(50); sim.Tick();
            clock.Advance(50); sim.Tick();
            Assert.AreEqual(3, sim.Fuel.Queue(kiln).Count);
            Assert.AreEqual(0, BuildingSystem.GatherTotal(sp));
        }
    }

    static class SimExt
    {
        public static ManualClock Clock(this Simulation sim) => (ManualClock)sim.Ctx.Clock;
    }
}
