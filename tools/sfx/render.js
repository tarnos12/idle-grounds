// Offline render of old-game/js/audio.js SFX -> WAV (44100 Hz mono 16-bit).
// Usage: node tools/sfx/render.js
// Mirrors voice(type,freq,t0,dur,peak,endFreq): exponential freq glide, gain
// 0.0001 -> peak (exp, 8 ms) -> 0.0001 (exp, at dur), osc stopped at dur+20 ms.
// Master gain 0.18 kept as in the JS (no normalisation). Oscillators are
// band-limited additive (like WebAudio's PeriodicWave). No filters/noise in JS.
const fs = require("fs"), path = require("path");
const SR = 44100, MASTER = 0.18, FLOOR = 0.0001;
const OUT = path.join(__dirname, "..", "..", "Assets", "_Project", "Audio", "SFX");

function osc(type, phase, f) {
  const nyq = SR / 2, maxH = Math.max(1, Math.floor(nyq / f));
  const w = 2 * Math.PI * phase;
  let s = 0;
  if (type === "sine") return Math.sin(w);
  if (type === "square") { for (let k = 1; k <= maxH; k += 2) s += Math.sin(k * w) / k; return s * 4 / Math.PI; }
  if (type === "triangle") { for (let k = 1, i = 0; k <= maxH; k += 2, i++) s += (i % 2 ? -1 : 1) * Math.sin(k * w) / (k * k); return s * 8 / (Math.PI * Math.PI); }
  throw new Error(type);
}
// voice = [type, freq, t0, dur, peak, endFreq]
function render(voices) {
  const total = Math.max(...voices.map(v => v[2] + v[3] + 0.02));
  const buf = new Float32Array(Math.ceil(total * SR));
  for (const [type, f0, t0, dur, peak, endF] of voices) {
    const f1 = endF && endF !== f0 ? Math.max(1, endF) : f0;
    const n0 = Math.round(t0 * SR), nStop = Math.round((dur + 0.02) * SR);
    let phase = 0;
    for (let i = 0; i < nStop && n0 + i < buf.length; i++) {
      const t = i / SR;
      const f = t >= dur ? f1 : f0 * Math.pow(f1 / f0, t / dur);
      let g;
      if (t < 0.008) g = FLOOR * Math.pow(peak / FLOOR, t / 0.008);
      else if (t < dur) g = peak * Math.pow(FLOOR / peak, (t - 0.008) / (dur - 0.008));
      else g = FLOOR;
      buf[n0 + i] += osc(type, phase, f) * g * MASTER;
      phase += f / SR; phase -= Math.floor(phase);
    }
  }
  return buf;
}
function wav(buf) {
  const b = Buffer.alloc(44 + buf.length * 2);
  b.write("RIFF", 0); b.writeUInt32LE(36 + buf.length * 2, 4); b.write("WAVEfmt ", 8);
  b.writeUInt32LE(16, 16); b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22);
  b.writeUInt32LE(SR, 24); b.writeUInt32LE(SR * 2, 28); b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34);
  b.write("data", 36); b.writeUInt32LE(buf.length * 2, 40);
  buf.forEach((v, i) => b.writeInt16LE(Math.round(Math.max(-1, Math.min(1, v)) * 32767), 44 + i * 2));
  return b;
}
const S = {
  harvest: [["triangle", 620, 0, 0.09, 0.5, 620 * 0.85]],           // JS: f = 620 +/- 60 random; centre rendered
  pickup: [["sine", 480, 0, 0.12, 0.5, 900]],
  swing: [["sine", 180, 0, 0.07, 0.4, 120]],
  craft: [["sine", 660, 0, 0.16, 0.5, 660], ["sine", 990, 0.09, 0.20, 0.4, 990]],
  build: [["triangle", 150, 0, 0.16, 0.7, 90], ["sine", 300, 0, 0.06, 0.25, 260]],
  upgrade: [["triangle", 700, 0, 0.10, 0.4, 700], ["triangle", 900, 0.06, 0.10, 0.4, 900], ["sine", 1320, 0.12, 0.16, 0.4, 1500]],
  unlock: [["sine", 523, 0, 0.18, 0.55, 523], ["sine", 784, 0.10, 0.26, 0.5, 784]],
  hit: [["square", 220, 0, 0.06, 0.28, 150]],
  kill: [["triangle", 260, 0, 0.20, 0.55, 90], ["sine", 130, 0, 0.22, 0.4, 70]],
  dragon: [["sine", 110, 0, 0.35, 0.8, 55], ["sine", 220, 0.04, 0.30, 0.3, 130]],
  ascend: [["sine", 196, 0, 0.40, 0.7, 196], ["sine", 294, 0.03, 0.36, 0.45, 294], ["triangle", 588, 0.06, 0.30, 0.28, 588]],
  error: [["square", 140, 0, 0.14, 0.3, 110]],
  click: [["sine", 660, 0, 0.04, 0.22, 660]],
};
fs.mkdirSync(OUT, { recursive: true });
console.log("name       ms     peak");
for (const [name, v] of Object.entries(S)) {
  const buf = render(v);
  fs.writeFileSync(path.join(OUT, name + ".wav"), wav(buf));
  const peak = buf.reduce((m, x) => Math.max(m, Math.abs(x)), 0);
  console.log(name.padEnd(10), String(Math.round(buf.length / SR * 1000)).padStart(4), peak.toFixed(4));
}
