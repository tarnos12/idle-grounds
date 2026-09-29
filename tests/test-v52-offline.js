#!/usr/bin/env node
/* Regression: v52 slice G — offline catch-up that loads fast and explains itself.
   (a) yield parity: new path (no ground physics during replay, memoized
       occupiedCells) vs the old path, 20 sim-min on a seeded late state —
       held-item totals within 2% (ground placement is stochastic; 1.07% seen on seed 11).
   (b) speed: same saturated state, new replay >= 2x faster than the old one
       (both measured here; old = physics on + un-memoized occupiedCells).
   (c) occupiedCells memo === a fresh computation after random add/remove.
   (d) sliced replay: Date.now restored between slices, an interrupted replay
       saves lastSeen at the resume point, and the next load replays exactly
       the remainder; Skip forfeits it.
   (e) summary shape: stalls ("why it stopped"), skippedMs, gate still 90s.
   Standalone vm sandbox — loads js/data.js, js/state.js, js/engine.js.
   Usage: node tests/test-v52-offline.js [repoRoot]   (default /home/user/idle-grounds)
   Exits 1 on any FAIL. */
"use strict";
const fs = require("fs"), path = require("path"), vm = require("vm");

const REPO = process.argv[2] || "/home/user/idle-grounds";
const JS = f => path.join(REPO, "js", f);

let fails = 0;
const check = (name, ok, info) => {
  console.log((ok ? "PASS " : "FAIL ") + name + (info !== undefined ? "  — " + info : ""));
  if (!ok) fails++;
};

