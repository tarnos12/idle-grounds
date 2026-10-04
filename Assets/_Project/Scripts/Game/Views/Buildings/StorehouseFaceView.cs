using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Storehouse (spec §2.4): item icon 26 px (or the building icon 24 px when empty) at 40%;
    /// "Item ×qty" / "empty" 10 px at 78%, red when qty ≥ cap. A small lock marks a type-locked store.
    /// </summary>
    public class StorehouseFaceView : BuildingFace
    {
        [SerializeField] SpriteRenderer itemIcon;
        [SerializeField] TextMeshPro label;
        [SerializeField] SpriteRenderer lockIcon;

        string lastItem = "?";

        public override void Layout(BuildingView v)
        {
            lastQty = -1; lastLabelItem = "?";
            itemIcon.transform.localPosition = v.L(0.5f, 0.40f);
            label.transform.localPosition = v.L(0.5f, 0.78f);
            label.rectTransform.sizeDelta = new Vector2(v.W - ViewKit.U(4f), ViewKit.U(14f));
            ViewKit.Font(label, 10f);
            lockIcon.transform.localPosition = v.L(1f, 0f) + new Vector3(-ViewKit.U(8f), -ViewKit.U(8f), 0f);
            ViewKit.Fit(lockIcon, lockIcon.sprite, 10f);
            lastItem = "?";
        }

        int lastQty = -1; string lastLabelItem = "?";

        public override void Refresh(BuildingView v)
        {
            var b = v.Building;
            string item = b.qty > 0 || b.locked ? b.item : null;
            if (item != lastItem)
            {
                lastItem = item;
                if (item != null) ViewKit.Fit(itemIcon, v.Sync.Sprites.Item(item), 26f);
                else ViewKit.Fit(itemIcon, v.Sync.Sprites.Building(b.type), 24f);
            }
            int cap = v.Sync.Sim.Buildings.StorehouseCap();
            string name = item != null ? (v.Sync.Sim.Config.Item(item)?.name ?? item) : null;
            if (b.qty != lastQty || item != lastLabelItem) { lastQty = b.qty; lastLabelItem = item; ViewKit.Text(label, b.qty > 0 && name != null ? name + " ×" + b.qty : "empty"); }
            ViewKit.Colour(label, b.qty >= cap ? UiPalette.Danger : UiPalette.Text);
            ViewKit.Show(lockIcon, b.locked);
        }
    }
}
