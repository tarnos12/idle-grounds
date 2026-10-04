using System.Collections.Generic;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>Lifetime stats (ui-input-render §4.14, ui.js openStats): two-column rows, refreshed while open.</summary>
    public class StatsPanelView : ModalView
    {
        [SerializeField] RectTransform rowsRoot;
        [SerializeField] UiTextRow rowTemplate;

        readonly List<UiTextRow> rows = new List<UiTextRow>();
        float nextRefresh;

        public override void Open()
        {
            if (Sim == null) return;
            base.Open();
            Refresh();
        }

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
            list.Add(("Regions unlocked", unlocked + " / " + Sim.Config.regions.Count));
            list.Add(("Carry capacity", Sim.Hand.Cap().ToString()));
            return list;
        }

        void Refresh()
        {
            nextRefresh = Time.unscaledTime + 1f;
            var data = Rows();
            while (rows.Count < data.Count) rows.Add(Instantiate(rowTemplate, rowsRoot));
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < data.Count;
                rows[i].gameObject.SetActive(on);
                if (on) rows[i].Set(data[i].label, data[i].value);
            }
        }
    }
}
