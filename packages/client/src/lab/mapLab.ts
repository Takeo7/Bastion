import { mareaThreshold, REGIONS, ROADS, type RegionId } from '@bastion/engine';
import '@fontsource/barlow/latin-500.css';
import '@fontsource/barlow/latin-700.css';
import '@fontsource/oswald/latin-500.css';
import '@fontsource/oswald/latin-600.css';
import * as THREE from 'three';
import type { BaseModelId } from '../game/syntyManifest';
import { KitBatch, kitModel, loadBaseKit } from '../strategy/baseKit';
import { GEOGRAPHY as G } from '../strategy/geoscape';

// Map lab (/mapa.html): sketches of the valley as a 2D map (Arturo,
// 2026-10-14: «que el valle sea un mapa y no un 3D»), one per style, from
// the same geography the 3D map draws (GEOGRAPHY) and the same campaign
// moment on each: the Marea up from the south, La Mula on its way to La Vega,
// two Nidos and two operations. `?estilo=carreteras|topo|atlas`;
// `__mapa.snap(name)` saves the canvas to the dev server's /__snap.

const params = new URLSearchParams(location.search);
const style = (params.get('estilo') ?? 'carreteras') as 'carreteras' | 'topo' | 'atlas';

const PX = 1600;
const S = PX / G.width;
const PY = Math.round(G.depth * S);
const px = (x: number) => (x + G.width / 2) * S;
const py = (z: number) => (z + G.depth / 2) * S;

const canvas = document.createElement('canvas');
canvas.width = PX;
canvas.height = PY;
canvas.style.cssText = 'display: block; max-width: 100vw; max-height: 100vh; margin: auto';
document.body.append(canvas);
const ctx = canvas.getContext('2d')!;

// ------------------------------------------------------------ the land, sampled

/** Half the canvas's resolution: what the land's colours are worked out at. */
const GW = PX / 2;
const GH = Math.round(PY / 2);
const at = (i: number, j: number) => ({ x: ((i + 0.5) / GW) * G.width - G.width / 2, z: ((j + 0.5) / GH) * G.depth - G.depth / 2 });
const H = new Float32Array(GW * GH);
const WOOD = new Float32Array(GW * GH);
const ASH = new Float32Array(GW * GH);
const MAREA = new Float32Array(GW * GH);
const inCity = (x: number, z: number, margin = 0) =>
  G.cities.some((c) => {
    const z0 = REGIONS[c.id];
    return Math.abs(x - z0.x - c.dx) < (c.cols * c.cell) / 2 + margin && Math.abs(z - z0.z - c.dz) < (c.rows * c.cell) / 2 + margin;
  });
for (let j = 0; j < GH; j++)
  for (let i = 0; i < GW; i++) {
    const { x, z } = at(i, j);
    const k = j * GW + i;
    H[k] = G.heightAt(x, z);
    WOOD[k] = inCity(x, z, 0.3) ? 0 : G.woods(x, z) - G.woodsAbove;
    ASH[k] = G.ashDepth(x, z);
    MAREA[k] = G.mareaAt(x, z);
  }
const water = (k: number, x: number, z: number) => H[k]! < -0.05 || Math.hypot(x - G.reservoir.x, z - G.reservoir.z) < G.reservoir.r - 0.1;

/** The Marea where it stands in the sketch: past the zones one road from Los Llanos. */
const LEVEL = Math.max(mareaThreshold('na') ?? 0, mareaThreshold('afr') ?? 0) + 0.3;
const fallen = (id: RegionId) => (mareaThreshold(id) ?? 99) <= LEVEL;

/** Light from the north-west on the slopes: 0 in shade, 1 facing it. */
function shade(i: number, j: number, k = 1.6): number {
  const l = (a: number, b: number) => H[Math.min(GH - 1, Math.max(0, b)) * GW + Math.min(GW - 1, Math.max(0, a))]!;
  const dx = (l(i + 1, j) - l(i - 1, j)) * k;
  const dz = (l(i, j + 1) - l(i, j - 1)) * k;
  const n = new THREE.Vector3(-dx, 2 * (G.width / GW), -dz).normalize();
  return Math.max(0, n.dot(new THREE.Vector3(-0.6, 0.65, -0.45).normalize()));
}

