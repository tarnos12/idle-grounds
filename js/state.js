/* ============================================================
   Idle Grounds — mutable game state + construction helpers
   ============================================================ */

// Per-area state. Nodes/ground/buildings are all created at runtime:
// nodes spawn into the area's spawn zone, resources drop on the ground,
// buildings are placed by the player.
function makeAreaState() {
  return {
    nodes: [],        // live resource nodes + fixtures (quarry)
    ground: [],       // dropped items        {id,item,x,y}  (one item per icon)
    buildings: [],    // placed buildings     {id,type,row,col,paid:{},built}
    spawnQueue: [],   // { at, kind } respawns pending per spawner
    genTimers: [],    // next-spawn time per generator
    nextNodeId: 1,
    nextGroundId: 1,
    nextBuildId: 1,
    // `speed` = regrow/growth speed; `harvestSpeed` = swing/chop/mine speed
    // (its own per-area variable, reserved for a future upgrade).
    upgrades: { maxTier: 1, speed: 0, harvestSpeed: 0, automation: 0, paid: {} },
  };
}

function makeInitialState() {
  const areas = {};
  for (const key of Object.keys(window.DATA.AREAS)) areas[key] = makeAreaState();
  return {
    // The "hand": what the cursor is carrying. Ordered stacks, total <= handCap.
    hand: [],               // [{ item, qty }] in pickup order
    handCap: window.DATA.HAND_CAP,   // inventory space (may grow over time)
    areas,
    world: {
      currentArea: "forest",
      unlocked: { forest: true, farm: false, mine: false, fishing: false },
    },
    build: { open: false, placing: null },  // build menu state (placing = building id)
    won: false,
    stats: { started: Date.now(), totalGathered: 0, totalCrafted: 0 },
  };
}

window.GS = makeInitialState();
