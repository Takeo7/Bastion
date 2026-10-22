// Converts the Synty assets listed in src/game/syntyManifest.ts to what the
// game loads, under packages/client/public/models/synty/<pack>/:
//
//   npx tsx packages/client/tools/synty/convert.ts            only what is missing
//   npx tsx packages/client/tools/synty/convert.ts --force    everything again
//   npx tsx packages/client/tools/synty/convert.ts hunter sofa   just these ids
//
// Blender (BLENDER, or the macOS default path) does the conversion headless
// with convert.py; then gltf-transform (through npx) compresses each GLB with
// meshopt. The Synty packs are read from assets-src/synty/, which never goes
// in the repository; a pack that only comes as a Unity package is extracted
// there first. See README.md.

import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, readFileSync, renameSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, dirname, join, resolve } from 'node:path';
import {
  ANIMATIONS,
  ATLASES,
  CHARACTERS,
  FX_MODELS,
  FX_TEXTURES,
  fxTextureFile,
  UI_MODELS,
  OUTFIT_LEAVES,
  SKIN_CELLS,
  WARDROBE,
  WARDROBE_SHAPES,
  wardrobeFile,
  type WardrobeAsset,
  type WardrobeKind,
  PACKS,
  PIECES,
  PROPS,
  TERRAIN,
  SKY_TEXTURES,
  SKY_MODELS,
  skyTextureFile,
  SYNTY_SCALE,
  WEAPONS,
  WEAPON_PARTS,
  BASE_MODELS,
  BASE_TEXTURES,
  atlasFile,
  baseTextureFile,
  modelFile,
  terrainFile,
  type BaseAsset,
  type AtlasId,
  type CharacterAsset,
  type PieceAsset,
  type PropAsset,
  type PackId,
} from '../../src/game/syntyManifest.ts';

const ROOT = resolve(import.meta.dirname, '../../../..');
const SOURCES = join(ROOT, 'assets-src/synty');
const PUBLIC = join(ROOT, 'packages/client/public');
const BLENDER = process.env.BLENDER ?? '/Applications/Blender.app/Contents/MacOS/Blender';
const SCRIPT = join(import.meta.dirname, 'convert.py');

const args = process.argv.slice(2);
const force = args.includes('--force');
const only = new Set(args.filter((a) => !a.startsWith('--')));

// ------------------------------------------------------------------ packs

/** Extracts the FBX and textures of a Unity package (a tar.gz of <guid>/{pathname,asset}) into its pack folder. */
function extractUnityPackage(pack: PackId): void {
  const { dir, unitypackage } = PACKS[pack];
  const target = join(SOURCES, dir);
  if (!unitypackage || existsSync(target)) return;
  const temp = join(SOURCES, `${dir}.tmp`);
  rmSync(temp, { recursive: true, force: true });
  mkdirSync(temp, { recursive: true });
  console.log(`Extracting ${unitypackage}…`);
  const tar = spawnSync('tar', ['-xzf', join(SOURCES, unitypackage), '-C', temp], { stdio: 'inherit' });
  if (tar.status !== 0) throw new Error(`tar failed on ${unitypackage}`);
  for (const guid of readdirSync(temp)) {
    const pathname = join(temp, guid, 'pathname');
    const asset = join(temp, guid, 'asset');
    if (!existsSync(pathname) || !existsSync(asset)) continue;
    const path = readFileSync(pathname, 'utf8').split('\n')[0]!.trim();
    // (Sidekick's presets, .sk: which parts make a character, its palette and its build.)
    if (!/\.(fbx|png|tga|sk)$/i.test(path)) continue;
    mkdirSync(dirname(join(target, path)), { recursive: true });
    renameSync(asset, join(target, path));
  }
  rmSync(temp, { recursive: true, force: true });
}

/** Every FBX of a pack by name; the Unity-ready copies win over the Unreal ones. */
const fbxIndex = new Map<PackId, Map<string, string>>();

