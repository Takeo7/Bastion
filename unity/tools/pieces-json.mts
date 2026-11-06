// RETIRED (5/11, docs/BIBLIOTECA.md §5): Assets/Bastion/Client/Resources/Pieces.json is the source, changed by hand; this script stays as history. To compare, run a copy that writes elsewhere, never over the list.
// The buildings' kit pieces for Unity (docs/UNITY-ARTE.md): 0.10.1's syntyManifest.ts PIECES (each one's pack and FBX,
// the box of it kept, how it sits in a tile: centre, run, out, unit, storey, base, its door leaf) with the source FBX
// each comes from (assets-src/synty, as convert.ts findFbx), to Assets/Bastion/Client/Resources/Pieces.json. Names and
// numbers only, nothing of Synty's. pieces-cut.py cuts them with Blender from this.
//   cd unity/tools && npx tsx pieces-json.mts
import { existsSync, readdirSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { basename, dirname, join, relative } from 'node:path';
const root = join(dirname(fileURLToPath(import.meta.url)), '../..');
const m = await import(join(root, 'packages/client/src/game/syntyManifest.ts'));
const sources = join(root, 'assets-src/synty');

const index = new Map<string, Map<string, string>>();
function findFbx(pack: string, name: string): string | null {
  const dir = join(sources, m.PACKS[pack].dir);
  if (name.includes('/')) {
    const path = join(dir, `${name}.fbx`);
    return existsSync(path) ? path : null;
  }
  let found = index.get(pack);
  if (!found) {
    found = new Map();
    const walk = (d: string) => {
      for (const entry of readdirSync(d, { withFileTypes: true })) {
        const path = join(d, entry.name);
        if (entry.isDirectory()) walk(path);
        else if (/\.fbx$/i.test(entry.name)) {
          const key = basename(entry.name, '.fbx');
          // As convert.ts: Unreal's and the collision meshes' copies only if there is no other.
          const worse = /unreal|unsupported|[\\/]collision[\\/]/i.test(path);
          if (!found!.has(key) || !worse) found!.set(key, path);
        }
      }
    };
    if (existsSync(dir)) walk(dir);
    index.set(pack, found);
  }
  return found.get(name) ?? null;
}

// (Unity's own, Ana's #3 of 1/11, Arturo: houses of Town's pieces, not all alike): the kit's other windows, doors and
// open shutters, each house picking its own, and the roller garage door in its wall, four tiles wide, without the
// track inside it (WallLayout.HouseLook).
const HOUSE_PIECES: Record<string, Record<string, unknown>> = {
  houseWindow2: { pack: 'town', fbx: 'SM_Bld_House_Window_02', centre: [1.25, -0.1], run: 'x', out: -1 },
  houseWindow3: { pack: 'town', fbx: 'SM_Bld_House_Window_03', centre: [1.25, -0.1], run: 'x', out: -1 },
  houseWindow4: { pack: 'town', fbx: 'SM_Bld_House_Window_04', centre: [1.25, -0.1], run: 'x', out: -1 },
  houseWindow5: { pack: 'town', fbx: 'SM_Bld_House_Window_05', centre: [3.75, -0.1], run: 'x', out: -1, drop: ['Glass'] },
  houseShutters: { pack: 'town', fbx: 'SM_Bld_House_Window_Shutters_Open_01', centre: [1.25, -0.1], run: 'x', out: -1 },
  houseDoor2: { pack: 'town', fbx: 'SM_Bld_House_Door_02', centre: [1.25, -0.1], run: 'x', out: -1, parts: true, leaf: 'SM_Bld_House_Door' },
  houseDoor3: { pack: 'town', fbx: 'SM_Bld_House_Door_03', centre: [1.25, -0.1], run: 'x', out: -1, parts: true, leaf: 'SM_Bld_House_Door' },
  houseGarage: { pack: 'town', fbx: 'SM_Bld_House_ExteriorWall_GarageDoor_01', centre: [2.5, -0.1], run: 'x', out: -1, keep: [[-50, -0.3, -0.02], [50, 0.05, 50]] },
};
// (Unity's own, phase 2 of Ana's #3, 1/11: the shops of POLYGON Shops' brick and render, not a house's boards, each its
// facing: WallLayout.ShopKits.) Shops has no source files here: its FBX are the Unity package's, in Assets/Synty (off
// GitHub). Its walls are 2.5 m modules, 3 m a storey, their outer face to −y: one tile cut from the middle of each. A
// facing's pieces are the white brick's model with its brick swapped for another of the pack's (`as`, `swap`: nothing
// to cut, PieceMake gives the same model those materials).
const SHOPS = 'unity/Bastion/Assets/Synty/PolygonShops/Models';
const TILE = [[0.625, -50, -0.02], [1.875, 50, 50]];
const SHOP_PIECES: Record<string, Record<string, unknown>> = {
  shopWall: { pack: 'shops', fbx: 'SM_Bld_Base_Wall_01', centre: [1.25, 0], run: 'x', out: -1, storey: 3, keep: TILE },
  // The pack's sash window, its frame's outer moulding (wider than a tile) cut to the tile.
  shopWindowWall: { pack: 'shops', fbx: 'SM_Bld_Wall_Window_Old_01', centre: [1.25, 0], run: 'x', out: -1, storey: 3, keep: TILE, drop: ['Glass'] },
  shopDoorWall: { pack: 'shops', fbx: 'SM_Bld_Base_Wall_Door_01', centre: [1.25, 0], run: 'x', out: -1, storey: 3, keep: TILE },
  shopSideDoor: { pack: 'shops', fbx: 'SM_Bld_Base_Door_01', centre: [0.5, 0], run: 'x', out: -1, storey: 3, parts: true, leaf: 'SM_Bld_Base_Door_01' },
  // A corner's post: a square of the wall's thickness, closed all round (the pack's corner fillers are a quarter of it).
  shopCorner: { pack: 'shops', fbx: 'SM_Bld_Base_Wall_01', centre: [0.1125, 0], run: 'x', out: -1, storey: 3, keep: [[0, -50, -0.02], [0.225, 50, 50]], fill: true },
  // Over a gap and a shop's front: the wall above a house's door's height (0.81 of a storey, as Town's houseLintel).
  shopLintel: { pack: 'shops', fbx: 'SM_Bld_Base_Wall_01', centre: [1.25, 0], run: 'x', out: -1, storey: 3, keep: [[0.625, -50, 2.43], [1.875, 50, 50]] },
};
const FACINGS: Record<string, string> = {
  Red: 'PolygonShops_Mat_Base_Brick_Red_01', Coloured: 'PolygonShops_Mat_Brick_Coloured_01', Render: 'PolygonShops_Mat_Base_Blank_01',
};
for (const [facing, material] of Object.entries(FACINGS))
  for (const id of ['shopWall', 'shopWindowWall', 'shopDoorWall', 'shopLintel', 'shopCorner'])
    SHOP_PIECES[`${id}${facing}`] = { ...SHOP_PIECES[id], as: id, swap: { PolygonShops_Mat_Base_Brick_White_01: material } };
// The awnings over a shop's front windows and doors (WallLayout.Awnings): the pack's, 2.4 m long, each cut in four half
// tiles, its two ends and the two halves of its middle, so a run of any length is the middle's halves between its ends.
// Their backs on the thickened wall's face (0.11 m of Shops' wall to 0.197), as high as the pack has them (a door's leaf
// swings in, under them).
const AWNINGS: Record<string, string> = { Red: 'SM_Bld_Awning_01', Blue: 'SM_Bld_Awning_02', Maroon: 'SM_Bld_Awning_03' };
for (const [style, fbx] of Object.entries(AWNINGS))
  for (const [half, from, to, middle] of [['EndL', 0, 0.625, 0.625], ['MidL', 0.625, 1.25, 1.25], ['MidR', 1.25, 1.875, 1.25], ['EndR', 1.875, 2.5, 1.875]] as const)
    SHOP_PIECES[`awning${style}${half}`] = { pack: 'shops', fbx, centre: [middle, 0.087], run: 'x', out: -1, storey: 3, keep: [[from, -50, -50], [to, 50, 50]] };
// (Unity's own too, phase 3 of #3: the offices of stone or concrete with a cornice, as City's old offices are; WallLayout's
// office kits.) Of Shops' too: its wall, door and corner as the shops', its tall sash window (two sashes and their glass,
// the materials of its wall's own renderer, not the glass's), the cornice along the top of the top storey and the plinth
// at the foot of the ground one, each a tile of the pack's mouldings; in each facing's stone or concrete.
const OFFICE_PIECES: Record<string, Record<string, unknown>> = {
  officeWindowWall: { pack: 'shops', fbx: 'SM_Bld_Wall_Window_Grid_01', centre: [1.25, 0], run: 'x', out: -1, storey: 3, keep: TILE, drop: ['Glass'], renderer: 'SM_Bld_Wall_Window_Grid_01' },
  officeCrown: { pack: 'shops', fbx: 'SM_Bld_Trim_Wall_High_03', centre: [1.25, 0], run: 'x', out: -1, storey: 3, keep: [[0.625, -50, -50], [1.875, 50, 50]] },
  officePlinth: { pack: 'shops', fbx: 'SM_Bld_Trim_Wall_Low_02', centre: [1.25, 0], run: 'x', out: -1, storey: 3, keep: [[0.625, -50, -50], [1.875, 50, 50]] },
};
const OFFICE_FACINGS: Record<string, string> = { Sillar: 'PolygonShops_Mat_Base_Concrete_Blocks_01', Hormigon: 'PolygonShops_Mat_Concrete_01' };
for (const [facing, material] of Object.entries(OFFICE_FACINGS)) {
  for (const [id, model] of [['officeWall', 'shopWall'], ['officeDoorWall', 'shopDoorWall'], ['officeLintel', 'shopLintel'], ['officeCorner', 'shopCorner'], ['officeWindowWall', 'officeWindowWall']] as const)
    OFFICE_PIECES[`${id}${facing}`] = { ...(SHOP_PIECES[model] ?? OFFICE_PIECES[model]), as: model, swap: { PolygonShops_Mat_Base_Brick_White_01: material } };
  for (const id of ['officeCrown', 'officePlinth'])
    OFFICE_PIECES[`${id}${facing}`] = { ...OFFICE_PIECES[id], as: id, swap: { PolygonShops_Mat_Base_Brick_Red_01: material } };
}
// (Unity's own, phase 4 of #3: the outskirts' and estates' halls of POLYGON Gang Warfare's warehouse, brick or block, or
// sheet metal; WallLayout's hall kits.) Its FBX are the Unity package's too. Its walls are 3 m modules, 3 m a storey,
// their outer face to −y. The brick's wall a tile from the middle; its tall factory window and the sheet's corrugations
// in whole bays (a window's metre between its posts, four of the sheet's 0.286 m waves), squeezed to a tile, so a run
// of them goes on unbroken; a door's wall with its frame, squeezed to a tile, and its leaf apart (it swings); a corner a
// square of the wall, closed all round. A facing is the pack's atlas in another of its colours (`swap`).
const GANG = 'unity/Bastion/Assets/Synty/PolygonGangWarfare/Models';
const BAY = [[1, -50, -50], [2, 50, 50]];
const WAVES = [[6 / 7, -50, -50], [2, 50, 50]];
const HALL_PIECES: Record<string, Record<string, unknown>> = {
  hallWall: { pack: 'gang', fbx: 'SM_Bld_Wall_Brick_01', centre: [1.5, 0], run: 'x', out: -1, storey: 3, keep: [[0.875, -50, -50], [2.125, 50, 50]] },
  hallWindowWall: { pack: 'gang', fbx: 'SM_Bld_Wall_Brick_Window_03', centre: [1.5, 0], run: 'x', out: -1, storey: 3, keep: BAY, squeeze: 1.25, drop: ['Glass'] },
  hallDoorWall: { pack: 'gang', fbx: 'SM_Bld_Wall_Brick_Door_01', centre: [1.5, 0], run: 'x', out: -1, storey: 3, keep: [[0.8, -50, -50], [2.2, 50, 50]], squeeze: 1.25 / 1.4, drop: ['SM_Bld_Wall_Door_01'] },
  hallDoor: { pack: 'gang', fbx: 'SM_Bld_Wall_Brick_Door_01', centre: [1.5, 0], run: 'x', out: -1, storey: 3, squeeze: 1.25 / 1.4, parts: true, leaf: 'SM_Bld_Wall_Door_01', drop: ['SM_Bld_Wall_Brick_Door_01'] },
  hallLintel: { pack: 'gang', fbx: 'SM_Bld_Wall_Brick_01', centre: [1.5, 0], run: 'x', out: -1, storey: 3, keep: [[0.875, -50, 2.43], [2.125, 50, 50]] },
  hallCorner: { pack: 'gang', fbx: 'SM_Bld_Wall_Brick_01', centre: [0.195, 0], run: 'x', out: -1, storey: 3, keep: [[0, -50, -0.02], [0.39, 50, 50]], fill: true },
  sheetWall: { pack: 'gang', fbx: 'SM_Bld_Wall_Metal_01', centre: [10 / 7, -0.0726], run: 'x', out: -1, storey: 3, keep: WAVES, squeeze: 1.09375 },
  sheetWindowWall: { pack: 'gang', fbx: 'SM_Bld_Wall_Metal_Window_01', centre: [10 / 7, -0.0726], run: 'x', out: -1, storey: 3, keep: WAVES, squeeze: 1.09375, drop: ['Glass'] },
  sheetDoorWall: { pack: 'gang', fbx: 'SM_Bld_Wall_Metal_Door_01', centre: [1.5, -0.0726], run: 'x', out: -1, storey: 3, keep: [[1.5 - 4 / 7, -50, -50], [1.5 + 4 / 7, 50, 50]], squeeze: 1.09375, drop: ['SM_Bld_Metal_Door_01'] },
  sheetDoor: { pack: 'gang', fbx: 'SM_Bld_Wall_Metal_Door_01', centre: [1.5, -0.0726], run: 'x', out: -1, storey: 3, squeeze: 1.09375, parts: true, leaf: 'SM_Bld_Metal_Door_01', drop: ['SM_Bld_Wall_Metal_Door_01'] },
  sheetLintel: { pack: 'gang', fbx: 'SM_Bld_Wall_Metal_01', centre: [10 / 7, -0.0726], run: 'x', out: -1, storey: 3, keep: [[6 / 7, -50, 2.43], [2, 50, 50]], squeeze: 1.09375 },
  sheetCorner: { pack: 'gang', fbx: 'SM_Bld_Wall_Metal_01', centre: [6 / 7 + 0.1085, -0.0726], run: 'x', out: -1, storey: 3, keep: [[6 / 7, -50, -0.2], [6 / 7 + 0.217, 50, 50]], fill: true },
};
// Each facing's atlas (BuildingLooks.HallFacings): the brick's in red, brown or grey block; the sheet's in brown, red, green or blue.
const HALL_FACINGS: Record<string, [string, string]> = {
  LadrilloRojo: ['hall', '01'], LadrilloPardo: ['hall', '02'], Bloque: ['hall', '04'],
  ChapaMarron: ['sheet', '01'], ChapaRoja: ['sheet', '02'], ChapaVerde: ['sheet', '03'], ChapaAzul: ['sheet', '04'],
};
for (const [facing, [kit, atlas]] of Object.entries(HALL_FACINGS))
  for (const id of ['Wall', 'WindowWall', 'DoorWall', 'Door', 'Lintel', 'Corner'])
    HALL_PIECES[`${kit}${id}${facing}`] = { ...HALL_PIECES[kit + id], as: kit + id, swap: { PolygonGangWarfare_01_A: `PolygonGangWarfare_${atlas}_A` } };
const UNITY_PACKS: Record<string, string> = { shops: SHOPS, gang: GANG };
const pieces: Record<string, Record<string, unknown>> = {};
const missing: string[] = [];
for (const [id, p] of Object.entries({ ...(m.PIECES as Record<string, Record<string, unknown>>), ...HOUSE_PIECES, ...SHOP_PIECES, ...OFFICE_PIECES, ...HALL_PIECES })) {
  const own = UNITY_PACKS[p.pack as string];
  const src = own ? (existsSync(join(root, own, `${p.fbx}.fbx`)) ? join(root, own, `${p.fbx}.fbx`) : null) : findFbx(p.pack as string, p.fbx as string);
  if (!src) missing.push(`${id} (${p.pack}/${p.fbx})`);
  pieces[id] = { ...p, src: src ? relative(root, src) : null };
}
const out = join(root, 'unity/Bastion/Assets/Bastion/Client/Resources/Pieces.json');
writeFileSync(out, JSON.stringify({ kitScale: m.KIT_SCALE, pieces }, null, 1) + '\n');
console.log(Object.keys(pieces).length, 'pieces ->', relative(root, out), missing.length ? `; no source: ${missing.join(', ')}` : '');
