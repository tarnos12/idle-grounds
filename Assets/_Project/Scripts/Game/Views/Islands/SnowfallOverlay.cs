using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Light snowfall over one Island (ART-SPEC §3.8, <c>fx_snowfall_256x256_4f</c>): the 4-frame, wrap-safe 256 px tile is laid on
    /// a grid that covers <see cref="area"/> (Island-local cells) and the frames play at <see cref="fps"/>. Built at runtime
    /// (no per-tile objects in the scene file). Calm motion (<see cref="Juice.ReduceMotion"/>): the overlay is hidden, because
    /// frozen flakes would read as dust specks. Time comes from <see cref="CodeAnimClock"/>.
    /// </summary>
    public sealed class SnowfallOverlay : MonoBehaviour
    {
        public Sprite[] frames;
        public Rect area;
        public float fps = 6f;
        public string sortingLayer = "Overlay";
        public int sortingOrder = 5;
        SpriteRenderer[] tiles;
        GameObject holder;
        int shown = -1;

        void Awake()
        {
            if (frames == null || frames.Length == 0 || frames[0] == null) { enabled = false; return; }
            float w = frames[0].bounds.size.x, h = frames[0].bounds.size.y;
            int nx = Mathf.CeilToInt(area.width / w), ny = Mathf.CeilToInt(area.height / h);
            holder = new GameObject("Tiles");
            holder.transform.SetParent(transform, false);
            tiles = new SpriteRenderer[nx * ny];
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    var sr = new GameObject("S" + x + "_" + y).AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(holder.transform, false);
                    sr.transform.localPosition = new Vector3(area.xMin + (x + 0.5f) * w, area.yMin + (y + 0.5f) * h, 0f);
                    sr.sortingLayerName = sortingLayer;
                    sr.sortingOrder = sortingOrder;
                    sr.sprite = frames[0];
                    tiles[y * nx + x] = sr;
                }
        }

        void Update()
        {
            if (tiles == null) return;
            bool calm = Juice.ReduceMotion;
            if (holder.activeSelf == calm) holder.SetActive(!calm);
            if (calm) return;
            int f = Mathf.FloorToInt(CodeAnimClock.Now * fps) % frames.Length;
            if (f == shown) return;
            shown = f;
            var s = frames[f];
            for (int i = 0; i < tiles.Length; i++) tiles[i].sprite = s;
        }
    }
}
