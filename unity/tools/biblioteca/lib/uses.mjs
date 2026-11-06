// What the game uses and where (docs/BIBLIOTECA.md §4, «Se usa en»), from the game's own lists (Client/Resources: MapArt,
// Pieces, Anims, Wardrobe, Undead; the sound's manifest), from the names the C# code writes out, and, once Arte's
// BibliotecaExport has made it, from assets-src/biblioteca/unity/usados.json (what the build carries). Read only.
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join, basename, extname, relative } from 'node:path';

const ROLE_LABELS = {
  motions: 'Movimientos', deadMotions: 'Movimientos de los muertos', idleVariations: 'Esperas', stances: 'Posturas',
  gestures: 'Gestos', deaths: 'Muertes', hits: 'Golpes recibidos', actions: 'Acciones', armedMotions: 'Movimientos con arma',
  rifleRests: 'Reposo con el fusil', feedClips: 'Comer', campLoops: 'Campamento, en bucle', campVariations: 'Campamento, variaciones',
  campMoves: 'Campamento, movimientos', campParts: 'Campamento, partes', campSpans: 'Campamento, tramos', campFree: 'Campamento, sueltos',
  campItems: 'Campamento, objetos',
};
/** The soldiers' classes by their id, as the game names them (Content.cs). */
export const CLASS_NAMES = {
  rookie: 'Recluta', specialist: 'Especialista', assault: 'Asalto', sharpshooter: 'Francotirador', grenadier: 'Granadero',
  trooper: 'Saqueador', officer: 'Cabecilla', lancer: 'Cazador',
};
export const classNames = (ids) => ids.map((u) => CLASS_NAMES[u] ?? u).join(', ');
/** A sound's bus, as its volume in Ajustes → Sonido (Volumes.cs). */
const BUS_NAMES = { sfx: 'Volumen «Efectos»', music: 'Volumen «Música»', ambience: 'Volumen «Ambiente»', ui: 'Volumen «Botones y avisos»', radio: 'Volumen «Botones y avisos»' };
/** A wardrobe piece's kind, as Personalizar says it. */
export const PIECE_NAMES = { outfit: 'Ropa', hair: 'Pelo', beard: 'Barba', face: 'Cara', headwear: 'Gorro o casco' };

/** «masc» and «femn», a part of a role's path in Anims.json, in Spanish. */
export const sexOf = (part) => ({ masc: 'hombre', femn: 'mujer' }[part] ?? part);

/** The labs and probes name models to try them, not for the game. */
const NOT_GAME = /(Lab|Probe|Shots|ShotSetups)\.cs$/;

const readJson = (file) => JSON.parse(readFileSync(file, 'utf8'));
const csFiles = (dir, out = []) => {
  if (!existsSync(dir)) return out;
  for (const e of readdirSync(dir, { withFileTypes: true })) {
    const p = join(dir, e.name);
    if (e.isDirectory()) csFiles(p, out);
    else if (e.name.endsWith('.cs') && !NOT_GAME.test(e.name)) out.push(p);
  }
  return out;
};

export class Uses {
  /**
   * `config`: packs.json. `imported(packId)`: whether its folder is in Assets/Synty. `namesOf(packId)`: its models' names
   * (lower case), for what the game asks for by name alone.
   */
  constructor(root, config, imported, namesOf) {
    this.root = root;
    this.config = config;
    this.imported = imported;
    this.namesOf = namesOf;
    this.game = join(root, 'unity/Bastion/Assets/Bastion');
    this.resources = join(this.game, 'Client/Resources');
    this.keyToPack = new Map();
    for (const [id, p] of Object.entries(config.packs)) for (const k of p.keys) this.keyToPack.set(k, id);
  }

  /** Reads everything again (the page's «Volver a leer»). */
  async load() {
    this.byModel = new Map();   // `${packId}|${name}` → uses
    this.display = new Map();   // the same keys → the name as the game writes it
    this.byName = [];           // asked by name alone: { name, use }, placed once the packs' names are known
    this.parts = new Map();     // Sidekick part → uses
    this.mixamo = new Map();    // Mixamo clip (lower case) → uses
    this.notes = [];
    this.code = this.scanCode();
    this.readCounts();
    this.readMapArt();
    this.readPieces();
    this.readAnims();
    this.readWardrobe();
    this.readCode();
    this.readBuild();
    this.sounds = this.readSounds();
    await this.placeByName();
    return this;
  }

