import * as THREE from 'three';
import * as SkeletonUtils from 'three/addons/utils/SkeletonUtils.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import type { Appearance, BuildId, HeadId } from '@bastion/engine';
import { loadModel, modelNow } from './synty';
import {
  BEARDS,
  FACES,
  HAIR_CELLS,
  HAIR_UNDER,
  HAIRS,
  HEADWEAR,
  OUTFITS,
  SKIN_CELLS,
  WARDROBE,
  type BeardId,
  type FaceId,
  type HairId,
  type OutfitId,
  type WardrobeKind,
  type WardrobeShape,
} from './syntyManifest';

// The soldiers' wardrobe (docs/SIDEKICK.md): a soldier put together at run time from the pieces of Sidekick
// characters (syntyManifest.ts WARDROBE), by their look: the clothes from the neck down, a face, the hair, a beard,
// what goes on the head. Each piece is a mesh on our skeleton with three shapes (feminine, slim, heavy); here they
// are set by the soldier's sex and build, baked, and joined into one mesh on one skeleton (one draw call), painted
// from a palette of the soldier's own: the outfit's, with their skin and their hair's colour. The result is a model
// like any converted character, for SyntyFigure to clone and dress.

/** Who a soldier is, piece by piece: everything the model is made from (dressOf). */
export interface Dress {
  outfit: OutfitId;
  sex: 'f' | 'm';
  build: BuildId;
  face: FaceId;
  hair: HairId | null;
  beard: BeardId | null;
  headwear: Exclude<HeadId, 'bare'> | null;
  /** SKIN_TONES, or the dead's (a risen soldier). */
  skin: number | 'risen';
  /** HAIR_COLORS. */
  hairColor: number;
}

/** The outfits in the barracks' order, and who wears each (a woman, a man or either). */
export const OUTFIT_CHOICES: readonly { id: OutfitId; sexes: readonly ('f' | 'm')[] }[] = (Object.entries(OUTFITS) as [OutfitId, (typeof OUTFITS)[OutfitId]][]).map(([id, o]) => ({
  id,
  sexes: [...o.sexes] as ('f' | 'm')[],
}));

/** The outfits someone of this sex can wear, in the barracks' order. */
export function outfitsFor(sex: 'f' | 'm'): OutfitId[] {
  return OUTFIT_CHOICES.filter((o) => o.sexes.includes(sex)).map((o) => o.id);
}

/**
 * The survivors soldiers were before Sidekick (POLYGON's, by Appearance.character) and the prototype's, each as the
 * outfit most like them: older saves keep their look.
 */
const OLD_OUTFITS: Record<string, OutfitId> = {
  soldierM: 'survivor2',
  soldierF: 'police2',
  hunter: 'survivor6',
  sheriff: 'survivor4',
  riotCop: 'police1',
  mechanic: 'survivor3',
  scout: 'survivor1',
  wanderer: 'survivor5',
  hazmat: 'survivor8',
  nun: 'civilian2',
  eastern: 'civilian4',
  islander: 'civilian3',
  coolM: 'civilian3',
  coolF: 'civilian1',
  punk: 'survivor7',
  teenF: 'civilian1',
  skSurvivor01: 'survivor1',
  skSurvivor02: 'survivor2',
  // The outlaws' outfits, out of the barracks (Arturo, 21/10: too Borderlands for the squad).
  outlaw1: 'survivor8',
  outlaw2: 'survivor7',
};

/** Who is rescued wears a civilian's winter clothes (one of these, by their id), bareheaded. */
export const CIVIL_OUTFITS: readonly OutfitId[] = ['civilian1', 'civilian2', 'civilian3', 'civilian4'];

/** The outfit a look names (one of OUTFITS, or an older id as its likeness), or null. */
export function outfitOf(character: string | undefined): OutfitId | null {
  if (!character) return null;
  return character in OUTFITS ? (character as OutfitId) : (OLD_OUTFITS[character] ?? null);
}

