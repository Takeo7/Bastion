import * as THREE from 'three';
import { FullScreenQuad, Pass } from 'three/addons/postprocessing/Pass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';

// Post-processing passes for the style lab.

const VERTEX = /* glsl */ `
varying vec2 vUv;
void main() {
  vUv = uv;
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}`;

const NOISE = /* glsl */ `
float hash12(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
float vnoise(vec2 p) {
  vec2 i = floor(p);
  vec2 f = fract(p);
  f = f * f * (3.0 - 2.0 * f);
  return mix(mix(hash12(i), hash12(i + vec2(1.0, 0.0)), f.x), mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0, 1.0)), f.x), f.y);
}`;

export interface EdgeOptions {
  color?: THREE.ColorRepresentation;
  /** Line width in render pixels. */
  thickness?: number;
  /** Relative depth jump that counts as a silhouette. */
  depthThreshold?: number;
  /** 1 − cos(angle) between normals that counts as a crease. */
  normalThreshold?: number;
  /** 0 = ink lines, 1 = pixel-art (dark silhouettes, light creases). */
  mode?: 0 | 1;
  /** Hand-drawn wobble, in pixels. */
  wobble?: number;
  /** Shifts the wobble noise so two passes draw two different strokes. */
  seed?: number;
  opacity?: number;
}

/**
 * Screen-space outlines from a depth + normal pre-pass. Objects in `hidden`
 * (effects, tactical overlays) are left out of the pre-pass.
 */
export class EdgePass extends Pass {
  hidden: THREE.Object3D[] = [];
  private readonly target: THREE.WebGLRenderTarget;
  private readonly normalMaterial = new THREE.MeshNormalMaterial();
  private readonly quad: FullScreenQuad;
  private readonly material: THREE.ShaderMaterial;

  constructor(
    private readonly scene: THREE.Scene,
    private readonly camera: THREE.Camera,
    opts: EdgeOptions = {},
  ) {
    super();
    this.target = new THREE.WebGLRenderTarget(1, 1, { depthTexture: new THREE.DepthTexture(1, 1) });
    this.material = new THREE.ShaderMaterial({
      uniforms: {
        tDiffuse: { value: null },
        tNormal: { value: null },
        tDepth: { value: null },
        resolution: { value: new THREE.Vector2(1, 1) },
        thickness: { value: opts.thickness ?? 1 },
        lineColor: { value: new THREE.Color(opts.color ?? 0x000000) },
        lineOpacity: { value: opts.opacity ?? 1 },
        depthThreshold: { value: opts.depthThreshold ?? 0.02 },
        normalThreshold: { value: opts.normalThreshold ?? 0.4 },
        cameraNear: { value: 0.1 },
        cameraFar: { value: 400 },
        ortho: { value: false },
        mode: { value: opts.mode ?? 0 },
        wobble: { value: opts.wobble ?? 0 },
        seed: { value: opts.seed ?? 0 },
      },
      vertexShader: VERTEX,
      fragmentShader: /* glsl */ `
        #include <packing>
        uniform sampler2D tDiffuse;
        uniform sampler2D tNormal;
        uniform sampler2D tDepth;
        uniform vec2 resolution;
        uniform float thickness;
        uniform vec3 lineColor;
        uniform float lineOpacity;
        uniform float depthThreshold;
        uniform float normalThreshold;
        uniform float cameraNear;
        uniform float cameraFar;
        uniform bool ortho;
        uniform int mode;
        uniform float wobble;
        uniform float seed;
        varying vec2 vUv;
        ${NOISE}

        float viewDepth(vec2 uv) {
          float d = texture2D(tDepth, uv).x;
          float z = ortho ? orthographicDepthToViewZ(d, cameraNear, cameraFar) : perspectiveDepthToViewZ(d, cameraNear, cameraFar);
          return -z;
        }
        vec3 nrm(vec2 uv) { return texture2D(tNormal, uv).xyz * 2.0 - 1.0; }

        void probe(vec2 uv, vec2 o, float d0, vec3 n0, inout float de, inout float ne) {
          float d = viewDepth(uv + o);
          de = max(de, (d - d0) / d0);
          // Creases are drawn on one side only so lines stay one pixel wide.
          if (d <= d0 * 1.002) ne = max(ne, 1.0 - dot(n0, nrm(uv + o)));
        }

        void main() {
          vec2 px = thickness / resolution;
          vec2 uv = vUv;
          if (wobble > 0.0) {
            vec2 q = vUv * resolution / 36.0 + seed * 31.7;
            uv += (vec2(vnoise(q), vnoise(q + 17.0)) - 0.5) * wobble * 2.0 / resolution;
          }
          vec4 col = texture2D(tDiffuse, vUv);
          if (texture2D(tDepth, uv).x >= 0.99999) { gl_FragColor = col; return; }
          float d0 = viewDepth(uv);
          vec3 n0 = nrm(uv);
          float de = 0.0;
          float ne = 0.0;
          probe(uv, vec2(px.x, 0.0), d0, n0, de, ne);
          probe(uv, vec2(-px.x, 0.0), d0, n0, de, ne);
          probe(uv, vec2(0.0, px.y), d0, n0, de, ne);
          probe(uv, vec2(0.0, -px.y), d0, n0, de, ne);
          float depthEdge = smoothstep(depthThreshold, depthThreshold * 1.8, de);
          float normalEdge = smoothstep(normalThreshold, normalThreshold * 1.5, ne);
          if (mode == 0) {
            col.rgb = mix(col.rgb, lineColor, max(depthEdge, normalEdge) * lineOpacity);
          } else {
            col.rgb *= 1.0 - 0.6 * depthEdge * lineOpacity;
            col.rgb += col.rgb * 0.5 * normalEdge * (1.0 - depthEdge) * lineOpacity;
          }
          gl_FragColor = col;
        }`,
    });
    this.quad = new FullScreenQuad(this.material);
  }