// Seeded PRNG (mulberry32) so two sandboxes build the SAME world.
function mulberry32(a) {
  return function () {
    a |= 0; a = a + 0x6D2B79F5 | 0;
    let t = Math.imul(a ^ a >>> 15, 1 | a);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}

// boot({ seed, clock, saved }): clock = { t, inc } drives the sandbox's
// Date.now (each call advances t by inc) — null = the real clock. saved =
// a GS object served from localStorage. Saves land in s._saved.
function boot(opts) {
  opts = opts || {};
  const s = {}; s.window = s; s.console = console; s.JSON = JSON;
  if (opts.clock) {
    const c = opts.clock;
    const FD = class extends Date {};
    FD.now = () => { const t = c.t; c.t += c.inc || 0; return t; };
    s.Date = FD;
  } else s.Date = class extends Date {};
  if (opts.seed !== undefined) {
    const M = Object.create(null);
    for (const k of Object.getOwnPropertyNames(Math)) M[k] = Math[k];
    M.random = mulberry32(opts.seed);
    s.Math = M;
  } else s.Math = Math;
  s.location = { reload() {} };
  s._saved = null;
  s.localStorage = {
    getItem: () => (opts.saved === undefined ? null : JSON.stringify(opts.saved)),
    setItem: (k, v) => { s._saved = JSON.parse(v); }, removeItem: () => {},
  };
  vm.createContext(s);
  vm.runInContext(fs.readFileSync(JS("data.js"), "utf8"), s, { filename: "data.js" });
  for (const f of ["state.js", "engine.js"])
    vm.runInContext(fs.readFileSync(JS(f), "utf8"), s, { filename: f });
  s.onGroundDrop = null; s.onSfx = null;
  for (const k of Object.keys(s.DATA.AREAS)) s.ENGINE.initArea(k);
  return s;
}

// A late, saturating state: starter network, every region open, automation L3.
function lateState(s) {
  s.ENGINE.setupStarterNetwork();
  for (const k of Object.keys(s.GS.world.unlocked)) s.GS.world.unlocked[k] = true;
  for (const k of Object.keys(s.DATA.AREAS)) s.GS.areas[k].upgrades.automation = 3;
  return s;
}

// The pre-v52 occupiedCells, verbatim (a fresh string Set every call).
function freshOccupied(s, areaKey) {
  const set = new Set();
  const area = s.GS.areas[areaKey];
  for (const n of area.nodes)
    for (let r = n.row; r < n.row + n.size; r++)
      for (let c = n.col; c < n.col + n.size; c++) set.add(r + "," + c);
  for (const b of area.buildings) {
    const sz = s.ENGINE.buildingSize(b.type);
    for (let r = b.row; r < b.row + sz.h; r++)
      for (let c = b.col; c < b.col + sz.w; c++) set.add(r + "," + c);
    // (v52 fix wave) a BUILT burner's 3x2 fuel rack, left of the footprint
    if (b.built && s.DATA.BUILDINGS[b.type].fuel)
      for (let r = b.row; r <= b.row + 1; r++)
        for (let c = b.col - 3; c <= b.col - 1; c++) set.add(r + "," + c);
  }
  return set;
}

// The pre-v52 replay loop, verbatim math: ground physics ON inside every
// gameTick, and (optionally) the un-memoized occupiedCells swapped in —
// classic-script function declarations are global-object properties, so the
// engine's internal calls resolve to the replacement.
function oldReplay(s, elapsed, unmemo) {
  if (unmemo) s.occupiedCells = areaKey => freshOccupied(s, areaKey);
  const E = s.ENGINE;
  const last = s.GS.lastSeen;
  const step = Math.max(250, Math.ceil(elapsed / 45000));
  const before = E.countHeldItems();
  const realNow = s.Date.now;
  let virt = last, sinceAuto = 0;
  try {
    s.Date.now = () => virt;
    for (; virt < last + elapsed; virt += step) {
      E.gameTick();
      sinceAuto += step;
      if (sinceAuto >= 1000) { E.automationTick(); sinceAuto -= 1000; }
    }
  } finally { s.Date.now = realNow; }
  return { before, after: E.countHeldItems() };
}
const total = t => Object.values(t).reduce((a, b) => a + b, 0);
const gainedSum = (b, a) => Object.keys(a).reduce((n, k) => n + Math.max(0, a[k] - (b[k] || 0)), 0);

const T0 = 1.7e12;             // fixed virtual epoch for the seeded runs
const SIM = 20 * 60 * 1000;    // 20 sim-minutes

try {
  // ---- (a) yield parity + (b) speed ---------------------------------------
  {
    const runs = [];
    for (const seed of [7, 11]) {
      const clkOld = { t: T0 }, clkNew = { t: T0 };
      const sOld = lateState(boot({ seed, clock: clkOld }));
      const sNew = lateState(boot({ seed, clock: clkNew }));
      sOld.GS.lastSeen = T0; sNew.GS.lastSeen = T0;
      clkOld.t = T0 + SIM; clkNew.t = T0 + SIM;
      const same = JSON.stringify(sOld.GS) === JSON.stringify(sNew.GS);
      check(`parity seed ${seed}: both sandboxes start from the identical world`, same);

      let t = process.hrtime.bigint();
      const old = oldReplay(sOld, SIM, true);
      const msOld = Number(process.hrtime.bigint() - t) / 1e6;

      t = process.hrtime.bigint();
      const r = sNew.ENGINE.runOfflineCatchup();
      const msNew = Number(process.hrtime.bigint() - t) / 1e6;

      const hOld = total(old.after), hNew = total(sNew.ENGINE.countHeldItems());
      const gOld = gainedSum(old.before, old.after), gNew = Object.values(r.gained).reduce((a, b) => a + b, 0);
      const diff = Math.abs(hNew - hOld) / Math.max(1, hOld);
      check(`parity seed ${seed}: held-item totals within 2% (old vs new, 20 sim-min; stochastic placement noise)`, diff <= 0.02,
        `old=${hOld} new=${hNew} (${(diff * 100).toFixed(2)}%), gained old=${gOld} new=${gNew}`);
      check(`parity seed ${seed}: replay covered the whole window`, r.simulatedMs >= SIM && r.skippedMs === 0,
        `simulated=${r.simulatedMs} skipped=${r.skippedMs}`);
      runs.push({ msOld, msNew });
      // every ground item ends in a valid spot (finite, in the map, outside colliders)
      const P = sNew.DATA.GRID.cells * sNew.DATA.GRID.cell;
      let bad = 0;
      for (const k of Object.keys(sNew.DATA.AREAS))
        for (const g of sNew.GS.areas[k].ground)
          if (!Number.isFinite(g.x) || !Number.isFinite(g.y) || g.x < 0 || g.y < 0 || g.x > P || g.y > P) bad++;
      check(`parity seed ${seed}: all ground items finite + in bounds after replay`, bad === 0, "bad=" + bad);
      let inside = 0;
      for (const k of Object.keys(sNew.DATA.AREAS)) inside += sNew.ENGINE.pushOutOfColliders(k);
      check(`parity seed ${seed}: post-replay pass left nothing inside a collider`, inside === 0, "pushed=" + inside);
    }
    const msOld = runs.reduce((a, r) => a + r.msOld, 0), msNew = runs.reduce((a, r) => a + r.msNew, 0);
    check("speed: saturated 20-min replay >= 2x faster than the old path", msOld >= 2 * msNew,
      `old=${msOld.toFixed(0)}ms new=${msNew.toFixed(0)}ms (${(msOld / msNew).toFixed(2)}x)`);
  }

  // ---- (c) occupiedCells memo == fresh computation ------------------------
  {
    const s = lateState(boot({ seed: 3 })), E = s.ENGINE, D = s.DATA;
    const rng = mulberry32(99);
    const eq = (a, b) => a.size === b.size && [...a].every(x => b.has(x));
    let mism = 0, ops = 0, reused = 0;
    const keys = Object.keys(D.AREAS);
    for (let i = 0; i < 400; i++) {
      const k = keys[Math.floor(rng() * keys.length)], area = s.GS.areas[k], cfg = D.AREAS[k];
      const op = Math.floor(rng() * 6);
      if (op === 0 && (cfg.spawners || []).length) {
        E.spawnFromSpawner(k, cfg.spawners[Math.floor(rng() * cfg.spawners.length)]);
      } else if (op === 1) {
        const live = area.nodes.filter(n => !n.fixed && !n.deco);
        if (live.length) E.depleteNode(k, live[Math.floor(rng() * live.length)]);
      } else if (op === 2) {
        const r = Math.floor(rng() * D.GRID.cells), c = Math.floor(rng() * D.GRID.cells);
        E.placeBuilding(k, rng() < 0.5 ? "storehouse" : "gathering_stone", r, c);
      } else if (op === 3) {
        const bs = area.buildings.filter(b => !D.BUILDINGS[b.type].indestructible);
        if (bs.length) E.demolishBuilding(k, bs[Math.floor(rng() * bs.length)].id);
      } else if (op === 4) {
        // remove one node AND add one (same length) — the id bump must invalidate
        const live = area.nodes.filter(n => !n.fixed && !n.deco);
        if (live.length && (cfg.spawners || []).length) {
          area.nodes = area.nodes.filter(n => n !== live[0]);
          E.spawnFromSpawner(k, cfg.spawners[0]);
        }
      } else {
        const a1 = E.occupiedCells(k), a2 = E.occupiedCells(k);
        if (a1 === a2) reused++;
      }
      ops++;
      if (!eq(E.occupiedCells(k), freshOccupied(s, k))) mism++;
    }
    check("occupiedCells: memo identical to a fresh computation after 400 random add/remove ops",
      mism === 0, `ops=${ops} mismatches=${mism}`);
    check("occupiedCells: unchanged area returns the cached Set (no rebuild)", reused > 0, "reused=" + reused);
    // placeBuilding at a cell that just became occupied must be refused
    const k = "center", a = s.GS.areas.center;
    const n = a.nodes.find(nn => !nn.fixed && !nn.deco);
    check("occupiedCells: canPlaceBuilding sees a freshly spawned node",
      !n || E.canPlaceBuilding(k, n.row, n.col, "storehouse") === false);
  }

  // ---- (d) sliced replay + resume + skip ----------------------------------
  {
    // clock advances 1ms per read, so a slice budget of 40ms = ~40 ticks
    const clk = { t: T0, inc: 0 };
    const s = lateState(boot({ seed: 5, clock: clk })), E = s.ENGINE;
    s.GS.lastSeen = T0;
    clk.t = T0 + 60 * 60 * 1000;          // 1h away
    const job = E.beginOfflineCatchup();
    check("slice: 1h gap starts a job at or above the modal tier", !!job && job.awayMs >= E.OFFLINE_MODAL_MS,
      job ? "awayMs=" + job.awayMs : "null");
    const realFn = s.Date.now;
    clk.inc = 1;
    const done1 = E.stepOfflineCatchup(job, 40);
    const done2 = E.stepOfflineCatchup(job, 40);
    clk.inc = 0;
    check("slice: two 40ms slices do not finish a 1h replay", !done1 && !done2 && job.virt > job.start,
      `virt-start=${job.virt - job.start}ms`);
    check("slice: Date.now is the real clock again between slices", s.Date.now === realFn);
    check("slice: offlineActive() while unfinished", E.offlineActive() === true);
    const pct = E.offlineProgress(job);
    check("slice: progress in (0,1)", pct > 0 && pct < 1, pct.toFixed(4));

    // tab closes mid-replay: the unload save stamps the resume point
    const nowAtUnload = clk.t;
    const remaining = job.end - job.virt;
    s.SAVE.saveState();
    const saved = s._saved;
    check("resume: mid-replay save stamps lastSeen = now - remaining",
      saved && saved.lastSeen === nowAtUnload - remaining,
      saved ? `lastSeen=${saved.lastSeen} expected=${nowAtUnload - remaining}` : "no save");

    // next load 0ms later: the new job covers exactly the remainder
    const clk2 = { t: nowAtUnload };
    const s2 = boot({ seed: 6, clock: clk2, saved });
    const job2 = s2.ENGINE.beginOfflineCatchup();
    check("resume: next load replays exactly the unsimulated remainder",
      !!job2 && job2.elapsedMs === remaining, job2 ? `elapsed=${job2.elapsedMs} remaining=${remaining}` : "null");
    // …and 5 more minutes closed adds 5 minutes
    const clk3 = { t: nowAtUnload + 300000 };
    const job3 = boot({ seed: 6, clock: clk3, saved }).ENGINE.beginOfflineCatchup();
    check("resume: time closed after the unload is added on top",
      !!job3 && job3.elapsedMs === remaining + 300000, job3 ? "elapsed=" + job3.elapsedMs : "null");

    // Skip: remainder forfeited — the next save stamps the real clock
    E.skipOfflineCatchup(job);
    check("skip: step reports done immediately", E.stepOfflineCatchup(job, 40) === true);
    const sum = E.finishOfflineCatchup(job);
    check("skip: summary reports the forfeited time", sum.skippedMs > 0 && sum.simulatedMs + sum.skippedMs === sum.elapsedMs,
      `simulated=${sum.simulatedMs} skipped=${sum.skippedMs} elapsed=${sum.elapsedMs}`);
    check("skip: offlineActive() false, resume point cleared", !E.offlineActive() && E.offlineResumeAt() === null);
    s.SAVE.saveState();
    check("skip: next save stamps the real clock", s._saved.lastSeen === clk.t, `lastSeen=${s._saved.lastSeen} now=${clk.t}`);
    check("skip: finish is idempotent", E.finishOfflineCatchup(job) === sum);

    // physics flag never leaks out of a slice: live ticks settle again
    const area = s.GS.areas.center;
    // a free spot: no collider there, and no Gathering Stone within reach
    let spot = null;
    for (let y = 200; y < 2800 && !spot; y += 97) for (let x = 200; x < 2800 && !spot; x += 97) {
      area.ground = [{ id: 1, item: "wood", x, y }];
      const nearStone = area.buildings.some(b => s.DATA.BUILDINGS[b.type].gather &&
        Math.hypot(E.buildingCenterPx(b).x - x, E.buildingCenterPx(b).y - y) < (s.DATA.BUILDINGS[b.type].gather.radius + 2) * 32);
      if (!nearStone && E.pushOutOfColliders("center") === 0) spot = { x, y };
    }
    area.ground = [{ id: 1e6, item: "wood", x: spot.x, y: spot.y }, { id: 1e6 + 1, item: "wood", x: spot.x + 1, y: spot.y }];
    E.gameTick();
    const g = area.ground.filter(it => it.id >= 1e6);
    check("flag: live gameTick still settles overlapping items after a replay (spread to ~18px)",
      g.length === 2 && Math.hypot(g[0].x - g[1].x, g[0].y - g[1].y) >= 17,
      g.length === 2 ? "d=" + Math.hypot(g[0].x - g[1].x, g[0].y - g[1].y).toFixed(2) : "n=" + g.length);
  }

  // ---- (e) summary shape + stall reasons + gate ---------------------------
  {
    const clk = { t: T0 };
    const s = lateState(boot({ seed: 8, clock: clk })), E = s.ENGINE, D = s.DATA;
    s.GS.lastSeen = T0 - 30000;
    check("gate: 30s away -> no job", E.beginOfflineCatchup() === null);
    s.GS.lastSeen = T0 - 20 * 60 * 1000;
    const r = E.runOfflineCatchup();
    check("summary: fields present", r && Number.isFinite(r.awayMs) && Number.isFinite(r.simulatedMs) &&
      r.skippedMs === 0 && Array.isArray(r.stalls) && typeof r.gained === "object",
      r ? Object.keys(r).join(",") : "null");
    // (v52 fix wave: back-pressure + per-type bot skips keep a 20-min replay
    // far below the cap now, so the full-ground stall is staged directly)
    const fullArea = s.GS.areas.farm, keep = fullArea.ground;
    fullArea.ground = Array.from({ length: 600 }, (_, k) => ({ id: 5e6 + k, item: "wheat", x: 500, y: 500 }));
    const stl = E.offlineStalls();
    fullArea.ground = keep;
    check("summary: saturated state reports a full-ground stall", stl.some(x => x.kind === "ground" && x.areaKey === "farm"),
      JSON.stringify(stl.map(x => x.kind + ":" + x.areaKey)) + " replay: " + JSON.stringify(r.stalls.map(x => x.kind + ":" + x.areaKey)));
    // a burner holding a full batch but with no fuel -> "nofuel"; a pavilion with disciples and no food -> "nobuns"
    const area = s.GS.areas.center;
    const burner = area.buildings.find(b => b.built && D.BUILDINGS[b.type].fuel && E.recipeOf(b));
    if (burner) {
      const rec = E.recipeOf(burner);
      burner.smeltDoneAt = 0; burner.fuelQ = []; burner.stock = Object.assign({}, rec.inputs);
    }
    const pav = Object.keys(D.BUILDINGS).find(t => D.BUILDINGS[t].roster);
    if (pav) area.buildings.push({ id: area.nextBuildId++, type: pav, row: 2, col: 2, built: true, paid: {},
      item: null, qty: 0, disciples: 2, buns: 0 });
    const st = E.offlineStalls();
    check("stalls: unfueled burner with inputs -> nofuel", !burner || st.some(x => x.kind === "nofuel" && x.areaKey === "center"),
      JSON.stringify(st.map(x => x.kind)));
    check("stalls: pavilion with disciples and no food -> nobuns", !pav || st.some(x => x.kind === "nobuns"),
      JSON.stringify(st.map(x => x.kind)));
    const gs = area.buildings.find(b => b.built && D.BUILDINGS[b.type].gather);
    if (gs) gs.inv = [{ item: "wood", qty: D.BUILDINGS[gs.type].gather.cap }];
    check("stalls: full Gathering Stone -> stonefull", !gs || E.offlineStalls().some(x => x.kind === "stonefull"));
  }
} catch (e) {
  console.log("FAIL harness threw — " + ((e && e.stack) || e));
  fails++;
}

console.log(fails ? `\n${fails} FAILED` : "\nALL PASS");
process.exit(fails ? 1 : 0);
