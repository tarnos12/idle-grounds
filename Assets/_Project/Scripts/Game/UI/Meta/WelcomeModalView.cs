using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The #welcome-modal of the original (main.js intro): the first-run Intro shape only — seedling,
    /// "Welcome to Idle Grounds". The offline progress / summary shapes were removed with offline
    /// progress (ADR 0002); their serialized widgets (moon sprite, progress bar, Skip, gains, "why")
    /// stay wired by MetaBuilder but are never shown. Continue marks the intro seen and saves.
    /// </summary>
    public class WelcomeModalView : ModalView
    {
        public enum Shape { Intro }

        [SerializeField] Image icon;
        // legacy offline-modal widgets (ADR 0002): kept serialized so the scene / MetaBuilder wiring stays valid
#pragma warning disable 414, 649
        [SerializeField] Sprite moonSprite;
        [SerializeField] Sprite introSprite;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI awayText;
        [SerializeField] GameObject progressGroup;
        [SerializeField] Image progressFill;
        [SerializeField] TextMeshProUGUI progressLabel;
        [SerializeField] Button skipButton;
        [SerializeField] TextMeshProUGUI skipLabel;
        [SerializeField] RectTransform gainsRoot;
        [SerializeField] UiIconLine gainTemplate;
        [SerializeField] TextMeshProUGUI noneText;
        [SerializeField] GameObject whyGroup;
        [SerializeField] RectTransform whyRoot;
        [SerializeField] TextMeshProUGUI whyTemplate;
#pragma warning restore 414, 649
        [SerializeField] Button continueButton;

        public Shape Current { get; private set; }
        public string AwayLine => awayText != null ? awayText.text : null;

        protected override void Awake()
        {
            base.Awake();
            if (continueButton != null) continueButton.onClick.AddListener(Continue);
        }

        void ClearLists()
        {
            if (gainsRoot != null) gainsRoot.gameObject.SetActive(false);
            noneText.gameObject.SetActive(false);
            whyGroup.SetActive(false);
        }

        // ------------------------------------------------------------------ shapes

        public void ShowIntro()
        {
            Current = Shape.Intro;
            icon.sprite = introSprite;
            titleText.text = "Welcome to Idle Grounds";
            awayText.text = "These are your cultivation grounds — tend them and awaken the Sleeping Dragon.";
            progressGroup.SetActive(false);
            ClearLists();
            noneText.gameObject.SetActive(true);
            noneText.text = "Follow the Quests panel (top-right) for what to do next, and open Help anytime. Left-click to gather · right-click to feed buildings · WASD to look around.";
            continueButton.gameObject.SetActive(true);
            Open();
        }

        // ------------------------------------------------------------------ dismiss

        /// <summary>Continue: close (never while the replay runs), mark the intro seen, save.</summary>
        public void Continue()
        {
            if (!IsOpen) return;
            base.Close();
            if (Sim != null) Sim.MarkIntroSeen();
            if (SaveService.Instance != null) SaveService.Instance.Save();
        }

        public override void Close() => Continue();

        public override bool Escape()
        {
            if (!IsOpen) return false;
            // dismissWelcome (ui.js:2762): just hides — only the Continue button marks the intro seen
            base.Close();
            return true;
        }
    }
}
