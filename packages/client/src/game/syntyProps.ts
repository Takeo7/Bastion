import * as THREE from 'three';
import { STOREY } from './coords';
import { atlasOf, loadModel } from './synty';
import { PROP_SETS, PROPS, SYNTY_SCALE, type AtlasId, type PropId, type PropSet } from './syntyManifest';

// Synty props for the map: each prop's meshes, normalised to a tile as the
// manifest says (fit, yaw, shift) and standing on y = 0 (or sunk), ready to instance.

/** One mesh of a prop: its geometry, its atlas and where it sits in the prop. */
export interface PropPart {
  geometry: THREE.BufferGeometry;
  atlas: AtlasId;
  matrix: THREE.Matrix4;
}

const parts = new Map<PropId, PropPart[]>();
const pending = new Map<PropId, Promise<void>>();

function prepare(id: PropId, model: THREE.Group): void {
  const a = PROPS[id] as { fit?: number; stretch?: boolean; thick?: number; yaw?: number; shift?: [number, number]; origin?: boolean; deck?: number; sink?: number };
  model.updateMatrixWorld(true);
  const box = new THREE.Box3().setFromObject(model);
  const size = box.getSize(new THREE.Vector3());
  // On its own origin (a pole with an arm) or its middle; on the floor either way.
  const centre = a.origin ? new THREE.Vector3() : box.getCenter(new THREE.Vector3());
  // The longest side to `fit` tiles; with `stretch` only that side; height and depth times `thick`.
  const along = a.fit ? a.fit / Math.max(size.x, size.z) : 1;
  const across = (a.stretch ? 1 : along) * (a.thick ?? 1);
  const scale = size.x >= size.z ? new THREE.Vector3(along, across, across) : new THREE.Vector3(across, across, along);
  // A deck one stands on, one storey up whatever its width took (a guard tower's on its legs).
  if (a.deck) scale.y = STOREY / (a.deck * SYNTY_SCALE);
  // Centred on the tile and on the floor (or sunk into it), turned to face north, then shifted along its own axes.
  const fit = new THREE.Matrix4()
    .makeTranslation(a.shift?.[0] ?? 0, -(a.sink ?? 0), a.shift?.[1] ?? 0)
    .multiply(new THREE.Matrix4().makeRotationY(((a.yaw ?? 0) * Math.PI) / 2))
    .multiply(new THREE.Matrix4().makeScale(scale.x, scale.y, scale.z))
    .multiply(new THREE.Matrix4().makeTranslation(-centre.x, -box.min.y, -centre.z));
  const list: PropPart[] = [];
  model.traverse((o) => {
    if (o instanceof THREE.Mesh) list.push({ geometry: o.geometry, atlas: atlasOf(o), matrix: fit.clone().multiply(o.matrixWorld) });
  });
  parts.set(id, list);
}

/** The parts of a prop, or null while it loads (asking for it starts the load). */
export function syntyProp(id: PropId): PropPart[] | null {
  const ready = parts.get(id);
  if (ready) return ready;
  if (!pending.has(id)) {
    const a = PROPS[id];
    pending.set(
      id,
      loadModel(a.pack, a.base ? 'base' : 'prop', a.base ?? id)
        .then((m) => prepare(id, m))
        // One that won't load (not converted yet) is drawn as nothing: waited for, the map rebuilt for it without end.
        .catch((e: unknown) => {
          console.warn(`[synty] prop ${id} did not load`, e);
          parts.set(id, []);
        }),
    );
  }
  return null;
}

/** Resolves when every prop asked for so far has loaded (or failed). */
export function syntyPropsLoaded(): Promise<void> {
  return Promise.all(pending.values()).then(() => undefined);
}

/** The Synty props a prop set draws (the map picks one per tile). */
export function syntySet(set: PropSet): readonly PropId[] {
  return PROP_SETS[set];
}

/** Starts loading every prop the map may use. */
export function preloadSyntyProps(): void {
  for (const id of Object.keys(PROPS) as PropId[]) syntyProp(id);
}
