using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Built Ascension Gate (spec §2.4): pulsing gold border p = 0.5+0.5 sin(now/500) (width (2.5+p) px,
    /// glow alpha 0.35+0.4p), icon 56 px at 36%, gold name 16 px at 64%, "Ascend · +N ☯" 12 px #fde68a
    /// at 84% (N = <see cref="IdleGrounds.Sim.Simulation.AscendReward"/>, ui.js:1121; sampled at 4 Hz, text
    /// rebuilt only when N changes).
    /// </summary>
    public class GateFaceView : BuildingFace
    {
        [SerializeField] SpriteRenderer icon;
        [SerializeField] TextMeshPro title;
        [SerializeField] TextMeshPro sub;
        [SerializeField] EdgeFrame glow;

        int lastReward = int.MinValue;
        float nextRewardAt;

        public string SubText => sub != null ? sub.text : null;

        public override void Layout(BuildingView v)
        {
            ViewKit.Fit(icon, v.Sync.Sprites.Building(v.Building.type), 56f);
            icon.transform.localPosition = v.L(0.5f, 0.36f);
            ViewKit.Show(icon, !v.RealArt);
            title.text = v.Def.name; ViewKit.Font(title, 16f); title.color = UiPalette.Gold;
            title.transform.localPosition = v.L(0.5f, 0.64f);
            title.rectTransform.sizeDelta = new Vector2(v.W, ViewKit.U(20f));
            sub.text = "Ascend"; ViewKit.Font(sub, 12f); sub.color = UiPalette.Hex("#fde68a");
            sub.transform.localPosition = v.L(0.5f, 0.84f);
            sub.rectTransform.sizeDelta = new Vector2(v.W, ViewKit.U(16f));
            lastReward = int.MinValue; nextRewardAt = 0f;
        }
        public override bool IsActive(BuildingView v) => true;


        public override void Refresh(BuildingView v)
        {
            float t = Time.unscaledTime;
            if (t >= nextRewardAt)
            {
                nextRewardAt = t + 0.25f;
                int n = v.Sync.Sim.AscendReward();
                if (n != lastReward)
                {
                    lastReward = n;
                    ViewKit.Text(sub, "Ascend · +" + n + " ☯");
                }
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
