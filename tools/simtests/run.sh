#!/usr/bin/env bash
# Plain-.NET sim tests (no Unity editor).  ./run.sh = fast (every change);  ./run.sh --all = + Slow playthroughs.
# UnityOnly (profiler GC counters) is always excluded.  Extra args go to `dotnet test`.
export DOTNET_CLI_UI_LANGUAGE=en VSLANG=1033 DOTNET_NOLOGO=1
cd "$(dirname "$0")" || exit 1
FILTER='TestCategory!=Slow&TestCategory!=UnityOnly'
if [ "$1" = "--all" ]; then FILTER='TestCategory!=UnityOnly'; shift; fi
START=$(date +%s)
OUT=$(dotnet test -c Release --nologo --filter "$FILTER" -v q "$@" 2>&1); CODE=$?
echo "$OUT" | grep -E "error|Failed |\[FAIL\]" | head -40
SUMMARY=$(echo "$OUT" | grep -E "^(Passed|Failed)!" | tail -1)
echo "${SUMMARY:-no test summary (build failed?)} | wall $(( $(date +%s) - START ))s | filter: $FILTER"
exit $CODE
