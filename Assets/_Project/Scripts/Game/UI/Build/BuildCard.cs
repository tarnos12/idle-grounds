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
        [Tooltip("Quest / milestone target: 🎯 badge (top-left) + a soft gold glow ring (box-shadow 0 0 0 2px rgba(251,191,36,.28)).")]
        [SerializeField] GameObject targetBadge;
        [SerializeField] Outline targetGlow;

        // delivered card art (ui_card_*): resolved once per card, then only the sprite is swapped per state
        Sprite sNormal, sAfford, sDim, sTarget;
        bool artLooked, art;
        Sprite shownCard;
        TextMeshProUGUI newLabel;

        readonly List<UiIconCount> costs = new List<UiIconCount>();
        BuildMenuView menu;

        public string Type { get; private set; }
        public bool Locked { get; private set; }
        public bool IsNew { get; private set; }
        public bool Affordable { get; private set; }
        public bool IsTarget { get; private set; }
        public string Tooltip { get; private set; }
        public Button Button => button;

        void Awake() { if (costTemplate != null) costTemplate.gameObject.SetActive(false); }

        public void Bind(BuildMenuView m, BuildingDef def, Sprite iconSprite, SpriteCache sprites,
            bool isNew, bool affordable, string lockedReason, string tooltip, bool target = false)
        {
            menu = m; Type = def.key; IsNew = isNew; IsTarget = target && lockedReason == null; Affordable = affordable; Locked = lockedReason != null; Tooltip = tooltip;
            name = "Card_" + def.key;
            icon.sprite = iconSprite;
            if (nameText.enableAutoSizing)
            {
                // the name keeps the body size (22 px on screen) and the card grows to fit it: auto-size drew 10-13 px names at
                // 1280x720, under the 12 px floor (UI QA batch B #7)
                nameText.enableAutoSizing = false;
                nameText.fontSize = 19.5f;
                nameText.textWrappingMode = TextWrappingModes.NoWrap;
            }
            nameText.text = def.name;
            newBadge.SetActive(isNew && !Locked);
            if (art && newLabel != null && badgeFontVersion != UiFont.Version) FitBadge();
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
            if (!ApplyArt())
            border.effectColor = Locked ? UiPalette.Line : IsTarget || isNew ? UiPalette.Gold : affordable ? UiPalette.Accent : UiPalette.Line;
            if (targetBadge != null) targetBadge.SetActive(IsTarget);
            if (art && targetGlow != null) targetGlow.enabled = false;
            if (targetGlow != null && !art) targetGlow.enabled = IsTarget;
            group.alpha = Locked ? 0.45f : IsTarget || isNew || affordable ? 1f : 0.62f;
            button.interactable = !Locked;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => menu.CardClicked(this));
        }

        void LookupArt()
        {
            artLooked = true;
            var runner = GameRunner.Instance;
            var db = runner != null ? runner.Database : null;
            if (db == null) return;
            sNormal = db.UiSprite("ui_card_normal"); sAfford = db.UiSprite("ui_card_affordable");
            sDim = db.UiSprite("ui_card_dim"); sTarget = db.UiSprite("ui_card_target");
            art = sNormal != null && sAfford != null && sDim != null && sTarget != null;
            if (!art) return;
            if (border != null) border.enabled = false;
            if (newBadge != null)
            {
                newLabel = newBadge.GetComponentInChildren<TextMeshProUGUI>(true);
                UiSkin.Attach(newBadge, "ui_pill_gold", fit: true, uiPerArt: 1f).Apply(db);
                var brt = (RectTransform)newBadge.transform;
                brt.sizeDelta = new Vector2(56f, 26f);    // 1x pill: 8 px caps around the label
                if (newLabel != null)
                {
                    newLabel.color = UiPalette.Gold;       // the pill art is dark with a gold rim
                    // the word at the body text size (22 px on screen, plain cut: no outline material, no fake bold); the old
                    // 14 px outlined label rendered ~8 px and read "DEW" (UI QA batch B #7)
                    if (newLabel.font != null) newLabel.fontSharedMaterial = newLabel.font.material;
                    newLabel.fontStyle = FontStyles.Normal;
                    newLabel.enableAutoSizing = false;
                    newLabel.fontSize = UiFont.PixelSize;
                    FitBadge();
                    // centred on the card's top edge, so it never covers the name; the strip leaves room above the cards
                    brt.pivot = new Vector2(1f, 0.5f);
                    brt.anchoredPosition = new Vector2(-8f, 0f);
                    if (transform.parent != null && transform.parent.TryGetComponent<HorizontalLayoutGroup>(out var strip) && strip.padding.top < 16)
                        strip.padding = new RectOffset(strip.padding.left, strip.padding.right, 16, strip.padding.bottom);
                }
            }
        }

        int badgeFontVersion = -1;

        /// <summary>Measures the "New" word in the current Text font (the font setting can change at any time) and sizes the pill around it.</summary>
        void FitBadge()
        {
            if (newLabel == null) return;
            var brt = (RectTransform)newBadge.transform;
            badgeFontVersion = UiFont.Version;
            newLabel.text = "New";
            newLabel.textWrappingMode = TextWrappingModes.NoWrap;
            newLabel.overflowMode = TextOverflowModes.Overflow;
            UiFont.Apply(newLabel);
            var pref = newLabel.GetPreferredValues("New", 9999f, 9999f);
            // tall line boxes (Jersey 10, Pixelify at 28 px) made a 50 px badge that hid the top half of the card name: cap the height
            brt.sizeDelta = new Vector2(Mathf.Ceil(pref.x + 20f), Mathf.Clamp(Mathf.Ceil(pref.y + 4f), 26f, 32f));
            newLabel.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>Swaps the card background by state (target / locked-or-unaffordable dim / affordable / new+unaffordable normal). False = no art, placeholder styling applies.</summary>
        bool ApplyArt()
        {
            if (!artLooked) LookupArt();
            if (!art) return false;
            var sp = IsTarget ? sTarget : Locked ? sDim : Affordable ? sAfford : IsNew ? sNormal : sDim;
            if (sp != shownCard)
            {
                shownCard = sp;
                UiSkin.ApplySprite(background, sp, UiSkin.UiPxPerArtPx, null, Color.white);
            }
            return true;
        }

        public void OnPointerEnter(PointerEventData e) { if (menu != null) menu.CardHovered(this, true); }
        public void OnPointerExit(PointerEventData e) { if (menu != null) menu.CardHovered(this, false); }
    }
}
