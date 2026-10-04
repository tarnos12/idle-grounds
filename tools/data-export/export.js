// Exports old-game/js/data.js (window.DATA) to JSON for the Unity importer.
// Functions (quest goals etc.) are emitted as {"$fn": "<source>"} so nothing is silently lost.
// Usage: node tools/data-export/export.js [out.json]
const fs = require('fs'), path = require('path'), vm = require('vm');
const root = path.join(__dirname, '..', '..');
const src = fs.readFileSync(path.join(root, 'old-game/js/data.js'), 'utf8');
const ctx = { window: {}, Math, console };
vm.createContext(ctx);
vm.runInContext(src, ctx);
const DATA = ctx.window.DATA;
const json = JSON.stringify(DATA, (k, v) => typeof v === 'function' ? { $fn: v.toString() } : v, 2);
const out = process.argv[2] || path.join(root, 'Assets/_Project/Data/Source/game-data.json');
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, json);
console.log('wrote', out, (json.length / 1024).toFixed(1) + ' KB; top-level keys:', Object.keys(DATA).join(', '));
