using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>
    /// A centred horizontal "qty icon qty icon …" line (ui.js drawNeedsLine U:173: 800 font,
    /// icon = 1.35*px, gap 0.3*px). Entries are pooled IconCount clones of an inactive template child.
    /// </summary>
    public class IconRow : MonoBehaviour
    {
        [SerializeField] IconCount template;
        readonly List<IconCount> pool = new List<IconCount>();
        string lastKey;

        public struct Entry
        {
            public string item; public int qty; public string label;
            public Entry(string i, int q) { item = i; qty = q; label = null; }
            /// <summary>label replaces the qty text (e.g. "3/10" installments).</summary>
            public Entry(string i, string l) { item = i; qty = 0; label = l; }
        }

        void Awake() { if (template != null) template.gameObject.SetActive(false); }

        /// <summary>Lay out entries centred on this transform. prefix = optional leading text (e.g. "Feed:").</summary>
        public void Set(IList<Entry> entries, float px, Color c, SpriteCache sprites, string prefix = null, float maxWidthUnits = 0f)
        {
            var sb = new System.Text.StringBuilder(prefix);
            foreach (var e in entries) sb.Append(e.item).Append(e.qty).Append(e.label).Append(',');
            sb.Append(px).Append(c);
            string key = sb.ToString();
            if (key == lastKey) return;
            lastKey = key;
            int n = entries.Count + (string.IsNullOrEmpty(prefix) ? 0 : 1);
            while (pool.Count < n)
            {
                var ic = Instantiate(template, transform);
                ic.name = "Entry" + pool.Count;
                pool.Add(ic);
            }
            float iconU = ViewKit.U(px * 1.35f), gapU = ViewKit.U(px * 0.3f);
            float total = 0f;
            var widths = new float[n];
            for (int i = 0; i < pool.Count; i++)
            {
                var ic = pool[i];
                bool on = i < n;
                ic.gameObject.SetActive(on);
                if (!on) continue;
                bool isPrefix = !string.IsNullOrEmpty(prefix) && i == 0;
                int ei = i - (string.IsNullOrEmpty(prefix) ? 0 : 1);
                string txt = isPrefix ? prefix : (entries[ei].label ?? entries[ei].qty.ToString());
                ViewKit.Font(ic.a, px);
                ic.a.fontStyle = FontStyles.Bold;
                ic.a.color = c;
                ic.a.text = txt;
                float tw = ic.a.GetPreferredValues(txt).x;
                ViewKit.Show(ic.icon, !isPrefix);
                if (ic.b != null) ViewKit.Show(ic.b, false);
                if (!isPrefix) ViewKit.Fit(ic.icon, sprites.Item(entries[ei].item), px * 1.35f);
                widths[i] = tw + (isPrefix ? 0f : iconU * 1.05f);
                total += widths[i] + (i < n - 1 ? gapU : 0f);
            }
            float scale = maxWidthUnits > 0f && total > maxWidthUnits ? maxWidthUnits / total : 1f;
            transform.localScale = new Vector3(scale, scale, 1f);
            float x = -total * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var ic = pool[i];
                ic.transform.localPosition = new Vector3(x, 0f, 0f);
                ic.a.alignment = TextAlignmentOptions.MidlineLeft;
                ic.a.rectTransform.pivot = new Vector2(0f, 0.5f);
                ic.a.rectTransform.sizeDelta = new Vector2(widths[i] + 0.5f, iconU);
                ic.a.transform.localPosition = Vector3.zero;
                float tw = ic.a.GetPreferredValues(ic.a.text).x;
                ic.icon.transform.localPosition = new Vector3(tw + iconU * 0.55f, 0f, 0f);
                x += widths[i] + gapU;
            }
        }

        public void Clear() { lastKey = null; foreach (var p in pool) p.gameObject.SetActive(false); }
    }
}
