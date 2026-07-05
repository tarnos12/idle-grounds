/* ============================================================
   Idle Grounds — canvas rendering + mouse interaction
   The world and the upgrade tree are drawn on <canvas> (no DOM
   rebuilds — hundreds of ground items are just fillText calls).
   All interaction is hit-tested from cursor -> game state, so
   rendering and input stay fully decoupled.
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
// Camera window size is zoom-driven: zoom 1 = closest (~17.5 tiles),
// 2 = default (~35 tiles), 3 = furthest (~52.5 tiles).
const ZOOM_UNIT_PX = 17.5 * G.cell;   // logical view WIDTH px per zoom unit (560)
const VIEW_ASPECT = 9 / 16;           // the play window is 16:9 (width is the zoom anchor)
let zoom = 2, zoomTarget = 2;
let VIEW_W = ZOOM_UNIT_PX * zoom;
let VIEW_H = VIEW_W * VIEW_ASPECT;

const $ = sel => document.querySelector(sel);
function el(tag, cls, html) {
  const n = document.createElement(tag);
  if (cls) n.className = cls;
  if (html != null) n.innerHTML = html;
  return n;
}

const PICKUP_R = G.cell * 2;   // gravity-field radius while holding left = 2 cells

// palette (kept in sync with style.css)
const C = {
  void: "#161b16", gridLine: "rgba(255,255,255,.05)",
  text: "#e6edf3", muted: "#94a3b8", accent: "#4ade80", accentDk: "#22a35a",
  gold: "#fbbf24", danger: "#f87171", line: "#3c4651", panel: "#2b333c", panel2: "#37414d",
  region: { center: "#25351f", farm: "#3a3318", mine: "#2c2c33", fishing: "#16323b" },
  zone: "rgba(74,222,128,.05)", zoneEdge: "rgba(74,222,128,.18)",
  frame: "rgba(74,222,128,.30)",
  built: "rgba(60,70,80,.95)", ghost: "rgba(74,222,128,.10)",
  altar: "rgba(74,60,30,.95)",
  dragon: "rgba(52,36,70,.95)", dragonEdge: "#a855f7",
  enemyZone: "rgba(248,113,113,.07)", enemyZoneEdge: "rgba(248,113,113,.30)",
  veil: "rgba(0,0,0,.55)",
};
// generator-field ground tints, per produced item: [fill, edge]
const FIELD_TINT = {
  clay:  ["rgba(184,115,66,.30)",  "rgba(220,190,120,.4)"],
  sand:  ["rgba(226,201,126,.28)", "rgba(220,190,120,.4)"],
  stone: ["rgba(148,163,184,.22)", "rgba(148,163,184,.4)"],
  water: ["rgba(96,165,250,.28)",  "rgba(96,165,250,.45)"],
};
// ---- item icon art (assets/icons/<key>.png, 16px pixel art) ---------
// Loaded lazily; anything missing keeps its emoji, so new items work before
// art exists. Pixel art is drawn with image smoothing OFF (crisp scaling).
const ICON_IMGS = {};
for (const key of Object.keys(DD.ITEM_ICONS)) {
  const img = new Image();
  img.onload = () => { ICON_IMGS[key] = img; if (window.requestRender) window.requestRender(); };
  img.src = `assets/icons/${key}.png`;
}
// Draw an item icon centred at (cx, cy), px square, on the world canvas.
function drawItemIcon(key, cx, cy, px) {
  const img = ICON_IMGS[key];
  if (img) { ctx.drawImage(img, Math.round(cx - px / 2), Math.round(cy - px / 2), px, px); return; }
  ctx.save();
  ctx.fillStyle = C.text;
  ctx.font = `${Math.round(px * 0.9)}px ${EMOJI_FONT}`;
  ctx.textAlign = "center"; ctx.textBaseline = "middle";
  ctx.fillText(E.itemIcon(key), cx, cy);
  ctx.restore();
}
// Draw a "qty ICON  qty ICON…" needs list centred at (cx, cy); the caller
// sets fillStyle. Optional prefix text ("Feed:") leads the line.
function drawNeedsLine(entries, cx, cy, px, prefix) {
  ctx.font = `800 ${px}px ${TEXT_FONT}`;
  ctx.textBaseline = "middle";
  if (!entries.length) { ctx.textAlign = "center"; ctx.fillText((prefix ? prefix + " " : "") + "…", cx, cy); return; }
  const gap = px * 0.3, iconPx = Math.round(px * 1.35);
  const pre = prefix ? prefix + " " : "";
  let total = pre ? ctx.measureText(pre).width : 0;
  for (const [, q] of entries) total += ctx.measureText(String(q)).width + gap * 0.5 + iconPx + gap;
  total -= gap;
  let x = cx - total / 2;
  ctx.textAlign = "left";
  if (pre) { ctx.fillText(pre, x, cy); x += ctx.measureText(pre).width; }
  for (const [it, q] of entries) {
    ctx.fillText(String(q), x, cy);
    x += ctx.measureText(String(q)).width + gap * 0.5;
    drawItemIcon(it, x + iconPx / 2, cy, iconPx);
    x += iconPx + gap;
  }
  ctx.textAlign = "center";
}
// DOM contexts (hand cursor, build menu, unlock buttons): <img> once the
// icon exists, emoji otherwise.
function iconHTML(key) {
  return ICON_IMGS[key]
    ? `<img class="item-ico" src="assets/icons/${key}.png" alt="${E.itemName(key)}">`
    : E.itemIcon(key);
}

// "Twemoji Mozilla" FIRST: Firefox mishandles Windows 11's Segoe UI Emoji
// (COLR v1) in canvas — glyphs come out as dim fillStyle-tinted silhouettes,
// which reads as a translucent grey film over every icon. Firefox always
// ships Twemoji Mozilla and renders it in canvas correctly; Chrome doesn't
// know the name and falls through to Segoe UI Emoji as before.
const EMOJI_FONT = '"Twemoji Mozilla", "Segoe UI Emoji", "Noto Color Emoji", sans-serif';
const TEXT_FONT = '"Segoe UI", system-ui, sans-serif';

// ---- input state --------------------------------------------
const cursor = { cx: 0, cy: 0, over: false, region: null, lx: 0, ly: 0, lrow: -1, lcol: -1 };
let debugShow = false;    // show per-node swing/click counters (Debug button)
let demolishMode = false; // next building clicked gets destroyed (refunds drop)
let leftHeld = false, rightHeld = false, pickupMode = false, harvestHeld = false, attackHeld = false;
let withdrawSH = null;    // storehouse being vacuumed with left-hold
let withdrawStart = 0, lastWithdraw = 0;
let holdStart = 0, lastDrop = 0, lastSwing = 0, loopRunning = false;
const CLICK_COOLDOWN = 100;   // ms — max ~10 real manual clicks / second
let lastClickAt = 0;

// camera pan (px offset of the world within the viewport)
const cam = { x: 0, y: 0 };
const keys = new Set();
const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
let sprint = false;  // Shift toggles 2x WASD pan speed
let viewScale = 1;   // logical world px -> CSS px

// ---- canvases ------------------------------------------------
let cvs = null, ctx = null;     // game world
let tcvs = null, tctx = null;   // upgrade tree
let dpr = 1;

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
  cam.x = clamp(cam.x, b.x0, Math.max(b.x0, b.x1 - VIEW_W));
  cam.y = clamp(cam.y, b.y0, Math.max(b.y0, b.y1 - VIEW_H));
}
// Start looking at the TOP-CENTRE of the centre region.
function recenterCamera() {
  const p = regionPx("center");
  cam.x = p.x + PLAY_W / 2 - VIEW_W / 2;
  cam.y = p.y;
  clampCam();
}
// Camera moved: repaint (canvas redraws; there is no DOM transform anymore).
function applyCamera() { requestGridPaint(); }

// The viewport fills the available window; viewScale maps the logical camera
// window (zoom-dependent) onto it.
let renderedW = 1200;
function fitViewport() {
  const top = ($("#topbar").getBoundingClientRect().height) || 56;
  const availW = window.innerWidth - 36;
  const availH = (window.innerHeight - top - 36) / VIEW_ASPECT;
  renderedW = Math.max(320, Math.min(availW, availH, 1440));
  viewScale = renderedW / VIEW_W;
  const vp = $("#world-viewport");
  vp.style.width = renderedW + "px";
  vp.style.height = (renderedW * VIEW_ASPECT) + "px";
  sizeCanvas();
  requestGridPaint();
  if (upgradesOpen) { sizeTreeCanvas(); drawTree(); }
}
function sizeCanvas() {
  if (!cvs) return;
  dpr = window.devicePixelRatio || 1;
  // Backing store must map to WHOLE device pixels, and the CSS size must be
  // exactly backing/dpr — a half-device-pixel disagreement (dpr 1.25 etc.)
  // makes the browser resample the whole canvas (soft/blurry output).
  cvs.width = Math.round(renderedW * dpr);
  cvs.height = Math.round(renderedW * VIEW_ASPECT * dpr);
  cvs.style.width = (cvs.width / dpr) + "px";
  cvs.style.height = (cvs.height / dpr) + "px";
}

// Apply a zoom level immediately (keeps the view centre anchored).
function setZoom(z) {
  z = clamp(z, 1, 3);
  const oldW = VIEW_W, oldH = VIEW_H;
  zoom = z; zoomTarget = z;
  VIEW_W = ZOOM_UNIT_PX * zoom;
  VIEW_H = VIEW_W * VIEW_ASPECT;
  cam.x += (oldW - VIEW_W) / 2;
  cam.y += (oldH - VIEW_H) / 2;
  viewScale = renderedW / VIEW_W;
  clampCam(); requestGridPaint();
}
// Smooth wheel zoom: ease toward the target a bit each frame.
let zoomAnimRunning = false;
function zoomStep() {
  const d = zoomTarget - zoom;
  const next = Math.abs(d) < 0.005 ? zoomTarget : zoom + d * 0.2;
  const oldW = VIEW_W, oldH = VIEW_H;
  zoom = next;
  VIEW_W = ZOOM_UNIT_PX * zoom;
  VIEW_H = VIEW_W * VIEW_ASPECT;
  cam.x += (oldW - VIEW_W) / 2;
  cam.y += (oldH - VIEW_H) / 2;
  viewScale = renderedW / VIEW_W;
  clampCam(); requestGridPaint();
  if (zoom !== zoomTarget) requestAnimationFrame(zoomStep);
  else zoomAnimRunning = false;
}
function onWheel(e) {
  e.preventDefault();
  zoomTarget = clamp(zoomTarget + (e.deltaY > 0 ? 0.25 : -0.25), 1, 3);
  if (!zoomAnimRunning) { zoomAnimRunning = true; requestAnimationFrame(zoomStep); }
}

// Map a mouse event to { region, local px (lx,ly), local cells (lrow,lcol) }.
function pointFromEvent(e) {
  const r = cvs.getBoundingClientRect();
  const wx = clamp(cam.x + (e.clientX - r.left) / viewScale, 0, WORLD_W - 1);
  const wy = clamp(cam.y + (e.clientY - r.top) / viewScale, 0, WORLD_H - 1);
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
  const gx = cam.x + VIEW_W / 2 - OFF, gy = cam.y + VIEW_H / 2 - OFF;
  return E.regionAt(Math.floor(gy / CELL), Math.floor(gx / CELL));
}

// ---- paint scheduling ----------------------------------------
let renderQueued = false;
function requestRender() {
  if (renderQueued) return;
  renderQueued = true;
  requestAnimationFrame(() => { renderQueued = false; render(); });
}
window.requestRender = requestRender;

// Coalesced canvas repaint (max one per frame). While something on screen is
// animating (hit squash, fish bob, AUTO badge) it self-chains frames.
let drawQueued = false;
function requestGridPaint() {
  if (drawQueued) return;
  drawQueued = true;
  requestAnimationFrame(() => {
    drawQueued = false;
    drawWorld();
    if (animActive()) requestGridPaint();
  });
}

// Anything time-animated visible right now?
function animActive() {
  const now = Date.now();
  const fr = 2 * CELL;
  const l = cam.x - fr, t = cam.y - fr, r = cam.x + VIEW_W + fr, b = cam.y + VIEW_H + fr;
  for (const key of Object.keys(DD.WORLD.regions)) {
    const p = regionPx(key);
    if (p.x > r || p.x + PLAY_W < l || p.y > b || p.y + PLAY_W < t) continue;
    const unlocked = E.isAreaUnlocked(key);
    for (const n of window.GS.areas[key].nodes) {
      const nx = n.col * CELL + p.x, ny = n.row * CELL + p.y;
      if (nx > r || nx + n.size * CELL < l || ny > b || ny + n.size * CELL < t) continue;
      if (n.hitAt && now - n.hitAt < 200) return true;
      if (unlocked && n.surfaceUntil > now) return true;   // bob + countdown
      if (n.autoFlash > now) return true;
    }
    // wandering enemies animate whenever one is on screen
    if (unlocked) for (const en of window.GS.areas[key].enemies || []) {
      const ex = en.x + p.x, ey = en.y + p.y;
      if (ex > l && ex < r && ey > t && ey < b) return true;
    }
    // wisps in flight animate whenever one is on screen
    if (unlocked) for (const w of window.GS.areas[key].wisps || []) {
      const wx = w.x + p.x, wy = w.y + p.y;
      if (wx > l && wx < r && wy > t && wy < b) return true;
    }
    // a smelting converter's progress bar animates while it's on screen
    if (unlocked) for (const bd of window.GS.areas[key].buildings) {
      if (!bd.built || !(DD.BUILDINGS[bd.type].smelt) || !(bd.smeltDoneAt > now)) continue;
      const bs = E.buildingSize(bd.type);
      const bx = bd.col * CELL + p.x, by = bd.row * CELL + p.y;
      if (bx < r && bx + bs.w * CELL > l && by < b && by + bs.h * CELL > t) return true;
    }
    // the dragon's floating stage text needs repaints until it fades
    if (key === "center" && unlocked && window.GS.dragon.msgUntil > now) return true;
  }
  // blessing countdown in the top bar ticks every second
  if (window.GS.buff && window.GS.buff.until > now) return true;
  return false;
}
// Tick gate: repaint on "idle" ticks only when something animated is visible.
function needsLiveRepaint() { return animActive(); }

// ---- top bar --------------------------------------------------
function renderTopBar() {
  const key = regionAtCamCentre();
  const a = key ? DD.AREAS[key] : null;
  $("#area-name").innerHTML = (a ? `${a.icon} ${a.name}${E.isAreaUnlocked(key) ? "" : " 🔒"}` : "🌫️ Wilds")
    + (sprint ? ` <span class="sprint-tag">🏃2×</span>` : "")
    + ((window.GS.ascensions || 0) > 0 ? ` <span class="sprint-tag">☯${window.GS.ascensions}</span>` : "");
  $("#hand-count").textContent = `${E.handTotal()}/${E.handCap()}`;
  // active dragon-pill blessing with a live countdown
  const buff = window.GS.buff, bp = $("#buff-pill");
  if (buff && buff.until > Date.now() && DD.DRAGON_BUFFS[buff.kind]) {
    bp.classList.remove("hidden");
    bp.innerHTML = `${iconHTML(buff.kind)} ${DD.DRAGON_BUFFS[buff.kind].name} ${Math.ceil((buff.until - Date.now()) / 1000)}s`;
  } else bp.classList.add("hidden");
  $("#build-btn").classList.toggle("on", window.GS.build.open);
  $("#demolish-btn").classList.toggle("on", demolishMode);
  $("#debug-btn").classList.toggle("on", debugShow);
}

// ---- world painter --------------------------------------------
function spriteSize(size) { return size >= 4 ? size * 30 : size >= 3 ? 90 : size >= 2 ? 72 : 30; }

// One-pulse squash driven purely by hitAt time — re-draws resume mid-pulse
// automatically, and a fresh click (new hitAt) restarts it from zero.
function hitSquash(node, now) {
  const t = now - (node.hitAt || 0);
  if (t < 0 || t >= 180) return null;
  const p = t / 180;
  let sy;
  if (p < 0.35) sy = 1 - (p / 0.35) * 0.16;
  else if (p < 0.7) sy = 0.84 + ((p - 0.35) / 0.35) * 0.22;
  else sy = 1.06 - ((p - 0.7) / 0.3) * 0.06;
  return { sy, sx: 1 + (1 - sy) * 0.4 };
}

let lastDrawError = null;   // surfaced by the F9 diagnostic
function drawWorld() {
  if (!ctx) return;
  try { drawWorldInner(); }
  catch (err) { lastDrawError = String((err && err.stack) || err); }
}
function drawWorldInner() {
  const s = viewScale, now = Date.now();
  const vw = renderedW, vh = renderedW * VIEW_ASPECT;
  const X = wx => (wx - cam.x) * s, Y = wy => (wy - cam.y) * s;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  // hard-reset every stateful knob: a leak (or an exception that skipped a
  // restore) must never carry translucency/dashes/shadows into next frames —
  // that manifests as a permanent semi-transparent film over the world
  ctx.globalAlpha = 1;
  ctx.setLineDash([]);
  ctx.shadowBlur = 0;
  ctx.filter = "none";
  ctx.globalCompositeOperation = "source-over";
  ctx.imageSmoothingEnabled = false;   // pixel-art icons scale crisply

  // void + faint world-aligned grid lines
  ctx.fillStyle = C.void;
  ctx.fillRect(0, 0, vw, vh);
  const step = CELL * s;
  if (step >= 7) {
    ctx.strokeStyle = C.gridLine; ctx.lineWidth = 1;
    ctx.beginPath();
    for (let x = (Math.ceil(cam.x / CELL) * CELL - cam.x) * s; x <= vw; x += step) { ctx.moveTo(x, 0); ctx.lineTo(x, vh); }
    for (let y = (Math.ceil(cam.y / CELL) * CELL - cam.y) * s; y <= vh; y += step) { ctx.moveTo(0, y); ctx.lineTo(vw, y); }
    ctx.stroke();
  }

  const fr = 4 * CELL;
  const view = { l: cam.x - fr, t: cam.y - fr, r: cam.x + VIEW_W + fr, b: cam.y + VIEW_H + fr };
  const visible = [];
  for (const key of Object.keys(DD.WORLD.regions)) {
    const p = regionPx(key);
    if (p.x > view.r || p.x + PLAY_W < view.l || p.y > view.b || p.y + PLAY_W < view.t) continue;
    visible.push({ key, ox: p.x, oy: p.y, unlocked: E.isAreaUnlocked(key) });
  }
  // LAYERED painting across ALL regions: every ground first, then locked
  // stacks (objects under their veil), then every unlocked region's objects,
  // then all item icons on the very top. A later-drawn region's background or
  // veil can never cover an earlier region's sprites this way.
  for (const v of visible) drawRegionGround(v.key, v.ox, v.oy, view, s, X, Y);
  for (const v of visible) if (!v.unlocked) {
    drawRegionObjects(v.key, v.ox, v.oy, now, view, s, X, Y);
    drawRegionVeil(v.ox, v.oy, s, X, Y);
  }
  for (const v of visible) if (v.unlocked) drawRegionObjects(v.key, v.ox, v.oy, now, view, s, X, Y);
  for (const v of visible) if (v.unlocked) drawRegionItems(v.key, v.ox, v.oy, view, s, X, Y);

  // link picking: rubber-band line from the chosen source to the cursor
  if (linkMode && linkMode.picking === "target" && linkMode.srcId && cursor.over &&
      cursor.region === linkMode.area) {
    const src = E.buildingById(linkMode.area, linkMode.srcId);
    if (src) {
      const o = regionPx(linkMode.area);
      const sc = E.buildingCenterPx(src);
      ctx.strokeStyle = "rgba(251,191,36,.8)"; ctx.lineWidth = 2;
      ctx.setLineDash([8, 6]);
      ctx.beginPath();
      ctx.moveTo(X(o.x + sc.x), Y(o.y + sc.y));
      ctx.lineTo(X(o.x + cursor.lx), Y(o.y + cursor.ly));
      ctx.stroke();
      ctx.setLineDash([]);
    }
  }

  // placement preview (only in an unlocked region)
  if (window.GS.build.placing && cursor.over && cursor.region) {
    const B = E.buildingSize(window.GS.build.placing);
    const ok = E.isAreaUnlocked(cursor.region) &&
      E.canPlaceBuilding(cursor.region, cursor.lrow, cursor.lcol, window.GS.build.placing);
    const p = regionPx(cursor.region);
    const px = X(p.x + cursor.lcol * CELL), py = Y(p.y + cursor.lrow * CELL);
    ctx.fillStyle = ok ? "rgba(74,222,128,.25)" : "rgba(248,113,113,.25)";
    ctx.strokeStyle = ok ? C.accent : C.danger;
    ctx.lineWidth = 2;
    ctx.fillRect(px, py, B.w * CELL * s, B.h * CELL * s);
    ctx.strokeRect(px, py, B.w * CELL * s, B.h * CELL * s);
  }
}

// LAYER 1 — flat ground: region tint, zone tints, frame.
function drawRegionGround(key, ox, oy, view, s, X, Y) {
  const cfg = DD.AREAS[key];
  const seen = (x, y, w, h) => x < view.r && x + w > view.l && y < view.b && y + h > view.t;

  ctx.fillStyle = C.region[key] || C.region.center;
  ctx.fillRect(X(ox), Y(oy), PLAY_W * s, PLAY_W * s);

  const zoneRect = (z, fill, edge) => {
    const zx = z.c0 * CELL + ox, zy = z.r0 * CELL + oy;
    const zw = (z.c1 - z.c0 + 1) * CELL, zh = (z.r1 - z.r0 + 1) * CELL;
    if (!seen(zx, zy, zw, zh)) return;
    ctx.fillStyle = fill;
    ctx.fillRect(X(zx), Y(zy), zw * s, zh * s);
    if (edge) {
      ctx.strokeStyle = edge; ctx.lineWidth = 1;
      ctx.setLineDash([4, 4]);
      ctx.strokeRect(X(zx), Y(zy), zw * s, zh * s);
      ctx.setLineDash([]);
    }
  };
  for (const z of E.noBuildRects(key)) zoneRect(z, C.zone, C.zoneEdge);
  for (const gen of cfg.generators || []) {
    const tint = FIELD_TINT[gen.item] || FIELD_TINT.sand;
    for (const z of E.zoneRects(gen.zone)) zoneRect(z, tint[0], tint[1]);
  }
  // the enemy zone reads as danger (reddish over the wild-land green)
  if (cfg.enemies)
    for (const z of E.zoneRects(cfg.enemies.zone)) zoneRect(z, C.enemyZone, C.enemyZoneEdge);

  ctx.strokeStyle = C.frame; ctx.lineWidth = 2;
  ctx.strokeRect(X(ox), Y(oy), PLAY_W * s, PLAY_W * s);
}

// LAYER 2 — objects: buildings and resource nodes.
// phase: "all" | "noTrees" (skip tree nodes) | "treesOnly" (only tree nodes)
function drawRegionObjects(key, ox, oy, now, view, s, X, Y, phase = "all") {
  const cfg = DD.AREAS[key];
  const st = window.GS.areas[key];
  const unlocked = E.isAreaUnlocked(key);
  const seen = (x, y, w, h) => x < view.r && x + w > view.l && y < view.b && y + h > view.t;

  if (phase === "treesOnly") { drawRegionNodes(key, ox, oy, now, view, s, X, Y, phase, cfg, st, unlocked, seen); return; }

  // wisp-lantern links: faint dashed threads under everything else
  // (the lantern being edited gets its threads highlighted)
  ctx.lineWidth = Math.max(1, 1.5 * s);
  ctx.setLineDash([6 * s, 6 * s]);
  for (const b of st.buildings) {
    if (!b.links || !b.links.length) continue;
    const editing = linkMode && linkMode.area === key && linkMode.id === b.id;
    ctx.strokeStyle = editing ? "rgba(251,191,36,.65)" : "rgba(251,191,36,.22)";
    for (const l of b.links) {
      const f = st.buildings.find(x => x.id === l.from), t = st.buildings.find(x => x.id === l.to);
      if (!f || !t) continue;
      const fc = E.buildingCenterPx(f), tc = E.buildingCenterPx(t);
      if (!seen(Math.min(fc.x, tc.x) + ox, Math.min(fc.y, tc.y) + oy,
                Math.abs(fc.x - tc.x) + 1, Math.abs(fc.y - tc.y) + 1)) continue;
      ctx.beginPath();
      ctx.moveTo(X(ox + fc.x), Y(oy + fc.y));
      ctx.lineTo(X(ox + tc.x), Y(oy + tc.y));
      ctx.stroke();
    }
  }
  ctx.setLineDash([]);

  // buildings (ghosts + built)
  for (const b of st.buildings) {
    const bs = E.buildingSize(b.type);
    const bx = b.col * CELL + ox, by = b.row * CELL + oy;
    const bw = bs.w * CELL, bh = bs.h * CELL;
    if (!seen(bx, by, bw, bh)) continue;
    const bCfg = DD.BUILDINGS[b.type];
    const isAltar = b.type === "center";
    const isDragon = b.type === "dragon";
    const dragonAwake = isDragon && !E.dragonStage();   // gold once fully woken
    ctx.fillStyle = !b.built ? C.ghost : isAltar ? C.altar : isDragon ? C.dragon : C.built;
    ctx.strokeStyle = !b.built ? C.accent : isAltar ? C.gold
      : isDragon ? (dragonAwake ? C.gold : C.dragonEdge) : C.line;
    ctx.lineWidth = Math.max(1.5, 2 * s / 0.7);
    if (!b.built) ctx.setLineDash([6, 4]);
    ctx.fillRect(X(bx), Y(by), bw * s, bh * s);
    ctx.strokeRect(X(bx), Y(by), bw * s, bh * s);
    ctx.setLineDash([]);
    const cxp = X(bx + bw / 2);
    ctx.textAlign = "center";
    if (b.built && isAltar) {
      const job = window.GS.upgradeJob;
      ctx.fillStyle = C.text;
      ctx.font = `${56 * s}px ${EMOJI_FONT}`; ctx.textBaseline = "middle";
      ctx.fillText(bCfg.icon, cxp, Y(by + bh * 0.38));
      ctx.fillStyle = C.text; ctx.font = `800 ${24 * s}px ${TEXT_FONT}`;
      ctx.fillText(bCfg.name, cxp, Y(by + bh * 0.68));
      if (job) {
        ctx.fillStyle = C.gold;
        drawNeedsLine(Object.entries(E.jobRemaining(job)), cxp, Y(by + bh * 0.86), 20 * s);
      }
    } else if (b.built && isDragon) {
      const st = E.dragonStage();
      const awake = !st;                             // all stages fed: it AWAKENS
      ctx.fillStyle = C.text;
      ctx.font = `${64 * s}px ${EMOJI_FONT}`; ctx.textBaseline = "middle";
      ctx.fillText(awake ? "🐲" : bCfg.icon, cxp, Y(by + bh * 0.36));
      ctx.fillStyle = awake ? C.gold : "#d8b4fe"; ctx.font = `800 ${18 * s}px ${TEXT_FONT}`;
      ctx.fillText(awake ? "Awakened Dragon" : bCfg.name, cxp, Y(by + bh * 0.64));
      if (st) {
        ctx.fillStyle = C.gold;
        drawNeedsLine(Object.entries(E.dragonRemaining()), cxp, Y(by + bh * 0.84), 15 * s, "Feed:");
      } else {
        ctx.fillStyle = C.muted; ctx.font = `700 ${13 * s}px ${TEXT_FONT}`;
        ctx.fillText("watches over the grounds", cxp, Y(by + bh * 0.84));
      }
      // stage-up murmur floats above the dragon for a few seconds
      const dr = window.GS.dragon;
      if (dr.msg && dr.msgUntil > Date.now()) {
        ctx.fillStyle = "#e9d5ff"; ctx.font = `800 ${14 * s}px ${TEXT_FONT}`;
        ctx.fillText(dr.msg, cxp, Y(by - 12));
      }
    } else if (b.built && (bCfg.gather || bCfg.lantern || bCfg.seal || bCfg.stoker)) {
      // compact 1x1 wisp-logistics formations: icon + a tiny status badge
      ctx.fillStyle = C.text;
      ctx.font = `${20 * s}px ${EMOJI_FONT}`; ctx.textBaseline = "middle"; ctx.textAlign = "center";
      ctx.fillText(bCfg.icon, cxp, Y(by + bh * 0.5));
      if (bCfg.seal && b.item) drawItemIcon(b.item, cxp, Y(by - 8), 13 * s);
      ctx.fillStyle = C.gold; ctx.font = `800 ${9 * s}px ${TEXT_FONT}`;
      const badge = (bCfg.gather || bCfg.stoker) ? String(E.gatherTotal(b))
        : bCfg.seal ? String(b.qty || 0)
        : `${(b.links || []).length}⛓`;
      ctx.fillText(badge, cxp, Y(by + bh + 8));
    } else if (b.built && bCfg.recipes) {
      // converter (Forge): icon + name (· active recipe when it has several),
      // a progress bar while a batch works, and the input STOCK it holds
      // (or what the next batch still needs)
      const rec = E.recipeOf(b);
      ctx.fillStyle = C.text;
      ctx.font = `${24 * s}px ${EMOJI_FONT}`; ctx.textBaseline = "middle";
      ctx.fillText(bCfg.icon, cxp, Y(by + bh * 0.32));
      ctx.fillStyle = C.text; ctx.font = `700 ${10 * s}px ${TEXT_FONT}`;
      ctx.fillText(bCfg.name + (bCfg.recipes.length > 1 && rec ? ` · ${rec.name}` : ""),
        cxp, Y(by + bh * 0.56));
      // burners: thin orange fuel gauge (red when empty)
      if (bCfg.fuel) {
        const fw = bw * 0.7 * s, fx0 = X(bx + bw * 0.15), fy0 = Y(by + bh * 0.63);
        ctx.fillStyle = "rgba(255,255,255,.12)"; ctx.fillRect(fx0, fy0, fw, 3 * s);
        ctx.fillStyle = (b.fuel || 0) > 0 ? "#fb923c" : "#ef4444";
        ctx.fillRect(fx0, fy0, fw * clamp((b.fuel || 0) / DD.FUEL_CAP, 0, 1), 3 * s);
      }
      const now2 = Date.now();
      if (b.smeltDoneAt > now2 && rec) {
        const total = rec.timeMs * (DD.TEST.ENABLED ? DD.TEST.timeScale : 1);
        const frac = clamp(1 - (b.smeltDoneAt - now2) / total, 0, 1);
        const pw = bw * 0.7 * s, px0 = X(bx + bw * 0.15), py0 = Y(by + bh * 0.7);
        ctx.fillStyle = "rgba(255,255,255,.15)"; ctx.fillRect(px0, py0, pw, 4 * s);
        ctx.fillStyle = C.gold; ctx.fillRect(px0, py0, pw * frac, 4 * s);
      }
      const stockEntries = Object.entries(b.stock || {}).filter(([, q]) => q > 0);
      ctx.fillStyle = C.gold;
      if (stockEntries.length) drawNeedsLine(stockEntries, cxp, Y(by + bh * 0.88), 10 * s);
      else if (b.smeltDoneAt <= now2)
        drawNeedsLine(Object.entries(E.smeltRemaining(b)), cxp, Y(by + bh * 0.88), 10 * s, "Feed:");
    } else if (b.built && b.type === "storehouse") {
      if (b.item) drawItemIcon(b.item, cxp, Y(by + bh * 0.4), 26 * s);
      else {
        ctx.fillStyle = C.text;
        ctx.font = `${24 * s}px ${EMOJI_FONT}`; ctx.textBaseline = "middle";
        ctx.fillText(bCfg.icon, cxp, Y(by + bh * 0.4));
      }
      ctx.fillStyle = C.text; ctx.font = `700 ${10 * s}px ${TEXT_FONT}`; ctx.textBaseline = "middle";
      ctx.fillText(b.item ? `${E.itemName(b.item)} ×${b.qty}` : "empty", cxp, Y(by + bh * 0.78));
    } else {
      ctx.fillStyle = C.text;
      ctx.font = `${24 * s}px ${EMOJI_FONT}`; ctx.textBaseline = "middle";
      ctx.globalAlpha = b.built ? 1 : 0.7;
      ctx.fillText(bCfg.icon, cxp, Y(by + bh * 0.38));
      ctx.globalAlpha = 1;
      ctx.fillStyle = C.text; ctx.font = `700 ${10 * s}px ${TEXT_FONT}`;
      ctx.fillText(bCfg.name, cxp, Y(by + bh * 0.66));
      if (!b.built) {
        ctx.fillStyle = C.gold;
        drawNeedsLine(Object.entries(E.buildingNeeds(b)), cxp, Y(by + bh * 0.86), 10 * s);
      }
    }
  }

  drawRegionNodes(key, ox, oy, now, view, s, X, Y, phase, cfg, st, unlocked, seen);
  if (unlocked) drawRegionEnemies(key, ox, oy, now, s, X, Y, cfg, st, seen);
}

// roaming enemies: sprite + hp pips, hit squash on strikes
function drawRegionEnemies(key, ox, oy, now, s, X, Y, cfg, st, seen) {
  const ecfg = cfg.enemies;
  if (!ecfg) return;
  for (const en of st.enemies || []) {
    const ex = ox + en.x, ey = oy + en.y;
    if (!seen(ex - 24, ey - 30, 48, 60)) continue;
    const sq = hitSquash(en, now);
    ctx.save();
    ctx.translate(X(ex), Y(ey));
    if (sq) ctx.scale(sq.sx, sq.sy);
    ctx.fillStyle = C.text;
    ctx.font = `${(en.kind === "boss" ? 36 : 26) * s}px ${EMOJI_FONT}`;
    ctx.textAlign = "center"; ctx.textBaseline = "middle";
    ctx.fillText(en.sprite || ecfg.sprite, 0, 0);
    ctx.restore();
    // hp pips above the beast
    const n = en.maxHp, px0 = X(ex) - ((n - 1) * 10 * s) / 2;
    for (let i = 0; i < n; i++) {
      ctx.beginPath();
      ctx.arc(px0 + i * 10 * s, Y(ey - 24), 3 * s, 0, Math.PI * 2);
      ctx.fillStyle = i < en.hp ? C.danger : "rgba(255,255,255,.25)";
      ctx.fill();
    }
  }
}

// resource nodes, back-to-front by row so nearer sprites overlap correctly
function drawRegionNodes(key, ox, oy, now, view, s, X, Y, phase, cfg, st, unlocked, seen) {
  const nodes = st.nodes.filter(n => {
    if (phase === "noTrees" && n.kind === "tree") return false;
    if (phase === "treesOnly" && n.kind !== "tree") return false;
    return seen(n.col * CELL + ox, n.row * CELL + oy - 3 * CELL, n.size * CELL, (n.size + 3) * CELL);
  }).sort((a, b) => a.row - b.row);
  for (const node of nodes) {
    const w = node.size * CELL;
    const bx = ox + node.col * CELL + w / 2;          // sprite base (bottom-centre)
    let by = oy + (node.row + node.size) * CELL - 2;
    const interactive = unlocked && !node.deco;

    // fish bob
    if (unlocked && node.interaction === "surface" && node.surfaceUntil > now)
      by -= Math.abs(Math.sin(now / 300)) * 5;

    // ground pad under interactive nodes
    if (interactive) {
      ctx.fillStyle = "rgba(74,222,128,.16)";
      ctx.beginPath();
      ctx.ellipse(X(bx), Y(oy + (node.row + node.size) * CELL - 2), w * 0.42 * s, 7 * s, 0, 0, Math.PI * 2);
      ctx.fill();
    }

    const sprite = node.sprite || (DD.TIER_SPRITES[key] || [])[node.tier - 1] || cfg.icon;
    // deco border trees are oversized + jittered (scenery, not gameplay)
    const fpx = (node.deco ? 30 * (node.decoScale || 1.8) : spriteSize(node.size)) * s;
    const sq = hitSquash(node, now);
    ctx.save();
    ctx.translate(X(bx + (node.decoDx || 0)), Y(by + (node.decoDy || 0)));
    if (sq) ctx.scale(sq.sx, sq.sy);                  // squash from the ground up
    if (node.deco) ctx.globalAlpha = 0.55;
    ctx.fillStyle = C.text;   // if a browser ever silhouettes the glyph, keep it bright
    ctx.font = `${fpx}px ${EMOJI_FONT}`;
    ctx.textAlign = "center"; ctx.textBaseline = "alphabetic";
    ctx.fillText(sprite, 0, 0);
    // the Spirit Tree shimmers: sparkles over its canopy (fantasy dressing)
    if (node.kind === "spirittree") {
      ctx.font = `${fpx * 0.22}px ${EMOJI_FONT}`;
      ctx.fillText("✨", -fpx * 0.28, -fpx * 0.72);
      ctx.fillText("✨", fpx * 0.3, -fpx * 0.55);
      ctx.fillText("✨", fpx * 0.05, -fpx * 0.92);
    }
    ctx.restore();

    // overlays
    ctx.textAlign = "center"; ctx.textBaseline = "middle";
    if (debugShow && node.interaction === "quarry") {
      ctx.fillStyle = C.gold; ctx.font = `800 ${11 * s}px ${TEXT_FONT}`;
      ctx.fillText(`${node.clicks || 0}/${node.clicksPerDrop || 5}`, X(bx), Y(by - w - 6));
    } else if (debugShow && (node.interaction === "chop" || node.interaction === "break") && node.hitsLeft > 0) {
      ctx.fillStyle = C.gold; ctx.font = `800 ${11 * s}px ${TEXT_FONT}`;
      ctx.fillText(`${node.hitsLeft}`, X(bx), Y(by - w - 6));
    }
    if (unlocked && node.interaction === "surface" && node.surfaceUntil) {
      ctx.fillStyle = C.danger; ctx.font = `800 ${11 * s}px ${TEXT_FONT}`;
      ctx.fillText(`${Math.max(0, (node.surfaceUntil - now) / 1000).toFixed(1)}s`, X(bx), Y(by + 10));
    }
    if (node.autoFlash > now) {
      ctx.fillStyle = C.gold; ctx.font = `800 ${9 * s}px ${TEXT_FONT}`;
      ctx.fillText("AUTO", X(bx + w / 2), Y(by - w - 2));
    }
  }
}

// LAYER 3 — item icons on the very top (one emoji per dropped item).
function drawRegionItems(key, ox, oy, view, s, X, Y) {
  const st = window.GS.areas[key];
  const seen = (x, y, w, h) => x < view.r && x + w > view.l && y < view.b && y + h > view.t;
  for (const g of st.ground) {
    if (!seen(g.x + ox - 16, g.y + oy - 16, 32, 32)) continue;
    drawItemIcon(g.item, X(ox + g.x), Y(oy + g.y), 20 * s);
  }
  // wisps in flight: a soft glow carrying its item icon. Position is
  // computed from time-of-departure (wispPos) — smooth at any framerate.
  const nowW = Date.now();
  for (const w of st.wisps || []) {
    const p = E.wispPos(key, w, nowW);
    if (!seen(p.x + ox - 16, p.y + oy - 24, 32, 48)) continue;
    const wx = X(ox + p.x), wy = Y(oy + p.y);
    // returning wisps (refused delivery, flying cargo home) glow red
    ctx.fillStyle = w.returning ? "rgba(248,113,113,.35)" : "rgba(74,222,128,.30)";
    ctx.beginPath(); ctx.arc(wx, wy, 7 * s, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = "#eafff2";
    ctx.beginPath(); ctx.arc(wx, wy, 2.5 * s, 0, Math.PI * 2); ctx.fill();
    drawItemIcon(w.item, wx, wy - 12 * s, 14 * s);
  }
}

// Veil over a locked region (drawn right after its own objects).
function drawRegionVeil(ox, oy, s, X, Y) {
  ctx.fillStyle = C.veil;
  ctx.fillRect(X(ox), Y(oy), PLAY_W * s, PLAY_W * s);
  ctx.fillStyle = C.text;
  ctx.font = `${64 * s}px ${EMOJI_FONT}`;
  ctx.textAlign = "center"; ctx.textBaseline = "middle";
  ctx.fillText("🔒", X(ox + PLAY_W / 2), Y(oy + PLAY_W / 2));
}

// Edge buttons for LOCKED neighbour regions (DOM overlay — event-driven UI).
function renderUnlockButtons() {
  const wrap = $("#arrows");
  wrap.innerHTML = "";
  for (const [key, side] of Object.entries(DD.WORLD.unlockSide)) {
    if (E.isAreaUnlocked(key)) continue;
    const cost = E.areaUnlockCost(key);
    const label = Object.entries(cost).map(([it, q]) => `${q} ${iconHTML(it)}`).join(" ");
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
  hc.innerHTML = hand.map((s, i) =>
    `<span class="hc-stack${i === 0 ? " first" : ""}">${s.qty}<span class="hc-ico">${iconHTML(s.item)}</span></span>`
  ).join("");
}

// ---- build menu ---------------------------------------------
function renderBuildMenu() {
  const bar = $("#build-menu");
  bar.classList.toggle("hidden", !window.GS.build.open);
  if (!window.GS.build.open) return;
  bar.innerHTML = "";
  for (const b of E.buildingCatalog()) {
    const cost = Object.entries(b.cost).map(([it, q]) => `${q} ${iconHTML(it)}`).join(" ");
    const card = el("button", "build-card" + (window.GS.build.placing === b.id ? " active" : ""));
    card.innerHTML = `<span class="bc-ico">${b.icon}</span><span class="bc-name">${b.name}</span><span class="bc-cost">${cost}</span>`;
    card.onclick = () => { window.GS.build.placing = b.id; window.GS.build.open = false; render(); };
    bar.appendChild(card);
  }
}

// ---- upgrade TREE (canvas, nodebuster-style) -----------------
// Free-form node positions (px, from data), curved edges, radial guide rings.
// Visibility by distance from OWNED nodes: <=1 full, ==2 "?", >=3 hidden
// unless the tree's debug toggle reveals them.
let treeDebug = false;
let treeCam = null;         // screen position of the tree's (0,0)
let treeHoverId = null;
const TREE_R = 26;          // half-size of a node square

function treeStates() {
  const nodes = DD.UPGRADE_TREE;
  const adj = {}; nodes.forEach(n => adj[n.id] = new Set());
  nodes.forEach(n => (n.links || []).forEach(l => { adj[n.id].add(l); adj[l].add(n.id); }));
  const owned = new Set(nodes.filter(n => E.upgradeLevel(n.area, n.type).lvl > 0).map(n => n.id));
  const src = owned.size ? [...owned] : ["hand"];
  const dist = {}; src.forEach(id => dist[id] = 0);
  const q = [...src];
  while (q.length) { const id = q.shift(); for (const nb of adj[id]) if (!(nb in dist)) { dist[nb] = dist[id] + 1; q.push(nb); } }
  const info = {};
  for (const n of nodes) {
    const d = dist[n.id] ?? 99;
    const tier = d <= 1 ? "full" : d === 2 ? "mystery" : "hidden";
    info[n.id] = {
      n, tier,
      visible: tier !== "hidden" || treeDebug,
      selectable: tier === "full" && (n.id === "hand" || owned.has(n.id) || [...adj[n.id]].some(a => owned.has(a))),
      owned: owned.has(n.id),
    };
  }
  return info;
}
function treeBBox() {
  let minX = 0, minY = 0, maxX = 0, maxY = 0;
  for (const n of DD.UPGRADE_TREE) {
    minX = Math.min(minX, n.x); maxX = Math.max(maxX, n.x);
    minY = Math.min(minY, n.y); maxY = Math.max(maxY, n.y);
  }
  return { minX, minY, maxX, maxY };
}
function sizeTreeCanvas() {
  if (!tcvs) return;
  const body = $("#upgrades-body");
  dpr = window.devicePixelRatio || 1;
  tcvs.width = Math.round(body.clientWidth * dpr);
  tcvs.height = Math.round(body.clientHeight * dpr);
  tcvs.style.width = (tcvs.width / dpr) + "px";
  tcvs.style.height = (tcvs.height / dpr) + "px";
}
function clampTreeCam() {
  const body = $("#upgrades-body");
  const bw = body.clientWidth, bh = body.clientHeight;
  const bb = treeBBox(), m = 90;
  treeCam.x = clamp(treeCam.x, Math.min(bw / 2, bw - m - bb.maxX), Math.max(bw / 2, m - bb.minX));
  treeCam.y = clamp(treeCam.y, Math.min(bh / 2, bh - m - bb.maxY), Math.max(bh / 2, m - bb.minY));
}
// Nodebuster-style tooltip: name / "Level: x/y" / description, with a white
// MAX bar when complete or the cost line otherwise.
function treeTip(i) {
  if (i.tier === "mystery" && !treeDebug) return `<div class="t-name">???</div><div class="t-desc">Undiscovered upgrade</div>`;
  const { lvl, max } = E.upgradeLevel(i.n.area, i.n.type);
  const cost = E.upgradeCost(i.n.area, i.n.type);
  const job = window.GS.upgradeJob;
  const isSel = job && job.area === i.n.area && job.type === i.n.type;
  const costTxt = cost ? Object.entries(cost).map(([it, qy]) => `${qy} ${E.itemName(it)}`).join(", ") : null;
  const fedTxt = isSel
    ? `${Object.values(job.paid).reduce((a, b) => a + b, 0)}/${Object.values(job.needs).reduce((a, b) => a + b, 0)}`
    : "";
  return `<div class="t-name">${i.n.name}</div>` +
    `<div class="t-lv">Level: ${lvl}/${max}</div>` +
    `<div class="t-desc">${i.n.desc}</div>` +
    (isSel ? `<div class="t-fed">Selected — fed ${fedTxt}</div>` : "") +
    (costTxt ? `<div class="t-cost">Cost: ${costTxt}</div>` : `<div class="t-max">MAX</div>`);
}
function drawTree() {
  if (!tctx || !upgradesOpen) return;
  const body = $("#upgrades-body");
  const bw = body.clientWidth, bh = body.clientHeight;
  if (!treeCam) treeCam = { x: bw / 2, y: bh / 2 };
  clampTreeCam();
  const info = treeStates();
  const nodes = DD.UPGRADE_TREE;
  const P = n => ({ x: treeCam.x + n.x, y: treeCam.y + n.y });

  tctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  tctx.globalAlpha = 1; tctx.setLineDash([]); tctx.shadowBlur = 0;
  // dark board (nodebuster-style)
  tctx.fillStyle = "#232926";
  tctx.fillRect(0, 0, bw, bh);

  // thick pale connector lines between visible nodes
  for (const n of nodes) {
    for (const l of n.links || []) {
      const a = info[n.id], b = info[l];
      if (!a.visible || !b.visible) continue;
      const p1 = P(n), p2 = P(nodes.find(m => m.id === l));
      tctx.strokeStyle = a.owned && b.owned ? "rgba(225,232,224,.45)" : "rgba(225,232,224,.22)";
      tctx.lineWidth = 5;
      tctx.beginPath();
      tctx.moveTo(p1.x, p1.y);
      tctx.lineTo(p2.x, p2.y);
      tctx.stroke();
    }
  }

  // square nodes with state-coloured borders
  const job = window.GS.upgradeJob;
  const S = TREE_R;                                  // half-size of the square
  for (const n of nodes) {
    const i = info[n.id];
    if (!i.visible) continue;
    const p = P(n);
    const { lvl, max } = E.upgradeLevel(n.area, n.type);
    const cost = E.upgradeCost(n.area, n.type);
    const isSel = job && job.area === n.area && job.type === n.type;
    const hovered = treeHoverId === n.id;
    const mystery = i.tier === "mystery" && !treeDebug;

    // GREEN = can be upgraded now, GOLD = fully complete, RED = locked
    const ring = mystery ? C.danger
      : !cost ? C.gold
      : i.selectable ? C.accent
      : C.danger;
    tctx.globalAlpha = mystery ? 0.75 : (i.tier === "full" && !i.selectable && !i.owned) ? 0.6 : 1;
    tctx.fillStyle = hovered && i.selectable ? "#171c1f" : "#0d1113";
    tctx.strokeStyle = ring;
    tctx.lineWidth = 3;
    tctx.beginPath();
    tctx.roundRect(p.x - S, p.y - S, S * 2, S * 2, 7);
    tctx.fill();
    tctx.stroke();

    tctx.textAlign = "center";
    if (mystery) {
      tctx.fillStyle = C.danger; tctx.font = `800 26px ${TEXT_FONT}`; tctx.textBaseline = "middle";
      tctx.fillText("?", p.x, p.y);
    } else {
      tctx.fillStyle = C.text;
      tctx.font = `24px ${EMOJI_FONT}`; tctx.textBaseline = "middle";
      tctx.fillText(n.icon, p.x, p.y - 3);
      tctx.fillStyle = ring;
      tctx.font = `800 9px ${TEXT_FONT}`;
      tctx.fillText(`${lvl}/${max}`, p.x, p.y + S - 8);
    }
    tctx.globalAlpha = 1;

    // white corner brackets on hover / around the active job (selection frame)
    if (hovered || isSel) {
      const o = S + 6, L = 11;
      tctx.strokeStyle = isSel && !hovered ? C.gold : "#e8ecef";
      tctx.lineWidth = 3;
      tctx.beginPath();
      for (const [sx, sy] of [[-1, -1], [1, -1], [-1, 1], [1, 1]]) {
        tctx.moveTo(p.x + sx * o - sx * L, p.y + sy * o);
        tctx.lineTo(p.x + sx * o, p.y + sy * o);
        tctx.lineTo(p.x + sx * o, p.y + sy * o - sy * L);
      }
      tctx.stroke();
    }
  }
}
function treeNodeAtScreen(mx, my) {
  if (!treeCam) return null;
  const info = treeStates();
  for (const n of DD.UPGRADE_TREE) {
    const i = info[n.id];
    if (!i.visible) continue;
    const x = treeCam.x + n.x, y = treeCam.y + n.y;
    if (Math.abs(mx - x) <= TREE_R + 4 && Math.abs(my - y) <= TREE_R + 4) return i;
  }
  return null;
}
function onTreeMove(e) {
  const r = tcvs.getBoundingClientRect();
  const i = treeNodeAtScreen(e.clientX - r.left, e.clientY - r.top);
  const id = i ? i.n.id : null;
  if (id !== treeHoverId) { treeHoverId = id; drawTree(); }
  tcvs.style.cursor = i && i.selectable ? "pointer" : "default";
  if (i) {
    // tooltip sits centred ABOVE the node (nodebuster-style), not at the mouse
    const nx = r.left + treeCam.x + i.n.x;
    const ny = r.top + treeCam.y + i.n.y - TREE_R - 12;
    showTip(treeTip(i), nx, ny);
  } else hideTip();
}
function onTreeClick(e) {
  const r = tcvs.getBoundingClientRect();
  const i = treeNodeAtScreen(e.clientX - r.left, e.clientY - r.top);
  if (!i || !i.selectable) return;
  if (!E.upgradeCost(i.n.area, i.n.type)) return;   // maxed
  if (E.selectUpgrade(i.n.area, i.n.type)) { hideTip(); toggleUpgrades(false); render(); }
}

// Instant tooltip (native title tooltips have a fixed OS delay).
// Anchored bottom-centre at (x, y): CSS translates it up and centres it.
function showTip(html, x, y) {
  const t = $("#utip");
  t.innerHTML = html;
  t.style.left = clamp(x, 140, window.innerWidth - 140) + "px";
  t.style.top = Math.max(120, y) + "px";
  t.classList.remove("hidden");
}
function hideTip() { $("#utip").classList.add("hidden"); }

function renderUpgrades() { sizeTreeCanvas(); drawTree(); }
let upgradesOpen = false;
function toggleUpgrades(force) {
  upgradesOpen = force != null ? force : !upgradesOpen;
  $("#upgrades-modal").classList.toggle("hidden", !upgradesOpen);
  if (upgradesOpen) { treeCam = null; treeHoverId = null; renderUpgrades(); }   // recentre on open
  else hideTip();
}
function toggleTreeDebug() {
  treeDebug = !treeDebug;
  $("#tree-debug-btn").classList.toggle("on", treeDebug);
  drawTree();
}

function toggleDebug(force) {
  debugShow = force != null ? force : !debugShow;
  render();
}
function toggleBuild(force) {
  window.GS.build.open = force != null ? force : !window.GS.build.open;
  if (window.GS.build.open) { window.GS.build.placing = null; demolishMode = false; }
  render();
}
function toggleDemolish(force) {
  demolishMode = force != null ? force : !demolishMode;
  if (demolishMode) { window.GS.build.placing = null; window.GS.build.open = false; }
  render();
}

// ---- converter recipe menu ----------------------------------
// Left-clicking a converter opens this picker. Choosing a DIFFERENT recipe
// drops everything the building holds on the ground first (engine rule).
let recipeMenuFor = null;   // { area, id } while open
function openRecipeMenu(areaKey, b) {
  recipeMenuFor = { area: areaKey, id: b.id };
  renderRecipeMenu();
}
function closeRecipeMenu() {
  recipeMenuFor = null;
  $("#recipe-menu").classList.add("hidden");
}
function renderRecipeMenu() {
  const bar = $("#recipe-menu");
  if (!recipeMenuFor) { bar.classList.add("hidden"); return; }
  const b = E.buildingById(recipeMenuFor.area, recipeMenuFor.id);
  const recipes = b && DD.BUILDINGS[b.type].recipes;
  if (!b || !b.built || !recipes) { closeRecipeMenu(); return; }
  bar.classList.remove("hidden");
  bar.innerHTML = `<div class="rm-title">${DD.BUILDINGS[b.type].icon} ${DD.BUILDINGS[b.type].name} — recipes</div>`;
  recipes.forEach((r, i) => {
    const inputs = Object.entries(r.inputs).map(([it, q]) => `${q} ${iconHTML(it)}`).join(" + ");
    const active = (b.recipe || 0) === i;
    const card = el("button", "build-card" + (active ? " active" : ""));
    card.innerHTML = `<span class="bc-ico">${iconHTML(r.output)}</span>` +
      `<span class="bc-name">${r.name}</span>` +
      `<span class="bc-cost">${inputs} → ${r.outputQty || 1} ${iconHTML(r.output)}</span>` +
      (active ? `<span class="bc-tag">active</span>` : "");
    card.onclick = () => {
      E.setRecipe(recipeMenuFor.area, recipeMenuFor.id, i);
      closeRecipeMenu();
      render();
    };
    bar.appendChild(card);
  });
}

// ---- wisp-lantern link editor --------------------------------
// Left-click a lantern -> panel lists its links (removable) and "Add link"
// starts a two-click pick: SOURCE building on the map, then TARGET.
let linkMode = null;   // { area, id, picking: null|"source"|"target", srcId }
function bLabel(b) {
  const cfg = DD.BUILDINGS[b.type];
  const typed = (cfg.seal || b.type === "storehouse") && b.item ? ` ${iconHTML(b.item)}` : "";
  return `${cfg.icon} ${cfg.name}${typed}`;
}
function openLinkMenu(areaKey, b) {
  linkMode = { area: areaKey, id: b.id, picking: null, srcId: null };
  renderLinkMenu();
  requestGridPaint();
}
function closeLinkMenu() {
  linkMode = null;
  $("#link-menu").classList.add("hidden");
  requestGridPaint();
}
function renderLinkMenu() {
  const bar = $("#link-menu");
  if (!linkMode) { bar.classList.add("hidden"); return; }
  const lan = E.buildingById(linkMode.area, linkMode.id);
  if (!lan || !lan.built) { closeLinkMenu(); return; }
  bar.classList.remove("hidden");
  bar.innerHTML = `<div class="rm-title">🏮 Wisp Lantern — links run in order, one per beat</div>`;
  (lan.links || []).forEach((l, i) => {
    const f = E.buildingById(linkMode.area, l.from), t = E.buildingById(linkMode.area, l.to);
    const row = el("div", "link-row",
      `<span class="lr-n">${i + 1}.</span> ${f ? bLabel(f) : "?"} <span class="lr-arr">→</span> ${t ? bLabel(t) : "?"}`);
    const x = el("button", "lr-x", "✕");
    x.title = "Remove this link";
    x.onclick = () => { E.removeLink(linkMode.area, linkMode.id, i); renderLinkMenu(); render(); };
    row.appendChild(x);
    bar.appendChild(row);
  });
  if (linkMode.picking) {
    const src = linkMode.srcId ? E.buildingById(linkMode.area, linkMode.srcId) : null;
    bar.appendChild(el("div", "rm-hint", linkMode.picking === "source"
      ? "Click the SOURCE building on the map (gatherer / seal / storehouse)… Esc cancels"
      : `${src ? bLabel(src) : "?"} → click the TARGET building… Esc cancels`));
  } else {
    const add = el("button", "build-card", `<span class="bc-name">➕ Add link</span>`);
    add.onclick = () => { linkMode.picking = "source"; linkMode.srcId = null; renderLinkMenu(); };
    bar.appendChild(add);
  }
}

// ---- tutorial quest panel ------------------------------------
// The side panel shows ONE quest at a time; goals read live state so
// already-done things are instantly claimable. Rebuilt only when the
// quest index / progress / collapsed state actually changes.
let lastQuestKey = "";
function renderQuestPanel() {
  const panel = $("#quest-panel");
  const gq = window.GS.quest;
  const i = gq.idx, total = DD.QUESTS.length;
  const p = i < total ? E.questProgress(i) : null;
  const key = `${gq.hidden}|${i}|${p ? p.cur + "/" + p.need + "/" + p.done : "end"}`;
  if (key === lastQuestKey) return;
  lastQuestKey = key;

  if (gq.hidden) {
    panel.className = "mini";
    panel.innerHTML = `<button id="quest-chip" title="Show quests">📜${p && p.done ? "❗" : ""}</button>`;
    $("#quest-chip").onclick = () => { gq.hidden = false; renderQuestPanel(); };
    return;
  }
  panel.className = "";
  if (i >= total) {
    panel.innerHTML = `<div class="qp-head"><span>📜 Quests</span>` +
      `<button id="quest-min" title="Collapse">–</button></div>` +
      `<div class="qp-done">🎉 All quests complete!<br>The grounds are yours, cultivator.</div>`;
    $("#quest-min").onclick = () => { gq.hidden = true; renderQuestPanel(); };
    return;
  }
  const q = DD.QUESTS[i];
  panel.innerHTML =
    `<div class="qp-head"><span>📜 Quest ${i + 1}/${total}</span>` +
    `<button id="quest-min" title="Collapse">–</button></div>` +
    `<div class="qp-name">${q.icon} ${q.name}</div>` +
    `<div class="qp-desc">${q.desc}</div>` +
    `<div class="qp-bar"><div class="qp-fill" style="width:${Math.round(100 * p.cur / p.need)}%"></div></div>` +
    `<div class="qp-row"><span class="qp-prog">${p.cur}/${p.need}</span>` +
    `<button id="quest-claim" ${p.done ? "" : "disabled"}>${p.done ? "Claim ✔" : "Claim"}</button></div>`;
  $("#quest-min").onclick = () => { gq.hidden = true; renderQuestPanel(); };
  $("#quest-claim").onclick = () => { if (E.claimQuest()) renderQuestPanel(); };
}

// ---- help / tutorial modal -----------------------------------
// Sections appear only once their content actually exists in the run
// (no Forge lesson before the dragon teaches the Forge, etc).
function openHelp() {
  const dr = window.GS.dragon.stage || 0;
  const u = window.GS.world.unlocked;
  const S = [];
  S.push(["🕹️ Controls",
    "WASD pans the camera (Shift toggles 2× sprint), mouse wheel zooms, B opens the build menu, Esc cancels/closes."]);
  S.push(["✋ Gathering & the hand",
    "Left-click resource nodes to harvest; HOLD to auto-swing. Items fall on the ground — hold left-click near them to vacuum into your hand (cap shown bottom-right). RIGHT-click drops items / feeds buildings; the front stack feeds first."]);
  S.push(["🏗️ Buildings",
    "B places a ghost; right-click-feed it its cost to build. Left-click a converter to pick its recipe (switching drops its held stock). 🗑 Demolish refunds. Converters hold up to 20 of each input."]);
  S.push(["🔥 Fuel",
    "Burners (Kiln, Forge…) show an orange fuel gauge — feed them wood, bamboo or charcoal (charcoal burns 4× longer, from the Charcoal Pit). A Furnace Spirit 🕯️ stokes every burner within 3 cells from its own stash."]);
  S.push(["🏛️ Altar upgrades",
    "Click the Altar to open the upgrade tree. Select a node, then right-click-feed the Altar the cost shown on it. Switching refunds what you fed."]);
  S.push(["🐉 The Sleeping Dragon",
    "Feed it each stage's tribute (right-click) and it teaches new recipes. Its current wish is written on it."]);
  S.push(["🦊 Fox Spirits",
    "They prowl the red corner. Click to strike (hold to auto-attack); they drop Spirit Essence. The tree's combat branch adds damage, more foxes and an AoE. RIGHT-click Beast Bait 🪱 (Cauldron) inside their zone to lure a Spirit Boar 🐗 — tough, but the only Beast Bone source."]);
  S.push(["🫕 Dragon pills",
    "The Pill Furnace refines Qi Elixirs into four pills. Right-click one onto the dragon and it exhales a timed blessing (60s base, longer with Dragon Affinity): Ember = burners 2×, Verdant = regrow 2×, Swiftwind = wisps 2×, Stoneheart = double mining drops. A new pill replaces the active one."]);
  S.push(["🏮 Wisp network",
    "Gathering Stones 🧿 vacuum ground items. Wisp Lanterns ferry them: click a lantern to edit its links (source → target, served in order, one per beat). Warding Seals 🈯 only pass their tuned item — right-click one with an item to retune. Storehouses 📦 buffer a single type; left-click any buffer to withdraw."]);
  if (dr >= 1) S.push(["🔥 Forge & smelting",
    "The dragon taught you the Forge: feed it iron ore + wood (wisps or hand) and it smelts Iron Bars from its stock automatically."]);
  if (dr >= 2) S.push(["🪸 Algae Farm",
    "Places ONLY in the fishing waters; passively grows algae around itself."]);
  if (dr >= 3) S.push(["🪴 Herb Garden",
    "Grows Spirit Herbs around itself on land — the cultivation herb."]);
  if (dr >= 4) S.push(["🐲 The Awakened Dragon",
    "It watches over the grounds and sheds Dragon Scales 🔶 beside itself (faster with a Dragon Shrine, which also lengthens blessings)."]);
  if (dr >= 4) S.push(["⛩️ Ascension",
    "Craft Talismans (Atelier) and Star Steel (Anvil), gather Dragon Scales, and raise the Ascension Gate. Completing it offers ASCENSION: reset the grounds, keep +8% permanent global speed per ascension (☯ in the bottom bar)."]);
  if (u.farm || u.mine || u.fishing) S.push(["🗺️ Regions",
    "Each region has unique resources (Farm: rice & cotton & sand; Mine: iron & jade; Fishing: fish, algae & spring water). Unlock borders with wood."]);
  else S.push(["🗺️ Regions",
    "Locked regions wait beyond the borders — gather wood and pay at a glowing 🔓 border button to expand."]);

  $("#help-body").innerHTML = S.map(([t, d]) =>
    `<div class="hp-sec"><div class="hp-t">${t}</div><div class="hp-d">${d}</div></div>`).join("");
  $("#help-modal").classList.remove("hidden");
}
function closeHelp() { $("#help-modal").classList.add("hidden"); }

// ---- ascension gate dialog -----------------------------------
// Completing (or clicking) the built Gate offers the ending: ascend and
// keep +8% global speed per ascension, or keep playing this run.
function syncAscendModal() {
  const modal = $("#ascend-modal");
  if (!modal) return;
  const show = !!window.GS.ascendPrompt;
  if (show) $("#ascend-count").textContent =
    `You have ascended ${window.GS.ascensions || 0} time${(window.GS.ascensions || 0) === 1 ? "" : "s"}. ` +
    `Ascending now grants a permanent +8% speed to everything — and begins the grounds anew.`;
  modal.classList.toggle("hidden", !show);
}

// ---- dragon story dialog ------------------------------------
// A stage-up stores its line in GS.dragon.dialog; the modal shows until the
// player continues. Sync is idempotent — called from both render paths.
function syncDragonDialog() {
  const dlg = window.GS.dragon && window.GS.dragon.dialog;
  const modal = $("#dragon-modal");
  if (!modal) return;
  const show = !!dlg;
  if (show) $("#dragon-text").textContent = dlg;
  modal.classList.toggle("hidden", !show);
}
function dismissDragonDialog() {
  window.GS.dragon.dialog = null;
  syncDragonDialog();
  render();   // catalog may have gained a freshly-taught building
}

// ---- master render ------------------------------------------
// Full render — repaints the canvas AND rebuilds event-driven DOM UI
// (unlock buttons, build menu). Use on discrete events, not ticks.
function render() {
  renderTopBar();
  requestGridPaint();
  renderUnlockButtons();
  renderBuildMenu();
  renderHandCursor();
  syncDragonDialog();
  syncAscendModal();
  renderQuestPanel();
  if (upgradesOpen) drawTree();
}
// Tick / hold-loop render — canvas + fast HUD only.
function renderPlay() {
  renderTopBar();
  requestGridPaint();
  renderHandCursor();
  syncDragonDialog();
  syncAscendModal();
  renderQuestPanel();
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
  syncCursor(e);
  renderHandCursor();
  if ((window.GS.build.placing || (linkMode && linkMode.picking)) && cursor.over)
    requestGridPaint();   // move the placement preview / link rubber-band
}

function onMouseDown(e) {
  if (!cursor.over) return;
  const p = pointFromEvent(e);
  cursor.region = p.region; cursor.lx = p.lx; cursor.ly = p.ly; cursor.lrow = p.lrow; cursor.lcol = p.lcol;
  const active = p.region && E.isAreaUnlocked(p.region);

  if (e.button === 2) { // right — drop / feed (or cancel placement/demolish)
    e.preventDefault();
    if (window.GS.build.placing || demolishMode) { window.GS.build.placing = null; demolishMode = false; render(); return; }
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

  // demolish mode: left-click a building to destroy it (refunds drop)
  if (demolishMode) {
    if (active) {
      const target = E.buildingAt(p.region, p.lrow, p.lcol);
      if (target) E.demolishBuilding(p.region, target.id);
    }
    demolishMode = false;
    render();
    return;
  }

  if (active) {
    // link picking captures ALL world clicks until done/cancelled
    if (linkMode && linkMode.picking) {
      const bAt = p.region === linkMode.area ? E.buildingAt(p.region, p.lrow, p.lcol) : null;
      if (bAt && linkMode.picking === "source" && E.canBeLinkSource(bAt)) {
        linkMode.srcId = bAt.id; linkMode.picking = "target";
        renderLinkMenu(); requestGridPaint();
      } else if (bAt && linkMode.picking === "target" && E.canBeLinkTarget(bAt) && bAt.id !== linkMode.srcId) {
        E.addLink(linkMode.area, linkMode.id, linkMode.srcId, bAt.id);
        linkMode.picking = null; linkMode.srcId = null;
        renderLinkMenu(); render();
      }
      return;
    }
    // enemies are struck before anything else under the cursor
    const en = E.enemyAt(p.region, p.lx, p.ly);
    if (en) {
      const t = Date.now();
      if (t - lastClickAt >= CLICK_COOLDOWN) { E.attackEnemy(p.region, en.id); lastClickAt = t; }
      else en.hitAt = t;   // too fast to count — still flinch
      leftHeld = true; attackHeld = true; lastSwing = t;
      startLoop(); renderPlay();
      return;
    }
    const sh = E.buildingAt(p.region, p.lrow, p.lcol);
    // the Altar opens the upgrade tree
    if (sh && sh.built && sh.type === "center") { toggleUpgrades(true); return; }
    // the built Ascension Gate re-offers the ending
    if (sh && sh.built && DD.BUILDINGS[sh.type].gate) {
      window.GS.ascendPrompt = true; syncAscendModal(); return;
    }
    // converters open their recipe picker; lanterns their link editor
    if (sh && sh.built && DD.BUILDINGS[sh.type].recipes) { openRecipeMenu(p.region, sh); return; }
    if (sh && sh.built && DD.BUILDINGS[sh.type].lantern) { openLinkMenu(p.region, sh); return; }
    if (recipeMenuFor) { closeRecipeMenu(); }   // clicking elsewhere closes them
    if (linkMode) { closeLinkMenu(); }
    // left-click/hold a buffer building (storehouse / seal / gatherer) to
    // withdraw its contents into the hand
    if (sh && sh.built && (sh.type === "storehouse" ||
        DD.BUILDINGS[sh.type].seal || DD.BUILDINGS[sh.type].gather)) {
      leftHeld = true; withdrawSH = sh;
      E.withdrawFromBuilding(sh, 1);    // a click takes one; holding accelerates
      withdrawStart = Date.now(); lastWithdraw = withdrawStart;
      startLoop(); renderPlay();
      return;
    }
    const node = nodeAtCell(p.region, p.lrow, p.lcol);
    if (node && !node.deco) {   // decorative border trees are inert
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
      E.suctionStep(p.region, p.lx, p.ly, PICKUP_R);   // starts the pull; loop continues it
      startLoop(); renderPlay();
      return;
    }
  }
}

// ---- WASD camera pan ----------------------------------------
let panRunning = false;
function panStep() {
  const s = sprint ? 24 : 12;            // px per frame (Shift sprint = 2x)
  let dx = 0, dy = 0;
  if (keys.has("w")) dy -= s;
  if (keys.has("s")) dy += s;
  if (keys.has("a")) dx -= s;
  if (keys.has("d")) dx += s;
  if (dx || dy) {
    if (upgradesOpen) {
      // pan the upgrade tree instead of the map while it's open
      if (!treeCam) treeCam = { x: 0, y: 0 };
      treeCam.x -= dx; treeCam.y -= dy;
      drawTree();
    } else {
      cam.x += dx; cam.y += dy;
      clampCam();
      requestGridPaint();
      // the world slid under the cursor, so recompute what it's over
      syncCursor({ clientX: cursor.cx, clientY: cursor.cy });
    }
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
  if (k === "shift" && !e.repeat) { sprint = !sprint; renderTopBar(); return; }  // sprint toggle (2x pan)
  if (k === "b") { toggleBuild(); return; }   // B toggles the build menu
  if (e.key === "F9") {                       // self-diagnostic (rendering issues)
    e.preventDefault();
    const r = cvs.getBoundingClientRect();
    const mid = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
    const GSx = window.GS;
    alert("IDLE GROUNDS DIAGNOSTIC\n" + JSON.stringify({
      drawError: lastDrawError,
      dpr: window.devicePixelRatio, zoom: +zoom.toFixed(2),
      canvas: { attrW: cvs.width, attrH: cvs.height, cssW: Math.round(r.width), cssH: Math.round(r.height) },
      topElementAtCentre: mid ? (mid.id || String(mid.className).slice(0, 30) || mid.tagName) : "none",
      unlocked: GSx.world.unlocked,
      nodes: Object.fromEntries(Object.keys(GSx.areas).map(a => [a, GSx.areas[a].nodes.length])),
      ground: Object.fromEntries(Object.keys(GSx.areas).map(a => [a, GSx.areas[a].ground.length])),
    }, null, 1));
    return;
  }
  if (e.key === "Escape") {
    if (upgradesOpen) { toggleUpgrades(false); return; }
    if (recipeMenuFor) { closeRecipeMenu(); return; }
    if (linkMode) {
      // picking backs out to the menu; a second Esc closes it
      if (linkMode.picking) { linkMode.picking = null; linkMode.srcId = null; renderLinkMenu(); requestGridPaint(); }
      else closeLinkMenu();
      return;
    }
    window.GS.build.placing = null; demolishMode = false; render();
  }
}
function onKeyUp(e) { keys.delete(e.key.toLowerCase()); }

function onMouseUp(e) {
  if (e.button === 0) { leftHeld = false; pickupMode = false; harvestHeld = false; attackHeld = false; withdrawSH = null; }
  if (e.button === 2) rightHeld = false;
}

// while a mouse button is held, keep vacuuming / drip-dropping
function startLoop() {
  if (loopRunning) return;
  loopRunning = true;
  const step = () => {
    let dirty = false;
    const rg = cursor.region && E.isAreaUnlocked(cursor.region) ? cursor.region : null;
    if (leftHeld && pickupMode && cursor.over && rg) {
      // gravity suction: items in range drift to the cursor, collect on arrival
      const s = E.suctionStep(rg, cursor.lx, cursor.ly, PICKUP_R);
      if (s.moved > 0 || s.picked > 0) dirty = true;
    }
    if (leftHeld && withdrawSH) {
      // withdraw rate tweens 1/s -> 5/s over the first 3 seconds of the hold
      const elapsed = Date.now() - withdrawStart;
      const rate = 1 + Math.min(elapsed / 3000, 1) * 4;   // 1 .. 5 items per second
      if (Date.now() - lastWithdraw >= 1000 / rate) {
        if (E.withdrawFromBuilding(withdrawSH, 1) > 0) dirty = true;
        lastWithdraw = Date.now();
      }
    }
    // hold-left over an enemy keeps striking at the area's attack rate
    // (the beast moves, so re-hit-test under the cursor every swing)
    if (attackHeld && cursor.over && rg) {
      const ecfg = DD.AREAS[rg].enemies;
      if (ecfg && Date.now() - lastSwing >= (ecfg.attackMs || 400)) {
        const en = E.enemyAt(rg, cursor.lx, cursor.ly);
        if (en) { E.attackEnemy(rg, en.id); lastSwing = Date.now(); dirty = true; }
      }
    }
    // hold-left over a node auto-swings at that node's own harvest rate
    if (harvestHeld && cursor.over && rg) {
      const n = nodeAtCell(rg, cursor.lrow, cursor.lcol);
      if (n && !n.deco && Date.now() - lastSwing >= E.harvestInterval(rg, n)) {
        E.harvestNode(rg, n.id, true); lastSwing = Date.now(); dirty = true;
      }
    }
    if (rightHeld && cursor.over && rg) {
      // deliberate for the first second, then accelerate hard so you can dump fast
      const elapsed = Date.now() - holdStart;
      const rate = elapsed < 1000
        ? 4                                             // 4/s for the first second
        : 4 + Math.min((elapsed - 1000) / 1000, 1) * 16; // ramp 4 -> 20/s over the next second
      if (Date.now() - lastDrop >= 1000 / rate) { E.dropFromHand(rg, cursor.lx, cursor.ly); lastDrop = Date.now(); dirty = true; }
    }
    if (dirty) renderPlay();
    if (leftHeld || rightHeld) requestAnimationFrame(step);
    else loopRunning = false;
  };
  requestAnimationFrame(step);
}

function wireInput() {
  // willReadFrequently forces CPU rasterization — GPU-composited canvases
  // corrupt on some Windows drivers (stale frames showing through as a
  // translucent layer, smearing on pan). Software drawing costs ~1.6ms/frame
  // for this game, so this is pure win.
  cvs = $("#game-canvas");
  ctx = cvs.getContext("2d", { willReadFrequently: true });
  tcvs = $("#tree-canvas");
  tctx = tcvs.getContext("2d", { willReadFrequently: true });
  const vp = $("#world-viewport");
  vp.addEventListener("mousedown", onMouseDown);
  vp.addEventListener("wheel", onWheel, { passive: false });
  vp.addEventListener("contextmenu", e => e.preventDefault());
  tcvs.addEventListener("mousemove", onTreeMove);
  tcvs.addEventListener("click", onTreeClick);
  tcvs.addEventListener("mouseleave", () => { treeHoverId = null; hideTip(); if (upgradesOpen) drawTree(); });
  window.addEventListener("mousemove", onMouseMove);
  window.addEventListener("mouseup", onMouseUp);
  window.addEventListener("keydown", onKeyDown);
  window.addEventListener("keyup", onKeyUp);
  window.addEventListener("resize", fitViewport);
  recenterCamera();                     // start at the top-centre of the centre region
  fitViewport();
  requestAnimationFrame(fitViewport);   // re-fit once layout has settled
}

window.UI = { render, renderPlay, needsLiveRepaint, recenterCamera, setZoom,
  toggleUpgrades, toggleBuild, toggleDemolish, toggleDebug, toggleTreeDebug, wireInput,
  dismissDragonDialog, openHelp, closeHelp,
  _draw: () => drawWorld() };   // test hook
