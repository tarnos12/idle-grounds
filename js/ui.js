/* ============================================================
   Idle Grounds — UI rendering + mouse interaction
   All world interaction is cursor/GS hit-tested at the viewport
   level, so the world DOM can rebuild freely without losing clicks.
   ============================================================ */

const E = window.ENGINE;
const DD = window.DATA;
const G = DD.GRID;
// NOTE: `CELL` is declared in engine.js; classic scripts share one global
// scope, so we reuse it here rather than redeclaring (which would throw).
const MARGIN = G.margin;              // inert border cells each side
const OFF = MARGIN * G.cell;          // px offset of the play area inside the world
const WORLD_PX = (G.cells + 2 * MARGIN) * G.cell;  // 34 * 32 = 1088
const VIEW_PX = G.cells * G.cell;     // viewport size = the play area (768)

const $ = sel => document.querySelector(sel);
function el(tag, cls, html) {
  const n = document.createElement(tag);
  if (cls) n.className = cls;
  if (html != null) n.innerHTML = html;
  return n;
}

// ---- input state --------------------------------------------
// cursor.x/y are in PLAY coordinates (0..VIEW_PX); negative / out-of-range
// means the cursor is over the inert margin (cursor.inPlay === false).
const cursor = { x: 0, y: 0, cx: 0, cy: 0, over: false, inPlay: false, row: -1, col: -1 };
let leftHeld = false, rightHeld = false, pickupMode = false;
let holdStart = 0, lastDrop = 0, loopRunning = false;

// camera pan (px offset of the world within the viewport), clamped to bounds
const cam = { x: (WORLD_PX - VIEW_PX) / 2, y: (WORLD_PX - VIEW_PX) / 2 };
const CAM_MAX = WORLD_PX - VIEW_PX;
const keys = new Set();
const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
let panning = false, panStartX = 0, panStartY = 0, camStartX = 0, camStartY = 0;
function recenterCamera() { cam.x = CAM_MAX / 2; cam.y = CAM_MAX / 2; }
function applyCamera() { $("#grid").style.transform = `translate(${-cam.x}px, ${-cam.y}px)`; }

function area() { return window.GS.world.currentArea; }
function pointFromEvent(e) {
  const r = $("#grid").getBoundingClientRect();   // reflects the camera transform
  const rawX = Math.max(0, Math.min(WORLD_PX - 1, e.clientX - r.left));
  const rawY = Math.max(0, Math.min(WORLD_PX - 1, e.clientY - r.top));
  const x = rawX - OFF, y = rawY - OFF;            // play-space px
  const col = Math.floor(x / CELL), row = Math.floor(y / CELL);
  const inPlay = row >= 0 && row < G.cells && col >= 0 && col < G.cells;
  return { x, y, row, col, inPlay };
}
function nodeAtCell(row, col) {
  return window.GS.areas[area()].nodes.find(n =>
    row >= n.row && row < n.row + n.size && col >= n.col && col < n.col + n.size) || null;
}

// ---- render coalescing --------------------------------------
let renderQueued = false;
function requestRender() {
  if (renderQueued) return;
  renderQueued = true;
  requestAnimationFrame(() => { renderQueued = false; render(); });
}
window.requestRender = requestRender;

// ---- top bar ------------------------------------------------
function renderTopBar() {
  const a = DD.AREAS[area()];
  $("#area-name").innerHTML = `${a.icon} ${a.name}`;
  $("#hand-count").textContent = `${E.handTotal()}/${DD.HAND_CAP}`;
  $("#build-btn").classList.toggle("on", window.GS.build.open);
}

// ---- world --------------------------------------------------
function spriteSize(size) { return size >= 2 ? 72 : 30; }

