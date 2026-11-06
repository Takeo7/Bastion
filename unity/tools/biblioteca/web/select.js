// Arturo's proposals (docs/BIBLIOTECA.md §4, «Seleccionar» and «La revisión»): the card's «Proponer» and the «Tu selección»
// list. The page only adds points as «propuesto» and deletes them while they still are; Gameplay reviews them in
// seleccion.json, so it is read again every few seconds and when the window comes back.
import { el, api, q, plural } from './util.js';
import { measure } from './render.js';

export const KIND_LABEL = { usar: 'Usar para', añadir: 'Añadir a', quitar: 'Quitar', nuevo: 'Uso nuevo' };
export const STATE_LABEL = { propuesto: 'Sin revisar', aprobado: 'Aprobada', rechazado: 'Rechazada', hecho: 'Hecha' };
/** What a point goes to, with its article: «Usar para el objeto del mapa «dumpster»», «Quitar de la lista «cityLow»». */
const REF_LABEL = {
  objeto: 'el objeto del mapa', lista: 'la lista', clip: 'la animación', arma: 'el arma', pieza: 'la pieza de edificio',
  armario: 'la ropa de los soldados', muerto: 'el zombi hecho de un personaje', 'lista de clips': 'la lista de animaciones',
  imagen: 'la imagen',
};
/** «Añadir a una lista» of a wardrobe piece (targets.mjs's MORE_OF). */
const MORE_OF = {
  outfit: 'una opción más de ropa en Personalizar', headwear: 'una opción más de «Cabeza» en Personalizar',
  hair: 'un pelo más para los soldados', beard: 'una barba más para los soldados', face: 'una cara más para los soldados',
};
const isList = (clase) => clase === 'lista' || clase === 'lista de clips';
/** «de el» is «del». */
const from = (text) => `de ${text}`.replace(/^de el /, 'del ');

export const keyOf = (paquete, nombre) => `${paquete}|${nombre}`;

export const selection = { data: { puntos: [] }, text: '', byKey: new Map() };

/** Reads seleccion.json; resolves true if it changed since the last read. */
export async function loadSelection() {
  const data = await api('/api/seleccion');
  const text = JSON.stringify(data.puntos);
  if (text === selection.text) return false;
  selection.data = data;
  selection.text = text;
  selection.byKey = new Map();
  for (const p of data.puntos) {
    const k = keyOf(p.recurso?.paquete, p.recurso?.nombre);
    if (!selection.byKey.has(k)) selection.byKey.set(k, []);
    selection.byKey.get(k).push(p);
  }
  return true;
}

/** The state shown on a tile for a resource with points: the least advanced one still open. */
export function badgeOf(paquete, nombre) {
  const points = selection.byKey.get(keyOf(paquete, nombre));
  if (!points?.length) return null;
  for (const s of ['propuesto', 'aprobado', 'hecho', 'rechazado']) if (points.some((p) => p.estado === s)) return s;
  return null;
}

export function destinationText(p) {
  const d = p.destino;
  if (!d) return p.tipo === 'quitar' ? 'del juego entero' : null;
  if (d.clase === 'juego') return 'del juego entero';
  if (d.clase === 'armario (tipo)') return MORE_OF[d.id] ?? d.id;
  return `${REF_LABEL[d.clase] ?? d.clase} «${d.id}»`;
}

function pointLine(p) {
  const dest = destinationText(p);
  // (The object, the clip or the piece is this resource: «Quitar el objeto…»; from a list: «Quitar de la lista…».)
  if (p.tipo === 'quitar') {
    if (dest === 'del juego entero') return 'Quitar del juego entero. Gameplay decide qué sale en su lugar.';
    if (p.destino?.clase === 'objeto') return `Quitar «${p.destino.id}» del mapa. Gameplay decide qué sale en su lugar.`;
    return isList(p.destino?.clase) ? `Quitar ${from(dest)}` : `Quitar ${dest}`;
  }
  if (p.tipo === 'nuevo') return 'Uso nuevo';
  if (p.destino?.clase === 'armario (tipo)') return dest[0].toUpperCase() + dest.slice(1);
  return `${KIND_LABEL[p.tipo] ?? p.tipo} ${dest ?? ''}`.trim();
}

function stateChip(s) { return el('span', { class: `state ${s}`, text: STATE_LABEL[s] ?? s }); }

/**
 * The card's «Proponer» block for a resource: `r` = { clase, paquete, nombre, fbx? }, `uses` what «Se usa en» says.
 * `offStyle`: the pack can be looked at, not chosen. `changed()`: called after a save or a delete.
 */
