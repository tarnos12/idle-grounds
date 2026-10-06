# HANDOFF — Idle Grounds (Unity port)

Workflow rules (master, commit+push per task, update this file in every
commit, git fetch first) live in `CLAUDE.md` — not repeated here.

## ▶ NEXT SESSION: start here

**State:** the Unity port is fully playable (all sim + view milestones and
parity audits done). On top of it the post-port design is implemented:
floating **Islands** with sky/parallax, one-way paired **Spirit Bridges**
(ADR 0003), **no offline progress** (ADR 0002; Tireless Wisps perk replaces
Long Slumber). Terms: see `CONTEXT.md` (Island, not region). Sim.Tick and view
syncs are near-zero GC. Real art is partly integrated; placeholders cover the
rest.

**Run it:** open `Assets/_Project/Scenes/Game.unity` (build index 0), press
Play. Hold LMB on nodes to harvest / on ground to vacuum, RMB drop, Q/E rotate,
WASD pan, wheel zoom (max out 6), B build menu, click Altar/Dragon/lanterns for
their panels.

**Current focus: the art integration loop** (poll Drive, pull, integrate,
review, give feedback in ART-SPEC Delivery log).
Next candidates:
1. Poll Drive `incoming/` (30 min x1h, hourly x2h, then 2-hourly; restart after
   any ART-SPEC update), run pull + integrate, review in Play, log in
   `docs/art/ART-SPEC.md` "Delivery log" and mirror the spec to Drive.
2. Art still requested (see spec section 3-4): the other 6 biome tilesets
   (blob 47f + fill + cliff, same process as Center), `island_underside_fill_32x32_3f`,
   `fix_spirittree_sparkle`, `enemy_boar_*`, remaining P2 buildings/nodes/items,
   UI icons/FX. Center path/plaza tiles are stored but not painted in game yet
   (they read plain; needs a pass).
3. Deferred: remaining low UI parity items (`docs/port/parity-audit-ui.md`),
   PlayMode smoke tests, Spirit Bridge / Island art once delivered.

## How things work

