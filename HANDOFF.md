# Idle Grounds — Session Handoff

> Read this first when starting a new Claude Code session. It captures the
> current state of the prototype, key decisions, and what's left to do.

## ⚠️ Session setup (important)

- **Work ONLY in this folder:** `C:\Work\Marrow Tap Projects\Farm Prototype Claude`
- Start the session **rooted in this folder** (open it as the working directory, or
  `cd` here in a terminal and run `claude`). Do **not** use the sibling
  `Legend of the Fallen Warrior - ORIGINAL` folder — it's a separate, unrelated project.
- Verify on startup: `git rev-parse --show-toplevel` should print this folder.

## What this is

A browser-based incremental/idle sandbox ("Idle Grounds") built from the GDD at:
`C:\Users\tarno\Downloads\GDD_IdleGrounds_v0.1.md`

Four areas (Forest, Farm, Mine, Fishing) share one tier/harvest system feeding a
crafting system; the win condition is crafting the **Worldstone**.

## Tech / how to run

- **Vanilla HTML/CSS/JS, no build step.** Plain `<script>` tags (not ES modules) so it
  runs over `file://`.
- **Run it:** double-click `index.html`, OR serve with the bundled
  `node server.js` (static server, no deps, port 5174) and open `http://localhost:5174`.
- A `.claude/launch.json` here defines a preview server named `idle-grounds`
  (`node server.js`, port 5174). The Claude preview tool reads the **session root's**
  launch.json, so this only works if the session is rooted in this folder.

## File map

| File | Role |
|------|------|
| `index.html`   | DOM layout: top bar/tabs, area grid, crafting panel, inventory, upgrades + win modals |
| `style.css`    | Dark theme, tier-colored badges, pulse / cooldown-ring / AUTO animations |
| `js/data.js`   | All static config: areas × 5 tiers (drops/timers/durability), recipes, costs |
| `js/state.js`  | Mutable game state `window.GS` + constructors |
| `js/engine.js` | Pure logic (no DOM): tier rolls, harvest, durability, crafting, gold sinks, ticks |
| `js/ui.js`     | DOM rendering, reads `GS` (rAF-coalesced) |
| `js/main.js`   | Bootstrap: prime tiles, wire events, start the 3 game-loop intervals |
| `server.js`    | Dependency-free static file server for local preview |

Architecture note: engine is DOM-free and globals hang off `window` (`DATA`, `GS`,
`ENGINE`, `UI`). This made a headless Node smoke test possible (see "Testing").

## What's implemented (all 12 GDD core systems)

- Per-area grids with `ready` / `cooldown` / `locked` tile states
- Click-to-harvest → drops to inventory → weighted tier roll → cooldown
- Mine **durability** (tiers 3-5 need multiple clicks to break)
- Inventory bar
- Crafting panel: recipe cards, ingredient availability coloring, Craft / Craft 10,
  "This Area" vs "Show All" filter
- Gold: passive +1/10s drip, 10% per-harvest bonus, one-time milestone craft bonuses
- Upgrades modal: tier unlocks (50/200/800/3000G), speed (−20%/lvl, ×3),
  automation (I/II/III @ 500/1500/4000G)
- Tile unlocks with scaling Gold cost (+25G per tile beyond initial)
- Area-unlock gating via milestone crafts (Wooden Fence→Farm, Iron Pickaxe→Mine,
  Fishing Rod→Fishing)
- Automation tick: harvests highest-tier ready tiles first, runs across all areas
- Worldstone win check + win screen (time played, gathered, crafted)

## Key design decisions / deviations from the GDD

1. **Station-gating cut to avoid dependency cycles.** The GDD's stations have circular
   deps (Forge needs Copper Ingot but is meant to *unlock* metal recipes;
   Workbench→Tier 3 and Enchanting→Essence Extract are similar, and the Worldstone
   needs Essence Extract). Resolution:
   - **Only the Worldstone is gated** (behind the **Enchanting Table** — clean, acyclic).
   - **Workbench / Forge are optional milestone crafts** that grant one-time Gold
     bonuses; all chain recipes are always craftable when you have materials.
   - *If you want literal GDD station-gating, the chains need reworking to break the
     cycles (e.g. a no-station "Bloomery" pre-recipe for the first Copper Ingot).*
2. **Tier spawn** = simple weighted random among currently-unlocked tiers using base
   weights `[70,20,7,2.5,0.5]`. The GDD's "higher-tier upgrades shift probabilities
   upward" is NOT yet implemented (no progressive shifting).
3. **Cooldown timing**: next tier is rolled at harvest time and the regrow timer uses
   *that* tier's duration (so a Void tree takes 90s to grow back).
4. **Speed upgrades** are multiplicative: `timer × 0.8^level`.

## Testing

- Headless engine smoke test (20 assertions, all passing) lived in the scratchpad —
  it stubs `window`, loads `data/state/engine.js` in VM scopes, and exercises
  harvest/craft/unlock/upgrade/automation/win. Re-create it if you want regression
  coverage (it is NOT committed; engine globals make it ~80 lines).
- Verified live in-browser: click→harvest→craft→inventory loop, cooldown rendering,
  gold accrual — all working, no console errors.

## Known quirks

- `preview_screenshot` times out: the ready-tile `pulse` is an infinite CSS animation,
  so the capture tool never gets a settled frame. Game is fine; use the accessibility
  snapshot / `preview_eval` instead. (Could gate pulse behind `prefers-reduced-motion`.)
- The preview tool's synthetic click may not fire `onclick`; native `element.click()`
  and real user clicks work.

## Suggested next steps (not yet done)

- [ ] **Balance pass** — gold costs, tile-unlock scaling, drop rates vs the GDD's
      ~4hr target progression curve (Section 8).
- [ ] **Progressive tier probability shifting** on tier upgrades (GDD Section 4 intent).
- [ ] **Win sequence polish** — GDD Section 9: tiles light up area-by-area before the
      win modal; add the craft particle/glow.
- [ ] **Automation visuals** — GDD wants a semi-transparent hand/cursor doing the
      area's action animation; currently only an "AUTO" label + flash.
- [ ] **Crafting search box** (GDD panel shows a Search/Filter field; only the
      area toggle exists).
- [ ] Optional: revisit literal station-gating (see decision #1) if desired.
- [ ] Optional: per-area action flavor (axe swing / splash / etc.) — currently shared.

## Git

- This folder is its own git repo. Initial commit: `Initial commit: Idle Grounds
  prototype (GDD v0.1)`.
- Commit only when asked. Keep all git operations inside this repo.

## Last session summary

Built the full prototype from the GDD, verified the core loop live in the browser,
and committed it. Spent time correcting an early mistake where the session (rooted in
the Legend folder) caused preview/git defaults to point at the wrong project — fully
reverted, no changes left in Legend. This handoff exists so the next session starts
cleanly rooted in THIS folder.
