# Idle Grounds: plan

Updated 2026-10-12 10:09. Source of truth: docs/Design/plan.json, edited with tools/plan/set-task.cjs. Do not edit PLAN.md by hand: edit plan.json and run `node tools/plan/build-plan.cjs`.

**13 of 26 tasks done**, 1 in progress, 1 to do, 3 blocked, 8 waiting.

## Being worked on now

- **M3** South cliff walls, side faces, grass lips: art loop: integrating deliveries as they arrive

| # | Milestone | Gate | Status | Tasks done |
|---|---|---|---|---|
| M1 | Unity port of the browser game |  | Done | 2 / 2 |
| M2 | Balance and onboarding |  | Done | 2 / 2 |
| M2b | Island requesters replace ascension (ADR 0007, accepted) |  | Waiting | 2 / 8 |
| M3 | 3/4 world look (ADR 0005) |  | In progress | 3 / 7 |
| M4 | Art coverage |  | Later | 2 / 4 |
| M5 | Builds and tech health |  | Waiting | 2 / 3 |

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
| Blocked | Art for the shrine, NPCs and 4 new islands (ART-SPEC §3.7, 144 files) | P1 shrine + 7 NPC sprites first, then the new island tilesets, buildings and items. | Artist |
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
| Waiting | Smooth stepped and long-straight coasts | Diagonal coasts read as rows of short pillars; reshape the coastline so cliffs run in longer, natural stretches. | Claude |

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
