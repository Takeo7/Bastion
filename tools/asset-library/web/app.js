// Synty asset library: local browser UI for the Bastion asset index server.
// Vanilla ES module, no build step. All asset strings reach the DOM through
// textContent / attributes only; nothing is injected as HTML.

import { ModelViewer } from './viewer.js';
import { normalizeDescriptor, FIDELITY_LABELS } from './appearance.js';

const PAGE_SIZE = 60;
const POLL_MS = 1500;
const REQUEST_TIMEOUT_MS = 20000;
const DRAFT_KEY = 'bastion-asset-library-drafts';

// ---------------------------------------------------------------------------
// Small DOM helpers

const $ = (sel, root = document) => root.querySelector(sel);

/** Builds an element. Strings and numbers in `children` become text nodes. */
function h(tag, attrs, ...children) {
  const el = document.createElement(tag);
  if (attrs) {
    for (const [key, value] of Object.entries(attrs)) {
      if (value === undefined || value === null || value === false) continue;
      if (key === 'class') el.className = value;
      else if (key === 'text') el.textContent = String(value);
      else if (key.startsWith('on') && typeof value === 'function') el.addEventListener(key.slice(2), value);
      else if (key in el && typeof value !== 'string') el[key] = value;
      else el.setAttribute(key, value === true ? '' : String(value));
    }
  }
  append(el, children);
  return el;
}

