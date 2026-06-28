/* ============================================================
   Idle Grounds — mutable game state + construction helpers
   ============================================================ */

// Build a fresh per-area state block from its config.
function makeAreaState(key) {
  const cfg = window.DATA.AREAS[key];
  const tiles = [];
  for (let i = 0; i < cfg.maxTiles; i++) {
    tiles.push({
      id: i,
      unlocked: i < cfg.initialTiles,
      tier: 1,
      state: "locked",          // locked | ready | cooldown
      cooldownEnd: 0,           // ms timestamp
      hitsLeft: 1,              // for mine durability
      autoFlash: 0,             // ms timestamp until which the AUTO pulse shows
    });
  }
  return {
    unlocked: cfg.unlockRecipe === null,
    tiles,
    upgrades: {
      maxTier: 1,    // highest unlocked tier (1..5)
      speed: 0,      // 0..3
      automation: 0, // 0..3
    },
  };
}

function makeInitialState() {
  const areas = {};
  for (const key of Object.keys(window.DATA.AREAS)) {
    areas[key] = makeAreaState(key);
  }
  return {
    gold: 0,
    inventory: {},          // item key -> count
    areas,
    activeArea: "forest",
    craftedOnce: {},        // recipe id -> true (for one-time gold bonuses)
    craftFilter: "active",  // "active" | "all"
    won: false,
    stats: { started: Date.now(), totalGathered: 0, totalCrafted: 0 },
  };
}

window.GS = makeInitialState();
