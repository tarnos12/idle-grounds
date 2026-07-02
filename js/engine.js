/* ============================================================
   Idle Grounds — game engine (pure logic, no DOM)
   Mutates window.GS; UI re-renders from it.
   ============================================================ */

const D = window.DATA;
const CELL = D.GRID.cell;
const PLAY_PX = D.GRID.cells * D.GRID.cell;                 // 24 * 32 = 768
function clampPx(v) { return Math.max(4, Math.min(PLAY_PX - 4, v)); }

function itemName(key) {
  return D.ITEM_NAMES[key] || key.replace(/_/g, " ").replace(/\b\w/g, c => c.toUpperCase());
}
function itemIcon(key) { return D.ITEM_ICONS[key] || "📦"; }

// ---- The hand (cursor carry, ordered stacks, total <= HAND_CAP) ----

function handTotal() { return window.GS.hand.reduce((s, x) => s + x.qty, 0); }
function handCap() { return window.GS.handCap; }
function handSpace() { return window.GS.handCap - handTotal(); }
function handCount(item) { const s = window.GS.hand.find(x => x.item === item); return s ? s.qty : 0; }

// Add up to `qty`, capped by remaining space. Returns the amount added.
function handAdd(item, qty) {
  const add = Math.min(qty, handSpace());
  if (add <= 0) return 0;
  const s = window.GS.hand.find(x => x.item === item);
  if (s) s.qty += add; else window.GS.hand.push({ item, qty: add });
  return add;
}
// Remove one unit of the first (oldest) stack. Returns the item or null.
function handTakeFirst() {
  const s = window.GS.hand[0];
  if (!s) return null;
  s.qty--;
  if (s.qty <= 0) window.GS.hand.shift();
  return s.item;
}
// Remove up to `n` of a specific item. Returns amount removed.
function handTake(item, n) {
  const idx = window.GS.hand.findIndex(x => x.item === item);
  if (idx < 0) return 0;
  const s = window.GS.hand[idx];
  const take = Math.min(n, s.qty);
  s.qty -= take;
  if (s.qty <= 0) window.GS.hand.splice(idx, 1);
  return take;
}

// Costs (upgrades / area unlocks) are paid straight from the hand.
function canAfford(cost) {
  for (const [item, qty] of Object.entries(cost)) if (handCount(item) < qty) return false;
  return true;
}
function spend(cost) {
  if (!canAfford(cost)) return false;
  for (const [item, qty] of Object.entries(cost)) handTake(item, qty);
  return true;
}

// ---- Storehouse: each built storehouse holds ONE item type --------
function storehouseCap() { return D.BUILDINGS.storehouse.cap || Infinity; }
// Right-click deposit: move one matching item from hand into the storehouse
// (an empty storehouse adopts the first hand item's type). Returns truthy on
// success so the accelerating right-hold loop keeps feeding it.
function depositToStorehouse(sh) {
  if (!sh.item) { const first = window.GS.hand[0]; if (!first) return null; sh.item = first.item; sh.qty = 0; }
  if (sh.qty >= storehouseCap() || handCount(sh.item) <= 0) return null;
  handTake(sh.item, 1); sh.qty++;
  return { deposited: sh.item };
}
// Left-click withdraw: take `n` items out into the hand (UI paces the rate).
function takeFromStorehouse(sh, n) {
  if (!sh.item) return 0;
  const take = Math.min(n || 1, handSpace(), sh.qty);
  if (take <= 0) return 0;
  const item = sh.item;
  handAdd(item, take); sh.qty -= take;
  if (sh.qty <= 0) sh.item = null;
  return take;
}
function scaled(n) { return Math.max(1, Math.ceil(D.TEST.ENABLED ? n * D.TEST.costScale : n)); }

// ---- Timers / tier rolling ----------------------------------

