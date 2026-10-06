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

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null || controller == null || panel == null) return;
            var b = controller.Hovered;
            bool show = b != null && b.built && runner.IsUnlocked(controller.HoveredArea) && !controller.AnyPanelOpen;
            if (panel.activeSelf != show) panel.SetActive(show);
            if (!show) { lastBuilding = null; return; }
            var sim = runner.Sim;
            var def = runner.Config.Building(b.type);
            string nm = def != null ? def.name : b.type;
            if (b.type == "dragon" && runner.State.won) nm = "Awakened Dragon";
            if (nameText.text != nm) nameText.text = nm;

            bool has = sim.BuildingStatus(controller.HoveredArea, b, stBuf);
            double cpm = 0;
            if (has && stBuf.state == BuildingState.Working && def != null && def.IsConverter
                && sim.ConverterFace(controller.HoveredArea, b, faceBuf)) cpm = faceBuf.craftsPerMin;
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
        }
    }
}
