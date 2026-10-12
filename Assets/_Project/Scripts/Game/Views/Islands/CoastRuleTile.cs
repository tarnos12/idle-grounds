using UnityEngine;
using UnityEngine.Tilemaps;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The Island coast's 47-blob Rule Tile, plus its 45° diagonal coast tiles (<see cref="kin"/>: half-cell
    /// triangles, one per facing). A kin tile counts as "This" for the blob rules, so a full coast cell beside a
    /// diagonal draws no rim on the side it shares with the triangle's land half (a diagonal leg is always land).
    /// Built and assigned by IslandArtBuilder / IslandDressingBuilder; the coast itself is painted by IslandCoastBuilder.
    /// </summary>
    [CreateAssetMenu(menuName = "Idle Grounds/Coast Rule Tile")]
    public class CoastRuleTile : RuleTile
    {
        [Tooltip("Tiles that count as this tile for the neighbour rules (the diagonal coast triangles).")]
        public TileBase[] kin = new TileBase[0];

        public override bool RuleMatch(int neighbor, TileBase other)
        {
            if (other is CoastShapeTile) other = this;
            else if (other != null && other != this && kin != null && System.Array.IndexOf(kin, other) >= 0) other = this;
            return base.RuleMatch(neighbor, other);
        }
    }
}
