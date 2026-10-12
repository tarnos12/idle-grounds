# ADR 0006 — Ascension unlocks content: Realm tiers, new Islands, harder Vows

Status: rejected (2026-10-12) — owner: "ascension is not fun, let's remove it"; replaced by island requesters (ADR 0007)

## Context

ADR 0004 removed the +20 % world speed per ascension: every run lasts ~2 h (bot: run 1 2:12, a
veteran no-vow run ≈ 1:54). The owner picked (2026-10-12) what ascending unlocks: **new Islands that
only appear after ascending**, **new buildings and higher-tier recipes**, and **harder Vows** worth
more AP. A permanent sect/home upgrade was not picked, so there is no new starting kit.

Today: 7 Islands on a 3×3 layout (Center start; Farm wood 50; Mine wood 80; Fishing wood 120;
Volcano iron_bar 60; Spirit Grove wood 150 + algae 100; Celestial Peak spirit_stone 40 + jade 20 +
obsidian 60). Slots (0,2) bottom-left and (2,2) bottom-right are empty. AP = (3 + 2·(Islands−1) +
apgain + offerings) × VOW_MULT[active vows] (1 / 1.15 / 1.3 / 1.5 / 1.75). Gate cost talisman 14,
star_steel 20, dragon_scale 6; offerings are 2 each of those 3 (+1 AP each). Long poles of a run
(balance-pass-1): Iron Bar batches at the Forge, Spirit Essence, dragon stage 4 (≈ 24 min), and the
gate chain (≈ 24 min).

## Decision

### 1. Realm tiers

`tier = min(ascensions, 4)`. Each tier is a cultivation realm shown on the ascension card and the
HUD: 0 Qi Condensation (run 1, as today), 1 Foundation Establishment, 2 Core Formation, 3 Nascent
Soul, 4 Heavenly Tribulation (endgame). Each tier adds **one Island, 2–3 buildings, 3 new items,
one new Gate item, and 2 Vows** (tiers 2–4). Before its tier an Island shows only as a distant
background silhouette (reuses the kept background-island art) — a visible promise, not a veil.

### 2. The ladder

| Tier (unlocks after) | Island, slot, biome | Unlock cost (mid-run) | Resources (nodes) | Signature buildings and recipes | New Gate item |
|---|---|---|---|---|---|
| 1 — Asc 1 | **Lotus Marsh**, (0,2) below Grove. Misty shallows, giant lotus pads, reed beds, stilt walkways. | plank 40, rope 20, algae 60 | lotus pads (surface, like koi) → `spirit_lotus`; reed beds (chop) → `reed` | **Lotus Pond** (generator, water only; spirit_lotus / 5 s, cap 24). **Lotus Distillery** (converter): spirit_lotus 2 + water 1 → qi_elixir 3; spirit_lotus 3 + qi_elixir 1 + spirit_stone 1 → `foundation_pill`. **Reed Press** (converter): reed 3 → paper 2; reed 2 → rope 1. | foundation_pill |
| 2 — Asc 2 | **Thunder Crag**, (2,2) below Volcano. Storm-black slate, crackling crystal spires, lightning-split trees. | iron_bar 40, glass 20, spirit_stone 20 | split trees (chop) → `thunderwood` (FUEL 30000, between charcoal and firestone); crystal spires (break) → `storm_crystal` | **Lightning Rod** (1×1 generator; storm_crystal / 8 s, cap 12; also the ward for the Tribulation Vow). **Thunder Forge** (Burner 3×5): iron_ore 3 + storm_crystal 1 → iron_bar 4 (6 s); star_steel 1 + storm_crystal 2 → `thunder_steel`. | thunder_steel |
| 3 — Asc 3 | **Azure Beast Ravine**, (3,1) right of Volcano. Mossy canyon, giant ribcages, beast dens, glowing vines. | star_steel 10, robe 10, beast_bait 10 | bone heaps (break) → beast_bone; soulvine (chop) → `soulvine`. Enemy: **Azure Wolf** (hp 12, pack of 2) → beast_bone + `beast_core` | **Beast Pen** (roster, like the Pavilion): tame up to 3 spirit beasts with beast_bait, feed koi / spirit_buns; produces beast_bone (+ rare beast_core). **Soul Refinery** (converter): beast_core 1 + soulvine 2 + spirit_jade 1 → `soul_lamp`. | soul_lamp |
| 4 — Asc 4 | **Tribulation Peak**, (1,3) below Celestial. Scorched jade terraces, rune circles, a permanent storm ring. | thunder_steel 10, soul_lamp 4, star_fragment 60 | peach trees (quarry fixture) → `immortal_peach`; scarred jade outcrops (break) → jade_shard 2–3 + rare spirit_jade | **Tribulation Array** (5×5): fed storm_crystal, it calls a heavenly strike every 30 s → 1 `tribulation_essence` per strike (generator in v1; the strike event is polish). **Immortal Furnace** (Burner 3×5): tribulation_essence 2 + immortal_peach 1 + soul_lamp 1 → `immortal_pill`. | immortal_pill |

Higher-tier recipes on old buildings (appended at the **end** of each recipe list so saved recipe
indices stay valid; gated by `minTier`): Loom reed 3 → cloth 2 (T1); Kiln storm_crystal 1 + sand 2
→ glass 4 (T2); Infusion Array beast_core 1 + stone 4 → spirit_stone 4 (T3); Cauldron immortal_peach 1
+ water 1 → qi_elixir 4 (T4).

### 3. Keeping runs ~2 h: new chains replace, not stack

