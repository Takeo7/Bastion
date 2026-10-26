#!/bin/bash
# The game's sound for Unity, generated from the 0.10.1's (docs/UNITY-SONIDO.md): unity/tools/sonido.sh [--salida <dir>]
# [--comprobar]. Into unity/Bastion/Assets/Bastion/Client/Audio/Resources/Generado/ unless told, which git leaves out.
# Needs Node, the repository's node_modules (esbuild, packages/desktop's Electron) and Homebrew's ffmpeg.
set -eu
cd "$(dirname "$0")/../.."
command -v ffmpeg >/dev/null || { echo "Falta ffmpeg (brew install ffmpeg)."; exit 1; }
[ -x node_modules/.bin/electron ] || { echo "Falta node_modules (npm install en la raíz del repositorio)."; exit 1; }
exec node unity/tools/sonido/generar.mjs "$@"
