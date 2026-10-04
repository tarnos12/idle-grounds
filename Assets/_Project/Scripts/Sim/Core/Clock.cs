using System;

namespace IdleGrounds.Sim
{
    /// <summary>Wall-clock source (ms). Offline replay swaps in a virtual clock.</summary>
    public interface IClock
    {
        long NowMs { get; }
    }

    /// <summary>Manually driven clock (tests, offline replay).</summary>
    public sealed class ManualClock : IClock
    {
        public long NowMs { get; set; }
        public ManualClock(long startMs = 1_000_000) { NowMs = startMs; }
        public void Advance(long ms) { NowMs += ms; }
    }

    /// <summary>Real time: Unix epoch ms (= JS Date.now()).</summary>
    public sealed class SystemClock : IClock
    {
        public long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>Single seedable randomness source for the whole simulation.</summary>
    public interface IRng
    {
        /// <summary>Uniform double in [0,1) (= Math.random()).</summary>
        double Next01();
    }

    /// <summary>xorshift64* PRNG — deterministic, seedable, fast.</summary>
    public sealed class XorShiftRng : IRng
    {
        ulong _s;
        public XorShiftRng(ulong seed = 0x9E3779B97F4A7C15UL) { _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }

        public ulong State { get => _s; set => _s = value == 0 ? 0x9E3779B97F4A7C15UL : value; }

        public ulong NextU64()
        {
            _s ^= _s >> 12;
            _s ^= _s << 25;
            _s ^= _s >> 27;
            return _s * 0x2545F4914F6CDD1DUL;
        }

        public double Next01() => (NextU64() >> 11) * (1.0 / 9007199254740992.0);   // 53 bits
    }

    /// <summary>JS rand helpers routed through an <see cref="IRng"/> (engine-systems §0).</summary>
    public static class RngExt
    {
        /// <summary>`rand(a,b)` engine.js:441 — uniform integer in [a,b] inclusive.</summary>
        public static int Rand(this IRng rng, int a, int b) => a + (int)Math.Floor(rng.Next01() * (b - a + 1));

        /// <summary>`rollAmount(spec)` engine.js:828.</summary>
        public static int RollAmount(this IRng rng, DropSpec spec) => spec.min + (int)Math.Floor(rng.Next01() * (spec.max - spec.min + 1));
    }
}
