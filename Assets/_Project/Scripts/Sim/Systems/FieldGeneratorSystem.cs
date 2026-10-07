using System;

namespace IdleGrounds.Sim
{
    /// <summary>Area field generators (engine-systems §6.6, engine.js:2352-2391).</summary>
    public sealed class FieldGeneratorSystem
    {
        readonly SimContext _ctx;
        public FieldGeneratorSystem(SimContext ctx) { _ctx = ctx; }

        /// <summary>Effective interval (ms) of generator <paramref name="gen"/> in an area.</summary>
        public double IntervalMs(AreaState area, FieldGeneratorDef gen)
        {
            var s = _ctx.State;
            var t = _ctx.Timing;
            int upLvl = string.IsNullOrEmpty(gen.upgrade) ? 0 : area.upgrades.Get(gen.upgrade);
            return gen.intervalMs * t.TimeScale * Math.Pow(0.8, upLvl) * Math.Pow(0.9, s.PerkLevel("bounty")) * t.PrestigeFactor(s);
        }

        /// <summary>gameTick step 3 for one (unlocked) area. Returns the repaint hint.</summary>
        public bool Tick(string areaKey, double now)
        {
            var cfg = _ctx.Config.Region(areaKey);
            var area = _ctx.State.Area(areaKey);
            var rng = _ctx.Rng;
            int CELL = _ctx.Cell;
            bool changed = false;
            for (int gi = 0; gi < cfg.generators.Count; gi++)
            {
                var gen = cfg.generators[gi];
                while (area.genTimers.Count <= gi) area.genTimers.Add(0);
                var tm = _ctx.Timing.Periodic(area.genTimers[gi], IntervalMs(area, gen), now);
                if (tm.n == 0) continue;
                area.genTimers[gi] = tm.next;
                var z = _ctx.Config.ZoneRects(gen.zone)[0];
                double fx0 = z.c0 * CELL - 16, fx1 = (z.c1 + 1) * CELL + 16;
                double fy0 = z.r0 * CELL - 16, fy1 = (z.r1 + 1) * CELL + 16;
                string rare = RareDrop.IsSet(gen.rareDrop) ? gen.rareDrop.item : null;
                int inField = 0, rareIn = 0;
                foreach (var g in area.ground)
                    if ((g.item == gen.item || (rare != null && g.item == rare)) &&
                        g.x >= fx0 && g.x <= fx1 && g.y >= fy0 && g.y <= fy1)
                    {
                        inField++;
                        if (g.item == rare) rareIn++;
                    }
                for (int ev = 0; ev < tm.n; ev++)
                {
                    if (inField >= gen.cap)
                    {
                        if (rare != null && rareIn < _ctx.Config.balance.genRareIdle && rng.Next01() < gen.rareDrop.chance)
                        {
                            Drop(areaKey, rare, z, GroundTag.Gen);
                            rareIn++; inField++; changed = true;
                        }
                        continue;
                    }
                    inField++;
                    Drop(areaKey, gen.item, z, GroundTag.None);
                    if (rare != null && rng.Next01() < gen.rareDrop.chance)
                    {
                        Drop(areaKey, rare, z, GroundTag.Gen);
                        inField++; rareIn++;
                    }
                    changed = true;
                }
            }
            return changed;
        }

        void Drop(string areaKey, string item, ZoneRect z, GroundTag tag)
        {
            var rng = _ctx.Rng;
            int CELL = _ctx.Cell;
            // JS evaluates x (col) before y (row)
            double x = (rng.Rand(z.c0, z.c1) + 0.5) * CELL;
            double y = (rng.Rand(z.r0, z.r1) + 0.5) * CELL;
            _ctx.Ground.DropGround(areaKey, item, 1, x, y, tag);
            _ctx.Flow.Produce(item, 1);
        }
    }
}
