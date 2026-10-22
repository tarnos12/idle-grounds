using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The common one-line (or multi-line) world tag in the shared style: [status shape] [item icon] text
    /// [trailing icon] on a plate. Owned by a <see cref="WorldLabel"/> (same GameObject); the layout is
    /// rebuilt only when something shown changes, so per-frame calls with unchanged values cost a few
    /// compares and allocate nothing. Used by storehouse / formation / generator counters, "No fuel",
    /// "Select an upgrade", the Heavenly Gate tag, placement reasons and the demolish preview.
    /// </summary>
    [RequireComponent(typeof(WorldLabel))]
    public class PlatedText : MonoBehaviour
    {
        public enum Pivot { Above, Below, Centre }

        public WorldLabel Label { get; private set; }
        public TextMeshPro Text { get; private set; }
        SpriteRenderer plate, shape, icon, trail;
        string lastText; Sprite lastShape, lastIcon, lastTrail; Color lastColour; float lastPx; Pivot pivot; bool dirty = true;
        float iconPx = 12f;

        /// <summary>Current text ("" when hidden).</summary>
        public string Shown => Text != null ? Text.text : null;

        public static PlatedText Create(Transform parent, string name, float px = WorldLabel.TextPx, Pivot pivot = Pivot.Below)
        {
            var l = WorldLabel.Create(parent, name);
            var p = l.gameObject.AddComponent<PlatedText>();
            p.Label = l;
            p.plate = WorldLabel.NewPlate(l.content);
            p.Text = WorldLabel.NewText(l.content, "Text", px, WorldLabel.TextColour);
            p.lastPx = px; p.pivot = pivot;
            return p;
        }

        public void SetIconPx(float px) { if (iconPx != px) { iconPx = px; dirty = true; } }
        public void SetPivot(Pivot p) { if (pivot != p) { pivot = p; dirty = true; } }

        /// <summary>Show the tag. Any sprite may be null. Cheap when nothing changed.</summary>
        int lastFontVersion = -1;

        public void Set(string text, Color colour, Sprite shapeSprite = null, Sprite iconSprite = null, Sprite trailSprite = null)
        {
            text ??= "";
            if (lastFontVersion != UiFont.Version) { lastFontVersion = UiFont.Version; dirty = true; }      // Text font setting: measure again in the new face
            if (!dirty && text == lastText && colour == lastColour && shapeSprite == lastShape && iconSprite == lastIcon && trailSprite == lastTrail) return;
            bool layout = dirty || text != lastText || shapeSprite != lastShape || iconSprite != lastIcon || trailSprite != lastTrail;
            dirty = false;
            lastText = text; lastColour = colour; lastShape = shapeSprite; lastIcon = iconSprite; lastTrail = trailSprite;
            if (Text.text != text) Text.text = text;
            Text.color = colour;
            if (layout) Relayout();
        }

        SpriteRenderer Ensure(ref SpriteRenderer sr, string name, int order)
        {
            if (sr == null) sr = WorldLabel.NewSprite(Label.content, name, null, order);
            return sr;
        }

        void Relayout()
        {
            float gap = ViewKit.U(3f), pad = ViewKit.U(4f), sw = ViewKit.U(11f), iw = ViewKit.U(iconPx);
            bool hasText = lastText.Length > 0;
            var pref = hasText ? Text.GetPreferredValues(lastText) : Vector2.zero;
            float tw = pref.x, th = Mathf.Max(ViewKit.U(lastPx * 1.15f), pref.y);
            float total = 0f; int parts = 0;
            if (lastShape != null) { total += sw; parts++; }
            if (lastIcon != null) { total += iw; parts++; }
            if (hasText) { total += tw; parts++; }
            if (lastTrail != null) { total += ViewKit.U(10f); parts++; }
            total += Mathf.Max(0, parts - 1) * gap;
            float h = Mathf.Max(th, lastIcon != null ? iw : 0f, lastShape != null ? sw : 0f) + 2f * ViewKit.U(2.5f);
            float w = total + 2f * pad;
            float cy = pivot == Pivot.Above ? h * 0.5f : pivot == Pivot.Below ? -h * 0.5f : 0f;
            float x = -total * 0.5f;
            if (lastShape != null) { Ensure(ref shape, "Shape", WorldLabel.OrderKeep); Place(shape, lastShape, x + sw * 0.5f, cy, 0f); x += sw + gap; }
            else if (shape != null) ViewKit.Show(shape, false);
            if (lastIcon != null) { Ensure(ref icon, "Icon", WorldLabel.OrderContent); Place(icon, lastIcon, x + iw * 0.5f, cy, iconPx); x += iw + gap; }
            else if (icon != null) ViewKit.Show(icon, false);
            ViewKit.Show(Text, hasText);
            if (hasText)
            {
                Text.rectTransform.sizeDelta = new Vector2(tw + ViewKit.U(2f), th);
                Text.transform.localPosition = new Vector3(x + tw * 0.5f, cy, 0f);
                x += tw + gap;
            }
            if (lastTrail != null) { Ensure(ref trail, "Trail", WorldLabel.OrderContent); Place(trail, lastTrail, x + ViewKit.U(5f), cy, 9f); }
            else if (trail != null) ViewKit.Show(trail, false);
            WorldLabel.SizePlate(plate, 0f, cy, w, h);
            Label.SetBounds(-w * 0.5f, cy - h * 0.5f, w, h);
        }

        static void Place(SpriteRenderer sr, Sprite s, float x, float y, float fitPx)
        {
            ViewKit.Show(sr, true);
            if (fitPx > 0f) ViewKit.Fit(sr, s, fitPx);
            else { sr.sprite = s; sr.transform.localScale = Vector3.one; }   // pixel sprites: native size (PPU 32)
            sr.transform.localPosition = new Vector3(x, y, 0f);
        }

        /// <summary>Hide everything (the label stops taking part in the layout).</summary>
        public void Hide() { ViewKit.Show(Label, false); }
        public void Show(bool on) { ViewKit.Show(Label, on); }
    }
}
