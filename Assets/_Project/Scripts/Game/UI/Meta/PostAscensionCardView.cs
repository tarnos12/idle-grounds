using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One-time "Ascension N complete" card (ui.js showAscendedCard) while State.justAscended is set:
    /// "+AP · world speed ×a → ×b", vows of the run, head starts, a Shrine hint; "Open Shrine" /
    /// "Begin run N+1". Dismissing clears justAscended and saves.
    /// </summary>
    public class PostAscensionCardView : ModalView
    {
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI bodyText;
        [SerializeField] Button shrineButton;
        [SerializeField] Button beginButton;
        [SerializeField] TextMeshProUGUI beginLabel;
        [SerializeField] PerkShopView perkShop;

        public string Body => bodyText != null ? bodyText.text : null;

        protected override void Awake()
        {
            base.Awake();
            if (shrineButton != null) shrineButton.onClick.AddListener(() => { Dismiss(); if (perkShop != null) perkShop.Open(); });
            if (beginButton != null) beginButton.onClick.AddListener(Dismiss);
        }

        public void Sync(bool show)
        {
            var ja = Sim?.State.justAscended;
            if (!show || ja == null) { if (IsOpen) base.Close(); return; }
            if (IsOpen) return;
            var vs = MetaText.ActiveVows(Sim);
            titleText.text = "Ascension " + ja.n + " complete";
            bodyText.text = "<b>+" + ja.ap + " AP</b>  ·  world speed <b>×" + ja.speedFrom.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " → ×" + ja.speedTo.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "</b>" +
                            (vs.Count > 0 ? "\n\nVows this run: " + string.Join(", ", vs.Select(v => v.name)) : "") +
                            "\n\n" + MetaText.HeadStarts(Sim) +
                            "\n\nSpend your Ascension Points at the Shrine before you begin — every perk applies at once.";
            beginLabel.text = "Begin run " + (ja.n + 1);
            base.Open();
        }

        public void Dismiss()
        {
            if (Sim != null) Sim.ClearJustAscended();
            if (SaveService.Instance != null) SaveService.Instance.Save();
            base.Close();
        }

        public override void Close() => Dismiss();
    }
}
