using UnityEngine;

namespace IdleGrounds.Game
{
    /// <summary>Palette of ui-input-render §7.</summary>
    public static class UiPalette
    {
        public static readonly Color Bg = Hex("#1a1f24"), Bg2 = Hex("#232a31"), Panel = Hex("#2b333c"), Panel2 = Hex("#323b46"),
            Line = Hex("#3c4651"), Text = Hex("#e6edf3"), Muted = Hex("#94a3b8"), Accent = Hex("#4ade80"),
            AccentDk = Hex("#22a35a"), Gold = Hex("#fbbf24"), Danger = Hex("#f87171"), Amber = Hex("#f59e0b"),
            Purple = Hex("#a855f7");

        /// <summary>Hand chip background rgba(20,25,30,.9).</summary>
        public static readonly Color HandChip = new Color(20 / 255f, 25 / 255f, 30 / 255f, 0.9f);

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    }
}