  add(packId, name, use) {
    const key = `${packId}|${name.toLowerCase()}`;
    if (!this.byModel.has(key)) this.byModel.set(key, []);
    if (!this.display.has(key) || name !== name.toLowerCase()) this.display.set(key, name);
    const list = this.byModel.get(key);
    if (!list.some((u) => u.title === use.title && u.detail === use.detail)) list.push(use);
  }

  /** A model the lists name with the game's pack key (`apocalypse`), or by name alone when they give none. */
  addKeyed(key, name, use) {
    const packId = this.keyToPack.get(key);
    if (packId) this.add(packId, name, { ...use, how: 'pack' });
    else this.byName.push({ name, use: { ...use, how: 'name' } });
  }

  /** Every string the game's C# writes out, with the files it is in. */
  scanCode() {
    const literals = new Map();
    for (const file of csFiles(this.game)) {
      const rel = relative(this.game, file);
      for (const m of readFileSync(file, 'utf8').matchAll(/"([A-Za-z][A-Za-z0-9_.\- ]{2,80})"/g)) {
        if (!literals.has(m[1])) literals.set(m[1], new Set());
        literals.get(m[1]).add(rel);
      }
    }
    return literals;
  }

  codeFiles(literal) {
    const files = this.code.get(literal);
    return files ? [...files].map((f) => basename(f)).sort() : [];
  }

  /**
   * Gameplay's counts (unity/tools/biblioteca/cuentas.json, engine-cs/tools/ContarMapas): how often each map object and
   * list comes out, by map; what Unity places by another rule (sinContar); and the lists and objects nothing draws
   * (sinNombrar: the 0.10.1's spare cover lists, cityLow and the rest).
   */
  readCounts() {
    const file = join(this.root, 'unity/tools/biblioteca/cuentas.json');
    this.counts = null;
    this.deadLists = new Set();
    this.deadObjects = new Set();
    if (!existsSync(file)) return;
    try {
      this.counts = readJson(file);
      this.deadLists = new Set(this.counts.sinNombrar?.listas ?? []);
      this.deadObjects = new Set(this.counts.sinNombrar?.objetos ?? []);
    } catch (e) {
      this.notes.push(`cuentas.json no se puede leer: ${e.message}`);
    }
  }

  /**
   * A model's uses split: the live ones, and the ones of objects nothing draws (`dead`). With only dead ones, «Lo lleva
   * la app» is not a use either: it goes in the app, but the game never shows it.
   */
  live(list) {
    const dead = list.filter((u) => u.dead);
    let uses = list.filter((u) => !u.dead);
    if (dead.length && uses.every((u) => u.kind === 'build')) uses = [];
    return { uses, dead };
  }

  readMapArt() {
    const art = readJson(join(this.resources, 'MapArt.json'));
    const setsOf = new Map();
    for (const [set, ids] of Object.entries(art.sets ?? {})) {
      for (const id of Array.isArray(ids) ? ids : Object.values(ids).flat()) {
        if (typeof id !== 'string') continue;
        if (!setsOf.has(id)) setsOf.set(id, []);
        setsOf.get(id).push(set);
      }
    }
    for (const [id, p] of Object.entries(art.props ?? {})) {
      const name = basename(p.cut?.source ?? p.fbx);
      const sets = setsOf.get(id) ?? [];
      const parts = [];
      if (sets.length) parts.push(`${sets.length === 1 ? 'En la lista' : 'En las listas'} ${sets.map((s) => {
        const files = this.codeFiles(s);
        if (this.deadLists.has(s)) return `«${s}» (sin usar)`;
        return files.length ? `«${s}» (${files.join(', ')})` : `«${s}»`;
      }).join(', ')}`);
      const files = this.codeFiles(id);
      if (files.length) parts.push(`El código lo pide por su nombre (${files.join(', ')})`);
      if (p.cut) parts.push('Recortado con Blender');
      const dead = this.deadObjects.has(id);
      if (dead) parts.unshift('Nunca sale: ningún código lo pide, ni pide sus listas');
      this.addKeyed(p.pack, name, { kind: 'prop', title: `Objeto del mapa «${id}»`, detail: parts.join('. ') || null, dead,
        refs: [{ clase: 'objeto', id }, ...sets.filter((s) => !this.deadLists.has(s)).map((s) => ({ clase: 'lista', id: s }))] });
    }
  }

