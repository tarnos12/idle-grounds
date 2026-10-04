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

        /// <summary>Show the preview: world top-left of the footprint, size in cells, reason (null = ok).</summary>
        public void Show(Vector3 topLeft, int w, int h, Sprite iconSprite, string reason, bool burner, float reachCells, Color reachRgb, bool nearBottom, Sprite art = null)
        {
            gameObject.SetActive(true);
            Reason = reason;
            transform.position = topLeft;
            bool ok = reason == null;
            fill.transform.localPosition = new Vector3(w * 0.5f, -h * 0.5f, 0f);
            fill.transform.localScale = new Vector3(w, h, 1f);
            fill.color = ok ? OkFill : BadFill;
            frame.transform.localPosition = Vector3.zero;
            frame.Set(w, h, ViewKit.U(2f), ok ? OkStroke : BadStroke, false);
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
                reach.localScale = new Vector3(2f * reachCells, 2f * reachCells, 1f);
                reachFill.color = new Color(reachRgb.r, reachRgb.g, reachRgb.b, 0.07f);
                reachRing.color = new Color(reachRgb.r, reachRgb.g, reachRgb.b, 0.5f);
            }

            ViewKit.Show(pill, !ok);
            ViewKit.Show(reasonText, !ok);
            if (!ok)
            {
                ViewKit.Font(reasonText, 11f);
                ViewKit.Text(reasonText, reason);
                float tw = reasonText.GetPreferredValues(reason).x + ViewKit.U(14f);
                float ph = ViewKit.U(20f);
                float y = nearBottom ? ViewKit.U(6f) + ph * 0.5f : -h - ViewKit.U(6f) - ph * 0.5f;
                pill.transform.localPosition = new Vector3(w * 0.5f, y, 0f);
                pill.size = new Vector2(tw, ph);
                reasonText.transform.localPosition = new Vector3(w * 0.5f, y, 0f);
            }
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
