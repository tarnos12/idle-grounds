using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>One loose ground item: a single icon 20 world px wide, no stack numbers (§2.7).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class GroundItemView : MonoBehaviour
    {
        public const float SizePx = 20f;

        [SerializeField] SpriteRenderer spriteRenderer;

        public string Area { get; private set; }
        public GroundItem Item { get; private set; }
        internal int seenFrame;
        string boundItem;

        public void Bind(string area, GroundItem g, Sprite sprite, AreaSpace space)
        {
            Area = area; Item = g;
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

        public void Refresh(AreaSpace space) => transform.position = space.PxToWorld(Area, Item.x, Item.y);

        void Reset() => spriteRenderer = GetComponent<SpriteRenderer>();
    }
}
