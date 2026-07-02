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
const MARGIN = G.margin;              // inert border cells around the whole map
const OFF = MARGIN * G.cell;          // px offset of the region grid inside the world
const PLAY_W = G.cells * G.cell;      // one region's play size in px
const GAP_PX = G.gap * G.cell;        // void strip separating adjacent regions
// ONE continuous map: WORLD.cols x WORLD.rows regions + gaps + margin around.
const WORLD_W = (DD.WORLD.cols * G.cells + (DD.WORLD.cols - 1) * G.gap + 2 * MARGIN) * G.cell;
const WORLD_H = (DD.WORLD.rows * G.cells + (DD.WORLD.rows - 1) * G.gap + 2 * MARGIN) * G.cell;
// Fixed camera window (~35 tiles) — you pan the camera to explore the map.
const VIEW_PX = 35 * G.cell;

const $ = sel => document.querySelector(sel);
function el(tag, cls, html) {
  const n = document.createElement(tag);
  if (cls) n.className = cls;
  if (html != null) n.innerHTML = html;
  return n;
}

const PICKUP_R = G.cell;   // vacuum radius while holding left = 1 cell

// ---- input state --------------------------------------------
// cursor.region is the map region under the pointer (null = void/margin);
// lx/ly + lrow/lcol are REGION-LOCAL px / cells (what the engine expects).
const cursor = { cx: 0, cy: 0, over: false, region: null, lx: 0, ly: 0, lrow: -1, lcol: -1 };
let debugShow = false;   // show per-node swing/click counters (Debug button)
let leftHeld = false, rightHeld = false, pickupMode = false, harvestHeld = false;
let withdrawSH = null;   // storehouse being vacuumed with left-hold
let withdrawStart = 0, lastWithdraw = 0;
let holdStart = 0, lastDrop = 0, lastSwing = 0, loopRunning = false;
// Manual clicks are rate-limited; faster spam isn't counted as a real hit
// (we still play feedback later). Holding is exempt (it auto-swings).
const CLICK_COOLDOWN = 100;   // ms — max ~10 real manual clicks / second
let lastClickAt = 0;

// camera pan (px offset of the world within the viewport)
const cam = { x: 0, y: 0 };   // set to the centre region by recenterCamera()
const keys = new Set();
const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
let panning = false, panStartX = 0, panStartY = 0, camStartX = 0, camStartY = 0;
let viewScale = 1;   // logical->physical zoom (viewport fills the window)

// World-px origin of a region.
function regionPx(key) {
  const o = E.regionOrigin(key);
  return { x: o.col * CELL + OFF, y: o.row * CELL + OFF };
}
// The camera may only roam the bounding box of UNLOCKED regions, expanded by
// exactly the gap strip — so its edge reaches a locked neighbour's border but
// never shows a single pixel of the locked region itself.
function allowedBox() {
  let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
  for (const key of Object.keys(DD.WORLD.regions)) {
    if (!E.isAreaUnlocked(key)) continue;
    const p = regionPx(key);
    x0 = Math.min(x0, p.x); y0 = Math.min(y0, p.y);
    x1 = Math.max(x1, p.x + PLAY_W); y1 = Math.max(y1, p.y + PLAY_W);
  }
  return {
    x0: Math.max(0, x0 - GAP_PX), y0: Math.max(0, y0 - GAP_PX),
    x1: Math.min(WORLD_W, x1 + GAP_PX), y1: Math.min(WORLD_H, y1 + GAP_PX),
  };
}
function clampCam() {
  const b = allowedBox();
  cam.x = clamp(cam.x, b.x0, Math.max(b.x0, b.x1 - VIEW_PX));
  cam.y = clamp(cam.y, b.y0, Math.max(b.y0, b.y1 - VIEW_PX));
}
// Start looking at the TOP-CENTRE of the centre region.
function recenterCamera() {
  const p = regionPx("center");
  cam.x = p.x + PLAY_W / 2 - VIEW_PX / 2;
  cam.y = p.y;
  clampCam();
}
function applyCamera() {
  $("#grid").style.transform =
    `translate(${-cam.x * viewScale}px, ${-cam.y * viewScale}px) scale(${viewScale})`;
}

// The viewport fills the available window square; since it only shows ~80% of
// the play area, the map is zoomed in (cells rendered larger than 32px).
function fitViewport() {
  const top = ($("#topbar").getBoundingClientRect().height) || 56;
  const avail = Math.min(window.innerWidth - 36, window.innerHeight - top - 36);
  const rendered = Math.max(280, Math.min(avail, 860));
  viewScale = rendered / VIEW_PX;
  const vp = $("#world-viewport");
  vp.style.width = rendered + "px";
  vp.style.height = rendered + "px";
  applyCamera();
}

