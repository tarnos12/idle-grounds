#!/usr/bin/env node
// Builds the Idle Grounds Hub: node tools/hub/build-hub.cjs <outDir>
// Sources (all in docs/Design/): plan.json, questions.json, question-images/, islands.json; game data from Assets/_Project/Data/Source/game-data.json.
const fs = require('fs'), path = require('path'), os = require('os');
const cp = require('child_process');

const root = path.resolve(__dirname, '..', '..');
const outDir = path.resolve(process.argv[2] || path.join(root, 'hub-out'));
const DESIGN = path.join(root, 'docs', 'Design');
const errors = [];
const readJson = (p, fallback) => {
  try { return JSON.parse(fs.readFileSync(p, 'utf8').replace(/^﻿/, '')); }
  catch (e) { if (fallback !== undefined) return fallback; errors.push('cannot read ' + p + ': ' + e.message); return null; }
};

const data = readJson(path.join(root, 'Assets/_Project/Data/Source/game-data.json'));
const plan = readJson(path.join(DESIGN, 'plan.json'), null);
const questions = readJson(path.join(DESIGN, 'questions.json'), []);
const islands = readJson(path.join(DESIGN, 'islands.json'), null);
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
    const src = path.join(DESIGN, im.file);
    const name = path.basename(im.file);
    if (fs.existsSync(src)) { fs.copyFileSync(src, path.join(outDir, 'q', name)); qImgs++; }
    else errors.push('question image missing: ' + im.file);
    im.src = 'q/' + name;
  }
}

// --- plan + islands schema checks
const TASK_ST = ['done', 'in-progress', 'next', 'later', 'todo', 'blocked', 'waiting'];
if (plan) {
  for (const k of ['title', 'updated', 'statusMeaning', 'owners', 'milestones']) if (!plan[k]) errors.push('plan.json missing "' + k + '"');
  for (const m of plan.milestones || []) {
    for (const k of ['id', 'name', 'status', 'goal', 'exit']) if (!m[k]) errors.push('milestone ' + (m.id || '?') + ' missing "' + k + '"');
    if (m.status && !TASK_ST.includes(m.status)) errors.push('milestone ' + m.id + ' has unknown status ' + m.status);
    for (const t of m.tasks || []) {
      if (!TASK_ST.includes(t.status)) errors.push('task "' + t.title + '" has unknown status ' + t.status);
      if (plan.owners && !plan.owners[t.owner]) errors.push('task "' + t.title + '" has owner not in plan.owners: ' + t.owner);
      if ((t.status === 'blocked' || t.status === 'waiting') && !t.blockedBy) errors.push('task "' + t.title + '" is ' + t.status + ' without blockedBy');
    }
  }
}
if (islands) {
  const ids = new Set(islands.islands.map(i => i.id));
  for (const i of islands.islands) {
    if (!islands.biomes[i.biome]) errors.push('island ' + i.id + ': unknown biome ' + i.biome);
    if (i.pathFrom && i.pathFrom !== 'shrine' && !ids.has(i.pathFrom)) errors.push('island ' + i.id + ': unknown pathFrom ' + i.pathFrom);
    if (i.order > 0 && Object.keys(i.tribute || {}).length !== 5) errors.push('island ' + i.id + ': tribute is not 5 items');
    if (!i.npc || i.npc.requests.length !== 3) errors.push('island ' + i.id + ': NPC does not have 3 requests');
    for (const it of [...Object.keys(i.tribute || {}), ...i.npc.requests.flatMap(r => Object.keys(r.needs))]) if (!data.ITEM_NAMES[it] && !/^(spirit_lotus|reed|foundation_pill|thunderwood|storm_crystal|thunder_steel|soulvine|beast_core|soul_lamp|immortal_peach|tribulation_essence|immortal_pill)$/.test(it)) errors.push('island ' + i.id + ': unknown item ' + it);
  }
  const live = data.WORLD.islands;
  for (const k in live) { const i = islands.islands.find(x => x.id === k); if (!i) errors.push('islands.json lacks built island ' + k); else if (k !== 'celestial' && (i.col !== live[k].col || i.row !== live[k].row)) errors.push('island ' + k + ' position differs from game-data WORLD.islands'); }
} else errors.push('docs/Design/islands.json not readable');

// --- template -> index.html (function replacers: no $-pattern surprises)
let html = fs.readFileSync(path.join(__dirname, 'template.html'), 'utf8');
const BS = String.fromCharCode(92);
const safe = o => JSON.stringify(o).replace(/</g, BS + 'u003c').replace(new RegExp('[' + BS + 'u2028' + BS + 'u2029]', 'g'), c => BS + 'u' + c.charCodeAt(0).toString(16));
for (const [ph, val] of [['/*DATA*/null', slim], ['/*PLAN*/null', plan], ['/*ISLANDS*/null', islands], ['/*QUESTIONS*/[]', questions]]) {
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
console.log(`islands ${islands ? islands.islands.length : 0}, milestones ${plan ? plan.milestones.length : 0}`);
console.log(`items ${nI} (art ${itemImgs}), buildings ${nB} (art ${bldImgs}), question images ${qImgs}, referenced files ${refs.size}`);
console.log(`total size ${(total / 1024 / 1024).toFixed(2)} MB; items with no SOURCES entry: ${unmapped.join(', ') || 'none'}`);
if (total > 3 * 1024 * 1024) errors.push('output exceeds 3 MB');
if (errors.length) { console.error('HUB BUILD FAILED:\n' + errors.join('\n')); process.exit(1); }
console.log('OK ->', path.join(outDir, 'index.html'));