**Layout.** Unity 6000.3.12f1, URP 2D, project at repo root. Code in
`Assets/_Project/Scripts/`, five asmdefs: `IdleGrounds.Sim` (pure C#, no
UnityEngine), `IdleGrounds.Game` (views, UI, input), `IdleGrounds.Editor`
(menus/builders), `IdleGrounds.Sim.Tests`, `IdleGrounds.Game.Tests`. Data in
`Assets/_Project/Data/`, art in `Assets/_Project/Art/`, audio in
`Assets/_Project/Audio/SFX/`. Docs: `docs/adr/0001..0003`, `docs/port/`
(`PLAN.md`, `engine-systems.md`, `data-catalog.md`, `ui-input-render.md`,
parity audits), `docs/art/ART-SPEC.md`. `old-game/` is the frozen JS reference
(`node old-game/server.js` -> http://localhost:5174).

**Sim vs Game.** `Sim` owns all state and rules (ticked by `GameRunner`;
deterministic clock/RNG, systems for buildings, converters, fuel, logistics,
dragon, upgrades, progression, pavilion, combat, automation, prestige, save
codec). View queries on `Sim` are non-allocating (cached buffers). `Game`
renders it: pooled Node/GroundItem/Wisp views, `*ViewSync` classes, uGUI
panels, `HandController`, `SaveService` (persistentDataPath
`idle-grounds-save.json`, 5 s autosave, corrupt -> `.bak`), `AudioService`.
Island GameObjects in the scene are the authority for island positions
(`SetIslandOffsets`).

**Data pipeline.** `node tools/data-export/export.js` ->
`Assets/_Project/Data/Source/game-data.json` -> menu
`Idle Grounds/Data/Import From JSON` (idempotent) regenerates the
ScriptableObjects + `GameDatabase.asset`. Emoji placeholder atlas:
`node tools/emoji-atlas/build.js`, then `Idle Grounds/Art/Slice Emoji Atlas`
and `Idle Grounds/Art/Build TMP Emoji Sprite Asset`. SFX: `node tools/sfx/render.js`.

**Scene + rebuild menus (all under `Idle Grounds/`).**
- `Scene/Build Game Scene` (skeleton) and `Scene/Rebuild All (World + M2-M8 + Islands)` (everything).
- Pieces: `Scene/Install Core Loop (M2)`, `Install Buildings (M3)`,
  `Install Logistics (M4)`, `Install Progression+Combat (M5+M6)`,
  `Install Meta (M7+M8)`, `Install Islands, Sky + Bridges (ADR 0003)`.
- Prefabs: `Prefabs/Build Core Prefabs`, `Build Building Prefabs (M3)`,
  `Build Logistics Prefabs (M4)`, `Build Progression + Combat Prefabs (M5+M6)`,
  `Build Meta Prefabs (M7+M8)`, `Build Island + Bridge Prefabs (ADR 0003)`.
- `Project/Ensure Sorting Layers`.

**Islands / coasts / undersides (`World/`).** `Build Islands` creates the
Island objects. Each Island has a `Coast` tilemap (irregular 8-30 cell margin
with lobes/bays/islets around the 93x93 playable square), authored once and
never overwritten: `Generate Missing Island Coasts`, `Regenerate Island
Coasts (overwrites)`, `Regenerate Selected Island Coast (overwrites)`.
`Rebuild Island Rim + Underside + Veil (from Coast)` derives rim, tapering
underside body (35% of width, cap 40, delivered rock/roots hung at native
scale) and veil. `Respace Islands (from coasts)` keeps >=14 cells of sky and
leaves room for underside depth. Placeholder art regen:
`Art/Regenerate Placeholder Island Art`, `Art/Regenerate Placeholder Blob Sheets`.

**Art pipeline.** Spec `docs/art/ART-SPEC.md` is mirrored to Drive
`G:\Mój dysk\AI files\Idle Grounds Art\` (with `incoming/<category>/` for
deliveries and `feedback/`). Pixel art, 32 px/cell, PPU 32. Flow:
`node tools/art-intake/pull.js` (copies/unzips new files into
`Assets/_Project/Art/Incoming/`, log `intake-log.json`; newest zip wins, never
replaces with an older delivery; `*preview*` files go to `_notes`) ->
menu `Idle Grounds/Art/Integrate Incoming Art` (import rules + wiring by key).
Conventions: blob tilesets use the artist bit order N,E,S,W,NE,SE,SW,NW
ascending (`blob_mask_reference.json`; spec 3.0); real building art renders
full-size as the body (`BuildingAsset.hasRealArt`) with info strips/pills
outside the footprint (converter strips above the art); real nodes/fixtures
render native-size, bottom-aligned (2f = intact/hit, Nf = loop); animated
building variants (working/glow) supported; `GameDatabase.fx` / `enemyAnims`
registries; sky moon is a fixed-to-view parallax slot. Rejected deliveries
are kept in `docs/art/rejected/` with feedback sheets in Drive `feedback/`;
review in Play mode before accepting, reject regressions and keep the last good
version pinned.
Integrated so far: all P1 items; buildings gathering stone, wisp lantern,
warding seal, workbench, storehouse, Altar, kiln, dragon (sleeping + awake);
Spirit Tree, quarry, bush, decorative trees; wisp FX; fox animations; Center
tileset complete (blob v5, fill v2.1, cliff, underside rock/roots); sky
complete (gradient, moon, peaks, 3 cloud layers).

**MCP.** Editor access via `unityMCP` (HTTP 127.0.0.1:8080/mcp); the server is
shared with another project, so first `set_active_instance`
`idle-grounds@d0cd7413e288ec3f` and never touch the other instance. Always save
the active scene after MCP editor work.

**Tests.** 145 EditMode tests (141 Sim, 4 Game), incl. `SimTickAllocTests`,
0-alloc query tests, starter-network golden test. Run via MCP `run_tests`
(EditMode) or Test Runner.

## Known issues / gotchas

- The TMP fallback font asset gets dirtied when entering Play — revert it
  before committing.
- Game View toolbar generates ~190 GC allocs/frame in the editor (editor-only;
  game code ~1 alloc/frame, Sim.Tick ~0.9/tick).
- ArtIntake reports animated art as "changed" on every run (harmless).
- First `execute_code` after entering Play often fails — warm up with a
  trivial call first.
- Blob v4 regressed to the teal v1 look and was rejected; v3 stayed pinned until
  v5. Sky clouds v1 and Center blob v1 also rejected (see `docs/art/rejected/`).
- JS quirk intentionally not ported: wisp-delivered firestone now goes to stock
  when the recipe takes it.
- Save id `slumber` kept for the Tireless Wisps perk (save compatibility).
- Center path/plaza tiles are delivered but not painted in game.
- Placeholder art under `Art/Islands|Sky|Bridges` is replaced by delivered art
  with the same names/keys.

## Last session summary (2026-10-06)

- Art: sky peaks (sky complete), awake dragon, decorative tree ring (hashed
  variants + flip) delivered and integrated.
- Islands: native-scale underside art over a solid tapering body; rows
  respaced for underside depth; path/plaza tiles stored.
- UX: generic Tooltip/TooltipTrigger on HUD, converter strips above art, quest
  panel fades over steles, boundary props use delivered art.
- Perf: non-alloc Sim view queries, views refresh every frame (throttles
  dropped), Sim.Tick and view syncs near-zero GC; `SimTickAllocTests`.
- (2026-10-05, for context) Center tileset finished, sky gradient/clouds/moon,
  sleeping dragon as animated building, rejected blob v4 and cloud v1.

## Earlier history

- 2026-10-04 kickoff: web prototype moved to `old-game/`, Unity project added,
  specs and ADR 0001 written (`docs/port/PLAN.md` milestones M0-M9).
- M1-M2: sim core, ScriptableObject data + JSON importer, core loop playable.
- M3-M6: buildings/converters, logistics (wisps, lanterns), dragon, upgrades,
  quests, combat, automation, pavilion; sim and view each done.
- M7-M8: prestige, save codec/service, audio, FX, help/stats; fully playable.
- M9: sim + UI parity audits and fixes (`docs/port/parity-audit-*.md`).
- Design pass (grilling): `CONTEXT.md`, ADR 0002 (no offline), ADR 0003
  (floating Islands + Spirit Bridges); irregular coasts, sky and parallax.
- Art pipeline v1: ART-SPEC, Drive hand-off, `tools/art-intake`; Center
  island, items, buildings, fixtures, creatures, sky delivered 2026-10-04..06.
- Perf pass: non-allocating queries and views (2026-10-06).
