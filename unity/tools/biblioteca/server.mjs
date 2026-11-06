// The resource library (docs/BIBLIOTECA.md): a page over every Synty pack on the external disk, the Mixamo clips and the
// sounds, telling what the game uses and where, and where Arturo proposes what to use (seleccion.json, for Gameplay to
// review). Only on 127.0.0.1:3420 (Synty never leaves this Mac). Writes nothing but seleccion.json and its cache,
// assets-src/biblioteca/ (unzipped sources, listings, thumbnails); never Assets/ nor the game's lists.
//   node unity/tools/biblioteca/server.mjs        (SYNTY=… for another place of the disk's packs)
import { createServer } from 'node:http';
import { existsSync, readFileSync, readdirSync, writeFileSync, mkdirSync, statSync, createReadStream } from 'node:fs';
import { join, dirname, extname, resolve, sep, basename } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readInventory } from './lib/inventory.mjs';
import { PackSources } from './lib/sources.mjs';
import { Uses } from './lib/uses.mjs';
import { Selection } from './lib/selection.mjs';
import { Targets, coverOf } from './lib/targets.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(here, '../../..');
const HOST = '127.0.0.1', PORT = 3420;
const SYNTY = process.env.SYNTY ?? '/Volumes/4T_White/50_INBOX/Synty';
const LOCAL = join(ROOT, 'assets-src/synty');
const MIXAMO = join(ROOT, 'assets-src/mixamo');
const CACHE = join(ROOT, 'assets-src/biblioteca');
const UNITY_SYNTY = join(ROOT, 'unity/Bastion/Assets/Synty');
/** The thumbnails' version: raised when the page draws them otherwise, so the old ones are not shown. */
const THUMBS = join(CACHE, 'miniaturas-2');
const config = JSON.parse(readFileSync(join(here, 'packs.json'), 'utf8'));

const TYPES = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8', '.json': 'application/json; charset=utf-8', '.png': 'image/png', '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg', '.tga': 'application/octet-stream', '.fbx': 'application/octet-stream', '.wav': 'audio/wav',
  '.svg': 'image/svg+xml', '.txt': 'text/plain; charset=utf-8',
};

let inventory, sources, uses, targets, usedTextures;
// (BIBLIOTECA_SELECCION: another file, to try the page without touching Arturo's.)
const selection = new Selection(process.env.BIBLIOTECA_SELECCION ?? join(here, 'seleccion.json'));
const SNAPSHOTS = join(ROOT, '.dev-snapshots');

function offStyle(pack) {
  return config.offStyle.lines.includes(pack.line) && !config.offStyle.allowed.includes(pack.id);
}
function imported(id) {
  const p = config.packs[id];
  return !!p && existsSync(join(UNITY_SYNTY, p.unity));
}
function kindOf(pack) {
  return { ANIMATION: 'anims', SIDEKICK: 'sidekick', INTERFACE: 'interface' }[pack.line] ?? 'models';
}
function sourcesOf(id) {
  if (!sources.has(id)) {
    const pack = inventory.packs.find((p) => p.id === id);
    if (!pack) return null;
    const sprites = pack.spriteZips ?? [];
    const zips = inventory.mounted ? [...pack.sourceZips.map((z) => join(SYNTY, id, 'SourceFiles', z)), ...sprites.map((z) => join(SYNTY, id, 'Extras', z))] : [];
    sources.set(id, new PackSources(id, zips, [...pack.sourceZips, ...sprites], LOCAL, CACHE));
  }
  return sources.get(id);
}
async function namesOf(id) {
  const s = sourcesOf(id);
  if (!s?.available) return new Set();
  return new Set((await s.models()).map((m) => m.name.toLowerCase()));
}

