import * as THREE from 'three';
import type { Mood } from './renderer';
import { loadModel } from './synty';
import { SKY_MODELS, skyTextureFile, type SkyModelId } from './syntyManifest';

// The sky round every scene (Arturo, 0.9.1: «que no sea todo blanco»): a dome shaded from the haze at the horizon to
// the zenith, the sun's glow on it; on the horizon the mountains round the valley (snow on them in winter) and, in a
// town or by a factory, roofs before them; clouds in rings above. Each part is centred on the camera as it draws
// (onBeforeRender), as far as its far plane allows, so none of it ever comes nearer or moves past; the parts are
// transparent, so the occlusion's pre-pass leaves them out (post.ts), drawn first of those and only where nothing
// else is. A handful of draw calls, a few thousand triangles.

/** What stands on the horizon: a town's roofs before the mountains, a factory's lower ones, or the mountains alone. */
export type SkyLand = 'town' | 'industry' | 'country';

/** The battle's land, for its sky (mapView sets it as it builds a map; the renderer's sky reads it as it draws). */
export const SKY_LAND: { value: SkyLand } = { value: 'country' };

/** The weather on top of the time of day: snow (a winter's grey, snow on the slopes) or clear. */
export interface SkyWeather {
  snow: boolean;
  night: boolean;
}

/** Each part's distance, a share of the camera's far plane: the dome furthest, the clouds nearest. */
const DOME = 0.9;
const MOUNTAINS = 0.8;
const ROOFS_FAR = 0.74;
const ROOFS_NEAR = 0.7;
const CLOUDS = 0.62;

/** The mountains' band: from below the horizon (it hides where the land ends) to above it, as a share of its distance. */
const MOUNTAIN_BAND: [number, number] = [-0.035, 0.15];
/** Times the mountains go round (the strip is a whole turn: wider and lower, as a valley's). */
const MOUNTAIN_REPEAT = 3;
/** The roofs' band, and the skyline strip each ring takes (0 the darkest of the four), how often it goes round. */
const ROOF_RINGS: Record<SkyLand, { strip: number; top: number; at: number; repeat: number }[]> = {
  town: [
    { strip: 2, top: 0.1, at: ROOFS_FAR, repeat: 4 },
    { strip: 1, top: 0.075, at: ROOFS_NEAR, repeat: 6 },
  ],
  industry: [{ strip: 3, top: 0.06, at: ROOFS_NEAR, repeat: 5 }],
  country: [],
};

/**
 * The cloud rings: by any weather, and a second (thinner, higher) in snow, when the sky is grey (`lift`: how much
 * higher, a share of the ring's radius; `turn`, so the two don't line up).
 */
const CLOUD_RINGS: { id: SkyModelId; snow: boolean; lift: number; turn: number }[] = [
  { id: 'cloudRing', snow: false, lift: 0, turn: 0 },
  { id: 'cloudRingThin', snow: true, lift: 0.08, turn: 1.9 },
];

const textures = new Map<string, THREE.Texture>();
function skyTexture(id: 'mountains' | 'skyline'): THREE.Texture {
  let t = textures.get(id);
  if (!t) {
    t = new THREE.TextureLoader().load(`${import.meta.env.BASE_URL}${skyTextureFile(id)}`);
    t.colorSpace = THREE.SRGBColorSpace;
    t.wrapS = THREE.RepeatWrapping;
    // Always seen larger than it is (a strip goes round the whole sky a few times): no smaller copies, which would
    // run one strip of the skyline into the next.
    t.generateMipmaps = false;
    t.minFilter = THREE.LinearFilter;
    textures.set(id, t);
  }
  return t;
}

/** Puts a part where the camera is, at its distance (`at`, a share of the far plane), as it draws. */
function followCamera(mesh: THREE.Object3D, at: number, turn = 0): void {
  mesh.matrixAutoUpdate = false;
  mesh.matrixWorldAutoUpdate = false;
  mesh.frustumCulled = false;
  const q = new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), turn);
  const s = new THREE.Vector3();
  mesh.onBeforeRender = (_r, _s, camera) => {
    const far = (camera as THREE.PerspectiveCamera).far ?? 400;
    s.setScalar(far * at);
    mesh.matrixWorld.compose(camera.position, q, s);
  };
}

