using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Light snowfall over one Island (ART-SPEC §3.8). Two art forms:
    /// <list type="bullet">
    /// <item>a frame loop (<c>fx_snowfall_256x256_4f</c>): the wrap-safe tile is laid on a grid that covers <see cref="area"/>
    /// (Island-local cells) and the frames play at <see cref="fps"/>;</item>
    /// <item>a single wrap-safe still (<c>fx_snowfall_256x256_1f</c>, owner pick <c>anim_route</c>: motion in code): the same grid
    /// (one extra row and column) is scrolled by <see cref="scrollPxPerSec"/> (art pixels per second, +x right, +y up) and wrapped
    /// every tile, snapped to whole art pixels so the 1-px flakes never shimmer between screen pixels.</item>
    /// </list>
    /// Built at runtime (no per-tile objects in the scene file). Calm motion (<see cref="Juice.ReduceMotion"/>): the overlay is
    /// hidden, because frozen flakes would read as dust specks. Time comes from <see cref="CodeAnimClock"/>.
    /// </summary>
    public sealed class SnowfallOverlay : MonoBehaviour
    {
        public Sprite[] frames;
        public Rect area;
        public float fps = 6f;
        /// <summary>Scroll speed for a single still, in art pixels per second (down-right drift = +x, -y).</summary>
        public Vector2 scrollPxPerSec = new Vector2(4f, -12f);
        public string sortingLayer = "Overlay";
        public int sortingOrder = 5;
        SpriteRenderer[] tiles;
        GameObject holder;
        int shown = -1;
        float tileW, tileH, texel;
        Vector2 basePos;

        bool Scrolls => frames != null && frames.Length == 1;

        void Awake()
        {
            if (frames == null || frames.Length == 0 || frames[0] == null) { enabled = false; return; }
            tileW = frames[0].bounds.size.x; tileH = frames[0].bounds.size.y;
            texel = 1f / frames[0].pixelsPerUnit;
            int extra = Scrolls ? 1 : 0;   // a scrolled grid starts one tile left / below and covers one more
            int nx = Mathf.CeilToInt(area.width / tileW) + extra, ny = Mathf.CeilToInt(area.height / tileH) + extra;
            holder = new GameObject("Tiles");
            holder.transform.SetParent(transform, false);
            basePos = new Vector2(area.xMin - (Scrolls ? tileW : 0f), area.yMin - (Scrolls ? tileH : 0f));
            tiles = new SpriteRenderer[nx * ny];
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    var sr = new GameObject("S" + x + "_" + y).AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(holder.transform, false);
                    sr.transform.localPosition = new Vector3((x + 0.5f) * tileW, (y + 0.5f) * tileH, 0f);
                    sr.sortingLayerName = sortingLayer;
                    sr.sortingOrder = sortingOrder;
                    sr.sprite = frames[0];
                    tiles[y * nx + x] = sr;
                }
            holder.transform.localPosition = new Vector3(basePos.x, basePos.y, 0f);
        }

        void Update()
        {
            if (tiles == null) return;
            bool calm = Juice.ReduceMotion;
            if (holder.activeSelf == calm) holder.SetActive(!calm);
            if (calm) return;
            float t = CodeAnimClock.Now;
            if (Scrolls)
            {
                // offset in whole art pixels, wrapped into [0, tile) so the grid never drifts away from the area
                float ox = Wrap(Mathf.Floor(t * scrollPxPerSec.x) * texel, tileW);
                float oy = Wrap(Mathf.Floor(t * scrollPxPerSec.y) * texel, tileH);
                holder.transform.localPosition = new Vector3(basePos.x + ox, basePos.y + oy, 0f);
                return;
            }
            int f = Mathf.FloorToInt(t * fps) % frames.Length;
            if (f == shown) return;
            shown = f;
            var s = frames[f];
            for (int i = 0; i < tiles.Length; i++) tiles[i].sprite = s;
        }

        static float Wrap(float v, float m) { v %= m; return v < 0f ? v + m : v; }
    }
}