/** A Sidekick slot (Synty's name) in Spanish, the left and right ones together. */
const SLOTS = {
  Head: 'Cabeza', Hair: 'Pelo', Eyebrow: 'Cejas', Eye: 'Ojos', Ear: 'Orejas', FacialHair: 'Barba', Nose: 'Nariz', Teeth: 'Dientes',
  Tongue: 'Lengua', Torso: 'Torso', ArmUpper: 'Brazos', ArmLower: 'Antebrazos', Hand: 'Manos', Hips: 'Cadera', Leg: 'Piernas',
  Foot: 'Pies', AttachmentHead: 'Cascos y gorros', AttachmentFace: 'En la cara', AttachmentBack: 'En la espalda',
  AttachmentHipsFront: 'En la cadera', AttachmentHipsBack: 'En la cadera', AttachmentHips: 'En la cadera',
  AttachmentShoulder: 'Hombreras', AttachmentElbow: 'Coderas', AttachmentKnee: 'Rodilleras', Wrap: 'Vendas',
};
const slotOf = (hueco) => (hueco ? SLOTS[hueco.replace(/(Left|Right)$/, '')] ?? hueco : 'Piezas');

/**
 * A Sidekick pack's characters and parts, from Arte's usados.json (with their pictures); without it, the parts' names
 * from the folder unpacked in assets-src/synty (SIDEKICK_<Pack>_Extracted), with what the lists say of them.
 */
function sidekickDetail(pack) {
  const sk = uses.sidekick;
  if (sk) {
    const drawn = new Set(sk.presets.filter((p) => p.juego).map((p) => p.nombre));
    const parts = sk.piezas.filter((p) => p.paquete === pack.id).map((p) => {
      const list = [...(uses.parts.get(p.parte) ?? [])];
      const by = p.presets.filter((n) => drawn.has(n));
      if (by.length) list.push({ kind: 'preset', title: by.length === 1 ? `En el personaje «${by[0]}»` : `En los personajes ${by.map((n) => `«${n}»`).join(', ')}`, detail: 'Un personaje de Sidekick que dibuja el juego' });
      if (p.app && !list.length) list.push({ kind: 'build', title: 'Lo lleva la app', detail: null });
      return { name: p.parte, group: slotOf(p.hueco), image: p.imagen, uses: list };
    });
    const people = sk.presets.filter((p) => p.paquete === pack.id).map((p) => ({
      name: p.nombre, group: 'Personajes', image: p.imagen ?? null, parts: p.partes?.length ?? 0,
      uses: p.juego ? [{ kind: 'preset', title: 'Lo dibuja el juego', detail: null }] : [],
    }));
    for (const h of sk.hechos.filter((x) => x.paquete === pack.id)) {
      people.push({ name: h.nombre, group: 'Zombis hechos de personajes', image: h.imagen ?? null,
        uses: [{ kind: 'undead', title: 'Zombi hecho de un personaje', detail: `Del personaje «${h.preset}»${h.mujer ? ', con cuerpo de mujer' : ''}` }] });
    }
    return { parts, people, pictures: true };
  }
  const prefixes = config.packs[pack.id]?.parts ?? [];
  const names = new Set();
  const dir = join(LOCAL, `SIDEKICK_${pack.title.replace(/[^A-Za-z]/g, '')}_Extracted`);
  const walk = (d) => {
    for (const e of readdirSafe(d)) {
      const p = join(d, e.name);
      if (e.isDirectory()) walk(p);
      else if (/^SK_[A-Z]+_[A-Z]+_\d+_/.test(e.name)) names.add(e.name.replace(/\.[^.]+$/, ''));
    }
  };
  if (existsSync(dir)) walk(dir);
  for (const part of uses.parts.keys()) names.add(part);
  const parts = [...names].filter((n) => prefixes.some((x) => n.startsWith(`SK_${x}_`))).sort()
    .map((name) => ({ name, group: 'Piezas', image: null, uses: uses.parts.get(name) ?? [] }));
  return { parts, people: [], pictures: false };
}
function readdirSafe(d) { try { return readdirSync(d, { withFileTypes: true }); } catch { return []; } }

