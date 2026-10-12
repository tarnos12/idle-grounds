#!/usr/bin/env node
// node tools/hub/set-task.cjs "<title regex>" <status> ["now or reason"] [--all]
const fs = require('fs'), path = require('path');
const file = path.resolve(__dirname, '..', '..', 'hub', 'plan.json');
const args = process.argv.slice(2);
const all = args.includes('--all');
const pos = args.filter(a => a !== '--all');
const [rx, status, note] = pos;
const STATUSES = ['in-progress', 'blocked', 'waiting', 'done'];
if (!rx || !STATUSES.includes(status)) { console.error('usage: set-task "<title regex>" <in-progress|blocked|waiting|done> ["now or reason"] [--all]'); process.exit(2); }
const plan = JSON.parse(fs.readFileSync(file, 'utf8').replace(/^﻿/, ''));
let re; try { re = new RegExp(rx, 'i'); } catch (e) { console.error('bad regex: ' + e.message); process.exit(2); }
const hits = [];
for (const m of plan.milestones) for (const t of m.tasks) if (re.test(t.title)) hits.push(t);
if (hits.length === 0) { console.error('no task matches ' + rx); process.exit(1); }
if (hits.length > 1 && !all) { console.error(hits.length + ' tasks match (use a tighter regex or --all):\n' + hits.map(t => '  - ' + t.title).join('\n')); process.exit(1); }
if ((status === 'blocked' || status === 'waiting') && !note) { console.error(status + ' needs a reason (third argument)'); process.exit(2); }
for (const t of hits) {
  t.status = status;
  delete t.now; delete t.blockedBy;
  if (status === 'in-progress' && note) t.now = note;
  if (status === 'blocked' || status === 'waiting') t.blockedBy = note;
  console.log(status + ': ' + t.title);
}
const d = new Date(), p = n => String(n).padStart(2, '0');
plan.updated = d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate()) + ' ' + p(d.getHours()) + ':' + p(d.getMinutes());
fs.writeFileSync(file, JSON.stringify(plan, null, 2) + '\n');
