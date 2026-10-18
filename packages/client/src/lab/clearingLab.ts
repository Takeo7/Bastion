import * as THREE from 'three';
import { fellTree, freshBase, placePiece, Rng, type BaseState, type PieceId } from '@bastion/engine';
import { ClearingArtView } from '../strategy/clearingArt';
import { Stage, type StageSet } from '../strategy/stage';
import '../styles.css';

// Clearing lab (/claro.html): the base's clearing as its screen draws it
// (strategy/clearingArt.ts), with no building tools: a sample of pieces round
// the caravan, and a piece alone (pieceModel, as the one about to be built)
// beside them (`?ghost`). `?seed=n` another clearing; `?bare` just the
// caravan; `?snow` the winter's light. `__claro.look(x, z, distance, yaw)`
// frames a spot; `__claro.fell(x0, y0, x1, y1)` fells a rectangle (redrawn
// by chunks); `__claro.depart()` and `__claro.arrive()` send La Lata off
// down its track and bring it back.

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
  for (let y = c - 4; y <= c + 1; y++) put(y === c - 2 ? 'gate' : 'palisade', c - 10, y);
  put('sandbags', c - 6, c - 3);
  put('sandbags', c - 5, c - 3);
  put('logs', c + 4, c - 3);
  put('tower', c + 7, c - 3);
  put('floodlight', c - 7, c);
  put('turret', c + 7, c + 1);
  put('snare', c - 5, c + 2);
}
art.setBase(base);
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
  look,
  fell(x0: number, y0: number, x1: number, y1: number) {
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) fellTree(base, x, y);
    art.setBase(base);
  },
  depart: () => art.depart(),
  arrive: () => art.arrive(),
};
