# UI QA and accessibility guide (UI QA reviewer)

Agent: `.claude/agents/ui-qa-reviewer.md` (sonnet).

## Purpose
Verify every UI change in the running game, and make sure the game is usable by people with small screens, low vision, colour-blindness or motion sensitivity. Report defects; the UI/UX designer decides design fixes.

## Current state of this game
- Intended behaviour is in `docs/guides/ui-ux.md`, `docs/port/ui-input-render.md` and the UI parity audit `docs/port/parity-audit-ui.md` (done in M9).
- **Accessibility features that exist:** HUD **Motion / Calm toggle** (reduce motion; turns off juice such as harvest squash, item arcs, motes, UI sparkle); Sound toggle; tooltips on HUD controls; hardware cursor swaps by state.
- **Not present yet (to evaluate and report):** a text-size option; colour-blind-safe alternates for have/need and can/cannot; key remapping; a high-contrast mode. Report each as a finding with a recommendation; the UI/UX designer decides, and the owner decides scope through the Hub if it is a new feature.
- Palette roles carry meaning: green nature/herbs, cinnabar red sect/danger, gold rewards/Altar/Dragon, cyan wisps/qi/water, orange fire, violet celestial. Several UI states rely on these colours.
- Camera shows 17.5 to 52 tiles across; HUD sits on top. WebGL build runs in a browser canvas that can be any size.
- Known UI notes: scroll art for Help/Welcome is jade until the redo; scrim art rejected; emoji placeholders on many icons; Game View toolbar GC noise is editor-only.

## Rules to enforce
1. Check at 1280x720, 1920x1080, 2560x1440 and a 16:10 window, and WebGL canvas resize.
2. Nothing clipped, overlapped, off-screen, or under another panel's raycast; click targets >= 24 px.
3. Body text >= 12 px on screen and readable at 1080p; contrast >= 4.5:1 body, 3:1 large.
4. Every effect honours Motion/Calm. No flashing faster than 3 per second.
5. Never rely on colour alone: add an icon, shape, text or pattern. Test deuteranopia and protanopia simulations (screenshots) on have/need rows, fuel, health, ghost validity.
6. Right-click feeding works on every sink; left-click never feeds; Esc closes; state survives close and re-open.
7. Tooltips do not leave the screen or stick.

## Checklists
- **Per screen:** resolution matrix, states (empty / partial / full / disabled / long names / huge numbers), hover / press / disabled, keyboard, tooltip, cursor.
- **Per new effect:** off in Calm, readable in Calm.
- **Per colour cue:** shape/text alternative present.
- **Per build:** WebGL and Windows smoke pass, first-run Welcome modal, save/reload keeps the dismissed intro.
- **ADR 0007 UI:** Shrine panel, Request panel (installments, waiting reasons), NPC marker, ending flow.

## Open issues
- No text-size, high-contrast or colour-blind modes yet.
- Colour-only signals not yet audited.
- Shrine and Request panels not built, so unreviewed.
- Motion toggle coverage for future Sky Path / marker effects unknown.

## Owner feedback that constrains this role
- No owner picks specifically on accessibility yet. Anything beyond reporting (a new option, a new mode) must go to the owner through the Hub Questions tab, with the full option range (none / text size only / all modes).
- `owner-picks-2026-10-12-requesters.json`: right-click feeding only, so QA must confirm there is no other way to feed.