/**
 * A soldier's sex as drawn: their look's; unset (older saves), their outfit's if only one sex wears it, else one
 * picked by `seed` (their campaign id), the same everywhere.
 */
export function soldierSex(look: Appearance | undefined, seed: string, sex?: 'f' | 'm'): 'f' | 'm' {
  const own = look?.sex ?? sex;
  if (own) return own;
  const outfit = outfitOf(look?.character);
  const sexes = outfit ? OUTFITS[outfit].sexes : 'fm';
  return sexes.length === 1 ? (sexes as 'f' | 'm') : pick(['f', 'm'] as const, seed + '|sex');
}

/**
 * The order recruits are dressed in when they haven't chosen (setRoster), by sex: survivors the most, then the
 * civilians' winter clothes, a policeman now and then; neighbours unlike each other.
 */
const WEAR_ORDER: Record<'f' | 'm', OutfitId[]> = {
  f: ['survivor1', 'survivor3', 'survivor7', 'survivor4', 'civilian1', 'survivor6', 'police2', 'survivor2', 'civilian4', 'survivor8', 'survivor5', 'civilian2', 'police1', 'civilian3', 'police3'],
  m: ['survivor2', 'survivor4', 'survivor8', 'survivor5', 'civilian3', 'survivor6', 'police1', 'survivor3', 'survivor7', 'civilian2', 'survivor1', 'civilian4', 'police3', 'civilian1', 'police2'],
};

/** The campaign's soldiers' outfits when they haven't chosen one (setRoster), by campaign id. */
const rostered = new Map<string, OutfitId>();

/**
 * Dresses the campaign's soldiers (every one, the fallen too: main.ts, on each campaign state): in the order they
 * joined, each the next outfit of their sex's WEAR_ORDER, so no two wear the same until all have been worn, and
 * nobody's changes when others join or fall.
 */
export function setRoster(soldiers: readonly { id: string; appearance?: Appearance }[]): void {
  rostered.clear();
  const joined = (id: string) => Number(id.replace(/\D/g, '')) || 0;
  const next = { f: 0, m: 0 };
  for (const s of [...soldiers].sort((a, b) => joined(a.id) - joined(b.id))) {
    const sex = soldierSex(s.appearance, s.id);
    const order = WEAR_ORDER[sex];
    rostered.set(s.id, order[next[sex]++ % order.length]!);
  }
}

/**
 * Skin tones (Sidekick's own, from its presets: HumanSpecies_01, Survivor 02 and 04, HumanSpecies_03, 02 and 04),
 * a colour each SKIN_CELLS cell; the dead's last.
 */
const SKIN_TONES: readonly string[][] = [
  'fcc19c f8c5a5 fcc19c fcc19c d9a78b d9a78b fcc19c d9a78b d9a78b daa07f d9a78b d9a78b fcc19c fcc19c',
  'eab38b eab38b eab38b eab38b cd9c7d cd9c7d eab38b cd9c7d cd9c7d d09a78 cd9c7d cd9c7d eab38b eab38b',
  'd5a57b d5a57b d5a57b d5a57b bb8b67 bb8b67 d5a57b bb8b67 bb8b67 bb8b67 bb8b67 bb8b67 d5a57b d5a57b',
  'ac7c56 ac7c56 ac7c56 ab805d 976b4d 976b4d ab805d 976b4d 976b4d 976b4d 976b4d 976b4d ab805d ac7c56',
  '805843 805843 805843 7e5d4c 29160c 29160c 7e5d4c 805843 805843 7e5742 805843 805843 7e5d4c 805843',
  '553d34 553d34 553d34 4c3b34 5d483f 5d483f 4c3b34 5d483f 5d483f 5b4c45 5d483f 5d483f 4c3b34 553d34',
].map((s) => s.split(' '));

/** A risen soldier's skin: Sidekick's grey-green dead (ApocalypseZombie_04). */
const RISEN_TONE = '5c6b59 5c6b59 5c6b59 494a40 494a40 494a40 373831 494a40 494a40 383f27 494a40 494a40 6d6f5b 5c6b59'.split(' ');

