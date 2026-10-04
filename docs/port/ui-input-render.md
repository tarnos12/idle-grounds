# Presentation layer spec: rendering, input, UI, FX, audio (from old-game)

Source of truth: `old-game/js/ui.js` (U), `main.js` (M), `audio.js` (A), `index.html` (H), `style.css` (S), `data.js` (D), `engine.js` (E).
Line numbers refer to those files. `CELL` = 32 px (D:269). "World px" = 1/32 cell unless stated.
Items use emoji as fallback sprites plus 16 px pixel-art PNGs (`assets/icons/<key>.png`, 23 files) drawn with smoothing OFF (U:60-84, 195-199). Buildings, nodes and enemies are emoji only.

---------------------------------------------------------------------
## 0. World geometry (U:14-27, D:268-274, 483-508)
- GRID: cell 32, cells 93 (play cells per region side), margin 10 (inert border cells around the whole map), gap 5 (void cells between regions).
- PLAY_W = 93*32 = 2976 px. GAP_PX = 160. OFF (margin px) = 320.
- Region origin (cells) = (ry*98, rx*98) (E:2213); px = cell*32 + OFF (U:258). Region cell lookup E:2220 (null in gaps/margins).
- Region grid (rx,ry): farm(0,0) center(1,0) mine(2,0) grove(0,1) fishing(1,1) volcano(2,1) celestial(1,2). cols=3 rows=3.
- WORLD_W = WORLD_H = (3*93+2*5+20)*32 = 9888 px.
- Unlock costs (D:500-507): farm wood10; mine wood16; fishing wood20; volcano iron_bar3; grove wheat12+wood8; celestial spirit_stone6+jade3+glass3 (paid from hand; engine applies the frugal perk).
- Footprints (cells): default 3x3; Altar, Dragon, Ascension Gate 5x5; burners (Kiln, Forge, Pill Furnace, Star Anvil) 3x5; Furnace Spirit, Gathering Stone, Wisp Lantern, Warding Seal 1x1. Node `size` 1..4 (quarry 2, spring 2, Spirit Tree 4).

---------------------------------------------------------------------
## 1. Camera (U:245-354, 3118-3143)
- Camera = top-left of the view in world px. View is 16:9. **View width = 560 * zoom px**, height = width*9/16. Zoom 1 = 560x315 px (17.5 tiles wide, closest); 2 = 1120x630 (default); 3 = 1680x945 (furthest).
- Start: top-centre of the centre region: `cam.x = center.x + PLAY_W/2 - VIEW_W/2`, `cam.y = center.y`, then clamped (recenterCamera U:284).
- Pan: WASD held, per animation frame **12 px/frame, 24 with sprint** (assume 60 fps -> 720 / 1440 px/s; use deltaTime). Diagonals are not normalised. **Shift is a toggle** (keydown, ignoring auto-repeat), not a hold; shown as "🏃2×" in the bottom bar. WASD calls preventDefault. While the Altar tree is open WASD pans the tree instead of the map. During an offline replay only WASD works.
- Zoom: wheel over the world viewport: target += 0.25 for wheel-down (zoom OUT), -= 0.25 for wheel-up, clamped [1,3] (so 8 steps). Smooth: each frame `zoom += (target-zoom)*0.2`, snap when |d|<0.005. Zoom keeps the **view centre anchored** (cam += (oldW-newW)/2, same for H) then clamps. Touch-only (+)/(-) buttons nudge the same target by 0.25 (U:3320). `setZoom(z)` is the instant variant.
- Clamp (allowedBox U:265): bounding box of all **unlocked** regions' play rects expanded by 160 px (GAP_PX) each side and intersected with [0,WORLD]. cam.x in [x0, max(x0, x1-VIEW_W)], same for y. The view edge may reach the border of a locked neighbour but never show inside it (when the view is larger than the box, it pins to x0/y0 and a sliver of locked region may show). Re-clamped after pan, zoom, region unlock.
- Viewport fit: renderedW = clamp(min(innerWidth-36, (innerHeight-barHeight-36)/(9/16)), 320, 1440); viewScale = renderedW/VIEW_W (CSS px per world px). Backing store = whole device pixels.
- Cursor->world (pointFromEvent U:357): world = cam + (mouse - canvasTopLeft)/viewScale (clamped into world); then region, local px, local cell.
- Location pill: region under the camera **centre** (cameraRegion falls back to nearest region centre when the centre is in a void gap).

---------------------------------------------------------------------
## 2. World rendering (U:548-1306). One 2D canvas, repainted on demand.
Self-chaining repaint while `animActive()` (U:407): node hit <200 ms, surfaced fish, AUTO flash, any enemy or wisp on screen, a working converter on screen, a gathering stone actively pulling, dragon message, buff countdowns, FX alive, quest ring visible. Built Gate pulse repaints every 150 ms. Game tick 50 ms (M:116), automation tick 1000 ms (M:121), autosave 5 s (M:110). Culling margin = 4 cells beyond view (U:597).

