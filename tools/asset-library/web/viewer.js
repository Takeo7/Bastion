// 3D viewer for the asset library. It opens the model named by the appearance
// descriptor, binds the real Unity materials per mesh and slot, honours the
// prefab's visible nodes and plays the embedded (or companion) animation clips.
// Nothing outside this server's /api/ is ever fetched.

import { TextureIndex, MaterialBinder, summarizeFidelity, materialsForModel, DECODABLE_EXT, extOf, materialKey } from './appearance.js';

const PLACEHOLDER_PNG = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGPYsmMvAARNAiroIjwVAAAAAElFTkSuQmCC';
const NEUTRAL_RGB = [180, 184, 189];
const TEXTURE_SLOTS = ['map', 'normalMap', 'bumpMap', 'specularMap', 'emissiveMap', 'alphaMap', 'aoMap', 'lightMap', 'roughnessMap', 'metalnessMap', 'displacementMap', 'envMap'];
const IMAGE_RE = /\.(png|jpe?g|tga|tiff?|bmp|gif|webp|psd|dds|exr|hdr|ktx2?)$/i;
const MAX_PARTS = 24;
const OVERLAP_BODIES = 4;
const SPEEDS = [0.25, 0.5, 1, 1.5, 2];

const nameKey = (s) => materialKey(s);
const plural = (n, one, many) => `${n} ${n === 1 ? one : many}`;
const fmtSec = (s) => `${(Math.round(s * 100) / 100).toFixed(2).replace('.', ',')} s`;

export function disposeObject(obj) {
  obj.traverse((o) => {
    o.geometry?.dispose?.();
    const mats = Array.isArray(o.material) ? o.material : o.material ? [o.material] : [];
    for (const m of mats) {
      for (const slot of TEXTURE_SLOTS) m[slot]?.dispose?.();
      m.dispose?.();
    }
    if (o.isSkinnedMesh) o.skeleton?.dispose?.();
  });
}

function meshesOf(root) {
  const out = [];
  root.traverse((o) => { if (o.isMesh) out.push(o); });
  return out;
}

function vec(v, n) {
  if (Array.isArray(v)) return v.length >= n ? v.slice(0, n).map(Number) : null;
  if (v && typeof v === 'object') {
    const a = n === 4 ? [v.x, v.y, v.z, v.w] : [v.x, v.y, v.z];
    return a.every((x) => Number.isFinite(Number(x))) ? a.map(Number) : null;
  }
  return null;
}

/**
 * Picks the descriptor material for one mesh slot: by mesh name (+ slot,
 * + material name) when the descriptor says which meshes it covers, otherwise
 * by material name. Exported for tests.
 */
export function pickMaterial(materials, binder, meshName, slot, matName, modelMaterialCount) {
  const mk = nameKey(meshName);
  const tagged = materials.filter((m) => Array.isArray(m.meshes) && m.meshes.some((n) => nameKey(n) === mk));
  if (tagged.length) {
    let c = tagged.filter((m) => m.slot === undefined || m.slot === slot);
    if (c.length > 1 && matName) {
      const byName = c.filter((m) => nameKey(m.name) === nameKey(matName));
      if (byName.length) c = byName;
    }
    if (c.length === 1) return { status: 'bound', material: c[0], via: 'mesh' };
    return { status: c.length ? 'ambiguous' : 'missing' };
  }
  return binder.match(matName || '', modelMaterialCount);
}

export class ModelViewer {
  constructor(host, controlsHost = null) {
    this.host = host;
    this.controlsHost = controlsHost;
    this.disposed = false;
    this.ready = false;
    this.gridVisible = true;
    this.onChange = null;
    this.raf = 0;
    this.playing = false;
    this.speed = 1;
    this.loop = true;
    this.clips = [];
    this.mixer = null;
    this.action = null;
    this.managers = [];
    this.texCache = new Map();
    this.pieces = [];
    this.warnings = [];
    this.mode = 'model';
    this.texStats = { failed: [], undecodable: [] };
    this.reqStats = { missing: [], ambiguous: [], undecodable: [] };
    this.matStats = { bound: 0, unbound: 0, single: 0, embedded: 0 };
    this.unboundNames = new Set();
    this.degraded = [];
  }

  degrade(msg) {
    if (!this.degraded.includes(msg)) this.degraded.push(msg);
    if (!this.warnings.includes(msg)) this.warnings.push(msg);
  }

  // ---- loading ------------------------------------------------------------

