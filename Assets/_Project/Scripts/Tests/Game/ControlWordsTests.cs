using IdleGrounds.Game;
using NUnit.Framework;

namespace IdleGrounds.Game.Tests
{
    /// <summary>Touch wording of the control texts (owner phone feedback 2026-10-13).</summary>
    public class ControlWordsTests
    {
        [TearDown] public void TearDown() => MobileUi.ClearForce();

        [TestCase("Hold left-click on the big Spirit Tree to chop it", "Hold on the big Spirit Tree to chop it")]
        [TestCase("then hold left-click near the fallen wood", "then hold near the fallen wood")]
        [TestCase("(WASD to look around, mouse-wheel to zoom.)", "(drag to look around, pinch to zoom.)")]
        [TestCase("RIGHT-click it while carrying the wood", "Give it while carrying the wood")]
        [TestCase("Carry leaves to the dragon and right-click it", "Carry leaves to the dragon and Give it")]
        [TestCase("Click: upgrades  ·  Right-click: feed the job", "Tap: upgrades  ·  Give: feed the job")]
        [TestCase("Press B, place a Storehouse ghost", "Open Build, place a Storehouse ghost")]
        [TestCase("Build a Forge (B), feed it", "Build a Forge, feed it")]
        [TestCase("Demolish: click a building - Esc to stop", "Demolish: tap a building - tap Demolish to stop")]
        [TestCase("the front stack feeds first (Q / E change which stack is in front)", "the front stack feeds first (Swap changes which stack is in front)")]
        [TestCase("Nothing special here", "Nothing special here")]
        public void Rewrite_Uses_Touch_Wording(string src, string want) => Assert.AreEqual(want, ControlWords.Rewrite(src));

        [Test]
        public void Rewrite_Leaves_No_Mouse_Or_Keyboard_Words()
        {
            string s = ControlWords.Rewrite("WASD pans the camera (Shift toggles 2× sprint), mouse wheel zooms, B opens the build menu, Q / E rotate the stacks in your hand, Esc cancels/closes.");
            foreach (var bad in new[] { "WASD", "click", "wheel", "Esc", "Q / E" }) StringAssert.DoesNotContain(bad, s);
        }

        [Test]
        public void Apply_Only_Rewrites_On_Touch_Devices()
        {
            const string src = "Hold left-click on the Spirit Tree";
            MobileUi.Force(false);
            Assert.AreEqual(src, ControlWords.Apply(src), "desktop text is unchanged");
            MobileUi.Force(true, 405f);
            Assert.AreEqual("Hold on the Spirit Tree", ControlWords.Apply(src));
        }
    }
}
