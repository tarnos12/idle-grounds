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
    ///   - CliffRim tilemap: the 3-cell south wall under every south-facing edge, one piece per joint (BuildCliff: run /
    ///     free end at an outer corner / tucked end at a concave step),
    ///   - Underside (3/4 view): compact native-scale hanging rock under SOUTH-facing edges only + pixel cloud puffs,
    ///   - Shadow: a translucent black copy of the silhouette offset 6 px down-right onto the sky (contact shadow),
    ///   - Veil: tiled fog masked (SpriteMask) to the landmass + rim + underside while the Island is locked,
    ///   - Boundary: a low stone wall marking the playable square.
    /// Every painted coast is re-cut by <see cref="SmoothCoast"/> (blurred seed outline on a fine irregular 2-3 cell
    /// lattice: follows the curve, runs of 2+ cells, no 1-cell staircases; islets redrawn as rounded clumps). The Islands live in prefabs (<see cref="IslandPrefabs"/>); the menus
    /// unpack them, rebuild and save them back.
    /// </summary>
    public static class IslandCoastBuilder
    {
        const int Cells = Island.Cells;
        const int Pad = 48;
        const int Size = Cells + 2 * Pad;
        const float MinMargin = 8f, MaxMargin = 30f, MaxIsletReach = 40f;
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
            using (IslandPrefabs.Edit())
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
            using var edit = IslandPrefabs.Edit();
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
            using var edit = IslandPrefabs.Edit();
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
            if (only == null) Respace();
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
            // BOLD silhouette: a base margin of 8-13 cells plus 2-3 big lobes/peninsulas (up to 30 cells out) and deep bays
            var bumps = new List<(float t, float sigma, float h)>();
            int lobes = 2 + rng.Next(2);
            float lobePhase = (float)rng.NextDouble();
            for (int i = 0; i < lobes; i++)
            {
                float t0 = ((i + lobePhase) / lobes + ((float)rng.NextDouble() - 0.5f) * 0.12f) * perim;
                bumps.Add((t0, 7f + (float)rng.NextDouble() * 13f, 13f + (float)rng.NextDouble() * 12f));
            }
            int bays = 2 + rng.Next(2);
            for (int i = 0; i < bays; i++) bumps.Add(((float)rng.NextDouble() * perim, 7f + (float)rng.NextDouble() * 8f, -(6f + (float)rng.NextDouble() * 6f)));

            float Margin(float t)
            {
                float n = 0.55f * Noise1(t / perim * 7f, 7, seed) + 0.3f * Noise1(t / perim * 19f, 19, seed + 1) + 0.15f * Noise1(t / perim * 45f, 45, seed + 2);
                float m = 8f + 6f * Mathf.Clamp01((n - 0.5f) * 3.2f + 0.5f);
                foreach (var (bt, s, h) in bumps)
                {
                    float d = Mathf.Abs(t - bt); d = Mathf.Min(d, perim - d);
                    m += h * Mathf.Exp(-d * d / (2f * s * s));
                }
                return Mathf.Clamp(m, MinMargin, MaxMargin);
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
                    float th = Margin(t) + (Noise2(col * 0.12f, row * 0.12f, seed + 9) - 0.5f) * 4f + (Noise2(col * 0.045f, row * 0.045f, seed + 19) - 0.5f) * 8f;
                    land[ix, iy] = d <= Mathf.Clamp(th, MinMargin, MaxMargin);
                }

            // cellular smoothing of the free margin (rounds jaggies, removes 1-cell spikes)
            for (int pass = 0; pass < 2; pass++)
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
            int islets = 2 + rng.Next(3);
            for (int k = 0, tries = 0; k < islets && tries < 200; tries++)
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
            for (float r = 0f; r < 120f; r += 0.5f)
            {
                int ix = Mathf.RoundToInt(cx0 + dx * r) + Pad, iy = Mathf.RoundToInt(cy0 + dy * r) + Pad;
                if (ix < 0 || iy < 0 || ix >= Size || iy >= Size) break;
                if (land[ix, iy]) e0 = r;
            }
            float ri = 2f + (float)rng.NextDouble() * (float)rng.NextDouble() * 6f + (float)rng.NextDouble() * 1.5f;
            float cx = cx0 + dx * (e0 + 4.5f + ri), cy = cy0 + dy * (e0 + 4.5f + ri);
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
                if (c.x < 5 || c.y < 5 || c.x >= Size - 5 || c.y >= Size - 5) return false;
                int col = c.x - Pad, row = c.y - Pad;
                float ddx = col - Mathf.Clamp(col, 0, Cells - 1), ddy = row - Mathf.Clamp(row, 0, Cells - 1);
                if (Mathf.Sqrt(ddx * ddx + ddy * ddy) > MaxIsletReach) return false;
                for (int a = -4; a <= 4; a++) for (int b = -4; b <= 4; b++) if (land[c.x + a, c.y + b]) return false;
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
            SmoothCoast(isl);           // long straight runs + 3-4 cell steps (no 1-cell staircases)
            EditorUtility.SetDirty(tm);
            EditorUtility.SetDirty(isl);
        }

        // ------------------------------------------------------------------ coast smoothing (owner pick 2026-10-12)

        // Diagonal coasts painted cell by cell read as rows of 1-cell pillars once the 3-cell south cliff wall and the
        // W/E side faces hang off every step; a coarse 4-7 x 3-5 lattice (the first redo) read as boxy terraces. The
        // seed outline is therefore blurred (organic curve, no noise jaggies) and re-cut on a FINE irregular lattice
        // (column widths StepMinX-StepMaxX, row heights StepMinY-StepMaxY, seeded per Island): the outline follows the
        // true curve, but every straight run is at least 2 cells and a diagonal becomes a staircase whose step lengths
        // vary (2, 3, 4, 5, 6 ...) with the local slope - never a 1-cell stair, never a big rectangular block.
        const int StepMinX = 2, StepMaxX = 3, StepMinY = 2, StepMaxY = 3, SmoothBlur = 2;

        /// <summary>
        /// Re-paints every Island's coast from its recorded <see cref="Island.coastSeed"/> (the same outline as before)
        /// and re-cuts it with <see cref="SmoothCoast"/>, then rebuilds everything derived. Deterministic: running it
        /// again gives the same coasts. OVERWRITES hand-painted Coast edits (use Smooth Current Coasts for those).
        /// </summary>
        [MenuItem("Idle Grounds/World/Re-cut Island Coasts From Seed (smoothed, overwrites)")]
        public static void SmoothAllMenu()
        {
            int n = 0;
            using (IslandPrefabs.Edit())
                foreach (var isl in Islands()) { EnsureCoastTilemap(isl); PaintCoast(isl, isl.coastSeed); RebuildDerived(isl); n++; }
            MarkDirty();
            Debug.Log("[IdleGrounds] Island coasts re-cut from their seeds (" + n + ") and rim / underside / veil / dressing rebuilt.");
        }

        /// <summary>Smooths the coast as currently painted (keeps hand edits as the input). Not idempotent: run it once.</summary>
        [MenuItem("Idle Grounds/World/Smooth Current Island Coasts (overwrites)")]
        public static void SmoothCurrentMenu()
        {
            using (IslandPrefabs.Edit())
                foreach (var isl in Islands()) { SmoothCoast(isl); RebuildDerived(isl); }
            MarkDirty();
        }

        static int FloorTo(int v, int s) => Mathf.FloorToInt(v / (float)s) * s;

        /// <summary>Re-cuts the Island's painted Coast on the coarse lattice (deterministic, idempotent). The play square stays land.</summary>
        public static bool SmoothCoast(Island isl)
        {
            var tm = EnsureCoastTilemap(isl);
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(IslandArtBuilder.CoastTilePath(isl.islandKey));
            if (tile == null) { Debug.LogError("[IdleGrounds] Coast Rule Tile missing for " + isl.islandKey); return false; }
            var land = ReadLand(isl);
            int ox = FloorTo(land.x0 - 10, 2), oy = FloorTo(land.y0 - 10, 2);
            int w = land.x1 + 10 - ox + 1, h = land.y1 + 10 - oy + 1;
            var a = new bool[w, h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) a[x, y] = land[ox + x, oy + y];
            var s = SmoothMask(a, ox, oy, KeySeed(isl.islandKey) ^ (isl.coastSeed * 31));
            var bounds = new BoundsInt(ox, oy, 0, w, h, 1);
            var tiles = new TileBase[w * h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) if (s[x, y]) tiles[x + y * w] = tile;
            tm.ClearAllTiles();
            tm.SetTilesBlock(bounds, tiles);
            tm.CompressBounds();
            EditorUtility.SetDirty(tm);
            return true;
        }

        static bool InSquare(int x, int y) => x >= 0 && x < Cells && y >= -Cells && y <= -1;

        /// <summary>Seeded lattice lines along one axis: cell -> block index; block i spans [start[i], start[i + 1]).</summary>
        static int[] Lattice(int n, int origin, int min, int max, int seed, out int count)
        {
            var idx = new int[n];
            count = 0;
            for (int i = 0; i < n; count++)
            {
                int len = min + (int)(HashU(count, origin, seed) % (uint)(max - min + 1));
                for (int k = 0; k < len && i < n; k++, i++) idx[i] = count;
            }
            return idx;
        }

        /// <summary>Blur -> block quantisation (irregular lattice) -> block clean-up (diagonal pinches, nubs, notches, specks, holes).</summary>
        static bool[,] SmoothMask(bool[,] a, int ox, int oy, int seed)
        {
            int w = a.GetLength(0), h = a.GetLength(1);
            var f = new float[w, h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) f[x, y] = a[x, y] ? 1f : 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                var t = new float[w, h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float sum = 0f;
                        for (int k = -SmoothBlur; k <= SmoothBlur; k++) { int xx = x + k; if (xx >= 0 && xx < w) sum += f[xx, y]; }
                        t[x, y] = sum / (2 * SmoothBlur + 1);
                    }
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        float sum = 0f;
                        for (int k = -SmoothBlur; k <= SmoothBlur; k++) { int yy = y + k; if (yy >= 0 && yy < h) sum += t[x, yy]; }
                        f[x, y] = sum / (2 * SmoothBlur + 1);
                    }
            }

            var colOf = Lattice(w, ox, StepMinX, StepMaxX, seed, out int bw);
            var rowOf = Lattice(h, oy, StepMinY, StepMaxY, seed + 7, out int bh);
            var mean = new float[bw, bh];
            var area = new int[bw, bh];
            var sq = new bool[bw, bh];
            var b = new bool[bw, bh];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    int bx = colOf[x], by = rowOf[y];
                    mean[bx, by] += f[x, y];
                    area[bx, by]++;
                    if (InSquare(ox + x, oy + y)) sq[bx, by] = true;
                }
            for (int bx = 0; bx < bw; bx++)
                for (int by = 0; by < bh; by++)
                {
                    mean[bx, by] /= Mathf.Max(1, area[bx, by]);
                    b[bx, by] = sq[bx, by] || mean[bx, by] >= 0.5f;
                }
            bool B(int x, int y) => x >= 0 && y >= 0 && x < bw && y < bh && b[x, y];
            int Nb(int x, int y) => (B(x + 1, y) ? 1 : 0) + (B(x - 1, y) ? 1 : 0) + (B(x, y + 1) ? 1 : 0) + (B(x, y - 1) ? 1 : 0);

            void Clean()
            {
            for (int iter = 0; iter < 8; iter++)
            {
                bool changed = false;
                // notches: a sea block walled in on 3 sides becomes land (1-block bays read as slots)
                for (int x = 0; x < bw; x++)
                    for (int y = 0; y < bh; y++)
                        if (!b[x, y] && Nb(x, y) >= 3) { b[x, y] = true; changed = true; }
                // nubs and 1-block-wide strands: a block held by one side (or none) is cut, so peninsulas are at least
                // 2 blocks wide (a 4-7 cell knob or finger reads as a pillar)
                for (int x = 0; x < bw; x++)
                    for (int y = 0; y < bh; y++)
                        if (b[x, y] && !sq[x, y] && Nb(x, y) <= 1) { b[x, y] = false; changed = true; }
                // diagonal-only contacts (1-cell pinches): bridge with the stronger of the two open blocks
                for (int x = 0; x < bw - 1; x++)
                    for (int y = 0; y < bh - 1; y++)
                    {
                        bool p00 = b[x, y], p10 = b[x + 1, y], p01 = b[x, y + 1], p11 = b[x + 1, y + 1];
                        if (p00 && p11 && !p10 && !p01) { if (mean[x + 1, y] >= mean[x, y + 1]) b[x + 1, y] = true; else b[x, y + 1] = true; changed = true; }
                        else if (p10 && p01 && !p00 && !p11) { if (mean[x, y] >= mean[x + 1, y + 1]) b[x, y] = true; else b[x + 1, y + 1] = true; changed = true; }
                    }
                if (!changed) break;
            }
            }
            Clean();

            var q = new Queue<Vector2Int>();
            var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            // satellite islets: the blur and the strand rule erase small ones, so each detached islet of the input is
            // re-cut from its raw coverage - every block at least half covered (a rounded clump of fine blocks), or for
            // a tiny islet the best-covered 2x2 blocks plus the best neighbouring block - as long as it stays clear of
            // other land
            var seen = new bool[w, h];
            for (int sx = 0; sx < w; sx++)
                for (int sy = 0; sy < h; sy++)
                {
                    if (!a[sx, sy] || seen[sx, sy]) continue;
                    var cov = new Dictionary<Vector2Int, int>();
                    bool main = false;
                    seen[sx, sy] = true; q.Enqueue(new Vector2Int(sx, sy));
                    while (q.Count > 0)
                    {
                        var p = q.Dequeue();
                        main |= InSquare(ox + p.x, oy + p.y);
                        var key = new Vector2Int(colOf[p.x], rowOf[p.y]);
                        cov[key] = cov.TryGetValue(key, out var c0) ? c0 + 1 : 1;
                        foreach (var d in dirs) { var n = p + d; if (n.x >= 0 && n.y >= 0 && n.x < w && n.y < h && a[n.x, n.y] && !seen[n.x, n.y]) { seen[n.x, n.y] = true; q.Enqueue(n); } }
                    }
                    if (main) continue;
                    bool any = false;
                    foreach (var k in cov.Keys) if (b[k.x, k.y]) { any = true; break; }
                    if (any) continue;                     // survived the quantisation on its own
                    int Cov(Vector2Int k) => cov.TryGetValue(k, out var cv) ? cv : 0;
                    int bestSum = 0; var best = new Vector2Int(-1, -1);
                    foreach (var k in cov.Keys)
                        for (int wx = k.x - 1; wx <= k.x; wx++)
                            for (int wy = k.y - 1; wy <= k.y; wy++)
                            {
                                if (wx < 0 || wy < 0 || wx + 1 >= bw || wy + 1 >= bh) continue;
                                int sum = Cov(new Vector2Int(wx, wy)) + Cov(new Vector2Int(wx + 1, wy)) + Cov(new Vector2Int(wx, wy + 1)) + Cov(new Vector2Int(wx + 1, wy + 1));
                                if (sum > bestSum || (sum == bestSum && (wx < best.x || (wx == best.x && wy < best.y)))) { bestSum = sum; best = new Vector2Int(wx, wy); }
                            }
                    if (best.x < 0) continue;
                    var pick = new List<Vector2Int>();
                    foreach (var kv in cov) if (kv.Value * 2 >= area[kv.Key.x, kv.Key.y]) pick.Add(kv.Key);
                    pick.Sort((p1, p2) => p1.x != p2.x ? p1.x.CompareTo(p2.x) : p1.y.CompareTo(p2.y));
                    if (pick.Count >= 5) best = new Vector2Int(-1, -1);
                    else pick = new List<Vector2Int> { best, best + Vector2Int.right, best + Vector2Int.up, best + Vector2Int.one };
                    Vector2Int extra = new Vector2Int(-1, -1); int extraCov = 0;
                    foreach (var pk in pick)
                        foreach (var d in dirs)
                        {
                            var n = pk + d;
                            if (pick.Contains(n) || n.x < 0 || n.y < 0 || n.x >= bw || n.y >= bh) continue;
                            int cv = Cov(n);
                            if (cv >= 0.4f * area[n.x, n.y] && cv > extraCov) { extraCov = cv; extra = n; }
                        }
                    if (extra.x >= 0 && best.x >= 0) pick.Add(extra);
                    bool clear = true;
                    foreach (var pk in pick)
                        for (int dx = -1; dx <= 1 && clear; dx++)
                            for (int dy = -1; dy <= 1 && clear; dy++)
                            {
                                var n = new Vector2Int(pk.x + dx, pk.y + dy);
                                if (B(n.x, n.y) && !pick.Contains(n)) clear = false;
                            }
                    if (!clear) continue;
                    foreach (var pk in pick) b[pk.x, pk.y] = true;
                    // diagonal-only contacts inside the islet: fill the corner block with more coverage
                    for (bool fixedAll = false; !fixedAll;)
                    {
                        fixedAll = true;
                        foreach (var pk in new List<Vector2Int>(pick))
                            foreach (var dg in new[] { new Vector2Int(1, 1), new Vector2Int(1, -1) })
                            {
                                var o2 = pk + dg;
                                if (!pick.Contains(o2)) continue;
                                var c1 = new Vector2Int(pk.x + dg.x, pk.y);
                                var c2 = new Vector2Int(pk.x, pk.y + dg.y);
                                if (b[c1.x, c1.y] || b[c2.x, c2.y]) continue;
                                int v1 = cov.TryGetValue(c1, out var t1) ? t1 : 0, v2 = cov.TryGetValue(c2, out var t2) ? t2 : 0;
                                var add = v1 >= v2 ? c1 : c2;
                                b[add.x, add.y] = true; pick.Add(add); fixedAll = false;
                            }
                    }
                }

            Clean();

            // specks: islets smaller than 2 blocks go; enclosed sea (lakes) is filled
            var comp = new int[bw, bh];
            var isletIds = new HashSet<int>();
            int id = 0;
            for (int x = 0; x < bw; x++)
                for (int y = 0; y < bh; y++)
                {
                    if (!b[x, y] || comp[x, y] != 0) continue;
                    id++;
                    var cells = new List<Vector2Int>();
                    bool hasSq = false;
                    comp[x, y] = id; q.Enqueue(new Vector2Int(x, y));
                    while (q.Count > 0)
                    {
                        var p = q.Dequeue(); cells.Add(p); hasSq |= sq[p.x, p.y];
                        foreach (var d in dirs) { var n = p + d; if (B(n.x, n.y) && comp[n.x, n.y] == 0) { comp[n.x, n.y] = id; q.Enqueue(n); } }
                    }
                    if (!hasSq && cells.Count < 2) foreach (var c in cells) b[c.x, c.y] = false;
                    else if (!hasSq) isletIds.Add(id);
                }
            var sea = new bool[bw, bh];
            for (int x = 0; x < bw; x++) for (int y = 0; y < bh; y++)
                    if ((x == 0 || y == 0 || x == bw - 1 || y == bh - 1) && !b[x, y]) { sea[x, y] = true; q.Enqueue(new Vector2Int(x, y)); }
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                foreach (var d in dirs) { var n = p + d; if (n.x >= 0 && n.y >= 0 && n.x < bw && n.y < bh && !b[n.x, n.y] && !sea[n.x, n.y]) { sea[n.x, n.y] = true; q.Enqueue(n); } }
            }

            var o = new bool[w, h];
            bool Isle(int x, int y) { int bx = colOf[x], by = rowOf[y]; return b[bx, by] && isletIds.Contains(comp[bx, by]); }
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    o[x, y] = InSquare(ox + x, oy + y) || ((b[colOf[x], rowOf[y]] || !sea[colOf[x], rowOf[y]]) && !Isle(x, y));

            // satellite islets: block unions read as little rectangles, so each is redrawn at cell resolution as a rounded
            // clump (superellipse) filling its block bounds (at least 5 x 5 cells), slightly squashed per islet; cells
            // within 2 of the main land are left out so it stays detached
            var mainLand = (bool[,])o.Clone();
            foreach (int iid in isletIds)
            {
                int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                        if (b[colOf[x], rowOf[y]] && comp[colOf[x], rowOf[y]] == iid) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
                if (x0 > x1) continue;
                while (x1 - x0 + 1 < 5) { if (((x1 - x0) & 1) == 0) x0--; else x1++; }
                while (y1 - y0 + 1 < 5) { if (((y1 - y0) & 1) == 0) y0--; else y1++; }
                float cx = (x0 + x1 + 1) * 0.5f, cy = (y0 + y1 + 1) * 0.5f, rx = (x1 - x0 + 1) * 0.5f, ry = (y1 - y0 + 1) * 0.5f;
                float p = 2.2f + (HashU(iid, x0, seed + 31) % 100) / 100f * 0.8f;      // 2.2 (round) .. 3.0 (squarer)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(w - 1, x1); x++)
                    for (int y = Mathf.Max(0, y0); y <= Mathf.Min(h - 1, y1); y++)
                    {
                        float dx = Mathf.Abs((x + 0.5f - cx) / rx), dy = Mathf.Abs((y + 0.5f - cy) / ry);
                        if (Mathf.Pow(dx, p) + Mathf.Pow(dy, p) > 1f) continue;
                        bool near = false;
                        for (int ka = -2; ka <= 2 && !near; ka++)
                            for (int kc = -2; kc <= 2 && !near; kc++)
                            {
                                int nx = x + ka, ny = y + kc;
                                near = nx >= 0 && ny >= 0 && nx < w && ny < h && mainLand[nx, ny];
                            }
                        if (!near) o[x, y] = true;
                    }
            }
            return o;
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
            var underCells = BuildUnderside(isl, land, cliffCells);
            BuildShadow(isl, land, cliffCells);
            BuildVeil(isl, land, cliffCells, underCells);
            BuildBoundary(isl);
            IslandDressingBuilder.BuildIsland(isl);      // macro patches, field patches, paths, scatter (ADR 0005)
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
            // 3/4 cliff wall (ART-SPEC §3.6): 3 cells tall, painted on the rim cells of a tilemap shifted one cell down
            // so the wall's top meets the coast's south edge and it hangs 3 cells below it. It sorts BEHIND the coast,
            // so land further south on screen (a bay's far shore, an islet) correctly hides it.
            var wall = AssetDatabase.LoadAssetAtPath<TileBase>(IslandArtBuilder.CliffWallTilePath(isl.islandKey));
            var cr0 = cliff.GetComponent<TilemapRenderer>();
            HasWall = wall != null;
            cliff.transform.localPosition = wall != null ? new Vector3(0f, -WallDrop, 0f) : Vector3.zero;
            if (cr0 != null) cr0.sortingOrder = wall != null ? -3 : -1;
            var tile = wall != null ? wall : AssetDatabase.LoadAssetAtPath<TileBase>(IslandArtBuilder.CliffTilePath(isl.islandKey));
            var ws = wall != null ? IslandDressingBuilder.EnsureWallSet(isl.islandKey) : null;
            if (ws != null)
            {
                // a wall further south on screen belongs to land in front: draw the rows top-down so it overlaps the one behind
                if (cr0 != null) cr0.sortOrder = TilemapRenderer.SortOrder.TopLeft;
                int seed = KeySeed(isl.islandKey) + 517;
                var sorted = new List<Vector2Int>(set);
                sorted.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
                var mid = new Dictionary<Vector2Int, int>();
                foreach (var c in sorted)
                {
                    var l = WallSide(set, land, c, -1);
                    var r = WallSide(set, land, c, 1);
                    TileBase t;
                    if (l == Joint.Run && r == Joint.Run)
                    {
                        // mid variants: seeded, never the same one as the wall cell to the left
                        int v = (int)(HashU(c.x, c.y, seed) % 3u);
                        if (mid.TryGetValue(c + Vector2Int.left, out var pv) && pv == v) v = (v + 1 + (int)(HashU(c.x, c.y, seed + 1) % 2u)) % 3;
                        mid[c] = v;
                        t = ws.mid[v];
                    }
                    else if (r == Joint.Run) t = l == Joint.End ? ws.endL : ws.tuckL;
                    else if (l == Joint.Run) t = r == Joint.End ? ws.endR : ws.tuckR;
                    else t = ws.pair[(l == Joint.End ? 0 : 2) + (r == Joint.End ? 0 : 1)];
                    cliff.SetTile(new Vector3Int(c.x, c.y, 0), t);
                }
            }
            else if (tile != null)
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

        enum Joint { Run, End, Tuck }

        /// <summary>
        /// How the south wall under rim cell <paramref name="c"/> meets its side <paramref name="dx"/> (-1 left, +1 right):
        /// Run - the wall goes on (a rim cell beside it); Tuck - land further south stands beside it (a concave step: the
        /// wall runs on behind that land, whose side face covers the joint) or a higher wall one row up overlaps it; End -
        /// open sky (an outer corner: the free end cap, under the side face's tapering end frame).
        /// </summary>
        static Joint WallSide(HashSet<Vector2Int> rim, Land land, Vector2Int c, int dx)
        {
            var n = new Vector2Int(c.x + dx, c.y);
            if (rim.Contains(n)) return Joint.Run;
            if (land[n.x, n.y]) return Joint.Tuck;
            if (rim.Contains(new Vector2Int(n.x, n.y + 1))) return Joint.Tuck;
            return Joint.End;
        }

        // 3/4 top-down: only the SOUTH-facing coast shows its cliff face, and the hanging underside is a compact
        // band of native-scale pieces under that south cliff (never beside the island, never stretched).
        const int BodyMaxRows = 3;                 // dark rock band right under the cliff rim (cells)
        const int ChainStep = 3;                   // an edge chain continues across a step of up to one lattice row (smoothed coasts step by 2-3)
        const float SecondRowDrop = 1.5f;          // the deeper, central second row of pieces starts this far down
        const float WallDrop = 1f - 6f / 32f;      // the cliff-wall tilemap is shifted this many cells down: the wall's ragged top 6 px tuck behind the coast edge (4 px left sky specks under the blob tiles' 3 px inset)
        const int WallBelowRim = 2;                // the 3-cell wall reaches this many cells below the rim cell
        static bool HasWall;                       // set by BuildCliff: the delivered 3/4 cliff wall is in use
        const float WallTuck = 2.2f;               // rim cell top -> first opaque row of a hanging piece (the wall's dense part ends ~2.4 cells down, then dithers out)
        const float PieceMaxHeight = 6f;           // tallest piece (rock 256x192 at PPU 32)
        const string IncomingIslands = "Assets/_Project/Art/Incoming/islands/";
        const string CloudPuffs = "Assets/_Project/Art/Incoming/sky/sky_cloudpuff_3q_128x64_3f.png";

        /// <summary>Total cells the underside reaches below the cliff rim: body / second row + tallest piece + a little mist.</summary>
        public static float UndersideExtent(float span) => 1f + WallBelowRim + SecondRowDrop + PieceMaxHeight + 2f;

        static float Snap(float v) => Mathf.Round(v * 32f) / 32f;

        /// <summary>
        /// Underside for the 3/4 view. Every SOUTH-facing edge (a land cell with open sky below it - the cells the
        /// cliff rim sits on) can carry hanging rock; west / east / north faces never do, so nothing stands beside
        /// the island like a pillar. Edges are grouped into profiles per column (p = 0: the lowest edge = the
        /// island's south coast; p = 1, 2: higher edges with open sky below - islets, the north shore of a bay) and
        /// each edge gets a factor from its chain (edges continuing left / right within 2 rows - a jump breaks the
        /// chain, so steep walls and short stubs get small pieces or none) and the open sky below it.
        /// Under it: dark underside fill behind every rim cell (no sky showing through the rim art) and 1-3 body
        /// rows under the strongest edges, native-scale pieces (rock / roots / stalactite / vines; lava drips on the
        /// volcano) hung top-centre half a cell under the rim, a deeper second row under the southernmost stretch
        /// (inverted-cone taper) and a couple of pixel cloud puffs at the tip. All of it sorts on the Sky layer, so
        /// any land in front (further south on screen) hides it. Returns the covered cells (for the veil mask).
        /// </summary>
        static HashSet<Vector2Int> BuildUnderside(Island isl, Land land, HashSet<Vector2Int> cliff)
        {
            const int Profiles = 3, NoEdge = int.MinValue;
            var covered = new HashSet<Vector2Int>();
            int w = land.x1 - land.x0 + 1;
            var prof = new int[Profiles][];
            var gap = new int[Profiles][];
            for (int p = 0; p < Profiles; p++) { prof[p] = new int[w]; gap[p] = new int[w]; }
            int minH = int.MaxValue;
            for (int i = 0; i < w; i++)
            {
                int x = land.x0 + i;
                for (int p = 0; p < Profiles; p++) { prof[p][i] = NoEdge; gap[p][i] = 0; }
                int p0 = 0, lastLand = int.MinValue;
                for (int y = land.y0; y <= land.y1 + 1 && p0 < Profiles; y++)
                {
                    bool here = land[x, y], below = land[x, y - 1];
                    if (here && !below)
                    {
                        int c = y - 1;
                        int g = lastLand == int.MinValue ? 99 : c - lastLand;     // open cells under the rim cell (incl. it)
                        if (p0 == 0 || g >= 4) { prof[p0][i] = c; gap[p0][i] = g; p0++; }
                    }
                    if (here) lastLand = y;
                }
                if (prof[0][i] != NoEdge) minH = Mathf.Min(minH, prof[0][i]);
            }

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
            if (minH == int.MaxValue) return covered;

            // per-profile factor: chain reach (both sides) x open sky below; smoothed along the chain
            var fs = new float[Profiles][];
            for (int p = 0; p < Profiles; p++)
            {
                var h = prof[p];
                var f = new float[w];
                for (int i = 0; i < w; i++)
                {
                    if (h[i] == NoEdge) continue;
                    int l = 0, r = 0;
                    for (int j = i - 1; j >= 0 && l < 6 && h[j] != NoEdge && Mathf.Abs(h[j] - h[j + 1]) <= ChainStep; j--) l++;
                    for (int j = i + 1; j < w && r < 6 && h[j] != NoEdge && Mathf.Abs(h[j] - h[j - 1]) <= ChainStep; j++) r++;
                    float endK = Mathf.Clamp01((Mathf.Min(l, r) + 1) / 4f);
                    float gapK = Mathf.Clamp01((gap[p][i] - 2) / 6f);
                    f[i] = endK * gapK;
                }
                fs[p] = new float[w];
                for (int i = 0; i < w; i++)
                {
                    if (f[i] <= 0f) continue;
                    float s = f[i]; int n = 1;
                    for (int j = i - 1; j >= Mathf.Max(0, i - 2) && h[j] != NoEdge && Mathf.Abs(h[j] - h[j + 1]) <= ChainStep; j--) { s += f[j]; n++; }
                    for (int j = i + 1; j <= Mathf.Min(w - 1, i + 2) && h[j] != NoEdge && Mathf.Abs(h[j] - h[j - 1]) <= ChainStep; j++) { s += f[j]; n++; }
                    fs[p][i] = Mathf.Min(f[i], s / n + 0.1f);          // smoothing may soften, never extend, a factor
                }
            }

            bool volcano = isl.islandKey == "volcano";
            // 3/4 underside-only pieces (Islands-036, roots touch-up Islands-037; flat top cut, no grass): they hang from
            // behind the cliff wall's fading bottom. Falls back to the old side-view pieces if the _3q set is missing.
            Sprite Pick1(string q, string old) { var s = IslandArtBuilder.Single(IncomingIslands + q); return s != null ? s : IslandArtBuilder.Single(old); }
            var rock = Pick1("island_underside_rock_3q_256x192.png", IslandArtBuilder.UndersideRock);
            var spike = Pick1("island_underside_stalactite_3q_96x160.png", IslandArtBuilder.UndersideStalactite);
            var roots = IslandDressingBuilder.MaskedUndersideRoots() ?? IslandArtBuilder.Single(IslandArtBuilder.UndersideRoots);
            var vines = Pick1("island_underside_vines_3q_128x128.png", IncomingIslands + "island_underside_vines_128x128.png");
            var lava = IslandArtBuilder.Frames(IncomingIslands + "island_underside_lavadrip_3q_128x160_4f.png");
            if (lava.Length == 0) lava = IslandArtBuilder.Frames(IncomingIslands + "island_underside_lavadrip_128x160_4f.png");
            var puffs = IslandArtBuilder.Frames(CloudPuffs);
            var tint = Color.Lerp(Color.white, WorldBuilder.UndersideTint(isl.islandKey), 0.3f);
            var rng = new System.Random(KeySeed(isl.islandKey) ^ (isl.coastSeed * 7919) ^ 0x2545F49);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            const string Sky = WorldBuilder.SkyLayer;

            // ---- body: underside fill behind every rim cell + 1-3 rows under the strong edges
            var fillTile = IslandArtBuilder.EnsureUndersideFillTile();
            var bodyT = new GameObject("Body").transform;
            bodyT.SetParent(root, false);
            var body = bodyT.gameObject.AddComponent<Tilemap>();
            var bodyR = bodyT.gameObject.AddComponent<TilemapRenderer>();
            bodyR.sortingLayerName = Sky;
            bodyR.sortingOrder = 30;
            body.color = Color.Lerp(tint, Color.black, 0.25f);
            var cellSet = new HashSet<Vector3Int>();
            if (HasWall)
            {
                // the opaque 3/4 cliff wall replaces the dark fill + body rows: its ragged, fading bottom shows the sky,
                // and the pieces below tuck behind it. Only report the wall's cells (veil mask).
                foreach (var c in cliff) for (int r = 0; r <= WallBelowRim; r++) covered.Add(new Vector2Int(c.x, c.y - r));
                Object.DestroyImmediate(bodyT.gameObject);
            }
            else foreach (var c in cliff) cellSet.Add(new Vector3Int(c.x, c.y, 0));
            for (int p = 0; p < Profiles && !HasWall; p++)
                for (int i = 0; i < w; i++)
                {
                    if (fs[p][i] <= 0.15f) continue;
                    int rows = Mathf.Clamp(Mathf.RoundToInt(fs[p][i] * BodyMaxRows), 1, Mathf.Min(BodyMaxRows, gap[p][i] - 1));
                    for (int r = 0; r < rows; r++)
                    {
                        if (r == rows - 1 && rows > 1 && rng.NextDouble() < 0.35) continue;      // ragged lower edge
                        cellSet.Add(new Vector3Int(land.x0 + i, prof[p][i] - 1 - r, 0));
                    }
                }
            foreach (var c in cellSet) covered.Add(new Vector2Int(c.x, c.y));
            if (fillTile != null && cellSet.Count > 0)
            {
                var cells = new Vector3Int[cellSet.Count];
                cellSet.CopyTo(cells);
                var arr = new TileBase[cells.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = fillTile;
                body.SetTiles(cells, arr);
            }

            // ---- native-scale pieces (scale 1, PPU 32, flipX only), hung top-centre from the rim
            int pieceN = 0;
            float tipY = float.MaxValue, tipX = 0f;
            Sprite Pick(float k)
            {
                float r = (float)rng.NextDouble();
                if (volcano && lava.Length > 0 && r < 0.55f) return lava[rng.Next(lava.Length)];
                if (volcano) return k > 0.35f && r < 0.85f ? rock : spike;            // basalt: no roots / green vines
                var v = vines != null ? vines : spike;
                if (k > 0.5f) return r < 0.55f ? rock : r < 0.82f ? roots : r < 0.92f ? v : spike;
                if (k > 0.2f) return r < 0.3f ? rock : r < 0.6f ? roots : r < 0.85f ? v : spike;
                return r < 0.6f ? v : spike;
            }
            Sprite Narrow() => volcano && lava.Length > 0 ? lava[rng.Next(lava.Length)] : (vines != null && rng.NextDouble() < 0.5 ? vines : spike);

            for (int p = 0; p < Profiles; p++)
            {
                var h = prof[p]; var f = fs[p];
                // highest hang line over a span of this profile's chain (pieces tuck under it); NaN if it leaves the chain
                float HangOver(float x0, float x1)
                {
                    int a = Mathf.FloorToInt(x0) - land.x0, b = Mathf.CeilToInt(x1) - 1 - land.x0;
                    if (a < 0 || b >= w || b < a) return float.NaN;
                    int m = int.MinValue;
                    for (int i = a; i <= b; i++)
                    {
                        if (f[i] <= 0.05f || (i > a && Mathf.Abs(h[i] - h[i - 1]) > ChainStep)) return float.NaN;
                        m = Mathf.Max(m, h[i]);
                    }
                    return m;
                }
                bool Hang(Sprite sp, float cx, float dropBelowRim, int order, float dark)
                {
                    if (sp == null) return false;
                    var bnd = sp.bounds;
                    float half = bnd.size.x * 0.5f;
                    float top = HangOver(cx - half + 0.5f, cx + half - 0.5f);   // the outer half-cell of the art is mostly transparent
                    if (float.IsNaN(top)) return false;
                    // with the 3/4 cliff wall: the art's first opaque row sits WallTuck cells under the rim cell's top,
                    // i.e. a few px above the wall's solid bottom (the wall fades out over its last ~0.9 cell)
                    float margin = HasWall ? IslandDressingBuilder.OpaqueTopMargin(sp) / 32f : 0f;
                    float yTop = HasWall ? top + 1f - WallTuck - dropBelowRim * 0.35f + margin      // small jitter: the flat top cut stays hidden
                                         : top + 0.5f - dropBelowRim;                        // tucked half a cell under the rim (hidden behind the ground)
                    var pos = new Vector3(Snap(cx - bnd.center.x), Snap(yTop - bnd.max.y), 0f);
                    var sr = WorldBuilder.Deco(root, "Piece" + pieceN++, sp, pos, 1f, Color.Lerp(tint, Color.black, dark), order, Sky);
                    sr.flipX = rng.NextDouble() < 0.5;
                    float bottom = yTop - bnd.size.y;
                    if (p == 0 && bottom < tipY) { tipY = bottom; tipX = cx; }
                    for (int x = Mathf.FloorToInt(cx - half); x < Mathf.CeilToInt(cx + half); x++)
                        for (int y = Mathf.FloorToInt(bottom); y < Mathf.CeilToInt(yTop); y++) covered.Add(new Vector2Int(x, y));
                    return true;
                }

                // first row: along every chain, spacing ~ the piece width so the tops form one ragged band
                for (float x = land.x0 + 1.5f; x < land.x1 - 0.5f;)
                {
                    int i = Mathf.Clamp(Mathf.FloorToInt(x) - land.x0, 0, w - 1);
                    if (f[i] <= (HasWall ? 0.3f : 0.05f) || (HasWall && rng.NextDouble() < 0.3)) { x += HasWall ? 2f : 1f; continue; }
                    var sp = Pick(f[i]);
                    int order = 38 + pieceN % 3;
                    if (Hang(sp, x, R(0f, 0.6f), order, R(0f, 0.12f))) { x += sp.bounds.size.x * (HasWall ? R(0.85f, 1.15f) : R(0.55f, 0.75f)); continue; }
                    var narrow = Narrow();       // the wide piece would leave the chain: try a narrow one
                    if (Hang(narrow, x, R(0f, 0.4f), order, R(0f, 0.12f))) { x += narrow.bounds.size.x * R(0.65f, 0.85f); continue; }
                    x += 1f;
                }
                // second, deeper row under the southernmost stretch: behind the first row -> an inverted-cone taper
                if (p == 0 && !HasWall)
                    for (float x = land.x0 + 4f; x < land.x1 - 3f; x += HasWall ? R(11f, 16f) : R(7f, 10f))
                    {
                        int i = Mathf.Clamp(Mathf.FloorToInt(x) - land.x0, 0, w - 1);
                        if (f[i] < 0.8f || h[i] - minH > 10) continue;
                        Sprite sp = volcano && lava.Length > 0 ? lava[rng.Next(lava.Length)] : (rng.NextDouble() < 0.6 ? rock : spike);
                        Hang(sp, x, SecondRowDrop + R(-0.5f, 0.5f), 34, R(0.15f, 0.3f));
                    }
            }

            // a couple of loose pixel cloud puffs drifting past the tip (1x, no stretching)
            if (puffs.Length > 0 && tipY < float.MaxValue)
            {
                var mist = new Color(0.86f, 0.92f, 0.96f, 0.55f);
                WorldBuilder.Deco(root, "MistA", puffs[rng.Next(puffs.Length)], new Vector3(Snap(tipX - R(2f, 4f)), Snap(tipY + 0.5f), 0f), 1f, mist, 45, Sky).flipX = rng.NextDouble() < 0.5;
                WorldBuilder.Deco(root, "MistB", puffs[rng.Next(puffs.Length)], new Vector3(Snap(tipX + R(3f, 6f)), Snap(tipY + 1.5f), 0f), 1f, new Color(mist.r, mist.g, mist.b, 0.4f), 44, Sky).flipX = rng.NextDouble() < 0.5;
            }
            EditorUtility.SetDirty(root.gameObject);
            return covered;
        }

        /// <summary>
        /// Soft drop shadow onto the sky: a black, semi-transparent copy of the landmass (same blob Rule Tile, so the
        /// silhouette matches exactly) + cliff rim, offset <see cref="ShadowOffset"/> down-right. Pixel-crisp, no blur; drawn
        /// on the Sky layer above the cloud sea and below the underside.
        /// </summary>
        // a contact shadow hugging the side faces and wall ends: half a cell (the old offset) left a detached dark block
        // beside every W / E step of the coast
        const float ShadowOffset = 6f / 32f;

        static void BuildShadow(Island isl, Land land, HashSet<Vector2Int> cliff)
        {
            var old = isl.transform.Find("Shadow");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Shadow").transform;
            root.SetParent(isl.transform, false);
            root.SetSiblingIndex(0);
            root.localPosition = new Vector3(ShadowOffset, -ShadowOffset, 0f);
            var shade = new Color(0f, 0f, 0f, 0.35f);
            Tilemap Map(string name)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                var tm = go.AddComponent<Tilemap>();
                var tr = go.AddComponent<TilemapRenderer>();
                tr.sortingLayerName = WorldBuilder.SkyLayer;
                tr.sortingOrder = 20;
                tm.color = shade;
                return tm;
            }
            var coastTile = AssetDatabase.LoadAssetAtPath<TileBase>(IslandArtBuilder.CoastTilePath(isl.islandKey));
            if (coastTile != null)
            {
                var pos = new List<Vector3Int>();
                for (int x = land.x0; x <= land.x1; x++)
                    for (int y = land.y0; y <= land.y1; y++)
                        if (land[x, y]) pos.Add(new Vector3Int(x, y, 0));
                var arr = new TileBase[pos.Count];
                for (int i = 0; i < arr.Length; i++) arr[i] = coastTile;
                Map("Land").SetTiles(pos.ToArray(), arr);
            }
            var wallTile = AssetDatabase.LoadAssetAtPath<TileBase>(IslandArtBuilder.CliffWallTilePath(isl.islandKey));
            var cliffTile = wallTile != null ? wallTile : AssetDatabase.LoadAssetAtPath<TileBase>(IslandArtBuilder.CliffTilePath(isl.islandKey));
            if (cliffTile != null && cliff.Count > 0)
            {
                var pos = new List<Vector3Int>(cliff.Count);
                foreach (var c in cliff) pos.Add(new Vector3Int(c.x, c.y, 0));
                var arr = new TileBase[pos.Count];
                for (int i = 0; i < arr.Length; i++) arr[i] = cliffTile;
                var rim = Map("Rim");
                if (wallTile != null) rim.transform.localPosition = new Vector3(0f, -WallDrop, 0f);     // follows the 3-cell wall
                // the wall's per-joint pieces (BuildCliff) when present, so the shadow keeps the same end caps
                var built = isl.transform.Find("CliffRim") != null ? isl.transform.Find("CliffRim").GetComponent<Tilemap>() : null;
                if (wallTile != null && built != null)
                    for (int i = 0; i < arr.Length; i++) { var t = built.GetTile(pos[i]); if (t != null) arr[i] = t; }
                rim.SetTiles(pos.ToArray(), arr);
            }
            EditorUtility.SetDirty(root.gameObject);
        }

        static void BuildVeil(Island isl, Land land, HashSet<Vector2Int> cliff, HashSet<Vector2Int> under)
        {
            // cell mask of everything the fog must cover: ground + cliff rim + the hanging underside
            int uy0 = land.y0, ux0 = land.x0, ux1 = land.x1;
            foreach (var c in under) { uy0 = Mathf.Min(uy0, c.y); ux0 = Mathf.Min(ux0, c.x); ux1 = Mathf.Max(ux1, c.x); }
            int gx0 = ux0 - 3, gx1 = ux1 + 3, gy0 = uy0 - 4, gy1 = land.y1 + 3;
            int gw = gx1 - gx0 + 1, gh = gy1 - gy0 + 1;
            var m = new bool[gw, gh];
            for (int x = land.x0; x <= land.x1; x++)
                for (int y = land.y0; y <= land.y1; y++)
                    if (land[x, y]) m[x - gx0, y - gy0] = true;
            foreach (var c in cliff) m[c.x - gx0, c.y - gy0] = true;
            foreach (var c in under) m[c.x - gx0, c.y - gy0] = true;
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

        /// <summary>
        /// Soft edge of the playable square: scattered rocks, shrubs and grass tufts along the boundary (no hard line),
        /// plus a "Guide" frame that <see cref="PlacementGuide"/> shows only while a building is being placed.
        /// </summary>
        static void BuildBoundary(Island isl)
        {
            var old = isl.transform.Find("Boundary");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var props = IslandArtBuilder.Frames(IslandArtBuilder.BoundaryProps);
            var incoming = ArtIntake.LoadIncoming(out var incomingFrames);
            Sprite stone = incoming.TryGetValue("item_stone", out var st0) ? st0 : null;
            Sprite bush = incomingFrames.TryGetValue("node_bush", out var bf) && bf.Length > 0 ? bf[0] : (incoming.TryGetValue("node_bush", out var b0) ? b0 : null);
            Sprite[] trees = incomingFrames.TryGetValue("deco_tree", out var tf) && tf.Length > 0 ? tf : null;
            var root = new GameObject("Boundary").transform;
            root.SetParent(isl.transform, false);
            var rng = new System.Random(KeySeed(isl.islandKey) ^ 0x1234567);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            if (props.Length >= 6)
            {
                void Scatter(Vector2 a, Vector2 b)
                {
                    float len = Vector2.Distance(a, b);
                    var dir = (b - a) / len;
                    for (float s = R(0f, 2f); s < len; s += R(1.4f, 4.5f))
                    {
                        if (rng.NextDouble() < 0.18) continue;
                        float kind = (float)rng.NextDouble();
                        int type = kind < 0.3f ? 0 : kind < 0.58f ? 1 : kind < 0.9f || trees == null ? 2 : 3;       // rock, shrub, tuft, tree
                        var p = a + dir * s + new Vector2(-dir.y, dir.x) * R(-0.9f, 0.9f);
                        var sr = new GameObject(type == 0 ? "Rock" : type == 1 ? "Shrub" : type == 2 ? "Tuft" : "Tree").AddComponent<SpriteRenderer>();
                        sr.transform.SetParent(root, false);
                        float sc = type == 0 ? R(0.5f, 1.0f) : type == 1 ? R(0.6f, 1.1f) : R(0.6f, 1.0f);
                        Sprite pick = props[Mathf.Min(type, 2) * 2 + rng.Next(2)];
                        // delivered art stays at native PPU-32 scale (1x); only the placeholder frames are jittered
                        if (type == 0 && stone != null) { pick = stone; sc = 1f; }
                        else if (type == 1 && bush != null) { pick = bush; sc = 1f; }
                        else if (type == 3) { pick = trees[rng.Next(trees.Length)]; sc = 1f; }
                        float k = pick.pixelsPerUnit / 32f;
                        sr.transform.localPosition = new Vector3(p.x, p.y - 0.35f - pick.bounds.min.y * (sc / k), 0f);   // sprite bottom sits on the edge line (any pivot)
                        sr.transform.localScale = new Vector3(sc / k, sc / k, 1f);
                        sr.sprite = pick;
                        sr.flipX = rng.NextDouble() < 0.5;
                        sr.sortingLayerName = Ground;
                        sr.sortingOrder = 13;
                    }
                }
                float n = Cells;
                Scatter(new Vector2(0, 0), new Vector2(n, 0));
                Scatter(new Vector2(0, -n), new Vector2(n, -n));
                Scatter(new Vector2(0, 0), new Vector2(0, -n));
                Scatter(new Vector2(n, 0), new Vector2(n, -n));
            }

            // faint guide line, shown only while placing (PlacementGuide)
            BuildingsBuilder.EnsureShapes();
            var guide = BuildingsBuilder.Frame(root, "Guide", 14, Ground);
            guide.transform.localPosition = Vector3.zero;
            guide.Set(Cells, Cells, ViewKit.U(2f), new Color(0.45f, 0.95f, 0.6f, 0.45f), true);
            guide.gameObject.SetActive(false);
            var pg = root.gameObject.AddComponent<PlacementGuide>();
            pg.guide = guide.gameObject;
        }

        // ------------------------------------------------------------------ spacing

        [MenuItem("Idle Grounds/World/Respace Islands (from coasts)")]
        public static void RespaceMenu() { Respace(); MarkDirty(); }

        /// <summary>
        /// Re-spaces the Island GameObjects on the original 3x3 neighbour layout so there are at least
        /// <see cref="CoastGap"/> cells of open sky between neighbouring coasts (incl. islets). The Center keeps its place;
        /// the scene stays the authority (GameRunner pushes the offsets to the sim).
        /// </summary>
        public static void Respace()
        {
            const float CoastGap = 14f, SkyGap = 12f;   // horizontal coast gap; open sky under the upper row's underside
            var byKey = new Dictionary<string, Island>();
            foreach (var i in Islands()) byKey[i.islandKey] = i;
            if (!byKey.ContainsKey("center")) return;
            // grid cell (col,row) of each Island (RegionDef rx/ry in the original)
            var grid = new Dictionary<string, Vector2Int>
            {
                { "farm", new Vector2Int(0, 0) }, { "center", new Vector2Int(1, 0) }, { "mine", new Vector2Int(2, 0) },
                { "grove", new Vector2Int(0, 1) }, { "fishing", new Vector2Int(1, 1) }, { "volcano", new Vector2Int(2, 1) },
                { "celestial", new Vector2Int(1, 2) },
            };
            var ext = new Dictionary<string, (float l, float r, float t, float b)>();
            foreach (var kv in byKey)
            {
                var land = ReadLand(kv.Value);
                ext[kv.Key] = (-land.x0, land.x1 - (Cells - 1), land.y1 + 1, -Cells - land.y0);
            }
            float MaxOver(int col, int row, bool useCol, System.Func<(float l, float r, float t, float b), float> f)
            {
                float m = 0f;
                foreach (var kv in grid)
                    if (byKey.ContainsKey(kv.Key) && (useCol ? kv.Value.x == col : kv.Value.y == row)) m = Mathf.Max(m, f(ext[kv.Key]));
                return m;
            }
            var undersideExt = new Dictionary<string, float>();
            foreach (var kv in byKey)
            {
                var land = ReadLand(kv.Value);
                int c0x = int.MaxValue, c1x = int.MinValue;
                for (int x = land.x0; x <= land.x1; x++)
                    for (int y = land.y0; y <= land.y1; y++)
                        if (land[x, y]) { c0x = Mathf.Min(c0x, x); c1x = Mathf.Max(c1x, x); break; }
                undersideExt[kv.Key] = c1x >= c0x ? UndersideExtent(c1x - c0x + 1) : 0f;
            }
            float MaxUnder(int row) { float m = 0f; foreach (var kv in grid) if (kv.Value.y == row && undersideExt.TryGetValue(kv.Key, out var u)) m = Mathf.Max(m, u); return m; }
            var cx = new float[3]; var cy = new float[3];
            var c0 = byKey["center"].transform.position;
            cx[1] = c0.x; cy[0] = c0.y;
            cx[0] = cx[1] - Cells - (MaxOver(0, 0, true, e => e.r) + MaxOver(1, 0, true, e => e.l) + CoastGap);
            cx[2] = cx[1] + Cells + (MaxOver(1, 0, true, e => e.r) + MaxOver(2, 0, true, e => e.l) + CoastGap);
            cy[1] = cy[0] - Cells - (MaxOver(0, 0, false, e => e.b) + MaxOver(0, 1, false, e => e.t) + SkyGap + MaxUnder(0));
            cy[2] = cy[1] - Cells - (MaxOver(0, 1, false, e => e.b) + MaxOver(0, 2, false, e => e.t) + SkyGap + MaxUnder(1));
            foreach (var kv in grid)
            {
                if (!byKey.TryGetValue(kv.Key, out var isl)) continue;
                isl.transform.position = new Vector3(Mathf.Round(cx[kv.Value.x]), Mathf.Round(cy[kv.Value.y]), 0f);
                EditorUtility.SetDirty(isl.transform);
            }
            Debug.Log("[IdleGrounds] Islands re-spaced: columns x=" + Mathf.Round(cx[0]) + "/" + Mathf.Round(cx[1]) + "/" + Mathf.Round(cx[2]) + ", rows y=" + Mathf.Round(cy[0]) + "/" + Mathf.Round(cy[1]) + "/" + Mathf.Round(cy[2]));
        }
    }
}
