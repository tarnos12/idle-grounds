using System.Collections.Generic;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Spirit Bridge panel (ADR 0003): left-click a built bridge → title, its state ("Unpaired" /
    /// "Sending to the Mine bridge — 128 cells of sky" / "Receiving from …"), buffer n/cap, and either
    /// the pairing list (<see cref="Simulation.PairableBridges"/>: unpaired built bridges on other unlocked
    /// Islands, with their sky distance) — each row pairs this bridge as the SENDER ("Send to") or the
    /// RECEIVER ("Receive from") — or, once paired, the partner line + "Unpair". Refusals from
    /// <see cref="Simulation.PairBridges"/> show in red. One building panel at a time (BuildController).
    /// </summary>
    public class BridgePanelView : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] GameObject panel;
        [SerializeField] Image icon;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI statusText;
        [SerializeField] TextMeshProUGUI bufferText;
        [SerializeField] TextMeshProUGUI hintText;
        [SerializeField] RectTransform rowsRoot;
        [SerializeField] BridgeRow rowTemplate;
        [SerializeField] TextMeshProUGUI emptyText;
        [SerializeField] Button unpairButton;
        [SerializeField] TextMeshProUGUI reasonText;
        [SerializeField] Button closeButton;

        readonly List<BridgeRow> rows = new List<BridgeRow>();
        SpriteCache sprites;
        float nextRefreshAt;
        string listSig;

        public bool IsOpen => panel != null && panel.activeSelf;
        public string Area { get; private set; }
        public int BuildingId { get; private set; }
        public string Reason { get; private set; }
        public string StatusLine => statusText != null ? statusText.text : null;
        public int CandidateCount { get; private set; }
        public IReadOnlyList<BridgeRow> Rows => rows;

        Simulation Sim => runner.Sim;
        Building Bridge => runner.State.Area(Area)?.BuildingById(BuildingId);

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (panel != null) panel.SetActive(false);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (unpairButton != null) unpairButton.onClick.AddListener(() => Unpair());
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        public void Open(string area, Building b)
        {
            if (b == null || !b.built) return;
            sprites ??= new SpriteCache(runner.Database, null);
            Area = area; BuildingId = b.id;
            Reason = null; listSig = null;
            panel.SetActive(true);
            var def = runner.Config.Building(b.type);
            if (icon != null) icon.sprite = sprites.Building(b.type);
            ViewKit.SetText(titleText, (def != null ? def.name : "Spirit Bridge") + " - " + IslandName(area));
            Refresh();
        }

        public void Close() { if (panel != null) panel.SetActive(false); }

        string IslandName(string key) => runner.Config.Region(key)?.name ?? key;

        /// <summary>Pair this bridge with a candidate: <paramref name="send"/> = this bridge sends to it. Null = paired, else the refusal.</summary>
        public string Pair(string island, int bridgeId, bool send)
        {
            if (!IsOpen) return "closed";
            string why = send ? Sim.PairBridges(Area, BuildingId, island, bridgeId) : Sim.PairBridges(island, bridgeId, Area, BuildingId);
            Reason = why;
            if (why == null) AudioService.Play("unlock"); else AudioService.Play("error");
            listSig = null;
            Refresh();
            return why;
        }

        public bool Unpair()
        {
            if (!IsOpen) return false;
            bool ok = Sim.UnpairBridge(Area, BuildingId);
            Reason = null; listSig = null;
            Refresh();
            return ok;
        }

        void LateUpdate()
        {
            if (!IsOpen || runner == null || runner.Sim == null) return;
            var b = Bridge;
            if (b == null || !b.built) { Close(); return; }
            if (Time.unscaledTime >= nextRefreshAt) Refresh();     // 5 Hz (and on open / pair / unpair)
        }

        static string Cells(double px, int cell) => Mathf.RoundToInt((float)(px / cell)) + " cells";

        void Refresh()
        {
            var b = Bridge;
            if (b == null) return;
            nextRefreshAt = Time.unscaledTime + 0.2f;
            int cell = runner.Config.grid.cell;
            var partner = Sim.BridgePartner(Area, b);
            int cap = runner.Config.Building(b.type)?.bridge.cap ?? 20;
            int n = BuildingSystem.GatherTotal(b);
            ViewKit.SetText(bufferText, "Buffer " + n + "/" + cap);
            if (bufferText != null) bufferText.color = n >= cap ? UiPalette.Danger : UiPalette.Text;

            if (partner != null)
            {
                var (ax, ay) = Sim.World.BuildingWorldCenterPx(Area, b);
                var (bx, by) = Sim.World.BuildingWorldCenterPx(b.pairIsland, partner);
                double d = System.Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                string other = IslandName(b.pairIsland);
                ViewKit.SetText(statusText, (b.pairSends ? "Sending to the " + other + " bridge" : "Receiving from the " + other + " bridge") + " - " + Cells(d, cell) + " of sky");
                if (statusText != null) statusText.color = b.pairSends ? UiPalette.Hex("#67e8f9") : UiPalette.Hex("#bef264");
                ViewKit.SetText(hintText, b.pairSends
                    ? "Link lanterns INTO this bridge; its wisps carry the buffer across the sky, one per beat."
                    : "Link a lantern FROM this bridge to carry what arrives to your buildings.");
                if (unpairButton != null) unpairButton.gameObject.SetActive(true);
                SetRows(null);
            }
            else
            {
                ViewKit.SetText(statusText, "Unpaired");
                if (statusText != null) statusText.color = UiPalette.Muted;
                ViewKit.SetText(hintText, "Pair with a Spirit Bridge on another unlocked Island. Each pair is one-way: pick whether this bridge sends or receives.");
                if (unpairButton != null) unpairButton.gameObject.SetActive(false);
                SetRows(Sim.PairableBridges(Area, BuildingId));
            }
            ViewKit.SetText(reasonText, Reason ?? "");
        }

        void SetRows(List<BridgeCandidate> list)
        {
            int cell = runner.Config.grid.cell;
            CandidateCount = list?.Count ?? 0;
            // rebuild only when the candidate set changes (buttons keep their listeners)
            var sig = new System.Text.StringBuilder();
            if (list != null) foreach (var c in list) sig.Append(c.island).Append(':').Append(c.building.id).Append(':').Append((int)(c.distancePx / cell)).Append(';');
            string s = sig.ToString();
            bool showEmpty = list != null && list.Count == 0;
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(showEmpty);
                if (showEmpty) emptyText.text = "No unpaired bridge on another unlocked Island yet - build one there first.";
            }
            if (s == listSig) return;
            listSig = s;
            foreach (var r in rows) if (r != null) Destroy(r.gameObject);
            rows.Clear();
            if (list == null || rowTemplate == null) { if (rowsRoot != null) rowsRoot.gameObject.SetActive(false); return; }
            if (rowsRoot != null) rowsRoot.gameObject.SetActive(list.Count > 0);
            foreach (var c in list)
            {
                var r = Instantiate(rowTemplate, rowsRoot);
                r.gameObject.SetActive(true);
                r.name = "Candidate_" + c.island + "_" + c.building.id;
                r.Island = c.island; r.BridgeId = c.building.id;
                if (r.icon != null) { var ic = runner.Database.RegionIcon(c.island); r.icon.sprite = ic; r.icon.enabled = ic != null; }
                if (r.label != null) r.label.text = IslandName(c.island) + " bridge - " + Cells(c.distancePx, cell);
                string isl = c.island; int id = c.building.id;
                if (r.sendButton != null) r.sendButton.onClick.AddListener(() => Pair(isl, id, true));
                if (r.receiveButton != null) r.receiveButton.onClick.AddListener(() => Pair(isl, id, false));
                rows.Add(r);
            }
        }
    }
}
