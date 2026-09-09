#!/bin/bash
set -euo pipefail
seabright_root="$(cd "$(dirname "$0")/.." && pwd)"
exec "$seabright_root/Builds/Seabright.app/Contents/MacOS/Seabright" \
  -autoplay -quitAfterAutoplay -artifacts "$seabright_root/Artifacts" \
  -screen-width 1600 -screen-height 1000 -screen-fullscreen 0 \
  -logFile "$seabright_root/Artifacts/player.log"
