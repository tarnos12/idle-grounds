# Idle Grounds — Session Handoff

> Read this first when starting a new Claude Code session. It captures the
> current state of the prototype, key decisions, and what's left to do.

## ▶ NEXT SESSION: start here (agreed 2026-07-04)

Build the **Forge function + combat upgrades** in one batch:
1. **Forge smelts Iron Ore → Iron Bar** (first tier-2 resource): right-click
   feed ore (+ maybe wood as fuel) into a built Forge; it converts on a timer
   and drops Iron Bars on the ground. Reuse the pattern for future
   converter buildings (Workbench etc.).
2. **Combat branch in the upgrade tree**: enemy cap (+1 fox/level), click
   damage, AoE strike — config hooks already exist in `AREAS.center.enemies`;
   Spirit Essence is the intended currency.
3. Then dragon stage 3 can demand Iron Bars and unlock the next layer.

## ⚠️ Session setup (important)

- **Work ONLY in this folder:** `C:\Work\Marrow Tap Projects\Farm Prototype Claude`
- Start the session **rooted in this folder**. Do **not** touch the sibling
  `Legend of the Fallen Warrior - ORIGINAL` folder — separate, unrelated project.
- Verify on startup: `git rev-parse --show-toplevel` should print this folder.
- **Always give the user the game link** after changes: http://localhost:5174

## What this is

A browser-based sandbox/idle prototype ("Idle Grounds") drifting toward a
**cultivation/xianxia** theme (Sleeping Dragon, Spirit Tree, Fox Spirits…):
ONE continuous, mouse-driven world you pan around. Physical resource nodes
drop items on the ground; a cursor "hand" carries them; buildings are placed
as ghosts and fed resources to construct. 100% resource economy — no gold.

## Tech / how to run

