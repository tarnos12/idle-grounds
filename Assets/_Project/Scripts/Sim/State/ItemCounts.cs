using System;
using System.Collections;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// Insertion-ordered string→int map (JS `{item: qty}` objects: paid, stock,
    /// pending, offered, perks, vows.done…). Order matters (feedNeeds walks
    /// key order). Setting a key to 0 keeps it (like JS); use Remove to delete.
    /// </summary>
    [Serializable]
    public sealed class ItemCounts : IEnumerable<ItemQty>
    {
        public List<ItemQty> entries = new List<ItemQty>();

        public ItemCounts() { }
        public ItemCounts(IEnumerable<ItemQty> src) { if (src != null) foreach (var e in src) Set(e.item, e.qty); }

        public int Count => entries.Count;

        int IndexOf(string key)
        {
            for (int i = 0; i < entries.Count; i++) if (entries[i].item == key) return i;
            return -1;
        }

        public bool Has(string key) => IndexOf(key) >= 0;
        public int Get(string key) { int i = IndexOf(key); return i >= 0 ? entries[i].qty : 0; }
        public int this[string key] { get => Get(key); set => Set(key, value); }

        public void Set(string key, int qty)
        {
            int i = IndexOf(key);
            if (i >= 0) entries[i].qty = qty; else entries.Add(new ItemQty(key, qty));
        }

        public int Add(string key, int delta) { int v = Get(key) + delta; Set(key, v); return v; }
        public bool Remove(string key) { int i = IndexOf(key); if (i < 0) return false; entries.RemoveAt(i); return true; }
        public void Clear() => entries.Clear();
        public int Total() { int t = 0; foreach (var e in entries) t += e.qty; return t; }
        public IEnumerable<string> Keys { get { foreach (var e in entries) yield return e.item; } }
        public ItemCounts Clone() => new ItemCounts(entries);

        public IEnumerator<ItemQty> GetEnumerator() => entries.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
