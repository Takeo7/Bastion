import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { GTAOPass } from 'three/addons/postprocessing/GTAOPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import type { Pass } from 'three/addons/postprocessing/Pass.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import type { UnitRig } from '../game/models';
import type { LightAnchor, MatSpec, Role } from './kit';
import { CROSSING, DEPTH, LANE_Z, SIDEWALK_TO, WIDTH, type ActorDef } from './layout';
import { EdgePass, gradePass, palettePass, PIXEL_PALETTE, tiltShiftPass } from './post';
import {
  brickTexture,
  gradientTexture,
  groundTextures,
  holoGridTexture,
  noiseTexture,
  paperTexture,
  toonRamp,
  type GroundPaint,
} from './textures';
import { voxelizeChildren, voxelizeProp } from '../game/voxel';

// ------------------------------------------------------------------ types

export interface StyleCtx {
  renderer: THREE.WebGLRenderer;
  scene: THREE.Scene;
  camera: THREE.PerspectiveCamera | THREE.OrthographicCamera;
  /** Effects and overlays: left out of outline pre-passes. */
  noEdges: THREE.Object3D[];
  anchors: { pos: THREE.Vector3; light: LightAnchor }[];
  /** Shared time uniform for animated materials. */
  time: { value: number };
}

export interface StyleRuntime {
  composer: EffectComposer | null;
  tick?(dt: number, time: number): void;
}

export interface ArtStyle {
  id: string;
  name: string;
  tagline: string;
  refs: string[];
  assets: string;
  cost: 'Muy bajo' | 'Bajo' | 'Medio' | 'Alto';
  pros: string[];
  cons: string[];
  camera: 'persp' | 'ortho';
  /** CSS pixels per rendered pixel (pixel art); 0 = native resolution. */
  pixelScale: number;
  props: 'kit' | 'blocks';
  /** Radial segments for round props. */
  seg: number;
  /** Muzzle flashes and explosions light the scene. */
  fxLights: boolean;
  /** Keep the game's own unit materials untouched. */
  keepUnits?: boolean;
  material(spec: MatSpec, ctx: StyleCtx): THREE.Material;
  role?(role: Role, spec: MatSpec): MatSpec;
  unitSpec?(spec: MatSpec, def: ActorDef): MatSpec;
  prop?(group: THREE.Group, ctx: StyleCtx): THREE.Object3D;
  rig?(rig: UnitRig, def: ActorDef, ctx: StyleCtx): void;
  debris?(color: number): THREE.Material & { opacity: number };
  ground(ctx: StyleCtx): THREE.Object3D;
  environment(ctx: StyleCtx): void;
  post(ctx: StyleCtx): StyleRuntime;
  /** Family in the lab menu. */
  family: Family;
  /** Camera preset forced when the style is picked. */
  preset?: 'tactica' | 'accion' | 'cenital' | 'iso';
  /** Upscale low-resolution frames smoothly (video) instead of as hard pixels. */
  pixelSmooth?: boolean;
  /** Units animate at this many poses per second (anime “on twos”). */
  stepped?: number;
  /** 2D overlay drawn every frame over the 3D view (captions, OSD, onomatopoeias). */
  hud?(h: HudCtx): void;
}

export type Family = 'voxel' | 'tinta' | 'comic' | 'sorpresa' | 'doggy' | 'aparcado';

export type FxKind = 'xcomShot' | 'alienShot' | 'grenade' | 'explosion' | 'hit';

/** An effect event projected to the screen, for onomatopoeias and subtitles. */
export interface Pop {
  kind: FxKind;
  /** CSS pixels. */
  x: number;
  y: number;
  /** Lab clock time when it happened. */
  born: number;
  seed: number;
}

export interface HudCtx {
  g: CanvasRenderingContext2D;
  /** CSS pixels; the context is already scaled by the device pixel ratio. */
  w: number;
  h: number;
  time: number;
  pops: Pop[];
  /** World point → CSS pixels on screen. */
  project(p: [number, number, number]): { x: number; y: number };
  /** Space covered by the lab's own panels (left column, bottom toolbar). */
  left: number;
  bottom: number;
}

// ---------------------------------------------------------------- helpers

