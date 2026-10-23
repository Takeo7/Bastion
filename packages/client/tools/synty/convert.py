# Converts Synty POLYGON FBX files to GLB for the game, headless in Blender:
#
#   Blender --background --factory-startup --python convert.py -- jobs.json
#
# convert.ts writes jobs.json from the manifest and runs this; see README.md.
# Every model comes out Y-up, in game units (SCALE: metres to tiles), without
# images: each material is named after the atlas it uses and the game puts the
# shared atlas texture on it. The kinds of job:
#
#  - character: a skinned body. Bones get one naming (BONES) whatever the pack
#    used, fingers, toes, IK and face bones go into their parent, and the
#    attachments (hair, hats, packs) hang from their bone as rigid meshes. A
#    second UV map, "part", tells the game which armour piece each corner is
#    (u) and whether its texel is cloth it may dye (v): see PART.
#  - sidekick: a Sidekick character, its parts one mesh on that skeleton (the dead, the living).
#  - wardrobe: a piece of the soldiers' wardrobe (clothes, a face, hair, a beard, headwear), with its shapes.
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
import re
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
# The fingers: the thumb, the index and the other three in one (Synty's hands), three or four joints each. The
# characters name them thumb_01_l, indexFinger_01_l, finger_01_l; the animation packs Thumb_01 on the left hand and
# Thumb_01_1 on the right; City Zombies' rig Thumb_01 and, its right hand's names taken, Thumb_01.001 (as Blender
# reads it).
for side, suffixes in (('L', ('_l', '')), ('R', ('_r', '_1', '.001'))):
    for chain, joints, name in (('Thumb', 3, 'thumb'), ('Index', 4, 'indexfinger'), ('Finger', 4, 'finger')):
        for j in range(1, joints + 1):
            for suffix in suffixes:
                BONES[f'{name}_0{j}{suffix}'] = f'{chain}_0{j}_{side}'

# Sidekick's (the prototype: SIDEKICK in the manifest) name all five: thumb_01_l, index_01_l, middle_01_l, ring and
# pinky, three joints each. Its middle finger is Synty's three in one; the ring and the little finger go with it
# (sidekick(): their weights moved onto the middle's before the skeleton is cleaned).
for side in ('L', 'R'):
    for j in range(1, 4):
        BONES[f'index_0{j}_{side.lower()}'] = f'Index_0{j}_{side}'
        BONES[f'middle_0{j}_{side.lower()}'] = f'Finger_0{j}_{side}'

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
# A Sidekick piece's own shade in its folds (23/10 #13: Synty's shader darkens them with a mask we don't have): its
# ambient occlusion, baked to a colour of its corners ("ao"), this many samples, out to this far (game units: the
# hollow under a collar, a strap, a fold).
AO_SAMPLES = 32
AO_DISTANCE = 0.12


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


def plain_slots(mesh, slots, uv, meshes=()):
    """The faces of the slots painted from a texture we don't have (a wire's alpha: no atlas of ours in the MaterialList)
    put on one spot `uv` of the atlas, one plain colour (they came out striped with whatever the atlas has there); all
    of a mesh named with one of `meshes`."""
    if not uv or not mesh.data.uv_layers:
        return
    whole = any(n in mesh.name for n in meshes)
    if not whole and not slots:
        return
    layer = mesh.data.uv_layers[0].data
    for p in mesh.data.polygons:
        i = p.material_index
        if whole or (i < len(slots) and not slots[i]):
            for l in p.loop_indices:
                layer[l].uv = uv


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