// Map a mouse event to { region, local px (lx,ly), local cells (lrow,lcol) }.
// region === null means the pointer is over void / the outer margin.
function pointFromEvent(e) {
  const r = $("#grid").getBoundingClientRect();       // reflects transform + scale
  const s = r.width / WORLD_W;                         // rendered px per logical px
  const wx = clamp((e.clientX - r.left) / s, 0, WORLD_W - 1);
  const wy = clamp((e.clientY - r.top) / s, 0, WORLD_H - 1);
  const gx = wx - OFF, gy = wy - OFF;                  // global play-space px
  const gRow = Math.floor(gy / CELL), gCol = Math.floor(gx / CELL);
  const region = (gx >= 0 && gy >= 0) ? E.regionAt(gRow, gCol) : null;
  let lx = 0, ly = 0, lrow = -1, lcol = -1;
  if (region) {
    const o = E.regionOrigin(region);
    lx = gx - o.col * CELL; ly = gy - o.row * CELL;
    lrow = gRow - o.row; lcol = gCol - o.col;
  }
  return { region, lx, ly, lrow, lcol };
}
function nodeAtCell(region, row, col) {
  return window.GS.areas[region].nodes.find(n =>
    row >= n.row && row < n.row + n.size && col >= n.col && col < n.col + n.size) || null;
}
// Region under the camera's centre — used for the location pill.
function regionAtCamCentre() {
  const gx = cam.x + VIEW_PX / 2 - OFF, gy = cam.y + VIEW_PX / 2 - OFF;
  return E.regionAt(Math.floor(gy / CELL), Math.floor(gx / CELL));
}

// ---- render coalescing --------------------------------------
let renderQueued = false;
function requestRender() {
  if (renderQueued) return;
  renderQueued = true;
  requestAnimationFrame(() => { renderQueued = false; render(); });
}
window.requestRender = requestRender;

// Coalesced grid-only repaint (max one per frame) — used while panning /
// moving a placement preview so mousemove bursts don't rebuild the DOM
// dozens of times per frame.
let gridPaintQueued = false;
function requestGridPaint() {
  if (gridPaintQueued) return;
  gridPaintQueued = true;
  requestAnimationFrame(() => { gridPaintQueued = false; renderGrid(); });
}

// Whether something animated is on camera (a surfaced fish's countdown or an
// AUTO badge) — only then does an "idle" tick still need to repaint.
function needsLiveRepaint() {
  const now = Date.now();
  const fr = 4 * CELL;
  const l = cam.x - fr, t = cam.y - fr, r = cam.x + VIEW_PX + fr, b = cam.y + VIEW_PX + fr;
  for (const key of Object.keys(DD.WORLD.regions)) {
    if (!E.isAreaUnlocked(key)) continue;
    const p = regionPx(key);
    if (p.x > r || p.x + PLAY_W < l || p.y > b || p.y + PLAY_W < t) continue;
    for (const n of window.GS.areas[key].nodes) {
      if (!(n.surfaceUntil > now || n.autoFlash > now)) continue;
      const nx = n.col * CELL + p.x, ny = n.row * CELL + p.y;
      if (nx < r && nx + n.size * CELL > l && ny < b && ny + n.size * CELL > t) return true;
    }
  }
  return false;
}

// ---- top bar ------------------------------------------------
function renderTopBar() {
  const key = regionAtCamCentre();
  const a = key ? DD.AREAS[key] : null;
  $("#area-name").innerHTML = a ? `${a.icon} ${a.name}${E.isAreaUnlocked(key) ? "" : " 🔒"}` : "🌫️ Wilds";
  $("#hand-count").textContent = `${E.handTotal()}/${E.handCap()}`;
  $("#build-btn").classList.toggle("on", window.GS.build.open);
  $("#debug-btn").classList.toggle("on", debugShow);
}

// ---- world --------------------------------------------------
function spriteSize(size) { return size >= 4 ? size * 30 : size >= 3 ? 90 : size >= 2 ? 72 : 30; }

