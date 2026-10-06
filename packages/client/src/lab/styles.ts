import * as THREE from 'three';
import type { MatSpec, Role } from './kit';
import { DEPTH, WIDTH } from './layout';
import { EdgePass, gradePass, palettePass, PIXEL_PALETTE, tiltShiftPass } from './post';
import { addEdges, bloom, composerFor, finish, gtao, HALFTONE, HATCHING, hex, holoMaterial, lampLights, lowPoly, shift, standard, streetGround, sun, toon, withMap, type ArtStyle } from './stylekit';
import { brickTexture, gradientTexture, holoGridTexture, noiseTexture, paperTexture, toonRamp } from './textures';
import { voxelizeProp, voxelizeRig } from '../game/voxel';

// The first round of styles (2026-10-04). The 90s variants live in styles90.ts.

// ================================================================= styles

const current: ArtStyle = {
  id: 'actual',
  family: 'aparcado',
  name: 'Actual',
  tagline: 'Lo que hay hoy en el juego: bloques y primitivas de colores.',
  refs: ['El prototipo M1–M3'],
  assets: 'Todo es código: cajas por casilla y muñecos de cápsulas.',
  cost: 'Muy bajo',
  pros: ['Lectura táctica clara: cada bloque es una cobertura', 'Cero trabajo de arte'],
  cons: ['Sin identidad ni ambiente', 'No comunica qué es cada objeto (¿coche? ¿muro?)'],
  camera: 'persp',
  pixelScale: 0,
  props: 'blocks',
  seg: 12,
  fxLights: true,
  keepUnits: true,
  material: (spec) => standard(spec),
  ground() {
    const g = new THREE.Group();
    const count = WIDTH * DEPTH;
    const floor = new THREE.InstancedMesh(new THREE.BoxGeometry(1, 0.1, 1), new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.95 }), count);
    floor.receiveShadow = true;
    const m = new THREE.Matrix4();
    const base = new THREE.Color(0x3a4650);
    for (let i = 0; i < count; i++) {
      const x = i % WIDTH;
      const y = Math.floor(i / WIDTH);
      m.setPosition(x + 0.5, -0.05, y + 0.5);
      floor.setMatrixAt(i, m);
      const jitter = (((x * 73856093) ^ (y * 19349663)) & 0xff) / 255;
      floor.setColorAt(i, base.clone().offsetHSL(0, 0, (jitter - 0.5) * 0.025));
    }
    g.add(floor);
    const pts: number[] = [];
    for (let x = 0; x <= WIDTH; x++) pts.push(x, 0.002, 0, x, 0.002, DEPTH);
    for (let y = 0; y <= DEPTH; y++) pts.push(0, 0.002, y, WIDTH, 0.002, y);
    g.add(
      new THREE.LineSegments(
        new THREE.BufferGeometry().setAttribute('position', new THREE.Float32BufferAttribute(pts, 3)),
        new THREE.LineBasicMaterial({ color: 0x7fb7d6, transparent: true, opacity: 0.08 }),
      ),
    );
    return g;
  },
  environment(ctx) {
    const bg = new THREE.Color(0x0a1016);
    ctx.scene.background = bg;
    ctx.scene.fog = new THREE.Fog(bg, 45, 110);
    ctx.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    ctx.renderer.toneMappingExposure = 1.2;
    ctx.scene.add(new THREE.HemisphereLight(0xc4dcff, 0x252c35, 1.7));
    const s = sun(ctx, 0xfff0dc, 2.9, [-18, 34, -12], { radius: 1 });
    s.shadow.bias = -0.0004;
  },
  post: () => ({ composer: null }),
};

// ------------------------------------------------------------ cinematic

let cinematicTextures: { noise: THREE.Texture; brick: THREE.Texture; bump: THREE.Texture } | null = null;
function cineTex() {
  cinematicTextures ??= { noise: noiseTexture(256, 0.62, 1, 7), brick: brickTexture(), bump: noiseTexture(256, 0, 1, 19, 5) };
  return cinematicTextures;
}