  override setSize(width: number, height: number): void {
    this.target.setSize(width, height);
    this.material.uniforms.resolution!.value.set(width, height);
  }

  override render(renderer: THREE.WebGLRenderer, writeBuffer: THREE.WebGLRenderTarget, readBuffer: THREE.WebGLRenderTarget): void {
    const visible = this.hidden.map((o) => o.visible);
    for (const o of this.hidden) o.visible = false;
    const background = this.scene.background;
    const override = this.scene.overrideMaterial;
    const clear = renderer.getClearColor(new THREE.Color());
    const alpha = renderer.getClearAlpha();
    this.scene.background = null;
    this.scene.overrideMaterial = this.normalMaterial;
    renderer.setClearColor(0x8080ff, 1);
    renderer.setRenderTarget(this.target);
    renderer.clear();
    renderer.render(this.scene, this.camera);
    this.scene.background = background;
    this.scene.overrideMaterial = override;
    renderer.setClearColor(clear, alpha);
    this.hidden.forEach((o, i) => (o.visible = visible[i]!));

    const u = this.material.uniforms;
    u.tDiffuse!.value = readBuffer.texture;
    u.tNormal!.value = this.target.texture;
    u.tDepth!.value = this.target.depthTexture;
    const cam = this.camera as THREE.PerspectiveCamera | THREE.OrthographicCamera;
    u.cameraNear!.value = cam.near;
    u.cameraFar!.value = cam.far;
    u.ortho!.value = cam instanceof THREE.OrthographicCamera;
    renderer.setRenderTarget(this.renderToScreen ? null : writeBuffer);
    if (this.clear) renderer.clear();
    this.quad.render(renderer);
  }

  override dispose(): void {
    this.target.dispose();
    this.material.dispose();
    this.normalMaterial.dispose();
    this.quad.dispose();
  }
}

// ------------------------------------------------------------- grading

export interface GradeOptions {
  saturation?: number;
  contrast?: number;
  brightness?: number;
  shadowTint?: [number, number, number];
  highlightTint?: [number, number, number];
  vignette?: number;
  grain?: number;
  aberration?: number;
  scanlines?: number;
  paper?: THREE.Texture;
  paperStrength?: number;
  /** Film gate weave in pixels, jumping 12 times a second. */
  weave?: number;
}

