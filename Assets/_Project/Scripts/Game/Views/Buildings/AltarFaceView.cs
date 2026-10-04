using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The Altar (spec §2.4): icon 56 px at 38%; name 800 24 px at 68%; muted "Select an upgrade" at 86%
    /// (upgrade jobs + their gold cost line arrive with the upgrade tree, M5).
    /// </summary>
    public class AltarFaceView : BuildingFace
    {
        [SerializeField] SpriteRenderer icon;
        [SerializeField] TextMeshPro title;
        [SerializeField] TextMeshPro sub;

        public override void Layout(BuildingView v)
        {
            ViewKit.Fit(icon, v.Sync.Sprites.Building(v.Building.type), 56f);
            icon.transform.localPosition = v.L(0.5f, 0.38f);
            title.text = v.Def.name; ViewKit.Font(title, 24f); title.color = UiPalette.Gold;
            title.transform.localPosition = v.L(0.5f, 0.68f);
            title.rectTransform.sizeDelta = new Vector2(v.W, ViewKit.U(28f));
            sub.text = "Select an upgrade"; ViewKit.Font(sub, 12f); sub.color = UiPalette.Muted;
            sub.transform.localPosition = v.L(0.5f, 0.86f);
            sub.rectTransform.sizeDelta = new Vector2(v.W, ViewKit.U(16f));
        }

        public override void Refresh(BuildingView v) { }
    }
}
