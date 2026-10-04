#!/usr/bin/env node
// Art intake pull: Drive "incoming/<category>/" -> Assets/_Project/Art/Incoming/<category>/
// - unzips zips (PowerShell Expand-Archive), loose files are copied as-is
// - README/NOTES (.md/.txt) go to Incoming/_notes/ (prefixed with the source name)
// - Incoming/intake-log.json records every imported file (name,size,sha1,date,source); reruns only bring new/changed files
// Usage: node tools/art-intake/pull.js [--src <incoming dir>] [--dry]
// Default src: $ART_INCOMING or G:/Mój dysk/AI files/Idle Grounds Art/incoming
'use strict';
const fs = require('fs'), path = require('path'), os = require('os'), crypto = require('crypto');
const { execFileSync } = require('child_process');

const CATEGORIES = ['items', 'buildings', 'nodes', 'fixtures', 'creatures', 'islands', 'sky', 'ui', 'fx'];
const ROOT = path.resolve(__dirname, '..', '..');
const DEST = path.join(ROOT, 'Assets', '_Project', 'Art', 'Incoming');
const LOG = path.join(DEST, 'intake-log.json');
const args = process.argv.slice(2);
const argv = (n) => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : null; };
const DRY = args.includes('--dry');
const SRC = argv('--src') || process.env.ART_INCOMING || 'G:/Mój dysk/AI files/Idle Grounds Art/incoming';
const ART_EXT = new Set(['.png', '.aseprite', '.ase']);
const NOTE_EXT = new Set(['.md', '.txt']);

const sha1 = (p) => crypto.createHash('sha1').update(fs.readFileSync(p)).digest('hex');
const walk = (d) => fs.readdirSync(d, { withFileTypes: true }).flatMap(e =>
  e.isDirectory() ? walk(path.join(d, e.name)) : [path.join(d, e.name)]);

if (!fs.existsSync(SRC)) { console.error('Source folder not found: ' + SRC); process.exit(1); }
const log = fs.existsSync(LOG) ? JSON.parse(fs.readFileSync(LOG, 'utf8')) : { files: [] };
const known = new Map(log.files.map(f => [f.path, f]));
const stats = { added: 0, updated: 0, skipped: 0, notes: 0, zips: 0, ignored: 0 };
const tmpRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'art-intake-'));

function importFile(file, category, source) {
  const ext = path.extname(file).toLowerCase();
  const base = path.basename(file);
  const hash = sha1(file), size = fs.statSync(file).size;
  let rel, destPath;
  const isPreview = /preview/i.test(base); // contact sheets for review, not game sprites
  if (ART_EXT.has(ext) && !isPreview) { rel = category + '/' + base; }
  else if (NOTE_EXT.has(ext) || (ART_EXT.has(ext) && isPreview)) { rel = '_notes/' + category + '-' + source.replace(/\.zip$/i, '') + '-' + base; }
  else { stats.ignored++; console.log('  ignored (unsupported type): ' + base); return; }
  destPath = path.join(DEST, rel);
  const prev = known.get(rel);
  if (prev && prev.sha1 === hash) { stats.skipped++; return; }
  console.log((prev ? '  update ' : '  add    ') + rel + ' (' + size + ' B) <- ' + source);
  if (!DRY) {
    fs.mkdirSync(path.dirname(destPath), { recursive: true });
    fs.copyFileSync(file, destPath);
    known.set(rel, { path: rel, name: base, size, sha1: hash, date: new Date().toISOString(), source });
  }
  if (rel.startsWith('_notes/')) stats.notes++; else if (prev) stats.updated++; else stats.added++;
}

for (const cat of CATEGORIES) {
  const dir = path.join(SRC, cat);
  if (!fs.existsSync(dir)) continue;
  console.log('[' + cat + ']');
  for (const f of walk(dir)) {
    if (path.extname(f).toLowerCase() === '.zip') {
      stats.zips++;
      const out = path.join(tmpRoot, cat + '-' + path.basename(f, '.zip'));
      execFileSync('powershell.exe', ['-NoProfile', '-Command',
        `Expand-Archive -LiteralPath '${f.replace(/'/g, "''")}' -DestinationPath '${out.replace(/'/g, "''")}' -Force`], { stdio: 'inherit' });
      for (const inner of walk(out)) importFile(inner, cat, path.basename(f));
    } else importFile(f, cat, '(loose)');
  }
}
fs.rmSync(tmpRoot, { recursive: true, force: true });

if (!DRY) {
  fs.mkdirSync(DEST, { recursive: true });
  log.files = [...known.values()].sort((a, b) => a.path.localeCompare(b.path));
  fs.writeFileSync(LOG, JSON.stringify(log, null, 2) + '\n');
}
console.log(`\nIntake ${DRY ? '(dry run) ' : ''}done: ${stats.added} added, ${stats.updated} updated, ${stats.skipped} unchanged, ${stats.notes} notes, ${stats.zips} zips, ${stats.ignored} ignored.`);
if (stats.added + stats.updated) console.log('Next: Unity menu "Idle Grounds/Art/Integrate Incoming Art".');
