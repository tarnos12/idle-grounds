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

**Dragon pills** (Pill Furnace; fed to the dragon → global buff, 60s base,
upgradeable to ~3 min via a Dragon Affinity node; new pill replaces active):
Ember (elixir+firestone → smelters 2×), Verdant (elixir+herb → regrow 2×),
Swiftwind (elixir+cotton → wisps 2×), Stoneheart (elixir+spirit stone →
double mining drops).

**Fuel (planned):** Forge/Kiln/Star Anvil/Pill Furnace burn fuel from a
gauge — wood 1×, charcoal 4×, firestone 12×; recipes then drop wood as an
input. The Furnace Spirit logistics building auto-stokes burners in ~3 cells.

## Wisp logistics ✅ (implemented)

Endpoints: **Gathering Stone** (vacuums ground items, radius 10 cells,
buffer 20), **Warding Seal** (typed pass-through, cap 5; right-click with
an item tunes it), **Storehouse** (typed buffer; `lock` keeps type when
empty), any **converter** (accepts only its remaining recipe inputs, up to
queue cap). **Wisp Lantern** holds a LIST of links {from,to}; every beat
(1s base, upgradeable) it services ONE link round-robin in added order,
sending 1 item the target accepts; wisps physically fly the cargo
(~120 px/s) and drop it on the ground if the target refuses on arrival.
Left-click any buffer building withdraws into the hand. Demolition severs
links and refunds buffers. Still missing: an in-game UI to create/edit
links (engine hook: `ENGINE.addLink`), rate/speed upgrades, Furnace Spirit.

**Starter network ✅:** fresh saves auto-build a wired demo (flag
`GS.starterPlaced`): gatherers at quarry/spirit tree/clay field/fox zone;
seals keeping the stone and wood lines pure; typed storehouses catching the
rare jade shards & bamboo; wood fanned round-robin to Forge → Workbench →
Paper Mill → Kiln; clay → Kiln; essence → Infusion Array. Sources are
seeded so every line visibly runs from the first minute.

## Roadmap phases

1. ✅ Logistics core + starter network + Kiln/Paper Mill/Infusion Array.
2. Link-editing UI (click lantern → pick source → pick target); lantern
   rate/wisp upgrades in the tree.
3. Loom, Cauldron, Mill, Brewery, Jade Carver, Charcoal Pit + fuel system
   + Furnace Spirit; renames (rice/koi/spring water).
4. Pill Furnace + dragon pill buffs; Star Anvil; beast bait → tier-2 beast.
5. Talisman Atelier, treasures, Dragon Shrine, Ascension Gate + prestige.
