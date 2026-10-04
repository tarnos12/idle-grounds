# Parity audit: Unity presentation vs old-game UI/input/render

Scope: `Assets/_Project/Scripts/Game/**` and editor builders vs `old-game/js/ui.js` (U), `main.js` (M), `audio.js`, `index.html`, `style.css`; spec `docs/port/ui-input-render.md`.
Method: every finding below was confirmed by reading both sides. Unconfirmed items were dropped. Read-only audit, nothing was run in Unity.

Areas verified as matching (no finding): hold rates and ramps (feed 4->20/s over 200 ms, withdraw 1->5/s, ground-hold 400 ms delay, 100 ms click cooldown, 900/500/400 ms buzz throttles), PICKUP_R/LOCK_R type-lock, edge-pick redirect, rack redirect, latched feed-hold end rules, Q/E direction, B toggle, Shift sprint toggle (not a hold), 12 px/frame pan (22.5 cells/s, x2 sprint, undiagonalised), wheel step 0.25 over [1,3] with 0.2/frame ease, centre-anchored zoom, camera clamp rule, place-several with Shift, RMB cancel of placing/demolish, enemy-before-building order, fixture throttle, engine SFX triggers (harvest/hit/kill/craft/build/upgrade/unlock/dragon/ascend), sparks/floaters caps and physics, quest-reward floaters, Help text, Stats rows, Perk shop, Ascend dialog, welcome/offline tiers, autosave, mute persistence. No pool leaks on RunReset were found (all pools release via `seenFrame` or `ClearAll`).

## HIGH

### H1. Region zone overlays and region frame are not rendered in game
- JS: `drawRegionGround` ui.js:767-798 (no-build zones, generator-field tints with FIELD_TINT, enemy zone red, 2 px green region frame `C.frame`).
- C#: `Game/ZoneMarker.cs:36` draws zones only in `OnDrawGizmos` (Scene view); `Editor/WorldBuilder.cs:117-129` just attaches markers. No runtime renderer, no region frame.
- Differs: player never sees where clay/sand/stone/water fields, the fox/enemy zone or no-build areas are (gameplay-relevant: placement of generators, fox hunting ground). Region borders are also unmarked.
- Fix: at scene build (or a `ZoneViewSync`), spawn a SpriteRenderer quad per rect on a "Zones" sorting layer with the spec 2.1 colours (noBuild `rgba(74,222,128,.05)` + dashed edge; clay/sand/stone/water fills/edges; enemy `rgba(248,113,113,.07)`/.30) and a 2 px `rgba(74,222,128,.30)` frame per region (reuse `EdgeFrame`).

### H2. Quest target ring missing
- JS: `questTargetRect` / `drawQuestRing` ui.js:2263-2310 (pulsing gold ring/dashed zone frame on the active quest target).
- C#: absent. `QuestDef.target` exists (`Sim/Config/ConfigTypes.cs:334,360`) but nothing under `Game/` reads it (grep: no `QuestRing`, no `.target` use).
- Differs: the main new-player guidance (where to go) is gone.
- Fix: add `QuestRingView` (LineRenderer circle / EdgeFrame): resolve the target as in JS (fixture node by kind, enemy zone rect, dragon/altar/building by type, region unlocked, quest unfinished), pulse `k=0.5+0.5 sin(now/320)`, alpha `.35+.4k`, radius `max(w,h)*0.62+6+6k` px, zone variant dashed inset `6+4k`.

### H3. Build menu has no quest/milestone target rank (rank 0, goal glow, tooltip)
- JS: `buildTargets` ui.js:2181, `renderBuildMenu` 1513-1561 (rank 0 target first, gold border + glow, 🎯 badge, tooltip " (your current goal)").
- C#: `UI/Build/BuildMenuView.cs:99` ranks only new/afford/rest; `Sim.BuildTargets()` (`ProgressionSystem.cs:247`) is never called from `Game/` (grep). Class doc says "arrive with M5" - not done.
- Fix: in `Rebuild()` compute `Sim.Progression.BuildTargets()`, rank 0 for matches, add `BuildCard` target styling (gold border, glow, 🎯 pill) and append " (your current goal)" to the tooltip; include targets in the catalogSig so it reorders when the goal changes.

## MEDIUM

