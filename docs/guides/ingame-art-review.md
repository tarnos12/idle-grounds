# In-game art review guide (In-game art reviewer)

Agent: `.claude/agents/ingame-art-reviewer.md` (sonnet).

## Purpose
Judge art as it appears in the running game and run the art-intake accept / reject step. A file that looks right in an image viewer can still fail in play: wrong layer, a seam at zoom 2x, wrong scale next to the hand, a hue jump at a border.

## Current state of this game
- **Pipeline:** artist (ChatGPT) delivers to Drive `AI files/Idle Grounds Art/incoming/<category>/` (mounted at `G:\Mój dysk\AI files\Idle Grounds Art\`). `node tools/art-intake/pull.js [--dry]` copies/unzips into `Assets/_Project/Art/Incoming/` (log `intake-log.json`; newest zip wins; `*preview*` goes to `_notes`). Unity menu `Idle Grounds/Art/Integrate Incoming Art` wires by key; it must report **0 changes on a second run and 0 unwired keys**. Island sheets: `Regenerate Placeholder Blob Sheets`, then `Rebuild Island Rim + Underside + Veil`.
- **Status sources:** ART-SPEC Delivery log (our status and feedback; repo copy `docs/art/ART-SPEC.md` is mirrored to Drive after every edit), `DELIVERY-LEDGER.json` (artist side; read it, do not rewrite unless the owner says we co-own it), `feedback/` evidence screenshots.
- **Conventions:** blob bit order N,E,S,W,NE,SE,SW,NW; real building art is the body (`BuildingAsset.hasRealArt`); real nodes render native-size bottom-aligned; `<key>_flow`, `<key>_sparkle`, `<key>_2x2` suffix rules; animated variants; `GameDatabase.fx` / `enemyAnims`.
- **Rejected on review so far:** see `art-direction.md` and `docs/art/rejected/` (keep the last good version pinned; e.g. blob v3 stayed pinned until v5).
- Current weak spots seen in play: cliff wall seams and stripes, W/E coasts, south-coast staircase diagonals, heavy side-view undersides (replaced by `_3q`), Fishing macro, Center path/plaza delivered but not painted.

## Rules to enforce
1. **Sixteen standing Drive rules** (summary): read status first; start new actionable work at once; follow the latest priorities; identify new files via ledger + intake log; inspect before integrating; integrate per manifest within technical constraints; confirm in Play screenshots before accepting; delivered / validated / integrated / accepted are different; document defects with exact filenames + evidence; re-read and merge before writing; upload requested outputs and verify; recheck Drive after each integration; blockers per task; stop only when nothing actionable remains (leave a resume point in HANDOFF); stay quiet when nothing changed; never claim work without evidence.
2. Accept only after seeing it in Play at closest and furthest zoom.
3. Never replace a good pinned asset with a regression.
4. Coast: no 90-degree angles, no 1-cell staircases, tiles connect, corners read as curves (owner rules). Since coast redo #4 the coast is a curve (`Editor/CoastCurves.cs`): one straight segment per crossed cell, ground / wall / lip / side-face pieces composed at build time into `Art/Islands/Coast/coast_{ground,wall,edge}_<biome>.png` (tiles in `Tiles/Coast`). Review curves at closest zoom for wall streaks on steep W/E turns, lip gaps on N coasts and tile-edge lines; screenshots: `bash tools/bg/shot.sh coast4` (veils hidden) and `coast4-veil`.
5. 3/4 view only; PPU 32; Point; integer scale.

## Checklists
- **Per delivery:** files complete and named right, PNG RGBA, exact size, frames, palette, alpha, tiling, loop closure.
- **Per integrated key:** sorting layer and order; shadow; scale vs 1 cell and the hand; no seams (1x and 2x zoom, all four coasts); animation loops; matches neighbours; save the scene.
- **Island change:** screenshot each of the four coasts, an inner corner, an outer corner, a peninsula and an islet; check wall-to-underside joints.
- **Reject:** move to `docs/art/rejected/` with `_vN_REJECTED`, write feedback sheet with screenshot, update Delivery log and Drive mirror.

## Open issues
- Review runs in the background copy; Play screenshots need headless Unity with a game view. Until that exists, report which checks were file-only.
- Ten delivered keys unused (dragon_stir, npc_disciple_meditate, glyph_*, island_veil_edge, island_waterfall*).
- ART-SPEC v4 batch (shrine, 11 NPCs, 4 Islands) will arrive in bulk; review P1 first.

## Owner feedback that constrains this role
`owner-picks-2026-10-12-coast.json` ("natural", "tiles need to connect better", "no 90 degree angles") is the acceptance bar for any coast, wall or joint piece. Visual sign-off questions go to the Hub with pictures.