### 2.1 Layer order (drawWorldInner U:570-675)
1. Void `#161b16`; world-aligned grid lines every 32 world px, `rgba(255,255,255,.05)` 1 px, only if 32*viewScale >= 7.
2. For every visible region: **ground** (U:767): region tint; no-build zones (`E.noBuildRects`) fill `rgba(74,222,128,.05)` + dashed (4,4) 1 px edge `rgba(74,222,128,.18)`; generator-field zones tinted by produced item (clay fill `rgba(184,115,66,.30)` edge `rgba(220,190,120,.4)`; sand `rgba(226,201,126,.28)`; stone `rgba(148,163,184,.22)`/`.4`; water `rgba(96,165,250,.28)`/`.45`; unknown -> sand) with dashed edge; enemy zone fill `rgba(248,113,113,.07)` edge `.30`; region frame `rgba(74,222,128,.30)` 2 px.
3. **Reach circles** (U:702): under sprites; radius = reach*32; fill `rgba(rgb,.07)`; dashed (6,5) stroke `rgba(rgb,.5)` 1.5 px. Gathering Stone rgb 74,222,128 radius `gather.radius`; Furnace Spirit rgb 251,146,60 radius `stoker.radius + 1.5` cells. Shown: hovered built stone/spirit; every stone of a region while that region's lantern link menu is open; placement ghost of a stone/spirit.
4. Locked regions: their objects, then veil `rgba(0,0,0,.55)` + 🔒 (64 px) at centre.
5. Unlocked regions: objects (link threads, buildings, nodes, enemies).
6. Unlocked regions: items (ground items, wisps) - on top of all objects.
7. Quest ring, "Skipping" chips, link rubber-band, placement preview, FX.
Nodes within a region sort ascending by row (back to front).

### 2.2 Region ground colours (U:44)
center `#25351f`, farm `#3a3318`, mine `#2c2c33`, fishing `#16323b`, volcano `#3a1c17`, grove `#1e3a2b`, celestial `#231d40`.

### 2.3 Lantern link threads (U:1007)
Dashed (6*s,6*s) line between building centres, width max(1,1.5*s); `rgba(251,191,36,.22)`, `.65` for the lantern being edited. Drawn under buildings.

### 2.4 Buildings (U:1030-1169). Border width max(1.5, 2*s/0.7) with s = viewScale.
Fills: ghost (unbuilt) `rgba(74,222,128,.10)` + dashed (6,4) `#4ade80`; built `rgba(60,70,80,.95)` + `#3c4651`; Altar `rgba(74,60,30,.95)` + gold `#fbbf24`; Dragon `rgba(52,36,70,.95)` + `#a855f7` (gold once awake). Label sizes use `lblPx(size,s)=max(size*s,10)` (never below 10 CSS px).
- **Altar** (built): 🏛️ 56*s at 38% of height; name 800 24*s at 68%; with an upgrade job: gold remaining-cost line (20*s) at 86%, else muted "Select an upgrade" 700 lblPx(16).
- **Dragon** (built): 🐉 (🐲 awake) 64*s at 36%; name "Sleeping Dragon" `#d8b4fe` / "Awakened Dragon" gold, 800 18*s at 64%; not awake: gold "Feed: qty icon ..." lblPx(15) at 84%; awake: muted "watches over the grounds". Murmur (`GS.dragon.msg` while `msgUntil>now` and no dialog): `#e9d5ff`, 800 lblPx(14), wrapped 42 chars x 3 lines, last baseline 12 px above building top, shadow, clamped to view.
- **1x1 formations** (stone/lantern/seal/spirit, built): icon 20*s centred; seal shows its item icon 13*s 8 px above; badge below the tile (+8*s): stone/spirit `qty/cap` (red at cap, else gold), seal `qty` (cap default 5), lantern `N⛓`; 800 lblPx(9). A linked stone shows up to 4 accepted-item icons under the badge.
- **Ascension Gate** (built): pulse `p=0.5+0.5*sin(now/500)`; gold border width (2.5+p)*s/0.7, glow alpha 0.35+0.4p, blur (6+8p)*s; ⛩️ 56*s at 36%; gold name 800 lblPx(16) at 64%; "Ascend · +N ☯" `#fde68a` 700 lblPx(12) at 84%.
- **Converter** (U:905 drawConverterFace; whole footprint is the face, burner = 3x5): name row at y+8px (emoji + muted name 700 lblPx(9.5), ellipsised); inputs row at 30% height: icon 18*s, spacing 24 world px, centred; top-left corner count = in stock (green if >= need else red), bottom-right = need per craft (gold); result icon 22*s at 55%, bottom-right gold count = batches the stock can still make (fuel ignored); status line at 75.5%: starved red "Needs"+icon, nofuel red "No fuel", full amber `#f59e0b` "Output pile full"/"Pile full"/"Stock full", working green "Working · N/min" -> "N/min" -> "Working" (N from craftRate, shown if >0), idle blank; text ellipsised to width-4. Progress bar 1 cell wide x 4*s at 90%, track `rgba(255,255,255,.15)`, fill gold, frac = 1-(smeltDoneAt-now)/dur with dur = recipe.timeMs * prestigeFactor (x0.5 on a burner while Ember blessing is active).
- **Fuel rack** (burners, U:957): 3 cols x 2 rows (96x64 px), OUTSIDE the footprint on the LEFT (x = building.x - 3 cells), top edge flush with the building top. Panel `rgba(40,28,18,.82)`, border `rgba(251,146,60,.5)`, dividers `rgba(255,255,255,.07)`. Slots filled oldest -> newest in order (row,col) = (0,0),(0,1),(0,2),(1,0),(1,1),(1,2); icon = 0.8*min(cell). Slot 0 = burning item: ghost icon at 22% alpha plus the real icon clipped to its left `rem/total` fraction (burns right to left). Empty: red "No fuel" 800 lblPx(10) just above the rack.
- **Storehouse** (built): item icon 26*s (or 📦 24*s if empty) at 40%; label "Item ×qty" or "empty" 700 lblPx(10) at 78% (red when qty >= cap).
- **Meditation Pavilion**: icon 24*s at 28%; "👤 d/cap" 700 lblPx(11) at 50%; status line at 67%; bun bar at 84% (70% width, 4*s high; track `rgba(255,255,255,.12)`; green fill, red when 0) = buns/foodCap.
- **Default building / ghost**: icon 24*s at 38% (alpha .7 if ghost); name 700 lblPx(10) at 66%; ghost: gold needs line ("qty icon ..." of what is still missing, `drawNeedsLine` U:173: 800 font, icon = 1.35*px, gap 0.3*px) at 86%. Construction = ghost with remaining needs; no bar.

