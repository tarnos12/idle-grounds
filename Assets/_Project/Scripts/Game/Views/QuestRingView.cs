using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Quest target ring (ui.js questTargetRect / drawQuestRing U:2263-2310): while the active quest is
    /// unfinished and its <c>QuestDef.target</c> resolves (<see cref="Simulation.QuestTarget"/>: fixture node,
    /// enemy zone, dragon / altar / building, a Spirit Bridge on any Island, a locked Island's unlock stele), a soft gold ring pulses on it:
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
        SpriteRenderer ring;
        /// <summary>Ring line width in texels (1 texel = 1 world px).</summary>
        public const int RingTexels = 2;
        Transform circleRoot;
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
            // VP-13: one pixel ring (2 texels, palette gold) replaces the 56 anti-aliased segments
            foreach (var sg in segs) sg.gameObject.SetActive(false);
            ring = new GameObject("PixelRing").AddComponent<SpriteRenderer>();
            ring.transform.SetParent(circleRoot, false);
            ring.sortingLayerName = sortingLayer; ring.sortingOrder = sortingOrder;
            circleRoot.gameObject.SetActive(false);
            if (zoneFrame != null) zoneFrame.gameObject.SetActive(false);
        }

        readonly QuestTargetInfo targetBuf = new QuestTargetInfo();
        UnlockSignSync signs;

        void Resolve()
        {
            has = false; TargetKind = null;
            var t = runner.Sim.QuestTarget(targetBuf) ? targetBuf : null;
            if (t == null) return;
            if (t.kind == "stele")
            {
                // a locked Island's unlock stele (a scene object, ADR 0003): ring the stele + its cost plate
                if (signs == null) signs = FindFirstObjectByType<UnlockSignSync>();
                var sv = signs != null ? signs.Find(t.area) : null;
                if (sv == null) return;
                var r = sv.WorldRect;
                topLeft = new Vector3(r.xMin, r.yMax, 0f);
                wU = r.width; hU = r.height;
                isZone = false;
                TargetKind = t.kind;
                has = true;
                return;
            }
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
            Resolve();
            bool on = has && ViewCull.Visible(topLeft, wU, hU);
            Showing = on;
            if (circleRoot.gameObject.activeSelf != (on && !isZone)) circleRoot.gameObject.SetActive(on && !isZone);
            if (zoneFrame != null && zoneFrame.gameObject.activeSelf != (on && isZone)) zoneFrame.gameObject.SetActive(on && isZone);
            if (!on) return;

            float k = 0.5f + 0.5f * Mathf.Sin((float)(Time.realtimeSinceStartupAsDouble * 1000.0 % 2_000_000d / 320.0));
            var c = PixelSprites.C(PixelSprites.P24, 0.45f + 0.45f * k);
            float lw = ViewKit.U(RingTexels);
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
            // radius in whole texels, in 2-texel steps (a handful of cached ring sprites per target)
            int rt = Mathf.Max(4, Mathf.RoundToInt(rad * PixelSprites.Ppu * 0.5f) * 2);
            var s = PixelSprites.Ring(rt, RingTexels, false);
            if (ring.sprite != s) ring.sprite = s;
            if (ring.color != c) ring.color = c;
        }
    }
}
