using System;
using System.Collections.Generic;
using System.Linq;
using IdleGrounds.Sim;

namespace IdleGrounds.Game
{
    /// <summary>
    /// Shared strings of the M7 panels (ui.js fmtAway, headStartsText, PERK_FX, offeringsHTML). The ☯ glyph renders via the TMP emoji sprite asset (
    /// EmojiSprites.asset).
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

        /// <summary>`headStartsText(asc)` — what a run starts with from the legacy perks.</summary>
        public static string HeadStarts(Simulation sim, int? asc = null)
        {
            var parts = new List<string>();
            int paths = Math.Min(sim.PerkLevel("paths"), PrestigeSystem.PathRegions.Length);
            int legacy = Math.Min(sim.PerkLevel("legacy"), PrestigeSystem.LegacyRegions.Length);
            if (paths > 0) parts.Add(string.Join(" + ", PrestigeSystem.PathRegions.Take(paths).Select(k => RegionName(sim.Config, k))) + " open");
            if (legacy > 0) parts.Add(string.Join(" + ", PrestigeSystem.LegacyRegions.Take(legacy).Select(k => RegionName(sim.Config, k))) + " auto L1");
            int hands = sim.PerkLevel("hands");
            if (hands > 0) parts.Add("hand +" + PrestigeSystem.HandsPerLevel * hands);
            double tm = sim.Dragon.TributeMult(asc);
            if (tm < 1) parts.Add("dragon tributes ×" + tm.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            return "Head starts: " + (parts.Count > 0 ? string.Join(" · ", parts) : "none yet — Remembered Paths and Legacy Automation add them");
        }

        static string Mul(double v) => "×" + v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>PERK_FX label + value at a level; null for unknown perks.</summary>
        public static (string label, string value)? PerkFx(Simulation sim, string id, int l)
        {
            switch (id)
            {
                case "haste": return ("timers", Mul(Math.Pow(0.95, l)));
                case "hall": return ("disciples", "+" + l);
                case "slumber": return ("offline", (8 + 2 * l) + "h");
                case "hands": return ("carry", "+" + 5 * l);
                case "frugal": return ("unlock cost", Mul(Math.Pow(0.8, l)));
                case "ember": return ("fuel use", Mul(Math.Pow(0.85, l)));
                case "apgain": return ("AP/ascension", "+" + l);
                case "autoboost": return ("nodes/tick", "+" + l);
                case "regrow": return ("regrow", Mul(Math.Pow(0.9, l)));
                case "gale": return ("lantern beat", Mul(Math.Pow(0.9, l)));
                case "fury": return ("damage", "+" + l);
                case "bless": return ("blessings", Mul(Math.Pow(1.2, l)));
                case "bounty": return ("fields", Mul(Math.Pow(0.9, l)));
                case "paths":
                    return ("opens", l > 0 ? string.Join(" + ", PrestigeSystem.PathRegions.Take(l).Select(k => RegionName(sim.Config, k))) : "none");
                case "legacy":
                    return ("auto L1", l > 0 ? string.Join(" + ", new[] { "Center", "Farm", "Mine" }.Take(l)) : "none");
                default: return null;
            }
        }

        /// <summary>`perkFxHTML` line: "label now → next" or "label now (max)".</summary>
        public static string PerkFxLine(Simulation sim, PerkDef p, int lvl)
        {
            var now = PerkFx(sim, p.id, lvl);
            if (now == null) return "";
            if (lvl >= p.max) return now.Value.label + " " + now.Value.value + " (max)";
            return now.Value.label + " " + now.Value.value + " → " + PerkFx(sim, p.id, lvl + 1).Value.value;
        }

        /// <summary>`offeringsHTML` (item names instead of inline icons).</summary>
        public static string Offerings(GameConfig cfg, GateOfferingInfo off)
        {
            if (off.count >= off.cap) return "Offerings " + off.count + "/" + off.cap + " — full (+" + off.count + " ☯)";
            return "Offerings " + off.count + "/" + off.cap + ": " +
                   string.Join("  ", cfg.gateOfferings.items.Select(it => ItemName(cfg, it) + " " + Math.Min(off.perType, off.offered.Get(it)) + "/" + off.perType));
        }

        /// <summary>Active vows of the run as "Name, Name".</summary>
        public static List<VowDef> ActiveVows(Simulation sim)
        {
            var list = new List<VowDef>();
            foreach (var id in sim.State.vows.active)
            {
                var v = sim.Config.vows.Find(x => x.id == id);
                if (v != null) list.Add(v);
            }
            return list;
        }
    }
}
