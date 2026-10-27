using System.Collections.Generic;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Code motion of the edge-ring decor (ART-SPEC §3.9.8 step D): the trees, shrubs and props that
    /// <c>IslandCoastBuilder.BuildBoundary</c> lays around the playable square are plain SpriteRenderers in the scene. At play
    /// start this scans them once; every sprite whose still has a matching code-anim.json entry (<see cref="CodeAnimLibrary.FindForStill"/>:
    /// art key, size key or "&lt;key&gt;#&lt;frame&gt;" variant, still size must match) gets a <see cref="CodeAnimator"/>, baked per
    /// variant frame and shared between instances; mirrored sprites stay correct (the animator follows <c>flipX</c>).
    /// One LateUpdate ticks them all, only those near the camera; no allocation per frame. Calm motion: the animator holds the
    /// rest frame. Without matching entries (the 32-px decor today) it only watches <see cref="CodeAnimLibrary.Version"/> and
    /// scans again when the config changes (a test entry, a reload).
    /// </summary>
    public sealed class DecorMotion : MonoBehaviour
    {
        CodeAnimator[] anims = new CodeAnimator[0];
        Vector2[] at = new Vector2[0];
        float[] phase = new float[0];
        int count;
        int version = -1;

        /// <summary>Animated decor sprites under this root.</summary>
        public int Count => count;
        public CodeAnimator Animator(int i) => anims[i];

        /// <summary>Add (once) to an Island's "Boundary" root at play start.</summary>
        public static DecorMotion Attach(Transform island)
        {
            var root = island != null ? island.Find("Boundary") : null;
            if (root == null) return null;
            var m = root.GetComponent<DecorMotion>();
            return m != null ? m : root.gameObject.AddComponent<DecorMotion>();
        }

        void Start() { if (version != CodeAnimLibrary.Version) Scan(); }

        /// <summary>Find the animated decor among the direct children (call again after the config changed: tests, tools).</summary>
        public int Scan()
        {
            _ = CodeAnimLibrary.File;      // load the config first (loading bumps the version)
            version = CodeAnimLibrary.Version;
            var list = new List<CodeAnimator>();
            var pos = new List<Vector2>();
            var ph = new List<float>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var sr = transform.GetChild(i).GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                var existing = sr.GetComponent<CodeAnimator>();
                if (existing != null) existing.Clear();       // a rescan: back to the still first
                var still = sr.sprite;
                if (still == null) continue;
                var e = CodeAnimLibrary.FindForStill(CodeAnimLibrary.KeyFromSpriteName(still.name), still);
                if (e == null || !e.HasEffects) continue;
                var ca = CodeAnimator.For(sr);
                ca.SelfTick = false;
                if (!ca.Configure(e, sr, still, _ => null)) continue;
                ca.Playing = true;
                list.Add(ca);
                pos.Add(sr.transform.position);
                ph.Add(Phase(sr.transform.position));
            }
            anims = list.ToArray();
            at = pos.ToArray();
            phase = ph.ToArray();
            count = anims.Length;
            return count;
        }

        /// <summary>Clock offset (seconds) of a decor piece's code motion: the crowns sway as a slow wind wave running across
        /// the Island instead of all at once (edge ring and NodeView decor rings alike).</summary>
        public static float Phase(Vector3 world) => Mathf.Repeat(world.x * 0.15f + world.y * 0.05f, 60f);     // never negative (step 0 below t = 0)

        void LateUpdate() => TickAll(CodeAnimClock.Now);

        /// <summary>One frame of every animated decor piece near the camera (all of them without a camera). No allocation.</summary>
        public void TickAll(float t)
        {
            if (version != CodeAnimLibrary.Version) Scan();
            if (count == 0) return;
            bool cull = ViewCull.TryGet(out var view);
            for (int i = 0; i < count; i++)
                if (!cull || view.Contains(at[i])) anims[i].Tick(t + phase[i]);
        }
    }
}