### M1. Click on a non-interactive building never falls through to vacuum
- JS: onMouseDown ui.js:3020-3071 (only altar/gate/converter/lantern/roster/withdraw-building `return`; any other building, e.g. a ghost or the Dragon, falls to node check then `groundNear` vacuum).
- C#: `Input/HandController.cs:180` returns for every building via `BuildingLeftClick`, which has no vacuum fall-through (220-258).
- Differs: with loose items within 64 px, left-clicking the Dragon, a ghost or a plain building (Mill-type default faces) does nothing instead of vacuuming.
- Fix: make `BuildingLeftClick` return bool (consumed); return false at the end (and when the building is a ghost) so `LeftDown` continues to the node/ground branches.

### M2. Mouse wheel zooms the camera when the pointer is over UI
- JS: `onWheel` is bound to `#world-viewport` only (ui.js:350, 3313); strips scroll sideways on wheel (3326).
- C#: `CameraController.cs:134-137` reads the global scroll axis; `PanSuspended` is set only by the Altar tree (`UpgradeTreeView.cs:83`). Scrolling the build strip, perk shop, help, stats, quest panel also zooms the world behind them.
- Fix: gate zoom on `!EventSystem.current.IsPointerOverGameObject()` (use `HandController.PointerOverUI`) and also suspend while any modal is open.

### M3. Ascension Gate face lacks the "+N ☯" preview
- JS: ui.js:1121 `Ascend · +N ☯` (`E.ascendReward()`).
- C#: `Views/Buildings/GateFaceView.cs:25` hard-codes "Ascend" (doc says "arrives M5"). `Simulation.AscendReward()` exists (`Simulation.cs:331`).
- Fix: `sub.text = "Ascend · +" + sim.AscendReward() + " ☯"` via change-guarded `ViewKit.Text` (☯ needs the emoji sprite asset).

### M4. Linked Gathering Stone does not show its accepted-item icons
- JS: ui.js:1100-1107 (up to 4 icons under the badge from `E.stoneAccepts`).
- C#: `Views/Buildings/FormationFaceView.cs` has no such row; `LogisticsSystem.StoneAccepts` (`LogisticsSystem.cs:92`) is unused by views.
- Fix: add a pooled `IconRow`/4 SpriteRenderers under the badge (icon `round(9*1.2)` px, 1 px gap), filled from `StoneAccepts(area,b)` (null/empty = hidden).

### M5. Unlock buttons became world signs mid-gap (behaviour change)
- JS: `renderUnlockButtons/positionUnlockButtons` ui.js:1312-1445: only locked regions adjacent to BOTH an unlocked region and the region under the camera centre; shown at the viewport edge on that side, so always one click away.
- C#: `Views/Unlock/UnlockSignSync.cs:93-110` places a sign at the gap centre for every locked/unlocked adjacency, regardless of camera. The player must pan to the middle of the border (46 cells along it) to pay.
- Fix: either keep as an intentional deviation (documented in the spec) or add a screen-space overlay variant that clamps the sign to the viewport side facing the region, as in the original.

### M6. Fish surface countdown label missing
- JS: ui.js:1257-1265 (red "x.xs" 10 px under the fish in the last 1 s or when hovered).
- C#: `Views/NodeView.cs` only does the bob (Refresh); no countdown.
- Fix: child TMP shown when `surfaceUntil-now <= 1000` or the node is hovered (HandController.CursorArea/LRow/LCol), text `((surfaceUntil-now)/1000).ToString("0.0")+"s"`, `#f87171` 800 11 px.

### M7. Per-frame allocations in Update/LateUpdate loops
All run every frame while visible (no culling, every building/node in every region is refreshed each frame):
- `Views/Buildings/IconRow.cs:30` builds a StringBuilder + key string on every `Set` (ghost needs, Dragon feed row, Altar job, every unlock sign).
- `Views/Buildings/ConverterFaceView.cs:69` calls `Sim.ConverterFace()` which allocates a face + lists per converter per frame (`ConverterSystem.cs:304`); `FuelRackView` and `BuildingTooltipView` can call it again.
- `Views/Buildings/BuildingView.cs:126` `BuildingNeeds(b)` per ghost per frame.
- `Views/Buildings/DragonFaceView.cs:77` `Wrap(StripEmoji(m))` (Split/List/StringBuilder) every frame while the murmur is shown, before the change-guard.
- `Views/Unlock/UnlockSignSync.cs:101` string concat per region pair per frame; `UnlockSignView.Refresh` builds `"p/q"` and `"pay x/y"` strings per frame.
- `UI/Meta/MetaBarView.cs:78` builds a key string every frame; `UI/Build/BuildMenuView.cs:65-77` `Sig()` StringBuilder + `Catalog()` every frame while open; `UI/Progression/UpgradeTreeView.cs:213` `UpgradeTree()` + Dictionary + StringBuilder every frame while open; `UI/Progression/PavilionPanelView.cs:93` StringBuilder every frame while open.
- Fix: cache by a cheap integer version (state hash / stock counts) and only rebuild strings on change; throttle the tree/menu/pavilion refreshes to 4-5 Hz or on events; cull building/node refresh to the camera rect + 4 cells (JS margin, U:597); give `Sim.ConverterFace` a reusable out object.

