using System.Collections.Generic;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Lays out every active <see cref="WorldLabel"/> once per frame, after the views have refreshed
    /// (VP-07 / VP-10): zoom scale (constant on-screen size), zoom LOD (full / compact / far), greedy
    /// collision avoidance (a label that would overlap one already placed moves up one or two rows, or
    /// down one; if every slot is taken its content is suppressed and only its status shape stays), and
    /// a final snap of each label to the screen pixel grid. Created at runtime (no scene change).
    /// Allocation-free per frame: fixed arrays grown only when more labels register.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public class WorldLabelSystem : MonoBehaviour
    {
        /// <summary>Reference zoom the label sizes are authored for.</summary>
        public const float RefZoom = 2f;
        public const float MinStripScale = 0.5f, MaxStripScale = 1.5f;
        const float Gap = 1f / 32f;

        static readonly List<WorldLabel> labels = new List<WorldLabel>(256);
        static WorldLabelSystem instance;

        /// <summary>Current camera zoom (view width / 17.5 cells).</summary>
        public static float Zoom { get; private set; } = RefZoom;
        /// <summary>World scale of Strip labels this frame.</summary>
        public static float StripScale { get; private set; } = 1f;
        /// <summary>World units per screen pixel this frame.</summary>
        public static float UnitsPerPixel { get; private set; } = 1f / 54f;
        public static int Count => labels.Count;

        int[] order = new int[256];
        Rect[] placed = new Rect[256];
        Rect[] rects = new Rect[256];
        float[] steps = new float[256];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { labels.Clear(); instance = null; Zoom = RefZoom; StripScale = 1f; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => Ensure();

        public static WorldLabelSystem Ensure()
        {
            if (instance != null) return instance;
            instance = FindFirstObjectByType<WorldLabelSystem>();
            if (instance == null) instance = new GameObject("WorldLabels").AddComponent<WorldLabelSystem>();
            if (instance.GetComponent<IslandNameLabels>() == null) instance.gameObject.AddComponent<IslandNameLabels>();
            if (instance.GetComponent<DecorStyle>() == null) instance.gameObject.AddComponent<DecorStyle>();
            return instance;
        }

        internal static void Register(WorldLabel l)
        {
            if (l.index >= 0) return;
            l.index = labels.Count;
            labels.Add(l);
            if (instance == null && Application.isPlaying) Ensure();
        }

        internal static void Unregister(WorldLabel l)
        {
            int i = l.index;
            if (i < 0 || i >= labels.Count || labels[i] != l) { l.index = -1; return; }
            int last = labels.Count - 1;
            if (i != last) { labels[i] = labels[last]; labels[i].index = i; }
            labels.RemoveAt(last);
            l.index = -1;
        }

        /// <summary>Zoom from the main camera (independent of CameraController, so fitted screenshots work too).</summary>
        static bool ReadCamera(out Camera cam)
        {
            cam = Camera.main;
            if (cam == null || !cam.orthographic) return false;
            float viewW = cam.orthographicSize * 2f * cam.aspect;
            Zoom = viewW / 17.5f;
            StripScale = Mathf.Clamp(Zoom / RefZoom, MinStripScale, MaxStripScale);
            UnitsPerPixel = cam.orthographicSize * 2f / Mathf.Max(1, cam.pixelHeight);
            return true;
        }

        void LateUpdate()
        {
            if (!ReadCamera(out var cam)) return;
            int n = labels.Count;
            if (order.Length < n) { int k = Mathf.NextPowerOfTwo(n); order = new int[k]; placed = new Rect[k]; rects = new Rect[k]; steps = new float[k]; }
            float h = cam.orthographicSize, w = h * cam.aspect; var cp = cam.transform.position;
            var view = new Rect(cp.x - w - 2f, cp.y - h - 2f, 2f * w + 4f, 2f * h + 4f);
            float z = Zoom;
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                var l = labels[i];
                bool hideAll = z < l.hideBelowZoom;
                bool far = !hideAll && z > l.hideAboveZoom;
                bool compact = !hideAll && !far && z > l.compactAboveZoom;
                if (far != l.Far || compact != l.Compact) { l.Far = far; l.Compact = compact; l.LodVersion++; }
                // scale: strips clamp; banners and a lone far status shape keep a constant screen size
                float s = l.scaleMode == WorldLabelScale.Banner ? z / RefZoom * l.bannerScale : far ? z / RefZoom : StripScale;
                var t = l.transform;
                var parent = t.parent;
                float ps = parent != null ? Mathf.Max(0.0001f, parent.lossyScale.x) : 1f;
                float ls = s / ps;
                var sc = t.localScale;
                if (sc.x != ls) t.localScale = new Vector3(ls, ls, 1f);
                t.localPosition = l.anchor;
                l.placedOffset = 0f;
                l.Suppressed = false;
                ApplyVisibility(l, hideAll, far, false);
                if (hideAll || !l.collide || far) continue;
                var p = t.position;
                var b = l.bounds;
                var r = new Rect(p.x + b.xMin * s, p.y + b.yMin * s, b.width * s, b.height * s);
                if (!r.Overlaps(view)) continue;
                rects[m] = r; steps[m] = r.height + Gap * s; order[m] = i; m++;
            }
            // top rows first, then left to right; higher priority first (insertion sort: no allocation)
            for (int a = 1; a < m; a++)
            {
                int oi = order[a]; var ra = rects[a]; float sa = steps[a];
                int j = a - 1;
                while (j >= 0 && Before(labels[oi], ra, labels[order[j]], rects[j]))
                { order[j + 1] = order[j]; rects[j + 1] = rects[j]; steps[j + 1] = steps[j]; j--; }
                order[j + 1] = oi; rects[j + 1] = ra; steps[j + 1] = sa;
            }
            int np = 0;
            for (int a = 0; a < m; a++)
            {
                var l = labels[order[a]];
                var r = rects[a];
                float st = steps[a];
                float chosen = float.NaN;
                for (int k = 0; k < 4; k++)
                {
                    float dy = k == 0 ? 0f : k == 1 ? st : k == 2 ? 2f * st : -st;
                    var rr = new Rect(r.x, r.y + dy, r.width, r.height);
                    bool hit = false;
                    for (int q = 0; q < np && !hit; q++) if (placed[q].Overlaps(rr)) hit = true;
                    if (!hit) { chosen = dy; placed[np++] = rr; break; }
                }
                if (float.IsNaN(chosen)) { l.Suppressed = true; ApplyVisibility(l, false, false, true); continue; }
                if (chosen != 0f)
                {
                    l.placedOffset = chosen;
                    l.transform.position += new Vector3(0f, chosen, 0f);
                }
            }
            // pixel snap (after offsets): whole screen pixels so text and plates do not shimmer
            float upp = UnitsPerPixel;
            if (upp > 0f)
                for (int i = 0; i < n; i++)
                {
                    var l = labels[i];
                    var t = l.transform;
                    var p = t.position;
                    if (l.clampToView)
                    {
                        float s = t.lossyScale.x; var b = l.bounds;
                        float minX = cp.x - w + 0.1f - b.xMin * s, maxX = cp.x + w - 0.1f - b.xMax * s;
                        float minY = cp.y - h + 0.1f - b.yMin * s, maxY = cp.y + h - 0.1f - b.yMax * s;
                        if (minX <= maxX) p.x = Mathf.Clamp(p.x, minX, maxX);
                        if (minY <= maxY) p.y = Mathf.Clamp(p.y, minY, maxY);
                    }
                    t.position = new Vector3(Mathf.Round(p.x / upp) * upp, Mathf.Round(p.y / upp) * upp, p.z);
                }
        }

        static bool Before(WorldLabel a, Rect ra, WorldLabel b, Rect rb)
        {
            if (a.priority != b.priority) return a.priority > b.priority;
            if (!Mathf.Approximately(ra.yMax, rb.yMax)) return ra.yMax > rb.yMax;
            return ra.xMin < rb.xMin;
        }

        static void ApplyVisibility(WorldLabel l, bool hideAll, bool far, bool suppressed)
        {
            bool showContent = !hideAll && !far && !suppressed;
            if (l.content != null && l.content.gameObject.activeSelf != showContent) l.content.gameObject.SetActive(showContent);
            if (l.keep != null)
            {
                bool showKeep = !hideAll;
                if (l.keep.gameObject.activeSelf != showKeep) l.keep.gameObject.SetActive(showKeep);
                var kp = far || suppressed ? l.keepFar : l.keepHome;
                if (l.keep.localPosition != kp) l.keep.localPosition = kp;
            }
        }
    }
}
