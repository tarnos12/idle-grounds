# Touch controls (Android phones, landscape)

Code: `Game/Input/TouchInputController.cs` (gestures), `Game/UI/MobileLayer.cs` (phone UI, runtime-built, scene untouched),
`Game/UI/MobileUi.cs` (dpi / safe area / UI hit test). Desktop mouse + keyboard are unchanged; gestures only react to a `Touchscreen`.

## One finger (a touch that starts on UI belongs to the UI and never reaches the world)
| Gesture | Result |
|---|---|
| Tap | left click at that point: node = one swing, enemy = strike, building = panel / withdraw 1, ground = vacuum pulse |
| Drag (moves > 10 dp before the hold fires) | pan the camera |
| Hold still 0.22 s | left hold: keep harvesting / attacking / vacuuming / withdrawing; the finger then moves the cursor (sweeps the vacuum). A hold on bare ground with nothing to vacuum is not engaged: moving then pans |
| Hold still 0.38 s on a building (or ghost / dragon / Altar) with items in hand | right hold: feed / drop with the ramp, same building kept while the finger stays on it |
| **Give** button on = every touch is a right click | tap drops / feeds one, hold ramps, drag moves the cursor |
| **Swap** button | rotate stacks (Q / E) |
| Build / demolish | card picked -> drag on the island (ghost sits above the finger), lift to leave it, **Place** / **Demolish** button confirms (red reason line when blocked), **Cancel** cancels |
## Two fingers
Pinch = continuous zoom anchored under the fingers (1..6), two-finger drag = pan. A running hold is released first.
## Info / back
Tooltips show on press and linger 2.4 s after a tap; world hover info lingers 2 s after a tap on a building. Android Back closes the topmost panel, else opens the Menu (jade panel, 3-column grid: Resume / Help / Stats / Sound / Motion / Keep awake / New game (confirm, wipes the save, starts with NO buildings) / Save and quit; it fits above the bar inside the safe area).
## Phone UI
CanvasScaler: match = height, reference height = 200 logical units per physical inch (520..760), so a phone shows ~533 units tall. HUD sits in a safe-area root (scrims still bleed to the edge). Controls under 48 dp get a 48 dp raycast padding (visual unchanged). Modal boxes shrink to fit. Quest panel starts collapsed and expands at 85 % scale. `EventSystem.pixelDragThreshold` = 12 dp.
Settings: 60 fps, vSync off, landscape L/R, saves on `OnApplicationPause` (SaveService).
Tests: `Tests/PlayMode/TouchInputTests.cs` (InputTestFixture + Touchscreen), `Tests/Game/MobileUiTests.cs`. Phone screenshots: `bash tools/bg/shot.sh phone-2400` / `phone-3200` (spec fields `mobileDpi`, `inset*`).
