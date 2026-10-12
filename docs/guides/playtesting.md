# Playtesting guide (Playtester)

Agent: `.claude/agents/playtester.md` (sonnet). Reports findings; does not fix.

## Purpose
Play the game the way a new player would and tell the team what happened: can it be finished, where do players get lost or bored, what breaks. Two tools: the playthrough bot (fast, deterministic) and a human-eyed walkthrough on a fresh save in the background copy.

## Current state of this game
- **Bot:** `Assets/_Project/Scripts/Tests/Sim/PlaythroughBot.cs` + `PlaythroughBotTests.cs`. It uses only the Sim command API, with real input limits (swing interval, 3 suction steps per tick at a 64 px radius, feed 20/s, withdraw 5/s, one mouse action per 50 ms tick) and two profiles: **expert** (island pan 1 s, instant cursor) and **human** (pan 2 s, 1500 px/s cursor, 300 ms per new target). Deterministic: Unity Mono and .NET 8 produce identical timelines.
- **Run it:** `bash tools/simtests/run.sh --all` (about 70 s, includes the 3 Slow playthroughs). Fast gate without bots: `bash tools/simtests/run.sh` (168 tests, about 30 s).
- **Baseline (Phase 1, 7 Islands, no Shrine or requests yet):** human pace to the Heavenly Gate ending **2:12:27**. Older reference timeline (balance pass 1, old ascension flow): Dragon stage 1 at 0:00:30 (bot), Mine 0:03:58, Dragon 2 0:07:33, Fishing 0:12:51, Dragon 3 0:15:38, Farm 0:16:58, Grove 0:55:09, Dragon 4 1:17:28, Volcano 1:28:28, Celestial 1:36:20 (see `docs/port/balance-pass-1.md`).
- **Targets to verify later (ADR 0007 section 6):** greedy policy ending 2:30-3:30; no-requests policy <= 4:15; Tributes in order; Island gaps 8-30 min; auto >= 5x hand mid-game.
- **Not covered by the bot:** whether the UI is understandable, whether waiting feels fun, visual glitches. A human-eyed pass is needed for those.
- Known gaps: PlayMode smoke tests (6) back up and restore the real save; one is flaky (`Logistics_WispLaunches_ConverterCompletes`). Never run them against the owner's save folder from the background copy without confirming the backup/restore path.

## Rules to enforce
1. Never touch the owner's save. Fresh save only, in the background copy's own data path.
2. Say exactly what you ran and what was bot-only vs human-eyed.
3. Report with repro steps, build/commit hash, expected vs actual; no vague "felt slow".
4. Findings ranked: **blocker** (cannot finish, data loss, crash), **major** (soft-lock risk, wrong numbers, blocked feature), **minor** (polish).
5. Record what felt good too, so it is not "fixed" away.
6. Do not fix. Hand off: balance to the balance designer, confusion to the onboarding designer, look to the art director, UI to UI/UX.

## Checklists
- **Soft-lock hunt:** every Tribute, recipe and request item has a source at that point; every stuck state has a way out; a full hand, a full storehouse, a destroyed lantern chain, bridging the same pair twice, loading mid-installment.
- **Save/load:** reload at each Dragon stage and after each Island; old v1 save migrates (`SaveMigrationTests` cover the codec; check in game).
- **Requests:** each of 33 completes, installments persist when switching, "†" waits visibly, "‡" explains itself, skipping all still ends the game.
- **Ending:** build the Heavenly Gate on Celestial only; the ending plays once, replay from the Gate works, free play continues.
- **Pace table:** minute, event, what the player was doing, idle seconds with nothing to do (> 60 s is a finding).
- **Regression after a fix:** repeat the original repro, plus its neighbours.

## Open issues
- Bot policies (greedy, no-requests) and request play are not implemented; bot cannot yet test the Shrine or requests.
- No agreed "boredom" metric; propose idle-seconds-per-hour and wait times from the bot (the report lists non-acting time: cursor moves 6:39, pans 3:26, converter waits 6:59 in the old run).
- Headless human-eyed play needs the background copy with a game view.

## Owner feedback that constrains this role
- `owner-picks-2026-10-12-requesters.json`: requests optional, all 3 open, right-click feeding only: the playtest must try skipping them.
- ADR 0004 (owner targets 2026-10-07): hands early, machines win; first run feels like 2-3 hours.
