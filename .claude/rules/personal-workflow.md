<!-- Auto-seeded into this repo's .claude/rules/ by a SessionStart hook, so
     cloud/remote sessions (which can't read ~/.claude) load it too. Canonical
     copy: https://raw.githubusercontent.com/tarnos12/claude-rules/master/RULES.md
     Safe to commit; edit the canonical copy to change it everywhere. -->

# Personal workflow rules (Mariusz)

How I want Claude Code to work. This repo's own instructions override these
when they conflict.

## Git & committing

- **Commit after every completed task** — one task = one focused, well-described
  commit. Frequent commits, not one big pile at the end.
- **Push after committing** when the repo has a remote.
- If on the default branch and the change is substantial, branch first.
- **Sync before starting** — `git fetch`, check the main branch.

## Session continuity docs

- For multi-session work, maintain a **handoff doc** (`HANDOFF.md`) with a
  "next session / start here" pointer and a "last session summary". Create it
  if it doesn't exist.
- **Update the handoff doc in the SAME commit as the code change.**
- Keep any design/plan doc current.

## Running & verifying

- After a change, tell me **how to see it running** (URL / command / preview)
  and include it in the reply.
- **Verify before claiming done** — run the code / preview; report failures
  honestly with real output.
- Bump cache-busting version tags (e.g. `?v=N`) on any code change.

## Style

- Direct, outcome first. Loop me in on load-bearing findings and direction
  changes; don't narrate every step.
- For multi-part tasks, do the whole thing rather than stopping for permission
  on each reversible step.

## Model usage & cost (added 2026-07-09)

- **The main loop (Fable, or whatever the session runs) is the
  planner/orchestrator/reviewer ONLY.** It reads results, makes judgment
  calls, integrates, commits, and talks to me. It must NOT hand-write
  test scripts, doc updates, screenshots drivers, data cross-checks, or
  other mechanical work — delegate those.
- **Tier delegated work by difficulty, not by habit:**
  - **Opus** — subtle logic edits (engine/save-migration/race conditions),
    adversarial verification of engine-logic findings, anything where a
    wrong answer ships a bug.
  - **Sonnet** — mechanical code edits from a clear contract, test-script
    authoring, doc/HANDOFF/DESIGN updates, data cross-checks, verification
    of doc/markup/mechanical findings.
  - **Haiku** — chores: run test suites and report output, take a
    screenshot with a provided script, version bumps, grep-style lookups.
- **Verification fan-outs must be tiered too**: Opus verifiers only for
  engine-logic findings; Sonnet verifiers for doc/aria/data/markup
  findings. (An all-Opus verify pass is the single biggest token waste.)
- **Exception — don't cargo-cult delegation:** if briefing an agent costs
  more than doing it (a 1-2 line edit, a single obvious command), the main
  loop just does it inline.
- **After every finished piece of work, report a model-usage review**:
  which model did what, subagent token counts per task (they're in each
  task result), and what should be tiered down next time.

<!-- Mirror this section into the canonical claude-rules RULES.md so all
     repos pick it up. -->
