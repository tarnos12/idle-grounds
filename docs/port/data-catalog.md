# Idle Grounds — Data Catalog (port reference)

Source: `old-game/js/data.js` (848 lines, VERSION num 52) + `old-game/DESIGN.md`.
All numbers/keys are transcribed exactly. `window.DATA` exports (data.js:843-848):
`ITEM_NAMES, ITEM_ICONS, TIER_SPRITES, AREAS, GRID, ZONES, BUILDINGS, DRAGON_STAGES, DRAGON_BUFFS, VITALITY, WORLD, AUTOMATION_CLICKS, FUEL, FUEL_CAP, FUEL_SLOTS, UPGRADE_TREE, QUESTS, QUEST_CHAIN, REVEAL, SOURCES, HAND_CAP, PERKS, TEST, VERSION, VOWS, VOW_MULT, GATE_OFFERINGS`.
Not exported but defined: helper `d(item,min,max)` (line 86), `_N,_T,_TL,_TR,_BL,_BR,_CENTRE` (282-287).

## Section index (line ranges)

| Section | Lines |
|---|---|
| ITEM_NAMES / ITEM_ICONS | 11-52 |
| FUEL, FUEL_CAP, FUEL_SLOTS | 56-58 |
| DRAGON_BUFFS (pills) | 63-68 |
| VITALITY | 72 |
| TIER_SPRITES | 75-83 |
| `d()` drop helper + interaction docs | 85-99 |
| AREAS (nodes, fixtures, generators, enemies) | 100-261 |
| GRID, zone constants, ZONES | 268-315 |
| BUILDINGS | 321-467 |
| DRAGON_STAGES (tributes) | 472-481 |
| WORLD (regions, unlockSide, unlockCost) | 483-508 |
| UPGRADE_TREE | 520-583 |
| QUEST_CHAIN, QUESTS | 599-677 |
| REVEAL | 686-708 |
| SOURCES | 713-760 |
| AUTOMATION_CLICKS, HAND_CAP | 764, 766 |
| PERKS | 774-807 |
| VOWS, VOW_MULT, GATE_OFFERINGS | 815-824 |
| TEST | 829-833 |
| VERSION | 838-841 |

Note: the starter network layout is NOT in data.js (it is built by the engine; see "Not in data.js" at the end).

---

## 1. Items (data.js:11-52, FUEL at 56)

Icon file = `old-game/assets/icons/<key>.png` if it exists (see section 17), else emoji fallback. Item keys are stable ids (renames: wheat=Rice, fish=Koi, water=Spring Water). Unknown keys fall back to title-cased name / 📦. Total 50 items.

| key | name | emoji | icon png | fuel value (ms) |
|---|---|---|---|---|
| wood | Wood | 🪵 | wood.png | 10000 |
| leaves | Leaves | 🍃 | leaves.png | |
| wheat | Rice | 🌾 | wheat.png | |
| cotton | Cotton | ☁️ | cotton.png | |
| stone | Stone | 🪨 | stone.png | |
| clay | Clay | 🧱 | clay.png | |
| sand | Sand | 🟡 | sand.png | |
| iron_ore | Iron Ore | 🔩 | iron_ore.png | |
| iron_bar | Iron Bar | 🧲 | iron_bar.png | |
| fish | Koi | 🐟 | fish.png | |
| algae | Algae | 🪸 | algae.png | |
| water | Spring Water | 💧 | water.png | |
| spirit_essence | Spirit Essence | ✨ | spirit_essence.png | |
| spirit_herb | Spirit Herb | 🌱 | spirit_herb.png | |
| bamboo | Bamboo | 🎍 | bamboo.png | 6000 |
| jade_shard | Jade Shard | 🟢 | jade_shard.png | |
| plank | Plank | 🟫 | plank.png | |
| brick | Brick | 🧱 | brick.png | |
| paper | Paper | 📜 | paper.png | |
| spirit_stone | Spirit Stone | 🔮 | spirit_stone.png | |
| tools | Tools | ⛏️ | tools.png | |
| glass | Glass | 🧊 | glass.png | |
| spirit_jade | Spirit Jade | 🟩 | spirit_jade.png | |
| cloth | Cloth | 🧶 | (none) | |
| rope | Rope | 🪢 | (none) | |
| robe | Robe | 🥋 | (none) | |
| flour | Rice Flour | 🍚 | (none) | |
| spirit_buns | Spirit Buns | 🥟 | (none) | |
| spirit_wine | Spirit Wine | 🍶 | (none) | |
| qi_elixir | Qi Elixir | 🧪 | (none) | |
| vitality_pill | Vitality Pill | 💊 | (none) | |
| beast_bait | Beast Bait | 🪱 | (none) | |
| charcoal | Charcoal | ⚫ | (none) | 40000 |
| jade | Jade | 💚 | (none) | |
| firestone | Firestone | 🌋 | (none) | 120000 |
| beast_bone | Beast Bone | 🦴 | (none) | |
| star_steel | Star Steel | ⚔️ | (none) | |
| ember_pill | Ember Pill | 🔴 | (none) | |
| verdant_pill | Verdant Pill | 🟢 | (none) | |
| swiftwind_pill | Swiftwind Pill | 🟡 | (none) | |
| stoneheart_pill | Stoneheart Pill | 🟣 | (none) | |
| talisman | Talisman | 🧧 | (none) | |
| dragon_scale | Dragon Scale | 🔶 | (none) | |
| obsidian | Obsidian | 🔲 | (none) | |
| star_fragment | Star Fragment | ☄️ | (none) | |
| moonpetal | Moonpetal | 💮 | (none) | |

Per-item stack/rarity flags: NONE in data.js (no stack size field; carrying is capped by HAND_CAP / building caps). "Rare" is expressed only via `rareDrop` entries on nodes. Items fall into: raw gathered, refined, treasure (talisman, dragon_scale), consumables (pills, vitality_pill, beast_bait).

### Fuel (data.js:56-58)

| const | value |
|---|---|
| FUEL (burn-ms per item) | `{ wood: 10000, bamboo: 6000, charcoal: 40000, firestone: 120000 }` |
| FUEL_CAP (legacy, superseded) | 60000 |
| FUEL_SLOTS | 6 (rack is 3x2 grid per comment; DESIGN.md says 2 cols x 3 rows drawn LEFT of the building, outside footprint, visual only) |

A batch consumes its own duration in burn-ms. Fed to any `fuel: true` building or a Furnace Spirit.

### Item sources hint text (SOURCES, data.js:713-760)

Verbatim; one per item (tested to cover every ITEM_NAMES key).

