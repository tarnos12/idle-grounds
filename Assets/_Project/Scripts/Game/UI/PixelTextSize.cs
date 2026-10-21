using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>The authored font size of a pixel-font text before <see cref="UiFont"/> snapped it (copied with the object, so
    /// clones of a snapped template still know their original size), and the size UiFont set.</summary>
    [DisallowMultipleComponent]
    public sealed class PixelTextSize : MonoBehaviour
    {
        public float authored, set = -1f;
    }
}
