# Agent team

Owner decision (`docs/Design/owner-picks-2026-10-12-team.json`): a permanent team of 11 roles, each owning a guide document, and **all agent work happens in the background copy** (a git worktree plus headless Unity, so the owner's open editor is never touched: `D:/WORK/Unity/idle-grounds-bg`, driven by `tools/bg/*`; see CLAUDE.md "Background copy"). The orchestrator (main session) plans, delegates, reviews, integrates, commits and talks to the owner.

## Roles

| Role | Agent file (`.claude/agents/`) | Guide | Model | Typical tasks |
|---|---|---|---|---|
| World designer | `world-designer.md` | `world.md` | opus | Island layouts and overworld, Shrine and Tributes, unlock order and gates, item-source graph, new Island content |
| Balance designer | `balance-designer.md` | `balance.md` | opus | Costs, timers, Tribute and request quantities, encounters and spawns, run and extend the playthrough bot |
| Onboarding designer | `onboarding-designer.md` | `onboarding.md` | sonnet | Quest chain, hints, tutorial order, teaching new mechanics |
| UI/UX designer | `ui-ux-designer.md` | `ui-ux.md` | sonnet | Specs for Shrine/Request panels and HUD, skin system, icon needs |
| UI QA reviewer | `ui-qa-reviewer.md` | `ui-qa.md` | sonnet | Layout/resolution checks, interaction bugs, accessibility (text size, reduce-motion, colour-blind) |
| Art director | `art-director.md` | `art-direction.md` | opus | ART-SPEC style rules, NPC / enemy / creature look, art briefs and redo requests |
| In-game art reviewer | `ingame-art-reviewer.md` | `ingame-art-review.md` | sonnet | Art intake accept/reject, layering, seams, scale, blending, joints in play |
| Writer | `writer.md` | `writing.md` | sonnet | Quest/Help/tooltip text, NPC and request text, story, terminology sweeps |
| Playtester | `playtester.md` | `playtesting.md` | sonnet | Bot runs and timelines, fresh-start walkthroughs, soft-lock hunting |
| Performance reviewer | `performance-reviewer.md` | `performance.md` | sonnet | GC, frame time, build and repo size, scaling to 11 Islands |
| Code health reviewer | `code-health-reviewer.md` | `code-health.md` | opus | Architecture, tests, save/data safety, dead code, docs drift, risky diff review |

Model tiering follows `.claude/rules/personal-workflow.md`: opus where a wrong answer ships a bug or a bad rule; sonnet for the rest; haiku only for chores (running tests, version bumps, lookups) by task specialists.

## Team rules (same in every agent file)

1. Read the plan (`docs/Design/plan.json`) and the owner's decisions (`docs/Design/owner-picks-*.json`) first.
2. Every agent has its own plan task with a "now" line: `node tools/plan/set-task.cjs "<title regex>" in-progress "<now>"`.
3. Owner questions only through the Hub Questions tab (`docs/Design/questions.json`, pictures in `question-images/` if visual). Offer every plausible option, including "all" / the maximal one. The orchestrator republishes the Hub.
4. Work only in the background copy (`bash tools/bg/sync.sh` first; Unity via `tools/bg/compile.sh`, `test.sh`, `menu.sh`, `shot.sh`; hand work back with `tools/bg/publish-back.sh <task> -m "..."`); never in the owner's open project or editor.
5. Test gate before pushing: `bash tools/simtests/run.sh` (fast .NET); full Unity only at milestones. Report results plainly.
6. Never `cat > file` without a heredoc; never reset or overwrite the owner's save.
7. Repo rules from CLAUDE.md still apply (master, commit + push per task, HANDOFF.md in the same commit).

## How roles work together
- World designer decides *what exists where*; balance designer decides *how much and how fast*; onboarding designer decides *when it is taught*; writer supplies *the words*; UI/UX designer shapes *the screens*; art director defines *the look* and in-game art reviewer enforces it in play.
- Reviewers (UI QA, in-game art, playtester, performance, code health) report findings as plan tasks with an owner role; they do not silently fix other roles' work.
- A change that touches a rule in a guide updates that guide in the same commit.

## Task specialists (ad-hoc general-purpose agents, no permanent file)

Use these for mechanical or implementation work. Start every brief with: the goal, the files to read, what is out of scope, the team rules above, and the report you want back. Template: *"You are a [specialist] for Idle Grounds. Goal: ... Read first: CLAUDE.md, HANDOFF.md, [files]. Work only in the background copy (`bash tools/bg/sync.sh`, then edit files under `D:/WORK/Unity/idle-grounds-bg`); do not use the owner's editor or unityMCP. Do: [numbered steps]. Do not: [limits]. Gate: `bash tools/simtests/run.sh` (+ `bash tools/bg/compile.sh` for Unity-side code). Hand back: `bash tools/bg/publish-back.sh <task> -m "..."`. Report: branch, files changed, test output, decisions, open questions."*

| Specialist | Model | One-paragraph brief template |
|---|---|---|
| **Engine pass** | opus for Sim logic / save migration, sonnet for mechanical edits | "Implement [system change] in `IdleGrounds.Sim` per ADR [n] section [x]. Keep Sim pure C# and deterministic and allocation-free in hot paths. Add tests next to the system; a save-schema change needs a version bump, a migration and a round-trip test. Work in the background copy (`bash tools/bg/sync.sh`); run the fast gate, then `bash tools/bg/compile.sh` and `bash tools/bg/test.sh editmode` (expect all ~175 to pass; `--filter <regex>` while iterating) and report counts. Publish with `bash tools/bg/publish-back.sh <task> -m "..."`. List every public API you changed." |
| **Art importer** | sonnet | "Run the art intake: `node tools/art-intake/pull.js`, then `bash tools/bg/menu.sh "Idle Grounds/Art/Integrate Incoming Art"` twice in the background copy (the second run must list 0 changed files; it prints the ArtIntake summary and unwired keys), and `bash tools/bg/shot.sh center` for in-game pictures. Do not accept or reject; hand the report to the in-game art reviewer. Report new, changed and unwired keys and any file that fails the ART-SPEC size or format checks." |
| **Feature builder** | sonnet | "Build [feature] from the spec in [path] using the existing UI skin roles and Editor builders (never hand-edit generated scenes). Wire data through `game-data.json` + importer. Add or update tests. Save scenes/prefabs. Provide a screenshot or the exact steps to see it. Ask UI QA / playtester to review; do not self-approve." |
| **Hub builder** | haiku or sonnet | "Rebuild the Hub with `node tools/hub/build-hub.cjs <scratchpad>/hub`, fix schema or image-reference errors in `docs/Design/*.json`, regenerate `docs/Design/PLAN.md` with `tools/plan/build-plan.cjs`. Report the output; the orchestrator publishes the Artifact to the Hub URL in CLAUDE.md." |
| **Release builder** | sonnet | "In the background copy (`bash tools/bg/sync.sh`), only after the milestone gate has passed (`bash tools/bg/test.sh all` + `bash tools/simtests/run.sh --all`), run `bash tools/bg/build.sh windows|webgl|all` (BuildScript; output in `D:/WORK/Unity/idle-grounds-bg/Builds`, the script prints sizes). Report build sizes and a startup check (run the exe; WebGL: `node D:\WORK\Unity\idle-grounds-bg\tools\serve-webgl.js` serves the bg build at http://localhost:5180); keep productName `idle grounds`. Do not publish or tag." |
| **Integrator** | sonnet (opus for conflicts in Sim code) | "Merge the listed worktree branches onto `master` in order, resolve conflicts, run the fast gate, update HANDOFF.md (pointer + last session summary), commit and push. Stop and report if a conflict touches Sim logic you cannot verify by tests." |

## Where things are
Plan and owner picks: `docs/Design/`. Hub: link in CLAUDE.md. ADRs: `docs/adr/`. Art spec: `docs/art/ART-SPEC.md`. Bot and balance reports: `docs/port/`. Test runner: `tools/simtests/`. Background copy + headless Unity: `tools/bg/` (CLAUDE.md "Background copy").
