import * as THREE from 'three';
import type { MatSpec, Role } from './kit';
import { DEPTH, WIDTH } from './layout';
import { gradePass, psxDitherPass } from './post';
import { bloom, composerFor, finish, gtao, hex, lampLights, shift, streetGround, sun, type ArtStyle, type HudCtx } from './stylekit';
import { gradientTexture, gritTextures, type GritTextures } from './textures';

// Side request (2026-10-04): three takes on Pizza Doggy's "game dev like it's
// 2005" asset packs — low-poly models, extra-crunchy photo textures, fog,
// grimy light and an uneasy, liminal mood.

// ------------------------------------------------------------- materials

interface GritOptions {
  /** Box-map the detail texture in object space (no UVs needed, no swimming on animated parts). */
  triplanar: boolean;
  /** Texture repeats per world unit. */
  scale: number;
  /** PS1 vertex snapping. */
  psx: boolean;
  /** Fog measured from a point on the ground instead of from the camera. */
  radialFog: RadialFog | null;
}

interface RadialFog {
  center: THREE.Vector2;
  near: number;
  far: number;
  color: THREE.Color;
}

const snap = { value: new THREE.Vector2(320, 240) };

/** Injects the early-2000s quirks into a lit material. */
function gritify<T extends THREE.MeshStandardMaterial | THREE.MeshLambertMaterial>(m: T, o: GritOptions): T {
  m.onBeforeCompile = (shader) => {
    shader.uniforms.triScale = { value: o.scale };
    shader.uniforms.snapRes = snap;
    if (o.radialFog) {
      shader.uniforms.radialCenter = { value: o.radialFog.center };
      shader.uniforms.radialNear = { value: o.radialFog.near };
      shader.uniforms.radialFar = { value: o.radialFog.far };
      shader.uniforms.radialColor = { value: o.radialFog.color };
    }
    shader.vertexShader =
      'uniform vec2 snapRes;\nvarying vec3 vObjPos;\nvarying vec3 vObjNrm;\nvarying vec3 vWorld2;\n' +
      shader.vertexShader.replace(
        '#include <project_vertex>',
        /* glsl */ `#include <project_vertex>
        vObjPos = transformed;
        vObjNrm = objectNormal;
        vec4 w2 = vec4(transformed, 1.0);
        #ifdef USE_INSTANCING
          w2 = instanceMatrix * w2;
        #endif
        vWorld2 = (modelMatrix * w2).xyz;
        ${
          o.psx
            ? `vec4 snapped = gl_Position;
        snapped.xyz /= snapped.w;
        snapped.xy = floor(snapped.xy * snapRes * 0.5) / (snapRes * 0.5);
        snapped.xyz *= snapped.w;
        gl_Position = snapped;`
            : ''
        }`,
      );
    let frag =
      'uniform float triScale;\nuniform vec2 radialCenter;\nuniform float radialNear;\nuniform float radialFar;\nuniform vec3 radialColor;\nvarying vec3 vObjPos;\nvarying vec3 vObjNrm;\nvarying vec3 vWorld2;\n' +
      shader.fragmentShader;
    if (o.triplanar) {
      frag = frag.replace(
        '#include <map_fragment>',
        /* glsl */ `#ifdef USE_MAP
          vec3 an = abs(normalize(vObjNrm));
          vec2 tuv = an.x > an.y && an.x > an.z ? vObjPos.zy : (an.y > an.z ? vObjPos.xz : vObjPos.xy);
          diffuseColor *= texture2D(map, tuv * triScale);
        #endif`,
      );
    }
    if (o.radialFog) {
      frag = frag.replace(
        '#include <fog_fragment>',
        /* glsl */ `{
          float fd = length(vWorld2.xz - radialCenter);
          float ff = smoothstep(radialNear, radialFar, fd);
          ff = max(ff, smoothstep(2.6, 5.5, vWorld2.y) * 0.6);
          gl_FragColor.rgb = mix(gl_FragColor.rgb, radialColor, ff);
        }`,
      );
    }
    shader.fragmentShader = frag;
  };
  m.customProgramCacheKey = () => `grit-${o.triplanar}-${o.psx}-${!!o.radialFog}`;
  return m;
}

