using System.Collections.Generic;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Stats panel (ui-input-render §4.14, ui.js openStats): an Overview tab of two-column rows and a Flow tab —
    /// the per-item flow ledger (produced / consumed / lost, net per minute over the last ~5 min), this run or
    /// lifetime, sortable. Refreshed once a second while open.
    /// </summary>
    public class StatsPanelView : ModalView
    {
        enum Sort { NetPerMin, NetTotal, Name }

        [SerializeField] RectTransform rowsRoot;
        [SerializeField] UiTextRow rowTemplate;
        [Header("Flow tab")]
        [SerializeField] Button overviewTab;
        [SerializeField] Button flowTab;
        [SerializeField] GameObject flowRoot;
        [SerializeField] Button scopeButton;
        [SerializeField] Button sortButton;
        [SerializeField] TextMeshProUGUI scopeLabel;
        [SerializeField] TextMeshProUGUI sortLabel;
        [SerializeField] TextMeshProUGUI emptyText;
        [SerializeField] RectTransform flowContent;
        [SerializeField] FlowRowView flowRowTemplate;

        readonly List<UiTextRow> rows = new List<UiTextRow>();
        readonly List<FlowRowView> flowRows = new List<FlowRowView>();
        readonly List<FlowEntry> sorted = new List<FlowEntry>();
        SpriteCache sprites;
        float nextRefresh;
        bool showFlow, lifetime;
        Sort sort = Sort.NetPerMin;
        FlowLedger sortLedger;

        protected override void Awake()
        {
            base.Awake();
            BuildFitLayout();
            if (overviewTab != null) overviewTab.onClick.AddListener(() => SetTab(false));
            if (flowTab != null) flowTab.onClick.AddListener(() => SetTab(true));
            if (scopeButton != null) scopeButton.onClick.AddListener(() => { lifetime = !lifetime; Refresh(); });
            if (sortButton != null) sortButton.onClick.AddListener(() => { sort = (Sort)(((int)sort + 1) % 3); Refresh(); });
        }

        public override void Open()
        {
            if (Sim == null) return;
            if (modal != null) modal.SetActive(true);
            Refresh();
            base.Open();
        }

        void SetTab(bool flow) { showFlow = flow; Refresh(); FitToScreen(); }

        /// <summary>Test / tooling hook: switch tab and scope.</summary>
        public void Show(bool flow, bool lifetimeTotals) { showFlow = flow; lifetime = lifetimeTotals; if (IsOpen) { Refresh(); FitToScreen(); } }

        // ---- fit (UI QA batch B #3 / #4): fixed header + tabs, a scrolling body of one shared height, number columns
        //      sized to the snapped pixel font, the Flow note on its own line under the Scope / Sort buttons

        /// <summary>Design height of the tab body (both tabs: switching never makes the window jump).</summary>
        public const float BodyHeight = 570f, MinBodyHeight = 150f, DesignWidth = 720f;
        /// <summary>The note under the Flow filter buttons.</summary>
        public const string FlowNote = "Net = change per minute over the last ~5 min";
        GameObject overviewRoot;
        LayoutElement overviewLe, flowLe, flowScrollLe;
        float bodyH = BodyHeight, columnsAt = -1f;
        /// <summary>Current body height (canvas units) after fitting the screen.</summary>
        public float BodyNow => bodyH;
        public ScrollRect OverviewScroll { get; private set; }

        void BuildFitLayout()
        {
            if (Box == null || rowsRoot == null) return;
            // Overview rows -> a ScrollRect (like the Flow list): a short phone screen scrolls the rows instead of pushing
            // the title, tabs and close button off the top
            var area = new GameObject("RowsScroll", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(rowsRoot.parent, false);
            area.SetSiblingIndex(rowsRoot.GetSiblingIndex());
            overviewLe = area.gameObject.AddComponent<LayoutElement>();
            overviewLe.minHeight = overviewLe.preferredHeight = BodyHeight;
            var vp = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            vp.SetParent(area, false);
            vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one; vp.offsetMin = vp.offsetMax = Vector2.zero;
            var vimg = vp.gameObject.AddComponent<Image>(); vimg.color = new Color(0f, 0f, 0f, 0f); vimg.raycastTarget = true;
            vp.gameObject.AddComponent<RectMask2D>();
            rowsRoot.SetParent(vp, false);
            rowsRoot.anchorMin = new Vector2(0f, 1f); rowsRoot.anchorMax = new Vector2(1f, 1f); rowsRoot.pivot = new Vector2(0.5f, 1f);
            rowsRoot.anchoredPosition = Vector2.zero; rowsRoot.sizeDelta = Vector2.zero;
            var rle = rowsRoot.GetComponent<LayoutElement>();
            if (rle != null) rle.minHeight = -1f;
            var vl = rowsRoot.GetComponent<VerticalLayoutGroup>();
            if (vl != null) vl.padding = new RectOffset(vl.padding.left, Mathf.Max(vl.padding.right, 26), vl.padding.top, Mathf.Max(vl.padding.bottom, 8));
            var fit = rowsRoot.GetComponent<ContentSizeFitter>();
            if (fit == null) fit = rowsRoot.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = area.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp; sr.content = rowsRoot; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 40f;
            var flowSr = flowContent != null ? flowContent.GetComponentInParent<ScrollRect>(true) : null;
            if (flowSr != null && flowSr.verticalScrollbar != null)
            {
                var bar = Instantiate(flowSr.verticalScrollbar.gameObject, area, false);
                bar.name = "Scrollbar";
                sr.verticalScrollbar = bar.GetComponent<Scrollbar>();
                sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide; sr.verticalScrollbarSpacing = 0f;
            }
            OverviewScroll = sr;
            overviewRoot = area.gameObject;

            if (flowRoot == null) return;
            flowLe = flowRoot.GetComponent<LayoutElement>();
            if (flowSr != null) flowScrollLe = flowSr.GetComponent<LayoutElement>();
            // the note leaves the button row and wraps on its own line (at 1280x720 the 22 px text no longer fits one row)
            var ctl = scopeButton != null ? scopeButton.transform.parent : null;
            var hint = ctl != null ? ctl.Find("Hint") : null;
            if (hint != null && ctl.parent == flowRoot.transform)
            {
                hint.SetParent(flowRoot.transform, false);
                hint.SetSiblingIndex(ctl.GetSiblingIndex() + 1);
                var ht = hint.GetComponent<TextMeshProUGUI>();
                if (ht != null)
                {
                    ht.text = FlowNote;
                    ht.alignment = TextAlignmentOptions.MidlineLeft;
                    ht.textWrappingMode = TextWrappingModes.Normal;
                }
            }
            // shorter column head ("Net"; the note says it is per minute)
            var header = flowRoot.transform.Find("ColumnHeader");
            if (header != null)
                foreach (Transform c in header)
                    if (c.name == "Net/min") { c.name = "Net"; var t = c.GetComponent<TextMeshProUGUI>(); if (t != null) t.text = "Net"; }
        }

        static readonly string[] Cols = { "Made", "Used", "Lost", "Net" };
        static readonly float[] ColDesign = { 86f, 86f, 76f, 90f };

        /// <summary>Box width, filter buttons and number columns follow the snapped text size (once per canvas scale).</summary>
        void FitColumns()
        {
            var box = Box;
            var cv = box != null ? box.GetComponentInParent<Canvas>(true) : null;
            if (cv == null) return;
            float ppu = UiFont.PixelsPerUnit(cv.rootCanvas);
            float key = ppu + (flowRoot != null && flowRoot.activeInHierarchy ? 1000f : 0f);     // hidden texts cannot be measured: refit per tab
            if (Mathf.Approximately(key, columnsAt)) return;
            columnsAt = key;
            float g = UiFit.TextGrowth(cv);
            box.sizeDelta = new Vector2(Mathf.Round(DesignWidth * Mathf.Min(g, 1.35f)), box.sizeDelta.y);
            UiFit.FitButton(overviewTab, 150f);
            UiFit.FitButton(flowTab, 150f);
            if (scopeButton != null && scopeLabel != null)
                UiFit.FitButton(scopeButton, Mathf.Max(150f, UiFit.WidestText(scopeLabel, "This run", "Lifetime") + 32f));
            if (sortButton != null && sortLabel != null)
                UiFit.FitButton(sortButton, Mathf.Max(210f, UiFit.WidestText(sortLabel, "Sort: net / min", "Sort: net total", "Sort: name") + 32f));
            var header = flowRoot != null ? flowRoot.transform.Find("ColumnHeader") : null;
            for (int i = 0; i < Cols.Length; i++)
            {
                var hc = header != null ? header.Find(Cols[i]) : null;
                var ht = hc != null ? hc.GetComponent<TextMeshProUGUI>() : null;
                float w = Mathf.Max(ColDesign[i], UiFit.WidestText(ht, Cols[i], "99.9k", "+99.9") + 8f);
                SetWidth(hc, w);
                if (flowRowTemplate != null) SetWidth(flowRowTemplate.transform.Find(Cols[i]), w);
                foreach (var r in flowRows) if (r != null) SetWidth(r.transform.Find(Cols[i]), w);
            }
        }

        static void SetWidth(Transform t, float w)
        {
            if (t == null) return;
            var le = t.GetComponent<LayoutElement>();
            if (le == null) le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w; le.minWidth = w;
        }

        void ApplyBody(float h)
        {
            bodyH = h;
            if (overviewLe != null) overviewLe.minHeight = overviewLe.preferredHeight = h;
            if (flowLe != null) { flowLe.minHeight = h; flowLe.preferredHeight = h; }
            if (flowScrollLe == null || flowRoot == null || !flowRoot.activeSelf) return;
            // the list gets what the buttons, the note and the column heads leave
            var fr = (RectTransform)flowRoot.transform;
            var vl = fr.GetComponent<VerticalLayoutGroup>();
            if (vl == null) return;
            // a full pass first, so the wrapped note is measured at its real width
            if (Box != null) LayoutRebuilder.ForceRebuildLayoutImmediate(Box);
            float other = vl.preferredHeight - LayoutUtility.GetPreferredHeight((RectTransform)flowScrollLe.transform);
            flowScrollLe.minHeight = 0f;
            flowScrollLe.preferredHeight = Mathf.Max(60f, Mathf.Floor(h - other));
        }

        protected override void ResetBody() { FitColumns(); ApplyBody(BodyHeight); }
        protected override void ShrinkBody(float over) => ApplyBody(Mathf.Max(MinBodyHeight, bodyH - over));

        void Update()
        {
            if (!IsOpen || Time.unscaledTime < nextRefresh) return;
            Refresh();
        }

        /// <summary>Group header marker: a row whose value is null renders as a heading (<see cref="Rows"/>).</summary>
        public const string ProgressHeading = "Progress", ProductionHeading = "Production", CombatHeading = "Combat";

        /// <summary>
        /// The Overview rows in three groups (Progress / Production / Combat); a group heading is a row with a null value.
        /// Rows whose value is zero are hidden except the first three data rows, and an empty group has no heading (VP-25).
        /// </summary>
        public List<(string label, string value)> Rows()
        {
            var G = Sim.State; var st = G.stats;
            var list = new List<(string, string)>();
            int data = 0;
            void Group(string heading, List<(string label, string value, bool zero)> items)
            {
                var shown = new List<(string, string)>();
                foreach (var it in items)
                {
                    bool keep = !it.zero || data < 3;
                    if (!keep) continue;
                    shown.Add((it.label, it.value));
                    data++;
                }
                if (shown.Count == 0) return;
                list.Add((heading, null));
                list.AddRange(shown);
            }
            (string, string, bool) N(string label, long v) => (label, v.ToString(), v == 0);
            int unlocked = 0;
            foreach (var r in Sim.Config.regions) if (G.world.IsUnlocked(r.key)) unlocked++;
            var progress = new List<(string, string, bool)>();
            progress.Add(("Playtime", st.started > 0 ? MetaText.FmtPlaytime(runner.SimNow - st.started) : "--", false));
            progress.Add(("Islands unlocked", unlocked + " / " + Sim.Config.regions.Count, false));
            progress.Add(N("Buildings built", st.buildingsBuilt));
            progress.Add(N("Upgrades applied", st.upgradesApplied));
            progress.Add(("Ending reached", G.ascended ? "yes" : "not yet", false));
            Group(ProgressHeading, progress);
            Group(ProductionHeading, new List<(string, string, bool)>
            {
                N("Total gathered", st.totalGathered), N("Total crafted", st.totalCrafted), N("Disciples recruited", st.disciplesRecruited),
                N("Wisp links added", st.linksAdded), N("Recipe switches", st.recipeSwitches), ("Carry capacity", Sim.Hand.Cap().ToString(), false),
            });
            Group(CombatHeading, new List<(string, string, bool)> { N("Fox spirits slain", st.foxKills) });
            return list;
        }

        void Refresh()
        {
            nextRefresh = Time.unscaledTime + 1f;
            if (overviewRoot != null) overviewRoot.SetActive(!showFlow);
            else if (rowsRoot != null) rowsRoot.gameObject.SetActive(!showFlow);
            if (flowRoot != null) flowRoot.SetActive(showFlow);
            Tint(overviewTab, !showFlow);
            Tint(flowTab, showFlow);
            if (showFlow) RefreshFlow(); else RefreshOverview();
        }

        static void Tint(Button b, bool selected)
        {
            if (b == null) return;
            var skin = b.GetComponent<UiButtonSkin>();   // delivered art: selected tab = toggle-on sprite
            if (skin != null && skin.SetToggle(selected ? UiToggleState.On : UiToggleState.Off)) return;
            if (b.targetGraphic != null) b.targetGraphic.color = selected ? UiPalette.AccentDk : UiPalette.Panel;
        }

        void RefreshOverview()
        {
            var data = Rows();
            while (rows.Count < data.Count) rows.Add(Instantiate(rowTemplate, rowsRoot));
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < data.Count;
                rows[i].gameObject.SetActive(on);
                if (!on) continue;
                bool heading = data[i].value == null;
                rows[i].Set(data[i].label, heading ? "" : data[i].value);
                if (rows[i].a != null)
                {
                    rows[i].a.color = heading ? UiPalette.Gold : UiPalette.Text;     // 4.5:1 on the dark panel
                    rows[i].a.fontStyle = heading ? FontStyles.Bold : FontStyles.Normal;
                    rows[i].a.fontSize = heading ? 21f : 19.5f;
                }
            }
        }

        // ---- Flow tab ----

        static string Fmt(long v)
        {
            if (v >= 10_000_000) return (v / 1_000_000.0).ToString("0.#") + "M";
            if (v >= 100_000) return (v / 1000.0).ToString("0") + "k";
            if (v >= 10_000) return (v / 1000.0).ToString("0.#") + "k";
            return v.ToString();
        }

        static string FmtRate(double r)
        {
            if (System.Math.Abs(r) < 0.05) return "0";
            return (r > 0 ? "+" : "") + r.ToString(System.Math.Abs(r) >= 100 ? "0" : "0.#");
        }

        long Prod(FlowEntry e) => lifetime ? e.produced : e.runProduced;
        long Cons(FlowEntry e) => lifetime ? e.consumed : e.runConsumed;
        long Lost(FlowEntry e) => lifetime ? e.lost : e.runLost;

        string ItemName(string it) => Sim.Config.Item(it)?.name ?? it;

        int Compare(FlowEntry a, FlowEntry b)
        {
            int c = 0;
            switch (sort)
            {
                case Sort.NetPerMin: c = sortLedger.NetPerMin(b).CompareTo(sortLedger.NetPerMin(a)); break;
                case Sort.NetTotal: c = (Prod(b) - Cons(b) - Lost(b)).CompareTo(Prod(a) - Cons(a) - Lost(a)); break;
                case Sort.Name: return string.CompareOrdinal(ItemName(a.item), ItemName(b.item));
            }
            return c != 0 ? c : string.CompareOrdinal(ItemName(a.item), ItemName(b.item));
        }

        void RefreshFlow()
        {
            var ledger = Sim.State.flow;
            sortLedger = ledger;
            sprites ??= new SpriteCache(runner.Database, null);
            if (scopeLabel != null) scopeLabel.text = lifetime ? "Lifetime" : "This run";
            if (sortLabel != null) sortLabel.text = "Sort: " + (sort == Sort.NetPerMin ? "net / min" : sort == Sort.NetTotal ? "net total" : "name");

            sorted.Clear();
            foreach (var e in ledger.items)
                if (e != null && (Prod(e) != 0 || Cons(e) != 0 || Lost(e) != 0)) sorted.Add(e);
            sorted.Sort(Compare);
            if (emptyText != null) emptyText.gameObject.SetActive(sorted.Count == 0);

            while (flowRows.Count < sorted.Count) flowRows.Add(Instantiate(flowRowTemplate, flowContent));
            for (int i = 0; i < flowRows.Count; i++)
            {
                bool on = i < sorted.Count;
                var r = flowRows[i];
                r.gameObject.SetActive(on);
                if (!on) continue;
                var e = sorted[i];
                if (r.icon != null) r.icon.sprite = sprites.Item(e.item);
                r.nameText.text = ItemName(e.item);
                r.producedText.text = Fmt(Prod(e));
                r.consumedText.text = Fmt(Cons(e));
                r.lostText.text = Fmt(Lost(e));
                double net = ledger.NetPerMin(e);
                r.netText.text = FmtRate(net);
                r.netText.color = net > 0.05 ? UiPalette.Accent : net < -0.05 ? UiPalette.Danger : UiPalette.Muted;
                r.lostText.color = Lost(e) > 0 ? UiPalette.Danger : UiPalette.MutedSoft;
            }
        }
    }
}