  /**
   * @param {{desc?: object, preview?: object, asset?: object, fetchDescriptor?: (id: string) => Promise<object|null>}} opts
   * `fetchDescriptor` returns a normalized descriptor for another asset (the body of an animation).
   */
  async load({ desc, preview, asset, fetchDescriptor = null }) {
    const THREE = await import('three');
    const { OrbitControls } = await import('three/addons/controls/OrbitControls.js');
    const { TGALoader } = await import('three/addons/loaders/TGALoader.js');
    if (this.disposed) return null;
    this.THREE = THREE;
    this.synthetic = !desc;
    this.desc = desc || {
      model: preview ? { kind: preview.kind, url: preview.url, id: asset?.id || '' } : null,
      textures: [], materials: [], warnings: [], clientWarnings: [], visibleNodes: [], parts: [],
      fidelity: 'unavailable', serverFidelity: 'unavailable', assetKind: asset?.kind || '', degraded: [],
    };
    const d = this.desc;
    this.index = new TextureIndex(d.textures);
    this.formatByUrl = new Map(d.textures.map((t) => [t.url, (t.format || extOf(t.path || t.name)).toLowerCase()]));
    this.allowed = new Set();
    const allow = (u) => { const k = this.urlKey(u); if (k) this.allowed.add(k); };
    allow(d.model?.url);
    allow(d.animationModel?.url);
    for (const t of d.textures) allow(t.url);
    for (const p of d.parts || []) allow(p.model?.url);
    const plainManager = new THREE.LoadingManager();
    plainManager.setURLModifier((u) => (this.isAllowed(u) ? u : PLACEHOLDER_PNG));
    this.managers.push(plainManager);
    this.texLoader = new THREE.TextureLoader(plainManager);
    this.tgaLoader = new TGALoader(plainManager);

    let root;
    let clips = [];
    if (d.model) {
      root = await this.loadModel(d.model);
      if (this.disposed) { disposeObject(root); return null; }
      clips = root.animations || [];
      if (!meshesOf(root).length) {
        if (d.animationModel) {
          let body = null;
          try {
            body = await this.loadModel(d.animationModel);
          } catch (err) {
            this.warnings.push(`No se pudo cargar el cuerpo para la animación: ${err?.message || err}.`);
          }
          if (this.disposed) { if (body) disposeObject(body); disposeObject(root); return null; }
          if (body && meshesOf(body).length) {
            disposeObject(root);
            root = body;
            this.mode = 'animation';
            await this.dressBody(root, d.animationModel, fetchDescriptor);
            if (this.disposed) { disposeObject(root); return null; }
          }
        }
        if (this.mode !== 'animation') {
          this.mode = 'skeleton';
          this.degrade('El archivo no trae malla y no hay cuerpo compatible: se muestra solo el esqueleto.');
        }
      } else {
        const own = materialsForModel(d.materials, d.model.id, d.model.id);
        const vis = d.visibleNodes || [];
        // With prefab visible nodes, hide first so hidden variants do not count as unbound.
        if (vis.length) this.applyVisibility(root, vis, true);
        this.applyMaterials(root, own, d.model.kind === 'glb');
        if (!vis.length) this.applyVisibility(root, vis, true);
        if (d.parts?.length) await this.loadParts(root, d.parts);
        if (this.disposed) { disposeObject(root); return null; }
      }
    } else if (d.materials.length) {
      this.mode = 'sphere';
      root = new THREE.Mesh(new THREE.SphereGeometry(1, 64, 32), this.makeMaterial(d.materials[0], false));
      root.name = d.materials[0].name;
      this.matStats.bound = 1;
      if (d.materials.length > 1) this.warnings.push(`El descriptor trae ${d.materials.length} materiales; la esfera muestra el primero («${d.materials[0].name}»).`);
    } else {
      throw new Error('El descriptor no indica ningún modelo ni material que se pueda mostrar.');
    }

    this.setupScene(THREE, OrbitControls, root);
    if (this.mode === 'skeleton') {
      const helper = new THREE.SkeletonHelper(root);
      this.scene.add(helper);
      this.helper = helper;
    }
    this.fit(true);
    this.setupAnimation(root, clips);
    this.buildControls();
    if (this.clips.length && (this.mode !== 'model' || d.assetKind === 'animacion')) this.play();

    const meshes = meshesOf(root);
    let triangles = 0;
    for (const m of meshes) {
      const g = m.geometry;
      if (g) triangles += g.index ? g.index.count / 3 : (g.attributes.position?.count || 0) / 3;
    }
    this.counts = { meshes: meshes.length, triangles: Math.round(triangles) };
    return { meshes: meshes.length, triangles: Math.round(triangles), clips: this.clips.length, mode: this.mode };
  }

  urlKey(u) {
    if (typeof u !== 'string' || !u) return null;
    try {
      const abs = new URL(u, location.href);
      return abs.origin === location.origin ? abs.pathname + abs.search : null;
    } catch {
      return null;
    }
  }