/** The land as an image at half resolution, from a colour for each sample, drawn up to the canvas. */
function paint(colour: (k: number, i: number, j: number, x: number, z: number) => [number, number, number, number?]): void {
  const half = document.createElement('canvas');
  half.width = GW;
  half.height = GH;
  const c = half.getContext('2d')!;
  const img = c.createImageData(GW, GH);
  for (let j = 0; j < GH; j++)
    for (let i = 0; i < GW; i++) {
      const { x, z } = at(i, j);
      const k = j * GW + i;
      const [r, g, b, a = 255] = colour(k, i, j, x, z);
      img.data.set([r, g, b, a], k * 4);
    }
  c.putImageData(img, 0, 0);
  ctx.imageSmoothingEnabled = true;
  ctx.drawImage(half, 0, 0, PX, PY);
}

const mix = (a: number[], b: number[], t: number) => a.map((v, i) => v + (b[i]! - v) * t) as [number, number, number];
const hex = (h: number): [number, number, number] => [(h >> 16) & 255, (h >> 8) & 255, h & 255];
/** A little grain, the same on every run. */
const grain = (i: number, j: number) => {
  const s = Math.sin(i * 12.9898 + j * 78.233) * 43758.5453;
  return s - Math.floor(s);
};

// ------------------------------------------------------------ what is on it

const ROAD_LINES = ROADS.map(([a, b]) => ({ a, b, line: G.road(a, b) }));

function polyline(line: [number, number][], width: number, color: string, dash: number[] = []): void {
  ctx.beginPath();
  line.forEach(([x, z], i) => (i ? ctx.lineTo(px(x), py(z)) : ctx.moveTo(px(x), py(z))));
  ctx.lineWidth = width;
  ctx.strokeStyle = color;
  ctx.lineCap = ctx.lineJoin = 'round';
  ctx.setLineDash(dash);
  ctx.stroke();
  ctx.setLineDash([]);
}

function roads(layers: { width: number; color: string; dash?: number[] }[]): void {
  for (const l of layers) for (const r of ROAD_LINES) polyline(r.line, l.width, l.color, l.dash);
}

function river(width: number, color: string): void {
  polyline(G.river, width, color);
}

/** Each city's blocks (a park, the square at its marker). */
function cityBlocks(each: (x: number, z: number, w: number, kind: 'built' | 'park' | 'square') => void): void {
  for (const c of G.cities) {
    const cx = REGIONS[c.id].x + c.dx;
    const cz = REGIONS[c.id].z + c.dz;
    for (let j = 0; j < c.rows; j++)
      for (let i = 0; i < c.cols; i++) {
        const x = cx - (c.cols * c.cell) / 2 + (i + 0.5) * c.cell;
        const z = cz - (c.rows * c.cell) / 2 + (j + 0.5) * c.cell;
        if (G.heightAt(x, z) < -0.05) continue;
        const corner = (i === 0 || i === c.cols - 1) && (j === 0 || j === c.rows - 1);
        if (corner && (i + j + c.id.length) % 2) continue;
        const kind = i === Math.floor(c.cols / 2) - 2 && j === Math.floor(c.rows / 2) + 1 ? 'park' : Math.hypot(x - REGIONS[c.id].x, z - REGIONS[c.id].z) < c.cell * 0.75 ? 'square' : 'built';
        each(x, z, c.cell, kind);
      }
  }
}

/** Each town's houses along its lane. */
function townHouses(each: (x: number, z: number, axis: number, t: (typeof G.towns)[number]) => void): void {
  for (const t of G.towns) {
    const dir = [Math.cos(t.axis), Math.sin(t.axis)];
    const half = Math.ceil(t.n / 2) * 0.22 + 0.12;
    for (let k = 0; k < t.n; k++) {
      const side = k % 2 ? 1 : -1;
      const s = -half + 0.24 + Math.floor(k / 2) * 0.44;
      each(t.x + dir[0]! * s - dir[1]! * side * 0.25, t.z + dir[1]! * s + dir[0]! * side * 0.25, t.axis, t);
    }
  }
}

const ICONS = ['home', 'r-mula', 'biohazard', 'target', 'skull', 'mh-flag'] as const;
const icons = new Map<string, HTMLImageElement>();
async function loadIcons(): Promise<void> {
  await Promise.all(
    ICONS.map(
      (id) =>
        new Promise<void>((done) => {
          const img = new Image();
          img.onload = () => done();
          img.onerror = () => done();
          img.src = `/ui/synty/icons/${id}.webp`;
          icons.set(id, img);
        }),
    ),
  );
}

