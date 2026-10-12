# Onboarding guide (Onboarding designer)

Agent: `.claude/agents/onboarding-designer.md` (sonnet).

## Purpose
Make sure a new player always knows what to do next and learns one idea at a time. Owns quest order, hints, tutorial timing, what is revealed when (with the world designer) and the structure of the Welcome/Help content. The writer owns the wording.

## Current state of this game
- **Quest chain v3: 17 quests** (`QUESTS` / `QUEST_CHAIN` in `game-data.json`), including island lessons "Bridge the sky", "Sky caravan", "Wine of three shores". `QuestHint` shows a next-step line; older saves remap by quest id. UI: `QuestPanelView`; the quest panel fades over steles.
- The chain was built for the *old* world: Islands unlocked by steles, quests named "waters" and "explore". ADR 0007 replaces steles with the Sky-Bridge Shrine, so quests and REVEAL rules that assume the old order must be retargeted (ADR 0007 Consequences).
- Playthrough reference (human pacing, old balance): wood/leaves at 0:02-0:04, Dragon stage 1 by 0:11, first building and Altar upgrade 0:14-0:41, Mine at 1:00, quest chain done at 5:23 in the first bot report. Longest tutorial steps were weaver (1:47) and iron (1:03).
- Existing aids: Welcome and Help modals (scroll skin), tooltips, the HUD Motion/Calm toggle, quest hints, juice (harvest chips, item arcs, vacuum magnet).
- **New things that need teaching** (ADR 0007): the Shrine and its Tribute panel; Sky Paths; the NPC speech-bubble marker; the Request panel (3 cards, pick any, installments kept); right-click feeding; Spirit Bridges now matter for Tributes; utilities earned from requests.

## Rules to enforce
1. One new mechanic per step; reveal by quest or REVEAL, never by a wall of text.
2. Hand work first: automation, wisps and bridges arrive after the hand limit is felt (ADR 0004: first 20-30 min are hand work).
3. Ignoring any optional system (requests, bridges) must never block the main path.
4. Every mechanic has a world cue, a one-line hint, a Help entry.
5. Feeding is right-click only; say so wherever items are accepted.
6. Teaching never relies on colour alone or on motion that reduce-motion hides (UI QA).

## Checklists
- **Fresh-start walk (first 30 min):** list every point where the next step is unclear. Target zero.
- **New mechanic:** trigger, cue, hint, Help entry, fallback if ignored, test the QuestHint line for done/not done states.
- **Chain end to Shrine hand-off:** the last quest points to the Shrine explicitly; the first Tribute is visible and payable by hand.
- **First request:** the NPC marker appears when its Island unlocks; the panel explains installments and that requests are optional.
- **Save from the old version:** quests remap; no player sees a finished tutorial replay.

## Open issues
- Chain not yet retargeted to the Shrine; "waters"/"explore" wording and triggers are stale.
- No teaching yet for Shrine, Sky Paths or requests (they do not exist in game yet).
- Emoji placeholders still appear in hints and Help.
- Long steps (weaver, iron) were the slowest in the bot report; check them with a human playtest.

## Owner feedback that constrains this role
- `owner-picks-2026-10-12-requesters.json`: requests are menu-driven, all open at once, right-click feeding only. Tell the player exactly that.
- `owner-picks-2026-10-12.json`: "we dont need to complete ALL requests on each island to move to the next". The tutorial must say requests are optional.
