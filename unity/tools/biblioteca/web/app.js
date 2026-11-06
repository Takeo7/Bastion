// The library's page (docs/BIBLIOTECA.md §4): the packs on the left, the one open in the middle, the card of what is
// picked on the right, where Arturo proposes what to use (select.js). The URL keeps the pack and model open (#p=…&m=…).
import { Viewer, ClipViewer, thumb, clearThumbs, forgetTextures } from './render.js';

import { $, el, api, q, mb, plural } from './util.js';
import { loadSelection, badgeOf, keyOf, proposeBlock, selectionView, countOpen, countAll, STATE_LABEL } from './select.js';

const LINES = ['POLYGON', 'SIDEKICK', 'ANIMATION', 'INTERFACE', 'SIMPLE', 'POLYGON MINI'];
const GAME = [
  { id: ':sonidos', title: 'Sonidos del juego', sub: 'Efectos, música, ambiente y botones' },
  { id: ':mixamo', title: 'Mixamo', sub: 'Animaciones que Synty no tiene' },
];

const state = {
  info: null,
  packs: [],
  packFilter: 'all',
  search: '',
  open: null,       // the pack's id
  detail: null,     // what /api/pack gave for it
  useFilter: 'all',
  group: null,
  picked: null,     // the resource's name
  selFilter: 'todos', // «Tu selección»'s filter
  viewer: null,
};

// ---- The bar on top ----

function renderTop() {
  const { info, packs } = state;
  const imported = packs.filter((p) => p.imported).length;
  const used = packs.filter((p) => p.inUse > 0).length;
  $('#summary').textContent = `${plural(packs.length, 'paquete', 'paquetes')} en el disco · ${imported} importados en Unity · ${used} con algo en uso`;
  const pills = [];
  pills.push(info.mounted
    ? el('span', { class: 'pill ok', text: 'Disco conectado' })
    : el('span', { class: 'pill warn', text: 'Disco sin conectar: solo se abren los paquetes que hay en assets-src/synty' }));
  pills.push(info.build
    ? el('span', { class: 'pill ok', title: 'Lo exporta Arte desde Unity (BibliotecaExport): lo que lleva la app.', text: `Lo que lleva la app: ${info.build.version ? `de la ${info.build.version}, ` : ''}sacado el ${new Date(info.build.made).toLocaleString('es-ES', { day: 'numeric', month: 'numeric', hour: '2-digit', minute: '2-digit' })}` })
    : el('span', { class: 'pill', title: 'Lo exporta Arte desde Unity (BibliotecaExport). Hasta entonces, faltan los personajes de Sidekick que el juego monta por presets.', text: 'Sin usados.json de Unity: «Se usa en» sale de las listas del juego y del código' }));
  for (const n of info.notes ?? []) pills.push(el('span', { class: 'pill warn', text: n }));
  $('#status').replaceChildren(...pills);
}

// ---- The packs on the left ----

function packMatches(p) {
  if (state.packFilter === 'used' && !(p.inUse > 0)) return false;
  if (state.packFilter === 'imported' && !p.imported) return false;
  const s = state.search.trim().toLowerCase();
  return !s || p.title.toLowerCase().includes(s) || p.id.toLowerCase().includes(s) || p.id === state.open;
}

function renderPacks() {
  const nav = $('#packs');
  const counts = {
    all: state.packs.length,
    used: state.packs.filter((p) => p.inUse > 0).length,
    imported: state.packs.filter((p) => p.imported).length,
  };
  const chip = (id, label) => el('button', {
    class: 'chip', 'aria-pressed': String(state.packFilter === id),
    onclick: () => { state.packFilter = id; renderPacks(); },
  }, label, el('span', { class: 'n', text: counts[id] }));
  const kids = [el('div', { class: 'chips' }, chip('all', 'Todos'), chip('used', 'Con algo en uso'), chip('imported', 'En Unity'))];

  kids.push(el('div', { class: 'group-title' }, el('span', { text: 'DEL JUEGO' })));
  const open = countOpen();
  kids.push(el('button', {
    class: 'pack-row', 'aria-current': String(state.open === ':seleccion'), onclick: () => openPack(':seleccion'),
  }, el('div', { class: 'pack-icon sel-icon', text: countAll() || '' }), el('div', {},
    el('div', { class: 'pack-name', text: 'Tu selección' }),
    el('div', { class: 'pack-meta', text: countAll() ? `${plural(countAll(), 'propuesta', 'propuestas')}${open ? ` · ${open} sin revisar` : ''}` : 'Lo que propones para el juego' }))));
  for (const g of GAME) {
    kids.push(el('button', {
      class: 'pack-row', 'aria-current': String(state.open === g.id), onclick: () => openPack(g.id),
    }, el('div', { class: 'pack-icon' }), el('div', {}, el('div', { class: 'pack-name', text: g.title }), el('div', { class: 'pack-meta', text: g.sub }))));
  }
  for (const line of LINES) {
    const list = state.packs.filter((p) => p.line === line && packMatches(p));
    if (!list.length) continue;
    const used = list.filter((p) => p.inUse > 0).length;
    kids.push(el('div', { class: 'group-title' }, el('span', { text: line }), el('span', { text: used ? `${used} de ${list.length} con algo en uso` : `${list.length}` })));
    for (const p of list) kids.push(packRow(p));
  }
  nav.replaceChildren(...kids);
}

