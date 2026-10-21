using UnityEngine;
using UnityEngine.InputSystem;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Orthographic pan / sprint-toggle / stepped zoom camera (ui-input-render section 1).
    /// <para><b>Crisp pixel zoom (VP-28, owner pick vp_zoom = crisp_map, 2026-10-21).</b> The zoom rests only on stops where
    /// one art texel is a whole number of screen pixels: k = 4 / 3 / 2 / 1 px per texel (15 / 20 / 30 / 60 cells across on a
    /// 1920 px wide view; other screens get the whole k nearest to those widths), plus one far "map" stop at half size
    /// (k = 1/2: 120 cells at 1920 px; a slight shimmer is accepted there). The wheel, the + / - keys and a pinch (on
    /// release) all land on a stop; the tween between stops is smoothed and, at rest, the view snaps to the screen-pixel
    /// grid so texels never swim. <see cref="Zoom"/> keeps its meaning (view width / 17.5 cells), so the world-label LOD
    /// thresholds still work.</para>
    /// Floating Islands (ADR 0003): clamped to the bounding box of the unlocked Islands plus any extra rects (the
    /// unlock steles of frontier Islands), expanded by <see cref="skyMargin"/> cells — so the view can pan
    /// over the open sky between unlocked Islands.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        const float BaseViewWidthCells = 17.5f;          // 560 px / 32
        const float PanCellsPerSecond = 12f * 60f / 32f; // 12 px/frame at 60 fps
        const float SprintMultiplier = 2f;
        /// <summary>Art texels per world unit (PPU 32; 1 cell = 1 world unit).</summary>
        public const float TexelsPerUnit = 32f;
        /// <summary>The crisp stops are the whole px-per-texel values nearest to these view widths (cells across).</summary>
        public static readonly float[] StopCells = { 15f, 20f, 30f, 60f };
        /// <summary>Pinch limits as a share of the closest / farthest stop (a pinch may overshoot a little, then settles).</summary>
        const float PinchOvershoot = 0.15f;

        [Tooltip("Start zoom (view width = 17.5 cells x this); snapped to the nearest crisp stop.")]
        [SerializeField] float startZoom = 2f;
        [Tooltip("Unused since the crisp stops (VP-28): the far 'map' stop is the limit. Kept so old scenes load.")]
#pragma warning disable 0414
        [SerializeField] float zoomMax = 6f;
