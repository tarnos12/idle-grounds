# Unity port — milestone plan

Architecture: `docs/adr/0001-unity-port-architecture.md`. Behaviour specs:
`engine-systems.md`, `data-catalog.md`, `ui-input-render.md` (this folder).
Each milestone ends playable/verified in `Assets/_Project/Scenes/Game.unity`.

| # | Milestone | Sim (IdleGrounds.Sim) | Unity side (scene / prefabs / UI) | Status |
|---|---|---|---|---|
| 0 | Specs + placeholder art | — | emoji atlas sprites, data export | ✅ |
| 1 | Foundation | config POCOs + JSON loader, state, clock/RNG, periodic, world/zones/occupancy, nodes + harvest, ground, hand, field generators, tests | Game scene, Input actions, camera pan/zoom/clamp, 7 Region GameObjects (tilemaps, zones, veils) | ✅ |
| 2 | Core loop playable | Simulation facade wiring | ScriptableObject defs + importer, `GameRunner`, Node/Fixture/GroundItem prefabs + pooled views, `HandController` (hold/latch/ramp), hand cursor, HUD bottom bar | ✅ |
| 3 | Buildings & converters | ghosts, construction, demolish, placement, converters, recipes, fuel, output piles, storehouse | Building prefab variants, build menu, placement ghost, recipe picker, converter face (inputs/progress/fuel rack) | |
| 4 | Logistics | gathering stone, seal, lantern links, wisps, furnace spirit | Wisp/lantern prefabs, link editor, reach circles, status dots | |
| 5 | Progression | dragon tributes/stages/pills/scales, Altar upgrade tree, quests + milestones, reveal, region unlocks + installments | Dragon/Altar views, upgrade-tree panel, quest panel, unlock signs | |
| 6 | Combat & idle | enemies, beast bait, Martial Vigor, automation, disciples/pavilion, generator buildings | Enemy prefab + HP bars, pavilion panel | |
| 7 | Prestige, save, offline | ascension gate, AP, perks, vows, ascend reset, save/load + sanitising, offline replay | perk shop, vows, ascension card, welcome-back modal, stats, ending | |
| 8 | Juice & audio | — | floating numbers, spark bursts, 13 SFX, help, settings | |
| 9 | Parity pass | golden tests vs JS behaviours (spec §18.6) | side-by-side check against old-game | |
