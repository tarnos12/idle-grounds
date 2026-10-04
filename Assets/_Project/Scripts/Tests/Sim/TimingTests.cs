using System;
using IdleGrounds.Sim;
using NUnit.Framework;

namespace IdleGrounds.Sim.Tests
{
    public class TimingTests
    {
        static int CountFires(GameConfig cfg, double step, double interval, double start, double end)
        {
            var t = new Timing(cfg);
            double due = 0;
            int fired = 0;
            for (double now = start; now <= end; now += step)
            {
                t.BeginTick(now);
                var tm = t.Periodic(due, interval, now);
                if (tm.n == 0) continue;
                fired += tm.n;
                due = tm.next;
            }
            return fired;
        }

        [Test]
        public void Periodic_CoarseTicksFireSameCountAsFine()
        {
            var cfg = SimTestUtil.LoadConfig();
            foreach (double interval in new[] { 300.0, 1000.0, 1234.5 })
            {
                int fine = CountFires(cfg, 50, interval, 1_000_000, 1_000_000 + 64_000);
                int coarse = CountFires(cfg, 640, interval, 1_000_000, 1_000_000 + 64_000);
                Assert.AreEqual(fine, coarse, "interval " + interval);
                Assert.AreEqual((int)Math.Floor(64_000 / interval) + 1, fine, "interval " + interval);
            }
        }

        [Test]
        public void Periodic_ZeroClockFiresOnceImmediately_ThenEveryInterval()
        {
            var t = new Timing(SimTestUtil.LoadConfig());
            t.BeginTick(5000);
            var r = t.Periodic(0, 1000, 5000);
            Assert.AreEqual(1, r.n);
            Assert.AreEqual(6000, r.next);
            t.BeginTick(5050);
            Assert.AreEqual(0, t.Periodic(r.next, 1000, 5050).n);
        }

        [Test]
        public void Periodic_StaleClockRestartsWithoutBurst()
        {
            var t = new Timing(SimTestUtil.LoadConfig());
            t.BeginTick(100_000);
            t.BeginTick(100_050);                       // tickGap 50
            var r = t.Periodic(10_000, 1000, 100_050);  // due long before previous tick
            Assert.AreEqual(1, r.n);
            Assert.AreEqual(101_050, r.next);
            // within the gap: catches up (no restart)
            t.BeginTick(105_050);                       // tickGap 5000
            var r2 = t.Periodic(101_050, 1000, 105_050);
            Assert.AreEqual(5, r2.n);
            Assert.AreEqual(106_050, r2.next);
        }

        [Test]
        public void Periodic_EdgeCases()
        {
            var t = new Timing(SimTestUtil.LoadConfig());
            t.BeginTick(1000);
            Assert.AreEqual(0, t.Periodic(0, 0, 1000).n);
            Assert.AreEqual(0, t.Periodic(0, -5, 1000).n);
            Assert.AreEqual(1, t.Periodic(double.NaN, 100, 1000).n);
            // capped at maxTickEvents
            t.BeginTick(11_000);    // gap 10000
            var r = t.Periodic(1000, 1, 11_000);
            Assert.AreEqual(400, r.n);
            Assert.AreEqual(11_000, r.next);    // max(1400, now - interval + 1)
        }

        [Test]
        public void TickGap_ClampedAndZeroOnFirstOrBackwards()
        {
            var t = new Timing(SimTestUtil.LoadConfig());
            t.BeginTick(1000);
            Assert.AreEqual(0, t.TickGap);
            t.BeginTick(60_000);
            Assert.AreEqual(10_000, t.TickGap);
            t.BeginTick(50_000);
            Assert.AreEqual(0, t.TickGap);
        }

        [Test]
        public void PrestigeFactor_ScaledAndBuffScale()
        {
            var cfg = SimTestUtil.LoadConfig();
            var t = new Timing(cfg);
            var s = GameState.CreateInitial(cfg, 0);
            Assert.AreEqual(1.0, t.PrestigeFactor(s), 1e-12);
            s.ascensions = 1;
            Assert.AreEqual(1 / 1.2, t.PrestigeFactor(s), 1e-12);
            s.perks.Set("haste", 2);
            s.won = true;
            s.vows.done.Set("burden", 1);
            Assert.AreEqual(1 / 1.2 * 0.9025 * 0.9 * 0.96, t.PrestigeFactor(s), 1e-12);
            s.vows.active.Add("restless");
            Assert.AreEqual(1 / 1.4 * 0.9025 * 0.9 * 0.96 * 0.96, t.NextPrestigeFactor(s), 1e-12);

            Assert.AreEqual(5, t.Scaled(10));
            Assert.AreEqual(1, t.Scaled(1));
            Assert.AreEqual(2, t.Scaled(3));
            Assert.AreEqual(1, t.BuffScale);
            Assert.AreEqual(0.2, t.TimeScale, 1e-12);
            cfg.test.enabled = false;
            Assert.AreEqual(10, t.Scaled(10));
            Assert.AreEqual(4, t.BuffScale);
            Assert.AreEqual(1, t.TimeScale);
        }

        [Test]
        public void Rng_IsDeterministic_AndRandInclusive()
        {
            var a = new XorShiftRng(7);
            var b = new XorShiftRng(7);
            bool sawLo = false, sawHi = false;
            for (int i = 0; i < 2000; i++)
            {
                int x = a.Rand(-2, 2);
                Assert.AreEqual(x, b.Rand(-2, 2));
                Assert.That(x, Is.InRange(-2, 2));
                sawLo |= x == -2; sawHi |= x == 2;
                double d = a.Next01(); b.Next01();
                Assert.That(d, Is.GreaterThanOrEqualTo(0).And.LessThan(1));
            }
            Assert.IsTrue(sawLo && sawHi);
            var spec = new DropSpec("wood", 3, 3);
            Assert.AreEqual(3, a.RollAmount(spec));
        }
    }
}
