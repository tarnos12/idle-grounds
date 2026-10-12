# ADR 0007 — Island requesters and the Sky-Bridge Shrine replace ascension

Status: proposed (2026-10-12) — supersedes the ascension parts of ADR 0004 and all of ADR 0006 (rejected);
changes the Island-unlock part of ADR 0003

## Context

Owner (2026-10-12): "ascension is not fun, lets remove it. The other game does it by having various
buildings/npcs on each island that request certain items (you select from menu) that unlock new recipes."
Follow-up constraints (binding): requests do **not** gate Island progression; Islands unlock by paying a
**Tribute at a magical shrine in the north** of the map, which also locks the final Island; each Island's
Tribute is **5 different items × 5–20**, chosen so automation is the reliable way to gather them; **each NPC
unlocks a new building producing something new/related to its Island, or a utility** (more automation).

Today a run is ~2:10 (bot, human pacing): Dragon stage 4 at 1:17, Gate at 2:08, then ascend → reset with AP,
perks and vows. Islands unlock at steles on their edge for wood 50–150 / iron_bar 60 / mixed costs. Fishing,
Grove, Volcano and Celestial have no automation at all (the Altar tree only automates Center, Farm, Mine).

## Decision

**One continuous game, no reset.** Three layers: the **Dragon** (Center, 4 Tribute stages, unchanged — the main
spine), the **Sky-Bridge Shrine** (Island unlocks), and **Requesters** (optional NPCs that unlock buildings and
utilities). The Altar upgrade tree stays as it is. The game ends at the **Heavenly Gate** on the final Island.

### 1. Sky-Bridge Shrine (replaces unlock steles and `WORLD.unlockCost`)

- A fixed, indestructible 5×5 building on the **north edge of the Center** (pre-placed like the Altar/Dragon).
- **Celestial Peak moves north of the Center** (slot (1,−1); it was (1,2) at the bottom). The Shrine stands at
  the head of the only path to it, so it visibly "locks the final Island".
- Click → Tribute menu: one card per still-locked Island (5 item rows with have/need, art preview). Pick one,
  then right-click-feed in installments (same code as Dragon/Altar `FeedNeeds`); wisp lanterns may also feed it.
- Paying a Tribute in full raises a **Sky Path** — a glowing jade bridge from the Center (Celestial: from the
  Shrine itself) to that Island — and unlocks it. Sky Paths are **visual only**; Spirit Bridges (ADR 0003
  logistics pairs) are unchanged and still the way items cross the sky.
- Order (owner question `tribute_order`, recommended "rings"): Ring 1 Mine/Fishing/Farm open from the start;
  Ring 2 Grove/Volcano after any 2 of Ring 1; Celestial after all six others.

| Island | Tribute (5 items) | Why automation |
|---|---|---|
| Mine | wood 20, stone 20, clay 10, leaves 15, plank 5 | starter fields + Gathering Stones |
| Fishing | wood 20, stone 15, plank 10, brick 10, charcoal 5 | Kiln/Charcoal Pit fed by lanterns |
| Farm | wood 20, leaves 20, clay 15, brick 10, spirit_essence 5 | Center automation, foxes |
| Grove | iron_bar 10, fish 15, wheat 20, rope 5, water 20 | three Islands' lines + a Spirit Bridge |
| Volcano | iron_bar 20, brick 20, tools 5, algae 15, cotton 15 | Forge line, Algae Farm |
| Celestial (final) | obsidian 20, spirit_herb 20, spirit_stone 15, jade 10, firestone 10 | Herb Garden, Infusion, Volcano veins |

Tributes total far less than today's unlock costs (wood 50–150, iron_bar 60, obsidian 60): balance retunes
quantities inside the owner's 5–20 band by **item depth** (Ring 2+/final items are converter outputs), and the
Dragon stays the long pole. All values are first guesses for the bot to tune.

### 2. Requesters

A requester is a pre-placed NPC (sprite + small prop: stall, hut, boat) on an Island, indestructible, present
once its Island is unlocked. **2 per Island, 1 on Celestial = 13 NPCs, 3 requests each = 39 requests.**

