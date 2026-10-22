import * as THREE from 'three';
import type { PieceId } from '@bastion/engine';
import { pieceLook } from './clearingArt';

// Pictures of the base's pieces for Construir's grid (Ana, 21/10 #35: «menos
// texto y más iconos»): each piece as the clearing draws it, in its own
// colours, seen from above at three quarters on a clear background, framed
// tight. The buildings are Arte's renders; the rest are made once a session,
// all together, in a small renderer of their own that is let go after
// (lab/renders/iconRenders.ts frames the same way).

/** The pictures' side, in pixels (twice what the grid shows, for sharp screens). */
const SIZE = 168;
/** Where the camera looks from: the front left, from above. */
const VIEW = new THREE.Vector3(-0.85, 1.05, 1).normalize();
const FOV = 22;

/**
 * The buildings come drawn already (Arte's renders, public/ui/synty/renders/piece-<id>): their walls are the map's
 * tiles, which a piece alone hasn't got. The rest (defences, traps) are made here.
 */
const DRAWN: ReadonlySet<PieceId> = new Set<PieceId>(['radio', 'quarters', 'infirmary', 'workshop', 'garden', 'quarantine', 'memorial', 'range', 'training', 'simulation', 'garage', 'echoes']);

const urls = new Map<PieceId, string>();
let running = false;

/** The picture of `piece`, if made. */
export function pieceIcon(piece: PieceId): string | undefined {
  return DRAWN.has(piece) ? `${import.meta.env.BASE_URL}ui/synty/renders/piece-${piece}@128.webp` : urls.get(piece);
}

/** Makes the pictures of `pieces` not made yet, then calls `done` (to draw the palette again). */
export function makePieceIcons(pieces: readonly PieceId[], done: () => void): void {
  const todo = pieces.filter((id) => !DRAWN.has(id) && !urls.has(id));
  if (!todo.length || running) return;
  running = true;
  void shootAll(todo)
    .catch((e: unknown) => console.warn('[construir] piece pictures', e))
    .finally(() => {
      running = false;
      done();
    });
}

async function shootAll(pieces: PieceId[]): Promise<void> {
  const looks = await Promise.all(pieces.map((id) => pieceLook(id)));
  await Promise.all(looks.map(painted));
  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
  try {
    renderer.setPixelRatio(1);
    renderer.setSize(SIZE, SIZE, false);
    renderer.toneMapping = THREE.NeutralToneMapping;
    renderer.setClearColor(0x000000, 0);
    pieces.forEach((id, i) => urls.set(id, shoot(renderer, looks[i]!)));
  } finally {
    renderer.dispose();
    renderer.forceContextLoss();
  }
}

/** A soft sky, a warm key from the front left and a cool rim behind: each piece reads on the dark steel. */
function studio(subject: THREE.Object3D): THREE.Scene {
  const scene = new THREE.Scene();
  const key = new THREE.DirectionalLight(0xfff0dc, 3.2);
  key.position.set(-1.2, 2, 1.4);
  const rim = new THREE.DirectionalLight(0xb4ccff, 1.2);
  rim.position.set(1.5, 1, -1.2);
  scene.add(subject, new THREE.HemisphereLight(0xe2eaff, 0x4a4036, 2.1), key, rim);
  return scene;
}

/** `subject` framed tight in the camera's own axes, as a WebP data URL. */
function shoot(renderer: THREE.WebGLRenderer, subject: THREE.Object3D): string {
  const scene = studio(subject);
  subject.updateMatrixWorld(true);
  const box = new THREE.Box3().setFromObject(subject);
  if (box.isEmpty()) return '';
  const right = new THREE.Vector3(0, 1, 0).cross(VIEW).normalize();
  const up = VIEW.clone().cross(right);
  const centre = box.getCenter(new THREE.Vector3());
  let [x0, x1, y0, y1, depth] = [Infinity, -Infinity, Infinity, -Infinity, 0];
  for (let i = 0; i < 8; i++) {
    const d = new THREE.Vector3(i & 1 ? box.max.x : box.min.x, i & 2 ? box.max.y : box.min.y, i & 4 ? box.max.z : box.min.z).sub(centre);
    x0 = Math.min(x0, d.dot(right));
    x1 = Math.max(x1, d.dot(right));
    y0 = Math.min(y0, d.dot(up));
    y1 = Math.max(y1, d.dot(up));
    depth = Math.max(depth, Math.abs(d.dot(VIEW)));
  }
  const target = centre.clone().addScaledVector(right, (x0 + x1) / 2).addScaledVector(up, (y0 + y1) / 2);
  const half = Math.max(x1 - x0, y1 - y0) / 2 / 0.94;
  const camera = new THREE.PerspectiveCamera(FOV, 1, 0.01, 400);
  camera.position.copy(target).addScaledVector(VIEW, half / Math.tan(THREE.MathUtils.degToRad(FOV / 2)) + depth);
  camera.up.copy(up);
  camera.lookAt(target);
  renderer.render(scene, camera);
  return renderer.domElement.toDataURL('image/webp', 0.9);
}

/** Resolves once every texture the subject wears has its image (or after five seconds). */
async function painted(subject: THREE.Object3D): Promise<void> {
  for (let i = 0; i < 100; i++) {
    let ok = true;
    subject.traverse((o) => {
      const map = o instanceof THREE.Mesh ? (o.material as THREE.MeshStandardMaterial).map : null;
      if (map && map.image == null) ok = false;
    });
    if (ok) return;
    await new Promise((r) => setTimeout(r, 50));
  }
}