function findFbx(pack: PackId, name: string): string {
  // A path inside the pack, for names that repeat in it (animation packs hold several rigs).
  if (name.includes('/')) {
    const path = join(SOURCES, PACKS[pack].dir, `${name}.fbx`);
    if (!existsSync(path)) throw new Error(`${name}.fbx is not in ${PACKS[pack].dir}`);
    return path;
  }
  let index = fbxIndex.get(pack);
  if (!index) {
    index = new Map();
    const walk = (dir: string) => {
      for (const entry of readdirSync(dir, { withFileTypes: true })) {
        const path = join(dir, entry.name);
        if (entry.isDirectory()) walk(path);
        else if (/\.fbx$/i.test(entry.name)) {
          const key = basename(entry.name, '.fbx');
          const worse = /unreal|unsupported|[\\/]collision[\\/]/i.test(path);
          if (!index!.has(key) || (!worse && /unreal|unsupported|[\\/]collision[\\/]/i.test(index!.get(key)!))) index!.set(key, path);
        }
      }
    };
    walk(join(SOURCES, PACKS[pack].dir));
    fbxIndex.set(pack, index);
  }
  const path = index.get(name);
  if (!path) throw new Error(`${name}.fbx is not in ${PACKS[pack].dir}`);
  return path;
}

/** Albedo texture of each material slot of each mesh, per prefab, from the pack's MaterialList. */
const materialLists = new Map<PackId, Map<string, Map<string, string[]>>>();

function slotsOf(pack: PackId, prefab: string): Record<string, (AtlasId | null)[]> {
  let list = materialLists.get(pack);
  if (!list) {
    list = new Map();
    const file = PACKS[pack].materialList;
    if (file) {
      let meshes: Map<string, string[]> | null = null;
      let slots: string[] | null = null;
      for (const line of readFileSync(join(SOURCES, PACKS[pack].dir, file), 'utf8').split(/\r?\n/)) {
        const t = line.trim();
        if (t.startsWith('Prefab Name:')) list.set(t.slice(12).trim(), (meshes = new Map()));
        else if (t.startsWith('Mesh Name:') && meshes) meshes.set(t.slice(10).trim(), (slots = []));
        else if (t.startsWith('Albedo:') && slots) slots.push(t.slice(7).trim().split(' ')[0]!);
      }
    }
    materialLists.set(pack, list);
  }
  const byAlbedo = new Map(Object.entries(ATLASES).map(([id, a]) => [a.albedo, id as AtlasId]));
  const out: Record<string, (AtlasId | null)[]> = {};
  for (const [mesh, slots] of list.get(prefab) ?? []) out[mesh] = slots.map((s) => byAlbedo.get(s) ?? null);
  return out;
}

/** The pack's own atlas. */
function atlasOf(pack: PackId): AtlasId {
  const id = PACKS[pack].atlas ?? (Object.keys(ATLASES) as AtlasId[]).find((a) => ATLASES[a].pack === pack);
  if (!id) throw new Error(`No atlas for pack ${pack}`);
  return id;
}

const atlasImage = (atlas: AtlasId) => join(SOURCES, PACKS[ATLASES[atlas].pack].dir, ATLASES[atlas].src);

// ------------------------------------------------------------------- jobs

interface Job {
  kind: 'character' | 'sidekick' | 'wardrobe' | 'weapon' | 'prop' | 'texture' | 'anim' | 'mixamo' | 'piece';
  id: string;
  out: string;
  [key: string]: unknown;
}

// --------------------------------------------------------- what to convert

for (const pack of Object.keys(PACKS) as PackId[]) extractUnityPackage(pack);

const jobs: Job[] = [];

/**
 * What each converted file was made from (sources.json next to them), so a
 * file whose manifest entry changed is converted again; files the manifest
 * no longer lists are deleted.
 */
const SOURCES_FILE = join(PUBLIC, 'models/synty/sources.json');
const made: Record<string, string> = existsSync(SOURCES_FILE) ? (JSON.parse(readFileSync(SOURCES_FILE, 'utf8')) as Record<string, string>) : {};
const listed = new Set<string>();
const sourceOf = (job: Omit<Job, 'out'>) => JSON.stringify(job).replaceAll(SOURCES, '');