const TEXTURE_FOR: Partial<Record<Role, [keyof GritTextures, number]>> = {
  brick: ['brick', 1],
  concrete: ['concrete', 0.8],
  concreteDark: ['concrete', 1.2],
  metal: ['metal', 1.5],
  darkMetal: ['metal', 1.5],
  alienMetal: ['metal', 1.2],
  paint: ['metal', 1],
  paint2: ['metal', 1],
  stripe: ['metal', 1.5],
  wood: ['wood', 1.5],
  woodDark: ['wood', 1.5],
  fabric: ['fabric', 3],
  foliage: ['foliage', 2],
  bark: ['wood', 3],
  soil: ['generic', 2],
  rubber: ['generic', 3],
};

interface GritLook {
  textures: GritTextures;
  lambert: boolean;
  psx: boolean;
  radialFog: RadialFog | null;
}

function gritMaterial(spec: MatSpec, look: GritLook): THREE.Material {
  const glow = !!spec.emissive;
  const pick = spec.role ? TEXTURE_FOR[spec.role] : (['generic', 3] as const);
  const map = glow || !pick ? null : look.textures[pick[0]];
  const common = {
    color: spec.color,
    map,
    emissive: spec.emissive ?? 0x000000,
    emissiveIntensity: glow ? (spec.emissiveIntensity ?? 1) : 0,
    transparent: (spec.opacity ?? 1) < 1,
    opacity: spec.opacity ?? 1,
  };
  const m = look.lambert
    ? new THREE.MeshLambertMaterial(common)
    : new THREE.MeshStandardMaterial({ ...common, roughness: Math.max(0.6, spec.roughness), metalness: Math.min(spec.metalness, 0.3) });
  return gritify(m, { triplanar: !!map, scale: pick?.[1] ?? 1, psx: look.psx, radialFog: look.radialFog });
}

function palette(colors: Partial<Record<Role, string>>, fallbackSat: number) {
  return (role: Role, spec: MatSpec): MatSpec => {
    const c = colors[role];
    return { ...spec, color: c ? hex(c) : shift(spec.color, fallbackSat, 0.95) };
  };
}

const COMMON_COLORS: Partial<Record<Role, string>> = {
  brick: '#86604e',
  concrete: '#9a968c',
  concreteDark: '#76736a',
  metal: '#7a8084',
  darkMetal: '#45484a',
  paint: '#5f6d5c',
  paint2: '#566c74',
  stripe: '#9a5a3a',
  wood: '#8a6c4e',
  woodDark: '#5e4a36',
  fabric: '#8a7f62',
  foliage: '#5a6640',
  bark: '#5a4636',
  alienMetal: '#4a3a48',
};

// ------------------------------------------------- 1. Ciudad gris, 2004

const cityTextures = () => gritTextures(128, false);

