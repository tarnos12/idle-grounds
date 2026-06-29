/* ============================================================
   Idle Grounds — UI rendering (reads window.GS, writes DOM)
   ============================================================ */

const E = window.ENGINE;
const DD = window.DATA;
const G = DD.GRID;

const $ = sel => document.querySelector(sel);
function el(tag, cls, html) {
  const n = document.createElement(tag);
  if (cls) n.className = cls;
  if (html != null) n.innerHTML = html;
  return n;
}

let upgradesOpen = false;
let pendingSlide = null; // direction the camera just moved, for the slide-in anim

// Coalesce full render requests into one per animation frame.
let renderQueued = false;
function requestRender() {
  if (renderQueued) return;
  renderQueued = true;
  requestAnimationFrame(() => { renderQueued = false; render(); });
}
window.requestRender = requestRender;

// Lightweight per-frame update: refresh cooldown timers in place WITHOUT
// rebuilding nodes (a full rebuild every frame destroys the node under the
// cursor mid-click — hover flicker / dropped clicks).
let liveTickQueued = false;
function requestLiveTick() {
  if (liveTickQueued) return;
  liveTickQueued = true;
  requestAnimationFrame(() => { liveTickQueued = false; refreshGrid(); });
}
window.requestLiveTick = requestLiveTick;

// ---- Top bar -------------------------------------------------

function renderTopBar() {
  const area = DD.AREAS[window.GS.world.currentArea];
  $("#area-name").innerHTML = `${area.icon} ${area.name}`;
}

// ---- World grid + sprites -----------------------------------

function costText(cost) {
  return Object.entries(cost)
    .map(([item, qty]) => `${qty} ${E.itemIcon(item)}`)
    .join(" ");
}

// Position a node's 2x2 footprint and its larger overflowing sprite box.
function placeNode(nodeEl, node) {
  const c = G.cell;
  nodeEl.style.left = (node.col * c) + "px";
  nodeEl.style.top = (node.row * c) + "px";
  nodeEl.style.width = (G.foot * c) + "px";
  nodeEl.style.height = (G.foot * c) + "px";
  nodeEl.style.zIndex = node.row; // lower rows draw in front
}

function buildNodeEl(areaKey, node, now) {
  const cfg = DD.AREAS[areaKey];
  const n = el("button", "node");
  n.dataset.nodeId = node.id;
  n.dataset.state = node.unlocked ? node.state : "lockedslot";
  n.dataset.tier = node.tier;
  placeNode(n, node);

  if (!node.unlocked) {
    n.classList.add("slot-locked");
    const cost = E.nodeUnlockCost(areaKey);
    n.innerHTML = `<span class="slot-plus">＋</span><span class="slot-cost">${costText(cost)}</span>`;
    n.title = `Clear plot — ${costText(cost)}`;
    n.onclick = () => { if (E.unlockNode(areaKey, node.id)) render(); };
    return n;
  }

  if (node.state === "ready") {
    n.classList.add("ready", cfg.interaction);
    const tierDef = cfg.tiers[node.tier - 1];
    const sprite = (DD.TIER_SPRITES[areaKey] || [])[node.tier - 1] || cfg.icon;
    let inner = `<span class="sprite t${node.tier}">${sprite}</span>`;
    inner += `<span class="node-tag t${node.tier}">${tierDef.name}</span>`;
    const totalHits = tierDef.hits || 1;
    if (totalHits > 1) {
      // chop / break: show remaining swings so the player knows it takes more.
      inner += `<span class="hits">${node.hitsLeft}/${totalHits} ${cfg.actionIcon}</span>`;
      n.dataset.hits = node.hitsLeft;
    }
    if (cfg.interaction === "surface") {
      // fishing: a surfaced fish has a short catch window — show the urgency.
      const left = Math.max(0, node.surfaceUntil - now);
      inner += `<span class="cd-timer catch">❗${(left / 1000).toFixed(1)}s</span>`;
    }
    n.innerHTML = inner;
    n.onclick = () => { E.harvestNode(areaKey, node.id, false); render(); };
  } else {
    n.classList.add("cooldown");
    // Fishing shows ripples while the fish is down; others show a dim sprite.
    const sprite = cfg.interaction === "surface"
      ? "🌊"
      : (DD.TIER_SPRITES[areaKey] || [])[node.tier - 1] || cfg.icon;
    const remain = Math.max(0, node.cooldownEnd - now);
    n.innerHTML =
      `<span class="sprite dim">${sprite}</span>` +
      `<span class="cd-timer">${(remain / 1000).toFixed(1)}s</span>`;
  }

  if (node.autoFlash > now) n.classList.add("auto");
  return n;
}

function renderWorld() {
  const areaKey = window.GS.world.currentArea;
  const px = G.cell * G.cells;

  const grid = $("#grid");
  grid.dataset.area = areaKey;
  grid.style.width = px + "px";
  grid.style.height = px + "px";
  grid.style.backgroundSize = `${G.cell}px ${G.cell}px`;
  grid.innerHTML = "";

  const now = Date.now();
  for (const node of window.GS.areas[areaKey].nodes) {
    grid.appendChild(buildNodeEl(areaKey, node, now));
  }

  // Camera slide-in animation when we just changed area.
  if (pendingSlide) {
    grid.style.animation = "none";
    void grid.offsetWidth; // reflow so the animation restarts
    grid.style.animation = `slide-${pendingSlide} .28s ease`;
    pendingSlide = null;
  }

  renderArrows(areaKey);
}