const cinematic: ArtStyle = {
  id: 'cinematico',
  family: 'aparcado',
  name: 'Cinemático nocturno',
  tagline: 'XCOM 2: noche, lluvia, asfalto mojado, farolas y neón alienígena.',
  refs: ['XCOM 2', 'Phoenix Point', 'Gears Tactics', 'Mutant Year Zero'],
  assets:
    'Modelos realistas con texturas PBR: packs de tienda (Sketchfab, CGTrader, Fab) exportados a glTF, animaciones de Mixamo, o encargarlos. Las primitivas de esta demo son lo que más “canta” en este estilo.',
  cost: 'Alto',
  pros: ['El más fiel a XCOM y el de más atmósfera', 'La luz y el posproceso hacen gran parte del trabajo', 'Encaja con el tono serio de la campaña'],
  cons: [
    'Exige modelos y animaciones de calidad y coherentes entre sí (mezclar packs se nota)',
    'El más pesado en GPU (sombras de farolas, AO, bloom)',
    'De noche la lectura táctica cuesta más: la interfaz tiene que compensar',
  ],
  camera: 'persp',
  pixelScale: 0,
  props: 'kit',
  seg: 16,
  fxLights: true,
  role(role, spec) {
    if (role === 'paint') return { ...spec, color: hex('#5d2a24') };
    if (role === 'window') return { ...spec, emissiveIntensity: 0.9 };
    return spec;
  },
  material(spec) {
    const t = cineTex();
    switch (spec.role) {
      case 'brick':
        return standard(spec, { map: t.brick, bumpMap: t.bump, bumpScale: 2 });
      case 'concrete':
      case 'concreteDark':
      case 'wood':
      case 'woodDark':
      case 'fabric':
      case 'bark':
        return standard(spec, { map: t.noise, roughnessMap: t.noise, bumpMap: t.bump, bumpScale: 1.5 });
      case 'metal':
      case 'darkMetal':
      case 'paint':
      case 'paint2':
      case 'alienMetal':
      case 'stripe':
        return standard(spec, { roughnessMap: t.noise, map: t.noise });
      default:
        return standard(spec, spec.role ? {} : { roughnessMap: t.noise });
    }
  },
  ground() {
    const t = cineTex();
    return streetGround(
      { px: 64, asphalt: '#3a3d42', sidewalk: '#6e6c66', curb: '#8a877f', paint: '#b9a650', jitter: 0.08, wear: true, joints: true },
      (tex) => new THREE.MeshStandardMaterial({ map: tex.map, roughnessMap: tex.rough, roughness: 1, metalness: 0, bumpMap: t.bump, bumpScale: 0.6 }),
      new THREE.MeshStandardMaterial({ color: 0x24272b, roughness: 0.6 }),
    );
  },
  environment(ctx) {
    const { scene, renderer } = ctx;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.3;
    scene.background = gradientTexture([[0, '#04070d'], [0.55, '#0c1522'], [1, '#1b2735']]);
    scene.fog = new THREE.FogExp2(0x0d1520, 0.03);

    // Night city reflections: dark sky, warm windows, a magenta sign.
    const env = new THREE.Scene();
    env.background = new THREE.Color(0x070b12);
    const panel = (color: number, k: number, x: number, y: number, z: number, w: number, h: number) => {
      const m = new THREE.Mesh(new THREE.PlaneGeometry(w, h), new THREE.MeshBasicMaterial({ color: new THREE.Color(color).multiplyScalar(k), side: THREE.DoubleSide }));
      m.position.set(x, y, z);
      m.lookAt(0, 0, 0);
      env.add(m);
    };
    panel(0x3a5a88, 0.6, 0, 12, 0, 30, 30);
    panel(0xffb070, 3, -9, 2, 4, 3, 1.5);
    panel(0xffb070, 3, 8, 3, -7, 2, 2);
    panel(0xff3aa8, 2.5, 6, 1.5, 8, 2, 0.8);
    const pmrem = new THREE.PMREMGenerator(renderer);
    scene.environment = pmrem.fromScene(env, 0.04).texture;
    scene.environmentIntensity = 0.55;
    pmrem.dispose();

    scene.add(new THREE.HemisphereLight(0x3a5072, 0x0a0c10, 1.1));
    sun(ctx, 0x8fb0ff, 0.6, [-0.5, 1, -0.6], { radius: 3 });
    lampLights(ctx, 0.75, 1, true);
  },
  post(ctx) {
    // Rain: short streaks falling through the whole diorama.
    const N = 1400;
    const pos = new Float32Array(N * 6);
    const seeds: number[] = [];
    for (let i = 0; i < N; i++) {
      const x = -4 + Math.random() * (WIDTH + 8);
      const y = Math.random() * 12;
      const z = -4 + Math.random() * (DEPTH + 10);
      seeds.push(x, y, z);
    }
    const geo = new THREE.BufferGeometry().setAttribute('position', new THREE.BufferAttribute(pos, 3));
    const rain = new THREE.LineSegments(geo, new THREE.LineBasicMaterial({ color: 0xa8bed6, transparent: true, opacity: 0.32, depthWrite: false }));
    rain.frustumCulled = false;
    ctx.scene.add(rain);
    const composer = composerFor(ctx);
    composer.addPass(gtao(ctx, 0.6, 1));
    composer.addPass(bloom(0.85, 0.55, 0.85));
    finish(composer, gradePass({ saturation: 0.92, contrast: 1.1, shadowTint: [0, 0.025, 0.05], highlightTint: [0.06, 0.03, 0], vignette: 0.5, grain: 0.05, aberration: 0.003 }));
    return {
      composer,
      tick(_dt, time) {
        for (let i = 0; i < N; i++) {
          const x = seeds[i * 3]!;
          const z = seeds[i * 3 + 2]!;
          const y = 12 - ((seeds[i * 3 + 1]! + time * 14) % 12);
          pos.set([x, y, z, x - 0.02, y + 0.35, z + 0.03], i * 6);
        }
        geo.attributes.position!.needsUpdate = true;
      },
    };
  },
};

