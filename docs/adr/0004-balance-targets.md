# ADR 0004 — Real balance targets

Status: accepted (2026-10-07)

## Context

The game shipped in the original's fast TEST mode (timers ×0.2, costs ×0.5).
The playthrough bot (`docs/port/playthrough-report.md`) showed a first
ascension in ~22 min even with real balance, with automation, wisps and the
Altar tree optional; vows were free AP; the perk shop maxed in ~7 runs; Spirit
Bridges were slower than carrying by hand.

## Decision

- **First run** (start → first ascension) for a typical player: **~2–3 hours**.
- **Later runs stay about the same length.** Ascension no longer mainly speeds
  the world up (the +20% world speed per ascension is removed or made
  negligible); it is meant to unlock **more content** (new islands / buildings /
  recipes — designed separately). Perks give quality-of-life, not big speed.
- **Hands early, machines win:** the first 20–30 min are mostly hand work; by
  mid-game wisps, converters and automation out-produce clicking **5–10×**.
- **TEST mode** stays as a dev toggle in `GameBalance`, **off by default**.
- Vows must cost something real (slower run) for their AP bonus.
- Spirit Bridges must beat hand-carrying for bulk transfer.

## Consequences

- The playthrough bot (human pacing, TEST off) is the regression gauge: its
  first-ascension time must land in 2–3 h and the automation share of
  production must pass 5× mid-run.
- Content-on-ascension needs its own design session.