export const CENTER = new THREE.Vector3(WIDTH / 2, 0, DEPTH / 2);

export function hex(c: string): THREE.Color {
  return new THREE.Color(c);
}

export function sun(ctx: StyleCtx, color: number, intensity: number, dir: [number, number, number], o: { map?: number; radius?: number; shadows?: boolean } = {}): THREE.DirectionalLight {
  const light = new THREE.DirectionalLight(color, intensity);
  const d = new THREE.Vector3(...dir).normalize().multiplyScalar(40);
  light.position.copy(CENTER).add(d);
  light.target.position.copy(CENTER);
  if (o.shadows !== false) {
    light.castShadow = true;
    light.shadow.mapSize.set(o.map ?? 2048, o.map ?? 2048);
    const cam = light.shadow.camera;
    cam.left = cam.bottom = -15;
    cam.right = cam.top = 15;
    cam.near = 1;
    cam.far = 90;
    light.shadow.bias = -0.0004;
    light.shadow.normalBias = 0.025;
    light.shadow.radius = o.radius ?? 1;
  }
  ctx.scene.add(light, light.target);
  return light;
}

export function composerFor(ctx: StyleCtx, samples = 4): EffectComposer {
  const target = new THREE.WebGLRenderTarget(4, 4, { type: THREE.HalfFloatType, samples });
  const composer = new EffectComposer(ctx.renderer, target);
  composer.addPass(new RenderPass(ctx.scene, ctx.camera));
  return composer;
}

/** GTAO that leaves effects and overlays out of its depth/normal pre-pass. */
export class FilteredGTAO extends GTAOPass {
  hidden: THREE.Object3D[] = [];
  override render(...args: Parameters<GTAOPass['render']>): void {
    const visible = this.hidden.map((o) => o.visible);
    for (const o of this.hidden) o.visible = false;
    super.render(...args);
    this.hidden.forEach((o, i) => (o.visible = visible[i]!));
  }
}

export function gtao(ctx: StyleCtx, radius: number, intensity: number): GTAOPass {
  const pass = new FilteredGTAO(ctx.scene, ctx.camera, 4, 4);
  pass.hidden = ctx.noEdges;
  pass.updateGtaoMaterial({ radius, distanceExponent: 1.5, thickness: 2, scale: 1, samples: 16 });
  pass.blendIntensity = intensity;
  return pass;
}

export function bloom(strength: number, radius: number, threshold: number): UnrealBloomPass {
  return new UnrealBloomPass(new THREE.Vector2(256, 256), strength, radius, threshold);
}

export function finish(composer: EffectComposer, ...after: Pass[]): EffectComposer {
  composer.addPass(new OutputPass());
  for (const p of after) composer.addPass(p);
  return composer;
}

export function standard(spec: MatSpec, extra: THREE.MeshStandardMaterialParameters = {}): THREE.MeshStandardMaterial {
  return new THREE.MeshStandardMaterial({
    color: spec.color,
    metalness: spec.metalness,
    roughness: spec.roughness,
    emissive: spec.emissive ?? 0x000000,
    emissiveIntensity: spec.emissive ? (spec.emissiveIntensity ?? 1.6) : 0,
    transparent: (spec.opacity ?? 1) < 1,
    opacity: spec.opacity ?? 1,
    ...extra,
  });
}

/** Vivid or muted variant of a colour. */
export function shift(c: THREE.Color, sat: number, light: number, towards?: THREE.Color, amount = 0): THREE.Color {
  const hsl = { h: 0, s: 0, l: 0 };
  c.getHSL(hsl);
  const out = new THREE.Color().setHSL(hsl.h, Math.min(1, hsl.s * sat), Math.min(0.92, hsl.l * light));
  return towards ? out.lerp(towards, amount) : out;
}

