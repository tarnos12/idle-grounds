/* ============================================================
   Idle Grounds — static game data (config, no mutable state)
   Loaded as a plain script: everything hangs off window.DATA
   ============================================================ */

// The ONLY items that exist in the game. Anything else found in an old
// save is scrubbed on load (see loadState). Unknown keys fall back to a
// title-cased name / 📦 icon.
const ITEM_NAMES = {
  wood: "Wood", leaves: "Leaves",
  wheat: "Wheat",
  stone: "Stone", clay: "Clay", sand: "Sand",
  fish: "Fish",
};
const ITEM_ICONS = {
  wood: "🪵", leaves: "🍃",
  wheat: "🌾",
  stone: "🪨", clay: "🧱", sand: "🟡",
  fish: "🐟",
};

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
//   quarry  — a fixed object; every `clicksPerDrop` clicks yields 1 `drop`.
// An area's `spawners` describe relocating nodes (each has its own zone,
// sizes, target and interaction). `fixtures` are fixed objects placed once.
// `generators` auto-spawn ground items. `noBuild` = zone(s) buildings avoid.
const AREAS = {
  center: {
    name: "Center", icon: "🌲", verb: "Chop", actionIcon: "🪓",
    base: "wood", noBuild: "corners",
    speedLabel: "Regrow Speed", timerLabel: "Regrow",
    tiers: [
      { name: "Oak", hits: 3, perHit: [d("wood", 1, 2)], drops: [], timer: 15 },
    ],
    spawners: [
      // trees in the two TOP corners
      { kind: "tree", zone: "cornersTop", sizes: [2], target: 8, interaction: "chop", useTiers: true, swingMs: 350 },
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
        sprite: "⛰️", clicksPerDrop: 5, drop: "stone" },
    ],
    generators: [
      // clay ground: a small field centred in the bottom-right corner
      { kind: "clay", zone: "clayField", item: "clay", intervalMs: 1500, cap: 10 },
      // passive stone production: silently tops the ground AROUND the rock up
      // to 10 stones (only counts stones lying in the quarry field); the
      // "quarry" upgrade speeds it up
      { kind: "stone", zone: "quarryField", item: "stone", intervalMs: 1500, cap: 10, upgrade: "quarry" },
    ],
  },
  farm: {
    name: "Farm", icon: "🌱", verb: "Harvest", actionIcon: "🌾",
    base: "wheat", noBuild: ["centre", "midLeft"],
    speedLabel: "Growth Speed", timerLabel: "Growth",
    // crops grow ONLY in the centre of the farm, as big 3x3 plots
    spawners: [{ kind: "crop", zone: "centre", sizes: [3], target: 8, scaleWithArea: false,
                 interaction: "instant", useTiers: true, swingMs: 300 }],
    generators: [
      // sand ground in the middle-left band auto-spawns sand (like centre's clay)
      { kind: "sand", zone: "midLeft", item: "sand", intervalMs: 1500, cap: 10 },
    ],
    tiers: [
      { name: "Wheat", drops: [d("wheat", 2, 3)], timer: 20 },
    ],
  },
  mine: {
    name: "Mine", icon: "⛰️", verb: "Mine", actionIcon: "⛏️",
    base: "stone", noBuild: "centre",
    speedLabel: "Mining Speed", timerLabel: "Respawn",
    spawners: [{ kind: "ore", zone: "centre", sizes: [1, 2], target: 10, interaction: "break", useTiers: true, swingMs: 450 }],
    tiers: [
      { name: "Stone", hits: 2, drops: [d("stone", 3), d("clay", 1)], timer: 10 },
    ],
  },
  fishing: {
    name: "Fishing", icon: "🎣", verb: "Reel", actionIcon: "🎣",
    base: "fish", noBuild: "centre",
    surfaceWindow: 3,   // seconds a fish stays up before it dives again
    speedLabel: "Fishing Speed", timerLabel: "Bite",
    spawners: [{ kind: "fish", zone: "centre", sizes: [1], target: 8, interaction: "surface", useTiers: true, swingMs: 350 }],
    tiers: [
      { name: "Minnow", drops: [d("fish", 1, 2)], timer: 12 },
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

// Named zone rectangles (inclusive cell bounds) on the 24x24 grid.
//   corners = four 8x8 blocks; centre = the middle 8x8 block.
// Resource nodes spawn ONLY in an area's spawn zone; buildings may go
// anywhere EXCEPT a spawn zone (the reserved wild land).
// Zones are a 3x3 division of the play grid (each block ~1/3 of the side), so
// they scale automatically with GRID.cells.
const _N = GRID.cells, _T = Math.floor(GRID.cells / 3);
const _TL = { r0: 0, c0: 0, r1: _T - 1, c1: _T - 1 };
const _TR = { r0: 0, c0: _N - _T, r1: _T - 1, c1: _N - 1 };
const _BL = { r0: _N - _T, c0: 0, r1: _N - 1, c1: _T - 1 };
const _BR = { r0: _N - _T, c0: _N - _T, r1: _N - 1, c1: _N - 1 };
const _CENTRE = { r0: _T, c0: _T, r1: _N - _T - 1, c1: _N - _T - 1 };
const ZONES = {
  corners:    [_TL, _TR, _BL, _BR],
  cornersTop: [_TL, _TR],
  cornerBL:   [_BL],
  cornerBR:   [_BR],
  centre:     [_CENTRE],
  midLeft:    [{ r0: _T, c0: 0, r1: _N - _T - 1, c1: _T - 1 }],   // middle-left band
  // small clay field centred INSIDE the bottom-right corner (doesn't touch it)
  clayField:  [(() => { const m = Math.floor((_N - _T + _N - 1) / 2), h = 4;   // centre of the BR block, 9x9
                        return { r0: m - h, c0: m - h, r1: m + h, c1: m + h }; })()],
  // matching stone field around the quarry rock, centred in the BL block
  quarryField:[(() => { const m = Math.floor((_N - _T + _N - 1) / 2), c = Math.floor((_T - 1) / 2), h = 4;
                        return { r0: m - h, c0: c - h, r1: m + h, c1: c + h }; })()],
};

// Buildings the player can place. cost is paid by dropping resources into
// the ghost. size defaults to GRID.building. Only `unlocked` ones are listed.
const BUILDINGS = {
  // The Altar anchors the upgrade system: a 5x5 pre-placed exactly in the
  // middle of the centre region, never buildable or destroyable. Click it to
  // pick an upgrade, then feed it resources like a ghost.
  // (type stays "center" internally; only the display name is Altar)
  center:    { name: "Altar",     icon: "🏛️", cost: {}, size: { w: 5, h: 5 }, unlocked: false, indestructible: true },
  workbench: { name: "Workbench", icon: "🛠️", cost: { wood: 8 },            unlocked: true },
  forge:     { name: "Forge",     icon: "🔥", cost: { wood: 5, stone: 10 }, unlocked: true },
  storehouse:{ name: "Storehouse",icon: "📦", cost: { wood: 12 }, cap: 200,  unlocked: true },
};

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

// Resource costs (in the AREA's base resource) for upgrades.
const COSTS = {
  speed: [40, 100, 220],                           // regrow/growth, I / II / III
  harvestSpeed: [30, 80, 180],                     // swing/chop/mine/hold rate, -20% each
  automation: [120, 320, 700],                     // I / II / III
  quarry: [40, 120, 300],                          // -1 click per stone each level
  hand: [10, 30, 80],                              // +5 carry capacity each level
};

// ------------------------------------------------------------------
// Upgrade TREE (nodebuster-style, drawn on canvas). Root = Hand Size at
// the centre; buying level 1 of a node unlocks its linked neighbours.
// Visibility by distance from owned nodes: <=1 full, ==2 shows "?",
// >=3 hidden (the tree screen's debug toggle reveals them).
// x/y are FREE-FORM pixel offsets from the root — scattered organically
// rather than on a grid.
// ------------------------------------------------------------------
const UPGRADE_TREE = [
  { id: "hand",   icon: "✋", name: "Hand Size",       x: 0,    y: 0,    area: "center",  type: "hand",
    desc: "+5 carry capacity per level.", links: ["spd_c", "act_c", "spd_f", "spd_m"] },
  // east — centre economy, drifting to fishing
  { id: "spd_c",  icon: "⏱️", name: "Regrow Speed",    x: 150,  y: -35,  area: "center",  type: "speed",
    desc: "Center trees & bushes respawn faster.", links: ["auto_c"] },
  { id: "auto_c", icon: "🤖", name: "Automation",      x: 300,  y: -85,  area: "center",  type: "automation",
    desc: "Auto-harvests Center nodes.", links: ["act_fi"] },
  { id: "act_fi", icon: "🎣", name: "Reel Speed",      x: 455,  y: -45,  area: "fishing", type: "harvestSpeed",
    desc: "Faster reeling when fishing.", links: [] },
  // west — harvesting power
  { id: "act_c",  icon: "🪓", name: "Action Speed",    x: -150, y: -35,  area: "center",  type: "harvestSpeed",
    desc: "Faster chop/hold swings in the Center.", links: ["quarry"] },
  { id: "quarry", icon: "⛏️", name: "Quarry Output",   x: -295, y: 40,   area: "center",  type: "quarry",
    desc: "The quarry produces stone faster.", links: ["act_m"] },
  { id: "act_m",  icon: "⚒️", name: "Mine Speed",      x: -450, y: -15,  area: "mine",    type: "harvestSpeed",
    desc: "Faster strikes in the Mine.", links: [] },
  // north — farm
  { id: "spd_f",  icon: "💧", name: "Growth Speed",    x: 40,   y: -150, area: "farm",    type: "speed",
    desc: "Farm crops regrow faster.", links: ["act_f"] },
  { id: "act_f",  icon: "🌾", name: "Harvest Speed",   x: -45,  y: -290, area: "farm",    type: "harvestSpeed",
    desc: "Faster crop harvesting.", links: ["auto_f"] },
  { id: "auto_f", icon: "🚜", name: "Farm Automation", x: 55,   y: -430, area: "farm",    type: "automation",
    desc: "Auto-harvests Farm crops.", links: [] },
  // south — mine & fishing economy
  { id: "spd_m",  icon: "⛰️", name: "Respawn Speed",   x: -40,  y: 150,  area: "mine",    type: "speed",
    desc: "Mine nodes respawn faster.", links: ["spd_fi"] },
  { id: "spd_fi", icon: "🌊", name: "Bite Speed",      x: 50,   y: 290,  area: "fishing", type: "speed",
    desc: "Fish surface more often.", links: ["auto_m"] },
  { id: "auto_m", icon: "🛠️", name: "Mine Automation", x: -40,  y: 430,  area: "mine",    type: "automation",
    desc: "Auto-mines ore veins.", links: [] },
];

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
  ITEM_NAMES, ITEM_ICONS, TIER_SPRITES,
  AREAS, GRID, ZONES, BUILDINGS, WORLD, COSTS, AUTOMATION_CLICKS,
  UPGRADE_TREE, HAND_CAP, TEST,
};
