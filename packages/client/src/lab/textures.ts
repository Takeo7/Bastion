import * as THREE from 'three';

// Procedural canvas textures for the style lab: nothing is loaded from disk.

function canvas(w: number, h: number): [HTMLCanvasElement, CanvasRenderingContext2D] {
  const c = document.createElement('canvas');
  c.width = w;
  c.height = h;
  return [c, c.getContext('2d')!];
}

/** Deterministic pseudo-random generator (mulberry32). */
export function rng(seed: number): () => number {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function finish(c: HTMLCanvasElement, opts: { srgb?: boolean; nearest?: boolean; repeat?: boolean } = {}): THREE.CanvasTexture {
  const tex = new THREE.CanvasTexture(c);
  if (opts.srgb !== false) tex.colorSpace = THREE.SRGBColorSpace;
  if (opts.nearest) {
    tex.magFilter = THREE.NearestFilter;
    tex.minFilter = THREE.NearestFilter;
    tex.generateMipmaps = false;
  } else tex.anisotropy = 8;
  if (opts.repeat) tex.wrapS = tex.wrapT = THREE.RepeatWrapping;
  return tex;
}

/** Soft radial glow for flashes and smoke. */
export function glowTexture(): THREE.CanvasTexture {
  const [c, g] = canvas(64, 64);
  const grad = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  grad.addColorStop(0, 'rgba(255,255,255,1)');
  grad.addColorStop(0.35, 'rgba(255,255,255,0.6)');
  grad.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = grad;
  g.fillRect(0, 0, 64, 64);
  return finish(c);
}

/** Vertical gradient for the sky / backdrop; `stops` go from top to bottom. */
export function gradientTexture(stops: [number, string][], radial = false): THREE.CanvasTexture {
  const [c, g] = canvas(radial ? 512 : 4, 512);
  const grad = radial ? g.createRadialGradient(256, 300, 20, 256, 300, 420) : g.createLinearGradient(0, 0, 0, 512);
  for (const [t, color] of stops) grad.addColorStop(t, color);
  g.fillStyle = grad;
  g.fillRect(0, 0, c.width, c.height);
  return finish(c);
}

/** Tileable value noise in grey levels [lo, hi]; used as grime map, roughness and bump. */
export function noiseTexture(size = 256, lo = 0.75, hi = 1, seed = 7, octaves = 4): THREE.CanvasTexture {
  const [c, g] = canvas(size, size);
  const img = g.createImageData(size, size);
  const rand = rng(seed);
  const grids = Array.from({ length: octaves }, (_, o) => {
    const n = 4 << o;
    return { n, v: Array.from({ length: n * n }, rand) };
  });
  const smooth = (t: number) => t * t * (3 - 2 * t);
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      let v = 0;
      let amp = 0.5;
      let norm = 0;
      for (const { n, v: cells } of grids) {
        const fx = (x / size) * n;
        const fy = (y / size) * n;
        const x0 = Math.floor(fx) % n;
        const y0 = Math.floor(fy) % n;
        const x1 = (x0 + 1) % n;
        const y1 = (y0 + 1) % n;
        const tx = smooth(fx - Math.floor(fx));
        const ty = smooth(fy - Math.floor(fy));
        const a = cells[y0 * n + x0]! * (1 - tx) + cells[y0 * n + x1]! * tx;
        const b = cells[y1 * n + x0]! * (1 - tx) + cells[y1 * n + x1]! * tx;
        v += (a * (1 - ty) + b * ty) * amp;
        norm += amp;
        amp *= 0.5;
      }
      const k = Math.round((lo + (hi - lo) * (v / norm)) * 255);
      const i = (y * size + x) * 4;
      img.data[i] = img.data[i + 1] = img.data[i + 2] = k;
      img.data[i + 3] = 255;
    }
  }
  g.putImageData(img, 0, 0);
  return finish(c, { srgb: false, repeat: true });
}

