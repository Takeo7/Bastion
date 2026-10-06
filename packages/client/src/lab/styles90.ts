import * as THREE from 'three';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import type { MatSpec, Role } from './kit';
import { DEPTH, SIDEWALK_TO, WIDTH } from './layout';
import { EdgePass, gradePass, newsprintPass, notebookPass, palettePass, psxDitherPass, rampPalette, tiltShiftPass, vhsPass } from './post';
import { bloom, composerFor, finish, gtao, hex, shift, standard, streetGround, sun, toon, withMap, type ArtStyle, type FxKind, type HudCtx, type Pop } from './stylekit';
import { crunchyTexture, gradientTexture, paperTexture, starfieldTexture, toonRamp } from './textures';
import { voxelizeProp, voxelizeRig, type Colour, type VoxelOptions } from '../game/voxel';

// Second round (2026-10-04): 90s-nostalgia variants of the three styles the
// user liked (voxel, ink, comic) plus a PlayStation surprise.

const dpr = () => Math.min(window.devicePixelRatio || 1, 2);
const age = (p: Pop, time: number) => time - p.born;

/** Most recent effect event of the given kinds still within `seconds`. */
function latest(pops: Pop[], time: number, seconds: number, kinds?: FxKind[]): Pop | null {
  let best: Pop | null = null;
  for (const p of pops) {
    if (age(p, time) > seconds || (kinds && !kinds.includes(p.kind))) continue;
    if (!best || p.born > best.born) best = p;
  }
  return best;
}

function outlined(g: CanvasRenderingContext2D, text: string, x: number, y: number, fill: string, stroke: string, width: number): void {
  g.lineJoin = 'round';
  g.lineWidth = width;
  g.strokeStyle = stroke;
  g.strokeText(text, x, y);
  g.fillStyle = fill;
  g.fillText(text, x, y);
}

/** Two-tone cel shading with a tinted shadow colour instead of a darker one. */
function cel(shadow: [number, number, number], threshold = 0.85): string {
  return /* glsl */ `
    vec3 baseCol = diffuseColor.rgb;
    outgoingLight = (lit > ${threshold.toFixed(2)} ? baseCol : baseCol * vec3(${shadow.map((v) => v.toFixed(3)).join(', ')})) + totalEmissiveRadiance;
  `;
}

// ================================================================ VOXEL

// -------------------------------------------------- Westwood RTS, 1999

const WESTWOOD_PALETTE = rampPalette(
  ['#6d7480', '#7d7466', '#6f7340', '#a8843c', '#8a4a2c', '#4a6a8a', '#c8a040', '#c02020', '#40c040', '#40d0e0', '#c040a0', '#c09070', '#3fa9ff', '#ffa630', '#5a4a3a'],
  8,
).concat(['#000000']);

const lambertVoxel = (glow: boolean, vertexColors: boolean): THREE.Material =>
  glow ? new THREE.MeshBasicMaterial({ vertexColors }) : new THREE.MeshLambertMaterial({ vertexColors });
const WESTWOOD_VOXELS: VoxelOptions = { size: 1 / 12, ao: false, jitter: 0.06, material: lambertVoxel };

export const voxel99: ArtStyle = {
  id: 'voxel99',
  family: 'voxel',
  name: 'Vóxel RTS 1999',
  tagline: 'Isométrica fija, 640×480 y 256 colores: el vóxel de los RTS de Westwood.',
  refs: ['Command & Conquer: Tiberian Sun (1999)', 'Blade Runner (Westwood, 1997)', 'Red Alert 2 (2000)', 'Comanche (1992)'],
  assets:
    'Como el vóxel moderno (MagicaVoxel), pero con menos resolución y paleta cerrada: aún más fácil de producir y de mantener coherente. La paleta hace de “director de arte”.',
  cost: 'Bajo',
  pros: ['Nostalgia de RTS de los 90 inmediata', 'La paleta cerrada unifica todo aunque lo haga gente distinta', 'Lectura táctica clarísima con la cámara isométrica fija'],
  cons: ['Cámara fija: nada de cámara cinematográfica (o se pierde el encanto)', 'Baja resolución: los detalles pequeños se pierden', 'Puede parecer “demasiado retro” para algunos'],
  camera: 'ortho',
  preset: 'iso',
  pixelScale: 2,
  props: 'kit',
  seg: 10,
  fxLights: true,
  role: (_role, spec) => ({ ...spec, color: shift(spec.color, 0.9, 0.98) }),
  material: (spec) => standard(spec),
  prop: (group) => voxelizeProp(group, WESTWOOD_VOXELS),
  rig: (rig) => voxelizeRig(rig.root, WESTWOOD_VOXELS, new Set([rig.flash])),
  ground() {
    return streetGround(
      { px: 12, asphalt: '#57534a', sidewalk: '#8f8672', curb: '#a89e86', paint: '#c9b25a', jitter: 0.14, wear: true, joints: true, nearest: true },
      (tex) => new THREE.MeshLambertMaterial({ map: tex.map }),
      new THREE.MeshBasicMaterial({ color: 0x000000 }),
    );
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.NeutralToneMapping;
    ctx.renderer.toneMappingExposure = 1.1;
    ctx.scene.background = new THREE.Color(0x000000);
    ctx.scene.add(new THREE.HemisphereLight(0xc8d0ff, 0x40382c, 1.5));
    sun(ctx, 0xfff0d0, 3, [-0.5, 1, 0.25], { radius: 0 });
  },
  post(ctx) {
    const composer = composerFor(ctx, 0);
    composer.addPass(bloom(0.35, 0.2, 0.9));
    finish(composer, palettePass(WESTWOOD_PALETTE, 0.02));
    return { composer };
  },
  hud({ g, w, time, project, left }) {
    // Credits counter and an RTS selection bracket with health pips.
    g.font = '34px VT323, monospace';
    const credits = `$ ${4750 + Math.floor(time * 3) * 25}`;
    const tw = g.measureText(credits).width;
    g.fillStyle = 'rgba(0,0,0,0.75)';
    g.fillRect(w - tw - 44, 18, tw + 28, 40);
    g.strokeStyle = '#5a6a4a';
    g.strokeRect(w - tw - 44, 18, tw + 28, 40);
    g.fillStyle = '#9dff6a';
    g.fillText(credits, w - tw - 30, 48);
    const a = project([4.5, 0, 8.5]);
    const b = project([4.5, 1.9, 8.5]);
    const hgt = Math.abs(a.y - b.y);
    const x0 = a.x - hgt * 0.42;
    const y0 = b.y;
    const s = hgt * 0.84;
    g.strokeStyle = '#ffffff';
    g.lineWidth = 2;
    const c = 10;
    for (const [cx, cy, dx, dy] of [[x0, y0, 1, 1], [x0 + s, y0, -1, 1], [x0, y0 + hgt, 1, -1], [x0 + s, y0 + hgt, -1, -1]] as const) {
      g.beginPath();
      g.moveTo(cx + dx * c, cy);
      g.lineTo(cx, cy);
      g.lineTo(cx, cy + dy * c);
      g.stroke();
    }
    for (let i = 0; i < 8; i++) {
      g.fillStyle = i < 6 ? '#43e04a' : '#1d3a1d';
      g.fillRect(x0 + i * (s / 8) + 1, y0 - 9, s / 8 - 2, 5);
    }
    void left;
  },
};

