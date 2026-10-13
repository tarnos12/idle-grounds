using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Control wording for touch screens. Quest, help, hint and tooltip texts are written for mouse + keyboard
    /// ("Hold left-click", "Right-click the dragon", "WASD", "mouse wheel"); on a phone the same sentences must say what
    /// the finger does: Tap, Hold, Give (a hold on a building with items in hand, or the Give button), drag to look around,
    /// pinch to zoom. <see cref="Apply"/> rewrites a string when <see cref="MobileUi.IsMobile"/>, <see cref="Rewrite"/> always
    /// (used by tests). MobileLayer also runs it on every changed TextMeshPro text, so data strings need no tokens.
    /// </summary>
    public static class ControlWords
    {
        struct Rule
        {
            public Regex re; public string with;
        }

        static readonly Rule[] Rules = Build(
            // whole sentences first
            (@"WASD pans the camera[^.]*?Esc cancels/closes", "Drag pans the camera, pinch zooms, the Build button opens the build menu, Swap rotates the stacks in your hand, Back closes panels"),
            (@"(?-i:Hold left-click)", "Hold"),
            (@"(?-i:hold left-click)", "hold"),
            (@"WASD to look around, mouse[- ]wheel to zoom", "drag to look around, pinch to zoom"),
            (@"\(WASD\)", "(drag)"),
            (@"WASD to look around", "drag to look around"),
            (@"WASD", "drag"),
            (@"mouse[- ]?wheel", "pinch"),
            (@"right-click-feed", "Give"),
            (@"right-clicking", "giving"),
            (@"right-click", "Give"),
            (@"left-click", "tap"),
            (@"\bclicking\b", "tapping"),
            (@"\bclicked\b", "tapped"),
            (@"\bclicks\b", "taps"),
            (@"\bclick\b", "tap"),
            (@" \(B\)", ""),
            (@"\bPress B\b", "Open Build"),
            (@"\bB places\b", "Build places"),
            (@"(?-i:\bEsc to stop\b)", "tap Demolish to stop"),
            (@"\(Esc: (stop|cancel)\)", "(Cancel button)"),
            (@"(?-i:\bEsc cancels\b)", "Cancel stops it"),
            (@"\bEsc\b", "Back"),
            (@"\bQ / E change\b", "Swap changes"),
            (@"\bQ / E\b", "Swap"),
            (@"\bhovered\b", "pressed"),
            (@"\bhover\b", "press"));

        /// <summary>Capital in, capital out: a sentence-initial "Click" becomes "Tap", a mid-sentence "click" becomes "tap".</summary>
        static string MatchCase(string found, string w)
        {
            if (w.Length == 0) return w;
            bool cap = found.Length > 1 && char.IsUpper(found[0]) && char.IsLower(found[1]);
            if (cap && char.IsLower(w[0])) return char.ToUpperInvariant(w[0]) + w.Substring(1);
            return w;
        }

        static Rule[] Build(params (string pattern, string with)[] list)
        {
            var r = new Rule[list.Length];
            for (int i = 0; i < list.Length; i++)
                r[i] = new Rule { re = new Regex(list[i].pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), with = list[i].with };
            return r;
        }

        static readonly string[] Triggers = { "lick", "WASD", "wheel", "Esc", "hover", "(B)", "Press B", "B places", "Q / E" };
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

        /// <summary>The text as shown on this device: touch wording on a phone, unchanged elsewhere.</summary>
        public static string Apply(string s) => MobileUi.IsMobile ? Rewrite(s) : s;

        /// <summary>Touch wording regardless of the platform (null / text without control words come back unchanged).</summary>
        public static string Rewrite(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            bool hit = false;
            for (int i = 0; i < Triggers.Length && !hit; i++) hit = s.IndexOf(Triggers[i], System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hit) return s;
            if (cache.TryGetValue(s, out var done)) return done;
            string r = s;
            foreach (var rule in Rules)
            {
                string w = rule.with;
                bool exact = rule.re.ToString().StartsWith("(?-i:");      // wording for these is fixed: no capital / lower-case adjusting
                r = rule.re.Replace(r, m => exact ? w : MatchCase(m.Value, w));
            }
            if (cache.Count > 1024) cache.Clear();
            cache[s] = r;
            return r;
        }
    }
}
