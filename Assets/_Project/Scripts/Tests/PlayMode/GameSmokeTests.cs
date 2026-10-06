using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleGrounds.Game;
using IdleGrounds.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IdleGrounds.PlayMode.Tests
{
    /// <summary>
    /// End-to-end smoke tests: load Game.unity with a FRESH save and drive the real scene. The user's real
    /// save (persistentDataPath/idle-grounds-save.json) is backed up byte-for-byte before each test and
    /// restored after it; SaveService / GameRunner are destroyed before the restore so nothing
    /// (autosave, OnApplicationQuit) can overwrite it again.
    /// </summary>
    public class GameSmokeTests
    {
        const string SceneName = "Game";
        const string Center = "center", Farm = "farm";

        byte[] savedBytes, savedTmpBytes;

        // ------------------------------------------------------------------ harness

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            string p = SaveService.SavePath;
            savedBytes = File.Exists(p) ? File.ReadAllBytes(p) : null;
            savedTmpBytes = File.Exists(p + ".tmp") ? File.ReadAllBytes(p + ".tmp") : null;
            DeleteSave();
            yield return LoadGame();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // stop every writer first, then put the user's file back
            if (SaveService.Instance != null) Object.Destroy(SaveService.Instance.gameObject);
            if (GameRunner.Instance != null) Object.Destroy(GameRunner.Instance.gameObject);
            yield return null;
            DeleteSave();
            string p = SaveService.SavePath;
            if (savedBytes != null) File.WriteAllBytes(p, savedBytes);
            if (savedTmpBytes != null) File.WriteAllBytes(p + ".tmp", savedTmpBytes);
        }

        static void DeleteSave()
        {
            string p = SaveService.SavePath;
            if (File.Exists(p)) File.Delete(p);
            if (File.Exists(p + ".tmp")) File.Delete(p + ".tmp");
        }

        static IEnumerator LoadGame()
        {
            var op = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, "scene '" + SceneName + "' must be in Build Settings");
            while (!op.isDone) yield return null;
            for (int i = 0; i < 3; i++) yield return null;     // Awake/Start + a few LateUpdate view syncs
        }

        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        static Simulation Sim => GameRunner.Instance.Sim;
        static GameState S => GameRunner.Instance.State;

        /// <summary>Free spot for a building (sim placement rules).</summary>
        static (int r, int c) FindSpot(string area, string type)
        {
            for (int r = 20; r < 80; r += 2)
                for (int c = 20; c < 80; c += 2)
                    if (Sim.PlaceReason(area, type, r, c) == null) return (r, c);
            Assert.Fail("no free spot for " + type + " in " + area);
            return (0, 0);
        }

        /// <summary>Hand-feed one item type into a building, like repeated right-clicks.</summary>
        static void Feed(string area, Building b, string item, int n)
        {
            var (x, y) = Sim.World.BuildingCenterPx(b);
            int guard = 0;
            while (n > 0 && guard++ < 200)
            {
                int add = Mathf.Min(n, Sim.Hand.Space());
                Assert.Greater(add, 0, "hand has no room");
                Sim.Hand.Add(item, add);
                Sim.Hand.MoveToFront(item);
                for (int k = 0; k < 100 && Sim.Hand.Count(item) > 0; k++)
                {
                    if (Sim.DropFromHand(area, x, y, true) == null) break;
                }
                int left = Sim.Hand.Count(item);
                n -= add - left;
                if (left > 0) { Sim.Hand.Take(item, left); break; }   // refused: building doesn't want more
            }
        }

        static Building BuildViaHand(string area, string type)
        {
            var (r, c) = FindSpot(area, type);
            var b = Sim.PlaceGhost(area, type, r, c);
            Assert.IsNotNull(b, "PlaceGhost " + type + " (" + Sim.PlaceReason(area, type, r, c) + ")");
            Assert.IsFalse(b.built);
            foreach (var cost in Sim.Config.Building(type).cost) Feed(area, b, cost.item, cost.qty);
            return b;
        }

        // ------------------------------------------------------------------ 1. boot

        [UnityTest, Timeout(60000)]
        public IEnumerator Boot_SceneLoads_WorldViewsExist()
        {
            Assert.IsNotNull(GameRunner.Instance, "GameRunner");
            Assert.IsNotNull(Sim, "Sim");
            Assert.IsTrue(GameRunner.Instance.FreshRun, "fresh save expected");
            yield return Wait(1.5f);

            var c = S.Area(Center);
            Assert.IsTrue(GameRunner.Instance.IsUnlocked(Center));
            Assert.IsNotNull(GameRunner.Instance.IslandObject(Center), "Center Island object");
            var altar = c.buildings.FirstOrDefault(b => b.type == "center");
            var dragon = c.buildings.FirstOrDefault(b => b.type == "dragon");
            Assert.IsNotNull(altar, "Altar");
            Assert.IsNotNull(dragon, "Dragon");
            int starter = c.buildings.Count(b => b.starter);
            Assert.Greater(starter, 10, "starter network");

            var bvs = Object.FindFirstObjectByType<BuildingViewSync>();
            Assert.IsNotNull(bvs, "BuildingViewSync");
            Assert.IsNotNull(bvs.Find(Center, altar.id), "Altar view");
            Assert.IsNotNull(bvs.Find(Center, dragon.id), "Dragon view");
            int starterViews = c.buildings.Count(b => b.starter && bvs.Find(Center, b.id) != null);
            Assert.AreEqual(starter, starterViews, "every starter building has a view");

            LogAssert.NoUnexpectedReceived();
        }

        // ------------------------------------------------------------------ 2. core loop

        [UnityTest, Timeout(60000)]
        public IEnumerator CoreLoop_HarvestAndVacuum_FillsHand()
        {
            var hand = Object.FindFirstObjectByType<HandController>();
            var world = Object.FindFirstObjectByType<WorldViewSync>();
            Assert.IsNotNull(hand, "HandController");
            Assert.IsNotNull(world, "WorldViewSync");
            var space = GameRunner.Instance.Space;
            var area = S.Area(Center);

            var tree = area.nodes.FirstOrDefault(n => n.kind == "spirittree");
            Assert.IsNotNull(tree, "Spirit Tree");
            long gatheredBefore = S.stats.totalGathered;

            // hold-harvest the tree through the hand controller
            hand.DebugPointAt(space.PxToWorld(Center, (tree.col + tree.size / 2.0) * 32, (tree.row + tree.size / 2.0) * 32));
            hand.DebugLeftDown();
            yield return Wait(3f);
            hand.DebugLeftUp();
            Assert.Greater(area.ground.Count, 0, "ground items exist");

            // vacuum the nearest ground item
            Sim.Hand.Take("wood", 1000);
            var g = area.ground.OrderBy(x => x.x * x.x + x.y * x.y).First();
            hand.DebugPointAt(space.PxToWorld(Center, g.x, g.y));
            hand.DebugLeftDown();
            yield return Wait(1.5f);
            hand.DebugLeftUp();
            hand.ReleaseDebugCursor();

            Assert.Greater(Sim.Hand.Total(), 0, "hand has items after vacuuming");
            yield return null;
            Assert.Greater(world.GroundViewCount, 0, "ground item views");
            Assert.Greater(world.NodeViewCount, 0, "node views");
        }

        // ------------------------------------------------------------------ 3. logistics

        [UnityTest, Timeout(60000)]
        public IEnumerator Logistics_WispLaunches_ConverterCompletes()
        {
            int launches = 0;
            Sim.Events.WispLaunched += (a, w) => launches++;
            var wispViews = Object.FindFirstObjectByType<WispViewSync>();
            Assert.IsNotNull(wispViews, "WispViewSync");

            bool sawWispView = false, crafted = false;
            float end = Time.realtimeSinceStartup + 18f;
            while (Time.realtimeSinceStartup < end && !(launches > 0 && sawWispView && crafted))
            {
                if (wispViews.ViewCount > 0) sawWispView = true;
                if (S.stats.totalCrafted > 0) crafted = true;
                yield return null;
            }
            Assert.IsTrue(launches > 0 || S.Area(Center).wisps.Count > 0, "a wisp launched");
            Assert.IsTrue(sawWispView, "a wisp view appeared");
            Assert.IsTrue(crafted, "a converter batch completed (stats.totalCrafted=" + S.stats.totalCrafted + ")");
        }

        // ------------------------------------------------------------------ 4. build

        [UnityTest, Timeout(60000)]
        public IEnumerator Build_WispLantern_FedFromHand_Completes()
        {
            S.quest.idx = Sim.Config.quests.Count;     // reveal everything
            int before = S.Area(Center).buildings.Count;
            var b = BuildViaHand(Center, "wisp_lantern");
            Assert.IsTrue(b.built, "ghost completes once fed");
            Assert.AreEqual(before + 1, S.Area(Center).buildings.Count);
            yield return null; yield return null;
            var bvs = Object.FindFirstObjectByType<BuildingViewSync>();
            var v = bvs.Find(Center, b.id);
            Assert.IsNotNull(v, "view for the new lantern");
            Assert.AreEqual("wisp_lantern", v.Building.type);
        }

        // ------------------------------------------------------------------ 5. save round-trip

        [UnityTest, Timeout(60000)]
        public IEnumerator Save_Reload_RestoresHandAndBuildings()
        {
            S.quest.idx = Sim.Config.quests.Count;
            BuildViaHand(Center, "wisp_lantern");
            Sim.Hand.Take("wood", 1000); Sim.Hand.Take("stone", 1000);
            Sim.Hand.Add("wood", 3); Sim.Hand.Add("stone", 2);

            // snapshot + save in the same frame so nothing can tick between them
            int buildings = S.Area(Center).buildings.Count;
            int wood = Sim.Hand.Count("wood"), stone = Sim.Hand.Count("stone");
            Assert.IsNotNull(SaveService.Instance, "SaveService");
            Assert.IsTrue(SaveService.Instance.Save(), "save: " + SaveService.Instance.LastError);
            Assert.IsTrue(File.Exists(SaveService.SavePath));

            yield return LoadGame();

            Assert.IsFalse(GameRunner.Instance.FreshRun, "reload should read the save");
            Assert.AreEqual(buildings, S.Area(Center).buildings.Count, "building count");
            Assert.AreEqual(wood, Sim.Hand.Count("wood"), "hand wood");
            Assert.AreEqual(stone, Sim.Hand.Count("stone"), "hand stone");
        }

        // ------------------------------------------------------------------ 6. island + bridge

        [UnityTest, Timeout(60000)]
        public IEnumerator Island_UnlockFarm_PairBridges_SkyWispLaunches()
        {
            S.quest.idx = Sim.Config.quests.Count;
            // pay the Farm unlock from the hand
            for (int i = 0; i < 20 && !Sim.World.IsAreaUnlocked(Farm); i++)
            {
                var rem = Sim.UnlockRemaining(Farm);
                foreach (var e in rem) Sim.Hand.Add(e.item, Mathf.Min(e.qty, Sim.Hand.Space()));
                Sim.UnlockArea(Farm);
            }
            Assert.IsTrue(Sim.World.IsAreaUnlocked(Farm), "Farm unlocked");
            yield return null; yield return null;
            Assert.IsTrue(GameRunner.Instance.IsUnlocked(Farm));
            Assert.IsTrue(Sim.IsBuildingUnlocked("spirit_bridge"), "bridge revealed with two Islands");

            var a = BuildViaHand(Center, "spirit_bridge");
            var b = BuildViaHand(Farm, "spirit_bridge");
            Assert.IsTrue(a.built && b.built, "both bridges built");
            Assert.IsNull(Sim.PairBridges(Center, a.id, Farm, b.id), "pairing refused");
            Assert.AreSame(b, Sim.BridgePartner(Center, a));

            var sender = a.pairSends ? a : b;
            sender.inv ??= new List<HandStack>();
            sender.inv.Add(new HandStack("wood", 3));

            var sky = Object.FindFirstObjectByType<SkyWispViewSync>();
            Assert.IsNotNull(sky, "SkyWispViewSync");
            bool viewSeen = false;
            float end = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < end && !(Sim.SkyWisps.Count > 0 && viewSeen))
            {
                if (sky.ViewCount > 0) viewSeen = true;
                yield return null;
            }
            Assert.Greater(Sim.SkyWisps.Count, 0, "a sky wisp launched");
            Assert.IsTrue(viewSeen, "sky wisp view appeared");
        }
    }
}
