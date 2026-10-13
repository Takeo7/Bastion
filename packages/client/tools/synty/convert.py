# Converts Synty POLYGON FBX files to GLB for the game, headless in Blender:
#
#   Blender --background --factory-startup --python convert.py -- jobs.json
#
# convert.ts writes jobs.json from the manifest and runs this; see README.md.
# Every model comes out Y-up, in game units (SCALE: metres to tiles), without
# images: each material is named after the atlas it uses and the game puts the
# shared atlas texture on it. Three kinds of job:
#
#  - character: a skinned body. Bones get one naming (BONES) whatever the pack
#    used, fingers, toes, IK and face bones go into their parent, and the
#    attachments (hair, hats, packs) hang from their bone as rigid meshes. A
#    second UV map, "part", tells the game which armour piece each corner is
#    (u) and whether its texel is cloth it may dye (v): see PART.
#  - headgear: a hat or helmet the game hangs from the head bone at run time.
#  - prop / weapon: static meshes joined per material. Weapons are turned so
#    the barrel points +Z with the grip at the origin; their muzzle goes in
#    the node's extras.
#  - texture: an atlas scaled down and saved as WebP.
#  - piece: a piece of a modular building kit (a wall, a door, a window): a
#    prop cut to a box first (a tile-wide strip of a module), with its meshes
#    kept apart when one of them swings (a door's leaf).
#  - anim: one animation clip with its skeleton (the bones the game retargets
#    and their ancestors), as Synty made it: no baked transforms, every frame
#    sampled. The game measures it against the pack's T-pose clip.

import bpy
import colorsys
import json
import math
import sys
from mathutils import Matrix, Vector

# --------------------------------------------------------------------- bones

# Synty rigs come in two namings (Apocalypse: Pelvis, spine_01, Thigh_L, calf_l;
# City Zombies: Hips, Spine_01, UpperLeg_L, LowerLeg_L). Both become the second,
# the one Synty's animation packs use, so clips will apply as they come.
BONES = {
    'pelvis': 'Hips', 'hips': 'Hips',
    'spine_01': 'Spine_01', 'spine_02': 'Spine_02', 'spine_03': 'Spine_03',
    'neck_01': 'Neck', 'neck': 'Neck', 'head': 'Head',
    'clavicle_l': 'Clavicle_L', 'clavicle_r': 'Clavicle_R',
    'upperarm_l': 'Shoulder_L', 'upperarm_r': 'Shoulder_R', 'shoulder_l': 'Shoulder_L', 'shoulder_r': 'Shoulder_R',
    'lowerarm_l': 'Elbow_L', 'lowerarm_r': 'Elbow_R', 'elbow_l': 'Elbow_L', 'elbow_r': 'Elbow_R',
    'hand_l': 'Hand_L', 'hand_r': 'Hand_R',
    'thigh_l': 'UpperLeg_L', 'thigh_r': 'UpperLeg_R', 'upperleg_l': 'UpperLeg_L', 'upperleg_r': 'UpperLeg_R',
    'calf_l': 'LowerLeg_L', 'calf_r': 'LowerLeg_R', 'lowerleg_l': 'LowerLeg_L', 'lowerleg_r': 'LowerLeg_R',
    'foot_l': 'Ankle_L', 'foot_r': 'Ankle_R', 'ankle_l': 'Ankle_L', 'ankle_r': 'Ankle_R',
    'ball_l': 'Ball_L', 'ball_r': 'Ball_R',
}

# Armour piece of each bone, for the "part" UV (u = PART / 8 + 1/16).
# 0 never dyed (head, hands, feet), 1 headgear, 2 torso, 3 arms, 4 legs, 5 the owner's band.
PART = {
    'Hips': 4, 'UpperLeg_L': 4, 'UpperLeg_R': 4, 'LowerLeg_L': 4, 'LowerLeg_R': 4,
    'Spine_01': 2, 'Spine_02': 2, 'Spine_03': 2, 'Clavicle_L': 2, 'Clavicle_R': 2,
    'Shoulder_L': 3, 'Shoulder_R': 3, 'Elbow_L': 3, 'Elbow_R': 3,
}
HEADGEAR = 1
BAND = 5
# The owner's band: this stretch of the upper arm, from the shoulder (0) to the elbow (1).
BAND_SPAN = (0.18, 0.5)


