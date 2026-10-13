using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Built Heavenly Gate (ADR 0007). VP-06: ONE readable tag in the shared world-label style (dark plate,
    /// outline) BELOW the footprint, title on line 1 (gold) and the hint on line 2 ("Click: Ascend to
    /// Immortality" / "Ascended - click to see the ending"), so it never sits on the pale Celestial ground
    /// unplated and never meets the hover name tag (that one anchors to the top edge, VP-19). The pulsing
    /// gold footprint frame stays only for placeholder art (with delivered art it read as an editor gizmo).
    /// </summary>
    public class GateFaceView : BuildingFace
    {
        public const string HintOpen = "Click: Ascend to Immortality", HintDone = "Ascended - click to see the ending";
        static readonly string GoldHex = ColorUtility.ToHtmlStringRGB(WorldLabel.GoldColour);

        [SerializeField] SpriteRenderer icon;
        [SerializeField] TextMeshPro title;
        [SerializeField] TextMeshPro sub;
        [SerializeField] EdgeFrame glow;

        PlatedText tag;
        int lastAscended = -1;
        bool real;
        string titleText = "";

        /// <summary>The hint line currently shown.</summary>
        public string SubText => sub != null ? sub.text : null;
        public string TagText => tag != null ? tag.Shown : null;

        public override void Layout(BuildingView v)
        {
            real = v.RealArt;
            ViewKit.Fit(icon, v.Sync.Sprites.Building(v.Building.type), 56f);
            icon.transform.localPosition = v.L(0.5f, 0.36f);
            ViewKit.Show(icon, !real);
            titleText = v.Def != null ? v.Def.name : "Heavenly Gate";
            title.text = titleText;
            ViewKit.Show(title, false);
            ViewKit.Show(sub, false);
            if (glow != null) ViewKit.Show(glow, !real);
            if (tag == null) tag = PlatedText.Create(transform, "Tag", WorldLabel.TextPx, PlatedText.Pivot.Below);
            tag.Label.SetAnchor(new Vector3(v.W * 0.5f, -v.H - ViewKit.U(3f), 0f));
            tag.Label.priority = 20;
            tag.Label.hideAboveZoom = 99f;      // the ending building stays named at every zoom
            tag.Text.lineSpacing = -8f;
            lastAscended = -1;
        }

        public override bool IsActive(BuildingView v) => true;

        public override void Refresh(BuildingView v)
        {
            int asc = v.Sync.Sim.State.ascended ? 1 : 0;
            if (asc != lastAscended)
            {
                lastAscended = asc;
                string hint = asc == 1 ? HintDone : HintOpen;
                ViewKit.Text(sub, hint);
                tag.Set("<size=125%><color=#" + GoldHex + ">" + titleText + "</color></size>\n" + hint, WorldLabel.TextColour);
            }
            if (real) return;
            float p = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 1000f / 500f);
            var g = UiPalette.Gold;
            v.Border.Set(v.W, v.H, ViewKit.U(2.5f + p), g, false);
            float pad = ViewKit.U(3f + 4f * p);
            glow.transform.localPosition = new Vector3(-pad, pad, 0f);
            glow.Set(v.W + 2 * pad, v.H + 2 * pad, ViewKit.U(3f), new Color(g.r, g.g, g.b, 0.35f + 0.4f * p), false);
        }
    }
}
