#!/usr/bin/env node
/* Regression: v52 slice F (balance) — jade veins, Dragon Shrine cost, Algae Farm /
   Herb Garden caps, dominated-recipe niches, fixture swing rate.
   Standalone vm sandbox — loads js/data.js, js/state.js, js/engine.js.
   Usage: node test-v52-balance.js [repoRoot]   (default: repo containing this file)
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

function boot() {
  const s = {}; s.window = s; s.console = console; s.Date = Date; s.Math = Math; s.JSON = JSON;
  s.localStorage = { getItem: () => null, setItem: () => {}, removeItem: () => {} };
  vm.createContext(s);
  for (const f of ["data.js", "state.js", "engine.js"])
    vm.runInContext(fs.readFileSync(JS(f), "utf8"), s, { filename: f });
  for (const k of Object.keys(s.DATA.AREAS)) s.ENGINE.initArea(k);
  s.onGroundDrop = null; s.onSfx = null;
  return s;
}

try {
  const s = boot();
  const D = s.DATA, E = s.ENGINE, GS = s.GS;

  // 1. jade veins ---------------------------------------------------------
  const sp = D.AREAS.mine.spawners.find(x => x.kind === "jadevein");
  check("mine has a jadevein spawner", !!sp);
  check("jadevein: break, 3 hits, drops jade_shard 1-2",
    sp && sp.interaction === "break" && sp.hits === 3 &&
    sp.drops.length === 1 && sp.drops[0].item === "jade_shard", JSON.stringify(sp && sp.drops));
  const veins = GS.areas.mine.nodes.filter(n => n.kind === "jadevein");
  check("fresh Mine spawns jade veins", veins.length >= 1, "count=" + veins.length);
  if (veins.length) {
    const v = veins[0];
    const before = GS.areas.mine.ground.filter(g => g.item === "jade_shard").reduce((a, g) => a + (g.qty || 1), 0);
    for (let i = 0; i < 3; i++) E.harvestNode("mine", v.id, false);
    const after = GS.areas.mine.ground.filter(g => g.item === "jade_shard").reduce((a, g) => a + (g.qty || 1), 0);
    check("breaking a jade vein drops jade_shard", after > before, before + " -> " + after);
    check("jade vein is consumed and queued to respawn",
      !GS.areas.mine.nodes.some(n => n.id === v.id) &&
      GS.areas.mine.spawnQueue.some(q => q.kind === "jadevein"));
  }

  // 2. Dragon Shrine cost -------------------------------------------------
  const shr = D.BUILDINGS.dragon_shrine.cost;
  check("Dragon Shrine cost has no jade", !("jade" in shr) && !("jade_shard" in shr), JSON.stringify(shr));
  check("Dragon Shrine cost = brick 10 + cloth 8 + obsidian 4",
    shr.brick === 10 && shr.cloth === 8 && shr.obsidian === 4, JSON.stringify(shr));

  // 3. Algae Farm / Herb Garden -------------------------------------------
  const af = D.BUILDINGS.algae_farm.gen, hg = D.BUILDINGS.herb_garden.gen;
  check("Algae Farm cap 24, interval 2000", af.cap === 24 && af.intervalMs === 2000, JSON.stringify(af));
  check("Herb Garden cap 24, interval 2500", hg.cap === 24 && hg.intervalMs === 2500, JSON.stringify(hg));

  // 4. recipes ------------------------------------------------------------
  const rec = name => {
    let hit = null;
    for (const k in D.BUILDINGS) for (const r of (D.BUILDINGS[k].recipes || [])) if (r.name === name) hit = r;
    return hit;
  };
  const ss = rec("Star Steel"), qe = rec("Qi Elixir"), gl = rec("Glass");
  check("bone Star Steel outputs 2", ss && ss.outputQty === 2, ss && ss.outputQty);
  check("herb Qi Elixir outputs 2", qe && qe.outputQty === 2 && qe.inputs.spirit_herb === 1, qe && qe.outputQty);
  check("sand Glass = 3 sand -> 2 glass, faster",
    gl && gl.inputs.sand === 3 && gl.outputQty === 2 && gl.timeMs < 6000, JSON.stringify(gl));
  const og = rec("Obsidian Glass"), ast = rec("Astral Steel"), moon = rec("Moon Elixir");
  check("Obsidian Glass unchanged (1 obsidian -> 2 glass)", og && og.inputs.obsidian === 1 && og.outputQty === 2);
  check("Astral Steel unchanged (outputs 1)", ast && ast.outputQty === 1);
  check("Moon Elixir unchanged (outputs 1)", moon && moon.outputQty === 1);

  // 5. fixtures -----------------------------------------------------------
  const fx = [];
  for (const k in D.AREAS) for (const f of (D.AREAS[k].fixtures || [])) fx.push(f);
  for (const kind of ["quarry", "spirittree", "spring"]) {
    const f = fx.find(x => x.kind === kind);
    check("fixture " + kind + " swingMs 350", f && f.swingMs === 350, f && f.swingMs);
  }
  check("spirit tree clicksPerDrop unchanged (3)", fx.find(x => x.kind === "spirittree").clicksPerDrop === 3);
  check("quarry clicksPerDrop unchanged (5)", fx.find(x => x.kind === "quarry").clicksPerDrop === 5);
  const tree = GS.areas.center.nodes.find(n => n.kind === "spirittree");
  check("placed spirit tree node carries swingMs 350", tree && tree.swingMs === 350, tree && tree.swingMs);
} catch (e) {
  console.log("FAIL exception — " + (e && e.stack || e));
  fails++;
}
console.log(fails ? "\n" + fails + " FAIL(S)" : "\nALL PASS");
process.exit(fails ? 1 : 0);
