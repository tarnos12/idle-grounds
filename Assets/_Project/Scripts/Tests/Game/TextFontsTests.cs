using IdleGrounds.Game;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleGrounds.Game.Tests
{
    /// <summary>Menu > Settings "Text font" (owner pick font_read = "setting", 2026-10-22): options, size rules, assets, preference.</summary>
    public class TextFontsTests
    {
        string saved;
        bool had;

        [SetUp] public void SetUp() { had = PlayerPrefs.HasKey(UiFont.PrefKey); saved = PlayerPrefs.GetString(UiFont.PrefKey, ""); }

        [TearDown]
        public void TearDown()
        {
            if (had) PlayerPrefs.SetString(UiFont.PrefKey, saved); else PlayerPrefs.DeleteKey(UiFont.PrefKey);
            UiFont.ReloadChoice();
        }

        [Test]
        public void Six_Options_Default_Is_Pixelify_Titles_With_Jersey()
        {
            Assert.AreEqual(6, TextFonts.Options.Length);
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var o in TextFonts.Options) { Assert.IsTrue(ids.Add(o.id), o.id); Assert.IsNotEmpty(o.label); Assert.IsNotEmpty(o.tip); }
            var d = TextFonts.Find(TextFonts.DefaultId);
            Assert.AreSame(TextFonts.Pixelify, d.title);
            Assert.AreSame(TextFonts.Jersey, d.body);
            foreach (var id in new[] { "pixelify", "jersey10", "vt323", "pressstart2p", "smooth" })
            {
                var o = TextFonts.Find(id);
                Assert.IsNotNull(o, id);
                Assert.AreSame(o.title, o.body, id + " is one face for titles and text");
            }
        }

        [TestCase("pixelify", 22f, 1f, 22f)]
        [TestCase("pixelify", 30f, 1f, 33f)]
        [TestCase("pixelify", 17f, 2.07f, 33f)]
        [TestCase("jersey10", 22f, 1f, 28f)]
        [TestCase("jersey10", 30f, 1f, 112f / 3f)]
        [TestCase("jersey10", 44f, 1f, 56f)]
        [TestCase("jersey10", 22f, 0.6667f, 28f)]
        [TestCase("vt323", 22f, 1f, 25f)]
        [TestCase("vt323", 30f, 1f, 37.5f)]
        [TestCase("pressstart2p", 22f, 1f, 16f)]
        [TestCase("pressstart2p", 30f, 1f, 24f)]
        [TestCase("pressstart2p", 44f, 1f, 32f)]
        [TestCase("atkinson", 22f, 1f, 22f)]
        [TestCase("atkinson", 30f, 1f, 33f)]
        public void Sizes_Snap_To_The_Face_Grid(string face, float authored, float scale, float want)
        {
            var f = TextFonts.FindFace(face);
            Assert.AreEqual(want, f.SnapScreenPx(authored, scale), 0.01f);
        }

        [Test]
        public void Every_Size_Is_A_Whole_Font_Pixel_And_Never_Under_The_Body_Size()
        {
            foreach (var f in TextFonts.Faces)
                for (float a = 8f; a <= 60f; a += 0.5f)
                    foreach (var sc in new[] { 0.6667f, 1f, 1.333f, 2.07f })
                    {
                        float px = f.SnapScreenPx(a, sc);
                        Assert.GreaterOrEqual(px, f.bodyPx - 0.001f, f.id);
                        float n = px / f.stepPx;
                        Assert.AreEqual(Mathf.Round(n), n, 0.001f, f.id + " " + a + " @" + sc + " -> " + px);
                    }
        }

        [Test]
        public void Every_Face_Is_Built()
        {
            foreach (var f in TextFonts.Faces)
            {
                if (f.sdfResource != null) Assert.IsNotNull(Resources.Load<Object>(f.sdfResource), f.sdfResource);
                if (f.rasterResource != null) Assert.IsNotNull(Resources.Load<Object>(f.rasterResource), f.rasterResource);
                if (f.boldResource != null) Assert.IsNotNull(Resources.Load<Object>(f.boldResource), f.boldResource);
            }
        }

        [Test]
        public void Choice_Is_A_Player_Preference_With_The_Default()
        {
            PlayerPrefs.DeleteKey(UiFont.PrefKey);
            UiFont.ReloadChoice();
            Assert.AreEqual(TextFonts.DefaultId, UiFont.Choice.id);
            UiFont.SetChoice("pressstart2p");
            Assert.AreEqual("pressstart2p", PlayerPrefs.GetString(UiFont.PrefKey));
            UiFont.ReloadChoice();
            Assert.AreEqual("pressstart2p", UiFont.Choice.id);
            UiFont.SetChoice("no-such-font");
            Assert.AreEqual("pressstart2p", UiFont.Choice.id, "unknown ids are ignored");
            PlayerPrefs.SetString(UiFont.PrefKey, "garbage");
            UiFont.ReloadChoice();
            Assert.AreEqual(TextFonts.DefaultId, UiFont.Choice.id, "a bad pref falls back to the default");
        }
    }
}
