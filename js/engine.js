/* ============================================================
   Idle Grounds — game engine (pure logic, no DOM)
   Mutates window.GS; UI re-renders from it.
   ============================================================ */

const D = window.DATA;

function itemName(key) {
  return D.ITEM_NAMES[key] || key.replace(/_/g, " ").replace(/\b\w/g, c => c.toUpperCase());
}
function itemIcon(key) { return D.ITEM_ICONS[key] || "📦"; }

function inv(item) { return window.GS.inventory[item] || 0; }
function addItem(item, qty) { window.GS.inventory[item] = inv(item) + qty; }

// ---- Resource costs (cost = { item: qty }) ------------------

function canAfford(cost) {
  for (const [item, qty] of Object.entries(cost)) if (inv(item) < qty) return false;
  return true;
}
function spend(cost) {
  if (!canAfford(cost)) return false;
  for (const [item, qty] of Object.entries(cost)) window.GS.inventory[item] = inv(item) - qty;
  return true;
}
function scaled(n) { return Math.max(1, Math.ceil(D.TEST.ENABLED ? n * D.TEST.costScale : n)); }

// ---- Timers --------------------------------------------------

// Effective timer for a tier in an area, after speed upgrades (-20% each).
function effectiveTimer(areaKey, tierIndex) {
  const cfg = D.AREAS[areaKey];
  const base = cfg.tiers[tierIndex].timer;
  const speed = window.GS.areas[areaKey].upgrades.speed;
  const testScale = D.TEST.ENABLED ? D.TEST.timeScale : 1;
  return base * Math.pow(0.8, speed) * testScale;
}

// ---- Tier rolling -------------------------------------------

function rollTier(areaKey) {
  const maxTier = window.GS.areas[areaKey].upgrades.maxTier;
  let total = 0;
  for (let t = 0; t < maxTier; t++) total += D.TIER_WEIGHTS[t];
  let r = Math.random() * total;
  for (let t = 0; t < maxTier; t++) {
    r -= D.TIER_WEIGHTS[t];
    if (r <= 0) return t + 1;
  }
  return 1;
}

// ---- Node lifecycle -----------------------------------------

function startCooldown(areaKey, node) {
  node.tier = rollTier(areaKey);
  node.state = "cooldown";
  node.cooldownEnd = Date.now() + effectiveTimer(areaKey, node.tier - 1) * 1000;
}

function setReady(areaKey, node) {
  const cfg = D.AREAS[areaKey];
  node.state = "ready";
  node.hitsLeft = cfg.tiers[node.tier - 1].durability || 1;
}

// Initialize node states for a (possibly newly unlocked) area.
function primeAreaNodes(areaKey) {
  const cfg = D.AREAS[areaKey];
  for (const node of window.GS.areas[areaKey].nodes) {
    if (!node.unlocked) { node.state = "locked"; continue; }
    if (node.state !== "locked") continue; // already primed
    if (cfg.initialReady) {
      node.tier = rollTier(areaKey);
      setReady(areaKey, node);
    } else {
      startCooldown(areaKey, node);
    }
  }
}

// ---- Harvesting ---------------------------------------------

function rollAmount(spec) {
  return spec.min + Math.floor(Math.random() * (spec.max - spec.min + 1));
}

function nodeById(areaKey, id) {
  return window.GS.areas[areaKey].nodes.find(n => n.id === id);
}

// Click a ready node. Returns true if anything happened.
function harvestNode(areaKey, nodeId, isAuto) {
  const node = nodeById(areaKey, nodeId);
  if (!node || node.state !== "ready") return false;

  const cfg = D.AREAS[areaKey];

  // Mine durability: needs multiple clicks to break.
  if (node.hitsLeft > 1) {
    node.hitsLeft--;
    if (isAuto) node.autoFlash = Date.now() + 400;
    return true;
  }

  // Break / harvest: grant drops.
  const tierDef = cfg.tiers[node.tier - 1];
  for (const spec of tierDef.drops) {
    const amt = rollAmount(spec);
    addItem(spec.item, amt);
    window.GS.stats.totalGathered += amt;
  }

  if (isAuto) node.autoFlash = Date.now() + 600;
  startCooldown(areaKey, node);
  return true;
}

// ---- Crafting -----------------------------------------------

function recipeById(id) { return D.RECIPES.find(r => r.id === id); }

function craftBlockReason(recipe, times = 1) {
  if (recipe.requires && !window.GS.inventory[recipe.requires]) {
    return `Requires ${itemName(recipe.requires)}`;
  }
  for (const [item, qty] of Object.entries(recipe.in)) {
    if (inv(item) < qty * times) return "Not enough materials";
  }
  return null;
}

function maxCraftable(recipe, cap) {
  if (recipe.requires && !window.GS.inventory[recipe.requires]) return 0;
  let n = cap;
  for (const [item, qty] of Object.entries(recipe.in)) {
    n = Math.min(n, Math.floor(inv(item) / qty));
  }
  return Math.max(0, n);
}

function craft(recipeId, times) {
  const recipe = recipeById(recipeId);
  if (!recipe) return 0;
  const n = Math.min(times, maxCraftable(recipe, times));
  if (n <= 0) return 0;

  for (const [item, qty] of Object.entries(recipe.in)) {
    window.GS.inventory[item] = inv(item) - qty * n;
  }
  for (const [item, qty] of Object.entries(recipe.out)) {
    addItem(item, qty * n);
  }
  window.GS.stats.totalCrafted += n;

  if (recipe.isWin && !window.GS.won) {
    window.GS.won = true;
    window.onWin && window.onWin();
  }
  return n;
}