export const ciudadGris: ArtStyle = {
  id: 'ciudadgris',
  family: 'doggy',
  name: 'Ciudad gris (2004)',
  tagline: 'Exterior industrial bajo cielo nublado, texturas fotográficas de baja resolución, niebla verdosa y resplandor de 2005.',
  refs: ['Half-Life 2 (Ciudad 17, 2004)', 'Pizza Doggy · PSX Mega Pack II', 'S.T.A.L.K.E.R. (2007)', 'Max Payne 2 (2003)'],
  assets:
    'Packs como los de Pizza Doggy (590+ modelos en .glb/.fbx/.blend, texturas y cielos) se cargan directamente en Three.js por ser glTF. Faltaría comprobar si incluyen soldados y aliens y qué dice su licencia sobre juegos web.',
  cost: 'Bajo',
  pros: ['Catálogo barato y enorme (bundle a 39 € en oferta) en glTF, sin conversiones', 'Tono serio y sucio, muy de invasión alienígena', 'Envejece bien: “2005” se lee como estilo, no como pobreza'],
  cons: ['Estética muy de moda en terror indie: hay que darle identidad', 'Texturas realistas: mezclar con modelos propios exige el mismo tratamiento', 'La legibilidad táctica con tanto gris requiere buena interfaz'],
  camera: 'persp',
  pixelScale: 0,
  props: 'kit',
  seg: 8,
  fxLights: true,
  role: palette({ ...COMMON_COLORS, window: '#3a4a50' }, 0.6),
  unitSpec: (spec) => ({ ...spec, color: shift(spec.color, 0.6, 0.95) }),
  material: (spec) => gritMaterial(spec, { textures: cityTextures(), lambert: false, psx: false, radialFog: null }),
  ground() {
    return streetGround(
      { px: 32, asphalt: '#4c4b48', sidewalk: '#77736b', curb: '#8a867c', paint: '#a8a070', jitter: 0.2, wear: true, joints: true },
      (tex) => gritify(new THREE.MeshStandardMaterial({ map: tex.map, roughness: 0.95 }), { triplanar: false, scale: 1, psx: false, radialFog: null }),
      new THREE.MeshStandardMaterial({ color: 0x3e3d3a, roughness: 1 }),
    );
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    ctx.renderer.toneMappingExposure = 1.05;
    ctx.scene.background = gradientTexture([[0, '#4d5a5e'], [0.55, '#7f8a86'], [1, '#a6a594']]);
    ctx.scene.fog = new THREE.FogExp2(0x7d8a86, 0.017);
    ctx.scene.add(new THREE.HemisphereLight(0xa8b4b4, 0x3a3530, 1.6));
    sun(ctx, 0xdfe6e6, 1.2, [-0.3, 1, -0.5], { radius: 4 });
    lampLights(ctx, 0.5, 1, false);
  },
  post(ctx) {
    const composer = composerFor(ctx);
    composer.addPass(gtao(ctx, 0.5, 0.9));
    composer.addPass(bloom(0.55, 0.8, 0.7));
    finish(
      composer,
      gradePass({ saturation: 0.62, contrast: 1.12, shadowTint: [0, 0.03, 0.025], highlightTint: [0.03, 0.025, 0], vignette: 0.4, grain: 0.05 }),
    );
    return { composer };
  },
  hud({ g, w, h, time, left, bottom }: HudCtx) {
    // Amber suit HUD and a chapter title that fades in and out.
    const box = (x: number, y: number, bw: number, label: string, value: string) => {
      g.fillStyle = 'rgba(0,0,0,0.38)';
      g.beginPath();
      g.roundRect(x, y, bw, 58, 8);
      g.fill();
      g.fillStyle = 'rgba(255,210,74,0.9)';
      g.font = '700 13px Rajdhani, sans-serif';
      g.fillText(label, x + 14, y + 38);
      g.font = '700 40px Rajdhani, sans-serif';
      g.textAlign = 'right';
      g.fillText(value, x + bw - 14, y + 44);
      g.textAlign = 'left';
    };
    // Laid out for 860 px of free width and scaled down when the view is narrower.
    const k = Math.min(1, (w - left) / 860);
    g.translate(left, 0);
    g.scale(k, k);
    const W = (w - left) / k;
    const y = (h - bottom) / k - 82;
    box(24, y, 180, 'SALUD', '100');
    box(216, y, 180, 'BLINDAJE', '47');
    box(W - 274, y, 250, 'MUNICIÓN', '18 | 54');
    const alpha = Math.max(0, Math.min(1, time / 0.8, (5 - time) / 1.2));
    if (alpha > 0) {
      g.globalAlpha = alpha;
      g.fillStyle = '#f2f2ea';
      g.font = '500 16px Rajdhani, sans-serif';
      g.letterSpacing = '6px';
      g.fillText('CAPÍTULO 3', 70, (h / k) * 0.42);
      g.font = '600 46px Rajdhani, sans-serif';
      g.letterSpacing = '14px';
      g.fillText('DISTRITO COMERCIAL', 70, (h / k) * 0.42 + 52);
      g.letterSpacing = '0px';
      g.globalAlpha = 1;
    }
  },
};

// ------------------------------------------------------ 2. Niebla, 1999

const FOG_COLOR = hex('#b9b6a8');
const fogLook = (): GritLook => ({
  textures: gritTextures(64, true),
  lambert: true,
  psx: false,
  radialFog: { center: new THREE.Vector2(4, 7.5), near: 5, far: 15.5, color: FOG_COLOR },
});
let fogLookCache: GritLook | null = null;
const fog = () => (fogLookCache ??= fogLook());

