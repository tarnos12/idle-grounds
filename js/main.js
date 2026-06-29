/* ============================================================
   Idle Grounds — bootstrap: prime state, wire UI, run ticks
   ============================================================ */

(function init() {
  const E = window.ENGINE;

  // Prime nodes for every already-unlocked area (just Forest at start).
  for (const key of Object.keys(window.DATA.AREAS)) {
    if (E.isAreaUnlocked(key)) E.primeAreaNodes(key);
  }

  // Static UI wiring.
  document.getElementById("upgrades-btn").onclick = () => window.UI.toggleUpgrades();
  document.getElementById("upgrades-close").onclick = () => window.UI.toggleUpgrades(false);
  document.getElementById("filter-active").onclick = () => { window.GS.craftFilter = "active"; window.UI.render(); };
  document.getElementById("filter-all").onclick = () => { window.GS.craftFilter = "all"; window.UI.render(); };
  document.getElementById("win-continue").onclick = () =>
    document.getElementById("win-modal").classList.add("hidden");

  window.UI.render();

  // Game loop: promote ready nodes + refresh visible timers (~10fps).
  // Live tick does in-place updates so the grid isn't rebuilt every frame.
  setInterval(() => { E.gameTick(); window.requestLiveTick(); }, 100);
  // Automation: harvest on behalf of the player every second. A full render
  // is only needed when it actually harvested (inventory/crafting changed).
  setInterval(() => { if (E.automationTick() > 0) window.requestRender(); }, 1000);
})();
