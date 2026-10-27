# Idle Grounds: plan

Updated 2026-10-27 02:11. Source of truth: docs/Design/plan.json, edited with tools/plan/set-task.cjs. Do not edit PLAN.md by hand: edit plan.json and run `node tools/plan/build-plan.cjs`.

**45 of 69 tasks done**, 3 in progress, 3 to do, 4 blocked, 14 waiting.

## Being worked on now

- **M3** South cliff walls, side faces, grass lips: art loop: integrating deliveries as they arrive
- **M3** Designer team visual pass: find and fix what makes the game look off: Batch A done; waiting for your answers (batch B) and sending the art request (batch C)
- **M7** Owner tests APK round 2: APK round 2 on Drive

| # | Milestone | Gate | Status | Tasks done |
|---|---|---|---|---|
| M1 | Unity port of the browser game |  | Done | 2 / 2 |
| M2 | Balance and onboarding |  | Done | 2 / 3 |
| M2b | Island requesters replace ascension (ADR 0007, accepted) |  | Waiting | 2 / 8 |
| M3 | 3/4 world look (ADR 0005) |  | In progress | 30 / 42 |
| M4 | Art coverage |  | Later | 2 / 4 |
| M5 | Builds and tech health |  | Waiting | 2 / 3 |
| M6 | Agent team and background copy |  | In progress | 2 / 2 |
| M7 | Android build |  | In progress | 3 / 5 |

**Task status:** Done: built and checked. In progress: someone (an agent) is working on it now; the "now" line says who. Next: the next thing to start. Later: planned, not started. To do: planned and can start. Blocked: cannot start; needs art, an owner answer or another task (the reason is shown). Waiting: could start, but not a good time yet (the reason is shown).

**Owners:** Claude: Builds and checks the work. Mariusz (owner): Decides; answers the Questions tab. Artist: ChatGPT via the shared Drive folder: delivers art from the art specification.

## M1: Unity port of the browser game. Done

Bring the whole browser prototype into Unity: every system and screen, with the world rebuilt as floating Islands joined by one-way Spirit Bridges and no offline progress.

**Exit:** The Unity game plays like the browser version (parity audits passed) and the Island and Bridge rules of ADR 0002 and 0003 are in.

| Status | Task | Detail | Owner |
|---|---|---|---|
| Done | Port every system and screen from the HTML game | The full game runs in Unity with scenes, prefabs and data assets; parity audits passed. | Claude |
| Done | Floating islands, Spirit Bridges, no offline progress | Regions became floating Islands linked by one-way bridges (ADR 0002, 0003). | Claude |

## M2: Balance and onboarding. Done

Make the first run take about 2 to 3 hours, with machines overtaking hand work in the mid game, and teach the island and bridge ideas through quests.

**Exit:** The playthrough bot confirms the pacing (ADR 0004) and the quests walk a new player through bridges, caravans and cross-island trade.

| Status | Task | Detail | Owner |
|---|---|---|---|
| Done | Real balance: 2-3 h first run, machines beat hands mid-game | TEST mode off by default; playthrough bot checks the pacing (ADR 0004). | Claude |
| Done | Island onboarding quests | Quests that teach bridges, caravans and cross-island trade, with next-step hints. | Claude |
| To do | Rebalance after the empty start | With no starter buildings, machines out-produce hands only 3.8x mid-run (target 5-10x, ADR 0004); ending at 2:33 is still in the 2-3 h band. | Claude |

## M2b: Island requesters replace ascension (ADR 0007, accepted). Waiting

Replace ascension with optional Island requesters: eleven Islands unlocked in a fixed order by tributes at the Sky-Bridge Shrine, one NPC per Island with three optional requests each, and the Heavenly Gate as the ending (ADR 0007).

**Exit:** The game has 11 Islands, the Shrine, 11 NPCs with 33 requests, no ascension, vows or perks, and the playthrough bot finishes in about 3 hours both with and without taking requests.

