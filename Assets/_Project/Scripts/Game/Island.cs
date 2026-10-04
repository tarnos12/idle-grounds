using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One 93×93-cell floating Island (CONTEXT.md). 1 cell = 1 world unit, +y up. The GameObject sits at
    /// the Island's top-left corner; Island row r maps to y = origin.y - r, column c to x = origin.x + c.
    /// The SCENE is the authority for where Islands float (ADR 0003): at boot <see cref="GameRunner"/>
    /// reads every Island's transform and hands the offsets to the sim (<c>Simulation.SetIslandOffsets</c>),
    /// so moving an Island in the Scene view moves it in-game (bridges' sky distance included).
    /// </summary>
    [ExecuteAlways]
    public class Island : MonoBehaviour
    {
        public const int Cells = 93;

        [Tooltip("Sim area key (farm, center, mine, grove, fishing, volcano, celestial).")]
        [FormerlySerializedAs("regionKey")]
        public string islandKey;
        public bool unlocked;
        public Tilemap tilemap;
        public GameObject veil;

        public Vector3 Origin => transform.position;

        /// <summary>Sim world offset in px (top-left corner, x right, y down), snapped to whole px.</summary>
        public (double x, double y) OffsetPx(int cellPx)
        {
            var o = Origin;
            return (System.Math.Round(o.x * cellPx), System.Math.Round(-o.y * cellPx));
        }

        /// <summary>World rect of the play area (x,y = bottom-left).</summary>
        public Rect WorldRect
        {
            get { var o = Origin; return new Rect(o.x, o.y - Cells, Cells, Cells); }
        }

        public Vector2 Centre => new Vector2(Origin.x + Cells * 0.5f, Origin.y - Cells * 0.5f);

        /// <summary>World position of the centre of the cell (row, col).</summary>
        public Vector3 CellToWorld(int row, int col)
        {
            var o = Origin;
            return new Vector3(o.x + col + 0.5f, o.y - row - 0.5f, 0f);
        }

        /// <summary>Cell (x = col, y = row) under a world position; may be outside 0..92.</summary>
        public Vector2Int WorldToCell(Vector3 world)
        {
            var o = Origin;
            return new Vector2Int(Mathf.FloorToInt(world.x - o.x), Mathf.FloorToInt(o.y - world.y));
        }

        public bool Contains(Vector3 world)
        {
            var c = WorldToCell(world);
            return c.x >= 0 && c.x < Cells && c.y >= 0 && c.y < Cells;
        }

        public void SetUnlocked(bool value)
        {
            unlocked = value;
            if (veil != null) veil.SetActive(!unlocked);
        }

        void OnValidate()
        {
            if (veil != null) veil.SetActive(!unlocked);
        }
    }
}
