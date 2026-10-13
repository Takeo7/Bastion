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
  SURVIVOR_CHOICES,
  HEADGEAR,
  PACKS,
  PIECES,
  PROPS,
  TERRAIN,
  SYNTY_SCALE,
  WEAPONS,
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
    if (!/\.(fbx|png|tga)$/i.test(path)) continue;
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
  const id = (Object.keys(ATLASES) as AtlasId[]).find((a) => ATLASES[a].pack === pack);
  if (!id) throw new Error(`No atlas for pack ${pack}`);
  return id;
}

const atlasImage = (atlas: AtlasId) => join(SOURCES, PACKS[ATLASES[atlas].pack].dir, ATLASES[atlas].src);

// ------------------------------------------------------------------- jobs

interface Job {
  kind: 'character' | 'headgear' | 'weapon' | 'prop' | 'texture' | 'anim' | 'piece';
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
  add({ kind: 'texture', id, out: join(PUBLIC, atlasFile(id)), src: atlasImage(id), size: a.size });
}

for (const [id, t] of Object.entries(TERRAIN) as [keyof typeof TERRAIN, (typeof TERRAIN)[keyof typeof TERRAIN]][]) {
  add({ kind: 'texture', id, out: join(PUBLIC, terrainFile(id)), src: join(SOURCES, t.src), size: t.size });
}

for (const [id, c] of Object.entries(CHARACTERS) as [string, CharacterAsset][]) {
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
    // One mesh for those whose hair never comes off; a survivor's stays apart, to hide under a hat.
    merge: !SURVIVOR_CHOICES.some((s) => s.id === id),
  });
}

for (const [id, h] of Object.entries(HEADGEAR)) {
  const atlas = atlasOf(h.pack);
  add({ kind: 'headgear', id, name: id, out: join(PUBLIC, modelFile(h.pack, 'hat', id)), fbx: findFbx(h.pack, h.fbx), atlas, atlasImage: atlasImage(atlas), dye: h.dye });
}

for (const [id, w] of Object.entries(WEAPONS)) {
  add({ kind: 'weapon', id, name: id, out: join(PUBLIC, modelFile(w.pack, 'wep', id)), fbx: findFbx(w.pack, w.fbx), atlas: atlasOf(w.pack), slots: slotsOf(w.pack, w.fbx) });
}

for (const [id, a] of Object.entries(ANIMATIONS)) {
  add({ kind: 'anim', id, out: join(PUBLIC, modelFile(a.pack, 'anim', id)), fbx: findFbx(a.pack, a.fbx) });
}

// The effects' shapes convert like props; their sprites like atlases (alpha kept).
for (const [id, f] of Object.entries(FX_MODELS)) {
  add({ kind: 'prop', id, name: id, out: join(PUBLIC, modelFile(f.pack, 'fx', id)), fbx: findFbx(f.pack, f.fbx), atlas: atlasOf(f.pack), slots: {} });
}

for (const [id, t] of Object.entries(FX_TEXTURES) as [keyof typeof FX_TEXTURES, (typeof FX_TEXTURES)[keyof typeof FX_TEXTURES]][]) {
  add({ kind: 'texture', id, out: join(PUBLIC, fxTextureFile(id)), src: join(SOURCES, PACKS.particleFx.dir, t.src), size: t.size });
}

for (const [id, p] of Object.entries(PROPS)) {
  add({ kind: 'prop', id, name: id, out: join(PUBLIC, modelFile(p.pack, 'prop', id)), fbx: findFbx(p.pack, p.fbx), atlas: atlasOf(p.pack), slots: slotsOf(p.pack, p.fbx) });
}

for (const [id, p] of Object.entries(PIECES) as [string, PieceAsset][]) {
  const piece = { keep: p.keep ?? null, parts: !!p.parts, drop: p.drop ?? [] };
  add({ kind: 'piece', id, name: id, out: join(PUBLIC, modelFile(p.pack, 'piece', id)), fbx: findFbx(p.pack, p.fbx), atlas: atlasOf(p.pack), slots: slotsOf(p.pack, p.fbx), ...piece });
}

// The base's models convert as whole pieces (no cut); their fbx is a path in the pack.
for (const [id, b] of Object.entries(BASE_MODELS) as [string, BaseAsset][]) {
  const piece = { keep: null, parts: !!b.parts, drop: b.drop ?? [] };
  add({ kind: 'piece', id, name: id, out: join(PUBLIC, modelFile(b.pack, 'base', id)), fbx: findFbx(b.pack, b.fbx), atlas: atlasOf(b.pack), slots: slotsOf(b.pack, basename(b.fbx)), ...piece });
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
