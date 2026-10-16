// Reuses already converted licensed Synty characters and death clips. No FBX
// conversion, source pack scan or deletion. Run from the repository root.
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { resolve, dirname } from 'node:path';
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';
import { MeshoptDecoder } from 'three/addons/libs/meshopt_decoder.module.js';

const root = resolve('packages/client/public/models/synty');
const output = resolve(process.argv[2] ?? root);
const loader = new GLTFLoader().setMeshoptDecoder(MeshoptDecoder);
globalThis.FileReader ??= class {
  readAsArrayBuffer(blob) { void blob.arrayBuffer().then((b) => { this.result = b; this.onloadend?.(); }); }
};
async function load(path) {
  const b = await readFile(resolve(root, path));
  return loader.parseAsync(b.buffer.slice(b.byteOffset, b.byteOffset + b.byteLength), '');
}
for (const [id, character, death] of [
  ['corpseCivilian', 'business', 'deathHeadFMasc'],
  ['corpseStaff', 'waitress', 'deathStomachFemn'],
]) {
  const [body, pose, fall] = await Promise.all([
    load(`apocalypse/chr-${character}.glb`), load('baseLocomotion/anim-tpose.glb'), load(`bowCombat/anim-${death}.glb`),
  ]);
  const proxy = pose.scene;
  const mixer = new THREE.AnimationMixer(proxy);
  mixer.clipAction(pose.animations[0]).play(); mixer.update(0); proxy.updateMatrixWorld(true);
  const inv = new Map();
  proxy.traverse((b) => inv.set(b.name, b.getWorldQuaternion(new THREE.Quaternion()).invert()));
  const hips = proxy.getObjectByName('Hips'), hipRest = hips.getWorldPosition(new THREE.Vector3());
  mixer.stopAllAction();
  mixer.clipAction(fall.animations[0]).setLoop(THREE.LoopOnce, 1).play();
  mixer.setTime(fall.animations[0].duration - 0.001); proxy.updateMatrixWorld(true);
  body.scene.updateMatrixWorld(true);
  const rest = new Map(), posed = new Map(), delta = new Map();
  body.scene.traverse((b) => { if (b.isBone) rest.set(b.name, {
    p: b.getWorldPosition(new THREE.Vector3()), q: b.getWorldQuaternion(new THREE.Quaternion()),
  }); });
  for (const name of rest.keys()) {
    const source = proxy.getObjectByName(name);
    delta.set(name, source.getWorldQuaternion(new THREE.Quaternion()).multiply(inv.get(name)));
  }
  const shift = hips.getWorldPosition(new THREE.Vector3()).sub(hipRest).multiplyScalar(rest.get('Hips').p.y / hipRest.y);
  body.scene.traverse((b) => {
    if (!b.isBone) return;
    const r = rest.get(b.name), parent = rest.get(b.parent?.name);
    const p = parent
      ? r.p.clone().sub(parent.p).applyQuaternion(delta.get(b.parent.name)).add(posed.get(b.parent.name))
      : r.p.clone().add(shift);
    posed.set(b.name, p);
    const q = delta.get(b.name).clone().multiply(r.q);
    const m = new THREE.Matrix4().compose(p, q, new THREE.Vector3(1, 1, 1));
    m.premultiply(b.parent.matrixWorld.clone().invert()); m.decompose(b.position, b.quaternion, b.scale);
    b.updateMatrixWorld(true);
  });
  const scene = new THREE.Group();
  body.scene.traverse((mesh) => {
    if (!mesh.isSkinnedMesh) return;
    mesh.skeleton.update();
    const g = mesh.geometry.clone(), points = [];
    for (let i = 0; i < g.attributes.position.count; i++) {
      const p = mesh.getVertexPosition(i, new THREE.Vector3()).applyMatrix4(mesh.matrixWorld);
      points.push(p.x, p.y, p.z);
    }
    g.setAttribute('position', new THREE.Float32BufferAttribute(points, 3));
    g.deleteAttribute('skinIndex'); g.deleteAttribute('skinWeight'); g.computeVertexNormals();
    const m = new THREE.MeshStandardMaterial(); m.name = 'apocalypse';
    scene.add(new THREE.Mesh(g, m));
  });
  const bounds = new THREE.Box3().setFromObject(scene), centre = bounds.getCenter(new THREE.Vector3());
  for (const mesh of scene.children) mesh.geometry.translate(-centre.x, -bounds.min.y, -centre.z);
  const file = resolve(output, `apocalypse/prop-${id}.glb`);
  await mkdir(dirname(file), { recursive: true });
  await writeFile(file, Buffer.from(await new GLTFExporter().parseAsync(scene, { binary: true })));
  console.log(id, new THREE.Box3().setFromObject(scene).getSize(new THREE.Vector3()).toArray());
}
