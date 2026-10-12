# Plan

Updated 2026-10-12 08:05. in-progress: 5, blocked: 4, waiting: 8, done: 8

## Being worked on now

- South cliff walls, side faces, grass lips (art loop: integrating deliveries as they arrive)
- Integrate vertical side walls (v2, with variants) (Opus agent: integrating + Play review)
- Integrate the new hanging undersides (3/4 rock, roots, vines) (Opus agent: integrating + Play review)
- Fishing ground patches (3rd version) (Opus agent: integrating + Play review)
- Rebuild the sky with the kept background islands (Opus agent: integrating + Play review)

## M1: Unity port of the browser game

- [x] **Port every system and screen from the HTML game** (done, L, Claude)
  - The full game runs in Unity with scenes, prefabs and data assets; parity audits passed.
- [x] **Floating islands, Spirit Bridges, no offline progress** (done, L, Claude)
  - Regions became floating Islands linked by one-way bridges (ADR 0002, 0003).

## M2: Balance and onboarding

- [x] **Real balance: 2-3 h first run, machines beat hands mid-game** (done, M, Claude)
  - TEST mode off by default; playthrough bot checks the pacing (ADR 0004).
- [x] **Island onboarding quests** (done, M, Claude)
  - Quests that teach bridges, caravans and cross-island trade, with next-step hints.

## M2b: Island requesters replace ascension (ADR 0007, accepted)

- [x] **Island requesters design (ADR 0007)** (done, M, Claude)
  - Accepted 2026-10-12: 11 islands in a fixed order unlocked by tributes at the north Sky-Bridge Shrine; 11 NPCs (one per island) with 3 optional requests each; Heavenly Gate ending.
- [ ] **Phase 1: remove ascension, perks, vows, offerings; Heavenly Gate ending; save migration** (waiting, L, Claude)
  - Old saves keep their progress; the game stays playable on 7 islands.
  - Why: Starts after the art integration agent leaves the Unity editor
- [ ] **Phase 2: Sky-Bridge Shrine, fixed unlock order, Celestial moves north, Sky Paths** (waiting, L, Claude)
  - Replaces the unlock steles; quests retargeted to the new order.
  - Why: After phase 1
- [ ] **Phase 3: requester NPCs on the 7 existing islands (21 requests)** (waiting, L, Claude)
  - Request panel, right-click feeding, rewards: new buildings and automation utilities.
  - Why: After phase 2
- [ ] **Phase 4: 4 new islands (Lotus Marsh, Thunder Crag, Azure Beast Ravine, Tribulation Peak) + their NPCs** (waiting, L, Claude)
  - Nodes, Azure Wolf, 9 buildings, 12 items, 4 NPCs; placeholder art until delivered.
  - Why: After phase 3 and the Game.unity split (M5)
- [ ] **Phase 5: balance to a ~3 h ending** (waiting, M, Claude)
  - Playthrough bot: greedy run 2:30-3:30, no-requests run under 4:15.
  - Why: After phase 4
- [ ] **Art for the shrine, NPCs and 4 new islands (ART-SPEC §3.7, 144 files)** (blocked, L, Artist)
  - P1 shrine + 7 NPC sprites first, then the new island tilesets, buildings and items.
  - Why: Requested 2026-10-12 in ART-SPEC §3.7

## M3: 3/4 world look (ADR 0005)

- [ ] **South cliff walls, side faces, grass lips** (in-progress, L, Claude)
  - Islands show a 3-tile cliff on the south edge and a narrow earth face on the west/east edges, so they read as floating rock seen from above.
  - Now: art loop: integrating deliveries as they arrive
- [ ] **Integrate vertical side walls (v2, with variants)** (in-progress, S, Claude)
  - Swap the rotated first version for the artist's corrected vertical side faces.
  - Now: Opus agent: integrating + Play review
- [ ] **Integrate the new hanging undersides (3/4 rock, roots, vines)** (in-progress, S, Claude)
  - Replace the side-view rocks under the south cliffs with the redrawn 3/4 pieces.
  - Now: Opus agent: integrating + Play review
- [ ] **Fishing ground patches (3rd version)** (in-progress, S, Claude)
  - Calm cool-grey shingle patches for the Fishing island; earlier two were rejected.
  - Now: Opus agent: integrating + Play review
- [ ] **Rebuild the sky with the kept background islands** (in-progress, S, Claude)
  - Background islands now load from their own folder so new deliveries can't replace them.
  - Now: Opus agent: integrating + Play review
- [ ] **Cliff wall seam and stripe fix** (blocked, S, Artist)
  - The artist's cliff tiles still have transparent seams and a light-to-dark stripe per tile; patched in code for now.
  - Why: Waiting for the artist's corrected cliff walls (ART-SPEC delivery log)
- [ ] **Smooth stepped and long-straight coasts** (blocked, M, Claude)
  - Diagonal coasts read as rows of short pillars; reshape the coastline so cliffs run in longer, natural stretches.
  - Why: Picked 'Yes, now' (2026-10-12); waiting for the Unity editor to respond (Reload dialog)

## M4: Art coverage

- [x] **All buildings, nodes, items and creatures have real art** (done, L, Claude)
  - Every building, resource, item and enemy uses delivered pixel art.
- [x] **UI chrome: panels, buttons, cards, bars, cursor** (done, M, Claude)
  - The interface uses the delivered jade/lacquer frames, buttons and cursors.
- [ ] **Replace emoji icons in the UI with real icons** (waiting, M, Claude)
  - Perk, upgrade, quest and toolbar icons were delivered but the UI still draws emoji.
  - Why: After the 3/4 world look lands
- [ ] **Use the 10 delivered pieces that have no place yet** (waiting, M, Claude)
  - Dragon stirring, meditating disciple, small glyphs, veil edge, waterfall pieces.
  - Why: After the 3/4 world look lands

## M5: Builds and tech health

- [x] **Windows and WebGL builds** (done, M, Claude)
  - One-click builds; browser saves fixed so progress survives a reload.
- [ ] **Rebuild Windows + WebGL with the new island look** (waiting, S, Claude)
  - Current builds predate the cliff walls and cloud sea.
  - Why: After the pending integrations
- [ ] **Keep the scene file under GitHub's 100 MB limit** (blocked, M, Claude)
  - Picked 'Yes, move it out' (2026-10-12): generated island layers become separate asset files in the repo (still on GitHub), so Game.unity shrinks well below the 100 MB limit.
  - Why: Waiting for the Unity editor to respond (Reload dialog)

