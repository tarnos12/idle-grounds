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
    }
}
