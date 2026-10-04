using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// World floater (§5): optional icon + bold text, life 850 ms — alpha 0→1 in 60 ms, hold to 400 ms,
    /// linear fade to 850 ms; rises 30 world px. Pooled by <see cref="FxService"/>.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        public const float LifeMs = 850f, RisePx = 30f, IconPx = 15f;

        [SerializeField] TextMeshPro text;
        [SerializeField] SpriteRenderer icon;

        float age, cellPx = 32f;
        Vector3 start;
        Color colour;
        System.Action<FloatingText> onDone;

        public bool Alive { get; private set; }
        public string Text => text.text;

        public void Play(Vector3 worldPos, string msg, Color c, Sprite iconSprite, int cell, System.Action<FloatingText> done)
        {
            onDone = done;
            age = 0f; Alive = true; cellPx = cell;
            start = worldPos; colour = c;
            text.text = msg;
            float iconUnits = IconPx / cell;
            if (iconSprite != null)
            {
                icon.gameObject.SetActive(true);
                icon.sprite = iconSprite;
                float k = iconUnits / Mathf.Max(0.0001f, iconSprite.bounds.size.x);
                icon.transform.localScale = new Vector3(k, k, 1f);
                icon.transform.localPosition = Vector3.zero;
                // text starts at icon x + 0.62 * icon size
                text.alignment = TextAlignmentOptions.Left;
                text.rectTransform.pivot = new Vector2(0f, 0.5f);
                text.transform.localPosition = new Vector3(0.62f * iconUnits, 0f, 0f);
            }
            else
            {
                icon.gameObject.SetActive(false);
                text.alignment = TextAlignmentOptions.Center;
                text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                text.transform.localPosition = Vector3.zero;
            }
            transform.position = start;
            Apply(0f);
        }

        void Apply(float a)
        {
            text.color = new Color(colour.r, colour.g, colour.b, a);
            if (icon.gameObject.activeSelf) icon.color = new Color(1f, 1f, 1f, a);
        }

        void Update()
        {
            if (!Alive) return;
            age += Time.unscaledDeltaTime * 1000f;
            float a = age < 60f ? age / 60f : age < 400f ? 1f : Mathf.Clamp01(1f - (age - 400f) / (LifeMs - 400f));
            transform.position = start + new Vector3(0f, Mathf.Min(age / LifeMs, 1f) * RisePx / cellPx, 0f);
            Apply(a);
            if (age >= LifeMs) { Alive = false; onDone?.Invoke(this); }
        }
    }
}