function want(job: Job): boolean {
  const key = job.out.slice(PUBLIC.length + 1);
  listed.add(key);
  if (only.size) return only.has(job.id);
  return force || !existsSync(job.out) || made[key] !== sourceOf({ ...job, out: undefined });
}

function add(job: Job): void {
  if (want(job)) jobs.push(job);
}

for (const [id, a] of Object.entries(ATLASES) as [AtlasId, (typeof ATLASES)[AtlasId]][]) {
  // (A Sidekick character's palette is made with it: its job below.)
  if ('composed' in a && a.composed) continue;
  add({ kind: 'texture', id, out: join(PUBLIC, atlasFile(id)), src: atlasImage(id), size: a.size, ...('palette' in a && a.palette ? { palette: true } : {}) });
}

for (const [id, t] of Object.entries(TERRAIN) as [keyof typeof TERRAIN, (typeof TERRAIN)[keyof typeof TERRAIN]][]) {
  add({ kind: 'texture', id, out: join(PUBLIC, terrainFile(id)), src: join(SOURCES, t.src), size: t.size });
}

// The sky (SKY_TEXTURES: a strip of each source's rows, at its size; SKY_MODELS: its clouds, as props).
for (const [id, t] of Object.entries(SKY_TEXTURES) as [keyof typeof SKY_TEXTURES, (typeof SKY_TEXTURES)[keyof typeof SKY_TEXTURES]][]) {
  add({ kind: 'texture', id, out: join(PUBLIC, skyTextureFile(id)), src: join(SOURCES, t.src), rows: t.rows, width: t.width, height: t.height });
}
for (const [id, m] of Object.entries(SKY_MODELS) as [keyof typeof SKY_MODELS, (typeof SKY_MODELS)[keyof typeof SKY_MODELS]][]) {
  add({ kind: 'prop', id, name: id, out: join(PUBLIC, modelFile(m.pack, 'sky', id)), fbx: findFbx(m.pack, m.fbx), atlas: 'generic', slots: {} });
}

for (const [id, c] of Object.entries(CHARACTERS) as [string, CharacterAsset][]) {
  if (c.sidekick) continue;
  const atlas = atlasOf(c.pack);
  add({
    kind: 'character',
    id,
    out: join(PUBLIC, modelFile(c.pack, 'chr', id)),
    fbx: findFbx(c.pack, c.fbx),
    mesh: c.mesh ?? null,
    atlas,
    atlasImage: atlasImage(atlas),
    attach: (c.attach ?? []).map((a) => ({ ...a, fbx: findFbx(c.pack, a.fbx) })),
    // One mesh, its hair and what hangs on it with the body (one draw call a pass).
    merge: true,
  });
}

// Sidekick: its packs' presets (Characters/<set>/<preset>/<preset>.sk: a character's parts, build and palette), which
// preset wears each part first, and which pack a part is in by its name.
const SIDEKICK_PACKS = (Object.keys(PACKS) as PackId[]).filter((p) => p.startsWith('sidekick'));
const presetDirs = new Map<string, string>();
function presetDir(pack: PackId, preset: string): string {
  const key = `${pack}/${preset}`;
  let dir = presetDirs.get(key);
  if (!dir) {
    const characters = join(SOURCES, PACKS[pack].dir, 'Assets/Synty/SidekickCharacters/Characters');
    const set = readdirSync(characters).find((s) => existsSync(join(characters, s, preset, `${preset}.sk`)));
    if (!set) throw new Error(`No Sidekick preset ${preset} in ${PACKS[pack].dir}`);
    presetDirs.set(key, (dir = join(characters, set, preset)));
  }
  return dir;
}
const presetParts = (pack: PackId, preset: string) =>
  [...readFileSync(join(presetDir(pack, preset), `${preset}.sk`), 'utf8').matchAll(/- Name: (SK_\w+)\s+PartType: (\w+)/g)].map(([, name, type]) => ({ name: name!, type: type! }));
