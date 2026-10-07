using System;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// Disciples / Meditation Pavilion (engine-systems §12.4, engine.js:1266-1293,
    /// 2576-2592): roster cap, recruiting with robes, cultivation cycles via
    /// periodic (catch-up). Food intake lives in BuildingSystem (hand) and
    /// endpointGive (wisps).
    /// </summary>
    public sealed class PavilionSystem
    {
        readonly SimContext _ctx;
        public PavilionSystem(SimContext ctx) { _ctx = ctx; }

        GameConfig Cfg => _ctx.Config;
        GameState S => _ctx.State;

        /// <summary>`rosterCap(b)` = cap + 2·center.discipleCap + perk(hall).</summary>
        public int RosterCap(Building b)
        {
            var r = Cfg.Building(b?.type)?.roster;
            if (r == null || !r.enabled) return 0;
            return r.cap + 2 * (S.Area("center")?.upgrades.discipleCap ?? 0) + S.PerkLevel("hall");
        }

        /// <summary>Why Recruit would fail (null = can recruit): "Not built" / "Full" / "Needs Robe".</summary>
        public string RecruitReason(string areaKey, int buildingId)
        {
            var b = S.Area(areaKey)?.BuildingById(buildingId);
            var r = b != null ? Cfg.Building(b.type)?.roster : null;
            if (r == null || !r.enabled) return "Not a pavilion";
            if (!b.built) return "Not built";
            if (b.disciples >= RosterCap(b)) return "Full";
            if (_ctx.Hand.Count(r.recruit) <= 0) return "Needs " + (Cfg.Item(r.recruit)?.name ?? r.recruit);
            return null;
        }

        /// <summary>`recruitDisciple(area,id)` — spend one robe from the hand.</summary>
        public bool Recruit(string areaKey, int buildingId)
        {
            if (RecruitReason(areaKey, buildingId) != null) return false;
            var b = S.Area(areaKey).BuildingById(buildingId);
            var r = Cfg.Building(b.type).roster;
            _ctx.Hand.Take(r.recruit, 1);
            _ctx.Flow.Consume(r.recruit, 1);
            b.disciples++;
            S.stats.disciplesRecruited++;
            _ctx.Events.RaiseDiscipleRecruited(areaKey, b);
            return true;
        }

        /// <summary>Cultivation cycle length (ms) right now.</summary>
        public double CycleMs(BuildingDef def) =>
            def.roster.produceMs * _ctx.Timing.TimeScale * _ctx.Timing.PrestigeFactor(S);

        /// <summary>gameTick step 6, pavilion part (engine.js:2578) — one built roster building.</summary>
        public bool Tick(string areaKey, AreaState area, Building b, BuildingDef def, double now)
        {
            if (!def.roster.enabled || b.disciples <= 0) return false;
            var tm = _ctx.Timing.Periodic(b.nextCultivate, CycleMs(def), now);
            if (tm.n > 0) b.nextCultivate = tm.next;
            bool changed = false;
            for (int ev = 0; ev < tm.n; ev++)
            {
                int worked = Math.Min(b.disciples, b.buns);
                if (worked <= 0) break;
                if (_ctx.Converters.OutputPileFull(area, b, def.roster.produce, now)) break;
                b.buns -= worked;
                var (cx, _) = _ctx.World.BuildingCenterPx(b);
                int h = Cfg.BuildingSize(b.type).h;
                _ctx.Ground.DropGround(areaKey, def.roster.produce, worked,
                    cx + _ctx.Rng.Rand(-40, 40), (b.row + h) * _ctx.Cell + 12, GroundTag.Crafted);
                _ctx.Flow.Produce(def.roster.produce, worked);
                changed = true;
            }
            return changed;
        }
    }
}
