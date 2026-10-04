# ADR 0003 — Floating islands and Spirit Bridges

Status: accepted (2026-10-04)

## Context

In the original, the seven regions sit on a fixed 3×3 grid with 5-cell gaps,
and wisps never leave their region, so about 11 recipes that need items from
two or three regions can only be supplied by hand. The planned fix was a
"Spirit Vault". We considered three options: shared-inventory vaults, a
central hub, and wisp routes that cross the border.

## Decision

- Regions become **Islands**: floating landmasses at **authored positions**
  in the Game scene, separated by sky. The starting layout keeps the old
  neighbour relations (Farm left of Center, Mine right, Fishing below, …)
  spread out by about 20–40 cells of sky.
- Cross-island logistics uses **Spirit Bridges**, not a vault:
  - Bridges come in one-way pairs, one bridge on each of two Islands, and any
    two unlocked Islands can be paired.
  - The player pairs them from a list in the bridge's panel.
  - The sending bridge has an untyped buffer (cap 20). Local lanterns feed it,
    and on each beat its wisps fly across the sky at the normal wisp speed, so
    distance matters.
  - The receiving bridge is emptied by local lanterns.
  - If a pair breaks, wisps already in flight return to the sender.
- A Spirit Bridge costs wood 10 + stone 10. It appears in the build menu once
  the player has unlocked a second Island.
- Island silhouettes are irregular and **purely visual**: each Island's
  painted landmass is larger than its square 93×93 playable area, with a
  hand-painted coast extending 3–12 cells beyond it. The simulation grid stays
  square, so there is no shape mask in the sim.
- A locked Island shows its veil plus an unlock sign at its edge.
- The sky between Islands is a gradient background with parallax clouds.

## Consequences

- Island positions become scene data instead of a formula. The sim still
  keeps Island-local coordinates, plus a per-Island world offset for flights
  between Islands.
- This deliberately diverges from `old-game/`: the parity audits treat world
  layout and cross-island logistics as out of scope.
- Local lantern networks don't change; bridges are just an endpoint type.
