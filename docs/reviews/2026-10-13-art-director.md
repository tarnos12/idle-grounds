# Art director review, 2026-10-13

Scope: the 100-shot tour (`scratchpad/tour/`, INDEX.md, captured 2026-10-12 on `bg/tour`, 1920x1080, real Play mode). This is a read-only review: no game files were changed. It was checked against ART-SPEC §1–3 and §3.6–3.7, ADR 0005, the art-direction guide and every `owner-picks-*.json`. Companion reviews from the same day: `2026-10-13-ingame-art.md` (art acceptance), `2026-10-13-ui-ux.md` and `2026-10-13-ui-qa.md`. Where a finding overlaps one of those, this review gives the art-direction rule and leaves the work item to the other review.

Measurements were taken from the PNGs: pixel colour samples, run lengths and counts of distinct colours. Zoom 2 (the start zoom) shows 35 cells across 1920 px, which is **1.71 screen px per texel**. Zoom 1 is 3.43 px per texel.

## Verdict in one paragraph

The **buildings are the best thing in the game**. Altar, Paper Mill, Storehouse, Loom, Forge, Infusion Array and the Spirit Tree are rich and consistent 3/4 pixel art in one hand (`40`, `37`, `38`). The south cliff and underside of the Center also work (`63`, `65`).

The **world they stand on looks unfinished**:
- Islands are flat, saturated single-colour fields (Grove and Fishing have 2 distinct colours in a 400x300 px ground sample).
- The pixels are not on an integer grid.
- All text uses the TMP default sans (LiberationSans) and the UI still has emoji.
- World-space overlays (status strips, counters, the fuel rack, dashed vector lines) sit on top of the art at full contrast.

The value hierarchy is inverted. The ground is the brightest, most saturated surface on screen (Center fill `#86B34A`) and the props and buildings must fight it. Fixing the **ground, the pixel grid and the type** improves every shot without one new building.

---

## Findings

Format: **shots** · what is wrong · fix (code/data vs art, with exact keys) · effort S (<1 h) / M (hours) / L (day+ or artist round) · owner decision?

### P1 — hurts the first impression or is clearly wrong

**P1-1. Pixels are not on an integer grid (mixels everywhere).**
- Shots: all world shots, measured on `14`, `41`.
- Wrong: at zoom 2 a texel is 1.71 screen px. A row through a building gives runs of `3,2,3,2,2,2,1,2,…` px, so neighbouring texels render 1, 2 or 3 px wide. At zoom 1 the runs are `3,4,3,…` (3.43 px per texel). Continuous zoom (0.25 steps, smoothed) means no stop is integer. This breaks the core rule in ART-SPEC §2.1 and ADR 0005 ("integer scale only"). On screen, outlines shimmer and become uneven, and 1-px details like grass flecks, ink outlines and dithering turn to mush. The flat look of the ground is partly this.
- Fix (code): snap the camera to integer screen-px per texel. Use ortho size = `Screen.height / (2 * 32 * k)` with k ∈ {4, 3, 2, 1}, which at 1080p gives 15 / 20 / 30 / 60 cells across. One option is URP Pixel Perfect Camera (upscale RT off, crop frame off), recomputed per zoom stop. The zoom animation tweens between stops, then lands exactly on one. Pan positions snap to 1/k texel. Beyond 60 cells, see the owner question.
- Effort: M. Owner decision: only the zoom range (Q-AD1).

**P1-2. The ground is flat paint: islands read as coloured blobs, not places.**
- Shots: `02`, `62`, `63`, `65`, `66`, `69`, `72`, `74`, `79`, `80`, `82`, `100`, `47`–`60`.
- Wrong: Center is one flat `#86B34A` (P14) with 1-px specks; 4 sample points across `02` give the same colour. Distinct colours in a 400x300 px ground sample: Grove **2** (`80`), Fishing **2** (`72`), Celestial 9, Center south 5. Farm is flat `#D9A45C` (P23).
  - At zoom 6 and in the overview (`47`, `100`) each island is a single colour fill with a rounded-square outline, like a board game.
  - Where texture exists, it is a visible repeat: Mine's 32-px chain-mail checker (`69`), the dash grid on Celestial (`82`), the lily pads on a grid in the Fishing pond (`72`), and Volcano's regular ember dots (`76`, `77`).
  - Center's macro patches are 4 cookie-cutter clover shapes stamped at one value step and tiled (`62`, `63`), which reads as wallpaper.