#pragma warning restore 0414
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
        int stopIndex = -1;                  // index into stops (0 = closest); -1 = not resolved yet
        float startZoomWanted;
        bool sprint;
        Rect unlockedBounds;
        readonly System.Collections.Generic.List<Rect> extraBounds = new System.Collections.Generic.List<Rect>();
        bool hasBounds;
        Vector2 centre;
        float pinchUntil;                    // a pinch drives the zoom directly until shortly after the last pinch step

        // crisp stops for the current view size: px per texel (descending: closest first) and the matching zoom values
        readonly float[] stopK = new float[StopCells.Length + 1];
        readonly float[] stopZoom = new float[StopCells.Length + 1];
        int stopCount, stopsForW = -1, stopsForH = -1;

        /// <summary>True while a modal that owns WASD / the wheel is open (Altar tree pans its own board).</summary>
        public static bool PanSuspended;

        public bool SprintActive => sprint;
        /// <summary>View width / 17.5 cells (1 = the original close-up).</summary>
        public float Zoom => zoom;
        public float ZoomTarget => zoomTarget;
        /// <summary>Index of the stop the zoom rests on / tweens to (0 = closest, <see cref="StopCount"/> - 1 = the map stop).</summary>
        public int StopIndex { get { EnsureStops(); return stopIndex; } }
        public int StopCount { get { EnsureStops(); return stopCount; } }
        /// <summary>Screen px per art texel at stop <paramref name="i"/> (0.5 at the map stop on a 1920 px wide view).</summary>
        public float StopPxPerTexel(int i) { EnsureStops(); return stopK[Mathf.Clamp(i, 0, stopCount - 1)]; }
        /// <summary><see cref="Zoom"/> value of stop <paramref name="i"/>.</summary>
        public float StopZoom(int i) { EnsureStops(); return stopZoom[Mathf.Clamp(i, 0, stopCount - 1)]; }
        /// <summary>True when the zoom rests on a stop (no tween, no pinch).</summary>
        public bool AtRest => Mathf.Approximately(zoom, zoomTarget) && Time.unscaledTime >= pinchUntil;

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
            startZoomWanted = startZoom;
            EnsureStops(true);
            SnapToStop(NearestStop(startZoomWanted));
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

        // ================================================================ crisp stops

        /// <summary>Rebuild the stop table when the view size changed (resize, orientation, screenshot render target).</summary>
        void EnsureStops(bool force = false)
        {
            if (cam == null) cam = GetComponent<Camera>();
            int w = Mathf.Max(1, cam.pixelWidth), h = Mathf.Max(1, cam.pixelHeight);
            if (!force && w == stopsForW && h == stopsForH && stopCount > 0) return;
            float keepZoom = stopCount > 0 && stopIndex >= 0 ? stopZoom[Mathf.Clamp(stopIndex, 0, stopCount - 1)] : -1f;
            stopsForW = w; stopsForH = h;
            stopCount = 0;
            float lastK = float.MaxValue;
            foreach (float cells in StopCells)
            {
                float k = Mathf.Max(1f, Mathf.Round(w / (TexelsPerUnit * cells)));
                if (k >= lastK) continue;            // two widths rounding to the same k: keep one stop
                stopK[stopCount++] = k;
                lastK = k;
            }
            stopK[stopCount++] = lastK * 0.5f;       // the far "map" stop: half size (0.5 px per texel at 1920 px)
            for (int i = 0; i < stopCount; i++) stopZoom[i] = ZoomForK(stopK[i]);
            if (keepZoom > 0f)
            {
                stopIndex = NearestStop(keepZoom);
                zoom = zoomTarget = stopZoom[stopIndex];
            }
        }

        /// <summary><see cref="Zoom"/> (view width / 17.5) at k screen px per art texel for the current view width.</summary>
        float ZoomForK(float k) => Mathf.Max(1, cam.pixelWidth) / (TexelsPerUnit * k) / BaseViewWidthCells;

        /// <summary>Index of the stop whose zoom is nearest to <paramref name="z"/> (compared on a log scale).</summary>
        public int NearestStop(float z)
        {
            EnsureStops();
            int best = 0; float bestD = float.MaxValue;
            float lz = Mathf.Log(Mathf.Max(0.01f, z));
            for (int i = 0; i < stopCount; i++)
            {
                float d = Mathf.Abs(Mathf.Log(stopZoom[i]) - lz);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        void SnapToStop(int i)
        {
            stopIndex = Mathf.Clamp(i, 0, stopCount - 1);
            zoom = zoomTarget = stopZoom[stopIndex];
        }

        /// <summary>One stop in (-1) or out (+1); the tween starts from the current zoom.</summary>
        public void StepZoom(int dir)
        {
            EnsureStops();
            if (stopIndex < 0) stopIndex = NearestStop(zoom);
            stopIndex = Mathf.Clamp(stopIndex + (dir > 0 ? 1 : -1), 0, stopCount - 1);
            zoomTarget = stopZoom[stopIndex];
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

        // ---- touch (TouchInputController): one-finger drag pan, two-finger pinch zoom ----

        /// <summary>Drag pan: the world follows the finger by <paramref name="screenDelta"/> pixels.</summary>
        public void TouchPan(Vector2 screenDelta)
        {
            if (cam == null || PanSuspended) return;
            float worldPerPx = cam.orthographicSize * 2f / Mathf.Max(1, cam.pixelHeight);
            centre -= screenDelta * worldPerPx;
            Clamp();
            Apply();
        }

        /// <summary>World point under a screen point (orthographic, ignores the camera's z).</summary>
        public Vector2 ScreenToWorld(Vector2 screen)
        {
            if (cam == null) return screen;
            return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
        }

        /// <summary>
        /// Pinch step: continuous zoom while the fingers move (a little past the closest / farthest stop, no smoothing)
        /// that keeps <paramref name="anchorWorld"/> under <paramref name="midScreen"/>, so the pinch also pans. When the
        /// pinch ends the zoom settles on the nearest crisp stop.
        /// </summary>
        public void TouchZoom(float newZoom, Vector2 anchorWorld, Vector2 midScreen)
        {
            if (cam == null || PanSuspended) return;
            EnsureStops();
            float lo = stopZoom[0] * (1f - PinchOvershoot), hi = stopZoom[stopCount - 1] * (1f + PinchOvershoot);
            zoom = Mathf.Clamp(newZoom, lo, hi);
            stopIndex = NearestStop(zoom);
            zoomTarget = stopZoom[stopIndex];          // where it settles once the fingers stop
            pinchUntil = Time.unscaledTime + 0.12f;
            ApplyZoom();
            transform.position = new Vector3(centre.x, centre.y, transform.position.z);
            centre += anchorWorld - ScreenToWorld(midScreen);
            Clamp();
            Apply();
        }

        /// <summary>Alias of <see cref="CenterOn"/>.</summary>
        public void Teleport(Vector2 world) => CenterOn(world);

        /// <summary>Camera centre in world units.</summary>
        public Vector2 Centre => centre;

        /// <summary>Back to the configured start cell (e.g. after an in-place load).</summary>
        public void ResetToStart()
        {
            RecomputeBounds();
            foreach (var r in FindObjectsByType<Island>(FindObjectsSortMode.None))
                if (r.islandKey == startIslandKey) { CenterOn(startTopAligned ? StartCentre(r) : (Vector2)r.CellToWorld(startRow, startCol)); return; }
        }

        void Update()
        {
            if (cam == null) return;
            EnsureStops();
            float dt = Time.unscaledDeltaTime;

            // Pan (diagonals intentionally not normalised, as in the original).
            Vector2 pan = controls.Gameplay.Pan.ReadValue<Vector2>();
            if (pan != Vector2.zero && !PanSuspended)
                centre += pan * (PanCellsPerSecond * (sprint ? SprintMultiplier : 1f) * dt);

            // Zoom: wheel up / + = one stop in, wheel down / - = one stop out.
            if (!PanSuspended)
            {
                float wheel = controls.Gameplay.Zoom.ReadValue<float>();
                if (Mathf.Abs(wheel) > 0.01f && !WheelOverUi()) StepZoom(wheel < 0 ? +1 : -1);
                var kb = Keyboard.current;
                if (kb != null && !KeysBlocked())
                {
                    if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) StepZoom(-1);
                    if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) StepZoom(+1);
                }
            }

            if (Time.unscaledTime >= pinchUntil && !Mathf.Approximately(zoom, zoomTarget))
            {
                float k = 1f - Mathf.Pow(1f - 0.2f, dt * 60f); // 0.2/frame at 60 fps, frame-rate independent
                zoom += (zoomTarget - zoom) * k;
                if (Mathf.Abs(zoomTarget - zoom) < 0.004f * zoomTarget) zoom = zoomTarget;
            }
            ApplyZoom();
            Clamp();
            Apply();
        }

        /// <summary>
        /// The wheel zooms only over the world viewport (ui.js binds onWheel to #world-viewport): not over
        /// UI (build strip, help, quest panel…) and not while a modal is up.
        /// </summary>
        static bool WheelOverUi()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && es.IsPointerOverGameObject()) return true;
            return KeysBlocked();
        }

        /// <summary>A modal (Help, Stats, Menu, confirm...) owns the keys / wheel.</summary>
        static bool KeysBlocked()
        {
            var meta = MetaUiController.Instance;
            return (meta != null && meta.AnyOpen) || ModalScrim.Any;
        }

        /// <summary>recenterCamera (ui.js:283): the view's TOP edge on the start Island's top, centred horizontally.</summary>
        Vector2 StartCentre(Island r)
        {
            ApplyZoom();
            var o = r.Origin;
            return new Vector2(o.x + Island.Cells * 0.5f, o.y - ViewHeight * 0.5f);
        }

        /// <summary>Set the zoom at once (no smoothing), snapped to the nearest crisp stop (automation / screenshots).</summary>
        public void SetZoomImmediate(float z)
        {
            if (cam == null) cam = GetComponent<Camera>();
            EnsureStops(true);
            SnapToStop(NearestStop(z));
            ApplyZoom(); Clamp(); Apply();
        }

        /// <summary>Set the zoom at once to stop <paramref name="i"/> (0 = closest).</summary>
        public void SetStopImmediate(int i)
        {
            EnsureStops(true);
            SnapToStop(i);
            ApplyZoom(); Clamp(); Apply();
        }

        float ViewWidth => BaseViewWidthCells * zoom;
        float ViewHeight => ViewWidth / cam.aspect;

        void ApplyZoom() => cam.orthographicSize = ViewHeight * 0.5f;

        /// <summary>Rendered position: at rest the view's bottom-left corner sits on the screen-pixel grid (1 / (32 k) world units).</summary>
        Vector3 Snapped(Vector2 c)
        {
            if (!AtRest || stopIndex < 0) return new Vector3(c.x, c.y, transform.position.z);
            float px = 1f / (TexelsPerUnit * stopK[stopIndex]);
            float halfW = cam.pixelWidth * px * 0.5f, halfH = cam.pixelHeight * px * 0.5f;
            float x = Mathf.Round((c.x - halfW) / px) * px + halfW;
            float y = Mathf.Round((c.y - halfH) / px) * px + halfH;
            return new Vector3(x, y, transform.position.z);
        }

        void Apply() => transform.position = Snapped(centre);

        bool shaking;
        /// <summary>Juice camera shake: a transient offset on top of the clean centre (Update re-applies the centre every frame).</summary>
        void LateUpdate()
        {
            var o = Juice.ShakeOffset();
            if (o == Vector2.zero && !shaking) return;
            shaking = o != Vector2.zero;
            transform.position = Snapped(centre + o);
        }

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
