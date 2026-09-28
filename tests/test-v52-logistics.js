#!/usr/bin/env node
/* Regression: v52 slice A — logistics & ground integrity.
   Compact wood/sand generator fields, link-aware Gathering Stones, player-drop
   grace window, value-aware ground eviction, automation litter pause, fuel
   sliver batches, TEST-scaled lantern beat, burner rack placement, recipe
   switch keeps shared stock, buildingStatus/craftRate/link health, starter
   network throughput, transient fields never saved.
   Standalone vm sandbox with a virtual clock — loads js/data.js, js/state.js,
   js/engine.js.
   Usage: node tests/test-v52-logistics.js [repoRoot]   (default /home/user/idle-grounds)
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

// boot(saved, testEnabled): fresh game (or `saved` from localStorage) on a
// virtual clock s.clock (ms) that Date.now() reads.
function boot(saved, testEnabled) {
  const s = {}; s.window = s; s.console = console; s.Math = Math; s.JSON = JSON;
  s.clock = 1.7e12;
  s.Date = { now: () => s.clock };
  s.location = { reload() {} };
  s.localStorage = {
    getItem: () => (saved === undefined ? null : JSON.stringify(saved)),
    setItem: (k, v) => { s.savedRaw = v; }, removeItem: () => {},
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
// advance the virtual clock `ms` in 50ms game ticks
const run = (s, ms) => { for (let t = 0; t < ms; t += 50) { s.ENGINE.gameTick(); s.clock += 50; } };
// place a finished building straight into an area (bypasses cost/unlock)
function put(s, areaKey, type, row, col, extra) {
  const a = s.GS.areas[areaKey];
  const b = Object.assign({ id: a.nextBuildId++, type, row, col, paid: {}, built: true, item: null, qty: 0 }, extra || {});
  if (s.DATA.BUILDINGS[type].gather) b.inv = [];
  if (s.DATA.BUILDINGS[type].lantern) { b.links = []; b.connIdx = 0; b.nextSend = 0; }
  a.buildings.push(b);
  return b;
}
// empty test area: the mine, unlocked, nothing on it
function blankMine(s) {
  initAll(s);
  s.GS.world.unlocked.mine = true;
  const a = s.GS.areas.mine;
  a.nodes = []; a.ground = []; a.buildings = []; a.spawnQueue = []; a.wisps = [];
  a.genTimers = [];
  return a;
}

try {
  // (1) compact generator fields -----------------------------------------
  {
    const s = boot(); initAll(s);
    const D = s.DATA, E = s.ENGINE, CELL = D.GRID.cell;
    const wg = D.AREAS.center.generators.find(g => g.item === "wood");
    const sg = D.AREAS.farm.generators.find(g => g.item === "sand");
    check("wood generator uses ZONES.woodField (cap 10)", wg.zone === "woodField" && wg.cap === 10, JSON.stringify(wg));
    check("sand generator uses ZONES.sandField (cap 10)", sg.zone === "sandField" && sg.cap === 10, JSON.stringify(sg));
    for (const zk of ["woodField", "sandField"]) {
      const z = D.ZONES[zk][0];
      check(`${zk} is 9x9`, z.r1 - z.r0 === 8 && z.c1 - z.c0 === 8, JSON.stringify(z));
    }
    const sf = D.ZONES.sandField[0], ml = D.ZONES.midLeft[0];
    check("sandField sits inside the Farm's midLeft band", sf.r0 >= ml.r0 && sf.r1 <= ml.r1 && sf.c0 >= ml.c0 && sf.c1 <= ml.c1,
      JSON.stringify(sf));
    check("sandField clear of Farm nodes", !s.GS.areas.farm.nodes.some(n =>
      n.row + n.size - 1 >= sf.r0 && n.row <= sf.r1 && n.col + n.size - 1 >= sf.c0 && n.col <= sf.c1));
    E.setupStarterNetwork();
    const R = D.BUILDINGS.gathering_stone.gather.radius * CELL;
    const wz = D.ZONES.woodField[0];
    const gsWood = s.GS.areas.center.buildings.find(b => b.type === "gathering_stone" && b.row === 16 && b.col === 44);
    check("starter wood stone at (16,44) = woodField centre", !!gsWood &&
      (wz.r0 + wz.r1) / 2 === 16 && (wz.c0 + wz.c1) / 2 === 44);
    const covers = (p, z) => {
      let n = 0;
      for (let r = z.r0; r <= z.r1; r++) for (let c = z.c0; c <= z.c1; c++)
        if (Math.hypot(p.x - (c + 0.5) * CELL, p.y - (r + 0.5) * CELL) > R) n++;
      return n;
    };
    check("wood stone radius covers every woodField cell", !!gsWood && covers(E.buildingCenterPx(gsWood), wz) === 0);
    const mid = { x: ((sf.c0 + sf.c1) / 2 + 0.5) * CELL, y: ((sf.r0 + sf.r1) / 2 + 0.5) * CELL };
    check("a stone at the sandField centre covers every cell", covers(mid, sf) === 0);
  }

  // (13) starter network: no Forge, stone storehouse, throughput ---------
  {
    const s = boot(undefined, true); initAll(s);
    const E = s.ENGINE, a = s.GS.areas.center;
    E.setupStarterNetwork();
    check("starter network has no Forge", !a.buildings.some(b => b.type === "forge"));
    const shStone = a.buildings.find(b => b.type === "storehouse" && b.item === "stone" && b.lock);
    const gsStone = a.buildings.find(b => b.type === "gathering_stone" && b.row === 80 && b.col === 13);
    const linked = shStone && gsStone && a.buildings.some(b => (b.links || []).some(l => l.from === gsStone.id && l.to === shStone.id));
    check("stone Storehouse (typed, locked) linked from the stone Gathering Stone", !!linked);
    run(s, 60000);
    const conv = a.buildings.filter(b => s.DATA.BUILDINGS[b.type].recipes);
    const idle = conv.filter(b => E.craftRate(b) <= 0).map(b => b.type);
    check("every starter converter crafted within the first TEST minute", idle.length === 0,
      conv.map(b => b.type + "=" + E.craftRate(b)).join(" "));
    run(s, 9 * 60000);
    const n = s.GS.stats.totalCrafted;
    check("starter network: >= 200 crafts in 10 TEST-minutes, zero input (v51: ~16 in 2h)", n >= 200, "crafted=" + n);
  }

  // (2) link-aware Gathering Stones ---------------------------------------
  {
    const s = boot(undefined, true); const a = blankMine(s);
    const E = s.ENGINE, CELL = s.DATA.GRID.cell;
    const gs = put(s, "mine", "gathering_stone", 40, 40);
    check("unlinked stone collects everything (stoneAccepts null)", E.stoneAccepts("mine", gs) === null);
    const seal = put(s, "mine", "warding_seal", 40, 50, { item: "wood", lock: true });
    const lan = put(s, "mine", "wisp_lantern", 44, 44);
    lan.links.push({ from: gs.id, to: seal.id });
    check("stone -> wood seal: accepts only wood", JSON.stringify(E.stoneAccepts("mine", gs)) === '["wood"]',
      JSON.stringify(E.stoneAccepts("mine", gs)));
    const kiln = put(s, "mine", "kiln", 30, 60, { recipe: 0, stock: {}, fuelQ: [] });
    lan.links.push({ from: gs.id, to: kiln.id });
    const acc = E.stoneAccepts("mine", gs);
    check("stone -> Kiln(Brick): + clay and every fuel item",
      ["wood", "clay", "bamboo", "charcoal", "firestone"].every(it => acc.includes(it)) && !acc.includes("leaves"),
      JSON.stringify(acc));
    const sh = put(s, "mine", "storehouse", 50, 40);
    lan.links.push({ from: gs.id, to: sh.id });
    check("stone -> empty unlocked Storehouse: collects everything", E.stoneAccepts("mine", gs) === null);
    lan.links.pop();
    const c = E.buildingCenterPx(gs);
    E.dropGround("mine", "leaves", 3, c.x + 40, c.y);
    E.dropGround("mine", "wood", 3, c.x - 40, c.y);
    run(s, 3000);
    const on = it => a.ground.filter(g => g.item === it).length;
    check("filtered stone vacuums the wood, leaves stay on the ground",
      on("wood") === 0 && on("leaves") === 3 && !gs.inv.some(st => st.item === "leaves"),
      "ground wood=" + on("wood") + " leaves=" + on("leaves") + " inv=" + JSON.stringify(gs.inv));
  }

  // (3) player-drop grace window ------------------------------------------
  {
    const s = boot(undefined, true); const a = blankMine(s);
    const E = s.ENGINE;
    const gs = put(s, "mine", "gathering_stone", 40, 40);
    const c = E.buildingCenterPx(gs);
    s.GS.hand = [{ item: "stone", qty: 2 }];
    const r = E.dropFromHand("mine", c.x + 60, c.y + 60);
    check("hand drop on open ground stamps manualAt", r && r.dropped === "stone" && a.ground.length === 1 &&
      a.ground[0].manualAt === s.clock, JSON.stringify(a.ground[0]));
    run(s, 3500);
    check("stone ignores a player drop inside the 4s grace window", E.gatherTotal(gs) === 0, "inv=" + E.gatherTotal(gs));
    run(s, 2500);
    check("stone collects it once the grace window passes", E.gatherTotal(gs) === 1, "inv=" + E.gatherTotal(gs));
    // manual harvest vs automation harvest
    a.ground = [];
    const node = { id: a.nextNodeId++, row: 20, col: 20, size: 1, kind: "ore", spawnerKind: "ore", interaction: "instant",
      tier: 1, hitsLeft: 1, regrowSec: 10, swingMs: 400, drops: [{ item: "stone", min: 1, max: 1 }], autoFlash: 0 };
    a.nodes.push(node);
    E.harvestNode("mine", node.id, true);   // the UI hold loop passes isAuto=true
    check("player (hold-loop) harvest drops carry manualAt", a.ground.length === 1 && !!a.ground[0].manualAt);
    a.ground = []; a.nodes = [Object.assign({}, node, { id: a.nextNodeId++ })];
    a.upgrades.automation = 1;
    E.automationTick();
    check("automation harvest drops are NOT protected", a.ground.length >= 1 && a.ground.every(g => !g.manualAt),
      JSON.stringify(a.ground));
    // combat loot is a player drop
    const cen = s.GS.areas.center;
    cen.enemies = [{ id: 99, x: 2700, y: 400, hp: 1, maxHp: 1, tx: 2700, ty: 400, hitAt: 0 }];
    cen.ground = [];
    E.attackEnemy("center", 99);
    check("combat loot carries manualAt", cen.ground.length > 0 && cen.ground.every(g => !!g.manualAt));
  }

  // (4) value-aware ground eviction ---------------------------------------
  {
    const s = boot(undefined, true); const a = blankMine(s);
    const E = s.ENGINE;
    E.dropGround("mine", "star_steel", 100, 500, 500);            // T3 (recipe output)
    E.dropGround("mine", "algae", 50, 500, 500, "crafted");       // Algae Farm product (tagged)
    E.dropGround("mine", "spirit_essence", 100, 500, 500);        // class 1 (fox loot)
    E.dropGround("mine", "leaves", 350, 500, 500);                // class 0
    check("at the cap: 600 items, nothing evicted yet", a.ground.length === 600);
    E.dropGround("mine", "dragon_scale", 5, 500, 500);
    const cnt = it => a.ground.filter(g => g.item === it).length;
    check("overflow evicts the oldest class-0 items (leaves) first", a.ground.length === 600 && cnt("leaves") === 345 &&
      cnt("dragon_scale") === 5 && cnt("star_steel") === 100, "leaves=" + cnt("leaves"));
    E.dropGround("mine", "jade_shard", 400, 500, 500);
    check("leaves gone before any class-1 item, then class 1 (essence) next",
      cnt("leaves") === 0 && cnt("spirit_essence") === 45 && cnt("jade_shard") === 400 && a.ground.length === 600,
      "leaves=" + cnt("leaves") + " essence=" + cnt("spirit_essence"));
    check("crafted-tagged algae, star steel, dragon scales never evicted while raw remain",
      cnt("algae") === 50 && cnt("star_steel") === 100 && cnt("dragon_scale") === 5);
    E.dropGround("mine", "wood", 10, 500, 500);
    check("a class-0 drop goes before the remaining class-1 items", cnt("wood") === 0 && cnt("spirit_essence") === 45 &&
      a.ground.length === 600, "len=" + a.ground.length);
    E.dropGround("mine", "iron_bar", 250, 500, 500);
    check("only protected items left: area exceeds the cap (805 <= 900)", a.ground.length === 805 && cnt("spirit_essence") === 0,
      "len=" + a.ground.length);
    E.dropGround("mine", "iron_bar", 200, 500, 500);
    check("hard ceiling 900: the oldest go regardless", a.ground.length === 900 && cnt("star_steel") === 0 && cnt("algae") === 45,
      "len=" + a.ground.length + " oldest=" + a.ground[0].item);
  }

  // (5) automation pauses on a littered field ----------------------------
  {
    const s = boot(undefined, true); initAll(s);
    const E = s.ENGINE, a = s.GS.areas.center;
    a.upgrades.automation = 1;
    a.ground = [];
    E.automationTick();
    check("automation runs on a clean field (autoPaused false)", a.autoPaused === false);
    E.dropGround("center", "star_steel", 460, 1500, 1500);
    const before = a.ground.length;
    const n = E.automationTick();
    check("> 450 ground items: area skipped, autoPaused = true", a.autoPaused === true && a.ground.length === before && n === 0,
      "len=" + a.ground.length + " harvested=" + n);
  }

  // (6) fuel sliver starts a batch + (10) buildingStatus -------------------
  {
    const s = boot(undefined, true); const a = blankMine(s);
    const E = s.ENGINE;
    const kiln = put(s, "mine", "kiln", 30, 60, { recipe: 0, stock: {}, fuelQ: [] });
    check("status: empty unlinked converter is idle", E.buildingStatus("mine", kiln).state === "idle");
    kiln.stock = { clay: 1 };
    const st1 = E.buildingStatus("mine", kiln);
    check("status: partial stock -> starved on clay", st1.state === "starved" && st1.item === "clay", JSON.stringify(st1));
    kiln.stock = { clay: 4 };
    check("status: inputs ready, no fuel -> nofuel", E.buildingStatus("mine", kiln).state === "nofuel");
    E.gameTick();
    check("no fuel: no batch starts", !kiln.smeltDoneAt);
    kiln.fuelQ = [{ item: "wood", rem: 300, total: 10000 }];      // 300ms sliver < 1000ms batch
    E.gameTick();
    check("a fuel sliver shorter than the batch still starts it", kiln.smeltDoneAt > 0 && kiln.stock.clay === 2,
      "smeltDoneAt=" + kiln.smeltDoneAt);
    check("status: batch running -> working", E.buildingStatus("mine", kiln).state === "working");
    run(s, 1100);
    check("sliver batch completes (free remainder) with a crafted-tagged brick",
      a.ground.some(g => g.item === "brick" && g.crafted) && E.craftRate(kiln) >= 1, "craftRate=" + E.craftRate(kiln));
    kiln.stock = { clay: 22 }; kiln.fuelQ = [{ item: "charcoal", rem: 40000, total: 40000 }];
    E.gameTick();
    check("status: running with every input at stockCap -> full", E.buildingStatus("mine", kiln).state === "full");
    const gs = put(s, "mine", "gathering_stone", 40, 40);
    check("status: gathering stone below cap -> null", E.buildingStatus("mine", gs) === null);
    gs.inv = [{ item: "stone", qty: 60 }];
    check("status: gathering stone at cap -> full", E.buildingStatus("mine", gs).state === "full");
    const sh = put(s, "mine", "storehouse", 50, 40, { item: "stone", qty: 200 });
    check("status: storehouse at cap -> full", E.buildingStatus("mine", sh).state === "full");
    const pav = put(s, "mine", "meditation_pavilion", 50, 50, { disciples: 1, buns: 0 });
    const sp = E.buildingStatus("mine", pav);
    check("status: pavilion with disciples, no food -> starved spirit_buns", sp.state === "starved" && sp.item === "spirit_buns");
    check("status: lantern -> null", E.buildingStatus("mine", put(s, "mine", "wisp_lantern", 60, 60)) === null);
  }

  // (7) lantern beat scaled in TEST + (12) link health ----------------------
  {
    const s = boot(undefined, true); const a = blankMine(s);
    const E = s.ENGINE;
    const gs = put(s, "mine", "gathering_stone", 40, 40);
    const sh = put(s, "mine", "storehouse", 40, 50, { item: "wood", lock: true });
    const lan = put(s, "mine", "wisp_lantern", 44, 44);
    lan.links.push({ from: gs.id, to: sh.id });
    E.gameTick();
    check("link health: empty source -> fail 'empty'", lan.links[0]._stat && lan.links[0]._stat.fail === "empty");
    gs.inv = [{ item: "stone", qty: 3 }];
    s.clock += 300; E.gameTick();
    check("link health: target refuses what the source holds -> 'refused'", lan.links[0]._stat.fail === "refused");
    gs.inv = [{ item: "wood", qty: 3 }];
    s.clock += 300; const t0 = s.clock; E.gameTick();
    const beat = lan.nextSend - t0, want = 1000 * s.DATA.TEST.timeScale * s.ENGINE.prestigeFactor();
    check("link health: send stamps sentAt, fail null", lan.links[0]._stat.sentAt === t0 && lan.links[0]._stat.fail === null);
    check("TEST mode scales the lantern beat by timeScale", Math.abs(beat - want) < 1, "beat=" + beat + " want=" + want);
  }

  // (8) burner rack placement ----------------------------------------------
  {
    const s = boot(undefined, true); const a = blankMine(s);
    const E = s.ENGINE;
    // the Mine's buildable land is outside its centre zone: use the left band
    put(s, "mine", "cauldron", 30, 5);                                // 3x3 at cols 5-7
    check("burner whose rack would cover a neighbour is refused", !E.canPlaceBuilding("mine", 30, 8, "star_anvil"));
    check("same burner with room for its rack is allowed", E.canPlaceBuilding("mine", 30, 11, "star_anvil"));
    check("rack may not leave the map (col < 3)", !E.canPlaceBuilding("mine", 10, 1, "kiln"));
    put(s, "mine", "kiln", 50, 15);                                   // rack cols 12-14, rows 50-51
    check("a footprint may not cover an existing burner's rack", !E.canPlaceBuilding("mine", 49, 11, "workbench"));
    check("a footprint beside the rack is fine", E.canPlaceBuilding("mine", 52, 11, "workbench"));
    check("placeBuilding honours the rack rule", E.placeBuilding("mine", "star_anvil", 30, 8) === null);
  }

  // (9) setRecipe keeps shared stock ---------------------------------------
  {
    const s = boot(undefined, true); const a = blankMine(s);
    const E = s.ENGINE;
    const loom = put(s, "mine", "loom", 40, 40, { recipe: 0, stock: { cotton: 7 }, smeltDoneAt: s.clock + 5000 });
    s.GS.hand = [];
    const sw0 = s.GS.stats.recipeSwitches || 0;
    E.setRecipe("mine", loom.id, 1);                                  // Cloth -> Rope (cotton+algae)
    check("switch to a recipe sharing cotton keeps it (incl. the batch in progress)",
      loom.stock.cotton === 10 && a.ground.length === 0 && s.GS.hand.length === 0, JSON.stringify(loom.stock));
    loom.stock.algae = 4;
    s.GS.handCap = 3; s.GS.hand = [];
    E.setRecipe("mine", loom.id, 2);                                  // Rope -> Robe (cloth+herb)
    check("non-shared stock goes to the hand while it has room", E.handTotal() === 3, JSON.stringify(s.GS.hand));
    check("only the overflow drops (as a manual drop)", a.ground.length === 11 && a.ground.every(g => !!g.manualAt),
      "ground=" + a.ground.length);
    check("recipeSwitches still counts", s.GS.stats.recipeSwitches === sw0 + 2);
  }

  // (11) transient fields never reach the save -----------------------------
  {
    const s = boot(undefined, true); initAll(s);
    const E = s.ENGINE, a = s.GS.areas.center;
    E.setupStarterNetwork();
    run(s, 20000);
    s.GS.hand = [{ item: "wood", qty: 1 }];
    E.dropFromHand("center", 1000, 1000);
    a.upgrades.automation = 1; E.automationTick();
    const hasCrafts = a.buildings.some(b => b._crafts && b._crafts.length);
    const hasStat = a.buildings.some(b => (b.links || []).some(l => l._stat));
    check("runtime transient fields exist before saving", hasCrafts && hasStat && a.ground.some(g => g.manualAt) &&
      a.autoPaused !== undefined);
    s.SAVE.saveState();
    const raw = s.savedRaw || "";
    check("saved JSON has no manualAt / _crafts / _stat / autoPaused",
      raw.length > 0 && !/"manualAt"|"_crafts"|"_stat"|"autoPaused"/.test(raw));
    const s2 = boot(JSON.parse(raw), true);
    check("the save loads back (starter network intact)", s2.GS.starterPlaced === true &&
      s2.GS.areas.center.buildings.length === a.buildings.length);
    // craftRate isn't recorded during offline replay
    const s3 = boot(undefined, true); initAll(s3);
    s3.ENGINE.setupStarterNetwork();
    s3.GS.lastSeen = s3.clock - 5 * 60000;
    s3.ENGINE.runOfflineCatchup();
    const rec = s3.GS.areas.center.buildings.some(b => b._crafts && b._crafts.length);
    check("offline replay records no craft-rate stamps", !rec && s3.GS.stats.totalCrafted > 0,
      "crafted=" + s3.GS.stats.totalCrafted);
  }
} catch (e) {
  console.log("FAIL threw: " + (e && e.stack || e));
  fails++;
}

console.log(fails ? `\n${fails} FAILED` : "\nALL PASS");
process.exit(fails ? 1 : 0);
