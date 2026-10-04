using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Build / demolish / building-panel state (ui-input-render §3.1-3.2, §4.2-4.3, §2.8):
    /// B toggles the build menu (opening clears placing + demolish); picking a card enters placement
    /// mode — a ghost follows the hovered cell (its TOP-LEFT) tinted by <see cref="Simulation.PlaceReason"/>;
    /// LMB places (Shift keeps placing), RMB / Esc cancel. Demolish mode (bottom-bar button): LMB on a
    /// building demolishes it (Altar/Dragon refuse) and always ends the mode. Converter clicks open
    /// the recipe picker. Esc chain: recipe picker → placing/demolish + build menu.
    /// HandController routes world clicks here first.
    /// </summary>
    public class BuildController : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] HandController hand;
        [SerializeField] FxService fx;
        [SerializeField] BuildingViewSync buildings;
        [SerializeField] PlacementPreview preview;
        [SerializeField] BuildMenuView buildMenu;
        [SerializeField] RecipePickerView recipePicker;

        IdleGroundsControls controls;

        public string Placing { get; private set; }
        public bool Demolishing { get; private set; }
        public bool BuildMenuOpen => buildMenu != null && buildMenu.IsOpen;
        public bool RecipePickerOpen => recipePicker != null && recipePicker.IsOpen;
        public bool AnyPanelOpen => BuildMenuOpen || RecipePickerOpen;
        /// <summary>Building under the cursor (null in void / when over UI).</summary>
        public Building Hovered { get; private set; }
        public string HoveredArea { get; private set; }
        public PlacementPreview Preview => preview;
        /// <summary>Last placement / demolish refusal (for automation + tests).</summary>
        public string LastRefusal { get; private set; }

        Simulation Sim => runner.Sim;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (hand == null) hand = FindFirstObjectByType<HandController>();
            controls = new IdleGroundsControls();
        }

        void OnEnable() { controls ??= new IdleGroundsControls(); controls.Gameplay.Enable(); }
        void OnDisable() => controls?.Gameplay.Disable();
        void OnDestroy() => controls?.Dispose();

        void Start()
        {
            if (buildings != null) buildings.Highlight = HighlightFor;
            if (preview != null) preview.Hide();
        }

        void Update()
        {
            if (runner == null || runner.Sim == null) return;
            if (controls.Gameplay.Build.WasPressedThisFrame()) ToggleBuildMenu();
            if (controls.Gameplay.Cancel.WasPressedThisFrame()) Escape();

            Hovered = null; HoveredArea = null;
            if (hand != null && hand.CursorOver && hand.CursorArea != null)
            {
                HoveredArea = hand.CursorArea;
                Hovered = Sim.World.BuildingAt(hand.CursorArea, hand.LRow, hand.LCol);
            }
            UpdatePreview();
        }

        // ================= modes =================

        public void ToggleBuildMenu()
        {
            if (BuildMenuOpen) CloseBuildMenu();
            else OpenBuildMenu();
        }

        public void OpenBuildMenu()
        {
            Placing = null; Demolishing = false;
            CloseRecipePicker();
            if (buildMenu != null) buildMenu.Open();
        }

        public void CloseBuildMenu() { if (buildMenu != null) buildMenu.Close(); }

        /// <summary>Card click: enter placement mode (the menu closes).</summary>
        public void BeginPlacing(string type)
        {
            if (runner.Config.Building(type) == null) return;
            CloseBuildMenu();
            CloseRecipePicker();
            Demolishing = false;
            Placing = type;
        }

        public void ToggleDemolish()
        {
            bool on = !Demolishing;
            CancelModes();
            CloseBuildMenu();
            Demolishing = on;
        }

        public void CancelModes() { Placing = null; Demolishing = false; if (preview != null) preview.Hide(); }

        /// <summary>Esc chain (§3.1): recipe picker → else placing/demolish + build menu.</summary>
        public void Escape()
        {
            if (RecipePickerOpen) { CloseRecipePicker(); return; }
            CancelModes();
            CloseBuildMenu();
        }

        // ================= world clicks (called by HandController) =================

        /// <summary>LMB steps 1-2 (§3.2). True = consumed (placing / demolish mode).</summary>
        public bool HandleWorldLeftClick(string area, double lx, double ly, bool shift)
        {
            if (Placing != null)
            {
                int row = (int)System.Math.Floor(ly / runner.Space.Cell), col = (int)System.Math.Floor(lx / runner.Space.Cell);
                TryPlace(area, row, col, shift);
                return true;
            }
            if (Demolishing)
            {
                var b = area != null ? Sim.BuildingAt(area, lx, ly) : null;
                TryDemolish(area, b);
                Demolishing = false;          // demolish mode always ends
                return true;
            }
            return false;
        }

        /// <summary>RMB step 1: cancel placing / demolish and stop.</summary>
        public bool CancelModeFromRightClick()
        {
            if (Placing == null && !Demolishing) return false;
            CancelModes();
            return true;
        }

        public string ReasonAt(string area, string type, int row, int col)
        {
            if (area == null) return "Off the edge";
            if (!runner.IsUnlocked(area)) return "Region locked";
            return Sim.PlaceReason(area, type, row, col);
        }

        public Building TryPlace(string area, int row, int col, bool keepPlacing = false)
        {
            if (Placing == null) return null;
            string why = ReasonAt(area, Placing, row, col);
            if (why != null)
            {
                LastRefusal = why;
                // TODO(M4 audio): error SFX
                return null;
            }
            var b = Sim.PlaceGhost(area, Placing, row, col);
            if (b == null) { LastRefusal = "Blocked"; return null; }
            LastRefusal = null;
            if (!keepPlacing) { Placing = null; if (preview != null) preview.Hide(); }
            return b;
        }

        public bool TryDemolish(string area, Building b)
        {
            if (b == null) { LastRefusal = "Nothing here"; return false; }
            bool ok = Sim.Demolish(area, b.id);
            LastRefusal = ok ? null : "Can't demolish";
            if (!ok && fx != null)
            {
                var (x, y) = Sim.World.BuildingCenterPx(b);
                fx.FloaterAt(area, x, y, "Can't demolish", FxService.Danger);
            }
            return ok;
        }

        // ================= recipe picker =================

        public void OpenRecipePicker(string area, Building b)
        {
            if (recipePicker == null || b == null) return;
            CloseBuildMenu();
            recipePicker.Open(area, b);
        }

        public void CloseRecipePicker() { if (recipePicker != null && recipePicker.IsOpen) recipePicker.Close(); }

        // ================= preview + highlight =================

        void UpdatePreview()
        {
            if (preview == null) return;
            if (Placing == null || hand == null || !hand.CursorOver || hand.CursorArea == null) { preview.Hide(); return; }
            var def = runner.Config.Building(Placing);
            string area = hand.CursorArea;
            int row = hand.LRow, col = hand.LCol;
            string reason = ReasonAt(area, Placing, row, col);
            int cell = runner.Space.Cell;
            float reach = def.gather.enabled ? def.gather.radius : def.stoker.enabled ? def.stoker.radius + FormationFaceView.StokeSlackCells : 0f;
            var rgb = def.stoker.enabled ? ViewKit.Rgba(251, 146, 60, 1f) : ViewKit.Rgba(74, 222, 128, 1f);
            bool nearBottom = row + def.sizeH >= runner.Space.Cells - 1;
            preview.Show(runner.Space.PxToWorld(area, col * cell, row * cell), def.sizeW, def.sizeH,
                buildings != null && buildings.Sprites != null ? buildings.Sprites.Building(Placing) : runner.Database.BuildingIcon(Placing),
                reason, def.fuel, reach, rgb, nearBottom);
        }

        (bool, bool, bool) HighlightFor(BuildingView v)
        {
            bool hovered = Hovered != null && v.Building == Hovered && v.Area == HoveredArea && Placing == null;
            bool selected = RecipePickerOpen && recipePicker.Area == v.Area && recipePicker.BuildingId == v.Building.id;
            return (hovered && !Demolishing && v.Building.built, selected, hovered && Demolishing);
        }

        public static bool ShiftHeld => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
    }
}