// ---------------------------------------------------------------- comic

let comicRamp: THREE.Texture | null = null;
const comic: ArtStyle = {
  id: 'comic',
  family: 'comic',
  name: 'Cómic',
  tagline: 'Cel shading: contorno negro, sombras en bandas y trama de puntos.',
  refs: ['XCOM: Chimera Squad', 'Borderlands', 'Hi-Fi Rush', 'The Wolf Among Us'],
  assets:
    'Modelos de poca densidad con colores planos o texturas pintadas a mano; el contorno y las bandas los pone el shader. Sirven packs low-poly retocados de color.',
  cost: 'Medio',
  pros: ['Siluetas muy legibles a distancia táctica', 'Envejece bien y oculta modelos sencillos', 'Personalidad fuerte (Chimera Squad lo usó en la saga)'],
  cons: ['El contorno pide geometría limpia', 'Posproceso de bordes: algo de coste en GPU', 'Estilo muy marcado: condiciona interfaz y efectos'],
  camera: 'persp',
  pixelScale: 0,
  props: 'kit',
  seg: 12,
  fxLights: true,
  role: (_role, spec) => ({ ...spec, color: shift(spec.color, 1.35, 1.12) }),
  unitSpec: (spec) => ({ ...spec, color: shift(spec.color, 1.3, 1.15) }),
  material(spec) {
    comicRamp ??= toonRamp([0.32, 0.62, 1]);
    return toon(spec, comicRamp, HALFTONE);
  },
  debris(color) {
    comicRamp ??= toonRamp([0.32, 0.62, 1]);
    return new THREE.MeshToonMaterial({ color, gradientMap: comicRamp, transparent: true });
  },
  ground() {
    comicRamp ??= toonRamp([0.32, 0.62, 1]);
    const ramp = comicRamp;
    return streetGround(
      { px: 32, asphalt: '#4d5566', sidewalk: '#d1c6b0', curb: '#efe6d4', paint: '#ffd84a', jitter: 0, wear: false, joints: true },
      (tex) => withMap(toon({ color: new THREE.Color(0xffffff), metalness: 0, roughness: 1 }, ramp, HALFTONE), tex.map),
      new THREE.MeshToonMaterial({ color: 0x4d5566, gradientMap: ramp }),
    );
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.NeutralToneMapping;
    ctx.renderer.toneMappingExposure = 1.05;
    ctx.scene.background = gradientTexture([[0, '#3c9cf0'], [0.6, '#9fd6ff'], [1, '#ffe2b0']]);
    ctx.scene.add(new THREE.HemisphereLight(0xcde8ff, 0x8a6f5a, 1.4));
    sun(ctx, 0xfff0d8, 3.2, [-0.6, 1.1, 0.35], { radius: 1 });
  },
  post(ctx) {
    const composer = composerFor(ctx);
    const edges = new EdgePass(ctx.scene, ctx.camera, { color: 0x14101a, thickness: 1.6, depthThreshold: 0.02, normalThreshold: 0.35 });
    edges.hidden = ctx.noEdges;
    composer.addPass(edges);
    finish(composer, gradePass({ saturation: 1.1, contrast: 1.05, vignette: 0.18 }));
    return { composer };
  },
};

// -------------------------------------------------------------- diorama

