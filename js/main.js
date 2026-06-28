/* ============================================================
   Idle Grounds — bootstrap: prime state, wire UI, run ticks
   ============================================================ */

(function init() {
  const E = window.ENGINE;

  // Prime tiles for every already-unlocked area (just Forest at start).
  for (const key of Object.keys(window.DATA.AREAS)) {
    if (window.GS.areas[key].unlocked) E.primeAreaTiles(key);
  }

  // Static UI wiring.
  document.getElementById("upgrades-btn").onclick = () => window.UI.toggleUpgrades();
  document.getElementById("upgrades-close").onclick = () => window.UI.toggleUpgrades(false);
  document.getElementById("filter-active").onclick = () => { window.GS.craftFilter = "active"; window.UI.render(); };
  document.getElementById("filter-all").onclick = () => { window.GS.craftFilter = "all"; window.UI.render(); };
  document.getElementById("win-continue").onclick = () =>
    document.getElementById("win-modal").classList.add("hidden");

  window.UI.render();

  // Game loop: promote ready tiles + refresh visible timers (~10fps).
  setInterval(() => { E.gameTick(); window.requestRender(); }, 100);
  // Passive gold drip: +1 every 10s.
  setInterval(() => { E.goldTick(); window.requestRender(); }, 10000);
  // Automation: harvest on behalf of the player every second.
  setInterval(() => { E.automationTick(); window.requestRender(); }, 1000);
})();
