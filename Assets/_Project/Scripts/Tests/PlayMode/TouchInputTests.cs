using System.Collections;
using System.IO;
using System.Linq;
using IdleGrounds.Game;
using IdleGrounds.Sim;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace IdleGrounds.PlayMode.Tests
{
    /// <summary>
    /// Touch controls end to end: loads Game.unity with a fresh save, pretends to be a 420 dpi phone and drives a simulated
    /// <see cref="Touchscreen"/> through the Input System test fixture (the same events a finger produces).
    /// The owner's save is backed up and restored like GameSmokeTests.
    /// </summary>
    // Named to sort BEFORE GameSmokeTests: a fixture-less run leaves the shared input-action asset bound to the old input state, which breaks UI touches here.
    public class A_TouchInputTests : InputTestFixture
    {
        const string Center = "center";

        byte[] savedBytes, savedTmpBytes;
        Touchscreen touch;
        Keyboard keyboard;
        HandController hand;
        CameraController cam;
        BuildController build;
        int manualHits;
        System.Action<string, Node, bool> hitHandler;

        static Simulation Sim => GameRunner.Instance.Sim;
        static GameState S => GameRunner.Instance.State;

        // ------------------------------------------------------------------ harness

        // NOTE: not a [UnitySetUp]: that runs BEFORE InputTestFixture.Setup(), which would wipe the devices added here.
        // Every test starts with `yield return Boot();`.
        IEnumerator Boot()
        {
            string p = SaveService.SavePath;
            savedBytes = File.Exists(p) ? File.ReadAllBytes(p) : null;
            savedTmpBytes = File.Exists(p + ".tmp") ? File.ReadAllBytes(p + ".tmp") : null;
            Delete();
            touch = InputSystem.AddDevice<Touchscreen>();
            keyboard = InputSystem.AddDevice<Keyboard>();

            var op = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            for (int i = 0; i < 3; i++) yield return null;

            // GameSmokeTests (no InputTestFixture) may have left the shared input-action asset bound to the old input state:
            // re-enable the UI module so its actions bind to this fixture's devices.
            var uiModule = Object.FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (uiModule != null)
            {
                uiModule.actionsAsset?.Disable();
                uiModule.enabled = false; yield return null; uiModule.enabled = true; yield return null;
            }
            var meta = MetaUiController.Instance;
            if (meta != null && meta.Welcome.IsOpen) meta.Welcome.Continue();
            S.quest.idx = Sim.Config.quests.Count;            // reveal every building
            MobileUi.Force(true, 420f);
            var layer = MobileLayer.Ensure();
            layer.Refresh();
            for (int i = 0; i < 40 && !layer.Built; i++) yield return null;
            Assert.IsTrue(layer.Built, "MobileLayer built its HUD");
            for (int i = 0; i < 4; i++) yield return null;

            hand = Object.FindFirstObjectByType<HandController>();
            cam = Object.FindFirstObjectByType<CameraController>();
            build = Object.FindFirstObjectByType<BuildController>();
            Assert.IsNotNull(hand); Assert.IsNotNull(cam); Assert.IsNotNull(build);
            Assert.Greater(Screen.width, 100, "a real screen size (Screen " + Screen.width + "x" + Screen.height + ")");
            cam.SetZoomImmediate(2f);
            manualHits = 0;
            hitHandler = (a, n, auto) => { if (!auto) manualHits++; };
            Sim.Events.NodeHit += hitHandler;
            Sim.Hand.Take("wood", 1000); Sim.Hand.Take("stone", 1000);
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (GameRunner.Instance != null && hitHandler != null) Sim.Events.NodeHit -= hitHandler;
            if (TouchInputController.Instance != null) TouchInputController.Instance.GiveMode = false;
            MobileUi.ClearForce();
            if (SaveService.Instance != null) Object.Destroy(SaveService.Instance.gameObject);
            if (GameRunner.Instance != null) Object.Destroy(GameRunner.Instance.gameObject);
            yield return null;
            Delete();
            string p = SaveService.SavePath;
            if (savedBytes != null) File.WriteAllBytes(p, savedBytes);
            if (savedTmpBytes != null) File.WriteAllBytes(p + ".tmp", savedTmpBytes);
        }

        static void Delete()
        {
            string p = SaveService.SavePath;
            if (File.Exists(p)) File.Delete(p);
            if (File.Exists(p + ".tmp")) File.Delete(p + ".tmp");
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        static IEnumerator Seconds(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        Vector2 ScreenOf(double px, double py) => cam.GetComponent<Camera>().WorldToScreenPoint(GameRunner.Instance.Space.PxToWorld(Center, px, py));

        /// <summary>Put an Island-pixel point at the middle of the screen and return its screen position.</summary>
        Vector2 Focus(double px, double py)
        {
            cam.CenterOn(GameRunner.Instance.Space.PxToWorld(Center, px, py));
            return ScreenOf(px, py);
        }

        Node SpiritTree()
        {
            var n = S.Area(Center).nodes.FirstOrDefault(x => x.kind == "spirittree");
            Assert.IsNotNull(n, "Spirit Tree fixture");
            return n;
        }

        static (double x, double y) NodeCenter(Node n) => ((n.col + n.size / 2.0) * 32, (n.row + n.size / 2.0) * 32);

        /// <summary>A free cell (top-left) and its pixel centre for ground / ghost tests.</summary>
        static (int r, int c, double x, double y) FreeSpot()
        {
            for (int r = 24; r < 76; r += 3)
                for (int c = 24; c < 76; c += 3)
                    if (Sim.PlaceReason(Center, "wisp_lantern", r, c) == null && HandController.NodeAtCell(S.Area(Center), r, c) == null)
                        return (r, c, (c + 0.5) * 32, (r + 0.5) * 32);
            Assert.Fail("no free spot");
            return default;
        }

        static Vector2 CenterOf(RectTransform rt)
        {
            var c = rt.TransformPoint(rt.rect.center);
            return RectTransformUtility.WorldToScreenPoint(null, c);
        }

        static Button FindButton(string name) =>
            Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(b => b.name == name);

        // ------------------------------------------------------------------ tap / hold harvest

        [UnityTest, Timeout(60000)]
        public IEnumerator Tap_OnNode_IsOneSwing()
        {
            yield return Boot();
            var tree = SpiritTree();
            var (x, y) = NodeCenter(tree);
            var pos = Focus(x, y);
            yield return Frames(2);
            BeginTouch(1, pos);
            yield return Frames(3);                    // a short tap: well under the hold time
            EndTouch(1, pos);
            yield return Frames(4);
            Assert.AreEqual(1, manualHits, "one tap = one swing");
            Assert.AreEqual(TouchInputController.Mode.None, TouchInputController.Instance.CurrentMode);
            Assert.IsFalse(hand.HarvestHoldActive, "no hold left running");
            yield return Seconds(0.8f);
            Assert.AreEqual(1, manualHits, "nothing keeps swinging after the finger lifted");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Hold_OnNode_KeepsHarvesting_UntilRelease()
        {
            yield return Boot();
            var tree = SpiritTree();
            var (x, y) = NodeCenter(tree);
            var pos = Focus(x, y);
            yield return Frames(2);
            BeginTouch(1, pos);
            yield return Seconds(2.6f);
            Assert.IsTrue(hand.HarvestHoldActive, "holding the finger keeps the harvest hold");
            Assert.GreaterOrEqual(manualHits, 3, "repeated swings while held (got " + manualHits + ")");
            EndTouch(1, pos);
            yield return Frames(4);
            int after = manualHits;
            Assert.IsFalse(hand.HarvestHoldActive);
            yield return Seconds(0.9f);
            Assert.AreEqual(after, manualHits, "release stops the swings");
        }

        // ------------------------------------------------------------------ vacuum

        [UnityTest, Timeout(60000)]
        public IEnumerator Tap_OnGroundItem_PicksItUp_Pulse()
        {
            yield return Boot();
            var (r, c, x, y) = FreeSpot();
            var area = S.Area(Center);
            area.ground.Add(new GroundItem { id = area.nextGroundId++, item = "wood", x = x, y = y });
            var pos = Focus(x, y);
            yield return Frames(2);
            BeginTouch(1, pos);
            yield return Frames(2);
            EndTouch(1, pos);
            yield return Frames(3);
            Assert.AreEqual(1, Sim.Hand.Count("wood"), "tap vacuums the item under the finger");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Hold_OnGround_Vacuums_AndDragSweeps()
        {
            yield return Boot();
            var (r, c, x, y) = FreeSpot();
            var area = S.Area(Center);
            area.ground.Add(new GroundItem { id = area.nextGroundId++, item = "wood", x = x, y = y });
            area.ground.Add(new GroundItem { id = area.nextGroundId++, item = "wood", x = x + 6, y = y + 4 });
            var pos = Focus(x, y);
            yield return Frames(2);
            BeginTouch(1, pos);
            yield return Seconds(0.7f);                // past the hold time: vacuum engaged
            Assert.IsTrue(hand.VacuumActive, "hold on ground = vacuum");
            Assert.GreaterOrEqual(Sim.Hand.Count("wood"), 2, "both items sucked");
            // a new item further away: dragging the held finger sweeps the vacuum (does NOT pan)
            var camBefore = cam.Centre;
            area.ground.Add(new GroundItem { id = area.nextGroundId++, item = "wood", x = x + 64, y = y });
            var pos2 = ScreenOf(x + 64, y);
            for (int i = 1; i <= 6; i++) { MoveTouch(1, Vector2.Lerp(pos, pos2, i / 6f)); yield return null; }
            yield return Frames(6);
            Assert.GreaterOrEqual(Sim.Hand.Count("wood"), 3, "the dragged vacuum picked the third item");
            Assert.AreEqual(camBefore.x, cam.Centre.x, 0.001f, "no camera pan during a vacuum sweep");
            EndTouch(1, pos2);
            yield return Frames(3);
            Assert.IsFalse(hand.VacuumActive);
        }

        // ------------------------------------------------------------------ feed / drop

        [UnityTest, Timeout(60000)]
        public IEnumerator LongPress_OnGhost_FeedsIt()
        {
            yield return Boot();
            var (r, c, x, y) = FreeSpot();
            var g = Sim.PlaceGhost(Center, "wisp_lantern", r, c);
            Assert.IsNotNull(g, "ghost placed");
            var cost = Sim.Config.Building("wisp_lantern").cost[0];
            Sim.Hand.Add(cost.item, System.Math.Min(cost.qty, Sim.Hand.Space()));
            int before = Sim.Hand.Count(cost.item);
            var (bx, by) = Sim.World.BuildingCenterPx(g);
            var pos = Focus(bx, by);
            yield return Frames(2);
            BeginTouch(1, pos);
            yield return Seconds(1.0f);                // past the 0.38 s feed hold
            EndTouch(1, pos);
            yield return Frames(3);
            Assert.Less(Sim.Hand.Count(cost.item), before, "long press fed the ghost from the hand");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator GiveMode_Tap_DropsOneItemOnTheGround()
        {
            yield return Boot();
            var (r, c, x, y) = FreeSpot();
            Sim.Hand.Add("wood", 3);
            TouchInputController.Instance.GiveMode = true;
            var pos = Focus(x, y);
            yield return Frames(2);
            BeginTouch(1, pos);
            yield return Frames(2);
            EndTouch(1, pos);
            yield return Frames(3);
            Assert.Less(Sim.Hand.Count("wood"), 3, "Give mode: a tap is a right click (drop)");
            Assert.IsTrue(S.Area(Center).ground.Any(g => g.item == "wood" && System.Math.Abs(g.x - x) < 48 && System.Math.Abs(g.y - y) < 48), "the dropped item lies on the ground at the touch");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SwapButton_RotatesStacks_AndShowsTooltip()
        {
            yield return Boot();
            Sim.Hand.Add("wood", 2); Sim.Hand.Add("stone", 2);
            Sim.Hand.MoveToFront("wood");
            Assert.AreEqual("wood", S.hand[0].item);
            var swap = FindButton("Swap");
            Assert.IsNotNull(swap, "Swap button");
            var pos = CenterOf((RectTransform)swap.transform);
            var dbg = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = pos }, dbg);
            string hitNames = string.Join(",", dbg.Select(h => h.gameObject.name));
            BeginTouch(1, pos);
            yield return Frames(2);
            Assert.IsNotNull(Tooltip.ShownText, "a touch on a UI control shows its tip; pos " + pos + " hits [" + hitNames + "] swap active " + swap.gameObject.activeInHierarchy + " mode " + TouchInputController.Instance.CurrentMode + " scrim " + ModalScrim.Any + " overUi " + UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(0) + "/" + UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject() + " touchActive " + touch.primaryTouch.isInProgress + " tpos " + touch.primaryTouch.position.ReadValue() + " module " + UnityEngine.EventSystems.EventSystem.current.currentInputModule);
            EndTouch(1, pos);
            yield return Frames(3);
            Assert.AreEqual("stone", S.hand[0].item, "Swap = Q: the front stack went to the back");
            Assert.AreEqual(0, manualHits);
        }

        // ------------------------------------------------------------------ pan / pinch

        [UnityTest, Timeout(60000)]
        public IEnumerator Drag_StartingOnANode_Pans_NoSwing()
        {
            yield return Boot();
            var tree = SpiritTree();
            var (x, y) = NodeCenter(tree);
            var pos = Focus(x, y);
            yield return Frames(3);
            var c0 = cam.Centre;
            BeginTouch(1, pos);
            yield return null;
            for (int i = 1; i <= 8; i++) { MoveTouch(1, pos + new Vector2(40f * i, 0f)); yield return null; }
            yield return Frames(2);
            Assert.AreEqual(TouchInputController.Mode.Pan, TouchInputController.Instance.CurrentMode);
            Assert.Less(cam.Centre.x, c0.x - 0.5f, "dragging right moves the camera left (the world follows the finger)");
            EndTouch(1, pos + new Vector2(320f, 0f));
            yield return Frames(4);
            Assert.AreEqual(0, manualHits, "a pan never swings");
            Assert.IsFalse(hand.LeftHeld);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Pinch_Zooms_InAndOut()
        {
            yield return Boot();
            var tree = SpiritTree();
            var (x, y) = NodeCenter(tree);
            var mid = Focus(x, y);
            yield return Frames(3);
            float z0 = cam.Zoom;
            var a0 = mid + new Vector2(-100f, 0f); var b0 = mid + new Vector2(100f, 0f);
            BeginTouch(1, a0); yield return null;
            BeginTouch(2, b0); yield return null;
            for (int i = 1; i <= 6; i++)                  // fingers apart: zoom in (smaller view)
            {
                float d = 100f + 25f * i;
                MoveTouch(1, mid + new Vector2(-d, 0f)); MoveTouch(2, mid + new Vector2(d, 0f)); yield return null;
            }
            yield return Frames(2);
            Assert.AreEqual(TouchInputController.Mode.Pinch, TouchInputController.Instance.CurrentMode);
            float z1 = cam.Zoom;
            Assert.Less(z1, z0 - 0.3f, "pinch out zooms in (" + z0 + " -> " + z1 + ")");
            for (int i = 1; i <= 6; i++)                  // pinch in: zoom out
            {
                float d = 250f - 30f * i;
                MoveTouch(1, mid + new Vector2(-d, 0f)); MoveTouch(2, mid + new Vector2(d, 0f)); yield return null;
            }
            yield return Frames(2);
            Assert.Greater(cam.Zoom, z1 + 0.3f, "pinch in zooms out");
            EndTouch(1, mid + new Vector2(-70f, 0f)); EndTouch(2, mid + new Vector2(70f, 0f));
            yield return Frames(4);
            Assert.AreEqual(0, manualHits, "a pinch never swings");
            Assert.AreEqual(TouchInputController.Mode.None, TouchInputController.Instance.CurrentMode);
        }

        // ------------------------------------------------------------------ UI never leaks

        [UnityTest, Timeout(60000)]
        public IEnumerator UiTap_DoesNotLeakIntoTheWorld()
        {
            yield return Boot();
            Sim.Hand.Add("wood", 2); Sim.Hand.Add("stone", 2);
            Sim.Hand.MoveToFront("wood");
            var btn = FindButton("Swap");
            var pos = CenterOf((RectTransform)btn.transform);
            // park the Spirit Tree exactly under that button: a leak would swing at it
            var tree = SpiritTree();
            var (x, y) = NodeCenter(tree);
            var cc = cam.GetComponent<Camera>();
            var shift = cc.ScreenToWorldPoint(new Vector3(pos.x, pos.y, 10f)) - cc.ScreenToWorldPoint(new Vector3(cc.pixelWidth / 2f, cc.pixelHeight / 2f, 10f));
            cam.CenterOn((Vector2)GameRunner.Instance.Space.PxToWorld(Center, x, y) - (Vector2)shift);
            yield return Frames(3);
            BeginTouch(1, pos);
            yield return Frames(3);
            Assert.AreEqual(TouchInputController.Mode.None, TouchInputController.Instance.CurrentMode, "a UI touch starts no world gesture");
            Assert.IsFalse(hand.TouchDown, "the hand never saw the finger");
            yield return Seconds(0.6f);                    // longer than any hold
            Assert.IsFalse(hand.LeftHeld);
            EndTouch(1, pos);
            yield return Frames(4);
            Assert.AreEqual(0, manualHits, "UI tap must not swing at the node behind it");
            Assert.AreEqual("stone", S.hand[0].item, "the tap reached the button (Swap rotated the hand)");
        }

        // ------------------------------------------------------------------ build aiming

        [UnityTest, Timeout(60000)]
        public IEnumerator Placement_DragGhost_ThenConfirmButton_Places()
        {
            yield return Boot();
            var (r, c, x, y) = FreeSpot();
            int before = S.Area(Center).buildings.Count;
            build.BeginPlacing("wisp_lantern");
            yield return Frames(3);
            var confirm = FindButton("Confirm");
            Assert.IsNotNull(confirm, "aim bar Confirm button");
            Assert.IsTrue(confirm.gameObject.activeInHierarchy, "aim bar shows while placing");
            var target = Focus(x, y);
            float lift = MobileUi.DpToPx(TouchInputController.AimLiftDp);
            var finger = target - new Vector2(0f, lift);
            BeginTouch(1, finger + new Vector2(-120f, 150f));
            yield return null;
            for (int i = 1; i <= 5; i++) { MoveTouch(1, Vector2.Lerp(finger + new Vector2(-120f, 150f), finger, i / 5f)); yield return null; }
            EndTouch(1, finger);
            yield return null;
            yield return Frames(3);
            Assert.IsNotNull(build.Placing, "lifting the finger does not place");
            Assert.AreEqual(before, S.Area(Center).buildings.Count);
            Assert.IsTrue(hand.CursorOver, "the aim point stays pinned (TouchSource " + hand.TouchSource + ", cursor " + hand.CursorScreen + ", screen " + Screen.width + "x" + Screen.height + ", mode " + TouchInputController.Instance.CurrentMode + ", placing " + build.Placing + ")");
            Assert.AreEqual(r, hand.LRow); Assert.AreEqual(c, hand.LCol);

            var pos = CenterOf((RectTransform)confirm.transform);
            BeginTouch(2, pos); yield return Frames(2); EndTouch(2, pos);
            yield return Frames(4);
            Assert.IsNull(build.Placing, "confirm placed the ghost");
            Assert.AreEqual(before + 1, S.Area(Center).buildings.Count);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Placement_CancelButton_Cancels()
        {
            yield return Boot();
            build.BeginPlacing("wisp_lantern");
            yield return Frames(3);
            var cancel = FindButton("Cancel");
            Assert.IsNotNull(cancel);
            var pos = CenterOf((RectTransform)cancel.transform);
            BeginTouch(1, pos); yield return Frames(2); EndTouch(1, pos);
            yield return Frames(3);
            Assert.IsNull(build.Placing);
        }

        // ------------------------------------------------------------------ phone layout

        [UnityTest, Timeout(60000)]
        public IEnumerator SafeArea_InsetsTheHud_ScrimsStillBleed()
        {
            yield return Boot();
            MobileUi.Force(true, 420f, default, new Vector4(120f, 30f, 90f, 24f));
            yield return Frames(4);
            var safe = MobileLayer.Instance.SafeArea;
            float f = safe.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
            Assert.AreEqual(120f / f, safe.offsetMin.x, 0.5f, "left inset (canvas units)");
            Assert.AreEqual(30f / f, safe.offsetMin.y, 0.5f, "bottom inset");
            Assert.AreEqual(-90f / f, safe.offsetMax.x, 0.5f, "right inset");
            Assert.AreEqual(-24f / f, safe.offsetMax.y, 0.5f, "top inset");
            var scrim = safe.GetComponentsInChildren<ModalScrim>(true).FirstOrDefault();
            if (scrim != null)
            {
                var rt = (RectTransform)scrim.transform;
                Assert.AreEqual(-120f / f, rt.offsetMin.x, 0.5f, "modal scrim covers the notch strip");
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator SmallControls_GetFingerSizedHitAreas()
        {
            yield return Boot();
            yield return Seconds(1.0f);                       // MobileLayer rescans every 0.75 s
            float minPx = MobileUi.DpToPx(MobileUi.MinTargetDp);
            int small = 0, padded = 0;
            foreach (var sel in Object.FindObjectsByType<Selectable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var g = sel.targetGraphic;
                if (g == null || !g.raycastTarget) continue;
                var rt = g.rectTransform; var sc = rt.lossyScale;
                if (sc.x <= 0.0001f) continue;
                float w = rt.rect.width * sc.x, h = rt.rect.height * sc.y;
                if (w >= minPx && h >= minPx) continue;
                small++;
                float effW = w + (-g.raycastPadding.x - g.raycastPadding.z) * sc.x, effH = h + (-g.raycastPadding.y - g.raycastPadding.w) * sc.y;
                if (effW >= minPx - 1f && effH >= minPx - 1f) padded++;
            }
            Assert.AreEqual(small, padded, "every control smaller than 48 dp has a >= 48 dp touch area (" + small + " small)");
        }

        // ------------------------------------------------------------------ Android Back

        [UnityTest, Timeout(60000)]
        public IEnumerator BackButton_OpensAndClosesPauseMenu_OrClosesPanelsFirst()
        {
            yield return Boot();
            yield return Frames(2);
            Press(keyboard.escapeKey); yield return Frames(2); Release(keyboard.escapeKey); yield return Frames(2);
            Assert.IsTrue(MobileLayer.Instance.PauseOpen, "Back with nothing open opens the menu");
            Press(keyboard.escapeKey); yield return Frames(2); Release(keyboard.escapeKey); yield return Frames(2);
            Assert.IsFalse(MobileLayer.Instance.PauseOpen, "Back again closes it");
            build.OpenBuildMenu();
            yield return Frames(2);
            Press(keyboard.escapeKey); yield return Frames(2); Release(keyboard.escapeKey); yield return Frames(2);
            Assert.IsFalse(build.BuildMenuOpen, "Back closes the open build menu first");
            Assert.IsFalse(MobileLayer.Instance.PauseOpen, "...without opening the menu");
        }
    }
}