def sidekick_import(paths, shape):
    """
    Sidekick parts on one skeleton (the first's), the ring and little fingers folded onto the middle. `shape` sets
    each part's shape keys: given the preset's sliders it applies them (a whole character), given the wardrobe's
    shapes it keeps those as shape keys of their own and drops the rest (wardrobe()). Returns the skeleton, the
    meshes, and the parts' own bones beyond the common skeleton (a strap's, a beard's: tors_dyn_01,
    fchr_dyn_01_01_c), each to the one of the skeleton it hangs from.
    """
    meshes = []
    extra = {}
    imported = [import_fbx(path) for path in paths]
    # The skeleton: the fullest of the parts' (some come with only the bones they hang from).
    arm = max((o for objs in imported for o in objs if o.type == 'ARMATURE'), key=lambda o: len(o.data.bones))
    for k, objs in enumerate(imported):
        for o in objs:
            if o.type == 'ARMATURE' and o is not arm:
                for b in o.data.bones:
                    if b.name in arm.data.bones:
                        continue
                    p = b.parent
                    while p is not None and p.name not in arm.data.bones:
                        p = p.parent
                    if p is not None:
                        extra[b.name] = p.name
        for o in objs:
            if o.type == 'MESH':
                o['part'] = k
                meshes.append(o)
            elif o is not arm:
                for c in list(o.children):
                    unparent(c)
                bpy.data.objects.remove(o)
    for m in meshes:
        world = m.matrix_world.copy()
        m.parent = arm
        m.matrix_world = world
        mod = next((x for x in m.modifiers if x.type == 'ARMATURE'), None) or m.modifiers.new('Armature', 'ARMATURE')
        mod.object = arm
        shape(m)
        # The ring and the little finger with the middle one (Synty's hands curl the three as one).
        groups = {g.name: g for g in m.vertex_groups}
        for g in list(m.vertex_groups):
            hit = re.match(r'(ring|pinky)_(0\d_[lr])$', g.name)
            if not hit:
                continue
            target = groups.get('middle_' + hit.group(2)) or m.vertex_groups.new(name='middle_' + hit.group(2))
            groups[target.name] = target
            for v in m.data.vertices:
                for e in v.groups:
                    if e.group == g.index and e.weight > 0:
                        target.add([v.index], e.weight, 'ADD')
            m.vertex_groups.remove(g)
        keep_uv(m)
    return arm, meshes, extra


def apply_sliders(body):
    """The preset's build on a part: its shape keys at the sliders' values, applied."""
    def shape(m):
        keys = m.data.shape_keys
        if not keys:
            return
        for name, value in body.items():
            if name in keys.key_blocks:
                keys.key_blocks[name].value = value
        bpy.ops.object.select_all(action='DESELECT')
        m.select_set(True)
        bpy.context.view_layer.objects.active = m
        bpy.ops.object.shape_key_remove(all=True, apply_mix=True)
    return shape


def keep_shapes(shapes):
    """
    The wardrobe's shapes on a part, each a shape key of its own made from Sidekick's (its sliders at those values),
    the rest dropped: a part without one of them gets it unchanged, so all of them join with the same keys.
    """
    def shape(m):
        if not m.data.shape_keys:
            m.shape_key_add(name='Basis')
        blocks = m.data.shape_keys.key_blocks
        basis = m.data.shape_keys.reference_key
        made = []
        for name, values in shapes.items():
            for k in blocks:
                k.value = 0
            for key, value in values.items():
                if key in blocks:
                    blocks[key].value = value
            made.append(m.shape_key_add(name='_' + name, from_mix=True))
        for k in list(blocks):
            if k != basis and k not in made:
                m.shape_key_remove(k)
        for k in made:
            k.name = k.name[1:]
            k.value = 0
    return shape


def sidekick_mesh(arm, meshes, extra, atlas, scale):
    """The parts made one mesh on the cleaned skeleton, in game units, painted from `atlas`."""
    bpy.ops.object.select_all(action='DESELECT')
    for m in meshes:
        m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    mesh = bpy.context.view_layer.objects.active
    # What rides on Sidekick's root (some attachments) goes with the hips, the root being no bone of ours; what rides
    # on a part's own bones, with the bone they hang from.
    moves = {**extra, 'root': 'pelvis'}
    for g in list(mesh.vertex_groups):
        if g.name not in moves:
            continue
        to = mesh.vertex_groups.get(moves[g.name]) or mesh.vertex_groups.new(name=moves[g.name])
        for v in mesh.data.vertices:
            for e in v.groups:
                if e.group == g.index and e.weight > 0:
                    to.add([v.index], e.weight, 'ADD')
        mesh.vertex_groups.remove(g)
    clean_skeleton(arm, [mesh])
    bare = sum(1 for v in mesh.data.vertices if not any(e.weight > 0 for e in v.groups))
    if bare:
        print(f"[sidekick] {len(meshes)} parts: {bare} vertices with no bone", flush=True)
    bake([arm, mesh], scale)
    mesh.parent = arm
    mod = next((x for x in mesh.modifiers if x.type == 'ARMATURE'), None) or mesh.modifiers.new('Armature', 'ARMATURE')
    mod.object = arm
    mesh.data.materials.clear()
    mesh.data.materials.append(atlas_material(atlas))
    for p in mesh.data.polygons:
        p.material_index = 0
    mesh.name = 'body'
    mesh.data.name = 'body'
    arm.name = 'rig'
    return mesh


