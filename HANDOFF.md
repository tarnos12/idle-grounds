# HANDOFF — Idle Grounds (Unity port)

## ▶ NEXT SESSION: start here

The Unity project (6000.3.12f1, URP, 2D) now lives at the repo root next to
`old-game/` (the v52 web prototype — reference only; history in
`old-game/HANDOFF.md`). It was copied in from `D:/WORK/Unity/idle grounds`
(with a space); the user reopens the repo folder in Unity Hub and deletes the
old copy once happy.

Unity MCP is registered in `.mcp.json` (shared server on 8080, also serves
another project) — pin our instance with `set_active_instance` first. If the
Unity tools aren't visible, the session started before `.mcp.json` existed:
restart it.

Next: confirm the Editor is connected from the repo path, then plan the port
from `old-game/DESIGN.md`. Work on `master` by default (feature branches only
when they make sense).

## Last session summary (2026-10-04)

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
