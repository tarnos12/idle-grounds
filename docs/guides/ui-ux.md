# UI/UX guide (UI/UX designer)

Agent: `.claude/agents/ui-ux-designer.md` (sonnet). Reviewed by the UI QA reviewer (`ui-qa.md`).

## Purpose
Design screens, flows and interaction rules, and keep the UI skin system coherent. You specify; a feature builder implements through the Editor builders; UI QA checks.

## Current state of this game
- **uGUI on a pixel-art skin** (code in `Assets/_Project/Scripts/Game/UI/`, builders in `Assets/_Project/Scripts/Editor/`).
- **Skin system:** `UiSkin` applies a role sprite *Sliced* at 1 art px = 2 UI px (1x floor), disabling the placeholder outline. `GameDatabase.ui` is the registry; `ArtIntake` sets 9-slice borders for every ART-SPEC section 5 key. `UiSkinRoles` has 20 roles (jade = modals and side panels, dark = tooltips and chips; root path "" = prefab root). Menu: `Idle Grounds/UI/Apply UI Skin Roles`.
- **Buttons:** `UiButtonSkin` (Standard / Primary / Close, SpriteSwap) on 36 buttons via builders and menu `Idle Grounds/UI/Apply Button Skins`; toggles (Build, Demolish, Sound, Motion, Stats tab) swap toggle_on / danger art. Also: `UiBar` (sliced fill driven by anchors), `UiCheckbox`, scrollbars on all ScrollRects, `UiArtRoles.AttachAll`, `GameCursor` (2x hardware cursor: open, closed, target; system arrow over UI), `Tooltip` / `TooltipTrigger`, `HandCursorView` (hand chip clamped above the bottom bar), `UiJuice`.
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

## Checklists
- **New panel spec:** purpose, trigger, layout, states (empty / partial / full / disabled), input (mouse, keys), roles and art keys, strings (writer), what QA checks.
- **Request panel:** 3 cards, installments, reward preview names the building or utility, waiting reason line for "†" and "‡" requests, NPC portrait 64x64.
- **Shrine panel:** next card + 5 greyed previews, paid installments shown, Sky Path preview.
- **Build through builders** so scenes can be regenerated; save scene/prefabs after.

## Open issues
- Emoji to real `ui_*` icons; the scroll redo for Help/Welcome; scrim.
- Shrine and Request panels do not exist yet.
- Camera range 17.5 to 52 tiles wide: HUD must stay readable at both.
- The TMP fallback font asset gets dirtied when entering Play (revert it before committing).

## Owner feedback that constrains this role
- `owner-picks-2026-10-12-requesters.json`: "you select from menu"; all 3 requests open at once, pick any; right-click feeding only.
- `owner-picks-2026-10-12.json`: tribute is paid at a shrine; no AP, perks or vow UI.