function append(el, children) {
  for (const child of children.flat(Infinity)) {
    if (child === undefined || child === null || child === false) continue;
    el.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return el;
}

function icon(name, cls = 'icon') {
  const ns = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(ns, 'svg');
  svg.setAttribute('class', cls);
  svg.setAttribute('aria-hidden', 'true');
  const use = document.createElementNS(ns, 'use');
  use.setAttribute('href', `#i-${name}`);
  svg.append(use);
  return svg;
}

const fmtInt = (n) => (n !== null && n !== '' && Number.isFinite(Number(n)) ? Number(n).toLocaleString('es-ES') : '-');

function fmtDate(value) {
  if (!value) return 'Nunca';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return String(value);
  return d.toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' });
}

const fmtTime = (d = new Date()) => d.toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit', second: '2-digit' });

// Kind ids as the catalog stores them (catalog.classify).
const KIND_LABELS = {
  animacion: 'Animación', interfaz: 'Interfaz', material: 'Material', modelo: 'Modelo',
  otro: 'Otro', personaje: 'Personaje', prefab: 'Prefab', textura: 'Textura',
};
function kindLabel(kind) {
  if (!kind) return 'Sin tipo';
  const k = String(kind);
  return KIND_LABELS[k.toLowerCase()] || k.charAt(0).toUpperCase() + k.slice(1);
}

const STATE_LABELS = { used: 'En uso en el juego', selected: 'Seleccionados', pending: 'Solicitados, sin integrar' };

// ---------------------------------------------------------------------------
// API

class ApiError extends Error {
  constructor(message, status, data) {
    super(message);
    this.status = status;
    this.data = data;
  }
}

async function api(path, { method = 'GET', body, signal } = {}) {
  const signals = [AbortSignal.timeout(REQUEST_TIMEOUT_MS)];
  if (signal) signals.push(signal);
  let res;
  try {
    res = await fetch(path, {
      method,
      headers: body === undefined ? { Accept: 'application/json' } : { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: AbortSignal.any ? AbortSignal.any(signals) : signal,
      cache: 'no-store',
    });
  } catch (err) {
    if (err.name === 'AbortError' && signal?.aborted) throw err;
    if (err.name === 'TimeoutError' || err.name === 'AbortError') throw new ApiError('El servidor local no ha respondido a tiempo.', 0);
    throw new ApiError('No se pudo contactar con el servidor local de la biblioteca.', 0);
  }
  let data = null;
  const text = await res.text();
  if (text) {
    try { data = JSON.parse(text); } catch { data = { message: text.slice(0, 300) }; }
  }
  if (!res.ok) {
    const detail = data && (data.error || data.message);
    throw new ApiError(detail ? String(detail) : `El servidor respondió con el código ${res.status}.`, res.status, data);
  }
  return { status: res.status, data };
}

// ---------------------------------------------------------------------------
// State

const state = {
  filters: { q: '', family: '', pack: '', kind: '', state: '', offset: 0 },
  status: null,
  statusError: null,
  packs: [],
  packsError: null,
  list: { items: [], total: 0, offset: 0, limit: PAGE_SIZE, loading: false, error: null },
  kinds: new Set(),
  listAbort: null,
  listSeq: 0,
  pollTimer: 0,
  wasIndexing: false,
  notices: new Map(),
  drafts: loadDrafts(),
  detail: null,
};

function loadDrafts() {
  try {
    const raw = sessionStorage.getItem(DRAFT_KEY);
    const parsed = raw ? JSON.parse(raw) : {};
    return new Map(Object.entries(parsed));
  } catch {
    return new Map();
  }
}

function persistDrafts() {
  try {
    sessionStorage.setItem(DRAFT_KEY, JSON.stringify(Object.fromEntries(state.drafts)));
  } catch {
    // Session storage is only a convenience copy of unsaved drafts.
  }
}

function isDirty(draft) {
  return !!draft && (draft.selected !== draft.base.selected || draft.intent !== draft.base.intent);
}

// ---------------------------------------------------------------------------
// Notices (status bar)

function setNotice(key, kind, text, actions = []) {
  state.notices.set(key, { kind, text, actions });
  renderNotices();
}

function clearNotice(key) {
  if (state.notices.delete(key)) renderNotices();
}

function renderNotices() {
  const host = $('#notices');
  host.replaceChildren();
  for (const [key, n] of state.notices) {
    const row = h('div', { class: `notice ${n.kind}`, 'data-key': key },
      n.kind === 'error' || n.kind === 'warn' ? icon('alert') : null,
      h('span', { class: 'notice-text', text: n.text }),
      n.actions.map((a) => h('button', { type: 'button', class: 'btn small', onclick: a.onClick, text: a.label })),
    );
    if (n.progress !== undefined) row.append(n.progress);
    host.append(row);
  }
}

// ---------------------------------------------------------------------------
// URL hash <-> filters

function readHash() {
  const p = new URLSearchParams(location.hash.slice(1));
  const page = Math.max(1, parseInt(p.get('page') || '1', 10) || 1);
  state.filters = {
    q: p.get('q') || '',
    family: p.get('family') || '',
    pack: p.get('pack') || '',
    kind: p.get('kind') || '',
    state: ['used', 'selected', 'pending'].includes(p.get('state')) ? p.get('state') : '',
    offset: (page - 1) * PAGE_SIZE,
  };
  return p.get('asset') || '';
}

function writeHash() {
  const f = state.filters;
  const p = new URLSearchParams();
  for (const k of ['q', 'family', 'pack', 'kind', 'state']) if (f[k]) p.set(k, f[k]);
  if (f.offset) p.set('page', String(Math.floor(f.offset / PAGE_SIZE) + 1));
  if (state.detail?.id) p.set('asset', state.detail.id);
  const hash = p.toString();
  const url = `${location.pathname}${location.search}${hash ? `#${hash}` : ''}`;
  if (url !== `${location.pathname}${location.search}${location.hash}`) history.replaceState(null, '', url);
}

function setFilters(patch, { keepOffset = false } = {}) {
  Object.assign(state.filters, patch);
  if (!keepOffset) state.filters.offset = 0;
  writeHash();
  syncFilterControls();
  renderLibraryTree();
  loadList();
}

// ---------------------------------------------------------------------------
// Status and indexing

async function loadStatus() {
  try {
    const { data } = await api('/api/status');
    state.status = data || {};
    state.statusError = null;
    clearNotice('status-error');
    renderStatus();
    collectKinds(state.status.kinds);
    collectKinds(state.status.coverage?.kinds);
    renderKindOptions();
    const indexing = !!state.status.indexing;
    if (indexing) schedulePoll();
    else if (state.wasIndexing) onIndexingFinished();
    state.wasIndexing = indexing;
    return true;
  } catch (err) {
    state.statusError = err.message;
    $('#cSelected').textContent = 'Desconocido';
    setNotice('status-error', 'error', `No se pudo leer el estado del índice. ${err.message}`, [
      { label: 'Reintentar', onClick: loadStatus },
    ]);
    if (state.wasIndexing) schedulePoll(POLL_MS * 3);
    return false;
  }
}

function schedulePoll(ms = POLL_MS) {
  clearTimeout(state.pollTimer);
  state.pollTimer = setTimeout(loadStatus, ms);
}

async function onIndexingFinished() {
  clearNotice('indexing');
  setNotice('indexed', 'ok', `Indexación terminada a las ${fmtTime()}. Lista actualizada.`, [
    { label: 'Cerrar aviso', onClick: () => clearNotice('indexed') },
  ]);
  await loadPacks();
  loadList();
}

function describeProgress(s) {
  const p = s.progress ?? s.indexProgress ?? s.task?.progress;
  if (p === undefined || p === null) return { text: '', ratio: null };
  if (typeof p === 'number') {
    const ratio = p > 1 ? p / 100 : p;
    return { text: `${Math.round(ratio * 100)} %`, ratio };
  }
  if (typeof p === 'object') {
    const done = Number(p.done ?? p.current ?? p.processed);
    const total = Number(p.total);
    const parts = [];
    if (Number.isFinite(done) && Number.isFinite(total) && total > 0) parts.push(`${fmtInt(done)} de ${fmtInt(total)}`);
    const label = p.pack || p.packName || p.stage || p.message || p.file;
    if (label) parts.push(String(label));
    const ratio = Number.isFinite(done) && Number.isFinite(total) && total > 0 ? Math.min(1, done / total) : null;
    return { text: parts.join(', '), ratio };
  }
  return { text: String(p), ratio: null };
}

function issueText(issue) {
  if (issue === null || issue === undefined) return '';
  if (typeof issue === 'string') return issue;
  if (typeof issue === 'object') {
    const main = issue.message || issue.detail || issue.error || issue.code || '';
    const where = issue.path || issue.pack || issue.archive || '';
    return [main, where].filter(Boolean).join(': ') || JSON.stringify(issue);
  }
  return String(issue);
}

function diskMissing(s) {
  if (!s) return false;
  if (s.disk === false || s.diskAvailable === false || s.sourceAvailable === false || s.volumeMounted === false) return true;
  if (s.disk && typeof s.disk === 'object' && (s.disk.available === false || s.disk.mounted === false)) return true;
  return false;
}

const COVERAGE_LABELS = {
  thumbnails: 'miniaturas', thumbnail: 'miniaturas', previews: 'vistas previas', preview: 'vistas previas',
  packThumbnails: 'imágenes de pack', glb: 'GLB', fbx: 'FBX', images: 'imágenes', image: 'imágenes',
  available: 'disponibles', missing: 'sin archivo', withPreview: 'con vista previa', withThumbnail: 'con miniatura',
  packs: 'entradas de biblioteca', packsAvailable: 'entradas disponibles', unityPackages: 'paquetes Unity',
  sourceZips: 'ZIP de originales', extrasZips: 'ZIP de extras', unrealFallbackZips: 'ZIP exclusivos de Unreal',
  looseFiles: 'archivos sueltos', skippedGodot: 'copias de Godot omitidas', skippedUnreal: 'copias de Unreal omitidas',
  archiveErrors: 'errores de lectura', cacheHits: 'archivos leídos desde caché', importedLocalFiles: 'archivos importados locales',
  seconds: 'segundos de indexación',
};

function renderStatus() {
  const s = state.status || {};
  $('#cAssets').textContent = fmtInt(s.assets);
  $('#cPacks').textContent = fmtInt(s.packs);
  $('#cUsed').textContent = fmtInt(s.used);
  // Server count: used assets not deselected plus requested ones. Never derived from /api/selections.
  $('#cSelected').textContent = !state.statusError && typeof s.selected === 'number' && Number.isFinite(s.selected) ? fmtInt(s.selected) : 'Desconocido';
  $('#cIndexed').textContent = fmtDate(s.indexedAt);

  const reindexBtn = $('#reindexBtn');
  reindexBtn.disabled = !!s.indexing;
  reindexBtn.querySelector('span').textContent = s.indexing ? 'Indexando…' : 'Indexar biblioteca';

  if (s.indexing) {
    const prog = describeProgress(s);
    setNotice('indexing', 'info', `Indexando la biblioteca. Solo se leen los paquetes; los originales no se modifican.${prog.text ? ` Progreso: ${prog.text}.` : ''}`);
    if (prog.ratio !== null) {
      const bar = h('progress', { max: 1, value: prog.ratio, 'aria-label': 'Progreso de la indexación' });
      state.notices.get('indexing').progress = bar;
      renderNotices();
    }
  } else {
    clearNotice('indexing');
  }

  if (diskMissing(s)) {
    setNotice('disk', 'warn', 'El disco externo con los paquetes de Synty no está conectado. Se muestran los datos del último índice; las vistas previas pueden fallar.');
  } else {
    clearNotice('disk');
  }

  const broken = s.selections && typeof s.selections === 'object' ? s.selections.broken : null;
  if (broken) {
    setNotice('selections-broken', 'error', `Selección bloqueada: no se puede guardar. ${broken}`);
  } else {
    clearNotice('selections-broken');
  }

  let issues = Array.isArray(s.issues) ? s.issues : typeof s.issues === 'number' && s.issues > 0 ? [`${s.issues} incidencias en el índice`] : [];
  if (broken) issues = issues.filter((i) => i !== broken);
  if (issues.length) {
    const first = issues.slice(0, 3).map(issueText).filter(Boolean).join(' · ');
    const more = issues.length > 3 ? ` y ${issues.length - 3} más` : '';
    setNotice('issues', 'warn', `Incidencias del índice (${issues.length}): ${first}${more}.`);
  } else {
    clearNotice('issues');
  }

  if (s.coverage && typeof s.coverage === 'object') {
    const parts = [];
    for (const [k, v] of Object.entries(s.coverage)) {
      if (typeof v === 'number') parts.push(`${COVERAGE_LABELS[k] || k}: ${fmtInt(v)}`);
      else if (v && typeof v === 'object' && Number.isFinite(Number(v.count ?? v.done))) {
        const total = Number(v.total);
        parts.push(`${COVERAGE_LABELS[k] || k}: ${fmtInt(v.count ?? v.done)}${Number.isFinite(total) ? ` de ${fmtInt(total)}` : ''}`);
      }
    }
    if (parts.length) setNotice('coverage', 'muted', `Cobertura: ${parts.join(', ')}.`);
    else clearNotice('coverage');
  }

  if (!s.indexing && Number(s.assets) === 0) renderList();
}

async function startReindex() {
  const btn = $('#reindexBtn');
  btn.disabled = true;
  try {
    await api('/api/reindex', { method: 'POST' });
    state.wasIndexing = true;
    setNotice('indexing', 'info', 'Indexación iniciada. Solo se leen los paquetes; los originales no se modifican.');
    clearNotice('indexed');
    schedulePoll(400);
  } catch (err) {
    if (err.status === 409) {
      state.wasIndexing = true;
      setNotice('indexing', 'info', 'Ya hay una indexación en marcha. Se sigue su progreso.');
      schedulePoll(400);
    } else {
      btn.disabled = false;
      setNotice('reindex-error', 'error', `No se pudo iniciar la indexación. ${err.message}`, [
        { label: 'Reintentar', onClick: () => { clearNotice('reindex-error'); startReindex(); } },
      ]);
    }
  }
}

async function downloadExport(ev) {
  ev.preventDefault();
  const link = ev.currentTarget;
  link.setAttribute('aria-busy', 'true');
  try {
    const res = await fetch('/api/export', { cache: 'no-store', signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS) });
    if (!res.ok) {
      const data = await res.json().catch(() => null);
      const detail = data && (data.error || data.message);
      throw new ApiError(detail ? String(detail) : `El servidor respondió con el código ${res.status}.`, res.status, data);
    }
    const blob = await res.blob();
    const name = /filename="?([^";]+)"?/i.exec(res.headers.get('Content-Disposition') || '')?.[1] || 'assets-synty-seleccionados.md';
    const url = URL.createObjectURL(blob);
    const a = h('a', { href: url, download: name });
    document.body.append(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 2000);
    clearNotice('export-error');
  } catch (err) {
    const msg = err instanceof ApiError ? err.message : 'No se pudo descargar la exportación.';
    setNotice('export-error', 'error', `Exportación fallida. ${msg}`, [
      { label: 'Cerrar aviso', onClick: () => clearNotice('export-error') },
    ]);
  } finally {
    link.removeAttribute('aria-busy');
  }
}