/** Brick courses for walls (grey-scale, multiplied by the material colour). */
export function brickTexture(): THREE.CanvasTexture {
  const [c, g] = canvas(256, 256);
  const rand = rng(11);
  g.fillStyle = '#9a9a9a';
  g.fillRect(0, 0, 256, 256);
  const rows = 8;
  const h = 256 / rows;
  for (let r = 0; r < rows; r++) {
    const offset = r % 2 ? 32 : 0;
    for (let x = -offset; x < 256; x += 64) {
      const k = 200 + Math.floor(rand() * 55);
      g.fillStyle = `rgb(${k},${k},${k})`;
      g.fillRect(x + 2, r * h + 2, 60, h - 4);
    }
  }
  return finish(c, { srgb: false, repeat: true });
}

/** Paper grain for the ink style (multiplied over the frame). */
export function paperTexture(): THREE.CanvasTexture {
  const [c, g] = canvas(512, 512);
  const img = g.createImageData(512, 512);
  const rand = rng(3);
  for (let i = 0; i < 512 * 512; i++) {
    const k = 236 + Math.floor(rand() * 19);
    img.data[i * 4] = k;
    img.data[i * 4 + 1] = k - 2;
    img.data[i * 4 + 2] = k - 8;
    img.data[i * 4 + 3] = 255;
  }
  g.putImageData(img, 0, 0);
  g.globalAlpha = 0.05;
  g.strokeStyle = '#7a6a50';
  for (let i = 0; i < 260; i++) {
    g.beginPath();
    const x = rand() * 512;
    const y = rand() * 512;
    g.moveTo(x, y);
    g.lineTo(x + (rand() - 0.5) * 30, y + (rand() - 0.5) * 30);
    g.stroke();
  }
  return finish(c, { srgb: false, repeat: true });
}

/** Banded lighting ramp for toon materials. */
export function toonRamp(levels: number[]): THREE.DataTexture {
  const data = new Uint8Array(levels.length * 4);
  levels.forEach((l, i) => {
    data[i * 4] = data[i * 4 + 1] = data[i * 4 + 2] = Math.round(l * 255);
    data[i * 4 + 3] = 255;
  });
  const tex = new THREE.DataTexture(data, levels.length, 1, THREE.RGBAFormat);
  tex.magFilter = tex.minFilter = THREE.NearestFilter;
  tex.generateMipmaps = false;
  tex.needsUpdate = true;
  return tex;
}

// ------------------------------------------------------------------ ground

export interface GroundPaint {
  /** Texture pixels per tile. */
  px: number;
  asphalt: string;
  sidewalk: string;
  curb: string;
  paint: string;
  /** Per-texel brightness jitter (0 = flat colour). */
  jitter: number;
  /** Cracks, stains and patches. */
  wear: boolean;
  /** Paving joints on the sidewalk. */
  joints: boolean;
  nearest?: boolean;
  /** Pencil-like strokes instead of clean shapes. */
  sketch?: boolean;
  sketchColor?: string;
}

export interface GroundTextures {
  map: THREE.CanvasTexture;
  /** Roughness (green channel): puddles are smooth. */
  rough: THREE.CanvasTexture;
}

/**
 * Paints the street: sidewalk rows `[sidewalkFrom, sidewalkTo)` along the
 * top, asphalt below with a dashed centre line and a zebra crossing.
 */