# A Sidekick palette's cells that are the body's, not the clothes' (y * 16 + x): the skin, the eyes, the teeth, the hair.
BODY_CELLS = [y * 16 + x for y in range(10, 15) for x in range(8)] + [9 * 16 + 3, 9 * 16 + 8]


def part_cells(meshes, palettes):
    """Each palette cell a part is painted from (y * 16 + x), and the palette of the preset that paints it (`palettes`, by part)."""
    own = {}
    loaded = {}
    for m in meshes:
        path = palettes[m['part']]
        loaded.setdefault(path, Atlas(path, 32))
        uv = m.data.uv_layers['map1'].data
        for i in range(len(uv)):
            own[min(15, int(uv[i].uv[0] * 16)) + min(15, int((1 - uv[i].uv[1]) * 16)) * 16] = loaded[path]
    return own


def unused(rgb):
    """Sidekick's colour for a cell a palette has no use for: pure red."""
    return rgb[0] > 0.99 and rgb[1] < 0.01 and rgb[2] < 0.01


def write_palette(path, own, fallbacks, paint={}, size=256):
    """
    A character's palette made of its parts' (`own`: each cell's), every cell a block of its own, grown to `size`
    without blending (as texture() grows a palette); a cell no part uses, or its palette left unused, from the first
    of `fallbacks` that has a colour for it; `paint`, cells ('x,y') given a colour of their own.
    """
    backs = [Atlas(f, 32) for f in fallbacks]
    k = size // 16
    px = [0.0] * (size * size * 4)
    for y in range(16):
        for x in range(16):
            u, v = (x + 0.5) / 16, 1 - (y + 0.5) / 16
            rgb = own[y * 16 + x].rgb(u, v) if y * 16 + x in own else (1.0, 0.0, 0.0)
            for b in backs:
                if not unused(rgb):
                    break
                rgb = b.rgb(u, v)
            rgb = paint.get(f'{x},{y}', rgb)
            # Blender's rows go from the bottom.
            for yy in range((15 - y) * k, (16 - y) * k):
                for xx in range(x * k, (x + 1) * k):
                    j = (yy * size + xx) * 4
                    px[j:j + 4] = [rgb[0], rgb[1], rgb[2], 1.0]
    img = bpy.data.images.new('palette', size, size, alpha=True)
    img.pixels = px
    img.file_format = 'WEBP'
    img.filepath_raw = path
    img.save(filepath=path, quality=100)


def occlusion(mesh):
    """The mesh's ambient occlusion by itself (the parts of one piece on one another), baked to its "ao" colour."""
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = AO_SAMPLES
    if scene.world is None:
        scene.world = bpy.data.worlds.new('World')
    scene.world.light_settings.distance = AO_DISTANCE
    scene.render.bake.target = 'VERTEX_COLORS'
    scene.render.bake.use_selected_to_active = False
    me = mesh.data
    attr = me.color_attributes.new(name='ao', type='FLOAT_COLOR', domain='POINT')
    me.color_attributes.active_color = attr
    bpy.ops.object.select_all(action='DESELECT')
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.bake(type='AO')


def sidekick(job, scale):
    """
    A Sidekick character, whole (the dead, the living): its preset's parts on one skeleton, its build (the preset's
    sliders) applied to the shape keys; then as any character: the skeleton cleaned to SYNTY_BONES, one mesh painted
    from its palette, the squad's colours where its clothes are (`dye`).
    """
    reset()
    arm, meshes, extra = sidekick_import(job['parts'], apply_sliders(job['body']))
    if job.get('paletteOut'):
        own = part_cells(meshes, job['palettes'])
        if job.get('bodyPalette'):
            dead = Atlas(job['bodyPalette'], 32)
            for c in BODY_CELLS:
                own[c] = dead
        write_palette(job['paletteOut'], own, job['fallbacks'], job.get('paint', {}))
    mesh = sidekick_mesh(arm, meshes, extra, job['atlas'], scale)
    if job.get('dye'):
        atlas = Atlas(job['atlasImage'], 256)
        write_parts(mesh, arm, atlas, skins=skin_colours(mesh, atlas))
    top = max((mesh.matrix_world @ v.co).z for v in mesh.data.vertices)
    arm['height'] = round(top, 4)
    occlusion(mesh)
    print(f"[sidekick] {job['id']}: {len(job['parts'])} parts, {sum(len(p.vertices) - 2 for p in mesh.data.polygons)} triangles, {len(arm.data.bones)} bones, {top:.3f} tall", flush=True)
    export(job['out'], skins=True, colors='ao')


