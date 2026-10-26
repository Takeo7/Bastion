// Node tests for the asset viewer's pure logic (no three.js, no DOM).
// Run: node --test tools/asset-library/tests/test_appearance.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { normalizeDescriptor, materialsForModel, summarizeFidelity, safeApiUrl } from '../web/appearance.js';
import { pickMaterial } from '../web/viewer.js';
import { MaterialBinder } from '../web/appearance.js';

const ORIGIN = 'http://127.0.0.1:5195';
const here = dirname(fileURLToPath(import.meta.url));
const F = (id) => `/api/assets/${id}/file`;

test('safeApiUrl rejects anything outside this server API', () => {
  assert.equal(safeApiUrl('/api/assets/a/file', ORIGIN), '/api/assets/a/file');
  for (const bad of ['https://evil.example/api/x', '//evil.example/api/x', 'javascript:alert(1)', 'file:///etc/passwd',
    '/vendor/three/build/three.module.js', '/api/../secret', 'http://u:p@127.0.0.1:5195/api/x', 'data:image/png;base64,AA']) {
    assert.equal(safeApiUrl(bad, ORIGIN), null, bad);
  }
});

test('normalizeDescriptor keeps the full material and texture contract', () => {
  const d = normalizeDescriptor({
    assetKind: 'Modelo',
    model: { id: 'm', kind: 'fbx', url: F('m'), name: 'Car' },
    textures: [{ id: 't', name: 'Atlas', path: 'A/Atlas.tga', url: F('t'), format: 'TGA' },
      { id: 'x', name: 'Ext', path: 'x.png', url: 'https://cdn.example/x.png' }],
    materials: [{ name: 'lambert1', map: F('t'), normalMap: F('n'), emissiveMap: F('e'), emissive: [0, 0.5, 1],
      alphaTest: 0.5, doubleSided: true, meshes: ['Car', 'Car_Overgrowth'], slot: 0, modelId: 'm', color: [255, 128, 0],
      extraMaps: { _Blood_Mask: F('b') } },
    { name: 'bad', map: 'https://evil.example/a.png' }],
    visibleNodes: ['Zombie_Bellboy_Male_01'],
    fidelity: 'resolved',
  }, ORIGIN);
  assert.equal(d.assetKind, 'modelo');
  assert.equal(d.model.name, 'Car');
  assert.deepEqual(d.textures.map((t) => [t.url, t.format]), [[F('t'), 'tga']]);
  const m = d.materials[0];
  assert.equal(m.emissiveMap, F('e'));
  assert.deepEqual(m.emissive, [0, 0.5, 1]);
  assert.equal(m.alphaTest, 0.5);
  assert.equal(m.doubleSided, true);
  assert.deepEqual(m.meshes, ['Car', 'Car_Overgrowth']);
  assert.equal(m.slot, 0);
  assert.equal(m.modelId, 'm');
  assert.deepEqual(m.color, [1, 128 / 255, 0]);
  assert.equal(d.materials[1].map, undefined);
  assert.deepEqual(d.visibleNodes, ['Zombie_Bellboy_Male_01']);
  // External map rejected + shader extra maps: server said resolved, client must say partial.
  assert.equal(d.serverFidelity, 'resolved');
  assert.equal(d.fidelity, 'partial');
  assert.ok(d.degraded.some((r) => r.includes('_Blood_Mask')));
});