function packRow(p) {
  const icon = el('img', { class: 'pack-icon', alt: '', loading: 'lazy', src: `/api/icon?${q({ id: p.id })}` });
  icon.addEventListener('error', () => icon.replaceWith(el('div', { class: 'pack-icon' })), { once: true });
  const tags = [];
  if (p.inUse > 0) tags.push(el('span', { class: 'tag used', text: `${p.inUse.toLocaleString('es-ES')} en uso` }));
  if (p.imported) tags.push(el('span', { class: 'tag', title: p.partial ? `A medias: ${p.partial}` : undefined, text: p.partial ? 'en Unity, a medias' : 'en Unity' }));
  if (p.offStyle) tags.push(el('span', { class: 'tag off', text: 'fuera de estilo' }));
  if (p.sources === 'none' && p.kind !== 'sidekick') tags.push(el('span', { class: 'tag', text: 'sin fuentes' }));
  return el('button', {
    class: `pack-row${p.offStyle ? ' off' : ''}`, 'aria-current': String(state.open === p.id),
    title: p.id, onclick: () => openPack(p.id),
  }, icon, el('div', {}, el('div', { class: 'pack-name', text: p.title }), el('div', { class: 'pack-meta' }, tags)));
}

// ---- The pack in the middle ----

async function openPack(id, picked = null) {
  if (state.open !== id) {
    clearThumbs();
    forgetTextures();
    state.useFilter = 'all';
    state.group = null;
  }
  state.open = id;
  state.picked = picked;
  state.detail = null;
  location.hash = q(picked ? { p: id, m: picked } : { p: id });
  renderPacks();
  renderCard();
  const main = $('#pack');
  main.replaceChildren(el('div', { class: 'empty', text: 'Abriendo el paquete… La primera vez, un ZIP grande tarda unos segundos.' }));
  main.scrollTop = 0;
  try {
    if (id === ':seleccion') state.detail = { special: 'selection' };
    else if (id === ':sonidos') state.detail = { special: 'sounds', ...(await api('/api/sounds')) };
    else if (id === ':mixamo') state.detail = { special: 'mixamo', clips: await api('/api/mixamo') };
    else state.detail = await api(`/api/pack?${q({ id })}`);
  } catch (e) {
    main.replaceChildren(el('div', { class: 'empty', text: `No se puede abrir: ${e.message}` }));
    return;
  }
  if (state.open !== id) return;
  renderPack();
  renderCard();
}

function packHead(d) {
  const facts = [];
  const sizes = [d.version && `Versión ${d.version}`, d.unitySize && `Unity ${mb(d.unitySize)}`, d.sourceSize && `fuentes ${mb(d.sourceSize)}`].filter(Boolean);
  if (sizes.length) facts.push(sizes.join(' · '));
  facts.push(!d.imported ? 'No está importado en Unity' : d.partial ? `Importado en Unity a medias: ${d.partial} (${d.unityFolder})` : `Importado en Unity (${d.unityFolder})`);
  if (d.kind !== 'sidekick') {
    facts.push({
      dir: 'Fuentes: la carpeta de assets-src/synty',
      zip: 'Fuentes: el ZIP del disco (se descomprime lo que se abre)',
      none: 'Sin fuentes: Synty no trae FBX para este paquete, o el disco no está conectado',
    }[d.sources]);
  }
  const icon = el('img', { alt: '', src: `/api/icon?${q({ id: d.id })}` });
  icon.addEventListener('error', () => icon.replaceWith(el('div', { class: 'pack-icon', style: 'width:84px;height:84px' })), { once: true });
  return el('div', { class: 'pack-head' }, icon, el('div', {},
    el('h2', { text: d.title }),
    el('div', { class: 'facts' }, el('span', { text: d.line }), facts.map((f) => el('span', { text: f })))));
}

function renderPack() {
  const d = state.detail;
  const main = $('#pack');
  if (d.special === 'selection') {
    return main.replaceChildren(...selectionView({
      filter: state.selFilter,
      setFilter: (f) => { state.selFilter = f; renderPack(); },
      open: (paquete, nombre) => openPack(paquete, nombre),
      changed: selectionChanged,
    }));
  }
  if (d.special === 'sounds') return main.replaceChildren(...soundsView(d));
  if (d.special === 'mixamo') return main.replaceChildren(...mixamoView(d));
  const kids = [packHead(d)];
  if (d.offStyle) kids.push(el('div', { class: 'note warn', text: 'Fuera de estilo: se puede mirar, pero no proponer. CLAUDE.md: nada de SIMPLE ni de POLYGON MINI, salvo los animales de Forest Animals y los gatos de Cats.' }));
  if (d.kind === 'sidekick') kids.push(...sidekickView(d));
  else if (d.kind === 'interface') kids.push(...interfaceView(d));
  else if (d.kind === 'anims') kids.push(...animsView(d));
  else kids.push(...modelsView(d));
  main.replaceChildren(...kids);
}

