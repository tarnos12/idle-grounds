# HANDOFF — Idle Grounds (Unity port)

Workflow rules (master, commit+push per task, update this file in every
commit, git fetch first) live in `CLAUDE.md` — not repeated here.

## ▶ NEXT SESSION: start here

**Now (2026-10-22):** Text font setting built on branch `bg/font` (Menu > Settings > Text font: 6 options, default Pixelify titles + Jersey 10, live, PlayerPrefs `ig_text_font`; orchestrator merges, then UI QA re-checks the batch B screens per font); PPU 64 pilot request (Paper Mill, Kiln, Workbench) going to the artist via ART-SPEC 3.9. ADR 0007 Phase 2 (Shrine) still needs owner go-ahead; 74 validated Phase 2 art files wait for it.

**Art intake 2026-10-21 (branch bg/art38):** 37 Drive zips reviewed (ART-SPEC Delivery log row 2026-10-21). In Play: sidewall v3 x7 and Fishing macro v4 accepted (CoastCurves side face 11 -> 18 px; Fishing macro now softened like the other biomes); `cliffwall_round` x7 rejected (flat stripes), straight walls stay pinned. 69 more files rejected on file review (NPC idles, sky path pieces, 19 working loops, 4 floating-base buildings, new-island blob/fill/wall/side/macro, lotus water, 6 portraits) and moved to `docs/art/rejected/`; 74 validated files sit unwired in Art/Incoming until ADR 0007 Phase 2. Next: artist redo; ArtIntake should prune stale GameDatabase entries when art is removed.
**Visual pass batch B (2026-10-21, merged):** building panels anchored on their building (BuildingPanelAnchor), Pixelify Sans for all text (UiFont runtime swap + static TMP asset, menu Idle Grounds/UI/Build Pixel Font), icon-only desktop bar + one shared Help/Settings menu (GameMenuView, Esc opens it), crisp zoom stops 15/20/30/60 + 120-cell map stop, calmer ground tints (DecorStyle). Next: orchestrator merges bg/visb, UI QA review, art for ui_sound/ui_motion/ui_menu/ui_help/ui_reset and the jade primary button.

**Android (2026-10-13, branch bg/android):** touch controls + phone UI + `BuildScript.BuildAndroid` landed (see `docs/guides/touch-controls.md`).
Next: the owner sideloads `Builds/Android/IdleGrounds.apk` and runs the manual checklist (Back key mapping, safe area on a real notch, feel of hold times); then tune constants in TouchInputController / MobileUi.

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

**Builds:** menu `Idle Grounds/Build/Windows | WebGL | Windows + WebGL`
(Editor/BuildScript.cs) → `Builds/Windows/IdleGrounds.exe`, `Builds/WebGL`
(gzip + decompression fallback). Serve WebGL with `node tools/serve-webgl.js`
→ http://localhost:5180 (launch config `webgl-build`). Keep productName
"idle grounds": it decides the save folder.

**Current focus: finish the 3/4 island look (ADR 0005).** §3.6 art is in
game (2026-10-11): south cliff walls (repaired copies in Art/Islands/Dressing),
edge lips, macro patches (softened), flowers, path v2, tiled cloud-sea sky
(ParallaxLayer Mode.Tile2D, menu Idle Grounds/World/Rebuild Sky (cloud sea)),
background islands. Still weak: W/E coasts read as a flat straight cut
(requested `island_<biome>_sidewall_32x32_4f`), staircase diagonals on south
coasts, heavy side-view underside pieces (`_3q` underside redo requested),
Fishing macro + sky_depth rejected. Windows/WebGL builds predate this pass.
**Unity MCP:** if the session's unityMCP tools are disconnected, drive the editor
with `node tools/mcp-call.js <tool> '<json>'` (pins idle-grounds).
Other open items: 10 keys without consumers (dragon_stir, npc_disciple_meditate,
glyph_*, island_veil_edge, island_waterfall*), emoji → real ui_* icons,
ascension-content design session (needs the user), Game.unity ~77 MB (GitHub
limit 100 MB). Art loop rules: memory art-drive-loop.md (cron jobs are
session-only, recreate on start).

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

