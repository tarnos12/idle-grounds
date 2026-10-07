# Balance pass 1 — real balance (ADR 0004)

Date: 2026-10-07. Targets: `docs/adr/0004-balance-targets.md`. Gauge: the playthrough bot
(`Assets/_Project/Scripts/Tests/Sim/PlaythroughBot.cs`, `PlaythroughBotTests.cs`).

## Result

| Profile | Mode | Before (first ascension) | After |
|---|---|---|---|
| human | real balance | 22:14 | **2:09:40** (seeds 777/1/2: 2:09–2:11) |
| expert | real balance | 15:25 | **1:35:39** (seed 12345) |
| expert | TEST (dev toggle) | 6:01 | ≈30 min |
| human, 3 runs (seed 4242) | real balance | — | 2:12 / 1:53 / run 3 with all four vows **2:48** (a no-vow run 3 ≈ 1:54, so vows ≈ +47 %) |
| expert, 3 runs | TEST | 5:57 / 4:24 / 5:04 (vows) | 29:46 / 25:28 / 31:20 (vows; ≈ +25 %) |

- **TEST mode is off by default** (`TEST.ENABLED: false`, `TestScaling.enabled = false`). Dev toggle: Editor menu
  **Idle Grounds → Dev → TEST Mode → Force On / Force Off / From Data** (PlayerPrefs `IdleGrounds.Dev.TestMode`,
  read by `GameRunner.Awake`, applies the next time Play starts).
- **Hands first, machines win.** Human run, items produced per 15 min (FlowLedger "produced" deltas; *hand* = made
  during the bot's own swings/attacks, *auto* = raw items made by the world tick: fields, generator buildings,
  Automation, disciples, dragon scales; *crafted* = converter output, reported separately):

| Window | hand | auto | crafted | auto ÷ hand |
|---|---|---|---|---|
| 0:00–0:15 | 664 | 445 | 79 | 0.7 |
| 0:15–0:30 | 675 | 513 | 78 | 0.8 |
| 0:30–0:45 | 565 | 2109 | 213 | 3.7 |
| 0:45–1:00 | 543 | 1988 | 141 | 3.7 |
| 1:00–1:15 | 83 | 664 | 65 | 8.0 |
| 1:15–1:30 | 6 | 1110 | 96 | 185.0 |
| 1:30–1:45 | 108 | 958 | 159 | 8.9 |
| 1:45–2:00 | 0 | 805 | 223 | ∞ |
| 2:00–2:15 | 33 | 314 | 138 | 9.5 |

Human timeline (real balance, seed 777):

```
    0:00:30  dragon stage 1
    0:03:58  island unlocked: mine
    0:07:33  dragon stage 2
    0:10:36  quest claimed: iron
    0:12:51  island unlocked: fishing
    0:15:38  dragon stage 3
    0:16:58  island unlocked: farm
    0:20:44  quest claimed: cultivate
    0:29:35  altar upgrade: center/automation  1
    0:36:55  altar upgrade: mine/automation  1
    0:38:10  first herb_garden built (center)
    0:41:38  spirit bridge fishingcenter paired
    0:44:03  altar upgrade: mine/automation  2
    0:47:00  altar upgrade: center/automation  2
    0:51:37  altar upgrade: farm/automation  1
    0:55:09  island unlocked: grove
    1:17:28  dragon stage 4 - AWAKENED
    1:21:53  altar upgrade: farm/automation  2
    1:28:28  island unlocked: volcano
    1:36:20  island unlocked: celestial
    1:41:10  dragon blessing: ember_pill
    1:44:04  altar upgrade: mine/automation  3
    2:08:07  first ascension_gate built (center)
    2:09:40  ASCENDED (+21 AP, total 21)
    2:09:40  island unlocked: mine
    phase dragon stage 4 0:24:22
```

