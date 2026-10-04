using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// Per-area ref-counted cell grid (engine-systems §3.3, engine.js:378-439).
    /// Occupied = every node's size² square (fixtures + deco too) + every
    /// building footprint (ghosts included) + every BUILT burner's 3×2 fuel
    /// rack (rows row..row+1, cols col-3..col-1). Memoised by a signature
    /// (list identity, count, next id for nodes and buildings, epoch) exactly
    /// like the JS cache, and patched in place on node add/remove; any other
    /// mutation triggers a full rebuild — so it always equals a recompute.
    /// </summary>
    public sealed class OccupancyGrid
    {
        sealed class Entry
        {
            public int[] cnt;
            public int epoch;
            public List<Node> nodes; public int nLen, nId;
            public List<Building> blds; public int bLen, bId;
        }

        readonly SimContext _ctx;
        readonly Dictionary<AreaState, Entry> _cache = new Dictionary<AreaState, Entry>();
        int _epoch;

        public OccupancyGrid(SimContext ctx) { _ctx = ctx; }

        /// <summary>JS `occEpoch++` — call when a ghost turns built in place (its rack appears).</summary>
        public void BumpEpoch() => _epoch++;

        /// <summary>Drop every cached grid (e.g. after a state swap).</summary>
        public void Invalidate() => _cache.Clear();

        int N => _ctx.N;

        bool Fresh(AreaState a, Entry h) =>
            h != null && h.epoch == _epoch && ReferenceEquals(h.nodes, a.nodes) && h.nLen == a.nodes.Count && h.nId == a.nextNodeId &&
            ReferenceEquals(h.blds, a.buildings) && h.bLen == a.buildings.Count && h.bId == a.nextBuildId;

        void Mark(int[] cnt, int r0, int c0, int h, int w, int d)
        {
            int n = N;
            for (int r = r0; r < r0 + h; r++)
                for (int c = c0; c < c0 + w; c++)
                    if (r >= 0 && c >= 0 && r < n && c < n) cnt[r * n + c] += d;
        }

        Entry Get(AreaState a)
        {
            _cache.TryGetValue(a, out var h);
            if (Fresh(a, h)) return h;
            var cnt = new int[N * N];
            foreach (var nd in a.nodes) Mark(cnt, nd.row, nd.col, nd.size, nd.size, 1);
            foreach (var b in a.buildings)
            {
                var (w, hh) = _ctx.Config.BuildingSize(b.type);
                Mark(cnt, b.row, b.col, hh, w, 1);
                var def = _ctx.Config.Building(b.type);
                if (b.built && def != null && def.fuel) Mark(cnt, b.row, b.col - 3, 2, 3, 1);
            }
            h = new Entry
            {
                cnt = cnt, epoch = _epoch,
                nodes = a.nodes, nLen = a.nodes.Count, nId = a.nextNodeId,
                blds = a.buildings, bLen = a.buildings.Count, bId = a.nextBuildId,
            };
            _cache[a] = h;
            return h;
        }

        /// <summary>Is cell (r,c) occupied? Out-of-grid cells are reported free (callers bound-check).</summary>
        public bool IsOccupied(AreaState a, int r, int c)
        {
            if (r < 0 || c < 0 || r >= N || c >= N) return false;
            return Get(a).cnt[r * N + c] > 0;
        }

        public bool AnyOccupied(AreaState a, int row, int col, int h, int w)
        {
            var e = Get(a);
            int n = N;
            for (int r = row; r < row + h; r++)
                for (int c = col; c < col + w; c++)
                    if (r >= 0 && c >= 0 && r < n && c < n && e.cnt[r * n + c] > 0) return true;
            return false;
        }

        /// <summary>Copy of the occupied flags (row-major N×N) — e.g. for placeDecoRing's private set.</summary>
        public bool[] Snapshot(AreaState a)
        {
            var e = Get(a);
            var res = new bool[e.cnt.Length];
            for (int i = 0; i < res.Length; i++) res[i] = e.cnt[i] > 0;
            return res;
        }

        /// <summary>`occNodeAdded` — node was just appended (id = nextNodeId-1).</summary>
        public void NodeAdded(AreaState a, Node node)
        {
            if (!_cache.TryGetValue(a, out var h)) return;
            if (h.epoch != _epoch || !ReferenceEquals(h.nodes, a.nodes) || h.nLen != a.nodes.Count - 1 || h.nId != node.id ||
                a.nextNodeId != node.id + 1 || !ReferenceEquals(h.blds, a.buildings) || h.bLen != a.buildings.Count ||
                h.bId != a.nextBuildId) return;
            Mark(h.cnt, node.row, node.col, node.size, node.size, 1);
            h.nLen = a.nodes.Count; h.nId = a.nextNodeId;
        }

        /// <summary>True if the cache is fresh right now (take before a removal, pass to <see cref="NodeRemoved"/>).</summary>
        public bool IsFresh(AreaState a) { _cache.TryGetValue(a, out var h); return Fresh(a, h); }

        /// <summary>`occNodeRemoved` — node was just removed; wasFresh = IsFresh before the removal.</summary>
        public void NodeRemoved(AreaState a, Node node, bool wasFresh)
        {
            if (!wasFresh || !_cache.TryGetValue(a, out var h)) return;
            Mark(h.cnt, node.row, node.col, node.size, node.size, -1);
            h.nLen = a.nodes.Count;
        }

        /// <summary>Recompute from scratch (test oracle).</summary>
        public bool[] Recompute(AreaState a)
        {
            _cache.Remove(a);
            return Snapshot(a);
        }
    }
}
