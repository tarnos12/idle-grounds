using System.Collections.Generic;
using IdleGrounds.Sim;

namespace IdleGrounds.Game
{
    /// <summary>
    /// The help modal sections, verbatim from ui.js <c>openHelp</c> (U:2312). Sections appear only once their
    /// content exists in the run (dragon stage / unlocked regions / ascended). Emoji are stripped for display
    /// by the view (the TMP font cannot draw them); &lt;b&gt; tags are TMP rich text.
    /// </summary>
    public static class HelpText
    {
        public const string Title = "❓ How to play";

        public static List<(string title, string body)> Sections(Simulation sim)
        {
            var G = sim.State;
            var cfg = sim.Config;
            int dr = G.dragon.stage;
            bool U(string k) => G.world.IsUnlocked(k);
            var fs = cfg.Building("furnace_spirit");
            var gs = cfg.Building("gathering_stone");
            int stokerCap = fs != null ? fs.stoker.cap : 0;
            string stokeReach = fs != null ? (fs.stoker.radius + FormationFaceView.StokeSlackCells).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "?";
            int gatherCap = gs != null ? gs.gather.cap : 0;

            var S = new List<(string, string)>();
            S.Add(("🕹️ Controls",
                "WASD pans the camera (Shift toggles 2× sprint), mouse wheel zooms, B opens the build menu, Q / E rotate the stacks in your hand, Esc cancels/closes."));
            S.Add(("✋ Gathering & the hand",
                "Left-click resource nodes to harvest; HOLD to auto-swing. Items fall on the ground — hold left-click near them to vacuum into your hand (cap shown in the bottom bar); if you START the hold on an item, only that item type is vacuumed. RIGHT-click drops items / feeds buildings; the front stack feeds first (Q / E change which stack is in front). Holding right-click keeps feeding but stops at the target: it never spills onto the ground once the building is full."));
            S.Add(("🏗️ Buildings",
                "B places a ghost; right-click-feed it its cost to build. Left-click a converter to pick its recipe (switching drops its held stock). 🗑 Demolish refunds. Converters hold up to 20 of each input, and show what they are doing under the result icon: <b>Needs</b> an input (red), <b>No fuel</b> (red), <b>Stock full</b> (amber — inputs at the cap), <b>Output pile full</b> (amber — too much of its output is lying around it, so it waits until the pile is cleared or wisped away), or Working with crafts per minute. Island unlocks can be paid in installments: click or right-click an Island's unlock stele to pay what your hand holds, as often as you like — what you have paid is kept."));
            S.Add(("🔥 Fuel racks",
                "Every burner (Kiln, Forge, Pill Furnace, Star Anvil) has one FUEL RACK on its LEFT: right-click wood, bamboo, charcoal or firestone into it (charcoal burns 4× longer than wood, firestone 12×). Fuel is separate from ingredients — recipes never consume it, only the burn does. A Furnace Spirit 🕯️ (holds " + stokerCap + ") tops up every burner whose CENTRE is inside its dashed circle (" + stokeReach + " cells) from its own stash; hover it to see the range."));
            S.Add(("🏛️ Altar upgrades",
                "Click the Altar to open the upgrade tree. Select a node, then right-click-feed the Altar the cost shown on it. Switching refunds what you fed."));
            S.Add(("🐉 The Sleeping Dragon",
                "Feed it each stage's tribute (right-click) and it teaches new recipes. Its current wish is written on it."));
            S.Add(("🦊 Fox Spirits",
                "They prowl the red corner. Click to strike (hold to auto-attack); they drop Spirit Essence. The tree's combat branch adds damage, more foxes and an AoE. RIGHT-click Beast Bait 🪱 (Cauldron) inside their zone to lure a Spirit Boar 🐗 — tough, but the only Beast Bone source. Right-click a Vitality Pill 💊 (Cauldron) in hand to quaff it for Martial Vigor: +2 attack and doubled beast loot for a while."));
            S.Add(("🫕 Dragon pills",
                "The Pill Furnace refines Qi Elixirs into four pills. Right-click one onto the dragon and it exhales a timed blessing (60s base, longer with Dragon Affinity): Ember = burners 2×, Verdant = regrow 2×, Swiftwind = wisps 2×, Stoneheart = double mining drops. A new pill replaces the active one."));
            S.Add(("🏮 Wisp network",
                "Gathering Stones 🧿 vacuum ground items inside their circle (hover one, or open a lantern's link menu, to see it; it holds " + gatherCap + " and shows n/cap). Wisp Lanterns ferry items: click a lantern to edit its links (Add link → source → target, served in order, one per beat; a dot per link shows sent / source empty / target refused / idle; amber also means the source holds nothing this target uses). Warding Seals 🈯 only pass their tuned item — right-click one with an item to retune. Storehouses 📦 buffer a single type; left-click any buffer to withdraw. Wisps cannot cross the sky on their own: links stay within one Island — to move items between Islands, build a Spirit Bridge on each, pair them (click a bridge), and link lanterns into the sending bridge and out of the receiving one. A link is refused if its target can never use the source's item (a Kiln can't take Planks)."));
            S.Add(("🧘 Disciples",
                "Build a Meditation Pavilion, then click it and Recruit disciples (each costs a Robe 🥋 from the Loom). Feed the pavilion Spirit Buns 🥟 (Mill) or Spirit Wine 🍶 (Brewery, worth 3×) by hand or wisp — while fed, each disciple cultivates Spirit Essence ✨. Disciple Mastery in the upgrade tree raises the cap."));
            if (dr >= 1) S.Add(("🔥 Forge & smelting",
                "The dragon taught you the Forge: feed it iron ore (wisps or hand) and it smelts Iron Bars automatically. Wood, charcoal and firestone are its FUEL — they go on the rack, not into the recipe."));
            if (U("volcano")) S.Add(("🌋 Volcano",
                "A molten Island yielding Obsidian and Firestone. Unlock its border by paying Iron Bars — the deep heat rewards those who have already mastered smelting."));
            if (U("grove")) S.Add(("🎋 Spirit Grove",
                "A serene Island growing Spirit Herb and Bamboo — cultivation reagents and a fast-burning fuel/building material."));
            if (dr >= 2) S.Add(("🪸 Algae Farm",
                "Places ONLY in the fishing waters; passively grows algae around itself."));
            if (dr >= 3) S.Add(("🪴 Herb Garden",
                "Grows Spirit Herbs around itself on land — the cultivation herb."));
            if (dr >= 4) S.Add(("🐲 The Awakened Dragon",
                "It watches over the grounds and sheds Dragon Scales 🔶 beside itself (faster with a Dragon Shrine, which also lengthens blessings)."));
            S.Add(("🛠️ Troubleshooting logistics",
                "<b>Stone full</b> (red n/cap): its buffer is clogged, often by byproducts. Link the stone to targets and it only collects what those targets use, so strays stay on the ground. " +
                "<b>Low fuel</b>: a burner reading No fuel needs items on its left rack — or a Furnace Spirit in range with a stocked stash. " +
                "<b>Nothing moves</b>: check the link dot (amber = source empty, or it holds nothing this target uses; red = target refused/full) and that both ends sit on the SAME Island — only Spirit Bridges carry items across the sky. " +
                "<b>Automation skipping</b>: when too much of one item is lying around an Island, automation stops harvesting that item (a small amber 'Skipping' chip with its icon shows at the Island's top-left corner) until you vacuum it up or clear it away; other work carries on."));
            if (dr >= 4) S.Add(("⛩️ Ascension",
                "Craft Talismans (Atelier) and Star Steel (Anvil), gather Dragon Scales, and raise the Ascension Gate — the built gate glows gold; click it to ascend. Ascending resets the grounds and grants Ascension Points (AP: 3, +2 per Island beyond the Center, plus gate offerings — right-click spare Talismans, Star Steel and Dragon Scales onto the gate, 2 of each count — all × the bonus from any vows you kept) and +20% world speed per ascension (it adds up; ☯ in the bottom bar)."));
            if (G.ascensions > 0 || G.ascendPoints > 0)
                S.Add(("☯ Ascension Shrine",
                    "Open the Shrine (the ☯ button in the bottom bar) to spend AP on permanent perks, grouped by Pace, Economy, Combat, Meta and Legacy — they persist through every future reset. Remembered Paths opens the Mine, then Fishing, then the Farm; Center automation also taps the Spirit Tree. After your first ascension you can also take Vows: optional restrictions for a run that pay out extra when you ascend."));
            if (U("farm") || U("mine") || U("fishing")) S.Add(("🗺️ Islands",
                "Each Island has unique resources (Farm: rice & cotton & sand; Mine: iron ore; Fishing: fish, algae & spring water). Jade shards drop from the Center quarry rock and Mine jade veins. Each locked Island bordering an open one shows an unlock stele on its edge facing the Center — pan over the sky to it and pay there."));
            else S.Add(("🗺️ Islands",
                "Locked Islands float beyond the sky — gather wood and pay at the stone unlock stele on a locked Island's edge to expand."));
            S.Add(("📊 Stats",
                "The 📊 Stats button in the bottom bar tracks your running totals — playtime, everything gathered and crafted, foxes slain, buildings, ascensions and more."));
            return S;
        }
    }
}
