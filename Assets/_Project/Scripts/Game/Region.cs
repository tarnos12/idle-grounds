using UnityEngine;
using UnityEngine.Tilemaps;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One 93x93 play region. 1 cell = 1 world unit, +y up. The GameObject sits at the region's
    /// top-left corner; region row r maps to y = origin.y - r, column c to x = origin.x + c.
    /// </summary>
    [ExecuteAlways]
    public class Region : MonoBehaviour
    {
        public const int Cells = 93;
        public const int Gap = 5;
        public const int Stride = Cells + Gap;
        public const int Margin = 10;

        public string regionKey;
        public int rx;
        public int ry;
        public bool unlocked;
        public Tilemap tilemap;
        public GameObject veil;

        public Vector3 Origin => transform.position;

        /// <summary>World rect of the play area (x,y = bottom-left).</summary>
        public Rect WorldRect
        {
            get { var o = Origin; return new Rect(o.x, o.y - Cells, Cells, Cells); }
        }

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
