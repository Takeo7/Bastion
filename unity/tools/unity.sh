#!/bin/bash
# Unity on the project in batchmode (docs/UNITY-ARTE.md §6): unity/tools/unity.sh [Unity's arguments…]
# With none, it opens the project, imports and compiles, and quits: the check that it builds. One Unity at a time on
# the project: it waits for any other run of this script, and Unity itself refuses if the editor has it open.
# The log goes to unity/Bastion/Logs/batch.log (LOG=… for another); its errors are printed at the end.
# A Unity killed by the timeout leaves the project as it was; the lock goes with this script.
set -u
tools=$(cd "$(dirname "$0")" && pwd)
project="$tools/../Bastion"
unity=${UNITY:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}
log=${LOG:-$project/Logs/batch.log}
lock=/tmp/bastion-unity.lock
mkdir -p "$(dirname "$log")"
until mkdir "$lock" 2>/dev/null; do sleep 2; done
trap 'rmdir "$lock"' EXIT
# QUIT=0: Unity stays open until what it runs quits it (the captures in play mode). TIMEOUT: seconds before it is
# stopped (a run stuck in play mode would never end), 900 unless said.
quit=-quit
[ "${QUIT:-1}" = 0 ] && quit=
"$unity" -batchmode $quit -projectPath "$project" -logFile "$log" "$@" &
pid=$!
( sleep "${TIMEOUT:-900}"; kill "$pid" 2>/dev/null && echo "Unity parado: más de ${TIMEOUT:-900} s" ) 2>/dev/null &
watch=$!
wait "$pid"
code=$?
# The watch's sleep too: left running it keeps this script's output open (a pipe after it would wait it out).
pkill -P "$watch" 2>/dev/null
kill "$watch" 2>/dev/null
wait "$watch" 2>/dev/null
grep -E "error CS[0-9]+|\[shots\]|Exception|Compilation failed|Scripts have compiler errors" "$log" | sort -u | head -40
echo "registro: $log (salida $code)"
exit $code