export const niebla: ArtStyle = {
  id: 'niebla',
  family: 'doggy',
  name: 'Niebla (1999)',
  tagline: 'Niebla densa que nace de la escuadra: lo que no ve el equipo se pierde en blanco. Óxido, ceniza que cae y una radio que chisporrotea.',
  refs: ['Silent Hill (1999)', 'Pizza Doggy · PSX Textures', 'Fatal Frame (2001)', 'Alone in the Dark: The New Nightmare (2001)'],
  assets:
    'Igual que la ciudad gris: modelos sencillos con texturas sucias de baja resolución. La niebla esconde la distancia, así que el detalle solo importa cerca de la escuadra.',
  cost: 'Bajo',
  pros: ['La niebla es también la niebla de guerra: estética y mecánica a la vez', 'Tensión altísima: los aliens salen de la nada', 'Muy barato de renderizar y de producir (se ve poco a la vez)'],
  cons: ['Se ve poco del mapa: hay que diseñar la cámara y la interfaz para eso', 'Muy asociado al terror: la campaña tendría que acompañar', 'Puede frustrar si no se calibra bien la distancia'],
  camera: 'persp',
  pixelScale: 2,
  props: 'kit',
  seg: 7,
  fxLights: true,
  role: palette({ ...COMMON_COLORS, paint: '#7a5444', metal: '#7a6e64', foliage: '#4e4a3a', window: '#4a4a46' }, 0.5),
  unitSpec: (spec) => ({ ...spec, color: shift(spec.color, 0.55, 0.95) }),
  material: (spec) => gritMaterial(spec, fog()),
  ground() {
    const look = fog();
    return streetGround(
      { px: 24, asphalt: '#66625a', sidewalk: '#8a8478', curb: '#9a9486', paint: '#a09a80', jitter: 0.24, wear: true, joints: true, nearest: true },
      (tex) => gritify(new THREE.MeshLambertMaterial({ map: tex.map }), { triplanar: false, scale: 1, psx: false, radialFog: look.radialFog }),
      gritify(new THREE.MeshLambertMaterial({ color: 0x5a564e }), { triplanar: false, scale: 1, psx: false, radialFog: look.radialFog }),
    );
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.NeutralToneMapping;
    ctx.renderer.toneMappingExposure = 1;
    ctx.scene.background = FOG_COLOR.clone();
    ctx.scene.add(new THREE.HemisphereLight(0xdcd8cc, 0x5a5048, 2.4));
    sun(ctx, 0xe8e4da, 0.9, [-0.2, 1, 0.3], { radius: 6, map: 1024 });
  },
  post(ctx) {
    // Ash drifting down through the whole street.
    const N = 900;
    const pos = new Float32Array(N * 3);
    const seeds = Array.from({ length: N }, () => [Math.random() * (WIDTH + 6) - 3, Math.random() * 8, Math.random() * (DEPTH + 6) - 3, Math.random() * 6]);
    const geo = new THREE.BufferGeometry().setAttribute('position', new THREE.BufferAttribute(pos, 3));
    const ash = new THREE.Points(geo, new THREE.PointsMaterial({ color: 0xe6e2d8, size: 0.07, transparent: true, opacity: 0.85, depthWrite: false }));
    ash.frustumCulled = false;
    ctx.scene.add(ash);
    ctx.noEdges.push(ash);
    const composer = composerFor(ctx, 0);
    finish(composer, gradePass({ saturation: 0.45, contrast: 0.96, brightness: 0.02, grain: 0.1, vignette: 0.35 }), psxDitherPass());
    return {
      composer,
      tick(_dt, time) {
        seeds.forEach(([x, y, z, ph], i) => {
          pos[i * 3] = x! + Math.sin(time * 0.6 + ph!) * 0.4;
          pos[i * 3 + 1] = 8 - ((y! + time * 0.5) % 8);
          pos[i * 3 + 2] = z! + Math.cos(time * 0.4 + ph!) * 0.3;
        });
        geo.attributes.position!.needsUpdate = true;
      },
    };
  },
  hud({ g, w, h, time, left, bottom }: HudCtx) {
    // Pocket radio crackling with static, and an uneasy caption.
    const x = w - 150;
    const y = 24;
    g.fillStyle = 'rgba(30,28,24,0.85)';
    g.beginPath();
    g.roundRect(x, y, 120, 70, 8);
    g.fill();
    g.fillStyle = 'rgba(120,110,95,0.9)';
    for (let r = 0; r < 4; r++) for (let c = 0; c < 5; c++) {
      g.beginPath();
      g.arc(x + 16 + c * 9, y + 18 + r * 11, 2.5, 0, Math.PI * 2);
      g.fill();
    }
    const level = 0.45 + 0.4 * Math.abs(Math.sin(time * 2.3)) * (0.6 + 0.4 * Math.sin(time * 17));
    for (let i = 0; i < 6; i++) {
      const hgt = Math.max(3, (Math.sin(time * 23 + i * 1.7) * 0.5 + 0.5) * level * 44);
      g.fillStyle = i < 4 ? '#b8c46a' : '#d8664a';
      g.fillRect(x + 70 + i * 7, y + 58 - hgt, 5, hgt);
    }
    g.font = 'italic 15px Georgia, serif';
    g.fillStyle = 'rgba(40,36,30,0.9)';
    g.fillText('kssshhh…', x + 24, y + 92);
    g.font = 'italic 22px Georgia, serif';
    g.fillStyle = 'rgba(30,28,24,0.75)';
    g.fillText('Hay algo ahí fuera, en la niebla…', left + 40, h - bottom - 40);
  },
};

