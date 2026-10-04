# HANDOFF — Idle Grounds (Unity port)

## ▶ NEXT SESSION: start here

The repo was restructured for the Unity port. The web prototype (last version
v52) now lives in `old-game/` — its full history/handoff is in
`old-game/HANDOFF.md`. No Unity project exists yet.

The user is creating the Unity project themselves and will connect Claude via
the Unity MCP server. Next: once it exists, check the project layout at the
repo root, add a Unity `.gitignore` if missing, and start porting from
`old-game/DESIGN.md`. Work directly on `master` (no feature branches).

## Last session summary (2026-10-04)

- Moved the entire web prototype (`index.html`, `js/`, `assets/`, `tests/`,
  `tools/`, `server.js`, `style.css`, docs) into `old-game/` via `git mv`.
  All its scripts use `__dirname`-relative paths, so it still runs:
  `node old-game/server.js` → http://localhost:5174. `.claude/launch.json`
  updated accordingly.
- New root `CLAUDE.md` for the port; old one kept at `old-game/CLAUDE.md`.
- Set up Matt Pocock engineering skills config: `docs/agents/`
  (GitHub issue tracker, default triage labels, single-context domain docs).
- Note: `.nojekyll` moved too — if GitHub Pages served the game from repo
  root, it's now at `/old-game/`.
