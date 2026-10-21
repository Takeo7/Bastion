import * as THREE from 'three';
import { BASE_MAP, BUILD_IDS, defaultAppearance, fellTree, freshBase, growBase, HEAD_IDS, placePiece, Rng, type Appearance, type BaseState, type BuildId, type HeadId, type PieceId } from '@bastion/engine';
import type { ArmoryStock } from '../strategy/clearing';
import { OUTFIT_CHOICES, setRoster } from '../game/wardrobe';
import { ClearingArtView } from '../strategy/clearingArt';
import { Stage, type StageSet } from '../strategy/stage';
import '../styles.css';

// Clearing lab (/claro.html): the base's clearing as its screen draws it
// (strategy/clearingArt.ts), with no building tools: a sample of pieces round
// the caravan, and a piece alone (pieceModel, as the one about to be built)
// beside them (`?ghost`). `?seed=n` another clearing; `?bare` just the
// caravan; `?snow` the winter's light; `?cerco` pieces in the way of
// the Soldados' and the Armería's shots. `__claro.look(x, z, distance, yaw)`
// frames a spot; `__claro.fell(x0, y0, x1, y1)` fells a rectangle (redrawn
// by chunks); `__claro.depart()` and `__claro.arrive()` send La Lata off
// down its track and bring it back; `__claro.soldados()` and
// `__claro.armeria(stock)`, the Soldados' and the Armería's shots (with what the Armería holds);
// `__claro.escuadra(n, informe, espejo, solo, libres)`, a squad of n by La Lata as its screen
// frames it (the debrief's: some wounded, one fallen; mirrored, as the second
// player sees it); `__claro.puestos()`, soldiers at their siege posts round
// the clearing (#58); and `__claro.subir()` boards it
// and sends La Lata off; `__claro.lata(v)` makes La Lata the campaign's
// vehicle v (0 4×4, 1 van, 2 lorry, 3 the bus); `?lata=v` from the start.
// `?nivel=n` the Bastion at level n (1 to 8: its stage, base.ts bastionStage),
// `__claro.nivel(n, desgaste)` another one, worn 0 to 3.

const params = new URLSearchParams(location.search);
const stage = new Stage();
stage.element.style.cssText = 'position: fixed; inset: 0';
document.body.append(stage.element);
const art = new ClearingArtView(params.has('snow'));
const scene = new THREE.Scene();
art.dress(scene);
scene.add(art.group);
const set: StageSet = {
  scene,
  ambientOcclusion: true,
  mood: art.mood,
  tick: (dt, time) => art.tick(dt),
  hits: () => [],
  beforeRender: (gl) => art.beforeRender(gl),
};
stage.add('claro', set);
art.watch(stage.camera);

const base: BaseState = freshBase(new Rng(Number(params.get('seed') ?? 3)));
if (params.has('nivel')) base.level = Number(params.get('nivel'));
const c = Math.floor(base.width / 2);
if (!params.has('bare')) {
  for (let y = c - 9; y <= c + 9; y++) for (let x = c - 11; x <= c + 11; x++) fellTree(base, x, y);
  const put = (id: PieceId, x: number, y: number, rot = 0, level = 1) => {
    const p = placePiece(base, id, x, y, rot);
    if (p.level) p.level = level;
  };
  put('radio', c - 9, c - 8, 0, 2);
  put('quarters', c - 5, c - 8, 0, 3);
  put('workshop', c + 4, c - 8, 1, 1);
  put('garden', c - 9, c + 3, 0, 2);
  put('memorial', c + 1, c + 4);
  // `?puertas`: a gate three wide (19/10: none hung on the posts between them).
  for (let y = c - 4; y <= c + 1; y++) put(y === c - 2 || (params.has('puertas') && (y === c - 3 || y === c - 1)) ? 'gate' : 'palisade', c - 10, y);
  put('sandbags', c - 6, c - 3);
  put('sandbags', c - 5, c - 3);
  put('logs', c + 4, c - 3);
  put('tower', c + 7, c - 3);
  put('floodlight', c - 7, c);
  put('turret', c + 7, c + 1);
  put('snare', c - 5, c + 2);
  // `?cerco`: a palisade across the barracks' front and a tower by the caravan's end, in the way of both shots.
  if (params.has('cerco')) {
    for (let x = c - 7; x <= c; x++) put('palisade', x, c - 2);
    put('tower', c + 4, c + 1);
  }
}
art.setBase(base);
if (params.has('lata')) art.setVehicle(Number(params.get('lata')));
// A piece alone, as the one about to be built, by the caravan.
if (params.has('ghost')) {
  const ghost = art.pieceModel('infirmary', 1, 2);
  ghost.position.set(c + 3.5, 0, c + 2.5);
  scene.add(ghost);
}