const PASTEL = hex('#fff1e0');
const diorama: ArtStyle = {
  id: 'diorama',
  family: 'aparcado',
  name: 'Diorama low-poly',
  tagline: 'Maqueta de juguete: geometría facetada, colores suaves y desenfoque de miniatura.',
  refs: ['Bad North', 'Townscaper', 'Synty POLYGON', 'Into the Breach (en espíritu)'],
  assets:
    'El más fácil de conseguir: packs CC0 (Kenney, Quaternius —personajes sci-fi ya animados—), Synty (de pago, catálogo enorme militar y sci-fi) o modelar en Blender en low-poly, que se aprende rápido.',
  cost: 'Bajo',
  pros: ['Barato y coherente: un programador puede producirlo', 'Rendimiento excelente', 'Dos jugadores mirando un tablero: la metáfora de maqueta encaja'],
  cons: ['Riesgo de parecer “asset store genérico”', 'Menos tensión: tono amable para una invasión alienígena', 'Hay que cuidar la paleta para que destaque'],
  camera: 'persp',
  pixelScale: 0,
  props: 'kit',
  seg: 6,
  fxLights: true,
  role(role, spec) {
    const colors: Partial<Record<Role, string>> = {
      brick: '#c98a6b',
      concrete: '#ddd3c2',
      concreteDark: '#a99f90',
      paint: '#4f8fb0',
      paint2: '#e0a458',
      stripe: '#e2604b',
      foliage: '#7fb069',
      fabric: '#d4c08a',
      wood: '#c99a62',
      alienMetal: '#5b4a6b',
      glass: '#8fb7c9',
      window: '#9cc3d5',
    };
    const c = colors[role];
    return { ...spec, color: c ? hex(c) : shift(spec.color, 0.9, 1.1, PASTEL, 0.15), emissiveIntensity: role === 'window' ? 0 : spec.emissiveIntensity, emissive: role === 'window' ? undefined : spec.emissive };
  },
  unitSpec: (spec) => ({ ...spec, color: shift(spec.color, 0.95, 1.12, PASTEL, 0.1) }),
  material: (spec) => standard(spec, { flatShading: true, roughness: Math.max(0.7, spec.roughness), metalness: Math.min(spec.metalness, 0.15) }),
  rig(rig) {
    rig.root.traverse((o) => {
      if (o instanceof THREE.Mesh && o !== rig.flash) o.geometry = lowPoly(o.geometry);
    });
  },
  ground() {
    const g = streetGround(
      { px: 16, asphalt: '#6f7c86', sidewalk: '#e3d6bf', curb: '#f2eadb', paint: '#f5e6a8', jitter: 0, wear: false, joints: true },
      (tex) => new THREE.MeshStandardMaterial({ map: tex.map, roughness: 0.95 }),
      null,
    );
    const soil = new THREE.Mesh(new THREE.BoxGeometry(WIDTH, 1.6, DEPTH), new THREE.MeshStandardMaterial({ color: 0x9a6a48, roughness: 1, flatShading: true }));
    soil.position.set(WIDTH / 2, -0.81, DEPTH / 2);
    soil.receiveShadow = true;
    const crust = new THREE.Mesh(new THREE.BoxGeometry(WIDTH + 0.04, 0.22, DEPTH + 0.04), new THREE.MeshStandardMaterial({ color: 0x5d646b, roughness: 1 }));
    crust.position.set(WIDTH / 2, -0.12, DEPTH / 2);
    const rock = new THREE.Mesh(new THREE.BoxGeometry(WIDTH - 0.3, 0.5, DEPTH - 0.3), new THREE.MeshStandardMaterial({ color: 0x7a5238, roughness: 1 }));
    rock.position.set(WIDTH / 2, -1.75, DEPTH / 2);
    g.add(soil, crust, rock);
    return g;
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    ctx.renderer.toneMappingExposure = 1.15;
    ctx.scene.background = gradientTexture([[0, '#fbe3cf'], [0.6, '#e7d9e6'], [1, '#bccfe3']], true);
    ctx.scene.add(new THREE.HemisphereLight(0xd8ecff, 0xb08a70, 1.5));
    sun(ctx, 0xffe0bc, 3.4, [-0.7, 0.9, 0.45], { radius: 4, map: 2048 });
  },
  post(ctx) {
    const composer = composerFor(ctx);
    composer.addPass(gtao(ctx, 0.5, 0.8));
    composer.addPass(tiltShiftPass(0.5, 0.1, 9));
    finish(composer, gradePass({ saturation: 1.06, contrast: 1.03, highlightTint: [0.03, 0.015, 0], vignette: 0.28 }));
    return { composer };
  },
};

// ------------------------------------------------------ low-poly sci-fi

