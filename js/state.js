/* ============================================================
   Idle Grounds — mutable game state + construction helpers
   ============================================================ */

// Per-area state. Nodes/ground/buildings are all created at runtime:
// nodes spawn into the area's spawn zone, resources drop on the ground,
// buildings are placed by the player.
function makeAreaState() {
  return {
    nodes: [],        // live resource nodes  {id,row,col,size,tier,hitsLeft,surfaceUntil,autoFlash}
    ground: [],       // dropped items        {id,item,qty,x,y}
    buildings: [],    // placed buildings     {id,type,row,col,paid:{},built}
    spawnQueue: [],   // ms timestamps at which a new node should appear
    nextNodeId: 1,
    nextGroundId: 1,
    nextBuildId: 1,
    upgrades: { maxTier: 1, speed: 0, automation: 0 },
  };
}

function makeInitialState() {
  const areas = {};
  for (const key of Object.keys(window.DATA.AREAS)) areas[key] = makeAreaState();
  return {
    // The "hand": what the cursor is carrying. Ordered stacks, total <= HAND_CAP.
    hand: [],               // [{ item, qty }] in pickup order
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
