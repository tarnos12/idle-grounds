#!/usr/bin/env node
/* Regression: ground-item cap (600/area, oldest despawn) + bucketed settleGround.
   Standalone vm sandbox — loads js/data.js, js/state.js, js/engine.js.
   Usage: node test-ground-settle.js [repoRoot]   (or REPO=... env)
   Exits 1 on any FAIL. */
"use strict";
const fs = require("fs"), path = require("path"), vm = require("vm");

const REPO = process.argv[2] || process.env.REPO ||
  (fs.existsSync(path.resolve(__dirname, "..", "js", "engine.js")) ? path.resolve(__dirname, "..") : "/home/user/idle-grounds");
const JS = f => path.join(REPO, "js", f);

let fails = 0;
const check = (name, ok, info) => {
  console.log((ok ? "PASS " : "FAIL ") + name + (info !== undefined ? "  — " + info : ""));
  if (!ok) fails++;
};

function boot(testMode) {
  const s = {}; s.window = s; s.console = console; s.Date = Date; s.Math = Math; s.JSON = JSON;
  s.localStorage = { getItem: () => null, setItem: () => {}, removeItem: () => {} };
  vm.createContext(s);
  vm.runInContext(fs.readFileSync(JS("data.js"), "utf8"), s, { filename: "data.js" });
  if (testMode !== undefined) s.DATA.TEST.ENABLED = testMode;
  vm.runInContext(fs.readFileSync(JS("state.js"), "utf8"), s, { filename: "state.js" });
  vm.runInContext(fs.readFileSync(JS("engine.js"), "utf8"), s, { filename: "engine.js" });
  for (const k of Object.keys(s.DATA.AREAS)) s.ENGINE.initArea(k);
  s.onGroundDrop = null; s.onSfx = null;
  return s;
}

// Reference = the pre-fix O(n^2) settleGround, verbatim math (MIN 18).
function refSettle(items, PLAY_PX) {
  const clampPx = v => Math.max(4, Math.min(PLAY_PX - 4, v));
  const MIN = 18; let moves = 0;
  for (let i = 0; i < items.length; i++) for (let j = i + 1; j < items.length; j++) {
    const a = items[i], b = items[j];
    let dx = b.x - a.x, dy = b.y - a.y, d = Math.hypot(dx, dy);
    if (d < 0.01) throw new Error("reference hit random branch — layout must avoid coincident items");
    if (d < MIN) {
      const push = (MIN - d) / 2, ux = dx / d, uy = dy / d;
      a.x -= ux * push; a.y -= uy * push; b.x += ux * push; b.y += uy * push; moves++;
    }
  }
  if (moves) for (const it of items) { it.x = clampPx(it.x); it.y = clampPx(it.y); }
  return moves;
}

// deterministic PRNG for layouts (independent of the engine's Math.random)
let seed = 12345;
const rnd = () => ((seed = (seed * 1103515245 + 12345) >>> 0) / 4294967296);

// ---- (a) cap: 800 drops -> 600 newest survive ----------------------------
{
  const s = boot(true), E = s.ENGINE, area = s.GS.areas.center;
  area.ground = [];
  const firstId = area.nextGroundId;
  E.dropGround("center", "stone", 800, 500, 500);          // one bulk call
  const ids = area.ground.map(g => g.id);
  const want = Array.from({ length: 600 }, (_, k) => firstId + 200 + k);
  check("cap: bulk drop of 800 leaves 600", area.ground.length === 600, "len=" + area.ground.length);
  check("cap: bulk survivors are the NEWEST 600 ids, in order", JSON.stringify(ids) === JSON.stringify(want),
    "first=" + ids[0] + " last=" + ids[ids.length - 1] + " want " + want[0] + ".." + want[599]);

  area.ground = [];
  const f2 = area.nextGroundId;
  for (let k = 0; k < 800; k++) E.dropGround("center", k % 2 ? "wood" : "stone", 1, 300 + (k % 40) * 20, 300 + Math.floor(k / 40) * 20);
  const ids2 = area.ground.map(g => g.id);
  check("cap: 800 single drops leave 600", area.ground.length === 600, "len=" + area.ground.length);
  check("cap: single-drop survivors are the NEWEST 600", ids2[0] === f2 + 200 && ids2[599] === f2 + 799 &&
    ids2.every((id, k) => id === f2 + 200 + k), "first=" + ids2[0] + " last=" + ids2[599]);
  const other = s.GS.areas.mine; other.ground = [];
  E.dropGround("mine", "stone", 10, 400, 400);
  check("cap: per-area (other area untouched by center overflow)", other.ground.length === 10 && area.ground.length === 600);
}