function effectiveTimer(areaKey, tierIndex) {
  const base = D.AREAS[areaKey].tiers[tierIndex].timer;
  const speed = window.GS.areas[areaKey].upgrades.speed;
  const testScale = D.TEST.ENABLED ? D.TEST.timeScale : 1;
  return base * Math.pow(0.8, speed) * testScale;
}

// Delay between held auto-swings for a node, reduced 20% per harvestSpeed lvl.
function harvestInterval(areaKey, node) {
  const base = (node && node.swingMs) || 350;
  const lvl = window.GS.areas[areaKey].upgrades.harvestSpeed || 0;
  return base * Math.pow(0.8, lvl);
}

// Effective clicks needed per quarry drop (base minus the quarry upgrade).
function quarryClicksPerDrop(areaKey, node) {
  return Math.max(1, node.clicksPerDrop - (window.GS.areas[areaKey].upgrades.quarry || 0));
}

function rollTier(areaKey) {
  const maxTier = window.GS.areas[areaKey].upgrades.maxTier;
  let total = 0;
  for (let t = 0; t < maxTier; t++) total += D.TIER_WEIGHTS[t];
  let r = Math.random() * total;
  for (let t = 0; t < maxTier; t++) { r -= D.TIER_WEIGHTS[t]; if (r <= 0) return t + 1; }
  return 1;
}

// ---- Grid / zones -------------------------------------------

function zoneRects(zoneKey) { return D.ZONES[zoneKey] || []; }
function noBuildRects(areaKey) {
  const nb = D.AREAS[areaKey].noBuild;
  return (Array.isArray(nb) ? nb : [nb]).flatMap(zoneRects);
}

// A cell a building isn't allowed on (the area's reserved wild land).
function inNoBuild(areaKey, r, c) {
  return noBuildRects(areaKey).some(z => r >= z.r0 && r <= z.r1 && c >= z.c0 && c <= z.c1);
}

// Set of "r,c" cells occupied by live nodes and placed buildings.
function occupiedCells(areaKey) {
  const set = new Set();
  const area = window.GS.areas[areaKey];
  for (const n of area.nodes)
    for (let r = n.row; r < n.row + n.size; r++)
      for (let c = n.col; c < n.col + n.size; c++) set.add(r + "," + c);
  const B = D.GRID.building;
  for (const b of area.buildings)
    for (let r = b.row; r < b.row + B.h; r++)
      for (let c = b.col; c < b.col + B.w; c++) set.add(r + "," + c);
  return set;
}

function rand(a, b) { return a + Math.floor(Math.random() * (b - a + 1)); }

// ---- Node spawning ------------------------------------------

function nodeCenterPx(node) {
  return { x: (node.col + node.size / 2) * CELL, y: (node.row + node.size / 2) * CELL };
}
function nodeById(areaKey, id) { return window.GS.areas[areaKey].nodes.find(n => n.id === id); }

// The drop specs for a node (tier-based, or the spawner's own drops).
function nodeSpecs(areaKey, node) {
  if (node.useTiers) {
    const t = D.AREAS[areaKey].tiers[node.tier - 1];
    return { perHit: t.perHit || [], drops: t.drops || [] };
  }
  return { perHit: node.perHit || [], drops: node.drops || [] };
}

