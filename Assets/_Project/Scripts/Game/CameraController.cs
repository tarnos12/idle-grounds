using UnityEngine;
using UnityEngine.InputSystem;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Orthographic pan / sprint-toggle / stepped zoom camera (ui-input-render section 1).
    /// View width = 17.5 cells * zoom; zoom 1..3 in 0.25 steps, smoothed; centre-anchored; clamped to the
    /// bounding box of unlocked regions expanded by one gap (5 cells), intersected with the world.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        const float BaseViewWidthCells = 17.5f;          // 560 px / 32
        const float PanCellsPerSecond = 12f * 60f / 32f; // 12 px/frame at 60 fps
        const float SprintMultiplier = 2f;
        const float ZoomStep = 0.25f, ZoomMin = 1f, ZoomMax = 3f;

        [SerializeField] float startZoom = 2f;
        [Tooltip("Initial camera centre: region key + local row/col.")]
        [SerializeField] string startRegionKey = "center";
        [SerializeField] int startRow = 12;
        [SerializeField] int startCol = 46;
        [Tooltip("Original recentre: view top = start region top, centred horizontally (startRow/startCol ignored).")]
        [SerializeField] bool startTopAligned = true;

        Camera cam;
        IdleGroundsControls controls;
        float zoom, zoomTarget;
        bool sprint;
        Rect unlockedBounds;
        bool hasBounds;
        Vector2 centre;

        /// <summary>True while a modal that owns WASD / the wheel is open (Altar tree pans its own board).</summary>
        public static bool PanSuspended;

        public bool SprintActive => sprint;
        public float Zoom => zoom;
        public float ZoomTarget => zoomTarget;

        /// <summary>Raw bounding box (world units) of unlocked regions' play rects.</summary>
        public void SetUnlockedBounds(Rect bounds)
        {
            unlockedBounds = bounds;
            hasBounds = true;
            if (cam != null) Clamp();
        }

        public void RecomputeBoundsFromRegions()
        {
            bool any = false;
            Rect box = default;
            foreach (var r in FindObjectsByType<Region>(FindObjectsSortMode.None))
            {
                if (!r.unlocked) continue;
                var wr = r.WorldRect;
                if (!any) { box = wr; any = true; }
                else box = Rect.MinMaxRect(Mathf.Min(box.xMin, wr.xMin), Mathf.Min(box.yMin, wr.yMin),
                                           Mathf.Max(box.xMax, wr.xMax), Mathf.Max(box.yMax, wr.yMax));
            }
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
            RecomputeBoundsFromRegions();
            Vector2 start = transform.position;
            foreach (var r in FindObjectsByType<Region>(FindObjectsSortMode.None))
                if (r.regionKey == startRegionKey) { start = startTopAligned ? StartCentre(r) : (Vector2)r.CellToWorld(startRow, startCol); break; }
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
            RecomputeBoundsFromRegions();
            foreach (var r in FindObjectsByType<Region>(FindObjectsSortMode.None))
                if (r.regionKey == startRegionKey) { CenterOn(startTopAligned ? StartCentre(r) : (Vector2)r.CellToWorld(startRow, startCol)); return; }
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

        /// <summary>recenterCamera (ui.js:283): the view's TOP edge on the start region's top, centred horizontally.</summary>
        Vector2 StartCentre(Region r)
        {
            ApplyZoom();
            var o = r.Origin;
            return new Vector2(o.x + Region.Cells * 0.5f, o.y - ViewHeight * 0.5f);
        }

        float ViewWidth => BaseViewWidthCells * zoom;
        float ViewHeight => ViewWidth / cam.aspect;

        void ApplyZoom() => cam.orthographicSize = ViewHeight * 0.5f;

        void Apply() => transform.position = new Vector3(centre.x, centre.y, transform.position.z);

        void Clamp()
        {
            if (cam == null || !hasBounds) return;
            const float gap = Region.Gap;
            float worldMin = -Region.Margin;
            float worldMax = 3 * Region.Cells + 2 * Region.Gap + Region.Margin; // 299
            float x0 = Mathf.Max(unlockedBounds.xMin - gap, worldMin);
            float x1 = Mathf.Min(unlockedBounds.xMax + gap, worldMax);
            float yTop = Mathf.Min(unlockedBounds.yMax + gap, Region.Margin);
            float yBot = Mathf.Max(unlockedBounds.yMin - gap, -worldMax);

            float w = ViewWidth, h = ViewHeight;
            float left = centre.x - w * 0.5f;
            float top = centre.y + h * 0.5f;
            left = Mathf.Clamp(left, x0, Mathf.Max(x0, x1 - w));
            top = Mathf.Clamp(top, Mathf.Min(yBot + h, yTop), yTop);
            centre = new Vector2(left + w * 0.5f, top - h * 0.5f);
        }
    }
}