| key | hint |
|---|---|
| wood | Spirit Tree 🌳 (Center, top) + fallen logs |
| leaves | chop bushes 🌿 around the Altar |
| wheat | Farm rice plots 🌾 |
| cotton | Farm cotton patches ☁️ |
| stone | quarry rock ⛰️ (bottom-left) · Mine rocks |
| clay | clay field (Center, bottom-right) · Mine rocks |
| sand | Farm sand band (left side) |
| iron_ore | Mine ⛓️ iron veins |
| iron_bar | Mine ⛓️ iron veins → Forge |
| fish | Fishing: click surfacing koi 🐟 |
| algae | Fishing: click surfacing algae 🪸 · Algae Farm |
| water | Fishing spring ⛲ (top-left) |
| spirit_essence | slay Fox Spirits 🦊 (top-right) · disciples |
| spirit_herb | Spirit Grove bushes · Herb Garden |
| bamboo | Spirit Tree rare drop · Spirit Grove stalks |
| jade_shard | Mine jade veins 🟢 · quarry rock rare drop |
| plank | Workbench ← Wood |
| brick | Kiln ← Clay |
| paper | Paper Mill ← Bamboo + Wood |
| spirit_stone | Infusion Array ← Stone + Spirit Essence |
| tools | Workbench ← Planks + Iron Bar |
| glass | Kiln ← Sand (Farm) or Obsidian |
| spirit_jade | Infusion Array ← Jade + Spirit Essence |
| cloth | Loom ← Cotton |
| rope | Loom ← Cotton + Algae |
| robe | Loom ← Cloth + Spirit Herb |
| flour | Mill ← Rice |
| spirit_buns | Mill ← Rice Flour + Spring Water |
| spirit_wine | Brewery ← Rice + Spring Water + Leaves |
| qi_elixir | Cauldron ← Spirit Herb + Water + Essence |
| vitality_pill | Cauldron ← Koi + Spirit Herb + Water |
| beast_bait | Cauldron ← Koi + Algae |
| charcoal | Charcoal Pit ← Wood |
| jade | Jade Carver ← Jade Shards |
| firestone | Volcano fire veins · rare in the Mine |
| beast_bone | Spirit Boar 🐗 (lure one with Beast Bait) |
| star_steel | Star Anvil ← Iron Bars + Firestone + Beast Bone |
| ember_pill | Pill Furnace ← Qi Elixir + Firestone |
| verdant_pill | Pill Furnace ← Qi Elixir + Spirit Herb |
| swiftwind_pill | Pill Furnace ← Qi Elixir + Cotton |
| stoneheart_pill | Pill Furnace ← Qi Elixir + Spirit Stone |
| talisman | Talisman Atelier ← Paper + Spirit Jade + Qi Elixir |
| dragon_scale | shed by the awakened dragon 🐉 |
| obsidian | Volcano obsidian rocks |
| star_fragment | Celestial Peak star rocks ☄️ |
| moonpetal | Celestial Peak moon shrubs 💮 |

### Dragon pills (DRAGON_BUFFS, data.js:63-68) and Vitality (72)

Feed one pill to the dragon (right-click) -> timed GLOBAL buff: 60s base, +30s per Dragon Affinity level (Dragon Shrine +60s per DESIGN; perk `bless` x1.2/level); a new pill replaces the active one. Effects wired in the engine by pill id.

| pill id | buff name | desc |
|---|---|---|
| ember_pill | Ember Blessing | Burners work twice as fast. |
| verdant_pill | Verdant Blessing | Nodes regrow twice as fast. |
| swiftwind_pill | Swiftwind Blessing | Wisps fly and send twice as fast. |
| stoneheart_pill | Stoneheart Blessing | Mining and quarry drops doubled. |

`VITALITY = { item: "vitality_pill", name: "Martial Vigor", ms: 45000, bonusDamage: 2, lootMult: 2 }` — self-taken combat consumable (right-click in hand): +2 attack damage, doubled beast loot, 45000 ms.

---

## 2. Drop spec helper and node interaction types (data.js:85-99)

`d(item, min, max)` -> `{ item, min, max }`; `max` defaults to `min`.

Interaction types:
- `chop` — multiple swings; each swing yields `perHit`, the felling swing also grants `drops`; then node relocates.
- `instant` — one click harvests whole `drops`.
- `break` — strikes yield nothing until the last of `hits` cracks it.
- `surface` — surfaces for `surfaceWindow` seconds; click while up to land `drops`.
- `quarry` — fixed object; every `clicksPerDrop` clicks yields `dropMin..dropMax` (default 1) of `drop`.

Area field legend: `spawners` = relocating nodes (own zone, sizes, target, interaction); `fixtures` = fixed objects placed once; `generators` = auto-spawn ground items; `noBuild` = zone(s) buildings avoid; `enemies` = roaming beasts (click to fight). `useTiers: true` = spawner takes its stats from the area's `tiers[0]` entry. `scaleWithArea: false` = target count not scaled with area size. `swingMs` = ms per swing/click cycle for the player's harvest of that node. `regrow` = respawn seconds (and `tiers[].timer` for tier-based); `spacing` = min spread.

TIER_SPRITES (data.js:75-83): center "🌳", farm "🌾", mine "🪨", fishing "🐟", volcano "🌋", grove "🎋", celestial "☄️".

---

## 3. AREAS (data.js:100-261) — per region

