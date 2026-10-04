using System.Text;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Text helpers for data strings ported from data.js: the strings carry inline emoji, which the
    /// LiberationSans TMP font cannot draw (no emoji sprite asset yet) — strip them so labels show no
    /// tofu boxes. Icons are drawn as separate Images / sprites instead.
    /// </summary>
    public static class UiText
    {
        public static string StripEmoji(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsHighSurrogate(c)) { i++; continue; }          // astral plane = emoji
                if (char.IsLowSurrogate(c)) continue;
                if (c == '️' || c == '︎' || c == '‍' || c == '⃣') continue;
                if (c >= '☀' && c <= '➿') continue;               // misc symbols + dingbats
                if (c >= '⬀' && c <= '⯿') continue;
                if (c >= '⌀' && c <= '⏿') continue;               // misc technical (hourglass…)
                if (c == 'ℹ' || c == '〰' || c == '〽') continue;
                sb.Append(c);
            }
            // collapse the double spaces / leading space emoji removal leaves
            var r = sb.ToString();
            while (r.Contains("  ")) r = r.Replace("  ", " ");
            return r.Replace(" .", ".").Replace(" ,", ",").Replace("( ", "(").Replace(" )", ")").Trim();
        }
    }
}
