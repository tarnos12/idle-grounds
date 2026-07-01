# Idle Grounds — Session Handoff

> Read this first when starting a new Claude Code session. It captures the
> current state of the prototype, key decisions, and what's left to do.

## ⚠️ Session setup (important)

- **Work ONLY in this folder:** `C:\Work\Marrow Tap Projects\Farm Prototype Claude`
- Start the session **rooted in this folder**. Do **not** touch the sibling
  `Legend of the Fallen Warrior - ORIGINAL` folder — separate, unrelated project.
- Verify on startup: `git rev-parse --show-toplevel` should print this folder.

## What this is

A browser-based sandbox/idle prototype ("Idle Grounds"). It began from a GDD but
has since pivoted into a **spatial, mouse-driven world game**: a `+`-shaped map of
four areas, physical resource nodes you harvest onto the ground, a cursor "hand"
that carries items, and buildings you place and feed.

The four areas branch off a centre: **Forest** (centre), **Farm** (left),
**Mine** (right), **Fishing** (down); the up arm is reserved/empty.

## Tech / how to run

- **Vanilla HTML/CSS/JS, no build step.** Plain `<script>` tags (not modules).
- **Run it:** double-click `index.html`, OR `node server.js` (static, port 5174)
  and open `http://localhost:5174`. `.claude/launch.json` defines the preview
  server `idle-grounds`.
- **View at a real desktop width** and reasonably tall — the world viewport is
  768×768 with a build menu / top bar around it.

## File map

| File | Role |
|------|------|
| `index.html` | Top bar (area, hand count, Build, Upgrades), world viewport, build menu, hand-cursor overlay, modals |
| `style.css`  | Theme, world ground/zones, nodes, ground items, buildings/ghosts, arrows, hand cursor, build menu, camera frame |
| `js/data.js` | Config: areas × 5 tiers (per-area `interaction`), `GRID` (cell/cells/margin/building), `ZONES`, `BUILDINGS`, `WORLD` (+ layout, arrow costs), `HAND_CAP`, `COSTS`, `TEST` |
| `js/state.js`| `window.GS`: `hand`, per-area `{nodes,ground,buildings,spawnQueue,upgrades}`, `world`, `build` |
| `js/engine.js`| Pure logic: hand ops, tier roll, zone/spawn, harvest, ground drop/pickup, buildings, upgrades, world/camera, ticks |
| `js/ui.js`   | Rendering + all mouse/keyboard interaction (viewport-level hit testing), camera |
| `js/main.js` | Bootstrap: init areas, wire input, run the 2 tick intervals |
| `server.js`  | Dependency-free static server |

Globals hang off `window` (`DATA`, `GS`, `ENGINE`, `UI`). Engine is DOM-free.
NOTE: classic scripts share ONE global scope — `CELL` is declared once (engine.js)
and reused in ui.js; don't redeclare shared `const`s across files.

## Current model (what's implemented)

**World / camera**
- Each area's **playable grid is 24×24** cells (32px). It sits inside a **34×34
  world** with a **5-cell inert border** (dimmed, framed) — no harvesting,
  dropping, or building there.
- **Camera** pans with **WASD** or by **left-dragging empty land**; clamped to the
  world, recenters on area change. Hit-testing reads the grid's transformed rect,
  so clicks stay accurate at any pan.
- **Travel arrows** on the edges open/switch areas; opening costs resources paid
  from the hand (kept ≤ hand cap). Camera slides/recenters on move.

**Resource nodes (spawn in reserved zones, relocate on depletion)**
- Nodes spawn RANDOMLY into an area's spawn zone — Forest/Farm use the four **8×8
  corners**; Mine/Fishing use the **centre 8×8**. There are NO buyable slots.
- Harvesting a node **depletes it (removes it) and queues a respawn** at a random
  free zone slot after the regrow timer (`depleteNode` + `spawnQueue`).
- **Per-area `interaction`:** `chop` (forest — many swings, wood each swing +
  bonus on felling), `instant` (farm — one click), `break` (mine — ore only on the
  final strike; 1×1 ore & 2×2 boulders), `surface` (fishing — a fish is catchable
  for `surfaceWindow`s, else it dives/relocates).

