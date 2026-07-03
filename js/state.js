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
    handCap: window.DATA.HAND_CAP,   // inventory space (grows with Hand Size)
    handLevel: 0,           // Hand Size upgrade level (root of the tree)
    areas,
    world: {
      // One continuous map; regions are visible but the camera can't pan into
      // a region until it's unlocked at its border button.
      unlocked: { center: true, farm: false, mine: false, fishing: false },
    },
    build: { open: false, placing: null },  // build menu state (transient)
    // The Center building's active upgrade project:
    // { area, type, item, qty, paid } — fed by right-clicking the building.
    upgradeJob: null,
    won: false,
    stats: { started: Date.now(), totalGathered: 0, totalCrafted: 0 },
  };
}

// ---- Persistence (localStorage autosave) ---------------------

const SAVE_KEY = "idle-grounds-save-v1";

// After a reset, block further saves until reload — otherwise the
// beforeunload autosave instantly re-writes the state we just wiped.
let saveDisabled = false;

function saveState() {
  if (saveDisabled) return false;
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
    fresh.handLevel = s.handLevel || 0;
    if (s.upgradeJob) fresh.upgradeJob = s.upgradeJob;
    Object.assign(fresh.world.unlocked, s.world.unlocked || {});
    fresh.won = !!s.won;
    if (s.stats) fresh.stats = s.stats;

    // ---- migration: scrub content that no longer exists in the game ----
    // (old saves may hold tier-2+ nodes and removed item types)
    const LIVE = new Set(Object.keys(window.DATA.ITEM_NAMES));
    for (const k of Object.keys(fresh.areas)) {
      const a = fresh.areas[k];
      // decorative rings changed shape and the quarry became an inert
      // auto-producer: drop both so initArea regenerates them with the
      // current config. Also drop any structurally corrupt entries — one
      // NaN-positioned node would make the canvas painter throw every frame.
      a.nodes = (a.nodes || []).filter(n => !n.deco && n.kind !== "quarry" &&
        Number.isFinite(n.row) && Number.isFinite(n.col) && Number.isFinite(n.size) && n.size > 0);
      a.buildings = (a.buildings || []).filter(b =>
        window.DATA.BUILDINGS[b.type] && Number.isFinite(b.row) && Number.isFinite(b.col));
      for (const n of a.nodes) {
        if (n.tier !== 1) {                      // high-tier node -> base type
          n.tier = 1;
          const t1 = window.DATA.AREAS[k].tiers[0];
          if (n.useTiers) n.hitsLeft = Math.min(n.hitsLeft || 1, t1.hits || 1);
        }
      }
      a.ground = (a.ground || []).filter(g => LIVE.has(g.item) && Number.isFinite(g.x) && Number.isFinite(g.y));
      for (const b of a.buildings || []) {
        if (b.item && !LIVE.has(b.item)) { b.item = null; b.qty = 0; }   // storehouse contents
        if (b.paid) for (const it of Object.keys(b.paid)) if (!LIVE.has(it)) delete b.paid[it];
      }
      if (a.upgrades) a.upgrades.maxTier = 1;    // tier upgrades are gone
    }
    fresh.hand = fresh.hand.filter(st => LIVE.has(st.item) && st.qty > 0);
    if (fresh.upgradeJob && (fresh.upgradeJob.type === "tier" || !LIVE.has(fresh.upgradeJob.item)))
      fresh.upgradeJob = null;
    return fresh;
  } catch (e) { return null; }
}

function clearSave() { saveDisabled = true; try { localStorage.removeItem(SAVE_KEY); } catch (e) {} }

window.SAVE = { saveState, loadState, clearSave, KEY: SAVE_KEY };
window.GS = loadState() || makeInitialState();
