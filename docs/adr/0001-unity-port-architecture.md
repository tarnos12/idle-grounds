# ADR 0001 — Unity port architecture

Status: accepted (2026-10-04)

## Context

`old-game/` is a ~8k-line vanilla-JS idle game: `data.js` (config),
`engine.js` (DOM-free simulation), `state.js` (save + migrations), `ui.js`
(canvas render + input). We port it to Unity 6000.3 (URP 2D) faithfully, and
the user wants a *real* Unity project: scenes, scene objects, prefabs,
ScriptableObjects — not a game generated entirely from code.

Behaviour specs: `docs/port/engine-systems.md`, `docs/port/data-catalog.md`,
`docs/port/ui-input-render.md`.

## Decision

### Assemblies (`Assets/_Project/Scripts/`)

| asmdef | Folder | References | Contents |
|---|---|---|---|
| `IdleGrounds.Sim` | `Sim/` | none (`noEngineReferences: true`) | Pure C# simulation: config POCOs, `GameState`, systems, `Simulation` facade, save model. Deterministic: injected `IClock` + seeded `IRng`. |
| `IdleGrounds.Sim.Tests` | `Tests/Sim/` | Sim, NUnit | EditMode tests on hand-built configs. |
| `IdleGrounds.Game` | `Game/` | Sim, Unity.InputSystem, Unity.TextMeshPro, UnityEngine.UI | ScriptableObject authoring, `GameRunner`, views, input, UI, audio, save I/O. |
| `IdleGrounds.Game.Tests` | `Tests/Game/` | Game, Sim | EditMode tests that load the real `GameDatabase.asset` (full-config parity tests). |
| `IdleGrounds.Editor` | `Editor/` | Game, Sim | Importers, scene/world builders, menu items. |

### Simulation (the deep module)

`Simulation` is the single interface the Unity layer talks to: `Tick()`
(50 ms cadence), `AutomationTick()` (1 s), player commands (`Harvest`,
`RightClick`, `Suction`, `PlaceGhost`, `Demolish`, `SetRecipe`, `AddLink`, …)
and a `SimEvents` object for presentation (drops, SFX, spawns, wisps, dragon,
quests). Internally one class per system in the order of engine-systems §2.3.

- **Units:** the sim keeps the JS's area-local **pixel** coordinates
  (32 px/cell) and **millisecond** times so every constant ports verbatim. The
  view maps px → world units (1 cell = 1 unit, +y up).
- **Ordering:** ordered collections everywhere the JS relies on key order.
- **Events are suppressed during offline replay.**

### Data

- Source of truth at authoring time: **ScriptableObject assets** in
  `Assets/_Project/Data/` (`ItemDef`, `BuildingDef` (+ recipes), `RegionDef`
  (spawners/fixtures/generators/enemies), `UpgradeNodeDef`, `PerkDef`,
  `VowDef`, `QuestDef`, `DragonStageDef`, `GameBalance`) referenced by one
  `GameDatabase` asset.
- Each SO wraps a `[Serializable]` POCO from `IdleGrounds.Sim` (string keys
  between defs, lists not dictionaries) plus Unity-only fields (sprites).
  `GameDatabase.BuildConfig()` → `GameConfig` for the sim.
- Initial import: `tools/data-export/export.js` dumps `data.js` to
  `Assets/_Project/Data/Source/game-data.json`; the editor importer
  (`Idle Grounds/Data/Import From JSON`) creates/updates the SO assets.
  Quest goals (JS lambdas) become a `QuestGoalKind` enum + params.

### Scenes, GameObjects, prefabs

- `Assets/_Project/Scenes/Game.unity` — the playable scene:
  - `--- Systems`: `GameRunner` (owns `Simulation`, tick accumulator,
    autosave), `InputRouter`, `AudioService`, `SaveService`.
  - `Main Camera` + `CameraController` (pan/zoom/clamp).
  - `World` (`Grid`): one **Region** GameObject per region placed at its
    world position, each with a ground **Tilemap**, zone markers (gizmos),
    locked-region veil and unlock sign. Static, visible and editable in the
    Scene view; built by `Idle Grounds/World/Build Regions` from config.
  - `Runtime` containers for pooled dynamic entities.
  - `UI` — uGUI Canvas (+ TextMeshPro) with panels as prefabs.
- Prefabs (`Assets/_Project/Prefabs/`): `GroundItem`, `Node`, `Fixture`,
  `Enemy`, `Wisp`, `Building` base + variants per building family
  (Converter, Burner, Storehouse, GatheringStone, WispLantern, WardingSeal,
  Pavilion, Generator, AscensionGate, Altar, Dragon), FX (`FloatingText`,
  `SparkBurst`), UI panels.
- Views pull sim state in `LateUpdate` (as the JS renderer did) and use
  `SimEvents` for one-shot FX/SFX. Dynamic entities are pooled.

### Input

Input System asset `Assets/_Project/Input/IdleGroundsControls.inputactions`
with `Gameplay` (Primary/Secondary hold, Point, Pan, Sprint toggle, Zoom,
RotateHand, Build, Cancel) and `UI` maps. Hold/latch/ramp state machines from
engine-systems §5.6–5.7 live in `HandController`, frame-rate independent.

### Art

Placeholder sprites from the emoji atlas (`Art/Sprites/Emoji`), item PNGs from
`old-game/assets/icons` where present. Real art replaces sprites per def.

### Saves

JSON (Newtonsoft, `com.unity.nuget.newtonsoft-json`) at
`Application.persistentDataPath/idle-grounds-save.json` with
`schemaVersion`; sanitising load rules from engine-systems §1.5; a failed
load backs the file up instead of overwriting it.

## Consequences

- Sim logic is testable without Play mode and replayable offline.
- Two layers to keep in sync (sim state ↔ views) — mitigated by pull-based
  views keyed by entity id.
- `TEST` fast mode is a `GameBalance` flag (default on, as in v52).