- **Vanilla HTML/CSS/JS, no build step**, plain `<script>` tags (shared global
  scope — don't redeclare `const`s across files; `CELL` lives in engine.js).
- **The world AND the upgrade tree render on `<canvas>`** (no DOM rebuilds).
  DOM is only overlays: bottom bar, unlock buttons, build menu, hand cursor,
  tooltip, modals.
- **Run:** `node server.js` (port 5174; sends `Cache-Control: no-store`).
  `.claude/launch.json` defines preview server `idle-grounds`. Assets are
  version-tagged (`?v=N` in index.html) — **bump on every change batch**.
- Progress **autosaves to localStorage** every 5s (`state.js`, key
  `idle-grounds-save-v1`); the **↺ Reset** button wipes it (`clearSave()`
  sets `saveDisabled` so the beforeunload autosave can't resurrect it).
  `loadState()` migrates old saves idempotently: scrubs dead items/kinds
  against the CURRENT config, drops corrupt entries, resets deco rings.

## ⚠️ Firefox canvas emoji (the "dark film" saga)

Firefox mishandles Windows 11's Segoe UI Emoji (COLR v1) in canvas — glyphs
render as dim fillStyle-tinted silhouettes (reads as a grey film over all
icons). Fixes that must stay:
- `EMOJI_FONT = '"Twemoji Mozilla", "Segoe UI Emoji", …'` (Twemoji FIRST).
- Explicit bright `ctx.fillStyle = C.text` before every emoji `fillText`.
- Canvas contexts use `{ willReadFrequently: true }` (CPU rasterization —
  GPU-composited canvases corrupt on some Windows drivers).
- Backing store maps to whole device px; CSS size = backing/dpr exactly.

## The world (one continuous map)

- Regions on a 3×2 region grid, each **75×75 cells** (32px): **Farm** (left),
  **Center** (middle), **Mine** (right), **Fishing** (below centre).
- **5-cell void gap** between regions; camera clamps to unlocked regions +
  gap so locked regions never show a pixel. Edge buttons pay wood to unlock.
- **WASD pan** (Shift toggles 2× sprint), **wheel zoom 1×–3×** (2× default),
  16:9 viewport. No drag-pan.

## Region contents (`DATA.AREAS`)

- **Center:** 10 small bushes (chop → leaves) mid; **Spirit Tree** (4×4,
  top-centre band) — the only wood source: quarry-style manual clicking
  (3 clicks → 2-3 wood), NO passive output; **quarry rock** bottom-left
  (5 clicks → 1 stone manual, unlimited, PLUS passive top-up to 10 stones in
  its field); **clay patch** bottom-right (generator, cap 10);
  **Sleeping Dragon 🐉** (5×5 building, top-left corner) — feed each stage's
  tribute (`DATA.DRAGON_STAGES`: 15 leaves → Forge; stone+clay → Algae Farm;
  iron+algae+water → TBD) to unlock recipes; future story hook;
  **Fox Spirits 🦊** (top-right corner, red-tinted zone) — wander, 3 clicks
  to kill (hold = auto-attack), drop Spirit Essence, respawn; cap 1
  (cap/damage/AoE are future upgrade hooks). Config: `AREAS.center.enemies`.
- **Farm:** 3×3 wheat plots + 2×2 cotton patches (centre zone only); sand
  generator in the middle-left band.
- **Mine:** stone ore (drops stone+clay) + tougher **iron veins** (⚙️, 3
  hits → iron ore) in its centre.
- **Fishing:** surfacing **algae** (100) and **fish** (40) — mostly algae
  early, by design; a **spring ⛲** top-left corner (3 clicks → 1 water +
  passive water field, cap 10). The **Algae Farm** building (dragon stage 2)
  is `waterOnly`: places ONLY inside the fishing waters, passively grows
  algae around itself (cap 8 nearby).
- Items: wood, leaves, wheat, cotton, stone, clay, sand, iron_ore, fish,
  algae, water, spirit_essence. Interactions: `chop`, `instant`, `break`,
  `surface`, `quarry` (fixtures, `dropMin..dropMax`).

## Carrying / economy

- Drops lie **on the ground** (1 icon per item, repulsion, building
  colliders). Hold-left = gravity suction (2-cell radius); right-click drops
  (4→20/s ramp). Hand cap 20, +5 per Hand Size level.
- **Storehouse** = visible single-item container (cap 200, paced withdraw).
- **Feeding rule everywhere** (ghosts / Altar / Dragon): front hand stack
  feeds if needed; otherwise the click reorders a needed item to the front
  (`feedNeeds` in engine.js).
- **Costs are multi-resource (max 3 types)**: building `cost` maps and each
  upgrade-tree node's own `costs: [lvl1, lvl2, lvl3]` array in
  `DATA.UPGRADE_TREE`. (Tier-2 conversions — e.g. Iron Ore → Iron Bar at the
  Forge — are the intended next economy layer.)
- **Upgrades:** click the **Altar** (5×5, exact centre) → nodebuster-style
  canvas tree (square nodes, GREEN=buyable / GOLD=maxed / RED=locked, "?" at
  distance 2, hidden ≥3, WASD pans, instant tooltip). Selecting sets
  `GS.upgradeJob {needs, paid}`; feed the Altar to fund; switching refunds.
- **Demolish** refunds 100% built / partial ghosts; Altar & Dragon are
  indestructible.

## Performance invariants (a regression here stalled whole machines)

1. **Dirty-flag rendering**: `gameTick()` returns changed; repaint only when
   changed or `animActive()` (hit squash, fish bob, AUTO badge, **visible
   enemies**, dragon msg). Idle = 0 draws. Enemy wandering deliberately does
   NOT set changed — the UI animates them only while on screen.
2. Coalesced paints via `requestGridPaint()` (max 1/frame, self-chains only
   while animating).
3. Interactive DOM (unlock buttons, build menu) rebuilt only on discrete
   events, never on ticks (tick-rebuild = hover flicker + eaten clicks).
4. Input is hit-tested from cursor→state (`pointFromEvent`), decoupled from
   rendering; interactions gate on `E.isAreaUnlocked`.
5. LAYERED painter: all grounds → locked stacks+veils → unlocked objects →
   item icons on top (a later region can never cover an earlier one's sprites).

## Testing knobs & debug

- `DATA.TEST`: `ENABLED`, `timeScale 0.2`, `costScale 0.5` (dragon tribute
  is also scaled).
- **🐞 Debug** shows node click/hit counters; tree modal has its own Debug
  (reveal hidden nodes). **F9** = rendering self-diagnostic alert.
- Headless preview: rAF throttled (call engine fns directly),
  `preview_screenshot` may time out (sample canvas pixels via
  `getImageData` instead), `window.dispatchEvent(new Event('resize'))` after
  `preview_resize`, stub `alert` for F9. Module-level `cam`/`lastDrawError`
  are reachable from `preview_eval` (classic-script globals).

## Suggested next steps

- [ ] **Forge function**: convert Iron Ore → Iron Bar (first tier-2
      resource — the user explicitly plans resource conversion).
- [ ] Dragon stage 3+ rewards, story dialogue UI for stage-ups.
- [ ] Combat upgrades: enemy cap, click damage, AoE attack (hooks exist).
- [ ] More tree nodes (combat/dragon branches); Workbench function.
- [ ] Offline/idle catch-up on load; feedback polish (particles, +N
      floaters, SFX); real sprite art; balance pass.

## Git

- Own git repo, no remote, history on `master`. Commit only when asked.

## Last session summary

Xianxia content drop: 4 new basic resources (water/spring+field in Fishing,
cotton plots, iron veins, algae outnumbering fish) + Spirit Essence;
multi-resource costs everywhere (per-node `costs` in the tree, multi-item
`upgradeJob`); corner trees replaced by ONE Spirit Tree (top-centre,
manual-only wood); Sleeping Dragon (top-left) — feed tribute stages to
unlock Forge → Algae Farm (water-only placement, passive algae); Fox Spirit
enemies (top-right) with click/hold combat, loot and respawn. All engine
paths verified headless via preview_eval; saves migrate (node kinds scrubbed
against config, spawner overshoot trimmed, old job format dropped).
