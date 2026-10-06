using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    /// <summary>
    /// Steady-state <see cref="Simulation.Tick"/> on the starter network allocates (almost) nothing:
    /// no per-tick snapshots, closures, scratch lists or InFlight objects. What remains is real state
    /// being created — a ground item dropped by a field generator / converter, a wisp launched by a
    /// lantern — a few dozen bytes per tick on average. Before the non-alloc pass this was ~64
    /// allocations / ~15 KB per tick.
    /// </summary>
    public class SimTickAllocTests
    {
        [Test]
        public void SteadyStateTick_AllocatesAlmostNothing()
        {
            var sim = SimTestUtil.NewSim(out var clock);
            for (int t = 0; t < 60_000; t += 50) { clock.Advance(50); sim.Tick(); }   // warm-up: JIT, buffers, a busy network

            const int N = 2000;
            long bytes, count;
            using (var rb = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame"))
            using (var rc = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocation In Frame Count"))
            {
                Assert.IsTrue(rb.Valid && rc.Valid, "GC recorders");
                long b0 = rb.CurrentValue, c0 = rc.CurrentValue;
                for (int i = 0; i < N; i++) { clock.Advance(50); sim.Tick(); }
                bytes = rb.CurrentValue - b0; count = rc.CurrentValue - c0;
            }
            var center = sim.State.Area("center");
            Assert.Greater(center.wisps.Count + center.ground.Count, 0, "the starter network is actually running");

            double bytesPerTick = bytes / (double)N, allocsPerTick = count / (double)N;
            TestContext.Out.WriteLine($"Simulation.Tick: {allocsPerTick:F3} allocs/tick, {bytesPerTick:F1} B/tick");
            Assert.Less(bytesPerTick, 128, $"Tick allocated {bytesPerTick:F1} B/tick ({allocsPerTick:F3} allocs/tick)");
            Assert.Less(allocsPerTick, 1.5, $"Tick made {allocsPerTick:F3} allocations/tick");
        }
    }
}
