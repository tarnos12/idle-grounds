using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// Click-combat (engine-systems §7, engine.js:2620-2653, 2689-2774):
    /// per-slot fox respawn clocks + cap, zone wander, enemyAt hit test,
    /// attackEnemy (damage, Spirit Wave splash), damageEnemy (loot, respawn
    /// clock, foxKills). Beast Bait lure lives in HandSystem.DropFromHand (§5.5).
    /// </summary>
    public sealed class CombatSystem
    {
        /// <summary>enemyAt hit radius (engine.js:2690).</summary>
        public const double HitRadiusPx = 22;
        /// <summary>The JS wander step assumes this tick length (step = speed/20).</summary>
        public const double NominalTickMs = 50;

        readonly SimContext _ctx;
        public CombatSystem(SimContext ctx) { _ctx = ctx; }

        GameConfig Cfg => _ctx.Config;
        GameState S => _ctx.State;
        IRng Rng => _ctx.Rng;

        EnemyDef EnemyCfg(string areaKey)
        {
            var e = Cfg.Region(areaKey)?.enemies;
            return e != null && e.enabled ? e : null;
        }

        /// <summary>Walk rect in area px: x ∈ [(c0+1)·32, c1·32], y ∈ [(r0+1)·32, r1·32]. False if no enemies/zone.</summary>
        public bool WalkRect(string areaKey, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = y0 = x1 = y1 = 0;
            var e = EnemyCfg(areaKey);
            if (e == null) return false;
            var rects = Cfg.ZoneRects(e.zone);
            if (rects.Count == 0) return false;
            var z = rects[0];
            int cell = _ctx.Cell;
            x0 = (z.c0 + 1) * cell; x1 = z.c1 * cell;
            y0 = (z.r0 + 1) * cell; y1 = z.r1 * cell;
            return true;
        }

        /// <summary>gameTick step 8: spawn up to the cap (per-slot clocks), then wander. Returns the repaint hint (spawns only, as JS).</summary>
        public bool Tick(string areaKey, double now)
        {
            var ecfg = EnemyCfg(areaKey);
            if (ecfg == null) return false;
            if (!WalkRect(areaKey, out int x0, out int y0, out int x1, out int y1)) return false;
            var area = S.Area(areaKey);
            bool changed = false;

            int cap = ecfg.cap + area.upgrades.enemyCap;
            int live = 0;
            foreach (var e in area.enemies) if (e.kind != "boss") live++;
            int missing = Math.Max(0, cap - live);
            area.enemyRespawns ??= new List<double>();
            var rq = area.enemyRespawns;
            if (rq.Count > missing) { rq.Sort(); rq.RemoveRange(missing, rq.Count - missing); }
            while (rq.Count < missing) rq.Add(now);
            for (int i = rq.Count - 1; i >= 0; i--)
            {
                if (rq[i] > now) continue;
                rq.RemoveAt(i);
                var en = new Enemy
                {
                    id = area.nextEnemyId++, x = Rng.Rand(x0, x1), y = Rng.Rand(y0, y1),
                    hp = ecfg.hp, maxHp = ecfg.hp, tx = Rng.Rand(x0, x1), ty = Rng.Rand(y0, y1), hitAt = 0,
                };
                area.enemies.Add(en);
                _ctx.Events.RaiseEnemySpawned(areaKey, en);
                changed = true;
            }

            // Wander. JS moves speed/20 px per 50 ms tick; the port scales by the
            // real tick gap (identical at 50 ms, frame-rate independent otherwise).
            double dt = _ctx.Timing.TickGap;
            if (dt > 0)
            {
                double retarget = dt == NominalTickMs ? 0.01 : 1 - Math.Pow(0.99, dt / NominalTickMs);
                foreach (var en in area.enemies)
                {
                    double spd = en.spd > 0 ? en.spd : ecfg.speed;
                    double step = spd * dt / 1000.0;
                    double dx = en.tx - en.x, dy = en.ty - en.y, dd = Math.Sqrt(dx * dx + dy * dy);
                    if (dd < step || Rng.Next01() < retarget) { en.tx = Rng.Rand(x0, x1); en.ty = Rng.Rand(y0, y1); }
                    else { en.x += dx / dd * step; en.y += dy / dd * step; }
                }
            }
            return changed;
        }

        /// <summary>`enemyAt(area,x,y)` — first enemy within 22 px, or null.</summary>
        public Enemy EnemyAt(string areaKey, double x, double y)
        {
            var area = S.Area(areaKey);
            if (area == null) return null;
            foreach (var en in area.enemies)
            {
                double dx = en.x - x, dy = en.y - y;
                if (Math.Sqrt(dx * dx + dy * dy) <= HitRadiusPx) return en;
            }
            return null;
        }

        public Enemy EnemyById(string areaKey, int id)
        {
            var area = S.Area(areaKey);
            if (area == null) return null;
            foreach (var en in area.enemies) if (en.id == id) return en;
            return null;
        }

        /// <summary>Hold-attack cadence (`enemies.attackMs || 400`); 0 when the area has no enemies.</summary>
        public int AttackIntervalMs(string areaKey)
        {
            var e = EnemyCfg(areaKey);
            return e == null ? 0 : e.attackMs > 0 ? e.attackMs : 400;
        }

        /// <summary>Strike damage: 1 + Spirit Blade + Martial Vigor + Fury perk.</summary>
        public int Damage(string areaKey, double now)
        {
            var area = S.Area(areaKey);
            return 1 + (area?.upgrades.damage ?? 0)
                   + (Timing.CombatBuffActive(S, now) ? Cfg.vitality.bonusDamage : 0) + S.PerkLevel("fury");
        }

        /// <summary>Spirit Wave splash radius: aoe · 1.5 cells (px).</summary>
        public double AoeRadius(string areaKey) => (S.Area(areaKey)?.upgrades.aoe ?? 0) * 1.5 * _ctx.Cell;

        /// <summary>`attackEnemy(area,id)` engine.js:2761 — false when no such enemy.</summary>
        public bool Attack(string areaKey, int id)
        {
            var area = S.Area(areaKey);
            var target = EnemyById(areaKey, id);
            if (target == null) return false;
            double now = _ctx.Now;
            _ctx.Events.RaiseSound("hit", areaKey);
            int dmg = Damage(areaKey, now);
            double R = AoeRadius(areaKey);
            var hit = new List<Enemy>();
            if (R > 0)
            {
                foreach (var en in area.enemies)
                {
                    double dx = en.x - target.x, dy = en.y - target.y;
                    if (en == target || Math.Sqrt(dx * dx + dy * dy) <= R) hit.Add(en);
                }
            }
            else hit.Add(target);
            foreach (var en in hit) DamageEnemy(areaKey, en, dmg);
            return true;
        }

        /// <summary>A click too fast to count still flinches the enemy (ui.js:3007: hitAt = now).</summary>
        public void Flinch(string areaKey, int id)
        {
            var en = EnemyById(areaKey, id);
            if (en != null) en.hitAt = _ctx.Now;
        }

        /// <summary>`damageEnemy(area,en,dmg)` engine.js:2695.</summary>
        public void DamageEnemy(string areaKey, Enemy en, int dmg)
        {
            var area = S.Area(areaKey);
            double now = _ctx.Now;
            en.hp -= dmg; en.hitAt = now;
            if (en.hp > 0) { _ctx.Events.RaiseEnemyHit(areaKey, en); return; }
            area.enemies.Remove(en);
            _ctx.Events.RaiseSound("kill", areaKey);
            var ecfg = Cfg.Region(areaKey).enemies;
            bool boss = en.kind == "boss";
            var drops = boss && ecfg.baitSpawn.enabled ? ecfg.baitSpawn.drops : ecfg.drops;
            int loot = Timing.CombatBuffActive(S, now) ? Cfg.vitality.lootMult : 1;
            if (drops != null)
                foreach (var spec in drops)
                {
                    int amt = Rng.RollAmount(spec) * loot;
                    if (amt > 0) { _ctx.Ground.DropGround(areaKey, spec.item, amt, en.x, en.y, GroundTag.Manual); S.stats.totalGathered += amt; _ctx.Flow.Produce(spec.item, amt); }
                }
            if (!boss)
            {
                area.enemyRespawns ??= new List<double>();
                double respawn = ecfg.respawnMs > 0 ? ecfg.respawnMs : 5000;
                area.enemyRespawns.Add(now + respawn * _ctx.Timing.TimeScale * _ctx.Timing.PrestigeFactor(S));
                S.stats.foxKills++;
            }
            _ctx.Events.RaiseEnemyKilled(areaKey, en);
        }
    }
}
