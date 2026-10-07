using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    public enum BuildingState { Working, Starved, NoFuel, Full, Idle }

    /// <summary>`buildingStatus` result (engine.js:1641): {state, item?, label, pile?}.</summary>
    public sealed class BuildingStatusInfo
    {
        public BuildingState state;
        public string item;     // may be null
        public string label;
        public bool pile;       // "Output pile full"

        public BuildingStatusInfo(BuildingState state, string item, string label, bool pile = false)
        { this.state = state; this.item = item; this.label = label; this.pile = pile; }

        /// <summary>Empty instance for the non-allocating <c>Status(area, b, into)</c> queries.</summary>
        public BuildingStatusInfo() { }

        /// <summary>Overwrite every field (reuse); returns this.</summary>
        public BuildingStatusInfo Set(BuildingState state, string item, string label, bool pile = false)
        { this.state = state; this.item = item; this.label = label; this.pile = pile; return this; }
        public BuildingStatusInfo SetPile(string item) => Set(BuildingState.Full, item, "Output pile full", true);
        public BuildingStatusInfo SetIdle(string item) => Set(BuildingState.Idle, item, "Idle");

        public static BuildingStatusInfo Pile(string item) => new BuildingStatusInfo(BuildingState.Full, item, "Output pile full", true);
        public static BuildingStatusInfo Idle(string item) => new BuildingStatusInfo(BuildingState.Idle, item, "Idle");
        public override string ToString() => state + (item != null ? "(" + item + ")" : "") + " " + label;
    }

    /// <summary>`inFlightTo(area,dst)` = cargo already flying to a building: {n, by}.</summary>
    public sealed class InFlight
    {
        public static readonly InFlight None = new InFlight();
        public int n;
        public readonly ItemCounts by = new ItemCounts();
    }

    /// <summary>
    /// Buildings (engine-systems §3.5, §5.5 steps 3-12, §8, §11.3 endpoints,
    /// §16 starter network): placement of ghosts, construction by feeding,
    /// demolition refunds, reveal/unlock queries, storehouse / seal /
    /// gathering-stone / stoker / pavilion buffers, generator buildings,
    /// Furnace Spirit stoking, the right-click building dispatcher.
    /// </summary>
    public sealed class BuildingSystem
    {
        readonly SimContext _ctx;
        public BuildingSystem(SimContext ctx) { _ctx = ctx; }

        GameConfig Cfg => _ctx.Config;
        GameState S => _ctx.State;
        HandSystem Hand => _ctx.Hand;
        FuelSystem Fuel => _ctx.Fuel;
        ConverterSystem Conv => _ctx.Converters;

        // ---- M5 hooks (dropFromHand steps 3, 4, 11) ----
        /// <summary>Step 3: built Altar. Default (null): no job / M5 not wired ⇒ handled, nothing happens.</summary>
        public HandSystem.BuildingFeedHook AltarFeed;
        /// <summary>Step 4: built Sleeping Dragon. Default (null): handled, nothing happens.</summary>
        public HandSystem.BuildingFeedHook DragonFeed;
        /// <summary>Step 11: built Ascension Gate. Default (null): falls through (ground drop unless noGround).</summary>
        public HandSystem.BuildingFeedHook GateFeed;

        // ================================================================
        // §8.3 unlock / reveal (pure queries for the UI)
        // ================================================================

        /// <summary>`isVeteran()` = ascended or the quest chain is finished.</summary>
        public bool IsVeteran() => S.ascensions > 0 || S.quest.idx >= Cfg.quests.Count;

        /// <summary>`questClaimed(id)`.</summary>
        public bool QuestClaimed(string id)
        {
            int j = Cfg.QuestIndex(id);
            return j >= 0 && S.quest.idx > j;
        }

        RevealRule RevealOf(string type)
        {
            foreach (var r in Cfg.reveal) if (r.building == type) return r;
            return null;
        }

        bool RevealCondMet(RevealCond c, bool vet)
        {
            switch (c.kind)
            {
                case RevealKind.Stage: return S.dragon.stage >= c.stage;
                case RevealKind.Quest: return vet || QuestClaimed(c.key);
                case RevealKind.Region: return vet || S.world.IsUnlocked(c.key);
                case RevealKind.Islands: return UnlockedIslandCount() >= c.count;   // no veteran bypass: pairing needs two Islands anyway
                default: return false;
            }
        }

        /// <summary>Unlocked Islands (Center included).</summary>
        public int UnlockedIslandCount()
        {
            int n = 0;
            foreach (var r in Cfg.regions) if (S.world.IsUnlocked(r.key)) n++;
            return n;
        }

        /// <summary>`isBuildingUnlocked(type)` engine.js:1124.</summary>
        public bool IsBuildingUnlocked(string type)
        {
            var def = Cfg.Building(type);
            if (def == null) return false;
            if (def.stageUnlock >= 0) return S.dragon.stage >= def.stageUnlock;
            var rule = RevealOf(type);
            if (rule == null || rule.any.Count == 0) return def.unlocked;
            if (S.builtTypes.Contains(type)) return true;
            bool vet = IsVeteran();
            foreach (var c in rule.any) if (RevealCondMet(c, vet)) return true;
            return false;
        }

        /// <summary>Why a type can't be built yet (null = available). Player-facing hint.</summary>
        public string UnlockReason(string type)
        {
            var def = Cfg.Building(type);
            if (def == null) return "Unknown building";
            if (IsBuildingUnlocked(type)) return null;
            if (def.stageUnlock >= 0) return "The Sleeping Dragon teaches this";
            var rule = RevealOf(type);
            if (rule == null || rule.any.Count == 0) return "Not buildable";
            var c = rule.any[0];
            switch (c.kind)
            {
                case RevealKind.Quest: return "Revealed by a quest";
                case RevealKind.Region: return "Open the " + (Cfg.Region(c.key)?.name ?? c.key);
                case RevealKind.Stage: return "The Sleeping Dragon teaches this";
                case RevealKind.Islands: return "Unlock a second island";
            }
            return "Locked";
        }

        /// <summary>Alias for <see cref="IsBuildingUnlocked"/> (UI naming).</summary>
        public bool IsBuildingAvailable(string type) => IsBuildingUnlocked(type);

        /// <summary>`isBuildingNew(type)` — "new" badge.</summary>
        public bool IsBuildingNew(string type) => S.ascensions <= 0 && IsBuildingUnlocked(type) && S.buildSeen.Get(type) < 2;

        /// <summary>`markBuildSeen(type)` (hover).</summary>
        public void MarkBuildSeen(string type) { if (Cfg.Building(type) != null) S.buildSeen.Set(type, 2); }

        /// <summary>`markBuildListed()` (menu opened).</summary>
        public void MarkBuildListed()
        {
            foreach (var b in Cfg.buildings)
                if (S.buildSeen.Get(b.key) == 0 && IsBuildingUnlocked(b.key)) S.buildSeen.Set(b.key, 1);
        }

        /// <summary>`buildMenuHasNew()`.</summary>
        public bool BuildMenuHasNew()
        {
            if (S.ascensions > 0) return false;
            foreach (var b in Cfg.buildings) if (S.buildSeen.Get(b.key) < 1 && IsBuildingUnlocked(b.key)) return true;
            return false;
        }

        /// <summary>`buildingCatalog()` — unlocked types in config order.</summary>
        public List<BuildingDef> Catalog()
        {
            var res = new List<BuildingDef>();
            foreach (var b in Cfg.buildings) if (IsBuildingUnlocked(b.key)) res.Add(b);
            return res;
        }

        // ================================================================
        // placement, construction, demolition (§3.5, §8.1, §8.2)
        // ================================================================

        /// <summary>`initLogistics(b)` engine.js:1259.</summary>
        public void InitLogistics(Building b)
        {
            var def = Cfg.Building(b.type);
            if (def == null) return;
            if ((def.gather.enabled || def.stoker.enabled || def.bridge.enabled) && b.inv == null) b.inv = new List<HandStack>();
            if (def.lantern.enabled) { b.links ??= new List<Link>(); b.nextSend = 0; }
            if (def.roster.enabled) b.nextCultivate = 0;
        }

        /// <summary>Why PlaceGhost would fail (null = allowed): "Locked" or the placeReason string.</summary>
        public string PlaceGhostReason(string areaKey, string type, int row, int col)
        {
            if (Cfg.Building(type) == null || S.Area(areaKey) == null) return "Unknown";
            if (!IsBuildingUnlocked(type)) return "Locked";
            return _ctx.World.PlaceReason(areaKey, row, col, type);
        }

        /// <summary>`placeBuilding(area,type,row,col)` engine.js:1295 — pushes an unpaid ghost; null if refused.</summary>
        public Building PlaceGhost(string areaKey, string type, int row, int col)
        {
            if (PlaceGhostReason(areaKey, type, row, col) != null) return null;
            var area = S.Area(areaKey);
            var b = new Building { id = area.nextBuildId++, type = type, row = row, col = col, built = false };
            InitLogistics(b);
            area.buildings.Add(b);
            _ctx.Events.RaiseBuildingPlaced(areaKey, b);
            return b;
        }

        /// <summary>`buildingNeeds(b)` = cost − paid (positive parts, cost order).</summary>
        public ItemCounts Needs(Building b)
        {
            var needs = new ItemCounts();
            NeedsInto(b, needs);
            return needs;
        }

        /// <summary>Non-allocating <see cref="Needs(Building)"/> into a caller-owned map (cleared with reuse).</summary>
        public void NeedsInto(Building b, ItemCounts into)
        {
            into.ClearReuse();
            var def = Cfg.Building(b.type);
            if (def == null) return;
            foreach (var c in def.cost) { int r = c.qty - b.paid.Get(c.item); if (r > 0) into.Set(c.item, r); }
        }

        /// <summary>Ghost completion (§5.5 step 12).</summary>
        void Complete(string areaKey, Building b)
        {
            b.built = true;
            _ctx.Occupancy.BumpEpoch();                 // a burner's fuel rack now occupies cells
            S.stats.buildingsBuilt++;
            if (!S.builtTypes.Contains(b.type)) S.builtTypes.Add(b.type);
            var def = Cfg.Building(b.type);
            if (def.roster.enabled && !S.pavilionSeeded)
            {
                S.pavilionSeeded = true;
                b.buns = def.roster.foodCap > 0 ? def.roster.foodCap : 20;
            }
            _ctx.Events.RaiseSound("build", areaKey);
            _ctx.Events.RaiseBuildingCompleted(areaKey, b);
            if (def.gate) { S.ascendPrompt = true; _ctx.Events.RaiseAscendPrompt(); }
        }

        /// <summary>
        /// `demolishBuilding(area,id)` engine.js:1814. Built: 100% cost + contents
        /// (store/seal, inv, converter stock + running batch inputs, disciples as
        /// robes, gate offerings; fuel NOT refunded). Ghost: only paid. All at
        /// the footprint centre tagged manual; lantern links to it are severed.
        /// </summary>
        public bool Demolish(string areaKey, int buildingId)
        {
            var area = S.Area(areaKey);
            if (area == null) return false;
            int i = area.buildings.FindIndex(x => x.id == buildingId);
            if (i < 0) return false;
            var b = area.buildings[i];
            var def = Cfg.Building(b.type);
            if (def == null || def.indestructible) return false;
            var (x, y) = _ctx.World.BuildingCenterPx(b);
            void Drop(string item, int qty) { if (qty > 0) _ctx.Ground.DropGround(areaKey, item, qty, x, y, GroundTag.Manual); }
            var flow = _ctx.Flow;
            // paid costs coming back un-consume them; a starter building's (never paid) cost is new items
            void Refund(string item, int qty)
            {
                if (qty <= 0) return;
                Drop(item, qty);
                if (b.starter) flow.Produce(item, qty); else flow.Unconsume(item, qty);
            }
            void Restore(string item, int qty) { if (qty <= 0) return; Drop(item, qty); flow.Unconsume(item, qty); }
            if (b.built)
            {
                foreach (var c in def.cost) Refund(c.item, c.qty);
                if ((b.type == "storehouse" || def.seal.enabled) && b.item != null && b.qty > 0) Drop(b.item, b.qty);
                if (b.inv != null) foreach (var st in b.inv) Drop(st.item, st.qty);
                if (def.IsConverter)
                {
                    if (b.stock != null) foreach (var e in b.stock) Drop(e.item, e.qty);
                    var rec = Conv.RecipeOf(b);
                    if (b.smeltDoneAt > 0 && rec != null) foreach (var e in rec.inputs) Restore(e.item, e.qty);
                }
                if (def.roster.enabled && b.disciples > 0) Restore(def.roster.recruit, b.disciples);
                if (def.gate && b.offered != null) foreach (var e in b.offered) Restore(e.item, e.qty);
                if (b.fuelQ != null) foreach (var f in b.fuelQ) flow.Lose(f.item, 1);   // fuel is not refunded
            }
            else
            {
                foreach (var e in b.paid) Restore(e.item, e.qty);
            }
            if (def.bridge.enabled) _ctx.Logistics.Unpair(areaKey, b.id);   // its sky wisps turn back / drop
            area.buildings.RemoveAt(i);
            foreach (var lb in area.buildings)
                if (lb.links != null) lb.links.RemoveAll(l => l.from == b.id || l.to == b.id);
            _ctx.Events.RaiseBuildingDemolished(areaKey, b);
            return true;
        }

        /// <summary>`buildingAt` by area-local px.</summary>
        public Building BuildingAtPx(string areaKey, double x, double y) =>
            _ctx.World.BuildingAt(areaKey, (int)Math.Floor(y / _ctx.Cell), (int)Math.Floor(x / _ctx.Cell));

        // ================================================================
        // §16 starter network
        // ================================================================

        /// <summary>`findSpot` engine.js:606 — Chebyshev ring scan radius 0..13, row-major per ring.</summary>
        public (int r, int c)? FindSpot(string areaKey, string type, int r0, int c0)
        {
            for (int rad = 0; rad < 14; rad++)
                for (int dr = -rad; dr <= rad; dr++)
                    for (int dc = -rad; dc <= rad; dc++)
                    {
                        if (Math.Max(Math.Abs(dr), Math.Abs(dc)) != rad) continue;
                        if (_ctx.World.CanPlaceBuilding(areaKey, r0 + dr, c0 + dc, type)) return (r0 + dr, c0 + dc);
                    }
            return null;
        }

        /// <summary>`placeBuilt` engine.js:615 — a BUILT game-placed building near an anchor (starter=true). Null if no spot.</summary>
        public Building PlaceBuilt(string areaKey, string type, int r0, int c0, string lockItem = null, bool starter = true)
        {
            var area = S.Area(areaKey);
            var spot = FindSpot(areaKey, type, r0, c0);
            if (spot == null) return null;
            var b = new Building { id = area.nextBuildId++, type = type, row = spot.Value.r, col = spot.Value.c, built = true, starter = starter };
            if (lockItem != null) { b.item = lockItem; b.locked = true; }
            InitLogistics(b);
            area.buildings.Add(b);
            _ctx.Occupancy.BumpEpoch();
            return b;
        }

        /// <summary>`setupStarterNetwork()` engine.js:627 — once per run, center only.</summary>
        public bool SetupStarterNetwork()
        {
            if (S.starterPlaced) return false;
            const string A = "center";
            Building P(string t, int r, int c, string lockItem = null) => PlaceBuilt(A, t, r, c, lockItem);
            var bench = P("workbench", 55, 36);
            var mill = P("paper_mill", 55, 42);
            var kiln = P("kiln", 55, 48);
            var array = P("infusion_array", 55, 54);
            var shJade = P("storehouse", 78, 27, "jade_shard");
            var shBamboo = P("storehouse", 26, 44, "bamboo");
            var shStone = P("storehouse", 83, 27, "stone");
            var gsStone = P("gathering_stone", 80, 13);
            var gsWood = P("gathering_stone", 16, 44);
            var gsClay = P("gathering_stone", 80, 80);
            var gsFox = P("gathering_stone", 12, 80);
            var sealStone = P("warding_seal", 74, 22, "stone");
            var sealWood = P("warding_seal", 30, 44, "wood");
            void L(Building lb, Building from, Building to)
            {
                if (lb != null && from != null && to != null) lb.links.Add(new Link { from = from.id, to = to.id });
            }
            var lanQ = P("wisp_lantern", 76, 20);
            L(lanQ, gsStone, sealStone); L(lanQ, sealStone, array); L(lanQ, gsStone, shJade);
            L(lanQ, gsStone, shStone);
            var lanT = P("wisp_lantern", 22, 44);
            L(lanT, gsWood, sealWood);
            L(lanT, sealWood, bench); L(lanT, sealWood, mill); L(lanT, sealWood, kiln);
            L(lanT, gsWood, shBamboo); L(lanT, shBamboo, mill);
            var lanM = P("wisp_lantern", 52, 58);
            L(lanM, gsClay, kiln); L(lanM, gsFox, array);
            var gsOut = P("gathering_stone", 61, 43);
            var gsSpirit = P("gathering_stone", 61, 55);
            var shPlank = P("storehouse", 63, 36, "plank");
            var shBrick = P("storehouse", 63, 45, "brick");
            var shSpirit = P("storehouse", 63, 57, "spirit_stone");
            var lanS = P("wisp_lantern", 62, 51);
            L(lanS, gsOut, shPlank); L(lanS, gsOut, shBrick); L(lanS, gsSpirit, shSpirit);
            int CELL = _ctx.Cell;
            void Seed(string item, int n, int r, int c) { _ctx.Ground.DropGround(A, item, n, (c + 0.5) * CELL, (r + 0.5) * CELL); _ctx.Flow.Produce(item, n); }
            Seed("stone", 8, 80, 14); Seed("jade_shard", 2, 79, 15);
            Seed("wood", 8, 15, 45); Seed("bamboo", 2, 16, 46);
            Seed("clay", 6, 80, 80); Seed("spirit_essence", 4, 12, 82);
            S.starterPlaced = true;
            return true;
        }

        // ================================================================
        // §11.3 endpoints (accept / give / take) + buffers (§8.5)
        // ================================================================

        public int StorehouseCap() => Hand.StorehouseCap();
        public int SealCap(BuildingDef def) => def.seal.cap > 0 ? def.seal.cap : 5;

        /// <summary>`gatherTotal(b)` = Σ inv qty.</summary>
        /// <summary>First stack holding <paramref name="item"/> (List.Find without a per-call closure; null list ⇒ null).</summary>
        static HandStack FindStack(List<HandStack> inv, string item)
        {
            if (inv == null) return null;
            for (int i = 0; i < inv.Count; i++) if (inv[i].item == item) return inv[i];
            return null;
        }

        public static int GatherTotal(Building b)
        {
            int t = 0;
            if (b.inv != null) foreach (var s in b.inv) t += s.qty;
            return t;
        }

        /// <summary>`foodValue(b,item)` — cultivation cycles one food item is worth (0 = not food).</summary>
        public int FoodValue(Building b, string item)
        {
            var r = Cfg.Building(b.type)?.roster;
            if (r == null || !r.enabled) return 0;
            if (r.foodValues != null && r.foodValues.Count > 0)
            {
                foreach (var f in r.foodValues) if (f.item == item) return f.qty;
                return 0;
            }
            return item == r.food ? 1 : 0;
        }

        /// <summary>`inFlightTo(area,dst)`.</summary>
        public InFlight InFlightTo(AreaState area, Building dst)
        {
            if (dst == null || area?.wisps == null || area.wisps.Count == 0) return InFlight.None;
            InFlight r = null;
            foreach (var w in area.wisps)
            {
                if (w.toId != dst.id) continue;
                r ??= new InFlight();
                r.n++; r.by.Add(w.item, 1);
            }
            return r ?? InFlight.None;
        }

        /// <summary>
        /// Non-allocating <see cref="InFlightTo(AreaState, Building)"/>: refills and returns <paramref name="into"/>
        /// (same counts, same item order), or <see cref="InFlight.None"/> when nothing is heading for dst.
        /// </summary>
        public InFlight InFlightTo(AreaState area, Building dst, InFlight into)
        {
            if (dst == null || area?.wisps == null || area.wisps.Count == 0) return InFlight.None;
            into.n = 0; into.by.ClearReuse();
            var ws = area.wisps;
            for (int i = 0; i < ws.Count; i++)
            {
                var w = ws[i];
                if (w.toId != dst.id) continue;
                into.n++; into.by.Add(w.item, 1);
            }
            return into.n > 0 ? into : InFlight.None;
        }

        /// <summary>`endpointAccepts(b,item,fly)` engine.js:1452.</summary>
        public bool EndpointAccepts(Building b, string item, InFlight fly = null)
        {
            if (b == null || !b.built) return false;
            var f = fly ?? InFlight.None;
            int fi = f.by.Get(item);
            var def = Cfg.Building(b.type);
            if (def == null) return false;
            if (def.seal.enabled) return b.item != null && b.item == item && b.qty + fi < SealCap(def);
            if (b.type == "storehouse")
            {
                string typ = b.item;
                if (typ == null) foreach (var e in f.by) { typ = e.item; break; }
                return (typ == null || typ == item) && b.qty + f.n < StorehouseCap();
            }
            if (def.gather.enabled)
                return GatherTotal(b) + f.n < def.gather.cap && (!b.hasAccEver || b.accEver == null || b.accEver.Contains(item));
            // Spirit Bridge: untyped buffer (sky reservations are checked when the sender launches)
            if (def.bridge.enabled) return GatherTotal(b) + f.n < def.bridge.cap;
            if (def.stoker.enabled) return Cfg.IsFuel(item) && GatherTotal(b) + f.n < def.stoker.cap;
            if (def.roster.enabled)
            {
                int due = 0;
                foreach (var e in f.by) due += FoodValue(b, e.item) * e.qty;
                int fv = FoodValue(b, item);
                return fv > 0 && def.roster.foodCap - b.buns - due >= fv;
            }
            if (def.IsConverter)
            {
                var rec = Conv.RecipeOf(b);
                bool isInput = ConverterSystem.IsInput(rec, item);
                // burners drink fuel into the rack UNLESS it's an ingredient of the current recipe with
                // stock room (§10.3); an ingredient fuel whose stock is full falls back to the rack
                if (def.fuel && Cfg.IsFuel(item))
                {
                    if (isInput && Conv.Stock(b).Get(item) + fi < Conv.StockCap(rec)) return true;
                    int fuelFly = 0;
                    foreach (var e in f.by) if (Cfg.IsFuel(e.item) && !ConverterSystem.IsInput(rec, e.item)) fuelFly += e.qty;
                    return Fuel.Space(b) - fuelFly > 0;
                }
                if (!isInput) return false;
                return Conv.Stock(b).Get(item) + fi < Conv.StockCap(rec);
            }
            return false;
        }

        /// <summary>`endpointGive(b,item)` engine.js:1697 — deliver one item; false = refused.</summary>
        public bool EndpointGive(Building b, string item)
        {
            if (!EndpointAccepts(b, item)) return false;
            var def = Cfg.Building(b.type);
            if (def.seal.enabled || b.type == "storehouse") { if (b.item == null) b.item = item; b.qty++; return true; }
            if (def.gather.enabled || def.stoker.enabled || def.bridge.enabled)
            {
                b.inv ??= new List<HandStack>();
                var st = FindStack(b.inv, item);
                if (st != null) st.qty++; else b.inv.Add(new HandStack(item, 1));
                return true;
            }
            if (def.roster.enabled) { b.buns = Math.Min(def.roster.foodCap, b.buns + FoodValue(b, item)); _ctx.Flow.Consume(item, 1); return true; }
            if (def.IsConverter)
            {
                // Fuel that is an ingredient of the current recipe (firestone on a Pill Furnace's
                // Ember recipe) goes to the input stock while it has room, else to the fuel rack —
                // the same rule as the hand path. (Deliberate fix of the JS quirk engine-systems
                // §11.3, where wisp-delivered fuel always went to the rack first.)
                if (def.fuel && Cfg.IsFuel(item))
                {
                    var rec = Conv.RecipeOf(b);
                    if (!(ConverterSystem.IsInput(rec, item) && Conv.Stock(b).Get(item) < Conv.StockCap(rec))) return Fuel.AddItem(b, item);
                }
                Conv.Stock(b).Add(item, 1);
                return true;
            }
            return false;
        }

        /// <summary>`endpointTake(b,item)` engine.js:1680.</summary>
        public bool EndpointTake(Building b, string item)
        {
            var def = Cfg.Building(b.type);
            if (def == null) return false;
            if (def.gather.enabled || def.stoker.enabled || def.bridge.enabled)
            {
                var st = FindStack(b.inv, item);
                if (st == null || st.qty <= 0) return false;
                st.qty--; if (st.qty <= 0) b.inv.Remove(st);
                return true;
            }
            if (def.seal.enabled || b.type == "storehouse")
            {
                if (b.item != item || b.qty <= 0) return false;
                b.qty--; if (b.qty <= 0 && !b.locked) b.item = null;
                return true;
            }
            return false;
        }

        /// <summary>`isLinkTarget(area,b)` — any lantern link points at it.</summary>
        public bool IsLinkTarget(string areaKey, Building b)
        {
            foreach (var lb in S.Area(areaKey).buildings)
                if (lb.links != null) foreach (var l in lb.links) if (l.to == b.id) return true;
            return false;
        }

        // ================================================================
        // §5.5 steps 3-12 — HandSystem.FeedBuilding implementation
        // ================================================================

        /// <summary>
        /// The building part of `dropFromHand` (engine.js:1930-2060). Returns
        /// true when the building handled the press (result may be null);
        /// false = fall through to the ground drop (step 13).
        /// </summary>
        public bool FeedBuilding(string areaKey, Building b, out DropResult result)
        {
            result = null;
            var def = Cfg.Building(b.type);
            if (def == null) return false;
            var H = S.hand;
            var first = H.Count > 0 ? H[0] : null;
            if (b.built)
            {
                // 3. Altar
                if (b.type == "center")
                {
                    if (AltarFeed != null) return AltarFeed(areaKey, b, out result) || true;
                    return true;   // no job (M5): nothing happens
                }
                // 4. Sleeping Dragon
                if (b.type == "dragon")
                {
                    if (DragonFeed != null) return DragonFeed(areaKey, b, out result) || true;
                    return true;
                }
                // 5. Furnace Spirit
                if (def.stoker.enabled)
                {
                    if (first != null && EndpointGive(b, first.item)) { Hand.Take(first.item, 1); result = DropResult.Of(DropResultKind.Fed, first.item); return true; }
                    foreach (var it in Cfg.FuelKeys)
                        if (Hand.Count(it) > 0 && EndpointAccepts(b, it)) { Hand.MoveToFront(it); result = DropResult.Of(DropResultKind.Reordered, it); return true; }
                    return true;
                }
                // 6. Meditation Pavilion
                if (def.roster.enabled)
                {
                    if (first != null && FoodValue(b, first.item) > 0 && EndpointGive(b, first.item))
                    { Hand.Take(first.item, 1); result = DropResult.Of(DropResultKind.Fed, first.item); return true; }
                    var foods = def.roster.foodValues != null && def.roster.foodValues.Count > 0
                        ? def.roster.foodValues : new List<ItemQty> { new ItemQty(def.roster.food, 1) };
                    foreach (var f in foods)
                        if (Hand.Count(f.item) > 0 && EndpointAccepts(b, f.item)) { Hand.MoveToFront(f.item); result = DropResult.Of(DropResultKind.Reordered, f.item); return true; }
                    return true;
                }
                // 7. Converter
                var rec = Conv.RecipeOf(b);
                if (rec != null)
                {
                    Conv.Stock(b);
                    bool firstIsInput = first != null && ConverterSystem.IsInput(rec, first.item);
                    if (def.fuel && first != null && Cfg.IsFuel(first.item) && !firstIsInput && Fuel.Space(b) > 0)
                    {
                        string it = first.item;
                        Hand.Take(it, 1);
                        Fuel.AddItem(b, it);
                        result = DropResult.Of(DropResultKind.Fed, it);
                        return true;
                    }
                    result = FeedRatio(b, rec);
                    if (result != null) return true;
                    if (def.fuel && Fuel.Space(b) > 0)
                        foreach (var it in Cfg.FuelKeys)
                            if (Hand.Count(it) > 0) { Hand.MoveToFront(it); result = DropResult.Of(DropResultKind.Reordered, it); return true; }
                    return true;
                }
                // 8. Warding Seal: tune to the front item (previous contents destroyed)
                if (def.seal.enabled)
                {
                    if (first == null) return true;
                    if (b.item != first.item) { if (b.item != null) _ctx.Flow.Lose(b.item, b.qty); b.item = first.item; b.qty = 0; b.locked = true; }
                    result = DropResult.Of(DropResultKind.Configured, first.item);
                    return true;
                }
                // 9. Gathering Stone
                if (def.gather.enabled)
                {
                    if (first == null || !EndpointGive(b, first.item)) return true;
                    Hand.Take(first.item, 1);
                    result = DropResult.Of(DropResultKind.Fed, first.item);
                    return true;
                }
                // 9b. Spirit Bridge: hand-feed a sending / unpaired bridge like a Gathering Stone
                if (def.bridge.enabled)
                {
                    if (first == null || _ctx.Logistics.BridgeReceiving(b) || !EndpointGive(b, first.item)) return true;
                    Hand.Take(first.item, 1);
                    result = DropResult.Of(DropResultKind.Fed, first.item);
                    return true;
                }
                // 10. Storehouse
                if (b.type == "storehouse") { result = Hand.DepositToStorehouse(b); return true; }
                // 11. Ascension Gate (M5 hook; may fall through)
                if (def.gate && GateFeed != null) return GateFeed(areaKey, b, out result);
                return false;
            }
            // 12. Ghost
            result = Hand.FeedNeeds(Needs(b), b.paid);
            if (result != null)
            {
                if (result.kind == DropResultKind.Fed) _ctx.Flow.Consume(result.item, 1);
                if (result.kind == DropResultKind.Fed && Needs(b).Count == 0) Complete(areaKey, b);
                result.buildingId = b.id;
            }
            return true;
        }

        /// <summary>`feedRatio(b, rec)` engine.js:1874 — feed the carried input with the lowest stock/need ratio.</summary>
        DropResult FeedRatio(Building b, RecipeDef rec)
        {
            var space = Conv.Space(b);
            var H = S.hand;
            var first = H.Count > 0 ? H[0] : null;
            var order = new List<string>();
            if (first != null && space.Get(first.item) > 0) order.Add(first.item);
            foreach (var it in space.Keys) if (order.Count == 0 || it != order[0]) order.Add(it);
            string pick = null; double best = double.PositiveInfinity;
            foreach (var it in order)
            {
                if (Hand.Count(it) <= 0) continue;
                int need = 0;
                foreach (var i in rec.inputs) if (i.item == it) need = i.qty;
                double r = (double)b.stock.Get(it) / Math.Max(1, need);
                if (r < best) { best = r; pick = it; }
            }
            if (pick != null) Hand.MoveToFront(pick);
            return Hand.FeedNeeds(space, b.stock);
        }

        // ================================================================
        // ticks
        // ================================================================

        /// <summary>gameTick step 4 — generator buildings (§8.6, engine.js:2395).</summary>
        public bool TickGenBuildings(string areaKey, double now)
        {
            var area = S.Area(areaKey);
            bool changed = false;
            int CELL = _ctx.Cell;
            foreach (var b in area.buildings)
            {
                var g = b.built ? Cfg.Building(b.type)?.gen : null;
                if (g == null || !g.enabled) continue;
                var tm = _ctx.Timing.Periodic(b.nextGen, g.intervalMs * _ctx.Timing.TimeScale * _ctx.Timing.PrestigeFactor(S), now);
                if (tm.n == 0) continue;
                b.nextGen = tm.next;
                var (bx, by) = _ctx.World.BuildingCenterPx(b);
                int R = 4 * CELL;
                int near = 0;
                foreach (var gi in area.ground)
                    if (gi.item == g.item && Math.Sqrt((gi.x - bx) * (gi.x - bx) + (gi.y - by) * (gi.y - by)) <= R) near++;
                if (near >= g.cap) { b.pileFull = true; b.pileAt = 0; continue; }
                if (Conv.OutputPileFull(area, b, g.item, now)) continue;
                for (int ev = 0; ev < tm.n && near < g.cap; ev++, near++)
                {
                    _ctx.Ground.DropGround(areaKey, g.item, 1, bx + _ctx.Rng.Rand(-R / 2, R / 2), by + _ctx.Rng.Rand(-R / 2, R / 2), GroundTag.Crafted);
                    _ctx.Flow.Produce(g.item, 1);
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>
        /// gameTick step 6 — the per-building logistics pass. Order per built
        /// building: Gathering Stone eject+vacuum → lantern beat →
        /// Furnace Spirit stoking (§10.4, here) → pavilion cultivation (§12.4).
        /// </summary>
        public bool TickLogistics(string areaKey, double now)
        {
            var area = S.Area(areaKey);
            bool changed = false;
            foreach (var b in area.buildings)
            {
                if (!b.built) continue;
                var def = Cfg.Building(b.type);
                if (def == null) continue;
                if (def.gather.enabled) changed |= _ctx.Logistics.TickStone(areaKey, area, b, def, now);     // §11.2
                if (def.lantern.enabled) changed |= _ctx.Logistics.TickLantern(areaKey, area, b, def, now);  // §11.5
                if (def.bridge.enabled) changed |= _ctx.Logistics.TickBridge(areaKey, area, b, def, now);    // ADR 0003
                if (def.stoker.enabled) changed |= Stoke(area, b, def);
                if (def.roster.enabled) changed |= _ctx.Pavilions.Tick(areaKey, area, b, def, now);           // §12.4
            }
            return changed;
        }

        /// <summary>Furnace Spirit (§10.4, engine.js:2561): one best fuel per burner in reach per tick.</summary>
        bool Stoke(AreaState area, Building b, BuildingDef def)
        {
            bool changed = false;
            var (cx, cy) = _ctx.World.BuildingCenterPx(b);
            double R = def.stoker.radius * _ctx.Cell;
            foreach (var t in area.buildings)
            {
                if (!t.built || Cfg.Building(t.type)?.fuel != true) continue;
                if (Fuel.Space(t) <= 0) continue;
                var (tx, ty) = _ctx.World.BuildingCenterPx(t);
                if (Math.Sqrt((tx - cx) * (tx - cx) + (ty - cy) * (ty - cy)) > R + 1.5 * _ctx.Cell) continue;
                HandStack st = null;
                if (b.inv != null)
                    foreach (var s in b.inv)
                        if (st == null || Cfg.FuelMs(s.item) > Cfg.FuelMs(st.item)) st = s;   // stable: first of the best
                if (st == null) break;
                st.qty--; if (st.qty <= 0) b.inv.Remove(st);
                Fuel.AddItem(t, st.item);
                changed = true;
            }
            return changed;
        }

        // ================================================================
        // §8.7 status
        // ================================================================

        /// <summary>`buildingStatus(area,b)` — null for ghosts/unknown/no status.</summary>
        public BuildingStatusInfo Status(string areaKey, Building b)
        {
            var st = new BuildingStatusInfo();
            return Status(areaKey, b, st) ? st : null;
        }

        /// <summary>
        /// Non-allocating `buildingStatus`: fills <paramref name="into"/> and returns true, or returns
        /// false where <see cref="Status(string, Building)"/> would return null (into left untouched).
        /// </summary>
        public bool Status(string areaKey, Building b, BuildingStatusInfo into)
        {
            if (b == null || !b.built) return false;
            var def = Cfg.Building(b.type);
            if (def == null) return false;
            if (def.IsConverter) return Conv.Status(areaKey, b, into);
            if (def.gather.enabled || def.stoker.enabled || def.bridge.enabled)
            {
                int cap = def.gather.enabled ? def.gather.cap : def.stoker.enabled ? def.stoker.cap : def.bridge.cap;
                if (GatherTotal(b) < cap) return false;
                into.Set(BuildingState.Full, null, "Full");
                return true;
            }
            if (def.seal.enabled)
            {
                if (b.qty < SealCap(def)) return false;
                into.Set(BuildingState.Full, b.item, "Full");
                return true;
            }
            if (b.type == "storehouse")
            {
                if (b.qty < StorehouseCap()) return false;
                into.Set(BuildingState.Full, b.item, "Full");
                return true;
            }
            if (def.roster.enabled)
            {
                if (b.disciples > 0 && !(b.buns > 0)) { into.Set(BuildingState.Starved, "spirit_buns", NeedsLabel("spirit_buns")); return true; }
                if (b.disciples > 0 && b.pileFull) { into.SetPile(def.roster.produce); return true; }
                return false;
            }
            if (def.gen.enabled && b.pileFull) { into.SetPile(def.gen.item); return true; }
            return false;
        }

        readonly Dictionary<string, string> _needsLabels = new Dictionary<string, string>();

        /// <summary>"Needs &lt;item name&gt;" status label, cached per item (no per-call string concat).</summary>
        public string NeedsLabel(string item)
        {
            string key = item ?? "";
            if (!_needsLabels.TryGetValue(key, out var s))
                _needsLabels[key] = s = "Needs " + (Cfg.Item(item)?.name ?? item);
            return s;
        }
    }
}
