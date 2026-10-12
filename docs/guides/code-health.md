# Code health guide (Code health reviewer)

Agent: `.claude/agents/code-health-reviewer.md` (opus).

## Purpose
Keep the foundations sound as the game grows from 7 to 11 Islands and gains the Shrine and requesters: architecture boundaries, tests that mean something, safe saves and data, no leftovers of removed systems, docs that match the code. Review risky diffs without anchoring on the author's explanation.

## Current state of this game
- **Layout:** Unity 6000.3.12f1, URP 2D. Five asmdefs plus tests: `IdleGrounds.Sim` (pure C#, no UnityEngine; all state and rules), `IdleGrounds.Game` (views, UI, input; `*ViewSync`, `HandController`, `SaveService`, `AudioService`), `IdleGrounds.Editor` (builders, menus), `IdleGrounds.Sim.Tests`, `IdleGrounds.Game.Tests` (+ PlayMode tests). Sim is ticked by `GameRunner` with a deterministic clock/RNG; view queries are non-allocating.
- **Tests:** fast gate `bash tools/simtests/run.sh` (dotnet test over linked Sim sources + `Tests/Sim`, 168 tests, about 30 s; excludes `Slow` and `UnityOnly`); `--all` adds Slow playthroughs (171). Unity EditMode 175/175 and PlayMode 5/6 at Phase 1 (one known failure/flaky: `Logistics_WispLaunches_ConverterCompletes`, timing, fails before and after the change). Full Unity runs block the editor, so only at milestones, in the background copy.
- **Data pipeline:** `Assets/_Project/Data/Source/game-data.json` is now hand-tuned. **Do not re-run `tools/data-export`** (overwrites it). Import with menu `Idle Grounds/Data/Import From JSON` (idempotent) to rebuild ScriptableObjects + `GameDatabase.asset`.
- **Save:** `SaveCodec` schema 2 with `MigrateFromV1` (+ `SaveMigrationTests`): drops ascensions, AP, perks, vows, offerings; perks become utility bonuses, built Ascension Gate refunded as ground items. v1 backup of the owner save exists in the save folder. ADR 0007 asks for another migration step for Shrine / requests / new Islands.
- **Retired but present:** perk effects still read through `Simulation.Bonus(key)` (empty on a fresh game) as the hook for Phase 3 requests; save id `slumber` kept for old Tireless Wisps; JS quirk intentionally not ported (wisp-delivered firestone goes to stock when the recipe takes it).
- **Repo notes:** `AGENTS.md` and `.codex/` are untracked copies for another tool and can drift from CLAUDE.md; `HANDOFF.md` is about 250 lines and growing; `old-game/` frozen; 10 art keys without consumers; ADR 0006 rejected, 0004's ascension parts superseded.
- **Unity gotchas (HANDOFF):** keep `ProjectSettings/EditorSettings.asset` `m_EnterPlayModeOptions` at 0; revert the dirtied TMP fallback font before committing; first `execute_code` after Play often fails; save the active scene after editor work; ArtIntake reports animated art as changed each run (harmless).

## Rules to enforce
1. Dependencies point inward: Game and Editor depend on Sim, never the reverse; no rules in views.
2. Sim is deterministic: no wall-clock time, no `UnityEngine.Random`, no static mutable state.
3. Every schema change: bump version, migrate, round-trip test, a fixture from the previous version.
4. Every referenced id (item, building, recipe, request, quest, REVEAL) exists; add a data-integrity test when adding tables (`SHRINE`, `REQUESTERS`).
5. A bug fix has a test that failed before; no retries to hide flakiness.
6. No retired terms outside migration code and historical ADR text.
7. No commits of tool noise (Library, Temp, dirtied fonts, `Builds`); respect the 100 MB per-file GitHub limit.
8. Docs are claims: test counts, ADR statuses and the HANDOFF pointer must match reality.

## Checklists
- **Diff review:** which layer, which tests, which saves are affected, hot-path allocations, determinism, naming per `CONTEXT.md`, unexpected asset churn.
- **Phase gate (ADR 0007 steps 2-6):** data-integrity test, migration test, bot still ends, fast gate green, Unity milestone run, retired-term Grep clean.
- **Cleanup sweep:** dead builders / menus, unused art keys, `// TODO`, commented code, duplicate helpers, big methods in `PlaythroughBot` / `Simulation`.
- **Docs drift:** CLAUDE.md vs AGENTS.md, HANDOFF accuracy, plan.json vs reality.

## Open issues
- Flaky PlayMode test (plan task "Fix the flaky PlayMode test").
- AGENTS.md / `.codex/` untracked and diverging.
- No data-integrity test for cross-table ids yet; none for `SHRINE` / `REQUESTERS` because they are not in the data.
- HANDOFF.md carries long history; consider trimming "Last session summary" (keep the rules in CLAUDE.md).
- Unity-only tests are not in the fast gate.

## Owner feedback that constrains this role
- `owner-picks-2026-10-12.json`: "move generated island dressing out of Game.unity (into repo assets, still on GitHub)": done; keep it so.
- `owner-picks-2026-10-12-requesters.json`: vows removed completely; no leftovers.
- Personal workflow (`.claude/rules/personal-workflow.md`): one task, one commit, HANDOFF in the same commit; verify before claiming done.
