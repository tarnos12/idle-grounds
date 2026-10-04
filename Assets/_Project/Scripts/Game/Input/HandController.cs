using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace IdleGrounds.Game
{
    /// <summary>
    /// World mouse input for the hand (ui-input-render §3.2, engine-systems §5.6-5.7):
    /// left press/hold = harvest (node swing pacing, fixture throttle) or vacuum (type-locked when
    /// the press starts on an item); right press/hold = drop / feed with the latch + ramp rules;
    /// Q / E rotate the hand. Picking is sim geometry (cells / px) — no colliders.
    /// M3: placement / demolish clicks route to <see cref="BuildController"/> first; a converter click
    /// opens the recipe picker; storehouse / seal / gathering stone clicks withdraw (1 → 5 per
    /// second hold ramp). Link picking, lantern/pavilion/altar panels and enemies are M4+.
    /// </summary>
    public class HandController : MonoBehaviour
    {
        public const double PickupR = 64, LockR = 24, EdgePickPx = 10, EdgePickFrac = 0.15, EdgeItemPx = 14;
        public const double ClickCooldownMs = 100, GroundRepeatMs = 400, FullBuzzMs = 900, ErrBuzzMs = 400;
        public const double SuctionHz = 60;

        [SerializeField] GameRunner runner;
        [SerializeField] FxService fx;
        [SerializeField] BuildController build;
        [SerializeField] Camera worldCamera;
        [Tooltip("M5: world-space region unlock signs (clicked before anything else, like the DOM buttons).")]
        [SerializeField] UnlockSignSync unlockSigns;

        [Header("Debug / automation (drives the cursor without a mouse)")]
        public bool useDebugCursor;
        public Vector2 debugCursorWorld;

        // ---- cursor (refreshed every frame) ----
        public bool CursorOver { get; private set; }       // inside the viewport and not over UI
        public bool PointerOverUI { get; private set; }
        public Vector2 CursorScreen { get; private set; }
        public Vector2 CursorWorld { get; private set; }
        public string CursorArea { get; private set; }     // region under the cursor (locked too), null in void
        public double Lx { get; private set; }
        public double Ly { get; private set; }
        public int LRow => (int)System.Math.Floor(Ly / Cell);
        public int LCol => (int)System.Math.Floor(Lx / Cell);

        // ---- left state ----
        bool leftHeld, pickupMode, harvestHeld;
        // withdraw hold (ui.js withdrawSH): building being emptied into the hand
        string withdrawArea; int withdrawId;
        double withdrawStart, lastWithdraw, lastWithdrawErr = -1e9;
        public const double WithdrawErrMs = 500;
        public bool WithdrawHoldActive => leftHeld && withdrawId > 0;
        string suckFilter;
        double lastClickAt = -1e9, lastSwing, lastFullBuzz = -1e9, suctionAcc;
        readonly Dictionary<string, double> fixtureHitAt = new Dictionary<string, double>();

        // ---- attack hold (M6) ----
        bool attackHeld;
        double lastAttack;
        public bool AttackHoldActive => attackHeld;
        public int Attacks { get; private set; }

        // ---- right state ----
        struct HoldTarget { public string region; public int id; public bool wasBuilt; public int stage; }
        bool rightHeld, holdDone, hasHoldTarget;
        HoldTarget holdTarget;
        string holdFront;
        double holdStart, lastDrop, lastErrBuzz;

        IdleGroundsControls controls;

        public bool LeftHeld => leftHeld;
        public bool RightHeld => rightHeld;
        public bool VacuumActive => leftHeld && pickupMode;
        public bool HarvestHoldActive => harvestHeld;
        public string SuckFilter => suckFilter;

        Simulation Sim => runner.Sim;
        GameState S => runner.State;
        int Cell => runner != null && runner.Space != null ? runner.Space.Cell : 32;
        /// <summary>UI clock (ms) — the JS Date.now() of the hold loops.</summary>
        static double Now => Time.realtimeSinceStartupAsDouble * 1000.0;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (worldCamera == null) worldCamera = Camera.main;
            controls = new IdleGroundsControls();
        }

        void OnEnable() { controls ??= new IdleGroundsControls(); controls.Gameplay.Enable(); }
        void OnDisable() => controls?.Gameplay.Disable();
        void OnDestroy() => controls?.Dispose();

        void Update()
        {
            if (runner == null || runner.Sim == null) return;
            UpdateCursor();

            // Q / E without ctrl / alt (ui.js onKeyDown ignores modified keys)
            var kb = Keyboard.current;
            bool mod = kb != null && (kb.ctrlKey.isPressed || kb.altKey.isPressed);
            if (!mod && controls.Gameplay.RotateLeft.WasPressedThisFrame()) Sim.RotateHand(+1);    // Q: front stack to the back
            if (!mod && controls.Gameplay.RotateRight.WasPressedThisFrame()) Sim.RotateHand(-1);   // E: back stack to the front

            if (!useDebugCursor)
            {
                if (controls.Gameplay.Primary.WasPressedThisFrame() && CursorOver) LeftDown();
                if (controls.Gameplay.Secondary.WasPressedThisFrame() && CursorOver) RightDown();
                if (controls.Gameplay.Primary.WasReleasedThisFrame()) LeftUp();
                if (controls.Gameplay.Secondary.WasReleasedThisFrame()) RightUp();
            }
            StepHolds(Time.unscaledDeltaTime * 1000.0);
        }

        void UpdateCursor()
        {
            if (useDebugCursor)
            {
                PointerOverUI = false;
                CursorWorld = debugCursorWorld;
                CursorScreen = worldCamera != null ? (Vector2)worldCamera.WorldToScreenPoint(debugCursorWorld) : Vector2.zero;
                CursorOver = true;
            }
            else
            {
                var mouse = Mouse.current;
                CursorScreen = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
                PointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                bool inView = CursorScreen.x >= 0 && CursorScreen.y >= 0 && CursorScreen.x < Screen.width && CursorScreen.y < Screen.height;
                CursorOver = mouse != null && inView && !PointerOverUI;
                if (worldCamera != null) CursorWorld = worldCamera.ScreenToWorldPoint(new Vector3(CursorScreen.x, CursorScreen.y, -worldCamera.transform.position.z));
            }
            if (runner.Space.WorldToArea(CursorWorld, out var a, out var x, out var y)) { CursorArea = a; Lx = x; Ly = y; }
            else { CursorArea = null; Lx = Ly = 0; }
        }

        // ================= public drive API (debug / automation / tests) =================

        /// <summary>Point the virtual cursor at a world position (enables the debug cursor).</summary>
        public void DebugPointAt(Vector2 world) { useDebugCursor = true; debugCursorWorld = world; UpdateCursor(); }
        public void DebugLeftDown() { UpdateCursor(); if (CursorOver) LeftDown(); }
        public void DebugLeftUp() => LeftUp();
        public void DebugRightDown() { UpdateCursor(); if (CursorOver) RightDown(); }
        public void DebugRightUp() => RightUp();
        /// <summary>Drop every hold (RunReset / modal).</summary>
        public void CancelHolds() { LeftUp(); RightUp(); }
        public void ReleaseDebugCursor() { LeftUp(); RightUp(); useDebugCursor = false; }

        // ================= press handlers =================

        void LeftDown()
        {
            string area = CursorArea;
            // 0. a region unlock sign pays the hand toward that region (DOM button in the original)
            if (TryUnlockSign()) return;
            // 1-2. placement / demolish modes capture the click
            if (build != null && build.HandleWorldLeftClick(area, Lx, Ly, BuildController.ShiftHeld)) return;
            if (area == null) return;
            if (!runner.IsUnlocked(area))
            {
                // clicking a real but still-locked region: nudge toward the unlock border
                Error(area, Lx, Ly, "Unlock this border first");
                return;
            }
            double lx = Lx, ly = Ly; int row = LRow, col = LCol;
            var areaState = S.Area(area);

            // 5. enemy under the cursor (before buildings): one counted strike per 100 ms, faster = flinch only
            var en = Sim.EnemyAt(area, lx, ly);
            if (en != null)
            {
                double t = Now;
                if (t - lastClickAt >= ClickCooldownMs) { Sim.Attack(area, en.id); Attacks++; lastClickAt = t; }
                else Sim.FlinchEnemy(area, en.id);
                if (fx != null) fx.Swing(area, en.x, en.y);     // strike spark, counted or not (ui.js:3006)
                leftHeld = true; attackHeld = true; lastAttack = t;
                return;
            }

            // 6. building under the cursor — edge-pick may redirect to vacuum. Only the Altar / Gate /
            //    panel / withdraw buildings consume the click; any other building (Dragon, ghosts, plain
            //    buildings) falls through to the node / vacuum checks (ui.js:3020-3071).
            var b = Sim.World.BuildingAt(area, row, col);
            if (b != null && !EdgePickRedirect(b, area, lx, ly) && BuildingLeftClick(area, b)) return;
            if (build != null) build.CloseBuildingPanels();     // clicking elsewhere closes the panel

            {
                var node = NodeAtCell(areaState, row, col);
                if (node != null && !node.deco)
                {
                    double t = Now;
                    string fk = area + ":" + node.id;
                    double iv = Sim.Timing.HarvestInterval(S, areaState, node);
                    bool fixReady = !node.isFixed || t - FixtureHit(fk) >= iv;
                    if (t - lastClickAt >= ClickCooldownMs && fixReady)
                    {
                        Sim.Harvest(area, node.id, false, false);
                        lastClickAt = t;
                        if (node.isFixed) fixtureHitAt[fk] = t;
                    }
                    else node.hitAt = runner.SimNow;    // too fast to count — still show the hit
                    if (fx != null) fx.Swing(area, lx, ly);    // swing spark at the cursor on every click (ui.js:3062)
                    leftHeld = true; harvestHeld = true; lastSwing = t;
                    return;
                }
            }

            if (GroundNear(areaState, lx, ly, PickupR, null))
            {
                leftHeld = true; pickupMode = true; suctionAcc = 0;
                var lockItem = NearestGround(areaState, lx, ly, LockR);
                suckFilter = lockItem?.item;     // starting ON an item type-locks the hold
                if (Sim.Hand.Space() <= 0) HandFullNudge(area, lx, ly);
                var s0 = Sim.Suction(area, lx, ly, PickupR, suckFilter);
                FxPickup(area, lx, ly, s0.picked);
            }
        }

        /// <summary>
        /// Building click (ui.js U:3020-3045): converter → recipe picker; storehouse / seal / gathering stone
        /// → immediate withdraw of 1, then the 1 → 5 per second hold. Altar, Gate,
        /// lantern and pavilion open their panels. Anything else closes the open panel and returns false
        /// (not consumed: the click falls through to the node / vacuum checks).
        /// </summary>
        bool BuildingLeftClick(string area, Building b)
        {
            var def = runner.Config.Building(b.type);
            if (b.built && b.type == "center")     // the Altar
            {
                if (build != null) build.OpenUpgradeTree();
                return true;
            }
            if (b.built && def != null && def.gate)       // the Ascension Gate re-opens the ascend dialog
            {
                if (build != null) build.CloseBuildingPanels();
                Sim.SetAscendPrompt(true);
                return true;
            }
            if (b.built && def != null && def.roster.enabled)
            {
                if (build != null) build.OpenPavilion(area, b);
                return true;
            }
            if (b.built && def != null && def.IsConverter)
            {
                if (build != null) build.OpenRecipePicker(area, b);
                return true;
            }
            if (b.built && def != null && def.lantern.enabled)
            {
                if (build != null) build.OpenLinkEditor(area, b);
                return true;
            }
            if (build != null) build.CloseBuildingPanels();
            // (the Furnace Spirit is withdrawable in the sim but ui.js only offers it for these three)
            if (b.built && def != null && (b.type == "storehouse" || def.seal.enabled || def.gather.enabled))
            {
                leftHeld = true; withdrawArea = area; withdrawId = b.id;
                int got = Sim.Withdraw(area, b.id, 1);
                if (got > 0) FxPickup(area, Lx, Ly, got);
                else if (Now - lastWithdrawErr >= WithdrawErrMs) { lastWithdrawErr = Now; AudioService.Play("error"); }
                withdrawStart = Now; lastWithdraw = withdrawStart;
                return true;
            }
            return false;
        }

        void LeftUp()
        {
            leftHeld = false; pickupMode = false; harvestHeld = false; suckFilter = null; attackHeld = false;
            withdrawId = 0; withdrawArea = null;
        }

        void RightDown()
        {
            if (TryUnlockSign()) return;     // right-click on an unlock sign pays like left click
            // 1. right-click cancels placing / demolish and stops
            if (build != null && build.CancelModeFromRightClick()) return;
            string area = CursorArea;
            if (area == null || !runner.IsUnlocked(area)) return;
            rightHeld = true; holdStart = Now; holdDone = false;
            var d = RackRedirect(area, Lx, Ly);
            var tb = Sim.World.BuildingAt(area, FloorCell(d.y), FloorCell(d.x));
            hasHoldTarget = tb != null;
            holdTarget = tb != null ? new HoldTarget { region = area, id = tb.id, wasBuilt = tb.built, stage = S.dragon.stage } : default;
            holdFront = tb == null && S.hand.Count > 0 ? S.hand[0].item : null;
            var r = Sim.DropFromHand(area, d.x, d.y, tb != null);
            if (tb != null) holdDone = FeedHoldEnded(r);
            else if (r != null && r.once) holdFront = null;   // a quaffed pill / bait lure: one per press
            if (r == null)
            {
                lastErrBuzz = holdStart;
                Error(area, Lx, Ly, "✗");
            }
            lastDrop = holdStart;      // next drop waits a full interval
        }

        void RightUp()
        {
            rightHeld = false; lastErrBuzz = 0; hasHoldTarget = false; holdDone = false; holdFront = null;
        }

        // ================= per-frame hold loop =================

        void StepHolds(double dtMs)
        {
            string rg = CursorArea != null && runner.IsUnlocked(CursorArea) ? CursorArea : null;
            double now = Now;

            if (leftHeld && pickupMode && CursorOver && rg != null)
            {
                // suction runs at a fixed 60 Hz (the JS ran it per animation frame)
                suctionAcc += dtMs;
                double step = 1000.0 / SuctionHz;
                int steps = 0, picked = 0;
                while (suctionAcc >= step && steps < 4)
                {
                    suctionAcc -= step; steps++;
                    picked += Sim.Suction(rg, Lx, Ly, PickupR, suckFilter).picked;
                }
                if (suctionAcc >= step) suctionAcc = 0;
                if (picked > 0) FxPickup(rg, Lx, Ly, picked);
                else if (Sim.Hand.Space() <= 0 && now - lastFullBuzz >= FullBuzzMs && GroundNear(S.Area(rg), Lx, Ly, PickupR, suckFilter))
                    HandFullNudge(rg, Lx, Ly);
            }

            if (leftHeld && withdrawId > 0)
            {
                // withdraw rate ramps 1/s -> 5/s over the first 0.2 s of the hold
                double rate = 1 + System.Math.Min((now - withdrawStart) / 200.0, 1) * 4;
                if (now - lastWithdraw >= 1000.0 / rate)
                {
                    if (Sim.Withdraw(withdrawArea, withdrawId, 1) > 0) { if (rg != null) FxPickup(rg, Lx, Ly, 1); }
                    else if (now - lastWithdrawErr >= WithdrawErrMs) { lastWithdrawErr = now; AudioService.Play("error"); }   // emptied: buzz once
                    lastWithdraw = Now;
                }
            }

            if (attackHeld && CursorOver && rg != null)
            {
                // attack-hold: re-hit-test the enemy under the cursor every enemies.attackMs (default 400)
                int iv = Sim.AttackIntervalMs(rg);
                if (iv <= 0) iv = 400;
                if (now - lastAttack >= iv)
                {
                    var en = Sim.EnemyAt(rg, Lx, Ly);
                    if (en != null) { Sim.Attack(rg, en.id); Attacks++; if (fx != null) fx.Swing(rg, en.x, en.y); }
                    lastAttack = Now;
                }
            }

            if (harvestHeld && CursorOver && rg != null)
            {
                var areaState = S.Area(rg);
                var n = NodeAtCell(areaState, LRow, LCol);
                if (n != null && !n.deco)
                {
                    double iv = Sim.Timing.HarvestInterval(S, areaState, n);
                    string fk = rg + ":" + n.id;
                    if (now - lastSwing >= iv && (!n.isFixed || now - FixtureHit(fk) >= iv))
                    {
                        // a full hand stops the auto-swing (a single click still harvests)
                        if (Sim.Hand.Space() <= 0) HandFullNudge(rg, Lx, Ly);
                        else
                        {
                            Sim.Harvest(rg, n.id, false, true);
                            if (fx != null) fx.Swing(rg, Lx, Ly);
                            lastSwing = Now;
                            if (n.isFixed) fixtureHitAt[fk] = lastSwing;
                        }
                    }
                }
            }

            // a modal popping up (dragon stage story, ascend, help...) ends a right-hold (ui.js:3262)
            if (rightHeld && (hasHoldTarget || holdFront != null) && !holdDone && AnyModalOpen())
            {
                holdDone = true; holdFront = null;
            }

            if (rightHeld && CursorOver && rg != null && hasHoldTarget && !holdDone)
            {
                // latched feed-hold: ramp 4 -> 20/s over 200 ms, only while still on the same building
                double elapsed = now - holdStart;
                double rate = 4 + System.Math.Min(elapsed / 200.0, 1) * 16;
                var d = RackRedirect(rg, Lx, Ly);
                var b = rg == holdTarget.region ? Sim.World.BuildingAt(rg, FloorCell(d.y), FloorCell(d.x)) : null;
                if (b != null && b.id == holdTarget.id && now - lastDrop >= 1000.0 / rate)
                {
                    var r = Sim.DropFromHand(rg, d.x, d.y, true);
                    lastDrop = Now;
                    holdDone = FeedHoldEnded(r);
                    if (r == null && Now - lastErrBuzz >= ErrBuzzMs)
                    {
                        lastErrBuzz = Now;
                        Error(rg, Lx, Ly, "✗");
                    }
                }
            }
            else if (rightHeld && CursorOver && rg != null && holdFront != null)
            {
                // ground-hold: repeats only after 400 ms, then ramps 4 -> 20/s; stops when the front
                // stack runs out or changes; paused while over a building / burner rack
                double elapsed = now - holdStart - GroundRepeatMs;
                var front = S.hand.Count > 0 ? S.hand[0] : null;
                if (front == null || front.item != holdFront) holdFront = null;
                else if (elapsed >= 0 && now - lastDrop >= 1000.0 / (4 + System.Math.Min(elapsed / 200.0, 1) * 16)
                         && !GroundHoldBlocked(rg, Lx, Ly))
                {
                    var r = Sim.DropFromHand(rg, Lx, Ly, false);
                    lastDrop = Now;
                    if (r != null && r.once) holdFront = null;
                }
            }
        }

        // ================= helpers =================

        /// <summary>Any modal overlay up (M7 modals, the dragon dialog, the Altar tree) — the `.modal:not(.hidden)` test.</summary>
        bool AnyModalOpen() =>
            (MetaUiController.Instance != null && MetaUiController.Instance.AnyOpen) ||
            (build != null && (build.DragonDialogOpen || build.UpgradeTreeOpen));

        bool TryUnlockSign()
        {
            if (unlockSigns == null) return false;
            var sign = unlockSigns.HitTest(CursorWorld);
            if (sign == null) return false;
            unlockSigns.Pay(sign, CursorWorld);
            return true;
        }

        int FloorCell(double px) => (int)System.Math.Floor(px / Cell);

        double FixtureHit(string fk) => fixtureHitAt.TryGetValue(fk, out var t) ? t : -1e9;

        /// <summary>`nodeAtCell` ui.js:372 — first node whose square holds the cell.</summary>
        public static Node NodeAtCell(AreaState area, int row, int col)
        {
            foreach (var n in area.nodes)
                if (row >= n.row && row < n.row + n.size && col >= n.col && col < n.col + n.size) return n;
            return null;
        }

        public static bool GroundNear(AreaState area, double lx, double ly, double r, string item)
        {
            foreach (var g in area.ground)
                if ((item == null || g.item == item) && Dist(g.x - lx, g.y - ly) <= r) return true;
            return false;
        }

        public static GroundItem NearestGround(AreaState area, double lx, double ly, double r)
        {
            GroundItem best = null; double bd = r;
            foreach (var g in area.ground)
            {
                double d = Dist(g.x - lx, g.y - ly);
                if (d <= bd) { bd = d; best = g; }
            }
            return best;
        }

        static double Dist(double dx, double dy) => System.Math.Sqrt(dx * dx + dy * dy);

        /// <summary>`edgePickRedirect` ui.js:3092 — vacuum instead of opening a &gt;1x1 building near its edge.</summary>
        bool EdgePickRedirect(Building b, string area, double lx, double ly)
        {
            var (w, h) = Sim.World.BuildingSize(b.type);
            if (w <= 1 && h <= 1) return false;
            double x0 = b.col * Cell, y0 = b.row * Cell;
            double edge = System.Math.Min(System.Math.Min(lx - x0, x0 + w * Cell - lx), System.Math.Min(ly - y0, y0 + h * Cell - ly));
            if (edge > System.Math.Min(EdgePickPx, EdgePickFrac * System.Math.Min(w, h) * Cell)) return false;
            if (Sim.Hand.Space() <= 0) return false;
            return GroundNear(S.Area(area), lx, ly, EdgeItemPx, null);
        }

        /// <summary>`rackRedirect` ui.js:2900 — a built burner's 3x2 rack (left of it) maps to its centre.</summary>
        (double x, double y) RackRedirect(string area, double lx, double ly)
        {
            int row = FloorCell(ly), col = FloorCell(lx);
            if (Sim.World.BuildingAt(area, row, col) != null) return (lx, ly);
            foreach (var b in S.Area(area).buildings)
            {
                if (!b.built || runner.Config.Building(b.type)?.fuel != true) continue;
                if (row >= b.row && row <= b.row + 1 && col >= b.col - 3 && col <= b.col - 1)
                {
                    var (w, h) = Sim.World.BuildingSize(b.type);
                    return ((b.col + w / 2.0) * Cell, (b.row + h / 2.0) * Cell);
                }
            }
            return (lx, ly);
        }

        bool GroundHoldBlocked(string area, double lx, double ly)
        {
            var d = RackRedirect(area, lx, ly);
            return Sim.World.BuildingAt(area, FloorCell(d.y), FloorCell(d.x)) != null;
        }

        /// <summary>`feedHoldEnded` ui.js — refusal, used/once, dragon stage change, or the ghost got built/removed.</summary>
        bool FeedHoldEnded(DropResult r)
        {
            if (r == null || r.kind == DropResultKind.Used || r.once) return true;
            if (S.dragon.stage != holdTarget.stage) return true;
            if (!holdTarget.wasBuilt)
            {
                var b = S.Area(holdTarget.region)?.BuildingById(holdTarget.id);
                if (b == null || b.built) return true;
            }
            return false;
        }

        bool HandFullNudge(string area, double lx, double ly)
        {
            if (Now - lastFullBuzz < FullBuzzMs) return false;
            lastFullBuzz = Now;
            AudioService.Play("error");
            if (fx != null) fx.FloaterAt(area, lx, ly - 8, "Hand full", FxService.Danger);
            return true;
        }

        void FxPickup(string area, double lx, double ly, int picked)
        {
            if (picked <= 0 || fx == null) return;
            fx.Pickup(area, lx, ly, picked);
        }

        void Error(string area, double lx, double ly, string msg)
        {
            AudioService.Play("error");
            if (fx != null) fx.FloaterAt(area, lx, ly, msg, FxService.Danger);
        }
    }
}
