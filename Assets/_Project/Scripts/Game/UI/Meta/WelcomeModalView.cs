using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The #welcome-modal of the original (main.js intro): the first-run intro only — seedling,
    /// "Welcome to Idle Grounds", a short how-to and Continue. (Offline progress and its summary were
    /// removed — ADR 0002.) Continue marks the intro seen and saves.
    /// </summary>
    public class WelcomeModalView : ModalView
    {
        [SerializeField] Image icon;
        [SerializeField] Sprite introSprite;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI bodyText;
        [SerializeField] TextMeshProUGUI hintText;
        [SerializeField] Button continueButton;

        public string BodyLine => bodyText != null ? bodyText.text : null;

        protected override void Awake()
        {
            base.Awake();
            if (continueButton != null) continueButton.onClick.AddListener(Continue);
        }

        public void ShowIntro()
        {
            if (icon != null && introSprite != null) icon.sprite = introSprite;
            titleText.text = "Welcome to Idle Grounds";
            bodyText.text = "These are your cultivation grounds — tend them and awaken the Sleeping Dragon.";
            hintText.text = "Follow the Quests panel (top-right) for what to do next, and open Help anytime. Left-click to gather · right-click to feed buildings · WASD to look around.";
            continueButton.gameObject.SetActive(true);
            Open();
        }

        /// <summary>Continue: close, mark the intro seen, save.</summary>
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