// Rebuilds only the passive grid contents (zones/buildings/nodes/ground/
// preview). None of these are hover/click targets, so this can run every
// tick. The interactive arrows + build menu are rendered separately and
// NOT rebuilt on ticks — otherwise they flicker and drop clicks.
// Render one region's contents at its world-px offset (ox, oy).
// Only elements intersecting the camera rect (view) are appended — the world
// is huge now, and painting everything each tick is what stalled machines.
function renderRegion(grid, key, ox, oy, now, view) {
  const cfg = DD.AREAS[key];
  const areaState = window.GS.areas[key];
  const unlocked = E.isAreaUnlocked(key);
  const seen = (x, y, w, h) => x < view.r && x + w > view.l && y < view.b && y + h > view.t;

  // ground tint + frame for the region
  const bg = el("div", "region-bg");
  bg.dataset.area = key;
  bg.style.left = ox + "px"; bg.style.top = oy + "px";
  bg.style.width = PLAY_W + "px"; bg.style.height = PLAY_W + "px";
  grid.appendChild(bg);
  const frame = el("div", "play-frame");
  frame.style.left = ox + "px"; frame.style.top = oy + "px";
  frame.style.width = PLAY_W + "px"; frame.style.height = PLAY_W + "px";
  grid.appendChild(frame);

  // reserved wild-land zones (no building allowed there)
  const zoneBox = (z, cls) => {
    const zx = z.c0 * CELL + ox, zy = z.r0 * CELL + oy;
    const zw = (z.c1 - z.c0 + 1) * CELL, zh = (z.r1 - z.r0 + 1) * CELL;
    if (!seen(zx, zy, zw, zh)) return;
    const zd = el("div", cls);
    zd.style.left = zx + "px"; zd.style.top = zy + "px";
    zd.style.width = zw + "px"; zd.style.height = zh + "px";
    grid.appendChild(zd);
  };
  for (const z of E.noBuildRects(key)) zoneBox(z, "zone");
  for (const gen of cfg.generators || [])
    for (const z of E.zoneRects(gen.zone)) zoneBox(z, "zone gen-" + gen.item);

  // buildings (ghosts + built)
  const B = G.building;
  for (const b of areaState.buildings) {
    if (!seen(b.col * CELL + ox, b.row * CELL + oy, B.w * CELL, B.h * CELL)) continue;
    const bCfg = DD.BUILDINGS[b.type];
    const bd = el("div", "building" + (b.built ? " built" : " ghost"));
    bd.style.left = b.col * CELL + ox + "px";
    bd.style.top = b.row * CELL + oy + "px";
    bd.style.width = B.w * CELL + "px";
    bd.style.height = B.h * CELL + "px";
    let inner;
    if (b.built && b.type === "storehouse") {
      inner = `<span class="b-ico">${b.item ? E.itemIcon(b.item) : bCfg.icon}</span>` +
        `<span class="b-name">${b.item ? E.itemName(b.item) + " ×" + b.qty : "empty"}</span>`;
    } else {
      inner = `<span class="b-ico">${bCfg.icon}</span><span class="b-name">${bCfg.name}</span>`;
      if (!b.built) {
        const needs = E.buildingNeeds(b);
        const list = Object.entries(needs).map(([it, q]) => `${q} ${E.itemIcon(it)}`).join(" ");
        inner += `<span class="b-needs">${list || "…"}</span>`;
      }
    }
    bd.innerHTML = inner;
    grid.appendChild(bd);
  }

  // resource nodes (trees, bushes, crops, ore, fish, quarry)
  for (const node of areaState.nodes) {
    if (!seen(node.col * CELL + ox, node.row * CELL + oy - 3 * CELL, // sprites overflow upward
              node.size * CELL, (node.size + 3) * CELL)) continue;
    const nd = el("div", "node ready" + (node.kind ? " k-" + node.kind : ""));
    // ONE pulse per click: a rebuilt element resumes the squash mid-flight
    // (negative delay = elapsed time), so unrelated re-renders don't replay
    // it — only a new click (fresh hitAt) restarts from zero.
    if (node.hitAt && now - node.hitAt < 180) {
      nd.classList.add("hit");
      nd.style.setProperty("--hit-delay", `${-(now - node.hitAt)}ms`);
    }
    nd.style.left = node.col * CELL + ox + "px";
    nd.style.top = node.row * CELL + oy + "px";
    nd.style.width = node.size * CELL + "px";
    nd.style.height = node.size * CELL + "px";
    nd.style.zIndex = node.row + 5;
    const sprite = node.sprite || (DD.TIER_SPRITES[key] || [])[node.tier - 1] || cfg.icon;
    let inner = `<span class="sprite" style="font-size:${spriteSize(node.size)}px">${sprite}</span>`;
    if (debugShow && node.interaction === "quarry") {
      inner += `<span class="hits">${node.clicks || 0}/${E.quarryClicksPerDrop(key, node)} ${cfg.actionIcon}</span>`;
    } else if (debugShow && (node.interaction === "chop" || node.interaction === "break") && node.hitsLeft > 0) {
      inner += `<span class="hits">${node.hitsLeft} ${cfg.actionIcon}</span>`;
    }
    if (unlocked && node.interaction === "surface" && node.surfaceUntil) {
      nd.classList.add("surface");
      inner += `<span class="cd-timer catch">${Math.max(0, (node.surfaceUntil - now) / 1000).toFixed(1)}s</span>`;
    }
    if (node.autoFlash > now) nd.classList.add("auto");
    nd.innerHTML = inner;
    grid.appendChild(nd);
  }

  // ground items — one icon per item (they never stack)
  for (const g of areaState.ground) {
    if (!seen(g.x + ox - 16, g.y + oy - 16, 32, 32)) continue;
    const gd = el("div", "ground");
    gd.style.left = g.x + ox + "px";
    gd.style.top = g.y + oy + "px";
    gd.innerHTML = `<span class="g-ico">${E.itemIcon(g.item)}</span>`;
    grid.appendChild(gd);
  }

  // locked regions are visible but dimmed behind a lock veil
  if (!unlocked) {
    const veil = el("div", "region-lock", "🔒");
    veil.style.left = ox + "px"; veil.style.top = oy + "px";
    veil.style.width = PLAY_W + "px"; veil.style.height = PLAY_W + "px";
    grid.appendChild(veil);
  }
}