// ------------------------------------------------------ toy bricks, 90s

const TOY_SOLID = ['#f2f3f2', '#a0a19f', '#635f52', '#1b1b1d', '#c4281c', '#0d69ac', '#f5cd30', '#287f47', '#d7c599', '#5c3a1e', '#da8641'];
const TOY_TRANS = ['#f7f18d', '#e00f1b', '#84d68d', '#9fd8ff', '#ff8ad0'];
const toLinear = (h: string) => new THREE.Color(h);
const TOY_SOLID_L = TOY_SOLID.map(toLinear);
const TOY_TRANS_L = TOY_TRANS.map(toLinear);

function nearest(c: { r: number; g: number; b: number }, list: THREE.Color[]): THREE.Color {
  // Compare in a roughly perceptual (gamma) space.
  const gm = (v: number) => Math.pow(Math.max(v, 0), 1 / 2.2);
  const r = gm(c.r);
  const gg = gm(c.g);
  const b = gm(c.b);
  let best = list[0]!;
  let bestD = Infinity;
  for (const p of list) {
    const d = 0.3 * (r - gm(p.r)) ** 2 + 0.55 * (gg - gm(p.g)) ** 2 + 0.15 * (b - gm(p.b)) ** 2;
    if (d < bestD) {
      bestD = d;
      best = p;
    }
  }
  return best;
}

function toyColour(c: Colour): Colour {
  if (c.glow) {
    const m = Math.max(c.r, c.g, c.b, 1e-4);
    const t = nearest({ r: c.r / m, g: c.g / m, b: c.b / m }, TOY_TRANS_L);
    return { r: t.r * 1.3, g: t.g * 1.3, b: t.b * 1.3, glow: true };
  }
  const s = nearest(c, TOY_SOLID_L);
  return { r: s.r, g: s.g, b: s.b, glow: false };
}

const plastic = (glow: boolean, vertexColors: boolean): THREE.Material =>
  glow
    ? new THREE.MeshBasicMaterial({ vertexColors, transparent: true, opacity: 0.82 })
    : new THREE.MeshPhysicalMaterial({ vertexColors, roughness: 0.3, metalness: 0, clearcoat: 0.6, clearcoatRoughness: 0.15 });
const BRICKS: VoxelOptions = { size: [0.125, 0.15, 0.125], ao: true, jitter: 0, quantize: toyColour, studs: true, material: plastic };