### 2.5 Nodes (U:1203-1272)
Anchor = bottom-centre of node square, 2 px up. Interactive nodes get a pad ellipse `rgba(74,222,128,.16)` (rx 0.42*w, ry 7). Sprite emoji = `node.sprite || TIER_SPRITES[region][0] || region icon`, size `spriteSize`: size>=4 -> size*30 (Spirit Tree 120), 3 -> 90, 2 -> 72, 1 -> 30 world px; alphabetic baseline at anchor. Deco border trees: 30*(decoScale||1.8), alpha .55, offset (decoDx,decoDy), inert.
Hit squash (U:553) 180 ms from `hitAt`: sy 1 -> 0.84 (35%) -> 1.06 (70%) -> 1.0, sx = 1+(1-sy)*0.4, pivot at the ground anchor. Fish (`surface` interaction, surfaceUntil>now): bob `-|sin(now/300)|*5` px; countdown "x.xs" red 800 lblPx(11) 10 px under, only in the last 1 s or when hovered. Spirit Tree: three ✨ at 22% size at (-0.28,-0.72),(0.30,-0.55),(0.05,-0.92) x fontPx. "AUTO" gold 800 lblPx(9) above top-right while `autoFlash>now`. Debug button shows `clicks/clicksPerDrop` / `hitsLeft` counters gold above the node.

### 2.6 Enemies (U:1176)
Emoji (`en.sprite` or area default) 26*s (boss 36*s) centred, same squash about the centre. HP pips: `maxHp` circles r=3*s, spacing 10*s, centred 24 px above; remaining red `#f87171`, lost `rgba(255,255,255,.25)`.

### 2.7 Ground items & wisps (U:1275)
Ground item = single icon 20*s centred; **no stack numbers** (piles are many separate items). Wisp: position `E.wispPos(area,wisp,now)` (interpolated from departure time); glow circle r 7*s `rgba(74,222,128,.30)` (**`rgba(248,113,113,.35)` red when `returning`** = delivery refused), core `#eafff2` r 2.5*s, cargo icon 14*s 12*s above.

### 2.8 World overlays
- Placement preview (U:639): footprint rect with the hovered cell as the TOP-LEFT cell; ok = fill `rgba(74,222,128,.25)` / stroke `#4ade80`, bad = `rgba(248,113,113,.25)` / `#f87171`, 2 px. Bad also shows a reason pill (`rgba(40,10,10,.9)`, radius 6, text `#fecaca` 800 lblPx(11)) below the ghost (above near bottom edge): "Region locked", `E.placeReason(...)`, or "Blocked". **No rotation for buildings** (Q/E rotate the hand).
- Link rubber-band: dashed (8,6) `rgba(251,191,36,.8)` 2 px from source centre to cursor while picking a target.
- Quest ring (U:2289): k=0.5+0.5*sin(now/320); stroke `rgba(251,191,36,0.35+0.4k)` width max(2,3*s); building/fixture: circle radius max(w,h)*0.62+6+6k around the rect centre; enemy zone: dashed (10*s,8*s) rect inset 6+4k. Target = `QUESTS[idx].target` {kind fixture|enemyZone|dragon|altar|<building id>, area}; only while the quest is unfinished and the region unlocked.
- "Skipping [icons]" chip (U:744): amber pill (fill `rgba(60,40,8,.92)`, stroke `#f59e0b` 1.5, text `#fde68a` 800 lblPx(10)), top-left of region (+6,+6 screen px), up to 4 icons at 1.4x, for `area._autoSkip` items; skipped if visible region width <40 px.

---------------------------------------------------------------------
## 3. Input (U:2856-3351, M:1-163)
All world mouse handlers gate on the pointer being inside the viewport. Button 2 = right.

### 3.1 Keys (onKeyDown U:3144)
| Key | Action |
|---|---|
| W A S D | pan 12 px/frame (tree pan when Altar modal open) |
| Shift | toggle sprint (2x), ignore repeat |
| B | toggle build menu (opening clears placing + demolish) |
| Q | `handRotate(+1)`: front stack to the back |
| E | `handRotate(-1)`: back stack to the front (no ctrl/meta/alt) |
| Esc | chain: close tree -> recipe menu -> roster -> link menu (picking first backs out to menu) -> dragon dialog -> perk shop -> ascend modal ("keep playing") -> stats -> help -> welcome -> ending -> else cancel placing/demolish + close build menu |
| F9 | alert() JSON diagnostics (dev only, drop it) |
| wheel | zoom (viewport); horizontal scroll on build/link/roster strips |
Demolish and Debug are buttons only. Keys other than WASD are ignored while an offline replay runs.

### 3.2 Mouse (onMouseDown U:2913)
`cursor` = {screen cx,cy; over; region; local lx,ly; lrow,lcol}, refreshed on mousemove and every pan frame.
Constants: PICKUP_R 64 px (2 cells); LOCK_R 24 px; EDGE_PICK_PX 10; EDGE_PICK_FRAC 0.15; EDGE_ITEM_PX 14; CLICK_COOLDOWN 100 ms; GROUND_REPEAT_MS 400.