function renderArrows(areaKey) {
  const wrap = $("#arrows");
  wrap.innerHTML = "";
  for (const dir of ["up", "down", "left", "right"]) {
    const target = E.neighborOf(areaKey, dir);
    if (!target) continue; // no area in this direction

    const a = el("button", `edge-arrow ${dir}`);
    if (target === "void" || !DD.AREAS[target]) {
      a.classList.add("disabled");
      a.innerHTML = `<span class="arr">${DD.WORLD.dirGlyph[dir]}</span><span class="arr-label">🔒 ???</span>`;
      a.title = "Reserved — nothing here yet";
    } else if (!E.isAreaUnlocked(target)) {
      a.classList.add("locked");
      const cost = E.areaUnlockCost(target);
      const ok = E.canAfford(cost);
      if (!ok) a.classList.add("cant");
      a.innerHTML = `<span class="arr">${DD.WORLD.dirGlyph[dir]}</span>` +
        `<span class="arr-label">🔒 ${DD.AREAS[target].name}<br>${costText(cost)}</span>`;
      a.title = `Open ${DD.AREAS[target].name} — ${costText(cost)}`;
      a.onclick = () => { if (E.unlockArea(target)) { pendingSlide = dir; render(); } };
    } else {
      a.innerHTML = `<span class="arr">${DD.WORLD.dirGlyph[dir]}</span>` +
        `<span class="arr-label">${DD.AREAS[target].icon} ${DD.AREAS[target].name}</span>`;
      a.title = `Go to ${DD.AREAS[target].name}`;
      a.onclick = () => { if (E.moveTo(target)) { pendingSlide = dir; render(); } };
    }
    wrap.appendChild(a);
  }
}

// Per-frame live refresh: update cooldown countdowns in place, rebuild a
// node only when its state actually changed. Keeps node identity stable so
// hover/clicks survive — never clears the grid.
function refreshGrid() {
  const areaKey = window.GS.world.currentArea;
  const surface = DD.AREAS[areaKey].interaction === "surface";
  const grid = $("#grid");
  const now = Date.now();

  for (const node of window.GS.areas[areaKey].nodes) {
    const elNode = grid.querySelector(`[data-node-id="${node.id}"]`);
    if (!elNode) { render(); return; }

    const wantState = node.unlocked ? node.state : "lockedslot";
    if (elNode.dataset.state !== wantState || Number(elNode.dataset.tier) !== node.tier) {
      elNode.replaceWith(buildNodeEl(areaKey, node, now)); // rebuild just this node
    } else if (node.unlocked && node.state === "cooldown") {
      const t = elNode.querySelector(".cd-timer");
      if (t) t.textContent = `${(Math.max(0, node.cooldownEnd - now) / 1000).toFixed(1)}s`;
    } else if (node.unlocked && node.state === "ready" && surface) {
      const t = elNode.querySelector(".cd-timer.catch");
      if (t) t.textContent = `❗${(Math.max(0, node.surfaceUntil - now) / 1000).toFixed(1)}s`;
    }
  }
}

// ---- Crafting panel -----------------------------------------

function recipeVisible(recipe) {
  if (window.GS.craftFilter === "all") return true;
  const a = window.GS.world.currentArea;
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
  let status, maxed = false;
  if (type === "tier") { maxed = up.maxTier >= 5; status = `Tier ${up.maxTier}/5`; }
  if (type === "speed") { maxed = up.speed >= 3; status = `Lv ${up.speed}/3`; }
  if (type === "automation") { maxed = up.automation >= 3; status = `Lv ${up.automation}/3`; }

  const affordable = cost != null && E.canAfford(cost);
  const btnLabel = maxed ? "MAX" : costText(cost);
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

  for (const [areaKey, cfg] of Object.entries(DD.AREAS)) {
    const unlocked = E.isAreaUnlocked(areaKey);
    const sec = el("div", "up-area" + (unlocked ? "" : " dim"));
    sec.appendChild(el("h3", null, `${cfg.icon} ${cfg.name}${unlocked ? "" : " 🔒"} ` +
      `<span class="up-bank">${E.itemIcon(cfg.base)} ${E.inv(cfg.base)}</span>`));
    if (!unlocked) {
      sec.appendChild(el("div", "up-desc", `Travel here and open it from the world map first.`));
      body.appendChild(sec);
      continue;
    }
    sec.appendChild(upgradeRow(areaKey, "tier", "Unlock Next Tier",
      up => up.maxTier >= 5 ? "All tiers unlocked." : `Enables ${DD.TIER_LABELS[up.maxTier]} ${cfg.tiers[up.maxTier].name} to spawn.`));
    sec.appendChild(upgradeRow(areaKey, "speed", cfg.speedLabel,
      up => `${cfg.timerLabel} timers −20% each (now ×${Math.pow(0.8, up.speed).toFixed(2)}).`));
    sec.appendChild(upgradeRow(areaKey, "automation", "Automation",
      up => up.automation === 0 ? "Auto-harvests ready nodes." :
        up.automation === 3 ? "Harvests ALL ready nodes each tick." :
        `Harvests ${DD.AUTOMATION_CLICKS[up.automation]} node(s) per tick.`));
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
  renderWorld();
  renderCrafting();
  renderInventory();
  if (upgradesOpen) renderUpgrades();
}

window.UI = { render, toggleUpgrades };
