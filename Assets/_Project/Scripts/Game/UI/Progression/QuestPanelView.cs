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
    /// (spend-AP line, dragon tribute / raise the Gate / ascend with have/need rows + source hints,
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
            Rebuild(true);
            return res;
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null || sprites == null) return;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshEvery;
            Rebuild(false);
        }

        string ItemName(string it) => runner.Config.Item(it)?.name ?? it;
        string BuildingName(string t) => runner.Config.Building(t)?.name ?? t;

        public void Rebuild(bool force)
        {
            var s = runner.State;
            var gq = s.quest;
            var quests = runner.Config.quests;
            int i = gq.idx, total = quests.Count;
            var p = i < total ? Sim.QuestProgress(i) : null;
            BuildMilestone(gq.hidden);
            var kb = new StringBuilder();
            kb.Append(gq.hidden).Append('|').Append(i).Append('|').Append(p != null ? p.cur + "/" + p.need + "/" + p.done : "end").Append('|');
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
                nameLine.Set(runner.Database.QuestIcon(q.id), q.name, UiPalette.Text);
                descText.text = UiText.StripEmoji(q.desc);
                FillUnlocks(i);
                barFill.fillAmount = p.need > 0 ? Mathf.Clamp01(p.cur / (float)p.need) : 0f;
                progText.text = p.cur + "/" + p.need;
                claimButton.interactable = p.done;
                claimLabel.text = p.done ? "Claim!" : "Claim";
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
            if (m.spendAp) Add(ascendSprite, "Spend " + m.ap + " AP at the Ascension Shrine (bottom bar)", UiPalette.Gold);
            switch (m.kind)
            {
                case MilestoneKind.DragonTribute:
                    Add(dragonSprite, "<b>Dragon tribute " + m.tributeNumber + "/" + m.tributeTotal + "</b>", UiPalette.Text);
                    Add(null, "Right-click the dragon to feed it. In hand / still needed:", UiPalette.Muted);
                    NeedRows(m);
                    msBarAt = msItems.Count; msBarValue = (float)m.tributeProgress;
                    break;
                case MilestoneKind.RaiseGate:
                    Add(sprites.Building("ascension_gate"), "<b>Raise the Ascension Gate</b>", UiPalette.Text);
                    Add(null, m.gatePlaced ? "Feed its ghost (right-click). In hand / still needed:" : "Build it (B), then feed it. In hand / cost:", UiPalette.Muted);
                    NeedRows(m);
                    if (m.step != null && !string.IsNullOrEmpty(m.step.text)) Add(null, "Next step: " + m.step.text, UiPalette.Accent);
                    break;
                case MilestoneKind.Ascend:
                    Add(ascendSprite, "<b>Ascend for +" + m.ascendReward + " AP</b>", UiPalette.Gold);
                    var off = m.offerings;
                    if (m.allRegionsOpen)
                    {
                        Add(null, "Click the Ascension Gate to ascend - every Island is open.", UiPalette.Muted);
                        if (off != null) Add(null, "Offerings " + off.count + "/" + off.cap + (off.count >= off.cap ? "" : " - right-click spares onto the Gate (+1 AP each)"), UiPalette.Text);
                    }
                    else
                        Add(null, "Click the Ascension Gate to ascend - each Island unlocked (+2 AP) or gate offering (" +
                                  (off != null ? off.count + "/" + off.cap : "0") + ") = more Ascension Points.", UiPalette.Muted);
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
                           (string.IsNullOrEmpty(r.sourceHint) ? "" : " <color=#94a3b8>- " + r.sourceHint + "</color>");
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
                msPool[j].Set(msItems[j].s, msItems[j].t, msItems[j].c);
                msPool[j].transform.SetSiblingIndex(j);
            }
            bool bar = msBarAt >= 0;
            msBar.SetActive(bar);
            if (bar)
            {
                msBar.transform.SetSiblingIndex(msBarAt);
                msBarFill.fillAmount = Mathf.Clamp01(msBarValue);
            }
        }
    }
}
