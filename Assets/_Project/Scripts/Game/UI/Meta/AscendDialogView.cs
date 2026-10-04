using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Ascension Gate dialog (ui-input-render §4.9, ui.js syncAscendModal): shown while State.ascendPrompt
    /// (gate completed, or a click on the built gate). Dynamic text: AP reward, world speed now → next,
    /// dragon tribute multiplier, gate offerings, kept vows (AP ×m), ascensions so far; static KEEP / RESET
    /// columns; after ≥ 1 ascension a vow picker for the next run. Buttons: Ascend (confirm → GameRunner.Ascend
    /// with the ticked vows), Keep playing (closes the prompt), See perks (shop preview on top).
    /// </summary>
    public class AscendDialogView : ModalView
    {
        public const string ConfirmText = "Ascend and begin the grounds anew? (+20% world speed per ascension, kept forever)";

        [SerializeField] TextMeshProUGUI countText;
        [SerializeField] GameObject vowsBox;
        [SerializeField] TextMeshProUGUI vowsHint;
        [SerializeField] RectTransform vowsRoot;
        [SerializeField] VowRowView vowTemplate;
        [SerializeField] Button ascendButton;
        [SerializeField] Button laterButton;
        [SerializeField] Button perksButton;
        [SerializeField] ConfirmDialogView confirm;
        [SerializeField] PerkShopView perkShop;

        /// <summary>Vows ticked for the NEXT run (UI-only, cleared after ascending).</summary>
        public readonly HashSet<string> VowPick = new HashSet<string>();
        readonly List<VowRowView> vowRows = new List<VowRowView>();
        string shownKey;

        public string CountText => countText != null ? countText.text : null;
        public bool VowsShown => vowsBox != null && vowsBox.activeSelf;

        protected override void Awake()
        {
            base.Awake();
            if (ascendButton != null) ascendButton.onClick.AddListener(AskAscend);
            if (laterButton != null) laterButton.onClick.AddListener(KeepPlaying);
            if (perksButton != null) perksButton.onClick.AddListener(() => { if (perkShop != null) perkShop.Open(); });
        }

        /// <summary>Driven by <see cref="MetaUiController"/>: show = State.ascendPrompt and nothing blocks it.</summary>
        public void Sync(bool show)
        {
            if (!show) { if (IsOpen) { base.Close(); shownKey = null; } return; }
            if (!IsOpen) { BuildVows(); base.Open(); }
            Refresh();
        }

        void Refresh()
        {
            var sim = Sim; var s = sim.State;
            int asc = s.ascensions, n = sim.AscendReward();
            double now = sim.Prestige.WorldSpeed, next = sim.Prestige.NextWorldSpeed;
            var (_, gate) = sim.Progression.BuiltGate();
            var off = gate != null ? sim.GateOfferings(gate) : null;
            var kept = MetaText.ActiveVows(sim);
            string html = "Ascending grants <b>" + n + " ☯</b> (Ascension Point" + (n == 1 ? "" : "s") + ").\n" +
                "World speed <b>×" + now.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " → ×" + next.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "</b> (machines, nature, wisps, foxes and your own hands)." +
                "\nDragon tributes <b>×" + sim.Dragon.TributeMult(asc).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " → ×" + sim.Dragon.TributeMult(asc + 1).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "</b> (the dragon remembers you)." +
                (off != null ? "\n" + MetaText.Offerings(sim.Config, off) + (off.count >= off.cap ? "" :
                    " (+" + off.count + " ☯) — right-click spare Talismans, Star Steel and Dragon Scales onto the Gate.") : "") +
                (kept.Count > 0 ? "\nVows kept this run: " + string.Join(", ", kept.Select(v => v.name)) + " — ☯ ×" + sim.Progression.VowMult().ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "." : "") +
                "\n(Ascended " + asc + " time" + (asc == 1 ? "" : "s") + " so far.)";
            if (html != shownKey) { shownKey = html; countText.text = html; }
        }

        void BuildVows()
        {
            var sim = Sim;
            bool show = sim.Prestige.CanChooseVows && sim.Config.vows.Count > 0;
            vowsBox.SetActive(show);
            if (!show) return;
            var mult = sim.Config.balance.vowMult;
            vowsHint.text = "A harder run pays more: ☯ ×" + string.Join(" / ×", mult.Skip(1).Select(m => m.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))) +
                            " for 1–4 vows kept to the next ascension. A vow's first completion leaves a permanent mark (timers ×0.96).";
            var vows = sim.Config.vows;
            while (vowRows.Count < vows.Count)
            {
                var r = Instantiate(vowTemplate, vowsRoot);
                r.gameObject.SetActive(true);
                r.toggle.onValueChanged.AddListener(on => { if (on) VowPick.Add(r.vowId); else VowPick.Remove(r.vowId); });
                vowRows.Add(r);
            }
            for (int i = 0; i < vowRows.Count; i++)
            {
                var r = vowRows[i];
                bool on = i < vows.Count;
                r.gameObject.SetActive(on);
                if (!on) continue;
                var v = vows[i];
                r.vowId = v.id;
                r.icon.sprite = runner.Database.VowIcon(v.id);
                bool marked = sim.State.vows.done.Get(v.id) > 0;
                r.label.text = "<b>" + UiText.StripEmoji(v.name) + "</b> — " + UiText.StripEmoji(v.desc) +
                               (marked ? " <color=" + MetaText.Accent + ">(marked)</color>" : "");
                r.toggle.SetIsOnWithoutNotify(VowPick.Contains(v.id));
            }
        }

        public void AskAscend()
        {
            if (confirm != null) confirm.Ask(ConfirmText, "Ascend", DoAscend);
            else DoAscend();
        }

        /// <summary>Ascend now with the ticked vows (no confirm).</summary>
        public void DoAscend()
        {
            var vows = VowPick.ToList();
            VowPick.Clear();
            base.Close();
            shownKey = null;
            if (perkShop != null && perkShop.IsOpen) perkShop.Close();
            runner.Ascend(vows);
        }

        public void KeepPlaying()
        {
            if (Sim != null) Sim.SetAscendPrompt(false);
            base.Close();
            shownKey = null;
        }

        public override void Close() => KeepPlaying();
    }
}
