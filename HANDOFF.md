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

A browser-based incremental/idle sandbox ("Idle Grounds"). Originally built from the
GDD at `C:\Users\tarno\Downloads\GDD_IdleGrounds_v0.1.md`, then pivoted to a **single
spatial tilemap world** (see "Architecture pivot" below).

The world is one big **+ shape**: **Forest** (centre), **Farm** (left), **Mine**
(right), **Fishing** (down), and an empty reserved arm (up). The win condition is
still crafting the **Worldstone**.

## Tech / how to run

- **Vanilla HTML/CSS/JS, no build step.** Plain `<script>` tags (not ES modules) so it
  runs over `file://`.
- **Run it:** double-click `index.html`, OR serve with the bundled
  `node server.js` (static server, no deps, port 5174) and open `http://localhost:5174`.
- A `.claude/launch.json` here defines a preview server named `idle-grounds`
  (`node server.js`, port 5174). The Claude preview tool reads the **session root's**
  launch.json, so this only works if the session is rooted in this folder.
- **View at a real desktop width.** The world viewport is a fixed 640×640; a fixed
  320px crafting panel sits beside it, so a narrow window squeezes the layout.

## File map

| File | Role |
|------|------|
| `index.html`   | DOM: top bar, world viewport (`#grid` + `#arrows`), crafting panel, inventory, modals |
| `style.css`    | Dark theme, world ground tints, node sprites, travel arrows, slide anim, panels |
| `js/data.js`   | Static config: areas × 5 tiers, `GRID` (cell/grid sizing), `WORLD` (+ layout, arrow costs), recipes, resource costs, `TEST` knobs |
| `js/state.js`  | Mutable `window.GS`; lays out each area's nodes on a lattice (`makeAreaNodes`) |
| `js/engine.js` | Pure logic (no DOM): tier rolls, harvest, durability, crafting, resource costs, node/area unlocks, camera moves, ticks |
| `js/ui.js`     | DOM rendering: world viewport, node sprites, arrows, crafting/inventory/upgrades (rAF-coalesced) |
| `js/main.js`   | Bootstrap: prime nodes, wire events, start the 2 game-loop intervals |
| `server.js`    | Dependency-free static file server for local preview |

Architecture note: engine is DOM-free and globals hang off `window` (`DATA`, `GS`,
`ENGINE`, `UI`).

## What's implemented

- **Single +-shaped world** with a **camera locked to one area** at a time. Each area
  is a **20×20 grid of 32px cells** (640×640px), with a per-area ground tint.
- **Resource nodes** on a lattice: each occupies a **2×2 footprint** but draws a
  **larger emoji sprite that overflows upward** (z-ordered by row so nearer nodes
  layer in front). Sprites are **emoji placeholders** (per-tier, see `DATA.TIER_SPRITES`).
- **Click-to-harvest** → drops to inventory → weighted tier roll → cooldown → regrow.
  Mine durability (tiers 3-5 need multiple clicks) preserved.
- **Travel arrows** on the four edges. A locked arrow shows its resource cost; clicking
  it **spends the resources, opens that area, and moves the camera there**. Once open,
  clicking an arrow just pans (with a directional slide animation). The up arm is a
  disabled `🔒 ???` placeholder.
- **Per-tile (plot) unlocks**: each area starts with `initialActive` central nodes;
  the rest are **locked plots** cleared by spending the area's base resource.
- **Crafting panel**: recipe cards, "This Area" vs "Show All" filter, Craft / Craft 10.
- **Upgrades modal**: tier unlocks, speed (−20%/lvl ×3), automation (I/II/III) — all
  **paid in each area's base resource**; the modal shows each area's resource bank.
- **Automation tick** harvests highest-tier ready nodes across unlocked areas.
- **Worldstone win check** + win screen (time / gathered / crafted).

## Key design decisions

1. **Gold removed entirely.** The economy is **100% resources** now. Each area has a
   `base` resource that funds its plot unlocks and upgrades: Forest→`wood`,
   Farm→`wheat`, Mine→`stone`, Fishing→`fish`. Area (arrow) unlocks are paid from
   current inventory (all in `wood`, since you start in Forest) — see `WORLD.unlockCost`.
2. **Areas unlock via the world arrows, not via crafting.** The old recipe-gated area
   unlocks (Wooden Fence→Farm, etc.) are gone. Those milestone recipes still exist as
   craftable **items** but no longer gate anything (candidates for removal/repurposing).
3. **No dependency cycles in unlocks.** All area-unlock costs are payable with Forest
   `wood`, so no area can deadlock. (Earlier a recipe-gating cycle blocked Mine/Fishing;
   that whole mechanic is now replaced by resource-paid arrows.)
4. **Render loop avoids full DOM rebuilds.** The 100 ms tick calls `requestLiveTick()`
   → `refreshGrid()`, which updates cooldown timers **in place** and rebuilds only nodes
   whose state changed. A full `render()` every frame previously destroyed the node
   under the cursor mid-click (hover flicker / dropped clicks); keep this invariant.
5. **Sprites:** 2×2 footprint for occupancy/click, larger visual that overflows. Emoji
   can't truly fill the intended 4×6-cell silhouette — swap `TIER_SPRITES` for real
   PNG/SVG art later; the footprint/overflow/z-order plumbing is already there.
6. **Tier spawn** = weighted random among unlocked tiers (`[70,20,7,2.5,0.5]`); no
   progressive shifting yet. Cooldown rolls the next tier at harvest time.

## Testing knobs (`DATA.TEST`)

- `ENABLED` — master switch; set `false` to restore base balance.
- `timeScale: 0.2` — cooldown/regrow length multiplier (15s → 3s).
- `costScale: 0.5` — multiplier on node-unlock / arrow-unlock / upgrade costs.

## Verifying in the headless preview

- `preview_screenshot` **times out** — the ready-node pulse is an infinite CSS
  animation, so the capture never settles. Use `preview_eval` / DOM geometry instead
  (could gate the pulse behind `prefers-reduced-motion`).
- `requestAnimationFrame` is **throttled** in the headless preview, so the rAF-driven
  live tick lags there — validate `refreshGrid()` logic by calling it directly.
- The preview window defaults to **1px wide**; call `preview_resize` to a real desktop
  width (e.g. 1280×820) before checking layout.

## Suggested next steps

- [ ] **Real sprite art** to fill the 4×6 silhouette (replace emoji in `TIER_SPRITES`).
- [ ] **Balance pass** on resource costs, plot-unlock scaling, drop rates.
- [ ] **Flesh out the empty (up) arm** — give it an area + content.
- [ ] **Progressive tier probability shifting** on tier upgrades.
- [ ] Decide the fate of the now-ungating **milestone recipes** (fence/pickaxe/rod).
- [ ] Optional: per-area harvest flavour, automation hand/cursor visual.

## Git

- This folder is its own git repo (no remote). Commit only when asked; keep all git
  operations inside this repo. History is on `master`.

## Last session summary

Removed gold; rebuilt the game from a tab-per-area button grid into a single
**+-shaped tilemap world** with a camera locked per area, edge **travel arrows** that
open adjacent areas for resources, 20×20 grids with 2×2 nodes drawing larger
overflowing emoji sprites, and a fully **resource-based economy** (plot/area unlocks
and upgrades all cost each area's base resource). Verified the whole loop end-to-end
(harvest → clear plot → travel → upgrade) with no console errors.