export const bloques: ArtStyle = {
  id: 'bloques',
  family: 'voxel',
  name: 'Bloques de juguete (1992)',
  tagline: 'El catálogo de juguetes de los 90: piezas de plástico brillante con sus “studs”, sobre una base gris en el espacio.',
  refs: ['Catálogos LEGO Space: Classic Space, Blacktron, M-Tron (1979–1992)', 'Micro Machines', 'Fotos de catálogo de juguetes', 'LEGO Island (1997)'],
  assets:
    'Mismo flujo que el vóxel (MagicaVoxel o vóxelizado automático) con vóxeles de proporción de ladrillo y paleta de juguete; los studs y el plástico los pone el motor.',
  cost: 'Bajo',
  pros: ['Nostalgia directísima y muy “fotografiable”', 'El tono de juguete suaviza la muerte permanente sin quitarle tensión', 'Destrucción de coberturas en piezas: espectacular'],
  cons: ['Ojo con la marca y el aspecto protegido de LEGO: habría que alejarse del ladrillo exacto', 'Muchas instancias de studs: hay que optimizar', 'Tono infantil para una invasión alienígena'],
  camera: 'persp',
  pixelScale: 0,
  props: 'kit',
  seg: 12,
  fxLights: true,
  material: (spec) => standard(spec),
  prop: (group) => voxelizeProp(group, BRICKS),
  rig: (rig) => voxelizeRig(rig.root, BRICKS, new Set([rig.flash])),
  debris: (color) => new THREE.MeshPhysicalMaterial({ color: nearest(new THREE.Color(color), TOY_SOLID_L), roughness: 0.3, clearcoat: 0.6, transparent: true }),
  ground() {
    const g = streetGround(
      { px: 32, asphalt: '#45463e', sidewalk: '#a0a19f', curb: '#a0a19f', paint: '#d4d5d2', jitter: 0, wear: false, joints: false },
      (tex) => new THREE.MeshStandardMaterial({ map: tex.map, roughness: 0.6 }),
      null,
    );
    const plate = new THREE.Mesh(new THREE.BoxGeometry(WIDTH, 0.08, DEPTH), new THREE.MeshStandardMaterial({ color: 0xa0a19f, roughness: 0.6 }));
    plate.position.set(WIDTH / 2, -0.041, DEPTH / 2);
    plate.receiveShadow = true;
    // Studs on the sidewalk, like a grey baseplate (the road plate is smooth).
    const pitch = 0.125;
    const nx = Math.round(WIDTH / pitch);
    const nz = Math.round(SIDEWALK_TO / pitch) - 1;
    const studs = new THREE.InstancedMesh(
      new THREE.CylinderGeometry(0.3 * pitch, 0.3 * pitch, 0.027, 10),
      new THREE.MeshPhysicalMaterial({ color: 0xa0a19f, roughness: 0.3, clearcoat: 0.6 }),
      nx * nz,
    );
    const m = new THREE.Matrix4();
    let i = 0;
    for (let x = 0; x < nx; x++) {
      for (let z = 0; z < nz; z++) {
        m.makeTranslation((x + 0.5) * pitch, 0.0135, (z + 0.5) * pitch);
        studs.setMatrixAt(i++, m);
      }
    }
    studs.receiveShadow = true;
    g.add(plate, studs);
    return g;
  },
  environment(ctx) {
    const { scene, renderer } = ctx;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1;
    scene.background = starfieldTexture();
    const pmrem = new THREE.PMREMGenerator(renderer);
    scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
    scene.environmentIntensity = 0.35;
    pmrem.dispose();
    scene.add(new THREE.HemisphereLight(0xffffff, 0x3a4058, 1));
    sun(ctx, 0xffffff, 2.6, [-0.5, 1, 0.6], { radius: 3 });
  },
  post(ctx) {
    const composer = composerFor(ctx);
    composer.addPass(gtao(ctx, 0.3, 0.8));
    composer.addPass(bloom(0.3, 0.3, 1.3));
    composer.addPass(tiltShiftPass(0.52, 0.17, 5));
    finish(composer, gradePass({ saturation: 1.08, contrast: 1.04, vignette: 0.2 }));
    return { composer };
  },
  hud({ g, w, left }) {
    // Catalogue label: set number badge, name and age range.
    const x = left + 24;
    const y = 24;
    g.save();
    g.fillStyle = '#f5cd30';
    g.strokeStyle = '#1b1b1d';
    g.lineWidth = 3;
    g.beginPath();
    g.roundRect(x, y, 112, 52, 8);
    g.fill();
    g.stroke();
    g.font = 'italic 34px "Archivo Black", "Arial Black", sans-serif';
    g.fillStyle = '#1b1b1d';
    g.fillText('6987', x + 10, y + 40);
    g.font = 'italic 30px "Archivo Black", "Arial Black", sans-serif';
    outlined(g, 'PUESTO BASTIÓN', x + 128, y + 34, '#ffffff', '#0d3a7a', 6);
    g.font = '600 16px Rajdhani, sans-serif';
    g.fillStyle = '#d8e4ff';
    g.fillText('412 piezas · 7–12 años · Serie Espacio', x + 130, y + 54);
    // "New!" starburst in the top-right corner.
    const cx = w - 90;
    const cy = 70;
    g.translate(cx, cy);
    g.rotate(-0.25);
    g.beginPath();
    for (let k = 0; k < 24; k++) {
      const r = k % 2 ? 40 : 54;
      const a = (k / 24) * Math.PI * 2;
      g.lineTo(Math.cos(a) * r, Math.sin(a) * r);
    }
    g.closePath();
    g.fillStyle = '#c4281c';
    g.fill();
    g.font = 'italic 22px "Archivo Black", "Arial Black", sans-serif';
    g.textAlign = 'center';
    outlined(g, '¡NUEVO!', 0, 8, '#ffffff', '#5a0a06', 4);
    g.restore();
  },
};

// ================================================================= TINTA

// --------------------------------------------------- newsstand comic

let newsPaper: THREE.Texture | null = null;
let flatRamp: THREE.Texture | null = null;
const flat = () => (flatRamp ??= toonRamp([0.55, 1]));

const ONOMATOPOEIA: Record<FxKind, string> = {
  explosion: '¡BADABUM!',
  xcomShot: '¡BLAM!',
  alienShot: '¡ZZZAP!',
  hit: '¡ARGH!',
  grenade: '¡GRANADA VA!',
};

function starburst(g: CanvasRenderingContext2D, r: number, spikes: number, seed: number): void {
  g.beginPath();
  for (let k = 0; k < spikes * 2; k++) {
    const jitter = 0.85 + (((seed * 9301 + k * 49297) % 233) / 233) * 0.3;
    const rr = (k % 2 ? r * 0.62 : r) * jitter;
    const a = (k / (spikes * 2)) * Math.PI * 2;
    g.lineTo(Math.cos(a) * rr * 1.25, Math.sin(a) * rr);
  }
  g.closePath();
}