- PlayMode smoke tests: `Assets/_Project/Scripts/Tests/PlayMode/GameSmokeTests.cs` (6 tests; they back up + restore the real save). Run via Test Runner (PlayMode).
- Watch `ProjectSettings/EditorSettings.asset` `m_EnterPlayModeOptions`: keep 0 (domain reload ON) — tooling once flipped it to 1; revert if it shows up in git status.

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

## Last session summary

- 2026-10-22: Text font setting (bg/font, owner pick font_read = setting): Jersey 10, VT323, Press Start 2P, Atkinson Hyperlegible added (OFL, Art/Fonts/<Name>/, menu Idle Grounds/UI/Build Text Font Choices: SDF + raster cut per face, fixed line heights). UiFont: Title / Body roles (UiTextRole or name/size rule), per-face size snapping (FontFace.SnapScreenPx), live switch (SetChoice -> SwapAll, Version/Changed for refits). GameMenuView: Settings page (Back arrow in the title bar, Esc returns to the menu), menu labels no longer auto-size (cells fit the widest label). Layout fixes for wide faces: Stats Flow note height, Altar tree footer two real lines, bridge status/title wrap beside buttons (UiFit.ShareRow), world pills measure in the chosen face (ViewKit.Font applies UiFont). Tests: TextFontsTests (EditMode). Open: recipe picker caption still runs under the panel edge at 1280x720 (all fonts, pre-existing).

- 2026-10-22: owner answered art_density (PPU 64 for standing objects, pilot Paper Mill/Kiln/Workbench) and font_read (Settings > Text font with all options + Press Start 2P, default Pixelify titles + Jersey 10); docs/Design/owner-picks-2026-10-22.json. Font setting + pilot request started.

- 2026-10-21: merged bg/uifix: ModalView.FitToScreen + Stats scroll body, Stats Flow / Altar tree layouts, world text floor 11 px (ViewKit.MinTextPx), BuildingPanelAnchor docks+scrolls on phone and closes when its building leaves the screen, quest panel width, Help key-caps [B]. Hub question font_read (recommended: Pixelify titles + Jersey 10 body).

- 2026-10-21: art intake (ingame-art-reviewer, bg/art38): pull.js skips SOURCE-STUDY images; 15 files accepted in Play (Islands-038 sidewalls, -039 Fishing macro), Islands-040 round walls rejected in Play, 69 files rejected on file review with repair notes, 74 validated and waiting on Phase 2. Evidence on Drive feedback/2026-10-21_*.png; ART-SPEC log row mirrored to Drive (3.9 stays repo-only).

- 2026-10-21: merged bg/artdir: 26-building audit (docs/art/reference/buildings-sheet.png, ART-SPEC 3.9 pending), Hub question art_density (pixel density), snowy Fishing brief ART-SPEC 3.8 mirrored to Drive.
- 2026-10-21 (bg/uiqa): UI QA review of batch B in docs/reviews/2026-10-21-ui-qa-batch-b.md (5 major: Pixelify 5/3/B/E digit+key confusion, phone Stats overflow, 720p Stats Flow + Altar tree collisions, sub-12 px pills/NEW badge). Fixed: desktop Menu button lost its tooltip (deferred Destroy). New shot act barTip.
- 2026-10-21 (bg/visb): visual pass batch B code. vp_panels: BuildingPanelAnchor puts the recipe picker (above first), link editor, bridge and pavilion panels beside the building art, follows the camera, clamps to the safe area above the bar, avoids the quest panel, docks top/bottom when no side fits; Stats/Help/Altar/Menu stay centred. vp_look: Pixelify Sans (OFL, Art/Fonts/PixelifySans). The font tells letters apart by 1-pixel slits (C/O, 5/8), so it must sit on whole font pixels: desktop uGUI text uses a hinted raster cut at 22 px (2 px per font pixel) drawn 1:1, point-filtered, on pixel-perfect canvases; larger sizes, phones (canvas scale ~2) and outlined/world text use SDF cuts sampled on the font grid (44 pt); every uGUI size is snapped to a multiple of 11 rendered px, min 22 (UiFont.SnappedScreenPx). Bold UI uses the regular cut (TMP fake bold and the off-grid bold cut closed the counters). UiFont swaps serialized LiberationSans texts at runtime (no scene rewrite); LiberationSans stays the fallback for arrows/ticks. TouchInputTests now start with the quest panel collapsed (with 22 px text an expanded panel reached the 640x480 test screen centre). vp_bar: desktop bar = icon-only Stats/Build/Demolish/Sound/Motion/Menu with tooltips; Help + New game moved into GameMenuView (shared with phone; MobileLayer pause menu delegates to it). vp_zoom: CameraController crisp stops (k = 4/3/2/1 px per texel + 1/2 map stop), wheel / +- keys / pinch-release snap, pixel-grid snap at rest. vp_mood: per-Island multiply ground tint (Volcano unchanged; needs art). Also fixed UiButtonSkin.Attach ignoring the variant on active objects (Primary/Close buttons built at runtime now get their art). Tests: simtests 168/168, EditMode UI 21/21, PlayMode 20/21 (Logistics_WispLaunches_ConverterCompletes fails on master too).

