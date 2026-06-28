/* ============================================================
   Idle Grounds — UI rendering (reads window.GS, writes DOM)
   ============================================================ */

const E = window.ENGINE;
const DD = window.DATA;

const $ = sel => document.querySelector(sel);
function el(tag, cls, html) {
  const n = document.createElement(tag);
  if (cls) n.className = cls;
  if (html != null) n.innerHTML = html;
  return n;
}

let upgradesOpen = false;

// Coalesce render requests into one per animation frame.
let renderQueued = false;
function requestRender() {
  if (renderQueued) return;
  renderQueued = true;
  requestAnimationFrame(() => { renderQueued = false; render(); });
}
window.requestRender = requestRender;

// ---- Top bar / tabs -----------------------------------------

function renderTopBar() {
  const tabs = $("#tabs");
  tabs.innerHTML = "";
  for (const [key, cfg] of Object.entries(DD.AREAS)) {
    const area = window.GS.areas[key];
    const tab = el("button", "tab" + (key === window.GS.activeArea ? " active" : "") + (area.unlocked ? "" : " locked"));
    tab.innerHTML = `<span class="ico">${cfg.icon}</span>${cfg.name}` +
      (area.unlocked ? "" : ` 🔒`);
    if (area.unlocked) tab.onclick = () => { window.GS.activeArea = key; render(); };
    else tab.title = `Unlock by crafting ${E.itemName(cfg.unlockRecipe)}`;
    tabs.appendChild(tab);
  }
  $("#gold-amount").textContent = Math.floor(window.GS.gold);
}

// ---- Area grid ----------------------------------------------

function tileLabel(areaKey, tile) {
  const cfg = DD.AREAS[areaKey];
  if (tile.state === "cooldown") {
    const remain = Math.max(0, tile.cooldownEnd - Date.now());
    return `${(remain / 1000).toFixed(1)}s`;
  }
  return cfg.tiers[tile.tier - 1].name;
}

function renderGrid() {
  const areaKey = window.GS.activeArea;
  const cfg = DD.AREAS[areaKey];
  const area = window.GS.areas[areaKey];

  $("#area-title").innerHTML = `${cfg.icon} ${cfg.name}`;

  const grid = $("#grid");
  grid.style.gridTemplateColumns = `repeat(${cfg.cols}, 1fr)`;
  grid.innerHTML = "";

  for (const tile of area.tiles) {
    if (!tile.unlocked) continue; // locked tiles shown via the unlock button row
    const t = el("button", "tile");
    t.dataset.tier = tile.tier;
    const now = Date.now();

    if (tile.state === "ready") {
      t.classList.add("ready");
      const tierDef = cfg.tiers[tile.tier - 1];
      let inner = `<div class="tier-badge t${tile.tier}">${DD.TIER_LABELS[tile.tier - 1]}</div>`;
      inner += `<div class="tile-emoji">${cfg.icon}</div>`;
      inner += `<div class="tile-name">${tierDef.name}</div>`;
      if ((tierDef.durability || 1) > 1) {
        inner += `<div class="hits">${tile.hitsLeft}/${tierDef.durability} ${cfg.actionIcon}</div>`;
      }
      t.innerHTML = inner;
      t.onclick = () => { E.harvestTile(areaKey, tile.id, false); render(); };
    } else {
      t.classList.add("cooldown");
      const remain = Math.max(0, tile.cooldownEnd - now);
      const total = E.effectiveTimer(areaKey, tile.tier - 1) * 1000;
      const pct = total > 0 ? Math.max(0, Math.min(100, (1 - remain / total) * 100)) : 100;
      t.innerHTML =
        `<div class="cd-ring" style="--pct:${pct}"></div>` +
        `<div class="tile-emoji dim">⏳</div>` +
        `<div class="tile-name">${(remain / 1000).toFixed(1)}s</div>`;
    }

    if (tile.autoFlash > now) t.classList.add("auto");
    grid.appendChild(t);
  }

  // Unlock-tile button row.
  const unlockRow = $("#unlock-row");
  unlockRow.innerHTML = "";
  const next = E.nextLockedTile(areaKey);
  if (next) {
    const cost = E.tileUnlockCost(areaKey);
    const btn = el("button", "unlock-tile" + (window.GS.gold >= cost ? "" : " disabled"));
    btn.innerHTML = `🔒 Unlock Tile — ${cost}G`;
    btn.onclick = () => { if (E.unlockTile(areaKey)) render(); };
    unlockRow.appendChild(btn);
  }
}

// ---- Crafting panel -----------------------------------------

function recipeVisible(recipe) {
  if (window.GS.craftFilter === "all") return true;
  const a = window.GS.activeArea;
  return recipe.area === a || recipe.area === "misc";
}

