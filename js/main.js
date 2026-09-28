/* ============================================================
   Idle Grounds — bootstrap: prime state, wire UI, run ticks
   ============================================================ */

(function init() {
  const E = window.ENGINE;

  // Populate EVERY region at boot — locked ones are visible on the map (the
  // camera just can't pan into them until they're unlocked at their border).
  for (const key of Object.keys(window.DATA.AREAS)) E.initArea(key);
  // Fresh saves get the pre-wired wisp starter network (built once).
  E.setupStarterNetwork();

  document.getElementById("upgrades-close").onclick = () => window.UI.toggleUpgrades(false);
  document.getElementById("tree-debug-btn").onclick = () => window.UI.toggleTreeDebug();
  document.getElementById("build-btn").onclick = () => window.UI.toggleBuild();
  document.getElementById("demolish-btn").onclick = () => window.UI.toggleDemolish();
  document.getElementById("debug-btn").onclick = () => window.UI.toggleDebug();
  document.getElementById("reset-btn").onclick = () => {
    if (confirm("Reset ALL progress and start over?")) { window.SAVE.clearSave(); location.reload(); }
  };
  document.getElementById("dragon-continue").onclick = () => window.UI.dismissDragonDialog();
  document.getElementById("help-btn").onclick = () => window.UI.openHelp();
  document.getElementById("help-close").onclick = () => window.UI.closeHelp();
  document.getElementById("stats-btn").onclick = () => window.UI.openStats();
  document.getElementById("stats-close").onclick = () => window.UI.closeStats();
  document.getElementById("ascend-go").onclick = () => {
    if (confirm("Ascend and begin the grounds anew? (+20% world speed per ascension, kept forever)"))
      E.ascend(window.UI.chosenVows ? window.UI.chosenVows() : []);
  };
  document.getElementById("ascend-later").onclick = () => {
    window.GS.ascendPrompt = false; window.UI.renderPlay();
  };
  document.getElementById("welcome-close").onclick = () => {
    window.UI.dismissWelcome(); window.GS.introSeen = true; window.SAVE.saveState();
  };
  document.getElementById("ending-continue").onclick = () => window.UI.dismissEnding();
  document.getElementById("perk-btn").onclick = () => window.UI.openPerkShop();
  document.getElementById("perk-close").onclick = () => window.UI.closePerkShop();

  // Version badge: number in the bottom bar, what-changed note as tooltip.
  const vc = document.getElementById("version-chip");
  if (vc && window.DATA.VERSION) {
    vc.textContent = "v" + window.DATA.VERSION.num;
    vc.title = window.DATA.VERSION.desc;
  }

  // Sound: the engine fires window.onSfx(name) at authoritative events (craft,
  // build, upgrade, unlock, hit, kill, dragon, ascend, harvest); route them to
  // the synth. A mute toggle persists via AUDIO; browsers need a user gesture
  // before audio plays, so resume the context on the first pointer/key press.
  window.onSfx = (name) => window.AUDIO.play(name);
  const muteBtn = document.getElementById("mute-btn");
  const paintMute = () => {
    const muted = window.AUDIO.isMuted();
    muteBtn.textContent = muted ? "🔇" : "🔊";
    muteBtn.setAttribute("aria-label", muted ? "Unmute sound" : "Mute sound");
  };
  paintMute();
  muteBtn.onclick = () => { window.AUDIO.setMuted(!window.AUDIO.isMuted()); paintMute(); if (!window.AUDIO.isMuted()) window.AUDIO.play("click"); };
  const unlockAudio = () => { window.AUDIO.resume(); };
  window.addEventListener("pointerdown", unlockAudio, { once: true });
  window.addEventListener("keydown", unlockAudio, { once: true });

  // Offline / idle catch-up: replay the passive economy for the time the tab
  // was closed, then show the "Welcome back" summary. Runs once, after the
  // world is primed but before the live loops start.
  const offline = window.ENGINE.runOfflineCatchup();

  window.UI.wireInput();
  window.UI.render();
  // First run: reuse the #welcome-modal DOM for a one-time intro. Only when
  // there's no offline summary, so the two never collide on the same load.
  if (!window.GS.introSeen && !offline) {
    const wm = document.getElementById("welcome-modal");
    document.querySelector("#welcome-modal .dragon-ico").textContent = "🌱";
    document.querySelector("#welcome-modal h2").textContent = "Welcome to Idle Grounds";
    document.getElementById("welcome-away").textContent =
      "These are your cultivation grounds — tend them and awaken the Sleeping Dragon 🐉.";
    document.getElementById("welcome-gains").innerHTML =
      "<span class=\"wg-none\">Follow the 📜 Quests panel (top-right) for what to do next, and open ❓ Help anytime. Left-click to gather · right-click to feed buildings · WASD to look around.</span>";
    wm.classList.remove("hidden");
  }
  window.UI.showOfflineSummary(offline);
  if (window.UI.showAscendedCard) window.UI.showAscendedCard();   // one-time post-ascension card

  // Autosave: every 5s and on tab close. (state.js loads it back on boot.)
  setInterval(() => window.SAVE.saveState(), 5000);
  window.addEventListener("beforeunload", () => window.SAVE.saveState());

  // Game loop: fishing dives + node respawns. Repaint ONLY when the tick
  // changed something (or an on-screen countdown/badge needs its text
  // updated) — unconditionally rebuilding the huge world DOM every 100ms
  // pegged the GPU/CPU hard enough to stall the whole machine.
  setInterval(() => {
    const changed = E.gameTick();
    if (changed || window.UI.needsLiveRepaint()) window.UI.renderPlay();
  }, 50);
  // Automation: harvest on behalf of the player every second.
  setInterval(() => { if (E.automationTick() > 0) window.UI.renderPlay(); }, 1000);
})();