// Rebuilds only the passive grid contents (zones/buildings/nodes/ground/
// preview). None of these are hover/click targets, so this can run every
// tick. The interactive arrows + build menu are rendered separately and
// NOT rebuilt on ticks — otherwise they flicker and drop clicks.
function renderGrid() {
  const a = area();
  const grid = $("#grid");
  grid.dataset.area = a;
  grid.style.width = WORLD_PX + "px";
  grid.style.height = WORLD_PX + "px";
  grid.style.backgroundSize = `${CELL}px ${CELL}px`;
  grid.style.transform = `translate(${-cam.x}px, ${-cam.y}px)`;
  grid.innerHTML = "";

  // frame around the 24x24 play area (everything outside is inert margin)
  const frame = el("div", "play-frame");
  frame.style.left = OFF + "px"; frame.style.top = OFF + "px";
  frame.style.width = VIEW_PX + "px"; frame.style.height = VIEW_PX + "px";
  grid.appendChild(frame);

  // reserved wild-land zones (no building allowed there)
  for (const z of E.zoneRects(a)) {
    const zd = el("div", "zone");
    zd.style.left = z.c0 * CELL + OFF + "px";
    zd.style.top = z.r0 * CELL + OFF + "px";
    zd.style.width = (z.c1 - z.c0 + 1) * CELL + "px";
    zd.style.height = (z.r1 - z.r0 + 1) * CELL + "px";
    grid.appendChild(zd);
  }

  // buildings (ghosts + built)
  const B = G.building;
  for (const b of window.GS.areas[a].buildings) {
    const cfg = DD.BUILDINGS[b.type];
    const bd = el("div", "building" + (b.built ? " built" : " ghost"));
    bd.style.left = b.col * CELL + OFF + "px";
    bd.style.top = b.row * CELL + OFF + "px";
    bd.style.width = B.w * CELL + "px";
    bd.style.height = B.h * CELL + "px";
    let inner = `<span class="b-ico">${cfg.icon}</span><span class="b-name">${cfg.name}</span>`;
    if (!b.built) {
      const needs = E.buildingNeeds(b);
      const list = Object.entries(needs).map(([it, q]) => `${q} ${E.itemIcon(it)}`).join(" ");
      inner += `<span class="b-needs">${list || "…"}</span>`;
    }
    bd.innerHTML = inner;
    grid.appendChild(bd);
  }

  // resource nodes
  const now = Date.now();
  for (const node of window.GS.areas[a].nodes) {
    const cfg = DD.AREAS[a];
    const nd = el("div", "node ready");
    nd.style.left = node.col * CELL + OFF + "px";
    nd.style.top = node.row * CELL + OFF + "px";
    nd.style.width = node.size * CELL + "px";
    nd.style.height = node.size * CELL + "px";
    nd.style.zIndex = node.row + 5;
    const sprite = (DD.TIER_SPRITES[a] || [])[node.tier - 1] || cfg.icon;
    let inner = `<span class="sprite" style="font-size:${spriteSize(node.size)}px">${sprite}</span>`;
    if ((cfg.interaction === "chop" || cfg.interaction === "break") && node.hitsLeft > 0) {
      inner += `<span class="hits">${node.hitsLeft} ${cfg.actionIcon}</span>`;
    }
    if (cfg.interaction === "surface" && node.surfaceUntil) {
      nd.classList.add("surface");
      inner += `<span class="cd-timer catch">${Math.max(0, (node.surfaceUntil - now) / 1000).toFixed(1)}s</span>`;
    }
    if (node.autoFlash > now) nd.classList.add("auto");
    nd.innerHTML = inner;
    grid.appendChild(nd);
  }

  // ground items
  for (const g of window.GS.areas[a].ground) {
    const gd = el("div", "ground");
    gd.style.left = g.x + OFF + "px";
    gd.style.top = g.y + OFF + "px";
    gd.innerHTML = `<span class="g-ico">${E.itemIcon(g.item)}</span><span class="g-qty">${g.qty}</span>`;
    grid.appendChild(gd);
  }

  // placement preview
  if (window.GS.build.placing && cursor.over && cursor.inPlay) {
    const ok = E.canPlaceBuilding(a, cursor.row, cursor.col);
    const pv = el("div", "preview " + (ok ? "ok" : "bad"));
    pv.style.left = cursor.col * CELL + OFF + "px";
    pv.style.top = cursor.row * CELL + OFF + "px";
    pv.style.width = B.w * CELL + "px";
    pv.style.height = B.h * CELL + "px";
    grid.appendChild(pv);
  }
}

