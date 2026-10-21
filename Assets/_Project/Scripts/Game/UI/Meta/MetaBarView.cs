using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// M7 parts of the bottom bar (ui-input-render §4.1): the mute and reduce-motion toggles (persisted, "click" on
    /// unmute) and the Help / Stats / Reset buttons (Reset → confirm "Reset ALL progress and start over?" → wipe +
    /// reload). The ascension Shrine pill and tag were removed with ascension (ADR 0007).
    /// </summary>
    public class MetaBarView : MonoBehaviour
    {
        public const string ResetText = "Start a new game? Your save is deleted and the new run starts with no buildings placed.";
        public const string NewGameTitle = "New game?", NewGameButton = "New game";

        [SerializeField] GameRunner runner;
        [SerializeField] Button helpButton;
        [SerializeField] Button statsButton;
        [SerializeField] Button resetButton;
        [SerializeField] Button muteButton;
        [SerializeField] TextMeshProUGUI muteLabel;
        [SerializeField] Button motionButton;
        [SerializeField] TextMeshProUGUI motionLabel;
        [SerializeField] HelpModalView help;
        [SerializeField] StatsPanelView stats;
        [SerializeField] ConfirmDialogView confirm;


        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (helpButton != null) helpButton.onClick.AddListener(() => help.Open());
            if (statsButton != null) statsButton.onClick.AddListener(() => stats.Open());
            if (resetButton != null) { resetButton.interactable = true; resetButton.onClick.AddListener(AskReset); }
            if (muteButton != null) muteButton.onClick.AddListener(ToggleMute);
            if (motionButton != null) motionButton.onClick.AddListener(ToggleMotion);
        }

        void Start()
        {
            PaintMute();
            PaintMotion();
            TooltipTrigger.Attach(motionButton, "Reduce motion: no screen shake, squash or flight tweens, no ambient motes");
            TooltipTrigger.Attach(helpButton, "How to play");
            TooltipTrigger.Attach(statsButton, "Your progress so far");
            TooltipTrigger.Attach(resetButton, "New game: wipe the save and start again with no buildings placed");
            if (resetButton != null) { var rl = resetButton.transform.Find("Label")?.GetComponent<TextMeshProUGUI>(); if (rl != null) rl.text = "New game"; }
            TooltipTrigger.Attach(muteButton, "Mute / unmute sound");
        }

        public void AskReset()
        {
            confirm.Ask(NewGameTitle, ResetText, NewGameButton, () => { if (SaveService.Instance != null) SaveService.Instance.ResetAll(); });
        }

        public void ToggleMute()
        {
            var a = AudioService.Instance;
            if (a == null) return;
            a.SetMuted(!a.Muted);
            PaintMute();
            if (!a.Muted) AudioService.Play("click");
            if (BottomBarView.Instance != null) BottomBarView.Instance.PaintToggleIcons();
        }

        public void ToggleMotion()
        {
            Juice.SetReduceMotion(!Juice.ReduceMotion);
            PaintMotion();
            AudioService.Play("click");
            if (BottomBarView.Instance != null) BottomBarView.Instance.PaintToggleIcons();
        }

        void PaintMotion()
        {
            bool calm = Juice.ReduceMotion;
            if (motionLabel != null) motionLabel.text = calm ? "Calm" : "Motion";
            if (motionButton == null) return;
            var skin = motionButton.GetComponent<UiButtonSkin>();   // delivered art: swap sprite instead of tinting
            if (skin != null && skin.SetToggle(calm ? UiToggleState.On : UiToggleState.Off)) return;
            if (motionButton.targetGraphic is Image img)
                img.color = calm ? UiPalette.AccentDk : UiPalette.Panel;
        }

        void PaintMute()
        {
            var a = AudioService.Instance;
            if (muteLabel != null) muteLabel.text = a != null && a.Muted ? "Muted" : "Sound";
            if (muteButton == null) return;
            bool muted = a != null && a.Muted;
            var skin = muteButton.GetComponent<UiButtonSkin>();
            if (skin != null && skin.SetToggle(muted ? UiToggleState.Danger : UiToggleState.Off)) return;
            if (muteButton.targetGraphic is Image img)
                img.color = muted ? new Color(0.45f, 0.12f, 0.12f) : UiPalette.Panel;
        }
    }
}
