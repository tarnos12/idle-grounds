using System.Collections.Generic;
using System.Text;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Quest panel + milestone tracker (ui-input-render §4.10, ui.js renderQuestPanelInner /
    /// milestoneInfo): fixed top-right. Collapsed = quest chip (+ alert when claimable). Expanded:
    /// "Quest i/N" + minimise; quest icon + name; description; "Unlocks:" reward preview; progress bar;
    /// "cur/need" + Claim (hand-bound rewards float "+n Name to hand" at 35% of the view height);
    /// "Next: …" faded; then the "Next milestone" block from <see cref="Simulation.Milestone"/>
    /// (dragon tribute / open Celestial / raise the Heavenly Gate / ascend / free play with have/need rows + source hints,
    /// next step, hungry-disciples hint). Rebuilt only when its key string changes.
    /// </summary>
    public class QuestPanelView : MonoBehaviour
    {
        const float RefreshEvery = 0.2f;

        [SerializeField] GameRunner runner;
        [SerializeField] FxService fx;
        [SerializeField] Camera worldCamera;
        [Header("collapsed")]
        [SerializeField] Button chip;
        [SerializeField] GameObject chipAlert;
        [Header("expanded")]
        [SerializeField] GameObject panel;
        [SerializeField] TextMeshProUGUI headerText;
        [SerializeField] Image headerIcon;
        [SerializeField] Button minButton;
        [SerializeField] GameObject questBlock;
        [SerializeField] UiIconLine nameLine;
        [SerializeField] TextMeshProUGUI descText;
        [SerializeField] RectTransform unlocksRow;
        [SerializeField] UiIconLine unlockTemplate;
        [SerializeField] Image barFill;
        [SerializeField] TextMeshProUGUI progText;
        [SerializeField] Button claimButton;
        [SerializeField] TextMeshProUGUI claimLabel;
        [Tooltip("Dims the Claim button until the quest is ready.")]
        [SerializeField] CanvasGroup claimGroup;
        [Tooltip("Pulsing gold ring on the Claim button while it is ready (static under Calm).")]
        [SerializeField] Outline claimGlow;
        [SerializeField] UiIconLine nextLine;
        [Header("milestone")]
        [SerializeField] GameObject msBlock;
        [SerializeField] GameObject msHeader;
        [SerializeField] RectTransform msLines;
        [SerializeField] UiIconLine msLineTemplate;
        [SerializeField] GameObject msBar;
        [SerializeField] Image msBarFill;
        [Header("sprites")]
        [SerializeField] Sprite questsSprite;
        [SerializeField] Sprite targetSprite;
        [SerializeField] Sprite dragonSprite;
        [SerializeField] Sprite ascendSprite;

        readonly List<UiIconLine> unlockPool = new List<UiIconLine>();
        readonly List<UiIconLine> msPool = new List<UiIconLine>();
        readonly List<(Sprite s, string t, Color c)> msItems = new List<(Sprite, string, Color)>();
        SpriteCache sprites;
        string lastKey;
        float nextRefresh;

        public string Key => lastKey;
        public bool Collapsed => runner != null && runner.State != null && runner.State.quest.hidden;
        public bool ClaimInteractable => claimButton != null && claimButton.interactable;
        /// <summary>Active quest description (+ "Next step" hint line) as shown.</summary>
        public string DescriptionText => descText != null ? descText.text : null;
        public IEnumerable<string> MilestoneLines { get { foreach (var l in msPool) if (l.gameObject.activeSelf) yield return l.text.text; } }

        Simulation Sim => runner.Sim;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (unlockTemplate != null) unlockTemplate.gameObject.SetActive(false);
            if (msLineTemplate != null) msLineTemplate.gameObject.SetActive(false);
            if (chip != null) chip.onClick.AddListener(() => SetCollapsed(false));
            if (minButton != null) minButton.onClick.AddListener(() => SetCollapsed(true));
            if (claimButton != null) claimButton.onClick.AddListener(() => Claim());
        }

        void Start()
        {
            if (fx == null) fx = FindFirstObjectByType<FxService>();
            if (worldCamera == null) worldCamera = Camera.main;
            sprites = new SpriteCache(runner.Database, null);
        }

        public void SetCollapsed(bool hidden)
        {
            Sim.SetQuestPanelHidden(hidden);
            Rebuild(true);
        }

        /// <summary>Claim button: claim + reward floaters. Null when not claimable.</summary>
        public QuestClaim Claim()
        {
            var res = Sim.ClaimQuest();
            if (res == null) return null;
            if (fx != null && worldCamera != null)
            {
                var top = worldCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.65f, -worldCamera.transform.position.z));
                float dy = 0f;
                foreach (var e in res.toHand)
                {
                    fx.Floater(top - new Vector3(0f, dy, 0f), "+" + e.qty + " " + ItemName(e.item) + " to hand", FxService.Gold, sprites.Item(e.item));
                    dy += ViewKit.U(22f);
                }
            }
            if (UiJuice.Instance != null && !Juice.ReduceMotion)
            {
                var celebrate = panel != null && panel.activeInHierarchy ? panel.transform as RectTransform : chip != null ? chip.transform as RectTransform : null;
                UiJuice.Instance.Sparkle(celebrate, 26);
            }
            Rebuild(true);
            return res;
        }

        // ---- stele vs quest panel: fade the panel while an unlock stele sits under it (so its cost label stays
        //      readable); hovering the panel itself restores it so it can still be used.
        UnlockSignSync signs;
        CanvasGroup fadeGroup;
        readonly Vector3[] corners = new Vector3[4];

        void FadeOverSteles()
        {
            if (fadeGroup == null) { if (!TryGetComponent(out fadeGroup)) fadeGroup = gameObject.AddComponent<CanvasGroup>(); }
            if (ModalScrim.Any)      // VP-05: nothing of the quest panel peeks out beside an open modal
            {
                fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, 0f, Time.unscaledDeltaTime * 12f);
                fadeGroup.blocksRaycasts = false;
                return;
            }
            fadeGroup.blocksRaycasts = true;
            if (signs == null) signs = FindFirstObjectByType<UnlockSignSync>();
            if (worldCamera == null || signs == null) return;
            var target = panel != null && panel.activeSelf ? (RectTransform)panel.transform : chip != null ? (RectTransform)chip.transform : null;
            if (target == null) return;
            target.GetWorldCorners(corners);
            var canvas = target.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            var b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            var ui = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            bool over = false;
            foreach (var s in signs.Signs)
            {
                var r = s.WorldRect;
                var p0 = worldCamera.WorldToScreenPoint(new Vector3(r.xMin, r.yMin, 0f));
                var p1 = worldCamera.WorldToScreenPoint(new Vector3(r.xMax, r.yMax, 0f));
                if (ui.Overlaps(Rect.MinMaxRect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y), Mathf.Max(p0.x, p1.x), Mathf.Max(p0.y, p1.y)))) { over = true; break; }
            }
            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool pointerOnPanel = mouse != null && ui.Contains(mouse.position.ReadValue());
            float goal = over && !pointerOnPanel ? 0.18f : 1f;
            fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, goal, Time.unscaledDeltaTime * 6f);
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null || sprites == null) return;
            FadeOverSteles();
            PulseClaim();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshEvery;
            Rebuild(false);
        }

        void PulseClaim()
        {
            if (claimGlow == null) return;
            bool ready = claimButton != null && claimButton.interactable && claimButton.isActiveAndEnabled;
            if (claimGlow.enabled != ready) claimGlow.enabled = ready;
            if (!ready) return;
            float a = Juice.ReduceMotion ? 0.7f : 0.45f + 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
            claimGlow.effectColor = new Color(UiPalette.Gold.r, UiPalette.Gold.g, UiPalette.Gold.b, a);
        }

        string ItemName(string it) => runner.Config.Item(it)?.name ?? it;
        string RegionName(string k) => runner.Config.Region(k)?.name ?? k;
        string BuildingName(string t) => runner.Config.Building(t)?.name ?? t;

        public void Rebuild(bool force)
        {
            var s = runner.State;
            var gq = s.quest;
            var quests = runner.Config.quests;
            int i = gq.idx, total = quests.Count;
            var p = i < total ? Sim.QuestProgress(i) : null;
            BuildMilestone(gq.hidden);
            string hint = !gq.hidden && i < total ? Sim.QuestHint() : null;
            var kb = new StringBuilder();
            kb.Append(gq.hidden).Append('|').Append(i).Append('|').Append(p != null ? p.cur + "/" + p.need + "/" + p.done : "end").Append('|');
            kb.Append(hint).Append('|');
            foreach (var m in msItems) kb.Append(m.t).Append(';');
            kb.Append(msBarValue);
            string key = kb.ToString();
            if (!force && key == lastKey) return;
            lastKey = key;

            // collapsed chip
            chip.gameObject.SetActive(gq.hidden);
            if (chipAlert != null) chipAlert.SetActive(p != null && p.done);
            panel.SetActive(!gq.hidden);
            if (gq.hidden) return;

            bool chainDone = i >= total;
            questBlock.SetActive(!chainDone);
            headerIcon.sprite = chainDone ? targetSprite : questsSprite;
            headerText.text = chainDone ? "Next milestone" : "Quest " + (i + 1) + "/" + total;
            if (msHeader != null) msHeader.SetActive(!chainDone);
            if (!chainDone)
            {
                var q = quests[i];
                nameLine.Set(runner.Database.QuestIcon(q.id), q.name, UiPalette.Gold);       // the current quest heading in gold
                descText.text = UiText.StripEmoji(q.desc) +
                                (string.IsNullOrEmpty(hint) ? "" : "\n<color=#" + ColorUtility.ToHtmlStringRGB(UiPalette.Accent) + ">Next step: " + UiText.StripEmoji(hint) + "</color>");
                FillUnlocks(i);
                UiBar.Set(barFill, p.need > 0 ? Mathf.Clamp01(p.cur / (float)p.need) : 0f);
                if (barFill != null && barFill.transform.parent != null)
                    barFill.transform.parent.gameObject.SetActive(false);       // the "cur/need" text already says it (VP-24)
                progText.text = p.cur + "/" + p.need;
                claimButton.interactable = p.done;
                claimLabel.text = p.done ? "Claim!" : "Claim";
                claimLabel.color = p.done ? UiPalette.Gold : UiPalette.Muted;
                if (claimGroup != null) claimGroup.alpha = p.done ? 1f : 0.55f;
                var nq = i + 1 < total ? quests[i + 1] : null;
                nextLine.gameObject.SetActive(nq != null);
                if (nq != null) nextLine.Set(runner.Database.QuestIcon(nq.id), "Next: " + nq.name, new Color(UiPalette.Text.r, UiPalette.Text.g, UiPalette.Text.b, 0.6f));
            }
            FillMilestone();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel.transform);
        }

        void FillUnlocks(int i)
        {
            var (reveals, items) = Sim.QuestRewardPreview(i);
            int n = reveals.Count + items.Count;
            unlocksRow.gameObject.SetActive(n > 0);
            while (unlockPool.Count < n)
            {
                var l = Instantiate(unlockTemplate, unlocksRow);
                unlockPool.Add(l);
            }
            int k = 0;
            foreach (var b in reveals) unlockPool[k++].Set(sprites.Building(b.key), "", UiPalette.Gold);
            foreach (var e in items) unlockPool[k++].Set(sprites.Item(e.item), "+" + e.qty, UiPalette.Gold);
            for (int j = 0; j < unlockPool.Count; j++)
            {
                bool on = j < n;
                if (unlockPool[j].gameObject.activeSelf != on) unlockPool[j].gameObject.SetActive(on);
                if (on && unlockPool[j].text != null) unlockPool[j].text.gameObject.SetActive(unlockPool[j].text.text.Length > 0);
            }
        }

        int msBarAt = -1;
        float msBarValue = -1f;

        void Add(Sprite s, string t, Color c) => msItems.Add((s, t, c));

        void BuildMilestone(bool hidden)
        {
            msItems.Clear(); msBarAt = -1; msBarValue = -1f;
            if (hidden) return;
            var m = Sim.Milestone();
            if (m == null) return;
            switch (m.kind)
            {
                case MilestoneKind.DragonTribute:
                    Add(dragonSprite, "<b>Dragon tribute " + m.tributeNumber + "/" + m.tributeTotal + "</b>", UiPalette.Text);
                    Add(null, "Right-click the dragon to feed it:", UiPalette.Muted);
                    NeedRows(m);
                    msBarAt = msItems.Count; msBarValue = (float)m.tributeProgress;
                    break;
                case MilestoneKind.OpenIsland:
                    Add(sprites.Building("heavenly_gate"), "<b>Open " + RegionName(m.island) + "</b>", UiPalette.Text);
                    Add(null, "The Gate can only stand there. Pay its unlock stele:", UiPalette.Muted);
                    NeedRows(m);
                    break;
                case MilestoneKind.RaiseGate:
                    Add(sprites.Building("heavenly_gate"), "<b>Raise the Heavenly Gate</b>", UiPalette.Text);
                    Add(null, m.gatePlaced ? "Feed its ghost (right-click):" : "Build it there (B), then feed it:", UiPalette.Muted);
                    NeedRows(m);
                    if (m.step != null && !string.IsNullOrEmpty(m.step.text)) Add(null, "Next step: " + m.step.text, UiPalette.Accent);
                    break;
                case MilestoneKind.Ascend:
                    Add(ascendSprite, "<b>Ascend to Immortality</b>", UiPalette.Gold);
                    Add(null, "Click the Heavenly Gate to play the ending.", UiPalette.Muted);
                    break;
                case MilestoneKind.FreePlay:
                    Add(ascendSprite, "<b>Free play</b>", UiPalette.Gold);
                    Add(null, "The ending is behind you. The grounds are yours - keep building.", UiPalette.Muted);
                    break;
            }
            if (m.hungryDisciples)
                Add(sprites.Item("spirit_buns"), "Disciples eat Spirit Buns - build a Mill (Rice Flour to Spirit Buns) or a Brewery (Spirit Wine) for more.", UiPalette.Text);
        }

        void NeedRows(MilestoneInfo m)
        {
            foreach (var r in m.needs)
            {
                string t = "<b>" + ItemName(r.item) + "</b> " + r.have + "/" + r.need +
                           (string.IsNullOrEmpty(r.sourceHint) ? "" : " <color=#94a3b8>- " + UiText.Ascii(r.sourceHint) + "</color>");
                Add(sprites.Item(r.item), t, r.Ok ? UiPalette.Accent : UiPalette.Text);
            }
        }

        void FillMilestone()
        {
            msBlock.SetActive(msItems.Count > 0);
            while (msPool.Count < msItems.Count)
            {
                var l = Instantiate(msLineTemplate, msLines);
                msPool.Add(l);
            }
            for (int j = 0; j < msPool.Count; j++)
            {
                bool on = j < msItems.Count;
                if (msPool[j].gameObject.activeSelf != on) msPool[j].gameObject.SetActive(on);
                if (!on) continue;
                msPool[j].Set(msItems[j].s, UiText.Ascii(msItems[j].t), msItems[j].c);
                msPool[j].transform.SetSiblingIndex(j);
            }
            bool bar = msBarAt >= 0;
            msBar.SetActive(bar);
            if (bar)
            {
                msBar.transform.SetSiblingIndex(msBarAt);
                UiBar.Set(msBarFill, Mathf.Clamp01(msBarValue));
            }
        }
    }
}