/** Final look in display space: saturation, split toning, vignette, grain, scanlines, paper. */
export function gradePass(o: GradeOptions): ShaderPass {
  const pass = new ShaderPass({
    uniforms: {
      tDiffuse: { value: null },
      resolution: { value: new THREE.Vector2(1, 1) },
      time: { value: 0 },
      saturation: { value: o.saturation ?? 1 },
      contrast: { value: o.contrast ?? 1 },
      brightness: { value: o.brightness ?? 0 },
      shadowTint: { value: new THREE.Vector3(...(o.shadowTint ?? [0, 0, 0])) },
      highlightTint: { value: new THREE.Vector3(...(o.highlightTint ?? [0, 0, 0])) },
      vignette: { value: o.vignette ?? 0 },
      grain: { value: o.grain ?? 0 },
      aberration: { value: o.aberration ?? 0 },
      scanlines: { value: o.scanlines ?? 0 },
      tPaper: { value: o.paper ?? null },
      paperStrength: { value: o.paper ? (o.paperStrength ?? 1) : 0 },
      weave: { value: o.weave ?? 0 },
    },
    vertexShader: VERTEX,
    fragmentShader: /* glsl */ `
      uniform sampler2D tDiffuse;
      uniform sampler2D tPaper;
      uniform vec2 resolution;
      uniform float time;
      uniform float saturation;
      uniform float contrast;
      uniform float brightness;
      uniform vec3 shadowTint;
      uniform vec3 highlightTint;
      uniform float vignette;
      uniform float grain;
      uniform float aberration;
      uniform float scanlines;
      uniform float paperStrength;
      uniform float weave;
      varying vec2 vUv;
      ${NOISE}
      void main() {
        vec2 uv = vUv;
        if (weave > 0.0) {
          float f = floor(time * 12.0);
          uv += (vec2(hash12(vec2(f, 1.0)), hash12(vec2(f, 7.0))) - 0.5) * weave / resolution;
        }
        vec3 c;
        if (aberration > 0.0) {
          vec2 d = (uv - 0.5) * aberration;
          c = vec3(texture2D(tDiffuse, uv + d).r, texture2D(tDiffuse, uv).g, texture2D(tDiffuse, uv - d).b);
        } else {
          c = texture2D(tDiffuse, uv).rgb;
        }
        float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
        c = mix(vec3(l), c, saturation);
        c = (c - 0.5) * contrast + 0.5 + brightness;
        c += shadowTint * (1.0 - l) * (1.0 - l) + highlightTint * l * l;
        vec2 v = vUv - 0.5;
        c *= 1.0 - vignette * dot(v, v) * 2.2;
        if (scanlines > 0.0) {
          float s = 0.5 + 0.5 * sin(gl_FragCoord.y * 2.1);
          float sweep = smoothstep(0.0, 0.04, abs(fract(vUv.y * 0.5 - time * 0.12) - 0.5));
          c *= 1.0 - scanlines * s * 0.6;
          c += vec3(0.05, 0.25, 0.35) * (1.0 - sweep) * scanlines;
        }
        if (paperStrength > 0.0) c *= mix(vec3(1.0), texture2D(tPaper, gl_FragCoord.xy / 512.0).rgb, paperStrength);
        if (grain > 0.0) c += (hash12(gl_FragCoord.xy + fract(time) * 100.0) - 0.5) * grain;
        gl_FragColor = vec4(clamp(c, 0.0, 1.0), 1.0);
      }`,
  });
  return pass;
}

// ----------------------------------------------------------- tilt-shift

/** Miniature look: blur grows away from a horizontal band in focus. */
export function tiltShiftPass(focus = 0.55, band = 0.12, maxBlur = 8): ShaderPass {
  return new ShaderPass({
    uniforms: {
      tDiffuse: { value: null },
      resolution: { value: new THREE.Vector2(1, 1) },
      focus: { value: focus },
      band: { value: band },
      maxBlur: { value: maxBlur },
    },
    vertexShader: VERTEX,
    fragmentShader: /* glsl */ `
      uniform sampler2D tDiffuse;
      uniform vec2 resolution;
      uniform float focus;
      uniform float band;
      uniform float maxBlur;
      varying vec2 vUv;
      void main() {
        float dist = max(0.0, abs(vUv.y - focus) - band);
        float r = clamp(dist * 4.0, 0.0, 1.0) * maxBlur;
        if (r < 0.5) { gl_FragColor = texture2D(tDiffuse, vUv); return; }
        vec4 acc = vec4(0.0);
        for (int i = 0; i < 32; i++) {
          float a = float(i) * 2.39996;
          float rr = sqrt((float(i) + 0.5) / 32.0) * r;
          acc += texture2D(tDiffuse, vUv + vec2(cos(a), sin(a)) * rr / resolution);
        }
        gl_FragColor = acc / 32.0;
      }`,
  });
}