function nameMatches(name) {
  const s = state.search.trim().toLowerCase();
  return !s || name.toLowerCase().includes(s);
}
function useMatches(r) {
  if (state.useFilter === 'used') return r.uses.length > 0;
  if (state.useFilter === 'unused') return r.uses.length === 0;
  return true;
}

function filterBar(list, groups) {
  const counts = { all: list.length, used: list.filter((r) => r.uses.length).length };
  counts.unused = counts.all - counts.used;
  const chip = (id, label) => el('button', {
    class: 'chip', 'aria-pressed': String(state.useFilter === id),
    onclick: () => { state.useFilter = id; renderPack(); },
  }, label, el('span', { class: 'n', text: counts[id].toLocaleString('es-ES') }));
  const bars = [el('div', { class: 'bar' }, chip('all', 'Todos'), chip('used', 'En uso'), chip('unused', 'Sin usar'))];
  if (groups && groups.size > 1) {
    const g = (id, label, n) => el('button', {
      class: 'chip', 'aria-pressed': String(state.group === id),
      onclick: () => { state.group = id; renderPack(); },
    }, label, el('span', { class: 'n', text: n.toLocaleString('es-ES') }));
    const sorted = [...groups.entries()].sort((a, b) => b[1] - a[1]);
    bars.push(el('div', { class: 'bar' }, el('span', { class: 'label', text: 'Grupo' }), g(null, 'Todos', list.length), sorted.map(([k, n]) => g(k, k, n))));
  }
  return bars;
}

function modelsView(d) {
  const all = d.models.filter((m) => nameMatches(m.name));
  const groups = new Map();
  for (const m of all) groups.set(m.group, (groups.get(m.group) ?? 0) + 1);
  if (state.group && !groups.has(state.group)) state.group = null;
  const shown = all.filter((m) => useMatches(m) && (!state.group || m.group === state.group))
    .sort((a, b) => (b.uses.length > 0) - (a.uses.length > 0) || a.name.localeCompare(b.name));
  const kids = [];
  if (d.sources === 'none') kids.push(el('div', { class: 'note', text: 'Este paquete no tiene FBX que abrir aquí. Lo que el juego le pide sale en la lista, sin imagen.' }));
  if (d.missing) {
    kids.push(el('div', { class: 'note', text: d.missing === 1
      ? 'El juego pide a este paquete 1 modelo sin un FBX con su nombre en las fuentes: es un prefab de Unity. Sale en el grupo «Sin FBX con su nombre».'
      : `El juego pide a este paquete ${d.missing.toLocaleString('es-ES')} modelos sin un FBX con su nombre en las fuentes: son prefabs de Unity. Salen en el grupo «Sin FBX con su nombre».` }));
  }
  kids.push(...filterBar(all, groups));
  if (!shown.length) kids.push(el('div', { class: 'empty', text: 'Nada con este filtro.' }));
  const grid = el('div', { class: 'grid' });
  const look = { atlas: d.atlas?.file ?? null };
  for (const m of shown) grid.append(tile(d, m, look));
  kids.push(grid);
  return kids;
}

const seen = new IntersectionObserver((entries) => {
  for (const e of entries) {
    e.target.dataset.visible = e.isIntersecting ? '1' : '';
    if (e.isIntersecting) e.target.dispatchEvent(new Event('visible'));
  }
}, { root: document.querySelector('#pack'), rootMargin: '300px' });

function tile(d, m, look) {
  const box = el('div', { class: 'thumb' });
  const used = m.uses.length > 0;
  const t = el('button', {
    class: `tile${used ? ' used' : ''}`, 'aria-current': String(state.picked === m.name), title: m.name,
    'data-key': keyOf(d.id, m.name), onclick: () => pick(m.name),
  }, box, el('div', { class: 'name', text: m.name }), used ? el('span', { class: 'dot', text: 'en uso' }) : null, badge(d.id, m.name));
  if (!m.path) {
    box.textContent = m.missing ? 'Sin FBX con su nombre' : 'Prefab de Unity: sin FBX con su nombre';
    return t;
  }
  if (m.contained) box.title = 'Del FBX que lleva todos los personajes del paquete';
  if (m.clip) {
    box.textContent = 'Animación: elígela para verla en movimiento';
    return t;
  }
  let asked = false;
  t.addEventListener('visible', () => {
    if (asked) return;
    asked = true;
    const img = el('img', { alt: '', decoding: 'async' });
    img.addEventListener('load', () => box.replaceChildren(img), { once: true });
    img.addEventListener('error', () => {
      box.textContent = '…';
      const done = (url) => {
        if (url === undefined) { asked = false; box.textContent = ''; return; }
        if (url) { img.src = url; box.replaceChildren(img); } else box.textContent = 'No se abre';
      };
      done.wanted = () => t.isConnected && t.dataset.visible === '1';
      thumb(d.id, m.path, look, done, m.thumbKey ?? m.path, m.contained ? m.name : null);
    }, { once: true });
    img.src = `/api/thumb?${q({ id: d.id, path: m.thumbKey ?? m.path })}`;
  });
  seen.observe(t);
  return t;
}

