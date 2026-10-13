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
        PlatedText tag;
        bool real;
        /// <summary>Real-art counter tag (shared WorldLabel style).</summary>
        public string TagText => tag != null && tag.isActiveAndEnabled ? tag.Shown : null;
        int lastQty = -1, lastCapShown = -1; string lastLabelItem = "?"; bool? lastLocked;

        public override void Layout(BuildingView v)
        {
            lastQty = -1; lastLabelItem = "?"; lastItem = "?"; lastCapShown = -1; lastLocked = null;
            real = v.RealArt;
            if (real)
            {
                if (tag == null) tag = PlatedText.Create(transform, "Tag", WorldLabel.TextPx, PlatedText.Pivot.Below);
                tag.Show(true);
                tag.Label.hideAboveZoom = 4.5f;
                tag.Label.SetAnchor(new Vector3(v.W * 0.5f, -v.H - ViewKit.U(2f), 0f));
                ViewKit.Show(label, false); ViewKit.Show(itemIcon, false); ViewKit.Show(lockIcon, false);
                return;
            }
            if (tag != null) tag.Show(false);
            ViewKit.Show(label, true);
            itemIcon.transform.localPosition = v.L(0.5f, 0.40f);
            label.transform.localPosition = v.L(0.5f, 0.78f);
            label.rectTransform.sizeDelta = new Vector2(v.W - ViewKit.U(4f), ViewKit.U(14f));
            ViewKit.Font(label, 10f);
            lockIcon.transform.localPosition = v.L(1f, 0f) + new Vector3(-ViewKit.U(8f), -ViewKit.U(8f), 0f);
            ViewKit.Fit(lockIcon, lockIcon.sprite, 10f);
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
                ViewKit.Show(itemIcon, !real);
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
                    // full = red text plus the crate shape (never colour alone)
                    tag.Set(txt, full ? WorldLabel.BadColour : WorldLabel.TextColour, full ? PixelSprites.Box : null,
                        item != null ? v.Sync.Sprites.Item(item) : null, b.locked ? lockIcon.sprite : null);
                }
                return;
            }
            string name = item != null ? (v.Sync.Sim.Config.Item(item)?.name ?? item) : null;
            if (b.qty != lastQty || item != lastLabelItem) { lastQty = b.qty; lastLabelItem = item; ViewKit.Text(label, b.qty > 0 && name != null ? name + " ×" + b.qty : "empty"); }
            ViewKit.Colour(label, full ? UiPalette.Danger : UiPalette.Text);
            ViewKit.Show(lockIcon, b.locked);
        }
    }
}