- 2026-10-21: owner answered the 8 visual-pass batch B questions (docs/Design/owner-picks-2026-10-21-visual.json): panels anchored on buildings, Pixelify Sans + icons everywhere, icon-only bar, map zoom step at 120 cells, calmer ground, all 26 buildings redone (too pixelated), snowy Fishing island. Batch B code + art-director brief started in bg copies.

- 2026-10-21: large art delivery found on Drive (Islands-038..050, Buildings-014..024, Creatures-006..008, Items-009, Nodes-007, UI-022..024, FX-005/006; 156 new + 15 updated files); ingame-art-reviewer agent started on intake in a bg task copy.

- 2026-10-13: Android/touch port. TouchInputController (tap / hold / drag-pan / pinch / Give mode / aim-confirm), MobileLayer (canvas scale by dpi, safe area, 48 dp hit padding, Swap/Give/Place/Cancel buttons, Back-key menu, 60 fps, landscape), UI input actions got Touchscreen bindings, tooltips work on touch, BuildScript.BuildAndroid + build.sh android, phone screenshot presets. Tests: 15 new PlayMode touch tests (class A_TouchInputTests, sorts first on purpose), 4 EditMode MobileUi tests. GameSmokeTests.Logistics_WispLaunches_ConverterCompletes fails on master too (pre-existing).

- 2026-10-13: visual pass A3 merged (world art in code): VeilArt (soft mist veil, no seams/coast leak), interim rack/bridge backings (quiet translucent rack, loud on hover/low fuel), seeded ground variety + prop jitter, cliff/underside/lip noise, rounded paths/plaza/islets. Opus builder hit the weekly limit at the end; finished by the orchestrator (rebuild + shots + gate). Batch A complete: master gate compile OK, EditMode 175/176 (1 skipped).

- 2026-10-13: visual pass A2 merged (world labels): Game/Views/WorldLabels (WorldLabel, WorldLabelSystem layout/LOD/collision, PixelSprites status shapes, PlatedText, IslandNameLabels, DecorStyle); converter strips by zoom; pixel rims/threads/rings (link threads hidden at rest: LinkLineSync.ShowAtRest); hover tag + hints; placement reasons + demolish refund preview; Gate captions. Owner-check constants: IslandNameLabels.ShowFromZoom, LinkLineSync.ShowAtRest. Merge compiled clean in the bg copy.

- 2026-10-13: visual pass A1 merged (UI): confirm dialog title + growing button, current wording (no ascension), MetaText.FmtPlaytime, Debug only in editor/dev builds, EnforceLayerOrder + ModalScrim, UiDock, recipe captions, Altar tree fit/pan/state cues, build menu paging, docks/bottom bar/quest claim/stats/help/toast polish. New menu Idle Grounds/UI/Rebuild UI Prefabs + Skins (UiRebuild.cs). EditMode 175 pass/1 skip.

- 2026-10-13: designer-team visual pass — 100-shot tour (scratchpad/tour, tools/bg/tour.sh), 4 reviews in docs/reviews/2026-10-13-*, merged plan docs/reviews/2026-10-13-visual-pass-plan.md (A 27 no-input fixes, B 9 needing answers, C 77 art files). 8 owner questions in the Hub. Batch A split into 3 builders in separate background task copies.

- 2026-10-12: coast redo #4 merged from bg/coast-curves (first background-copy feature): curved coastlines — wide-blurred seed outline + gentle swell, one straight segment per crossed cell (CoastShapeTile, Editor/CoastCurves.cs), walls laid by arc length with taper, curved lips/side faces, per-pixel veil masks. EditMode 175/175 in the bg copy, fast sim 167/167. Art hook: island_<biome>_cliffwall_round_32x96_4f. Sign-off asked (coast4_signoff).

