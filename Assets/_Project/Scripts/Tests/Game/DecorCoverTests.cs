using IdleGrounds.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace IdleGrounds.Game.Tests
{
    /// <summary>DecorCover: flower / scatter decor under a building footprint is hidden on cover and put back (tile, flip, colour) on uncover; paths stay.</summary>
    public class DecorCoverTests
    {
        GameObject islandGo;
        Island island;
        Tilemap flowers, scatter, path;
        Tile tile;

        static Tilemap Map(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<Tilemap>();
        }

        // island cell (row, col) -> dressing tilemap cell (col, -row - 1), as IslandDressingBuilder lays them out
        static Vector3Int C(int row, int col) => new Vector3Int(col, -row - 1, 0);

        [SetUp]
        public void SetUp()
        {
            islandGo = new GameObject("TestIsland", typeof(Grid));
            islandGo.transform.position = new Vector3(100f, 50f, 0f);
            island = islandGo.AddComponent<Island>();
            island.islandKey = "test";
            var dressing = new GameObject(DecorCover.DressingRoot).transform;
            dressing.SetParent(islandGo.transform, false);
            flowers = Map(dressing, "Flowers");
            scatter = Map(dressing, "Scatter");
            path = Map(dressing, "Path");
            tile = ScriptableObject.CreateInstance<Tile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(islandGo);
            Object.DestroyImmediate(tile);
        }

        [Test]
        public void Cover_HidesFlowersAndScatterInFootprint_UncoverRestores()
        {
            flowers.SetTile(C(10, 20), tile);
            flowers.SetTileFlags(C(10, 20), TileFlags.None);
            var flip = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
            flowers.SetTransformMatrix(C(10, 20), flip);
            scatter.SetTile(C(12, 22), tile);
            flowers.SetTile(C(10, 23), tile);          // one cell right of a 3x3 footprint at col 20..22
            path.SetTile(C(11, 21), tile);
            var cover = new DecorCover(k => k == "test" ? island : null);

            cover.Cover(1L, "test", 20, 10, 3, 3);
            Assert.IsNull(flowers.GetTile(C(10, 20)), "flower inside the footprint hidden");
            Assert.IsNull(scatter.GetTile(C(12, 22)), "scatter inside the footprint hidden");
            Assert.IsNotNull(flowers.GetTile(C(10, 23)), "flower outside the footprint stays");
            Assert.IsNotNull(path.GetTile(C(11, 21)), "paths are never touched");
            Assert.AreEqual(2, cover.HiddenTiles);
            Assert.IsTrue(cover.IsCovered("test", 11, 21));

            cover.Cover(1L, "test", 20, 10, 3, 3);     // re-bind, same footprint: no-op
            Assert.AreEqual(2, cover.HiddenTiles);

            cover.Uncover(1L);
            Assert.AreSame(tile, flowers.GetTile(C(10, 20)), "flower restored");
            Assert.AreEqual(flip, flowers.GetTransformMatrix(C(10, 20)), "flip restored");
            Assert.AreSame(tile, scatter.GetTile(C(12, 22)), "scatter restored");
            Assert.AreEqual(0, cover.HiddenTiles);
            Assert.IsFalse(cover.IsCovered("test", 11, 21));
            cover.Uncover(1L);                          // twice: harmless
        }

        [Test]
        public void OverlappingCovers_RestoreOnlyWhenLastLeaves()
        {
            flowers.SetTile(C(5, 5), tile);
            var cover = new DecorCover(k => island);
            cover.Cover(1L, "test", 5, 5, 1, 1);        // building B placed on the cells first ...
            cover.Cover(2L, "test", 4, 4, 2, 2);        // ... demolished building A's view released after it in the same frame
            cover.Uncover(2L);
            Assert.IsNull(flowers.GetTile(C(5, 5)), "still under building 1");
            cover.Uncover(1L);
            Assert.AreSame(tile, flowers.GetTile(C(5, 5)));
        }

        [Test]
        public void MovedFootprint_RestoresOldCells()
        {
            flowers.SetTile(C(1, 1), tile);
            flowers.SetTile(C(8, 8), tile);
            var cover = new DecorCover(k => island);
            cover.Cover(7L, "test", 1, 1, 1, 1);
            cover.Cover(7L, "test", 8, 8, 1, 1);         // same key re-bound to another building
            Assert.AreSame(tile, flowers.GetTile(C(1, 1)));
            Assert.IsNull(flowers.GetTile(C(8, 8)));
            cover.Clear();
            Assert.AreSame(tile, flowers.GetTile(C(8, 8)));
        }

        [Test]
        public void NoIslandOrNoDressing_IsHarmless()
        {
            var cover = new DecorCover(k => null);
            cover.Cover(1L, "nowhere", 0, 0, 3, 3);
            cover.Uncover(1L);
            Assert.AreEqual(0, cover.HiddenTiles);
        }
    }
}