def wardrobe(job, scale):
    """
    A piece of the soldiers' wardrobe (the game puts a soldier together from them): Sidekick parts made one mesh on
    our skeleton, its shapes kept (`shapes`: the body's sex and build, set at run time). The "part" map as a
    character's: the squad's colours where the clothes are, measured on its preset's palette (`atlasImage`), the
    skin being its cells there (`skins`); headgear takes the helmet colour, a face, hair and beards none. An outfit
    carries its palette, a hex colour a cell (16 × 16, row by row from the top), in the rig's extras; headwear too,
    and the cells it is painted from (`cells`), which keep their colours there on whatever outfit.
    """
    reset()
    arm, meshes, extra = sidekick_import(job['parts'], keep_shapes(job['shapes']))
    # Each part in its own preset's colours: the cells it is painted from, taken from that palette.
    own = part_cells(meshes, job['palettes'])
    mesh = sidekick_mesh(arm, meshes, extra, 'wardrobe', scale)
    atlas = Atlas(job['atlasImage'], 32)
    skins = [atlas.rgb(u, v) for u, v in job['skins']]
    fixed = {'outfit': None, 'headwear': HEADGEAR}.get(job['piece'], 0)
    write_parts(mesh, arm, atlas, fixed, skins)
    if job['piece'] in ('outfit', 'headwear'):
        fallback = Atlas(job['fallback'], 32)
        paint = job.get('paint', {})
        def cell(x, y):
            if f'{x},{y}' in paint:
                return paint[f'{x},{y}']
            u, v = (x + 0.5) / 16, 1 - (y + 0.5) / 16
            rgb = own.get(y * 16 + x, atlas).rgb(u, v)
            return fallback.rgb(u, v) if unused(rgb) else rgb
        arm['palette'] = ''.join('%02x%02x%02x' % tuple(round(c * 255) for c in cell(x, y)) for y in range(16) for x in range(16))
        # What goes on the head keeps its own colours: the cells it is painted from (y * 16 + x), its palette's.
        uv = mesh.data.uv_layers['map1'].data
        arm['cells'] = sorted({min(15, int(uv[i].uv[0] * 16)) + min(15, int((1 - uv[i].uv[1]) * 16)) * 16 for i in range(len(uv))})
    top = max((mesh.matrix_world @ v.co).z for v in mesh.data.vertices)
    arm['height'] = round(top, 4)
    occlusion(mesh)
    print(f"[wardrobe] {job['id']}: {len(job['parts'])} parts, {sum(len(p.vertices) - 2 for p in mesh.data.polygons)} triangles, {len(mesh.data.vertices)} vertices, {len(arm.data.bones)} bones", flush=True)
    export(job['out'], skins=True, morphs=True, colors='ao')


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
        plain_slots(m, slots.get(m.name) or slots.get(m.data.name), job.get('plain'), job.get('plainMeshes', ()))
    turn = Matrix.Rotation(-math.pi / 2, 4, 'Z') if job['kind'] == 'weapon' else Matrix()
    # Boxes in the FBX's metres: cut to `keep`, then `hole` taken out (a guard tower's hut off its deck).
    bake(meshes)
    for m in meshes:
        if job.get('keep'):
            cut(m, job['keep'])
        if job.get('hole'):
            hole(m, job['hole'])
        m.data.transform(Matrix.Scale(scale, 4))
    for m in [m for m in meshes if not len(m.data.polygons)]:
        meshes.remove(m)
        bpy.data.objects.remove(m)
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


