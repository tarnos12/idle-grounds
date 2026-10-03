#!/usr/bin/env node
/* v52 slice E (clarity): the pure/static parts of the readability pass.
   ui.js needs a DOM, so its pure helpers (wrapLines, lblPx) are extracted by
   source and evaluated in a vm; help copy / data are checked as text.
   Usage: node test-v52-clarity.js [repoRoot]   (default /home/user/idle-grounds)
   Exits 1 on any FAIL. */
"use strict";
const fs = require("fs"), path = require("path"), vm = require("vm");

const REPO = process.argv[2] || "/home/user/idle-grounds";
const read = f => fs.readFileSync(path.join(REPO, f), "utf8");

let fails = 0;
const check = (name, ok, info) => {
  console.log((ok ? "PASS " : "FAIL ") + name + (info !== undefined ? "  — " + info : ""));
  if (!ok) fails++;
};

// pull a top-level `function name(...) { ... }` (brace-matched) out of a source
function extractFn(src, name) {
  const i = src.indexOf("function " + name + "(");
  if (i < 0) throw new Error("missing function " + name);
  let d = 0, j = src.indexOf("{", i);
  for (let k = j; k < src.length; k++) {
    if (src[k] === "{") d++;
    else if (src[k] === "}" && --d === 0) return src.slice(i, k + 1);
  }
  throw new Error("unbalanced " + name);
}

