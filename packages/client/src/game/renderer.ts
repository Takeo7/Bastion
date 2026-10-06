import * as THREE from 'three';
import { CSS2DRenderer } from 'three/addons/renderers/CSS2DRenderer.js';
import { FrameLimiter, onGraphics, pixelRatio, type GraphicsSettings } from '../settings';
import { voxelPost, type VoxelPost } from './post';

type FrameFn = (dt: number, time: number) => void;

/** Vertical sky gradient (top to horizon) as a screen-space background. */
function skyGradient(stops: string[]): THREE.Texture {
  const canvas = document.createElement('canvas');
  canvas.width = 2;
  canvas.height = 256;
  const ctx = canvas.getContext('2d')!;
  const g = ctx.createLinearGradient(0, 0, 0, canvas.height);
  stops.forEach((c, i) => g.addColorStop(i / (stops.length - 1), c));
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  return texture;
}

/** Owns the WebGL renderer, the scene, the lights and the render loop. */
export class Renderer {
  readonly scene = new THREE.Scene();
  readonly camera = new THREE.PerspectiveCamera(40, 1, 0.1, 400);
  readonly gl: THREE.WebGLRenderer;
  readonly labels = new CSS2DRenderer();
  // Voxel art (M5): a warm afternoon sun under a pale sky, as in the art lab.
  private readonly sun = new THREE.DirectionalLight(0xffe0b0, 3.3);
  private readonly frameFns = new Set<FrameFn>();
  private readonly clock = new THREE.Timer();
  /** Voxel post-processing: ambient occlusion in corners and contacts, bloom on lights. */
  private readonly post: VoxelPost;
  private readonly limiter = new FrameLimiter();
  private effects: GraphicsSettings['effects'] = 'high';
  private readonly unsubscribe: () => void;

  constructor(private readonly container: HTMLElement) {
    this.gl = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
    this.gl.shadowMap.enabled = true;
    this.gl.shadowMap.type = THREE.PCFShadowMap;
    this.gl.toneMapping = THREE.ACESFilmicToneMapping;
    this.gl.toneMappingExposure = 1.2;
    container.append(this.gl.domElement);

    this.labels.domElement.className = 'labels-layer';
    container.append(this.labels.domElement);

    this.scene.background = skyGradient(['#6fa6dc', '#b8cfe0', '#f4d2a2']);
    // Thin, cool haze: distance reads as aerial perspective, not as a sepia wash.
    this.scene.fog = new THREE.FogExp2(0xbfcad3, 0.007);

    this.scene.add(new THREE.HemisphereLight(0xb4d6ff, 0x5a5248, 1.3));
    this.sun.castShadow = true;
    this.sun.shadow.mapSize.set(2048, 2048);
    this.sun.shadow.bias = -0.0004;
    this.sun.shadow.normalBias = 0.02;
    this.scene.add(this.sun, this.sun.target);

    this.post = voxelPost(this.gl, this.scene, this.camera);

    new ResizeObserver(() => this.resize()).observe(container);
    this.unsubscribe = onGraphics((g) => this.applyGraphics(g));
    this.gl.setAnimationLoop(() => this.frame());
  }

  /** Graphics settings, live: resolution, shadows and how much post-processing. */
  private applyGraphics(g: GraphicsSettings): void {
    this.effects = g.effects;
    this.post.ao.enabled = g.effects === 'high';
    if (this.gl.shadowMap.enabled !== g.shadows) {
      this.gl.shadowMap.enabled = g.shadows;
      this.sun.castShadow = g.shadows;
      // Materials compile with or without shadow lookups: rebuild them.
      this.scene.traverse((o) => {
        const m = (o as THREE.Mesh).material;
        for (const mat of Array.isArray(m) ? m : m ? [m] : []) mat.needsUpdate = true;
      });
    }
    this.gl.setPixelRatio(pixelRatio());
    this.resize();
  }

  dispose(): void {
    this.unsubscribe();
    this.gl.setAnimationLoop(null);
    this.post.composer.dispose();
    this.gl.dispose();
  }

  /** Points the sun and its shadow camera at the whole map. */
  fitMap(width: number, height: number): void {
    const cx = width / 2;
    const cz = height / 2;
    const r = Math.hypot(width, height) / 2 + 2;
    this.sun.position.set(cx - 18, 34, cz - 12);
    this.sun.target.position.set(cx, 0, cz);
    const cam = this.sun.shadow.camera;
    cam.left = -r;
    cam.right = r;
    cam.top = r;
    cam.bottom = -r;
    cam.near = 1;
    cam.far = 90;
    cam.updateProjectionMatrix();
  }

  onFrame(fn: FrameFn): () => void {
    this.frameFns.add(fn);
    return () => this.frameFns.delete(fn);
  }

  private resize(): void {
    const w = this.container.clientWidth || window.innerWidth;
    const h = this.container.clientHeight || window.innerHeight;
    this.gl.setSize(w, h);
    this.post.composer.setPixelRatio(this.gl.getPixelRatio());
    this.post.composer.setSize(w, h);
    this.labels.setSize(w, h);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  /** Draws one frame as the player sees it: scene with post-processing, then the CSS2D labels. */
  render(dt = 0): void {
    if (this.effects === 'low') this.gl.render(this.scene, this.camera);
    else this.post.composer.render(dt);
    this.labels.render(this.scene, this.camera);
  }

  private frame(): void {
    // With a frame-rate cap, skipped ticks neither simulate nor draw: dt spans them.
    if (!this.limiter.ready()) return;
    this.clock.update();
    const dt = Math.min(this.clock.getDelta(), 0.1);
    const time = this.clock.getElapsed();
    for (const fn of this.frameFns) fn(dt, time);
    this.render(dt);
  }
}