/** A Synty icon at (x, z) on a plate (`plate` its colour, null none), `size` pixels. */
function icon(id: (typeof ICONS)[number], x: number, z: number, size: number, plate: string | null, ring?: string): void {
  const cx = px(x);
  const cy = py(z);
  if (plate) {
    ctx.beginPath();
    ctx.arc(cx, cy, size * 0.62, 0, Math.PI * 2);
    ctx.fillStyle = plate;
    ctx.fill();
    if (ring) {
      ctx.lineWidth = 3;
      ctx.strokeStyle = ring;
      ctx.stroke();
    }
  }
  const img = icons.get(id);
  if (img?.complete && img.naturalWidth) ctx.drawImage(img, cx - size / 2, cy - size / 2, size, size);
}

function label(text: string, x: number, z: number, o: { font: string; color: string; halo?: string; haloWidth?: number; spacing?: number; align?: CanvasTextAlign; dy?: number }): void {
  ctx.font = o.font;
  ctx.textAlign = o.align ?? 'center';
  ctx.textBaseline = 'middle';
  (ctx as unknown as { letterSpacing: string }).letterSpacing = `${o.spacing ?? 0}px`;
  const cx = px(x);
  const cy = py(z) + (o.dy ?? 0);
  if (o.halo) {
    ctx.lineWidth = o.haloWidth ?? 4;
    ctx.strokeStyle = o.halo;
    ctx.lineJoin = 'round';
    ctx.strokeText(text, cx, cy);
  }
  ctx.fillStyle = o.color;
  ctx.fillText(text, cx, cy);
  (ctx as unknown as { letterSpacing: string }).letterSpacing = '0px';
}

/** The campaign's moment in the sketch: La Mula on the road from Aldren to La Vega, two Nidos, two operations. */
const MULA = (() => {
  const line = G.road('weu', 'oce');
  return { line, at: line[Math.round(line.length * 0.6)]! };
})();
const NIDOS: RegionId[] = ['afr', 'na'];
const OPS: RegionId[] = ['sa', 'eeu'];
const ZONE_IDS = Object.keys(REGIONS) as RegionId[];

/** Marching squares over the heights: the line segments where the ground is at `level`. */
function contour(level: number): [number, number, number, number][] {
  const out: [number, number, number, number][] = [];
  const v = (i: number, j: number) => H[j * GW + i]! - level;
  for (let j = 0; j + 1 < GH; j++)
    for (let i = 0; i + 1 < GW; i++) {
      const a = v(i, j);
      const b = v(i + 1, j);
      const c = v(i + 1, j + 1);
      const d = v(i, j + 1);
      const edges: [number, number][] = [];
      const cross = (p: number, q: number, x0: number, y0: number, x1: number, y1: number) => {
        if (p > 0 !== q > 0) {
          const t = p / (p - q);
          edges.push([x0 + (x1 - x0) * t, y0 + (y1 - y0) * t]);
        }
      };
      cross(a, b, i, j, i + 1, j);
      cross(b, c, i + 1, j, i + 1, j + 1);
      cross(c, d, i + 1, j + 1, i, j + 1);
      cross(d, a, i, j + 1, i, j);
      for (let e = 0; e + 1 < edges.length; e += 2) out.push([edges[e]![0], edges[e]![1], edges[e + 1]![0], edges[e + 1]![1]]);
    }
  return out;
}

function strokeContour(level: number, width: number, color: string): void {
  ctx.beginPath();
  for (const [x0, y0, x1, y1] of contour(level)) {
    ctx.moveTo((x0 + 0.5) * 2, (y0 + 0.5) * 2);
    ctx.lineTo((x1 + 0.5) * 2, (y1 + 0.5) * 2);
  }
  ctx.lineWidth = width;
  ctx.strokeStyle = color;
  ctx.stroke();
}

/** Diagonal hatching over the Marea (`color`, `gap` pixels apart). */
function hatchMarea(color: string, gap: number, width: number): void {
  const mask = document.createElement('canvas');
  mask.width = PX;
  mask.height = PY;
  const m = mask.getContext('2d')!;
  const half = document.createElement('canvas');
  half.width = GW;
  half.height = GH;
  const hc = half.getContext('2d')!;
  const img = hc.createImageData(GW, GH);
  for (let k = 0; k < GW * GH; k++) img.data.set([0, 0, 0, MAREA[k]! < LEVEL ? 255 : 0], k * 4);
  hc.putImageData(img, 0, 0);
  m.drawImage(half, 0, 0, PX, PY);
  m.globalCompositeOperation = 'source-in';
  m.strokeStyle = color;
  m.lineWidth = width;
  m.beginPath();
  for (let d = -PY; d < PX; d += gap) {
    m.moveTo(d, 0);
    m.lineTo(d + PY, PY);
  }
  m.stroke();
  ctx.drawImage(mask, 0, 0);
}

