#!/usr/bin/env node
/* UI polish pass (design-pass follow-up): unlock-button right-click / partial
   state / repositioning, ghost pill clamp, offline rows, help copy, floater
   halo, hand-chip overlays, compact automation chip, offerings line.
   ui.js needs a DOM, so pure helpers are extracted by source and evaluated in
   a vm; wiring/copy is checked as source text.
   Usage: node test-ui-polish.js [repoRoot] */
"use strict";
const fs = require("fs"), path = require("path"), vm = require("vm");
const REPO = process.argv[2] || "/home/user/idle-grounds";
const read = f => fs.readFileSync(path.join(REPO, f), "utf8");
let fails = 0;
const check = (name, ok, info) => {
  console.log((ok ? "PASS " : "FAIL ") + name + (info !== undefined ? "  — " + info : ""));
  if (!ok) fails++;
};
function extractFn(src, name) {
  const i = src.indexOf("function " + name + "(");
  if (i < 0) throw new Error("missing function " + name);
  let d = 0;
  for (let k = src.indexOf("{", i); k < src.length; k++) {
    if (src[k] === "{") d++;
    else if (src[k] === "}" && --d === 0) return src.slice(i, k + 1);
  }
  throw new Error("unbalanced " + name);
}
// body of a function, for scoped source assertions
const body = (src, name) => extractFn(src, name);

