# Art direction guide (Art director)

Agent: `.claude/agents/art-director.md` (opus). Spec: `docs/art/ART-SPEC.md` (mirrored to Drive `AI files/Idle Grounds Art/`).

## Purpose
Set and protect the look: style rules, palette, sizes, the look of NPCs, enemies and creatures, island and coast look, and the art requests sent to the artist (ChatGPT via Drive). Acceptance of delivered art is the in-game art reviewer's job (`ingame-art-review.md`).

## Current state of this game
- **View:** top-down **3/4** (Stardew / Eastward). ADR 0005: only the **south** coast shows a cliff wall (3-cell wall, seam-repaired copies in `Art/Islands/Dressing`), W/E/N coasts get a grass lip and a soft drop shadow, hanging undersides only below the south wall, and a **sea of clouds seen from above** below the islands (tiled `ParallaxLayer`, menu `Idle Grounds/World/Rebuild Sky (cloud sea)`). No horizon, peaks or moon.
- **Technical:** 32 px per cell, **PPU 32**, Point filter, no compression, integer scale only (1x or 2x), PNG RGBA, binary alpha (4 alpha steps allowed for glows, mist, clouds), light from the upper left, 1 px ink outline on items/creatures/icons.
- **Palette:** 32 colours P01-P32 (ART-SPEC 1.3), e.g. ink `#0D0F14`, jade `#2F9A7A`, cinnabar `#C8322B`, gold `#F2C94C`, qi cyan `#5ED4E8`, lava `#FF8A1F`, celestial violet `#7D5FC4`. Roles: green nature, cinnabar sect, gold rewards, cyan wisps/water, orange fire, violet celestial. Per-Island palettes in 1.3.
- **Canvas sizes (2.2):** items 32x32, 1x1 buildings 32x48, standard 3x3 buildings 96x128, Burners 3x5 96x192, landmarks (Altar, Dragon, Heavenly Gate, Shrine, Beast Pen, Tribulation Array) 160x192, nodes 32x48 / 64x80 / 96x112, creatures 32x32 (boar 48x32) facing right, wisp 12x12, requester NPCs 32x48 facing the viewer, portraits 64x64.
- **Coast ground tiles:** 47-tile blob sheets (bit order N,E,S,W,NE,SE,SW,NW ascending; `blob_mask_reference.json`) plus 3 fill variants and scatter. Delivered: Center blob v5 and fill v2.1; other biomes use macro patches and generated coasts.
- **Coast feedback history (owner):** (1) `coast_shape` "redo": "Coastline has to look natural, the tiles need to connect better"; (2) `coast_signoff` "finer / rounder": "We can't have 90degrees angles". Result so far: coast redo #2 from a seeded 2-3 cell lattice, per-joint cliff walls (Run/End/Tuck), inward side-face feet; P1 art request for diagonal / rounded coast and joint pieces is open.
- **Rejected so far** (`docs/art/rejected/`): Center blob v4 (teal v1 look), sky clouds v1, Farm/Fishing/Grove blob+fill v1 (busy tiling), `sky_depth` (too busy), Fishing macro v1-v3 (warm tan stains on cool shingle), `_3q` underside redraws that were grass-topped mini-islands, scrim (dither grid). Cliff wall seams/stripes are with the artist.
- **Open art volume:** ART-SPEC section 3.7 requests ~144 files (P1: Shrine and 7 NPCs, then the 4 new Islands). Ten delivered keys still have no consumer.

## Rules to enforce
1. 3/4 view for everything; no side-view pieces.
2. Palette and alpha rules; no anti-aliasing against transparency; no baked shadows unless the table says so.
3. Natural coast: no 90-degree corners, no 1-cell staircases, steps of 2-3 cells, rounded islets, joints that connect.
4. NPCs: 32x48, 3/4 front, friendly, each Island's NPC reads at a glance by silhouette and one colour accent; portraits match.
5. Enemies and creatures: 32x32, facing right, calm and slightly comic, no blood, 4f idle at 6 fps, 4f move at 10 fps, 2f hit at 12 fps.
6. Same-kind objects look the same across Islands (a "tree" is a tree).
7. Every brief names filenames, sizes, frames, reference pieces, priority.

## Checklists
- **New art request:** add rows to ART-SPEC with exact keys; check not already delivered (ledger); priority; mirror to Drive.
- **Redo request:** quote the rejected key, what is wrong with a screenshot, what is expected, link the owner rule that applies.
- **New biome:** palette from 1.3, ground blob, fill, macro, flowers, cliffwall, lip, underside, veil.
- **Style audit:** pick 5 random in-game screenshots; any piece that breaks the rules becomes a plan task.

## Open issues
- Diagonal / rounded coast and joint pieces; wall width and end frames; fishing macro; `sky_depth`.
- Placeholders under `Art/Islands|Sky|Bridges` until replaced by same-named delivery.
- Section 3.7 batch (144 keys) is `blocked` on the artist in the plan.
- **Building density (2026-10-21):** owner says all buildings look too pixelated / low quality (`vp_props`). Audit + reference sheet done (`docs/art/reference/buildings-sheet.png`, `tools/art-ref/buildings-sheet.ps1`: 8 REF, 10 CLEAN, 8 REDRAW); redo draft ART-SPEC 3.9 waits on Hub question `art_density` (32 vs 64 px per cell, scope) and is not on Drive yet.
- **Snowy Fishing (2026-10-21, `vp_fishing`):** ART-SPEC 3.8 sent to Drive. Snow is a mid-value field (P06 base, P07 only on crests) to respect `vp_mood`; ice shelf strip laid along the pond curve; no green decor on Fishing.

## Rule change log
- 2026-10-21: Fishing palette (ART-SPEC 1.3) is now snow P05-P07 + ice P27-P29 + water P26-P27. Building target rules (ART-SPEC 3.9, pending density): projection test beside Paper Mill / Storehouse / Meditation Pavilion, silhouette at least ~85 % of the footprint width, smallest detail a 2x2 cluster, 1-px noise at most ~16 % (sheet metric), no baked shadow or steam in static frames.

## Owner feedback that constrains this role
`owner-picks-2026-10-12-coast.json` (both answers above), `owner-picks-2026-10-21-visual.json` (`vp_props` audit + "too pixelated", `vp_fishing` snow, `vp_mood` calmer ground, `vp_zoom` crisp steps), ADR 0005 (user: "the perspective of the floating island backgrounds doesn't match the game island perspective"), and the ongoing owner sign-off on coast looks through the Hub (`coast_signoff`).