def canonical(name):
    return BONES.get(name.lower())


# -------------------------------------------------------------------- import

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


def bake(objects, scale=1.0):
    """Applies every transform (and an extra uniform scale) to the objects' data."""
    for o in objects:
        unparent(o)
    for o in objects:
        o.matrix_world = Matrix.Scale(scale, 4) @ o.matrix_world
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def keep_uv(mesh):
    """Only the first UV map (the atlas), named map1."""
    layers = mesh.data.uv_layers
    while len(layers) > 1:
        layers.remove(layers[len(layers) - 1])
    if len(layers):
        layers[0].name = 'map1'


def atlas_material(key):
    m = bpy.data.materials.get(key)
    if not m:
        m = bpy.data.materials.new(key)
    return m


def assign_atlases(mesh, slots, default):
    """Each material slot gets the material named after its atlas (from the MaterialList, else the default)."""
    for i, slot in enumerate(mesh.material_slots):
        key = slots[i] if slots and i < len(slots) and slots[i] else default
        slot.material = atlas_material(key)
    if not mesh.material_slots:
        mesh.data.materials.append(atlas_material(default))


# --------------------------------------------------------------- characters

def clean_skeleton(arm, meshes):
    """Renames the bones, folds the ones the game does not pose into their parent and deletes the rest."""
    target = {}
    for b in arm.data.bones:
        c = canonical(b.name)
        p = b
        while c is None and p.parent is not None:
            p = p.parent
            c = canonical(p.name)
        target[b.name] = c
    for mesh in meshes:
        sums = [dict() for _ in mesh.data.vertices]
        names = {g.index: g.name for g in mesh.vertex_groups}
        for v in mesh.data.vertices:
            for g in v.groups:
                t = target.get(names.get(g.group))
                if t and g.weight > 0:
                    sums[v.index][t] = sums[v.index].get(t, 0) + g.weight
        mesh.vertex_groups.clear()
        groups = {}
        for i, w in enumerate(sums):
            for t, weight in w.items():
                if t not in groups:
                    groups[t] = mesh.vertex_groups.new(name=t)
                groups[t].add([i], weight, 'REPLACE')
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.data.edit_bones
    kept = {b.name for b in eb if canonical(b.name)}
    for b in list(eb):
        if b.name in kept:
            p = b.parent
            while p is not None and p.name not in kept:
                p = p.parent
            b.parent = p
    for b in list(eb):
        if b.name not in kept:
            eb.remove(b)
    for b in eb:
        b.name = canonical(b.name)
    bpy.ops.object.mode_set(mode='OBJECT')


def bone_head(arm, name):
    return arm.matrix_world @ arm.data.bones[name].head_local


def attach(arm, path, bone, scale, name, turn=-math.pi / 2):
    """
    Hangs an attachment (hair, hat, pack) from a bone as a rigid mesh. Synty
    draws them around the bone's joint, in their own axes: forward is +X and
    up +Y in the file, so they turn a quarter about the vertical (`turn`;
    Office's face as the body does and turn none).
    """
    objs = import_fbx(path)
    meshes = [o for o in objs if o.type == 'MESH']
    for o in objs:
        if o.type != 'MESH':
            for c in o.children:
                unparent(c)
            bpy.data.objects.remove(o)
    head = bone_head(arm, bone)
    out = []
    for i, o in enumerate(meshes):
        unparent(o)
        o.matrix_world = Matrix.Translation(head) @ Matrix.Rotation(turn, 4, 'Z') @ Matrix.Scale(scale, 4) @ o.matrix_world
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        keep_uv(o)
        o.name = name if i == 0 else f'{name}_{i}'
        o.data.name = o.name
        world = o.matrix_world.copy()
        o.parent = arm
        o.parent_type = 'BONE'
        o.parent_bone = bone
        o.matrix_world = world
        out.append(o)
    return out


