#!/usr/bin/env node
/* Regression: v52 slice C — hand & input feel (engine-testable parts).
   ENGINE.handRotate (Q / E), suctionStep's type-lock filter, and
   dropFromHand's `noGround` flag used by the latched right-hold.
   Fix wave: consumables report `once` (one per press), the hold-chop's
   `held` flag (no AUTO badge, keeps the sound), the node-grace refresh +
   8s fixture grace, and static checks of the ui.js hold-loop wiring.
   Standalone vm sandbox — loads js/data.js, js/state.js, js/engine.js.
   Usage: node tests/test-v52-input.js [repoRoot]   (default /home/user/idle-grounds)
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

function boot() {
  const s = {}; s.window = s; s.console = console; s.Date = Date; s.Math = Math; s.JSON = JSON;
  s.location = { reload() {} };
  s.localStorage = { getItem: () => null, setItem: () => {}, removeItem: () => {} };
  vm.createContext(s);
  vm.runInContext(fs.readFileSync(JS("data.js"), "utf8"), s, { filename: "data.js" });
  for (const f of ["state.js", "engine.js"])
    vm.runInContext(fs.readFileSync(JS(f), "utf8"), s, { filename: f });
  s.onGroundDrop = null; s.onSfx = null;
  return s;
}
// virtual-clock boot (Date.now() reads s.clock) for the grace-window checks
function bootClock() {
  const s = {}; s.window = s; s.console = console; s.Math = Math; s.JSON = JSON;
  s.clock = 1.7e12; s.Date = { now: () => s.clock };
  s.location = { reload() {} };
  s.localStorage = { getItem: () => null, setItem: () => {}, removeItem: () => {} };
  vm.createContext(s);
  vm.runInContext(fs.readFileSync(JS("data.js"), "utf8"), s, { filename: "data.js" });
  for (const f of ["state.js", "engine.js"])
    vm.runInContext(fs.readFileSync(JS(f), "utf8"), s, { filename: f });
  s.onGroundDrop = null; s.onSfx = null;
  return s;
}
const handStr = s => s.GS.hand.map(h => h.item + ":" + h.qty).join(",");
const setHand = (s, stacks) => { s.GS.hand.length = 0; for (const [item, qty] of stacks) s.GS.hand.push({ item, qty }); };

try {
  // (a) handRotate ---------------------------------------------------------
  {
    const s = boot(), E = s.ENGINE;
    check("ENGINE.handRotate exported", typeof E.handRotate === "function");
    setHand(s, [["wood", 3], ["stone", 2], ["clay", 1]]);
    let f = E.handRotate(1);
    check("handRotate(+1): front stack goes to the back", handStr(s) === "stone:2,clay:1,wood:3" && f === "stone", handStr(s));
    f = E.handRotate(-1);
    check("handRotate(-1): back stack comes to the front", handStr(s) === "wood:3,stone:2,clay:1" && f === "wood", handStr(s));
    E.handRotate(-1);
    check("handRotate(-1) again", handStr(s) === "clay:1,wood:3,stone:2", handStr(s));
    const before = E.handTotal();
    for (let i = 0; i < 7; i++) E.handRotate(i % 2 ? -1 : 1);
    check("rotation never changes totals", E.handTotal() === before, String(E.handTotal()));

    setHand(s, [["wood", 5]]);
    const r1 = E.handRotate(1), r2 = E.handRotate(-1);
    check("1-stack hand: rotate is a no-op, returns the item", handStr(s) === "wood:5" && r1 === "wood" && r2 === "wood", handStr(s));

    setHand(s, []);
    check("empty hand: rotate returns null, hand stays empty",
      E.handRotate(1) === null && E.handRotate(-1) === null && s.GS.hand.length === 0);
  }

  // (b) suctionStep item filter ---------------------------------------------
  {
    const s = boot(), E = s.ENGINE;
    E.initArea("center");
    const area = s.GS.areas.center;
    setHand(s, []);
    area.ground = [];
    let id = 90000;
    const put = (item, x, y) => area.ground.push({ id: id++, item, x, y });
    const X = 400, Y = 400;
    put("wood", X + 5, Y); put("stone", X - 5, Y);          // both inside the 12px collect radius
    put("wood", X + 40, Y); put("stone", X - 40, Y);        // both in the pull field
    const r = E.suctionStep("center", X, Y, 64, "wood");
    check("filtered suction picks only the locked type", r.picked === 1 && E.handCount("wood") === 1 && E.handCount("stone") === 0,
      JSON.stringify(r) + " hand=" + handStr(s));
    const farStone = area.ground.find(g => g.item === "stone" && g.x < X - 20);
    check("filtered suction leaves other types unmoved", farStone && farStone.x === X - 40 && r.moved === 1,
      farStone && String(farStone.x));
    check("near stone left on the ground", area.ground.some(g => g.item === "stone" && g.x === X - 5));

    const r2 = E.suctionStep("center", X, Y, 64, null);
    check("null filter vacuums any type (today's behaviour)", r2.picked === 1 && E.handCount("stone") === 1,
      JSON.stringify(r2) + " hand=" + handStr(s));
    const r3 = E.suctionStep("center", X, Y, 64);
    check("omitted filter == any type", r3.moved === 2, JSON.stringify(r3));

    // full hand: no pull at all, filtered or not
    setHand(s, [["clay", s.GS.handCap]]);
    const r4 = E.suctionStep("center", X, Y, 64, "wood");
    check("full hand: filtered suction does nothing", r4.moved === 0 && r4.picked === 0, JSON.stringify(r4));
  }

  // (c) dropFromHand noGround (latched right-hold never spills) --------------
  {
    const s = boot(), E = s.ENGINE, D = s.DATA;
    for (const k of Object.keys(D.AREAS)) E.initArea(k);
    E.setupStarterNetwork();   // the starter lantern is a built no-feed building
    const area = s.GS.areas.center;
    area.ground = [];
    setHand(s, [["wood", 4], ["stone", 2]]);
    // an open ground point (no building): noGround refuses, plain drop drops
    let x = -1, y = -1;
    for (let r = 2; r < 40 && x < 0; r++) for (let c = 2; c < 40; c++)
      if (!E.buildingAt("center", r, c) && !area.nodes.some(n => r >= n.row && r < n.row + n.size && c >= n.col && c < n.col + n.size)) {
        x = c * 32 + 16; y = r * 32 + 16; break;
      }
    const n0 = E.handTotal();
    const rNo = E.dropFromHand("center", x, y, true);
    check("noGround: open ground returns null and keeps the hand", rNo === null && E.handTotal() === n0 && area.ground.length === 0,
      JSON.stringify(rNo));
    const rYes = E.dropFromHand("center", x, y);
    check("default: open ground drops the front item", rYes && rYes.dropped === "wood" && E.handTotal() === n0 - 1 && area.ground.length === 1,
      JSON.stringify(rYes));

    // a built building with no feed branch (lantern) must not spill under noGround
    const lan = area.buildings.find(b => b.built && D.BUILDINGS[b.type].lantern);
    if (lan) {
      const s2 = E.buildingSize(lan.type);
      const cx = (lan.col + s2.w / 2) * 32, cy = (lan.row + s2.h / 2) * 32;
      const g0 = area.ground.length, h0 = E.handTotal();
      const rl = E.dropFromHand("center", cx, cy, true);
      check("noGround on a built lantern: null, nothing spilled", rl === null && area.ground.length === g0 && E.handTotal() === h0,
        JSON.stringify(rl));
    } else check("starter lantern present for the noGround check", false);

    // a ghost still feeds with noGround set, and reports completion
    const type = Object.keys(D.BUILDINGS).find(t => {
      const c = D.BUILDINGS[t].cost || {}; return Object.keys(c).length === 1 && c.wood && !D.BUILDINGS[t].unique;
    });
    // v52 slice D gates placeBuilding on the progressive reveal (the
    // Workbench shows after the "build" quest) — reveal it the way play does.
    if (type && !E.isBuildingUnlocked(type)) s.GS.builtTypes = Object.assign(s.GS.builtTypes || {}, { [type]: true });
    if (type) {
      let placed = null;
      for (let r = 2; r < 60 && !placed; r++) for (let c = 2; c < 60 && !placed; c++)
        if (E.canPlaceBuilding("center", r, c, type) && E.placeBuilding("center", type, r, c))
          placed = area.buildings[area.buildings.length - 1];
      if (placed) {
        const need = E.buildingNeeds(placed).wood || 0;
        setHand(s, [["wood", need], ["stone", 5]]);
        const sz = E.buildingSize(type), gx = (placed.col + sz.w / 2) * 32, gy = (placed.row + sz.h / 2) * 32;
        let fed = 0, last = null;
        for (let i = 0; i < need + 3 && !placed.built; i++) { last = E.dropFromHand("center", gx, gy, true); if (last && last.fed) fed++; }
        check("noGround ghost feed completes the ghost", placed.built && fed === need && last && last.building === placed.id,
          `${type} fed=${fed}/${need}`);
        check("leftover stone stays in hand after completion (UI stops the hold there)", E.handCount("stone") === 5, handStr(s));
      } else check("could place a wood-only ghost for the feed check", false, type);
    } else console.log("SKIP ghost feed check — no single-wood-cost building in data");
  }
  // (d) consumables are one per press: dropFromHand reports `once` ----------
  {
    const s = boot(), E = s.ENGINE, D = s.DATA;
    for (const k of Object.keys(D.AREAS)) E.initArea(k);
    const area = s.GS.areas.center;
    area.ground = [];
    // Vitality Pill quaffed on open ground
    let x = -1, y = -1;
    for (let r = 2; r < 40 && x < 0; r++) for (let c = 2; c < 40; c++)
      if (!E.buildingAt("center", r, c) && !area.nodes.some(n => r >= n.row && r < n.row + n.size && c >= n.col && c < n.col + n.size)) {
        x = c * 32 + 16; y = r * 32 + 16; break;
      }
    setHand(s, [[D.VITALITY.item, 3], ["wood", 2]]);
    const rv = E.dropFromHand("center", x, y);
    check("Vitality Pill: used + once (ground-hold ends after one)", rv && rv.used && rv.once === true && E.handCount(D.VITALITY.item) === 2,
      JSON.stringify(rv));
    // a plain ground drop is NOT one-per-press
    setHand(s, [["wood", 3]]);
    const rw = E.dropFromHand("center", x, y);
    check("plain wood drop has no once flag (ground-hold repeats)", rw && rw.dropped === "wood" && !rw.once, JSON.stringify(rw));
    // Beast Bait inside the fox zone lures one boar and reports lured+once
    const ecfg = D.AREAS.center.enemies;
    const z = E.zoneRects(ecfg.zone)[0];
    const bx = (z.c0 + 1) * 32 + 16, by = (z.r0 + 1) * 32 + 16;
    setHand(s, [["beast_bait", 3]]);
    const e0 = area.enemies.length;
    const rb = E.dropFromHand("center", bx, by, true);
    check("Beast Bait lure: lured + once, one boar, one bait", rb && rb.lured === true && rb.once === true &&
      area.enemies.length === e0 + 1 && E.handCount("beast_bait") === 2, JSON.stringify(rb));
    // a dragon pill fed to the dragon: one blessing per press
    const pillKey = Object.keys(D.DRAGON_BUFFS)[0];
    const dragon = area.buildings.find(b => b.type === "dragon");
    if (dragon && pillKey) {
      dragon.built = true;
      const dc = E.buildingCenterPx(dragon);
      setHand(s, [[pillKey, 4]]);
      const rd = E.dropFromHand("center", dc.x, dc.y, true);
      check("dragon pill: fed + once, one pill spent", rd && rd.fed === pillKey && rd.once === true && E.handCount(pillKey) === 3,
        JSON.stringify(rd));
      // a tribute item fed to the dragon is NOT one-per-press
      const need = E.dragonRemaining ? Object.keys(E.dragonRemaining())[0] : null;
      if (need) {
        setHand(s, [[need, 2]]);
        const rt = E.dropFromHand("center", dc.x, dc.y, true);
        check("dragon tribute feed has no once flag", rt && rt.fed === need && !rt.once, JSON.stringify(rt));
      }
    } else check("dragon + dragon pill present in data", false);
  }

  // (e) harvestNode: isAuto (automation) vs held (player hold-chop) ----------
  {
    const s = boot(), E = s.ENGINE, D = s.DATA;
    for (const k of Object.keys(D.AREAS)) E.initArea(k);
    let sfx = 0; s.onSfx = k => { if (k === "harvest") sfx++; };
    const a = s.GS.areas.center;
    const mk = () => { const n = { id: a.nextNodeId++, row: 30, col: 30, size: 1, kind: "bush", interaction: "chop",
      tier: 1, hitsLeft: 5, regrowSec: 10, swingMs: 400, perHit: [], drops: [], autoFlash: 0 }; a.nodes.push(n); return n; };
    const n1 = mk();
    E.harvestNode("center", n1.id, false, true);
    check("held swing: harvest sound plays, no AUTO badge", sfx === 1 && !n1.autoFlash, `sfx=${sfx} autoFlash=${n1.autoFlash}`);
    const n2 = mk();
    E.harvestNode("center", n2.id, true);
    check("isAuto swing: AUTO badge, silent", sfx === 1 && n2.autoFlash > 0, `sfx=${sfx} autoFlash=${n2.autoFlash}`);
  }

  // (f) manual grace: fixture drops 8s, re-armed while the player keeps swinging
  {
    const s = bootClock(), E = s.ENGINE, D = s.DATA;
    for (const k of Object.keys(D.AREAS)) E.initArea(k);
    s.GS.world.unlocked.mine = true;
    const a = s.GS.areas.mine;
    a.nodes = []; a.ground = []; a.buildings = []; a.spawnQueue = []; a.wisps = []; a.genTimers = [];
    const gs = { id: a.nextBuildId++, type: "gathering_stone", row: 20, col: 20, paid: {}, built: true, item: null, qty: 0, inv: [] };
    a.buildings.push(gs);
    const run = ms => { for (let t = 0; t < ms; t += 50) { E.gameTick(); s.clock += 50; } };
    const fx = { id: a.nextNodeId++, row: 20, col: 24, size: 1, kind: "rock", interaction: "quarry", fixed: true,
      tier: 1, clicksPerDrop: 1, dropItem: "stone", swingMs: 400, autoFlash: 0 };
    a.nodes.push(fx);
    E.harvestNode("mine", fx.id, false);
    const g1 = a.ground[0];
    // park the drops between the stone and the rock: this checks the grace
    // clock, not the collider exit (an item behind a fixed node, on the far
    // side from a stone, is a separate ground-settle matter)
    const park = () => { for (const g of a.ground) { g.x = 700; g.y = 656; } };
    park();
    check("fixture drop: grace stamped 8s (manualAt = now + 4000), tagged _src", g1 && g1.manualAt === s.clock + 4000 && g1._src === fx.id,
      JSON.stringify(g1));
    run(6000);
    check("stone still ignores the fixture drop after 6s", E.gatherTotal(gs) === 0, "inv=" + E.gatherTotal(gs));
    E.harvestNode("mine", fx.id, false, true);   // the player keeps holding on the rock
    park();
    check("a later held swing re-arms the earlier drop's grace", g1.manualAt === s.clock + 4000 && a.ground.length === 2,
      `manualAt-clock=${g1.manualAt - s.clock} ground=${a.ground.length}`);
    run(7000);
    check("both drops still protected 7s after the last swing", E.gatherTotal(gs) === 0, "inv=" + E.gatherTotal(gs));
    run(2500);
    check("stone collects both once the 8s window from the last swing passes", E.gatherTotal(gs) === 2, "inv=" + E.gatherTotal(gs));
    // a regular node: 4s grace, and swinging it doesn't re-arm OTHER nodes' drops
    a.ground = [];
    const n = { id: a.nextNodeId++, row: 22, col: 22, size: 1, kind: "ore", interaction: "instant", tier: 1, hitsLeft: 1,
      regrowSec: 10, swingMs: 400, drops: [{ item: "stone", min: 1, max: 1 }], autoFlash: 0 };
    a.nodes.push(n);
    E.harvestNode("mine", n.id, false);
    const g2 = a.ground[0];
    check("regular node drop: 4s grace (manualAt = now)", g2 && g2.manualAt === s.clock && g2._src === n.id, JSON.stringify(g2));
    s.clock += 1000;
    E.harvestNode("mine", fx.id, false, true);
    check("swinging a different node leaves this drop's grace alone", g2.manualAt === s.clock - 1000, String(g2.manualAt - s.clock));
    // automation drops carry neither manualAt nor _src
    a.ground = [];
    const n3 = Object.assign({}, n, { id: a.nextNodeId++, hitsLeft: 1 });
    a.nodes = [n3]; a.upgrades.automation = 1;
    E.automationTick();
    check("automation drops: no manualAt, no _src", a.ground.length >= 1 && a.ground.every(g => !g.manualAt && g._src == null),
      JSON.stringify(a.ground));
    // _src never reaches a save
    a.ground = [{ id: 1, item: "stone", x: 10, y: 10, manualAt: 5, _src: 3 }];
    const saved = JSON.parse(JSON.stringify(s.GS, s.transientReplacer));
    check("save strips _src and manualAt", saved.areas.mine.ground[0]._src === undefined && saved.areas.mine.ground[0].manualAt === undefined,
      JSON.stringify(saved.areas.mine.ground[0]));
  }

  // (g) ui.js wiring (static — the hold loop needs a DOM) --------------------
  {
    const ui = fs.readFileSync(JS("ui.js"), "utf8");
    check("hold loop swings with held=true, not isAuto", /E\.harvestNode\(rg, n\.id, false, true\)/.test(ui) && !/E\.harvestNode\(rg, n\.id, true\)/.test(ui));
    check("feedHoldEnded ends on r.once and on a dragon stage change",
      /function feedHoldEnded[\s\S]{0,300}r\.once[\s\S]{0,200}GS\.dragon\.stage !== holdTarget\.stage/.test(ui));
    check("ground-hold drop ends the hold on r.once", /dropFromHand\(rg, cursor\.lx, cursor\.ly\)[\s\S]{0,120}r && r\.once\) holdFront = null/.test(ui));
    check("ground-hold pauses over a fuel rack (groundHoldBlocked via rackRedirect)",
      /function groundHoldBlocked[\s\S]{0,200}rackRedirect/.test(ui) && /!groundHoldBlocked\(rg, cursor\.lx, cursor\.ly\)/.test(ui));
    check("a visible modal ends a right-hold", /\.modal:not\(\.hidden\)"\)\) \{\s*holdDone = true; holdFront = null;/.test(ui));
    check("edge redirect: >1x1, min(10px, 15%), item within 14px, hand space",
      /function edgePickRedirect[\s\S]{0,500}s\.w <= 1 && s\.h <= 1[\s\S]{0,200}EDGE_PICK_FRAC[\s\S]{0,120}handSpace\(\) <= 0[\s\S]{0,80}EDGE_ITEM_PX/.test(ui));
    check("hand chip: innerHTML guarded by a contents signature; mousemove only moves it",
      /if \(sig === handChipSig\) return;/.test(ui) && /moveHandCursor\(\);\s*\/\/ position only/.test(ui));
  }
} catch (err) {
  console.log("FAIL threw: " + (err && err.stack || err));
  fails++;
}

console.log(fails ? `\n${fails} FAILED` : "\nALL PASS");
process.exit(fails ? 1 : 0);