// ---------------------------------------------------------------------------
// Packs / libraries sidebar

async function loadPacks() {
  const tree = $('#libraryTree');
  try {
    const { data } = await api('/api/packs');
    state.packs = Array.isArray(data?.items) ? data.items : [];
    state.packsError = null;
  } catch (err) {
    state.packsError = err.message;
  }
  if (!state.packs.length && !state.packsError) tree.replaceChildren(h('p', { class: 'muted', text: 'No hay packs indexados todavía.' }));
  renderLibraryTree();
}

const expandedFamilies = new Set();

function groupFamilies(packs) {
  const map = new Map();
  for (const p of packs) {
    const fam = p.family || 'Sin familia';
    if (!map.has(fam)) map.set(fam, { name: fam, key: p.family || '', packs: [], assets: 0, used: 0 });
    const g = map.get(fam);
    g.packs.push(p);
    g.assets += Number(p.assetCount) || 0;
    g.used += Number(p.usedCount) || 0;
  }
  const groups = [...map.values()].sort((a, b) => a.name.localeCompare(b.name, 'es'));
  for (const g of groups) g.packs.sort((a, b) => String(a.name).localeCompare(String(b.name), 'es'));
  return groups;
}

function renderLibraryTree() {
  const tree = $('#libraryTree');
  const f = state.filters;
  $('#clearLibrary').hidden = !f.family && !f.pack;
  if (state.packsError) {
    tree.replaceChildren(h('div', { class: 'inline-error' },
      h('p', { text: `No se pudieron cargar los packs. ${state.packsError}` }),
      h('button', { type: 'button', class: 'btn small', onclick: loadPacks, text: 'Reintentar' })));
    return;
  }
  if (!state.packs.length) return;
  const needle = $('#packSearch').value.trim().toLowerCase();
  const groups = groupFamilies(state.packs);
  const frag = document.createDocumentFragment();
  let shown = 0;
  groups.forEach((g, gi) => {
    const packs = needle ? g.packs.filter((p) => String(p.name).toLowerCase().includes(needle) || g.name.toLowerCase().includes(needle)) : g.packs;
    if (!packs.length) return;
    shown += packs.length;
    const containsActive = g.packs.some((p) => p.id === f.pack);
    const open = !!needle || expandedFamilies.has(g.name) || containsActive || (!!g.key && f.family === g.key);
    const listId = `fam-list-${gi}`;
    const section = h('section', { class: 'family' });
    const toggle = h('button', {
      type: 'button', class: 'fam-toggle', 'aria-expanded': String(open), 'aria-controls': listId,
      title: open ? 'Plegar familia' : 'Desplegar familia',
      onclick: () => {
        if (expandedFamilies.has(g.name)) expandedFamilies.delete(g.name); else expandedFamilies.add(g.name);
        renderLibraryTree();
      },
    }, icon('next', 'icon chevron'), h('span', { class: 'sr-only', text: open ? 'Plegar' : 'Desplegar' }));
    const famBtn = h('button', {
      type: 'button', class: 'fam-name', 'aria-pressed': String(!!g.key && f.family === g.key && !f.pack),
      disabled: !g.key, title: g.key ? `Filtrar por la familia ${g.name}` : undefined,
      onclick: () => { expandedFamilies.add(g.name); setFilters({ family: f.family === g.key && !f.pack ? '' : g.key, pack: '' }); closeSidebarIfNarrow(); },
    }, h('span', { class: 'fam-label', text: g.name }), h('span', { class: 'count', text: fmtInt(g.assets) }));
    section.append(h('div', { class: 'fam-row' }, toggle, famBtn));
    if (open) {
      const ul = h('ul', { class: 'pack-list', id: listId });
      for (const p of packs) {
        const meta = [`${fmtInt(p.assetCount)} assets`];
        if (Number(p.usedCount) > 0) meta.push(`${fmtInt(p.usedCount)} en uso`);
        if (p.available === false) meta.push('no disponible en disco');
        ul.append(h('li', null, h('button', {
          type: 'button', class: `pack-btn${p.available === false ? ' unavailable' : ''}`,
          'aria-pressed': String(f.pack === p.id),
          title: `Filtrar por el pack ${p.name}`,
          onclick: () => { setFilters({ pack: f.pack === p.id ? '' : p.id, family: '' }); closeSidebarIfNarrow(); },
        }, h('span', { class: 'pack-name', text: p.name || p.id }), h('span', { class: 'pack-meta', text: meta.join(' · ') }))));
      }
      section.append(ul);
    }
    frag.append(section);
  });
  if (!shown) frag.append(h('p', { class: 'muted', text: 'Ningún pack coincide con ese nombre.' }));
  tree.replaceChildren(frag);
}

function packById(id) {
  return state.packs.find((p) => p.id === id);
}

// ---------------------------------------------------------------------------
// Kinds

function collectKinds(source) {
  if (!source) return;
  const values = Array.isArray(source) ? source : typeof source === 'object' ? Object.keys(source) : [];
  for (const v of values) {
    const k = typeof v === 'object' && v ? v.id || v.kind || v.name : v;
    if (k) state.kinds.add(String(k));
  }
}

function renderKindOptions() {
  const sel = $('#kindSelect');
  if (state.filters.kind) state.kinds.add(state.filters.kind);
  const kinds = [...state.kinds].sort((a, b) => kindLabel(a).localeCompare(kindLabel(b), 'es'));
  const existing = [...sel.options].slice(1).map((o) => o.value).join('|');
  if (existing !== kinds.join('|')) {
    sel.replaceChildren(h('option', { value: '', text: 'Todos los tipos' }), ...kinds.map((k) => h('option', { value: k, text: kindLabel(k) })));
  }
  sel.value = state.filters.kind;
}

// ---------------------------------------------------------------------------
// Asset list

async function loadList() {
  state.listAbort?.abort();
  const ctrl = new AbortController();
  state.listAbort = ctrl;
  const seq = ++state.listSeq;
  const f = state.filters;
  const params = new URLSearchParams();
  for (const k of ['q', 'family', 'pack', 'kind', 'state']) if (f[k]) params.set(k, f[k]);
  params.set('limit', String(PAGE_SIZE));
  params.set('offset', String(f.offset));
  state.list.loading = true;
  state.list.error = null;
  renderList();
  try {
    const { data } = await api(`/api/assets?${params}`, { signal: ctrl.signal });
    if (seq !== state.listSeq) return;
    const items = Array.isArray(data?.items) ? data.items : [];
    state.list = {
      items,
      total: Number(data?.total) || 0,
      offset: Number(data?.offset) || 0,
      limit: Number(data?.limit) || PAGE_SIZE,
      revision: data?.revision,
      loading: false,
      error: null,
    };
    for (const it of items) if (it.kind) state.kinds.add(String(it.kind));
    renderKindOptions();
    if (state.list.total > 0 && state.list.offset >= state.list.total) {
      const lastPage = Math.max(0, Math.ceil(state.list.total / PAGE_SIZE) - 1);
      setFilters({ offset: lastPage * PAGE_SIZE }, { keepOffset: true });
      return;
    }
  } catch (err) {
    if (err.name === 'AbortError') return;
    if (seq !== state.listSeq) return;
    state.list.loading = false;
    state.list.error = err.message;
  }
  renderList();
}