// Try to place one node from a spawner into a random free slot in its zone.
function spawnFromSpawner(areaKey, sp) {
  const area = window.GS.areas[areaKey];
  const occ = occupiedCells(areaKey);
  const rects = zoneRects(sp.zone);

  for (let attempt = 0; attempt < 40; attempt++) {
    const size = sp.sizes[rand(0, sp.sizes.length - 1)];
    const z = rects[rand(0, rects.length - 1)];
    if (z.r1 - z.r0 + 1 < size || z.c1 - z.c0 + 1 < size) continue;
    const row = rand(z.r0, z.r1 - size + 1);
    const col = rand(z.c0, z.c1 - size + 1);
    let free = true;
    for (let r = row; r < row + size && free; r++)
      for (let c = col; c < col + size && free; c++)
        if (occ.has(r + "," + c)) free = false;
    if (!free) continue;

    // keep same-kind nodes at least `spacing` cells apart (spread them out)
    if (sp.spacing && area.nodes.some(n => n.spawnerKind === sp.kind &&
        Math.hypot(n.row - row, n.col - col) < sp.spacing)) continue;

    const useTiers = !!sp.useTiers;
    const tier = useTiers ? rollTier(areaKey) : 1;
    const tierDef = useTiers ? D.AREAS[areaKey].tiers[tier - 1] : null;
    const node = {
      id: area.nextNodeId++, row, col, size,
      kind: sp.kind, spawnerKind: sp.kind, interaction: sp.interaction, useTiers,
      tier, hitsLeft: useTiers ? (tierDef.hits || 1) : (sp.hits || 1),
      regrowSec: useTiers ? tierDef.timer : (sp.regrow || 10),
      swingMs: sp.swingMs || 350, sprite: sp.sprite || null,
      perHit: sp.perHit || null, drops: sp.drops || null,
      surfaceUntil: sp.interaction === "surface" ? Date.now() + (D.AREAS[areaKey].surfaceWindow || 3) * 1000 : 0,
      autoFlash: 0,
    };
    area.nodes.push(node);
    return node;
  }
  return null; // zone was full
}

// Place a fixed object (the quarry) centred in its zone, once.
function placeFixture(areaKey, fx) {
  const area = window.GS.areas[areaKey];
  const z = zoneRects(fx.zone)[0];
  const row = z.r0 + Math.floor((z.r1 - z.r0 + 1 - fx.size) / 2);
  const col = z.c0 + Math.floor((z.c1 - z.c0 + 1 - fx.size) / 2);
  area.nodes.push({
    id: area.nextNodeId++, row, col, size: fx.size, kind: fx.kind, interaction: fx.interaction,
    fixed: true, tier: 1, clicks: 0, clicksPerDrop: fx.clicksPerDrop, dropItem: fx.drop,
    swingMs: fx.swingMs || 1000, sprite: fx.sprite || "⛰️", autoFlash: 0,
  });
}

// Fill an area to its spawner targets, place fixtures, seed generators.
// Node targets scale with the play area (relative to the original 24x24) so a
// bigger map stays populated at a similar density.
function areaScale() { return Math.max(1, Math.round((D.GRID.cells / 24) ** 2)); }

function initArea(areaKey) {
  const cfg = D.AREAS[areaKey], area = window.GS.areas[areaKey];
  const factor = areaScale();
  for (const sp of cfg.spawners || []) {
    const target = sp.scaleWithArea === false ? sp.target : sp.target * factor;
    let guard = 0;
    const live = () => area.nodes.filter(n => n.spawnerKind === sp.kind).length;
    while (live() < target && guard++ < target * 8 + 50) if (!spawnFromSpawner(areaKey, sp)) break;
  }
  for (const fx of cfg.fixtures || [])
    if (!area.nodes.some(n => n.kind === fx.kind)) placeFixture(areaKey, fx);
  area.genTimers = (cfg.generators || []).map(() => 0);
}

// Remove a relocating node and queue a replacement from its spawner.
function depleteNode(areaKey, node) {
  const area = window.GS.areas[areaKey];
  const i = area.nodes.indexOf(node);
  if (i >= 0) area.nodes.splice(i, 1);
  const speed = window.GS.areas[areaKey].upgrades.speed;
  const scale = D.TEST.ENABLED ? D.TEST.timeScale : 1;
  const delay = (node.regrowSec || 10) * Math.pow(0.8, speed) * scale * 1000;
  area.spawnQueue.push({ at: Date.now() + delay, kind: node.spawnerKind });
}

// ---- Ground items (never stack — one icon per item) ---------