- First 20 % of the run: auto ÷ hand = 0.72 (human) / 0.52 (expert): the first ~30 min are hand work. Mid-run
  (40–80 %): **7.8×** (human; 8.0 on seeds 1/2), 16.6× (expert — it clicks less). Last 20 %: ~30× (the gate chain
  is converter work). A first tuning had mid-run at ≈ 20×; it was brought into 5–10× by giving the mid-game more
  hand-only demand and fewer free machine items (Herb Garden slower, fewer Mine rocks, Grove unlock wants Algae,
  Celestial unlock wants Obsidian). Lowering AUTOMATION_CLICKS or `autoSkipLoose` was tried and barely moved it
  (Automation is node-regrow-bound, not click-bound), so they stay.
- **Spirit Bridge** Fishing→Center: 4.8 items/s (was ≈0.8/s) vs 1.7/s (hand cap 20) / 2.3/s (Hand Size 3) carried by
  hand at human pacing (`BridgeThroughputTests`).

Phase times, human (before → after): quest chain 5:23 → ~21:00, dragon stage 4 1:28 → 24:22, Volcano + Celestial
2:07 → 13:33, pill 2:20 → 4:49, gate 5:19 → 24:03, plus the new investment rounds (≈32 min of Altar jobs / generator
buildings / disciples).

## Mechanic (code) changes

| Where | Old | New | Why |
|---|---|---|---|
| `Timing.PrestigeFactor` | 1/(1+0.2·asc) · 0.95^haste · (won ∨ dragonBlessed ? 0.9) · 0.96^vowMarks | 1/(1+`ascensionSpeedPerRun`·asc) · `hasteStep`^haste · (won ? `awakenedSpeed`) · `vowMarkStep`^marks = 0 / 0.98 / 0.9 / 0.99 (`GameBalance`) | Later runs ≈ same length: no +20 % world speed per ascension; Haste and vow marks are small QoL; the awake dragon's −10 % lasts only the rest of that run. |
| `DragonSystem.TributeMult` | max(0.4, 1/(1+0.25·asc)) | max(`tributeShrinkFloor`, 1/(1+`tributeShrinkPerRun`·asc)), shrink 0 ⇒ 1 | Tributes no longer shrink each ascension (that also cancelled the Restless vow). |
| Vow of the Restless Dragon | tributes ×2 | ×`restlessTributeMult` 1.5 (ceil) | With 6× bigger tributes, ×2 made the all-vows run > 2× as long for 1.75× AP; ×1.5 still costs ~40 % time. |
| Spirit Bridge | 1 item per sky wisp | `bridge.carry` items per sky wisp (`SkyWisp.qty`, saved; old saves load as 1); reservation counts items; a beat takes min(carry, receiver room); a refused remainder flies home; ground drops keep the count | Bridges must beat hand carrying for bulk. |
| `TestScaling.enabled` default | true | false | ADR 0004. |
| `GameRunner`, `Game/Core/DevSettings.cs`, `Editor/DevMenu.cs` | — | TEST-mode override | Dev way to flip TEST. |

## Data changes (`Assets/_Project/Data/Source/game-data.json`, old → new)

game-data.json is now the source of truth for balance: `tools/data-export/export.js` would overwrite it from the
frozen `old-game/js/data.js` — do not re-run it.