- Fix:
  - Code (S–M):
    - Pick per cell among the 3 fill variants with a seeded hash, no repeat of the same variant in a 2x2 block.
    - Place macro patches with random flip X/Y (no rotation: pixel rule), keep ≥ 1 cell between instances, and use 2 densities (sparse at the coast, denser near the build zone).
    - Add a large-scale value-variation layer: 3–6-cell blobs, one palette step lighter or darker, dithered edge, from a noise mask. These are drawn from the existing macro art, so no new colour.
  - Art (L): the non-Center ground is a generated placeholder because the Farm, Fishing and Grove blob v1 sets were rejected. Request new **fill** sets first, which are cheaper than full blobs and fix most of the look: `island_farm_ground_fill_32x32_3f.png`, `island_fishing_ground_fill_32x32_3f.png`, `island_grove_ground_fill_32x32_3f.png`, `island_mine_ground_fill_32x32_3f.png` (redo: calmer, no checker), `island_volcano_ground_fill_32x32_3f.png` (redo: irregular plates, embers not on a grid), `island_celestial_ground_fill_32x32_3f.png` (redo: veined marble, no dash grid).
  - Brief for all of these: "3 seamless variants that tile with each other; 3–4 tones from the biome palette; detail clusters of 2–4 px, never a regular grid; nothing that lines up on the 32-px cell edge". Plus `island_center_macro_96x96_4f.png` **v2**: 4 clearly different organic shapes (long drift, ring, kidney, scatter), not 4 clover crosses.
- Effort: M code, L art. Owner decision: no (ART-SPEC §3.6 "ground richness" already asks for this).

**P1-3. The value and saturation hierarchy is inverted: the ground outshouts the buildings.**
- Shots: `02`, `14`, `18`, `62`, `100`.
- Wrong: the Center fill P14 `#86B34A` is a high-chroma lime that covers about 70–80% of the start screen. It is lighter than most building bodies (wood P21/P22, roofs P03/P04) and more saturated than the sky, so the eye goes to empty grass. Other islands use the far ends of the value range as their *fill*: Celestial `#E8ECF3` (P07, near white) and Volcano `#1B1F2B` (P02, near black). On those, dark props on dark ground (Volcano obsidian, `76`) and pale props on pale ground (Celestial clouds, `82`) lose contrast. Grove herbs on a flat P09 teal also lose contrast (`79`).
- Fix: set a value band per layer (see the style addendum). Ground fill sits in the **middle** value band with low to medium chroma. Props are +1 contrast step. Buildings and interactables are full contrast. Concretely:
  - Center's base is mostly P13 `#4F7A2F` with P14 as the highlight tone, not the reverse.
  - Celestial's base is P06 `#B7BCCD` with P07 only as highlights.
  - Volcano's base is P03 `#2E3345` with P02 cracks.
  - Grove mixes P13 and P09.
  - Code can do this today, as a palette remap or tint of the fill tiles in the tile builder. Art follows with the fills in P1-2.
- Effort: S (code remap), then art. Owner decision: **yes, Q-AD2** (taste: how bright the world is).

**P1-4. Emoji and the default font are still in the shipped UI and world text.**
- Shots: `01`, `02`, `08`, `14`, `15`, `62`, `94`, `18`, `38`, `36`.
- Wrong: every text is LiberationSans SDF, the TMP default (the only font in the project). That includes the island name labels (`62`: a 60-px translucent anti-aliased "Center" lying on the grass), the counters, converter strips, buttons and modals. Smooth emoji glyphs remain:
  - the sprout in the Welcome modal (`01`)
  - the dart-board "Next milestone" icon, the hand ✋ and the 📊 chart in the bottom bar
  - inline 🌳 and 🌿 in quest text (`02`), the lock on the stele (`94`), and the scroll in "Quest 1/17"
  
  These are vector-smooth and anti-aliased next to 1-px-outlined pixel art, which is the clearest "prototype" signal on screen. ART-SPEC §5.4 already names the font: Pixelify Sans for body, Silkscreen for titles, both OFL.
