/* Bundle the whole game into ONE self-contained HTML file, for publishing
   as a claude.ai Artifact (the "always give the user a link" rule in
   HANDOFF.md — cloud sessions can't expose localhost, an Artifact link
   works on any device).

   Usage: node tools/build-artifact.js [outFile]   (default dist/idle-grounds.html)

   Notes for the Artifact tool: the file is page CONTENT (no <!DOCTYPE>/
   <html>/<head>/<body> — the artifact host wraps it). Everything is inlined,
   so the strict artifact CSP (no external requests) is satisfied. The game
   is deliberately single-theme (its own dark world). localStorage may be
   unavailable in the artifact sandbox — every save/load call site already
   try/catches, so the game still runs (just without autosave). */
const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "..");
const out = process.argv[2] || path.join(root, "dist", "idle-grounds.html");

const css = fs.readFileSync(path.join(root, "style.css"), "utf8");
const index = fs.readFileSync(path.join(root, "index.html"), "utf8");

// body content of index.html, minus the <script src> tags (inlined below)
const body = index
  .split(/<body>/i)[1]
  .split(/<script\s/i)[0]
  .trim();

// pixel-art item icons as data URIs (the artifact CSP blocks file requests;
// ui.js prefers window.ICON_DATA over the assets/icons/ paths when present)
const iconDir = path.join(root, "assets", "icons");
const icons = {};
if (fs.existsSync(iconDir))
  for (const f of fs.readdirSync(iconDir))
    if (f.endsWith(".png"))
      icons[f.slice(0, -4)] = "data:image/png;base64," +
        fs.readFileSync(path.join(iconDir, f)).toString("base64");
const iconScript = `<script>window.ICON_DATA = ${JSON.stringify(icons)};</script>`;

// same order as index.html — classic scripts sharing one global scope
const files = ["data.js", "state.js", "engine.js", "ui.js", "main.js"];
const scripts = files.map(f => {
  const src = fs.readFileSync(path.join(root, "js", f), "utf8");
  if (src.includes("</script")) throw new Error(`${f} contains "</script" — would break inlining`);
  return `<script>\n/* ---- js/${f} ---- */\n${src}\n</script>`;
}).join("\n");

const page = `<title>Idle Grounds — Prototype</title>
<style>
${css}
</style>
${body}
${iconScript}
${scripts}
`;

fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, page);
console.log(`bundled ${(page.length / 1024).toFixed(0)} KB -> ${out}`);
