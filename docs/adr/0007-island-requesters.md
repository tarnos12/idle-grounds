# ADR 0007 — Island requesters, the Sky-Bridge Shrine and 11 Islands replace ascension

Status: accepted (2026-10-12) — supersedes the ascension parts of ADR 0004 and all of ADR 0006 (rejected; its four
Islands, items and buildings are reused here); changes the Island-unlock part of ADR 0003.
Owner decisions: `hub/owner-picks-2026-10-12.json`, `hub/owner-picks-2026-10-12-requesters.json`.

## Context

Owner (2026-10-12): "ascension is not fun, lets remove it. The other game does it by having various buildings/npcs on
each island that request certain items (you select from menu) that unlock new recipes." Binding follow-ups: requests do
**not** gate progression; Islands unlock by paying a **Tribute at a magical shrine in the north** that also locks the
final Island; each Tribute is **5 different items × 5–20**, chosen so automation is the reliable way to gather them;
each NPC request unlocks **a new building** (something new/related to its Island) **or a utility** (e.g. more
automation). Hub answers: fixed unlock order; 1 NPC per Island; all 3 requests open at once; right-click feeding only;
the Heavenly Gate on Celestial Peak is the ending, then free play; vows removed completely; **all 4** ADR 0006 Islands
added, between Volcano and Celestial.

## Decision

**One continuous game, no reset, 11 Islands.** Three layers: the **Dragon** (Center, 4 stages, unchanged — the main
spine), the **Sky-Bridge Shrine** (Island unlocks, fixed order) and **Requesters** (optional NPCs: buildings and
utilities). The Altar upgrade tree is unchanged. The game ends at the **Heavenly Gate** on Celestial Peak.

### 1. Sky-Bridge Shrine and unlock order

