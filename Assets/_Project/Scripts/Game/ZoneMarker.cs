using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleGrounds.Game
{
    [Serializable]
    public struct CellRect
    {
        public int r0, c0, r1, c1; // inclusive, region-local (row, col)
        public CellRect(int r0, int c0, int r1, int c1) { this.r0 = r0; this.c0 = c0; this.r1 = r1; this.c1 = c1; }
        public int Width => c1 - c0 + 1;
        public int Height => r1 - r0 + 1;
    }

    /// <summary>
    /// Named zone (data-catalog section 4) as a list of inclusive cell rects. The GameObject sits at the
    /// region origin; gizmos make zones visible in the Scene view.
    /// </summary>
    public class ZoneMarker : MonoBehaviour
    {
        public string zoneName;
        [Tooltip("noBuild, generator, enemy, fixture, spawner...")]
        public string role;
        public Color color = Color.white;
        public List<CellRect> rects = new List<CellRect>();

        public Rect WorldRect(CellRect r)
        {
            var o = transform.position;
            return new Rect(o.x + r.c0, o.y - r.r1 - 1, r.Width, r.Height);
        }

        void OnDrawGizmos()
        {
            foreach (var cr in rects)
            {
                var w = WorldRect(cr);
                var centre = new Vector3(w.center.x, w.center.y, 0f);
                var size = new Vector3(w.width, w.height, 0.01f);
                var fill = color; fill.a = 0.12f;
                Gizmos.color = fill;
                Gizmos.DrawCube(centre, size);
                var line = color; line.a = 0.9f;
                Gizmos.color = line;
                Gizmos.DrawWireCube(centre, size);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(new Vector3(w.xMin + 0.5f, w.yMax - 0.5f, 0f), zoneName);
#endif
            }
        }
    }
}
