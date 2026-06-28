/* ============================================================
   Idle Grounds — game engine (pure logic, no DOM)
   Mutates window.GS; UI re-renders from it. Emits "render"
   requests via window.requestRender() (defined in ui.js).
   ============================================================ */

const D = window.DATA;

function itemName(key) {
  return D.ITEM_NAMES[key] || key.replace(/_/g, " ").replace(/\b\w/g, c => c.toUpperCase());
}
function itemIcon(key) { return D.ITEM_ICONS[key] || "📦"; }

function inv(item) { return window.GS.inventory[item] || 0; }
function addItem(item, qty) {
  window.GS.inventory[item] = inv(item) + qty;
}

function addGold(n) { window.GS.gold += n; }

// ---- Timers --------------------------------------------------

// Effective timer for a tier in an area, after speed upgrades (-20% each, multiplicative).
function effectiveTimer(areaKey, tierIndex) {
  const cfg = D.AREAS[areaKey];
  const base = cfg.tiers[tierIndex].timer;
  const speed = window.GS.areas[areaKey].upgrades.speed;
  return base * Math.pow(0.8, speed);
}

// ---- Tier rolling -------------------------------------------

// Weighted random tier (1..maxTier) using base weights.
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

// ---- Tile lifecycle -----------------------------------------

// Put a tile into cooldown: roll its next tier now and time the regrow
// off that tier (so a slow Void tree takes 90s to grow back).
function startCooldown(areaKey, tile) {
  tile.tier = rollTier(areaKey);
  tile.state = "cooldown";
  tile.cooldownEnd = Date.now() + effectiveTimer(areaKey, tile.tier - 1) * 1000;
}

function setReady(areaKey, tile) {
  const cfg = D.AREAS[areaKey];
  tile.state = "ready";
  tile.hitsLeft = cfg.tiers[tile.tier - 1].durability || 1;
}

// Initialize tile states for a (possibly newly unlocked) area.
function primeAreaTiles(areaKey) {
  const cfg = D.AREAS[areaKey];
  for (const tile of window.GS.areas[areaKey].tiles) {
    if (!tile.unlocked) { tile.state = "locked"; continue; }
    if (tile.state !== "locked") continue; // already primed
    if (cfg.initialReady) {
      tile.tier = rollTier(areaKey);
      setReady(areaKey, tile);
    } else {
      startCooldown(areaKey, tile);
    }
  }
}

// ---- Harvesting ---------------------------------------------

function rollAmount(spec) {
  return spec.min + Math.floor(Math.random() * (spec.max - spec.min + 1));
}

// Click a ready tile. Returns true if anything happened.
// isAuto suppresses some feedback but otherwise identical.
function harvestTile(areaKey, tileId, isAuto) {
  const area = window.GS.areas[areaKey];
  const tile = area.tiles[tileId];
  if (!tile || tile.state !== "ready") return false;

  const cfg = D.AREAS[areaKey];

  // Mine durability: needs multiple clicks to break.
  if (tile.hitsLeft > 1) {
    tile.hitsLeft--;
    if (isAuto) tile.autoFlash = Date.now() + 400;
    return true;
  }

  // Break / harvest: grant drops.
  const tierDef = cfg.tiers[tile.tier - 1];
  for (const spec of tierDef.drops) {
    const amt = rollAmount(spec);
    addItem(spec.item, amt);
    window.GS.stats.totalGathered += amt;
  }

  // Harvesting Gold bonus: 10% chance of +1 Gold per harvest.
  if (Math.random() < 0.10) addGold(1);

  if (isAuto) tile.autoFlash = Date.now() + 600;
  startCooldown(areaKey, tile);
  return true;
}

// ---- Crafting -----------------------------------------------

function recipeById(id) { return D.RECIPES.find(r => r.id === id); }

// Why a recipe can't be crafted right now: null if craftable.
function craftBlockReason(recipe, times = 1) {
  if (recipe.requires && !window.GS.inventory[recipe.requires]) {
    return `Requires ${itemName(recipe.requires)}`;
  }
  for (const [item, qty] of Object.entries(recipe.in)) {
    if (inv(item) < qty * times) return "Not enough materials";
  }
  return null;
}

// Largest N (<= cap) we can craft right now.
function maxCraftable(recipe, cap) {
  if (recipe.requires && !window.GS.inventory[recipe.requires]) return 0;
  let n = cap;
  for (const [item, qty] of Object.entries(recipe.in)) {
    n = Math.min(n, Math.floor(inv(item) / qty));
  }
  return Math.max(0, n);
}

