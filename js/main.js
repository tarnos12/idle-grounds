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
  document.getElementById("ascend-go").onclick = () => {
    if (confirm("Ascend and begin the grounds anew? (+8% permanent global speed)")) E.ascend();
  };
  document.getElementById("ascend-later").onclick = () => {
    window.GS.ascendPrompt = false; window.UI.renderPlay();
  };
  document.getElementById("welcome-close").onclick = () => window.UI.dismissWelcome();

  // Offline / idle catch-up: replay the passive economy for the time the tab
  // was closed, then show the "Welcome back" summary. Runs once, after the
  // world is primed but before the live loops start.
  const offline = window.ENGINE.runOfflineCatchup();

  window.UI.wireInput();
  window.UI.render();
  window.UI.showOfflineSummary(offline);

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