const presetPalette = (pack: PackId, preset: string) => join(presetDir(pack, preset), 'Textures', `T_${preset}ColorMap.png`);
/** Every Sidekick pack's presets (the species' bases last), and which wears each part first. */
const presets: { pack: PackId; preset: string }[] = SIDEKICK_PACKS.flatMap((pack) => {
  const characters = join(SOURCES, PACKS[pack].dir, 'Assets/Synty/SidekickCharacters/Characters');
  if (!existsSync(characters)) return [];
  return readdirSync(characters).flatMap((set) => readdirSync(join(characters, set)).filter((p) => existsSync(join(characters, set, p, `${p}.sk`))).map((preset) => ({ pack, preset })));
}).sort((a, b) => Number(a.preset.startsWith('HumanSpecies')) - Number(b.preset.startsWith('HumanSpecies')));
const wornBy = new Map<string, { pack: PackId; preset: string }>();
for (const p of presets) for (const { name } of presetParts(p.pack, p.preset)) if (!wornBy.has(name)) wornBy.set(name, p);
/** The pack whose parts a part's name says it is (SK_APOC_OUTL_…: Outlaws); the species' are in every pack. */
const PART_PACKS: [RegExp, PackId][] = [[/^SK_APOC_SURV_/, 'sidekickSurvivors'], [/^SK_APOC_OUTL_/, 'sidekickOutlaws'], [/^SK_MDRN_CIVL_/, 'sidekickCivilians'], [/^SK_MDRN_POLC_/, 'sidekickPolice'], [/^SK_(APOC_ZOMB|ZOMB_BASE)_/, 'sidekickZombies']];
const partPack = (name: string, own: PackId) => PART_PACKS.find(([re]) => re.test(name))?.[1] ?? own;
function partPalette(name: string, own: PackId): string {
  const by = wornBy.get(name) ?? presets.find((p) => p.pack === partPack(name, own));
  return by ? presetPalette(by.pack, by.preset) : presetPalette('sidekickSurvivors', 'ApocalypseSurvivor_02');
}
/** Every palette of a part's pack and then the rest: where a cell a palette left unused (red) is looked for next. */
const fallbacks = (pack: PackId) => [...presets.filter((p) => p.pack === pack), ...presets.filter((p) => p.pack !== pack)].map((p) => presetPalette(p.pack, p.preset));
const hasFbx = (pack: PackId, name: string) => {
  try {
    findFbx(pack, name);
    return true;
  } catch {
    return false;
  }
};

