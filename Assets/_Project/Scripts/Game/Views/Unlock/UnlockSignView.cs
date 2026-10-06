using System.Collections.Generic;
using IdleGrounds.Sim;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// A world-space Island unlock stele (ui-input-render §4.11 unlock button, as a scene object; ADR 0003):
    /// stands just inside the locked Island's edge facing the Center. A carved stone stele (ART-SPEC §3.5:
    /// locked / partly paid / ready frames) rises above a cost plate showing the unlock
    /// icon, "Unlock {Island}", the cost line ("qty icon", or "paid/needed icon" once an installment
    /// was paid) and "pay have/need" when the hand covers part of it. States from
    /// <see cref="Simulation.UnlockPayInfo"/>: afford = solid gold border + pulsing glow (1.8 s);
    /// partial = dashed gold, opacity .8; cant = dashed #6b561c, opacity .6 (1 while hovered).
    /// Clicks are routed by <see cref="HandController"/> through <see cref="UnlockSignSync.HitTest"/>.
    /// </summary>
    public class UnlockSignView : MonoBehaviour
    {
        public const float W = 4.6f, H = 3.3f;
        /// <summary>Stele art height above the plate (world units; 64×96 px art = 2×3 cells).</summary>
        public const float SteleH = 3f;
        public static readonly Color PanelColour = ViewKit.Rgba(20, 25, 30, 0.86f), CantBorder = UiPalette.Hex("#6b561c");

        [SerializeField] SpriteRenderer panel;
        [SerializeField] EdgeFrame frame;
        [SerializeField] EdgeFrame glow;
        [SerializeField] SpriteRenderer icon;
        [SerializeField] TextMeshPro title;
        [SerializeField] IconRow cost;
        [SerializeField] TextMeshPro pay;
        [Header("Stele (ART-SPEC 3.5)")]
        [SerializeField] SpriteRenderer stele;
        [SerializeField] Sprite steleLocked;
        [SerializeField] Sprite stelePartial;
        [SerializeField] Sprite[] steleReady;

        readonly List<IconRow.Entry> entries = new List<IconRow.Entry>();
        public string Area { get; private set; }
        public string FromArea { get; private set; }
        public UnlockPayState PayState { get; private set; }
        public bool Hovered { get; set; }
        public Rect WorldRect => new Rect(transform.position.x - W * 0.5f, transform.position.y - H * 0.5f, W, H + (stele != null ? SteleH : 0f));
        /// <summary>True once any installment was paid toward this Island (the half-glowing stele).</summary>
        public bool PartlyPaid { get; private set; }
        public string PayLabel => pay != null && pay.gameObject.activeSelf ? pay.text : null;
        internal int seenFrame;

        public void Bind(string area, string from, Vector3 centre, string islandName)
        {
            Area = area; FromArea = from;
            name = "UnlockStele_" + area;
            if (stele != null)
            {
                var s0 = steleLocked != null ? steleLocked : stele.sprite;
                stele.sprite = s0;
                float sh = s0 != null ? s0.bounds.size.y : 1f;
                float k = SteleH / Mathf.Max(0.0001f, sh);
                stele.transform.localScale = new Vector3(k, k, 1f);
                stele.transform.localPosition = new Vector3(0f, H * 0.5f - ViewKit.U(2f), 0f);     // art pivot = bottom-centre
            }
            transform.position = centre;
            panel.drawMode = SpriteDrawMode.Sliced;
            panel.size = new Vector2(W, H);
            panel.transform.localPosition = Vector3.zero;
            frame.transform.localPosition = new Vector3(-W * 0.5f, H * 0.5f, 0f);
            glow.transform.localPosition = new Vector3(-W * 0.5f - ViewKit.U(5f), H * 0.5f + ViewKit.U(5f), 0f);
            glow.Set(W + ViewKit.U(10f), H + ViewKit.U(10f), ViewKit.U(4f), Color.clear, false);
            ViewKit.Fit(icon, icon.sprite, 26f);
            icon.transform.localPosition = new Vector3(0f, H * 0.5f - ViewKit.U(20f), 0f);
            title.text = "Unlock " + islandName;
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
            lastSig = long.MinValue; lastHave = lastNeed = int.MinValue;
        }

        /// <summary>Cost / pay state is read every frame (non-allocating queries into cached buffers); the icon row and
        /// pay text only rebuild when a value changes. <see cref="Invalidate"/> forces a rebuild next frame.</summary>
        readonly ItemCounts costBuf = new ItemCounts(), paidBuf = new ItemCounts();
        long lastSig = long.MinValue;
        int lastHave = int.MinValue, lastNeed = int.MinValue;

        public void Invalidate() { lastSig = long.MinValue; }

        public void Refresh(Simulation sim, SpriteCache sprites, double nowMs)
        {
            bool hasCost = sim.AreaUnlockCost(Area, costBuf);
            sim.UnlockPaid(Area, paidBuf);
            long sig = hasCost ? 31 : 17;
            if (hasCost)
                foreach (var e in costBuf)
                    sig = sig * 131 + (e.item != null ? e.item.GetHashCode() : 0) + e.qty * 7919L + paidBuf.Get(e.item) * 104729L;
            if (sig != lastSig)
            {
                lastSig = sig;
                PartlyPaid = false;
                entries.Clear();
                if (hasCost)
                    foreach (var e in costBuf)
                    {
                        int p = paidBuf.Get(e.item);
                        if (p > 0) PartlyPaid = true;
                        entries.Add(p > 0 ? new IconRow.Entry(e.item, Mathf.Min(p, e.qty) + "/" + e.qty) : new IconRow.Entry(e.item, e.qty));
                    }
                cost.Set(entries, 13f, UiPalette.Gold, sprites, null, W - ViewKit.U(12f));
            }
            {
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
            if (stele != null)
            {
                Sprite s = steleLocked;
                if (state == UnlockPayState.Afford && steleReady != null && steleReady.Length > 0)
                    s = steleReady[(int)(nowMs / 180d) % steleReady.Length];
                else if (PartlyPaid && stelePartial != null) s = stelePartial;
                if (s != null && stele.sprite != s) stele.sprite = s;
                stele.color = WithA(Color.white, Mathf.Lerp(0.85f, 1f, a));
            }
        }

        static Color WithA(Color c, float a) { c.a = a; return c; }
    }
}