export const tebeo: ArtStyle = {
  id: 'tebeo',
  family: 'tinta',
  name: 'Tebeo de quiosco',
  tagline: 'Papel de periódico amarillento, trama CMYK descuadrada y tinta negra: el cómic de 125 pesetas.',
  refs: ['Comix Zone (Sega, 1995)', 'Cómics Forum de Marvel en el quiosco', 'Mortadelo y Filemón', 'Spider-Man de los 90 (Todd McFarlane)'],
  assets:
    'Modelos low-poly con colores planos y primarios. La trama de imprenta, el papel y el registro desajustado los pone el posproceso; rótulos y onomatopeyas son 2D.',
  cost: 'Bajo',
  pros: ['Nostalgia de quiosco muy reconocible', 'Las onomatopeyas convierten cada disparo en viñeta', 'Base perfecta para cinemáticas en viñetas y menús de cómic'],
  cons: ['La trama puede “vibrar” al mover la cámara', 'Mucho ruido visual si hay muchas unidades', 'La interfaz debe parecer rotulada a mano'],
  camera: 'persp',
  pixelScale: 0,
  props: 'kit',
  seg: 10,
  fxLights: false,
  role: (_role, spec) => ({ ...spec, color: shift(spec.color, 1.3, 1.08), emissiveIntensity: spec.emissive ? 0.8 : undefined }),
  unitSpec: (spec) => ({ ...spec, color: shift(spec.color, 1.35, 1.1), emissiveIntensity: spec.emissive ? 0.8 : undefined }),
  material: (spec) => toon(spec, flat(), null),
  debris: (color) => new THREE.MeshToonMaterial({ color, gradientMap: flat(), transparent: true }),
  ground() {
    return streetGround(
      { px: 32, asphalt: '#8a9ab8', sidewalk: '#f0e2b8', curb: '#f8f0dc', paint: '#ffd23a', jitter: 0, wear: false, joints: true },
      (tex) => withMap(toon({ color: new THREE.Color(0xffffff), metalness: 0, roughness: 1 }, flat(), null), tex.map),
      new THREE.MeshToonMaterial({ color: 0x8a9ab8, gradientMap: flat() }),
    );
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.NeutralToneMapping;
    ctx.renderer.toneMappingExposure = 1;
    ctx.scene.background = new THREE.Color('#8fd0f0');
    ctx.scene.add(new THREE.HemisphereLight(0xffffff, 0x9a8a7a, 1.7));
    sun(ctx, 0xffffff, 2.6, [-0.6, 1, 0.4], { radius: 1 });
  },
  post(ctx) {
    newsPaper ??= paperTexture();
    const composer = composerFor(ctx);
    const edges = new EdgePass(ctx.scene, ctx.camera, { color: 0x000000, thickness: 1.8, depthThreshold: 0.02, normalThreshold: 0.35, wobble: 0.6 });
    edges.hidden = ctx.noEdges;
    composer.addPass(edges);
    finish(composer, newsprintPass(newsPaper, 5.5 * dpr()));
    return { composer };
  },
  hud({ g, w, h, time, pops, left, bottom }) {
    // Panel border with a paper gutter.
    const m = 12;
    g.fillStyle = '#efe2bd';
    g.fillRect(0, 0, w, m);
    g.fillRect(0, h - m, w, m);
    g.fillRect(0, 0, m, h);
    g.fillRect(w - m, 0, m, h);
    g.lineWidth = 4;
    g.strokeStyle = '#111';
    g.strokeRect(m, m, w - 2 * m, h - 2 * m);
    // Caption box.
    g.font = '24px Bangers, Impact, sans-serif';
    const caption = 'MIENTRAS TANTO, EN EL DISTRITO COMERCIAL...';
    const cw = g.measureText(caption).width + 28;
    const cx = Math.max(left + 20, m + 2);
    g.fillStyle = '#ffe066';
    g.fillRect(cx, m + 2, cw, 42);
    g.lineWidth = 3;
    g.strokeRect(cx, m + 2, cw, 42);
    g.fillStyle = '#111';
    g.fillText(caption, cx + 14, m + 32);
    void bottom;
    // Onomatopoeias.
    for (const p of pops) {
      const a = age(p, time);
      if (a > 1.1) continue;
      const k = Math.min(1, a / 0.1) * (1 + Math.max(0, 0.1 - Math.abs(a - 0.1)) * 2);
      g.save();
      if (p.kind === 'grenade') {
        // Speech balloon from the thrower.
        g.translate(p.x - 40, p.y - 90);
        g.scale(k, k);
        g.font = '26px Bangers, Impact, sans-serif';
        const text = ONOMATOPOEIA.grenade;
        const tw = g.measureText(text).width;
        g.beginPath();
        g.ellipse(0, 0, tw / 2 + 22, 30, 0, 0, Math.PI * 2);
        g.moveTo(10, 26);
        g.lineTo(34, 76);
        g.lineTo(28, 24);
        g.fillStyle = '#ffffff';
        g.fill();
        g.lineWidth = 3;
        g.strokeStyle = '#111';
        g.stroke();
        g.fillStyle = '#111';
        g.textAlign = 'center';
        g.fillText(text, 0, 9);
        g.restore();
        continue;
      }
      const big = p.kind === 'explosion';
      g.translate(p.x + (big ? 0 : 30), p.y - (big ? 60 : 40));
      g.rotate((p.seed - 0.5) * 0.5);
      g.scale(k, k);
      starburst(g, big ? 78 : 40, big ? 14 : 9, Math.floor(p.seed * 1000));
      g.fillStyle = big ? '#ffd23a' : '#ffffff';
      g.fill();
      g.lineWidth = 3;
      g.strokeStyle = '#111';
      g.stroke();
      g.font = `${big ? 52 : 28}px Bangers, Impact, sans-serif`;
      g.textAlign = 'center';
      outlined(g, ONOMATOPOEIA[p.kind], 0, big ? 18 : 10, big ? '#e8322a' : '#1f6fd0', '#111', big ? 6 : 4);
      g.restore();
    }
  },
};

// ------------------------------------------------------ school notebook

let notePaper: THREE.Texture | null = null;
const BIC = 'vec3(0.13, 0.2, 0.62)';
const BLUE_HATCH = /* glsl */ `
  vec3 baseCol = diffuseColor.rgb;
  float s1 = fract((gl_FragCoord.x + gl_FragCoord.y) / 6.0);
  float s2 = fract((gl_FragCoord.x - gl_FragCoord.y) / 6.0);
  float h = 0.0;
  if (lit < 1.05) h = smoothstep(0.8, 0.92, s1);
  if (lit < 0.62) h = max(h, smoothstep(0.8, 0.92, s2));
  outgoingLight = mix(baseCol, ${BIC}, h * 0.75);
`;
const HIGHLIGHTER: Partial<Record<Role, string>> = {
  foliage: '#b8f27a',
  stripe: '#ff9ad6',
  lightWarm: '#fff27a',
  lightCool: '#fff27a',
  window: '#cfeaff',
  glass: '#cfeaff',
  alienGlow: '#ff9ad6',
  alienMetal: '#ffe3f4',
  paint: '#aee0ff',
  paint2: '#fff27a',
};