function dropGround(areaKey, item, qty, x, y) {
  const area = window.GS.areas[areaKey];
  for (let k = 0; k < qty; k++) {
    const jx = clampPx(x + rand(-16, 16)), jy = clampPx(y + rand(-16, 16));
    area.ground.push({ id: area.nextGroundId++, item, x: jx, y: jy });
  }
}

function rollAmount(spec) { return spec.min + Math.floor(Math.random() * (spec.max - spec.min + 1)); }

function grantDropsGround(areaKey, node, specs) {
  const c = nodeCenterPx(node);
  for (const spec of specs || []) {
    const amt = rollAmount(spec);
    if (amt > 0) { dropGround(areaKey, spec.item, amt, c.x, c.y); window.GS.stats.totalGathered += amt; }
  }
}

// Drop a node's accumulated (deferred) yield — used when a chop clears.
function flushPending(areaKey, node) {
  if (!node.pending) return;
  const c = nodeCenterPx(node);
  for (const [item, qty] of Object.entries(node.pending)) {
    if (qty > 0) { dropGround(areaKey, item, qty, c.x, c.y); window.GS.stats.totalGathered += qty; }
  }
  node.pending = null;
}

// Push overlapping ground items apart so they don't sit on top of each other.
// Returns how many pushes happened (0 = everything already settled).
function settleGround(areaKey) {
  const items = window.GS.areas[areaKey].ground;
  const MIN = 18;
  let moves = 0;
  for (let i = 0; i < items.length; i++) {
    for (let j = i + 1; j < items.length; j++) {
      const a = items[i], b = items[j];
      let dx = b.x - a.x, dy = b.y - a.y, d = Math.hypot(dx, dy);
      if (d < 0.01) { dx = rand(-10, 10) || 1; dy = rand(-10, 10) || 1; d = Math.hypot(dx, dy); }
      if (d < MIN) {
        const push = (MIN - d) / 2, ux = dx / d, uy = dy / d;
        a.x -= ux * push; a.y -= uy * push; b.x += ux * push; b.y += uy * push;
        moves++;
      }
    }
  }
  if (moves) for (const it of items) { it.x = clampPx(it.x); it.y = clampPx(it.y); }
  return moves;
}

// Vacuum ground items near (x,y) into the hand (one item per icon).
function pickupNear(areaKey, x, y, radius) {
  const area = window.GS.areas[areaKey];
  const near = area.ground
    .map(g => ({ g, d: Math.hypot(g.x - x, g.y - y) }))
    .filter(o => o.d <= radius)
    .sort((a, b) => a.d - b.d);
  let picked = 0;
  const taken = new Set();
  for (const { g } of near) {
    if (handSpace() <= 0) break;
    if (handAdd(g.item, 1) > 0) { taken.add(g.id); picked++; }
  }
  if (taken.size) area.ground = area.ground.filter(g => !taken.has(g.id));
  return picked;
}

// ---- Harvesting ---------------------------------------------

// Click a node. Behaviour depends on its `interaction`.
function harvestNode(areaKey, nodeId, isAuto) {
  const node = nodeById(areaKey, nodeId);
  if (!node) return false;

  // AUTO badge should stay solid while auto-mining: last longer than the gap
  // between auto-swings (and the 1s automation tick).
  const flashMs = Math.max(node.swingMs || 400, 1000) + 300;

  if (node.interaction === "quarry") {
    // fixed object: every N clicks yields one drop (N shrinks with the
    // area's quarry upgrade); never depletes
    node.clicks = (node.clicks || 0) + 1;
    if (isAuto) node.autoFlash = Date.now() + flashMs;
    if (node.clicks >= quarryClicksPerDrop(areaKey, node)) {
      node.clicks = 0;
      const c = nodeCenterPx(node);
      dropGround(areaKey, node.dropItem, 1, c.x, c.y);
      window.GS.stats.totalGathered += 1;
    }
    return true;
  }

  const specs = nodeSpecs(areaKey, node);
  if (node.interaction === "chop") {
    // Nothing drops mid-chop: accumulate each swing's yield, release on clear.
    node.pending = node.pending || {};
    for (const spec of specs.perHit || []) {
      const amt = rollAmount(spec);
      if (amt > 0) node.pending[spec.item] = (node.pending[spec.item] || 0) + amt;
    }
    node.hitsLeft--;
    if (isAuto) node.autoFlash = Date.now() + flashMs;
    if (node.hitsLeft > 0) return true;
    flushPending(areaKey, node);                      // felled/cleared — drop it all
    grantDropsGround(areaKey, node, specs.drops);
    depleteNode(areaKey, node);
    return true;
  }
  if (node.interaction === "break") {
    node.hitsLeft--;
    if (isAuto) node.autoFlash = Date.now() + flashMs;
    if (node.hitsLeft > 0) return true;               // nothing until it cracks
    grantDropsGround(areaKey, node, specs.drops);
    depleteNode(areaKey, node);
    return true;
  }
  // instant (crops) & surface (fishing): one click lands the drops
  grantDropsGround(areaKey, node, specs.drops);
  if (isAuto) node.autoFlash = Date.now() + flashMs;
  depleteNode(areaKey, node);
  return true;
}

