// Builds Assets/_Project/Art/Sprites/Emoji/emoji_atlas.png + emoji_atlas.json
// from emoji-manifest.json using headless Edge. Node 24, no deps.
// Usage: node tools/emoji-atlas/build.js
const fs = require("fs");
const path = require("path");
const os = require("os");
const { execFileSync } = require("child_process");
const { pathToFileURL } = require("url");

const CELL = 128, COLS = 16, FONT_PX = 104;
const EDGE = process.env.EDGE_PATH || "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const root = path.resolve(__dirname, "..", "..");
const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, "emoji-manifest.json"), "utf8"));
const outDir = path.join(root, "Assets", "_Project", "Art", "Sprites", "Emoji");
fs.mkdirSync(outDir, { recursive: true });

const rows = Math.ceil(manifest.length / COLS);
const W = COLS * CELL, H = rows * CELL;
if (W > 4096) throw new Error("atlas too wide: " + W);

const cells = manifest.map((m, i) => {
  const x = (i % COLS) * CELL, y = Math.floor(i / COLS) * CELL;
  return `<div class="c" style="left:${x}px;top:${y}px${m.color ? ";color:" + m.color : ""}">${m.emoji}</div>`;
}).join("\n");
const html = `<!doctype html><html><head><meta charset="utf-8"><style>
html,body{margin:0;padding:0;background:transparent;overflow:hidden}
body{width:${W}px;height:${H}px;position:relative}
.c{position:absolute;width:${CELL}px;height:${CELL}px;display:flex;align-items:center;justify-content:center;
font-family:"Segoe UI Emoji","Noto Color Emoji",sans-serif;font-size:${FONT_PX}px;line-height:${CELL}px;text-align:center;overflow:hidden}
</style></head><body>
${cells}
</body></html>`;

const tmp = fs.mkdtempSync(path.join(os.tmpdir(), "emoji-atlas-"));
const htmlPath = path.join(tmp, "atlas.html");
fs.writeFileSync(htmlPath, html, "utf8");
const png = path.join(outDir, "emoji_atlas.png");
if (fs.existsSync(png)) fs.unlinkSync(png);

execFileSync(EDGE, [
  "--headless", "--no-sandbox", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
  "--default-background-color=00000000", `--window-size=${W},${H}`,
  `--user-data-dir=${path.join(tmp, "profile")}`,
  `--screenshot=${png}`, pathToFileURL(htmlPath).href,
], { stdio: "inherit", timeout: 120000 });

for (let i = 0; i < 100 && !fs.existsSync(png); i++) Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 200);
if (!fs.existsSync(png)) throw new Error("Edge did not produce " + png);
// verify PNG dimensions from IHDR
const b = fs.readFileSync(png);
const pw = b.readUInt32BE(16), ph = b.readUInt32BE(20);
console.log(`PNG ${pw}x${ph} (expected ${W}x${H})`);
if (pw !== W || ph !== H) console.warn("WARNING: size mismatch (headless window chrome?)");

const json = {
  cell: CELL, cols: COLS, width: pw, height: ph,
  sprites: manifest.map((m, i) => ({ key: m.key, x: i % COLS, y: Math.floor(i / COLS) })),
};
fs.writeFileSync(path.join(outDir, "emoji_atlas.json"), JSON.stringify(json, null, 1));
try { fs.rmSync(tmp, { recursive: true, force: true, maxRetries: 5, retryDelay: 300 }); } catch (e) { /* temp profile still locked; harmless */ }
console.log(`wrote ${manifest.length} sprites, ${COLS} cols x ${rows} rows`);
