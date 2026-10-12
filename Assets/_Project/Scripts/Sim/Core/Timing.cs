using System;

namespace IdleGrounds.Sim
{
    /// <summary>Result of <see cref="Timing.Periodic"/>: events due + the clock's next due time.</summary>
    public struct PeriodicResult
    {
        public int n;
        public double next;
        public PeriodicResult(int n, double next) { this.n = n; this.next = next; }
    }

    /// <summary>
    /// Tick-gap bookkeeping, due-time clocks and the world speed factors
    /// (engine-systems §0 TEST scaling, §2.2 periodic, §2.5 world factor).
    /// </summary>
    public sealed class Timing
    {
        readonly GameConfig _cfg;
        public double LastTickAt { get; private set; }
        /// <summary>now − previous tick, clamped to [0, maxTickGap]; 0 on the first tick or a backwards clock.</summary>
        public double TickGap { get; private set; }

        public Timing(GameConfig cfg) { _cfg = cfg; }

        /// <summary>Start of gameTick (engine.js:2329).</summary>
        public void BeginTick(double now)
        {
            TickGap = LastTickAt > 0 && now > LastTickAt ? Math.Min(now - LastTickAt, _cfg.balance.maxTickGap) : 0;
            LastTickAt = now;
        }

        /// <summary>Forget the previous tick (e.g. after a run reset).</summary>
        public void Reset() { LastTickAt = 0; TickGap = 0; }

        /// <summary>`periodic(due, interval, now)` engine.js:2314 — due-time re-arm with bounded catch-up.</summary>
        public PeriodicResult Periodic(double due, double interval, double now)
        {
            if (!(interval > 0)) return new PeriodicResult(0, due);
            if (double.IsNaN(due) || double.IsInfinity(due) || due < now - TickGap - interval) due = now;   // stale: restart now, no burst
            if (now < due) return new PeriodicResult(0, due);
            int n = (int)Math.Min(_cfg.balance.maxTickEvents, Math.Floor((now - due) / interval) + 1);
            return new PeriodicResult(n, Math.Max(due + n * interval, now - interval + 1));
        }

        /// <summary>TEST timeScale (1 when TEST disabled).</summary>
        public double TimeScale => _cfg.test.enabled ? _cfg.test.timeScale : 1;

        /// <summary>`scaled(n)` engine.js:128 = max(1, ceil(ENABLED ? n·costScale : n)).</summary>
        public int Scaled(double n) => (int)Math.Max(1, Math.Ceiling(_cfg.test.enabled ? n * _cfg.test.costScale : n));

        /// <summary>`buffScale()` engine.js:1889 — 1 in TEST, 4 otherwise (inverse of timeScale).</summary>
        public double BuffScale => _cfg.test.enabled ? 1 : 4;

        /// <summary>
        /// Every world timer's multiplier (was `prestigeFactor`, engine.js:274 §2.5). Ascension is gone (ADR 0007):
        /// the "haste" utility bonus (−2%/level) and the awakened dragon (−10%).
        /// </summary>
        public double WorldFactor(GameState s)
        {
            var b = _cfg.balance;
            return Math.Pow(b.hasteStep, s.Bonus("haste")) * (s.won ? b.awakenedSpeed : 1);
        }

        /// <summary>`buffActive(kind)` engine.js:131.</summary>
        public static bool BuffActive(GameState s, string kind, double now) => s.buff != null && s.buff.kind == kind && s.buff.until > now;

        /// <summary>`combatBuffActive()` engine.js:136.</summary>
        public static bool CombatBuffActive(GameState s, double now) => s.combatBuff != null && s.combatBuff.until > now;

        /// <summary>`harvestInterval(area,node)` engine.js:348 — not TEST-scaled.</summary>
        public double HarvestInterval(GameState s, AreaState area, Node node)
        {
            double b = node != null && node.swingMs > 0 ? node.swingMs : 350;
            return Math.Max(120, b * Math.Pow(0.8, area.upgrades.harvestSpeed) * WorldFactor(s));
        }
    }
}
