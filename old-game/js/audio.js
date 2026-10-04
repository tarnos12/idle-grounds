/* ============================================================
   Idle Grounds — procedural WebAudio SFX
   Every sound is synthesized at runtime (oscillators + gain
   envelopes) — NO external assets, NO fetch. The game ships as a
   CSP-locked artifact where any network request is blocked, so the
   whole sound library lives in this one file. Soft, chime-like
   timbres to match the cultivation/idle mood — never arcade-harsh.
   ============================================================ */

const AUDIO = (function () {
  const MUTE_KEY = "ig_muted";
  const MASTER = 0.18;            // gentle master gain — keep it easy on the ears

  let ctx = null;                // lazily created on first play (gesture-gated)
  let master = null;             // master gain node
  let unavailable = false;       // set if WebAudio isn't supported at all

  // Read the persisted mute preference; default UNMUTED. localStorage may be
  // absent in the sandbox, so every access is wrapped.
  let muted = (function () {
    try { return localStorage.getItem(MUTE_KEY) === "1"; } catch (e) { return false; }
  })();

  // Bring up the AudioContext on demand. Returns false if we can't (no
  // support / creation threw) so callers can bail without throwing.
  function ensureCtx() {
    if (ctx) return true;
    if (unavailable) return false;
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) { unavailable = true; return false; }
    try {
      ctx = new AC();
      master = ctx.createGain();
      master.gain.value = MASTER;
      master.connect(ctx.destination);
      return true;
    } catch (e) { unavailable = true; return false; }
  }

  // A single enveloped oscillator voice. Quick attack, exponential decay to a
  // tiny floor (never 0 — exponentialRampToValueAtTime forbids it) so notes
  // fade instead of clicking. Node is stopped/cleaned after it finishes.
  function voice(type, freq, t0, dur, peak, endFreq) {
    try {
      const osc = ctx.createOscillator();
      const g = ctx.createGain();
      osc.type = type || "sine";
      osc.frequency.setValueAtTime(freq, t0);
      if (endFreq && endFreq !== freq) {
        osc.frequency.exponentialRampToValueAtTime(Math.max(1, endFreq), t0 + dur);
      }
      g.gain.setValueAtTime(0.0001, t0);
      g.gain.exponentialRampToValueAtTime(peak, t0 + 0.008);   // ~8ms attack
      g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);   // decay to floor
      osc.connect(g);
      g.connect(master);
      osc.start(t0);
      osc.stop(t0 + dur + 0.02);
    } catch (e) { /* never let a bad note break gameplay */ }
  }

  // ---- the sound library -------------------------------------
  // Each entry gets the context's current time and paints a few voices.
  const SOUNDS = {
    // fires often — quiet, tiny, pitch-jittered so repeats don't feel robotic
    harvest(now) {
      const f = 620 + (Math.random() * 120 - 60);
      voice("triangle", f, now, 0.09, 0.5, f * 0.85);
    },
    // light rising blip — item vacuumed into the hand
    pickup(now) {
      voice("sine", 480, now, 0.12, 0.5, 900);
    },
    // very soft tick/thud — a tool swing that dropped no loot yet
    swing(now) {
      voice("sine", 180, now, 0.07, 0.4, 120);
    },
    // pleasant two-note chime — a converter finished a batch
    craft(now) {
      voice("sine", 660, now, 0.16, 0.5, 660);
      voice("sine", 990, now + 0.09, 0.20, 0.4, 990);
    },
    // low wooden thunk — a building was placed/completed
    build(now) {
      voice("triangle", 150, now, 0.16, 0.7, 90);
      voice("sine", 300, now, 0.06, 0.25, 260);
    },
    // bright ascending sparkle — an altar upgrade applied
    upgrade(now) {
      voice("triangle", 700, now, 0.10, 0.4, 700);
      voice("triangle", 900, now + 0.06, 0.10, 0.4, 900);
      voice("sine", 1320, now + 0.12, 0.16, 0.4, 1500);
    },
    // warm two-note flourish — a new region unlocked
    unlock(now) {
      voice("sine", 523, now, 0.18, 0.55, 523);
      voice("sine", 784, now + 0.10, 0.26, 0.5, 784);
    },
    // short muffled impact — striking a fox spirit
    hit(now) {
      voice("square", 220, now, 0.06, 0.28, 150);
    },
    // richer descending thud — a beast died
    kill(now) {
      voice("triangle", 260, now, 0.20, 0.55, 90);
      voice("sine", 130, now, 0.22, 0.4, 70);
    },
    // deep slow awe — dragon stage-up / awakening (~350ms low sweep)
    dragon(now) {
      voice("sine", 110, now, 0.35, 0.8, 55);
      voice("sine", 220, now + 0.04, 0.30, 0.3, 130);
    },
    // resonant gong-like swell — ascension, the grandest yet still soft
    ascend(now) {
      voice("sine", 196, now, 0.40, 0.7, 196);
      voice("sine", 294, now + 0.03, 0.36, 0.45, 294);
      voice("triangle", 588, now + 0.06, 0.30, 0.28, 588);
    },
    // short low buzz — invalid action / can't afford
    error(now) {
      voice("square", 140, now, 0.14, 0.3, 110);
    },
    // tiny subtle UI tick — button presses
    click(now) {
      voice("sine", 660, now, 0.04, 0.22, 660);
    },
  };

  // ---- public API --------------------------------------------
  const api = {
    // Resume a suspended context (main.js calls this on first user gesture).
    resume() {
      try {
        if (ctx && ctx.state === "suspended") ctx.resume();
      } catch (e) { /* no-op */ }
    },

    // Play a named sound. Silent if muted, unknown, or WebAudio is missing.
    play(name, opts) {
      if (muted) return;
      const snd = SOUNDS[name];
      if (!snd) return;
      if (!ensureCtx()) return;
      try {
        if (ctx.state === "suspended") ctx.resume();
        snd(ctx.currentTime, opts || {});
      } catch (e) { /* never throw from a sound */ }
    },

    isMuted() { return muted; },

    setMuted(v) {
      muted = !!v;
      try { localStorage.setItem(MUTE_KEY, muted ? "1" : "0"); } catch (e) {}
      return muted;
    },

    toggleMute() { return api.setMuted(!muted); },
  };

  return api;
})();

window.AUDIO = AUDIO;
