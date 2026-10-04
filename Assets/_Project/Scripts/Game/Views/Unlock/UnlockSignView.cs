using System.Collections.Generic;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// A world-space region unlock sign (ui-input-render §4.11 unlock button, as a scene object): sits
    /// in the void gap on the locked region's border facing an unlocked neighbour. Shows the unlock
    /// icon, "Unlock {Region}", the cost line ("qty icon", or "paid/needed icon" once an installment
    /// was paid) and "pay have/need" when the hand covers part of it. States from
    /// <see cref="Simulation.UnlockPayInfo"/>: afford = solid gold border + pulsing glow (1.8 s);
    /// partial = dashed gold, opacity .8; cant = dashed #6b561c, opacity .6 (1 while hovered).
    /// Clicks are routed by <see cref="HandController"/> through <see cref="UnlockSignSync.HitTest"/>.
    /// </summary>
    public class UnlockSignView : MonoBehaviour
    {
        public const float W = 4.6f, H = 3.3f;
        public static readonly Color PanelColour = ViewKit.Rgba(20, 25, 30, 0.86f), CantBorder = UiPalette.Hex("#6b561c");

        [SerializeField] SpriteRenderer panel;
        [SerializeField] EdgeFrame frame;
        [SerializeField] EdgeFrame glow;
        [SerializeField] SpriteRenderer icon;
        [SerializeField] TextMeshPro title;
        [SerializeField] IconRow cost;
        [SerializeField] TextMeshPro pay;

        readonly List<IconRow.Entry> entries = new List<IconRow.Entry>();
        public string Area { get; private set; }
        public string FromArea { get; private set; }
        public UnlockPayState PayState { get; private set; }
        public bool Hovered { get; set; }
        public Rect WorldRect => new Rect(transform.position.x - W * 0.5f, transform.position.y - H * 0.5f, W, H);
        public string PayLabel => pay != null && pay.gameObject.activeSelf ? pay.text : null;
        internal int seenFrame;

        public void Bind(string area, string from, Vector3 centre, string regionName)
        {
            Area = area; FromArea = from;
            name = "UnlockSign_" + area + "_from_" + from;
            transform.position = centre;
            panel.drawMode = SpriteDrawMode.Sliced;
            panel.size = new Vector2(W, H);
            panel.transform.localPosition = Vector3.zero;
            frame.transform.localPosition = new Vector3(-W * 0.5f, H * 0.5f, 0f);
            glow.transform.localPosition = new Vector3(-W * 0.5f - ViewKit.U(5f), H * 0.5f + ViewKit.U(5f), 0f);
            glow.Set(W + ViewKit.U(10f), H + ViewKit.U(10f), ViewKit.U(4f), Color.clear, false);
            ViewKit.Fit(icon, icon.sprite, 26f);
            icon.transform.localPosition = new Vector3(0f, H * 0.5f - ViewKit.U(20f), 0f);
            title.text = "Unlock " + regionName;
            ViewKit.Font(title, 15f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 8f * ViewKit.FontPerPx; title.fontSizeMax = 15f * ViewKit.FontPerPx;
            title.rectTransform.sizeDelta = new Vector2(W - ViewKit.U(14f), ViewKit.U(22f));
            title.transform.localPosition = new Vector3(0f, H * 0.5f - ViewKit.U(46f), 0f);
            cost.transform.localPosition = new Vector3(0f, H * 0.5f - ViewKit.U(70f), 0f);
            cost.Clear();
            ViewKit.Font(pay, 12f);
            pay.rectTransform.sizeDelta = new Vector2(W - ViewKit.U(10f), ViewKit.U(16f));
            pay.transform.localPosition = new Vector3(0f, H * 0.5f - ViewKit.U(92f), 0f);
            nextSampleAt = 0f; lastHave = lastNeed = int.MinValue;
        }

        /// <summary>Cost / pay state is sampled at 4 Hz (the sim queries allocate); <see cref="Invalidate"/> forces the next frame.</summary>
        public const float SampleSeconds = 0.25f;
        float nextSampleAt;
        int lastHave = int.MinValue, lastNeed = int.MinValue;

        public void Invalidate() => nextSampleAt = 0f;

        public void Refresh(Simulation sim, SpriteCache sprites, double nowMs)
        {
            float t = Time.unscaledTime;
            if (t >= nextSampleAt)
            {
                nextSampleAt = t + SampleSeconds;
                var costAll = sim.AreaUnlockCost(Area);
                var paid = sim.UnlockPaid(Area);
                entries.Clear();
                if (costAll != null)
                    foreach (var e in costAll)
                    {
                        int p = paid != null ? paid.Get(e.item) : 0;
                        entries.Add(p > 0 ? new IconRow.Entry(e.item, Mathf.Min(p, e.qty) + "/" + e.qty) : new IconRow.Entry(e.item, e.qty));
                    }
                cost.Set(entries, 13f, UiPalette.Gold, sprites, null, W - ViewKit.U(12f));

                var (st, have, need) = sim.UnlockPayInfo(Area);
                PayState = st;
                bool part = st == UnlockPayState.Partial;
                ViewKit.Show(pay, part);
                if (part && (have != lastHave || need != lastNeed)) { lastHave = have; lastNeed = need; ViewKit.Text(pay, "pay " + have + "/" + need); }
            }

            var state = PayState;
            bool partial = state == UnlockPayState.Partial;
            float a = Hovered || state == UnlockPayState.Afford ? 1f : partial ? 0.8f : 0.6f;
            var border = state == UnlockPayState.Cant ? CantBorder : UiPalette.Gold;
            frame.Set(W, H, ViewKit.U(2f), WithA(border, a), state != UnlockPayState.Afford);
            panel.color = WithA(PanelColour, PanelColour.a * Mathf.Lerp(0.7f, 1f, a));
            icon.color = WithA(Color.white, a);
            title.color = WithA(UiPalette.Gold, a);
            pay.color = WithA(UiPalette.Gold, a);
            float pulse = state == UnlockPayState.Afford ? 0.5f + 0.5f * Mathf.Sin((float)(nowMs % 1800d / 1800d) * Mathf.PI * 2f) : 0f;
            glow.SetColour(ViewKit.Rgba(251, 191, 36, 0.55f * pulse));
        }

        static Color WithA(Color c, float a) { c.a = a; return c; }
    }
}
