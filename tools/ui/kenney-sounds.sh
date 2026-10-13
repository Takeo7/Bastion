#!/bin/bash
# The interface sounds («Mixta», audio/ui.ts), taken from
# Kenney's «Interface Sounds» (CC0, assets-src/kenney/interface-sounds/Audio,
# not in the repo): only the ones in use, trimmed of silence, peak-normalised,
# mono WAV at 32 kHz (WAV decodes everywhere, Safari included, and has no
# encoder delay before a click). Run from the repo root: bash tools/ui/kenney-sounds.sh
set -euo pipefail
SRC=assets-src/kenney/interface-sounds/Audio
OUT=packages/client/public/audio/ui
mkdir -p "$OUT"
NAMES=(
  tick_004 click_001 select_002 switch_004 glass_006 maximize_007 confirmation_001 error_004
)
for n in "${NAMES[@]}"; do
  trim="silenceremove=start_periods=1:start_threshold=-55dB,areverse,silenceremove=start_periods=1:start_threshold=-55dB,areverse"
  peak=$(ffmpeg -v info -i "$SRC/$n.ogg" -af "$trim,volumedetect" -f null - 2>&1 | sed -n 's/.*max_volume: \(-*[0-9.]*\) dB/\1/p')
  gain=$(python3 -c "print(round(-3 - float('$peak'), 2))")
  ffmpeg -v error -y -i "$SRC/$n.ogg" -af "$trim,volume=${gain}dB,afade=t=in:d=0.002,areverse,afade=t=in:d=0.006,areverse" -ac 1 -ar 32000 -c:a pcm_s16le "$OUT/$n.wav"
done
ls -la "$OUT" | awk '{s+=$5} END {print "total bytes", s}'
