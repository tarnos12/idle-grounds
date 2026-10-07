using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>One item's counters: lifetime totals and the current-run section.</summary>
    [Serializable]
    public class FlowEntry
    {
        public string item;
        public long produced, consumed, lost;
        public long runProduced, runConsumed, runLost;
        /// <summary>Rolling 1-minute buckets [kind * Buckets + slot] (kind 0 produced, 1 consumed, 2 lost). Runtime only.</summary>
        [NonSerialized] internal int[] bk;

        public long Net => produced - consumed - lost;
        public long RunNet => runProduced - runConsumed - runLost;
    }

    /// <summary>
    /// Per-item flow ledger (the Stats "Flow" tab): how many of each item the world
    /// produced, consumed and lost. Counted at the single points where an item
    /// enters or leaves play — moving items between hand / ground / buffers / wisps is a
    /// transfer and never counts:
    ///   produced = harvest drops, field + building generators, converter / pavilion output,
    ///              dragon scales, combat loot, quest rewards, the starter seeds, and the free
    ///              refund of a starter building's (never paid) cost;
    ///   consumed = converter batch inputs, fuel burned, building costs, dragon tributes, Altar
    ///              upgrade jobs, region-unlock installments, gate offerings, disciples' food and
    ///              robes, pills / bait used (refunds un-consume: demolish, recipe switch, job cancel);
    ///   lost     = ground eviction over the cap, a Warding Seal retuned over its contents, the
    ///              fuel rack of a demolished burner.
    ///
    /// Persistence: saved with the GameState (reflection codec; <see cref="Sanitize"/> on load).
    /// Ascension: the LIFETIME totals survive (like <c>stats</c>), the per-run section
    /// (<c>run*</c>) is zeroed by <see cref="ResetRun"/>. The rolling per-minute rates (last 5 one-minute
    /// buckets) are runtime-only: they restart empty on load and carry across an ascension.
    /// Steady-state allocation-free (entries and bucket arrays are created the first time an item is seen).
    /// </summary>
    [Serializable]
    public class FlowLedger
    {
        public const int Buckets = 5;
        const double MinuteMs = 60000.0;

        public List<FlowEntry> items = new List<FlowEntry>();

        [NonSerialized] Dictionary<string, int> _idx;
        [NonSerialized] bool _started;
        [NonSerialized] long _minute;
        [NonSerialized] int _slot;
        [NonSerialized] double _startMs, _nowMs;

        // ---- counting ----

        public void Produce(string item, int qty) { if (qty > 0 && item != null) Bump(item, 0, qty); }
        public void Consume(string item, int qty) { if (qty > 0 && item != null) Bump(item, 1, qty); }
        public void Lose(string item, int qty) { if (qty > 0 && item != null) Bump(item, 2, qty); }

        /// <summary>A consumption taken back (refund of a payment / cancelled batch): consumed -= qty, floored at 0.</summary>
        public void Unconsume(string item, int qty)
        {
            if (qty <= 0 || item == null) return;
            var e = Entry(item, true);
            e.consumed = Math.Max(0, e.consumed - qty);
            e.runConsumed = Math.Max(0, e.runConsumed - qty);
            if (e.bk != null) e.bk[Buckets + _slot] -= qty;   // may dip below 0 within a bucket; rates clamp the sum
        }

        void Bump(string item, int kind, int qty)
        {
            var e = Entry(item, true);
            switch (kind)
            {
                case 0: e.produced += qty; e.runProduced += qty; break;
                case 1: e.consumed += qty; e.runConsumed += qty; break;
                default: e.lost += qty; e.runLost += qty; break;
            }
            (e.bk ??= new int[3 * Buckets])[kind * Buckets + _slot] += qty;
        }

        /// <summary>The entry of an item (null when never seen and not <paramref name="create"/>).</summary>
        public FlowEntry Entry(string item, bool create = false)
        {
            if (_idx == null || _idx.Count != items.Count) RebuildIndex();
            if (_idx.TryGetValue(item, out int i)) return items[i];
            if (!create) return null;
            var e = new FlowEntry { item = item, bk = new int[3 * Buckets] };
            _idx[item] = items.Count;
            items.Add(e);
            return e;
        }

        void RebuildIndex()
        {
            _idx ??= new Dictionary<string, int>();
            _idx.Clear();
            for (int i = 0; i < items.Count; i++) if (items[i].item != null) _idx[items[i].item] = i;
        }

        /// <summary>Zero the per-run section (ascension). Lifetime totals and rates are kept.</summary>
        public void ResetRun()
        {
            foreach (var e in items) e.runProduced = e.runConsumed = e.runLost = 0;
        }

        // ---- rolling rate ----

        /// <summary>Roll the 1-minute buckets forward to <paramref name="nowMs"/> (call once per tick).</summary>
        public void Advance(double nowMs)
        {
            _nowMs = nowMs;
            long m = (long)Math.Floor(nowMs / MinuteMs);
            if (!_started) { _started = true; _minute = m; _startMs = nowMs; _slot = (int)(m % Buckets); return; }
            if (m <= _minute) return;
            int n = (int)Math.Min(m - _minute, Buckets);
            for (int s = 1; s <= n; s++)
            {
                int slot = (int)((_minute + s) % Buckets);
                foreach (var e in items)
                    if (e.bk != null) { e.bk[slot] = 0; e.bk[Buckets + slot] = 0; e.bk[2 * Buckets + slot] = 0; }
            }
            _minute = m;
            _slot = (int)(m % Buckets);
        }

        /// <summary>Minutes the rolling window currently spans (≤ 5; shorter right after start / load).</summary>
        public double WindowMinutes
        {
            get
            {
                if (!_started) return 0;
                double frac = (_nowMs - _minute * MinuteMs) / MinuteMs;
                double span = Math.Min(Buckets - 1 + Math.Max(0, Math.Min(1, frac)), (_nowMs - _startMs) / MinuteMs);
                return Math.Max(span, 0.5);
            }
        }

        double Sum(FlowEntry e, int kind)
        {
            if (e.bk == null) return 0;
            long t = 0;
            for (int i = 0; i < Buckets; i++) t += e.bk[kind * Buckets + i];
            return Math.Max(0, t);
        }

        public double ProducedPerMin(FlowEntry e) => _started ? Sum(e, 0) / WindowMinutes : 0;
        public double ConsumedPerMin(FlowEntry e) => _started ? Sum(e, 1) / WindowMinutes : 0;
        public double LostPerMin(FlowEntry e) => _started ? Sum(e, 2) / WindowMinutes : 0;
        /// <summary>Net items per minute over the window: produced − consumed − lost.</summary>
        public double NetPerMin(FlowEntry e) => _started ? (Sum(e, 0) - Sum(e, 1) - Sum(e, 2)) / WindowMinutes : 0;

        // ---- load ----

        /// <summary>Drop unknown / duplicate / null entries, clamp negatives, keep run ≤ lifetime. Idempotent.</summary>
        public void Sanitize(Func<string, bool> live)
        {
            items ??= new List<FlowEntry>();
            var seen = new HashSet<string>();
            var keep = new List<FlowEntry>(items.Count);
            foreach (var e in items)
            {
                if (e == null || e.item == null || !live(e.item) || !seen.Add(e.item)) continue;
                e.produced = Math.Max(0, e.produced); e.consumed = Math.Max(0, e.consumed); e.lost = Math.Max(0, e.lost);
                e.runProduced = Math.Max(0, Math.Min(e.runProduced, e.produced));
                e.runConsumed = Math.Max(0, Math.Min(e.runConsumed, e.consumed));
                e.runLost = Math.Max(0, Math.Min(e.runLost, e.lost));
                keep.Add(e);
            }
            items = keep;
            _idx = null;
            _started = false;
        }
    }
}
