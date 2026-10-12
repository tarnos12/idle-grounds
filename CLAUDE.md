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

## Background copy (headless Unity for agents)

The owner's editor has **this folder** open. Agents never touch it (no unityMCP, no scene/prefab/asset
writes here); they use a **git worktree with its own Library** driven by headless Unity:

- Copy: `D:\WORK\Unity\idle-grounds-bg` on branch `bg/main` (tracks `origin/master`). Unity:
  `C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe` (auto-found from ProjectVersion.txt;
  override `UNITY_EXE`). Logs, test XML and screenshots go to `<bg>/Logs/bg/` (git-ignored).
- Scripts `tools/bg/*.sh` (Git Bash; PowerShell: `tools/bg/bg.ps1 <cmd> ...`). Run them from either
  checkout; they always target the bg copy (`--bg <dir>` / `IG_BG` for a task copy). Exit 0 = ok.
  - `sync.sh [branch] [--force]` — bg copy → latest `origin/master` (or branch); refuses to drop
    uncommitted/unpushed work without `--force`.
  - `compile.sh` — open headless, import, report compile errors.
  - `test.sh editmode|playmode|all [--filter regex]` — Unity Test Framework; prints totals + failures.
  - `menu.sh "Idle Grounds/..." [--no-scene]` — run a menu item (Game.unity opened first), save, list
    the files it changed.
  - `shot.sh <spec.json|preset> [--edit] [--ui] [--save file] [--out dir]` — PNGs from the game
    camera after a real Play-mode boot (fresh run unless `--save`); presets in `tools/bg/shots/`
    (`center` = whole Center Island + close-up). `--ui` draws the HUD (best effort).
  - `unity.sh <Class.Method>` — any static editor method; `build.sh windows|webgl|all` — player builds.
  - `publish-back.sh <task> -m "msg"` — commit the bg copy's work on `bg/<task>` and push it.
  - `task.sh new|rm|list <name>` — extra copy `D:\WORK\Unity\idle-grounds-bg-<name>` for parallel
    agents (Library seeded in ~15 s).
- Entry points live in `Assets/_Project/Scripts/Editor/Batch.cs` (batch-mode only). Play mode in the
  bg copy uses `IDLEGROUNDS_SAVE_DIR=<bg>/Library/bg-save` (see `SaveService.SaveDir`), never the
  owner's save. MCP for Unity stays off in batch mode. Two editors on two folders are fine.
- **Flow:** `sync.sh` → work + `compile.sh`/`test.sh`/`shot.sh` in the bg copy → `publish-back.sh
  <task>` → orchestrator merges `origin/bg/<task>` into `master` (HANDOFF.md, gate, push, delete the
  branch) → the owner pulls and Unity refreshes → `sync.sh` puts the bg copy back on master.
- Timings (2026-10-12): first open ~55 s with the copied Library, EditMode 175 tests ~6 min, menu
  item ~25 s, 2 screenshots ~50 s.

## Agent team (owner decision 2026-10-12: `docs/Design/owner-picks-2026-10-12-team.json`)

A permanent team of 11 role agents lives in `.claude/agents/` (world-designer, balance-designer,
onboarding-designer, ui-ux-designer, ui-qa-reviewer, art-director, ingame-art-reviewer, writer,
playtester, performance-reviewer, code-health-reviewer). Each owns a guide in `docs/guides/`; the
team table, model tiers and the task-specialist brief templates (engine pass, art importer, feature
builder, hub builder, release builder, integrator) are in **`docs/guides/README.md`**. The main
session orchestrates, reviews, integrates and commits. Rules for every agent:

1. Read the plan (`docs/Design/plan.json`) and the owner's decisions (`owner-picks-*.json`) first.
2. Each agent has its own plan task with a "now" line (`node tools/plan/set-task.cjs ...`).
3. Owner questions only via the Hub Questions tab (`docs/Design/questions.json`, with pictures if
   visual); offer every plausible option, including "all" / the maximal one.
4. **All agent work happens in the background copy** (git worktree + headless Unity, see
   "Background copy" above: `tools/bg/*`), never in the owner's open project or editor.
5. Run the test gate before pushing (fast `bash tools/simtests/run.sh`; full Unity only at
   milestones) and report results plainly.
6. Never `cat > file` without a heredoc; never reset the owner's save.
