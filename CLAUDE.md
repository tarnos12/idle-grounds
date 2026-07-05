# CLAUDE.md — how to work on Idle Grounds

Project instructions for any Claude Code session on this repo. Read the three
companion docs before doing work:
- **HANDOFF.md** — current state, ▶ NEXT SESSION pointer, last-session summary.
- **DESIGN.md** — the agreed economy/building/logistics plan with done-markers.
- **README.md** — what the game is + how to run.

Idle Grounds is a browser sandbox/idle prototype (cultivation/xianxia theme) in
vanilla HTML/CSS/JS — no build step, no dependencies.

## Workflow rules (the user's preferences — follow these every time)

1. **Commit + push after every completed task.** Don't let work pile up
   uncommitted. One task = one focused commit on `master`, pushed to
   `origin` (github.com/tarnos12/idle-grounds).
2. **Every commit MUST update HANDOFF.md** in the same commit — refresh both
   the "▶ NEXT SESSION: start here" pointer and the "Last session summary".
   A stale handoff misdirects the next session. (Note: the `▶` heading char
   can defeat a regex replace — edit that line with an exact string.)
3. **Bump the `?v=N` asset version** in `index.html` on any code change (all
   6 references) — the server sends `no-store` but the version tag is the
   reliable cache-bust. Keep it in step with what HANDOFF records.
4. **Update DESIGN.md** when scope/roadmap changes (mark items ✅, add plans).
5. **Always share the game link** — include `http://localhost:5174` in the
   reply after any change or server start.
6. **Sync with git before starting** — `git fetch`, check `origin/master`;
   other Claude sessions (local + cloud) share this repo.
7. **Verify before committing.** Prefer headless checks via the preview tools
   (`preview_eval` against the running server) — engine math AND real click
   paths. Report failures honestly.

## Testing notes

- Headless preview tabs throttle `setInterval` to ~1/s — drive
  `E.gameTick()` manually to fast-forward time-based logic.
- Exercise real UI via synthetic `MouseEvent`s on `#world-viewport` AFTER a
  `mousemove` (handlers gate on `cursor.over`); `preview_screenshot` can time
  out, so sample canvas pixels via `getImageData` when you need visual proof.
- `DATA.TEST` has `ENABLED` + `timeScale`/`costScale` for fast iteration;
  a real balance pass needs `ENABLED=false`.

## Architecture conventions (don't break these)

- **Shared global scope** across classic `<script>` files — never redeclare a
  `const` across files (`CELL` lives in engine.js). Layers hang off globals:
  `window.DATA` (config), `GS` (state), `ENGINE` (pure logic, DOM-free),
  `UI` (canvas render + input), `SAVE`.
- **World + upgrade tree render on `<canvas>`**; DOM is only overlays
  (bottom bar, menus, hand cursor, modals, quest panel).
- **Firefox emoji fix must stay:** Twemoji-first `EMOJI_FONT`, explicit bright
  `fillStyle` before every emoji `fillText`, `willReadFrequently: true`,
  whole-device-pixel canvas backing. (See HANDOFF "dark film" section.)
- **Saves migrate idempotently** in `loadState()` — scrub against the CURRENT
  config, default new fields, drop corrupt/dead entries. Add a migration line
  whenever you add state or change item/building shapes.
- **Item icons:** `assets/icons/<key>.png` with emoji fallback, so new items
  work before art exists. Building/node/enemy sprites are still emoji.
