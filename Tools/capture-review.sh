#!/bin/bash
set -euo pipefail
seabright_root="$(cd "$(dirname "$0")/.." && pwd)"
seabright_unity="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity}"
mkdir -p "$seabright_root/Artifacts"
exec "$seabright_unity" -batchmode -projectPath "$seabright_root" \
  -executeMethod Seabright.Editor.ReviewCaptureTasks.Capture \
  -logFile "$seabright_root/Artifacts/review-capture.log"