/** The Marea's edge as a line. */
function mareaEdge(color: string, width: number, dash: number[]): void {
  const keep = H.slice();
  H.set(MAREA.map((v) => v - LEVEL));
  ctx.setLineDash(dash);
  strokeContour(0, width, color);
  ctx.setLineDash([]);
  H.set(keep);
}

// ------------------------------------------------------------- the styles

/** A printed road map, folded and worn: cream paper, roads in red and yellow, towns in brick, the relief shaded. */
function roadMap(): void {
  const paper = hex(0xf1e9d4);
  paint((k, i, j, x, z) => {
    const h = H[k]!;
    let c = paper as number[];
    if (h > 2.3) c = mix(c, hex(0xe2d6bd), Math.min(1, (h - 2.3) / 1.2));
    if (WOOD[k]! > 0 && h < 2.8 && h > 0) c = mix(c, hex(0xcbdba9), Math.min(1, WOOD[k]! * 12));
    if (ASH[k]! > 0) c = mix(c, hex(0xd9d3c6), Math.min(1, ASH[k]! * 3));
    if (h > 4.1) c = [250, 248, 242];
    if (water(k, x, z)) c = hex(0xb4d3e4);
    const s = shade(i, j);
    c = mix(c, [120, 100, 80], Math.max(0, 0.62 - s) * 0.5);
    const g = (grain(i, j) - 0.5) * 9;
    return [c[0]! + g, c[1]! + g, c[2]! + g];
  });
  river(10, '#7fb0cf');
  river(7, '#b4d3e4');
  // Towns and cities in brick.
  cityBlocks((x, z, w, kind) => {
    ctx.fillStyle = kind === 'park' ? '#bfd29c' : kind === 'square' ? '#efe0c4' : '#d58a63';
    ctx.fillRect(px(x - w * 0.43), py(z - w * 0.43), w * 0.86 * S, w * 0.86 * S);
  });
  townHouses((x, z) => {
    ctx.fillStyle = '#c4664a';
    ctx.fillRect(px(x) - 5, py(z) - 5, 10, 10);
  });
  roads([
    { width: 9, color: '#7c2b20' },
    { width: 6.5, color: '#d8432e' },
    { width: 1.6, color: '#f6e3a0', dash: [10, 8] },
  ]);
  hatchMarea('rgba(120, 30, 40, 0.55)', 12, 2.5);
  mareaEdge('#7a1d27', 2.5, [12, 6]);
  // Road numbers, on their badges.
  ROAD_LINES.forEach((r, n) => {
    const [x, z] = r.line[Math.floor(r.line.length / 2)]!;
    ctx.fillStyle = '#f6f0de';
    ctx.strokeStyle = '#7c2b20';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.roundRect(px(x) - 22, py(z) - 12, 44, 24, 5);
    ctx.fill();
    ctx.stroke();
    label(`C-${n + 1}`, x, z, { font: '600 15px Oswald', color: '#7c2b20' });
  });
  for (const id of ZONE_IDS) {
    const r = REGIONS[id];
    label(r.name.toUpperCase(), r.x, r.z, { font: '600 26px Oswald', color: fallen(id) ? '#6b1b22' : '#2a2318', halo: '#f6f0de', haloWidth: 6, spacing: 2, dy: -34 });
  }
  overlays('#f6f0de', '#2a2318');
  // The folds: two across, three down, each a light ridge and its shadow; the paper darker at the edges.
  for (const f of [1, 2]) fold(0, (PY * f) / 3, PX, (PY * f) / 3);
  for (const f of [1, 2, 3]) fold((PX * f) / 4, 0, (PX * f) / 4, PY);
  const edge = ctx.createRadialGradient(PX / 2, PY / 2, PY * 0.45, PX / 2, PY / 2, PX * 0.72);
  edge.addColorStop(0, 'rgba(90, 60, 20, 0)');
  edge.addColorStop(1, 'rgba(90, 60, 20, 0.32)');
  ctx.fillStyle = edge;
  ctx.fillRect(0, 0, PX, PY);
  legend(['Carretera', 'Pueblo', 'Ciudad', 'Bajo la Marea'], ['#d8432e', '#c4664a', '#d58a63', 'rgba(120,30,40,0.6)'], '#f6f0de', '#2a2318', 'MAPA DE CARRETERAS · VALLE DE KESSRA');
}

