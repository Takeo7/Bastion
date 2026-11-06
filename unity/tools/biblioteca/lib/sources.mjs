// A pack's source files (docs/BIBLIOTECA.md §4): the external disk's SourceFiles zips, or a folder already unzipped in
// assets-src/synty (the 0.10.1's conversion's, read first: no unzipping). What is in them: the models, the textures and
// the MaterialList (each prefab's meshes, their slots and each slot's albedo). A file in a zip is unzipped the first time
// it is asked for, into the cache (assets-src/biblioteca/zip/), and served from there after.
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { existsSync, readdirSync, readFileSync, writeFileSync, mkdirSync, statSync } from 'node:fs';
import { join, basename, dirname, extname, resolve, sep } from 'node:path';

const run = promisify(execFile);

/** Folders never looked in: other engines' copies, collision hulls, the OBJ copies of the FBX. */
const SKIPPED = /^(unreal.*|obj|collision|godot.*|_duplicados|__macosx)$/i;
const MODEL = /\.fbx$/i, TEXTURE = /\.(png|tga|jpe?g)$/i, MATERIALS = /materiallist[^/]*\.txt$/i;

/** A source's name with its version and case taken off: POLYGON_Office_SourceFiles_v4 is POLYGON_Office_SourceFiles. */
const bare = (name) => name.replace(/\.zip$/i, '').replace(/_v\d+(_\d+)*$/i, '').toLowerCase();

/** A texture's name as MaterialList and the files may differ in it: PolygonApocalypse_01_A, PolygonApocalypse_Texture_01_A. */
export const textureKey = (name) => basename(name, extname(name)).toLowerCase().replace(/textures?|tex|materials?|mat/g, '').replace(/[^a-z0-9]/g, '');

/** A name's words, without the ones that say nothing (Texture, Mat, the pack's Polygon… name). */
const words = (name) => basename(name, extname(name)).toLowerCase().split(/[_\-\s.]+/)
  .filter((w) => w && !/^(textures?|tex|materials?|mat|polygon.*)$/.test(w));
const wordFits = (a, b) => a === b || (/^\d+$/.test(a) || /^\d+$/.test(b) ? false : (a.length >= 3 && b.startsWith(a)) || (b.length >= 3 && a.startsWith(b)));

/**
 * When MaterialList names a texture otherwise than its file (Vehicle_01_Mud.png for PolygonApocalypse_Veh_Texture_01_Mud):
 * the file with every word of the name (a word may be cut short: Veh, Vehicle), and the fewest others.
 */
