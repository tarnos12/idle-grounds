using IdleGrounds.Game.Data;
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
        const float PunchSec = 0.12f, ShakeSec = 0.22f;

        [SerializeField] Transform squash;
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] SpriteRenderer pad;
        [SerializeField] SortingGroup sortingGroup;
        [Tooltip("Gold AUTO badge, shown while the automation bot just swung this node (autoFlash).")]
        [SerializeField] TMPro.TextMeshPro autoBadge;
        [Tooltip("Fish surface countdown \"x.xs\" (#f87171 800 11 px, 10 px under the sprite base): last second or while hovered.")]
        [SerializeField] TMPro.TextMeshPro countdown;
        [Tooltip("Three sparkles over the Spirit Tree (§2.5).")]
        [SerializeField] SpriteRenderer[] sparkles;

        public bool AutoBadgeShown => autoBadge != null && autoBadge.gameObject.activeSelf;
        public bool SparklesShown => (overlay != null && overlay.gameObject.activeSelf) || sparkles != null && sparkles.Length > 0 && sparkles[0] != null && sparkles[0].gameObject.activeSelf;
        static readonly Vector2[] SparkleAt = { new Vector2(-0.28f, -0.72f), new Vector2(0.30f, -0.55f), new Vector2(0.05f, -0.92f) };

        public string Area { get; private set; }
        public Node Node { get; private set; }
        internal int seenFrame;

        float spriteUnits;          // sprite height in world units
        Vector3 basePos;
        Vector3 spriteLocal;

        /// <summary>`spriteSize(size)` ui.js:549, in world px.</summary>
        public static float SpriteSizePx(int size) => size >= 4 ? size * 30 : size >= 3 ? 90 : size >= 2 ? 72 : 30;

        const float ArtFps = 6f;
        const float OverlayFps = 8f;    // ART-SPEC: overlay loops play at 8 fps
        const float ArtPpu = 32f;
        Sprite[] artFrames;     // real art with >1 frames
        Sprite[] overlayFrames; // real-art overlay strip (Spirit Tree sparkle), drawn just above the sprite
        SpriteRenderer overlay; // lazily created child of the sprite renderer (same pivot / scale / flip)
        bool realArt;
        int maxHits;

        public void Bind(string area, Node node, Sprite sprite, AreaSpace space, SpriteEntry art = null)
        {
            Area = area; Node = node;
            name = $"Node_{area}_{node.id}_{(node.isFixed ? node.kind : node.spawnerKind ?? node.kind)}";
            int cell = space.Cell;
            realArt = art != null && art.realArt && art.sprite != null;
            bool decoArt = realArt && node.deco;
            // deco trees: frames are 3 static variants, picked by hash of id/position (not animated)
            artFrames = realArt && !decoArt && art.frames != null && art.frames.Length > 1 ? art.frames : null;
            maxHits = Mathf.Max(1, node.hitsLeft);
            if (realArt) sprite = art.sprite;
            overlayFrames = realArt && !decoArt && art.overlay != null && art.overlay.Length > 0 ? art.overlay : null;
            if (overlayFrames != null && overlay == null)
            {
                var go = new GameObject("Overlay");
                go.transform.SetParent(spriteRenderer.transform, false);
                overlay = go.AddComponent<SpriteRenderer>();
                overlay.sortingLayerID = spriteRenderer.sortingLayerID;
                overlay.sortingOrder = spriteRenderer.sortingOrder + 1;
                overlay.sharedMaterial = spriteRenderer.sharedMaterial;
            }
            if (overlay != null)
            {
                overlay.gameObject.SetActive(overlayFrames != null);
                if (overlayFrames != null) overlay.sprite = overlayFrames[0];
            }
            bool flipX = false;
            if (decoArt)
            {
                var vf = art.frames != null && art.frames.Length > 0 ? art.frames : new[] { art.sprite };
                uint h0 = unchecked((uint)(node.id * 73856093) ^ (uint)(node.col * 19349663) ^ (uint)(node.row * 83492791));
                h0 ^= h0 >> 13; h0 *= 0x5bd1e995u; h0 ^= h0 >> 15;
                sprite = vf[(int)(h0 % (uint)vf.Length)];
                flipX = ((h0 >> 8) & 1u) != 0;
            }
            // anchor: bottom-centre of the node square, 2 px up
            basePos = space.PxToWorld(area, (node.col + node.size / 2.0) * cell, (node.row + node.size) * cell - 2);
            float px = node.deco ? 30f * (float)(node.decoScale > 0 ? node.decoScale : 1.8) : SpriteSizePx(node.size);
            float k;
            float bottom;
            if (realArt)
            {
                // native PPU-32 size, bottom-aligned on the footprint's bottom edge (2 px below the anchor), centred
                k = ArtPpu / cell;
                var bd = sprite.bounds;
                px = bd.size.y * ArtPpu;
                spriteUnits = bd.size.y * k;
                bottom = -bd.min.y * k - 2f / cell;
                spriteRenderer.sprite = sprite;
                spriteRenderer.transform.localScale = new Vector3(k, k, 1f);
                spriteLocal = new Vector3(-bd.center.x * k, bottom, 0f);
            }
            else
            {
            spriteUnits = px / cell;
            spriteRenderer.sprite = sprite;
            float h = sprite != null ? Mathf.Max(0.0001f, sprite.bounds.size.y) : 1f;
            k = spriteUnits / h;
            spriteRenderer.transform.localScale = new Vector3(k, k, 1f);
            // sprite pivot is its centre: lift it so its bottom rests on the anchor
            bottom = sprite != null ? -sprite.bounds.min.y * k : spriteUnits * 0.5f;
            spriteLocal = new Vector3(0f, bottom, 0f);
            }
            if (node.deco) spriteLocal += new Vector3(node.decoDx / (float)cell, -node.decoDy / (float)cell, 0f);
            spriteRenderer.transform.localPosition = spriteLocal;
            spriteRenderer.flipX = flipX;
            if (overlay != null) overlay.flipX = flipX;
            var c = spriteRenderer.color; c.a = node.deco && !decoArt ? 0.55f : 1f; spriteRenderer.color = c;

            pad.gameObject.SetActive(!node.deco && !realArt);
            if (!node.deco && !realArt)
            {
                // pad ellipse rgba(74,222,128,.16), rx 0.42*w, ry 7 px
                float w = node.size;   // node square width in units
                pad.transform.localScale = new Vector3(2f * 0.42f * w, 2f * 7f / cell, 1f);
                pad.transform.localPosition = Vector3.zero;
            }
            // Spirit Tree: three sparkles at 22% of the sprite size (offsets x fontPx, y down)
            bool tree = node.isFixed && node.kind == "spirittree" && overlayFrames == null;   // emoji sparkles only without the art overlay
            if (sparkles != null)
                for (int i = 0; i < sparkles.Length; i++)
                {
                    var s = sparkles[i];
                    if (s == null) continue;
                    s.gameObject.SetActive(tree && i < SparkleAt.Length);
                    if (!tree || i >= SparkleAt.Length) continue;
                    ViewKit.Fit(s, s.sprite, px * 0.22f);
                    s.transform.localPosition = new Vector3(SparkleAt[i].x * spriteUnits, -SparkleAt[i].y * spriteUnits, 0f);
                }
            if (autoBadge != null)
            {
                autoBadge.gameObject.SetActive(false);
                autoBadge.transform.localPosition = new Vector3(spriteUnits * 0.42f, spriteUnits + ViewKit.U(4f), 0f);
            }
            if (countdown != null) { countdown.gameObject.SetActive(false); lastTenths = -1; }
            transform.position = basePos;
            sortingGroup.sortingOrder = node.row;      // back-to-front by row (§2.1)
            squash.localScale = Vector3.one;
            squash.localPosition = Vector3.zero;
            punchAt = -10f; punchFinal = false; seenHitAt = node.hitAt;
        }

        float punchAt = -10f;
        double seenHitAt;
        bool punchFinal;

        /// <summary>Hit feedback (FxService, from Sim NodeHit): squash punch; <paramref name="final"/> = felling/cracking blow.</summary>
        public void Punch(bool final) { punchAt = Time.unscaledTime; punchFinal = final; }

        /// <summary>Per-frame animation from sim state: hit squash, fish bob.</summary>
        public void Refresh(double now, int cell, bool autoFlash = false, bool unlocked = true, bool hovered = false)
        {
            var n = Node;
            if (autoBadge != null && autoBadge.gameObject.activeSelf != autoFlash) autoBadge.gameObject.SetActive(autoFlash);
            // juice: squash-and-stretch punch (0.9 / 1.1 over ~120 ms) on every hit, soft shake on the final one
            if (n.hitAt != seenHitAt) { seenHitAt = n.hitAt; if (n.hitAt > 0) punchAt = Time.unscaledTime; }
            float sx = 1f, sy = 1f, shakeX = 0f;
            if (!Juice.ReduceMotion)
            {
                float age = Time.unscaledTime - punchAt;
                if (age < PunchSec)
                {
                    float t = age / PunchSec;
                    float k = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;     // snap in, ease out
                    float amp = punchFinal ? 0.15f : 0.10f;
                    sy = 1f - amp * k; sx = 1f + amp * k;
                }
                if (punchFinal && age < ShakeSec)
                    shakeX = Mathf.Round(Mathf.Sin(age * 80f) * (1f - age / ShakeSec) * 1.6f) / cell;   // whole-pixel wobble
            }
            squash.localScale = new Vector3(sx, sy, 1f);
            squash.localPosition = new Vector3(shakeX, 0f, 0f);

            float bob = 0f;
            bool surfaced = unlocked && n.interaction == NodeInteraction.Surface && n.surfaceUntil > now;
            if (surfaced)
                bob = Mathf.Abs(Mathf.Sin((float)(now % 1_000_000d / 300d))) * 5f / cell;
            spriteRenderer.transform.localPosition = spriteLocal + new Vector3(0f, bob, 0f);

            if (artFrames != null)
            {
                int fi = 0;
                if (artFrames.Length == 2 && (n.interaction == NodeInteraction.Chop || n.interaction == NodeInteraction.Break || n.interaction == NodeInteraction.Quarry))
                    fi = (n.hitsLeft < maxHits || n.clicks > 0) ? 1 : 0;      // intact -> cracked / trimmed
                else if (n.interaction != NodeInteraction.Surface || surfaced)
                    fi = (int)(Time.unscaledTime * ArtFps) % artFrames.Length;
                if (spriteRenderer.sprite != artFrames[fi]) spriteRenderer.sprite = artFrames[fi];
            }
            if (overlayFrames != null)
            {
                int oi = (int)(Time.unscaledTime * OverlayFps) % overlayFrames.Length;
                if (overlay.sprite != overlayFrames[oi]) overlay.sprite = overlayFrames[oi];
            }

            // fishing countdown (ui.js:1257): only in the last second, or while hovered
            if (countdown != null)
            {
                double left = n.surfaceUntil - now;
                bool show = surfaced && (left < 1000 || hovered);
                if (countdown.gameObject.activeSelf != show) countdown.gameObject.SetActive(show);
                if (show)
                {
                    int tenths = (int)System.Math.Round(left / 100.0);
                    if (tenths != lastTenths)
                    {
                        lastTenths = tenths;
                        countdown.text = TenthsLabel(tenths);
                    }
                    countdown.transform.localPosition = new Vector3(0f, bob - 10f / cell, 0f);
                }
            }
        }

        int lastTenths = -1;
        static string[] tenthsCache;
        /// <summary>"x.xs" for a count of tenths (cached, no per-frame allocation).</summary>
        static string TenthsLabel(int tenths)
        {
            if (tenths < 0) tenths = 0;
            if (tenths >= 600) return (tenths / 10.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
            tenthsCache ??= new string[600];
            return tenthsCache[tenths] ??= (tenths / 10.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
        }
    }
}