/** The camera on (x, z) from `distance` tiles, its bearing `yaw` (radians, 0 from the south-east as the battle's default), 50° down. */
const look = (x: number, z: number, distance = 30, yaw = 0) => {
  const target = new THREE.Vector3(x, 0, z);
  const a = Math.PI / 4 + yaw;
  const down = (50 * Math.PI) / 180;
  const dir = new THREE.Vector3(Math.sin(a) * Math.cos(down), Math.sin(down), Math.cos(a) * Math.cos(down));
  void stage.show('claro', { pos: target.clone().addScaledVector(dir, distance), target }, true);
};
look(c, c, 34);
(window as unknown as { __claro: unknown }).__claro = {
  /** The clearing's art itself (to look into it), and its stage (a shot of one's own: show(pos, target, fov)). */
  art,
  show: (pos: [number, number, number], target: [number, number, number], fov = 38) => void stage.show('claro', { pos: new THREE.Vector3(...pos), target: new THREE.Vector3(...target), fov }, true),
  look,
  fell(x0: number, y0: number, x1: number, y1: number) {
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) fellTree(base, x, y);
    art.setBase(base);
  },
  depart: () => art.depart(),
  arrive: () => art.arrive(),
  /** A campaign's base instead (its `bastion`, as a save has it: grown to the map of today if it is smaller). */
  base(saved: BaseState) {
    const b = structuredClone(saved);
    if (b.width < BASE_MAP) growBase(b, BASE_MAP, new Rng(1));
    art.setBase(b);
  },
  /** Soldados and the Armería as their screens frame them, a soldier by the caravan. */
  soldados() {
    art.setShowcase('assault', '#e8a33a', undefined, undefined, 'lab');
    void stage.show('claro', art.soldierShot(), true);
  },
  /** `stock`: what the Armería holds (its corner shows it: 20/10 #29); none, as it stands. */
  armeria(stock?: ArmoryStock) {
    if (stock) art.setArmory(stock);
    void stage.show('claro', art.armoryShot(), true);
  },
  /**
   * The squad's screen: `n` soldiers by La Lata, the two commanders' halves (`alone`: one commander's, in one row, the
   * last `empty` places free); the debrief's, with its wounded and its fallen.
   */
  escuadra(n = 6, debrief = false, mirror = false, alone = false, empty = 0, looks: 'none' | 'default' | 'mixed' = 'none') {
    const templates = ['assault', 'sharpshooter', 'specialist', 'grenadier', 'rookie'] as const;
    // The soldiers' looks: none (older saves), their player's as they join (by sex), or each a head and a build of their own.
    const look = (i: number, color: string): Appearance | undefined => {
      if (looks === 'none') return undefined;
      const a = defaultAppearance(i < Math.ceil(n / 2) ? 0 : 1, color, i % 2 ? 'm' : 'f');
      return looks === 'mixed' ? { ...a, head: HEAD_IDS[i % HEAD_IDS.length]!, build: BUILD_IDS[(i * 2) % BUILD_IDS.length]! } : a;
    };
    // Dressed as the campaign dresses its soldiers (wardrobe.ts setRoster), in the order they joined.
    setRoster(Array.from({ length: n }, (_, i) => ({ id: `lab${i}`, appearance: look(i, '#e8a33a') })));
    art.setLineup([
      ...Array.from({ length: n }, (_, i) => ({
        template: templates[i % templates.length]!,
        color: alone || i < Math.ceil(n / 2) ? '#e8a33a' : '#4fb0e8',
        appearance: look(i, alone || i < Math.ceil(n / 2) ? '#e8a33a' : '#4fb0e8'),
        seed: `lab${i}`,
        ...(debrief ? { status: i === 1 ? ('dead' as const) : i % 3 === 2 ? ('wounded' as const) : ('ok' as const) } : {}),
      })),
      ...Array.from({ length: empty }, () => null),
    ], { mirror, halves: !alone });
    void stage.show('claro', art.lineupShot(), true);
  },
  /**
   * The wardrobe's outfits in a row (wardrobe.ts OUTFIT_CHOICES, from `from`), each on a soldier of `sex` ('f', 'm',
   * or by turns: '') with `head` and `build`, in one player's colour.
   */
  armario(from = 0, n = 8, sex: '' | 'f' | 'm' = '', head: HeadId = 'bare', build: BuildId = 'normal') {
    const outfits = OUTFIT_CHOICES.slice(from, from + n);
    art.setLineup(
      outfits.map((o, i) => ({
        template: 'rookie' as const,
        color: '#e8a33a',
        appearance: { ...defaultAppearance(0, '#e8a33a', sex || (i % 2 ? 'm' : 'f')), head, build, character: o.id },
        seed: `armario${from + i}`,
      })),
      { halves: false },
    );
    void stage.show('claro', art.lineupShot(), true);
    return outfits.map((o) => o.id).join(' ');
  },
  subir: () => art.depart(),
  /** A siege's posts: six soldiers about the clearing, both commanders', one on the tower. */
  puestos() {
    const spots = [[c - 6, c - 3], [c - 5, c - 3], [c + 7, c - 3], [c + 3, c + 3], [c - 3, c + 4], [c + 6, c + 1]];
    const templates = ['assault', 'sharpshooter', 'specialist', 'grenadier', 'rookie', 'assault'] as const;
    art.setPosts(spots.map(([x, y], i) => ({ entry: { template: templates[i]!, color: i % 2 ? '#4fb0e8' : '#e8a33a', seed: `post${i}` }, x: x!, y: y! })));
    look(c, c, 22, 0);
  },
  lata: (v: number) => art.setVehicle(v),
  nivel(n: number, wear = 0) {
    base.level = n;
    const caravan = base.pieces.find((p) => p.piece === 'caravan');
    if (caravan) caravan.hp = [40, 26, 12, 0][wear] ?? 40;
    art.setBase(base);
  },
};
