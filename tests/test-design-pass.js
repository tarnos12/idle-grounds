#!/usr/bin/env node
/* Regression: feature/design-pass (?v=51) — evidence-driven design pass.
   Automation ladder, wood generator, buffer caps, ascension AP/prestige math,
   dragonBlessed migration + ascend() carry-over, buff duration scaling, quests.
   Standalone vm sandbox — loads js/data.js, js/state.js, js/engine.js.
   Usage: node test-design-pass.js [repoRoot]   (default /home/user/idle-grounds)
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

// boot(saved, testEnabled): saved = object served from localStorage (undefined = fresh game)
function boot(saved, testEnabled) {
  const s = {}; s.window = s; s.console = console; s.Date = Date; s.Math = Math; s.JSON = JSON;
  s.location = { reload() {} };
  s.localStorage = {
    getItem: () => (saved === undefined ? null : JSON.stringify(saved)),
    setItem: () => {}, removeItem: () => {},
  };
  vm.createContext(s);
  vm.runInContext(fs.readFileSync(JS("data.js"), "utf8"), s, { filename: "data.js" });
  if (testEnabled !== undefined) s.DATA.TEST.ENABLED = !!testEnabled;
  for (const f of ["state.js", "engine.js"])
    vm.runInContext(fs.readFileSync(JS(f), "utf8"), s, { filename: f });
  s.onGroundDrop = null; s.onSfx = null;
  return s;
}
const initAll = s => { for (const k of Object.keys(s.DATA.AREAS)) s.ENGINE.initArea(k); };
const unlockAll = s => { for (const k of Object.keys(s.GS.world.unlocked)) s.GS.world.unlocked[k] = true; };

try {
  // (a) automation ladder -------------------------------------------------
  {
    const s = boot(), A = s.DATA.AUTOMATION_CLICKS;
    check("AUTOMATION_CLICKS === {1:2,2:6,3:20}", A[1] === 2 && A[2] === 6 && A[3] === 20 && Object.keys(A).length === 3,
      JSON.stringify(A));
  }

  // (b) wood generator + buffer caps --------------------------------------
  {
    const s = boot(), D = s.DATA;
    const g = D.AREAS.center.generators.find(x => x.kind === "wood");
    check("center has a wood generator (cap 10)", !!g && g.cap === 10 && g.item === "wood", JSON.stringify(g));
    check("wood generator zone exists in ZONES", !!g && !!D.ZONES[g.zone], g && g.zone);
    check("gathering_stone gather.cap === 60", D.BUILDINGS.gathering_stone.gather.cap === 60,
      String(D.BUILDINGS.gathering_stone.gather.cap));
    check("warding_seal seal.cap === 20", D.BUILDINGS.warding_seal.seal.cap === 20,
      String(D.BUILDINGS.warding_seal.seal.cap));
  }

  // (c) ascendReward with all 7 regions -----------------------------------
  {
    const s = boot(); initAll(s); unlockAll(s);
    const n = Object.values(s.GS.world.unlocked).length;
    check("7 regions exist", n === 7, String(n));
    check("ascendReward, all regions unlocked === 3+2*6+0 === 15", s.ENGINE.ascendReward() === 15, String(s.ENGINE.ascendReward()));
  }

  // (d) prestigeFactor -----------------------------------------------------
  {
    const s = boot(); initAll(s);
    s.GS.ascensions = 1; s.GS.won = false; s.GS.dragonBlessed = false; s.GS.perks = {};
    const f1 = s.ENGINE.prestigeFactor();
    check("prestigeFactor @ascensions=1 ~ 0.85", near(f1, 0.85, 0.005), f1.toFixed(4));
    s.GS.dragonBlessed = true;
    const f2 = s.ENGINE.prestigeFactor();
    check("prestigeFactor @ascensions=1 + dragonBlessed ~ 0.765", near(f2, 0.765, 0.005), f2.toFixed(4));
  }

  // (e) dragonBlessed migration --------------------------------------------
  {
    const fresh = boot();
    check("dragonBlessed: fresh state is false", fresh.GS.dragonBlessed === false, String(fresh.GS.dragonBlessed));
    const base = JSON.parse(JSON.stringify(fresh.SAVE.fresh()));
    delete base.dragonBlessed;
    const won = boot(Object.assign({}, base, { won: true }));
    check("dragonBlessed: saved {won:true} w/o field migrates true", won.GS.dragonBlessed === true, String(won.GS.dragonBlessed));
    const notWon = boot(Object.assign({}, base, { won: false }));
    check("dragonBlessed: saved {won:false} w/o field migrates false", notWon.GS.dragonBlessed === false, String(notWon.GS.dragonBlessed));
    const kept = boot(Object.assign({}, base, { won: true, dragonBlessed: false }));
    check("dragonBlessed: explicit stored false is kept", kept.GS.dragonBlessed === false, String(kept.GS.dragonBlessed));
  }

  // (f) ascend() carries stats + blessing ----------------------------------
  {
    const s = boot(); initAll(s);
    const E = s.ENGINE;
    s.GS.stats.totalCrafted = 42; s.GS.won = true;
    unlockAll(s);
    const expectAP = (s.GS.ascendPoints || 0) + E.ascendReward();
    E.ascend();                 // location.reload is stubbed; GS is replaced in place
    const GS = s.GS;
    check("ascend(): stats.totalCrafted carried (42)", GS.stats.totalCrafted === 42, String(GS.stats.totalCrafted));
    check("ascend(): dragonBlessed === true", GS.dragonBlessed === true, String(GS.dragonBlessed));
    check("ascend(): ascensions === 1", GS.ascensions === 1, String(GS.ascensions));
    check("ascend(): ascendPoints === before + reward (" + expectAP + ", all-unlocked => 15)",
      GS.ascendPoints === expectAP && expectAP === 15, String(GS.ascendPoints));
  }

  // (g) buff duration scaling (TEST on = 60s, real balance = 240s) ---------
  for (const [enabled, want] of [[true, 60000], [false, 240000]]) {
    const s = boot(undefined, enabled); initAll(s);
    const E = s.ENGINE, GS = s.GS;
    const drg = GS.areas.center.buildings.find(b => b.type === "dragon");
    const c = E.buildingCenterPx(drg);
    GS.hand = [{ item: "ember_pill", qty: 1 }];
    const t0 = Date.now();
    const r = E.dropFromHand("center", c.x, c.y);
    const dur = GS.buff ? GS.buff.until - t0 : NaN;
    check(`buff duration, TEST.ENABLED=${enabled} ~ ${want}`, !!r && r.fed === "ember_pill" && near(dur, want, 2000),
      `fed=${r && r.fed} dur=${dur}`);
  }

  // (h) quests ---------------------------------------------------------------
  {
    const s = boot(); initAll(s);
    const Q = s.DATA.QUESTS;
    check("QUESTS length === 14 (v52 spine: dragon stages folded in, recipe/craft dropped)", Q.length === 14, String(Q.length));
    const ids = Q.map(q => q.id);
    check("quest ids unique", new Set(ids).size === ids.length, ids.join(","));
    const bad = [];
    Q.forEach((q, i) => {
      try {
        const p = q.goal(), pp = s.ENGINE.questProgress(i);
        if (!p || !Number.isFinite(p.cur) || !(p.need > 0) || !pp || typeof pp.done !== "boolean") bad.push(q.id + ":shape");
      } catch (e) { bad.push(q.id + ":" + e.message); }
    });
    check("every quest goal() returns {cur,need} on a fresh state", bad.length === 0, bad.join(", ") || "ok");
    const tail = ids.slice(-2).join(",");
    check("last two quests: weaver, cultivate (v52)", tail === "weaver,cultivate", tail);
    check("waters quest still in the chain (v52)", ids.includes("waters"), ids.join(","));
  }
} catch (e) {
  console.log("FAIL exception — " + (e && e.stack || e));
  fails++;
}

console.log(fails ? `\n${fails} FAILED` : "\nALL PASSED");
process.exit(fails ? 1 : 0);
