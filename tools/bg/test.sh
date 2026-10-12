#!/usr/bin/env bash
# Unity Test Framework run in the background copy (headless).
#   bash tools/bg/test.sh [editmode|playmode|all] [--filter <regex>] [--category <cat>] [--graphics]
# Results XML: <bg>/Logs/bg/results-<platform>-<time>.xml.  Exit 0 = all passed, 1 = failures/errors.
# PlayMode tests boot Game.unity: they use IDLEGROUNDS_SAVE_DIR (bg-only save), never the owner's save.
source "$(dirname "$0")/common.sh" "$@"; set -- "${ARGS[@]}"
WHICH="${1:-editmode}"; [ $# -gt 0 ] && shift
EXTRA=(); GFX=()
while [ $# -gt 0 ]; do
  case "$1" in
    --filter) EXTRA+=(-testFilter "$2"); shift 2 ;;
    --category) EXTRA+=(-testCategory "$2"); shift 2 ;;
    --graphics) GFX=(--graphics); shift ;;
    *) die "unknown option $1" 64 ;;
  esac
done
case "$WHICH" in
  editmode) PLATFORMS=(EditMode) ;;
  playmode) PLATFORMS=(PlayMode) ;;
  all) PLATFORMS=(EditMode PlayMode) ;;
  *) die "usage: test.sh [editmode|playmode|all] [--filter regex] [--category cat] [--graphics]" 64 ;;
esac
require_bg
RC=0
for P in "${PLATFORMS[@]}"; do
  RES="$OUT_ROOT/results-${P,,}-$(date +%Y%m%d-%H%M%S).xml"
  run_unity "test-${P,,}" "${GFX[@]}" -- -runTests -testPlatform "$P" -testResults "$(winpath "$RES")" "${EXTRA[@]}"
  # Unity: 0 = all passed, 2 = some failed, other = run error
  if [ "$UNITY_COMPILE_ERRORS" != 0 ]; then echo "--- compile errors:"; grep -E 'error CS[0-9]+' "$UNITY_LOG" | sort -u | head -30; fi
  node "$BG_TOOLS/lib/test-summary.cjs" "$RES" "$P"; S=$?
  echo "bg: $P wall ${UNITY_SECS}s, unity exit $UNITY_CODE, results $(winpath "$RES")"
  if [ $S != 0 ] || { [ "$UNITY_CODE" != 0 ] && [ "$UNITY_CODE" != 2 ]; }; then RC=1; fi
  [ "$UNITY_CODE" = 2 ] && RC=1
done
[ $RC = 0 ] && echo "TESTS OK" || echo "TESTS FAILED"
exit $RC