const ACID = hex('#8dff3a');
const polygon: ArtStyle = {
  id: 'polygon',
  family: 'aparcado',
  name: 'Low-poly sci-fi (tipo Synty)',
  tagline: 'Facetado y colores planos de paleta, noche alienígena con niebla y franjas de luz saturadas.',
  refs: ['Synty POLYGON Sci-Fi Outpost / Sci-Fi Worlds', 'Deep Rock Galactic', 'Astroneer', 'Grounded'],
  assets:
    'Comprar packs POLYGON (pago único por pack, 5 puestos) o SyntyPass (suscripción). Vienen en FBX/Unity: se pasan a glTF con Blender. Todos comparten una textura-paleta, así que mezclar packs Synty es coherente. Los personajes necesitan sus animaciones (Synty Animation o Mixamo).',
  cost: 'Medio',
  pros: [
    'Lo que más se parece a un juego terminado con menos esfuerzo propio',
    'Catálogo enorme y coherente: soldados, aliens, vehículos, bases, naturaleza',
    'El ambiente (niebla, bloom, luces de color) lo hace el motor, como en esta demo',
  ],
  cons: [
    'Muy reconocible: hay cientos de juegos con estos packs',
    'Coste en dinero y licencia por puesto; con suscripción, al cancelar no se puede seguir ampliando el juego',
    'Hay que convertir FBX → glTF y adaptar los esqueletos a nuestra animación',
  ],
  camera: 'persp',
  pixelScale: 0,
  props: 'kit',
  seg: 7,
  fxLights: true,
  role(role, spec) {
    const colors: Partial<Record<Role, string>> = {
      brick: '#4c5a6c',
      concrete: '#7f8c99',
      concreteDark: '#5a6470',
      metal: '#a3adb8',
      darkMetal: '#2b3240',
      paint: '#d8d3c6',
      paint2: '#3f6d8c',
      stripe: '#ff7a1a',
      wood: '#8a6a4a',
      woodDark: '#4f3b2a',
      fabric: '#8a8466',
      foliage: '#7c4fd0',
      bark: '#3d2b52',
      soil: '#2e2433',
      glass: '#1f3a4a',
      window: '#244050',
      alienMetal: '#3a2440',
    };
    const c = colors[role];
    if (role === 'lightWarm') return { ...spec, color: ACID.clone(), emissive: ACID.clone(), emissiveIntensity: 2.2 };
    if (role === 'lightCool') return { ...spec, color: hex('#ffb04a'), emissive: hex('#ff8a1a'), emissiveIntensity: 3.5 };
    if (role === 'window') return { ...spec, color: hex(c!), emissive: hex('#5ad8ff'), emissiveIntensity: 1.4 };
    return { ...spec, color: c ? hex(c) : shift(spec.color, 1.25, 1.05) };
  },
  unitSpec: (spec) => ({ ...spec, color: shift(spec.color, 1.3, 1.08), emissiveIntensity: spec.emissive ? 3 : undefined }),
  material: (spec) => standard(spec, { flatShading: true, roughness: Math.max(0.55, spec.roughness), metalness: Math.min(spec.metalness, 0.35) }),
  prop(group, ctx) {
    // Floor-level light strips on the outpost walls, as in the Synty kits.
    const kind = group.userData.kind as string;
    if (kind === 'wall' || kind === 'window' || kind === 'door') {
      const strip = new THREE.Mesh(
        new THREE.BoxGeometry(0.94, 0.05, 0.03),
        polygon.material({ color: ACID.clone(), emissive: ACID.clone(), emissiveIntensity: 1.6, metalness: 0, roughness: 1 }, ctx),
      );
      strip.position.set(0, 0.36, 0.52);
      group.add(strip);
    }
    return group;
  },
  rig(rig) {
    rig.root.traverse((o) => {
      if (o instanceof THREE.Mesh && o !== rig.flash) o.geometry = lowPoly(o.geometry);
    });
  },
  ground() {
    return streetGround(
      { px: 24, asphalt: '#46525a', sidewalk: '#6f7b86', curb: '#ff7a1a', paint: '#d9e86a', jitter: 0.05, wear: false, joints: true },
      (tex) => new THREE.MeshStandardMaterial({ map: tex.map, roughness: 0.8, flatShading: true }),
      new THREE.MeshStandardMaterial({ color: 0x241d2c, roughness: 1 }),
    );
  },
  environment(ctx) {
    const { scene, renderer } = ctx;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.35;
    scene.background = gradientTexture([[0, '#06121c'], [0.5, '#123444'], [1, '#2d5a5e']]);
    scene.fog = new THREE.FogExp2(0x24505a, 0.026);
    // A gas giant hanging over the outpost.
    const planet = new THREE.Mesh(new THREE.IcosahedronGeometry(30, 3), new THREE.MeshBasicMaterial({ color: 0x3d8f86, fog: false }));
    planet.position.set(-30, 32, -95);
    const rings = new THREE.Mesh(
      new THREE.RingGeometry(38, 52, 64),
      new THREE.MeshBasicMaterial({ color: 0x9fe8d8, transparent: true, opacity: 0.35, side: THREE.DoubleSide, fog: false }),
    );
    rings.position.copy(planet.position);
    rings.rotation.set(1.2, 0.3, 0.2);
    scene.add(planet, rings);
    scene.add(new THREE.HemisphereLight(0x6fb4d0, 0x3a2440, 2.4));
    sun(ctx, 0xa8dcff, 2.2, [-0.4, 1, -0.7], { radius: 2.5 });
    lampLights(ctx, 0.9, 1.2, true);
  },
  post(ctx) {
    const composer = composerFor(ctx);
    composer.addPass(gtao(ctx, 0.55, 1));
    composer.addPass(bloom(0.8, 0.55, 0.8));
    finish(composer, gradePass({ saturation: 1.15, contrast: 1.08, shadowTint: [0, 0.03, 0.04], highlightTint: [0.04, 0.02, 0], vignette: 0.42 }));
    return { composer };
  },
};

