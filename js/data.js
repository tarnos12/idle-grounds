/* ============================================================
   Idle Grounds — static game data (config, no mutable state)
   Loaded as a plain script: everything hangs off window.DATA
   ============================================================ */

// Human-readable item names. Anything not listed falls back to a
// title-cased version of its key (see itemName() in engine.js).
const ITEM_NAMES = {
  wood: "Wood", hardwood: "Hardwood", ancient_bark: "Ancient Bark",
  magic_wood: "Magic Wood", void_timber: "Void Timber",
  plank: "Plank", lumber_frame: "Lumber Frame",

  wheat: "Wheat", carrot: "Carrot", pumpkin: "Pumpkin",
  seed_pouch: "Seed Pouch", mystic_herb: "Mystic Herb", starfruit: "Starfruit",
  flour: "Flour", carrot_bundle: "Carrot Bundle",
  essence_extract: "Essence Extract", starfruit_concentrate: "Starfruit Concentrate",

  stone: "Stone", clay: "Clay", copper_ore: "Copper Ore",
  iron_ore: "Iron Ore", adamantine_ore: "Adamantine Ore",
  stone_block: "Stone Block", clay_brick: "Clay Brick",
  foundation_slab: "Foundation Slab", copper_ingot: "Copper Ingot",
  iron_ingot: "Iron Ingot", steel_alloy: "Steel Alloy",
  adamantine_ingot: "Adamantine Ingot",

  fish: "Fish", fish_scale: "Fish Scale", rare_fish: "Rare Fish",
  fish_oil: "Fish Oil", moonfish_fillet: "Moonfish Fillet", star_pearl: "Star Pearl",
  fish_fillet: "Fish Fillet", refined_oil: "Refined Oil",
  rare_fillet: "Rare Fillet", polished_pearl: "Polished Pearl",

  braided_rope: "Braided Rope",
  wooden_fence: "Wooden Fence", iron_pickaxe: "Iron Pickaxe",
  fishing_rod: "Fishing Rod", workbench: "Workbench", forge: "Forge",
  enchanting_table: "Enchanting Table", worldstone: "Worldstone",
};

// Small emoji per item, purely cosmetic for inventory/cards.
const ITEM_ICONS = {
  wood: "🪵", hardwood: "🪵", ancient_bark: "🪵", magic_wood: "✨", void_timber: "🌑",
  plank: "📏", lumber_frame: "🗜️",
  wheat: "🌾", carrot: "🥕", pumpkin: "🎃", seed_pouch: "👝", mystic_herb: "🌿", starfruit: "⭐",
  flour: "🥖", carrot_bundle: "🥕", essence_extract: "🧪", starfruit_concentrate: "🧴",
  stone: "🪨", clay: "🧱", copper_ore: "🟤", iron_ore: "⚙️", adamantine_ore: "💠",
  stone_block: "🧊", clay_brick: "🧱", foundation_slab: "🟫",
  copper_ingot: "🟧", iron_ingot: "⬜", steel_alloy: "🔩", adamantine_ingot: "💎",
  fish: "🐟", fish_scale: "🐠", rare_fish: "🎣", fish_oil: "🛢️",
  moonfish_fillet: "🌙", star_pearl: "🔮",
  fish_fillet: "🍣", refined_oil: "🫗", rare_fillet: "🍱", polished_pearl: "⚪",
  braided_rope: "🪢", wooden_fence: "🚧", iron_pickaxe: "⛏️", fishing_rod: "🎏",
  workbench: "🛠️", forge: "🔥", enchanting_table: "🔮", worldstone: "🌍",
};

// Base spawn weights by tier index (1-5). Used by weighted roll among
// whichever tiers are currently unlocked in an area.
const TIER_WEIGHTS = [70, 20, 7, 2.5, 0.5];
const TIER_LABELS = ["Common", "Uncommon", "Rare", "Epic", "Legendary"];

// Per-tier emoji sprite for each area (the big overflowing world sprite).
// Index 0..4 = tier 1..5. Cosmetic only.
const TIER_SPRITES = {
  forest:  ["🌳", "🌲", "🌴", "🎄", "🌌"],
  farm:    ["🌾", "🥕", "🎃", "🌿", "⭐"],
  mine:    ["🪨", "🧱", "🟤", "⛏️", "💠"],
  fishing: ["🐟", "🐠", "🎣", "🌙", "🔮"],
};

