using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>One row of the Stats "Flow" tab: item icon + name, produced / consumed / lost, net per minute.</summary>
    public class FlowRowView : MonoBehaviour
    {
        public Image icon;
        public TextMeshProUGUI nameText, producedText, consumedText, lostText, netText;
    }
}
