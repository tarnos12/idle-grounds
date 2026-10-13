using IdleGrounds.Sim;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Meditation Pavilion (spec §2.4): icon 24 px at 28%; disciple icon + "d/cap" 11 px at 50%; status
    /// line at 67%; bun bar at 84% (70% width, 4 px; green fill, red when empty) = buns/foodCap.
    /// </summary>
    public class PavilionFaceView : BuildingFace
    {
        [SerializeField] SpriteRenderer icon;
        [SerializeField] SpriteRenderer discipleIcon;
        [SerializeField] TextMeshPro disciples;
        [SerializeField] TextMeshPro status;
        [SerializeField] SpriteRenderer barTrack;
        [SerializeField] SpriteRenderer barFill;

        float x0, w, y, h;
        PlatedText tag;
        bool real;
        public string TagText => tag != null && tag.isActiveAndEnabled ? tag.Shown : null;

        public override void Layout(BuildingView v)
        {
            lastDisc = -1; lastCap = -1;
            ViewKit.Fit(icon, v.Sync.Sprites.Building(v.Building.type), 24f);
            icon.transform.localPosition = v.L(0.5f, 0.28f);
            ViewKit.Show(icon, !v.RealArt);
            ViewKit.Fit(discipleIcon, discipleIcon.sprite, 12f);
            discipleIcon.transform.localPosition = v.L(0.5f, 0.5f) - new Vector3(ViewKit.U(14f), 0f, 0f);
            disciples.transform.localPosition = v.L(0.5f, 0.5f) + new Vector3(ViewKit.U(6f), 0f, 0f);
            ViewKit.Font(disciples, 11f);
            status.transform.localPosition = v.L(0.5f, 0.67f);
            status.rectTransform.sizeDelta = new Vector2(v.W - ViewKit.U(4f), ViewKit.U(12f));
            ViewKit.Font(status, 9.5f);
            w = 0.7f * v.W; h = ViewKit.U(4f); x0 = (v.W - w) * 0.5f; y = -0.84f * v.H;
            ViewKit.Bar(barTrack, x0, y, w, h);
            barTrack.color = new Color(1f, 1f, 1f, 0.12f);
            // real art: one tag below the footprint (status shape + disciple icon + d/cap + status) instead of text over the roof
            real = v.RealArt;
            if (real)
            {
                if (tag == null) tag = PlatedText.Create(transform, "Tag", WorldLabel.TextPx, PlatedText.Pivot.Below);
                tag.Show(true);
                tag.Label.SetAnchor(new Vector3(v.W * 0.5f, -v.H - ViewKit.U(2f), 0f));
            }
            else if (tag != null) tag.Show(false);
            ViewKit.Show(disciples, !real); ViewKit.Show(discipleIcon, !real); ViewKit.Show(status, !real);
            ViewKit.Show(barTrack, !real); ViewKit.Show(barFill, !real);
            lastTagState = -2;
        }

        int lastDisc = -1, lastCap = -1, lastTagState = -2; string lastTagLabel, tagText = ""; readonly BuildingStatusInfo stBuf = new BuildingStatusInfo();
        public override bool IsActive(BuildingView v) => v.Building.disciples > 0;


        public override void Refresh(BuildingView v)
        {
            var b = v.Building; var r = v.Def.roster;
            if (b.disciples != lastDisc || r.cap != lastCap) { lastDisc = b.disciples; lastCap = r.cap; ViewKit.Text(disciples, b.disciples + "/" + r.cap); lastTagState = -2; }
            var st = v.Sync.Sim.BuildingStatus(v.Area, b, stBuf) ? stBuf : null;
            ViewKit.Text(status, st != null ? st.label : "");
            ViewKit.Colour(status, st == null ? UiPalette.Muted : st.state == BuildingState.Full ? UiPalette.Amber : UiPalette.Danger);
            if (real)
            {
                int ts = st != null ? (int)st.state : -1; string tl = st != null ? st.label : null;
                if (ts != lastTagState || tl != lastTagLabel)
                {
                    lastTagState = ts; lastTagLabel = tl;
                    tagText = st != null && !string.IsNullOrEmpty(st.label) ? disciples.text + "  " + st.label : disciples.text;
                }
                var col = st == null ? WorldLabel.TextColour : st.state == BuildingState.Full ? WorldLabel.WarnColour : st.state == BuildingState.Working ? WorldLabel.GoodColour : WorldLabel.BadColour;
                tag.Set(tagText, col, PixelSprites.ForState(st != null ? st.state : (BuildingState?)null), discipleIcon.sprite);
                return;
            }
            int cap = r.foodCap > 0 ? r.foodCap : 20;
            float frac = b.buns > 0 ? Mathf.Clamp01(b.buns / (float)cap) : 1f;   // empty = full-width red
            ViewKit.Bar(barFill, x0, y, w * frac, h);
            barFill.color = b.buns > 0 ? UiPalette.Accent : UiPalette.Danger;
        }
    }
}
