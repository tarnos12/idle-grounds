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
  // `lock` keeps the container typed even when empty (warding seals and the
  // starter's rare-find storehouses stay tuned to their item)
  if (sh.qty <= 0 && !sh.lock) sh.item = null;
  return take;
}

// Left-click withdraw for any buffer building (storehouse / seal / gatherer).
function withdrawFromBuilding(b, n) {
  const cfg = D.BUILDINGS[b.type];
  if (b.type === "storehouse" || cfg.seal) return takeFromStorehouse(b, n);
  if (cfg.gather || cfg.stoker) {
    let took = 0;
    while (took < (n || 1) && handSpace() > 0 && (b.inv || []).length) {
      const st = b.inv[0];
      if (handAdd(st.item, 1) > 0) { st.qty--; took++; if (st.qty <= 0) b.inv.shift(); }
      else break;
    }
    return took;
  }
  return 0;
}
function scaled(n) { return Math.max(1, Math.ceil(D.TEST.ENABLED ? n * D.TEST.costScale : n)); }

// Is the given dragon-pill blessing active right now?
function buffActive(kind) {
  const b = window.GS.buff;
  return !!(b && b.kind === kind && b.until > Date.now());
}
// Is the Vitality Pill combat buff (Martial Vigor) active?
function combatBuffActive() {
  const b = window.GS.combatBuff;
  return !!(b && b.until > Date.now());
}

// ---- Prestige perks (Ascension Shrine) ----------------------
// Permanent, AP-bought upgrades that persist across every reset.
function perkLevel(id) { return (window.GS.perks && window.GS.perks[id]) || 0; }
function perkDef(id) { return D.PERKS.find(p => p.id === id) || null; }
// AP price of the NEXT level, or null when maxed / unknown.
function perkCost(id) {
  const def = perkDef(id); if (!def) return null;
  const lvl = perkLevel(id);
  return lvl >= def.max ? null : def.cost[lvl];
}
// Buy one level if affordable and not maxed. Returns true on success.
function buyPerk(id) {
  const cost = perkCost(id);
  if (cost == null || (window.GS.ascendPoints || 0) < cost) return false;
  window.GS.ascendPoints -= cost;
  window.GS.perks[id] = perkLevel(id) + 1;
  if (id === "hands") window.GS.handCap += 5;    // apply Fleet Hands live
  return true;
}
// AP earned by ascending NOW: 1 base + 1 per unlocked region beyond Center.
function ascendReward() {
  const regions = Object.values(window.GS.world.unlocked).filter(Boolean).length;
  return 1 + Math.max(0, regions - 1) + perkLevel("apgain");
}
// The offline catch-up window, extended +2h per Long Slumber level.
function offlineCapMs() { return (8 + 2 * perkLevel("slumber")) * 3600 * 1000; }

// Prestige: each completed Ascension shaves 8% off every duration
// (regrowth, batches, lantern beats). Compounds multiplicatively, and the
// Eternal Haste perk shaves a further 5% per level.
function prestigeFactor() {
  // Awakened dragon's blessing: a permanent ~11% global speedup.
  return Math.pow(0.92, window.GS.ascensions || 0) * Math.pow(0.95, perkLevel("haste")) * (window.GS.won ? 0.9 : 1);
}

// Is a Dragon Shrine standing anywhere? (blessings +60s, scales 2x rate)
function shrineBuilt() {
  for (const k of Object.keys(window.GS.areas))
    if (window.GS.areas[k].buildings.some(b => b.built && D.BUILDINGS[b.type].shrine)) return true;
  return false;
}

// The Ascension itself: reset the grounds, keep the prestige counter (and
// spare veterans the tutorial). Saves, then reboots into the fresh run.
function ascend() {
  if (window.onSfx) window.onSfx("ascend");
  const asc = (window.GS.ascensions || 0) + 1;
  const pts = (window.GS.ascendPoints || 0) + ascendReward();
  const perks = window.GS.perks || {};
  const fresh = window.SAVE.fresh();
  fresh.ascensions = asc;
  fresh.ascendPoints = pts;            // AP + perks survive the reset
  fresh.perks = perks;
  fresh.handCap = D.HAND_CAP + 5 * (perks.hands || 0);   // re-apply Fleet Hands
  fresh.quest.idx = D.QUESTS.length;   // veterans skip the tutorial chain
  window.GS = fresh;
  window.SAVE.saveState();
  location.reload();
}

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
      perHit: sp.perHit || null, drops: sp.drops || null, rareDrop: sp.rareDrop || null,
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
    dropMin: fx.dropMin, dropMax: fx.dropMax, rareDrop: fx.rareDrop || null,
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

// ---- Starter wisp network (fresh saves) -----------------------
// Auto-builds a small pre-wired demo so the logistics are alive from the
// first minute: collectors at the stone/wood/clay/fox sources, seals
// filtering the rare finds off to typed storehouses, and lanterns fanning
// wood out to four consumers round-robin.

// Nearest legal placement to an anchor cell (ring-scan outward).
function findSpot(areaKey, type, r0, c0) {
  for (let rad = 0; rad < 14; rad++)
    for (let dr = -rad; dr <= rad; dr++)
      for (let dc = -rad; dc <= rad; dc++) {
        if (Math.max(Math.abs(dr), Math.abs(dc)) !== rad) continue;
        if (canPlaceBuilding(areaKey, r0 + dr, c0 + dc, type)) return { r: r0 + dr, c: c0 + dc };
      }
  return null;
}
function placeBuilt(areaKey, type, r0, c0, extra) {
  const area = window.GS.areas[areaKey];
  const spot = findSpot(areaKey, type, r0, c0);
  if (!spot) return null;
  const b = Object.assign({ id: area.nextBuildId++, type, row: spot.r, col: spot.c,
    paid: {}, built: true, item: null, qty: 0 }, extra || {});
  initLogistics(b);
  area.buildings.push(b);
  return b;
}
function setupStarterNetwork() {
  if (window.GS.starterPlaced) return false;
  const A = "center";
  // producers in the open band south of the Altar
  const forge = placeBuilt(A, "forge", 52, 26);
  const bench = placeBuilt(A, "workbench", 52, 31);
  const mill  = placeBuilt(A, "paper_mill", 52, 36);
  const kiln  = placeBuilt(A, "kiln", 52, 41);
  const array = placeBuilt(A, "infusion_array", 52, 46);
  // typed storehouses for the rare finds — placed NEAR their source
  // (just outside the wild-land corners, beside the quarry / below the tree)
  const shJade   = placeBuilt(A, "storehouse", 60, 26, { item: "jade_shard", lock: true });
  const shBamboo = placeBuilt(A, "storehouse", 26, 36, { item: "bamboo", lock: true });
  // collectors at each source
  const gsStone = placeBuilt(A, "gathering_stone", 62, 18);
  const gsWood  = placeBuilt(A, "gathering_stone", 16, 33);
  const gsClay  = placeBuilt(A, "gathering_stone", 62, 56);
  const gsFox   = placeBuilt(A, "gathering_stone", 12, 62);
  // seals keep the main lines pure
  const sealStone = placeBuilt(A, "warding_seal", 58, 22, { item: "stone", lock: true });
  const sealWood  = placeBuilt(A, "warding_seal", 30, 36, { item: "wood", lock: true });
  // lanterns + links (link order = round-robin send order)
  const L = (lb, from, to) => { if (lb && from && to) lb.links.push({ from: from.id, to: to.id }); };
  const lanQ = placeBuilt(A, "wisp_lantern", 60, 20);
  L(lanQ, gsStone, sealStone); L(lanQ, sealStone, array); L(lanQ, gsStone, shJade);
  const lanT = placeBuilt(A, "wisp_lantern", 22, 36);
  L(lanT, gsWood, sealWood);
  L(lanT, sealWood, forge); L(lanT, sealWood, bench); L(lanT, sealWood, mill); L(lanT, sealWood, kiln);
  L(lanT, gsWood, shBamboo);
  const lanM = placeBuilt(A, "wisp_lantern", 44, 52);
  L(lanM, gsClay, kiln); L(lanM, gsFox, array);
  // seed the sources so every line visibly runs from the first minute
  const seed = (item, n, r, c) => dropGround(A, item, n, (c + 0.5) * CELL, (r + 0.5) * CELL);
  seed("stone", 8, 62, 12); seed("jade_shard", 2, 61, 13);
  seed("wood", 8, 15, 37);  seed("bamboo", 2, 16, 38);
  seed("clay", 6, 62, 62);  seed("spirit_essence", 4, 12, 64);
  window.GS.starterPlaced = true;
  return true;
}

