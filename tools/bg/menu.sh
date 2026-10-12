#!/usr/bin/env bash
# Run an editor menu item headless in the background copy, then save scenes/assets.
#   bash tools/bg/menu.sh "Idle Grounds/Art/Integrate Incoming Art" [--scene <path>|--no-scene] [--graphics]
# Opens Assets/_Project/Scenes/Game.unity first (most builders work on it) unless --no-scene.
# Prints the files the menu item changed in the bg copy (git status delta; editor churn ignored).
# Exit 0 = ran with no errors; 1 = missing item / exception / compile error; 2 = ran but logged errors.
source "$(dirname "$0")/common.sh" "$@"; set -- "${ARGS[@]}"
[ -n "$1" ] || die "usage: menu.sh \"<Menu/Path>\" [--scene path|--no-scene] [--graphics]" 64
MENU="$1"; shift
SCENE=(); OPTS=()
while [ $# -gt 0 ]; do
  case "$1" in
    --scene) SCENE=(-scene "$2"); shift 2 ;;
    --no-scene) SCENE=(-noScene); shift ;;
    --graphics) OPTS+=(--graphics); shift ;;
    --timeout) OPTS+=(--timeout "$2"); shift 2 ;;
    *) die "unknown option $1" 64 ;;
  esac
done
require_bg
BEFORE="$(bg_status)"
run_unity "menu" "${OPTS[@]}" -- -executeMethod IdleGrounds.Editor.Batch.RunMenu -menu "$MENU" "${SCENE[@]}"
AFTER="$(bg_status)"
CHANGED="$(diff <(printf '%s\n' "$BEFORE") <(printf '%s\n' "$AFTER") | sed -n 's/^> //p')"
if [ -n "$CHANGED" ]; then echo "--- files changed by this run ($(printf '%s\n' "$CHANGED" | wc -l)):"; printf '%s\n' "$CHANGED" | head -60
else echo "--- files changed by this run: 0"; fi
[ "$UNITY_COMPILE_ERRORS" != 0 ] && { grep -E 'error CS[0-9]+' "$UNITY_LOG" | sort -u | head -20; }
RES="$(grep -E '\[Batch\] RESULT' "$UNITY_LOG" | tail -1)"; echo "${RES:-no [Batch] RESULT line (method did not run?)}"
if [ "$UNITY_CODE" = 0 ] && [ "$UNITY_COMPILE_ERRORS" = 0 ]; then echo "MENU OK (${UNITY_SECS}s)"; exit 0; fi
echo "MENU FAILED: exit $UNITY_CODE (${UNITY_SECS}s) — log $(winpath "$UNITY_LOG")"
[ "$UNITY_CODE" = 2 ] && exit 2; exit 1