| What | Old | New | Why |
|---|---|---|---|
| TEST.ENABLED | true | false | ADR 0004: real balance is the default; TEST stays a dev toggle (Idle Grounds/Dev/TEST Mode). |
| Spirit Bridge bridge | {cap 20, rateMs 1000, speed 170} | {cap 60, rateMs 1000, speed 340, carry 5} | Bridges must beat hand carrying: 5 items per sky wisp, 2× flight speed, 60-item buffer/reservation → ≈5 items/s Fishing→Center (was ≈0.8/s). |
| center clay field intervalMs | 1500 | 6000 | Hands early: the starter clay field out-produced clicking from minute 0 (was 1 per 1.5 s). |
| center clay field upgrade | — | quarry | Quarry Output now also speeds the clay field (a machine path for clay). |
| center stone field intervalMs | 1500 | 6000 | Hands early (was 1 per 1.5 s); Quarry Output (x0.8/level) speeds it back up. |
| center wood field intervalMs | 3000 | 8000 | Hands early (was 1 per 3 s). |
| center wood field upgrade | — | speed | Regrow Speed now also speeds the wood field. |
| farm sand field intervalMs | 1500 | 4000 | Hands early (was 1 per 1.5 s). |
| farm sand field upgrade | — | speed | Growth Speed now also speeds the sand field. |
| fishing water field intervalMs | 2000 | 4000 | Hands early (was 1 per 2 s). |
| fishing water field upgrade | — | speed | Bite Speed now also speeds the spring field. |
| mine ironvein target | 2 | 3 | Iron is the long pole of a run: 3 veins instead of 2. |
| Dragon stage 1 tribute | {leaves 15} | {leaves 50} | Run length x6-7 (ADR 0004). |
| Dragon stage 2 tribute | {stone 25, clay 10} | {stone 120, clay 60} | Run length; the Mine (hand) and the slowed starter fields supply it. |
| Dragon stage 3 tribute | {iron_bar 8, algae 15, water 10} | {iron_bar 24, algae 80, water 80} | Run length; the iron quest asks for these 24 bars. |
| Dragon stage 4 tribute | {spirit_herb 20, spirit_essence 15, iron_bar 5} | {spirit_herb 450, spirit_essence 180, iron_bar 70} | The awakening is the mid-run machine check: Herb Gardens, disciples and Mine Automation pay off here. |
| farm unlock cost | {wood 10} | {wood 50} | Run length x6-7; each Island is a real goal. |
| mine unlock cost | {wood 16} | {wood 80} | ″ |
| fishing unlock cost | {wood 20} | {wood 120} | ″ |
| volcano unlock cost | {iron_bar 3} | {iron_bar 60} | ″ |
| grove unlock cost | {wheat 12, wood 8} | {wood 150, algae 100} | Algae (Fishing, by hand) instead of rice: hand work stays relevant mid-run. |
| celestial unlock cost | {spirit_stone 6, jade 3, glass 3} | {spirit_stone 40, jade 20, obsidian 60} | Obsidian (Volcano, by hand) instead of glass: hand work mid-run. |
| Workbench cost | {wood 8} | {wood 20} | Run length x~3 (buildings are a smaller share than tributes). |
| Kiln cost | {wood 10, clay 5} | {wood 25, clay 20} | ″ |
| Paper Mill cost | {wood 10, stone 5} | {wood 25, stone 20} | ″ |
| Infusion Array cost | {stone 10, spirit_essence 5} | {stone 30, spirit_essence 8} | ″ |
| Loom cost | {wood 10, plank 4} | {wood 30, plank 10} | ″ |
| Mill cost | {wood 8, stone 6} | {wood 25, stone 20} | ″ |
| Brewery cost | {wood 8, clay 6} | {wood 25, clay 20} | ″ |
| Cauldron cost | {stone 8, iron_bar 2} | {stone 30, iron_bar 6} | ″ |
| Jade Carver cost | {wood 6, stone 8} | {wood 20, stone 30} | ″ |
| Pill Furnace cost | {brick 6, iron_bar 4, tools 2} | {brick 40, iron_bar 20, tools 6} | ″ |
| Star Anvil cost | {iron_bar 6, tools 3, glass 2} | {iron_bar 60, tools 16, glass 20} | ″ |
| Talisman Atelier cost | {plank 6, jade 2, glass 2} | {plank 60, jade 16, glass 16} | ″ |
| Dragon Shrine cost | {brick 10, cloth 8, obsidian 4} | {brick 30, cloth 20, obsidian 15} | ″ |
| Ascension Gate cost | {talisman 3, star_steel 3, dragon_scale 3} | {talisman 14, star_steel 20, dragon_scale 6} | The gate is the finale of a run: more talismans / star steel / scales. |
| Charcoal Pit cost | {stone 6, clay 4} | {stone 20, clay 15} | Run length x~3 (buildings are a smaller share than tributes). |
| Furnace Spirit cost | {stone 4, spirit_essence 2} | {stone 10, spirit_essence 4} | ″ |
| Meditation Pavilion cost | {plank 6, cloth 4, rope 2} | {plank 16, cloth 8, rope 4} | ″ |
| Forge cost | {wood 5, stone 10} | {wood 20, stone 40} | ″ |
| Storehouse cost | {wood 12} | {wood 30} | ″ |
| Algae Farm cost | {wood 12, algae 6} | {wood 40, algae 20} | ″ |
| Herb Garden cost | {wood 10, water 5, clay 5} | {wood 40, water 20, clay 20} | ″ |
| Gathering Stone cost | {stone 5} | {stone 12} | ″ |
| Wisp Lantern cost | {wood 5, stone 5} | {wood 12, stone 12} | ″ |
| Warding Seal cost | {wood 3, stone 3} | {wood 8, stone 8} | ″ |
| Spirit Bridge cost | {wood 10, stone 10} | {wood 40, stone 40} | ″ |
| Altar `hand` (Hand Size) L1 / L2 / L3 | {wood 10} / {wood 25, leaves 10} / {cotton 15, wood 40} | {wood 30} / {wood 80, leaves 30} / {cotton 40, wood 120} | Run length x~3. |
| Altar `wisps` (Wisp Haste) L1 / L2 / L3 | {wood 40, spirit_stone 1} / {spirit_stone 3, plank 8} / {spirit_stone 6, brick 8} | {wood 120, spirit_stone 3} / {spirit_stone 8, plank 24} / {spirit_stone 16, brick 24} | ″ |
| Altar `affinity` (Dragon Affinity) L1 / L2 / L3 | {qi_elixir 2, wood 40} / {spirit_stone 3, qi_elixir 3} / {spirit_jade 1, qi_elixir 5} | {qi_elixir 4, wood 120} / {spirit_stone 8, qi_elixir 8} / {spirit_jade 3, qi_elixir 12} | ″ |
| Altar `disciples` (Disciple Mastery) L1 / L2 / L3 | {robe 2, spirit_stone 2} / {spirit_stone 4, spirit_buns 20} / {spirit_jade 2, robe 5} | {robe 4, spirit_stone 6} / {spirit_stone 12, spirit_buns 40} / {spirit_jade 5, robe 10} | ″ |
| Altar `spd_c` (Regrow Speed) L1 / L2 / L3 | {wood 20} / {wood 50, leaves 15} / {wood 120, spirit_essence 10} | {wood 60} / {wood 150, leaves 45} / {wood 300, spirit_essence 25} | ″ |
| Altar `auto_c` (Automation) L1 / L2 / L3 | {wood 60, stone 30} / {stone 120, clay 40} / {iron_ore 40, spirit_essence 20} | {wood 150, stone 80} / {stone 300, clay 120} / {iron_ore 100, spirit_essence 50} | Automation is the mid-run payoff; priced to repay in ~5 min. |
| Altar `act_fi` (Reel Speed) L1 / L2 / L3 | {fish 10, algae 15} / {algae 40, wood 30} / {fish 40, iron_ore 15} | {fish 30, algae 40} / {algae 100, wood 90} / {fish 100, iron_ore 40} | Run length x~3. |
| Altar `act_c` (Action Speed) L1 / L2 / L3 | {wood 15, stone 5} / {stone 40, leaves 20} / {stone 80, spirit_essence 15} | {wood 45, stone 15} / {stone 120, leaves 60} / {stone 240, spirit_essence 40} | ″ |
| Altar `quarry` (Quarry Output) L1 / L2 / L3 | {wood 20, stone 10} / {stone 50, clay 20} / {stone 100, water 25} | {wood 60, stone 30} / {stone 150, clay 60} / {stone 300, water 75} | ″ |
| Altar `act_m` (Mine Speed) L1 / L2 / L3 | {stone 30} / {stone 60, clay 25} / {iron_ore 30, clay 50} | {stone 90} / {stone 180, clay 75} / {iron_ore 90, clay 150} | ″ |
| Altar `spd_f` (Growth Speed) L1 / L2 / L3 | {wheat 15, wood 20} / {wheat 40, water 15} / {wheat 80, cotton 20} | {wheat 45, wood 60} / {wheat 120, water 45} / {wheat 240, cotton 60} | ″ |
| Altar `act_f` (Harvest Speed) L1 / L2 / L3 | {wheat 25} / {wheat 50, sand 15} / {cotton 25, water 20} | {wheat 75} / {wheat 150, sand 45} / {cotton 75, water 60} | ″ |
| Altar `auto_f` (Farm Automation) L1 / L2 / L3 | {wheat 60, wood 40} / {wheat 120, cotton 30} / {cotton 60, water 40, iron_ore 20} | {wheat 150, wood 120} / {wheat 300, cotton 80} / {cotton 150, water 100, iron_ore 50} | Automation is the mid-run payoff. |
| Altar `spd_m` (Respawn Speed) L1 / L2 / L3 | {stone 25, wood 20} / {stone 60, clay 30} / {iron_ore 25, water 20} | {stone 75, wood 60} / {stone 180, clay 90} / {iron_ore 75, water 60} | Run length x~3. |
| Altar `spd_fi` (Bite Speed) L1 / L2 / L3 | {fish 10, wood 20} / {algae 30, clay 20} / {fish 30, water 30} | {fish 30, wood 60} / {algae 90, clay 60} / {fish 90, water 90} | ″ |
| Altar `auto_m` (Mine Automation) L1 / L2 / L3 | {stone 80, clay 30} / {iron_ore 30, stone 100} / {iron_ore 60, water 30, algae 30} | {stone 200, clay 80} / {iron_ore 80, stone 300} / {iron_ore 150, water 80, algae 80} | Automation is the mid-run payoff. |
| Altar `foe_cap` (Spirit Call) L1 / L2 / L3 | {leaves 25, wood 15} / {spirit_essence 10, wood 40} / {spirit_essence 25, iron_bar 5} | {leaves 75, wood 45} / {spirit_essence 30, wood 120} / {spirit_essence 75, iron_bar 15} | Run length x~3. |
| Altar `foe_dmg` (Spirit Blade) L1 / L2 / L3 | {spirit_essence 5, stone 20} / {spirit_essence 15, iron_ore 10} / {spirit_essence 30, iron_bar 8} | {spirit_essence 15, stone 60} / {spirit_essence 45, iron_ore 30} / {spirit_essence 90, iron_bar 24} | ″ |
| Altar `foe_aoe` (Spirit Wave) L1 / L2 / L3 | {spirit_essence 12, water 10} / {spirit_essence 25, iron_bar 5} / {spirit_essence 50, iron_bar 12} | {spirit_essence 36, water 30} / {spirit_essence 75, iron_bar 15} / {spirit_essence 150, iron_bar 36} | ″ |
| FUEL | {wood 10000, bamboo 6000, charcoal 40000, firestone 120000} | {wood 5000, bamboo 3000, charcoal 20000, firestone 60000} | Fuel was so plentiful that Vow of the Cold Hearth cost nothing; half the burn time per unit. |
| Perk haste desc | -5% to every timer in the world (regrow, batches, wisp beats, fields, foxes, your swings). Stacks with the +20% world speed per ascension. | -2% to every timer in the world (regrow, batches, wisp beats, fields, foxes, your swings). | No world speed per ascension any more (ADR 0004); Haste is now a small QoL perk. |
| Meditation Pavilion roster.foodCap | 20 | 60 | Disciples ate one bun per Essence (4 rice + 1 water each), so the machine never paid; bigger larder. |
| Meditation Pavilion roster.foodValues | {spirit_buns 1, spirit_wine 3} | {spirit_buns 3, spirit_wine 8} | One Spirit Wine (2 rice, 2 water, 1 leaf) now feeds 8 cultivation cycles (was 3); buns 3 (was 1). |
| Vow restless desc | Every dragon tribute is doubled. | Every dragon tribute is raised by half. | Vow of the Restless Dragon is now x1.5 (balance.restlessTributeMult), was x2. |
| Herb Garden gen.intervalMs | 2500 | 6000 | Machines overshot: a garden supplements the Grove instead of replacing it (was 2500). |
| mine ore target | 10 | 7 | Machines overshot: fewer stone/clay rocks for Mine Automation to sweep (was 10). |

