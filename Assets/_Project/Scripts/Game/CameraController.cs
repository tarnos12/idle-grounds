using UnityEngine;
using UnityEngine.InputSystem;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Orthographic pan / sprint-toggle / stepped zoom camera (ui-input-render section 1).
    /// View width = 17.5 cells * zoom; zoom 1..zoomMax in 0.25 steps, smoothed; centre-anchored. Floating
    /// Islands (ADR 0003): clamped to the bounding box of the unlocked Islands plus any extra rects (the
    /// unlock steles of frontier Islands), expanded by <see cref="skyMargin"/> cells — so the view can pan
    /// over the open sky between unlocked Islands.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        const float BaseViewWidthCells = 17.5f;          // 560 px / 32
        const float PanCellsPerSecond = 12f * 60f / 32f; // 12 px/frame at 60 fps
        const float SprintMultiplier = 2f;
        const float ZoomStep = 0.25f, ZoomMin = 1f;

        [SerializeField] float startZoom = 2f;
        [Tooltip("Furthest zoom-out (view width = 17.5 cells x this). The original stopped at 3; Islands float far apart.")]
        [SerializeField] float zoomMax = 6f;
        [Tooltip("Cells of open sky the view may show beyond the unlocked Islands / steles.")]
        [SerializeField] float skyMargin = 12f;
        [Tooltip("Initial camera centre: Island key + local row/col.")]
        [UnityEngine.Serialization.FormerlySerializedAs("startRegionKey")]
        [SerializeField] string startIslandKey = "center";
        [SerializeField] int startRow = 12;
        [SerializeField] int startCol = 46;
        [Tooltip("Original recentre: view top = start Island top, centred horizontally (startRow/startCol ignored).")]
        [SerializeField] bool startTopAligned = true;

        Camera cam;
        IdleGroundsControls controls;
        float zoom, zoomTarget;
        bool sprint;
        Rect unlockedBounds;
        readonly System.Collections.Generic.List<Rect> extraBounds = new System.Collections.Generic.List<Rect>();
        float ZoomMax => Mathf.Max(ZoomMin, zoomMax);
        bool hasBounds;
        Vector2 centre;

        /// <summary>True while a modal that owns WASD / the wheel is open (Altar tree pans its own board).</summary>
        public static bool PanSuspended;

        public bool SprintActive => sprint;
        public float Zoom => zoom;
        public float ZoomTarget => zoomTarget;

        /// <summary>Raw bounding box (world units) the view may roam (before the sky margin).</summary>
        public void SetUnlockedBounds(Rect bounds)
        {
            unlockedBounds = bounds;
            hasBounds = true;
            if (cam != null) Clamp();
        }

        /// <summary>Extra world rects the view must be able to reach (frontier unlock steles). Re-clamps.</summary>
        public void SetExtraBounds(System.Collections.Generic.IEnumerable<Rect> rects)
        {
            extraBounds.Clear();
            if (rects != null) extraBounds.AddRange(rects);
            RecomputeBounds();
        }

        /// <summary>Union of the unlocked Islands' play rects and the extra rects.</summary>
        public void RecomputeBounds()
        {
            bool any = false;
            Rect box = default;
            void Add(Rect wr)
            {
                if (!any) { box = wr; any = true; }
                else box = Rect.MinMaxRect(Mathf.Min(box.xMin, wr.xMin), Mathf.Min(box.yMin, wr.yMin),
                                           Mathf.Max(box.xMax, wr.xMax), Mathf.Max(box.yMax, wr.yMax));
            }
            foreach (var r in FindObjectsByType<Island>(FindObjectsSortMode.None))
                if (r.unlocked) Add(r.VisualRect);
            foreach (var r in extraBounds) Add(r);
            if (any) SetUnlockedBounds(box); else hasBounds = false;
        }

        void Awake()
        {
            PanSuspended = false;      // static: survives Enter-Play-Mode without a domain reload
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            controls = new IdleGroundsControls();
            zoom = zoomTarget = Mathf.Clamp(startZoom, ZoomMin, ZoomMax);
        }

        void OnEnable()
        {
            controls ??= new IdleGroundsControls();
            controls.Gameplay.Enable();
            controls.Gameplay.SprintToggle.performed += OnSprint;
        }

        void OnDisable()
        {
            controls.Gameplay.SprintToggle.performed -= OnSprint;
            controls.Gameplay.Disable();
        }

        void OnDestroy() => controls?.Dispose();

        void OnSprint(InputAction.CallbackContext ctx)
        {
            sprint = !sprint;
        }

        void Start()
        {
            RecomputeBounds();
            Vector2 start = transform.position;
            foreach (var r in FindObjectsByType<Island>(FindObjectsSortMode.None))
                if (r.islandKey == startIslandKey) { start = startTopAligned ? StartCentre(r) : (Vector2)r.CellToWorld(startRow, startCol); break; }
            centre = start;
            ApplyZoom();
            Clamp();
            Apply();
        }

        /// <summary>Jump the camera centre to a world point (clamped to the unlocked bounds).</summary>
        public void CenterOn(Vector2 world)
        {
            centre = world;
            if (cam == null) return;
            ApplyZoom();
            Clamp();
            Apply();
        }

        /// <summary>Alias of <see cref="CenterOn"/>.</summary>
        public void Teleport(Vector2 world) => CenterOn(world);

        /// <summary>Camera centre in world units.</summary>
        public Vector2 Centre => centre;

        /// <summary>Back to the configured start cell (new run after an ascension).</summary>
        public void ResetToStart()
        {
            RecomputeBounds();
            foreach (var r in FindObjectsByType<Island>(FindObjectsSortMode.None))
                if (r.islandKey == startIslandKey) { CenterOn(startTopAligned ? StartCentre(r) : (Vector2)r.CellToWorld(startRow, startCol)); return; }
        }

        void Update()
        {
            if (cam == null) return;
            float dt = Time.unscaledDeltaTime;

            // Pan (diagonals intentionally not normalised, as in the original).
            Vector2 pan = controls.Gameplay.Pan.ReadValue<Vector2>();
            if (pan != Vector2.zero && !PanSuspended)
                centre += pan * (PanCellsPerSecond * (sprint ? SprintMultiplier : 1f) * dt);

            // Zoom: wheel up = zoom in, wheel down = zoom out.
            float wheel = controls.Gameplay.Zoom.ReadValue<float>();
            if (Mathf.Abs(wheel) > 0.01f && !PanSuspended && !WheelOverUi())
                zoomTarget = Mathf.Clamp(zoomTarget + (wheel < 0 ? ZoomStep : -ZoomStep), ZoomMin, ZoomMax);

            if (!Mathf.Approximately(zoom, zoomTarget))
            {
                float k = 1f - Mathf.Pow(1f - 0.2f, dt * 60f); // 0.2/frame at 60 fps, frame-rate independent
                zoom += (zoomTarget - zoom) * k;
                if (Mathf.Abs(zoomTarget - zoom) < 0.005f) zoom = zoomTarget;
            }
            ApplyZoom();
            Clamp();
            Apply();
        }

        /// <summary>
        /// The wheel zooms only over the world viewport (ui.js binds onWheel to #world-viewport): not over
        /// UI (build strip, perk shop, help, quest panel…) and not while a modal is up.
        /// </summary>
        static bool WheelOverUi()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && es.IsPointerOverGameObject()) return true;
            var meta = MetaUiController.Instance;
            return meta != null && meta.AnyOpen;
        }

        /// <summary>recenterCamera (ui.js:283): the view's TOP edge on the start Island's top, centred horizontally.</summary>
        Vector2 StartCentre(Island r)
        {
            ApplyZoom();
            var o = r.Origin;
            return new Vector2(o.x + Island.Cells * 0.5f, o.y - ViewHeight * 0.5f);
        }

        /// <summary>Set the zoom at once (no smoothing), clamped to 1..zoomMax (automation / screenshots).</summary>
        public void SetZoomImmediate(float z)
        {
            zoom = zoomTarget = Mathf.Clamp(z, ZoomMin, ZoomMax);
            if (cam == null) return;
            ApplyZoom(); Clamp(); Apply();
        }

        float ViewWidth => BaseViewWidthCells * zoom;
        float ViewHeight => ViewWidth / cam.aspect;

        void ApplyZoom() => cam.orthographicSize = ViewHeight * 0.5f;

        void Apply() => transform.position = new Vector3(centre.x, centre.y, transform.position.z);

        void Clamp()
        {
            if (cam == null || !hasBounds) return;
            float m = skyMargin;
            float x0 = unlockedBounds.xMin - m, x1 = unlockedBounds.xMax + m;
            float y0 = unlockedBounds.yMin - m, y1 = unlockedBounds.yMax + m;
            float w = ViewWidth, h = ViewHeight;
            // a view wider/taller than the bounds is centred on them; otherwise it stays inside
            centre.x = w >= x1 - x0 ? (x0 + x1) * 0.5f : Mathf.Clamp(centre.x, x0 + w * 0.5f, x1 - w * 0.5f);
            centre.y = h >= y1 - y0 ? (y0 + y1) * 0.5f : Mathf.Clamp(centre.y, y0 + h * 0.5f, y1 - h * 0.5f);
        }
    }
}
