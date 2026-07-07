/* ============================================================
   Idle Grounds — static game data (config, no mutable state)
   Loaded as a plain script: everything hangs off window.DATA
   ============================================================ */

// The ONLY items that exist in the game. Anything else found in an old
// save is scrubbed on load (see loadState). Unknown keys fall back to a
// title-cased name / 📦 icon.
// (keys are stable ids — wheat/fish/water renamed to Rice/Koi/Spring Water
// for the cultivation flavour without touching saves)
const ITEM_NAMES = {
  wood: "Wood", leaves: "Leaves",
  wheat: "Rice", cotton: "Cotton",
  stone: "Stone", clay: "Clay", sand: "Sand", iron_ore: "Iron Ore",
  iron_bar: "Iron Bar",
  fish: "Koi", algae: "Algae", water: "Spring Water",
  spirit_essence: "Spirit Essence", spirit_herb: "Spirit Herb",
  bamboo: "Bamboo", jade_shard: "Jade Shard",
  plank: "Plank", brick: "Brick", paper: "Paper", spirit_stone: "Spirit Stone",
  tools: "Tools", glass: "Glass", spirit_jade: "Spirit Jade",
  cloth: "Cloth", rope: "Rope", robe: "Robe",
  flour: "Rice Flour", spirit_buns: "Spirit Buns", spirit_wine: "Spirit Wine",
  qi_elixir: "Qi Elixir", vitality_pill: "Vitality Pill", beast_bait: "Beast Bait",
  charcoal: "Charcoal", jade: "Jade",
  firestone: "Firestone", beast_bone: "Beast Bone", star_steel: "Star Steel",
  ember_pill: "Ember Pill", verdant_pill: "Verdant Pill",
  swiftwind_pill: "Swiftwind Pill", stoneheart_pill: "Stoneheart Pill",
  talisman: "Talisman", dragon_scale: "Dragon Scale",
};
const ITEM_ICONS = {
  wood: "🪵", leaves: "🍃",
  wheat: "🌾", cotton: "☁️",
  stone: "🪨", clay: "🧱", sand: "🟡", iron_ore: "🔩",
  iron_bar: "🧲",
  fish: "🐟", algae: "🪸", water: "💧",
  spirit_essence: "✨", spirit_herb: "🌱",
  bamboo: "🎍", jade_shard: "🟢",
  plank: "🟫", brick: "🧱", paper: "📜", spirit_stone: "🔮",
  tools: "⛏️", glass: "🧊", spirit_jade: "🟩",
  cloth: "🧶", rope: "🪢", robe: "🥋",
  flour: "🍚", spirit_buns: "🥟", spirit_wine: "🍶",
  qi_elixir: "🧪", vitality_pill: "💊", beast_bait: "🪱",
  charcoal: "⚫", jade: "💚",
  firestone: "🌋", beast_bone: "🦴", star_steel: "⚔️",
  ember_pill: "🔴", verdant_pill: "🟢",
  swiftwind_pill: "🟡", stoneheart_pill: "🟣",
  talisman: "🧧", dragon_scale: "🔶",
};

// Burner fuel values in burn-milliseconds (a batch consumes its own
// duration). Fed to any `fuel: true` building or a Furnace Spirit.
const FUEL = { wood: 10000, bamboo: 6000, charcoal: 40000, firestone: 120000 };
const FUEL_CAP = 60000;   // max burn-ms a burner holds

// Dragon pills: feed one to the dragon (right-click) for a timed GLOBAL
// buff — 60s base, +30s per Dragon Affinity level; a new pill replaces
// the active one. Effects are wired in the engine by pill id.
const DRAGON_BUFFS = {
  ember_pill:      { name: "Ember Blessing",   desc: "Burners work twice as fast." },
  verdant_pill:    { name: "Verdant Blessing", desc: "Nodes regrow twice as fast." },
  swiftwind_pill:  { name: "Swiftwind Blessing", desc: "Wisps fly and send twice as fast." },
  stoneheart_pill: { name: "Stoneheart Blessing", desc: "Mining and quarry drops doubled." },
};

// Vitality Pill: a self-taken combat consumable (right-click it in hand).
// Grants Martial Vigor — bonus attack damage and doubled beast loot.
const VITALITY = { item: "vitality_pill", name: "Martial Vigor", ms: 45000, bonusDamage: 2, lootMult: 2 };

// One sprite per area's resource (tiers are gone — single type each).
const TIER_SPRITES = {
  center:  ["🌳"],
  farm:    ["🌾"],
  mine:    ["🪨"],
  fishing: ["🐟"],
};

// d(item, min, max) -> drop spec. max defaults to min (fixed amount).
function d(item, min, max) { return { item, min, max: max == null ? min : max }; }