/** An INTERFACE pack's folder as a group, in Spanish (Lore, 5/11); the platforms and the folders named as the pack, as they are. */
const UI_FOLDERS = {
  icons_weapons: 'Iconos de armas', icons_weaponmods: 'Iconos de mejoras de armas', icons_ammo: 'Iconos de munición',
  icons_armor: 'Iconos de armadura', icons_explosives: 'Iconos de explosivos', icons_resources: 'Iconos de recursos',
  icons_inventory: 'Iconos del inventario', icons_map: 'Iconos del mapa', icons_status: 'Iconos de estado',
  icons_stats: 'Iconos de estadísticas', icons_special: 'Iconos especiales', icons_social: 'Iconos sociales',
  icons_menu: 'Iconos de menú', icons_settings: 'Iconos de ajustes', icons_factions: 'Iconos de facciones',
  icons_scifimenus: 'Iconos de Sci-Fi Menus', icons_input: 'Controles', mousekeyboard: 'Ratón y teclado',
  gamepadgeneric: 'Mando genérico', badges: 'Insignias', crests: 'Escudos', cursors: 'Cursores', fonts: 'Tipografías',
  fx: 'Efectos', greebles: 'Adornos', samples: 'Ejemplos', branding: 'Logotipos',
};
const uiFolder = (d) => UI_FOLDERS[d.toLowerCase()] ?? d;