| Status | Task | Detail | Owner |
|---|---|---|---|
| Done | Island requesters design (ADR 0007) | Accepted 2026-10-12: 11 islands in a fixed order unlocked by tributes at the north Sky-Bridge Shrine; 11 NPCs (one per island) with 3 optional requests each; Heavenly Gate ending. | Claude |
| Done | Phase 1: remove ascension, perks, vows, offerings; Heavenly Gate ending; save migration | Done 2026-10-12: ascension, Ascension Points, perks, vows, offerings and the run reset removed; Heavenly Gate on Celestial is the ending (free play continues); old saves migrate (perks become bonuses where a requester replaces them). Playthrough bot: 2:12 human pace to the ending. | Claude |
| Waiting | Phase 2: Sky-Bridge Shrine, fixed unlock order, Celestial moves north, Sky Paths | Replaces the unlock steles; quests retargeted to the new order. | Claude |
| Waiting | Phase 3: requester NPCs on the 7 existing islands (21 requests) | Request panel, right-click feeding, rewards: new buildings and automation utilities. | Claude |
| Waiting | Phase 4: 4 new islands (Lotus Marsh, Thunder Crag, Azure Beast Ravine, Tribulation Peak) + their NPCs | Nodes, Azure Wolf, 9 buildings, 12 items, 4 NPCs; placeholder art until delivered. | Claude |
| Waiting | Phase 5: balance to a ~3 h ending | Playthrough bot: greedy run 2:30-3:30, no-requests run under 4:15. | Claude |
| Waiting | Art for the shrine, NPCs and 4 new islands (ART-SPEC §3.7, 144 files) | P1 shrine + 7 NPC sprites first, then the new island tilesets, buildings and items. | Artist |
| To do | Fix the flaky PlayMode test Logistics_WispLaunches_ConverterCompletes | The first converter batch takes 20.5 s of game time but the test waits only 18 s; it fails before and after phase 1. | Claude |

## M3: 3/4 world look (ADR 0005). In progress

Give every Island the agreed 3/4 look: cliff walls, side faces, hanging undersides and natural coastlines (ADR 0005).

**Exit:** All Islands show the new walls, undersides and smooth coasts without seams or stripes, and the owner has seen the result.