// Per-node `interaction`:
//   chop    — multiple swings; every swing yields `perHit`, the felling
//             swing also grants `drops`; then the node relocates.
//   instant — a single click harvests the whole `drops`.
//   break   — strikes yield nothing until the last `hits` cracks it.
//   surface — surfaces for `surfaceWindow`s; click while up to land `drops`.
//   quarry  — a fixed object; every `clicksPerDrop` clicks yields
//             `dropMin..dropMax` (default 1) of `drop`.
// An area's `spawners` describe relocating nodes (each has its own zone,
// sizes, target and interaction). `fixtures` are fixed objects placed once.
// `generators` auto-spawn ground items. `noBuild` = zone(s) buildings avoid.
// `enemies` describes the area's roaming beasts (click to fight).
const AREAS = {
  center: {
    name: "Center", icon: "🌲", verb: "Chop", actionIcon: "🪓",
    base: "wood", noBuild: ["corners", "midTop"],
    speedLabel: "Regrow Speed", timerLabel: "Regrow",
    tiers: [
      { name: "Oak", hits: 3, perHit: [d("wood", 1, 2)], drops: [], timer: 15 },
    ],
    spawners: [
      // bushes in the buildable centre — small (1x1), capped at 10, spread out;
      // chopped for leaves, then respawn.
      { kind: "bush", zone: "centre", sizes: [1], target: 10, scaleWithArea: false, spacing: 6,
        interaction: "chop", swingMs: 300, sprite: "🌿", hits: 2, regrow: 12,
        perHit: [d("leaves", 1)], drops: [d("leaves", 1, 2)] },
    ],
    fixtures: [
      // the quarry rock: manually minable WITHOUT limit (5 clicks -> 1 stone,
      // hold auto-clicks at 1/s) — and it ALSO produces stone passively
      // (see the generator below)
      { kind: "quarry", zone: "cornerBL", size: 2, interaction: "quarry", swingMs: 1000,
        sprite: "⛰️", clicksPerDrop: 5, drop: "stone",
        rareDrop: { item: "jade_shard", chance: 0.12 } },
      // the Spirit Tree: ONE great tree centred in the top band — the only
      // wood source in the Center. Works like the rock but has NO passive
      // production: it only gives while you click / hold on it.
      { kind: "spirittree", zone: "midTop", size: 4, interaction: "quarry", swingMs: 1000,
        sprite: "🌳", clicksPerDrop: 3, drop: "wood", dropMin: 2, dropMax: 3,
        rareDrop: { item: "bamboo", chance: 0.12 } },
    ],
    generators: [
      // clay ground: a small field centred in the bottom-right corner
      { kind: "clay", zone: "clayField", item: "clay", intervalMs: 1500, cap: 10 },
      // passive stone production: silently tops the ground AROUND the rock up
      // to 10 stones (only counts stones lying in the quarry field); the
      // "quarry" upgrade speeds it up
      { kind: "stone", zone: "quarryField", item: "stone", intervalMs: 1500, cap: 10, upgrade: "quarry",
        rareDrop: { item: "jade_shard", chance: 0.08 } },
    ],
    // Fox Spirits haunt the top-right corner: they wander their zone, take a
    // few hits to slay, and drop Spirit Essence. `cap` is the BASE cap — the
    // enemyCap/damage/aoe upgrades in the tree raise it and boost combat.
    enemies: { zone: "cornerTR", name: "Fox Spirit", sprite: "🦊", cap: 1, hp: 3,
               speed: 30, respawnMs: 6000, attackMs: 400, drops: [d("spirit_essence", 1, 2)],
               // right-click Beast Bait inside the zone to lure this tier-2
               // beast — tough, slow, and the only Beast Bone source
               baitSpawn: { name: "Spirit Boar", sprite: "🐗", hp: 8, speed: 18,
                            drops: [d("beast_bone", 1, 2), d("spirit_essence", 1)] } },
  },
  farm: {
    name: "Farm", icon: "🌱", verb: "Harvest", actionIcon: "🌾",
    base: "wheat", noBuild: ["centre", "midLeft"],
    speedLabel: "Growth Speed", timerLabel: "Growth",
    // crops grow ONLY in the centre of the farm: big 3x3 wheat plots plus a
    // few smaller cotton patches.
    spawners: [
      { kind: "crop", zone: "centre", sizes: [3], target: 8, scaleWithArea: false,
        interaction: "instant", useTiers: true, swingMs: 300 },
      { kind: "cotton", zone: "centre", sizes: [2], target: 5, scaleWithArea: false,
        interaction: "instant", swingMs: 300, sprite: "☁️", regrow: 18, drops: [d("cotton", 1, 2)] },
    ],
    generators: [
      // sand ground in the middle-left band auto-spawns sand (like centre's clay)
      { kind: "sand", zone: "midLeft", item: "sand", intervalMs: 1500, cap: 10 },
    ],
    tiers: [
      { name: "Rice", drops: [d("wheat", 2, 3)], timer: 20 },
    ],
  },
  mine: {
    name: "Mine", icon: "⛰️", verb: "Mine", actionIcon: "⛏️",
    base: "stone", noBuild: "centre",
    speedLabel: "Mining Speed", timerLabel: "Respawn",
    spawners: [
      { kind: "ore", zone: "centre", sizes: [1, 2], target: 10, interaction: "break", useTiers: true, swingMs: 450,
        rareDrop: { item: "firestone", chance: 0.05 } },
      // iron veins: rarer, tougher rocks scattered among the stone
      { kind: "ironvein", zone: "centre", sizes: [2], target: 2, interaction: "break",
        swingMs: 500, sprite: "⚙️", hits: 3, regrow: 12, drops: [d("iron_ore", 1, 2)],
        rareDrop: { item: "firestone", chance: 0.15 } },
    ],
    tiers: [
      { name: "Stone", hits: 2, drops: [d("stone", 3), d("clay", 1)], timer: 10 },
    ],
  },
  fishing: {
    name: "Fishing", icon: "🎣", verb: "Reel", actionIcon: "🎣",
    base: "fish", noBuild: "centre",
    surfaceWindow: 3,   // seconds a fish stays up before it dives again
    speedLabel: "Fishing Speed", timerLabel: "Bite",
    // the water yields EITHER fish or algae — mostly algae early on (the
    // Algae Farm building later automates algae entirely).
    spawners: [
      { kind: "fish", zone: "centre", sizes: [1], target: 4, interaction: "surface", useTiers: true, swingMs: 350 },
      { kind: "algae", zone: "centre", sizes: [1], target: 10, interaction: "surface",
        swingMs: 350, sprite: "🪸", regrow: 8, drops: [d("algae", 1, 2)] },
    ],
    fixtures: [
      // the spring: click/hold for water, and it wells up passively into the
      // field around it (see the generator)
      { kind: "spring", zone: "cornerTL", size: 2, interaction: "quarry", swingMs: 1000,
        sprite: "⛲", clicksPerDrop: 3, drop: "water" },
    ],
    generators: [
      { kind: "water", zone: "springField", item: "water", intervalMs: 2000, cap: 10 },
    ],
    tiers: [
      { name: "Koi", drops: [d("fish", 1, 2)], timer: 12 },
    ],
  },
};