const COMMON = /* glsl */ `
  #include <common>
  uniform vec3 skyHorizon;
`;

/** The dome: the haze at the horizon (as the fog has it), the middle of the sky above, the zenith; the sun's glow. */
function domeMaterial(u: Record<string, THREE.IUniform>): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    uniforms: u,
    vertexShader: /* glsl */ `
      varying vec3 vDir;
      void main() {
        vDir = position;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }`,
    fragmentShader: /* glsl */ `
      ${COMMON}
      uniform vec3 skyMiddle;
      uniform vec3 skyZenith;
      uniform vec3 skySunDir;
      uniform vec3 skySun;
      uniform float skyGlow;
      varying vec3 vDir;
      void main() {
        vec3 d = normalize(vDir);
        float e = d.y;
        vec3 c = mix(skyHorizon, skyMiddle, smoothstep(0.0, 0.22, e));
        c = mix(c, skyZenith, smoothstep(0.18, 0.75, e));
        float s = max(dot(d, skySunDir), 0.0);
        c += skySun * (pow(s, 900.0) * 2.0 + pow(s, 10.0) * 0.35 * skyGlow);
        gl_FragColor = vec4(c, 1.0);
        #include <tonemapping_fragment>
        #include <colorspace_fragment>
      }`,
    side: THREE.BackSide,
    depthWrite: false,
    transparent: true,
  });
}

/**
 * A band on the horizon cut from a strip of texture (`rows`, its v from and to): its colour as the land's light
 * has it (skyLand), snow on it in winter (skySnow), `haze` of the haze over it, and more the further down it goes
 * (up to `foot` of its height), all of it at the haze's colour at its foot, where it meets the land.
 */
function bandMaterial(u: Record<string, THREE.IUniform>, map: THREE.Texture, rows: [number, number], repeat: number, haze: number, foot: number): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    uniforms: { ...u, skyMap: { value: map }, skyRows: { value: new THREE.Vector2(...rows) }, skyRepeat: { value: repeat }, skyAir: { value: haze }, skyFoot: { value: foot } },
    vertexShader: /* glsl */ `
      varying vec2 vUv;
      void main() {
        vUv = uv;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }`,
    fragmentShader: /* glsl */ `
      ${COMMON}
      uniform sampler2D skyMap;
      uniform vec2 skyRows;
      uniform float skyRepeat;
      uniform float skyAir;
      uniform float skyFoot;
      uniform vec3 skyLand;
      uniform float skySnow;
      varying vec2 vUv;
      void main() {
        vec4 t = texture2D(skyMap, vec2(vUv.x * skyRepeat, mix(skyRows.x, skyRows.y, vUv.y)));
        if (t.a < 0.5) discard;
        vec3 c = t.rgb;
        // Snow: the slopes greyed and lightened to a winter's white, the rock's shading kept.
        float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
        c = mix(c, vec3(0.78, 0.84, 0.92) * (0.55 + l * 1.1), skySnow);
        c *= skyLand;
        c = mix(c, skyHorizon, clamp(skyAir + (1.0 - smoothstep(0.0, skyFoot, vUv.y)) * (1.0 - skyAir), 0.0, 1.0));
        gl_FragColor = vec4(c, 1.0);
        #include <tonemapping_fragment>
        #include <colorspace_fragment>
      }`,
    side: THREE.BackSide,
    depthWrite: false,
    transparent: true,
  });
}

/** The clouds: lit from above, shaded below, both as the light has them; the haze over the further ones' feet. */
function cloudMaterial(u: Record<string, THREE.IUniform>): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    uniforms: u,
    vertexShader: /* glsl */ `
      varying float vUp;
      varying float vSun;
      uniform vec3 skySunDir;
      void main() {
        vUp = normal.y;
        vSun = dot(normalize((modelMatrix * vec4(normal, 0.0)).xyz), skySunDir);
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }`,
    fragmentShader: /* glsl */ `
      ${COMMON}
      uniform vec3 skyCloudLit;
      uniform vec3 skyCloudShade;
      varying float vUp;
      varying float vSun;
      void main() {
        float k = clamp(vUp * 0.5 + 0.5 + max(vSun, 0.0) * 0.25, 0.0, 1.0);
        vec3 c = mix(skyCloudShade, skyCloudLit, k);
        gl_FragColor = vec4(c, 1.0);
        #include <tonemapping_fragment>
        #include <colorspace_fragment>
      }`,
    depthWrite: false,
    transparent: true,
  });
}

