using IdleGrounds.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleGrounds.Game
{
    /// <summary>
    /// A resource node / fixture / deco tree (ui-input-render §2.5). Root sits at the ground anchor
    /// (bottom-centre of the node square, 2 px up); the Squash child scales about it; the sprite is
    /// sized to spriteSize(size) world px. Pull-based: <see cref="Refresh"/> each LateUpdate.
    /// </summary>
    [RequireComponent(typeof(SortingGroup))]
    public class NodeView : MonoBehaviour
    {
        const float SquashMs = 180f;

        [SerializeField] Transform squash;
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] SpriteRenderer pad;
        [SerializeField] SortingGroup sortingGroup;

        public string Area { get; private set; }
        public Node Node { get; private set; }
        internal int seenFrame;

        float spriteUnits;          // sprite height in world units
        Vector3 basePos;
        Vector3 spriteLocal;

        /// <summary>`spriteSize(size)` ui.js:549, in world px.</summary>
        public static float SpriteSizePx(int size) => size >= 4 ? size * 30 : size >= 3 ? 90 : size >= 2 ? 72 : 30;

        public void Bind(string area, Node node, Sprite sprite, AreaSpace space)
        {
            Area = area; Node = node;
            name = $"Node_{area}_{node.id}_{(node.isFixed ? node.kind : node.spawnerKind ?? node.kind)}";
            int cell = space.Cell;
            // anchor: bottom-centre of the node square, 2 px up
            basePos = space.PxToWorld(area, (node.col + node.size / 2.0) * cell, (node.row + node.size) * cell - 2);
            float px = node.deco ? 30f * (float)(node.decoScale > 0 ? node.decoScale : 1.8) : SpriteSizePx(node.size);
            spriteUnits = px / cell;
            spriteRenderer.sprite = sprite;
            float h = sprite != null ? Mathf.Max(0.0001f, sprite.bounds.size.y) : 1f;
            float k = spriteUnits / h;
            spriteRenderer.transform.localScale = new Vector3(k, k, 1f);
            // sprite pivot is its centre: lift it so its bottom rests on the anchor
            float bottom = sprite != null ? -sprite.bounds.min.y * k : spriteUnits * 0.5f;
            spriteLocal = new Vector3(0f, bottom, 0f);
            if (node.deco) spriteLocal += new Vector3(node.decoDx / (float)cell, -node.decoDy / (float)cell, 0f);
            spriteRenderer.transform.localPosition = spriteLocal;
            var c = spriteRenderer.color; c.a = node.deco ? 0.55f : 1f; spriteRenderer.color = c;

            pad.gameObject.SetActive(!node.deco);
            if (!node.deco)
            {
                // pad ellipse rgba(74,222,128,.16), rx 0.42*w, ry 7 px
                float w = node.size;   // node square width in units
                pad.transform.localScale = new Vector3(2f * 0.42f * w, 2f * 7f / cell, 1f);
                pad.transform.localPosition = Vector3.zero;
            }
            transform.position = basePos;
            sortingGroup.sortingOrder = node.row;      // back-to-front by row (§2.1)
            squash.localScale = Vector3.one;
        }

        /// <summary>Per-frame animation from sim state: hit squash, fish bob.</summary>
        public void Refresh(double now, int cell)
        {
            var n = Node;
            float sy = 1f;
            double dt = now - n.hitAt;
            if (n.hitAt > 0 && dt >= 0 && dt < SquashMs)
            {
                float t = (float)(dt / SquashMs);
                sy = t < 0.35f ? Mathf.Lerp(1f, 0.84f, t / 0.35f)
                   : t < 0.7f ? Mathf.Lerp(0.84f, 1.06f, (t - 0.35f) / 0.35f)
                   : Mathf.Lerp(1.06f, 1f, (t - 0.7f) / 0.3f);
            }
            float sx = 1f + (1f - sy) * 0.4f;
            squash.localScale = new Vector3(sx, sy, 1f);

            float bob = 0f;
            if (n.interaction == NodeInteraction.Surface && n.surfaceUntil > now)
                bob = Mathf.Abs(Mathf.Sin((float)(now % 1_000_000d / 300d))) * 5f / cell;
            spriteRenderer.transform.localPosition = spriteLocal + new Vector3(0f, bob, 0f);
        }
    }
}
