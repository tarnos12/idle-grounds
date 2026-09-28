#!/usr/bin/env node
/* Regression: v52 slice C — hand & input feel (engine-testable parts).
   ENGINE.handRotate (Q / E), suctionStep's type-lock filter, and
   dropFromHand's `noGround` flag used by the latched right-hold.
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
} catch (err) {
  console.log("FAIL threw: " + (err && err.stack || err));
  fails++;
}

console.log(fails ? `\n${fails} FAILED` : "\nALL PASS");
process.exit(fails ? 1 : 0);
