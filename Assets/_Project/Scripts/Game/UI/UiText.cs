using System.Text;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Text helpers for data strings ported from data.js: the strings carry inline emoji. They render
    /// through the TMP default sprite asset (EmojiSprites.asset, codepoint-mapped), so by default only the
    /// invisible selectors (VS16 / ZWJ / keycap) are removed. If the sprite asset is missing, set
    /// <see cref="StripAll"/> to drop emoji instead (avoids tofu boxes).
    /// </summary>
    public static class UiText
    {
        /// <summary>Fallback switch: true = strip every emoji (used when no emoji sprite asset is available).</summary>
        public static bool StripAll = false;

        /// <summary>Kept name for existing call sites; strips selectors, or all emoji when <see cref="StripAll"/>.</summary>
        public static string StripEmoji(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return StripAll ? StripAllEmoji(s) : StripSelectors(s);
        }

        static string StripSelectors(string s)
        {
            if (s.IndexOf('️') < 0 && s.IndexOf('︎') < 0 && s.IndexOf('‍') < 0 && s.IndexOf('⃣') < 0) return s;
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                if (c != '️' && c != '︎' && c != '‍' && c != '⃣') sb.Append(c);
            return sb.ToString();
        }

        public static string StripAllEmoji(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsHighSurrogate(c)) { i++; continue; }          // astral plane = emoji
                if (char.IsLowSurrogate(c)) continue;
                if (c == '️' || c == '︎' || c == '‍' || c == '⃣') continue;
                if (c >= '☀' && c <= '➿') continue;            // misc symbols + dingbats
                if (c >= '⬀' && c <= '⯿') continue;
                if (c >= '⌀' && c <= '⏿') continue;            // misc technical (hourglass…)
                if (c == 'ℹ' || c == '〰' || c == '〽') continue;
                sb.Append(c);
            }
            var r = sb.ToString();
            while (r.Contains("  ")) r = r.Replace("  ", " ");
            return r.Replace(" .", ".").Replace(" ,", ",").Replace("( ", "(").Replace(" )", ")").Trim();
        }
    }
}
