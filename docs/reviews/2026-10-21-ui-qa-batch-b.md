# UI QA review: visual pass batch B (2026-10-21)

Reviewer: UI QA. Build: master e42f225 (bg/visb merged). Method: `tools/bg/shot.sh` Play-mode shots with HUD on, t30 bot save, at 1920x1080, 1280x720, phone 2400x1080 and phone 2520x1080 (21:9; 3200x1440 preset not rerun). 57 shots per size: recipe / link / bridge / pavilion panels with the building at centre, left, right, top, bottom and off-screen-left, plus Stats (3 tabs), Help, Menu, Altar tree, both confirms, Build menu, Demolish, Welcome, Dragon dialog, hand chip, toast, quest panel expanded/collapsed and every bar tooltip. Raw shots are in the session scratchpad; the ones cited below are copied to `docs/reviews/img/2026-10-21-batch-b/` (paths relative to this file: `img/2026-10-21-batch-b/<name>`). Contact sheets: `sheet_d1080_kiln.png` (anchoring matrix), `sheet_d720_g.png`, `sheet_p2400_g.png`.

Not covered: Esc order was reviewed in code only (no scripted key input in shot.sh); cursor swap; colour-blind simulation (no new red/green-only signals seen in these screens: have/need pills carry a check mark, No Fuel carries a "!" icon, locked tree nodes carry "?"); WebGL canvas resize; 2560x1440; the 21:9 and 720p anchored-panel shots were taken but only skimmed.

## Findings

| # | Sev | Where | Finding | Evidence | Suggested fix / owner |
|---|-----|-------|---------|----------|------|
| 1 | major | Everywhere numbers appear (build card costs, quest panel, recipe line, stats) | Pixelify's **5 reads as S** and its 3 reads as 8 at 22 px: cost "25" shows "2S", "450" shows "4S0", recipe "5 s" shows "S s", "Wisp links 3" looks like 8. The font adoption was meant to fix 5/8 confusion; digits are the one place a misread costs the player. | `d1080_g_build.png` (Kiln "2S"), `quest_digits_zoom.png`, `d1080_p_kiln_ctr.png`, `p2400_g_stats.png` | Draw digits (and `/ x + - :`) from a different cut/fallback font (TMP fallback by Unicode range 0030-0039) or pick a font whose 5 and 3 are distinct; UI/UX designer to choose, art director to approve. |
| 2 | major | Help (and anywhere keys are named: tooltips "Build (B)", Help controls) | Capital **B reads as 8** ("8 opens the build menu", "8 places a ghost"), **E reads as a C with a bar**, and `(` `)` look like `{` `}`. Key names are the instruction. | `d1080_g_help.png`, crop in this session | Show keys as key-caps / `[B]` in a distinct style, or fix with the font choice in #1. UI/UX designer. |
| 3 | major | Stats on phone 2400x1080 / 2520x1080 (all three tabs) | Stats panel is taller than the safe area and is **not scaled to fit**: title, close button and tab row are off the top of the screen, the bottom rows run into the bar. Only Back can close it. (GameMenuView.Fit shrinks; ModalView does not.) | `p2400_g_stats.png`, `sheet_p2400_g.png` (stats, stats_flow, stats_lifetime) | Give StatsView/ModalView the same fit-to-safe-area shrink as GameMenuView.Fit, or make the body scroll with a fixed header. Feature builder. |
| 4 | major | Stats > Flow at 1280x720 | Header controls collide: "This run", "Sort: net / min" and the "net / min = last ~5 min" note overprint each other; column heads "Used / Lost / Net/min" overlap. 22 px text no longer fits the old column widths. | `d720_g_stats_flow.png` | Wrap the filter row into two lines or drop the note to a tooltip; widen columns / shorten heads ("Used", "Lost", "Net"). UI/UX designer + feature builder. |
| 5 | major | Altar tree at 1280x720 | "Drag or WASD to pan" runs into **Recentre**, which touches the close button; "Cancel & refund" is clipped at the panel edge; the "needs 30 Wood" label sits under neighbouring nodes ("eeds 30 Woo"); the status line wraps and the second line is cut off. Same label/line crowding at 1080p (`needs 30 Wood` over the connector lines). | `d720_g_tree.png` | Shorten hint to "Drag / WASD: pan", give the status line two rows or truncate, move the "needs" label to the status line only. UI/UX designer. |
| 6 | major | Bottom bar, Menu icon (desktop, found while testing tooltips) | **The Menu icon had no tooltip.** `EnsureMenuButton` cloned the Help button and used deferred `Destroy` on the cloned TooltipTrigger; `ApplyStyle` re-used that dying trigger in the same frame, so the trigger was removed at end of frame. **Fixed in this branch** (`DestroyImmediate`); verified: `tips1080_MenuButton.png` shows "Menu (Esc): help, sound, motion, new game". | `tips1080_MenuButton.png` | Done. Feature builder to confirm in review. |
| 7 | major | World pills and "NEW" badge (all sizes) | Text below 12 px: world-space building pills ("19/1", "x10") are about 11 px and the "NEW" badge is about 8-9 px in an outline, so it reads as "DEW". Rule: nothing under 12 px on screen. | `d1080_g_build.png` (badge), `d1080_p_kiln_ctr.png` | Raise pill text to the 22 px raster or at least 12 px on screen; badge: use an icon dot or a larger word. UI/UX designer. |
| 8 | minor | Anchored panels, building off screen | When the building is off screen (shot `_offL`), the panel stays clamped to the screen edge pointing at nothing, and still follows nothing useful. Consider closing it, or showing an arrow towards the building. | `d1080_p_kiln_offL.png` | UI/UX designer decides: close on off-screen vs. keep. |
| 9 | minor | Anchored panels, overlap | Panels beside a building necessarily cover the neighbouring buildings (kiln recipe popup hides the Paper Mill pill and half the Paper Mill; link editor covers the area above its lantern). No overlap with the quest panel or the bottom bar was found in any of the 8 positions tried; the panel dodged the quest panel to the left/below correctly. The link editor (810 px wide) is the widest and always lands above or below the lantern. | `sheet_d1080_kiln.png` | None needed; noted as expected consequence of the owner pick. |
| 10 | minor | Phone, anchored panels | The "too big, shrink uniformly" path takes body text under 22 px on phones: bridge panel text is about 70 percent size (about 15 px equivalent), noticeably smaller than the rest of the phone UI. | `p2400_p_bridge_ctr.png` | Prefer docking + scroll to shrinking below ~85 percent; or narrower PanelWidth on phones. Feature builder. |
| 11 | minor | Quest panel expanded at 1280x720 | Panel is a narrow tall column (about 95 px of text width) so lines break after one or two words ("Dragon / tribute 4/4"); at 1080p it is fine. | `sheet_d720_g.png` (hud_quest_expanded) | Min width for the panel at 22 px text, or auto-collapse below 800 px canvas height. UI/UX designer. |
| 12 | minor | Esc order (code review) | `BuildController.TryEscape`: confirm, then GameMenuView, then tree / recipe / pavilion / bridge / link / dragon dialog, then Stats / Help / Welcome, then placing / build menu. So the order is **confirm -> menu -> panels**, not confirm -> panels -> menu. This is sensible while the menu is a modal on top, but the brief expected the menu last; and the menu can be opened while a recipe panel is open (`d1080_g_menu_over_recipe.png`) and then Esc closes the menu first and the panel second. Also Esc with nothing open opens the Menu (desktop) -- the expected "Menu (Esc)" behaviour. | code | Confirm intent with UI/UX designer; no change needed if menu-on-top is intended. |
| 13 | minor | Tooltips | Bar tooltip text wraps awkwardly at 720p ("Stats: your progress so / far"); tooltips for icon-only buttons are the only label, so touch/long names matter. My act places tooltips as a touch press (up-left of the icon), so their position relative to the bar is not representative of the mouse. | `tips720_*.png` | Tooltip max width 440 -> measured width; low priority. |
| 14 | minor | Fallback glyphs | No tofu boxes seen. The arrow in world pills is a thin LiberationSans arrow, small next to Pixelify; the quest text uses ASCII "->" (readable). Playtime shows "--" at t=0. | `d1080_p_kiln_ctr.png` | None. |
| 15 | info | Accessibility | There is still no text-size option, high-contrast mode or colour-blind alternates; with the 22 px floor body text is readable at 1080p and 720p. Motion/Calm toggle present in bar and Menu. Reporting only; owner scope decision needed via the Hub if wanted. | n/a | UI/UX designer. |

