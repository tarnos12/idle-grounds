using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>One Ascension Shrine card: icon, name + gold "lvl/max", first-pick pill, desc, effect line, buy button.</summary>
    public class PerkCardView : MonoBehaviour
    {
        public Image background;
        public Outline border;
        public CanvasGroup group;
        public Button cardButton;
        public Image icon;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI levelText;
        public GameObject pickTag;
        public TextMeshProUGUI descText;
        public TextMeshProUGUI fxText;
        public Button buyButton;
        public TextMeshProUGUI buyLabel;

        [HideInInspector] public string perkId;
    }
}
