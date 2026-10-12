# Visual pass plan, 2026-10-13

Merged from the four reviews of the 100-shot tour (`bg/tour`, 1920x1080, v53): `2026-10-13-art-director.md` (**AD**), `2026-10-13-ingame-art.md` (**IA**), `2026-10-13-ui-ux.md` (**UX**), `2026-10-13-ui-qa.md` (**QA**). Shot numbers are the tour file prefixes (`14` = `14_mid_hud_overview.png`). Finding refs: AD `P1-1`, IA `P1-1`, UX `#5`, QA `#12`.

Rules used for the split:
- **Batch A** = no owner input needed (bugs, wording, layering, readability, interim fixes). Ordered by impact/effort, best first.
- **Batch B** = needs an owner answer; each links to a Hub question id in `docs/Design/questions.json` (group "Visual pass", 8 questions).
- **Batch C** = new art keys. Specs are one line each, ready for ART-SPEC. Rows marked *gated* wait for the linked answer.
- Commitments honoured: `owner-picks-2026-10-12*.json` (ascension removed, Shrine + Requests, right-click feeding only, curved coasts signed off) and ADR 0007. The unlock steles are being replaced by the Shrine, so **no polish on steles** (only the wording and label fixes in VP-02 and VP-07).
- Scrim: UX Q5 is settled in A (flat 45% black, no art), so it is not a Hub question.
- Integer pixel zoom is in **B** (VP-28) because it changes the farthest zoom from 105 to 60 cells (or 120 as a half-size step); that is a noticeable change to the zoom range.
- Roles follow `.claude/rules/personal-workflow.md`: Sonnet for mechanical code, strings and tests; Haiku for chores; Opus only where a wrong answer ships a bug (camera snapping, collision/LOD layout).

---

## Batch A — no owner input

Effort S < 1 h, M = hours, L = day+. "Owner?" is n for every A item.

### A1. Bugs and wrong text (do first, all S)

**VP-01 Confirm button text overflows the button**
- Raised by: QA #1; UX 17. Shots `24`, `35`.
- Fix: OK/Cancel get min width 200 plus preferred width of the label (flexible layout element), or the label wraps. `MetaBuilder.BuildConfirmPrefab`, `ConfirmDialogView`.
- Code. S. Sonnet.

**VP-02 Stale ascension, steles and "Islands 1/7" wording**
- Raised by: UX 1, 23. Shots `08`, `09`, `24`, `35`, `36`, `94`, `95`.
- Fix: Stats rows become "Islands 1/11", "Requests 0/33", "Tributes paid", "Ending reached" (drop "Ascended to Immortality"). Rewrite the Help sections "Buildings" (steles) and add "Shrine" and "Requests" sections (text only, the tab redesign is VP-30). Veil copy points to the Shrine ("Pay the Tribute at the Sky-Bridge Shrine"). Keep the Heavenly Gate title line the owner picked ("Ascend to Immortality" plays the ending); remove every other "ascend" use. Writer owns the strings; stay inside `CONTEXT.md` terms.
- Code (strings). S. Sonnet (Writer).

**VP-03 Playtime shows "497734h 2m" on the ending card**
- Raised by: QA #4; UX 17. Shot `36`.
- Fix: clamp and format playtime as "h m" (negative or implausible shows "--"); check the clock domain of `runner.SimNow - s.stats.started` in `DragonDialogView` ~line 100 and `StatsPanelView` ~line 72. **Verify live first**: if the tour-save writer left `started` at 0 it is a tooling artefact, but the clamp stays.
- Code. S. Sonnet. **Verify live.**

**VP-04 Player-visible "Debug" button in the Altar header**
- Raised by: QA #8; UX 9. Shots `12`, `17`, `31`.
- Fix: show only with a dev flag (editor or development build). `ProgressionBuilder.cs` ~253, `UpgradeTreeView.cs` ~67.
- Code. S. Haiku.