Unchanged on purpose: recipe inputs and times (the "max 3 resource types per cost/recipe" rule holds — every changed
cost has ≤ 3 types), AUTOMATION_CLICKS 2/6/20, hand cap, VOW_MULT, the AP reward formula and perk costs (21 AP per run
now buys the 234-AP shop in ~11 runs ≈ 20+ h instead of ~7 runs ≈ 2.5 h — runs got longer, so prices did not need to).

## Bot changes (test code)

- **Investment planner** (`Invest(round)`, `Plan`): at phase boundaries the bot buys what an idle-hybrid player
  would: Regrow Speed → Automation (Center); Respawn / Bite Speed → Mine Automation; Herb Gardens ×2–3; Mine + Center
  Automation L2; Farm Growth / Harvest → Farm Automation L1–2; an Algae Farm; a full pavilion (3 disciples);
  Action Speed → Quarry Output; Mine Respawn / Automation L3. Nodes that are not selectable and buildings not yet
  taught are skipped.
- **Disciples are the Essence machine**: with ≥ 2 disciples the bot keeps the pavilion fed with Spirit Wine
  (Brewery) and collects the Essence instead of hunting foxes.
- **Veteran runs** wake the dragon to stage 3 before investing (they skip the quest chain that used to do it).
- The iron quest feeds bars to the dragon as it goes when the tribute exceeds the hand; ghost placement retries when
  something moved into the spot; sink timeouts 45 → 120 min.
