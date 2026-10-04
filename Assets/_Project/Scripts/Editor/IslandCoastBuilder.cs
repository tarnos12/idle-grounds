using System.Collections.Generic;
using System.IO;
using IdleGrounds.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Irregular Island silhouettes (ADR 0003 / ART-SPEC 3.0), VISUAL ONLY. The playable area stays the square
    /// 93x93 sim grid; each Island's "Coast" tilemap (47-blob Rule Tile) holds the painted landmass: the square
    /// itself plus a hand-style coast 3-12 cells beyond it (bays, peninsulas, an occasional detached islet).
    ///
    /// AUTHORED DATA: the coast is generated ONCE (<see cref="GenerateMissing"/>, also run by the world
    /// builders) and from then on belongs to the scene - it is never overwritten unless you explicitly run
    /// "Idle Grounds/World/Regenerate Island Coasts (overwrites)". Paint over it by hand in the Tile Palette
    /// whenever you like; "Rebuild Island Rim + Underside + Veil" then re-derives everything that hangs off it:
    ///   - CliffRim tilemap: a cliff cell under every south-facing edge (diagonals, corners, end caps),
    ///   - Underside decorations: a few varied hanging rocks / spikes / roots + mist along the real bottom outline,
    ///   - Veil: tiled fog masked (SpriteMask) to the landmass + rim + underside while the Island is locked,
    ///   - Boundary: a low stone wall marking the playable square.
    /// </summary>
    public static class IslandCoastBuilder
    {
        const int Cells = Island.Cells;
        const int Pad = 24;
        const int Size = Cells + 2 * Pad;
        const float MinMargin = 3f, MaxMargin = 12f, MaxIsletReach = 17.5f;
        const string Ground = "Ground";
        const int MaskPpu = 4;

        // ------------------------------------------------------------------ menus

        [MenuItem("Idle Grounds/World/Generate Missing Island Coasts")]
        public static void GenerateMissingMenu() { GenerateMissing(); }

        [MenuItem("Idle Grounds/World/Regenerate Island Coasts (overwrites)")]
        public static void RegenerateAllMenu() { Regenerate(null); }

        [MenuItem("Idle Grounds/World/Regenerate Selected Island Coast (overwrites)")]
        public static void RegenerateSelectedMenu()
        {
            var go = Selection.activeGameObject;
            var isl = go != null ? go.GetComponentInParent<Island>() : null;
            if (isl == null) { Debug.LogWarning("[IdleGrounds] Select an Island_<key> object (or a child) first."); return; }
            Regenerate(isl);
        }

        [MenuItem("Idle Grounds/World/Rebuild Island Rim + Underside + Veil (from Coast)")]
        public static void RebuildDerivedMenu()
        {
            foreach (var isl in Islands()) RebuildDerived(isl);
            MarkDirty();
            Debug.Log("[IdleGrounds] Island rim / underside / veil / boundary rebuilt from the Coast tilemaps.");
        }

        // ------------------------------------------------------------------ entry points

        static List<Island> Islands()
        {
            var list = new List<Island>();
            foreach (var i in Object.FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!string.IsNullOrEmpty(i.islandKey) && i.name == "Island_" + i.islandKey) list.Add(i);
            list.Sort((a, b) => string.CompareOrdinal(a.islandKey, b.islandKey));
            return list;
        }

        static void MarkDirty()
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        /// <summary>Paints a coast ONLY on Islands whose Coast tilemap is empty, then rebuilds everything derived. Safe to call any time.</summary>
        public static void GenerateMissing()
        {
            IslandArtBuilder.EnsureArt();
            int painted = 0;
            foreach (var isl in Islands())
            {
                var tm = EnsureCoastTilemap(isl);
                if (!HasTiles(tm)) { PaintCoast(isl, isl.coastSeed); painted++; }
                RebuildDerived(isl);
            }
            MarkDirty();
            Debug.Log("[IdleGrounds] Island coasts: " + painted + " newly painted, the rest kept as authored.");
        }

        /// <summary>Re-rolls the coast of one Island (or all when null) - OVERWRITES the painted Coast tilemap.</summary>
        public static void Regenerate(Island only)
        {
            IslandArtBuilder.EnsureArt();
            var rng = new System.Random(System.Environment.TickCount);
            int n = 0;
            foreach (var isl in Islands())
            {
                if (only != null && isl != only) continue;
                EnsureCoastTilemap(isl);
                PaintCoast(isl, rng.Next(1, int.MaxValue));
                RebuildDerived(isl);
                n++;
            }
            MarkDirty();
            Debug.Log("[IdleGrounds] Island coasts re-rolled (overwritten): " + n);
        }

        static T Get<T>(GameObject go) where T : Component { var c = go.GetComponent<T>(); return c != null ? c : go.AddComponent<T>(); }

        static bool HasTiles(Tilemap tm)
        {
            if (tm == null) return false;
            tm.CompressBounds();
            var b = tm.cellBounds;
            foreach (var p in b.allPositionsWithin) if (tm.GetTile(p) != null) return true;
            return false;
        }

        /// <summary>The Island's Coast tilemap (created under the Island if missing): same Grid, drawn under the playable Ground.</summary>
        public static Tilemap EnsureCoastTilemap(Island isl)
        {
            if (isl.coast != null) return isl.coast;
            var t = isl.transform.Find("Coast");
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = new GameObject("Coast");
                go.transform.SetParent(isl.transform, false);
                go.transform.SetSiblingIndex(0);
            }
            var tm = Get<Tilemap>(go);
            var tr = Get<TilemapRenderer>(go);
            tr.sortingLayerName = Ground;
            tr.sortingOrder = -2;
            isl.coast = tm;
            EditorUtility.SetDirty(isl);
            return tm;
        }

        // ------------------------------------------------------------------ noise

        static uint HashU(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * -1640531535);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        static float H01(int x, int y, int seed) => (HashU(x, y, seed) & 0xFFFFFF) / (float)0x1000000;

        static float Smooth(float f) => f * f * (3f - 2f * f);

        /// <summary>Periodic 1-D value noise: u in [0, period) wraps.</summary>
        static float Noise1(float u, int period, int seed)
        {
            int i0 = Mathf.FloorToInt(u);
            float f = Smooth(u - i0);
            int a = ((i0 % period) + period) % period, b = (a + 1) % period;
            return Mathf.Lerp(H01(a, 0, seed), H01(b, 0, seed), f);
        }

        static float Noise2(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = Smooth(x - x0), fy = Smooth(y - y0);
            float a = H01(x0, y0, seed), b = H01(x0 + 1, y0, seed), c = H01(x0, y0 + 1, seed), d = H01(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static int KeySeed(string key)
        {
            unchecked
            {
                int h = 23;
                foreach (char ch in key) h = h * 31 + ch;
                return h == 0 ? 1 : h;
            }
        }

        // ------------------------------------------------------------------ coast generation

        /// <summary>Cell (col, row) -> tilemap cell (col, -row-1).</summary>
        static Vector3Int Cell(int col, int row) => new Vector3Int(col, -row - 1, 0);

        /// <summary>The procedural outline: land[col + Pad, row + Pad] over the padded grid. Always contains the square.</summary>
        static bool[,] Outline(int seed)
        {
            var rng = new System.Random(seed);
            const float perim = 4f * Cells;
            // peninsulas and bays along the perimeter
            var bumps = new List<(float t, float sigma, float h)>();
            int peninsulas = 3 + rng.Next(3), bays = 3 + rng.Next(2);
            for (int i = 0; i < peninsulas; i++) bumps.Add(((float)rng.NextDouble() * perim, 3.5f + (float)rng.NextDouble() * 4f, 3.5f + (float)rng.NextDouble() * 3f));
            for (int c = 0; c < 4; c++) if (rng.NextDouble() < 0.75) bumps.Add((c * Cells + (c % 2 == 0 ? 0f : 0f), 12f + (float)rng.NextDouble() * 6f, -(3f + (float)rng.NextDouble() * 3f)));   // pinched corners
            for (int i = 0; i < bays; i++) bumps.Add(((float)rng.NextDouble() * perim, 5f + (float)rng.NextDouble() * 6f, -(3.5f + (float)rng.NextDouble() * 3f)));

            float Margin(float t)
            {
                float n = 0.55f * Noise1(t / perim * 12f, 12, seed) + 0.3f * Noise1(t / perim * 29f, 29, seed + 1) + 0.15f * Noise1(t / perim * 60f, 60, seed + 2);
                float v = Mathf.Clamp01((n - 0.5f) * 3.8f + 0.5f);
                float m = 3f + 8f * v;
                foreach (var (bt, s, h) in bumps)
                {
                    float d = Mathf.Abs(t - bt); d = Mathf.Min(d, perim - d);
                    m += h * Mathf.Exp(-d * d / (2f * s * s));
                }
                return 3f + 9f * Smooth(Mathf.Clamp01((m - 2f) / 16f));
            }

            var land = new bool[Size, Size];
            var dist = new float[Size, Size];
            for (int ix = 0; ix < Size; ix++)
                for (int iy = 0; iy < Size; iy++)
                {
                    int col = ix - Pad, row = iy - Pad;
                    int cc = Mathf.Clamp(col, 0, Cells - 1), cr = Mathf.Clamp(row, 0, Cells - 1);
                    float dx = col - cc, dy = row - cr;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    dist[ix, iy] = d;
                    if (d <= 0f) { land[ix, iy] = true; continue; }
                    float t = cr == 0 ? cc : cc == Cells - 1 ? Cells + cr : cr == Cells - 1 ? 2f * Cells + (Cells - 1 - cc) : 3f * Cells + (Cells - 1 - cr);
                    float th = Margin(t) + (Noise2(col * 0.17f, row * 0.17f, seed + 9) - 0.5f) * 3f + (Noise2(col * 0.06f, row * 0.06f, seed + 19) - 0.5f) * 4f;
                    land[ix, iy] = d <= Mathf.Clamp(th, MinMargin, MaxMargin);
                }

            // cellular smoothing of the free margin (rounds jaggies, removes 1-cell spikes)
            for (int pass = 0; pass < 1; pass++)
            {
                var next = (bool[,])land.Clone();
                for (int ix = 1; ix < Size - 1; ix++)
                    for (int iy = 1; iy < Size - 1; iy++)
                    {
                        if (dist[ix, iy] <= MinMargin) { next[ix, iy] = true; continue; }
                        int cnt = 0;
                        for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++) if ((a != 0 || b != 0) && land[ix + a, iy + b]) cnt++;
                        if (cnt >= 5) next[ix, iy] = true; else if (cnt <= 3) next[ix, iy] = false;
                    }
                land = next;
            }

            KeepConnected(land);
            FillHoles(land);

            // satellite islets: small detached blobs a few cells off the coast
            int islets = 1 + (rng.NextDouble() < 0.55 ? 1 : 0);
            for (int k = 0, tries = 0; k < islets && tries < 60; tries++)
                if (TryIslet(land, rng, seed + 100 + k)) k++;
            return land;
        }

        static void KeepConnected(bool[,] land)
        {
            var seen = new bool[Size, Size];
            var q = new Queue<Vector2Int>();
            var start = new Vector2Int(Pad, Pad);
            q.Enqueue(start); seen[start.x, start.y] = true;
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var n = p + d;
                    if (n.x < 0 || n.y < 0 || n.x >= Size || n.y >= Size || seen[n.x, n.y] || !land[n.x, n.y]) continue;
                    seen[n.x, n.y] = true; q.Enqueue(n);
                }
            }
            for (int x = 0; x < Size; x++) for (int y = 0; y < Size; y++) if (land[x, y] && !seen[x, y]) land[x, y] = false;
        }

        static void FillHoles(bool[,] land)
        {
            var sea = new bool[Size, Size];
            var q = new Queue<Vector2Int>();
            void Push(int x, int y) { if (!land[x, y] && !sea[x, y]) { sea[x, y] = true; q.Enqueue(new Vector2Int(x, y)); } }
            for (int i = 0; i < Size; i++) { Push(i, 0); Push(i, Size - 1); Push(0, i); Push(Size - 1, i); }
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                if (p.x > 0) Push(p.x - 1, p.y);
                if (p.x < Size - 1) Push(p.x + 1, p.y);
                if (p.y > 0) Push(p.x, p.y - 1);
                if (p.y < Size - 1) Push(p.x, p.y + 1);
            }
            for (int x = 0; x < Size; x++) for (int y = 0; y < Size; y++) if (!land[x, y] && !sea[x, y]) land[x, y] = true;
        }

        static bool TryIslet(bool[,] land, System.Random rng, int seed)
        {
            double th = rng.NextDouble() * System.Math.PI * 2.0;
            float dx = (float)System.Math.Cos(th), dy = (float)System.Math.Sin(th);
            float cx0 = (Cells - 1) * 0.5f, cy0 = (Cells - 1) * 0.5f;
            float e0 = 0f;
            for (float r = 0f; r < 70f; r += 0.5f)
            {
                int ix = Mathf.RoundToInt(cx0 + dx * r) + Pad, iy = Mathf.RoundToInt(cy0 + dy * r) + Pad;
                if (ix < 0 || iy < 0 || ix >= Size || iy >= Size) return false;
                if (land[ix, iy]) e0 = r;
            }
            float ri = 2.2f + (float)rng.NextDouble() * 1.4f;
            float cx = cx0 + dx * (e0 + 3.2f + ri), cy = cy0 + dy * (e0 + 3.2f + ri);
            var cells = new List<Vector2Int>();
            for (int ix = 0; ix < Size; ix++)
                for (int iy = 0; iy < Size; iy++)
                {
                    float px = ix - Pad - cx, py = iy - Pad - cy;
                    if (px * px + py * py > (ri + 1.5f) * (ri + 1.5f)) continue;
                    float eff = ri * (0.72f + 0.55f * Noise2((ix - Pad) * 0.45f, (iy - Pad) * 0.45f, seed));
                    if (Mathf.Sqrt(px * px + py * py) <= eff) cells.Add(new Vector2Int(ix, iy));
                }
            if (cells.Count < 6) return false;
            foreach (var c in cells)
            {
                if (c.x < 3 || c.y < 3 || c.x >= Size - 3 || c.y >= Size - 3) return false;
                int col = c.x - Pad, row = c.y - Pad;
                float ddx = col - Mathf.Clamp(col, 0, Cells - 1), ddy = row - Mathf.Clamp(row, 0, Cells - 1);
                if (Mathf.Sqrt(ddx * ddx + ddy * ddy) > MaxIsletReach) return false;
                for (int a = -2; a <= 2; a++) for (int b = -2; b <= 2; b++) if (land[c.x + a, c.y + b]) return false;
            }
            foreach (var c in cells) land[c.x, c.y] = true;
            return true;
        }

        /// <summary>Paints the procedural coast into the Island's Coast tilemap (clears it first).</summary>
        public static void PaintCoast(Island isl, int seed)
        {
            var tm = EnsureCoastTilemap(isl);
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(IslandArtBuilder.CoastTilePath(isl.islandKey));
            if (tile == null) { Debug.LogError("[IdleGrounds] Coast Rule Tile missing for " + isl.islandKey + " - run Idle Grounds/Art/Regenerate Placeholder Island Art."); return; }
            if (seed == 0) seed = KeySeed(isl.islandKey);
            var land = Outline(seed);
            tm.ClearAllTiles();
            var bounds = new BoundsInt(-Pad, -Cells - Pad, 0, Size, Size, 1);
            var tiles = new TileBase[Size * Size];
            for (int ix = 0; ix < Size; ix++)
                for (int iy = 0; iy < Size; iy++)
                    if (land[ix, iy])
                    {
                        var p = Cell(ix - Pad, iy - Pad);
                        tiles[(p.x - bounds.xMin) + (p.y - bounds.yMin) * Size] = tile;
                    }
            tm.SetTilesBlock(bounds, tiles);
            tm.CompressBounds();
            isl.coastSeed = seed;
            EditorUtility.SetDirty(tm);
            EditorUtility.SetDirty(isl);
        }

        // ------------------------------------------------------------------ land read-back (hand-edits included)

        sealed class Land
        {
            public int x0, y0, x1, y1;      // inclusive tilemap cell bounds
            readonly bool[,] a;
            public Land(int x0, int y0, int x1, int y1) { this.x0 = x0; this.y0 = y0; this.x1 = x1; this.y1 = y1; a = new bool[x1 - x0 + 1, y1 - y0 + 1]; }
            public bool this[int x, int y]
            {
                get => x >= x0 && x <= x1 && y >= y0 && y <= y1 && a[x - x0, y - y0];
                set { if (x >= x0 && x <= x1 && y >= y0 && y <= y1) a[x - x0, y - y0] = value; }
            }
        }

        static Land ReadLand(Island isl)
        {
            var tm = isl.coast;
            int x0 = 0, y0 = -Cells, x1 = Cells - 1, y1 = -1;
            if (tm != null)
            {
                tm.CompressBounds();
                var b = tm.cellBounds;
                if (b.size.x > 0 && b.size.y > 0)
                {
                    x0 = Mathf.Min(x0, b.xMin); y0 = Mathf.Min(y0, b.yMin);
                    x1 = Mathf.Max(x1, b.xMax - 1); y1 = Mathf.Max(y1, b.yMax - 1);
                }
            }
            var land = new Land(x0, y0, x1, y1);
            if (tm != null)
                foreach (var p in tm.cellBounds.allPositionsWithin) if (tm.GetTile(p) != null) land[p.x, p.y] = true;
            for (int x = 0; x < Cells; x++) for (int y = -Cells; y <= -1; y++) land[x, y] = true;     // the play area is always ground
            return land;
        }

        // ------------------------------------------------------------------ derived: cliff rim, underside, veil, boundary

        public static void RebuildDerived(Island isl)
        {
            EnsureCoastTilemap(isl);
            var land = ReadLand(isl);
            var cliffCells = BuildCliff(isl, land);
            BuildUnderside(isl, land, out var xc, out var halfW);
            BuildVeil(isl, land, cliffCells, xc, halfW);
            BuildBoundary(isl);
            EditorUtility.SetDirty(isl.gameObject);
        }

        static HashSet<Vector2Int> BuildCliff(Island isl, Land land)
        {
            var go = isl.transform.Find("CliffRim");
            Tilemap cliff;
            if (go == null)
            {
                var g = new GameObject("CliffRim");
                g.transform.SetParent(isl.transform, false);
                cliff = g.AddComponent<Tilemap>();
                var cr = g.AddComponent<TilemapRenderer>();
                cr.sortingLayerName = Ground;
                cr.sortingOrder = -1;
            }
            else cliff = go.GetComponent<Tilemap>();
            cliff.ClearAllTiles();
            var set = new HashSet<Vector2Int>();
            for (int x = land.x0; x <= land.x1; x++)
                for (int y = land.y0; y <= land.y1; y++)
                    if (land[x, y] && !land[x, y - 1]) set.Add(new Vector2Int(x, y - 1));
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(IslandArtBuilder.CliffTilePath(isl.islandKey));
            if (tile != null)
            {
                var pos = new List<Vector3Int>(set.Count);
                foreach (var c in set) pos.Add(new Vector3Int(c.x, c.y, 0));
                var arr = new TileBase[pos.Count];
                for (int i = 0; i < arr.Length; i++) arr[i] = tile;
                cliff.SetTiles(pos.ToArray(), arr);
            }
            EditorUtility.SetDirty(cliff);
            return set;
        }

        static void BuildUnderside(Island isl, Land land, out float xCentre, out float halfWidth)
        {
            int w = land.x1 - land.x0 + 1;
            var bot = new float[w];
            int first = -1, last = -1;
            double sumX = 0, sumW = 0;
            for (int i = 0; i < w; i++)
            {
                int x = land.x0 + i;
                bot[i] = float.NaN;
                int cnt = 0;
                for (int y = land.y0; y <= land.y1; y++)
                    if (land[x, y]) { if (float.IsNaN(bot[i])) bot[i] = y - 1; cnt++; }
                if (!float.IsNaN(bot[i])) { if (first < 0) first = i; last = i; sumX += (x + 0.5) * cnt; sumW += cnt; }
            }
            xCentre = (float)(sumX / System.Math.Max(1.0, sumW));
            float xMin = land.x0 + first, xMax = land.x0 + last + 1;
            halfWidth = (xMax - xMin) * 0.5f;
            xCentre = (xMin + xMax) * 0.5f;
            // fill gaps with the nearest valid column, then smooth (box 7)
            var filled = (float[])bot.Clone();
            for (int i = 0; i < w; i++)
            {
                if (!float.IsNaN(filled[i])) continue;
                int best = -1;
                for (int d = 1; d < w && best < 0; d++)
                {
                    if (i - d >= 0 && !float.IsNaN(bot[i - d])) best = i - d;
                    else if (i + d < w && !float.IsNaN(bot[i + d])) best = i + d;
                }
                if (best >= 0) filled[i] = bot[best];
            }
            var att = new float[w];
            for (int i = 0; i < w; i++)
            {
                float s = 0; int n = 0;
                for (int d = -3; d <= 3; d++) { int j = Mathf.Clamp(i + d, 0, w - 1); if (!float.IsNaN(filled[j])) { s += filled[j]; n++; } }
                att[i] = n > 0 ? s / n : 0f;
            }
            float Attach(float x) => att[Mathf.Clamp(Mathf.FloorToInt(x) - land.x0, 0, w - 1)];

            var t = isl.transform.Find("Underside");
            Transform root;
            if (t == null)
            {
                root = new GameObject("Underside").transform;
                root.SetParent(isl.transform, false);
            }
            else root = t;
            for (int i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);
            root.localPosition = Vector3.zero;

            var rock = IslandArtBuilder.Single(IslandArtBuilder.UndersideRock);
            var spike = IslandArtBuilder.Single(IslandArtBuilder.UndersideStalactite);
            var roots = IslandArtBuilder.Single(IslandArtBuilder.UndersideRoots);
            var mist = IslandArtBuilder.Single(IslandArtBuilder.BaseMist);
            var tint = WorldBuilder.UndersideTint(isl.islandKey);
            var rng = new System.Random(KeySeed(isl.islandKey) ^ (isl.coastSeed * 7919) ^ 0x2545F49);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            const string Sky = WorldBuilder.SkyLayer;
            float span = xMax - xMin;

            // one big mass slightly off-centre, then smaller overlapping pieces of different widths
            float mainX = xCentre + R(-8f, 8f);
            float mainScale = Mathf.Clamp(0.5f * span / 8f, 3.6f, 6f);
            float rockH = rock != null ? rock.bounds.size.y : 6f;
            float mainTop = Attach(mainX) + 0.45f;
            float tipY = mainTop - rockH * mainScale * 0.82f;
            WorldBuilder.Deco(root, "Rock", rock, new Vector3(mainX, mainTop, 0f), mainScale, tint, 40, Sky);

            int rocks = 3 + (span > 115f ? 1 : 0);
            for (int i = 0; i < rocks; i++)
            {
                float x = Mathf.Lerp(xMin + 10f, xMax - 10f, (i + 0.5f) / rocks + R(-0.1f, 0.1f));
                var sr = WorldBuilder.Deco(root, "RockPiece" + i, rock, new Vector3(x, Attach(x) + 0.45f + R(0f, 1f), 0f), R(1.9f, 3.1f), Color.Lerp(tint, Color.black, R(0f, 0.15f)), 39, Sky);
                sr.flipX = rng.NextDouble() < 0.5;
            }
            int spikes = 4 + rng.Next(3);
            for (int i = 0; i < spikes; i++)
            {
                float x = R(xMin + 4f, xMax - 4f);
                var sr = WorldBuilder.Deco(root, "Spike" + i, spike, new Vector3(x, Attach(x) + 0.3f, 0f), R(0.9f, 2.4f), Color.Lerp(tint, Color.black, R(0.05f, 0.25f)), 38, Sky);
                sr.flipX = rng.NextDouble() < 0.5;
            }
            int rootN = 1 + rng.Next(2);
            for (int i = 0; i < rootN; i++)
            {
                float x = R(xCentre - span * 0.3f, xCentre + span * 0.3f);
                WorldBuilder.Deco(root, "Roots" + i, roots, new Vector3(x, Attach(x) + 0.2f, 0f), R(1.5f, 2.4f), Color.white, 41, Sky);
            }
            WorldBuilder.Deco(root, "MistA", mist, new Vector3(mainX - 8f, tipY, 0f), 9f, new Color(1, 1, 1, 0.5f), 45, Sky);
            WorldBuilder.Deco(root, "MistB", mist, new Vector3(mainX + 10f, tipY - 2f, 0f), 7f, new Color(1, 1, 1, 0.4f), 45, Sky);
            WorldBuilder.Deco(root, "MistC", mist, new Vector3(xMin + 3f, Attach(xMin + 3f) - 4f, 0f), 6f, new Color(1, 1, 1, 0.3f), 44, Sky);
            WorldBuilder.Deco(root, "MistD", mist, new Vector3(xMax - 3f, Attach(xMax - 3f) - 5f, 0f), 6f, new Color(1, 1, 1, 0.3f), 44, Sky);
            for (int i = 0; i < 2; i++)
            {
                float x = R(xMin + 12f, xMax - 12f);
                WorldBuilder.Deco(root, "MistE" + i, mist, new Vector3(x, Attach(x) - R(6f, 12f), 0f), R(4f, 6f), new Color(1, 1, 1, 0.25f), 44, Sky);
            }
            EditorUtility.SetDirty(root.gameObject);
        }

        static void BuildVeil(Island isl, Land land, HashSet<Vector2Int> cliff, float xc, float halfW)
        {
            // cell mask of everything the fog must cover: ground + cliff rim + the hanging underside
            int gx0 = land.x0 - 3, gx1 = land.x1 + 3, gy0 = land.y0 - 16, gy1 = land.y1 + 3;
            int gw = gx1 - gx0 + 1, gh = gy1 - gy0 + 1;
            var m = new bool[gw, gh];
            var minY = new Dictionary<int, int>();
            for (int x = land.x0; x <= land.x1; x++)
                for (int y = land.y0; y <= land.y1; y++)
                    if (land[x, y])
                    {
                        m[x - gx0, y - gy0] = true;
                        if (!minY.TryGetValue(x, out var cur) || y < cur) minY[x] = y;
                    }
            foreach (var c in cliff) m[c.x - gx0, c.y - gy0] = true;
            foreach (var kv in minY)
            {
                float k = Mathf.Clamp01(1f - Mathf.Abs(kv.Key + 0.5f - xc) / (halfW + 1f));
                int depth = Mathf.Max(2, Mathf.RoundToInt(9f * Mathf.Pow(k, 0.6f)));
                for (int d = 1; d <= depth; d++) { int y = kv.Value - 1 - d; if (y >= gy0) m[kv.Key - gx0, y - gy0] = true; }
            }
            var dil = new bool[gw, gh];                      // 1-cell dilation so the fog hugs the coast softly
            for (int x = 0; x < gw; x++)
                for (int y = 0; y < gh; y++)
                {
                    if (!m[x, y]) continue;
                    for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++) { int nx = x + a, ny = y + b; if (nx >= 0 && ny >= 0 && nx < gw && ny < gh) dil[nx, ny] = true; }
                }

            int tw = gw * MaskPpu, th = gh * MaskPpu;
            var a0 = new float[tw, th];
            for (int x = 0; x < tw; x++) for (int y = 0; y < th; y++) a0[x, y] = dil[x / MaskPpu, y / MaskPpu] ? 1f : 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                var a1 = new float[tw, th];
                for (int x = 0; x < tw; x++)
                    for (int y = 0; y < th; y++)
                    {
                        float s = 0;
                        for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++) s += a0[Mathf.Clamp(x + a, 0, tw - 1), Mathf.Clamp(y + b, 0, th - 1)];
                        a1[x, y] = s / 9f;
                    }
                a0 = a1;
            }
            Directory.CreateDirectory(IslandArtBuilder.MaskDir);
            string path = $"{IslandArtBuilder.MaskDir}/veil_mask_{isl.islandKey}.png";
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            var px = new Color[tw * th];
            for (int x = 0; x < tw; x++) for (int y = 0; y < th; y++) px[y * tw + x] = new Color(1, 1, 1, a0[x, y]);
            tex.SetPixels(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = MaskPpu;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            st.spriteAlignment = (int)SpriteAlignment.Center;
            st.spritePivot = new Vector2(0.5f, 0.5f);
            imp.SetTextureSettings(st);
            imp.SaveAndReimport();
            var maskSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            // the Veil object (created by WorldBuilder) now spans the whole landmass bounding box
            var veil = isl.veil != null ? isl.veil : isl.transform.Find("Veil")?.gameObject;
            if (veil == null)
            {
                veil = new GameObject("Veil");
                veil.transform.SetParent(isl.transform, false);
                isl.veil = veil;
            }
            bool wasActive = veil.activeSelf;
            veil.transform.localScale = Vector3.one;
            veil.transform.localPosition = new Vector3(gx0 + gw * 0.5f, gy0 + gh * 0.5f, 0f);
            var sr = Get<SpriteRenderer>(veil);
            var fog = IslandArtBuilder.Frames(IslandArtBuilder.VeilMist);
            if (fog.Length > 0)
            {
                sr.sprite = fog[0];
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                sr.size = new Vector2(gw, gh);
                sr.color = new Color(0.72f, 0.7f, 0.8f, 0.85f);
            }
            sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            sr.sortingLayerName = "Overlay";
            sr.sortingOrder = 0;
            var maskT = veil.transform.Find("Mask");
            var maskGo = maskT != null ? maskT.gameObject : new GameObject("Mask");
            maskGo.transform.SetParent(veil.transform, false);
            maskGo.transform.localPosition = Vector3.zero;
            var sm = Get<SpriteMask>(maskGo);
            sm.sprite = maskSprite;
            sm.alphaCutoff = 0.5f;
            // 🔒 stays at the play area's centre
            var lockT = veil.transform.Find("Lock");
            if (lockT != null) lockT.position = isl.transform.TransformPoint(new Vector3(Cells * 0.5f, -Cells * 0.5f, 0f));
            veil.SetActive(wasActive);
            EditorUtility.SetDirty(veil);
        }

        static void BuildBoundary(Island isl)
        {
            var old = isl.transform.Find("Boundary");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var wall = IslandArtBuilder.Single(IslandArtBuilder.BoundaryWall);
            if (wall == null) return;
            var root = new GameObject("Boundary").transform;
            root.SetParent(isl.transform, false);
            void Edge(string name, Vector3 pos, float rot)
            {
                var sr = new GameObject(name).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(root, false);
                sr.transform.localPosition = pos;
                sr.transform.localRotation = Quaternion.Euler(0, 0, rot);
                sr.sprite = wall;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                sr.size = new Vector2(Cells + 0.3f, 1f);
                sr.color = new Color(1f, 1f, 1f, 0.55f);
                sr.sortingLayerName = Ground;
                sr.sortingOrder = 13;
            }
            Edge("North", new Vector3(Cells * 0.5f, 0f, 0f), 0f);
            Edge("South", new Vector3(Cells * 0.5f, -Cells, 0f), 0f);
            Edge("West", new Vector3(0f, -Cells * 0.5f, 0f), 90f);
            Edge("East", new Vector3(Cells, -Cells * 0.5f, 0f), 90f);
        }
    }
}