// ---- Buildings ----------------------------------------------

function buildingCatalog() {
  return Object.entries(D.BUILDINGS)
    .filter(([, b]) => b.unlocked)
    .map(([id, b]) => ({ id, ...b }));
}

function buildingFootprint(row, col) {
  const B = D.GRID.building;
  return { row, col, h: B.h, w: B.w };
}

function canPlaceBuilding(areaKey, row, col) {
  const B = D.GRID.building;
  if (row < 0 || col < 0 || row + B.h > D.GRID.cells || col + B.w > D.GRID.cells) return false;
  const occ = occupiedCells(areaKey);
  for (let r = row; r < row + B.h; r++)
    for (let c = col; c < col + B.w; c++) {
      if (inNoBuild(areaKey, r, c)) return false;   // no building on wild land
      if (occ.has(r + "," + c)) return false;
    }
  return true;
}

function placeBuilding(areaKey, type, row, col) {
  if (!D.BUILDINGS[type] || !D.BUILDINGS[type].unlocked) return null;
  if (!canPlaceBuilding(areaKey, row, col)) return null;
  const area = window.GS.areas[areaKey];
  const b = { id: area.nextBuildId++, type, row, col, paid: {}, built: false, item: null, qty: 0 };
  area.buildings.push(b);
  return b;
}

// Remaining resources a ghost still needs: { item: qty }.
function buildingNeeds(b) {
  const cost = D.BUILDINGS[b.type].cost;
  const needs = {};
  for (const [item, qty] of Object.entries(cost)) {
    const rem = qty - (b.paid[item] || 0);
    if (rem > 0) needs[item] = rem;
  }
  return needs;
}

function buildingAt(areaKey, row, col) {
  const B = D.GRID.building;
  return window.GS.areas[areaKey].buildings.find(b =>
    row >= b.row && row < b.row + B.h && col >= b.col && col < b.col + B.w) || null;
}

// ---- Right-click drop: feed a ghost, else drop on the ground -

function dropFromHand(areaKey, x, y) {
  const col = Math.floor(x / CELL), row = Math.floor(y / CELL);
  const b = buildingAt(areaKey, row, col);
  if (b && b.built && b.type === "storehouse") return depositToStorehouse(b);
  if (b && !b.built) {
    const needs = buildingNeeds(b);
    for (const item of Object.keys(needs)) {
      if (handCount(item) > 0) {
        handTake(item, 1);
        b.paid[item] = (b.paid[item] || 0) + 1;
        if (Object.keys(buildingNeeds(b)).length === 0) b.built = true;
        return { fed: item, building: b.id };
      }
    }
    return null; // over a ghost but hand has nothing it needs
  }
  const item = handTakeFirst();
  if (!item) return null;
  dropGround(areaKey, item, 1, x, y);
  return { dropped: item };
}

