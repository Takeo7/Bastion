# The soldiers' wardrobe for Unity (docs/UNITY-ARTE.md): from the 0.10.1's converted pieces (packages/client/public/
# models/synty/sources.json), each piece's Sidekick parts, the preset each part is painted from, its fallback and its
# own paint, to Assets/Bastion/Client/Resources/Wardrobe.json. Names and colours only, nothing of Synty's.
#   python3 -I unity/tools/wardrobe-json.py
import json, os, re
root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sources = json.load(open(os.path.join(root, 'packages/client/public/models/synty/sources.json')))
preset = lambda path: re.search(r'T_(\w+?)ColorMap\.png$', path).group(1) if path else None
# A paint is a hex colour, or Blender's RGB (0 to 1) as the converter wrote it.
hexed = lambda c: c if isinstance(c, str) else '%02x%02x%02x' % tuple(round(v * 255) if v <= 1 else round(v) for v in c[:3])
pieces = []
for value in sources.values():
    job = json.loads(value)
    if job.get('kind') != 'wardrobe':
        continue
    pieces.append({
        'piece': job['piece'],
        'id': job['id'],
        'parts': [os.path.basename(p)[:-4] for p in job['parts']],
        'palettes': [preset(p) for p in job['palettes']],
        'base': preset(job.get('atlasImage')),
        'fallback': preset(job.get('fallback')),
        'paint': {cell: hexed(c) for cell, c in job.get('paint', {}).items()},
    })
pieces.sort(key=lambda p: (p['piece'], p['id']))
out = os.path.join(root, 'unity/Bastion/Assets/Bastion/Client/Resources/Wardrobe.json')
with open(out, 'w') as f:
    json.dump({'pieces': pieces}, f, indent=1, ensure_ascii=False)
    f.write('\n')
print(len(pieces), 'pieces ->', os.path.relpath(out, root))
