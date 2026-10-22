using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>What a text was authored with before <see cref="UiFont"/> changed it (copied with the object, so clones of a
    /// switched template still know their original size, bold intent and styled material), and what UiFont set last (a
    /// different value now means game code changed it since, and that becomes the new authored value).</summary>
    [DisallowMultipleComponent]
    public sealed class PixelTextSize : MonoBehaviour
    {
        public float authored, set = -1f;
        /// <summary>The text asked for bold (UiFont draws it with a bold cut or none: no synthetic bold on pixel fonts).</summary>
        public bool bold;
        /// <summary>The styled (outlined) material it was authored with, or null for the font's plain material.</summary>
        public Material baseMat;
        /// <summary>The material UiFont set last.</summary>
        public Material setMat;
        public bool recorded;
        /// <summary>Auto-size range as authored, and the max UiFont set (scaled to the face: Jersey 10 is drawn smaller per em).</summary>
        public float autoMin, autoMax, autoSetMax = -1f;
    }
}
