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

// d(item, min, max) -> drop spec. max defaults to min (fixed amount).
function d(item, min, max) { return { item, min, max: max == null ? min : max }; }

// Each area: grid shape + the per-tier definition (drops/timer/durability).
const AREAS = {
  forest: {
    name: "Forest", icon: "🌲", verb: "Chop", actionIcon: "🪓",
    cols: 5, maxTiles: 25, initialTiles: 9, initialReady: true,
    speedLabel: "Regrow Speed", timerLabel: "Regrow",
    unlockRecipe: null, // available from start
    tiers: [
      { name: "Oak",      drops: [d("wood", 2, 3)],                                  timer: 15 },
      { name: "Hardwood", drops: [d("wood", 3), d("hardwood", 1)],                   timer: 25 },
      { name: "Ancient",  drops: [d("wood", 2), d("hardwood", 2), d("ancient_bark", 1)], timer: 40 },
      { name: "Magic",    drops: [d("hardwood", 2), d("magic_wood", 1)],             timer: 60 },
      { name: "Void",     drops: [d("magic_wood", 1), d("void_timber", 1)],          timer: 90 },
    ],
  },
  farm: {
    name: "Farm", icon: "🌱", verb: "Harvest", actionIcon: "🌾",
    cols: 5, maxTiles: 25, initialTiles: 6, initialReady: false,
    speedLabel: "Growth Speed", timerLabel: "Growth",
    unlockRecipe: "wooden_fence",
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
    cols: 5, maxTiles: 20, initialTiles: 9, initialReady: true,
    speedLabel: "Mining Speed", timerLabel: "Respawn",
    unlockRecipe: "iron_pickaxe",
    tiers: [
      { name: "Stone",      drops: [d("stone", 3), d("clay", 1)],                    timer: 10, durability: 1 },
      { name: "Clay Vein",  drops: [d("clay", 4)],                                   timer: 15, durability: 1 },
      { name: "Copper Vein",drops: [d("stone", 2), d("copper_ore", 2)],              timer: 30, durability: 2 },
      { name: "Iron Vein",  drops: [d("copper_ore", 1), d("iron_ore", 2)],           timer: 50, durability: 3 },
      { name: "Adamantine", drops: [d("iron_ore", 1), d("adamantine_ore", 1)],       timer: 90, durability: 4 },
    ],
  },
  fishing: {
    name: "Fishing", icon: "🎣", verb: "Reel", actionIcon: "🎣",
    cols: 4, maxTiles: 8, initialTiles: 3, initialReady: false,
    speedLabel: "Fishing Speed", timerLabel: "Bite",
    unlockRecipe: "fishing_rod",
    tiers: [
      { name: "Minnow",      drops: [d("fish", 1, 2)],                               timer: 12 },
      { name: "Bass",        drops: [d("fish", 2), d("fish_scale", 1)],              timer: 20 },
      { name: "Sunfish",     drops: [d("fish", 2), d("rare_fish", 1), d("fish_oil", 1)], timer: 35 },
      { name: "Moonfish",    drops: [d("rare_fish", 1), d("moonfish_fillet", 1)],    timer: 55 },
      { name: "Cosmic Carp", drops: [d("moonfish_fillet", 1), d("star_pearl", 1)],   timer: 90 },
    ],
  },
};

// Crafting recipes. `in` = {item: qty}, `out` = {item: qty}.
// area = which area tab filter it belongs to ("misc" = always shown).
// unlocksArea / firstCraftGold / requires are optional.
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
  // Rope (gateway to fishing)
  { id: "braided_rope", name: "Braided Rope", area: "fishing",in: { wheat: 5, fish_fillet: 2 },      out: { braided_rope: 1 } },

  // Milestones
  { id: "wooden_fence", name: "Wooden Fence", area: "misc", in: { plank: 6 },
    out: { wooden_fence: 1 }, unlocksArea: "farm", firstCraftGold: 25 },
  { id: "iron_pickaxe", name: "Iron Pickaxe", area: "misc", in: { plank: 4, stone_block: 3 },
    out: { iron_pickaxe: 1 }, unlocksArea: "mine", firstCraftGold: 50 },
  { id: "fishing_rod",  name: "Fishing Rod",  area: "misc", in: { plank: 3, braided_rope: 2 },
    out: { fishing_rod: 1 }, unlocksArea: "fishing", firstCraftGold: 75 },
  { id: "workbench",    name: "Workbench",    area: "misc", in: { plank: 6, stone_block: 4 },
    out: { workbench: 1 }, firstCraftGold: 100 },
  { id: "forge",        name: "Forge",        area: "misc", in: { stone_block: 4, clay_brick: 4, copper_ingot: 2 },
    out: { forge: 1 }, firstCraftGold: 150 },
  { id: "enchanting_table", name: "Enchanting Table", area: "misc", in: { foundation_slab: 6, essence_extract: 4 },
    out: { enchanting_table: 1 }, firstCraftGold: 250 },

  // Endgame — gated behind the Enchanting Table
  { id: "worldstone", name: "Worldstone", area: "misc", requires: "enchanting_table",
    in: {
      void_timber: 4, starfruit_concentrate: 4, adamantine_ingot: 4, polished_pearl: 4,
      essence_extract: 10, steel_alloy: 6, lumber_frame: 6, refined_oil: 8,
    },
    out: { worldstone: 1 }, isWin: true },
];

// Gold upgrade costs.
const COSTS = {
  tierUnlock: { 2: 50, 3: 200, 4: 800, 5: 3000 },   // per area, by tier
  speed: [100, 300, 700],                            // I / II / III
  automation: [500, 1500, 4000],                     // I / II / III
  tileBase: 25,                                      // +25 per tile beyond initial
};

// How many ready tiles each automation level harvests per tick.
const AUTOMATION_CLICKS = { 1: 1, 2: 2, 3: Infinity };

window.DATA = {
  ITEM_NAMES, ITEM_ICONS, TIER_WEIGHTS, TIER_LABELS,
  AREAS, RECIPES, COSTS, AUTOMATION_CLICKS,
};
