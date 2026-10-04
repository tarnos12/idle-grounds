using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One recipe-picker cell (spec §4.3): 58x58 CSS (87 px) showing the recipe's output icon; active =
    /// gold border + glow, hover = accent border; a same-output sibling gets a small badge with the
    /// input item that differs.
    /// </summary>
    public class RecipeCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] Button button;
        [SerializeField] Outline border;
        [SerializeField] Outline glow;
        [SerializeField] Image icon;
        [SerializeField] Image badge;

        RecipePickerView picker;
        bool active, hover;
        public int Index { get; private set; }
        public Button Button => button;

        public void Bind(RecipePickerView p, int index, Sprite output, Sprite badgeSprite, bool isActive)
        {
            picker = p; Index = index; active = isActive; hover = false;
            name = "Recipe" + index;
            icon.sprite = output;
            badge.gameObject.SetActive(badgeSprite != null);
            badge.sprite = badgeSprite;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => picker.Select(Index));
            Style();
        }

        public void SetActive(bool a) { if (a != active) { active = a; Style(); } }

        void Style()
        {
            border.effectColor = active ? UiPalette.Gold : hover ? UiPalette.Accent : UiPalette.Line;
            border.effectDistance = active || hover ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            glow.enabled = active;
        }

        public void OnPointerEnter(PointerEventData e) { hover = true; Style(); picker?.Hover(Index); }
        public void OnPointerExit(PointerEventData e) { hover = false; Style(); picker?.Hover(-1); }
    }
}