- 2026-10-12: background copy — git worktree D:WORKUnityidle-grounds-bg (branch bg/main) + headless Unity via tools/bg/* (sync, compile, test, menu, shot, unity, build, publish-back, task). Editor entry points in Editor/Batch.cs; SaveService honours IDLEGROUNDS_SAVE_DIR in the Editor so background Play/tests never touch the owner's save. Verified: compile 53 s, EditMode 175/175 in 6 min, menu 26 s, Play screenshots 47 s; owner's editor and save untouched. Flow: CLAUDE.md 'Background copy'. Known: PlayMode GameSmokeTests.Logistics_WispLaunches_ConverterCompletes fails (pre-existing timing issue, plan task).

- 2026-10-12: coast redo #3 — corner-sampled coast model with 45° diagonal cells (CoastDiag_<biome> tiles; runtime CoastRuleTile treats them as land), corner clean-up (no square corners/spikes), rounded ≥7×7 islets; diagonal south walls (rise/fall, sheared from repaired frames), diagonal ground edges + north lips composed from delivered art; hooks for island_<biome>_ground_diag / cliffwall_diag when delivered. Owner sign-off asked (coast3_signoff).

- 2026-10-12: agent team — 11 role agents in .claude/agents (world, balance, onboarding, ui-ux, ui-qa, art-director, ingame-art-reviewer, writer, playtester, performance, code-health) with guides in docs/guides (README = team table + task-specialist briefs); CLAUDE.md 'Agent team' section. Agents have no Unity-MCP/Artifact tools; they work in the background copy (M6 task, not set up yet).

- 2026-10-12 Phase 1 (ADR 0007): ascension/AP/perks/vows/offerings/run reset removed; Heavenly Gate (Celestial, dragon stage 4) ends the game, free play continues (`Simulation.AscendToImmortality`). Perk effects live behind `Simulation.Bonus(key)` (empty on a fresh game) for Phase 3 requests. SaveCodec schema 2 + MigrateFromV1 (+SaveMigrationTests). Bot: 2:12:27 human pace to the ending. Tests: .NET 167 fast / 169 all, Unity EditMode 175/175, PlayMode 5/6 (Logistics_WispLaunches_ConverterCompletes fails before and after — timing). User save migrated to v2; v1 backup kept as idle-grounds-save.v1-backup-2026-10-12.json in the save folder. Next: Phase 2 (Sky-Bridge Shrine).

- 2026-10-12: coast redo #2 — outline repainted from coastSeed with a fine 2–3 cell seeded lattice (organic, no 1-cell stairs, rounded islets); per-joint cliff walls (Run/End/Tuck via WallSide/Joint; composed joint sprites dress_cliffwall_<biome>_joints.png), inward side-face feet, 6 px wall tuck, shadow offset 6 px. Fast sim tests 168/168. Owner sign-off asked in the Hub (coast_signoff).

- 2026-10-12: Hub upgraded to the hub-kit conventions: sources in docs/Design/ (plan.json with milestone status/goal/exit, questions.json, question-images/, owner-picks, islands.json), plan tools in tools/plan/ (set-task, build-plan → PLAN.md + Temp/plan/Idle-Grounds-Plan.pdf), new Islands tab (11-island map, unlock order, tributes, NPC requests, #islands/<id>).

- 2026-10-12: .NET sim test runner (tools/simtests, dotnet test over linked Sim + Tests/Sim): fast 168 tests ~30 s (`tools/simtests/run.sh`), `--all` adds 3 Slow playthroughs (171, ~70 s); UnityOnly alloc tests stay in Unity. Full Unity EditMode/PlayMode only at milestones (CLAUDE.md Testing).

- 2026-10-12: Game.unity split — each Island_<key> generated hierarchy is a prefab (Assets/_Project/Prefabs/Islands/Generated, 10–12 MB each); scene 78 MB → 0.4 MB. Builders wrap edits in `using (IslandPrefabs.Edit())`; menu Idle Grounds/World/Save Islands To Prefabs. Coast smoothing (SmoothCoast + 'Re-cut Island Coasts From Seed') is applied but came out boxy; owner asked for a redo (natural coastline, tiles must connect) — next task. Tests 177/177.

- 2026-10-12 art: vertical sidewalls + variants (no rotation; seeded variants), _3q undersides under the south walls (WallTuck 2.2, roots speck masked), fishing macro v3 enabled (needs repair), sky background islands confirmed. Tests 177/177; Game.unity 78.4 MB. Sidewall width/end frames and the fishing macro sent back to the artist.

- 2026-10-12: ADR 0007 accepted (island requesters replace ascension; fixed order Mine→Fishing→Farm→Grove→Volcano→Lotus Marsh→Thunder Crag→Azure Beast Ravine→Tribulation Peak→Celestial; Shrine tributes; 11 NPCs × 3 requests; Heavenly Gate ending; vows/perks/AP removed). ART-SPEC v4 §3.7 requests 144 files (P1 shrine + NPCs). Implementation = 6 phases in the ADR; phase 1 starts after the art integration agent finishes. CONTEXT.md still needs the new terms (Shrine, Tribute, Requester, Heavenly Gate).

- 2026-10-12: ADR 0007 island requesters drafted (proposed): Sky-Bridge Shrine on the Center's north edge takes per-island Tributes (5 items × 5–20) to raise bridges; 13 NPCs × 3 optional requests unlock island buildings/utilities (old perk effects); Heavenly Gate on Celestial = ending; ascension/AP/perks/vows/prestige removed with a save migration. 7 owner questions in the Hub.

- 2026-10-12 owner: REMOVE ascension (not fun). Replace it with per-island requester buildings/NPCs that take item requests (picked from a menu) and unlock recipes. ADR 0006 rejected; ADR 0007 (island requesters) is being drafted; the ascension questions are withdrawn from the Hub.

- 2026-10-12: ADR 0006 ascension content drafted (proposed): 4 realm tiers, each adds an island (Lotus Marsh, Thunder Crag, Azure Beast Ravine, Tribulation Peak), buildings, higher recipes, Gate items and weighted vows. 5 decisions posted in the Hub Questions tab (asc_tiers, asc_island_themes, asc_island_mode, asc_vow_style, asc_postgame).

- 2026-10-12 owner picks (docs/Design/owner-picks-2026-10-12.json): ascension unlocks = new islands + new buildings/higher recipe tiers + harder vows; smooth coasts now; move generated island dressing out of Game.unity (into repo assets, still on GitHub). Next: ADR 0006 ascension design (in progress), coast smoothing + scene split once Unity responds.

- 2026-10-12: Project Hub (CLAUDE.md section): docs/Design/plan.json + questions.json + islands.json, tools/hub/build-hub.cjs + template, tools/plan/set-task + build-plan; published and pinned at https://claude.ai/artifact/EAabdoZhE79jgBc6THmrwG. Open owner questions: ascension unlocks, smoothing coasts, scene size.

- 2026-10-12: Islands-036 underside _3q quality redo pulled and validated (good). Pending in Unity: swap IslandCoastBuilder underside pieces (side-view rock/roots/stalactite/vines/lavadrip) for the _3q versions, plus sidewalls v2, fishing macro v3, sky rebuild.

- 2026-10-12: Islands-035 underside-only redraws rejected on quality (flat triangles with flecks). The first _3q mini-islands are preserved as Art/Sky/BackgroundIslands/sky_bgisle_* (IslandsBuilder.BuildSky now reads them there) because the redraws reuse the incoming keys. Still pending in Unity (editor unresponsive): vertical sidewalls + variants, fishing macro v3, sky rebuild with the new background-island paths (code not yet compiled).

- 2026-10-12: pulled Islands-033 (fishing macro v3, looks right) and Islands-034 (vertical sidewalls + 8f variants, correct orientation). NOT integrated: Unity editor unresponsive (likely a 'scene changed on disk' dialog). Next: remove the build-time rotation in IslandDressingBuilder.EnsureSideWall, use the 8f variants for W/E faces, enable the fishing macro (drop the fishing exclusion in IslandDressingBuilder + ArtIntake), rebuild, review in Play.

- 2026-10-12: fishing macro redo (Islands-031) reviewed in game and rejected again (warm tan stains on cool shingle); fallback kept.

- 2026-10-12: W/E side faces (sidewall art rotated at build time, SideWallW/E tilemaps + shadow; edge lips moved onto the visible coast edge, CoastInset=3) and cliffwall v2 with the extended build-time repair (hole fill + per-frame brightness detrend). Sidewall redo requested (wrong orientation); cliffwall still has seam defects. Tests 177/177; Game.unity ~78 MB.

- 2026-10-11: §3.6 art integrated: 3-cell south cliff walls (seam-repaired), W/E/N grass lips, delivered macro patches + flowers, path v2, tiled cloud-sea sky + background islands. sky_depth and fishing macro rejected; sidewall art requested. Added tools/mcp-call.js (direct MCP client). Tests 177/177.

- 2026-10-11: §3.6 art pulled and inspected (files OK); _3q undersides rejected (they are grass-topped mini-islands); sky_depth pre-flagged as too busy. Integration blocked: Unity not running.

- 2026-10-09 ground dressing: IslandDressingBuilder (menu Idle Grounds/World/Rebuild Island Dressing; also called from RebuildDerived) builds a Dressing tilemap stack per island. Contents: macro grass patches (noise-placed generated decals), generator-field textures (earth base + zone_*_patch), Center plaza disc + 3 short winding warm-stone paths (recoloured path/plaza copies in Art/Islands/Dressing), biome scatter, Fishing pond (AnimatedTile), volcano lavacrack and celestial twinkle. Zone tint rectangles are hidden at rest (PlacementGuide shows them only while placing/demolishing). HandCursorView clamps the hand chip above the bottom bar. Game.unity is ~74 MB (GitHub hard limit 100 MB; watch it). Unused: island_waterfall (the art is wrong: it shows a tiny island) and island_veil_edge.

- 2026-10-09 visuals (ADR 0005, 3/4 view): undersides now hang only under south-facing coast (native-scale pieces, ~12 cells reach, camera underside limit 56→14), drop shadow per island, sky = abyss vignette (#10293a) + CloudSea (Game/Views/Sky/CloudSea.cs: 96 cloud puffs, 3 depths, pixel-snapped drift); horizon gradient, peaks, moon and cloud strips retired. ART-SPEC v3 §3.6 asks for 3/4 cliff walls, edge lips, a top-down cloud sea, distant islands and ground macro patches. Still weak: straight W/E coasts, bland ground (the ground dressing pass is next).

- 2026-10-09 builds: Windows (152 MB, runs clean) + WebGL (21 MB) via BuildScript. WebGL save fix: File.Replace is unsupported on the WebGL FS (every save after the first threw) and writes were not flushed to IndexedDB; WriteAtomic now writes directly on WebGL and calls IG_SyncFs (Plugins/WebGL/IdbSync.jslib, FS.syncfs). Verified: the intro stays dismissed after a reload. Big art batch pulled (138 wired, 27 pending).

- 2026-10-09: ArtIntake is idempotent. A second run reports 0 assignments changed and leaves git clean. Fixes: frame arrays are compared by content (SameFrames) for buildings, dragon stages and building variants; the Dragon building is no longer set twice per run (it took the bld_dragon art and was then overwritten by the dragon_sleeping strip, so it changed on every run; bld_dragon is now marked consumed).

- 2026-10-09 UI art (3): cards (build + perk), recipe cells, ingredient frames + count badges, UiBar (Sliced fill driven by anchors), pills, HP pips, UiCheckbox, Scrollbars on all ScrollRects, GameCursor (2x hardware cursor: open/closed/target, system arrow over UI). New editor UiArtRoles.AttachAll (run from Apply UI Skin Roles and CoreLoopBuilder.Save). Scrim art rejected (dither grid).

- 2026-10-08 UI buttons: UiButtonSkin (Standard/Primary/Close, SpriteSwap, shared 2x slice multiplier with 1x floor) on 36 buttons via builders + menu Idle Grounds/UI/Apply Button Skins (Editor/UiButtonRoles.cs); toggles (Build/Demolish/Sound/Motion/Stats tab) swap toggle_on/danger art on state change. Cards/cells/pills/minimise still placeholder.

- 2026-10-08 UI art (2): bottom bar, quest frame, tooltip + hand-chip frames and scroll v2 integrated. UiSkinRoles has 20 roles (root path "" = prefab root); builders updated (Help/Welcome = scroll, QuestPanel = questpanel frame, tooltips = tooltip frame, hand chip, BottomBar).

- 2026-10-08 UI skin: GameDatabase.ui registry; ArtIntake sets 9-slice borders for every §5 key and wires leftover ui_* keys; UiSkin component (Game/UI/UiSkin.cs) applies a role sprite Sliced at 1 art px = 2 UI px and disables the placeholder Outline; menu Idle Grounds/UI/Apply UI Skin Roles (Editor/UiSkinRoles.cs) plus the builders attach roles (jade = modals/side panels, dark = tooltips/chips); FuelRackView draws ui_fuel_rack. The scroll panel art was rejected (Help/Welcome use jade until the redo arrives).

- 2026-10-08 art (5): forge, algae farm, meditation pavilion, dragon shrine, ascension gate (+ loops), Spirit Bridge unpaired/sending/receiving and unlock stele locked/partial/ready integrated. Every building and node now has real art.

- 2026-10-08 art (4): brewery, cauldron, jade carver, pill furnace, star anvil, talisman atelier (+ working loops) integrated; 21/27 buildings real.

- 2026-10-08 art (3): charcoal pit, furnace spirit, herb garden, Celestial starrock (1x1/2x2) + moonshrub integrated. All resource nodes have real art; 15/27 buildings.

- 2026-10-08 art (2): Volcano firevein + obsidian (1x1/2x2), Grove bamboostalk (1x1/2x2) + herbbush integrated; all nodes except Celestial have real art.

- 2026-10-08 art: Mine nodes (ore 1x1/2x2, iron/jade veins, intact/cracked) and Fishing koi/algae loops integrated. New convention `<key>_2x2` = big-spawner-node art (ArtIntake adds the region entry; SpriteCache.NodeArt prefers it for size >= 2).

- 2026-10-07 art (2): 23 items, node_crop/node_cotton, enemy_boar idle/move/hit and Fishing ground v3 (pale shingle) integrated and accepted; 0 unwired keys.

- 2026-10-07 art: 17 items, Mill/Loom/Paper Mill/Infusion Array (+ working loops), Spirit Tree sparkle overlay, spring + flow loop, Farm/Grove ground redo and the Celestial set integrated and accepted. Fishing ground v3 requested (it reads as open water). New ArtIntake conventions for region sprites: `<key>_flow` = full-sprite animation frames, `<key>_sparkle` = overlay strip (SpriteEntry.overlay), which NodeView draws one sorting order above at 8 fps.

- 2026-10-07: island onboarding quests (chain v3, 17 quests: Bridge the sky, Sky caravan, Wine of three shores; QuestHint next-step line; region wording -> Islands; chain-2 saves remap by quest id). NonAlloc tests now take the min over 5 windows (the GC byte counter is process-wide, so editor/MCP noise caused a one-off 12 KB flake); per-quest coverage added. 177 EditMode tests.

- 2026-10-07: ADR 0004 balance targets (first run 2-3 h, later runs same length + more content on ascension, hands early/machines win 5-10x, TEST mode off by default). Juice pass landed (Game/Juice + UiJuice: harvest squash/chips, item arcs + magnet vacuum, build bursts, kill shake + motes, ambient motes, UI press/sparkle; HUD Motion/Calm toggle = reduce motion). Balance pass 1 landed (docs/port/balance-pass-1.md): TEST mode off by default (dev toggle Idle Grounds/Dev/TEST Mode), first ascension ~2:10 human / 1:36 expert, auto/hand 0.7 early -> 7.8 mid-run, no world speed per ascension, bridges carry multiple items; 168 EditMode tests (~7.7 min; playthroughs ~2 min each). DO NOT re-run tools/data-export (it would overwrite the tuned game-data.json). island onboarding quests queued after the balance pass.

- 2026-10-07: playthrough bot (Tests/Sim/PlaythroughBot*.cs, docs/port/playthrough-report.md): game completable, first ascension ~6 min (TEST expert) … ~22 min (real balance, human pacing); balance concerns listed in the report; 161 EditMode tests.

- 2026-10-07: Stats → Flow tab (FlowLedger: produced/consumed/lost per item, run + lifetime, 5-min rates); 157 EditMode tests.

- 2026-10-07 art: underside fill + 5 biome cliffs + Mine/Volcano ground accepted; Farm/Fishing/Grove ground rejected (busy tiling, redo requested; placeholders kept); Celestial outstanding. (2026-10-06)

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