// Craft a recipe `times`. Returns number actually crafted.
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

  // One-time effects fire on first successful craft only.
  if (!window.GS.craftedOnce[recipe.id]) {
    window.GS.craftedOnce[recipe.id] = true;
    if (recipe.firstCraftGold) addGold(recipe.firstCraftGold);
    if (recipe.unlocksArea) unlockArea(recipe.unlocksArea);
  }

  if (recipe.isWin && !window.GS.won) {
    window.GS.won = true;
    window.onWin && window.onWin();
  }
  return n;
}

function unlockArea(areaKey) {
  const area = window.GS.areas[areaKey];
  if (area.unlocked) return;
  area.unlocked = true;
  primeAreaTiles(areaKey);
}

// ---- Gold sinks ---------------------------------------------

function tileUnlockCost(areaKey) {
  const cfg = D.AREAS[areaKey];
  const area = window.GS.areas[areaKey];
  const unlockedCount = area.tiles.filter(t => t.unlocked).length;
  const beyondInitial = unlockedCount - cfg.initialTiles; // 0 for first extra
  return D.COSTS.tileBase * (beyondInitial + 1);
}

function nextLockedTile(areaKey) {
  return window.GS.areas[areaKey].tiles.find(t => !t.unlocked) || null;
}

function unlockTile(areaKey) {
  const tile = nextLockedTile(areaKey);
  if (!tile) return false;
  const cost = tileUnlockCost(areaKey);
  if (window.GS.gold < cost) return false;
  window.GS.gold -= cost;
  tile.unlocked = true;
  tile.state = "locked";   // primeAreaTiles will activate it
  primeAreaTiles(areaKey);
  return true;
}

// Upgrade purchase. type: "tier" | "speed" | "automation".
function upgradeCost(areaKey, type) {
  const up = window.GS.areas[areaKey].upgrades;
  if (type === "tier") {
    const next = up.maxTier + 1;
    return next > 5 ? null : D.COSTS.tierUnlock[next];
  }
  if (type === "speed") {
    return up.speed >= 3 ? null : D.COSTS.speed[up.speed];
  }
  if (type === "automation") {
    return up.automation >= 3 ? null : D.COSTS.automation[up.automation];
  }
  return null;
}

function buyUpgrade(areaKey, type) {
  const cost = upgradeCost(areaKey, type);
  if (cost == null || window.GS.gold < cost) return false;
  window.GS.gold -= cost;
  const up = window.GS.areas[areaKey].upgrades;
  if (type === "tier") up.maxTier++;
  else if (type === "speed") up.speed++;
  else if (type === "automation") up.automation++;
  return true;
}

// ---- Ticks --------------------------------------------------

// Promote any cooldown tiles whose timer has elapsed.
function gameTick() {
  const now = Date.now();
  for (const areaKey of Object.keys(D.AREAS)) {
    const area = window.GS.areas[areaKey];
    if (!area.unlocked) continue;
    for (const tile of area.tiles) {
      if (tile.state === "cooldown" && now >= tile.cooldownEnd) {
        setReady(areaKey, tile);
      }
    }
  }
}

// Passive gold drip: +1 every 10s.
function goldTick() { addGold(1); }

// Automation: each unlocked area with automation harvests its readiest
// tiles (highest tier first), up to the level's click budget.
function automationTick() {
  for (const areaKey of Object.keys(D.AREAS)) {
    const area = window.GS.areas[areaKey];
    if (!area.unlocked) continue;
    const level = area.upgrades.automation;
    if (level <= 0) continue;

    const budget = D.AUTOMATION_CLICKS[level];
    const ready = area.tiles
      .filter(t => t.state === "ready")
      .sort((a, b) => b.tier - a.tier);

    let clicks = 0;
    for (const tile of ready) {
      if (clicks >= budget) break;
      harvestTile(areaKey, tile.id, true);
      clicks++;
    }
  }
}

window.ENGINE = {
  itemName, itemIcon, inv, addItem, addGold,
  effectiveTimer, rollTier, startCooldown, setReady, primeAreaTiles,
  harvestTile, recipeById, craftBlockReason, maxCraftable, craft,
  unlockArea, tileUnlockCost, nextLockedTile, unlockTile,
  upgradeCost, buyUpgrade, gameTick, goldTick, automationTick,
};