function renderArrows(areaKey) {
  const wrap = $("#arrows");
  wrap.innerHTML = "";
  for (const dir of ["up", "down", "left", "right"]) {
    const target = E.neighborOf(areaKey, dir);
    if (!target) continue;
    const btn = el("button", `edge-arrow ${dir}`);
    if (target === "void" || !DD.AREAS[target]) {
      btn.classList.add("disabled");
      btn.innerHTML = `<span class="arr">${DD.WORLD.dirGlyph[dir]}</span><span class="arr-label">🔒 ???</span>`;
    } else if (!E.isAreaUnlocked(target)) {
      const cost = E.areaUnlockCost(target);
      const label = Object.entries(cost).map(([it, q]) => `${q} ${E.itemIcon(it)}`).join(" ");
      btn.classList.add("locked");
      if (!E.canAfford(cost)) btn.classList.add("cant");
      btn.innerHTML = `<span class="arr">${DD.WORLD.dirGlyph[dir]}</span><span class="arr-label">🔒 ${DD.AREAS[target].name}<br>${label}</span>`;
      btn.onclick = () => { if (E.unlockArea(target)) { recenterCamera(); render(); } };
    } else {
      btn.innerHTML = `<span class="arr">${DD.WORLD.dirGlyph[dir]}</span><span class="arr-label">${DD.AREAS[target].icon} ${DD.AREAS[target].name}</span>`;
      btn.onclick = () => { if (E.moveTo(target)) { recenterCamera(); render(); } };
    }
    wrap.appendChild(btn);
  }
}

// ---- hand cursor overlay ------------------------------------
function renderHandCursor() {
  const hc = $("#hand-cursor");
  const hand = window.GS.hand;
  if (!hand.length) { hc.classList.add("hidden"); return; }
  hc.classList.remove("hidden");
  hc.style.left = cursor.cx + "px";
  hc.style.top = cursor.cy + "px";
  const top = hand[0];
  hc.innerHTML =
    `<span class="hc-ico">${E.itemIcon(top.item)}</span>` +
    `<span class="hc-qty">${top.qty}</span>` +
    (hand.length > 1 ? `<span class="hc-more">+${hand.length - 1}</span>` : "");
}

// ---- build menu ---------------------------------------------
function renderBuildMenu() {
  const bar = $("#build-menu");
  bar.classList.toggle("hidden", !window.GS.build.open);
  if (!window.GS.build.open) return;
  bar.innerHTML = "";
  for (const b of E.buildingCatalog()) {
    const cost = Object.entries(b.cost).map(([it, q]) => `${q} ${E.itemIcon(it)}`).join(" ");
    const card = el("button", "build-card" + (window.GS.build.placing === b.id ? " active" : ""));
    card.innerHTML = `<span class="bc-ico">${b.icon}</span><span class="bc-name">${b.name}</span><span class="bc-cost">${cost}</span>`;
    card.onclick = () => { window.GS.build.placing = b.id; window.GS.build.open = false; render(); };
    bar.appendChild(card);
  }
}

