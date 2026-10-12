#!/usr/bin/env node
// node tools/hub/build-plan.cjs  -> writes hub/PLAN.md from hub/plan.json
const fs = require('fs'), path = require('path');
const dir = path.resolve(__dirname, '..', '..', 'hub');
const plan = JSON.parse(fs.readFileSync(path.join(dir, 'plan.json'), 'utf8').replace(/^﻿/, ''));
const ORDER = ['in-progress', 'blocked', 'waiting', 'done'];
const counts = {}; const now = [];
for (const m of plan.milestones) for (const t of m.tasks) { counts[t.status] = (counts[t.status] || 0) + 1; if (t.status === 'in-progress') now.push(t); }
let o = '# Plan\n\nUpdated ' + plan.updated + '. ' + ORDER.map(s => s + ': ' + (counts[s] || 0)).join(', ') + '\n\n';
if (now.length) o += '## Being worked on now\n\n' + now.map(t => '- ' + t.title + (t.now ? ' (' + t.now + ')' : '')).join('\n') + '\n\n';
for (const m of plan.milestones) {
  o += '## ' + m.id + ': ' + m.title + '\n\n';
  for (const t of m.tasks) {
    o += '- [' + (t.status === 'done' ? 'x' : ' ') + '] **' + t.title + '** (' + t.status + (t.size ? ', ' + t.size : '') + (t.owner ? ', ' + t.owner : '') + ')\n';
    if (t.detail) o += '  - ' + t.detail + '\n';
    if (t.now) o += '  - Now: ' + t.now + '\n';
    if (t.blockedBy) o += '  - Why: ' + t.blockedBy + '\n';
  }
  o += '\n';
}
fs.writeFileSync(path.join(dir, 'PLAN.md'), o);
console.log('wrote hub/PLAN.md');