function assetStatus(a) {
  const draft = state.drafts.get(a.id);
  const status = a.status || (a.usages?.length ? 'used' : a.selected ? 'requested' : 'available');
  const info = status === 'used'
    ? { key: 'used', label: 'En uso' }
    : status === 'requested'
      ? { key: 'requested', label: 'Solicitado' }
      : { key: 'available', label: 'Sin seleccionar' };
  const extra = [];
  if (status === 'used' && a.selected === false) extra.push({ key: 'off', label: 'Deseleccionado' });
  if (a.available === false) extra.push({ key: 'missing', label: 'No disponible en disco' });
  if (isDirty(draft)) extra.push({ key: 'draft', label: 'Borrador sin guardar' });
  return { ...info, extra };
}

function statusBadges(a) {
  const s = assetStatus(a);
  return h('span', { class: 'badges' },
    h('span', { class: `badge ${s.key}`, text: s.label }),
    s.extra.map((e) => h('span', { class: `badge ${e.key}`, text: e.label })));
}

function thumbBlock(a, { large = false } = {}) {
  const wrap = h('span', { class: `thumb${large ? ' large' : ''}` });
  const placeholder = (text) => h('span', { class: 'thumb-empty' }, icon('cube', 'icon big'), h('span', { text }));
  if (a.thumbnailUrl) {
    const img = h('img', { src: a.thumbnailUrl, alt: a.thumbnailIsPack ? `Imagen del pack ${a.packName || ''}` : `Miniatura de ${a.name || 'asset'}`, loading: 'lazy', decoding: 'async' });
    img.addEventListener('error', () => {
      img.remove();
      wrap.classList.add('broken');
      wrap.prepend(placeholder('Miniatura no disponible'));
    }, { once: true });
    wrap.append(img);
    if (a.thumbnailIsPack) {
      wrap.classList.add('is-pack');
      wrap.append(h('span', { class: 'thumb-tag', text: 'Imagen del pack' }));
    }
  } else {
    wrap.append(placeholder('Sin vista previa'));
  }
  return wrap;
}

function renderList() {
  const host = $('#results');
  const L = state.list;
  const f = state.filters;
  host.setAttribute('aria-busy', String(!!L.loading));
  renderResultHead();

  if (L.error) {
    host.replaceChildren(h('div', { class: 'panel error-panel', role: 'alert' },
      icon('alert', 'icon big'),
      h('h2', { text: 'No se pudo cargar la lista de assets' }),
      h('p', { text: L.error }),
      h('button', { type: 'button', class: 'btn primary', onclick: loadList, text: 'Reintentar' })));
    $('#pager').hidden = true;
    return;
  }

  if (L.loading && !L.items.length) {
    host.replaceChildren(h('ul', { class: 'grid' }, Array.from({ length: 12 }, () => h('li', { class: 'card skeleton', 'aria-hidden': 'true' }))));
    $('#pager').hidden = true;
    return;
  }

  const s = state.status;
  if (!L.items.length) {
    const noIndex = s && Number(s.assets) === 0;
    const anyFilter = f.q || f.family || f.pack || f.kind || f.state;
    let panel;
    if (s?.indexing) {
      panel = h('div', { class: 'panel' }, h('h2', { text: 'Indexando la biblioteca' }),
        h('p', { text: 'Los resultados aparecerán cuando termine. El progreso se ve arriba.' }));
    } else if (noIndex) {
      panel = h('div', { class: 'panel' },
        icon('cube', 'icon big'),
        h('h2', { text: 'La biblioteca todavía no está indexada' }),
        h('p', { text: 'La indexación lee los paquetes de Synty y el repositorio en modo solo lectura para listar cada asset individual y detectar cuáles usa ya el juego. No mueve, convierte ni modifica los originales.' }),
        h('button', { type: 'button', class: 'btn primary', onclick: startReindex, text: 'Indexar biblioteca' }));
    } else if (anyFilter) {
      panel = h('div', { class: 'panel' }, h('h2', { text: 'Ningún asset coincide' }),
        h('p', { text: 'Prueba con otra búsqueda o quita algún filtro.' }),
        h('button', { type: 'button', class: 'btn', onclick: clearAllFilters, text: 'Quitar todos los filtros' }));
    } else {
      panel = h('div', { class: 'panel' }, h('h2', { text: 'Sin resultados' }));
    }
    host.replaceChildren(panel);
    $('#pager').hidden = true;
    return;
  }

  const grid = h('ul', { class: `grid${L.loading ? ' is-loading' : ''}` });
  for (const a of L.items) {
    const pack = a.packName || packById(a.packId)?.name || a.packId || '';
    grid.append(h('li', null, h('button', {
      type: 'button', class: 'card', 'data-id': a.id,
      onclick: () => openDetail(a.id),
    },
    thumbBlock(a),
    h('span', { class: 'card-body' },
      h('span', { class: 'card-name', text: a.name || a.id, title: a.name || a.id }),
      h('span', { class: 'card-meta' }, h('span', { class: 'kind', text: kindLabel(a.kind) }), h('span', { class: 'pack', text: pack, title: pack })),
      statusBadges(a)))));
  }
  host.replaceChildren(grid);
  renderPager();
}

function renderResultHead() {
  const L = state.list;
  const f = state.filters;
  const count = $('#resultCount');
  if (L.loading) count.textContent = 'Cargando assets…';
  else if (L.error) count.textContent = '';
  else if (!L.total) count.textContent = '0 assets';
  else {
    const from = L.offset + 1;
    const to = Math.min(L.offset + L.items.length, L.total);
    count.textContent = `${fmtInt(L.total)} assets · mostrando del ${fmtInt(from)} al ${fmtInt(to)}`;
  }

  const chips = $('#activeFilters');
  chips.replaceChildren();
  const chip = (label, onRemove) => chips.append(h('span', { class: 'chip' },
    h('span', { text: label }),
    h('button', { type: 'button', class: 'chip-x', title: `Quitar filtro: ${label}`, onclick: onRemove }, icon('close', 'icon tiny'), h('span', { class: 'sr-only', text: `Quitar filtro ${label}` }))));
  if (f.q) chip(`Búsqueda: ${f.q}`, () => { $('#searchInput').value = ''; setFilters({ q: '' }); });
  if (f.family) chip(`Familia: ${f.family}`, () => setFilters({ family: '' }));
  if (f.pack) {
    const p = packById(f.pack);
    chip(`Pack: ${p?.name || f.pack}`, () => setFilters({ pack: '' }));
  }
  if (f.kind) chip(`Tipo: ${kindLabel(f.kind)}`, () => setFilters({ kind: '' }));
  if (f.state) chip(`Estado: ${STATE_LABELS[f.state]}`, () => setFilters({ state: '' }));

  const banner = $('#packBanner');
  const p = f.pack ? packById(f.pack) : null;
  if (!p) { banner?.remove(); return; }
  const node = h('div', { class: 'pack-banner', id: 'packBanner' },
    p.thumbnailUrl ? h('figure', { class: 'pack-figure' },
      h('img', { src: p.thumbnailUrl, alt: `Imagen del pack ${p.name}`, loading: 'lazy', onerror: (e) => e.currentTarget.closest('figure')?.remove() }),
      h('figcaption', { text: 'Imagen del pack' })) : null,
    h('div', null,
      h('h2', { text: p.name || p.id }),
      h('p', { class: 'muted', text: [p.family, `${fmtInt(p.assetCount)} assets`, `${fmtInt(p.usedCount)} en uso`, p.available === false ? 'no disponible en disco' : ''].filter(Boolean).join(' · ') }),
      h('p', { class: 'hint', text: 'El pack es solo un filtro: para pedir algo hay que seleccionar cada asset por separado.' })));
  if (banner) banner.replaceWith(node); else $('.result-head').after(node);
}

