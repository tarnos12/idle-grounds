/* ============================================================
   Idle Grounds — mutable game state + save/load persistence
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
    // speed = regrow/growth; harvestSpeed = swing/chop/mine/hold rate;
    // quarry = fewer clicks per stone. paid = incremental upgrade funding.
    upgrades: { maxTier: 1, speed: 0, harvestSpeed: 0, automation: 0, quarry: 0, paid: {} },
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
      // One continuous map; regions are visible but the camera can't pan into
      // a region until it's unlocked at its border button.
      unlocked: { center: true, farm: false, mine: false, fishing: false },
    },
    build: { open: false, placing: null },  // build menu state (transient)
    won: false,
    stats: { started: Date.now(), totalGathered: 0, totalCrafted: 0 },
  };
}

// ---- Persistence (localStorage autosave) ---------------------

const SAVE_KEY = "idle-grounds-save-v1";

function saveState() {
  try {
    const s = JSON.parse(JSON.stringify(window.GS));
    s.build = { open: false, placing: null };   // never persist UI mode
    localStorage.setItem(SAVE_KEY, JSON.stringify(s));
    return true;
  } catch (e) { return false; }
}

// Load a save by merging it onto a fresh state, so fields added in newer
// code versions keep their defaults instead of coming back undefined.
function loadState() {
  try {
    const raw = localStorage.getItem(SAVE_KEY);
    if (!raw) return null;
    const s = JSON.parse(raw);
    if (!s || !s.areas || !s.world) return null;
    const fresh = makeInitialState();
    for (const k of Object.keys(fresh.areas)) {
      if (!s.areas[k]) continue;
      Object.assign(fresh.areas[k], s.areas[k]);
      fresh.areas[k].upgrades = Object.assign(
        { maxTier: 1, speed: 0, harvestSpeed: 0, automation: 0, quarry: 0, paid: {} },
        s.areas[k].upgrades || {});
    }
    if (Array.isArray(s.hand)) fresh.hand = s.hand;
    if (s.handCap) fresh.handCap = s.handCap;
    Object.assign(fresh.world.unlocked, s.world.unlocked || {});
    fresh.won = !!s.won;
    if (s.stats) fresh.stats = s.stats;
    return fresh;
  } catch (e) { return null; }
}

function clearSave() { try { localStorage.removeItem(SAVE_KEY); } catch (e) {} }

window.SAVE = { saveState, loadState, clearSave, KEY: SAVE_KEY };
window.GS = loadState() || makeInitialState();