// ---- (b) behaviour: pair semantics match the old code --------------------
{
  const s = boot(true), E = s.ENGINE, area = s.GS.areas.center, P = s.DATA.GRID.cells * s.DATA.GRID.cell;
  // 4px apart -> old code: push = (18-4)/2 = 7 each -> exactly 18 apart
  area.ground = [{ id: 1, item: "stone", x: 400, y: 400 }, { id: 2, item: "stone", x: 404, y: 400 }];
  const m = E.settleGround("center");
  const [a, b] = area.ground, d = Math.hypot(b.x - a.x, b.y - a.y);
  check("pair 4px apart pushed to >= 18px", d >= 18 - 1e-9, "d=" + d.toFixed(6));
  check("pair 4px apart lands exactly where old code put it (393,400)/(411,400)",
    a.x === 393 && a.y === 400 && b.x === 411 && b.y === 400 && m === 1, `a=(${a.x},${a.y}) b=(${b.x},${b.y}) moves=${m}`);

  // straddling a 64px bucket boundary (x=448) must still be found
  area.ground = [{ id: 1, item: "stone", x: 446, y: 200 }, { id: 2, item: "stone", x: 450, y: 200 }];
  E.settleGround("center");
  const [c1, c2] = area.ground;
  check("cross-bucket pair (446 | 450) pushed apart to 18", Math.abs((c2.x - c1.x) - 18) < 1e-9, "dx=" + (c2.x - c1.x));
  // diagonal bucket neighbour
  area.ground = [{ id: 1, item: "stone", x: 447, y: 447 }, { id: 2, item: "stone", x: 449, y: 449 }];
  E.settleGround("center");
  const [e1, e2] = area.ground;
  check("diagonal-bucket pair pushed apart to 18", Math.abs(Math.hypot(e2.x - e1.x, e2.y - e1.y) - 18) < 1e-9);

  // far pair: untouched, returns 0
  area.ground = [{ id: 1, item: "stone", x: 100, y: 100 }, { id: 2, item: "stone", x: 300, y: 300 }];
  const mf = E.settleGround("center");
  check("far pair does not move (returns 0)", mf === 0 && area.ground[0].x === 100 && area.ground[0].y === 100 &&
    area.ground[1].x === 300 && area.ground[1].y === 300);
  // exactly 18 apart: old code did not push (d < MIN false)
  area.ground = [{ id: 1, item: "stone", x: 100, y: 100 }, { id: 2, item: "stone", x: 118, y: 100 }];
  check("pair at exactly MIN=18 is not pushed", E.settleGround("center") === 0 && area.ground[1].x === 118);
  // edge clamp still applies after a push near the map border
  area.ground = [{ id: 1, item: "stone", x: 5, y: 300 }, { id: 2, item: "stone", x: 7, y: 300 }];
  E.settleGround("center");
  check("push past the border is clamped to 4px", area.ground[0].x === 4, "x=" + area.ground[0].x);

  // randomized equivalence vs the old O(n^2) pass on SPARSE layouts
  // (every item has at most one neighbour < 18px, so pair order can't matter)
  let same = 0, trials = 300, worst = 0;
  for (let t = 0; t < trials; t++) {
    const pts = [];
    while (pts.length < 60) {
      const p = { x: 4 + rnd() * (P - 8), y: 4 + rnd() * (P - 8) };
      if (rnd() < 0.5 && pts.length) { const q = pts[pts.length - 1]; p.x = q.x + (rnd() - 0.5) * 30; p.y = q.y + (rnd() - 0.5) * 30; }
      const close = pts.filter(q => Math.hypot(q.x - p.x, q.y - p.y) < 40);
      if (close.length > 1 || close.some(q => Math.hypot(q.x - p.x, q.y - p.y) < 0.05)) continue;
      if (close.length === 1 && pts.some(q => q !== close[0] && Math.hypot(q.x - close[0].x, q.y - close[0].y) < 40)) continue;
      pts.push(p);
    }
    const A = pts.map((p, k) => ({ id: k, item: "stone", x: p.x, y: p.y }));
    const B = pts.map((p, k) => ({ id: k, item: "stone", x: p.x, y: p.y }));
    area.ground = A;
    const mNew = E.settleGround("center");
    const mOld = refSettle(B, P);
    let diff = 0;
    for (let k = 0; k < A.length; k++) diff = Math.max(diff, Math.abs(A[k].x - B[k].x), Math.abs(A[k].y - B[k].y));
    worst = Math.max(worst, diff);
    if (diff === 0 && mNew === mOld) same++;
  }
  check("sparse layouts: bit-identical to old pairwise pass (" + trials + " random trials)", same === trials,
    same + "/" + trials + " identical, worst diff=" + worst);
}

