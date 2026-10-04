using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.Pool;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Pooled enemy views keyed by (area, enemy id), pulled from State.areas[k].enemies each
    /// LateUpdate (unlocked areas only — locked ones freeze under the veil and spawn nothing).
    /// Sprite: lured boar (kind "boss") → region "enemy_bait" sprite, else "enemy". Kill FX: a gold
    /// "{name} slain" floater at the body (loot "+N" floaters come from GroundDropped as usual).
    /// </summary>
    public class EnemyViewSync : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] EnemyView prefab;
        [SerializeField] Transform root;
        [SerializeField] FxService fx;

        readonly Dictionary<long, EnemyView> views = new Dictionary<long, EnemyView>();
        readonly List<long> releaseList = new List<long>();
        ObjectPool<EnemyView> pool;
        int frame;

        public int ViewCount => views.Count;
        public IEnumerable<EnemyView> Views => views.Values;
        public int Kills { get; private set; }

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (root == null) root = transform;
            pool = new ObjectPool<EnemyView>(
                () => Instantiate(prefab, root),
                v => v.gameObject.SetActive(true),
                v => v.gameObject.SetActive(false),
                v => Destroy(v.gameObject), false, 16, 256);
        }

        void Start()
        {
            if (fx == null) fx = FindFirstObjectByType<FxService>();
            if (runner != null && runner.Sim != null) runner.Sim.Events.EnemyKilled += OnKilled;
        }

        void OnDestroy()
        {
            if (runner != null && runner.Sim != null) runner.Sim.Events.EnemyKilled -= OnKilled;
        }

        void OnKilled(string area, Enemy e)
        {
            Kills++;
            if (fx == null || !runner.IsUnlocked(area)) return;
            var def = runner.Config.Region(area)?.enemies;
            string nm = e.kind == "boss" ? def?.baitSpawn?.name : def?.name;
            fx.FloaterAt(area, e.x, e.y - 14, (string.IsNullOrEmpty(nm) ? "Beast" : nm) + " slain", FxService.Gold, SpriteFor(area, e));
        }

        public Sprite SpriteFor(string area, Enemy e) =>
            e.kind == "boss" ? runner.Database.BaitSprite(area) : runner.Database.EnemySprite(area);

        static long Key(int areaIdx, int id) => ((long)areaIdx << 32) | (uint)id;

        public EnemyView Find(string area, int id)
        {
            var areas = runner.State.areas;
            for (int i = 0; i < areas.Count; i++)
                if (areas[i].key == area) return views.TryGetValue(Key(i, id), out var v) ? v : null;
            return null;
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null) return;
            frame++;
            double now = runner.SimNow;
            var areas = runner.State.areas;
            for (int ai = 0; ai < areas.Count; ai++)
            {
                var area = areas[ai];
                if (area.enemies == null || !runner.IsUnlocked(area.key)) continue;
                foreach (var e in area.enemies)
                {
                    long k = Key(ai, e.id);
                    if (!views.TryGetValue(k, out var v) || v.Enemy != e)
                    {
                        if (v == null) { v = pool.Get(); views[k] = v; }
                        v.Bind(area.key, e, SpriteFor(area.key, e));
                    }
                    v.seenFrame = frame;
                    v.Refresh(runner.Space, now);
                }
            }
            releaseList.Clear();
            foreach (var kv in views) if (kv.Value.seenFrame != frame) releaseList.Add(kv.Key);
            foreach (var k in releaseList) { pool.Release(views[k]); views.Remove(k); }
        }
    }
}
