using System.Collections.Generic;
using IdleGrounds.Game;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleGrounds.Game.Tests
{
    /// <summary>Code animation layer (owner pick anim_route 2026-10-26): config loading, artist file names, pixel snapping,
    /// pixel-exact bakes and calm motion. Zero GC per tick: PlayMode CodeAnimPlayModeTests (edit-mode property setters allocate in the editor).</summary>
    public class CodeAnimTests
    {
        bool hadCalm, calm;
        readonly List<Object> trash = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            hadCalm = PlayerPrefs.HasKey(Juice.ReducePref); calm = Juice.ReduceMotion;
            CodeAnimLibrary.Reload();
        }

        [TearDown]
        public void TearDown()
        {
            Juice.SetReduceMotion(calm);
            if (!hadCalm) PlayerPrefs.DeleteKey(Juice.ReducePref);
            foreach (var o in trash) if (o != null) Object.DestroyImmediate(o);
            trash.Clear();
            CodeAnimLibrary.Reload();
        }

        // ------------------------------------------------------------ config

        [Test]
        public void Config_Loads_With_Every_Key_And_Valid_Effects()
        {
            var f = CodeAnimLibrary.File;
            Assert.IsNotNull(f); Assert.GreaterOrEqual(f.version, 1);
            string[] keys = { "bld_kiln", "bld_paper_mill", "bld_workbench", "bld_sky_shrine", "npc_lin", "npc_ouyang", "npc_kang", "npc_jiang", "npc_tian", "npc_qingyun", "npc_xuan" };
            foreach (var k in keys) Assert.IsNotNull(CodeAnimLibrary.Find(k), k);
            foreach (var e in f.entries)
            {
                Assert.Greater(e.StillW, 0, e.key); Assert.Greater(e.StillH, 0, e.key);
                if (e.keepDeliveredLoop) { Assert.IsFalse(e.HasEffects, e.key + ": accepted loop keeps no code effects"); continue; }
                Assert.IsTrue(e.HasEffects, e.key);
                Assert.AreEqual(e.key.StartsWith("npc_"), e.PlaysAlways, e.key + ": NPCs idle always, buildings only while working");
                foreach (var x in e.effects)
                {
                    string id = e.key + "/" + x.name;
                    Assert.AreNotEqual(CodeAnimKind.Unknown, x.kind, id + " type " + x.type);
                    Assert.IsFalse(string.IsNullOrEmpty(x.name), id);
                    if (x.HasRect)
                    {
                        Assert.GreaterOrEqual(x.rect[0], 0, id); Assert.GreaterOrEqual(x.rect[1], 0, id);
                        Assert.LessOrEqual(x.rect[0] + x.rect[2], e.StillW, id); Assert.LessOrEqual(x.rect[1] + x.rect[3], e.StillH, id);
                    }
                    if (x.points != null)
                    {
                        Assert.AreEqual(0, x.points.Length % 2, id);
                        for (int i = 0; i < x.points.Length; i += 2) { Assert.That(x.points[i], Is.InRange(0, e.StillW), id); Assert.That(x.points[i + 1], Is.InRange(0, e.StillH), id); }
                    }
                    if (x.kind == CodeAnimKind.Glow || x.kind == CodeAnimKind.Flicker) Assert.That(x.alpha[0], Is.InRange(0f, 1f), id);
                    if (x.kind == CodeAnimKind.Smoke) Assert.AreEqual("hide", x.calm, id + ": no smoke in calm motion");
                    if (x.kind == CodeAnimKind.Layer && x.offsets != null) Assert.AreEqual(0, x.offsets.Length % 2, id + ": slide = [dx, dy] pairs");
                }
            }
            Assert.IsTrue(CodeAnimLibrary.Find("bld_kiln").replaceDeliveredLoop);
            Assert.IsTrue(CodeAnimLibrary.Find("bld_paper_mill").replaceDeliveredLoop);
        }

        [Test]
        public void Parse_Applies_Defaults_And_Survives_Bad_Json()
        {
            var f = CodeAnimLibrary.Parse("{\"entries\":[{\"key\":\"npc_x\",\"still\":[64,96],\"effects\":[{\"type\":\"smoke\",\"name\":\"s\"},{\"type\":\"glow\",\"name\":\"g\",\"colorA\":\"#FF8A1F\"}]}]}");
            var e = f.entries[0];
            Assert.AreEqual(6f, e.fps); Assert.AreEqual(4, e.steps); Assert.IsTrue(e.PlaysAlways);
            Assert.AreEqual(CodeAnimKind.Smoke, e.effects[0].kind); Assert.AreEqual("puff", e.effects[0].sprite); Assert.Less(e.effects[0].CalmAlpha, 0f);
            Assert.AreEqual(CodeAnimKind.Glow, e.effects[1].kind); Assert.AreEqual(1f, e.effects[1].colA.r, 0.01f); Assert.GreaterOrEqual(e.effects[1].CalmAlpha, 0f);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("bad code-anim.json"));
            var bad = CodeAnimLibrary.Parse("{ not json");
            Assert.IsNotNull(bad.entries); Assert.AreEqual(0, bad.entries.Length);
        }

        [Test]
        public void Artist_File_Names_Follow_ArtSpec_398()
        {
            Assert.AreEqual("bld_paper_mill_layer_wheel", CodeAnimLibrary.FileKey("bld_paper_mill", CodeAnimLibrary.LayerSuffix("wheel")));
            Assert.AreEqual("npc_kang_layer_arm_raised", CodeAnimLibrary.FileKey("npc_kang", CodeAnimLibrary.PoseSuffix("arm", "raised")));
            Assert.AreEqual("npc_kang_layer_arm", CodeAnimLibrary.FileKey("npc_kang", CodeAnimLibrary.PoseSuffix("arm", "")));
            Assert.AreEqual("npc_xuan_base", CodeAnimLibrary.FileKey("npc_xuan", CodeAnimLibrary.BaseSuffix));
            Assert.AreEqual("bld_paper_mill_layer_front", CodeAnimLibrary.FileKey("bld_paper_mill", CodeAnimLibrary.FrontSuffix));
        }

        // ------------------------------------------------------------ math

        [Test]
        public void Alpha_Is_Quantised_To_Four_Steps_And_Positions_Snap_To_Texels()
        {
            var allowed = new[] { 0f, 64f / 255f, 128f / 255f, 191f / 255f, 1f };
            for (float a = 0f; a <= 1f; a += 0.01f)
                Assert.That(System.Array.Exists(allowed, v => Mathf.Abs(v - CodeAnimMath.QuantizeAlpha(a)) < 1e-5f), "alpha " + a);
            Assert.AreEqual(3f / 64f, CodeAnimMath.Snap(0.05f, 64f), 1e-6f);
            Assert.AreEqual(-2f / 32f, CodeAnimMath.Snap(-0.06f, 32f), 1e-6f);
            for (float t = 0f; t < 10f; t += 0.37f) Assert.That(CodeAnimMath.Pulse(t, 1.3f, 0.2f) * 3f % 1f, Is.EqualTo(0f).Within(1e-4f), "pulse has 4 levels");
            Assert.AreEqual(CodeAnimMath.Hash01(5, 2, 7), CodeAnimMath.Hash01(5, 2, 7));
        }

        // ------------------------------------------------------------ bakes

        static PixelImage Img(int w, int h) => new PixelImage(w, h);
        static readonly Color32 Red = new Color32(200, 30, 30, 255), Blue = new Color32(40, 130, 180, 255), White = new Color32(232, 236, 243, 255);

        static CodeAnimEffect Fx(string json)
        {
            var f = CodeAnimLibrary.Parse("{\"entries\":[{\"key\":\"t\",\"effects\":[" + json + "]}]}");
            return f.entries[0].effects[0];
        }

        [Test]
        public void Sway_Shears_From_Pivot_And_Clears_Vacated_Pixels()
        {
            var img = Img(20, 10);
            for (int y = 0; y < 10; y++) img.Set(5, y, Red);
            var e = Fx("{\"type\":\"sway\",\"name\":\"bar\",\"rect\":[0,0,20,10],\"pivot\":[5,9],\"axis\":\"x\",\"offsets\":[0,3]}");
            CodeAnimBake.ApplyBaked(e, img, 1);
            Assert.AreEqual(255, img.Get(8, 0).a, "tip moved 3 px right");
            Assert.AreEqual(0, img.Get(5, 0).a, "vacated tip pixel is transparent");
            Assert.AreEqual(255, img.Get(5, 9).a, "pivot row stays");
            int n = 0; foreach (var c in img.px) if (c.a == 255) n++;
            Assert.AreEqual(10, n, "same pixel count: moved, not smeared");
        }

        [Test]
        public void Shift_Fills_From_The_Trailing_Side_And_Scroll_Wraps()
        {
            var img = Img(10, 12);
            for (int y = 0; y < 4; y++) img.Set(4, y, White);       // "screw"
            for (int y = 4; y < 8; y++) for (int x = 2; x < 7; x++) img.Set(x, y, Red);   // "press block"
            var e = Fx("{\"type\":\"shift\",\"name\":\"press\",\"rect\":[2,4,5,4],\"masks\":[{\"hue\":[350,20],\"sat\":[0.5,1]}],\"offsets\":[0,2]}");
            CodeAnimBake.ApplyBaked(e, img, 1);
            Assert.AreEqual(Red, img.Get(2, 9), "block moved down 2");
            Assert.AreEqual(White, img.Get(4, 4), "screw stretches into the vacated rows");
            Assert.AreEqual(0, img.Get(2, 4).a, "vacated row beside the screw is clear");

            var w = Img(3, 6);
            for (int y = 0; y < 6; y++) w.Set(1, y, y == 0 ? White : Blue);
            var s = Fx("{\"type\":\"scroll\",\"name\":\"water\",\"rect\":[0,0,3,6],\"speed\":2}");
            CodeAnimBake.ApplyBaked(s, w, 1);
            Assert.AreEqual(White, w.Get(1, 2), "foam scrolled 2 px down");
            Assert.AreEqual(Blue, w.Get(1, 0));
        }

        [Test]
        public void Line_Is_Connected_And_Its_End_Sways()
        {
            var img = Img(16, 40);
            var e = Fx("{\"type\":\"line\",\"name\":\"l\",\"points\":[4,2,4,30],\"colorA\":\"#7A809A\",\"offsets\":[0,3]}");
            CodeAnimBake.ApplyBaked(e, img, 1);
            Assert.AreEqual(255, img.Get(4, 2).a, "starts at the rod tip");
            Assert.AreEqual(255, img.Get(7, 30).a, "end swayed 3 px");
            for (int y = 2; y <= 30; y++) { int n = 0; for (int x = 0; x < 16; x++) if (img.Get(x, y).a == 255) n++; Assert.GreaterOrEqual(n, 1, "row " + y); }
        }

        // ------------------------------------------------------------ animator

        Sprite Still(int w, int h, float ppu)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                px[y * w + x] = x > 8 && x < w - 8 && y < h - 10 ? (y % 7 == 0 ? Blue : (x % 5 == 0 ? White : Red)) : new Color32(0, 0, 0, 0);
            t.SetPixels32(px); t.Apply();
            trash.Add(t);
            var s = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect);
            trash.Add(s);
            return s;
        }

        CodeAnimator Animator(string effectsJson, out SpriteRenderer sr, out Sprite still, float ppu = 64f, System.Func<string, Sprite> resolve = null)
        {
            var f = CodeAnimLibrary.Parse("{\"entries\":[{\"key\":\"npc_test\",\"still\":[64,96],\"fps\":4,\"steps\":4,\"effects\":[" + effectsJson + "]}]}");
            var go = new GameObject("codeanim-test"); trash.Add(go);
            go.transform.position = new Vector3(3.3f, -7.1f, 0f);
            sr = go.AddComponent<SpriteRenderer>();
            still = Still(64, 96, ppu);
            sr.sprite = still;
            var ca = CodeAnimator.For(sr);
            Assert.IsTrue(ca.ConfigureUncached(f.entries[0], sr, still, resolve));
            return ca;
        }

        const string AllOverlays =
            "{\"type\":\"glow\",\"name\":\"g\",\"rect\":[10,10,30,30],\"colorA\":\"#5ED4E8\",\"colorB\":\"#F2C94C\",\"period\":1.1}," +
            "{\"type\":\"flicker\",\"name\":\"f\",\"sprite\":\"flame\",\"size\":[5,9],\"count\":3,\"points\":[20,60,27,61],\"offsets\":[0,1,2,1]}," +
            "{\"type\":\"flicker\",\"name\":\"st\",\"sprite\":\"star\",\"size\":[3,3],\"mode\":\"cycle\",\"points\":[30,30,35,35]}," +
            "{\"type\":\"smoke\",\"name\":\"sm\",\"points\":[33,12],\"size\":[5,5],\"growTo\":[11,9],\"rise\":13,\"drift\":5,\"count\":4,\"period\":1.7}," +
            "{\"type\":\"toggle\",\"name\":\"tg\",\"rect\":[40,70,7,9],\"onSteps\":[2,3]}," +
            "{\"type\":\"sway\",\"name\":\"sw\",\"rect\":[30,20,20,30],\"pivot\":[32,49],\"offsets\":[0,2,0,-2]}";

        [Test]
        public void Overlays_And_Frames_Stay_On_The_Texel_Grid()
        {
            Juice.SetReduceMotion(false);
            var ca = Animator(AllOverlays, out var sr, out var still);
            Assert.AreEqual(4, ca.BaseFrameCount);
            for (int i = 0; i < ca.BaseFrameCount; i++)
            {
                var f = ca.BaseFrame(i);
                Assert.AreEqual(still.rect.size, f.rect.size); Assert.AreEqual(still.pixelsPerUnit, f.pixelsPerUnit);
                Assert.AreEqual(still.pivot.x, f.pivot.x, 1e-3f); Assert.AreEqual(still.pivot.y, f.pivot.y, 1e-3f);
            }
            int shown = 0;
            for (float t = 0.013f; t < 9f; t += 0.0937f)
            {
                ca.Tick(t);
                for (int i = 0; i < ca.SlotCount; i++)
                {
                    var r = ca.SlotRenderer(i);
                    if (!r.enabled) continue;
                    shown++;
                    var p = r.transform.localPosition * 64f;
                    Assert.AreEqual(Mathf.Round(p.x), p.x, 1e-3f, r.name + " x at t=" + t);
                    Assert.AreEqual(Mathf.Round(p.y), p.y, 1e-3f, r.name + " y at t=" + t);
                    // the overlay's own pivot is a texel corner too
                    var sp = r.sprite;
                    Assert.AreEqual(Mathf.Round(sp.pivot.x), sp.pivot.x, 1e-3f); Assert.AreEqual(Mathf.Round(sp.pivot.y), sp.pivot.y, 1e-3f);
                    Assert.AreEqual(64f, sp.pixelsPerUnit);
                    float a = r.color.a;
                    Assert.That(a, Is.EqualTo(0f).Or.EqualTo(64f / 255f).Within(1e-4f).Or.EqualTo(128f / 255f).Within(1e-4f).Or.EqualTo(191f / 255f).Within(1e-4f).Or.EqualTo(1f).Within(1e-4f), r.name + " alpha " + a);
                }
            }
            Assert.Greater(shown, 100);

            // the glow mask of rect [10,10,..] sits exactly on those still texels: pivot is bottom-centre (32, 96 from top-left)
            ca.Tick(0.4f);
            for (int i = 0; i < ca.SlotCount; i++)
                if (ca.SlotRenderer(i).sprite != null && ca.SlotRenderer(i).sprite.name.EndsWith("_g"))
                    Assert.AreEqual(new Vector3((10 - 32) / 64f, (96 - 10) / 64f, 0f), ca.SlotRenderer(i).transform.localPosition, "glow on its texels");
            // smoke rises above its emitter texel (y up in the world)
            for (int i = 0; i < ca.SlotCount; i++)
                if (ca.SlotRenderer(i).enabled && ca.SlotRenderer(i).sprite.name.Contains("_puff"))
                    Assert.GreaterOrEqual(ca.SlotRenderer(i).transform.localPosition.y, (96 - 12) / 64f - 1e-4f, "smoke above the emitter");
        }

        [Test]
        public void Calm_Motion_Holds_The_Rest_Frame_Without_Smoke()
        {
            Juice.SetReduceMotion(true);
            var ca = Animator(AllOverlays, out var sr, out _);
            Color? glow = null;
            for (float t = 0f; t < 6f; t += 0.21f)
            {
                ca.Tick(t);
                Assert.AreSame(ca.BaseFrame(0), sr.sprite, "rest frame at t=" + t);
                for (int i = 0; i < ca.SlotCount; i++)
                {
                    var r = ca.SlotRenderer(i);
                    if (r.name.Length == 0) continue;
                    var s = r.sprite;
                    if (s != null && s.name.Contains("_puff")) Assert.IsFalse(r.enabled, "no smoke when calm");
                    if (s != null && s.name.EndsWith("_g") && r.enabled) { if (glow == null) glow = r.color; else Assert.AreEqual(glow.Value, r.color, "glow holds steady"); }
                }
            }
            Assert.IsNotNull(glow, "glow stays visible (steady) when calm");
            Juice.SetReduceMotion(false);
            bool moved = false;
            for (float t = 0f; t < 3f; t += 0.13f) { ca.Tick(t); if (sr.sprite != ca.BaseFrame(0)) moved = true; }
            Assert.IsTrue(moved, "full motion plays the cycle again");
        }

        [Test]
        public void Stops_And_Restores_The_Still_When_Not_Playing()
        {
            var f = CodeAnimLibrary.Parse("{\"entries\":[{\"key\":\"bld_test\",\"still\":[64,96],\"play\":\"working\",\"effects\":[" + AllOverlays + "]}]}");
            var go = new GameObject("codeanim-test2"); trash.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            var still = Still(64, 96, 64f); sr.sprite = still;
            var ca = CodeAnimator.For(sr);
            Assert.IsTrue(ca.ConfigureUncached(f.entries[0], sr, still, null));
            ca.Playing = true; Juice.SetReduceMotion(false);
            for (float t = 0f; t < 2f; t += 0.1f) ca.Tick(t);
            ca.Playing = false; ca.Tick(2.5f);
            Assert.AreSame(still, sr.sprite);
            for (int i = 0; i < ca.SlotCount; i++) Assert.IsFalse(ca.SlotRenderer(i).enabled);
        }

        [Test]
        public void Layers_Wait_For_Artist_Files_Then_Replace_The_Still_With_The_Base()
        {
            const string json = "{\"type\":\"layer\",\"name\":\"wheel\",\"sprite\":\"wheel\",\"pivot\":[32,48],\"angles\":[0,15,30,45]}," +
                                "{\"type\":\"shift\",\"name\":\"standin\",\"onlyWithoutLayers\":true,\"rect\":[10,10,20,20],\"offsets\":[0,1,2,1]}";
            var ca = Animator(json, out var sr, out var still);
            CollectionAssert.AreEquivalent(new[] { "npc_test_layer_wheel" }, ca.MissingLayers);
            Assert.AreEqual(4, ca.BaseFrameCount, "stand-in runs while the layer is missing");

            var layer = Still(64, 96, 64f); var bas = Still(64, 96, 64f);
            var ca2 = Animator(json, out var sr2, out _, 64f, s => s == "layer_wheel" ? layer : s == "base" ? bas : null);
            Assert.AreEqual(0, ca2.MissingLayers.Count);
            Assert.AreEqual(0, ca2.BaseFrameCount, "stand-in off once the layer is here");
            ca2.Tick(0.6f);
            Assert.AreSame(bas, sr2.sprite, "the base (still minus moving parts) is drawn under the layer");
            Assert.AreEqual(1, ca2.SlotCount);
            Assert.IsTrue(ca2.SlotRenderer(0).enabled);
        }
    }
}
