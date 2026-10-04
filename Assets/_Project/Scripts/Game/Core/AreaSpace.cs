using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Maps area-local sim pixels (32 px/cell, +y down) to world units (1 cell = 1 unit, +y up) and back.
    /// Region top-left sits at world (rx*stride, -ry*stride) — the same layout <see cref="Region"/> uses.
    /// </summary>
    public sealed class AreaSpace
    {
        readonly int cell, cells, stride;
        readonly List<RegionDef> regions;
        readonly Dictionary<string, Vector2> origins = new Dictionary<string, Vector2>();

        public AreaSpace(GameConfig cfg)
        {
            cell = cfg.grid.cell;
            cells = cfg.grid.cells;
            stride = cfg.grid.cells + cfg.grid.gap;
            regions = cfg.regions;
            foreach (var r in regions) origins[r.key] = new Vector2(r.rx * stride, -r.ry * stride);
        }

        public int Cell => cell;
        public int Cells => cells;

        /// <summary>World position of the region's top-left corner.</summary>
        public Vector2 Origin(string area) => origins.TryGetValue(area, out var o) ? o : Vector2.zero;

        public float PxToUnits(double px) => (float)(px / cell);

        /// <summary>Area-local px (x right, y down) -> world point (z = 0).</summary>
        public Vector3 PxToWorld(string area, double x, double y)
        {
            var o = Origin(area);
            return new Vector3(o.x + (float)(x / cell), o.y - (float)(y / cell), 0f);
        }

        /// <summary>World -> (area, local px). False in the void/gaps. Locked regions are returned too.</summary>
        public bool WorldToArea(Vector2 w, out string area, out double x, out double y)
        {
            int gCol = Mathf.FloorToInt(w.x), gRow = Mathf.FloorToInt(-w.y);
            foreach (var r in regions)
            {
                int r0 = r.ry * stride, c0 = r.rx * stride;
                if (gRow >= r0 && gRow < r0 + cells && gCol >= c0 && gCol < c0 + cells)
                {
                    var o = origins[r.key];
                    area = r.key;
                    x = (w.x - o.x) * cell;
                    y = (o.y - w.y) * cell;
                    return true;
                }
            }
            area = null; x = y = 0;
            return false;
        }

        /// <summary>Region under a world point, else the region whose centre is nearest (location pill rule).</summary>
        public string RegionAtOrNearest(Vector2 w)
        {
            if (WorldToArea(w, out var a, out _, out _)) return a;
            string best = null; float bd = float.MaxValue;
            foreach (var r in regions)
            {
                var c = origins[r.key] + new Vector2(cells * 0.5f, -cells * 0.5f);
                float d = (c - w).sqrMagnitude;
                if (d < bd) { bd = d; best = r.key; }
            }
            return best;
        }
    }
}
