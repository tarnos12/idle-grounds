using UnityEngine;
using UnityEngine.Tilemaps;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The Island coast's 47-blob Rule Tile, plus its 45° diagonal coast tiles (<see cref="kin"/>: half-cell
    /// triangles, one per facing). A kin tile counts as "This" for the blob rules, so a full coast cell beside a
    /// diagonal draws no rim on the side it shares with the triangle's land half (a diagonal leg is always land).
    /// Built and assigned by IslandArtBuilder / IslandDressingBuilder; the coast itself is painted by IslandCoastBuilder.
    /// Also used for the play area's ground fill tile.
    /// </summary>
    [CreateAssetMenu(menuName = "Idle Grounds/Coast Rule Tile")]
    public class CoastRuleTile : RuleTile
    {
        [Tooltip("Tiles that count as this tile for the neighbour rules (the diagonal coast triangles).")]
        public TileBase[] kin = new TileBase[0];

        [Tooltip("The ground fill variants: an interior cell picks one by a seeded cell hash (VP-14) instead of the Rule Tile's " +
                 "Perlin random, avoiding the picks of its left and lower neighbours, so no variant forms runs or blocks.")]
        public Sprite[] fill = new Sprite[0];

        public override bool RuleMatch(int neighbor, TileBase other)
        {
            if (other is CoastShapeTile) other = this;
            else if (other != null && other != this && kin != null && System.Array.IndexOf(kin, other) >= 0) other = this;
            return base.RuleMatch(neighbor, other);
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            base.GetTileData(position, tilemap, ref tileData);
            if (fill == null || fill.Length < 2 || tileData.sprite == null || System.Array.IndexOf(fill, tileData.sprite) < 0) return;
            tileData.sprite = fill[FillVariant(position.x, position.y, fill.Length)];
            tileData.transform = Matrix4x4.identity;
        }

        static uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 0x85EBCA77u ^ (uint)y * 0xC2B2AE3Du ^ 0x27D4EB2Fu;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return h;
            }
        }

        /// <summary>Seeded fill variant of a cell: its hash pick, moved off the left / lower neighbours' picks when it repeats one.</summary>
        public static int FillVariant(int x, int y, int n)
        {
            if (n <= 1) return 0;
            int Raw(int a, int b) => (int)(Hash(a, b) % (uint)n);
            int v = Raw(x, y), l = Raw(x - 1, y), d = Raw(x, y - 1);
            if (v != l && v != d) return v;
            int s = (int)((Hash(y, x) >> 7) % (uint)n);
            for (int k = 1; k < n; k++)
            {
                int c = (v + k + s) % n;
                if (c != l && c != d) return c;
            }
            return (v + 1) % n;
        }
    }
}
