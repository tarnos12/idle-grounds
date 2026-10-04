using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// World & grid helpers (engine-systems §3): region origins, regionAt,
    /// zones, noBuild, footprints, building lookup, placement validation.
    /// All simulation is per area in area-local cells/px.
    /// </summary>
    public sealed class World
    {
        readonly SimContext _ctx;
        public World(SimContext ctx) { _ctx = ctx; }

        GameConfig Cfg => _ctx.Config;
        GameState S => _ctx.State;

        /// <summary>`clampPx(v)` engine.js:9 = clamp(v, 4, PLAY_PX-4).</summary>
        public double ClampPx(double v) => Math.Max(4, Math.Min(_ctx.PlayPx - 4, v));

        // ---- regions (§3.1) ----
        public int Stride => Cfg.grid.cells + Cfg.grid.gap;

        /// <summary>`regionOrigin(k)` engine.js:2213 — global play-cell origin.</summary>
        public (int row, int col) RegionOrigin(string key)
        {
            var r = Cfg.Region(key);
            return r == null ? (0, 0) : (r.ry * Stride, r.rx * Stride);
        }

        /// <summary>`regionAt(gRow,gCol)` engine.js:2220 — null for void/gap.</summary>
        public string RegionAt(int gRow, int gCol)
        {
            int n = Cfg.grid.cells, st = Stride;
            foreach (var r in Cfg.regions)
                if (gRow >= r.ry * st && gRow < r.ry * st + n && gCol >= r.rx * st && gCol < r.rx * st + n) return r.key;
            return null;
        }

        public bool IsAreaUnlocked(string key) => S.world.IsUnlocked(key);

        // ---- Island world offsets (ADR 0003) ----
        // The sim keeps Island-local px everywhere; only flights between Islands (Spirit Bridges)
        // need world px = Island offset + local px (x right, y down, like local px). Defaults come
        // from config (RegionDef.islandCol/islandRow × cell); the scene overrides them at boot.

        readonly Dictionary<string, (double x, double y)> _islandOffsetPx = new Dictionary<string, (double x, double y)>();

        /// <summary>World px of the Island's top-left corner (scene override, else the config default).</summary>
        public (double x, double y) IslandOffsetPx(string key)
        {
            if (key != null && _islandOffsetPx.TryGetValue(key, out var o)) return o;
            var r = Cfg.Region(key);
            return r == null ? (0, 0) : ((double)r.islandCol * _ctx.Cell, (double)r.islandRow * _ctx.Cell);
        }

        /// <summary>Override one Island's world offset (px). Unknown keys are ignored.</summary>
        public void SetIslandOffsetPx(string key, double x, double y)
        {
            if (Cfg.Region(key) == null || double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y)) return;
            _islandOffsetPx[key] = (x, y);
        }

        /// <summary>Drop every override (back to the config defaults).</summary>
        public void ResetIslandOffsets() => _islandOffsetPx.Clear();

        /// <summary>Island-local px → world px.</summary>
        public (double x, double y) ToWorldPx(string island, double x, double y)
        {
            var (ox, oy) = IslandOffsetPx(island);
            return (ox + x, oy + y);
        }

        /// <summary>Building footprint centre in world px.</summary>
        public (double x, double y) BuildingWorldCenterPx(string island, Building b)
        {
            var (x, y) = BuildingCenterPx(b);
            return ToWorldPx(island, x, y);
        }

        // ---- zones (§3.2) ----
        public List<ZoneRect> ZoneRects(string zone) => Cfg.ZoneRects(zone);

        /// <summary>`cellInZone` engine.js:1166.</summary>
        public bool CellInZone(string zone, int r, int c)
        {
            foreach (var z in Cfg.ZoneRects(zone)) if (z.Contains(r, c)) return true;
            return false;
        }

        /// <summary>`inNoBuild(area,r,c)` engine.js:369.</summary>
        public bool InNoBuild(string areaKey, int r, int c)
        {
            var reg = Cfg.Region(areaKey);
            if (reg == null) return false;
            foreach (var nb in reg.noBuild) if (CellInZone(nb, r, c)) return true;
            return false;
        }

        // ---- footprints (§3.4) ----
        public (int w, int h) BuildingSize(string type) => Cfg.BuildingSize(type);

        /// <summary>`buildingAt(area,row,col)` engine.js:1804 — first building whose footprint holds the cell.</summary>
        public Building BuildingAt(string areaKey, int row, int col)
        {
            foreach (var b in S.Area(areaKey).buildings)
            {
                var (w, h) = BuildingSize(b.type);
                if (row >= b.row && row < b.row + h && col >= b.col && col < b.col + w) return b;
            }
            return null;
        }

        public (double x, double y) BuildingCenterPx(Building b)
        {
            var (w, h) = BuildingSize(b.type);
            return ((b.col + w / 2.0) * _ctx.Cell, (b.row + h / 2.0) * _ctx.Cell);
        }

        public (double x, double y) NodeCenterPx(Node n) =>
            ((n.col + n.size / 2.0) * _ctx.Cell, (n.row + n.size / 2.0) * _ctx.Cell);

        /// <summary>`gateExists()` engine.js:222 — any gate (ghost or built) anywhere.</summary>
        public bool GateExists()
        {
            foreach (var a in S.areas)
                foreach (var b in a.buildings)
                    if (Cfg.Building(b.type)?.gate == true) return true;
            return false;
        }

        /// <summary>`rackCells(area)` engine.js:1248 — racks of ALL burners incl. ghosts.</summary>
        public HashSet<int> RackCells(string areaKey)
        {
            var set = new HashSet<int>();
            foreach (var b in S.Area(areaKey).buildings)
            {
                var def = Cfg.Building(b.type);
                if (def == null || !def.fuel) continue;
                for (int r = b.row; r <= b.row + 1; r++)
                    for (int c = b.col - 3; c <= b.col - 1; c++) set.Add(CellKey(r, c));
            }
            return set;
        }

        // ---- placement (§3.5) ----

        /// <summary>`canPlaceBuilding` engine.js:1170.</summary>
        public bool CanPlaceBuilding(string areaKey, int row, int col, string type) => PlaceReason(areaKey, row, col, type) == null;

        /// <summary>`placeReason` engine.js:1207 — null when allowed, else the player-facing reason.</summary>
        public string PlaceReason(string areaKey, int row, int col, string type)
        {
            var def = Cfg.Building(type);
            int bw = def != null ? def.sizeW : Cfg.grid.buildingW, bh = def != null ? def.sizeH : Cfg.grid.buildingH;
            int N = _ctx.N;
            bool waterOnly = def != null && def.waterOnly, anyZone = def != null && def.anyZone;
            if (row < 0 || col < 0 || row + bh > N || col + bw > N) return "Off the edge";
            if (waterOnly && areaKey != "fishing") return "Water only";
            if (def != null && def.gate && GateExists()) return "Only one Ascension Gate";
            var area = S.Area(areaKey);
            var occ = _ctx.Occupancy;
            bool wild = false, water = false, blocked = false;
            for (int r = row; r < row + bh; r++)
                for (int c = col; c < col + bw; c++)
                {
                    if (waterOnly ? !CellInZone("centre", r, c) : (!anyZone && InNoBuild(areaKey, r, c)))
                    {
                        if (waterOnly) water = true; else wild = true;
                    }
                    if (occ.IsOccupied(area, r, c)) blocked = true;
                }
            if (water) return "Water only";
            if (wild) return WildLandReason(areaKey);
            if (blocked) return "Blocked";
            var racks = RackCells(areaKey);
            if (def != null && def.fuel)
            {
                if (col - 3 < 0) return "Fuel rack blocked";
                for (int r = row; r <= row + 1; r++)
                    for (int c = col - 3; c <= col - 1; c++)
                    {
                        if (occ.IsOccupied(area, r, c) || racks.Contains(CellKey(r, c))) return "Fuel rack blocked";
                        if (!anyZone && InNoBuild(areaKey, r, c)) return "Fuel rack blocked";
                    }
            }
            for (int r = row; r < row + bh; r++)
                for (int c = col; c < col + bw; c++)
                    if (racks.Contains(CellKey(r, c))) return "Blocked";
            return null;
        }

        /// <summary>Collision-free key for a (possibly out-of-grid) cell, like the JS "r,c" strings.</summary>
        public static int CellKey(int r, int c) => (r + 1024) * 4096 + (c + 1024);

        /// <summary>`wildLandReason` engine.js:1242.</summary>
        public static string WildLandReason(string areaKey) =>
            areaKey == "center" ? "Build around the Altar clearing" : "Build on the rim, outside the field";
    }
}
