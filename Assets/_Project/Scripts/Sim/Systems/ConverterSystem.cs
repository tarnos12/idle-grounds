using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>One input row of a converter face: stock vs. per-batch need vs. cap.</summary>
    public struct ConverterInputView
    {
        public string item;
        public int have;
        public int need;
        public int cap;
    }

    /// <summary>Everything the converter panel/face draws (ui.js drawConverterFace), as one pure query.</summary>
    public sealed class ConverterFace
    {
        public int recipeIndex;
        public RecipeDef recipe;
        public List<ConverterInputView> inputs = new List<ConverterInputView>();
        /// <summary>`craftsPossible(b)` — batches the current stock covers (fuel ignored).</summary>
        public int craftable;
        public bool running;
        /// <summary>Batch progress 0..1 (0 when idle).</summary>
        public double progress;
        /// <summary>Real batch duration right now (ms).</summary>
        public double batchMs;
        public bool isBurner;
        /// <summary>Copy of the rack, OLDEST first (index 0 = the one alight).</summary>
        public List<FuelSlot> fuel = new List<FuelSlot>();
        public int fuelSlots;
        public double fuelTotal;
        public BuildingStatusInfo status;
        /// <summary>`craftRate(b, true)` — crafts per minute.</summary>
        public double craftsPerMin;
    }

    /// <summary>
    /// Converter buildings (engine-systems §9, engine.js:1305-1415, 2414-2468):
    /// recipe + input stock, recipe switching, the batch loop with inline
    /// due-time catch-up, output back-pressure, craft-rate ring.
    /// </summary>
    public sealed class ConverterSystem
    {
        readonly SimContext _ctx;
        public ConverterSystem(SimContext ctx) { _ctx = ctx; }

        GameConfig Cfg => _ctx.Config;
        GameState S => _ctx.State;
        FuelSystem Fuel => _ctx.Fuel;

        // ---- §9.1 recipe & stock ----

        /// <summary>`recipeOf(b)` = recipes[b.recipe||0] (null for non-converters / bad index).</summary>
        public RecipeDef RecipeOf(Building b)
        {
            var list = Cfg.Building(b?.type)?.recipes;
            if (list == null || b.recipe < 0 || b.recipe >= list.Count) return null;
            return list[b.recipe];
        }

        public bool IsBurner(Building b) => Cfg.Building(b?.type)?.fuel == true;

        public int StockCap(RecipeDef rec) => rec.stockCap > 0 ? rec.stockCap : Cfg.balance.defaultStockCap;

        public ItemCounts Stock(Building b) => b.stock ??= new ItemCounts();

        static int InputQty(RecipeDef rec, string item)
        {
            foreach (var i in rec.inputs) if (i.item == item) return i.qty;
            return -1;
        }

        public static bool IsInput(RecipeDef rec, string item) => rec != null && InputQty(rec, item) >= 0;

        /// <summary>`smeltRemaining(b)` — what the NEXT batch still needs beyond stock.</summary>
        public ItemCounts Remaining(Building b)
        {
            var rem = new ItemCounts();
            var rec = RecipeOf(b);
            if (rec == null) return rem;
            var st = Stock(b);
            foreach (var i in rec.inputs) { int r = i.qty - st.Get(i.item); if (r > 0) rem.Set(i.item, r); }
            return rem;
        }

        /// <summary>`smeltSpace(b)` — free stock space per input (cap − stock, &gt;0).</summary>
        public ItemCounts Space(Building b)
        {
            var sp = new ItemCounts();
            var rec = RecipeOf(b);
            if (rec == null) return sp;
            var st = Stock(b);
            int cap = StockCap(rec);
            foreach (var i in rec.inputs) { int s = cap - st.Get(i.item); if (s > 0) sp.Set(i.item, s); }
            return sp;
        }

        /// <summary>`canStartBatch(b)` — every input stock ≥ qty.</summary>
        public bool CanStartBatch(Building b)
        {
            var rec = RecipeOf(b);
            if (rec == null) return false;
            var st = Stock(b);
            foreach (var i in rec.inputs) if (st.Get(i.item) < i.qty) return false;
            return true;
        }

        /// <summary>`craftsPossible(b)` — min floor(stock/qty); fuel ignored.</summary>
        public int CraftsPossible(Building b)
        {
            var rec = RecipeOf(b);
            if (rec == null || rec.inputs.Count == 0) return 0;
            var st = Stock(b);
            int n = int.MaxValue;
            foreach (var i in rec.inputs) n = Math.Min(n, st.Get(i.item) / Math.Max(1, i.qty));
            return n;
        }

        /// <summary>Real batch duration: timeMs · timeScale · prestigeFactor · (burner &amp; Ember blessing ? 0.5).</summary>
        public double BatchMs(Building b, double now)
        {
            var rec = RecipeOf(b);
            if (rec == null) return 0;
            double cost = rec.timeMs * _ctx.Timing.TimeScale * _ctx.Timing.PrestigeFactor(S);
            if (IsBurner(b) && Timing.BuffActive(S, "ember_pill", now)) cost *= 0.5;
            return cost;
        }

        // ---- §9.2 recipe switch ----

        /// <summary>
        /// `setRecipe(area,id,idx)` engine.js:1345. The batch in progress is
        /// cancelled and its inputs refunded with the stock; inputs the new
        /// recipe also uses stay (may exceed the new cap), the rest go to the
        /// hand, overflow drops at the footprint centre (manual). Fuel untouched.
        /// </summary>
        public bool SetRecipe(string areaKey, int buildingId, int idx)
        {
            var b = S.Area(areaKey)?.BuildingById(buildingId);
            var list = Cfg.Building(b?.type)?.recipes;
            if (list == null || list.Count == 0 || idx < 0 || idx >= list.Count) return false;
            if (b.recipe == idx) return true;
            var (x, y) = _ctx.World.BuildingCenterPx(b);
            var held = Stock(b).Clone();
            if (b.smeltDoneAt > 0)
            {
                var old = RecipeOf(b);
                if (old != null) foreach (var i in old.inputs) held.Add(i.item, i.qty);
            }
            var keep = list[idx];
            b.stock = new ItemCounts();
            foreach (var e in held)
            {
                if (!(e.qty > 0)) continue;
                if (IsInput(keep, e.item)) { b.stock.Set(e.item, e.qty); continue; }
                int over = e.qty - _ctx.Hand.Add(e.item, e.qty);
                if (over > 0) _ctx.Ground.DropGround(areaKey, e.item, over, x, y, GroundTag.Manual);
            }
            b.smeltDoneAt = 0;
            b.recipe = idx;
            S.stats.recipeSwitches++;
            return true;
        }

        // ---- §9.5 output back-pressure ----

        /// <summary>`outputPileCount` — items of `item` within the footprint expanded by 3 cells (inclusive).</summary>
        public int OutputPileCount(AreaState area, Building b, string item)
        {
            var (w, h) = Cfg.BuildingSize(b.type);
            int CELL = _ctx.Cell, m = Cfg.balance.outputPileCells * CELL;
            double x0 = b.col * CELL - m, x1 = (b.col + w) * CELL + m;
            double y0 = b.row * CELL - m, y1 = (b.row + h) * CELL + m;
            int n = 0;
            foreach (var g in area.ground)
                if (g.item == item && g.x >= x0 && g.x <= x1 && g.y >= y0 && g.y <= y1) n++;
            return n;
        }

        /// <summary>`outputPileFull` — cached for PILE_RECHECK_MS per building/item.</summary>
        public bool OutputPileFull(AreaState area, Building b, string item, double now)
        {
            if (b.pileAt != 0 && now < b.pileAt && b.pileItem == item) return b.pileFull;
            b.pileAt = now + Cfg.balance.pileRecheckMs; b.pileItem = item;
            return b.pileFull = OutputPileCount(area, b, item) >= Cfg.balance.outputPileMax;
        }

        // ---- craft rate (transient) ----

        /// <summary>`noteCraft(b, now)`.</summary>
        public void NoteCraft(Building b, double now)
        {
            var r = b.crafts ??= new List<double>();
            int win = Cfg.balance.craftWindowMs;
            while (r.Count > 0 && now - r[0] > win) r.RemoveAt(0);
            if (r.Count == 0) b.craftFirst = now;
            r.Add(now);
            if (r.Count > 240) r.RemoveRange(0, r.Count - 240);
        }

        /// <summary>`craftRate(b, forDisplay)` — crafts per minute.</summary>
        public double CraftRate(Building b, bool forDisplay)
        {
            var r = b?.crafts;
            if (r == null || r.Count == 0) return 0;
            double now = _ctx.Now;
            int win = Cfg.balance.craftWindowMs, n = 0;
            for (int i = r.Count - 1; i >= 0 && now - r[i] <= win; i--) n++;
            if (n < 2) return forDisplay ? 0 : n;
            double first = b.craftFirst != 0 ? b.craftFirst : r[0];
            double span = Math.Min(Math.Max(now - first, 1000), win);
            return span >= win ? n : (n - 1) * 60000.0 / span;
        }

        // ---- §9.3 batch loop (gameTick step 5) ----

        /// <summary>gameTick step 5 for one area: finish/start batches with catch-up, burner fuel burn.</summary>
        public bool Tick(string areaKey, double now)
        {
            var area = S.Area(areaKey);
            bool changed = false;
            double tickGap = _ctx.Timing.TickGap;
            int maxEv = Cfg.balance.maxTickEvents;
            foreach (var b in area.buildings)
            {
                var rec = b.built ? RecipeOf(b) : null;
                if (rec == null) continue;
                bool isBurner = IsBurner(b);
                double emberK = isBurner ? Math.Pow(0.85, S.PerkLevel("ember")) : 1;
                var stock = Stock(b);
                for (int ev = 0; ev < maxEv; ev++)
                {
                    double doneAt = 0;
                    if (b.smeltDoneAt != 0 && now >= b.smeltDoneAt)
                    {
                        // burners charge the batch's unburned tail (fuelBurnAt..due time)
                        if (isBurner) Fuel.Burn(b, Math.Max(0, b.smeltDoneAt - (b.fuelBurnAt != 0 ? b.fuelBurnAt : b.smeltDoneAt)) * emberK);
                        doneAt = b.smeltDoneAt;
                        var (w, h) = Cfg.BuildingSize(b.type);
                        double bx = (b.col + w / 2.0) * _ctx.Cell, by = (b.row + h) * _ctx.Cell + 12;
                        int qty = rec.outputQty > 0 ? rec.outputQty : 1;
                        _ctx.Ground.DropGround(areaKey, rec.output, qty, bx, by, GroundTag.Crafted);
                        S.stats.totalCrafted += qty;
                        NoteCraft(b, now);
                        _ctx.Events.RaiseSound("craft", areaKey);
                        _ctx.Events.RaiseBatchFinished(areaKey, b, rec.output, qty);
                        b.smeltDoneAt = 0;
                        changed = true;
                    }
                    // a finished batch always lands, but no NEW batch starts on a full output pile
                    if (b.smeltDoneAt != 0 || !CanStartBatch(b) || OutputPileFull(area, b, rec.output, now)) break;
                    double cost = BatchMs(b, now);
                    // burners start on ANY fuel left (the remainder of the batch is free)
                    if (isBurner && !(Fuel.Total(b) > 0)) break;
                    foreach (var i in rec.inputs) stock.Add(i.item, -i.qty);
                    // catch-up: re-arm from the finished batch's due time when it fell within this tick
                    double t0 = doneAt != 0 && doneAt >= now - tickGap ? doneAt : now;
                    b.smeltDoneAt = t0 + cost;
                    b.fuelBurnAt = t0;
                    changed = true;
                    _ctx.Events.RaiseBatchStarted(areaKey, b, rec.output, rec.outputQty > 0 ? rec.outputQty : 1);
                    if (b.smeltDoneAt > now) break;
                }
                if (isBurner)
                {
                    if (b.smeltDoneAt != 0 && now < b.smeltDoneAt)
                        Fuel.Burn(b, Math.Max(0, now - (b.fuelBurnAt != 0 ? b.fuelBurnAt : now)) * emberK);
                    b.fuelBurnAt = now;   // idle time never burns a backlog
                }
            }
            return changed;
        }

        // ---- §8.7 status + face ----

        /// <summary>The converter part of `buildingStatus` (engine.js:1641).</summary>
        public BuildingStatusInfo Status(string areaKey, Building b)
        {
            var rec = RecipeOf(b);
            if (rec == null) return BuildingStatusInfo.Idle(null);
            var stock = Stock(b);
            int cap = StockCap(rec);
            bool atCap = true;
            foreach (var i in rec.inputs) if (stock.Get(i.item) < cap) { atCap = false; break; }
            if (b.smeltDoneAt > 0)
                return atCap ? new BuildingStatusInfo(BuildingState.Full, null, "Stock full")
                             : new BuildingStatusInfo(BuildingState.Working, null, "Working");
            string missing = null;
            foreach (var i in rec.inputs) if (stock.Get(i.item) < i.qty) { missing = i.item; break; }
            if (missing == null)
            {
                if (b.pileFull && b.pileItem == rec.output) return BuildingStatusInfo.Pile(rec.output);
                if (IsBurner(b) && !(Fuel.Total(b) > 0)) return new BuildingStatusInfo(BuildingState.NoFuel, null, "No fuel");
                return new BuildingStatusInfo(BuildingState.Working, null, "Working");
            }
            bool any = false;
            foreach (var i in rec.inputs) if (stock.Get(i.item) > 0) { any = true; break; }
            if (!any && !_ctx.Buildings.IsLinkTarget(areaKey, b)) return BuildingStatusInfo.Idle(missing);
            return new BuildingStatusInfo(BuildingState.Starved, missing, "Needs " + (Cfg.Item(missing)?.name ?? missing));
        }

        /// <summary>Everything the converter face / panel needs; null for non-converters.</summary>
        public ConverterFace Face(string areaKey, Building b)
        {
            var rec = RecipeOf(b);
            if (rec == null) return null;
            double now = _ctx.Now;
            var stock = Stock(b);
            int cap = StockCap(rec);
            var f = new ConverterFace
            {
                recipeIndex = b.recipe,
                recipe = rec,
                craftable = CraftsPossible(b),
                running = b.smeltDoneAt > 0,
                batchMs = BatchMs(b, now),
                isBurner = IsBurner(b),
                fuelSlots = Fuel.Slots,
                status = b.built ? Status(areaKey, b) : null,
                craftsPerMin = CraftRate(b, true),
            };
            foreach (var i in rec.inputs)
                f.inputs.Add(new ConverterInputView { item = i.item, have = stock.Get(i.item), need = i.qty, cap = cap });
            f.progress = b.smeltDoneAt > now && f.batchMs > 0 ? Math.Max(0, Math.Min(1, 1 - (b.smeltDoneAt - now) / f.batchMs)) : 0;
            if (f.isBurner)
            {
                var q = Fuel.Queue(b);
                for (int i = q.Count - 1; i >= 0; i--) f.fuel.Add(new FuelSlot { item = q[i].item, rem = q[i].rem, total = q[i].total });
                f.fuelTotal = Fuel.Total(b);
            }
            return f;
        }
    }
}
