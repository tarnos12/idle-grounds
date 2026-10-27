using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Hides the ground decor (the Island dressing's <c>Flowers</c> and <c>Scatter</c> tilemaps, built by
    /// IslandDressingBuilder) under building footprints, so flowers / pebbles never show through the open
    /// parts of building art, and puts them back when the building goes. Event-driven: <see cref="BuildingViewSync"/>
    /// calls <see cref="Cover"/> when it binds a view (placed ghost, completed building, every building at load)
    /// and <see cref="Uncover"/> when it releases one (demolished). Per-cell reference counts, so a building placed
    /// on the cells of one removed in the same frame keeps them hidden whatever the call order. Paths, water,
    /// resource nodes and every other dressing layer are untouched. No per-frame work.
    /// </summary>
    public sealed class DecorCover
    {
        /// <summary>Dressing root under each Island (IslandDressingBuilder.RootName) and the tilemaps it hides.</summary>
        public const string DressingRoot = "Dressing";
        public static readonly string[] MapNames = { "Flowers", "Scatter" };

        struct Saved { public TileBase tile; public Matrix4x4 m; public Color c; public TileFlags flags; }

        sealed class AreaMaps
        {
            public Island island;
            public Tilemap[] maps;
            public readonly Dictionary<Vector2Int, int> count = new Dictionary<Vector2Int, int>();    // island cell (col, row) -> covering buildings
            public readonly Dictionary<(int map, Vector3Int cell), Saved> saved = new Dictionary<(int, Vector3Int), Saved>();
        }

        readonly System.Func<string, Island> islandOf;
        readonly Dictionary<string, AreaMaps> byArea = new Dictionary<string, AreaMaps>();
        readonly Dictionary<long, (string area, RectInt rect)> covered = new Dictionary<long, (string, RectInt)>();

        public DecorCover(System.Func<string, Island> islandOf) { this.islandOf = islandOf; }

        /// <summary>Decor tiles currently hidden (all areas).</summary>
        public int HiddenTiles { get { int n = 0; foreach (var a in byArea.Values) n += a.saved.Count; return n; } }

        /// <summary>True while island cell (row, col) of <paramref name="area"/> is under a covering footprint.</summary>
        public bool IsCovered(string area, int row, int col) => byArea.TryGetValue(area, out var a) && a.count.ContainsKey(new Vector2Int(col, row));

        AreaMaps Maps(string area)
        {
            if (byArea.TryGetValue(area, out var a)) return a;
            a = new AreaMaps { island = islandOf?.Invoke(area) };
            var root = a.island != null ? a.island.transform.Find(DressingRoot) : null;
            a.maps = new Tilemap[MapNames.Length];
            if (root != null)
                for (int i = 0; i < MapNames.Length; i++) { var t = root.Find(MapNames[i]); a.maps[i] = t != null ? t.GetComponent<Tilemap>() : null; }
            byArea[area] = a;
            return a;
        }

        /// <summary>Hide the decor under building <paramref name="key"/>'s footprint (island cells; col/row = top-left).
        /// Same key + same footprint again: no-op; a different footprint releases the old one first.</summary>
        public void Cover(long key, string area, int col, int row, int w, int h)
        {
            var rect = new RectInt(col, row, Mathf.Max(1, w), Mathf.Max(1, h));
            if (covered.TryGetValue(key, out var old))
            {
                if (old.area == area && old.rect.Equals(rect)) return;
                Uncover(key);
            }
            covered[key] = (area, rect);
            var a = Maps(area);
            for (int r = rect.yMin; r < rect.yMax; r++)
                for (int c = rect.xMin; c < rect.xMax; c++)
                {
                    var cell = new Vector2Int(c, r);
                    a.count.TryGetValue(cell, out int n);
                    a.count[cell] = n + 1;
                    if (n == 0) Hide(a, r, c);
                }
        }

        /// <summary>Put back the decor under building <paramref name="key"/>'s footprint (cells no other building still covers).</summary>
        public void Uncover(long key)
        {
            if (!covered.TryGetValue(key, out var e)) return;
            covered.Remove(key);
            if (!byArea.TryGetValue(e.area, out var a)) return;
            for (int r = e.rect.yMin; r < e.rect.yMax; r++)
                for (int c = e.rect.xMin; c < e.rect.xMax; c++)
                {
                    var cell = new Vector2Int(c, r);
                    if (!a.count.TryGetValue(cell, out int n)) continue;
                    if (n > 1) { a.count[cell] = n - 1; continue; }
                    a.count.Remove(cell);
                    Restore(a, r, c);
                }
        }

        /// <summary>Restore everything (e.g. before the sync is torn down).</summary>
        public void Clear()
        {
            var keys = new List<long>(covered.Keys);
            foreach (var k in keys) Uncover(k);
        }

        static void Hide(AreaMaps a, int row, int col)
        {
            if (a.island == null) return;
            var world = a.island.CellToWorld(row, col);
            for (int i = 0; i < a.maps.Length; i++)
            {
                var tm = a.maps[i];
                if (tm == null) continue;
                var p = tm.WorldToCell(world);
                var t = tm.GetTile(p);
                if (t == null) continue;
                a.saved[(i, p)] = new Saved { tile = t, m = tm.GetTransformMatrix(p), c = tm.GetColor(p), flags = tm.GetTileFlags(p) };
                tm.SetTile(p, null);
            }
        }

        static void Restore(AreaMaps a, int row, int col)
        {
            if (a.island == null) return;
            var world = a.island.CellToWorld(row, col);
            for (int i = 0; i < a.maps.Length; i++)
            {
                var tm = a.maps[i];
                if (tm == null) continue;
                var p = tm.WorldToCell(world);
                if (!a.saved.TryGetValue((i, p), out var s)) continue;
                a.saved.Remove((i, p));
                tm.SetTile(p, s.tile);
                tm.SetTileFlags(p, TileFlags.None);
                tm.SetTransformMatrix(p, s.m);
                tm.SetColor(p, s.c);
                tm.SetTileFlags(p, s.flags);
            }
        }
    }
}
