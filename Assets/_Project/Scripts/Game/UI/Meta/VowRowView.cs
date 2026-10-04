using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>Ascend dialog vow checkbox row: toggle + icon + "Name — desc ✓ marked".</summary>
    public class VowRowView : MonoBehaviour
    {
        public Toggle toggle;
        public Image icon;
        public TextMeshProUGUI label;
        [HideInInspector] public string vowId;
    }
}
