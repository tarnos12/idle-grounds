using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.Pool;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Pull-based qi trails (ADR 0003): each LateUpdate walks every unlocked Island's built Spirit Bridges
    /// and, for each SENDING bridge with an intact partner (<see cref="Simulation.BridgePartner"/>), keeps
    /// one <see cref="QiTrailView"/> from its world centre to the receiver's. Trails vanish when a pair
    /// breaks or either end is demolished.
    /// </summary>
    public class QiTrailSync : MonoBehaviour
    {
        public static readonly Color TrailTint = new Color(0.55f, 0.95f, 1f, 0.85f);

        [SerializeField] GameRunner runner;
        [SerializeField] QiTrailView prefab;
        [SerializeField] Transform root;

        readonly Dictionary<long, QiTrailView> views = new Dictionary<long, QiTrailView>();
        readonly List<long> releaseList = new List<long>();
        ObjectPool<QiTrailView> pool;
        int frame;

        public int Count => views.Count;
        public IEnumerable<QiTrailView> Trails => views.Values;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (root == null) root = transform;
            pool = new ObjectPool<QiTrailView>(
                () => Instantiate(prefab, root),
                v => v.gameObject.SetActive(true),
                v => v.gameObject.SetActive(false),
                v => Destroy(v.gameObject), false, 8, 128);
        }

        static long Key(int areaIdx, int id) => ((long)areaIdx << 32) | (uint)id;

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null || prefab == null) return;
            frame++;
            var sim = runner.Sim;
            var areas = runner.State.areas;
            for (int ai = 0; ai < areas.Count; ai++)
            {
                var area = areas[ai];
                if (!runner.IsUnlocked(area.key)) continue;
                foreach (var b in area.buildings)
                {
                    if (!b.built || b.pairIsland == null || !b.pairSends) continue;
                    var partner = sim.BridgePartner(area.key, b);
                    if (partner == null) continue;
                    long k = Key(ai, b.id);
                    if (!views.TryGetValue(k, out var v)) { v = pool.Get(); views[k] = v; v.Bind(area.key + "_" + b.id); }
                    v.seenFrame = frame;
                    var (ax, ay) = sim.World.BuildingWorldCenterPx(area.key, b);
                    var (bx, by) = sim.World.BuildingWorldCenterPx(b.pairIsland, partner);
                    v.Set(runner.Space.WorldPxToWorld(ax, ay), runner.Space.WorldPxToWorld(bx, by), TrailTint);
                }
            }
            releaseList.Clear();
            foreach (var kv in views) if (kv.Value.seenFrame != frame) releaseList.Add(kv.Key);
            foreach (var k in releaseList) { pool.Release(views[k]); views.Remove(k); }
        }
    }
}