function animsView(d) {
  const all = d.models.filter((m) => nameMatches(m.name));
  const shown = all.filter(useMatches).sort((a, b) => (b.uses.length > 0) - (a.uses.length > 0) || a.name.localeCompare(b.name));
  const rows = el('div', { class: 'rows' });
  for (const m of shown) rows.append(row(m.name, m.uses, m.group, () => pick(m.name)));
  return [el('div', { class: 'note', text: 'Elige una para verla en movimiento. Se ve en el sitio: al andar o correr, la figura no avanza.' }), ...filterBar(all), rows];
}

function sidekickView(d) {
  const kids = [];
  if (!d.pictures) kids.push(el('div', { class: 'note', text: 'Sidekick no trae FBX: las imágenes las saca Unity (Arte, BibliotecaExport). Sin su usados.json, solo la lista; los personajes que el juego monta por presets salen sin usar.' }));
  const all = [...d.people, ...d.parts].filter((x) => nameMatches(x.name));
  const groups = new Map();
  for (const x of all) groups.set(x.group, (groups.get(x.group) ?? 0) + 1);
  if (state.group && !groups.has(state.group)) state.group = null;
  kids.push(...filterBar(all, groups));
  const shown = all.filter((x) => useMatches(x) && (!state.group || x.group === state.group));
  const people = shown.filter((x) => d.people.includes(x));
  const parts = shown.filter((x) => !d.people.includes(x)).sort((a, b) => (b.uses.length > 0) - (a.uses.length > 0) || a.name.localeCompare(b.name));
  const picture = (x) => {
    const box = el('div', { class: 'thumb' });
    if (x.image) {
      const img = el('img', { alt: '', loading: 'lazy', src: `/api/unity?${q({ file: x.image })}` });
      img.addEventListener('error', () => { box.textContent = 'Sin imagen'; }, { once: true });
      box.append(img);
    } else box.textContent = x.name.replace(/^SK_/, '').replace(/_/g, ' ');
    const used = x.uses.length > 0;
    return el('button', { class: `tile${used ? ' used' : ''}`, 'aria-current': String(state.picked === x.name), title: x.name, 'data-key': keyOf(d.id, x.name), onclick: () => pick(x.name) },
      box, el('div', { class: 'name', text: x.name }), used ? el('span', { class: 'dot', text: 'en uso' }) : null, badge(d.id, x.name));
  };
  if (people.length) {
    kids.push(el('h3', { class: 'section', text: `PERSONAJES · ${people.length}` }));
    kids.push(el('div', { class: 'grid' }, people.map(picture)));
  }
  if (parts.length) {
    kids.push(el('h3', { class: 'section', text: `PIEZAS · ${parts.length}` }));
    kids.push(el('div', { class: 'grid' }, parts.map(picture)));
  }
  if (!people.length && !parts.length) kids.push(el('div', { class: 'empty', text: 'Nada con este filtro.' }));
  return kids;
}

/** A resource's state in «Tu selección», on its tile or row; updated when seleccion.json changes. */
function badge(paquete, nombre) {
  const s = badgeOf(paquete, nombre);
  return s ? el('span', { class: `sel ${s}`, text: STATE_LABEL[s] }) : null;
}
function updateBadges() {
  for (const e of document.querySelectorAll('[data-key]')) {
    const bar = e.dataset.key.indexOf('|');
    e.querySelector(':scope > .sel')?.remove();
    const b = badge(e.dataset.key.slice(0, bar), e.dataset.key.slice(bar + 1));
    if (b) e.append(b);
  }
}

/** An INTERFACE pack: its images as they are, by folder; in use, the HUD's icons the build carries. */
function interfaceView(d) {
  const all = d.images.filter((x) => nameMatches(x.name));
  const groups = new Map();
  for (const x of all) groups.set(x.group, (groups.get(x.group) ?? 0) + 1);
  if (state.group && !groups.has(state.group)) state.group = null;
  const shown = all.filter((x) => useMatches(x) && (!state.group || x.group === state.group))
    .sort((a, b) => (b.uses.length > 0) - (a.uses.length > 0) || a.name.localeCompare(b.name));
  const kids = [];
  if (!d.images.length) kids.push(el('div', { class: 'note', text: 'Este paquete no tiene imágenes que abrir aquí, o el disco no está conectado.' }));
  kids.push(...filterBar(all, groups));
  const grid = el('div', { class: 'grid' });
  for (const x of shown.slice(0, 600)) {
    const used = x.uses.length > 0;
    grid.append(el('button', { class: `tile${used ? ' used' : ''}`, 'aria-current': String(state.picked === x.name), title: x.name, 'data-key': keyOf(d.id, x.name), onclick: () => pick(x.name) },
      x.path ? el('div', { class: 'thumb sprite' }, el('img', { alt: '', loading: 'lazy', src: `/api/file?${q({ id: d.id, path: x.path })}` }))
        : el('div', { class: 'thumb', text: 'Sin imagen con su nombre' }),
      el('div', { class: 'name', text: x.name }), used ? el('span', { class: 'dot', text: 'en uso' }) : null, badge(d.id, x.name)));
  }
  kids.push(grid);
  if (shown.length > 600) kids.push(el('p', { class: 'hint pad', text: `Se ven las 600 primeras de ${shown.length.toLocaleString('es-ES')}. Busca o elige un grupo para ver las demás.` }));
  return kids;
}

