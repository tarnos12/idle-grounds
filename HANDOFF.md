# Idle Grounds — Session Handoff

> Read this first when starting a new Claude Code session. It captures the
> current state of the prototype, key decisions, and what's left to do.

## ▶ NEXT SESSION: start here

READ DESIGN.md FIRST — the full economy/building/logistics plan with
done-markers. **The entire roadmap (phases 1-5) is implemented, plus
disciples, full item-sink coverage, offline/idle catch-up, feedback juice,
and deeper prestige (Ascension Shrine perk shop).** Current asset version:
?v=47.

Everything designed is live: gathering, dragon story (4 stages ->
awakening), combat (foxes + baited Spirit Boar), the whole T1/T2/T3
economy, wisp logistics (gatherers/lanterns/seals/furnace-spirit) with a
link-editor UI and fuel system, dragon pills + timed blessings, disciples
(Meditation Pavilion), and the Ascension prestige loop.

Remaining directions are NEW design scope — confirm with the user before
picking:
1. **More regions** — the map has room; each could add unique resources.
2. **Sprite art pass** — swap emoji for sheet art per the DESIGN.md
   wishlist (item icons already use assets/icons/*.png with emoji
   fallback; buildings/nodes/enemies are still emoji).
3. **More prestige perks** — the Ascension Shrine (js/data.js `PERKS`) has
   4 perks; add more (yield %, wisp haste, combat, cheaper unlocks…) — each
   needs one wiring point in engine.js like the existing ones.
4. Polish: SFX are DONE (procedural WebAudio synth, ?v=41). Balance is
   deliberately left in FAST TEST MODE (DATA.TEST.ENABLED=true) — the user
   chose to keep quick play while iterating (2026-07-08); a real GDD-balance
   pass (ENABLED=false) is a future toggle when they're ready to ship slow.
   (Offline catch-up, feedback juice, deeper prestige AND sound are DONE.)

**Every git commit MUST update this file** (this pointer + the Last
session summary below) so a fresh session knows the state; bump the ?v=
asset version on any code change too.

## Session setup

- **FIRST, before ANY work: sync with git.** `git fetch origin`, and
  branch off the LATEST `origin/master` — new commits arrive from the user
  and from OTHER Claude sessions (local + cloud share this repo). `master`
  is always the source of truth; never start from a stale base.
- **Branch policy (agreed 2026-07-05).** Work on a `feature/<kebab-desc>`
  branch named for the feature (e.g. `feature/fast-hold-ramp`), not on
  `master` directly and not on old `claude/…` branches. When the task is
  DONE: merge the branch into `master`, push `master`, and delete the
  feature branch (local + `origin`). Finished work must never be left
  stranded on a branch. (See CLAUDE.md rule #1.)
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
  Cloud sessions still merge their `feature/…` branch into `master` when
  done (per the branch policy above) — the artifact link is just how they
  let the user *test*, separate from where code lands.

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

## Item icon art (assets/icons/)

- All 14 resources use **16px pixel-art icons** from `assets/items_sheet.png`
  (user-provided sheet, 36×35 grid of 16px cells), extracted to
  `assets/icons/<item_key>.png`. `tools/sheet.js` (zero-dep PNG
  decode/crop/scale) does the extraction:
  `node tools/sheet.js crop <sheet> <x> <y> 16 16 1 assets/icons/<key>.png`.
- ui.js preloads them into `ICON_IMGS`; **anything missing falls back to the
  emoji** (`ITEM_ICONS`), so new items work before art exists. Canvas draws
  via `drawItemIcon` / `drawNeedsLine` (qty+icon lists) with
  `imageSmoothingEnabled = false`; DOM overlays via `iconHTML()`
  (`img.item-ico`, `image-rendering: pixelated`).
- Node/building/enemy sprites are still emoji — same sheet + tool can supply
  them later (there are trees, fish, gems, tools… on it).

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

## Tutorial & quests

- **Quest panel** (top-right, collapsible to a chip): sequential chain in
  DATA.QUESTS — each goal() reads LIVE state (hand counts, dragon stage,
  stats.foxKills/buildingsBuilt/upgradesApplied/linksAdded/recipeSwitches/
  totalCrafted, region unlocks), so pre-completed things are instantly
  claimable. Claim advances GS.quest.idx. Stat hooks live in engine.js at
  the relevant actions.
- **Help modal** (bottom-bar button): sections are built on open and
  gated by unlock state (Forge lesson needs dragon stage 1, Algae Farm 2,
  Herb Garden 3, awakened 4; region tips once any region is unlocked).

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

**Rack to top-left + rack/building item collision + version badge (?v=47).**
Three user requests in one pass: (1) the burner fuel rack (3 wide x 2 tall)
now hangs flush with the building's TOP edge on the left (was vertically
centred) — one-line change in ui.js's burner draw branch. (2)
`pushOutOfColliders` now also pushes ground items out of a BUILT burner's
fuel-rack rect (mirrors drawFuelRack's placement; ghosts excluded since no
rack is drawn) — items can no longer slide under racks or footprints.
(3) NEW in-game version badge: `DATA.VERSION = { num, desc }` in data.js
feeds a small "vN" chip beside the brand in the bottom bar; hover shows the
one-line what-changed note. CLAUDE.md rule 3 now says to update
DATA.VERSION together with every ?v= bump. Verified: 6/6 rack-collision
suite (incl. below-rack spot untouched = top-alignment proof), all prior
suites green, screenshot confirms rack at top-left, items pushed clear,
v47 chip visible.

**Map grown ~2x buildable, corners held at 25x25 (?v=46).** GRID.cells
75 → 93 and `_T` decoupled from region size (fixed 25), so the 4 corner
blocks stay 25×25 while the buildable middle bands grow: center buildable
2500 → 5074 cells (2.03×). Applies to ALL regions uniformly (one shared
grid); mine/fishing/volcano/grove spawner counts scale via areaScale (now
15) but their centre zones grew ~3×, so node density actually DROPS
slightly. `setupStarterNetwork` fully re-tuned to the new fixture spots
(altar auto-centres at 44,44; quarry ~79,11; tree ~10,44; fox ~12,80; clay
76-84) — all 18 starter buildings place, collectors within gather radius of
their sources (7.1/6.0/7.0/0.0 cells). NEW MIGRATION: saves now carry a
`gridCells` stamp; loading a save from a different grid re-rolls nodes /
spawnQueue / enemies (fixtures & fields land in the CURRENT zones) while
KEEPING buildings (grid only ever grew, so all coords stay in-bounds) —
note an old save's starter network keeps its old positions (demolish
refunds 100% if re-placement is wanted); a Reset shows the pristine new
layout. Verified: 29/29 geometry+starter suite, 7/7 regrid-migration suite,
all prior regression suites, browser boot clean, building placeable deep in
the new southern band.

**Fuel rack shape → 3x2 (?v=45).** The burner fuel rack is now a 3-wide ×
2-tall grid (was 2-wide × 3-tall), still on the LEFT of the footprint and
still holding 6. Changed `FUEL_CELLS` to a 3-col×2-row layout, `drawFuelRack`
cols=3/rows=2, and the burner draw call to `bx - 3*CELL, by + 1.5*CELL,
3*CELL, 2*CELL` (vertically centred on the 5-tall footprint). Verified in
Firefox: the 6 fuel items fill a 3×2 grid. (Center-map 2× buildable lands
next — grid grows with corners held ~fixed; starter network re-tuned.)

**Workflow-driven UX / a11y / touch pass (?v=44, dynamic-workflow orchestration).**
Same planner→fan-out→verify→fix loop across onboarding, feedback, touch, and
DOM/CSS accessibility. Workflow #1 = 4 dimensions (onboarding, feedback, touch
via Opus; a11y-dom via Sonnet) → Opus verify tagging safe-to-apply vs
real-but-big + behavior-risk. Of 28 findings (2 refuted), 22 were safe; I
applied ~20 (dropped "hide Debug/Reset" since the dev uses them, and folded the
help-nudge into the intro). Shipped via 3 disjoint-file worktree agents:
- **Onboarding:** one-time first-run intro (reuses #welcome-modal; premise +
  goal + controls) gated on a new `introSeen` flag (existing saves migrate to
  true, so only genuinely new players see it); quest-completion now points to
  the dragon→Ascension-Gate endgame; quest 1 teaches WASD/wheel.
- **Feedback:** the previously-dead AUDIO "error" sound + a ✗/🔒 floater now
  fire on every silent rejection — empty-hand/wrong-item/full-building
  right-click (throttled on hold), locked-region click, refused build
  placement, unaffordable unlock/perk, empty withdraw.
- **A11y:** Esc closes ALL modals; aria-labels on emoji/✕ buttons + recipe
  cells + link-remove + quest chips; role="dialog"/aria-modal/aria-labelledby
  + tabindex on every modal; :focus-visible ring; prefers-reduced-motion
  disables the node bob/hit CSS animations; canvas text alternatives;
  maxed-button contrast bumped to AA.
- **Touch (safe slice):** on-screen +/- zoom buttons shown only on coarse-
  pointer devices (desktop unchanged); wired to the same smooth-zoom the wheel
  uses (zoom-in lowers zoomTarget = zooms in).
Verified: engine suites pass; browser confirms fresh-save intro shows once
(dismiss+reload → gone), all aria present, Esc closes modals, zoom buttons
work, no page errors. DEFERRED to the user (real-but-big, all touch): no
touch input path at all, no touch pan, no touch feed/drop, full pinch/gesture
system — i.e. genuine mobile support is a separate medium-large feature.

**Workflow-driven performance pass (?v=43, dynamic-workflow orchestration).**
Same planner→fan-out→verify→fix loop, aimed at the 50ms tick + rAF render
hot paths. Workflow #1 = 4 perf dimensions (tick-cost, render-hotpath,
alloc-gc via Opus; dom-thrash via Sonnet) → Opus verify tagging each with
perf-impact + behavior-risk. Of 12 findings (0 refuted), only 2 were
safe-to-apply clear wins; the other 10 were negligible-impact micro-opts held
back on purpose (regression surface > benefit). Applied:
- **Off-screen gathering-stone repaint (engine.js + ui.js):** the Gathering
  Stone vacuum set `changed=true` on every item-nudge, forcing the heavy
  `renderPlay()` at 20/s even when the stone was off-screen. Now the nudge is
  visual-only (consumption still flags `changed`); `animActive()` gained an
  on-screen-gather-pull check (viewport-culled, early-out) so visible pulls
  still animate at 60fps. Browser-verified: on-screen pull animates,
  off-screen gameTick returns changed=false (no forced repaint).
- **Wisp-link render (ui.js):** replaced two per-link O(buildings) `find()`
  scans with a per-region id→building Map (O(1), byte-identical output).
The codebase was already well-optimized — this pass confirmed that and
removed the one genuine constant-repaint offender. Engine suites + browser
all green.

**Workflow-driven hardening pass (?v=42, dynamic-workflow orchestration).**
A 2-workflow, model-tiered pass (planner/orchestrator/reviewer in the main
loop; Opus for hard logic, Sonnet for mechanical): Workflow #1 = 6 review
dimensions → adversarial Opus verify (refuted 1 false positive via git
history, confirmed 7). Workflow #2 = 3 worktree-isolated fix agents with
disjoint file ownership. Shipped fixes:
- **HIGH (ui.js):** the withdraw hold-loop threw `fxPickup(null,…)` when the
  cursor sat over the inter-region void, wedging `loopRunning=true` and killing
  ALL hold/auto interactions (harvest, pickup, attack, drip-drop, withdraw)
  until reload. Fixed at the root (only paint FX when a real region exists),
  hardened `fxPickup` to no-op on a null/unknown region, and wrapped the rAF
  step in try/catch so no future hiccup can permanently wedge input.
- **MED (engine.js):** Meditation Pavilion `b.buns` is an abstract cultivation
  -cycle counter, not a Spirit Buns inventory — demolish minted Buns from
  cycles (laundering wine 3:1) and `countHeldItems` mis-tallied them. Now:
  accept-gate requires room for a food's full value (no wasted wine), demolish
  refunds no cycles (consumed like fuel), tally ignores cycles.
- **LOW (engine.js):** enemy zone refilled to cap instantly after a multi-kill
  — respawn timer now advances on each spawn, not only on kill.
- **LOW (ui.js):** opening a building menu (recipe/link/roster) now closes the
  other two.
- **LOW (data.js):** grove unlock cost 24 → 20 (≤ base hand cap, honouring the
  stated invariant for real balance).
- **DOC (DESIGN.md):** added done-markers for v39/40/41; un-marked the shipped
  rice/koi/spring-water renames + bamboo second source.
Verified headless (engine 12/12, perks, audit suite) + browser (menu close,
fxPickup guard, clean boot).

**Procedural SFX + mute toggle (?v=41, agent audio + manual wiring).** New
`js/audio.js` = `window.AUDIO`, a tiny WebAudio synth (lazy context on first
gesture, ~0.18 master gain, oscillator+envelope voices, NO assets so the CSP
artifact still works). 13 soft chime-like sounds: harvest, pickup, swing,
craft, build, upgrade, unlock, hit, kill, dragon, ascend, error, click. The
ENGINE stays DOM-free and fires a new optional `window.onSfx(name)` hook at
authoritative events (mirroring the existing `window.onGroundDrop` pattern);
main.js installs `onSfx = n => AUDIO.play(n)`, resumes the context on first
pointer/key, and wires a 🔊/🔇 mute button (persisted to `ig_muted`, default
on). onSfx is detached during offline catch-up so the replay is silent.
Harvest fires only on player swings (`!isAuto`), so automation is silent.
Browser-verified: events route to the synth, mute toggles+persists, all 13
sounds play without throwing, no page errors; perks + audit tests still green.

**Bug-audit fixes (?v=40, agent audit + manual fixes).** A read-only audit
agent surfaced four real defects; all fixed and headless-tested (13/13):
1. **HIGH — firestone was uncraftable as an ingredient.** firestone is both a
   fuel AND an input for Ember Pill (Pill Furnace) and Star Steel (Star Anvil).
   Burner feeding short-circuited every firestone into the fuel rack, so those
   recipes (and thus the Ascension Gate, which needs Star Steel) could never
   start via wisp or normal hand-feed. `endpointAccepts` and `dropFromHand`
   now route a fuel item that is ALSO the current recipe's ingredient into the
   recipe stock instead of burning it; pure fuels (wood) still go to the rack.
2. **MEDIUM — Automation mined fixtures.** `automationTick` auto-harvested the
   Spirit Tree / quarry rock / spring (uncapped, non-depleting) instead of the
   regrowing field nodes it's meant to. Now filters out `n.fixed` fixtures.
3. **LOW — offline bun tally used a phantom key** (`spirit_bun` vs the real
   `spirit_buns`) in `countHeldItems`. Fixed.
4. **LOW — converter progress bar** ignored `prestigeFactor()` and the Ember
   blessing in its denominator, so it lagged after any ascension. Now matches
   the real batch duration.

**Victory overlay + Help refresh (?v=39, agent team).** When the Sleeping
Dragon fully awakens (`GS.won`), a one-time `#ending-modal` overlay now fires
from `renderPlay()` via `maybeShowEnding()` — 🐲 "The Dragon Awakens", a
congratulatory line about the permanent ~11% global-speed blessing, and a
couple of highlight stats (ascensions, total crafted, playtime). The
`endingShown` module flag makes it show once per page load (no persisted
state). The Help modal gained sections for burner fuel racks, the Volcano
and Spirit Grove regions, the Ascension Shrine, and the Stats panel.
Browser-verified: modal shows, dismisses, stays hidden on re-render; all 5
new Help sections render; the 3-perks headless test passes 19/19.

**3 more prestige perks (?v=38, agent team).** Shop 8 → 11: **Deep Roots**
(nodes respawn 10% faster/level — engine `depleteNode` respawn delay),
**Wisp Gale** (lanterns send 10% faster/level — lantern `nextSend`), and
**Battle Fury** (+1 beast damage per strike/level — `attackEnemy` dmg), each
a pure passive via `perkLevel` (no `buyPerk` change). Perks slice of a
parallel "finishing polish" batch; verified headless. (Ending overlay +
Help refresh land next at ?v=39.)

**Furnace layout + 2 perks (?v=37, agent team).** The four fuel burners —
Forge, Kiln, Pill Furnace, Star Anvil — resized **3x4 → 3x5**, and their
fuel rack moved from the top of the footprint to a **2-col × 3-row, 6-slot
rack drawn on the LEFT side, outside the footprint** (visual only — NOT part
of collision), so the crafting face now fills the full 3x5 building
(`drawFuelRack` grid is now 2×3; the burner draw branch draws the face over
the full footprint + the rack at `bx-2*CELL, by+CELL`). Two new
`DATA.PERKS`: **Ascendant Insight** (+1 Ascension Point per ascension per
level, max 3 — engine `ascendReward`) and **Keen Automation** (automation
harvests +1 extra node/tick per level, max 3 — engine `automationTick`),
each a pure passive via `perkLevel`. Built by an in-session agent team
(furnace slice, perks slice, docs, test); manager integrated on `master`.
Verified: 17-check headless run (burners 3x5, 8 perks, ascendReward +2 at
apgain L2, automationTick 3 = base 1 + autoboost 2) + a Chromium screenshot
of a forge with the left fuel rack, no draw errors.

**Stats panel (?v=36, agent team).** A "📊 Stats" top-bar button opens
`#stats-modal` (index.html) listing lifetime stats — playtime, totals
gathered/crafted, fox kills, buildings built, upgrades applied, disciples,
wisp links, recipe switches, ascensions + points, regions unlocked (N/6),
carry capacity — via `openStats`/`closeStats` in ui.js (reuses `fmtAway`
for playtime), wired in main.js, styled `.stats-box`/`.st-row` in style.css.
Cosmetic read-only overlay, no game-state/save change. Built as the
stats-panel slice of the parallel batch; verified in Chromium (13 rows,
opens/closes, no errors).

**Prestige perks + awakening reward (?v=35, agent team).** Two new Ascension
Shrine perks in `DATA.PERKS`: **Frugal Frontier** (region unlock costs
−20%/level, max 3 — wired in engine `areaUnlockCost`) and **Ember Heart**
(burners consume fuel 15% slower/level, max 4 — wired in `burnFuel`), each a
pure passive multiplier via `perkLevel` (no `buyPerk` change). **Awakening
blessing:** fully awakening the dragon (`GS.won`) now grants a permanent
~11% global speed boost, folded into `prestigeFactor()` (`×0.9` when won),
so finishing the dragon story finally pays off mechanically. Built as an
agent team (data+engine slice, test slice); verified headless (6 perks;
Frugal drops mine unlock 8→6 wood at lvl 2; prestigeFactor ×0.9 on win) and
`node --check`. (Stats panel lands next at ?v=36.)

**Spirit Grove region — built by an in-session agent team (?v=34).** A new
pannable region at world grid **(0,1)** (bottom-left, below the Farm),
unlocked for **12 rice + 12 wood**. It **completes the 3x2 map** — the last
void corner is filled, so every grid cell is now a real region. A gathering
region with **no new items**: herb bushes spawn **Spirit Herb** and bamboo
stalks spawn **Bamboo**, two previously-scarce mid-game inputs (Spirit Herb
feeds Robe/Qi Elixir/Vitality/Verdant Pill; Bamboo feeds Paper and is a
fuel), relieving two supply bottlenecks. Region logic stays generic over
`WORLD.regions`/`AREAS`, so **no engine change**. Files: `js/data.js`
(AREAS.grove with herbbush/bamboostalk spawners, TIER_SPRITES, WORLD region
+ unlockCost + wiring), `js/state.js` (`world.unlocked.grove:false` default),
`js/ui.js` (grove region colour) + a new **`.edge-arrow.down-left`** unlock-
button position in `style.css`, `?v=34`. **Workflow:** manager + three
worktree subagents (data/state, docs, test); manager owned the coupled
ui/css integration and merged on the default branch. Verified headless
(grove loads with 160 herbbush+bamboostalk nodes, unlock for the scaled
cost, spirit_herb/bamboo drops) + a Chromium render of the (0,1) region and
its bottom-left unlock button, no draw errors.

**Volcano region — built by an in-session agent team (?v=33).** A new
pannable region at world grid **(2,1)** (bottom-right, below the Mine),
unlocked for **3 iron bars** (`unlockSide: "up"`, the free edge; it fills a
previously-void corner). Adds one new item **Obsidian** (from obsidian
rocks, a `break` node) and re-uses **Firestone** (premium fuel, from fire
veins); a new **Kiln** recipe **Obsidian Glass** (1 obsidian → 2 glass —
obsidian being volcanic glass) sinks obsidian into the existing glass chain.
Region logic is fully generic over `WORLD.regions`/`AREAS`, so **no engine
change** was needed. Files: `js/data.js` (obsidian item + icon, AREAS.volcano
with obsidian/firevein spawners, TIER_SPRITES, WORLD region/unlockSide/
unlockCost, Kiln recipe), `js/state.js` (`world.unlocked.volcano:false`
default — loadState already merges saved unlocked over defaults), `js/ui.js`
(volcano region colour), `?v=33`. **Workflow:** ran as a manager + three
worktree-isolated subagents (data/state slice, docs slice, test slice); the
manager owned the coupled `ui.js`/integration and merged serially. Verified
headless (region loads with 100 obsidian+firevein nodes, unlock for the
scaled iron-bar cost, Kiln obsidian→glass batch) and a Chromium render of
the (2,1) region, no draw errors.

**Bigger buildings + furnace fuel-area layout + hover names (?v=32).**
`GRID.building` default footprint is now **3x3** (was 3x2 — "a bit larger,
at least 3 tall"); **burners (Forge/Kiln/Pill Furnace/Star Anvil) are 3x4**
(explicit `size` in data.js) with the **top 3x2 as the fuel area** (now
`FUEL_SLOTS=6`, a 3-col×2-row grid, oldest burns right-to-left, "No fuel"
above) and the crafting face in the **bottom 3x2** (the fuel area doesn't
shift the centre). `drawConverterFace`/`drawFuelRack` now take an explicit
content rect. The **recipe picker floats above the clicked building**
(`positionRecipeMenu`, clamped on screen; CSS no longer pins it bottom-
centre). **Hovering any built building shows its name** as plain text at
the bottom-centre (`#hover-name`, `updateHoverName` in onMouseMove).
Placement/starter-network already validate against each building's real
size, so the resize caused **zero footprint overlaps** (verified: 18
starter buildings, 0 overlaps). Test hooks `UI._lookAt`, `UI._openRecipe`.
Verified in Chromium (sizes, no overlaps, forge 3x4 screenshot with fuel
top + crafting bottom, recipe menu above building, hover "Forge"), no
draw errors.

**Visible fuel racks + converter crafting face (?v=31).** Burners
(Forge/Kiln/Star Anvil/Pill Furnace) replaced their invisible scalar
`b.fuel` gauge with a **visible FIFO queue** `b.fuelQ=[{item,rem,total}]`:
fuel is ADDED at the front, BURNED from the back (oldest finishes first),
max `DATA.FUEL_SLOTS` (4). Engine helpers `fuelQueue/fuelTotal/fuelSpace/
addFuelItem/burnFuel`; a batch needs `fuelTotal>=cost` to start and burns
1:1 with elapsed time while running (gameTick, silent — animated on-screen
via animActive). Fuel is NOT a recipe input (iron bars need no wood). Wisp
delivery + Furnace Spirit stoking now push into the queue. On the map: a
**2x2 fuel rack** drawn left of the building (`drawFuelRack`) — items in
slots, the back one burning right-to-left (clip on rem/total), "No fuel"
label centred above when empty. The building face (`drawConverterFace`) is
now centred (fuel rack excluded): a row of input icons (top-left = in
stock, bottom-right = need/craft), a result icon (bottom-right =
`craftsPossible`, fuel ignored), and a 1-cell centred progress bar. Also:
the Altar shows "Select an upgrade" when no job is picked. Saves migrate
(legacy scalar fuel + stray stocked wood → queue, biggest-unit packing).
Verified: 20 engine checks + screenshots (rack, face have/need/crafts,
altar text), no draw errors.

**Recipe picker redesign (?v=30).** The converter recipe menu
(`#recipe-menu`, `renderRecipeMenu` in ui.js) is now a floating popup: a
"Recipes" title over a 3-column icon grid (one `.rm-cell` per existing
recipe, showing just the output icon; active one gold-outlined). Hovering a
cell opens `#recipe-info` bottom-right — output icon, recipe name, then each
required input as a row: item icon with a count badge on its lower-right +
the item name. Click selects (unchanged `setRecipe`). CSS split so
`#link-menu`/`#roster-menu` keep the old docked-bar look. Test hook
`UI._openRecipe(area,id)`. Verified in Chromium (grid cells, hover detail,
badges "2"/"1", bottom-right placement, select+close) + screenshot.

**Deeper prestige — Ascension Shrine (?v=29).** Ascending now grants
**Ascension Points** (AP: `1 + unlocked regions beyond Center`, so 1–4/run)
on top of the +8% speed, spent in a perk shop on **permanent** upgrades
that persist across every reset. Perks (`DATA.PERKS`, wired in engine):
Eternal Haste (−5%/lvl to all durations, via `prestigeFactor`), Master's
Hall (+1 disciple cap/lvl, via `rosterCap`), Long Slumber (+2h offline
window/lvl, via `offlineCapMs`), Fleet Hands (+5 hand cap/lvl, live + on
fresh runs). State: `GS.ascendPoints` + `GS.perks{id:lvl}`, saved and
migrated (levels clamped to config max, unknown ids dropped, carried
through `ascend()`). UI: a gold ☯ top-bar pill (shows AP, glows when
something's affordable) opens `#perk-modal` (`UI.openPerkShop`). Engine
helpers: `perkLevel/perkCost/buyPerk/ascendReward/offlineCapMs`. Verified
headless (19 checks: buy/effects/max/ascension-carry/save-migrate) AND in
Chromium (pill → open → buy → live update → close, no errors).

**Feedback juice (?v=28).** Purely-cosmetic FX layer in `ui.js` (no game
state, nothing saved): floating **"+N" numbers** with the item icon and
**spark bursts**. Kept in world px; `drawFX` renders on top in
`drawWorldInner` and culls the dead; `fxActive()` is folded into
`animActive()` so paints keep coming while FX are in flight. Sources:
`engine.dropGround` now calls an optional `window.onGroundDrop(area,item,
qty,x,y)` hook → the UI floats a "+N" for any on-screen drop (harvest
yields, converter output, disciple essence, fox loot), with a gold sparkle
for prized loot (essence/scales/pills/jade); player pickups pop a green
"+N" at the cursor; harvest/attack swings throw a small spark. On-screen &
count-capped (60 floaters / 240 sparks). The hook is DETACHED during
offline catch-up so a fast-forward never queues a blizzard. Verified in
Chromium: real harvest-hold makes `needsLiveRepaint` true with lit pixels,
no draw/page errors.

**Offline / idle catch-up (?v=27).** The tab standing closed no longer
wastes time. `state.js` stamps `lastSeen` into every save; on load
`engine.runOfflineCatchup()` replays the passive economy for the gap by
overriding `Date.now` to a virtual clock and fast-forwarding the REAL
`gameTick`/`automationTick` (so generators, converters, wisps, disciples
and automation stay authoritative — no parallel math to drift). Bounded:
elapsed capped at 8h, tick count capped (~1s worst-case compute; step
widens for long absences but stays under the ≥1.5s generator intervals),
gaps <5s ignored (plain reloads). Everything offline can produce is already
cap-limited (fields, converter stock+fuel, disciple buns), so it can't run
away. A "Welcome back" modal (`#welcome-modal`, `UI.showOfflineSummary`)
lists what accrued. `Date.now` is restored in a `finally` even if a tick
throws. Verified in Chromium: 2h away → 548ms sim, clock restored, gains
shown, no errors. Migration-safe: pre-feature saves have `lastSeen: null`
→ sim skipped (no false credit).

Prior — hold actions reach max speed in **0.2s** (?v=25): the right-hold
drop/feed ramp went from "4/s for a full second, then 4→20/s over the next
second" to a straight 4→20/s over the first 0.2s; the left-hold storehouse
withdraw likewise ramps 1→5/s in 0.2s instead of 3s. One-line change in
`ui.js` startLoop, verified in real Chromium (≈19 drops/sec of holding vs
≈5 before). Also switched the team to a **feature-branch workflow** (see
CLAUDE.md rule #1 / Session setup): work lands on `feature/…` branches that
merge to `master` when done; the old `claude/idle-grounds-setup-pracof`
branch was retired.

Prior session — added `.claude/rules/personal-workflow.md` — the user's
global workflow rules, committed so cloud/remote sessions load them (a
global `~/.claude/settings.json` SessionStart hook now auto-seeds this file
into any git repo the user works in; canonical copy:
github.com/tarnos12/claude-rules). No game-code change in that commit.

Added **CLAUDE.md** (auto-loaded each session) codifying the user's
workflow rules: commit+push after every task, update HANDOFF on every
commit, bump `?v=`, keep DESIGN current, always share the localhost link,
sync git first, verify headless before committing — plus the architecture
conventions. No game-code change in that commit.

Closed the last dead-end item loops (?v=24): Tools/Glass/Rope became
advanced build MATERIALS gating T3 buildings behind their T2 producers
(Pill Furnace needs tools, Star Anvil tools+glass, Talisman Atelier glass,
Meditation Pavilion rope); Vitality Pill became a combat consumable —
right-click it in the front hand slot to quaff (never dropped) for Martial
Vigor (+2 attack, 2x fox/boar loot, 45s), shown as a live bottom-bar badge
beside any dragon blessing. Now every produced item has a consumer.

Recent history (newest first): trade-good sinks (v24) · disciple capacity
tree node + Spirit Wine as premium disciple food (v23) · Disciples /
Meditation Pavilion cultivating essence from robes+buns (v22) · phase-5
finale: Talisman Atelier, Dragon Scales, Dragon Shrine, Ascension Gate +
prestige (v21) · phase-4: Pill Furnace, 4 dragon pills & blessings, Star
Anvil, firestone, Spirit Boar (v20) · phase-3: 6 producers + fuel system +
Furnace Spirit + Wisp Haste + rice/koi/spring-water renames (v19) · recipe
picker (v16), link editor (v17), tutorial quests + Help modal (v18).

Testing note: headless preview tabs throttle setInterval to ~1/s — drive
E.gameTick() manually to fast-forward, and exercise real click paths via
synthetic MouseEvents on #world-viewport after a mousemove (the handlers
gate on cursor.over). All features above were verified this way (engine
math + real UI clicks) with zero console/draw errors.
