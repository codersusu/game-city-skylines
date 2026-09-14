#!/bin/bash
set -euo pipefail
seabright_root="$(cd "$(dirname "$0")/.." && pwd)"
seabright_unity="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity}"
seabright_mode="${1:-build}"
case "$seabright_mode" in
  build) seabright_method="Seabright.Editor.BuildPipelineTasks.ValidateAndBuild" ;;
  validate) seabright_method="Seabright.Editor.BuildPipelineTasks.Validate" ;;
  package) seabright_method="Seabright.Editor.BuildPipelineTasks.BuildStandalone" ;;
  web) seabright_method="Seabright.Editor.BuildPipelineTasks.BuildWeb" ;;
  *) echo "Usage: Tools/build.sh [build|validate|package|web]" >&2; exit 2 ;;
esac
if [ ! -x "$seabright_unity" ]; then
  echo "Unity editor not found at $seabright_unity. Set UNITY_EDITOR to the executable path." >&2
  exit 2
fi
mkdir -p "$seabright_root/Artifacts"
seabright_target=()
if [ "$seabright_mode" = "web" ]; then seabright_target=(-buildTarget WebGL); fi
"$seabright_unity" -batchmode -quit "${seabright_target[@]}" -projectPath "$seabright_root" \
  -executeMethod "$seabright_method" -logFile "$seabright_root/Artifacts/unity-$seabright_mode.log"
if [ "$seabright_mode" = "build" ] || [ "$seabright_mode" = "package" ]; then
  bash "$seabright_root/Tools/sign-macos.sh"
fi