Common area fields: name, icon, verb, actionIcon, base (the area's headline item), noBuild, speedLabel, timerLabel. Zones named here are defined in section 4.

### 3.1 center (100-152)
- name "Center", icon 🌲, verb "Chop", actionIcon 🪓, base "wood", noBuild `["corners","midTop"]`, speedLabel "Regrow Speed", timerLabel "Regrow".
- tiers: `{ name:"Oak", hits:3, perHit:[d(wood,1,2)], drops:[], timer:15 }` (note: tier node not used by spawners below; kept as data).
- Spawners:

| kind | zone | sizes | target | scaleWithArea | spacing | interaction | swingMs | sprite | hits | regrow | perHit | drops |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| bush | centre | [1] | 10 | false | 6 | chop | 300 | 🌿 | 2 | 12 | leaves 1 | leaves 1-2 |

- Fixtures:

| kind | zone | size | interaction | swingMs | sprite | clicksPerDrop | drop | dropMin/Max | autoTap | rareDrop |
|---|---|---|---|---|---|---|---|---|---|---|
| quarry (rock) | cornerBL | 2 | quarry | 350 | ⛰️ | 5 | stone | (default 1) | - | jade_shard 0.12 |
| spirittree | midTop | 4 | quarry | 350 | 🌳 | 3 | wood | 2 / 3 | true (Center Automation taps once per tick per level) | bamboo 0.12 |

  Quarry rock: manually minable without limit (hold auto-clicks ~3/s) AND produces stone passively via generator below. Spirit Tree: no passive production.
- Generators (ground-item spawners; `cap` = max items lying in zone):

| kind | zone | item | intervalMs | cap | upgrade | rareDrop |
|---|---|---|---|---|---|---|
| clay | clayField | clay | 1500 | 10 | - | - |
| stone | quarryField | stone | 1500 | 10 | "quarry" (upgrade node speeds it) | jade_shard 0.08 |
| wood | woodField | wood | 3000 | 10 | - | - |

- Enemies: `{ zone:"cornerTR", name:"Fox Spirit", sprite:"🦊", cap:1, hp:3, speed:30, respawnMs:6000, attackMs:400, drops:[d(spirit_essence,1,2)] }`. `cap` is BASE cap (raised by enemyCap/damage/aoe upgrades). Wander zone, take hits to slay.
  - baitSpawn (right-click Beast Bait inside the zone): `{ name:"Spirit Boar", sprite:"🐗", hp:8, speed:18, drops:[d(beast_bone,1,2), d(spirit_essence,1)] }` — tier-2 beast, only Beast Bone source.

### 3.2 farm (153-173)
- name "Farm", icon 🌱, verb "Harvest", actionIcon 🌾, base "wheat", noBuild `["centre","midLeft"]`, speedLabel "Growth Speed", timerLabel "Growth".
- tiers: `{ name:"Rice", drops:[d(wheat,2,3)], timer:20 }`
- Spawners:

| kind | zone | sizes | target | scaleWithArea | interaction | useTiers | swingMs | sprite | regrow | drops |
|---|---|---|---|---|---|---|---|---|---|---|
| crop | centre | [3] | 8 | false | instant | true (Rice tier: wheat 2-3, timer 20) | 300 | (none; wheat emoji) | (tier timer 20) | (tier) |
| cotton | centre | [2] | 5 | false | instant | - | 300 | ☁️ | 18 | cotton 1-2 |

- Generators: `{ kind:"sand", zone:"sandField", item:"sand", intervalMs:1500, cap:10 }`.
- No fixtures, no enemies.

### 3.3 mine (174-193)
- name "Mine", icon ⛰️, verb "Mine", actionIcon ⛏️, base "stone", noBuild `"centre"` (string, not array), speedLabel "Mining Speed", timerLabel "Respawn".
- tiers: `{ name:"Stone", hits:2, drops:[d(stone,3), d(clay,1)], timer:10 }`
- Spawners:

| kind | zone | sizes | target | interaction | useTiers | swingMs | sprite | hits | regrow | drops | rareDrop |
|---|---|---|---|---|---|---|---|---|---|---|---|
| ore | centre | [1,2] | 10 | break | true (Stone tier: hits 2, stone 3 + clay 1, timer 10) | 450 | - | (tier 2) | (tier 10) | (tier) | firestone 0.05 |
| ironvein | centre | [2] | 2 | break | - | 500 | ⛓️ | 3 | 12 | iron_ore 1-2 | firestone 0.15 |
| jadevein | centre | [2] | 1 | break | - | 500 | 🟢 | 3 | 14 | jade_shard 1-2 | - |

### 3.4 fishing (194-218)
- name "Fishing", icon 🎣, verb "Reel", actionIcon 🎣, base "fish", noBuild `"centre"`, `surfaceWindow: 3` (seconds a fish stays up), speedLabel "Fishing Speed", timerLabel "Bite".
- tiers: `{ name:"Koi", drops:[d(fish,1,2)], timer:12 }`
- Spawners:

| kind | zone | sizes | target | interaction | useTiers | swingMs | sprite | regrow | drops |
|---|---|---|---|---|---|---|---|---|---|
| fish | centre | [1] | 4 | surface | true (Koi: fish 1-2, timer 12) | 350 | - | (tier 12) | (tier) |
| algae | centre | [1] | 10 | surface | - | 350 | 🪸 | 8 | algae 1-2 |

- Fixtures: `{ kind:"spring", zone:"cornerTL", size:2, interaction:"quarry", swingMs:350, sprite:"⛲", clicksPerDrop:3, drop:"water" }`
- Generators: `{ kind:"water", zone:"springField", item:"water", intervalMs:2000, cap:10 }`.

### 3.5 volcano (219-230)
- name "Volcano", icon 🌋, verb "Mine", actionIcon ⛏️, base "obsidian", noBuild `"centre"`, speedLabel "Cooling Speed", timerLabel "Reform".
- tiers: `{ name:"Obsidian", hits:3, drops:[d(obsidian,1,2)], timer:14 }`
- Spawners:

| kind | zone | sizes | target | interaction | swingMs | sprite | hits | regrow | drops |
|---|---|---|---|---|---|---|---|---|---|
| obsidian | centre | [1,2] | 8 | break | 500 | ⬛ | 3 | 14 | obsidian 1-2 |
| firevein | centre | [2] | 2 | break | 600 | 🌋 | 4 | 20 | firestone 1 (d(firestone,1,1)) |

### 3.6 grove (231-243) "Spirit Grove"
- name "Spirit Grove", icon 🎋, verb "Gather", actionIcon 🌿, base "spirit_herb", noBuild `"centre"`, speedLabel "Regrow Speed", timerLabel "Regrow".
- tiers: `{ name:"Spirit Herb", hits:2, drops:[d(spirit_herb,1,2)], timer:16 }`
- Spawners:

| kind | zone | sizes | target | interaction | swingMs | sprite | hits | regrow | perHit | drops |
|---|---|---|---|---|---|---|---|---|---|---|
| herbbush | centre | [1] | 10 | chop | 350 | 🌿 | 2 | 16 | spirit_herb 1 | spirit_herb 1-2 |
| bamboostalk | centre | [1,2] | 6 | break | 400 | 🎋 | 3 | 18 | - | bamboo 1-2 |

### 3.7 celestial (244-260) "Celestial Peak"
- name "Celestial Peak", icon ☁️, verb "Gather", actionIcon ✨, base "star_fragment", noBuild `"centre"`, speedLabel "Reform Speed", timerLabel "Reform".
- tiers: `{ name:"Star Fragment", hits:3, drops:[d(star_fragment,1,2)], timer:16 }`
- Spawners:

| kind | zone | sizes | target | interaction | swingMs | sprite | hits | regrow | perHit | drops | rareDrop |
|---|---|---|---|---|---|---|---|---|---|---|---|
| starrock | centre | [1,2] | 8 | break | 550 | ☄️ | 3 | 16 | - | star_fragment 1-2 | firestone 0.05 |
| moonshrub | centre | [1] | 10 | chop | 350 | 💮 | 2 | 18 | moonpetal 1 | moonpetal 1-2 | - |

---

## 4. World constants, GRID and ZONES (data.js:268-315)

`GRID = { cell: 32 (px/cell), cells: 93 (playable cells per region side), margin: 10 (inert border cells around whole map), gap: 5 (inert void cells between adjacent regions), building: { w:3, h:3 } (default footprint) }`.
`_N = 93, _T = 25` (corner block 25x25, decoupled from _N).

Zone rectangles (inclusive row/col bounds, per region grid of 93x93; computed from source formulas):

| zone | formula | r0..r1 | c0..c1 |
|---|---|---|---|
| cornerTL (_TL) | r0:0,c0:0,r1:_T-1,c1:_T-1 | 0..24 | 0..24 |
| cornerTR (_TR) | c0:_N-_T, c1:_N-1 | 0..24 | 68..92 |
| cornerBL (_BL) | r0:_N-_T | 68..92 | 0..24 |
| cornerBR (_BR) | | 68..92 | 68..92 |
| corners | [TL,TR,BL,BR] | | |
| centre (_CENTRE) | r0:_T,c0:_T,r1:_N-_T-1,c1:_N-_T-1 | 25..67 | 25..67 |
| midTop | r0:0,c0:_T,r1:_T-1,c1:_N-_T-1 | 0..24 | 25..67 |
| midLeft | r0:_T,c0:0,r1:_N-_T-1,c1:_T-1 | 25..67 | 0..24 |
| clayField | m=floor((_N-_T+_N-1)/2)=80, h=4; r/c = m±h | 76..84 | 76..84 |
| quarryField | m=80, c=floor((_T-1)/2)=12, h=4; rows m±h, cols c±h | 76..84 | 8..16 |
| springField | m=floor((_T-1)/2)=12, h=4 | 8..16 | 8..16 |
| woodField | literal `{r0:12,c0:40,r1:20,c1:48}` (9x9; Gathering Stone at (16,44)) | 12..20 | 40..48 |
| sandField | m=floor((_T+_N-_T-1)/2)=46, c=18, h=4 | 42..50 | 14..22 |

(Row first, col second; (row,col) = (r,c).) Zones `corners`, `cornerXX`, `centre`, `midTop`, `midLeft` are used by `noBuild` and spawner/fixture/enemy `zone` fields. Resource nodes spawn ONLY in their spawner's zone; buildings can go anywhere except `noBuild` zones (unless `anyZone: true`).

---

## 5. Buildings (data.js:321-467)

Default size = GRID.building 3x3. Footprint is `size {w,h}`. `cost` = resources dropped into the ghost (max 3 types). `unlocked: true` = listed (subject to REVEAL, section 11); `stageUnlock: n` = also listed when dragon stage >= n. Fields noted as flags below. Internal type key `center` displays as "Altar".

### 5.1 Overview table

| key | name | emoji | size WxH | cost | unlocked | stageUnlock | flags / behaviour |
|---|---|---|---|---|---|---|---|
| center | Altar | 🏛️ | 5x5 | {} | false | - | indestructible; pre-placed middle of centre region; upgrade tree anchor |
| dragon | Sleeping Dragon | 🐉 | 5x5 | {} | false | - | indestructible; pre-placed Center top-left corner; tribute stages |
| workbench | Workbench | 🛠️ | 3x3 | wood 8 | true | - | converter |
| kiln | Kiln | 🏺 | 3x5 | wood 10, clay 5 | true | - | converter, `fuel:true` |
| paper_mill | Paper Mill | 📜 | 3x3 | wood 10, stone 5 | true | - | converter |
| infusion_array | Infusion Array | 🔮 | 3x3 | stone 10, spirit_essence 5 | true | - | converter |
| loom | Loom | 🧵 | 3x3 | wood 10, plank 4 | true | - | converter |
| mill | Mill | 🌾 | 3x3 | wood 8, stone 6 | true | - | converter |
| brewery | Brewery | 🍶 | 3x3 | wood 8, clay 6 | true | - | converter |
| cauldron | Cauldron | ⚗️ | 3x3 | stone 8, iron_bar 2 | true | - | converter |
| jade_carver | Jade Carver | 🗿 | 3x3 | wood 6, stone 8 | true | - | converter |
| pill_furnace | Pill Furnace | 🫕 | 3x5 | brick 6, iron_bar 4, tools 2 | true | - | converter, `fuel:true` |
| star_anvil | Star Anvil | ⚒️ | 3x5 | iron_bar 6, tools 3, glass 2 | true | - | converter, `fuel:true` |
| talisman_atelier | Talisman Atelier | 🖌️ | 3x3 | plank 6, jade 2, glass 2 | true | - | converter |
| dragon_shrine | Dragon Shrine | 🐲 | 3x3 | brick 10, cloth 8, obsidian 4 | true | - | `shrine:true` (blessings +60s; dragon sheds Dragon Scales 2x as often while one stands) |
| ascension_gate | Ascension Gate | ⛩️ | 5x5 | talisman 3, star_steel 3, dragon_scale 3 | true | - | `gate:true` (final monument; offers ASCENSION; unique) |
| charcoal_pit | Charcoal Pit | 🕳️ | 3x3 | stone 6, clay 4 | true | - | converter (NOT a burner) |
| furnace_spirit | Furnace Spirit | 🕯️ | 1x1 | stone 4, spirit_essence 2 | true | - | `anyZone:true`; `stoker:{ radius:3, cap:20 }` auto-stokes fuel into burners within radius from own fuel buffer (wisp-suppliable) |
| meditation_pavilion | Meditation Pavilion | 🧘 | 3x3 | plank 6, cloth 4, rope 2 | true | - | `roster` (see 5.3) |
| forge | Forge | 🔥 | 3x5 | wood 5, stone 10 | **false** | **1** | converter, `fuel:true` |
| storehouse | Storehouse | 📦 | 3x3 | wood 12 | true | - | `cap:200` (typed buffer; lock keeps type when empty per DESIGN) |
| algae_farm | Algae Farm | 🪸 | 3x3 | wood 12, algae 6 | **false** | **2** | `waterOnly:true` (only in fishing centre zone); `gen:{ item:"algae", intervalMs:2000, cap:24 }` |
| herb_garden | Herb Garden | 🪴 | 3x3 | wood 10, water 5, clay 5 | **false** | **3** | `gen:{ item:"spirit_herb", intervalMs:2500, cap:24 }` (on land) |
| gathering_stone | Gathering Stone | 🧿 | 1x1 | stone 5 | true | - | `anyZone:true`; `gather:{ radius:8, cap:60 }` (cap was 20 -> 60) |
| wisp_lantern | Wisp Lantern | 🏮 | 1x1 | wood 5, stone 5 | true | - | `anyZone:true`; `lantern:{ rateMs:1000, speed:170 }` |
| warding_seal | Warding Seal | 🈯 | 1x1 | wood 3, stone 3 | true | - | `anyZone:true`; `seal:{ cap:20 }` (cap was 5 -> 20) |

Default for omitted fields: `size` omitted = 3x3; `stockCap` omitted = 20 per input item (feeding past cap refused; comment line 331-333). Burners (`fuel:true`) all use size `{w:3,h:5}` (comment says "3x4" in line 73 of GRID block, but each burner overrides to 3x5).

Per DESIGN.md: converters hold per-item INPUT STOCK (cap 20), batches auto-start when stock covers recipe; left-click recipe picker; switching recipe drops held stock on the ground. Max 3 resource types per cost/recipe. Fuel is a machine resource, not a recipe input.

### 5.2 Recipes per converter (inputs -> output x qty, timeMs)

Each recipe: `{ name, inputs, output, outputQty, timeMs }`. No per-recipe fuel field and no per-converter `stockCap` override appear in data.js (default 20 applies everywhere). Fuel need = the batch consumes its own `timeMs` in burn-ms from the fuel rack (see FUEL).

| building | recipe name | inputs | output | outputQty | timeMs |
|---|---|---|---|---|---|
| workbench | Plank | wood 3 | plank | 1 | 4000 |
| workbench | Tools | plank 2, iron_bar 1 | tools | 1 | 6000 |
| kiln (burner) | Brick | clay 2 | brick | 1 | 5000 |
| kiln | Glass | sand 3 | glass | 2 | 5000 |
| kiln | Obsidian Glass | obsidian 1 | glass | 2 | 5000 |
| paper_mill | Paper | bamboo 1, wood 2 | paper | 1 | 5000 |
| infusion_array | Spirit Stone | stone 3, spirit_essence 1 | spirit_stone | 1 | 8000 |
| infusion_array | Spirit Jade | jade 1, spirit_essence 2 | spirit_jade | 1 | 9000 |
| loom | Cloth | cotton 3 | cloth | 1 | 5000 |
| loom | Rope | cotton 2, algae 2 | rope | 1 | 5000 |
| loom | Robe | cloth 2, spirit_herb 1 | robe | 1 | 8000 |
| mill | Rice Flour | wheat 2 | flour | 1 | 4000 |
| mill | Spirit Buns | flour 2, water 1 | spirit_buns | 1 | 6000 |
| brewery | Spirit Wine | wheat 2, water 2, leaves 1 | spirit_wine | 1 | 8000 |
| cauldron | Qi Elixir | spirit_herb 1, water 2, spirit_essence 1 | qi_elixir | 2 | 8000 |
| cauldron | Vitality Pill | fish 1, spirit_herb 1, water 1 | vitality_pill | 1 | 7000 |
| cauldron | Beast Bait | fish 2, algae 2 | beast_bait | 1 | 6000 |
| cauldron | Moon Elixir | moonpetal 2, water 1 | qi_elixir | 2 | 8000 |
| jade_carver | Jade | jade_shard 3 | jade | 1 | 6000 |
| pill_furnace (burner) | Ember Pill | qi_elixir 1, firestone 1 | ember_pill | 1 | 9000 |
| pill_furnace | Verdant Pill | qi_elixir 1, spirit_herb 1 | verdant_pill | 1 | 9000 |
| pill_furnace | Swiftwind Pill | qi_elixir 1, cotton 1 | swiftwind_pill | 1 | 9000 |
| pill_furnace | Stoneheart Pill | qi_elixir 1, spirit_stone 1 | stoneheart_pill | 1 | 9000 |
| star_anvil (burner) | Star Steel | iron_bar 2, firestone 1, beast_bone 1 | star_steel | 2 | 10000 |
| star_anvil | Astral Steel | star_fragment 3, iron_bar 2 | star_steel | 2 | 9000 |
| talisman_atelier | Talisman | paper 2, spirit_jade 1, qi_elixir 1 | talisman | 1 | 10000 |
| charcoal_pit | Charcoal | wood 2 | charcoal | 1 | 4000 |
| forge (burner) | Iron Bar | iron_ore 2 | iron_bar | 1 | 6000 |

Note: firestone is a recipe INPUT in Ember Pill / Star Steel and also a fuel (FUEL.firestone 120000); engine routes it as fuel vs ingredient by context (DESIGN "bug-audit": "firestone now routes correctly as fuel vs. recipe ingredient").

### 5.3 Special building configs

- **meditation_pavilion.roster**: `{ cap:3, recruit:"robe", food:"spirit_buns", foodCap:20, foodValues:{ spirit_buns:1, spirit_wine:3 }, produce:"spirit_essence", produceMs:6000 }`. Each disciple costs 1 Robe (Recruit button); eats food (value = cycles fueled); produces spirit_essence every 6000 ms. Disciple Mastery upgrade +2 cap per pavilion per level; perk `hall` +1.
- **gathering_stone.gather**: radius 8 cells, total buffer cap 60.
- **wisp_lantern.lantern**: rateMs 1000 (services one link {from,to} round-robin per beat, sends 1 item), speed 170 px/s. Wisp Haste upgrade: beat x0.85 and speed +25% per level.
- **warding_seal.seal**: cap 20, locked to one item type.
- **furnace_spirit.stoker**: radius 3, cap 20.
- **storehouse.cap**: 200 (DESIGN: typed buffer, `lock`).
- **algae_farm / herb_garden gen**: see table (DESIGN: x3 cap & 2x speed in design pass 2, already reflected in values).
- **ascension_gate.gate**: unique (per DESIGN); see GATE_OFFERINGS section 13.
- **dragon_shrine.shrine**: see above.

---

## 6. Dragon stages / tributes (DRAGON_STAGES, data.js:472-481)

Max 3 resource types per stage. Indexed 0..3; completing stage index i sets dragon.stage = i+1. Tributes scale after ascension: x1/(1+0.25n), floor 0.4 (DESIGN, engine). Vow of the Restless Dragon doubles them. Stage 4 = awake (`GS.won`).

| idx | needs | text (verbatim, abridged only with "…" where the source has it) | unlocks |
|---|---|---|---|
| 0 | leaves 15 | "The dragon cracks one eye open… and teaches you the Forge." | Forge (stage 1) |
| 1 | stone 25, clay 10 | "The dragon yawns a plume of steam… and teaches you the Algae Farm." | Algae Farm (stage 2) |
| 2 | iron_bar 8, algae 15, water 10 | "The dragon tastes forged iron and rumbles approval. \"You shape the earth well, little cultivator. Grow me the herbs of spirit — I will teach you to garden what cannot be farmed.\" (Herb Garden unlocked)" | Herb Garden (stage 3) |
| 3 | spirit_herb 20, spirit_essence 15, iron_bar 5 | "The dragon breathes in the herbs, the essence, the iron — and OPENS ITS EYES. \"I dreamed a thousand years, and you woke me with patience, not swords. Cultivate on, little one. I will watch over these grounds.\" 🐲 THE DRAGON IS AWAKE." | Stage 4: dragon_shrine, ascension_gate reveal; dragon sheds Dragon Scales (45s, cap-5 pile, 2x with shrine per DESIGN); ending overlay; ~11% permanent global speed |

Stage-3 (index 2) iron_bar tribute is used by quest "iron" (`E.dragonTribute(2).iron_bar`).

---

## 7. World / regions (WORLD, data.js:483-508)

One continuous map; each region is a full 93x93 block; regions pan (no travel). `cols: 3, rows: 3` (region grid; bottom corners (0,2),(2,2) = void).

| region | rx | ry | position note | unlockSide | unlockCost (before TEST costScale) |
|---|---|---|---|---|---|
| center | 1 | 0 | start region | - (always open) | - |
| farm | 0 | 0 | left of centre | "left" | wood 10 |
| mine | 2 | 0 | right of centre | "right" | wood 16 |
| fishing | 1 | 1 | below centre | "down" | wood 20 |
| volcano | 2 | 1 | below the mine | "up" | iron_bar 3 |
| grove | 0 | 1 | below the farm | "down-left" | wheat 12, wood 8 |
| celestial | 1 | 2 | below fishing | "down" | spirit_stone 6, jade 3, glass 3 |

Notes: unlock cost is paid from the hand (so <= hand cap); region-unlock installments tracked in `GS.world.unlockPaid`; perk `frugal` -20%/level; TEST.costScale 0.5 applies. DESIGN.md text mentions "12 rice + 12 wood" for grove (older); source says wheat 12 + wood 8 — use source.
Pixel world origin/offset is not in data.js (engine: region pixel offset = f(rx,ry, cells, gap, margin)); suggestion: region origin cell = margin + rx*(cells+gap), margin + ry*(cells+gap).

---

## 8. Upgrade tree (UPGRADE_TREE, data.js:520-583)

Root = `hand`. Buying level 1 of a node unlocks its `links` neighbours. Visibility by graph distance from owned nodes: <=1 full, ==2 shows "?", >=3 hidden (debug toggle reveals). x/y = free-form pixel offsets from root (x right, y down). Each node has 3 levels (max level 3 = `costs.length`); `costs[i]` = {item:qty} for level i+1, paid at the Altar (multi-resource, max 3 types). TEST.costScale applies when enabled. `area` = which region's harvest/speed the node affects (type below). `type` = engine effect id.

| id | icon | name | x | y | area | type | desc | links | costs L1 / L2 / L3 |
|---|---|---|---|---|---|---|---|---|---|
| hand | ✋ | Hand Size | 0 | 0 | center | hand | +5 carry capacity per level. | spd_c, act_c, spd_f, spd_m, foe_cap, wisps | {wood10} / {wood25, leaves10} / {cotton15, wood40} |
| wisps | 🏮 | Wisp Haste | -170 | 115 | center | wispRate | Lanterns send more often and wisps fly faster. | affinity | {wood40, spirit_stone1} / {spirit_stone3, plank8} / {spirit_stone6, brick8} |
| affinity | 🐲 | Dragon Affinity | -300 | 190 | center | affinity | Dragon-pill blessings last +30s per level. | disciples | {qi_elixir2, wood40} / {spirit_stone3, qi_elixir3} / {spirit_jade1, qi_elixir5} |
| disciples | 🧘 | Disciple Mastery | -230 | 300 | center | discipleCap | +2 disciple capacity per Meditation Pavilion, per level. | - | {robe2, spirit_stone2} / {spirit_stone4, spirit_buns20} / {spirit_jade2, robe5} |
| spd_c | ⏱️ | Regrow Speed | 150 | -35 | center | speed | Center bushes respawn faster. | auto_c | {wood20} / {wood50, leaves15} / {wood120, spirit_essence10} |
| auto_c | 🤖 | Automation | 300 | -85 | center | automation | Auto-harvests Center bushes and taps the Spirit Tree 🌳 (1 swing/s per level) — its wood flows to the starter wood line. | act_fi | {wood60, stone30} / {stone120, clay40} / {iron_ore40, spirit_essence20} |
| act_fi | 🎣 | Reel Speed | 455 | -45 | fishing | harvestSpeed | Faster reeling when fishing. | - | {fish10, algae15} / {algae40, wood30} / {fish40, iron_ore15} |
| act_c | 🪓 | Action Speed | -150 | -35 | center | harvestSpeed | Faster chop/hold swings in the Center. | quarry | {wood15, stone5} / {stone40, leaves20} / {stone80, spirit_essence15} |
| quarry | ⛏️ | Quarry Output | -295 | 40 | center | quarry | The quarry produces stone faster. | act_m | {wood20, stone10} / {stone50, clay20} / {stone100, water25} |
| act_m | ⚒️ | Mine Speed | -450 | -15 | mine | harvestSpeed | Faster strikes in the Mine. | - | {stone30} / {stone60, clay25} / {iron_ore30, clay50} |
| spd_f | 💧 | Growth Speed | 40 | -150 | farm | speed | Farm crops regrow faster. | act_f | {wheat15, wood20} / {wheat40, water15} / {wheat80, cotton20} |
| act_f | 🌾 | Harvest Speed | -45 | -290 | farm | harvestSpeed | Faster crop harvesting. | auto_f | {wheat25} / {wheat50, sand15} / {cotton25, water20} |
| auto_f | 🚜 | Farm Automation | 55 | -430 | farm | automation | Auto-harvests Farm crops. | - | {wheat60, wood40} / {wheat120, cotton30} / {cotton60, water40, iron_ore20} |
| spd_m | ⛰️ | Respawn Speed | -40 | 150 | mine | speed | Mine nodes respawn faster. | spd_fi | {stone25, wood20} / {stone60, clay30} / {iron_ore25, water20} |
| spd_fi | 🌊 | Bite Speed | 50 | 290 | fishing | speed | Fish & algae surface more often. | auto_m | {fish10, wood20} / {algae30, clay20} / {fish30, water30} |
| auto_m | 🛠️ | Mine Automation | -40 | 430 | mine | automation | Auto-mines ore veins. | - | {stone80, clay30} / {iron_ore30, stone100} / {iron_ore60, water30, algae30} |
| foe_cap | 🦊 | Spirit Call | 160 | 120 | center | enemyCap | +1 Fox Spirit roams the grove per level. | foe_dmg | {leaves25, wood15} / {spirit_essence10, wood40} / {spirit_essence25, iron_bar5} |
| foe_dmg | ⚔️ | Spirit Blade | 315 | 205 | center | damage | +1 damage per strike on beasts. | foe_aoe | {spirit_essence5, stone20} / {spirit_essence15, iron_ore10} / {spirit_essence30, iron_bar8} |
| foe_aoe | 💥 | Spirit Wave | 470 | 300 | center | aoe | Strikes ripple outward, hitting nearby beasts (wider per level). | - | {spirit_essence12, water10} / {spirit_essence25, iron_bar5} / {spirit_essence50, iron_bar12} |

Prerequisites: a node unlocks when ANY node that lists it in `links` has level >= 1 (derived from `links`; `hand` is the owned root). Effect magnitudes by type: hand +5 carry/level; wispRate beat x0.85 & wisp speed +25%/level; affinity +30s/level; discipleCap +2/pavilion/level; automation clicks from AUTOMATION_CLICKS; damage +1/level; enemyCap +1/level; aoe widening per level (exact radius is engine); speed/harvestSpeed/quarry factors are in the engine (not in data.js).

---

## 9. Quests (QUEST_CHAIN = 2; QUESTS, data.js:599-677)

Sequential chain (side panel); each quest appears only after the previous is claimed. `goal()` reads live state: `{cur, need}`; claim enabled when cur >= need. `reward.reveal` is DISPLAY only (REVEAL drives the menu). `reward.items` land in hand (else beside Altar). `builds` = buildings asked for (build-menu target first). `target` = world object the quest ring pulses on. Saves index the array; QUEST_CHAIN bumps on reorder so saves remap by id.

| # | id | icon | name | goal (cur / need) | reward | builds | target |
|---|---|---|---|---|---|---|---|
| 1 | wood | 🪵 | First timber | handCount("wood") / 5 | reveal [storehouse] | - | {area:center, kind:fixture, id:spirittree} |
| 2 | leaves | 🍃 | Bush whacker | handCount("leaves") / 5 | - | - | - |
| 3 | dragon1 | 🐉 | Wake the sleeper | dragon.stage >= 1 ? 1:0 / 1 | reveal [forge] | - | {center, dragon} |
| 4 | fox | 🦊 | Fox hunt | stats.foxKills / 1 | reveal [infusion_array] | - | {center, enemyZone} |
| 5 | build | 🔨 | Raise a building | stats.buildingsBuilt / 1 | reveal [gathering_stone, wisp_lantern, warding_seal, workbench, kiln, paper_mill, charcoal_pit] | [storehouse] | - |
| 6 | upgrade | 🏛️ | First insight | stats.upgradesApplied / 1 | items {wood:10} | - | {center, altar} |
| 7 | link | 🏮 | Wisp wrangler | stats.linksAdded / 1 | reveal [furnace_spirit] | - | {center, building, id:wisp_lantern} |
| 8 | explore | 🔓 | Beyond the woods | (world.unlocked.farm OR mine OR fishing) ? 1:0 / 1 | - | - | - |
| 9 | dragon2 | 🐉 | Stone & clay for the dragon | dragon.stage >= 2 ? 1:0 / 1 | reveal [algae_farm], items {wood:8} | - | {center, dragon} |
| 10 | iron | 🧲 | Iron for the dragon | need = max(1, dragonTribute(2).iron_bar \|\| 1); if dragon.stage>=3 -> {cur:need,need}; paid = (stage===2 ? dragon.paid.iron_bar\|\|0 : 0); cur = min(need, handCount("iron_bar") + paid) | - | [forge] | - |
| 11 | waters | 🎣 | Unlock the waters | world.unlocked.fishing ? 1:0 / 1 | reveal [loom, mill, brewery] | - | - |
| 12 | dragon3 | 🐉 | The dragon tastes iron | dragon.stage >= 3 ? 1:0 / 1 | reveal [herb_garden, pill_furnace, star_anvil, talisman_atelier] | - | {center, dragon} |
| 13 | weaver | 🪢 | Weaver's path | min(handCount("rope"),2) + min(handCount("cloth"),6) / 8 | reveal [meditation_pavilion], items {spirit_herb:2} | [loom] | {center, building, id:loom} |
| 14 | cultivate | 🧘 | Gather disciples | stats.disciplesRecruited / 1 | - | [meditation_pavilion, loom] | {center, building, id:meditation_pavilion} |

Quest descriptions (verbatim):
1. wood: "Hold left-click on the big Spirit Tree 🌳 (top of the Center) to chop it, then hold left-click near the fallen wood to vacuum 5 into your hand. (WASD to look around, mouse-wheel to zoom.)"
2. leaves: "Chop the small bushes 🌿 around the Altar and collect 5 leaves."
3. dragon1: "Carry leaves to the Sleeping Dragon (top-left corner) and RIGHT-click it to feed its tribute until it stirs — the dragon teaches you the Forge."
4. fox: "A Fox Spirit prowls the red zone (top-right corner). Click it until it falls — hold left-click to auto-attack. It drops Spirit Essence."
5. build: "Press B, place a Storehouse ghost somewhere open, then RIGHT-click it while carrying the wood it asks for."
6. upgrade: "Click the Altar to open the upgrade tree, pick an upgrade, then RIGHT-click-feed the Altar the cost it shows."
7. link: "Wisps already ferry items along the dashed threads. Click a Wisp Lantern, press ➕ Add link, then click a source (🧿/📦) and a target building."
8. explore: "Carry enough wood to a glowing border button and unlock a neighbouring region (Farm, Mine or Fishing)."
9. dragon2: "The dragon's next tribute is stone and clay. The starter Gathering Stones 🧿 by the quarry rock and the clay field (bottom corners) collect both — LEFT-click (or hold) one to withdraw into your hand, then right-click the dragon."
10. iron: "Unlock the Mine, break ⛓️ iron veins for Iron Ore, then build a Forge (B), feed it ore plus wood as fuel, and forge the Iron Bars the dragon's third tribute asks for (bars already fed to it count)."
11. waters: "Fishing is where Algae and Spring Water come from — carry wood to the glowing border button and unlock Fishing."
12. dragon3: "Feed the dragon its third tribute — Iron Bars, Algae and Spring Water. The 🎯 milestone below lists what's still needed and where each comes from."
13. weaver: "Build a Loom (B), then click the Loom and pick Rope (Cotton + Algae); switch to Cloth (Cotton) after. Carry 2 Rope and 6 Cloth in hand. Cotton grows on the Farm — unlock it if you haven't."
14. cultivate: "Build a Meditation Pavilion (your first comes stocked with Spirit Buns), weave a Robe at the Loom (Cloth + Spirit Herb), then click the pavilion and Recruit a disciple to cultivate Spirit Essence for you."

Goal state fields needed from game state: `GS.stats.{foxKills, buildingsBuilt, upgradesApplied, linksAdded, disciplesRecruited}`, `GS.dragon.{stage, paid}`, `GS.world.unlocked[region]`, `ENGINE.handCount(item)`, `ENGINE.dragonTribute(stageIdx)`.

---

## 10. REVEAL (build-menu progressive reveal, data.js:686-708)

Type -> list of conditions; ANY satisfied reveals. `{quest:id}` = quest claimed; `{region:key}` = region unlocked; `{stage:n}` = dragon stage >= n. Owning one (player-built, `GS.builtTypes`) also reveals it. Veterans (ascended, or chain finished) skip quest/region conditions but NOT stage ones. Types with `stageUnlock` (forge, algae_farm, herb_garden) keep that gate; types with neither (center/Altar, dragon) stay hidden.

| building | conditions (ANY) |
|---|---|
| storehouse | quest wood |
| gathering_stone | quest build |
| wisp_lantern | quest build |
| warding_seal | quest build |
| workbench | quest build |
| kiln | quest build |
| paper_mill | quest build |
| charcoal_pit | quest build |
| infusion_array | quest fox |
| furnace_spirit | quest link |
| loom | region farm, OR quest waters |
| mill | region farm, OR quest waters |
| brewery | region farm, OR quest waters |
| jade_carver | region mine |
| cauldron | region mine |
| meditation_pavilion | quest weaver, OR region grove |
| pill_furnace | stage 3 |
| star_anvil | stage 3 |
| talisman_atelier | stage 3 |
| dragon_shrine | stage 4 |
| ascension_gate | stage 4 |

Not in REVEAL (always from stageUnlock): forge (stage 1), algae_farm (stage 2), herb_garden (stage 3).

---

## 11. Misc numeric constants

| const | value | line |
|---|---|---|
| AUTOMATION_CLICKS | `{ 1: 2, 2: 6, 3: 20 }` nodes harvested per tick per automation level (L3 was Infinity) | 764 |
| HAND_CAP | 20 (items carried in hand; +5/level Hand Size; perk hands +5/level; Vow of Burden halves) | 766 |
| QUEST_CHAIN | 2 | 599 |

---

## 12. Perks (PERKS, data.js:774-807)

Ascension Shrine: AP spent on permanent perk levels; `cost[i]` = AP price of level i+1. 15 perks.

| id | name | icon | max | cost per level | desc (verbatim) |
|---|---|---|---|---|---|
| haste | Eternal Haste | ⚡ | 5 | [1,2,3,5,8] | -5% to every timer in the world (regrow, batches, wisp beats, fields, foxes, your swings). Stacks with the +20% world speed per ascension. |
| hall | Master's Hall | 🏯 | 5 | [1,2,3,4,6] | +1 disciple capacity at every Meditation Pavilion, per level. |
| slumber | Long Slumber | 🌙 | 4 | [1,2,4,6] | +2h to the offline catch-up window per level (base 8h). |
| hands | Fleet Hands | 🤲 | 5 | [1,2,3,4,6] | +5 permanent carrying capacity per level (on top of Hand Size). |
| frugal | Frugal Frontier | 🧭 | 3 | [2,4,6] | Region unlock costs -20% per level. |
| ember | Ember Heart | 🔥 | 4 | [2,3,5,7] | Burners consume fuel 15% slower per level. |
| apgain | Ascendant Insight | 🌟 | 3 | [3,5,8] | +1 Ascension Point per ascension per level. |
| autoboost | Keen Automation | ⚙️ | 3 | [3,5,8] | Automation harvests +1 extra node per tick per level. |
| regrow | Deep Roots | 🌿 | 3 | [2,4,6] | Nodes respawn 10% faster per level. |
| gale | Wisp Gale | 🌀 | 3 | [2,4,6] | Wisp lanterns send 10% faster per level. |
| fury | Battle Fury | ⚔️ | 3 | [2,4,6] | +1 damage to beasts per strike per level. |
| bless | Heaven's Favor | 🌠 | 3 | [2,4,6] | Dragon-pill blessings last +20% longer per level. |
| bounty | Astral Bounty | ☄️ | 3 | [2,4,6] | Passive fields (clay, stone, wood, sand, spring water) well up 10% faster per level. |
| paths | Remembered Paths | 🗺️ | 3 | [3,6,12] | Each run starts with the Mine already open; level 2 adds Fishing, level 3 the Farm. |
| legacy | Legacy Automation | 🤖 | 3 | [4,8,16] | Each run starts with Automation L1 in the Center; level 2 adds the Farm, level 3 the Mine (works once that region is open). |

Max level = `max`. DESIGN.md: AP per ascension = 3 + 2 per unlocked region + gate offerings, x vow multiplier (v52 formula, engine); world speed +20%/ascension additive 1/(1+0.2n), floor 120ms on swings; dragon tributes x1/(1+0.25n) floor 0.4; awakening blessing ~11% permanent global speed.

---

## 13. Vows, vow multiplier, gate offerings (data.js:815-824)

Opt-in challenge runs chosen in the ascend modal after the first ascension, for the NEXT run. Ascending with vows active multiplies AP by `VOW_MULT[count]`. First completion of each vow leaves a permanent mark (x0.96 timers).

| id | name | icon | desc |
|---|---|---|---|
| burden | Vow of Burden | 🎒 | Your hands carry half as much. |
| coldhearth | Vow of the Cold Hearth | 🧊 | Burners consume fuel twice as fast. |
| restless | Vow of the Restless Dragon | 🐉 | Every dragon tribute is doubled. |
| solitude | Vow of Solitude | 🕯️ | The run starts without the starter wisp network. |

`VOW_MULT = [1, 1.15, 1.3, 1.5, 1.75]` (index = number of active vows 0..4).
`GATE_OFFERINGS = { items: ["talisman","star_steel","dragon_scale"], cap: 6, perType: 2 }` — at the built Ascension Gate: +1 AP each, capped at 6 total, 2 of each type.

---

## 14. TEST multipliers (data.js:829-833)

```
TEST = { ENABLED: true, timeScale: 0.2, costScale: 0.5 }
```
- `ENABLED: true` in the source as committed (testing convenience). **Real balance uses ENABLED = false** ("flip ENABLED to false to restore GDD balance"; CLAUDE.md: "a real balance pass needs ENABLED=false"). In the port, bake REAL values: timeScale 1.0, costScale 1.0, keep as fields in GameBalance for a debug toggle.
- `timeScale: 0.2` = cooldown/regrow length multiplier (15s -> 3s) — applies to timers when ENABLED.
- `costScale: 0.5` = arrow (region) unlock + upgrade cost multiplier when ENABLED.
- All numbers in this catalog are the raw (unscaled) values.

---

## 15. VERSION (data.js:838-841)

`VERSION = { num: 52, desc: "Design pass 2: idle loop that runs, reveal+quests, vows & legacy perks, readable logistics" }`

---

## 16. Not in data.js (needed but defined elsewhere in old-game)

- Starter network layout (fresh saves auto-build wired demo, flag `GS.starterPlaced`): described only in DESIGN.md (gatherers at quarry/spirit tree/clay field/fox zone; seals keeping stone & wood lines pure; typed storehouses for rare jade shards & bamboo plus stone/plank/brick/spirit-stone sinks; wood fanned round-robin Forge->Workbench->Paper Mill->Kiln (v52: starter network WITHOUT a Forge); clay->Kiln; essence->Infusion Array). Exact positions live in `old-game/js/engine.js`/`state.js` (not covered here). Known: starter wood Gathering Stone at (row 16, col 44) in Center; Farm sand Gathering Stone at (46,18).
- Fixed positions of Altar (centre of Center region, 5x5), Sleeping Dragon (Center top-left corner, 5x5): engine.
- Dragon Scale shed interval: 45s, cap-5 pile, 2x with shrine (DESIGN.md), engine.
- Offline catch-up: base 8h cap (+2h per `slumber`), engine.
- Building sprite wishlist (DESIGN.md "Building sprite wishlist"; 16 px/cell native): storehouse/workbench/forge/kiln/paper_mill/infusion_array/algae_farm/herb_garden 3x2 -> 48x32; gathering_stone/wisp_lantern/warding_seal 1x1 16x16; altar/dragon_sleeping/dragon_awake/ascension_gate/dragon_shrine 5x5 80x80; spirit_tree 4x4 fixture 64x80; quarry_rock/spring 2x2 32x32; fox_spirit 16-24; wisp 8-16. None exist yet (emoji only).

---

## 17. Icon assets (old-game/assets/)

`old-game/assets/icons/` contains exactly 23 PNGs (file name = item key + .png):
algae, bamboo, brick, clay, cotton, fish, glass, iron_bar, iron_ore, jade_shard, leaves, paper, plank, sand, spirit_essence, spirit_herb, spirit_jade, spirit_stone, stone, tools, water, wheat, wood.

Items WITHOUT an icon PNG (use emoji fallback or author new art): cloth, rope, robe, flour, spirit_buns, spirit_wine, qi_elixir, vitality_pill, beast_bait, charcoal, jade, firestone, beast_bone, star_steel, ember_pill, verdant_pill, swiftwind_pill, stoneheart_pill, talisman, dragon_scale, obsidian, star_fragment, moonpetal (23 items).

`old-game/assets/items_sheet.png` — a single sprite sheet containing item art (source of the individual icons); not referenced from data.js. Inspect before slicing.

Building, node, enemy sprites are emoji-only in the old game.

---

## 18. Unity mapping suggestions

ScriptableObject types (one asset per entry unless noted):

- **ItemDef** (50 assets): `key` (string id, keep old keys), `displayName`, `emoji`, `icon` (Sprite), `fuelBurnMs` (0 = not fuel; wood 10000, bamboo 6000, charcoal 40000, firestone 120000), `sourceHint` (SOURCES text), `category` (Raw/Refined/Treasure/Consumable, derived), optional `isDragonPill`, `dragonBuffName`, `dragonBuffDesc` (DRAGON_BUFFS merged here), `isVitality`.
- **BuildingDef** (26): `key`, `displayName`, `emoji`, `sprite`, `size` (Vector2Int, default 3x3), `cost` (list of {ItemDef, qty}, max 3), `unlockedByDefault`, `stageUnlock` (int, 0 = none), `indestructible`, `anyZone`, `waterOnly`, `isBurner`, `recipes` (List<RecipeDef>), `stockCap` (default 20), `genItem/genIntervalMs/genCap` (algae_farm, herb_garden), `storageCap` (storehouse 200), `gatherRadius/gatherCap`, `lanternRateMs/lanternSpeed`, `sealCap`, `stokerRadius/stokerCap`, roster block (`discipleCap, recruitItem, foodItem, foodCap, foodValues[], produceItem, produceMs`), flags `isShrine`, `isGate`; plus `revealConditions` (list of RevealCondition{type: Quest/Region/Stage, questId/regionKey/stage}).
- **RecipeDef**: `name`, `inputs` (list {ItemDef, qty}), `output` ItemDef, `outputQty`, `timeMs`. Convert `Obsidian Glass`, `Moon Elixir`, `Astral Steel` as normal alt recipes.
- **RegionDef** (7): `key`, `name`, `icon`, `rx`, `ry`, `unlockSide`, `unlockCost` list, `baseItem`, `verb`, `actionIcon`, `speedLabel`, `timerLabel`, `noBuildZones` (list of ZoneId), `surfaceWindowSec` (fishing 3), `tierSprite`, `tier` (name,hits,drops,timer), lists of `NodeDef` spawners, `FixtureDef` fixtures, `GeneratorDef` generators, `EnemyDef` enemy zone entry. Region-level zone table lives in **GameBalance** (ZoneRect: id, r0, c0, r1, c1).
- **NodeDef** (spawners + fixtures; ~17 assets): `kind`, `zone`, `sizes[]`, `target`, `scaleWithArea`, `spacing`, `interaction` (enum Chop/Instant/Break/Surface/Quarry), `swingMs`, `sprite/emoji`, `hits`, `regrowSec`, `perHit` drops, `drops` (DropSpec{item,min,max}), `rareDrop` {item,chance}, `useTiers`, fixture-only fields: `size`, `clicksPerDrop`, `dropItem`, `dropMin`, `dropMax`, `autoTap`. Generator fields (GeneratorDef): `kind`, `zone`, `item`, `intervalMs`, `cap`, `upgradeId` (quarry), `rareDrop`.
- **EnemyDef**: `name`, `sprite`, `zone`, `baseCap`, `hp`, `speed`, `respawnMs`, `attackMs`, `drops`, and an optional `baitSpawn` (another EnemyDef: Spirit Boar hp 8, speed 18, drops beast_bone 1-2 + spirit_essence 1; `baitItem = beast_bait`).
- **UpgradeNodeDef** (19): `id`, `icon`, `name`, `position` (Vector2), `area` (RegionDef ref), `type` enum (Hand, WispRate, Affinity, DiscipleCap, Speed, Automation, HarvestSpeed, Quarry, EnemyCap, Damage, Aoe), `desc`, `links` (UpgradeNodeDef refs), `costs` (3 levels x list{ItemDef,qty}); maxLevel = costs.Length.
- **PerkDef** (15): `id`, `name`, `icon`, `max`, `costs[]` (AP), `desc`. **VowDef** (4): `id`, `name`, `icon`, `desc`.
- **QuestDef** (14, ordered list asset `QuestChain` with `chainVersion = 2`): `id`, `icon`, `name`, `desc`, `goalType` enum + params (e.g. HandCount{item,need}, StatAtLeast{stat,need}, DragonStage{n}, RegionUnlockedAny{regions}, IronForDragon, RopeClothCombo{rope 2, cloth 6, need 8}), `rewardItems`, `rewardReveal` (display list), `builds` (BuildingDef refs), `target` {area, kind, id}.
- **DragonStageDef** list (4): `needs` (list{ItemDef,qty}), `text`.
- **GameBalance** (single SO): `cellPx 32, cells 93, margin 10, gap 5, defaultBuildingSize 3x3, cornerSize 25 (_T), zone rects, regionGrid cols 3/rows 3, handCap 20, fuelSlots 6, fuelCapLegacy 60000, automationClicks[2,6,20], vitality{item,ms 45000,bonusDamage 2,lootMult 2}, dragonBlessing base 60s (+30s/affinity), vowMult[1,1.15,1.3,1.5,1.75], gateOfferings{items,cap 6,perType 2}, test{enabled false, timeScale 0.2, costScale 0.5}, version{num 52, desc}, offlineCapHours 8, ascension +20% speed each, tributeShrink 0.25 (floor 0.4)`.

---

## 19. Ambiguities noted while transcribing

1. Fuel rack layout: code comment says "3x2 grid" (FUEL_SLOTS 6); DESIGN.md says 2 cols x 3 rows on the left. Same count, orientation unclear.
2. GRID.building comment says burners override to 3x4, but BUILDINGS use `{w:3,h:5}` (source of truth: 3x5).
3. DESIGN.md says Gathering Stone radius 10 / Furnace Spirit ~3; source has gather.radius 8 (use source). DESIGN grove cost "12 rice + 12 wood" vs source wheat 12 + wood 8.
4. center area's `tiers` entry (Oak, hits 3, perHit wood 1-2) is not referenced by any spawner (bush has its own stats); likely legacy. Similar legacy `tiers` for volcano/grove/celestial/mine only matter where `useTiers: true` (farm crop, mine ore, fishing fish).
5. `noBuild` is an array in center/farm and a string in others — normalize.
6. Upgrade level effect magnitudes for speed/harvestSpeed/quarry/aoe, region pixel origins, starter-network coordinates, Altar/Dragon exact coordinates, dragon scale rates, and offline details are engine-side (not in data.js).
7. TEST.ENABLED is `true` in source; real balance requires false (all catalog numbers are unscaled).
8. Fox `cap:1` is base cap; `speed` units (30, 18) are px/s presumably, undocumented.