def hole(obj, boxes):
    """Takes out of a mesh (its data in the FBX's metres) the faces whose middle is in one of `boxes` ([[x0, y0, z0], [x1, y1, z1]] each)."""
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    inside = lambda c, b: all(b[0][k] <= c[k] <= b[1][k] for k in range(3))
    gone = [f for f in bm.faces if any(inside(f.calc_center_median(), b) for b in boxes)]
    bmesh.ops.delete(bm, geom=gone, context='FACES')
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
        plain_slots(m, slots.get(m.name) or slots.get(m.data.name), job.get('plain'))
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
        # A door leaf's own pane (`leaf`_…Glass) joins the leaf, so it swings with it.
        leaf = next((m for m in meshes if m.name == job.get('leaf')), None)
        panes = [m for m in meshes if leaf and m is not leaf and m.name.startswith(leaf.name + '_') and m.name.endswith('Glass')]
        if panes:
            bpy.ops.object.select_all(action='DESELECT')
            for m in [leaf, *panes]:
                m.select_set(True)
            bpy.context.view_layer.objects.active = leaf
            bpy.ops.object.join()
            for m in panes:
                meshes.remove(m)
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
    # The fingers take the game's names (renaming a bone renames its tracks); face, props and toes are leaves the game
    # does not pose.
    for b in arm.data.bones:
        c = canonical(b.name)
        if c and c != b.name and c.split('_')[0] in ('Thumb', 'Index', 'Finger'):
            b.name = c
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


# The Mixamo rig's bones (X Bot's, mixamorig:…) by the game's names (BONES' values): what a Mixamo clip moves.
MIXAMO = {
    'Hips': 'Hips', 'Spine': 'Spine_01', 'Spine1': 'Spine_02', 'Spine2': 'Spine_03', 'Neck': 'Neck', 'Head': 'Head',
}
for side, s in (('Left', 'L'), ('Right', 'R')):
    MIXAMO.update({
        f'{side}Shoulder': f'Clavicle_{s}', f'{side}Arm': f'Shoulder_{s}', f'{side}ForeArm': f'Elbow_{s}', f'{side}Hand': f'Hand_{s}',
        f'{side}UpLeg': f'UpperLeg_{s}', f'{side}Leg': f'LowerLeg_{s}', f'{side}Foot': f'Ankle_{s}', f'{side}ToeBase': f'Ball_{s}',
    })
    for j in (1, 2, 3):
        MIXAMO.update({f'{side}HandThumb{j}': f'Thumb_0{j}_{s}', f'{side}HandIndex{j}': f'Index_0{j}_{s}', f'{side}HandMiddle{j}': f'Finger_0{j}_{s}'})