- **Menu:** click the NPC → Request panel: portrait, one line of dialogue, its 3 request cards (needs with
  have/need, reward preview, lock reason). Requests I and II are open at once (the player chooses); III (the
  master's request) opens after both. Pick one → it becomes that NPC's active request; switching keeps paid
  installments per request. Several NPCs can have active requests at once.
- **Delivery:** right-click-feed in installments (as Dragon/Altar) **and** wisp lanterns may link to an NPC
  (it accepts only what its active request still needs) — requests become a logistics goal, not a hand chore.
- **Rewards:** I = usually a **new building** for that Island, II = a **utility**, III = the bigger one. New
  buildings appear in the build menu (REVEAL kind `{ "request": id }`); utilities are named bonuses.
- Optional: no Island, Dragon stage or the ending needs any request. They are the shortcuts that make the
  late game faster and the automation for the four Islands that have none today.

| Island · NPC | I (open) | II (open) | III (after I + II) |
|---|---|---|---|
| Center · **Steward Lin**, outer-sect steward | wood 60, plank 20 → **Bamboo Nursery** (generator: bamboo / 8 s, cap 12) | brick 20, paper 10 → utility: lanterns hold +2 links | spirit_stone 10, cloth 10, paper 20 → utility: +5 carry, Storehouses +50 % (ex Fleet Hands) |
| Center · **Beast-Tamer Yao** (fox glade) | spirit_essence 15, stone 30 → **Spirit Snare** (1×1 in the beast zone, auto-strikes foxes 1/s; automates essence) | beast_bait 10, spirit_buns 15 → **Beast Pen** (roster of 3 tamed boars, fed fish/buns → beast_bone + essence) | beast_bone 20, iron_bar 15 → utility: Pen holds 5, +1 strike damage (ex Battle Fury) |
| Mine · **Foreman Kang** | iron_ore 40, tools 4 → **Deep Shaft** (generator: iron_ore / 8 s, cap 20) | stone 100, clay 50 → utility: every Automation +1 node per tick (ex Keen Automation) | iron_bar 30, brick 30 → **Blast Forge** (Burner: iron_ore 3 + charcoal 1 → iron_bar 3) |
| Mine · **Jade Hermit Shi** | jade_shard 30, water 20 → **Jade Spring** (generator: jade_shard / 12 s) | jade 10, spirit_essence 10 → utility: Gathering Stone radius +50 %, nodes respawn +10 % (ex Deep Roots) | spirit_stone 12, jade 12 → **Spirit Jade Grotto** (converter: jade 1 + spirit_stone 1 → spirit_jade 1, no essence) |
| Fishing · **Fisher-Hermit Jiang** (straight hook) | fish 40, rope 6 → **Koi Pond** (water-only generator: fish / 6 s) | algae 60, plank 20 → utility: **Fishing Automation** (auto-reels surfacing fish/algae; Altar levels 2–3 added) | vitality_pill 6, spirit_jade 3 → **Bait Kitchen** (converter: fish 1 + algae 1 → beast_bait 2) |
| Fishing · **Ferryman Bo** | plank 30, rope 10 → utility: Spirit Bridges carry +2 per sky wisp | fish 60, algae 40 → utility: wisps fly/send +15 % (ex Tireless Wisps) | glass 15, spirit_stone 10 → **Sky Ferry Pier** (a Spirit Bridge endpoint with buffer 40 that sends twice per beat) |
| Farm · **Rice Immortal Tian** | wheat 80, water 40 → **Spirit Paddy** (generator: wheat / 6 s, rare spirit_herb) | flour 30, spirit_buns 10 → utility: field generators +10 % (ex Astral Bounty) | spirit_wine 10, spirit_herb 50 → **Elixir Still** (converter: spirit_herb 2 + spirit_wine 1 → qi_elixir 2, no essence) |
| Farm · **Weaver Mei** | cotton 60, plank 20 → **Cotton Terrace** (generator: cotton / 6 s) | cloth 30, rope 10 → utility: lantern link range +50 % | robe 8, spirit_jade 2 → **Silk Hall** (converter: cotton 3 + spirit_herb 1 → robe 1; cotton 2 + water 1 → paper 2) |
| Grove · **Abbess Qingyun** | robe 6, spirit_buns 20 → utility: +1 disciple per Pavilion (ex Master's Hall) | spirit_herb 80, bamboo 40 → utility: **Grove Automation** (herbs, bamboo) | qi_elixir 10, spirit_jade 4 → **Essence Well** (converter: spirit_herb 3 + water 1 → spirit_essence 2) |
| Grove · **Pill Sage Hua** | qi_elixir 10, vitality_pill 4 → utility: blessings +20 % (ex Heaven's Favor) | ember_pill 2, verdant_pill 2 → utility: Pill Furnace makes 2 pills per batch | spirit_jade 6, spirit_herb 30, qi_elixir 20 → **Foundation Cauldron** (Burner: qi_elixir 2 + spirit_jade 1 + spirit_herb 3 → `foundation_pill`, new item) |
| Volcano · **Flame Smith Ouyang** | iron_bar 30, firestone 10 → **Obsidian Kiln** (Burner: sand 2 + obsidian 1 → glass 4) | obsidian 30, charcoal 30 → utility: Burners use 15 % less fuel (ex Ember Heart) | star_steel 4, tools 10 → **Star Crucible** (Burner: iron_bar 2 + obsidian 2 + firestone 1 → star_steel 2, no beast_bone) |
| Volcano · **Fire Monk Huo** | firestone 15, qi_elixir 6 → **Fire Vent** (generator: firestone / 10 s) | brick 40, obsidian 20 → utility: **Volcano Automation** (obsidian, fire veins) | ember_pill 4, spirit_stone 15 → utility: Burners within 8 cells of a Fire Vent need no fuel |
| Celestial · **Star-Gazer Elder Xuan** | star_fragment 40, glass 20 → **Star Well** (generator: star_fragment / 10 s) | moonpetal 30, talisman 4 → utility: all timers −5 % (ex Eternal Haste), dragon scales ×1.5 | star_steel 8, spirit_jade 6 → utility: **Celestial Automation** (star rocks, moon shrubs) |

New items: only `foundation_pill` (optional: the Heavenly Gate accepts it as a substitute, see §4). Every new
building is a recolour/variant of an existing footprint (generator 3×3, converter 3×3, Burner 3×5).

### 3. Unlock map (everything gated by ascension, perks, vows or the old unlock costs)

- **Island unlock costs + steles** → Shrine Tributes (§1). `unlockSide`, stele quest target → Shrine.
- **Ascension Gate** (stage 4 REVEAL) → **Heavenly Gate** on Celestial (REVEAL: Celestial unlocked + stage 4).
- **Perks** → requester utilities: hands, hall, slumber, autoboost, regrow, bounty, ember, bless, haste, fury
  (table above). **Dropped:** frugal (no repeat unlocks), apgain (no AP), paths and legacy (no reset).
- **Altar tree, Dragon stages and all quest/region/stage REVEALs**: stay unchanged. No recipe was ascension-
  gated; existing buildings keep their recipes (recipe indices untouched).

### 4. Removed, and what takes over each job

| Removed | Its job | Replaced by |
|---|---|---|
| Ascension reset, `ascensions`, `ascendPrompt`, PrestigeSystem | long-term goal, replay | requesters + Shrine; one ending |
| Ascension Points, Offerings, `GATE_OFFERINGS` | reward for depth | utilities (permanent, earned in-world) |
| Perk shop (☯ pill), `PERKS` | quality of life | requester utilities |
| Vows, `VOW_MULT`, vow marks | optional challenge | none in v1 (no reward to pay them with) |
| Ascend dialog, post-ascension card, vow rows, AP on HUD/MetaBar | prestige UI | Request panel, Shrine panel, "requests 12/39" in the stats panel |
| Milestone "RaiseGate/Ascend" | endgame pointer | milestones "Shrine Tribute → <Island>" and "Raise the Heavenly Gate" |

**Ending:** the awakened Dragon stays a big mid-game moment (keeps its −10 % speed, no longer the ending
modal). Building the **Heavenly Gate** (talisman 14, star_steel 20, dragon_scale 6; reuses Gate art) and
clicking "Ascend to Immortality" plays the true ending; the world then continues as free play.

### 5. Pacing (single game)

Main path ≈ today's run minus the ascend: Dragon 4 ≈ 1:15, Celestial ≈ 1:35, Gate ≈ 2:10 with no requests.
A typical player does ~half the requests on the way: +45–60 min of request goals, −20–30 min from shortcuts
(Deep Shaft/Blast Forge on iron, Elixir Still/Essence Well on essence, Star Crucible) → **ending ≈ 2:45–3:15**,
completionist (39/39) ≈ 4 h. Requests on each Island cost roughly one Tribute's depth, so they never block.

## Consequences

**Data (game-data.json):** new `REQUESTERS` table; new `SHRINE` table; `WORLD.islands.celestial` moves north;
`WORLD.unlockCost`/`unlockSide` deleted; ~16 new `BUILDINGS` (`requester:true` NPC types + reward buildings,
`gen`/converter as usual); REVEAL kind `{ "request": id }`; `ITEM_NAMES.foundation_pill`; delete `PERKS`,
`VOWS`, `VOW_MULT`, `GATE_OFFERINGS`; `ascension_gate` → `heavenly_gate` (`ending:true`).

```json
"REQUESTERS": { "kang": { "name": "Foreman Kang", "area": "mine", "sprite": "npc_kang",
  "anchor": { "x": 30, "y": 12 }, "line": "The deep veins sing, if you can hear them.",
  "requests": [
    { "id": "kang_shaft", "tier": 1, "needs": { "iron_ore": 40, "tools": 4 }, "reward": { "reveal": ["deep_shaft"] } },
    { "id": "kang_auto",  "tier": 1, "needs": { "stone": 100, "clay": 50 }, "reward": { "bonus": { "autoboost": 1 } } },
    { "id": "kang_forge", "tier": 2, "after": ["kang_shaft", "kang_auto"], "needs": { "iron_bar": 30, "brick": 30 },
      "reward": { "reveal": ["blast_forge"] } } ] } },
"SHRINE": { "order": "rings", "tributes": { "mine": { "ring": 1, "needs": { "wood": 20, "stone": 20, "clay": 10, "leaves": 15, "plank": 5 } } } }
```

**Sim:** new `RequesterSystem` (state `requests { done[], active{npc→id}, paid{id→ItemCounts} }`; `Select`,
`Feed` via `Hand.FeedNeeds`, `Complete` → apply reward, event `RequestFulfilled`, `Bonus(key)`). Every perk
wiring point (Timing, Hand.Cap, Pavilion, Logistics, Converter/Fuel, Automation, Node, FieldGenerator, Dragon,
Combat) reads `Requests.Bonus(key)` instead of `S.PerkLevel(id)`. ProgressionSystem: Shrine Tribute feed
replaces stele unlocks; ring rules; milestones. AutomationSystem gains Fishing/Grove/Volcano/Celestial
targets. LogisticsSystem: requester/Shrine as lantern targets (accept = remaining needs). Delete
PrestigeSystem; Timing drops ascension/haste-perk/vow-mark terms; `won` no longer triggers the ending.

**Save (SaveCodec, version bump):** drop `ascensions`, `ascendPoints`, `perks`, `vows`, `justAscended`,
`perkShopSeen`, `ascendPrompt`, `offerings` (refund paid offerings as ground items at the gate). Veteran saves:
each owned perk with a utility equivalent marks that request **done** (gifted, no Tribute). Already-unlocked
Islands stay unlocked (their Sky Path appears); in-progress `unlockPaid` installments become Shrine Tribute
paid (capped at the new needs, rest refunded). A built/ghost Ascension Gate becomes a Heavenly Gate. NPCs and
the Shrine are inserted on load at their anchors (nearest free cells; blocking buildings are nudged). Celestial
moving north is a world-offset change only (Island-local coordinates are untouched).

**UI:** Request panel (portrait, dialogue, 3 cards, Accept/active badge), Shrine Tribute panel (Island cards,
ring locks), speech-bubble icon over NPCs with an open request, Sky Path reveal animation, ending flow on the
Gate. Delete AscendDialogView, PerkShopView, PostAscensionCardView, VowRowView and the AP parts of
MetaBar/MetaText/HelpText/QuestPanel/GateFaceView.

**Art:** 13 NPC sprites (3/4 view, 2-frame idle) + 13 portraits + 13 small props; Shrine (5×5); Sky Path
bridge (tileable, glow); Celestial re-placement north (coast/underside unchanged); ~16 reward buildings as
recolour variants; `foundation_pill` icon. ≈ 60 pieces, about half of ADR 0006's ~110.

**Tests:** delete PrestigeTests and the multi-run bot modes; RequesterTests (select/switch/installments,
rewards, bonus wiring, lantern delivery, save round-trip, veteran migration); Shrine tests (ring rules,
Sky Path unlock, old `unlockPaid` migration). Playthrough bot plays the Shrine and a greedy subset of
requests and must reach the ending in **2:30–3:30** (human) with automation ≥ 5× mid-game (ADR 0004);
a "no requests" bot must still finish (proves requests are optional).