// --------------------------------------------- 3. Noche de linternas, PSX

const BEAM_LENGTH = 5;
const BEAM_GEOMETRY = new THREE.ConeGeometry(BEAM_LENGTH * Math.tan(0.4), BEAM_LENGTH, 24, 1, true)
  .translate(0, -BEAM_LENGTH / 2, 0)
  .rotateX(-Math.PI / 2);
const BEAM_MATERIAL = new THREE.ShaderMaterial({
  transparent: true,
  depthWrite: false,
  blending: THREE.AdditiveBlending,
  side: THREE.DoubleSide,
  vertexShader: /* glsl */ `
    varying float vAlong;
    varying vec3 vNormal;
    varying vec3 vView;
    void main() {
      vAlong = position.z / ${BEAM_LENGTH.toFixed(1)};
      vec4 mv = modelViewMatrix * vec4(position, 1.0);
      vView = -mv.xyz;
      vNormal = normalize(normalMatrix * normal);
      gl_Position = projectionMatrix * mv;
    }`,
  fragmentShader: /* glsl */ `
    varying float vAlong;
    varying vec3 vNormal;
    varying vec3 vView;
    void main() {
      // Brighter where the beam is seen through its thickest part, fading along its length.
      float facing = pow(abs(dot(normalize(vNormal), normalize(vView))), 2.0);
      float fade = pow(1.0 - clamp(vAlong, 0.0, 1.0), 1.8);
      gl_FragColor = vec4(vec3(1.0, 0.92, 0.75) * 0.1 * facing * fade, 1.0);
    }`,
});

const nightLook = (): GritLook => ({ textures: gritTextures(64, true), lambert: true, psx: true, radialFog: null });
let nightLookCache: GritLook | null = null;
const night = () => (nightLookCache ??= nightLook());