function renderCrafting() {
  const list = $("#recipe-list");
  list.innerHTML = "";

  $("#filter-active").classList.toggle("on", window.GS.craftFilter === "active");
  $("#filter-all").classList.toggle("on", window.GS.craftFilter === "all");

  for (const recipe of DD.RECIPES) {
    if (!recipeVisible(recipe)) continue;

    const reason = E.craftBlockReason(recipe, 1);
    const card = el("div", "recipe" + (reason ? " blocked" : ""));

    const out = Object.entries(recipe.out)[0];
    let head = `<div class="r-head"><span class="r-ico">${E.itemIcon(out[0])}</span>` +
      `<span class="r-name">${recipe.name}</span></div>`;

    const ins = Object.entries(recipe.in)
      .map(([item, qty]) => {
        const have = E.inv(item);
        const ok = have >= qty ? "ok" : "miss";
        return `<span class="ing ${ok}">${qty}× ${E.itemName(item)} <em>(${have})</em></span>`;
      }).join(" ");
    let body = `<div class="r-ing">${ins} → ${out[1]}× ${recipe.name}</div>`;

    if (recipe.requires) {
      const ok = window.GS.inventory[recipe.requires];
      body += `<div class="r-req ${ok ? "ok" : "miss"}">Requires: ${E.itemName(recipe.requires)}</div>`;
    }
    if (recipe.unlocksArea && !window.GS.areas[recipe.unlocksArea].unlocked) {
      body += `<div class="r-unlock">🔓 Unlocks ${DD.AREAS[recipe.unlocksArea].name}</div>`;
    }
    if (recipe.isWin) card.classList.add("win-recipe");

    const can1 = E.maxCraftable(recipe, 1) >= 1;
    const can10 = E.maxCraftable(recipe, 10) >= 1;
    const actions = el("div", "r-actions");
    const b1 = el("button", "craft-btn" + (can1 ? "" : " disabled"), "Craft ▶");
    b1.onclick = () => { if (E.craft(recipe.id, 1)) render(); };
    const b10 = el("button", "craft-btn small" + (can10 ? "" : " disabled"), "Craft 10 ▶");
    b10.onclick = () => { if (E.craft(recipe.id, 10)) render(); };
    actions.appendChild(b1); actions.appendChild(b10);

    card.innerHTML = head + body;
    card.appendChild(actions);
    list.appendChild(card);
  }
}

// ---- Inventory ----------------------------------------------

function renderInventory() {
  const bar = $("#inventory");
  bar.innerHTML = "";
  const entries = Object.entries(window.GS.inventory).filter(([, n]) => n > 0);
  if (entries.length === 0) {
    bar.appendChild(el("span", "inv-empty", "Inventory empty — start harvesting!"));
    return;
  }
  for (const [item, n] of entries) {
    bar.appendChild(el("span", "inv-item", `${E.itemIcon(item)} ${E.itemName(item)}: <b>${n}</b>`));
  }
}

// ---- Upgrades modal -----------------------------------------

function upgradeRow(areaKey, type, label, descFn) {
  const cost = E.upgradeCost(areaKey, type);
  const up = window.GS.areas[areaKey].upgrades;
  const row = el("div", "up-row");
  let status, btnLabel, maxed = false;
  if (type === "tier") { maxed = up.maxTier >= 5; status = `Tier ${up.maxTier}/5`; }
  if (type === "speed") { maxed = up.speed >= 3; status = `Lv ${up.speed}/3`; }
  if (type === "automation") { maxed = up.automation >= 3; status = `Lv ${up.automation}/3`; }

  const affordable = cost != null && window.GS.gold >= cost;
  btnLabel = maxed ? "MAX" : `${cost}G`;
  row.innerHTML = `<div class="up-info"><b>${label}</b> <span class="up-status">${status}</span>` +
    `<div class="up-desc">${descFn(up)}</div></div>`;
  const btn = el("button", "up-buy" + (maxed ? " maxed" : affordable ? "" : " disabled"), btnLabel);
  if (!maxed) btn.onclick = () => { if (E.buyUpgrade(areaKey, type)) renderUpgrades(); };
  row.appendChild(btn);
  return row;
}

function renderUpgrades() {
  const body = $("#upgrades-body");
  body.innerHTML = "";
  $("#up-gold").textContent = Math.floor(window.GS.gold);

  for (const [areaKey, cfg] of Object.entries(DD.AREAS)) {
    const area = window.GS.areas[areaKey];
    const sec = el("div", "up-area" + (area.unlocked ? "" : " dim"));
    sec.appendChild(el("h3", null, `${cfg.icon} ${cfg.name}${area.unlocked ? "" : " 🔒"}`));
    if (!area.unlocked) {
      sec.appendChild(el("div", "up-desc", `Unlock by crafting ${E.itemName(cfg.unlockRecipe)}.`));
      body.appendChild(sec);
      continue;
    }
    sec.appendChild(upgradeRow(areaKey, "tier", "Unlock Next Tier",
      up => up.maxTier >= 5 ? "All tiers unlocked." : `Enables ${DD.TIER_LABELS[up.maxTier]} ${cfg.tiers[up.maxTier].name} to spawn.`));
    sec.appendChild(upgradeRow(areaKey, "speed", cfg.speedLabel,
      up => `${cfg.timerLabel} timers −20% each (now ×${Math.pow(0.8, up.speed).toFixed(2)}).`));
    sec.appendChild(upgradeRow(areaKey, "automation", "Automation",
      up => up.automation === 0 ? "Auto-harvests ready tiles." :
        up.automation === 3 ? "Harvests ALL ready tiles each tick." :
        `Harvests ${DD.AUTOMATION_CLICKS[up.automation]} tile(s) per tick.`));
    body.appendChild(sec);
  }
}

function toggleUpgrades(force) {
  upgradesOpen = force != null ? force : !upgradesOpen;
  $("#upgrades-modal").classList.toggle("hidden", !upgradesOpen);
  if (upgradesOpen) renderUpgrades();
}

// ---- Win screen ---------------------------------------------

window.onWin = function () {
  const s = window.GS.stats;
  const mins = ((Date.now() - s.started) / 60000).toFixed(1);
  $("#win-stats").innerHTML =
    `<div>⏱️ Time played: <b>${mins} min</b></div>` +
    `<div>⛏️ Resources gathered: <b>${s.totalGathered}</b></div>` +
    `<div>🛠️ Items crafted: <b>${s.totalCrafted}</b></div>`;
  $("#win-modal").classList.remove("hidden");
};

// ---- Master render ------------------------------------------

function render() {
  renderTopBar();
  renderGrid();
  renderCrafting();
  renderInventory();
  if (upgradesOpen) renderUpgrades();
}

window.UI = { render, toggleUpgrades };
