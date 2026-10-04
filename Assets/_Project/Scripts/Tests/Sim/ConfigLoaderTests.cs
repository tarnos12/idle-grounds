using System.Collections.Generic;
using System.Linq;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    public class ConfigLoaderTests
    {
        [Test]
        public void LoadsGameData_AndValidatesClean()
        {
            var cfg = SimTestUtil.LoadConfig();
            var errs = cfg.Validate();
            Assert.That(errs, Is.Empty, string.Join("\n", errs));
        }

        [Test]
        public void Counts_MatchDataJs()
        {
            var cfg = SimTestUtil.LoadConfig();
            Assert.AreEqual(46, cfg.items.Count, "items");
            Assert.AreEqual(7, cfg.regions.Count, "regions");
            Assert.AreEqual(26, cfg.buildings.Count, "buildings");
            Assert.AreEqual(28, cfg.buildings.Sum(b => b.recipes.Count), "recipes");
            Assert.AreEqual(14, cfg.quests.Count, "quests");
            Assert.AreEqual(19, cfg.upgradeTree.Count, "upgrade nodes");
            Assert.AreEqual(15, cfg.perks.Count, "perks");
            Assert.AreEqual(4, cfg.vows.Count, "vows");
            Assert.AreEqual(4, cfg.dragonStages.Count, "dragon stages");
            Assert.AreEqual(4, cfg.dragonBuffs.Count, "dragon buffs");
            Assert.AreEqual(21, cfg.reveal.Count, "reveal rules");
            CollectionAssert.AreEqual(new[] { "center", "farm", "mine", "fishing", "volcano", "grove", "celestial" },
                cfg.regions.Select(r => r.key).ToArray());
        }

        [Test]
        public void Scalars_AndFuelOrder()
        {
            var cfg = SimTestUtil.LoadConfig();
            Assert.AreEqual(20, cfg.balance.handCap);
            Assert.AreEqual(6, cfg.balance.fuelSlots);
            Assert.AreEqual(2, cfg.balance.questChain);
            Assert.AreEqual(52, cfg.balance.versionNum);
            CollectionAssert.AreEqual(new[] { 2, 6, 20 }, cfg.balance.automationClicks);
            CollectionAssert.AreEqual(new[] { 1, 1.15, 1.3, 1.5, 1.75 }, cfg.balance.vowMult);
            Assert.IsTrue(cfg.test.enabled);
            Assert.AreEqual(0.2, cfg.test.timeScale, 1e-12);
            Assert.AreEqual(0.5, cfg.test.costScale, 1e-12);
            CollectionAssert.AreEqual(new[] { "wood", "bamboo", "charcoal", "firestone" }, cfg.FuelKeys.ToArray());
            Assert.AreEqual(120000, cfg.FuelMs("firestone"));
            Assert.AreEqual(32, cfg.grid.cell);
            Assert.AreEqual(93, cfg.grid.cells);
        }

        [Test]
        public void Regions_WorldAndContent()
        {
            var cfg = SimTestUtil.LoadConfig();
            var c = cfg.Region("center");
            Assert.AreEqual((1, 0), (c.rx, c.ry));
            Assert.IsEmpty(c.unlockCost);
            CollectionAssert.AreEqual(new[] { "corners", "midTop" }, c.noBuild);
            Assert.AreEqual("bush", c.spawners[0].kind);
            Assert.IsFalse(c.spawners[0].scaleWithArea);
            Assert.AreEqual(6, c.spawners[0].spacing);
            Assert.AreEqual(NodeInteraction.Chop, c.spawners[0].interaction);
            Assert.AreEqual(2, c.fixtures.Count);
            Assert.AreEqual(2, c.fixtures[1].dropMin);
            Assert.IsTrue(c.fixtures[1].autoTap);
            Assert.AreEqual("quarry", c.generators[1].upgrade);
            Assert.AreEqual(0.08, c.generators[1].rareDrop.chance, 1e-12);
            Assert.IsTrue(c.enemies.enabled);
            Assert.IsTrue(c.enemies.baitSpawn.enabled);
            Assert.AreEqual(8, c.enemies.baitSpawn.hp);

            var grove = cfg.Region("grove");
            CollectionAssert.AreEqual(new[] { "wheat", "wood" }, grove.unlockCost.Select(q => q.item).ToArray());
            CollectionAssert.AreEqual(new[] { "centre" }, cfg.Region("mine").noBuild);
            Assert.AreEqual(3, cfg.Region("fishing").surfaceWindow);
            var mine = cfg.Region("mine");
            Assert.IsTrue(mine.spawners[0].useTiers);
            Assert.AreEqual(2, mine.tiers[0].hits);
            Assert.AreEqual(3, mine.tiers[0].drops[0].max);
            Assert.IsTrue(mine.spawners[0].scaleWithArea);
        }

        [Test]
        public void Buildings_Shapes()
        {
            var cfg = SimTestUtil.LoadConfig();
            var kiln = cfg.Building("kiln");
            Assert.AreEqual((3, 5), (kiln.sizeW, kiln.sizeH));
            Assert.IsTrue(kiln.fuel);
            Assert.AreEqual(3, kiln.recipes.Count);
            Assert.AreEqual(-1, kiln.stageUnlock);
            Assert.AreEqual(1, cfg.Building("forge").stageUnlock);
            Assert.AreEqual((3, 3), cfg.BuildingSize("workbench"));
            Assert.AreEqual(200, cfg.Building("storehouse").cap);
            Assert.IsTrue(cfg.Building("gathering_stone").gather.enabled);
            Assert.AreEqual(60, cfg.Building("gathering_stone").gather.cap);
            var pav = cfg.Building("meditation_pavilion").roster;
            Assert.IsTrue(pav.enabled);
            CollectionAssert.AreEqual(new[] { "spirit_buns", "spirit_wine" }, pav.foodValues.Select(f => f.item).ToArray());
            Assert.IsTrue(cfg.Building("algae_farm").waterOnly);
            Assert.AreEqual(24, cfg.Building("algae_farm").gen.cap);
            Assert.IsTrue(cfg.Building("ascension_gate").gate);
            Assert.IsTrue(cfg.Building("center").indestructible);
            var bench = cfg.Building("workbench").recipes[1];
            CollectionAssert.AreEqual(new[] { "plank", "iron_bar" }, bench.inputs.Select(i => i.item).ToArray());
        }

        [Test]
        public void QuestGoals_Translated()
        {
            var cfg = SimTestUtil.LoadConfig();
            QuestDef Q(string id) => cfg.quests.First(q => q.id == id);
            Assert.AreEqual(QuestGoalKind.HandCount, Q("wood").goalKind);
            Assert.AreEqual("wood", Q("wood").goalItem);
            Assert.AreEqual(5, Q("wood").goalNeed);
            Assert.AreEqual(QuestGoalKind.DragonStageAtLeast, Q("dragon2").goalKind);
            Assert.AreEqual(2, Q("dragon2").goalStage);
            Assert.AreEqual(QuestGoalKind.StatAtLeast, Q("fox").goalKind);
            Assert.AreEqual("foxKills", Q("fox").goalStat);
            Assert.AreEqual(QuestGoalKind.AnyRegionUnlocked, Q("explore").goalKind);
            CollectionAssert.AreEqual(new[] { "farm", "mine", "fishing" }, Q("explore").goalRegions);
            CollectionAssert.AreEqual(new[] { "fishing" }, Q("waters").goalRegions);
            Assert.AreEqual(QuestGoalKind.DragonTributeItem, Q("iron").goalKind);
            Assert.AreEqual(2, Q("iron").goalStage);
            Assert.AreEqual("iron_bar", Q("iron").goalItem);
            Assert.AreEqual(QuestGoalKind.HandCountCapped, Q("weaver").goalKind);
            Assert.AreEqual(8, Q("weaver").goalNeed);
            CollectionAssert.AreEqual(new[] { "rope:2", "cloth:6" }, Q("weaver").goalItems.Select(i => i.ToString()).ToArray());
            Assert.AreEqual("disciplesRecruited", Q("cultivate").goalStat);
            CollectionAssert.AreEqual(new[] { "wood" }, Q("upgrade").rewardItems.Select(i => i.item).ToArray());
            Assert.AreEqual("spirittree", Q("wood").target.id);
            foreach (var q in cfg.quests) Assert.AreNotEqual(QuestGoalKind.Unknown, q.goalKind, q.id);
        }

        [Test]
        public void Zones_MatchSpec()
        {
            var cfg = SimTestUtil.LoadConfig();
            var expect = new Dictionary<string, string>
            {
                { "cornerTL", "0..24,0..24" }, { "cornerTR", "0..24,68..92" }, { "cornerBL", "68..92,0..24" },
                { "cornerBR", "68..92,68..92" }, { "centre", "25..67,25..67" }, { "midTop", "0..24,25..67" },
                { "midLeft", "25..67,0..24" }, { "clayField", "76..84,76..84" }, { "quarryField", "76..84,8..16" },
                { "springField", "8..16,8..16" }, { "woodField", "12..20,40..48" }, { "sandField", "42..50,14..22" },
            };
            foreach (var kv in expect)
            {
                var rects = cfg.ZoneRects(kv.Key);
                Assert.AreEqual(1, rects.Count, kv.Key);
                Assert.AreEqual(kv.Value, rects[0].ToString(), kv.Key);
            }
            var corners = cfg.ZoneRects("corners").Select(r => r.ToString()).ToArray();
            CollectionAssert.AreEqual(new[] { "0..24,0..24", "0..24,68..92", "68..92,0..24", "68..92,68..92" }, corners);
        }

        [Test]
        public void Validate_ReportsUnknownItems()
        {
            var cfg = SimTestUtil.LoadConfig();
            cfg.buildings[2].cost.Add(new ItemQty("unobtainium", 1));
            cfg.regions[0].spawners[0].drops.Add(new DropSpec("nope", 1, 1));
            var errs = cfg.Validate();
            Assert.IsTrue(errs.Any(e => e.Contains("unobtainium")));
            Assert.IsTrue(errs.Any(e => e.Contains("'nope'")));
        }

        [Test]
        public void MiniJson_PreservesKeyOrder_AndParsesValues()
        {
            var o = (JsonObject)MiniJson.Parse("{\"b\":1,\"a\":[true,null,\"x\\u0041\"],\"c\":-2.5e1}");
            CollectionAssert.AreEqual(new[] { "b", "a", "c" }, o.Keys.ToArray());
            Assert.AreEqual(1.0, o["b"]);
            var a = (List<object>)o["a"];
            Assert.AreEqual(true, a[0]);
            Assert.IsNull(a[1]);
            Assert.AreEqual("xA", a[2]);
            Assert.AreEqual(-25.0, o["c"]);
        }
    }
}