export function groundTextures(w: number, h: number, sidewalkTo: number, laneZ: number, crossX: [number, number], p: GroundPaint): GroundTextures {
  const W = w * p.px;
  const H = h * p.px;
  const [c, g] = canvas(W, H);
  const [rc, rg] = canvas(W, H);
  const rand = rng(21);
  const T = p.px;

  g.fillStyle = p.asphalt;
  g.fillRect(0, 0, W, H);
  g.fillStyle = p.sidewalk;
  g.fillRect(0, 0, W, sidewalkTo * T);
  rg.fillStyle = '#c8c8c8';
  rg.fillRect(0, 0, W, H);

  if (p.joints) {
    g.strokeStyle = 'rgba(0,0,0,0.18)';
    g.lineWidth = Math.max(1, T / 32);
    for (let x = 0; x <= w * 2; x++) {
      g.beginPath();
      g.moveTo((x * T) / 2, 0);
      g.lineTo((x * T) / 2, sidewalkTo * T);
      g.stroke();
    }
    for (let y = 0; y <= sidewalkTo * 2; y++) {
      g.beginPath();
      g.moveTo(0, (y * T) / 2);
      g.lineTo(W, (y * T) / 2);
      g.stroke();
    }
  }
  // Curb.
  g.fillStyle = p.curb;
  g.fillRect(0, sidewalkTo * T - T * 0.1, W, T * 0.14);

  // Lane markings and zebra crossing.
  g.fillStyle = p.paint;
  for (let x = 0; x < w; x += 2) g.fillRect(x * T + T * 0.2, laneZ * T - T * 0.05, T * 1.1, T * 0.1);
  for (let y = sidewalkTo + 0.4; y < h - 0.3; y += 0.7) {
    g.fillRect(crossX[0] * T, y * T, (crossX[1] - crossX[0]) * T, T * 0.35);
  }

  if (p.wear) {
    // Stains, patches and puddles (puddles are dark and smooth).
    for (let i = 0; i < 26; i++) {
      const x = rand() * W;
      const y = sidewalkTo * T + rand() * (H - sidewalkTo * T);
      const r = (0.3 + rand() * 1.1) * T;
      const grad = g.createRadialGradient(x, y, 0, x, y, r);
      grad.addColorStop(0, 'rgba(0,0,0,0.28)');
      grad.addColorStop(1, 'rgba(0,0,0,0)');
      g.fillStyle = grad;
      g.fillRect(x - r, y - r, r * 2, r * 2);
      if (i % 2 === 0) {
        // Flat-bottomed puddles: a wide roughness ramp reflects as a bright ring.
        const rgrad = rg.createRadialGradient(x, y, 0, x, y, r * 0.8);
        rgrad.addColorStop(0, 'rgb(28,28,28)');
        rgrad.addColorStop(0.8, 'rgb(28,28,28)');
        rgrad.addColorStop(1, 'rgba(200,200,200,0)');
        rg.fillStyle = rgrad;
        rg.fillRect(x - r, y - r, r * 2, r * 2);
      }
    }
    g.strokeStyle = 'rgba(0,0,0,0.35)';
    g.lineWidth = Math.max(1, T / 40);
    for (let i = 0; i < 40; i++) {
      let x = rand() * W;
      let y = rand() * H;
      g.beginPath();
      g.moveTo(x, y);
      for (let k = 0; k < 6; k++) {
        x += (rand() - 0.5) * T * 0.6;
        y += (rand() - 0.5) * T * 0.6;
        g.lineTo(x, y);
      }
      g.stroke();
    }
  }

  if (p.sketch) {
    g.strokeStyle = p.sketchColor ?? 'rgba(60,40,30,0.22)';
    g.lineWidth = 1;
    for (let i = 0; i < 900; i++) {
      const x = rand() * W;
      const y = rand() * H;
      const len = T * (0.15 + rand() * 0.3);
      g.beginPath();
      g.moveTo(x, y);
      g.lineTo(x + len, y - len * 0.5);
      g.stroke();
    }
  }

  if (p.jitter > 0) {
    const img = g.getImageData(0, 0, W, H);
    const block = p.nearest ? 1 : 1;
    for (let y = 0; y < H; y += block) {
      for (let x = 0; x < W; x += block) {
        const j = 1 + (rand() - 0.5) * p.jitter;
        const i = (y * W + x) * 4;
        img.data[i] = Math.min(255, img.data[i]! * j);
        img.data[i + 1] = Math.min(255, img.data[i + 1]! * j);
        img.data[i + 2] = Math.min(255, img.data[i + 2]! * j);
      }
    }
    g.putImageData(img, 0, 0);
  }

  return { map: finish(c, { nearest: p.nearest }), rough: finish(rc, { srgb: false, nearest: p.nearest }) };
}

