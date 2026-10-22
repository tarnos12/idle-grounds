using IdleGrounds.Game.Data;
using NUnit.Framework;
using UnityEngine;

namespace IdleGrounds.Game.Tests
{
    /// <summary>obj64 (owner pick art_density, ART-SPEC §3.9): "@64" files import at PPU 64 and draw at the same world size.</summary>
    public class ArtDensityTests
    {
        [TestCase("bld_kiln", "bld_kiln", 32)]
        [TestCase("bld_kiln@64", "bld_kiln", 64)]
        [TestCase("bld_kiln_working_192x384_4f@64", "bld_kiln_working_192x384_4f", 64)]
        [TestCase("node_bush_64x96_2f@64", "node_bush_64x96_2f", 64)]
        [TestCase("bld_kiln@32", "bld_kiln@32", 32)]      // only @64 is a tag: anything else stays in the name (unwired -> warning)
        [TestCase("bld_kiln@128", "bld_kiln@128", 32)]
        [TestCase("@64", "@64", 32)]
        public void SplitDensity(string baseName, string key, int density)
        {
            Assert.AreEqual(key, ArtKeys.SplitDensity(baseName, out int d));
            Assert.AreEqual(density, d);
        }

        [Test]
        public void NativeScale_SameWorldSize_At32And64()
        {
            var t32 = new Texture2D(96, 128); var t64 = new Texture2D(192, 256);
            var s32 = Sprite.Create(t32, new Rect(0, 0, 96, 128), new Vector2(0.5f, 0f), 32f);
            var s64 = Sprite.Create(t64, new Rect(0, 0, 192, 256), new Vector2(0.5f, 0f), 64f);
            var s100 = Sprite.Create(t32, new Rect(0, 0, 96, 128), new Vector2(0.5f, 0f), 100f);
            try
            {
                Assert.AreEqual(1f, ViewKit.NativeScale(s32));
                Assert.AreEqual(1f, ViewKit.NativeScale(s64));
                Assert.AreEqual(s32.bounds.size.x * ViewKit.NativeScale(s32), s64.bounds.size.x * ViewKit.NativeScale(s64), 1e-5f, "3 cells wide at both densities");
                Assert.AreEqual(s32.bounds.size.y * ViewKit.NativeScale(s32), s64.bounds.size.y * ViewKit.NativeScale(s64), 1e-5f);
                Assert.AreEqual(3f, s100.bounds.size.x * ViewKit.NativeScale(s100), 1e-4f, "other PPU: treated as 32 texels per cell");
            }
            finally { Object.DestroyImmediate(s32); Object.DestroyImmediate(s64); Object.DestroyImmediate(s100); Object.DestroyImmediate(t32); Object.DestroyImmediate(t64); }
        }
    }
}
