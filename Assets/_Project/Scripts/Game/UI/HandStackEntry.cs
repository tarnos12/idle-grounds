using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>One "qty icon" entry of the hand chip.</summary>
    public class HandStackEntry : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI qty;
        [SerializeField] Image icon;
        [SerializeField] LayoutElement iconLayout;

        public void Set(int count, Sprite sprite, Color colour, float iconSize, bool front)
        {
            qty.text = count.ToString();
            qty.color = colour;
            qty.fontStyle = front ? FontStyles.Bold : FontStyles.Normal;
            icon.sprite = sprite;
            if (iconLayout != null) { iconLayout.preferredWidth = iconSize; iconLayout.preferredHeight = iconSize; }
        }
    }
}
