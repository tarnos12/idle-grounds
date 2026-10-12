using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Coordinates the M7 modals (main.js boot flow + ui.js modal priorities):
    /// <list type="bullet">
    /// <item>Boot: an intro-unseen game → the welcome modal's intro shape (no offline catch-up, ADR 0002).</item>
    /// <item>The ending (ADR 0007): when the Heavenly Gate is completed — or a built one is clicked
    ///   (<see cref="AskAscend"/>) — the confirm dialog offers "Ascend to Immortality"; the ending card itself is
    ///   <see cref="DragonDialogView"/> (pull-based on <see cref="Simulation.EndingPending"/>). Free play continues.</item>
    /// <item>Esc chain (BuildController.Escape: confirm, then the world panels, then these): stats → help → welcome.</item>
    /// <item>RunReset (in-place load): closes the world panels (BuildController) and cancels hand holds.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class MetaUiController : MonoBehaviour
    {
        public const string AscendTitle = "Ascend to Immortality?";
        public const string AscendText = "The ending plays once - then your grounds stay as they are and free play continues.";
        public const string AscendAgainTitle = "Watch the ending again?";
        public const string AscendAgainText = "The Heavenly Gate plays the ending once more. Your grounds stay as they are.";

        [SerializeField] GameRunner runner;
        [SerializeField] BuildController build;
        [SerializeField] HelpModalView help;
        [SerializeField] StatsPanelView stats;
        [SerializeField] WelcomeModalView welcome;
        [SerializeField] ToastView toast;
        [SerializeField] ConfirmDialogView confirm;

        public static MetaUiController Instance { get; private set; }

        public HelpModalView Help => help;
        public StatsPanelView Stats => stats;
        public WelcomeModalView Welcome => welcome;
        public ToastView Toast => toast;
        public ConfirmDialogView Confirm => confirm;

        public bool AnyOpen =>
            (confirm != null && confirm.IsOpen) || (help != null && help.IsOpen) ||
            (stats != null && stats.IsOpen) || (welcome != null && welcome.IsOpen);

        Simulation subscribed;

        void Awake()
        {
            Instance = this;
            if (runner == null) runner = GameRunner.Instance;
            if (build == null) build = FindFirstObjectByType<BuildController>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        void Start()
        {
            EnforceLayerOrder();
            if (runner == null || runner.Sim == null) return;
            subscribed = runner.Sim;
            subscribed.Events.RunReset += OnRunReset;
            subscribed.Events.HeavenlyGateRaised += OnGateRaised;
            if (!runner.State.introSeen) welcome.ShowIntro();
        }

        /// <summary>
        /// Canvas sibling order, tail last (VP-05): HUD panels and the bottom bar first, then every modal (so each modal's
        /// scrim dims and blocks the bar and the quest panel), then the toast, then the hand chip.
        /// </summary>
        void EnforceLayerOrder()
        {
            var canvas = welcome != null ? welcome.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return;
            var root = canvas.transform;
            void Last(Component c) { if (c != null && c.transform.parent == root) c.transform.SetAsLastSibling(); }
            if (build != null) Last(build.UpgradeTree);
            Last(help); Last(stats); Last(welcome);
            Last(FindFirstObjectByType<DragonDialogView>());
            Last(confirm); Last(toast);
            var cursor = root.Find("HandCursor");
            if (cursor != null) cursor.SetAsLastSibling();
        }

        void Unsubscribe()
        {
            if (subscribed == null) return;
            subscribed.Events.RunReset -= OnRunReset;
            subscribed.Events.HeavenlyGateRaised -= OnGateRaised;
            subscribed = null;
        }

        void OnRunReset()
        {
            if (build != null) { build.CloseBuildMenu(); build.CloseBuildingPanels(); build.CancelModes(); }
            if (build != null && build.UpgradeTreeOpen) build.UpgradeTree.Close();
            var hand = FindFirstObjectByType<HandController>();
            if (hand != null) hand.CancelHolds();
        }

        void OnGateRaised(string area, Building gate) => AskAscend();

        /// <summary>The built Heavenly Gate was clicked / completed: offer "Ascend to Immortality" (the ending).</summary>
        public void AskAscend()
        {
            if (runner == null || runner.Sim == null || confirm == null || !runner.Sim.CanAscendToImmortality) return;
            var sim = runner.Sim;
            confirm.Ask(sim.State.ascended ? AscendAgainTitle : AscendTitle, sim.State.ascended ? AscendAgainText : AscendText, "Ascend to Immortality", () =>
            {
                if (sim.AscendToImmortality() && SaveService.Instance != null) SaveService.Instance.Save();
            });
        }

        /// <summary>Esc on the confirm dialog (checked before every other layer). True = consumed.</summary>
        public bool EscapeConfirm() => confirm != null && confirm.Escape();

        /// <summary>Esc for the M7 modals in ui.js order: stats → help → welcome. True = consumed.</summary>
        public bool Escape()
        {
            if (EscapeConfirm()) return true;
            if (stats != null && stats.Escape()) return true;
            if (help != null && help.Escape()) return true;
            if (welcome != null && welcome.Escape()) return true;
            return false;
        }
    }
}
