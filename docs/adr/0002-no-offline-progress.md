# ADR 0002 — No offline progress

Status: accepted (2026-10-04)

## Context

The browser original (and the first Unity port, M7) replays the passive
economy for the time the game was closed: up to 8 h (+2 h per Long Slumber
perk level), with a progress modal and a "Welcome back" summary. The user
wants Idle Grounds to be a 100% active game: machines keep producing on their
own, but only while the game is running.

## Decision

- When the game is closed the world **freezes**. On load it resumes exactly as
  saved: no catch-up, no welcome-back modal. Periodic clocks re-arm from "now"
  (stale clocks restart without bursting — the existing `Periodic` rule).
- The game keeps simulating while the window is merely unfocused/backgrounded.
- `OfflineReplay`, the offline boot tiers, the offline modals and the
  Long Slumber perk are removed. Its perk slot becomes **Tireless Wisps**
  (+15% wisp speed and beat rate per level, same costs/max); existing owners
  keep their levels.

## Consequences

- Deliberate divergence from `old-game/` — the parity audits must treat
  offline behaviour as out of scope.
- `lastSeen` / `offlineAwayFrom` save fields become unused (kept readable for
  old saves, ignored).
- Simpler boot path; a crash loses at most the time since the last autosave.