**Right button = drop / feed** (U:2919):
1. If placing or demolish mode: cancel it and stop.
2. Ignore in locked/void.
3. `rackRedirect`: a point in a built burner's rack zone (rows b.row..b.row+1, cols b.col-3..b.col-1, only when no real building is there) maps to the burner's footprint centre.
4. Target building tb at the redirected point: latch `holdTarget {region,id,wasBuilt,dragonStage}`; else `holdFront` = front hand stack's item.
5. One immediate `E.dropFromHand(region,x,y,noGround=!!tb)`. null = refusal: error SFX + red "✗" floater. Result `.once` (quaffed pill, bait lure) or `.used` ends the hold.
6. Loop (below). **Latched feed-hold** (began on a building): repeats only while the cursor stays on that same building (rack redirect counts); rate **4 -> 20 per second ramp over 200 ms** (`4+min(elapsed/200,1)*16`, interval 1000/rate); never falls through to the ground; ends on null/once/used, dragon stage change, ghost completed, or a modal opening; refusal buzz max every 400 ms (+ "✗"). **Ground-hold** (began on open ground): immediate drop; auto-repeat only after 400 ms then the same ramp (ramp clock starts after the 400 ms); stops when the front stack empties or its item changes (release and press again); paused while the cursor is over a building/rack; `once` stops it.
- Right-click on a region unlock button pays like left click.

**Left button**, first match wins (U:2940-3072):
1. Placement mode: `E.placeBuilding` at hovered cell (top-left); success clears placing unless **Shift held (place several)**; failure = error SFX.
2. Demolish mode: `E.demolishBuilding` on the building under cursor (refund drops); demolish mode always ends.
3. Locked region: error SFX + red floater "🔒 Unlock this border first".
4. Link picking (lantern editor) captures all world clicks: wrong region -> hint row + floater "Wisps can't cross the void" + error SFX; source needs `canBeLinkSource`; target needs `canBeLinkTarget`, != source; `E.linkRefusal` -> floater with reason, stay in target picking; else `E.addLink`.
5. Enemy at cursor (`E.enemyAt`; before buildings): `E.attackEnemy` if >=100 ms since last click else just flinch (`hitAt`); spark; starts attack-hold.
6. Building at cursor (unless `edgePickRedirect`: building >1x1, point within min(10 px, 15% of the smaller side) of its edge, loose item within 14 px, hand has space -> vacuum instead): built Altar -> open tree; built Gate -> ascend modal; converter -> recipe picker; lantern -> link editor; pavilion -> roster; storehouse/seal/gathering stone -> **withdraw** (immediate `withdrawFromBuilding(sh,1)`, then hold; empty -> error SFX throttled 500 ms). One building panel at a time; clicking elsewhere closes them.
7. Non-deco node: `E.harvestNode(region,id,false)` if 100 ms since the last click and (for `fixed` fixtures: Spirit Tree, quarry rock, spring) >= `harvestInterval` since the last counted fixture hit; else visual `hitAt` only. Spark at cursor. Starts harvest-hold.
8. Ground item within 64 px: vacuum hold. If the press starts within 24 px of an item the hold is **type-locked** to it. Hand full -> "Hand full" nudge. Immediate `suctionStep`.
**Left-hold loop** (rAF): vacuum each frame `E.suctionStep(region, cursor, 64, filter)` (items in range slide toward the cursor, collected within 12 px); picked>0 -> pickup FX + "pickup" SFX; hand full with items in range -> "Hand full" red floater + error SFX at most every 900 ms. Withdraw: **1 -> 5 per second over 200 ms** (`1+min(elapsed/200,1)*4`), each success shows pickup FX, emptied buzzes (500 ms throttle). Attack-hold: re-hit-test enemy under cursor every `area.enemies.attackMs` (default 400 ms). Harvest-hold: node under cursor swings every `harvestInterval` = max(120, (node.swingMs||350) * 0.8^harvestSpeedLevel * prestigeFactor) ms (fixtures also on their own timer); full hand stops auto-swing (single click still harvests). Release clears all left state.

### 3.3 Hover / cursor display
- OS cursor over the viewport: crosshair; tree canvas pointer over selectable nodes.
- **Hand chip** (U:1467; S:274): follows mouse (+14,+14); each hand stack "qty icon" in order, first stack accent 12 px text / 18 px icon, others muted 16 px icon; bg `rgba(20,25,30,.9)`, 1 px accent border, r=8. Hidden when hand is empty, pointer over quest panel / recipe menu+info / build, link, roster strips / bottom bar, or any modal is open.
- Hover name: built building under the cursor, 16 px 800, bottom-centre (76 px above window bottom); hidden while build/link/roster/recipe panel or a modal is up; "Awakened Dragon" when `GS.won`.

---------------------------------------------------------------------
## 4. UI panels
Style: dark panels, radius 8-14, 1 px `#3c4651` borders. Modal = scrim `rgba(0,0,0,.55)` + centred box (bg `#232a31`, r=14, width min(560px,92vw), max-height 86vh, padding 18).

### 4.1 Bottom bar `#topbar` (H:36, S:43, U:497)
At the BOTTOM of the window. Left: "🌍 Idle Grounds" 17 px 800 + **version badge** `v52` (11 px 700 muted, bordered chip, tooltip = `VERSION.desc`; M:41). Right (gap 10), in order:
1. Area pill "📍 {icon} {name}[ 🔒]" + tags "🏃2×" (sprint), "☯N" (ascensions), vow icon chip.
2. Hand pill "✋ n/cap": green; gold when >=90%; red at cap.
3. Buff pill (hidden unless active): `rgba(168,85,247,.18)` + `#a855f7` border; "{icon} {Blessing} {s}s" and "{💊} Martial Vigor {s}s"; per-second countdown.
4. "☯ N" Shrine pill: hidden until ascended / AP>0 / Gate built; gold glow when any perk is affordable.
5. Mute 🔊/🔇 (persisted; plays "click" on unmute). 6. "❓ Help", "📊 Stats". 7. touch-only "−"/"+" zoom. 8. "🔨 Build" (toggle; gold dot when unseen revealed buildings), "🗑 Demolish" (toggle; red when on), "🐞 Debug" (toggle), "↺ Reset" (red text; confirm "Reset ALL progress and start over?" then clear save + reload).
Buttons: panel fill, 1 px line border, r=8-10, hover panel-2, "on" = accent-dk fill + accent border.

