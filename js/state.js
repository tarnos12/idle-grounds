/* ============================================================
   Idle Grounds — mutable game state + construction helpers
   ============================================================ */

// Lay out an area's resource nodes on the lattice from DATA.GRID.
// Nodes nearest the centre start active; the rest are locked and are
// opened by spending the area's base resource.
function makeAreaNodes(cfg) {
  const G = window.DATA.GRID;
  const center = G.cells / 2;
  const slots = [];
  let id = 0;
  for (const row of G.nodeRows) {
    for (const col of G.nodeCols) {
      const cx = col + G.foot / 2, cy = row + G.foot / 2;
      slots.push({ id: id++, row, col, dist: Math.hypot(cx - center, cy - center) });
    }
  }
  // Activate the `initialActive` nodes closest to the centre.
  const order = [...slots].sort((a, b) => a.dist - b.dist);
  const active = new Set(order.slice(0, cfg.initialActive).map(s => s.id));

  return slots.map(s => ({
    id: s.id, row: s.row, col: s.col,
    unlocked: active.has(s.id),
    tier: 1,
    state: "locked",   // locked | ready | cooldown
    cooldownEnd: 0,     // ms timestamp
    hitsLeft: 1,        // remaining chops/strikes (chop & break areas)
    surfaceUntil: 0,    // ms timestamp a surfaced fish dives (fishing)
    autoFlash: 0,       // ms timestamp until which the AUTO pulse shows
  }));
}

function makeAreaState(key) {
  const cfg = window.DATA.AREAS[key];
  return {
    nodes: makeAreaNodes(cfg),
    upgrades: { maxTier: 1, speed: 0, automation: 0 },
  };
}

function makeInitialState() {
  const areas = {};
  for (const key of Object.keys(window.DATA.AREAS)) {
    areas[key] = makeAreaState(key);
  }
  return {
    inventory: {},          // item key -> count
    areas,
    world: {
      currentArea: "forest",
      // Forest is open from the start; the others are bought at the arrows.
      unlocked: { forest: true, farm: false, mine: false, fishing: false },
    },
    craftFilter: "active",  // "active" | "all"
    won: false,
    stats: { started: Date.now(), totalGathered: 0, totalCrafted: 0 },
  };
}

window.GS = makeInitialState();