/** Plain street plane plus a wider apron so the board does not end abruptly. */
export function streetGround(
  paint: GroundPaint,
  material: (tex: { map: THREE.Texture; rough: THREE.Texture }) => THREE.Material,
  apron: THREE.Material | null,
  /** Subdivisions per tile (affine texture warping needs small polygons). */
  segments = 0,
): THREE.Group {
  const g = new THREE.Group();
  const tex = groundTextures(WIDTH, DEPTH, SIDEWALK_TO, LANE_Z, CROSSING, paint);
  const geometry = segments ? new THREE.PlaneGeometry(WIDTH, DEPTH, WIDTH * segments, DEPTH * segments) : new THREE.PlaneGeometry(WIDTH, DEPTH);
  const plane = new THREE.Mesh(geometry.rotateX(-Math.PI / 2), material(tex));
  plane.position.set(WIDTH / 2, 0, DEPTH / 2);
  plane.receiveShadow = true;
  g.add(plane);
  if (apron) {
    const outer = new THREE.Mesh(new THREE.PlaneGeometry(WIDTH + 80, DEPTH + 80).rotateX(-Math.PI / 2), apron);
    outer.position.set(WIDTH / 2, -0.01, DEPTH / 2);
    outer.receiveShadow = true;
    g.add(outer);
  }
  return g;
}

/** Toon material with optional extra shading (halftone dots, hatching) injected after lighting. */
export function toon(spec: MatSpec, ramp: THREE.Texture, shading: string | null): THREE.MeshToonMaterial {
  const m = new THREE.MeshToonMaterial({
    color: spec.color,
    gradientMap: ramp,
    emissive: spec.emissive ?? 0x000000,
    emissiveIntensity: spec.emissive ? Math.min(spec.emissiveIntensity ?? 1, 1.6) : 0,
    transparent: (spec.opacity ?? 1) < 1,
    opacity: spec.opacity ?? 1,
  });
  if (shading) {
    m.onBeforeCompile = (shader) => {
      shader.fragmentShader = shader.fragmentShader.replace(
        '#include <opaque_fragment>',
        /* glsl */ `{
          float lum = dot(outgoingLight, vec3(0.299, 0.587, 0.114));
          float base = max(dot(diffuseColor.rgb, vec3(0.299, 0.587, 0.114)), 0.03);
          float lit = lum / base;
          ${shading}
        }
        #include <opaque_fragment>`,
      );
    };
    m.customProgramCacheKey = () => shading;
  }
  return m;
}

export function withMap(m: THREE.MeshToonMaterial, map: THREE.Texture): THREE.MeshToonMaterial {
  m.map = map;
  return m;
}

export const HALFTONE = /* glsl */ `
  vec2 cell = mat2(0.7071, -0.7071, 0.7071, 0.7071) * gl_FragCoord.xy / 7.0;
  vec2 f = fract(cell) - 0.5;
  float r = clamp((0.8 - lit) * 0.75, 0.0, 0.48);
  float dotMask = 1.0 - smoothstep(r - 0.06, r + 0.06, length(f));
  outgoingLight *= 1.0 - dotMask * 0.32;
`;

export const HATCHING = /* glsl */ `
  float s1 = fract((gl_FragCoord.x + gl_FragCoord.y) / 7.0);
  float s2 = fract((gl_FragCoord.x - gl_FragCoord.y) / 7.0);
  float h = 0.0;
  if (lit < 0.85) h = smoothstep(0.78, 0.9, s1);
  if (lit < 0.5) h = max(h, smoothstep(0.78, 0.9, s2));
  outgoingLight = mix(outgoingLight, vec3(0.16, 0.11, 0.08), h * 0.55);
`;

export function lampLights(ctx: StyleCtx, spotScale: number, pointScale: number, shadows: boolean): void {
  for (const { pos, light } of ctx.anchors) {
    if (light.kind === 'spot') {
      if (spotScale <= 0) continue;
      const spot = new THREE.SpotLight(light.color, light.intensity * spotScale, light.distance, 0.95, 0.7, 2);
      spot.position.copy(pos);
      spot.target.position.set(pos.x, 0, pos.z + 0.6);
      if (shadows) {
        spot.castShadow = true;
        spot.shadow.mapSize.set(1024, 1024);
        spot.shadow.bias = -0.0005;
        spot.shadow.normalBias = 0.03;
      }
      ctx.scene.add(spot, spot.target);
    } else if (pointScale > 0) {
      const p = new THREE.PointLight(light.color, light.intensity * pointScale, light.distance, 2);
      p.position.copy(pos);
      ctx.scene.add(p);
    }
  }
}

