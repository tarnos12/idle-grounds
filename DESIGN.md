# Idle Grounds — Economy & Building Design (agreed 2026-07-05)

The approved full-economy plan. Implemented parts are marked ✅; the rest is
the roadmap. Max 3 resource types per cost/recipe, everywhere.

## Resource tiers

**Gathered (raw):** wood, leaves, stone, clay, bamboo* (rare from Spirit
Tree ✅; also common from bamboo stalks in the Spirit Grove ✅), rice
(rename of wheat ✅), cotton, sand, iron ore,
jade shard* (rare from quarry/stone field ✅), firestone*, koi (rename of
fish ✅), algae, spring water (rename ✅), spirit herb,
spirit essence, beast bone* (from tier-2 beasts, lured with Beast Bait),
star fragment* (break starrock in Celestial Peak ✅), moonpetal (chop
moonshrub in Celestial Peak ✅).

**Refined (T2):** ✅ iron bar (Forge), ✅ plank (Workbench), ✅ brick (Kiln),
✅ paper (Paper Mill: bamboo+wood for now, bamboo+water later),
✅ spirit stone (Infusion Array: 3 stone + essence — T3 by tier but live now),
tools, glass, cloth, rope, robe, flour, spirit buns, spirit wine, qi elixir,
vitality pill, beast bait, charcoal, jade, star steel.

**Altar-infused / T3:** spirit stone ✅, spirit jade, star steel, dragon pills.
**Treasures:** talisman, dragon scale (gift from the awakened dragon).

## Buildings

**T2 producers (10):** ✅ Forge (iron bar), ✅ Kiln (brick, glass later),
✅ Workbench (plank; tools later), ✅ Paper Mill, Loom (cloth/rope/robe),
Mill (flour/buns), Brewery (spirit wine), Jade Carver, Cauldron
(qi elixir / vitality pill / beast bait), Charcoal Pit (fuel).

**T3 producers (4):** ✅ Infusion Array (spirit stone; spirit jade later),
Star Anvil (star steel), Pill Furnace (4 dragon pills), Talisman Atelier.
The Altar stays purely the upgrade shrine — infusion is its own building.

**Endgame (2):** Ascension Gate (final monument: talisman + star steel +
dragon scale; prestige hook), Dragon Shrine (dragon scales, longer buffs).

**Dragon pills ✅** (Pill Furnace; fed to the dragon → global buff, 60s base,
+30s per Dragon Affinity level; new pill replaces active):
Ember (elixir+firestone → smelters 2×), Verdant (elixir+herb → regrow 2×),
Swiftwind (elixir+cotton → wisps 2×), Stoneheart (elixir+spirit stone →
double mining drops).

**Fuel ✅ (implemented):** Forge/Kiln/Star Anvil/Pill Furnace burn fuel from a
gauge — wood 1×, charcoal 4×, firestone 12×; recipes then drop wood as an
input. The Furnace Spirit logistics building auto-stokes burners in ~3 cells.

## Wisp logistics ✅ (implemented)

Endpoints: **Gathering Stone** (vacuums ground items, radius 10 cells,
buffer 20), **Warding Seal** (typed pass-through, cap 5; right-click with
an item tunes it), **Storehouse** (typed buffer; `lock` keeps type when
empty), any **converter** (holds an INPUT STOCK per recipe item, cap 20 —
stockCap in the smelt config; batches start themselves whenever the stock
covers the recipe; feeding past the cap is refused). **Wisp Lantern** holds a LIST of links {from,to}; every beat
(1s base, upgradeable) it services ONE link round-robin in added order,
sending 1 item the target accepts; wisps physically fly the cargo
(~170 px/s, time-parametric so rendering is smooth at any framerate).
If the target refuses on arrival (e.g. the player hand-filled it
mid-flight) the wisp flies the cargo BACK to its source (red glow),
dropping it only if home is gone or full too.
Left-click any buffer building withdraws into the hand. Demolition severs
links and refunds buffers. Link editor ✅: left-click a lantern -> panel lists links (removable),
"Add link" = click a source building then a target on the map (rubber-band
line, invalid picks ignored, Esc backs out). Wisp Haste tree node ✅
(beat ×0.85 and wisp speed +25% per level); Furnace Spirit ✅.

**Starter network ✅:** fresh saves auto-build a wired demo (flag
`GS.starterPlaced`): gatherers at quarry/spirit tree/clay field/fox zone;
seals keeping the stone and wood lines pure; typed storehouses catching the
rare jade shards & bamboo; wood fanned round-robin to Forge → Workbench →
Paper Mill → Kiln; clay → Kiln; essence → Infusion Array. Sources are
seeded so every line visibly runs from the first minute.

## Roadmap phases

