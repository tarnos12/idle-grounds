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

        public override void Layout(BuildingView v)
        {
            ViewKit.Fit(icon, v.Sync.Sprites.Building(v.Building.type), 24f);
            icon.transform.localPosition = v.L(0.5f, 0.28f);
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
        }

        public override void Refresh(BuildingView v)
        {
            var b = v.Building; var r = v.Def.roster;
            ViewKit.Text(disciples, b.disciples + "/" + r.cap);
            var st = v.Sync.Sim.BuildingStatus(v.Area, b);
            ViewKit.Text(status, st != null ? st.label : "");
            ViewKit.Colour(status, st == null ? UiPalette.Muted : st.state == BuildingState.Full ? UiPalette.Amber : UiPalette.Danger);
            int cap = r.foodCap > 0 ? r.foodCap : 20;
            float frac = b.buns > 0 ? Mathf.Clamp01(b.buns / (float)cap) : 1f;   // empty = full-width red
            ViewKit.Bar(barFill, x0, y, w * frac, h);
            barFill.color = b.buns > 0 ? UiPalette.Accent : UiPalette.Danger;
        }
    }
}
