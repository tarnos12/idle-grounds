#!/usr/bin/env bash
# Run any static editor method headless in the background copy.
#   bash tools/bg/unity.sh <Namespace.Class.Method> [--graphics] [--timeout SEC] [--allow-exceptions] [-- extra Unity args]
# Example: bash tools/bg/unity.sh IdleGrounds.Editor.BuildScript.BuildWindows
# Fails (exit 1) on a non-zero Unity exit, compile errors, or exceptions in the log (unless --allow-exceptions).
source "$(dirname "$0")/common.sh" "$@"; set -- "${ARGS[@]}"
[ -n "$1" ] || die "usage: unity.sh <Class.Method> [--graphics] [--timeout SEC] [--allow-exceptions] [-- extra args]" 64
METHOD="$1"; shift
OPTS=(); ALLOW_EXC=0; EXTRA=()
while [ $# -gt 0 ]; do
  case "$1" in
    --graphics) OPTS+=(--graphics); shift ;;
    --timeout) OPTS+=(--timeout "$2"); shift 2 ;;
    --allow-exceptions) ALLOW_EXC=1; shift ;;
    --) shift; EXTRA=("$@"); break ;;
    *) EXTRA+=("$1"); shift ;;
  esac
done
QUIT=(-quit); [ "${IG_BG_NOQUIT:-0}" = 1 ] && QUIT=()
run_unity "${METHOD##*.}" "${OPTS[@]}" -- -executeMethod "$METHOD" "${QUIT[@]}" "${EXTRA[@]}"
EXC=$(count_exceptions "$UNITY_LOG")
FAIL=0
[ "$UNITY_CODE" != 0 ] && FAIL=1
[ "$UNITY_COMPILE_ERRORS" != 0 ] && { echo "--- compile errors:"; grep -E 'error CS[0-9]+' "$UNITY_LOG" | sort -u | head -30; FAIL=1; }
if [ "$EXC" != 0 ]; then
  echo "--- $EXC exception line(s):"; grep -E -A3 '^[A-Za-z0-9_.]*Exception(: |$)' "$UNITY_LOG" | head -40
  [ "$ALLOW_EXC" = 1 ] || FAIL=1
fi
grep -E '\[Batch\] RESULT' "$UNITY_LOG" | tail -1
if [ $FAIL = 0 ]; then echo "UNITY OK: $METHOD (${UNITY_SECS}s)"; else echo "UNITY FAILED: $METHOD (exit $UNITY_CODE, ${UNITY_SECS}s) — log: $(winpath "$UNITY_LOG")"; fi
exit $FAIL
