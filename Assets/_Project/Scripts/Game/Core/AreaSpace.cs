using System.Collections.Generic;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Maps Island-local sim pixels (32 px/cell, +y down) to world units (1 cell = 1 unit, +y up) and back.
    /// An Island's top-left corner sits at world (offsetPx.x / cell, -offsetPx.y / cell) — the sim's Island
    /// offsets, which <see cref="GameRunner"/> fills from the scene's <see cref="Island"/> transforms (ADR 0003).
    /// Also converts the sim's WORLD px (sky wisps, bridge flights) to world units.
    /// </summary>
    public sealed class AreaSpace
    {
        readonly int cell, cells;
        readonly List<RegionDef> islands;
        readonly Dictionary<string, Vector2> origins = new Dictionary<string, Vector2>();

        /// <summary>Origins from the sim's Island offsets (scene authority once GameRunner applied them).</summary>
        public AreaSpace(GameConfig cfg, Simulation sim)
        {
            cell = cfg.grid.cell;
            cells = cfg.grid.cells;
            islands = cfg.regions;
            foreach (var r in islands)
            {
                var (ox, oy) = sim != null ? sim.IslandOffsetPx(r.key) : ((double)r.islandCol * cell, (double)r.islandRow * cell);
                origins[r.key] = new Vector2((float)(ox / cell), -(float)(oy / cell));
            }
        }

        /// <summary>Config default origins (no scene / sim).</summary>
        public AreaSpace(GameConfig cfg) : this(cfg, null) { }

        public int Cell => cell;
        public int Cells => cells;

        /// <summary>World position of the Island's top-left corner.</summary>
        public Vector2 Origin(string area) => origins.TryGetValue(area, out var o) ? o : Vector2.zero;

        /// <summary>World centre of the Island.</summary>
        public Vector2 IslandCentre(string area) => Origin(area) + new Vector2(cells * 0.5f, -cells * 0.5f);

        /// <summary>World rect (x,y = bottom-left) of the Island's play area.</summary>
        public Rect IslandRect(string area) { var o = Origin(area); return new Rect(o.x, o.y - cells, cells, cells); }

        public float PxToUnits(double px) => (float)(px / cell);

        /// <summary>Island-local px (x right, y down) -> world point (z = 0).</summary>
        public Vector3 PxToWorld(string area, double x, double y)
        {
            var o = Origin(area);
            return new Vector3(o.x + (float)(x / cell), o.y - (float)(y / cell), 0f);
        }

        /// <summary>Sim WORLD px (Island offset + local px; sky wisps) -> world point.</summary>
        public Vector3 WorldPxToWorld(double x, double y) => new Vector3((float)(x / cell), -(float)(y / cell), 0f);

        /// <summary>World -> (Island, local px). False over open sky. Locked Islands are returned too.</summary>
        public bool WorldToArea(Vector2 w, out string area, out double x, out double y)
        {
            foreach (var r in islands)
            {
                var o = origins[r.key];
                float lx = w.x - o.x, ly = o.y - w.y;
                if (lx >= 0 && lx < cells && ly >= 0 && ly < cells)
                {
                    area = r.key;
                    x = lx * cell;
                    y = ly * cell;
                    return true;
                }
            }
            area = null; x = y = 0;
            return false;
        }

        /// <summary>Island under a world point, else the Island whose centre is nearest (location pill rule).</summary>
        public string IslandAtOrNearest(Vector2 w)
        {
            if (WorldToArea(w, out var a, out _, out _)) return a;
            string best = null; float bd = float.MaxValue;
            foreach (var r in islands)
            {
                float d = (IslandCentre(r.key) - w).sqrMagnitude;
                if (d < bd) { bd = d; best = r.key; }
            }
            return best;
        }
    }
}