// ---- Node unlocks (paid in the area's base resource) --------

function nodeUnlockCost(areaKey) {
  const cfg = D.AREAS[areaKey];
  const unlockedCount = window.GS.areas[areaKey].nodes.filter(n => n.unlocked).length;
  const beyondInitial = unlockedCount - cfg.initialActive; // 0 for the first extra
  const qty = scaled(D.COSTS.nodeBase * (beyondInitial + 1));
  return { [cfg.base]: qty };
}

function unlockNode(areaKey, nodeId) {
  const node = nodeById(areaKey, nodeId);
  if (!node || node.unlocked) return false;
  if (!spend(nodeUnlockCost(areaKey))) return false;
  node.unlocked = true;
  node.state = "locked";   // primeAreaNodes will activate it
  primeAreaNodes(areaKey);
  return true;
}

// ---- Upgrades (paid in the area's base resource) ------------

function upgradeCost(areaKey, type) {
  const up = window.GS.areas[areaKey].upgrades;
  const base = D.AREAS[areaKey].base;
  let raw = null;
  if (type === "tier") { const next = up.maxTier + 1; raw = next > 5 ? null : D.COSTS.tierUnlock[next]; }
  if (type === "speed") raw = up.speed >= 3 ? null : D.COSTS.speed[up.speed];
  if (type === "automation") raw = up.automation >= 3 ? null : D.COSTS.automation[up.automation];
  return raw == null ? null : { [base]: scaled(raw) };
}

function buyUpgrade(areaKey, type) {
  const cost = upgradeCost(areaKey, type);
  if (!cost || !spend(cost)) return false;
  const up = window.GS.areas[areaKey].upgrades;
  if (type === "tier") up.maxTier++;
  else if (type === "speed") up.speed++;
  else if (type === "automation") up.automation++;
  return true;
}

// ---- World / camera -----------------------------------------

// Which area lies in `dir` from `fromArea` ("up"|"down"|"left"|"right").
// Returns an area key, "void" for the reserved empty arm, or null.
function neighborOf(fromArea, dir) {
  const here = D.WORLD.layout[fromArea];
  const delta = D.WORLD.dirs[dir];
  const tx = here.x + delta.x, ty = here.y + delta.y;
  for (const [key, pos] of Object.entries(D.WORLD.layout)) {
    if (pos.x === tx && pos.y === ty) return key;
  }
  return null;
}

function areaUnlockCost(areaKey) {
  const base = D.WORLD.unlockCost[areaKey];
  if (!base) return null;
  const out = {};
  for (const [item, qty] of Object.entries(base)) out[item] = scaled(qty);
  return out;
}

function isAreaUnlocked(areaKey) { return !!window.GS.world.unlocked[areaKey]; }

// Pay to open an adjacent area, then move the camera onto it.
function unlockArea(areaKey) {
  if (areaKey === "void" || !D.AREAS[areaKey]) return false;
  if (isAreaUnlocked(areaKey)) return moveTo(areaKey);
  const cost = areaUnlockCost(areaKey);
  if (!cost || !spend(cost)) return false;
  window.GS.world.unlocked[areaKey] = true;
  primeAreaNodes(areaKey);
  window.GS.world.currentArea = areaKey;
  return true;
}

function moveTo(areaKey) {
  if (!isAreaUnlocked(areaKey)) return false;
  window.GS.world.currentArea = areaKey;
  return true;
}

// ---- Ticks --------------------------------------------------

// Promote any cooldown nodes whose timer has elapsed.
function gameTick() {
  const now = Date.now();
  for (const areaKey of Object.keys(D.AREAS)) {
    if (!isAreaUnlocked(areaKey)) continue;
    for (const node of window.GS.areas[areaKey].nodes) {
      if (node.state === "cooldown" && now >= node.cooldownEnd) setReady(areaKey, node);
    }
  }
}

// Automation: each unlocked area with automation harvests its readiest
// nodes (highest tier first), up to the level's click budget.
function automationTick() {
  let harvested = 0;
  for (const areaKey of Object.keys(D.AREAS)) {
    if (!isAreaUnlocked(areaKey)) continue;
    const level = window.GS.areas[areaKey].upgrades.automation;
    if (level <= 0) continue;

    const budget = D.AUTOMATION_CLICKS[level];
    const ready = window.GS.areas[areaKey].nodes
      .filter(n => n.state === "ready")
      .sort((a, b) => b.tier - a.tier);

    let clicks = 0;
    for (const node of ready) {
      if (clicks >= budget) break;
      harvestNode(areaKey, node.id, true);
      clicks++;
    }
    harvested += clicks;
  }
  return harvested;
}

window.ENGINE = {
  itemName, itemIcon, inv, addItem, canAfford,
  effectiveTimer, rollTier, startCooldown, setReady, primeAreaNodes,
  nodeById, harvestNode, recipeById, craftBlockReason, maxCraftable, craft,
  nodeUnlockCost, unlockNode, upgradeCost, buyUpgrade,
  neighborOf, areaUnlockCost, isAreaUnlocked, unlockArea, moveTo,
  gameTick, automationTick,
};