function renderPager() {
  const L = state.list;
  const pager = $('#pager');
  const pages = Math.max(1, Math.ceil(L.total / PAGE_SIZE));
  const page = Math.floor(L.offset / PAGE_SIZE) + 1;
  pager.hidden = L.total <= PAGE_SIZE;
  $('#pFirst').disabled = page <= 1;
  $('#pPrev').disabled = page <= 1;
  $('#pNext').disabled = page >= pages;
  $('#pLast').disabled = page >= pages;
  const label = $('#pLabel');
  label.replaceChildren(
    h('label', { for: 'pInput', text: 'Página' }),
    h('input', {
      id: 'pInput', type: 'number', min: '1', max: String(pages), value: String(page), inputmode: 'numeric',
      onchange: (e) => goPage(parseInt(e.currentTarget.value, 10)),
    }),
    h('span', { text: `de ${fmtInt(pages)}` }));
}

function goPage(page) {
  const pages = Math.max(1, Math.ceil(state.list.total / PAGE_SIZE));
  const p = Math.min(pages, Math.max(1, page || 1));
  setFilters({ offset: (p - 1) * PAGE_SIZE }, { keepOffset: true });
  window.scrollTo({ top: 0 });
}

function clearAllFilters() {
  $('#searchInput').value = '';
  setFilters({ q: '', family: '', pack: '', kind: '', state: '' });
}

function syncFilterControls() {
  const f = state.filters;
  if (document.activeElement !== $('#searchInput')) $('#searchInput').value = f.q;
  for (const r of document.querySelectorAll('input[name="state"]')) r.checked = r.value === f.state;
  renderKindOptions();
}

function patchListItem(asset) {
  const i = state.list.items.findIndex((x) => x.id === asset.id);
  if (i >= 0) {
    state.list.items[i] = { ...state.list.items[i], ...asset };
    renderList();
  }
}

// ---------------------------------------------------------------------------
// Detail dialog

const dialog = () => $('#detail');
// Counts closes requested by the app; the 'close' event may arrive later.
let pendingAppCloses = 0;

async function openDetail(id) {
  if (state.detail && state.detail.id !== id) {
    const ok = await closeDetail();
    if (!ok) return;
  }
  const d = { id, asset: null, loading: true, error: null, saving: false, saveMsg: null, conflict: null, viewer: null, viewerSection: null, view: 'image' };
  state.detail = d;
  writeHash();
  const dlg = dialog();
  if (!dlg.open) dlg.showModal();
  renderDetail();
  await fetchDetail(d);
}

async function fetchDetail(d) {
  d.loading = true;
  d.error = null;
  renderDetail();
  try {
    const { data } = await api(`/api/assets/${encodeURIComponent(d.id)}`);
    if (state.detail !== d) return;
    d.asset = data;
    d.loading = false;
    ensureDraft(d.asset);
  } catch (err) {
    if (state.detail !== d) return;
    d.loading = false;
    d.error = err.status === 404 ? 'Este asset ya no está en el índice.' : err.message;
  }
  renderDetail();
}

function ensureDraft(asset) {
  const base = { selected: !!asset.selected, intent: asset.intent || '' };
  const existing = state.drafts.get(asset.id);
  if (existing && isDirty(existing)) {
    existing.serverChanged = existing.base.selected !== base.selected || existing.base.intent !== base.intent;
    existing.base = base;
    existing.revision = asset.revision;
    existing.name = asset.name;
    if (!isDirty(existing)) state.drafts.delete(asset.id);
  } else {
    state.drafts.set(asset.id, { ...base, base, revision: asset.revision, name: asset.name });
  }
  persistDrafts();
}

function cleanDraftIfClean(id) {
  const draft = state.drafts.get(id);
  if (draft && !isDirty(draft)) {
    state.drafts.delete(id);
    persistDrafts();
  }
}

function closeDetail() {
  const d = state.detail;
  if (!d) return Promise.resolve(true);
  // Escape can fire both 'cancel' and 'close'; share one close flow.
  if (!d.closing) d.closing = closeDetailFlow(d).finally(() => { d.closing = null; });
  return d.closing;
}

async function closeDetailFlow(d) {
  const draft = state.drafts.get(d.id);
  if (isDirty(draft) && !d.saving) {
    const choice = await askConfirm(draft.name || d.id);
    const reopen = () => {
      // The browser may have closed the modal on its own (Escape without user activation).
      const dlg = dialog();
      if (state.detail === d && !dlg.open) dlg.showModal();
      return false;
    };
    if (choice === 'stay') return reopen();
    if (choice === 'save') {
      const ok = await saveSelection();
      if (!ok) return reopen();
    }
    if (choice === 'discard') {
      state.drafts.delete(d.id);
      persistDrafts();
    }
  }
  if (d.saving) return false;
  cleanDraftIfClean(d.id);
  disposeViewer(d);
  state.detail = null;
  const dlg = dialog();
  if (dlg.open) {
    pendingAppCloses++;
    dlg.close();
  }
  writeHash();
  renderList();
  const card = document.querySelector(`.card[data-id="${CSS.escape(d.id)}"]`);
  card?.focus();
  return true;
}

function askConfirm(name) {
  const dlg = $('#confirm');
  $('#cText').textContent = `Has cambiado la selección o la nota de «${name}» y no está guardado en el servidor.`;
  return new Promise((resolve) => {
    // Resolve from the user action itself: the dialog 'close' event is queued
    // as a task and can be deferred, so it is only a fallback here.
    let done = false;
    const finish = (value) => {
      if (done) return;
      done = true;
      dlg.removeEventListener('click', onClick);
      dlg.removeEventListener('cancel', onCancel);
      dlg.removeEventListener('close', onClose);
      if (dlg.open) dlg.close(value);
      resolve(value);
    };
    const onClick = (e) => {
      const btn = e.target.closest('button[value]');
      if (btn) finish(btn.value);
    };
    const onCancel = (e) => {
      e.preventDefault();
      finish('stay');
    };
    const onClose = () => finish(dlg.returnValue || 'stay');
    dlg.returnValue = '';
    dlg.addEventListener('click', onClick);
    dlg.addEventListener('cancel', onCancel);
    dlg.addEventListener('close', onClose);
    dlg.showModal();
    dlg.querySelector('button[value="stay"]').focus();
  });
}

function disposeViewer(d) {
  if (d?.viewer) {
    d.viewer.dispose();
    d.viewer = null;
  }
}

function renderDetail() {
  const d = state.detail;
  if (!d) return;
  const body = $('#dBody');
  const a = d.asset;
  $('#dTitle').textContent = a?.name || (d.loading ? 'Cargando asset…' : 'Asset');
  $('#dKicker').textContent = a ? [a.family, a.packName || a.packId].filter(Boolean).join(' · ') : '';

  if (d.loading && !a) {
    body.replaceChildren(h('p', { class: 'muted pad', text: 'Cargando el detalle del asset…' }));
    return;
  }
  if (d.error && !a) {
    body.replaceChildren(h('div', { class: 'panel error-panel', role: 'alert' },
      h('p', { text: `No se pudo cargar el asset. ${d.error}` }),
      h('button', { type: 'button', class: 'btn primary', onclick: () => fetchDetail(d), text: 'Reintentar' })));
    return;
  }

  // Keep a live 3D viewer when only the form part changes.
  // Build the preview once per opened asset so form updates never reset it.
  if (!d.viewerSection) d.viewerSection = renderViewerSection(d);
  body.replaceChildren(
    d.viewerSection,
    h('div', { class: 'detail-cols' },
      renderInfoSection(a),
      renderSelectionSection(d),
      renderUsagesSection(a),
      renderLocatorSection(a)));
}

const AUTO_3D_KINDS = new Set(['modelo', 'prefab', 'personaje', 'animacion', 'material']);
const FIDELITY_CLASS = { resolved: 'ok', partial: 'warn', unavailable: 'bad' };

/** GET /api/assets/<id>/appearance, validated. Resolves null when the server has none. */
async function fetchAppearance(id) {
  try {
    const { data } = await api(`/api/assets/${encodeURIComponent(id)}/appearance`);
    return normalizeDescriptor(data, location.origin);
  } catch (err) {
    if (err.name === 'AbortError') throw err;
    if (err.status === 404) return null;
    throw err;
  }
}