export function proposeBlock(r, uses, { offStyle, changed }) {
  const box = el('div', { class: 'propose' });
  const mine = selection.byKey.get(keyOf(r.paquete, r.nombre)) ?? [];
  const kids = [el('h3', { text: 'PROPONER' })];
  if (mine.length) {
    kids.push(el('div', { class: 'points' }, mine.map((p) => el('div', { class: 'point', 'data-point': p.id },
      el('div', { class: 'point-head' }, el('b', { text: p.id }), stateChip(p.estado)),
      el('div', { text: pointLine(p) }),
      p.nota ? el('div', { class: 'faint', text: p.nota }) : null,
      p.motivo ? el('div', { class: 'faint', text: `Gameplay: ${p.motivo}` }) : null,
      p.estado === 'propuesto' ? el('button', { class: 'link', text: 'Borrar', onclick: () => remove(p.id, changed) }) : null))));
  }
  if (offStyle) {
    kids.push(el('p', { class: 'faint', text: 'Fuera de estilo: no se puede proponer.' }));
    box.append(...kids);
    return box;
  }
  const form = el('div', { class: 'form' });
  const choose = (tipo) => openForm(form, tipo, r, uses, changed);
  const can = { usar: ['modelo', 'clip', 'pieza', 'imagen'].includes(r.clase), añadir: ['modelo', 'clip', 'pieza'].includes(r.clase), quitar: uses.length > 0, nuevo: true };
  kids.push(el('div', { class: 'actions' },
    can.usar ? el('button', { text: 'Usar para…', onclick: () => choose('usar') }) : null,
    can.añadir ? el('button', { text: 'Añadir a una lista', onclick: () => choose('añadir') }) : null,
    can.quitar ? el('button', { text: 'Quitar', onclick: () => choose('quitar') }) : null,
    el('button', { text: 'Uso nuevo', onclick: () => choose('nuevo') })));
  kids.push(el('p', { class: 'hint', text: 'Es solo una propuesta: el juego no cambia hasta que Gameplay la aprueba y se mete en Unity.' }));
  kids.push(form);
  box.append(...kids);
  return box;
}

async function remove(id, changed) {
  try {
    await api(`/api/seleccion?${q({ id })}`, { method: 'DELETE' });
  } catch (e) {
    alert(e.message);
  }
  await loadSelection();
  changed?.();
}

const TITLES = {
  usar: 'Usar para…', añadir: 'Añadir a una lista', quitar: 'Quitar', nuevo: 'Uso nuevo',
};
const HELP = {
  usar: 'Pone este recurso en algo que el juego ya tiene, en lugar del que lleva ahora.',
  añadir: 'Una variante más en una lista de la que el juego elige.',
  quitar: 'Lo saca de donde lo nombra el juego.',
  nuevo: 'Algo que el juego aún no hace. Va a Gameplay como pedido: lleva reglas o código.',
};

