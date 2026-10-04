using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    public struct SuctionResult
    {
        public int moved;
        public int picked;
    }

    /// <summary>
    /// Ground items (engine-systems §4, engine.js:708-1001): dropGround with
    /// tags/grace, eviction classes & caps, suction/pickup, settle and
    /// push-out-of-colliders physics.
    /// </summary>
    public sealed class GroundSystem
    {
        readonly SimContext _ctx;
        Dictionary<string, int> _evictClass;

        public GroundSystem(SimContext ctx) { _ctx = ctx; }

        GameBalance Bal => _ctx.Config.balance;

        // ---- §4.2 eviction class ----

        /// <summary>Static class map derived from DATA (engine.js:726): 0 raw common, 1 other raw, 2 protected.</summary>
        public Dictionary<string, int> EvictClassMap
        {
            get
            {
                if (_evictClass != null) return _evictClass;
                var cfg = _ctx.Config;
                var crafted = new HashSet<string>();
                var raw = new HashSet<string>();
                foreach (var b in cfg.buildings) foreach (var r in b.recipes) crafted.Add(r.output);
                foreach (var a in cfg.regions)
                {
                    bool rawCost = true;
                    foreach (var c in a.unlockCost) if (crafted.Contains(c.item)) rawCost = false;
                    if (!rawCost) continue;
                    foreach (var t in a.tiers) { foreach (var s in t.perHit) raw.Add(s.item); foreach (var s in t.drops) raw.Add(s.item); }
                    foreach (var sp in a.spawners) { foreach (var s in sp.perHit) raw.Add(s.item); foreach (var s in sp.drops) raw.Add(s.item); }
                    foreach (var fx in a.fixtures) if (!string.IsNullOrEmpty(fx.drop)) raw.Add(fx.drop);
                    foreach (var g in a.generators) raw.Add(g.item);
                }
                var m = new Dictionary<string, int>();
                foreach (var it in cfg.items) m[it.key] = crafted.Contains(it.key) ? 2 : raw.Contains(it.key) ? 0 : 1;
                foreach (var it in new[] { "dragon_scale", "jade_shard", "firestone" }) m[it] = 2;
                _evictClass = m;
                return m;
            }
        }

        /// <summary>`evictClassOf(g)`.</summary>
        public int EvictClassOf(GroundItem g)
        {
            if (g.crafted) return 2;
            if (!EvictClassMap.TryGetValue(g.item ?? "", out int c)) return 1;
            return c == 2 && g.gen ? 1 : c;
        }

        // ---- §4.1 dropGround ----

        /// <summary>`dropGround(area,item,qty,x,y,tag)` engine.js:804 — one ground entry per unit.</summary>
        public void DropGround(string areaKey, string item, int qty, double x, double y, GroundTag tag = GroundTag.None)
        {
            var area = _ctx.State.Area(areaKey);
            if (tag == GroundTag.None && _ctx.AutoHarvesting) tag = GroundTag.Gen;
            double now = _ctx.Now;
            ManualSource? src = tag == GroundTag.Manual ? _ctx.ManualSrc : null;
            double manualAt = tag == GroundTag.Manual ? now + (src.HasValue ? src.Value.grace - Bal.manualGraceMs : 0) : 0;
            int freshFrom = area.ground.Count;
            var w = _ctx.World;
            var rng = _ctx.Rng;
            int j = Bal.dropJitterPx;
            for (int k = 0; k < qty; k++)
            {
                double jx = w.ClampPx(x + rng.Rand(-j, j)), jy = w.ClampPx(y + rng.Rand(-j, j));
                var g = new GroundItem { id = area.nextGroundId++, item = item, x = jx, y = jy };
                if (manualAt != 0) g.manualAt = manualAt;
                if (src.HasValue) g.src = src.Value.nodeId;
                if (tag == GroundTag.Crafted) g.crafted = true;
                else if (tag == GroundTag.Gen && EvictClassOf(g) == 2) g.gen = true;
                area.ground.Add(g);
            }
            if (area.ground.Count > Bal.groundCap) EvictGround(area, freshFrom);
            _ctx.Events.RaiseGroundDropped(areaKey, item, qty, x, y);
        }

        // ---- §4.3 eviction ----

        /// <summary>`evictGround(area, freshFrom)` engine.js:767. Returns count evicted.</summary>
        public int EvictGround(AreaState area, int freshFrom)
        {
            var g = area.ground;
            int n = g.Count;
            int excess = n - Bal.groundCap;
            if (excess <= 0) return 0;
            if (freshFrom < 0 || freshFrom > n) freshFrom = n;
            var cls = new sbyte[n];
            var outF = new bool[n];
            double now = _ctx.Now;
            int prot = 0, gone = 0;
            for (int i = 0; i < n; i++)
            {
                var it = g[i];
                int c = EvictClassOf(it);
                if (c == 2) prot++;
                cls[i] = (sbyte)(i >= freshFrom || (it.manualAt != 0 && now - it.manualAt < Bal.manualGraceMs) ? -1 : c);
            }
            for (int i = 0; i < n && gone < excess && prot > Bal.protectedShare; i++)
                if (cls[i] == 2) { outF[i] = true; gone++; prot--; }
            for (int c = 0; c <= 1 && gone < excess; c++)
                for (int i = 0; i < n && gone < excess; i++)
                    if (cls[i] == c) { outF[i] = true; gone++; }
            for (int pass = 0; pass < 2 && n - gone > Bal.groundHardCap; pass++)
                for (int i = 0; i < n && n - gone > Bal.groundHardCap; i++)
                    if (!outF[i] && (pass == 1 || i < freshFrom)) { outF[i] = true; gone++; }
            if (gone == 0) return 0;
            int wi = 0;
            for (int i = 0; i < n; i++) if (!outF[i]) g[wi++] = g[i];
            g.RemoveRange(wi, n - wi);
            return gone;
        }

        // ---- §4.4 pickup ----

        /// <summary>
        /// `suctionStep(area,x,y,radius,filter)` engine.js:964 — the left-hold
        /// vacuum. Call at a fixed rate (the JS ran it per animation frame).
        /// </summary>
        public SuctionResult SuctionStep(string areaKey, double x, double y, double radius, string itemFilter)
        {
            var res = new SuctionResult();
            var hand = _ctx.Hand;
            if (hand.Space() <= 0) return res;
            var area = _ctx.State.Area(areaKey);
            HashSet<int> taken = null;
            foreach (var g in area.ground)
            {
                if (itemFilter != null && g.item != itemFilter) continue;
                double dx = x - g.x, dy = y - g.y, d = Math.Sqrt(dx * dx + dy * dy);
                if (d > radius) continue;
                if (d <= Bal.pickupCollectPx)
                {
                    if (hand.Space() > 0 && hand.Add(g.item, 1) > 0) { (taken ??= new HashSet<int>()).Add(g.id); res.picked++; }
                    continue;
                }
                double pull = 1 + (1 - d / radius) * 3;
                g.x += dx / d * Math.Min(pull, d);
                g.y += dy / d * Math.Min(pull, d);
                res.moved++;
            }
            if (taken != null) area.ground.RemoveAll(g => taken.Contains(g.id));
            return res;
        }

        /// <summary>`pickupNear(area,x,y,radius)` engine.js:987 — instant vacuum, nearest first.</summary>
        public int PickupNear(string areaKey, double x, double y, double radius)
        {
            var area = _ctx.State.Area(areaKey);
            var near = new List<(GroundItem g, double d, int i)>();
            for (int i = 0; i < area.ground.Count; i++)
            {
                var g = area.ground[i];
                double d = Math.Sqrt((g.x - x) * (g.x - x) + (g.y - y) * (g.y - y));
                if (d <= radius) near.Add((g, d, i));
            }
            near.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.i.CompareTo(b.i));   // stable
            int picked = 0;
            var taken = new HashSet<int>();
            foreach (var o in near)
            {
                if (_ctx.Hand.Space() <= 0) break;
                if (_ctx.Hand.Add(o.g.item, 1) > 0) { taken.Add(o.g.id); picked++; }
            }
            if (taken.Count > 0) area.ground.RemoveAll(g => taken.Contains(g.id));
            return picked;
        }

        /// <summary>Is the item still in its Gathering-Stone grace window?</summary>
        public bool InGrace(GroundItem g, double now) => g.manualAt != 0 && now - g.manualAt < Bal.manualGraceMs;

        /// <summary>`refreshNodeGrace` engine.js:1013.</summary>
        public void RefreshNodeGrace(AreaState area, Node node, int grace)
        {
            double at = _ctx.Now + grace - Bal.manualGraceMs;
            foreach (var g in area.ground)
                if (g.src == node.id && g.manualAt != 0 && g.manualAt < at) g.manualAt = at;
        }

        // ---- §4.5 physics (not run during offline replay) ----
        const double SettleEps = 0.05;

        /// <summary>`settleGround(area)` engine.js:860 — pairwise repulsion via 64-px buckets. Returns visible pushes.</summary>
        public int SettleGround(string areaKey)
        {
            var items = _ctx.State.Area(areaKey).ground;
            int n = items.Count;
            if (n < 2) return 0;
            double MIN = Bal.settleMinPx, MIN2 = MIN * MIN + 1e-6;
            int G = (_ctx.PlayPx >> 6) + 1;
            var head = new int[G * G];
            for (int i = 0; i < head.Length; i++) head[i] = -1;
            var next = new int[n];
            var cellOf = new int[n];
            for (int i = n - 1; i >= 0; i--)
            {
                int cx = Bucket(items[i].x, G), cy = Bucket(items[i].y, G);
                int c = cx * G + cy;
                cellOf[i] = c; next[i] = head[c]; head[c] = i;
            }
            int moves = 0, pushed = 0;
            var rng = _ctx.Rng;
            for (int i = 0; i < n; i++)
            {
                var a = items[i];
                int cx = cellOf[i] / G, cy = cellOf[i] - cx * G;
                for (int nx = cx - 1; nx <= cx + 1; nx++)
                {
                    if (nx < 0 || nx >= G) continue;
                    for (int ny = cy - 1; ny <= cy + 1; ny++)
                    {
                        if (ny < 0 || ny >= G) continue;
                        for (int j = head[nx * G + ny]; j != -1; j = next[j])
                        {
                            if (j <= i) continue;
                            var b = items[j];
                            double dx = b.x - a.x, dy = b.y - a.y;
                            if (dx * dx + dy * dy > MIN2) continue;
                            double d = Math.Sqrt(dx * dx + dy * dy);
                            if (d < 0.01)
                            {
                                int rx = rng.Rand(-10, 10), ry = rng.Rand(-10, 10);
                                dx = rx != 0 ? rx : 1; dy = ry != 0 ? ry : 1;
                                d = Math.Sqrt(dx * dx + dy * dy);
                            }
                            if (d < MIN)
                            {
                                double push = (MIN - d) / 2, ux = dx / d, uy = dy / d;
                                a.x -= ux * push; a.y -= uy * push; b.x += ux * push; b.y += uy * push;
                                pushed++;
                                if (push > SettleEps) moves++;
                            }
                        }
                    }
                }
            }
            if (pushed > 0)
            {
                double HI = _ctx.PlayPx - 4;
                var w = _ctx.World;
                foreach (var it in items)
                {
                    if (it.x < 4 || it.x > HI) it.x = w.ClampPx(it.x);
                    if (it.y < 4 || it.y > HI) it.y = w.ClampPx(it.y);
                }
            }
            return moves;
        }

        static int Bucket(double v, int G)
        {
            if (double.IsNaN(v)) return 0;
            int c = (int)Math.Floor(v / 64.0);   // JS `v >> 6` (truncation; equal for v ≥ 0, clamped below)
            return c < 0 ? 0 : c >= G ? G - 1 : c;
        }

        struct Rect { public double x0, y0, x1, y1; }

        /// <summary>`pushOutOfColliders(area)` engine.js:911. Returns visible moves.</summary>
        public int PushOutOfColliders(string areaKey)
        {
            var area = _ctx.State.Area(areaKey);
            if (area.ground.Count == 0) return 0;
            int CELL = _ctx.Cell;
            var cfg = _ctx.Config;
            var rects = new List<Rect>();
            foreach (var b in area.buildings)
            {
                var (w, h) = cfg.BuildingSize(b.type);
                rects.Add(new Rect { x0 = b.col * CELL, y0 = b.row * CELL, x1 = (b.col + w) * CELL, y1 = (b.row + h) * CELL });
                var def = cfg.Building(b.type);
                if (b.built && def != null && def.fuel)
                    rects.Add(new Rect { x0 = (b.col - 3) * CELL, y0 = b.row * CELL, x1 = b.col * CELL, y1 = (b.row + 2) * CELL });
            }
            foreach (var nd in area.nodes)
                if (nd.isFixed) rects.Add(new Rect { x0 = nd.col * CELL, y0 = nd.row * CELL, x1 = (nd.col + nd.size) * CELL, y1 = (nd.row + nd.size) * CELL });
            if (rects.Count == 0) return 0;
            bool InAny(double x, double y)
            {
                foreach (var r in rects) if (x > r.x0 && x < r.x1 && y > r.y0 && y < r.y1) return true;
                return false;
            }
            var wd = _ctx.World;
            double now = _ctx.Now;
            int moved = 0;
            var exits = new List<(double d, double x, double y)>(4);
            foreach (var g in area.ground)
            {
                if (g.pullAt != 0 && now - g.pullAt < 200) continue;
                foreach (var r in rects)
                {
                    if (g.x <= r.x0 || g.x >= r.x1 || g.y <= r.y0 || g.y >= r.y1) continue;
                    exits.Clear();
                    exits.Add((g.x - r.x0, wd.ClampPx(r.x0 - 8), wd.ClampPx(g.y)));
                    exits.Add((r.x1 - g.x, wd.ClampPx(r.x1 + 8), wd.ClampPx(g.y)));
                    exits.Add((g.y - r.y0, wd.ClampPx(g.x), wd.ClampPx(r.y0 - 8)));
                    exits.Add((r.y1 - g.y, wd.ClampPx(g.x), wd.ClampPx(r.y1 + 8)));
                    StableSort(exits, e => e.d);
                    if (g.hasPullTo && now - g.pullAt < 5000)
                    {
                        double px = g.pullToX, py = g.pullToY;
                        StableSort(exits, e => Math.Sqrt((e.x - px) * (e.x - px) + (e.y - py) * (e.y - py)));
                    }
                    var ex = exits[0];
                    foreach (var e in exits) if (!InAny(e.x, e.y)) { ex = e; break; }
                    if (Math.Abs(ex.x - g.x) + Math.Abs(ex.y - g.y) > SettleEps) moved++;
                    g.x = ex.x; g.y = ex.y;
                }
            }
            return moved;
        }

        static void StableSort<T>(List<T> list, Func<T, double> key)
        {
            // insertion sort: stable, tiny lists
            for (int i = 1; i < list.Count; i++)
            {
                var v = list[i];
                double k = key(v);
                int j = i - 1;
                while (j >= 0 && key(list[j]) > k) { list[j + 1] = list[j]; j--; }
                list[j + 1] = v;
            }
        }
    }
}
