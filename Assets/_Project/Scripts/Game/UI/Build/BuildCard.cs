using System.Collections.Generic;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One build-menu card (spec §4.2): icon, name 13/700, cost "qty icon …" 11 gold, "new" pill.
    /// Rank styling: new = gold pill, affordable = green border, rest = opacity .62; a locked card
    /// (optional) shows its unlock reason instead of the cost.
    /// </summary>
    public class BuildCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] Button button;
        [SerializeField] Image background;
        [SerializeField] Outline border;
        [SerializeField] Image icon;
        [SerializeField] TextMeshProUGUI nameText;
        [SerializeField] RectTransform costRow;
        [SerializeField] UiIconCount costTemplate;
        [SerializeField] TextMeshProUGUI lockedText;
        [SerializeField] GameObject newBadge;
        [SerializeField] CanvasGroup group;

        readonly List<UiIconCount> costs = new List<UiIconCount>();
        BuildMenuView menu;

        public string Type { get; private set; }
        public bool Locked { get; private set; }
        public bool IsNew { get; private set; }
        public bool Affordable { get; private set; }
        public string Tooltip { get; private set; }
        public Button Button => button;

        void Awake() { if (costTemplate != null) costTemplate.gameObject.SetActive(false); }

        public void Bind(BuildMenuView m, BuildingDef def, Sprite iconSprite, SpriteCache sprites,
            bool isNew, bool affordable, string lockedReason, string tooltip)
        {
            menu = m; Type = def.key; IsNew = isNew; Affordable = affordable; Locked = lockedReason != null; Tooltip = tooltip;
            name = "Card_" + def.key;
            icon.sprite = iconSprite;
            nameText.text = def.name;
            newBadge.SetActive(isNew && !Locked);
            lockedText.gameObject.SetActive(Locked);
            lockedText.text = lockedReason ?? "";
            costRow.gameObject.SetActive(!Locked);
            int n = Locked ? 0 : def.cost.Count;
            while (costs.Count < n)
            {
                var c = Instantiate(costTemplate, costRow);
                costs.Add(c);
            }
            for (int i = 0; i < costs.Count; i++)
            {
                bool on = i < n;
                costs[i].gameObject.SetActive(on);
                if (on) costs[i].Set(sprites.Item(def.cost[i].item), def.cost[i].qty.ToString());
            }
            border.effectColor = Locked ? UiPalette.Line : isNew ? UiPalette.Gold : affordable ? UiPalette.Accent : UiPalette.Line;
            group.alpha = Locked ? 0.45f : isNew || affordable ? 1f : 0.62f;
            button.interactable = !Locked;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => menu.CardClicked(this));
        }

        public void OnPointerEnter(PointerEventData e) { if (menu != null) menu.CardHovered(this, true); }
        public void OnPointerExit(PointerEventData e) { if (menu != null) menu.CardHovered(this, false); }
    }
}
