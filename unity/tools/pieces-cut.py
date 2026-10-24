# The buildings' kit pieces for Unity, cut from the source FBX as 0.10.1's convert.py `piece` cut them (the same boxes,
# in the FBX's own units, after its transforms are applied), each scaled to metres and written as an FBX that Unity
# imports into Assets/Synty/Pieces (off GitHub with the rest of Synty). Ids always named, never all at once (CLAUDE.md:
# as convert.ts). Pieces.json is unity/tools/pieces-json.mts's.
#   blender --background --factory-startup --python unity/tools/pieces-cut.py -- <Pieces.json> <repo> <out dir> <id>…
import json
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

SKIP = ('LOD1', 'LOD2', 'LOD3', 'Collision', 'collision', 'UCX_')


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, use_custom_props=False)
    return [o for o in bpy.data.objects if o not in before]


def unparent(o):
    world = o.matrix_world.copy()
    o.parent = None
    o.matrix_world = world


def bake(objects):
    """Applies every transform to the objects' data."""
    for o in objects:
        unparent(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def cut(obj, keep):
    """Cuts a mesh (its data in the FBX's units) to the box keep = [[x0, y0, z0], [x1, y1, z1]] (convert.py cut)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    for axis in range(3):
        for value, above in ((keep[0][axis], True), (keep[1][axis], False)):
            normal = Vector((0, 0, 0))
            normal[axis] = 1
            geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
            bmesh.ops.bisect_plane(bm, geom=geom, plane_co=normal * value, plane_no=normal, clear_inner=above, clear_outer=not above)
    bm.to_mesh(obj.data)
    bm.free()


def piece(pid, p, repo, out_dir):
    reset()
    objs = import_fbx(os.path.join(repo, p['src']))
    drop = p.get('drop', [])
    meshes = [o for o in objs if o.type == 'MESH' and not any(t in o.name for t in SKIP) and not any(d in o.name for d in drop)]
    for o in objs:
        if o not in meshes:
            for c in o.children:
                unparent(c)
    for o in objs:
        if o not in meshes:
            bpy.data.objects.remove(o)
    if not meshes:
        print(f'[pieces] {pid}: no meshes')
        return
    bake(meshes)
    unit = p.get('unit', 1)
    for m in meshes:
        if p.get('keep'):
            cut(m, p['keep'])
        # In metres (City's are in hundreds of them).
        m.data.transform(Matrix.Scale(unit, 4))
    for m in [m for m in meshes if not len(m.data.polygons)]:
        meshes.remove(m)
        bpy.data.objects.remove(m)
    if not meshes:
        print(f'[pieces] {pid}: nothing left in the box')
        return
    if p.get('parts'):
        # A door leaf's own pane (`leaf`_…Glass) joins the leaf, so it swings with it.
        leaf = next((m for m in meshes if m.name == p.get('leaf')), None)
        panes = [m for m in meshes if leaf and m is not leaf and m.name.startswith(leaf.name + '_') and m.name.endswith('Glass')]
        if panes:
            bpy.ops.object.select_all(action='DESELECT')
            for m in [leaf, *panes]:
                m.select_set(True)
            bpy.context.view_layer.objects.active = leaf
            bpy.ops.object.join()
        for m in [m for m in bpy.data.objects if m.type == 'MESH']:
            m.data.name = m.name
    else:
        bpy.ops.object.select_all(action='DESELECT')
        for m in meshes:
            m.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        if len(meshes) > 1:
            bpy.ops.object.join()
        obj = bpy.context.view_layer.objects.active
        obj.name = pid
        obj.data.name = pid
    out = os.path.join(out_dir, f'{pid}.fbx')
    bpy.ops.export_scene.fbx(filepath=out, use_selection=False, object_types={'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
                             path_mode='STRIP', use_custom_props=False)
    names = ', '.join(o.name for o in bpy.data.objects if o.type == 'MESH')
    print(f'[pieces] {pid}: {out} ({names})')


def main():
    args = sys.argv[sys.argv.index('--') + 1:]
    spec_path, repo, out_dir, ids = args[0], args[1], args[2], args[3:]
    spec = json.load(open(spec_path))['pieces']
    os.makedirs(out_dir, exist_ok=True)
    for pid in ids:
        p = spec.get(pid)
        if p is None or not p.get('src'):
            print(f'[pieces] {pid}: not in Pieces.json, or no source')
            continue
        piece(pid, p, repo, out_dir)


main()