def mixamo(job):
    """
    A Mixamo clip (Arturo, 21/10: the weapons' animations Synty doesn't have) on Synty's skeleton, as the game's own
    clips are (anim): each bone turned from Mixamo's T-pose (`tpose`) as much as the clip turns it, onto Synty's
    T-pose (`target`, Base Locomotion's), in the world, both rigs facing the same way; the hips moved as far for
    their height. Frame by frame, at the clip's own rate, then exported as anim() exports. `mirror`: the clip's left
    for the right and the other way round, every turn reflected across the body's middle (a throw with the free hand).
    `inPlace`: the hips' way along the ground taken out, evenly over the clip (one Mixamo couldn't make In Place: the
    sprint, 3.3 m a cycle), so it ends where it began.
    """
    reset()
    scene = bpy.context.scene
    target_objs = import_fbx(job['target'])
    arm = next(o for o in target_objs if o.type == 'ARMATURE')
    for o in target_objs:
        if o is not arm:
            bpy.data.objects.remove(o)
    for b in arm.data.bones:
        c = canonical(b.name)
        if c and c != b.name:
            b.name = c
    pose_objs = import_fbx(job['tpose'])
    rest = next(o for o in pose_objs if o.type == 'ARMATURE')
    clip_objs = import_fbx(job['fbx'])
    src = next(o for o in clip_objs if o.type == 'ARMATURE')
    for o in pose_objs + clip_objs:
        if o.type != 'ARMATURE':
            bpy.data.objects.remove(o)
    action = src.animation_data.action
    first, last = (int(round(v)) for v in action.frame_range)
    scene.render.fps = job.get('fps', 30)

    def bone(o, name):
        return o.pose.bones.get(f'mixamorig:{name}') or o.pose.bones.get(name)

    def world(o, pb):
        return o.matrix_world @ pb.matrix

    # The T-poses: Mixamo's (its clip's first frame) and Synty's (its action's first frame).
    scene.frame_set(int(rest.animation_data.action.frame_range[0]) if rest.animation_data and rest.animation_data.action else 0)
    scene.frame_set(int(arm.animation_data.action.frame_range[0]) if arm.animation_data and arm.animation_data.action else 0)
    bpy.context.view_layer.update()
    mirror = job.get('mirror', False)
    flip = lambda m: m.replace('Left', '\0').replace('Right', 'Left').replace('\0', 'Right') if mirror else m
    # (Mixamo's bone each of Synty's takes its turn from: its own, or mirrored, the other side's.)
    pairs = [(flip(m), s) for m, s in MIXAMO.items() if bone(rest, flip(m)) and bone(src, flip(m)) and arm.pose.bones.get(s)]
    m0 = {m: world(rest, bone(rest, m)).to_quaternion() for m, _ in pairs}
    s0 = {s: world(arm, arm.pose.bones[s]).to_quaternion() for _, s in pairs}
    basis0 = {pb.name: pb.matrix_basis.copy() for pb in arm.pose.bones}
    # Which way each faces (heel to toe, flat): the turn from Mixamo's to Synty's.
    def facing(o, foot, toe):
        a = world(o, foot).translation
        b = world(o, toe).translation
        v = Vector((b.x - a.x, b.y - a.y, 0))
        return v.normalized()
    fm = facing(rest, bone(rest, 'LeftFoot'), bone(rest, 'LeftToeBase'))
    fs = facing(arm, arm.pose.bones['Ankle_L'], arm.pose.bones['Ball_L'])
    turn = fm.rotation_difference(fs)
    # Across the body (Synty's left to right, flat): the plane a mirrored clip is reflected in.
    side = fs.cross(Vector((0, 0, 1))).normalized()
    def reflect_q(q):
        v = Vector((q.x, q.y, q.z))
        v = -(v - 2 * v.dot(side) * side)
        return q.__class__((q.w, v.x, v.y, v.z))
    def reflect_v(v):
        return v - 2 * v.dot(side) * side
    hips_m0 = world(rest, bone(rest, 'Hips')).translation.copy()
    hips_s0 = world(arm, arm.pose.bones['Hips']).translation.copy()
    floor_m = min(world(rest, bone(rest, n)).translation.z for n in ('LeftToeBase', 'RightToeBase', 'LeftFoot', 'RightFoot'))
    floor_s = min(world(arm, arm.pose.bones[n]).translation.z for n in ('Ball_L', 'Ball_R', 'Ankle_L', 'Ankle_R'))
    k = (hips_s0.z - floor_s) / max(1e-6, hips_m0.z - floor_m)
    inv_arm = arm.matrix_world.inverted()
    rot_arm = arm.matrix_world.to_quaternion().inverted()
    # The retargeted clip on Synty's rig, its own action.
    if not arm.animation_data:
        arm.animation_data_create()
    out = bpy.data.actions.new(job['id'])
    arm.animation_data.action = out
    order = []
    def walk(b):
        order.append(b.name)
        for c in b.children:
            walk(c)
    for b in arm.data.bones:
        if not b.parent:
            walk(b)
    goal = dict(pairs)
    goal = {s: m for m, s in pairs}
    drift = Vector((0, 0, 0))
    if job.get('inPlace') and last > first:
        scene.frame_set(first)
        bpy.context.view_layer.update()
        h0 = world(src, bone(src, 'Hips')).translation.copy()
        scene.frame_set(last)
        bpy.context.view_layer.update()
        h1 = world(src, bone(src, 'Hips')).translation.copy()
        drift = Vector(((h1.x - h0.x) / (last - first), (h1.y - h0.y) / (last - first), 0))
    for f in range(first, last + 1):
        scene.frame_set(f)
        bpy.context.view_layer.update()
        hips = world(src, bone(src, 'Hips')).translation - drift * (f - first)
        moved = turn @ ((hips - hips_m0) * k)
        hips_s = hips_s0 + (reflect_v(moved) if mirror else moved)
        posed = {}
        for name in order:
            pb = arm.pose.bones[name]
            b = pb.bone
            if b.parent:
                parent = posed[b.parent.name]
                chain = parent @ b.parent.matrix_local.inverted() @ b.matrix_local
            else:
                chain = b.matrix_local.copy()
            m = goal.get(name)
            if m:
                delta = turn @ world(src, bone(src, m)).to_quaternion() @ m0[m].inverted() @ turn.inverted()
                q = (reflect_q(delta) if mirror else delta) @ s0[name]
                head = (inv_arm @ hips_s) if name == 'Hips' else (chain @ basis0[name]).translation
                a = Matrix.Translation(head) @ (rot_arm @ q).to_matrix().to_4x4()
                basis = chain.inverted() @ a
            else:
                basis = basis0[name].copy()
                a = chain @ basis
            posed[name] = a
            pb.matrix_basis = basis
            pb.keyframe_insert('rotation_quaternion', frame=f - first)
            pb.keyframe_insert('location', frame=f - first)
    for o in (rest, src):
        bpy.data.objects.remove(o)
    keep = set(BONES.values()) | {'Root'}
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    for b in list(arm.data.edit_bones):
        if b.name not in keep:
            arm.data.edit_bones.remove(b)
    bpy.ops.object.mode_set(mode='OBJECT')
    arm.name = 'rig'
    print(f"[mixamo] {job['id']}: {last - first + 1} frames, {len(pairs)} bones, hips ×{k:.3f}", flush=True)
    export(job['out'], skins=False, animations=True)


