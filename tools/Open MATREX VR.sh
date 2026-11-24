#!/usr/bin/env bash
set -eu

folder=$(cd -- "$(dirname -- "$0")" && pwd -P)
if [ "${1:-}" = "--show-paths" ]; then
    printf 'Player: %s\nConfig: %s\nRunData: %s\nCache: %s\nPreferences: %s\n' \
        "$folder/MATREX VR.x86_64" "$folder/Config" "$folder/RunData" "$folder/Cache" "$folder/Preferences"
    exit 0
fi

cd -- "$folder"
export XDG_CONFIG_HOME="$folder/Preferences"
export XDG_CACHE_HOME="$folder/Cache"
mkdir -p -- "$XDG_CONFIG_HOME" "$XDG_CACHE_HOME"
exec "$folder/MATREX VR.x86_64" "$@" --config-dir "$folder/Config" --data-dir "$folder/RunData" -configure
