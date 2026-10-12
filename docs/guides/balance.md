# Balance guide (Balance designer)

Agent: `.claude/agents/balance-designer.md` (opus).

## Purpose
Own the numbers: recipes, timers, costs, Tributes, request costs, automation levels, hand caps, enemy and spawn tuning, and the playthrough bot that proves the pacing. Duties of an "AI designer" (making the bot play well enough to be a gauge) also live here.

## Current state of this game
**Targets** (ADR 0004 as amended by ADR 0007 section 6):
- Ending (Heavenly Gate) at human pacing with the greedy request policy: **about 3:00, accepted band 2:30-3:30**.
- No-requests policy must still end, **at most 4:15** (proof that requests are optional). Completionist (33/33) about 4 h.
- Tribute timestamps (first guesses): Mine 0:08, Fishing 0:20, Farm 0:32, Grove 0:48, Volcano 1:05, Lotus 1:25, Thunder 1:45, Ravine 2:05, Tribulation 2:25, Celestial 2:40, Gate 3:00. Dragon 3 about 1:10, Dragon 4 about 2:20. Every Island gap 8-30 min.
- Hands early, machines win: first 20-30 min mostly hand work; automation >= 5x hand mid-game. Spirit Bridges beat hand-carrying. TEST mode is a dev toggle, off by default.

**Latest measured numbers**
- Phase 1 bot (7 Islands, no requests yet): **2:12:27** human pace start to the ending. This is *before* Shrine, requests and the 4 new Islands, so it is a floor, not the target.
- Balance pass 1 (`docs/port/balance-pass-1.md`): first ascension human 2:09:40, expert 1:35:39. Auto/hand per 15 min: 0.7, 0.8, 3.7, 3.7, 8.0, 185, 8.9, inf, 9.5; mid-run 7.8x human, 16.6x expert. Bridge 4.8 items/s vs 1.7-2.3 by hand.
- Tests: fast .NET 167-168, all 169-171 including Slow playthroughs; Unity EditMode 175/175 at Phase 1.

**Bot** (`Assets/_Project/Scripts/Tests/Sim/PlaythroughBot.cs`, `PlaythroughBotTests.cs`): uses only the Sim command API with real input limits; pacing profiles *expert* and *human* (island pan 1 s / 2 s, cursor speed, 300 ms per new target); a goal stack with a recursive `Acquire(item, n)`. ADR 0007 asks for two policies (greedy, no-requests) and assertions: Tributes in order, gaps 8-30 min, auto >= 5x hand, no Tribute item needed before its source Island. Seeds used: 777, 1, 2, 12345.

**Encounters:** Fox (essence), Boar (bone, lured with Beast Bait), Azure Wolf (Ravine; hp 12, pair; beast_bone + beast_core). CombatSystem moves to per-Island enemies (ADR 0007). Keep it gentle: calm tone, no blood.

## Rules to enforce
1. Measure with the bot before and after; one lever at a time; keep seeds fixed.
2. Quantities may move; Island order may not. Cut Tribute quantities or Dragon 4 needs before cutting content.
3. Tribute items 5-20 each; hand-only raws <= 20.
4. Do not re-run `tools/data-export` (overwrites tuned data).
5. Mid-run auto/hand stays in 5-10x; if it jumps toward 20x, add hand-only demand or slow free machine items (past fixes: slower Herb Garden, fewer Mine rocks).
6. Utility bonuses (e.g. -5 % timers, +15 % wisp speed) are read via `Requests.Bonus(key)`; check stacking never makes a stage trivial.

## Checklists
- **Before a data change:** record bot baseline (both policies, 3+ seeds).
- **After:** table of Island/Dragon/ending timestamps, auto/hand windows, invariants held, a note on tedious waits.
- **New recipe/building:** inputs available at that point? output consumed by something? payback time vs hand work?
- **New enemy/spawn:** drops needed by a recipe; respawn vs automation click rate.

## Open issues
- Ending will move from about 2:10 to about 3:00 once 4 Islands, Shrine and requests land; the bot policies are not written yet (ADR 0007 step 5).
- Flaky PlayMode test `Logistics_WispLaunches_ConverterCompletes` (timing) hides real wisp-timing regressions.
- Tribute quantities for Islands 6-10 are first guesses.
- Automation is node-regrow-bound, not click-bound; lowering `AUTOMATION_CLICKS` barely moves it.

## Owner feedback that constrains this role
- `owner-picks-2026-10-12-requesters.json` / `owner-picks-2026-10-12.json`: Tributes of 5 items x 5-20 so automation is the reliable route; requests never gate; fixed order; vows removed (no challenge modifiers).
- The 2026-10-07 target (2-3 h first run, hands then machines) was set by the owner in the ADR 0004 session; ADR 0007 raises it to about 3 h because of four more Islands.