# ------------------------------------------------------------------ textures

def texture(job):
    reset()
    img = bpy.data.images.load(job['src'])
    if job.get('rows'):
        # A strip of its rows (from and to, a share of its height from the top), at width × height.
        import numpy as np
        top, bottom = job['rows']
        w = job['width']
        full = round(job['height'] / (bottom - top))
        img.scale(w, full)
        px = np.empty(w * full * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        # Blender's rows go from the bottom.
        y0 = full - round(bottom * full)
        band = px.reshape(full, w, 4)[y0:y0 + job['height']]
        out = bpy.data.images.new(img.name + '_strip', w, band.shape[0], alpha=True)
        out.pixels.foreach_set(band.ravel())
        out.file_format = 'WEBP'
        out.filepath_raw = job['out']
        out.save(filepath=job['out'], quality=90)
        return
    size = job['size']
    if job.get('palette') and img.size[0] < size:
        # A palette (Sidekick's 32 × 32 colours): grown with every colour a block of its own, none blended.
        w, h = img.size
        src = list(img.pixels)
        k = size // w
        out = bpy.data.images.new(img.name + '_big', w * k, h * k, alpha=True)
        px = [0.0] * (w * k * h * k * 4)
        for y in range(h * k):
            for x in range(w * k):
                i = ((y // k) * w + x // k) * 4
                j = (y * w * k + x) * 4
                px[j:j + 4] = src[i:i + 4]
        out.pixels = px
        out.file_format = 'WEBP'
        out.filepath_raw = job['out']
        out.save(filepath=job['out'], quality=100)
        return
    if img.size[0] > size:
        img.scale(size, size)
    img.file_format = 'WEBP'
    img.save(filepath=job['out'], quality=job.get('quality', 90))


# -------------------------------------------------------------------- export

def export(path, skins, animations=False, morphs=False, colors=None):
    # (`colors`: the colour attribute of that name as the mesh's COLOR_0; else none, as the materials use none.)
    extra = {'export_vertex_color': 'NAME', 'export_vertex_color_name': colors} if colors else {}
    bpy.ops.export_scene.gltf(
        **extra,
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
        export_morph=morphs,
        export_morph_normal=morphs,
        export_morph_tangent=False,
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
        elif job['kind'] == 'sidekick':
            sidekick(job, scale)
        elif job['kind'] == 'wardrobe':
            wardrobe(job, scale)
        elif job['kind'] in ('prop', 'weapon'):
            static(job, scale)
        elif job['kind'] == 'texture':
            texture(job)
        elif job['kind'] == 'anim':
            anim(job)
        elif job['kind'] == 'mixamo':
            mixamo(job)
        elif job['kind'] == 'piece':
            piece(job, scale)


main()
