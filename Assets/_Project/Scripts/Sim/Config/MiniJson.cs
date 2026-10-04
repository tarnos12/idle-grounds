using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace IdleGrounds.Sim
{
    /// <summary>Insertion-ordered JSON object (JS object key order matters for DATA).</summary>
    public sealed class JsonObject
    {
        readonly List<KeyValuePair<string, object>> _entries = new List<KeyValuePair<string, object>>();
        readonly Dictionary<string, int> _index = new Dictionary<string, int>();

        public int Count => _entries.Count;
        public IReadOnlyList<KeyValuePair<string, object>> Entries => _entries;

        public IEnumerable<string> Keys { get { foreach (var e in _entries) yield return e.Key; } }

        public void Set(string key, object value)
        {
            if (_index.TryGetValue(key, out int i)) _entries[i] = new KeyValuePair<string, object>(key, value);
            else { _index[key] = _entries.Count; _entries.Add(new KeyValuePair<string, object>(key, value)); }
        }

        public bool Has(string key) => _index.ContainsKey(key);
        public object Get(string key) => _index.TryGetValue(key, out int i) ? _entries[i].Value : null;
        public object this[string key] => Get(key);
    }

    /// <summary>
    /// Tiny recursive-descent JSON parser (no Newtonsoft / UnityEngine in Sim).
    /// Produces JsonObject / List&lt;object&gt; / double / string / bool / null.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var p = new Parser(json);
            p.SkipWs();
            var v = p.ParseValue();
            p.SkipWs();
            if (!p.AtEnd) throw p.Error("trailing characters");
            return v;
        }

        sealed class Parser
        {
            readonly string _s;
            int _i;
            public Parser(string s) { _s = s; _i = 0; if (_s.Length > 0 && _s[0] == '﻿') _i = 1; }
            public bool AtEnd => _i >= _s.Length;

            public FormatException Error(string msg) => new FormatException("JSON: " + msg + " at " + _i);

            public void SkipWs()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') _i++;
                    else break;
                }
            }

            char Peek() => _i < _s.Length ? _s[_i] : '\0';

            void Expect(char c)
            {
                if (Peek() != c) throw Error("expected '" + c + "'");
                _i++;
            }

            public object ParseValue()
            {
                SkipWs();
                char c = Peek();
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Literal("true"); return true;
                    case 'f': Literal("false"); return false;
                    case 'n': Literal("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw Error("unexpected '" + c + "'");
                }
            }

            void Literal(string lit)
            {
                if (string.CompareOrdinal(_s, _i, lit, 0, lit.Length) != 0) throw Error("expected " + lit);
                _i += lit.Length;
            }

            JsonObject ParseObject()
            {
                Expect('{');
                var o = new JsonObject();
                SkipWs();
                if (Peek() == '}') { _i++; return o; }
                while (true)
                {
                    SkipWs();
                    string k = ParseString();
                    SkipWs();
                    Expect(':');
                    object v = ParseValue();
                    o.Set(k, v);
                    SkipWs();
                    if (Peek() == ',') { _i++; continue; }
                    Expect('}');
                    return o;
                }
            }

            List<object> ParseArray()
            {
                Expect('[');
                var a = new List<object>();
                SkipWs();
                if (Peek() == ']') { _i++; return a; }
                while (true)
                {
                    a.Add(ParseValue());
                    SkipWs();
                    if (Peek() == ',') { _i++; continue; }
                    Expect(']');
                    return a;
                }
            }

            string ParseString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("bad escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("bad \\u escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw Error("bad escape \\" + e);
                    }
                }
            }

            double ParseNumber()
            {
                int start = _i;
                if (Peek() == '-') _i++;
                while (!AtEnd && "0123456789.eE+-".IndexOf(_s[_i]) >= 0) _i++;
                string num = _s.Substring(start, _i - start);
                if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    throw Error("bad number '" + num + "'");
                return d;
            }
        }
    }
}