// ---------------------------------------------------------------- pixel

let pixelRamp: THREE.Texture | null = null;
const pixel: ArtStyle = {
  id: 'pixel',
  family: 'aparcado',
  name: 'Píxel 3D',
  tagline: '3D renderizado a baja resolución, paleta cerrada y contorno de un píxel.',
  refs: ['t3ssel8r (demos de píxel 3D)', 'A Short Hike', 'Octopath Traveler (HD-2D)', 'Hyper Light Drifter (paleta)'],
  assets:
    'Modelos sencillos con colores planos: la pixelación esconde la falta de detalle y unas animaciones simples bastan. Lo que esta demo ya tiene casi sirve tal cual.',
  cost: 'Bajo',
  pros: ['Identidad fuerte y muy indie', 'Perdona muchísimo: primitivas bien coloreadas ya funcionan', 'Barato de ejecutar'],
  cons: ['Detalles pequeños (armas, iconos) se pierden a distancia', 'Cámara y zoom deben ir en pasos fijos para que los píxeles no “bailen”', 'La interfaz tiene que ser pixel art a juego'],
  camera: 'ortho',
  pixelScale: 3,
  props: 'kit',
  seg: 8,
  fxLights: true,
  role: (_role, spec) => ({ ...spec, color: shift(spec.color, 1.2, 1.05) }),
  unitSpec: (spec) => ({ ...spec, color: shift(spec.color, 1.25, 1.1) }),
  material(spec) {
    pixelRamp ??= toonRamp([0.38, 0.7, 1]);
    return toon(spec, pixelRamp, null);
  },
  ground() {
    pixelRamp ??= toonRamp([0.38, 0.7, 1]);
    const ramp = pixelRamp;
    return streetGround(
      { px: 8, asphalt: '#5a6274', sidewalk: '#c9bda4', curb: '#e6dcc6', paint: '#f2d36b', jitter: 0.1, wear: false, joints: true, nearest: true },
      (tex) => new THREE.MeshToonMaterial({ map: tex.map, gradientMap: ramp }),
      new THREE.MeshToonMaterial({ color: 0x4a5163, gradientMap: ramp }),
    );
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.NeutralToneMapping;
    ctx.renderer.toneMappingExposure = 1;
    ctx.scene.background = gradientTexture([[0, '#2f5fa8'], [0.6, '#79b4e6'], [1, '#d6ecf2']]);
    ctx.scene.add(new THREE.HemisphereLight(0xb8d4ff, 0x5a4a52, 1.5));
    sun(ctx, 0xfff0d8, 3.2, [-0.8, 0.9, 0.3], { radius: 0, map: 1024 });
  },
  post(ctx) {
    const composer = composerFor(ctx, 0);
    const edges = new EdgePass(ctx.scene, ctx.camera, { mode: 1, thickness: 1, depthThreshold: 0.03, normalThreshold: 0.3 });
    edges.hidden = ctx.noEdges;
    composer.addPass(edges);
    composer.addPass(bloom(0.25, 0.15, 0.95));
    finish(composer, palettePass(PIXEL_PALETTE, 0.06));
    return { composer };
  },
};

// ---------------------------------------------------------------- voxel

