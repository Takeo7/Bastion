// What a selection can point at (docs/BIBLIOTECA.md §4, «Seleccionar»): the game's ids and lists a resource can go to,
// read from the game's lists (MapArt, Anims, Wardrobe). Only the ones that exist: a new use is a free note for Gameplay.
import { readFileSync } from 'node:fs';
import { join, basename } from 'node:path';
import { classNames, PIECE_NAMES, sexOf } from './uses.mjs';

/** «Añadir a una lista» of a wardrobe piece: Personalizar chooses the clothes and the head; the rest, the soldiers get. */
export const MORE_OF = {
  outfit: 'Una opción más de ropa en Personalizar', headwear: 'Una opción más de «Cabeza» en Personalizar',
  hair: 'Un pelo más para los soldados', beard: 'Una barba más para los soldados', face: 'Una cara más para los soldados',
};

const readJson = (file) => JSON.parse(readFileSync(file, 'utf8'));

/** The clip lists the game picks from (an array of clip ids), by their place in Anims.json, in Spanish. */
const CLIP_LISTS = { idleVariations: 'Esperas', actions: 'Acciones', rifleRests: 'Reposo con el fusil', campVariations: 'Campamento, variaciones', campFree: 'Campamento, sueltos' };

/** A cover list by its name: cityLow is low cover, cityHigh high (the engine's tiles Low and High). */
export const coverOf = (set) => (/Low$/.test(set) ? 'baja' : /High$/.test(set) ? 'alta' : null);

export class Targets {
  /** `uses`: the loaded Uses (for each clip's roles and each prop's pack). */
  constructor(root, uses) {
    const res = join(root, 'unity/Bastion/Assets/Bastion/Client/Resources');
    this.art = readJson(join(res, 'MapArt.json'));
    this.anims = readJson(join(res, 'Anims.json'));
    this.wardrobe = readJson(join(res, 'Wardrobe.json')).pieces ?? [];
    this.uses = uses;
  }

  /** The map's prop `id` as a model: its pack on the disk, its FBX's name and how it is fitted. */
  prop(id) {
    const p = this.art.props[id];
    if (!p) return null;
    return { id, pack: this.uses.keyToPack.get(p.pack) ?? null, name: basename(p.cut?.source ?? p.fbx), fit: p.fit ?? null };
  }

  setsOf(id) {
    return Object.entries(this.art.sets).filter(([, ids]) => Array.isArray(ids) && ids.includes(id)).map(([s]) => s);
  }

  /** For a resource of `clase` (modelo, clip, pieza, personaje, sonido): what «Usar para…» and «Añadir a una lista» offer. */
  forClass(clase) {
    if (clase === 'modelo') {
      // (Not what nothing draws, Gameplay's cuentas.json: cityLow and the other spare cover lists, and their objects.)
      const objects = Object.keys(this.art.props).filter((id) => !this.uses.deadObjects?.has(id)).sort().map((id) => {
        const sets = this.setsOf(id);
        const live = sets.filter((s) => !this.uses.deadLists?.has(s));
        return { clase: 'objeto', id, label: `Objeto del mapa «${id}»`, sub: live.length ? `En ${live.map((s) => `«${s}»`).join(', ')}` : null };
      });
      const carriers = new Map();
      for (const [unit, ids] of Object.entries(this.anims.unitWeapons ?? {})) for (const w of new Set(ids)) carriers.set(w, [...(carriers.get(w) ?? []), unit]);
      const weapons = Object.keys(this.anims.weapons ?? {}).sort().map((id) => ({
        clase: 'arma', id, label: `Arma «${id}»`, sub: carriers.has(id) ? `La llevan: ${classNames(carriers.get(id))}` : null,
      }));
      const sets = Object.entries(this.art.sets).filter(([id, ids]) => Array.isArray(ids) && !this.uses.deadLists?.has(id)).sort(([a], [b]) => a.localeCompare(b)).map(([id, ids]) => {
        // (Gameplay's cuentas.json says which lists the engine puts on cover, «baja», «alta» or «las dos»; else by the name.)
        const cover = this.uses.counts?.coberturas?.[id] ?? coverOf(id);
        return { clase: 'lista', id, label: `Lista «${id}»`, cobertura: cover, count: ids.length,
          sub: `${cover ? (cover === 'las dos' ? 'Cobertura baja y alta. ' : `Cobertura ${cover}. `) : ''}${ids.length === 1 ? 'Ahora, solo' : `Ahora, ${ids.length}:`} ${ids.slice(0, 6).map((x) => `«${x}»`).join(', ')}${ids.length > 6 ? '…' : ''}` };
      });
      return { usar: [...objects, ...weapons], añadir: sets };
    }
    if (clase === 'clip') {
      const clips = Object.keys(this.anims.clips).sort().map((id) => ({
        clase: 'clip', id, label: `Animación «${id}»`, sub: this.uses.roles?.has(id) ? [...this.uses.roles.get(id)].sort().join('; ') : null,
      }));
      const lists = [];
      for (const [table, label] of Object.entries(CLIP_LISTS)) {
        const v = this.anims[table];
        if (Array.isArray(v)) lists.push({ clase: 'lista de clips', id: table, label, sub: `Ahora, ${v.length}`, count: v.length });
        else if (v && typeof v === 'object') {
          for (const [k, x] of Object.entries(v)) if (Array.isArray(x)) lists.push({ clase: 'lista de clips', id: `${table}.${k}`, label: `${label} · ${sexOf(k)}`, sub: `Ahora, ${x.length}`, count: x.length });
        }
      }
      return { usar: clips, añadir: lists };
    }
    if (clase === 'pieza') {
      const usar = this.wardrobe.map((w) => ({ clase: 'armario', id: w.id, label: `${PIECE_NAMES[w.piece] ?? w.piece} de los soldados: «${w.id}»`,
        sub: (w.piece === 'outfit' && this.uses.outfits?.get(w.id)) || (w.piece === 'headwear' && this.uses.heads?.get(w.id))
          ? `En Personalizar: «${this.uses.outfits.get(w.id) ?? this.uses.heads.get(w.id)}»` : null }));
      const kinds = [...new Set(this.wardrobe.map((w) => w.piece))].sort();
      const añadir = kinds.map((k) => ({ clase: 'armario (tipo)', id: k, label: MORE_OF[k] ?? `Una opción más de ${(PIECE_NAMES[k] ?? k).toLowerCase()}`, sub: `Ahora, ${this.wardrobe.filter((w) => w.piece === k).length}` }));
      return { usar, añadir };
    }
    if (clase === 'imagen') {
      const usar = (this.uses.uiImages ?? []).slice().sort((a, b) => a.name.localeCompare(b.name)).map((i) => ({
        clase: 'imagen', id: i.name, label: `Imagen «${i.name}»`, sub: `${i.pack.replace(/^INTERFACE - /, '')}${i.detail ? `; ${i.detail}` : ''}`,
      }));
      return { usar, añadir: [] };
    }
    return { usar: [], añadir: [] };
  }

  /** Whether a target exists, for the server to check a new point. */
  has(clase, d) {
    const t = this.forClass(clase);
    return [...t.usar, ...t.añadir].some((x) => x.clase === d.clase && x.id === d.id);
  }
}
