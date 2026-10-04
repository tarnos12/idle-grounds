using System.Text;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Meditation Pavilion roster strip (ui-input-render §4.5, ui.js renderRoster): left-click a built
    /// pavilion → "Meditation Pavilion", "Disciples d/cap  buns/foodCap", hint "Each cultivates X while
    /// fed Y (feed by hand or wisp).", and "Recruit" costing one recruit item (Robe) from the hand —
    /// disabled with the reason (<see cref="Simulation.RecruitReason"/>: "Pavilion is full" /
    /// "Carry a Robe to recruit"). One building panel at a time (BuildController).
    /// </summary>
    public class PavilionPanelView : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] GameObject panel;
        [SerializeField] Image icon;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI disciplesText;
        [SerializeField] Image foodIcon;
        [SerializeField] TextMeshProUGUI foodText;
        [SerializeField] TextMeshProUGUI hintText;
        [SerializeField] Button recruitButton;
        [SerializeField] TextMeshProUGUI recruitLabel;
        [SerializeField] Image recruitItemIcon;
        [SerializeField] TextMeshProUGUI reasonText;
        [SerializeField] Button closeButton;

        SpriteCache sprites;
        public bool IsOpen => panel != null && panel.activeSelf;
        public string Area { get; private set; }
        public int BuildingId { get; private set; }
        public string Reason { get; private set; }
        public bool RecruitEnabled => recruitButton != null && recruitButton.interactable;

        Simulation Sim => runner.Sim;
        Building Pavilion => runner.State.Area(Area)?.BuildingById(BuildingId);

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (panel != null) panel.SetActive(false);
            if (recruitButton != null) recruitButton.onClick.AddListener(() => Recruit());
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        public void Open(string area, Building b)
        {
            if (b == null || !b.built) return;
            sprites ??= new SpriteCache(runner.Database, null);
            Area = area; BuildingId = b.id;
            panel.SetActive(true);
            var def = runner.Config.Building(b.type);
            icon.sprite = sprites.Building(b.type);
            titleText.text = def != null ? def.name : b.type;
            Refresh();
        }

        public void Close() { if (panel != null) panel.SetActive(false); }

        public bool Recruit()
        {
            if (!IsOpen) return false;
            bool ok = Sim.RecruitDisciple(Area, BuildingId);
            Refresh();
            return ok;
        }

        string ItemName(string it) => runner.Config.Item(it)?.name ?? it;

        void LateUpdate()
        {
            if (!IsOpen || runner == null || runner.Sim == null) return;
            var b = Pavilion;
            if (b == null || !b.built) { Close(); return; }
            Refresh();
        }

        void Refresh()
        {
            var b = Pavilion;
            if (b == null) return;
            var r = runner.Config.Building(b.type).roster;
            int cap = Sim.RosterCap(b);
            int foodCap = r.foodCap > 0 ? r.foodCap : 20;
            ViewKit.SetText(disciplesText, "Disciples " + b.disciples + "/" + cap);
            if (foodIcon != null) foodIcon.sprite = sprites.Item(r.food ?? "spirit_buns");
            ViewKit.SetText(foodText, b.buns + "/" + foodCap);
            if (foodText != null) foodText.color = b.buns > 0 ? UiPalette.Text : UiPalette.Danger;
            var sb = new StringBuilder("Each cultivates ").Append(ItemName(r.produce)).Append(" while fed ");
            for (int i = 0; i < r.foodValues.Count; i++) { if (i > 0) sb.Append(" / "); sb.Append(ItemName(r.foodValues[i].item)); }
            if (r.foodValues.Count == 0) sb.Append(ItemName(r.food));
            sb.Append(" (feed by hand or wisp).");
            ViewKit.SetText(hintText, UiText.StripEmoji(sb.ToString()));
            if (recruitItemIcon != null) recruitItemIcon.sprite = sprites.Item(r.recruit);
            ViewKit.SetText(recruitLabel, "Recruit (1 " + ItemName(r.recruit) + ")");
            string why = Sim.RecruitReason(Area, BuildingId);
            Reason = why == null ? null : why == "Full" ? "Pavilion is full" : why.StartsWith("Needs") ? "Carry a " + ItemName(r.recruit) + " to recruit" : why;
            recruitButton.interactable = why == null;
            ViewKit.SetText(reasonText, Reason ?? "");
        }
    }
}
