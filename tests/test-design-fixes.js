#!/usr/bin/env node
/* Regression: designer-playtest batch (?v=50) — ENGINE-testable fixes.
   endingSeen migration, dragon-pill auto-front, starter clay/stone coverage,
   offline welcome-back gate (90s).
   Standalone vm sandbox — loads js/data.js, js/state.js, js/engine.js.
   Usage: node test-design-fixes.js [repoRoot]   (default /home/user/idle-grounds)
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

// boot(saved): saved = object served from localStorage (or undefined for a fresh game)
function boot(saved) {
  const s = {}; s.window = s; s.console = console; s.Date = Date; s.Math = Math; s.JSON = JSON;
  s.localStorage = {
    getItem: () => (saved === undefined ? null : JSON.stringify(saved)),
    setItem: () => {}, removeItem: () => {},
  };
  vm.createContext(s);
  for (const f of ["data.js", "state.js", "engine.js"])
    vm.runInContext(fs.readFileSync(JS(f), "utf8"), s, { filename: f });
  s.onGroundDrop = null; s.onSfx = null;
  return s;
}
const initAll = s => { for (const k of Object.keys(s.DATA.AREAS)) s.ENGINE.initArea(k); };

try {
  // (a) endingSeen ------------------------------------------------------
  const fresh = boot();
  check("endingSeen: fresh state is false", fresh.GS.endingSeen === false, String(fresh.GS.endingSeen));
  const base = JSON.parse(JSON.stringify(fresh.SAVE.fresh()));
  delete base.endingSeen;
  const won = boot(Object.assign({}, base, { won: true }));
  check("endingSeen: saved {won:true} w/o field migrates true", won.GS.endingSeen === true, String(won.GS.endingSeen));
  const notWon = boot(Object.assign({}, base, { won: false }));
  check("endingSeen: saved {won:false} w/o field migrates false", notWon.GS.endingSeen === false, String(notWon.GS.endingSeen));
  const kept = boot(Object.assign({}, base, { won: true, endingSeen: false }));
  check("endingSeen: explicit stored false is kept", kept.GS.endingSeen === false, String(kept.GS.endingSeen));

  // (b) dragon pill front-move -------------------------------------------
  {
    const s = boot(); initAll(s);
    const E = s.ENGINE, GS = s.GS;
    const drg = GS.areas.center.buildings.find(b => b.type === "dragon");
    check("dragon building exists in center", !!drg);
    const c = E.buildingCenterPx(drg);
    GS.hand = [{ item: "wood", qty: 1 }, { item: "ember_pill", qty: 1 }];
    const r1 = E.dropFromHand("center", c.x, c.y);
    check("pill front-move: 1st drop returns {reordered:'ember_pill'}",
      !!r1 && r1.reordered === "ember_pill", JSON.stringify(r1));
    check("pill front-move: hand[0] is ember_pill", !!GS.hand[0] && GS.hand[0].item === "ember_pill",
      GS.hand.map(h => h.item).join(","));
    const r2 = E.dropFromHand("center", c.x, c.y);
    check("pill front-move: 2nd drop returns {fed:'ember_pill'}", !!r2 && r2.fed === "ember_pill", JSON.stringify(r2));
    check("pill front-move: GS.buff.kind === 'ember_pill'", !!GS.buff && GS.buff.kind === "ember_pill",
      JSON.stringify(GS.buff));
  }

  // (c) starter gathering stones cover their fields -----------------------
  {
    const s = boot(); initAll(s);
    const E = s.ENGINE, GS = s.GS, D = s.DATA, CELL = D.GRID.cell;
    E.setupStarterNetwork();
    const stones = GS.areas.center.buildings.filter(b => b.type === "gathering_stone");
    const R = D.BUILDINGS.gathering_stone.gather.radius * CELL;
    for (const [zone, label] of [["clayField", "clay"], ["quarryField", "stone"]]) {
      const z = D.ZONES[zone][0];
      const mid = { x: (z.c0 + z.c1 + 1) / 2 * CELL, y: (z.r0 + z.r1 + 1) / 2 * CELL };
      // the field's starter stone = the gathering stone nearest the field centre
      let gs = null, best = Infinity;
      for (const b of stones) {
        const p = E.buildingCenterPx(b), d = Math.hypot(p.x - mid.x, p.y - mid.y);
        if (d < best) { best = d; gs = b; }
      }
      const p = E.buildingCenterPx(gs);
      let worst = 0, n = 0;
      for (let r = z.r0; r <= z.r1; r++) for (let c = z.c0; c <= z.c1; c++) {
        const d = Math.hypot(p.x - (c + 0.5) * CELL, p.y - (r + 0.5) * CELL);
        worst = Math.max(worst, d); if (d > R) n++;
      }
      check(`starter ${label} stone covers every ${zone} cell`, !!gs && n === 0,
        `stone@(${gs.row},${gs.col}) worst ${(worst / CELL).toFixed(1)} cells, radius ${R / CELL}, uncovered ${n}`);
    }
  }

  // (d) offline welcome-back gate (90s) -------------------------------------
  {
    const s = boot(); initAll(s);
    s.GS.lastSeen = Date.now() - 30000;
    const r30 = s.ENGINE.runOfflineCatchup();
    check("offline gate: 30s away returns null", r30 === null, JSON.stringify(r30));
    s.GS.lastSeen = Date.now() - 120000;
    const r120 = s.ENGINE.runOfflineCatchup();
    check("offline gate: 120s away returns a summary",
      !!r120 && typeof r120 === "object" && Number.isFinite(r120.elapsedMs),
      r120 ? "elapsedMs=" + r120.elapsedMs : String(r120));
  }
} catch (e) {
  console.log("FAIL harness threw — " + ((e && e.stack) || e));
  fails++;
}

console.log(fails ? `\n${fails} FAILED` : "\nall passed");
process.exit(fails ? 1 : 0);