### 4.2 Build menu (U:1513; S:297-311, 593-607)
Horizontal strip above the bottom bar; wheel scrolls sideways. **No categories/tabs.** One card per revealed building (`E.buildingCatalog()`): icon 24, name 13/700, cost "qty icon ..." 11 gold, min-width 110, r=10. Sort rank: 0 = quest/milestone target (🎯, gold border+glow), 1 = "new" (gold pill, cleared on hover), 2 = affordable from hand (green border), 3 = rest (opacity .62); stable by catalogue order. Tooltip "{name} — {role}" (+ " (your current goal)"), role from `buildRole` U:1496. Empty text "Nothing to build yet — follow the 📜 quests to unlock buildings." Click -> `GS.build.placing=id`, menu closes, placement mode. If the revealed set changes while open, card clicks are ignored for 400 ms. Reveal driven by `DATA.REVEAL`.

### 4.3 Recipe picker + detail (U:1790-1887; S:329-371)
Left-click a built converter. Popup centred above the building top, clamped on screen: bg `#232a31`, r=14, padding 14. Title "Recipes" 14/800; grid 3 columns of 58x58 cells (gap 8) showing each recipe's output icon (32 px); active = gold border + glow; hover = accent border; same-output sibling recipes get a 14 px badge (top-right) with the input item that differs. Click = `E.setRecipe` + close (engine drops held stock on change). Hover -> detail popup (224 px) right of the picker (flips left if off-screen): output icon 42, name 16/800, gold yield line "→ {qty}× {icon} {Item} · {secs}s" (secs = timeMs*prestigeFactor/1000, 1 decimal), then a row per input: 26 px icon with gold count badge + item name 13.

### 4.4 Lantern link editor (U:1920-2010; S:314-327)
Left-click a built Wisp Lantern -> wrapping strip (max-height 40vh): title "🏮 Wisp Lantern — links run in order, one per beat"; button "➕ Add link" or prompts "Click the SOURCE building on the map (gatherer / seal / storehouse)… Esc cancels" then "{src} → click the TARGET building… Esc cancels"; red warnings "Wisps can't cross the void between regions" / "{reason} — pick another target"; then one row per link: `● n. {from} → {to} ✕`. Dot colour (from `lk._stat`): red = target refused, amber = source empty / source holds nothing the target uses, green (glow) = sent in last 3 s, grey `#64748b` = idle; each has a tooltip text. Stone label = icon + top buffer icon + live count; others "icon name [typed item icon]". ✕ = `E.removeLink`. World: the lantern's threads highlight and all stones show reach circles.

### 4.5 Roster (U:1889)
Strip: "🧘 Meditation Pavilion", "👤 Disciples d/cap  {bun icon} buns/foodCap", hint "Each cultivates {icon} while fed {food icons} (feed by hand or wisp).", "➕ Recruit" costing 1 Robe from the hand (disabled: "Pavilion is full" / "Carry a Robe to recruit").

### 4.6 Storehouse / seal / stone
No panel. Left-click/hold withdraws; right-click deposits/retunes (engine `dropFromHand` onto the building). Contents shown on the world label (2.4).

### 4.7 Altar upgrade tree (canvas U:1555-1772; S:469-525; data D:520-583)
Modal width min(920px,96vw), height min(880px,92vh): "🏛️ Altar — Upgrades", "🐞 Debug" (reveal hidden) and ✕. Body canvas, no scrollbars, WASD pans. treeCam = screen position of node (0,0), starts at body centre on open; clamp x in [min(bw/2, bw-90-maxX), max(bw/2, 90-minX)] (same y).
- Board `#232926`. Edges: straight, width 5, `rgba(225,232,224,.45)` both ends owned else `.22`, only between visible nodes.
- Node: rounded square 52x52 (half 26), r=7, fill `#0d1113` (hovered+selectable `#171c1f`), border 3: green `#4ade80` selectable, gold maxed, red locked/mystery. Alpha .75 mystery; .6 visible-but-locked and not owned. Icon 24 px at (x, y-3); "lvl/max" 800 9 px in border colour at y+18. Mystery: red "?" 800 26. Hover/selected: corner brackets offset 32, arm 11, width 3, white (gold if selected and not hovered).
- Visibility (treeStates U:1564): BFS over undirected links from all owned nodes + root "hand": dist<=1 full, 2 mystery "?", >=3 hidden (unless debug). Selectable = full AND (root or owned or neighbour of owned).
- Hit box +-(26+4). Tooltip (instant, centred 12 px above node, 230 px wide, bg `rgba(8,11,13,.97)`, r=6): name 13.5/800, "Level: x/y", description muted, "Selected — fed a/b" gold, then "Cost: qty Name, ..." gold (top border) or white "MAX" bar; mystery = "???" / "Undiscovered upgrade".
- Click selectable non-maxed node -> `E.selectUpgrade(area,type)` and close; player then right-click-feeds the Altar (cost shown on Altar face). Switching refunds.
- Node positions (x,y px; -> links) (D:520-583): hand(0,0)->spd_c,act_c,spd_f,spd_m,foe_cap,wisps; wisps(-170,115)->affinity; affinity(-300,190)->disciples; disciples(-230,300); spd_c(150,-35)->auto_c; auto_c(300,-85)->act_fi; act_fi(455,-45); act_c(-150,-35)->quarry; quarry(-295,40)->act_m; act_m(-450,-15); spd_f(40,-150)->act_f; act_f(-45,-290)->auto_f; auto_f(55,-430); spd_m(-40,150)->spd_fi; spd_fi(50,290)->auto_m; auto_m(-40,430); foe_cap(160,120)->foe_dmg; foe_dmg(315,205)->foe_aoe; foe_aoe(470,300). Tree Y grows downward (screen space). Icons/names/descs/3-level costs live in D:520-583: import as ScriptableObject data, do not retype.

