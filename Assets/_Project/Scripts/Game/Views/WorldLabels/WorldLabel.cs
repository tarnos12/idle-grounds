using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>How a <see cref="WorldLabel"/> scales with the camera zoom.</summary>
    public enum WorldLabelScale
    {
        /// <summary>Building strips, counters, tags: constant on-screen size, clamped so the label stays
        /// between 0.75x and 1.5x of its zoom-2 size relative to the art (world scale zoom/2 in [0.5, 1.5]).</summary>
        Strip,
        /// <summary>Island names: always the same on-screen size (world scale = zoom / 2 x bannerScale).</summary>
        Banner,
    }

    /// <summary>
    /// One world-space label in the shared style (VP-07): dark P01 pill at 80% with a 1-texel P04 border,
    /// UI font with a 1-px ink outline, fixed sort order on the Overlay layer (above nodes and buildings,
    /// below every canvas). The owner view lays out the children in label-local units (1 unit = 1 cell,
    /// zoom-2 size, pivot = the anchor point) and calls <see cref="SetAnchor"/> / <see cref="SetBounds"/>;
    /// <see cref="WorldLabelSystem"/> then scales it per zoom, applies the zoom LOD, nudges it vertically
    /// when it would overlap another label (VP-10), and snaps it to the screen pixel grid.
    /// <para><see cref="content"/> is everything that hides with LOD / collision; <see cref="keep"/> (optional,
    /// e.g. a status shape) stays visible in the far band and when the content is suppressed.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldLabel : MonoBehaviour
    {
        public const string Layer = "Overlay";
        /// <summary>Sort orders on the Overlay layer: plate, then content, then the status shape.</summary>
        public const int OrderPlate = 60, OrderContent = 62, OrderKeep = 64;
        /// <summary>Text sizes in world px at zoom 2 (8 world px = ~14 screen px at 1080p).</summary>
        public const float TextPx = 9f, SmallPx = 8f, TitlePx = 11f;
        public static readonly Color TextColour = PixelSprites.C(PixelSprites.P07), MutedColour = PixelSprites.C(PixelSprites.P06),
            GoldColour = PixelSprites.C(PixelSprites.P24), GoodColour = PixelSprites.C(PixelSprites.P11), BadColour = PixelSprites.C(PixelSprites.P19),
            WarnColour = PixelSprites.C(PixelSprites.P30);

        public Transform content;
        public Transform keep;
        public WorldLabelScale scaleMode = WorldLabelScale.Strip;
        public float bannerScale = 1f;
        /// <summary>Content hides at zoom above this (keep stays). 99 = never.</summary>
        public float hideAboveZoom = 4.5f;
        /// <summary>Everything hides at zoom below this.</summary>
        public float hideBelowZoom = 0f;
        /// <summary><see cref="Compact"/> turns on at zoom above this (the owner draws a shorter strip).</summary>
        public float compactAboveZoom = 99f;
        public bool collide = true;
        /// <summary>Keep the whole label inside the camera view (cursor-following tags).</summary>
        public bool clampToView;
        /// <summary>Higher keeps its place; lower moves out of the way.</summary>
        public int priority;

        public bool Compact { get; internal set; }
        public bool Far { get; internal set; }
        public bool Suppressed { get; internal set; }
        /// <summary>Owner-side state flags changed (Compact / Far): relayout.</summary>
        public int LodVersion { get; internal set; }

        internal Vector3 anchor;
        internal Rect bounds = new Rect(-0.5f, 0f, 1f, 0.5f);
        internal Vector3 keepHome, keepFar;
        internal int index = -1;
        internal float placedOffset;

        /// <summary>Anchor point in the parent's local space (the label pivot sits here before any collision offset).</summary>
        public void SetAnchor(Vector3 local) { anchor = local; }

        /// <summary>Label-local box (units at zoom-2 size) used for overlap tests.</summary>
        public void SetBounds(float xMin, float yMin, float w, float h) { bounds = new Rect(xMin, yMin, w, h); }

        /// <summary>Where <see cref="keep"/> sits in the full strip, and where it sits alone (far / suppressed).</summary>
        public void SetKeep(Vector3 home, Vector3 alone) { keepHome = home; keepFar = alone; }

        void OnEnable() => WorldLabelSystem.Register(this);
        void OnDisable() => WorldLabelSystem.Unregister(this);

        // ================= shared style helpers =================

        /// <summary>A label root + Content child under <paramref name="parent"/>.</summary>
        public static WorldLabel Create(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<WorldLabel>();
            var c = new GameObject("Content").transform;
            c.SetParent(go.transform, false);
            l.content = c;
            return l;
        }

        /// <summary>The sliced plate renderer (size set by <see cref="SizePlate"/>).</summary>
        public static SpriteRenderer NewPlate(Transform parent)
        {
            var go = new GameObject("Plate");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelSprites.Plate;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingLayerName = Layer; sr.sortingOrder = OrderPlate;
            return sr;
        }

        /// <summary>Plate centred at (cx, cy) of size (w, h) units, sizes rounded up to whole texels.</summary>
        public static void SizePlate(SpriteRenderer plate, float cx, float cy, float w, float h)
        {
            if (plate == null) return;
            w = Mathf.Ceil(w * PixelSprites.Ppu) / PixelSprites.Ppu; h = Mathf.Ceil(h * PixelSprites.Ppu) / PixelSprites.Ppu;
            plate.transform.localPosition = new Vector3(cx, cy, 0f);
            var sz = new Vector2(w, h);
            if (plate.size != sz) plate.size = sz;
        }

        /// <summary>A new TMP text in the shared style.</summary>
        public static TextMeshPro NewText(Transform parent, string name, float px, Color c, TMP_FontAsset font = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            if (font != null) t.font = font;
            Style(t, px, c);
            return t;
        }

        /// <summary>Apply the shared text style: size, colour, bold, 1-px ink outline, no wrap, Overlay layer.</summary>
        public static void Style(TextMeshPro t, float px, Color c)
        {
            if (t == null) return;
            ViewKit.Font(t, px);
            t.color = c;
            t.fontStyle = FontStyles.Bold;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            ViewKit.Outline(t);
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null) { mr.sortingLayerName = Layer; mr.sortingOrder = OrderContent; }
        }

        /// <summary>A sprite renderer on the Overlay layer.</summary>
        public static SpriteRenderer NewSprite(Transform parent, string name, Sprite s, int order = OrderContent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingLayerName = Layer; sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Move a renderer onto the Overlay layer at <paramref name="order"/>.</summary>
        public static void Sort(Renderer r, int order)
        {
            if (r == null) return;
            r.sortingLayerName = Layer; r.sortingOrder = order;
        }
    }
}