/** The uses to count: «Lo lleva la app» only when it is the only one; and each title once, for the line under a row. */
const counted = (uses) => (uses.some((u) => u.kind !== 'build') ? uses.filter((u) => u.kind !== 'build') : uses);
const titles = (uses) => [...new Set(uses.map((u) => u.title))].join(' · ');

function row(name, uses, sub, onclick, paquete = state.open) {
  const used = uses.length > 0;
  return el('button', { class: `row${used ? ' used' : ''}`, 'aria-current': String(state.picked === name), 'data-key': keyOf(paquete, name), onclick },
    el('span', { class: 'title', text: name }),
    el('span', { class: 'faint', text: used ? plural(counted(uses).length, 'uso', 'usos') : 'sin usar' }),
    used ? el('span', { class: 'sub', text: titles(uses) }) : sub ? el('span', { class: 'sub', text: sub }) : null,
    badge(paquete, name));
}

function mixamoView(d) {
  const all = d.clips.map((c) => ({ ...c, uses: c.uses })).filter((c) => nameMatches(c.name));
  const shown = all.filter(useMatches).sort((a, b) => (b.uses.length > 0) - (a.uses.length > 0) || a.name.localeCompare(b.name));
  const rows = el('div', { class: 'rows' });
  for (const c of shown) rows.append(row(c.name, c.uses, c.file, () => pick(c.name)));
  return [
    el('div', { class: 'pack-head' }, el('div', {}, el('h2', { text: 'Mixamo' }), el('div', { class: 'facts' },
      el('span', { text: `${plural(d.clips.length, 'clip', 'clips')} en assets-src/mixamo` }),
      el('span', { text: 'Las animaciones que Synty no tiene: las de armas (Arturo, 21/10), las del campamento, los zombis al comer y morder, y alguna más' })))),
    el('div', { class: 'note', text: 'Elige uno para verlo en movimiento. Los clips de Mixamo no traen cuerpo: se ven sus huesos.' }),
    ...filterBar(all), rows,
  ];
}

function soundsView(d) {
  if (!d.present) return [el('div', { class: 'empty', text: 'No hay sonidos generados en Unity (unity/tools/sonido.sh). Sin ellos no hay nada que escuchar.' })];
  const kids = [el('div', { class: 'pack-head' }, el('div', {}, el('h2', { text: 'Sonidos del juego' }), el('div', { class: 'facts' },
    el('span', { text: 'Del manifiesto de audio que genera unity/tools/sonido. Cambiarlos es cosa de Red.' }))))];
  for (const g of d.groups) {
    const events = g.events.filter((e) => nameMatches(e.event));
    if (!events.length) continue;
    kids.push(el('h3', { class: 'section', text: `${g.title.toUpperCase()} · ${events.length}` }));
    const rows = el('div', { class: 'rows' });
    for (const e of events) {
      const play = el('button', { class: 'play', 'aria-label': `Escuchar ${e.event}`, text: '▶', onclick: (ev) => { ev.stopPropagation(); playSound(e.files); } });
      const sub = [e.files.length > 1 ? `${e.files.length} variantes` : null, e.extra, e.code.length ? `En el código: ${e.code.join(', ')}` : null].filter(Boolean).join(' · ');
      rows.append(el('div', { class: 'row sound', 'aria-current': String(state.picked === e.event), 'data-key': keyOf(':sonidos', e.event), onclick: () => pick(e.event) },
        play, el('span', { class: 'title', text: e.event }), el('span', {}), sub ? el('span', { class: 'sub', text: sub }) : null, badge(':sonidos', e.event)));
    }
    kids.push(rows);
  }
  return kids;
}

let soundTurn = 0;
function playSound(files) {
  const player = $('#player');
  const file = files[soundTurn++ % files.length];
  player.src = `/api/sound?${q({ file })}`;
  player.play().catch(() => {});
}

// ---- The card on the right ----

function pick(name) {
  state.picked = name;
  location.hash = q({ p: state.open, m: name });
  for (const t of document.querySelectorAll('.tile[aria-current], .row[aria-current]')) {
    t.setAttribute('aria-current', String(t.title === name || t.querySelector('.title')?.textContent === name));
  }
  renderCard();
}

function usesBlock(uses) {
  if (!uses.length) return el('p', { class: 'unused', text: 'No lo usa el juego.' });
  return el('div', {}, uses.map((u) => el('div', { class: 'use' },
    el('b', { text: u.title }),
    u.detail ? el('p', { text: u.detail }) : null,
    u.how === 'name' ? el('p', { class: 'how', text: 'El juego lo pide solo por el nombre, y Unity le da el de este paquete.' }) : null)));
}