/** Hair colours: black, dark brown, brown, auburn, ginger, blonde, grey. */
const HAIR_COLORS = ['151515', '2b1915', '5b3b21', '8a4f2c', 'b06545', 'c9a46a', '8c8782'];

/** How often each hair colour is picked (black and browns the most). */
const HAIR_ODDS = [4, 4, 3, 1, 1, 1, 1].flatMap((n, i) => Array<number>(n).fill(i));

/** The shapes each build sets (none: the middle one), and how much of them (BUILD_WEIGHTS: the converted shapes are Sidekick's at full). */
const BUILD_SHAPES: Record<BuildId, WardrobeShape | null> = { slim: 'slim', normal: null, heavy: 'heavy' };
export const BUILD_WEIGHTS: Record<'slim' | 'heavy', number> = { slim: 1, heavy: 0.4 };

/**
 * Everything a soldier is made from, from their look and their campaign id (`seed`): their outfit (the look's, else
 * the campaign's for them, setRoster, else one of their sex's picked by the seed), what is on their head, their build; the face, the hair and its colour,
 * the skin and a man's beard, by the seed. `risen`: one of the dead now, in their clothes, the skin grey; `pool`,
 * the outfits to pick from instead (and a build by the seed), and `head` what is on the head without a look.
 */
export function dressOf(look: Appearance | undefined, seed: string, sex?: 'f' | 'm', opts: { risen?: boolean; pool?: readonly OutfitId[]; head?: HeadId } = {}): Dress {
  const s = soldierSex(look, seed, sex);
  const outfit = outfitOf(look?.character) ?? rostered.get(seed) ?? pick(opts.pool ?? outfitsFor(s), seed + '|outfit');
  const head: HeadId = look?.head ?? opts.head ?? 'helmet';
  const risen = !!opts.risen;
  const headwear = head === 'bare' ? null : head;
  const own = pick(
    (Object.keys(HAIRS) as HairId[]).filter((h) => HAIRS[h].sexes === s && h !== HAIR_UNDER[s]),
    seed + '|hair',
  );
  const under = headwear ? HEADWEAR[headwear].hair : 'own';
  const beards = Object.keys(BEARDS) as BeardId[];
  return {
    outfit,
    sex: s,
    build: look?.build ?? (opts.pool ? pick(['slim', 'normal', 'normal', 'heavy'] as const, seed + '|build') : 'normal'),
    face: pick(Object.keys(FACES) as FaceId[], seed + '|face'),
    hair: under === 'own' ? own : under === 'under' ? HAIR_UNDER[s] : null,
    // Half the men shaven.
    beard: s === 'm' ? pick([...beards, ...beards.map(() => null)], seed + '|beard') : null,
    headwear,
    skin: risen ? 'risen' : pick(SKIN_TONES.map((_, i) => i), seed + '|skin'),
    hairColor: pick(HAIR_ODDS, seed + '|hairColor'),
  };
}

/** A dress as a key: the same soldier, one model. */
export const dressKey = (d: Dress) => [d.outfit, d.sex, d.build, d.face, d.hair, d.beard, d.headwear, d.skin, d.hairColor].join('|');

/** The pieces a dress is made of, the outfit first. */
function piecesOf(d: Dress): [WardrobeKind, string][] {
  const out: [WardrobeKind, string][] = [['outfit', d.outfit], ['face', d.face]];
  if (d.hair) out.push(['hair', d.hair]);
  if (d.beard) out.push(['beard', d.beard]);
  if (d.headwear) out.push(['headwear', d.headwear]);
  return out;
}

const pieceNow = ([kind, id]: [WardrobeKind, string]) => modelNow(WARDROBE[kind][id]!.pack, 'ward', `${kind}-${id}`);
const pieceLoad = ([kind, id]: [WardrobeKind, string]) => loadModel(WARDROBE[kind][id]!.pack, 'ward', `${kind}-${id}`);