function fold(x0: number, y0: number, x1: number, y1: number): void {
  for (const [off, color] of [
    [-1.5, 'rgba(255, 255, 255, 0.35)'],
    [1.5, 'rgba(70, 50, 20, 0.18)'],
  ] as const) {
    ctx.beginPath();
    const ox = y0 === y1 ? 0 : off;
    const oy = y0 === y1 ? off : 0;
    ctx.moveTo(x0 + ox, y0 + oy);
    ctx.lineTo(x1 + ox, y1 + oy);
    ctx.lineWidth = 3;
    ctx.strokeStyle = color;
    ctx.stroke();
  }
}

/** A topographic sheet: hypsometric tints, contours every 0.2 (thicker every 1), a grid with its references. */
function topoMap(): void {
  const tints: [number, number][] = [
    [0, 0xdfe7c4],
    [0.5, 0xd8dfb0],
    [1.0, 0xe6d9a2],
    [1.6, 0xdcc18c],
    [2.4, 0xcca57c],
    [3.4, 0xbd9a80],
    [4.4, 0xf2f0ec],
  ];
  paint((k, i, j, x, z) => {
    const h = H[k]!;
    if (water(k, x, z)) return [...hex(0xa3c6dc)] as [number, number, number];
    let c = hex(tints[0]![1]) as number[];
    for (let t = 1; t < tints.length; t++) if (h > tints[t - 1]![0]) c = mix(hex(tints[t - 1]![1]), hex(tints[t]![1]), Math.min(1, (h - tints[t - 1]![0]) / (tints[t]![0] - tints[t - 1]![0])));
    if (WOOD[k]! > 0 && h < 2.8 && h > 0) c = mix(c, hex(0xa9c78c), Math.min(0.8, WOOD[k]! * 10));
    if (ASH[k]! > 0) c = mix(c, hex(0xcfcac2), Math.min(1, ASH[k]! * 3));
    const s = shade(i, j);
    c = mix(c, [90, 80, 70], Math.max(0, 0.6 - s) * 0.35);
    return c as [number, number, number];
  });
  // Woods: a pattern of little trees.
  ctx.fillStyle = 'rgba(60, 110, 50, 0.55)';
  for (let y = 6; y < PY; y += 13)
    for (let x = ((y / 13) % 2) * 6; x < PX; x += 12) {
      const k = Math.floor(y / 2) * GW + Math.floor(x / 2);
      if (WOOD[k]! > 0.02 && H[k]! > 0 && H[k]! < 2.8) {
        ctx.beginPath();
        ctx.arc(x, y, 2.6, 0, Math.PI * 2);
        ctx.fill();
      }
    }
  for (let l = 0.2; l < 6; l += 0.2) {
    const index = Math.abs(l - Math.round(l)) < 0.01;
    strokeContour(l, index ? 1.5 : 0.7, index ? 'rgba(120, 72, 36, 0.85)' : 'rgba(140, 90, 50, 0.55)');
  }
  river(5, '#4f8ab3');
  ctx.strokeStyle = '#4f8ab3';
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.arc(px(G.reservoir.x), py(G.reservoir.z), (G.reservoir.r - 0.1) * S, 0, Math.PI * 2);
  ctx.stroke();
  cityBlocks((x, z, w, kind) => {
    if (kind === 'square') return;
    ctx.fillStyle = kind === 'park' ? 'rgba(60,110,50,0.5)' : '#3a3632';
    ctx.fillRect(px(x - w * 0.38), py(z - w * 0.38), w * 0.76 * S, w * 0.76 * S);
  });
  townHouses((x, z) => {
    ctx.fillStyle = '#2a2622';
    ctx.fillRect(px(x) - 3.5, py(z) - 3.5, 7, 7);
  });
  roads([
    { width: 6, color: '#2a2622' },
    { width: 3.4, color: '#f0a83c' },
  ]);
  hatchMarea('rgba(150, 40, 110, 0.5)', 9, 1.6);
  mareaEdge('#8a1f63', 2, []);
  // The grid: 2 units a square, lettered across, numbered down, on a white margin.
  ctx.strokeStyle = 'rgba(40, 50, 70, 0.35)';
  ctx.lineWidth = 1;
  for (let x = -G.width / 2; x <= G.width / 2; x += 2) {
    ctx.beginPath();
    ctx.moveTo(px(x), 0);
    ctx.lineTo(px(x), PY);
    ctx.stroke();
  }
  for (let z = -G.depth / 2; z <= G.depth / 2; z += 2) {
    ctx.beginPath();
    ctx.moveTo(0, py(z));
    ctx.lineTo(PX, py(z));
    ctx.stroke();
  }
  for (let x = -G.width / 2, n = 0; x < G.width / 2; x += 2, n++) label(String.fromCharCode(65 + n), x + 1, -G.depth / 2 + 0.25, { font: '600 14px Oswald', color: '#28324a', halo: '#f4f2ec' });
  for (let z = -G.depth / 2, n = 1; z < G.depth / 2; z += 2, n++) label(String(n), -G.width / 2 + 0.25, z + 1, { font: '600 14px Oswald', color: '#28324a', halo: '#f4f2ec' });
  for (const id of ZONE_IDS) {
    const r = REGIONS[id];
    label(r.name.toUpperCase(), r.x, r.z, { font: '500 22px Oswald', color: fallen(id) ? '#6d1350' : '#1d2230', halo: 'rgba(244,242,236,0.9)', haloWidth: 5, spacing: 3, dy: -32 });
  }
  overlays('#f4f2ec', '#1d2230');
  legend(['Carretera', 'Edificado', 'Curva de nivel', 'Bajo la Marea'], ['#f0a83c', '#3a3632', 'rgba(120,72,36,0.85)', 'rgba(150,40,110,0.55)'], '#f4f2ec', '#1d2230', 'KESSRA · HOJA ÚNICA · EQUIDISTANCIA 0,2');
}