Also noted as OK: Menu (centred, 6 buttons, close art, Calm/On labels), both confirms, Welcome, Dragon dialog, Build menu (scrolls, close, fits above the bar), Demolish banner, hand chip, toast, quest chip collapsed, Help scroll body (contrast fine on parchment), bar icons (about 45 px targets), recipe popup above the building.

## Verdict per screen

| Screen | Desktop 1080 | Desktop 720 | Phone 2400 / 2520 |
|--------|--------------|-------------|-------------------|
| Recipe picker (anchored) | pass (digit issue #1) | pass | pass (shrink #10) |
| Link editor | pass | pass | pass (shrink) |
| Bridge panel | pass | pass | pass with #10 |
| Pavilion panel | pass | pass | pass |
| Stats (3 tabs) | pass | **fail** Flow tab (#4) | **fail** (#3) |
| Help | fail until keys readable (#2) | same | same |
| Menu | pass | pass | pass |
| Altar tree | minor (#5) | **fail** (#5) | pass (small) |
| Confirm dialogs | pass | pass | pass |
| Build menu | pass (NEW badge #7, digits #1) | pass | pass |
| Quest panel | pass | minor (#11) | pass |
| Bar icon tooltips | pass (Menu tip fixed #6) | pass | pass |

## What I changed

- `Assets/_Project/Scripts/Game/UI/BottomBarView.cs`: `Destroy` -> `DestroyImmediate` for the cloned Help tooltip trigger (finding #6).
- `Assets/_Project/Scripts/Editor/BatchActs.cs`: new shot act `barTip:<ChildName>` (bar tooltip as hovered) for tooltip screenshots.
- `docs/Design/plan.json`: task "UI QA: batch B review" (M3).
