using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Coordinates the M7 modals (main.js boot flow + ui.js modal priorities):
    /// <list type="bullet">
    /// <item>Boot: Toast tier → toast "Welcome back — +N item(s) while away" (when N &gt; 0); Full tier → the
    ///   welcome modal's progress shape, then the summary when <see cref="GameRunner.ReplayFinished"/> fires;
    ///   tier None on a fresh, intro-unseen run → the intro shape.</item>
    /// <item>Per frame: the ascend dialog follows State.ascendPrompt and the post-ascension card follows
    ///   State.justAscended — both held back while the welcome modal / replay is up.</item>
    /// <item>Esc chain (BuildController.Escape: confirm, then the world panels, then these): perk shop → ascend →
    ///   stats → help → welcome → post-ascension card (ui.js order).</item>
    /// <item>RunReset (ascension): closes the world panels (BuildController) and cancels hand holds.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class MetaUiController : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] BuildController build;
        [SerializeField] HelpModalView help;
        [SerializeField] StatsPanelView stats;
        [SerializeField] PerkShopView perkShop;
        [SerializeField] AscendDialogView ascend;
        [SerializeField] PostAscensionCardView postAscension;
        [SerializeField] WelcomeModalView welcome;
        [SerializeField] ToastView toast;
        [SerializeField] ConfirmDialogView confirm;

        public static MetaUiController Instance { get; private set; }

        public HelpModalView Help => help;
        public StatsPanelView Stats => stats;
        public PerkShopView PerkShop => perkShop;
        public AscendDialogView Ascend => ascend;
        public PostAscensionCardView PostAscension => postAscension;
        public WelcomeModalView Welcome => welcome;
        public ToastView Toast => toast;
        public ConfirmDialogView Confirm => confirm;

        public bool AnyOpen =>
            (confirm != null && confirm.IsOpen) || (perkShop != null && perkShop.IsOpen) || (help != null && help.IsOpen) ||
            (stats != null && stats.IsOpen) || (postAscension != null && postAscension.IsOpen) ||
            (ascend != null && ascend.IsOpen) || (welcome != null && welcome.IsOpen);

        void Awake()
        {
            Instance = this;
            if (runner == null) runner = GameRunner.Instance;
            if (build == null) build = FindFirstObjectByType<BuildController>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (runner != null)
            {
                runner.ReplayFinished -= OnReplayFinished;
                if (runner.Sim != null) runner.Sim.Events.RunReset -= OnRunReset;
            }
        }

        void Start()
        {
            if (runner == null || runner.Sim == null) return;
            runner.ReplayFinished += OnReplayFinished;
            runner.Sim.Events.RunReset += OnRunReset;
            var boot = runner.BootResult;
            if (boot == null) return;
            switch (boot.tier)
            {
                case OfflineTier.Full:
                    if (runner.Replaying) welcome.ShowProgress(runner.ReplayJob);
                    else OnReplayFinished(runner.ReplaySummary);
                    break;
                case OfflineTier.Toast:
                    ShowToastFor(boot.summary);
                    break;
                default:
                    if (!runner.State.introSeen) welcome.ShowIntro();
                    break;
            }
        }

        void ShowToastFor(OfflineSummary summary)
        {
            if (summary == null || toast == null) return;
            int n = summary.gained.entries.FindAll(e => e.qty > 0).ConvertAll(e => e.qty).Sum();
            if (n > 0) toast.Show("Welcome back — +" + n + " item" + (n == 1 ? "" : "s") + " while away");
        }

        void OnReplayFinished(OfflineSummary summary) => welcome.ShowSummary(summary);

        void OnRunReset()
        {
            if (build != null) { build.CloseBuildMenu(); build.CloseBuildingPanels(); build.CancelModes(); }
            if (build != null && build.UpgradeTreeOpen) build.UpgradeTree.Close();
            var hand = FindFirstObjectByType<HandController>();
            if (hand != null) hand.CancelHolds();
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null) return;
            bool blocked = runner.Replaying || welcome.IsOpen;
            postAscension.Sync(!blocked && runner.State.justAscended != null);
            ascend.Sync(!blocked && runner.State.ascendPrompt);
        }

        /// <summary>Esc on the confirm dialog (checked before every other layer). True = consumed.</summary>
        public bool EscapeConfirm() => confirm != null && confirm.Escape();

        /// <summary>Esc for the M7 modals in ui.js order: perk → ascend → stats → help → welcome → ending (post-ascension card). True = consumed.</summary>
        public bool Escape()
        {
            if (EscapeConfirm()) return true;
            if (perkShop != null && perkShop.Escape()) return true;
            if (ascend != null && ascend.Escape()) return true;
            if (stats != null && stats.Escape()) return true;
            if (help != null && help.Escape()) return true;
            if (welcome != null && welcome.Escape()) return true;
            if (postAscension != null && postAscension.Escape()) return true;
            return false;
        }
    }

    static class IntListExt
    {
        public static int Sum(this System.Collections.Generic.List<int> l) { int s = 0; foreach (var v in l) s += v; return s; }
    }
}