/** Models put together lately, by dress (the oldest go past MADE_MAX: their figures keep theirs). */
const made = new Map<string, THREE.Group>();
const MADE_MAX = 48;

function remember(key: string, model: THREE.Group): THREE.Group {
  made.delete(key);
  made.set(key, model);
  while (made.size > MADE_MAX) made.delete(made.keys().next().value!);
  return model;
}

/** Forgets the models put together (the labs, trying other BUILD_WEIGHTS). */
export function forgetWardrobe(): void {
  made.clear();
}

/** The soldier's model if its pieces have loaded (put together now if need be), else undefined (they start loading). */
export function wardrobeNow(d: Dress): THREE.Group | undefined {
  const key = dressKey(d);
  const done = made.get(key);
  if (done) return remember(key, done);
  const scenes = piecesOf(d).map(pieceNow);
  if (scenes.some((s) => !s)) return undefined;
  return remember(key, assemble(d, scenes as THREE.Group[]));
}

/** The soldier's model, once its pieces have loaded. */
export async function loadWardrobe(d: Dress): Promise<THREE.Group> {
  const scenes = await Promise.all(piecesOf(d).map(pieceLoad));
  return made.get(dressKey(d)) ?? remember(dressKey(d), assemble(d, scenes));
}

/** Starts loading every piece of the wardrobe (the barracks shows them all). */
export function preloadWardrobe(): void {
  for (const kind of Object.keys(WARDROBE) as WardrobeKind[]) for (const id of Object.keys(WARDROBE[kind])) void pieceLoad([kind, id]).catch(() => undefined);
}

// ----------------------------------------------------------------- putting together

const skinnedOf = (scene: THREE.Object3D) => {
  let found: THREE.SkinnedMesh | null = null;
  scene.traverse((o) => {
    if (!found && o instanceof THREE.SkinnedMesh) found = o;
  });
  if (!found) throw new Error('A wardrobe piece without a skinned mesh');
  return found as THREE.SkinnedMesh;
};

/**
 * The outfit's skeleton with all the pieces' meshes on it as one: each piece's corners in the pose at rest (its
 * shapes set, then where its bones put it: the files keep their own quantised frames), its bones by name onto the
 * outfit's, the cloth the squad's colours dye measured on the soldier's palette; the palette in userData.palette.
 */
function assemble(d: Dress, scenes: THREE.Group[]): THREE.Group {
  const model = SkeletonUtils.clone(scenes[0]!) as THREE.Group;
  model.updateMatrixWorld(true);
  const body = skinnedOf(model);
  const bones = body.skeleton.bones;
  const index = new Map(bones.map((b, i) => [b.name, i]));
  const extras = (s: THREE.Object3D) => (s.getObjectByName('rig') ?? s).userData as { palette?: string; cells?: number[] };
  const hat = d.headwear ? extras(scenes[scenes.length - 1]!) : null;
  const colors = paletteOf(d, extras(scenes[0]!).palette ?? '', hat);
  const weights: Partial<Record<WardrobeShape, number>> = { feminine: d.sex === 'f' ? 1 : 0 };
  const shape = BUILD_SHAPES[d.build];
  if (shape) weights[shape] = BUILD_WEIGHTS[shape as 'slim' | 'heavy'];
  const parts = scenes.map((s) => atRest(skinnedOf(s), weights, index, colors));
  const geometry = mergeGeometries(parts.map((p) => p.geometry));
  if (!geometry) throw new Error(`The pieces of ${dressKey(d)} would not join`);
  shiftJoints(model, bones, geometry.getAttribute('position').array as Float32Array, concat(parts.map((p) => p.base)));
  geometry.computeBoundingBox();
  geometry.computeBoundingSphere();
  body.geometry = geometry;
  // How tall it stands, the head and what is on it with the rest (the outfit's own is from the neck down).
  (model.getObjectByName('rig') ?? model).userData.height = geometry.boundingBox!.max.y;
  // Its shapes are baked in.
  body.morphTargetInfluences = undefined;
  body.morphTargetDictionary = undefined;
  // Corners where they are at rest: the bones' inverses are their places at rest, nothing between.
  body.bind(new THREE.Skeleton(bones, bones.map((b) => b.matrixWorld.clone().invert())), new THREE.Matrix4());
  model.userData.palette = paletteTexture(colors);
  return model;
}

