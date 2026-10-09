# ADR 0005 — 3/4 top-down view, south cliff faces, sea of clouds below

Status: accepted (2026-10-09)

## Context

The camera looks down on the islands and every building, node and creature is
drawn in 3/4 view (Stardew Valley / Eastward style). The world dressing did not
match:
- side-view island undersides were hung along every coast, so on the west and
  east edges they read as tall brown pillars;
- the sky was a horizon landscape (gradient with horizon band, moon, distant
  mountain peaks, flat-bottomed cloud strips).

User feedback: "the perspective of the floating island backgrounds doesn't match
the game island perspective"; the island looked bland and the edges pixelated.

## Decision

- **3/4 top-down** for the whole world. An island shows its top surface; only
  the **south-facing coast** shows a cliff face (a 3/4 rock wall), with the
  hanging underside (rocks, roots, vines, waterfalls) below that wall only. West,
  east and north coasts show a grass lip and a soft drop shadow onto the clouds,
  never hanging rock.
- **Below the islands is a sea of clouds seen from above**: a calm depth colour,
  layered cloud banks and puffs at parallax depths, optional light shafts, and
  distant small islands drawn in the same 3/4 view. No horizon, peaks or moon.
- **Pixel art stays at integer scale** (PPU 32, 1× or 2×; never stretched or
  fractionally scaled).
- Islands are **dressed**, not flat: biome scatter, zone patches instead of
  tinted rectangles, paths/plaza, water and waterfalls.

## Consequences

- The sky peaks, moon and horizon gradient are retired. ART-SPEC §3.6 asks the
  artist for the replacement set: 3/4 south cliff walls, top-down cloud-sea
  layers, distant 3/4 islands, ground macro patches.
- The underside and sky code (IslandCoastBuilder, IslandsBuilder.BuildSky) place
  dressing by coast direction.
