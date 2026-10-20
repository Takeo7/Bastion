import * as THREE from 'three';
import { extendShader } from './fogOfWar';

// A tree's crown, drawn at its size (19/10, MAPAS.md §4.7: 4–6 m across and
// 6–9 m high), fades away where it would hide what the player looks at, as a
// building's roof does: over the selected soldier, the target, the tile under
// the cursor, or between any of them and the camera. Its trunk stays. In the
// map's merged batch: each tree's vertices carry where it stands (aTree), the
// shader the points looked at (TREE_FADE, set every frame by mapView).

/** Up to three points looked at (x, z, and 1 if in use), the way to the camera on the ground, how far a crown reaches over it. */
export const TREE_FADE = {
  uTreeFocus: { value: [new THREE.Vector3(), new THREE.Vector3(), new THREE.Vector3()] },
  uTreeToCam: { value: new THREE.Vector2(0, 1) },
  uTreeReach: { value: 5 },
};

/** How high a trunk is, in tiles, above which a tree fades: over a soldier's head. */
export const TRUNK = 1.5;

/** The crown's fade on `material`: its geometry carries aTree (x, z, the trunk's top). */
export function treeFadePatch(material: THREE.Material): void {
  extendShader(material, 'tree-fade', (shader) => {
    Object.assign(shader.uniforms, TREE_FADE);
    shader.vertexShader = shader.vertexShader
      .replace(
        '#include <common>',
        `#include <common>
        attribute vec3 aTree;
        uniform vec3 uTreeFocus[3];
        uniform vec2 uTreeToCam;
        uniform float uTreeReach;
        varying float vTreeFade;
        varying float vTreeHigh;`,
      )
      .replace(
        '#include <project_vertex>',
        `#include <project_vertex>
        {
          float fade = 0.0;
          for (int k = 0; k < 3; k++) {
            vec3 f = uTreeFocus[k];
            if (f.z < 0.5) continue;
            // From the point looked at towards the camera, as far as a crown hides it.
            vec2 ab = uTreeToCam * uTreeReach;
            float t = clamp(dot(aTree.xy - f.xy, ab) / max(dot(ab, ab), 1e-4), 0.0, 1.0);
            float d = length(aTree.xy - (f.xy + ab * t));
            fade = max(fade, 1.0 - smoothstep(1.4, 2.8, d));
          }
          vTreeFade = fade * 0.88;
          vTreeHigh = (modelMatrix * vec4(transformed, 1.0)).y - aTree.z;
        }`,
      );
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', '#include <common>\nvarying float vTreeFade;\nvarying float vTreeHigh;')
      .replace(
        '#include <clipping_planes_fragment>',
        `#include <clipping_planes_fragment>
        if (vTreeHigh > 0.0 && fract(52.9829189 * fract(dot(gl_FragCoord.xy, vec2(0.06711056, 0.00583715)))) < vTreeFade * smoothstep(0.0, 0.6, vTreeHigh)) discard;`,
      );
  });
}

/** The points looked at this frame (ground positions; null, none) and the camera: TREE_FADE's uniforms. */
export function aimTreeFade(points: readonly (THREE.Vector3 | null)[], camera: THREE.Camera | null): void {
  const f = TREE_FADE.uTreeFocus.value;
  for (let k = 0; k < 3; k++) {
    const p = points[k];
    if (p) f[k]!.set(p.x, p.z, 1);
    else f[k]!.set(0, 0, 0);
  }
  const first = points.find((p) => p);
  if (!camera || !first) return;
  const dx = camera.position.x - first.x;
  const dz = camera.position.z - first.z;
  const flat = Math.hypot(dx, dz);
  if (flat < 1e-3) return;
  TREE_FADE.uTreeToCam.value.set(dx / flat, dz / flat);
  // A crown some 6 tiles up hides the ground this far towards the camera.
  const tan = (camera.position.y - first.y) / flat;
  TREE_FADE.uTreeReach.value = Math.min(8, 6 / Math.max(tan, 0.4));
}