function fuzzy(albedo, textures) {
  const want = words(albedo);
  if (!want.length) return null;
  let best = null, extra = Infinity;
  for (const file of textures.values()) {
    const have = words(file);
    if (!want.every((w) => have.some((h) => wordFits(w, h)))) continue;
    const more = have.length - want.length + (/\/(alts?|misc)\//i.test(file) ? 0.5 : 0);
    if (more < extra) { best = file; extra = more; }
  }
  return best;
}

function walk(dir, under = '', out = []) {
  for (const e of readdirSync(join(dir, under), { withFileTypes: true })) {
    if (e.name.startsWith('.')) continue;
    const rel = under ? `${under}/${e.name}` : e.name;
    if (e.isDirectory()) { if (!SKIPPED.test(e.name)) walk(dir, rel, out); }
    else out.push(rel);
  }
  return out;
}

/** A path in a source, safe to join to its root (no going up). */
function inside(root, rel) {
  const full = resolve(root, rel);
  if (full !== root && !full.startsWith(root + sep)) throw new Error(`Fuera de la carpeta: ${rel}`);
  return full;
}

/** unzip takes entry names as patterns: its wildcards taken literally. */
const literal = (entry) => entry.replace(/[[\]*?\\]/g, (c) => `\\${c}`);

export class PackSources {
  /**
   * `zips`: the pack's SourceFiles zips on the disk (absolute; empty with the disk away). `zipNames`: their names, kept in
   * the inventory, to find the unzipped folders with the disk away too. `local`: assets-src/synty. `cache`: the library's.
   */
  constructor(id, zips, zipNames, local, cache) {
    this.id = id;
    this.roots = [];
    const folders = existsSync(local) ? readdirSync(local) : [];
    for (const name of zipNames) {
      const dir = folders.find((f) => bare(f) === bare(name) && statSync(join(local, f)).isDirectory());
      const zip = zips.find((z) => basename(z) === name);
      if (dir) this.roots.push({ kind: 'dir', dir: join(local, dir), name });
      else if (zip) this.roots.push({ kind: 'zip', zip, name, out: join(cache, 'zip', basename(name, '.zip')) });
    }
    this.cache = cache;
    this.listing = null;
    this.pending = new Map();
  }

  get available() { return this.roots.length > 0; }
  get kind() { return this.roots.length === 0 ? 'none' : this.roots.every((r) => r.kind === 'dir') ? 'dir' : 'zip'; }

  /** Every file of every source, as `<root>/<path>` (the root is the source's name, so a path names its source). */
  async files() {
    if (this.listing) return this.listing;
    const all = [];
    for (const r of this.roots) {
      let rels;
      if (r.kind === 'dir') rels = walk(r.dir);
      else {
        const size = statSync(r.zip).size;
        const saved = join(this.cache, 'listas', `${basename(r.name, '.zip')}.${size}.txt`);
        if (existsSync(saved)) rels = readFileSync(saved, 'utf8').split('\n').filter(Boolean);
        else {
          const { stdout } = await run('unzip', ['-Z1', r.zip], { maxBuffer: 256 << 20 });
          rels = stdout.split('\n').filter((l) => l && !l.endsWith('/'));
          mkdirSync(dirname(saved), { recursive: true });
          writeFileSync(saved, rels.join('\n'));
        }
        rels = rels.filter((p) => !p.split('/').slice(0, -1).some((s) => SKIPPED.test(s)) && !p.startsWith('__MACOSX'));
      }
      for (const p of rels) all.push(`${r.name}/${p}`);
    }
    this.listing = all;
    return all;
  }

  /**
   * The models: one per name (the shortest path, when a pack has the same model twice), and the MaterialList's prefabs
   * with no FBX of their own name (a pack's characters, most often all in one FBX), with no path.
   */
  async models() {
    const byName = new Map();
    for (const p of await this.files()) {
      if (!MODEL.test(p)) continue;
      const name = basename(p, extname(p));
      const had = byName.get(name.toLowerCase());
      if (!had || p.length < had.path.length) byName.set(name.toLowerCase(), { name, path: p });
    }
    const ml = await this.materials();
    for (const [key, prefab] of ml.prefabs) {
      if (byName.has(key)) continue;
      // (A character's prefab is often its FBX under another name: SM_Chr_Zombie_Jock_Male_01 is SK_Zombie_Jock_Male_01.)
      const rest = key.replace(/^s[mk]_(chr_)?/, '');
      const twin = [`sk_${rest}`, `sk_chr_${rest}`, `sm_${rest}`, `sm_chr_${rest}`].map((n) => byName.get(n)).find((m) => m && !m.prefab);
      byName.set(key, { name: ml.names.get(key) ?? key, path: twin?.path ?? null, prefab: true, fbx: twin?.name ?? null });
    }
    return [...byName.values()].sort((a, b) => a.name.localeCompare(b.name));
  }

  /** Textures by textureKey; a pack's main folder before its Alts and Misc. */
  async textures() {
    if (this.textureMap) return this.textureMap;
    const map = new Map();
    const rank = (p) => (/\/(alts?|misc|demo[^/]*)\//i.test(p) ? 1 : 0);
    for (const p of await this.files()) {
      if (!TEXTURE.test(p)) continue;
      const key = textureKey(p);
      const had = map.get(key);
      if (!had || rank(p) < rank(had) || (rank(p) === rank(had) && p.length < had.length)) map.set(key, p);
    }
    this.textureMap = map;
    return map;
  }

  /** The MaterialList: each prefab's meshes and slots, each slot's albedo, and the albedo most slots use (the atlas). */
  async materials() {
    if (this.materialList) return this.materialList;
    const prefabs = new Map(), names = new Map(), slots = new Map(), count = new Map();
    for (const p of (await this.files()).filter((f) => MATERIALS.test(f))) {
      const text = await this.read(p);
      let prefab = null, mesh = null, slot = null;
      for (const line of text.split(/\r?\n/)) {
        let m;
        if ((m = /^\s*Prefab Name:\s*(.+?)\s*$/.exec(line))) { prefab = []; prefabs.set(m[1].toLowerCase(), prefab); names.set(m[1].toLowerCase(), m[1]); mesh = null; }
        else if ((m = /^\s*Mesh Name:\s*(.+?)\s*$/.exec(line)) && prefab) { mesh = { mesh: m[1], slots: [] }; prefab.push(mesh); }
        else if ((m = /^\s*Slot:\s*(.+?)(?:\s*\(([^)]*)\))?\s*$/.exec(line)) && mesh) {
          // (Three ways of writing it: «Slot: X» with an «Albedo:» line after (Apocalypse), «Slot: X (Texture)»
          // (Shops), «Slot: X (Uses custom shader)» with no texture (Palm City): then the slot's own name.)
          slot = { slot: m[1], albedo: null, named: m[2] && !/shader/i.test(m[2]) ? m[2] : null };
          mesh.slots.push(slot);
        }
        else if ((m = /^\s*Albedo:\s*(.+?)\s*\(/.exec(line)) && slot && !slot.albedo) slot.albedo = m[1];
      }
    }
    for (const prefab of prefabs.values()) {
      for (const mesh of prefab) {
        for (const sl of mesh.slots) {
          sl.albedo ??= sl.named ?? sl.slot;
          delete sl.named;
          if (!slots.has(sl.slot.toLowerCase())) slots.set(sl.slot.toLowerCase(), sl.albedo);
          count.set(sl.albedo, (count.get(sl.albedo) ?? 0) + 1);
        }
      }
    }
    const atlas = [...count.entries()].sort((a, b) => b[1] - a[1])[0]?.[0] ?? null;
    this.materialList = { prefabs, names, slots, atlas };
    return this.materialList;
  }

  /**
   * The pack's atlas: the texture most of the MaterialList's slots use; if there is none (no MaterialList, or a map pack
   * painted from another pack's textures), its first texture ending in 01_A or 01, else its first texture.
   */
  async atlasFile() {
    const found = await this.texture((await this.materials()).atlas);
    if (found) return found;
    const textures = [...(await this.textures()).entries()].sort((a, b) => a[1].length - b[1].length || a[1].localeCompare(b[1]));
    return (textures.find(([k]) => /01a$/.test(k)) ?? textures.find(([k]) => /01$/.test(k)) ?? textures[0])?.[1] ?? null;
  }

  /** Each slot's texture by textureKey of the slot's name: for an FBX's material named as the slot (or nearly). */
  async slotFiles() {
    if (this.slotFileMap) return this.slotFileMap;
    const map = {};
    for (const [slot, albedo] of (await this.materials()).slots) {
      const file = await this.texture(albedo);
      if (file) map[textureKey(slot)] = file;
    }
    this.slotFileMap = map;
    return map;
  }

  /** A texture's file for an albedo the MaterialList names; null if the pack has none of that name. */
  async texture(albedo) {
    if (!albedo) return null;
    const textures = await this.textures();
    return textures.get(textureKey(albedo)) ?? fuzzy(albedo, textures);
  }

  /** A file's place on this machine, unzipped first if it is in a zip. */
  async path(file) {
    const slash = file.indexOf('/');
    const r = this.roots.find((x) => x.name === file.slice(0, slash));
    if (!r) throw new Error(`No es de este paquete: ${file}`);
    const rel = file.slice(slash + 1);
    if (r.kind === 'dir') return inside(r.dir, rel);
    const out = inside(r.out, rel);
    if (existsSync(out)) return out;
    if (!this.pending.has(out)) {
      this.pending.set(out, (async () => {
        mkdirSync(r.out, { recursive: true });
        await run('unzip', ['-o', '-q', r.zip, literal(rel), '-d', r.out], { maxBuffer: 16 << 20 });
        if (!existsSync(out)) throw new Error(`No está en el zip: ${rel}`);
      })().finally(() => this.pending.delete(out)));
    }
    await this.pending.get(out);
    return out;
  }

  async read(file) { return readFileSync(await this.path(file), 'utf8'); }
}
