# Playthrough bot report: is the game still completable?

Date: 2026-10-07. Bot: `Assets/_Project/Scripts/Tests/Sim/PlaythroughBot.cs`.
Tests: `PlaythroughBotTests.cs` (4 EditMode tests, about 37 s in the Editor). Full EditMode suite: 161/161 green.

## Verdict

**Yes, the game is completable.** On the current design (floating Islands with Spirit Bridges, no offline progress, Tireless Wisps, the firestone routing fix), a fresh run reaches the Ascension Gate, fills all 6 offerings, ascends and buys perks. This holds:

- in TEST mode and at real balance (`test.enabled = false`);
- at "expert" pacing and at "human" pacing;
- for 12 seeds in each mode;
- for three ascensions in a row, where the third run carries **all four vows** (Burden, Cold Hearth, Restless, Solitude).

The bot never got stuck, and the per-tick world-invariant checks found no violations. **No Sim bugs were found, so no Sim code changed.** The Unity Editor (Mono) produces exactly the same timelines as a .NET 8 harness, so the runs are deterministic across runtimes too.

| Profile | Mode | First ascension (sim time) | AP |
|---|---|---|---|
| expert | TEST (timeScale 0.2, costScale 0.5) | **6:01** | 21 |
| human | TEST | 11:49 | 21 |
| expert | real balance | 15:25 | 21 |
| human | real balance | **22:14** | 21 |
| expert, 3 runs (run 3 with all vows) | TEST | 5:57 / 10:21 / 15:25 | 21 / 22 / 39 |

The cap was 6 simulated hours, and the slowest run used about 6% of it. Coarse ticks were not needed: every run uses true 50 ms ticks and takes 0.6–4 s of wall time in .NET (3.6–12 s in the Editor).

## How the bot plays

- **Commands:** it uses only the Sim's command API: `Harvest`, `Suction`, `DropFromHand`, `Withdraw`, `RotateHand`, `PlaceGhost`, `SetRecipe`, `AddLink`, `PairBridges`, `UnlockArea`, `SelectUpgradeNode`, `RecruitDisciple`, `Attack`, `ClaimQuest`, `Ascend` and `BuyPerk`. The only thing it edits directly is the clock.
- **Tick model:** `Tick()` runs every 50 ms and `AutomationTick()` every 1 s, the same cadence as `GameRunner`.
- **Input limits:** the bot follows `HandController`'s limits, with one mouse action per 50 ms tick:
  - harvest swings are paced by `Timing.HarvestInterval`;
  - vacuum runs 3 suction steps per tick (60 Hz) at a 64 px radius, type-locked;
  - feeding is at most 20 per second, withdrawing at most 5 per second;
  - attacks follow `attackMs`;
  - rotating the hand (Q) costs one tick.
- **Hand cap:** the hand cap is enforced by the Sim. When the hand is full, the bot drops on open ground only the items no active goal needs.
- **Pacing profiles:**
  - **expert:** an island pan costs 1 s, and the cursor moves instantly within an island.
  - **human:** an island pan costs 2 s, the cursor moves at 1500 px/s, and every new target adds 300 ms.
- **Planner:** a stack of goals.
  - The 14-quest chain runs in order. `QuestTarget` and the quest goals pick what to do.
  - Then the bot buys Hand Size ×2 and Regrow Speed. It builds a Spirit Bridge line, Fishing → Center: a gathering stone on the spring field → lantern → bridge → paired bridge → lantern → storehouse. It then feeds dragon stage 4 and unlocks the remaining islands.
  - It crafts an **Ember Pill** in a Pill Furnace and feeds it to the dragon. The firestone goes in as an ingredient.
  - Finally it raises the Gate, fills the offerings, ascends and spends its AP.
  - Every sink (dragon tribute, Altar job, ghost cost, island installments) uses one generic `Deliver` loop. Its `remaining()` is the same data `Milestone()` shows. That loop calls a recursive `Acquire(item, n)`, which tries, in order:
    1. loose items on this island;
    2. a storehouse, seal, stone or bridge holding the item;
    3. loose items elsewhere;
    4. otherwise it produces the item: it crafts it (finds or builds a converter, sets the recipe, feeds inputs and fuel, collects the output), gathers it raw from a node, fixture or field on an unlocked island (unlocking the island first if needed), hunts a fox for essence, lures a boar with Beast Bait for bone, or waits by the dragon for scales.