function renderViewerSection(d) {
  const a = d.asset;
  const preview = a.preview && a.preview.url ? a.preview : null;
  const preview3d = preview && (preview.kind === 'glb' || preview.kind === 'fbx') ? preview : null;
  const section = h('section', { class: 'viewer-section', 'aria-label': 'Vista del asset' });
  const viewport = h('div', { class: 'viewport' });
  const caption = h('p', { class: 'viewport-caption' });
  const note = h('p', { class: 'viewer-note', role: 'status' });
  const tools = h('div', { class: 'viewer-tools' });
  const controls = h('div', { class: 'viewer-controls' });
  const fidelity = h('div', { class: 'viewer-fidelity', 'aria-live': 'polite' });
  // Descriptor state: undefined = not asked yet, null = none, object = normalized.
  let desc;
  let descError = '';

  const can3d = () => !!(preview3d || desc?.model || desc?.materials?.length);

  const renderFidelity = () => {
    fidelity.replaceChildren();
    if (d.view !== '3d' || !d.viewer?.ready) return;
    const s = d.viewer.summary();
    const head = h('p', { class: `fidelity-badge ${FIDELITY_CLASS[s.level] || 'bad'}` },
      h('strong', { text: FIDELITY_LABELS[s.level] || s.level }));
    fidelity.append(head);
    const lines = [...s.reasons, ...s.warnings.filter((w) => !s.reasons.includes(w))];
    if (lines.length) {
      const list = h('ul', { class: 'fidelity-list' });
      for (const line of lines.slice(0, 12)) list.append(h('li', { text: line }));
      if (lines.length > 12) list.append(h('li', { text: `Y ${lines.length - 12} avisos más.` }));
      fidelity.append(list);
    }
    fidelity.dataset.level = s.level;
  };

  const showImage = () => {
    disposeViewer(d);
    d.view = 'image';
    viewport.replaceChildren();
    controls.replaceChildren();
    fidelity.replaceChildren();
    note.textContent = '';
    let src = null;
    let label = '';
    if (preview && preview.kind === 'image') {
      src = preview.url;
      label = 'Vista previa del asset';
    } else if (a.thumbnailUrl) {
      src = a.thumbnailUrl;
      label = a.thumbnailIsPack ? 'Imagen del pack: no muestra este asset concreto' : 'Miniatura del asset';
    }
    if (src) {
      const img = h('img', { src, alt: label, decoding: 'async' });
      img.addEventListener('error', () => {
        viewport.replaceChildren(h('div', { class: 'viewport-empty' }, icon('image', 'icon big'), h('span', { text: 'La imagen no se pudo cargar.' })));
        caption.textContent = '';
      }, { once: true });
      viewport.append(img);
      if (a.thumbnailIsPack && src === a.thumbnailUrl) viewport.append(h('span', { class: 'thumb-tag', text: 'Imagen del pack' }));
      caption.textContent = label;
    } else {
      viewport.append(h('div', { class: 'viewport-empty' }, icon('cube', 'icon big'), h('span', { text: 'Este asset no tiene vista previa ni miniatura.' })));
      caption.textContent = '';
    }
    const notes = [];
    if (preview?.note) notes.push(preview.note);
    if (descError) notes.push(`No se pudo leer la apariencia: ${descError}`);
    note.textContent = notes.join(' ');
    renderTools();
  };

  const show3d = async () => {
    disposeViewer(d);
    d.view = '3d';
    fidelity.replaceChildren();
    controls.replaceChildren();
    viewport.replaceChildren(h('div', { class: 'viewport-empty' }, h('span', { class: 'spinner', 'aria-hidden': 'true' }), h('span', { text: 'Cargando el visor 3D…' })));
    note.textContent = '';
    const v = new ModelViewer(viewport, controls);
    d.viewer = v;
    renderTools();
    try {
      if (desc === undefined) {
        try {
          desc = await fetchAppearance(a.id);
        } catch (err) {
          desc = null;
          descError = err?.message || 'error desconocido';
        }
        if (d.viewer !== v) return;
      }
      if (!desc?.model && !desc?.materials?.length && !preview3d) {
        disposeViewer(d);
        showImage();
        return;
      }
      const target = desc?.model || (desc?.materials?.length ? null : preview3d);
      caption.textContent = target
        ? `Modelo ${String(target.kind).toUpperCase()}${target.name ? ` «${target.name}»` : ''}`
        : 'Material sobre una esfera de muestra';
      const useDesc = desc && (desc.model || desc.materials.length) ? desc : null;
      const fetchDescriptor = async (id) => (id === a.id ? desc : fetchAppearance(id));
      const result = await v.load({ desc: useDesc, preview: preview3d, asset: a, fetchDescriptor });
      if (!result || d.viewer !== v) return;
      const notes = [];
      if (preview?.note && !useDesc) notes.push(/[.!?]$/.test(preview.note.trim()) ? preview.note.trim() : `${preview.note.trim()}.`);
      if (descError) notes.push(`No se pudo leer la apariencia (${descError}): solo se ve lo que lleva el archivo.`);
      const modeText = { animation: 'Animación sobre el cuerpo de origen.', skeleton: 'Solo esqueleto.', sphere: 'Material sobre esfera.' }[result.mode];
      if (modeText) notes.push(modeText);
      notes.push(`${fmtInt(result.meshes)} ${result.meshes === 1 ? 'malla' : 'mallas'}, ${fmtInt(result.triangles)} triángulos${result.clips ? `, ${result.clips} ${result.clips === 1 ? 'clip' : 'clips'}` : ''}.`);
      note.textContent = notes.join(' ');
      v.onChange = () => { if (d.viewer === v) renderFidelity(); };
      renderFidelity();
      renderTools();
    } catch (err) {
      if (d.viewer !== v) return;
      disposeViewer(d);
      controls.replaceChildren();
      viewport.replaceChildren(h('div', { class: 'viewport-empty error' }, icon('alert', 'icon big'),
        h('span', { text: `No se pudo abrir el modelo. ${err?.message || ''}`.trim() })));
      d.view = 'error';
      renderTools();
    }
  };

  const renderTools = () => {
    tools.replaceChildren();
    if (d.view !== '3d' && (can3d() || desc === undefined && preview3d)) {
      tools.append(h('button', { type: 'button', class: 'btn small', onclick: show3d }, icon('cube'), h('span', { text: 'Ver en 3D' })));
    }
    if (d.view === '3d' || d.view === 'error') {
      tools.append(h('button', { type: 'button', class: 'btn small', onclick: showImage }, icon('image'), h('span', { text: 'Ver imagen' })));
    }
    if (d.view === '3d' && d.viewer?.ready) {
      tools.append(
        h('button', {
          type: 'button', class: 'btn small', 'aria-pressed': String(d.viewer.gridVisible),
          onclick: (e) => { d.viewer.setGrid(!d.viewer.gridVisible); e.currentTarget.setAttribute('aria-pressed', String(d.viewer.gridVisible)); },
        }, icon('grid'), h('span', { text: 'Rejilla' })),
        h('button', { type: 'button', class: 'btn small', onclick: () => d.viewer.fit() }, icon('frame'), h('span', { text: 'Encuadrar' })));
    }
    if (!preview && d.view === 'image' && !can3d()) {
      tools.append(h('span', { class: 'muted small', text: 'Sin vista previa individual para este asset.' }));
    }
  };

  section.append(h('figure', { class: 'viewport-wrap' }, viewport, h('figcaption', null, caption)), controls, tools, fidelity, note);
  showImage();
  // Models, prefabs, characters, animations and materials open in 3D directly.
  if (AUTO_3D_KINDS.has(String(a.kind || '').toLowerCase()) || preview3d) show3d();
  return section;
}