/** A converted mesh's position and normal as plain floats (meshopt keeps them as normalised integers, ±1 at most). */
function plain(geometry: THREE.BufferGeometry): THREE.BufferGeometry {
  const out = new THREE.BufferGeometry();
  for (const name of ['position', 'normal'] as const) {
    const a = geometry.getAttribute(name);
    if (!a) continue;
    const f = new Float32Array(a.count * 3);
    for (let i = 0; i < a.count; i++) for (let k = 0; k < 3; k++) f[i * 3 + k] = a.getComponent(i, k);
    out.setAttribute(name, new THREE.BufferAttribute(f, 3));
  }
  const index = geometry.getIndex();
  if (index) out.setIndex(index.clone());
  return out;
}

/** An open ring round the camera, radius 1, from `bottom` to `top` (shares of the radius), its v up its height. */
function ring(bottom: number, top: number): THREE.BufferGeometry {
  const g = new THREE.CylinderGeometry(1, 1, top - bottom, 128, 1, true);
  g.translate(0, (top + bottom) / 2, 0);
  return g;
}

export class Sky {
  readonly group = new THREE.Group();
  private readonly u: Record<string, THREE.IUniform> = {
    skyHorizon: { value: new THREE.Color() },
    skyMiddle: { value: new THREE.Color() },
    skyZenith: { value: new THREE.Color() },
    skySunDir: { value: new THREE.Vector3(0, 1, 0) },
    skySun: { value: new THREE.Color() },
    skyGlow: { value: 0 },
    skyLand: { value: new THREE.Color(1, 1, 1) },
    skySnow: { value: 0 },
    skyCloudLit: { value: new THREE.Color(1, 1, 1) },
    skyCloudShade: { value: new THREE.Color(0.6, 0.6, 0.65) },
  };
  /** The roofs before the mountains, a set for each land (made when first needed, then shown or hidden). */
  private readonly roofs: Partial<Record<SkyLand, THREE.Group>> = {};
  private roofLand: SkyLand | null = null;
  /** The clouds' rings (CLOUD_RINGS), each shown as the weather has it. */
  private readonly clouds: Partial<Record<SkyModelId, THREE.Object3D>> = {};
  private weather: SkyWeather = { snow: false, night: false };

  /** `land`: what stands on the horizon; unset, the battle's (SKY_LAND), read as it draws. */
  constructor(private readonly land?: SkyLand) {
    this.group.name = 'sky';
    const dome = new THREE.Mesh(new THREE.SphereGeometry(1, 32, 16), domeMaterial(this.u));
    dome.renderOrder = -100;
    followCamera(dome, DOME);
    const mountains = new THREE.Mesh(ring(MOUNTAIN_BAND[0], MOUNTAIN_BAND[1]), bandMaterial(this.u, skyTexture('mountains'), [0, 1], MOUNTAIN_REPEAT, 0.3, 0.55));
    mountains.renderOrder = -99;
    followCamera(mountains, MOUNTAINS, 0.7);
    this.group.add(dome, mountains);
    // The roofs come and go with the land: looked at as the dome draws (first of the sky), for the next frame.
    dome.onAfterRender = () => this.setRoofs(this.land ?? SKY_LAND.value);
    for (const { id, lift, turn } of CLOUD_RINGS) {
      void loadModel(SKY_MODELS[id].pack, 'sky', id)
        .then((scene) => {
          // The ring made one of radius 1 round the origin, its height as it is (a share of that radius).
          scene.updateMatrixWorld(true);
          const parts: THREE.BufferGeometry[] = [];
          scene.traverse((o) => {
            if (o instanceof THREE.Mesh) parts.push(plain(o.geometry).applyMatrix4(o.matrixWorld));
          });
          const box = new THREE.Box3();
          for (const g of parts) {
            g.computeBoundingBox();
            box.union(g.boundingBox!);
          }
          const radius = Math.max(box.max.x - box.min.x, box.max.z - box.min.z) / 2 || 1;
          const cx = (box.min.x + box.max.x) / 2;
          const cz = (box.min.z + box.max.z) / 2;
          const material = cloudMaterial(this.u);
          const clouds = new THREE.Group();
          for (const g of parts) {
            g.translate(-cx, 0, -cz).scale(1 / radius, 1 / radius, 1 / radius).translate(0, lift, 0);
            const m = new THREE.Mesh(g, material);
            m.renderOrder = -96;
            m.castShadow = m.receiveShadow = false;
            followCamera(m, CLOUDS, turn);
            clouds.add(m);
          }
          this.clouds[id] = clouds;
          this.group.add(clouds);
          this.showClouds();
        })
        .catch(() => undefined);
    }
  }