// ---- (c) perf: 600 clustered items, 2000 settle calls < 5s ---------------
{
  const s = boot(true), E = s.ENGINE, area = s.GS.areas.center;
  area.ground = [];
  for (let k = 0; k < 600; k++) area.ground.push({ id: k + 1, item: "stone", x: 1400 + rnd() * 80, y: 1400 + rnd() * 80 });
  const t0 = Date.now();
  let last = -1;
  for (let k = 0; k < 2000; k++) last = E.settleGround("center");
  const ms = Date.now() - t0;
  check("perf: 2000 settleGround calls on 600 clustered items < 5000ms", ms < 5000, ms + "ms (last pass moves=" + last + ")");
  // the pile should have spread out (min spacing close to 18)
  let minD = Infinity; const g = area.ground;
  for (let i = 0; i < g.length; i++) for (let j = i + 1; j < g.length; j++) minD = Math.min(minD, Math.hypot(g[i].x - g[j].x, g[i].y - g[j].y));
  check("perf: pile actually spread (min spacing >= 17px)", minD >= 17, "minSpacing=" + minD.toFixed(3));
  check("perf: no item lost or NaN", g.length === 600 && g.every(it => Number.isFinite(it.x) && Number.isFinite(it.y)));
}

// ---- (d) offline: 30 virtual minutes with automation < 30s ---------------
{
  const s = boot(false), E = s.ENGINE;                      // REAL balance, like the audit
  E.setupStarterNetwork();
  for (const k of ["center", "mine"]) { s.GS.world.unlocked[k] = true; s.GS.areas[k].upgrades.automation = 1; }
  s.GS.lastSeen = Date.now() - 30 * 60 * 1000;
  const t0 = Date.now();
  const r = E.runOfflineCatchup();
  const ms = Date.now() - t0;
  const ground = Object.keys(s.DATA.AREAS).map(k => s.GS.areas[k].ground.length);
  check("offline: 30min catch-up (auto L1 center+mine) < 30000ms", ms < 30000, ms + "ms");
  check("offline: returns a summary { elapsedMs, gained }", !!r && Number.isFinite(r.elapsedMs) && r.elapsedMs > 0 &&
    typeof r.gained === "object" && Object.keys(r.gained).length > 0,
    r ? "elapsedMs=" + r.elapsedMs + " items=" + Object.keys(r.gained).length : "null");
  check("offline: every area ground <= 600", ground.every(n => n <= 600), "ground=" + JSON.stringify(ground));
}

console.log(fails ? `\n${fails} FAILED` : "\nALL PASS");
process.exit(fails ? 1 : 0);
