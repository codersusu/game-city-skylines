#!/bin/bash
set -euo pipefail
seabright_root="$(cd "$(dirname "$0")/.." && pwd)"
# Seal the final local bundle, including Unity's nested native libraries.
/usr/bin/codesign --force --deep --sign - "$seabright_root/Builds/Seabright.app"
/usr/bin/codesign --verify --deep --strict --verbose=2 "$seabright_root/Builds/Seabright.app"
