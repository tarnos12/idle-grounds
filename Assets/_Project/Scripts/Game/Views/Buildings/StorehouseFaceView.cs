using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Storehouse (spec §2.4): item icon 26 px (or the building icon 24 px when empty) at 40%;
    /// "Item ×qty" / "empty" 10 px at 78%, red when qty ≥ cap. A small lock marks a type-locked store.
    /// Real-art variant: a compact pill BELOW the footprint (item icon + "n/cap" + lock) so the art stays clear.
    /// </summary>
    public class StorehouseFaceView : BuildingFace
    {
        [SerializeField] SpriteRenderer itemIcon;
        [SerializeField] TextMeshPro label;
        [SerializeField] SpriteRenderer lockIcon;

        string lastItem = "?";
        SpriteRenderer plate;
        bool real;
        int lastQty = -1, lastCapShown = -1; string lastLabelItem = "?"; bool? lastLocked;

        public override void Layout(BuildingView v)
        {
            lastQty = -1; lastLabelItem = "?"; lastItem = "?"; lastCapShown = -1; lastLocked = null;
            real = v.RealArt;
            if (real)
            {
                if (plate == null) plate = ViewKit.NewSquare(transform, "Plate", label.GetComponent<MeshRenderer>(), -2, ViewKit.PlateColour);
                plate.gameObject.SetActive(true);
                ViewKit.ToOverlay(this);
                ViewKit.Font(label, 9f);
                ViewKit.Outline(label);
                label.rectTransform.sizeDelta = new Vector2(ViewKit.U(60f), ViewKit.U(12f));
                ViewKit.Fit(lockIcon, lockIcon.sprite, 9f);
                return;
            }
            if (plate != null) plate.gameObject.SetActive(false);
            itemIcon.transform.localPosition = v.L(0.5f, 0.40f);
            label.transform.localPosition = v.L(0.5f, 0.78f);
            label.rectTransform.sizeDelta = new Vector2(v.W - ViewKit.U(4f), ViewKit.U(14f));
            ViewKit.Font(label, 10f);
            lockIcon.transform.localPosition = v.L(1f, 0f) + new Vector3(-ViewKit.U(8f), -ViewKit.U(8f), 0f);
            ViewKit.Fit(lockIcon, lockIcon.sprite, 10f);
        }

        void LayoutPill(BuildingView v, bool hasItem, bool locked, string text)
        {
            float iconW = hasItem ? ViewKit.U(13f) : 0f, lockW = locked ? ViewKit.U(10f) : 0f, gap = ViewKit.U(3f);
            float tw = label.GetPreferredValues(text).x;
            float total = iconW + (hasItem ? gap : 0f) + tw + (locked ? gap + lockW : 0f);
            float y = -v.H - ViewKit.U(9f), x = v.W * 0.5f - total * 0.5f;
            plate.transform.localPosition = new Vector3(v.W * 0.5f, y, 0f);
            plate.transform.localScale = new Vector3(total + ViewKit.U(10f), ViewKit.U(14f), 1f);
            if (hasItem) { itemIcon.transform.localPosition = new Vector3(x + iconW * 0.5f, y, 0f); x += iconW + gap; }
            label.transform.localPosition = new Vector3(x + tw * 0.5f, y, 0f);
            x += tw + gap;
            if (locked) lockIcon.transform.localPosition = new Vector3(x + lockW * 0.5f, y, 0f);
        }

        public override void Refresh(BuildingView v)
        {
            var b = v.Building;
            string item = b.qty > 0 || b.locked ? b.item : null;
            if (item != lastItem)
            {
                lastItem = item;
                if (real) ViewKit.Fit(itemIcon, item != null ? v.Sync.Sprites.Item(item) : null, 13f);
                else if (item != null) ViewKit.Fit(itemIcon, v.Sync.Sprites.Item(item), 26f);
                else ViewKit.Fit(itemIcon, v.Sync.Sprites.Building(b.type), 24f);
                ViewKit.Show(itemIcon, item != null || !real);
            }
            int cap = v.Sync.Sim.Buildings.StorehouseCap();
            bool full = b.qty >= cap;
            if (real)
            {
                if (b.qty != lastQty || item != lastLabelItem || cap != lastCapShown || lastLocked != b.locked)
                {
                    lastQty = b.qty; lastLabelItem = item; lastCapShown = cap; lastLocked = b.locked;
                    string txt = b.qty + "/" + cap;
                    ViewKit.Text(label, txt);
                    LayoutPill(v, item != null, b.locked, txt);
                }
                ViewKit.Colour(label, full ? UiPalette.Danger : UiPalette.Text);
                ViewKit.Show(lockIcon, b.locked);
                return;
            }
            string name = item != null ? (v.Sync.Sim.Config.Item(item)?.name ?? item) : null;
            if (b.qty != lastQty || item != lastLabelItem) { lastQty = b.qty; lastLabelItem = item; ViewKit.Text(label, b.qty > 0 && name != null ? name + " ×" + b.qty : "empty"); }
            ViewKit.Colour(label, full ? UiPalette.Danger : UiPalette.Text);
            ViewKit.Show(lockIcon, b.locked);
        }
    }
}
