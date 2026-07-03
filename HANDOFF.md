# Idle Grounds — Session Handoff

> Read this first when starting a new Claude Code session. It captures the
> current state of the prototype, key decisions, and what's left to do.

## ⚠️ Session setup (important)

- **Work ONLY in this folder:** `C:\Work\Marrow Tap Projects\Farm Prototype Claude`
- Start the session **rooted in this folder**. Do **not** touch the sibling
  `Legend of the Fallen Warrior - ORIGINAL` folder — separate, unrelated project.
- Verify on startup: `git rev-parse --show-toplevel` should print this folder.

## What this is

A browser-based sandbox/idle prototype ("Idle Grounds"): ONE continuous,
mouse-driven world you pan around. Physical resource nodes drop items on the
ground; a cursor "hand" carries them; buildings are placed as ghosts and fed
resources to construct. Originally from a GDD, long since pivoted.

## Tech / how to run

- **Vanilla HTML/CSS/JS, no build step**, plain `<script>` tags (shared global
  scope — don't redeclare `const`s across files; `CELL` lives in engine.js).
- **Run:** `node server.js` (port 5174) or double-click `index.html`.
  `.claude/launch.json` defines preview server `idle-grounds`.
- Progress **autosaves to localStorage** every 5s (`state.js`, key
  `idle-grounds-save-v1`); the **↺ Reset** button wipes it.

## The world (one continuous map)

- Regions on a 3×2 region grid, each **75×75 cells** (32px): **Farm** (left),
  **Center** (middle — the old "forest"), **Mine** (right), **Fishing** (below
  centre). Bottom corners + gaps are void. `DATA.WORLD.regions`.
- **5-cell void gap** separates adjacent regions; 10-cell margin rings the map.
- **No travel arrows** — pan with **WASD only** (no drag-pan); **Shift
  toggles sprint** (2x pan speed, 🏃 tag in the location pill). The camera
  window is fixed (~35 tiles); viewport scales to the window.
- **Locked regions:** populated & present but the camera clamps to the union
  of unlocked regions + the gap, so **zero pixels of a locked region are ever
  visible**. Edge buttons ("🔓 Unlock Farm — N🪵") pay from the hand; unlocking
  just widens the camera range and enables interaction.

## Region contents (spawners / fixtures / generators — `DATA.AREAS`)

- **Center:** trees (chop) in the two top-corner zones; 10 small 1×1 bushes
  (chop → leaves, capped, spread ≥6 cells) in the buildable centre; a fixed
  **2×2 quarry** bottom-left (1 stone per 5 clicks; hold = auto-mine 1/s); a
  **clay patch** bottom-right auto-spawning pickable clay (cap 10).
- **Farm:** crops ONLY in its central zone as **3×3 plots** (8, unscaled);
  **sand flat** in the middle-left band (generator like clay, cap 10).
- **Mine:** 1×1 ore + 2×2 boulders (break — yield only on the final strike)
  in its centre. **Fishing:** surfacing fish (catch within ~3s or they dive).
- Interactions per node: `chop` (multi-swing; yield **only on the clearing
  swing** — accumulated via `node.pending`), `instant`, `break`, `surface`,
  `quarry`. Depleted nodes **relocate**: respawn in a random free zone slot.

## Carrying / economy (no global inventory)

- Harvest drops items **on the ground** — 1 icon per item, never stacked;
  a per-tick repulsion (`settleGround`) keeps them apart.
- **Hand** (cursor): hold-left vacuums within **1 cell**; cap `GS.handCap`
  (20, mutable). Right-click drops 1; hold ramps 4→20/s after 1s.
- Manual clicks rate-limited to ~10/s (`CLICK_COOLDOWN`); holding auto-swings
  at each node's own `swingMs` (chop 350 / mine 450 / quarry 1000...).
- **Storehouse** (built building) = visible single-item container (cap 200):
  right-click deposits matching items, left-click/hold withdraws (1/s → 5/s
  over 3s). Shown on the building: "🪵 Wood ×47".
- **Buildings:** `B` or 🔨 opens the menu → 3-wide × 2-tall ghost (green/red
  preview) → right-click-feed resources to construct. Blocked on each
  region's `noBuild` zones (accepts an array, e.g. farm's centre + sand band).
  Only the Storehouse *does* anything yet.
