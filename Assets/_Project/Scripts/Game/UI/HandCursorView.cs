using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The hand chip (ui-input-render §3.3): follows the pointer at (+14,+14) px and lists every hand
    /// stack as "qty icon" in order — the front stack in accent colour with a bigger icon, the others
    /// muted. Hidden when the hand is empty or the pointer is over UI.
    /// </summary>
    public class HandCursorView : MonoBehaviour
    {
        [SerializeField] GameRunner runner;
        [SerializeField] HandController hand;
        [SerializeField] RectTransform chip;
        [SerializeField] RectTransform entryContainer;
        [SerializeField] HandStackEntry entryTemplate;
        [SerializeField] Vector2 offsetPx = new Vector2(14f, 14f);
        [SerializeField] float frontIcon = 27f, otherIcon = 24f;   // 18 / 16 CSS px at 1.5x

        readonly List<HandStackEntry> entries = new List<HandStackEntry>();
        Canvas canvas;
        RectTransform canvasRect;
        string lastKey;
        float lastScale = 1f;

        public bool ChipVisible => chip != null && chip.gameObject.activeSelf;

        void Awake()
        {
            if (runner == null) runner = GameRunner.Instance;
            if (hand == null) hand = FindFirstObjectByType<HandController>();
            canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? canvas.rootCanvas.GetComponent<RectTransform>() : null;
            if (entryTemplate != null) entryTemplate.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (runner == null || runner.Sim == null || chip == null) return;
            var h = runner.State.hand;
            bool show = h.Count > 0 && hand != null && !hand.PointerOverUI;
            if (chip.gameObject.activeSelf != show) chip.gameObject.SetActive(show);
            if (!show) return;

            string key = Key(h);
            if (key != lastKey) { lastKey = key; Rebuild(h); }

            if (canvasRect != null)
            {
                float f = canvas.rootCanvas.scaleFactor;
                var screen = hand.CursorScreen + new Vector2(offsetPx.x, -offsetPx.y) * f;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
                chip.anchoredPosition = local;
            }
            float s = 1f;
            if (!Juice.ReduceMotion)
            {
                float tp = (Time.unscaledTime - Juice.HandPunchAt) / 0.12f;
                if (tp >= 0f && tp < 1f) s = 1f + 0.12f * Mathf.Sin(tp * Mathf.PI);      // items just arrived
                float tf = (Time.unscaledTime - Juice.HandFullAt) / 0.4f;
                if (tf >= 0f && tf < 1f) s = Mathf.Max(s, 1f + 0.14f * Mathf.Abs(Mathf.Sin(tf * Mathf.PI * 2f)) * (1f - tf));
            }
            if (s != lastScale) { lastScale = s; chip.localScale = new Vector3(s, s, 1f); }
        }

        static string Key(List<Sim.HandStack> h)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var s in h) sb.Append(s.item).Append(':').Append(s.qty).Append(';');
            return sb.ToString();
        }

        void Rebuild(List<Sim.HandStack> h)
        {
            while (entries.Count < h.Count)
            {
                var e = Instantiate(entryTemplate, entryContainer);
                e.name = "Stack" + entries.Count;
                entries.Add(e);
            }
            for (int i = 0; i < entries.Count; i++)
            {
                bool on = i < h.Count;
                entries[i].gameObject.SetActive(on);
                if (!on) continue;
                bool front = i == 0;
                entries[i].Set(h[i].qty, runner.Database.ItemIcon(h[i].item),
                    front ? UiPalette.Accent : UiPalette.Muted, front ? frontIcon : otherIcon, front);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(chip);
        }
    }
}
