using NUnit.Framework;
using UnityEngine;

namespace IdleGrounds.Game.Tests
{
    /// <summary>
    /// ART-SPEC §3.9.8 step F, integrator note 2: a killed enemy's view holds on for its die strip
    /// (played once at <see cref="EnemyView.DieFps"/>, about 0.5 s for 4 frames) before release.
    /// </summary>
    public class EnemyDieHoldTests
    {
        [Test]
        public void DieFrameAt_PlaysEachFrameOnce_ThenEnds()
        {
            const int n = 4;
            float step = 1f / EnemyView.DieFps;
            Assert.AreEqual(0, EnemyView.DieFrameAt(0f, n));
            Assert.AreEqual(0, EnemyView.DieFrameAt(step * 0.5f, n));
            Assert.AreEqual(1, EnemyView.DieFrameAt(step * 1.5f, n));
            Assert.AreEqual(2, EnemyView.DieFrameAt(step * 2.5f, n));
            Assert.AreEqual(3, EnemyView.DieFrameAt(step * 3.5f, n));
            Assert.AreEqual(-1, EnemyView.DieFrameAt(step * 4.01f, n), "released after the last frame");
            Assert.AreEqual(-1, EnemyView.DieFrameAt(10f, n));
        }

        [Test]
        public void DieFrameAt_NoFrames_ReleasesAtOnce()
        {
            Assert.AreEqual(-1, EnemyView.DieFrameAt(0f, 0));
            Assert.AreEqual(0f, EnemyView.DieHoldSeconds(0));
        }

        [Test]
        public void DieHold_FourFrames_IsHalfASecond()
        {
            Assert.AreEqual(8f, EnemyView.DieFps);
            Assert.That(EnemyView.DieHoldSeconds(4), Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void DieFrameAt_BeforeStart_ShowsFirstFrame()
        {
            Assert.AreEqual(0, EnemyView.DieFrameAt(-0.1f, 4));
        }
    }
}
