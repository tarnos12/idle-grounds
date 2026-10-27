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

        [TestCase("deco_snow_props_128x128_4f@64", "deco_snow_props", 4, 128, 128, 64)]
        [TestCase("deco_snow_props_64x96_8f@64", "deco_snow_props", 8, 64, 96, 64)]
        [TestCase("deco_snow_props_32x48_8f", "deco_snow_props", 8, 32, 48, 32)]
        [TestCase("island_fishing_ice_hole_64x64", "island_fishing_ice_hole", 1, 64, 64, 32)]
        [TestCase("bld_kiln@64", "bld_kiln", 1, 0, 0, 64)]
        public void ParseFileName(string baseName, string key, int frames, int w, int h, int density)
        {
            Assert.AreEqual(key, ArtKeys.ParseFileName(baseName, out int f, out int fw, out int fh, out int d));
            Assert.AreEqual((frames, w, h, density), (f, fw, fh, d));
        }

        [System.Serializable] class DecorEntry { public string island, trees, props, bigProps; }
        [System.Serializable] class DecorFile { public DecorEntry[] islands; }

        /// <summary>Fake delivered file names (no assets) through the intake's alias rule: the Fishing props / bigProps keys of
        /// island-decor.json resolve to the step D @64 strips once they land (ART-SPEC §3.9.8 "Step D", integrator note 2), and to
        /// the 32-px files until then, whatever subset of D3 / D4 is delivered.</summary>
        [TestCase(false, false, "deco_snow_props_32x48_8f", "deco_snow_props_64x64_4f", "deco_snow_props_32x48_8f")]
        [TestCase(true, true, "deco_snow_props_64x96_8f@64", "deco_snow_props_128x128_4f@64", "deco_snow_props_64x96_8f@64")]
        [TestCase(true, false, "deco_snow_props_64x96_8f@64", "deco_snow_props_64x64_4f", "deco_snow_props_64x96_8f@64")]
        [TestCase(false, true, "deco_snow_props_32x48_8f", "deco_snow_props_128x128_4f@64", "deco_snow_props_128x128_4f@64")]
        public void StepD_Snow_Props_Resolve_The_IslandDecor_Keys(bool d3, bool d4, string props, string bigProps, string bare)
        {
            var names = new System.Collections.Generic.List<string> { "deco_snow_props_32x48_8f", "deco_snow_props_64x64_4f" };
            if (d3) names.Add("deco_snow_props_64x96_8f@64");
            if (d4) names.Add("deco_snow_props_128x128_4f@64");
            var files = new System.Collections.Generic.List<(int w, int h, int density)>();
            foreach (var n in names)
            {
                Assert.AreEqual("deco_snow_props", ArtKeys.ParseFileName(n, out _, out int w, out int h, out int d));
                files.Add((w, h, d));
            }
            var aliases = ArtKeys.SizeAliases("deco_snow_props", files, out int bareIdx);
            string json = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "_Project/Art/Islands/island-decor.json"));
            var fishing = System.Array.Find(JsonUtility.FromJson<DecorFile>(json).islands, e => e.island == "fishing");
            Assert.IsNotNull(fishing);
            Assert.IsTrue(aliases.ContainsKey(fishing.props), "props key " + fishing.props + " is an alias");
            Assert.IsTrue(aliases.ContainsKey(fishing.bigProps), "bigProps key " + fishing.bigProps + " is an alias");
            Assert.AreEqual(props, names[aliases[fishing.props]], "props (1x1)");
            Assert.AreEqual(bigProps, names[aliases[fishing.bigProps]], "bigProps (2x1)");
            Assert.AreEqual(bare, names[bareIdx], "bare key: the highest density, smallest canvas");
            // the @64 files also by their own canvas (deco_snow_props_64x96 / _128x128); a 32-px canvas name is a world size
            for (int i = 0; i < names.Count; i++)
                if (files[i].density == 64) Assert.AreEqual(i, aliases["deco_snow_props_" + files[i].w + "x" + files[i].h], names[i]);
        }

        [Test]
        public void SizeAliases_Only_For_Keys_In_Several_World_Sizes()
        {
            var one = ArtKeys.SizeAliases("node_ore_1x1", new[] { (32, 48, 32), (64, 96, 64) }, out int bare);
            Assert.AreEqual(0, one.Count, "the @64 redraw of the same footprint is no second size");
            Assert.AreEqual(-1, bare);
            Assert.AreEqual(0, ArtKeys.SizeAliases("bld_kiln", new[] { (0, 0, 32), (0, 0, 64) }, out _).Count);
        }
    }
}