// ------------------------------------------------------------------
// World: one big map laid out as a + . Forest is the centre; the four
// arms branch out. The camera is locked to one area at a time and the
// player pays resources at the border arrow to open the next area.
// ------------------------------------------------------------------
const GRID = {
  cell: 32,           // px per cell
  cells: 75,          // 75 x 75 PLAYABLE cells per area (~10x the old 24x24 area)
  margin: 10,         // inert border cells around the whole map
  gap: 5,             // inert void cells separating adjacent regions
  building: { w: 3, h: 2 },   // every building occupies a 3-wide x 2-tall block
};

// Named zone rectangles (inclusive cell bounds), a 3x3 division of the play
// grid (each block ~1/3 of the side) so they scale with GRID.cells.
// Resource nodes spawn ONLY in an area's spawn zone; buildings may go
// anywhere EXCEPT a noBuild zone (the reserved wild land).
const _N = GRID.cells, _T = Math.floor(GRID.cells / 3);
const _TL = { r0: 0, c0: 0, r1: _T - 1, c1: _T - 1 };
const _TR = { r0: 0, c0: _N - _T, r1: _T - 1, c1: _N - 1 };
const _BL = { r0: _N - _T, c0: 0, r1: _N - 1, c1: _T - 1 };
const _BR = { r0: _N - _T, c0: _N - _T, r1: _N - 1, c1: _N - 1 };
const _CENTRE = { r0: _T, c0: _T, r1: _N - _T - 1, c1: _N - _T - 1 };
const ZONES = {
  corners:    [_TL, _TR, _BL, _BR],
  cornerTL:   [_TL],
  cornerTR:   [_TR],
  cornerBL:   [_BL],
  cornerBR:   [_BR],
  centre:     [_CENTRE],
  midTop:     [{ r0: 0, c0: _T, r1: _T - 1, c1: _N - _T - 1 }],   // top-centre band
  midLeft:    [{ r0: _T, c0: 0, r1: _N - _T - 1, c1: _T - 1 }],   // middle-left band
  // small clay field centred INSIDE the bottom-right corner (doesn't touch it)
  clayField:  [(() => { const m = Math.floor((_N - _T + _N - 1) / 2), h = 4;   // centre of the BR block, 9x9
                        return { r0: m - h, c0: m - h, r1: m + h, c1: m + h }; })()],
  // matching stone field around the quarry rock, centred in the BL block
  quarryField:[(() => { const m = Math.floor((_N - _T + _N - 1) / 2), c = Math.floor((_T - 1) / 2), h = 4;
                        return { r0: m - h, c0: c - h, r1: m + h, c1: c + h }; })()],
  // water field around the spring, centred in the TL block (fishing region)
  springField:[(() => { const m = Math.floor((_T - 1) / 2), h = 4;
                        return { r0: m - h, c0: m - h, r1: m + h, c1: m + h }; })()],
};

