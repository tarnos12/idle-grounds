using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.Pool;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Pull-based views of the wisps crossing the sky between paired Spirit Bridges (ADR 0003): each
    /// LateUpdate walks <see cref="Simulation.SkyWisps"/> and positions pooled <see cref="WispView"/>s
    /// (the same prefab as Island wisps) from <see cref="Simulation.SkyWispPos"/> — WORLD px, a pure
    /// function of the clock, so flight is smooth. Returning wisps (pair broken / receiver refused) glow red.
    /// Keyed by sky wisp id.
    /// </summary>
    public class SkyWispViewSync : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] WispView prefab;
        [SerializeField] Transform root;

        readonly Dictionary<int, WispView> views = new Dictionary<int, WispView>();
        readonly List<int> releaseList = new List<int>();
        ObjectPool<WispView> pool;
        SpriteCache sprites;
        int frame;

        public int ViewCount => views.Count;
        public IEnumerable<WispView> Views => views.Values;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (root == null) root = transform;
            pool = new ObjectPool<WispView>(
                () => Instantiate(prefab, root),
                v => v.gameObject.SetActive(true),
                v => v.gameObject.SetActive(false),
                v => Destroy(v.gameObject), false, 16, 512);
        }

        void Start() => sprites = new SpriteCache(runner.Database, null);

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null || sprites == null || prefab == null) return;
            frame++;
            var sim = runner.Sim;
            double now = runner.SimNow;
            var list = sim.SkyWisps;
            if (list != null)
                for (int i = 0; i < list.Count; i++)
                {
                    var w = list[i];
                    if (!views.TryGetValue(w.id, out var v) || v.Wisp != w)
                    {
                        if (v == null) { v = pool.Get(); views[w.id] = v; }
                        v.Bind("sky", w, sprites.Item(w.item), sprites.FxFrames("fx_wisp"), sprites.FxFrames("fx_wisp_returning"));
                    }
                    v.seenFrame = frame;
                    var p = sim.SkyWispPos(w, now);
                    v.Refresh(runner.Space.WorldPxToWorld(p.x, p.y));
                }
            releaseList.Clear();
            foreach (var kv in views) if (kv.Value.seenFrame != frame) releaseList.Add(kv.Key);
            foreach (var k in releaseList) { pool.Release(views[k]); views.Remove(k); }
        }
    }
}
