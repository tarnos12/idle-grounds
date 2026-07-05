# Idle Grounds — Economy & Building Design (agreed 2026-07-05)

The approved full-economy plan. Implemented parts are marked ✅; the rest is
the roadmap. Max 3 resource types per cost/recipe, everywhere.

## Resource tiers

**Gathered (raw):** wood, leaves, stone, clay, bamboo* (rare from Spirit
Tree ✅), rice (rename of wheat, pending), cotton, sand, iron ore,
jade shard* (rare from quarry/stone field ✅), firestone*, koi (rename of
fish, pending), algae, spring water (rename, pending), spirit herb,
spirit essence, beast bone* (from tier-2 beasts, lured with Beast Bait).

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
Gives the Loom robe + Mill bun + Brewery wine chains a purpose. Next
frontiers: more regions, deeper prestige, sprite art (see wishlist).

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