/** Bright tactical grid on a dark floor, for the holographic table. */
export function holoGridTexture(w: number, h: number): THREE.CanvasTexture {
  const T = 64;
  const [c, g] = canvas(w * T, h * T);
  g.fillStyle = '#03090d';
  g.fillRect(0, 0, c.width, c.height);
  g.strokeStyle = 'rgba(80,220,255,0.55)';
  g.lineWidth = 2;
  for (let x = 0; x <= w; x++) {
    g.beginPath();
    g.moveTo(x * T, 0);
    g.lineTo(x * T, c.height);
    g.stroke();
  }
  for (let y = 0; y <= h; y++) {
    g.beginPath();
    g.moveTo(0, y * T);
    g.lineTo(c.width, y * T);
    g.stroke();
  }
  g.fillStyle = 'rgba(80,220,255,0.9)';
  for (let x = 0; x <= w; x++) for (let y = 0; y <= h; y++) g.fillRect(x * T - 3, y * T - 3, 6, 6);
  return finish(c);
}

/** Small noisy tile for "crunchy" low-resolution textures (PlayStation look). */
export function crunchyTexture(size = 32, lo = 0.7, seed = 5): THREE.CanvasTexture {
  const [c, g] = canvas(size, size);
  const img = g.createImageData(size, size);
  const rand = rng(seed);
  for (let i = 0; i < size * size; i++) {
    const k = Math.round((lo + rand() * (1 - lo)) * 255);
    img.data[i * 4] = img.data[i * 4 + 1] = img.data[i * 4 + 2] = k;
    img.data[i * 4 + 3] = 255;
  }
  g.putImageData(img, 0, 0);
  return finish(c, { srgb: false, nearest: true, repeat: true });
}

/** A field of stars for catalogue-style space backdrops. */
export function starfieldTexture(): THREE.CanvasTexture {
  const [c, g] = canvas(1024, 640);
  const grad = g.createRadialGradient(512, 360, 40, 512, 360, 640);
  grad.addColorStop(0, '#24407a');
  grad.addColorStop(0.55, '#101d42');
  grad.addColorStop(1, '#050818');
  g.fillStyle = grad;
  g.fillRect(0, 0, 1024, 640);
  const rand = rng(77);
  for (let i = 0; i < 420; i++) {
    const r = rand() < 0.08 ? 1.6 : 0.8;
    g.fillStyle = `rgba(255,255,255,${0.35 + rand() * 0.65})`;
    g.beginPath();
    g.arc(rand() * 1024, rand() * 640, r, 0, Math.PI * 2);
    g.fill();
  }
  return finish(c);
}

// ------------------------------------------------------------- gritty set

/**
 * Low-resolution "photo" detail textures for the early-2000s gritty styles:
 * near-grey multipliers (the role colour tints them) with bricks, concrete
 * pits and streaks, rust, planks, weave and leaves.
 */
export interface GritTextures {
  brick: THREE.CanvasTexture;
  concrete: THREE.CanvasTexture;
  metal: THREE.CanvasTexture;
  wood: THREE.CanvasTexture;
  fabric: THREE.CanvasTexture;
  foliage: THREE.CanvasTexture;
  generic: THREE.CanvasTexture;
}

function speckle(img: ImageData, rand: () => number, amount: number): void {
  for (let i = 0; i < img.data.length; i += 4) {
    const j = (rand() - 0.5) * amount;
    img.data[i] = Math.max(0, Math.min(255, img.data[i]! + j));
    img.data[i + 1] = Math.max(0, Math.min(255, img.data[i + 1]! + j));
    img.data[i + 2] = Math.max(0, Math.min(255, img.data[i + 2]! + j));
  }
}

function stains(g: CanvasRenderingContext2D, size: number, rand: () => number, count: number, colour: string, max = 0.25): void {
  for (let i = 0; i < count; i++) {
    const x = rand() * size;
    const y = rand() * size;
    const r = size * (0.04 + rand() * 0.14);
    const grad = g.createRadialGradient(x, y, 0, x, y, r);
    grad.addColorStop(0, colour.replace('A', (max * (0.4 + rand() * 0.6)).toFixed(2)));
    grad.addColorStop(1, colour.replace('A', '0'));
    g.fillStyle = grad;
    g.fillRect(x - r, y - r, r * 2, r * 2);
  }
}