- Fix:
  - Code (M): import Pixelify Sans as a TMP asset (bitmap or Raster mode, Point filter, sizes 8/16/24 on the integer grid) and set it as the TMP default plus every prefab font. Build the TMP sprite asset from the **real `ui_*` and `item_*` pixel icons** instead of `emoji_atlas.png`, and map the emoji codepoints in `UiText` to those sprites.
  - Art: confirm or request the HUD keys in §4.5.4: `ui_hand`, `ui_stats`, `ui_build`, `ui_demolish`, `ui_quests`, `ui_target`, `ui_lock`, `ui_unlock`, plus new `ui_sound`, `ui_motion`, `ui_help`, `ui_reset`, `ui_welcome_sprout` (32x32 each). The UI/UX review keeps the list.
- Effort: M code, M art. Owner decision: covered by UI/UX Q3 (I recommend its option E: real icons plus one pixel font).

**P1-5. World overlays sit on top of the art at full contrast, in the wrong font, with vector lines.**
- Shots: `14`, `18`, `37`, `38`, `21`, `25`, `40`, `44`.
- Wrong:
  - Converter status strips ("20/1 20/2 → ×10"), stock counters ("0/60", "137/200", "0/200") and status text ("No fuel") are drawn in the world as anti-aliased sans. They scale with zoom, so they are ~28 px tall at zoom 1 (`37`). Saturated green, red and gold numbers sit on a translucent box.
  - In the dense cluster they overprint each other and the roofs: "0/3" and "12/2" collide in `38`, and the "Fishing" label crosses the Spirit Bridge.
  - Dashed link lines (cyan and gold), gather circles (the gold ring around the Spirit Tree in `02`) and the white hover rectangle (`18`) are thin anti-aliased vectors, not pixel lines.
  - The plaza has gold dashed corner markers (`40`) that read as editor gizmos.
