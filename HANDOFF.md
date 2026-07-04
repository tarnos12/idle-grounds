# Idle Grounds — Session Handoff

> Read this first when starting a new Claude Code session. It captures the
> current state of the prototype, key decisions, and what's left to do.

## ▶ NEXT SESSION: start here

Dragon stages 3+4 and the story-dialog UI are DONE (see "Last session
summary"). Next up, in rough priority order (confirm with the user first):
1. **Workbench function**: it builds but does nothing. Reuse the Forge's
   `smelt` converter config in `BUILDINGS` (rename the concept if it grows
   beyond smelting) — e.g. wood + iron bars → tools; Spirit Herbs are now
   available as a crafting input too (elixirs?).
2. **Post-awakening content**: the dragon awakening sets `GS.won` but
   nothing consumes it yet (the old #win-modal is unused) — a stats screen,
   a prestige loop, or the dragon granting a passive blessing.
3. Offline/idle catch-up on load; feedback polish (particles, +N floaters).

## Session setup

- **FIRST, before ANY work: sync with git.** `git fetch origin`, pull the
  working branch, and check `origin/master` too — new commits can appear
  from the user or from OTHER Claude sessions (local and cloud sessions
  share this repo). If master is ahead, bring those commits into the
  working branch before touching anything.
- **Division of labour (agreed 2026-07-04):**
  - **Cloud sessions** develop, test headless, push — and when the user
    asks to test, they give the ARTIFACT link, never localhost (a cloud
    container's localhost is unreachable from the user's browser; don't
    re-litigate this). To refresh the build:
    `node tools/build-artifact.js <scratchpad>/idle-grounds.html`, then
    publish with the Artifact tool (favicon 🌍), REDEPLOYING TO THE SAME
    URL via the `url:` param:
    https://claude.ai/code/artifact/69f9e3c1-fb9f-45f8-8927-80e5fd02eb67
    — that's the user's bookmark; never mint a new URL. (Artifact
    sandbox: localStorage autosave / confirm() may be blocked — all call
    sites try/catch, the game still runs; mention it's a test build.)
  - **Local sessions** (user's PC, `C:\Work\Marrow Tap Projects\Farm
    Prototype Claude`) own localhost: pull latest, run `node server.js`
    (backgrounded), give http://localhost:5174.
- Merge the working `claude/...` branch into `master` only when the user
  asks — but note the user sometimes fast-forwards master themselves;
  check where master points instead of assuming.

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
  iron bars+algae+water → Herb Garden; herbs+essence+bars → it AWAKENS 🐲,
  gold border, refuses food, sets `GS.won`). Each stage-up opens the story
  dialog modal (#dragon-modal, `GS.dragon.dialog`);
  **Fox Spirits 🦊** (top-right corner, red-tinted zone) — wander, 3 hp,
  click to fight (hold = auto-attack), drop Spirit Essence, respawn; base
  cap 1. The tree's combat branch (foe_cap/foe_dmg/foe_aoe nodes) raises
  the cap (+1/level), click damage (+1/level) and adds an AoE ripple
  (strikes also hit enemies within `aoe lvl × 1.5` cells of the target).
  Config: `AREAS.center.enemies`; state in `areas.center.upgrades`
  (`enemyCap`/`damage`/`aoe`).
- **Farm:** 3×3 wheat plots + 2×2 cotton patches (centre zone only); sand
  generator in the middle-left band.
- **Mine:** stone ore (drops stone+clay) + tougher **iron veins** (⚙️, 3
  hits → iron ore) in its centre.
- **Fishing:** surfacing **algae** (100) and **fish** (40) — mostly algae
  early, by design; a **spring ⛲** top-left corner (3 clicks → 1 water +
  passive water field, cap 10). The **Algae Farm** building (dragon stage 2)
  is `waterOnly`: places ONLY inside the fishing waters, passively grows
  algae around itself (cap 8 nearby).
- Items: wood, leaves, wheat, cotton, stone, clay, sand, iron_ore,
  iron_bar 🧲 (tier-2, Forge-only), fish, algae, water, spirit_essence,
  spirit_herb 🌱 (Herb Garden-only). Interactions: `chop`, `instant`,
  `break`, `surface`, `quarry` (fixtures, `dropMin..dropMax`).

## Carrying / economy

- Drops lie **on the ground** (1 icon per item, repulsion, building
  colliders). Hold-left = gravity suction (2-cell radius); right-click drops
  (4→20/s ramp). Hand cap 20, +5 per Hand Size level.
- **Storehouse** = visible single-item container (cap 200, paced withdraw).
- **Converter buildings** (the Forge; pattern for Workbench etc.): a `smelt`
  config in `BUILDINGS` — `{ inputs, output, outputQty, timeMs, queueCap }`.
  Right-click feed the inputs (same `feedNeeds` rule); each complete set
  queues one batch; `gameTick` runs the queue on a timer (TEST-timescaled)
  and drops the output beside the building. UI shows queue count + a gold
  progress bar (repaints via `animActive`). Demolish refunds undelivered
  batches + the partial feed. Forge: 2 iron_ore + 1 wood → 1 iron_bar / 6s.
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

- [x] **Forge function**: Iron Ore → Iron Bar converter (done).
- [x] Combat upgrades: enemy cap, click damage, AoE attack (done).
- [x] Dragon stage 3 (Herb Garden) + stage 4 (awakening) + story dialog UI.
- [ ] More tree nodes (dragon branch); Workbench function (reuse `smelt`).
- [ ] Post-awakening content (GS.won is set but unused).
- [ ] Offline/idle catch-up on load; feedback polish (particles, +N
      floaters, SFX); real sprite art; balance pass.

## Git

- Remote: `origin` = github.com/tarnos12/idle-grounds (private). Cloud
  sessions develop on their designated `claude/...` branch and push there;
  `master` is the main line. Commit only when asked (cloud sessions: commit
  + push at the end of each batch as instructed).

## Last session summary

Dragon stages 3+4 + story dialog: **spirit_herb 🌱** item; **Herb Garden 🪴**
building (dragon stage 3, cost 10🪵+5💧+5🧱, land placement, `gen` grows
spirit herbs nearby, cap 6). Stage 3's tribute (8🧲+15🪸+10💧) now teaches
it with proper story text. **Stage 4** (20🌱+15✨+5🧲) AWAKENS the dragon:
sprite 🐲, gold border, "watches over the grounds", refuses further food,
sets `GS.won` (unused so far). **Story dialog UI**: stage-ups set
`GS.dragon.dialog`; `syncDragonDialog()` (called from both render paths)
shows #dragon-modal until Continue clears it (persists across reloads
mid-dialog). Verified headless: full stage 0→4 walk, catalog unlocks per
stage, modal show/dismiss via the real render path, herb generation capped,
no draw errors. Assets bumped to `?v=11`.
