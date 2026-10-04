using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>A UI row: optional icon + (rich) text. Used by the quest panel / milestone tracker.</summary>
    public class UiIconLine : MonoBehaviour
    {
        public Image icon;
        public TextMeshProUGUI text;

        public void Set(Sprite s, string t, Color c)
        {
            if (icon != null)
            {
                bool on = s != null;
                if (icon.gameObject.activeSelf != on) icon.gameObject.SetActive(on);
                if (on && icon.sprite != s) icon.sprite = s;
            }
            if (text != null)
            {
                t = UiText.StripEmoji(t);
                if (text.text != t) text.text = t;
                text.color = c;
            }
        }
    }
}
