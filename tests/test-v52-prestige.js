#!/usr/bin/env node
/* Regression: v52 slice B — prestige that shortens runs + reasons for run 2..N.
   Additive prestige curve, prestige on the clocks players wait on (fields,
   generator buildings, fox respawn, dragon scales, manual swings), per-slot
   fox respawn, gate offerings + AP formula, vows (mult, marks, hooks), legacy
   perks applied by ascend(), justAscended, save migration.
   Fix wave: legacy perks apply at buy time, due-time timer re-arm (coarse
   vs fine ticks agree), unique gate + per-type offerings + demolish refund,
   ascension-shrunk dragon tributes, region-unlock installments, config-
   owned node fields refreshed on load.
   Standalone vm sandbox — loads js/data.js, js/state.js, js/engine.js with a
   controllable clock.
   Usage: node tests/test-v52-prestige.js [repoRoot]   (default /home/user/idle-grounds)
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
const near = (a, b, tol) => Math.abs(a - b) <= tol;

// boot(saved): saved = JSON string or object served from localStorage
// (undefined = fresh game). The sandbox gets a fake clock (s.clock.t) and a
// localStorage that remembers writes (s.stored).
function boot(saved) {
  const s = {}; s.window = s; s.console = console; s.Math = Math; s.JSON = JSON;
  const clock = { t: 1.7e12 };
  function FakeDate(...a) { return a.length ? new Date(...a) : new Date(clock.t); }
  FakeDate.now = () => clock.t;
  s.Date = FakeDate; s.clock = clock;
  s.location = { reload() {} };
  s.stored = saved === undefined ? null : (typeof saved === "string" ? saved : JSON.stringify(saved));
  s.localStorage = {
    getItem: () => s.stored,
    setItem: (k, v) => { s.stored = v; }, removeItem: () => { s.stored = null; },
  };
  vm.createContext(s);
  vm.runInContext(fs.readFileSync(JS("data.js"), "utf8"), s, { filename: "data.js" });
  for (const f of ["state.js", "engine.js"])
    vm.runInContext(fs.readFileSync(JS(f), "utf8"), s, { filename: f });
  s.onGroundDrop = null; s.onSfx = null;
  return s;
}
const initAll = s => { for (const k of Object.keys(s.DATA.AREAS)) s.ENGINE.initArea(k); };
const unlockAll = s => { for (const k of Object.keys(s.GS.world.unlocked)) s.GS.world.unlocked[k] = true; };
const scaleOf = s => s.DATA.TEST.ENABLED ? s.DATA.TEST.timeScale : 1;
// a plain (un-blessed, perk-less) world at `asc` ascensions
function world(asc) {
  const s = boot(); initAll(s);
  s.GS.ascensions = asc; s.GS.won = false; s.GS.dragonBlessed = false; s.GS.perks = {};
  return s;
}

try {
  // (1) prestige factor curve: 1 / (1 + 0.2 * asc) --------------------------
  {
    for (const [asc, want] of [[0, 1], [1, 1 / 1.2], [2, 1 / 1.4], [5, 0.5]]) {
      const f = world(asc).ENGINE.prestigeFactor();
      check(`prestigeFactor @asc=${asc} ~ ${want.toFixed(4)}`, near(f, want, 1e-9), f.toFixed(4));
    }
    const s = world(1); s.GS.perks = { haste: 2 }; s.GS.dragonBlessed = true;
    const f = s.ENGINE.prestigeFactor();
    check("haste x0.95^n and blessing x0.9 still stack", near(f, (1 / 1.2) * 0.95 * 0.95 * 0.9, 1e-9), f.toFixed(4));
  }

  // (2) each newly-scaled clock shortens at asc 1 ----------------------------
  {
    const R = 1 / 1.2;
    // field generators (center's first generator)
    const gen = asc => {
      const s = world(asc), a = s.GS.areas.center, t = s.clock.t;
      a.genTimers = [0]; a.ground = [];
      s.ENGINE.gameTick();
      return a.genTimers[0] - t;
    };
    const g0 = gen(0), g1 = gen(1);
    check("field generator interval x1/1.2 at asc 1", g0 > 0 && near(g1 / g0, R, 1e-6), `${g0} -> ${g1}`);

    // generator building (Algae Farm in fishing)
    const gb = asc => {
      const s = world(asc); s.GS.world.unlocked.fishing = true;
      const a = s.GS.areas.fishing, t = s.clock.t;
      const b = { id: 9001, type: "algae_farm", row: 40, col: 40, paid: {}, built: true, nextGen: 0 };
      a.buildings.push(b);
      s.ENGINE.gameTick();
      return b.nextGen - t;
    };
    const b0 = gb(0), b1 = gb(1);
    check("generator building interval x1/1.2 at asc 1", b0 > 0 && near(b1 / b0, R, 1e-6), `${b0} -> ${b1}`);

    // fox respawn (kill -> this slot's clock)
    const fox = asc => {
      const s = world(asc), a = s.GS.areas.center;
      s.ENGINE.gameTick();                                   // fills the empty slot
      const en = a.enemies.find(e => e.kind !== "boss");
      if (!en) return NaN;
      en.hp = 1; s.ENGINE.attackEnemy("center", en.id);
      return a.enemyRespawns[0] - s.clock.t;
    };
    const f0 = fox(0), f1 = fox(1);
    check("fox respawn x1/1.2 at asc 1", f0 > 0 && near(f1 / f0, R, 1e-6), `${f0} -> ${f1}`);

    // awakened dragon's scale shedding
    const scale = asc => {
      const s = world(asc);
      s.GS.dragon.stage = s.DATA.DRAGON_STAGES.length; s.GS.dragonScaleAt = 0;
      s.ENGINE.gameTick();
      return s.GS.dragonScaleAt - s.clock.t;
    };
    const d0 = scale(0), d1 = scale(1);
    // (awakened => won; compare with the blessing off in both runs)
    check("dragon scale interval x1/1.2 at asc 1", d0 > 0 && near(d1 / d0, R, 1e-6), `${d0} -> ${d1}`);

    // manual swings + fixture swings (floor 120ms)
    const s0 = world(0), s1 = world(1), s5 = world(20);
    const bush = { swingMs: 300 }, rock = { swingMs: 1000 };
    const h0 = s0.ENGINE.harvestInterval("center", bush), h1 = s1.ENGINE.harvestInterval("center", bush);
    check("harvestInterval x1/1.2 at asc 1", near(h1 / h0, R, 1e-6), `${h0} -> ${h1}`);
    const r0 = s0.ENGINE.harvestInterval("center", rock), r1 = s1.ENGINE.harvestInterval("center", rock);
    check("fixture swing interval x1/1.2 at asc 1", near(r1 / r0, R, 1e-6), `${r0} -> ${r1}`);
    check("harvestInterval floor 120ms", s5.ENGINE.harvestInterval("center", bush) === 120,
      String(s5.ENGINE.harvestInterval("center", bush)));
  }

  // (3) per-slot fox respawn: cap 3 ~ 3x throughput; cap 1 unchanged --------
  {
    const kills = (capBonus, ms) => {
      const s = world(0), a = s.GS.areas.center;
      a.upgrades.enemyCap = capBonus;
      let n = 0;
      const end = s.clock.t + ms;
      while (s.clock.t < end) {
        s.ENGINE.gameTick();
        for (const en of a.enemies.filter(e => e.kind !== "boss")) { en.hp = 1; s.ENGINE.attackEnemy("center", en.id); n++; }
        s.clock.t += 50;
      }
      return n;
    };
    const k1 = kills(0, 60000), k3 = kills(2, 60000);
    check("cap 3 kills ~3x cap 1 over 60s", k1 > 0 && near(k3 / k1, 3, 0.35), `cap1=${k1} cap3=${k3}`);

    // cap 1: after a kill at t the fox returns exactly one interval later
    const s = world(0), a = s.GS.areas.center, E = s.ENGINE;
    const iv = s.DATA.AREAS.center.enemies.respawnMs * scaleOf(s);
    E.gameTick();
    check("cap 1: fresh area spawns its fox at once", a.enemies.length === 1, String(a.enemies.length));
    const t0 = s.clock.t;
    a.enemies[0].hp = 1; E.attackEnemy("center", a.enemies[0].id);
    s.clock.t = t0 + iv - 50; E.gameTick();
    const early = a.enemies.length;
    s.clock.t = t0 + iv; E.gameTick();
    check("cap 1: respawn exactly one interval after the kill", early === 0 && a.enemies.length === 1,
      `before=${early} at=${a.enemies.length}`);
    // Spirit Call adds a slot that fills at once
    a.upgrades.enemyCap = 1; E.gameTick();
    check("raising the cap fills the new slot at once", a.enemies.length === 2, String(a.enemies.length));
  }

  // (4) offerings cap + AP formula -------------------------------------------
  {
    const s = world(0), E = s.ENGINE, GS = s.GS;
    unlockAll(s);
    check("AP with no gate, all regions === 15", E.ascendReward() === 15, String(E.ascendReward()));
    const gate = { id: 9100, type: "ascension_gate", row: 30, col: 62, paid: {}, built: true };
    GS.areas.center.buildings.push(gate);
    const c = E.buildingCenterPx(gate);
    check("test gate spot is free (buildingAt finds the gate)",
      E.buildingAt("center", Math.floor(c.y / 32), Math.floor(c.x / 32)) === gate, "");
    // scales alone stop at 2 of 6 (per-type cap 2)
    GS.hand = [{ item: "dragon_scale", qty: 5 }];
    let fedS = 0;
    for (let i = 0; i < 6; i++) { const r = E.dropFromHand("center", c.x, c.y); if (r && r.fed) fedS++; }
    check("gate: scales alone stop at 2 (per-type cap)", fedS === 2 && E.gateOfferings("center", gate).count === 2
      && E.handCount("dragon_scale") === 3, `fed=${fedS} left=${E.handCount("dragon_scale")}`);
    GS.hand = [{ item: "wood", qty: 2 }, { item: "talisman", qty: 5 }, { item: "star_steel", qty: 3 }, { item: "dragon_scale", qty: 3 }];
    const r0 = E.dropFromHand("center", c.x, c.y);
    check("gate: carried offering reordered to the front", r0 && r0.reordered === "talisman", JSON.stringify(r0));
    let fed = 0;
    for (let i = 0; i < 12; i++) { const r = E.dropFromHand("center", c.x, c.y); if (r && r.fed) fed++; }
    const off = E.gateOfferings("center", gate);
    check("gate offerings capped at 6 = 2 of each", fed === 4 && off.count === 6 && off.cap === 6 && gate.offerings === 6
      && JSON.stringify(gate.offered) === '{"dragon_scale":2,"talisman":2,"star_steel":2}',
      `fed=${fed} ${JSON.stringify(off)}`);
    check("capped gate keeps the surplus offerings in hand",
      E.handCount("talisman") === 3 && E.handCount("star_steel") === 1 && E.handCount("dragon_scale") === 3,
      `t=${E.handCount("talisman")} s=${E.handCount("star_steel")} d=${E.handCount("dragon_scale")}`);
    check("AP = 3 + 2*(regions-1) + offerings === 21", E.ascendReward() === 21, String(E.ascendReward()));
    GS.perks = { apgain: 1 };
    check("AP + apgain 1 === 22", E.ascendReward() === 22, String(E.ascendReward()));
    GS.vows.active = ["burden", "restless"];
    check("AP x vowMult(2 vows) === round(22*1.3) = 29", E.ascendReward() === 29, String(E.ascendReward()));
    // non-offering items still drop on the ground at a built gate
    GS.vows.active = []; GS.hand = [{ item: "wood", qty: 1 }];
    const rw = E.dropFromHand("center", c.x, c.y);
    check("gate: non-offering item drops on the ground", rw && rw.dropped === "wood", JSON.stringify(rw));
  }

  // (5) vowMult + marks --------------------------------------------------------
  {
    const s = world(1), E = s.ENGINE, GS = s.GS, ids = s.DATA.VOWS.map(v => v.id);
    check("4 vows: burden, coldhearth, restless, solitude", ids.join(",") === "burden,coldhearth,restless,solitude", ids.join(","));
    const want = [1, 1.15, 1.3, 1.5, 1.75], got = [];
    for (let n = 0; n <= 4; n++) { GS.vows.active = ids.slice(0, n); got.push(E.vowMult()); }
    check("vowMult by count === [1,1.15,1.3,1.5,1.75]", JSON.stringify(got) === JSON.stringify(want), JSON.stringify(got));
    GS.vows.active = []; const base = E.prestigeFactor();
    GS.vows.done = { burden: 1, restless: 3 };
    check("2 marks => prestigeFactor x0.96^2", near(E.prestigeFactor() / base, 0.96 * 0.96, 1e-9),
      (E.prestigeFactor() / base).toFixed(4));
    GS.vows.active = ["coldhearth"];
    check("nextPrestigeFactor counts active vows as marks + one more ascension",
      near(E.nextPrestigeFactor(), (1 / 1.4) * Math.pow(0.96, 3), 1e-9), E.nextPrestigeFactor().toFixed(4));
  }

  // (6) vow hooks ----------------------------------------------------------------
  {
    // burden: hand capacity halved
    const s = world(1), E = s.ENGINE, GS = s.GS;
    const capBefore = E.handCap();
    GS.vows.active = ["burden"];
    check("burden: handCap() halved", E.handCap() === Math.floor(GS.handCap / 2) && capBefore === GS.handCap,
      `${capBefore} -> ${E.handCap()}`);
    GS.hand = [];
    const added = E.handAdd("wood", 100);
    check("burden: handAdd stops at the halved cap", added === Math.floor(GS.handCap / 2), String(added));

    // coldhearth: burners consume fuel 2x
    const burn = vows => {
      GS.vows.active = vows;
      const b = { fuelQ: [{ item: "wood", rem: 10000, total: 10000 }] };
      E.burnFuel(b, 1000);
      return 10000 - b.fuelQ[0].rem;
    };
    const n0 = burn([]), n2 = burn(["coldhearth"]);
    check("coldhearth: fuel burns 2x", n0 === 1000 && n2 === 2000, `${n0} -> ${n2}`);

    // restless: dragon tributes x2
    GS.vows.active = []; GS.dragon.stage = 0; GS.dragon.paid = {};
    const t0 = E.dragonRemaining();
    GS.vows.active = ["restless"];
    const t2 = E.dragonRemaining();
    const ok = Object.keys(t0).length > 0 && Object.keys(t0).every(k => t2[k] === 2 * t0[k]);
    check("restless: each dragon tribute doubled", ok, `${JSON.stringify(t0)} -> ${JSON.stringify(t2)}`);
  }
  {
    // solitude: the next run gets no starter network; vows chosen => next run
    const s = world(1), E = s.ENGINE;
    unlockAll(s);
    E.ascend(["solitude", "burden", "bogus", "burden"]);
    const GS = s.GS;
    check("ascend(nextVows): active = known, deduped", JSON.stringify(GS.vows.active) === '["solitude","burden"]',
      JSON.stringify(GS.vows.active));
    check("solitude: starterPlaced pre-set on the fresh run", GS.starterPlaced === true, String(GS.starterPlaced));
    initAll(s);
    const placed = E.setupStarterNetwork();
    const n = GS.areas.center.buildings.filter(b => b.type === "wisp_lantern").length;
    check("solitude: setupStarterNetwork places nothing", placed === false && n === 0, `placed=${placed} lanterns=${n}`);
    // completing the run with those vows: AP x1.3, done counts, marks
    unlockAll(s);
    const ap0 = GS.ascendPoints, reward = E.ascendReward();
    check("ascending with 2 vows kept: reward === round(15*1.3)=20", reward === 20, String(reward));
    E.ascend([]);
    check("ascend: AP added with the vow multiplier", s.GS.ascendPoints === ap0 + 20, `${ap0} -> ${s.GS.ascendPoints}`);
    check("ascend: vows.done incremented, active cleared",
      s.GS.vows.done.solitude === 1 && s.GS.vows.done.burden === 1 && s.GS.vows.active.length === 0,
      JSON.stringify(s.GS.vows));
    check("ascend: 2 marks folded into prestigeFactor",
      near(E.prestigeFactor(), (1 / 1.6) * 0.9 ** (s.GS.dragonBlessed ? 1 : 0) * 0.96 * 0.96, 1e-9), E.prestigeFactor().toFixed(4));
    // no vows can be taken on the FIRST ascension
    const f = world(0); f.ENGINE.ascend(["burden"]);
    check("first ascension ignores vow choices", f.GS.vows.active.length === 0, JSON.stringify(f.GS.vows.active));
  }

  // (7) legacy perks applied in ascend() -------------------------------------
  {
    const s = world(1); s.GS.perks = { paths: 2, legacy: 3 };
    s.ENGINE.ascend([]);
    const GS = s.GS, u = GS.world.unlocked;
    check("Remembered Paths 2: mine + fishing open, farm closed (dragon path first)", u.mine && u.fishing && !u.farm, JSON.stringify(u));
    check("Legacy Automation 3: center/farm/mine automation 1",
      ["center", "farm", "mine"].every(k => GS.areas[k].upgrades.automation === 1) && GS.areas.fishing.upgrades.automation === 0,
      ["center", "farm", "mine", "fishing"].map(k => GS.areas[k].upgrades.automation).join(","));
    const p1 = world(1); p1.GS.perks = { paths: 1, legacy: 1 }; p1.ENGINE.ascend([]);
    check("paths 1 / legacy 1: mine open + center auto only",
      p1.GS.world.unlocked.mine && !p1.GS.world.unlocked.fishing && !p1.GS.world.unlocked.farm && p1.GS.areas.center.upgrades.automation === 1
        && p1.GS.areas.farm.upgrades.automation === 0, "");
    const none = world(1); none.ENGINE.ascend([]);
    check("no legacy perks: regions closed, automation 0",
      !none.GS.world.unlocked.farm && none.GS.areas.center.upgrades.automation === 0, "");
    check("perk defs: paths [3,6,12], legacy [4,8,16]",
      JSON.stringify(s.ENGINE.perkDef("paths").cost) === "[3,6,12]" && JSON.stringify(s.ENGINE.perkDef("legacy").cost) === "[4,8,16]", "");
  }

  // (8) justAscended set / persisted / cleared -------------------------------
  {
    const s = world(0); unlockAll(s);
    s.ENGINE.ascend();
    const ja = s.GS.justAscended;
    check("ascend() sets justAscended {n, ap, speedFrom, speedTo}",
      ja && ja.n === 1 && ja.ap === 15 && near(ja.speedFrom, 1, 1e-9) && near(ja.speedTo, 1.2, 1e-9), JSON.stringify(ja));
    const r = boot(s.stored);
    check("justAscended survives the save/reload", r.GS.justAscended && r.GS.justAscended.n === 1,
      JSON.stringify(r.GS.justAscended));
    r.GS.justAscended = null; r.SAVE.saveState();
    const r2 = boot(r.stored);
    check("cleared justAscended stays cleared", r2.GS.justAscended === null, JSON.stringify(r2.GS.justAscended));
  }

  // (9) migration: pre-vow saves and v51 saves -------------------------------
  {
    const f = boot(); initAll(f);
    check("fresh state: vows {active:[],done:{}}, justAscended null",
      JSON.stringify(f.GS.vows) === '{"active":[],"done":{}}' && f.GS.justAscended === null, JSON.stringify(f.GS.vows));
    // v51-shaped save: no vows / justAscended / enemyRespawns; shared enemyRespawnAt
    const v51 = JSON.parse(JSON.stringify(f.GS));
    delete v51.vows; delete v51.justAscended;
    for (const k of Object.keys(v51.areas)) { delete v51.areas[k].enemyRespawns; v51.areas[k].enemyRespawnAt = 0; }
    v51.areas.center.enemyRespawnAt = f.clock.t + 4000;
    v51.ascensions = 2; v51.perks = { haste: 1 };
    const m = boot(v51);
    check("v51 save loads with default vows", JSON.stringify(m.GS.vows) === '{"active":[],"done":{}}', JSON.stringify(m.GS.vows));
    check("v51 save: justAscended null", m.GS.justAscended === null, String(m.GS.justAscended));
    check("v51 save: shared enemyRespawnAt -> one slot clock",
      JSON.stringify(m.GS.areas.center.enemyRespawns) === JSON.stringify([f.clock.t + 4000])
        && m.GS.areas.center.enemyRespawnAt === undefined && m.GS.areas.farm.enemyRespawns.length === 0,
      JSON.stringify(m.GS.areas.center.enemyRespawns));
    check("v51 save: ascensions/perks kept", m.GS.ascensions === 2 && m.GS.perks.haste === 1, "");
    initAll(m); let threw = null;
    try { for (let i = 0; i < 20; i++) { m.ENGINE.gameTick(); m.clock.t += 50; } } catch (e) { threw = e.message; }
    check("v51 save ticks cleanly", threw === null, threw || "ok");
    // corrupt vows / offerings scrubbed; idempotent re-load
    const bad = JSON.parse(JSON.stringify(f.GS));
    bad.vows = { active: ["burden", "nope", "burden", 7], done: { restless: 2, nope: 1, burden: NaN, solitude: -1 } };
    bad.areas.center.buildings.push({ id: 9200, type: "ascension_gate", row: 10, col: 10, paid: {}, built: true, offerings: 99 });
    bad.justAscended = { n: "x" };
    const c = boot(bad);
    check("corrupt vows scrubbed", JSON.stringify(c.GS.vows) === '{"active":["burden"],"done":{"restless":2}}', JSON.stringify(c.GS.vows));
    const g = c.GS.areas.center.buildings.find(b => b.id === 9200);
    check("gate offerings clamped to cap", g && g.offerings === 6, g && String(g.offerings));
    check("corrupt justAscended dropped", c.GS.justAscended === null, JSON.stringify(c.GS.justAscended));
    c.SAVE.saveState();
    const c2 = boot(c.stored);
    check("migration idempotent (vows)", JSON.stringify(c2.GS.vows) === JSON.stringify(c.GS.vows), JSON.stringify(c2.GS.vows));
  }

  // (10) legacy perks apply at buy time ----------------------------------------
  {
    const s = world(1), E = s.ENGINE, GS = s.GS;
    GS.ascendPoints = 100;
    GS.world.unlockPaid = { fishing: { wood: 3 } };
    GS.hand = [];
    check("paths L1 bought: mine opens now (dragon path first)", E.buyPerk("paths") && GS.world.unlocked.mine && !GS.world.unlocked.fishing,
      JSON.stringify(GS.world.unlocked));
    check("paths L2 bought: fishing opens now", E.buyPerk("paths") && GS.world.unlocked.fishing, "");
    check("paths L2: fishing installments refunded to the hand, entry cleared",
      E.handCount("wood") === 3 && !GS.world.unlockPaid.fishing, `wood=${E.handCount("wood")}`);
    GS.areas.farm.upgrades.automation = 2;
    check("legacy L1 bought: center automation 1 now", E.buyPerk("legacy") && GS.areas.center.upgrades.automation === 1, "");
    check("legacy L2 bought: farm keeps its higher level (max(cur,1))",
      E.buyPerk("legacy") && GS.areas.farm.upgrades.automation === 2 && GS.areas.mine.upgrades.automation === 0,
      ["center", "farm", "mine"].map(k => GS.areas[k].upgrades.automation).join(","));
    check("legacy L3 bought: mine automation 1 now", E.buyPerk("legacy") && GS.areas.mine.upgrades.automation === 1, "");
    GS.world.unlocked.farm = true;
    const ap = GS.ascendPoints;
    check("paths L3 on an already-open region: just the level", E.buyPerk("paths") && GS.perks.paths === 3
      && GS.ascendPoints === ap - 12, `ap ${ap} -> ${GS.ascendPoints}`);
  }

  // (11) timers re-arm from the DUE time: coarse ticks == fine ticks ------------
  {
    // run `ms` of world time at a fixed tick `step`; return event counts per clock
    const run = (step, ms, asc) => {
      const s = world(asc), E = s.ENGINE, GS = s.GS, CELL = s.DATA.GRID.cell;
      GS.world.unlocked.fishing = true; GS.world.unlocked.mine = true;
      const tally = {};
      s.onGroundDrop = (k, item, qty) => { tally[k + ":" + item] = (tally[k + ":" + item] || 0) + qty; };
      const fish = GS.areas.fishing, mine = GS.areas.mine;
      fish.buildings.push({ id: 9301, type: "algae_farm", row: 40, col: 40, paid: {}, built: true, nextGen: 0 });
      mine.nodes = []; mine.buildings = []; mine.genTimers = [];
      const src = { id: 9401, type: "storehouse", row: 40, col: 30, paid: {}, built: true, item: "wood", qty: 150 };
      const dst = { id: 9402, type: "storehouse", row: 40, col: 50, paid: {}, built: true, item: "wood", qty: 0, lock: true };
      const lan = { id: 9403, type: "wisp_lantern", row: 44, col: 40, paid: {}, built: true, links: [{ from: 9401, to: 9402 }], connIdx: 0, nextSend: 0 };
      const pav = { id: 9404, type: "meditation_pavilion", row: 30, col: 40, paid: {}, built: true, disciples: 2, buns: 1e6, nextCultivate: 0 };
      mine.buildings.push(src, dst, lan, pav);
      const w0 = mine.nextWispId;
      const end = s.clock.t + ms;
      while (s.clock.t < end) {
        E.gameTick();
        for (const k of Object.keys(GS.areas)) GS.areas[k].ground = [];   // no field caps bind
        src.qty = 150; dst.qty = 0;
        s.clock.t += step;
      }
      return { clay: tally["center:clay"] || 0, stone: tally["center:stone"] || 0, algae: tally["fishing:algae"] || 0,
               beats: mine.nextWispId - w0, cycles: 1e6 - pav.buns };
    };
    for (const asc of [0, 3]) {
      const fine = run(50, 60000, asc), coarse = run(640, 60000, asc);
      for (const k of ["clay", "stone", "algae", "beats", "cycles"]) {
        // equal up to the last partial coarse step (one step's worth of events)
        const slack = k === "beats" ? 4 : 2;
        check(`timer re-arm @asc=${asc}: ${k} at 640ms steps ~ 50ms steps`, fine[k] > 0 && Math.abs(fine[k] - coarse[k]) <= slack,
          `fine=${fine[k]} coarse=${coarse[k]}`);
      }
    }
    // a clock that wasn't running (region just opened) restarts, no burst
    const s = world(0), a = s.GS.areas.center, E = s.ENGINE;
    let n = 0; s.onGroundDrop = (k, item) => { if (k === "center" && item === "clay") n++; };
    a.ground = []; a.genTimers = [s.clock.t - 600000];
    E.gameTick();
    check("stale generator clock fires once (no catch-up burst)", n === 1, String(n));
  }

  // (12) unique gate + demolish refund -----------------------------------------
  {
    const s = world(0), E = s.ENGINE, GS = s.GS;
    const a = GS.areas.center;
    GS.stats = GS.stats || {};
    const type = "ascension_gate";
    s.DATA.BUILDINGS[type].unlocked = true;
    const origUnlocked = E.isBuildingUnlocked;
    // find a free spot for a gate ghost
    let spot = null;
    for (let r = 0; r < 90 && !spot; r++) for (let c = 0; c < 90 && !spot; c++)
      if (E.canPlaceBuilding("center", r, c, type)) spot = [r, c];
    check("a gate can be placed while none exists", !!spot, JSON.stringify(spot));
    const ghost = { id: 9500, type, row: spot[0], col: spot[1], paid: {}, built: false };
    a.buildings.push(ghost);
    let second = false;
    for (const k of ["center", "farm"])
      for (let r = 0; r < 90 && !second; r++) for (let c = 0; c < 90 && !second; c++)
        if (E.canPlaceBuilding(k, r, c, type)) second = true;
    check("a second gate can't be placed anywhere (ghost exists)", !second && E.gateExists(), "");
    // built gate with offerings: demolish refunds cost + offerings
    ghost.built = true; ghost.offered = { talisman: 2, dragon_scale: 1 }; ghost.offerings = 3;
    a.ground = [];
    E.demolishBuilding("center", 9500);
    const cnt = it => a.ground.filter(g => g.item === it).length;
    const cost = s.DATA.BUILDINGS[type].cost;
    check("demolished gate refunds its offerings (+ cost)",
      cnt("talisman") === cost.talisman + 2 && cnt("dragon_scale") === cost.dragon_scale + 1 && cnt("star_steel") === cost.star_steel,
      `t=${cnt("talisman")} d=${cnt("dragon_scale")} s=${cnt("star_steel")}`);
    check("gate can be placed again after demolish", !E.gateExists() && E.canPlaceBuilding("center", spot[0], spot[1], type), "");
  }

  // (13) dragon tributes shrink with ascensions ----------------------------------
  {
    const s = world(0), E = s.ENGINE;
    const tm = [0, 1, 2, 4, 10].map(a => +E.tributeMult(a).toFixed(4));
    check("tributeMult = 1/(1+0.25a), floor 0.4", JSON.stringify(tm) === "[1,0.8,0.6667,0.5,0.4]", JSON.stringify(tm));
    const needs = asc => { const w = world(asc); w.GS.dragon.stage = 0; w.GS.dragon.paid = {}; return w.ENGINE.dragonRemaining(); };
    const n0 = needs(0), n2 = needs(2), st = s.DATA.DRAGON_STAGES[0].needs;
    const sc = q => Math.max(1, Math.ceil(s.DATA.TEST.ENABLED ? q * s.DATA.TEST.costScale : q));
    const ok = Object.keys(st).every(k => n0[k] === sc(st[k]) && n2[k] === Math.max(1, Math.ceil(sc(st[k]) / 1.5)));
    check("asc 2: stage tribute x0.667 (ceil, min 1)", ok, `${JSON.stringify(n0)} -> ${JSON.stringify(n2)}`);
    const r = world(2); r.GS.vows.active = ["restless"]; r.GS.dragon.stage = 0; r.GS.dragon.paid = {};
    const nr = r.ENGINE.dragonRemaining();
    check("restless doubles the shrunk tribute", Object.keys(n2).every(k => nr[k] === 2 * n2[k]), JSON.stringify(nr));
  }

  // (14) region unlock installments (real balance + Vow of Burden) --------------
  {
    const s = world(1), E = s.ENGINE, GS = s.GS;
    s.DATA.TEST.ENABLED = false;
    GS.vows.active = ["burden"];
    const cap = E.handCap();
    const cost = E.areaUnlockCost("mine");
    const total = Object.values(cost).reduce((x, y) => x + y, 0);
    check("burden at real balance: mine costs more than the hand holds", total > cap, `cost=${total} cap=${cap}`);
    GS.hand = [];
    E.handAdd("wood", cap);
    const r1 = E.unlockArea("mine");
    check("partial payment returns {paid}, remembers it", r1 && r1.paid === Math.min(cap, cost.wood || 0)
      && GS.world.unlockPaid.mine && !GS.world.unlocked.mine, JSON.stringify(r1) + " " + JSON.stringify(GS.world.unlockPaid));
    check("nothing to pay -> false", E.unlockArea("mine") === false, "");
    // survives save/reload
    s.SAVE.saveState();
    const r = boot(s.stored);
    check("installments survive a reload", JSON.stringify(r.GS.world.unlockPaid) === JSON.stringify(GS.world.unlockPaid),
      JSON.stringify(r.GS.world.unlockPaid));
    let clicks = 1, res = r1;
    while (res !== true && clicks < 20) {
      for (const [it, q] of Object.entries(E.unlockRemaining("mine"))) E.handAdd(it, Math.min(q, E.handSpace()));
      res = E.unlockArea("mine"); clicks++;
    }
    check("burden: mine opens after a few installments", res === true && GS.world.unlocked.mine && !GS.world.unlockPaid.mine,
      `clicks=${clicks}`);
    check("fresh state: world.unlockPaid {}", JSON.stringify(world(0).GS.world.unlockPaid) === "{}", "");
    // migration scrub + idempotence
    const f = world(0);
    const bad = JSON.parse(JSON.stringify(f.GS));
    bad.world.unlockPaid = { farm: { wood: 4, bogus_item: 3, clay: NaN }, center: { wood: 2 }, nowhere: { wood: 1 }, mine: "x", fishing: {} };
    const m = boot(bad);
    check("unlockPaid scrubbed (dead items, open/unknown regions, junk)",
      JSON.stringify(m.GS.world.unlockPaid) === '{"farm":{"wood":4}}', JSON.stringify(m.GS.world.unlockPaid));
    m.SAVE.saveState();
    const m2 = boot(m.stored);
    check("unlockPaid migration idempotent", JSON.stringify(m2.GS.world.unlockPaid) === '{"farm":{"wood":4}}', "");
    const pre = JSON.parse(JSON.stringify(f.GS)); delete pre.world.unlockPaid;
    check("pre-installment save: unlockPaid {}", JSON.stringify(boot(pre).GS.world.unlockPaid) === "{}", "");
    // ascension resets installments
    const a = world(1); a.GS.world.unlockPaid = { farm: { wood: 3 } }; a.ENGINE.ascend([]);
    check("ascend: installments reset", JSON.stringify(a.GS.world.unlockPaid) === "{}", JSON.stringify(a.GS.world.unlockPaid));
  }

  // (15) config-owned node fields refreshed on load; gate offered migration ------
  {
    const f = world(0);
    const old = JSON.parse(JSON.stringify(f.GS));
    const q = old.areas.center.nodes.find(n => n.fixed && n.kind === "quarry");
    const bush = old.areas.center.nodes.find(n => !n.fixed && n.spawnerKind === "bush");
    q.swingMs = 1000; q.sprite = "🪨"; q.clicksPerDrop = 9;
    bush.swingMs = 999; bush.sprite = "?";
    const m = boot(old);
    const q2 = m.GS.areas.center.nodes.find(n => n.fixed && n.kind === "quarry");
    const b2 = m.GS.areas.center.nodes.find(n => n.id === bush.id);
    const fx = m.DATA.AREAS.center.fixtures.find(x => x.kind === "quarry");
    const sp = m.DATA.AREAS.center.spawners.find(x => x.kind === "bush");
    check("v51 fixture refreshed: swingMs/sprite/clicksPerDrop from config",
      q2 && q2.swingMs === fx.swingMs && q2.sprite === fx.sprite && q2.clicksPerDrop === fx.clicksPerDrop,
      q2 && `${q2.swingMs} ${q2.sprite} ${q2.clicksPerDrop}`);
    check("spawner node refreshed: swingMs/sprite from config", b2 && b2.swingMs === sp.swingMs && b2.sprite === sp.sprite,
      b2 && `${b2.swingMs} ${b2.sprite}`);
    // legacy count-only gate offerings -> per item (scales first), idempotent
    const g = JSON.parse(JSON.stringify(f.GS));
    g.areas.center.buildings.push({ id: 9600, type: "ascension_gate", row: 10, col: 10, paid: {}, built: true, offerings: 5 });
    g.areas.center.buildings.push({ id: 9601, type: "ascension_gate", row: 20, col: 20, paid: {}, built: true,
      offered: { talisman: 9, star_steel: NaN, bogus: 2, dragon_scale: 1 } });
    const gm = boot(g);
    const g0 = gm.GS.areas.center.buildings.find(b => b.id === 9600), g1 = gm.GS.areas.center.buildings.find(b => b.id === 9601);
    check("legacy offerings 5 -> {dragon_scale 2, star_steel 2, talisman 1}",
      JSON.stringify(g0.offered) === '{"talisman":1,"star_steel":2,"dragon_scale":2}' && g0.offerings === 5, JSON.stringify(g0.offered));
    check("corrupt offered scrubbed + clamped per type", JSON.stringify(g1.offered) === '{"talisman":2,"dragon_scale":1}' && g1.offerings === 3,
      JSON.stringify(g1.offered));
    gm.SAVE.saveState();
    const gm2 = boot(gm.stored);
    check("offered migration idempotent",
      JSON.stringify(gm2.GS.areas.center.buildings.find(b => b.id === 9600).offered) === JSON.stringify(g0.offered), "");
  }
} catch (e) {
  console.log("FAIL exception — " + (e && e.stack || e));
  fails++;
}

console.log(fails ? `\n${fails} FAILED` : "\nALL PASSED");
process.exit(fails ? 1 : 0);
