using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    public enum UpgradeNodeTier { Full, Mystery, Hidden }

    /// <summary>Tree-panel read-out of one node (ui.js treeStates + engine upgradeLevel/upgradeCost).</summary>
    public sealed class UpgradeNodeState
    {
        public UpgradeNodeDef node;
        /// <summary>BFS distance from owned nodes ∪ root "hand" (99 = unreachable).</summary>
        public int distance;
        public UpgradeNodeTier tier;
        /// <summary>Drawn at all (tier != Hidden). The JS debug toggle is a view concern.</summary>
        public bool visible;
        /// <summary>Can be picked as the Altar job (tier Full and root/owned/adjacent to owned).</summary>
        public bool selectable;
        public bool owned;
        public int level, max;
        public bool maxed;
        /// <summary>Scaled cost of the next level (null when maxed).</summary>
        public ItemCounts nextCost;
        /// <summary>This node is the current Altar job.</summary>
        public bool selected;
        /// <summary>Undirected neighbour ids (edges to draw: node.links).</summary>
        public List<string> neighbours = new List<string>();
        /// <summary>Reused cost map for the non-allocating UpgradeSystem.TreeStates(into).</summary>
        internal ItemCounts costBuffer;
    }

    /// <summary>
    /// Altar upgrade tree (engine-systems §12.3, engine.js:2070-2157): levels,
    /// scaled costs, the upgradeJob fed at the Altar, effects, and the tree
    /// visibility BFS (ui.js:1564). Effect consumers read areas[].upgrades
    /// directly; combat/automation derived values are exposed here for M6.
    /// </summary>
    public sealed class UpgradeSystem
    {
        readonly SimContext _ctx;
        public UpgradeSystem(SimContext ctx) { _ctx = ctx; }

        GameConfig Cfg => _ctx.Config;
        GameState S => _ctx.State;

        public const string RootId = "hand";

        // ---- levels & costs ----

        /// <summary>`upgradeLevel(area,type)` → (lvl, max). Unknown ⇒ (0,0).</summary>
        public (int lvl, int max) Level(string areaKey, string type)
        {
            var a = S.Area(areaKey);
            if (a == null) return (0, 0);
            var up = a.upgrades;
            switch (type)
            {
                case "tier": return (up.maxTier - 1, 4);
                case "hand": return (S.handLevel, 3);
                case "speed": case "harvestSpeed": case "automation": case "quarry":
                case "enemyCap": case "damage": case "aoe": case "wispRate":
                case "affinity": case "discipleCap":
                    return (up.Get(type), 3);
                default: return (0, 0);
            }
        }

        public UpgradeNodeDef Node(string areaKey, string type)
        {
            foreach (var n in Cfg.upgradeTree) if (n.area == areaKey && n.type == type) return n;
            return null;
        }

        public UpgradeNodeDef NodeById(string id)
        {
            foreach (var n in Cfg.upgradeTree) if (n.id == id) return n;
            return null;
        }

        /// <summary>`upgradeCost(area,type)` — scaled next-level cost, null when maxed / no data.</summary>
        public ItemCounts Cost(string areaKey, string type)
        {
            var res = new ItemCounts();
            return Cost(areaKey, type, res) ? res : null;
        }

        /// <summary>Non-allocating <see cref="Cost(string, string)"/> into <paramref name="into"/> (cleared with reuse); false = null (maxed / no data).</summary>
        public bool Cost(string areaKey, string type, ItemCounts into)
        {
            into.ClearReuse();
            var (lvl, max) = Level(areaKey, type);
            if (max <= 0 || lvl >= max) return false;
            var node = Node(areaKey, type);
            if (node?.costs == null || lvl >= node.costs.Count || node.costs[lvl] == null) return false;
            foreach (var c in node.costs[lvl].items) into.Set(c.item, _ctx.Timing.Scaled(c.qty));
            return true;
        }

        // ---- the Altar job ----

        /// <summary>`jobRemaining(job)` = needs − paid (positive parts).</summary>
        public ItemCounts JobRemaining(UpgradeJob job)
        {
            var rem = new ItemCounts();
            if (job?.needs == null) return rem;
            foreach (var e in job.needs)
            {
                int r = e.qty - job.paid.Get(e.item);
                if (r > 0) rem.Set(e.item, r);
            }
            return rem;
        }

        /// <summary>`refundUpgradeJob()` — drop the job's paid items at the Altar centre (manual), clear the job.</summary>
        public void RefundJob()
        {
            var job = S.upgradeJob;
            S.upgradeJob = null;
            if (job == null) return;
            var altar = S.Area("center")?.buildings.Find(b => b.type == "center");
            double x, y;
            if (altar != null) (x, y) = _ctx.World.BuildingCenterPx(altar);
            else { x = _ctx.PlayPx / 2.0; y = _ctx.PlayPx / 2.0; }
            foreach (var e in job.paid)
                if (e.qty > 0) { _ctx.Ground.DropGround("center", e.item, e.qty, x, y, GroundTag.Manual); _ctx.Flow.Unconsume(e.item, e.qty); }
        }

        /// <summary>`selectUpgrade(area,type)` — maxed ⇒ false; same job ⇒ true; else refund the old job and start this one.</summary>
        public bool Select(string areaKey, string type)
        {
            var cost = Cost(areaKey, type);
            if (cost == null) return false;
            var job = S.upgradeJob;
            if (job != null && job.area == areaKey && job.type == type) return true;
            if (job != null) RefundJob();
            S.upgradeJob = new UpgradeJob { area = areaKey, type = type, needs = cost, paid = new ItemCounts() };
            return true;
        }

        /// <summary>Select by tree node id; also enforces UI selectability (BFS rule).</summary>
        public bool SelectNode(string nodeId)
        {
            var n = NodeById(nodeId);
            if (n == null) return false;
            foreach (var st in TreeStates()) if (st.node == n && !st.selectable) return false;
            return Select(n.area, n.type);
        }

        /// <summary>dropFromHand step 3 (engine.js:1919) — feed the job; completion applies it.</summary>
        public bool AltarFeed(string areaKey, Building b, out DropResult result)
        {
            var job = S.upgradeJob;
            if (job == null) { result = null; return true; }
            result = _ctx.Hand.FeedNeeds(JobRemaining(job), job.paid);
            if (result != null && result.kind == DropResultKind.Fed) _ctx.Flow.Consume(result.item, 1);
            if (result != null && result.kind == DropResultKind.Fed && JobRemaining(job).Count == 0)
            {
                Apply(job.area, job.type);
                S.upgradeJob = null;
            }
            return true;
        }

        /// <summary>`applyUpgrade(area,type)` — stats, sfx, increment (hand ⇒ handLevel++, handCap += 5).</summary>
        public void Apply(string areaKey, string type)
        {
            var up = S.Area(areaKey).upgrades;
            S.stats.upgradesApplied++;
            _ctx.Events.RaiseSound("upgrade", areaKey);
            switch (type)
            {
                case "tier": up.maxTier++; break;
                case "hand": S.handLevel++; S.handCap += 5; break;
                default: up.Set(type, up.Get(type) + 1); break;
            }
            _ctx.Events.RaiseUpgradeApplied(areaKey, type);
        }

        // ---- tree visibility (ui.js treeStates) ----

        /// <summary>Every tree node's state, in config order.</summary>
        public List<UpgradeNodeState> TreeStates()
        {
            var nodes = Cfg.upgradeTree;
            var adj = new Dictionary<string, List<string>>();
            foreach (var n in nodes) adj[n.id] = new List<string>();
            foreach (var n in nodes)
                foreach (var l in n.links)
                {
                    if (!adj.ContainsKey(l)) continue;
                    if (!adj[n.id].Contains(l)) adj[n.id].Add(l);
                    if (!adj[l].Contains(n.id)) adj[l].Add(n.id);
                }
            var owned = new HashSet<string>();
            foreach (var n in nodes) if (Level(n.area, n.type).lvl > 0) owned.Add(n.id);
            var dist = new Dictionary<string, int>();
            var q = new Queue<string>();
            foreach (var n in nodes) if (owned.Contains(n.id)) { dist[n.id] = 0; q.Enqueue(n.id); }
            if (adj.ContainsKey(RootId) && !dist.ContainsKey(RootId)) { dist[RootId] = 0; q.Enqueue(RootId); }
            while (q.Count > 0)
            {
                var id = q.Dequeue();
                foreach (var nb in adj[id]) if (!dist.ContainsKey(nb)) { dist[nb] = dist[id] + 1; q.Enqueue(nb); }
            }
            var job = S.upgradeJob;
            var res = new List<UpgradeNodeState>();
            foreach (var n in nodes)
            {
                int d = dist.TryGetValue(n.id, out var dv) ? dv : 99;
                var tier = d <= 1 ? UpgradeNodeTier.Full : d == 2 ? UpgradeNodeTier.Mystery : UpgradeNodeTier.Hidden;
                bool adjOwned = false;
                foreach (var a in adj[n.id]) if (owned.Contains(a)) { adjOwned = true; break; }
                var (lvl, max) = Level(n.area, n.type);
                res.Add(new UpgradeNodeState
                {
                    node = n, distance = d, tier = tier, visible = tier != UpgradeNodeTier.Hidden,
                    selectable = tier == UpgradeNodeTier.Full && (n.id == RootId || owned.Contains(n.id) || adjOwned),
                    owned = owned.Contains(n.id), level = lvl, max = max, maxed = max > 0 && lvl >= max,
                    nextCost = Cost(n.area, n.type),
                    selected = job != null && job.area == n.area && job.type == n.type,
                    neighbours = adj[n.id],
                });
            }
            return res;
        }

        // scratch for the non-allocating TreeStates(into)
        readonly Dictionary<string, List<string>> _adj = new Dictionary<string, List<string>>();
        readonly List<List<string>> _adjPool = new List<List<string>>();
        readonly HashSet<string> _owned = new HashSet<string>();
        readonly Dictionary<string, int> _dist = new Dictionary<string, int>();
        readonly Queue<string> _bfs = new Queue<string>();

        /// <summary>
        /// Non-allocating <see cref="TreeStates()"/> (same values, config order): refills
        /// <paramref name="into"/>, reusing its <see cref="UpgradeNodeState"/> objects, their
        /// <c>neighbours</c> lists and their <c>nextCost</c> maps (nextCost is null when maxed, as before).
        /// </summary>
        public void TreeStates(List<UpgradeNodeState> into)
        {
            var nodes = Cfg.upgradeTree;
            var adj = _adj;
            foreach (var kv in adj) { kv.Value.Clear(); _adjPool.Add(kv.Value); }
            adj.Clear();
            foreach (var n in nodes)
            {
                List<string> l;
                if (_adjPool.Count > 0) { l = _adjPool[_adjPool.Count - 1]; _adjPool.RemoveAt(_adjPool.Count - 1); }
                else l = new List<string>();
                if (adj.TryGetValue(n.id, out var old)) { old.Clear(); _adjPool.Add(old); }
                adj[n.id] = l;
            }
            foreach (var n in nodes)
                foreach (var l in n.links)
                {
                    if (!adj.ContainsKey(l)) continue;
                    if (!adj[n.id].Contains(l)) adj[n.id].Add(l);
                    if (!adj[l].Contains(n.id)) adj[l].Add(n.id);
                }
            var owned = _owned; owned.Clear();
            foreach (var n in nodes) if (Level(n.area, n.type).lvl > 0) owned.Add(n.id);
            var dist = _dist; dist.Clear();
            var q = _bfs; q.Clear();
            foreach (var n in nodes) if (owned.Contains(n.id)) { dist[n.id] = 0; q.Enqueue(n.id); }
            if (adj.ContainsKey(RootId) && !dist.ContainsKey(RootId)) { dist[RootId] = 0; q.Enqueue(RootId); }
            while (q.Count > 0)
            {
                var id = q.Dequeue();
                foreach (var nb in adj[id]) if (!dist.ContainsKey(nb)) { dist[nb] = dist[id] + 1; q.Enqueue(nb); }
            }
            var job = S.upgradeJob;
            if (into.Count > nodes.Count) into.RemoveRange(nodes.Count, into.Count - nodes.Count);
            for (int k = 0; k < nodes.Count; k++)
            {
                var n = nodes[k];
                UpgradeNodeState s;
                if (k < into.Count && into[k] != null) s = into[k];
                else { s = new UpgradeNodeState(); if (k < into.Count) into[k] = s; else into.Add(s); }
                int d = dist.TryGetValue(n.id, out var dv) ? dv : 99;
                var tier = d <= 1 ? UpgradeNodeTier.Full : d == 2 ? UpgradeNodeTier.Mystery : UpgradeNodeTier.Hidden;
                bool adjOwned = false;
                var nbs = adj[n.id];
                foreach (var a in nbs) if (owned.Contains(a)) { adjOwned = true; break; }
                var (lvl, max) = Level(n.area, n.type);
                s.node = n; s.distance = d; s.tier = tier; s.visible = tier != UpgradeNodeTier.Hidden;
                s.selectable = tier == UpgradeNodeTier.Full && (n.id == RootId || owned.Contains(n.id) || adjOwned);
                s.owned = owned.Contains(n.id); s.level = lvl; s.max = max; s.maxed = max > 0 && lvl >= max;
                s.costBuffer ??= new ItemCounts();
                s.nextCost = Cost(n.area, n.type, s.costBuffer) ? s.costBuffer : null;
                s.selected = job != null && job.area == n.area && job.type == n.type;
                if (s.neighbours == null || ReferenceEquals(s.neighbours, nbs)) s.neighbours = new List<string>();
                s.neighbours.Clear();
                s.neighbours.AddRange(nbs);
            }
        }

        // ---- derived effect values for systems not yet ported (M6) ----

        /// <summary>Bot click budget per automation tick: AUTOMATION_CLICKS[lvl] + perk autoboost (0 at lvl 0).</summary>
        public int AutomationBudget(string areaKey)
        {
            int lvl = S.Area(areaKey)?.upgrades.automation ?? 0;
            return lvl <= 0 ? 0 : Cfg.balance.AutomationClicks(lvl) + S.PerkLevel("autoboost");
        }

        /// <summary>Regular enemy cap = config cap + Spirit Call level.</summary>
        public int EnemyCap(string areaKey)
        {
            var e = Cfg.Region(areaKey)?.enemies;
            if (e == null || !e.enabled) return 0;
            return e.cap + (S.Area(areaKey)?.upgrades.enemyCap ?? 0);
        }

        /// <summary>Strike damage: 1 + Spirit Blade + Martial Vigor bonus + Fury perk.</summary>
        public int AttackDamage(string areaKey, double now) =>
            1 + (S.Area(areaKey)?.upgrades.damage ?? 0)
              + (Timing.CombatBuffActive(S, now) ? Cfg.vitality.bonusDamage : 0) + S.PerkLevel("fury");

        /// <summary>Spirit Wave splash radius in px (aoe · 1.5 cells).</summary>
        public double AoeRadiusPx(string areaKey) => (S.Area(areaKey)?.upgrades.aoe ?? 0) * 1.5 * _ctx.Cell;
    }
}