- **Production-share instrumentation**: per-5-min windows of hand / auto / crafted items from FlowLedger deltas
  (`Share`, `AutoRatio`, `AutoByItem`, `HandByItem`, `Investments`), printed by every PlaythroughBot test.

## Tests

- `PlaythroughBotTests`: TEST smoke (< 1 h sim); **real human 2–3 h** + hand-dominated first 20 min + mid-run
  auto in [5, 12]× hand + Automation ×3, a Herb Garden and 3 disciples bought; **real expert ≥ 1.5 h**; three ascensions
  (TEST): run 2 ≥ 0.7 × run 1, all-vows run 3 ≥ 1.1 × run 2; determinism.
- Editor (Mono) times: human real 118 s, expert real 114 s, determinism 108 s, three ascensions 73 s, TEST smoke 26 s.
  Expert / three-ascension / determinism are `[Category("Slow")]` (still run by default; filter them out for quick
  loops). Full EditMode suite: 168/168 green (≈ 7.7 min).
- New `BridgeThroughputTests`: a bridge pair beats 1.5 × the best hand-carry rate; a sky wisp carries a stack, the
  reservation counts items, nothing is lost.
- `SimTestUtil.LoadConfig()` now pins TEST scaling on (the unit tests' numbers were written for it);
  `LoadDataConfig()` returns the shipped config. Stale expectations in the other Sim tests were updated to the new
  numbers / formulas.

## Remaining concerns

1. **The auto share is demand-driven**: the bot clicks only when no loose / stored stock exists, so the ratio moves
   with what the mid-game asks for by hand (Grove herbs, Algae, Obsidian). Expert play reads 16× mid-run; the last
   20 % of a run is ~30× (converter work). The test asserts human mid-run in [5, 12]. Expert sits 6 min above its
   1.5 h floor.
2. **Most of the time is hauling**: cursor moves + island pans are ~1 h 15 min of the human run's 2 h 14 min.
   Lanterns still move 1 item per beat, so wisp logistics do not replace carrying yet, and the bot builds no lantern
   networks besides the bridge line. Next pass: lantern throughput, and a bot that links stores → converters.
3. **Bridge end-to-end** is limited by the lanterns that fill and empty it (1 item/s each; Wisp Haste helps); the
   pair itself does ~5/s.
4. **Later runs** are ~85 % of run 1 (tutorial skipped + Remembered Paths / Legacy Automation / Fleet Hands). Fine
   for "≈ same length"; content-on-ascension (ADR 0004) still needs its design session.
5. **The late game leans on iron and essence**: dragon stage 4 (450 herbs, 180 essence, 70 bars) ≈ 24 min, the gate
   chain ≈ 24 min. The main machine waits are Iron Bar / Spirit Stone / Jade batches; a second Forge would help, but
   the bot never builds duplicate converters.
6. Quest goals are code (`$fn`), so the weaver quest (2 rope + 6 cloth) did not scale; the tutorial is ~21 min.
7. Only one seed per profile is asserted; spot checks over 4 seeds varied by < 1 %.
