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
                if (e.PlaysWhileSurfaced) Assert.IsTrue(e.key.StartsWith("node_"), e.key + ": only surfacing nodes play while surfaced");
                else Assert.AreEqual(e.key.StartsWith("npc_") || e.key.StartsWith("fix_") || e.key.StartsWith("node_") || e.key.StartsWith("deco_"), e.PlaysAlways, e.key + ": NPCs, fixtures, nodes and decor idle always, buildings only while working");
                if (e.overlayOnly)
                    foreach (var x in e.effects) Assert.IsFalse(x.IsBaked || x.kind == CodeAnimKind.Layer, e.key + "/" + x.name + ": overlay-only entries draw overlays only");
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
                    if (x.OnGround) { Assert.AreEqual("hide", x.calm, id + ": no ripples in calm motion"); Assert.That(x.points, Is.Not.Null.And.Length.EqualTo(2), id + ": one waterline centre"); }
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

        // ------------------------------------------------------------ step C nodes: overlay-only states, ripples, the spring

        static readonly string ProjectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));

        /// <summary>A PNG file as a top-left PixelImage (CPU decode: works in batch mode without a graphics device).</summary>
        PixelImage Png(string relPath)
        {
            string p = System.IO.Path.Combine(ProjectRoot, relPath);
            if (!System.IO.File.Exists(p)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false); trash.Add(t);
            Assert.IsTrue(t.LoadImage(System.IO.File.ReadAllBytes(p)), relPath);
            var raw = t.GetPixels32();
            var img = new PixelImage(t.width, t.height);
            for (int y = 0; y < t.height; y++) System.Array.Copy(raw, (t.height - 1 - y) * t.width, img.px, y * t.width, t.width);
            return img;
        }

        /// <summary>Readable sprites of a w×h image (frames side by side), bottom-centre pivot like the integrated strips.</summary>
        Sprite[] Frames(PixelImage img, int fw, float ppu, string name)
        {
            var t = new Texture2D(img.w, img.h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point }; trash.Add(t);
            var flip = new Color32[img.w * img.h];
            for (int y = 0; y < img.h; y++) System.Array.Copy(img.px, y * img.w, flip, (img.h - 1 - y) * img.w, img.w);
            t.SetPixels32(flip); t.Apply();
            var a = new Sprite[img.w / fw];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = Sprite.Create(t, new Rect(i * fw, 0, fw, img.h), new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect);
                a[i].name = a.Length > 1 ? name + "_" + i : name; trash.Add(a[i]);
            }
            return a;
        }

        /// <summary>Two 64x96 state frames: frame 1 = frame 0 with a dark 2-px "crack" through the blue band rows.</summary>
        Sprite[] StateFrames()
        {
            var img = new PixelImage(128, 96);
            for (int y = 0; y < 96; y++) for (int x = 0; x < 64; x++)
                {
                    Color32 c = x > 8 && x < 56 && y < 86 ? (y % 7 == 0 ? Blue : (x % 5 == 0 ? White : Red)) : new Color32(0, 0, 0, 0);
                    img.Set(x, y, c);
                    if (c.a == 255 && (x == 30 || x == 31) && y > 20 && y < 60) c = new Color32(13, 15, 20, 255);
                    img.Set(64 + x, y, c);
                }
            return Frames(img, 64, 64f, "node_test_64x96_2f@64");
        }

        const string StateFx =
            "{\"type\":\"glow\",\"name\":\"g\",\"rect\":[10,10,44,70],\"masks\":[{\"hue\":[190,215],\"sat\":[0.4,1],\"val\":[0.3,1]}],\"colorA\":\"#5ED4E8\",\"colorB\":\"#F2C94C\",\"alpha\":[0,0.5],\"period\":1.1,\"additive\":true}," +
            "{\"type\":\"flicker\",\"name\":\"gl\",\"sprite\":\"glint\",\"size\":[5,5],\"mode\":\"cycle\",\"points\":[20,30,40,50],\"onSteps\":[4],\"colorA\":\"#E8ECF3\",\"alpha\":[0.75,0.75],\"calm\":\"hide\"}," +
            "{\"type\":\"sway\",\"name\":\"ignored\",\"rect\":[10,10,40,40],\"pivot\":[30,49],\"offsets\":[0,2,0,-2]}";

        [Test]
        public void OverlayOnly_Keeps_The_Hit_Frame_And_Its_Cuts_Follow_The_State()
        {
            Juice.SetReduceMotion(false);
            var f = CodeAnimLibrary.Parse("{\"entries\":[{\"key\":\"node_test\",\"still\":[64,96],\"fps\":4,\"steps\":4,\"play\":\"always\",\"overlayOnly\":true,\"effects\":[" + StateFx + "]}]}");
            var frames = StateFrames();
            var go = new GameObject("codeanim-state"); trash.Add(go);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = frames[0];
            var ca = CodeAnimator.For(sr);
            Assert.IsTrue(ca.ConfigureOverlayUncached(f.entries[0], sr, frames));
            Assert.IsTrue(ca.OverlayOnly);
            Assert.AreEqual(0, ca.BaseFrameCount, "no baked frames in overlay-only mode (the sway is ignored)");
            Assert.AreEqual(1 + 2, ca.SlotCount, "glow + 2 glint points");
            SpriteRenderer glow = null;
            ca.Tick(0.4f);
            for (int i = 0; i < ca.SlotCount; i++) if (ca.SlotRenderer(i).sprite != null && ca.SlotRenderer(i).sprite.name.Contains("_g_s")) glow = ca.SlotRenderer(i);
            Assert.IsNotNull(glow, "glow slot");

            // intact: the owner's frame 0 stays, the glow is the cut of frame 0
            for (float t = 0f; t < 3f; t += 0.11f) { ca.Tick(t); Assert.AreSame(frames[0], sr.sprite, "intact frame kept at t=" + t); }
            ca.Tick(0.4f); StringAssert.EndsWith("_g_s0", glow.sprite.name);
            // first hit: the owner shows frame 1; no tick puts frame 0 back, and the glow follows the crack (cut of frame 1)
            sr.sprite = frames[1];
            bool glintSeen = false;
            for (float t = 3f; t < 9f; t += 0.07f)
            {
                ca.Tick(t);
                Assert.AreSame(frames[1], sr.sprite, "hit frame survives the tick at t=" + t);
                for (int i = 0; i < ca.SlotCount; i++)
                {
                    var r = ca.SlotRenderer(i);
                    if (!r.enabled) continue;
                    var p = r.transform.localPosition * 64f;
                    Assert.AreEqual(Mathf.Round(p.x), p.x, 1e-3f); Assert.AreEqual(Mathf.Round(p.y), p.y, 1e-3f);
                    if (r.sprite.name.Contains("_gl")) glintSeen = true;
                }
                if (glow.enabled) StringAssert.EndsWith("_g_s1", glow.sprite.name, "glow cut of the hit frame");
            }
            Assert.IsTrue(glintSeen, "glints run on the hit frame too");
            // the crack pixels are not part of the hit frame's glow cut (they are dark rock now): glow rect starts at (10, 10)
            // (the cut sprites are not readable without a graphics device: check the same cut on the CPU images)
            var gfx = f.entries[0].effects[0];
            var i0 = PixelImage.FromSprite(frames[0]); var i1 = PixelImage.FromSprite(frames[1]);
            var gr = CodeAnimBake.Rect(gfx, i0);
            var cut0 = CodeAnimBake.Cut(gfx, i0, gr, false); var cut1 = CodeAnimBake.Cut(gfx, i1, gr, false);
            Assert.AreEqual(255, cut1.Get(20 - 10, 21 - 10).a, "blue band pixel glows");
            Assert.AreEqual(0, cut1.Get(30 - 10, 21 - 10).a, "cracked pixel does not glow in the hit frame");
            Assert.AreEqual(255, cut0.Get(30 - 10, 21 - 10).a, "the same pixel glows on the intact frame");
            sr.sprite = frames[0]; ca.Tick(0.4f);
            StringAssert.EndsWith("_g_s0", glow.sprite.name, "back to the intact cut when the owner shows frame 0 again");
            Assert.AreEqual(glow.sprite.rect.size, new Vector2(gr.width, gr.height));

            // not playing / calm / Clear never write the sprite either
            sr.sprite = frames[1];
            ca.Playing = false; ca.Tick(10f); Assert.AreSame(frames[1], sr.sprite);
            Juice.SetReduceMotion(true);
            for (float t = 0f; t < 2f; t += 0.2f) { ca.Tick(t); Assert.AreSame(frames[1], sr.sprite, "calm"); }
            for (int i = 0; i < ca.SlotCount; i++) if (ca.SlotRenderer(i).enabled) Assert.IsFalse(ca.SlotRenderer(i).sprite.name.Contains("_gl"), "no glints when calm");
            ca.Clear(); Assert.AreSame(frames[1], sr.sprite, "Clear keeps the owner's frame");
        }

        [Test]
        public void OverlayOnly_Entry_Through_Configure_Never_Writes_The_Sprite()
        {
            Juice.SetReduceMotion(false);
            var f = CodeAnimLibrary.Parse("{\"entries\":[{\"key\":\"node_test\",\"still\":[64,96],\"play\":\"always\",\"overlayOnly\":true,\"effects\":[" + StateFx + "]}]}");
            var frames = StateFrames();
            var go = new GameObject("codeanim-state2"); trash.Add(go);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = frames[0];
            var ca = CodeAnimator.For(sr);
            Assert.IsTrue(ca.Configure(f.entries[0], sr, frames[0], null), "Configure routes an overlayOnly entry to the overlay mode");
            Assert.IsTrue(ca.OverlayOnly);
            sr.sprite = frames[1];
            for (float t = 0f; t < 4f; t += 0.13f) { ca.Tick(t); Assert.AreSame(frames[1], sr.sprite); }
            ca.Clear();
        }

        [Test]
        public void Sprite_Names_Give_The_Key_And_Only_A_Matching_Still_Finds_Its_Entry()
        {
            Assert.AreEqual("node_ironvein", CodeAnimLibrary.KeyFromSpriteName("node_ironvein_128x160_2f@64_0"));
            Assert.AreEqual("node_ore_1x1", CodeAnimLibrary.KeyFromSpriteName("node_ore_1x1_64x96_2f@64_1"));
            Assert.AreEqual("fix_spring_snow", CodeAnimLibrary.KeyFromSpriteName("fix_spring_snow@64"));
            Assert.AreEqual("fix_spring_snow", CodeAnimLibrary.KeyFromSpriteName("fix_spring_snow"));
            Assert.AreEqual("node_ironvein", CodeAnimLibrary.KeyFromSpriteName("node_ironvein_64x80_2f_1"));

            Sprite Named(int w, int h, string n) { var s = Still(w, h, 64f); s.name = n; return s; }
            // the Fishing spring: art key fix_spring (Island swap), 32-px still keeps its flow loop, the @64 still gets the code
            Assert.IsNull(CodeAnimLibrary.FindForStill("fix_spring", Named(64, 80, "fix_spring_snow")), "32-px spring: no code animation");
            Assert.AreEqual("fix_spring_snow", CodeAnimLibrary.FindForStill("fix_spring", Named(128, 160, "fix_spring_snow@64"))?.key);
            Assert.AreEqual("node_ore_1x1", CodeAnimLibrary.FindForStill("node_ore", Named(64, 96, "node_ore_1x1_64x96_2f@64_0"))?.key);
            Assert.AreEqual("node_ore_2x2", CodeAnimLibrary.FindForStill("node_ore_2x2", Named(128, 160, "node_ore_2x2_128x160_2f@64_0"))?.key);
            Assert.IsNull(CodeAnimLibrary.FindForStill("node_ironvein", Named(64, 80, "node_ironvein_64x80_2f_0")), "32-px vein: no code animation");
            Assert.AreEqual("node_crop", CodeAnimLibrary.FindForStill("node_crop", Named(192, 224, "node_crop@64"))?.key, "art-key lookup still works");
        }

        const string WaterFx =
            "{\"type\":\"ripple\",\"name\":\"rings\",\"points\":[31,88],\"size\":[40,10],\"growTo\":[64,20],\"count\":2,\"period\":1.6,\"alphaSteps\":[0.75,0.5,0.5,0.25],\"colorA\":\"#C4F6FF\"}," +
            "{\"type\":\"splash\",\"name\":\"splash\",\"points\":[31,84],\"size\":[2,2],\"count\":4,\"period\":1.6,\"life\":0.5,\"rise\":10,\"drift\":18,\"colorA\":\"#E8ECF3\",\"sort\":1}";

        [Test]
        public void Ripple_Rings_Grow_On_The_Grid_Stay_On_The_Water_And_Rest_When_Calm()
        {
            Juice.SetReduceMotion(false);
            var f = CodeAnimLibrary.Parse("{\"entries\":[{\"key\":\"node_test\",\"still\":[64,96],\"fps\":8,\"play\":\"surfaced\",\"effects\":[" + WaterFx + "]}]}");
            var e = f.entries[0];
            Assert.IsTrue(e.PlaysWhileSurfaced); Assert.IsFalse(e.PlaysAlways);
            Assert.AreEqual(CodeAnimKind.Ripple, e.effects[0].kind); Assert.AreEqual(CodeAnimKind.Splash, e.effects[1].kind);
            Assert.IsTrue(e.effects[0].OnGround); Assert.Less(e.effects[0].CalmAlpha, 0f, "ripples hide when calm");
            var go = new GameObject("codeanim-ripple"); trash.Add(go);
            var sr = go.AddComponent<SpriteRenderer>(); var still = Still(64, 96, 64f); sr.sprite = still; sr.sortingOrder = 5;
            var ca = CodeAnimator.For(sr);
            Assert.IsTrue(ca.ConfigureUncached(e, sr, still, null));
            Assert.AreEqual(2 * 2 + 4, ca.SlotCount, "2 rings x (back + front) + 4 droplets");

            // not surfaced: nothing on the water
            ca.Playing = false;
            for (float t = 0f; t < 2f; t += 0.1f) { ca.Tick(t); for (int i = 0; i < ca.SlotCount; i++) Assert.IsFalse(ca.SlotRenderer(i).enabled); }

            ca.Playing = true;
            var widths = new HashSet<float>();
            int drops = 0, dropsUp = 0;
            for (float t = 0.01f; t < 6.4f; t += 0.05f)
            {
                ca.Tick(t);
                for (int i = 0; i < ca.SlotCount; i++)
                {
                    var r = ca.SlotRenderer(i);
                    if (!r.enabled) continue;
                    var p = r.transform.localPosition * 64f;
                    Assert.AreEqual(Mathf.Round(p.x), p.x, 1e-3f, r.sprite.name); Assert.AreEqual(Mathf.Round(p.y), p.y, 1e-3f, r.sprite.name);
                    Assert.AreEqual(Mathf.Round(r.sprite.pivot.x), r.sprite.pivot.x, 1e-3f); Assert.AreEqual(Mathf.Round(r.sprite.pivot.y), r.sprite.pivot.y, 1e-3f);
                    float a = r.color.a;
                    Assert.That(a, Is.EqualTo(64f / 255f).Within(1e-4f).Or.EqualTo(128f / 255f).Within(1e-4f).Or.EqualTo(191f / 255f).Within(1e-4f).Or.EqualTo(1f).Within(1e-4f), r.sprite.name + " alpha " + a);
                    if (r.sprite.name.Contains("_ring"))
                    {
                        widths.Add(r.sprite.rect.width);
                        bool back = r.sprite.name.EndsWith("b");
                        if (back) Assert.Less(r.sortingOrder, sr.sortingOrder, "back half behind the still"); else Assert.Greater(r.sortingOrder, sr.sortingOrder, "front half in front");
                        Assert.AreEqual(new Vector3((31 - 32) / 64f, (96 - 88) / 64f, 0f), r.transform.localPosition, "ring centred on the waterline texel");
                    }
                    else { drops++; if (p.y > (96 - 84) + 0.5f) dropsUp++; }
                }
            }
            Assert.AreEqual(4, widths.Count, "rings grow through 4 sizes");
            Assert.Greater(drops, 0); Assert.Greater(dropsUp, 0, "droplets hop above the water");

            // the still bobs: water overlays stay put (GroundLift moves them down by the lift, in whole texels)
            ca.Tick(0.3f);
            var at = new Vector3[ca.SlotCount]; var on = new bool[ca.SlotCount];
            for (int i = 0; i < ca.SlotCount; i++) { at[i] = ca.SlotRenderer(i).transform.localPosition; on[i] = ca.SlotRenderer(i).enabled; }
            ca.GroundLift = 3f / 64f; ca.Tick(0.3f);
            int moved = 0;
            for (int i = 0; i < ca.SlotCount; i++)
                if (on[i]) { Assert.AreEqual(at[i] - new Vector3(0f, 3f / 64f, 0f), ca.SlotRenderer(i).transform.localPosition, "lift 3 texels -> overlay 3 texels down"); moved++; }
            Assert.Greater(moved, 0);
            ca.GroundLift = 0.0371f; ca.Tick(0.3f);
            for (int i = 0; i < ca.SlotCount; i++)
                if (ca.SlotRenderer(i).enabled) { var p = ca.SlotRenderer(i).transform.localPosition * 64f; Assert.AreEqual(Mathf.Round(p.y), p.y, 1e-3f, "lift snaps to the texel grid"); }
            ca.GroundLift = 0f;

            Juice.SetReduceMotion(true);
            for (float t = 0f; t < 3f; t += 0.1f) { ca.Tick(t); for (int i = 0; i < ca.SlotCount; i++) Assert.IsFalse(ca.SlotRenderer(i).enabled, "calm: no ripples, no splash"); }
        }

        [Test]
        public void Ring_And_Drop_Sprites_Are_Thin_Connected_Outlines()
        {
            foreach (var (w, h) in new[] { (60, 16), (84, 26), (41, 11) })
            {
                var back = CodeAnimBake.Ring(w, h, false); var front = CodeAnimBake.Ring(w, h, true);
                int nb = 0, nf = 0;
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                    {
                        bool b = back.Get(x, y).a == 255, fr = front.Get(x, y).a == 255;
                        Assert.IsFalse(b && fr, "halves do not overlap");
                        if (b) { nb++; Assert.Less(y, h / 2); }
                        if (fr) { nf++; Assert.GreaterOrEqual(y, h / 2); }
                    }
                Assert.GreaterOrEqual(nb, w); Assert.GreaterOrEqual(nf, w);
                Assert.AreEqual(0, front.Get(w / 2, h - 3).a, "hollow: no fill inside the ring");
                Assert.AreEqual(255, front.Get(w / 2, h - 1).a, "front rim on the bottom row");
                Assert.AreEqual(255, back.Get(w / 2, 0).a, "back rim on the top row");
            }
            var d = CodeAnimBake.Drop(3, 3);
            Assert.AreEqual(0, d.Get(0, 0).a); Assert.AreEqual(255, d.Get(1, 0).a); Assert.AreEqual(255, d.Get(0, 2).a);
            var g = CodeAnimBake.Glint(5);
            Assert.AreEqual(255, g.Get(2, 2).r); Assert.AreEqual(0, g.Get(0, 0).a);
        }

        [Test]
        public void StepC_Glints_Sit_On_Unchanged_Rock_And_Glows_Have_Pixels_In_Both_Frames()
        {
            var files = new Dictionary<string, string>
            {
                { "node_ore_1x1", "node_ore_1x1_64x96_2f@64.png" }, { "node_ore_2x2", "node_ore_2x2_128x160_2f@64.png" },
                { "node_ironvein", "node_ironvein_128x160_2f@64.png" }, { "node_jadevein", "node_jadevein_128x160_2f@64.png" },
            };
            foreach (var kv in files)
            {
                var e = CodeAnimLibrary.Find(kv.Key);
                Assert.IsNotNull(e, kv.Key); Assert.IsTrue(e.overlayOnly, kv.Key + " overlay-only"); Assert.IsTrue(e.PlaysAlways, kv.Key);
                var strip = Png("Assets/_Project/Art/Incoming/nodes/" + kv.Value);
                if (strip == null) Assert.Inconclusive("art missing: " + kv.Value);
                Assert.AreEqual(e.StillW * 2, strip.w, kv.Key + ": 2-frame strip of the still size"); Assert.AreEqual(e.StillH, strip.h);
                Color32 At(int frame, int x, int y) => strip.Get(frame * e.StillW + x, y);
                foreach (var x in e.effects)
                {
                    if (x.kind == CodeAnimKind.Flicker && x.points != null)
                        for (int i = 0; i < x.points.Length; i += 2)
                        {
                            int cx = x.points[i], cy = x.points[i + 1] - x.size[1] / 2 - 1;   // glint centre (anchor = bottom-centre of a 5x5 glint)
                            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                                {
                                    var a = At(0, cx + dx, cy + dy); var b = At(1, cx + dx, cy + dy);
                                    Assert.AreEqual(255, a.a, $"{kv.Key} glint {i / 2} on the rock (intact)");
                                    Assert.AreEqual(a, b, $"{kv.Key} glint {i / 2} off the crack (same pixels in both frames)");
                                }
                        }
                    if (x.kind == CodeAnimKind.Glow)
                        for (int fr = 0; fr < 2; fr++)
                        {
                            int n = 0;
                            for (int y = x.rect[1]; y < x.rect[1] + x.rect[3]; y++) for (int xx = x.rect[0]; xx < x.rect[0] + x.rect[2]; xx++) if (x.InMask(At(fr, xx, y))) n++;
                            Assert.Greater(n, 40, $"{kv.Key}/{x.name}: glow pixels in frame {fr}");
                        }
                }
            }
        }

        [Test]
        public void StepC_Fish_Algae_And_Spring_Entries_Animate_Their_At64_Stills()
        {
            Juice.SetReduceMotion(false);
            foreach (var key in new[] { "node_fish", "node_algae" })
            {
                var e = CodeAnimLibrary.Find(key);
                Assert.IsNotNull(e, key); Assert.IsTrue(e.PlaysWhileSurfaced, key + " ripples only while surfaced");
                bool ripple = false; foreach (var x in e.effects) if (x.kind == CodeAnimKind.Ripple) ripple = true;
                Assert.IsTrue(ripple, key + " has ripple rings");
                var img = Png("Assets/_Project/Art/Incoming/nodes/" + key + "@64.png");
                if (img == null) Assert.Inconclusive("art missing: " + key);
                var still = Frames(img, img.w, 64f, key + "@64")[0];
                Assert.AreSame(e, CodeAnimLibrary.FindForStill(key, still));
                var go = new GameObject("codeanim-" + key); trash.Add(go);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = still;
                var ca = CodeAnimator.For(sr);
                Assert.IsTrue(ca.ConfigureUncached(e, sr, still, null));
                ca.Playing = true; int shown = 0;
                for (float t = 0f; t < 3f; t += 0.1f) { ca.Tick(t); Assert.AreSame(still, sr.sprite); for (int i = 0; i < ca.SlotCount; i++) if (ca.SlotRenderer(i).enabled) shown++; }
                Assert.Greater(shown, 20, key + ": rings on the water");
            }

            // the snowy spring: the 32-px still keeps its flow loop; the @64 entry animates a 128x160 still. The accepted
            // still is pending (Fixtures-64-004), so the rejected v1 (same canvas, same water) stands in for the bake check.
            var sp = CodeAnimLibrary.Find("fix_spring_snow");
            Assert.IsNotNull(sp); Assert.IsTrue(sp.PlaysAlways);
            Assert.AreEqual(128, sp.StillW); Assert.AreEqual(160, sp.StillH);
            var v32 = Png("Assets/_Project/Art/Incoming/fixtures/fix_spring_snow.png");
            if (v32 != null) Assert.IsNull(CodeAnimLibrary.FindForStill("fix_spring", Frames(v32, v32.w, 32f, "fix_spring_snow")[0]), "32-px spring keeps its loop");
            var rej = Png("docs/art/rejected/fix_spring_snow@64_v1_REJECTED.png");
            if (rej == null) Assert.Inconclusive("rejected spring file missing");
            var s64 = Frames(rej, rej.w, 64f, "fix_spring_snow@64")[0];
            Assert.AreSame(sp, CodeAnimLibrary.FindForStill("fix_spring", s64));
            var go2 = new GameObject("codeanim-spring"); trash.Add(go2);
            var sr2 = go2.AddComponent<SpriteRenderer>(); sr2.sprite = s64;
            var ca2 = CodeAnimator.For(sr2);
            Assert.IsTrue(ca2.ConfigureUncached(sp, sr2, s64, null));
            Assert.AreEqual(sp.steps, ca2.BaseFrameCount, "water scroll baked into the frames");
            // the same bake on the CPU (baked frames are not readable without a graphics device)
            var f0 = new PixelImage(rej); var f1 = new PixelImage(rej);
            foreach (var x in sp.effects) if (x.IsBaked) { CodeAnimBake.ApplyBaked(x, f0, 0); CodeAnimBake.ApplyBaked(x, f1, 1); }
            int diff = 0, outside = 0;
            for (int y = 0; y < 160; y++) for (int x = 0; x < 128; x++)
                    if (!f0.Get(x, y).Equals(f1.Get(x, y))) { diff++; if (y < 24 || y >= 97) outside++; }
            Assert.Greater(diff, 50, "basin water moves"); Assert.AreEqual(0, outside, "only the basin and the spout move");
            int puffs = 0;
            ca2.Playing = true;
            for (float t = 0f; t < 4f; t += 0.1f) { ca2.Tick(t); for (int i = 0; i < ca2.SlotCount; i++) if (ca2.SlotRenderer(i).enabled && ca2.SlotRenderer(i).sprite.name.Contains("_puff")) puffs++; }
            Assert.Greater(puffs, 10, "steam rises");
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

        // ------------------------------------------------------------ step D decor: variants, sizes, mirror (ART-SPEC §3.9.8)

        [Test]
        public void Decor_Sprite_Names_Give_Key_Size_And_Variant()
        {
            void Is(string name, string key, string sized, int frame)
            {
                CodeAnimLibrary.ParseSpriteName(name, out var k, out var s, out var f);
                Assert.AreEqual(key, k, name); Assert.AreEqual(sized, s, name); Assert.AreEqual(frame, f, name);
            }
            Is("deco_snow_props_128x128_4f@64_2", "deco_snow_props", "deco_snow_props_128x128", 2);
            Is("deco_snow_props_64x96_8f@64_0", "deco_snow_props", "deco_snow_props_64x96", 0);
            Is("deco_tree_32x48_3f_1", "deco_tree", "deco_tree_32x48", 1);
            Is("deco_tree_64x96_3f@64", "deco_tree", "deco_tree_64x96", -1);
            Is("fix_spring_snow@64", "fix_spring_snow", null, -1);
            Is("node_ore_1x1_64x96_2f@64_1", "node_ore_1x1", "node_ore_1x1_64x96", 1);
            Is("deco_tree_code_0", "deco_tree_code_0", null, -1);
        }

        [Test]
        public void Decor_StepD_Entries_Wait_For_Their_At64_Frames()
        {
            Sprite Named(int w, int h, string n) { var s = Still(w, h, 64f); s.name = n; return s; }
            // the 64 entries: only a frame of the @64 size finds them; a variant entry ("#0") wins over its key
            Assert.AreEqual("deco_tree", CodeAnimLibrary.FindForStill("deco_tree", Named(64, 96, "deco_tree_64x96_3f@64_2"))?.key);
            Assert.AreEqual("deco_pine_snow", CodeAnimLibrary.FindForStill("deco_pine_snow", Named(64, 96, "deco_pine_snow_64x96_3f@64_1"))?.key);
            Assert.AreEqual("deco_snow_props_64x96#0", CodeAnimLibrary.FindForStill("deco_snow_props", Named(64, 96, "deco_snow_props_64x96_8f@64_0"))?.key, "the stone lantern");
            Assert.AreEqual("deco_snow_props_64x96", CodeAnimLibrary.FindForStill("deco_snow_props", Named(64, 96, "deco_snow_props_64x96_8f@64_3"))?.key);
            Assert.AreEqual("deco_snow_props_128x128", CodeAnimLibrary.FindForStill("deco_snow_props", Named(128, 128, "deco_snow_props_128x128_4f@64_1"))?.key);
            // the 32-px decor of today keeps still: nothing changes until step D lands
            Assert.IsNull(CodeAnimLibrary.FindForStill("deco_snow_props", Named(32, 48, "deco_snow_props_32x48_8f_0")));
            Assert.IsNull(CodeAnimLibrary.FindForStill("deco_snow_props", Named(64, 64, "deco_snow_props_64x64_4f_0")));
            foreach (var file in new[] { "deco_tree_32x48_3f", "deco_pine_snow_32x48_3f", "deco_snow_props_32x48_8f", "deco_snow_props_64x64_4f" })
            {
                var img = Png("Assets/_Project/Art/Incoming/fixtures/" + file + ".png");
                if (img == null) continue;
                int fw = file.Contains("64x64") ? 64 : 32;
                foreach (var f in Frames(img, fw, 32f, file))
                    Assert.IsNull(CodeAnimLibrary.FindForStill(file.Substring(0, file.IndexOf("_" + fw + "x")), f), f.name + ": no code motion on the 32-px decor");
            }
            foreach (var key in new[] { "deco_tree", "deco_pine_snow", "deco_snow_props_64x96", "deco_snow_props_64x96#0", "deco_snow_props_128x128" })
            {
                var e = CodeAnimLibrary.Find(key);
                Assert.IsNotNull(e, key); Assert.IsTrue(e.PlaysAlways, key);
                foreach (var x in e.effects)
                    if (x.kind == CodeAnimKind.Flicker && x.sprite == "glint") Assert.AreEqual("hide", x.calm, key + ": no glints in calm motion");
            }
        }

        /// <summary>The temporary 32-px test entry of the brief: proves the decor hook on today's deco_tree (not shipped).</summary>
        const string Decor32Entry =
            "{\"entries\":[{\"key\":\"deco_tree\",\"still\":[32,48],\"fps\":2,\"steps\":4,\"play\":\"always\",\"effects\":[" +
            "{\"type\":\"sway\",\"name\":\"crown sway\",\"rect\":[0,0,32,36],\"pivot\":[16,36],\"axis\":\"x\",\"offsets\":[0,1,0,-1]}," +
            "{\"type\":\"glint\",\"name\":\"glints\",\"sprite\":\"glint\",\"size\":[3,3],\"count\":3,\"rect\":[0,0,32,36],\"masks\":[{\"val\":[0.45,1]}],\"calm\":\"hide\",\"sort\":1}]}]}";

        [Test]
        public void Decor_Test_Entry_Sways_Every_32px_Variant_And_Mirrors_With_The_Tree()
        {
            var img = Png("Assets/_Project/Art/Incoming/fixtures/deco_tree_32x48_3f.png");
            if (img == null) Assert.Inconclusive("art missing: deco_tree_32x48_3f");
            CodeAnimLibrary.Use(CodeAnimLibrary.Parse(Decor32Entry));
            var frames = Frames(img, 32, 32f, "deco_tree_32x48_3f");
            Assert.AreEqual(3, frames.Length);
            Juice.SetReduceMotion(false);
            var e = CodeAnimLibrary.Find("deco_tree");
            for (int v = 0; v < frames.Length; v++)
            {
                Assert.AreSame(e, CodeAnimLibrary.FindForStill("deco_tree", frames[v]), "variant " + v);
                // CPU bake of the same frames: the crown moves, the trunk foot and roots (rows >= 36) never do
                var src = new PixelImage(32, 48);
                for (int y = 0; y < 48; y++) for (int x = 0; x < 32; x++) src.Set(x, y, img.Get(v * 32 + x, y));
                var f1 = new PixelImage(src); var f3 = new PixelImage(src);
                foreach (var x in e.effects) if (x.IsBaked) { CodeAnimBake.ApplyBaked(x, f1, 1); CodeAnimBake.ApplyBaked(x, f3, 3); }
                int moved = 0, below = 0;
                for (int y = 0; y < 48; y++) for (int x = 0; x < 32; x++)
                        if (!f1.Get(x, y).Equals(src.Get(x, y)) || !f3.Get(x, y).Equals(src.Get(x, y))) { moved++; if (y >= 36) below++; }
                Assert.Greater(moved, 10, "variant " + v + ": the crown sways");
                Assert.AreEqual(0, below, "variant " + v + ": trunk foot and roots stay put");

                // the animator on a plain and on a mirrored tree: same frames, overlays at mirrored x and flipped
                var go = new GameObject("deco-" + v); trash.Add(go);
                var a = go.AddComponent<SpriteRenderer>(); a.sprite = frames[v];
                var go2 = new GameObject("deco-mirror-" + v); trash.Add(go2);
                var b = go2.AddComponent<SpriteRenderer>(); b.sprite = frames[v]; b.flipX = true;
                var ca = CodeAnimator.For(a); var cb = CodeAnimator.For(b);
                Assert.IsTrue(ca.Configure(e, a, frames[v], null)); Assert.IsTrue(cb.Configure(e, b, frames[v], null));     // shared bake per variant
                Assert.AreEqual(4, ca.BaseFrameCount, "4 baked sway frames per variant");
                Assert.AreEqual(3, ca.SlotCount, "3 glints picked from the variant's own light pixels");
                int shown = 0;
                for (float t = 0.05f; t < 8f; t += 0.25f)
                {
                    ca.Tick(t); cb.Tick(t);
                    Assert.AreSame(a.sprite, b.sprite, "the mirrored tree shows the same baked frame (its renderer flips it)");
                    Assert.IsTrue(b.flipX);
                    for (int i = 0; i < ca.SlotCount; i++)
                    {
                        var ra = ca.SlotRenderer(i); var rb = cb.SlotRenderer(i);
                        Assert.AreEqual(ra.enabled, rb.enabled);
                        if (!ra.enabled) continue;
                        shown++;
                        Assert.IsFalse(ra.flipX); Assert.IsTrue(rb.flipX, "overlay flips with the tree");
                        Assert.AreEqual(-ra.transform.localPosition.x, rb.transform.localPosition.x, 1e-5f, "overlay at the mirrored x");
                        Assert.AreEqual(ra.transform.localPosition.y, rb.transform.localPosition.y, 1e-5f);
                        var p = ra.transform.localPosition * 32f;
                        Assert.AreEqual(Mathf.Round(p.x), p.x, 1e-3f); Assert.AreEqual(Mathf.Round(p.y), p.y, 1e-3f);
                    }
                }
                Assert.Greater(shown, 5, "variant " + v + ": glints show");
                // calm motion holds the rest frame (the still) and no glints
                Juice.SetReduceMotion(true);
                for (float t = 0f; t < 4f; t += 0.3f)
                {
                    ca.Tick(t);
                    Assert.AreSame(ca.BaseFrame(0), a.sprite);
                    for (int i = 0; i < ca.SlotCount; i++) Assert.IsFalse(ca.SlotRenderer(i).enabled, "no glints when calm");
                }
                Juice.SetReduceMotion(false);
            }
        }

        [Test]
        public void Glints_Without_Points_Pick_Texels_Of_The_Mask()
        {
            var t = new Texture2D(64, 96, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point }; trash.Add(t);
            var px = new Color32[64 * 96];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(40, 90, 50, 255);
            // 4 snow texels (top-left origin) -> Unity rows are bottom-up
            var snow = new[] { new Vector2Int(10, 5), new Vector2Int(40, 20), new Vector2Int(20, 50), new Vector2Int(50, 70) };
            foreach (var s in snow) px[(95 - s.y) * 64 + s.x] = new Color32(232, 236, 243, 255);
            t.SetPixels32(px); t.Apply();
            var still = Sprite.Create(t, new Rect(0, 0, 64, 96), new Vector2(0.5f, 0f), 64f, 0, SpriteMeshType.FullRect); trash.Add(still);
            var f = CodeAnimLibrary.Parse("{\"entries\":[{\"key\":\"deco_x\",\"still\":[64,96],\"play\":\"always\",\"effects\":[" +
                "{\"type\":\"glint\",\"name\":\"g\",\"sprite\":\"glint\",\"size\":[5,5],\"count\":2,\"masks\":[{\"sat\":[0,0.08],\"val\":[0.9,1]}]}]}]}");
            var go = new GameObject("glint-auto"); trash.Add(go);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = still;
            var ca = CodeAnimator.For(sr);
            Assert.IsTrue(ca.ConfigureUncached(f.entries[0], sr, still, null));
            Assert.AreEqual(2, ca.SlotCount, "count glints");
            Juice.SetReduceMotion(false);
            var seen = new HashSet<Vector2Int>();
            for (float tt = 0f; tt < 6f; tt += 0.1f)
            {
                ca.Tick(tt);
                for (int i = 0; i < ca.SlotCount; i++)
                {
                    var r = ca.SlotRenderer(i);
                    if (!r.enabled) continue;
                    var lp = r.transform.localPosition * 64f;     // bottom-centre of the glint, pivot of the still = (32, 96)
                    seen.Add(new Vector2Int(Mathf.RoundToInt(lp.x) + 32, 96 - Mathf.RoundToInt(lp.y) - 3));
                }
            }
            Assert.AreEqual(2, seen.Count);
            foreach (var s in seen) Assert.Contains(s, snow, "glint centred on a snow texel");
        }
    }
}
