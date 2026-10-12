#!/usr/bin/env bash
# Visual tour for design reviews: ~170 raw 1920x1080 PNGs (islands, buildings, every panel, sky, locked islands).
#   bash tools/bg/tour.sh <outDir> [spec ...]      (default: all specs)
# 1. bot saves (fresh/t30/end) via tools/simtests TourSaves test -> <outDir>/saves   (plain .NET, ~1 min)
# 2. specs via tools/bg/tour/gen-specs.cjs -> <outDir>/specs/*.json
# 3. one tools/bg/shot.sh run per spec -> <outDir>/raw/<spec>/*.png   (~50 s each)
# Specs: world_end fresh_ui fresh_world t30_ui end_ui end_world2.  Curate/rename afterwards (see tour INDEX.md).
source "$(dirname "$0")/common.sh" "$@"; set -- "${ARGS[@]}"
[ -n "$1" ] || die "usage: tour.sh <outDir> [spec ...]" 64
OUT="$1"; shift; mkdir -p "$OUT/saves" "$OUT/raw"; OUT="$(cd "$OUT" && pwd)"
OUTW="$(cygpath -m "$OUT")"
if [ ! -f "$OUT/saves/end.json" ]; then
  echo "tour: generating bot saves"
  ( cd "$(dirname "$0")/../simtests" && IG_TOUR_SAVES="$OUTW/saves" dotnet test -c Release --nologo --filter "Name=WriteTourSaves" -v q ) >/dev/null 2>&1
  [ -f "$OUT/saves/end.json" ] || die "bot saves were not written (dotnet test failed?)" 1
fi
node "$(cygpath -m "$(dirname "$0")")/tour/gen-specs.cjs" "$OUTW" >/dev/null || die "spec generation failed" 1
SPECS=("$@"); [ ${#SPECS[@]} -gt 0 ] || SPECS=(world_end fresh_ui fresh_world t30_ui end_ui end_world2)
for s in "${SPECS[@]}"; do
  echo "tour: $s"; mkdir -p "$OUT/raw/$s"
  bash "$(dirname "$0")/shot.sh" "$OUTW/specs/$s.json" --out "$OUTW/raw/$s" | grep -E "RESULT|FAILED|SHOT " | sort -u
done
echo "tour: done -> $OUTW/raw"
