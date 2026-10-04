using System.Collections.Generic;
using IdleGrounds.Game.Data;
using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.Pool;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Pull-based building views (ADR 0001), sibling of <see cref="WorldViewSync"/>: each LateUpdate
    /// walks State.areas[k].buildings (every area — locked ones sit under the veil) and creates,
    /// refreshes or releases pooled <see cref="BuildingView"/>s keyed by (area, building id). The view
    /// prefab per type comes from the <see cref="BuildingPrefabSet"/>; one pool per prefab.
    /// </summary>
    public class BuildingViewSync : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] BuildingPrefabSet prefabSet;
        [SerializeField] Transform root;

        readonly Dictionary<long, BuildingView> views = new Dictionary<long, BuildingView>();
        readonly Dictionary<GameObject, ObjectPool<BuildingView>> pools = new Dictionary<GameObject, ObjectPool<BuildingView>>();
        readonly Dictionary<string, GameObject> prefabByType = new Dictionary<string, GameObject>();
        readonly List<long> releaseList = new List<long>();
        int frame;

        public Simulation Sim => runner.Sim;
        public AreaSpace Space => runner.Space;
        public GameRunner Runner => runner;
        public SpriteCache Sprites { get; private set; }
        public int ViewCount => views.Count;
        public IEnumerable<BuildingView> Views => views.Values;

        /// <summary>Optional highlight provider (BuildController): hovered / selected / demolish-hover building.</summary>
        public System.Func<BuildingView, (bool hover, bool selected, bool demolish, bool reach)> Highlight;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (root == null) root = transform;
        }

        void Start() => Sprites = new SpriteCache(runner.Database, null);

        static long Key(int areaIdx, int id) => ((long)areaIdx << 32) | (uint)id;

        public BuildingView Find(string area, int id)
        {
            var areas = runner.State.areas;
            for (int i = 0; i < areas.Count; i++)
                if (areas[i].key == area) return views.TryGetValue(Key(i, id), out var v) ? v : null;
            return null;
        }

        public GameObject PrefabFor(string type)
        {
            if (prefabByType.TryGetValue(type, out var p)) return p;
            p = prefabSet != null ? prefabSet.Resolve(runner.Database, runner.Config.Building(type)) : null;
            prefabByType[type] = p;
            return p;
        }

        ObjectPool<BuildingView> Pool(GameObject prefab)
        {
            if (pools.TryGetValue(prefab, out var pool)) return pool;
            pool = new ObjectPool<BuildingView>(
                () => { var go = Instantiate(prefab, root); var v = go.GetComponent<BuildingView>(); v.sourcePrefab = prefab; return v; },
                v => v.gameObject.SetActive(true),
                v => v.gameObject.SetActive(false),
                v => Destroy(v.gameObject), false, 16, 512);
            pools[prefab] = pool;
            return pool;
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null || Sprites == null) return;
            frame++;
            var areas = runner.State.areas;
            for (int ai = 0; ai < areas.Count; ai++)
            {
                var area = areas[ai];
                foreach (var b in area.buildings)
                {
                    long k = Key(ai, b.id);
                    if (!views.TryGetValue(k, out var v) || v.Building != b)
                    {
                        var prefab = PrefabFor(b.type);
                        if (prefab == null) continue;
                        if (v != null && v.sourcePrefab != prefab) { Pool(v.sourcePrefab).Release(v); v = null; }
                        if (v == null) { v = Pool(prefab).Get(); views[k] = v; }
                        v.Bind(this, area.key, b, runner.Config.Building(b.type));
                    }
                    v.seenFrame = frame;
                    if (Highlight != null) { var h = Highlight(v); v.SetHighlight(h.hover, h.selected, h.demolish, h.reach); }
                    v.Refresh();
                }
            }
            releaseList.Clear();
            foreach (var kv in views) if (kv.Value.seenFrame != frame) releaseList.Add(kv.Key);
            foreach (var k in releaseList) { var v = views[k]; v.SetHighlight(false, false, false, false); Pool(v.sourcePrefab).Release(v); views.Remove(k); }
        }
    }
}
