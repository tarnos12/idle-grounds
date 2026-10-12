# In-game art review, 2026-10-13

Reviewer: In-game art reviewer (read-only pass, no Unity run, no game files changed).
Source: screenshot tour `bg/tour` captured 2026-10-12, 1920x1080, real Play mode, headless (`tools/bg/shot.sh`). Tour dir: `C:/Users/Tarnos/AppData/Local/Temp/claude/D--WORK-Unity-idle-grounds/852aa094-726c-4216-b1e9-e2270f12c7b3/scratchpad/tour/` (file names below are relative to it).

Bars used: owner-picks-2026-10-12-coast.json (natural coast, tiles connect, no 90 degree angles), ART-SPEC/guide rules (PPU 32, Point, integer scale, 3/4 view, macro patches not visibly repeating).

## Coverage and honesty notes

- Looked at and judged: 02, 13, 21, 22, 28, 32, 33, 34, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 48, 62, 63, 64, 65, 66, 67, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 88, 91, 93, 94, 95, 98, 99, 100.
- Not individually inspected (image budget ran out, UI-modal shots mostly, covered by the UI-QA review): 01, 03-12, 14-20, 23-27, 29-31, 35-36, 47, 49-61 (whole-island zoom-6 sets), 86, 87, 89, 90, 92, 96, 97.
- Everything is judged from 1920x1080 frames, not by pixel peeping a crop. Findings that need a 1:1 crop to be certain are marked "verify".
- Frames are single stills (20 ms steps), so animation loops, jitter and particle timing were NOT judged. The Awakened Dragon, forge fire, wisps and cloud motion are unreviewed.
- Status of the tour's scope: the 3 new islands in owner-picks (Lotus Marsh, Thunder Crag, Beast Ravine, Tribulation Peak) are not in the build yet, so not reviewed.

## Findings

### P1 (fix before the next sign-off)

**P1-1. Locked-island veil: visible tile seams, and the coast rim leaks through it**
- Shots: `94_locked_farm_unlock_stele_normal_zoom.png` (vertical seams at about x=207/645/1085/1525, horizontal steps at about y=365/675/800; orange dash marks along the right edge x about 1500-1570, y 240-880), `95_locked_fishing_unlock_stele_normal_zoom.png` (seams at about x=108/545/985/1425/1865 and y=520/640), `91_locked_view_west_from_center_zoom6.png` (orange dashes on the veil edge at x about 510).
- Problem: the veil pattern is a single tile with non-matching edges, so brightness steps form a grid across the whole island. These are exactly the views a new player sees (Farm and Fishing are the first locks, at normal zoom 2). At the island edge the real coast lip/wall peeks through as orange dashes instead of being covered, so the veil edge looks torn.
- Likely cause: art. The veil texture (`island_veil` family, built by `Rebuild Island Rim + Underside + Veil`) is not seamless, plus the veil mask does not cover the lip sprite (sort order of veil vs coast lip, or lip drawn above the veil).
- Fix: regenerate the veil texture as a true seamless tile (or use a larger tile than the camouflage motif, plus a low-frequency overlay to hide repeats); raise the veil sorting order above the coast lip/wall sprites or extend the mask by 1-2 texels. Check at zoom 2 and 6.
- Effort: M.

**P1-2. Heavenly Gate captions unreadable and drawn over the art**
- Shots: `43_bldg_heavenly_gate_close.png` (text at y about 835 and 945), `34_end_heavenly_gate_hover.png` (name tag collides with the caption at x about 860-1060, y about 940).
- Problem: "Heavenly Gate" is drawn on top of the gate sprite (gold on gold/red, over the swirl base). "Ascended - click to see the ending" is pale yellow on the near-white Celestial ground, so it is almost invisible, and the left half sits under the hover name tag. Gold selection rectangle crosses the sprite's top third inconsistently (43 vs 34: rectangle starts at y=465, hover box at y=370).
- Likely cause: code (world-space caption has no background plate/outline; positioned at sprite centre-bottom). Not art.
- Fix: caption on a dark plate (same style as the storehouse count plate), anchored below the sprite's footprint; one outline rectangle only (footprint, not sprite bounds); drop the second line when the name tag shows.
- Effort: S.

