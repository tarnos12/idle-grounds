using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Layout helpers for the pixel-font UI (UI QA batch B fixes, 2026-10-21). Text is snapped to whole font pixels ON SCREEN
    /// (<see cref="UiFont"/>), so on a small desktop window (1280x720: canvas scale 0.67) every text is ~1.5x larger relative to
    /// the 1920x1080 layout it was built for. Fixed button widths and panel widths therefore have to follow the text:
    /// <see cref="FitButton"/> sizes a button to its label, <see cref="TextGrowth"/> says how much wider a text panel must be,
    /// and <see cref="FreeArea"/> is the screen space a centred window may use (safe area minus the bottom bar).
    /// </summary>
    public static class UiFit
    {
        /// <summary>Gap above the bottom bar and below the top edge for centred windows (canvas units).</summary>
        public const float BottomGap = 10f, TopGap = 16f, SideGap = 12f;

        /// <summary>
        /// How much larger body text is relative to the 1080p layout: 1 at 1920x1080 and on phones, 1.5 at 1280x720
        /// (22 screen px stays 22 px while the canvas shrinks), times how much wider the chosen body face is than Pixelify
        /// (Text font setting: Press Start 2P). Use it to widen text columns, never to scale art.
        /// </summary>
        public static float TextGrowth(Canvas anyCanvas)
        {
            var root = anyCanvas != null ? anyCanvas.rootCanvas : null;
            float ppu = UiFont.PixelsPerUnit(root);
            float wide = Mathf.Max(1f, UiFont.BodyWidthFactor);
            if (MobileUi.IsMobile || ppu <= 0f) return wide;
            return Mathf.Max(1f, 1f / ppu) * wide;
        }

        /// <summary>
        /// Width a button needs for its label at the snapped size (+ <paramref name="pad"/> on both sides together); the button's
        /// LayoutElement grows to it (never shrinks below <paramref name="minWidth"/>). True = the width changed.
        /// </summary>
        public static bool FitButton(Component button, float minWidth = 0f, float pad = 32f)
        {
            if (button == null) return false;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return false;
            float w = Mathf.Ceil(WidestText(label, label.text) + pad);
            var le = button.GetComponent<LayoutElement>();
            if (le == null) le = button.gameObject.AddComponent<LayoutElement>();
            if (minWidth <= 0f) minWidth = le.minWidth > 0f ? le.minWidth : 0f;
            float want = Mathf.Max(minWidth, w);
            if (Mathf.Approximately(le.preferredWidth, want) && Mathf.Approximately(le.minWidth, want)) return false;
            le.preferredWidth = want; le.minWidth = want;
            return true;
        }

        /// <summary>
        /// A row (HorizontalLayoutGroup) where <paramref name="flex"/> takes the room left and wraps, and every item in
        /// <paramref name="keep"/> keeps its full width (buttons, a short count): wide faces (Press Start 2P) and 720p no longer
        /// run one label into the next. Call after the texts changed.
        /// </summary>
        public static void ShareRow(TMP_Text flex, params Component[] keep)
        {
            if (flex == null) return;
            flex.textWrappingMode = TextWrappingModes.Normal;
            var le = flex.GetComponent<LayoutElement>() ?? flex.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f; le.minWidth = 0f;
            foreach (var k in keep)
            {
                if (k == null) continue;
                var kl = k.GetComponent<LayoutElement>() ?? k.gameObject.AddComponent<LayoutElement>();
                float w = k is TMP_Text t ? Mathf.Ceil(WidestText(t, t.text)) : (kl.preferredWidth > 0f ? kl.preferredWidth : ((RectTransform)k.transform).rect.width);
                if (k is Button) { var lab = k.GetComponentInChildren<TMP_Text>(true); if (lab != null && lab.gameObject.activeInHierarchy) w = Mathf.Max(w, WidestText(lab, lab.text) + 24f); }
                kl.minWidth = Mathf.Max(kl.minWidth, w);
                if (kl.preferredWidth < kl.minWidth) kl.preferredWidth = kl.minWidth;
            }
        }

        /// <summary>Widest label among <paramref name="texts"/> at <paramref name="label"/>'s font and size (canvas units).</summary>
        public static float WidestText(TMP_Text label, params string[] texts)
        {
            if (label == null || !label.gameObject.activeInHierarchy) return 0f;     // TMP cannot measure an inactive text
            UiFont.Apply(label);
            // measure at the size the label will have on this canvas (the snap may not have run yet for a hidden label)
            float keep = label.fontSize;
            float ppu = UiFont.PixelsPerUnit(label.canvas != null ? label.canvas.rootCanvas : null);
            if (!label.enableAutoSizing && ppu > 0f && label is TextMeshProUGUI)
            {
                float authored = label.TryGetComponent<PixelTextSize>(out var rec) && rec.authored > 0f ? rec.authored : keep;
                label.fontSize = UiFont.SnappedScreenPx(label, authored, ppu) / ppu;
            }
            float w = 0f;
            foreach (var s in texts) w = Mathf.Max(w, label.GetPreferredValues(s, 9999f, 9999f).x);
            if (!Mathf.Approximately(label.fontSize, keep)) label.fontSize = keep;
            return Mathf.Ceil(w);
        }

        /// <summary>
        /// The rect (root-canvas local units, centre origin) a centred window may use: the canvas inside the safe-area insets,
        /// above the bottom bar (+ <see cref="BottomGap"/>) and <see cref="TopGap"/> below the top.
        /// </summary>
        public static Rect FreeArea(RectTransform canvasRect)
        {
            if (canvasRect == null) return new Rect(-960f, -540f, 1920f, 1080f);
            var cv = canvasRect.GetComponent<Canvas>();
            float cs = Mathf.Max(0.01f, cv != null ? cv.scaleFactor : 1f);
            var ins = MobileUi.IsMobile ? MobileUi.SafeInsets() : Vector4.zero;
            var r = canvasRect.rect;
            var bar = BottomBarView.Instance;
            float barH = bar != null && bar.isActiveAndEnabled ? ((RectTransform)bar.transform).rect.height : 0f;
            float bottom = r.yMin + barH + ins.y / cs + BottomGap, top = r.yMax - ins.w / cs - TopGap;
            float left = r.xMin + ins.x / cs + SideGap, right = r.xMax - ins.z / cs - SideGap;
            return Rect.MinMaxRect(left, bottom, Mathf.Max(left + 1f, right), Mathf.Max(bottom + 1f, top));
        }
    }
}