// Buildings the player can place. cost is paid by dropping resources into
// the ghost (multi-resource costs allowed, max 3 types). size defaults to
// GRID.building. Listed when `unlocked`, OR once the Sleeping Dragon reaches
// `stageUnlock` (its feeding milestones teach new recipes).
const BUILDINGS = {
  // The Altar anchors the upgrade system: a 5x5 pre-placed exactly in the
  // middle of the centre region, never buildable or destroyable. Click it to
  // pick an upgrade, then feed it resources like a ghost.
  // (type stays "center" internally; only the display name is Altar)
  center:    { name: "Altar",     icon: "🏛️", cost: {}, size: { w: 5, h: 5 }, unlocked: false, indestructible: true },
  // The Sleeping Dragon: pre-placed in the Center's top-left corner. Feed it
  // each stage's tribute (see DRAGON_STAGES) and it unlocks new recipes.
  dragon:    { name: "Sleeping Dragon", icon: "🐉", cost: {}, size: { w: 5, h: 5 }, unlocked: false, indestructible: true },
  // Converters carry a `recipes` LIST — left-click the building to pick the
  // active one (switching drops the held stock on the ground). Feeding fills
  // the input stock (per-item cap `stockCap`, default 20); batches start
  // themselves whenever the stock covers the active recipe.
  workbench: { name: "Workbench", icon: "🛠️", cost: { wood: 8 },            unlocked: true,
               recipes: [
                 { name: "Plank", inputs: { wood: 3 }, output: "plank", outputQty: 1, timeMs: 4000 },
                 { name: "Tools", inputs: { plank: 2, iron_bar: 1 }, output: "tools", outputQty: 1, timeMs: 6000 },
               ] },
  kiln:      { name: "Kiln",      icon: "🏺", cost: { wood: 10, clay: 5 },  unlocked: true,
               fuel: true,
               recipes: [
                 { name: "Brick", inputs: { clay: 2 }, output: "brick", outputQty: 1, timeMs: 5000 },
                 { name: "Glass", inputs: { sand: 2 }, output: "glass", outputQty: 1, timeMs: 6000 },
               ] },
  paper_mill:{ name: "Paper Mill", icon: "📜", cost: { wood: 10, stone: 5 }, unlocked: true,
               recipes: [
                 { name: "Paper", inputs: { bamboo: 1, wood: 2 }, output: "paper", outputQty: 1, timeMs: 5000 },
               ] },
  // Infusion Array: a formation circle that imbues mundane materials with
  // fox essence — spirit stones (premium currency) and spirit jade.
  infusion_array: { name: "Infusion Array", icon: "🔮", cost: { stone: 10, spirit_essence: 5 }, unlocked: true,
               recipes: [
                 { name: "Spirit Stone", inputs: { stone: 3, spirit_essence: 1 }, output: "spirit_stone", outputQty: 1, timeMs: 8000 },
                 { name: "Spirit Jade", inputs: { jade: 1, spirit_essence: 2 }, output: "spirit_jade", outputQty: 1, timeMs: 9000 },
               ] },
  // ---- Phase-3 producers ----
  loom:      { name: "Loom",      icon: "🧵", cost: { wood: 10, plank: 4 }, unlocked: true,
               recipes: [
                 { name: "Cloth", inputs: { cotton: 3 }, output: "cloth", outputQty: 1, timeMs: 5000 },
                 { name: "Rope", inputs: { cotton: 2, algae: 2 }, output: "rope", outputQty: 1, timeMs: 5000 },
                 { name: "Robe", inputs: { cloth: 2, spirit_herb: 1 }, output: "robe", outputQty: 1, timeMs: 8000 },
               ] },
  mill:      { name: "Mill",      icon: "🌾", cost: { wood: 8, stone: 6 }, unlocked: true,
               recipes: [
                 { name: "Rice Flour", inputs: { wheat: 2 }, output: "flour", outputQty: 1, timeMs: 4000 },
                 { name: "Spirit Buns", inputs: { flour: 2, water: 1 }, output: "spirit_buns", outputQty: 1, timeMs: 6000 },
               ] },
  brewery:   { name: "Brewery",   icon: "🍶", cost: { wood: 8, clay: 6 }, unlocked: true,
               recipes: [
                 { name: "Spirit Wine", inputs: { wheat: 2, water: 2, leaves: 1 }, output: "spirit_wine", outputQty: 1, timeMs: 8000 },
               ] },
  cauldron:  { name: "Cauldron",  icon: "⚗️", cost: { stone: 8, iron_bar: 2 }, unlocked: true,
               recipes: [
                 { name: "Qi Elixir", inputs: { spirit_herb: 1, water: 2, spirit_essence: 1 }, output: "qi_elixir", outputQty: 1, timeMs: 8000 },
                 { name: "Vitality Pill", inputs: { fish: 1, spirit_herb: 1, water: 1 }, output: "vitality_pill", outputQty: 1, timeMs: 7000 },
                 { name: "Beast Bait", inputs: { fish: 2, algae: 2 }, output: "beast_bait", outputQty: 1, timeMs: 6000 },
               ] },
  jade_carver:{ name: "Jade Carver", icon: "🗿", cost: { wood: 6, stone: 8 }, unlocked: true,
               recipes: [
                 { name: "Jade", inputs: { jade_shard: 3 }, output: "jade", outputQty: 1, timeMs: 6000 },
               ] },
  // ---- Phase-4 T3 producers (both burners) ----
  pill_furnace:{ name: "Pill Furnace", icon: "🫕", cost: { brick: 6, iron_bar: 4, tools: 2 }, unlocked: true,
               fuel: true,
               recipes: [
                 { name: "Ember Pill", inputs: { qi_elixir: 1, firestone: 1 }, output: "ember_pill", outputQty: 1, timeMs: 9000 },
                 { name: "Verdant Pill", inputs: { qi_elixir: 1, spirit_herb: 1 }, output: "verdant_pill", outputQty: 1, timeMs: 9000 },
                 { name: "Swiftwind Pill", inputs: { qi_elixir: 1, cotton: 1 }, output: "swiftwind_pill", outputQty: 1, timeMs: 9000 },
                 { name: "Stoneheart Pill", inputs: { qi_elixir: 1, spirit_stone: 1 }, output: "stoneheart_pill", outputQty: 1, timeMs: 9000 },
               ] },
  star_anvil:{ name: "Star Anvil", icon: "⚒️", cost: { iron_bar: 6, tools: 3, glass: 2 }, unlocked: true,
               fuel: true,
               recipes: [
                 { name: "Star Steel", inputs: { iron_bar: 2, firestone: 1, beast_bone: 1 }, output: "star_steel", outputQty: 1, timeMs: 10000 },
               ] },
  // ---- Phase-5 endgame ----
  talisman_atelier:{ name: "Talisman Atelier", icon: "🖌️", cost: { plank: 6, jade: 2, glass: 2 }, unlocked: true,
               recipes: [
                 { name: "Talisman", inputs: { paper: 2, spirit_jade: 1, qi_elixir: 1 }, output: "talisman", outputQty: 1, timeMs: 10000 },
               ] },
  // Dragon Shrine: honours the awakened dragon — blessings last +60s and
  // it sheds Dragon Scales twice as often while one stands.
  dragon_shrine:{ name: "Dragon Shrine", icon: "🐲", cost: { brick: 10, cloth: 8, jade: 3 }, unlocked: true,
               shrine: true },
  // Ascension Gate: the final monument. Building it offers ASCENSION —
  // reset the grounds, keep a permanent +8% global speed per ascension.
  ascension_gate:{ name: "Ascension Gate", icon: "⛩️", size: { w: 5, h: 5 }, unlocked: true,
               gate: true, cost: { talisman: 3, star_steel: 3, dragon_scale: 3 } },
  charcoal_pit:{ name: "Charcoal Pit", icon: "🕳️", cost: { stone: 6, clay: 4 }, unlocked: true,
               recipes: [
                 { name: "Charcoal", inputs: { wood: 2 }, output: "charcoal", outputQty: 1, timeMs: 4000 },
               ] },
  // Furnace Spirit: a little shrine that auto-stokes fuel into any burner
  // within `radius` cells, from its own fuel-item buffer (wisp-suppliable).
  furnace_spirit: { name: "Furnace Spirit", icon: "🕯️", size: { w: 1, h: 1 }, anyZone: true,
               cost: { stone: 4, spirit_essence: 2 }, unlocked: true,
               stoker: { radius: 3, cap: 20 } },
  // Meditation Pavilion: recruit Disciples (each costs a Robe) who cultivate
  // Spirit Essence while fed Spirit Buns — a peaceful essence source and the
  // sink that gives the Loom (robe) and Mill (bun) chains a purpose. Feed it
  // buns by hand or wisp; the Recruit button in its panel spends a Robe.
  meditation_pavilion: { name: "Meditation Pavilion", icon: "🧘", cost: { plank: 6, cloth: 4, rope: 2 }, unlocked: true,
               roster: { cap: 3, recruit: "robe", food: "spirit_buns", foodCap: 20,
                         // disciples eat any of these (value = cycles it fuels);
                         // Spirit Wine is the Brewery's premium food
                         foodValues: { spirit_buns: 1, spirit_wine: 3 },
                         produce: "spirit_essence", produceMs: 6000 } },
  // Converter buildings carry a `smelt` recipe: right-click feed the inputs
  // (same feeding rule as ghosts); each complete set queues one batch, the
  // building works through the queue on a timer and drops the output on the
  // ground beside itself. Reuse this pattern for the Workbench etc.
  // `fuel: true` buildings burn from a fuel gauge (see FUEL) instead of
  // taking wood in their recipes — feed them wood/bamboo/charcoal directly
  // or let a Furnace Spirit stoke them.
  forge:     { name: "Forge",     icon: "🔥", cost: { wood: 5, stone: 10 }, unlocked: false, stageUnlock: 1,
               fuel: true,
               recipes: [
                 { name: "Iron Bar", inputs: { iron_ore: 2 }, output: "iron_bar", outputQty: 1, timeMs: 6000 },
               ] },
  storehouse:{ name: "Storehouse",icon: "📦", cost: { wood: 12 }, cap: 200,  unlocked: true },
  // Algae Farm: can ONLY be placed in the water (fishing's centre zone);
  // passively grows algae around itself.
  algae_farm:{ name: "Algae Farm", icon: "🪸", cost: { wood: 12, algae: 6 }, unlocked: false, stageUnlock: 2,
               waterOnly: true, gen: { item: "algae", intervalMs: 4000, cap: 8 } },
  // Herb Garden: taught at dragon stage 3 — passively grows Spirit Herbs
  // (the cultivation herbs) around itself, on land.
  herb_garden:{ name: "Herb Garden", icon: "🪴", cost: { wood: 10, water: 5, clay: 5 }, unlocked: false,
                stageUnlock: 3, gen: { item: "spirit_herb", intervalMs: 5000, cap: 6 } },

  // ---- Wisp logistics (small 1x1 formations; may sit on wild land) ----
  // Gathering Stone: vacuums ground items within `radius` cells into its
  // buffer (capacity `cap` total across types).
  gathering_stone: { name: "Gathering Stone", icon: "🧿", size: { w: 1, h: 1 }, anyZone: true,
               cost: { stone: 5 }, unlocked: true, gather: { radius: 8, cap: 20 } },
  // Wisp Lantern: hosts worker wisps. Holds a LIST of links {from,to}
  // (building ids); every `rateMs` it services ONE link, round-robin in the
  // order they were added, sending 1 item the target accepts.
  wisp_lantern: { name: "Wisp Lantern", icon: "🏮", size: { w: 1, h: 1 }, anyZone: true,
               cost: { wood: 5, stone: 5 }, unlocked: true, lantern: { rateMs: 1000, speed: 170 } },
  // Warding Seal: a pass-through buffer locked to ONE item type — wisps
  // simply never bring it anything else, so lines stay pure.
  warding_seal: { name: "Warding Seal", icon: "🈯", size: { w: 1, h: 1 }, anyZone: true,
               cost: { wood: 3, stone: 3 }, unlocked: true, seal: { cap: 5 } },
};