  readPieces() {
    const pieces = readJson(join(this.resources, 'Pieces.json')).pieces ?? {};
    for (const [id, p] of Object.entries(pieces)) {
      if (!p.fbx) continue;
      this.addKeyed(p.pack, basename(p.fbx), { kind: 'piece', title: `Pieza de edificio «${id}»`, detail: 'Recortada con Blender', refs: [{ clase: 'pieza', id }] });
    }
  }

  readAnims() {
    const anims = readJson(join(this.resources, 'Anims.json'));
    const roles = new Map();
    const walk = (value, path, table) => {
      if (typeof value === 'string') {
        if (anims.clips[value]) {
          if (!roles.has(value)) roles.set(value, new Set());
          roles.get(value).add([ROLE_LABELS[table], ...path.map(sexOf)].join(' · '));
        }
      } else if (Array.isArray(value)) value.forEach((v) => walk(v, path, table));
      else if (value && typeof value === 'object') {
        for (const [k, v] of Object.entries(value)) {
          if (anims.clips[k]) walk(k, path, table);
          walk(v, [...path, k], table);
        }
      }
    };
    for (const table of Object.keys(ROLE_LABELS)) if (anims[table]) walk(anims[table], [], table);
    this.clips = anims.clips;
    this.roles = roles;
    for (const [id, c] of Object.entries(anims.clips)) {
      const detail = roles.has(id) ? [...roles.get(id)].sort().join('; ') : 'Sin papel en Anims.json: lo pide el código';
      const use = { kind: 'clip', title: `Animación «${id}»`, detail, refs: [{ clase: 'clip', id }] };
      if (c.pack === 'mixamo') {
        const key = c.fbx.toLowerCase();
        if (!this.mixamo.has(key)) this.mixamo.set(key, []);
        this.mixamo.get(key).push(use);
      } else this.addKeyed(c.pack, basename(c.fbx), use);
    }
    const carriers = new Map();
    for (const [unit, ids] of Object.entries(anims.unitWeapons ?? {})) {
      for (const id of new Set(ids)) {
        if (!carriers.has(id)) carriers.set(id, []);
        carriers.get(id).push(unit);
      }
    }
    for (const [id, w] of Object.entries(anims.weapons ?? {})) {
      if (!w.fbx) continue;
      const who = carriers.get(id);
      const use = { kind: 'weapon', title: `Arma «${id}»`, detail: who ? `La llevan: ${classNames(who)}` : null, refs: [{ clase: 'arma', id }] };
      // (Named `pack:model` when several packs have one of that name: the pan, the katana; else by the name alone.)
      const colon = w.fbx.indexOf(':');
      if (colon > 0) this.addKeyed(w.fbx.slice(0, colon), basename(w.fbx.slice(colon + 1)), use);
      else this.byName.push({ name: basename(w.fbx), use: { ...use, how: 'name' } });
    }
  }

  readWardrobe() {
    // (The outfits' names in Personalizar: People.OutfitNames, engine-cs's Lore.)
    this.outfits = new Map();
    const people = join(this.root, 'engine-cs/package/Runtime/Lore/People.cs');
    if (existsSync(people)) {
      const block = /OutfitNames[^{]*\{([\s\S]*?)\};/.exec(readFileSync(people, 'utf8'))?.[1] ?? '';
      for (const m of block.matchAll(/\["(\w+)"\]\s*=\s*"([^"]+)"/g)) this.outfits.set(m[1], m[2]);
    }
    // (And the heads' names in Personalizar, «Cabeza»: Appearances.Heads.)
    this.heads = new Map();
    const looks = join(this.root, 'engine-cs/package/Runtime/Appearances.cs');
    if (existsSync(looks)) {
      for (const m of readFileSync(looks, 'utf8').matchAll(/\[HeadIds\.(\w+)\][^}]*?Name = "([^"]+)"/g)) this.heads.set(m[1][0].toLowerCase() + m[1].slice(1), m[2]);
    }
    const add = (part, use) => {
      if (!this.parts.has(part)) this.parts.set(part, []);
      this.parts.get(part).push(use);
    };
    for (const w of readJson(join(this.resources, 'Wardrobe.json')).pieces ?? []) {
      const outfit = (w.piece === 'outfit' && this.outfits.get(w.id)) || (w.piece === 'headwear' && this.heads.get(w.id));
      for (const part of w.parts ?? []) {
        add(part, { kind: 'wardrobe', title: `${PIECE_NAMES[w.piece] ?? w.piece} de los soldados: «${w.id}»`,
          detail: outfit ? `En Personalizar: «${outfit}»` : null, refs: [{ clase: 'armario', id: w.id }] });
      }
    }
    const undead = join(this.resources, 'Undead.json');
    if (existsSync(undead)) {
      for (const [id, u] of Object.entries(readJson(undead))) {
        for (const [part] of u.parts ?? []) add(part, { kind: 'undead', title: `Zombi hecho de un personaje: «${id}»`, detail: null, refs: [{ clase: 'muerto', id }] });
      }
    }
  }