### 4.8 Ascension Shrine / perk shop (U:2511-2598; S:89-114)
Modal width min(560,94vw), sticky header: "☯ Ascension Shrine" + ✕; line "**N** Ascension Point(s) to spend · M ascension(s)" + "Head starts: ..." (`headStartsText` U:2498: regions opened by Remembered Paths, Legacy auto-L1 regions, hand +5/level, dragon tribute multiplier; "none yet ..." fallback). Preview mode when opened from the ascend modal: "You have N ☯ (+G on ascending)", "Next run — ...". Groups with small-caps headers: Pace [haste, regrow, gale], Economy [frugal, ember, bounty, hands], Combat [fury], Meta [apgain, hall, autoboost, slumber, bless], Legacy [paths, legacy], "Other". Card: icon 30, name 15/800 + gold "lvl/max", "★ good first pick" pill (haste, hands, paths), desc 12.5 muted, accent effect line "{label} now → next" or "(max)" (PERK_FX U:2525: haste timers ×0.95^l, hall +l, slumber 8+2l h, hands +5l, frugal ×0.8^l, ember ×0.85^l, apgain +l, autoboost +l, regrow ×0.9^l, gale ×0.9^l, fury +l, bless ×1.2^l, bounty ×0.9^l, paths Mine+Fishing+Farm in order, legacy Center/Farm/Mine), buy button "{cost} ☯" / "MAX"; affordable card gold border, maxed opacity .6; unaffordable click -> error SFX. Perk data D:774-810. After buying: rebuild shop, top bar, unlock buttons, repaint.

