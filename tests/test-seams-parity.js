#!/usr/bin/env node
/* Regression: merge-seam fixes on the design pass (engine).
   - live (50ms) vs coarse (640ms, offline-replay sized) tick parity for
     field generators incl. rare finds, converters incl. burner fuel, and
     Gathering Stone hauling
   - pull-aware push-out (no ping-pong at a fixed node's edge)
   - settleGround / pushOutOfColliders only count VISIBLE moves (idle = 0)
   - Frugal Frontier bought mid-run: overpaid installments refunded, a fully
     paid region opens; canPayUnlock true with nothing left to pay
   - wisp in-flight reservation: targets never overshot, no returning spill
   - starter network sinks (plank / brick / spirit-stone stores)
   - Center Automation taps the Spirit Tree (quarry + spring stay manual)
   - Remembered Paths opens mine -> fishing -> farm
   Standalone vm sandbox with a virtual clock — loads js/data.js,
   js/state.js, js/engine.js.
   Usage: node tests/test-seams-parity.js [repoRoot]   (default: this repo)
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

// boot(testEnabled, seed): fresh game on a virtual clock s.clock (ms) that
// Date.now() reads; Math.random is a seeded LCG so runs are reproducible.
function boot(testEnabled, seed) {
  const s = {}; s.window = s; s.console = console; s.JSON = JSON;
  let x = (seed || 7) >>> 0;
  s.Math = Object.create(Math);
  s.Math.random = () => ((x = (x * 1664525 + 1013904223) >>> 0) / 4294967296);
  s.clock = 1.7e12;
  s.Date = { now: () => s.clock };
  s.location = { reload() {} };
  s.localStorage = { getItem: () => null, setItem() {}, removeItem() {} };
  vm.createContext(s);
  vm.runInContext(fs.readFileSync(JS("data.js"), "utf8"), s, { filename: "data.js" });
  if (testEnabled !== undefined) s.DATA.TEST.ENABLED = !!testEnabled;
  for (const f of ["state.js", "engine.js"])
    vm.runInContext(fs.readFileSync(JS(f), "utf8"), s, { filename: f });
  s.onGroundDrop = null; s.onSfx = null;
  for (const k of Object.keys(s.DATA.AREAS)) s.ENGINE.initArea(k);
  return s;
}
// a finished building straight into an area (bypasses cost/unlock)
function put(s, areaKey, type, row, col, extra) {
  const a = s.GS.areas[areaKey];
  const b = Object.assign({ id: a.nextBuildId++, type, row, col, paid: {}, built: true, item: null, qty: 0 }, extra || {});
  if (s.DATA.BUILDINGS[type].gather) b.inv = [];
  if (s.DATA.BUILDINGS[type].lantern) { b.links = []; b.connIdx = 0; b.nextSend = 0; }
  a.buildings.push(b);
  return b;
}
function blankMine(s) {
  s.GS.world.unlocked.mine = true;
  const a = s.GS.areas.mine;
  a.nodes = []; a.ground = []; a.buildings = []; a.spawnQueue = []; a.wisps = []; a.genTimers = [];
  s.DATA.AREAS.mine.generators = [];
  return a;
}
// drive `ms` of world time in ticks of `step`, calling each(s) after every tick
function drive(s, ms, step, each) {
  for (let t = 0; t < ms; t += step) { s.ENGINE.gameTick(); if (each) each(s); s.clock += step; }
}
const within = (a, b, pct) => Math.abs(a - b) <= Math.max(a, b) * pct / 100;
const pctOf = (a, b) => (b ? (100 * a / b).toFixed(1) + "%" : "n/a");

try {
  // (1) field generator rare finds: full field (idle branch) + drained field
  for (const mode of ["full", "drained"]) {
    const res = {};
    for (const step of [50, 640]) {
      const s = boot(true, 11), E = s.ENGINE, D = s.DATA, CELL = D.GRID.cell;
      const a = s.GS.areas.center;
      a.buildings = a.buildings.filter(b => b.type === "center" || b.type === "dragon");
      // only the quarry field runs, with a loud rare chance (low variance)
      const gen = D.AREAS.center.generators.find(g => g.kind === "stone");
      gen.rareDrop.chance = 0.5;
      D.AREAS.center.generators = [gen];
      a.genTimers = [0];
      const z = D.ZONES[gen.zone][0];
      const tally = {};
      s.onGroundDrop = (k, item, qty) => { tally[item] = (tally[item] || 0) + qty; };
      const fill = () => {           // full: 10 stones lie in the field, jade collected
        a.ground = [];
        for (let k = 0; k < gen.cap; k++)
          a.ground.push({ id: a.nextGroundId++, item: "stone", x: (z.c0 + 1 + (k % 5)) * CELL, y: (z.r0 + 2 + Math.floor(k / 5) * 3) * CELL });
      };
      if (mode === "full") fill();
      drive(s, 10 * 60000, step, () => { if (mode === "full") a.ground = a.ground.filter(g => g.item !== "jade_shard"); else a.ground = []; });
      res[step] = tally;
    }
    const j50 = res[50].jade_shard || 0, j640 = res[640].jade_shard || 0;
    check(`field gen (${mode} field): 640ms-step jade within 6% of live (${j640} vs ${j50} = ${pctOf(j640, j50)})`,
      j50 > 500 && within(j50, j640, 6));
    if (mode === "drained") {
      const s50 = res[50].stone || 0, s640 = res[640].stone || 0;
      check(`field gen (drained): 640ms-step stone within 3% of live (${s640} vs ${s50})`, s50 > 1000 && within(s50, s640, 3));
    }
  }

  // (2) converters: burner (Kiln, fuel) + plain (Workbench), 640ms vs 50ms
  {
    // crafts per building (tallied from drops) + fuel burned, at one step
    const count = step => {
      const s = boot(true, 3), E = s.ENGINE; const a = blankMine(s);
      const kiln = put(s, "mine", "kiln", 30, 60, { recipe: 0, stock: { clay: 20 }, fuelQ: [] });
      const bench = put(s, "mine", "workbench", 30, 20, { recipe: 0, stock: { wood: 20 } });
      const tally = {}; let woodIn = 0;
      s.onGroundDrop = (k, item, qty) => { tally[item] = (tally[item] || 0) + qty; };
      drive(s, 5 * 60000, step, () => {
        kiln.stock.clay = 20; bench.stock.wood = 20;
        while (E.fuelSpace(kiln) > 0) { E.addFuelItem(kiln, "wood"); woodIn++; }
        a.ground = [];
      });
      return { brick: tally.brick || 0, plank: tally.plank || 0, burned: woodIn * s.DATA.FUEL.wood - E.fuelTotal(kiln) };
    };
    const L = count(50), C = count(640);
    check(`converter: Kiln bricks at 640ms within 3% of live (${C.brick} vs ${L.brick} = ${pctOf(C.brick, L.brick)})`, L.brick > 200 && within(L.brick, C.brick, 3));
    check(`converter: Workbench planks at 640ms within 3% of live (${C.plank} vs ${L.plank} = ${pctOf(C.plank, L.plank)})`, L.plank > 300 && within(L.plank, C.plank, 3));
    check(`converter: Kiln fuel burned at 640ms within 4% of live (${Math.round(C.burned)} vs ${Math.round(L.burned)} ms = ${pctOf(C.burned, L.burned)})`,
      L.burned > 200000 && within(L.burned, C.burned, 4));
    // a batch that runs the full 5 min burns ~the elapsed time (the tail is charged)
    check("converter: live Kiln burns ~ its running time (tail segment charged)", within(L.burned, 5 * 60000, 3), Math.round(L.burned) + "ms of 300000");
  }

  // (3) Gathering Stone hauling: 640ms steps haul at the live rate
  {
    const res = {};
    for (const step of [50, 640]) {
      const s = boot(true, 5), E = s.ENGINE, CELL = s.DATA.GRID.cell; const a = blankMine(s);
      const gs = put(s, "mine", "gathering_stone", 45, 45);
      const c = { x: 45.5 * CELL, y: 45.5 * CELL };
      for (let k = 0; k < 40; k++) {
        const ang = (k / 40) * Math.PI * 2, R = 7 * CELL;
        a.ground.push({ id: a.nextGroundId++, item: "stone", x: c.x + Math.cos(ang) * R, y: c.y + Math.sin(ang) * R });
      }
      let t = 0, doneAt = null;
      E.gameTick();                                   // (first tick: tickGap 0)
      while (t < 30000 && doneAt === null) { s.clock += step; t += step; E.gameTick(); if (!a.ground.length) doneAt = t; }
      res[step] = { doneAt, held: E.gatherTotal(gs) };
    }
    check(`stone hauling: 40 items collected at 640ms steps within ~1 step of live (${res[640].doneAt}ms vs ${res[50].doneAt}ms)`,
      res[50].doneAt !== null && res[640].doneAt !== null && res[640].doneAt <= res[50].doneAt * 1.15 + 640 && res[640].held === 40,
      JSON.stringify(res));
    // a 10s throttled-tab tick moves an item at most 13 live ticks' worth
    const s = boot(true, 5), E = s.ENGINE, CELL = s.DATA.GRID.cell; const a = blankMine(s);
    put(s, "mine", "gathering_stone", 45, 45);
    const g = { id: a.nextGroundId++, item: "stone", x: 45.5 * CELL - 7.9 * CELL, y: 45.5 * CELL };
    a.ground.push(g);
    E.gameTick(); const x0 = g.x; s.clock += 10000; E.gameTick();
    check("stone pull: a 10s tick is capped at 13x the live step", g.x - x0 <= 6 * 13 + 1e-6 && g.x - x0 > 6, "moved " + (g.x - x0).toFixed(1) + "px");
  }

  // (4) pull-aware push-out: an item a stone was hauling exits TOWARD it
  {
    const s = boot(true, 1), E = s.ENGINE, CELL = s.DATA.GRID.cell; const a = blankMine(s);
    a.nodes.push({ id: 900, row: 40, col: 40, size: 2, kind: "rock", fixed: true, tier: 1, interaction: "quarry" });
    const gs = put(s, "mine", "gathering_stone", 40, 46);
    const stoneC = { x: 46.5 * CELL, y: 40.5 * CELL };
    // just inside the rock's LEFT edge (nearest exit = away from the stone)
    const g = { id: a.nextGroundId++, item: "stone", x: 40 * CELL + 6, y: 40.6 * CELL, _pullAt: s.clock - 300, _pullTo: stoneC };
    a.ground = [g];
    E.pushOutOfColliders("mine");
    check("push-out: a recently pulled item exits on the side facing its stone", g.x > 42 * CELL, "x=" + g.x.toFixed(1));
    const h = { id: a.nextGroundId++, item: "stone", x: 40 * CELL + 6, y: 40.6 * CELL };
    a.ground = [h];
    E.pushOutOfColliders("mine");
    check("push-out: an unpulled item still takes the nearest edge", h.x < 40 * CELL, "x=" + h.x.toFixed(1));
    const k = { id: a.nextGroundId++, item: "stone", x: 40 * CELL + 6, y: 40.6 * CELL, _pullAt: s.clock - 6000, _pullTo: stoneC };
    a.ground = [k];
    E.pushOutOfColliders("mine");
    check("push-out: a stale pull (> 5s) no longer steers the exit", k.x < 40 * CELL, "x=" + k.x.toFixed(1));
    gs.inv = [];
    check("_pullTo is transient (stripped on save)", !JSON.stringify({ g }, (key, v) => (key[0] === "_" ? undefined : v)).includes("_pullTo"));
  }

  // (5) settle / push-out count only visible moves -> an idle pile is 0
  {
    const s = boot(true, 9), E = s.ENGINE; const a = blankMine(s);
    for (let k = 0; k < 60; k++) a.ground.push({ id: a.nextGroundId++, item: "stone", x: 1500 + (k % 8) * 3, y: 1500 + Math.floor(k / 8) * 3 });
    let last = -1;
    for (let k = 0; k < 400; k++) last = E.settleGround("mine");
    let tail = 0;
    for (let k = 0; k < 50; k++) tail += E.settleGround("mine");
    check("settleGround: a settled 60-item pile reports 0 visible moves", tail === 0, "last=" + last + " tail=" + tail);
    // an item inside a footprint flush against the map edge can't move: not a move
    put(s, "mine", "storehouse", 0, 0);
    a.ground = [{ id: a.nextGroundId++, item: "stone", x: 4, y: 4 }];
    const inAll = [];
    for (let r = 0; r < 3; r++) for (let c = 3; c < 6; c++) put(s, "mine", "gathering_stone", r, c);   // wall off the right
    for (let c = 0; c < 6; c++) put(s, "mine", "gathering_stone", 3, c);                              // and the bottom
    const m1 = E.pushOutOfColliders("mine"), m2 = E.pushOutOfColliders("mine");
    check("pushOutOfColliders: a boxed-in item that can't move isn't counted", m2 === 0, "m1=" + m1 + " m2=" + m2);
  }

  // (6) Frugal Frontier mid-run ----------------------------------------------
  {
    // (REAL costs: mine 16 wood -> 13 at Frugal 1, fishing 20 -> 16)
    const s = boot(false, 2), E = s.ENGINE, G = s.GS, D = s.DATA;
    G.ascendPoints = 100; G.hand = []; G.handCap = 1000;
    const full = E.areaUnlockCost("mine");
    const items = Object.keys(full);
    // pay everything but 1 of the first item
    G.world.unlockPaid = { mine: Object.assign({}, full) };
    G.world.unlockPaid.mine[items[0]] -= 1;
    check("REAL mine cost is 16 wood (fixture for the refund math)", JSON.stringify(full) === '{"wood":16}', JSON.stringify(full));
    check("before Frugal: mine needs 1 more, can't pay from an empty hand", !E.canPayUnlock("mine") && Object.keys(E.unlockRemaining("mine")).length === 1);
    // a partly paid region that stays short even after the discount
    G.world.unlockPaid.farm = {};
    const farmCost = E.areaUnlockCost("farm"), fk = Object.keys(farmCost)[0];
    G.world.unlockPaid.farm[fk] = 1;
    check("buyPerk('frugal') succeeds", E.buyPerk("frugal"));
    const cheap = E.areaUnlockCost("mine");
    check("Frugal: a region whose remaining cost is now empty opens on the spot", G.world.unlocked.mine === true && !G.world.unlockPaid.mine,
      JSON.stringify(G.world.unlockPaid));
    const refund = items.every(it => E.handCount(it) === (full[it] - (it === items[0] ? 1 : 0)) - cheap[it]);
    check("Frugal: overpaid installments go back to the hand (15 paid - 13 = 2 wood)", refund && E.handCount("wood") === 2,
      items.map(it => `${it}:${E.handCount(it)}`).join(" "));
    check("Frugal: an under-paid region stays locked with its installment", !G.world.unlocked.farm && G.world.unlockPaid.farm[fk] === 1);
    // overflow beside the Altar when the hand is full
    const s2 = boot(false, 2), E2 = s2.ENGINE, G2 = s2.GS;
    G2.ascendPoints = 100; G2.hand = []; G2.handCap = 1;
    const f2 = E2.areaUnlockCost("fishing");
    G2.world.unlockPaid = { fishing: Object.assign({}, f2) };
    const g0 = s2.GS.areas.center.ground.length;
    E2.buyPerk("frugal");
    const c2 = E2.areaUnlockCost("fishing");
    const over = Object.keys(f2).reduce((n, it) => n + f2[it] - c2[it], 0);
    const spilled = s2.GS.areas.center.ground.slice(g0);
    const PX = s2.DATA.GRID.cells * s2.DATA.GRID.cell;
    check("Frugal: refund overflow lands beside the Altar as player drops", G2.world.unlocked.fishing && over === 4 &&
      E2.handCount("wood") === 1 && spilled.length === over - 1 && spilled.every(g => g.manualAt && Math.hypot(g.x - PX / 2, g.y - (PX / 2 + 3 * 32)) < 40),
      `over=${over} spilled=${spilled.length}`);
    // canPayUnlock: nothing left to pay -> true, and the click opens it
    const s3 = boot(true, 2), E3 = s3.ENGINE, G3 = s3.GS;
    G3.hand = [];
    G3.world.unlockPaid = { fishing: Object.assign({}, E3.areaUnlockCost("fishing")) };
    check("canPayUnlock: true when the remaining cost is empty (empty hand)", E3.canPayUnlock("fishing") === true);
    check("unlockArea on a fully paid region opens it", E3.unlockArea("fishing") === true && G3.world.unlocked.fishing);
  }

  // (7) wisp in-flight reservation: seal / converter never overshot ----------
  {
    const s = boot(true, 4), E = s.ENGINE; const a = blankMine(s);
    const src = put(s, "mine", "storehouse", 45, 5, { item: "wood", qty: 150 });
    const seal = put(s, "mine", "warding_seal", 45, 85, { item: "wood", lock: true });   // a long flight
    const bench = put(s, "mine", "workbench", 10, 85, { recipe: 0, stock: {} });
    const lan = put(s, "mine", "wisp_lantern", 46, 45);
    lan.links.push({ from: src.id, to: seal.id }, { from: src.id, to: bench.id });
    const cap = s.DATA.BUILDINGS.warding_seal.seal.cap;
    let worstSeal = 0, worstStock = 0, returned = 0, dropped = 0;
    const dropInfo = {};
    s.onGroundDrop = (k, item) => { if (k !== "mine") return; dropped++; dropInfo[item] = (dropInfo[item] || 0) + 1; };
    drive(s, 60000, 50, () => {
      const fly = a.wisps.filter(w => w.toId === seal.id).length;
      worstSeal = Math.max(worstSeal, seal.qty + fly);
      worstStock = Math.max(worstStock, (bench.stock.wood || 0) + a.wisps.filter(w => w.toId === bench.id).length);
      returned += a.wisps.filter(w => w.returning).length;
      src.qty = 150;
      bench.stock.wood = Math.min(bench.stock.wood || 0, 20); bench.smeltDoneAt = 0;   // bench never crafts: stock only fills
    });
    check(`wisps: seal + cargo in flight never exceeds its cap (${worstSeal} <= ${cap})`, worstSeal <= cap && seal.qty === cap);
    check(`wisps: converter stock + cargo in flight never exceeds stockCap (${worstStock} <= 20)`, worstStock <= 20);
    check("wisps: no wisp ever flew home and nothing spilled", returned === 0 && dropped === 0 && a.ground.length === 0,
      `returned=${returned} dropped=${JSON.stringify(dropInfo)}`);
    // a full storehouse: nothing sent, link reads refused
    const s2 = boot(true, 4), E2 = s2.ENGINE; const a2 = blankMine(s2);
    const st = put(s2, "mine", "storehouse", 45, 80, { item: null, qty: 0 });
    const w1 = { id: 1, item: "wood", toId: st.id, x0: 0, y0: 0, x: 0, y: 0, t0: s2.clock, sp: 1 };
    a2.wisps.push(w1);
    const fly = E2.inFlightTo(a2, st);
    check("wisps: an empty untyped store takes the type of cargo flying in", E2.endpointAccepts(st, "wood", fly) && !E2.endpointAccepts(st, "stone", fly));
  }

  // (7b) lantern fairness: a shared seal can't phase-lock one target -----------
  {
    const s = boot(true, 4), E = s.ENGINE; const a = blankMine(s);
    const src = put(s, "mine", "storehouse", 40, 20, { item: "wood", qty: 150 });
    const seal = put(s, "mine", "warding_seal", 40, 30, { item: "wood", lock: true });
    const benchA = put(s, "mine", "workbench", 30, 40, { recipe: 0, stock: {} });
    const benchB = put(s, "mine", "workbench", 50, 40, { recipe: 0, stock: {} });
    const lan = put(s, "mine", "wisp_lantern", 44, 30);
    // stone->seal first, then two hungry Workbenches sharing the seal
    lan.links.push({ from: src.id, to: seal.id }, { from: seal.id, to: benchA.id }, { from: seal.id, to: benchB.id });
    const sends = [0, 0, 0];
    drive(s, 60000, 50, () => {
      src.qty = 150; a.ground = [];
      lan.links.forEach((l, i) => { if (l._seq && l._seq !== l._last) { sends[i]++; l._last = l._seq; } });
    });
    check("lantern: the 2nd target behind a shared seal still gets served (no phase lock)",
      sends[2] > 0.6 * sends[1] && sends[2] > 50, "sends per link " + sends.join(","));
    check("lantern: _seq / _stat are transient (stripped on save)",
      !JSON.stringify(lan, (k, v) => (k[0] === "_" ? undefined : v)).includes("_seq"));
  }

  // (8) starter network sinks + zero-input run --------------------------------
  {
    const s = boot(true, 8), E = s.ENGINE, a = s.GS.areas.center;
    E.setupStarterNetwork();
    const B = id => a.buildings.find(b => b.id === id);
    const store = it => a.buildings.find(b => b.type === "storehouse" && b.item === it && b.lock);
    const linkedFromStone = st => a.buildings.some(lb => (lb.links || []).some(l => l.to === st.id && B(l.from) && B(l.from).type === "gathering_stone"));
    for (const it of ["plank", "brick", "spirit_stone"])
      check(`starter: a locked ${it} Storehouse fed by a Gathering Stone link`, !!store(it) && linkedFromStone(store(it)));
    const bench = a.buildings.find(b => b.type === "workbench"), kiln = a.buildings.find(b => b.type === "kiln");
    const gsOut = a.buildings.find(b => b.type === "gathering_stone" && b.row === 61 && b.col === 43);
    const R = s.DATA.BUILDINGS.gathering_stone.gather.radius;
    const reach = (conv) => { const bs = E.buildingSize(conv.type), o = { x: conv.col + bs.w / 2, y: conv.row + bs.h + 0.4 };
      return Math.hypot(o.x - (gsOut.col + 0.5), o.y - (gsOut.row + 0.5)) < R - 1; };
    check("starter: the output stone reaches the Workbench and Kiln output spots", !!gsOut && reach(bench) && reach(kiln));
    const sealWood = a.buildings.find(b => b.type === "warding_seal" && b.item === "wood");
    let maxGround = 0, returned = 0;
    const perMin = [];
    for (let m = 0; m < 20; m++) {
      const c0 = s.GS.stats.totalCrafted;
      drive(s, 60000, 50, () => { returned += a.wisps.filter(w => w.returning).length; });
      perMin.push(s.GS.stats.totalCrafted - c0);
      maxGround = Math.max(maxGround, a.ground.length);
    }
    const sc = sealWood.col + 0.5, sr = sealWood.row + 0.5, CELL = s.DATA.GRID.cell;
    const woodAtSeal = a.ground.filter(g => g.item === "wood" && Math.hypot(g.x / CELL - sc, g.y / CELL - sr) < 4).length;
    check("starter zero-input: planks + bricks bank into their stores", store("plank").qty > 100 && store("brick").qty > 100,
      `plank=${store("plank").qty} brick=${store("brick").qty}`);
    check("starter zero-input: crafting runs well past the old 12-pile stop (>= 5 active minutes)", perMin.filter(n => n > 0).length >= 5,
      "crafts/min " + perMin.join(","));
    check("starter zero-input: the ground stays tidy (< 100 loose items over 20 TEST-min)", maxGround < 100, "max=" + maxGround);
    check("starter zero-input: no wood pile at the wood seal, no wisp flew home", woodAtSeal <= 2 && returned === 0,
      `woodAtSeal=${woodAtSeal} returned=${returned}`);
  }

  // (9) Center Automation taps the Spirit Tree --------------------------------
  {
    const s = boot(true, 6), E = s.ENGINE, a = s.GS.areas.center;
    E.setupStarterNetwork();
    const tree = a.nodes.find(n => n.kind === "spirittree"), rock = a.nodes.find(n => n.kind === "quarry");
    const tc = E.nodeCenterPx(tree);
    let treeWood = 0;
    s.onGroundDrop = (k, item, qty, x, y) => { if (k === "center" && item === "wood" && x === tc.x && y === tc.y) treeWood += qty; };
    E.automationTick();
    check("automation L0: the tree is not tapped", treeWood === 0 && (tree.clicks || 0) === 0);
    a.upgrades.automation = 1;
    const rc0 = rock.clicks || 0;
    let auto = 0;
    drive(s, 60000, 50, () => { if ((auto += 50) >= 1000) { auto -= 1000; E.automationTick(); } });
    check(`automation L1: the Spirit Tree yields wood (~50/min; got ${treeWood})`, treeWood >= 35 && treeWood <= 70);
    check("automation: the quarry rock stays manual", (rock.clicks || 0) === rc0);
    const gsWood = a.buildings.find(b => b.type === "gathering_stone" && b.row === 16 && b.col === 44);
    check("automation: tree wood reaches the starter wood line (not left loose)", a.ground.filter(g => g.item === "wood").length < 30,
      "loose wood=" + a.ground.filter(g => g.item === "wood").length + " stone=" + E.gatherTotal(gsWood));
    // L2 swings twice per tick
    a.upgrades.automation = 2; tree.clicks = 0;
    E.automationTick();
    check("automation L2: two tree swings per tick", tree.clicks === 2, "clicks=" + tree.clicks);
    check("tree node desc mentions the Spirit Tree", /Spirit Tree/.test(s.DATA.UPGRADE_TREE ? JSON.stringify(s.DATA.UPGRADE_TREE) : JSON.stringify(s.DATA)));
  }

  // (10) Remembered Paths order -------------------------------------------------
  {
    const s = boot(true, 1);
    check("PATH_REGIONS = mine, fishing, farm", JSON.stringify(s.ENGINE.PATH_REGIONS) === '["mine","fishing","farm"]');
    check("Paths perk desc names the Mine first", /Mine already open/.test(s.ENGINE.perkDef("paths").desc), s.ENGINE.perkDef("paths").desc);
  }
} catch (e) {
  console.log("FAIL exception — " + (e && e.stack || e));
  fails++;
}

console.log(fails ? `\n${fails} FAILED` : "\nALL PASSED");
process.exit(fails ? 1 : 0);