function renderInfoSection(a) {
  const s = assetStatus(a);
  const explain = s.key === 'used'
    ? 'El juego ya lo usa según la evidencia detectada. Viene seleccionado por defecto.'
    : s.key === 'requested'
      ? 'Está seleccionado para ClaudeCode, pero no hay evidencia de que el juego ya lo use.'
      : 'No se usa en el juego ni se ha pedido.';
  const pack = a.packName || packById(a.packId)?.name || a.packId || '';
  return h('section', { class: 'detail-section info' },
    h('h3', { text: 'Estado' }),
    statusBadges(a),
    h('p', { class: 'muted', text: explain }),
    h('dl', { class: 'kv' },
      h('dt', { text: 'Tipo' }), h('dd', { text: kindLabel(a.kind) }),
      h('dt', { text: 'Pack' }), h('dd', null,
        pack ? h('button', { type: 'button', class: 'link-btn', title: 'Filtrar la lista por este pack', onclick: () => filterFromDetail({ pack: a.packId, family: '' }), text: pack }) : '-'),
      h('dt', { text: 'Familia' }), h('dd', null,
        a.family ? h('button', { type: 'button', class: 'link-btn', title: 'Filtrar la lista por esta familia', onclick: () => filterFromDetail({ family: a.family, pack: '' }), text: a.family }) : '-'),
      h('dt', { text: 'En disco' }), h('dd', { text: a.available === false ? 'No disponible ahora' : 'Disponible' })));
}

async function filterFromDetail(patch) {
  const ok = await closeDetail();
  if (ok) setFilters(patch);
}

function renderUsagesSection(a) {
  const usages = Array.isArray(a.usages) ? a.usages : [];
  const section = h('section', { class: 'detail-section usages' }, h('h3', { text: `Usos detectados (${usages.length})` }));
  if (!usages.length) {
    section.append(h('p', { class: 'muted', text: a.status === 'used'
      ? 'El servidor lo marca en uso pero no ha enviado evidencia.'
      : 'No se ha detectado ningún uso en el repositorio.' }));
    return section;
  }
  const list = h('ol', { class: 'usage-list' });
  for (const u of usages) {
    const ev = Array.isArray(u.evidence) ? u.evidence : [];
    let runtime = null;
    if (u.runtime === true) runtime = 'Se carga al jugar';
    else if (u.runtime === false) runtime = 'Solo referencia, no se carga al jugar';
    else if (u.runtime) runtime = `Ejecución: ${u.runtime}`;
    list.append(h('li', null,
      h('p', { class: 'usage-purpose', text: u.purpose || 'Uso sin descripción' }),
      runtime ? h('p', { class: 'muted small', text: runtime }) : null,
      ev.length
        ? h('ul', { class: 'evidence' }, ev.map((e) => h('li', null,
          h('code', { text: `${e.path || '?'}${e.line ? `:${e.line}` : ''}` }),
          e.detail ? h('span', { class: 'ev-detail', text: e.detail }) : null)))
        : h('p', { class: 'muted small', text: 'Sin evidencia registrada.' })));
  }
  section.append(list);
  return section;
}

function renderLocatorSection(a) {
  const src = a.source || {};
  const rows = [
    ['Id', a.id], ['Ruta en el pack', a.path], ['Archivo del paquete', src.archive], ['Miembro', src.member],
    ['Ruta local', src.localPath], ['Formato', src.format], ['Revisión', a.revision],
  ].filter(([, v]) => v !== undefined && v !== null && v !== '');
  return h('details', { class: 'detail-section locator' },
    h('summary', { text: 'Localizador técnico' }),
    h('dl', { class: 'kv mono' }, rows.map(([k, v]) => [h('dt', { text: k }), h('dd', { text: String(v) })])),
    h('p', { class: 'muted small', text: 'Datos para que ClaudeCode encuentre el original. Los archivos se leen sin modificarlos.' }));
}

function renderSelectionSection(d) {
  const a = d.asset;
  const draft = state.drafts.get(a.id) || { selected: !!a.selected, intent: a.intent || '', base: { selected: !!a.selected, intent: a.intent || '' } };
  const dirty = isDirty(draft);
  const form = h('form', { class: 'detail-section selection', novalidate: true });
  const statusLine = h('p', { class: 'save-status', role: 'status', 'aria-live': 'polite' });

  const checkbox = h('input', { type: 'checkbox', id: 'selChk', checked: draft.selected, disabled: d.saving });
  const textarea = h('textarea', {
    id: 'selIntent', rows: '4', maxlength: '5000', disabled: d.saving,
    placeholder: 'Ejemplo: valla rota para el borde del pinar en la misión del asedio',
  });
  textarea.value = draft.intent;
  const saveBtn = h('button', { type: 'submit', class: 'btn primary', disabled: d.saving || !dirty }, icon('save'), h('span', { text: d.saving ? 'Guardando…' : 'Guardar' }));
  const discardBtn = h('button', { type: 'button', class: 'btn ghost', disabled: d.saving || !dirty, text: 'Descartar cambios' });

  const deselectHint = h('p', { class: 'hint warn-hint', text: 'Quitar la selección solo lo saca de la lista de pedidos. No borra nada del juego ni de los archivos.' });
  const updateLocal = () => {
    const cur = state.drafts.get(a.id) || { base: { selected: !!a.selected, intent: a.intent || '' }, revision: a.revision, name: a.name };
    cur.selected = checkbox.checked;
    cur.intent = textarea.value;
    cur.name = a.name;
    state.drafts.set(a.id, cur);
    persistDrafts();
    const isD = isDirty(cur);
    saveBtn.disabled = d.saving || !isD;
    discardBtn.disabled = d.saving || !isD;
    deselectHint.hidden = !(a.status === 'used' && !checkbox.checked);
    d.saveMsg = null;
    statusLine.className = 'save-status';
    statusLine.textContent = isD ? 'Cambios sin guardar.' : '';
  };
  checkbox.addEventListener('change', updateLocal);
  textarea.addEventListener('input', updateLocal);
  discardBtn.addEventListener('click', () => {
    state.drafts.set(a.id, { ...draft.base, base: draft.base, revision: a.revision, name: a.name });
    persistDrafts();
    d.conflict = null;
    renderDetail();
  });
  form.addEventListener('submit', (e) => { e.preventDefault(); saveSelection(); });

  deselectHint.hidden = !(a.status === 'used' && !draft.selected);

  form.append(
    h('h3', { text: 'Selección' }),
    h('label', { class: 'check' }, checkbox, h('span', { text: 'Seleccionado' })),
    deselectHint,
    h('label', { class: 'field-label', for: 'selIntent', text: 'Para qué quiero usarlo' }),
    textarea,
    h('p', { class: 'hint', text: 'La nota va al servidor y sale en la exportación para ClaudeCode. Seleccionar no lo integra en el juego.' }));

  if (draft.serverChanged && dirty) {
    form.append(h('p', { class: 'hint warn-hint', text: 'Tienes un borrador anterior de este asset y el servidor ha cambiado desde entonces. Revísalo antes de guardar.' }));
  }

  if (d.conflict?.blocked) {
    const c = d.conflict;
    form.append(h('div', { class: 'conflict', role: 'alert' },
      h('p', { class: 'conflict-title', text: c.known ? 'Selección bloqueada: el servidor no acepta cambios ahora.' : 'El servidor rechazó el cambio (409).' }),
      c.message ? h('p', { class: 'prewrap', text: c.message }) : null,
      h('p', { text: 'Tu borrador sigue en el formulario y no se ha recargado nada. Cuando se resuelva, pulsa Guardar otra vez.' })));
  } else if (d.conflict) {
    const c = d.conflict;
    form.append(h('div', { class: 'conflict', role: 'alert' },
      h('p', { class: 'conflict-title', text: 'Conflicto: otro cambio se guardó antes que el tuyo.' }),
      c.message ? h('p', { class: 'prewrap', text: c.message }) : null,
      h('p', { text: 'Se ha recargado la versión del servidor y tu borrador sigue en el formulario. Revisa y pulsa Guardar para sobrescribir.' }),
      c.reloadError
        ? h('p', { class: 'error-text', text: `No se pudo recargar el asset: ${c.reloadError}` })
        : h('dl', { class: 'kv' },
          h('dt', { text: 'En el servidor' }), h('dd', { text: c.serverSelected ? 'Seleccionado' : 'Sin seleccionar' }),
          h('dt', { text: 'Nota del servidor' }), h('dd', { class: 'prewrap', text: c.serverIntent || '(vacía)' })),
      c.reloadError ? h('button', { type: 'button', class: 'btn small', onclick: () => reloadAfterConflict(d, null, c.message), text: 'Reintentar recarga' }) : null));
  }

  if (d.saveMsg) {
    statusLine.className = `save-status ${d.saveMsg.kind}`;
    statusLine.textContent = d.saveMsg.text;
  } else if (dirty) {
    statusLine.textContent = 'Cambios sin guardar.';
  }

  form.append(h('div', { class: 'form-actions' }, saveBtn, discardBtn), statusLine);
  if (d.saveMsg?.kind === 'error' && !d.saving) {
    form.querySelector('.form-actions').append(h('button', { type: 'button', class: 'btn', onclick: () => saveSelection(), text: 'Reintentar' }));
  }
  return form;
}