try {
  const ui = read("js/ui.js"), css = read("style.css");

  // (2) unlockPayState: afford only when the hand covers ALL of the remainder
  {
    const sb = { window: { GS: { hand: [] } }, E: null };
    sb.E = { unlockRemaining: () => ({ wood: 10, stone: 2 }), areaUnlockCost: () => ({ wood: 10, stone: 2 }) };
    vm.createContext(sb);
    vm.runInContext(extractFn(ui, "unlockPayState"), sb);
    const st = hand => { sb.window.GS.hand = hand; return vm.runInContext('unlockPayState("farm")', sb); };
    check("payState: empty hand -> cant", st([]).state === "cant");
    check("payState: irrelevant item -> cant", st([{ item: "leaf", qty: 5 }]).state === "cant");
    const p = st([{ item: "wood", qty: 4 }]);
    check("payState: installment only -> partial 4/12", p.state === "partial" && p.have === 4 && p.need === 12, JSON.stringify(p));
    check("payState: one of two items fully covered is still partial", st([{ item: "wood", qty: 10 }]).state === "partial");
    check("payState: covers everything -> afford", st([{ item: "wood", qty: 10 }, { item: "stone", qty: 2 }]).state === "afford");
    check("payState: surplus -> afford", st([{ item: "wood", qty: 20 }, { item: "stone", qty: 9 }]).state === "afford");
    sb.E.unlockRemaining = () => null;
    check("payState: unknown region -> cant", st([{ item: "wood", qty: 4 }]).state === "cant");
  }

  // (1) right-click pays and never reaches the world's mousedown
  {
    const el = body(ui, "unlockButtonEl");
    check("unlock button: mousedown stops propagation", /addEventListener\("mousedown"[\s\S]*stopPropagation/.test(el));
    check("unlock button: mousedown button 2 calls the pay path", /e\.button === 2[\s\S]*payUnlockButton\(key\)/.test(el));
    check("unlock button: contextmenu stops propagation", /addEventListener\("contextmenu"[\s\S]*stopPropagation/.test(el));
    check("unlock button: left click uses the same pay path", /btn\.onclick = \(\) => payUnlockButton\(key\)/.test(el));
    check("help: 'click or right-click the unlock button to pay what your hand holds'",
      /click or right-click the unlock button to pay what your hand holds/.test(ui));
  }
  check("css: partial = dashed gold, dimmer than afford; .arr-pay label",
    /\.edge-arrow\.partial\s*\{[^}]*dashed[^}]*var\(--gold\)/.test(css) && /\.arr-pay/.test(css));

  // (3) repositioning hooks
  {
    check("positionUnlockButtons: right button slides left of the quest panel at tiny sizes",
      /classList\.contains\("right"\)[\s\S]*style\.right/.test(body(ui, "positionUnlockButtons")));
    check("positionUnlockButtons runs on link/recipe open + close",
      ["openRecipeMenu", "closeRecipeMenu", "closeLinkMenu"].every(f => /positionUnlockButtons\(\)/.test(body(ui, f))) &&
      /positionUnlockButtons\(\);\s*\n\s*requestGridPaint/.test(ui));
    check("positionUnlockButtons runs when the build strip opens/closes", /wasHidden === open\) positionUnlockButtons/.test(body(ui, "renderBuildMenu")));
    check("ResizeObserver watches the viewport too", /"#quest-panel", "#world-viewport"/.test(ui));
  }

  // (4) ghost pill clamped inside the view
  check("ghost pill: drawn above the ghost near the bottom edge, clamped in view",
    /ty \+ h > vh - 4\) ty = py - h - 4/.test(ui) && /ty = clamp\(ty, 4,/.test(ui));

  // (5) offline: no redundant "Bots paused" row
  {
    const sb = { DD: { AREAS: { farm: { name: "Farm" } } }, E: {}, fmtAway: n => n + "ms" };
    vm.createContext(sb);
    vm.runInContext(extractFn(ui, "stallRowsHTML"), sb);
    const html = vm.runInContext(`stallRowsHTML(${JSON.stringify({ elapsedMs: 1e6,
      stalls: [{ kind: "autopaused", areaKey: "farm", count: 9 }, { kind: "autoskip", areaKey: "farm", names: ["Rice"] }] })})`, sb);
    check("offline: no 'Bots paused' row", !/Bots paused/.test(html) && !/Bots paused/.test(ui));
    check("offline: the per-type skip row remains", /bots skipped Rice/.test(html));
  }

  // (6) help copy
  {
    check("help: hand cap is in the bottom bar", /cap shown in the bottom bar/.test(ui) && !/bottom-right\)/.test(ui));
    check("help: link-dot legend adds the amber 'nothing this target uses' state",
      /amber also means the source holds nothing this target uses/.test(ui));
    check("help: Center automation taps the Spirit Tree", /Center automation also taps the Spirit Tree/.test(ui));
    check("help: Remembered Paths opens Mine, then Fishing, then the Farm", /Remembered Paths opens the Mine, then Fishing, then the Farm/.test(ui));
    check("perk readout follows E.PATH_REGIONS", /E\.PATH_REGIONS \|\|/.test(ui.slice(ui.indexOf("paths:     ["))));
  }

  // (7) floaters: halo + full alpha for ~400 ms
  {
    const d = ui.slice(ui.indexOf("for (const f of floaters) {"), ui.indexOf("for (const f of floaters) {") + 1200);
    check("floaters: dark halo (strokeText) before the fill", /strokeText[\s\S]*fillText/.test(d));
    check("floaters: full alpha until 400 ms, then fades", /age < 400 \? Math\.min\(1, age \/ 60\)/.test(d));
  }

  // (8) hand chip hides over DOM overlays
  {
    check("hand chip: overlay selector covers the quest panel", /HAND_OVERLAYS = "[^"]*#quest-panel/.test(ui));
    check("hand chip: pointerenter/pointerleave wired + mousemove re-check",
      /pointerenter", \(\) => setOverlayHover\(true\)/.test(ui) && /pointerleave", \(\) => setOverlayHover\(false\)/.test(ui) &&
      /closest\(HAND_OVERLAYS\)/.test(ui));
    check("hand chip: hidden while overlayHover", /overlayHover \|\|/.test(body(ui, "renderHandCursor")));
    const z = css.match(/#hand-cursor\s*\{[^}]*z-index:\s*(\d+)/), qz = css.match(/#quest-panel\s*\{[^}]*z-index:\s*(\d+)/);
    check("hand chip: z-index below the quest panel", z && qz && +z[1] < +qz[1], z && qz ? z[1] + " < " + qz[1] : "");
  }

  // (9) compact automation chip at the region's top-left
  {
    const d = body(ui, "drawAutoSkip");
    check("skip chip: anchored at the region's top-left, not centred",
      /const x0 = l \+ 6/.test(d) && !/\(l \+ r\) \/ 2/.test(d));
    check("skip chip: still draws the skipped item icons", /drawItemIcon\(it/.test(d));
    check("skip chip: compact (no long sentence)", !/too many lying around/.test(d));
  }

  // (10) offerings line
  {
    const sb = { DD: { GATE_OFFERINGS: { items: ["talisman", "star_steel", "dragon_scale"] } }, iconHTML: it => "[" + it[0] + "]" };
    vm.createContext(sb);
    vm.runInContext(extractFn(ui, "offeringsHTML"), sb);
    const f = o => vm.runInContext(`offeringsHTML(${JSON.stringify(o)})`, sb);
    const full = f({ count: 6, cap: 6, perType: 2, offered: { talisman: 2, star_steel: 2, dragon_scale: 2 } });
    check("offerings: full -> 'Offerings 6/6 — full (+6 ☯)'", full === "Offerings 6/6 — full (+6 ☯)", full);
    const part = f({ count: 3, cap: 6, perType: 2, offered: { talisman: 1, star_steel: 2 } });
    check("offerings: per-type remainder", /\[t\] 1\/2 \[s\] 2\/2 \[d\] 0\/2/.test(part), part);
    check("offerings: invitation to right-click only while not full",
      /off\.count >= off\.cap \? "" : ` — right-click spares/.test(ui));
  }
} catch (e) {
  console.log("FAIL harness threw — " + ((e && e.stack) || e));
  fails++;
}
console.log(fails ? `\n${fails} FAILED` : "\nALL PASS");
process.exit(fails ? 1 : 0);