const VOXEL = 1 / 16;
const voxel: ArtStyle = {
  id: 'voxel',
  family: 'voxel',
  name: 'Vóxel',
  tagline: 'Todo hecho de cubos: modelos tipo MagicaVoxel y destrucción natural.',
  refs: ['Teardown', 'Shadows of Doubt', 'Cube World', 'MagicaVoxel'],
  assets:
    'MagicaVoxel (gratis y muy fácil: es pintar con cubos) exportando a glTF. O vóxelizar automáticamente cualquier modelo, como hace esta demo con los provisionales.',
  cost: 'Bajo',
  pros: ['Un programador puede hacer los assets', 'La destrucción de coberturas de XCOM encaja perfecta (volar cubos)', 'Estética coherente y reconocible'],
  cons: ['Animar personajes vóxel por piezas queda rígido', 'Muchos polígonos si no se optimiza (greedy meshing)', 'Riesgo de “parece Minecraft”'],
  camera: 'persp',
  pixelScale: 0,
  props: 'kit',
  seg: 12,
  fxLights: true,
  role(role, spec) {
    if (role === 'window') return { ...spec, color: hex('#41607a'), emissive: undefined };
    if (role === 'glass') return { ...spec, color: hex('#2f4656'), opacity: 1 };
    return { ...spec, color: shift(spec.color, 1.1, 1.08) };
  },
  material: (spec) => standard(spec),
  prop: (group) => voxelizeProp(group, VOXEL),
  rig: (rig) => voxelizeRig(rig.root, VOXEL, new Set([rig.flash])),
  ground() {
    return streetGround(
      { px: 16, asphalt: '#555a60', sidewalk: '#a39c8c', curb: '#bdb5a4', paint: '#d9c35a', jitter: 0.16, wear: false, joints: true, nearest: true },
      (tex) => new THREE.MeshStandardMaterial({ map: tex.map, roughness: 0.95 }),
      new THREE.MeshStandardMaterial({ color: 0x4d5257, roughness: 0.95 }),
    );
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    ctx.renderer.toneMappingExposure = 1.2;
    ctx.scene.background = gradientTexture([[0, '#6fa6dc'], [0.6, '#b8cfe0'], [1, '#f4d2a2']]);
    ctx.scene.fog = new THREE.FogExp2(0xe8d2b4, 0.012);
    ctx.scene.add(new THREE.HemisphereLight(0xb4d6ff, 0x6b5a48, 1.2));
    sun(ctx, 0xffd29a, 3.8, [-0.9, 0.55, -0.25], { radius: 2, map: 4096 });
  },
  post(ctx) {
    const composer = composerFor(ctx);
    composer.addPass(gtao(ctx, 0.35, 1));
    composer.addPass(bloom(0.5, 0.4, 0.9));
    finish(composer, gradePass({ saturation: 1.08, contrast: 1.04, highlightTint: [0.04, 0.02, 0], vignette: 0.3 }));
    return { composer };
  },
};

// ----------------------------------------------------------------- holo

const HOLO_CYAN = hex('#38d8ff');
const HOLO_RED = hex('#ff4a6e');
const holo: ArtStyle = {
  id: 'holo',
  family: 'aparcado',
  name: 'Mesa holográfica',
  tagline: 'La batalla vista en la mesa de mando: siluetas, contornos de luz y barrido de escáner.',
  refs: ['Frozen Synapse', 'Mesa holográfica del Avenger (XCOM 2)', 'Invisible, Inc.', 'Tron'],
  assets: 'Casi nada: geometría simple de una pieza. Todo el aspecto lo pone el shader, así que cualquier modelo encaja.',
  cost: 'Muy bajo',
  pros: ['Lectura táctica máxima: equipos por color, coberturas claras', 'Diferenciador y baratísimo', 'Sirve también como “modo táctico” dentro de otro estilo'],
  cons: ['Frío: poca emoción y poco carisma de personajes', 'Efectos y explosiones tienen que rediseñarse a juego', 'Cansa en sesiones largas si no se matiza'],
  camera: 'persp',
  pixelScale: 0,
  props: 'kit',
  seg: 10,
  fxLights: false,
  unitSpec: (spec, def) => ({ ...spec, color: hex(def.color), alien: def.team === 'alien', role: undefined, emissive: spec.emissive }),
  material(spec, ctx) {
    if (!spec.role) return holoMaterial(ctx, spec.color.clone().multiplyScalar(spec.emissive ? 1.6 : 1), 0.14);
    const tint = spec.alien ? HOLO_RED : HOLO_CYAN;
    return holoMaterial(ctx, spec.emissive ? tint.clone().lerp(new THREE.Color(1, 1, 1), 0.3).multiplyScalar(1.6) : tint, 0.06);
  },
  prop(group) {
    let alien = false;
    group.traverse((o) => {
      if (o instanceof THREE.Mesh && (o.userData.role === 'alienMetal' || o.userData.role === 'alienGlow')) alien = true;
    });
    addEdges(group, (alien ? HOLO_RED : HOLO_CYAN).clone().multiplyScalar(0.9), 0.7);
    return group;
  },
  rig(rig, def) {
    const c = hex(def.color);
    rig.flash.userData.noEdges = true;
    addEdges(rig.root, c, 0.9);
  },
  debris: () => new THREE.MeshBasicMaterial({ color: 0x9feaff, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false }),
  ground() {
    const g = new THREE.Group();
    const plane = new THREE.Mesh(new THREE.PlaneGeometry(WIDTH, DEPTH).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ map: holoGridTexture(WIDTH, DEPTH) }));
    plane.position.set(WIDTH / 2, 0, DEPTH / 2);
    g.add(plane);
    const frame = new THREE.LineSegments(
      new THREE.EdgesGeometry(new THREE.BoxGeometry(WIDTH + 0.4, 0.3, DEPTH + 0.4)),
      new THREE.LineBasicMaterial({ color: 0x6fe6ff, transparent: true, opacity: 0.9, blending: THREE.AdditiveBlending }),
    );
    frame.position.set(WIDTH / 2, -0.15, DEPTH / 2);
    g.add(frame);
    return g;
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.NoToneMapping;
    ctx.scene.background = gradientTexture([[0, '#0b2230'], [0.5, '#04121a'], [1, '#010407']], true);
    ctx.scene.fog = new THREE.Fog(0x02070b, 32, 70);
  },
  post(ctx) {
    const composer = composerFor(ctx);
    composer.addPass(bloom(1.15, 0.55, 0.12));
    finish(composer, gradePass({ scanlines: 0.22, aberration: 0.004, grain: 0.035, vignette: 0.55, contrast: 1.05 }));
    return { composer };
  },
};

