using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>Two-text row template: help section (title + body) or stats row (label + value).</summary>
    public class UiTextRow : MonoBehaviour
    {
        public TextMeshProUGUI a;
        public TextMeshProUGUI b;

        public void Set(string first, string second)
        {
            if (a != null) a.text = first;
            if (b != null) b.text = second;
        }
    }
}
