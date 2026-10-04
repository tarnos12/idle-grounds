using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>Kinds of <see cref="DropResult"/> (JS dropFromHand return shapes, §5.5).</summary>
    public enum DropResultKind { Used, Fed, Reordered, Configured, Deposited, Dropped }

    /// <summary>Result DTO of dropFromHand / feed rules. null = nothing happened.</summary>
    public sealed class DropResult
    {
        public DropResultKind kind;
        public string item;
        public bool once;
        public bool lured;
        public int buildingId;      // 0 = none

        public static DropResult Of(DropResultKind k, string item) => new DropResult { kind = k, item = item };
    }

    /// <summary>
    /// The hand (engine-systems §5.1-5.5, engine.js:16-127, 1855-2068): ordered
    /// unique stacks capped by handCap; withdraw/deposit helpers; feedNeeds;
    /// the right-click dispatcher (ground-drop path; building feeding = M2 hook).
    /// </summary>
    public sealed class HandSystem
    {
        readonly SimContext _ctx;
        public HandSystem(SimContext ctx) { _ctx = ctx; }

        List<HandStack> H => _ctx.State.hand;
        GameConfig Cfg => _ctx.Config;

        /// <summary>
        /// M2 hook for dropFromHand steps 3-12 (Altar, Dragon, stoker, roster,
        /// converter, seal, gathering stone, storehouse, gate, ghost). Return
        /// true when the building handled the press (result may be null =
        /// "consumed, nothing to do"); false falls through to the ground drop.
        /// TODO(M2): implement in a BuildingFeedSystem and assign here.
        /// </summary>
        public delegate bool BuildingFeedHook(string areaKey, Building b, out DropResult result);
        public BuildingFeedHook FeedBuilding;

        // ---- §5.1 model ----
        public int Total() { int t = 0; foreach (var s in H) t += s.qty; return t; }

        /// <summary>`handCap()` — Vow of Burden halves it.</summary>
        public int Cap() => _ctx.State.VowActive("burden") ? Math.Max(1, _ctx.State.handCap / 2) : _ctx.State.handCap;
        public int Space() => Cap() - Total();

        public int IndexOf(string item) { for (int i = 0; i < H.Count; i++) if (H[i].item == item) return i; return -1; }
        public int Count(string item) { int i = IndexOf(item); return i >= 0 ? H[i].qty : 0; }
        public string Front => H.Count > 0 ? H[0].item : null;

        /// <summary>`handAdd` — merge or append at the BACK; returns added.</summary>
        public int Add(string item, int qty)
        {
            int add = Math.Min(qty, Space());
            if (add <= 0) return 0;
            int i = IndexOf(item);
            if (i >= 0) H[i].qty += add; else H.Add(new HandStack(item, add));
            return add;
        }

        /// <summary>`handTakeFirst` — −1 from the front stack.</summary>
        public string TakeFirst()
        {
            if (H.Count == 0) return null;
            var s = H[0];
            s.qty--;
            if (s.qty <= 0) H.RemoveAt(0);
            return s.item;
        }

        /// <summary>`handTake(item,n)` — returns removed amount.</summary>
        public int Take(string item, int n)
        {
            int i = IndexOf(item);
            if (i < 0) return 0;
            var s = H[i];
            int take = Math.Min(n, s.qty);
            s.qty -= take;
            if (s.qty <= 0) H.RemoveAt(i);
            return take;
        }

        public bool MoveToFront(string item)
        {
            int i = IndexOf(item);
            if (i < 0) return false;
            if (i > 0) { var s = H[i]; H.RemoveAt(i); H.Insert(0, s); }
            return true;
        }

        /// <summary>`handRotate(dir)`: +1 front→back (Q), −1 back→front (E). Returns new front item.</summary>
        public string Rotate(int dir)
        {
            if (H.Count == 0) return null;
            if (H.Count > 1)
            {
                if (dir < 0) { var last = H[H.Count - 1]; H.RemoveAt(H.Count - 1); H.Insert(0, last); }
                else { var first = H[0]; H.RemoveAt(0); H.Add(first); }
            }
            return H[0].item;
        }

        public bool CanAfford(IEnumerable<ItemQty> cost)
        {
            foreach (var c in cost) if (Count(c.item) < c.qty) return false;
            return true;
        }

        public bool Spend(List<ItemQty> cost)
        {
            if (!CanAfford(cost)) return false;
            foreach (var c in cost) Take(c.item, c.qty);
            return true;
        }

        // ---- §5.2 / §5.3 storehouse & withdraw ----
        public int StorehouseCap() { var d = Cfg.Building("storehouse"); return d != null && d.cap > 0 ? d.cap : int.MaxValue; }

        /// <summary>`depositToStorehouse(sh)` engine.js:91.</summary>
        public DropResult DepositToStorehouse(Building sh)
        {
            if (sh.item == null) { if (H.Count == 0) return null; sh.item = H[0].item; sh.qty = 0; }
            if (sh.qty >= StorehouseCap() || Count(sh.item) <= 0) return null;
            if (H[0].item != sh.item) { MoveToFront(sh.item); return DropResult.Of(DropResultKind.Reordered, sh.item); }
            Take(sh.item, 1); sh.qty++;
            return DropResult.Of(DropResultKind.Deposited, sh.item);
        }

        /// <summary>`takeFromStorehouse(sh,n)` engine.js:101.</summary>
        public int TakeFromStorehouse(Building sh, int n)
        {
            if (sh.item == null) return 0;
            int take = Math.Min(Math.Max(1, n), Math.Min(Space(), sh.qty));
            if (take <= 0) return 0;
            string item = sh.item;
            Add(item, take); sh.qty -= take;
            if (sh.qty <= 0 && !sh.locked) sh.item = null;
            return take;
        }

        /// <summary>`withdrawFromBuilding(b,n)` engine.js:114.</summary>
        public int WithdrawFromBuilding(Building b, int n)
        {
            var cfg = Cfg.Building(b.type);
            if (cfg == null) return 0;
            if (b.type == "storehouse" || cfg.seal.enabled) return TakeFromStorehouse(b, n);
            if (cfg.gather.enabled || cfg.stoker.enabled)
            {
                int took = 0, want = Math.Max(1, n);
                while (took < want && Space() > 0 && b.inv != null && b.inv.Count > 0)
                {
                    var st = b.inv[0];
                    if (Add(st.item, 1) > 0) { st.qty--; took++; if (st.qty <= 0) b.inv.RemoveAt(0); }
                    else break;
                }
                return took;
            }
            return 0;
        }

        // ---- §5.4 feeding ----

        /// <summary>`feedNeeds(rem, paid)` engine.js:1860.</summary>
        public DropResult FeedNeeds(ItemCounts rem, ItemCounts paid)
        {
            var first = H.Count > 0 ? H[0] : null;
            if (first != null && rem.Get(first.item) > 0)
            {
                Take(first.item, 1);
                paid.Add(first.item, 1);
                return DropResult.Of(DropResultKind.Fed, first.item);
            }
            foreach (var e in rem)
                if (e.qty > 0 && Count(e.item) > 0) { MoveToFront(e.item); return DropResult.Of(DropResultKind.Reordered, e.item); }
            return null;
        }

        // ---- §5.5 dropFromHand ----

        /// <summary>
        /// `dropFromHand(area,x,y,noGround)` engine.js:1893. M1 implements
        /// step 1 (Vitality Pill), step 2 (Beast Bait lure), the building hook
        /// for steps 3-12 and step 13 (ground drop).
        /// </summary>
        public DropResult DropFromHand(string areaKey, double x, double y, bool noGround)
        {
            int cell = _ctx.Cell;
            int col = (int)Math.Floor(x / cell), row = (int)Math.Floor(y / cell);
            double now = _ctx.Now;
            var s = _ctx.State;
            // 1. Vitality Pill — quaffed anywhere
            if (H.Count > 0 && H[0].item == Cfg.vitality.item)
            {
                Take(Cfg.vitality.item, 1);
                s.combatBuff = new CombatBuffState { until = now + Cfg.vitality.ms * _ctx.Timing.BuffScale };
                return new DropResult { kind = DropResultKind.Used, item = Cfg.vitality.item, once = true };
            }
            // 2. Beast Bait inside the enemy zone lures a boss
            var reg = Cfg.Region(areaKey);
            var ecfg = reg?.enemies;
            if (H.Count > 0 && H[0].item == "beast_bait" && ecfg != null && ecfg.enabled && ecfg.baitSpawn.enabled)
            {
                var rects = Cfg.ZoneRects(ecfg.zone);
                if (rects.Count > 0 && rects[0].Contains(row, col))
                {
                    Take("beast_bait", 1);
                    var bs = ecfg.baitSpawn;
                    var area = s.Area(areaKey);
                    area.enemies.Add(new Enemy
                    {
                        id = area.nextEnemyId++, x = x, y = y, hp = bs.hp, maxHp = bs.hp, tx = x, ty = y,
                        hitAt = 0, kind = "boss", sprite = bs.sprite, spd = bs.speed,
                    });
                    return new DropResult { kind = DropResultKind.Fed, item = "beast_bait", lured = true, once = true };
                }
            }
            // 3-12. buildings (M2)
            var b = _ctx.World.BuildingAt(areaKey, row, col);
            if (b != null && FeedBuilding != null && FeedBuilding(areaKey, b, out var fed)) return fed;
            // 13. ground drop
            if (noGround) return null;
            string item = TakeFirst();
            if (item == null) return null;
            _ctx.Ground.DropGround(areaKey, item, 1, x, y, GroundTag.Manual);
            return DropResult.Of(DropResultKind.Dropped, item);
        }
    }
}
