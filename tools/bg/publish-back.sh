#!/usr/bin/env bash
# Commit the background copy's work on a task branch and push it, so it reaches master through the
# orchestrator (never straight onto master, never into the owner's folder).
#   bash tools/bg/publish-back.sh <task-name> -m "<commit message>" [--dry-run]
# Branch: bg/<task-name>, created from the current HEAD and carrying the uncommitted work.  Editor churn is
# never committed; everything else `git status` shows in the bg copy is - check it first.
#
# Flow:  agent works in the bg copy -> publish-back.sh (pushes origin/bg/<task>) -> orchestrator, in the
# main checkout: git fetch && git merge --no-ff origin/bg/<task>, HANDOFF.md + gate, git push,
# git push origin --delete bg/<task> -> the owner pulls (Unity refreshes) -> tools/bg/sync.sh puts the
# bg copy back on bg/main = new master.
source "$(dirname "$0")/common.sh" "$@"; set -- "${ARGS[@]}"
TASK=""; MSG=""; DRY=0
while [ $# -gt 0 ]; do
  case "$1" in
    -m|--message) MSG="$2"; shift 2 ;;
    --dry-run) DRY=1; shift ;;
    -*) die "unknown option $1" 64 ;;
    *) TASK="$1"; shift ;;
  esac
done
[ -n "$TASK" ] && [ -n "$MSG" ] || die "usage: publish-back.sh <task-name> -m \"message\" [--dry-run]" 64
[[ "$TASK" =~ ^[a-z0-9][a-z0-9._-]*$ ]] || die "task name must be kebab-case: $TASK" 64
require_bg
G() { git -C "$(winpath "$BG")" "$@"; }
restore_churn
CHANGES="$(bg_status)"
AHEAD="$(G log --oneline HEAD --not --remotes 2>/dev/null)"
[ -n "$CHANGES" ] || [ -n "$AHEAD" ] || die "nothing to publish (no changes, no unpushed commits)" 2
BR="bg/$TASK"
echo "bg: publishing to $BR:"
[ -n "$CHANGES" ] && printf '%s\n' "$CHANGES" | head -60
[ -n "$AHEAD" ] && { echo "  + unpushed commits:"; printf '    %s\n' "$AHEAD"; }
if [ $DRY = 1 ]; then echo "bg: --dry-run: nothing committed or pushed"; exit 0; fi
[ "$(G rev-parse --abbrev-ref HEAD)" = "$BR" ] || G checkout -q -B "$BR" || die "could not create $BR"
if [ -n "$CHANGES" ]; then
  G add -A && G commit -q -m "$MSG" || die "commit failed"
fi
G push -u origin "$BR" || die "push failed"
echo "PUBLISHED $BR @ $(G rev-parse --short HEAD)"
echo "orchestrator: git fetch && git merge --no-ff origin/$BR  (then HANDOFF.md, gate, push; git push origin --delete $BR)"
echo "afterwards:   bash tools/bg/sync.sh   (puts this copy back on bg/main = master)"
