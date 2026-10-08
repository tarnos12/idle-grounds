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
            if (overviewTab != null) overviewTab.onClick.AddListener(() => SetTab(false));
            if (flowTab != null) flowTab.onClick.AddListener(() => SetTab(true));
            if (scopeButton != null) scopeButton.onClick.AddListener(() => { lifetime = !lifetime; Refresh(); });
            if (sortButton != null) sortButton.onClick.AddListener(() => { sort = (Sort)(((int)sort + 1) % 3); Refresh(); });
        }

        public override void Open()
        {
            if (Sim == null) return;
            base.Open();
            Refresh();
        }

        void SetTab(bool flow) { showFlow = flow; Refresh(); }

        /// <summary>Test / tooling hook: switch tab and scope.</summary>
        public void Show(bool flow, bool lifetimeTotals) { showFlow = flow; lifetime = lifetimeTotals; if (IsOpen) Refresh(); }

        void Update()
        {
            if (!IsOpen || Time.unscaledTime < nextRefresh) return;
            Refresh();
        }

        public List<(string label, string value)> Rows()
        {
            var G = Sim.State; var st = G.stats;
            var list = new List<(string, string)>();
            if (st.started > 0) list.Add(("Playtime", MetaText.FmtAway(runner.SimNow - st.started)));
            list.Add(("Total gathered", st.totalGathered.ToString()));
            list.Add(("Total crafted", st.totalCrafted.ToString()));
            list.Add(("Fox spirits slain", st.foxKills.ToString()));
            list.Add(("Buildings built", st.buildingsBuilt.ToString()));
            list.Add(("Upgrades applied", st.upgradesApplied.ToString()));
            list.Add(("Disciples recruited", st.disciplesRecruited.ToString()));
            list.Add(("Wisp links added", st.linksAdded.ToString()));
            list.Add(("Recipe switches", st.recipeSwitches.ToString()));
            list.Add(("Ascensions", G.ascensions.ToString()));
            list.Add(("Ascension Points", G.ascendPoints.ToString()));
            int unlocked = 0;
            foreach (var r in Sim.Config.regions) if (G.world.IsUnlocked(r.key)) unlocked++;
            list.Add(("Islands unlocked", unlocked + " / " + Sim.Config.regions.Count));
            list.Add(("Carry capacity", Sim.Hand.Cap().ToString()));
            return list;
        }

        void Refresh()
        {
            nextRefresh = Time.unscaledTime + 1f;
            if (rowsRoot != null) rowsRoot.gameObject.SetActive(!showFlow);
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
                if (on) rows[i].Set(data[i].label, data[i].value);
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
                r.lostText.color = Lost(e) > 0 ? UiPalette.Danger : UiPalette.Muted;
            }
        }
    }
}
