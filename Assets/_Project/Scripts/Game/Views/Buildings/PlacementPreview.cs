using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Placement ghost (spec §2.8): footprint rect with the hovered cell as its TOP-LEFT cell; ok =
    /// fill rgba(74,222,128,.25) / stroke #4ade80, bad = rgba(248,113,113,.25) / #f87171, 2 px. Bad also
    /// shows a reason pill (rgba(40,10,10,.9), text #fecaca 800 11 px) below the ghost (above it near
    /// the bottom edge). Burners preview their 3x2 fuel rack; stones / spirits their reach circle.
    /// </summary>
    public class PlacementPreview : MonoBehaviour
    {
        public static readonly Color OkFill = ViewKit.Rgba(74, 222, 128, 0.25f), OkStroke = UiPalette.Hex("#4ade80");
        public static readonly Color BadFill = ViewKit.Rgba(248, 113, 113, 0.25f), BadStroke = UiPalette.Hex("#f87171");

        [SerializeField] SpriteRenderer fill;
        [SerializeField] EdgeFrame frame;
        [SerializeField] SpriteRenderer icon;
        [SerializeField] SpriteRenderer rackFill;
        [SerializeField] EdgeFrame rackFrame;
        [SerializeField] Transform reach;
        [SerializeField] SpriteRenderer reachFill;
        [SerializeField] SpriteRenderer reachRing;
        [SerializeField] SpriteRenderer pill;
        [SerializeField] TextMeshPro reasonText;

        public string Reason { get; private set; }
        public bool Valid => Reason == null;

        // ---- VP-20: reason / cost tag in the shared world-label style + per-cell blocked tint ----
        PlatedText tag;
        SpriteRenderer[] cellTints = new SpriteRenderer[0];
        int shownW, shownH;
        float artTop;
        /// <summary>The feedback tag text (reason when blocked, place hint + cost when valid).</summary>
        public string TagText => tag != null && tag.isActiveAndEnabled ? tag.Shown : null;
        public static readonly Color CellBad = PixelSprites.C(PixelSprites.P18, 0.45f), CellOk = PixelSprites.C(PixelSprites.P11, 0.12f);

        /// <summary>Feedback for the current ghost: <paramref name="text"/> (rich text, may be two lines), and which
        /// footprint cells are blocked (row-major w*h; null = none). Call after <see cref="Show"/>; cheap when unchanged.</summary>
        public void SetFeedback(string text, bool ok, bool nearBottom, bool[] blocked)
        {
            if (tag == null) { tag = PlatedText.Create(transform, "Tag", WorldLabel.TextPx, PlatedText.Pivot.Below); tag.Label.priority = 30; tag.Label.hideAboveZoom = 99f; }
            tag.Show(true);
            tag.SetPivot(nearBottom ? PlatedText.Pivot.Above : PlatedText.Pivot.Below);
            tag.Label.SetAnchor(nearBottom ? new Vector3(shownW * 0.5f, artTop + ViewKit.U(3f), 0f) : new Vector3(shownW * 0.5f, -shownH - ViewKit.U(3f), 0f));
            tag.Set(text, WorldLabel.TextColour, ok ? PixelSprites.Ok : PixelSprites.Blocked);
            int n = shownW * shownH;
            if (cellTints.Length < n)
            {
                var old = cellTints; cellTints = new SpriteRenderer[n];
                for (int i = 0; i < old.Length; i++) cellTints[i] = old[i];
            }
            for (int i = 0; i < cellTints.Length; i++)
            {
                bool on = i < n && !ok;
                if (!on) { if (cellTints[i] != null) ViewKit.Show(cellTints[i], false); continue; }
                if (cellTints[i] == null)
                {
                    cellTints[i] = WorldLabel.NewSprite(transform, "Cell" + i, PixelSprites.Pixel, fill.sortingOrder + 1);
                    cellTints[i].sortingLayerID = fill.sortingLayerID;
                }
                var sr = cellTints[i];
                ViewKit.Show(sr, true);
                int r = i / shownW, c = i % shownW;
                sr.transform.localPosition = new Vector3(c + 0.5f, -r - 0.5f, 0f);
                sr.transform.localScale = new Vector3(PixelSprites.Ppu - 2f, PixelSprites.Ppu - 2f, 1f);   // 1-texel gap between cells
                var want = blocked != null && i < blocked.Length && blocked[i] ? CellBad : CellOk;
                if (sr.color != want) sr.color = want;
            }
        }

        /// <summary>Show the preview: world top-left of the footprint, size in cells, reason (null = ok).</summary>
        public void Show(Vector3 topLeft, int w, int h, Sprite iconSprite, string reason, bool burner, float reachCells, Color reachRgb, bool nearBottom, Sprite art = null)
        {
            gameObject.SetActive(true);
            Reason = reason;
            transform.position = topLeft;
            bool ok = reason == null;
            fill.transform.localPosition = new Vector3(w * 0.5f, -h * 0.5f, 0f);
            fill.transform.localScale = new Vector3(w, h, 1f);
            fill.color = ok ? OkFill : new Color(BadFill.r, BadFill.g, BadFill.b, 0.06f);   // blocked cells get their own tint (SetFeedback)
            shownW = w; shownH = h;
            artTop = art != null ? Mathf.Max(0f, art.bounds.size.y * (art.pixelsPerUnit / ViewKit.Cell) - h) : 0f;
            frame.transform.localPosition = Vector3.zero;
            frame.Set(w, h, ViewKit.U(1f), ok ? PixelSprites.C(PixelSprites.P11) : PixelSprites.C(PixelSprites.P18), !ok);
            ViewKit.Fit(icon, iconSprite, w <= 1 && h <= 1 ? 20f : 24f);
            icon.transform.localPosition = new Vector3(w * 0.5f, -h * (w <= 1 && h <= 1 ? 0.5f : 0.38f), 0f);
            var ic = icon.color; ic.a = 0.7f; icon.color = ic;
            ShowArt(art, w, h, ok);

            ViewKit.Show(rackFill, burner);
            ViewKit.Show(rackFrame, burner);
            if (burner)
            {
                rackFill.transform.localPosition = new Vector3(-1.5f, -1f, 0f);
                rackFill.transform.localScale = new Vector3(3f, 2f, 1f);
                rackFill.color = new Color(fill.color.r, fill.color.g, fill.color.b, 0.15f);
                rackFrame.transform.localPosition = new Vector3(-3f, 0f, 0f);
                rackFrame.Set(3f, 2f, ViewKit.U(1.5f), ok ? FuelRackView.PanelBorder : BadStroke, true);
            }

            ViewKit.Show(reach, reachCells > 0);
            if (reachCells > 0)
            {
                reach.localPosition = new Vector3(w * 0.5f, -h * 0.5f, 0f);
                FormationFaceView.PixelRing(reach, reachRing, reachFill, reachCells, reachRgb);
            }

            // the old reason pill is replaced by the shared-style tag (SetFeedback); kept hidden for the prefab
            ViewKit.Show(pill, false);
            ViewKit.Show(reasonText, false);
        }

        SpriteRenderer artSr;

        /// <summary>Delivered body art: drawn semi-transparent (red-tinted when blocked) bottom-aligned on the footprint; replaces the small icon.</summary>
        void ShowArt(Sprite art, int w, int h, bool ok)
        {
            if (art != null && artSr == null)
            {
                var go = new GameObject("Art");
                go.transform.SetParent(transform, false);
                artSr = go.AddComponent<SpriteRenderer>();
                artSr.sortingLayerID = icon.sortingLayerID;
                artSr.sortingOrder = icon.sortingOrder - 1;
            }
            if (artSr != null) ViewKit.Show(artSr, art != null);
            ViewKit.Show(icon, art == null);
            if (art == null) return;
            artSr.sprite = art;
            float k = art.pixelsPerUnit / ViewKit.Cell;
            artSr.transform.localScale = new Vector3(k, k, 1f);
            artSr.transform.localPosition = new Vector3(w * 0.5f, -h, 0f);
            artSr.color = ok ? new Color(1f, 1f, 1f, 0.6f) : new Color(1f, 0.6f, 0.6f, 0.6f);
        }

        public void Hide() { if (gameObject.activeSelf) gameObject.SetActive(false); Reason = null; }
    }
}