1. ✅ Logistics core + starter network + Kiln/Paper Mill/Infusion Array.
2. ✅ Link-editing UI + Wisp Haste tree node (beat -15%/lvl, speed +25%/lvl).
3. ✅ Loom/Cauldron/Mill/Brewery/Jade Carver/Charcoal Pit; ✅ fuel system
   (burners: Forge+Kiln, FUEL wood 10s / bamboo 6s / charcoal 40s, cap 60s,
   batch consumes its duration); ✅ Furnace Spirit; ✅ renames.
4. ✅ Pill Furnace + 4 dragon pills (fed to the dragon -> timed global
   blessing, 60s +30s/Dragon Affinity lvl, new pill replaces); ✅ Star
   Anvil (star steel); ✅ firestone (rare from mine ore/veins, top fuel);
   ✅ Beast Bait -> Spirit Boar (8hp, beast bones).
5. ✅ Talisman Atelier (paper+spirit jade+elixir -> talisman); ✅ Dragon
   Scales shed by the awakened dragon (45s, cap-5 pile, 2x with a
   shrine); ✅ Dragon Shrine (+60s blessings); ✅ Ascension Gate
   (3 talisman + 3 star steel + 3 scale) -> ASCEND: reset, keep +8%
   permanent global speed per ascension (tutorial skipped on reruns).

THE FULL ROADMAP IS IMPLEMENTED, plus DISCIPLES (post-roadmap):
Meditation Pavilion recruits disciples with Robes and feeds them Spirit
Buns (or Spirit Wine, worth 3x — closing the Brewery loop) to cultivate
Spirit Essence passively. Disciple Mastery tree node (+2 cap/level).
Gives the Loom robe + Mill bun + Brewery wine chains a purpose. Tools/Glass/Rope are now advanced build materials (Pill Furnace needs
tools, Star Anvil tools+glass, Talisman Atelier glass, Pavilion rope);
Vitality Pill is a combat consumable (quaff -> Martial Vigor: +2 attack
& 2x beast loot, 45s). Every produced item now has a consumer.

✅ Offline / idle catch-up (post-roadmap): a closed tab keeps producing.
On load the engine replays the passive economy for the away-time (virtual
clock over the real gameTick/automationTick; capped 8h, bounded compute)
and a "Welcome back" modal lists the gains. Only cap-bounded passive output
accrues (generators, queued converters, wisps, disciples, automation).

✅ Feedback juice (post-roadmap): cosmetic-only floating "+N" numbers and
spark bursts (ui.js FX layer; engine dropGround exposes a window.onGroundDrop
hook). Fires on harvest yields, crafts, loot, pickups and swings; gold
sparkle for prized loot; suppressed during offline catch-up.

✅ Deeper prestige — Ascension Shrine (post-roadmap): ascending grants
Ascension Points (1 + unlocked regions beyond Center); a perk shop (☯ pill)
spends them on permanent perks that persist across resets — Eternal Haste
(global speed), Master's Hall (disciple cap), Long Slumber (offline hours),
Fleet Hands (carry capacity). Perks in js/data.js PERKS; add more by wiring
one point in engine.js each.

✅ Converter UI overhaul (post-roadmap): burners show a visible 2x2 fuel
rack (FIFO — fed at the front, burned from the back right-to-left, "No fuel"
label when empty); every converter's face shows centred input icons
(have/need), a result icon with the crafts the current stock can still make
(fuel ignored), and a 1-cell progress bar. Recipe picker is an icon-grid
popup with a bottom-right hover detail. Fuel is a machine resource, not a
recipe input.

✅ Volcano region (post-roadmap): a new pannable region at world grid (2,1)
— bottom-right, directly below the Mine — unlocked for 3 iron bars. It
yields **Obsidian** (new item) from obsidian rocks and **Firestone**
(existing premium fuel) from fire veins. Obsidian feeds a new Kiln recipe
**Obsidian Glass** (1 obsidian → 2 glass — obsidian is volcanic glass),
giving the region an immediate sink into the existing glass economy.

✅ Spirit Grove region (post-roadmap): a new pannable region at world grid
(0,1) — bottom-left, directly below the Farm — unlocked for 12 rice + 12
wood. It **completes the 3x2 map** (no more void corners). A gathering
region: herb bushes yield **Spirit Herb** and bamboo stalks yield **Bamboo**
— both existing, previously-scarce mid-game inputs (Spirit Herb feeds
Robe/Qi Elixir/Vitality/Verdant Pill; Bamboo feeds Paper and doubles as a
fuel), so it relieves two bottlenecks with **no new items**.

✅ Depth & QoL batch (post-roadmap): two new Ascension Shrine perks —
**Frugal Frontier** (region unlock costs −20%/level, max 3) and **Ember
Heart** (burners consume fuel 15% slower/level, max 4) — each wired at one
engine point (areaUnlockCost / burnFuel). **Awakening blessing:** fully
awakening the Sleeping Dragon (GS.won) now grants a permanent ~11% global
speed boost (folded into prestigeFactor), so finishing the dragon story
finally pays off. **📊 Stats panel:** a top-bar button opens a modal of
lifetime stats (playtime, totals, fox kills, buildings, upgrades, disciples,
wisp links, recipe switches, ascensions + points, regions unlocked, carry).