**P1-3. Dense cluster: world-space labels overprint each other and the art**
- Shots: `33_end_recipe_picker_forge_dense_cluster.png` (pavilion "3/3" over "Needs Spirit Buds" at about 610-760, 800-850; stack rows at y=878 and 933 sit on the next building's base), `22_mid_bridge_panel_paired.png` (orange "Output pile full" over "1/3" and the pavilion base at about 625-745, 815-850), `38_bldg_cluster_north_close.png` (row text "0/3" colliding with "12/2" at about 1640-1800, 215-245; "0/200" tag plus "- Fishing" green label sitting on the dashed link lines, about 830-985, 395-410).
- Problem: status lines, icon rows and counters are all drawn centred on the building with no collision handling; in the late-game layout they overprint each other and cover the lower 20 percent of neighbouring sprites. Red status text is also very low contrast on the saturated green (`No fuel`, `Needs Spirit Buds`).
- Likely cause: code (view layer placement). Art is fine.
- Fix: dark text plate for status lines; stack the icon row / status / counter in one anchored column per building; hide status text at zoom >= 3; clamp so rows never overlap a neighbour (simple vertical offset when bounds intersect).
- Effort: M.

**P1-4. Wood rack and Spirit Bridge sprites read as opaque placeholder rectangles**
- Shots: `37_bldg_converter_cluster_south_close.png` (rack at about 795-1125, 155-375), `38_bldg_cluster_north_close.png` (rack top-left; bridge at about 795-1015, 265-375), `33`, `39`, `44_bldg_fishing_stone_lantern_bridge.png` (bridge at about 823-933, 600-678).
- Problem: the fuel rack is a hard-edged, fully black rectangle with an orange frame and no ground contact/shadow, so it is the highest-contrast object in the base and looks like a missing texture. The Spirit Bridge is a small dark plate with two lantern pillars (about 110x55 px next to 160 px storehouses and 310 px mills) and a "0/60" label printed over its middle; it is also visually "flat" next to the painted 3/4 buildings. Same sprite reads differently when inactive (38 grey plate vs 22 coloured).
- Likely cause: art (placeholder, `hasRealArt` false or a framed icon sheet, not a 3/4 building) and data (scale/pivot).
- Fix: replace with real 3/4 art with transparent corners and a baked soft shadow; bridge about 2x2 cells with the counter below the footprint, not over the sprite. Until then tint the rack plate to the ground (alpha 0.6) so it does not dominate.
- Effort: L for art, S for the interim tint.

**P1-5. Pixel density and scale are not integer (verify)**
- Evidence: arithmetic from INDEX (zoom 2 = 35 cells over 1920 px = 54.9 px/cell, PPU 32 = 1.71 px per texel; zoom 1 = 109.7 px/cell = 3.43 px/texel). Also visible: macro ground patches, sky and veil use about 2x larger texels than nodes and buildings (compare `62_center_coast_north.png` cloud blocks about 6 px vs bush detail about 3 px; `94` veil blocks about 6 px vs stele about 3 px; `41_bldg_awakened_dragon_close.png` dragon detail much finer than the ground).
- Problem: the project rule is integer scale + Point. If the orthographic size is not snapped (or no PixelPerfectCamera upscale), pixel columns will be uneven and shimmer while panning/zooming; mixed texel density makes sprites look pasted onto a different-resolution world.
- Likely cause: code (camera zoom steps are continuous 1..6, not integer pixel multiples), plus art (terrain/sky delivered at 2x density).
- Fix: verify with a 1:1 crop of two adjacent texels in a flat sprite; if uneven, snap camera ortho size so screen px per texel is an integer at each zoom stop (or render to a low-res RT then upscale); ask the artist to keep macro patches and sky at 1x texel density (or declare 2x as intentional in ART-SPEC).
- Effort: M (camera) and L (art, if density mismatch is not accepted).

### P2 (visible polish issues)

**P2-1. Cliff wall tiles repeat as vertical stripes and show tile joints**
- Shots: `67_farm_coast_south.png` (identical sand columns every about 54 px), `77_volcano_coast_south.png` (lava-crack pattern repeats every about 54 px, hard seam where it meets the underside rocks), `83_celestial_coast_south.png` (thin light vertical joints at about x=820 and 1150), `74_fishing_coast_south.png`, `80_grove_coast_south.png`, `70_mine_coast_south.png` (wall reads as a picket fence / railing).
- Problem: the `coast_wall_<biome>` tiles are short with high-contrast verticals, so the repeat is obvious; the wall bottom edge is a ruler-straight line in 67 before the underside, while the rocks beneath hang from separate anchors with sky gaps between wall and rock (67 at about x=430-520, y=830-850).
- Likely cause: art (wall tile width and lack of variants) plus build-time composition in `Editor/CoastCurves.cs` (one straight segment per cell, same piece each time).
- Fix: 3-4 wall variants picked by hash, with the vertical features offset so they never align; give the wall a ragged bottom edge that overlaps the underside piece by 2-3 texels; check Mine south (add a top lip and a stone-block pattern).
- Effort: M.

**P2-2. Grove (and partly Fishing/Celestial) lip steps as per-segment slabs**
- Shots: `80_grove_coast_south.png` (bright green lip blocks at each segment, about x=0-960, y=840-1000), `74_fishing_coast_south.png`, `83_celestial_coast_south.png` (dark floating outline above the pale lip at about y=660-700).
- Problem: along the curve the lip is drawn as separate rectangular blocks that step down, which reads as a staircase and violates the owner rule (no 1-cell staircases, no 90 degree angles). Celestial has a detached dark outline line above the lip (a gap of about 2 texels).
- Likely cause: code/data (lip placed per cell segment without rotation or without a continuous cap) and art (lip piece has square ends).
- Fix: rotate the lip piece to the segment angle with 1-texel overlap, or draw the lip as a continuous strip along the baked curve; remove the Celestial outline gap (move the outline piece to the lip's top row).
- Effort: M.

**P2-3. Underside hanging rocks overlap the wall face on diagonal coasts**
- Shots: `64_center_coast_east.png` (root-and-rock piece drawn over the wall at about x=1300-1480, y=660-890), `63_center_coast_south.png` (corner at bottom-left), `98_sky_below_center_to_fishing_zoom6.png` (spaced triangular stalactites, wall bottom ends in jagged teeth against open cloud).
- Problem: on diagonal and steep segments the underside piece starts mid-wall and covers it, with a visible vertical offset from the wall bottom; on flat segments the pieces sit evenly spaced like identical teeth.
- Likely cause: code (underside anchor uses the segment centre, not the wall's bottom-edge point along the normal) and art (only 3-4 variants).
- Fix: anchor on the wall's bottom edge, with random lateral jitter and size scale 0.8-1.2; draw the piece behind the wall (lower sort order) for the first 4 texels.
- Effort: S-M.

**P2-4. Macro ground repeats too regularly (Mine, Celestial, Volcano, Fishing)**
- Shots: `69_mine_ore_field_normal_zoom.png` and `71_mine_coast_west.png` (repeating checkerboard-dither tile with equal-spaced light dashes; identical mist patches), `82_celestial_starrock_field_normal_zoom.png` (dot-and-dash pattern in rows), `76_volcano_obsidian_field_normal_zoom.png` (purple dots in a regular grid), `72_fishing_pond_field_normal_zoom.png` (pond water: lily/lotus rows at regular intervals).
- Problem: ground tile is clearly periodic at the 5-6 cell scale; on Mine the busy dither competes with the ore nodes for readability (grey boulders vs grey ground). Pond interior repeats and looks like a pasted rectangle with a ragged fringe.
- Likely cause: art (single ground tile, no variants/macro overlay) and data (macro patch sprites repeat identically).
- Fix: add a second low-contrast macro layer (Center and Grove already have patches; reuse that pipeline), reduce dither contrast on Mine, offset the pond's lily pattern with a noise mask.
- Effort: M.

**P2-5. Stone path and plaza: 90 degree staircases, dashed debug-like outline, double edges**
- Shots: `37_bldg_converter_cluster_south_close.png` (path top-right, brown dotted outline, right-angle steps), `40_bldg_altar_close.png` (plaza overlays a second lighter semi-transparent square at the four corners with yellow tick marks; hard stepped silhouette around a round altar), `39_bldg_cluster_wide_zoom3.png`, `21_mid_link_editor_lantern.png`.
- Problem: owner rule says no 90 degree angles; the path is built from square cells with a dotted outline that looks like the build-grid overlay. No blend into the grass. The plaza squares double up with the path and have different alpha.
- Likely cause: art (path painted as full cells) and code (plaza overlay always on, not only in build mode).
- Fix: rounded path tiles (blob autotile) with a soft grass fringe, hide the corner ticks outside build mode, make the plaza a circle under the altar.
- Effort: M.

**P2-6. Overlay text drawn over world: island names, "Select an upgrade", bridge destination**
- Shots: `62_center_coast_north.png` (default-font "Center" at about 830-1090, 860-920, overlapped by a bush), `95_locked_fishing_unlock_stele_normal_zoom.png` ("Fishing" default font, seen at 93 as well), `40_bldg_altar_close.png` ("Select an upgrade" grey-blue on grey path at about 800-1115, 825-860, nearly invisible), `44`/`38` (green "Center" / "Fishing" next to the bridge on dashed lines).
- Problem: texts use the default Unity font, no outline or plate, and sort inconsistently (sometimes under bushes). Not styled like the rest of the HUD.
- Likely cause: code (TextMesh/world text using default font and no sorting order).
- Fix: one world-label style (project font, outline, dark plate, fixed sort order above nodes, below dialogs), hide the island-name at zoom < 3.
- Effort: S.

**P2-7. Awakened Dragon: dithered fringe, mixed density**
- Shot: `41_bldg_awakened_dragon_close.png`, hover version in `32_end_hover_awakened_dragon.png`.
- Problem: a halo of orange/dark noise pixels around the top-left and bottom edge (alpha dither from the image generator) reads as dirt; no soft ground shadow; pixel detail finer than surrounding terrain. Sprite is also much larger than most buildings but has the same selection box as small ones in 32 (box about 280x335 px vs sprite 270x300: OK).
- Likely cause: art (cutout not cleaned), possibly Point-sampled downscale.
- Fix: clean the alpha (remove isolated pixels, quantise to palette), add a 2-3 texel oval shadow under the rock base.
- Effort: S (art edit).

**P2-8. Satellite islets look like perfect discs**
- Shots: `91_locked_view_west_from_center_zoom6.png` (grass ellipse with drum-like wall at about 1075-1240, 185-440), `48_center_overview_fit.png` (the small circle at about 525-575, 390-440).
- Problem: perfect ellipse with hard vertical cliff, violating "natural" coast; hanging vines at the centre look glued on.
- Likely cause: data (islet generator uses an ellipse mask) / art.
- Fix: noise-perturb the islet mask, apply the same curve pipeline as the main island or use a dedicated organic islet sprite set.
- Effort: S.

**P2-9. Fishing south cliff colour jump**
- Shot: `74_fishing_coast_south.png`.
- Problem: saturated teal/blue wall with green moss lip next to pale lavender-grey snow reads as a different island biome (hue jump at the border, "grass cliff" on snow).
- Fix: desaturate the wall toward the ground palette (icy blue-grey, lip colour from the ground), keep the teal only as accents.
- Effort: S.

**P2-10. Resource bushes at the Spirit Tree read as square swatches**
- Shot: `02_firstrun_start_view_hud.png` (leaf bushes at about 600-1100, 660-1010).
- Problem: bush/leaf nodes are drawn as dense rectangular tiles with dithered borders sitting on the dark tree-shadow decal; the grid shape is obvious; the golden gather circle overlaps the tree trunk and the bush row.
- Fix: draw nodes as irregular bush sprites (not full-cell tiles); soften the tree shadow decal edge (hard dither band at its right/bottom).
- Effort: M (art).

### P3 (nice to have)

- P3-1 Sprite reuse: three different buildings share the barrel-house sprite (`38_bldg_cluster_north_close.png`, `33_end_recipe_picker_forge_dense_cluster.png`), so identity relies on labels. Art request for distinct silhouettes.
- P3-2 Output piles are as large as the converter itself (scroll pile in `37` about 280 px wide vs mill 300 px) and have no contact shadow; they overlap the mill base. Shrink stacks or stack them 3-high.
- P3-3 Cost badge on the placed-unbuilt ghost (`28_mid_placed_unbuilt_ghost.png`, "25 / 20" at y about 910) is gold-on-green with no outline; the ghost outline is dashed teal and blends with the grass.
- P3-4 Quarry and tree rows look mechanical: rock decal in an "X" pattern (`42_bldg_storehouses_and_quarry_patch.png`), straight lines of identical trees (`42`, `13_firstrun_center_max_zoom_out.png`). Add jitter in placement.
- P3-5 Faint, semi-transparent grass tufts (about 30-40 percent alpha) in `62`, `63`, `64`, `32` look like rendering glitches rather than art. Raise to full alpha or remove.
- P3-6 Cloud sea: flat pale cloud puffs are sharper and unshaded versus the dithered sea (`98_sky_below_center_to_fishing_zoom6.png`, lower left); the dither shows as a visible mosaic in `62`-`65`. The overview `100_sky_whole_world_overview.png` is an art-review-only zoom and looks like wallpaper at that scale: ignore.
- P3-7 Coast W/E: where the curve becomes vertical the wall collapses into a hairline lip (`64`, top right; `65_center_coast_west.png`, `68_farm_coast_east.png`, `75_fishing_coast_east.png`, `78`, `81`, `84`). This is probably correct perspective, but the change from thick wall to hairline is abrupt; blend the wall height over 3-4 segments.
- P3-8 Bridge/link lines (thin cyan and gold dashes) run over ground, cliffs and sky without fading (`98`, `63` left edge, `42`). Fade by distance or confine to build mode.
- P3-9 `85_locked_farm_veil_on_overview.png`: bottom of the veil has a sawtooth rim as the underside shows through. Art-review framing only, but the same teeth are visible on real approach shots.

## What looked right

- Center terrain, macro patches, Spirit Tree, buildings in the Center cluster (Altar, Forge, Loom, Kiln, pavilion) have consistent palette and scale; building-to-building sort order is correct in all inspected shots (no front/behind errors, nothing z-fighting).
- Center and Farm coast curves read as curves (owner coast4 sign-off holds) in 62, 63, 64, 67; Volcano wall and lava drips (77) are the strongest coast.
- Ghost tint (green/red) and footprint rectangle are readable (45, 46).
- Grove field (79) macro patches and mushroom/bamboo scale are good.

## Top 10 (suggested order of work)

1. P1-1 Veil seams and rim leak (S-M, most visible to new players).
2. P1-3 Overlapping world labels in dense clusters (M).
3. P1-2 Heavenly Gate captions unreadable (S, quick win).
4. P1-4 Wood rack and Spirit Bridge placeholder rectangles (interim tint S, art L).
5. P1-5 Integer pixel scale / mixed texel density (verify first, M).
6. P2-1 Cliff wall tile repetition and joints (M).
7. P2-2 Lip steps on Grove/Fishing/Celestial (M).
8. P2-5 Path and plaza 90 degree stairs and debug-looking outline (M).
9. P2-6 One world-label style (S).
10. P2-4 Macro ground repetition on Mine/Celestial/Volcano/pond (M).

Next steps for the orchestrator: P1-2, P2-6, P2-7, P2-8, P2-9 are small and can go straight to a Sonnet fix pass; P1-5 needs a 1:1 crop check first; the rest need art-director briefs. No keys were accepted, rejected or re-integrated by this review (no Drive deliveries involved).
