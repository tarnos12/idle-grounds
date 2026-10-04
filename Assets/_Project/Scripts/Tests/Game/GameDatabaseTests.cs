using System.IO;
using System.Linq;
using IdleGrounds.Game.Data;
using IdleGrounds.Sim;
using NUnit.Framework;
using UnityEditor;

namespace IdleGrounds.Game.Tests
{
    public class GameDatabaseTests
    {
        const string DbPath = "Assets/_Project/Data/GameDatabase.asset";
        const string JsonPath = "Assets/_Project/Data/Source/game-data.json";

        GameDatabase db;
        GameConfig built, json;

        [SetUp]
        public void SetUp()
        {
            db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DbPath);
            Assert.IsNotNull(db, "GameDatabase.asset missing - run Idle Grounds/Data/Import From JSON");
            built = db.BuildConfig();
            json = GameConfigJson.Load(File.ReadAllText(JsonPath));
        }

        [Test]
        public void BuildConfig_Validates()
        {
            var errs = built.Validate();
            Assert.That(errs, Is.Empty, string.Join("\n", errs));
        }

        [Test]
        public void Counts_MatchJson()
        {
            Assert.AreEqual(json.items.Count, built.items.Count, "items");
            Assert.AreEqual(json.regions.Count, built.regions.Count, "regions");
            Assert.AreEqual(json.buildings.Count, built.buildings.Count, "buildings");
            Assert.AreEqual(json.buildings.Sum(b => b.recipes.Count), built.buildings.Sum(b => b.recipes.Count), "recipes");
            Assert.AreEqual(json.upgradeTree.Count, built.upgradeTree.Count, "upgrades");
            Assert.AreEqual(json.quests.Count, built.quests.Count, "quests");
            Assert.AreEqual(json.perks.Count, built.perks.Count, "perks");
            Assert.AreEqual(json.vows.Count, built.vows.Count, "vows");
            Assert.AreEqual(json.dragonStages.Count, built.dragonStages.Count, "dragon stages");
            Assert.AreEqual(json.dragonBuffs.Count, built.dragonBuffs.Count, "dragon buffs");
            Assert.AreEqual(json.zones.Count, built.zones.Count, "zones");
            Assert.AreEqual(json.reveal.Count, built.reveal.Count, "reveal");
        }

        [Test]
        public void Order_MatchesJson()
        {
            CollectionAssert.AreEqual(json.items.Select(i => i.key).ToArray(), built.items.Select(i => i.key).ToArray());
            CollectionAssert.AreEqual(json.regions.Select(i => i.key).ToArray(), built.regions.Select(i => i.key).ToArray());
            CollectionAssert.AreEqual(json.buildings.Select(i => i.key).ToArray(), built.buildings.Select(i => i.key).ToArray());
            CollectionAssert.AreEqual(json.quests.Select(i => i.id).ToArray(), built.quests.Select(i => i.id).ToArray());
            CollectionAssert.AreEqual(json.upgradeTree.Select(i => i.id).ToArray(), built.upgradeTree.Select(i => i.id).ToArray());
        }

        [Test]
        public void EveryItemAndBuilding_HasIcon()
        {
            foreach (var a in db.items) Assert.IsNotNull(a.icon, "item icon " + a.def.key);
            foreach (var a in db.buildings) Assert.IsNotNull(a.icon, "building icon " + a.def.key);
            Assert.IsNotNull(db.missingSprite);
        }
    }
}