**VP-05 Modal scrim and layering (quest panel peeks out, modal under the bar)**
- Raised by: UX 5; QA #5, #6, #25. Shots `01`, `08`, `09`, `12`, `17`, `24`, `29`, `31`.
- Fix: one flat 45% black full-screen dim (plain colour `Image`, no art) behind every modal, the same on Welcome, Help, Confirm and Dragon dialogs; hide or fade the quest panel and the hand chip while a modal is open; Altar window ends above the bottom bar (height minus bar), centred.
- Code. S. Sonnet. **Verify live**: the scrim may already work in play (QA #25 suspects a capture artefact).

**VP-06 Heavenly Gate captions unreadable and overlapping**
- Raised by: IA P1-2; QA #2; UX 3. Shots `34`, `35`, `43`.
- Fix: one tag on a dark pill with title and hint on two lines, never overlapping the hover name tag; no gold-on-pale text. Uses the shared style from VP-07 once it exists; the quick fix can ship first.
- Code. S. Sonnet.

### A2. Readability and overlap

**VP-07 One world-label style (dark plate, outline, constant size)**
- Raised by: AD P1-5, P2-7; IA P2-6, P3-3; QA #2, #11, #16, #17, #35; UX 3. Shots `62`, `18`, `19`, `28`, `33`, `34`, `40`, `94`.
- Fix: one `WorldLabel` style used by: island names, status text ("No fuel", "Needs ...", "Output pile full", "Select an upgrade"), the bridge destination ("<- Fishing"), the placed-ghost cost, the stele label (add have/need) and the Gate. Dark P01 pill at 80%, 1 px outline, the existing UI font (the pixel font follows VP-30), on-screen size constant and clamped (0.75x-1.5x), fixed sort order above nodes and below dialogs. Island names: hidden at zoom 5+, shown as a small banner at 1-4 (the signpost idea is not pursued; Hub question dropped as non-essential). Contrast target 4.5:1.
- Code. M. Sonnet.

**VP-08 Hand chip covers panels, Build cards and ghosts**
- Raised by: QA #12; UX 14, 22. Shots `15`, `21`, `22`, `23`, `27`, `31`, `33`, `35`.
- Fix: hide the chip while the pointer is over a panel or modal, show only when the hand is non-empty, clamp to the screen, flip near edges, park above bottom docks.
- Code. S. Sonnet. **Verify live** (the tour drives the chip with a debug cursor).

**VP-09 Recipe picker shows icons only (no names), popup too wide for one cell**
- Raised by: QA #3; UX 15. Shots `20`, `33`.
- Fix: caption line under the hovered cell ("Glass: 3 sand + fuel, 6 s") plus tooltip, popup width fits its cell count, checkmark on the active recipe, no popup when there is only one recipe. If two Kiln recipes still look alike, add a distinguishing badge (art optional, not required).
- Code. S-M. Sonnet.

**VP-10 Converter strips and status text overprint each other; colour-only status**
- Raised by: AD P1-5; IA P1-3; QA #10, #11; UX 16. Shots `13`, `19`, `22`, `33`, `37`, `38`.
- Fix: anchor icon row, status and counter in one column per building; vertical offset when bounds intersect a neighbour; zoom LOD (zoom <= 2 full strip, 3-4 icon plus one number, >= 5 status dot only); pair colour with shape (blocked = square with "!", starved = empty bowl, working = gear, full = box; tick/cross on have/need); status text not covered by gauges ("Output pile ful" clip).
- Code. M. **Opus** (layout and collision logic).

### A3. World and veil

**VP-11 Veil: tile seams every 438 px, coast rim leaks through, hard outline**
- Raised by: AD P1-7; IA P1-1. Shots `85`-`95`, `91`.
- Fix (code half): offset the tiling per layer, overlap two layers at non-commensurate periods (256 and 192 texels), stop sampling the island ground under it (desaturate 80%), extend the mask 1-2 texels or raise the veil above the coast lip and wall, feather the outline 2-3 cells into the sky. Art half is VP-C (`island_veil_mist_256x256_4f` v2, `island_veil_edge_64x64`).
- Code. S-M. Sonnet.

**VP-12 Fuel rack and Spirit Bridge look like opaque placeholder rectangles (interim)**
- Raised by: AD P1-6; IA P1-4; QA #34. Shots `14`, `18`, `37`, `38`, `44`.
- Fix: remove the black fill and the bridge backing quad (check `BridgeFaceView` and sprite import alpha); empty rack at ~0.6 alpha tinted to the ground, stronger only on hover or low fuel; counter moved below the bridge footprint. The final rack look is decided in VP-34.
- Code. S. Sonnet.

**VP-13 World overlay lines, rings and markers restyled**
- Raised by: AD P1-5, P3-4, P3-5; IA P2-5, P3-8; UX 10. Shots `02`, `18`, `21`, `25`, `38`, `40`, `44`, `98`.
- Fix: link lines, gather circles and the hover rectangle drawn as 1-texel pixel lines on the texel grid, dashes 2 on / 2 off, palette colours (P28 link, P24 fuel, P18 blocked); links fade by distance or show only in build mode; Altar plaza corner markers and radius lines only in build mode or on hover; version badge "v53" moves into Help (or dev builds only). Palette is existing, so no art.
- Code. M. Sonnet.

**VP-14 Seeded ground variety, spacing and prop jitter**
- Raised by: AD P1-2 (code half), P3-3, P3-6; IA P2-4, P3-4, P3-5. Shots `02`, `62`, `63`, `69`, `72`, `76`, `82`, `42`, `38`.
- Fix: pick among the 3 fill variants per cell with a seeded hash (no same variant twice in a 2x2 block); macro patches with random flip X/Y (no rotation), min 1 cell apart, two densities (sparse at the coast, denser near the build zone); a large-scale value layer (3-6 cell blobs, one palette step lighter or darker, dithered edge); lily pads at random offsets (not the 32 px grid); jitter in tree and quarry rows; raise the 30-40% alpha grass tufts to full or remove; starter layout gets 1-cell grass or plaza gutters. No new colours, so independent of the mood answer (VP-33).
- Code and data. S-M. Sonnet.

**VP-15 Decor reads like interactables**
- Raised by: AD P2-3. Shots `02`, `62`, `74`, `79`, `82`.
- Fix: decor gets no ink outline (ground-tinted), one value step toward the ground, max 16x16 visible texels; interactables keep the outline plus the contact shadow (VP-C) and a 1-texel P07 hover rim.
- Code and data. S. Sonnet.

**VP-16 Cliff walls and underside: barcode stripes, overlap on diagonals, sawtooth fringe, lip steps**
- Raised by: AD P2-4, P2-9; IA P2-1, P2-2, P2-3, P3-7. Shots `63`, `64`, `67`, `74`, `77`, `80`, `83`, `98`.
- Fix: pick wall frames from run-length noise (never the same frame twice in a row, 3-4 variants by hash); ragged wall bottom overlapping the underside piece by 2-3 texels; underside anchored on the wall's bottom edge with lateral jitter and 0.8-1.2 scale, drawn behind the wall for the first 4 texels; fewer, larger underside rocks with 1-2 long ones per island; lip drawn as one continuous strip along the baked curve (or rotated to the segment angle, 1 texel overlap), and the Celestial outline gap closed; blend the wall height over 3-4 segments where the curve goes vertical. `Editor/CoastCurves.cs`. Art keys for the walls are in VP-C.
- Code. M. Sonnet.

**VP-17 Paths, plaza and satellite islets are right-angled or perfect discs**
- Raised by: AD P2-5; IA P2-5, P2-8. Shots `14`, `18`, `37`, `40`, `48`, `91`.
- Fix: path layout rounds outer corners with diagonal cells; plaza overlay as a circle under the altar, drawn once (no double edge); islet masks noise-perturbed in 3-6 cell lobes and run through the same curve pipeline as the main islands. Extends the owner's "no 90 degree angles" rule from coasts to everything.
- Code and data. M. Sonnet.

### A4. UI behaviour and polish

**VP-18 Altar tree: nodes cut off, no pan hint, state shown by colour only**
- Raised by: QA #7, #9; UX 9. Shots `12`, `17`, `31`.
- Fix: fit tree to the window on open plus drag-to-pan and a "recentre" control; hover tooltip with name, effect, next cost (have/need); node states get non-colour cues (check = maxed, lock = locked, thicker ring = selected; maxed and selected no longer both gold); correct the hint wording ("click a green node"); cost mirrored under the selected node.
- Code. M. Sonnet.

**VP-19 Hover name tag anchored away from its target**
- Raised by: UX 11; QA #20. Shots `04`, `07`, `18`, `19`, `26`, `32`, `34`.
- Fix: anchor the tag to the top edge of the highlighted building (never under the cursor), second line status with icon, third line one hint ("Right-click: feed fuel"); expand after 0.6 s dwell; keep on screen. `Tooltip.Place` reads `Mouse.current` and assumes an overlay canvas, which is wrong for Screen Space-Camera.
- Code. S-M. Sonnet. **Verify live** (likely a headless artefact; re-check with a real mouse before changing).

**VP-20 Placement and demolish feedback is binary and unexplained**
- Raised by: UX 12; QA #17, #30; IA P3-3. Shots `06`, `07`, `25`, `26`, `28`, `45`, `46`.
- Fix: reason line ("Locked: finish Quest 4", "Blocked: overlaps Paper Mill", "Not enough: need 25 wood"), blocked cells tinted individually, cost chip with have/need on the ghost (on a plate, VP-07), tick or word for a valid ghost, refund preview on demolish hover ("+60% 15 wood"), mode banner "Demolish: click a building, Esc to stop".
- Code. M. Sonnet.

**VP-21 Build menu: overflow, no affordability, big empty state**
- Raised by: QA #14, #15; UX 13. Shots `05`, `15`.
- Fix: arrows plus mouse-wheel scroll and edge fade; affordable and dim card states (`ui_card_affordable` and `ui_card_dim` art already exists, wire it); names fit the card (shrink or wrap), larger cost line; tooltip with recipe summary; empty state shrinks, names the next quest and has a close button; collapse to a half-height strip while a ghost is held.
- Code. M. Sonnet.

**VP-22 Bottom docks inconsistent (link editor, bridge, pavilion)**
- Raised by: QA #13, #26, #27; UX 14. Shots `21`, `22`, `23`.
- Fix: one `DockPanel` template (title bar with icon, subtitle, body, Close and Esc on every dock, including the link editor); status dot paired with an icon or word and a tooltip; "(empty)" wording clarified; Recruit button icons spaced and the reason line not colour-only; bridge header split into two lines.
- Code. M. Sonnet.

**VP-23 Bottom-bar alignment and jitter (layout stays as is until VP-31)**
- Raised by: QA #28, #29; UX 10. Shots all HUD shots, `07`.
- Fix: fixed pill widths, one baseline for Help, read-out style distinct from buttons, hand pill and chip not duplicated; "Esc to exit" banner in demolish mode (shared with VP-20).
- Code. S. Haiku.

**VP-24 Quest panel Claim states and collapse**
- Raised by: QA #18, #19; UX 18. Shots `02`, `03`, `11`, `14`.
- Fix: not-ready Claim visibly dimmed; ready state by glow or icon and text, not by turning red (the colour palette itself is VP-30); heading of the current quest in gold, milestone line collapsed to one line, bar removed where text already shows the count. **Verify live** whether the "-" minimise works (`11` is labelled collapsed but shows the full panel).
- Code. S. Sonnet. **Verify live.**

**VP-25 Stats, Flow and Help polish**
- Raised by: QA #22, #23, #24; UX 20. Shots `08`, `09`, `10`, `16`.
- Fix: fixed modal height across tabs; Flow headers and zeros at 4.5:1, scrollbar clear of the last column; wider scrollbar inside the frame plus fade at the clip edge in Help; Stats groups (Progress / Production / Combat), zero rows hidden except the first three.
- Code. S. Sonnet.

**VP-26 Toast, confirm and dialog text hierarchy**
- Raised by: UX 17, 19; QA #32, #33. Shots `24`, `29`, `30`, `35`, `01`.
- Fix: toast as a dark pill with a category icon, max 3 stacked, 3 s; confirm dialog gets a title plus body (no orphaned "Immortality?" line); Dragon story dialog gets a title and speaker line; one punctuation convention (ASCII " - ", "->" in quest text) and no orphan line wraps.
- Code (strings). S. Sonnet.

**VP-27 Live QA run: resolutions, keyboard, real mouse (verify only, no fix yet)**
- Raised by: QA "not verifiable from stills" and #21; AD (nothing). Needs a real play session.
- Check: 1280x720, 2560x1440, 16:10 and WebGL resize (quest panel 375x570 covers a third of the world at 720p); keyboard paths and focus rings; Calm/Motion; right-click-only feeding; cursor art; colour-blind simulation shots. Output: a defects list. If 720p is bad, VP-37 gets a Hub question.
- QA run. M. Sonnet (tester). **Verify live.**

### "Verify live" items (need a real session, not the headless tour)

| Item | What to check |
|---|---|
| VP-03 | Is "497734h" a tour-save artefact (started = 0) or a real clock bug? |
| VP-05 | Do Welcome, Help, Confirm and Dragon already dim the world in play? |
| VP-08 | Does the hand chip really cover panels with a real mouse? |
| VP-19 | Is the name tag really at bottom-centre with a real mouse? |
| VP-24 | Does the quest panel minimise ("-")? |
| VP-27 | Resolution matrix, keyboard focus, Calm/Motion, colour-blind check |
| AD P1-1 (VP-28) | One 1:1 crop of adjacent texels to confirm uneven widths (arithmetic already shows 1.71 px/texel) |

---

## Batch B — needs owner answers

All 8 questions are in `docs/Design/questions.json`, group "Visual pass". Work starts on the answer; until then it only continues as A-batch interim items.

| ID | Item | Raised by | Question id | Effort | Role |
|---|---|---|---|---|---|
| VP-28 | Integer pixel zoom: camera snaps to whole screen px per texel (4/3/2/1 px = 15/20/30/60 cells across, optionally a 120-cell far step); pan snaps to 1/k texel; zoom tween lands on a stop; also the ART-SPEC §2.1 / ADR 0005 rule | AD P1-1, P3-1; IA P1-5 | `vp_zoom` | M | Opus (camera, `CameraController`) |
| VP-29 | Shrine panel and Request panel (spec in UX "Proposed specs": Tribute rows with have/need bars, 3 request cards with reason lines, NPC marker, Sky Path reveal) | UX 2 | `vp_panels` | L (code M, art S) | Sonnet; spec by UI/UX |
| VP-30 | First-run guidance: objective pointer, one-sentence quest text with "?" tooltip, named unlock, auto-claim of pickup quests, optional tutorial | UX 4, 18 | `vp_guidance` | M | Sonnet |
| VP-31 | UI look: primary button off red, one jade frame family (Welcome/Help leave parchment, toast and quest panel framed), real pixel icons and a TMP sprite asset from `ui_*`/`item_*` art, Pixelify Sans / Silkscreen, Help as 5 short tabs with key chips | UX 6, 7, 8, 21; AD P1-4, P2-6; QA #19 | `vp_look` | M-L | Sonnet; art in VP-C |
| VP-32 | Bottom bar layout: icon-only buttons with tooltips, Reset moved away from Demolish, title dropped after the first session, optional status block | UX 10; QA #29 | `vp_bar` | M | Sonnet |
| VP-33 | Ground key and sky mood: remap fill tiles to a mid value with lower chroma (Center base P13 with P14 highlights, Celestial P06 with P07 highlights, Volcano P03 with P02 cracks), optional dawn sky, warm grade, island moods, day cycle | AD P1-3, P3-2 | `vp_mood` | S code + art | Sonnet |
| VP-34 | Fuel rack final look (soft box / drawn log rack with fed logs / fire that follows fuel) | AD P1-6; IA P1-4; QA #34 | `vp_props` | S code + M art | Sonnet |
| VP-35 | Re-draw Kiln, Workbench, Herb Garden, paddy plots in front 3/4; optionally audit all 26 buildings | AD P2-1 | `vp_props` | L art | Art director brief |
| VP-36 | Fishing Island ground identity (shingle / mossy shore / snow / shore with channels and jetties); wall colour follows | AD P2-8; IA P2-9 | `vp_fishing` | M art | Art director brief |
| VP-37 | Text size or UI scale option and small-screen quest panel | QA #21 | none yet (asked only if VP-27 shows trouble at 720p) | L | UI/UX |

---

## Batch C — new art (paste into ART-SPEC)

77 files in 40 rows. **45 files need no owner answer**; **32 are gated** (answers in brackets). Conventions: PPU 32, Point, 3/4 view, palette P01-P32, selective outline, no rotation, 3 seamless variants tile with each other. Tiling art must be checked tiled 3x3 (veil) or 4x4 (fills) at zoom 2 before delivery.

### C1. Ready now (45 files)

| Key | Canvas / frames | One-line spec |
|---|---|---|
| `island_farm_ground_fill_32x32_3f` | 32x32, 3 | 3 seamless variants, 3-4 warm sand/straw tones around P23 in the mid value band, 2-4 px detail clusters, never a regular grid, nothing aligned to the 32 px edge. |
| `island_grove_ground_fill_32x32_3f` | 32x32, 3 | 3 seamless teal-moss variants mixing P13 and P09, leaf-litter flecks in clusters, calmer than the herbs on top. |
| `island_mine_ground_fill_32x32_3f` | 32x32, 3 | Redo: calm grey rock, low-contrast dither, no checker and no equal-spaced dashes; ore nodes must read on top. |
| `island_volcano_ground_fill_32x32_3f` | 32x32, 3 | Redo: base P03 with P02 cracks as irregular plates; embers scattered, not on a grid; obsidian props must read on it. |
| `island_celestial_ground_fill_32x32_3f` | 32x32, 3 | Redo: veined marble, base P06 with P07 only as highlights; no dash grid; clouds and star-dust props must read on it. |
| `island_center_macro_96x96_4f` v2 | 96x96, 4 | 4 clearly different organic patches (long drift, ring, kidney, scatter) instead of 4 clover crosses, one value step off the ground, dithered edge. |
| `island_center_path_32x32_15f` v2 | 32x32, 15 | Path blob set: outer corners rounded by 1/4 cell, inner corners filled with moss, no red-brown 1 px outline (P20 at the south edge only), soft grass fringe. |
| `island_veil_mist_256x256_4f` v2 | 256x256, 4 | Dense swirling pale lilac-grey fog, palette P06/P07 with P32 shadows only, 4 alpha steps, swirls 16-48 px, no warm colours, wraps seamlessly on both axes. |
| `island_veil_edge_64x64` | 64x64, 1 | Soft feathered mist edge that fades the veil 2-3 cells into the sky. |
| `island_center_cliffwall_round_32x96_4f` | 32x96, 4 | Already open in the Delivery log: continuous horizontal strata through frame edges (rows 12, 30, 52), vertical accents on at most 1 of 3 frames. |
| `island_farm_cliffwall_round_32x96_4f` | 32x96, 4 | Same brief, sand strata, no picket-fence columns. |
| `island_mine_cliffwall_round_32x96_4f` | 32x96, 4 | Same brief plus a top lip and a stone-block pattern. |
| `island_fishing_cliffwall_round_32x96_4f` | 32x96, 4 | Same brief; desaturate toward the ground palette (icy blue-grey, lip colour from the ground), teal as accent only. |
| `island_volcano_cliffwall_round_32x96_4f` | 32x96, 4 | Same brief; lava cracks on at most 1 of 3 frames, never every 32 px. |
| `island_grove_cliffwall_round_32x96_4f` | 32x96, 4 | Same brief; vines as the vertical accent, calmer lip blocks. |
| `island_celestial_cliffwall_round_32x96_4f` | 32x96, 4 | Same brief; violet crystals on at most 1 of 3 frames, no detached outline above the lip. |
| `fx_contact_shadow_32x16_3f` | 32x16, 3 (S/M/L) | Ellipse narrower than the footprint, offset +2 texels right and +1 down, one palette step darker than the ground, 50% checker dither, binary alpha; 1x1, 2x2, 3x3 footprints use frames, never transform scale. |
| `island_<biome>_flowers_32x32_4f` v2 (7 biomes: center, farm, mine, fishing, volcano, grove, celestial; per existing key) | 32x32, 4 | Decor without ink outline, one value step toward the ground, max 16x16 visible texels. |
| `item_<key>_drop_16x16` (12 most common items) | 16x16, 1 | Hand-cleaned nearest downsample of the item icon for ground drops, outline kept, readable at 1x. |
| `bld_awakened_dragon` (fix) | existing | Clean the alpha: remove isolated dither pixels, quantise to palette, bake a 2-3 texel oval shadow under the rock base. |
| `ui_sound`, `ui_motion`, `ui_help`, `ui_reset` | 32x32 each (4) | Bottom-bar icons matching the `ui_*` set; Reset reads as danger without being red-only. |
| `ui_free_play` | 32x32 | Replaces the spiral emoji for "Free play". |
| `ui_dragon` | 16x16 and 32x32 | Dragon head for the ending card and dialog; reuse `ui_quest_dragon1` if it reads at 16 px. |
| `ui_welcome_sprout` | 32x32 | Sprout for the Welcome modal, replacing the emoji. |
| `ui_check_done` | 16x16 | Done tick for request cards, tree nodes (maxed) and the valid-ghost tag. |

Count: 5 ground fills + 1 macro + 1 path + 2 veil + 7 cliff walls + 1 shadow + 7 flowers + 12 drops + 1 dragon fix + 8 UI icons = 45 files in 25 rows.

**Verify before ordering (not new):** `ui_hand`, `ui_stats`, `ui_build`, `ui_demolish`, `ui_quests`, `ui_target`, `ui_lock`, `ui_unlock`, `ui_claim`, `ui_add`, `ui_controls`, `ui_quest_*`, `ui_area_*`, `ui_action_*` are in ART-SPEC but the tour shows emoji: check delivery in the Delivery log and wiring in code (feeds VP-31).

### C2. Gated on an owner answer (32 files)

| Key | Gated on | One-line spec |
|---|---|---|
| `island_fishing_ground_fill_32x32_3f` | `vp_fishing` | 3 seamless variants for the chosen identity (moss shore: greens and wet sand with water-edge darks); no snow look; not on the 32 px grid. |
| `island_fishing_macro_96x96_4f` v4 | `vp_fishing` | 4 patches to match the chosen identity; lily pads scattered, not gridded. |
| `fix_fuel_rack_96x64` | `vp_props` | 3/4 wooden log rack seen front-above: two posts, slanted shingle roof, 2x3 cubbies; empty cubbies dark P20 wood, never black; no orange glow except firestone. |
| `fx_rack_fuel_wood_32x32`, `fx_rack_fuel_bamboo_32x32`, `fx_rack_fuel_charcoal_32x32`, `fx_rack_fuel_firestone_32x32` (4) | `vp_props` | Per-fuel stack sprites drawn in 3/4 that code composites into the cubbies. |
| `bld_workbench` | `vp_props` | 96x128: front-facing bench, roof awning seen from front-above; reference `bld_paper_mill`. |
| `bld_kiln` | `vp_props` | 96x192: upright brick kiln, chimney at the back, mouth facing south. |
| `bld_herb_garden` | `vp_props` | 96x128: rectangular raised bed seen front-above. |
| `node_crop` (+ paddy variants) | `vp_props` | Rectangular bunded plot with the front bank visible, not a rhombus. |
| `sky_cloudsea_far_512x512`, `sky_cloudsea_mid_512x512`, `sky_depth_512x512` (3) | `vp_mood` | Dawn variant: pale white-cyan cloud tops with soft gold light, calmer; two layers at non-commensurate periods, lower contrast far away. |
| `ui_btn_primary_*` (set) | `vp_look` | Recolour to bright jade fill with gold text; red stays for danger only. |
| `ui_questpanel_frame_64x64` | `vp_look` | Recolour to jade on night ink with a single cinnabar seal; gold as a 2 px accent. |
| `ui_island_preview_<id>` (10, e.g. farm, mine, fishing, volcano, grove, lotus, thunder, ravine, tribulation, celestial) | `vp_panels` | 128x72 sky-view thumbnail of the Island for the Shrine card. |
| `ui_request_card_normal`, `ui_request_card_done`, `ui_request_card_waiting` (3) | `vp_panels` | 9-slice card frames for the three request states; done has a check, waiting is dimmed. |
| `ui_shrine`, `ui_request`, `ui_request_marker` (3; check ART-SPEC §3.7 first, they may already be listed) | `vp_panels` | Panel header icons and the NPC marker with a count badge slot (hidden when all 3 requests are done). |

Count: 2 fishing + 1 rack + 4 fuel + 3 buildings + 1 node + 3 sky + 2 UI recolours + 10 previews + 3 cards + 3 icons = 32 files in 15 rows. With C1 (45) the total is **77 files**.

Dropped on purpose (not essential, no Hub question): `fix_island_signpost_64x96` and `ui_island_banner_96x24` (island names are restyled in VP-07), `ui_scrim_vignette` (flat dim, VP-05), and distinct silhouettes for the three buildings that share the barrel-house sprite (IA P3-1, revisit with the next building batch).

---

## Suggested order of work

1. VP-01, 02, 04, 05, 06, 03 (one Sonnet pass, all S, removes everything that looks broken or wrong).
2. VP-07, 08, 09, 10 (readability and overlap).
3. VP-11, 12, 13 (veil, fuel rack/bridge interim, overlay lines).
4. VP-14, 15, 16, 17 (world look in code, no new art).
5. VP-18 to 26 (UI behaviour), VP-27 live QA in parallel.
6. B items as the owner answers; send C1 to the art loop now.
