#!/usr/bin/env bash
# Shared helpers for tools/bg/*.sh — the BACKGROUND COPY of Idle Grounds (CLAUDE.md "Background copy").
# Sourced, not run.  Every script accepts `--bg <dir>` (or env IG_BG) to target another worktree
# (per-task copies made by tools/bg/task.sh); default = "<main checkout>-bg".
#
# Safety: refuses to run Unity on the owner's main checkout, refuses when another Unity already has the
# background copy open, and sets IDLEGROUNDS_SAVE_DIR so Play mode never touches the owner's real save.

set -o pipefail
# (MSYS2_ARG_CONV_EXCL is set only on the Unity.exe call: Windows paths must reach it untouched)
unset UNITY_MCP_ALLOW_BATCH             # MCP for Unity stays off in batch mode (never joins the owner's MCP server)

BG_TOOLS="${IG_BG_TOOLS_HOME:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)}"

die()  { echo "bg: ERROR: $1" >&2; exit "${2:-1}"; }
info() { echo "bg: $*"; }
winpath() { cygpath -m "$1"; }           # D:/WORK/... (Unity accepts forward slashes)

# main checkout = first entry of `git worktree list`
MAIN_ROOT="$(git -C "$BG_TOOLS" worktree list --porcelain 2>/dev/null | sed -n '1s/^worktree //p')"
[ -n "$MAIN_ROOT" ] || die "not inside a git checkout"
MAIN_ROOT="$(cd "$MAIN_ROOT" && pwd)"

# parse --bg <dir> out of the args; the rest stays in ARGS
ARGS=()
while [ $# -gt 0 ]; do
  case "$1" in
    --bg) BG_OVERRIDE="$2"; shift 2 ;;
    --bg=*) BG_OVERRIDE="${1#--bg=}"; shift ;;
    *) ARGS+=("$1"); shift ;;
  esac
done
BG="${BG_OVERRIDE:-${IG_BG:-${MAIN_ROOT}-bg}}"
[ -d "$BG" ] && BG="$(cd "$BG" && pwd)"

OUT_ROOT="$BG/Logs/bg"                   # Logs/ is git-ignored

require_bg() {
  [ -d "$BG" ] || die "background copy not found: $BG (create it: git worktree add -b bg/main \"$BG\" origin/master — see CLAUDE.md)"
  [ -e "$BG/.git" ] || die "$BG is not a git worktree"
  [ "$BG" != "$MAIN_ROOT" ] || die "refusing to work in the owner's main checkout ($MAIN_ROOT)"
  case "$(winpath "$BG")" in "$(winpath "$MAIN_ROOT")") die "refusing: BG resolves to the owner's checkout";; esac
  mkdir -p "$OUT_ROOT"
}

find_unity() {
  if [ -n "$UNITY_EXE" ] && [ -f "$UNITY_EXE" ]; then echo "$UNITY_EXE"; return; fi
  local ver; ver="$(sed -n 's/^m_EditorVersion: *//p' "$BG/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
  [ -n "$ver" ] || die "cannot read the editor version from ProjectSettings/ProjectVersion.txt"
  local c
  for c in "/c/Program Files/Unity/Hub/Editor/$ver/Editor/Unity.exe" \
           "$(cygpath -u "$(sed -n 's/^"\(.*\)"$/\1/p' "$APPDATA/UnityHub/secondaryInstallPath.json" 2>/dev/null)" 2>/dev/null)/$ver/Editor/Unity.exe" \
           "/c/Program Files/Unity $ver/Editor/Unity.exe"; do
    [ -f "$c" ] && { echo "$c"; return; }
  done
  die "Unity $ver not found (set UNITY_EXE=/path/to/Unity.exe)"
}

# A running editor holds Temp/UnityLockfile open (Windows: it cannot be renamed while locked).
require_unlocked() {
  local lf="$BG/Temp/UnityLockfile"
  if [ -e "$lf" ]; then
    if mv "$lf" "$lf.probe" 2>/dev/null; then mv "$lf.probe" "$lf"; else
      die "another Unity instance has $BG open (Temp/UnityLockfile is locked) — wait for it or close it" 3
    fi
  fi
}

export_env() {
  export IDLEGROUNDS_SAVE_DIR="$(winpath "$BG")/Library/bg-save"     # never the owner's persistentDataPath save
  mkdir -p "$BG/Library/bg-save"
}