// ---- upgrades modal (paid from hand) ------------------------
function upgradeRow(areaKey, type, label, descFn) {
  const cost = E.upgradeCost(areaKey, type);
  const up = window.GS.areas[areaKey].upgrades;
  const row = el("div", "up-row");
  let status, maxed = false;
  if (type === "tier") { maxed = up.maxTier >= 5; status = `Tier ${up.maxTier}/5`; }
  if (type === "speed") { maxed = up.speed >= 3; status = `Lv ${up.speed}/3`; }
  if (type === "automation") { maxed = up.automation >= 3; status = `Lv ${up.automation}/3`; }
  const affordable = cost != null && E.canAfford(cost);
  const btnLabel = maxed ? "MAX" : Object.entries(cost).map(([it, q]) => `${q} ${E.itemIcon(it)}`).join(" ");
  row.innerHTML = `<div class="up-info"><b>${label}</b> <span class="up-status">${status}</span><div class="up-desc">${descFn(up)}</div></div>`;
  const btn = el("button", "up-buy" + (maxed ? " maxed" : affordable ? "" : " disabled"), btnLabel);
  if (!maxed) btn.onclick = () => { if (E.buyUpgrade(areaKey, type)) render(); };
  row.appendChild(btn);
  return row;
}
function renderUpgrades() {
  const body = $("#upgrades-body");
  body.innerHTML = "";
  for (const [areaKey, cfg] of Object.entries(DD.AREAS)) {
    const unlocked = E.isAreaUnlocked(areaKey);
    const sec = el("div", "up-area" + (unlocked ? "" : " dim"));
    sec.appendChild(el("h3", null, `${cfg.icon} ${cfg.name}${unlocked ? "" : " 🔒"} <span class="up-bank">✋ ${E.itemIcon(cfg.base)} ${E.handCount(cfg.base)}</span>`));
    if (!unlocked) { sec.appendChild(el("div", "up-desc", "Travel here and open it first.")); body.appendChild(sec); continue; }
    sec.appendChild(upgradeRow(areaKey, "tier", "Unlock Next Tier",
      up => up.maxTier >= 5 ? "All tiers unlocked." : `Enables ${DD.TIER_LABELS[up.maxTier]} ${cfg.tiers[up.maxTier].name}.`));
    sec.appendChild(upgradeRow(areaKey, "speed", cfg.speedLabel,
      up => `${cfg.timerLabel} timers −20% each (now ×${Math.pow(0.8, up.speed).toFixed(2)}).`));
    sec.appendChild(upgradeRow(areaKey, "automation", "Automation",
      up => up.automation === 0 ? "Auto-harvests nodes." : `Harvests ${DD.AUTOMATION_CLICKS[up.automation]} node(s)/tick.`));
    body.appendChild(sec);
  }
}
let upgradesOpen = false;
function toggleUpgrades(force) {
  upgradesOpen = force != null ? force : !upgradesOpen;
  $("#upgrades-modal").classList.toggle("hidden", !upgradesOpen);
  if (upgradesOpen) renderUpgrades();
}
function toggleBuild(force) {
  window.GS.build.open = force != null ? force : !window.GS.build.open;
  if (window.GS.build.open) window.GS.build.placing = null;
  render();
}

// ---- master render ------------------------------------------
// Full render — rebuilds interactive UI too (arrows, build menu). Use on
// discrete events, never on a repeating tick.
function render() {
  renderTopBar();
  renderGrid();
  renderArrows(area());
  renderBuildMenu();
  renderHandCursor();
  if (upgradesOpen) renderUpgrades();
}

// Tick / hold-loop render — updates only the fast-changing world grid, hand
// count and cursor overlay. Leaves arrows + build menu untouched so they
// don't flicker or lose clicks while the game ticks or you drag the mouse.
function renderPlay() {
  renderTopBar();
  renderGrid();
  renderHandCursor();
}
window.renderPlay = renderPlay;

// ---- mouse interaction --------------------------------------
function syncCursor(e) {
  cursor.cx = e.clientX; cursor.cy = e.clientY;
  const vp = $("#world-viewport").getBoundingClientRect();
  cursor.over = e.clientX >= vp.left && e.clientX <= vp.right && e.clientY >= vp.top && e.clientY <= vp.bottom;
  const p = pointFromEvent(e);
  cursor.x = p.x; cursor.y = p.y; cursor.row = p.row; cursor.col = p.col; cursor.inPlay = p.inPlay;
}
function onMouseMove(e) {
  cursor.cx = e.clientX; cursor.cy = e.clientY;
  if (panning) {                          // dragging the map
    cam.x = clamp(camStartX - (e.clientX - panStartX), 0, CAM_MAX);
    cam.y = clamp(camStartY - (e.clientY - panStartY), 0, CAM_MAX);
    applyCamera();
    renderHandCursor();
    return;
  }
  syncCursor(e);
  renderHandCursor();
  if (window.GS.build.placing && cursor.over) renderGrid(); // move the preview
}

