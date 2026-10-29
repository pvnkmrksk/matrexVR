#!/bin/zsh
set -e
preview_root="${0:A:h}"
preview_app="$preview_root/Preview/MATREX VR.app"
if [[ ! -d "$preview_app" ]]; then
  print "Build the local preview first: python3 tools/build_local_desktop.py"
  print "Expected application: $preview_app"
  read -k 1 "?Press any key to close."
  exit 1
fi
open -n "$preview_app" --args --config-dir "$preview_root/Preview/Config" --data-dir "$preview_root/Preview/RunData" -configure
