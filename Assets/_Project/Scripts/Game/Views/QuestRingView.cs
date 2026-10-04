using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Quest target ring (ui.js questTargetRect / drawQuestRing U:2263-2310): while the active quest is
    /// unfinished and its <c>QuestDef.target</c> resolves (<see cref="Simulation.QuestTarget"/>: fixture node,
    /// enemy zone, dragon / altar / building, region unlocked), a soft gold ring pulses on it:
    /// k = 0.5 + 0.5 sin(now/320), stroke rgba(251,191,36, .35+.4k), 3 px. Objects get a circle of radius
    /// max(w,h)·0.62 + 6 + 6k px around their centre; a whole zone gets a dashed frame inset 6 + 4k px.
    /// The circle is a ring of pooled square-sprite segments (same 2D sprite pipeline as the rest of the
    /// world); the zone frame is an <see cref="EdgeFrame"/>. The target is re-resolved at 4 Hz (the query
    /// allocates); nothing is drawn when it is off camera.
    /// </summary>
    public class QuestRingView : MonoBehaviour
    {
        public const int Segments = 56;
        public const float LinePx = 3f;
        static readonly Color Gold = new Color(251 / 255f, 191 / 255f, 36 / 255f, 1f);

        [SerializeField] GameRunner runner;
        [Tooltip("1-unit white square sprite for the circle segments.")]
        [SerializeField] Sprite segmentSprite;
        [SerializeField] EdgeFrame zoneFrame;
        [SerializeField] string sortingLayer = "Overlay";
        [SerializeField] int sortingOrder = 5;

        SpriteRenderer[] segs;
        Transform circleRoot;
        float nextResolveAt;
        bool has, isZone;
        Vector3 topLeft;          // world, +y up
        float wU, hU;             // size in units

        /// <summary>True while a ring / zone frame is drawn (for automation + screenshots).</summary>
        public bool Showing { get; private set; }
        public bool ShowingZone => Showing && isZone;
        public string TargetKind { get; private set; }

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            circleRoot = new GameObject("Circle").transform;
            circleRoot.SetParent(transform, false);
            segs = new SpriteRenderer[Segments];
            for (int i = 0; i < Segments; i++)
            {
                var sr = new GameObject("Seg" + i).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(circleRoot, false);
                sr.sprite = segmentSprite;
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder;
                segs[i] = sr;
            }
            circleRoot.gameObject.SetActive(false);
            if (zoneFrame != null) zoneFrame.gameObject.SetActive(false);
        }

        void Resolve()
        {
            has = false; TargetKind = null;
            var t = runner.Sim.QuestTarget();
            if (t == null) return;
            var space = runner.Space;
            int cell = space.Cell;
            double x, y, w, h;
            isZone = false;
            if (t.node != null) { x = t.node.col * cell; y = t.node.row * cell; w = h = t.node.size * cell; }
            else if (t.zone != null)
            {
                x = t.zone.c0 * cell; y = t.zone.r0 * cell;
                w = (t.zone.c1 - t.zone.c0 + 1) * cell; h = (t.zone.r1 - t.zone.r0 + 1) * cell;
                isZone = true;
            }
            else if (t.building != null)
            {
                var (bw, bh) = runner.Config.BuildingSize(t.building.type);
                x = t.building.col * cell; y = t.building.row * cell; w = bw * cell; h = bh * cell;
            }
            else return;
            topLeft = space.PxToWorld(t.area, x, y);
            wU = (float)(w / cell); hU = (float)(h / cell);
            TargetKind = t.kind;
            has = true;
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null) return;
            float now = Time.unscaledTime;
            if (now >= nextResolveAt) { nextResolveAt = now + 0.25f; Resolve(); }
            bool on = has && ViewCull.Visible(topLeft, wU, hU);
            Showing = on;
            if (circleRoot.gameObject.activeSelf != (on && !isZone)) circleRoot.gameObject.SetActive(on && !isZone);
            if (zoneFrame != null && zoneFrame.gameObject.activeSelf != (on && isZone)) zoneFrame.gameObject.SetActive(on && isZone);
            if (!on) return;

            float k = 0.5f + 0.5f * Mathf.Sin((float)(Time.realtimeSinceStartupAsDouble * 1000.0 % 2_000_000d / 320.0));
            var c = new Color(Gold.r, Gold.g, Gold.b, 0.35f + 0.4f * k);
            float lw = ViewKit.U(LinePx);
            if (isZone)
            {
                if (zoneFrame == null) return;
                float inset = ViewKit.U(6f + 4f * k);
                zoneFrame.transform.position = new Vector3(topLeft.x + inset, topLeft.y - inset, 0f);
                zoneFrame.Set(wU - 2f * inset, hU - 2f * inset, lw, c, true);
                return;
            }
            float rad = Mathf.Max(wU, hU) * 0.62f + ViewKit.U(6f + 6f * k);
            circleRoot.position = new Vector3(topLeft.x + wU * 0.5f, topLeft.y - hU * 0.5f, 0f);
            float seg = 2f * Mathf.PI * rad / Segments * 1.04f;      // slight overlap: no gaps between segments
            for (int i = 0; i < Segments; i++)
            {
                float a = (i + 0.5f) / Segments * Mathf.PI * 2f;
                var sr = segs[i];
                sr.transform.localPosition = new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad, 0f);
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
                sr.transform.localScale = new Vector3(seg, lw, 1f);
                if (sr.color != c) sr.color = c;
            }
        }
    }
}