async function openForm(form, tipo, r, uses, changed) {
  form.replaceChildren(el('p', { class: 'faint', text: 'Cargando…' }));
  let picked = null;
  const warnings = el('div', { class: 'warnings' });
  const note = el('textarea', { rows: 3, placeholder: tipo === 'nuevo' ? 'Qué uso nuevo: para qué y dónde.' : 'Nota (si quieres): por qué o cómo.' });
  const save = el('button', { class: 'primary', text: 'Proponer', disabled: tipo === 'usar' || tipo === 'añadir' });
  const status = el('p', { class: 'faint' });
  const kids = [el('h4', { text: TITLES[tipo] }), el('p', { class: 'hint', text: HELP[tipo] })];

  let serverWarnings = [];
  let heightWarning = null;
  const showWarnings = () => {
    const all = [...serverWarnings, ...(heightWarning ? [heightWarning] : [])];
    warnings.replaceChildren(...all.map((w) => el('div', { class: 'warning', text: w })));
  };
  api(`/api/avisos?${q({ clase: r.clase, id: r.paquete, name: r.nombre, path: r.fbx ?? '' })}`).then((w) => { serverWarnings = w; showWarnings(); }).catch(() => {});

  if (tipo === 'usar' || tipo === 'añadir') {
    const targets = (await api(`/api/destinos?${q({ clase: r.clase })}`))[tipo] ?? [];
    const search = el('input', { type: 'search', placeholder: tipo === 'usar' ? 'Buscar: dumpster, rifle, idleMasc…' : 'Buscar una lista: cityLow, crates…' });
    const list = el('div', { class: 'choices' });
    const chosen = el('div', { class: 'chosen' });
    const draw = () => {
      const s = search.value.trim().toLowerCase();
      const shown = targets.filter((t) => !s || t.id.toLowerCase().includes(s) || (t.sub ?? '').toLowerCase().includes(s)).slice(0, 60);
      list.replaceChildren(...shown.map((t) => el('button', {
        class: 'choice', 'aria-pressed': String(picked === t),
        onclick: async () => {
          picked = t;
          chosen.replaceChildren(el('b', { text: t.label }), t.sub ? el('div', { class: 'faint', text: t.sub }) : null);
          save.disabled = false;
          draw();
          heightWarning = null;
          showWarnings();
          heightWarning = await heightCheck(r, t);
          if (picked === t) showWarnings();
        },
      }, el('span', { text: t.label }), t.sub ? el('span', { class: 'faint', text: t.sub }) : null)));
      if (!shown.length) list.append(el('p', { class: 'faint', text: 'Nada con esa búsqueda.' }));
    };
    search.addEventListener('input', draw);
    draw();
    kids.push(search, list, chosen);
  } else if (tipo === 'quitar') {
    const refs = [];
    for (const u of uses) for (const ref of u.refs ?? []) if (!refs.some((x) => x.clase === ref.clase && x.id === ref.id)) refs.push(ref);
    const cap = (t) => t[0].toUpperCase() + t.slice(1);
    // (A map object is asked for by a kind of decor of the engine: taken out alone, its tile would be left undrawn.
    // So it only proposes taking this model out; what comes out in its place, Gameplay decides (5/11).)
    const options = [...refs.map((ref) => {
      if (ref.clase === 'objeto') return { ...ref, label: `«${ref.id}» del mapa` };
      const what = `${REF_LABEL[ref.clase] ?? ref.clase} «${ref.id}»`;
      return { ...ref, label: cap(isList(ref.clase) ? from(what) : what) };
    }), { clase: 'juego', id: 'todo', label: 'Del juego entero' }];
    // (A list's last variant: taken out, the list would be left empty.)
    const sizes = new Map(((await api(`/api/destinos?${q({ clase: r.clase })}`).catch(() => ({})))['añadir'] ?? []).map((t) => [`${t.clase}|${t.id}`, t.count]));
    const choose = (o) => {
      picked = o;
      heightWarning = isList(o.clase) && sizes.get(`${o.clase}|${o.id}`) === 1
        ? `Es la única variante de «${o.id}»: la lista se quedaría vacía. Gameplay decide qué sale en su lugar.` : null;
      showWarnings();
    };
    kids.push(el('div', { class: 'choices' }, options.map((o, i) => el('label', { class: 'radio' },
      el('input', { type: 'radio', name: 'quitar', checked: i === 0, onchange: () => choose(o) }), el('span', { text: o.label })))));
    kids.push(el('p', { class: 'hint', text: 'Si quitas un objeto del mapa o lo quitas del juego entero, Gameplay decide qué sale en su lugar.' }));
    choose(options[0]);
    kids.push(el('h4', { text: 'Lo nombran' }), el('div', {}, uses.map((u) => el('div', { class: 'use small' },
      el('b', { text: u.title }), u.detail ? el('p', { text: u.detail }) : null))));
  }
  kids.push(note, warnings, el('div', { class: 'actions' }, save, el('button', { text: 'Cancelar', onclick: () => form.replaceChildren() })), status);
  save.addEventListener('click', async () => {
    save.disabled = true;
    status.textContent = 'Guardando…';
    try {
      const destino = picked ? { clase: picked.clase, id: picked.id } : null;
      const point = await api('/api/seleccion', {
        method: 'POST',
        body: JSON.stringify({ tipo, recurso: r, destino, nota: note.value, avisos: heightWarning ? [heightWarning] : [] }),
      });
      // (The form closes, and the card shows the new point among this resource's.)
      form.replaceChildren();
      await loadSelection();
      changed?.();
      form.closest('.propose')?.querySelector(`[data-point="${point.id}"]`)?.classList.add('fresh');
    } catch (e) {
      status.textContent = e.message;
      save.disabled = false;
    }
  });
  form.replaceChildren(...kids);
}

/**
 * Whether a model would stand otherwise than a cover list's: its height in the game estimated from its FBX's shape and
 * the fit the list's models have (the longest side across to `fit` tiles, MapBuilder.Place), against theirs.
 */
