using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Generic hover tooltip (parity-audit L1; replaces the browser <c>title</c>s): one lazily-built chip on the
    /// root Canvas that follows the pointer. uGUI elements use <see cref="TooltipTrigger"/>; world hovers call
    /// <see cref="Show"/> / <see cref="Hide"/> directly with an owner token. Not raycastable.
    /// </summary>
    public class Tooltip : MonoBehaviour
    {
        static Tooltip instance;
        RectTransform rt, canvasRect;
        TextMeshProUGUI label;
        Canvas canvas;
        object owner;

        public static string ShownText => instance != null && instance.rt != null && instance.rt.gameObject.activeSelf ? instance.label.text : null;

        static Tooltip Ensure(TMP_FontAsset font)
        {
            if (instance != null) return instance;
            var cv = GameObject.Find("UI/Canvas");
            var canvas = cv != null ? cv.GetComponent<Canvas>() : Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return null;
            var go = new GameObject("Tooltip", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(canvas.transform, false);
            go.GetComponent<CanvasGroup>().blocksRaycasts = false;
            var t = go.AddComponent<Tooltip>();
            t.canvas = canvas; t.canvasRect = canvas.rootCanvas.GetComponent<RectTransform>();
            t.rt = (RectTransform)go.transform;
            t.rt.anchorMin = t.rt.anchorMax = t.rt.pivot = new Vector2(0f, 1f);
            var img = go.AddComponent<Image>();
            img.color = UiPalette.HandChip; img.raycastTarget = false;
            var ol = go.AddComponent<Outline>(); ol.effectColor = UiPalette.Line; ol.effectDistance = new Vector2(1f, -1f);
            var tgo = new GameObject("Text", typeof(RectTransform));
            tgo.transform.SetParent(go.transform, false);
            t.label = tgo.AddComponent<TextMeshProUGUI>();
            if (font != null) t.label.font = font;
            t.label.fontSize = 20f; t.label.color = UiPalette.Text; t.label.raycastTarget = false;
            t.label.textWrappingMode = TextWrappingModes.Normal;
            t.label.alignment = TextAlignmentOptions.TopLeft;
            var lrt = t.label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(10f, 6f); lrt.offsetMax = new Vector2(-10f, -6f);
            go.SetActive(false);
            instance = t;
            return t;
        }

        /// <summary>Show <paramref name="text"/> at the pointer; <paramref name="who"/> identifies the owner for <see cref="Hide"/>.</summary>
        public static void Show(object who, string text, TMP_FontAsset font = null)
        {
            if (string.IsNullOrEmpty(text)) { Hide(who); return; }
            var t = Ensure(font);
            if (t == null) return;
            t.owner = who;
            t.transform.SetAsLastSibling();
            if (t.label.text != text) t.label.text = text;
            t.Place(true);
        }

        public static void Hide(object who)
        {
            if (instance == null || instance.owner != who) return;
            instance.owner = null;
            instance.rt.gameObject.SetActive(false);
        }

        void Place(bool show)
        {
            // size to text (max 460 px wide), then put it under/right of the pointer, flipped to stay on screen
            var size = label.GetPreferredValues(label.text, 440f, 0f);
            rt.sizeDelta = new Vector2(Mathf.Ceil(size.x) + 20f, Mathf.Ceil(size.y) + 12f);
            if (show && !rt.gameObject.activeSelf) rt.gameObject.SetActive(true);
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            float f = canvas.rootCanvas.scaleFactor;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mouse.position.ReadValue() + new Vector2(16f, -20f) * f, null, out var p);
            var half = canvasRect.rect;           // canvas rect is centred (pivot 0.5)
            float x = p.x, y = p.y;
            if (x + rt.sizeDelta.x > half.xMax) x = half.xMax - rt.sizeDelta.x;
            if (y - rt.sizeDelta.y < half.yMin) y = p.y + 40f + rt.sizeDelta.y;   // flip above the pointer
            rt.anchoredPosition = new Vector2(x - half.xMin, y - half.yMax);
        }

        void LateUpdate()
        {
            if (rt.gameObject.activeSelf) Place(false);
        }
    }

    /// <summary>Put on any raycastable uGUI element to give it a hover <see cref="Tooltip"/> (static text or a live provider).</summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string text;
        public System.Func<string> provider;
        bool over;

        public static TooltipTrigger Attach(Component target, string text, System.Func<string> provider = null)
        {
            if (target == null) return null;
            if (!target.TryGetComponent<TooltipTrigger>(out var t)) t = target.gameObject.AddComponent<TooltipTrigger>();
            t.text = text; t.provider = provider;
            return t;
        }

        TMP_FontAsset Font()
        {
            var tmp = GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp == null) tmp = GetComponentInParent<TextMeshProUGUI>();
            if (tmp == null) tmp = FindFirstObjectByType<TextMeshProUGUI>();
            return tmp != null ? tmp.font : null;
        }

        public void OnPointerEnter(PointerEventData e) { over = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { over = false; Tooltip.Hide(this); }
        void OnDisable() { if (over) { over = false; Tooltip.Hide(this); } }

        void Update()
        {
            if (over && provider != null) Refresh();
        }

        void Refresh() => Tooltip.Show(this, provider != null ? provider() : text, Font());
    }
}
