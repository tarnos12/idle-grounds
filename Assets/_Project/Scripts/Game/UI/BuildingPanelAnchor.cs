using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Building panels open ON the building being used (owner pick vp_panels, 2026-10-21: "All pop up panels should be on
    /// the building we interact with"). Put on a panel's RectTransform (the views attach it themselves): after
    /// <see cref="Bind"/> the panel sits beside the building's art (right, else left, else above, else below), follows it
    /// while the camera pans or zooms, and is clamped to the safe area above the bottom bar. A side is kept while it still
    /// fits (no flip-flopping while panning). When no side fits (a phone, or a panel taller than the space) the panel
    /// docks to the top or bottom screen edge away from the building.
    /// <para>Never shrunk below the text floor (UI QA batch B #10): a panel wider than the space takes the space's width
    /// (its text wraps); a panel taller than the space keeps its title bar and the rest scrolls (the body is wrapped in a
    /// ScrollRect the first time that happens). Only a panel that cannot scroll (no vertical layout) still shrinks.</para>
    /// <para>Leaving the screen (UI QA batch B #8): once the building has been on screen, the panel closes when the building
    /// is panned or zoomed completely out of view (<see cref="OnLeftScreen"/>, default: hide the panel) unless
    /// <see cref="KeepOffScreen"/> says the player is still using it (the link editor while picking a source or target).
    /// A panel never points at nothing.</para>
    /// Global windows (Stats, Help, the Altar tree, Menu) stay centred and do not use this.
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildingPanelAnchor : MonoBehaviour
    {
        public enum Side { None, Right, Left, Above, Below, DockTop, DockBottom }

        /// <summary>Gap between the building art and the panel, and the margin to the screen edges (canvas units).</summary>
        public const float Gap = 14f, Margin = 8f;

        [Tooltip("Panel width in canvas units (the height follows the content); 0 = keep the panel's own (fitted) size.")]
        [SerializeField] float width = 640f;
        [Tooltip("Small popups (recipe picker) try above the building first, then right / left / below.")]
        [SerializeField] bool preferAbove;

        public Side Current { get; private set; }
        public string Area { get; private set; }
        public int BuildingId { get; private set; }
        public bool Bound => Area != null;
        /// <summary>Last placement in root-canvas local units (panel rect and building rect), for tests.</summary>
        public Rect PanelRect { get; private set; }
        public Rect TargetRect { get; private set; }

        RectTransform rt, canvasRect;
        Canvas canvas;
        Camera worldCam;
        GameRunner runner;
        BuildingViewSync views;
        QuestPanelView quest;
        Rect obstacle;            // the quest panel / chip in root-canvas units (zero = none)
        bool converted;
        readonly Vector3[] corners = new Vector3[4];

        /// <summary>Attach (or fetch) the anchor on <paramref name="panel"/> with a panel width in canvas units.</summary>
        public static BuildingPanelAnchor Attach(GameObject panel, float width, bool preferAbove = false)
        {
            if (panel == null) return null;
            if (!panel.TryGetComponent<BuildingPanelAnchor>(out var a)) a = panel.AddComponent<BuildingPanelAnchor>();
            a.width = width; a.preferAbove = preferAbove;
            return a;
        }

        /// <summary>Called when the building leaves the screen (null = hide the panel). The views pass their Close.</summary>
        public System.Action OnLeftScreen;
        /// <summary>True = keep the panel while its building is off screen (the player is working with it elsewhere).</summary>
        public System.Func<bool> KeepOffScreen;
        /// <summary>The building has been on screen since <see cref="Bind"/> (only then does leaving the screen close the panel).</summary>
        public bool SeenOnScreen { get; private set; }
        /// <summary>Uniform scale of the last placement (1 unless the panel cannot scroll).</summary>
        public float Shrink { get; private set; } = 1f;
        /// <summary>The panel body scrolls (it was taller than the space at least once).</summary>
        public bool Scrolls => scrollLe != null;

        /// <summary>Anchor to building <paramref name="buildingId"/> on <paramref name="area"/> and place at once.</summary>
        public void Bind(string area, int buildingId)
        {
            Area = area; BuildingId = buildingId; Current = Side.None;
            SeenOnScreen = false;
            Init();
            Convert();
            heightCheck = 0;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Place();
        }

        public void Unbind() { Area = null; }

        void Init()
        {
            rt = (RectTransform)transform;
            if (runner == null) runner = GameRunner.Instance;
            if (worldCam == null) worldCam = Camera.main;
            if (canvas == null) { canvas = GetComponentInParent<Canvas>(); if (canvas != null) canvas = canvas.rootCanvas; }
            if (canvas != null) canvasRect = (RectTransform)canvas.transform;
            if (views == null) views = FindFirstObjectByType<BuildingViewSync>();
            if (quest == null) quest = FindFirstObjectByType<QuestPanelView>();
        }

        /// <summary>Bottom-dock layout -> a free, centre-pivoted panel of fixed width (the content sets the height).</summary>
        void Convert()
        {
            if (converted) return;
            converted = true;
            var dock = GetComponent<UiDock>();
            if (dock != null) Destroy(dock);           // no longer a bottom dock: the hand chip need not park above it
            var size = rt.rect.size;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width > 0f ? width : size.x, size.y);
        }

        // ---------------------------------------------------------------- fit (no shrinking below the text floor)

        LayoutElement scrollLe;
        RectTransform scrollContent;
        bool wrapFailed;
        int heightCheck;
        float lastAreaH = -1f;

        /// <summary>Everything under the first layout child (the title bar) moves into a vertical ScrollRect.</summary>
        bool WrapForScroll()
        {
            if (scrollLe != null) return true;
            if (wrapFailed) return false;
            var vlg = GetComponent<VerticalLayoutGroup>();
            var csf = GetComponent<ContentSizeFitter>();
            int first = -1;
            for (int i = 0; i < rt.childCount; i++)
            {
                var c = rt.GetChild(i);
                if (!c.gameObject.activeSelf) continue;
                var le = c.GetComponent<LayoutElement>();
                if (le != null && le.ignoreLayout) continue;
                first = i; break;
            }
            if (vlg == null || csf == null || csf.verticalFit != ContentSizeFitter.FitMode.PreferredSize || first < 0 || first >= rt.childCount - 1)
            {
                wrapFailed = true;
                return false;
            }
            var moving = new System.Collections.Generic.List<Transform>();
            for (int i = first + 1; i < rt.childCount; i++)
            {
                var c = rt.GetChild(i);
                var le = c.GetComponent<LayoutElement>();
                if (le != null && le.ignoreLayout) continue;       // badges / overlays stay where they are
                moving.Add(c);
            }
            var body = new GameObject("ScrollBody", typeof(RectTransform)).GetComponent<RectTransform>();
            body.SetParent(rt, false);
            body.SetSiblingIndex(first + 1);
            scrollLe = body.gameObject.AddComponent<LayoutElement>();
            scrollLe.flexibleWidth = 1f;
            var vp = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            vp.SetParent(body, false);
            vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one; vp.offsetMin = vp.offsetMax = Vector2.zero;
            var vimg = vp.gameObject.AddComponent<Image>(); vimg.color = new Color(0f, 0f, 0f, 0f); vimg.raycastTarget = true;
            vp.gameObject.AddComponent<RectMask2D>();
            scrollContent = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            scrollContent.SetParent(vp, false);
            scrollContent.anchorMin = new Vector2(0f, 1f); scrollContent.anchorMax = new Vector2(1f, 1f); scrollContent.pivot = new Vector2(0.5f, 1f);
            scrollContent.anchoredPosition = Vector2.zero; scrollContent.sizeDelta = Vector2.zero;
            var cl = scrollContent.gameObject.AddComponent<VerticalLayoutGroup>();
            cl.spacing = vlg.spacing; cl.childAlignment = vlg.childAlignment;
            cl.childControlWidth = vlg.childControlWidth; cl.childControlHeight = vlg.childControlHeight;
            cl.childForceExpandWidth = vlg.childForceExpandWidth; cl.childForceExpandHeight = false;
            cl.padding = new RectOffset(0, 0, 0, 0);
            var cf = scrollContent.gameObject.AddComponent<ContentSizeFitter>();
            cf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; cf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (var c in moving) c.SetParent(scrollContent, false);
            var sr = body.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp; sr.content = scrollContent; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 40f;
            AddScrollbar(sr);
            cl.padding = new RectOffset(0, 22, 0, 4);      // clear of the 16 px scrollbar
            return true;
        }

        /// <summary>A vertical scrollbar like the builders' (ui_scrollbar art: tinted track + handle), shown only when needed.</summary>
        static void AddScrollbar(ScrollRect sr)
        {
            const float W = 16f;
            var go = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var bar = (RectTransform)go.transform;
            bar.SetParent(sr.transform, false);
            bar.anchorMin = new Vector2(1f, 0f); bar.anchorMax = new Vector2(1f, 1f); bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(W, 0f); bar.anchoredPosition = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var hrt = (RectTransform)handle.transform;
            hrt.SetParent(bar, false);
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one; hrt.offsetMin = hrt.offsetMax = Vector2.zero;
            var himg = handle.GetComponent<Image>(); himg.color = new Color(1f, 1f, 1f, 0.35f);
            var sb = go.GetComponent<Scrollbar>();
            sb.handleRect = hrt; sb.targetGraphic = himg; sb.direction = Scrollbar.Direction.BottomToTop;
            var db = GameRunner.Instance != null ? GameRunner.Instance.Database : null;
            if (db != null)
            {
                UiSkin.Attach(go, "ui_scrollbar", fit: true, tint: new Color(1f, 1f, 1f, 0.6f)).Apply(db);
                UiSkin.Attach(handle, "ui_scrollbar", fit: true).Apply(db);
            }
            sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            sr.verticalScrollbarSpacing = 0f;
        }

        /// <summary>Width capped to the space (text wraps); height capped by scrolling the body. Returns the uniform shrink still needed.</summary>
        float FitInto(Rect area)
        {
            if (width > 0f)
            {
                float w = Mathf.Floor(Mathf.Min(width, area.width));
                if (!Mathf.Approximately(rt.sizeDelta.x, w)) { rt.sizeDelta = new Vector2(w, rt.sizeDelta.y); LayoutRebuilder.ForceRebuildLayoutImmediate(rt); heightCheck = 0; }
            }
            bool areaChanged = !Mathf.Approximately(area.height, lastAreaH);
            lastAreaH = area.height;
            if (rt.rect.height > area.height + 0.5f && scrollLe == null && WrapForScroll()) { areaChanged = true; heightCheck = 0; }
            if (scrollLe != null && (areaChanged || heightCheck-- <= 0))
            {
                heightCheck = 15;          // content can grow (new link rows, a refusal line): re-measure a few times a second
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
                float contentH = LayoutUtility.GetPreferredHeight(scrollContent);
                float rest = LayoutUtility.GetPreferredHeight(rt) - LayoutUtility.GetPreferredHeight((RectTransform)scrollLe.transform);
                float want = Mathf.Floor(Mathf.Max(60f, Mathf.Min(contentH, area.height - rest)));
                if (!Mathf.Approximately(scrollLe.preferredHeight, want)) { scrollLe.preferredHeight = want; scrollLe.minHeight = want; LayoutRebuilder.ForceRebuildLayoutImmediate(rt); }
            }
            Vector2 size = rt.rect.size;
            return Mathf.Min(1f, area.width / Mathf.Max(1f, size.x), area.height / Mathf.Max(1f, size.y));
        }

        void LateUpdate()
        {
            if (Area == null || !isActiveAndEnabled) return;
            Place();
        }

        /// <summary>Root-canvas local rect of a world-space box.</summary>
        Rect WorldToCanvas(Vector3 min, Vector3 max)
        {
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 a = worldCam.WorldToScreenPoint(min), b = worldCam.WorldToScreenPoint(max);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, a, cam, out var la);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, b, cam, out var lb);
            return Rect.MinMaxRect(Mathf.Min(la.x, lb.x), Mathf.Min(la.y, lb.y), Mathf.Max(la.x, lb.x), Mathf.Max(la.y, lb.y));
        }

        Rect CanvasRectOf(RectTransform r)
        {
            r.GetWorldCorners(corners);
            Vector2 a = canvasRect.InverseTransformPoint(corners[0]), b = canvasRect.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>The building's footprint plus its art (tall roofs, chimneys), in root-canvas units.</summary>
        bool TargetInCanvas(out Rect r)
        {
            r = default;
            var b = runner != null && runner.Sim != null ? runner.State.Area(Area)?.BuildingById(BuildingId) : null;
            if (b == null) return false;
            var def = runner.Config.Building(b.type);
            int cell = runner.Space.Cell;
            int w = def != null ? def.sizeW : 1, h = def != null ? def.sizeH : 1;
            Vector3 tl = runner.Space.PxToWorld(Area, b.col * cell, b.row * cell);
            Vector3 min = new Vector3(tl.x, tl.y - h, 0f), max = new Vector3(tl.x + w, tl.y, 0f);
            var view = views != null ? views.Find(Area, BuildingId) : null;
            var art = view != null ? view.ArtRenderer : null;
            if (art != null && art.sprite != null)
            {
                var bb = art.bounds;
                min = Vector3.Min(min, bb.min); max = Vector3.Max(max, bb.max);
            }
            r = WorldToCanvas(min, max);
            return true;
        }

        /// <summary>Where panels may go: the safe area (phone) or the canvas, minus the bottom bar.</summary>
        Rect Bounds()
        {
            Rect area;
            var ml = MobileLayer.Instance;
            if (ml != null && ml.SafeArea != null && ml.SafeArea.gameObject.activeInHierarchy) area = CanvasRectOf(ml.SafeArea);
            else area = canvasRect.rect;
            var bar = BottomBarView.Instance;
            if (bar != null && bar.isActiveAndEnabled)
            {
                var br = CanvasRectOf((RectTransform)bar.transform);
                if (br.yMax > area.yMin && br.yMax < area.yMax) area.yMin = br.yMax;
            }
            area.xMin += Margin; area.xMax -= Margin; area.yMin += Margin; area.yMax -= Margin;
            return area;
        }

        /// <summary>
        /// Put <paramref name="panel"/> (centre pivot set here) next to <paramref name="next"/>: right of it, else left, top
        /// edges aligned, clamped into the root canvas. Used for a popup's companion (the recipe detail card).
        /// </summary>
        public static void PlaceBeside(RectTransform panel, RectTransform next)
        {
            if (panel == null || next == null) return;
            var cv = next.GetComponentInParent<Canvas>();
            if (cv == null) return;
            var root = (RectTransform)cv.rootCanvas.transform;
            var c4 = new Vector3[4];
            next.GetWorldCorners(c4);
            Vector2 a = root.InverseTransformPoint(c4[0]), b = root.InverseTransformPoint(c4[2]);
            var n = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            var s = panel.rect.size;
            var area = root.rect;
            float x = n.xMax + Gap + s.x * 0.5f;
            if (x + s.x * 0.5f > area.xMax - Margin) x = n.xMin - Gap - s.x * 0.5f;
            float y = n.yMax - s.y * 0.5f;
            x = Mathf.Round(Mathf.Clamp(x, area.xMin + Margin + s.x * 0.5f, area.xMax - Margin - s.x * 0.5f));
            y = Mathf.Round(Mathf.Clamp(y, area.yMin + Margin + s.y * 0.5f, area.yMax - Margin - s.y * 0.5f));
            panel.position = root.TransformPoint(new Vector3(x, y, 0f));
        }

        void Place()
        {
            if (rt == null) Init();
            if (canvasRect == null || worldCam == null || !TargetInCanvas(out var target)) return;
            var area = Bounds();
            // the building left the screen: close (a panel never points at nothing; UI QA batch B #8)
            bool onScreen = target.Overlaps(canvasRect.rect);
            if (onScreen) SeenOnScreen = true;
            else if (SeenOnScreen && (KeepOffScreen == null || !KeepOffScreen()))
            {
                Unbind();
                if (OnLeftScreen != null) OnLeftScreen(); else gameObject.SetActive(false);
                return;
            }
            rt.localScale = Vector3.one;
            // wider than the space: take its width; taller: the body scrolls; only a panel that cannot scroll shrinks
            float k = FitInto(area);
            Shrink = k;
            Vector2 size = rt.rect.size;
            var q = quest != null && quest.isActiveAndEnabled ? quest.VisibleRect : null;
            obstacle = q != null ? CanvasRectOf(q) : default;
            Vector2 s = size * k;

            Side side = Current;
            if (side == Side.None || !Fits(side, target, area, s) || !Clear(side, target, area, s)) side = Pick(target, area, s);
            Current = side;
            Vector2 c = CentreFor(side, target, area, s);
            // clamp inside the area and land on whole canvas units (crisp text)
            c.x = Mathf.Round(Mathf.Clamp(c.x, area.xMin + s.x * 0.5f, Mathf.Max(area.xMin + s.x * 0.5f, area.xMax - s.x * 0.5f)));
            c.y = Mathf.Round(Mathf.Clamp(c.y, area.yMin + s.y * 0.5f, Mathf.Max(area.yMin + s.y * 0.5f, area.yMax - s.y * 0.5f)));
            if (k < 1f) rt.localScale = new Vector3(k, k, 1f);
            rt.position = canvasRect.TransformPoint(new Vector3(c.x, c.y, 0f));
            PanelRect = new Rect(c - s * 0.5f, s);
            TargetRect = target;
        }

        static readonly Side[] Order = { Side.Right, Side.Left, Side.Above, Side.Below };
        static readonly Side[] OrderAbove = { Side.Above, Side.Right, Side.Left, Side.Below };

        Side Pick(Rect t, Rect area, Vector2 s)
        {
            foreach (var o in preferAbove ? OrderAbove : Order) if (Fits(o, t, area, s) && Clear(o, t, area, s)) return o;
            foreach (var o in preferAbove ? OrderAbove : Order) if (Fits(o, t, area, s)) return o;      // overlapping the quest panel beats docking away
            // no room beside the building: dock to the screen edge away from it (top when the building is low on screen)
            return t.center.y < area.center.y ? Side.DockTop : Side.DockBottom;
        }

        /// <summary>The panel fits on this side without leaving the area (the cross axis may slide along the building).</summary>
        static bool Fits(Side side, Rect t, Rect area, Vector2 s)
        {
            switch (side)
            {
                case Side.Right: return t.xMax + Gap + s.x <= area.xMax && s.y <= area.height && t.xMax + Gap >= area.xMin;
                case Side.Left: return t.xMin - Gap - s.x >= area.xMin && s.y <= area.height && t.xMin - Gap <= area.xMax;
                case Side.Above: return t.yMax + Gap + s.y <= area.yMax && s.x <= area.width && t.yMax + Gap >= area.yMin;
                case Side.Below: return t.yMin - Gap - s.y >= area.yMin && s.x <= area.width && t.yMin - Gap <= area.yMax;
                case Side.DockTop: case Side.DockBottom: return s.x <= area.width && s.y <= area.height;
                default: return false;
            }
        }

        /// <summary>The placed panel does not overlap the quest panel / chip (top-right HUD zone).</summary>
        bool Clear(Side side, Rect t, Rect area, Vector2 s)
        {
            if (obstacle.width <= 0f || obstacle.height <= 0f) return true;
            var c = CentreFor(side, t, area, s);
            c.x = Mathf.Clamp(c.x, area.xMin + s.x * 0.5f, Mathf.Max(area.xMin + s.x * 0.5f, area.xMax - s.x * 0.5f));
            c.y = Mathf.Clamp(c.y, area.yMin + s.y * 0.5f, Mathf.Max(area.yMin + s.y * 0.5f, area.yMax - s.y * 0.5f));
            return !new Rect(c - s * 0.5f, s).Overlaps(obstacle);
        }

        static Vector2 CentreFor(Side side, Rect t, Rect area, Vector2 s)
        {
            switch (side)
            {
                case Side.Right: return new Vector2(t.xMax + Gap + s.x * 0.5f, t.center.y);
                case Side.Left: return new Vector2(t.xMin - Gap - s.x * 0.5f, t.center.y);
                case Side.Above: return new Vector2(t.center.x, t.yMax + Gap + s.y * 0.5f);
                case Side.Below: return new Vector2(t.center.x, t.yMin - Gap - s.y * 0.5f);
                case Side.DockTop: return new Vector2(t.center.x, area.yMax - s.y * 0.5f);
                default: return new Vector2(t.center.x, area.yMin + s.y * 0.5f);
            }
        }
    }
}