const gritCache = new Map<string, GritTextures>();

export function gritTextures(size: number, nearest: boolean): GritTextures {
  const key = `${size}|${nearest}`;
  const hit = gritCache.get(key);
  if (hit) return hit;
  const S = size;
  const k = S / 128;
  const done = (c: HTMLCanvasElement, g: CanvasRenderingContext2D, rand: () => number, noise: number) => {
    const img = g.getImageData(0, 0, S, S);
    speckle(img, rand, noise);
    g.putImageData(img, 0, 0);
    return finish(c, { srgb: false, nearest, repeat: true });
  };

  // Bricks in running bond, each one a slightly different shade.
  const brick = (() => {
    const [c, g] = canvas(S, S);
    const rand = rng(101);
    g.fillStyle = '#9a958d';
    g.fillRect(0, 0, S, S);
    const bh = 6 * k;
    const bw = 18 * k;
    for (let row = 0; row * bh < S; row++) {
      const off = row % 2 ? bw / 2 : 0;
      for (let x = -off; x < S; x += bw) {
        const v = 170 + rand() * 70;
        const warm = rand() * 14;
        g.fillStyle = `rgb(${v + warm},${v},${v - warm})`;
        g.fillRect(x + k, row * bh + k, bw - 2 * k, bh - 2 * k);
        if (rand() < 0.12) {
          g.fillStyle = 'rgba(40,30,25,0.35)';
          g.fillRect(x + k, row * bh + k, bw - 2 * k, bh - 2 * k);
        }
      }
    }
    stains(g, S, rand, 7, 'rgba(30,25,20,A)', 0.3);
    return done(c, g, rand, 26);
  })();

  // Poured concrete: pits, rain streaks, form-tie holes and a seam.
  const concrete = (() => {
    const [c, g] = canvas(S, S);
    const rand = rng(202);
    g.fillStyle = '#c8c4bc';
    g.fillRect(0, 0, S, S);
    stains(g, S, rand, 14, 'rgba(60,58,52,A)', 0.22);
    for (let i = 0; i < 12; i++) {
      const x = rand() * S;
      const grad = g.createLinearGradient(0, 0, 0, S);
      grad.addColorStop(0, 'rgba(50,48,44,0.28)');
      grad.addColorStop(1, 'rgba(50,48,44,0)');
      g.fillStyle = grad;
      g.fillRect(x, 0, (1 + rand() * 3) * k, S * (0.3 + rand() * 0.7));
    }
    g.fillStyle = 'rgba(30,30,30,0.5)';
    for (let i = 0; i < 40; i++) g.fillRect(rand() * S, rand() * S, k, k);
    for (const [x, y] of [[0.25, 0.25], [0.75, 0.25], [0.25, 0.75], [0.75, 0.75]] as const) {
      g.beginPath();
      g.arc(x * S, y * S, 2 * k, 0, Math.PI * 2);
      g.fill();
    }
    g.fillStyle = 'rgba(40,40,40,0.35)';
    g.fillRect(0, S / 2, S, k);
    return done(c, g, rand, 30);
  })();

  // Painted metal with rust blooms, scratches and rivets.
  const metal = (() => {
    const [c, g] = canvas(S, S);
    const rand = rng(303);
    g.fillStyle = '#dcdcd8';
    g.fillRect(0, 0, S, S);
    for (let y = 0; y < S; y += k) {
      g.fillStyle = `rgba(255,255,255,${rand() * 0.08})`;
      g.fillRect(0, y, S, k);
    }
    stains(g, S, rand, 9, 'rgba(120,62,30,A)', 0.75);
    stains(g, S, rand, 6, 'rgba(70,38,20,A)', 0.5);
    g.strokeStyle = 'rgba(255,255,255,0.35)';
    g.lineWidth = k;
    for (let i = 0; i < 10; i++) {
      const x = rand() * S;
      const y = rand() * S;
      g.beginPath();
      g.moveTo(x, y);
      g.lineTo(x + (rand() - 0.5) * 30 * k, y + (rand() - 0.5) * 8 * k);
      g.stroke();
    }
    g.fillStyle = 'rgba(30,30,30,0.6)';
    for (let x = 6 * k; x < S; x += 16 * k) {
      g.fillRect(x, 4 * k, 2 * k, 2 * k);
      g.fillRect(x, S - 6 * k, 2 * k, 2 * k);
    }
    return done(c, g, rand, 22);
  })();

  // Weathered planks with grain and knots.
  const wood = (() => {
    const [c, g] = canvas(S, S);
    const rand = rng(404);
    const pw = 32 * k;
    for (let p = 0; p < 4; p++) {
      const base = 170 + rand() * 50;
      for (let x = 0; x < pw; x += k) {
        const v = base + Math.sin(x * 0.7 + rand() * 0.3) * 12;
        g.fillStyle = `rgb(${v},${v * 0.92},${v * 0.82})`;
        g.fillRect(p * pw + x, 0, k, S);
      }
      for (let i = 0; i < 6; i++) {
        g.fillStyle = 'rgba(60,40,25,0.3)';
        g.fillRect(p * pw + rand() * pw, 0, k, S);
      }
      g.fillStyle = 'rgba(40,28,18,0.7)';
      g.fillRect(p * pw, 0, k, S);
      g.beginPath();
      g.ellipse(p * pw + pw * (0.3 + rand() * 0.4), rand() * S, 2.5 * k, 4 * k, 0, 0, Math.PI * 2);
      g.fill();
    }
    stains(g, S, rand, 5, 'rgba(40,30,20,A)', 0.3);
    return done(c, g, rand, 20);
  })();

  // Coarse canvas weave (sandbags).
  const fabric = (() => {
    const [c, g] = canvas(S, S);
    const rand = rng(505);
    for (let y = 0; y < S; y += 2 * k) {
      for (let x = 0; x < S; x += 2 * k) {
        const v = ((x + y) / (2 * k)) % 2 ? 205 : 180;
        g.fillStyle = `rgb(${v},${v},${v - 6})`;
        g.fillRect(x, y, 2 * k, 2 * k);
      }
    }
    stains(g, S, rand, 10, 'rgba(60,50,35,A)', 0.35);
    return done(c, g, rand, 28);
  })();

  // Clumps of leaves.
  const foliage = (() => {
    const [c, g] = canvas(S, S);
    const rand = rng(606);
    g.fillStyle = '#6a6a6a';
    g.fillRect(0, 0, S, S);
    for (let i = 0; i < 260; i++) {
      const v = 90 + rand() * 150;
      g.fillStyle = `rgb(${v},${v},${v})`;
      g.beginPath();
      g.ellipse(rand() * S, rand() * S, (2 + rand() * 3) * k, (1 + rand() * 2) * k, rand() * 3, 0, Math.PI * 2);
      g.fill();
    }
    return done(c, g, rand, 20);
  })();

  // Generic grime for everything else (units included).
  const generic = (() => {
    const [c, g] = canvas(S, S);
    const rand = rng(707);
    g.fillStyle = '#d2d0cc';
    g.fillRect(0, 0, S, S);
    stains(g, S, rand, 16, 'rgba(40,36,30,A)', 0.3);
    g.strokeStyle = 'rgba(255,255,255,0.25)';
    g.lineWidth = k;
    for (let i = 0; i < 8; i++) {
      const x = rand() * S;
      const y = rand() * S;
      g.beginPath();
      g.moveTo(x, y);
      g.lineTo(x + (rand() - 0.5) * 20 * k, y + (rand() - 0.5) * 20 * k);
      g.stroke();
    }
    return done(c, g, rand, 26);
  })();

  const set = { brick, concrete, metal, wood, fabric, foliage, generic };
  gritCache.set(key, set);
  return set;
}