// --------------------------------------------------------------- palette

function hexToRgb(hex: string): THREE.Vector3 {
  const n = parseInt(hex.replace('#', ''), 16);
  return new THREE.Vector3(((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255);
}

/** Snaps every pixel to a fixed palette with ordered (Bayer) dithering. Runs in display space. */
export function palettePass(colors: string[], dither = 0.05): ShaderPass {
  return new ShaderPass({
    defines: { COUNT: colors.length },
    uniforms: {
      tDiffuse: { value: null },
      palette: { value: colors.map(hexToRgb) },
      dither: { value: dither },
    },
    vertexShader: VERTEX,
    fragmentShader: /* glsl */ `
      uniform sampler2D tDiffuse;
      uniform vec3 palette[COUNT];
      uniform float dither;
      varying vec2 vUv;
      float bayer2(vec2 p) { return mod(2.0 * p.x + 3.0 * p.y, 4.0); }
      float bayer4(vec2 p) {
        p = mod(floor(p), 4.0);
        return (4.0 * bayer2(mod(p, 2.0)) + bayer2(floor(p / 2.0))) / 16.0;
      }
      void main() {
        vec3 c = texture2D(tDiffuse, vUv).rgb;
        c += (bayer4(gl_FragCoord.xy) - 0.47) * dither;
        vec3 best = palette[0];
        float bestD = 1e9;
        for (int i = 0; i < COUNT; i++) {
          vec3 d = c - palette[i];
          float dd = dot(d * d, vec3(0.3, 0.55, 0.15));
          if (dd < bestD) { bestD = dd; best = palette[i]; }
        }
        gl_FragColor = vec4(best, 1.0);
      }`,
  });
}

/** A hand-picked 38-colour palette with ramps for stone, skin/wood, fire, foliage, sky and alien purple. */
export const PIXEL_PALETTE = [
  '#0d0e14', '#1a1c26', '#282d3a', '#3a4151', '#535c6c', '#737d8c', '#9aa3ae', '#c6ccd2', '#eeece2',
  '#2a1c19', '#472d24', '#6b442f', '#8f613e', '#b6885a', '#d9b281', '#c9b896', '#e8dcc0',
  '#561a23', '#8f2a2e', '#c64632', '#ea7638', '#f5ad4c', '#fae27a',
  '#1b3226', '#2e5036', '#4b773d', '#7fa24c', '#bcd17a',
  '#19294b', '#264d86', '#3683c2', '#6cbfe8', '#b2ebf4',
  '#3b1e49', '#6d2e78', '#ad479b', '#ee7cbf', '#ffc3e4',
];

// ------------------------------------------------------------ newsprint

const BAYER = /* glsl */ `
float bayer2(vec2 p) { return mod(2.0 * p.x + 3.0 * p.y, 4.0); }
float bayer4(vec2 p) {
  p = mod(floor(p), 4.0);
  return (4.0 * bayer2(mod(p, 2.0)) + bayer2(floor(p / 2.0))) / 16.0;
}`;

/**
 * Cheap 90s comic printing: the frame is split into CMYK plates printed as
 * rotated halftone screens on yellowed newsprint, with the colour plates
 * slightly out of register. Solid blacks (ink lines) stay solid.
 */
export function newsprintPass(paper: THREE.Texture, cell: number): ShaderPass {
  return new ShaderPass({
    uniforms: {
      tDiffuse: { value: null },
      tPaper: { value: paper },
      resolution: { value: new THREE.Vector2(1, 1) },
      cell: { value: cell },
    },
    vertexShader: VERTEX,
    fragmentShader: /* glsl */ `
      uniform sampler2D tDiffuse;
      uniform sampler2D tPaper;
      uniform vec2 resolution;
      uniform float cell;
      varying vec2 vUv;
      vec4 cmyk(vec3 c) {
        float k = 1.0 - max(max(c.r, c.g), c.b);
        float d = max(1.0 - k, 1e-4);
        return vec4((1.0 - c.r - k) / d, (1.0 - c.g - k) / d, (1.0 - c.b - k) / d, k);
      }
      float screen(vec2 frag, float angle, float value) {
        float s = sin(angle);
        float co = cos(angle);
        vec2 f = fract(mat2(co, -s, s, co) * frag / cell) - 0.5;
        float r = sqrt(clamp(value, 0.0, 1.0)) * 0.74;
        return 1.0 - smoothstep(r - 0.09, r + 0.09, length(f));
      }
      void main() {
        vec2 px = cell * 0.28 / resolution;
        vec2 frag = gl_FragCoord.xy;
        float C = cmyk(texture2D(tDiffuse, vUv + vec2(px.x, 0.0)).rgb).x;
        float M = cmyk(texture2D(tDiffuse, vUv + vec2(-px.x, px.y)).rgb).y;
        float Y = cmyk(texture2D(tDiffuse, vUv + vec2(0.0, -px.y)).rgb).z;
        float K = cmyk(texture2D(tDiffuse, vUv).rgb).w;
        float c = screen(frag, 0.2618, C * 0.9);
        float m = screen(frag, 1.309, M * 0.9);
        float y = screen(frag, 0.0, Y * 0.9);
        float k = K > 0.72 ? smoothstep(0.72, 0.85, K) : screen(frag, 0.785, K * 0.75);
        vec3 col = vec3(0.93, 0.87, 0.72) * texture2D(tPaper, frag / 512.0).rgb;
        col *= mix(vec3(1.0), vec3(0.05, 0.62, 0.88), c);
        col *= mix(vec3(1.0), vec3(0.9, 0.12, 0.52), m);
        col *= mix(vec3(1.0), vec3(1.0, 0.9, 0.08), y);
        col *= mix(vec3(1.0), vec3(0.1, 0.08, 0.09), k);
        gl_FragColor = vec4(col, 1.0);
      }`,
  });
}

// ------------------------------------------------------------- notebook

/** School notebook: the drawing is multiplied onto squared paper with a red margin. */
export function notebookPass(paper: THREE.Texture, cell: number): ShaderPass {
  return new ShaderPass({
    uniforms: {
      tDiffuse: { value: null },
      tPaper: { value: paper },
      resolution: { value: new THREE.Vector2(1, 1) },
      cell: { value: cell },
    },
    vertexShader: VERTEX,
    fragmentShader: /* glsl */ `
      uniform sampler2D tDiffuse;
      uniform sampler2D tPaper;
      uniform vec2 resolution;
      uniform float cell;
      varying vec2 vUv;
      void main() {
        vec3 drawing = texture2D(tDiffuse, vUv).rgb;
        vec2 frag = gl_FragCoord.xy;
        vec3 paper = vec3(0.99, 0.985, 0.965) * mix(vec3(1.0), texture2D(tPaper, frag / 512.0).rgb, 0.5);
        vec2 g = abs(fract(frag / cell + 0.5) - 0.5) * cell;
        float grid = 1.0 - smoothstep(0.35, 1.1, min(g.x, g.y));
        paper = mix(paper, vec3(0.6, 0.73, 0.9), grid * 0.6);
        float margin = 1.0 - smoothstep(0.8, 2.2, abs(frag.x - resolution.x * 0.07));
        paper = mix(paper, vec3(0.9, 0.36, 0.4), margin * 0.85);
        gl_FragColor = vec4(paper * drawing, 1.0);
      }`,
  });
}

// ------------------------------------------------------------------ VHS

/**
 * A cartoon taped off the TV: smeared and delayed colour (YIQ), soft luma,
 * a tracking band, head-switching noise at the bottom, tape grain, CRT
 * scanlines and a slightly curved, darkened tube.
 */
export function vhsPass(): ShaderPass {
  return new ShaderPass({
    uniforms: {
      tDiffuse: { value: null },
      resolution: { value: new THREE.Vector2(1, 1) },
      time: { value: 0 },
    },
    vertexShader: VERTEX,
    fragmentShader: /* glsl */ `
      uniform sampler2D tDiffuse;
      uniform vec2 resolution;
      uniform float time;
      varying vec2 vUv;
      ${NOISE}
      vec3 toYiq(vec3 c) { return mat3(0.299, 0.596, 0.211, 0.587, -0.274, -0.523, 0.114, -0.322, 0.312) * c; }
      vec3 toRgb(vec3 c) { return mat3(1.0, 1.0, 1.0, 0.956, -0.272, -1.106, 0.621, -0.647, 1.703) * c; }
      void main() {
        vec2 cc = vUv - 0.5;
        float r2 = dot(cc, cc);
        vec2 uv = 0.5 + cc * (1.0 + 0.07 * r2);
        if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) { gl_FragColor = vec4(0.0, 0.0, 0.0, 1.0); return; }
        float line = floor(uv.y * 240.0);
        float bandY = fract(time * 0.045);
        float band = 1.0 - smoothstep(0.0, 0.035, abs(uv.y - bandY));
        float jitter = (hash12(vec2(line, floor(time * 30.0))) - 0.5) * (0.0012 + band * 0.012);
        float head = 1.0 - smoothstep(0.0, 0.03, uv.y);
        jitter += head * 0.02 * sin(uv.y * 900.0 + time * 40.0);
        uv.x += jitter;
        vec2 px = vec2(1.0 / resolution.x, 0.0);
        float luma = toYiq(texture2D(tDiffuse, uv).rgb).x * 0.6
          + (toYiq(texture2D(tDiffuse, uv - px).rgb).x + toYiq(texture2D(tDiffuse, uv + px).rgb).x) * 0.2;
        vec2 chroma = vec2(0.0);
        for (int i = 0; i < 9; i++) chroma += toYiq(texture2D(tDiffuse, uv - px * (float(i) * 1.6 + 2.0)).rgb).yz;
        vec3 c = toRgb(vec3(luma, chroma / 9.0 * 1.2));
        c = c * 0.9 + 0.045;
        float n = hash12(vec2(gl_FragCoord.x * 0.5, line) + fract(time * 7.0) * 100.0);
        c += (n - 0.5) * 0.07;
        c += step(0.975, n) * band * 0.5 + head * (n - 0.3) * 0.3;
        c *= 0.86 + 0.14 * sin(uv.y * resolution.y * 3.14159);
        c *= 1.0 - r2 * 1.1;
        gl_FragColor = vec4(clamp(c, 0.0, 1.0), 1.0);
      }`,
  });
}

// ------------------------------------------------------------------ PSX

/** PlayStation colour: 15-bit (5 bits per channel) with the console's 4×4 ordered dither. */
export function psxDitherPass(): ShaderPass {
  return new ShaderPass({
    uniforms: { tDiffuse: { value: null } },
    vertexShader: VERTEX,
    fragmentShader: /* glsl */ `
      uniform sampler2D tDiffuse;
      varying vec2 vUv;
      ${BAYER}
      void main() {
        vec3 c = texture2D(tDiffuse, vUv).rgb;
        c = floor(c * 31.0 + bayer4(gl_FragCoord.xy) + 0.03) / 31.0;
        gl_FragColor = vec4(c, 1.0);
      }`,
  });
}

/** Builds palette ramps (dark → light) around each base colour, VGA-style. */
export function rampPalette(bases: string[], steps: number): string[] {
  const out: string[] = [];
  const hsl = { h: 0, s: 0, l: 0 };
  for (const b of bases) {
    new THREE.Color(b).getHSL(hsl, THREE.SRGBColorSpace);
    for (let i = 0; i < steps; i++) {
      const l = 0.07 + (i / (steps - 1)) * 0.83;
      const s = hsl.s * (1 - Math.abs(l - 0.5) * 0.6);
      out.push('#' + new THREE.Color().setHSL(hsl.h, s, l, THREE.SRGBColorSpace).getHexString(THREE.SRGBColorSpace));
    }
  }
  return out;
}