// Remove a relocating node and queue a replacement from its spawner.
function depleteNode(areaKey, node) {
  const area = window.GS.areas[areaKey];
  const i = area.nodes.indexOf(node);
  if (i >= 0) area.nodes.splice(i, 1);
  const speed = window.GS.areas[areaKey].upgrades.speed;
  const scale = D.TEST.ENABLED ? D.TEST.timeScale : 1;
  const buffFac = buffActive("verdant_pill") ? 0.5 : 1;   // Verdant Blessing
  const delay = (node.regrowSec || 10) * Math.pow(0.8, speed) * scale * 1000 * buffFac * prestigeFactor() * Math.pow(0.9, perkLevel("regrow"));
  area.spawnQueue.push({ at: Date.now() + delay, kind: node.spawnerKind });
}

// ---- Ground items (never stack — one icon per item) ---------

function dropGround(areaKey, item, qty, x, y) {
  const area = window.GS.areas[areaKey];
  for (let k = 0; k < qty; k++) {
    const jx = clampPx(x + rand(-16, 16)), jy = clampPx(y + rand(-16, 16));
    area.ground.push({ id: area.nextGroundId++, item, x: jx, y: jy });
  }
  // Feedback juice: the UI hooks this to float a "+N" at the drop. Detached
  // during offline catch-up so a fast-forward doesn't queue a blizzard.
  if (window.onGroundDrop) window.onGroundDrop(areaKey, item, qty, x, y);
}

function rollAmount(spec) { return spec.min + Math.floor(Math.random() * (spec.max - spec.min + 1)); }

