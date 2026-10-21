# UI/UX guide (UI/UX designer)

Agent: `.claude/agents/ui-ux-designer.md` (sonnet). Reviewed by the UI QA reviewer (`ui-qa.md`).

## Purpose
Design screens, flows and interaction rules, and keep the UI skin system coherent. You specify; a feature builder implements through the Editor builders; UI QA checks.

## Current state of this game
- **uGUI on a pixel-art skin** (code in `Assets/_Project/Scripts/Game/UI/`, builders in `Assets/_Project/Scripts/Editor/`).
- **Skin system:** `UiSkin` applies a role sprite *Sliced* at 1 art px = 2 UI px (1x floor), disabling the placeholder outline. `GameDatabase.ui` is the registry; `ArtIntake` sets 9-slice borders for every ART-SPEC section 5 key. `UiSkinRoles` has 20 roles (jade = modals and side panels, dark = tooltips and chips; root path "" = prefab root). Menu: `Idle Grounds/UI/Apply UI Skin Roles`.
- **Buttons:** `UiButtonSkin` (Standard / Primary / Close, SpriteSwap) on 36 buttons via builders and menu `Idle Grounds/UI/Apply Button Skins`; toggles (Build, Demolish, Sound, Motion, Stats tab) swap toggle_on / danger art. Also: `UiBar` (sliced fill driven by anchors), `UiCheckbox`, scrollbars on all ScrollRects, `UiArtRoles.AttachAll`, `GameCursor` (2x hardware cursor: open, closed, target; system arrow over UI), `Tooltip` / `TooltipTrigger`, `HandCursorView` (hand chip clamped above the bottom bar), `UiJuice`.
- **Batch B (2026-10-21, owner picks vp_*):** building panels (recipe picker, link editor, bridge, pavilion; future Shrine/Request panels too) use `BuildingPanelAnchor` (beside the building art, follows the camera, clamped to the safe area, clear of the quest panel, docks top/bottom when no side fits); global windows (Stats, Help, Altar tree, Menu) stay centred. One font: Pixelify Sans (`UiFont` swaps old texts at runtime; rebuild the assets with Idle Grounds/UI/Build Pixel Font). Text sizes must be whole font pixels on screen: 22 px (body, raster 1:1 on desktop), 33, 44...; never author 15-19 and expect it to stay; no fake bold (hierarchy by size and colour). Desktop bar is icon-only with tooltips; Help + New game live in `GameMenuView` (one menu for desktop and phone, Esc / Back opens it). Placeholder code-drawn icons waiting for art: ui_sound, ui_sound_off, ui_motion, ui_motion_calm, ui_menu, ui_help, ui_reset (`UiIcons`). Zoom: crisp stops 15/20/30/60 cells + 120-cell map stop.
- **Existing screens:** HUD / MetaBar, bottom bar, build menu (cards), Altar upgrade tree, Dragon dialog, Pavilion panel, quest panel, Stats (with Flow tab), Help and Welcome (jade scroll until the scroll redo), Confirm dialog, toasts, fuel rack.
- **Art rejected and pending:** scrim art (dither grid) rejected; cards/pills/minimise were placeholders then integrated (UI art 3). Emoji icons are still placeholders for many `ui_*` keys (plan M4: replace emoji icons).
- **To build (ADR 0007):** Shrine panel (next Island card with 5 rows have/need and art preview, greyed previews of the rest), Request panel (portrait, one line, 3 cards open at once, have/need, reward preview), NPC request marker, Sky Path reveal, ending flow on the Heavenly Gate. **To delete:** Ascend dialog, perk shop, post-ascension card, vow rows, AP parts of MetaBar / MetaText / HelpText / QuestPanel / GateFaceView (already removed in Phase 1; verify nothing is left).

## Rules to enforce
1. Reuse existing roles and components; a new role needs a spec entry (art director) and a `UiSkinRoles` line.
2. Right-click feeding only; panels state it. Installments persist; switching cards is free.
3. Always show have / need; show *why* something is unavailable.
4. Esc and a Close button on every panel; one primary action.
5. No fractional UI scaling of pixel art; text crisp.
6. Honour reduce-motion (HUD Motion/Calm toggle) for every effect.
7. Never colour-only signalling (see `ui-qa.md`).
8. **Esc / Back order (top layer first):** yes/no confirm -> Menu (`GameMenuView`, always drawn on top, so it closes before anything under it) -> Altar tree -> recipe picker -> pavilion -> bridge -> link editor (picking backs out to its menu first) -> Dragon dialog -> Stats / Help / Welcome -> placing / demolish / build menu. With nothing open, Esc opens the Menu (desktop) and Back opens it (phone). Code: `BuildController.TryEscape`. A new panel goes into this chain at its drawing layer.
9. **Windows fit the screen; text never shrinks below the floor.** Centred windows (`ModalView`) sit between the bottom bar and the top of the safe area; when too tall, their header (title, tabs, close) stays and the body scrolls (`ShrinkBody`); a uniform scale is the last resort only. Building panels (`BuildingPanelAnchor`) take the screen width when wider (text wraps) and scroll everything under their title bar when taller (dock top/bottom); they shrink only if they cannot scroll.
10. **A building panel closes when its building leaves the screen** (panned or zoomed out of view after it was on screen), unless the player is still working with it elsewhere (link editor while picking a source or target). It never points at nothing.
11. **Layouts follow the snapped text.** At 1280x720 the canvas is 0.67 but text stays 22 screen px, so text is 1.5x larger relative to the layout: size buttons to their labels (`UiFit.FitButton`), widen text panels by `UiFit.TextGrowth` (quest panel, Stats), wrap filter/notes onto their own line, never auto-size names below the floor. Check every screen at 1920x1080, 1280x720 and phone 2400x1080.
12. **World text floor:** 11 world px (`ViewKit.MinTextPx`: one Pixelify pixel per art pixel at the zoom-2 label size, about 19 screen px at 1080p and 12.5 at 720p). UI text floor: 22 screen px desktop, 33 phone. Badges carry a word at body size (the build card "New" sits on the card's top edge).
13. **Key names are key-caps:** `[B]`, `[Q] / [E]`, `[Esc]`, `[WASD]` in Help and tooltips (`ControlWords` turns them into touch words on phones).

## Checklists
- **New panel spec:** purpose, trigger, layout, states (empty / partial / full / disabled), input (mouse, keys), roles and art keys, strings (writer), what QA checks.
- **Request panel:** 3 cards, installments, reward preview names the building or utility, waiting reason line for "†" and "‡" requests, NPC portrait 64x64.
- **Shrine panel:** next card + 5 greyed previews, paid installments shown, Sky Path preview.
- **Build through builders** so scenes can be regenerated; save scene/prefabs after.

## Open issues
- Pixelify Sans look-alikes (5/S, 1/7, 3/8, B/8, E/C): owner question `font_read` (Hub), pick pending; candidates Jersey 10 (30 px), VT323, Atkinson Hyperlegible, all OFL.
- Building-card names and world pills grew with the text floors (UI QA batch B #7): watch label collisions in dense areas.
- Emoji to real `ui_*` icons; the scroll redo for Help/Welcome; scrim.
- Shrine and Request panels do not exist yet.
- Camera range 17.5 to 52 tiles wide: HUD must stay readable at both.
- The TMP fallback font asset gets dirtied when entering Play (revert it before committing).

## Owner feedback that constrains this role
- `owner-picks-2026-10-12-requesters.json`: "you select from menu"; all 3 requests open at once, pick any; right-click feeding only.
- `owner-picks-2026-10-12.json`: tribute is paid at a shrine; no AP, perks or vow UI.
