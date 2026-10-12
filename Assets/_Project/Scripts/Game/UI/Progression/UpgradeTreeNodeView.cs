using IdleGrounds.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// One Altar tree node (ui-input-render §4.7): rounded 52 px square, fill #0d1113 (#171c1f when
    /// hovered + selectable), 3 px border — green selectable, gold maxed, red locked / mystery; alpha
    /// .75 mystery, .6 visible-but-locked and not owned; icon 24 px, "lvl/max" 9 px in the border colour;
    /// mystery = red "?". Hover/selected frame: white, gold when selected and not hovered.
    /// </summary>
    public class UpgradeTreeNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public static readonly Color Fill = UiPalette.Hex("#0d1113"), FillHover = UiPalette.Hex("#171c1f");

        [SerializeField] Image background;
        [SerializeField] Outline border;
        [SerializeField] Image icon;
        [SerializeField] TextMeshProUGUI level;
        [SerializeField] TextMeshProUGUI mystery;
        [Tooltip("Selection frame: an Image BEHIND the background, a few px larger (reads as a ring).")]
        [SerializeField] Image brackets;
        [SerializeField] CanvasGroup group;
        [Tooltip("Lock glyph on locked nodes (never colour alone).")]
        [SerializeField] Image lockBadge;
        [Tooltip("Remaining cost under the selected node.")]
        [SerializeField] TextMeshProUGUI costLabel;

        UpgradeTreeView owner;
        public string Id { get; private set; }
        public UpgradeNodeState State { get; private set; }
        public bool Hovered { get; private set; }
        public bool Mystery { get; private set; }
        public RectTransform Rect => (RectTransform)transform;
        public Color BorderColour => border != null ? border.effectColor : Color.clear;

        public void Init(UpgradeTreeView owner, string id, Sprite sprite)
        {
            this.owner = owner; Id = id;
            name = "Node_" + id;
            icon.sprite = sprite;
        }

        public void Apply(UpgradeNodeState st, bool debug, bool selectedJob, string costUnder = null)
        {
            State = st;
            Mystery = st.tier == UpgradeNodeTier.Mystery && !debug;
            Color c = st.maxed ? UiPalette.Gold : st.selectable && !Mystery ? UiPalette.Accent : UiPalette.Danger;
            // node alpha is baked into the colours (blended onto the board) so the 3 px border never
            // shows through a translucent fill
            float a = Mystery ? 0.75f : (!st.selectable && !st.owned) ? 0.6f : 1f;
            group.alpha = 1f;
            border.effectColor = Mix(c, a);
            background.color = Mix(Hovered && st.selectable && !Mystery ? FillHover : Fill, a);
            icon.color = new Color(1f, 1f, 1f, a);
            mystery.color = Mix(UiPalette.Danger, a);
            c = Mix(c, a);
            ViewKit.Show(icon, !Mystery);
            ViewKit.Show(level, !Mystery);
            ViewKit.Show(mystery, Mystery);
            // state is never colour alone: maxed reads "MAX", locked carries a lock glyph, selected a thick white ring
            string lv = st.maxed ? "MAX" : st.level + "/" + st.max;
            if (level.text != lv) level.text = lv;
            level.color = c;
            bool locked = !Mystery && !st.selectable && !st.maxed;
            if (lockBadge != null) ViewKit.Show(lockBadge, locked);
            bool frame = Hovered || selectedJob;
            ViewKit.Show(brackets, frame);
            if (frame)
            {
                brackets.color = selectedJob ? Color.white : UiPalette.Hex("#9fb3c8");
                var brt = brackets.rectTransform;
                float ring = selectedJob ? 12f : 8f;                         // selected: thicker ring
                brt.offsetMin = new Vector2(-ring, -ring); brt.offsetMax = new Vector2(ring, ring);
            }
            if (costLabel != null)
            {
                bool showCost = selectedJob && !string.IsNullOrEmpty(costUnder);
                ViewKit.Show(costLabel, showCost);
                if (showCost && costLabel.text != costUnder) costLabel.text = costUnder;
            }
        }

        public static readonly Color Board = UiPalette.Hex("#232926");
        static Color Mix(Color c, float a) => Color.Lerp(Board, c, a);

        public void OnPointerEnter(PointerEventData e) { Hovered = true; owner.Hover(this); }
        public void OnPointerExit(PointerEventData e) { Hovered = false; owner.Unhover(this); }
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) owner.Click(this); }
    }
}