### M8. Right-hold is not ended when a modal opens
- JS: startLoop ui.js:3262 (`holdDone=true; holdFront=null` when any `.modal:not(.hidden)`).
- C#: `HandController.StepHolds` has no modal check; `CursorOver` goes false only if the pointer sits over a blocking element. Scrim-less modal states (dragon dialog appearing mid-hold while the pointer stays over the world behind it) keep feeding.
- Fix: in `StepHolds`, if `MetaUiController.Instance.AnyOpen || build.DragonDialogOpen || build.UpgradeTreeOpen`, set `holdDone = true; holdFront = null`.

### M9. Error buzz missing when a withdraw-hold empties
- JS: ui.js:3232 plays `error` (500 ms throttle) when the held withdraw yields nothing.
- C#: `HandController.cs:327` updates `lastWithdrawErr` but plays nothing ("M4 audio" comment left behind).
- Fix: `AudioService.Play("error");` in that branch.

## LOW

- L1. **Version badge tooltip missing**. JS main.js:14-17 sets `title = VERSION.desc`. C# `BottomBarView.cs:62` sets only the text. No custom tooltip system exists, so every `title` in the original (hand pill "Q: ... E: ...", buff pill desc, vow chip, link dot texts outside the editor, build card role outside the hint line) is missing. Fix: small hover-tooltip component.
- L2. **Vow chip missing** from the area pill. JS ui.js:503,540-546 (vow icons, names in tooltip). C# `MetaBarView` has the ascension tag only (it adds `×speed`, which is extra). Fix: icon chip from `MetaText.ActiveVows`.
- L3. **Debug button and per-node counters missing**. JS ui.js:1250-1256, 1774, index.html `#debug-btn`. C# has no Debug toggle (node `clicks/clicksPerDrop` / `hitsLeft` gold counters). Fix: bottom-bar toggle + TMP above nodes, or drop deliberately.
- L4. **F9 diagnostics**: `Diagnostics` action is bound (`IdleGroundsControls.inputactions`) but unused. Spec says drop it: remove the action.
- L5. **Esc priority differs**. JS ui.js:3178-3196: tree > recipe > roster > link > dragon > perk > ascend > stats > help > welcome > ending > cancel modes. C# `BuildController.Escape` (~line 125) runs all M7 modals (confirm, perk, help, stats, post-ascension, ascend, welcome) BEFORE tree/recipe/roster/link/dragon. Only visible when two layers are open at once (e.g. Help over the tree). Fix: reorder, or accept.
- L6. **Welcome Esc marks the intro seen**. JS `dismissWelcome` (Esc) does not set `introSeen` (only the Continue button does, main.js:11-13). C# `WelcomeModalView.Escape` -> `Continue` -> `MarkIntroSeen`.
- L7. **Hover name rules**. JS `updateHoverName` ui.js:2881-2892: only built buildings in unlocked regions, hidden for build/link/roster/recipe, shown while placing, name only. C# `BuildController.Update` (line 82) resolves `Hovered` in locked regions too; `BuildingTooltipView.cs:30` hides it while placing, shows ghosts as "Under construction" and adds a status line. Fix: gate on `runner.IsUnlocked`, `b.built`, and drop the placing check if strict parity is wanted.
- L8. **Sprint toggle works during offline replay**. JS ui.js:3153 ignores everything except WASD during replay. C# `CameraController.OnSprint` has no `runner.Replaying` check.
- L9. **Arrow keys also pan** (`IdleGroundsControls.inputactions` "Arrows" composite); the original binds WASD only. Q/E are also accepted with ctrl/alt (JS ignores them with modifiers).
- L10. **Start camera and aspect**. JS recentre (ui.js:284-289): view top = region top, 16:9 letterboxed view. C# `CameraController` startRow=12 (view centre row 12 vs ~9.8 in JS at zoom 2) and view height follows the window aspect, so ultrawide/narrow windows see less/more world than the original. Fix: startRow ~ `VIEW_H/2/32`, optional pillarbox to 16:9.
- L11. **Swing sparks position/trigger**. JS `fxSwing` at the cursor on every click, including throttled ones (ui.js:3003,3028 etc.). C# `FxService.OnNodeHit` (line 98) sparks at the node centre and only for counted hits.
- L12. **Region pill in void**. JS `regionAtCamCentre` -> "Wilds" in gaps (ui.js:377, 498-499). C# `BottomBarView.cs:82` uses `RegionAtOrNearest`.
- L13. **Dragon murmur not clamped to the view** (JS drawDragonSpeech ui.js:826-840 clamps x into the viewport).
- L14. **Recipe detail seconds format**. JS `+secs.toFixed(1)` -> "3s"; C# `RecipePickerView.cs` always "3.0s" (trivial).
- L15. **Link picking in the void**. JS ignores clicks outside any region (it only handles `active` regions, ui.js:2967-2999); C# `HandController.cs:156` routes `area==null` into `LinkEditorView.HandleWorldClick`, which floats "Wisps can't cross the void" and sets the warning row.
- L16. **Help text says "glowing 🔓 button on the edge"** (HelpText verbatim) but the Unity UI uses world signs (see M5). Update copy if M5 stays.
- L17. **Extras not in the original** (harmless, list for awareness): hover/selection outlines on buildings, "{name} slain" floater (`EnemyViewSync.OnKilled`), "Paid"/"unlocked!"/"Carry the cost" floaters on signs, "Can't demolish" floater, region name TMP labels above regions (`WorldBuilder.cs:145-153`), enemy HP bar when maxHp > 16, lantern beat bar, tree drag-pan.
- L18. **Veil lacks the 🔒 glyph** (JS `drawRegionVeil` ui.js:1299-1310 draws 64 px 🔒 at region centre; `WorldBuilder.cs:131-141` only a 0.55 black quad).
- L19. **Event hygiene**: `GameRunner.Awake` (line 66) subscribes `Sim.Events.RunReset` and never unsubscribes (same lifetime as Sim, harmless). `FxService.Start` dereferences `runner.Sim.Events` without a null check (NRE if the GameDatabase is missing and GameRunner disabled itself).
- L20. **Static `CameraController.PanSuspended`** survives Enter-Play-Mode-without-domain-reload; reset it in `Awake`.

