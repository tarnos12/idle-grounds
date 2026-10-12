#!/usr/bin/env bash
# Bring the background copy to the latest origin/<branch> (default master).
#   bash tools/bg/sync.sh [branch] [--force]
# The bg worktree sits on its own scratch branch (bg/main for the default copy, bg/<task> for task copies);
# sync re-points that branch at origin/<branch>.  Refuses (exit 2) when the copy has uncommitted work or
# commits that are on no remote branch, unless --force (which DISCARDS them).  Editor churn (TMP fallback
# atlas, .vsconfig) is always discarded.  Library/ is kept, so the next Unity run only re-imports the delta.
# run from a temp copy: the checkout below may rewrite this very file while bash is still reading it
if [ -z "$IG_BG_SYNC_TMP" ]; then
  T="$(mktemp -d)"; cp "$(dirname "$0")/sync.sh" "$(dirname "$0")/common.sh" "$T/"
  IG_BG_SYNC_TMP="$T" IG_BG_TOOLS_HOME="$(cd "$(dirname "$0")" && pwd)" bash "$T/sync.sh" "$@"; rc=$?
  rm -rf "$T"; exit $rc
fi
source "$(dirname "$0")/common.sh" "$@"; set -- "${ARGS[@]}"
BRANCH=master; FORCE=0
for a in "$@"; do
  case "$a" in
    --force) FORCE=1 ;;
    -*) die "unknown option $a" 64 ;;
    *) BRANCH="$a" ;;
  esac
done
require_bg; require_unlocked
G() { git -C "$(winpath "$BG")" "$@"; }
info "fetching origin"
G fetch --prune origin || die "git fetch failed"
G rev-parse --verify -q "origin/$BRANCH" >/dev/null || die "no such remote branch: origin/$BRANCH" 64
restore_churn
DIRTY="$(bg_status)"
if [ -n "$DIRTY" ]; then
  if [ $FORCE = 0 ]; then
    echo "bg: the background copy has uncommitted changes:"; printf '%s\n' "$DIRTY" | head -40
    die "refusing to discard them - publish them (tools/bg/publish-back.sh) or rerun with --force" 2
  fi
  info "--force: discarding $(printf '%s\n' "$DIRTY" | wc -l) uncommitted change(s)"
  G reset -q --hard && G clean -q -fd || die "could not discard changes"
fi
UNPUSHED="$(G log --oneline HEAD --not --remotes 2>/dev/null)"
if [ -n "$UNPUSHED" ]; then
  if [ $FORCE = 0 ]; then
    echo "bg: commits on $(G rev-parse --abbrev-ref HEAD) that are on no remote branch:"; printf '%s\n' "$UNPUSHED" | head -20
    die "refusing to drop them - run tools/bg/publish-back.sh first, or --force" 2
  fi
  info "--force: dropping $(printf '%s\n' "$UNPUSHED" | wc -l) unpushed commit(s)"
fi
CUR="$(G rev-parse --abbrev-ref HEAD)"
LOCAL="bg/main"                                   # the default copy always returns to bg/main
if [ "$BG" != "${MAIN_ROOT}-bg" ]; then case "$CUR" in bg/*) LOCAL="$CUR" ;; esac; fi   # task copies keep bg/<task>
G checkout -q -B "$LOCAL" "origin/$BRANCH" || die "checkout failed"
G branch -q --set-upstream-to="origin/$BRANCH" "$LOCAL" 2>/dev/null
info "synced $(winpath "$BG") -> $LOCAL @ $(G log --oneline -1 HEAD)"
