#!/bin/bash
# A version, from tested to published (the review of 10/11, row 20: these steps lived in a session's temporary folder,
# one copy per version). It runs in the clean copy of the repository, the one without anybody's half-done work, with
# the roles' patches already applied and docs/NOTAS-<version>.md from Lore in place:
#   unity/tools/publicar.sh 0.12.19 probar                    tests, compile, CombatLab.Close, the Mac and Windows apps,
#                                                              and the Mac app measured in a city and in the base
#   unity/tools/publicar.sh 0.12.19 publicar <commit.txt>     commit with that message, push, the GitHub release
#   unity/tools/publicar.sh 0.12.19 principal                 the downloads and the Mac app to the main tree, the main
#                                                              tree onto the published commit, and copia.sh
# «publicar» refuses to go on unless «probar» ended clean for this same tree (it leaves a mark with the tree's state).
#   PRINCIPAL=<folder>   the main tree, where everybody works (default: ../Bastion, next to this copy)
#   SALIDA=<folder>      logs, captures and the release's files (default: unity/Bastion/Builds/publicar/<version>)
#   BASTION_FIRMA=<id>   the Mac app's signing identity (docs/FIRMA.md); unset or empty, ad hoc. The 0.12.16–0.12.18
#                        went out signed with Arturo's «Apple Development» certificate, to see whether macOS stopped
#                        asking about the firewall and the local network at each version: it didn't (Arturo, 10/11)
#   REPO=<owner/name>    default: Takeo7/Bastion
# The main tree's part never touches a working file there except the downloads, the app and bundleVersion: after it,
# `git status` in the main tree shows what the roles have that the version doesn't. Put the version's patches in the
# main tree before, or they show up backwards.
set -u
root="$(cd "$(dirname "$0")/../.." && pwd)"
version=${1:-}
step=${2:-}
if ! [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || [ -z "$step" ]; then
  echo "Uso: unity/tools/publicar.sh <versión> probar | publicar <commit.txt> | principal"
  exit 2
fi
main=${PRINCIPAL:-$(dirname "$root")/Bastion}
out=${SALIDA:-$root/unity/Bastion/Builds/publicar/$version}
repo=${REPO:-Takeo7/Bastion}
sign=${BASTION_FIRMA-}
settings=$root/unity/Bastion/ProjectSettings/ProjectSettings.asset
notes=$root/docs/NOTAS-$version.md
logs=$root/unity/Bastion/Logs
mark=$out/probada.txt
mkdir -p "$out" || exit 1
cd "$root" || exit 1

# What «probar» tested: the commit and every change on top of it.
tree_state() {
  git rev-parse HEAD
  git status --porcelain
  git diff HEAD | shasum | cut -d' ' -f1
  git ls-files -o --exclude-standard -z | xargs -0 shasum 2>/dev/null | shasum | cut -d' ' -f1
}

title() { sed -n 's/^\*\*Título:\*\* *//p' "$notes" | head -1; }

probar() {
  if [ "$root" = "$main" ]; then echo "Esto es el árbol principal: la versión se compila en la copia limpia."; exit 2; fi
  if [ ! -f "$notes" ]; then echo "Falta docs/NOTAS-$version.md (Lore)."; exit 2; fi
  if [ -z "$(title)" ]; then echo "docs/NOTAS-$version.md no lleva «**Título:**»."; exit 2; fi
  if [ -n "$sign" ] && [ "$(security find-identity -v -p codesigning | grep -c -F "$sign")" != 1 ]; then
    echo "La identidad «$sign» no está una sola vez en el llavero (security find-identity -v -p codesigning)."
    exit 2
  fi
  rm -f "$mark"
  # The version, in the project: it goes in the commit.
  sed -i '' "s/^  bundleVersion: .*$/  bundleVersion: $version/" "$settings"
  grep -q "^  bundleVersion: $version$" "$settings" || { echo "No he podido poner bundleVersion en $version."; exit 1; }

  echo "== pruebas del motor"
  dotnet test engine-cs/tests -nologo > "$out/pruebas.log" 2>&1
  code=$?
  tail -1 "$out/pruebas.log"
  if [ $code != 0 ]; then echo "Las pruebas fallan: $out/pruebas.log"; exit 1; fi

  # What FigureCatalogMake gives back here against Arte's, in the main tree (none of the three is in git).
  if [ -d "$main" ]; then
    for f in FigureCatalog.asset Undead.json Wardrobe.json; do
      r=unity/Bastion/Assets/Bastion/Client/Resources/$f
      cmp -s "$main/$r" "$r" && echo "igual $f" || echo "DISTINTO $f (mirarlo con Arte)"
    done
  fi

  echo "== compilar"
  LOG=$logs/publicar-compilar.log unity/tools/unity.sh || exit 1

  echo "== CombatLab.Close"
  mkdir -p "$out/close"
  LOG=$logs/publicar-close.log unity/tools/captura.sh -shotSetup Bastion.EditorTools.CombatLab.Close -shotPlay \
    -shotCamera "Shot camera" -shotFrames 110 -shotFps 10 -shotOut "$out/close" || exit 1
  grep -h "\[combat\].*done" "$logs/publicar-close.log" | tail -1
  # Not ours: Unity's search index, and Sidekick's own check for a newer version of its tool, which GitHub answers with
  # a 403 when it is asked too often (0.12.19). An exception counts unless one of the lines after it names those.
  closeFaults=$(awk '
    /Exception/ && !/^[ \t]+at / && !/SearchDatabase|QuickSearch/ { if (pending) c++; pending = 1; at = NR; next }
    pending && /Synty\.SidekickCharacters\.ToolDownloader/ { pending = 0; next }
    pending && NR > at + 4 { c++; pending = 0 }
    END { if (pending) c++; print c + 0 }' "$logs/publicar-close.log")
  echo "excepciones en Close: $closeFaults"
  grep -q "\[combat\].*done" "$logs/publicar-close.log" || { echo "Close no ha llegado al final."; exit 1; }

  echo "== Mac"
  BASTION_FIRMA=$sign LOG=$logs/publicar-mac.log TIMEOUT=3600 unity/tools/unity.sh \
    -executeMethod Bastion.Client.Net.EditorTools.AppBuild.Mac || exit 1
  app=unity/Bastion/Builds/Mac/Bastion.app
  built=$(/usr/libexec/PlistBuddy -c "Print CFBundleShortVersionString" "$app/Contents/Info.plist")
  [ "$built" = "$version" ] || { echo "La app del Mac dice $built, no $version."; exit 1; }
  codesign -v --deep --strict "$app" || { echo "La firma de la app del Mac no pasa."; exit 1; }
  codesign -dvv "$app" 2>&1 | grep -E "^Authority|^Timestamp|^Signature" | head -3

  echo "== Windows"
  LOG=$logs/publicar-win.log TIMEOUT=3600 unity/tools/unity.sh \
    -executeMethod Bastion.Client.Net.EditorTools.AppBuild.Windows || exit 1
  ls -la unity/Bastion/Builds/Bastion-Mac.zip unity/Bastion/Builds/Bastion-Windows.zip || exit 1

  # The Mac app by itself, hosting on this machine with its own data: 90 s of combat in a city, 120 s in the base.
  appFaults=0
  for mode in ciudad base; do
    data=$out/app-$mode
    rm -rf "$data"; mkdir -p "$data"
    BASTION_DATOS=$data BASTION_ANFITRION=Medida BASTION_SEMILLA=7 BASTION_MEDIR=$mode \
      "$app/Contents/MacOS/Bastion" -logFile "$data/Player.log"
    code=$?
    faults=$(grep -c "Exception" "$data/Player.log")
    layout=$(grep -c "Layout update is struggling" "$data/Player.log")
    done_=$(grep -c "\[prueba\] medir: $mode terminado" "$data/red.log" 2>/dev/null)
    echo "== app, $mode: salida $code · excepciones $faults · avisos de layout $layout"
    grep -h "\[prueba\] medir" "$data/red.log"
    grep -h "\[rend Medida\] combate" "$data/red.log" | grep -oE "[0-9]+ tir(ón|ones)" | awk '{s+=$1} END {print "tirones en combate: " s+0}'
    if [ $code != 0 ] || [ "$faults" != 0 ] || [ "$layout" != 0 ] || [ "${done_:-0}" = 0 ]; then appFaults=1; fi
  done

  if [ "$closeFaults" != 0 ] || [ $appFaults != 0 ]; then
    echo "NO está para publicar: mirar las excepciones de arriba."
    exit 1
  fi
  tree_state > "$mark"
  echo "== probada: $version"
}

publicar() {
  message=${1:-}
  if [ ! -f "$message" ]; then echo "Falta el mensaje del commit: publicar.sh $version publicar <commit.txt>"; exit 2; fi
  if [ ! -f "$mark" ] || [ "$(tree_state)" != "$(cat "$mark")" ]; then
    echo "Este árbol no es el que pasó «probar» (o no lo pasó): unity/tools/publicar.sh $version probar"
    exit 2
  fi
  if [ "$(sed -n 2p "$message")" != "" ] || ! head -1 "$message" | grep -q "^Bastión $version: "; then
    echo "El mensaje empieza por «Bastión $version: …» en una línea, y la segunda va vacía."
    exit 2
  fi
  if gh release view "v$version" --repo "$repo" > /dev/null 2>&1; then echo "La v$version ya está publicada."; exit 2; fi
  git add -A && git commit -q -F "$message" || exit 1
  hash=$(git rev-parse --short HEAD)
  git push -q origin HEAD:main || { echo "El commit $hash está hecho, pero no se ha subido."; exit 1; }
  # The release's text: Lore's notes from the line after the title, with the commit.
  awk 'f{print} /^\*\*Título:\*\*/{f=1}' "$notes" | sed "s/\[commit\]/$hash/" | sed '1{/^$/d;}' > "$out/body.md"
  cp unity/Bastion/Builds/Bastion-Mac.zip "$out/Bastion-$version-mac.zip" || exit 1
  cp unity/Bastion/Builds/Bastion-Windows.zip "$out/Bastion-$version-win-x64.zip" || exit 1
  gh release create "v$version" --repo "$repo" --target main --title "$(title)" --notes-file "$out/body.md" \
    "$out/Bastion-$version-mac.zip" "$out/Bastion-$version-win-x64.zip" || exit 1
  echo "Mac: https://github.com/$repo/releases/download/v$version/Bastion-$version-mac.zip"
  echo "Falta: unity/tools/publicar.sh $version principal"
}

principal() {
  if [ ! -d "$main/.git" ] || [ "$root" = "$main" ]; then echo "No encuentro el árbol principal en $main (PRINCIPAL=…)."; exit 2; fi
  for zip in "Bastion-$version-mac.zip" "Bastion-$version-win-x64.zip"; do
    [ -f "$out/$zip" ] || { echo "Falta $out/$zip: primero «publicar»."; exit 2; }
  done
  # Where Arturo looks for the downloads, and the app he opens from the project.
  for folder in unity/Bastion/Builds/release packages/desktop/dist/release; do
    mkdir -p "$main/$folder" && cp "$out/Bastion-$version-mac.zip" "$out/Bastion-$version-win-x64.zip" "$main/$folder/" || exit 1
  done
  rm -rf "$main/unity/Bastion/Builds/Mac/Bastion.app"
  ditto unity/Bastion/Builds/Mac/Bastion.app "$main/unity/Bastion/Builds/Mac/Bastion.app" || exit 1
  codesign -v --deep --strict "$main/unity/Bastion/Builds/Mac/Bastion.app" || echo "La firma de la app copiada no pasa."
  # The main tree onto the published commit, keeping every working file as it is.
  git -C "$main" fetch -q origin && git -C "$main" reset -q origin/main || exit 1
  sed -i '' "s/^  bundleVersion: .*$/  bundleVersion: $version/" "$main/unity/Bastion/ProjectSettings/ProjectSettings.asset"
  echo "El árbol principal, en $(git -C "$main" log --oneline -1 | cut -c1-80)"
  echo "Archivos de la versión que allí son distintos (trabajo de los roles, o un parche sin poner):"
  git show --name-only --format= HEAD | while IFS= read -r f; do
    cmp -s "$f" "$main/$f" || echo "  $f"
  done | grep -v "^  engine-cs/golden/" | head -60
  "$main/unity/tools/copia.sh"
  [ $? = 2 ] && echo "La copia al disco externo queda pendiente."
  echo "== hecho: $version"
}

case "$step" in
  probar) probar ;;
  publicar) publicar "${3:-}" ;;
  principal) principal ;;
  *) echo "No sé qué es «$step»: probar, publicar o principal."; exit 2 ;;
esac
