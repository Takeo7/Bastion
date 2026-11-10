#!/bin/bash
# A copy, on the external disk, of what lives outside git and only on this Mac (Arturo, 10/11; the review of 10/11, row 19):
#   unity/tools/copia.sh
#   DEST=<folder> unity/tools/copia.sh     another place (default: /Volumes/4T_White/40_BACKUPS/Bastion)
# What it copies:
#   trabajo/<date>/   the files changed or new since the last commit (with HEAD.txt and cambios.patch, to put them back);
#   dev-snapshots/    .dev-snapshots: each role's notes, patches and captures;
#   assets-src/       the Synty originals and the Mixamo clips, which only Arturo can download;
#   memoria/          the sessions' memory (~/.claude/projects/<this project>/memory);
#   ios/              ../Bastion-ios, the iOS copy of the project, without what Unity rebuilds.
# It never deletes anything on the disk: a file gone from here stays there. Without the disk it says so and exits 2,
# so a release goes on without it. Run when each version is published (docs/UNITY.md).
set -u
root="$(cd "$(dirname "$0")/../.." && pwd)"
dest=${DEST:-/Volumes/4T_White/40_BACKUPS/Bastion}
if [ ! -d "$(dirname "$dest")" ]; then
  echo "Sin copia: no está $(dirname "$dest") (¿el disco externo, sin montar?)."
  exit 2
fi
mkdir -p "$dest" || exit 1
day=$(date +%Y-%m-%d)
fail=0

# The work without commit: the files themselves, and the patch against HEAD.
work="$dest/trabajo/$day"
mkdir -p "$work"
git -C "$root" rev-parse HEAD > "$work/HEAD.txt"
git -C "$root" diff --binary HEAD > "$work/cambios.patch"
git -C "$root" ls-files -z -m -o --exclude-standard | (cd "$root" && while IFS= read -r -d '' f; do [ -e "$f" ] && printf '%s\0' "$f"; done) \
  | rsync -a --from0 --files-from=- "$root/" "$work/" || fail=1
echo "trabajo/$day: $(find "$work" -type f | wc -l | tr -d ' ') archivos"

copy() { # copy <name> <from> [rsync options…]
  local name=$1 from=$2
  shift 2
  if [ ! -d "$from" ]; then echo "$name: no está $from"; return; fi
  mkdir -p "$dest/$name"
  rsync -a "$@" "$from/" "$dest/$name/" || fail=1
  echo "$name: $(du -sh "$dest/$name" | cut -f1)"
}
copy dev-snapshots "$root/.dev-snapshots"
copy assets-src "$root/assets-src"
copy memoria "$HOME/.claude/projects/$(echo "$root" | tr / -)/memory"
copy ios "$(dirname "$root")/Bastion-ios" --exclude Library --exclude Temp --exclude Logs --exclude Builds --exclude UserSettings \
  --exclude bin --exclude obj --exclude node_modules --exclude "Assets/Synty" --exclude "Assets/Mixamo" --exclude Generado

if [ $fail = 0 ]; then echo "Copia hecha en $dest"; else echo "Copia con errores en $dest"; fi
exit $fail