// Sidekick's characters made whole (CHARACTERS with `sidekick`: the dead, the living, the VIP): their preset's parts
// (the teeth and the tongue never show; nor what `drop` leaves off; `swap` puts others in for some types; `undead`
// makes a living one dead: the zombies' head, face and bare body, picked by its id), its build (the preset's
// BlendShapes as the shape keys' values), and a palette of the parts' own (each as a preset that wears it paints it),
// saved as the character's atlas (the same id).
const SIDEKICK_NEVER = new Set(['Teeth', 'Tongue']);
const zb = (n: number, code: string) => `SK_ZOMB_BASE_${String(n).padStart(2, '0')}_${code}_ZB01`;
const UNDEAD: Record<string, (n: number) => string> = {
  Head: () => zb(1, '01HEAD'),
  EyeLeft: () => zb(1, '05EYEL'),
  EyeRight: () => zb(1, '06EYER'),
  EarLeft: () => zb(1, '07EARL'),
  EarRight: () => zb(1, '08EARR'),
  Nose: (n) => zb((n % 6) + 1, '35NOSE'),
  EyebrowLeft: (n) => zb((n % 8) + 1, '03EBRL'),
  EyebrowRight: (n) => zb((n % 8) + 1, '04EBRR'),
  Hair: (n) => zb((n % 10) + 1, '02HAIR'),
  FacialHair: (n) => zb((n % 4) + 1, '09FCHR'),
};
const idNumber = (id: string) => [...id].reduce((h, ch) => (h * 31 + ch.charCodeAt(0)) >>> 0, 7);
for (const [id, c] of Object.entries(CHARACTERS) as [string, CharacterAsset][]) {
  if (!c.sidekick) continue;
  const preset = c.sidekick;
  const sk = readFileSync(join(presetDir(c.pack, preset), `${preset}.sk`), 'utf8');
  const n = idNumber(id);
  const own = presetParts(c.pack, preset).filter((p) => !SIDEKICK_NEVER.has(p.type) && !(c.drop ?? []).includes(p.type));
  const parts = own.map(({ name, type }) => {
    if (c.swap?.[type]) return c.swap[type]!;
    if (c.undead && UNDEAD[type]) return UNDEAD[type]!(n);
    // A bare arm, hand or leg the zombies' bare one.
    const dead = name.replace(/^SK_HUMN_BASE_\d\d_/, 'SK_ZOMB_BASE_01_').replace(/_HU01$/, '_ZB01');
    if (c.undead && name.startsWith('SK_HUMN_BASE_') && hasFbx('sidekickZombies', dead)) return dead;
    return name;
  });
  for (const [type, name] of Object.entries(c.swap ?? {})) if (!own.some((p) => p.type === type)) parts.push(name);
  const value = (key: string) => Number(sk.match(new RegExp(`${key}: (-?[\\d.]+)`))?.[1] ?? 0);
  const [type, size, muscle] = [value('BodyTypeValue'), value('BodySizeValue'), value('MuscleValue')];
  add({
    kind: 'sidekick',
    id,
    out: join(PUBLIC, modelFile(c.pack, 'chr', id)),
    parts: parts.map((name) => findFbx(partPack(name, c.pack), name)),
    palettes: parts.map((name) => partPalette(name, c.pack)),
    fallbacks: fallbacks(c.pack),
    // One of the dead: the skin, the eyes and the hair as one of Sidekick's zombies has them, whatever its clothes'.
    ...(c.undead ? { bodyPalette: presetPalette('sidekickZombies', `ApocalypseZombie_0${(n % 5) + 1}`) } : {}),
    // Sidekick's sliders (±100) on its shape keys (0 to 1): feminine at +100, heavy or skinny, buff.
    body: { masculineFeminine: (type + 100) / 200, defaultHeavy: Math.max(0, size) / 100, defaultSkinny: Math.max(0, -size) / 100, defaultBuff: Math.max(0, muscle) / 100 },
    atlas: id,
    paletteOut: join(PUBLIC, atlasFile(id as AtlasId)),
    paint: Object.fromEntries(Object.entries(c.paint ?? {}).map(([cell, hex]) => [cell, [0, 2, 4].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)])),
  });
}

for (const kind of Object.keys(WARDROBE) as WardrobeKind[]) {
  for (const [id, w] of Object.entries(WARDROBE[kind]) as [string, WardrobeAsset][]) {
    const parts = w.parts ?? presetParts(w.pack, w.preset).filter((p) => !OUTFIT_LEAVES.includes(p.type)).map((p) => p.name);
    add({
      kind: 'wardrobe',
      id,
      out: join(PUBLIC, wardrobeFile(kind, id)),
      piece: kind,
      parts: parts.map((name) => findFbx(partPack(name, w.pack), name)),
      palettes: parts.map((name) => partPalette(name, w.pack)),
      shapes: WARDROBE_SHAPES,
      atlasImage: presetPalette(w.pack, w.preset),
      fallback: presetPalette('sidekickSurvivors', 'ApocalypseSurvivor_02'),
      skins: SKIN_CELLS.map(([x, y]) => [(x + 0.5) / 16, 1 - (y + 0.5) / 16]),
      // Cells given a colour of their own (only those that have some: the others' jobs as they were).
      ...(w.paint ? { paint: Object.fromEntries(Object.entries(w.paint).map(([cell, hex]) => [cell, [0, 2, 4].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)])) } : {}),
    });
  }
}


for (const [id, w] of Object.entries(WEAPONS)) {
  add({ kind: 'weapon', id, name: id, out: join(PUBLIC, modelFile(w.pack, 'wep', id)), fbx: findFbx(w.pack, w.fbx), atlas: atlasOf(w.pack), slots: slotsOf(w.pack, w.fbx) });
}