/** How near a joint the corners that move it are (model units), all of them, whatever their bones. */
const JOINT_REACH = 0.08;

/**
 * The joints moved with the body's shapes, as Sidekick's rig follows its sliders (a heavy body's shoulders out, a
 * woman's narrower): each by the mean of how far the corners round it moved, the nearest the most. Else the arms
 * would turn about joints left inside a wider chest, and the bulk (bulk.ts) would keep them out of it.
 */
function shiftJoints(model: THREE.Object3D, bones: THREE.Bone[], shaped: Float32Array, base: Float32Array): void {
  model.updateMatrixWorld(true);
  const joints = bones.map((b) => b.getWorldPosition(new THREE.Vector3()));
  const moves = bones.map(() => new THREE.Vector3());
  const sums = bones.map(() => 0);
  const p0 = new THREE.Vector3();
  for (let v = 0; v < base.length / 3; v++) {
    p0.fromArray(base, v * 3);
    for (let j = 0; j < bones.length; j++) {
      const dist = p0.distanceTo(joints[j]!);
      if (dist >= JOINT_REACH) continue;
      const g = (1 - dist / JOINT_REACH) ** 2;
      moves[j]!.x += (shaped[v * 3]! - base[v * 3]!) * g;
      moves[j]!.y += (shaped[v * 3 + 1]! - base[v * 3 + 1]!) * g;
      moves[j]!.z += (shaped[v * 3 + 2]! - base[v * 3 + 2]!) * g;
      sums[j]! += g;
    }
  }
  // Parents first, each to its place among its moved parent's.
  const depth = (b: THREE.Object3D) => {
    let n = 0;
    for (let o = b.parent; o; o = o.parent) n++;
    return n;
  };
  const order = bones.map((_, i) => i).sort((a, b) => depth(bones[a]!) - depth(bones[b]!));
  const goal = joints.map((j, i) => (sums[i]! > 0 ? j.clone().addScaledVector(moves[i]!, 1 / sums[i]!) : null));
  for (const i of order) {
    const b = bones[i]!;
    const to = goal[i];
    if (!to || !b.parent) continue;
    b.parent.updateWorldMatrix(true, false);
    b.position.copy(b.parent.worldToLocal(to.clone()));
    b.updateMatrixWorld(true);
  }
  model.updateMatrixWorld(true);
}

function concat(arrays: Float32Array[]): Float32Array {
  const out = new Float32Array(arrays.reduce((n, a) => n + a.length, 0));
  let at = 0;
  for (const a of arrays) {
    out.set(a, at);
    at += a.length;
  }
  return out;
}

const mv = new THREE.Vector3();
const mv0 = new THREE.Vector3();
const acc0 = new THREE.Vector3();
const mn = new THREE.Vector3();
const acc = new THREE.Vector3();
const accN = new THREE.Vector3();
const t1 = new THREE.Vector3();

/**
 * A piece's mesh as plain attributes in its scene's frame at rest, its shapes baked, its bones the outfit's; and
 * where its corners were without its shapes (`base`: what moves the joints, shiftJoints).
 */
