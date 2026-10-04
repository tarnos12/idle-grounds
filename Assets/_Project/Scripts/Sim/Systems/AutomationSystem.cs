using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleGrounds.Sim
{
    /// <summary>Per-area automation read-out for the view (§13.1).</summary>
    public sealed class AutomationStatus
    {
        public int level;
        /// <summary>Node-click budget per tick (AUTOMATION_CLICKS[level] + autoboost perk); 0 when off.</summary>
        public int budget;
        /// <summary>Any yield type saturated (≥ AUTO_SKIP_LOOSE loose raw) — the UI marker.</summary>
        public bool paused;
        /// <summary>The saturated types whose nodes were skipped last tick.</summary>
        public List<string> skipped = new List<string>();
    }

    /// <summary>
    /// `automationTick()` engine.js:2786 (engine-systems §13.1): per unlocked
    /// area, one bot swing per regrowing node up to the click budget, skipping
    /// nodes whose yield types are saturated on the ground; autoTap fixtures
    /// (the Spirit Tree) get `level` extra swings outside the budget.
    /// </summary>
    public sealed class AutomationSystem
    {
        readonly SimContext _ctx;
        public AutomationSystem(SimContext ctx) { _ctx = ctx; }

        GameConfig Cfg => _ctx.Config;
        GameState S => _ctx.State;

        /// <summary>`nodeYieldTypes(area,node)` — perHit ∪ drops item types (no rare finds), first-seen order.</summary>
        public List<string> NodeYieldTypes(string areaKey, Node node)
        {
            var sp = _ctx.Nodes.NodeSpecs(areaKey, node);
            var outL = new List<string>();
            foreach (var s in sp.perHit) if (!outL.Contains(s.item)) outL.Add(s.item);
            foreach (var s in sp.drops) if (!outL.Contains(s.item)) outL.Add(s.item);
            return outL;
        }

        bool IsAutoTapFixture(string areaKey, Node n)
        {
            var reg = Cfg.Region(areaKey);
            if (reg == null) return false;
            foreach (var f in reg.fixtures) if (f.kind == n.kind) return f.autoTap;
            return false;
        }

        /// <summary>Runs one automation tick over every unlocked area; returns total bot clicks.</summary>
        public int Tick()
        {
            int harvested = 0;
            int skipLoose = Cfg.balance.autoSkipLoose;
            foreach (var reg in Cfg.regions)
            {
                string areaKey = reg.key;
                if (!_ctx.World.IsAreaUnlocked(areaKey)) continue;
                var area = S.Area(areaKey);
                if (area == null) continue;
                int level = area.upgrades.automation;
                if (level <= 0) { area.autoSkip = new List<string>(); area.autoPaused = false; continue; }

                var loose = new Dictionary<string, int>();
                foreach (var g in area.ground)
                    if (_ctx.Ground.EvictClassOf(g) < 2)
                        loose[g.item] = (loose.TryGetValue(g.item, out int c) ? c : 0) + 1;
                int Loose(string t) => t != null && loose.TryGetValue(t, out int v) ? v : 0;

                var skip = new List<string>();
                int budget = Cfg.balance.AutomationClicks(level) + S.PerkLevel("autoboost");
                // stable sort by tier desc (LINQ OrderBy is stable, like Array.prototype.sort)
                var nodes = area.nodes.Where(n => !n.deco && !n.isFixed).OrderByDescending(n => n.tier).ToList();
                var taps = area.nodes.Where(n => n.isFixed && !n.deco && IsAutoTapFixture(areaKey, n)).ToList();
                int clicks = 0;
                _ctx.AutoHarvesting = true;     // bot drops: no player grace window, tagged gen
                try
                {
                    foreach (var node in nodes)
                    {
                        bool full = false;
                        foreach (var t in NodeYieldTypes(areaKey, node))
                            if (Loose(t) >= skipLoose) { full = true; if (!skip.Contains(t)) skip.Add(t); }
                        if (full) continue;
                        if (clicks >= budget) continue;   // keep scanning: autoSkip lists every saturated type
                        _ctx.Nodes.Harvest(areaKey, node.id, true, false);
                        clicks++;
                    }
                    foreach (var node in taps)
                    {
                        string t = string.IsNullOrEmpty(node.dropItem) ? "stone" : node.dropItem;
                        if (Loose(t) >= skipLoose) { if (!skip.Contains(t)) skip.Add(t); continue; }
                        for (int i = 0; i < level; i++) { _ctx.Nodes.Harvest(areaKey, node.id, true, false); clicks++; }
                    }
                }
                finally { _ctx.AutoHarvesting = false; }
                area.autoSkip = skip;
                area.autoPaused = skip.Count > 0;
                harvested += clicks;
            }
            return harvested;
        }

        /// <summary>Automation read-out for an area (null for unknown areas).</summary>
        public AutomationStatus Status(string areaKey)
        {
            var area = S.Area(areaKey);
            if (area == null) return null;
            int level = area.upgrades.automation;
            return new AutomationStatus
            {
                level = level,
                budget = level <= 0 ? 0 : Cfg.balance.AutomationClicks(level) + S.PerkLevel("autoboost"),
                paused = area.autoPaused,
                skipped = new List<string>(area.autoSkip ?? new List<string>()),
            };
        }

        /// <summary>The node's AUTO badge is lit (autoFlash in the future).</summary>
        public bool AutoFlashing(Node n, double now) => n != null && n.autoFlash > now;
    }
}
