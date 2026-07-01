/* ============================================================
   Idle Grounds — bootstrap: prime state, wire UI, run ticks
   ============================================================ */

(function init() {
  const E = window.ENGINE;

  // Populate every already-unlocked area (just Forest at start).
  for (const key of Object.keys(window.DATA.AREAS)) {
    if (E.isAreaUnlocked(key)) E.initArea(key);
  }

  document.getElementById("upgrades-btn").onclick = () => window.UI.toggleUpgrades();
  document.getElementById("upgrades-close").onclick = () => window.UI.toggleUpgrades(false);
  document.getElementById("build-btn").onclick = () => window.UI.toggleBuild();
  document.getElementById("debug-btn").onclick = () => window.UI.toggleDebug();

  window.UI.wireInput();
  window.UI.render();

  // Game loop: fishing dives + node respawns, then repaint. renderPlay only
  // rebuilds the passive grid — NOT the arrows/build menu — so hovering an
  // arrow or clicking a build card isn't disrupted by the tick.
  setInterval(() => { E.gameTick(); window.UI.renderPlay(); }, 100);
  // Automation: harvest on behalf of the player every second.
  setInterval(() => { if (E.automationTick() > 0) window.UI.renderPlay(); }, 1000);
})();
