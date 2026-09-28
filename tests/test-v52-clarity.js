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
} catch (e) {
  console.log("FAIL harness threw — " + ((e && e.stack) || e));
  fails++;
}

console.log(fails ? `\n${fails} FAILED` : "\nall passed");
process.exit(fails ? 1 : 0);
