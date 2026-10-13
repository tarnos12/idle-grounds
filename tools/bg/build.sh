#!/usr/bin/env bash
# Player builds from the background copy via Editor/BuildScript.cs (same as menu Idle Grounds/Build/...).
#   bash tools/bg/build.sh [windows|webgl|android|all]
# Output: <bg>/Builds/Windows/IdleGrounds.exe, <bg>/Builds/WebGL, <bg>/Builds/Android/IdleGrounds.apk (git-ignored).
# "all" = Windows + WebGL (Android is separate: switching the build target reimports the whole project, ~10+ min).  Release builder only, after
# the milestone gate (full Unity EditMode + PlayMode + Slow).  Exit 0 only when every build Succeeded.
source "$(dirname "$0")/common.sh" "$@"; set -- "${ARGS[@]}"
WHICH="${1:-windows}"
case "$WHICH" in
  windows) M=BuildWindows; N=1 ;;
  webgl) M=BuildWebGL; N=1 ;;
  android) M=BuildAndroid; N=1 ;;
  all) M=BuildAll; N=2 ;;
  *) die "usage: build.sh [windows|webgl|android|all]" 64 ;;
esac
run_unity "build-$WHICH" --timeout 7200 -- -executeMethod "IdleGrounds.Editor.BuildScript.$M" -quit
grep -E 'BuildScript:' "$UNITY_LOG" | sed 's/^/  /'
OK=$(grep -cE 'BuildScript: .* Succeeded' "$UNITY_LOG")
for d in "$BG/Builds/Windows" "$BG/Builds/WebGL" "$BG/Builds/Android"; do
  [ -d "$d" ] && echo "  $(winpath "$d"): $(du -sh "$d" | cut -f1)"
done
if [ "$UNITY_CODE" = 0 ] && [ "$OK" = "$N" ]; then echo "BUILD OK (${UNITY_SECS}s)"; exit 0; fi
echo "BUILD FAILED: $OK/$N succeeded, unity exit $UNITY_CODE - log $(winpath "$UNITY_LOG")"; exit 1