async function renderCard() {
  const card = $('#card');
  state.viewer?.destroy();
  state.viewer = null;
  const d = state.detail;
  if (!d || !state.picked) {
    const kind = d?.special === 'mixamo' ? 'anims' : d?.special === 'sounds' ? 'sounds' : d?.kind;
    const say = {
      interface: ['Elige una imagen para verla y saber dónde la usa el juego.', 'En verde, lo que usa el juego.'],
      anims: ['Elige un clip para verlo en movimiento y saber dónde lo usa el juego.', 'En verde, lo que usa el juego.'],
      sounds: ['Elige un sonido para escucharlo y saber dónde lo usa el juego.', 'En verde, lo que usa el juego.'],
    }[kind] ?? ['Elige un modelo para verlo en 3D y saber dónde lo usa el juego.', 'Se gira con el ratón y se acerca con la rueda. En verde, lo que usa el juego.'];
    card.replaceChildren(el('div', { class: 'empty' }, el('p', { text: say[0] }), el('p', { class: 'hint', text: say[1] })));
    return;
  }
  const name = state.picked;
  if (d.special === 'selection') return card.replaceChildren(el('div', { class: 'empty' }, el('p', { text: 'Pulsa «Ver» en una propuesta para abrir su modelo o su clip.' })));
  if (d.special === 'mixamo') {
    const c = d.clips.find((x) => x.name === name);
    if (!c) return;
    const body = el('div', { class: 'card-body' }, el('h2', { text: c.name }), el('div', { class: 'path', text: `assets-src/mixamo/${c.file}` }),
      el('div', {}, el('h3', { text: 'SE USA EN' }), usesBlock(c.uses)),
      propose({ clase: 'clip', paquete: ':mixamo', nombre: c.name, fbx: c.file }, c.uses, false));
    return clipCard(card, body, `/api/mixamo/file?${q({ file: c.file })}`, null, name);
  }
  if (d.special === 'sounds') {
    const e = d.groups.flatMap((g) => g.events.map((x) => ({ ...x, group: g.title }))).find((x) => x.event === name);
    if (!e) return;
    const soundUses = [{ title: 'Suena en el juego', detail: e.code.length ? `Lo nombra el código: ${e.code.join(', ')}` : null }];
    return card.replaceChildren(el('div', { class: 'card-body' }, el('h2', { text: e.event }),
      el('div', { class: 'path', text: `${e.group} · ${plural(e.files.length, 'archivo', 'archivos')}` }),
      el('div', { class: 'actions' }, e.files.map((f, i) => el('button', { text: `▶ ${e.files.length > 1 ? `Variante ${i + 1}` : 'Escuchar'}`, onclick: () => playSound([f]) }))),
      el('div', {}, el('h3', { text: 'SE USA EN' }), usesBlock(soundUses)),
      propose({ clase: 'sonido', paquete: ':sonidos', nombre: e.event }, soundUses, false),
      el('p', { class: 'hint', text: 'Si Gameplay lo aprueba, lo cambia Red (unity/tools/sonido).' })));
  }
  if (d.kind === 'interface') {
    const x = d.images.find((i) => i.name === name);
    if (!x) return;
    const img = x.path ? el('img', { class: 'picture sprite', alt: '', src: `/api/file?${q({ id: d.id, path: x.path })}` }) : el('div', { class: 'picture' });
    const size = el('div', { class: 'path', text: x.path ? `${d.title} · ${x.path}` : `${d.title} · sin imagen con su nombre` });
    img.addEventListener('load', () => { size.textContent = `${d.title} · ${img.naturalWidth} × ${img.naturalHeight} px · ${x.path}`; }, { once: true });
    return card.replaceChildren(img, el('div', { class: 'card-body' }, el('h2', { text: x.name }), size,
      x.unity ? el('div', { class: 'hint', text: `En Unity se llama «${x.unity}».` }) : null,
      el('div', {}, el('h3', { text: 'SE USA EN' }), usesBlock(x.uses)),
      propose({ clase: 'imagen', paquete: d.id, nombre: x.name, fbx: x.path }, x.uses, d.offStyle),
      el('p', { class: 'hint', text: 'Si Gameplay lo aprueba, lo mete UI.' })));
  }
  if (d.kind === 'sidekick') {
    const p = [...d.people, ...d.parts].find((x) => x.name === name);
    if (!p) return;
    const pic = p.image ? el('img', { class: 'picture', alt: '', src: `/api/unity?${q({ file: p.image })}` }) : null;
    return card.replaceChildren(...[pic, el('div', { class: 'card-body' }, el('h2', { text: p.name }),
      el('div', { class: 'path', text: `${d.title} · ${p.group}${p.parts ? ` · ${plural(p.parts, 'pieza', 'piezas')}` : ''}` }),
      el('div', {}, el('h3', { text: 'SE USA EN' }), usesBlock(p.uses)),
      propose({ clase: d.people.includes(p) ? 'personaje' : 'pieza', paquete: d.id, nombre: p.name }, p.uses, d.offStyle),
      p.image ? el('div', { class: 'hint', text: 'La imagen la saca Unity (Arte), con los colores del personaje que la lleva.' }) : null)].filter(Boolean));
  }
  const m = d.models?.find((x) => x.name === name);
  if (!m) return card.replaceChildren(el('div', { class: 'empty', text: `No hay «${name}» en este paquete.` }));
  const body = el('div', { class: 'card-body' },
    el('h2', { text: m.name }),
    el('div', { class: 'path', text: m.path ? `${d.title} · ${m.path}` : `${d.title} · ${m.missing ? 'sin FBX con su nombre' : 'prefab de Unity, sin FBX con su nombre'}` }),
    m.fbx ? el('div', { class: 'hint', text: `Prefab de Unity hecho con el FBX ${m.fbx}.` }) : null,
    m.contained ? el('div', { class: 'hint', text: `Prefab de Unity: sale de un FBX con todos los animales del paquete (${d.container}).` }) : null,
    el('div', {}, el('h3', { text: 'SE USA EN' }), usesBlock(m.uses)),
    deadBlock(m.dead),
    countsBlock(m.uses),
    d.kind === 'interface' ? null : propose({ clase: d.kind === 'anims' || m.clip ? 'clip' : 'modelo', paquete: d.id, nombre: m.name, ...(m.path ? { fbx: m.path } : {}) }, m.uses, d.offStyle));
  const kids = [];
  if (m.path && (d.kind === 'anims' || m.clip)) {
    const mannequin = await api(`/api/maniqui?${q({ id: d.id, path: m.path })}`).catch(() => null);
    if (state.picked !== name) return;
    return clipCard(card, body, `/api/file?${q({ id: d.id, path: m.path })}`, mannequin, name);
  }
  if (m.path && d.kind === 'models') {
    const canvas = el('canvas', { class: 'viewer' });
    kids.push(canvas);
    card.replaceChildren(...kids, body);
    state.viewer = new Viewer(canvas);
    const info = await api(`/api/model?${q({ id: d.id, path: m.path, name: m.name })}`).catch(() => null);
    if (state.picked !== name) return;
    const ok = await state.viewer.show(d.id, m.path, info?.look ?? { atlas: d.atlas?.file ?? null });
    if (state.picked !== name) return;
    if (!ok) body.prepend(el('div', { class: 'warning', text: 'Este FBX no se abre aquí (puede ser de una versión antigua de FBX).' }));
    if (info?.warnings?.length) body.querySelector('.propose')?.before(el('div', {}, el('h3', { text: 'AVISOS DE LA VISTA' }), info.warnings.map((w) => el('div', { class: 'warning', text: w }))));
  } else {
    card.replaceChildren(body);
  }
}

