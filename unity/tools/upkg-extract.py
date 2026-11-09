# Copies assets out of a .unitypackage into a project, with their .meta (the same GUIDs as Unity's own import), without
# Unity, so it works whatever state the project's scripts are in; Unity imports them the next time it opens:
#   python3 -I upkg-extract.py <package> <project folder> [asset path…]   (paths as in the package: Assets/Synty/…)
#   python3 -I upkg-extract.py --only Sprites,Textures <package> <project folder>
#   python3 -I upkg-extract.py --under Assets/Synty/<pack folder> <package> <project folder>
#   python3 -I upkg-extract.py --move Assets/<its folder>=Assets/Synty/<ours> --under Assets/<its folder> <package> <project>
#   python3 -I upkg-extract.py --refs <package> <project folder> <asset path…>
# With no paths, the whole package but its demo scenes and samples (Assets/<…>/<pack>/Scenes, Samples); with --only,
# only what lies in folders of those names (and the folders above them, for their .meta); with --under, only what lies
# under that folder (and the folder itself), but its demo scenes and samples; with --move, what lies under the first
# folder written under the second instead (a pack outside Assets/Synty, kept off GitHub with the rest: its GUIDs go with
# its .meta, so what it refers to it still finds); with --refs, the paths given and every asset of the package they
# refer to, and those refer to (by GUID, in prefabs, materials and assets), but the PolygonGeneric each pack brings
# (the other packs' is already in, and its shaders are shared), with the folders they are in.
import os, re, sys, tarfile
args = sys.argv[1:]
refs = args[:1] == ['--refs']
if refs:
    args = args[1:]
only = None
under = None
move = None
if args[:1] == ['--move']:
    move = tuple(args[1].rstrip('/').split('=', 1))
    args = args[2:]
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
    if refs:
        path_of = {m.name.split('/')[0]: t.extractfile(m).read().decode('utf-8').splitlines()[0] for m in members if m.name.endswith('/pathname')}
        asset_of = {m.name.split('/')[0]: m for m in members if m.name.endswith('/asset') and m.isfile()}
        guid_of = {path: guid for guid, path in path_of.items()}
        seen, todo = set(), [guid_of[p] for p in wanted if p in guid_of]
        while todo:
            guid = todo.pop()
            if guid in seen or guid not in path_of or path_of[guid].startswith('Assets/Synty/PolygonGeneric/'):
                continue
            seen.add(guid)
            if guid in asset_of and path_of[guid].endswith(('.prefab', '.mat', '.asset')):
                todo += re.findall(r'guid: ([0-9a-f]{32})', t.extractfile(asset_of[guid]).read().decode('utf-8', 'replace'))
        found = {path_of[guid] for guid in seen}
        # And the folders they are in, for their .meta.
        found |= {'/'.join(p.split('/')[:k]) for p in found for k in range(3, p.count('/') + 1)} & set(path_of.values())
        wanted = found | (wanted - set(path_of.values()))
    for m in members:
        if m.name.endswith('/pathname'):
            path = t.extractfile(m).read().decode('utf-8').splitlines()[0]
            if keep(path):
                folders[m.name.split('/')[0]] = path
    written = 0
    for m in members:
        folder, _, part = m.name.partition('/')
        if folder in folders and part in ('asset', 'asset.meta'):
            path = folders[folder]
            if move and (path == move[0] or path.startswith(move[0] + '/')):
                path = move[1] + path[len(move[0]):]
            out = os.path.join(project, path + ('.meta' if part == 'asset.meta' else ''))
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
            if move and (path == move[0] or path.startswith(move[0] + '/')):
                path = move[1] + path[len(move[0]):]
            os.makedirs(os.path.join(project, path), exist_ok=True)
print(f'{written} files from {os.path.basename(package)}')
missing = wanted - set(folders.values())
if missing:
    sys.exit('not in the package: ' + ', '.join(sorted(missing)))
