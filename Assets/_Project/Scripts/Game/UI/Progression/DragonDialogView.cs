using IdleGrounds.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Dragon story modal + awakening ending card (ui-input-render §4.13, ui.js syncDragonDialog /
    /// maybeShowEnding). Pull-based: shows while State.dragon.dialog is set (purple border, dragon
    /// sprite 56 px, the stage text, "Continue"); after the dialog, once, when
    /// <see cref="Simulation.EndingPending"/> the gold "The Dragon Awakens" card with stats.
    /// Continue / Esc dismisses (<see cref="Simulation.DismissDragonDialog"/> / MarkEndingSeen).
    /// </summary>
    public class DragonDialogView : MonoBehaviour
    {
        const string EndingText = "The grounds are complete. The Sleeping Dragon stirs, unfurls, and rises to watch over all you have " +
                                  "raised. In gratitude it grants a permanent blessing - the whole world quickens by roughly 11%, forever.";

        [SerializeField] GameRunner runner;
        [SerializeField] GameObject modal;
        [SerializeField] Image box;
        [SerializeField] Outline border;
        [SerializeField] Image icon;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI bodyText;
        [SerializeField] TextMeshProUGUI statsText;
        [SerializeField] Button continueButton;
        [SerializeField] Sprite sleepingSprite;
        [SerializeField] Sprite awakeSprite;

        public enum Mode { Closed, Story, Ending }
        public Mode State { get; private set; } = Mode.Closed;
        public bool IsOpen => State != Mode.Closed;
        public string Body => bodyText != null ? bodyText.text : null;

        string shownText;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (continueButton != null) continueButton.onClick.AddListener(() => Continue());
            if (modal != null) modal.SetActive(false);
        }

        /// <summary>Continue / Esc. True = consumed.</summary>
        public bool Continue()
        {
            if (!IsOpen) return false;
            if (State == Mode.Story) runner.Sim.DismissDragonDialog();
            else runner.Sim.MarkEndingSeen();
            Hide();
            return true;
        }

        void Hide()
        {
            State = Mode.Closed; shownText = null;
            if (modal != null) modal.SetActive(false);
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null) return;
            var s = runner.State;
            if (s.dragon.dialog != null)
            {
                if (State != Mode.Story || shownText != s.dragon.dialog) ShowStory(s.dragon.dialog);
            }
            else if (runner.Sim.EndingPending)
            {
                if (State != Mode.Ending) ShowEnding();
            }
            else if (IsOpen) Hide();
        }

        void ShowStory(string text)
        {
            State = Mode.Story; shownText = text;
            modal.SetActive(true);
            border.effectColor = UiPalette.Purple;
            icon.sprite = sleepingSprite;
            titleText.gameObject.SetActive(false);
            bodyText.text = UiText.StripEmoji(text);
            statsText.gameObject.SetActive(false);
        }

        void ShowEnding()
        {
            State = Mode.Ending; shownText = null;
            modal.SetActive(true);
            border.effectColor = UiPalette.Gold;
            icon.sprite = awakeSprite;
            titleText.gameObject.SetActive(true);
            titleText.text = "The Dragon Awakens";
            bodyText.text = EndingText;
            var s = runner.State;
            var gate = runner.Config.Building("ascension_gate");
            int tal = 3, star = 3;
            if (gate != null) foreach (var c in gate.cost) { if (c.item == "talisman") tal = c.qty; if (c.item == "star_steel") star = c.qty; }
            double playMs = s.stats.started > 0 ? runner.SimNow - s.stats.started : 0;
            statsText.gameObject.SetActive(true);
            statsText.text =
                "<color=#fbbf24>The dragon now sheds Dragon Scales - with " + tal + " Talismans and " + star +
                " Star Steel, raise the Ascension Gate (B).</color>\n\n" +
                "Ascensions  <b>" + s.ascensions + "</b>\nTotal crafted  <b>" + s.stats.totalCrafted + "</b>\n" +
                "Playtime  <b>" + FmtAway(playMs) + "</b>";
        }

        /// <summary>ui.js fmtAway: "Xh Ym" / "Ym" / "Xs".</summary>
        public static string FmtAway(double ms)
        {
            long s = (long)(ms / 1000);
            if (s >= 3600) return (s / 3600) + "h " + (s % 3600 / 60) + "m";
            if (s >= 60) return (s / 60) + "m";
            return s + "s";
        }
    }
}
