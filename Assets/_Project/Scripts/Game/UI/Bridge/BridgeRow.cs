using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>One pairing candidate in the <see cref="BridgePanelView"/>: Island icon + name + sky distance, "Send to" / "Receive from".</summary>
    public class BridgeRow : MonoBehaviour
    {
        public Image icon;
        public TextMeshProUGUI label;
        public Button sendButton;
        public Button receiveButton;

        public string Island { get; set; }
        public int BridgeId { get; set; }
    }
}
