using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// Burner fuel rack (engine-systems §10.1-10.2, engine.js:1315-1340):
    /// `b.fuelQ` = FIFO of discrete fuel items, newest at index 0, burned from
    /// the END (oldest first), max <c>balance.fuelSlots</c> (6).
    /// </summary>
    public sealed class FuelSystem
    {
        readonly SimContext _ctx;
        public FuelSystem(SimContext ctx) { _ctx = ctx; }

        GameConfig Cfg => _ctx.Config;

        public int Slots => Cfg.balance.fuelSlots;

        /// <summary>`fuelQueue(b)` — lazily created.</summary>
        public List<FuelSlot> Queue(Building b) => b.fuelQ ??= new List<FuelSlot>();

        /// <summary>`fuelTotal(b)` = Σ rem.</summary>
        public double Total(Building b)
        {
            double t = 0;
            foreach (var f in Queue(b)) t += f.rem;
            return t;
        }

        /// <summary>`fuelSpace(b)` = slots − length.</summary>
        public int Space(Building b) => Slots - Queue(b).Count;

        /// <summary>`addFuelItem(b,item)` — unshift a full unit at the front; false if not fuel / rack full.</summary>
        public bool AddItem(Building b, string item)
        {
            int ms = Cfg.FuelMs(item);
            if (ms <= 0 || Space(b) <= 0) return false;
            Queue(b).Insert(0, new FuelSlot { item = item, rem = ms, total = ms });
            return true;
        }

        /// <summary>`burnFuel(b,ms)` — burn from the back; Vow of the Cold Hearth doubles it; pop when rem ≤ 0.5.</summary>
        public void Burn(Building b, double ms)
        {
            if (_ctx.State.VowActive("coldhearth")) ms *= 2;
            var q = Queue(b);
            while (ms > 0 && q.Count > 0)
            {
                var back = q[q.Count - 1];
                double take = Math.Min(ms, back.rem);
                back.rem -= take; ms -= take;
                if (back.rem <= 0.5) q.RemoveAt(q.Count - 1);
            }
        }
    }
}
