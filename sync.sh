#!/usr/bin/env bash
# FarmSense deploy helper: copy the built runtime tree into a mods folder.
#
# Usage:
#   bash sync.sh                                  # <Game>/Mods/FarmSense
#   bash sync.sh "/path/to/Mods"                  # explicit mods folder
#   MODS_DIR="/path/to/Mods" bash sync.sh
#   bash sync.sh --yes "/path/to/Mods"            # skip the 5 second countdown
#
# Dry run first, then a real rsync of this one mod folder only.

set -u

MOD_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC="$MOD_ROOT/dist/FarmSense/"
MODE="${1:-}"

if [ "$MODE" = "--yes" ]; then
    shift
fi

find_game_dir() {
    local candidates=(
        "${GAME_DIR:-}"
        "$HOME/.steam/steam/steamapps/common/7 Days To Die"
        "$HOME/.local/share/Steam/steamapps/common/7 Days To Die"
        "$HOME/.steam/steam/steamapps/common/7 Days to Die"
        "/mnt/c/Program Files (x86)/Steam/steamapps/common/7 Days To Die"
    )
    local c
    for c in "${candidates[@]}"; do
        [ -n "$c" ] || continue
        if [ -d "$c/7DaysToDie_Data/Managed" ]; then
            printf '%s\n' "$c"
            return 0
        fi
    done
    return 1
}

if [ -n "${1:-}" ]; then
    MODS_DIR="$1"
elif [ -n "${MODS_DIR:-}" ]; then
    MODS_DIR="$MODS_DIR"
else
    GAME="$(find_game_dir || true)"
    if [ -z "$GAME" ]; then
        echo "ERROR: no mods folder given and no game installation found." >&2
        echo "Use:  bash sync.sh \"/path/to/Mods\"" >&2
        exit 1
    fi
    MODS_DIR="$GAME/Mods"
fi

DST="$MODS_DIR/FarmSense/"

if [ ! -d "$SRC" ]; then
    echo "ERROR: $SRC is missing - run ./build.sh first." >&2
    exit 1
fi

mkdir -p "$DST"

EXCLUDES=(--exclude "0Harmony.dll" --exclude "Harmony*.dll" --exclude "MonoMod*" --exclude "Mono.Cecil*")

echo "Source: $SRC"
echo "Target: $DST"
echo
echo "== Dry run =="
rsync -n -ai --delete "${EXCLUDES[@]}" "$SRC" "$DST"

echo
if [ "$MODE" = "--yes" ]; then
    echo "== Real sync (--yes) =="
else
    echo "== Real sync in 5 seconds - press Ctrl+C to abort =="
    sleep 5
fi

rsync -ai --delete "${EXCLUDES[@]}" "$SRC" "$DST"

echo
echo "== Verification (source vs. target) =="
sha256sum "$SRC/FarmSense.dll"
sha256sum "$DST/FarmSense.dll"