// The Dragon's feeding milestones. Each stage lists the tribute it wants
// (max 3 resource types) and what waking it a little further unlocks.
// This is the future hook for recipe unlocks and story dialogue.
const DRAGON_STAGES = [
  { needs: { leaves: 15 },
    text: "The dragon cracks one eye open… and teaches you the Forge." },
  { needs: { stone: 25, clay: 10 },
    text: "The dragon yawns a plume of steam… and teaches you the Algae Farm." },
  { needs: { iron_bar: 8, algae: 15, water: 10 },
    text: "The dragon tastes forged iron and rumbles approval. \"You shape the earth well, little cultivator. Grow me the herbs of spirit — I will teach you to garden what cannot be farmed.\" (Herb Garden unlocked)" },
  { needs: { spirit_herb: 20, spirit_essence: 15, iron_bar: 5 },
    text: "The dragon breathes in the herbs, the essence, the iron — and OPENS ITS EYES. \"I dreamed a thousand years, and you woke me with patience, not swords. Cultivate on, little one. I will watch over these grounds.\" 🐲 THE DRAGON IS AWAKE." },
];

const WORLD = {
  // ONE continuous map. Each region is a full GRID.cells x GRID.cells block;
  // farm/mine/fishing extend the centre's sides and you PAN between them
  // (no travel arrows). rx/ry are region-grid coordinates.
  regions: {
    farm:    { rx: 0, ry: 0 },   // left of centre
    center:  { rx: 1, ry: 0 },
    mine:    { rx: 2, ry: 0 },   // right of centre
    fishing: { rx: 1, ry: 1 },   // below centre
  },
  cols: 3, rows: 2,              // region-grid extents (bottom corners = void)
  // which viewport edge hosts a locked region's unlock button
  unlockSide: { farm: "left", mine: "right", fishing: "down" },
  // resource cost to open each region (paid from hand, so <= hand cap).
  unlockCost: {
    farm:    { wood: 10 },
    mine:    { wood: 16 },
    fishing: { wood: 20 },
  },
};

