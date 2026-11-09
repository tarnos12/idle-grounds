using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleGrounds.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdleGrounds.Game.Tests
{
    /// <summary>ART-SPEC §3.10.3 auto tiles: dual-grid indices, sheet layout, side-face frames, overlay opening, and the sheets on disk
    /// (placeholders and delivered ones) sliced as the contract says with the template alpha.</summary>
    public class AutoTileTests
    {
        static HashSet<Vector2Int> Cells(params (int x, int y)[] c) => new HashSet<Vector2Int>(c.Select(p => new Vector2Int(p.x, p.y)));

        [Test]
        public void AutoTile_TwoByTwoBlock_GivesContractIndices()
        {
            var d = AutoTileGrid.Display(Cells((0, 0), (1, 0), (0, 1), (1, 1)));
            int[,] expect = { { 8, 12, 4 }, { 10, 15, 5 }, { 2, 3, 1 } };   // top row first
            Assert.AreEqual(9, d.Count);
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 3; i++)
                {
                    int j = 2 - row;
                    Assert.AreEqual(expect[row, i], d[new Vector2Int(i, j)], $"display ({i},{j})");
                }
        }

        [Test]
        public void AutoTile_DisplayMatchesIndexPredicate()
        {
            var data = Cells((0, 0), (1, 0), (3, 2), (2, 2), (2, 3), (5, 5));
            var d = AutoTileGrid.Display(data);
            for (int i = -1; i < 8; i++)
                for (int j = -1; j < 8; j++)
                {
                    int w = AutoTileGrid.Index((x, y) => data.Contains(new Vector2Int(x, y)), i, j);
                    Assert.AreEqual(w, d.TryGetValue(new Vector2Int(i, j), out int v) ? v : 0, $"({i},{j})");
                }
        }

        [Test]
        public void AutoTile_LayoutMatchesCornerGrid_AndCoversAll16()
        {
            var g = AutoTileGrid.CornerGrid;
            var seen = new HashSet<int>();
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                {
                    int w = (g[r][c] == '1' ? 1 : 0) | (g[r][c + 1] == '1' ? 2 : 0) | (g[r + 1][c] == '1' ? 4 : 0) | (g[r + 1][c + 1] == '1' ? 8 : 0);
                    Assert.AreEqual(w, AutoTileGrid.Layout[r, c], $"tile ({r},{c})");
                    Assert.AreEqual(r * 4 + c, AutoTileGrid.SlotOf(w));
                    seen.Add(w);
                }
            Assert.AreEqual(16, seen.Count);
        }

        [Test]
        public void AutoTile_SlotPicksFillsForFullAndNothingForEmpty()
        {
            Assert.AreEqual(-1, AutoTileGrid.Slot(0, 3, 4));
            for (int i = -5; i < 5; i++)
                for (int j = -5; j < 5; j++)
                {
                    int s = AutoTileGrid.Slot(15, i, j);
                    Assert.That(s, Is.InRange(16, 19));
                    Assert.AreEqual(16 + AutoTileGrid.FillVariant(i, j), s);
                }
            Assert.AreEqual(5, AutoTileGrid.SlotOf(15), "T15 sits at row 1, column 1");
        }

        [Test]
        public void AutoTile_SideFaceFramesStackContinuously()
        {
            Assert.AreEqual(0, AutoTileGrid.SideFaceFrame(0));
            Assert.AreEqual(3, AutoTileGrid.SideFaceFrame(-1));
            Assert.AreEqual(1, AutoTileGrid.SideFaceFrame(5));
            for (int y = -9; y < 9; y++)   // one tilemap cell down = the next frame of the strip
                Assert.AreEqual((AutoTileGrid.SideFaceFrameAtTilemapY(y) + 1) % 4, AutoTileGrid.SideFaceFrameAtTilemapY(y - 1));
        }

        [Test]
        public void AutoTile_Open2x2_DropsSingleCellsAndStrips()
        {
            var cells = Cells((0, 0), (1, 0), (0, 1), (1, 1), (2, 1), (5, 5), (8, 0), (8, 1), (8, 2));
            var o = AutoTileGrid.Open2x2(cells);
            CollectionAssert.AreEquivalent(Cells((0, 0), (1, 0), (0, 1), (1, 1)), o);
        }

        // ---------------------------------------------------------------- sheets on disk

        const string TemplateDir = "docs/art/reference/autotile/";

        static IEnumerable<string> SheetFiles()
        {
            foreach (var dir in new[] { "Assets/_Project/Art/Islands/AutoTiles", "Assets/_Project/Art/Incoming/islands" })
                if (Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir, "island_*_auto_*.png")) yield return f.Replace('\\', '/');
        }

        static Color32[] Pixels(string path, out int w, out int h)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            w = t.width; h = t.height;
            var px = t.GetPixels32();
            Object.DestroyImmediate(t);
            return px;
        }

        [Test]
        public void AutoTile_SheetsAreSlicedAsTheContract()
        {
            var files = SheetFiles().ToList();
            Assert.IsNotEmpty(files, "no auto-tile sheets (run Idle Grounds/World/Rebuild Auto Tiles)");
            foreach (var f in files)
            {
                bool side = f.EndsWith("_64x128.png");
                var sprites = AssetDatabase.LoadAllAssetsAtPath(f).OfType<Sprite>().ToDictionary(s => s.name);
                string bn = Path.GetFileNameWithoutExtension(f);
                Assert.AreEqual(side ? 8 : 20, sprites.Count, f);
                for (int n = 0; n < sprites.Count; n++)
                {
                    Assert.IsTrue(sprites.TryGetValue(bn + "_" + n, out var s), f + " sprite " + n);
                    Assert.AreEqual(32f, s.pixelsPerUnit, f);
                    int r = side ? n % 4 : n / 4, c = side ? n / 4 : n % 4, h = side ? 128 : 160;
                    Assert.AreEqual(new Rect(c * 32, h - (r + 1) * 32, 32, 32), s.rect, f + " sprite " + n);
                    Assert.AreEqual(FilterMode.Point, s.texture.filterMode, f);
                }
            }
        }

        [Test]
        public void AutoTile_RenderStripsMatchTheirSheets()
        {
            // the Tiles draw from Art/Islands/AutoTiles/Strips/<sheet>_strip.png: slot n at x = n * 34 + 1 (1-px extruded edges);
            // it is rebuilt from the delivered sheet when there is one, else from the placeholder
            foreach (var f in SheetFiles())
            {
                string bn = Path.GetFileNameWithoutExtension(f);
                string inc = "Assets/_Project/Art/Incoming/islands/" + bn + ".png";
                if (!f.StartsWith("Assets/_Project/Art/Incoming") && File.Exists(inc)) continue;   // the delivered sheet is the source
                string strip = "Assets/_Project/Art/Islands/AutoTiles/Strips/" + bn + "_strip.png";
                Assert.IsTrue(File.Exists(strip), strip);
                var sp = Pixels(strip, out int sw, out int sh);
                var px = Pixels(f, out int w, out int h);
                bool side = f.EndsWith("_64x128.png");
                int n = (w / 32) * (h / 32);
                Assert.AreEqual(n * 34, sw, strip); Assert.AreEqual(32, sh, strip);
                Assert.AreEqual(n, AssetDatabase.LoadAllAssetsAtPath(strip).OfType<Sprite>().Count(), strip);
                for (int k = 0; k < n; k++)
                {
                    int r = side ? k % 4 : k / 4, c = side ? k / 4 : k % 4;
                    int bx = c * 32, by = h - (r + 1) * 32;              // bottom-up texture rows
                    for (int y = 0; y < 32; y += 3)
                        for (int x = 0; x < 32; x += 3)
                            Assert.AreEqual(px[(by + y) * w + bx + x], sp[y * sw + k * 34 + 1 + x], $"{strip} slot {k} at {x},{y}");
                }
            }
        }

        [Test]
        public void AutoTile_SheetsHaveTemplateAlpha_AndT15EqualsF0()
        {
            var ta = Pixels(TemplateDir + "autotile_wang16_alpha_128x160.png", out int tw, out int th);
            var sa = Pixels(TemplateDir + "autotile_sidewall_alpha_64x128.png", out int sw, out int sh);
            foreach (var f in SheetFiles())
            {
                bool side = f.EndsWith("_64x128.png");
                var px = Pixels(f, out int w, out int h);
                var tpl = side ? sa : ta;
                Assert.AreEqual(side ? sw : tw, w, f); Assert.AreEqual(side ? sh : th, h, f);
                int off = 0;
                for (int i = 0; i < px.Length; i++) if ((px[i].a > 127) != (tpl[i].a > 127)) off++;
                Assert.AreEqual(0, off, f + ": alpha px off the template");
                if (side) continue;
                // bottom-up rows: F0 = sheet rows 128-159 from the top = texture rows 0-31; T15 = rows 32-63 from the top = texture rows 96-127
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                        Assert.AreEqual(px[y * w + x], px[(96 + y) * w + 32 + x], $"{f}: T15 != F0 at {x},{y}");
            }
        }
    }
}
