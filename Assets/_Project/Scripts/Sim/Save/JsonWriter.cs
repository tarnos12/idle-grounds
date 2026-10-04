using System;
using System.Globalization;
using System.Text;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// Minimal streaming JSON writer (no Newtonsoft / UnityEngine in Sim) —
    /// the counterpart of <see cref="MiniJson"/>. Compact output; numbers
    /// in invariant culture, doubles round-trip ("R"); NaN/±Infinity are
    /// written as null (JSON has no non-finite numbers).
    /// </summary>
    public sealed class JsonWriter
    {
        readonly StringBuilder _sb;
        // per nesting level: has the current container written an element yet?
        bool[] _first = new bool[32];
        int _depth;
        bool _afterKey;

        public JsonWriter(int capacity = 4096) { _sb = new StringBuilder(capacity); }

        public override string ToString() => _sb.ToString();

        void BeforeValue()
        {
            if (_afterKey) { _afterKey = false; return; }
            if (_depth > 0)
            {
                if (!_first[_depth]) _sb.Append(',');
                _first[_depth] = false;
            }
        }

        void Push()
        {
            _depth++;
            if (_depth >= _first.Length) Array.Resize(ref _first, _first.Length * 2);
            _first[_depth] = true;
        }

        public JsonWriter BeginObject() { BeforeValue(); _sb.Append('{'); Push(); return this; }
        public JsonWriter EndObject() { _depth--; _sb.Append('}'); return this; }
        public JsonWriter BeginArray() { BeforeValue(); _sb.Append('['); Push(); return this; }
        public JsonWriter EndArray() { _depth--; _sb.Append(']'); return this; }

        public JsonWriter Key(string name)
        {
            if (!_first[_depth]) _sb.Append(',');
            _first[_depth] = false;
            WriteString(name);
            _sb.Append(':');
            _afterKey = true;
            return this;
        }

        public JsonWriter Null() { BeforeValue(); _sb.Append("null"); return this; }
        public JsonWriter Value(bool v) { BeforeValue(); _sb.Append(v ? "true" : "false"); return this; }
        public JsonWriter Value(int v) { BeforeValue(); _sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
        public JsonWriter Value(long v) { BeforeValue(); _sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }

        public JsonWriter Value(double v)
        {
            BeforeValue();
            if (double.IsNaN(v) || double.IsInfinity(v)) _sb.Append("null");
            else _sb.Append(v.ToString("R", CultureInfo.InvariantCulture));
            return this;
        }

        public JsonWriter Value(string v)
        {
            BeforeValue();
            if (v == null) _sb.Append("null"); else WriteString(v);
            return this;
        }

        void WriteString(string s)
        {
            _sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    case '\b': _sb.Append("\\b"); break;
                    case '\f': _sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) _sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }
    }
}
