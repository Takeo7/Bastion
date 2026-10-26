// Appearance descriptor helpers for the asset viewer. Pure logic: no three.js
// and no DOM, so the rules can be tested in Node.
//
// Contract (GET /api/assets/<id>/appearance):
//   { model: {id, kind: 'fbx'|'glb', url} | null,
//     textures: [{id, name, path, url}],
//     materials: [{name, map?, normalMap?, emissiveMap?, color?, emissive?, transparent?, opacity?,
//                  alphaTest?, doubleSided?, metalness?, roughness?, meshes?, slot?, modelId?}],
//     visibleNodes?: [string], assetKind?: string,
//     parts?: [{model, visibleNodes?, materials?: [name], parentName?, local?: {Position, Rotation, Scale}}
//              | {prefab: {id, name}}],
//     animationModel?: {id, kind, url, name},
//     warnings: [], fidelity: 'resolved'|'partial'|'unavailable' }

export const FIDELITY_LEVELS = ['unavailable', 'partial', 'resolved'];
export const FIDELITY_LABELS = {
  resolved: 'Apariencia fiel',
  partial: 'Apariencia parcial',
  unavailable: 'Apariencia no disponible',
};

const MODEL_KINDS = new Set(['fbx', 'glb']);
// Formats a browser (plus TGALoader) can decode. PSD, TIFF, EXR and DDS are not.
export const DECODABLE_EXT = new Set(['png', 'jpg', 'jpeg', 'webp', 'gif', 'bmp', 'tga']);
const MAX_ITEMS = 2000;
const rank = (f) => Math.max(0, FIDELITY_LEVELS.indexOf(f));
const MAX_TEXT = 500;

const str = (v, max = MAX_TEXT) => (typeof v === 'string' ? v.slice(0, max) : typeof v === 'number' ? String(v) : '');

/**
 * Returns the same-origin path+query of `raw` when it points at this server's
 * API, or null. Anything else (other hosts, file:, javascript:, credentials,
 * paths outside /api/) is rejected, so a descriptor can never make the viewer
 * reach outside the local library.
 */
export function safeApiUrl(raw, origin) {
  if (typeof raw !== 'string' || !raw || raw.length > 2048) return null;
  let u;
  try {
    u = new URL(raw, origin);
  } catch {
    return null;
  }
  if (u.origin !== origin || (u.protocol !== 'http:' && u.protocol !== 'https:')) return null;
  if (u.username || u.password) return null;
  if (!u.pathname.startsWith('/api/') || /(^|\/)\.\.?(\/|$)/.test(u.pathname)) return null;
  return u.pathname + u.search;
}

function issueText(issue) {
  if (issue === null || issue === undefined) return '';
  if (typeof issue === 'string') return issue.slice(0, MAX_TEXT);
  if (typeof issue === 'object') {
    const main = issue.message || issue.detail || issue.error || issue.code || '';
    const where = issue.path || issue.name || '';
    return [str(main), str(where)].filter(Boolean).join(': ');
  }
  return String(issue).slice(0, MAX_TEXT);
}

function unit(v) {
  const n = Number(v);
  return Number.isFinite(n) ? Math.min(1, Math.max(0, n)) : undefined;
}

function rgb(v) {
  if (!Array.isArray(v) || v.length < 3) return undefined;
  const c = v.slice(0, 3).map(Number);
  if (!c.every(Number.isFinite)) return undefined;
  // Accept 0..1 floats (Unity .mat) and 0..255 bytes.
  const scale = c.some((x) => x > 1) ? 255 : 1;
  return c.map((x) => Math.min(1, Math.max(0, x / scale)));
}

function modelRef(raw, origin, label, warnings) {
  if (!raw || typeof raw !== 'object') return null;
  const kind = str(raw.kind, 8).toLowerCase();
  const url = safeApiUrl(raw.url, origin);
  if (!MODEL_KINDS.has(kind)) {
    warnings.push(`${label}: formato «${kind || '?'}» no admitido por el visor.`);
    return null;
  }
  if (!url) {
    warnings.push(`${label}: se ignora una URL fuera del servidor local.`);
    return null;
  }
  return { id: str(raw.id, 200), kind, url };
}

function strList(v, max = MAX_ITEMS) {
  return (Array.isArray(v) ? v : []).slice(0, max).map((x) => str(x, 300)).filter(Boolean);
}

