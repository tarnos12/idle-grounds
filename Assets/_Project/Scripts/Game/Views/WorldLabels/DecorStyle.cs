using UnityEngine;
using UnityEngine.Tilemaps;

namespace IdleGrounds.Game
{
    /// <summary>
    /// World value key, applied once to every Island (locked ones too):
    /// <list type="bullet">
    /// <item><b>Calmer ground (VP-33, owner pick vp_mood = ground, 2026-10-21):</b> every ground layer of an Island (fill,
    /// coast, macro patches, path, lip, walls, underside) is multiplied by a per-Island tint that lowers the value and the
    /// chroma of its dominant colour, so buildings, resource nodes and dropped items stand out. Multiply can only darken:
    /// Volcano (already near black) is left as it is; the lasting fix is calmer ground-fill art (ART-SPEC requests
    /// island_*_ground_fill_32x32_3f, mid value band, lower chroma).</item>
    /// <item><b>Decor (VP-15):</b> scatter / flower dressing and deco trees are pulled one value step toward the ground
    /// (on top of the ground tint) so decor never reads as something to click; interactable nodes keep full value.</item>
    /// </list>
    /// </summary>
    public class DecorStyle : MonoBehaviour
    {
        /// <summary>Multiply tint for decor: about one palette value step darker, slightly toward the grass hue.</summary>
        public static readonly Color DecorTint = new Color(0.80f, 0.86f, 0.76f, 1f);
        static readonly string[] DecorMaps = { "Scatter", "Flowers" };
        /// <summary>Children that are not ground: the fog veil, its mist and the build-zone overlays keep their colours.</summary>
        static readonly string[] SkipPrefixes = { "Veil", "Mist", "Zone", "NoBuild" };

        /// <summary>
        /// Per-Island ground tint (multiply). Targets from the dominant ground colour in the 2026-10-21 shots: Center grass
        /// (134,179,74) to ~(106,134,72); Farm sand (217,164,92) to ~(176,139,92); Mine slate (74,80,104) to ~(70,74,88);
        /// Fishing (183,188,205) to ~(157,160,170); Grove teal (31,107,87) to ~(31,90,80); Celestial marble (232,236,243)
        /// to ~(183,189,204) = P06. Volcano is already darker than the target band.
        /// </summary>
        public static Color GroundTint(string island)
        {
            switch (island)
            {
                case "center": return new Color(0.79f, 0.75f, 0.97f, 1f);
                case "farm": return new Color(0.81f, 0.85f, 1.00f, 1f);
                case "mine": return new Color(0.95f, 0.93f, 0.85f, 1f);
                case "fishing": return new Color(0.86f, 0.85f, 0.83f, 1f);
                case "grove": return new Color(1.00f, 0.84f, 0.92f, 1f);
                case "celestial": return new Color(0.79f, 0.80f, 0.84f, 1f);
                default: return Color.white;      // volcano + Islands without a tuned key
            }
        }

        /// <summary>Decor tint on <paramref name="island"/> (decor tint x ground tint).</summary>
        public static Color DecorTintOn(string island) => DecorTint * GroundTint(island);

        bool done;
        int tries;

        void LateUpdate()
        {
            if (done || ++tries > 600) return;
            var islands = FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (islands.Length == 0) return;
            foreach (var isl in islands) Apply(isl);
            done = true;
        }

        static bool Skipped(Transform t, Transform root)
        {
            for (var p = t; p != null && p != root; p = p.parent)
                foreach (var pre in SkipPrefixes) if (p.name.StartsWith(pre)) return true;
            return false;
        }

        static Color Grey(Color c) { float v = (c.r + c.g + c.b) / 3f; return new Color(v, v, v, 1f); }

        static Color Mul(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a);

        /// <summary>Multiply one Island's ground and decor layers by its tints (call once per Island).</summary>
        public static void Apply(Island isl)
        {
            var ground = GroundTint(isl.islandKey);
            foreach (var tm in isl.GetComponentsInChildren<Tilemap>(true))
            {
                if (Skipped(tm.transform, isl.transform)) continue;
                bool decor = System.Array.IndexOf(DecorMaps, tm.gameObject.name) >= 0;
                // the stone path keeps its hue (a green-compensating tint turned it violet): value step only
                var tint = tm.gameObject.name == "Path" ? Grey(ground) : decor ? DecorTint * ground : ground;
                tm.color = Mul(tm.color, tint);
            }
            foreach (var sr in isl.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.sortingLayerName != "Ground" || Skipped(sr.transform, isl.transform)) continue;
                sr.color = Mul(sr.color, ground);
            }
        }
    }
}