/** An illustrated atlas: the land painted flat, the places drawn as Synty pictograms, the labels on our steel plates. */
async function atlasMap(): Promise<void> {
  paint((k, i, j, x, z) => {
    const h = H[k]!;
    if (water(k, x, z)) return mix(hex(0x6fa6c6), hex(0x8fc0d8), Math.min(1, Math.max(0, -h) * 2)) as [number, number, number];
    let c = mix(hex(0x9fbe72), hex(0xb7c784), Math.min(1, Math.max(0, h) / 1.2)) as number[];
    if (h > 2.2) c = mix(c, hex(0x8f8f8c), Math.min(1, (h - 2.2) / 1.0));
    if (h > 3.9) c = mix(c, hex(0xf3f3f0), Math.min(1, (h - 3.9) / 0.4));
    if (WOOD[k]! > 0 && h < 2.6 && h > 0) c = mix(c, hex(0x5f8a4a), Math.min(1, WOOD[k]! * 8));
    if (ASH[k]! > 0) c = mix(c, hex(0x8d877e), Math.min(1, ASH[k]! * 2.5));
    // The light in a few flat tones, as painted.
    const s = Math.round(shade(i, j, 2.2) * 4) / 4;
    c = mix(c, [30, 40, 50], Math.max(0, 0.66 - s) * 0.75);
    c = mix(c, [255, 250, 230], Math.max(0, s - 0.8) * 0.6);
    return c as [number, number, number];
  });
  river(9, '#5f97b8');
  river(6, '#86bad5');
  roads([
    { width: 8, color: 'rgba(40, 36, 30, 0.75)' },
    { width: 5, color: '#efe3c2' },
  ]);
  ctx.globalAlpha = 0.85;
  hatchMarea('rgba(30, 10, 20, 0.6)', 7, 4);
  ctx.globalAlpha = 1;
  mareaEdge('#f0503c', 3, []);
  // The places, as Synty models seen from above at an angle.
  const art = await pictograms();
  const place = (key: string, x: number, z: number, size: number) => {
    const img = art.get(key);
    if (img) ctx.drawImage(img, px(x) - size / 2, py(z) - size * 0.62, size, size);
  };
  place('atalaya', G.atalaya.x, G.atalaya.z, 130);
  for (const t of G.towns) place(t.landmark === 'church' ? (t.snow ? 'snowTown' : 'town') : 'towerTown', t.x, t.z, 120);
  for (const c of G.cities) place(c.id === 'afr' ? 'city' : 'port', REGIONS[c.id].x + c.dx, REGIONS[c.id].z + c.dz, 230);
  place('dam', G.reservoir.x - 1.6, G.reservoir.z + 2.1, 120);
  place('farm', REGIONS.oce.x - 0.9, REGIONS.oce.z + 0.8, 110);
  place('farm', REGIONS.oce.x + 1.3, REGIONS.oce.z - 0.6, 90);
  place('ruin', REGIONS.na.x + 0.6, REGIONS.na.z - 0.3, 110);
  for (const id of ZONE_IDS) plate(REGIONS[id].name.toUpperCase(), REGIONS[id].x, REGIONS[id].z, fallen(id));
  overlays('#1b1e20', '#eef0ea', true);
  legend(['Carretera', 'Bosque', 'Bajo la Marea', 'Operación'], ['#efe3c2', '#5f8a4a', '#f0503c', '#f2c230'], 'rgba(27,30,32,0.94)', '#eef0ea', 'EL VALLE DE KESSRA');
}

