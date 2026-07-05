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
