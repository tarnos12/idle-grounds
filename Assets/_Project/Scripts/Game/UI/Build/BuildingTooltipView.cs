using IdleGrounds.Sim;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Hover name (spec §3.3) + status: the building under the cursor, bottom-centre above the bar —
    /// name 800, then the <see cref="Simulation.BuildingStatus"/> line (green working, red starved / no
    /// fuel, amber full, muted idle); a ghost shows "Under construction". Hidden while the build menu or
    /// recipe picker is up, or while placing.
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

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (panel != null) panel.SetActive(false);
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null || controller == null || panel == null) return;
            var b = controller.Hovered;
            bool show = b != null && !controller.AnyPanelOpen && controller.Placing == null;
            if (panel.activeSelf != show) panel.SetActive(show);
            if (!show) return;
            var sim = runner.Sim;
            var def = runner.Config.Building(b.type);
            string nm = def != null ? def.name : b.type;
            if (b.type == "dragon" && runner.State.won) nm = "Awakened Dragon";
            if (nameText.text != nm) nameText.text = nm;

            string line = null; Color c = UiPalette.Muted;
            if (!b.built) { line = "Under construction"; c = UiPalette.Gold; }
            else
            {
                var st = sim.BuildingStatus(controller.HoveredArea, b);
                if (st != null)
                {
                    line = st.label;
                    switch (st.state)
                    {
                        case BuildingState.Working:
                            c = UiPalette.Accent;
                            var f = def != null && def.IsConverter ? sim.ConverterFace(controller.HoveredArea, b) : null;
                            if (f != null && f.craftsPerMin > 0) line += " · " + ViewKit.Fmt(f.craftsPerMin) + "/min";
                            break;
                        case BuildingState.Starved:
                        case BuildingState.NoFuel: c = UiPalette.Danger; break;
                        case BuildingState.Full: c = UiPalette.Amber; break;
                        default: c = UiPalette.Muted; break;
                    }
                }
            }
            bool hasLine = !string.IsNullOrEmpty(line);
            if (statusText.gameObject.activeSelf != hasLine) statusText.gameObject.SetActive(hasLine);
            if (hasLine) { if (statusText.text != line) statusText.text = line; statusText.color = c; }
        }
    }
}
