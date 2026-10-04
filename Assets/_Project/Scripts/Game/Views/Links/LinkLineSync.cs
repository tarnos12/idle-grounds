using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.Pool;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Lantern link threads (spec 2.3): one dashed line per link of every built lantern in an unlocked
    /// area, rgba(251,191,36,.22), width 1.5 px; .65 for the lantern whose link editor is open
    /// (a status dot sits on each of its threads), .45 for the hovered lantern. Drawn under buildings.
    /// </summary>
    public class LinkLineSync : MonoBehaviour
    {
        public const float WidthPx = 1.5f;
        public static readonly Color Faint = ViewKit.Rgba(251, 191, 36, 0.22f), Hover = ViewKit.Rgba(251, 191, 36, 0.45f), Edit = ViewKit.Rgba(251, 191, 36, 0.65f);

        [SerializeField] GameRunner runner;
        [SerializeField] LinkLineView prefab;
        [SerializeField] BuildController build;
        [SerializeField] LinkEditorView editor;
        [SerializeField] Transform root;

        readonly Dictionary<long, LinkLineView> views = new Dictionary<long, LinkLineView>();
        readonly List<long> releaseList = new List<long>();
        ObjectPool<LinkLineView> pool;
        int frame;

        public int ViewCount => views.Count;
        public IEnumerable<LinkLineView> Views => views.Values;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (root == null) root = transform;
            pool = new ObjectPool<LinkLineView>(
                () => Instantiate(prefab, root),
                v => v.gameObject.SetActive(true),
                v => v.gameObject.SetActive(false),
                v => Destroy(v.gameObject), false, 32, 512);
        }

        static long Key(int areaIdx, int lanternId, int idx) => ((long)areaIdx << 44) | ((long)lanternId << 16) | (uint)idx;

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null) return;
            frame++;
            var sim = runner.Sim;
            var space = runner.Space;
            var areas = runner.State.areas;
            for (int ai = 0; ai < areas.Count; ai++)
            {
                var area = areas[ai];
                if (!runner.IsUnlocked(area.key)) continue;
                foreach (var lb in area.buildings)
                {
                    if (!lb.built || lb.links == null || lb.links.Count == 0) continue;
                    bool editing = editor != null && editor.IsOpen && editor.Area == area.key && editor.LanternId == lb.id;
                    bool hovered = !editing && build != null && build.Hovered == lb && build.HoveredArea == area.key;
                    var col = editing ? Edit : hovered ? Hover : Faint;
                    for (int i = 0; i < lb.links.Count; i++)
                    {
                        var l = lb.links[i];
                        var f = area.BuildingById(l.from); var t = area.BuildingById(l.to);
                        if (f == null || t == null) continue;
                        long k = Key(ai, lb.id, i);
                        if (!views.TryGetValue(k, out var v)) { v = pool.Get(); views[k] = v; }
                        v.seenFrame = frame;
                        var (fx, fy) = sim.World.BuildingCenterPx(f);
                        var (tx, ty) = sim.World.BuildingCenterPx(t);
                        v.Set(space.PxToWorld(area.key, fx, fy), space.PxToWorld(area.key, tx, ty), col, WidthPx);
                        var st = editing ? sim.LinkStatus(area.key, lb.id, i) : null;
                        v.SetDot(st != null, st != null ? LinkLineView.DotColour(st.dot) : default);
                    }
                }
            }
            releaseList.Clear();
            foreach (var kv in views) if (kv.Value.seenFrame != frame) releaseList.Add(kv.Key);
            foreach (var k in releaseList) { pool.Release(views[k]); views.Remove(k); }
        }
    }
}
