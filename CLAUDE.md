# CLAUDE.md — how to work on Idle Grounds

Idle Grounds is being **ported to Unity** (started 2026-10-04). The original
browser prototype (vanilla HTML/CSS/JS, cultivation/xianxia sandbox idle)
lives in **`old-game/`** as the reference implementation — its own
`old-game/CLAUDE.md`, `HANDOFF.md`, `DESIGN.md` and `README.md` describe it.
Read `old-game/DESIGN.md` for the agreed economy/building/logistics design
the port should reproduce.

Read **HANDOFF.md** (repo root) before doing work — current state of the port
and the ▶ NEXT SESSION pointer.

## Workflow rules (the user's preferences — follow these every time)

1. **Work on `master` by default (agreed 2026-10-04).** Commit + push after
   every completed task. Use a `feature/<short-kebab-desc>` branch only when
   it genuinely helps (large/risky/multi-session work that shouldn't land
   half-done); merge it back to `master` and delete it when finished.
2. **Every commit MUST update the root HANDOFF.md** in the same commit —
   refresh both the "▶ NEXT SESSION: start here" pointer and the "Last
   session summary". (Edit the `▶` line with an exact string.)
3. **Sync with git before starting** — `git fetch`, check `origin/master`;
   other Claude sessions share this repo.
4. **Verify before committing** and report failures honestly.
5. `old-game/` is frozen reference material — don't change it unless asked.
   It still runs: `node old-game/server.js` → `http://localhost:5174`
   (launch config `idle-grounds`).

## Unity project + MCP

- Unity **6000.3.12f1**, URP, 2D packages. The project lives at the repo
  root (`Assets/`, `Packages/`, `ProjectSettings/`).
- Editor access is through **MCP for Unity** (`unityMCP` in `.mcp.json`,
  HTTP `127.0.0.1:8080/mcp`). That server is **shared with another project**
  ("Monster Catching Game 2D"), so first call `set_active_instance` with this
  project's instance (`mcpforunity://instances` lists them; ours is named
  `idle-grounds` or `idle grounds`). Never act on the other instance.

## Agent skills

### Issue tracker

Issues are tracked in GitHub Issues on `tarnos12/idle-grounds` via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default five-role vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one root `CONTEXT.md` + `docs/adr/` (created lazily). See `docs/agents/domain.md`.
