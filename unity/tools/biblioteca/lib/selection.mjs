// Arturo's selection (docs/BIBLIOTECA.md §4, «Seleccionar» and «La revisión»): unity/tools/biblioteca/seleccion.json, in
// git. The page only adds points as «propuesto» and deletes its own while they still are; Gameplay sets «aprobado» or
// «rechazado» (with a `motivo`), and Arte, UI or Red «hecho» (with a `captura`), by hand in the file. So it is read again
// on every request, and written whole (to a file beside it, then moved over it) only from a fresh read.
import { existsSync, readFileSync, writeFileSync, renameSync } from 'node:fs';

export const KINDS = ['usar', 'añadir', 'quitar', 'nuevo'];
export const STATES = ['propuesto', 'aprobado', 'rechazado', 'hecho'];
const CLASSES = ['modelo', 'clip', 'pieza', 'personaje', 'sonido', 'imagen'];

const HEADER = 'La selección de Arturo en la biblioteca (docs/BIBLIOTECA.md §4). La página solo añade puntos en «propuesto» '
  + 'y borra los suyos mientras lo sigan siendo. Gameplay pone «aprobado» o «rechazado» (con `motivo`); Arte, UI o Red, '
  + '«hecho» (con `captura`, la ruta de su captura en .dev-snapshots). Se puede editar a mano: la página lo vuelve a leer sola. '
  + '`siguiente` es el número del próximo id: no se repite aunque se borre un punto.';

export class Selection {
  constructor(file) { this.file = file; }

  read() {
    if (!existsSync(this.file)) return { _: HEADER, siguiente: 1, puntos: [] };
    const data = JSON.parse(readFileSync(this.file, 'utf8'));
    data.puntos ??= [];
    data.siguiente ??= 1 + Math.max(0, ...data.puntos.map((p) => Number(/\d+$/.exec(p.id ?? '')?.[0] ?? 0)));
    return data;
  }

  write(data) {
    // (One point a line, as usados.json: a hand edit and git's diff read by point.)
    const text = `{\n "_": ${JSON.stringify(data._ ?? HEADER)},\n "siguiente": ${data.siguiente},\n "puntos": [\n`
      + data.puntos.map((p) => `  ${JSON.stringify(p)}`).join(',\n') + (data.puntos.length ? '\n' : '') + ' ]\n}\n';
    writeFileSync(`${this.file}.tmp`, text);
    renameSync(`${this.file}.tmp`, this.file);
  }

  /** A new point from the page, checked; throws with the reason in Spanish if it is not one. */
  add(point) {
    const tipo = point?.tipo;
    if (!KINDS.includes(tipo)) throw new Error('Ese tipo de propuesta no existe.');
    const r = point.recurso;
    if (!r || !CLASSES.includes(r.clase) || typeof r.paquete !== 'string' || typeof r.nombre !== 'string' || !r.nombre) throw new Error('Falta el recurso.');
    const d = point.destino ?? null;
    if ((tipo === 'usar' || tipo === 'añadir') && !(d?.clase && d?.id)) throw new Error('Falta elegir para qué es.');
    const nota = String(point.nota ?? '').trim().slice(0, 1000);
    if (tipo === 'nuevo' && !nota) throw new Error('En un uso nuevo, la nota tiene que decir cuál.');
    const avisos = Array.isArray(point.avisos) ? [...new Set(point.avisos.map(String))].slice(0, 8).map((a) => a.slice(0, 400)) : [];
    const data = this.read();
    const same = data.puntos.find((p) => p.estado === 'propuesto' && p.tipo === tipo && p.recurso?.paquete === r.paquete
      && p.recurso?.nombre === r.nombre && (p.destino?.clase ?? null) === (d?.clase ?? null) && (p.destino?.id ?? null) === (d?.id ?? null));
    if (same) throw new Error(`Ya lo has propuesto: ${same.id}.`);
    const id = `B-${String(data.siguiente).padStart(3, '0')}`;
    const made = {
      id, tipo,
      recurso: { clase: r.clase, paquete: r.paquete, nombre: r.nombre, ...(r.fbx ? { fbx: r.fbx } : {}) },
      destino: d ? { clase: String(d.clase), id: String(d.id) } : null,
      nota, fecha: new Date().toISOString().slice(0, 10), estado: 'propuesto', avisos, motivo: null, captura: null,
    };
    data.puntos.push(made);
    data.siguiente += 1;
    this.write(data);
    return made;
  }

  /** Deletes a point of the page's while it is still «propuesto». */
  remove(id) {
    const data = this.read();
    const at = data.puntos.findIndex((p) => p.id === id);
    if (at < 0) throw new Error(`No hay ningún ${id}.`);
    if (data.puntos[at].estado !== 'propuesto') throw new Error(`Gameplay ya ha revisado ${id}: no se puede borrar.`);
    data.puntos.splice(at, 1);
    this.write(data);
  }
}
