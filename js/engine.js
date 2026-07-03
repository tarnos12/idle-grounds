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
// Bring an item's stack to the front of the hand (slot 1 = the active item
// that right-click drops/feeds next). Returns true if the item is in hand.
function handMoveToFront(item) {
  const idx = window.GS.hand.findIndex(x => x.item === item);
  if (idx < 0) return false;
  if (idx > 0) window.GS.hand.unshift(window.GS.hand.splice(idx, 1)[0]);
  return true;
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
  // same rule as ghosts: if the matching item isn't the front stack, the
  // click just reorders it to the front; the next click deposits
  if (window.GS.hand[0].item !== sh.item) { handMoveToFront(sh.item); return { reordered: sh.item }; }
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


// Tiers are removed from the game: every resource spawns as its single base
// type (one tree, one crop, one ore, one fish). Kept as a function so the
// spawn path stays unchanged if tiers ever return.
function rollTier() { return 1; }

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

// Footprint of a building type (most use the default 3x2; the Altar is 5x5).
function buildingSize(type) {
  return (D.BUILDINGS[type] && D.BUILDINGS[type].size) || D.GRID.building;
}

// Set of "r,c" cells occupied by live nodes and placed buildings.
function occupiedCells(areaKey) {
  const set = new Set();
  const area = window.GS.areas[areaKey];
  for (const n of area.nodes)
    for (let r = n.row; r < n.row + n.size; r++)
      for (let c = n.col; c < n.col + n.size; c++) set.add(r + "," + c);
  for (const b of area.buildings) {
    const s = buildingSize(b.type);
    for (let r = b.row; r < b.row + s.h; r++)
      for (let c = b.col; c < b.col + s.w; c++) set.add(r + "," + c);
  }
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
    fixed: true, tier: 1, deco: fx.interaction === "none",   // inert fixtures are pure scenery
    clicks: 0, clicksPerDrop: fx.clicksPerDrop, dropItem: fx.drop,
    dropMin: fx.dropMin, dropMax: fx.dropMax,
    swingMs: fx.swingMs || 1000, sprite: fx.sprite || "⛰️", autoFlash: 0,
  });
}

// Fill an area to its spawner targets, place fixtures, seed generators.
// Node targets scale with the play area (relative to the original 24x24) so a
// bigger map stays populated at a similar density.
function areaScale() { return Math.max(1, Math.round((D.GRID.cells / 24) ** 2)); }

// Ring of decorative, non-interactive border trees around a cell rect.
// Purely scenery: the trees are LARGER than gameplay trees and jittered in
// size/position, and the ring leaves an ENTRANCE gap on the side that faces
// the centre of the map.
function placeDecoRing(areaKey, rect, sprite) {
  const area = window.GS.areas[areaKey];
  const occ = occupiedCells(areaKey);
  const N = D.GRID.cells;
  const cx = (rect.c0 + rect.c1 + 1) / 2, cy = (rect.r0 + rect.r1 + 1) / 2;
  const aim = Math.atan2(N / 2 - cy, N / 2 - cx);   // direction toward the map centre
  for (let r = rect.r0 - 1; r <= rect.r1 + 1; r++) {
    for (let c = rect.c0 - 1; c <= rect.c1 + 1; c++) {
      const onRing = r === rect.r0 - 1 || r === rect.r1 + 1 || c === rect.c0 - 1 || c === rect.c1 + 1;
      if (!onRing || r < 0 || c < 0 || r >= N || c >= N) continue;
      if (occ.has(r + "," + c)) continue;
      // entrance: skip ring cells within ~±32° of the centre-facing direction
      const ang = Math.atan2(r + 0.5 - cy, c + 0.5 - cx);
      const diff = Math.atan2(Math.sin(ang - aim), Math.cos(ang - aim));
      if (Math.abs(diff) < 0.55) continue;
      area.nodes.push({ id: area.nextNodeId++, row: r, col: c, size: 1, kind: "deco",
        interaction: "none", deco: true, tier: 1, sprite,
        decoScale: 1.6 + Math.random() * 0.8,        // 48..72px — bigger than play trees' cells
        decoDx: rand(-6, 6), decoDy: rand(-3, 3),    // organic jitter
        autoFlash: 0 });
      occ.add(r + "," + c);
    }
  }
}

