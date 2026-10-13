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
        readonly List<Entry> last = new List<Entry>();
        float lastPx, lastMaxW; Color lastColour; string lastPrefix; bool hasLast;
        /// <summary>Laid-out width (units, after the fit-to-width scale) and height of the last <see cref="Set"/>.</summary>
        public float Width { get; private set; }
        public float Height { get; private set; }
        /// <summary>Optional per-entry mark (tick / cross) drawn at each icon's corner; null = none.</summary>
        public System.Func<int, Sprite> MarkFor;
        readonly List<SpriteRenderer> marks = new List<SpriteRenderer>();

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
            if (Same(entries, px, c, prefix, maxWidthUnits)) return;     // value compare, no per-frame allocation
            last.Clear();
            for (int i = 0; i < entries.Count; i++) last.Add(entries[i]);
            lastPx = px; lastColour = c; lastPrefix = prefix; lastMaxW = maxWidthUnits; hasLast = true;
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
                ViewKit.Outline(ic.a);
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
            Width = total * scale; Height = iconU * scale;
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
                bool isPrefix = !string.IsNullOrEmpty(prefix) && i == 0;
                int ei = i - (string.IsNullOrEmpty(prefix) ? 0 : 1);
                var mk = MarkFor != null && !isPrefix ? MarkFor(ei) : null;
                while (marks.Count <= i) marks.Add(null);
                if (mk != null)
                {
                    if (marks[i] == null)
                    {
                        marks[i] = new GameObject("Mark").AddComponent<SpriteRenderer>();
                        marks[i].transform.SetParent(ic.transform, false);
                        var mr = ic.a.GetComponent<MeshRenderer>();
                        marks[i].sortingLayerID = mr.sortingLayerID; marks[i].sortingOrder = mr.sortingOrder + 2;
                    }
                    ViewKit.Show(marks[i], true);
                    marks[i].sprite = mk;
                    marks[i].transform.localPosition = ic.icon.transform.localPosition + new Vector3(iconU * 0.4f, -iconU * 0.35f, 0f);
                }
                else if (marks[i] != null) ViewKit.Show(marks[i], false);
                x += widths[i] + gapU;
            }
        }

        bool Same(IList<Entry> entries, float px, Color c, string prefix, float maxW)
        {
            if (!hasLast || entries.Count != last.Count || px != lastPx || c != lastColour || maxW != lastMaxW || prefix != lastPrefix) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                var a = entries[i]; var b = last[i];
                if (a.qty != b.qty || a.item != b.item || a.label != b.label) return false;
            }
            return true;
        }

        public void Clear() { hasLast = false; last.Clear(); foreach (var p in pool) p.gameObject.SetActive(false); }
    }
}
