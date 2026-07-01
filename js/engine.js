/* ============================================================
   Idle Grounds — game engine (pure logic, no DOM)
   Mutates window.GS; UI re-renders from it.
   ============================================================ */

const D = window.DATA;
const CELL = D.GRID.cell;

function itemName(key) {
  return D.ITEM_NAMES[key] || key.replace(/_/g, " ").replace(/\b\w/g, c => c.toUpperCase());
}
function itemIcon(key) { return D.ITEM_ICONS[key] || "📦"; }

// ---- The hand (cursor carry, ordered stacks, total <= HAND_CAP) ----

function handTotal() { return window.GS.hand.reduce((s, x) => s + x.qty, 0); }
function handSpace() { return D.HAND_CAP - handTotal(); }
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
function scaled(n) { return Math.max(1, Math.ceil(D.TEST.ENABLED ? n * D.TEST.costScale : n)); }

// ---- Timers / tier rolling ----------------------------------

function effectiveTimer(areaKey, tierIndex) {
  const base = D.AREAS[areaKey].tiers[tierIndex].timer;
  const speed = window.GS.areas[areaKey].upgrades.speed;
  const testScale = D.TEST.ENABLED ? D.TEST.timeScale : 1;
  return base * Math.pow(0.8, speed) * testScale;
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

function zoneRects(areaKey) { return D.ZONES[D.AREAS[areaKey].spawn] || []; }

function inSpawnZone(areaKey, r, c) {
  return zoneRects(areaKey).some(z => r >= z.r0 && r <= z.r1 && c >= z.c0 && c <= z.c1);
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

// ---- Node spawning (random free slot inside the spawn zone) --

function nodeCenterPx(node) {
  return { x: (node.col + node.size / 2) * CELL, y: (node.row + node.size / 2) * CELL };
}
function nodeById(areaKey, id) { return window.GS.areas[areaKey].nodes.find(n => n.id === id); }

function spawnNode(areaKey) {
  const cfg = D.AREAS[areaKey];
  const area = window.GS.areas[areaKey];
  const occ = occupiedCells(areaKey);
  const rects = zoneRects(areaKey);

  for (let attempt = 0; attempt < 40; attempt++) {
    const size = cfg.nodeSizes[rand(0, cfg.nodeSizes.length - 1)];
    const z = rects[rand(0, rects.length - 1)];
    if (z.r1 - z.r0 + 1 < size || z.c1 - z.c0 + 1 < size) continue;
    const row = rand(z.r0, z.r1 - size + 1);
    const col = rand(z.c0, z.c1 - size + 1);
    let free = true;
    for (let r = row; r < row + size && free; r++)
      for (let c = col; c < col + size && free; c++)
        if (occ.has(r + "," + c)) free = false;
    if (!free) continue;

    const tier = rollTier(areaKey);
    const node = {
      id: area.nextNodeId++, row, col, size, tier,
      hitsLeft: cfg.tiers[tier - 1].hits || 1,
      surfaceUntil: cfg.interaction === "surface" ? Date.now() + (cfg.surfaceWindow || 3) * 1000 : 0,
      autoFlash: 0,
    };
    area.nodes.push(node);
    return node;
  }
  return null; // zone was full
}

// Fill an area up to its live-node target (called on first unlock).
function initArea(areaKey) {
  const target = D.AREAS[areaKey].target;
  let guard = 0;
  while (window.GS.areas[areaKey].nodes.length < target && guard++ < 200) {
    if (!spawnNode(areaKey)) break;
  }
}

// Remove a node and schedule a replacement to appear elsewhere later.
function depleteNode(areaKey, node) {
  const area = window.GS.areas[areaKey];
  const i = area.nodes.indexOf(node);
  if (i >= 0) area.nodes.splice(i, 1);
  area.spawnQueue.push(Date.now() + effectiveTimer(areaKey, node.tier - 1) * 1000);
}

// ---- Ground items -------------------------------------------

function dropGround(areaKey, item, qty, x, y) {
  if (qty <= 0) return;
  const area = window.GS.areas[areaKey];
  // small scatter so a burst of drops doesn't stack on one pixel
  const jx = x + rand(-14, 14), jy = y + rand(-14, 14);
  area.ground.push({ id: area.nextGroundId++, item, qty, x: jx, y: jy });
}

function rollAmount(spec) { return spec.min + Math.floor(Math.random() * (spec.max - spec.min + 1)); }

function grantDropsGround(areaKey, node, specs) {
  const c = nodeCenterPx(node);
  for (const spec of specs || []) {
    const amt = rollAmount(spec);
    if (amt > 0) { dropGround(areaKey, spec.item, amt, c.x, c.y); window.GS.stats.totalGathered += amt; }
  }
}

// Vacuum ground items near (x,y) into the hand. Returns amount picked up.
function pickupNear(areaKey, x, y, radius) {
  const area = window.GS.areas[areaKey];
  const near = area.ground
    .map(g => ({ g, d: Math.hypot(g.x - x, g.y - y) }))
    .filter(o => o.d <= radius)
    .sort((a, b) => a.d - b.d);
  let picked = 0;
  for (const { g } of near) {
    if (handSpace() <= 0) break;
    const got = handAdd(g.item, g.qty);
    g.qty -= got; picked += got;
  }
  area.ground = area.ground.filter(g => g.qty > 0);
  return picked;
}

// ---- Harvesting ---------------------------------------------

// Click a live node. Behaviour depends on the area's `interaction`.
function harvestNode(areaKey, nodeId, isAuto) {
  const node = nodeById(areaKey, nodeId);
  if (!node) return false;
  const cfg = D.AREAS[areaKey];
  const tierDef = cfg.tiers[node.tier - 1];

  if (cfg.interaction === "chop") {
    grantDropsGround(areaKey, node, tierDef.perHit);   // a bit each swing
    node.hitsLeft--;
    if (isAuto) node.autoFlash = Date.now() + 400;
    if (node.hitsLeft > 0) return true;
    grantDropsGround(areaKey, node, tierDef.drops);     // felled bonus
    depleteNode(areaKey, node);
    return true;
  }
  if (cfg.interaction === "break") {
    node.hitsLeft--;
    if (isAuto) node.autoFlash = Date.now() + 400;
    if (node.hitsLeft > 0) return true;                 // nothing until it cracks
    grantDropsGround(areaKey, node, tierDef.drops);
    depleteNode(areaKey, node);
    return true;
  }
  // instant (farm) & surface (fishing): one click lands the drops
  grantDropsGround(areaKey, node, tierDef.drops);
  if (isAuto) node.autoFlash = Date.now() + 600;
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
      if (inSpawnZone(areaKey, r, c)) return false;   // no building on wild land
      if (occ.has(r + "," + c)) return false;
    }
  return true;
}

function placeBuilding(areaKey, type, row, col) {
  if (!D.BUILDINGS[type] || !D.BUILDINGS[type].unlocked) return null;
  if (!canPlaceBuilding(areaKey, row, col)) return null;
  const area = window.GS.areas[areaKey];
  const b = { id: area.nextBuildId++, type, row, col, paid: {}, built: false };
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
  if (type === "automation") raw = up.automation >= 3 ? null : D.COSTS.automation[up.automation];
  return raw == null ? null : { [base]: scaled(raw) };
}

function buyUpgrade(areaKey, type) {
  const cost = upgradeCost(areaKey, type);
  if (!cost || !spend(cost)) return false;
  const up = window.GS.areas[areaKey].upgrades;
  if (type === "tier") up.maxTier++;
  else if (type === "speed") up.speed++;
  else if (type === "automation") up.automation++;
  return true;
}

// ---- World / camera -----------------------------------------

function neighborOf(fromArea, dir) {
  const here = D.WORLD.layout[fromArea], delta = D.WORLD.dirs[dir];
  const tx = here.x + delta.x, ty = here.y + delta.y;
  for (const [key, pos] of Object.entries(D.WORLD.layout))
    if (pos.x === tx && pos.y === ty) return key;
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

function unlockArea(areaKey) {
  if (areaKey === "void" || !D.AREAS[areaKey]) return false;
  if (isAreaUnlocked(areaKey)) return moveTo(areaKey);
  const cost = areaUnlockCost(areaKey);
  if (!cost || !spend(cost)) return false;
  window.GS.world.unlocked[areaKey] = true;
  initArea(areaKey);
  window.GS.world.currentArea = areaKey;
  return true;
}

function moveTo(areaKey) {
  if (!isAreaUnlocked(areaKey)) return false;
  window.GS.world.currentArea = areaKey;
  return true;
}

// ---- Ticks --------------------------------------------------

function gameTick() {
  const now = Date.now();
  for (const areaKey of Object.keys(D.AREAS)) {
    if (!isAreaUnlocked(areaKey)) continue;
    const area = window.GS.areas[areaKey];
    const cfg = D.AREAS[areaKey];

    // Fishing: a surfaced fish that isn't caught in time dives (relocates).
    if (cfg.interaction === "surface") {
      for (const node of area.nodes.slice())
        if (node.surfaceUntil && now >= node.surfaceUntil) depleteNode(areaKey, node);
    }
    // Bring queued respawns to life once their timer elapses.
    if (area.spawnQueue.length) {
      const due = area.spawnQueue.filter(t => t <= now);
      area.spawnQueue = area.spawnQueue.filter(t => t > now);
      for (let i = 0; i < due.length; i++) if (!spawnNode(areaKey)) area.spawnQueue.push(now + 500);
    }
  }
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
  handTotal, handSpace, handCount, handAdd, handTakeFirst, handTake, canAfford,
  effectiveTimer, rollTier, zoneRects, inSpawnZone, occupiedCells,
  spawnNode, initArea, nodeById, nodeCenterPx, depleteNode, harvestNode,
  dropGround, grantDropsGround, pickupNear,
  buildingCatalog, buildingFootprint, canPlaceBuilding, placeBuilding,
  buildingNeeds, buildingAt, dropFromHand,
  upgradeCost, buyUpgrade,
  neighborOf, areaUnlockCost, isAreaUnlocked, unlockArea, moveTo,
  gameTick, automationTick,
};
