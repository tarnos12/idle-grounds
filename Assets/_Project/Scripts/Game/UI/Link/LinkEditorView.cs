using System.Collections.Generic;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Lantern link editor (ui-input-render 4.4, ui.js linkMode): left-click a built Wisp Lantern opens a
    /// strip above the bottom bar listing its links (status dot, "n.", source > target, remove) and an
    /// "Add link" button. Add link = two map clicks: a SOURCE (<see cref="Simulation.CanBeLinkSource"/>)
    /// then a TARGET (<see cref="Simulation.CanBeLinkTarget"/>, != source) with a rubber-band thread
    /// following the cursor; a refused pair floats the reason and stays on the target step; a click
    /// outside the lantern's region says "Wisps can't cross the void". Esc backs out one step
    /// (picking -> menu -> closed). Valid picks are outlined (BuildController.HighlightFor) and every
    /// Gathering Stone of the region shows its reach circle while the editor is open.
    /// </summary>
    public class LinkEditorView : MonoBehaviour
    {
        public enum Mode { Closed, Menu, PickSource, PickTarget }

        public static readonly Color RubberColour = ViewKit.Rgba(251, 191, 36, 0.8f);
        const string SourceHint = "Click the SOURCE building on the map (gatherer / seal / storehouse)... Esc cancels";

        [SerializeField] GameRunner runner;
        [SerializeField] HandController hand;
        [SerializeField] FxService fx;
        [SerializeField] LinkLineView rubberBand;
        [SerializeField] GameObject panel;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] Button addButton;
        [SerializeField] TextMeshProUGUI hintText;
        [SerializeField] TextMeshProUGUI warnText;
        [SerializeField] TextMeshProUGUI tipText;
        [SerializeField] RectTransform grid;
        [SerializeField] LinkRow rowTemplate;

        readonly List<LinkRow> rows = new List<LinkRow>();
        readonly HashSet<int> candidates = new HashSet<int>();
        SpriteCache sprites;
        float candidatesAt;
        string refuse;
        bool voidHint;

        public Mode State { get; private set; } = Mode.Closed;
        public bool IsOpen => State != Mode.Closed;
        public bool Picking => State == Mode.PickSource || State == Mode.PickTarget;
        public string Area { get; private set; }
        public int LanternId { get; private set; }
        public int SourceId { get; private set; }
        public IReadOnlyList<LinkRow> Rows => rows;
        public Button AddButton => addButton;
        public string Hint => hintText != null && hintText.gameObject.activeSelf ? hintText.text : null;
        public string Warning => warnText != null && warnText.gameObject.activeSelf ? warnText.text : null;
        public LinkLineView RubberBand => rubberBand;

        Simulation Sim => runner.Sim;
        Building Lantern => runner.State.Area(Area)?.BuildingById(LanternId);

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (hand == null) hand = FindFirstObjectByType<HandController>();
            if (fx == null) fx = FindFirstObjectByType<FxService>();
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (panel != null) panel.SetActive(false);
            if (rubberBand != null) rubberBand.gameObject.SetActive(false);
            if (addButton != null) addButton.onClick.AddListener(BeginAddLink);
        }

        // ================= state =================

        public void Open(string area, Building lantern)
        {
            if (lantern == null || !lantern.built) return;
            sprites ??= new SpriteCache(runner.Database, null);
            Area = area; LanternId = lantern.id; SourceId = 0;
            refuse = null; voidHint = false;
            State = Mode.Menu;
            panel.SetActive(true);
            candidates.Clear();
            Refresh();
        }

        public void Close()
        {
            State = Mode.Closed;
            SourceId = 0; refuse = null; voidHint = false;
            candidates.Clear();
            if (panel != null) panel.SetActive(false);
            if (rubberBand != null) rubberBand.gameObject.SetActive(false);
        }

        /// <summary>"Add link" button: start picking the source.</summary>
        public void BeginAddLink()
        {
            if (State != Mode.Menu) return;
            State = Mode.PickSource;
            SourceId = 0; refuse = null; voidHint = false;
            RecomputeCandidates();
            Refresh();
        }

        /// <summary>Esc: picking -> menu, menu -> closed. True = consumed.</summary>
        public bool Escape()
        {
            if (!IsOpen) return false;
            if (Picking)
            {
                State = Mode.Menu; SourceId = 0; refuse = null; voidHint = false;
                candidates.Clear();
                if (rubberBand != null) rubberBand.gameObject.SetActive(false);
                Refresh();
            }
            else Close();
            return true;
        }

        public void RemoveLink(int index)
        {
            if (!IsOpen) return;
            Sim.RemoveLink(Area, LanternId, index);
            Refresh();
        }

        /// <summary>True when this building is a valid pick for the current step (outline + highlight).</summary>
        public bool IsCandidate(int buildingId) => Picking && candidates.Contains(buildingId);

        void RecomputeCandidates()
        {
            candidates.Clear();
            candidatesAt = Time.unscaledTime;
            if (State == Mode.PickSource) foreach (var b in Sim.LinkSources(Area)) candidates.Add(b.id);
            else if (State == Mode.PickTarget) foreach (var b in Sim.LinkTargets(Area, SourceId)) candidates.Add(b.id);
        }

        // ================= world clicks (BuildController routes here while Picking) =================

        /// <summary>One captured world click while picking (ui.js:2972).</summary>
        public void HandleWorldClick(string area, double lx, double ly)
        {
            if (!Picking) return;
            if (area != Area)
            {
                // wisps only fly inside one region: say why the pick was ignored
                if (!voidHint) { voidHint = true; Refresh(); }
                if (fx != null && hand != null) fx.Floater(hand.CursorWorld, "Wisps can't cross the void", FxService.Danger);
                return;
            }
            voidHint = false;
            var b = Sim.BuildingAt(area, lx, ly);
            if (b != null && State == Mode.PickSource && Sim.CanBeLinkSource(b))
            {
                SourceId = b.id; State = Mode.PickTarget; refuse = null;
                RecomputeCandidates();
            }
            else if (b != null && State == Mode.PickTarget && Sim.CanBeLinkTarget(b) && b.id != SourceId)
            {
                var why = Sim.LinkRefusal(area, SourceId, b.id);
                if (why != null)
                {
                    // can never carry anything: say why, stay in target picking
                    if (fx != null && hand != null) fx.Floater(hand.CursorWorld, why.text, FxService.Danger);
                    refuse = why.text;
                }
                else
                {
                    Sim.AddLink(area, LanternId, SourceId, b.id);
                    State = Mode.Menu; SourceId = 0; refuse = null;
                    candidates.Clear();
                    if (rubberBand != null) rubberBand.gameObject.SetActive(false);
                }
            }
            Refresh();
        }

        // ================= view =================

        public void SetTip(string tip)
        {
            if (tipText == null) return;
            bool on = !string.IsNullOrEmpty(tip);
            if (tipText.gameObject.activeSelf != on) tipText.gameObject.SetActive(on);
            if (on && tipText.text != tip) tipText.text = tip;
        }

        static string Name(Building b, BuildingDef def) => def != null ? def.name : b.type;

        /// <summary>ui.js bLabel: a stone reads as its fullest buffer item + live count; others by name (+ typed item).</summary>
        void Describe(Building b, LinkRow.Endpoint e)
        {
            if (b == null) { LinkRow.SetEndpoint(e, null, null, "?"); return; }
            var def = runner.Config.Building(b.type);
            Sprite icon = sprites.Building(b.type), item = null;
            string text;
            if (def != null && def.gather.enabled)
            {
                HandStack top = null;
                if (b.inv != null) foreach (var s in b.inv) if (s.qty > 0 && (top == null || s.qty > top.qty)) top = s;
                if (top != null) item = sprites.Item(top.item);
                text = top != null ? "x" + BuildingSystem.GatherTotal(b) : "(empty)";
            }
            else
            {
                text = Name(b, def);
                if (def != null && (def.seal.enabled || b.type == "storehouse") && b.item != null) item = sprites.Item(b.item);
            }
            LinkRow.SetEndpoint(e, icon, item, text);
        }

        void LateUpdate()
        {
            if (!IsOpen || runner == null || runner.Sim == null) return;
            var lan = Lantern;
            if (lan == null || !lan.built) { Close(); return; }
            if (Picking && Time.unscaledTime - candidatesAt > 0.25f) RecomputeCandidates();
            Refresh();
            UpdateRubberBand();
        }

        void UpdateRubberBand()
        {
            if (rubberBand == null) return;
            bool show = State == Mode.PickTarget && hand != null && hand.CursorOver && hand.CursorArea == Area;
            if (show)
            {
                var src = runner.State.Area(Area)?.BuildingById(SourceId);
                if (src == null) show = false;
                else
                {
                    var (cx, cy) = Sim.World.BuildingCenterPx(src);
                    rubberBand.Set(runner.Space.PxToWorld(Area, cx, cy), hand.CursorWorld, RubberColour, 2f);
                    rubberBand.SetDot(false, default);
                }
            }
            if (rubberBand.gameObject.activeSelf != show) rubberBand.gameObject.SetActive(show);
        }

        void Refresh()
        {
            var lan = Lantern;
            if (lan == null) return;
            if (titleText != null && titleText.text.Length == 0) titleText.text = "Wisp Lantern - links run in order, one per beat";

            // hint / add button (the pick prompt replaces "Add link", as in the original)
            if (addButton != null) addButton.gameObject.SetActive(!Picking);
            string hint = null;
            if (State == Mode.PickSource) hint = SourceHint;
            else if (State == Mode.PickTarget)
            {
                var src = runner.State.Area(Area)?.BuildingById(SourceId);
                string nm = src != null ? Name(src, runner.Config.Building(src.type)) : "?";
                hint = nm + " > click the TARGET building... Esc cancels";
            }
            SetLabel(hintText, hint);
            string warn = voidHint ? "Wisps can't cross the void between regions"
                : (refuse != null && State == Mode.PickTarget) ? refuse + " - pick another target" : null;
            SetLabel(warnText, warn);

            // rows
            int n = lan.links != null ? lan.links.Count : 0;
            while (rows.Count < n)
            {
                var r = Instantiate(rowTemplate, grid);
                r.Init(this);
                rows.Add(r);
            }
            var area = runner.State.Area(Area);
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < n;
                if (rows[i].gameObject.activeSelf != on) rows[i].gameObject.SetActive(on);
                if (!on) continue;
                var l = lan.links[i];
                var row = rows[i];
                row.SetIndex(i);
                var st = Sim.LinkStatus(Area, LanternId, i);
                row.SetStatus(LinkLineView.DotColour(st.dot), st.text);
                Describe(area.BuildingById(l.from), row.Source);
                Describe(area.BuildingById(l.to), row.Target);
            }
        }

        static void SetLabel(TextMeshProUGUI t, string s)
        {
            if (t == null) return;
            bool on = !string.IsNullOrEmpty(s);
            if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
            if (on && t.text != s) t.text = s;
        }
    }
}
