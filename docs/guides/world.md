# World guide (World designer)

Agent: `.claude/agents/world-designer.md` (opus). Keep this file current; it is the role's memory.

## Purpose
Own the shape of the world and what unlocks when: Island layouts and the overworld, Zones, node and fixture placement, the Sky-Bridge Shrine and its Tributes, the fixed unlock order, Sky Paths, and the progression gates (Dragon stages, Heavenly Gate). Quantities and timings are shared with the balance designer; words with the writer; look with the art director.

## Current state of this game
- **11 Islands, one continuous game** (ADR 0007). Fixed unlock order: Center (start), Mine, Fishing, Farm, Spirit Grove, Volcano, Lotus Marsh, Thunder Crag, Azure Beast Ravine, Tribulation Peak, Celestial Peak (final). `docs/Design/islands.json` is the source for the Hub Islands tab.
- **Built today (Phase 1 done):** Center, Mine, Fishing, Farm, Grove, Volcano, Celestial. **Planned:** Lotus Marsh, Thunder Crag, Azure Beast Ravine, Tribulation Peak (plan tasks Phase 2-4 are "waiting").
- `game-data.json` still has the old layout (7 Islands; Celestial at 127,252, the future Tribulation slot) and no `SHRINE` / `REQUESTERS` tables yet. The target layout is ADR 0007 section 2: Celestial 125,-128 (north of the Center); Farm 0,6; Center 123,0; Mine 249,4; Grove 8,131; Fishing 125,126; Volcano 252,122; Lotus 4,258; Tribulation 127,252; Thunder 250,256; Ravine 378,126 (cells; world grows to 4x4 slots).
- Each Island has a 93x93-cell square playable area. The painted coast extends 8-30 cells beyond it and is purely visual (ADR 0003). Keep at least 14 cells of sky between coasts.
- **Sky-Bridge Shrine:** fixed, indestructible 5x5 on the Center's north edge (local row 1, col 44; rows 0-7 x cols 42-50 stay clear). Each Tribute is 5 different items x 5-20; paying it raises a visual Sky Path and unlocks the next Island. Spirit Bridges (ADR 0003) are separate and carry items.
- **Tribute list (first guesses, balance tunes):** Mine: wood 20, leaves 15, stone 20, plank 10, brick 10. Fishing: iron_ore 20, iron_bar 10, brick 20, plank 20, spirit_stone 5. Farm: algae 20, iron_bar 15, tools 5, jade 10, charcoal 20. Grove: wheat 20, flour 15, cloth 10, rope 10, glass 10. Volcano: spirit_herb 20, paper 15, robe 5, qi_elixir 10, spirit_buns 10. Later Islands: ADR 0007 section 1.
- **Gates besides Tributes:** the Dragon (Center, 4 stages) reveals Forge, Algae Farm, Herb Garden, Pill Furnace; stage 4 plus Celestial open allows the **Heavenly Gate** (talisman 14, star_steel 20, dragon_scale 6, tribulation_essence 10; buildable only on Celestial). Requests unlock buildings/utilities and never gate progression.
- New Island contents (nodes, enemies, on-unlock buildings, request-only buildings): ADR 0007 section 3.

## Rules to enforce
1. Order never changes without an owner decision (`SHRINE.order` is authoritative).
2. Tribute items come only from earlier Islands or buildings revealed without requests; hand-only raws at most 20.
3. Requests are optional: no item on the main path may come only from a request reward.
4. One NPC per Island, 3 requests all open at once, right-click feeding only.
5. Every node/fixture/enemy lists Island, Zone, yield, consumer and art key.
6. Edit `game-data.json` by hand; never run `tools/data-export`. Keep `islands.json`, ADR tables and `WORLD.islands` identical.
7. Existing saves keep unlocked Islands even if out of order (Sky Paths appear); layout changes are world-offset changes only.

## Checklists
- **New Island:** biome and palette (art director), node list, enemy, on-unlock buildings, NPC + 3 requests, Tribute, position (>= 14 cells of sky), automation target, Hub `islands.json` entry, art keys requested.
- **Item graph walk:** for each item, first source by hand, first source by automation, every consumer. Flag items with no source, or whose source is behind a later gate (the "†" items are allowed only in requests).
- **Shrine change:** clear zone intact, panel shows the next Island plus greyed previews, no wisp delivery.
- **Layout change:** camera bounds, sky tiling, Island prefab regeneration, background Islands placement.

## Open issues
- Phases 2-4 (Shrine, 7 requesters, 4 new Islands) not built; `SHRINE` / `REQUESTERS` data not written.
- Hand-only raws in Tributes 6-10 (obsidian, reed, soulvine) may feel like chores if the player skips requests.
- Two requests wait on items from later Islands (†) and two on their own NPC's building (‡); the panel must say why.
- Shrine placement collides with the Spirit Tree `midTop` zone and existing saves' buildings (nudge logic needed).
- 11 Islands and negative world rows stress IslandsBuilder, camera bounds and sky.

## Owner feedback that constrains this role
- `owner-picks-2026-10-12.json`: "ascension is not fun, lets remove it"; requests do not gate progression; Islands unlock by a Tribute at a magical shrine in the north that also locks the final Island; Tribute = 5 different items x 5-20 so automation is the reliable way; each NPC unlocks a new building or a utility.
- `owner-picks-2026-10-12-requesters.json`: fixed order; 1 NPC per Island; all 3 requests open at once; right-click feeding only; Heavenly Gate ending then free play; vows removed; all 4 new Islands, placed after Volcano and before Celestial.
- `owner-picks-2026-10-12-team.json`: all agent work happens in the background copy.