async function heightCheck(r, t) {
  if (r.clase !== 'modelo' || !r.fbx || (t.clase !== 'lista' && t.clase !== 'objeto')) return null;
  const m = await api(`/api/medidas?${q({ clase: t.clase, id: t.id })}`).catch(() => null);
  if (!m?.cobertura) return null;
  const tall = async (x) => {
    if (x.tall) return x.tall;
    if (!(x.fit > 0) || x.stretch) return null;
    const s = await measure(x.pack, x.path);
    return s ? (s.y / Math.max(s.x, s.z, 1e-6)) * x.fit : null;
  };
  const heights = (await Promise.all(m.miembros.map(tall))).filter((h) => h > 0);
  if (!heights.length) return null;
  const fits = m.miembros.map((x) => x.fit).filter((f) => f > 0).sort((a, b) => a - b);
  const fit = fits.length ? fits[Math.floor(fits.length / 2)] : 1;
  const own = await measure(r.paquete, r.fbx);
  if (!own) return null;
  const mine = (own.y / Math.max(own.x, own.z, 1e-6)) * fit;
  const list = m.listas.find((x) => /Low$|High$/.test(x)) ?? t.id;
  const fmt = (h) => h.toLocaleString('es-ES', { maximumFractionDigits: 2 });
  if (m.cobertura === 'baja' && mine > Math.max(...heights) * 1.2) {
    return `Es más alto que lo demás de «${list}», que es cobertura baja: mediría unas ${fmt(mine)} casillas de alto, y lo más alto de la lista, ${fmt(Math.max(...heights))}. Se vería como cobertura alta, pero cubriría como baja.`;
  }
  if (m.cobertura === 'alta' && mine < Math.min(...heights) * 0.8) {
    return `Es más bajo que lo demás de «${list}», que es cobertura alta: mediría unas ${fmt(mine)} casillas de alto, y lo más bajo de la lista, ${fmt(Math.min(...heights))}. Se vería como cobertura baja, pero cubriría como alta.`;
  }
  return null;
}

/** «Tu selección»: every point, by state, with what Gameplay said and the capture once done. */
export function selectionView({ filter, setFilter, open, changed }) {
  const points = selection.data.puntos ?? [];
  const counts = { todos: points.length };
  for (const s of Object.keys(STATE_LABEL)) counts[s] = points.filter((p) => p.estado === s).length;
  const chip = (id, label) => el('button', { class: 'chip', 'aria-pressed': String(filter === id), onclick: () => setFilter(id) },
    label, el('span', { class: 'n', text: counts[id] ?? 0 }));
  const kids = [
    el('div', { class: 'pack-head' }, el('div', {}, el('h2', { text: 'Tu selección' }), el('div', { class: 'facts' },
      el('span', { text: 'Tus propuestas. Gameplay las aprueba o las rechaza, y quien las mete en Unity las marca como «Hecha», con una captura.' }),
      el('span', { text: 'unity/tools/biblioteca/seleccion.json' })))),
    el('div', { class: 'bar' }, chip('todos', 'Todos'), Object.entries(STATE_LABEL).map(([s, l]) => chip(s, l))),
  ];
  const shown = points.filter((p) => filter === 'todos' || p.estado === filter).slice().reverse();
  if (!shown.length) kids.push(el('div', { class: 'empty', text: points.length ? 'Nada con este filtro.' : 'Aún no has propuesto nada. Elige un modelo, un clip o un sonido, y propón desde su ficha.' }));
  const rows = el('div', { class: 'rows' });
  for (const p of shown) {
    const r = p.recurso ?? {};
    const pack = r.paquete === ':mixamo' ? 'Mixamo' : r.paquete === ':sonidos' ? 'Sonidos del juego' : r.paquete?.replace(/^[A-Z ]+ - /, '');
    rows.append(el('div', { class: 'row selection-row' },
      el('div', { class: 'point-head' }, el('b', { text: p.id }), stateChip(p.estado), el('span', { class: 'faint', text: p.fecha })),
      el('div', { class: 'title' }, el('b', { text: r.nombre }), el('span', { class: 'faint', text: ` · ${pack}` })),
      el('div', { text: pointLine(p) }),
      p.nota ? el('div', { class: 'faint', text: `Nota: ${p.nota}` }) : null,
      (p.avisos ?? []).length ? el('div', { class: 'faint', text: `Avisos: ${p.avisos.join(' ')}` }) : null,
      p.motivo ? el('div', { class: 'motivo', text: `Gameplay: ${p.motivo}` }) : null,
      p.captura ? el('a', { href: `/api/captura?${q({ ruta: p.captura })}`, target: '_blank' },
        el('img', { class: 'capture', alt: `Captura de ${p.id}`, src: `/api/captura?${q({ ruta: p.captura })}` })) : null,
      el('div', { class: 'actions' },
        r.paquete && r.paquete !== ':sonidos' ? el('button', { text: 'Ver', onclick: () => open(r.paquete, r.nombre) }) : null,
        p.estado === 'propuesto' ? el('button', { text: 'Borrar', onclick: () => remove(p.id, changed) }) : null)));
  }
  kids.push(rows);
  return kids;
}

export const countOpen = () => (selection.data.puntos ?? []).filter((p) => p.estado === 'propuesto').length;
export const countAll = () => (selection.data.puntos ?? []).length;
export { plural };