# run_unity <label> [--graphics] [--timeout SEC] -- <unity args...>
#   Runs Unity.exe -batchmode [-nographics] -projectPath <bg> -logFile <log> <args>, streams the
#   interesting log lines, prints a summary.  Sets UNITY_CODE, UNITY_LOG, UNITY_SECS, UNITY_COMPILE_ERRORS.
run_unity() {
  local label="$1"; shift
  local graphics=0 tmo="${IG_BG_TIMEOUT:-3600}"
  while [ $# -gt 0 ]; do
    case "$1" in
      --graphics) graphics=1; shift ;;
      --timeout) tmo="$2"; shift 2 ;;
      --) shift; break ;;
      *) break ;;
    esac
  done
  require_bg; require_unlocked; export_env
  local unity; unity="$(find_unity)"
  local stamp; stamp="$(date +%Y%m%d-%H%M%S)"
  UNITY_LOG="$OUT_ROOT/$label-$stamp.log"
  : > "$UNITY_LOG"
  local ng=(-nographics); [ "$graphics" = 1 ] && ng=()
  info "unity: $label  (project $(winpath "$BG"), log $(winpath "$UNITY_LOG"))"
  local t0; t0=$(date +%s)
  MSYS2_ARG_CONV_EXCL="*" timeout --kill-after=30 "$tmo" "$unity" -batchmode "${ng[@]}" -projectPath "$(winpath "$BG")" -logFile "$(winpath "$UNITY_LOG")" "$@" &
  local pid=$!
  # stream the useful lines while Unity runs
  tail -n +1 -F --pid=$pid "$UNITY_LOG" 2>/dev/null | grep --line-buffered -E \
    '\[Batch\]|error CS[0-9]+|Scripts have compiler errors|Exception:|Aborting batchmode|BuildScript:|Test (run|execution)|executeMethod|No valid Unity Editor license' &
  local tailpid=$!
  wait $pid; UNITY_CODE=$?
  sleep 1; kill $tailpid 2>/dev/null; wait $tailpid 2>/dev/null
  UNITY_SECS=$(( $(date +%s) - t0 ))
  UNITY_COMPILE_ERRORS=$(grep -cE 'error CS[0-9]+' "$UNITY_LOG")
  [ "$UNITY_CODE" = 124 ] && echo "bg: TIMEOUT after ${tmo}s"
  if grep -qiE 'No valid Unity Editor license|License is not active|licen[cs]e.*(error|fail)' "$UNITY_LOG"; then
    echo "bg: LICENSE problem — see $(winpath "$UNITY_LOG") (activate via Unity Hub as the same Windows user; not fought here)"
  fi
  info "unity: $label finished: exit $UNITY_CODE in ${UNITY_SECS}s, compile errors: $UNITY_COMPILE_ERRORS"
  return 0
}

# exception lines in a Unity log, ignoring editor-internal noise we cannot act on (UnityEditor.Search indexing)
count_exceptions() {
  awk '/^[A-Za-z0-9_.]*Exception(: |$)/ { if (pend) n++; pend=1; k=0; next }
       pend { k++; if ($0 ~ /UnityEditor.Search./) pend=0; else if (k>=4) { n++; pend=0 } }
       END { if (pend) n++; print n+0 }' "$1"
}

# Files the editor rewrites just by opening the project (dynamic TMP atlas, VS config). They are
# not real changes: sync discards them, publish-back never commits them, menu/shot diffs ignore them.
CHURN_TRACKED=("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset"
               "ProjectSettings/ShaderGraphSettings.asset")
CHURN_UNTRACKED=(".vsconfig")

restore_churn() {
  local f
  for f in "${CHURN_TRACKED[@]}"; do git -C "$(winpath "$BG")" checkout -q -- "$f" 2>/dev/null; done
  for f in "${CHURN_UNTRACKED[@]}"; do git -C "$(winpath "$BG")" ls-files --error-unmatch -- "$f" >/dev/null 2>&1 || rm -f "$BG/$f"; done
}

# `git status --porcelain` of the bg copy without editor churn
bg_status() {
  local out; out="$(git -C "$(winpath "$BG")" status --porcelain --untracked-files=all)"
  local f
  for f in "${CHURN_TRACKED[@]}" "${CHURN_UNTRACKED[@]}"; do out="$(printf '%s\n' "$out" | grep -vF -- "$f")"; done
  printf '%s\n' "$out" | sed '/^$/d'
}
