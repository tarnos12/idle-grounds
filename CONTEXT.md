# Idle Grounds

A cultivation-themed sandbox idle game: the player gathers physical items by hand across a continuous world, builds and feeds buildings, and automates hauling with spirit wisps on the way to awakening the Sleeping Dragon and raising the Heavenly Gate — one continuous game with no reset; free play continues after the ending (ADR 0007).

## Language

### World

**Island**:
One of the seven unlockable 93×93-cell floating landmasses of the world (Center, Farm, Mine, Fishing, Volcano, Spirit Grove, Celestial Peak), separated by open sky.
_Avoid_: Region, area, map, level

**Zone**:
A named rectangle of cells inside an Island (e.g. centre, a corner, a generator field) that scopes where things spawn or may be built.
_Avoid_: Field (except in a zone's own name), area

**Center**:
The starting Island — never the Altar building.

### Buildings

**Altar**:
The upgrade-shrine building in the Center where the player selects and pays for upgrades.
_Avoid_: Center (as a building name)

**Converter**:
A building that turns its input stock into output items according to its selected recipe.
_Avoid_: Producer, machine, crafter

**Burner**:
A Converter that must also burn fuel from its fuel rack to run (Forge, Kiln, Pill Furnace, Star Anvil).

**Generator building**:
A building that produces an item from nothing on a timer (Algae Farm, Herb Garden).
_Avoid_: Producer

**Heavenly Gate**:
The ending building: buildable only on Celestial Peak once that Island is open and the Sleeping Dragon is awake; when built, "Ascend to Immortality" plays the ending and the world continues as free play (nothing resets).
_Avoid_: Ascension Gate (retired), portal

### Logistics

**Spirit Bridge**:
A building paired one-to-one with a Spirit Bridge on a different Island; each pair is one-way, with wisps carrying items across the sky from the sending bridge to the receiving bridge.
_Avoid_: Spirit Vault, portal

### Paying into sinks

**Tribute**:
Items fed to the Sleeping Dragon to advance its stage.

**Cost**:
The items required to construct a ghost building, unlock an Island, or buy an Altar upgrade level; may be paid in installments.
_Avoid_: Price, needs

**Needs**:
The still-unpaid remainder of a Tribute or Cost — not a concept of its own.

### Progression

**Ending**:
Choosing "Ascend to Immortality" at the built Heavenly Gate; it plays once (it can be replayed from the gate) and the game continues as free play.
_Avoid_: Win (the awakened Dragon is a mid-game moment, not the ending)

**Utility bonus**:
A permanent, named effect (e.g. faster timers, extra carry) that a requester will grant (ADR 0007 §4); until requesters exist, only saves migrated from the ascension era carry them, converted from the perks they owned.
_Avoid_: Perk

### Retired (ADR 0007)

**Ascension**, **Ascension Points (AP)**, **Perk**, **Vow**, **Offering**, **Ascension Gate**: the old reset-and-replay loop. Removed from the game; old saves are migrated (perks become utility bonuses, a built Ascension Gate is refunded as ground items). Do not use these words for current mechanics.