// ---- Upgrades (paid from the hand) --------------------------

function upgradeCost(areaKey, type) {
  const up = window.GS.areas[areaKey].upgrades;
  const base = D.AREAS[areaKey].base;
  let raw = null;
  if (type === "tier") { const next = up.maxTier + 1; raw = next > 5 ? null : D.COSTS.tierUnlock[next]; }
  if (type === "speed") raw = up.speed >= 3 ? null : D.COSTS.speed[up.speed];
  if (type === "harvestSpeed") raw = up.harvestSpeed >= 3 ? null : D.COSTS.harvestSpeed[up.harvestSpeed];
  if (type === "automation") raw = up.automation >= 3 ? null : D.COSTS.automation[up.automation];
  if (type === "quarry") raw = (up.quarry || 0) >= 3 ? null : D.COSTS.quarry[up.quarry || 0];
  return raw == null ? null : { [base]: scaled(raw) };
}

// How much has already been paid toward the current level of an upgrade.
function upgradePaid(areaKey, type) {
  return window.GS.areas[areaKey].upgrades.paid[type] || 0;
}

// Contribute toward an upgrade from the hand. Since the hand only holds 20,
// expensive upgrades are funded over several trips (like feeding a building):
// each call pays as much as the hand currently holds, and the upgrade applies
// once its cost is fully covered.
function buyUpgrade(areaKey, type) {
  const cost = upgradeCost(areaKey, type);
  if (!cost) return false;
  const [item, qty] = Object.entries(cost)[0];
  const up = window.GS.areas[areaKey].upgrades;
  const need = qty - (up.paid[type] || 0);
  const pay = Math.min(need, handCount(item));
  if (pay <= 0) return false;
  handTake(item, pay);
  up.paid[type] = (up.paid[type] || 0) + pay;
  if (up.paid[type] >= qty) {
    up.paid[type] = 0;
    if (type === "tier") up.maxTier++;
    else if (type === "speed") up.speed++;
    else if (type === "harvestSpeed") up.harvestSpeed++;
    else if (type === "automation") up.automation++;
    else if (type === "quarry") up.quarry = (up.quarry || 0) + 1;
  }
  return true;
}

// ---- World regions (one continuous map) ---------------------

// Global play-cell origin of a region. Regions tile a WORLD.cols x WORLD.rows
// grid with a GRID.gap-cell void strip separating adjacent blocks.
function regionOrigin(areaKey) {
  const r = D.WORLD.regions[areaKey];
  const stride = D.GRID.cells + D.GRID.gap;
  return { row: r.ry * stride, col: r.rx * stride };
}

// Which region a global play cell belongs to (null = void / gap / margin).
function regionAt(gRow, gCol) {
  const N = D.GRID.cells, stride = N + D.GRID.gap;
  for (const [key, r] of Object.entries(D.WORLD.regions)) {
    if (gRow >= r.ry * stride && gRow < r.ry * stride + N &&
        gCol >= r.rx * stride && gCol < r.rx * stride + N) return key;
  }
  return null;
}

function areaUnlockCost(areaKey) {
  const base = D.WORLD.unlockCost[areaKey];
  if (!base) return null;
  const out = {};
  for (const [item, qty] of Object.entries(base)) out[item] = scaled(qty);
  return out;
}

function isAreaUnlocked(areaKey) { return !!window.GS.world.unlocked[areaKey]; }

// Pay to open a region. All regions exist (and are visible) from the start;
// unlocking only widens where the camera may pan and enables interaction.
function unlockArea(areaKey) {
  if (!D.AREAS[areaKey] || isAreaUnlocked(areaKey)) return false;
  const cost = areaUnlockCost(areaKey);
  if (!cost || !spend(cost)) return false;
  window.GS.world.unlocked[areaKey] = true;
  // Refresh stale surfaced fish so they don't all dive the instant it opens.
  const cfg = D.AREAS[areaKey];
  for (const n of window.GS.areas[areaKey].nodes)
    if (n.surfaceUntil)
      n.surfaceUntil = Date.now() + (cfg.surfaceWindow || 3) * 1000 * (0.5 + Math.random());
  return true;
}