// ------------------------------------------------------------------
// Upgrade TREE (nodebuster-style, drawn on canvas). Root = Hand Size at
// the centre; buying level 1 of a node unlocks its linked neighbours.
// Visibility by distance from owned nodes: <=1 full, ==2 shows "?",
// >=3 hidden (the tree screen's debug toggle reveals them).
// x/y are FREE-FORM pixel offsets from the root — scattered organically
// rather than on a grid.
// Each node carries its own `costs` — one {item: qty} map per level
// (multi-resource, max 3 types; feeds through the Altar like a ghost).
// ------------------------------------------------------------------
const UPGRADE_TREE = [
  { id: "hand",   icon: "✋", name: "Hand Size",       x: 0,    y: 0,    area: "center",  type: "hand",
    desc: "+5 carry capacity per level.", links: ["spd_c", "act_c", "spd_f", "spd_m", "foe_cap", "wisps"],
    costs: [{ wood: 10 }, { wood: 25, leaves: 10 }, { cotton: 15, wood: 40 }] },
  { id: "wisps",  icon: "🏮", name: "Wisp Haste",      x: -170, y: 115,  area: "center",  type: "wispRate",
    desc: "Lanterns send more often and wisps fly faster.", links: ["affinity"],
    costs: [{ wood: 40, spirit_stone: 1 }, { spirit_stone: 3, plank: 8 }, { spirit_stone: 6, brick: 8 }] },
  { id: "affinity", icon: "🐲", name: "Dragon Affinity", x: -300, y: 190, area: "center", type: "affinity",
    desc: "Dragon-pill blessings last +30s per level.", links: ["disciples"],
    costs: [{ qi_elixir: 2, wood: 40 }, { spirit_stone: 3, qi_elixir: 3 }, { spirit_jade: 1, qi_elixir: 5 }] },
  { id: "disciples", icon: "🧘", name: "Disciple Mastery", x: -230, y: 300, area: "center", type: "discipleCap",
    desc: "+2 disciple capacity per Meditation Pavilion, per level.", links: [],
    costs: [{ robe: 2, spirit_stone: 2 }, { spirit_stone: 4, spirit_buns: 20 }, { spirit_jade: 2, robe: 5 }] },
  // east — centre economy, drifting to fishing
  { id: "spd_c",  icon: "⏱️", name: "Regrow Speed",    x: 150,  y: -35,  area: "center",  type: "speed",
    desc: "Center bushes respawn faster.", links: ["auto_c"],
    costs: [{ wood: 20 }, { wood: 50, leaves: 15 }, { wood: 120, spirit_essence: 10 }] },
  { id: "auto_c", icon: "🤖", name: "Automation",      x: 300,  y: -85,  area: "center",  type: "automation",
    desc: "Auto-harvests Center nodes.", links: ["act_fi"],
    costs: [{ wood: 60, stone: 30 }, { stone: 120, clay: 40 }, { iron_ore: 40, spirit_essence: 20 }] },
  { id: "act_fi", icon: "🎣", name: "Reel Speed",      x: 455,  y: -45,  area: "fishing", type: "harvestSpeed",
    desc: "Faster reeling when fishing.", links: [],
    costs: [{ fish: 10, algae: 15 }, { algae: 40, wood: 30 }, { fish: 40, iron_ore: 15 }] },
  // west — harvesting power
  { id: "act_c",  icon: "🪓", name: "Action Speed",    x: -150, y: -35,  area: "center",  type: "harvestSpeed",
    desc: "Faster chop/hold swings in the Center.", links: ["quarry"],
    costs: [{ wood: 15, stone: 5 }, { stone: 40, leaves: 20 }, { stone: 80, spirit_essence: 15 }] },
  { id: "quarry", icon: "⛏️", name: "Quarry Output",   x: -295, y: 40,   area: "center",  type: "quarry",
    desc: "The quarry produces stone faster.", links: ["act_m"],
    costs: [{ wood: 20, stone: 10 }, { stone: 50, clay: 20 }, { stone: 100, water: 25 }] },
  { id: "act_m",  icon: "⚒️", name: "Mine Speed",      x: -450, y: -15,  area: "mine",    type: "harvestSpeed",
    desc: "Faster strikes in the Mine.", links: [],
    costs: [{ stone: 30 }, { stone: 60, clay: 25 }, { iron_ore: 30, clay: 50 }] },
  // north — farm
  { id: "spd_f",  icon: "💧", name: "Growth Speed",    x: 40,   y: -150, area: "farm",    type: "speed",
    desc: "Farm crops regrow faster.", links: ["act_f"],
    costs: [{ wheat: 15, wood: 20 }, { wheat: 40, water: 15 }, { wheat: 80, cotton: 20 }] },
  { id: "act_f",  icon: "🌾", name: "Harvest Speed",   x: -45,  y: -290, area: "farm",    type: "harvestSpeed",
    desc: "Faster crop harvesting.", links: ["auto_f"],
    costs: [{ wheat: 25 }, { wheat: 50, sand: 15 }, { cotton: 25, water: 20 }] },
  { id: "auto_f", icon: "🚜", name: "Farm Automation", x: 55,   y: -430, area: "farm",    type: "automation",
    desc: "Auto-harvests Farm crops.", links: [],
    costs: [{ wheat: 60, wood: 40 }, { wheat: 120, cotton: 30 }, { cotton: 60, water: 40, iron_ore: 20 }] },
  // south — mine & fishing economy
  { id: "spd_m",  icon: "⛰️", name: "Respawn Speed",   x: -40,  y: 150,  area: "mine",    type: "speed",
    desc: "Mine nodes respawn faster.", links: ["spd_fi"],
    costs: [{ stone: 25, wood: 20 }, { stone: 60, clay: 30 }, { iron_ore: 25, water: 20 }] },
  { id: "spd_fi", icon: "🌊", name: "Bite Speed",      x: 50,   y: 290,  area: "fishing", type: "speed",
    desc: "Fish & algae surface more often.", links: ["auto_m"],
    costs: [{ fish: 10, wood: 20 }, { algae: 30, clay: 20 }, { fish: 30, water: 30 }] },
  { id: "auto_m", icon: "🛠️", name: "Mine Automation", x: -40,  y: 430,  area: "mine",    type: "automation",
    desc: "Auto-mines ore veins.", links: [],
    costs: [{ stone: 80, clay: 30 }, { iron_ore: 30, stone: 100 }, { iron_ore: 60, water: 30, algae: 30 }] },
  // south-east — combat (Fox Spirits; Spirit Essence is the branch currency)
  { id: "foe_cap", icon: "🦊", name: "Spirit Call",    x: 160,  y: 120,  area: "center",  type: "enemyCap",
    desc: "+1 Fox Spirit roams the grove per level.", links: ["foe_dmg"],
    costs: [{ leaves: 25, wood: 15 }, { spirit_essence: 10, wood: 40 }, { spirit_essence: 25, iron_bar: 5 }] },
  { id: "foe_dmg", icon: "⚔️", name: "Spirit Blade",   x: 315,  y: 205,  area: "center",  type: "damage",
    desc: "+1 damage per strike on beasts.", links: ["foe_aoe"],
    costs: [{ spirit_essence: 5, stone: 20 }, { spirit_essence: 15, iron_ore: 10 }, { spirit_essence: 30, iron_bar: 8 }] },
  { id: "foe_aoe", icon: "💥", name: "Spirit Wave",    x: 470,  y: 300,  area: "center",  type: "aoe",
    desc: "Strikes ripple outward, hitting nearby beasts (wider per level).", links: [],
    costs: [{ spirit_essence: 12, water: 10 }, { spirit_essence: 25, iron_bar: 5 }, { spirit_essence: 50, iron_bar: 12 }] },
];

