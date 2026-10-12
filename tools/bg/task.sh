#!/usr/bin/env bash
# Extra per-task background copies (parallel agents): each is its own worktree + branch + Library.
#   bash tools/bg/task.sh new <name>    -> <main>-bg-<name> on bg/<name> from origin/master, Library seeded from the main bg copy
#   bash tools/bg/task.sh rm <name> [--force]   -> remove that worktree (refuses with uncommitted work unless --force)
#   bash tools/bg/task.sh list
# Then point any tools/bg script at it with --bg <dir> or IG_BG=<dir>.
source "$(dirname "$0")/common.sh" "$@"; set -- "${ARGS[@]}"
CMD="$1"; NAME="$2"; FORCE=0; [ "$3" = "--force" ] && FORCE=1
M() { git -C "$(winpath "$MAIN_ROOT")" "$@"; }
case "$CMD" in
  list) M worktree list ;;
  new)
    [[ "$NAME" =~ ^[a-z0-9][a-z0-9._-]*$ ]] || die "usage: task.sh new <kebab-name>" 64
    DIR="${MAIN_ROOT}-bg-$NAME"; [ -e "$DIR" ] && die "already exists: $DIR" 2
    SEED="${MAIN_ROOT}-bg/Library"; [ -d "$SEED" ] || die "seed Library missing: $SEED (set up the main bg copy first)"
    M fetch -q origin || die "fetch failed"
    M worktree add -q -b "bg/$NAME" "$(winpath "$DIR")" origin/master || die "worktree add failed"
    git -C "$(winpath "$DIR")" branch -q --set-upstream-to=origin/master "bg/$NAME" 2>/dev/null
    info "seeding Library from $(winpath "$SEED") (robocopy)"
    t0=$(date +%s)
    MSYS2_ARG_CONV_EXCL='*' robocopy "$(cygpath -w "$SEED")" "$(cygpath -w "$DIR/Library")" /E /R:0 /W:0 /MT:16 /NFL /NDL /NP /NJH \
      /XF EditorInstance.json ilpp.pid ArtifactDB-lock SourceAssetDB-lock /XD bg-save | tail -6
    rc=${PIPESTATUS[0]}; [ "$rc" -ge 8 ] && die "robocopy failed ($rc)"
    info "ready in $(( $(date +%s) - t0 ))s: $(winpath "$DIR")"
    info "use it: IG_BG=$(winpath "$DIR") bash tools/bg/compile.sh   (or --bg $(winpath "$DIR"))"
    ;;
  rm)
    DIR="${MAIN_ROOT}-bg-$NAME"; [ -d "$DIR" ] || die "no such task copy: $DIR" 2
    BG="$DIR"; DIRTY="$(bg_status)"
    if [ -n "$DIRTY" ] && [ $FORCE = 0 ]; then printf '%s\n' "$DIRTY" | head; die "uncommitted work in $DIR - publish it or pass --force" 2; fi
    M worktree remove --force "$(winpath "$DIR")" || die "worktree remove failed"
    M branch -q -D "bg/$NAME" 2>/dev/null
    info "removed $DIR (a pushed remote branch is kept)"
    ;;
  *) die "usage: task.sh new|rm|list <name>" 64 ;;
esac
