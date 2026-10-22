# UI QA review: Text font setting (2026-10-22)

Reviewer: UI QA. Build: master 736b1a6 (bg/font merged) plus the fixes listed under "Fixed in this branch" (branch bg/fontqa).

Method: `tools/bg/shot.sh` Play-mode shots, HUD on, t30 bot save (`WriteTourSaves`), act `font:<id>` for each of the six options
(`pixelify`, `pixelify_jersey` = default, `jersey10`, `vt323`, `pressstart2p`, `smooth`) at 1920x1080, 1280x720 and phone 2400x1080
(dpi 405, insets 96/24/48/0). 23 screens per font and size: Build menu, Stats (Overview / Flow / Lifetime), Help, Altar tree, Menu,
Settings, quest panel expanded and collapsed, recipe picker (Kiln, Workbench), link editor, bridge panel, pavilion panel, both
confirms, dragon dialog, toast, hand chip, building tooltip, Welcome, plain HUD. That is 414 shots per full pass; the pass was run
three times (before fixes, after the first fixes, final). Raw shots live in the session scratchpad; the sheets cited here are in
`docs/reviews/img/2026-10-22-fonts/` (paths below are relative to `docs/reviews/`). Each sheet is six panels, in menu order:
pixelify, pixelify_jersey, jersey10, vt323, pressstart2p, smooth (a yellow label sits over the top-left of each panel and hides the
first words of a panel that is docked top-left, e.g. the pavilion title; it is not a game bug).

One-page contact sheet of the default font on the six busiest screens (1080p: Build, Stats, Stats Flow, Altar tree, recipe picker,
link editor): `img/2026-10-22-fonts/contact_default_d1080.png` (full path
`D:\WORK\Unity\idle-grounds-bg-fontqa\docs\reviews\img\2026-10-22-fonts\contact_default_d1080.png`; taken from the first pass, before
the fixes, which do not change those six screens in the default font except the build card badge).

Not covered: 2560x1440, 21:9, WebGL canvas resize, colour-blind simulation (no new colour-only signals: the link editor still has
state words next to its dots), cursor swap, switching the font while a building panel is already open (the shots switch the font,
then open the panel; Settings switches live and refits, but a switch with the recipe picker or link editor open was not shot).
One phone pass died once with "GameRunner did not boot" (a flake; the rerun passed).

## Findings (status as of the final pass)

Severity: blocker / major / minor / info.