function initArea(areaKey) {
  const cfg = D.AREAS[areaKey], area = window.GS.areas[areaKey];
  // The indestructible Altar (type "center") anchors the upgrade system:
  // a 5x5 placed EXACTLY centred, before spawners so nothing overlaps it.
  if (areaKey === "center" && !area.buildings.some(b => b.type === "center")) {
    const s = buildingSize("center");
    area.buildings.push({
      id: area.nextBuildId++, type: "center",
      row: (D.GRID.cells - s.h) / 2, col: (D.GRID.cells - s.w) / 2,   // 75-5 -> 35: exact centre
      paid: {}, built: true, item: null, qty: 0,
    });
  }
  // The Sleeping Dragon: pre-placed dead centre of the top-left corner zone.
  if (areaKey === "center" && !area.buildings.some(b => b.type === "dragon")) {
    const s = buildingSize("dragon");
    const z = zoneRects("cornerTL")[0];
    area.buildings.push({
      id: area.nextBuildId++, type: "dragon",
      row: z.r0 + Math.floor((z.r1 - z.r0 + 1 - s.h) / 2),
      col: z.c0 + Math.floor((z.c1 - z.c0 + 1 - s.w) / 2),
      paid: {}, built: true, item: null, qty: 0,
    });
  }
  // fixtures first (fixed positions), then their decorative borders,
  // then the random spawners fill in around everything.
  for (const fx of cfg.fixtures || [])
    if (!area.nodes.some(n => n.kind === fx.kind)) placeFixture(areaKey, fx);
  if (areaKey === "center" && !area.nodes.some(n => n.kind === "deco")) {
    // the rings border the WHOLE reserved corner blocks (the light-green
    // tinted zones), not just the quarry rock / clay field inside them
    for (const z of D.ZONES.cornerBL) placeDecoRing(areaKey, z, "🌲");
    for (const z of D.ZONES.cornerBR) placeDecoRing(areaKey, z, "🌲");
  }
  const factor = areaScale();
  for (const sp of cfg.spawners || []) {
    const target = sp.scaleWithArea === false ? sp.target : sp.target * factor;
    let guard = 0;
    const live = () => area.nodes.filter(n => n.spawnerKind === sp.kind).length;
    while (live() < target && guard++ < target * 8 + 50) if (!spawnFromSpawner(areaKey, sp)) break;
    // and TRIM overshoot — an old save keeps its nodes, but a config that
    // lowered this spawner's target should apply to it too
    if (live() > target) {
      const excess = new Set(area.nodes.filter(n => n.spawnerKind === sp.kind).slice(target).map(n => n.id));
      area.nodes = area.nodes.filter(n => !excess.has(n.id));
    }
  }
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

// Buildings (incl. the Altar) and fixed nodes (the big quarry stone) are
// solid to ground items: anything inside a footprint is pushed out through
// the nearest edge. Returns how many items were pushed.
function pushOutOfColliders(areaKey) {
  const area = window.GS.areas[areaKey];
  if (!area.ground.length) return 0;
  const rects = [];
  for (const b of area.buildings) {
    const s = buildingSize(b.type);
    rects.push({ x0: b.col * CELL, y0: b.row * CELL, x1: (b.col + s.w) * CELL, y1: (b.row + s.h) * CELL });
  }
  for (const n of area.nodes)
    if (n.fixed) rects.push({ x0: n.col * CELL, y0: n.row * CELL, x1: (n.col + n.size) * CELL, y1: (n.row + n.size) * CELL });
  if (!rects.length) return 0;
  let moved = 0;
  for (const g of area.ground) {
    for (const r of rects) {
      if (g.x <= r.x0 || g.x >= r.x1 || g.y <= r.y0 || g.y >= r.y1) continue;
      const dl = g.x - r.x0, dr = r.x1 - g.x, dt = g.y - r.y0, db = r.y1 - g.y;
      const m = Math.min(dl, dr, dt, db);
      if (m === dl) g.x = r.x0 - 8; else if (m === dr) g.x = r.x1 + 8;
      else if (m === dt) g.y = r.y0 - 8; else g.y = r.y1 + 8;
      g.x = clampPx(g.x); g.y = clampPx(g.y);
      moved++;
    }
  }
  return moved;
}

// Gravity suction while holding left: items within `radius` of the cursor
// are pulled toward it (faster the closer they get); once they reach the
// cursor they're collected as usual. Does nothing when the hand is full.
function suctionStep(areaKey, x, y, radius) {
  if (handSpace() <= 0) return { moved: 0, picked: 0 };
  const area = window.GS.areas[areaKey];
  let moved = 0, picked = 0;
  const taken = new Set();
  for (const g of area.ground) {
    const dx = x - g.x, dy = y - g.y, d = Math.hypot(dx, dy);
    if (d > radius) continue;
    if (d <= 12) {                                   // reached the cursor — collect
      if (handSpace() > 0 && handAdd(g.item, 1) > 0) { taken.add(g.id); picked++; }
      continue;
    }
    const pull = 1 + (1 - d / radius) * 3;           // gentle gravity: stronger when closer
    g.x += (dx / d) * Math.min(pull, d);
    g.y += (dy / d) * Math.min(pull, d);
    moved++;
  }
  if (taken.size) area.ground = area.ground.filter(g => !taken.has(g.id));
  return { moved, picked };
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
  if (!node || node.deco) return false;   // decorative nodes can't be interacted with

  // AUTO badge should stay solid while auto-mining: last longer than the gap
  // between auto-swings (and the 1s automation tick).
  const flashMs = Math.max(node.swingMs || 400, 1000) + 300;

  // Every swing on a chop/break/quarry node plays the hit squash animation
  // (the UI also stamps this for throttled clicks that don't count).
  if (node.interaction === "chop" || node.interaction === "break" || node.interaction === "quarry")
    node.hitAt = Date.now();

  if (node.interaction === "quarry") {
    // manual gathering has NO cap: every `clicksPerDrop` clicks drops
    // `dropMin..dropMax` (default 1) of the fixture's item (any passive
    // production is handled separately by the area's generator)
    node.clicks = (node.clicks || 0) + 1;
    if (isAuto) node.autoFlash = Date.now() + flashMs;
    if (node.clicks >= (node.clicksPerDrop || 5)) {
      node.clicks = 0;
      const amt = node.dropMin ? rand(node.dropMin, node.dropMax || node.dropMin) : 1;
      const c = nodeCenterPx(node);
      dropGround(areaKey, node.dropItem || "stone", amt, c.x, c.y);
      window.GS.stats.totalGathered += amt;
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

// Unlocked outright, or taught by the Sleeping Dragon (stageUnlock).
function isBuildingUnlocked(type) {
  const b = D.BUILDINGS[type];
  if (!b) return false;
  return !!b.unlocked || (b.stageUnlock != null && (window.GS.dragon.stage || 0) >= b.stageUnlock);
}

function buildingCatalog() {
  return Object.entries(D.BUILDINGS)
    .filter(([id]) => isBuildingUnlocked(id))
    .map(([id, b]) => ({ id, ...b }));
}

function buildingFootprint(row, col) {
  const B = D.GRID.building;
  return { row, col, h: B.h, w: B.w };
}

// Is cell (r,c) inside a named zone?
function cellInZone(zoneKey, r, c) {
  return zoneRects(zoneKey).some(z => r >= z.r0 && r <= z.r1 && c >= z.c0 && c <= z.c1);
}

function canPlaceBuilding(areaKey, row, col, type) {
  const B = D.GRID.building;
  if (row < 0 || col < 0 || row + B.h > D.GRID.cells || col + B.w > D.GRID.cells) return false;
  const water = type && D.BUILDINGS[type] && D.BUILDINGS[type].waterOnly;
  if (water && areaKey !== "fishing") return false;   // water buildings live in the fishing waters
  const occ = occupiedCells(areaKey);
  for (let r = row; r < row + B.h; r++)
    for (let c = col; c < col + B.w; c++) {
      // waterOnly buildings INVERT the rule: every cell must be in the water
      // (fishing's centre zone); everything else avoids the wild land.
      if (water ? !cellInZone("centre", r, c) : inNoBuild(areaKey, r, c)) return false;
      if (occ.has(r + "," + c)) return false;
    }
  return true;
}

function placeBuilding(areaKey, type, row, col) {
  if (!isBuildingUnlocked(type)) return null;
  if (!canPlaceBuilding(areaKey, row, col, type)) return null;
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
  return window.GS.areas[areaKey].buildings.find(b => {
    const s = buildingSize(b.type);
    return row >= b.row && row < b.row + s.h && col >= b.col && col < b.col + s.w;
  }) || null;
}

// Destroy a building. A COMPLETE building refunds 100% of its build cost
// (plus a storehouse's contents); a ghost refunds only what was inserted.
// Everything drops on the ground at the building. The Center is protected.
function demolishBuilding(areaKey, buildingId) {
  const area = window.GS.areas[areaKey];
  const i = area.buildings.findIndex(b => b.id === buildingId);
  if (i < 0) return false;
  const b = area.buildings[i];
  const cfg = D.BUILDINGS[b.type];
  if (cfg.indestructible) return false;
  const s = buildingSize(b.type);
  const x = (b.col + s.w / 2) * CELL, y = (b.row + s.h / 2) * CELL;
  if (b.built) {
    for (const [item, qty] of Object.entries(cfg.cost)) dropGround(areaKey, item, qty, x, y);
    if (b.type === "storehouse" && b.item && b.qty > 0) dropGround(areaKey, b.item, b.qty, x, y);
  } else {
    for (const [item, qty] of Object.entries(b.paid)) dropGround(areaKey, item, qty, x, y);
  }
  area.buildings.splice(i, 1);
  return true;
}

// ---- Right-click drop: feed a ghost, else drop on the ground -

// Shared feeding rule (ghosts / altar / dragon): if the FRONT hand stack is
// something `rem` still needs, spend 1 into `paid`; otherwise the click just
// reorders a needed item we carry to the front (the NEXT click feeds it).
function feedNeeds(rem, paid) {
  const first = window.GS.hand[0];
  if (first && rem[first.item]) {
    handTake(first.item, 1);
    paid[first.item] = (paid[first.item] || 0) + 1;
    return { fed: first.item };
  }
  for (const item of Object.keys(rem))
    if (handCount(item) > 0) { handMoveToFront(item); return { reordered: item }; }
  return null;
}

function dropFromHand(areaKey, x, y) {
  const col = Math.floor(x / CELL), row = Math.floor(y / CELL);
  const b = buildingAt(areaKey, row, col);
  // Center building: feed the selected upgrade job (multi-resource).
  if (b && b.built && b.type === "center") {
    const job = window.GS.upgradeJob;
    if (!job) return null;
    const res = feedNeeds(jobRemaining(job), job.paid);
    if (res && res.fed && !Object.keys(jobRemaining(job)).length) {
      applyUpgrade(job.area, job.type);
      window.GS.upgradeJob = null;
    }
    return res;
  }
  // The Sleeping Dragon: feed its current stage's tribute; when sated it
  // advances a stage (unlocking recipes) and murmurs its stage text.
  if (b && b.built && b.type === "dragon") {
    if (!dragonStage()) return null;
    const dr = window.GS.dragon;
    const res = feedNeeds(dragonRemaining(), dr.paid);
    if (res && res.fed && !Object.keys(dragonRemaining()).length) {
      const st = dragonStage();
      dr.stage++; dr.paid = {};
      dr.msg = st.text; dr.msgUntil = Date.now() + 8000;
    }
    return res;
  }
  if (b && b.built && b.type === "storehouse") return depositToStorehouse(b);
  if (b && !b.built) {
    const res = feedNeeds(buildingNeeds(b), b.paid);
    if (res && res.fed && Object.keys(buildingNeeds(b)).length === 0) b.built = true;
    return res ? Object.assign(res, { building: b.id }) : null;
  }
  const item = handTakeFirst();
  if (!item) return null;
  dropGround(areaKey, item, 1, x, y);
  return { dropped: item };
}

// ---- Upgrades (paid from the hand) --------------------------

// Each tree node carries its own per-level cost maps (multi-resource, max 3
// types). Returns { item: qty } scaled for TEST mode, or null when maxed.
function upgradeCost(areaKey, type) {
  const { lvl, max } = upgradeLevel(areaKey, type);
  if (max <= 0 || lvl >= max) return null;
  const node = D.UPGRADE_TREE.find(n => n.area === areaKey && n.type === type);
  const raw = node && node.costs && node.costs[lvl];
  if (!raw) return null;
  const out = {};
  for (const [item, qty] of Object.entries(raw)) out[item] = scaled(qty);
  return out;
}

// Bought levels + max for an upgrade — drives the tree display.
function upgradeLevel(areaKey, type) {
  const up = window.GS.areas[areaKey].upgrades;
  if (type === "tier") return { lvl: up.maxTier - 1, max: 4 };
  if (type === "speed") return { lvl: up.speed, max: 3 };
  if (type === "harvestSpeed") return { lvl: up.harvestSpeed, max: 3 };
  if (type === "automation") return { lvl: up.automation, max: 3 };
  if (type === "quarry") return { lvl: up.quarry || 0, max: 3 };
  if (type === "hand") return { lvl: window.GS.handLevel || 0, max: 3 };
  return { lvl: 0, max: 0 };
}

// ---- Upgrade jobs (funded by feeding the Center building) ----

function applyUpgrade(areaKey, type) {
  const up = window.GS.areas[areaKey].upgrades;
  if (type === "tier") up.maxTier++;
  else if (type === "speed") up.speed++;
  else if (type === "harvestSpeed") up.harvestSpeed++;
  else if (type === "automation") up.automation++;
  else if (type === "quarry") up.quarry = (up.quarry || 0) + 1;
  else if (type === "hand") { window.GS.handLevel = (window.GS.handLevel || 0) + 1; window.GS.handCap += 5; }
}

// Per-item amounts an upgrade job still needs: { item: qty }.
function jobRemaining(job) {
  const rem = {};
  for (const [item, qty] of Object.entries(job.needs || {})) {
    const r = qty - (job.paid[item] || 0);
    if (r > 0) rem[item] = r;
  }
  return rem;
}

// Drop whatever was inserted into the current job on the ground by the
// Center building, then clear the job.
function refundUpgradeJob() {
  const job = window.GS.upgradeJob;
  window.GS.upgradeJob = null;
  if (!job) return;
  const cb = window.GS.areas.center.buildings.find(b => b.type === "center");
  const s = buildingSize("center");
  const x = cb ? (cb.col + s.w / 2) * CELL : PLAY_PX / 2;
  const y = cb ? (cb.row + s.h / 2) * CELL : PLAY_PX / 2;
  for (const [item, qty] of Object.entries(job.paid || {}))
    if (qty > 0) dropGround("center", item, qty, x, y);
}

// Pick the Center building's active upgrade project. Switching away from a
// partially-fed job drops its inserted resources on the ground first.
function selectUpgrade(areaKey, type) {
  const cost = upgradeCost(areaKey, type);
  if (!cost) return false;
  const job = window.GS.upgradeJob;
  if (job && job.area === areaKey && job.type === type) return true;  // already selected
  if (job) refundUpgradeJob();
  window.GS.upgradeJob = { area: areaKey, type, needs: cost, paid: {} };
  return true;
}

// ---- The Sleeping Dragon (automated recipe-unlock progression) ----

// The dragon's CURRENT stage definition, or null once fully progressed.
function dragonStage() { return D.DRAGON_STAGES[window.GS.dragon.stage] || null; }

// Per-item tribute the current stage still wants (TEST-scaled): { item: qty }.
function dragonRemaining() {
  const st = dragonStage();
  if (!st) return {};
  const rem = {};
  for (const [item, qty] of Object.entries(st.needs)) {
    const r = scaled(qty) - (window.GS.dragon.paid[item] || 0);
    if (r > 0) rem[item] = r;
  }
  return rem;
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
      // some generators speed up with an upgrade (e.g. quarry stone output)
      const upLvl = gen.upgrade ? (area.upgrades[gen.upgrade] || 0) : 0;
      area.genTimers[gi] = now + gen.intervalMs * scale * Math.pow(0.8, upLvl);
      // the cap counts only items lying INSIDE this generator's field —
      // items mined/carried elsewhere don't block passive production
      const z = zoneRects(gen.zone)[0];
      const fx0 = z.c0 * CELL - 16, fx1 = (z.c1 + 1) * CELL + 16;
      const fy0 = z.r0 * CELL - 16, fy1 = (z.r1 + 1) * CELL + 16;
      const inField = area.ground.filter(g => g.item === gen.item &&
        g.x >= fx0 && g.x <= fx1 && g.y >= fy0 && g.y <= fy1).length;
      if (inField >= gen.cap) return;
      dropGround(areaKey, gen.item, 1, (rand(z.c0, z.c1) + 0.5) * CELL, (rand(z.r0, z.r1) + 0.5) * CELL);
      changed = true;
    });

    // Generator BUILDINGS (e.g. the Algae Farm) drip their item around
    // themselves up to a nearby cap.
    for (const b of area.buildings) {
      const gcfg = b.built && D.BUILDINGS[b.type].gen;
      if (!gcfg) continue;
      if (now < (b.nextGen || 0)) continue;
      b.nextGen = now + gcfg.intervalMs * scale;
      const bs = buildingSize(b.type);
      const bx = (b.col + bs.w / 2) * CELL, by = (b.row + bs.h / 2) * CELL;
      const R = 4 * CELL;
      const near = area.ground.filter(g => g.item === gcfg.item &&
        Math.hypot(g.x - bx, g.y - by) <= R).length;
      if (near >= gcfg.cap) continue;
      dropGround(areaKey, gcfg.item, 1, bx + rand(-R / 2, R / 2), by + rand(-R / 2, R / 2));
      changed = true;
    }

    // Enemies: spawn up to the cap, then wander between random waypoints
    // inside their zone. (Movement doesn't set `changed` — the UI animates
    // visible enemies itself, so off-screen wandering costs no repaints.)
    const ecfg = cfg.enemies;
    if (ecfg) {
      const z = zoneRects(ecfg.zone)[0];
      const x0 = (z.c0 + 1) * CELL, x1 = z.c1 * CELL;
      const y0 = (z.r0 + 1) * CELL, y1 = z.r1 * CELL;
      if (area.enemies.length < ecfg.cap && now >= (area.enemyRespawnAt || 0)) {
        area.enemies.push({
          id: area.nextEnemyId++, x: rand(x0, x1), y: rand(y0, y1),
          hp: ecfg.hp, maxHp: ecfg.hp, tx: rand(x0, x1), ty: rand(y0, y1), hitAt: 0,
        });
        changed = true;
      }
      const step = ecfg.speed / 10;                 // px per 100ms tick
      for (const en of area.enemies) {
        const dx = en.tx - en.x, dy = en.ty - en.y, dd = Math.hypot(dx, dy);
        if (dd < step || Math.random() < 0.01) { en.tx = rand(x0, x1); en.ty = rand(y0, y1); }
        else { en.x += (dx / dd) * step; en.y += (dy / dd) * step; }
      }
    }

    // Separate overlapping ground items (gravity-like repulsion), and keep
    // them out of solid footprints (buildings, the quarry stone).
    if (area.ground.length > 1 && settleGround(areaKey) > 0) changed = true;
    if (pushOutOfColliders(areaKey) > 0) changed = true;
  }
  return changed;
}

// ---- Enemies (click-combat) ----------------------------------

function enemyAt(areaKey, x, y) {
  return window.GS.areas[areaKey].enemies.find(en => Math.hypot(en.x - x, en.y - y) <= 22) || null;
}

// One hit. A slain enemy drops its loot where it stood and schedules the
// zone's next spawn.
function attackEnemy(areaKey, id) {
  const area = window.GS.areas[areaKey];
  const i = area.enemies.findIndex(en => en.id === id);
  if (i < 0) return false;
  const en = area.enemies[i];
  en.hp -= 1; en.hitAt = Date.now();
  if (en.hp <= 0) {
    area.enemies.splice(i, 1);
    const ecfg = D.AREAS[areaKey].enemies;
    for (const spec of ecfg.drops || []) {
      const amt = rollAmount(spec);
      if (amt > 0) { dropGround(areaKey, spec.item, amt, en.x, en.y); window.GS.stats.totalGathered += amt; }
    }
    const scale = D.TEST.ENABLED ? D.TEST.timeScale : 1;
    area.enemyRespawnAt = Date.now() + (ecfg.respawnMs || 5000) * scale;
  }
  return true;
}

function automationTick() {
  let harvested = 0;
  for (const areaKey of Object.keys(D.AREAS)) {
    if (!isAreaUnlocked(areaKey)) continue;
    const level = window.GS.areas[areaKey].upgrades.automation;
    if (level <= 0) continue;
    const budget = D.AUTOMATION_CLICKS[level];
    const nodes = window.GS.areas[areaKey].nodes.filter(n => !n.deco).sort((a, b) => b.tier - a.tier);
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
  effectiveTimer, harvestInterval, rollTier, zoneRects, noBuildRects, inNoBuild, occupiedCells,
  spawnFromSpawner, placeFixture, initArea, nodeById, nodeCenterPx, depleteNode, harvestNode,
  dropGround, grantDropsGround, settleGround, pickupNear, suctionStep, pushOutOfColliders,
  buildingCatalog, buildingSize, buildingFootprint, canPlaceBuilding, placeBuilding,
  buildingNeeds, buildingAt, dropFromHand, isBuildingUnlocked,
  upgradeCost, upgradeLevel, selectUpgrade, refundUpgradeJob, demolishBuilding,
  jobRemaining, dragonStage, dragonRemaining, enemyAt, attackEnemy,
  regionOrigin, regionAt, areaUnlockCost, isAreaUnlocked, unlockArea,
  gameTick, automationTick,
};
