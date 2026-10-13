import * as THREE from 'three';
import { atlasFile, type AtlasId } from './syntyManifest';

// The map's atlases packed into one texture (a draw per material: with a
// material per pack, every pack the map uses cost a draw in each pass; with
// Construction, Military and Office that was nine more a frame on «alto»).
// Town and Apocalypse keep their 2048 texels in the top half; the 1024 ones
// (Nature's 512 drawn up) share the bottom half. Every UV of the map's models
// is inside its own atlas, so a model only needs its UVs moved into its
// atlas's place (batch.ts, when merging); the walls' dye reads the UV back in
// its own atlas (buildings.ts). Characters keep their packs' own textures.

const SIZE = 4096;

/** Where each packed atlas sits, as UV (x, y, width, height); flipY off, as glTF: y runs down the image. */
const RECTS: Partial<Record<AtlasId, readonly [number, number, number, number]>> = {
  town: [0, 0, 0.5, 0.5],
  apocalypse: [0.5, 0, 0.5, 0.5],
  city: [0, 0.5, 0.25, 0.25],
  farm: [0.25, 0.5, 0.25, 0.25],
  office: [0.5, 0.5, 0.25, 0.25],
  construction: [0.75, 0.5, 0.25, 0.25],
  military: [0, 0.75, 0.25, 0.25],
  nature: [0.25, 0.75, 0.25, 0.25],
};

/** The packed atlases, in index order (aAtlas). */
export const PACKED = Object.keys(RECTS) as AtlasId[];

/** Where atlas `id` sits in the packed texture; null if it isn't packed (it keeps its own). */
export function atlasRect(id: AtlasId): readonly [number, number, number, number] | null {
  return RECTS[id] ?? null;
}

/** Atlas `id`'s index among the packed ones (aAtlas), or −1. */
export function atlasIndex(id: AtlasId): number {
  return PACKED.indexOf(id);
}

/** The packed rects as shader uniforms (by aAtlas). */
export const ATLAS_RECTS = { value: PACKED.map((id) => new THREE.Vector4(...RECTS[id]!)) };

let packed: THREE.Texture | null = null;

/** The packed texture: drawn once every atlas has loaded (until then, blank). */
export function mapAtlasTexture(): THREE.Texture {
  if (packed) return packed;
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = SIZE;
  const ctx = canvas.getContext('2d')!;
  const texture: THREE.Texture = new THREE.CanvasTexture(canvas);
  texture.flipY = false;
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.anisotropy = 4;
  texture.wrapS = texture.wrapT = THREE.ClampToEdgeWrapping;
  packed = texture;
  const loader = new THREE.ImageLoader();
  let pending = PACKED.length;
  for (const id of PACKED) {
    const [x, y, w, h] = RECTS[id]!;
    loader.load(
      `${import.meta.env.BASE_URL}${atlasFile(id)}`,
      (image) => {
        ctx.drawImage(image, x * SIZE, y * SIZE, w * SIZE, h * SIZE);
        if (--pending === 0) {
          texture.needsUpdate = true;
          // Uploaded: the canvas's own pixels are no longer needed in memory.
          void createImageBitmap(canvas).then((bitmap) => {
            texture.image = bitmap;
            texture.needsUpdate = true;
            canvas.width = canvas.height = 1;
          });
        }
      },
      undefined,
      () => {
        if (--pending === 0) texture.needsUpdate = true;
      },
    );
  }
  return texture;
}
