# Writing guide (Writer)

Agent: `.claude/agents/writer.md` (sonnet).

## Purpose
Own all player-facing words and the light story: quest and hint text, Help/Welcome, tooltips, item and building descriptions, the 11 requesters and their request text, the Dragon and the ending. Guard the vocabulary in `CONTEXT.md`.

## Current state of this game
- **Premise:** a cultivation sect's grounds on floating Islands; the player gathers by hand, builds, automates with spirit wisps, wakes the Sleeping Dragon and raises the Heavenly Gate. One continuous game; free play after the ending.
- **Text lives in:** `game-data.json` (`QUESTS`, `QUEST_CHAIN`, item names, building names; `REQUESTERS` lines once added), `Assets/_Project/Scripts/Game/UI/Meta/HelpText.cs`, `MetaText.cs`, `Progression/QuestPanelView.cs`, tooltips in views. English only.
- **Vocabulary (CONTEXT.md):** Island, Zone, Center (the Island, never the Altar), Altar, Converter, Burner, Generator building, Heavenly Gate, Spirit Bridge, Tribute (to the Dragon or the Shrine), Cost, Needs, Ending, Utility bonus. **Retired:** Ascension, Ascension Points (AP), Perk, Vow, Offering, Ascension Gate. Terms still to add to CONTEXT.md: Sky-Bridge Shrine, Sky Path, Requester.
- **The 11 requesters** (name, Island, role): Steward Lin (Center, outer-sect steward), Foreman Kang (Mine), Fisher-Hermit Jiang (Fishing), Rice Immortal Tian (Farm), Abbess Qingyun (Spirit Grove), Flame Smith Ouyang (Volcano), Lotus Maiden He (Lotus Marsh), Storm Sage Lei (Thunder Crag), Beast-Tamer Yao (Azure Beast Ravine), Tribulation Elder Mo (Tribulation Peak), Star-Gazer Elder Xuan (Celestial). 3 requests each (33), full table with items and rewards in ADR 0007 section 4. Sample greeting in the ADR: Foreman Kang, "The deep veins sing, if you can hear them."
- **Tone** (ART-SPEC 1.1): calm, serene, a little humorous; handmade sect workshops; no blood, nothing grim.
- Light story beats: the Dragon sleeps (stages 1-4, awakened mid-game, keeps its -10 % speed buff, no modal); the Shrine asks for tribute to raise Sky Paths; requesters offer help for goods; at the Gate, "Ascend to Immortality" plays the ending, then the world carries on.

## Rules to enforce
1. Use CONTEXT.md words; zero retired words in player-facing text (Grep after each edit).
2. Hints one line (about 70 characters), tooltips two lines, NPC lines one sentence, plain words.
3. Each NPC has a distinct voice that fits their Island (see the agent file); no snark, no urgency pressure.
4. A request names what is wanted and what it gives (building or named utility).
5. Waiting requests say why: "†" needs an item from a later Island (it can wait), "‡" needs this NPC's own building first.
6. Say "right-click to feed" wherever items are accepted.
7. Do not change ids; change display names only; tell the world designer.
8. The ending is quiet and says the world keeps going.

## Checklists
- **New string:** where it shows, max length, tone check, retired-word check, glossary check.
- **New NPC:** name, title, greeting, 3 request blurbs, reward text, speech-bubble hint, portrait caption.
- **Terminology sweep:** Grep for ascen, perk, vow, offering, " AP", region, "unlock stele" in `Assets`, `docs/guides`, Hub sources.
- **Quest chain:** each quest's title, one-line goal, completion toast, hint.

## Open issues
- CONTEXT.md lacks Sky-Bridge Shrine, Sky Path, Requester (HANDOFF note).
- Quest text still refers to old Island unlocks ("waters", "explore").
- No ending text written yet; no per-NPC voice samples beyond Kang's line.
- Help text may still mention retired systems; run the sweep.

## Owner feedback that constrains this role
- `owner-picks-2026-10-12.json`: "ascension is not fun, lets remove it" and the vows removal in `-requesters`: never reintroduce those words or ideas.
- Requesters are NPCs with items "you select from menu"; "unlock new recipes/buildings"; each NPC unlocks a new building or a utility.
