# Idle Grounds — Engine Systems Specification (port reference)

Behavioural spec of the browser prototype's simulation, written so the Unity/C#
port can reproduce it **without reading the JS**. Source of truth:

| File | Lines | Role |
|---|---|---|
| `old-game/js/engine.js` | 3186 | pure game logic, global `ENGINE` (DOM-free) |
| `old-game/js/state.js` | 487 | `GS` state shape, `SAVE` (save/load + migrations) |
| `old-game/js/data.js` | 848 | static config `DATA` (items, areas, buildings, tree, quests, perks…) |
| `old-game/js/main.js` | 163 | boot order, loop cadence, offline-replay driver |
| `old-game/js/ui.js` | 3366 | input rules that belong to the "hand" (hold/latch/ramp) — cited where relevant |

Citations are `file:line function`. "px" = area-local pixels; "cell" = 32 px.
`now` = `Date.now()` (wall-clock ms) unless stated — **in the port every
`now` must come from an injectable clock** (offline replay virtualises it).

> Where `DESIGN.md` and `data.js` disagree, **data.js wins** (e.g. DESIGN says
> Gathering Stone radius 10; data says 8).

---

## 0. Global constants & conventions

| Constant | Value | Where |
|---|---|---|
| `CELL` | `DATA.GRID.cell` = 32 px | engine.js:7 |
| `GRID.cells` (N) | 93 cells per side per region | data.js:270 |
| `PLAY_PX` | N·CELL = 2976 px (the comment "768" is stale) | engine.js:8 |
| `clampPx(v)` | `clamp(v, 4, PLAY_PX-4)` | engine.js:9 |
| `GRID.building` | default footprint `{w:3,h:3}` | data.js:273 |
| `GRID.gap` / `margin` | 5 void cells between regions / 10 inert border cells (view only) | data.js:271-272 |
| `HAND_CAP` | 20 | data.js:766 |
| `FUEL` (burn-ms) | wood 10000, bamboo 6000, charcoal 40000, firestone 120000 | data.js:56 |
| `FUEL_SLOTS` | 6 | data.js:58 |
| `AUTOMATION_CLICKS` | {1:2, 2:6, 3:20} | data.js:764 |
| `TEST` | `{ENABLED:true, timeScale:0.2, costScale:0.5}` (**currently ON**) | data.js:829 |
| `GROUND_CAP` / `GROUND_HARD_CAP` | 600 / 900 per area | engine.js:710-711 |
| `PROTECTED_SHARE` | 480 | engine.js:764 |
| `MANUAL_GRACE_MS` / `FIXTURE_GRACE_MS` | 4000 / 8000 | engine.js:712-713 |
| `GEN_RARE_IDLE` | 2 | engine.js:765 |
| `OUTPUT_PILE_MAX` / `_CELLS` / `PILE_RECHECK_MS` | 12 / 3 / 500 | engine.js:1616 |
| `CRAFT_WINDOW_MS` | 60000 | engine.js:1583 |
| `MAX_TICK_GAP` / `MAX_TICK_EVENTS` | 10000 ms / 400 | engine.js:2310-2311 |
| `AUTO_SKIP_LOOSE` | 120 | engine.js:2776 |
| `OFFLINE_MAX_TICKS` / `OFFLINE_MIN_MS` / `OFFLINE_MODAL_MS` / `OFFLINE_PLATEAU_MS` | 45000 / 90000 / 600000 / 900000 | engine.js:2854-2856, 2946 |

**Randomness.** `rand(a,b)` = uniform integer in `[a,b]` inclusive
(engine.js:441). `rollAmount(spec)` = `spec.min + floor(random·(max-min+1))`
(engine.js:828). Drop spec helper `d(item,min,max)` (max defaults to min,
data.js:86). The port should route all randomness through one seedable RNG
(tests need determinism).

**Iteration order matters.** Areas are always processed in the key order of
`DATA.AREAS`: `center, farm, mine, fishing, volcano, grove, celestial`.
Buildings/nodes/ground/wisps are processed in array (insertion) order. Use
ordered collections (`List<T>`, insertion-ordered dictionaries) everywhere a
JS object's key order is relied on (stock maps, recipe `inputs`, cost maps,
`FUEL`, etc. — JS preserves insertion order for string keys).

**TEST scaling** (port as a config flag, not code):
- `timeScale` multiplies: node regrow delay, field generator interval,
  generator-building interval, converter batch time, lantern beat, pavilion
  cycle, fox respawn, dragon-scale interval, `effectiveTimer`. It does **not**
  touch `harvestInterval`, fuel values, wisp flight speed, surface window,
  enemy movement, buff durations.
- `costScale` via `scaled(n) = max(1, ceil(ENABLED ? n·costScale : n))`
  (engine.js:128) applies to: upgrade costs, dragon tributes, region unlock
  costs. **Not** to building costs or quest rewards.
- `buffScale()` = `TEST.ENABLED ? 1 : 4` (engine.js:1889) multiplies dragon
  blessing and Vitality durations (60 s in test ⇒ 240 s real) — note the
  inverse direction compared with timeScale.

---

## 1. Game state (GS), save format, migrations

### 1.1 Area state — `state.js:8 makeAreaState`

| Field | Type | Meaning |
|---|---|---|
| `nodes` | `Node[]` | live resource nodes, fixtures, deco trees (§6) |
| `ground` | `GroundItem[]` | loose items, **one entry per unit** `{id,item,x,y,[crafted],[gen]}` + transients `manualAt,_src,_pullAt,_pullTo` |
| `buildings` | `Building[]` | placed buildings incl. ghosts (§8) |
| `spawnQueue` | `{at:ms, kind:spawnerKind}[]` | pending node respawns |
| `genTimers` | `number[]` | next-due ms per `cfg.generators[i]` (0 = fire now) |
| `enemies` | `Enemy[]` | `{id,x,y,hp,maxHp,tx,ty,hitAt,[kind:"boss"],[sprite],[spd]}` |
| `enemyRespawns` | `number[]` | per-missing-fox respawn due times |
| `wisps` | `Wisp[]` | items in flight `{id,x0,y0,x,y,item,toId,fromId,t0,sp,[returning]}` |
| `nextNodeId, nextGroundId, nextBuildId, nextEnemyId, nextWispId` | int (start 1) | per-area id counters |
| `upgrades` | object | `{maxTier:1, speed, harvestSpeed, automation, quarry, enemyCap, damage, aoe, wispRate, affinity, discipleCap, paid:{}}` all int 0 (maxTier legacy, always 1; `paid` unused legacy) |
| `_autoSkip` | string[] (transient) | item types automation skipped this tick |
| `autoPaused` | bool (transient, stripped on save) | `_autoSkip.length>0` |

### 1.2 Root state — `state.js:31 makeInitialState`

| Field | Type / default | Meaning |
|---|---|---|
| `hand` | `{item,qty}[]` = [] | carried stacks, ordered; `hand[0]` = "front"/active stack |
| `handCap` | int = `HAND_CAP` (20) | base capacity (Hand Size +5/lvl, Fleet Hands +5/lvl) |
| `handLevel` | int 0 | Hand Size upgrade level |
| `areas` | `{areaKey: AreaState}` | one per `DATA.AREAS` key |
| `world.unlocked` | `{center:true, others:false}` | open regions |
| `world.unlockPaid` | `{region:{item:qty}}` = {} | installments toward locked regions |
| `build` | `{open:false, placing:null}` | UI mode — **never persisted** (reset on save) |
| `upgradeJob` | `null \| {area,type,needs:{},paid:{}}` | Altar project being fed |
| `dragon` | `{stage:0, paid:{}, msg:null, msgUntil:0, dialog:null}` | Sleeping Dragon progression |
| `dragonScaleAt` | number (NOT in initial state; created by gameTick, saved) | next scale shed time |
| `starterPlaced` | bool false | starter network built |
| `won` | bool | dragon awakened this run |
| `dragonBlessed` | bool | permanent blessing (survives ascension) |
| `buff` | `null \| {kind:pillItemId, until:ms}` | active dragon-pill blessing |
| `combatBuff` | `null \| {until:ms}` | Martial Vigor |
| `ascensions` | int 0 | completed ascensions |
| `ascendPrompt` | bool | show ascend dialog (UI) |
| `ascendPoints` | int 0 | AP currency |
| `perks` | `{perkId: level}` | permanent perks |
| `vows` | `{active:[], done:{vowId:count}}` | challenge runs |
| `justAscended` | `null \| {n, ap, speedFrom, speedTo}` | one-time card |
| `introSeen`, `endingSeen` | bool | UI one-shots |
| `gridCells` | int = `GRID.cells` | geometry stamp for migration |
| `quest` | `{idx:0, hidden:false, chain:QUEST_CHAIN(=2)}` | tutorial chain cursor |
| `builtTypes` | `{type:true}` | types the PLAYER completed (reveal) |
| `buildSeen` | `{type: 1\|2}` | build-menu "new" badge levels |
| `pavilionSeeded` | bool | first pavilion of the run got free buns |
| `perkShopSeen` | bool | UI hint |
| `lastSeen` | ms = now | last save stamp (offline catch-up) |
| `offlineAwayFrom` | `null \| ms` | original away start of an interrupted replay |
| `stats` | `{started, totalGathered, totalCrafted, foxKills, buildingsBuilt, upgradesApplied, linksAdded, recipeSwitches, disciplesRecruited}` | lifetime counters (survive ascension) |

### 1.3 Node, Building, Wisp shapes (for reference)

- **Spawner node** (engine.js:484): `{id,row,col,size,kind,spawnerKind,interaction,useTiers,tier:1,hitsLeft,regrowSec,swingMs,sprite,perHit,drops,rareDrop,surfaceUntil,autoFlash, [pending:{item:qty}], [hitAt]}`.
- **Fixture node** (engine.js:507): `{id,row,col,size,kind,interaction,fixed:true,tier:1,deco:(interaction==="none"),clicks:0,clicksPerDrop,dropItem,dropMin,dropMax,rareDrop,swingMs,sprite,autoFlash}`.
- **Deco node** (engine.js:540): `{id,row,col,size:1,kind:"deco",interaction:"none",deco:true,tier:1,sprite,decoScale,decoDx,decoDy,autoFlash}`.
- **Building** (engine.js:1299): `{id,type,row,col,paid:{},built,item:null,qty:0, [starter], [lock]}` plus per-kind fields: gatherer/stoker `inv:[{item,qty}]`; lantern `links:[{from,to}], connIdx, nextSend`; roster `disciples, buns, nextCultivate`; converter `recipe, stock:{}, smeltDoneAt, [fuelQ:[{item,rem,total}], fuelBurnAt]`; gen building `nextGen`; gate `offered:{}, offerings`. Transients: `_crafts,_craftFirst,_pileFull,_pileAt,_pileItem,_accEver,_seq`; link transients `_stat:{sentAt,fail}, _seq`.

### 1.4 Save — `state.js:121 saveState`

- Key `"idle-grounds-save-v1"` in localStorage (port: a JSON file / PlayerPrefs).
- Serialise `GS` dropping (via `transientReplacer`, state.js:117) **every key starting with `_`**, plus `manualAt` and `autoPaused`.
- Force `build = {open:false, placing:null}`.
- `lastSeen = ENGINE.offlineResumeAt()` if finite (replay unfinished, §15) else `now`.
- `saveDisabled` flag (set by `clearSave`, state.js:483) blocks all saves until reload (so a reset is not re-written by the unload save).
- Cadence (main.js): autosave every **5000 ms** (starts only after offline replay ends) and on `beforeunload` (wired before the replay). `ascend()` saves explicitly. Closing the welcome modal saves.

### 1.5 Load + migrations — `state.js:138 loadState`

Returns `null` (⇒ fresh state) if no save, unparsable, missing `areas`/`world`,
or **any exception** during migration (note: the next autosave then overwrites
the old save — port should back it up instead). Otherwise merge onto
`makeInitialState()` in this order:

1. Per known area: shallow-assign saved area onto fresh; `upgrades` = defaults ⊕ saved.
2. `hand` if array; `handCap` if truthy; `handLevel||0`; `upgradeJob` if present; `dragon` = defaults ⊕ saved.
3. `world.unlocked` ⊕ saved.
4. `world.unlockPaid`: keep only known-area, still-locked regions with object values; per item keep finite `>0`, floored; drop empty maps.
5. `won=!!s.won`; `dragonBlessed = s.dragonBlessed ?? s.won`; `starterPlaced`.
6. `buff` kept only if `until` finite and `kind ∈ DRAGON_BUFFS`; `combatBuff` if `until` finite.
7. `ascensions` finite else 0; `ascendPrompt`; `introSeen = (s.introSeen !== false)`; `endingSeen = s.endingSeen ?? s.won`.
8. `ascendPoints` finite else 0.
9. `vows.active`: known ids, deduped, order kept. `vows.done`: known ids with finite `>0` → floored.
10. `justAscended` kept only if `n, ap, speedFrom, speedTo` all finite.
11. `perks`: for each known perk, finite `>0` level clamped to `perk.max`.
12. `lastSeen` finite else **null** (⇒ no offline catch-up). `offlineAwayFrom` kept only if finite, lastSeen finite and `offlineAwayFrom < lastSeen`.
13. `stats` = defaults ⊕ saved.
14. **Quest remap**: `legacyChain = !s.quest || s.quest.chain !== QUEST_CHAIN`. Merge quest, set chain to current. If not legacy: `idx = clamp(idx, 0, QUESTS.length)`. Else if veteran (`ascensions>0 || won`): `idx = QUESTS.length`. Else `remapLegacyQuestIdx(idx, s.dragonBlessed===undefined ? V50 : V51)` (state.js:429): old idx o means quests `[0,o)` claimed; new idx = max over claimed old ids that exist in the new chain of `(newIndex+1)`; `o >= old.length` ⇒ end of chain. V50 = `wood,leaves,dragon1,fox,build,upgrade,link,recipe,craft,explore,cultivate`; V51 adds `waters,weaver` before `cultivate`.
15. **builtTypes**: if saved object, keep known types with truthy values. Else (pre-v52): if `starterPlaced`, tag starter buildings in the raw center list via `inferStarterTags` (state.js:444 — match each built, untagged building, by ascending id, to the nearest unused `LEGACY_STARTER_SPOTS` entry of the same type within Chebyshev distance ≤ 3); then, unless `stats.buildingsBuilt === 0`, every built, non-starter, known type counts.
16. **buildSeen**: saved object → known types with finite `>0`, clamped to 2. Missing ⇒ every type = 2 (old saves see no badges).
17. `pavilionSeeded = s.pavilionSeeded ?? (disciplesRecruited>0 || any built roster building)`; `perkShopSeen`.
18. Per area scrub (`LIVE` = keys of `ITEM_NAMES`; `regrid = s.gridCells !== GRID.cells`):
    - `enemyRespawns`: if raw save lacks the array, convert legacy scalar `enemyRespawnAt` (>0) to `[at]`; filter finite; delete `enemyRespawnAt`.
    - `regrid` ⇒ clear `nodes, spawnQueue, enemies, genTimers, enemyRespawns` (buildings kept).
    - Nodes kept only if: not deco; fixed ⇒ kind ∈ current fixtures, else `spawnerKind` ∈ current spawners; finite row/col/size and size>0. (Deco rings are regenerated by initArea.)
    - `spawnQueue` kept only for current spawner kinds. Buildings kept only for known types with finite row/col.
    - Config-owned node fields refreshed from current config: fixtures get `swingMs(||1000), sprite(||"⛰️"), clicksPerDrop, dropItem, dropMin, dropMax, rareDrop(||null)`; spawner nodes get `swingMs(||350), sprite(||null)`.
    - Any node with `tier !== 1` ⇒ `tier=1`; if `useTiers`, `hitsLeft = min(hitsLeft||1, tiers[0].hits||1)`.
    - Ground: keep LIVE items with finite x,y; delete `manualAt`; normalise `crafted` to `true` or absent; `gen` kept (as `true`) only when not crafted.
    - Delete `autoPaused`. Enemies kept if finite x,y,hp and hp>0. Wisps kept if LIVE + finite x,y; missing `t0` ⇒ `x0=x,y0=y,t0=now`; `sp` missing or `<50` ⇒ 170.
    - Buildings: `inv` filtered to LIVE & qty>0; `links` filtered to existing from/to ids. `starter` normalised; storehouse `item` not LIVE ⇒ `item=null, qty=0`; `paid` scrubbed of dead items.
    - **Gate offerings**: if gate and (`offerings` or `offered` defined): `per = perType||cap`. With `offered` object: for each offering item in order, `q = clamp(floor(src[it]), 0, min(per, cap-total))`. Else legacy count n = clamp(floor(offerings),0,cap) attributed in **reverse** item order (dragon_scale, star_steel, talisman), `min(per,n)` each, then re-keyed in forward order. `offerings = total`.
    - Roster: `disciples`, `buns` finite and ≥0 else 0.
    - Converters: `recipe` clamped to valid index else 0; `stock ||= {}`; legacy `smeltPaid` merged into stock (LIVE, >0) and deleted; legacy `queue` (n batches) adds `inputs·n` of the current recipe to stock, deleted; stock scrubbed of dead items; `smeltDoneAt` finite ≥0 else 0.
    - Burners: legacy scalar `fuel` (+ `stock.wood·FUEL.wood`, then stock.wood deleted) → if `fuelQ` is an array, filter to known fuel items with finite `rem>0` and set `total=FUEL[item]`; if that yields nothing and legacy ms>0, pack greedily biggest unit first (firestone, charcoal, wood) while `ms ≥ FUEL[u]` and slots remain, then a partial wood `rem=min(ms, FUEL.wood)`. Truncate to `FUEL_SLOTS`. `fuelBurnAt` finite else 0.
    - `upgrades.maxTier = 1`.
19. Hand filtered to LIVE & qty>0. `upgradeJob` dropped if it lacks `needs` or has legacy `item`. `dragon.paid` and `unlockPaid` scrubbed of dead items (empty region maps deleted).

**Port rule:** migrations must stay idempotent (running twice = same result).
The C# save should carry an explicit `schemaVersion`; reproduce the rules
above only to import web saves (optional) — but keep the *sanitisation* rules
(scrub unknown ids, clamp, default) for every load.

---

## 2. Main loop

### 2.1 Boot order — `main.js:5-163`

1. `loadState() || makeInitialState()` (state.js:487).
2. `initArea(k)` for **every** area (locked ones too — they're visible).
3. `setupStarterNetwork()` (no-op if `starterPlaced`).
4. Register save-on-exit. `beginOfflineCatchup()`:
   - no job ⇒ go live;
   - `job.awayMs < 10 min` ⇒ replay synchronously (`step(job, ∞)`, `finish`), then live;
   - `≥ 10 min` ⇒ sliced async replay (50 ms wall slices; 500 ms when hidden) behind a progress modal with Skip, then finish + summary, then live.
5. Live: `gameTick()` every **50 ms**; `automationTick()` every **1000 ms**; autosave every **5000 ms**. (Both ticks return change hints used only for repainting.)

### 2.2 `periodic(due, interval, now)` — engine.js:2314 (due-time re-arm)

Module state: `lastTickAt`, `tickGap = clamp(now-lastTickAt, 0, 10000)` (0 on
first tick or if clock went backwards), set at the start of each `gameTick`.

```
periodic(due, interval, now):
  if !(interval > 0): return {n:0, next:due}
  if !finite(due) or due < now - tickGap - interval: due = now     // stale/paused/fresh clock: restart, no burst
  if now < due: return {n:0, next:due}
  n = min(400, floor((now-due)/interval) + 1)
  return {n, next: max(due + n*interval, now - interval + 1)}
```

A clock of `0` therefore fires **once immediately**, then every `interval`.
Coarse ticks (offline replay, throttled tab) fire the same number of events as
fine ticks. Used by field generators, generator buildings, lanterns, pavilions.
Converters implement the same idea inline (§9).

### 2.3 `gameTick()` — engine.js:2325 (per unlocked area, in area order)

Locked areas are **skipped entirely** (their clocks freeze; respawn/surface timers resume when opened).

1. **Surface dives**: every node with `surfaceUntil && now ≥ surfaceUntil` ⇒ `depleteNode` (no drops).
2. **Respawn queue**: entries with `at ≤ now` are removed; for each, `spawnFromSpawner(spawner of kind)`; on failure (zone full) re-queue at `now+500`.
3. **Field generators** (§6.6).
4. **Generator buildings** (§8.6).
5. **Converters** (§9) incl. burner fuel burn (§10).
6. **Per-building logistics loop**, one pass over `area.buildings` in order; for each **built** building, in this order: Gathering Stone eject+vacuum (§11.2) → Lantern beat (§11.5) → Furnace Spirit stoking (§10.4) → Pavilion cultivation (§12.4).
7. **Wisp flights/arrivals** (§11.6).
8. **Enemies** spawn + wander (§7).
9. **Dragon scales** (center only, after awakening) (§12.2).
10. **Ground physics** — *skipped during offline replay*: `settleGround` then `pushOutOfColliders` (§4.5).

Returns `changed` (bool) — a repaint hint only.

### 2.4 `automationTick()` — engine.js:2786 (every 1000 ms)

See §13.1.

### 2.5 World speed factor — `prestigeFactor(o)` engine.js:274

```
prestigeFactor = 1/(1 + 0.2*ascensions)
               * 0.95^perk(haste)
               * (won || dragonBlessed ? 0.9 : 1)
               * 0.96^vowMarks          // vows completed at least once
```
`o` may override `{ascensions, marks}` (preview). `nextPrestigeFactor()` =
factor with `ascensions+1` and `marks = vowMarks(includeActive=true)`
(engine.js:284). "World speed" shown to the player = `1/prestigeFactor`.

Applies (multiplicatively) to: node regrow delay, `harvestInterval`, field
generators, generator buildings, converter batch time, lantern beat, pavilion
cycle, fox respawn, dragon-scale interval. **Not** to: wisp flight speed,
surface window, enemy walk speed, fuel burn rate, buff durations.

---

## 3. World & grid

### 3.1 Regions — `data.js:483 WORLD`, engine.js:2213-2227

- Region grid (rx,ry): farm(0,0) center(1,0) mine(2,0) fishing(1,1) volcano(2,1) grove(0,1) celestial(1,2). 3×3 grid; unused slots are void.
- `regionOrigin(k)` = `{row: ry·98, col: rx·98}` (stride = N+gap = 93+5).
- `regionAt(gRow,gCol)`: the region whose `[origin, origin+N)` square contains the global cell, else null (void/gap).
- **All simulation is per area in area-local coordinates** (cells 0..92, px 0..2976). Global coordinates are a view concern. Wisps never cross regions (links are per area).

### 3.2 Zones — data.js:282-315 (inclusive cell rects; N=93, T=25)

| Zone | Rect (r0..r1, c0..c1) |
|---|---|
| cornerTL | 0..24, 0..24 |
| cornerTR | 0..24, 68..92 |
| cornerBL | 68..92, 0..24 |
| cornerBR | 68..92, 68..92 |
| corners | the four above |
| centre | 25..67, 25..67 |
| midTop | 0..24, 25..67 |
| midLeft | 25..67, 0..24 |
| clayField | 76..84, 76..84 |
| quarryField | 76..84, 8..16 |
| springField | 8..16, 8..16 |
| woodField | 12..20, 40..48 |
| sandField | 42..50, 14..22 |

`zoneRects(key)` returns the list; `cellInZone(key,r,c)` (engine.js:1166).
**noBuild** per area (`data.js` `noBuild`): center = `corners + midTop`;
farm = `centre + midLeft`; mine/fishing/volcano/grove/celestial = `centre`.
`inNoBuild(area,r,c)` (engine.js:369).

### 3.3 Occupancy — engine.js:378-439

Occupied cell set ("r,c") = every node's `size×size` square (incl. fixtures and
deco) + every building footprint (**ghosts included**) + every **built** burner's
fuel rack (3 wide × 2 tall at `rows row..row+1, cols col-3..col-1`, i.e. LEFT of
the footprint, top-aligned). It is memoised (signature: list identity, length,
next-id for nodes and buildings, plus `occEpoch` bumped when a ghost completes
in place) and patched incrementally by `occNodeAdded/occNodeRemoved` with
per-cell reference counts. **Port note:** the cache is a pure optimisation; a
C# occupancy grid (`int[N,N]` ref-counts) updated on add/remove/complete is the
natural equivalent. Behaviour must equal recomputing from scratch.

`rackCells(area)` (engine.js:1248) = rack cells of **all** burners incl. ghosts
(used by placement only).

### 3.4 Building footprint & lookup

- `buildingSize(type)` = `BUILDINGS[type].size || {w:3,h:3}` (engine.js:374). Sizes: Altar/Dragon/Gate 5×5; burners (kiln, forge, pill_furnace, star_anvil) w3×h5; logistics + furnace spirit 1×1; everything else 3×3.
- `buildingAt(area,row,col)` (engine.js:1804): first building whose footprint contains the cell (racks are not part of it; the UI redirects rack clicks — §5.6).
- `buildingCenterPx(b)` = `((col+w/2)·CELL, (row+h/2)·CELL)`.

### 3.5 Placement validation — `canPlaceBuilding` engine.js:1170, `placeReason` engine.js:1207

`placeReason` mirrors `canPlaceBuilding` exactly (returns null when allowed).
Check order and reason strings:

1. Out of bounds (`row<0 || col<0 || row+h>N || col+w>N`) → `"Off the edge"`.
2. `waterOnly && area !== "fishing"` → `"Water only"`.
3. `gate && gateExists()` (any gate, ghost or built, any area) → `"Only one Ascension Gate"`.
4. Footprint scan, per cell: if `waterOnly` the cell must be inside zone `centre` (else *water* flag); else if not `anyZone` the cell must not be noBuild (else *wild* flag); any occupied cell sets *blocked*. Reasons in priority: `"Water only"`, then wild → `"Build around the Altar clearing"` (center) / `"Build on the rim, outside the field"` (others) (engine.js:1242), then `"Blocked"`.
5. If the type is a burner (`fuel:true`): `col-3 < 0` → `"Fuel rack blocked"`; each rack cell occupied, in another rack, or (non-anyZone) noBuild → `"Fuel rack blocked"`.
6. Any footprint cell inside an existing rack (ghost burners included) → `"Blocked"`.