  isAllowed(u) {
    if (typeof u !== 'string') return false;
    if (u.startsWith('blob:') || u.startsWith('data:')) return true;
    const k = this.urlKey(u);
    return !!k && this.allowed.has(k);
  }

  formatOf(url) {
    return this.formatByUrl.get(url) || '';
  }

  /** Maps a texture request made by a model file to a descriptor texture (unique match only). */
  resolveRequest(url, base) {
    let req = String(url);
    try {
      const abs = new URL(req, location.href);
      if (abs.origin === location.origin) {
        req = decodeURIComponent(abs.pathname);
        if (base && req.startsWith(base)) req = req.slice(base.length);
      }
    } catch { /* keep raw */ }
    const r = this.index.resolve(req);
    if (r.status === 'resolved') {
      const ext = this.formatOf(r.texture.url) || extOf(r.texture.path || r.texture.name);
      if (ext && !DECODABLE_EXT.has(ext)) { this.reqStats.undecodable.push(req); return null; }
      return r.texture;
    }
    (r.status === 'ambiguous' ? this.reqStats.ambiguous : this.reqStats.missing).push(req);
    return null;
  }

  makeManager(modelUrl) {
    const THREE = this.THREE;
    const manager = new THREE.LoadingManager();
    this.managers.push(manager);
    const base = (this.urlKey(modelUrl) || '').replace(/[^/]*$/, '');
    manager.setURLModifier((url) => {
      if (this.isAllowed(url)) return url;
      if (IMAGE_RE.test(url.split(/[?#]/)[0])) {
        const t = this.resolveRequest(url, base);
        if (t && this.formatOf(t.url) !== 'tga') return t.url;
        return PLACEHOLDER_PNG;
      }
      return 'data:,';
    });
    // FBXLoader asks the manager for .tga: resolve against the descriptor too.
    const viewer = this;
    manager.addHandler(/\.tga$/i, {
      path: '',
      setPath(p) { this.path = p || ''; return this; },
      setCrossOrigin() { return this; },
      setRequestHeader() { return this; },
      load(file, onLoad) {
        const t = viewer.resolveRequest((this.path || '') + file, base);
        const tex = t ? viewer.loadTexture(t.url, THREE.SRGBColorSpace, true) : null;
        if (tex) { if (onLoad) queueMicrotask(() => onLoad(tex)); return tex; }
        const ph = new THREE.DataTexture(new Uint8Array([...NEUTRAL_RGB, 255]), 1, 1);
        ph.needsUpdate = true;
        ph.userData.placeholder = true;
        if (onLoad) queueMicrotask(() => onLoad(ph));
        return ph;
      },
    });
    manager.onLoad = () => { this.neutralizePlaceholders(); this.render(); };
    return manager;
  }

  async loadModel(ref) {
    const manager = this.makeManager(ref.url);
    if (ref.kind === 'glb') {
      const { GLTFLoader } = await import('three/addons/loaders/GLTFLoader.js');
      const loader = new GLTFLoader(manager);
      try {
        const { MeshoptDecoder } = await import('three/addons/libs/meshopt_decoder.module.js');
        loader.setMeshoptDecoder(MeshoptDecoder);
      } catch { /* only uncompressed GLB without the decoder */ }
      const gltf = await loader.loadAsync(ref.url);
      const root = gltf.scene || gltf.scenes?.[0];
      if (!root) throw new Error('El archivo no contiene ninguna escena.');
      root.animations = gltf.animations || [];
      root.userData.glb = true;
      return root;
    }
    const { FBXLoader } = await import('three/addons/loaders/FBXLoader.js');
    const root = await new FBXLoader(manager).loadAsync(ref.url);
    if (!root) throw new Error('El archivo no contiene ninguna escena.');
    return root;
  }

  loadTexture(url, colorSpace, flipY) {
    const key = `${url}|${colorSpace}|${flipY}`;
    if (this.texCache.has(key)) return this.texCache.get(key);
    const fmt = this.formatOf(url);
    if (fmt && !DECODABLE_EXT.has(fmt)) {
      this.texStats.undecodable.push(url);
      this.texCache.set(key, null);
      return null;
    }
    const fail = () => {
      this.texStats.failed.push(url);
      this.onChange?.();
    };
    let tex;
    if (fmt === 'tga') {
      tex = this.tgaLoader.load(url, () => this.render(), undefined, fail);
    } else {
      tex = this.texLoader.load(url, () => this.render(), undefined, fail);
      tex.flipY = flipY;
    }
    tex.colorSpace = colorSpace;
    tex.anisotropy = 4;
    this.texCache.set(key, tex);
    return tex;
  }

  makeMaterial(dm, glb) {
    const THREE = this.THREE;
    const flipY = !glb;
    const m = new THREE.MeshStandardMaterial({ name: dm.name });
    if (dm.color) m.color.setRGB(dm.color[0], dm.color[1], dm.color[2], THREE.SRGBColorSpace);
    if (dm.map) m.map = this.loadTexture(dm.map, THREE.SRGBColorSpace, flipY);
    if (dm.normalMap) m.normalMap = this.loadTexture(dm.normalMap, THREE.NoColorSpace, flipY);
    if (dm.emissiveMap) {
      m.emissiveMap = this.loadTexture(dm.emissiveMap, THREE.SRGBColorSpace, flipY);
      const e = dm.emissive || [1, 1, 1];
      m.emissive.setRGB(e[0], e[1], e[2], THREE.SRGBColorSpace);
    } else if (dm.emissive) {
      m.emissive.setRGB(dm.emissive[0], dm.emissive[1], dm.emissive[2], THREE.SRGBColorSpace);
    }
    m.metalness = dm.metalness ?? 0;
    m.roughness = dm.roughness ?? 0.8;
    if (dm.transparent) {
      m.transparent = true;
      m.opacity = dm.opacity ?? 1;
      m.depthWrite = false;
    } else if (dm.alphaTest > 0 && dm.alphaTest < 1) {
      m.alphaTest = dm.alphaTest;
    }
    if (dm.doubleSided) m.side = THREE.DoubleSide;
    m.userData.descriptor = dm.name;
    return m;
  }

  applyMaterials(root, materials, glb) {
    const binder = new MaterialBinder(materials.filter((m) => !(Array.isArray(m.meshes) && m.meshes.length)));
    const stats = (o) => (o.visible ? this.matStats : { bound: 0, unbound: 0, single: 0, embedded: 0 });
    const names = new Set();
    const meshes = meshesOf(root);
    for (const o of meshes) for (const m of [].concat(o.material)) if (m) names.add(m.name);
    for (const o of meshes) {
      const list = [].concat(o.material);
      const out = list.map((m, slot) => {
        const st = stats(o);
        if (glb && m?.map) { st.embedded++; return m; }
        const r = pickMaterial(materials, binder, o.name, slot, m?.name, names.size);
        if (r.status !== 'bound') {
          st.unbound++;
          if (o.visible) this.unboundNames.add(`${o.name}${m?.name ? ` (${m.name})` : ''}`);
          return m;
        }
        st.bound++;
        if (r.via === 'single') st.single++;
        const nm = this.makeMaterial(r.material, glb);
        if (o.isSkinnedMesh || o.geometry?.attributes?.color === undefined) nm.vertexColors = false;
        m?.dispose?.();
        return nm;
      });
      o.material = Array.isArray(o.material) ? out : out[0];
    }
  }

  isPlaceholder(t) {
    if (!t) return false;
    if (t.userData?.placeholder) return true;
    const src = t.image?.src || t.source?.data?.src;
    return typeof src === 'string' && src === PLACEHOLDER_PNG;
  }

  neutralizePlaceholders() {
    if (!this.root || !this.THREE) return;
    const THREE = this.THREE;
    const neutral = new THREE.Color().setRGB(NEUTRAL_RGB[0] / 255, NEUTRAL_RGB[1] / 255, NEUTRAL_RGB[2] / 255, THREE.SRGBColorSpace);
    this.root.traverse((o) => {
      if (!o.isMesh) return;
      for (const m of [].concat(o.material)) {
        if (!m || m.userData?.descriptor) continue;
        for (const slot of TEXTURE_SLOTS) {
          if (this.isPlaceholder(m[slot])) {
            m[slot] = null;
            if (slot === 'map' && m.color) m.color.copy(neutral);
            m.needsUpdate = true;
          }
        }
      }
    });
  }

  /** Gives the animation body its own real materials when the server can describe them. */
  async dressBody(root, ref, fetchDescriptor) {
    const label = ref.name || 'cuerpo de muestra';
    let bd = null;
    if (fetchDescriptor && ref.id) {
      try { bd = await fetchDescriptor(ref.id); } catch { bd = null; }
    }
    if (this.disposed) return;
    const mats = bd ? materialsForModel(bd.materials, ref.id, bd.model?.id || ref.id) : [];
    if (mats.length) {
      for (const t of bd.textures || []) {
        const k = this.urlKey(t.url);
        if (!k) continue;
        this.allowed.add(k);
        if (!this.formatByUrl.has(t.url)) this.formatByUrl.set(t.url, (t.format || extOf(t.path || t.name)).toLowerCase());
      }
      const before = { ...this.matStats };
      if (bd.visibleNodes?.length) this.applyVisibility(root, bd.visibleNodes, false);
      this.applyMaterials(root, mats, ref.kind === 'glb');
      const unbound = this.matStats.unbound - before.unbound;
      this.warnings.push(`Animación mostrada sobre «${label}», el rig de origen que indica el servidor, con sus materiales.`);
      if (unbound > 0 || bd.fidelity !== 'resolved') this.degrade(`El cuerpo «${label}» no tiene todos sus materiales resueltos.`);
      return;
    }
    this.neutralizeAll(root);
    this.degrade(`Animación mostrada sobre «${label}», el rig de origen que indica el servidor; ese cuerpo va en gris neutro, sin sus materiales reales.`);
  }

  neutralizeAll(root) {
    const THREE = this.THREE;
    root.traverse((o) => {
      if (!o.isMesh) return;
      o.material = [].concat(o.material).length > 1
        ? [].concat(o.material).map(() => new THREE.MeshStandardMaterial({ color: new THREE.Color().setRGB(0.7, 0.72, 0.74), roughness: 0.85 }))
        : new THREE.MeshStandardMaterial({ color: new THREE.Color().setRGB(0.7, 0.72, 0.74), roughness: 0.85 });
    });
  }

  applyVisibility(root, visibleNodes, topLevel) {
    const meshes = meshesOf(root);
    if (visibleNodes.length) {
      const keys = new Set(visibleNodes.map(nameKey));
      let shown = 0;
      for (const m of meshes) {
        let v = false;
        for (let o = m; o && !v; o = o.parent) v = keys.has(nameKey(o.name));
        m.visible = v;
        if (v) shown++;
      }
      if (!shown) {
        for (const m of meshes) m.visible = true;
        this.warnings.push('Ninguna malla coincide con los nodos visibles del prefab: se muestran todas.');
      } else if (shown < meshes.length && topLevel) {
        this.warnings.push(`Se muestran ${shown} de ${meshes.length} mallas: las que activa el prefab.`);
      }
      return;
    }
    if (!topLevel) return;
    const bodies = meshes.filter((m) => m.isSkinnedMesh);
    if (bodies.length >= OVERLAP_BODIES) {
      this.pieces = bodies;
      const first = Math.max(0, bodies.findIndex((m) => [].concat(m.material).some((x) => x?.userData?.descriptor)));
      this.showPiece(first, false);
      this.warnings.push(`El archivo trae ${bodies.length} cuerpos superpuestos y el servidor no dice cuál activa el prefab: se muestra uno cada vez (elige en «Pieza»).`);
    }
  }

  showPiece(i, refit = true) {
    this.pieceIndex = i;
    this.pieces.forEach((m, j) => { m.visible = i < 0 || j === i; });
    if (refit && this.ready) this.fit();
  }

  async loadParts(root, parts) {
    const THREE = this.THREE;
    let failed = 0;
    const mainId = this.desc.model?.id || '';
    this.degrade(`Prefab compuesto de ${plural(parts.length, 'pieza', 'piezas')}: su posición se convierte desde Unity y es aproximada.`);
    if (parts.length > MAX_PARTS) this.degrade(`Solo se cargan las primeras ${MAX_PARTS} piezas: faltan ${parts.length - MAX_PARTS}.`);
    for (const p of parts.slice(0, MAX_PARTS)) {
      if (this.disposed) return;
      if (!p.model) { failed++; continue; }
      try {
        const obj = await this.loadModel(p.model);
        if (this.disposed) { disposeObject(obj); return; }
        // Bindings come from materials[] by modelId; parts[].materials only lists their names.
        const mats = materialsForModel(this.desc.materials, p.model.id, mainId);
        if (p.visibleNodes?.length) this.applyVisibility(obj, p.visibleNodes, false);
        this.applyMaterials(obj, mats, p.model.kind === 'glb');
        const parentNode = p.parentName ? root.getObjectByName(p.parentName) : null;
        if (p.parentName && !parentNode) this.degrade(`No se encuentra el nodo «${p.parentName}» para colgar «${p.model.name || 'una pieza'}»: va en la raíz.`);
        if (!p.local) this.degrade(`La pieza «${p.model.name || 'sin nombre'}» no trae transformación local: su posición puede no ser la del prefab.`);
        const parent = parentNode || root;
        const pos = vec(p.local?.position, 3);
        const rot4 = vec(p.local?.rotation, 4);
        const rot3 = rot4 ? null : vec(p.local?.rotation, 3);
        const scl = vec(p.local?.scale, 3);
        // Unity is left-handed: mirror X for positions, negate Y/Z of rotations.
        if (pos) obj.position.set(-pos[0], pos[1], pos[2]);
        if (rot4) obj.quaternion.set(rot4[0], -rot4[1], -rot4[2], rot4[3]);
        else if (rot3) {
          const q = new THREE.Quaternion().setFromEuler(new THREE.Euler(...rot3.map(THREE.MathUtils.degToRad), 'YXZ'));
          obj.quaternion.set(q.x, -q.y, -q.z, q.w);
        }
        if (scl) obj.scale.set(scl[0], scl[1], scl[2]);
        parent.add(obj);
      } catch {
        failed++;
      }
    }
    if (failed) this.degrade(`${plural(failed, 'pieza', 'piezas')} del prefab no se han podido cargar.`);
  }

  // ---- scene ----------------------------------------------------------------

  setupScene(THREE, OrbitControls, root) {
    let renderer;
    try {
      renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, powerPreference: 'low-power' });
    } catch {
      disposeObject(root);
      throw new Error('Este navegador no puede abrir WebGL.');
    }
    this.renderer = renderer;
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.setClearColor(0x2a2d31, 1);
    renderer.domElement.setAttribute('aria-label', 'Visor 3D: arrastra para girar, rueda para acercar, clic derecho para desplazar');
    renderer.domElement.setAttribute('role', 'img');
    renderer.domElement.tabIndex = 0;

    const scene = new THREE.Scene();
    this.scene = scene;
    scene.add(new THREE.HemisphereLight(0xffffff, 0x50545a, 1.6));
    const key = new THREE.DirectionalLight(0xffffff, 1.6);
    scene.add(key);
    const fill = new THREE.DirectionalLight(0xffffff, 0.5);
    fill.position.set(-4, 2, -3);
    scene.add(fill);
    this.key = key;
    scene.add(root);
    this.root = root;
    this.neutralizePlaceholders();

    const camera = new THREE.PerspectiveCamera(40, 1, 0.01, 1000);
    this.camera = camera;
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = false;
    controls.listenToKeyEvents(renderer.domElement);
    controls.addEventListener('change', () => this.render());
    this.controls = controls;

    this.host.replaceChildren(renderer.domElement);
    this.ro = new ResizeObserver(() => this.resize());
    this.ro.observe(this.host);
    this.ready = true;
    this.resize();
  }

  visibleBox() {
    const THREE = this.THREE;
    const box = new THREE.Box3();
    this.root.updateMatrixWorld(true);
    this.root.traverseVisible((o) => {
      if (o.isMesh && o.geometry) {
        if (!o.geometry.boundingBox) o.geometry.computeBoundingBox();
        box.union(o.geometry.boundingBox.clone().applyMatrix4(o.matrixWorld));
      }
    });
    if (box.isEmpty()) {
      const p = new THREE.Vector3();
      this.root.traverse((o) => { if (o.isBone) box.expandByPoint(o.getWorldPosition(p)); });
    }
    return box;
  }

  fit(first = false) {
    if (!this.root || !this.camera) return;
    const THREE = this.THREE;
    const box = this.visibleBox();
    if (box.isEmpty()) throw new Error('El modelo no tiene geometría visible.');
    const size = box.getSize(new THREE.Vector3());
    const center = box.getCenter(new THREE.Vector3());
    const radius = Math.max(size.length() / 2, 1e-3);
    const fov = THREE.MathUtils.degToRad(this.camera.fov);
    const dist = (radius / Math.sin(fov / 2)) * 1.05;
    const dir = new THREE.Vector3(1, 0.65, 1.25).normalize();
    this.camera.position.copy(center).addScaledVector(dir, dist);
    this.camera.near = Math.max(dist / 1000, 1e-4);
    this.camera.far = dist * 100;
    this.camera.updateProjectionMatrix();
    this.controls.target.copy(center);
    this.controls.minDistance = radius * 0.05;
    this.controls.maxDistance = dist * 20;
    this.controls.update();
    if (this.grid) { this.scene.remove(this.grid); this.grid.geometry.dispose(); this.grid.material.dispose(); }
    const step = Math.pow(10, Math.floor(Math.log10(Math.max(size.x, size.z, 1e-3))));
    const extent = Math.ceil((Math.max(size.x, size.z) * 1.6) / step) * step;
    const grid = new THREE.GridHelper(extent, Math.max(2, Math.round(extent / step)), 0x6b7078, 0x464a50);
    grid.position.set(center.x, box.min.y, center.z);
    grid.visible = this.gridVisible;
    this.grid = grid;
    this.scene.add(grid);
    this.key.position.copy(center).add(new THREE.Vector3(radius * 2, radius * 3, radius * 2.5));
    this.key.target.position.copy(center);
    if (first) this.scene.add(this.key.target);
    this.render();
  }

  setGrid(on) {
    this.gridVisible = on;
    if (this.grid) this.grid.visible = on;
    this.render();
  }

  resize() {
    if (!this.renderer || this.disposed) return;
    const w = Math.max(1, this.host.clientWidth);
    const hgt = Math.max(1, this.host.clientHeight);
    this.renderer.setSize(w, hgt, false);
    this.camera.aspect = w / hgt;
    this.camera.updateProjectionMatrix();
    this.render();
  }

  render() {
    if (this.disposed || !this.renderer) return;
    this.renderer.render(this.scene, this.camera);
  }

  // ---- animation ------------------------------------------------------------

  setupAnimation(root, clips) {
    const THREE = this.THREE;
    this.clips = (clips || []).filter((c) => c && c.tracks?.length && c.duration > 0);
    if (!this.clips.length) return;
    this.clock = new THREE.Clock(false);
    this.mixer = new THREE.AnimationMixer(root);
    this.mixer.addEventListener('finished', () => {
      this.playing = false;
      this.stopLoop();
      this.syncControls();
    });
    this.selectClip(0);
  }

  selectClip(i) {
    const THREE = this.THREE;
    const clip = this.clips[i];
    if (!clip || !this.mixer) return;
    this.clipIndex = i;
    if (this.action) this.action.stop();
    this.action = this.mixer.clipAction(clip);
    this.action.setLoop(this.loop ? THREE.LoopRepeat : THREE.LoopOnce, Infinity);
    this.action.clampWhenFinished = true;
    this.action.reset().play();
    this.action.paused = !this.playing;
    this.mixer.update(0);
    this.render();
    this.syncControls();
  }

  play() {
    if (!this.action) return;
    if (!this.loop && this.action.time >= this.action.getClip().duration - 1e-4) this.action.reset();
    this.action.enabled = true;
    this.action.paused = false;
    if (!this.action.isRunning()) this.action.play();
    this.playing = true;
    this.clock.start();
    this.startLoop();
    this.syncControls();
  }

  pause() {
    this.playing = false;
    if (this.action) this.action.paused = true;
    this.stopLoop();
    this.syncControls();
  }

  setTime(t) {
    if (!this.action) return;
    const dur = this.action.getClip().duration;
    this.action.time = Math.min(Math.max(0, t), dur);
    this.action.paused = !this.playing;
    this.mixer.update(0);
    this.render();
    this.syncControls();
  }

  setSpeed(s) {
    this.speed = s;
    if (this.mixer) this.mixer.timeScale = s;
  }

  setLoop(on) {
    this.loop = on;
    if (this.action) this.action.setLoop(on ? this.THREE.LoopRepeat : this.THREE.LoopOnce, Infinity);
  }

  startLoop() {
    if (this.raf || this.disposed) return;
    const tick = () => {
      this.raf = 0;
      if (!this.playing || this.disposed) return;
      this.mixer.update(this.clock.getDelta());
      this.render();
      this.syncControls(true);
      this.raf = requestAnimationFrame(tick);
    };
    this.clock.getDelta();
    this.raf = requestAnimationFrame(tick);
  }

  stopLoop() {
    if (this.raf) cancelAnimationFrame(this.raf);
    this.raf = 0;
    this.clock?.stop();
  }

  // ---- controls (plain DOM, Spanish labels) ---------------------------------

  buildControls() {
    const host = this.controlsHost;
    if (!host) return;
    host.replaceChildren();
    const el = (tag, attrs = {}, text = '') => {
      const n = document.createElement(tag);
      for (const [k, v] of Object.entries(attrs)) n.setAttribute(k, v);
      if (text) n.textContent = text;
      return n;
    };
    const ui = {};
    if (this.pieces.length) {
      const wrap = el('label', { class: 'anim-field' });
      wrap.append(el('span', {}, 'Pieza'));
      const sel = el('select', { 'aria-label': 'Pieza visible' });
      sel.append(el('option', { value: '-1' }, `Todas (${this.pieces.length}, superpuestas)`));
      this.pieces.forEach((m, i) => sel.append(el('option', { value: String(i) }, m.name || `Malla ${i + 1}`)));
      sel.value = String(this.pieceIndex ?? 0);
      sel.addEventListener('change', () => this.showPiece(Number(sel.value)));
      wrap.append(sel);
      host.append(wrap);
    }
    if (this.clips.length) {
      const bar = el('div', { class: 'anim-controls', role: 'group', 'aria-label': 'Animación' });
      ui.play = el('button', { type: 'button', class: 'btn small anim-play' });
      ui.play.addEventListener('click', () => (this.playing ? this.pause() : this.play()));
      bar.append(ui.play);
      if (this.clips.length > 1) {
        ui.clip = el('select', { 'aria-label': 'Clip de animación' });
        this.clips.forEach((c, i) => ui.clip.append(el('option', { value: String(i) }, `${c.name || `Clip ${i + 1}`} (${fmtSec(c.duration)})`)));
        ui.clip.addEventListener('change', () => this.selectClip(Number(ui.clip.value)));
        bar.append(ui.clip);
      } else {
        bar.append(el('span', { class: 'anim-name' }, this.clips[0].name || 'Animación'));
      }
      ui.scrub = el('input', { type: 'range', min: '0', step: '0.001', value: '0', 'aria-label': 'Posición en la animación' });
      ui.scrub.addEventListener('input', () => { if (this.playing) this.pause(); this.setTime(Number(ui.scrub.value)); });
      ui.time = el('span', { class: 'anim-time', 'aria-live': 'off' });
      const speed = el('select', { 'aria-label': 'Velocidad' });
      for (const s of SPEEDS) speed.append(el('option', { value: String(s) }, `${String(s).replace('.', ',')}×`));
      speed.value = String(this.speed);
      speed.addEventListener('change', () => this.setSpeed(Number(speed.value)));
      const loopLabel = el('label', { class: 'anim-loop' });
      const loop = el('input', { type: 'checkbox' });
      loop.checked = this.loop;
      loop.addEventListener('change', () => this.setLoop(loop.checked));
      loopLabel.append(loop, document.createTextNode(' Bucle'));
      bar.append(ui.scrub, ui.time, speed, loopLabel);
      host.append(bar);
    }
    this.ui = ui;
    this.syncControls();
  }

  syncControls(fromTick = false) {
    const ui = this.ui;
    if (!ui || !this.action) return;
    const dur = this.action.getClip().duration;
    const t = this.loop ? this.action.time % dur : Math.min(this.action.time, dur);
    if (fromTick && this.lastSync && performance.now() - this.lastSync < 100) return;
    this.lastSync = performance.now();
    ui.play.textContent = this.playing ? 'Pausa' : 'Reproducir';
    ui.play.setAttribute('aria-pressed', String(this.playing));
    ui.scrub.max = String(dur);
    ui.scrub.value = String(t);
    ui.time.textContent = `${fmtSec(t)} / ${fmtSec(dur)}`;
    if (ui.clip) ui.clip.value = String(this.clipIndex);
  }

  // ---- fidelity ---------------------------------------------------------------

  summary() {
    const d = this.desc || {};
    const textures = { failed: this.texStats.failed, undecodable: this.texStats.undecodable };
    // Model-side texture requests only matter for materials the descriptor did not replace.
    if (this.matStats.unbound > 0) {
      textures.missing = this.reqStats.missing;
      textures.ambiguous = this.reqStats.ambiguous;
      textures.undecodable = [...textures.undecodable, ...this.reqStats.undecodable];
    }
    const materials = this.mode === 'model' ? this.matStats : {};
    const f = summarizeFidelity({
      server: d.fidelity || 'unavailable',
      modelLoaded: this.ready,
      synthetic: this.synthetic,
      textures,
      materials,
      degraded: [...(d.degraded || []), ...this.degraded],
    });
    const reasons = [...f.reasons];
    if (this.unboundNames.size && this.mode === 'model') {
      const list = [...this.unboundNames].slice(0, 4).join(', ');
      reasons.push(`Sin material real (color del archivo o neutro): ${list}${this.unboundNames.size > 4 ? '…' : ''}.`);
    }
    if (this.mode === 'skeleton' && f.level === 'resolved') f.level = 'partial';
    const warnings = [...(d.warnings || []), ...(d.clientWarnings || []), ...this.warnings];
    return { level: f.level, reasons, warnings: [...new Set(warnings)], mode: this.mode, ...(this.counts || {}), clips: this.clips.length };
  }

  dispose() {
    if (this.disposed) return;
    this.disposed = true;
    this.ready = false;
    this.playing = false;
    this.stopLoop();
    if (this.mixer) {
      this.mixer.stopAllAction();
      if (this.root) this.mixer.uncacheRoot(this.root);
    }
    for (const m of this.managers) { try { m.abort?.(); } catch { /* no abort support */ } }
    this.ro?.disconnect();
    this.controls?.stopListenToKeyEvents?.();
    this.controls?.dispose();
    if (this.scene) disposeObject(this.scene);
    for (const t of this.texCache.values()) t?.dispose?.();
    this.texCache.clear();
    if (this.renderer) {
      this.renderer.dispose();
      this.renderer.forceContextLoss();
      this.renderer.domElement.remove();
    }
    this.controlsHost?.replaceChildren();
    this.scene = this.root = this.renderer = this.camera = this.controls = this.grid = this.mixer = this.action = this.ui = null;
    this.onChange = null;
  }
}
