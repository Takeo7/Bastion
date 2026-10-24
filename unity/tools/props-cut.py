# A prop's model cut as 0.10.1's convert.py cut it (`keep`: what is outside the box goes; `hole`: the faces whose middle
# is in a box go), from the source FBX, its transforms applied, in the FBX's metres; written as an FBX for Unity, with
# the axes pieces-cut.py writes. SourceProps runs it for the props of its list that carry a cut (the guard tower's hut and
# stairs off its deck), then imports the result into Assets/Synty/Extra (off GitHub with the rest of Synty).
#   blender --background --factory-startup --python unity/tools/props-cut.py -- <source.fbx> <out.fbx> <cut.json: {"keep": …, "hole": […]}>
import json
import sys

import bmesh
import bpy
from mathutils import Vector

SKIP = ('LOD1', 'LOD2', 'LOD3', 'Collision', 'collision', 'UCX_')


def unparent(o):
    world = o.matrix_world.copy()
    o.parent = None
    o.matrix_world = world


def cut(obj, keep):
    """Cuts a mesh (its data in the FBX's metres) to the box keep = [[x0, y0, z0], [x1, y1, z1]] (convert.py cut)."""
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


def hole(obj, boxes):
    """Takes out the faces whose middle is in one of `boxes` ([[x0, y0, z0], [x1, y1, z1]] each; convert.py hole)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    inside = lambda c, b: all(b[0][k] <= c[k] <= b[1][k] for k in range(3))
    gone = [f for f in bm.faces if any(inside(f.calc_center_median(), b) for b in boxes)]
    bmesh.ops.delete(bm, geom=gone, context='FACES')
    bm.to_mesh(obj.data)
    bm.free()


def main():
    args = sys.argv[sys.argv.index('--') + 1:]
    source, out, spec = args[0], args[1], json.load(open(args[2]))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=source, use_custom_props=False)
    objs = list(bpy.data.objects)
    meshes = [o for o in objs if o.type == 'MESH' and not any(t in o.name for t in SKIP)]
    for o in objs:
        if o not in meshes:
            for c in o.children:
                unparent(c)
    for o in objs:
        if o not in meshes:
            bpy.data.objects.remove(o)
    for o in meshes:
        unparent(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for m in meshes:
        if spec.get('keep'):
            cut(m, spec['keep'])
        if spec.get('hole'):
            hole(m, spec['hole'])
    for m in [m for m in meshes if not len(m.data.polygons)]:
        meshes.remove(m)
        bpy.data.objects.remove(m)
    bpy.ops.export_scene.fbx(filepath=out, use_selection=False, object_types={'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
                             path_mode='STRIP', use_custom_props=False)
    print(f'[props-cut] {out}: {", ".join(m.name for m in meshes)}')


main()