// ---- Ticks --------------------------------------------------

// Advance the world. Returns true only if something visible changed, so the
// caller can skip repainting idle frames (repainting a huge world every tick
// was expensive enough to stall the machine).
function gameTick() {
  const now = Date.now();
  const scale = D.TEST.ENABLED ? D.TEST.timeScale : 1;
  let changed = false;
  for (const areaKey of Object.keys(D.AREAS)) {
    if (!isAreaUnlocked(areaKey)) continue;
    const area = window.GS.areas[areaKey];
    const cfg = D.AREAS[areaKey];

    // Surfaced nodes (fishing) not caught in time dive / relocate.
    for (const node of area.nodes.slice())
      if (node.surfaceUntil && now >= node.surfaceUntil) { depleteNode(areaKey, node); changed = true; }

    // Bring queued respawns to life (each carries which spawner it belongs to).
    if (area.spawnQueue.length) {
      const due = area.spawnQueue.filter(e => e.at <= now);
      area.spawnQueue = area.spawnQueue.filter(e => e.at > now);
      for (const e of due) {
        const sp = (cfg.spawners || []).find(s => s.kind === e.kind);
        if (sp && spawnFromSpawner(areaKey, sp)) changed = true;
        else if (sp) area.spawnQueue.push({ at: now + 500, kind: e.kind });
      }
    }

    // Generators (e.g. clay ground) auto-drop items up to their cap.
    (cfg.generators || []).forEach((gen, gi) => {
      if (!area.genTimers) area.genTimers = [];
      if (now < (area.genTimers[gi] || 0)) return;
      area.genTimers[gi] = now + gen.intervalMs * scale;
      if (area.ground.filter(g => g.item === gen.item).length >= gen.cap) return;
      const z = zoneRects(gen.zone)[0];
      dropGround(areaKey, gen.item, 1, (rand(z.c0, z.c1) + 0.5) * CELL, (rand(z.r0, z.r1) + 0.5) * CELL);
      changed = true;
    });

    // Separate overlapping ground items (gravity-like repulsion).
    if (area.ground.length > 1 && settleGround(areaKey) > 0) changed = true;
  }
  return changed;
}

function automationTick() {
  let harvested = 0;
  for (const areaKey of Object.keys(D.AREAS)) {
    if (!isAreaUnlocked(areaKey)) continue;
    const level = window.GS.areas[areaKey].upgrades.automation;
    if (level <= 0) continue;
    const budget = D.AUTOMATION_CLICKS[level];
    const nodes = window.GS.areas[areaKey].nodes.slice().sort((a, b) => b.tier - a.tier);
    let clicks = 0;
    for (const node of nodes) {
      if (clicks >= budget) break;
      harvestNode(areaKey, node.id, true);
      clicks++;
    }
    harvested += clicks;
  }
  return harvested;
}

window.ENGINE = {
  itemName, itemIcon,
  handTotal, handCap, handSpace, handCount, handAdd, handTakeFirst, handTake, canAfford,
  depositToStorehouse, takeFromStorehouse,
  effectiveTimer, harvestInterval, quarryClicksPerDrop, rollTier, zoneRects, noBuildRects, inNoBuild, occupiedCells,
  spawnFromSpawner, placeFixture, initArea, nodeById, nodeCenterPx, depleteNode, harvestNode,
  dropGround, grantDropsGround, settleGround, pickupNear,
  buildingCatalog, buildingFootprint, canPlaceBuilding, placeBuilding,
  buildingNeeds, buildingAt, dropFromHand,
  upgradeCost, upgradePaid, buyUpgrade,
  regionOrigin, regionAt, areaUnlockCost, isAreaUnlocked, unlockArea,
  gameTick, automationTick,
};
