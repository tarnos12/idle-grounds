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
    ///   - Underside (3/4 view): compact native-scale hanging rock under SOUTH-facing edges only + pixel cloud puffs,
    ///   - Shadow: a translucent black copy of the silhouette offset half a cell down-right onto the sky,
    ///   - Veil: tiled fog masked (SpriteMask) to the landmass + rim + underside while the Island is locked,
    ///   - Boundary: a low stone wall marking the playable square.
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

        // 3/4 top-down: only the SOUTH-facing coast shows its cliff face, and the hanging underside is a compact
        // band of native-scale pieces under that south cliff (never beside the island, never stretched).
        const int BodyMaxRows = 3;                 // dark rock band right under the cliff rim (cells)
        const float SecondRowDrop = 2.5f;          // the deeper, central second row of pieces starts this far down
        const float PieceMaxHeight = 6f;           // tallest piece (rock 256x192 at PPU 32)
        const string IncomingIslands = "Assets/_Project/Art/Incoming/islands/";
        const string CloudPuffs = "Assets/_Project/Art/Incoming/sky/sky_cloudpuff_128x64_3f.png";

        /// <summary>Total cells the underside reaches below the cliff rim: body / second row + tallest piece + a little mist.</summary>
        public static float UndersideExtent(float span) => 1f + SecondRowDrop + PieceMaxHeight + 3f;

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
                    for (int j = i - 1; j >= 0 && l < 6 && h[j] != NoEdge && Mathf.Abs(h[j] - h[j + 1]) <= 2; j--) l++;
                    for (int j = i + 1; j < w && r < 6 && h[j] != NoEdge && Mathf.Abs(h[j] - h[j - 1]) <= 2; j++) r++;
                    float endK = Mathf.Clamp01((Mathf.Min(l, r) + 1) / 4f);
                    float gapK = Mathf.Clamp01((gap[p][i] - 2) / 6f);
                    f[i] = endK * gapK;
                }
                fs[p] = new float[w];
                for (int i = 0; i < w; i++)
                {
                    if (f[i] <= 0f) continue;
                    float s = f[i]; int n = 1;
                    for (int j = i - 1; j >= Mathf.Max(0, i - 2) && h[j] != NoEdge && Mathf.Abs(h[j] - h[j + 1]) <= 2; j--) { s += f[j]; n++; }
                    for (int j = i + 1; j <= Mathf.Min(w - 1, i + 2) && h[j] != NoEdge && Mathf.Abs(h[j] - h[j - 1]) <= 2; j++) { s += f[j]; n++; }
                    fs[p][i] = Mathf.Min(f[i], s / n + 0.1f);          // smoothing may soften, never extend, a factor
                }
            }

            bool volcano = isl.islandKey == "volcano";
            var rock = IslandArtBuilder.Single(IslandArtBuilder.UndersideRock);
            var spike = IslandArtBuilder.Single(IslandArtBuilder.UndersideStalactite);
            var roots = IslandArtBuilder.Single(IslandArtBuilder.UndersideRoots);
            var vines = IslandArtBuilder.Single(IncomingIslands + "island_underside_vines_128x128.png");
            var lava = IslandArtBuilder.Frames(IncomingIslands + "island_underside_lavadrip_128x160_4f.png");
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
            foreach (var c in cliff) cellSet.Add(new Vector3Int(c.x, c.y, 0));
            for (int p = 0; p < Profiles; p++)
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
                        if (f[i] <= 0.05f || (i > a && Mathf.Abs(h[i] - h[i - 1]) > 2)) return float.NaN;
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
                    float yTop = top + 0.5f - dropBelowRim;                        // tucked half a cell under the rim (hidden behind the ground)
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
                    if (f[i] <= 0.05f) { x += 1f; continue; }
                    var sp = Pick(f[i]);
                    int order = 38 + pieceN % 3;
                    if (Hang(sp, x, R(0f, 0.6f), order, R(0f, 0.12f))) { x += sp.bounds.size.x * R(0.55f, 0.75f); continue; }
                    var narrow = Narrow();       // the wide piece would leave the chain: try a narrow one
                    if (Hang(narrow, x, R(0f, 0.4f), order, R(0f, 0.12f))) { x += narrow.bounds.size.x * R(0.65f, 0.85f); continue; }
                    x += 1f;
                }
                // second, deeper row under the southernmost stretch: behind the first row -> an inverted-cone taper
                if (p == 0)
                    for (float x = land.x0 + 4f; x < land.x1 - 3f; x += R(7f, 10f))
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
        /// silhouette matches exactly) + cliff rim, offset half a cell down-right. Pixel-crisp, no blur; drawn on the
        /// Sky layer above the cloud sea and below the underside.
        /// </summary>
        static void BuildShadow(Island isl, Land land, HashSet<Vector2Int> cliff)
        {
            var old = isl.transform.Find("Shadow");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Shadow").transform;
            root.SetParent(isl.transform, false);
            root.SetSiblingIndex(0);
            root.localPosition = new Vector3(0.5f, -0.5f, 0f);
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
            var cliffTile = AssetDatabase.LoadAssetAtPath<TileBase>(IslandArtBuilder.CliffTilePath(isl.islandKey));
            if (cliffTile != null && cliff.Count > 0)
            {
                var pos = new List<Vector3Int>(cliff.Count);
                foreach (var c in cliff) pos.Add(new Vector3Int(c.x, c.y, 0));
                var arr = new TileBase[pos.Count];
                for (int i = 0; i < arr.Length; i++) arr[i] = cliffTile;
                Map("Rim").SetTiles(pos.ToArray(), arr);
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