✅ Furnace layout + 2 perks (post-roadmap): the four fuel burners (Forge,
Kiln, Pill Furnace, Star Anvil) grew **3x4 → 3x5**, and their fuel slots
moved off the top of the footprint into a **2-col × 3-row, 6-slot rack
drawn on the LEFT, outside the footprint** (visual only, not collision) —
so the crafting face now fills the whole 3x5 building. Two new Ascension
Shrine perks: **Ascendant Insight** (+1 Ascension Point per ascension per
level, max 3 — wired in engine `ascendReward`) and **Keen Automation**
(automation harvests +1 extra node/tick per level, max 3 — wired in
`automationTick`).

✅ Victory/ending overlay (post-roadmap): fully awakening the Sleeping
Dragon now shows a dedicated ending modal (`#ending-modal`, driven by
`maybeShowEnding`) instead of just the passive speed blessing.

✅ Bug-audit fixes (post-roadmap): firestone now routes correctly as fuel
vs. recipe ingredient, automation no longer harvests fixtures (Spirit
Tree/quarry rock/spring), spirit_buns has its own offline-tally key, and
the converter progress bar is prestige-aware.

✅ Procedural WebAudio SFX (post-roadmap): `js/audio.js` (`window.AUDIO`)
generates 13 sounds procedurally via a `window.onSfx` hook, plus a mute
toggle.

✅ Celestial Peak region (post-roadmap): a new late-game region ☁️ at
`WORLD.regions.celestial` (`rx:1, ry:2`, below Fishing — `WORLD.rows` grew
2→3, `unlockSide.celestial: "down"`, cost `spirit_stone:6 + jade:3 +
glass:3`). Two new resources: **star fragment** ☄️ (break the `starrock`
spawner, target 8, 3 hits, 1-2/drop, 5% rare firestone) and **moonpetal**
💮 (chop the `moonshrub` spawner, target 10, 2 hits, 1/hit). Two new
alternate late-game recipes so the T3 chains don't dead-end on beast-bone
supply: Star Anvil's "Astral Steel" (`star_fragment:3 + iron_bar:2` →
star_steel, 9s, no beast_bone) and Cauldron's "Moon Elixir"
(`moonpetal:2 + water:1` → qi_elixir, 8s). Two new Ascension Shrine perks (js/data.js `PERKS`): **bless** (dragon-pill
blessing duration x1.2 per level, max 3 — wired into the dragon-pill
duration calc in `dropFromHand`) and **bounty** (generator interval x0.9
per level, max 3 — wired into `gameTick`'s `genTimers` line).

Next frontiers: sprite art (see wishlist), more perks.

## Building sprite wishlist (assets/buildings/<file>)

PNG + transparency; native sizes assume 16 px/cell (clean multiples OK;
16x16 pieces can be scaled chunky). Missing files keep their emoji.

| Filename | Footprint | Native px | Depicts | Priority |
|---|---|---|---|---|
| storehouse.png | 3x2 | 48x32 | wooden chest | now |
| workbench.png | 3x2 | 48x32 | crafting bench | now |
| forge.png | 3x2 | 48x32 | smithy furnace | now |
| kiln.png | 3x2 | 48x32 | pottery kiln | now |
| paper_mill.png | 3x2 | 48x32 | mill / scribe hut | now |
| infusion_array.png | 3x2 | 48x32 | glowing formation circle | now |
| gathering_stone.png | 1x1 | 16x16 | runed magnet-stone | now |
| wisp_lantern.png | 1x1 | 16x16 | paper lantern | now |
| warding_seal.png | 1x1 | 16x16 | talisman on a post | now |
| altar.png | 5x5 | 80x80 | stone upgrade shrine | now |
| dragon_sleeping.png | 5x5 | 80x80 | curled sleeping dragon | now |
| dragon_awake.png | 5x5 | 80x80 | awake dragon, gold accents | now |
| algae_farm.png | 3x2 | 48x32 | floating rack in water | unlockable |
| herb_garden.png | 3x2 | 48x32 | planter beds | unlockable |
| spirit_tree.png | 4x4 fixture | 64x80 | grand glowing tree | fixture |
| quarry_rock.png | 2x2 fixture | 32x32 | big boulder | fixture |
| spring.png | 2x2 fixture | 32x32 | water spring | fixture |
| fox_spirit.png | enemy | 16-24 sq | fox spirit | creature |
| wisp.png | carrier | 8-16 sq | glowing soul orb | creature |
| loom.png / cauldron.png / mill.png / brewery.png / jade_carver.png / charcoal_pit.png | 3x2 | 48x32 | phase-3 producers | future |
| star_anvil.png / pill_furnace.png / talisman_atelier.png | 3x2 | 48x32 | T3 producers | future |
| ascension_gate.png / dragon_shrine.png | 5x5 | 80x80 | endgame monuments | endgame |