| # | Sev | Font | Where | Finding | Evidence | Status |
|---|-----|------|-------|---------|----------|--------|
| 1 | major | all (worst Press Start 2P, VT323) | Recipe picker, 1280x720 | The caption "Brick: 2 Clay + fuel, 5 s (current)" wraps to 2-3 lines but its holder was a fixed 52 px: "(current)" ran onto the popup frame. | `img/2026-10-22-fonts/before_d720_recipe.jpg`, after: `after_d720_recipe_pressstart2p.jpg`, `after_d720_recipe_vt323.jpg` | FIXED: `RecipePickerView.FitCaption`, the holder grows to the wrapped text (measured at the popup width after the layout pass) |
| 2 | major | Press Start 2P | Link editor, 720p / 1080p / phone | One link row is wider than the 820-unit panel: the state word was cut ("waiti...", "block..."), source and target text ran into the arrow. | `before_d720_link.jpg`, after: `after_d720_link.jpg`, `after_d1080_link.jpg`, `after_p2400_link.jpg` | FIXED: `LinkEditorView.FitRowWidth` (state-word column and row cells measured in the face, the anchor widens the panel up to the free space) |
| 3 | major | all, worst in wide faces | Pavilion panel (beside the building) | Title, "Disciples d/cap", the food count and the close button were six items on one line (about 800 units) in a 640-unit panel: the title was cut at the left and the counts crowded the close button. | `before_d720_pavilion.jpg`, after: `after_d720_pavilion.jpg` | FIXED: `PavilionPanelView.SplitHeader` moves the counts to a second line |
| 4 | major | Press Start 2P | Confirm "Ascend to Immortality" (720p, 1080p) | The button label was wider than the button and the 660 box: "Ascend to Immortali" cut, OK pushed into Cancel. | `before_d720_confirm.jpg`, after: `after_d720_confirm.jpg` | FIXED: `ConfirmDialogView.FitButtons` (measured in the drawn face, re-checked while open, the box widens with the button row) |
| 5 | major | Press Start 2P | Welcome modal, Dragon dialog | "Continue >" lost its first letters ("ontinue"): button fixed at 230 / 210 px. | `before_d720_welcome.jpg`, after: `after_d720_welcome.jpg` | FIXED: `UiFit.FitButton` on both Continue buttons |
| 6 | major | Jersey 10 family (the default!), Pixelify | Build menu cards | The "New" badge took the full line height of the face (about 50 px in Jersey 10), centred on the card top it hid the top half of the card name, and it kept the width measured in the previous font after a switch. | `after_d720_build.jpg` (badge is 26-32 px tall in all six) | FIXED: `BuildCard.FitBadge`, re-measured when `UiFont.Version` changes |
| 7 | major | all | World pills (converter strip, formation badge, plated text) | Measured once: after a font switch the plates kept the width of the old face (VT323 and Press Start 2P digits ran out of their plate). | `after_d720_build.jpg` | FIXED for the switch: `ConverterFaceView`, `FormationFaceView`, `PlatedText` re-measure on `UiFont.Version`. The remaining crowding is #12 |
| 8 | major | Press Start 2P | Settings page, 720p and phone | The "Changes at once..." note and the sample line were the widest items, so the page grew wider than the option grid and the note ran to the frame; on phone the whole page scaled down. | `before_d720_settings.jpg`, after: `after_d720_settings.jpg`, `after_p2400_settings.jpg` | FIXED: `GameMenuView` lines take the grid width and wrap (16 px side padding); phone page fits without scaling |
| 9 | minor | Press Start 2P, VT323 | Quest panel expanded | The quest icons sit on the first letter of every line (`&Dragon tribute`) and the panel is tall (about 330 px at 720p, rows tight). "This run" no longer touches its frame in Stats Flow. | `after_d720_quest.jpg` | OPEN: the gap is `UiIconLine` spacing 6 (`ProgressionBuilder.IconLine`); changing it moves every quest line in the default font too, so UI/UX designer to pick the gap |
| 10 | minor | Press Start 2P | Stats Flow, 720p | The "Net = change per minute..." note wraps to two lines and the second line touches the column header row. | `after_d720_flow.jpg` | OPEN: note height in the Flow layout of `StatsPanelView` (feature builder) |
| 11 | minor | Press Start 2P | Bridge panel, 720p | Title "Spirit Bridge - Center" wraps to two lines and the body to four; the panel is tall but inside the safe area. | `after_d720_bridge.jpg` | OPEN, accepted |
| 12 | minor | Press Start 2P, VT323 | World pills at zoom 2, 720p | Pills are plates sized for the 11 px world floor; Press Start 2P text ("15/1", "20/2") is close to the neighbouring icon. | `after_d720_build.jpg` (bottom-left panel) | OPEN: needs a per-face plate width; art director / UI/UX designer |
| 13 | major | Pixelify only | Everywhere digits appear | 5 still reads as S ("S7/60", cost "2S", "Brick: 8 Clay"), the batch B #1 defect. | `after_d720_pavilion.jpg` (panel 1), `after_d1080_help.jpg` (panel 1, "2x") | By design: option 1 is the old look. The default option draws every digit and the `[B]` key names in Jersey 10 and reads clean on all screens at all three sizes. Resolves batch B #1 and #2 for the default |
| 14 | info | all | Glyph fallbacks | No tofu boxes in any font. Arrows are the Liberation fallback ("->" in quest text, the thin arrow in world pills); the chain icon and fox in quest text are emoji sprites. `(` `)` look like `{` `}` in Pixelify and VT323; `[B]` key-caps are clear in every font. | `after_d1080_help.jpg` | no action |
| 15 | info | all | Title / body roles | Panel titles, Settings, Stats, the tree header and the confirm headline use the title face and body text uses the body face in every panel shot; no mismatches. In the default the tree title is Pixelify and the hint line Jersey, as designed. | all sheets | pass |
| 16 | info | all | Text floor | The smallest UI text is the world pill text (about 12 px at 720p, 19 px at 1080p) and tooltips; nothing under 12 px in UI canvases. Jersey 10 and VT323 body text at 720p is the thinnest but legible. A text-size option is still not offered (batch B #15). | `after_d720_help.jpg` | no change |

## Fixed in this branch (files)

- `Assets/_Project/Scripts/Game/UI/Build/RecipePickerView.cs` (#1)
- `Assets/_Project/Scripts/Game/UI/Link/LinkEditorView.cs`, `Link/LinkRow.cs`, `UI/BuildingPanelAnchor.cs` (#2: `SetWidth`)
- `Assets/_Project/Scripts/Game/UI/Progression/PavilionPanelView.cs` (#3), `Progression/DragonDialogView.cs` (#5)
- `Assets/_Project/Scripts/Game/UI/Meta/ConfirmDialogView.cs` (#4), `Meta/WelcomeModalView.cs` (#5)
- `Assets/_Project/Scripts/Game/UI/Build/BuildCard.cs` (#6)
- `Assets/_Project/Scripts/Game/Views/Buildings/ConverterFaceView.cs`, `FormationFaceView.cs`, `Game/Views/WorldLabels/PlatedText.cs` (#7)
- `Assets/_Project/Scripts/Game/UI/GameMenuView.cs` (#8)

## Verdict per screen

Default font "Pixelify + Jersey 10"; the other fonts are noted where they differ.

| Screen | 1080p | 720p | Phone 2400 | Notes |
|--------|-------|------|------------|-------|
| Build menu | pass | pass | pass | badge fixed (#6) |
| Stats (3 tabs) | pass | pass | pass | Press Start 2P Flow note touches the header (#10) |
| Help | pass | pass | pass | |
| Altar tree | pass | pass | pass | Press Start 2P status line wraps to two lines, fits |
| Menu / Settings | pass | pass | pass | Press Start 2P fixed (#8) |
| Quest panel | pass | pass | pass | Press Start 2P / VT323 icon gap (#9), minor |
| Recipe picker | pass | pass | pass | #1 fixed |
| Link editor | pass | pass | pass | #2 fixed |
| Bridge panel | pass | pass | pass | Press Start 2P tall (#11) |
| Pavilion panel | pass | pass | pass | #3 fixed |
| Confirm dialogs | pass | pass | pass | #4 fixed |
| Welcome / Dragon dialog | pass | pass | pass | #5 fixed |
| Hand chip, tooltip, toast | pass | pass | pass | |

Per font: Pixelify Sans pass (digit look-alikes by design, #13); Pixelify + Jersey 10 pass; Jersey 10 pass; VT323 pass (thin at 720p);
Press Start 2P pass with minor findings #9 to #12; Smooth pass. No major finding is open.

## Suggestions (not done)

- Quest icon gap and pill plate width per face (#9, #12) need a one-line design decision; both are local.
- A "Text size" option next to "Text font" in Settings (batch B #15, still open).
- A PlayMode test that opens each panel under each font and asserts no text rect leaves its panel (the shot loop here is the manual form).
