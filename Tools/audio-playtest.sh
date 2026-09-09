#!/bin/bash
set -euo pipefail
seabright_root="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$seabright_root/Artifacts"
exec "$seabright_root/Builds/Seabright.app/Contents/MacOS/Seabright" \
  -audioTest -quitAfterAudioTest -artifacts "$seabright_root/Artifacts" \
  -screen-width 1600 -screen-height 1000 -screen-fullscreen 0 \
  -logFile "$seabright_root/Artifacts/audio-player.log"
