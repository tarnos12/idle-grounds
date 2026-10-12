using System;
using IdleGrounds.Sim;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Shared strings of the M7 panels (ui.js fmtAway, plurals, names).
    /// </summary>
    public static class MetaText
    {
        public const string Gold = "#fbbf24", Accent = "#4ade80", Danger = "#f87171", Muted = "#94a3b8";

        /// <summary>ui.js fmtAway: "Xh Ym" / "Ym" / "Xs".</summary>
        public static string FmtAway(double ms)
        {
            long s = (long)Math.Floor(Math.Max(0, ms) / 1000), h = s / 3600, m = s % 3600 / 60;
            if (h > 0) return h + "h " + m + "m";
            if (m > 0) return m + "m";
            return s + "s";
        }

        public static string Plural(int n, string word) => n + " " + word + (n == 1 ? "" : "s");

        public static string RegionName(GameConfig cfg, string key) => cfg.Region(key)?.name ?? key;

        public static string ItemName(GameConfig cfg, string key) => cfg.Item(key)?.name ?? key;
    }
}
