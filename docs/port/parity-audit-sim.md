# Simulation parity audit — C# `Assets/_Project/Scripts/Sim` vs `old-game/js`

Date: 2026-10-04. Read-only adversarial comparison of the pure-C# simulation
against `old-game/js/engine.js`, `state.js`, `data.js` (+ `main.js` boot flow).
Every finding below was confirmed by reading both sides; candidate differences
that turned out equivalent or unreachable were dropped (see "Checked and
equivalent" at the end).

**Result: no high or medium findings.** The port is line-for-line faithful in
every economy path. 0 high · 0 medium · 5 low · 2 informational.

---

## Findings

### L1 — `dragonScaleAt` survives save/load in C#; the JS resets it on every load (low)

- **JS:** `state.js:138-246 loadState` rebuilds the state from
  `makeInitialState()` and copies an explicit field list. `dragonScaleAt` is
  not in that list, so it is `undefined` after every load. `engine.js:2659`
  (`if (!window.GS.dragonScaleAt) GS.dragonScaleAt = now + interval`) then
  restarts the shed clock one full interval after boot (or after the start of
  the offline replay).
- **C#:** `GameState.cs:376` is a persisted public field, written by
  `SaveCodec.Serialize` (reflection) and kept by `SaveCodec.cs:358`.
  `DragonSystem.TickScales` (`DragonSystem.cs:163-169`) continues the old
  schedule.
- **Difference:** after a load, an awakened dragon's next scale arrives up to
  one interval (45 s × timeScale × shrine × prestige) earlier in C# than in
  the JS. At most +1 scale per load, so not economy-breaking. Note that
  `docs/port/engine-systems.md:108` says "saved". That matches the JS save
  file but not the JS load behaviour.
- **Fix:** if you want strict parity, add `s.dragonScaleAt = 0;` in
  `SaveCodec.Sanitize`. Otherwise keep it (the continuous clock is arguably
  better) and correct the spec line to call it an intentional deviation.

### L2 — Toast-tier boot loses the welcome summary when the replay throws (low)

- **JS:** `main.js:83-88` wraps `stepOfflineCatchup` and
  `finishOfflineCatchup` in separate try/catch blocks. A throwing replay still
  produces a summary with `failed: true`, which `showOfflineSummary` displays.
- **C#:** `Simulation.Boot` (`Simulation.cs:75-80`) uses
  `try { Offline.Step(job); } finally { Offline.Finish(job); }`. The exception
  propagates, and `GameRunner.Awake` (`GameRunner.cs:67-68`) catches it and
  sets `tier = None`. `job.summary` is computed but discarded, so the player
  gets no "catch-up failed" toast.
- **Fix:** in `Boot`, catch the exception from `Step` (log it; the job is
  already `failed`), then still return
  `new OfflineBoot { tier = Toast, summary = Offline.Finish(job) }`.

### L3 — Enemy wander is scaled by tick gap in C#; the JS uses a fixed per-tick step (low, cosmetic)

- **JS:** `engine.js:2647-2652` moves `speed/20` px per gameTick, retargets
  with probability 0.01 per tick, and also moves on the first tick.
- **C#:** `CombatSystem.cs:78-92` moves `speed·dt/1000` and retargets with
  probability `1-0.99^(dt/50)`. It skips movement when `TickGap == 0` (the
  first tick after boot, `ReplaceState`, or the replay start).
- **Difference:** the two are identical at 50 ms live ticks. In the offline
  replay (≥250 ms steps), C# foxes cover their real distance while JS foxes
  crawl at about 1/5 speed. Only positions are affected; spawns, loot and
  respawn clocks are identical.
- **Fix:** none needed. Optionally add a comment documenting it as an
  intentional frame-rate-independence deviation.

### L4 — Deco rings persist in C# saves; the JS regenerates them on each load (low, cosmetic)

- **JS:** `state.js:273` drops every `deco` node on load. `initArea`
  (`engine.js:577-582`) then rebuilds the corner rings with fresh random
  `decoScale`/`decoDx`/`decoDy` jitter.
- **C#:** `SaveCodec.cs:442-445` keeps `kind=="deco"` nodes in the center, so
  `InitArea` (`NodeSystem.cs:74`) never regenerates them.
- **Difference:** the jitter of the border trees is stable across sessions
  instead of re-rolled. The rings also won't follow a future change to the
  corner zones or `placeDecoRing` without a regrid. Visual only.
- **Fix:** none required. If ring layout changes later, drop deco nodes in
  `SanitizeArea` as the JS does. The trade-off is that save → load → save is
  no longer byte-identical.

### L5 — Web (localStorage) saves cannot be imported (low / by design)

- **JS migrations not ported:**
  - `state.js:209-220` legacy quest-chain remap (`remapLegacyQuestIdx`, V50/V51 id lists).
  - `state.js:228-236`, `444-481` builtTypes inference via `inferStarterTags` / `LEGACY_STARTER_SPOTS`.
  - `state.js:240` buildSeen default "all seen".
  - `state.js:243-245` pavilionSeeded inference.
  - `state.js:261-264` scalar `enemyRespawnAt` → array.
  - `state.js:368-376` converter `smeltPaid`/`queue` → stock.
  - `state.js:382-399` scalar `fuel` + `stock.wood` → `fuelQ`.
  - `state.js:345-352` count-only gate offerings. This one IS ported (`SaveCodec.cs:527-538`).
- **C#:** `SaveCodec.cs:22-26` documents the gap. `TryDeserialize` also
  rejects any save without an `areas` array, and a web save has an `areas`
  object.
- **Impact:** a player can't carry a browser save into the Unity build. This
  only matters if web-save import is wanted.
- **Fix:** if wanted, write a one-time `WebSaveImporter` that maps the JS
  object shape (`areas` object keyed by region, `world.unlocked` object,
  `perks` / `builtTypes` objects) onto `GameState` and applies the migrations
  above. Then run the normal `Sanitize`.

### I1 — engine.js functions with no C# counterpart (informational)

All of these are dead code, trivial, or inlined. None is a gameplay gap.

| JS (engine.js) | Status |
|---|---|
| `effectiveTimer` :339 | Not ported. Unused by `ui.js`/`main.js` (dead in JS too); tier timers live in `NodeSystem.RegrowDelayMs`. |
| `buildingFootprint` :1160 | Not ported. Unused (dead in JS). |
| `noBuildRects` :363 | No sim method. `World.InNoBuild` covers the placement rule; `Game/ZoneMarker.cs` draws noBuild zones from config. |
| `rollTier` :358 | Inlined as `tier = 1` (`NodeSystem.cs:180`). |
| `occCells`, `occFresh` :394-399 | Folded into `OccupancyGrid.Mark` / `Fresh`. |
| `autoSkipList` :2933, `heldTotal` :2883, `cfgFixtures` :2785 | Inlined / private helpers (`OfflineReplay.HeldTotal`, `AutomationSystem.IsAutoTapFixture`). |
| `pileStatus` :1631 | `BuildingStatusInfo.Pile`. |
| `itemName` / `itemIcon` :11-14 | Config lookups (`Cfg.Item(k).name` / `GameDatabase.ItemIcon`). |
| `saveDisabled` / `clearSave` (state.js:113, 483) | Belongs to the Game layer (`SaveService`), not the sim. |

### I2 — A known JS quirk ported deliberately (informational)

`endpointGive` (`engine.js:1707`) sends ANY fuel item that a wisp delivers to
a burner's rack, even when it is an input of the current recipe. For example,
firestone sent to a Pill Furnace on the Ember Pill recipe fills the rack, or
is refused when the rack is full. It never reaches stock. `endpointAccepts`
and the hand path route it to stock instead. C# `BuildingSystem.cs:441-447`
reproduces this on purpose. **Not a port difference.** It is a design-review
item: a wisp-fed Ember/Star Steel line can't get firestone into stock. To
change it, make both sides use
`if (cfg.fuel && FUEL[item] && !isInput) return addFuelItem(...)`.

---

## Checked and equivalent (no finding)

System by system: each JS path was read next to its C# counterpart. Constants,
branch order and scaling factors match unless noted above.

- **Nodes / harvest:**
  - `initArea`: Altar at (44,44), dragon in cornerTL, fixtures, deco, spawner fill and trim, `areaScale` = 15.
  - `spawnFromSpawner`: 40 attempts, spacing, surfaceUntil.
  - Quarry, chop, break and instant/surface branches, rare drops, Stoneheart ×2.
  - `flashMs`, grace refresh, `manualSrc`.
  - `depleteNode` delay: `regrow·0.8^speed·timeScale·verdant·prestige·0.9^regrow`.
  - Surface dives and the 500 ms respawn retry.
- **Ground:**
  - Eviction class map, protected share 480, hard cap 900, freshFrom.
  - Grace stamps (4 s / 8 s).
  - Gen tagging only under automation.
  - Suction (pull 1+3·(1-d/r), collect 12 px), pickupNear.
  - Settle (18 px, 64 px buckets), push-out (8 px exits, pull 200 ms / 5 s rules).
- **Hand / dispatcher:**
  - Cap with burden, add/take/rotate/front.
  - Storehouse deposit/withdraw with lock, feedNeeds, feedRatio.
  - The full `dropFromHand` order: Vitality, bait lure, Altar, Dragon, stoker, roster, converter (fuel-vs-ingredient rule), seal tune, stone, storehouse, gate (falls through), ghost, ground.
- **Buildings / placement:**
  - `canPlaceBuilding`/`placeReason`: edge, water-only, unique gate, noBuild/anyZone, occupancy including built racks, rack fit, ghost racks.
  - Ghost completion: epoch, stats, builtTypes, first-pavilion buns, gate prompt.
  - Demolish refunds, including the running batch, disciples→robes and offerings; fuel not refunded.
  - Starter network: coords, locks, links, seeds.
  - Reveal rules: stageUnlock, vet skip, builtTypes.
  - New badges.
- **Converters / fuel:**
  - Recipe switch refund; stock cap 20.
  - Batch loop with due-time catch-up, max 400 events.
  - Pile back-pressure (12 items / 3 cells / 500 ms).
  - Ember pill ×0.5, Ember Heart 0.85^lvl, Cold Hearth ×2.
  - Burn-from-back with the 0.5 pop rule; start on any fuel; `fuelBurnAt` bookkeeping.
  - Status labels and `craftRate`.
- **Logistics:**
  - targetTypes, stoneAccepts, ejectUnwanted (`_accEver`), in-flight reservations, accept/give/take.
  - Stone vacuum (22 px, pull ×min(13, gap/50)).
  - Lantern least-recently-served pick with rotation tie-break, catch-up beats with back-dated `t0`.
  - Beat: `rate·timeScale·0.85^haste·wind·prestige·0.9^gale`. Speed: `·(1+0.25·haste)/wind`.
  - 250 ms idle retry.
  - Wisp arrive/return/drop (+24 px); link refusal; add/remove link.
  - Stoker best-fuel-first, radius + 1.5 cells.
- **Combat:**
  - Per-slot respawn clocks, cap + Spirit Call, boss excluded from the cap.
  - Damage: 1 + blade + vigor + fury. Splash: aoe·1.5 cells.
  - Loot ×lootMult, boss table, respawn `respawnMs·timeScale·prestige`, foxKills.
- **Automation:**
  - Per-type saturation at 120 loose class-0/1 items, budget `AUTOMATION_CLICKS[lvl]` + autoboost.
  - autoTap fixtures get `level` swings outside the budget; bot drops untagged → gen.
- **Dragon:**
  - `tributeMult` (floor 0.4), Restless ×2, `dragonTribute` display quirk.
  - Pills: `(60 s + 30 s·affinity + 60 s shrine)·1.2^bless·buffScale`.
  - Stage advance texts and awakening; scales: 45 s·timeScale·(shrine 0.5)·prestige, pile cap 5 within 6 cells.
- **Upgrades:**
  - Level/max table, scaled costs, job select/refund/feed/apply (hand → +5 cap), tree BFS visibility.
- **Quests / milestone:**
  - All 13 goal lambdas map to `QuestGoalKind`, including explore any-of, weaver capped sum and iron tribute.
  - Claim overflow below the Altar (+14 px).
- **Region unlocks:**
  - `scaled`·0.8^frugal cost, installments, `openRegion` surface re-roll, unlock sfx.
  - Frugal reconcile; Remembered Paths refund + open; Legacy Automation.
- **Pavilion:**
  - Roster cap: base + 2·discipleCap + hall.
  - Recruit with robe; food values and cap.
  - Cycle: `produceMs·timeScale·prestige` with catch-up and pile back-pressure.
- **Prestige / perks / vows:**
  - All 15 perks and 4 vows wired at the same single points.
  - `prestigeFactor` (additive 0.2/asc, haste, dragon blessing 0.9, 0.96/mark), `ascendReward` rounding.
  - `ascend()` carry-over list; hands re-applied, quest skip, solitude, paths/legacy.
- **Offline replay:**
  - 90 s / 10 min tiers; cap (8 + 2·slumber) h; step `max(250, ceil(elapsed/45000))`.
  - Automation every 1000 sim-ms; per-minute samples and 15 min plateau signature.
  - Resume stamp via `offlineAwayFrom`; skip/levelled.
  - Summary gained/plateau/stalls: the nofuel rule mirrors the batch start.
  - Physics skipped, then settle passes; virtual clock drives every timer and buff.
- **Save / load:**
  - Every JS-persisted field has a persisted C# field.
  - Transients excluded: `manualAt`, `_src`, pull, `_stat`, `_seq`, `_crafts`, pile, `_accEver`, `autoSkip`, `autoPaused`.
  - Sanitisation rules match `state.js` for C# saves, apart from L1, L4 and L5.
- **Data:** the `GameDatabase.asset` balance/test/grid/vitality/gate blocks and spot-checked building/region assets match `data.js` (TEST on, timeScale 0.2, costScale 0.5).