| Status | Task | Detail | Owner |
|---|---|---|---|
| In progress | South cliff walls, side faces, grass lips **(NOW)** | Islands show a 3-tile cliff on the south edge and a narrow earth face on the west/east edges, so they read as floating rock seen from above. | Claude |
| Done | Integrate vertical side walls (v2, with variants) | Swap the rotated first version for the artist's corrected vertical side faces. | Claude |
| Done | Integrate the new hanging undersides (3/4 rock, roots, vines) | Replace the side-view rocks under the south cliffs with the redrawn 3/4 pieces. | Claude |
| Blocked | Fishing ground patches (3rd version) | Calm cool-grey shingle patches for the Fishing island; earlier two were rejected. | Claude |
| Done | Rebuild the sky with the kept background islands | Background islands now load from their own folder so new deliveries can't replace them. | Claude |
| Blocked | Cliff wall seam and stripe fix | The artist's cliff tiles still have transparent seams and a light-to-dark stripe per tile; patched in code for now. | Artist |
| Done | Smooth stepped and long-straight coasts | Signed off 2026-10-12: curved coastlines with walls, lips and side faces following the curve. | Claude |
| In progress | Designer team visual pass: find and fix what makes the game look off **(NOW)** | 4 reviews done (docs/reviews/2026-10-13-*); merged plan docs/reviews/2026-10-13-visual-pass-plan.md: Batch A 27 fixes (no input), Batch B 9 (your answers), Batch C 77 new art files. | Claude |
| Done | Visual pass batch A1: UI fixes (buttons, wording, dialogs, scrim, tree, menus) | VP-01..05, 08, 09, 18, 21-26: stale ascension wording, overflowing confirm button, playtime, Debug button, dim behind dialogs, hand chip overlap, recipe names, Altar tree fit and states, build menu, bars and docks, dialog text. | Claude |
| Done | Visual pass batch A2: world labels and overlays | VP-06, 07, 10, 13, 15, 19, 20: readable plated world labels, Heavenly Gate captions, converter strips by zoom, pixel-line overlays, decor vs interactable, hover tags, placement/demolish feedback. | Claude |
| Done | Visual pass batch A3: world art in code (veil, ground, cliffs, paths, rack/bridge) | VP-11, 12, 14, 16, 17: veil seams + coast leak, interim rack and bridge backings, seeded ground variety, cliff/underside/lip noise, rounded paths/plaza/islets. | Claude |
| Done | Visual pass batch B: changes that need your answers | Owner answered 2026-10-21 (owner-picks-2026-10-21-visual.json): panels anchored on the building, guidance text-only, Pixelify Sans + icons for ALL UI (consistent), icon-only bar with tooltips and Reset in a menu, zoom: 4 crisp steps + far map step at 120 cells, calmer darker ground on every island. | Claude |
| Waiting | Visual pass batch C: new art (77 files) | Ground fills, macro patches, veil mist v2, shadows, rack/bridge, icons, item drops; 45 files can start now, 32 wait on your answers. | Artist |
| Done | Review round cliff walls Islands-051 (7 biomes) | Repaired island_<biome>_cliffwall_round_32x96_4f x7 (replace the Islands-040 v1 rejected 2026-10-21): inspect, un-pin in the coast builder, rebuild island sheets, check every curved south coast in Play, accept or reject per file. | Claude |
| To do | Follow-ups from batch A | Veil shows faint vertical banding beyond the max zoom; converter strips can sit on the building above in dense clusters; real-mouse checks of hover tags and the hand chip (VP-27). | Claude |
| Waiting | Building art redo: reference sheet for all 26 buildings | Owner signed off the 64 px pilot 2026-10-24 (rollout). First batch: pilot fixes (Workbench front view + loop, Kiln idle mouth + smoke, Paper Mill base + fuller loop) and B1 Sky-Bridge Shrine; then NPCs, other buildings, nodes, trees/props, creatures. | Claude |
| Waiting | Snowy northern Fishing island | Owner pick vp_fishing: snow props and ice at the pond edge, on purpose. Needs an art request (art director) and integration once delivered. | Claude |
| Done | UI QA: batch B review | UI QA pass of visual pass batch B (anchored panels, Pixelify Sans, icon bar + menu, crisp zoom). Findings in docs/reviews/2026-10-21-ui-qa-batch-b.md. | Claude |
| Done | UI QA fixes after batch B | From docs/reviews/2026-10-21-ui-qa-batch-b.md: Stats doesn't fit phone safe area (#3), Stats Flow overlap at 720p (#4), Altar tree crowding (#5), world pill / NEW badge text under 12 px (#7), phone panels shrink below the text floor (#10), narrow quest panel at 720p (#11), off-screen anchored panels (#8). | Claude |
| Done | Pixel font readability: 5 reads as S, 3 as 8, B as 8 | Owner 2026-10-22: Menu > Settings 'Text font' (Pixelify only, Pixelify titles + Jersey 10 [default], Jersey 10 only, VT323, Press Start 2P, Smooth). | Claude |
| Done | Code animation: glows, flicker, smoke, water, sways | Owner pick anim_route = code_simple_all (2026-10-26): building and NPC motion made in code from accepted stills (Kiln fire + smoke, Shrine gate glow + lanterns, Paper Mill water + sheet, hammer glow, NPC sways/nods); moving parts (saw, wheel, arm, rings) from separate art layers. | Claude |
| Done | Art review: batch L1 layers + Sky Path FX-007 | In-game art reviewer: Layers-64-001 (Paper Mill wheel/press/front/base), Layers-64-002 (Kang arm poses, Xuan rings/front), FX-007 (32-px sky path repair). Composite == still, pivots into code-anim.json, in-game check, accept/reject per file. | Claude |
| Done | Art review: batch L1R layers + Sky Path corner FX-008 | In-game art reviewer: Layers-64-003 (Paper Mill one complete wheel, press, front, base), Layers-64-004 (Kang arm poses + base, Xuan rings + front + base), FX-008 (sky path corner). Stack test, layer-only-part and flat-fill checks, NOTES pivots into code-anim.json, in-game frame strips, accept/reject per file. | Claude |
| Done | Art review: batch L1R2 layers | In-game art reviewer: Layers-64-005 (Paper Mill new wheel on the fixed ellipse, press, front, base), Layers-64-006 (Kang base + raised arm, Xuan rings + base + front). Stack test (wheel waived), part-only, holes/flat-fill checks, numbers vs 3.9.8, in-game frame strips, accept/reject per file; a third failure goes to the owner. Result 2026-10-26: Paper Mill front, Xuan ring + front validated (held); wheel, press, Paper Mill base, Kang base, Kang raised arm, Xuan base rejected 3x, awaiting owner decision (Hub question layers_finish). | Claude |
| Done | Art review: 64 px B2 starter buildings + Dragon strips + B3 set 1 | In-game art reviewer: Buildings-64-006 (Altar, Storehouse, Gathering Stone, Wisp Lantern, Warding Seal stills), Buildings-64-007 (Infusion Array, Loom, Mill stills + base + layer), Creatures-64-005 (dragon sleeping/stir/awake @64). Numbers per file, Play next to 32 px buildings at 15/20/30/60/120 cells, layer motion in code-anim.json, accept/reject per file. | Claude |
| Done | Art review: 64 px B3 stills Buildings-64-008 to -011 (12 buildings) | In-game art reviewer: Brewery, Cauldron, Jade Carver (-008), Pill Furnace, Star Anvil, Talisman Atelier (-009), Heavenly Gate art (bld_ascension_gate), Charcoal Pit, Dragon Shrine (-010), Forge, Furnace Spirit, Meditation Pavilion (-011). Numbers per file (upscale diff, noise vs 32, outline, width, front 3/4, base), Play next to 64/32 buildings at 15/20/30/60/120 cells, code-anim.json entries (glow, flicker, smoke) replacing the 32-px loops, accept/reject per file. | Claude |
| Done | Art spec: stills + moving-part layers only | Rewrite ART-SPEC 3.9 so ChatGPT draws stills and separate layers for moving parts; no more hand-drawn loop frames. | Claude |
| Done | Review coast edge profiles + round side walls Islands-052 (7 biomes) | island_<biome>_coast_edge_profile_32x8_2f + island_<biome>_sidewall_round_32x32_4f x7 (optional pieces of the 2026-10-12 curved-coast request): inspect, wire into CoastCurves (new keys), rebuild island sheets, check every Island in Play at the zoom steps, accept or reject per file. | Claude |
| Done | Review snowy Fishing P1 (Islands-054, Fixtures-006) + Farm/Grove/Mine edge profiles (Islands-053) | Inspect 11 files, wire the ice edge along the pond shore (EnsurePond) and per-Island decor keys (island-decor.json -> edge ring), rebuild Island sheets/prefabs, check in Play before/after, accept or reject per file; ART-SPEC v6.14 + Drive mirror + feedback. | Claude |
| Waiting | Fix: fish and algae nodes surface on snow around the Fishing pond | Seen in the snowy Fishing review 2026-10-26: water nodes spawn on the shore/snow, not only in the water. | Claude |
| Done | Review snowy Fishing P2 + P1 redo (Islands-055/056/057, Fixtures-007) | Inspect 14 files (macro/flowers/pines/props redo; snow cornice profile, new edge_lip + ice_floe, scatter, straight wall, blob, side walls), wire edge_lip and ice_floe, rebuild sheets/prefabs, check in Play before/after, accept or reject per file; ART-SPEC + Drive mirror + feedback. | Claude |
| Done | Review portrait repairs UI-025 + snowy Fishing props/warm spring Fixtures-008 | 6 repaired NPC portraits (Jiang, Kang, Lin, Mo, Tian, Yao) validated as files against the 5 kept ones; deco_snow_props_64x64_4f, fix_spring_snow + flow loop wired for Fishing and checked in Play. | Claude |
| Done | Review new biome tilesets Lotus Marsh Islands-059 + Thunder Crag Islands-060 | Redo of the 2026-10-21 rejection (Islands-041..048): inspect blob/fill/water/cliff wall/side walls/macro/flowers with numbers vs the accepted Center set, preview assembled on a throwaway island in the bg copy at the zoom steps, validate (waiting on ADR 0007 Phase 6) or reject with repair text; ART-SPEC + Drive mirror + feedback; sheets docs/art/reference/biome-lotus-059.jpg, biome-thunder-060.jpg. Result 2026-10-27: Lotus blob, cliff wall, flowers, water and Thunder blob, cliff wall, flowers validated (wait on ADR 0007 Phase 6); both fills, both macros and all 4 side-wall files rejected (redo requested in ART-SPEC v6.18). | Claude |
| Blocked | Art redo: Lotus Marsh + Thunder Crag fills, macros, side walls (Islands-059/060 rejections) | Artist redo requested in ART-SPEC v6.18 (§3.7 rows): island_<lotus/thunder>_ground_fill (no mark repeated on an even spacing), _macro (irregular multi-colour outlines), _sidewall + _sidewall_variants (18 px Islands-038 format). Then the in-game art reviewer checks them on a preview Island. | Claude |
| Done | Review biome tileset redo Islands-061 (Lotus) + Islands-062 (Thunder) + Islands-063 (Azure Beast Ravine) | Redo deliveries 2026-10-27 00:41-00:47: Lotus + Thunder ground_blob (resent), ground_fill, macro, sidewall, sidewall_variants; Ravine cliffwall, ground_blob, ground_fill, macro, sidewall, sidewall_variants (redo of the 2026-10-21 rejection). Inspect with numbers vs the accepted Center set, preview Island in the bg copy at 1x-4x, validate (waits on ADR 0007 Phase 6) or reject with repair text; ART-SPEC + Drive mirror + feedback; sheets docs/art/reference/biome-lotus-061.jpg, biome-thunder-062.jpg, biome-ravine-063.jpg. Result 2026-10-27 (ART-SPEC v6.19): validated (wait on ADR 0007 Phase 6) = Lotus/Thunder/Ravine blobs, Lotus fill, Ravine cliff wall (+ Ravine flowers byte-identical to Islands-045); rejected 3x, awaiting orchestrator = Lotus/Thunder macro, sidewall, sidewall_variants and Thunder fill (docs/art/rejected *_v3_REJECTED.png); rejected with repair text = Ravine fill, macro, sidewall, sidewall_variants (*_v2_REJECTED.png). | Claude |
| Done | Review Azure Beast Ravine redo Islands-065 (fill, macro, side walls, blob) | Reviewed 2026-10-27 on a preview Island: blob (frame 46 = new fill v0), fill and both side-wall files validated (wait on ADR 0007 Phase 6); macro rejected 3x (X / four-lobe flat stamps) - awaiting owner decision tiles_finish, no 4th round. | Claude |
| Done | Review P2 NPC stills + layers Creatures-64-004 and Tribulation Peak tileset Islands-064 | Deliveries 2026-10-27 01:03-01:17: npc_he/lei/mo/yao@64 stills, npc_he_base + layer_lotus + layer_lotus_open, npc_yao_base + layer_pup_tail; island_tribulation cliffwall, ground_blob, ground_fill, macro, sidewall, sidewall_variants (+ flowers resent). Numbers vs the accepted P1 NPC stills and biome sets, stack test, code-anim.json entries for He/Yao/Lei/Mo with preview strips, throwaway preview Island; ART-SPEC + Delivery log + Drive mirror. | Claude |
| Waiting | Art redo: Lei + Yao stills (remove floating marks) and Tribulation cliff wall (Creatures-64-004 / Islands-064 rejections) | Requested in ART-SPEC v6.20: npc_lei@64 and npc_yao@64 resent with only the detached marks transparent (0 other px differ); island_tribulation_cliffwall_32x96_8f redrawn as stepped jade terraces (no vertical stripes). He/Yao layers wait on layers_finish; Tribulation fill, macro, side walls wait on tiles_finish. | Artist |
| Waiting | Route for Lotus/Thunder macro, side walls and Thunder fill (rejected 3x, Islands-061/062) | 7 files rejected a 3rd time on 2026-10-27 (ART-SPEC v6.19 §3.7 rows: island_<lotus/thunder>_macro_96x96_4f, _sidewall_32x32_4f, _sidewall_variants_32x32_8f, island_thunder_ground_fill_32x32_3f). The art loop stops here; the orchestrator picks the next route (owner question in the Hub, new brief, or in-house edit). Files in docs/art/rejected/*_v3_REJECTED.png; evidence docs/art/reference/biome-lotus-061.jpg, biome-thunder-062.jpg. | Claude |
| Done | Art redo: Azure Beast Ravine fill, macro, side walls (Islands-063 rejections) | Requested in ART-SPEC v6.19 (§3.7 rows): island_ravine_ground_fill (random low-contrast marks, no glint, not the Lotus layout), _macro (dithered multi-colour organic patches, not discs), _sidewall + _sidewall_variants (no per-tile moss cap, strata). Then the in-game art reviewer checks them on a preview Island. | Claude |
| Done | Art redo: snow props 64x64 + spring steam (Fixtures-009), Mo portrait (UI-026) | Requested in ART-SPEC v6.16 after the 2026-10-26 review: deco_snow_props_64x64_4f (snow caps vanish on the P06 ground, slab under props) and the steam of fix_spring_snow_flow (lies on the water); npc_mo_portrait (face too small, twin of Lei). Wiring is ready (island-decor.json bigProps/swaps). | Claude |
| Done | Review snowy Fishing round 3 (Islands-058, Fixtures-009) + Mo portrait UI-026 | Inspect 7 files (macro frames 0+2, ice floes, snow side faces, 64x64 snow props, spring steam loop, Mo portrait); pull each zip alone, integrate, rebuild Island sheets, Play before/after on Fishing, Mo side by side with the 10 portraits; accept/reject per file; ART-SPEC + Drive mirror + feedback; sheet docs/art/reference/snow-fishing-r3.jpg. | Claude |

## M4: Art coverage. Later

Make sure everything the player sees has real art, not placeholders or emoji.

**Exit:** No emoji icons remain in the UI and every delivered art piece is used somewhere in the game.

| Status | Task | Detail | Owner |
|---|---|---|---|
| Done | All buildings, nodes, items and creatures have real art | Every building, resource, item and enemy uses delivered pixel art. | Claude |
| Done | UI chrome: panels, buttons, cards, bars, cursor | The interface uses the delivered jade/lacquer frames, buttons and cursors. | Claude |
| Waiting | Replace emoji icons in the UI with real icons | Perk, upgrade, quest and toolbar icons were delivered but the UI still draws emoji. | Claude |
| Waiting | Use the 10 delivered pieces that have no place yet | Dragon stirring, meditating disciple, small glyphs, veil edge, waterfall pieces. | Claude |

## M5: Builds and tech health. Waiting

Keep the project healthy to build and store: Windows and WebGL builds that work, and a scene file small enough for GitHub.

**Exit:** Fresh Windows and WebGL builds show the new island look, and no file in the repository exceeds GitHub's limit.

| Status | Task | Detail | Owner |
|---|---|---|---|
| Done | Windows and WebGL builds | One-click builds; browser saves fixed so progress survives a reload. | Claude |
| Waiting | Rebuild Windows + WebGL with the new island look | Current builds predate the cliff walls and cloud sea. | Claude |
| Done | Keep the scene file under GitHub's 100 MB limit | Picked 'Yes, move it out' (2026-10-12): generated island layers become separate asset files in the repo (still on GitHub), so Game.unity shrinks well below the 100 MB limit. | Claude |

## M6: Agent team and background copy. In progress

Agents work like a small studio: permanent designer roles with their own guide documents, and all agent work happens in a background copy of the project so the owner's open Unity editor is never touched.

**Exit:** 11 role agents defined with guide docs; a background worktree + headless Unity runs imports, rebuilds, screenshots and tests; CLAUDE.md rules updated; one real task done end to end through the background copy.

| Status | Task | Detail | Owner |
|---|---|---|---|
| Done | Define the 11 role agents and their guide documents | World, Balance, Onboarding, UI/UX, UI QA, Art director, In-game art reviewer, Writer, Playtester, Performance, Code health — each an agent definition with a guide doc it owns. | Claude |
| Done | Background copy: git worktree + headless Unity for all agent work | A second copy of the project where agents import, rebuild, take screenshots and run tests without touching your open editor; results arrive via git. | Claude |

## M7: Android build. In progress

The game runs and plays well on an Android phone in landscape, with touch controls, and the owner gets an APK to test.

**Exit:** Touch controls cover every mouse action; UI readable on a phone; landscape locked; full test gate green; APK built and tested (emulator or device); owner confirms on his phone.

| Status | Task | Detail | Owner |
|---|---|---|---|
| Done | Touch controls, phone UI, landscape, Android build | Merged 2026-10-13: tap/hold/long-press feed, Give + Swap buttons, drag pan, pinch zoom, drag-ghost placement with Place/Cancel, tap-linger tooltips, Back menu; safe area, dpi canvas scale, 48dp hit areas; landscape lock; APK 51 MB. Touch PlayMode tests 15/15. | Claude |
| Blocked | Run the APK on an Android emulator | Install the Android emulator + a system image (large download from Google) to test the APK before you do. | Claude |
| Done | Owner tests the APK on his phone | 2026-10-13: runs on the owner's phone. Feedback: Menu overflows, bottom bar crowded/clipped, desktop control words in quest/help text, Swap/Give buttons unstyled, wants a fresh start with no buildings (screenshots in docs/Design/feedback/phone-2026-10-13-*). | Mariusz (owner) |
| Done | Phone fixes round 1 + new game with no buildings | Merged 2026-10-13: Menu fits (jade, 3-column), compact phone bar (Stats/Build/Demolish/Menu), touch wording (ControlWords), styled Swap/Give, New game starts with NO buildings (bot: 2:33 human pace, auto/hand 3.8x mid-run - below the 5x target). | Claude |
| In progress | Owner tests APK round 2 **(NOW)** | Check the Menu, bar, touch wording, Swap/Give and New game on the phone; quest panel looked semi-transparent in captures - confirm on device. | Mariusz (owner) |
