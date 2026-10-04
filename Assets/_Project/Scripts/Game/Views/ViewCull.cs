using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Per-frame world rect of the main camera expanded by the original's 4-cell paint margin
    /// (ui.js:597 <c>fr = 4 * CELL</c>). Views skip their per-frame refresh outside it.
    /// </summary>
    public static class ViewCull
    {
        public const float MarginCells = 4f;

        static int frame = -1;
        static Rect rect;
        static bool valid;

        /// <summary>Camera rect + margin (world units). False when there is no orthographic main camera.</summary>
        public static bool TryGet(out Rect r)
        {
            if (frame != Time.frameCount)
            {
                frame = Time.frameCount;
                var cam = Camera.main;
                valid = cam != null && cam.orthographic;
                if (valid)
                {
                    float h = cam.orthographicSize, w = h * cam.aspect;
                    var p = cam.transform.position;
                    rect = new Rect(p.x - w - MarginCells, p.y - h - MarginCells, 2f * (w + MarginCells), 2f * (h + MarginCells));
                }
            }
            r = rect;
            return valid;
        }

        /// <summary>Camera rect without margin (world units).</summary>
        public static bool TryGetView(out Rect r)
        {
            if (!TryGet(out r)) return false;
            r = new Rect(r.x + MarginCells, r.y + MarginCells, r.width - 2f * MarginCells, r.height - 2f * MarginCells);
            return true;
        }

        /// <summary>True when the world rect given by its top-left corner and size (units, +y up) touches the culled view.</summary>
        public static bool Visible(Vector3 topLeft, float w, float h)
        {
            if (!TryGet(out var r)) return true;
            return topLeft.x < r.xMax && topLeft.x + w > r.xMin && topLeft.y > r.yMin && topLeft.y - h < r.yMax;
        }

        /// <summary>Point test with an extra radius (units).</summary>
        public static bool Visible(Vector3 p, float radius = 0f)
        {
            if (!TryGet(out var r)) return true;
            return p.x + radius >= r.xMin && p.x - radius <= r.xMax && p.y + radius >= r.yMin && p.y - radius <= r.yMax;
        }
    }
}