// d(item, min, max) -> drop spec. max defaults to min (fixed amount).
function d(item, min, max) { return { item, min, max: max == null ? min : max }; }

// Each area plays differently (see `interaction`):
//   chop    (forest)  — multiple swings; every swing yields a bit (`perHit`),
//                       the felling swing also grants the tier `drops`.
//   instant (farm)    — a single click harvests the whole tier `drops`.
//   break   (mine)    — multiple strikes yield nothing; ore (`drops`) only
//                       drops when the rock finally cracks (last `hits`).
//   surface (fishing) — a fish surfaces for `surfaceWindow`s; click while it's
//                       up to land the `drops`, else it dives and resurfaces.
// `base` is the resource that funds the area's plot/upgrade costs.
const AREAS = {
  forest: {
    name: "Forest", icon: "🌲", verb: "Chop", actionIcon: "🪓",
    base: "wood", interaction: "chop",
    spawn: "corners", nodeSizes: [2], target: 12,
    speedLabel: "Regrow Speed", timerLabel: "Regrow",
    tiers: [
      { name: "Oak",      hits: 3, perHit: [d("wood", 1, 2)],   drops: [],                                   timer: 15 },
      { name: "Hardwood", hits: 3, perHit: [d("wood", 2)],      drops: [d("hardwood", 1)],                   timer: 25 },
      { name: "Ancient",  hits: 4, perHit: [d("wood", 2)],      drops: [d("hardwood", 1), d("ancient_bark", 1)], timer: 40 },
      { name: "Magic",    hits: 4, perHit: [d("hardwood", 1)],  drops: [d("magic_wood", 1)],                 timer: 60 },
      { name: "Void",     hits: 5, perHit: [d("hardwood", 1)],  drops: [d("magic_wood", 1), d("void_timber", 1)], timer: 90 },
    ],
  },
  farm: {
    name: "Farm", icon: "🌱", verb: "Harvest", actionIcon: "🌾",
    base: "wheat", interaction: "instant",
    spawn: "corners", nodeSizes: [2], target: 12,
    speedLabel: "Growth Speed", timerLabel: "Growth",
    tiers: [
      { name: "Wheat",       drops: [d("wheat", 2, 3)],                              timer: 20 },
      { name: "Carrot",      drops: [d("wheat", 2), d("carrot", 1)],                 timer: 30 },
      { name: "Pumpkin",     drops: [d("carrot", 1), d("pumpkin", 1), d("seed_pouch", 1)], timer: 50 },
      { name: "Mystic Herb", drops: [d("pumpkin", 1), d("mystic_herb", 1)],          timer: 75 },
      { name: "Starfruit",   drops: [d("mystic_herb", 1), d("starfruit", 1)],        timer: 120 },
    ],
  },
  mine: {
    name: "Mine", icon: "⛰️", verb: "Mine", actionIcon: "⛏️",
    base: "stone", interaction: "break",
    spawn: "centre", nodeSizes: [1, 2], target: 10,   // 1x1 ore + 2x2 boulders
    speedLabel: "Mining Speed", timerLabel: "Respawn",
    tiers: [
      { name: "Stone",      hits: 2, drops: [d("stone", 3), d("clay", 1)],           timer: 10 },
      { name: "Clay Vein",  hits: 2, drops: [d("clay", 4)],                          timer: 15 },
      { name: "Copper Vein",hits: 3, drops: [d("stone", 2), d("copper_ore", 2)],     timer: 30 },
      { name: "Iron Vein",  hits: 4, drops: [d("copper_ore", 1), d("iron_ore", 2)],  timer: 50 },
      { name: "Adamantine", hits: 5, drops: [d("iron_ore", 1), d("adamantine_ore", 1)], timer: 90 },
    ],
  },
  fishing: {
    name: "Fishing", icon: "🎣", verb: "Reel", actionIcon: "🎣",
    base: "fish", interaction: "surface",
    spawn: "centre", nodeSizes: [1], target: 8,
    surfaceWindow: 3,   // seconds a fish stays up before it dives again
    speedLabel: "Fishing Speed", timerLabel: "Bite",
    tiers: [
      { name: "Minnow",      drops: [d("fish", 1, 2)],                               timer: 12 },
      { name: "Bass",        drops: [d("fish", 2), d("fish_scale", 1)],              timer: 20 },
      { name: "Sunfish",     drops: [d("fish", 2), d("rare_fish", 1), d("fish_oil", 1)], timer: 35 },
      { name: "Moonfish",    drops: [d("rare_fish", 1), d("moonfish_fillet", 1)],    timer: 55 },
      { name: "Cosmic Carp", drops: [d("moonfish_fillet", 1), d("star_pearl", 1)],   timer: 90 },
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
  cells: 24,          // 24 x 24 cells per area  ->  768 x 768 px
  building: { w: 2, h: 3 },   // every building occupies a 2-wide x 3-tall block
};

// Named zone rectangles (inclusive cell bounds) on the 24x24 grid.
//   corners = four 8x8 blocks; centre = the middle 8x8 block.
// Resource nodes spawn ONLY in an area's spawn zone; buildings may go
// anywhere EXCEPT a spawn zone (the reserved wild land).
const ZONES = {
  corners: [
    { r0: 0,  c0: 0,  r1: 7,  c1: 7  },   // top-left
    { r0: 0,  c0: 16, r1: 7,  c1: 23 },   // top-right
    { r0: 16, c0: 0,  r1: 23, c1: 7  },   // bottom-left
    { r0: 16, c0: 16, r1: 23, c1: 23 },   // bottom-right
  ],
  centre: [
    { r0: 8, c0: 8, r1: 15, c1: 15 },
  ],
};

// Buildings the player can place. cost is paid by dropping resources into
// the ghost. size defaults to GRID.building. Only `unlocked` ones are listed.
const BUILDINGS = {
  workbench: { name: "Workbench", icon: "🛠️", cost: { wood: 8 },            unlocked: true },
  forge:     { name: "Forge",     icon: "🔥", cost: { wood: 5, stone: 10 }, unlocked: true },
  storehouse:{ name: "Storehouse",icon: "📦", cost: { wood: 12 },           unlocked: true },
  altar:     { name: "Altar",     icon: "🔮", cost: { stone: 20, wood: 10 },unlocked: false },
};

const WORLD = {
  // area-grid offsets from Forest; "void" is the empty arm (up), reserved.
  layout: {
    forest:  { x: 0,  y: 0 },
    farm:    { x: -1, y: 0 },   // left
    mine:    { x: 1,  y: 0 },   // right
    fishing: { x: 0,  y: 1 },   // down
    void:    { x: 0,  y: -1 },  // up  — empty for now
  },
  dirs: { up: { x: 0, y: -1 }, down: { x: 0, y: 1 }, left: { x: -1, y: 0 }, right: { x: 1, y: 0 } },
  dirGlyph: { up: "▲", down: "▼", left: "◀", right: "▶" },
  // resource cost to first open each area (paid from hand, so <= hand cap).
  unlockCost: {
    farm:    { wood: 10 },
    mine:    { wood: 16 },
    fishing: { wood: 20 },
  },
};

// Crafting recipes. `in` = {item: qty}, `out` = {item: qty}.
// area = which area tab filter it belongs to ("misc" = always shown).
const RECIPES = [
  // Wood
  { id: "plank",        name: "Plank",        area: "forest", in: { wood: 3 },                       out: { plank: 1 } },
  { id: "lumber_frame", name: "Lumber Frame", area: "forest", in: { plank: 4 },                      out: { lumber_frame: 1 } },
  // Stone
  { id: "stone_block",  name: "Stone Block",  area: "mine",   in: { stone: 3 },                      out: { stone_block: 1 } },
  { id: "clay_brick",   name: "Clay Brick",   area: "mine",   in: { clay: 2 },                       out: { clay_brick: 1 } },
  { id: "foundation_slab", name: "Foundation Slab", area: "mine", in: { stone_block: 2, clay_brick: 1 }, out: { foundation_slab: 1 } },
  // Metal
  { id: "copper_ingot", name: "Copper Ingot", area: "mine",   in: { copper_ore: 2 },                 out: { copper_ingot: 1 } },
  { id: "iron_ingot",   name: "Iron Ingot",   area: "mine",   in: { iron_ore: 2 },                   out: { iron_ingot: 1 } },
  { id: "steel_alloy",  name: "Steel Alloy",  area: "mine",   in: { copper_ingot: 2, iron_ingot: 1 },out: { steel_alloy: 1 } },
  { id: "adamantine_ingot", name: "Adamantine Ingot", area: "mine", in: { adamantine_ore: 2 },       out: { adamantine_ingot: 1 } },
  // Farm
  { id: "flour",        name: "Flour",        area: "farm",   in: { wheat: 4 },                      out: { flour: 1 } },
  { id: "carrot_bundle",name: "Carrot Bundle",area: "farm",   in: { carrot: 3 },                     out: { carrot_bundle: 1 } },
  { id: "essence_extract", name: "Essence Extract", area: "farm", in: { mystic_herb: 2 },            out: { essence_extract: 1 } },
  { id: "starfruit_concentrate", name: "Starfruit Concentrate", area: "farm", in: { starfruit: 2 }, out: { starfruit_concentrate: 1 } },
  // Fish
  { id: "fish_fillet",  name: "Fish Fillet",  area: "fishing",in: { fish: 3 },                       out: { fish_fillet: 1 } },
  { id: "refined_oil",  name: "Refined Oil",  area: "fishing",in: { fish_oil: 2 },                   out: { refined_oil: 1 } },
  { id: "rare_fillet",  name: "Rare Fillet",  area: "fishing",in: { rare_fish: 2 },                  out: { rare_fillet: 1 } },
  { id: "polished_pearl", name: "Polished Pearl", area: "fishing", in: { star_pearl: 2 },            out: { polished_pearl: 1 } },
  // Rope
  { id: "braided_rope", name: "Braided Rope", area: "fishing",in: { wheat: 6 },                      out: { braided_rope: 1 } },

  // Milestones (flavour / intermediate crafts)
  { id: "wooden_fence", name: "Wooden Fence", area: "misc", in: { plank: 6 },                        out: { wooden_fence: 1 } },
  { id: "iron_pickaxe", name: "Iron Pickaxe", area: "misc", in: { plank: 4, lumber_frame: 2 },       out: { iron_pickaxe: 1 } },
  { id: "fishing_rod",  name: "Fishing Rod",  area: "misc", in: { plank: 3, braided_rope: 2 },       out: { fishing_rod: 1 } },
  { id: "workbench",    name: "Workbench",    area: "misc", in: { plank: 6, stone_block: 4 },        out: { workbench: 1 } },
  { id: "forge",        name: "Forge",        area: "misc", in: { stone_block: 4, clay_brick: 4, copper_ingot: 2 }, out: { forge: 1 } },
  { id: "enchanting_table", name: "Enchanting Table", area: "misc", in: { foundation_slab: 6, essence_extract: 4 }, out: { enchanting_table: 1 } },

  // Endgame — gated behind the Enchanting Table
  { id: "worldstone", name: "Worldstone", area: "misc", requires: "enchanting_table",
    in: {
      void_timber: 4, starfruit_concentrate: 4, adamantine_ingot: 4, polished_pearl: 4,
      essence_extract: 10, steel_alloy: 6, lumber_frame: 6, refined_oil: 8,
    },
    out: { worldstone: 1 }, isWin: true },
];

// Resource costs (in the AREA's base resource) for upgrades.
const COSTS = {
  tierUnlock: { 2: 20, 3: 60, 4: 160, 5: 400 },   // per area, by tier
  speed: [40, 100, 220],                           // I / II / III
  automation: [120, 320, 700],                     // I / II / III
};

// How many ready nodes each automation level harvests per tick.
const AUTOMATION_CLICKS = { 1: 1, 2: 2, 3: Infinity };

const HAND_CAP = 20;   // max items carried in-hand at once

// ------------------------------------------------------------------
// TESTING CONVENIENCES — flip ENABLED to false to restore GDD balance.
// ------------------------------------------------------------------
const TEST = {
  ENABLED: true,
  timeScale: 0.2,    // cooldown/regrow length multiplier (15s -> 3s)
  costScale: 0.5,    // arrow-unlock / upgrade cost multiplier
};

window.DATA = {
  ITEM_NAMES, ITEM_ICONS, TIER_WEIGHTS, TIER_LABELS, TIER_SPRITES,
  AREAS, GRID, ZONES, BUILDINGS, WORLD, RECIPES, COSTS, AUTOMATION_CLICKS,
  HAND_CAP, TEST,
};