- A fixed, indestructible 5×5 building on the **north edge of the Center** (local anchor row 1, col 44; the Spirit
  Tree's `midTop` placement must keep rows 0–7 × cols 42–50 clear). **Celestial Peak moves north of the Center**; the
  Shrine stands at the head of the only path to it, so it visibly locks the final Island.
- Click → Tribute panel: the **next** Island's card (5 rows have/need, art preview) plus greyed previews of the rest.
  Right-click-feed in installments (Dragon/Altar `FeedNeeds`). No wisp delivery (owner: feeding only).
- A full Tribute raises a **Sky Path** — a glowing jade bridge from the nearest earlier Island (Celestial: from the
  Shrine) — and unlocks the Island. Sky Paths are visual only; Spirit Bridges (ADR 0003) still carry items.
- Tribute items come only from **earlier Islands and buildings revealed without requests** (requests are optional).
  "Auto" = how the item is automated on the main path (Altar automation, generator buildings, lantern-fed converters).

| # | Island (id) | Tribute — 5 items | Main-path automation / gate |
|---|---|---|---|
| 0 | Center (`center`) | start | — |
| 1 | Mine (`mine`) | wood 20, leaves 15, stone 20, plank 10, brick 10 | Center automation + Gathering Stones; Workbench/Kiln by lantern |
| 2 | Fishing (`fishing`) | iron_ore 20, iron_bar 10, brick 20, plank 20, spirit_stone 5 | Mine automation; Forge (Dragon 1); Infusion Array |
| 3 | Farm (`farm`) | algae 20, iron_bar 15, tools 5, jade 10, charcoal 20 | Algae Farm (Dragon 2); Jade Carver; Charcoal Pit |
| 4 | Spirit Grove (`grove`) | wheat 20, flour 15, cloth 10, rope 10, glass 10 | Farm automation; Mill, Loom, Kiln (sand) |
| 5 | Volcano (`volcano`) | spirit_herb 20, paper 15, robe 5, qi_elixir 10, spirit_buns 10 | Grove herbs by hand (Herb Garden from Dragon 3); Paper Mill, Loom, Cauldron, Mill |
| 6 | Lotus Marsh (`lotus`) | obsidian 20, firestone 10, glass 20, ember_pill 5, spirit_stone 20 | obsidian/firestone by hand (small); Pill Furnace (Dragon 3) |
| 7 | Thunder Crag (`thunder`) | spirit_lotus 20, reed 20, iron_bar 20, glass 15, talisman 5 | Lotus Pond, reed by hand; Talisman Atelier |
| 8 | Azure Beast Ravine (`ravine`) | thunderwood 20, storm_crystal 15, thunder_steel 5, beast_bait 20, robe 10 | Lightning Rod, Thunder Forge; Cauldron |
| 9 | Tribulation Peak (`tribulation`) | soulvine 20, beast_core 10, soul_lamp 5, thunder_steel 10, spirit_jade 15 | Ravine nodes/wolves by hand; Soul Refinery |
| 10 | Celestial Peak (`celestial`, final) | immortal_peach 15, tribulation_essence 10, talisman 10, soul_lamp 5, spirit_stone 20 | Peach fixture; Tribulation Array |

Quantities are first guesses inside the owner's 5–20 band; the bot tunes them (§6). Hand-only raw items are kept
≤ 20 and appear only after the Island that produces them (its requester sells the automation utility).

### 2. Layout (world offsets, cells; Island-local coordinates unchanged)

| Island | col,row | | Island | col,row | | Island | col,row |
|---|---|---|---|---|---|---|---|
| **Celestial** (N of Center) | 125,−128 | | Grove | 8,131 | | Lotus Marsh (SW) | 4,258 |
| Farm | 0,6 | | Fishing | 125,126 | | Tribulation Peak (S, old Celestial slot) | 127,252 |
| Center | 123,0 | | Volcano | 252,122 | | Thunder Crag (SE) | 250,256 |
| Mine | 249,4 | | Azure Beast Ravine (E of Volcano) | 378,126 | | | |

The world grows to 4×4 slots (cols 0–470, rows −128–350). Negative rows are fine: positions are scene data (ADR 0003).

### 3. The four new Islands (from ADR 0006, re-tuned for one game)

| Island · biome | Nodes / enemy → items | Revealed on unlock (main path) | Request-only buildings |
|---|---|---|---|
| **Lotus Marsh** — misty shallows, giant lotus pads, reed beds, stilt walks | lotus pads (surface, like koi) → `spirit_lotus`; reed beds (chop) → `reed` | **Lotus Pond** (generator, water cells only; spirit_lotus / 5 s, cap 24); **Reed Press** (converter: reed 3 → paper 2; reed 2 → rope 1) | Lotus Distillery |
| **Thunder Crag** — storm-black slate, crystal spires, split trees | split trees (chop) → `thunderwood` (FUEL 30000); spires (break) → `storm_crystal` | **Lightning Rod** (1×1 generator; storm_crystal / 8 s, cap 12); **Thunder Forge** (Burner 3×5: iron_ore 3 + storm_crystal 1 → iron_bar 4, 6 s; iron_bar 2 + storm_crystal 2 → `thunder_steel` 1) | — (utilities) |
| **Azure Beast Ravine** — mossy canyon, giant ribcages, glowing vines | bone heaps (break) → beast_bone; soulvine (chop) → `soulvine`; **Azure Wolf** (hp 12, pair) → beast_bone + `beast_core` | **Soul Refinery** (converter: beast_core 1 + soulvine 2 + spirit_jade 1 → `soul_lamp`) | Beast Pen |
| **Tribulation Peak** — scorched jade terraces, rune circles, storm ring | peach trees (quarry fixture) → `immortal_peach`; scarred jade (break) → jade_shard 2–3, rare spirit_jade | **Tribulation Array** (5×5 converter: storm_crystal 1 → `tribulation_essence` 1 / 30 s) | Immortal Furnace |

New items (12): spirit_lotus, reed, foundation_pill, thunderwood, storm_crystal, thunder_steel, soulvine, beast_core,
soul_lamp, immortal_peach, tribulation_essence, immortal_pill. Change vs ADR 0006: thunder_steel no longer needs
star_steel (beast_bone is hand-only until the Beast Pen). New-Island automation comes from each requester's utility.

### 4. Requesters (11 NPCs × 3 requests = 33)

A requester is a pre-placed, indestructible NPC (3/4 sprite, idle loop) present once its Island is unlocked. Click →
**Request panel**: portrait, one line, **all 3 request cards open at once** (have/need, reward preview); pick any;
paid installments are kept per request, so switching is free. **Delivery: right-click feeding only.** A speech-bubble
marker floats over an NPC while it has an unfinished request. Rewards: a **building** (REVEAL kind `{"request": id}`)
or a **utility** (named bonus read via `Requests.Bonus(key)`; "(ex …)" = replaces that perk).
**† = item from a later Island** (the request just waits; allowed because requests are optional).
**‡ = needs this NPC's own building reward first.** Automation utilities set that Island's automation to **level 2**
(`AUTOMATION_CLICKS`) and auto-harvest its nodes.

| Island · NPC | Request A | Request B | Request C |
|---|---|---|---|
| Center · **Steward Lin**, outer-sect steward | wood 60, plank 20 → **Bamboo Nursery** (generator: bamboo / 8 s, cap 12) | spirit_essence 15, stone 30 → **Spirit Snare** (1×1, auto-strikes foxes in reach 1/s) | spirit_stone 10, cloth 10 †Farm, paper 20 → utility: +5 carry, Storehouses +50 % (ex Fleet Hands) |
| Mine · **Foreman Kang** | iron_ore 40, tools 4 → **Deep Shaft** (generator: iron_ore / 8 s, cap 20) | stone 100, clay 50 → utility: every automation +1 node per tick (ex Keen Automation) | iron_bar 30, charcoal 30 → **Blast Forge** (Burner: iron_ore 3 + charcoal 1 → iron_bar 3) |
| Fishing · **Fisher-Hermit Jiang** | fish 30, algae 30 → utility: **Fishing Automation** (koi, algae, spring) | plank 30, rope 10 †Farm → **Koi Pond** (water-only generator: fish / 6 s) | glass 15 †Farm, spirit_stone 10 → utility: Spirit Bridges carry +2 per sky wisp, send +15 % |
| Farm · **Rice Immortal Tian** | wheat 60, water 30 → **Spirit Paddy** (generator: wheat / 6 s, rare spirit_herb) | flour 30, spirit_buns 10 → utility: fields +10 %, nodes respawn +10 % (ex Astral Bounty, Deep Roots) | spirit_wine 10, spirit_herb 40 †Grove → **Elixir Still** (converter: spirit_herb 2 + spirit_wine 1 → qi_elixir 2) |
| Spirit Grove · **Abbess Qingyun** | spirit_herb 60, bamboo 30 → utility: **Grove Automation** | robe 6, spirit_buns 20 → utility: +1 disciple per Pavilion (ex Master's Hall) | qi_elixir 10, spirit_jade 4 → **Essence Well** (converter: spirit_herb 3 + water 1 → spirit_essence 2) |
| Volcano · **Flame Smith Ouyang** | obsidian 30, firestone 10 → utility: **Volcano Automation** | iron_bar 30, brick 40 → **Fire Vent** (generator: firestone / 10 s, cap 12) | ember_pill 4, star_steel 4 → utility: Burners use 25 % less fuel (ex Ember Heart) |
| Lotus Marsh · **Lotus Maiden He** | spirit_lotus 30, reed 30 → utility: **Marsh Automation** | spirit_lotus 30, qi_elixir 10, spirit_stone 10 → **Lotus Distillery** (converter: spirit_lotus 2 + water 1 → qi_elixir 3; spirit_lotus 3 + qi_elixir 1 + spirit_stone 1 → foundation_pill) | foundation_pill 6 ‡, storm_crystal 10 †Thunder → utility: Pill Furnace makes 2 pills per batch |
| Thunder Crag · **Storm Sage Lei** | thunderwood 30, storm_crystal 20 → utility: **Crag Automation** | thunder_steel 6, glass 20 → utility: wisps fly and lanterns send +15 % (ex Tireless Wisps, Wisp Gale) | thunder_steel 10, beast_core 4 †Ravine → utility: every timer −5 % (ex Eternal Haste) |
| Azure Beast Ravine · **Beast-Tamer Yao** | beast_bait 20, beast_bone 20 → **Beast Pen** (5×5 roster of 3 tamed beasts fed fish or spirit_buns → beast_bone, rare beast_core) | beast_core 6, soulvine 20 → utility: **Ravine Automation** (bone heaps, soulvine) | soul_lamp 4, tribulation_essence 6 †Tribulation → utility: +1 damage per strike, Pen holds 5 (ex Battle Fury) |
| Tribulation Peak · **Tribulation Elder Mo** | immortal_peach 20, jade_shard 40 → utility: **Peak Automation** | tribulation_essence 10, soul_lamp 4 → **Immortal Furnace** (Burner 3×5: tribulation_essence 2 + immortal_peach 1 + soul_lamp 1 → immortal_pill) | immortal_pill 3 ‡, star_fragment 30 †Celestial → utility: blessings +20 %, dragon scales ×1.5 (ex Heaven's Favor) |
| Celestial Peak · **Star-Gazer Elder Xuan** | star_fragment 40, glass 20 → **Star Well** (generator: star_fragment / 10 s) | moonpetal 30, talisman 4 → utility: **Celestial Automation** | star_steel 10, tribulation_essence 10 → utility: Heavenly Gate cost −25 % |

13 building rewards, 20 utilities. Dropped perks with no successor: Frugal Frontier, Ascendant Insight, Remembered
Paths, Legacy Automation (they only made sense with resets).

### 5. Removed systems and replacements

| Removed | Its job | Replaced by |
|---|---|---|
| Ascension reset, `ascensions`, `ascendPrompt`, PrestigeSystem | long-term goal, replay | 11 Islands, 33 requests, one ending, free play after |
| Ascension Points, Offerings, `GATE_OFFERINGS` | reward for depth | utilities (earned in-world, permanent) |
| Perk shop, `PERKS` | quality of life | requester utilities (§4) |
| Vows, `VOWS`, `VOW_MULT`, vow marks | optional challenge | none (owner: remove completely) |
| Island steles, `WORLD.unlockCost`, `unlockSide`, ring/any-order unlocks | Island gating | Sky-Bridge Shrine, fixed order (§1) |
| Ascend dialog, post-ascension card, vow rows, AP on HUD/MetaBar | prestige UI | Shrine panel, Request panel, "requests 12/33" in Stats |
| Ascension Gate, "Ascend" milestone | ending | **Heavenly Gate** (REVEAL: `celestial` unlocked + Dragon 4); milestones "Tribute → <Island>", "Raise the Heavenly Gate" |

**Ending:** the awakened Dragon stays a mid-game moment (keeps its −10 % speed, no modal). The Heavenly Gate costs
talisman 14, star_steel 20, dragon_scale 6, tribulation_essence 10 (Xuan C: −25 %), is buildable only on Celestial
Peak, reuses the Ascension Gate art; "Ascend to Immortality" plays the ending, then the world continues as free play
(all systems keep running; unfinished requests stay open).

### 6. Pacing (one continuous game) and how the bot verifies it

Target at human pacing with a greedy request policy: **ending ≈ 3:00 (accepted band 2:30–3:30)**. Tribute timestamps
(first guesses): Mine 0:08 · Fishing 0:20 · Farm 0:32 · Grove 0:48 · Volcano 1:05 · Lotus 1:25 · Thunder 1:45 ·
Ravine 2:05 · Tribulation 2:25 · Celestial 2:40 · Gate 3:00; Dragon 3 ≈ 1:10, Dragon 4 ≈ 2:20. Completionist
(33/33) ≈ 4 h. Playthrough bot (`HeadlessPlaythrough`), human-pacing mode, two policies: **greedy** (takes a request
when its reward is estimated to save more time than it costs) must end in 2:30–3:30; **no-requests** must still end
(≤ 4:15) — the proof that requests are optional. Both assert: Tributes paid strictly in order; every Island gap
8–30 min; automation ≥ 5× hand rate mid-game (ADR 0004); no Tribute item needed before its source Island. The bot
logs per-Island, per-request and Dragon timestamps; balance retunes quantities, never the order.

## Consequences

**Data (game-data.json → importer → GameDatabase):** `AREAS` keys in unlock order (center, mine, fishing, farm, grove,
volcano, lotus, thunder, ravine, tribulation, celestial) with 4 new areas; `WORLD.islands` per §2; delete
`unlockCost`, `unlockSide`, `PERKS`, `VOWS`, `VOW_MULT`, `GATE_OFFERINGS`; 12 items in `ITEM_NAMES`/`ITEM_ICONS`/
`SOURCES`, `FUEL.thunderwood`; 31 new `BUILDINGS` (6 on-unlock, 13 rewards, `sky_shrine`, 11 `requester:true` NPC types);
`ascension_gate` → `heavenly_gate` (`ending:true`, `area:"celestial"`); REVEAL kinds `{"request": id}` and
`{"region": id}`; quests/reveals that assumed the old order (`waters` → loom/mill/brewery, `explore`) retargeted.

```json
"SHRINE": { "building": "sky_shrine", "anchor": { "area": "center", "x": 44, "y": 1 },
  "order": ["mine", "fishing", "farm", "grove", "volcano", "lotus", "thunder", "ravine", "tribulation", "celestial"],
  "tributes": { "mine": { "needs": { "wood": 20, "leaves": 15, "stone": 20, "plank": 10, "brick": 10 },
                          "pathFrom": "center" } } },
"REQUESTERS": { "kang": { "name": "Foreman Kang", "area": "mine", "sprite": "npc_kang", "portrait": "npc_kang_portrait",
  "anchor": { "x": 30, "y": 12 }, "line": "The deep veins sing, if you can hear them.",
  "requests": [
    { "id": "kang_shaft", "needs": { "iron_ore": 40, "tools": 4 }, "reward": { "reveal": ["deep_shaft"] } },
    { "id": "kang_auto",  "needs": { "stone": 100, "clay": 50 },   "reward": { "bonus": { "autoboost": 1 } } },
    { "id": "kang_forge", "needs": { "iron_bar": 30, "charcoal": 30 }, "reward": { "reveal": ["blast_forge"] } } ] } }
```
`SHRINE.order` is authoritative; an automation utility is `{"bonus": {"auto_<area>": 2}}`.

**Sim:** new `RequesterSystem` (state `requests { done[], paid{id→ItemCounts} }`; `Feed` via `Hand.FeedNeeds`,
`Complete` → reward, event `RequestFulfilled`, `Bonus(key)`); every perk wiring point (Timing, Hand.Cap, Pavilion,
Logistics, Fuel, Automation, Node, FieldGenerator, Dragon, Combat) reads `Requests.Bonus(key)`. ProgressionSystem:
`ShrineSystem` feed replaces steles; `NextIsland`; milestones. AutomationSystem gains targets for Fishing, Grove,
Volcano, Celestial and the 4 new Islands. CombatSystem: per-Island enemies (Azure Wolf). Beast Pen generalises the
Pavilion roster. Delete PrestigeSystem; Timing drops ascension/haste-perk/vow terms; Dragon stage 4 no longer ends.

**Save migration (SaveCodec version bump):** drop `ascensions`, `ascendPoints`, `perks`, `vows`, `justAscended`,
`perkShopSeen`, `ascendPrompt`, `offerings` (paid offerings refunded as ground items by the Altar). Each owned perk
with a successor marks that request **done** (gifted). Unlocked Islands stay unlocked even out of order (Sky Paths
appear); the Shrine offers the first still-locked Island in `order`. In-progress `unlockPaid` becomes Shrine paid
(capped at the new needs, rest refunded). A built or ghost Ascension Gate is removed and its paid cost refunded at
its cell (the Heavenly Gate must be built on Celestial). Missing AreaStates are added (locked). NPCs and the Shrine
are inserted at their anchors (blocking buildings nudged to the nearest free cells). Celestial's move is a world-
offset change only. Recipe indices of existing buildings are untouched.

**UI:** Shrine panel, Request panel (portrait, line, 3 cards), NPC request marker, Sky Path reveal, ending flow on
the Gate. Delete AscendDialogView, PerkShopView, PostAscensionCardView, VowRowView and the AP parts of MetaBar/
MetaText/HelpText/QuestPanel/GateFaceView. **Docs:** CONTEXT.md (11 Islands, Shrine, Requester, Sky Path; Offering
removed), ART-SPEC §3.7 (≈140 new keys; perk/vow/AP art retired).

## Implementation order

1. **Data + removal:** tables above, importer, delete perks/vows/offerings/Prestige + their UI, Heavenly Gate ending,
   SaveCodec migration + tests. The game stays playable on 7 Islands (old order until step 2).
2. **Shrine:** Sky-Bridge Shrine, fixed order, Celestial north, Sky Paths, quest/milestone retarget, ShrineTests.
3. **Requesters (7 existing Islands):** RequesterSystem, Request panel, feeding, 7 NPCs, their 21 rewards, bonus
   wiring, RequesterTests (select/installments/rewards/bonus/save round-trip/veteran migration).
4. **New Islands:** 4 AREAS in IslandsBuilder (after the Game.unity split, M5), nodes, Azure Wolf, 6 on-unlock and
   3 reward buildings, 4 NPCs and their 12 requests, 12 items — placeholder art first.
5. **Balance:** both bot policies, retune §1/§4 quantities to §6 bands; update ADR 0004 numbers.
6. **Art:** integrate ART-SPEC §3.7 as batches arrive (P1 Shrine + 7 NPCs first).

## Risks

- **Length:** 4 more Islands push the ending from ~2:10 to ~3:00; if the bot overshoots, cut Tribute quantities and
  Dragon 4 needs before cutting content.
- **Hand-only raw items** in Tributes 6–10 (obsidian, reed, soulvine, …) may feel like chores if the player skips
  requests; keep them ≤ 20 or swap for converter outputs.
- **Scene size / layout:** 11 Islands and negative world rows; IslandsBuilder, camera bounds and sky must handle them.
- **Shrine placement** collides with the Spirit Tree's `midTop` zone and existing saves' buildings (nudge logic).
- **Art volume** (≈140 keys) is the long pole; ship with placeholders, P1 first.
- **‡ requests** (He C, Mo C) need the same NPC's building — the panel must show why they wait.