class Atlas:
    """Atlas pixels for sampling texel colours (bottom row first, like Blender's UVs)."""

    def __init__(self, path, size=512):
        img = bpy.data.images.load(path)
        img.scale(size, size)
        self.size = size
        self.px = list(img.pixels)
        bpy.data.images.remove(img)

    def rgb(self, u, v):
        s = self.size
        x = min(s - 1, max(0, int((u % 1.0) * s)))
        y = min(s - 1, max(0, int((v % 1.0) * s)))
        i = (y * s + x) * 4
        return self.px[i], self.px[i + 1], self.px[i + 2]


def dyeable(rgb, skins):
    """
    Cloth the game may dye: not near black (boots, belts, hair), not near
    white (eyes, teeth), and not this character's skin (bare arms and legs).
    """
    h, s, v = colorsys.rgb_to_hsv(*rgb)
    if v < 0.16 or (v > 0.93 and s < 0.08):
        return False
    return all(sum((a - b) ** 2 for a, b in zip(rgb, k)) > 0.0045 for k in skins)


def skin_colours(mesh, atlas):
    """
    A character's skin, as the texels of its face: the colours that cover a
    good share of the head and neck. Measured per character, so khaki or tan
    cloth is not mistaken for skin, and bare arms and legs are still told apart.
    """
    me = mesh.data
    uv = me.uv_layers['map1'].data
    names = {g.index: g.name for g in mesh.vertex_groups}
    head = set()
    for v in me.vertices:
        best = max(v.groups, key=lambda g: g.weight, default=None)
        if best and names.get(best.group) in ('Head', 'Neck'):
            head.add(v.index)
    count = {}
    total = 0
    for loop in me.loops:
        if loop.vertex_index not in head:
            continue
        rgb = atlas.rgb(*uv[loop.index].uv)
        key = tuple(round(c * 24) / 24 for c in rgb)
        count[key] = count.get(key, 0) + 1
        total += 1
    return [k for k, n in sorted(count.items(), key=lambda kv: -kv[1])[:3] if n >= total * 0.12]


def write_parts(mesh, arm, atlas, fixed=None, skins=()):
    """The "part" UV map: armour piece by the corner's main bone (or `fixed`), dyeable by its texel (not `skins`)."""
    me = mesh.data
    uv = me.uv_layers['map1'].data
    layer = me.uv_layers.new(name='part')
    names = {g.index: g.name for g in mesh.vertex_groups}
    main = []
    for v in me.vertices:
        best = max(v.groups, key=lambda g: g.weight, default=None)
        main.append(names.get(best.group) if best else None)
    bones = arm.data.bones if arm else None
    for loop in me.loops:
        vi = loop.vertex_index
        bone = main[vi]
        part = fixed if fixed is not None else PART.get(bone, 0)
        mask = 1.0 if part and dyeable(atlas.rgb(*uv[loop.index].uv), skins) else 0.0
        if fixed is None and bone in ('Shoulder_L', 'Shoulder_R'):
            # Measured to the elbow joint: imported bone tails point anywhere.
            a, z = bones[bone].head_local, bones[bone.replace('Shoulder', 'Elbow')].head_local
            t = (me.vertices[vi].co - a).dot(z - a) / max(1e-6, (z - a).length_squared)
            if BAND_SPAN[0] < t < BAND_SPAN[1]:
                part, mask = BAND, 1.0
        layer.data[loop.index].uv = (part / 8 + 1 / 16, mask)


