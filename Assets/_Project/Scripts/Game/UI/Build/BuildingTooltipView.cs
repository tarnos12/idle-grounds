using IdleGrounds.Sim;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Hover name (spec §3.3, ui.js updateHoverName U:2881): the BUILT building under the cursor in an
    /// UNLOCKED region, bottom-centre above the bar ("Awakened Dragon" once won). Shown while placing too;
    /// hidden while the build menu / recipe picker / link editor / roster (or any other world panel) is up.
    /// C# extra: a <see cref="Simulation.BuildingStatus"/> line under the name (green working + crafts/min,
    /// red starved / no fuel, amber full, muted idle), refreshed every frame via the non-allocating queries (text rebuilt only on change).
    /// </summary>
    public class BuildingTooltipView : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] BuildController controller;
        [SerializeField] GameObject panel;
        [SerializeField] TextMeshProUGUI nameText;
        [SerializeField] TextMeshProUGUI statusText;

        public string ShownName => panel != null && panel.activeSelf ? nameText.text : null;
        public string ShownStatus => panel != null && panel.activeSelf && statusText.gameObject.activeSelf ? statusText.text : null;

        Building lastBuilding;
        readonly BuildingStatusInfo stBuf = new BuildingStatusInfo();
        readonly ConverterFace faceBuf = new ConverterFace();
        BuildingState lastState = (BuildingState)(-1); string lastLabel; double lastCpm = -1; bool lastHas;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (panel != null) panel.SetActive(false);
        }

        // ---- VP-19: anchored to the top edge of the hovered building (never under the cursor), kept on screen;
        //      line 2 = status with its shape (not colour alone), line 3 = one hint, shown after a 0.6 s dwell ----
        public const float DwellSec = 0.6f;
        RectTransform rootRt;
        Canvas canvas;
        BuildingViewSync views;
        UnityEngine.UI.Image statusShape;
        TextMeshProUGUI hintText;
        float hoverStart;
        bool built;
        /// <summary>Third line (hint), null while hidden.</summary>
        public string ShownHint => panel != null && panel.activeSelf && hintText != null && hintText.gameObject.activeSelf ? hintText.text : null;
        /// <summary>Canvas-local position of the tag bottom-centre this frame.</summary>
        public Vector2 AnchorScreen { get; private set; }

        void BuildExtras()
        {
            if (built || statusText == null || panel == null) return;
            built = true;
            rootRt = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            views = FindFirstObjectByType<BuildingViewSync>();
            // status row: [shape] [status text]
            var row = new GameObject("StatusRow", typeof(RectTransform));
            row.transform.SetParent(statusText.transform.parent, false);
            row.transform.SetSiblingIndex(statusText.transform.GetSiblingIndex());
            var hl = row.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter; hl.spacing = 6f;
            hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var ig = new GameObject("Shape", typeof(RectTransform));
            ig.transform.SetParent(row.transform, false);
            statusShape = ig.AddComponent<UnityEngine.UI.Image>();
            statusShape.raycastTarget = false; statusShape.preserveAspect = true;
            var le = ig.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredWidth = le.preferredHeight = 22f; le.minWidth = le.minHeight = 22f;
            statusText.transform.SetParent(row.transform, false);
            // hint line
            hintText = Instantiate(statusText, statusText.transform.parent.parent, false);
            hintText.name = "Hint";
            hintText.transform.SetAsLastSibling();
            hintText.fontSize = statusText.fontSize * 0.85f;
            hintText.color = UiPalette.Muted;
            hintText.gameObject.SetActive(false);
        }

        static string HintFor(BuildingDef def, string type)
        {
            if (def == null) return null;
            if (type == "center") return "Click: upgrades  ·  Right-click: feed the job";
            if (def.ending) return "Click: Ascend to Immortality";
            if (def.roster.enabled) return "Click: disciples  ·  Right-click: feed";
            if (def.IsConverter) return "Click: recipe  ·  Right-click: feed";
            if (def.bridge.enabled) return "Click: pairing  ·  Right-click: load";
            if (def.lantern.enabled) return "Click: edit links";
            if (type == "storehouse" || def.seal.enabled || def.gather.enabled) return "Click: take  ·  Right-click: store";
            return null;
        }

        void Place(Building b, string area, BuildingDef def)
        {
            var cam = Camera.main;
            if (cam == null || rootRt == null || canvas == null) return;
            int cell = runner.Space.Cell;
            float w = def != null ? def.sizeW : 1;
            float top = 0f;
            var view = views != null ? views.Find(area, b.id) : null;
            if (view != null) top = view.ArtTop;
            var world = runner.Space.PxToWorld(area, b.col * cell, b.row * cell) + new Vector3(w * 0.5f, top + 0.15f, 0f);
            // canvas-local maths (like RecipePickerView): works for Overlay and Screen Space-Camera canvases
            var rc = canvas.rootCanvas;
            var crt = (RectTransform)rc.transform;
            var ucam = rc.renderMode == RenderMode.ScreenSpaceOverlay ? null : rc.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(crt, cam.WorldToScreenPoint(world), ucam, out var local);
            var prt = (RectTransform)panel.transform;
            Vector2 size = prt.rect.size;
            var half = crt.rect.size * 0.5f;
            const float margin = 8f, bottomBar = 70f;
            float x = Mathf.Clamp(local.x, -half.x + size.x * 0.5f + margin, half.x - size.x * 0.5f - margin);
            float y = local.y + 6f;
            if (y + size.y > half.y - margin)
            {
                // top edge off screen: hang below the building's footprint instead
                var below = runner.Space.PxToWorld(area, b.col * cell, (b.row + (def != null ? def.sizeH : 1)) * cell) + new Vector3(w * 0.5f, -0.15f, 0f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(crt, cam.WorldToScreenPoint(below), ucam, out var lb);
                y = lb.y - size.y - 6f;
            }
            y = Mathf.Clamp(y, -half.y + bottomBar, half.y - size.y - margin);
            AnchorScreen = new Vector2(x, y);
            rootRt.position = crt.TransformPoint(new Vector3(x, y, 0f));
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null || controller == null || panel == null) return;
            var b = controller.Hovered;
            bool show = b != null && b.built && runner.IsUnlocked(controller.HoveredArea) && !controller.AnyPanelOpen;
            if (panel.activeSelf != show) panel.SetActive(show);
            if (!show) { lastBuilding = null; return; }
            BuildExtras();
            if (b != lastBuilding) hoverStart = Time.unscaledTime;
            var sim = runner.Sim;
            var def = runner.Config.Building(b.type);
            string nm = def != null ? def.name : b.type;
            if (b.type == "dragon" && runner.State.won) nm = "Awakened Dragon";
            if (nameText.text != nm) nameText.text = nm;

            bool has = sim.BuildingStatus(controller.HoveredArea, b, stBuf);
            double cpm = 0;
            if (has && stBuf.state == BuildingState.Working && def != null && def.IsConverter
                && sim.ConverterFace(controller.HoveredArea, b, faceBuf)) cpm = faceBuf.craftsPerMin;
            bool dwell = Time.unscaledTime - hoverStart >= DwellSec;
            if (hintText != null)
            {
                string hint = dwell ? HintFor(def, b.type) : null;
                bool hasHint = !string.IsNullOrEmpty(hint);
                if (hintText.gameObject.activeSelf != hasHint) hintText.gameObject.SetActive(hasHint);
                if (hasHint && hintText.text != hint) hintText.text = hint;
            }
            Place(b, controller.HoveredArea, def);
            if (b == lastBuilding && has == lastHas && (!has || (stBuf.state == lastState && stBuf.label == lastLabel)) && cpm == lastCpm) return;
            lastBuilding = b; lastHas = has; lastState = has ? stBuf.state : (BuildingState)(-1); lastLabel = has ? stBuf.label : null; lastCpm = cpm;
            string line = null; Color c = UiPalette.Muted;
            if (has)
            {
                line = stBuf.label;
                switch (stBuf.state)
                {
                    case BuildingState.Working:
                        c = UiPalette.Accent;
                        if (cpm > 0) line += " · " + ViewKit.Fmt(cpm) + "/min";
                        break;
                    case BuildingState.Starved:
                    case BuildingState.NoFuel: c = UiPalette.Danger; break;
                    case BuildingState.Full: c = UiPalette.Amber; break;
                    default: c = UiPalette.Muted; break;
                }
            }
            bool hasLine = !string.IsNullOrEmpty(line);
            if (statusText.gameObject.activeSelf != hasLine) statusText.gameObject.SetActive(hasLine);
            if (hasLine) { if (statusText.text != line) statusText.text = line; statusText.color = c; }
            if (statusShape != null)
            {
                var row = statusShape.transform.parent.gameObject;
                if (row.activeSelf != hasLine) row.SetActive(hasLine);
                statusShape.sprite = PixelSprites.ForState(has ? stBuf.state : (BuildingState?)null);
            }
        }
    }
}