/** What MapArt names but nothing draws (cuentas.json's sinNombrar): the 0.10.1's spare cover lists and their objects. */
function deadBlock(dead) {
  if (!dead?.length) return null;
  return el('div', {}, el('h3', { text: 'EN LAS LISTAS, PERO NO SALE' }),
    el('p', { class: 'faint', text: 'Está en las listas del mapa, pero nunca sale en el juego: ningún código lo pide, ni pide las listas en las que está. Lo dicen las cuentas de Gameplay.' }),
    dead.map((u) => el('div', { class: 'use off' }, el('b', { text: u.title }), u.detail ? el('p', { text: u.detail }) : null)));
}

/** «En los mapas»: how often each of the model's map objects comes out, from Gameplay's counts (cuentas.json). */
function countsBlock(uses) {
  const ids = [...new Set(uses.flatMap((u) => (u.refs ?? []).filter((r) => r.clase === 'objeto').map((r) => r.id)))];
  if (!ids.length) return null;
  const box = el('div', {}, el('h3', { text: 'EN LOS MAPAS' }), el('p', { class: 'faint', text: 'Contando…' }));
  api(`/api/cuentas?${q({ ids: ids.join(',') })}`).then((c) => {
    if (!c.present) { box.replaceChildren(); return; }
    const pct = (x) => `${Math.round(x * 100)} %`;
    const num = (x) => x.toLocaleString('es-ES', { maximumFractionDigits: 1 });
    const kids = [el('h3', { text: 'EN LOS MAPAS' })];
    for (const o of c.objetos) {
      if (o.estado === 'muerto') continue;
      const head = el('b', { text: `«${o.id}»` });
      if (o.estado === 'sinContar') { kids.push(el('div', { class: 'count' }, head, el('p', { class: 'faint', text: 'Sin contar: a este lo coloca Unity, no el motor, como a los coches, los bloques, las cajas de botín y los muebles.' }))); continue; }
      if (o.estado === 'nunca') { kids.push(el('div', { class: 'count' }, head, el('p', { class: 'faint', text: `No ha salido en ningún mapa de los ${num(c.tipos * c.semillas)} contados.` }))); continue; }
      const rows = o.mapas.slice(0, 6).map((m) => el('tr', {}, el('td', { text: m.nombre }), el('td', { class: 'n', text: num(m.media) }),
        el('td', { class: 'n', text: `${m.min}–${m.max}` }), el('td', { class: 'n', text: pct(m.mapas) })));
      kids.push(el('div', { class: 'count' }, head,
        el('p', { class: 'faint', text: `Sale en ${o.mapas.length} de ${c.tipos} tipos de mapa.${o.mapas.length > 6 ? ' Donde más:' : ''}` }),
        el('table', { class: 'counts' }, el('tr', {}, el('th', { text: 'Tipo de mapa' }), el('th', { class: 'n', text: 'Media' }), el('th', { class: 'n', text: 'De… a…' }),
          el('th', { class: 'n', title: `En cuántos de los ${c.semillas} mapas de ese tipo sale al menos uno.`, text: 'Sale en' })), rows)));
    }
    kids.push(el('p', { class: 'hint', text: `Contado con el motor en ${c.semillas} mapas de cada tipo (cuentas de Gameplay, de la ${c.version}).` }));
    box.replaceChildren(...kids);
  }).catch(() => box.replaceChildren());
  return box;
}