const DOODLE: Record<FxKind, string> = {
  explosion: '¡¡BOOOM!!',
  xcomShot: 'pium pium',
  alienShot: '¡zzzt!',
  hit: '¡ay!',
  grenade: '¡granada!',
};

function arrow(g: CanvasRenderingContext2D, x0: number, y0: number, x1: number, y1: number): void {
  g.beginPath();
  g.moveTo(x0, y0);
  g.quadraticCurveTo((x0 + x1) / 2 + 30, (y0 + y1) / 2 - 20, x1, y1);
  g.stroke();
  const a = Math.atan2(y1 - ((y0 + y1) / 2 - 20), x1 - ((x0 + x1) / 2 + 30));
  g.beginPath();
  g.moveTo(x1, y1);
  g.lineTo(x1 - Math.cos(a - 0.45) * 14, y1 - Math.sin(a - 0.45) * 14);
  g.moveTo(x1, y1);
  g.lineTo(x1 - Math.cos(a + 0.45) * 14, y1 - Math.sin(a + 0.45) * 14);
  g.stroke();
}

export const cuaderno: ArtStyle = {
  id: 'cuaderno',
  family: 'tinta',
  name: 'Cuaderno de clase',
  tagline: 'Boli BIC azul y subrayadores fluorescentes sobre la cuadrícula: las batallas que dibujábamos en clase.',
  refs: ['Las batallas en el margen del cuaderno (EGB, 90s)', 'Bolígrafo BIC Cristal', 'Subrayadores fluorescentes', 'Sketch Turner en Comix Zone (1995)'],
  assets:
    'Casi nada: geometría sencilla en blanco. El trazo de boli, el sombreado a rayas, los fosforitos y el papel cuadriculado son posproceso; las anotaciones, 2D.',
  cost: 'Muy bajo',
  pros: ['Muy original y entrañable: nadie lo espera en un táctico', 'Barato y coherente: todo sale del shader', 'Encaja con un tono de humor y con menús de “cuaderno de campaña”'],
  cons: ['Poco espectacular en efectos y explosiones', 'Lectura de equipos solo por color de fosforito', 'Puede cansar en partidas largas; mejor con variaciones por misión'],
  camera: 'persp',
  pixelScale: 0,
  props: 'kit',
  seg: 10,
  fxLights: false,
  role: (role, spec) => ({ ...spec, color: hex(HIGHLIGHTER[role] ?? '#ffffff'), emissive: undefined, emissiveIntensity: undefined, opacity: 1 }),
  unitSpec(spec) {
    const hsl = { h: 0, s: 0, l: 0 };
    spec.color.getHSL(hsl);
    const ink = hsl.s < 0.35 || hsl.l < 0.12 ? new THREE.Color(1, 1, 1) : new THREE.Color().setHSL(hsl.h, 1, 0.68);
    return { ...spec, color: ink, emissive: undefined, emissiveIntensity: undefined };
  },
  material: (spec) => toon(spec, flat(), BLUE_HATCH),
  debris: () => new THREE.MeshBasicMaterial({ color: 0x2a3a9a, transparent: true }),
  ground() {
    return streetGround(
      { px: 32, asphalt: '#ffffff', sidewalk: '#ffffff', curb: '#c8d4f0', paint: '#b8c6ee', jitter: 0, wear: false, joints: false, sketch: true, sketchColor: 'rgba(40,60,160,0.14)' },
      (tex) => withMap(toon({ color: new THREE.Color(0xffffff), metalness: 0, roughness: 1 }, flat(), BLUE_HATCH), tex.map),
      null,
    );
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.NoToneMapping;
    ctx.scene.background = new THREE.Color(0xffffff);
    ctx.scene.add(new THREE.HemisphereLight(0xffffff, 0xffffff, 1.6));
    sun(ctx, 0xffffff, 2.6, [-0.6, 1, 0.45], { radius: 1 });
  },
  post(ctx) {
    notePaper ??= paperTexture();
    const composer = composerFor(ctx);
    const pen = new EdgePass(ctx.scene, ctx.camera, { color: 0x22339e, thickness: 1.2, depthThreshold: 0.018, normalThreshold: 0.3, wobble: 1.8 });
    const retrace = new EdgePass(ctx.scene, ctx.camera, { color: 0x22339e, thickness: 1, depthThreshold: 0.02, normalThreshold: 0.35, wobble: 2.6, seed: 3, opacity: 0.5 });
    pen.hidden = retrace.hidden = ctx.noEdges;
    composer.addPass(pen);
    composer.addPass(retrace);
    finish(composer, notebookPass(notePaper, 22 * dpr()));
    return { composer };
  },
  hud({ g, w, time, pops, project, left }) {
    const ink = '#22339e';
    g.fillStyle = ink;
    g.strokeStyle = ink;
    const tx = Math.max(left + 24, w * 0.07 + 18);
    g.font = '700 38px Caveat, cursive';
    g.fillText('Batalla del Distrito Comercial', tx, 52);
    g.lineWidth = 2;
    g.beginPath();
    for (let x = 0; x < 430; x += 6) g.lineTo(tx + x, 62 + Math.sin(x * 0.18) * 2.5);
    g.stroke();
    g.font = '500 26px Caveat, cursive';
    g.textAlign = 'right';
    g.fillText('martes, 14 de mayo de 1996', w - 28, 44);
    g.textAlign = 'left';
    // Doodled labels pointing at each side.
    const aliens = project([13.8, 2.4, 6.6]);
    g.font = '700 32px Caveat, cursive';
    g.fillText('¡¡ALIENS!!', aliens.x + 130, aliens.y - 150);
    g.lineWidth = 2;
    arrow(g, aliens.x + 160, aliens.y - 138, aliens.x + 30, aliens.y - 30);
    const squad = project([3.2, 2.2, 9.5]);
    g.fillText('nosotros :)', squad.x - 150, squad.y + 120);
    arrow(g, squad.x - 100, squad.y + 98, squad.x - 10, squad.y + 30);
    for (const p of pops) {
      const a = age(p, time);
      if (a > 1.2 || p.kind === 'grenade') continue;
      const big = p.kind === 'explosion';
      g.save();
      g.translate(p.x + (big ? -40 : 24), p.y - (big ? 70 : 34));
      g.rotate((p.seed - 0.5) * 0.4);
      g.font = `700 ${big ? 54 : 28}px Caveat, cursive`;
      g.fillText(DOODLE[p.kind], 0, 0);
      if (big) {
        g.lineWidth = 2;
        for (let k = 0; k < 10; k++) {
          const ang = (k / 10) * Math.PI * 2;
          g.beginPath();
          g.moveTo(90 + Math.cos(ang) * 95, -16 + Math.sin(ang) * 50);
          g.lineTo(90 + Math.cos(ang) * 125, -16 + Math.sin(ang) * 66);
          g.stroke();
        }
      }
      g.restore();
    }
  },
};