- Fix (code, M):
  - One overlay style: dark P01 pill at 80%, a 1-px P04 border and the pixel font.
  - **Constant screen size** at integer scale (don't scale with zoom). LOD: full strip at zoom 1–2, icon and number only at 3–4, hidden at 5+ unless hovered.
  - Collision: stack or offset labels that overlap and drop to icon-only.
  - Render lines and circles as 1-texel pixel lines snapped to the texel grid, with dashes in texel units (2 on, 2 off) and palette colours only (P28 link, P24 fuel, P18 blocked).
  - Show the plaza corner markers only in build mode.
- Effort: M. Owner decision: no (UI/UX style rules 7–8 agree).

**P1-6. The fuel rack and the Spirit Bridge read as placeholder rectangles in the world.**
- Shots: `14`, `18`, `37`, `38`, `44`.
- Wrong:
  - The fuel rack is an opaque black 96x64 box with a bright orange grid, about 165x110 px at zoom 2. It is the highest-contrast object on the start cluster, so the Kiln looks like it has a UI panel stuck to it. Empty, it is a black void with a red "No fuel" above it. With logs (`37`) the logs are drawn flat-on inside cells, not in 3/4.
  - The Spirit Bridge sits on a dark grey opaque rectangle (`38`, `44`) with a translucent oval counter on it.
  
  Both look unfinished. The in-game art review files the bridge as P1-4.
- Fix: art redo `ui_fuel_rack_96x64.png` → new world prop **`fix_fuel_rack_96x64.png`** with a full brief:
  - A 3/4 wooden log rack seen front-above: two posts, a slanted roof shingle and 2x3 cubbies.
  - Empty cubbies are dark P20 wood, not black. Filled cubbies get logs and charcoal drawn in 3/4 by code-composited per-fuel sprites `fx_rack_fuel_<wood|bamboo|charcoal|firestone>_32x32.png`.
  - Selective outline, no orange glow except on firestone.
  
  For the bridge: check why the sprite has a non-transparent background (likely import alpha or a backing quad in `BridgeFaceView`) and remove the backing quad. Interim code: lower the rack's alpha in the empty state and drop the black fill.
- Effort: S code, M art. Owner decision: **yes, Q-AD3** (diegetic woodpile vs UI rack).

**P1-7. The locked-island veil looks like camouflage cloth with tile seams.**
- Shots: `85`–`95`, especially `94` and `91`.
- Wrong:
  - Seams are clearly visible every 438 px: vertical at about x = 205 / 645 / 1085 / 1525 and horizontal at about y = 265 / 700 in `94`. That is the 256-texel tile x 1.71.
  - The pattern is orange, purple and grey camo blotches, nothing like the spec's "dense swirling pale-lilac-grey fog". The orange blobs read as Farm sand stains bleeding through.
  - It is the first thing a player sees when panning from the Center at zoom 6 (`91`): half the screen.
  - The veil also keeps the island's rounded-square outline exactly, so it looks like a painted-over island rather than mist.
- Fix:
  - Code (S): offset the tiling per layer, overlap two layers at non-commensurate periods (256 and 192 texels), stop sampling the island ground under it (or desaturate it 80%), and feather the outline 2–3 cells into the sky with `island_veil_edge_64x64.png`.
  - Art (M): redo `island_veil_mist_256x256_4f.png` v2 with this brief:
    - Palette P06/P07 with P32 shadows only.
    - 4 alpha steps, swirl shapes 16–48 px, no warm colours.
    - Edges wrap seamlessly in both axes; check it by tiling 3x3 before delivery.
  
  Same item as in-game art P1-1; this adds the palette brief.
- Effort: S code, M art. Owner decision: no.

### P2 — visible polish problems

**P2-1. Projection clash: some pieces are drawn isometric (2:1, seen from a corner), not 3/4 front.**
- Shots: `37` (Kiln, Workbench), `14`, `38` (Herb Garden), `66` (rice paddy nodes).
- Wrong:
  - The Kiln is a long dragon kiln receding diagonally to the upper right.
  - The Workbench is seen from its corner, with a diamond footprint.
  - The Herb Garden and the crop/paddy plots are rhombuses.
  
  Every other building (Altar, Storehouse, Paper Mill, Loom, Forge, Pavilion) shows a front face plus the roof top over a rectangular footprint. On a square grid the diamonds don't fill their cells, so they look rotated and sit "off" the grid. This breaks guide rule 1 ("3/4 view for everything").
- Fix: art redraws, same keys, same canvases:
  - `bld_workbench.png` (96x128): front-facing bench, roof awning seen from front-above.
  - `bld_kiln.png` (96x192): an upright brick kiln with the chimney at the back, mouth facing south.
  - `bld_herb_garden.png` (96x128): a rectangular raised bed seen front-above.
  - `node_crop` and its paddy variants: rectangular bunded plot with front bank visible.
  
  Each brief cites `bld_paper_mill` / `bld_storehouse` as the reference for projection.
- Effort: L (artist round). Owner decision: **yes, Q-AD4** (only priority versus the §3.7 batch).

**P2-2. Nothing has a contact shadow, so props and buildings look pasted on.**
- Shots: `02`, `14`, `18`, `40`, `44`, `66`, `69`.
- Wrong: ART-SPEC §1.1 says shadows fall down-right, one tone darker. No building, node or tree has a ground shadow. The only shadow in the game is the huge dithered dark patch under the Spirit Tree (`02`), about 600x400 px, which reads as a stain rather than shade.
- Fix: code adds a shared shadow under every building, node, fixture and NPC: an ellipse a little narrower than the footprint, offset +2 texels right and +1 down, ground colour one palette step darker, 50% checker dither, binary alpha. Art key **`fx_contact_shadow_32x16_3f.png`** (sizes S/M/L as frames; 1x1, 2x2 and 3x3 footprints scale by frame, never by transform). Shrink the Spirit Tree shade to about 1.5x the canopy.
- Effort: S code, S art. Owner decision: no.

**P2-3. You cannot tell what is interactive from what is decoration.**
- Shots: `02`, `62`, `63`, `74`, `79`, `82`.
- Wrong: decorative scatter (pebble clusters, clover tufts, white flowers, grass tufts) uses the same ink outline, saturation and size as harvestable nodes (stones, bushes, herb bushes). In `02` the stone *node* and the pebble *decor* are both grey rocks with ink outlines, about 40 px. In `82` cloud macro patches and star-dust decor compete with star-rock nodes.
- Fix: code and data. Decor gets no ink outline (or a ground-tinted outline), is pulled one value step toward the ground, and is limited to ≤ 16x16 visible texels. Interactables keep the ink outline plus the contact shadow (P2-2) and get a 1-texel P07 rim on hover. Art follow-up: `island_<biome>_flowers_32x32_4f` v2 without ink outline.
- Effort: S code, M art. Owner decision: no.

**P2-3b. Item drops on the ground are scaled to a fraction (mixels).**
- Shot: `37` (paper scrolls, iron bars).
- Wrong: ART-SPEC says items show at about 20 px on the ground, a 0.6x scale. That breaks the integer rule and the drops look smeary and outsized as a crowd.
- Fix: art keys `item_<key>_drop_16x16.png`, or a code rule to show drops at 1x with a density cap. Recommend a generated 16x16 nearest downsample, then hand cleanup by the artist for the 12 most common items.
- Effort: M. Owner decision: no.

**P2-4. The south cliff walls read as barcodes on the non-Center biomes.**
- Shots: `74` (Fishing), `77` (Volcano), `83` (Celestial), `80` (Grove), against `63` (Center, fine).
- Wrong: each 32-px column is a vertical frame repeated along the wall. On Volcano, Celestial and Fishing that becomes a regular picket-fence stripe of verticals (lava cracks every 32 px, violet crystals every 32 px), with no horizontal strata to tie the columns together. Laid along the new curves it is worse, because each column tilts on its own.
- Fix:
  - Code (S): never repeat the same frame twice in a row; pick frames from a run-length noise.
  - Art: these keys are already open in the Delivery log: `island_<biome>_cliffwall_round_32x96_4f` plus the cliff-wall seam fix. Add to the brief: "continuous horizontal strata that run through frame edges at fixed heights (rows 12, 30, 52); vertical accents (cracks, crystals, vines) on at most 1 of 3 frames".
- Effort: S code, M art. Owner decision: no (follows the coast feedback).

**P2-5. Paths and plaza are right-angled rectangles, and the satellite islets are perfect circles.**
- Shots: `14`, `18`, `40` (blocky stair-step paths with a red-brown 1-px outline), `91` (round grass "coin" islet).
- Wrong: the owner's binding coast rule, "we can't have 90 degree angles" and "round the corners into curves", now applies to every coast, but the paths still turn in hard right angles with rectangular notches. The islet in `91` is a perfect disc, which reads as geometry rather than nature.
- Fix:
  - Code: path layout rounds corners with diagonal cells, using the rounded-corner frames the requested `island_center_path_32x32_15f.png` v2 already allows. Seed the islet outline from the same noise used for the coasts, in 3–6 cell lobes.
  - Art: add to the path v2 brief "outer corners rounded by 1/4 cell, inner corners filled with moss; no 1-px outline in red-brown, use P20 at the edge only on the south side".
- Effort: M. Owner decision: no (the coast rule extended; add it to the guide).

**P2-6. Four frame languages in the UI, and the quest panel is the loudest thing on screen.**
- Shots: `01`, `02`, `08`, `12`, `15`, `29`, `36`.
- Wrong: there are four frame styles:
  - parchment with torn edges (Welcome, Help)
  - an ornate gold and red frame (quest panel, about 375x570 px, the most saturated element of the start screen)
  - thin jade lines on dark (Altar tree, build strip)
  - jade dotted buttons in the bottom bar
  
  Primary buttons are red in one place, jade outline in another and red-on-gold in a third (`01` Continue, `02` Claim, `12` Claim!).
- Fix: UI/UX review Q3 and its style rules 1–3. The art-direction rule: one frame family (jade on night ink) everywhere, gold only as an accent of at most 2 px, and no parchment. Recolour `ui_questpanel_frame_64x64.png` to jade/ink with a single cinnabar seal.
- Effort: M. Owner decision: UI/UX Q3.

**P2-7. The island name labels are the clearest default-font leftover.**
- Shots: `62`, plus the tiny labels at the top of `47`–`60`.
- Wrong: a translucent white LiberationSans "Center" about 60 px tall at zoom 2, lying on the grass over a bush, with no outline. At zoom 6 it shrinks to an unreadable 10 px speck.
- Fix: code, S for option B in Q-AD5. Art for option C: `fix_island_signpost_64x96.png` (carved stone marker with a blank plaque; the name is rendered in the pixel font on the plaque) plus `ui_island_banner_96x24.png` (9-slice).
- Effort: S–M. Owner decision: **yes, Q-AD5**.

**P2-8. Fishing reads as a snowfield.**
- Shots: `72`, `73`, `74`, `44`, `54`.
- Wrong: the ground is a flat `#B7BCCD` (P06) with blue specks, so "snow" in INDEX and in any player's eye. ART-SPEC §1.3 gives Fishing P26–P28 water plus P23/P14 shore. The rejected macros say the intent was "cool shingle". Neither shows. Lotus pads and reeds float on a pale grey void.
- Fix: decide the identity first (Q-AD6), then brief `island_fishing_ground_fill_32x32_3f.png` and `island_fishing_macro_96x96_4f.png` v4 to match.
- Effort: M art. Owner decision: **yes, Q-AD6**.

**P2-9. The south underside reads as a sawtooth fringe at a distance.**
- Shots: `52`, `56`, `85`, `100`, against `63` and `98` (fine close up).
- Wrong: at zoom ≥ 4 the hanging rocks shrink into a row of identical small triangles under every south coast, like a paper fringe.
- Fix: code places fewer and larger `island_underside_rock_3q` pieces with varied widths and lengths (the ART-SPEC §3.2 "combine well" intent), and lets one or two long pieces per island hang 1.5x deeper. Art: no change.
- Effort: S. Owner decision: no.

### P3 — nice to have

**P3-1. Cloud-sea tile repetition shows past the game's zoom limit, and the sky is far off-palette.**
- Shots: `52`, `56`, `85`–`90`, `100` (all fit/ortho, beyond the game's limit); `96` and `91` at real zooms look good.
- Wrong: a diagonal wallpaper repeat at far framing. A sample of the sky has 3,927 distinct colours because alpha-blended layers fall between palette entries.
- Fix: if the B or D option of Q-AD1 adds a far zoom, use two layers at non-commensurate periods plus per-layer random offset, and lower cloud contrast with zoom. Otherwise only marketing shots are affected. Palette: accept the blend, or bake the layers in palette. Decide when the new `sky_cloudsea_*` set lands.
- Effort: S. Owner decision: no.

**P3-2. The sky mood is a stormy dusk, while the tone in the brief is "calm, serene, dawn".**
- Shots: `96`, `62`, `65`, `91`.
- The cloud sea at zoom 2 is beautifully drawn but dark slate teal (`#34475A`–`#516678`). It looks good behind lime grass, but it sets a night or storm mood under every island. Owner decision: **Q-AD7**. Art keys if changed: `sky_cloudsea_far_512x512.png`, `sky_cloudsea_mid_512x512.png`, `sky_depth_512x512.png` (dawn variant).

**P3-3.** The Fishing pond lily pads repeat on the 32-px grid (`72`). Code: scatter the lily overlay at random offsets instead of per tile.

**P3-4.** The Altar plaza's gold dashed corner markers and the dashed yellow radius lines through the grass (`40`) should show only on hover or in build mode.

**P3-5.** The version badge "v53" is always visible bottom-left (`02` and others). Move it into Help or Stats, or show it only in dev builds.

**P3-6.** The dense late cluster (`38`, `33`) has no breathing room: buildings stand edge to edge on bright grass. This is data and layout: give the starter layout 1-cell grass or plaza gutters with flower borders so a cluster reads as a "sect courtyard".

---

## Draft Hub questions (for the orchestrator to add to `docs/Design/questions.json`; pictures from the tour shots named)

**Q-AD1 `ag_zoom_stops` — Crisp pixels mean fixed zoom steps. Which zoom range?** (pictures: `14` with a crop at 2 px per texel, `13`)
- A: Four crisp steps only: 15, 20, 30 and 60 cells across. The farthest view gets closer than today's 105 cells.
- B: The four crisp steps plus one far "map" step at 120 cells. That step is half size, so fine detail may shimmer slightly. *(recommended)*
- C: Keep today's smooth zoom. Pixels stay uneven and soft.
- D (maximal): B, plus a separate drawn world-map view with island icons and Sky Paths for the far zoom, instead of shrinking the world.

**Q-AD2 `ag_ground_key` — How bright and colourful should the island ground be?** (pictures: `02`, `100`, plus a mock with Center fill remapped P14→P13)
- A: Keep it as is (bright lime grass, near-white Celestial, near-black Volcano).
- B: One step calmer: darker, less saturated ground on every island, so buildings and resources stand out. *(recommended)*
- C: B, plus a soft warm colour grade over the whole world (slight vignette, warm light from the upper left).
- D (maximal): C, plus each island gets its own light mood (misty Grove, hazy Volcano, glowing Celestial) as a subtle overlay.

**Q-AD3 `ag_fuel_rack` — How should a burner's fuel rack look?** (picture: `37`, `18`)
- A: Keep the black box with the orange grid.
- B: Same rack, but softer: dark wood instead of black, and it only shows strongly when you hover or fuel is low.
- C: Replace it with a drawn wooden log rack standing next to the burner; the logs you feed appear on it. *(recommended)*
- D (maximal): C, plus the burner's fire and smoke grow and shrink with the fuel left.

**Q-AD4 `ag_iso_redraw` — Four pieces are drawn from a corner (diamond shape) instead of from the front like the rest: Kiln, Workbench, Herb Garden, rice plots. When should they be redrawn?** (pictures: `37`, `38`, `66`)
- A: Later, after the new-island batch.
- B: Now, before the new-island batch, so the 19 new buildings copy the right angle. *(recommended)*
- C: Only the Kiln and Workbench now (the starter area), the rest later.
- D (maximal): B, plus a full check of all 26 existing buildings against one reference sheet.

**Q-AD5 `ag_island_names` — How should island names show in the world?** (picture: `62`)
- A: Remove them from the world (the name shows only in the bottom-bar area pill).
- B: Keep them, restyled: pixel font in a small dark banner, the same size at every zoom, hidden when zoomed far out. *(recommended for now)*
- C: A carved stone signpost near each island's coast with the name on it.
- D (maximal): C, plus a short name banner that slides in at the top of the screen when you pan onto an island.

**Q-AD6 `ag_fishing_ground` — Fishing Island currently looks like snow. What should its ground be?** (pictures: `72`, `74`)
- A: Cool pebble shingle (grey-blue stones, wet dark patches), as first intended.
- B: Mossy lake shore: green moss and wet sand around the pond, as in the original brief. *(recommended)*
- C: Make it a snowy northern fishing island on purpose (snow props, ice at the pond edge).
- D (maximal): B, plus shallow water channels and small wooden jetties across the island.

**Q-AD7 `ag_sky_mood` — The sea of clouds under the islands is a dark, stormy blue. Keep it?** (pictures: `96`, `65`)
- A: Keep the dark dusk sky.
- B: Lighter dawn: pale white-cyan cloud tops with soft gold light, calmer and brighter. *(recommended, matches "calm, serene")*
- C: Somewhere between: lighten the clouds one step, keep the blue.
- D (maximal): a slow day cycle (dawn → day → dusk) on the sky only.

---

## Top 10 fixes in order

1. **Integer pixel scale.** Snap camera zoom stops to whole screen-px per texel (P1-1). M, code; Q-AD1 only for the far range. Every later fix looks better once this lands.
2. **Ground value key.** Remap the fill tiles to a mid value with lower chroma (P1-3). S, code; Q-AD2.
3. **Ground variety by code.** Seeded fill variants, flipped and spaced macro patches, and a large-scale value-variation layer (P1-2 code half). S–M.
4. **One pixel font plus real icons.** Pixelify Sans as the TMP default and a sprite asset from the `ui_*`/`item_*` art; this removes the emoji and LiberationSans (P1-4). M; UI/UX Q3.
5. **World overlay restyle.** Pill style, constant screen size, zoom LOD, collision and pixel-snapped palette lines (P1-5). M.
6. **Contact shadows plus the decor/interactable split** (P2-2, P2-3). S code, plus `fx_contact_shadow_32x16_3f`.
7. **Fuel rack and Spirit Bridge backing.** Remove the opaque backings now; brief `fix_fuel_rack_96x64` (P1-6). S + M art; Q-AD3.
8. **Veil.** Seam offset, dual layers and feathered edge in code, plus the `island_veil_mist_256x256_4f` v2 brief (P1-7). S + M art.
9. **Art round "ground and coasts":** the six biome `*_ground_fill_32x32_3f` sets, `island_center_macro_96x96_4f` v2, `cliffwall_round` with the strata rule, and path v2 with rounded corners (P1-2, P2-4, P2-5). L art.
10. **Art round "projection":** redraw the Kiln, Workbench, Herb Garden and paddy in 3/4 front (P2-1). L art; Q-AD4.

Items 1–8 need **no new art** to land a visible improvement. Items 9–10 are briefs for the artist.

---

## Style rules addendum (proposed for `docs/guides/art-direction.md` → "Rules to enforce")

8. **Integer pixels, always.** Every zoom stop renders a whole number of screen px per texel (1, 2, 3 or 4). Non-integer stops are allowed only during a zoom tween. No sprite is scaled by a non-integer factor; a smaller variant is drawn instead (e.g. 16x16 ground drops).
9. **Value bands per layer**, darkest or least saturated first: sky/cloud sea → ground fill (mid value, chroma ≤ the biome's "grass mid" tone, never P07 or P01/P02 as the main fill) → decor (±1 step from the ground, no ink outline) → interactables and nodes (ink outline + contact shadow) → buildings (full palette) → UI (framed, on night ink). A layer may never be brighter *and* more saturated than the layer above it.
10. **No flat ground.** Any 3x3-cell window of ground at zoom 2 shows at least 3 tones from the biome palette, and no detail pattern repeats on the 32-px grid. Identical macro shapes never touch.
11. **One projection test** for every building, node and fixture: rectangular footprint, a visible front (south) face and a visible top; no diamond footprints and no corner-on views. Check it beside `bld_paper_mill` before acceptance.
12. **Grounded objects.** Everything standing on the ground gets the shared contact shadow: down-right, one palette step darker, dithered.
13. **Interactive reads at a glance.** Interactables have an ink outline, a contact shadow and a hover rim; decoration has none of the three.
14. **No emoji and one type family.** World and UI text uses the pixel font at integer sizes (8/16/24) with a 1-px P01 outline. Icons come from the pixel `ui_*`/`item_*` set only.
15. **World overlays are pixel art too.** Lines, rings and dashes are 1 texel wide on the texel grid, in palette colours. Labels keep a constant screen size, follow the zoom LOD and never overlap the art they describe.
16. **Natural shapes everywhere, not only coasts.** The owner's "no 90-degree angles" applies to paths, plazas, islets, veils and zone patches as well. Corners are rounded or diagonal, with steps of 2–3 cells at most.
17. **Repetition check before acceptance.** Any tiling art (fill, sky, veil, water, cliff) is tiled 4x4 at zoom 2 and at the farthest zoom. A visible seam, grid or stripe rejects it.

---

Files: this review only (`docs/reviews/2026-10-13-art-director.md`). No plan task set, no Hub questions posted and no commit, as asked (read-only). The orchestrator should add Q-AD1 to Q-AD7 to the Hub and turn the Top 10 into plan tasks.
