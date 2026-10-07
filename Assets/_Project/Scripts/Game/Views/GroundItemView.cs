using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One loose ground item: a single icon 20 world px wide, no stack numbers (§2.7). Juice: a freshly dropped
    /// item arcs out of its node (short parabola + one small bounce, ~340 ms) instead of appearing in place.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class GroundItemView : MonoBehaviour
    {
        public const float SizePx = 20f;
        const float PopSec = 0.34f, ArcFrac = 0.72f;

        [SerializeField] SpriteRenderer spriteRenderer;

        public string Area { get; private set; }
        public GroundItem Item { get; private set; }
        public SpriteRenderer Renderer => spriteRenderer;
        public bool Popping => popStart >= 0f;
        internal int seenFrame;
        string boundItem;
        float popStart = -1f, popHeight;
        Vector3 popFrom;

        public void Bind(string area, GroundItem g, Sprite sprite, AreaSpace space)
        {
            Area = area; Item = g;
            popStart = -1f;
            if (boundItem != g.item)
            {
                boundItem = g.item;
                spriteRenderer.sprite = sprite;
                float w = sprite != null ? Mathf.Max(0.0001f, sprite.bounds.size.x) : 1f;
                float k = SizePx / space.Cell / w;
                transform.localScale = new Vector3(k, k, 1f);
            }
            spriteRenderer.sortingOrder = g.id & 0x7FFF;
        }

        /// <summary>Start the drop arc from <paramref name="from"/> (world) to wherever the sim item sits.</summary>
        public void StartPop(Vector3 from)
        {
            if (Juice.ReduceMotion) return;
            popFrom = from; popStart = Time.unscaledTime;
            popHeight = (0.45f + 0.30f * ((Item.id * 37 & 255) / 255f));       // 14-24 px hop
        }

        public void Refresh(AreaSpace space)
        {
            var target = space.PxToWorld(Area, Item.x, Item.y);
            if (popStart < 0f) { transform.position = target; return; }
            float t = (Time.unscaledTime - popStart) / PopSec;
            if (t >= 1f) { popStart = -1f; transform.position = target; return; }
            Vector3 p; float h;
            if (t < ArcFrac)
            {
                float u = t / ArcFrac;
                p = Vector3.LerpUnclamped(popFrom, target, Juice.OutQuad(u));
                h = 4f * popHeight * u * (1f - u);
            }
            else
            {
                float u = (t - ArcFrac) / (1f - ArcFrac);
                p = target;
                h = 4f * popHeight * 0.18f * u * (1f - u);        // landing bounce
            }
            p.y += h;
            transform.position = p;
        }

        void Reset() => spriteRenderer = GetComponent<SpriteRenderer>();
    }
}
