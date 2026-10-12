#!/usr/bin/env bash
# Open the background copy headless, import/compile everything, report compile errors.
#   bash tools/bg/compile.sh            (exit 0 = compiled clean)
exec bash "$(dirname "$0")/unity.sh" IdleGrounds.Editor.Batch.Compile --allow-exceptions "$@"
