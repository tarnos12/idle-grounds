# HANDOFF — Idle Grounds (Unity port)

## ▶ NEXT SESSION: start here

Full Unity port in progress. Port specs live in `docs/port/`:
`data-catalog.md` (every DATA table), `ui-input-render.md` (camera, input,
rendering, panels, FX, audio), `engine-systems.md` (simulation behaviour —
being written). Placeholder art: `Assets/_Project/Art/Sprites/Emoji/`
(180-sprite emoji atlas built by `node tools/emoji-atlas/build.js`, sliced
in Unity via `Idle Grounds/Art/Slice Emoji Atlas`).

Next: architecture (pure C# sim core + ScriptableObject defs + scene-authored
world + prefab views + uGUI/TMP HUD), then milestone 1 — Game scene with the
Center region, camera, hand, harvesting and ground items.

## Last session summary (2026-10-04, port kickoff)

- Connected MCP to the `idle-grounds` instance (the shared server also
  serves "Monster Catching Game 2D" — never touch it).
- Wrote port specs `docs/port/data-catalog.md` + `ui-input-render.md`.
- Emoji atlas tool + Unity slicer (`IdleGrounds.Editor` asmdef).

## Earlier session summary (2026-10-04)

- Moved the web prototype into `old-game/` (still runs:
  `node old-game/server.js` → http://localhost:5174).
- New root `CLAUDE.md`/`HANDOFF.md`; Matt Pocock skills config in
  `docs/agents/` (GitHub issues, default triage labels, single-context docs).
- Detected the user's Unity project via MCP for Unity (server 3.4.7, two
  Editors connected); copied `Assets/`, `Packages/`, `ProjectSettings/` into
  the repo root; added a Unity `.gitignore`; registered `unityMCP` in
  `.mcp.json`.
- Note: `.nojekyll` moved into `old-game/` — GitHub Pages (if used) serves
  the game at `/old-game/`.
