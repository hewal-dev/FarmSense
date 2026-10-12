#!/usr/bin/env bash
# FarmSense Build Script (7 Days to Die v3.x, Linux / Mono)
#
# Usage:
#   bash build.sh                    # auto-detect the game directory
#   bash build.sh "/path/to/game"    # point it explicitly
#   GAME_DIR="/path/to/game" bash build.sh
#
# Requires Mono (`mcs`). On Debian/Ubuntu: sudo apt install mono-devel
#
# The game assemblies are referenced for compilation only and are never copied.
# Harmony comes exclusively from the game installation (<Game>/Mods/0_TFP_Harmony/)
# and is never part of the build output.

set -u

MOD_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_DIR="$MOD_ROOT/src"
BIN_DIR="$MOD_ROOT/bin"
DIST="$MOD_ROOT/dist/FarmSense"
OUT_DLL="$BIN_DIR/FarmSense.dll"

# ---------------------------------------------------------------------------
# 1) Locate the game directory
# ---------------------------------------------------------------------------
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

GAME_DIR="${1:-$(find_game_dir || true)}"

if [ -z "$GAME_DIR" ] || [ ! -d "$GAME_DIR/7DaysToDie_Data/Managed" ]; then
    echo "ERROR: game directory not found." >&2
    echo "Pass it explicitly:  bash build.sh \"/path/to/7 Days To Die\"" >&2
    echo "or:                  GAME_DIR=\"/path/to/7 Days To Die\" bash build.sh" >&2
    exit 1
fi

MANAGED="$GAME_DIR/7DaysToDie_Data/Managed"
HARMONY="${HARMONY:-$GAME_DIR/Mods/0_TFP_Harmony/0Harmony.dll}"

echo "=== FarmSense Build ==="
echo "Game directory: $GAME_DIR"
echo "Output:         $OUT_DLL"
echo

# ---------------------------------------------------------------------------
# 2) Check prerequisites (clear message instead of a compiler stack trace)
# ---------------------------------------------------------------------------
if ! command -v mcs >/dev/null 2>&1; then
    echo "ERROR: 'mcs' (Mono C# compiler) not found." >&2
    echo "Install it with: sudo apt install mono-devel   (or your distribution's package)" >&2
    exit 1
fi

if ! ls "$SRC_DIR"/*.cs >/dev/null 2>&1; then
    echo "ERROR: no source files in $SRC_DIR" >&2
    exit 1
fi

MISSING=0
for req in Assembly-CSharp.dll LogLibrary.dll UnityEngine.dll UnityEngine.CoreModule.dll \
           UnityEngine.IMGUIModule.dll UnityEngine.UI.dll UnityEngine.UIModule.dll \
           UnityEngine.AudioModule.dll; do
    if [ ! -f "$MANAGED/$req" ]; then
        echo "ERROR: missing reference: $MANAGED/$req" >&2
        echo "       Is the game directory correct? Expected <Game>/7DaysToDie_Data/Managed." >&2
        MISSING=1
    fi
done
[ "$MISSING" -eq 0 ] || exit 1

# Harmony lives in the game installation, not in the mod folder.
if [ ! -f "$HARMONY" ]; then
    FOUND="$(find "$GAME_DIR" -name '0Harmony.dll' 2>/dev/null | head -1)"
    if [ -n "$FOUND" ]; then
        HARMONY="$FOUND"
    else
        echo "ERROR: 0Harmony.dll not found." >&2
        echo "Expected: $GAME_DIR/Mods/0_TFP_Harmony/0Harmony.dll" >&2
        echo "It belongs to the game installation (Harmony 2.13) and must NOT be copied into a mod folder." >&2
        echo "Alternatively pass HARMONY=/path/to/0Harmony.dll" >&2
        exit 1
    fi
fi

NETSTANDARD="${NETSTANDARD:-$(find /usr/lib/mono -name "netstandard.dll" -path "*/4.8-api/*" 2>/dev/null | head -1)}"
if [ -z "$NETSTANDARD" ]; then
    NETSTANDARD="$(find /usr/lib/mono -name "netstandard.dll" 2>/dev/null | head -1)"
fi
if [ -z "$NETSTANDARD" ]; then
    echo "ERROR: netstandard.dll not found (check your Mono installation)." >&2
    exit 1
fi

# ---------------------------------------------------------------------------
# 3) Compile
# ---------------------------------------------------------------------------
mkdir -p "$BIN_DIR" "$DIST"

mcs -target:library \
    -out:"$OUT_DLL" \
    -lib:"$MANAGED" \
    -r:Assembly-CSharp.dll \
    -r:LogLibrary.dll \
    -r:UnityEngine.dll \
    -r:UnityEngine.CoreModule.dll \
    -r:UnityEngine.IMGUIModule.dll \
    -r:UnityEngine.UI.dll \
    -r:UnityEngine.UIModule.dll \
    -r:UnityEngine.InputModule.dll \
    -r:UnityEngine.InputLegacyModule.dll \
    -r:UnityEngine.TextRenderingModule.dll \
    -r:UnityEngine.AudioModule.dll \
    -r:UnityEngine.AnimationModule.dll \
    -r:"$HARMONY" \
    -r:"$NETSTANDARD" \
    "$SRC_DIR"/*.cs || { echo "BUILD FAILED" >&2; exit 1; }

# ---------------------------------------------------------------------------
# 4) Build the runtime tree (ready to copy into a mods folder)
# ---------------------------------------------------------------------------
cp "$OUT_DLL" "$DIST/FarmSense.dll"
cp "$MOD_ROOT/ModInfo.xml" "$DIST/ModInfo.xml"
cp "$MOD_ROOT/Config.xml" "$DIST/Config.xml"
rm -rf "$DIST/Config"
cp -r "$MOD_ROOT/Config" "$DIST/Config"
rm -f "$DIST"/Config/*.bak 2>/dev/null || true

# Never ship Harmony: 7 Days to Die provides it itself
# (<Mods>/0_TFP_Harmony/0Harmony.dll, Harmony 2.13). ModManager loads EVERY *.dll in a
# mod folder; a second copy with the same simple name causes startup errors on some setups.
rm -f "$DIST/0Harmony.dll"
if find "$DIST" -type f \( -iname '*harmony*.dll' -o -iname 'monomod*' -o -iname 'mono.cecil*' \) | grep -q .; then
    echo "ERROR: Harmony/MonoMod DLL inside the runtime tree ($DIST) - never ship it." >&2
    exit 1
fi

echo
echo "BUILD SUCCESSFUL"
sha256sum "$OUT_DLL"
echo
printf 'Packaged %s (without Harmony)\n' "$DIST"
echo
echo "Installation: copy the folder"
echo "  $DIST"
echo "to"
echo "  <Game>/Mods/FarmSense/"
