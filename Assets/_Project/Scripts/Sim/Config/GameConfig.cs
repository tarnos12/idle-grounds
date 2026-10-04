using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// The whole static config (= old-game DATA). Serializable lists are the
    /// source of truth; <see cref="Build"/> creates the runtime lookups.
    /// Region order = DATA.AREAS key order (center, farm, mine, fishing,
    /// volcano, grove, celestial) — the order every system iterates areas in.
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        public GridDef grid = new GridDef();
        public TestScaling test = new TestScaling();
        public GameBalance balance = new GameBalance();
        public List<ItemDef> items = new List<ItemDef>();
        public List<ZoneDef> zones = new List<ZoneDef>();
        public List<RegionDef> regions = new List<RegionDef>();
        public List<BuildingDef> buildings = new List<BuildingDef>();
        public List<DragonStageDef> dragonStages = new List<DragonStageDef>();
        public List<DragonBuffDef> dragonBuffs = new List<DragonBuffDef>();
        public VitalityDef vitality = new VitalityDef();
        public List<UpgradeNodeDef> upgradeTree = new List<UpgradeNodeDef>();
        public List<QuestDef> quests = new List<QuestDef>();
        public List<RevealRule> reveal = new List<RevealRule>();
        public List<PerkDef> perks = new List<PerkDef>();
        public List<VowDef> vows = new List<VowDef>();
        public GateOffering gateOfferings = new GateOffering();

        // ---- runtime lookups (not serialized) ----
        [NonSerialized] Dictionary<string, ItemDef> _items;
        [NonSerialized] Dictionary<string, ZoneDef> _zones;
        [NonSerialized] Dictionary<string, RegionDef> _regions;
        [NonSerialized] Dictionary<string, int> _regionIndex;
        [NonSerialized] Dictionary<string, BuildingDef> _buildings;
        [NonSerialized] Dictionary<string, DragonBuffDef> _buffs;
        [NonSerialized] Dictionary<string, PerkDef> _perks;
        [NonSerialized] List<string> _fuelKeys;
        [NonSerialized] bool _built;

        static readonly List<ZoneRect> NoRects = new List<ZoneRect>();

        public bool IsBuilt => _built;

        /// <summary>Create the lookup dictionaries. Idempotent; returns this.</summary>
        public GameConfig Build()
        {
            _items = new Dictionary<string, ItemDef>();
            foreach (var it in items) if (it != null && it.key != null) _items[it.key] = it;
            _zones = new Dictionary<string, ZoneDef>();
            foreach (var z in zones) if (z != null && z.key != null) _zones[z.key] = z;
            _regions = new Dictionary<string, RegionDef>();
            _regionIndex = new Dictionary<string, int>();
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i]?.key == null) continue;
                _regions[regions[i].key] = regions[i];
                _regionIndex[regions[i].key] = i;
            }
            _buildings = new Dictionary<string, BuildingDef>();
            foreach (var b in buildings) if (b != null && b.key != null) _buildings[b.key] = b;
            _buffs = new Dictionary<string, DragonBuffDef>();
            foreach (var b in dragonBuffs) if (b != null && b.item != null) _buffs[b.item] = b;
            _perks = new Dictionary<string, PerkDef>();
            foreach (var p in perks) if (p != null && p.id != null) _perks[p.id] = p;
            _fuelKeys = new List<string>();
            foreach (var it in items) if (it != null && it.fuelMs > 0) _fuelKeys.Add(it.key);
            _built = true;
            return this;
        }

        void Ensure() { if (!_built) Build(); }

        public ItemDef Item(string key) { Ensure(); return key != null && _items.TryGetValue(key, out var v) ? v : null; }
        public bool IsItem(string key) { Ensure(); return key != null && _items.ContainsKey(key); }
        public ZoneDef Zone(string key) { Ensure(); return key != null && _zones.TryGetValue(key, out var v) ? v : null; }
        /// <summary>`zoneRects(key)` (engine.js:362): unknown ⇒ empty.</summary>
        public List<ZoneRect> ZoneRects(string key) => Zone(key)?.rects ?? NoRects;
        public RegionDef Region(string key) { Ensure(); return key != null && _regions.TryGetValue(key, out var v) ? v : null; }
        public int RegionIndex(string key) { Ensure(); return key != null && _regionIndex.TryGetValue(key, out var v) ? v : -1; }
        public BuildingDef Building(string key) { Ensure(); return key != null && _buildings.TryGetValue(key, out var v) ? v : null; }
        public DragonBuffDef DragonBuff(string item) { Ensure(); return item != null && _buffs.TryGetValue(item, out var v) ? v : null; }
        public PerkDef Perk(string id) { Ensure(); return id != null && _perks.TryGetValue(id, out var v) ? v : null; }
        /// <summary>FUEL key order (= item order of fuel items).</summary>
        public IReadOnlyList<string> FuelKeys { get { Ensure(); return _fuelKeys; } }
        public int FuelMs(string item) => Item(item)?.fuelMs ?? 0;
        public bool IsFuel(string item) => FuelMs(item) > 0;
        public int QuestIndex(string id)
        {
            for (int i = 0; i < quests.Count; i++) if (quests[i].id == id) return i;
            return -1;
        }

        /// <summary>Footprint {w,h} of a building type (engine.js:374).</summary>
        public (int w, int h) BuildingSize(string type)
        {
            var b = Building(type);
            return b != null ? (b.sizeW, b.sizeH) : (grid.buildingW, grid.buildingH);
        }

        /// <summary>Cross-reference validation. Returns human-readable errors (empty = OK).</summary>
        public List<string> Validate()
        {
            Build();
            var errs = new List<string>();
            void CheckItem(string ctx, string key)
            {
                if (string.IsNullOrEmpty(key)) errs.Add(ctx + ": empty item key");
                else if (!IsItem(key)) errs.Add(ctx + ": unknown item '" + key + "'");
            }
            void Items(string ctx, List<ItemQty> list)
            {
                if (list == null) return;
                foreach (var q in list) { CheckItem(ctx, q.item); if (q.qty <= 0) errs.Add(ctx + ": non-positive qty for " + q.item); }
            }
            void Specs(string ctx, List<DropSpec> list)
            {
                if (list == null) return;
                foreach (var s in list) { CheckItem(ctx, s.item); if (s.max < s.min) errs.Add(ctx + ": max < min for " + s.item); }
            }
            void Rare(string ctx, RareDrop r)
            {
                if (!RareDrop.IsSet(r)) return;
                CheckItem(ctx + ".rareDrop", r.item);
                if (r.chance < 0 || r.chance > 1) errs.Add(ctx + ": rare chance out of range");
            }
            void ZoneRef(string ctx, string z)
            {
                if (Zone(z) == null || Zone(z).rects.Count == 0) errs.Add(ctx + ": unknown zone '" + z + "'");
            }

            if (items.Count == 0) errs.Add("no items");
            var seen = new HashSet<string>();
            foreach (var it in items) if (!seen.Add(it.key)) errs.Add("duplicate item " + it.key);
            if (regions.Count == 0) errs.Add("no regions");
            int N = grid.cells;
            foreach (var z in zones)
                foreach (var r in z.rects)
                    if (r.r0 < 0 || r.c0 < 0 || r.r1 >= N || r.c1 >= N || r.r1 < r.r0 || r.c1 < r.c0)
                        errs.Add("zone " + z.key + " rect out of grid " + r);

            foreach (var reg in regions)
            {
                string rc = "region " + reg.key;
                foreach (var nb in reg.noBuild) ZoneRef(rc + ".noBuild", nb);
                Items(rc + ".unlockCost", reg.unlockCost);
                for (int i = 0; i < reg.tiers.Count; i++) { Specs(rc + ".tier" + i, reg.tiers[i].perHit); Specs(rc + ".tier" + i, reg.tiers[i].drops); }
                foreach (var sp in reg.spawners)
                {
                    string c = rc + ".spawner " + sp.kind;
                    ZoneRef(c, sp.zone);
                    if (sp.sizes.Count == 0) errs.Add(c + ": no sizes");
                    if (sp.useTiers && reg.tiers.Count == 0) errs.Add(c + ": useTiers without tiers");
                    if (sp.interaction == NodeInteraction.None || sp.interaction == NodeInteraction.Quarry) errs.Add(c + ": bad interaction " + sp.interaction);
                    Specs(c, sp.perHit); Specs(c, sp.drops); Rare(c, sp.rareDrop);
                }
                foreach (var fx in reg.fixtures)
                {
                    string c = rc + ".fixture " + fx.kind;
                    ZoneRef(c, fx.zone);
                    if (fx.interaction != NodeInteraction.None) CheckItem(c + ".drop", string.IsNullOrEmpty(fx.drop) ? "stone" : fx.drop);
                    Rare(c, fx.rareDrop);
                }
                foreach (var g in reg.generators)
                {
                    string c = rc + ".generator " + g.kind;
                    ZoneRef(c, g.zone); CheckItem(c, g.item); Rare(c, g.rareDrop);
                    if (g.intervalMs <= 0) errs.Add(c + ": intervalMs <= 0");
                }
                if (reg.enemies.enabled)
                {
                    ZoneRef(rc + ".enemies", reg.enemies.zone);
                    Specs(rc + ".enemies", reg.enemies.drops);
                    if (reg.enemies.baitSpawn.enabled) Specs(rc + ".baitSpawn", reg.enemies.baitSpawn.drops);
                }
            }
            foreach (var b in buildings)
            {
                string c = "building " + b.key;
                Items(c + ".cost", b.cost);
                if (b.sizeW <= 0 || b.sizeH <= 0) errs.Add(c + ": bad size");
                foreach (var r in b.recipes)
                {
                    Items(c + ".recipe " + r.name, r.inputs);
                    CheckItem(c + ".recipe " + r.name + ".output", r.output);
                    if (r.timeMs <= 0) errs.Add(c + ".recipe " + r.name + ": timeMs <= 0");
                }
                if (b.roster.enabled)
                {
                    CheckItem(c + ".roster.recruit", b.roster.recruit);
                    CheckItem(c + ".roster.produce", b.roster.produce);
                    foreach (var fv in b.roster.foodValues) CheckItem(c + ".roster.food", fv.item);
                }
                if (b.gen.enabled) CheckItem(c + ".gen", b.gen.item);
            }
            for (int i = 0; i < dragonStages.Count; i++) Items("dragonStage" + i, dragonStages[i].needs);
            foreach (var bf in dragonBuffs) CheckItem("dragonBuff", bf.item);
            CheckItem("vitality", vitality.item);
            var nodeIds = new HashSet<string>();
            foreach (var n in upgradeTree) nodeIds.Add(n.id);
            foreach (var n in upgradeTree)
            {
                if (Region(n.area) == null) errs.Add("upgrade " + n.id + ": unknown area " + n.area);
                foreach (var l in n.links) if (!nodeIds.Contains(l)) errs.Add("upgrade " + n.id + ": unknown link " + l);
                foreach (var lv in n.costs) Items("upgrade " + n.id, lv.items);
            }
            foreach (var q in quests)
            {
                string c = "quest " + q.id;
                if (q.goalKind == QuestGoalKind.Unknown) errs.Add(c + ": untranslated goal");
                if (q.goalKind == QuestGoalKind.HandCount || q.goalKind == QuestGoalKind.DragonTributeItem) CheckItem(c + ".goal", q.goalItem);
                foreach (var gi in q.goalItems) CheckItem(c + ".goal", gi.item);
                foreach (var r in q.goalRegions) if (Region(r) == null) errs.Add(c + ": unknown region " + r);
                Items(c + ".reward", q.rewardItems);
                foreach (var t in q.rewardReveal) if (Building(t) == null) errs.Add(c + ": unknown reveal " + t);
                foreach (var t in q.builds) if (Building(t) == null) errs.Add(c + ": unknown build " + t);
            }
            foreach (var rr in reveal)
            {
                if (Building(rr.building) == null) errs.Add("reveal: unknown building " + rr.building);
                foreach (var cnd in rr.any)
                {
                    if (cnd.kind == RevealKind.Quest && QuestIndex(cnd.key) < 0) errs.Add("reveal " + rr.building + ": unknown quest " + cnd.key);
                    if (cnd.kind == RevealKind.Region && Region(cnd.key) == null) errs.Add("reveal " + rr.building + ": unknown region " + cnd.key);
                }
            }
            foreach (var p in perks) if (p.cost.Count != p.max) errs.Add("perk " + p.id + ": cost count != max");
            foreach (var it in gateOfferings.items) CheckItem("gateOfferings", it);
            foreach (var it in items) if (string.IsNullOrEmpty(it.sourceHint)) errs.Add("item " + it.key + ": no source hint");
            return errs;
        }
    }
}
