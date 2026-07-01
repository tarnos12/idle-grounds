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
const GRID_PX = G.cell * G.cells;

const $ = sel => document.querySelector(sel);
function el(tag, cls, html) {
  const n = document.createElement(tag);
  if (cls) n.className = cls;
  if (html != null) n.innerHTML = html;
  return n;
}

// ---- input state --------------------------------------------
const cursor = { x: 0, y: 0, cx: 0, cy: 0, over: false }; // grid px + client px
let leftHeld = false, rightHeld = false, pickupMode = false;
let holdStart = 0, lastDrop = 0, loopRunning = false;

function area() { return window.GS.world.currentArea; }
function pointFromEvent(e) {
  const r = $("#grid").getBoundingClientRect();
  const x = Math.max(0, Math.min(GRID_PX - 1, e.clientX - r.left));
  const y = Math.max(0, Math.min(GRID_PX - 1, e.clientY - r.top));
  return { x, y, row: Math.floor(y / CELL), col: Math.floor(x / CELL) };
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

function renderWorld() {
  const a = area();
  const grid = $("#grid");
  grid.dataset.area = a;
  grid.style.width = GRID_PX + "px";
  grid.style.height = GRID_PX + "px";
  grid.style.backgroundSize = `${CELL}px ${CELL}px`;
  grid.innerHTML = "";

  // reserved wild-land zones (no building allowed there)
  for (const z of E.zoneRects(a)) {
    const zd = el("div", "zone");
    zd.style.left = z.c0 * CELL + "px";
    zd.style.top = z.r0 * CELL + "px";
    zd.style.width = (z.c1 - z.c0 + 1) * CELL + "px";
    zd.style.height = (z.r1 - z.r0 + 1) * CELL + "px";
    grid.appendChild(zd);
  }

  // buildings (ghosts + built)
  const B = G.building;
  for (const b of window.GS.areas[a].buildings) {
    const cfg = DD.BUILDINGS[b.type];
    const bd = el("div", "building" + (b.built ? " built" : " ghost"));
    bd.style.left = b.col * CELL + "px";
    bd.style.top = b.row * CELL + "px";
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
    nd.style.left = node.col * CELL + "px";
    nd.style.top = node.row * CELL + "px";
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
    gd.style.left = g.x + "px";
    gd.style.top = g.y + "px";
    gd.innerHTML = `<span class="g-ico">${E.itemIcon(g.item)}</span><span class="g-qty">${g.qty}</span>`;
    grid.appendChild(gd);
  }

  // placement preview
  if (window.GS.build.placing && cursor.over) {
    const ok = E.canPlaceBuilding(a, cursor.row, cursor.col);
    const pv = el("div", "preview " + (ok ? "ok" : "bad"));
    pv.style.left = cursor.col * CELL + "px";
    pv.style.top = cursor.row * CELL + "px";
    pv.style.width = B.w * CELL + "px";
    pv.style.height = B.h * CELL + "px";
    grid.appendChild(pv);
  }

  renderArrows(a);
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
      btn.onclick = () => { if (E.unlockArea(target)) render(); };
    } else {
      btn.innerHTML = `<span class="arr">${DD.WORLD.dirGlyph[dir]}</span><span class="arr-label">${DD.AREAS[target].icon} ${DD.AREAS[target].name}</span>`;
      btn.onclick = () => { if (E.moveTo(target)) render(); };
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
function render() {
  renderTopBar();
  renderWorld();
  renderBuildMenu();
  renderHandCursor();
  if (upgradesOpen) renderUpgrades();
}

// ---- mouse interaction --------------------------------------
function onMouseMove(e) {
  cursor.cx = e.clientX; cursor.cy = e.clientY;
  const vp = $("#world-viewport").getBoundingClientRect();
  cursor.over = e.clientX >= vp.left && e.clientX <= vp.right && e.clientY >= vp.top && e.clientY <= vp.bottom;
  if (cursor.over) { const p = pointFromEvent(e); cursor.x = p.x; cursor.y = p.y; cursor.row = p.row; cursor.col = p.col; }
  renderHandCursor();
  if (window.GS.build.placing && cursor.over) requestRender();
}

function onMouseDown(e) {
  if (!cursor.over) return;
  const p = pointFromEvent(e);
  cursor.x = p.x; cursor.y = p.y; cursor.row = p.row; cursor.col = p.col;

  if (e.button === 2) { // right — drop / feed (or cancel placement)
    e.preventDefault();
    if (window.GS.build.placing) { window.GS.build.placing = null; render(); return; }
    rightHeld = true; holdStart = Date.now(); lastDrop = 0;
    E.dropFromHand(area(), p.x, p.y);   // immediate first drop
    startLoop(); render();
    return;
  }
  if (e.button !== 0) return;

  // placement mode: left-click places the ghost
  if (window.GS.build.placing) {
    if (E.placeBuilding(area(), window.GS.build.placing, p.row, p.col)) {
      if (!e.shiftKey) window.GS.build.placing = null; // shift = place several
    }
    render();
    return;
  }

  // click a node -> one harvest swing; else start vacuum pickup
  const node = nodeAtCell(p.row, p.col);
  if (node) { E.harvestNode(area(), node.id, false); render(); return; }
  leftHeld = true; pickupMode = true;
  E.pickupNear(area(), p.x, p.y, 40);
  startLoop(); render();
}

function onMouseUp(e) {
  if (e.button === 0) { leftHeld = false; pickupMode = false; }
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
    if (dirty) render();
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
  window.addEventListener("keydown", e => { if (e.key === "Escape") { window.GS.build.placing = null; render(); } });
}

window.UI = { render, toggleUpgrades, toggleBuild, wireInput };
