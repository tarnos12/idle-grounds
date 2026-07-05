/* ============================================================
   Idle Grounds — mutable game state + save/load persistence
   ============================================================ */

// Per-area state. Nodes/ground/buildings are all created at runtime:
// nodes spawn into the area's spawn zone, resources drop on the ground,
// buildings are placed by the player.
function makeAreaState() {
  return {
    nodes: [],        // live resource nodes + fixtures (quarry, spirit tree…)
    ground: [],       // dropped items        {id,item,x,y}  (one item per icon)
    buildings: [],    // placed buildings     {id,type,row,col,paid:{},built}
    spawnQueue: [],   // { at, kind } respawns pending per spawner
    genTimers: [],    // next-spawn time per generator
    enemies: [],      // roaming beasts       {id,x,y,hp,maxHp,tx,ty,hitAt}
    enemyRespawnAt: 0,
    wisps: [],        // items in flight      {id,x,y,item,toId}
    nextNodeId: 1,
    nextGroundId: 1,
    nextBuildId: 1,
    nextEnemyId: 1,
    nextWispId: 1,
    // speed = regrow/growth; harvestSpeed = swing/chop/mine/hold rate;
    // quarry = fewer clicks per stone; enemyCap/damage/aoe = combat branch.
    // paid = incremental upgrade funding.
    upgrades: { maxTier: 1, speed: 0, harvestSpeed: 0, automation: 0, quarry: 0,
                enemyCap: 0, damage: 0, aoe: 0, paid: {} },
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
    // { area, type, needs:{item:qty}, paid:{item:qty} } — fed by right-click.
    upgradeJob: null,
    // The Sleeping Dragon's progression: feed each stage's tribute to advance
    // (unlocks recipes). msg/msgUntil float its stage text briefly; dialog
    // holds the story line for the modal until the player dismisses it.
    dragon: { stage: 0, paid: {}, msg: null, msgUntil: 0, dialog: null },
    starterPlaced: false,   // the pre-wired wisp demo network (built once)
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
        { maxTier: 1, speed: 0, harvestSpeed: 0, automation: 0, quarry: 0,
          enemyCap: 0, damage: 0, aoe: 0, paid: {} },
        s.areas[k].upgrades || {});
    }
    if (Array.isArray(s.hand)) fresh.hand = s.hand;
    if (s.handCap) fresh.handCap = s.handCap;
    fresh.handLevel = s.handLevel || 0;
    if (s.upgradeJob) fresh.upgradeJob = s.upgradeJob;
    if (s.dragon) fresh.dragon = Object.assign({ stage: 0, paid: {}, msg: null, msgUntil: 0, dialog: null }, s.dragon);
    Object.assign(fresh.world.unlocked, s.world.unlocked || {});
    fresh.won = !!s.won;
    fresh.starterPlaced = !!s.starterPlaced;
    if (s.stats) fresh.stats = s.stats;

    // ---- migration: scrub content that no longer exists in the game ----
    // (old saves may hold removed node kinds, tiers and item types)
    const LIVE = new Set(Object.keys(window.DATA.ITEM_NAMES));
    for (const k of Object.keys(fresh.areas)) {
      const a = fresh.areas[k];
      const cfg = window.DATA.AREAS[k];
      const spKinds = new Set((cfg.spawners || []).map(sp => sp.kind));
      const fxKinds = new Set((cfg.fixtures || []).map(fx => fx.kind));
      // keep only nodes the CURRENT config still spawns/places (corner trees,
      // old fixtures etc. vanish; initArea refills anything missing). Deco
      // rings are dropped + regenerated. Also drop structurally corrupt
      // entries — one NaN-positioned node would make the painter throw.
      a.nodes = (a.nodes || []).filter(n => !n.deco &&
        (n.fixed ? fxKinds.has(n.kind) : spKinds.has(n.spawnerKind)) &&
        Number.isFinite(n.row) && Number.isFinite(n.col) && Number.isFinite(n.size) && n.size > 0);
      a.spawnQueue = (a.spawnQueue || []).filter(e => spKinds.has(e.kind));
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
      a.enemies = (a.enemies || []).filter(en =>
        Number.isFinite(en.x) && Number.isFinite(en.y) && Number.isFinite(en.hp) && en.hp > 0);
      a.wisps = (a.wisps || []).filter(w =>
        LIVE.has(w.item) && Number.isFinite(w.x) && Number.isFinite(w.y));
      for (const w of a.wisps) {
        // older step-based wisps lack flight parameters: restart the flight
        // from where they were (px/s speeds are >= 50; per-tick ones aren't)
        if (!Number.isFinite(w.t0)) { w.x0 = w.x; w.y0 = w.y; w.t0 = Date.now(); }
        if (!Number.isFinite(w.sp) || w.sp < 50) w.sp = 170;
      }
      for (const b of a.buildings || []) {
        // logistics state: scrub dead items from gathering buffers and links
        if (b.inv) b.inv = b.inv.filter(st => LIVE.has(st.item) && st.qty > 0);
        if (b.links) b.links = b.links.filter(l =>
          (a.buildings || []).some(x => x.id === l.from) &&
          (a.buildings || []).some(x => x.id === l.to));
      }
      for (const b of a.buildings || []) {
        if (b.item && !LIVE.has(b.item)) { b.item = null; b.qty = 0; }   // storehouse contents
        if (b.paid) for (const it of Object.keys(b.paid)) if (!LIVE.has(it)) delete b.paid[it];
        // converter state: clamp the active recipe index, scrub dead input
        // items, sanitise counters. The old queue/smeltPaid format converts
        // into the input stock.
        const recipes = window.DATA.BUILDINGS[b.type].recipes;
        if (recipes) {
          if (!Number.isFinite(b.recipe) || b.recipe < 0 || b.recipe >= recipes.length) b.recipe = 0;
          b.stock = b.stock || {};
          if (b.smeltPaid) {
            for (const [it, q] of Object.entries(b.smeltPaid))
              if (LIVE.has(it) && q > 0) b.stock[it] = (b.stock[it] || 0) + q;
            delete b.smeltPaid;
          }
          if (Number.isFinite(b.queue) && b.queue > 0)
            for (const [it, q] of Object.entries(recipes[b.recipe].inputs))
              b.stock[it] = (b.stock[it] || 0) + q * b.queue;
          delete b.queue;
          for (const it of Object.keys(b.stock)) if (!LIVE.has(it)) delete b.stock[it];
          if (!Number.isFinite(b.smeltDoneAt) || b.smeltDoneAt < 0) b.smeltDoneAt = 0;
        }
      }
      if (a.upgrades) a.upgrades.maxTier = 1;    // tier upgrades are gone
    }
    fresh.hand = fresh.hand.filter(st => LIVE.has(st.item) && st.qty > 0);
    // upgrade jobs went multi-resource ({needs,paid} maps): drop the old
    // single-item {item,qty} format instead of guessing a conversion.
    if (fresh.upgradeJob && (!fresh.upgradeJob.needs || fresh.upgradeJob.item))
      fresh.upgradeJob = null;
    for (const it of Object.keys(fresh.dragon.paid || {})) if (!LIVE.has(it)) delete fresh.dragon.paid[it];
    return fresh;
  } catch (e) { return null; }
}

function clearSave() { saveDisabled = true; try { localStorage.removeItem(SAVE_KEY); } catch (e) {} }

window.SAVE = { saveState, loadState, clearSave, KEY: SAVE_KEY };
window.GS = loadState() || makeInitialState();