  /** The time of day (the renderer's mood) and the weather: the dome's colours, the land's, the clouds'. */
  setMood(m: Mood, weather: SkyWeather): void {
    this.weather = weather;
    const u = this.u;
    const stops = m.sky.map((c) => new THREE.Color(c));
    (u.skyHorizon!.value as THREE.Color).set(m.haze);
    (u.skyMiddle!.value as THREE.Color).copy(stops[Math.min(1, stops.length - 1)]!).lerp(new THREE.Color(m.haze), 0.25);
    (u.skyZenith!.value as THREE.Color).copy(stops[0]!);
    (u.skySunDir!.value as THREE.Vector3).set(...m.sunFrom).normalize();
    const sun = new THREE.Color(m.sun);
    (u.skySun!.value as THREE.Color).copy(sun).multiplyScalar(weather.night ? 0.25 : weather.snow ? 0.35 : 0.8);
    u.skyGlow!.value = weather.night ? 0.3 : weather.snow ? 0.4 : 1;
    // The land as its light has it: lit by the sky and the sun by day, dark and blue by night.
    const land = new THREE.Color(m.skyLight).lerp(sun, 0.35).multiplyScalar(weather.night ? 0.35 : 1.05);
    (u.skyLand!.value as THREE.Color).copy(land);
    u.skySnow!.value = weather.snow ? 0.75 : 0;
    const haze = new THREE.Color(m.haze);
    const lit = new THREE.Color(1, 1, 1).lerp(sun, 0.25);
    const shade = new THREE.Color(m.skyLight).lerp(haze, 0.5);
    if (weather.night) {
      lit.copy(new THREE.Color(m.skyLight)).multiplyScalar(0.45);
      shade.copy(haze).multiplyScalar(0.7);
    } else if (weather.snow) {
      lit.set(0xdfe5ec);
      shade.set(0x9aa6b4);
    }
    (u.skyCloudLit!.value as THREE.Color).copy(lit);
    (u.skyCloudShade!.value as THREE.Color).copy(shade);
    this.showClouds();
  }

  private showClouds(): void {
    for (const r of CLOUD_RINGS) {
      const c = this.clouds[r.id];
      if (c) c.visible = !r.snow || this.weather.snow;
    }
  }

  /** The roofs before the mountains, as the land has them (each land's made once). */
  private setRoofs(land: SkyLand): void {
    if (land === this.roofLand) return;
    this.roofLand = land;
    for (const [k, g] of Object.entries(this.roofs)) g.visible = k === land;
    if (this.roofs[land]) return;
    const set = (this.roofs[land] = new THREE.Group());
    this.group.add(set);
    ROOF_RINGS[land].forEach((r, i) => {
      // The skyline's four strips, darkest at the top: this one's rows (v from the bottom).
      // Inset a little: the strip next to it doesn't bleed in along the edges. Its solid foot (the lowest quarter) melts into the haze.
      const rows: [number, number] = [1 - (r.strip + 1) / 4 + 0.004, 1 - r.strip / 4 - 0.004];
      const mesh = new THREE.Mesh(ring(-0.01, r.top), bandMaterial(this.u, skyTexture('skyline'), rows, r.repeat, 0.22 - i * 0.1, 0.45));
      mesh.renderOrder = -98 + i;
      followCamera(mesh, r.at, 1.3 + i * 2.1);
      set.add(mesh);
    });
  }
}
