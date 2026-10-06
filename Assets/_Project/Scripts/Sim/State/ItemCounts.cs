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
        /// <summary>Entry objects recycled by <see cref="ClearReuse"/> (runtime only, never saved).</summary>
        [NonSerialized] List<ItemQty> _spare;

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
            if (i >= 0) { entries[i].qty = qty; return; }
            if (_spare != null && _spare.Count > 0)
            {
                var e = _spare[_spare.Count - 1];
                _spare.RemoveAt(_spare.Count - 1);
                e.item = key; e.qty = qty;
                entries.Add(e);
            }
            else entries.Add(new ItemQty(key, qty));
        }

        /// <summary>
        /// Clear for refilling without allocation: the entry objects are kept and reused by later
        /// <see cref="Set"/>s. Only for caller-owned scratch / read-out maps (the non-allocating view
        /// queries) — never on a map whose <see cref="ItemQty"/> objects someone else may still hold.
        /// </summary>
        public void ClearReuse()
        {
            if (entries.Count == 0) return;
            _spare ??= new List<ItemQty>(Math.Max(4, entries.Count));
            _spare.AddRange(entries);
            entries.Clear();
        }

        /// <summary>Overwrite with <paramref name="src"/>'s entries (same order), reusing entry objects. Null ⇒ empty.</summary>
        public void CopyFromReuse(ItemCounts src)
        {
            if (ReferenceEquals(src, this)) return;
            ClearReuse();
            if (src == null) return;
            for (int i = 0; i < src.entries.Count; i++) Set(src.entries[i].item, src.entries[i].qty);
        }

        public int Add(string key, int delta) { int v = Get(key) + delta; Set(key, v); return v; }
        public bool Remove(string key) { int i = IndexOf(key); if (i < 0) return false; entries.RemoveAt(i); return true; }
        public void Clear() => entries.Clear();
        public int Total() { int t = 0; foreach (var e in entries) t += e.qty; return t; }
        public IEnumerable<string> Keys { get { foreach (var e in entries) yield return e.item; } }
        public ItemCounts Clone() => new ItemCounts(entries);

        /// <summary>Struct enumerator: `foreach` over an ItemCounts does not allocate.</summary>
        public List<ItemQty>.Enumerator GetEnumerator() => entries.GetEnumerator();
        IEnumerator<ItemQty> IEnumerable<ItemQty>.GetEnumerator() => entries.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => entries.GetEnumerator();
    }
}