for (const [id, w] of Object.entries(WEAPON_PARTS)) {
  add({ kind: 'weapon', id, name: id, out: join(PUBLIC, modelFile(w.pack, 'wep', id)), fbx: findFbx(w.pack, w.fbx), atlas: atlasOf(w.pack), slots: slotsOf(w.pack, w.fbx) });
}

for (const [id, a] of Object.entries(ANIMATIONS)) {
  // Mixamo's turned onto Synty's skeleton, from its own T-pose to Base Locomotion's (convert.py mixamo).
  if (a.pack === 'mixamo') {
    const dir = join(SOURCES, PACKS.mixamo.dir);
    const mirror = 'mirror' in a && a.mirror ? { mirror: true } : {};
    add({ kind: 'mixamo', id, out: join(PUBLIC, modelFile(a.pack, 'anim', id)), fbx: join(dir, `${a.fbx}.fbx`), tpose: join(dir, 'T-Pose.fbx'), target: findFbx(ANIMATIONS.tpose.pack, ANIMATIONS.tpose.fbx), ...mirror });
    continue;
  }
  add({ kind: 'anim', id, out: join(PUBLIC, modelFile(a.pack, 'anim', id)), fbx: findFbx(a.pack, a.fbx) });
}

// The effects' shapes convert like props; their sprites like atlases (alpha kept).
for (const [id, f] of Object.entries(FX_MODELS)) {
  add({ kind: 'prop', id, name: id, out: join(PUBLIC, modelFile(f.pack, 'fx', id)), fbx: findFbx(f.pack, f.fbx), atlas: atlasOf(f.pack), slots: {} });
}

// The interface's renders' models convert like props too.
for (const [id, u] of Object.entries(UI_MODELS)) {
  add({ kind: 'prop', id, name: id, out: join(PUBLIC, modelFile(u.pack, 'ui', id)), fbx: findFbx(u.pack, u.fbx), atlas: atlasOf(u.pack), slots: slotsOf(u.pack, u.fbx) });
}

for (const [id, t] of Object.entries(FX_TEXTURES) as [keyof typeof FX_TEXTURES, (typeof FX_TEXTURES)[keyof typeof FX_TEXTURES]][]) {
  add({ kind: 'texture', id, out: join(PUBLIC, fxTextureFile(id)), src: join(SOURCES, PACKS.particleFx.dir, t.src), size: t.size });
}

for (const [id, p] of Object.entries(PROPS) as [string, PropAsset][]) {
  // A base model reused as furniture has no GLB of its own; a baked one is its tool's (bake-medical-corpses.mjs).
  if (p.base || p.baked) continue;
  add({ kind: 'prop', id, name: id, out: join(PUBLIC, modelFile(p.pack, 'prop', id)), fbx: findFbx(p.pack, p.fbx), atlas: p.atlas ?? atlasOf(p.pack), slots: slotsOf(p.pack, p.fbx), keep: p.keep ?? null, hole: p.hole ?? null, plain: p.plain ?? null, plainMeshes: p.plainMeshes ?? [] });
}

for (const [id, p] of Object.entries(PIECES) as [string, PieceAsset][]) {
  const piece = { keep: p.keep ?? null, parts: !!p.parts, drop: p.drop ?? [], leaf: p.leaf ?? null };
  add({ kind: 'piece', id, name: id, out: join(PUBLIC, modelFile(p.pack, 'piece', id)), fbx: findFbx(p.pack, p.fbx), atlas: p.atlas ?? atlasOf(p.pack), slots: slotsOf(p.pack, p.fbx), ...piece });
}

// The base's models convert as whole pieces (no cut); their fbx is a path in the pack.
for (const [id, b] of Object.entries(BASE_MODELS) as [string, BaseAsset][]) {
  const piece = { keep: null, parts: !!b.parts, drop: b.drop ?? [], plain: b.plain ?? null };
  add({ kind: 'piece', id, name: id, out: join(PUBLIC, modelFile(b.pack, 'base', id)), fbx: findFbx(b.pack, b.fbx), atlas: b.atlas ?? atlasOf(b.pack), slots: slotsOf(b.pack, basename(b.fbx)), ...piece });
}