/** A clip's card: the figure moving on top, with a pause, what it moves on and how long it lasts. */
async function clipCard(card, body, clipUrl, mannequin, name) {
  const canvas = el('canvas', { class: 'viewer' });
  const pause = el('button', { text: 'Pausa', disabled: true });
  const says = el('span', { class: 'faint', text: 'Cargando el clip…' });
  card.replaceChildren(canvas, el('div', { class: 'clip-bar' }, pause, says), body);
  const viewer = new ClipViewer(canvas);
  state.viewer = viewer;
  pause.addEventListener('click', () => { viewer.toggle(); pause.textContent = viewer.paused ? 'Seguir' : 'Pausa'; });
  try {
    const r = await viewer.play(clipUrl, mannequin);
    if (state.picked !== name) return;
    if (!r.ok) { says.textContent = 'Este FBX no trae ningún clip.'; return; }
    pause.disabled = false;
    const secs = r.seconds.toLocaleString('es-ES', { maximumFractionDigits: 1 });
    says.textContent = r.on === 'maniquí'
      ? `${secs} s · ${mannequin.only ? `sobre un perro del paquete (${mannequin.path.split('/').pop().replace(/\.fbx$/i, '')})` : `sobre el maniquí de Synty (${mannequin.path.split('/').pop().replace(/\.fbx$/i, '')})`}`
      : `${secs} s · sobre sus huesos: el clip no trae cuerpo`;
  } catch (e) {
    says.textContent = 'Este FBX no se abre aquí.';
  }
}

/** The card's «Proponer», redrawn alone when the selection changes (the 3D view stays). */
function propose(r, uses, offStyle) {
  const holder = el('div', { class: 'propose-holder', 'data-propose': keyOf(r.paquete, r.nombre) });
  const draw = () => holder.replaceChildren(proposeBlock(r, uses, { offStyle, changed: selectionChanged }));
  holder.redraw = draw;
  draw();
  return holder;
}

/** After a save, a delete or a change by hand in seleccion.json (Gameplay's review). */
function selectionChanged() {
  updateBadges();
  renderPacks();
  if (state.detail?.special === 'selection') renderPack();
  // (The open form keeps what is being written: only a card with no form open is redrawn.)
  for (const h of document.querySelectorAll('[data-propose]')) if (!h.querySelector('.form textarea')) h.redraw?.();
}

async function refreshSelection() {
  if (document.visibilityState !== 'visible') return;
  try {
    if (await loadSelection()) selectionChanged();
  } catch { /* a hand edit half written: the next read */ }
}
setInterval(refreshSelection, 8000);
window.addEventListener('focus', refreshSelection);

// ---- Start ----

async function load() {
  [state.info, state.packs] = await Promise.all([api('/api/state'), api('/api/packs'), loadSelection().catch(() => false)]);
  renderTop();
  renderPacks();
  const hash = new URLSearchParams(location.hash.slice(1));
  if (hash.get('p')) openPack(hash.get('p'), hash.get('m'));
  else renderCard();
}

// (A link to a pack or a model, pasted or come back to with the browser's arrows.)
window.addEventListener('hashchange', () => {
  const hash = new URLSearchParams(location.hash.slice(1));
  const p = hash.get('p'), m = hash.get('m');
  if (p && p !== state.open) openPack(p, m);
  else if (p && m !== state.picked && state.detail) pick(m);
});

let searchTimer = 0;
$('#search').addEventListener('input', (e) => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    state.search = e.target.value;
    renderPacks();
    if (state.detail) renderPack();
  }, 150);
});
$('#reload').addEventListener('click', async () => {
  $('#reload').disabled = true;
  try {
    await api('/api/reload', { method: 'POST' });
    await load();
  } finally {
    $('#reload').disabled = false;
  }
});

$('#pack').replaceChildren(el('div', { class: 'empty', text: 'Elige un paquete a la izquierda.' }));
load().catch((e) => { $('#summary').textContent = `No se puede leer la biblioteca: ${e.message}`; });
