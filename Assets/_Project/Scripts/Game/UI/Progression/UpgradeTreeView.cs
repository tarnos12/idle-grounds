using System.Collections.Generic;
using System.Text;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Altar upgrade tree modal (ui-input-render §4.7, ui.js openTree/drawTree): opened by left-clicking
    /// the built Altar. Nodes sit at their data positions (px, y down) around the root "hand", scaled by
    /// <see cref="Scale"/> (UI = 1.5x CSS px); straight edges width 5 between visible nodes (.45 alpha
    /// when both ends are owned, else .22). Visibility / selectability come from
    /// <see cref="Simulation.UpgradeTree"/> (BFS tiers). Hover = instant tooltip; clicking a selectable,
    /// non-maxed node selects it as the Altar job (<see cref="Simulation.SelectUpgradeNode"/>) and closes.
    /// WASD (and mouse drag) pan the board instead of the map while open (treeCam clamp of the spec).
    /// The footer shows the active job's remaining needs (fed by right-clicking the Altar) + Cancel/refund.
    /// </summary>
    public class UpgradeTreeView : MonoBehaviour, IDragHandler
    {
        public const float Scale = 1.5f, Half = 26f, EdgeWidth = 5f, PanPxPerSec = 720f;

        [SerializeField] GameRunner runner;
        [SerializeField] GameObject modal;
        [SerializeField] RectTransform box;
        [SerializeField] RectTransform body;
        [SerializeField] RectTransform tree;
        [SerializeField] RectTransform edgesRoot;
        [SerializeField] RectTransform nodesRoot;
        [SerializeField] Image edgeTemplate;
        [SerializeField] UpgradeTreeNodeView nodeTemplate;
        [SerializeField] RectTransform tooltip;
        [SerializeField] TextMeshProUGUI tooltipText;
        [SerializeField] TextMeshProUGUI jobText;
        [SerializeField] Button cancelJobButton;
        [SerializeField] Button closeButton;
        [SerializeField] Button debugButton;

        readonly Dictionary<string, UpgradeTreeNodeView> nodes = new Dictionary<string, UpgradeTreeNodeView>();
        readonly List<(Image img, string a, string b)> edges = new List<(Image, string, string)>();
        List<UpgradeNodeState> states;
        readonly Dictionary<string, UpgradeNodeState> byId = new Dictionary<string, UpgradeNodeState>();
        UpgradeTreeNodeView hovered;
        Vector2 treeCam;
        float minX, maxX, minY, maxY;

        public bool IsOpen => modal != null && modal.activeSelf;
        public bool RevealAll { get; private set; }
        public Vector2 TreeCam => treeCam;
        public IReadOnlyDictionary<string, UpgradeTreeNodeView> Nodes => nodes;
        public string TooltipShown => tooltip != null && tooltip.gameObject.activeSelf ? tooltipText.text : null;
        public string LastRefusal { get; private set; }

        Simulation Sim => runner.Sim;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (modal != null) modal.SetActive(false);
            if (edgeTemplate != null) edgeTemplate.gameObject.SetActive(false);
            if (nodeTemplate != null) nodeTemplate.gameObject.SetActive(false);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (cancelJobButton != null) cancelJobButton.onClick.AddListener(CancelJob);
            if (debugButton != null) debugButton.onClick.AddListener(() => { RevealAll = !RevealAll; Refresh(); });
        }

        void OnDisable() => CameraController.PanSuspended = false;

        // ================= open / close =================

        public void Open()
        {
            if (runner == null || runner.Sim == null) return;
            modal.SetActive(true);
            BuildOnce();
            LayoutBox();
            treeCam = new Vector2(body.rect.width * 0.5f, body.rect.height * 0.5f);
            ClampCam();
            hovered = null;
            LastRefusal = null;
            CameraController.PanSuspended = true;
            Refresh();
        }

        public void Close()
        {
            if (modal != null) modal.SetActive(false);
            if (tooltip != null) tooltip.gameObject.SetActive(false);
            hovered = null;
            CameraController.PanSuspended = false;
        }

        void LayoutBox()
        {
            var canvas = (RectTransform)modal.transform;
            Canvas.ForceUpdateCanvases();
            float w = Mathf.Min(920f * Scale, canvas.rect.width * 0.96f);
            float h = Mathf.Min(880f * Scale, canvas.rect.height * 0.92f);
            box.sizeDelta = new Vector2(w, h);
            LayoutRebuilder.ForceRebuildLayoutImmediate(box);
        }

        void BuildOnce()
        {
            if (nodes.Count > 0) return;
            var cfg = runner.Config.upgradeTree;
            minX = minY = float.MaxValue; maxX = maxY = float.MinValue;
            foreach (var n in cfg)
            {
                var v = Instantiate(nodeTemplate, nodesRoot);
                v.gameObject.SetActive(true);
                v.Init(this, n.id, runner.Database.UpgradeIcon(n.id));
                v.Rect.anchoredPosition = new Vector2(n.x * Scale, -n.y * Scale);
                nodes[n.id] = v;
                minX = Mathf.Min(minX, n.x * Scale); maxX = Mathf.Max(maxX, n.x * Scale);
                minY = Mathf.Min(minY, n.y * Scale); maxY = Mathf.Max(maxY, n.y * Scale);
            }
            foreach (var n in cfg)
                foreach (var l in n.links)
                {
                    if (!nodes.ContainsKey(l)) continue;
                    var img = Instantiate(edgeTemplate, edgesRoot);
                    img.gameObject.SetActive(true);
                    img.name = "Edge_" + n.id + "_" + l;
                    Place(img.rectTransform, nodes[n.id].Rect.anchoredPosition, nodes[l].Rect.anchoredPosition);
                    edges.Add((img, n.id, l));
                }
        }

        static void Place(RectTransform rt, Vector2 a, Vector2 b)
        {
            var d = b - a;
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(d.magnitude, EdgeWidth * Scale);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        // ================= interaction =================

        public void Hover(UpgradeTreeNodeView v) { hovered = v; Refresh(); }
        public void Unhover(UpgradeTreeNodeView v) { if (hovered == v) hovered = null; Refresh(); }

        /// <summary>Node click: select the job and close; refusals stay open (error).</summary>
        public bool Click(UpgradeTreeNodeView v)
        {
            var st = v.State;
            if (st == null || v.Mystery || !st.visible && !RevealAll) { LastRefusal = "Undiscovered"; return false; }
            if (st.maxed) { LastRefusal = "Maxed"; return false; }
            if (!st.selectable) { LastRefusal = "Locked"; return false; }
            if (!Sim.SelectUpgradeNode(v.Id)) { LastRefusal = "Refused"; return false; }
            LastRefusal = null;
            Close();
            return true;
        }

        public bool ClickNode(string id) => nodes.TryGetValue(id, out var v) && Click(v);

        public void CancelJob()
        {
            if (Sim.State.upgradeJob == null) return;
            Sim.CancelUpgradeJob();
            Refresh();
        }

        public void OnDrag(PointerEventData e)
        {
            var canvas = GetComponentInParent<Canvas>();
            float k = canvas != null ? canvas.scaleFactor : 1f;
            treeCam += new Vector2(e.delta.x, -e.delta.y) / k;
            ClampCam();
            ApplyCam();
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            Vector2 dir = Vector2.zero;
            if (kb.aKey.isPressed) dir.x += 1; if (kb.dKey.isPressed) dir.x -= 1;   // WASD moves the view: content slides opposite
            if (kb.wKey.isPressed) dir.y += 1; if (kb.sKey.isPressed) dir.y -= 1;
            if (dir == Vector2.zero) return;
            treeCam += new Vector2(dir.x, -dir.y) * PanPxPerSec * Time.unscaledDeltaTime;
            ClampCam();
            ApplyCam();
        }

        /// <summary>treeCam = body-local position (x right, y down) of node (0,0); spec clamp with a 90 px margin.</summary>
        void ClampCam()
        {
            float bw = body.rect.width, bh = body.rect.height, m = 90f * Scale;
            float x0 = Mathf.Min(bw / 2, bw - m - maxX), x1 = Mathf.Max(bw / 2, m - minX);
            float y0 = Mathf.Min(bh / 2, bh - m - maxY), y1 = Mathf.Max(bh / 2, m - minY);
            treeCam = new Vector2(Mathf.Clamp(treeCam.x, x0, x1), Mathf.Clamp(treeCam.y, y0, y1));
        }

        void ApplyCam() { if (tree != null) tree.anchoredPosition = new Vector2(treeCam.x, -treeCam.y); }

        /// <summary>The tree query + strings allocate: full refresh at 5 Hz (and on hover / click / open); the pan applies every frame.</summary>
        public const float RefreshSeconds = 0.2f;
        float nextRefreshAt;

        void LateUpdate()
        {
            if (!IsOpen || runner == null || runner.Sim == null) return;
            ApplyCam();
            PlaceTooltip();
            if (Time.unscaledTime >= nextRefreshAt) Refresh();
        }

        // ================= view =================

        void Refresh()
        {
            if (!IsOpen) return;
            nextRefreshAt = Time.unscaledTime + RefreshSeconds;
            ApplyCam();
            states = Sim.UpgradeTree();
            byId.Clear();
            foreach (var st in states) byId[st.node.id] = st;
            var job = Sim.State.upgradeJob;
            foreach (var kv in nodes)
            {
                if (!byId.TryGetValue(kv.Key, out var st)) { kv.Value.gameObject.SetActive(false); continue; }
                bool vis = st.visible || RevealAll;
                if (kv.Value.gameObject.activeSelf != vis) kv.Value.gameObject.SetActive(vis);
                kv.Value.Apply(st, RevealAll, st.selected);
            }
            foreach (var (img, a, b) in edges)
            {
                bool va = byId.TryGetValue(a, out var sa) && (sa.visible || RevealAll);
                bool vb = byId.TryGetValue(b, out var sb) && (sb.visible || RevealAll);
                bool on = va && vb;
                if (img.gameObject.activeSelf != on) img.gameObject.SetActive(on);
                if (on) img.color = new Color(225 / 255f, 232 / 255f, 224 / 255f, sa.owned && sb.owned ? 0.45f : 0.22f);
            }
            RefreshTooltip(job);
            RefreshJob(job);
        }

        string ItemName(string it) => runner.Config.Item(it)?.name ?? it;

        void RefreshTooltip(UpgradeJob job)
        {
            if (tooltip == null) return;
            var v = hovered != null && hovered.isActiveAndEnabled ? hovered : null;
            bool on = v != null && v.State != null;
            if (tooltip.gameObject.activeSelf != on) tooltip.gameObject.SetActive(on);
            if (!on) return;
            var st = v.State; var n = st.node;
            var sb = new StringBuilder();
            if (v.Mystery) sb.Append("<b>???</b>\n<color=#94a3b8>Undiscovered upgrade</color>");
            else
            {
                sb.Append("<b>").Append(n.name).Append("</b>\n");
                sb.Append("Level: ").Append(st.level).Append('/').Append(st.max).Append('\n');
                sb.Append("<color=#94a3b8>").Append(UiText.StripEmoji(n.desc)).Append("</color>");
                if (st.selected && job != null)
                {
                    int fed = job.paid.Total(), all = job.paid.Total() + Sim.UpgradeJobRemaining().Total();
                    sb.Append("\n<color=#fbbf24>Selected - fed ").Append(fed).Append('/').Append(all).Append("</color>");
                }
                if (st.maxed) sb.Append("\n<b>MAX</b>");
                else if (st.nextCost != null)
                {
                    sb.Append("\n<color=#fbbf24>Cost: ");
                    bool first = true;
                    foreach (var e in st.nextCost) { if (!first) sb.Append(", "); first = false; sb.Append(e.qty).Append(' ').Append(ItemName(e.item)); }
                    sb.Append("</color>");
                }
            }
            string s = sb.ToString();
            if (tooltipText.text != s) tooltipText.text = s;
            PlaceTooltip();
        }

        /// <summary>Tooltip centred 12 px above the hovered node (follows the pan every frame).</summary>
        void PlaceTooltip()
        {
            if (tooltip == null || !tooltip.gameObject.activeSelf) return;
            var v = hovered != null && hovered.isActiveAndEnabled ? hovered : null;
            if (v == null) return;
            var p = v.Rect.anchoredPosition + new Vector2(treeCam.x, -treeCam.y);
            tooltip.anchoredPosition = new Vector2(p.x, p.y + (Half + 12f) * Scale);
        }

        void RefreshJob(UpgradeJob job)
        {
            if (jobText == null) return;
            if (job == null)
            {
                jobText.text = "<color=#94a3b8>No upgrade selected - click a green node, then right-click its cost onto the Altar.</color>";
                if (cancelJobButton != null) cancelJobButton.gameObject.SetActive(false);
                return;
            }
            string nm = job.type;
            foreach (var st in states) if (st.selected) { nm = st.node.name; break; }
            var sb = new StringBuilder("Selected: <b>").Append(nm).Append("</b>  <color=#fbbf24>still needs ");
            bool first = true;
            foreach (var e in Sim.UpgradeJobRemaining())
            {
                if (e.qty <= 0) continue;
                if (!first) sb.Append(", "); first = false;
                sb.Append(e.qty).Append(' ').Append(ItemName(e.item));
            }
            sb.Append("</color> - right-click the Altar to feed it");
            jobText.text = sb.ToString();
            if (cancelJobButton != null) cancelJobButton.gameObject.SetActive(true);
        }
    }
}