/** A zone's name on a plate of dark steel with the hazard yellow (red if lost). */
function plate(text: string, x: number, z: number, lost: boolean): void {
  ctx.font = '600 20px Oswald';
  (ctx as unknown as { letterSpacing: string }).letterSpacing = '2px';
  const w = ctx.measureText(text).width + 22;
  (ctx as unknown as { letterSpacing: string }).letterSpacing = '0px';
  const cx = px(x);
  const cy = py(z) + 44;
  ctx.fillStyle = 'rgba(27, 30, 32, 0.92)';
  ctx.beginPath();
  ctx.roundRect(cx - w / 2, cy - 14, w, 28, 3);
  ctx.fill();
  ctx.fillStyle = lost ? '#f0503c' : '#f2c230';
  ctx.fillRect(cx - w / 2, cy - 14, 4, 28);
  label(text, x, z, { font: '600 20px Oswald', color: lost ? '#f7b2a8' : '#eef0ea', spacing: 2, dy: 44 });
}

/** What the campaign puts on the map, the same in each style: Atalaya, La Mula and its way, the Nidos, the operations. */
function overlays(plateColor: string, ink: string, steel = false): void {
  const ring = steel ? '#f2c230' : ink;
  // La Mula's way so far and the rest of it.
  const k = MULA.line.indexOf(MULA.at);
  polyline(MULA.line.slice(k), 4, steel ? '#f2c230' : '#1d4fb0', [3, 9]);
  icon('home', G.atalaya.x, G.atalaya.z + (steel ? -1.0 : 0), 34, plateColor, ring);
  if (!steel) label('ATALAYA', G.atalaya.x, G.atalaya.z, { font: '600 16px Oswald', color: ink, halo: plateColor, haloWidth: 5, spacing: 2, dy: -30 });
  icon('r-mula', MULA.at[0], MULA.at[1], 40, plateColor, steel ? '#f2c230' : '#1d4fb0');
  // Beside the zone's marker, clear of its name (above it on the atlas's plates, below it elsewhere).
  const dz = steel ? -0.55 : 0.5;
  for (const id of NIDOS) icon('biohazard', REGIONS[id].x + 0.75, REGIONS[id].z + dz, 34, plateColor, '#f0503c');
  for (const id of OPS) icon('target', REGIONS[id].x + 0.75, REGIONS[id].z + dz, 34, plateColor, '#f2c230');
  // Each zone's marker: a ring at its place.
  for (const id of ZONE_IDS) {
    const r = REGIONS[id];
    ctx.beginPath();
    ctx.arc(px(r.x), py(r.z), 9, 0, Math.PI * 2);
    ctx.fillStyle = fallen(id) ? '#f0503c' : steel ? '#f2c230' : ink;
    ctx.fill();
    ctx.lineWidth = 3;
    ctx.strokeStyle = plateColor;
    ctx.stroke();
  }
}

function legend(names: string[], colors: string[], paper: string, ink: string, title: string): void {
  const x = 24;
  const y = PY - 24 - 40 - names.length * 26;
  ctx.fillStyle = paper;
  ctx.strokeStyle = ink;
  ctx.lineWidth = 1.5;
  ctx.beginPath();
  ctx.roundRect(x, y, 300, 52 + names.length * 26, 4);
  ctx.fill();
  ctx.stroke();
  ctx.font = '600 16px Oswald';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'middle';
  ctx.fillStyle = ink;
  ctx.fillText(title, x + 14, y + 22);
  names.forEach((n, i) => {
    ctx.fillStyle = colors[i]!;
    ctx.fillRect(x + 14, y + 44 + i * 26, 30, 12);
    ctx.fillStyle = ink;
    ctx.font = '500 15px Barlow';
    ctx.fillText(n, x + 54, y + 50 + i * 26);
  });
}

// ------------------------------------------------------------ Synty pictograms