// ================================================================= CÓMIC

// --------------------------------------------- 90s cartoon taped on VHS

let celRamp: THREE.Texture | null = null;
const celGradient = () => (celRamp ??= toonRamp([0.5, 1]));
const VHS_CEL = cel([0.5, 0.44, 0.8]);

export const vhs: ArtStyle = {
  id: 'vhs',
  family: 'comic',
  name: 'Dibujos animados en VHS',
  tagline: 'La serie de la tarde grabada en cinta: cel de dos tonos con sombra violeta, contorno grueso, color que sangra y ruido de tracking.',
  refs: ['X-Men: la serie animada (1992)', 'Batman: la serie animada (1992)', 'Las Tortugas Ninja (1987)', 'Cintas VHS grabadas de la tele'],
  assets:
    'Low-poly con colores planos muy saturados; el cel, el contorno y el efecto de cinta son shader. Funciona con modelos sencillos porque la cinta “perdona” el detalle.',
  cost: 'Bajo',
  pros: ['Nostalgia instantánea (el OSD de “PLAY” lo dice todo)', 'Colores y siluetas muy legibles', 'El filtro VHS puede ser solo para repeticiones y cinemáticas'],
  cons: ['El ruido de cinta cansa como filtro permanente', 'Pierde nitidez para leer iconos pequeños', 'Necesita interfaz y tipografías a juego'],
  camera: 'persp',
  pixelScale: 2,
  pixelSmooth: true,
  props: 'kit',
  seg: 12,
  fxLights: true,
  role: (_role, spec) => ({ ...spec, color: shift(spec.color, 1.45, 1.06) }),
  unitSpec: (spec) => ({ ...spec, color: shift(spec.color, 1.5, 1.1) }),
  material: (spec) => toon(spec, celGradient(), VHS_CEL),
  debris: (color) => new THREE.MeshToonMaterial({ color, gradientMap: celGradient(), transparent: true }),
  ground() {
    return streetGround(
      { px: 24, asphalt: '#5b5868', sidewalk: '#e2b88e', curb: '#f2dcbc', paint: '#ffd23a', jitter: 0, wear: false, joints: true },
      (tex) => withMap(toon({ color: new THREE.Color(0xffffff), metalness: 0, roughness: 1 }, celGradient(), VHS_CEL), tex.map),
      new THREE.MeshToonMaterial({ color: 0x4a4858, gradientMap: celGradient() }),
    );
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.NeutralToneMapping;
    ctx.renderer.toneMappingExposure = 1;
    ctx.scene.background = gradientTexture([[0, '#2a3f9a'], [0.55, '#8a7fd0'], [1, '#ffb37a']]);
    ctx.scene.add(new THREE.HemisphereLight(0xb0b8ff, 0x5a4060, 1.6));
    sun(ctx, 0xffd8a8, 3.2, [-0.8, 0.85, 0.3], { radius: 0 });
  },
  post(ctx) {
    const composer = composerFor(ctx, 0);
    const edges = new EdgePass(ctx.scene, ctx.camera, { color: 0x0a0812, thickness: 1.1, depthThreshold: 0.02, normalThreshold: 0.35 });
    edges.hidden = ctx.noEdges;
    composer.addPass(edges);
    composer.addPass(bloom(0.3, 0.3, 0.85));
    finish(composer, vhsPass());
    return { composer };
  },
  hud({ g, w, h, time, left, bottom }) {
    g.font = '40px VT323, monospace';
    const osd = (text: string, x: number, y: number) => {
      g.fillStyle = 'rgba(255,40,60,0.55)';
      g.fillText(text, x - 2, y);
      g.fillStyle = 'rgba(40,220,255,0.55)';
      g.fillText(text, x + 2, y);
      g.fillStyle = '#f4f4f4';
      g.fillText(text, x, y);
    };
    osd('PLAY ▶', left + 40, 64);
    const t = 12 * 60 + 34 + Math.floor(time);
    const counter = `SP  ${Math.floor(t / 3600)}:${String(Math.floor(t / 60) % 60).padStart(2, '0')}:${String(t % 60).padStart(2, '0')}`;
    g.textAlign = 'right';
    osd(counter, w - 40, h - bottom - 30);
    g.textAlign = 'left';
  },
};

// ---------------------------------------------------------- anime OVA

