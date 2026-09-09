#!/bin/bash
set -euo pipefail
seabright_root="$(cd "$(dirname "$0")/.." && pwd)"
seabright_unity="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity}"
export SEABRIGHT_FFMPEG="${SEABRIGHT_FFMPEG:-$seabright_root/Artifacts/Trailer/dependencies/imageio_ffmpeg/binaries/ffmpeg-macos-aarch64-v7.1}"
mkdir -p "$seabright_root/Artifacts/Trailer"
exec "$seabright_unity" -batchmode -projectPath "$seabright_root" \
  -executeMethod Seabright.Editor.TrailerCaptureTasks.Capture \
  -logFile "$seabright_root/Artifacts/Trailer/unity-capture.log" "$@"
