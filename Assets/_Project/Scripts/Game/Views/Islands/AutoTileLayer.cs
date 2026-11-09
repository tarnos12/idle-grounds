using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// ART-SPEC §3.10.3 hand-painting hook: put this on a hidden data Tilemap named <c>Auto_&lt;terrain&gt;_Data</c> (any marker tile,
    /// TilemapRenderer off). <c>Idle Grounds/World/Rebuild Auto Tiles</c> (and the dressing rebuild) fills its sibling display
    /// Tilemap <c>Auto_&lt;terrain&gt;</c> at local (-0.5, -0.5) from the painted cells with the
    /// <c>island_&lt;biome&gt;_auto_&lt;terrain&gt;_128x160</c> sheet. Keep data maps outside the Island's <c>Dressing</c> child
    /// (the dressing rebuild recreates that one).
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Tilemaps.Tilemap))]
    public class AutoTileLayer : MonoBehaviour
    {
        public string biome = "lotus";
        public string terrain = "grass";
        /// <summary>Sorting order of the display map on the Ground layer; -1 = the contract default for the terrain.</summary>
        public int sortingOrder = -1;
    }
}
