using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Built Heavenly Gate (ADR 0007; spec §2.4 face of the old Ascension Gate): pulsing gold border
    /// p = 0.5+0.5 sin(now/500) (width (2.5+p) px, glow alpha 0.35+0.4p), icon 56 px at 36%, gold name 16 px at
    /// 64%, "Ascend to Immortality" / "Ascended" 12 px #fde68a at 84% (text rebuilt only when it changes).
    /// </summary>
    public class GateFaceView : BuildingFace
    {
        [SerializeField] SpriteRenderer icon;
        [SerializeField] TextMeshPro title;
        [SerializeField] TextMeshPro sub;
        [SerializeField] EdgeFrame glow;

        int lastAscended = -1;

        public string SubText => sub != null ? sub.text : null;

        public override void Layout(BuildingView v)
        {
            ViewKit.Fit(icon, v.Sync.Sprites.Building(v.Building.type), 56f);
            icon.transform.localPosition = v.L(0.5f, 0.36f);
            ViewKit.Show(icon, !v.RealArt);
            title.text = v.Def.name; ViewKit.Font(title, 16f); title.color = UiPalette.Gold;
            title.transform.localPosition = v.L(0.5f, 0.64f);
            title.rectTransform.sizeDelta = new Vector2(v.W, ViewKit.U(20f));
            sub.text = "Ascend to Immortality"; ViewKit.Font(sub, 12f); sub.color = UiPalette.Hex("#fde68a");
            sub.transform.localPosition = v.L(0.5f, 0.84f);
            sub.rectTransform.sizeDelta = new Vector2(v.W, ViewKit.U(16f));
            lastAscended = -1;
        }
        public override bool IsActive(BuildingView v) => true;


        public override void Refresh(BuildingView v)
        {
            int asc = v.Sync.Sim.State.ascended ? 1 : 0;
            if (asc != lastAscended)
            {
                lastAscended = asc;
                ViewKit.Text(sub, asc == 1 ? "Ascended - click to see the ending" : "Ascend to Immortality");
            }
            float p = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 1000f / 500f);
            var g = UiPalette.Gold;
            v.Border.Set(v.W, v.H, ViewKit.U(2.5f + p), g, false);
            float pad = ViewKit.U(3f + 4f * p);
            glow.transform.localPosition = new Vector3(-pad, pad, 0f);
            glow.Set(v.W + 2 * pad, v.H + 2 * pad, ViewKit.U(3f), new Color(g.r, g.g, g.b, 0.35f + 0.4f * p), false);
        }
    }
}
