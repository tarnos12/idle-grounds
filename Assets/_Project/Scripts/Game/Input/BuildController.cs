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
        [SerializeField] LinkEditorView linkEditor;
        [Header("M5 progression panels")]
        [SerializeField] UpgradeTreeView upgradeTree;
        [SerializeField] PavilionPanelView pavilionPanel;
        [SerializeField] DragonDialogView dragonDialog;
        [Header("ADR 0003 Spirit Bridges")]
        [SerializeField] BridgePanelView bridgePanel;

        IdleGroundsControls controls;

        public string Placing { get; private set; }
        public bool Demolishing { get; private set; }
        public bool BuildMenuOpen => buildMenu != null && buildMenu.IsOpen;
        public bool RecipePickerOpen => recipePicker != null && recipePicker.IsOpen;
        public bool LinkEditorOpen => linkEditor != null && linkEditor.IsOpen;
        public LinkEditorView LinkEditor => linkEditor;
        public bool UpgradeTreeOpen => upgradeTree != null && upgradeTree.IsOpen;
        public bool PavilionOpen => pavilionPanel != null && pavilionPanel.IsOpen;
        public bool DragonDialogOpen => dragonDialog != null && dragonDialog.IsOpen;
        public bool BridgePanelOpen => bridgePanel != null && bridgePanel.IsOpen;
        public BridgePanelView BridgePanel => bridgePanel;
        public UpgradeTreeView UpgradeTree => upgradeTree;
        public PavilionPanelView PavilionPanel => pavilionPanel;
        public DragonDialogView DragonDialog => dragonDialog;
        public bool AnyPanelOpen => BuildMenuOpen || RecipePickerOpen || LinkEditorOpen || PavilionOpen || BridgePanelOpen || UpgradeTreeOpen || DragonDialogOpen;
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
            CloseRecipePicker(); CloseLinkEditor(); ClosePavilion(); CloseBridgePanel();
            if (buildMenu != null) buildMenu.Open();
        }

        public void CloseBuildMenu() { if (buildMenu != null) buildMenu.Close(); }

        /// <summary>Card click: enter placement mode (the menu closes).</summary>
        public void BeginPlacing(string type)
        {
            if (runner.Config.Building(type) == null) return;
            CloseBuildMenu();
            CloseRecipePicker(); CloseLinkEditor();
            Demolishing = false;
            Placing = type;
        }

        public void ToggleDemolish()
        {
            bool on = !Demolishing;
            CancelModes();
            CloseBuildMenu();
            CloseLinkEditor();
            Demolishing = on;
        }

        public void CancelModes() { Placing = null; Demolishing = false; if (preview != null) preview.Hide(); }

        /// <summary>
        /// Esc chain (ui.js onKeyDown U:3178): (the C#-only confirm dialog) → tree → recipe picker → roster →
        /// link editor → dragon dialog → perk shop → ascend → stats → help → welcome → post-ascension card
        /// (MetaUiController) → placing/demolish + build menu.
        /// </summary>
        public void Escape()
        {
            var meta = MetaUiController.Instance;
            if (meta != null && meta.EscapeConfirm()) return;          // a yes/no confirm sits above everything
            if (UpgradeTreeOpen) { upgradeTree.Close(); return; }
            if (RecipePickerOpen) { CloseRecipePicker(); return; }
            if (PavilionOpen) { pavilionPanel.Close(); return; }
            if (BridgePanelOpen) { bridgePanel.Close(); return; }
            if (LinkEditorOpen) { linkEditor.Escape(); return; }      // picking backs out to the menu, then closes
            if (DragonDialogOpen) { dragonDialog.Continue(); return; }
            if (meta != null && meta.Escape()) return;                 // M7 modals
            CancelModes();
            CloseBuildMenu();
        }

        // ================= world clicks (called by HandController) =================

        /// <summary>LMB steps 1-2 (§3.2). True = consumed (placing / demolish mode).</summary>
        public bool HandleWorldLeftClick(string area, double lx, double ly, bool shift)
        {
            // link picking captures ALL world clicks until done / cancelled (ui.js:2972)
            // (only clicks inside an unlocked region: void / locked clicks fall through like ui.js:2963-2970)
            if (linkEditor != null && linkEditor.Picking && area != null && runner.IsUnlocked(area)) { linkEditor.HandleWorldClick(area, lx, ly); return true; }
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
            if (linkEditor != null && linkEditor.Picking) { linkEditor.Escape(); return true; }
            if (Placing == null && !Demolishing) return false;
            CancelModes();
            return true;
        }

        public string ReasonAt(string area, string type, int row, int col)
        {
            if (area == null) return "Off the edge";
            if (!runner.IsUnlocked(area)) return "Island locked";
            return Sim.PlaceReason(area, type, row, col);
        }

        public Building TryPlace(string area, int row, int col, bool keepPlacing = false)
        {
            if (Placing == null) return null;
            string why = ReasonAt(area, Placing, row, col);
            if (why != null)
            {
                LastRefusal = why;
                AudioService.Play("error");
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
            if (!ok) AudioService.Play("error");
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
            CloseBuildMenu(); CloseBridgePanel();
            recipePicker.Open(area, b);
        }

        public void CloseRecipePicker() { if (recipePicker != null && recipePicker.IsOpen) recipePicker.Close(); }

        // ================= lantern link editor =================

        public void OpenLinkEditor(string area, Building lantern)
        {
            if (linkEditor == null || lantern == null) return;
            CloseBuildMenu(); CloseRecipePicker(); CloseBridgePanel();
            linkEditor.Open(area, lantern);
        }

        public void CloseLinkEditor() { if (linkEditor != null && linkEditor.IsOpen) linkEditor.Close(); }

        /// <summary>Clicking elsewhere on the map closes the one open building panel (ui.js:3014).</summary>
        public void CloseBuildingPanels() { CloseRecipePicker(); CloseLinkEditor(); ClosePavilion(); CloseBridgePanel(); }

        /// <summary>Spirit Bridge panel (ADR 0003): pairing list / partner + Unpair.</summary>
        public void OpenBridgePanel(string area, Building b)
        {
            if (bridgePanel == null || b == null) return;
            CloseBuildMenu(); CloseRecipePicker(); CloseLinkEditor(); ClosePavilion();
            bridgePanel.Open(area, b);
        }

        public void CloseBridgePanel() { if (BridgePanelOpen) bridgePanel.Close(); }

        // ================= M5: Altar tree + pavilion roster =================

        public void OpenUpgradeTree()
        {
            if (upgradeTree == null) return;
            CloseBuildMenu(); CloseBuildingPanels(); CancelModes();
            upgradeTree.Open();
        }

        public void OpenPavilion(string area, Building b)
        {
            if (pavilionPanel == null || b == null) return;
            CloseBuildMenu(); CloseRecipePicker(); CloseLinkEditor(); CloseBridgePanel();
            pavilionPanel.Open(area, b);
        }

        public void ClosePavilion() { if (PavilionOpen) pavilionPanel.Close(); }

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
                reason, def.fuel, reach, rgb, nearBottom, runner.Database.BuildingArt(Placing));
        }

        (bool, bool, bool, bool) HighlightFor(BuildingView v)
        {
            bool hovered = Hovered != null && v.Building == Hovered && v.Area == HoveredArea && Placing == null;
            bool selected = RecipePickerOpen && recipePicker.Area == v.Area && recipePicker.BuildingId == v.Building.id;
            bool hover = hovered && !Demolishing && v.Building.built;
            bool reach = false;
            if (LinkEditorOpen && linkEditor.Area == v.Area)
            {
                int id = v.Building.id;
                bool cand = linkEditor.IsCandidate(id);
                // edited lantern + chosen source gold; valid picks outlined, gold while hovered
                selected = id == linkEditor.LanternId || (linkEditor.Picking && id == linkEditor.SourceId) || (cand && hovered);
                hover = cand && !selected;
                reach = v.Def != null && v.Def.gather.enabled;      // every stone of the region shows its circle
            }
            return (hover, selected, hovered && Demolishing, reach);
        }

        public static bool ShiftHeld => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
    }
}
