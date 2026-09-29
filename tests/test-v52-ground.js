#!/usr/bin/env node
/* Regression: v52 fix wave — "ground" slice (the ground/litter blocker).
   Output back-pressure (converters, generator buildings, pavilions), the
   eviction rules (fresh + grace items never evicted, protected share 480,
   generator rare finds are raw, 900 hard ceiling), per-type automation skip
   (area._autoSkip), capacity-aware Gathering Stones (+ ejection of dead
   stock, 'nomatch' link health, pulls pass over colliders), generator rare
   drops inside the field cap, burner racks in occupiedCells, and the
   zero-input starter network never eating a player's harvest.
   Standalone vm sandbox with a virtual clock — loads js/data.js, js/state.js,
   js/engine.js.
   Usage: node tests/test-v52-ground.js [repoRoot]   (default /home/user/idle-grounds)
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

function boot(saved, testEnabled) {
  const s = {}; s.window = s; s.console = console; s.Math = Math; s.JSON = JSON;
  s.clock = 1.7e12;
  s.Date = { now: () => s.clock };
  s.location = { reload() {} };
  s.localStorage = {
    getItem: () => (saved === undefined ? null : (typeof saved === "string" ? saved : JSON.stringify(saved))),
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
const run = (s, ms, auto) => {
  for (let t = 0; t < ms; t += 50) {
    s.ENGINE.gameTick(); s.clock += 50;
    if (auto && t % 1000 === 0) s.ENGINE.automationTick();
  }
};
function put(s, areaKey, type, row, col, extra) {
  const a = s.GS.areas[areaKey];
  const b = Object.assign({ id: a.nextBuildId++, type, row, col, paid: {}, built: true, item: null, qty: 0 }, extra || {});
  if (s.DATA.BUILDINGS[type].gather) b.inv = [];
  if (s.DATA.BUILDINGS[type].lantern) { b.links = []; b.connIdx = 0; b.nextSend = 0; }
  a.buildings.push(b);
  return b;
}
function blankMine(s) {
  initAll(s);
  s.GS.world.unlocked.mine = true;
  const a = s.GS.areas.mine;
  a.nodes = []; a.ground = []; a.buildings = []; a.spawnQueue = []; a.wisps = []; a.genTimers = [];
  return a;
}
const count = (a, it) => a.ground.filter(g => g.item === it).length;

try {
  // ---- (1) eviction rules -------------------------------------------------
  {
    const s = boot(undefined, true); const a = blankMine(s); const E = s.ENGINE;
    check("eviction classes: jade 2, generator jade ('gen') 1, crafted 2, leaves 0",
      E.evictClassOf({ item: "jade_shard" }) === 2 && E.evictClassOf({ item: "jade_shard", gen: true }) === 1 &&
      E.evictClassOf({ item: "algae", crafted: true }) === 2 && E.evictClassOf({ item: "leaves" }) === 0);
    // protected litter fills the area to the cap
    E.dropGround("mine", "brick", 600, 500, 500, "crafted");
    // a player's harvest lands (manual): never evicted by its own call, the
    // protected share (> 480) gives way instead
    E.dropGround("mine", "wood", 5, 800, 800, "manual");
    check("600 crafted + player drop: the drop stays, the oldest crafted go", count(a, "wood") === 5 &&
      count(a, "brick") === 595 && a.ground.length === 600, "wood=" + count(a, "wood") + " brick=" + count(a, "brick"));
    // a bot / generator drop: evicts protected while over the share, never the grace wood
    E.dropGround("mine", "stone", 10, 900, 900);
    check("raw drop over a protected share > 480: oldest protected evicted, raw + grace kept",
      count(a, "stone") === 10 && count(a, "wood") === 5 && count(a, "brick") === 585);
    // drive protected down to the 480 share, then raw starts churning (oldest raw first)
    E.dropGround("mine", "stone", 105, 900, 900);
    check("protected shrinks to exactly the 480 share", count(a, "brick") === 480 && a.ground.length === 600,
      "brick=" + count(a, "brick"));
    E.dropGround("mine", "leaves", 20, 900, 900);
    check("at the share: new raw evicts the oldest class-0 raw (stone), never grace wood or bricks",
      count(a, "brick") === 480 && count(a, "wood") === 5 && count(a, "leaves") === 20 && count(a, "stone") === 95,
      "stone=" + count(a, "stone"));
    // generator rare jade is class 1 and goes before protected (<= share) bricks
    a.ground = [];
    E.dropGround("mine", "brick", 400, 500, 500, "crafted");
    E.dropGround("mine", "jade_shard", 150, 500, 500, "gen");
    E.dropGround("mine", "jade_shard", 50, 500, 500);          // placed / looted jade: protected
    s.clock += 5000;                                            // (no grace in play)
    E.dropGround("mine", "iron_ore", 30, 500, 500);
    const genJ = a.ground.filter(g => g.item === "jade_shard" && g.gen).length;
    check("generator jade (class 1) is evicted before protected jade/bricks", genJ === 120 &&
      a.ground.filter(g => g.item === "jade_shard" && !g.gen).length === 50 && count(a, "brick") === 400,
      "genJade=" + genJ);
    // grace: manual drops inside the window survive a flood; after it they are ordinary raw
    a.ground = [];
    E.dropGround("mine", "wood", 600, 500, 500, "manual");
    E.dropGround("mine", "stone", 50, 500, 500);
    check("in-grace player drops are never evicted (area may pass 600)", count(a, "wood") === 600 && a.ground.length === 650);
    s.clock += E.MANUAL_GRACE_MS + 1;
    E.dropGround("mine", "stone", 1, 500, 500);
    check("after the grace window they evict like raw (back to 600)", a.ground.length === 600 && count(a, "wood") === 549,
      "len=" + a.ground.length + " wood=" + count(a, "wood"));
    // hard ceiling: only untouchables left -> the oldest go, older batches first
    a.ground = [];
    for (let i = 0; i < 17; i++) E.dropGround("mine", "wood", 50, 500, 500, "manual");   // 850, all in grace
    const firstNew = a.nextGroundId;
    E.dropGround("mine", "leaves", 100, 500, 500, "manual");
    check("hard ceiling 900: oldest older-batch items go, this call's 100 kept", a.ground.length === 900 &&
      count(a, "leaves") === 100 && a.ground[a.ground.length - 1].id === firstNew + 99, "len=" + a.ground.length);
  }

  // ---- (2) output back-pressure -------------------------------------------
  {
    const s = boot(undefined, true); const a = blankMine(s); const E = s.ENGINE;
    const bench = put(s, "mine", "workbench", 40, 40, { recipe: 0, stock: { wood: 20 * 3 } });
    let peak = 0;
    for (let t = 0; t < 120000; t += 50) {
      E.gameTick(); s.clock += 50;
      bench.stock.wood = Math.max(bench.stock.wood, 9);           // endless input
      peak = Math.max(peak, count(a, "plank"));
    }
    check("converter stops at the output pile cap (12 + one batch)", peak >= E.OUTPUT_PILE_MAX && peak <= E.OUTPUT_PILE_MAX + 1,
      "peak planks=" + peak);
    const st = E.buildingStatus("mine", bench);
    check("status: {state:'full', item:'plank', label:'Output pile full'}", st && st.state === "full" && st.item === "plank" &&
      st.label === "Output pile full", JSON.stringify(st));
    // a pile FAR away doesn't count
    a.ground.forEach(g => { g.x += 20 * 32; });
    for (let t = 0; t < 1000; t += 50) { E.gameTick(); s.clock += 50; }
    const st2 = E.buildingStatus("mine", bench);
    check("status back to working once the pile is gone", st2 && st2.state === "working", JSON.stringify(st2));
    for (let t = 0; t < 10000; t += 50) { E.gameTick(); s.clock += 50; bench.stock.wood = Math.max(bench.stock.wood, 9); }
    check("a pile moved > 3 cells away no longer blocks (crafting resumed)", count(a, "plank") > peak, "planks=" + count(a, "plank"));
    // starved + stale pile flag: 'Needs' wins (pile only blocks a ready converter)
    bench.stock = {}; bench._pileFull = true; bench._pileItem = "plank"; bench.smeltDoneAt = 0;
    const st3 = E.buildingStatus("mine", bench);
    check("a starved converter says Needs, not Output pile full", st3 && st3.state !== "full", JSON.stringify(st3));

    // pavilion: a full essence pile pauses cultivation and eats no buns
    const pav = put(s, "mine", "meditation_pavilion", 60, 60, { disciples: 2, buns: 10, nextCultivate: 0 });
    const c = E.buildingCenterPx(pav);
    E.dropGround("mine", "spirit_essence", 12, c.x, c.y + 60);
    run(s, 20000);
    const sp = E.buildingStatus("mine", pav);
    check("pavilion: full essence pile -> no buns eaten, status Output pile full", pav.buns === 10 && sp && sp.label === "Output pile full",
      "buns=" + pav.buns + " " + JSON.stringify(sp));
    a.ground = a.ground.filter(g => g.item !== "spirit_essence");
    run(s, 20000);
    check("pavilion resumes once the pile is collected", pav.buns < 10, "buns=" + pav.buns);

    // generator building (Algae Farm): back-pressure + status
    const s2 = boot(undefined, true); const a2 = blankMine(s2); const E2 = s2.ENGINE;
    const farm = put(s2, "mine", "algae_farm", 40, 40);
    run(s2, 5 * 60000);
    const near = E2.outputPileCount(a2, farm, "algae");
    const fs2 = E2.buildingStatus("mine", farm);
    check("Algae Farm stops at the pile cap and reports it", near >= 12 && near <= 13 && fs2 && fs2.label === "Output pile full",
      "near=" + near + " " + JSON.stringify(fs2));
  }

  // ---- (3) automation: per-type skip ---------------------------------------
  {
    const s = boot(undefined, true); const a = blankMine(s); const E = s.ENGINE;
    a.upgrades.automation = 3;
    const mk = (id, drops, r) => ({ id, row: r, col: 10, size: 1, kind: "x", spawnerKind: "x", interaction: "instant",
      tier: 1, hitsLeft: 1, regrowSec: 999, swingMs: 400, drops, autoFlash: 0 });
    a.nodes = [mk(a.nextNodeId++, [{ item: "stone", min: 3, max: 3 }, { item: "clay", min: 1, max: 1 }], 10),
               mk(a.nextNodeId++, [{ item: "iron_ore", min: 1, max: 1 }], 20)];
    E.dropGround("mine", "stone", E.AUTO_SKIP_LOOSE, 2000, 2000);
    const n = E.automationTick();
    check("a node is skipped when ANY yield type is saturated; others still run",
      n === 1 && JSON.stringify(a._autoSkip) === '["stone"]' && count(a, "iron_ore") === 1 && count(a, "clay") === 0,
      "n=" + n + " skip=" + JSON.stringify(a._autoSkip));
    // bot rare finds are raw (class 1) — they don't pin the protected share
    a.nodes = [Object.assign(mk(a.nextNodeId++, [], 30), { interaction: "break", rareDrop: { item: "firestone", chance: 1 } })];
    a.ground = [];
    E.automationTick();
    const fsn = a.ground.find(g => g.item === "firestone");
    check("a bot's rare find (vein firestone) is tagged gen -> class 1", !!fsn && fsn.gen === true && E.evictClassOf(fsn) === 1,
      JSON.stringify(fsn));
  }

  // ---- (4) capacity-aware stones, ejection, nomatch, untuned seal ----------
  {
    const s = boot(undefined, true); const a = blankMine(s); const E = s.ENGINE, CAP = s.DATA.BUILDINGS.storehouse.cap;
    const gs = put(s, "mine", "gathering_stone", 40, 40);
    gs.inv = [{ item: "stone", qty: 5 }, { item: "leaves", qty: 4 }, { item: "jade_shard", qty: 1 }];
    const shS = put(s, "mine", "storehouse", 40, 50, { item: "stone", lock: true, qty: CAP });
    const shJ = put(s, "mine", "storehouse", 50, 50, { item: "jade_shard", lock: true, qty: 0 });
    const lan = put(s, "mine", "wisp_lantern", 44, 44);
    lan.links.push({ from: gs.id, to: shS.id }, { from: gs.id, to: shJ.id });
    check("full stone Storehouse -> stone accepts only jade (capacity-aware)",
      JSON.stringify(E.stoneAccepts("mine", gs)) === '["jade_shard"]', JSON.stringify(E.stoneAccepts("mine", gs)));
    check("ever-set still includes stone", JSON.stringify(E.stoneAccepts("mine", gs, true)) === '["stone","jade_shard"]');
    E.gameTick();
    check("first links: the leaves no target could ever take are ejected as manual drops beside the stone",
      !gs.inv.some(x => x.item === "leaves") && gs.inv.some(x => x.item === "stone") && count(a, "leaves") === 4 &&
      a.ground.every(g => g.manualAt), JSON.stringify(gs.inv));
    run(s, 10000);
    check("ejected leaves are never re-collected", count(a, "leaves") === 4 && !gs.inv.some(x => x.item === "leaves"));
    s.GS.hand = [{ item: "leaves", qty: 1 }];
    check("a wired stone refuses a non-accepted item (endpointAccepts)", E.endpointAccepts(gs, "leaves") === false &&
      E.endpointAccepts(gs, "jade_shard") === true);
    // jade keeps flowing while the stone store is full
    const c = E.buildingCenterPx(gs);
    E.dropGround("mine", "jade_shard", 3, c.x + 90, c.y);
    E.dropGround("mine", "stone", 3, c.x - 90, c.y);
    run(s, 15000);
    check("full stone store: jade still collected + delivered, stone left on the ground",
      count(a, "jade_shard") === 0 && count(a, "stone") === 3 && shJ.qty >= 2, "jadeStore=" + shJ.qty + " stoneGround=" + count(a, "stone"));
    // nomatch link health
    const shW = put(s, "mine", "storehouse", 30, 30, { item: "wood", lock: true, qty: 0 });
    const lan2 = put(s, "mine", "wisp_lantern", 30, 36);
    const shX = put(s, "mine", "storehouse", 34, 30, { item: "clay", lock: true, qty: 4 });
    lan2.links.push({ from: shX.id, to: shW.id });
    s.clock += 2000; E.gameTick();
    check("link health: source holds only types the target never takes -> 'nomatch'",
      lan2.links[0]._stat && lan2.links[0]._stat.fail === "nomatch", JSON.stringify(lan2.links[0]._stat));
    // untuned seal
    const gs2 = put(s, "mine", "gathering_stone", 70, 70);
    const seal = put(s, "mine", "warding_seal", 70, 75);
    lan2.links.push({ from: gs2.id, to: seal.id });
    check("stone -> untuned Warding Seal: accepts anything (null)", E.stoneAccepts("mine", gs2) === null);
    seal.item = "wood"; seal.qty = s.DATA.BUILDINGS.warding_seal.seal.cap;
    check("tuned + full seal adds nothing (capacity-aware)", JSON.stringify(E.stoneAccepts("mine", gs2)) === "[]");
  }

  // ---- (5) generator rare drops inside the field cap -----------------------
  {
    const s = boot(undefined, true); initAll(s); const E = s.ENGINE, a = s.GS.areas.center, D = s.DATA, CELL = D.GRID.cell;
    a.buildings = a.buildings.filter(b => b.type === "center" || b.type === "dragon");
    a.ground = [];
    const gi = D.AREAS.center.generators.findIndex(g => g.kind === "stone");
    const gen = D.AREAS.center.generators[gi];
    const z = E.zoneRects(gen.zone)[0];
    const inField = it => a.ground.filter(g => g.item === it && g.x >= z.c0 * CELL - 16 && g.x <= (z.c1 + 1) * CELL + 16 &&
      g.y >= z.r0 * CELL - 16 && g.y <= (z.r1 + 1) * CELL + 16).length;
    run(s, 10 * 60000);
    const st = inField("stone"), jd = inField("jade_shard");
    check("quarry field: stone + jade together bounded (cap + idle rare <= 2)", st + jd <= gen.cap + 2 && jd <= gen.cap,
      "stone=" + st + " jade=" + jd);
    check("generator jade is tagged gen (raw)", a.ground.filter(g => g.item === "jade_shard").every(g => g.gen === true));
    // a field full of jade blocks the stone generator
    a.ground = [];
    E.dropGround("center", "jade_shard", gen.cap, (z.c0 + 2) * CELL, (z.r0 + 2) * CELL, "gen");
    a.genTimers = [];
    run(s, 30000);
    check("rare drops count toward the field cap (no stone while the field holds cap jade)", inField("stone") === 0,
      "stone=" + inField("stone"));
  }

  // ---- (6) occupiedCells includes built burner racks ------------------------
  {
    const s = boot(undefined, true); const a = blankMine(s); const E = s.ENGINE, D = s.DATA;
    const occ0 = E.occupiedCells("mine");
    const kiln = E.placeBuilding("mine", "kiln", 30, 30) || (() => { const b = put(s, "mine", "kiln", 30, 30, { built: false }); return b; })();
    const has = () => E.occupiedCells("mine").has("30,27") && E.occupiedCells("mine").has("31,29");
    check("ghost burner: rack cells not occupied yet", !has() && E.occupiedCells("mine") !== occ0);
    // finish the ghost through the real right-click path
    const need = E.buildingNeeds(kiln);
    const [lastItem] = Object.keys(need);
    for (const [it, q] of Object.entries(need)) kiln.paid[it] = (kiln.paid[it] || 0) + q - (it === lastItem ? 1 : 0);
    s.GS.hand = [{ item: lastItem, qty: 1 }];
    const memo = E.occupiedCells("mine");
    const c = E.buildingCenterPx(kiln);
    const r = E.dropFromHand("mine", c.x, c.y);
    check("ghost completed via dropFromHand", kiln.built === true, JSON.stringify(r));
    check("completion invalidates the memo: rack cells now occupied", E.occupiedCells("mine") !== memo && has());
    // spawner never lands a node on a rack cell
    const sp = { kind: "t", zone: "centre", sizes: [1], target: 999, interaction: "instant", drops: [] };
    let onRack = 0;
    const racks = E.rackCells("mine");
    for (let i = 0; i < 3000; i++) {
      const n = E.spawnFromSpawner("mine", sp);
      if (n && racks.has(n.row + "," + n.col)) onRack++;
    }
    check("node spawner never places on a built burner's rack", onRack === 0, "onRack=" + onRack);
    E.demolishBuilding("mine", kiln.id);
    check("demolish frees the rack cells", !E.occupiedCells("mine").has("30,27"));
  }

  // ---- (7) zero-input starter network: raw drops never vanish ---------------
  for (const test of [true, false]) {
    const s = boot(undefined, test); initAll(s); const E = s.ENGINE, a = s.GS.areas.center;
    E.setupStarterNetwork();
    a.upgrades.automation = 1;
    const mins = test ? 20 : 40;
    let maxLen = 0, crafted = 0;
    for (let m = 0; m < mins; m++) {
      for (let t = 0; t < 60000; t += 50) { E.gameTick(); s.clock += 50; if (t % 1000 === 0) E.automationTick(); }
      maxLen = Math.max(maxLen, a.ground.length);
    }
    crafted = s.GS.stats.totalCrafted;
    const tree = a.nodes.find(n => n.kind === "spirittree");
    const ids0 = new Set(a.ground.map(g => g.id));
    for (let i = 0; i < 9; i++) { E.harvestNode("center", tree.id, false); s.clock += 400; }
    const fresh = a.ground.filter(g => !ids0.has(g.id) && g.item === "wood").length;
    const label = test ? "TEST" : "REAL";
    check(label + " zero-input " + mins + " min: ground stays far below the cap", maxLen < 400, "max=" + maxLen);
    check(label + " zero-input: the player's tree harvest lands and stays", fresh >= 6, "fresh wood=" + fresh);
    check(label + " zero-input: automation not blocked by crafted litter (skips only saturated leaves)",
      Array.isArray(a._autoSkip) && a._autoSkip.every(t => t === "leaves"), JSON.stringify(a._autoSkip));
    const jadeSh = a.buildings.find(b => b.type === "storehouse" && b.item === "jade_shard");
    const j0 = jadeSh.qty;
    run(s, 5 * 60000);
    check(label + " zero-input: jade keeps flowing to its storehouse", jadeSh.qty > j0 || jadeSh.qty >= s.DATA.BUILDINGS.storehouse.cap,
      "jade " + j0 + " -> " + jadeSh.qty);
    a.ground = a.ground.filter(g => g.item !== "brick");
    const c0 = s.GS.stats.totalCrafted;
    run(s, 60000);
    check(label + " collecting the brick pile resumes the Kiln", s.GS.stats.totalCrafted > c0 && crafted > 0,
      "crafts +" + (s.GS.stats.totalCrafted - c0));
  }

  // ---- (8) save: gen tag persists, transient fields don't -------------------
  {
    const s = boot(undefined, true); initAll(s); const E = s.ENGINE, a = s.GS.areas.center;
    E.setupStarterNetwork();
    a.upgrades.automation = 1;
    run(s, 60000, true);
    E.dropGround("center", "jade_shard", 1, 500, 500, "gen");
    a.ground[a.ground.length - 1].gen = true;
    s.SAVE.saveState();
    const raw = s.savedRaw || "";
    check("saved JSON has no _autoSkip / _pileFull / _accEver / _pullAt", raw.length > 0 &&
      !/"_autoSkip"|"_pileFull"|"_pileAt"|"_accEver"|"_pullAt"/.test(raw));
    const s2 = boot(raw, true);
    const g2 = s2.GS.areas.center.ground.filter(g => g.item === "jade_shard" && g.gen);
    check("gen tag survives save/load as a boolean", g2.length >= 1 && g2.every(g => g.gen === true));
    const s3 = boot(raw.replace(/"gen":true/g, '"gen":"yes","crafted":true'), true);
    check("loadState: a crafted item never keeps gen (idempotent scrub)",
      s3.GS.areas.center.ground.every(g => !(g.crafted && g.gen)));
  }
} catch (e) {
  console.log("FAIL threw: " + (e && e.stack || e));
  fails++;
}

console.log(fails ? `\n${fails} FAILED` : "\nALL PASSED");
process.exit(fails ? 1 : 0);