function numList(v, n) {
  if (Array.isArray(v)) {
    const a = v.slice(0, n).map(Number);
    return a.length === n && a.every(Number.isFinite) ? a : undefined;
  }
  if (v && typeof v === 'object') {
    const a = (n === 4 ? [v.x, v.y, v.z, v.w] : [v.x, v.y, v.z]).map(Number);
    return a.every(Number.isFinite) ? a : undefined;
  }
  return undefined;
}

/** Unity local transform; the server may send Position/Rotation/Scale or lower case keys. */
function localTransform(raw) {
  if (!raw || typeof raw !== 'object') return undefined;
  const get = (k) => raw[k] ?? raw[k.toLowerCase()] ?? raw[`m_Local${k}`];
  const out = {};
  const pos = numList(get('Position'), 3);
  const rot = numList(get('Rotation'), 4) || numList(get('Rotation'), 3);
  const scl = numList(get('Scale'), 3);
  if (pos) out.position = pos;
  if (rot) out.rotation = rot;
  if (scl) out.scale = scl;
  return Object.keys(out).length ? out : undefined;
}

function material(m, origin, counter) {
  const out = { name: str(m.name, 300) };
  for (const slot of ['map', 'normalMap', 'emissiveMap']) {
    if (m[slot] === undefined || m[slot] === null || m[slot] === '') continue;
    const url = safeApiUrl(m[slot], origin);
    if (url) out[slot] = url; else counter.rejected++;
  }
  const color = rgb(m.color);
  if (color) out.color = color;
  const emissive = rgb(m.emissive);
  if (emissive) out.emissive = emissive;
  if (typeof m.transparent === 'boolean') out.transparent = m.transparent;
  if (typeof m.doubleSided === 'boolean') out.doubleSided = m.doubleSided;
  for (const k of ['opacity', 'metalness', 'roughness', 'alphaTest']) {
    const v = unit(m[k]);
    if (v !== undefined) out[k] = v;
  }
  const meshes = strList(m.meshes, 200);
  if (meshes.length) out.meshes = meshes;
  const slot = Number(m.slot);
  if (m.slot !== undefined && m.slot !== null && Number.isInteger(slot) && slot >= 0 && slot < 256) out.slot = slot;
  if (str(m.modelId, 200)) out.modelId = str(m.modelId, 200);
  if (str(m.unityMaterial, 300)) out.unityMaterial = str(m.unityMaterial, 300);
  if (str(m.shader, 200)) out.shader = str(m.shader, 200);
  if (m.extraMaps && typeof m.extraMaps === 'object') {
    const extra = Object.keys(m.extraMaps).slice(0, 20).map((k) => str(k, 100)).filter(Boolean);
    if (extra.length) out.extraMaps = extra;
  }
  return out;
}

/**
 * Validates a raw descriptor. Never throws: invalid parts are dropped and
 * explained in `clientWarnings` (Spanish, user facing). Anything that makes
 * the client show less than the server described (a part without model, an
 * external URL) is listed in `degraded` and lowers the fidelity to partial.
 */