for (const [id, t] of Object.entries(BASE_TEXTURES) as [keyof typeof BASE_TEXTURES, (typeof BASE_TEXTURES)[keyof typeof BASE_TEXTURES]][]) {
  add({ kind: 'texture', id, out: join(PUBLIC, baseTextureFile(id)), src: join(SOURCES, t.src), size: t.size });
}

// ---------------------------------------------------------------- convert

/** Deletes converted files the manifest no longer lists, and forgets them. */
function prune(): void {
  const root = join(PUBLIC, 'models/synty');
  if (!existsSync(root)) return;
  for (const pack of readdirSync(root, { withFileTypes: true })) {
    if (!pack.isDirectory()) continue;
    for (const file of readdirSync(join(root, pack.name))) {
      const key = `models/synty/${pack.name}/${file}`;
      if (listed.has(key)) continue;
      rmSync(join(root, pack.name, file));
      delete made[key];
      console.log(`Removed ${key} (no longer in the manifest).`);
    }
  }
}

async function run(): Promise<void> {
  if (!only.size) prune();
  if (!jobs.length) {
    writeFileSync(SOURCES_FILE, JSON.stringify(made, null, 1) + '\n');
    console.log('Nothing to convert (add --force to redo everything).');
    return;
  }
  for (const j of jobs) mkdirSync(dirname(j.out), { recursive: true });
  const spec = join(tmpdir(), `synty-jobs-${process.pid}.json`);
  writeFileSync(spec, JSON.stringify({ scale: SYNTY_SCALE, jobs }, null, 1));
  console.log(`Blender: ${jobs.length} jobs…`);
  const blender = spawnSync(BLENDER, ['--background', '--factory-startup', '--python', SCRIPT, '--', spec], { encoding: 'utf8', maxBuffer: 1 << 28 });
  rmSync(spec, { force: true });
  const log = `${blender.stdout}\n${blender.stderr}`;
  const errors = log.split('\n').filter((l) => /Traceback|Error:|error:/.test(l));
  if (blender.status !== 0 || errors.length) {
    console.error(log.split('\n').filter((l) => !/^WARNING: User property|^FBX version|reports\s+\| WARNING/.test(l)).slice(-60).join('\n'));
    throw new Error('Blender failed');
  }
  const models = jobs.filter((j) => j.kind !== 'texture');
  console.log(`gltf-transform: compressing ${models.length} models…`);
  const queue = [...models];
  const worker = async () => {
    for (let j = queue.shift(); j; j = queue.shift()) await compress(j.out);
  };
  await Promise.all(Array.from({ length: 6 }, worker));
  for (const j of jobs) made[j.out.slice(PUBLIC.length + 1)] = sourceOf({ ...j, out: undefined });
  writeFileSync(SOURCES_FILE, JSON.stringify(made, null, 1) + '\n');
  let total = 0;
  for (const j of jobs) total += statSync(j.out).size;
  console.log(`Done: ${jobs.length} files, ${(total / 1024).toFixed(0)} KB.`);
}

/** Meshopt compression in place, keeping skeletons, hung attachments and the "part" UV map as they are. */
function compress(file: string): Promise<void> {
  const temp = `${file}.tmp.glb`;
  const flags = ['--flatten', 'false', '--join', 'false', '--instance', 'false', '--palette', 'false', '--simplify', 'false', '--prune-attributes', 'false', '--texture-compress', 'false'];
  return new Promise((done, fail) => {
    let err = '';
    const p = spawn('npx', ['--yes', '@gltf-transform/cli@4.5.1', 'optimize', file, temp, '--compress', 'meshopt', ...flags]);
    p.stderr.on('data', (d) => (err += d));
    p.on('close', (code) => {
      if (code !== 0) return fail(new Error(`gltf-transform failed on ${file}:\n${err}`));
      renameSync(temp, file);
      done();
    });
  });
}

await run();