/** An INTERFACE pack's images (PNG and JPG of its sources and its Extras' sprites), by folder, with what the game uses. */
async function interfaceDetail(pack) {
  const s = sourcesOf(pack.id);
  if (!s?.available) return { images: [] };
  const seen = new Set();
  const images = [];
  for (const f of await s.files()) {
    if (!/\.(png|jpe?g)$/i.test(f) || /(^|\/)(demo|_?scenes?)\//i.test(f)) continue;
    const name = basename(f, extname(f));
    if (seen.has(name.toLowerCase())) continue;
    seen.add(name.toLowerCase());
    const dirs = f.split('/').slice(1, -1).filter((d) => !/^(source_?sprites|sourcefiles|sprites|core)$/i.test(d));
    images.push({ name, path: f, group: dirs.slice(-2).map(uiFolder).join(' · ') || 'Imágenes', uses: uses.forModel(pack.id, name) });
  }
  // (Unity's package names some otherwise than the sprites' zip: ICON_Apocalpyse_Inventory_Backpack_01 is ICON_Inventory_Backpack.)
  const norm = (n) => n.toLowerCase().replace(/apoc(alpyse|alypse)_?/g, '').replace(/_0*1$/, '');
  const byNorm = new Map(images.map((i) => [norm(i.name), i]));
  for (const u of uses.ofPack(pack.id)) {
    if (seen.has(u.name)) continue;
    const hit = byNorm.get(norm(u.shown ?? u.name));
    if (hit && !hit.uses.length) { hit.uses = u.uses; hit.unity = u.shown; }
    else if (!hit) images.push({ name: u.shown, path: null, group: 'Sin imagen con su nombre', uses: u.uses, missing: true });
  }
  return { images };
}

/** A model's group, from the second part of its name (SM_Prop_…, SM_Bld_…); an animation's, from its folder. */
const GROUPS = {
  bld: 'Edificios', prop: 'Objetos', veh: 'Vehículos', env: 'Entorno', wep: 'Armas', chr: 'Personajes', item: 'Objetos de mano',
  sign: 'Carteles', generic: 'Genéricos', fx: 'Efectos', icon: 'Iconos', wall: 'Muros', door: 'Puertas', tree: 'Árboles',
  rock: 'Rocas', plant: 'Plantas', bush: 'Plantas', grass: 'Plantas', flower: 'Plantas', road: 'Calles', floor: 'Suelos',
  generric: 'Genéricos', gernric: 'Genéricos', rubble: 'Escombros', flame: 'Fuego', deadbody: 'Cadáveres', zombie: 'Zombis', gib: 'Restos',
  character: 'Personajes', characters: 'Personajes', animations: 'Animaciones', cloud: 'Nubes', skydome: 'Cielo',
  gen: 'Genéricos', gerneric: 'Genéricos', props: 'Objetos', char: 'Personajes', particle: 'Efectos', gorechunk: 'Restos',
  lightray: 'Rayos de luz', lightraycube: 'Rayos de luz', lightrayround: 'Rayos de luz', sunshafts: 'Rayos de luz',
  dog: 'Perros', simplecats: 'Gatos', bear: 'Animales', boar: 'Animales', deer: 'Animales', doe: 'Animales', fox: 'Animales',
  moose: 'Animales', rabbit: 'Animales', raccoon: 'Animales', skunk: 'Animales', stag: 'Animales', wolf: 'Animales',
};
function groupOf(model, kind) {
  if (kind === 'anims') {
    const parts = model.path.split('/');
    return parts.length > 2 ? parts[parts.length - 2] : 'Animaciones';
  }
  const bits = model.name.split('_');
  // (Cut at the first space too: «LightRayCube 1» is LightRayCube's.)
  const token = (/^(sm|sk|sa|pfb)$/i.test(bits[0]) ? bits[1] : bits[0])?.split(' ')[0].toLowerCase() ?? '';
  return GROUPS[token] ?? (token ? token[0].toUpperCase() + token.slice(1) : 'Otros');
}

async function loadAll() {
  inventory = readInventory(SYNTY, CACHE);
  sources = new Map();
  uses = await new Uses(ROOT, config, imported, namesOf).load();
  targets = new Targets(ROOT, uses);
  usedTextures = new Map();
}

function packSummary(pack) {
  const p = config.packs[pack.id];
  const kind = kindOf(pack);
  let inUse;
  if (kind === 'sidekick') {
    const sk = sidekickDetail(pack);
    inUse = sk.parts.filter((x) => x.uses.length).length + sk.people.filter((x) => x.uses.length).length;
  } else inUse = uses.ofPack(pack.id).length;
  // (An INTERFACE pack's in use are its images the build carries, from usados.json: uses.ofPack has them too.)
  const s = sourcesOf(pack.id);
  return {
    ...pack,
    kind,
    offStyle: offStyle(pack),
    imported: imported(pack.id),
    unityFolder: p?.unity ?? null,
    partial: p?.partial ?? null,
    sources: s?.kind ?? 'none',
    inUse,
  };
}

async function packDetail(id) {
  const pack = inventory.packs.find((p) => p.id === id);
  if (!pack) return null;
  const summary = packSummary(pack);
  if (summary.kind === 'sidekick') return { ...summary, ...sidekickDetail(pack), models: [], missing: 0 };
  if (summary.kind === 'interface') return { ...summary, ...(await interfaceDetail(pack)), models: [], missing: 0 };
  const s = sourcesOf(id);
  const models = s?.available ? await s.models() : [];
  const have = new Set(models.map((m) => m.name.toLowerCase()));
  const listed = models.map((m) => ({ ...m, group: groupOf(m, summary.kind), ...uses.live(uses.forModel(id, m.name)) }));
  // (A models pack's own clips, FBX under Animations/: the dogs'. They play on the pack's mannequin.)
  if (summary.kind === 'models') {
    for (const m of listed) if (m.path && /(^|\/)animations?\//i.test(m.path)) { m.clip = true; m.group = 'Animaciones'; }
  }
  // (What the game asks this pack for and its sources lack: a Unity prefab of another name, or a pack with no sources here.)
  const missing = uses.ofPack(id).filter((u) => !have.has(u.name));
  for (const m of missing) {
    listed.push({ name: m.shown, path: null, missing: true, group: 'Sin FBX con su nombre', uses: m.uses });
  }
  // (Characters all in one FBX, the pack's `container`: each prefab drawn from it, with its own meshes alone.)
  const holder = await containerOf(id);
  if (holder) {
    for (const m of listed) {
      if (m.path || !(m.prefab || m.missing)) continue;
      m.path = holder;
      m.contained = true;
      m.thumbKey = `${holder}@${m.name}`;
      m.missing = false;
      m.group = groupOf(m, summary.kind);
    }
    const own = listed.find((m) => m.path === holder && !m.contained);
    const kin = listed.find((m) => m.contained);
    if (own && kin) own.group = kin.group;
  }
  let atlas = null;
  if (s?.available && summary.kind === 'models') atlas = { albedo: (await s.materials()).atlas, file: await s.atlasFile() };
  return { ...summary, models: listed, missing: listed.filter((m) => m.missing).length, container: holder ? basename(holder) : null, atlas };
}

/**
 * The mannequin a clip of an ANIMATION pack was made on: its pack's PolygonSyntyCharacter or SidekickSyntyCharacter
 * (Sidekick's for a clip under Sidekick/ or named A_MOD_…), else Base Locomotion's or Goblin Locomotion's; null if none.
 */
async function mannequinFor(id, clipPath) {
  const own = config.packs[id]?.container;
  if (own?.mannequin) {
    const holder = await containerOf(id);
    if (holder) return { pack: id, path: holder, only: [own.mannequin] };
  }
  const want = /sidekick|(^|\/)A_MOD_/i.test(clipPath ?? '') ? 'sidekicksyntycharacter' : 'polygonsyntycharacter';
  for (const pid of [id, 'ANIMATION - Base Locomotion', 'ANIMATION - Goblin Locomotion']) {
    const s = sourcesOf(pid);
    if (!s?.available) continue;
    const files = (await s.files()).filter((f) => /(^|\/)(polygon|sidekick)syntycharacter\.fbx$/i.test(f));
    const f = files.find((x) => x.toLowerCase().endsWith(`${want}.fbx`)) ?? files[0];
    if (f) return { pack: pid, path: f };
  }
  return null;
}

/**
 * «En los mapas» of a model's map objects, from Gameplay's cuentas.json: for each object, the maps it comes out in,
 * most first; «sin contar» if Unity places it by another rule; nothing drawn if no code asks for it.
 */
function countsFor(ids) {
  const c = uses.counts;
  if (!c) return { present: false, objetos: [] };
  const maps = c.mapas ?? {};
  return {
    present: true, semillas: c.semillas ?? null, version: c.version ?? null, hecho: c.hecho ?? null, tipos: Object.keys(maps).length,
    objetos: ids.map((id) => {
      if (uses.deadObjects.has(id)) return { id, estado: 'muerto' };
      if ((c.sinContar ?? []).includes(id)) return { id, estado: 'sinContar' };
      const per = Object.entries(c.objetos?.[id] ?? {}).map(([k, v]) => ({ clave: k, nombre: maps[k]?.nombre ?? k, ...v }))
        .sort((a, b) => b.media - a.media);
      return { id, estado: per.length ? 'contado' : 'nunca', mapas: per };
    }),
  };
}

/** The FBX in a pack's sources that holds its characters (packs.json's `container`), or null. */
async function containerOf(id) {
  const c = config.packs[id]?.container;
  const s = sourcesOf(id);
  if (!c || !s?.available) return null;
  return (await s.files()).find((f) => f.endsWith(`/${c.fbx}`)) ?? null;
}

/** The meshes of a prefab drawn from its pack's container: the MaterialList's (no attachments), or the one its name gives. */
function containedMeshes(id, name, prefab) {
  const fromList = (prefab ?? []).map((m) => m.mesh).filter((m) => !/_attach_/i.test(m));
  if (fromList.length) return fromList;
  const rule = config.packs[id]?.container?.mesh;
  return rule ? [name.replace(new RegExp(rule[0]), rule[1])] : [name];
}

/** What the page needs to paint a model as Unity would: the texture of each of its slots, and what may come out wrong. */
async function modelInfo(id, path, asked) {
  const s = sourcesOf(id);
  if (!s?.available) return null;
  // (`asked`: the name the page shows, a Unity prefab's when it is made from an FBX of another name.)
  const fbx = basename(path, extname(path));
  const name = asked || fbx;
  const ml = await s.materials();
  const atlasFile = await s.atlasFile();
  // (An SK_ model, the one with bones, is not in the MaterialList: its SM_ twin is, with the same slots.)
  const prefab = [name, fbx].map((n) => n.toLowerCase()).flatMap((n) => [n, n.replace(/^sk_/, 'sm_')])
    .map((n) => ml.prefabs.get(n)).find(Boolean) ?? null;
  // (How the page paints it: each mesh's slots in order, from the MaterialList (an SK_'s meshes by their SM_ names);
  // a material of a mesh it does not list, by the slot of its name; else the atlas.)
  const meshes = {};
  for (const mesh of prefab ?? []) {
    meshes[mesh.mesh.toLowerCase().replace(/^sk_/, 'sm_')] = await Promise.all(mesh.slots.map((sl) => s.texture(sl.albedo)));
  }
  const look = { atlas: atlasFile, meshes, byKey: await s.slotFiles() };
  if (path === (await containerOf(id)) && name !== fbx) look.only = containedMeshes(id, name, prefab);
  // (Only what makes the page show it otherwise than Unity: in Unity each prefab has its own materials.)
  const warnings = [];
  if (!prefab) {
    if (!look.only) warnings.push('No está en la MaterialList del paquete: aquí se pinta con su atlas y puede verse con otros colores que en Unity.');
  } else {
    const lacking = new Set();
    for (const mesh of prefab) {
      for (const sl of mesh.slots) if (sl.albedo && !(await s.texture(sl.albedo))) lacking.add(sl.albedo);
    }
    for (const t of lacking) warnings.push(`Usa una textura que este paquete no trae (${t}): aquí se ve con el atlas y puede tener otros colores.`);
  }
  return { name, path, look, prefab, warnings, ...uses.live(uses.forModel(id, name)) };
}

// ---- What a proposal warns of before it is saved (docs/BIBLIOTECA.md §4): a pack not in Unity, a texture the game
// does not load yet. The height against a cover list is the page's, which measures the FBX (/api/medidas). ----

/** The textures a model is painted with: its meshes' from the MaterialList, else the pack's atlas. */
async function texturesOf(id, path, name) {
  const info = await modelInfo(id, path, name);
  const set = new Set(Object.values(info?.look.meshes ?? {}).flat().filter(Boolean));
  if (!set.size && info?.look.atlas) set.add(info.look.atlas);
  return set;
}
/** The textures of a pack's models the game uses (kept until «Volver a leer»). */
function packTextures(id) {
  if (!usedTextures.has(id)) {
    usedTextures.set(id, (async () => {
      const all = new Set();
      for (const m of (await packDetail(id))?.models ?? []) {
        if (m.uses.length && m.path) for (const t of await texturesOf(id, m.path, m.name)) all.add(t);
      }
      return all;
    })());
  }
  return usedTextures.get(id);
}

async function warningsFor(r) {
  const out = [];
  if (r.paquete === ':mixamo') {
    if (!uses.mixamo.get(r.nombre.toLowerCase())?.length) out.push('Este clip de Mixamo no está en Unity: Arte tendrá que importarlo (MixamoImport).');
    return out;
  }
  const pack = inventory.packs.find((p) => p.id === r.paquete);
  if (!pack) return out;
  if (!imported(pack.id)) out.push('Su paquete no está importado en Unity: Arte tendrá que importarlo y añadirlo a importar-synty.sh.');
  else if (config.packs[pack.id]?.partial) out.push(`Su paquete está importado a medias en Unity (${config.packs[pack.id].partial}): puede que este no esté y haya que importarlo.`);
  if (r.clase === 'modelo' && r.fbx) {
    const used = await packTextures(pack.id);
    if (!used.size) out.push('Nada de este paquete está en el juego: su atlas sería un material más donde salga, y cada material más cuesta en el fotograma.');
    else {
      const fresh = [...(await texturesOf(pack.id, r.fbx, r.nombre))].filter((t) => !used.has(t));
      if (fresh.length === 1) out.push(`Usa una textura que el juego aún no carga (${basename(fresh[0])}): sería un material más donde salga, y cada material más cuesta en el fotograma.`);
      else if (fresh.length) out.push(`Usa texturas que el juego aún no carga (${fresh.map((f) => basename(f)).join(', ')}): serían materiales nuevos donde salga, y cada material más cuesta en el fotograma.`);
    }
  }
  return out;
}

/** A list's cover: cuentas.json's `listas.<id>.cobertura` once Gameplay gives it, else by its name (shelfLow, shelfHigh). */
const coverOfList = (id) => uses.counts?.coberturas?.[id] ?? coverOf(id);

/** For the height check: the models of a cover list (or of an object's lists), with where their FBX is and their fit. */
async function measures(clase, id) {
  let ids = [], sets = [];
  if (clase === 'lista') { ids = targets.art.sets[id] ?? []; sets = [id]; }
  else if (clase === 'objeto') { sets = targets.setsOf(id).filter((x) => !uses.deadLists.has(x)); ids = [id, ...sets.filter(coverOfList).flatMap((x) => targets.art.sets[x])]; }
  const cover = sets.map(coverOfList).find(Boolean) ?? null;
  const members = [];
  for (const pid of [...new Set(ids)]) {
    const p = targets.prop(pid);
    const s = p?.pack ? sourcesOf(p.pack) : null;
    if (!s?.available) continue;
    const m = (await s.models()).find((x) => x.path && x.name.toLowerCase() === p.name.toLowerCase());
    const art = targets.art.props[pid];
    if (m) members.push({ id: pid, pack: p.pack, path: m.path, fit: art.fit ?? 0, stretch: !!art.stretch, tall: art.tall ?? 0 });
  }
  return { cobertura: cover, listas: sets, miembros: members };
}

function send(res, status, body, type = 'application/json; charset=utf-8') {
  res.writeHead(status, { 'Content-Type': type, 'Cache-Control': 'no-store' });
  res.end(typeof body === 'string' || Buffer.isBuffer(body) ? body : JSON.stringify(body));
}
function sendFile(res, file, cache = false) {
  if (!existsSync(file) || !statSync(file).isFile()) return send(res, 404, { error: 'no está' });
  res.writeHead(200, {
    'Content-Type': TYPES[extname(file).toLowerCase()] ?? 'application/octet-stream',
    'Content-Length': statSync(file).size,
    'Cache-Control': cache ? 'max-age=86400' : 'no-store',
  });
  createReadStream(file).pipe(res);
}
/** `rel` under `root`, or null if it climbs out. */
function within(root, rel) {
  const full = resolve(root, rel);
  return full === root || full.startsWith(root + sep) ? full : null;
}
const thumbFile = (id, path) => within(THUMBS, `${id}/${path}.png`);
function readBody(req, limit) {
  return new Promise((ok, fail) => {
    const chunks = [];
    let size = 0;
    req.on('data', (c) => {
      size += c.length;
      if (size > limit) { fail(new Error('demasiado grande')); req.destroy(); } else chunks.push(c);
    });
    req.on('end', () => ok(Buffer.concat(chunks)));
    req.on('error', fail);
  });
}

async function handle(req, res) {
  const url = new URL(req.url, `http://${HOST}:${PORT}`);
  const q = (k) => url.searchParams.get(k);
  const path = url.pathname;

  if (path === '/' || path === '/index.html') return sendFile(res, join(here, 'web/index.html'));
  if (path.startsWith('/web/')) return sendFile(res, within(join(here, 'web'), path.slice(5)) ?? '');
  if (path.startsWith('/vendor/three/')) return sendFile(res, within(join(here, 'node_modules/three'), path.slice(14)) ?? '', true);

  if (path === '/api/state') {
    return send(res, 200, {
      mounted: inventory.mounted, synty: SYNTY, read: inventory.read, build: uses.build, notes: uses.notes, sidekick: !!uses.sidekick,
      unplaced: uses.unplaced.map((u) => ({ name: u.name, title: u.use.title })),
      mixamo: existsSync(MIXAMO), sounds: uses.sounds.present,
    });
  }
  if (path === '/api/reload' && req.method === 'POST') { await loadAll(); return send(res, 200, { ok: true }); }
  if (path === '/api/packs') return send(res, 200, inventory.packs.map(packSummary));
  if (path === '/api/pack') {
    const detail = await packDetail(q('id'));
    return detail ? send(res, 200, detail) : send(res, 404, { error: 'no hay ese paquete' });
  }
  if (path === '/api/model') {
    const info = await modelInfo(q('id'), q('path'), q('name'));
    return info ? send(res, 200, info) : send(res, 404, { error: 'sin fuentes' });
  }
  if (path === '/api/file') {
    const s = sourcesOf(q('id'));
    if (!s?.available) return send(res, 404, { error: 'sin fuentes' });
    return sendFile(res, await s.path(q('path')), true);
  }
  if (path === '/api/icon') {
    const pack = inventory.packs.find((p) => p.id === q('id'));
    if (!pack?.icon || !inventory.mounted) return send(res, 404, { error: 'sin icono' });
    return sendFile(res, join(SYNTY, pack.id, pack.icon), true);
  }
  if (path === '/api/thumb') {
    const file = thumbFile(q('id'), q('path'));
    if (!file) return send(res, 400, { error: 'ruta no válida' });
    if (req.method === 'POST') {
      const body = await readBody(req, 2 << 20);
      if (body.subarray(1, 4).toString() !== 'PNG') return send(res, 400, { error: 'no es un PNG' });
      mkdirSync(dirname(file), { recursive: true });
      writeFileSync(file, body);
      return send(res, 200, { ok: true });
    }
    return sendFile(res, file);
  }
  if (path === '/api/unity') {
    // (Arte's pictures: assets-src/biblioteca/unity/sidekick, as usados.json's `carpeta` says.)
    const base = uses.sidekick?.carpeta ? join(ROOT, uses.sidekick.carpeta) : null;
    return sendFile(res, base ? within(base, q('file') ?? '') ?? '' : '', true);
  }
  if (path === '/api/seleccion') {
    if (req.method === 'POST') {
      const point = JSON.parse((await readBody(req, 64 << 10)).toString('utf8') || '{}');
      const r = point.recurso ?? {};
      const pack = inventory.packs.find((p) => p.id === r.paquete);
      if (!pack && r.paquete !== ':mixamo' && r.paquete !== ':sonidos') return send(res, 400, { error: 'No hay ese paquete.' });
      if (pack && offStyle(pack)) return send(res, 400, { error: 'Fuera de estilo: no se puede proponer (CLAUDE.md).' });
      if ((point.tipo === 'usar' || point.tipo === 'añadir') && !targets.has(r.clase, point.destino ?? {})) return send(res, 400, { error: 'Eso no está en las listas del juego.' });
      point.avisos = [...(await warningsFor(r)), ...(Array.isArray(point.avisos) ? point.avisos : [])];
      try { return send(res, 200, selection.add(point)); } catch (e) { return send(res, 400, { error: e.message }); }
    }
    if (req.method === 'DELETE') {
      try { selection.remove(q('id')); return send(res, 200, { ok: true }); } catch (e) { return send(res, 400, { error: e.message }); }
    }
    try { return send(res, 200, selection.read()); } catch (e) { return send(res, 500, { error: `seleccion.json no se puede leer: ${e.message}` }); }
  }
  if (path === '/api/maniqui') return send(res, 200, await mannequinFor(q('id'), q('path')));
  if (path === '/api/cuentas') return send(res, 200, countsFor((q('ids') ?? '').split(',').filter(Boolean)));
  if (path === '/api/destinos') return send(res, 200, targets.forClass(q('clase')));
  if (path === '/api/medidas') return send(res, 200, await measures(q('clase'), q('id')));
  if (path === '/api/avisos') {
    return send(res, 200, await warningsFor({ clase: q('clase'), paquete: q('id'), nombre: q('name') ?? '', fbx: q('path') || null }));
  }
  if (path === '/api/captura') {
    const rel = (q('ruta') ?? '').replace(/^\.dev-snapshots\//, '');
    const file = within(SNAPSHOTS, rel);
    return sendFile(res, file && /\.(png|jpe?g)$/i.test(file) ? file : '');
  }
  if (path === '/api/mixamo') {
    const files = existsSync(MIXAMO) ? readdirSync(MIXAMO).filter((f) => /\.fbx$/i.test(f)).sort() : [];
    return send(res, 200, files.map((f) => {
      const name = basename(f, extname(f));
      return { name, file: f, uses: uses.mixamo.get(name.toLowerCase()) ?? [] };
    }));
  }
  if (path === '/api/mixamo/file') return sendFile(res, within(MIXAMO, q('file') ?? '') ?? '', true);
  if (path === '/api/sounds') return send(res, 200, uses.sounds);
  if (path === '/api/sound') return sendFile(res, uses.sounds.present ? within(uses.sounds.dir, q('file') ?? '') ?? '' : '');
  return send(res, 404, { error: 'no hay nada aquí' });
}

await loadAll();
createServer((req, res) => {
  handle(req, res).catch((e) => {
    console.error(e);
    if (!res.headersSent) send(res, 500, { error: e.message });
    else res.end();
  });
}).listen(PORT, HOST, () => {
  console.log(`Biblioteca en http://${HOST}:${PORT} · ${inventory.packs.length} paquetes · disco ${inventory.mounted ? 'conectado' : 'sin conectar'}`);
});