- **Each tier's shortcut targets a measured long pole**: T1 Lotus replaces the herb/essence/water
  Qi Elixir grind and bamboo paper (talisman chain); T2 Thunder Forge replaces the Forge iron
  bottleneck (≈ 2.5 min of pure waiting per run, plus 35 bars by hand-fed batches) and thunderwood
  replaces charcoal; T3 Beast Pen replaces bait-luring Spirit Boars for beast_bone; T4 peaches feed
  qi_elixir and the Dragon.
- **The Gate keeps four cost types: dragon_scale 6 + the three newest tier items**, so old items
  drop out as new ones arrive:
  T0 talisman 14, star_steel 20 · T1 talisman 10, star_steel 14, foundation_pill 8 ·
  T2 talisman 8, foundation_pill 8, thunder_steel 10 · T3 talisman 8, thunder_steel 8, soul_lamp 8 ·
  T4 thunder_steel 6, soul_lamp 6, immortal_pill 6. Offerings = 2 of each cost type (cap 8 from T1).
- **Dragon stage 4** swaps part of its tribute for the newest island item from T2 on (e.g. T4:
  spirit_herb 450 → spirit_herb 200 + immortal_peach 40), so the newest Island is on the main path.
- Time budget per later run: −20 to −30 min (tutorial skipped, Remembered Paths / Legacy perks,
  tier shortcuts) and +20 to +30 min (unlock + set up the new Island, its Gate item). Target band
  per run **1:50–2:30** at human pacing, checked by the playthrough bot at every tier.
- Islands stay cumulative (a T4 run has 11 Islands); each extra Island is +2 AP, so AP per run
  grows ~21 → ~33 without vows. The perk shop (234 AP) then fills in ~8 runs instead of ~11.

### 4. Harder Vows

Switch VOW_MULT from "by count" to **per-vow weights**: mult = 1 + Σ weight (cap ×3.0). Existing
weights keep today's all-four total: Burden .15, Cold Hearth .15, Restless Dragon .2, Solitude .25.

| Unlocks | Vow | Rule | Weight |
|---|---|---|---|
| Asc 2 | Withered Fields | Field generators and generator buildings run 50 % slower. | +.25 |
| Asc 2 | Narrow Bridge | Spirit Bridges carry 1 item per sky wisp (instead of 5). | +.25 |
| Asc 3 | Wrathful Beasts | Beasts have ×3 HP and drop half; the Beast Pen holds 1. | +.20 |
| Asc 3 | Silent Altar | Altar upgrade costs ×2. | +.35 |
| Asc 4 | Forgotten Path | Remembered Paths and Legacy Automation perks are off this run. | +.30 |
| Asc 4 | Heavenly Tribulation | Every ~3 min lightning stalls one random converter for 60 s and scatters its output, unless a Lightning Rod is within 8 cells. | +.50 |

Each vow must cost ≥ 10 % run time on the bot (ADR 0004: no free AP); weights are retuned from the
measured cost. First completion still gives the vow mark (×0.99 timers).

### 5. After tier 4

Ascending at tier 4 plays the **true ending** (immortal ascension). Further ascensions stay at
tier 4; replay value comes from vows and the AP shop (post-game option is an owner question).

## Consequences

**Data (game-data.json → importer → GameDatabase):** 4 new `AREAS` (+ `WORLD.islands`, `unlockCost`,
`unlockSide`) with a new `minAscension`; ~9 new `BUILDINGS` (+ `REVEAL` kind `{ "tier": n }`);
12 new items in `ITEM_NAMES` / `ITEM_ICONS` / `SOURCES` (spirit_lotus, reed, foundation_pill,
thunderwood, storm_crystal, thunder_steel, soulvine, beast_core, soul_lamp, immortal_peach,
tribulation_essence, immortal_pill); `FUEL.thunderwood`; recipe `minTier`; `ascension_gate.costByTier`
and `GATE_OFFERINGS.itemsByTier`; `DRAGON_STAGES[3].needsByTier`; `VOWS` gain `minAscension` and
`weight` (VOW_MULT retired).

**Sim:** ProgressionSystem — `Tier`, region visibility/unlock gated by `minAscension`, tier REVEAL,
tiered Gate cost/offerings and AscendReward (weights). ConverterSystem hides recipes above the tier.
New: Beast Pen (generalise PavilionSystem's roster), Tribulation Array (generator first), the
Tribulation vow event. CombatSystem: per-Island enemies (today only Center has one).

**Save:** `ascensions` already persists. `GameState.CreateInitial` builds AreaStates for all regions;
loading an older save must add missing AreaStates (locked). Recipe indices are stable (append only).
Active vow ids are unchanged; weights apply on the next ascend.

**Scene/world:** IslandsBuilder places 4 more authored Islands (grid grows to 4×4 bounds); do this
after the Game.unity split (M5) so the scene stays under 100 MB.

**Art (ART-SPEC §4 additions):** per Island — ground tileset + macro patches, 3/4 south cliff and
side walls (recolour of existing sets allowed), underside dressing, 6–10 biome scatter pieces, 2–3
node sprites (+ depleted states), 2–3 buildings (3/4 view, 3×5 / 5×5 / 1×1), 3 item icons, a
background-silhouette variant; plus Azure Wolf + tamed-beast sprites, 6 vow icons, 4 realm badges.
≈ 25 pieces per Island, ~110 total — the largest cost of this ADR; tiers can ship one at a time.

**Tests:** the playthrough bot gains tier play (unlock the newest Island, build its chain, pay the
tiered Gate) and asserts each run 1:50–2:30 at tiers 0–4 and each new vow ≥ +10 % time.
