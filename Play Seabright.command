#!/bin/bash
set -euo pipefail
seabright_root="$(cd "$(dirname "$0")" && pwd)"
open "$seabright_root/Builds/Seabright.app"