// ------------------------------------------------------------------
// Tutorial QUESTS — a sequential chain shown in the side panel. Each
// `goal()` reads LIVE state, so anything the player already did counts
// immediately (the Claim button lights up as soon as cur >= need).
// Later quests only appear after earlier ones are claimed, so nothing
// references content the player hasn't seen yet.
// ------------------------------------------------------------------
const QUESTS = [
  { id: "wood", icon: "🪵", name: "First timber",
    desc: "Hold left-click on the big Spirit Tree 🌳 (top of the Center) to chop it, then hold left-click near the fallen wood to vacuum 5 into your hand.",
    goal: () => ({ cur: window.ENGINE.handCount("wood"), need: 5 }) },
  { id: "leaves", icon: "🍃", name: "Bush whacker",
    desc: "Chop the small bushes 🌿 around the Altar and collect 5 leaves.",
    goal: () => ({ cur: window.ENGINE.handCount("leaves"), need: 5 }) },
  { id: "dragon1", icon: "🐉", name: "Wake the sleeper",
    desc: "Carry leaves to the Sleeping Dragon (top-left corner) and RIGHT-click it to feed its tribute until it stirs.",
    goal: () => ({ cur: window.GS.dragon.stage >= 1 ? 1 : 0, need: 1 }) },
  { id: "fox", icon: "🦊", name: "Fox hunt",
    desc: "A Fox Spirit prowls the red zone (top-right corner). Click it until it falls — hold left-click to auto-attack. It drops Spirit Essence.",
    goal: () => ({ cur: window.GS.stats.foxKills || 0, need: 1 }) },
  { id: "build", icon: "🔨", name: "Raise a building",
    desc: "Press B, place a Storehouse ghost somewhere open, then RIGHT-click it while carrying the wood it asks for.",
    goal: () => ({ cur: window.GS.stats.buildingsBuilt || 0, need: 1 }) },
  { id: "upgrade", icon: "🏛️", name: "First insight",
    desc: "Click the Altar to open the upgrade tree, pick an upgrade, then RIGHT-click-feed the Altar the cost it shows.",
    goal: () => ({ cur: window.GS.stats.upgradesApplied || 0, need: 1 }) },
  { id: "link", icon: "🏮", name: "Wisp wrangler",
    desc: "Wisps already ferry items along the dashed threads. Click a Wisp Lantern, press ➕ Add link, then click a source (🧿/📦) and a target building.",
    goal: () => ({ cur: window.GS.stats.linksAdded || 0, need: 1 }) },
  { id: "recipe", icon: "🏺", name: "Change of plans",
    desc: "Click the Kiln and switch its recipe. (Anything it held drops on the ground — that's normal.)",
    goal: () => ({ cur: window.GS.stats.recipeSwitches || 0, need: 1 }) },
  { id: "craft", icon: "⚙️", name: "Production line",
    desc: "Let your buildings craft 5 items in total (planks, bricks, spirit stones…). Keep the wisps fed!",
    goal: () => ({ cur: window.GS.stats.totalCrafted || 0, need: 5 }) },
  { id: "explore", icon: "🔓", name: "Beyond the woods",
    desc: "Carry enough wood to a glowing border button and unlock a neighbouring region (Farm, Mine or Fishing).",
    goal: () => {
      const u = window.GS.world.unlocked;
      return { cur: (u.farm || u.mine || u.fishing) ? 1 : 0, need: 1 };
    } },
  { id: "cultivate", icon: "🧘", name: "Gather disciples",
    desc: "Build a Meditation Pavilion, weave a Robe at the Loom, then click the pavilion and Recruit a disciple to cultivate Spirit Essence for you.",
    goal: () => ({ cur: window.GS.stats.disciplesRecruited || 0, need: 1 }) },
];

