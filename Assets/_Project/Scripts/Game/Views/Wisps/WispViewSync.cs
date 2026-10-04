using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.Pool;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Pull-based wisp views (ADR 0001): each LateUpdate walks State.areas[k].wisps of every unlocked
    /// area and positions pooled <see cref="WispView"/>s from <see cref="Simulation.WispPos"/> (a pure
    /// function of the wall clock, so flight is smooth at any frame rate). Keyed by (area, wisp id).
    /// </summary>
    public class WispViewSync : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] WispView prefab;
        [SerializeField] Transform root;

        readonly Dictionary<long, WispView> views = new Dictionary<long, WispView>();
        readonly List<long> releaseList = new List<long>();
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
                v => Destroy(v.gameObject), false, 64, 1024);
        }

        void Start() => sprites = new SpriteCache(runner.Database, null);

        static long Key(int areaIdx, int id) => ((long)areaIdx << 32) | (uint)id;

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null || sprites == null) return;
            frame++;
            var sim = runner.Sim;
            double now = runner.SimNow;
            var areas = runner.State.areas;
            for (int ai = 0; ai < areas.Count; ai++)
            {
                var area = areas[ai];
                if (area.wisps == null || area.wisps.Count == 0 || !runner.IsUnlocked(area.key)) continue;
                foreach (var w in area.wisps)
                {
                    long k = Key(ai, w.id);
                    if (!views.TryGetValue(k, out var v) || v.Wisp != w)
                    {
                        if (v == null) { v = pool.Get(); views[k] = v; }
                        v.Bind(area.key, w, sprites.Item(w.item));
                    }
                    v.seenFrame = frame;
                    var p = sim.WispPos(area.key, w, now);
                    v.Refresh(runner.Space.PxToWorld(area.key, p.x, p.y));
                }
            }
            releaseList.Clear();
            foreach (var kv in views) if (kv.Value.seenFrame != frame) releaseList.Add(kv.Key);
            foreach (var k in releaseList) { pool.Release(views[k]); views.Remove(k); }
        }
    }
}