try {
  const ui = read("js/ui.js"), data = read("js/data.js");

  // -- helpers evaluated in isolation
  const sb = {};
  vm.createContext(sb);
  vm.runInContext("const MIN_LABEL_PX = 10;\n" + extractFn(ui, "lblPx") + "\n" + extractFn(ui, "wrapLines"), sb);
  const lblPx = (a, b) => vm.runInContext(`lblPx(${a}, ${b})`, sb);
  const wrap = (t, n, m) => vm.runInContext(`wrapLines(${JSON.stringify(t)}, ${n}, ${m})`, sb);

  check("lblPx: never below 10 CSS px at zoom-out (s=0.4)", lblPx(9.5, 0.4) === 10, String(lblPx(9.5, 0.4)));
  check("lblPx: scales up at zoom-in (s=1.6)", Math.abs(lblPx(10, 1.6) - 16) < 1e-9, String(lblPx(10, 1.6)));

  const long = "I dreamed a thousand years of quiet green hills and slow rivers, and you woke me with patience, not swords, little cultivator; the scales are yours to keep and the fires are yours to tend.";
  const lines = wrap(long, 42, 3);
  check("wrapLines: <= 3 lines", lines.length <= 3, String(lines.length));
  check("wrapLines: every line <= 42 chars", lines.every(l => l.length <= 42), lines.map(l => l.length).join(","));
  check("wrapLines: truncated text ends with an ellipsis", lines[lines.length - 1].endsWith("…"), lines[lines.length - 1]);
  const short = wrap("The dragon stirs.", 42, 3);
  check("wrapLines: short text is one untouched line", short.length === 1 && short[0] === "The dragon stirs.", JSON.stringify(short));
  const giant = wrap("x".repeat(100), 42, 3);
  check("wrapLines: an overlong single word is clipped to the width", giant.length === 1 && giant[0].length <= 42, JSON.stringify(giant.map(l => l.length)));

  // -- data: iron vein no longer uses the settings-gear glyph
  const vein = /kind: "ironvein"[^}]*?sprite: "([^"]+)"/.exec(data);
  check("iron vein sprite is not the settings gear", !!vein && !/⚙/.test(vein[1]), vein ? vein[1] : "no match");

  // -- Help copy matches current mechanics
  const help = ui.slice(ui.indexOf("function openHelp"), ui.indexOf("function closeHelp"));
  check("help: no 'orange' fuel gauge wording", !/orange/i.test(help));
  check("help: exactly one fuel section", (help.match(/Fuel racks/g) || []).length === 1);
  check("help: no duplicated 'Burner fuel racks' section", !/Burner fuel racks/.test(help));
  check("help: Forge smelts iron ore with fuel wording", /iron ore/.test(help) && /FUEL/.test(help) && !/iron ore \+ wood/.test(help));
  check("help: has a Troubleshooting logistics section", /Troubleshooting logistics/.test(help));
  check("help: mentions Q / E hand rotate", /Q \/ E/.test(help));
  check("help: no stale +8% ascension speed", !/\+8%/.test(help));
  check("help: +20% world speed per ascension", /\+20% world speed per ascension/.test(help));
  check("help: Legacy perk group + Vows mentioned", /Legacy/.test(help) && /Vows/.test(help));

  // ================= v52 fix wave (clarity) =================
  const css = read("style.css");

  // -- (1) unlock buttons stay out from under the quest panel; own button when centred in a locked region
  const pub = extractFn(ui, "positionUnlockButtons");
  check("unlock: positionUnlockButtons measures #quest-panel", /getBoundingClientRect/.test(pub) && /quest-panel/.test(pub));
  check("unlock: repositioned on resize, quest panel change and button rebuild",
    /positionUnlockButtons\(\);\s*\n\s*if \(upgradesOpen\)/.test(ui) && /ResizeObserver/.test(ui) &&
    /positionUnlockButtons/.test(extractFn(ui, "renderQuestPanel")) && /positionUnlockButtons/.test(extractFn(ui, "renderUnlockButtons")));
  check("unlock: camera inside a locked frontier region shows that region's own button ('here', centred)",
    /unlockButtonEl\(unlockCentreKey, "here"\)/.test(ui) && /\.edge-arrow\.here\s*\{[^}]*top:\s*50%[^}]*left:\s*50%/.test(css));

  // -- (2) automation-skip marker (replaces 'Automation paused')
  check("marker: no 'Automation paused' text left in ui.js", !/Automation paused/.test(ui));
  check("marker: compact 'Skipping' chip at the region's top-left (icons kept)", /"Skipping"/.test(ui) && /drawAutoSkip/.test(ui) && !/too many lying around/.test(ui));
  {
    const sb2 = {}; vm.createContext(sb2);
    vm.runInContext(extractFn(ui, "autoSkipOf"), sb2);
    const f = a => vm.runInContext(`JSON.stringify(autoSkipOf(${a}))`, sb2);
    check("autoSkipOf: absent/empty/garbage -> []", f("undefined") === "[]" && f("{}") === "[]" && f("{_autoSkip:[]}") === "[]" && f("{_autoSkip:'x'}") === "[]" && f("null") === "[]");
    check("autoSkipOf: returns the item keys", f("{_autoSkip:['wood','leaves']}") === '["wood","leaves"]');
  }

  // -- (3) converter status wording, drawn width-aware
  const dsl = extractFn(ui, "drawStatusLine");
  check("status: 'Output pile full' and 'Stock full' labels", /Output pile full/.test(dsl) && /Stock full/.test(dsl) && !/Queue full/.test(ui));
  check("status: bare N/min alternative when the box is narrow", /`Working \\u00b7 \$\{n\}\/min`, `\$\{n\}\/min`/.test(dsl) && /maxW/.test(dsl));
  check("status: ellipsizes to the building width", /\\u2026/.test(dsl));
  check("status: rate read only for display (>=2 crafts)", /craftRate\(b, true\)/.test(dsl));

  // -- (4) converter header label never below 10 CSS px
  const dcf = extractFn(ui, "drawConverterFace");
  check("header label: font from lblPx (>=10 px), ellipsis instead of shrinking", /const fpx = lblPx\(9\.5, s\)/.test(dcf) && !/fpx \*=/.test(dcf) && /\\u2026|…/.test(dcf));

  // -- (5) gate pulse throttled, subtitle shortened
  check("gate: subtitle is 'Ascend · +N ☯'", /`Ascend \\u00b7 \+\$\{/.test(ui) && !/Click to ascend/.test(ui));
  check("gate: pulse repaints ~every 150 ms, not every frame",
    /GATE_PULSE_MS = 150/.test(ui) && !/bCfg?\.gate|\.gate\b/.test(extractFn(ui, "animActive")) && /gateOnScreen\(\)\) scheduleSlowPaint/.test(ui));

  check("gate: the 50 ms tick gate (needsLiveRepaint) does not include the gate pulse", /function needsLiveRepaint\(\) \{ return animActive\(\); \}/.test(ui));

  // -- (6) Furnace Spirit reach circle == engine stoke test
  const eng = read("js/engine.js");
  const eR = /const R = bCfg\.stoker\.radius \* CELL;[\s\S]*?> R \+ ([0-9.]+) \* CELL/.exec(eng);
  const uS = /STOKE_SLACK_CELLS = ([0-9.]+)/.exec(ui);
  check("spirit reach: drawn slack == engine's slack", !!eR && !!uS && eR[1] === uS[1], (eR && eR[1]) + " vs " + (uS && uS[1]));
  check("spirit reach: reachOf draws radius + slack", /stoker \? \{ r: stokeReach\(cfg\)/.test(ui));

  // -- (7) help copy
  check("help: Gathering Stone capacity interpolated from DATA (60)", /gathering_stone\.gather\.cap/.test(help) && !/holds 20/.test(help) && /gather: \{ radius: 8, cap: 60 \}/.test(data));
  check("help: Furnace Spirit capacity interpolated (20)", /furnace_spirit\.stoker\.cap/.test(help) && /stoker: \{ radius: 3, cap: 20 \}/.test(data));
  check("help: AP = 3 + 2 per region beyond the Center + offerings x vows", /3, \+2 per region beyond the Center/.test(help) && /2 of each count/.test(help) && /vows/.test(help) && !/one per region unlocked/.test(help));
  check("help: burners with racks are Kiln, Forge, Pill Furnace, Star Anvil (no Cauldron)", /Kiln, Forge, Pill Furnace, Star Anvil/.test(help) && !/Cauldron…/.test(help));
  check("help: output pile back-pressure, automation skip, unlock installments, refused links",
    /Output pile full/.test(help) && /Automation skipping/.test(help) && /installments/.test(help) && /link is refused/.test(help));
  check("help: no stale 'Queue full' / 'Littered ground'", !/Queue full/.test(help) && !/Littered ground/.test(help));

  // -- (8) data numbers
  check("data: jadevein target 1", /kind: "jadevein"[^}]*?target: 1,/.test(data));
  check("data: Moon Elixir + Astral Steel output 2", /Moon Elixir[^\n]*outputQty: 2/.test(data) && /Astral Steel[^\n]*outputQty: 2/.test(data));

  // -- craftRate: hidden until >=2 crafts; over min(elapsed since first, 60 s)
  {
    let T = 1e9;
    const s3 = {}; s3.window = s3; s3.console = console; s3.Math = Math; s3.JSON = JSON;
    s3.Date = { now: () => T }; s3.localStorage = { getItem: () => null, setItem() {}, removeItem() {} };
    vm.createContext(s3);
    for (const f of ["data.js", "state.js", "engine.js"]) vm.runInContext(read("js/" + f), s3, { filename: f });
    const E = s3.ENGINE, b = {};
    // stamp through the engine's own noteCraft (internal, reached via the ring the same way)
    const stamp = () => vm.runInContext("noteCraft", s3)(b, T);
    check("craftRate: no crafts -> 0", E.craftRate(b, true) === 0);
    stamp();
    check("craftRate: 1 craft is hidden for display", E.craftRate(b, true) === 0);
    T += 10000; stamp();
    const r2 = E.craftRate(b, true);
    check("craftRate: 2 crafts 10 s apart -> 6/min, not diluted by the 60 s window", Math.abs(r2 - 6) < 0.01, String(r2));
    for (let i = 0; i < 4; i++) { T += 10000; stamp(); }
    check("craftRate: 6 crafts over 50 s -> 6/min", Math.abs(E.craftRate(b, true) - 6) < 0.01, String(E.craftRate(b, true)));
    for (let i = 0; i < 6; i++) { T += 10000; stamp(); }
    check("craftRate: full window -> crafts in the last 60 s (6-7)", E.craftRate(b, true) >= 6 && E.craftRate(b, true) <= 7, String(E.craftRate(b, true)));
    T += 200000;
    check("craftRate: long idle -> 0", E.craftRate(b, true) === 0);
    stamp(); T += 5000; stamp();
    check("craftRate: restarts cleanly after an idle gap (2 crafts 5 s apart -> 12/min)", Math.abs(E.craftRate(b, true) - 12) < 0.01, String(E.craftRate(b, true)));
  }
} catch (e) {
  console.log("FAIL harness threw — " + ((e && e.stack) || e));
  fails++;
}

console.log(fails ? `\n${fails} FAILED` : "\nall passed");
process.exit(fails ? 1 : 0);
