using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// Resource nodes (engine-systems §6, engine.js:443-706, 1003-1103):
    /// initArea (Altar/Dragon, fixtures, deco rings, spawner fill + trim),
    /// spawnFromSpawner, harvest (chop/instant/break/surface/quarry), rare
    /// drops, depleteNode → regrow queue, surface dives, respawn queue.
    /// </summary>
    public sealed class NodeSystem
    {
        readonly SimContext _ctx;
        public NodeSystem(SimContext ctx) { _ctx = ctx; }

        GameConfig Cfg => _ctx.Config;
        GameState S => _ctx.State;
        IRng Rng => _ctx.Rng;

        /// <summary>`areaScale()` engine.js:519 = max(1, round((N/24)²)) — 15 for N=93.</summary>
        public int AreaScale()
        {
            double v = Math.Pow(Cfg.grid.cells / 24.0, 2);
            return Math.Max(1, (int)Math.Floor(v + 0.5));   // JS Math.round
        }

        public int SpawnerTarget(SpawnerDef sp) => sp.scaleWithArea ? sp.target * AreaScale() : sp.target;

        /// <summary>`nodeSpecs(area,node)` engine.js:451.</summary>
        public (List<DropSpec> perHit, List<DropSpec> drops) NodeSpecs(string areaKey, Node node)
        {
            var empty = new List<DropSpec>();
            if (node.useTiers)
            {
                var tiers = Cfg.Region(areaKey).tiers;
                var t = node.tier - 1 >= 0 && node.tier - 1 < tiers.Count ? tiers[node.tier - 1] : null;
                return (t?.perHit ?? empty, t?.drops ?? empty);
            }
            return (node.perHit ?? empty, node.drops ?? empty);
        }

        // ---- §6.2 initArea ----

        /// <summary>`initArea(area)` engine.js:550 — idempotent, run on every boot.</summary>
        public void InitArea(string areaKey)
        {
            var cfg = Cfg.Region(areaKey);
            var area = S.Area(areaKey);
            int N = _ctx.N;
            if (areaKey == "center" && !area.buildings.Exists(b => b.type == "center"))
            {
                var (w, h) = Cfg.BuildingSize("center");
                area.buildings.Add(new Building
                {
                    id = area.nextBuildId++, type = "center", row = (N - h) / 2, col = (N - w) / 2,
                    built = true,
                });
            }
            if (areaKey == "center" && !area.buildings.Exists(b => b.type == "dragon"))
            {
                var (w, h) = Cfg.BuildingSize("dragon");
                var z = Cfg.ZoneRects("cornerTL")[0];
                area.buildings.Add(new Building
                {
                    id = area.nextBuildId++, type = "dragon",
                    row = z.r0 + (z.r1 - z.r0 + 1 - h) / 2,
                    col = z.c0 + (z.c1 - z.c0 + 1 - w) / 2,
                    built = true,
                });
            }
            foreach (var fx in cfg.fixtures)
                if (!area.nodes.Exists(n => n.kind == fx.kind)) PlaceFixture(areaKey, fx);
            if (areaKey == "center" && !area.nodes.Exists(n => n.kind == "deco"))
            {
                foreach (var z in Cfg.ZoneRects("cornerBL")) PlaceDecoRing(areaKey, z, "🌲");
                foreach (var z in Cfg.ZoneRects("cornerBR")) PlaceDecoRing(areaKey, z, "🌲");
            }
            foreach (var sp in cfg.spawners)
            {
                int target = SpawnerTarget(sp);
                int guard = 0;
                while (LiveCount(area, sp.kind) < target && guard++ < target * 8 + 50)
                    if (SpawnFromSpawner(areaKey, sp) == null) break;
                if (LiveCount(area, sp.kind) > target)
                {
                    var excess = new HashSet<int>();
                    int seen = 0;
                    foreach (var n in area.nodes)
                        if (n.spawnerKind == sp.kind && seen++ >= target) excess.Add(n.id);
                    area.nodes.RemoveAll(n => excess.Contains(n.id));
                }
            }
            area.genTimers = new List<double>();
            foreach (var _ in cfg.generators) area.genTimers.Add(0);
        }

        static int LiveCount(AreaState area, string kind)
        {
            int c = 0;
            foreach (var n in area.nodes) if (n.spawnerKind == kind) c++;
            return c;
        }

        /// <summary>`placeFixture` engine.js:502 — centred in the zone's first rect.</summary>
        public Node PlaceFixture(string areaKey, FixtureDef fx)
        {
            var area = S.Area(areaKey);
            var z = Cfg.ZoneRects(fx.zone)[0];
            int row = z.r0 + FloorDiv(z.r1 - z.r0 + 1 - fx.size, 2);
            int col = z.c0 + FloorDiv(z.c1 - z.c0 + 1 - fx.size, 2);
            var node = new Node
            {
                id = area.nextNodeId++, row = row, col = col, size = fx.size, kind = fx.kind, interaction = fx.interaction,
                isFixed = true, tier = 1, deco = fx.interaction == NodeInteraction.None,
                clicks = 0, clicksPerDrop = fx.clicksPerDrop, dropItem = fx.drop,
                dropMin = fx.dropMin, dropMax = fx.dropMax, rareDrop = RareDrop.IsSet(fx.rareDrop) ? fx.rareDrop : null,
                swingMs = fx.swingMs > 0 ? fx.swingMs : 1000, sprite = string.IsNullOrEmpty(fx.sprite) ? "⛰️" : fx.sprite,
            };
            area.nodes.Add(node);
            return node;
        }

        static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);

        /// <summary>`placeDecoRing` engine.js:525 — inert ring around a rect with an entrance facing the map centre.</summary>
        public void PlaceDecoRing(string areaKey, ZoneRect rect, string sprite)
        {
            var area = S.Area(areaKey);
            int N = _ctx.N;
            var occ = _ctx.Occupancy.Snapshot(area);   // private copy, grown below
            double cx = (rect.c0 + rect.c1 + 1) / 2.0, cy = (rect.r0 + rect.r1 + 1) / 2.0;
            double aim = Math.Atan2(N / 2.0 - cy, N / 2.0 - cx);
            for (int r = rect.r0 - 1; r <= rect.r1 + 1; r++)
                for (int c = rect.c0 - 1; c <= rect.c1 + 1; c++)
                {
                    bool onRing = r == rect.r0 - 1 || r == rect.r1 + 1 || c == rect.c0 - 1 || c == rect.c1 + 1;
                    if (!onRing || r < 0 || c < 0 || r >= N || c >= N) continue;
                    if (occ[r * N + c]) continue;
                    double ang = Math.Atan2(r + 0.5 - cy, c + 0.5 - cx);
                    double diff = Math.Atan2(Math.Sin(ang - aim), Math.Cos(ang - aim));
                    if (Math.Abs(diff) < 0.55) continue;
                    area.nodes.Add(new Node
                    {
                        id = area.nextNodeId++, row = r, col = c, size = 1, kind = "deco",
                        interaction = NodeInteraction.None, deco = true, tier = 1, sprite = sprite,
                        decoScale = 1.6 + Rng.Next01() * 0.8,
                        decoDx = Rng.Rand(-6, 6), decoDy = Rng.Rand(-3, 3),
                    });
                    occ[r * N + c] = true;
                }
        }

        // ---- §6.3 spawnFromSpawner ----

        /// <summary>`spawnFromSpawner(area, sp)` engine.js:460 — null when the zone is full.</summary>
        public Node SpawnFromSpawner(string areaKey, SpawnerDef sp)
        {
            var area = S.Area(areaKey);
            var rects = Cfg.ZoneRects(sp.zone);
            var occ = _ctx.Occupancy;
            if (rects.Count == 0 || sp.sizes.Count == 0) return null;
            for (int attempt = 0; attempt < Cfg.balance.spawnAttempts; attempt++)
            {
                int size = sp.sizes[Rng.Rand(0, sp.sizes.Count - 1)];
                var z = rects[Rng.Rand(0, rects.Count - 1)];
                if (z.r1 - z.r0 + 1 < size || z.c1 - z.c0 + 1 < size) continue;
                int row = Rng.Rand(z.r0, z.r1 - size + 1);
                int col = Rng.Rand(z.c0, z.c1 - size + 1);
                if (occ.AnyOccupied(area, row, col, size, size)) continue;
                if (sp.spacing > 0)
                {
                    bool tooClose = false;
                    foreach (var n in area.nodes)
                        if (n.spawnerKind == sp.kind && Hypot(n.row - row, n.col - col) < sp.spacing) { tooClose = true; break; }
                    if (tooClose) continue;
                }
                var reg = Cfg.Region(areaKey);
                bool useTiers = sp.useTiers;
                int tier = 1;   // rollTier(): tiers removed — always 1
                var tierDef = useTiers && reg.tiers.Count >= tier ? reg.tiers[tier - 1] : null;
                double surfaceWin = reg.surfaceWindow > 0 ? reg.surfaceWindow : Cfg.balance.defaultSurfaceWindowSec;
                var node = new Node
                {
                    id = area.nextNodeId++, row = row, col = col, size = size,
                    kind = sp.kind, spawnerKind = sp.kind, interaction = sp.interaction, useTiers = useTiers,
                    tier = tier,
                    hitsLeft = useTiers ? (tierDef != null && tierDef.hits > 0 ? tierDef.hits : 1) : (sp.hits > 0 ? sp.hits : 1),
                    regrowSec = useTiers ? (tierDef?.timer ?? 0) : (sp.regrow > 0 ? sp.regrow : 10),
                    swingMs = sp.swingMs > 0 ? sp.swingMs : 350,
                    sprite = string.IsNullOrEmpty(sp.sprite) ? null : sp.sprite,
                    perHit = sp.perHit != null && sp.perHit.Count > 0 ? sp.perHit : null,
                    drops = sp.drops != null && sp.drops.Count > 0 ? sp.drops : null,
                    rareDrop = RareDrop.IsSet(sp.rareDrop) ? sp.rareDrop : null,
                    surfaceUntil = sp.interaction == NodeInteraction.Surface ? _ctx.Now + surfaceWin * 1000 : 0,
                };
                area.nodes.Add(node);
                occ.NodeAdded(area, node);
                _ctx.Events.RaiseNodeSpawned(areaKey, node);
                return node;
            }
            return null;
        }

        static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);

        // ---- §6.5 regrow ----

        /// <summary>Regrow delay in ms for a node in an area (engine.js:701-704).</summary>
        public double RegrowDelayMs(string areaKey, Node node)
        {
            var area = S.Area(areaKey);
            var t = _ctx.Timing;
            double buffFac = Timing.BuffActive(S, "verdant_pill", _ctx.Now) ? 0.5 : 1;
            double regrow = node.regrowSec != 0 ? node.regrowSec : 10;
            return regrow * Math.Pow(0.8, area.upgrades.speed) * t.TimeScale * 1000 * buffFac
                   * t.PrestigeFactor(S) * Math.Pow(0.9, S.PerkLevel("regrow"));
        }

        /// <summary>`depleteNode(area,node)` engine.js:693 — remove + queue a replacement at a new random spot.</summary>
        public void DepleteNode(string areaKey, Node node)
        {
            var area = S.Area(areaKey);
            int i = area.nodes.IndexOf(node);
            if (i >= 0)
            {
                bool fresh = _ctx.Occupancy.IsFresh(area);
                area.nodes.RemoveAt(i);
                _ctx.Occupancy.NodeRemoved(area, node, fresh);
            }
            area.spawnQueue.Add(new SpawnQueueEntry(_ctx.Now + RegrowDelayMs(areaKey, node), node.spawnerKind));
            _ctx.Events.RaiseNodeDepleted(areaKey, node);
        }

        // ---- gameTick steps 1-2 ----

        /// <summary>Step 1: surfaced nodes not caught in time dive (no drops).</summary>
        public bool TickSurfaceDives(string areaKey, double now)
        {
            var area = S.Area(areaKey);
            bool changed = false;
            var nodes = area.nodes;
            int first = -1;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].surfaceUntil != 0 && now >= nodes[i].surfaceUntil) { first = i; break; }
            if (first < 0) return false;            // nothing surfaced is due — no snapshot needed
            // snapshot (DepleteNode may mutate the list) into a reused buffer
            var snap = _nodeSnap;
            snap.Clear(); snap.AddRange(nodes);
            for (int i = first; i < snap.Count; i++)
            {
                var node = snap[i];
                if (node.surfaceUntil != 0 && now >= node.surfaceUntil) { DepleteNode(areaKey, node); changed = true; }
            }
            snap.Clear();
            return changed;
        }
        readonly List<Node> _nodeSnap = new List<Node>();

        /// <summary>Step 2: due respawns (failure ⇒ re-queue at now+500).</summary>
        public bool TickRespawnQueue(string areaKey, double now)
        {
            var area = S.Area(areaKey);
            if (area.spawnQueue.Count == 0) return false;
            bool anyDue = false;
            for (int i = 0; i < area.spawnQueue.Count && !anyDue; i++) if (area.spawnQueue[i].at <= now) anyDue = true;
            if (!anyDue) return false;             // nothing due: skip the FindAll/RemoveAll closures
            return SpawnDue(areaKey, area, now);
        }

        // split out so the lambdas' closure over `now` is only allocated when something is due
        bool SpawnDue(string areaKey, AreaState area, double now)
        {
            var cfg = Cfg.Region(areaKey);
            var due = area.spawnQueue.FindAll(e => e.at <= now);
            area.spawnQueue.RemoveAll(e => e.at <= now);
            bool changed = false;
            foreach (var e in due)
            {
                var sp = cfg.spawners.Find(s => s.kind == e.kind);
                if (sp != null && SpawnFromSpawner(areaKey, sp) != null) changed = true;
                else if (sp != null) area.spawnQueue.Add(new SpawnQueueEntry(now + Cfg.balance.respawnRetryMs, e.kind));
            }
            return changed;
        }

        // ---- §6.4 harvesting ----

        /// <summary>`harvestNode(area,id,isAuto,held)` engine.js:1022.</summary>
        public bool Harvest(string areaKey, int nodeId, bool isAuto, bool held)
        {
            var area = S.Area(areaKey);
            var node = area?.NodeById(nodeId);
            if (node == null || node.deco) return false;
            if (!isAuto) _ctx.Events.RaiseSound("harvest", areaKey);
            var tag = _ctx.AutoHarvesting ? GroundTag.None : GroundTag.Manual;
            if (tag == GroundTag.Manual)
            {
                int grace = node.isFixed ? Cfg.balance.fixtureGraceMs : Cfg.balance.manualGraceMs;
                _ctx.Ground.RefreshNodeGrace(area, node, grace);
                _ctx.ManualSrc = new ManualSource { nodeId = node.id, grace = grace };
            }
            try { return HarvestSwing(areaKey, node, isAuto, tag); }
            finally { _ctx.ManualSrc = null; }
        }

        bool HarvestSwing(string areaKey, Node node, bool isAuto, GroundTag tag)
        {
            double now = _ctx.Now;
            double flashMs = Math.Max(node.swingMs > 0 ? node.swingMs : 400, 1000) + 300;
            var ground = _ctx.Ground;
            var stats = S.stats;
            if (node.interaction == NodeInteraction.Chop || node.interaction == NodeInteraction.Break || node.interaction == NodeInteraction.Quarry)
                node.hitAt = now;
            _ctx.Events.RaiseNodeHit(areaKey, node, isAuto);

            if (node.interaction == NodeInteraction.Quarry)
            {
                node.clicks++;
                if (isAuto) node.autoFlash = now + flashMs;
                if (node.clicks >= (node.clicksPerDrop > 0 ? node.clicksPerDrop : 5))
                {
                    node.clicks = 0;
                    int amt = node.dropMin != 0 ? Rng.Rand(node.dropMin, node.dropMax != 0 ? node.dropMax : node.dropMin) : 1;
                    if (Timing.BuffActive(S, "stoneheart_pill", now)) amt *= 2;
                    var c = _ctx.World.NodeCenterPx(node);
                    ground.DropGround(areaKey, string.IsNullOrEmpty(node.dropItem) ? "stone" : node.dropItem, amt, c.x, c.y, tag);
                    stats.totalGathered += amt;
                    if (node.rareDrop != null && Rng.Next01() < node.rareDrop.chance)
                    {
                        ground.DropGround(areaKey, node.rareDrop.item, 1, c.x, c.y, tag);
                        stats.totalGathered += 1;
                    }
                }
                return true;
            }

            var specs = NodeSpecs(areaKey, node);
            if (node.interaction == NodeInteraction.Chop)
            {
                node.pending ??= new ItemCounts();
                foreach (var spec in specs.perHit)
                {
                    int amt = Rng.RollAmount(spec);
                    if (amt > 0) node.pending.Add(spec.item, amt);
                }
                node.hitsLeft--;
                if (isAuto) node.autoFlash = now + flashMs;
                if (node.hitsLeft > 0) return true;
                FlushPending(areaKey, node, tag);
                GrantDropsGround(areaKey, node, specs.drops, 1, tag);
                DepleteNode(areaKey, node);
                return true;
            }
            if (node.interaction == NodeInteraction.Break)
            {
                node.hitsLeft--;
                if (isAuto) node.autoFlash = now + flashMs;
                if (node.hitsLeft > 0) return true;
                GrantDropsGround(areaKey, node, specs.drops, Timing.BuffActive(S, "stoneheart_pill", now) ? 2 : 1, tag);
                if (node.rareDrop != null && Rng.Next01() < node.rareDrop.chance)
                {
                    var c = _ctx.World.NodeCenterPx(node);
                    ground.DropGround(areaKey, node.rareDrop.item, 1, c.x, c.y, tag);
                    stats.totalGathered += 1;
                }
                DepleteNode(areaKey, node);
                return true;
            }
            // instant & surface
            GrantDropsGround(areaKey, node, specs.drops, 1, tag);
            if (isAuto) node.autoFlash = now + flashMs;
            DepleteNode(areaKey, node);
            return true;
        }

        /// <summary>`grantDropsGround` engine.js:830.</summary>
        public void GrantDropsGround(string areaKey, Node node, List<DropSpec> specs, int mult, GroundTag tag)
        {
            if (specs == null) return;
            var c = _ctx.World.NodeCenterPx(node);
            foreach (var spec in specs)
            {
                int amt = Rng.RollAmount(spec) * (mult != 0 ? mult : 1);
                if (amt > 0) { _ctx.Ground.DropGround(areaKey, spec.item, amt, c.x, c.y, tag); S.stats.totalGathered += amt; }
            }
        }

        /// <summary>`flushPending` engine.js:839.</summary>
        void FlushPending(string areaKey, Node node, GroundTag tag)
        {
            if (node.pending == null) return;
            var c = _ctx.World.NodeCenterPx(node);
            foreach (var e in node.pending)
                if (e.qty > 0) { _ctx.Ground.DropGround(areaKey, e.item, e.qty, c.x, c.y, tag); S.stats.totalGathered += e.qty; }
            node.pending = null;
        }
    }
}