// ------------------------------------------------------------------ ink

let inkRamp: THREE.Texture | null = null;
let paper: THREE.Texture | null = null;
const PAPER = hex('#efe4cc');
const ink: ArtStyle = {
  id: 'tinta',
  family: 'tinta',
  name: 'Tinta (Moebius)',
  tagline: 'Línea entintada a mano, colores planos pastel y sombras tramadas sobre papel.',
  refs: ['Sable', 'Moebius (Jean Giraud)', 'Return of the Obra Dinn (tramado)', 'Breath of the Wild (paleta)'],
  assets:
    'Modelos low-poly limpios con colores planos; tinta, trama y papel los pone el shader. El arte conceptual en 2D (dibujado) se traslada casi directo.',
  cost: 'Bajo',
  pros: ['Muy original para un táctico', 'Las líneas ayudan a leer siluetas y coberturas', 'Coherente con ilustraciones 2D para menús, cartas y base'],
  cons: ['La trama puede hacer ruido con muchas unidades en pantalla', 'Efectos e interfaz deben “dibujarse” a juego', 'Menos espectacular en explosiones y luces'],
  camera: 'persp',
  pixelScale: 0,
  props: 'kit',
  seg: 10,
  fxLights: false,
  role(role, spec) {
    const colors: Partial<Record<Role, string>> = {
      brick: '#e09a76',
      concrete: '#ece2ca',
      concreteDark: '#c9bda3',
      metal: '#9fb6b8',
      darkMetal: '#6e7e86',
      paint: '#5e9aa8',
      paint2: '#e6b95c',
      wood: '#d8aa6c',
      woodDark: '#a87b4c',
      fabric: '#d4c391',
      foliage: '#93b673',
      bark: '#8a6a4c',
      stripe: '#d65a4a',
      alienMetal: '#7d5c8a',
      glass: '#9cc0c6',
      window: '#a9cfd4',
    };
    const c = colors[role];
    return { ...spec, color: c ? hex(c) : shift(spec.color, 0.8, 1.1, PAPER, 0.2), emissiveIntensity: spec.emissive ? 0.6 : undefined, opacity: 1 };
  },
  unitSpec: (spec) => ({ ...spec, color: shift(spec.color, 0.85, 1.12, PAPER, 0.18), emissiveIntensity: spec.emissive ? 0.6 : undefined }),
  material(spec) {
    inkRamp ??= toonRamp([0.62, 1]);
    return toon(spec, inkRamp, HATCHING);
  },
  debris(color) {
    inkRamp ??= toonRamp([0.62, 1]);
    return new THREE.MeshToonMaterial({ color, gradientMap: inkRamp, transparent: true });
  },
  ground() {
    inkRamp ??= toonRamp([0.62, 1]);
    const ramp = inkRamp;
    return streetGround(
      { px: 48, asphalt: '#cfc4ae', sidewalk: '#e9dfc8', curb: '#f6efdf', paint: '#f7f2e6', jitter: 0, wear: false, joints: true, sketch: true },
      (tex) => withMap(toon({ color: new THREE.Color(0xffffff), metalness: 0, roughness: 1 }, ramp, HATCHING), tex.map),
      new THREE.MeshToonMaterial({ color: 0xcfc4ae, gradientMap: ramp }),
    );
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.NoToneMapping;
    ctx.scene.background = PAPER.clone();
    ctx.scene.fog = new THREE.Fog(PAPER.clone(), 30, 75);
    ctx.scene.add(new THREE.HemisphereLight(0xfffaf0, 0xd9c9a8, 1.7));
    sun(ctx, 0xffffff, 2.2, [-0.6, 1, 0.4], { radius: 1 });
  },
  post(ctx) {
    paper ??= paperTexture();
    const composer = composerFor(ctx);
    const edges = new EdgePass(ctx.scene, ctx.camera, { color: 0x2a1e16, thickness: 1.3, depthThreshold: 0.018, normalThreshold: 0.3, wobble: 1.3 });
    edges.hidden = ctx.noEdges;
    composer.addPass(edges);
    finish(composer, gradePass({ paper, paperStrength: 0.75, saturation: 0.95, vignette: 0.12, grain: 0.02 }));
    return { composer };
  },
};

export { current, cinematic, polygon, diorama, comic, pixel, voxel, holo, ink };