function renderGrid() {
  const grid = $("#grid");
  grid.style.width = WORLD_W + "px";
  grid.style.height = WORLD_H + "px";
  grid.style.backgroundSize = `${CELL}px ${CELL}px`;
  applyCamera();
  grid.innerHTML = "";

  const now = Date.now();
  // Cull to the camera view (+4-cell fringe): first whole regions, then every
  // element inside renderRegion. Panning repaints per frame, so edges fill in.
  const fr = 4 * CELL;
  const view = { l: cam.x - fr, t: cam.y - fr, r: cam.x + VIEW_PX + fr, b: cam.y + VIEW_PX + fr };
  for (const key of Object.keys(DD.WORLD.regions)) {
    const p = regionPx(key);
    if (p.x > view.r || p.x + PLAY_W < view.l || p.y > view.b || p.y + PLAY_W < view.t) continue;
    renderRegion(grid, key, p.x, p.y, now, view);
  }

  // placement preview (only in an unlocked region)
  if (window.GS.build.placing && cursor.over && cursor.region) {
    const B = G.building;
    const ok = E.isAreaUnlocked(cursor.region) &&
      E.canPlaceBuilding(cursor.region, cursor.lrow, cursor.lcol);
    const p = regionPx(cursor.region);
    const pv = el("div", "preview " + (ok ? "ok" : "bad"));
    pv.style.left = cursor.lcol * CELL + p.x + "px";
    pv.style.top = cursor.lrow * CELL + p.y + "px";
    pv.style.width = B.w * CELL + "px";
    pv.style.height = B.h * CELL + "px";
    grid.appendChild(pv);
  }
}

