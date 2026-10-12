# Plan

Updated 2026-10-12 12:00. in-progress: 1, blocked: 8, waiting: 3, done: 7

## Being worked on now

- South cliff walls, side faces, grass lips (art loop: integrating deliveries as they arrive)

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
- [ ] **Decide what each ascension unlocks** (blocked, M, Mariusz)
  - Ascension no longer speeds the world up, so later runs need new content instead (new islands, buildings, recipes).
  - Why: Owner answer: Questions tab, 'What should ascensions unlock?'

## M3: 3/4 world look (ADR 0005)

- [ ] **South cliff walls, side faces, grass lips** (in-progress, L, Claude)
  - Islands show a 3-tile cliff on the south edge and a narrow earth face on the west/east edges, so they read as floating rock seen from above.
  - Now: art loop: integrating deliveries as they arrive
- [ ] **Integrate vertical side walls (v2, with variants)** (blocked, S, Claude)
  - Swap the rotated first version for the artist's corrected vertical side faces.
  - Why: Unity editor is not responding (likely a 'scene changed on disk - Reload' dialog)
- [ ] **Integrate the new hanging undersides (3/4 rock, roots, vines)** (blocked, S, Claude)
  - Replace the side-view rocks under the south cliffs with the redrawn 3/4 pieces.
  - Why: Unity editor is not responding
- [ ] **Fishing ground patches (3rd version)** (blocked, S, Claude)
  - Calm cool-grey shingle patches for the Fishing island; earlier two were rejected.
  - Why: Unity editor is not responding
- [ ] **Rebuild the sky with the kept background islands** (blocked, S, Claude)
  - Background islands now load from their own folder so new deliveries can't replace them.
  - Why: Unity editor is not responding
- [ ] **Cliff wall seam and stripe fix** (blocked, S, Artist)
  - The artist's cliff tiles still have transparent seams and a light-to-dark stripe per tile; patched in code for now.
  - Why: Waiting for the artist's corrected cliff walls (ART-SPEC delivery log)
- [ ] **Smooth stepped and long-straight coasts** (blocked, M, Claude)
  - Diagonal coasts read as rows of short pillars; reshape the coastline so cliffs run in longer, natural stretches.
  - Why: Owner answer: Questions tab, 'Smooth the coastlines now?'

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
  - Game.unity is about 78 MB because generated island dressing is stored in it.
  - Why: Owner answer: Questions tab, 'Move generated island dressing out of the scene?'

