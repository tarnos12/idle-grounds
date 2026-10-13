using IdleGrounds.Game;
using NUnit.Framework;
using UnityEngine;

namespace IdleGrounds.Game.Tests
{
    /// <summary>Phone layout maths (EditMode): density, CanvasScaler reference height and safe-area insets.</summary>
    public class MobileUiTests
    {
        [TearDown] public void TearDown() => MobileUi.ClearForce();

        [Test]
        public void DpToPx_Scales_With_Density()
        {
            MobileUi.Force(true, 320f);
            Assert.AreEqual(96f, MobileUi.DpToPx(48f), 0.01f, "48 dp at 320 dpi = 96 px");
            MobileUi.Force(true, 480f);
            Assert.AreEqual(144f, MobileUi.DpToPx(48f), 0.01f);
        }

        [Test]
        public void RefHeight_Phone_IsSmall_Tablet_Clamped()
        {
            // 6.5" 1080x2400 (~405 dpi): 2.67 in tall -> ~533 logical units
            MobileUi.Force(true, 405f, new Vector2Int(2400, 1080));
            Assert.That(MobileUi.RefHeight(), Is.InRange(MobileUi.RefHeightMin, 560f));
            // 1440x3200 at ~540 dpi is the same physical height
            MobileUi.Force(true, 540f, new Vector2Int(3200, 1440));
            Assert.That(MobileUi.RefHeight(), Is.InRange(MobileUi.RefHeightMin, 560f));
            // a 10" tablet (1200 px tall, 220 dpi) never exceeds the max
            MobileUi.Force(true, 220f, new Vector2Int(1920, 1200));
            Assert.AreEqual(MobileUi.RefHeightMax, MobileUi.RefHeight(), 0.01f);
        }

        [Test]
        public void Phone_Keeps_Text_At_Least_2mm()
        {
            // a 20-unit label at the phone scale must stay >= ~2.0 mm tall (Android 14 sp ~ 2.2 mm)
            MobileUi.Force(true, 405f, new Vector2Int(2400, 1080));
            float scale = MobileUi.ScreenH / MobileUi.RefHeight();     // px per logical unit
            float mm = 20f * scale / MobileUi.Dpi * 25.4f;
            Assert.GreaterOrEqual(mm, 2.0f, "20-unit label = " + mm.ToString("0.0") + " mm");
        }

        [Test]
        public void ForcedInsets_Are_Returned()
        {
            MobileUi.Force(true, 400f, new Vector2Int(2400, 1080), new Vector4(90f, 20f, 60f, 30f));
            Assert.AreEqual(new Vector4(90f, 20f, 60f, 30f), MobileUi.SafeInsets());
        }
    }
}