/** Replaces low-poly-able primitives by coarse versions, keeping their orientation. */
export const lowCache = new Map<string, THREE.BufferGeometry>();
export function lowPoly(geo: THREE.BufferGeometry): THREE.BufferGeometry {
  const hit = lowCache.get(geo.uuid);
  if (hit) return hit;
  const p = (geo as unknown as { parameters?: Record<string, number> }).parameters ?? {};
  let low: THREE.BufferGeometry | null = null;
  switch (geo.type) {
    case 'CapsuleGeometry':
      low = new THREE.CapsuleGeometry(p.radius, p.height, 2, 6);
      break;
    case 'SphereGeometry':
      low = new THREE.IcosahedronGeometry(p.radius, 0);
      break;
    case 'CylinderGeometry':
      low = new THREE.CylinderGeometry(p.radiusTop, p.radiusBottom, p.height, 6);
      break;
    case 'TorusGeometry':
      low = new THREE.TorusGeometry(p.radius, p.tube, 3, 8);
      break;
  }
  if (!low) return geo;
  geo.computeBoundingBox();
  const want = geo.boundingBox!.getSize(new THREE.Vector3());
  const centre = geo.boundingBox!.getCenter(new THREE.Vector3());
  const candidates = [
    low,
    low.clone().rotateX(Math.PI / 2),
    low.clone().rotateZ(Math.PI / 2),
  ];
  let best = low;
  let bestErr = Infinity;
  for (const c of candidates) {
    c.computeBoundingBox();
    const err = c.boundingBox!.getSize(new THREE.Vector3()).sub(want).lengthSq();
    if (err < bestErr) {
      bestErr = err;
      best = c;
    }
  }
  // Keep the original placement too (bones are shifted to hang from their joint).
  best.computeBoundingBox();
  const offset = centre.sub(best.boundingBox!.getCenter(new THREE.Vector3()));
  best.translate(offset.x, offset.y, offset.z);
  lowCache.set(geo.uuid, best);
  return best;
}

export function holoMaterial(ctx: StyleCtx, tint: THREE.Color, base = 0.1): THREE.ShaderMaterial {
  return new THREE.ShaderMaterial({
    uniforms: { tint: { value: tint }, time: ctx.time, base: { value: base } },
    vertexShader: /* glsl */ `
      varying vec3 vNormal;
      varying vec3 vView;
      varying vec3 vWorld;
      void main() {
        vec4 wp = modelMatrix * vec4(position, 1.0);
        vWorld = wp.xyz;
        vec4 mv = viewMatrix * wp;
        vView = -mv.xyz;
        vNormal = normalize(normalMatrix * normal);
        gl_Position = projectionMatrix * mv;
      }`,
    fragmentShader: /* glsl */ `
      uniform vec3 tint;
      uniform float time;
      uniform float base;
      varying vec3 vNormal;
      varying vec3 vView;
      varying vec3 vWorld;
      void main() {
        float f = pow(1.0 - abs(dot(normalize(vNormal), normalize(vView))), 2.0);
        float scan = smoothstep(0.93, 1.0, fract(vWorld.y * 1.5 - time * 0.5)) * 0.5;
        float bands = 0.05 * step(0.5, fract(vWorld.y * 14.0));
        gl_FragColor = vec4(tint * (base + bands + f * 1.4 + scan), 1.0);
      }`,
  });
}

export function addEdges(root: THREE.Object3D, color: THREE.Color, opacity = 0.85): void {
  const meshes: THREE.Mesh[] = [];
  root.traverse((o) => {
    if (o instanceof THREE.Mesh && !o.userData.noEdges) meshes.push(o);
  });
  for (const mesh of meshes) {
    const lines = new THREE.LineSegments(
      new THREE.EdgesGeometry(mesh.geometry, 28),
      new THREE.LineBasicMaterial({ color, transparent: true, opacity, blending: THREE.AdditiveBlending, depthWrite: false }),
    );
    mesh.add(lines);
  }
}

/** Keeps every ShaderPass resolution uniform in sync with the drawing buffer. */
export function syncResolution(composer: EffectComposer, width: number, height: number): void {
  for (const p of composer.passes) {
    if (p instanceof ShaderPass && p.uniforms.resolution) p.uniforms.resolution.value.set(width, height);
  }
}
