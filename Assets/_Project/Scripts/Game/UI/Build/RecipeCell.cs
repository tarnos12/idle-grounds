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

        // delivered cell art (ui_recipe_cell / _active, 58 art px, fixed size at 2 UI px per art px)
        Sprite sNormal, sActive;
        bool artLooked, art;
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

        void LookupArt()
        {
            artLooked = true;
            var runner = GameRunner.Instance;
            var db = runner != null ? runner.Database : null;
            if (db == null) return;
            sNormal = db.UiSprite("ui_recipe_cell"); sActive = db.UiSprite("ui_recipe_cell_active");
            art = sNormal != null && sActive != null;
            if (!art) return;
            var img = (Image)button.targetGraphic;
            img.type = Image.Type.Simple;
            border.enabled = false; glow.enabled = false;
            var le = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            float px = sNormal.rect.width * UiSkin.UiPxPerArtPx;     // 116 UI px: the picker grid cell is resized to match
            le.preferredWidth = le.preferredHeight = px;
            var grid = transform.parent != null ? transform.parent.GetComponent<GridLayoutGroup>() : null;
            if (grid != null) grid.cellSize = new Vector2(px, px);
            var irt = icon.rectTransform;
            irt.sizeDelta = new Vector2(64f, 64f);     // 32 art px icon
        }

        void Style()
        {
            if (!artLooked) LookupArt();
            if (art)
            {
                var img = (Image)button.targetGraphic;
                var sp = active ? sActive : sNormal;
                if (img.sprite != sp) img.sprite = sp;
                var c = active || hover ? Color.white : new Color(0.86f, 0.86f, 0.86f, 1f);   // binary-alpha art: hover = full brightness
                if (img.color != c) img.color = c;
                return;
            }
            border.effectColor = active ? UiPalette.Gold : hover ? UiPalette.Accent : UiPalette.Line;
            border.effectDistance = active || hover ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            glow.enabled = active;
        }

        public void OnPointerEnter(PointerEventData e) { hover = true; Style(); picker?.Hover(Index); }
        public void OnPointerExit(PointerEventData e) { hover = false; Style(); picker?.Hover(-1); }
    }
}
