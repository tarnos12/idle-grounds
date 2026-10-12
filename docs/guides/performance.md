# Performance guide (Performance reviewer)

Agent: `.claude/agents/performance-reviewer.md` (sonnet).

## Purpose
Keep the game fast, light on memory and small to ship and store. Measure first, then report; fix only small clear problems unless asked.

## Current state of this game
- **GC targets (HANDOFF):** `Sim.Tick` is near-zero GC (about 0.9 allocations per tick). View queries on `Sim` are non-allocating (cached buffers) and views refresh every frame (throttles were dropped because they were no longer needed). Game code is about 1 allocation per frame; the Game View toolbar adds about 190 allocations per frame in the editor only.
- **Tests that guard it:** `SimTickAllocTests` and `NonAllocQueries_DoNotAllocate` are `UnityOnly` (profiler counters), so they only run inside Unity, not in the fast .NET gate. `NonAllocQueryTests.cs` covers the query side. The GC byte counter is process-wide, so the tests take the minimum over 5 windows (a one-off 12 KB flake came from editor / MCP noise).
- **Architecture helps:** `IdleGrounds.Sim` is pure C#; pooled Node, GroundItem and Wisp views; wisps and ground items are the main object counts. FlowLedger tracks produced / consumed / lost per item (check its cost with 11 Islands).
- **Size:**
  - `Game.unity` was split in 2026-10-12: 78 MB to 0.4 MB. Each Island's generated hierarchy is now a prefab in `Assets/_Project/Prefabs/Islands/Generated` at 10-12 MB each (11 Islands approximately 120-130 MB total; GitHub's limit is 100 MB **per file**, so each prefab is fine, but the repo grows). Menu `Idle Grounds/World/Save Islands To Prefabs`; builders wrap edits in `using (IslandPrefabs.Edit())`.
  - Builds (2026-10-09): Windows 152 MB (runs clean), WebGL 21 MB (gzip + decompression fallback). These **predate** the 3/4 look pass and the Island prefabs; both builds need a re-measure (plan M5: rebuild Windows + WebGL).
- **WebGL specifics:** single-threaded, no `File.Replace`, saves flushed to IndexedDB (`IdbSync.jslib`); keep GC pressure low, avoid big textures and per-frame allocation.
- **Save:** autosave every 5 s to `persistentDataPath/idle-grounds-save.json` (corrupt file becomes `.bak`); size grows with requests and Islands.
- **Frame time:** no hard budget exists yet; treat 16.6 ms (60 fps) as the target on a mid laptop and measure.

## Rules to enforce
1. No allocations in `Sim.Tick`, view syncs, `Update` or hot loops; no LINQ, no `foreach` over `List<T>` interfaces, no string building per frame, no `GetComponent` / `Find*` per frame.
2. New per-Island systems must scale linearly with 11 Islands and be pooled.
3. Textures: Point filter, no mipmaps for pixel art; consider atlases / Sprite Atlas (`unity:manage-sprite-atlas` skill) before adding hundreds of sprites; no oversized sources.
4. Keep each file < 100 MB and the scene tiny; report repo growth per PR.
5. Measure on a player build or the Profiler, not the editor overlay.
6. Report numbers, not feelings; keep the baseline table below current.

## Checklists
- **Per gameplay change:** run the fast tests; at a milestone run the UnityOnly alloc tests headlessly; compare Sim.Tick ms and GC.
- **Per world / art change:** draw calls, tilemap count, camera bounds at all 11 Island positions, sky tiling, prefab sizes (`ls -l` on prefabs), WebGL build size.
- **Per build:** build size, startup time, first-frame hitch, 10-minute soak (memory flat?).
- **Per new effect:** pooled? Calm-mode aware? per-frame cost.

## Baseline table (fill in as you measure)
| Metric | Last value | Date | Budget |
|---|---|---|---|
| Sim.Tick allocations per tick | about 0.9 | 2026-10-07 | near 0 |
| Game code allocations per frame | about 1 | 2026-10-07 | near 0 |
| Game.unity | 0.4 MB | 2026-10-12 | < 5 MB |
| Island prefab (each) | 10-12 MB | 2026-10-12 | report growth |
| Windows build | 152 MB (old) | 2026-10-09 | re-measure |
| WebGL build | 21 MB (old) | 2026-10-09 | re-measure |
| Frame time / FPS | not measured | | 16.6 ms proposed |

## Open issues
- Frame-time and memory baselines have never been recorded.
- Builds predate the 3/4 pass; Island prefab count will grow to 11.
- No headless Unity profiling path yet (waiting on the background copy).
- Camera underside limit and per-Island tilemaps under the new layout are unmeasured.

## Owner feedback that constrains this role
- `owner-picks-2026-10-12.json`: move generated Island dressing out of `Game.unity` (done) and "We need everything on GitHub": do not solve size by dropping files from git.
- `owner-picks-2026-10-12-team.json`: all measurement runs happen in the background copy.
