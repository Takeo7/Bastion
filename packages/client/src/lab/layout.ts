import { PLAYER_COLORS, type TemplateId } from '@bastion/engine';

// The diorama every style renders: a street corner with a squad behind cover
// on the left and an alien pod on the right. Tiles are 1 world unit; x grows
// to the right of the default camera and z towards it.

export const WIDTH = 18;
export const DEPTH = 12;
/** Rows [0, SIDEWALK_TO) are the building and its sidewalk. */
export const SIDEWALK_TO = 3;
export const LANE_Z = 7.5;
export const CROSSING: [number, number] = [8, 10];

export type PropKind =
  | 'wall'
  | 'window'
  | 'door'
  | 'car'
  | 'sandbags'
  | 'crates'
  | 'barrier'
  | 'planter'
  | 'lamp'
  | 'relay'
  | 'alienBarricade'
  | 'kiosk'
  | 'tree'
  | 'rubble'
  | 'hydrant';

/** Cover the prop gives in the rules; the current placeholder look draws only this. */
export type CoverClass = 'wall' | 'high' | 'low' | 'none';

export const COVER: Record<PropKind, CoverClass> = {
  wall: 'wall',
  window: 'wall',
  door: 'wall',
  car: 'high',
  sandbags: 'low',
  crates: 'low',
  barrier: 'low',
  planter: 'low',
  lamp: 'none',
  relay: 'high',
  alienBarricade: 'low',
  kiosk: 'high',
  tree: 'high',
  rubble: 'none',
  hydrant: 'none',
};

export interface PropDef {
  kind: PropKind;
  x: number;
  z: number;
  /** Footprint in tiles along x and z (default 1×1). */
  w?: number;
  d?: number;
  /** Rotation in quarter turns. */
  rot?: number;
  seed?: number;
}

export interface ActorDef {
  id: string;
  template: TemplateId;
  color: string;
  x: number;
  z: number;
  team: 'xcom' | 'alien';
  /** Facing in radians (default: towards the other side of the street). */
  facing?: number;
}

const P1 = PLAYER_COLORS[0];
const P2 = PLAYER_COLORS[1];
export const ALIEN_COLOR = '#ff4d4d';

function buildingRow(): PropDef[] {
  const row: PropDef[] = [];
  for (let x = 0; x < WIDTH; x++) {
    const kind: PropKind = x === 4 || x === 13 ? 'window' : x === 9 ? 'door' : 'wall';
    row.push({ kind, x, z: 0, seed: x });
  }
  return row;
}

export const PROPS: PropDef[] = [
  ...buildingRow(),
  { kind: 'lamp', x: 2, z: 2 },
  { kind: 'lamp', x: 15, z: 2 },
  { kind: 'planter', x: 7, z: 1 },
  { kind: 'hydrant', x: 11, z: 2 },
  // XCOM side.
  { kind: 'car', x: 5, z: 4, d: 2, seed: 1 },
  { kind: 'sandbags', x: 5, z: 8, d: 2 },
  { kind: 'crates', x: 3, z: 10, seed: 2 },
  { kind: 'rubble', x: 7, z: 6, seed: 3 },
  // No man's land.
  { kind: 'barrier', x: 9, z: 8, d: 2 },
  { kind: 'rubble', x: 10, z: 4, seed: 5 },
  // Alien side.
  { kind: 'relay', x: 14, z: 4 },
  { kind: 'alienBarricade', x: 12, z: 5, d: 2 },
  { kind: 'kiosk', x: 12, z: 9 },
  { kind: 'tree', x: 16, z: 8 },
  { kind: 'crates', x: 15, z: 11, seed: 9 },
];

export const ACTORS: ActorDef[] = [
  { id: 'sharp', template: 'sharpshooter', color: P1, x: 4, z: 6, team: 'xcom' },
  { id: 'assault', template: 'assault', color: P2, x: 4, z: 8, team: 'xcom' },
  { id: 'grenadier', template: 'grenadier', color: P1, x: 2, z: 10, team: 'xcom' },
  { id: 'spec', template: 'specialist', color: P2, x: 4, z: 4, team: 'xcom' },
  { id: 'trooper', template: 'trooper', color: ALIEN_COLOR, x: 13, z: 5, team: 'alien' },
  { id: 'officer', template: 'officer', color: ALIEN_COLOR, x: 13, z: 6, team: 'alien' },
  { id: 'xenoid', template: 'xenoid', color: ALIEN_COLOR, x: 13, z: 8, team: 'alien' },
  { id: 'lancer', template: 'lancer', color: ALIEN_COLOR, x: 14, z: 10, team: 'alien' },
  { id: 'mec', template: 'mec', color: ALIEN_COLOR, x: 15, z: 5, team: 'alien' },
];

/** Tiles blocked for the movement preview. */
export function blockedTiles(): Set<string> {
  const set = new Set<string>();
  for (const p of PROPS) {
    if (COVER[p.kind] === 'none') continue;
    for (let dx = 0; dx < (p.w ?? 1); dx++) for (let dz = 0; dz < (p.d ?? 1); dz++) set.add(`${p.x + dx},${p.z + dz}`);
  }
  for (const a of ACTORS) set.add(`${a.x},${a.z}`);
  return set;
}

/**
 * Every unit model in a row well to the side of the street, out of every
 * camera preset except "Desfile".
 */
export const PARADE_X = 34;

const PARADE_ORDER: [TemplateId, 'xcom' | 'alien'][] = [
  ['assault', 'xcom'],
  ['grenadier', 'xcom'],
  ['sharpshooter', 'xcom'],
  ['specialist', 'xcom'],
  ['vip', 'xcom'],
  ['trooper', 'alien'],
  ['officer', 'alien'],
  ['lancer', 'alien'],
  ['xenoid', 'alien'],
  ['sectoid', 'alien'],
  ['zombie', 'alien'],
  ['mec', 'alien'],
  ['relay', 'alien'],
];

export const PARADE: ActorDef[] = PARADE_ORDER.map(([template, team], i) => ({
  id: `parade-${template}`,
  template,
  color: team === 'xcom' ? (i % 2 ? P2 : P1) : ALIEN_COLOR,
  x: PARADE_X + i * 1.35,
  z: 6,
  team,
  facing: 0,
}));
