using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The Altar (spec §2.4): icon 56 px at 38%; name 800 24 px at 68%; with an upgrade job a gold
    /// remaining-cost line (20 px) at 86% (fed by right-clicking the Altar), else muted
    /// "Select an upgrade" (left-click opens the upgrade tree).
    /// </summary>
    public class AltarFaceView : BuildingFace
    {
        [SerializeField] SpriteRenderer icon;
        [SerializeField] TextMeshPro title;
        [SerializeField] TextMeshPro sub;
        [SerializeField] IconRow jobRow;

        readonly List<IconRow.Entry> entries = new List<IconRow.Entry>();

        public bool JobShown => jobRow != null && jobRow.gameObject.activeSelf;

        public override void Layout(BuildingView v)
        {
            ViewKit.Fit(icon, v.Sync.Sprites.Building(v.Building.type), 56f);
            icon.transform.localPosition = v.L(0.5f, 0.38f);
            ViewKit.Show(icon, !v.RealArt);
            title.text = v.Def.name; ViewKit.Font(title, 24f); title.color = UiPalette.Gold;
            title.transform.localPosition = v.L(0.5f, 0.68f);
            title.rectTransform.sizeDelta = new Vector2(v.W, ViewKit.U(28f));
            ViewKit.Show(title, !v.RealArt);   // the real art is self-explanatory; the name stays in the hover tooltip
            sub.text = "Select an upgrade"; ViewKit.Font(sub, 10f); sub.color = UiPalette.Muted; ViewKit.Outline(sub, 0.2f);
            sub.transform.localPosition = new Vector3(v.W * 0.5f, -v.H - ViewKit.U(8f), 0f);   // below the footprint, out of the art
            sub.rectTransform.sizeDelta = new Vector2(v.W, ViewKit.U(12f));
            if (jobRow != null) { jobRow.transform.localPosition = new Vector3(v.W * 0.5f, -v.H - ViewKit.U(12f), 0f); jobRow.Clear(); }
        }

        public override void Refresh(BuildingView v)
        {
            var sim = v.Sync.Sim;
            var job = sim.State.upgradeJob;
            entries.Clear();
            if (job != null)
                foreach (var e in sim.UpgradeJobRemaining()) if (e.qty > 0) entries.Add(new IconRow.Entry(e.item, e.qty));
            bool on = job != null && jobRow != null;
            ViewKit.Show(sub, !on);
            if (jobRow != null)
            {
                ViewKit.Show(jobRow, on);
                if (on) jobRow.Set(entries, 12f, UiPalette.Gold, v.Sync.Sprites, null, v.W - ViewKit.U(8f));
            }
        }
    }
}
