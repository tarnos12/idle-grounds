using System.Collections.Generic;
using System.Linq;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The #welcome-modal of the original (ui-input-render §4.12, ui.js showOfflineProgress /
    /// showOfflineSummary, main.js intro): three shapes —
    /// <list type="bullet">
    /// <item>Intro (first run, no catch-up): seedling, "Welcome to Idle Grounds".</item>
    /// <item>Progress (Full tier): moon, "You were away …", 12 px accent bar "Catching up… N%", Skip
    ///   ("Skip — forfeit the last Xh Ym" / "Skip — output has levelled off" / "Stopping…"); cannot be dismissed.</item>
    /// <item>Summary: away line variants, gains chips sorted by qty (or "Nothing new was produced…"),
    ///   "Why it stopped" rows (stallRowsHTML).</item>
    /// </list>
    /// Continue marks the intro seen and saves.
    /// </summary>
    public class WelcomeModalView : ModalView
    {
        public enum Shape { Intro, Progress, Summary }

        [SerializeField] Image icon;
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
        [SerializeField] Button continueButton;

        public Shape Current { get; private set; }
        public string AwayLine => awayText != null ? awayText.text : null;
        public int GainCount { get; private set; }
        public int WhyCount { get; private set; }
        readonly List<UiIconLine> gains = new List<UiIconLine>();
        readonly List<TextMeshProUGUI> whys = new List<TextMeshProUGUI>();
        bool skipping;
        SpriteCache sprites;

        protected override void Awake()
        {
            base.Awake();
            if (skipButton != null) skipButton.onClick.AddListener(Skip);
            if (continueButton != null) continueButton.onClick.AddListener(Continue);
        }

        SpriteCache Sprites => sprites ??= new SpriteCache(runner.Database, null);

        void ClearLists()
        {
            foreach (var g in gains) g.gameObject.SetActive(false);
            foreach (var w in whys) w.gameObject.SetActive(false);
            noneText.gameObject.SetActive(false);
            whyGroup.SetActive(false);
            GainCount = WhyCount = 0;
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

        public void ShowProgress(OfflineJob job)
        {
            Current = Shape.Progress;
            icon.sprite = moonSprite;
            titleText.text = "Welcome back";
            awayText.text = job.resumed
                ? "You were away " + MetaText.FmtAway(job.awayMs) + ". Finishing your interrupted catch-up…"
                : "You were away " + MetaText.FmtAway(job.awayMs) + ". Catching up on what the grounds made…";
            ClearLists();
            progressGroup.SetActive(true);
            continueButton.gameObject.SetActive(false);
            skipping = false;
            skipButton.interactable = true;
            UpdateProgress(job);
            Open();
        }

        /// <summary>`updateOfflineProgress(job)` — between replay slices.</summary>
        public void UpdateProgress(OfflineJob job)
        {
            if (job == null) return;
            double f = OfflineReplay.Progress(job);
            int pct = (int)System.Math.Floor(f * 100);
            progressFill.fillAmount = pct / 100f;
            progressLabel.text = "Catching up… " + pct + "%";
            if (!skipping)
            {
                double left = System.Math.Max(0, job.end - job.virt);
                skipLabel.text = OfflineReplay.Levelled(job) ? "Skip — output has levelled off" : "Skip — forfeit the last " + MetaText.FmtAway(left);
            }
        }

        public void Skip()
        {
            if (Current != Shape.Progress || skipping) return;
            skipping = true;
            skipButton.interactable = false;
            skipLabel.text = "Stopping…";
            runner.SkipReplay();
        }

        void LateUpdate()
        {
            if (IsOpen && Current == Shape.Progress && runner.Replaying) UpdateProgress(runner.ReplayJob);
        }

        /// <summary>`showOfflineSummary(summary)` for the modal tier (the toast tier is handled by the controller).</summary>
        public void ShowSummary(OfflineSummary summary)
        {
            Current = Shape.Summary;
            icon.sprite = moonSprite;
            titleText.text = "Welcome back";
            progressGroup.SetActive(false);
            continueButton.gameObject.SetActive(true);
            ClearLists();
            if (summary == null)
            {
                awayText.text = "The catch-up couldn't finish — your grounds are as you left them.";
                Open();
                return;
            }
            awayText.text = AwayText(summary);
            var cfg = runner.Config;
            var list = summary.gained.entries.Where(e => e.qty > 0).OrderByDescending(e => e.qty).ToList();
            if (list.Count > 0)
            {
                while (gains.Count < list.Count) gains.Add(Instantiate(gainTemplate, gainsRoot));
                for (int i = 0; i < list.Count; i++)
                {
                    gains[i].gameObject.SetActive(true);
                    gains[i].Set(Sprites.Item(list[i].item), "<b>+" + list[i].qty + "</b> " + MetaText.ItemName(cfg, list[i].item), UiPalette.Text);
                }
                GainCount = list.Count;
            }
            else
            {
                noneText.gameObject.SetActive(true);
                noneText.text = "Nothing new was produced — set up generators, converters or disciples to gather while you're gone.";
            }
            var rows = StallRows(cfg, summary);
            if (rows.Count > 0)
            {
                whyGroup.SetActive(true);
                while (whys.Count < rows.Count) whys.Add(Instantiate(whyTemplate, whyRoot));
                for (int i = 0; i < rows.Count; i++) { whys[i].gameObject.SetActive(true); whys[i].text = rows[i]; }
                WhyCount = rows.Count;
            }
            Open();
        }

        public static string AwayText(OfflineSummary s)
        {
            double away = s.awayMs > 0 ? s.awayMs : s.elapsedMs;
            string line = "You were away " + MetaText.FmtAway(away);
            if (s.capped) line += " — the grounds work for up to " + MetaText.FmtAway(s.capMs > 0 ? s.capMs : s.elapsedMs) + " while you're gone";
            if (s.resumed) line += " (your catch-up was interrupted and has now finished)";
            if (s.failed) line += ". The catch-up hit an error after " + MetaText.FmtAway(s.simulatedMs) + " and stopped early — the rest couldn't be credited:";
            else if (s.saturatedMs > 0) line += ". The grounds kept working until everything was saturated:";
            else if (s.skippedMs > 0 && s.levelled) line += ". You skipped the catch-up after " + MetaText.FmtAway(s.simulatedMs) + "; output had already levelled off, so little was lost:";
            else if (s.skippedMs > 0) line += ". You skipped the catch-up after " + MetaText.FmtAway(s.simulatedMs) + "; the rest was forfeited:";
            else line += ". The grounds kept working:";
            return line;
        }

        /// <summary>`stallRowsHTML(summary)` — "Why it stopped" rows.</summary>
        public static List<string> StallRows(GameConfig cfg, OfflineSummary summary)
        {
            var rows = new List<string>();
            string Area(string k) => MetaText.RegionName(cfg, k);
            string Names(OfflineStall s) => string.Join(", ", s.names);
            if (summary.saturatedMs > 0)
                rows.Add("Output levelled off after about <b>" + MetaText.FmtAway(summary.flatAtMs ?? 0) + "</b> — saturated: nothing more would have been produced in the remaining " +
                         MetaText.FmtAway(System.Math.Max(0, summary.elapsedMs - (summary.flatAtMs ?? 0))) + ", so the catch-up stopped there (nothing was forfeited).");
            else if (summary.plateauMs != null)
                rows.Add("Output levelled off after about <b>" + MetaText.FmtAway(summary.plateauMs.Value) + "</b> — the rest of the time added little.");
            var full = summary.stalls.Where(s => s.kind == "ground").Select(s => Area(s.area)).ToList();
            if (full.Count > 0)
                rows.Add("Ground full in <b>" + string.Join(", ", full) + "</b>: the oldest loose raw items were cleared. Gathering Stones feeding Storehouses keep it clear.");
            foreach (var s in summary.stalls)
            {
                switch (s.kind)
                {
                    case "autoskip":
                        rows.Add("<b>" + Area(s.area) + "</b>: bots skipped " + Names(s) + " — plenty already lay loose. Gathering Stones + Storehouses clear it."); break;
                    case "outfull":
                        rows.Add("<b>" + Area(s.area) + "</b>: " + Names(s) + " stopped — " + (s.count > 1 ? "their output piles are" : "its output pile is") + " full. A Gathering Stone beside it hauls products away."); break;
                    case "nofuel":
                        rows.Add("<b>" + Area(s.area) + "</b>: " + (s.count > 1 ? s.count + " burners" : "a burner") + " (" + Names(s) + ") ran out of fuel — a Furnace Spirit keeps racks stoked."); break;
                    case "nobuns":
                        rows.Add("<b>" + Area(s.area) + "</b>: " + Names(s) + " ran out of food, so the disciples stopped cultivating — link food to it."); break;
                    case "stonefull":
                        rows.Add("<b>" + Area(s.area) + "</b>: " + s.count + " " + (s.count > 1 ? "Gathering Stones are" : "Gathering Stone is") + " full — link a Wisp Lantern to haul from " + (s.count > 1 ? "them" : "it") + "."); break;
                }
            }
            return rows;
        }

        // ------------------------------------------------------------------ dismiss

        /// <summary>Continue: close (never while the replay runs), mark the intro seen, save.</summary>
        public void Continue()
        {
            if (runner.Replaying || !IsOpen) return;
            base.Close();
            if (Sim != null) Sim.MarkIntroSeen();
            if (SaveService.Instance != null) SaveService.Instance.Save();
        }

        public override void Close() => Continue();

        public override bool Escape()
        {
            if (!IsOpen) return false;
            Continue();
            return true;        // swallowed even while replaying
        }
    }
}