export function normalizeDescriptor(raw, origin) {
  const clientWarnings = [];
  const degraded = [];
  const d = raw && typeof raw === 'object' ? raw : {};
  const model = modelRef(d.model, origin, 'Modelo', clientWarnings);
  if (model && str(d.model.name, 300)) model.name = str(d.model.name, 300);
  const animationModel = modelRef(d.animationModel, origin, 'Modelo para la animación', clientWarnings);
  if (animationModel && str(d.animationModel.name, 300)) animationModel.name = str(d.animationModel.name, 300);
  if (d.model && !model) degraded.push('El modelo del descriptor no se puede abrir en el visor.');

  const textures = [];
  let rejected = 0;
  for (const t of (Array.isArray(d.textures) ? d.textures : []).slice(0, MAX_ITEMS)) {
    if (!t || typeof t !== 'object') continue;
    const url = safeApiUrl(t.url, origin);
    if (!url) { rejected++; continue; }
    const tex = { id: str(t.id, 200), name: str(t.name, 300), path: str(t.path, 1000), url };
    const fmt = str(t.format, 10).toLowerCase().replace(/^\./, '');
    if (/^[a-z0-9]{1,8}$/.test(fmt)) tex.format = fmt;
    textures.push(tex);
  }
  if (rejected) clientWarnings.push(`Se ignoran ${rejected} textura${rejected === 1 ? '' : 's'} con URL fuera del servidor local.`);

  const materials = [];
  const counter = { rejected: 0 };
  for (const m of (Array.isArray(d.materials) ? d.materials : []).slice(0, MAX_ITEMS)) {
    if (!m || typeof m !== 'object' || !str(m.name, 300)) continue;
    materials.push(material(m, origin, counter));
  }
  const shaderExtras = [...new Set(materials.flatMap((m) => m.extraMaps || []))];
  if (shaderExtras.length) degraded.push(`El visor no reproduce mapas propios del shader (${shaderExtras.slice(0, 4).join(', ')}).`);
  if (counter.rejected) {
    clientWarnings.push(`Se ignoran ${counter.rejected} mapa${counter.rejected === 1 ? '' : 's'} de material con URL fuera del servidor local.`);
    degraded.push('Faltan mapas de material.');
  }

  const visibleNodes = strList(d.visibleNodes, 500);

  const parts = [];
  for (const p of (Array.isArray(d.parts) ? d.parts : []).slice(0, 200)) {
    if (!p || typeof p !== 'object') continue;
    if (!p.model) {
      const name = str(p.prefab?.name, 300) || str(p.name, 300) || 'sin nombre';
      const msg = `La pieza «${name}» es un prefab anidado y el visor no la monta: falta en la vista.`;
      clientWarnings.push(msg);
      degraded.push(msg);
      continue;
    }
    const ref = modelRef(p.model, origin, 'Pieza del prefab', clientWarnings);
    if (!ref) { degraded.push('Una pieza del prefab no se puede abrir.'); continue; }
    if (str(p.model.name, 300)) ref.name = str(p.model.name, 300);
    const part = { model: ref };
    const vis = strList(p.visibleNodes, 500);
    if (vis.length) part.visibleNodes = vis;
    // parts[].materials are binding NAMES; the real bindings live in materials[] with modelId.
    const names = strList(p.materials, 200);
    if (names.length) part.materialNames = names;
    if (str(p.parentName, 300)) part.parentName = str(p.parentName, 300);
    const local = localTransform(p.local);
    if (local) part.local = local;
    parts.push(part);
  }

  const warnings = (Array.isArray(d.warnings) ? d.warnings : []).slice(0, 50).map(issueText).filter(Boolean);
  const serverFidelity = FIDELITY_LEVELS.includes(d.fidelity) ? d.fidelity : 'unavailable';
  let fidelity = serverFidelity;
  if (!model && !materials.length) fidelity = 'unavailable';
  if (degraded.length && rank(fidelity) > rank('partial')) fidelity = 'partial';
  const assetKind = str(d.assetKind, 40).toLowerCase();
  return { model, animationModel, textures, materials, visibleNodes, parts, assetKind, warnings, fidelity, serverFidelity, degraded, clientWarnings };
}

/**
 * Bindings for one model of the descriptor. A binding without modelId belongs
 * to the main model; bindings tagged with another modelId are never applied to it.
 */
export function materialsForModel(materials, modelId, mainId) {
  const isMain = !modelId || modelId === mainId;
  return (materials || []).filter((m) => (m.modelId ? m.modelId === modelId : isMain));
}

// ---------------------------------------------------------------------------
// Texture resolution: a texture request from a model (FBX RelativeFilename,
// glTF uri) is matched against the descriptor by path suffix, then exact file
// name, then file stem (same name, other extension). Only a UNIQUE match is
// used; ambiguous requests stay unresolved and are reported.

