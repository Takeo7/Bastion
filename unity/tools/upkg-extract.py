# Copies assets out of a .unitypackage into a project, with their .meta (the same GUIDs as Unity's own import), without
# Unity, so it works whatever state the project's scripts are in; Unity imports them the next time it opens:
#   python3 -I upkg-extract.py <package> <project folder> [asset path…]   (paths as in the package: Assets/Synty/…)
#   python3 -I upkg-extract.py --only Sprites,Textures <package> <project folder>
#   python3 -I upkg-extract.py --under Assets/Synty/<pack folder> <package> <project folder>
# With no paths, the whole package but its demo scenes and samples (Assets/<…>/<pack>/Scenes, Samples); with --only,
# only what lies in folders of those names (and the folders above them, for their .meta); with --under, only what lies
# under that folder (and the folder itself), but its demo scenes and samples.
import os, sys, tarfile
args = sys.argv[1:]
only = None
under = None
if args[:1] == ['--only']:
    only = set(args[1].split(','))
    args = args[2:]
if args[:1] == ['--under']:
    under = args[1].rstrip('/')
    args = args[2:]
package, project, wanted = args[0], args[1], set(args[2:])

def keep(path):
    if wanted:
        return path in wanted
    if under is not None and path != under and not path.startswith(under + '/'):
        return False
    parts = path.split('/')
    if only is not None:
        # In one of those folders, or a folder on the way to one (kept for its .meta; empty ones go below).
        return any(p in only for p in parts[3:]) or len(parts) <= 3
    return not any(p in ('Scenes', 'Samples') for p in parts[3:4])

with tarfile.open(package, 'r:gz') as t:
    folders = {}
    members = t.getmembers()
    for m in members:
        if m.name.endswith('/pathname'):
            path = t.extractfile(m).read().decode('utf-8').splitlines()[0]
            if keep(path):
                folders[m.name.split('/')[0]] = path
    written = 0
    for m in members:
        folder, _, part = m.name.partition('/')
        if folder in folders and part in ('asset', 'asset.meta'):
            out = os.path.join(project, folders[folder] + ('.meta' if part == 'asset.meta' else ''))
            os.makedirs(os.path.dirname(out), exist_ok=True)
            with open(out, 'wb') as f:
                f.write(t.extractfile(m).read())
            written += 1
            if wanted:
                print(out)
    # A folder is a .meta with no asset (a folder carries a GUID too): the folder made, even empty, for its .meta.
    with_asset = {m.name.partition('/')[0] for m in members if m.name.endswith('/asset')}
    for folder, path in folders.items():
        if folder not in with_asset:
            os.makedirs(os.path.join(project, path), exist_ok=True)
print(f'{written} files from {os.path.basename(package)}')
missing = wanted - set(folders.values())
if missing:
    sys.exit('not in the package: ' + ', '.join(sorted(missing)))