  /** Models the code names (the animals, the special dead, La Lata…), found by name alone once the packs' names are known. */
  readCode() {
    for (const [literal, files] of this.code) {
      if (!/^(SM|SK|SA|FX|PFB|PolygonParticles|SimpleCats|Stag|Doe|Boar|Rabbit)_/.test(literal) || literal.endsWith('_')) continue;
      const names = [...files].map((f) => basename(f)).sort();
      this.byName.push({ name: literal, use: { kind: 'code', how: 'name', title: 'Lo nombra el código', detail: names.join(', '), quiet: true } });
    }
    // (Names the code puts together from a call's argument: packs.json's `composed`.)
    for (const c of this.config.composed?.calls ?? []) {
      const file = join(this.game, c.file);
      if (!existsSync(file)) continue;
      for (const m of readFileSync(file, 'utf8').matchAll(new RegExp(`\\b${c.call}\\("([A-Za-z0-9_]+)"`, 'g'))) {
        this.byName.push({ name: `${c.prefix}${m[1]}${c.suffix}`, use: {
          kind: 'code', how: 'name', title: 'Lo nombra el código', detail: basename(c.file), quiet: true,
        } });
      }
    }
  }

  /**
   * What the build carries, from Arte's export (unity/tools/biblioteca/usados.json, BibliotecaExport): every asset of
   * Synty's and Mixamo's the build's scenes, catalogs and Resources depend on, with its pack on the disk and what pulls
   * it in; and Sidekick's parts, presets and the game's own characters made of them, with their pictures.
   */
  readBuild() {
    const file = join(this.root, 'unity/tools/biblioteca/usados.json');
    this.build = null;
    this.sidekick = null;
    if (!existsSync(file)) return;
    try {
      const data = readJson(file);
      const roots = data.raices ?? [];
      this.build = { made: data.hecho ?? null, version: data.version ?? null, count: data.usados?.length ?? 0 };
      const whence = (from) => {
        const said = [...new Set((from ?? []).map((f) => {
          const root = roots[f.raiz]?.ruta;
          return [root ? basename(root, extname(root)) : null, f.lista, f.en].filter(Boolean).join(' · ');
        }))];
        return said.length > 3 ? `${said.slice(0, 3).join('; ')}; y ${said.length - 3} más` : said.join('; ') || null;
      };
      this.uiImages = [];
      for (const a of data.usados ?? []) {
        // (Of the INTERFACE packs, their images: the HUD's icons.)
        if (a.paquete?.startsWith('INTERFACE') && ['textura', 'sprite'].includes(a.tipo)) {
          const use = { kind: 'build', title: 'Lo lleva la app', detail: whence(a.desde), refs: [{ clase: 'imagen', id: a.modelo }] };
          this.add(a.paquete, a.modelo, { ...use, how: 'pack' });
          this.uiImages.push({ name: a.modelo, pack: a.paquete, detail: use.detail });
          continue;
        }
        if (!['modelo', 'prefab', 'clip'].includes(a.tipo) || !a.paquete || !a.modelo || a.paquete.startsWith('SIDEKICK')) continue;
        // (Ours, cut by Blender from a pack's FBX: the lists already give that FBX its use.)
        // (Nor the collision hulls a prefab brings along: nobody chooses them.)
        if (/^Assets\/Synty\/(Pieces|Extra)\//.test(a.ruta ?? '') || /_collision$/i.test(a.modelo)) continue;
        // (Nor a Unity FBX that only holds characters whose prefabs come in on their own: City Zombies' Zombies.fbx,
        // brought by «characters: SM_Chr_Zombie_…», whose sources have each one apart, as SK_Zombie_….fbx.)
        const held = (a.desde ?? []).map((f) => /^characters: (.+)$/.exec(f.en ?? '')?.[1]);
        if (a.tipo === 'modelo' && held.length && held.every(Boolean) && !held.includes(a.modelo)) continue;
        const use = { kind: 'build', title: 'Lo lleva la app', detail: whence(a.desde) };
        if (a.paquete === 'Mixamo') {
          // (In Unity, Mixamo's clips go by the game's id: rifleIdle.fbx is assets-src/mixamo's «Rifle Idle».)
          const key = (this.clips?.[a.modelo]?.fbx ?? a.modelo).toLowerCase();
          if (!this.mixamo.has(key)) this.mixamo.set(key, []);
          this.mixamo.get(key).push(use);
        } else this.add(a.paquete, a.modelo, { ...use, how: 'pack' });
      }
      if (data.sidekick) this.sidekick = data.sidekick;
    } catch (e) {
      this.notes.push(`usados.json no se puede leer: ${e.message}`);
    }
  }

  /**
   * What is asked by name alone goes to the pack Unity would give (MapLab.Prefabs): of the imported packs that have it,
   * the last by folder name, and the late packs (Shops, Gang Warfare, Palm City) after the rest.
   */
  async placeByName() {
    const order = Object.entries(this.config.packs)
      .filter(([id, p]) => this.imported(id) && !p.parts)
      .sort(([, a], [, b]) => (a.late ? 1 : 0) - (b.late ? 1 : 0) || b.unity.localeCompare(a.unity))
      .map(([id]) => id);
    const names = new Map();
    for (const id of order) names.set(id, await this.namesOf(id));
    // (A prefab named as its FBX less the SM_ or SK_: SIMPLE - Forest Animals' Stag_01 is SM_Stag_01.fbx.)
    const spelled = (name) => [name, `SM_${name}`, `SK_${name}`].map((n) => n.toLowerCase());
    const byPrefix = order.filter((id) => this.config.packs[id].prefabs).map((id) => [id, new RegExp(this.config.packs[id].prefabs)]);
    this.unplaced = [];
    for (const { name, use } of this.byName) {
      let id = null, as = name;
      for (const n of spelled(name)) {
        id = order.find((p) => names.get(p)?.has(n));
        if (id) { as = n; break; }
      }
      // (Else a Unity prefab of a pack known by the start of its name, with no FBX of that name: it shows as such.)
      if (!id) id = byPrefix.find(([, re]) => re.test(name))?.[0] ?? null;
      if (id) this.add(id, as === name ? name : as, use);
      else if (!use.quiet) this.unplaced.push({ name, use });
    }
  }

  forModel(packId, name) { return this.byModel.get(`${packId}|${name.toLowerCase()}`) ?? []; }

  /** The uses the lists give a pack, with the model names they ask for (to tell what its sources lack); only live ones. */
  ofPack(packId) {
    const out = [];
    for (const [key, list] of this.byModel) {
      const bar = key.indexOf('|');
      if (key.slice(0, bar) !== packId) continue;
      const { uses, dead } = this.live(list);
      if (uses.length) out.push({ name: key.slice(bar + 1), shown: this.display.get(key), uses, dead });
    }
    return out;
  }

  /** The sounds, by group and event, with where the code names each event. */
  readSounds() {
    const dir = join(this.game, 'Client/Audio/Resources/Generado');
    const file = join(dir, 'manifiesto.json');
    if (!existsSync(file)) return { present: false, groups: [] };
    const m = readJson(file);
    const ev = (event, files, extra) => ({ event, files, extra: extra ?? null, code: this.codeFiles(event) });
    const groups = [
      { id: 'sintetizados', title: 'Sintetizados', events: Object.entries(m.sintetizados ?? {}).map(([e, s]) => ev(e, s.files ?? [], BUS_NAMES[s.bus] ?? s.bus)) },
      { id: 'grabados', title: 'Grabados', events: Object.entries(m.grabados ?? {}).map(([e, s]) => ev(e, s.map((x) => x.file))) },
      { id: 'vivos', title: 'Música y ambiente', events: Object.entries(m.vivos ?? {}).map(([e, s]) => ev(e, [s.file], s.seconds ? `${Math.round(s.seconds / 60)} min` : null)) },
      { id: 'interfaz', title: 'Interfaz', events: (m.interfaz ?? []).map((f) => ev(basename(f, extname(f)), [f])) },
    ];
    return { present: true, dir, origin: m.origen ?? null, groups };
  }
}
