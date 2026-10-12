# Plan

Updated 2026-10-12 07:54. in-progress: 6, blocked: 6, waiting: 3, done: 7

## Being worked on now

- Island requesters design (ADR 0007) (Opus agent: finalising ADR 0007 with all answers + art request)
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

## M2b: Island requesters replace ascension

- [ ] **Island requesters design (ADR 0007)** (in-progress, M, Claude)
  - All decided 2026-10-12: unlock order Mine, Fishing, Farm, Grove, Volcano, Lotus Marsh, Thunder Crag, Azure Beast Ravine, Tribulation Peak, Celestial; 1 NPC per island (11), 3 requests all open, right-click feeding only, Heavenly Gate ending, vows removed.
  - Now: Opus agent: finalising ADR 0007 with all answers + art request
- [ ] **Remove ascension (gate, points, perks, vows, prestige reset)** (blocked, M, Claude)
  - Take out the Ascension Gate, Ascension Points, perk shop, vows and the run reset; old saves keep their progress.
  - Why: Needs the requester design (ADR 0007) so nothing the game relies on is removed early
- [ ] **Build island requesters** (blocked, L, Claude)
  - Requester buildings/NPCs per island with a request menu; each fulfilled request unlocks recipes.
  - Why: Needs your answers on the requester design
- [ ] **4 new islands: Lotus Marsh, Thunder Crag, Azure Beast Ravine, Tribulation Peak** (blocked, L, Claude)
  - Picked 2026-10-12: all 4 join the unlock order, each with one NPC and new buildings; needs ~100 new art pieces.
  - Why: Needs the final ADR 0007 and the new island art (art request goes out with the ADR)

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