### 4.9 Ascend modal, vows, post-ascension card (U:2401-2509; H:110)
Gold-bordered modal: ⛩️ "The Ascension Gate stands complete"; dynamic text: "+N ☯", "World speed ×a → ×b", tribute ×a → ×b, gate offerings ("Offerings c/cap: {icon} o/perType ..." or "full (+c ☯)"), kept vows ("AP ×m"), "Ascended K times so far"; KEEP / RESET two columns (Keep: Perks + AP, Ascension speed + vow marks, Dragon's blessing, Lifetime stats, Know-how; Reset: Buildings & regions, Resources, Dragon stages, Upgrades). After >=1 ascension a Vows box with 4 checkboxes (D:815: burden 🎒 half hand, coldhearth 🧊 fuel x2, restless 🐉 tributes x2, solitude 🕯️ no starter wisps; "✓ marked" when completed once); AP multiplier by count VOW_MULT [1,1.15,1.3,1.5,1.75]. Buttons "☯ Ascend" (confirm "Ascend and begin the grounds anew? (+20% world speed per ascension, kept forever)" -> `E.ascend(chosenVows)`), "Keep playing", "☯ See perks". Opened by clicking the built Gate or `GS.ascendPrompt`.
Post-ascension card: "Ascension N complete", "+AP ☯ · world speed ×a → ×b", vows, head-starts, "Spend your Ascension Points at the Shrine ...", buttons "☯ Open Shrine" / "Begin run N+1"; once, when `GS.justAscended`.

### 4.10 Quest panel + milestone tracker (U:2012-2257; S:402-433, 608-622)
Fixed top-right (14,14), 250 wide, r=12, max-height 100vh-120 scroll. Collapsed: "📜" chip (+"❗" if claimable). Expanded: header "📜 Quest i/N" + "–"; "{icon} {name}" 14/800; desc 12 muted; "Unlocks:" row (revealed building icons, "+n icon"); progress bar (6 px, accent) + "cur/need" gold + "Claim"/"Claim ✔" button (`E.claimQuest`; hand-bound rewards float "+n Name → hand" gold at 35% view height, 22 px apart); "Next: {icon} {name}" faded 60%. Data D:600+ (`goal()` reads live state; `reward.reveal/items`, `builds`, `target`).
Milestone block "🎯 Next milestone" (`milestoneInfo` U:2112), shown from minute 0 (body of the panel once the chain ends): (a) gold "☯ Spend N AP at the Ascension Shrine" if AP unspent and shop not yet opened; (b) dragon tribute "n/total" with have/need rows ("icon **Name** have/need — source hint") + bar; (c) Gate: unbuilt -> "Raise the Ascension Gate" with cost rows and a recursive "Next step:" (`stepToward` U:2054: Storehouse stock, switch recipe, build producer, source hint); built -> "Ascend for +N ☯" and offerings; (d) hungry-disciples Mill/Brewery hint. Also provides the build-menu 🎯 targets. Rebuild only when a key string changes.

### 4.11 Region unlock buttons (U:1308-1445; S:238-271)
Overlays inside the viewport for LOCKED regions that are adjacent (|drx|+|dry|=1) to an unlocked region AND to the region under the camera centre; on the viewport side facing it (left/right/up/down), or centred ("here") when the camera centre is inside that locked frontier region. Content "🔓" + "Unlock {Region}" + cost "qty icon ..." (becomes "paid/needed icon" after an installment) + "pay have/need" when partial. Style: `rgba(20,25,30,.86)`, r=10, dashed `#6b561c` border, gold text, opacity .6 (1 on hover). States (`unlockPayState` vs hand): afford = solid gold border + pulsing glow (1.8 s, 0..10 px `rgba(251,191,36,.55)`); partial = dashed gold, opacity .8; cant = default. Left or right click pays the hand's contents (`E.unlockArea`): true = unlocked (clamp camera, full render); object = installment paid; false = error SFX. Side buttons shift below the quest panel (or left of it) if overlapping. Sides are derived geometrically (`WORLD.unlockSide` D:498 is unused).

### 4.12 Welcome-back / offline (U:2617-2765, M:65-163; E:2855 OFFLINE_MIN_MS 90 s, OFFLINE_MODAL_MS 10 min)
<90 s: nothing. 90 s-10 min: synchronous replay then toast "Welcome back — +N item(s) while away" (top-centre, accent-dk border, 5 s then 0.6 s fade, only if N>0). >=10 min: modal 🌙 "Welcome back", replay in ~50 ms slices behind it with progress bar (12 px accent gradient) "Catching up… N%" and Skip ("Skip — forfeit the last Xh Ym" or "Skip — output has levelled off"; "Stopping…"); Continue hidden until done; then summary line "You were away X ..." (variants: beyond cap (8 h + Long Slumber), interrupted, failed, saturated, skipped), gains chips "icon **+q** Name" sorted by qty (or "Nothing new was produced ..."), "Why it stopped" rows (saturation, ground full, autoskip, output full, no fuel, no food, stone full; `stallRowsHTML` U:2667). `fmtAway`: "Xh Ym" / "Ym" / "Xs". Cannot be dismissed during replay. First launch reuses the modal as intro: 🌱 "Welcome to Idle Grounds" (M:94). Autosave/live loops start after the replay.

### 4.13 Dragon dialog / ending (U:2600, 2767)
Dragon story modal: purple border, 🐉 56 px, `GS.dragon.dialog` text, "Continue ▶" (Esc also continues). Ending (once, after the dialog; `GS.won && !endingSeen`): gold-bordered 🐲 "The Dragon Awakens", flavour (world ~11% faster), stats (Ascensions, Total crafted, Playtime) + Gate hint, "Continue ▶". `#win-modal` is unused.

### 4.14 Stats modal (U:2371)
Two-column rows (width min(440,92vw)): Playtime, Total gathered, Total crafted, Fox spirits slain, Buildings built, Upgrades applied, Disciples recruited, Wisp links added, Recipe switches, Ascensions, Ascension Points, Regions unlocked "x / y", Carry capacity.

### 4.15 Help modal (U:2312)
Scrollable titled sections (title 14/800, body 13 muted), conditionally shown by dragon stage / unlocked regions / ascended. Port the text verbatim from `openHelp`.

### 4.16 Tooltips
Native `title` on buttons, pills, cards, vow chip, version chip; instant custom tooltip only for the tree. Link dots carry text.

---------------------------------------------------------------------
## 5. FX / juice (U:85-169; cosmetic, never saved)
- **Floaters** (cap 60): world position, optional icon 15*s + text 800 14*s. Colour gold for item keys matching /essence|scale|pill|elixir|talisman|jade/, steel `#cbd5e1` for /iron|steel|star|tools|glass/, else green `#4ade80`; red `#f87171` for errors ("Hand full", "✗", locks, link refusals). Life 850 ms: alpha 0->1 in 60 ms, hold to 400 ms, linear fade to 850; rises 30 world px. Dark halo `rgba(8,10,14,.85)` width max(2,3.5*s), shadow blur 4*s. Text placed at icon x + 0.62*icon size.
- Engine drop hook `onGroundDrop(area,item,qty,x,y)`: unlocked + on-screen -> "+qty" floater with icon at y-6; gold items also 8 sparks. Not fired during offline replay.
- `fxPickup`: green "+N" at cursor (y-8) + 5 sparks + "pickup" SFX. `fxSwing`: 4 sparks `rgba(226,232,240,.9)`.
- **Sparks** (cap 240): random angle, speed 30-100 px/s, initial vy -30 px/s extra, gravity 90 px/s², radius 1.5-3.5 world px shrinking 40% over life, life 380-640 ms, alpha 1->0 linear; drawn under floaters.
- Quest reward floaters (see 4.10). Other animation: node squash, fish bob, gate pulse, quest ring pulse, unlock-button pulse, fuel burn clip, wisp motion. `prefers-reduced-motion` disables CSS-only ones.

---------------------------------------------------------------------
## 6. Audio (audio.js; WebAudio, fully procedural)
Master gain 0.18. Voice(type, freq Hz, start offset s, dur s, peak, endFreq Hz): oscillator through a gain that starts at 0.0001, ramps exponentially to `peak` at +8 ms, then exponentially to 0.0001 at `dur`; frequency glides exponentially freq->endFreq over `dur`; stopped at dur+20 ms. Mute stored under localStorage `ig_muted` ("1"), default unmuted; audio context resumes on first user gesture. No music, no volume slider. Engine fires `window.onSfx(name)` (M:52); UI fires "pickup" (U:163) and "error" (many places) and "click" (mute toggle).
| name | trigger | voices |
|---|---|---|
| harvest | harvest swing | triangle f=620±60 random, 0, 0.09, 0.5, ->f*0.85 |
| pickup | vacuum pickup / withdraw | sine 480, 0, 0.12, 0.5, ->900 |
| swing | swing, no loot | sine 180, 0, 0.07, 0.4, ->120 |
| craft | converter batch done | sine 660, 0, 0.16, 0.5; sine 990, +0.09, 0.20, 0.4 |
| build | building placed/completed | triangle 150, 0, 0.16, 0.7, ->90; sine 300, 0, 0.06, 0.25, ->260 |
| upgrade | altar upgrade applied | triangle 700, 0, 0.10, 0.4; triangle 900, +0.06, 0.10, 0.4; sine 1320, +0.12, 0.16, 0.4, ->1500 |
| unlock | region unlocked | sine 523, 0, 0.18, 0.55; sine 784, +0.10, 0.26, 0.5 |
| hit | fox struck | square 220, 0, 0.06, 0.28, ->150 |
| kill | beast died | triangle 260, 0, 0.20, 0.55, ->90; sine 130, 0, 0.22, 0.4, ->70 |
| dragon | dragon stage-up / awakening | sine 110, 0, 0.35, 0.8, ->55; sine 220, +0.04, 0.30, 0.3, ->130 |
| ascend | ascension | sine 196, 0, 0.40, 0.7; sine 294, +0.03, 0.36, 0.45; triangle 588, +0.06, 0.30, 0.28 |
| error | invalid action, refused feed, full hand, locked click | square 140, 0, 0.14, 0.3, ->110 |
| click | UI tick | sine 660, 0, 0.04, 0.22 |
Unity: generate AudioClips at startup from these params (or bake to WAV), play via a small pooled AudioSource set; mute via PlayerPrefs.

---------------------------------------------------------------------
## 7. Palette / fonts / style (S:4-24, U:40-58)
`bg #1a1f24`, `bg-2 #232a31`, `panel #2b333c`, `panel-2 #323b46`, `line #3c4651`, `text #e6edf3`, `muted #94a3b8`, `accent #4ade80`, `accent-dk #22a35a`, `gold #fbbf24`, `danger #f87171`, amber `#f59e0b`, purple `#a855f7`; tier colours `#9ca3af #4ade80 #60a5fa #c084fc #fbbf24`. Body background radial-gradient(circle at 50% -10%, `#243039`, `#1a1f24` 60%). Void `#161b16`; altar `rgba(74,60,30,.95)`; dragon `rgba(52,36,70,.95)`; veil `rgba(0,0,0,.55)`. Region tints in 2.2. Fonts: "Segoe UI", system-ui sans (weights 700/800 dominate); emoji via Twemoji/Segoe UI Emoji/Noto Color Emoji. Sizes: body 13-14, pills 14/700, headings 15-17/800, tiny labels 9-10. Viewport frame: 1 px line, r=12, shadow. Text selection disabled. Focus-visible ring 2 px accent.

---------------------------------------------------------------------
## 8. Unity mapping suggestions
**World = scene GameObjects with SpriteRenderers.** Use PPU 32 so 1 cell = 1 unit (world px/32).
- `RegionView` prefab per region (tinted background quad, frame, pooled zone overlay quads, veil + lock sprite). `NodeView` (SpriteRenderer + squash component pivoted at the base), `BuildingView` variants (frame 9-slice + world-space TMP labels + child icon SpriteRenderers; Altar/Dragon/Gate/Converter/Storehouse/Pavilion/Formation; `FuelRackView` child), `EnemyView` (+ pip row), pooled `GroundItemView` (hundreds), pooled `WispView`, `LinkLine` (LineRenderer, tiled dashed material), `ReachCircle` (scaled sprite), `PlacementGhost`, `QuestRing`, pooled `FloaterView`, one pooled spark system (gravity 90 px/s² = 2.8 units/s²).
- Sorting: layers Ground < Zones < Radii < Objects (y-sorted by row) < Items < Wisps < Overlay/FX. Locked regions: veil above their own objects but below other regions' objects (give each region a sorting-order band, veil at the top of its own band).
- Item art: import the 23 PNGs (Point filter, no mipmap); remaining items need sprites (emoji fallback must become sprite/TMP sprite-asset, don't rely on emoji fonts).
- Camera: one orthographic URP-2D camera, orthographic size = (560*zoom/32)*9/32 = 4.92*zoom units (zoom 1..3); lerp zoom 0.2/frame (use damping); pan 22.5 units/s (45 with sprint) with the same clamp rule; no Cinemachine needed. The original repaints on demand; Unity just updates views from state events.
**UI: recommend uGUI (Canvas + TMP)** for HUD and modals: heavy coupling to world space (hand chip follows the cursor, popups anchored over world buildings via WorldToScreenPoint, unlock buttons overlaid on viewport edges), rich rows with inline icons (TMP sprite assets), per-frame countdowns, and the custom-drawn tree (about 19 node buttons plus rotated-Image lines, pan/hover/brackets) all fit RectTransforms. UI Toolkit is acceptable for static modals (help, stats, perk shop, welcome, vows) but mixing two systems costs more; choose UI Toolkit only if the project standardises on it. Screen-space overlay canvas; modal stack for Esc priority; ScrollRects for the strips (wheel -> horizontal); `EventSystem.IsPointerOverGameObject` drives hand-chip hiding and world-click gating.
**Input System** (`Gameplay` map; disable it while a modal is open except WASD when needed): `Pan` Vector2 (WASD composite), `SprintToggle` Button (Shift, performed only), `Zoom` Axis (Mouse scroll Y; up = in, step 0.25), `Point` (pointer position), `Primary` Button (LMB; started + canceled for holds), `Secondary` Button (RMB; started + canceled), `RotateHandForward` (Q) / `RotateHandBack` (E), `ToggleBuild` (B), `Cancel` (Escape). "Place several" = Shift state read at the click (Shift also toggles sprint in the original). Drop F9. Implement hold semantics in one `HoldController` ticked from `Update` with the constants in 3.2 (hit-testing by cell math, no colliders needed; enemies by distance via engine). Touch: original only adds zoom buttons.

**Notable complexities**: cross-region layer interleaving (2.1); right-click hold latch rules and rack redirect (3.2); edge-vacuum redirect (3.2); fixture click throttling; build menu 400 ms reshuffle lock; hand chip hiding rules; tree BFS visibility; `stepToward` recipe walker (pure logic, port as a service); fuel-icon clip burn animation; offline replay tiers (engine side, sliced 50 ms); emoji dependency.
