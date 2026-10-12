using IdleGrounds.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Dragon story modal + the ending card (ui-input-render §4.13, ui.js syncDragonDialog /
    /// maybeShowEnding). Pull-based: shows while State.dragon.dialog is set (purple border, dragon
    /// sprite 56 px, the stage text, "Continue"); after the dialog, once, when
    /// <see cref="Simulation.EndingPending"/> (the player chose "Ascend to Immortality" at the Heavenly Gate,
    /// ADR 0007) the gold "Ascended to Immortality" card with stats; free play continues after it.
    /// Continue / Esc dismisses (<see cref="Simulation.DismissDragonDialog"/> / MarkEndingSeen).
    /// </summary>
    public class DragonDialogView : MonoBehaviour
    {
        const string EndingText = "You step through the Heavenly Gate. Talisman, star steel and dragon scale hum as one, the awakened " +
                                  "Dragon circles the peak, and you rise beyond the clouds - an Immortal. The grounds you raised remain, " +
                                  "and they are yours to keep tending.";

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
            titleText.text = "Ascended to Immortality";
            bodyText.text = EndingText;
            var s = runner.State;
            double playMs = s.stats.started > 0 ? runner.SimNow - s.stats.started : 0;
            int islands = 0;
            foreach (var r in runner.Config.regions) if (s.world.IsUnlocked(r.key)) islands++;
            statsText.gameObject.SetActive(true);
            statsText.text =
                "<color=#fbbf24>The End - free play continues.</color>\n\n" +
                "Islands  <b>" + islands + "/" + runner.Config.regions.Count + "</b>\nBuildings built  <b>" + s.stats.buildingsBuilt + "</b>\n" +
                "Total crafted  <b>" + s.stats.totalCrafted + "</b>\nPlaytime  <b>" + FmtAway(playMs) + "</b>";
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