function grantDropsGround(areaKey, node, specs, mult) {
  const c = nodeCenterPx(node);
  for (const spec of specs || []) {
    const amt = rollAmount(spec) * (mult || 1);
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
  if (!isAuto && window.onSfx) window.onSfx("harvest", areaKey);   // player swing feedback

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
      let amt = node.dropMin ? rand(node.dropMin, node.dropMax || node.dropMin) : 1;
      if (buffActive("stoneheart_pill")) amt *= 2;   // Stoneheart Blessing
      const c = nodeCenterPx(node);
      dropGround(areaKey, node.dropItem || "stone", amt, c.x, c.y);
      window.GS.stats.totalGathered += amt;
      // rare finds: jade shards in the rock, bamboo shoots at the tree…
      if (node.rareDrop && Math.random() < node.rareDrop.chance) {
        dropGround(areaKey, node.rareDrop.item, 1, c.x, c.y);
        window.GS.stats.totalGathered += 1;
      }
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
    grantDropsGround(areaKey, node, specs.drops, buffActive("stoneheart_pill") ? 2 : 1);
    // rare finds on the final crack (firestone in ore / iron veins)
    if (node.rareDrop && Math.random() < node.rareDrop.chance) {
      const c = nodeCenterPx(node);
      dropGround(areaKey, node.rareDrop.item, 1, c.x, c.y);
      window.GS.stats.totalGathered += 1;
    }
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
  const bCfg = (type && D.BUILDINGS[type]) || {};
  const B = bCfg.size || D.GRID.building;
  if (row < 0 || col < 0 || row + B.h > D.GRID.cells || col + B.w > D.GRID.cells) return false;
  if (bCfg.waterOnly && areaKey !== "fishing") return false;   // water buildings live in the fishing waters
  const occ = occupiedCells(areaKey);
  for (let r = row; r < row + B.h; r++)
    for (let c = col; c < col + B.w; c++) {
      // waterOnly buildings INVERT the rule: every cell must be in the water
      // (fishing's centre zone). anyZone formations (wisp logistics) may sit
      // on wild land. Everything else avoids the noBuild zones.
      if (bCfg.waterOnly ? !cellInZone("centre", r, c)
        : (!bCfg.anyZone && inNoBuild(areaKey, r, c))) return false;
      if (occ.has(r + "," + c)) return false;
    }
  return true;
}

// Per-type mutable fields for logistics buildings.
function initLogistics(b) {
  const cfg = D.BUILDINGS[b.type];
  if ((cfg.gather || cfg.stoker) && !b.inv) b.inv = [];
  if (cfg.lantern) { b.links = b.links || []; b.connIdx = b.connIdx || 0; b.nextSend = 0; }
  if (cfg.roster) { b.disciples = b.disciples || 0; b.buns = b.buns || 0; b.nextCultivate = 0; }
}

// A pavilion's disciple capacity: base + the Disciple Mastery upgrade
// (+2 per level, read from the Center tree — applies to every region).
function rosterCap(b) {
  const cfg = D.BUILDINGS[b.type].roster;
  return cfg.cap + 2 * (window.GS.areas.center.upgrades.discipleCap || 0)
    + perkLevel("hall");   // Master's Hall prestige perk
}
// How many cultivation cycles a food item fuels for this building (0 = not
// accepted). Falls back to the single `food` string for older configs.
function foodValue(b, item) {
  const cfg = D.BUILDINGS[b.type].roster;
  if (cfg.foodValues) return cfg.foodValues[item] || 0;
  return item === cfg.food ? 1 : 0;
}

// Recruit a disciple into a Meditation Pavilion: spends one Robe from the
// hand (up to the pavilion's cap). Returns true on success.
function recruitDisciple(areaKey, id) {
  const b = buildingById(areaKey, id);
  const cfg = b && D.BUILDINGS[b.type].roster;
  if (!cfg || !b.built) return false;
  if ((b.disciples || 0) >= rosterCap(b)) return false;
  if (handCount(cfg.recruit) <= 0) return false;
  handTake(cfg.recruit, 1);
  b.disciples = (b.disciples || 0) + 1;
  window.GS.stats.disciplesRecruited = (window.GS.stats.disciplesRecruited || 0) + 1;
  return true;
}

function placeBuilding(areaKey, type, row, col) {
  if (!isBuildingUnlocked(type)) return null;
  if (!canPlaceBuilding(areaKey, row, col, type)) return null;
  const area = window.GS.areas[areaKey];
  const b = { id: area.nextBuildId++, type, row, col, paid: {}, built: false, item: null, qty: 0 };
  initLogistics(b);
  area.buildings.push(b);
  return b;
}

// ---- Converter buildings (the Forge's smelt recipe) ----------
// A `smelt` config turns fed inputs into queued batches; gameTick works
// through the queue on a timer and drops the output beside the building.

// Converters carry a `recipes` list; `b.recipe` indexes the ACTIVE one.
function recipeOf(b) {
  const list = D.BUILDINGS[b.type] && D.BUILDINGS[b.type].recipes;
  return (list && list[b.recipe || 0]) || null;
}

// ---- Burner fuel: a visible FIFO queue of discrete fuel items -------
// b.fuelQ = [{ item, rem, total }] — newest at index 0 (front), oldest at
// the END (back). Fuel is ADDED at the front and BURNED from the back, so
// the item put in first finishes first. Max D.FUEL_SLOTS items (the 2x2
// rack). fuelBurnAt marks the last time we charged burn against the batch.
function fuelQueue(b) { return (b.fuelQ = b.fuelQ || []); }
function fuelTotal(b) { return fuelQueue(b).reduce((s, f) => s + f.rem, 0); }
function fuelSpace(b) { return D.FUEL_SLOTS - fuelQueue(b).length; }
// Add one fuel item at the front. Returns true if the rack had room.
function addFuelItem(b, item) {
  if (D.FUEL[item] == null || fuelSpace(b) <= 0) return false;
  fuelQueue(b).unshift({ item, rem: D.FUEL[item], total: D.FUEL[item] });
  return true;
}
// Burn `ms` from the back (oldest) items, popping spent ones so the next
// one only starts once the current finishes.
function burnFuel(b, ms) {
  const q = fuelQueue(b);
  while (ms > 0 && q.length) {
    const back = q[q.length - 1];
    const take = Math.min(ms, back.rem);
    back.rem -= take; ms -= take;
    if (back.rem <= 0.5) q.pop();
  }
}
// Switch a converter's active recipe. Everything it holds — the input
// stock AND the batch in progress — drops on the ground first.
function setRecipe(areaKey, buildingId, idx) {
  const b = buildingById(areaKey, buildingId);
  const list = b && D.BUILDINGS[b.type].recipes;
  if (!list || idx < 0 || idx >= list.length) return false;
  if ((b.recipe || 0) === idx) return true;
  const s = buildingSize(b.type);
  const x = (b.col + s.w / 2) * CELL, y = (b.row + s.h / 2) * CELL;
  for (const [item, qty] of Object.entries(b.stock || {}))
    if (qty > 0) dropGround(areaKey, item, qty, x, y);
  if (b.smeltDoneAt > 0) {
    const old = recipeOf(b);
    if (old) for (const [item, qty] of Object.entries(old.inputs)) dropGround(areaKey, item, qty, x, y);
  }
  b.stock = {};
  b.smeltDoneAt = 0;
  b.recipe = idx;
  window.GS.stats.recipeSwitches = (window.GS.stats.recipeSwitches || 0) + 1;
  return true;
}

// Converters hold an INPUT STOCK per item (cap `stockCap`, default 20).
// Batches start automatically whenever the stock covers the recipe; feeding
// (by hand or wisp) only tops the stock up — past the cap it is refused.

// Inputs the NEXT batch still needs beyond current stock: { item: qty }.
function smeltRemaining(b) {
  const cfg = recipeOf(b);
  if (!cfg) return {};
  b.stock = b.stock || {};
  const rem = {};
  for (const [item, qty] of Object.entries(cfg.inputs)) {
    const r = qty - (b.stock[item] || 0);
    if (r > 0) rem[item] = r;
  }
  return rem;
}
// Free stock space per input item (feeding stops at the cap): { item: qty }.
function smeltSpace(b) {
  const cfg = recipeOf(b);
  if (!cfg) return {};
  b.stock = b.stock || {};
  const cap = cfg.stockCap || 20;
  const space = {};
  for (const item of Object.keys(cfg.inputs)) {
    const s = cap - (b.stock[item] || 0);
    if (s > 0) space[item] = s;
  }
  return space;
}
function canStartBatch(b) {
  const cfg = recipeOf(b);
  if (!cfg) return false;
  b.stock = b.stock || {};
  return Object.entries(cfg.inputs).every(([it, q]) => (b.stock[it] || 0) >= q);
}
// How many batches the CURRENT stock could make if no more is fed (fuel is
// deliberately ignored — it answers "what will these materials yield").
function craftsPossible(b) {
  const cfg = recipeOf(b);
  if (!cfg) return 0;
  b.stock = b.stock || {};
  let n = Infinity;
  for (const [it, q] of Object.entries(cfg.inputs)) n = Math.min(n, Math.floor((b.stock[it] || 0) / q));
  return Number.isFinite(n) ? n : 0;
}

// ---- Wisp logistics ------------------------------------------
// Endpoints are buildings: gathering stones (mixed buffer), warding seals
// (typed pass-through), storehouses (typed buffer) and converters (inputs).
// Wisp Lanterns hold links {from,to}; each beat services ONE link round-robin.

function buildingById(areaKey, id) {
  return window.GS.areas[areaKey].buildings.find(b => b.id === id) || null;
}
function buildingCenterPx(b) {
  const s = buildingSize(b.type);
  return { x: (b.col + s.w / 2) * CELL, y: (b.row + s.h / 2) * CELL };
}
function gatherTotal(b) { return (b.inv || []).reduce((s, x) => s + x.qty, 0); }

// Will `b` accept one `item` right now?
function endpointAccepts(b, item) {
  if (!b || !b.built) return false;
  const cfg = D.BUILDINGS[b.type];
  if (cfg.seal) return !!b.item && b.item === item && (b.qty || 0) < (cfg.seal.cap || 5);
  if (b.type === "storehouse") return (b.item ? b.item === item : true) && (b.qty || 0) < storehouseCap();
  if (cfg.gather) return gatherTotal(b) < cfg.gather.cap;
  if (cfg.stoker) return D.FUEL[item] != null && gatherTotal(b) < cfg.stoker.cap;
  if (cfg.roster) return foodValue(b, item) > 0 && (cfg.roster.foodCap - (b.buns || 0)) >= foodValue(b, item);
  if (cfg.recipes) {
    const rec = recipeOf(b);
    const isInput = rec && rec.inputs[item] != null;
    // burners drink fuel items into their visible rack (max FUEL_SLOTS) —
    // UNLESS the item is also an ingredient of the current recipe (e.g.
    // firestone in Ember Pill / Star Steel), which must reach recipe stock.
    if (cfg.fuel && D.FUEL[item] != null && !isInput) return fuelSpace(b) > 0;
    if (!isInput) return false;
    b.stock = b.stock || {};
    return (b.stock[item] || 0) < (rec.stockCap || 20);
  }
  return false;
}
// Which item can `src` supply that `dst` accepts? (null = nothing to send)
function pickTransfer(src, dst) {
  const sCfg = D.BUILDINGS[src.type];
  if (sCfg.gather || sCfg.stoker) {
    const st = (src.inv || []).find(s => s.qty > 0 && endpointAccepts(dst, s.item));
    return st ? st.item : null;
  }
  if (sCfg.seal || src.type === "storehouse")
    return src.item && (src.qty || 0) > 0 && endpointAccepts(dst, src.item) ? src.item : null;
  return null;
}
function endpointTake(b, item) {
  const cfg = D.BUILDINGS[b.type];
  if (cfg.gather || cfg.stoker) {
    const st = (b.inv || []).find(s => s.item === item);
    if (!st || st.qty <= 0) return false;
    st.qty--; if (st.qty <= 0) b.inv = b.inv.filter(s => s !== st);
    return true;
  }
  if (cfg.seal || b.type === "storehouse") {
    if (b.item !== item || (b.qty || 0) <= 0) return false;
    b.qty--; if (b.qty <= 0 && !b.lock) b.item = null;
    return true;
  }
  return false;
}
// Deliver one `item` into `b`. False -> the carrier drops it on the ground.
function endpointGive(b, item) {
  if (!endpointAccepts(b, item)) return false;
  const cfg = D.BUILDINGS[b.type];
  if (cfg.seal || b.type === "storehouse") { if (!b.item) b.item = item; b.qty = (b.qty || 0) + 1; return true; }
  if (cfg.gather || cfg.stoker) {
    const st = (b.inv = b.inv || []).find(s => s.item === item);
    if (st) st.qty++; else b.inv.push({ item, qty: 1 });
    return true;
  }
  if (cfg.roster) { b.buns = Math.min(cfg.roster.foodCap, (b.buns || 0) + foodValue(b, item)); return true; }
  if (cfg.recipes) {
    if (cfg.fuel && D.FUEL[item] != null) return addFuelItem(b, item);
    b.stock = b.stock || {};
    b.stock[item] = (b.stock[item] || 0) + 1;
    return true;
  }
  return false;
}
// Where is wisp `w` right now? Pure function of time: departure point +
// constant speed toward the (static) target centre. frac >= 1 = arrived.
function wispPos(areaKey, w, now) {
  const dst = buildingById(areaKey, w.toId);
  if (!dst) return { x: w.x, y: w.y, frac: 1 };
  const t = buildingCenterPx(dst);
  const D = Math.hypot(t.x - w.x0, t.y - w.y0);
  const frac = D ? Math.min(1, ((now - w.t0) / 1000) * (w.sp || 170) / D) : 1;
  return { x: w.x0 + (t.x - w.x0) * frac, y: w.y0 + (t.y - w.y0) * frac, frac };
}

// Wire a new link onto a lantern.
function addLink(areaKey, lanternId, fromId, toId) {
  const lb = buildingById(areaKey, lanternId);
  if (!lb || !D.BUILDINGS[lb.type].lantern) return false;
  lb.links = lb.links || [];
  lb.links.push({ from: fromId, to: toId });
  window.GS.stats.linksAdded = (window.GS.stats.linksAdded || 0) + 1;
  return true;
}
function removeLink(areaKey, lanternId, index) {
  const lb = buildingById(areaKey, lanternId);
  if (!lb || !lb.links || index < 0 || index >= lb.links.length) return false;
  lb.links.splice(index, 1);
  if (lb.connIdx >= lb.links.length) lb.connIdx = 0;
  return true;
}
// What may anchor a link: sources hold withdrawable stock; targets accept.
function canBeLinkSource(b) {
  if (!b || !b.built) return false;
  const cfg = D.BUILDINGS[b.type];
  return !!(cfg.gather || cfg.stoker || cfg.seal || b.type === "storehouse");
}
function canBeLinkTarget(b) {
  if (!b || !b.built) return false;
  const cfg = D.BUILDINGS[b.type];
  return !!(cfg.gather || cfg.stoker || cfg.seal || b.type === "storehouse" || cfg.recipes || cfg.roster);
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
    if ((b.type === "storehouse" || cfg.seal) && b.item && b.qty > 0) dropGround(areaKey, b.item, b.qty, x, y);
    for (const st of b.inv || []) if (st.qty > 0) dropGround(areaKey, st.item, st.qty, x, y);
    // a converter refunds its whole input stock plus the batch in progress
    if (cfg.recipes) {
      for (const [item, qty] of Object.entries(b.stock || {}))
        if (qty > 0) dropGround(areaKey, item, qty, x, y);
      const rec = recipeOf(b);
      if (b.smeltDoneAt > 0 && rec)
        for (const [item, qty] of Object.entries(rec.inputs))
          dropGround(areaKey, item, qty, x, y);
    }
    // a pavilion refunds its disciples (as Robes); cultivation cycles are
    // consumed, not stored — no refund (like fuel).
    if (cfg.roster) {
      if (b.disciples > 0) dropGround(areaKey, cfg.roster.recruit, b.disciples, x, y);
    }
  } else {
    for (const [item, qty] of Object.entries(b.paid)) dropGround(areaKey, item, qty, x, y);
  }
  area.buildings.splice(i, 1);
  // sever any wisp links that referenced the demolished endpoint
  for (const lb of area.buildings)
    if (lb.links) lb.links = lb.links.filter(l => l.from !== b.id && l.to !== b.id);
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
  // Vitality Pill in the front hand slot is TAKEN (never dropped): it grants
  // the Martial Vigor combat buff. Right-click it anywhere to quaff.
  if (window.GS.hand[0] && window.GS.hand[0].item === D.VITALITY.item) {
    handTake(D.VITALITY.item, 1);
    window.GS.combatBuff = { until: Date.now() + D.VITALITY.ms };
    return { used: D.VITALITY.item };
  }
  // Beast Bait INSIDE the enemy zone always lures (even over a formation
  // that would otherwise catch the drop): a tier-2 beast appears there.
  const firstB = window.GS.hand[0];
  const eCfg = D.AREAS[areaKey].enemies;
  if (firstB && firstB.item === "beast_bait" && eCfg && eCfg.baitSpawn) {
    const z = zoneRects(eCfg.zone)[0];
    if (row >= z.r0 && row <= z.r1 && col >= z.c0 && col <= z.c1) {
      handTake("beast_bait", 1);
      const bs = eCfg.baitSpawn;
      const area2 = window.GS.areas[areaKey];
      area2.enemies.push({ id: area2.nextEnemyId++, x, y, hp: bs.hp, maxHp: bs.hp,
        tx: x, ty: y, hitAt: 0, kind: "boss", sprite: bs.sprite, spd: bs.speed });
      return { fed: "beast_bait" };
    }
  }
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
  // Dragon PILLS (any stage, even awakened): it exhales a timed blessing.
  if (b && b.built && b.type === "dragon") {
    const first0 = window.GS.hand[0];
    if (first0 && D.DRAGON_BUFFS[first0.item]) {
      handTake(first0.item, 1);
      const dur = 60000 + 30000 * (window.GS.areas.center.upgrades.affinity || 0)
        + (shrineBuilt() ? 60000 : 0);   // a Dragon Shrine honours the blessing
      window.GS.buff = { kind: first0.item, until: Date.now() + dur };
      return { fed: first0.item };
    }
    if (!dragonStage()) return null;
    const dr = window.GS.dragon;
    const res = feedNeeds(dragonRemaining(), dr.paid);
    if (res && res.fed && !Object.keys(dragonRemaining()).length) {
      const st = dragonStage();
      dr.stage++; dr.paid = {};
      dr.msg = st.text; dr.msgUntil = Date.now() + 8000;
      dr.dialog = st.text;   // story dialog box (persists until dismissed)
      if (window.onSfx) window.onSfx("dragon", areaKey);
      if (!dragonStage()) window.GS.won = true;   // final stage: it AWAKENS
    }
    return res;
  }
  // Furnace Spirit: right-click feeds it fuel items for its stoking buffer.
  if (b && b.built && D.BUILDINGS[b.type].stoker) {
    const first = window.GS.hand[0];
    if (first && endpointGive(b, first.item)) { handTake(first.item, 1); return { fed: first.item }; }
    for (const it of Object.keys(D.FUEL))
      if (handCount(it) > 0 && endpointAccepts(b, it)) { handMoveToFront(it); return { reordered: it }; }
    return null;
  }
  // Meditation Pavilion: right-click feeds disciple food (Spirit Buns, or
  // Spirit Wine which is worth more).
  if (b && b.built && D.BUILDINGS[b.type].roster) {
    const first = window.GS.hand[0];
    if (first && foodValue(b, first.item) > 0 && endpointGive(b, first.item)) {
      handTake(first.item, 1); return { fed: first.item };
    }
    const foods = D.BUILDINGS[b.type].roster.foodValues || { [D.BUILDINGS[b.type].roster.food]: 1 };
    for (const it of Object.keys(foods))
      if (handCount(it) > 0 && endpointAccepts(b, it)) { handMoveToFront(it); return { reordered: it }; }
    return null;
  }
  // Converter buildings (the Forge): feed recipe inputs into the stock —
  // up to the per-item cap; batches start themselves from the stock.
  // Burners take fuel items (front stack first) straight into their gauge.
  if (b && b.built && recipeOf(b)) {
    b.stock = b.stock || {};
    const bCfg = D.BUILDINGS[b.type];
    const rec = recipeOf(b);
    const first = window.GS.hand[0];
    // A fuel item that is ALSO the current recipe's ingredient (firestone in
    // Ember Pill / Star Steel) is fed as an ingredient, not burnt — otherwise
    // fall through to feedNeeds below so it lands in recipe stock.
    const firstIsInput = first && rec && rec.inputs[first.item] != null;
    if (bCfg.fuel && first && D.FUEL[first.item] != null && !firstIsInput && fuelSpace(b) > 0) {
      handTake(first.item, 1);
      addFuelItem(b, first.item);
      return { fed: first.item };
    }
    const res = feedNeeds(smeltSpace(b), b.stock);
    if (res) return res;
    // nothing the recipe needs — bring carried fuel forward instead
    if (bCfg.fuel && fuelSpace(b) > 0)
      for (const it of Object.keys(D.FUEL))
        if (handCount(it) > 0) { handMoveToFront(it); return { reordered: it }; }
    return null;
  }
  // Warding Seal: right-click TUNES it to the front hand item (consumes
  // nothing) — from then on wisps only route that item through it.
  if (b && b.built && D.BUILDINGS[b.type].seal) {
    const first = window.GS.hand[0];
    if (!first) return null;
    if (b.item !== first.item) { b.item = first.item; b.qty = 0; b.lock = true; }
    return { configured: first.item };
  }
  // Gathering Stone: right-click deposits the front item into its buffer.
  if (b && b.built && D.BUILDINGS[b.type].gather) {
    const first = window.GS.hand[0];
    if (!first) return null;
    if (!endpointGive(b, first.item)) return null;
    handTake(first.item, 1);
    return { fed: first.item };
  }
  if (b && b.built && b.type === "storehouse") return depositToStorehouse(b);
  if (b && !b.built) {
    const res = feedNeeds(buildingNeeds(b), b.paid);
    if (res && res.fed && Object.keys(buildingNeeds(b)).length === 0) {
      b.built = true;
      window.GS.stats.buildingsBuilt = (window.GS.stats.buildingsBuilt || 0) + 1;
      if (window.onSfx) window.onSfx("build", areaKey);
      // completing the Ascension Gate offers the ending
      if (D.BUILDINGS[b.type].gate) window.GS.ascendPrompt = true;
    }
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
  if (type === "enemyCap") return { lvl: up.enemyCap || 0, max: 3 };
  if (type === "damage") return { lvl: up.damage || 0, max: 3 };
  if (type === "aoe") return { lvl: up.aoe || 0, max: 3 };
  if (type === "wispRate") return { lvl: up.wispRate || 0, max: 3 };
  if (type === "affinity") return { lvl: up.affinity || 0, max: 3 };
  if (type === "discipleCap") return { lvl: up.discipleCap || 0, max: 3 };
  return { lvl: 0, max: 0 };
}

// ---- Upgrade jobs (funded by feeding the Center building) ----

function applyUpgrade(areaKey, type) {
  const up = window.GS.areas[areaKey].upgrades;
  window.GS.stats.upgradesApplied = (window.GS.stats.upgradesApplied || 0) + 1;
  if (window.onSfx) window.onSfx("upgrade", areaKey);
  if (type === "tier") up.maxTier++;
  else if (type === "speed") up.speed++;
  else if (type === "harvestSpeed") up.harvestSpeed++;
  else if (type === "automation") up.automation++;
  else if (type === "quarry") up.quarry = (up.quarry || 0) + 1;
  else if (type === "enemyCap") up.enemyCap = (up.enemyCap || 0) + 1;
  else if (type === "damage") up.damage = (up.damage || 0) + 1;
  else if (type === "aoe") up.aoe = (up.aoe || 0) + 1;
  else if (type === "wispRate") up.wispRate = (up.wispRate || 0) + 1;
  else if (type === "affinity") up.affinity = (up.affinity || 0) + 1;
  else if (type === "discipleCap") up.discipleCap = (up.discipleCap || 0) + 1;
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
  const frugal = Math.pow(0.8, perkLevel("frugal"));   // -20% per level
  for (const [item, qty] of Object.entries(base)) out[item] = Math.max(1, Math.ceil(scaled(qty) * frugal));
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
  if (window.onSfx) window.onSfx("unlock", areaKey);
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
      // generators can also surface rare finds (uncapped, chance-gated)
      if (gen.rareDrop && Math.random() < gen.rareDrop.chance)
        dropGround(areaKey, gen.rareDrop.item, 1, (rand(z.c0, z.c1) + 0.5) * CELL, (rand(z.r0, z.r1) + 0.5) * CELL);
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

    // Converter buildings (the Forge): finish the active batch (drop its
    // output beside the building), then start the next straight from the
    // input stock whenever it covers the recipe.
    for (const b of area.buildings) {
      const scfg = b.built && recipeOf(b);
      if (!scfg) continue;
      if (b.smeltDoneAt && now >= b.smeltDoneAt) {
        const bs = buildingSize(b.type);
        const bx = (b.col + bs.w / 2) * CELL, by = (b.row + bs.h) * CELL + 12;
        const qty = scfg.outputQty || 1;
        dropGround(areaKey, scfg.output, qty, bx, by);
        window.GS.stats.totalCrafted += qty;
        if (window.onSfx) window.onSfx("craft", areaKey);
        b.smeltDoneAt = 0;
        changed = true;
      }
      if (!b.smeltDoneAt && canStartBatch(b)) {
        let cost = scfg.timeMs * scale * prestigeFactor();
        // Ember Blessing: burners work twice as fast (and burn half the fuel)
        if (D.BUILDINGS[b.type].fuel && buffActive("ember_pill")) cost *= 0.5;
        // burners need enough fuel to see the whole batch through; it's spent
        // gradually below (matching the burn animation), not up front.
        if (D.BUILDINGS[b.type].fuel && fuelTotal(b) < cost) continue;
        for (const [it, q] of Object.entries(scfg.inputs)) b.stock[it] -= q;
        b.smeltDoneAt = now + cost;
        b.fuelBurnAt = now;
        changed = true;
      }
      // Burn fuel from the back of the rack in real time while a batch runs
      // (silent — the UI animates on-screen burners via animActive).
      if (D.BUILDINGS[b.type].fuel) {
        if (b.smeltDoneAt && now < b.smeltDoneAt) {
          burnFuel(b, (now - (b.fuelBurnAt || now)) * Math.pow(0.85, perkLevel("ember")));
        }
        b.fuelBurnAt = now;   // keep current so idle time never burns a backlog
      }
    }

    // ---- Wisp logistics ------------------------------------------
    for (const b of area.buildings) {
      if (!b.built) continue;
      const bCfg = D.BUILDINGS[b.type];
      // Gathering Stones vacuum nearby ground items into their buffer.
      if (bCfg.gather && gatherTotal(b) < bCfg.gather.cap) {
        const c = buildingCenterPx(b);
        const R = bCfg.gather.radius * CELL;
        const taken = new Set();
        for (const g of area.ground) {
          const dx = c.x - g.x, dy = c.y - g.y, d = Math.hypot(dx, dy);
          if (d > R) continue;
          if (d <= 22) {
            if (gatherTotal(b) < bCfg.gather.cap && endpointGive(b, g.item)) { taken.add(g.id); changed = true; }
            continue;
          }
          const pull = 2 + (1 - d / R) * 4;
          g.x += (dx / d) * Math.min(pull, d);
          g.y += (dy / d) * Math.min(pull, d);
          // Nudge is visual-only: on-screen pulls repaint via animActive(); off-screen movement needs no repaint (matches enemy/wisp movement).
        }
        if (taken.size) area.ground = area.ground.filter(g => !taken.has(g.id));
      }
      // Wisp Lanterns service ONE link per beat, round-robin in added order.
      // The Wisp Haste upgrade quickens the beat and the wisps themselves.
      if (bCfg.lantern && (b.links || []).length && now >= (b.nextSend || 0)) {
        const haste = area.upgrades.wispRate || 0;
        const wind = buffActive("swiftwind_pill") ? 0.5 : 1;   // Swiftwind Blessing
        for (let k = 0; k < b.links.length; k++) {
          const l = b.links[(b.connIdx + k) % b.links.length];
          const src = buildingById(areaKey, l.from), dst = buildingById(areaKey, l.to);
          if (!src || !dst || !src.built) continue;
          const item = pickTransfer(src, dst);
          if (!item) continue;
          endpointTake(src, item);
          const sc = buildingCenterPx(src);
          // parametric flight: position is derived from departure time, so
          // rendering is silky at any framerate regardless of tick rate.
          // fromId lets a refused delivery fly its cargo back home.
          area.wisps.push({ id: area.nextWispId++, x0: sc.x, y0: sc.y, x: sc.x, y: sc.y,
                            item, toId: l.to, fromId: l.from, t0: now,
                            sp: (bCfg.lantern.speed || 170) * (1 + 0.25 * haste) / wind });
          b.connIdx = (b.connIdx + k + 1) % b.links.length;
          b.nextSend = now + (bCfg.lantern.rateMs || 1000) * Math.pow(0.85, haste) * wind * prestigeFactor() * Math.pow(0.9, perkLevel("gale"));
          changed = true;
          break;
        }
        if (now >= (b.nextSend || 0)) b.nextSend = now + 250;   // idle: retry soon
      }
      // Furnace Spirits stoke low burners within their radius from their
      // own fuel buffer (best fuel first).
      if (bCfg.stoker) {
        const c = buildingCenterPx(b);
        const R = bCfg.stoker.radius * CELL;
        for (const t of area.buildings) {
          if (!t.built || !D.BUILDINGS[t.type].fuel) continue;
          if (fuelSpace(t) <= 0) continue;   // rack full
          const tc = buildingCenterPx(t);
          if (Math.hypot(tc.x - c.x, tc.y - c.y) > R + 1.5 * CELL) continue;
          const st = (b.inv || []).slice().sort((s1, s2) => D.FUEL[s2.item] - D.FUEL[s1.item])[0];
          if (!st) break;
          st.qty--; if (st.qty <= 0) b.inv = b.inv.filter(s => s !== st);
          addFuelItem(t, st.item);
          changed = true;
        }
      }
      // Meditation Pavilions: fed disciples cultivate essence each cycle,
      // eating one Spirit Bun per essence produced. Out of buns -> idle.
      if (bCfg.roster && (b.disciples || 0) > 0) {
        if (now >= (b.nextCultivate || 0)) {
          b.nextCultivate = now + bCfg.roster.produceMs * scale * prestigeFactor();
          const worked = Math.min(b.disciples, b.buns || 0);
          if (worked > 0) {
            b.buns -= worked;
            const c = buildingCenterPx(b);
            dropGround(areaKey, bCfg.roster.produce, worked, c.x + rand(-40, 40), (b.row + buildingSize(b.type).h) * CELL + 12);
            changed = true;
          }
        }
      }
    }
    // Wisps carry their cargo to the link target. Flight is time-parametric
    // (see wispPos) — the tick only records positions and handles arrivals.
    if (area.wisps && area.wisps.length) {
      const done = new Set();
      for (const w of area.wisps) {
        const dst = buildingById(areaKey, w.toId);
        if (!dst || !dst.built) { dropGround(areaKey, w.item, 1, w.x, w.y); done.add(w.id); changed = true; continue; }
        const p = wispPos(areaKey, w, now);
        w.x = p.x; w.y = p.y;               // persisted fallback position
        if (p.frac >= 1) {
          if (endpointGive(dst, w.item)) { done.add(w.id); changed = true; }
          else if (!w.returning && w.fromId && buildingById(areaKey, w.fromId)) {
            // target filled up mid-flight (e.g. hand-fed): fly the cargo home
            w.returning = true;
            w.toId = w.fromId;
            w.x0 = p.x; w.y0 = p.y; w.t0 = now;
            changed = true;
          } else {
            dropGround(areaKey, w.item, 1, p.x, p.y + 24);
            done.add(w.id); changed = true;
          }
        }
      }
      if (done.size) area.wisps = area.wisps.filter(w => !done.has(w.id));
    }

    // Enemies: spawn up to the cap, then wander between random waypoints
    // inside their zone. (Movement doesn't set `changed` — the UI animates
    // visible enemies itself, so off-screen wandering costs no repaints.)
    const ecfg = cfg.enemies;
    if (ecfg) {
      const z = zoneRects(ecfg.zone)[0];
      const x0 = (z.c0 + 1) * CELL, x1 = z.c1 * CELL;
      const y0 = (z.r0 + 1) * CELL, y1 = z.r1 * CELL;
      const cap = ecfg.cap + (area.upgrades.enemyCap || 0);   // Spirit Call upgrade
      // baited beasts don't count toward the regular spawn cap
      if (area.enemies.filter(e => e.kind !== "boss").length < cap && now >= (area.enemyRespawnAt || 0)) {
        area.enemies.push({
          id: area.nextEnemyId++, x: rand(x0, x1), y: rand(y0, y1),
          hp: ecfg.hp, maxHp: ecfg.hp, tx: rand(x0, x1), ty: rand(y0, y1), hitAt: 0,
        });
        // each replacement waits a full interval — advance the shared timer on
        // spawn too (not only on kill) or multi-empty slots refill every tick
        area.enemyRespawnAt = now + (ecfg.respawnMs || 5000) * scale;
        changed = true;
      }
      for (const en of area.enemies) {
        const step = (en.spd || ecfg.speed) / 20;   // px per 50ms tick
        const dx = en.tx - en.x, dy = en.ty - en.y, dd = Math.hypot(dx, dy);
        if (dd < step || Math.random() < 0.01) { en.tx = rand(x0, x1); en.ty = rand(y0, y1); }
        else { en.x += (dx / dd) * step; en.y += (dy / dd) * step; }
      }
    }

    // The AWAKENED dragon sheds Dragon Scales beside itself now and then
    // (twice as often while a Dragon Shrine stands; small pile cap).
    if (areaKey === "center" && !D.DRAGON_STAGES[window.GS.dragon.stage]) {
      const interval = 45000 * scale * (shrineBuilt() ? 0.5 : 1);
      if (!window.GS.dragonScaleAt) window.GS.dragonScaleAt = now + interval;
      else if (now >= window.GS.dragonScaleAt) {
        window.GS.dragonScaleAt = now + interval;
        const drg = area.buildings.find(bb => bb.type === "dragon");
        if (drg) {
          const c = buildingCenterPx(drg);
          const near = area.ground.filter(g => g.item === "dragon_scale" &&
            Math.hypot(g.x - c.x, g.y - c.y) <= 6 * CELL).length;
          if (near < 5) {
            dropGround(areaKey, "dragon_scale", 1, c.x + rand(-60, 60), c.y + 90);
            changed = true;
          }
        }
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

// Deal `dmg` to one enemy. A slain enemy drops its loot where it stood and
// schedules the zone's next spawn.
function damageEnemy(areaKey, en, dmg) {
  const area = window.GS.areas[areaKey];
  en.hp -= dmg; en.hitAt = Date.now();
  if (en.hp > 0) return;
  const i = area.enemies.indexOf(en);
  if (i >= 0) area.enemies.splice(i, 1);
  if (window.onSfx) window.onSfx("kill", areaKey);
  const ecfg = D.AREAS[areaKey].enemies;
  // baited tier-2 beasts carry their own loot table and don't touch the
  // regular respawn clock
  const drops = en.kind === "boss" && ecfg.baitSpawn ? ecfg.baitSpawn.drops : ecfg.drops;
  const loot = combatBuffActive() ? D.VITALITY.lootMult : 1;   // Martial Vigor
  for (const spec of drops || []) {
    const amt = rollAmount(spec) * loot;
    if (amt > 0) { dropGround(areaKey, spec.item, amt, en.x, en.y); window.GS.stats.totalGathered += amt; }
  }
  if (en.kind !== "boss") {
    const scale = D.TEST.ENABLED ? D.TEST.timeScale : 1;
    area.enemyRespawnAt = Date.now() + (ecfg.respawnMs || 5000) * scale;
    window.GS.stats.foxKills = (window.GS.stats.foxKills || 0) + 1;
  }
}

// ---- Tutorial quests -----------------------------------------

// Live progress of quest `i` — goals read current state, so anything the
// player already achieved counts immediately.
function questProgress(i) {
  const q = D.QUESTS[i];
  if (!q) return null;
  const p = q.goal();
  return { cur: Math.min(p.cur, p.need), need: p.need, done: p.cur >= p.need };
}
function claimQuest() {
  const gq = window.GS.quest;
  const p = questProgress(gq.idx);
  if (!p || !p.done) return false;
  gq.idx++;
  return true;
}

// One strike on the targeted enemy. Damage scales with the Spirit Blade
// upgrade; with Spirit Wave the strike also ripples to every other enemy
// within `aoe level * 1.5 cells` of the target.
function attackEnemy(areaKey, id) {
  const area = window.GS.areas[areaKey];
  const target = area.enemies.find(en => en.id === id);
  if (!target) return false;
  if (window.onSfx) window.onSfx("hit", areaKey);
  const up = area.upgrades;
  const dmg = 1 + (up.damage || 0) + (combatBuffActive() ? D.VITALITY.bonusDamage : 0) + perkLevel("fury");
  const R = (up.aoe || 0) * 1.5 * CELL;
  const hit = R > 0
    ? area.enemies.filter(en => en === target || Math.hypot(en.x - target.x, en.y - target.y) <= R)
    : [target];
  for (const en of hit) damageEnemy(areaKey, en, dmg);
  return true;
}

function automationTick() {
  let harvested = 0;
  for (const areaKey of Object.keys(D.AREAS)) {
    if (!isAreaUnlocked(areaKey)) continue;
    const level = window.GS.areas[areaKey].upgrades.automation;
    if (level <= 0) continue;
    const budget = D.AUTOMATION_CLICKS[level] + perkLevel("autoboost");
    // Only regrowing field nodes are automated — fixtures (Spirit Tree,
    // quarry rock, spring) give solely to a manual click/hold, never a bot.
    const nodes = window.GS.areas[areaKey].nodes
      .filter(n => !n.deco && !n.fixed).sort((a, b) => b.tier - a.tier);
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

// ---- Offline / idle catch-up --------------------------------
// While the tab was closed the world stood still. On reload we replay the
// PASSIVE economy for the gap by fast-forwarding a virtual clock through the
// real gameTick/automationTick — so every producer (generators, converters,
// wisps, disciples, automation) stays authoritative and can't drift from a
// parallel formula. Everything offline can make is naturally bounded (field
// caps, converter stock + fuel, disciple buns, building buffers), so this
// can't run away. Bounded compute too: a very long absence just widens the
// simulated step (generator intervals are >=1.5s, so a sub-second step stays
// faithful). Returns { elapsedMs, gained:{item:qty} } or null if the gap was
// too short to bother (a plain reload).
const OFFLINE_MAX_TICKS = 45000;          // ~1s worst-case compute on load
const OFFLINE_MIN_MS = 5000;              // ignore reloads / trivial gaps
// (the max window is offlineCapMs(): 8h + 2h per Long Slumber perk level)

// Total units of each item that exist as loot or stock ANYWHERE — ground,
// wisps in flight, and every building store (storehouse qty, gatherer/stoker
// buffers, converter input stock, pavilion buns). Used to diff before/after.
function countHeldItems() {
  const tally = {};
  const add = (it, q) => { if (it && q > 0) tally[it] = (tally[it] || 0) + q; };
  for (const areaKey of Object.keys(D.AREAS)) {
    const area = window.GS.areas[areaKey];
    if (!area) continue;
    for (const g of area.ground) add(g.item, 1);
    for (const w of area.wisps || []) add(w.item, 1);
    for (const b of area.buildings) {
      if (b.item) add(b.item, b.qty || 0);                        // storehouse
      for (const s of b.inv || []) add(s.item, s.qty);            // gatherer/stoker buffer
      for (const it of Object.keys(b.stock || {})) add(it, b.stock[it]);  // converter stock
    }
  }
  return tally;
}

function runOfflineCatchup() {
  const realNow = Date.now;
  const now = realNow();
  const last = window.GS.lastSeen;
  if (!Number.isFinite(last)) return null;      // pre-feature save: skip
  let elapsed = now - last;
  if (elapsed <= OFFLINE_MIN_MS) return null;   // just a reload
  elapsed = Math.min(elapsed, offlineCapMs());
  const step = Math.max(250, Math.ceil(elapsed / OFFLINE_MAX_TICKS));
  const before = countHeldItems();
  const sink = window.onGroundDrop;             // silence "+N" floaters during the sim
  const sfxSink = window.onSfx;                 // and mute SFX for the whole replay
  let virt = last, sinceAuto = 0;
  try {
    window.onGroundDrop = null;
    window.onSfx = null;
    Date.now = () => virt;                      // drive every timer off the virtual clock
    for (; virt < last + elapsed; virt += step) {
      gameTick();
      sinceAuto += step;
      if (sinceAuto >= 1000) { automationTick(); sinceAuto -= 1000; }
    }
  } finally {
    Date.now = realNow;                         // ALWAYS restore, even if a tick throws
    window.onGroundDrop = sink;
    window.onSfx = sfxSink;
  }
  const after = countHeldItems();
  const gained = {};
  for (const it of Object.keys(after)) {
    const d = after[it] - (before[it] || 0);
    if (d > 0) gained[it] = d;
  }
  return { elapsedMs: elapsed, gained };
}

window.ENGINE = {
  itemName, itemIcon,
  countHeldItems, runOfflineCatchup,
  handTotal, handCap, handSpace, handCount, handAdd, handTakeFirst, handTake, canAfford,
  depositToStorehouse, takeFromStorehouse,
  effectiveTimer, harvestInterval, rollTier, zoneRects, noBuildRects, inNoBuild, occupiedCells,
  spawnFromSpawner, placeFixture, initArea, nodeById, nodeCenterPx, depleteNode, harvestNode,
  dropGround, grantDropsGround, settleGround, pickupNear, suctionStep, pushOutOfColliders,
  buildingCatalog, buildingSize, buildingFootprint, canPlaceBuilding, placeBuilding,
  buildingNeeds, buildingAt, dropFromHand, isBuildingUnlocked, recipeOf, setRecipe, smeltRemaining,
  fuelQueue, fuelTotal, fuelSpace, craftsPossible,
  buildingById, buildingCenterPx, gatherTotal, withdrawFromBuilding, addLink, removeLink,
  canBeLinkSource, canBeLinkTarget, setupStarterNetwork,
  wispPos, endpointAccepts, endpointGive, smeltSpace,
  questProgress, claimQuest, buffActive, combatBuffActive, prestigeFactor, shrineBuilt, ascend, recruitDisciple, rosterCap,
  perkLevel, perkDef, perkCost, buyPerk, ascendReward,
  upgradeCost, upgradeLevel, selectUpgrade, refundUpgradeJob, demolishBuilding,
  jobRemaining, dragonStage, dragonRemaining, enemyAt, attackEnemy,
  regionOrigin, regionAt, areaUnlockCost, isAreaUnlocked, unlockArea,
  gameTick, automationTick,
};
