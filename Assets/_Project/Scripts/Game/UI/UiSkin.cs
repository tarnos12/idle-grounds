using IdleGrounds.Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Swaps a placeholder panel Image for delivered 9-slice chrome (db.ui, e.g. "ui_panel_jade").
    /// No real art for the key -> does nothing (the flat placeholder look stays).
    /// Scale choice: the CanvasScaler is 1920x1080 reference (match 0.5) so 1080p = scale 1. Art is 32 PPU and
    /// Image slices borders by canvas.referencePixelsPerUnit / (sprite.PPU * multiplier), so
    /// multiplier = refPPU / (spritePPU * 2) renders 1 art pixel as exactly 2 UI pixels (1.5625 at the default refPPU 100).
    /// Extras: the placeholder Outline is disabled; a LayoutGroup on the same object gets padding >= the frame thickness;
    /// light-on-dark text under a parchment ("ui_panel_scroll") panel is re-inked dark so it stays readable.
    /// </summary>
    [DisallowMultipleComponent]
    public class UiSkin : MonoBehaviour
    {
        public const float UiPxPerArtPx = 2f;
        static readonly Color Ink = new Color(0.22f, 0.13f, 0.06f, 1f);

        [SerializeField] string key = "ui_panel_jade";
        public string Key { get => key; set => key = value; }
        [Tooltip("Shrink the slice scale (min 1x) so the borders fit the rect (pills, bars, scrollbars). Re-evaluated when the rect changes.")]
        [SerializeField] bool fit;
        [Tooltip("Multiplied into the image colour when the art is applied (white = unchanged).")]
        [SerializeField] Color tint = Color.white;
        [Tooltip("Fit against the parent rect instead (progress fills: their own width is the value).")]
        [SerializeField] bool fitParent;
        [Tooltip("UI px per art px (2 = the panel scale; pills / badges use 1 so their 8 px caps fit a text line).")]
        [SerializeField] float uiPerArt = UiPxPerArtPx;
        public float UiPerArt { get => uiPerArt; set => uiPerArt = value; }
        public bool Fit { get => fit; set => fit = value; }
        public Color Tint { get => tint; set => tint = value; }
        public bool FitParent { get => fitParent; set => fitParent = value; }

        bool applied;

        /// <summary>
        /// pixelsPerUnitMultiplier that renders 1 art px as <paramref name="uiPerArt"/> UI px. When <paramref name="fitRect"/> is
        /// given and the rect is too small for the borders at that scale, the scale shrinks (min 1) so the borders still fit.
        /// </summary>
        public static float SliceMultiplier(Image img, Sprite sp, float uiPerArt, RectTransform fitRect = null)
        {
            var canvas = img.canvas;
            float refPpu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            float s = uiPerArt;
            if (fitRect != null)
            {
                var b = sp.border;   // (L,B,R,T)
                float w = fitRect.rect.width, h = fitRect.rect.height;
                if (b.x + b.z > 0f && w > 0f) s = Mathf.Min(s, w / (b.x + b.z));
                if (b.y + b.w > 0f && h > 0f) s = Mathf.Min(s, h / (b.y + b.w));
                s = Mathf.Max(1f, s);
            }
            return refPpu / (sp.pixelsPerUnit * s);
        }

        /// <summary>Add (or update) a UiSkin on <paramref name="go"/>.</summary>
        public static UiSkin Attach(GameObject go, string skinKey, bool fit = false, Color? tint = null, bool fitParent = false, float uiPerArt = UiPxPerArtPx)
        {
            var s = go.GetComponent<UiSkin>();
            if (s == null) s = go.AddComponent<UiSkin>();
            s.key = skinKey;
            s.fit = fit; s.fitParent = fitParent; s.uiPerArt = uiPerArt;
            s.tint = tint ?? Color.white;
            return s;
        }

        /// <summary>Put a delivered sprite on an Image: sliced at <paramref name="uiPerArt"/> UI px per art px when it has borders, else simple. Colour reset to <paramref name="tint"/>.</summary>
        public static void ApplySprite(Image img, Sprite sp, float uiPerArt, RectTransform fitRect, Color tint)
        {
            if (img.sprite != sp) img.sprite = sp;
            var type = sp.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            if (img.type != type) img.type = type;
            if (type == Image.Type.Sliced)
            {
                float m = SliceMultiplier(img, sp, uiPerArt, fitRect);
                if (!Mathf.Approximately(img.pixelsPerUnitMultiplier, m)) img.pixelsPerUnitMultiplier = m;
                img.fillCenter = true;
            }
            if (img.color != tint) img.color = tint;
        }

        /// <summary>Swap to another key (e.g. pill colour by state). Call only on state change.</summary>
        public void SetKey(string newKey)
        {
            if (newKey == key && applied) return;
            key = newKey;
            var runner = GameRunner.Instance;
            if (runner != null && runner.Database != null) applied = Apply(runner.Database);
        }

        void OnRectTransformDimensionsChange()
        {
            if (!applied || !fit) return;
            var img = GetComponent<Image>();
            if (img == null || img.sprite == null || img.type != Image.Type.Sliced) return;
            var fr = fitParent && transform.parent is RectTransform pr ? pr : (RectTransform)transform;
            float m = SliceMultiplier(img, img.sprite, uiPerArt, fr);
            if (!Mathf.Approximately(img.pixelsPerUnitMultiplier, m)) img.pixelsPerUnitMultiplier = m;
        }

        void OnEnable()
        {
            if (!applied) TryApply();
            if (applied && key == "ui_panel_scroll") Invoke(nameof(InkText), 0f);   // content may be (re)built right after the modal opens
        }

        void Start() { if (!applied) TryApply(); }   // GameRunner may Awake after us

        void TryApply()
        {
            var runner = GameRunner.Instance;
            if (runner != null && runner.Database != null) applied = Apply(runner.Database);
        }

        /// <summary>Returns true when real art was applied.</summary>
        public bool Apply(GameDatabase db)
        {
            var img = GetComponent<Image>();
            var sp = db != null ? db.UiSprite(key) : null;
            if (img == null || sp == null) return false;
            var fr = !fit ? null : fitParent && transform.parent is RectTransform pr ? pr : (RectTransform)transform;
            ApplySprite(img, sp, uiPerArt, fr, tint);
            var ol = GetComponent<Outline>();     // the placeholder border; the art has its own frame
            if (ol != null) ol.enabled = false;

            var lg = GetComponent<LayoutGroup>();   // keep content clear of the frame
            if (lg != null && !fit)
            {
                var b = sp.border;   // (L,B,R,T) art px
                var p = lg.padding;
                p.left = Mathf.Max(p.left, Mathf.CeilToInt(b.x * UiPxPerArtPx));
                p.bottom = Mathf.Max(p.bottom, Mathf.CeilToInt(b.y * UiPxPerArtPx));
                p.right = Mathf.Max(p.right, Mathf.CeilToInt(b.z * UiPxPerArtPx));
                p.top = Mathf.Max(p.top, Mathf.CeilToInt(b.w * UiPxPerArtPx));
                lg.padding = p;
            }
            if (key == "ui_panel_scroll" && Application.isPlaying) InkText();   // runtime only: never bake dark text into prefabs
            return true;
        }

        /// <summary>Parchment is light: turn light text dark (neutral -> ink brown, accents -> darkened hue). Buttons keep their text.</summary>
        void InkText()
        {
            foreach (var t in GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.GetComponentInParent<Button>() != null) continue;
                var c = t.color;
                float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                if (max < 0.55f) continue;            // already dark
                t.color = (max - min) < 0.2f ? new Color(Ink.r, Ink.g, Ink.b, c.a) : new Color(c.r * 0.4f, c.g * 0.4f, c.b * 0.4f, c.a);
            }
        }
    }
}
