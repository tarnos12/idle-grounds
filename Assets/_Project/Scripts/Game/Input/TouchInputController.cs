using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Touch gestures for phones / tablets (docs/guides/touch-controls.md). Reads <see cref="Touchscreen"/> and drives the
    /// existing mouse pipeline through <see cref="HandController"/> (virtual cursor + left / right press),
    /// <see cref="CameraController"/> (pan, pinch zoom) and <see cref="BuildController"/> (placement aim). Desktop
    /// mouse / keyboard input is untouched: with no touches this component does nothing.
    ///
    /// One finger, started in the world (a touch that starts on UI belongs to the UI and never reaches the world):
    ///   tap                  = left click at that point (node: one swing; building: panel / withdraw 1; ground: vacuum pulse; enemy: strike)
    ///   drag                 = pan the camera (any drag that moves more than <see cref="PanSlopDp"/> before the hold fires)
    ///   hold still 0.22 s    = left hold (keep harvesting a node, keep attacking, vacuum the ground, withdraw from a storehouse);
    ///                          the finger then moves the cursor (sweeping the vacuum). A hold on ground with nothing to
    ///                          vacuum is not "engaged": moving then pans instead.
    ///   hold still 0.38 s on a building with a non-empty hand = right hold (feed / drop with the ramp), the finger keeps feeding
    ///                          the same building. Storehouse / seal / gathering stone hold = withdraw (left hold).
    ///   "Give" toggle button = every touch is a right click (tap drops / feeds one, hold = ramp, drag moves the cursor).
    ///   Placement / demolish: touch-drag moves the ghost (offset above the finger), release leaves it; the "Place" /
    ///                          "Demolish" button confirms, "Cancel" cancels.
    /// Two fingers: pinch = zoom (continuous, anchored under the fingers), two-finger drag = pan. Any hold is released first.
    /// </summary>
    public class TouchInputController : MonoBehaviour
    {
        public static TouchInputController Instance { get; private set; }

        public enum Mode { None, Pending, Pan, Hold, Feed, Aim, Pinch, PinchWait }

        public const float PanSlopDp = 10f, HoldSeconds = 0.22f, FeedHoldSeconds = 0.38f, AimLiftDp = 56f, InfoLingerSec = 2f;

        /// <summary>"Give" mode: touches act as right clicks (drop / feed).</summary>
        public bool GiveMode { get; set; }
        public Mode CurrentMode => mode;
        public HandController.TouchTarget Target => target;
        public bool FingerDown => mode != Mode.None && mode != Mode.PinchWait;

        HandController hand;
        CameraController cam;
        BuildController build;
        float nextFind;

        Mode mode;
        int primaryId = -1;
        Vector2 startPos, pos, lastPos;
        float startTime;
        HandController.TouchTarget target;
        bool aimPinned;

        int idA = -1, idB = -1;
        Vector2 posA, posB;
        float pinchDist0, pinchZoom0;
        Vector2 pinchAnchor;

        readonly HashSet<int> ignored = new HashSet<int>();
        readonly HashSet<int> finished = new HashSet<int>();     // ended touches still reported by the device (never replay them)
        readonly HashSet<int> seen = new HashSet<int>();      // every slot the device reports (incl. a lingering Ended one)
        readonly HashSet<int> active = new HashSet<int>();    // fingers still on the glass

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        bool Resolve()
        {
            if (hand != null && cam != null && build != null) return true;
            if (Time.unscaledTime < nextFind) return hand != null;
            nextFind = Time.unscaledTime + 1f;
            if (hand == null) hand = FindFirstObjectByType<HandController>();
            if (cam == null) cam = FindFirstObjectByType<CameraController>();
            if (build == null) build = FindFirstObjectByType<BuildController>();
            return hand != null;
        }

        static float Px(float dp) => MobileUi.DpToPx(dp);

        void Update()
        {
            var ts = Touchscreen.current;
            if (ts == null || !Resolve())
            {
                if (mode != Mode.None) CancelAll();
                return;
            }
            if (ModalScrim.Any && mode != Mode.None) CancelAll();

            seen.Clear(); active.Clear();
            var touches = ts.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                var phase = t.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.None) continue;
                int id = t.touchId.ReadValue();
                var p = t.position.ReadValue();
                bool ended = phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled;
                bool canceled = phase == UnityEngine.InputSystem.TouchPhase.Canceled;
                seen.Add(id);
                if (!ended) active.Add(id);
                Feed(id, p, ended, canceled);
            }
            // a tracked finger whose slot vanished without an ended frame (a hitch dropped it)
            if (primaryId >= 0 && !seen.Contains(primaryId) && mode != Mode.Pinch) EndPrimary(pos, true);
            if (mode == Mode.Pinch && (!seen.Contains(idA) || !seen.Contains(idB))) EndPinch();
            ignored.RemoveWhere(id => !seen.Contains(id));
            finished.RemoveWhere(id => !seen.Contains(id));
            if (mode == Mode.PinchWait && active.Count == 0) mode = Mode.None;

            Tick();
            if (aimPinned && build != null && !build.AimActive) { hand.TouchUnpin(); aimPinned = false; }
        }

        // ---------------------------------------------------------------- per-touch events

        void Feed(int id, Vector2 p, bool ended, bool canceled)
        {
            if (finished.Contains(id)) return;
            if (id == primaryId && mode != Mode.Pinch)
            {
                pos = p;
                if (ended) EndPrimary(p, canceled);
                return;
            }
            if (mode == Mode.Pinch && (id == idA || id == idB))
            {
                if (id == idA) posA = p; else posB = p;
                if (ended) { finished.Add(id); EndPinch(); }
                return;
            }
            if (ignored.Contains(id)) return;      // a UI touch, or a surplus finger
            // a finger we have not seen
            if (MobileUi.IsOverUi(p) || ModalScrim.Any || CameraController.PanSuspended) { if (!ended) ignored.Add(id); return; }
            if (mode == Mode.None)
            {
                BeginPrimary(id, p);
                if (ended) EndPrimary(p, canceled);
            }
            else if (!ended && (mode == Mode.Pending || mode == Mode.Pan || mode == Mode.Hold || mode == Mode.Feed || mode == Mode.Aim))
                BeginPinch(id, p);
            else if (!ended) ignored.Add(id);
        }

        void BeginPrimary(int id, Vector2 p)
        {
            primaryId = id; startPos = lastPos = pos = p; startTime = Time.unscaledTime;
            finished.Remove(id);
            Tooltip.DismissAll();
            if (build != null && build.AimActive)
            {
                mode = Mode.Aim; aimPinned = true;
                hand.TouchPoint(Lift(p), true, true);
                return;
            }
            if (GiveMode)
            {
                mode = Mode.Feed;
                hand.TouchPoint(p, true);
                hand.TouchRightDown();
                return;
            }
            mode = Mode.Pending;
            hand.TouchPoint(p, true);
            target = hand.TouchClassify();
        }

        void EndPrimary(Vector2 p, bool canceled)
        {
            finished.Add(primaryId);
            switch (mode)
            {
                case Mode.Pending:
                    if (!canceled && (p - startPos).magnitude > Px(PanSlopDp)) { hand.TouchUp(0f); cam.TouchPan(p - startPos); }   // a flick that began and ended inside one frame
                    else if (!canceled)
                    {
                        // a tap = a left click
                        hand.TouchPoint(p, true);
                        bool onBuilding = hand.TouchOnBuilding();
                        hand.TouchLeftDown();
                        hand.TouchLeftUp();
                        hand.TouchUp(onBuilding && build != null && !build.AnyPanelOpen ? InfoLingerSec : 0f);
                    }
                    else hand.TouchUp(0f);
                    break;
                case Mode.Hold: hand.TouchLeftUp(); hand.TouchUp(0f); break;
                case Mode.Feed: hand.TouchRightUp(); hand.TouchUp(0f); break;
                case Mode.Aim: hand.TouchPoint(Lift(p), false, true); break;
                default: hand.TouchUp(0f); break;
            }
            mode = Mode.None; primaryId = -1;
        }

        void CancelAll()
        {
            if (mode == Mode.Hold) hand.TouchLeftUp();
            if (mode == Mode.Feed) hand.TouchRightUp();
            if (mode == Mode.Aim) { /* keep the pinned ghost */ }
            else hand.TouchUp(0f);
            ignored.Clear();
            mode = Mode.None; primaryId = -1; idA = idB = -1;
        }

        // ---------------------------------------------------------------- per-frame

        void Tick()
        {
            switch (mode)
            {
                case Mode.Pending:
                {
                    float el = Time.unscaledTime - startTime;
                    if ((pos - startPos).magnitude > Px(PanSlopDp))
                    {
                        hand.TouchUp(0f);
                        mode = Mode.Pan;
                        cam.TouchPan(pos - startPos);
                        lastPos = pos;
                        break;
                    }
                    hand.TouchPoint(startPos, true);
                    if (el >= (target == HandController.TouchTarget.Building ? FeedHoldSeconds : HoldSeconds))
                    {
                        if (target == HandController.TouchTarget.Building) { mode = Mode.Feed; hand.TouchRightDown(); }
                        else { mode = Mode.Hold; hand.TouchLeftDown(); }
                    }
                    break;
                }
                case Mode.Hold:
                    hand.TouchPoint(pos, true);
                    // nothing engaged (empty ground, no items near): the drag pans instead
                    if (!hand.LeftHeld && (pos - startPos).magnitude > Px(PanSlopDp))
                    {
                        hand.TouchLeftUp(); hand.TouchUp(0f);
                        mode = Mode.Pan; cam.TouchPan(pos - startPos); lastPos = pos;
                    }
                    break;
                case Mode.Feed:
                    hand.TouchPoint(pos, true);
                    break;
                case Mode.Pan:
                    cam.TouchPan(pos - lastPos);
                    lastPos = pos;
                    break;
                case Mode.Aim:
                    hand.TouchPoint(Lift(pos), true, true);
                    break;
                case Mode.Pinch:
                {
                    float d = Mathf.Max(1f, (posA - posB).magnitude);
                    cam.TouchZoom(pinchZoom0 * pinchDist0 / d, pinchAnchor, (posA + posB) * 0.5f);
                    break;
                }
            }
        }

        Vector2 Lift(Vector2 p) => p + new Vector2(0f, Px(AimLiftDp));

        // ---------------------------------------------------------------- pinch

        void BeginPinch(int secondId, Vector2 secondPos)
        {
            // release whatever the first finger was doing
            if (mode == Mode.Hold) { hand.TouchLeftUp(); hand.TouchUp(0f); }
            else if (mode == Mode.Feed) { hand.TouchRightUp(); hand.TouchUp(0f); }
            else if (mode == Mode.Pending || mode == Mode.Pan) hand.TouchUp(0f);
            idA = primaryId; idB = secondId; posA = pos; posB = secondPos;
            pinchDist0 = Mathf.Max(1f, (posA - posB).magnitude);
            pinchZoom0 = cam.Zoom;
            pinchAnchor = cam.ScreenToWorld((posA + posB) * 0.5f);
            primaryId = -1;
            mode = Mode.Pinch;
        }

        void EndPinch()
        {
            // the remaining finger is ignored until it lifts (no accidental tap / pan after a zoom)
            if (idA >= 0 && active.Contains(idA) && !finished.Contains(idA)) ignored.Add(idA);
            if (idB >= 0 && active.Contains(idB) && !finished.Contains(idB)) ignored.Add(idB);
            idA = idB = -1;
            mode = Mode.PinchWait;
        }

        // ---------------------------------------------------------------- confirm buttons (MobileLayer)

        /// <summary>Confirm the placement / demolish aimed at by the ghost (the on-screen "Place" / "Demolish" button).</summary>
        public bool ConfirmAim()
        {
            if (!Resolve() || build == null || !build.AimActive) return false;
            hand.TouchLeftDown();
            hand.TouchLeftUp();
            return true;
        }

        /// <summary>Cancel placement / demolish (the on-screen "Cancel" button).</summary>
        public void CancelAim()
        {
            if (build != null) build.CancelModes();
            if (hand != null) hand.TouchUnpin();
            aimPinned = false;
        }
    }
}