// Edge buttons for LOCKED neighbour regions: pay to open them (the map is
// continuous — once open you simply pan across; no travel arrows).
function renderUnlockButtons() {
  const wrap = $("#arrows");
  wrap.innerHTML = "";
  for (const [key, side] of Object.entries(DD.WORLD.unlockSide)) {
    if (E.isAreaUnlocked(key)) continue;
    const cost = E.areaUnlockCost(key);
    const label = Object.entries(cost).map(([it, q]) => `${q} ${E.itemIcon(it)}`).join(" ");
    const btn = el("button", `edge-arrow ${side} locked` + (E.canAfford(cost) ? "" : " cant"));
    btn.innerHTML = `<span class="arr">🔓</span>` +
      `<span class="arr-label">Unlock ${DD.AREAS[key].name}<br>${label}</span>`;
    btn.onclick = () => { if (E.unlockArea(key)) { clampCam(); render(); } };
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
  if (type === "harvestSpeed") { maxed = up.harvestSpeed >= 3; status = `Lv ${up.harvestSpeed}/3`; }
  if (type === "automation") { maxed = up.automation >= 3; status = `Lv ${up.automation}/3`; }
  if (type === "quarry") { maxed = (up.quarry || 0) >= 3; status = `Lv ${up.quarry || 0}/3`; }
  // Upgrades are funded incrementally from the hand: show remaining cost and
  // any progress already paid. The button is active if the hand holds any of
  // the resource (it contributes as much as it can each click).
  let btnLabel = "MAX", canPay = false, progress = "";
  if (!maxed) {
    const [item, qty] = Object.entries(cost)[0];
    const paid = E.upgradePaid(areaKey, type);
    btnLabel = `${qty - paid} ${E.itemIcon(item)}`;
    canPay = E.handCount(item) > 0;
    if (paid > 0) progress = ` <span class="up-prog">paid ${paid}/${qty}</span>`;
  }
  row.innerHTML = `<div class="up-info"><b>${label}</b> <span class="up-status">${status}${progress}</span><div class="up-desc">${descFn(up)}</div></div>`;
  const btn = el("button", "up-buy" + (maxed ? " maxed" : canPay ? "" : " disabled"), btnLabel);
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
    if (!unlocked) { sec.appendChild(el("div", "up-desc", "Unlock this region at its border first.")); body.appendChild(sec); continue; }
    sec.appendChild(upgradeRow(areaKey, "tier", "Unlock Next Tier",
      up => up.maxTier >= 5 ? "All tiers unlocked." : `Enables ${DD.TIER_LABELS[up.maxTier]} ${cfg.tiers[up.maxTier].name}.`));
    sec.appendChild(upgradeRow(areaKey, "speed", cfg.speedLabel,
      up => `${cfg.timerLabel} timers −20% each (now ×${Math.pow(0.8, up.speed).toFixed(2)}).`));
    sec.appendChild(upgradeRow(areaKey, "harvestSpeed", "Action Speed",
      up => `Chop/mine/hold swings −20% each (now ×${Math.pow(0.8, up.harvestSpeed).toFixed(2)}).`));
    if ((cfg.fixtures || []).some(f => f.kind === "quarry")) {
      const base = cfg.fixtures.find(f => f.kind === "quarry").clicksPerDrop;
      sec.appendChild(upgradeRow(areaKey, "quarry", "Quarry Yield",
        up => `1 stone every ${Math.max(1, base - (up.quarry || 0))} clicks.`));
    }
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

function toggleDebug(force) {
  debugShow = force != null ? force : !debugShow;
  render();
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
  renderUnlockButtons();
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
  cursor.region = p.region; cursor.lx = p.lx; cursor.ly = p.ly; cursor.lrow = p.lrow; cursor.lcol = p.lcol;
}
function onMouseMove(e) {
  cursor.cx = e.clientX; cursor.cy = e.clientY;
  if (panning) {                          // dragging the map
    cam.x = camStartX - (e.clientX - panStartX) / viewScale;
    cam.y = camStartY - (e.clientY - panStartY) / viewScale;
    clampCam();
    applyCamera();
    requestGridPaint();                   // culled content must fill in as we pan
    renderHandCursor();
    return;
  }
  syncCursor(e);
  renderHandCursor();
  if (window.GS.build.placing && cursor.over) requestGridPaint(); // move the preview
}

function onMouseDown(e) {
  if (!cursor.over) return;
  const p = pointFromEvent(e);
  cursor.region = p.region; cursor.lx = p.lx; cursor.ly = p.ly; cursor.lrow = p.lrow; cursor.lcol = p.lcol;
  // interactions only work in an UNLOCKED region; anywhere else is inert land
  const active = p.region && E.isAreaUnlocked(p.region);

  if (e.button === 2) { // right — drop / feed (or cancel placement)
    e.preventDefault();
    if (window.GS.build.placing) { window.GS.build.placing = null; render(); return; }
    if (!active) return;
    rightHeld = true; holdStart = Date.now();
    E.dropFromHand(p.region, p.lx, p.ly);  // ground drop, ghost feed, or storehouse deposit
    lastDrop = holdStart;                  // next drop waits a full interval
    startLoop(); renderPlay();
    return;
  }
  if (e.button !== 0) return;

  // placement mode: left-click places the ghost
  if (window.GS.build.placing) {
    if (active && E.placeBuilding(p.region, window.GS.build.placing, p.lrow, p.lcol)) {
      if (!e.shiftKey) window.GS.build.placing = null; // shift = place several
    }
    render();
    return;
  }

  // Inside an unlocked region, a press on a node harvests, and a press on/near
  // a pile of dropped items starts the vacuum. Anywhere else (empty land,
  // locked regions, void) a left-drag pans the map.
  if (active) {
    // left-click/hold a storehouse to vacuum its contents into the hand
    const sh = E.buildingAt(p.region, p.lrow, p.lcol);
    if (sh && sh.built && sh.type === "storehouse") {
      leftHeld = true; withdrawSH = sh;
      E.takeFromStorehouse(sh, 1);      // a click takes one; holding accelerates
      withdrawStart = Date.now(); lastWithdraw = withdrawStart;
      startLoop(); renderPlay();
      return;
    }
    const node = nodeAtCell(p.region, p.lrow, p.lcol);
    if (node) {
      // a manual click swings once (rate-limited); holding then auto-swings
      const t = Date.now();
      if (t - lastClickAt >= CLICK_COOLDOWN) { E.harvestNode(p.region, node.id, false); lastClickAt = t; }
      else node.hitAt = t;   // too fast to count as damage — still show the hit
      leftHeld = true; harvestHeld = true; lastSwing = t;
      startLoop(); renderPlay();
      return;
    }
    const itemsNear = window.GS.areas[p.region].ground.some(g => Math.hypot(g.x - p.lx, g.y - p.ly) <= PICKUP_R);
    if (itemsNear) {
      leftHeld = true; pickupMode = true;
      E.pickupNear(p.region, p.lx, p.ly, PICKUP_R);
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
  if (keys.has("w")) { cam.y -= s; moved = true; }
  if (keys.has("s")) { cam.y += s; moved = true; }
  if (keys.has("a")) { cam.x -= s; moved = true; }
  if (keys.has("d")) { cam.x += s; moved = true; }
  if (moved) {
    clampCam();
    applyCamera();
    requestGridPaint();                   // culled content must fill in as we pan
    // the world slid under the cursor, so recompute what it's over
    syncCursor({ clientX: cursor.cx, clientY: cursor.cy });
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
  if (k === "b") { toggleBuild(); return; }   // B toggles the build menu
  if (e.key === "Escape") { window.GS.build.placing = null; render(); }
}
function onKeyUp(e) { keys.delete(e.key.toLowerCase()); }

function onMouseUp(e) {
  if (e.button === 0) {
    leftHeld = false; pickupMode = false; harvestHeld = false; withdrawSH = null;
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
    // everything below acts in the region under the cursor (if unlocked)
    const rg = cursor.region && E.isAreaUnlocked(cursor.region) ? cursor.region : null;
    if (leftHeld && pickupMode && cursor.over && rg) { if (E.pickupNear(rg, cursor.lx, cursor.ly, PICKUP_R) > 0) dirty = true; }
    if (leftHeld && withdrawSH) {
      // withdraw rate tweens 1/s -> 5/s over the first 3 seconds of the hold
      const elapsed = Date.now() - withdrawStart;
      const rate = 1 + Math.min(elapsed / 3000, 1) * 4;   // 1 .. 5 items per second
      if (Date.now() - lastWithdraw >= 1000 / rate) {
        if (E.takeFromStorehouse(withdrawSH, 1) > 0) dirty = true;
        lastWithdraw = Date.now();
      }
    }
    // hold-left over a node auto-swings at that node's own harvest rate
    if (harvestHeld && cursor.over && rg) {
      const n = nodeAtCell(rg, cursor.lrow, cursor.lcol);
      if (n && Date.now() - lastSwing >= E.harvestInterval(rg, n)) {
        E.harvestNode(rg, n.id, true); lastSwing = Date.now(); dirty = true;
      }
    }
    if (rightHeld && cursor.over && rg) {
      // deliberate for the first second, then accelerate hard so you can dump fast
      const elapsed = Date.now() - holdStart;
      const rate = elapsed < 1000
        ? 4                                             // 4/s for the first second
        : 4 + Math.min((elapsed - 1000) / 1000, 1) * 16; // ramp 4 -> 20/s over the next second
      const interval = 1000 / rate;
      if (Date.now() - lastDrop >= interval) { E.dropFromHand(rg, cursor.lx, cursor.ly); lastDrop = Date.now(); dirty = true; }
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
  window.addEventListener("resize", fitViewport);
  recenterCamera();                     // start at the top-centre of the play area
  fitViewport();
  requestAnimationFrame(fitViewport);   // re-fit once layout has settled
}

window.UI = { render, renderPlay, needsLiveRepaint, recenterCamera, toggleUpgrades, toggleBuild, toggleDebug, wireInput };
