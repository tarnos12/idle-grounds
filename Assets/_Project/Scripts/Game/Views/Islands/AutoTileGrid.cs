using System.Collections.Generic;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// ART-SPEC §3.10.3 auto tiles, the dual grid (pure logic, no assets). A terrain is a set of <b>data cells</b>; the display
    /// Tilemap <c>Auto_&lt;terrain&gt;</c> sits half a cell down-left (local (-0.5, -0.5)) so every display cell's 4 corners are the
    /// centres of 4 data cells. Display cell (i, j) (Unity y up) takes NW = data(i-1, j), NE = data(i, j), SW = data(i-1, j-1),
    /// SE = data(i, j-1); Wang index bits NW = 1, NE = 2, SW = 4, SE = 8. Index 0 = no tile, 15 = a fill variant F0-F3.
    /// <para>Sheet (128 x 160, 4 columns x 5 rows of 32 x 32, row 0 at the top): rows 0-3 hold Wang index <see cref="Layout"/>[r, c],
    /// row 4 the fills F0-F3. Sprite slot = r * 4 + c (0..19).</para>
    /// </summary>
    public static class AutoTileGrid
    {
        public const int NW = 1, NE = 2, SW = 4, SE = 8;
        public const int Columns = 4, Rows = 5, TileSize = 32, Fills = 4;
        /// <summary>Sprite slot of fill F0 (row 4, column 0); F0..F3 = slots 16..19.</summary>
        public const int FillSlot0 = 16;

        /// <summary>Wang index of the sheet tile at row r (from the top), column c (rows 0-3).</summary>
        public static readonly int[,] Layout = { { 10, 13, 12, 4 }, { 14, 15, 7, 9 }, { 3, 11, 5, 2 }, { 8, 6, 1, 0 } };

        /// <summary>The corner grid the layout is cut from (rows top-down): tile (r, c) corners = grid[r][c], [r][c+1], [r+1][c], [r+1][c+1].</summary>
        public static readonly string[] CornerGrid = { "01000", "01110", "11101", "00100", "01000" };

        static readonly int[] slotOf = BuildSlots();

        static int[] BuildSlots()
        {
            var s = new int[16];
            for (int i = 0; i < 16; i++) s[i] = -1;
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++) s[Layout[r, c]] = r * Columns + c;
            return s;
        }

        /// <summary>Sheet sprite slot (r * 4 + c) of a Wang index 0..15.</summary>
        public static int SlotOf(int wang) => slotOf[wang & 15];

        /// <summary>Wang index of display cell (i, j) from a data predicate.</summary>
        public static int Index(System.Func<int, int, bool> data, int i, int j) =>
            (data(i - 1, j) ? NW : 0) | (data(i, j) ? NE : 0) | (data(i - 1, j - 1) ? SW : 0) | (data(i, j - 1) ? SE : 0);

        /// <summary>
        /// Every display cell touched by <paramref name="data"/> with its Wang index (1..15; 0 never appears).
        /// Data cell (x, y) is a corner of display cells (x, y) NE, (x+1, y) NW, (x, y+1) SE, (x+1, y+1) SW.
        /// </summary>
        public static Dictionary<Vector2Int, int> Display(ICollection<Vector2Int> data)
        {
            var outIdx = new Dictionary<Vector2Int, int>(data.Count * 2);
            foreach (var d in data)
            {
                Or(outIdx, new Vector2Int(d.x, d.y), NE);
                Or(outIdx, new Vector2Int(d.x + 1, d.y), NW);
                Or(outIdx, new Vector2Int(d.x, d.y + 1), SE);
                Or(outIdx, new Vector2Int(d.x + 1, d.y + 1), SW);
            }
            return outIdx;
        }

        static void Or(Dictionary<Vector2Int, int> m, Vector2Int k, int bit) => m[k] = m.TryGetValue(k, out int v) ? v | bit : bit;

        /// <summary>Sprite slot for display cell (i, j) with Wang index <paramref name="wang"/>: index 15 picks a fill variant
        /// F0-F3 (<see cref="CoastRuleTile.FillVariant"/>, no equal neighbour to the left / below); -1 = no tile.</summary>
        public static int Slot(int wang, int i, int j)
        {
            if (wang <= 0) return -1;
            if (wang == 15) return FillSlot0 + FillVariant(i, j);
            return SlotOf(wang);
        }

        /// <summary>Fill variant 0..3 of display cell (i, j) (the coast fill's picker, so both read the same).</summary>
        public static int FillVariant(int i, int j) => CoastRuleTile.FillVariant(i, j, Fills);

        /// <summary>Side-face strip frame for an Island cell row <paramref name="cellY"/> (rows count down, as the Island's cells):
        /// going down the stacked pieces are 0, 1, 2, 3, 0, 1 ..., and frame k + 1 sits under frame k in the 128-px strip, so
        /// row 127 of the strip flows into row 0 of the next piece: one continuous face.</summary>
        public static int SideFaceFrame(int cellY) => ((cellY % 4) + 4) % 4;

        /// <summary><see cref="SideFaceFrame"/> for a dressing Tilemap cell y (y up; Island row = -y - 1).</summary>
        public static int SideFaceFrameAtTilemapY(int tilemapY) => SideFaceFrame(-tilemapY - 1);

        /// <summary>Morphological opening with a 2 x 2 block: keeps only cells that belong to a fully set 2 x 2 block (no single
        /// cells or one-cell strips: overlay patches are blobs of at least 2 x 2 cells, §3.10.3 "Placement").</summary>
        public static HashSet<Vector2Int> Open2x2(ICollection<Vector2Int> cells)
        {
            var set = cells as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(cells);
            var keep = new HashSet<Vector2Int>();
            foreach (var p in set)
                for (int dx = -1; dx <= 0; dx++)
                    for (int dy = -1; dy <= 0; dy++)
                    {
                        int x = p.x + dx, y = p.y + dy;
                        if (set.Contains(new Vector2Int(x, y)) && set.Contains(new Vector2Int(x + 1, y)) &&
                            set.Contains(new Vector2Int(x, y + 1)) && set.Contains(new Vector2Int(x + 1, y + 1)))
                        {
                            keep.Add(new Vector2Int(x, y)); keep.Add(new Vector2Int(x + 1, y));
                            keep.Add(new Vector2Int(x, y + 1)); keep.Add(new Vector2Int(x + 1, y + 1));
                        }
                    }
            return keep;
        }
    }
}
