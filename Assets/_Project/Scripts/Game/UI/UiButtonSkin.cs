using IdleGrounds.Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    public enum UiButtonVariant { Standard, Primary, Close }
    public enum UiToggleState { Off, On, Danger }

    /// <summary>
    /// Swaps a placeholder Button for delivered art (db.ui): Standard = ui_btn_*, Primary = ui_btn_primary_*, Close = ui_btn_close (4-frame strip,
    /// fixed 2x size, the art already shows the X so the label is hidden). Sliced variants render 1 art px as 2 UI px (shrinking, min 1x, when the
    /// button is too small for the borders). Transition becomes SpriteSwap (selected = normal so a clicked button doesn't stay "hover").
    /// No art for the variant -> nothing changes (placeholder stays). <see cref="SetToggle"/> swaps the resting sprite for on/danger toggles.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public class UiButtonSkin : MonoBehaviour
    {
        public const float CloseSize = 40f;   // 20 art px x 2

        [SerializeField] UiButtonVariant variant = UiButtonVariant.Standard;
        public UiButtonVariant Variant { get => variant; set => variant = value; }

        bool applied;
        UiToggleState toggle;
        Sprite normal, on, danger, hover, pressed, disabled;
        float lastMult = -1f;

        public bool HasArt => applied;

        public static UiButtonSkin Attach(GameObject go, UiButtonVariant v)
        {
            var s = go.GetComponent<UiButtonSkin>();
            if (s == null) s = go.AddComponent<UiButtonSkin>();
            s.variant = v;
            return s;
        }

        void OnEnable() { if (!applied) TryApply(); }
        void Start() { if (!applied) TryApply(); }

        void TryApply()
        {
            var runner = GameRunner.Instance;
            if (runner != null && runner.Database != null) applied = Apply(runner.Database);
        }

        void OnRectTransformDimensionsChange()
        {
            if (applied && variant != UiButtonVariant.Close) Refit();
        }

        void Refit()
        {
            var img = GetComponent<Button>().targetGraphic as Image;
            if (img == null || img.sprite == null || img.type != Image.Type.Sliced) return;
            float m = UiSkin.SliceMultiplier(img, img.sprite, UiSkin.UiPxPerArtPx, (RectTransform)transform);
            if (!Mathf.Approximately(m, lastMult)) { lastMult = m; img.pixelsPerUnitMultiplier = m; }
        }

        /// <summary>Returns true when real art was applied.</summary>
        public bool Apply(GameDatabase db)
        {
            var btn = GetComponent<Button>();
            var img = btn != null ? btn.targetGraphic as Image : null;
            if (img == null || db == null) return false;

            Sprite n, h, p, d;
            if (variant == UiButtonVariant.Close)
            {
                var f = db.UiFrames("ui_btn_close");
                if (f == null || f.Length < 4) return false;
                n = f[0]; h = f[1]; p = f[2]; d = f[3];
            }
            else
            {
                string k = variant == UiButtonVariant.Primary ? "ui_btn_primary_" : "ui_btn_";
                n = db.UiSprite(k + "normal"); h = db.UiSprite(k + "hover"); p = db.UiSprite(k + "pressed"); d = db.UiSprite(k + "disabled");
                if (n == null || h == null || p == null || d == null) return false;
            }
            normal = n; hover = h; pressed = p; disabled = d;
            on = db.UiSprite("ui_btn_toggle_on"); danger = db.UiSprite("ui_btn_toggle_danger");

            img.color = Color.white;
            var ol = GetComponent<Outline>();     // placeholder border; the art has its own
            if (ol != null) ol.enabled = false;

            if (variant == UiButtonVariant.Close)
            {
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                var rt = (RectTransform)transform;
                rt.sizeDelta = new Vector2(CloseSize, CloseSize);
                var le = GetComponent<LayoutElement>();
                if (le == null) le = gameObject.AddComponent<LayoutElement>();
                le.minWidth = le.preferredWidth = le.minHeight = le.preferredHeight = CloseSize;
                foreach (var t in GetComponentsInChildren<TMP_Text>(true)) t.enabled = false;   // the art already shows the X
            }
            else
            {
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                lastMult = -1f;
            }
            applied = true;
            ApplySprites(btn, img);
            if (variant != UiButtonVariant.Close) Refit();
            return true;
        }

        /// <summary>Build/Demolish "on" look. Returns false when there is no art (caller keeps the placeholder tint).</summary>
        public bool SetToggle(UiToggleState state)
        {
            if (toggle == state && applied) return applied;
            toggle = state;
            if (!applied) TryApply();
            if (!applied) return false;
            var btn = GetComponent<Button>();
            ApplySprites(btn, btn.targetGraphic as Image);
            return true;
        }

        void ApplySprites(Button btn, Image img)
        {
            Sprite rest = normal, hi = hover;
            if (toggle == UiToggleState.On && on != null) rest = hi = on;
            else if (toggle == UiToggleState.Danger && danger != null) rest = hi = danger;
            if (img.sprite != rest) img.sprite = rest;
            btn.transition = Selectable.Transition.SpriteSwap;
            btn.spriteState = new SpriteState { highlightedSprite = hi, pressedSprite = pressed, selectedSprite = rest, disabledSprite = disabled };
        }
    }
}
