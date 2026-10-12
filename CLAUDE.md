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

## Project Hub (owner dashboard) — https://claude.ai/artifact/EAabdoZhE79jgBc6THmrwG

One private, pinned page for the owner: game data (**Items**, **Buildings**, **Charts**), the
**Islands** map (11 Islands in unlock order, Tributes, NPC requests; `#islands/<id>` deep
link), the **Plan** and open **Questions**. It is generated, never edited by hand.
- Sources (all in `docs/Design/`): `plan.json`, `questions.json`, `question-images/`,
  `islands.json`, `owner-picks-<date>.json`; game data
  `Assets/_Project/Data/Source/game-data.json`; item/building art in `Art/Incoming`.
  `plan.json` schema: title, source, statusMeaning, owners, and per milestone
  id/name/status/goal/exit/optional gate/tasks (task: title, detail, status, size, owner,
  now, blockedBy). Statuses: done, in-progress, next, later, todo, blocked, waiting.
  `islands.json` is the single source for the Islands tab (positions, tributes, NPCs,
  requests); keep it in step with `docs/adr/0007-island-requesters.md` and `WORLD.islands`.
- Build: `node tools/hub/build-hub.cjs <scratchpad>/hub` (checks script syntax, image
  refs, plan/islands schema), then publish `<scratchpad>/hub/index.html` with its `img/`
  and `q/` side files **to the same URL above** (Artifact `url`).
- Tasks: `node tools/plan/set-task.cjs "<precise title regex>" <done|in-progress|next|later|todo|blocked|waiting|-> ["now" | "reason"]`;
  `node tools/plan/build-plan.cjs` writes `docs/Design/PLAN.md` plus a printable A4 page and
  a PDF (headless Edge) at `Temp/plan/Idle-Grounds-Plan.pdf`.
- Rules: every owner request/pick/delivery becomes a plan task at once; every running
  agent has an `in-progress` task with a `now` line; blocked/waiting tasks always say
  why. Owner decisions go **only** in the Questions tab (recommended options marked,
  never pre-selected). When answers are pasted in chat: save them verbatim in
  `docs/Design/owner-picks-<date>.json`, remove answered questions, turn answers into tasks,
  rebuild + republish. Owner picks are commitments — read the picks files before
  planning. Rebuild + republish after every plan/question/data change, and end every
  status report with the Hub link.

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

## Testing

- **After every change: run the fast .NET sim tests** — `tools/simtests/run.sh`
  (or `powershell -ExecutionPolicy Bypass -File tools/simtests/run.ps1`). Plain
  `dotnet test` over the linked Sim sources + `Tests/Sim`; no Unity editor needed
  (168 tests, ~30 s). Excludes `Slow` and `UnityOnly`.
- `tools/simtests/run.sh --all` also runs the `Slow` playthrough bots (171 tests, ~70 s).
- `UnityOnly` tests (profiler GC-allocation: `SimTickAllocTests`, `NonAllocQueries_DoNotAllocate`)
  only run in Unity.
- **Full Unity EditMode + PlayMode (+ Slow playthroughs) only at milestones / before
  builds** — they block the editor (~177 EditMode tests incl. 4 Game tests, several minutes).
