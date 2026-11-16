#!/bin/bash
# Bastión for iOS (docs/IOS.md; Arturo, 7/11): unity/tools/ios.sh simulador|aparato [Unity's arguments…]
#
# 1. The project's copy for iOS (IOS_COPIA, ../Bastion-ios/unity/Bastion beside the repository) brought up to date from
#    this one: Assets, Packages and ProjectSettings, not Library (it keeps its own, imported for iOS). Its Assets/IosLocal/
#    is the copy's own (tests that only run there) and is left alone. Building for iOS in the main project would switch
#    its platform, and every session would import it all again.
# 2. Unity on the copy, -buildTarget iOS: AppBuild.IosSimulator (Builds/iOS-sim) or IosDevice (Builds/iOS), the Xcode
#    project. Its log: the copy's Logs/ios-build.log.
# 3. xcodebuild: for the simulator unsigned; for the device signed by the team EQUIPO (Arturo's personal one, with his
#    Apple ID in Xcode > Settings > Accounts: -allowProvisioningUpdates makes the profile, and
#    -allowProvisioningDeviceRegistration puts a new device in it). Its log: the copy's Logs/xcodebuild-<mode>.log.
# 4. Installed: in the simulator SIMULADOR (the «Bastion iOS Duo» unless said), or on the device DISPOSITIVO (its id in
#    xcrun devicectl list devices). ABRIR=1 opens it too.
# SOLO_UNITY=1 stops after 2; SOLO_XCODE=1 starts at 3, on the Xcode project already made.
#
# One at a time on the copy: its own lock, /tmp/bastion-ios-unity.lock, which whoever runs Unity on the copy takes too.
# The main project's lock (unity.sh) isn't needed: they are two projects.
set -u
mode=${1:-}
case "$mode" in
  simulador) method=IosSimulator; out=iOS-sim ;;
  aparato) method=IosDevice; out=iOS ;;
  *) echo "uso: unity/tools/ios.sh simulador|aparato [argumentos de Unity…]"; exit 2 ;;
esac
shift
tools=$(cd "$(dirname "$0")" && pwd)
project=$(cd "$tools/../Bastion" && pwd)
copy=${IOS_COPIA:-$(cd "$tools/../../.." && pwd)/Bastion-ios/unity/Bastion}
unity=${UNITY:-/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity}
sim=${SIMULADOR:-229BDFD9-4440-459D-9ED7-62BAE78C28C6}
team=${EQUIPO:-}
lock=/tmp/bastion-ios-unity.lock
id=com.takeo7.bastion
if [ ! -d "$copy/Library" ]; then echo "no está la copia para iOS en $copy (docs/IOS.md §4)"; exit 1; fi
if [ "$mode" = aparato ] && [ -z "$team" ]; then echo "para el aparato hace falta EQUIPO=<id del equipo personal de Arturo>"; exit 2; fi
until mkdir "$lock" 2>/dev/null; do sleep 2; done
trap 'rmdir "$lock"' EXIT

if [ "${SOLO_XCODE:-0}" != 1 ]; then
started=$(date +%s)
for part in Assets Packages ProjectSettings; do
  rsync -a --delete --exclude '/IosLocal' --exclude '/IosLocal.meta' --exclude '.DS_Store' "$project/$part/" "$copy/$part/" || { echo "no se puso al día $part"; exit 1; }
done
# The icon's tool, which AppIcon finds beside the project (../tools/icono).
mkdir -p "$copy/../tools"
rsync -a --delete "$tools/icono/" "$copy/../tools/icono/" || { echo "no se copió tools/icono"; exit 1; }
echo "copia al día ($copy) en $(( $(date +%s) - started )) s"

log="$copy/Logs/ios-build.log"
started=$(date +%s)
extra=()
[ -n "$team" ] && extra=(-equipo "$team")
"$unity" -batchmode -quit -projectPath "$copy" -buildTarget iOS -logFile "$log" -executeMethod "Bastion.Client.Net.EditorTools.AppBuild.$method" ${extra[@]+"${extra[@]}"} "$@" &
pid=$!
( sleep "${TIMEOUT:-2400}"; kill "$pid" 2>/dev/null && echo "Unity parado: más de ${TIMEOUT:-2400} s" ) 2>/dev/null &
watch=$!
wait "$pid"
code=$?
pkill -P "$watch" 2>/dev/null
kill "$watch" 2>/dev/null
wait "$watch" 2>/dev/null
grep -E "^\[red\]|error CS[0-9]+|Exception|Compilation failed|Scripts have compiler errors" "$log" | sort -u | head -40
echo "Unity: salida $code en $(( $(date +%s) - started )) s (registro: $log)"
[ $code -eq 0 ] || exit $code
[ "${SOLO_UNITY:-0}" = 1 ] && exit 0
fi

xlog="$copy/Logs/xcodebuild-$mode.log"
started=$(date +%s)
cd "$copy/Builds/$out" || exit 1
if [ "$mode" = simulador ]; then
  xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -sdk iphonesimulator \
    -destination "platform=iOS Simulator,id=$sim" -derivedDataPath "../$out-dd" CODE_SIGNING_ALLOWED=NO > "$xlog" 2>&1
  code=$?
  app="$copy/Builds/$out-dd/Build/Products/Release-iphonesimulator/Bastion.app"
else
  xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -sdk iphoneos \
    -destination "generic/platform=iOS" -derivedDataPath "../$out-dd" -allowProvisioningUpdates -allowProvisioningDeviceRegistration \
    DEVELOPMENT_TEAM="$team" CODE_SIGN_STYLE=Automatic > "$xlog" 2>&1
  code=$?
  app="$copy/Builds/$out-dd/Build/Products/Release-iphoneos/Bastion.app"
fi
grep -E "error:|\*\* BUILD" "$xlog" | sort -u | head -20
echo "xcodebuild: salida $code en $(( $(date +%s) - started )) s (registro: $xlog)"
[ $code -eq 0 ] || exit $code
echo "app: $app"

if [ "$mode" = simulador ]; then
  xcrun simctl boot "$sim" 2>/dev/null
  xcrun simctl install "$sim" "$app" || exit 1
  echo "instalada en el simulador $sim"
  [ "${ABRIR:-0}" = 1 ] && xcrun simctl launch "$sim" "$id"
elif [ -n "${DISPOSITIVO:-}" ]; then
  xcrun devicectl device install app --device "$DISPOSITIVO" "$app" || exit 1
  echo "instalada en $DISPOSITIVO"
  [ "${ABRIR:-0}" = 1 ] && xcrun devicectl device process launch --device "$DISPOSITIVO" "$id"
fi
exit 0
