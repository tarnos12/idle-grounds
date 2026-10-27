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
                Assert.AreEqual(e.key.StartsWith("npc_") || e.key.StartsWith("fix_") || e.key.StartsWith("node_"), e.PlaysAlways, e.key + ": NPCs, fixtures and nodes idle always, buildings only while working");
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
            // the Paper Mill wheel is one complete side-view layer that turns in its ellipse (no hand-drawn poses)
            CodeAnimEffect wheel = null;
            foreach (var x in CodeAnimLibrary.Find("bld_paper_mill").effects) if (x.name == "wheel") wheel = x;
            Assert.IsNotNull(wheel);
            Assert.IsTrue(wheel.IsEllipseSpin, "wheel: ellipseSpin");
            Assert.IsNull(wheel.poses, "wheel: no pose layers");
            Assert.That(wheel.ellipse, Is.Not.Null.And.Length.EqualTo(2));
            Assert.That(wheel.pivot[0], Is.InRange(0, 192)); Assert.That(wheel.pivot[1], Is.InRange(0, 256));
            Assert.GreaterOrEqual(wheel.count, 16); Assert.AreNotEqual(0f, wheel.spin);
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

        // ------------------------------------------------------------ ellipse spin (side-view wheel)

        static readonly Color32 Rim = new Color32(168, 112, 58, 255), Spoke = new Color32(242, 201, 76, 255), Hub = new Color32(61, 38, 22, 255);
        static readonly Vector2 WheelPivot = new Vector2(40, 60);
        static readonly float[] WheelEllipse = { 20f, 45f };

        /// <summary>An 8-spoke wheel drawn in circle space (radius 45: rim 41-45, hub 5) and squashed to the 20 x 45 ellipse.</summary>
        static PixelImage SideWheel()
        {
            var img = Img(80, 120);
            for (int y = 0; y < img.h; y++)
                for (int x = 0; x < img.w; x++)
                {
                    float cx = (x - WheelPivot.x) * 45f / 20f, cy = y - WheelPivot.y;
                    float r = Mathf.Sqrt(cx * cx + cy * cy);
                    if (r > 45.5f) continue;
                    if (r >= 41f) { img.Set(x, y, Rim); continue; }
                    if (r <= 5f) { img.Set(x, y, Hub); continue; }
                    float ang = Mathf.Atan2(cy, cx) * Mathf.Rad2Deg;
                    float off = Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 45f) * 45f)) * Mathf.Deg2Rad * r;
                    if (off <= 2.5f) img.Set(x, y, Spoke);
                }
            return img;
        }

        static int Diff(PixelImage a, PixelImage b)
        {
            int n = 0;
            for (int i = 0; i < a.px.Length; i++) { var p = a.px[i]; var q = b.px[i]; if ((p.a >= 128) != (q.a >= 128) || (p.a >= 128 && (p.r != q.r || p.g != q.g || p.b != q.b))) n++; }
            return n;
        }

        static int Opaque(PixelImage a) { int n = 0; foreach (var c in a.px) if (c.a >= 128) n++; return n; }

        [Test]
        public void EllipseSpin_Rest_Frame_Is_The_Layer_And_A_Turn_There_And_Back_Round_Trips()
        {
            var src = SideWheel();
            Assert.Greater(Opaque(src), 800);
            Assert.AreEqual(0, Diff(src, CodeAnimBake.EllipseTurn(src, WheelPivot, WheelEllipse, 0f, true)), "0 deg = the layer, pixel for pixel");
            Assert.AreEqual(0, Diff(src, CodeAnimBake.EllipseTurn(src, WheelPivot, WheelEllipse, 360f, true)), "a whole turn = the layer");
            foreach (float deg in new[] { 11.25f, 30f, 67.5f })
            {
                var back = CodeAnimBake.EllipseTurn(CodeAnimBake.EllipseTurn(src, WheelPivot, WheelEllipse, deg, false), WheelPivot, WheelEllipse, -deg, false);
                int d = Diff(src, back);
                Assert.LessOrEqual(d, Opaque(src) * 15 / 100, deg + " deg there and back (two nearest-neighbour lookups): " + d + " px differ");
            }
            // an 8-spoke wheel turned by one spoke spacing (45 deg in circle space) looks like the rest pose: the squash is undone
            // before the turn (a plain 2D turn of the ellipse tips it sideways and differs in far more texels)
            var turned = CodeAnimBake.EllipseTurn(src, WheelPivot, WheelEllipse, 45f, true);
            int same = Diff(src, turned), flat = Diff(src, CodeAnimBake.Transform(src, WheelPivot, 45f, 1f));
            UnityEngine.Debug.Log($"[EllipseSpinTest] opaque={Opaque(src)} 45deg ellipse={same} flat={flat}");
            Assert.LessOrEqual(same, Opaque(src) * 20 / 100, "45 deg ellipse turn of 8 spokes ~ rest: " + same + " px differ");
            Assert.Greater(flat, same * 5, "a flat 2D turn is far off (" + flat + " px vs " + same + ")");
        }

        [Test]
        public void EllipseSpin_Frames_Stay_Inside_The_Ellipse_Mask_And_Have_No_Pinholes()
        {
            var src = SideWheel();
            float radius = CodeAnimBake.EllipseRadius(src, WheelPivot, WheelEllipse);
            Assert.That(radius, Is.InRange(45f, 46.5f));
            var frames = CodeAnimBake.EllipseSpinFrames(src, WheelPivot, WheelEllipse, 16, true, out var crop);
            Assert.AreEqual(16, frames.Length);
            // tolerance: nearest-neighbour snapping of up to 0.75 texel on the squashed axis = 0.75 * 45/20 circle units
            float tol = 0.75f * 45f / 20f;
            for (int k = 0; k < frames.Length; k++)
            {
                var f = frames[k];
                Assert.AreEqual(crop.width, f.w); Assert.AreEqual(crop.height, f.h);
                int holes = 0;
                for (int y = 0; y < f.h; y++)
                    for (int x = 0; x < f.w; x++)
                    {
                        int sx = crop.x + x, sy = crop.y + y;
                        if (f.Get(x, y).a >= 128)
                            Assert.IsTrue(CodeAnimBake.InEllipse(WheelPivot, WheelEllipse, radius + tol, sx, sy), $"frame {k}: texel ({sx},{sy}) outside the wheel's ellipse");
                        else if (CodeAnimBake.InEllipse(WheelPivot, WheelEllipse, radius, sx, sy)
                                 && f.Get(x - 1, y).a >= 128 && f.Get(x + 1, y).a >= 128 && f.Get(x, y - 1).a >= 128 && f.Get(x, y + 1).a >= 128) holes++;
                    }
                Assert.AreEqual(0, holes, "frame " + k + ": 1-px holes are filled");
                Assert.That(Opaque(f), Is.InRange(Opaque(src) * 85 / 100, Opaque(src) * 115 / 100), "frame " + k + " keeps the wheel's size");
            }
            Assert.AreEqual(0, Diff(CodeAnimBake.Crop(src, crop), frames[0]), "frame 0 = the rest layer");
        }

        [Test]
        public void EllipseSpin_Bake_Is_Deterministic_And_Affine_Spec_Matches_Radii()
        {
            var src = SideWheel();
            var a = CodeAnimBake.EllipseSpinFrames(src, WheelPivot, WheelEllipse, 12, true, out var ca);
            var b = CodeAnimBake.EllipseSpinFrames(src, WheelPivot, WheelEllipse, 12, true, out var cb);
            Assert.AreEqual(ca, cb);
            for (int k = 0; k < a.Length; k++) Assert.AreEqual(0, Diff(a[k], b[k]), "frame " + k);
            // [rx, ry] is shorthand for the affine [rx/R, 0, 0, ry/R]
            var c = CodeAnimBake.EllipseSpinFrames(src, WheelPivot, new[] { 20f / 45f, 0f, 0f, 1f }, 12, true, out var cc);
            Assert.AreEqual(ca, cc);
            for (int k = 0; k < a.Length; k++) Assert.AreEqual(0, Diff(a[k], c[k]), "affine frame " + k);
            Assert.IsFalse(CodeAnimBake.EllipseMatrix(new[] { 0f, 0f, 0f, 0f }, out _), "degenerate spec");
        }

        [Test]
        public void EllipseSpin_Layer_Cycles_Its_Frames_On_The_Texel_Grid_And_Rests_When_Calm()
        {
            var wheelImg = new PixelImage(64, 96);
            var w = SideWheel();
            for (int y = 0; y < 96; y++) for (int x = 0; x < 64; x++) wheelImg.Set(x, y, w.Get(x + 8, y + 12));   // pivot (32, 48) on the 64x96 canvas
            var wt = new Texture2D(64, 96, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point }; trash.Add(wt);   // readable (no GPU readback in batch mode)
            var flip = new Color32[64 * 96];
            for (int y = 0; y < 96; y++) System.Array.Copy(wheelImg.px, y * 64, flip, (95 - y) * 64, 64);
            wt.SetPixels32(flip); wt.Apply();
            var layer = Sprite.Create(wt, new Rect(0, 0, 64, 96), new Vector2(0.5f, 0f), 64f, 0, SpriteMeshType.FullRect); trash.Add(layer);
            var bas = Still(64, 96, 64f);
            const string json = "{\"type\":\"layer\",\"name\":\"wheel\",\"sprite\":\"wheel\",\"mode\":\"ellipseSpin\",\"pivot\":[32,48],\"ellipse\":[20,45],\"spin\":-90,\"count\":16}";
            var ca = Animator(json, out var sr, out _, 64f, s => s == "layer_wheel" ? layer : s == "base" ? bas : null);
            Assert.AreEqual(0, ca.MissingLayers.Count);
            Assert.AreEqual(1, ca.SlotCount);
            var r = ca.SlotRenderer(0);

            Juice.SetReduceMotion(false);
            var seen = new List<Sprite>();
            for (float t = 0.01f; t < 4f; t += 1f / 24f)       // 90 deg/s, 16 frames of 22.5 deg: 4 frames per second
            {
                ca.Tick(t);
                Assert.IsTrue(r.enabled);
                var p = r.transform.localPosition * 64f;
                Assert.AreEqual(Mathf.Round(p.x), p.x, 1e-3f); Assert.AreEqual(Mathf.Round(p.y), p.y, 1e-3f);
                if (!seen.Contains(r.sprite)) seen.Add(r.sprite);
            }
            Assert.AreEqual(16, seen.Count, "a full turn in 4 s shows all 16 frames");
            ca.Tick(0.3f); var s1 = r.sprite; ca.Tick(0.3f + 0.25f); var s2 = r.sprite;
            Assert.AreNotSame(s1, s2, "a quarter second later the wheel has turned one frame");

            Juice.SetReduceMotion(true);
            for (float t = 0f; t < 3f; t += 0.37f) { ca.Tick(t); StringAssert.EndsWith("_spin0", r.sprite.name, "calm motion holds the rest frame"); }
        }
    }
}