const ANIME_CEL = cel([0.56, 0.6, 0.76]);
const SUBTITLES: Record<FxKind, string> = {
  grenade: 'Granadera: ¡Granada fuera! ¡Cubríos!',
  explosion: 'Tiradora: ... ¿Les hemos dado?',
  xcomShot: 'Tiradora: Objetivo a la vista. Disparo.',
  alienShot: 'Asalto: ¡Fuego enemigo desde el relé!',
  hit: 'Tiradora: Impacto confirmado.',
};

export const anime: ArtStyle = {
  id: 'anime',
  family: 'comic',
  name: 'Anime OVA (1995)',
  tagline: 'Cel con sombras frías, cielo pintado al atardecer, resplandor difuso, grano de película y animación a doce dibujos por segundo.',
  refs: ['Ghost in the Shell (1995)', 'Patlabor 2 (1993)', 'Akira (1988)', 'Neon Genesis Evangelion (1995)'],
  assets:
    'Low-poly con colores apagados y sombras de color; las poses clave importan más que la animación fluida (se anima “a dos”). El cielo y los fondos pueden ser pinturas 2D.',
  cost: 'Medio',
  pros: ['Tono serio y melancólico, ideal para la campaña', 'La animación “a dos” disimula animaciones baratas', 'Cinemáticas con subtítulos y encuadres de película'],
  cons: ['Exige buen gusto de color y fondos pintados', 'Las bandas negras quitan espacio a la vista táctica (solo para cinemáticas)', 'Referencia muy concreta: puede no gustar a todos'],
  camera: 'persp',
  pixelScale: 0,
  stepped: 12,
  props: 'kit',
  seg: 12,
  fxLights: true,
  role: (_role, spec) => ({ ...spec, color: shift(spec.color, 0.72, 1.02, hex('#9fb0a8'), 0.15) }),
  unitSpec: (spec) => ({ ...spec, color: shift(spec.color, 0.85, 1.05) }),
  material: (spec) => toon(spec, celGradient(), ANIME_CEL),
  debris: (color) => new THREE.MeshToonMaterial({ color, gradientMap: celGradient(), transparent: true }),
  ground() {
    return streetGround(
      { px: 48, asphalt: '#6a7a7c', sidewalk: '#b4ae9e', curb: '#cbc4b2', paint: '#e2d49c', jitter: 0.03, wear: true, joints: true },
      (tex) => withMap(toon({ color: new THREE.Color(0xffffff), metalness: 0, roughness: 1 }, celGradient(), ANIME_CEL), tex.map),
      new THREE.MeshToonMaterial({ color: 0x3e4a4e, gradientMap: celGradient() }),
    );
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.NeutralToneMapping;
    ctx.renderer.toneMappingExposure = 1;
    ctx.scene.background = gradientTexture([[0, '#1f3442'], [0.55, '#5f7f80'], [1, '#e0b48a']]);
    ctx.scene.fog = new THREE.Fog(0x7f9490, 24, 62);
    ctx.scene.add(new THREE.HemisphereLight(0x9fc0c8, 0x40303a, 1.3));
    sun(ctx, 0xffc89a, 3.2, [-1, 0.8, 0.2], { radius: 0 });
  },
  post(ctx) {
    const composer = composerFor(ctx);
    const edges = new EdgePass(ctx.scene, ctx.camera, { color: 0x2a2226, thickness: 1, depthThreshold: 0.02, normalThreshold: 0.4, opacity: 0.85 });
    edges.hidden = ctx.noEdges;
    composer.addPass(edges);
    composer.addPass(bloom(0.4, 0.85, 0.72));
    finish(composer, gradePass({ saturation: 0.88, contrast: 1.06, grain: 0.07, vignette: 0.35, weave: 1.2 }));
    return { composer };
  },
  hud({ g, w, h, time, pops, left }) {
    const bar = Math.max(0, (h - w / 1.85) / 2);
    g.fillStyle = '#000';
    g.fillRect(0, 0, w, bar);
    g.fillRect(0, h - bar, w, bar);
    g.font = '13px Arial, sans-serif';
    g.fillStyle = 'rgba(255,255,255,0.7)';
    g.fillText('[Bastión-Fansub] OVA 01 «Distrito Comercial» [VHSRip]', left + 20, Math.max(18, bar - 10));
    const p = latest(pops, time, 2.4);
    if (p) {
      g.font = 'bold 28px Arial, sans-serif';
      g.textAlign = 'center';
      outlined(g, SUBTITLES[p.kind], w / 2 + left / 2, h - bar - 26, '#fff36b', '#000', 5);
      g.textAlign = 'left';
    }
  },
};

// ============================================================== SORPRESA

// ----------------------------------------------- PlayStation tactics, 1997

const snap = { value: new THREE.Vector2(320, 240) };
let crunch: THREE.Texture | null = null;

/** PS1 rendering quirks: vertices snap to a 320×240 grid and textures map affinely (they swim). */
function psxify<T extends THREE.Material>(m: T): T {
  m.onBeforeCompile = (shader) => {
    shader.uniforms.snapRes = snap;
    shader.vertexShader =
      'uniform vec2 snapRes;\nvarying float vAffine;\n' +
      shader.vertexShader.replace(
        '#include <project_vertex>',
        /* glsl */ `#include <project_vertex>
        vec4 snapped = gl_Position;
        snapped.xyz /= snapped.w;
        snapped.xy = floor(snapped.xy * snapRes * 0.5) / (snapRes * 0.5);
        snapped.xyz *= snapped.w;
        gl_Position = snapped;
        vAffine = gl_Position.w;
        #ifdef USE_MAP
          vMapUv *= gl_Position.w;
        #endif`,
      );
    shader.fragmentShader =
      'varying float vAffine;\n' +
      shader.fragmentShader.replace(
        '#include <map_fragment>',
        /* glsl */ `#ifdef USE_MAP
          diffuseColor *= texture2D(map, vMapUv / vAffine);
        #endif`,
      );
  };
  m.customProgramCacheKey = () => 'psx';
  return m;
}

