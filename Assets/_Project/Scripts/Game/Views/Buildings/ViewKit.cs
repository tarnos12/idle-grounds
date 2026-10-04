using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>Small helpers shared by world-space building views (world px = 1/32 unit).</summary>
    public static class ViewKit
    {
        /// <summary>TMP world fontSize per world px (fontSize 10 ≈ 1 unit tall; 1 unit = 32 px).</summary>
        public const float FontPerPx = 0.3125f;
        public const float Cell = 32f;

        public static float U(float px) => px / Cell;

        /// <summary>Assign a sprite and scale the renderer so its larger side is <paramref name="px"/> world px.</summary>
        public static void Fit(SpriteRenderer sr, Sprite s, float px)
        {
            if (sr == null) return;
            if (sr.sprite != s) sr.sprite = s;
            float m = s != null ? Mathf.Max(0.0001f, Mathf.Max(s.bounds.size.x, s.bounds.size.y)) : 1f;
            float k = U(px) / m;
            sr.transform.localScale = new Vector3(k, k, 1f);
        }

        public static void Text(TextMeshPro t, string s)
        {
            if (t != null && t.text != s) t.text = s;
        }

        public static void Font(TextMeshPro t, float px) { if (t != null) t.fontSize = px * FontPerPx; }

        public static void Colour(TextMeshPro t, Color c) { if (t != null && t.color != c) t.color = c; }

        public static void Show(Component c, bool on) { if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on); }
        public static void Show(GameObject g, bool on) { if (g != null && g.activeSelf != on) g.SetActive(on); }

        public static Color Rgba(int r, int g, int b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);

        /// <summary>A plain filled bar: renderer uses a 1-unit square sprite (Simple draw mode); left-anchored at x0.</summary>
        public static void Bar(SpriteRenderer sr, float x0, float yCenter, float w, float h)
        {
            if (sr == null) return;
            sr.transform.localPosition = new Vector3(x0 + w * 0.5f, yCenter, 0f);
            sr.transform.localScale = new Vector3(Mathf.Max(0f, w), h, 1f);
        }

        public static string Fmt(double v) => v >= 10 ? System.Math.Round(v).ToString("0", System.Globalization.CultureInfo.InvariantCulture) : v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }
}
