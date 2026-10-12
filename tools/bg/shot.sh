#!/usr/bin/env bash
# Headless screenshots of the game world from the background copy (Unity WITH a graphics device).
#   bash tools/bg/shot.sh <spec.json | preset> [--out <dir>] [--edit] [--ui] [--save <save.json>]
# Presets live in tools/bg/shots/<name>.json (e.g. `center`).  Spec fields (all optional except shots):
#   mode "play" (default: real Play-mode boot through GameRunner, fresh run or `save`) | "edit" (scene as authored)
#   width/height (1920x1080), warmupFrames (60), ui (false: world only; true: canvases drawn too),
#   save (path to an idle-grounds-save.json to boot from), timeoutSeconds (300),
#   shots: [{ name, island ("center", "farm", ...; empty = absolute), fit (whole Island), x, y (offset / position, cells), size (ortho half-height, cells) }]
# PNGs go to --out (default <bg>/Logs/bg/shots/<time>/); the path list is printed at the end.
source "$(dirname "$0")/common.sh" "$@"; set -- "${ARGS[@]}"
[ -n "$1" ] || die "usage: shot.sh <spec.json|preset> [--out dir] [--edit] [--ui] [--save file]" 64
SPEC="$1"; shift
[ -f "$SPEC" ] || SPEC="$BG_TOOLS/shots/$SPEC.json"
[ -f "$SPEC" ] || die "spec not found: $1 (presets: $(ls "$BG_TOOLS/shots" | sed 's/\.json$//' | tr '\n' ' '))" 64
OUTDIR=""; PATCH=()
while [ $# -gt 0 ]; do
  case "$1" in
    --out) OUTDIR="$2"; shift 2 ;;
    --edit) PATCH+=("mode=edit"); shift ;;
    --ui) PATCH+=("ui=true"); shift ;;
    --save) PATCH+=("save=$(cygpath -m "$2")"); shift 2 ;;
    *) die "unknown option $1" 64 ;;
  esac
done
require_bg
[ -n "$OUTDIR" ] || OUTDIR="$OUT_ROOT/shots/$(date +%Y%m%d-%H%M%S)"
mkdir -p "$OUTDIR"; OUTDIR="$(cd "$OUTDIR" && pwd)"
# apply command-line overrides into a temp copy of the spec
EFFECTIVE="$OUT_ROOT/shot-spec-$$.json"
node -e '
  const fs=require("fs"); const [src,dst,...kv]=process.argv.slice(1);
  const s=JSON.parse(fs.readFileSync(src,"utf8"));
  for (const p of kv){ const i=p.indexOf("="); const k=p.slice(0,i), v=p.slice(i+1); s[k]= v==="true"?true: v==="false"?false: v; }
  fs.writeFileSync(dst, JSON.stringify(s,null,1));' "$(cygpath -m "$SPEC")" "$(cygpath -m "$EFFECTIVE")" "${PATCH[@]}" || die "bad spec JSON: $SPEC" 65
run_unity "shot" --graphics --timeout 900 -- -executeMethod IdleGrounds.Editor.Batch.Screenshots -shotSpec "$(winpath "$EFFECTIVE")" -shotOut "$(winpath "$OUTDIR")"
rm -f "$EFFECTIVE"
EXC=$(count_exceptions "$UNITY_LOG"); [ "$EXC" != 0 ] && { echo "--- $EXC exception line(s):"; grep -E -A3 '^[A-Za-z0-9_.]*Exception(: |$)' "$UNITY_LOG" | head -30; }
RES="$(grep -E '\[Batch\] RESULT' "$UNITY_LOG" | tail -1)"; echo "${RES:-no [Batch] RESULT line}"
echo "--- PNGs in $(winpath "$OUTDIR"):"; ls -1 "$OUTDIR"/*.png 2>/dev/null | while read -r f; do echo "  $(winpath "$f")  ($(stat -c %s "$f") bytes)"; done
if [ "$UNITY_CODE" = 0 ]; then echo "SHOT OK (${UNITY_SECS}s)"; exit 0; fi
echo "SHOT FAILED: exit $UNITY_CODE (${UNITY_SECS}s) — log $(winpath "$UNITY_LOG")"; exit 1