function psxWindow(g: CanvasRenderingContext2D, x: number, y: number, w: number, h: number): void {
  const grad = g.createLinearGradient(0, y, 0, y + h);
  grad.addColorStop(0, '#3446b0');
  grad.addColorStop(1, '#0b1048');
  g.fillStyle = grad;
  g.beginPath();
  g.roundRect(x, y, w, h, 7);
  g.fill();
  g.lineWidth = 3;
  g.strokeStyle = '#e9e9f2';
  g.stroke();
  g.lineWidth = 1;
  g.strokeStyle = '#7a80a8';
  g.beginPath();
  g.roundRect(x + 4, y + 4, w - 8, h - 8, 5);
  g.stroke();
}

export const psx: ArtStyle = {
  id: 'psx',
  family: 'sorpresa',
  name: 'Tácticas de PlayStation (1997)',
  tagline: 'Vértices que tiemblan, texturas que se deforman, niebla de distancia, sombras de mancha y color de 15 bits tramado.',
  refs: ['Vandal Hearts (1996)', 'Final Fantasy Tactics (1997)', 'Front Mission 3 (1999)', 'Metal Gear Solid (1998)'],
  assets:
    'Modelos de pocos polígonos con texturas pequeñas pintadas (64–128 px). Es el retro de moda en el indie actual: hay muchos packs “PSX” baratos o gratis y se modela rápido en Blender.',
  cost: 'Bajo',
  pros: ['Nostalgia de consola muy fuerte y estética de moda', 'Assets baratos: pocos polígonos y texturas mínimas', 'Encaja con la cámara táctica y menús tipo FF Tactics'],
  cons: ['El temblor de vértices molesta a algunos jugadores (conviene poder bajarlo)', 'Baja resolución: hay que cuidar la legibilidad de la interfaz', 'Muy asociado a terror indie; hay que darle identidad propia'],
  camera: 'persp',
  pixelScale: 3,
  props: 'kit',
  seg: 6,
  fxLights: true,
  role: (_role, spec) => ({ ...spec, color: shift(spec.color, 1.1, 1.05) }),
  material(spec) {
    crunch ??= crunchyTexture(32, 0.68, 5);
    const glow = !!spec.emissive;
    return psxify(
      new THREE.MeshLambertMaterial({
        color: spec.color,
        map: glow ? null : crunch,
        emissive: spec.emissive ?? 0x000000,
        emissiveIntensity: glow ? Math.min(spec.emissiveIntensity ?? 1, 1.5) : 0,
        transparent: (spec.opacity ?? 1) < 1,
        opacity: spec.opacity ?? 1,
      }),
    );
  },
  rig(rig) {
    // No real-time shadows on the PS1: a dark blob under each unit.
    const blob = new THREE.Mesh(
      new THREE.CircleGeometry(0.4, 10).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ color: 0x000000, transparent: true, opacity: 0.45, depthWrite: false }),
    );
    blob.position.y = 0.02;
    rig.root.add(blob);
    rig.root.traverse((o) => (o.castShadow = false));
  },
  ground() {
    const g = streetGround(
      { px: 16, asphalt: '#5a5650', sidewalk: '#9a8f80', curb: '#b0a590', paint: '#d8c060', jitter: 0.22, wear: true, joints: true, nearest: true },
      (tex) => psxify(new THREE.MeshLambertMaterial({ map: tex.map })),
      null,
      1,
    );
    // The surroundings sit well below the street: snapped vertices shift depth
    // enough to make two near-coplanar planes fight.
    const apron = new THREE.Mesh(new THREE.PlaneGeometry(WIDTH + 80, DEPTH + 80, 20, 20).rotateX(-Math.PI / 2), psxify(new THREE.MeshLambertMaterial({ color: 0x2e2c30 })));
    apron.position.set(WIDTH / 2, -0.6, DEPTH / 2);
    g.add(apron);
    return g;
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.NeutralToneMapping;
    ctx.renderer.toneMappingExposure = 1.1;
    ctx.renderer.shadowMap.enabled = false;
    ctx.scene.background = gradientTexture([[0, '#1b2440'], [0.55, '#5b5a7a'], [1, '#c79a7a']]);
    ctx.scene.fog = new THREE.Fog(0x3d3a52, 16, 44);
    ctx.scene.add(new THREE.HemisphereLight(0xa0a8d0, 0x3a3040, 1.6));
    sun(ctx, 0xffe0c0, 2.4, [-0.6, 1, 0.3], { shadows: false });
  },
  post(ctx) {
    const composer = composerFor(ctx, 0);
    finish(composer, psxDitherPass());
    return {
      composer,
      tick() {
        const size = ctx.renderer.getDrawingBufferSize(new THREE.Vector2());
        snap.value.set(240 * (size.x / Math.max(size.y, 1)), 240);
      },
    };
  },
  hud({ g, w, h, bottom }) {
    g.font = '22px DotGothic16, monospace';
    g.textBaseline = 'alphabetic';
    const shadowText = (text: string, x: number, y: number, color = '#ffffff') => {
      g.fillStyle = '#000';
      g.fillText(text, x + 2, y + 2);
      g.fillStyle = color;
      g.fillText(text, x, y);
    };
    psxWindow(g, w / 2 - 130, 22, 260, 48);
    g.textAlign = 'center';
    shadowText('TURNO DE XCOM', w / 2, 55, '#ffe9a0');
    g.textAlign = 'left';
    const bx = w - 330;
    const by = h - bottom - 142;
    psxWindow(g, bx, by, 300, 122);
    shadowText('Asalto  Bianchi', bx + 22, by + 36);
    shadowText('Nv. 3   Cabo', bx + 22, by + 64, '#a8c8ff');
    shadowText('PV   5/ 5    PA  2', bx + 22, by + 96);
  },
};