- **Converters:** the bot leaves the starter network's converters on their recipes. When it needs a different recipe it builds its own (second kiln, workbench, infusion array).

## Timeline

| Milestone | TEST, expert | Real, human |
|---|---|---|
| quests: wood / leaves | 0:00 / 0:01 | 0:02 / 0:04 |
| dragon stage 1 (quest dragon1) | 0:01 | 0:11 |
| fox, first building, first Altar upgrade, link | 0:02 – 0:09 | 0:14 – 0:41 |
| Mine unlocked (explore) | 0:12 | 1:00 |
| dragon stage 2 | 0:16 | 1:26 |
| iron quest (Forge + bars) | 0:32 | 2:29 |
| Fishing unlocked (waters) | 0:35 | 2:46 |
| dragon stage 3 | 0:43 | 3:14 |
| Farm unlocked | 0:51 | 3:41 |
| weaver (2 rope + 6 cloth) | 1:13 | 5:01 |
| first disciple, **quest chain done** | 1:16 | 5:23 |
| Hand Size 3, Regrow Speed 1 | 1:31 / 1:34 | 6:57 / 7:15 |
| Spirit Bridge Fishing→Center paired | 2:05 | 8:54 |
| Grove unlocked | 2:11 | 9:14 |
| **dragon AWAKENED** (stage 4) | 2:28 | 10:23 |
| Volcano / Celestial unlocked | 2:37 / 2:56 | 10:55 / 12:30 |
| Ember Pill blessing | 3:40 | 14:51 |
| Star Anvil built | 4:55 | 18:59 |
| **Ascension Gate built** | 5:22 | 20:10 |
| offerings 6/6, **ASCENDED** (+21 AP), perks bought | 6:01 | 22:14 |

Time per phase, real balance, human pacing:

| Phase | Time |
|---|---|
| quest chain | 5:23 |
| hand upgrades | 1:51 |
| bridge setup | 1:38 |
| dragon stage 4 | 1:28 |
| Volcano + Celestial unlocks | 2:07 |
| Pill Furnace and pill | 2:20 |
| **gate** | **5:19** |
| offerings | 2:03 |

The longest tutorial steps are weaver (1:47) and iron (1:03).

Where the non-acting time went in that run:

| Activity | Time |
|---|---|
| cursor moves | 6:39 |
| island pans | 3:26 |
| waiting for converter batches | 6:59 |
| swing cooldown | 1:44 |
| waiting for regrow | 0:34 |
| waiting for a fox | 0:32 |

Within the converter waits, the biggest items are iron_bar (2:23), jade (0:51), talisman (0:44) and spirit_jade (0:40). The waiting is only noticeable at real balance: at expert pacing in TEST mode, the bot spends less than 1:20 of the run waiting on batches.

## Stuck points and blockers

**There were no blockers.** Every quest completes. Every tribute, cost and installment can be paid from sources on unlocked islands. Each cross-island recipe (Qi Elixir, Rope, Robe, Talisman, Star Steel and the unlock costs) is supplied by carrying items by hand, and the bridge works as a supplement. The veteran runs and the all-vows run (no starter network, half hand, doubled tributes, double fuel burn) also complete.

Things that cost the bot time while it was being written. These are bot mistakes, not Sim bugs, but they are traps for a real player:

1. **A gathering stone placed just outside a field collects almost nothing.** The bot's first spot for the Fishing stone was the cell diagonally past the spring field corner, at 18,18. The 8-cell reach covered only a sliver of the field, and the bot's own vacuuming emptied what it did reach. The bridge delivered 4 items in 3 minutes. Moving the stone to the field centre fixed it: the bridge then delivered 60 items in the same time. The reach circle in the UI matters.
2. **Switching a starter converter's recipe fights its lantern links.** For example, the starter kiln set to Glass refuses the clay its lantern keeps sending. Building a second converter is the clean answer. The milestone walker's `SwitchRecipe` step can point a player at the starter building.
3. **Fuel goes to the burner's rack whenever it is at the front of the hand.** If wood is the front stack while you feed iron ore to a forge, the wood goes into the rack. This is by design, but it can surprise players.

## Balance concerns

These are observations only. No balance numbers were changed.

1. **Runs are very short and mostly active.** At real balance, a careful human-paced player reaches the first ascension in about 22 minutes; an expert player does it in 15. In TEST mode it takes 6–12 minutes. The bot never bought Automation, and it bought only 4 Altar levels (Hand Size ×3, Regrow Speed). The idle layer (field generators, wisps, automation, disciples) is optional. Active clicking with good routing beats it, so there is no point where waiting is the best option.
2. **The gate is the long pole, at about 25% of a run.** It needs Star Anvil → Tools ×3 → planks + iron bars, plus Star Steel, Talisman (Paper + Spirit Jade + Qi Elixir) and 3 dragon scales. The Forge is the main bottleneck: about 35 iron bars per run at 6 s each with one input, and roughly 2.5 min of pure waiting at real balance.
3. **Vows are free AP.** The all-four-vows run took 5:04, against 6:01 for run 1, and paid 39 AP instead of 22. Each vow's penalty is offset by something else:
   - **Fleet Hands** cancels Burden.
   - **Tribute shrink** (×1/(1+0.25·asc)) cancels Restless.
   - **Fuel is abundant**, so Cold Hearth costs nothing: wood = 10 s per unit, and the starter seal feeds the Forge.
   - **Solitude** costs about 40 s (run 3 took 5:04, run 2 4:23), because the bot builds the converters it needs anyway.
4. **AP economy is fast.** 21 AP after the first run buys 10 perk levels. The whole shop costs 234 AP, so it is maxed in about 7 runs, or about 6 with vows. Remembered Paths and Legacy Automation also shorten the following runs.
5. **Spirit Bridge throughput is limited by distance.** A sender only launches while `receiver inv + in-flight < 20`, so throughput is at most 20 items per flight time.
   - Fishing→Center is about 4,000 px at 170 px/s, so roughly 24 s per flight and at most about 0.84 items/s. The bot measured 198 items in about 4 min.
   - From Celestial it would be about 0.42 items/s.
   - The sending bridge sat at 20/20 with 20 wisps in the air the whole time. Only Tireless Wisps and Wisp Haste raise the cap.
   - Bridges were never needed: the bot took only 8 water from the bridge's storehouse, and carrying by hand was faster. To make bridges matter, consider raising the receiver reservation cap for long routes.
6. **The dragon is cheap.** Stage 4 (10 herb, 8 essence, 3 bars in TEST) takes about 20 s. The ×0.5 scale timer with the Shrine was never needed: 3 + 2 scales accumulate while the gate chain runs.
7. **The weaver quest is the slowest tutorial step.** Cloth costs 3 cotton, and the quest needs 6 cloth + 2 rope in hand, about 24 cotton from 5 patches with an 18 s regrow. It is not a blocker, but it is a grind at real balance.

## Running it

- **In the Editor:** EditMode tests, `PlaythroughBotTests`.
  - TEST mode, expert: seed 12345.
  - Real balance, human pacing: seed 777.
  - Three ascensions with vows: seed 4242.
  - Determinism: seed 99, run twice.
- **Test output:** each test prints its timeline, phases and idle breakdown to the test output.
- **Bot knobs:**
  - `RunsWanted`, `VowsForNextRun` and `UseHumanPacing()`;
  - `Trace` / `TraceEvery` print a progress line;
  - `CheckInvariants` (on by default): hand ≤ cap, buffers ≤ caps, no negative stock or fuel, no NaN or off-island positions, ground ≤ hard cap, dragon never overpaid.