**Ground items + hand carry (no global stockpile)**
- Harvesting **drops items on the ground** where the node was.
- The **hand** (cursor) carries up to `HAND_CAP` (20), ordered stacks. **Hold left**
  over items to vacuum them in; **right-click** drops 1 at a time, ramping 1→5/s
  after a 1s hold. A cursor overlay shows what's carried.
- Left-click routing (viewport-level): node → harvest; on/near a pile → vacuum;
  empty land → drag-pan.

**Buildings (placement + construction; no function yet)**
- **Build** button opens a bottom menu of **unlocked** buildings. Select → a 2×3
  **ghost** previews (green ok / red blocked; can't overlap zones/occupied/off-grid).
  Left-click places; Esc / right-click cancels.
- **Right-click a ghost** with a needed resource in hand to feed it; it **builds**
  when fully paid (e.g. Forge = 5 wood + 10 stone). Built buildings do nothing yet.

**Upgrades (incrementally funded from hand)**
- Upgrades modal: tier / speed / automation, paid in each area's base resource.
- Because the hand caps at 20, upgrades are **funded incrementally** — each click
  pays as much as the hand holds and tracks `upgrades.paid[type]`; the upgrade
  applies once fully covered. The button shows remaining cost + "paid X/Y".

## Key decisions

1. **No gold — economy is 100% resources.** Base resource per area funds arrow
   unlocks + upgrades (Forest→wood, Farm→wheat, Mine→stone, Fishing→fish).
2. **Areas unlock via arrows** (resources), not crafting. The old crafting panel
   and global inventory are gone; carry is the hand + ground.
3. **Render split (important):** the 100 ms tick calls `renderPlay()` which rebuilds
   ONLY the passive world grid. `render()` (full — also rebuilds arrows + build
   menu) is used on discrete events. Rebuilding interactive UI every tick caused
   hover flicker + dropped clicks; keep this invariant.
4. **Viewport-level input:** all world interaction is hit-tested from cursor→GS at
   the viewport listener, so the grid DOM can rebuild every tick without losing
   clicks (harvest fires on mousedown, not on a per-node handler).
5. **Sprites are emoji placeholders** (`TIER_SPRITES`, `BUILDINGS[].icon`).

## Testing knobs (`DATA.TEST`)

- `ENABLED` master switch; `timeScale: 0.2` (regrow ×0.2); `costScale: 0.5`
  (arrow/upgrade cost ×0.5).

## Verifying in the headless preview

- `requestAnimationFrame` is **throttled** headless — so the WASD/drag pan loop
  and the mouse hold-loops (vacuum, drop-accel) won't advance there. Verify their
  ENGINE ops directly; confirm live *feel* in a real browser.
- `preview_screenshot` tends to **time out** (infinite CSS animations) — use
  `preview_eval` / DOM geometry instead.
- The preview window can default to **1px wide**; `preview_resize` to a desktop
  size before checking layout.

## Suggested next steps

- [ ] **Give buildings a function** (they only place/build right now).
- [ ] **Save/persistence** — a refresh currently wipes all progress.
- [ ] **Real sprite art** to fill the intended larger silhouettes.
- [ ] **Balance pass** (costs, timers, spawn counts, drop rates).
- [ ] **Flesh out the empty ↑ arm**; progressive tier-probability shifting.
- [ ] Automation vs multi-hit areas: it does one hit per node per tick (slow).

## Git

- Own git repo, no remote, history on `master`. Commit only when asked.
- Recent: pannable camera + flicker fix → 24×24 zones/ground/buildings →
  per-area mechanics → +-world / gold removal.

## Last session summary

Built the spatial overhaul: 24×24 zoned grids with random-spawn/relocate nodes,
ground-item drops + a 20-item hand (hold-to-vacuum, right-click drop), building
ghosts fed by resources, a pannable camera (WASD + drag) with a 5-cell inert
border, and fixed tick-driven flicker on arrows/build menu. Upgrades are now
funded incrementally from the hand so expensive ones aren't blocked by the cap.