function onMouseDown(e) {
  if (!cursor.over) return;
  const p = pointFromEvent(e);
  cursor.x = p.x; cursor.y = p.y; cursor.row = p.row; cursor.col = p.col; cursor.inPlay = p.inPlay;

  if (e.button === 2) { // right — drop / feed (or cancel placement)
    e.preventDefault();
    if (window.GS.build.placing) { window.GS.build.placing = null; render(); return; }
    if (!p.inPlay) return;               // the margin is inert
    rightHeld = true; holdStart = Date.now(); lastDrop = 0;
    E.dropFromHand(area(), p.x, p.y);   // immediate first drop
    startLoop(); renderPlay();
    return;
  }
  if (e.button !== 0) return;

  // placement mode: left-click places the ghost
  if (window.GS.build.placing) {
    if (p.inPlay && E.placeBuilding(area(), window.GS.build.placing, p.row, p.col)) {
      if (!e.shiftKey) window.GS.build.placing = null; // shift = place several
    }
    render();
    return;
  }

  // Inside the play area, a press on a node harvests, and a press on/near a
  // pile of dropped items starts the vacuum. Anywhere else (empty land or the
  // inert margin) a left-drag pans the map.
  if (p.inPlay) {
    const node = nodeAtCell(p.row, p.col);
    if (node) { E.harvestNode(area(), node.id, false); renderPlay(); return; }
    const itemsNear = window.GS.areas[area()].ground.some(g => Math.hypot(g.x - p.x, g.y - p.y) <= 40);
    if (itemsNear) {
      leftHeld = true; pickupMode = true;
      E.pickupNear(area(), p.x, p.y, 40);
      startLoop(); renderPlay();
      return;
    }
  }
  // grab-drag the map
  panning = true; panStartX = e.clientX; panStartY = e.clientY; camStartX = cam.x; camStartY = cam.y;
  $("#world-viewport").classList.add("grabbing");
}

// ---- WASD camera pan ----------------------------------------
let panRunning = false;
function panStep() {
  const s = 12;                          // px per frame
  let moved = false;
  if (keys.has("w")) { cam.y = Math.max(0, cam.y - s); moved = true; }
  if (keys.has("s")) { cam.y = Math.min(CAM_MAX, cam.y + s); moved = true; }
  if (keys.has("a")) { cam.x = Math.max(0, cam.x - s); moved = true; }
  if (keys.has("d")) { cam.x = Math.min(CAM_MAX, cam.x + s); moved = true; }
  if (moved) {
    applyCamera();
    // the world slid under the cursor, so recompute what it's over
    syncCursor({ clientX: cursor.cx, clientY: cursor.cy });
    if (window.GS.build.placing) renderGrid();
  }
  if (["w", "a", "s", "d"].some(k => keys.has(k))) requestAnimationFrame(panStep);
  else panRunning = false;
}
function onKeyDown(e) {
  const k = e.key.toLowerCase();
  if (["w", "a", "s", "d"].includes(k)) {
    keys.add(k); e.preventDefault();
    if (!panRunning) { panRunning = true; requestAnimationFrame(panStep); }
    return;
  }
  if (e.key === "Escape") { window.GS.build.placing = null; render(); }
}
function onKeyUp(e) { keys.delete(e.key.toLowerCase()); }

function onMouseUp(e) {
  if (e.button === 0) {
    leftHeld = false; pickupMode = false;
    if (panning) { panning = false; $("#world-viewport").classList.remove("grabbing"); }
  }
  if (e.button === 2) rightHeld = false;
}

// while a mouse button is held, keep vacuuming / drip-dropping
function startLoop() {
  if (loopRunning) return;
  loopRunning = true;
  const step = () => {
    let dirty = false;
    if (leftHeld && pickupMode && cursor.over) { if (E.pickupNear(area(), cursor.x, cursor.y, 40) > 0) dirty = true; }
    if (rightHeld && cursor.over) {
      const elapsed = Date.now() - holdStart;
      const interval = elapsed >= 1000 ? 200 : 1000 - elapsed * 0.8; // 1/s ramping to 5/s
      if (Date.now() - lastDrop >= interval) { E.dropFromHand(area(), cursor.x, cursor.y); lastDrop = Date.now(); dirty = true; }
    }
    if (dirty) renderPlay();
    if (leftHeld || rightHeld) requestAnimationFrame(step);
    else loopRunning = false;
  };
  requestAnimationFrame(step);
}

function wireInput() {
  const vp = $("#world-viewport");
  vp.addEventListener("mousedown", onMouseDown);
  vp.addEventListener("contextmenu", e => e.preventDefault());
  window.addEventListener("mousemove", onMouseMove);
  window.addEventListener("mouseup", onMouseUp);
  window.addEventListener("keydown", onKeyDown);
  window.addEventListener("keyup", onKeyUp);
}

window.UI = { render, renderPlay, recenterCamera, toggleUpgrades, toggleBuild, wireInput };
