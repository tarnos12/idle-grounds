using System.Globalization;
using IdleGrounds.Game;
using UnityEngine;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Shot acts for the §3.10 auto tiles (art reviews; in memory, nothing is saved):
    ///   autoTilePreview:BIOME@island,dx,dy   a 36 x 24-cell preview Island of BIOME's auto-tile sheets (ground with a hole, overlay
    ///                                       clusters, pond, path, side-face column; AutoTileBuilder.BuildPreview) centred dx, dy
    ///                                       cells from the Island's centre. BIOME "all" = lotus, thunder, ravine, tribulation in a
    ///                                       row, 44 cells apart (lotus at dx).
    /// </summary>
    public static partial class Batch
    {
        static readonly string[] AutoTileBiomes = { "lotus", "thunder", "ravine", "tribulation" };

        static bool AutoTileAct(string name, string arg)
        {
            if (name != "autoTilePreview") return false;
            int at = arg.IndexOf('@');
            string biome = arg.Substring(0, at).Trim();
            var p = arg.Substring(at + 1).Split(',');
            Vector2 c = Vector2.zero;
            foreach (var isl in Object.FindObjectsByType<Island>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (isl.islandKey == p[0].Trim()) { c = isl.Centre; break; }
            c += new Vector2(float.Parse(p[1].Trim(), CultureInfo.InvariantCulture), float.Parse(p[2].Trim(), CultureInfo.InvariantCulture));
            if (biome == "all")
                for (int i = 0; i < AutoTileBiomes.Length; i++) AutoTileBuilder.BuildPreview(AutoTileBiomes[i], c + new Vector2(44f * i, 0f));
            else AutoTileBuilder.BuildPreview(biome, c);
            return true;
        }
    }
}
