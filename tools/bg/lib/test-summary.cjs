// Summarise a Unity Test Framework NUnit3 results XML: totals + every failure (name, message, first stack lines).
// usage: node test-summary.cjs <results.xml> [label]   -> exit 0 if no failures, 1 if failures, 2 if unreadable
const fs = require('fs');
const [, , file, label = ''] = process.argv;
let xml;
try { xml = fs.readFileSync(file, 'utf8'); } catch (e) { console.log(`TESTS ${label}: no results file (${file})`); process.exit(2); }
const run = /<test-run\b([^>]*)>/.exec(xml);
if (!run) { console.log(`TESTS ${label}: results file has no <test-run>`); process.exit(2); }
const attr = (s, n) => { const m = new RegExp('(?:^|\\s)' + n + '="([^"]*)"').exec(s); return m ? m[1] : ''; };
const a = run[1];
const total = +attr(a, 'total'), passed = +attr(a, 'passed'), failed = +attr(a, 'failed'),
      skipped = +attr(a, 'skipped'), inconcl = +attr(a, 'inconclusive'),
      dur = parseFloat((attr(a, 'duration') || '0').replace(',', '.'));   // Unity writes the locale decimal separator
const dec = s => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&amp;/g, '&');
const fails = [];
const re = /<test-case\b([^>]*)>([\s\S]*?)<\/test-case>/g;
let m;
while ((m = re.exec(xml))) {
  if (attr(m[1], 'result') !== 'Failed') continue;
  const msg = /<message><!\[CDATA\[([\s\S]*?)\]\]><\/message>/.exec(m[2]);
  const st = /<stack-trace><!\[CDATA\[([\s\S]*?)\]\]><\/stack-trace>/.exec(m[2]);
  fails.push({ name: dec(attr(m[1], 'fullname')), msg: msg ? msg[1].trim() : '', st: st ? st[1].trim().split('\n').slice(0, 3).join('\n      ') : '' });
}
for (const f of fails) console.log(`  FAIL ${f.name}\n      ${f.msg.split('\n').slice(0, 4).join('\n      ')}${f.st ? '\n      ' + f.st : ''}`);
console.log(`TESTS ${label}: total ${total}, passed ${passed}, failed ${failed}, skipped ${skipped}, inconclusive ${inconcl} (${dur.toFixed(1)}s)`);
if (total === 0) { console.log(`TESTS ${label}: NO TESTS RAN (filter matched nothing, or the run aborted)`); process.exit(2); }
process.exit(failed > 0 ? 1 : 0);
