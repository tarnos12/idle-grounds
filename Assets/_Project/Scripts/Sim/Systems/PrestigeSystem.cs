using System;
using System.Collections.Generic;

namespace IdleGrounds.Sim
{
    /// <summary>
    /// Prestige (engine-systems §14, engine.js:141-335): perks bought with AP,
    /// the ascension reset, vows. Perk/vow effects are read at their single
    /// wiring points in the other systems (haste → Timing.PrestigeFactor, hall →
    /// Pavilion, slumber (Tireless Wisps) → Logistics beat/speed, hands → handCap, frugal →
    /// Progression.UnlockCost, ember/coldhearth → Converter/Fuel, apgain →
    /// AscendReward, autoboost → Automation, regrow → Node, gale → Logistics,
    /// fury → Combat, bless/restless → Dragon, bounty → FieldGenerator,
    /// burden → Hand.Cap); this class applies the immediate (buy-time) and the
    /// fresh-run (ascend-time) effects.
    /// </summary>
    public sealed class PrestigeSystem
    {
        /// <summary>Remembered Paths: level n opens PATH_REGIONS[n-1] (engine.js:155).</summary>
        public static readonly string[] PathRegions = { "mine", "fishing", "farm" };
        /// <summary>Legacy Automation: level n grants Automation L1 in LEGACY_REGIONS[n-1].</summary>
        public static readonly string[] LegacyRegions = { "center", "farm", "mine" };
        /// <summary>Fleet Hands: +5 carrying capacity per level.</summary>
        public const int HandsPerLevel = 5;

        readonly Simulation _sim;
        SimContext Ctx => _sim.Ctx;
        GameState S => Ctx.State;
        GameConfig Cfg => Ctx.Config;

        public PrestigeSystem(Simulation sim) { _sim = sim; }

        public int? PerkCost(string id) => Ctx.Progression.PerkCost(id);

        public bool CanAfford(string id)
        {
            var c = PerkCost(id);
            return c != null && S.ascendPoints >= c.Value;
        }

        /// <summary>`buyPerk(id)` engine.js:163 — deduct AP, level++, apply the immediate effect.</summary>
        public bool BuyPerk(string id)
        {
            var cost = PerkCost(id);
            if (cost == null || S.ascendPoints < cost.Value) return false;
            S.ascendPoints -= cost.Value;
            int lvl = S.perks.Add(id, 1);
            if (id == "hands") S.handCap += HandsPerLevel;     // Fleet Hands live
            if (id == "paths" && lvl - 1 < PathRegions.Length)
            {
                string pk = PathRegions[lvl - 1];
                if (Cfg.Region(pk) != null && !S.world.IsUnlocked(pk))
                {
                    // installments already paid toward it go back to the hand (spill: Center)
                    var paid = Ctx.Progression.UnlockPaid(pk).Clone();
                    S.world.unlockPaid.RemoveAll(e => e.region == pk);
                    Ctx.Progression.RefundToHand(paid);
                    Ctx.Progression.OpenRegion(pk);
                }
            }
            if (id == "frugal") Ctx.Progression.ReconcileUnlockInstallments();
            if (id == "legacy" && lvl - 1 < LegacyRegions.Length)
            {
                var a = S.Area(LegacyRegions[lvl - 1]);
                if (a != null) a.upgrades.automation = Math.Max(a.upgrades.automation, 1);
            }
            return true;
        }

        /// <summary>Vows can be chosen for the next run only after the first ascension.</summary>
        public bool CanChooseVows => S.ascensions >= 1;

        /// <summary>World speed shown to the player = 1/prestigeFactor.</summary>
        public double WorldSpeed => 1.0 / Ctx.Timing.PrestigeFactor(S);

        /// <summary>World speed of the next run (one more ascension; active vows counted as marks).</summary>
        public double NextWorldSpeed => 1.0 / Ctx.Timing.NextPrestigeFactor(S);

        /// <summary>
        /// Build the fresh-run state `ascend(nextVows)` (engine.js:298) would
        /// switch to, without installing it. Pure: the current state is untouched.
        /// </summary>
        public GameState BuildAscendedState(IEnumerable<string> nextVows, out int reward)
        {
            var old = S;
            int asc = old.ascensions + 1;
            reward = Ctx.Progression.AscendReward();
            int pts = old.ascendPoints + reward;
            var perks = old.perks ?? new ItemCounts();
            double speedFrom = 1.0 / Ctx.Timing.PrestigeFactor(old);

            // vows kept this run are completed; the new run takes the chosen ones
            var vows = new VowsState { done = (old.vows?.done ?? new ItemCounts()).Clone() };
            if (old.vows != null) foreach (var id in old.vows.active) vows.done.Add(id, 1);
            if (old.ascensions >= 1 && nextVows != null)
                foreach (var id in nextVows)
                    if (id != null && Cfg.vows.Exists(v => v.id == id) && !vows.active.Contains(id)) vows.active.Add(id);

            var fresh = GameState.CreateInitial(Cfg, (long)Ctx.Now);
            fresh.ascensions = asc;
            fresh.ascendPoints = pts;                       // AP + perks survive the reset
            fresh.perks = perks;
            fresh.handCap = Cfg.balance.handCap + HandsPerLevel * perks.Get("hands");   // Hand Size levels are lost
            fresh.quest.idx = Cfg.quests.Count;             // veterans skip the tutorial chain
            fresh.dragonBlessed = old.dragonBlessed || old.won;
            fresh.stats = old.stats ?? new GameStats();     // lifetime
            fresh.introSeen = true;
            fresh.endingSeen = old.endingSeen;
            fresh.vows = vows;
            if (vows.active.Contains("solitude")) fresh.starterPlaced = true;   // Vow of Solitude: no starter network
            int paths = Math.Min(perks.Get("paths"), PathRegions.Length);
            for (int i = 0; i < paths; i++) if (Cfg.Region(PathRegions[i]) != null) fresh.world.SetUnlocked(PathRegions[i], true);
            int legacy = Math.Min(perks.Get("legacy"), LegacyRegions.Length);
            for (int i = 0; i < legacy; i++)
            {
                var a = fresh.Area(LegacyRegions[i]);
                if (a != null) a.upgrades.automation = Math.Max(a.upgrades.automation, 1);
            }
            // one-time "Ascension n complete" card (speedTo on the fresh state — includes the new marks)
            fresh.justAscended = new JustAscended { n = asc, ap = reward, speedFrom = speedFrom, speedTo = 1.0 / Ctx.Timing.PrestigeFactor(fresh) };
            return fresh;
        }

        /// <summary>
        /// `ascend(nextVows)` — no eligibility check (the UI gates it on a built
        /// Gate). Installs the fresh run and re-runs the boot (initArea ×all +
        /// starter network); raises SoundRequested("ascend") then RunReset.
        /// The caller should save right after (JS saves then reloads).
        /// </summary>
        public JustAscended Ascend(IEnumerable<string> nextVows)
        {
            Ctx.Events.RaiseSound("ascend", null);
            var fresh = BuildAscendedState(nextVows, out _);
            _sim.ReplaceState(fresh);
            return fresh.justAscended;
        }
    }
}
