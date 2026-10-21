using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Base of the M7 modals (ui-input-render §4: scrim rgba(0,0,0,.55) + centred bg-2 box). The scrim is a
    /// raycast target, so world clicks are blocked while open (HandController checks IsPointerOverGameObject).
    /// <para>Every modal fits the free screen area (UI QA batch B #3): the box is centred between the bottom bar and the top
    /// of the safe area; when it is too tall, the view's scrolling body gives up height first (<see cref="ShrinkBody"/>:
    /// the header, tabs and close button stay on screen and the body scrolls); a fixed-size box (Help) is cut to the area
    /// (its body already scrolls); only what still does not fit is scaled down as a last resort.</para>
    /// </summary>
    public abstract class ModalView : MonoBehaviour
    {
        [SerializeField] protected GameRunner runner;
        [SerializeField] protected GameObject modal;
        [SerializeField] protected Button closeButton;

        public bool IsOpen => modal != null && modal.activeSelf;
        protected Simulation Sim => runner != null ? runner.Sim : null;

        RectTransform box;
        ContentSizeFitter boxFitter;
        float designHeight = -1f;
        Vector2 lastCanvas;
        float lastScale;
        bool fitDirty = true;
        /// <summary>Uniform scale applied by the last fit (1 = none; the scrolling body normally makes it unnecessary).</summary>
        public float FitScale { get; private set; } = 1f;
        /// <summary>The modal's box (child "Box" of the modal root), or null.</summary>
        public RectTransform Box
        {
            get
            {
                if (box == null && modal != null) box = modal.transform.Find("Box") as RectTransform;
                return box;
            }
        }

        protected virtual void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (modal != null) modal.SetActive(false);
        }

        public virtual void Open()
        {
            if (modal != null) modal.SetActive(true);
            fitDirty = true;
            FitToScreen();
        }

        public virtual void Close() { if (modal != null) modal.SetActive(false); }

        /// <summary>Esc. True = consumed.</summary>
        public virtual bool Escape()
        {
            if (!IsOpen) return false;
            Close();
            return true;
        }

        /// <summary>Refit after the content changed size (tab switch, new rows).</summary>
        protected void MarkFitDirty() { fitDirty = true; }

        protected virtual void LateUpdate()
        {
            if (!IsOpen || Box == null) return;
            var cr = CanvasRect();
            if (cr == null) return;
            float sc = UiFont.PixelsPerUnit(cr.GetComponent<Canvas>());
            if (fitDirty || cr.rect.size != lastCanvas || !Mathf.Approximately(sc, lastScale)) FitToScreen();
        }

        RectTransform CanvasRect()
        {
            var cv = modal != null ? modal.GetComponentInParent<Canvas>(true) : null;
            return cv != null ? (RectTransform)cv.rootCanvas.transform : null;
        }

        /// <summary>
        /// Called when the box is <paramref name="over"/> canvas units taller than the free area: give up body height (a
        /// scrolling list). Default: nothing to give.
        /// </summary>
        protected virtual void ShrinkBody(float over) { }

        /// <summary>The body takes its full design height again (called before every fit).</summary>
        protected virtual void ResetBody() { }

        /// <summary>Centre the box in the free area and make it fit (see the class summary).</summary>
        public void FitToScreen()
        {
            var b = Box;
            var cr = CanvasRect();
            if (b == null || cr == null || !IsOpen) return;
            fitDirty = false;
            lastCanvas = cr.rect.size;
            lastScale = UiFont.PixelsPerUnit(cr.GetComponent<Canvas>());
            if (boxFitter == null) boxFitter = b.GetComponent<ContentSizeFitter>();
            bool fitted = boxFitter != null && boxFitter.enabled && boxFitter.verticalFit != ContentSizeFitter.FitMode.Unconstrained;
            if (!fitted && designHeight < 0f) designHeight = b.sizeDelta.y;
            b.localScale = Vector3.one;
            var area = UiFit.FreeArea(cr);
            ResetBody();
            if (!fitted) b.sizeDelta = new Vector2(b.sizeDelta.x, Mathf.Floor(Mathf.Min(designHeight, area.height)));
            LayoutRebuilder.ForceRebuildLayoutImmediate(b);
            float over = b.rect.height - area.height;
            if (over > 0.5f)
            {
                ShrinkBody(over);
                LayoutRebuilder.ForceRebuildLayoutImmediate(b);
            }
            float h = Mathf.Max(1f, b.rect.height), w = Mathf.Max(1f, b.rect.width);
            float k = Mathf.Min(1f, area.height / h, area.width / w);
            FitScale = k;
            if (k < 0.999f) b.localScale = new Vector3(k, k, 1f);
            // centred in the free area (box anchors at the centre of its full-screen parent)
            if (b.parent is RectTransform parent)
            {
                Vector2 local = parent.InverseTransformPoint(cr.TransformPoint(area.center));
                float y = local.y - parent.rect.center.y;
                b.anchoredPosition = new Vector2(b.anchoredPosition.x, Mathf.Round(y));
            }
        }
    }
}