test('parts: string material names, upper-case Unity transform, nested prefab warned', () => {
  const d = normalizeDescriptor({
    model: { id: 'main', kind: 'fbx', url: F('main') },
    materials: [{ name: 'Body', map: F('t1') }, { name: 'Gun', map: F('t2'), modelId: 'gun' }],
    parts: [
      { model: { id: 'gun', kind: 'fbx', url: F('gun'), name: 'SM_Gun' }, materials: ['Gun'], parentName: 'Hand_R',
        local: { Position: [1, 2, 3], Rotation: [0, 0, 0, 1], Scale: [1, 1, 1] } },
      { model: { id: 'hat', kind: 'fbx', url: F('hat') }, local: { position: [0, 1, 0] } },
      { prefab: { id: 'p2', name: 'SM_Nested' } },
      { model: { id: 'evil', kind: 'fbx', url: 'https://evil.example/api/x' } },
    ],
    fidelity: 'resolved',
  }, ORIGIN);
  assert.equal(d.parts.length, 2);
  const [gun, hat] = d.parts;
  assert.deepEqual(gun.materialNames, ['Gun']);
  assert.equal(gun.materials, undefined, 'names must never be passed as material objects');
  assert.deepEqual(gun.local, { position: [1, 2, 3], rotation: [0, 0, 0, 1], scale: [1, 1, 1] });
  assert.equal(gun.parentName, 'Hand_R');
  assert.deepEqual(hat.local, { position: [0, 1, 0] });
  assert.ok(d.clientWarnings.some((w) => w.includes('SM_Nested')));
  assert.equal(d.fidelity, 'partial');
  // Bindings by modelId: main model never receives the part's bindings.
  assert.deepEqual(materialsForModel(d.materials, 'main', 'main').map((m) => m.name), ['Body']);
  assert.deepEqual(materialsForModel(d.materials, 'gun', 'main').map((m) => m.name), ['Gun']);
  assert.deepEqual(materialsForModel(d.materials, 'hat', 'main').map((m) => m.name), []);
});

test('pickMaterial binds by mesh and slot, not first material', () => {
  const mats = [{ name: 'A', meshes: ['Car'], slot: 0 }, { name: 'B', meshes: ['Car'], slot: 1 }, { name: 'C' }];
  const binder = new MaterialBinder(mats.filter((m) => !m.meshes));
  assert.equal(pickMaterial(mats, binder, 'Car', 1, 'lambert1', 1).material.name, 'B');
  assert.equal(pickMaterial(mats, binder, 'Other', 0, 'C', 1).material.name, 'C');
  assert.equal(pickMaterial(mats, binder, 'Other', 0, 'Z', 2).status, 'missing');
});

test('summarizeFidelity never claims more than the server and drops on degraded reasons', () => {
  assert.equal(summarizeFidelity({ server: 'resolved', modelLoaded: true }).level, 'resolved');
  const s = summarizeFidelity({ server: 'resolved', modelLoaded: true, degraded: ['Cuerpo en gris neutro.'] });
  assert.equal(s.level, 'partial');
  assert.ok(s.reasons.includes('Cuerpo en gris neutro.'));
  assert.equal(summarizeFidelity({ server: 'partial', modelLoaded: true }).level, 'partial');
  assert.equal(summarizeFidelity({ server: 'resolved', modelLoaded: false }).level, 'unavailable');
});

const scratch = process.env.ASSET_SCRATCH || join(process.env.HOME || '', '.hermes/profiles/bastion/cache/scratch');
for (const [file, check] of [
  ['car_model-descriptor.json', (d) => {
    assert.equal(d.fidelity, 'resolved');
    assert.equal(d.materials[0].alphaTest, 0.5);
    assert.deepEqual(d.materials[0].meshes.length, 2);
  }],
  ['character_prefab-descriptor.json', (d) => {
    assert.deepEqual(d.visibleNodes, ['Zombie_Bellboy_Male_01']);
    assert.ok(d.materials[0].emissiveMap);
    assert.equal(d.fidelity, 'partial', 'shader extra maps must lower fidelity');
  }],
  ['animation_clip-descriptor.json', (d) => {
    assert.equal(d.assetKind, 'animacion');
    assert.equal(d.animationModel.name, 'PolygonSyntyCharacter');
  }],
]) {
  const path = join(scratch, file);
  test(`real descriptor ${file}`, { skip: !existsSync(path) && 'scratch descriptor not present' }, () => {
    const d = normalizeDescriptor(JSON.parse(readFileSync(path, 'utf8')), ORIGIN);
    assert.equal(d.clientWarnings.filter((w) => w.includes('fuera del servidor')).length, 0);
    check(d);
  });
}