- **Upgrades live in the 🏛️ Center building** (indestructible, pre-placed
  mid-centre). Left-click it → menu; SELECT an upgrade → its cost shows on
  the building like a ghost; right-click-feed the region's base resource to
  fund it (`GS.upgradeJob`). Selecting a different upgrade drops whatever
  was fed into the previous one. Types: tier unlock, regrow speed, **Action
  Speed** (swing rate −20%/lvl), **Quarry Yield** (−1 click/stone per lvl,
  centre only), automation.
- **🗑 Demolish** (bottom bar): next building clicked is destroyed — a
  complete building refunds 100% of its cost (+ storehouse contents), a
  ghost refunds only what was inserted; refunds drop on the ground. The
  Center building can't be demolished. Esc/right-click cancels the mode.

## Performance invariants (a regression here stalled whole machines)

1. **Never promote the world div to a GPU layer** (no `will-change` — it's
   ~8000×5600px; re-rasterizing it 10×/s froze PCs).
2. **Render only what's on camera**: `renderRegion` culls every element to
   the view (+4-cell fringe). ~40 DOM elements on screen vs ~400 in state.
3. **Idle ticks don't repaint**: `gameTick()`/`settleGround()` return changed
   flags; main.js repaints only when changed or `needsLiveRepaint()` (visible
   fish countdown / AUTO badge). Idle = 0 rebuilds.
4. **Interactive UI (unlock buttons, build menu, bottom bar) is rebuilt only
   on discrete events**, never on ticks (tick-rebuild caused hover flicker +
   eaten clicks). Pan/preview repaints coalesce via `requestGridPaint()`.
5. All world input is hit-tested at the viewport level from cursor→GS
   (`pointFromEvent` → region + region-local coords), so DOM rebuilds never
   lose clicks. Text selection is disabled globally.

## Testing knobs & debug

- `DATA.TEST`: `ENABLED`, `timeScale 0.2` (regrow ×0.2), `costScale 0.5`.
- **🐞 Debug** toggles per-node swing/click counters. **↺ Reset** wipes save.
- Headless preview: rAF is throttled (hold-loops/pan don't advance — call
  engine fns directly), `preview_screenshot` times out (infinite CSS anims),
  resize needs a `window.dispatchEvent(new Event('resize'))` after
  `preview_resize`. Verify logic via `preview_eval`.

## Suggested next steps

- [ ] **Building functions beyond Storehouse** — needs a design call: with
      the crafting panel gone, what do Workbench/Forge do? (e.g. Forge
      converts ore→ingots dropped into it; Workbench unlocks building types.)
- [ ] Offline/idle catch-up on load (saves store absolute timestamps; away
      time currently just fires everything due at once).
- [ ] Enrich Mine/Fishing like Center/Farm got (unique sub-features).
- [ ] Feedback polish: throttled-click "fake hit" animation/SFX, hit
      particles, floating +N numbers.
- [ ] Real sprite art; balance pass; the empty region slots.

## Git

- Own git repo, no remote, history on `master`. Commit only when asked.

## Last session summary

Merged the four areas into one continuous pannable map (regions as side
extensions of the Center with 5-cell void gaps; camera hard-clamped so locked
regions never show; edge-button unlocks), reworked the farm (central 3×3
crops, middle-left sand generator), fixed a machine-stalling rendering
runaway (GPU layer + full-DOM ticks → culling + dirty-flag repaints), made
the Storehouse a visible single-type container with paced withdraw, added
localStorage autosave + Reset, and added Action Speed / Quarry Yield upgrades.
