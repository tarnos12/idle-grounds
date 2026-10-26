using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Per-Island decor art keys from <c>Assets/_Project/Art/Islands/island-decor.json</c> (ART-SPEC §3.8 snowy Fishing:
    /// snowy pines and snow props replace the green deco trees and bushes). Keys are Art/Incoming keys; a key that is
    /// not delivered falls back to the default art in the builder that asks.
    /// </summary>
    public static class IslandDecor
    {
        public const string Path = IslandArtBuilder.IslandDir + "/island-decor.json";

        [System.Serializable] public class Entry { public string island; public string trees; public string props; }
        [System.Serializable] class FileData { public Entry[] islands; }

        static Entry[] Load()
        {
            if (!File.Exists(Path)) return new Entry[0];
            var d = JsonUtility.FromJson<FileData>(File.ReadAllText(Path));
            return d?.islands ?? new Entry[0];
        }

        /// <summary>Decor keys of one Island (null when it has none).</summary>
        public static Entry For(string island)
        {
            foreach (var e in Load()) if (e != null && e.island == island) return e;
            return null;
        }

        /// <summary>Every decor key named in the file (ArtIntake counts them as consumed).</summary>
        public static IEnumerable<string> AllKeys()
        {
            foreach (var e in Load())
            {
                if (e == null) continue;
                if (!string.IsNullOrEmpty(e.trees)) yield return e.trees;
                if (!string.IsNullOrEmpty(e.props)) yield return e.props;
            }
        }
    }
}
