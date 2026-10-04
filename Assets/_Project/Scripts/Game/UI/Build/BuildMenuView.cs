using System.Collections.Generic;
using System.Text;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Build menu (ui-input-render §4.2): a horizontal strip above the bottom bar (wheel scrolls it
    /// sideways), one card per revealed building (<c>Buildings.Catalog()</c>), no categories. Sort rank
    /// on open / reveal: "new" → affordable from the hand → rest (stable by catalogue order); quest
    /// targets (rank 0) arrive with quests (M5). Opening marks every revealed type as listed; hovering a
    /// card marks it seen (clears its "new" pill) and shows "{name} — {role}" in the hint line. If the
    /// revealed set changes while open, card clicks are ignored for 400 ms. Click = placement mode.
    /// </summary>
    public class BuildMenuView : MonoBehaviour
    {
        public const double ClickGuardMs = 400;

        [SerializeField] GameRunner runner;
        [SerializeField] BuildController controller;
        [SerializeField] GameObject panel;
        [SerializeField] RectTransform content;
        [SerializeField] BuildCard cardTemplate;
        [SerializeField] TextMeshProUGUI emptyText;
        [SerializeField] TextMeshProUGUI hintText;
        [Tooltip("Also list not-yet-revealed buildings (dimmed, with their unlock reason). Off = faithful to the original.")]
        [SerializeField] bool showLocked;

        readonly List<BuildCard> cards = new List<BuildCard>();
        SpriteCache sprites;
        string catalogSig;
        double clickLockUntil;
        float restyleAt;

        public bool IsOpen => panel != null && panel.activeSelf;
        public IReadOnlyList<BuildCard> Cards => cards;
        public bool ClickGuardActive => Now < clickLockUntil;
        static double Now => Time.realtimeSinceStartupAsDouble * 1000.0;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (panel != null) panel.SetActive(false);
        }

        Simulation Sim => runner.Sim;

        public void Open()
        {
            if (panel == null || runner == null || runner.Sim == null) return;
            sprites ??= new SpriteCache(runner.Database, null);
            panel.SetActive(true);
            Sim.Buildings.MarkBuildListed();
            catalogSig = Sig();
            clickLockUntil = 0;
            Rebuild();
            if (hintText != null) hintText.text = "";
        }

        public void Close() { if (panel != null) panel.SetActive(false); }

        string Sig()
        {
            var sb = new StringBuilder();
            foreach (var d in Sim.Buildings.Catalog()) sb.Append(d.key).Append(',');
            return sb.ToString();
        }

        void Update()
        {
            if (!IsOpen || runner.Sim == null) return;
            string sig = Sig();
            if (sig != catalogSig)
            {
                catalogSig = sig;
                clickLockUntil = Now + ClickGuardMs;     // the strip reshuffled under the pointer
                Sim.Buildings.MarkBuildListed();          // a reveal while open counts as listed
                Rebuild();
            }
            else if (Time.unscaledTime >= restyleAt) { restyleAt = Time.unscaledTime + 0.25f; Restyle(); }
        }

        struct Row { public BuildingDef def; public int rank, order; public string locked; }

        void Rebuild()
        {
            var rows = new List<Row>();
            var cfg = runner.Config;
            int order = 0;
            foreach (var d in cfg.buildings)
            {
                order++;
                bool unlocked = Sim.Buildings.IsBuildingUnlocked(d.key);
                if (!unlocked && (!showLocked || d.indestructible)) continue;
                if (d.indestructible) continue;      // Altar / Dragon are never in the catalogue's build list
                int rank = !unlocked ? 9 : Sim.Buildings.IsBuildingNew(d.key) ? 1 : Sim.Hand.CanAfford(d.cost) ? 2 : 3;
                rows.Add(new Row { def = d, rank = rank, order = order, locked = unlocked ? null : Sim.Buildings.UnlockReason(d.key) });
            }
            rows.Sort((a, b) => a.rank != b.rank ? a.rank.CompareTo(b.rank) : a.order.CompareTo(b.order));
            while (cards.Count < rows.Count)
            {
                var c = Instantiate(cardTemplate, content);
                cards.Add(c);
            }
            for (int i = 0; i < cards.Count; i++)
            {
                bool on = i < rows.Count;
                cards[i].gameObject.SetActive(on);
                if (!on) continue;
                cards[i].transform.SetSiblingIndex(i + 1);
                BindCard(cards[i], rows[i].def, rows[i].locked);
            }
            if (emptyText != null) emptyText.gameObject.SetActive(rows.Count == 0);
        }

        void BindCard(BuildCard c, BuildingDef d, string locked)
        {
            bool isNew = locked == null && Sim.Buildings.IsBuildingNew(d.key);
            bool afford = locked == null && Sim.Hand.CanAfford(d.cost);
            c.Bind(this, d, sprites.Building(d.key), sprites, isNew, afford, locked, d.name + " — " + Role(d));
        }

        /// <summary>Re-evaluate new / affordable styling without reordering.</summary>
        void Restyle()
        {
            foreach (var c in cards)
            {
                if (!c.gameObject.activeSelf || c.Locked) continue;
                var d = runner.Config.Building(c.Type);
                bool isNew = Sim.Buildings.IsBuildingNew(d.key), afford = Sim.Hand.CanAfford(d.cost);
                if (isNew != c.IsNew || afford != c.Affordable) BindCard(c, d, null);
            }
        }

        public void CardHovered(BuildCard c, bool on)
        {
            if (on && !c.Locked)
            {
                Sim.Buildings.MarkBuildSeen(c.Type);
                BindCard(c, runner.Config.Building(c.Type), null);
            }
            if (hintText != null) hintText.text = on ? c.Tooltip : "";
        }

        public void CardClicked(BuildCard c)
        {
            if (c.Locked || ClickGuardActive) return;
            if (controller != null) controller.BeginPlacing(c.Type);
        }

        /// <summary>`buildRole` ui.js:1496.</summary>
        public string Role(BuildingDef b)
        {
            var cfg = runner.Config;
            string N(string k) => cfg.Item(k)?.name ?? k;
            if (b.IsConverter)
            {
                var seen = new List<string>();
                foreach (var r in b.recipes) { var n = N(r.output); if (!seen.Contains(n)) seen.Add(n); }
                return "Makes " + string.Join(", ", seen);
            }
            if (b.gather.enabled) return $"Vacuums loose items within {b.gather.radius} cells into its buffer";
            if (b.lantern.enabled) return "Hosts wisps that ferry items along its links";
            if (b.seal.enabled) return "Pass-through buffer locked to one item type";
            if (b.stoker.enabled) return $"Stokes fuel into burners whose centre is within {b.stoker.radius + FormationFaceView.StokeSlackCells} cells";
            if (b.roster.enabled) return $"Disciples cultivate {N(b.roster.produce)} (they eat {N(b.roster.food)})";
            if (b.gen.enabled) return $"Grows {N(b.gen.item)} around itself" + (b.waterOnly ? " (water only)" : "");
            if (b.shrine) return "Longer dragon blessings, more Dragon Scales";
            if (b.gate) return "The final monument — Ascend from here";
            if (b.key == "storehouse") return $"Stores up to {b.cap} of one item type";
            return "";
        }
    }
}