export function normPath(p) {
  let s = String(p || '').replace(/\\/g, '/').split(/[?#]/)[0].trim().toLowerCase();
  s = s.replace(/\/{2,}/g, '/');
  const out = [];
  for (const seg of s.split('/')) {
    if (!seg || seg === '.') continue;
    if (seg === '..') { out.pop(); continue; }
    out.push(seg);
  }
  return out.join('/');
}

export const baseName = (p) => normPath(p).split('/').pop() || '';
export function extOf(p) {
  const b = baseName(p);
  const i = b.lastIndexOf('.');
  return i > 0 ? b.slice(i + 1) : '';
}
export function stemOf(p) {
  const b = baseName(p);
  const i = b.lastIndexOf('.');
  return i > 0 ? b.slice(0, i) : b;
}

function uniqueByUrl(list) {
  const seen = new Map();
  for (const t of list) if (!seen.has(t.url)) seen.set(t.url, t);
  return [...seen.values()];
}

export class TextureIndex {
  constructor(textures) {
    this.entries = (textures || []).map((t) => ({ ...t, key: normPath(t.path || t.name) }));
    this.byName = new Map();
    this.byStem = new Map();
    for (const e of this.entries) {
      for (const [map, k] of [[this.byName, baseName(e.path || e.name)], [this.byStem, stemOf(e.path || e.name)]]) {
        if (!k) continue;
        if (!map.has(k)) map.set(k, []);
        map.get(k).push(e);
      }
      // A texture may also be known by its display name.
      if (e.name && baseName(e.name) !== baseName(e.path || e.name)) {
        const k = baseName(e.name);
        if (!this.byName.has(k)) this.byName.set(k, []);
        this.byName.get(k).push(e);
      }
    }
  }

  get size() { return this.entries.length; }

  /** @returns {{status: 'resolved'|'ambiguous'|'missing', texture?, via?, candidates?: string[]}} */
  resolve(request) {
    const req = normPath(request);
    if (!req) return { status: 'missing' };
    const pick = (list, via) => {
      const u = uniqueByUrl(list);
      if (u.length === 1) return { status: 'resolved', texture: u[0], via };
      if (u.length > 1) return { status: 'ambiguous', via, candidates: u.map((t) => t.path || t.name) };
      return null;
    };
    if (req.includes('/')) {
      const r = pick(this.entries.filter((e) => e.key === req || e.key.endsWith(`/${req}`) || req.endsWith(`/${e.key}`)), 'path');
      if (r) return r;
    }
    const name = baseName(req);
    const r1 = pick(this.byName.get(name) || [], 'name');
    if (r1) return r1;
    const r2 = pick(this.byStem.get(stemOf(req)) || [], 'stem');
    if (r2) return r2;
    return { status: 'missing' };
  }
}

// ---------------------------------------------------------------------------
// Material binding by name. Exact name first, then case-insensitive with
// Unity/Blender suffixes removed. Never "first material wins": the only
// fallback is one descriptor material for one model material, reported apart.

export function materialKey(name) {
  return String(name || '').toLowerCase()
    .replace(/\s*\(instance\)\s*/g, '')
    .replace(/\.\d{3}$/, '')
    .trim();
}

export class MaterialBinder {
  constructor(materials) {
    this.materials = materials || [];
    this.exact = new Map();
    this.loose = new Map();
    for (const m of this.materials) {
      if (!this.exact.has(m.name)) this.exact.set(m.name, []);
      this.exact.get(m.name).push(m);
      const k = materialKey(m.name);
      if (!this.loose.has(k)) this.loose.set(k, []);
      this.loose.get(k).push(m);
    }
  }

  /**
   * @param {string} name material name in the model
   * @param {number} modelMaterialCount distinct material names in the model
   */
  match(name, modelMaterialCount) {
    const ex = this.exact.get(name) || [];
    if (ex.length === 1) return { status: 'bound', material: ex[0], via: 'exact' };
    if (ex.length > 1) return { status: 'ambiguous' };
    const lo = this.loose.get(materialKey(name)) || [];
    if (lo.length === 1) return { status: 'bound', material: lo[0], via: 'nocase' };
    if (lo.length > 1) return { status: 'ambiguous' };
    if (this.materials.length === 1 && modelMaterialCount === 1) return { status: 'bound', material: this.materials[0], via: 'single' };
    return { status: 'missing' };
  }
}

// ---------------------------------------------------------------------------
// Fidelity as seen by the client. It never claims more than the server.

export function summarizeFidelity({ server = 'unavailable', modelLoaded = false, synthetic = false, textures = {}, materials = {}, degraded = [] } = {}) {
  const reasons = [];
  if (!modelLoaded) return { level: 'unavailable', reasons: ['No se ha podido mostrar el modelo.'] };
  let level = server;
  const n = (k) => (Array.isArray(textures[k]) ? textures[k].length : Number(textures[k]) || 0);
  const bad = n('missing') + n('ambiguous') + n('failed') + n('denied') + n('undecodable');
  if (synthetic) reasons.push('El servidor no ha dado el descriptor de apariencia: solo se ve lo que lleva el archivo.');
  if (bad > 0) reasons.push(`${bad} textura${bad === 1 ? '' : 's'} sin resolver o sin poder cargar.`);
  if ((materials.unbound || 0) > 0) reasons.push(`${materials.unbound} material${materials.unbound === 1 ? '' : 'es'} sin enlace con el descriptor.`);
  if ((materials.single || 0) > 0) reasons.push('Material asignado por ser el único, no por nombre.');
  for (const r of degraded) if (r && !reasons.includes(r)) reasons.push(r);
  if (bad > 0 || (materials.unbound || 0) > 0 || (materials.single || 0) > 0 || synthetic || degraded.length) {
    if (rank(level) > rank('partial')) level = 'partial';
  }
  if (server !== 'resolved' && !synthetic) reasons.push(`El servidor declara la apariencia como «${FIDELITY_LABELS[server] || server}».`);
  return { level, reasons };
}