## Counts
High 3, Medium 9, Low 20 (32 total).

## Resolution (2026-10-04)

Every finding was re-checked against `ui.js` / the C# before fixing. Verified in Play mode (no console errors), EditMode 133/133 green. Rebuild-all (`Idle Grounds/Scene/Rebuild Core Loop + Buildings (M2+M3)`) reproduces everything; `Game.unity` saved. Screenshots: `Screenshots/parity_zone_overlays.png`, `parity_quest_ring.png`, `parity_build_goal_card.png`, `parity_gate_reward.png`.

| ID | Status | Notes |
|----|--------|-------|
| H1 | **fixed** | `WorldBuilder.BuildZoneOverlays` (run by CoreLoopBuilder install) builds `Region_*/ZoneOverlay` from the config: noBuild / FIELD_TINT generator fields / enemy zone fills + 1 px dashed edges, 2 px region frame (Ground sorting layer, orders 10-12). Note: the project's linear colour space makes the 5 % fills read a little stronger than the sRGB-blended canvas. |
| H2 | **fixed** | `Views/QuestRingView.cs` (Runtime/QuestRing): `Sim.QuestTarget()` at 4 Hz, pulsing gold segment ring / dashed inset zone frame, culled off camera. |
| H3 | **fixed** | `BuildMenuView` ranks `Sim.BuildTargets()` first (rank 0), signature includes targets; `BuildCard` gold border + glow Outline + 🎯 badge; hint gets " (your current goal)". |
| M1 | **fixed** | `BuildingLeftClick` returns consumed; other buildings (Dragon, ghosts, plain) fall through to node / vacuum. |
| M2 | **fixed** | Wheel zoom ignored over UI (`IsPointerOverGameObject`) and while an M7 modal is open. |
| M3 | **fixed** | Gate face "Ascend · +N ☯" (`AscendReward()` at 4 Hz, text rebuilt on change). |
| M4 | **fixed** | `FormationFaceView`: up to 4 accepted-item icons (11 px, 1 px gap) under a linked stone's badge (`StoneAccepts` at 4 Hz). |
| M5 | **accepted deviation** | World signs in the gap stay (proper scene objects); the original's camera-region adjacency filter is intentionally not reproduced. Help copy updated (L16). |
| M6 | **fixed** | Node prefab `Countdown` TMP (#f87171, 11 px, 10 px under the base) in the last second or while hovered; cached "x.xs" strings. |
| M7 | **fixed** | Culling to camera rect + 4 cells for buildings / nodes / unlock signs (`ViewCull`); IconRow value-compare (no key string); converter face sampled at 5 Hz with per-frame progress extrapolation + change-guarded text, fuel rack reuses it; ghost needs computed inline; dragon murmur wrapped only on change; sign pair keys are ints, cost/pay sampled at 4 Hz; MetaBar int/bool compare; build menu bitmask signature at 4 Hz; Altar tree refresh 5 Hz (pan + tooltip per frame, reused dictionary); pavilion panel 5 Hz; also Generator / Pavilion / Storehouse / Formation badge strings change-guarded. Remaining allocation is inside Sim (see below). |
| M8 | **fixed** | `StepHolds` ends a right-hold when any modal (M7 / dragon dialog / Altar tree) is open. |
| M9 | **fixed** | Error buzz when the withdraw-hold empties. |
| L1 | deferred | Needs a generic hover-tooltip component for all `title`s; not a cheap win. |
| L2 | **fixed** | Vow icons appended to the area-pill tag (shown when ascended or vows active); also removed the duplicated ☯ in that tag. Names-in-tooltip waits on L1. |
| L3 | deferred | Debug toggle + node counters are a dev aid; not ported. |
| L4 | deferred | Removing the F9 action means regenerating `IdleGroundsControls.cs`; harmless while unused. |
| L5 | **fixed** | Esc order now: confirm (C# only) → tree → recipe → roster → link → dragon → perk → ascend → stats → help → welcome → post-ascension → cancel modes. |
| L6 | **fixed** | Welcome Esc only hides; Continue marks the intro seen. |
| L7 | **fixed** | Hover name only for built buildings in unlocked regions, shown while placing; status line kept as a C# extra (sampled 4 Hz). |
| L8 | **fixed** | Sprint toggle ignored during offline replay. |
| L9 | **accepted / fixed** | Arrow-key pan kept (harmless extra); Q/E now ignored with Ctrl/Alt. |
| L10 | **partly fixed** | Start camera = view top on the Center region top, centred (`startTopAligned`). 16:9 pillarboxing deferred (window-aspect view kept). |
| L11 | **fixed** | Swing sparks at the cursor on every node click / auto-swing and at the enemy on every strike (HandController); sim-event sparks removed. |
| L12 | **fixed** | Area pill shows "Wilds" with the 🌫 icon in the void gaps. |
| L13 | **fixed** | Dragon murmur centre clamped 6 px inside the view. |
| L14 | **fixed** | Recipe seconds use `+toFixed(1)` formatting ("3s", "2.5s"). |
| L15 | **fixed** | Link picking only consumes clicks inside unlocked regions; void / locked clicks behave as in JS. |
| L16 | **fixed** | Help copy points at the gap sign. |
| L17 | n/a | Awareness list of extras; kept. |
| L18 | **fixed** | 64 px 🔒 at every veil centre (built with the overlays). |
| L19 | **fixed** | GameRunner unsubscribes RunReset; FxService.Start null-guards the Sim. |
| L20 | **fixed** | `PanSuspended` reset in `CameraController.Awake`. |

Sim-side follow-ups (not done here — Sim is owned by another agent): `ConverterSystem.Face` / `Simulation.ConverterFace` should accept a reusable face object (it still allocates at 5 Hz per visible converter); `LogisticsSystem.StoneAccepts`, `BuildingSystem.Status`, `ProgressionSystem.Target/BuildTargets`, `Simulation.UpgradeTree`, `AreaUnlockCost/UnlockPaid` allocate per call (views now sample them at 4-5 Hz).
