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

  window.UI.wireInput();
  window.UI.render();

  // Game loop: fishing dives + node respawns, then repaint. Full rebuild is
  // safe because all world interaction is captured at the viewport level.
  setInterval(() => { E.gameTick(); window.UI.render(); }, 100);
  // Automation: harvest on behalf of the player every second.
  setInterval(() => { if (E.automationTick() > 0) window.UI.render(); }, 1000);
})();
