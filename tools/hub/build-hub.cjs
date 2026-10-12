#!/usr/bin/env node
// Builds the Idle Grounds Hub: node tools/hub/build-hub.cjs <outDir>
const fs = require('fs'), path = require('path'), os = require('os');
const cp = require('child_process');

const root = path.resolve(__dirname, '..', '..');
const outDir = path.resolve(process.argv[2] || path.join(root, 'hub-out'));
const errors = [];
const readJson = (p, fallback) => {
  try { return JSON.parse(fs.readFileSync(p, 'utf8').replace(/^﻿/, '')); }
  catch (e) { if (fallback !== undefined) return fallback; errors.push('cannot read ' + p + ': ' + e.message); return null; }
};

const data = readJson(path.join(root, 'Assets/_Project/Data/Source/game-data.json'));
const plan = readJson(path.join(root, 'hub/plan.json'), null);
const questions = readJson(path.join(root, 'hub/questions.json'), []);
if (!data) { console.error(errors.join('\n')); process.exit(1); }

fs.mkdirSync(path.join(outDir, 'img'), { recursive: true });
fs.mkdirSync(path.join(outDir, 'q'), { recursive: true });

// --- game data trimmed to what the page needs
const slim = {
  ITEM_NAMES: data.ITEM_NAMES, ITEM_ICONS: data.ITEM_ICONS, BUILDINGS: data.BUILDINGS,
  AREAS: {}, SOURCES: data.SOURCES, REVEAL: data.REVEAL, DRAGON_STAGES: data.DRAGON_STAGES,
  UPGRADE_TREE: (data.UPGRADE_TREE || []).map(u => ({ id: u.id, icon: u.icon, name: u.name, area: u.area, desc: u.desc, costs: u.costs })),
  PERKS: data.PERKS, VOWS: data.VOWS,
  QUESTS: (data.QUESTS || []).map(q => ({ id: q.id, icon: q.icon, name: q.name, desc: q.desc, reward: q.reward })),
  imgs: { items: {}, buildings: {} }
};
for (const k in data.AREAS) { const a = data.AREAS[k]; slim.AREAS[k] = { name: a.name, icon: a.icon, verb: a.verb, base: a.base }; }

// --- real art, only when present
let itemImgs = 0, bldImgs = 0;
const copyIf = (src, dst) => { if (fs.existsSync(src)) { fs.copyFileSync(src, dst); return true; } return false; };
for (const k in data.ITEM_NAMES) {
  if (copyIf(path.join(root, 'Assets/_Project/Art/Incoming/items', 'item_' + k + '.png'), path.join(outDir, 'img', 'item_' + k + '.png'))) { slim.imgs.items[k] = 'img/item_' + k + '.png'; itemImgs++; }
}
for (const k in data.BUILDINGS) {
  if (copyIf(path.join(root, 'Assets/_Project/Art/Incoming/buildings', 'bld_' + k + '.png'), path.join(outDir, 'img', 'bld_' + k + '.png'))) { slim.imgs.buildings[k] = 'img/bld_' + k + '.png'; bldImgs++; }
}

// --- question images
let qImgs = 0;
for (const q of questions) {
  if (!q.imgs) continue;
  for (const im of q.imgs) {
    const src = path.join(root, 'hub', im.file);
    const name = path.basename(im.file);
    if (fs.existsSync(src)) { fs.copyFileSync(src, path.join(outDir, 'q', name)); qImgs++; }
    else errors.push('question image missing: ' + im.file);
    im.src = 'q/' + name;
  }
}

// --- template -> index.html (function replacers: no $-pattern surprises)
let html = fs.readFileSync(path.join(__dirname, 'template.html'), 'utf8');
const BS = String.fromCharCode(92);
const safe = o => JSON.stringify(o).replace(/</g, BS + 'u003c').replace(new RegExp('[' + BS + 'u2028' + BS + 'u2029]', 'g'), c => BS + 'u' + c.charCodeAt(0).toString(16));
for (const [ph, val] of [['/*DATA*/null', slim], ['/*PLAN*/null', plan], ['/*QUESTIONS*/[]', questions]]) {
  if (!html.includes(ph)) errors.push('template placeholder missing: ' + ph);
  html = html.replace(ph, () => safe(val));
}
fs.writeFileSync(path.join(outDir, 'index.html'), html);

// --- checks: script syntax + referenced files
const scripts = [...html.matchAll(/<script(?![^>]*\bsrc=)[^>]*>([\s\S]*?)<\/script>/g)].map(m => m[1]);
scripts.forEach((s, i) => {
  const tmp = path.join(os.tmpdir(), 'hub-check-' + process.pid + '-' + i + '.js');
  fs.writeFileSync(tmp, s);
  const r = cp.spawnSync(process.execPath, ['--check', tmp], { encoding: 'utf8' });
  fs.unlinkSync(tmp);
  if (r.status !== 0) errors.push('script ' + i + ' failed node --check:\n' + r.stderr);
});
if (!scripts.length) errors.push('no inline script found');
const refs = new Set();
for (const m of html.matchAll(/["'(]((?:img|q)\/[A-Za-z0-9_.\-]+\.(?:png|jpg|jpeg|webp|gif|svg))/g)) refs.add(m[1]);
for (const r of refs) if (!fs.existsSync(path.join(outDir, r))) errors.push('referenced file missing: ' + r);
if (/<!doctype|<html[ >]|<head[ >]|<body[ >]/i.test(html.replace(/<script[\s\S]*?<\/script>/g, ''))) errors.push('template contains forbidden document tags');

// --- report
let total = 0;
(function walk(d) { for (const f of fs.readdirSync(d)) { const p = path.join(d, f); const s = fs.statSync(p); s.isDirectory() ? walk(p) : (total += s.size); } })(outDir);
const nB = Object.keys(data.BUILDINGS).length, nI = Object.keys(data.ITEM_NAMES).length;
const unmapped = Object.keys(data.ITEM_NAMES).filter(k => !data.SOURCES[k]);
console.log(`items ${nI} (art ${itemImgs}), buildings ${nB} (art ${bldImgs}), question images ${qImgs}, referenced files ${refs.size}`);
console.log(`total size ${(total / 1024 / 1024).toFixed(2)} MB; items with no SOURCES entry: ${unmapped.join(', ') || 'none'}`);
if (total > 3 * 1024 * 1024) errors.push('output exceeds 3 MB');
if (errors.length) { console.error('HUB BUILD FAILED:\n' + errors.join('\n')); process.exit(1); }
console.log('OK ->', path.join(outDir, 'index.html'));
