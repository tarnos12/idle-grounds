using System.Collections;
using IdleGrounds.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleGrounds.PlayMode.Tests
{
    /// <summary>CodeAnimator.Tick allocates no GC memory per frame (owner pick anim_route; performance gate). Play mode, because
    /// in edit mode every SpriteRenderer / Transform setter allocates for the editor's change tracking.</summary>
    public class CodeAnimPlayModeTests
    {
        const string Effects =
            "{\"type\":\"glow\",\"name\":\"g\",\"rect\":[10,10,30,30],\"colorA\":\"#5ED4E8\",\"colorB\":\"#F2C94C\",\"period\":1.1}," +
            "{\"type\":\"flicker\",\"name\":\"f\",\"sprite\":\"flame\",\"size\":[5,9],\"count\":3,\"points\":[20,60,27,61],\"offsets\":[0,1,2,1]}," +
            "{\"type\":\"flicker\",\"name\":\"st\",\"sprite\":\"star\",\"size\":[3,3],\"mode\":\"cycle\",\"points\":[30,30,35,35]}," +
            "{\"type\":\"smoke\",\"name\":\"sm\",\"points\":[33,12],\"size\":[5,5],\"growTo\":[11,9],\"rise\":13,\"drift\":5,\"count\":4,\"period\":1.7}," +
            "{\"type\":\"toggle\",\"name\":\"tg\",\"rect\":[40,70,7,9],\"onSteps\":[2,3]}," +
            "{\"type\":\"sway\",\"name\":\"sw\",\"rect\":[30,20,20,30],\"pivot\":[32,49],\"offsets\":[0,2,0,-2]}";

        // step C nodes: overlay-only glow + glints on 2 state frames, and ripple rings + splash on the water
        const string StateEffects =
            "{\"type\":\"glow\",\"name\":\"g\",\"rect\":[10,10,40,60],\"colorA\":\"#C2521C\",\"colorB\":\"#FF8A1F\",\"alpha\":[0,0.5],\"period\":2.8,\"additive\":true}," +
            "{\"type\":\"flicker\",\"name\":\"gl\",\"sprite\":\"glint\",\"size\":[5,5],\"mode\":\"cycle\",\"points\":[20,30,30,40,40,50],\"onSteps\":[6],\"colorA\":\"#E8ECF3\",\"alpha\":[0.75,0.75],\"calm\":\"hide\"}," +
            "{\"type\":\"ripple\",\"name\":\"r\",\"points\":[31,88],\"size\":[40,10],\"growTo\":[64,20],\"count\":2,\"period\":1.6}," +
            "{\"type\":\"splash\",\"name\":\"sp\",\"points\":[31,84],\"count\":4,\"period\":1.6,\"life\":0.5,\"rise\":10,\"drift\":18}";

        static long Allocs(System.Action a)
        {
            a();   // warm-up / JIT outside the measurement
            using (var rc = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocation In Frame Count"))
            {
                Assert.IsTrue(rc.Valid, "GC recorder");
                long c0 = rc.CurrentValue; a(); return rc.CurrentValue - c0;
            }
        }

        [UnityTest]
        public IEnumerator Tick_Allocates_Nothing()
        {
            var t = new Texture2D(64, 96, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[64 * 96];
            for (int i = 0; i < px.Length; i++) px[i] = (i % 64) > 8 && (i % 64) < 56 ? new Color32(200, 40, 40, 255) : new Color32(0, 0, 0, 0);
            t.SetPixels32(px); t.Apply();
            var still = Sprite.Create(t, new Rect(0, 0, 64, 96), new Vector2(0.5f, 0f), 64f, 0, SpriteMeshType.FullRect);
            var go = new GameObject("codeanim-alloc");
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = still;
            var f = CodeAnimLibrary.Parse("{\"entries\":[{\"key\":\"npc_alloc\",\"still\":[64,96],\"fps\":8,\"steps\":4,\"effects\":[" + Effects + "]}]}");
            var ca = CodeAnimator.For(sr);
            ca.SelfTick = false;
            Assert.IsTrue(ca.ConfigureUncached(f.entries[0], sr, still, null));
            yield return null;

            float t0 = 1.3f;
            long ticks = Allocs(() => { for (int i = 0; i < 3000; i++) ca.Tick(t0 + i * 0.0173f); });
            long control = Allocs(() => { for (int i = 0; i < 3000; i++) go.transform.localPosition = new Vector3(i * 0.01f, 0f, 0f); });
            TestContext.Out.WriteLine($"CodeAnimator.Tick x3000 (10 overlays + 4 baked frames): {ticks} GC allocations; control (3000 transform writes): {control}");
            Object.Destroy(go); Object.Destroy(still); Object.Destroy(t);
            Assert.AreEqual(0, ticks, "CodeAnimator.Tick must not allocate");
        }

        [UnityTest]
        public IEnumerator OverlayOnly_And_Water_Tick_Allocates_Nothing()
        {
            var t = new Texture2D(128, 96, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[128 * 96];
            for (int i = 0; i < px.Length; i++) { int x = i % 128 % 64; px[i] = x > 8 && x < 56 ? (i % 128 >= 64 && x == 30 ? new Color32(13, 15, 20, 255) : new Color32(200, 90, 40, 255)) : new Color32(0, 0, 0, 0); }
            t.SetPixels32(px); t.Apply();
            var f0 = Sprite.Create(t, new Rect(0, 0, 64, 96), new Vector2(0.5f, 0f), 64f, 0, SpriteMeshType.FullRect);
            var f1 = Sprite.Create(t, new Rect(64, 0, 64, 96), new Vector2(0.5f, 0f), 64f, 0, SpriteMeshType.FullRect);
            var go = new GameObject("codeanim-alloc-state");
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = f0;
            var f = CodeAnimLibrary.Parse("{\"entries\":[{\"key\":\"node_alloc\",\"still\":[64,96],\"fps\":4,\"steps\":4,\"play\":\"always\",\"overlayOnly\":true,\"effects\":[" + StateEffects + "]}]}");
            var ca = CodeAnimator.For(sr);
            ca.SelfTick = false;
            Assert.IsTrue(ca.ConfigureOverlayUncached(f.entries[0], sr, new[] { f0, f1 }));
            yield return null;

            float t0 = 0.7f;
            long ticks = Allocs(() =>
            {
                for (int i = 0; i < 3000; i++)
                {
                    if (i == 1500) sr.sprite = f1;              // the first hit: the owner shows the cracked frame
                    ca.GroundLift = (i % 6) / 64f;               // a bobbing still: water overlays stay on the water
                    ca.Tick(t0 + i * 0.0173f);
                }
            });
            TestContext.Out.WriteLine($"overlay-only + ripple + splash Tick x3000 ({ca.SlotCount} slots): {ticks} GC allocations");
            Assert.AreSame(f1, sr.sprite, "the hit frame survives");
            Object.Destroy(go); Object.Destroy(f0); Object.Destroy(f1); Object.Destroy(t);
            Assert.AreEqual(0, ticks, "overlay-only / water Tick must not allocate");
        }

        /// <summary>Step D decor (ART-SPEC §3.9.8): the edge-ring DecorMotion picks the shipping deco_pine_snow entry up for a
        /// 64x96 variant strip (stand-in pixels: crown, trunk, snow texels), bakes per variant, keeps mirrored pines correct, and
        /// ticks every piece without allocating.</summary>
        [UnityTest]
        public IEnumerator DecorMotion_Animates_At64_Variants_Mirrored_Without_Allocating()
        {
            var t = new Texture2D(192, 96, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[192 * 96];
            for (int v = 0; v < 3; v++)
                for (int y = 0; y < 96; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        Color32 c = default;
                        if (y < 70 && x >= 8 + v * 2 && x < 56 - v * 2) c = (y % 9 == 3 && x % 7 == 2) ? new Color32(232, 236, 243, 255) : new Color32(40, 110, 60, 255);
                        else if (y >= 70 && x >= 28 && x < 36) c = new Color32(120, 80, 40, 255);
                        px[(95 - y) * 192 + v * 64 + x] = c;
                    }
            t.SetPixels32(px); t.Apply();
            var frames = new Sprite[3];
            for (int v = 0; v < 3; v++)
            {
                frames[v] = Sprite.Create(t, new Rect(v * 64, 0, 64, 96), new Vector2(0.5f, 0f), 64f, 0, SpriteMeshType.FullRect);
                frames[v].name = "deco_pine_snow_64x96_3f@64_" + v;
            }
            var island = new GameObject("decor-island");
            var root = new GameObject("Boundary"); root.transform.SetParent(island.transform, false);
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("Tree"); go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(i * 3f, 0f, 0f);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = frames[i % 3]; sr.flipX = i >= 3;
            }
            var stone = new GameObject("Rock"); stone.transform.SetParent(root.transform, false);
            stone.AddComponent<SpriteRenderer>().sprite = Sprite.Create(t, new Rect(0, 0, 32, 48), new Vector2(0.5f, 0f), 32f);   // no entry: stays plain
            var m = DecorMotion.Attach(island.transform);
            Assert.IsNotNull(m);
            Assert.AreEqual(6, m.Scan(), "every 64x96 pine animates, the rock does not");
            Assert.IsNull(stone.GetComponent<CodeAnimator>());
            Assert.AreEqual(4, m.Animator(0).BaseFrameCount, "sway baked per variant");
            Assert.AreSame(m.Animator(0).BaseFrame(1), m.Animator(3).BaseFrame(1), "one bake per variant, shared by the mirrored pine");
            yield return null;

            // the mirrored pine (i = 3) shows its glints at the mirrored x, flipped; same clock for the comparison
            var a = m.Animator(0); var b = m.Animator(3);
            int shown = 0;
            for (float tt = 0.1f; tt < 30f; tt += 0.25f)
            {
                a.Tick(tt); b.Tick(tt);
                for (int i = 0; i < a.SlotCount; i++)
                {
                    var ra = a.SlotRenderer(i); var rb = b.SlotRenderer(i);
                    Assert.AreEqual(ra.enabled, rb.enabled);
                    if (!ra.enabled) continue;
                    shown++;
                    Assert.IsTrue(rb.flipX);
                    Assert.AreEqual(-ra.transform.localPosition.x, rb.transform.localPosition.x, 1e-5f);
                }
            }
            Assert.Greater(shown, 0, "snow glints show");

            float t0 = 0.9f;
            long ticks = Allocs(() => { for (int i = 0; i < 3000; i++) m.TickAll(t0 + i * 0.0173f); });
            TestContext.Out.WriteLine($"DecorMotion.TickAll x3000 ({m.Count} pines): {ticks} GC allocations");
            Object.Destroy(island);
            foreach (var f in frames) Object.Destroy(f);
            Object.Destroy(t);
            Assert.AreEqual(0, ticks, "decor motion must not allocate");
        }
    }
}