/** The places as small renders of the valley's Synty models, seen from above at an angle, on a clear background. */
async function pictograms(): Promise<Map<string, HTMLCanvasElement>> {
  await loadBaseKit();
  const size = 256;
  const gl = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
  gl.setSize(size, size);
  gl.setClearColor(0x000000, 0);
  gl.toneMapping = THREE.ACESFilmicToneMapping;
  gl.outputColorSpace = THREE.SRGBColorSpace;
  const sets: Record<string, [BaseModelId, number, number, number][]> = {
    town: [
      ['mapChurch', 0, 0, 0.5],
      ['mapHouse', -0.5, 0.3, 0.3],
      ['mapHouseSquare', 0.45, 0.35, 0.28],
      ['mapHouseL', 0.05, 0.6, 0.3],
    ],
    snowTown: [
      ['mapChurch', 0, 0, 0.5],
      ['mapHouse', -0.5, 0.3, 0.3],
      ['mapHouseSquare', 0.45, 0.35, 0.28],
    ],
    towerTown: [
      ['mapTankTower', 0, -0.1, 0.3],
      ['mapHouse', -0.45, 0.3, 0.3],
      ['mapHouseL', 0.4, 0.3, 0.3],
    ],
    city: [
      ['mapTower', 0, 0, 0.45],
      ['mapFlats', -0.5, 0.2, 0.42],
      ['mapBlockM', 0.5, 0.15, 0.4],
      ['mapShopL', -0.1, 0.6, 0.4],
    ],
    port: [
      ['mapFactory', -0.2, 0, 0.6],
      ['mapCrane', 0.5, 0.1, 0.5],
      ['mapLighthouse', 0.75, 0.5, 0.25],
      ['mapFlats', -0.6, 0.45, 0.35],
    ],
    atalaya: [
      ['bunker', 0, 0, 0.55],
      ['mast', -0.45, -0.2, 0.3],
      ['waterTower', 0.45, -0.1, 0.25],
    ],
    dam: [
      ['mapCooling', 0, 0, 0.55],
      ['mapChimney', 0.45, -0.1, 0.3],
    ],
    farm: [
      ['mapBarn', 0, 0, 0.5],
      ['mapSilo', 0.45, -0.1, 0.22],
    ],
    ruin: [
      ['mapRuin', 0, 0, 0.55],
      ['mapTreeDead', 0.5, 0.25, 0.3],
    ],
  };
  const out = new Map<string, HTMLCanvasElement>();
  for (const [key, list] of Object.entries(sets)) {
    const scene = new THREE.Scene();
    scene.add(new THREE.HemisphereLight(0xd8e0f0, 0x6a5e50, 1.6));
    const sun = new THREE.DirectionalLight(0xffe2c0, 2.4);
    sun.position.set(-2, 4, 2);
    scene.add(sun);
    const kit = new KitBatch();
    for (const [id, x, z, fit] of list) if (kitModel(id)) kit.put(id, { x: x * 0.8, z: z * 0.8, fit: fit * 1.5, yaw: 0.4, finish: key === 'snowTown' || key === 'atalaya' ? 'mapSnow' : 'map' });
    const g = kit.build(false);
    scene.add(g);
    const box = new THREE.Box3().setFromObject(g);
    const centre = box.getCenter(new THREE.Vector3());
    const r = box.getSize(new THREE.Vector3()).length() * 0.55;
    const cam = new THREE.OrthographicCamera(-r, r, r, -r, 0.01, 50);
    cam.position.copy(centre).add(new THREE.Vector3(0, 1.1, 1.4).normalize().multiplyScalar(10));
    cam.lookAt(centre);
    gl.render(scene, cam);
    const c = document.createElement('canvas');
    c.width = c.height = size;
    c.getContext('2d')!.drawImage(gl.domElement, 0, 0);
    out.set(key, c);
  }
  gl.dispose();
  return out;
}

// --------------------------------------------------------------------- draw

async function snap(name = `mapa_${style}`): Promise<string> {
  await fetch(`/__snap?name=${name}`, { method: 'POST', body: canvas.toDataURL('image/jpeg', 0.92) });
  return name;
}

async function draw(): Promise<void> {
  await Promise.all([document.fonts.load('600 20px Oswald'), document.fonts.load('500 20px Oswald'), document.fonts.load('500 15px Barlow'), document.fonts.load('700 15px Barlow'), loadIcons()]);
  if (style === 'topo') topoMap();
  else if (style === 'atlas') await atlasMap();
  else roadMap();
  document.title = `Bastión · Mapa (${style})`;
}

const ready = draw();
(window as unknown as { __mapa: unknown }).__mapa = { ready, snap };
