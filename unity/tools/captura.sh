#!/bin/bash
# Captures from Unity in batchmode (Bastion.EditorTools.Shots; docs/UNITY-ARTE.md §6):
#   unity/tools/captura.sh -shotOut <file.png | folder> [-shotScene …] [-shotSetup …] [-shotSize …] [-shotFrames … -shotFps …]
# In play mode (-shotPlay) the tool quits Unity itself once the shots are taken.
case " $* " in *" -shotPlay "*) export QUIT=0 ;; esac
exec "$(dirname "$0")/unity.sh" -executeMethod Bastion.EditorTools.Shots.Run "$@"
