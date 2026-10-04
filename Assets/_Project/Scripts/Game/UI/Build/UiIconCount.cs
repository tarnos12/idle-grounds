using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>uGUI "qty icon [label]" entry (build-card cost, recipe detail input rows).</summary>
    public class UiIconCount : MonoBehaviour
    {
        public Image icon;
        public TextMeshProUGUI qty;
        public TextMeshProUGUI label;

        public void Set(Sprite s, string q, string l = null)
        {
            if (icon != null) icon.sprite = s;
            if (qty != null) qty.text = q;
            if (label != null) { label.text = l ?? ""; label.gameObject.SetActive(!string.IsNullOrEmpty(l)); }
        }
    }
}
