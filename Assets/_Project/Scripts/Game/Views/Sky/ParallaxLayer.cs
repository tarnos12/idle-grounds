using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One layer of the sky behind the floating Islands (ADR 0003, ART-SPEC §3.3), following the main
    /// camera in LateUpdate (after <see cref="CameraController"/> moved it):
    /// <list type="bullet">
    /// <item><see cref="Mode.Fill"/> — the sky gradient: stretched over the whole view.</item>
    /// <item><see cref="Mode.Band"/> — a horizontally tileable strip (clouds far/mid/near, peaks): a Tiled
    ///   SpriteRenderer pinned to a screen band (<see cref="bandCentre"/>, <see cref="bandHeight"/> as view-height
    ///   fractions) whose texture scrolls by <see cref="scroll"/> × the camera's world x (0 = glued to the screen,
    ///   1 = moves with the world) plus a slow <see cref="drift"/> — the parallax.</item>
    /// </list>
    /// <item><see cref="Mode.Tile2D"/> — a texture that tiles in BOTH axes (the top-down cloud sea, ADR 0005): a Tiled
    ///   SpriteRenderer drawn at an integer <see cref="pixelScale"/> (never stretched), covering the view, scrolling by
    ///   <see cref="scroll"/> × the camera position in x AND y (depth) plus a slow wind <see cref="windDrift"/>.</item>
    /// Sizes follow the view, so the sky looks the same at every zoom. Real art drops in by replacing the sprite.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParallaxLayer : MonoBehaviour
    {
        public enum Mode { Fill, Band, Fixed, Tile2D }

        public Mode mode = Mode.Band;
        [Tooltip("Band: vertical centre as a fraction of the view height from the view centre (-0.5 = bottom edge).")]
        public float bandCentre = -0.25f;
        [Tooltip("Band: height as a fraction of the view height.")]
        public float bandHeight = 0.35f;
        [Tooltip("Band: how much the layer moves with the world (0 = screen-fixed, 1 = world-fixed).")]
        [Range(0f, 1f)] public float scroll = 0.2f;
        [Tooltip("Band: extra vertical parallax (fraction of the camera's world y, wrapped into ±0.1 view).")]
        [Range(0f, 0.2f)] public float verticalScroll = 0.02f;
        [Tooltip("Band: constant drift in screen-widths per minute (wind).")]
        public float drift = 0.05f;
        [Tooltip("Fixed (moon): anchor from the left edge of the view (0..1); scroll = tiny parallax.")]
        public float anchorX = 0.18f;
        [Tooltip("Fixed: anchor from the top edge of the view (0..1).")]
        public float anchorY = 0.15f;
        [Tooltip("Fixed: view width (world units) at which the sprite is drawn at native size; it scales with the view.")]
        public float refViewWidth = 35f;
        [Tooltip("Tile2D: integer art scale (1 = native PPU 32, 2 = 2x).")]
        public int pixelScale = 1;
        [Tooltip("Tile2D: wind drift in world units per second (x, y).")]
        public Vector2 windDrift = new Vector2(0.15f, 0f);
        [Tooltip("Z of the layer (in front of the camera's near plane).")]
        public float z = 10f;

        [SerializeField] Camera target;
        SpriteRenderer sr;

        public float OffsetX { get; private set; }

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (target == null) target = Camera.main;
        }

        void LateUpdate()
        {
            if (target == null) { target = Camera.main; if (target == null) return; }
            if (sr == null || sr.sprite == null) return;
            float h = target.orthographicSize * 2f, w = h * target.aspect;
            var cp = target.transform.position;
            var sb = sr.sprite.bounds.size;
            if (mode == Mode.Fill)
            {
                sr.drawMode = SpriteDrawMode.Simple;
                transform.position = new Vector3(cp.x, cp.y, z);
                transform.localScale = new Vector3(w * 1.02f / Mathf.Max(0.0001f, sb.x), h * 1.02f / Mathf.Max(0.0001f, sb.y), 1f);
                return;
            }

            if (mode == Mode.Fixed)
            {
                sr.drawMode = SpriteDrawMode.Simple;
                float k1 = w / Mathf.Max(0.01f, refViewWidth);
                transform.localScale = new Vector3(k1, k1, 1f);
                float px = Mathf.Clamp(-cp.x * scroll, -0.05f * w, 0.05f * w), py = Mathf.Clamp(-cp.y * scroll, -0.05f * h, 0.05f * h);
                transform.position = new Vector3(cp.x + (anchorX - 0.5f) * w + px, cp.y + (0.5f - anchorY) * h + py, z);
                return;
            }

            if (mode == Mode.Tile2D)
            {
                float ks = Mathf.Max(1, pixelScale);
                if (sr.drawMode != SpriteDrawMode.Tiled) { sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous; }
                transform.localScale = new Vector3(ks, ks, 1f);
                float tw = sb.x * ks, th = sb.y * ks;
                sr.size = new Vector2((w + 2f * tw) / ks, (h + 2f * th) / ks);
                float t = Time.unscaledTime;
                // scroll = 1: fixed in the world; 0: glued to the screen
                float ox = Mathf.Repeat(cp.x * scroll - windDrift.x * t, tw), oy = Mathf.Repeat(cp.y * scroll - windDrift.y * t, th);
                float px = cp.x - ox, py = cp.y - oy;
                float g = 1f / (32f / ks);
                transform.position = new Vector3(Mathf.Round(px / g) * g, Mathf.Round(py / g) * g, z);
                return;
            }

            // band: scale so the strip's height is bandHeight × view height; tile it horizontally
            float bandH = Mathf.Max(0.01f, bandHeight * h);
            float k = bandH / Mathf.Max(0.0001f, sb.y);
            float tileW = sb.x * k;
            if (sr.drawMode != SpriteDrawMode.Tiled) sr.drawMode = SpriteDrawMode.Tiled;
            transform.localScale = new Vector3(k, k, 1f);
            sr.size = new Vector2((w + 2f * tileW) / k, sb.y);
            // scroll: the camera's world x (in screen widths) times the factor, plus wind drift
            float travel = cp.x * scroll + Time.unscaledTime / 60f * drift * w;
            OffsetX = -Mathf.Repeat(travel, tileW);
            float vy = Mathf.Clamp(-cp.y * verticalScroll, -0.1f * h, 0.1f * h);
            transform.position = new Vector3(cp.x + OffsetX, cp.y + bandCentre * h + vy, z);
        }
    }
}
