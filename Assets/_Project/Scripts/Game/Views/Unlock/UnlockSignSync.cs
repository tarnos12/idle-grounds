using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.Pool;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Keeps one <see cref="UnlockSignView"/> per (locked frontier region, unlocked neighbour) pair,
    /// centred in the void gap on the border they share (the side is derived geometrically from the
    /// region grid, as the original did — <c>unlockSide</c> is unused there too). The camera clamp lets
    /// the view reach exactly that gap, so the sign is always reachable. Signs vanish when the region
    /// unlocks (veils lift via GameRunner). <see cref="Pay"/> = <see cref="Simulation.UnlockArea"/>.
    /// </summary>
    public class UnlockSignSync : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] UnlockSignView prefab;
        [SerializeField] Transform root;
        [SerializeField] FxService fx;
        [SerializeField] HandController hand;

        readonly Dictionary<int, UnlockSignView> views = new Dictionary<int, UnlockSignView>();
        readonly List<int> releaseList = new List<int>();
        ObjectPool<UnlockSignView> pool;
        SpriteCache sprites;
        int frame;

        public IEnumerable<UnlockSignView> Signs => views.Values;
        public int Count => views.Count;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (root == null) root = transform;
            pool = new ObjectPool<UnlockSignView>(
                () => Instantiate(prefab, root),
                v => v.gameObject.SetActive(true),
                v => v.gameObject.SetActive(false),
                v => Destroy(v.gameObject), false, 8, 32);
        }

        void Start()
        {
            if (fx == null) fx = FindFirstObjectByType<FxService>();
            if (hand == null) hand = FindFirstObjectByType<HandController>();
            sprites = new SpriteCache(runner.Database, null);
        }

        public UnlockSignView Find(string area)
        {
            foreach (var v in views.Values) if (v.Area == area) return v;
            return null;
        }

        /// <summary>World centre of the gap between locked region <paramref name="locked"/> and neighbour <paramref name="from"/>.</summary>
        public Vector3 GapCentre(RegionDef locked, RegionDef from)
        {
            var o = runner.Space.Origin(locked.key);
            float cells = runner.Space.Cells, gap = runner.Config.grid.gap, half = cells * 0.5f;
            int dx = from.rx - locked.rx, dy = from.ry - locked.ry;
            if (dx < 0) return new Vector3(o.x - gap * 0.5f, o.y - half, 0f);          // neighbour on the left
            if (dx > 0) return new Vector3(o.x + cells + gap * 0.5f, o.y - half, 0f);  // right
            if (dy < 0) return new Vector3(o.x + half, o.y + gap * 0.5f, 0f);          // above (ry grows down)
            return new Vector3(o.x + half, o.y - cells - gap * 0.5f, 0f);              // below
        }

        /// <summary>Sign under a world point (null = none).</summary>
        public UnlockSignView HitTest(Vector2 world)
        {
            foreach (var v in views.Values) if (v.WorldRect.Contains(world)) return v;
            return null;
        }

        /// <summary>Pay the hand toward the sign's region (left or right click on it).</summary>
        public UnlockResult Pay(UnlockSignView v, Vector2 at)
        {
            var res = runner.Sim.UnlockArea(v.Area);
            v.Invalidate();
            if (fx != null)
            {
                string nm = runner.Config.Region(v.Area)?.name ?? v.Area;
                if (res.kind == UnlockResultKind.Unlocked) fx.Floater(at, nm + " unlocked!", FxService.Gold);
                else if (res.kind == UnlockResultKind.Paid) fx.Floater(at, "Paid " + res.paid, FxService.Gold);
                else { fx.Floater(at, "Carry the cost to unlock", FxService.Danger); AudioService.Play("error"); }
            }
            return res;
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null || sprites == null) return;
            frame++;
            var regions = runner.Config.regions;
            double now = runner.SimNow;
            for (int li = 0; li < regions.Count; li++)
            {
                var l = regions[li];
                if (runner.IsUnlocked(l.key)) continue;
                for (int ui = 0; ui < regions.Count; ui++)
                {
                    var u = regions[ui];
                    if (!runner.IsUnlocked(u.key) || Mathf.Abs(u.rx - l.rx) + Mathf.Abs(u.ry - l.ry) != 1) continue;
                    int k = li * 64 + ui;      // (locked, neighbour) pair key — no per-frame string
                    if (!views.TryGetValue(k, out var v))
                    {
                        v = pool.Get();
                        views[k] = v;
                        v.Bind(l.key, u.key, GapCentre(l, u), l.name);
                    }
                    v.seenFrame = frame;
                    v.Hovered = hand != null && hand.CursorOver && v.WorldRect.Contains(hand.CursorWorld);
                    if (!ViewCull.Visible(v.transform.position, UnlockSignView.W)) continue;
                    v.Refresh(runner.Sim, sprites, now);
                }
            }
            releaseList.Clear();
            foreach (var kv in views) if (kv.Value.seenFrame != frame) releaseList.Add(kv.Key);
            foreach (var k in releaseList) { pool.Release(views[k]); views.Remove(k); }
        }
    }
}
