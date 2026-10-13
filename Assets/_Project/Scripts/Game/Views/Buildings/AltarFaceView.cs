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
        // VP-07: "Select an upgrade" and the job's remaining cost as shared-style tags below the footprint
        PlatedText subTag;
        WorldLabel jobLabel;
        SpriteRenderer jobPlate;
        public string SubShown => subTag != null && subTag.isActiveAndEnabled ? subTag.Shown : null;

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
            if (subTag == null) subTag = PlatedText.Create(transform, "SubTag", WorldLabel.TextPx, PlatedText.Pivot.Below);
            subTag.Label.SetAnchor(new Vector3(v.W * 0.5f, -v.H - ViewKit.U(2f), 0f));
            subTag.Set("Select an upgrade", WorldLabel.MutedColour, PixelSprites.Idle);
            ViewKit.Show(sub, false);
            if (jobRow != null)
            {
                if (jobLabel == null)
                {
                    jobLabel = WorldLabel.Create(transform, "JobTag");
                    jobPlate = WorldLabel.NewPlate(jobLabel.content);
                    jobRow.transform.SetParent(jobLabel.content, false);
                }
                jobLabel.SetAnchor(new Vector3(v.W * 0.5f, -v.H - ViewKit.U(2f), 0f));
                jobRow.transform.localPosition = new Vector3(0f, -ViewKit.U(10f), 0f);
                foreach (var r in jobRow.GetComponentsInChildren<Renderer>(true)) WorldLabel.Sort(r, r is MeshRenderer ? WorldLabel.OrderContent + 1 : WorldLabel.OrderContent);
            }
        }

        public override void Refresh(BuildingView v)
        {
            var sim = v.Sync.Sim;
            var job = sim.State.upgradeJob;
            entries.Clear();
            if (job != null)
                foreach (var e in sim.UpgradeJobRemaining()) if (e.qty > 0) entries.Add(new IconRow.Entry(e.item, e.qty));
            bool on = job != null && jobRow != null;
            subTag.Show(!on);
            if (jobRow != null)
            {
                ViewKit.Show(jobLabel, on);
                ViewKit.Show(jobRow, on);
                if (on)
                {
                    jobRow.Set(entries, 10f, WorldLabel.GoldColour, v.Sync.Sprites, null, v.W - ViewKit.U(8f));
                    float pw = jobRow.Width + ViewKit.U(8f), ph = jobRow.Height + ViewKit.U(5f);
                    WorldLabel.SizePlate(jobPlate, 0f, -ViewKit.U(10f), pw, ph);
                    jobLabel.SetBounds(-pw * 0.5f, -ViewKit.U(10f) - ph * 0.5f, pw, ph);
                }
            }
        }
    }
}