// How many ready nodes each automation level harvests per tick.
const AUTOMATION_CLICKS = { 1: 1, 2: 2, 3: Infinity };

const HAND_CAP = 20;   // max items carried in-hand at once

// ------------------------------------------------------------------
// Prestige perks (the Ascension Shrine). Each Ascension grants Ascension
// Points (AP); AP buy PERMANENT perk levels that persist across every
// future reset. `cost[i]` is the AP price of level i+1. Effects are wired
// in engine.js (perkLevel/perkBonus).
// ------------------------------------------------------------------
const PERKS = [
  { id: "haste",   name: "Eternal Haste",  icon: "⚡", max: 5, cost: [1, 2, 3, 5, 8],
    desc: "-5% to every duration in the world (regrow, batches, wisp beats). Compounds with your +8%/ascension." },
  { id: "hall",    name: "Master's Hall",  icon: "🏯", max: 5, cost: [1, 2, 3, 4, 6],
    desc: "+1 disciple capacity at every Meditation Pavilion, per level." },
  { id: "slumber", name: "Long Slumber",   icon: "🌙", max: 4, cost: [1, 2, 4, 6],
    desc: "+2h to the offline catch-up window per level (base 8h)." },
  { id: "hands",   name: "Fleet Hands",    icon: "🤲", max: 5, cost: [1, 2, 3, 4, 6],
    desc: "+5 permanent carrying capacity per level (on top of Hand Size)." },
];

// ------------------------------------------------------------------
// TESTING CONVENIENCES — flip ENABLED to false to restore GDD balance.
// ------------------------------------------------------------------
const TEST = {
  ENABLED: true,
  timeScale: 0.2,    // cooldown/regrow length multiplier (15s -> 3s)
  costScale: 0.5,    // arrow-unlock / upgrade cost multiplier
};

window.DATA = {
  ITEM_NAMES, ITEM_ICONS, TIER_SPRITES,
  AREAS, GRID, ZONES, BUILDINGS, DRAGON_STAGES, DRAGON_BUFFS, VITALITY, WORLD, AUTOMATION_CLICKS, FUEL, FUEL_CAP,
  UPGRADE_TREE, QUESTS, HAND_CAP, PERKS, TEST,
};