`placeBuilding(area,type,row,col)` (engine.js:1295): requires
`isBuildingUnlocked(type)` and `canPlaceBuilding`; pushes a ghost
`{id,type,row,col,paid:{},built:false,item:null,qty:0}` + `initLogistics`. No
cost is taken (ghosts are fed — §8.1).

### 3.6 Region unlocks (installments) — engine.js:2229-2298

- `areaUnlockCost(k)` = for each item of `WORLD.unlockCost[k]`: `max(1, ceil(scaled(q) · 0.8^perk(frugal)))`. Base costs: farm wood 10; mine wood 16; fishing wood 20; volcano iron_bar 3; grove wheat 12 + wood 8; celestial spirit_stone 6, jade 3, glass 3. Center has none.
- `unlockRemaining(k)` = cost − `unlockPaid[k]` (positive parts only).
- `canPayUnlock(k)` = remaining is empty (needs only the click) OR hand holds ≥1 of some remaining item.
- `unlockArea(k)` (engine.js:2278): returns false if unknown/already open. For each remaining item in order: `take = min(rem, handCount)`, move from hand into `unlockPaid[k]`. If anything still remains → `{paid:n}` (n>0) or `false`. Else delete `unlockPaid[k]`, `openRegion(k)`, sfx `"unlock"`, return `true`. Installments are **never refunded** (except by perks, §14).
- `openRegion(k)` (engine.js:2262): `unlocked[k]=true`; every node with `surfaceUntil` gets `now + surfaceWindow·1000·(0.5+random)` (so frozen fish don't all dive at once).

---

## 4. Ground items

### 4.1 `dropGround(area, item, qty, x, y, tag)` — engine.js:804

```
if tag is undefined and autoHarvesting: tag = "gen"          // bot drops
src = (tag=="manual") ? manualSrc : null                     // set by harvestNode
manualAt = (tag=="manual") ? now + (src ? src.grace - 4000 : 0) : 0
freshFrom = ground.length
repeat qty times:
   g = {id: nextGroundId++, item, x: clampPx(x+rand(-16,16)), y: clampPx(y+rand(-16,16))}
   if manualAt: g.manualAt = manualAt          (transient)
   if src:      g._src = src.id                (transient)
   if tag=="crafted": g.crafted = true
   elif tag=="gen" and evictClassOf(g)==2: g.gen = true   (saved)
   ground.push(g)
if ground.length > 600: evictGround(area, freshFrom)
raise onGroundDrop(area, item, qty, x, y)      // UI "+N" floater; null during replay
```

Items never stack: one ground entry per unit.

**Tags:** `"manual"` = player action (harvest/loot/hand drop/refund/spill) —
Gathering Stones ignore it during its grace; `"crafted"` = building product —
protected from eviction; `"gen"` = generator/bot rare find — demoted to raw
eviction class. Untagged = ordinary (field generator commons, wisp spills,
dragon scales, starter seeds, quest overflow).

**Grace:** stones skip `g` while `now - g.manualAt < 4000`. Fixture drops are
stamped `now + 4000` so they're skipped for 8000 ms total. `manualAt` is never
saved (a reload clears all grace).

### 4.2 Eviction class — `evictClassOf(g)` engine.js:726

0 = raw common (evicted first), 1 = other raw, 2 = protected.

- `g.crafted` ⇒ 2.
- Static map built once from DATA: `crafted` = every recipe output; a region is
  *raw-cost* if every item of its unlock cost is not crafted (center has no cost
  ⇒ raw; farm/mine/fishing/grove raw; volcano/celestial not). Class 0 = items in
  raw-cost regions' tier `perHit/drops`, spawner `perHit/drops` (not rareDrop),
  fixture `drop`, generator `item`. Crafted outputs ⇒ 2. Then
  `dragon_scale, jade_shard, firestone` forced to 2. Everything else ⇒ 1.
  Resulting class 1 today: spirit_essence, beast_bone, obsidian, star_fragment, moonpetal.
- Unknown item ⇒ 1. Class 2 with `g.gen` ⇒ 1.

### 4.3 `evictGround(area, freshFrom)` — engine.js:767

Runs when `ground.length > 600`; `excess = n - 600`. Items at index ≥
`freshFrom` (added by this call) and items in manual grace are untouchable for
steps 1-3 (`cls=-1`). Oldest-first (array order):

1. While `protectedCount > 480`: evict class-2 items (oldest first).
2. Evict class 0, then class 1, oldest first, until `excess` met.
3. Hard ceiling: while remaining `> 900`, evict oldest not-yet-evicted items — pass 0 only `i < freshFrom`, pass 1 anything (incl. fresh/grace).
Compact the array preserving order. Returns count evicted.

### 4.4 Pickup helpers

- `suctionStep(area,x,y,radius,itemFilter)` (engine.js:964) — the left-hold vacuum, called **once per animation frame** by the UI (frame-rate dependent; the port should call it at a fixed rate, e.g. 60 Hz). If hand full → nothing. For each ground item (matching filter): `d = dist(cursor, g)`; skip `d>radius`; if `d ≤ 12` → `handAdd(item,1)`, remove; else move toward cursor by `min(1 + (1-d/radius)·3, d)` px. Ignores grace. Returns `{moved, picked}`.
- `pickupNear(area,x,y,radius)` (engine.js:987) — instant vacuum, nearest first, until hand full (exported; not used by current UI).

### 4.5 Ground physics (presentation-only, not run during replay)

- `settleGround(area)` (engine.js:860): pairwise repulsion, min distance **18 px**. Spatial hash with 64-px buckets (each item checks own + 8 neighbours, pairs `j>i`). For a pair closer than 18: if `d<0.01` pick a random direction `(rand(-10,10)||1, rand(-10,10)||1)`; push each by `(18-d)/2` along the axis. Clamp all items if any push happened. Returns count of pushes > 0.05 px. Result is order-dependent but only cosmetic — exact parity is not required, but must be deterministic and bounded.
- `pushOutOfColliders(area)` (engine.js:911): colliders = every building footprint (ghosts too), every **built** burner's rack rect `(col-3..col)×(row..row+2)` in px, every **fixed** node. An item strictly inside a rect (and not being pulled by a stone within the last 200 ms, `_pullAt`) exits through the nearest edge (+8 px beyond it, clamped) whose landing spot is not inside any collider; if the item was pulled by a stone in the last 5000 ms, exits are ordered by distance to the stone (`_pullTo`) instead. Counts visible moves.
- `settleAfterReplay()` (engine.js:3089): ≤6 settle passes (stop at 0 moves) + ≤2 collider passes per unlocked area.

### 4.6 Back-pressure on ground

- Ground cap/eviction (§4.3).
- Output pile: producers start no new work while ≥12 of their product lie within the footprint expanded by 3 cells (§9.5).
- Field generators count only items inside their field (+16 px) (§6.6).
- Automation skips node types with ≥120 loose raw items (§13.1).
- Dragon scales: ≤5 within 6 cells.

---

## 5. The hand

### 5.1 Model — engine.js:16-84

`hand` = ordered list of `{item, qty}`, unique per item, total ≤ `handCap()`.

- `handCap()` = `vow(burden) ? max(1, floor(GS.handCap/2)) : GS.handCap`.
- `handTotal`, `handSpace = cap - total`, `handCount(item)`.
- `handAdd(item,qty)`: `add = min(qty, space)`; merges into existing stack or appends a new stack at the **back**; returns added.
- `handTakeFirst()`: −1 from `hand[0]`, remove empty stack; returns item.
- `handTake(item,n)`: removes up to n from that stack.
- `handMoveToFront(item)`: moves the stack to index 0.
- `handRotate(dir)` (Q = +1: front→back; E = −1: back→front); 1-stack no-op; returns new front item.
- `canAfford/spend(cost)` — hand-paid costs (legacy; current costs are fed).

### 5.2 Withdraw — engine.js:91-127

- `takeFromStorehouse(sh, n)`: `take = min(n||1, handSpace, sh.qty)`; add; `qty -= take`; when empty and not `lock` ⇒ `item=null`.
- `withdrawFromBuilding(b, n)`: storehouse / seal → above; gatherer / stoker → take from `inv[0]` one at a time (FIFO by stack), up to n while space.
- UI pacing (ui.js:3225): left-hold on a storehouse/seal/gatherer withdraws 1 immediately, then at `rate = 1 + min(elapsed/200,1)·4` items/s (1→5/s over the first 200 ms).

### 5.3 Deposit — `depositToStorehouse(sh)` engine.js:91

Empty (untyped) storehouse adopts `hand[0].item`. Refuse (null) if full
(`qty ≥ 200`) or none of its item in hand. If the item is not the front stack
⇒ move it to front, return `{reordered}` (the **next** click deposits). Else
move 1 → `{deposited}`.

### 5.4 Feeding rule — `feedNeeds(rem, paid)` engine.js:1860

Shared by ghosts, Altar job, dragon: if `hand[0]` is an item `rem` still
needs ⇒ take 1, `paid[item]++`, `{fed}`. Else the first item of `rem` (key
order) that the hand holds is moved to the front ⇒ `{reordered}`. Else `null`.

`feedRatio(b, rec)` (engine.js:1874) — converters: among stock-space items
(front stack first if it has space, then recipe order), pick the carried item
with the lowest `stock[it] / inputs[it]` (strict `<`, so ties keep that order),
move it to front, then `feedNeeds(smeltSpace(b), b.stock)`.

### 5.5 `dropFromHand(area, x, y, noGround)` — engine.js:1893

The right-click dispatcher. `row/col = floor(y/32), floor(x/32)`. First match wins:

1. **Vitality Pill in front** (anywhere): take 1, `combatBuff = {until: now + 45000·buffScale}` ⇒ `{used, once:true}`.
2. **Beast Bait in front** and area has `enemies.baitSpawn` and cell is inside the enemy zone rect: take 1, push boss enemy at (x,y) `{hp:8, maxHp:8, tx:x, ty:y, hitAt:0, kind:"boss", sprite, spd:18}` ⇒ `{fed, lured:true, once:true}`.
3. `b = buildingAt(row,col)`. Built **Altar** (`type=="center"`): no job ⇒ null; else `feedNeeds(jobRemaining(job), job.paid)`; if fed and nothing remains ⇒ `applyUpgrade(job.area, job.type)`, `upgradeJob=null`.
4. Built **Dragon** (§12.1).
5. Built **stoker** (Furnace Spirit): front item via `endpointGive` ⇒ take 1 `{fed}`; else move the first carried fuel item (FUEL key order) it accepts to front `{reordered}`; else null.
6. Built **roster** (Pavilion): front item with `foodValue>0` and `endpointGive` ⇒ `{fed}`; else reorder a carried accepted food; else null.
7. Built **converter** (`recipeOf(b)` non-null): if burner and front item is fuel, **not** an input of the current recipe, and `fuelSpace>0` ⇒ take 1, `addFuelItem` `{fed}`. Else `feedRatio`; if that returns something ⇒ it. Else if burner with fuel space, reorder the first carried fuel ⇒ `{reordered}`. Else null.
8. Built **Warding Seal**: needs a front item; if `b.item !== front` ⇒ `item=front, qty=0, lock=true` — **the seal's previous contents are destroyed** — ⇒ `{configured}` (consumes nothing).
9. Built **Gathering Stone**: front item via `endpointGive` (respects cap and `_accEver`) ⇒ take 1 `{fed}`; else null (no reorder).
10. Built **Storehouse** ⇒ `depositToStorehouse`.
11. Built **Gate**: if front is an offering item: if `gateTakes` ⇒ take 1, `offered[item]++`, `offerings = count` `{fed}`; else reorder another acceptable offering or return null (never spills). If front is not an offering: reorder an acceptable carried offering `{reordered}`, else **fall through**.
12. **Ghost** (`!b.built`): `feedNeeds(buildingNeeds(b), b.paid)`. On completion (nothing left): `built=true`, `occEpoch++`, `stats.buildingsBuilt++`, `builtTypes[type]=true`; if roster and `!pavilionSeeded` ⇒ `pavilionSeeded=true`, `buns = foodCap (20)`; sfx `"build"`; gate ⇒ `ascendPrompt=true`. Result gets `building: b.id`.
13. If `noGround` ⇒ null. Else `handTakeFirst()` and drop 1 at (x,y) tagged manual ⇒ `{dropped}`.

Note: built buildings with no feed behaviour (lantern, shrine, gen buildings)
fall to step 13 (UI passes `noGround=true` when the press began on any building, so the result is null).

### 5.6 Right-hold latch & ramp (UI rules the port must keep) — ui.js:227-235, 2913-2939, 3104-3116, 3261-3292

- **Rack redirect** (ui.js:2900): a right-click on a built burner's rack cell (not on a real footprint) is redirected to the burner's centre.
- **Press**: `tb = buildingAt(redirected point)`. `holdTarget = tb ? {region,id,wasBuilt,stage:dragon.stage} : null`; `holdFront = !tb ? hand[0].item : null`. Call `dropFromHand(x,y, noGround = !!tb)` once immediately.
- **Latched feed-hold** (press began on a building): repeats only while the cursor is still over **that same building**, at `rate = 4 + min(elapsed/200,1)·16` per second (4→20/s over 200 ms), always with `noGround=true`. It ends for good (`holdDone`) when `feedHoldEnded(r)`: `r` null, `r.used`, `r.once`, dragon stage changed, or the ghost it began on is now built/gone. Also ends if any modal opens.
- **Ground-hold** (press began on open ground): after the initial drop, waits `GROUND_REPEAT_MS = 400`, then repeats at `4 + min((elapsed-400)/200,1)·16`/s, only while `hand[0].item === holdFront` (stops when the front stack runs out/changes), paused while over a building (incl. rack), stops after a `once` result.
- On release everything resets.

### 5.7 Left-click / hold rules (UI) — ui.js:2940-3073, 3207-3260

Priority on press: placement mode → demolish mode → locked region (refuse) →
link picking → **enemy** under cursor (≤22 px) → building (edge-pick
redirect, Altar opens tree, gate re-offers ascension, converter/lantern/pavilion
open panels, storehouse/seal/gatherer start withdraw-hold) → **node** under the
cell → **ground** item within `PICKUP_R = 64 px` (vacuum-hold).

- `CLICK_COOLDOWN = 100 ms` between counted clicks (≈10/s); a too-fast click only sets `hitAt` (flinch).
- Fixtures count at most one swing per `harvestInterval` (per fixture) even for spam clicks.
- **Harvest hold**: re-hit-tests the node under the cursor every frame; swings when `now - lastSwing ≥ harvestInterval(node)` (and per-fixture interval); **stops while the hand is full** (a single click still harvests). Uses `harvestNode(area,id,false,true)`.
- **Attack hold**: every `enemies.attackMs` (400 ms) strikes the enemy under the cursor.
- **Vacuum hold**: if the press started within `LOCK_R = 24 px` of an item, the hold is **type-locked** to that item (`suckFilter`); otherwise any. Runs `suctionStep` every frame.
- **Edge-pick** (ui.js:3092): on a building larger than 1×1, a click within `min(10 px, 15%·min(w,h)·32)` of its edge, with hand space and a loose item within 14 px, vacuums instead of opening the building.

`harvestInterval(area,node)` (engine.js:348) = `max(120, (node.swingMs||350) · 0.8^upgrades.harvestSpeed · prestigeFactor())`.

---

## 6. Resource nodes, spawners, fixtures, generators

### 6.1 Area config shape (data.js:100-261)

Each area: `tiers[0]` (single tier: `hits, perHit, drops, timer`), `spawners[]`
(`kind, zone, sizes[], target, scaleWithArea, spacing, interaction, useTiers,
swingMs, sprite, hits, regrow, perHit, drops, rareDrop{item,chance}`),
`fixtures[]` (`kind, zone, size, interaction, swingMs, sprite, clicksPerDrop,
drop, dropMin, dropMax, autoTap, rareDrop`), `generators[]` (`kind, zone, item,
intervalMs, cap, upgrade, rareDrop`), `enemies`, `surfaceWindow` (fishing 3 s).

Summary of the live config:

| Area | Spawners (target ×15 unless noted) | Fixtures | Generators |
|---|---|---|---|
| center | bush (chop, 1×1, target **10 flat**, spacing 6, hits 2, regrow 12, perHit leaves 1, drops leaves 1-2, swing 300) | quarry rock (BL, 2×2, 5 clicks→1 stone, rare jade 12%); spirit tree (midTop, 4×4, 3 clicks→2-3 wood, rare bamboo 12%, **autoTap**) | clay field 1500 ms cap 10; stone quarryField 1500 ms cap 10 `upgrade:"quarry"`, rare jade 8%; wood woodField 3000 ms cap 10 |
| farm | crop (instant, 3×3, **8 flat**, useTiers: wheat 2-3, timer 20); cotton (instant, 2×2, **5 flat**, regrow 18, cotton 1-2) | — | sand sandField 1500 ms cap 10 |
| mine | ore (break, 1-2, 10, useTiers: hits 2, stone 3 + clay 1, timer 10, rare firestone 5%, swing 450); ironvein (break 2×2, 2, hits 3, regrow 12, iron_ore 1-2, rare firestone 15%); jadevein (break 2×2, 1, hits 3, regrow 14, jade_shard 1-2) | — | — |
| fishing | fish (surface 1×1, 4, useTiers fish 1-2, timer 12); algae (surface, 10, regrow 8, algae 1-2) | spring (TL, 2×2, 3 clicks→1 water) | water springField 2000 ms cap 10 |
| volcano | obsidian (break 1-2, 8, hits 3, regrow 14, obsidian 1-2); firevein (break 2, 2, hits 4, regrow 20, firestone 1) | — | — |
| grove | herbbush (chop 1, 10, hits 2, regrow 16, perHit herb 1, drops herb 1-2); bamboostalk (break 1-2, 6, hits 3, regrow 18, bamboo 1-2) | — | — |
| celestial | starrock (break 1-2, 8, hits 3, regrow 16, star_fragment 1-2, rare firestone 5%); moonshrub (chop 1, 10, hits 2, regrow 18, perHit moonpetal 1, drops 1-2) | — | — |

`areaScale()` (engine.js:519) = `max(1, round((N/24)²))` = **15** for N=93;
spawner target = `scaleWithArea===false ? target : target·15`.

### 6.2 `initArea(area)` — engine.js:550 (idempotent; run on every boot)

1. center: if no Altar, push `{type:"center", row:(N-5)/2=44, col:44, built:true, item:null, qty:0, paid:{}}`.
2. center: if no dragon, push dragon centred in cornerTL: `row = 0 + floor((25-5)/2) = 10`, col 10, built.
3. For each fixture kind not present ⇒ `placeFixture` (engine.js:502): centred in the zone's first rect: `row = r0 + floor((rows - size)/2)`, same for col (quarry → (79,11); spirit tree → (10,44); spring → (11,11)).
4. center: if no deco nodes ⇒ `placeDecoRing` around each `cornerBL` and `cornerBR` rect (engine.js:525): ring cells one outside the rect, inside the map, not occupied; skip cells within ±0.55 rad of the direction from rect centre toward map centre (the "entrance"); deco node with `decoScale = 1.6 + random·0.8`, `decoDx = rand(-6,6)`, `decoDy = rand(-3,3)`. Deco is inert but occupies cells.
5. For each spawner: spawn until `live ≥ target` (guard `target·8+50` attempts, stop at first failure); if `live > target`, **trim** the excess (keep the first `target` in array order).
6. `genTimers = generators.map(() => 0)` (**reset every boot** ⇒ each field drops once immediately on load).

### 6.3 `spawnFromSpawner(area, sp)` — engine.js:460

Up to 40 attempts: pick random size from `sp.sizes`, random rect of `sp.zone`,
skip if the rect is smaller than size, random top-left within it; reject if any
cell occupied; reject if `sp.spacing` and any same-spawner node has
`hypot(Δrow, Δcol) < spacing` (top-left distance). On success create the node:
`hitsLeft = useTiers ? tier.hits||1 : sp.hits||1`; `regrowSec = useTiers ?
tier.timer : sp.regrow||10`; `swingMs = sp.swingMs||350`; `perHit, drops,
rareDrop` copied from the spawner (tier nodes read the tier's specs via
`nodeSpecs`); `surfaceUntil = interaction=="surface" ? now + surfaceWindow·1000 : 0`.
Push + patch occupancy; return node or null.

`nodeSpecs(area,node)` (engine.js:451): `useTiers ? tiers[tier-1].{perHit,drops} : node.{perHit,drops}` (missing ⇒ []).

### 6.4 Harvesting — `harvestNode(area, id, isAuto, held)` engine.js:1022, `harvestSwing` engine.js:1035

```
harvestNode:
  node = byId; if !node or node.deco: return false
  if !isAuto: onSfx("harvest", area)
  tag = autoHarvesting ? undefined : "manual"
  if tag=="manual":
     grace = node.fixed ? 8000 : 4000
     refreshNodeGrace(area, node, grace)   // re-stamp this node's earlier ground drops: manualAt = max(manualAt, now+grace-4000) for g._src==node.id (only items that HAVE manualAt)
     manualSrc = {id: node.id, grace}
  try harvestSwing(...) finally manualSrc = null
```

`harvestSwing`: `flashMs = max(swingMs||400, 1000) + 300`. For chop/break/quarry set `node.hitAt = now`.

- **quarry** (fixtures): `clicks++`; if isAuto `autoFlash = now+flashMs`. When `clicks ≥ (clicksPerDrop||5)`: `clicks=0`; `amt = dropMin ? rand(dropMin, dropMax||dropMin) : 1`; **×2 if stoneheart blessing**; drop `dropItem||"stone"` at node centre with tag; `totalGathered += amt`; rare: `random < rareDrop.chance` ⇒ drop 1 rare, `totalGathered++`. Fixtures never deplete.
- **chop**: per swing roll each `perHit` spec into `node.pending[item]`; `hitsLeft--`; flash if auto; if `hitsLeft>0` return. On felling: `flushPending` (drop accumulated pending at centre, add to totalGathered), then `grantDropsGround(drops, mult 1)`, then `depleteNode`.
- **break**: `hitsLeft--`, flash; nothing until 0; then drops ×(stoneheart?2:1); rare roll (1 item); `depleteNode`.
- **instant / surface**: drops ×1, flash, `depleteNode`.

`grantDropsGround(area,node,specs,mult,tag)` (engine.js:830): per spec `amt = rollAmount·mult`; if >0 drop at node centre, `totalGathered += amt`.

### 6.5 Regrow — `depleteNode(area,node)` engine.js:693

Remove node (patch occupancy) and queue:
```
delay = (node.regrowSec||10) * 0.8^upgrades.speed * (TEST?timeScale:1) * 1000
        * (verdant blessing ? 0.5 : 1) * prestigeFactor() * 0.9^perk(regrow)
spawnQueue.push({at: now + delay, kind: node.spawnerKind})
```
The replacement spawns at a **new random spot** (nodes relocate). Fixtures are never depleted.

### 6.6 Field generators — engine.js:2352

Per generator `gi` of an unlocked area:
```
interval = gen.intervalMs * timeScale * 0.8^(gen.upgrade ? upgrades[gen.upgrade] : 0)
           * 0.9^perk(bounty) * prestigeFactor()
tm = periodic(genTimers[gi]||0, interval, now); if tm.n==0: skip; genTimers[gi] = tm.next
z = first rect of gen.zone; field px box = [c0*32-16, (c1+1)*32+16] × [r0*32-16, (r1+1)*32+16]
inField = count ground items of gen.item OR gen.rareDrop.item inside the box; rareIn = rare subset
repeat tm.n times:
  if inField >= gen.cap:
     if rare and rareIn < 2 and random < rare.chance:
        drop rare at random cell centre of z, tag "gen"; rareIn++; inField++
     continue
  inField++; drop gen.item at random cell centre (rand(c0,c1)+0.5)*32, (rand(r0,r1)+0.5)*32, NO tag
  if rare and random < chance: drop rare (tag "gen"); inField++; rareIn++
```

### 6.7 Fixture specifics

- **Quarry rock** (center BL): manual/hold only (no autoTap); passive stone comes from the stone generator (Quarry Output upgrade speeds that generator, not clicks).
- **Spirit Tree** (center midTop): manual + `autoTap` (automation swings it `level` times/tick, §13.1). No generator (the woodField generator is separate).
- **Spring** (fishing TL): manual only; the water generator wells into springField.

---

## 7. Enemies — engine.js:2620-2653, 2689-2774

Config (center only, data.js:146): zone cornerTR, Fox Spirit, `cap 1, hp 3,
speed 30 px/s, respawnMs 6000, attackMs 400, drops spirit_essence 1-2`;
`baitSpawn` Spirit Boar `hp 8, speed 18, drops beast_bone 1-2 + spirit_essence 1`.

**Spawn (each gameTick):** walk rect `x ∈ [(c0+1)·32, c1·32]`, `y ∈ [(r0+1)·32, r1·32]`.
```
cap = ecfg.cap + upgrades.enemyCap
missing = max(0, cap - count(enemies where kind != "boss"))
rq = enemyRespawns
if rq.length > missing: sort ascending; truncate to missing     // keep earliest clocks
while rq.length < missing: rq.push(now)                          // unclocked slots fill now
for i from end to 0: if rq[i] <= now: remove; push fox {id, x:rand(x0,x1), y:rand(y0,y1), hp, maxHp:hp, tx,ty random, hitAt:0}
```
**Wander (each gameTick):** `step = (en.spd || ecfg.speed)/20` px (assumes 50 ms
ticks — **not** time-scaled; offline replay moves enemies less, which is
harmless). If `dist(target) < step` or `random < 0.01` ⇒ new random target in
the rect; else move `step` toward it.

**Hit test** `enemyAt(area,x,y)` = first enemy within 22 px.

**`attackEnemy(area,id)`** (engine.js:2761): sfx `"hit"`;
`dmg = 1 + upgrades.damage + (MartialVigor ? 2 : 0) + perk(fury)`;
`R = upgrades.aoe · 1.5 · 32`; hit set = target + (if R>0) every enemy within R
of the target (bosses included); `damageEnemy` each.

**`damageEnemy`** (engine.js:2695): `hp -= dmg; hitAt = now`; if ≤0: remove,
sfx `"kill"`; loot table = boss ? `baitSpawn.drops` : `drops`; each spec
`rollAmount · (Vigor ? 2 : 1)` dropped at the enemy position tagged manual,
added to `totalGathered`. Non-boss: push respawn clock
`now + respawnMs·timeScale·prestigeFactor()` and `stats.foxKills++`. Bosses
never respawn and never count toward the cap.

**Martial Vigor**: `combatBuffActive()` = `combatBuff.until > now`
(engine.js:136); set by quaffing a Vitality Pill (§5.5) — `45000·buffScale` ms,
+2 damage, loot ×2 (`DATA.VITALITY`).

**Beast Bait**: §5.5 step 2.

---

## 8. Buildings

### 8.1 Ghosts & construction

- Placement makes a ghost (§3.5). Ghosts occupy cells and count as colliders; a ghost burner's rack is reserved against placement but not occupancy until built.
- `buildingNeeds(b)` (engine.js:1794) = `cost − paid` positive parts. Fed via `feedNeeds` (§5.4) one item per press/beat. Completion: §5.5 step 12.
- Building costs are **not** TEST-scaled.

### 8.2 Demolition — `demolishBuilding(area,id)` engine.js:1814

Refuses `indestructible` (Altar, Dragon). All refunds drop at the footprint
centre tagged **manual**:
- Built: 100% of `cost`; storehouse/seal contents; `inv` stacks (gatherer/stoker); converter `stock` **plus** the in-progress batch's inputs if `smeltDoneAt>0`; pavilion disciples as `robe`×disciples (buns not refunded); gate `offered` items. Burner fuel is **not** refunded.
- Ghost: only `paid`.
Then remove it and **filter every lantern's links** referencing its id. In-flight wisps toward it drop their cargo next tick (§11.6).

### 8.3 Unlock / reveal — engine.js:1110-1158

- `isVeteran()` = `ascensions>0 || quest.idx ≥ QUESTS.length`.
- `questClaimed(id)` = quest index `j` exists and `quest.idx > j`.
- `isBuildingUnlocked(type)`: unknown ⇒ false; `stageUnlock != null` ⇒ `dragon.stage ≥ stageUnlock` (forge 1, algae_farm 2, herb_garden 3); no `REVEAL[type]` ⇒ `!!unlocked` (Altar/Dragon false); `builtTypes[type]` ⇒ true; else any condition: `{stage:n}` ⇒ stage ≥ n (always checked), `{quest:id}` ⇒ veteran or claimed, `{region:k}` ⇒ veteran or unlocked.
- REVEAL table (data.js:686): storehouse←quest wood; gathering_stone/wisp_lantern/warding_seal/workbench/kiln/paper_mill/charcoal_pit←quest build; infusion_array←fox; furnace_spirit←link; loom/mill/brewery←region farm or quest waters; jade_carver/cauldron←region mine; meditation_pavilion←quest weaver or region grove; pill_furnace/star_anvil/talisman_atelier←stage 3; dragon_shrine/ascension_gate←stage 4.
- Badges: `isBuildingNew` = not ascended, unlocked, `buildSeen<2`; `markBuildSeen` sets 2 (hover); `markBuildListed` sets 1 for unlocked unseen (menu opened); `buildMenuHasNew` = not ascended and any unlocked type with seen<1. `buildingCatalog()` = unlocked types.

### 8.4 Building catalogue (data.js:321-467)

| Type | Size | Cost | Behaviour |
|---|---|---|---|
| center (Altar) | 5×5 | — | indestructible; upgrade job sink (§12.3) |
| dragon | 5×5 | — | indestructible; tributes/pills (§12.1) |
| workbench | 3×3 | wood 8 | converter: Plank(wood3→1, 4 s), Tools(plank2+iron_bar1→1, 6 s) |
| kiln | 3×5 burner | wood 10, clay 5 | Brick(clay2→1, 5 s), Glass(sand3→2, 5 s), Obsidian Glass(obsidian1→glass2, 5 s) |
| paper_mill | 3×3 | wood 10, stone 5 | Paper(bamboo1+wood2→1, 5 s) |
| infusion_array | 3×3 | stone 10, spirit_essence 5 | Spirit Stone(stone3+ess1, 8 s), Spirit Jade(jade1+ess2, 9 s) |
| loom | 3×3 | wood 10, plank 4 | Cloth(cotton3, 5 s), Rope(cotton2+algae2, 5 s), Robe(cloth2+herb1, 8 s) |
| mill | 3×3 | wood 8, stone 6 | Rice Flour(wheat2, 4 s), Spirit Buns(flour2+water1, 6 s) |
| brewery | 3×3 | wood 8, clay 6 | Spirit Wine(wheat2+water2+leaves1, 8 s) |
| cauldron | 3×3 | stone 8, iron_bar 2 | Qi Elixir(herb1+water2+ess1→2, 8 s), Vitality Pill(fish1+herb1+water1, 7 s), Beast Bait(fish2+algae2, 6 s), Moon Elixir(moonpetal2+water1→qi_elixir 2, 8 s) |
| jade_carver | 3×3 | wood 6, stone 8 | Jade(jade_shard3, 6 s) |
| pill_furnace | 3×5 burner | brick 6, iron_bar 4, tools 2 | Ember/Verdant/Swiftwind/Stoneheart pills (qi_elixir1 + firestone/herb/cotton/spirit_stone 1, 9 s) |
| star_anvil | 3×5 burner | iron_bar 6, tools 3, glass 2 | Star Steel(iron_bar2+firestone1+beast_bone1→2, 10 s), Astral Steel(star_fragment3+iron_bar2→star_steel 2, 9 s) |
| talisman_atelier | 3×3 | plank 6, jade 2, glass 2 | Talisman(paper2+spirit_jade1+qi_elixir1, 10 s) |
| charcoal_pit | 3×3 | stone 6, clay 4 | Charcoal(wood2, 4 s) — not a burner |
| forge | 3×5 burner | wood 5, stone 10 | Iron Bar(iron_ore2, 6 s); stageUnlock 1 |
| dragon_shrine | 3×3 | brick 10, cloth 8, obsidian 4 | `shrine`: blessings +60 s, scales 2× (§12.2) |
| ascension_gate | 5×5 | talisman 3, star_steel 3, dragon_scale 3 | unique; offerings; ascension (§14) |
| storehouse | 3×3 | wood 12 | typed buffer, cap 200 |
| algae_farm | 3×3 | wood 12, algae 6 | `waterOnly`, `gen {algae, 2000 ms, cap 24}`, stageUnlock 2 |
| herb_garden | 3×3 | wood 10, water 5, clay 5 | `gen {spirit_herb, 2500 ms, cap 24}`, stageUnlock 3 |
| furnace_spirit | 1×1 anyZone | stone 4, ess 2 | `stoker {radius 3, cap 20}` |
| meditation_pavilion | 3×3 | plank 6, cloth 4, rope 2 | `roster {cap 3, recruit robe, foodCap 20, foodValues {spirit_buns:1, spirit_wine:3}, produce spirit_essence, produceMs 6000}` |
| gathering_stone | 1×1 anyZone | stone 5 | `gather {radius 8, cap 60}` |
| wisp_lantern | 1×1 anyZone | wood 5, stone 5 | `lantern {rateMs 1000, speed 170}` |
| warding_seal | 1×1 anyZone | wood 3, stone 3 | `seal {cap 20}` |

`initLogistics(b)` (engine.js:1259): gather/stoker ⇒ `inv=[]`; lantern ⇒ `links=[], connIdx=0, nextSend=0`; roster ⇒ `disciples=0, buns=0, nextCultivate=0`.

### 8.5 Storehouse / Seal semantics

- Storehouse: `item` (type) + `qty ≤ 200`. Untyped when empty unless `lock`. Starter storehouses are locked to their item.
- Seal: same storage fields; only ever holds its tuned `item`; `qty ≤ 20`; tuned by right-click (§5.5 step 8); untuned seal accepts nothing.

### 8.6 Generator buildings — engine.js:2395

Per built building with `gen`:
```
tm = periodic(b.nextGen||0, gen.intervalMs * timeScale * prestigeFactor(), now)   // NO bounty perk
if !tm.n: continue; b.nextGen = tm.next
centre (bx,by); R = 4*32
near = count ground gen.item within R (circle) of centre
if near >= gen.cap: b._pileFull = true; b._pileAt = 0; continue
if outputPileFull(area,b,gen.item,now): continue
for ev < tm.n while near < cap: drop 1 at (bx+rand(-64,64), by+rand(-64,64)) tag "crafted"; near++
```
Practical note: drops land inside the 3-cell output-pile box, so the
**effective cap is 12** (OUTPUT_PILE_MAX), not 24.

### 8.7 Building status — `buildingStatus(area,b)` engine.js:1641

null for ghosts/unknown. Converters: no recipe ⇒ idle; `atCap` = every input
stock ≥ stockCap; running batch ⇒ `full "Stock full"` if atCap else `working`;
not running: first input short (recipe order) = `missing`; none missing ⇒
pile full (`b._pileFull && _pileItem==output`) ⇒ `{state:"full", pile:true,
label:"Output pile full"}`; burner with no fuel ⇒ `nofuel`; else `working`.
Missing: no stock at all and not a link target ⇒ `idle {item:missing}`; else
`starved "Needs X"`. Gatherer/stoker: total ≥ cap ⇒ `full`. Seal/storehouse
full ⇒ `full`. Roster: disciples>0 & buns≤0 ⇒ `starved spirit_buns`;
disciples>0 & `_pileFull` ⇒ pile. Gen building `_pileFull` ⇒ pile.

`craftRate(b, forDisplay)` (engine.js:1599): ring `b._crafts` of finish times
(≤240, pruned to 60 s), `b._craftFirst` = start of the current run. `n` = crafts
within 60 s; `n<2` ⇒ 0 (display) / n; `span = clamp(now - first, 1000, 60000)`;
result `span ≥ 60000 ? n : (n-1)·60000/span` per minute. Not recorded during
replay. Transient.

---

## 9. Converters — engine.js:1305-1415, 2414-2468

### 9.1 Recipe & stock

- `recipeOf(b)` = `recipes[b.recipe||0]`.
- `stockCap` = `recipe.stockCap || 20` (none set ⇒ 20) per input item.
- `smeltRemaining(b)` = per input `qty − stock` (>0). `smeltSpace(b)` = per input `cap − stock` (>0). `canStartBatch(b)` = every input stock ≥ qty. `craftsPossible(b)` = `min floor(stock/qty)` (fuel ignored).

### 9.2 Recipe switch — `setRecipe(area,id,idx)` engine.js:1345

Invalid ⇒ false; same index ⇒ true (no-op). `held = stock ∪ (smeltDoneAt>0 ?
old recipe inputs : {})` (the batch in progress is **cancelled and refunded**).
New stock = held items the new recipe also uses (amount kept, may exceed the
new cap). Other items: `handAdd`, overflow dropped at footprint centre tagged
manual. `smeltDoneAt=0`, `recipe=idx`, `stats.recipeSwitches++`. Burner fuel is untouched.

### 9.3 Batch loop (gameTick step 5)

Per built converter `b` with a recipe; `isBurner = cfg.fuel`; `emberK =
isBurner ? 0.85^perk(ember) : 1`:
```
for ev in 0..399:
  doneAt = 0
  if b.smeltDoneAt and now >= b.smeltDoneAt:                       // finish
     if isBurner: burnFuel(b, max(0, smeltDoneAt - (fuelBurnAt || smeltDoneAt)) * emberK)
     doneAt = smeltDoneAt
     drop output×outputQty at (centre x, (row+h)*32 + 12) tag "crafted"
     stats.totalCrafted += qty; if !replay: noteCraft(b, now); onSfx("craft")
     smeltDoneAt = 0
  if smeltDoneAt or !canStartBatch(b) or outputPileFull(area,b,output,now): break
  cost = timeMs * timeScale * prestigeFactor()
  if isBurner and ember blessing: cost *= 0.5
  if isBurner and !(fuelTotal(b) > 0): break                       // needs ANY fuel to start
  stock[input] -= qty for each input
  t0 = (doneAt and doneAt >= now - tickGap) ? doneAt : now          // catch-up re-arm
  smeltDoneAt = t0 + cost; fuelBurnAt = t0
  if smeltDoneAt > now: break
if isBurner:
  if smeltDoneAt and now < smeltDoneAt: burnFuel(b, max(0, now - (fuelBurnAt||now)) * emberK)
  fuelBurnAt = now                                                 // idle time never burns
```
Key properties: a started batch always completes even if fuel runs out mid-way
(the "remainder is free"); fuel is burned continuously while running, not paid
up-front; the finished batch's output always lands even if the pile is full.

### 9.4 Inputs — feeding

- By hand: §5.5 step 7 (ratio-balanced). By wisp: `endpointGive` (§11.3) adds 1 to `stock[item]`.
- Acceptance: §11.3 `endpointAccepts` (stock + in-flight < cap; fuel rules for burners).

### 9.5 Output back-pressure — engine.js:1616-1631

`outputPileCount(area,b,item)` = ground items of `item` with
`x ∈ [col·32−96, (col+w)·32+96]`, `y ∈ [row·32−96, (row+h)·32+96]` (inclusive).
`outputPileFull(area,b,item,now)`: cached result while `now < b._pileAt` and
same item; else recompute, `_pileAt = now+500`, `_pileFull = count ≥ 12`.
Used by converters (start gate), generator buildings, pavilions.

### 9.6 Status lines

See §8.7 `buildingStatus`.

---

## 10. Fuel

### 10.1 Rack (FIFO) — engine.js:1315-1340

`b.fuelQ = [{item, rem, total}]`, **max 6**. New fuel is inserted at **index 0**
(`unshift`), burning consumes from the **end** (oldest first).
- `addFuelItem(b,item)`: unknown fuel or full ⇒ false; else unshift `{item, rem:FUEL[item], total:FUEL[item]}`.
- `fuelTotal` = Σ rem; `fuelSpace` = 6 − length.
- `burnFuel(b, ms)`: `ms *= 2` under Vow of the Cold Hearth; while ms>0 and queue non-empty: take `min(ms, back.rem)` from the back; pop when `rem ≤ 0.5`.

### 10.2 Burn rate

Real-time burn while a batch runs: fuel-ms consumed = elapsed ms × `0.85^ember`
× (coldhearth ? 2 : 1). Ember blessing halves batch duration but not the burn
rate ⇒ half the fuel per batch. Timers' prestigeFactor/TEST shorten batches and
so also reduce fuel per batch.

### 10.3 Fuel vs. ingredient

Firestone is both a fuel and an input (Ember Pill, Star Steel). Rule
everywhere: **if the item is an input of the current recipe it goes to stock,
otherwise (fuel item) to the rack**. Wisp acceptance counts in-flight fuel only
for fuel items that are not current inputs.

### 10.4 Furnace Spirit (stoker) — engine.js:2561

Each gameTick, for each built stoker `b` (centre c, `R = 3·32`): for each built
burner `t` in building order with `fuelSpace(t) > 0` and
`dist(centre t, c) ≤ R + 1.5·32` (centre-to-centre): pick the stoker stack with
the **highest FUEL value** (stable sort), decrement it (remove empty), and
`addFuelItem(t, item)`; if the stoker is empty ⇒ stop. **One item per burner
per tick** (20/s), regardless of how full the rack is. Stoker buffer: fuel
items only, cap 20 total, filled by hand (§5.5 step 5) or wisp.

### 10.5 Ember Heart perk — `emberK = 0.85^level` (§9.3).

---

## 11. Wisp logistics

### 11.1 Roles

| Building | Link source? | Link target? | Holds |
|---|---|---|---|
| gathering_stone | yes | yes | mixed `inv`, cap 60 |
| furnace_spirit (stoker) | yes | yes | fuel `inv`, cap 20 |
| warding_seal | yes | yes | one tuned type, cap 20 |
| storehouse | yes | yes | one type, cap 200 |
| converters | no | yes | input stock / fuel rack |
| meditation_pavilion | no | yes | buns (food value) |

`canBeLinkSource` / `canBeLinkTarget` (engine.js:1782-1791) require `built`.

### 11.2 Gathering Stone — engine.js:2474-2506, 1537-1571

Each tick per built stone, before its lantern-related processing:
1. `ejectUnwanted(area,b)`: `ever = stoneAccepts(area,b,true)` (capacity-free) → stored as transient `b._accEver`. If `ever` non-null, every `inv` stack whose item ∉ ever is dropped (all of it) at `(centre.x, centre.y + 32)` tagged manual and removed.
2. `stoneFly` = number of wisps whose `toId == b.id` (outbound to it or returning home to it).
3. If `gatherTotal + stoneFly < 60`: `acc = stoneAccepts(area,b)` (capacity-aware); `R = 8·32`. For each ground item: skip if `acc` and item ∉ acc; skip if in grace; `d = dist`; skip `d > R`; if `d ≤ 22` ⇒ if still room and `endpointGive(b,item)` collect (remove from ground); else pull toward centre by `min((2 + (1-d/R)·4) · min(13, max(1, (tickGap||50)/50)), d)` px and stamp `_pullAt=now, _pullTo=c`.

`stoneAccepts(area,b,ever)`: null for non-stones. Scan **every building's
links** in the area; for links `from == b.id`: `types = targetTypes(target,
room = !ever)`; any `null` ⇒ return null (accept anything); union the rest.
No outgoing links ⇒ null (collect everything).

`targetTypes(t, room)` (engine.js:1509): unbuilt ⇒ []; gather ⇒ null; seal:
untuned ⇒ null, full & room ⇒ [], else [item]; storehouse: full & room ⇒ [],
typed ⇒ [item], else null; stoker ⇒ all FUEL keys; roster ⇒ keys of
foodValues; converter ⇒ current recipe inputs + (burner) all FUEL keys; else [].

### 11.3 Accept / give / take — engine.js:1436-1497, 1680-1713

`inFlightTo(area,dst)` = `{n, by:{item:count}}` over wisps with `toId == dst.id`.

`endpointAccepts(b, item, fly=none)`; `fi = fly.by[item]`:
- not built ⇒ false.
- seal: `b.item == item && qty + fi < 20`.
- storehouse: `typ = b.item || first key of fly.by`; `(typ ? typ==item : true) && qty + fly.n < 200`.
- gather: `gatherTotal + fly.n < 60 && (!b._accEver || item ∈ _accEver)`.
- stoker: `item is fuel && gatherTotal + fly.n < 20`.
- roster: `due = Σ foodValue(it)·count` over fly; `foodValue(item) > 0 && foodCap − buns − due ≥ foodValue(item)`.
- converter: `isInput = item ∈ recipe.inputs`; if burner and item is fuel and !isInput ⇒ `fuelSpace − (in-flight fuel items that are not inputs) > 0`; elif !isInput ⇒ false; else `stock[item] + fi < stockCap`.
- else false.

`endpointGive(b,item)` (no fly): if `!endpointAccepts(b,item)` ⇒ false. seal/storehouse: adopt type if none, `qty++`. gather/stoker: increment or append stack. roster: `buns = min(foodCap, buns + foodValue)`. converter: burner+fuel ⇒ `addFuelItem` (note: this branch runs for fuel items even if they are inputs? — no: `endpointAccepts` already routed; but `endpointGive` checks `cfg.fuel && FUEL[item]!=null` **without** the isInput test, so a firestone delivered to a Pill Furnace on the Ember recipe goes to the **rack** if there's rack space, else returns false). Otherwise `stock[item]++`.

> ⚠ Faithful-port gotcha: `endpointAccepts` sends an *input* firestone to stock,
> but `endpointGive` puts any fuel item into the rack first. Preserve or
> consciously fix (document the decision).

`endpointTake(b,item)`: gather/stoker: decrement that stack (remove empty);
seal/storehouse: requires `b.item==item && qty>0`, `qty--`, clear type when 0
unless `lock`.

`pickTransfer(src,dst,fly)`: gather/stoker ⇒ first `inv` stack (order) with
qty>0 that dst accepts; seal/storehouse ⇒ its item if qty>0 and accepted; else null.
`sourceHolds(src)`; `sourceMatches(src,dst)` (types the target could ever take, capacity-free).

### 11.4 Links — engine.js:1725-1780

- `linkSourceTypes(src)`: stoker ⇒ FUEL keys; seal/storehouse ⇒ `[item]` or null; gather ⇒ null.
- `linkTargetTypesEver(t)`: gather ⇒ null; seal/storehouse ⇒ `[item]`/null; converter ⇒ union of ALL recipes' inputs + FUEL keys if burner; else `targetTypes(t)`.
- `linkRefusal(area,from,to)`: not a source ⇒ `{code:"source","Not a link source"}`; not a target ⇒ `{code:"target","Not a link target"}`; same id ⇒ `{code:"self","A building can't feed itself"}`; either types null ⇒ allowed; intersection non-empty ⇒ allowed; else `{code:"types", "<Target> can't use <a/b…>"}`.
- `addLink(area, lanternId, from, to)`: lantern must exist; refusal ⇒ false; push `{from,to}`; `stats.linksAdded++`. Links are stored **on the lantern**, any distance within the area.
- `removeLink(area, lanternId, index)`: splice; if `connIdx ≥ length` ⇒ 0.

### 11.5 Lantern beat — engine.js:2507-2558

Per built lantern with ≥1 link and `now ≥ (nextSend||0)`:
```
haste = upgrades.wispRate      // of THIS area (the tree node exists only for center)
wind  = swiftwind blessing ? 0.5 : 1
beat  = rateMs(1000) * timeScale * 0.85^haste * wind * prestigeFactor() * 0.9^perk(gale)
tm = periodic(nextSend||0, beat, now)
due = tm.next - tm.n*beat; idle = (tm.n == 0)
for ev < tm.n (due += beat):
   pick = -1; pickSeq = +inf
   for k in 0..links-1:  idx = (connIdx + k) % L; l = links[idx]
      src,dst = byId; skip if missing or src unbuilt
      item = pickTransfer(src, dst, inFlightTo(area,dst))
      l._stat ||= {sentAt:0, fail:null}
      if !item: l._stat.fail = !sourceHolds(src) ? "empty" : sourceMatches(src,dst) ? "refused" : "nomatch"; continue
      l._stat.fail = null
      if (l._seq||0) < pickSeq: pick = idx, pickItem = item, pickSeq = l._seq||0     // least-recently-served; ties → rotation order from connIdx
   if pick < 0: idle = true; break
   l = links[pick]; l._stat.sentAt = now; l._seq = b._seq = (b._seq||0)+1
   endpointTake(src, item)
   spawn wisp {id, x0,y0 = x,y = centre(src), item, toId:l.to, fromId:l.from,
               t0: min(now, due), sp: speed(170) * (1 + 0.25*haste) / wind}
   connIdx = (pick+1) % L
nextSend = idle ? now + 250 : tm.next
```
`_seq` counters are transient (after reload all 0 ⇒ plain round-robin from connIdx).
Note `t0 = min(now, due)` back-dates catch-up wisps so their flight already progressed.

### 11.6 Wisp flight — engine.js:1716, 2594-2618

`wispPos(area,w,now)`: target = centre of `toId` building (missing ⇒ `{x:w.x, y:w.y, frac:1}`);
`D = dist(x0,y0 → target)`; `frac = D ? min(1, (now−t0)/1000 · sp / D) : 1`;
position = lerp. Flight is a pure function of time.

Per tick per wisp:
- dst missing or unbuilt ⇒ drop item at `(w.x, w.y)` (no tag), remove.
- Else update `w.x,w.y = wispPos`. If `frac ≥ 1`:
  - `endpointGive(dst,item)` ⇒ delivered, remove.
  - else if not `returning` and `fromId` building exists ⇒ `returning = true; toId = fromId; x0,y0 = current; t0 = now` (flies home; reserved against home via inFlightTo).
  - else drop at `(x, y+24)` (no tag), remove.

### 11.7 Wisp Haste / Gale / Swiftwind

Beat × `0.85^wispRate × 0.9^gale × (swift ? 0.5 : 1)`; flight speed ×
`(1+0.25·wispRate) / (swift ? 0.5 : 1)`. Only `areas.center.upgrades.wispRate`
can ever be > 0 (tree node lives on center) ⇒ Haste affects **only Center
lanterns** — faithful behaviour, flag for design review.

---

## 12. Altar upgrades, Dragon, Disciples

### 12.1 Sleeping Dragon — engine.js:1932-1957, 2162-2207

Stages (data.js:472): 0 `leaves 15` → teaches Forge; 1 `stone 25, clay 10` →
Algae Farm; 2 `iron_bar 8, algae 15, water 10` → Herb Garden; 3 `spirit_herb 20,
spirit_essence 15, iron_bar 5` → **awakening**. `dragonStage()` = current
stage def or null after the last.

- `tributeMult(asc)` = `max(0.4, 1/(1 + 0.25·ascensions))`.
- `dragonNeeds()` = per item `max(1, ceil(scaled(q) · tributeMult)) · (vow restless ? 2 : 1)`.
- `dragonRemaining()` = needs − `dragon.paid`.
- `dragonTribute(i)`: current stage ⇒ `paid + remaining` per item; other stages ⇒ `scaled(q)·(restless?2:1)` (**without** tributeMult — display inconsistency, keep or fix knowingly).

Right-click on the built dragon (§5.5 step 4):
1. Front item is a dragon pill (`DRAGON_BUFFS` key) ⇒ take 1; `dur = (60000 + 30000·center.upgrades.affinity + (shrineBuilt ? 60000 : 0)) · 1.2^perk(bless) · buffScale`; `GS.buff = {kind, until: now + dur}` (replaces any active one) ⇒ `{fed, once}`. Works at any stage.
2. `pill` = first pill anywhere in hand. If no stage remains (awake) ⇒ reorder pill to front or null.
3. `feedNeeds(dragonRemaining(), paid)`; on completion: `stage++`, `paid = {}`, `msg = text`, `msgUntil = now+8000`, `dialog = text`, sfx `"dragon"`; if no next stage ⇒ `won = dragonBlessed = true`.
4. Return the feed result, else reorder a pill, else null.

Blessing effects (`buffActive(kind)` = `buff.kind==kind && buff.until > now`):
ember_pill — burner batch time ×0.5; verdant_pill — regrow ×0.5; swiftwind_pill
— lantern beat ×0.5 and wisp speed ×2; stoneheart_pill — quarry-type drops ×2
(rock, Spirit Tree, spring) and break-node drops ×2 (rare finds not doubled).

### 12.2 Dragon scales & Shrine — engine.js:2655-2673, 288

After awakening (center area only): `interval = 45000 · timeScale · (shrine ? 0.5 : 1) · prestigeFactor()`.
If `!GS.dragonScaleAt` ⇒ set `now + interval`; else when `now ≥ dragonScaleAt`:
re-arm `now + interval` (not catch-up), and if fewer than 5 scales lie within
6 cells of the dragon centre ⇒ drop 1 at `(cx + rand(-60,60), cy + 90)` (no
tag; scales are class 2). `shrineBuilt()` = any built `shrine` building in any
area. Shrine also adds +60 s to blessings.

### 12.3 Altar upgrade tree — engine.js:2070-2157, data.js:520

Nodes `{id, area, type, links, costs[lvl]}`; max level 3 for every type
(`upgradeLevel`, engine.js:2086; legacy `tier` max 4). Selectability (UI,
ui.js:1564): BFS from owned nodes (lvl>0) **plus root "hand"** over the
undirected link graph; distance ≤1 visible, 2 "mystery", ≥3 hidden; selectable
= distance ≤1 and (root, or owned, or adjacent to an owned node).

- `upgradeCost(area,type)` = `scaled(costs[lvl])` per item, null when maxed.
- `selectUpgrade(area,type)`: maxed ⇒ false; same job ⇒ true; else refund the current job (`refundUpgradeJob`: drop `paid` at Altar centre tagged manual) and set `upgradeJob = {area, type, needs: cost, paid:{}}`.
- Fed at the Altar (§5.5 step 3). `jobRemaining(job)` = needs − paid.
- `applyUpgrade(area,type)`: `stats.upgradesApplied++`, sfx `"upgrade"`, increment the field; `hand` ⇒ `handLevel++`, `handCap += 5`.

Effects by type (per `area` unless noted):

| type | Effect |
|---|---|
| hand (center) | +5 handCap |
| speed | regrow delay ×0.8^lvl (that area) |
| harvestSpeed | `harvestInterval` ×0.8^lvl (that area) |
| automation | bot budget `AUTOMATION_CLICKS[lvl]` and autoTap swings = lvl (that area) |
| quarry (center) | generators with `upgrade:"quarry"` interval ×0.8^lvl |
| enemyCap / damage / aoe (center) | +1 fox / +1 damage / +1.5-cell splash radius per lvl |
| wispRate (center) | lantern beat ×0.85^lvl, wisp speed ×(1+0.25·lvl) |
| affinity (center, global) | blessings +30 s/lvl |
| discipleCap (center, global) | +2 roster cap per pavilion/lvl |

Tree nodes: hand, wisps, affinity, disciples, spd_c, auto_c, act_fi(fishing harvestSpeed), act_c, quarry, act_m(mine harvestSpeed), spd_f, act_f, auto_f, spd_m, spd_fi, auto_m, foe_cap, foe_dmg, foe_aoe — costs in data.js:520-583.

### 12.4 Disciples / Meditation Pavilion — engine.js:1266-1293, 2576-2592

- `rosterCap(b)` = `3 + 2·center.upgrades.discipleCap + perk(hall)`.
- `foodValue(b,item)` = `foodValues[item] || 0` (buns 1, wine 3).
- `recruitDisciple(area,id)`: built pavilion, below cap, hand has a robe ⇒ take 1, `disciples++`, `stats.disciplesRecruited++`.
- Food in: hand (§5.5 step 6) or wisp; `buns = min(20, buns + value)`.
- First completed pavilion of a run starts with `buns = 20` (`pavilionSeeded`).
- Cycle (only while `disciples > 0`): `tm = periodic(nextCultivate||0, produceMs(6000)·timeScale·prestigeFactor(), now)`; if tm.n set `nextCultivate = tm.next`; per event: `worked = min(disciples, buns)`; stop if 0; stop if `outputPileFull(spirit_essence)`; `buns -= worked`; drop `worked` essence at `(cx + rand(-40,40), (row+h)·32 + 12)` tagged crafted.

---

## 13. Automation, quests, reveal

### 13.1 `automationTick()` — engine.js:2786

Per unlocked area (area order):
```
level = upgrades.automation
if level <= 0: _autoSkip = []; autoPaused = false; continue
loose = count of ground items with evictClassOf < 2, per item           // computed ONCE per area per tick
budget = AUTOMATION_CLICKS[level] + perk(autoboost)
nodes = non-deco, non-fixed nodes (stable sort by tier desc — all tier 1 ⇒ array order)
taps  = fixed non-deco nodes whose fixture config has autoTap
autoHarvesting = true
for node in nodes:
   full = yield types (perHit ∪ drops, not rare) with loose >= 120
   if full: add to skip; continue                                       // keeps scanning to list every saturated type
   if clicks >= budget: continue
   harvestNode(area, node.id, isAuto=true); clicks++                    // ONE swing per node per tick
for node in taps:
   t = node.dropItem || "stone"; if loose[t] >= 120: skip t; continue
   repeat level times: harvestNode(area, node.id, true); clicks++       // not limited by budget
autoHarvesting = false (finally)
_autoSkip = skip; autoPaused = skip non-empty
```
Bot swings: no sfx, `autoFlash` badge, no grace, drops tagged `"gen"` (class-2
rare finds become class 1). Surface fish and instant crops count as one click
each; chop/break nodes need several ticks (one swing per tick).

### 13.2 Quests — engine.js:2720-2756, data.js:599-677

Chain (`QUEST_CHAIN = 2`), `quest.idx` = current. Goals read **live** state
(progress counts things done before the quest was shown):

| # | id | goal (cur / need) | reward |
|---|---|---|---|
| 0 | wood | handCount(wood) / 5 | reveal storehouse |
| 1 | leaves | handCount(leaves) / 5 | — |
| 2 | dragon1 | stage ≥ 1 | reveal forge |
| 3 | fox | stats.foxKills / 1 | reveal infusion_array |
| 4 | build | stats.buildingsBuilt / 1 | reveal logistics + workbench/kiln/paper_mill/charcoal_pit |
| 5 | upgrade | stats.upgradesApplied / 1 | items wood 10 |
| 6 | link | stats.linksAdded / 1 | reveal furnace_spirit |
| 7 | explore | farm∨mine∨fishing unlocked | — |
| 8 | dragon2 | stage ≥ 2 | reveal algae_farm; items wood 8 |
| 9 | iron | `need = max(1, dragonTribute(2).iron_bar)`; stage ≥ 3 ⇒ done; else `min(need, handCount(iron_bar) + (stage==2 ? paid.iron_bar : 0))` | — |
| 10 | waters | fishing unlocked | reveal loom/mill/brewery |
| 11 | dragon3 | stage ≥ 3 | reveal herb_garden/pill_furnace/star_anvil/talisman_atelier |
| 12 | weaver | min(rope,2) + min(cloth,6) in hand / 8 | reveal pavilion; items spirit_herb 2 |
| 13 | cultivate | stats.disciplesRecruited / 1 | — |

`questProgress(i)` = `{cur: min(cur,need), need, done: cur ≥ need}`.
`claimQuest()`: not done ⇒ false; `idx++`; for each reward item (known, qty>0):
`handAdd`; the rest dropped (no tag) at `(altar centre x, (altar.row+5)·32 + 14)`.
Returns `{id, toHand, dropped, at}`. `reward.reveal` is display-only (REVEAL drives the menu).

### 13.3 Reveal — §8.3.

---

## 14. Prestige

### 14.1 Ascension Gate — engine.js:221-249

- Unique (ghost or built, any area). Completing it sets `ascendPrompt = true`; left-clicking a built gate re-opens the prompt (UI).
- Offerings (`GATE_OFFERINGS = {items:[talisman, star_steel, dragon_scale], cap 6, perType 2}`): `gateOfferings(b).count = min(6, Σ min(2, offered[it]))`; `gateTakes(b,item)` = item is an offering, count < 6, `offered[item] < 2`. Fed by right-click (§5.5 step 11). Refunded on demolish.

### 14.2 AP — `ascendReward()` engine.js:215

```
regions = number of true entries in world.unlocked (Center included)
AP = round((3 + 2*max(0, regions-1) + perk(apgain) + gateOfferingsCount(first built gate)) * vowMult())
vowMult = VOW_MULT[min(activeVows, 4)] = [1, 1.15, 1.3, 1.5, 1.75]
```

### 14.3 `ascend(nextVows)` — engine.js:298

Engine performs no eligibility check (UI gates it). Sequence:
1. sfx `"ascend"`; `asc = ascensions+1`; `reward = ascendReward()`; `pts = ascendPoints + reward`; `speedFrom = 1/prestigeFactor()`.
2. Vows: `done` copied; each active vow `done[id]++`; new `active` = `nextVows` (known, deduped) **only if the player had already ascended at least once** (`ascensions ≥ 1` before this one).
3. `fresh = makeInitialState()`; carry: `ascensions=asc`, `ascendPoints=pts`, `perks` (same object), `handCap = 20 + 5·perks.hands` (Hand Size levels are lost), `quest.idx = QUESTS.length`, `dragonBlessed = old.dragonBlessed || old.won`, `stats` (lifetime), `introSeen=true`, `endingSeen`, `vows`.
4. Solitude vow active ⇒ `starterPlaced = true` (no starter network).
5. Remembered Paths: open `["mine","fishing","farm"].slice(0, perks.paths)`. Legacy Automation: `automation = max(automation,1)` in `["center","farm","mine"].slice(0, perks.legacy)`.
6. Replace GS; `justAscended = {n:asc, ap:reward, speedFrom, speedTo: 1/prestigeFactor()}` (computed on the fresh state — includes the new marks); save; **reload** (port: re-run the boot sequence `initArea` ×all + `setupStarterNetwork` without a scene reload).

What resets: hand, handLevel, every area (nodes, ground, buildings, upgrades), world unlocks (except Paths), unlock installments, upgrade job, dragon (stage 0 — but `dragonBlessed` keeps the 0.9 speed factor), buffs, `won`, quest (skipped to end), builtTypes/buildSeen, pavilionSeeded, perkShopSeen, dragonScaleAt.

### 14.4 Perks — engine.js:141-210, data.js:774

`perkCost(id)` = `cost[level]` or null when maxed. `buyPerk(id)`: needs AP;
deduct; level++; immediate effects:
- `hands`: `handCap += 5` now.
- `paths`: region `PATH_REGIONS[lvl-1]` if still locked: refund its installments to hand (`refundToHand`: overflow dropped at Center `(PLAY_PX/2, PLAY_PX/2 + 96)` tagged manual) and `openRegion`.
- `frugal`: for each locked region with installments: cost now lower ⇒ excess paid refunded to hand; if nothing remains ⇒ delete installments, open region, sfx `"unlock"`.
- `legacy`: `automation = max(.,1)` in `LEGACY_REGIONS[lvl-1]`.

| Perk | max | AP costs | Effect |
|---|---|---|---|
| haste | 5 | 1,2,3,5,8 | prestigeFactor ×0.95^lvl |
| hall | 5 | 1,2,3,4,6 | +1 roster cap |
| slumber | 4 | 1,2,4,6 | offline cap +2 h |
| hands | 5 | 1,2,3,4,6 | +5 hand cap |
| frugal | 3 | 2,4,6 | unlock cost ×0.8^lvl |
| ember | 4 | 2,3,5,7 | fuel burn ×0.85^lvl |
| apgain | 3 | 3,5,8 | +1 AP per ascension |
| autoboost | 3 | 3,5,8 | +1 automation budget |
| regrow | 3 | 2,4,6 | regrow ×0.9^lvl |
| gale | 3 | 2,4,6 | lantern beat ×0.9^lvl |
| fury | 3 | 2,4,6 | +1 damage |
| bless | 3 | 2,4,6 | blessing duration ×1.2^lvl |
| bounty | 3 | 2,4,6 | field generator interval ×0.9^lvl (not gen buildings) |
| paths | 3 | 3,6,12 | start with Mine / +Fishing / +Farm open |
| legacy | 3 | 4,8,16 | start with Automation L1 in Center / +Farm / +Mine |

### 14.5 Vows — engine.js:251-263, data.js:815

`burden` (hand cap halved), `coldhearth` (fuel burn ×2), `restless` (tributes
×2), `solitude` (no starter network). `vowMarks(includeActive)` = number of
vows with `done ≥ 1` (+ active ones if requested); each mark ×0.96 in
prestigeFactor.

---

## 15. Offline catch-up — engine.js:2839-3155

Principle: **replay the real `gameTick`/`automationTick` on a virtual clock**
(no parallel formulas).

### 15.1 `beginOfflineCatchup()`

```
last = GS.lastSeen; af = GS.offlineAwayFrom; GS.offlineAwayFrom = null
if !finite(last): return null                        // pre-feature save
gap = now - last; if gap <= 90000: return null       // plain reload
resumed = finite(af) && af < last; from = resumed ? af : last
cap = (8 + 2*perk(slumber)) h; elapsed = min(gap, cap)
job = {start:last, end:last+elapsed, virt:last, sinceAuto:0,
       step: max(250, ceil(elapsed/45000)),
       awayMs: now-from, elapsedMs: elapsed, capped: gap-elapsed > 1000, capMs: cap, resumed,
       before: countHeldItems(), samples:[[0, heldTotal(before)]],
       sig:null, flatSince:null, stop,done,saturated,failed:false, summary:null}
offlinePlateauCheck(job, 0, before)
GS.offlineAwayFrom = from
```

`countHeldItems()` = per-item units on the ground, in wisps, in every building
(`item/qty`, `inv`, `stock`) — **hand and pavilion buns excluded** (despite the
comment), unlocked or not.

### 15.2 `stepOfflineCatchup(job, budgetMs)`

Returns true when finished (end reached / saturated / stopped). During a slice:
mute `onGroundDrop` and `onSfx`, set `offlineSim` (skip ground physics) and
`offlineReplay` (no craft-rate stamps), virtualise the clock to `job.virt`.
```
while virt < end:
   gameTick()
   sinceAuto += step; if sinceAuto >= 1000: automationTick(); sinceAuto -= 1000
   virt += step
   sim = virt - start
   if sim - lastSample.sim >= 60000:
       held = countHeldItems(); samples.push([sim, heldTotal(held)])
       if virt < end and offlinePlateauCheck(job, sim, held): saturated = true; break
   if wallElapsed >= budgetMs: break
```
On exception: `failed = stop = true`, rethrow. Always restore clock + hooks + flags.

Note: `tickGap` inside the replay equals `step` (≥250 ms), so stone pulls and
due-time clocks scale correctly; enemy wander does not (cosmetic).

### 15.3 Plateau — `offlineSignature` / `offlinePlateauCheck`

Signature = sorted `item:count` of held totals + per unlocked area
`|area:groundLen/wispLen[p]autoSkip` + per built building `id + status.state +
(qty + gatherTotal + buns + disciples + Σstock)`. `active` = any converter with
`smeltDoneAt>0` or status working. If active, or no streak, or signature
changed ⇒ restart streak (`sig = active ? null : sig`, `flatSince = active ? null
: sim`) and return false. Else saturated when `sim − flatSince ≥ 15 min`.
Checked once per sim-minute. `offlineLevelled(job)` = streak ≥ 60 s (Skip button copy).

### 15.4 Other API

- `skipOfflineCatchup(job)` ⇒ `stop = true` (remainder forfeited).
- `offlineProgress(job)` = 1 if saturated else `(virt-start)/(end-start)` clamped.
- `offlineResumeAt()` (save stamp while unfinished) = `now − max(0, end − virt)` ⇒ next load's gap = unsimulated remainder + time closed; null if no live job.
- `offlineActive()`.

### 15.5 `finishOfflineCatchup(job)` → summary (idempotent)

`done = true`; clear job & `offlineAwayFrom`. `simulatedMs = min(virt,end) − start`;
`rest = elapsedMs − simulatedMs`. Summary:
`{awayMs, elapsedMs, simulatedMs, skippedMs: saturated ? 0 : rest, saturatedMs:
saturated ? rest : 0, flatAtMs: saturated ? flatSince : null, levelled:
!saturated && stop && !failed && offlineLevelled, resumed, capped, capMs, failed,
gained:{item:+Δ}, stalls:[], plateauMs}`. Then (try/catch ⇒ failed):
`settleAfterReplay()`; `gained` = positive deltas of `countHeldItems()`;
`plateauMs` = first sample reaching 99% of the total gain if `≥ 60 s` and
(saturated or `< 0.8·simulatedMs`); `stalls = offlineStalls()`.

`offlineStalls()` (engine.js:2895) per unlocked area:
`ground` (ground ≥ 570), `autopaused`, `autoskip` (names), `nofuel` (burner idle,
stock covers a batch, `fuelTotal < batch cost` — stricter than the real start
rule "any fuel"), `outfull` (converter status label "Output pile full"),
`nobuns` (disciples>0, buns≤0), `stonefull` (gatherer at cap). Names deduped.

`runOfflineCatchup()` = begin + step(∞) + finish (sync; tests).

Boot tiers (main.js): <90 s nothing; <10 min sync before first frame + toast;
≥10 min async sliced with progress, Skip, then full summary.

---

## 16. Starter network — `setupStarterNetwork()` engine.js:627

Runs once (`starterPlaced`), center only. `placeBuilt(type, r, c, extra)`
(engine.js:615) uses `findSpot` (engine.js:606): ring search radius 0..13
(Chebyshev rings, row-major within a ring) for the first cell where
`canPlaceBuilding` holds; creates a **built** building with `starter:true`,
`initLogistics`. Failed placement ⇒ null (links involving null are skipped).

Order (anchor row,col):
1. workbench (55,36), paper_mill (55,42), kiln (55,48), infusion_array (55,54).
2. storehouses locked: jade_shard (78,27), bamboo (26,44), stone (83,27).
3. gathering stones: gsStone (80,13), gsWood (16,44), gsClay (80,80), gsFox (12,80).
4. seals (locked): stone (74,22), wood (30,44).
5. Lantern Q (76,20): gsStone→sealStone, sealStone→array, gsStone→shJade, gsStone→shStone.
6. Lantern T (22,44): gsWood→sealWood, sealWood→bench, sealWood→mill, sealWood→kiln, gsWood→shBamboo, shBamboo→mill.
7. Lantern M (52,58): gsClay→kiln, gsFox→array.
8. gsOut (61,43), gsSpirit (61,55), storehouses locked plank (63,36), brick (63,45), spirit_stone (63,57); Lantern S (62,51): gsOut→shPlank, gsOut→shBrick, gsSpirit→shSpirit.
9. Seeds (untagged ground drops at cell centres): stone 8 @(80,14), jade_shard 2 @(79,15), wood 8 @(15,45), bamboo 2 @(16,46), clay 6 @(80,80), spirit_essence 4 @(12,82).
10. `starterPlaced = true`. Links added here do **not** increment `stats.linksAdded`.

---

## 17. Hooks / events the engine exposes

| JS hook | Args | Fired by | C# event |
|---|---|---|---|
| `window.onGroundDrop` | `(areaKey, item, qty, x, y)` | every `dropGround` (nulled during replay) | `GroundDropped` (floating "+N") |
| `window.onSfx` | `(name, areaKey?)` | names: `harvest` (player swing), `hit`, `kill`, `craft`, `build`, `upgrade`, `unlock`, `dragon`, `ascend` (nulled during replay) | `SoundRequested` |
| `ascend()` → `location.reload()` | — | ascension | `RunReset` (view rebuilds) |
| return values | `dropFromHand` → `{fed|reordered|used|once|lured|configured|deposited|dropped|building}` / null; `unlockArea` → true/{paid}/false; `claimQuest` → `{id,toHand,dropped,at}`; `gameTick` → changed; `automationTick` → swings | — | return DTOs + optional events |

State the view polls (no hook): `dragon.msg/msgUntil/dialog`, `ascendPrompt`,
`justAscended`, `buff`, `combatBuff`, `node.hitAt/autoFlash`, `enemy.hitAt`,
`wispPos`, `buildingStatus`, `craftRate`, link `_stat`, `area.autoPaused`.
Recommend turning these into explicit events in C# (below).

---

## 18. Suggested C# architecture

### 18.1 Assemblies

- **`IdleGrounds.Sim`** (`.asmdef`, `noEngineReferences: true`) — pure C#: state, config, systems, save. No `UnityEngine`. Tested by EditMode tests (`IdleGrounds.Sim.Tests`).
- **`IdleGrounds.Data`** — config loaded from ScriptableObjects or JSON *into plain C# records* (`GameConfig`) consumed by Sim. (ScriptableObject authoring lives in a Unity assembly; Sim only sees the POCO.)
- **`IdleGrounds.View`** — MonoBehaviours: renderers, input (the hold/latch/ramp state machines of §5.6-5.7), UI.

### 18.2 Core types

```csharp
public interface IClock { long NowMs { get; } }          // real, or virtual during offline replay
public interface IRng   { int Range(int a, int bInclusive); double Next01(); }

public sealed class GameState { Hand Hand; Dictionary<AreaId, AreaState> Areas; World World; Dragon Dragon; ... }
public sealed class AreaState { List<Node> Nodes; List<GroundItem> Ground; List<Building> Buildings; ... }
// Building: one class with nullable component blocks (Converter?, Gatherer?, Lantern?, Roster?, Store?, Gate?, GenBuilding?)
// mirrors the JS "config flags" model and keeps saves simple.

public sealed class Simulation {
    public Simulation(GameConfig cfg, GameState s, IClock clock, IRng rng);
    public SimEvents Events { get; }
    public bool Tick();               // == gameTick
    public int  AutomationTick();
    // commands (player intents) — each returns a result DTO:
    public DropResult  RightClick(AreaId a, Vec2 px, bool noGround);
    public bool        Harvest(AreaId a, int nodeId, bool isAuto, bool held);
    public bool        Attack(AreaId a, int enemyId);
    public SuctionResult Suction(AreaId a, Vec2 px, float radius, ItemId? filter);
    public int         Withdraw(AreaId a, int buildingId, int n);
    public Building?   PlaceGhost(...); public bool Demolish(...); public bool SetRecipe(...);
    public bool AddLink(...); public bool RemoveLink(...); public bool SelectUpgrade(...);
    public UnlockResult UnlockArea(AreaId a); public QuestClaim? ClaimQuest(); public bool BuyPerk(PerkId p);
    public void Ascend(IReadOnlyList<VowId> nextVows);
}
```

### 18.3 Systems (one class each, called by `Simulation.Tick` in the §2.3 order)

| System | Responsibilities | JS origin |
|---|---|---|
| `Timing` | `periodic`, `tickGap`, `PrestigeFactor`, TEST scaling | 2302-2320, 274 |
| `OccupancyGrid` | ref-counted cell grid; racks; placement checks + reasons | 378-439, 1170-1256 |
| `NodeSystem` | spawn, fixtures, deco rings, harvest, deplete/respawn queue, surface dives | 443-706, 1003-1103 |
| `FieldGeneratorSystem` | area generators | 2352-2391 |
| `GroundSystem` | dropGround, eviction classes, grace, settle, colliders | 708-1001 |
| `HandSystem` | stacks, rotate, feed rules, `RightClick` dispatcher | 16-127, 1855-2068 |
| `ConverterSystem` + `FuelSystem` | batches, stock, recipe switch, fuel FIFO, stoker | 1305-1415, 2414-2468, 2559-2575 |
| `GenBuildingSystem`, `PavilionSystem` | gen buildings, disciples | 2393-2412, 2576-2592 |
| `LogisticsSystem` | stones, endpoints, links, lantern beats, wisps | 1417-1791, 2470-2618 |
| `CombatSystem` | foxes, boar, attack, loot, Martial Vigor | 2620-2653, 2687-2774 |
| `DragonSystem`, `UpgradeSystem` | tributes, pills/buffs, scales, Altar jobs | 1932-1957, 2070-2207, 2655-2673 |
| `AutomationSystem` | bots | 2776-2837 |
| `ProgressionSystem` | reveal, quests, region unlocks | 1105-1158, 2209-2298, 2720-2756 |
| `PrestigeSystem` | AP, ascend, perks, vows, gate | 141-335 |
| `OfflineReplay` | begin/step/finish with a `VirtualClock`; summary/stalls | 2839-3155 |
| `SaveService` | JSON (de)serialise, transient exclusion, sanitising migrations | state.js |

### 18.4 Events (`SimEvents`) the view subscribes to

`GroundDropped(area,item,qty,pos)` · `SoundRequested(name,area)` ·
`NodeSpawned/NodeDepleted/NodeHit(area,node,isAuto)` ·
`BuildingPlaced/BuildingCompleted/BuildingDemolished` · `BatchStarted/BatchFinished(area,building,item,qty)` ·
`WispLaunched/WispArrived/WispReturned/WispDropped` · `EnemySpawned/EnemyHit/EnemyKilled` ·
`DragonStageAdvanced(stage,text)` · `DragonAwakened` · `BlessingStarted(kind,until)` · `CombatBuffStarted(until)` ·
`UpgradeApplied(area,type)` · `RegionUnlocked(area)` · `QuestClaimed` · `AscendPromptRequested` · `RunReset` ·
`OfflineProgress(fraction)` / `OfflineFinished(summary)`.

Rules: events are **suppressed (or buffered and dropped) during offline replay**,
exactly as the JS nulls `onGroundDrop/onSfx`. Presentation fields (`hitAt`,
`autoFlash`, settle/push-out) may move to the view, but `pushOutOfColliders`
changes item positions that stones then vacuum — keep it in Sim if parity of
logistics timing matters (recommended; skip it in replay as JS does).

### 18.5 MonoBehaviour layer

`GameRunner` (owns `Simulation`, `FixedUpdate`-style accumulator: `Tick()` every
50 ms, `AutomationTick()` every 1000 ms, autosave 5 s + `OnApplicationPause/Quit`),
`RegionView` per area (pooled sprites for nodes/ground/enemies/wisps, reads
state each frame; wisp positions via `WispPos(now)`), `HandController`
(left/right hold state machines with the exact rates of §5.6-5.7, frame-rate
independent), `BuildingPanelViews` (recipe/link/roster), `OfflineModal`.

### 18.6 Testing hooks

Inject `IClock` + seeded `IRng`; EditMode tests drive `Tick()` with a manual
clock. Golden tests worth writing first: `periodic` catch-up equivalence (50 ms
vs 640 ms ticks yield equal counts), converter catch-up with burner fuel, lantern
least-recently-served fairness (shared seal case), eviction ordering, offline
plateau stop, load-migration idempotence.