def character(job, scale):
    reset()
    objs = import_fbx(job['fbx'])
    arm = next(o for o in objs if o.type == 'ARMATURE')
    keep = job.get('mesh')
    meshes = []
    for o in objs:
        if o.type == 'MESH' and (not keep or o.name == keep):
            meshes.append(o)
        elif o is not arm:
            bpy.data.objects.remove(o)
    clean_skeleton(arm, meshes)
    bake([arm] + meshes, scale)
    atlas = Atlas(job['atlasImage']) if job.get('atlasImage') else None
    skins = []
    for m in meshes:
        keep_uv(m)
        m.parent = arm
        mod = next((x for x in m.modifiers if x.type == 'ARMATURE'), None) or m.modifiers.new('Armature', 'ARMATURE')
        mod.object = arm
        assign_atlases(m, None, job['atlas'])
        m.name = 'body'
        m.data.name = 'body'
        if atlas:
            skins = skin_colours(m, atlas)
            write_parts(m, arm, atlas, skins=skins)
    hung = []
    for a in job.get('attach', []):
        for o in attach(arm, a['fbx'], a['bone'], scale, a['name'], math.radians(a.get('turn', -90))):
            assign_atlases(o, None, a.get('atlas', job['atlas']))
            if atlas:
                # Headgear takes the helmet colour; hair, beards and packs keep theirs.
                o.vertex_groups.clear()
                write_parts(o, arm, atlas, HEADGEAR if a.get('dye') else 0, skins)
            hung.append((o, a['bone']))
    if job.get('merge') and hung and meshes:
        merge_attachments(arm, meshes[0], hung)
    arm.name = 'rig'
    top = max((o.matrix_world @ v.co).z for o in [o for o in bpy.data.objects if o.type == 'MESH' and o.name == 'body'] for v in o.data.vertices)
    arm['height'] = round(top, 4)
    export(job['out'], skins=True)


def merge_attachments(arm, body, hung):
    """
    Joins the attachments into the skinned body, each moving with its bone,
    so the character is one mesh (one draw call per pass). Only for those
    whose hair never comes off (enemies, the dead): a survivor's hair hides
    under the barracks' hats.
    """
    for o, bone in hung:
        world = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = world
        o.vertex_groups.clear()
        o.vertex_groups.new(name=bone).add([v.index for v in o.data.vertices], 1.0, 'REPLACE')
    bpy.ops.object.select_all(action='DESELECT')
    for o, _ in hung:
        o.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()


def headgear(job, scale):
    """A hat or helmet to hang from the head bone at run time: the joint at the origin, forward +Z (glTF)."""
    reset()
    objs = import_fbx(job['fbx'])
    meshes = [o for o in objs if o.type == 'MESH']
    for o in objs:
        if o.type != 'MESH':
            bpy.data.objects.remove(o)
    for o in meshes:
        unparent(o)
        o.matrix_world = Matrix.Rotation(-math.pi / 2, 4, 'Z') @ Matrix.Scale(scale, 4) @ o.matrix_world
    bake(meshes)
    bpy.ops.object.select_all(action='DESELECT')
    for m in meshes:
        assign_atlases(m, None, job['atlas'])
        keep_uv(m)
        m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = job['name']
    obj.data.name = job['name']
    if job.get('atlasImage'):
        write_parts(obj, None, Atlas(job['atlasImage']), HEADGEAR if job.get('dye') else 0)
    export(job['out'], skins=False)


# -------------------------------------------------------------- props, guns

def static(job, scale):
    reset()
    objs = import_fbx(job['fbx'])
    meshes = []
    for o in objs:
        if o.type == 'MESH' and not any(t in o.name for t in ('LOD1', 'LOD2', 'LOD3', 'Collision', 'collision')):
            meshes.append(o)
    for o in objs:
        if o not in meshes:
            for c in o.children:
                unparent(c)
    for o in objs:
        if o not in meshes:
            bpy.data.objects.remove(o)
    slots = job.get('slots', {})
    for m in meshes:
        assign_atlases(m, slots.get(m.name) or slots.get(m.data.name), job['atlas'])
        keep_uv(m)
    turn = Matrix.Rotation(-math.pi / 2, 4, 'Z') if job['kind'] == 'weapon' else Matrix()
    bake(meshes, scale)
    for m in meshes:
        m.data.transform(turn)
    bpy.ops.object.select_all(action='DESELECT')
    for m in meshes:
        m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = job['name']
    obj.data.name = job['name']
    if job['kind'] == 'weapon':
        # Barrel along Blender −Y (glTF +Z): the muzzle is the far end, at the height of the barrel there.
        vs = [v.co for v in obj.data.vertices]
        tip = min(v.y for v in vs)
        near = [v for v in vs if v.y < tip + 0.015]
        mz = sum(v.z for v in near) / len(near)
        mx = sum(v.x for v in near) / len(near)
        obj['muzzle'] = [round(mx, 4), round(mz, 4), round(-tip, 4)]
    export(job['out'], skins=False)