async function saveSelection() {
  const d = state.detail;
  if (!d?.asset || d.saving) return false;
  const a = d.asset;
  const draft = state.drafts.get(a.id);
  if (!draft) return true;
  d.saving = true;
  d.saveMsg = { kind: 'info', text: 'Guardando en el servidor…' };
  renderDetail();
  try {
    const { data } = await api(`/api/selections/${encodeURIComponent(a.id)}`, {
      method: 'PUT',
      body: { selected: draft.selected, intent: draft.intent, revision: a.revision },
    });
    if (state.detail !== d) return true;
    let saved = data?.asset;
    if (!saved) {
      const res = await api(`/api/assets/${encodeURIComponent(a.id)}`);
      saved = res.data;
    }
    d.asset = { ...a, ...saved };
    state.drafts.delete(a.id);
    persistDrafts();
    ensureDraft(d.asset);
    d.conflict = null;
    d.saving = false;
    const ok = !!d.asset.selected === draft.selected && (d.asset.intent || '').trim() === draft.intent.trim();
    d.saveMsg = ok
      ? { kind: 'ok', text: `Guardado en el servidor a las ${fmtTime()}.` }
      : { kind: 'warn', text: 'El servidor respondió, pero lo guardado no coincide con lo enviado. Revisa el formulario.' };
    patchListItem(d.asset);
    renderDetail();
    loadStatus();
    return true;
  } catch (err) {
    d.saving = false;
    if (state.detail !== d) return false;
    if (err.status === 409) {
      const kind = await classifyConflict(err.data);
      if (state.detail !== d) return false;
      if (kind !== 'revision') {
        // Blocked store (broken JSON) or unknown: keep the draft and its base, no reload.
        d.conflict = { blocked: true, known: kind === 'blocked', message: err.message };
        d.saveMsg = { kind: 'error', text: kind === 'blocked' ? 'No guardado: la selección está bloqueada.' : `No guardado. ${err.message}` };
        renderDetail();
        return false;
      }
      d.saveMsg = { kind: 'error', text: 'No guardado: hay una versión más reciente en el servidor.' };
      await reloadAfterConflict(d, err.data, err.message);
      return false;
    }
    d.saveMsg = { kind: 'error', text: `No guardado. ${err.message}` };
    renderDetail();
    return false;
  }
}

// A revision conflict carries `current`; other 409s are told apart through
// status.selections.broken (selecciones.json edited by hand and invalid).
async function classifyConflict(data) {
  if (data && typeof data === 'object' && 'current' in data) return 'revision';
  const ok = await loadStatus();
  if (!ok) return 'unknown';
  return state.status?.selections?.broken ? 'blocked' : 'revision';
}

async function reloadAfterConflict(d, conflictData, message = '') {
  d.conflict = { reloadError: null, message };
  try {
    let fresh = conflictData?.asset;
    if (!fresh || fresh.id !== d.id) {
      const res = await api(`/api/assets/${encodeURIComponent(d.id)}`);
      fresh = res.data;
    }
    if (state.detail !== d) return;
    d.asset = fresh;
    const draft = state.drafts.get(d.id);
    if (draft) {
      draft.base = { selected: !!fresh.selected, intent: fresh.intent || '' };
      draft.revision = fresh.revision;
      persistDrafts();
    }
    d.conflict = { serverSelected: !!fresh.selected, serverIntent: fresh.intent || '', message };
    patchListItem(fresh);
  } catch (err) {
    d.conflict = { reloadError: err.message, message };
  }
  renderDetail();
}

// ---------------------------------------------------------------------------
// Sidebar on narrow screens

const narrow = () => window.matchMedia('(max-width: 899px)').matches;

function openSidebar() {
  document.body.classList.add('sidebar-open');
  $('#scrim').hidden = false;
  $('#toggleSidebar').setAttribute('aria-expanded', 'true');
  $('#closeSidebar').focus();
}

function closeSidebar() {
  document.body.classList.remove('sidebar-open');
  $('#scrim').hidden = true;
  $('#toggleSidebar').setAttribute('aria-expanded', 'false');
}

function closeSidebarIfNarrow() {
  if (narrow() && document.body.classList.contains('sidebar-open')) {
    closeSidebar();
    $('#toggleSidebar').focus();
  }
}

// ---------------------------------------------------------------------------
// Wiring

function debounce(fn, ms) {
  let t = 0;
  return (...args) => {
    clearTimeout(t);
    t = setTimeout(() => fn(...args), ms);
  };
}

function bind() {
  const search = $('#searchInput');
  const onSearch = debounce(() => {
    const q = search.value.trim();
    if (q !== state.filters.q) setFilters({ q });
  }, 300);
  search.addEventListener('input', onSearch);
  $('#searchForm').addEventListener('submit', (e) => {
    e.preventDefault();
    setFilters({ q: search.value.trim() });
  });

  for (const r of document.querySelectorAll('input[name="state"]')) {
    r.addEventListener('change', () => { if (r.checked) setFilters({ state: r.value }); });
  }
  $('#kindSelect').addEventListener('change', (e) => setFilters({ kind: e.currentTarget.value }));
  $('#packSearch').addEventListener('input', debounce(renderLibraryTree, 120));
  $('#clearLibrary').addEventListener('click', () => setFilters({ family: '', pack: '' }));

  $('#pFirst').addEventListener('click', () => goPage(1));
  $('#pPrev').addEventListener('click', () => goPage(Math.floor(state.list.offset / PAGE_SIZE)));
  $('#pNext').addEventListener('click', () => goPage(Math.floor(state.list.offset / PAGE_SIZE) + 2));
  $('#pLast').addEventListener('click', () => goPage(Math.ceil(state.list.total / PAGE_SIZE)));

  $('#reindexBtn').addEventListener('click', startReindex);
  $('#exportLink').addEventListener('click', downloadExport);

  $('#toggleSidebar').addEventListener('click', openSidebar);
  $('#closeSidebar').addEventListener('click', () => { closeSidebar(); $('#toggleSidebar').focus(); });
  $('#scrim').addEventListener('click', closeSidebar);
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && document.body.classList.contains('sidebar-open') && !dialog().open) closeSidebarIfNarrow();
  });

  const dlg = dialog();
  $('#dClose').addEventListener('click', () => closeDetail());
  dlg.addEventListener('cancel', (e) => {
    e.preventDefault();
    closeDetail();
  });
  dlg.addEventListener('click', (e) => {
    if (e.target === dlg) closeDetail();
  });
  // Browsers may close a modal on Escape without a cancel event. Run the
  // normal close flow anyway: it asks about drafts and reopens on 'stay'.
  dlg.addEventListener('close', () => {
    if (pendingAppCloses > 0) {
      pendingAppCloses--;
      return;
    }
    if (!state.detail || dlg.open || state.detail.closing) return;
    closeDetail();
  });

  window.addEventListener('hashchange', () => {
    const assetId = readHash();
    syncFilterControls();
    renderLibraryTree();
    loadList();
    if (assetId && assetId !== state.detail?.id) openDetail(assetId);
  });

  window.addEventListener('beforeunload', (e) => {
    for (const draft of state.drafts.values()) {
      if (isDirty(draft)) {
        e.preventDefault();
        e.returnValue = '';
        return;
      }
    }
  });
}

async function init() {
  const assetId = readHash();
  bind();
  syncFilterControls();
  renderList();
  await Promise.all([loadStatus(), loadPacks()]);
  await loadList();
  if (assetId) openDetail(assetId);
}

init();