function atRest(mesh: THREE.SkinnedMesh, weights: Partial<Record<WardrobeShape, number>>, index: Map<string, number>, colors: Uint8Array): { geometry: THREE.BufferGeometry; base: Float32Array } {
  const src = mesh.geometry;
  const pos = src.getAttribute('position');
  const nor = src.getAttribute('normal');
  const uv = src.getAttribute('uv');
  const part = src.getAttribute('uv1');
  // Its folds' shade (the converter's baked "ao": synty.ts ensureShade), none where the piece has none.
  const shade = src.getAttribute('color');
  const si = src.getAttribute('skinIndex');
  const sw = src.getAttribute('skinWeight');
  const n = pos.count;
  const dict = mesh.morphTargetDictionary ?? {};
  const shapes = Object.entries(weights)
    .filter(([name, w]) => w && name in dict)
    .map(([name, w]) => ({ w: w!, p: src.morphAttributes.position?.[dict[name]!], n: src.morphAttributes.normal?.[dict[name]!] }));
  const relative = src.morphTargetsRelative;
  // Each bone's matrix at rest, from the corners as stored to the scene.
  mesh.updateMatrixWorld(true);
  const skin = mesh.skeleton.bones.map((b, i) => new THREE.Matrix4().multiplyMatrices(b.matrixWorld, mesh.skeleton.boneInverses[i]!).multiply(mesh.bindMatrix));
  const turns = skin.map((m) => new THREE.Matrix3().getNormalMatrix(m));
  const remap = mesh.skeleton.bones.map((b) => index.get(b.name) ?? 0);
  const P = new Float32Array(n * 3);
  const P0 = new Float32Array(n * 3);
  const N = new Float32Array(n * 3);
  const U = new Float32Array(n * 2);
  const D = new Float32Array(n * 2);
  const I = new Uint16Array(n * 4);
  const W = new Float32Array(n * 4);
  const S = new Uint8Array(n);
  for (let v = 0; v < n; v++) {
    S[v] = shade ? Math.round(THREE.MathUtils.clamp(shade.getX(v), 0, 1) * 255) : 255;
    mv.fromBufferAttribute(pos, v);
    mv0.copy(mv);
    mn.fromBufferAttribute(nor, v);
    for (const s of shapes) {
      if (s.p) {
        if (relative) mv.addScaledVector(t1.fromBufferAttribute(s.p, v), s.w);
        else mv.lerp(t1.fromBufferAttribute(s.p, v), s.w);
      }
      if (s.n) {
        if (relative) mn.addScaledVector(t1.fromBufferAttribute(s.n, v), s.w);
        else mn.lerp(t1.fromBufferAttribute(s.n, v), s.w);
      }
    }
    acc.set(0, 0, 0);
    acc0.set(0, 0, 0);
    accN.set(0, 0, 0);
    for (let k = 0; k < 4; k++) {
      const w = sw.getComponent(v, k);
      const b = si.getComponent(v, k);
      I[v * 4 + k] = remap[b] ?? 0;
      W[v * 4 + k] = w;
      if (w <= 0) continue;
      acc.addScaledVector(t1.copy(mv).applyMatrix4(skin[b]!), w);
      acc0.addScaledVector(t1.copy(mv0).applyMatrix4(skin[b]!), w);
      accN.addScaledVector(t1.copy(mn).applyMatrix3(turns[b]!), w);
    }
    acc.toArray(P, v * 3);
    acc0.toArray(P0, v * 3);
    accN.normalize().toArray(N, v * 3);
    const u = uv.getX(v);
    const t = uv.getY(v);
    U[v * 2] = u;
    U[v * 2 + 1] = t;
    // The part map (convert.py write_parts): u the piece, v cloth (0) or not (1), measured again on this palette.
    const piece = part ? part.getX(v) : 0;
    D[v * 2] = piece;
    D[v * 2 + 1] = Math.floor(piece * 8) > 0 && cloth(colors, u, t) ? 0 : 1;
  }
  const out = new THREE.BufferGeometry();
  out.setAttribute('position', new THREE.BufferAttribute(P, 3));
  out.setAttribute('normal', new THREE.BufferAttribute(N, 3));
  out.setAttribute('uv', new THREE.BufferAttribute(U, 2));
  out.setAttribute('uv1', new THREE.BufferAttribute(D, 2));
  out.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(I, 4));
  out.setAttribute('skinWeight', new THREE.BufferAttribute(W, 4));
  out.setAttribute('color', new THREE.BufferAttribute(S, 1, true));
  const idx = src.getIndex();
  if (idx) out.setIndex(new THREE.Uint32BufferAttribute(Uint32Array.from(idx.array as ArrayLike<number>), 1));
  return { geometry: out, base: P0 };
}