# -------------------------------------------------------------------- pieces

def cut(obj, keep):
    """Cuts a mesh (its data in the FBX's metres) to the box keep = [[x0, y0, z0], [x1, y1, z1]]."""
    import bmesh
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


def piece(job, scale):
    reset()
    objs = import_fbx(job['fbx'])
    skip = ('LOD1', 'LOD2', 'LOD3', 'Collision', 'collision', 'UCX_')
    meshes = [o for o in objs if o.type == 'MESH' and not any(t in o.name for t in skip) and not any(d in o.name for d in job.get('drop', []))]
    for o in objs:
        if o not in meshes:
            for c in o.children:
                unparent(c)
    for o in objs:
        if o not in meshes:
            bpy.data.objects.remove(o)
    slots = job.get('slots', {})
    for m in meshes:
        assign_atlases(m, slots.get(m.name) or slots.get(m.data.name), job['atlas'])
        keep_uv(m)
    # In the FBX's metres first, so the box is as the manifest gives it; then to game units.
    bake(meshes)
    for m in meshes:
        if job.get('keep'):
            cut(m, job['keep'])
        m.data.transform(Matrix.Scale(scale, 4))
    for m in [m for m in meshes if not len(m.data.polygons)]:
        meshes.remove(m)
        bpy.data.objects.remove(m)
    if job.get('parts'):
        for m in meshes:
            m.data.name = m.name
    else:
        bpy.ops.object.select_all(action='DESELECT')
        for m in meshes:
            m.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        if len(meshes) > 1:
            bpy.ops.object.join()
        obj = bpy.context.view_layer.objects.active
        obj.name = job['name']
        obj.data.name = job['name']
    export(job['out'], skins=False)


# ---------------------------------------------------------------- animations

def anim(job):
    reset()
    objs = import_fbx(job['fbx'])
    arm = next(o for o in objs if o.type == 'ARMATURE')
    for o in objs:
        if o is not arm:
            bpy.data.objects.remove(o)
    # Fingers, face, props and toes are leaves the game does not pose.
    keep = set(BONES.values()) | {'Root'}
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    for b in list(arm.data.edit_bones):
        if b.name not in keep:
            arm.data.edit_bones.remove(b)
    bpy.ops.object.mode_set(mode='OBJECT')
    arm.animation_data.action.name = job['id']
    arm.name = 'rig'
    export(job['out'], skins=False, animations=True)


# ------------------------------------------------------------------ textures

def texture(job):
    reset()
    img = bpy.data.images.load(job['src'])
    size = job['size']
    if img.size[0] > size:
        img.scale(size, size)
    img.file_format = 'WEBP'
    img.save(filepath=job['out'], quality=job.get('quality', 90))


# -------------------------------------------------------------------- export

def export(path, skins, animations=False):
    bpy.ops.export_scene.gltf(
        filepath=path,
        export_format='GLB',
        export_image_format='NONE',
        export_materials='EXPORT',
        export_texcoords=True,
        export_normals=True,
        export_tangents=False,
        export_skins=skins,
        export_animations=animations,
        export_animation_mode='ACTIVE_ACTIONS',
        export_force_sampling=True,
        export_anim_slide_to_zero=True,
        # Every bone keeps its track: a missing one would take the skeleton's rest from another clip.
        export_optimize_animation_size=False,
        export_morph=False,
        export_yup=True,
        export_apply=False,
        export_extras=True,
        export_cameras=False,
        export_lights=False,
    )


def main():
    args = sys.argv[sys.argv.index('--') + 1:]
    with open(args[0]) as f:
        spec = json.load(f)
    scale = spec['scale']
    for job in spec['jobs']:
        print(f"[synty] {job['kind']}: {job['out']}", flush=True)
        if job['kind'] == 'character':
            character(job, scale)
        elif job['kind'] == 'headgear':
            headgear(job, scale)
        elif job['kind'] in ('prop', 'weapon'):
            static(job, scale)
        elif job['kind'] == 'texture':
            texture(job)
        elif job['kind'] == 'anim':
            anim(job)
        elif job['kind'] == 'piece':
            piece(job, scale)


main()