export const linternas: ArtStyle = {
  id: 'linternas',
  family: 'doggy',
  name: 'Noche de linternas (PSX)',
  tagline: 'Oscuridad total: solo las linternas de la escuadra, una farola que parpadea y los ojos de los aliens. Vértices que tiemblan y color de 15 bits.',
  refs: ['Pizza Doggy · PSX Mega Pack', 'Resident Evil (1996)', 'Cry of Fear (2012)', 'Puppet Combo (terror PSX)'],
  assets:
    'Modelos de pocos polígonos con texturas pequeñas; en la oscuridad casi todo el trabajo lo hace la luz. Es justo el uso para el que están pensados los packs PSX.',
  cost: 'Muy bajo',
  pros: ['Atmósfera brutal para misiones nocturnas y de sigilo', 'Las linternas cuentan qué ve cada soldado: lectura de línea de visión', 'Casi cualquier asset sirve: la luz unifica'],
  cons: ['Muy oscuro como estilo de toda la campaña: mejor como variante nocturna', 'Muchas luces con sombras: coste de GPU a vigilar', 'El temblor PSX no gusta a todo el mundo (opción para desactivarlo)'],
  camera: 'persp',
  pixelScale: 3,
  props: 'kit',
  seg: 6,
  fxLights: true,
  role: palette({ ...COMMON_COLORS, window: '#40382a' }, 0.6),
  unitSpec: (spec) => ({ ...spec, color: shift(spec.color, 0.7, 1) }),
  material: (spec) => gritMaterial(spec, night()),
  rig(rig, def) {
    if (def.team !== 'xcom') return;
    // A torch on every rifle: it follows the aim and casts shadows.
    const z0 = rig.muzzle.position.z + 0.05;
    const torch = new THREE.SpotLight(0xfff0d0, 90, 22, 0.48, 0.6, 1.2);
    torch.position.set(0, 0.02, z0);
    torch.target.position.set(0, 1.4, 6);
    torch.castShadow = true;
    torch.shadow.mapSize.set(512, 512);
    torch.shadow.bias = -0.0008;
    torch.shadow.normalBias = 0.04;
    // A faint visible beam, which also shows where each soldier is looking.
    const beam = new THREE.Mesh(BEAM_GEOMETRY, BEAM_MATERIAL);
    beam.position.copy(torch.position);
    beam.rotation.x = -Math.atan2(1.38, 6 - z0);
    rig.gunPivot.add(torch, torch.target, beam);
  },
  ground() {
    return streetGround(
      { px: 24, asphalt: '#45443f', sidewalk: '#66625a', curb: '#77736a', paint: '#8a8460', jitter: 0.24, wear: true, joints: true, nearest: true },
      (tex) => gritify(new THREE.MeshLambertMaterial({ map: tex.map }), { triplanar: false, scale: 1, psx: true, radialFog: null }),
      null,
      1,
    );
  },
  environment(ctx) {
    ctx.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    ctx.renderer.toneMappingExposure = 1.2;
    ctx.scene.background = new THREE.Color(0x020203);
    ctx.scene.fog = new THREE.Fog(0x000000, 24, 46);
    ctx.scene.add(new THREE.HemisphereLight(0x1c2436, 0x050505, 0.5));
    lampLights(ctx, 0.85, 1.2, false);
  },
  post(ctx) {
    // Street lamps hang straight off the scene; the torches live inside the rigs.
    const lamps = ctx.scene.children.filter((o): o is THREE.SpotLight => o instanceof THREE.SpotLight);
    const base = lamps.map((l) => l.intensity);
    const composer = composerFor(ctx, 0);
    composer.addPass(bloom(0.6, 0.4, 0.6));
    finish(composer, gradePass({ saturation: 0.8, contrast: 1.15, grain: 0.07, vignette: 0.7 }), psxDitherPass());
    return {
      composer,
      tick(_dt, time) {
        const size = ctx.renderer.getDrawingBufferSize(new THREE.Vector2());
        snap.value.set(240 * (size.x / Math.max(size.y, 1)), 240);
        // The first street lamp is dying.
        const l = lamps[0];
        if (l) {
          const flick = Math.sin(time * 37) > 0.2 && Math.sin(time * 5.3) > -0.6 ? 1 : 0.08;
          l.intensity = base[0]! * flick;
        }
      },
    };
  },
  hud({ g, w, h, time, bottom }: HudCtx) {
    // Torch battery and a mission clock.
    g.font = '20px DotGothic16, monospace';
    const x = w - 200;
    const y = h - bottom - 60;
    g.fillStyle = 'rgba(0,0,0,0.6)';
    g.fillRect(x - 12, y - 30, 188, 50);
    g.fillStyle = '#d8d0b8';
    g.fillText('LINTERNAS', x, y - 6);
    g.strokeStyle = '#d8d0b8';
    g.lineWidth = 2;
    g.strokeRect(x + 112, y - 22, 48, 20);
    g.fillRect(x + 160, y - 16, 4, 8);
    const bars = 3 - (Math.floor(time / 6) % 2);
    for (let i = 0; i < bars; i++) {
      g.fillStyle = bars === 1 ? '#d8664a' : '#9ad86a';
      g.fillRect(x + 116 + i * 14, y - 18, 11, 12);
    }
    const t = 3 * 3600 + 12 * 60 + Math.floor(time);
    g.fillStyle = 'rgba(216,208,184,0.8)';
    g.fillText(`NOCHE · ${String(Math.floor(t / 3600)).padStart(2, '0')}:${String(Math.floor(t / 60) % 60).padStart(2, '0')}`, w - 200, 44);
  },
};