// ----------------------------------------------------------------- the palette

/** The cells of a Sidekick palette that are a body's, not its clothes': the skin, the eyes, the teeth, the hair. */
const BODY_CELLS = new Set<number>();
for (let y = 10; y < 15; y++) for (let x = 0; x < 8; x++) BODY_CELLS.add(y * 16 + x);
BODY_CELLS.add(9 * 16 + 3);
BODY_CELLS.add(9 * 16 + 8);

/**
 * Whether the squad's colours may dye a texel (convert.py dyeable): cloth, so not the body's cells, not near black
 * (boots, belts, straps), not near white (eyes, teeth, badges).
 */
function cloth(colors: Uint8Array, u: number, v: number): boolean {
  const c = Math.min(15, Math.max(0, Math.floor(u * 16))) + Math.min(15, Math.max(0, Math.floor(v * 16))) * 16;
  if (BODY_CELLS.has(c)) return false;
  const r = colors[c * 4]! / 255;
  const g = colors[c * 4 + 1]! / 255;
  const b = colors[c * 4 + 2]! / 255;
  const hi = Math.max(r, g, b);
  const lo = Math.min(r, g, b);
  const s = hi > 0 ? (hi - lo) / hi : 0;
  return !(hi < 0.16 || (hi > 0.93 && s < 0.08));
}

/**
 * The soldier's palette, RGBA a cell (16 × 16, row by row from the top): the outfit's; what they have on their head
 * in its own colours (`hat`: its palette and the cells it is painted from, the body's left as they are); their skin,
 * their hair's colour.
 */
function paletteOf(d: Dress, hex: string, hat: { palette?: string; cells?: number[] } | null): Uint8Array {
  const out = new Uint8Array(256 * 4);
  const set = (cell: number, h: string, k = 1) => {
    for (let i = 0; i < 3; i++) out[cell * 4 + i] = Math.round(parseInt(h.slice(i * 2, i * 2 + 2), 16) * k);
    out[cell * 4 + 3] = 255;
  };
  for (let c = 0; c < 256; c++) set(c, hex.slice(c * 6, c * 6 + 6) || '808080');
  for (const c of hat?.cells ?? []) if (!BODY_CELLS.has(c) && hat?.palette) set(c, hat.palette.slice(c * 6, c * 6 + 6));
  const tone = d.skin === 'risen' ? RISEN_TONE : SKIN_TONES[d.skin]!;
  SKIN_CELLS.forEach(([x, y], i) => set(y * 16 + x, tone[i]!));
  // The eyebrows a little darker than the hair.
  const hair = HAIR_COLORS[d.hairColor]!;
  HAIR_CELLS.forEach(([x, y]) => set(y * 16 + x, hair, y === 10 ? 0.8 : 1));
  return out;
}

function paletteTexture(colors: Uint8Array): THREE.DataTexture {
  const t = new THREE.DataTexture(colors, 16, 16, THREE.RGBAFormat);
  t.colorSpace = THREE.SRGBColorSpace;
  t.magFilter = THREE.NearestFilter;
  t.minFilter = THREE.NearestFilter;
  t.generateMipmaps = false;
  t.needsUpdate = true;
  return t;
}

/** Stable pick from a list by a string (FNV-1a), as syntyFigures' own. */
function pick<T>(list: readonly T[], seed: string): T {
  let h = 0x811c9dc5;
  for (let i = 0; i < seed.length; i++) h = Math.imul(h ^ seed.charCodeAt(i), 0x01000193);
  return list[(h >>> 0) % list.length]!;
}
