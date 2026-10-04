using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.Pool;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Pull-based world views (ADR 0001): each LateUpdate reads State.areas[k].nodes / ground and
    /// creates, updates or releases pooled views keyed by (area, sim id). Nodes of every area are
    /// shown (locked ones sit under the region veil); ground items only for unlocked areas (§2.1).
    /// </summary>
    public class WorldViewSync : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] NodeView nodePrefab;
        [SerializeField] GroundItemView groundItemPrefab;
        [SerializeField] Transform nodesRoot;
        [SerializeField] Transform groundRoot;
        [Tooltip("Sprite for the inert deco border trees (the pine emoji has no atlas entry yet).")]
        [SerializeField] Sprite decoSprite;
        [Tooltip("Cursor source for the fish countdown hover (found at runtime when empty).")]
        [SerializeField] HandController hand;

        readonly Dictionary<long, NodeView> nodeViews = new Dictionary<long, NodeView>();
        readonly Dictionary<long, GroundItemView> groundViews = new Dictionary<long, GroundItemView>();
        readonly List<long> releaseList = new List<long>();
        ObjectPool<NodeView> nodePool;
        ObjectPool<GroundItemView> groundPool;
        SpriteCache sprites;
        int frame;

        public int NodeViewCount => nodeViews.Count;
        public int GroundViewCount => groundViews.Count;

        /// <summary>The live view for a node (null when none) — handy for tests/FX.</summary>
        public NodeView FindNodeView(string area, int id) =>
            nodeViews.TryGetValue(Key(AreaIndex(area), id), out var v) ? v : null;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            nodePool = new ObjectPool<NodeView>(
                () => Instantiate(nodePrefab, nodesRoot),
                v => v.gameObject.SetActive(true),
                v => v.gameObject.SetActive(false),
                v => Destroy(v.gameObject), false, 256, 4096);
            groundPool = new ObjectPool<GroundItemView>(
                () => Instantiate(groundItemPrefab, groundRoot),
                v => v.gameObject.SetActive(true),
                v => v.gameObject.SetActive(false),
                v => Destroy(v.gameObject), false, 256, 8192);
        }

        void Start() => sprites = new SpriteCache(runner.Database, decoSprite);

        static long Key(int areaIdx, int id) => ((long)areaIdx << 32) | (uint)id;

        int AreaIndex(string key)
        {
            var areas = runner.State.areas;
            for (int i = 0; i < areas.Count; i++) if (areas[i].key == key) return i;
            return -1;
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null || sprites == null) return;
            frame++;
            var space = runner.Space;
            double now = runner.SimNow;
            int cell = space.Cell;
            var areas = runner.State.areas;
            // node under the cursor (the fish countdown also shows while hovered, ui.js:1259)
            Node hoveredNode = null;
            if (hand == null) hand = FindFirstObjectByType<HandController>();
            if (hand != null && hand.CursorOver && hand.CursorArea != null && runner.IsUnlocked(hand.CursorArea))
                hoveredNode = HandController.NodeAtCell(runner.State.Area(hand.CursorArea), hand.LRow, hand.LCol);
            for (int ai = 0; ai < areas.Count; ai++)
            {
                var area = areas[ai];
                bool unlocked = runner.IsUnlocked(area.key);
                foreach (var n in area.nodes)
                {
                    long k = Key(ai, n.id);
                    if (!nodeViews.TryGetValue(k, out var v) || v.Node != n)
                    {
                        if (v == null) { v = nodePool.Get(); nodeViews[k] = v; }
                        v.Bind(area.key, n, sprites.Node(area.key, n), space, sprites.NodeArt(area.key, n));
                    }
                    v.seenFrame = frame;
                    if (!ViewCull.Visible(v.transform.position, n.size + 3f)) continue;     // off-camera: skip the animation
                    v.Refresh(now, cell, !n.deco && runner.Sim.NodeAutoFlashing(n), unlocked, n == hoveredNode);
                }
                if (!unlocked) continue;
                foreach (var g in area.ground)
                {
                    long k = Key(ai, g.id);
                    if (!groundViews.TryGetValue(k, out var v) || v.Item != g)
                    {
                        if (v == null) { v = groundPool.Get(); groundViews[k] = v; }
                        v.Bind(area.key, g, sprites.Item(g.item), space);
                    }
                    v.seenFrame = frame;
                    v.Refresh(space);
                }
            }

            releaseList.Clear();
            foreach (var kv in nodeViews) if (kv.Value.seenFrame != frame) releaseList.Add(kv.Key);
            foreach (var k in releaseList) { nodePool.Release(nodeViews[k]); nodeViews.Remove(k); }
            releaseList.Clear();
            foreach (var kv in groundViews) if (kv.Value.seenFrame != frame) releaseList.Add(kv.Key);
            foreach (var k in releaseList) { groundPool.Release(groundViews[k]); groundViews.Remove(k); }
        }
    }
}
