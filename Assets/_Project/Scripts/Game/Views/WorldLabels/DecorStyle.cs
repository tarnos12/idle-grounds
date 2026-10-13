using UnityEngine;
using UnityEngine.Tilemaps;

namespace IdleGrounds.Game
{
    /// <summary>
    /// VP-15 (code half): decor must not read as something to click. Scatter / flower dressing and deco
    /// trees are pulled one value step toward the ground (multiply tint, no new colours); interactable
    /// nodes keep full value and get a 1-texel P07 hover rim (<see cref="NodeView"/>). The art half
    /// (outline-free flowers, max 16x16 texels) is the `island_*_flowers_32x32_4f` v2 request.
    /// </summary>
    public class DecorStyle : MonoBehaviour
    {
        /// <summary>Multiply tint for decor: about one palette value step darker, slightly toward the grass hue.</summary>
        public static readonly Color DecorTint = new Color(0.80f, 0.86f, 0.76f, 1f);
        static readonly string[] DecorMaps = { "Scatter", "Flowers" };

        bool done;
        int tries;

        void LateUpdate()
        {
            if (done || ++tries > 600) return;
            var islands = FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (islands.Length == 0) return;
            foreach (var isl in islands)
                foreach (var tm in isl.GetComponentsInChildren<Tilemap>(true))
                    if (System.Array.IndexOf(DecorMaps, tm.gameObject.name) >= 0) tm.color = DecorTint;
            done = true;
        }
    }
}
