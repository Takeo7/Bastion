import type { BuildId, HeadId, TemplateId } from '@bastion/engine';

// The Synty POLYGON assets the game uses, and for what: the single list.
// tools/synty/convert.ts reads it to convert exactly these FBX files to GLB
// (public/models/synty/<pack>/<name>.glb) and these atlases to WebP; the game
// reads it to know which model shows each unit, weapon, hat and prop. Add an
// asset here, run the converter, and the game can use it (README in
// tools/synty). The licence lets only converted models that the game uses
// into the repository, so keep this list to what is used.
//
// No three.js here: the converter runs in Node.

/** Metres to game units: a tile is 1.5 m, so a 1.8 m survivor stands 1.2 tall. */
export const SYNTY_SCALE = 1 / 1.5;

export type PackId = 'apocalypse' | 'cityZombies' | 'bossZombies' | 'town' | 'farm' | 'nature' | 'city';

/**
 * Where each pack lives under assets-src/synty/. A pack that only comes as a
 * Unity package is extracted next to it first (`extracted`): its paths are
 * then the package's own (Assets/Synty/…).
 */
export const PACKS: Record<PackId, { dir: string; materialList?: string; unitypackage?: string }> = {
  apocalypse: { dir: 'POLYGON_Apocalypse_SourceFiles_v4', materialList: 'MaterialList_PolygonApocalypse.txt' },
  cityZombies: { dir: 'POLYGON_City_Zombies_Extracted', unitypackage: 'POLYGON_City_Zombies_Unity_2022_3_v1_7_2.unitypackage' },
  bossZombies: { dir: 'POLYGON_BossZombies_SourceFiles_v4', materialList: 'MaterialList_PolygonBossZombies.txt' },
  town: { dir: 'POLYGON_Town_SourceFiles_v7', materialList: 'MaterialList_PolygonTown.txt' },
  farm: { dir: 'POLYGON_Farm_SourceFiles_v6', materialList: 'MaterialList_PolygonFarm.txt' },
  nature: { dir: 'POLYGON_Nature_SourceFiles_v3', materialList: 'MaterialList_PolygonNature.txt' },
  city: { dir: 'POLYGON_City_SourceFiles_v6', materialList: 'MaterialList_PolygonCity.txt' },
};

/**
 * Texture atlases, one per pack (Synty paints a whole pack from one image).
 * The game loads each once and every model of the pack shares it.
 * `albedo` is how the pack's MaterialList calls it.
 */
export const ATLASES = {
  apocalypse: { pack: 'apocalypse', src: 'Textures/PolygonApocalypse_Texture_01_A.png', albedo: 'PolygonApocalypse_01_A.png', size: 2048 },
  cityZombies: { pack: 'cityZombies', src: 'Assets/Synty/PolygonCityZombies/Textures/Alts/PolygonZombie_01_A.png', albedo: 'PolygonZombie_01_A.png', size: 1024 },
  bossZombies: { pack: 'bossZombies', src: 'Textures/PolygonZombieBoss_Texture_01_A.png', albedo: 'PolygonZombieBoss_01_A.png', size: 1024 },
  town: { pack: 'town', src: 'Textures/PolygonTown_01_A.png', albedo: 'PolygonTown_01_A.png', size: 2048 },
  farm: { pack: 'farm', src: 'Textures/PolygonFarm_Texture_01_A.png', albedo: 'PolygonFarm_01_A.png', size: 1024 },
  nature: { pack: 'nature', src: 'Textures/PolygonNature_01.png', albedo: 'PolygonNature_01.png', size: 1024 },
  city: { pack: 'city', src: 'Textures/PolygonCity_Texture_01_A.png', albedo: 'PolygonCity_01_A.png', size: 2048 },
} satisfies Record<string, { pack: PackId; src: string; albedo: string; size: number }>;

export type AtlasId = keyof typeof ATLASES;

/** Kinds of converted model; a model's file is `<kind>-<id>.glb` in its pack's folder. */
export type ModelKind = 'chr' | 'hat' | 'wep' | 'prop';

/** Path of a converted file under public/ (the game prefixes Vite's base URL). */
export const syntyFile = (pack: PackId, file: string) => `models/synty/${pack}/${file}`;
export const modelFile = (pack: PackId, kind: ModelKind, id: string) => syntyFile(pack, `${kind}-${id}.glb`);
export const atlasFile = (atlas: AtlasId) => syntyFile(ATLASES[atlas].pack, `${atlas}.webp`);

// ------------------------------------------------------------- characters

/**
 * Bones the converter keeps (Synty's animation-pack names); the rest fold
 * into these. Their order is free: the game finds them by name.
 */
export const SYNTY_BONES = [
  'Hips', 'Spine_01', 'Spine_02', 'Spine_03', 'Neck', 'Head',
  'Clavicle_L', 'Shoulder_L', 'Elbow_L', 'Hand_L', 'Clavicle_R', 'Shoulder_R', 'Elbow_R', 'Hand_R',
  'UpperLeg_L', 'LowerLeg_L', 'Ankle_L', 'Ball_L', 'UpperLeg_R', 'LowerLeg_R', 'Ankle_R', 'Ball_R',
] as const;

export type SyntyBone = (typeof SYNTY_BONES)[number];

/** A rigid piece hung from a bone: hair, beard, a hat. `dye`: it takes the helmet colour. */
export interface Attachment {
  fbx: string;
  bone: SyntyBone;
  name: string;
  dye?: boolean;
}

/**
 * A skinned character. `mesh` picks one body out of an FBX that holds many
 * (City Zombies). `hair` names the attachments a hat hides. `female` bodies
 * have smaller heads: hats shrink a little on them.
 */
export interface CharacterAsset {
  pack: PackId;
  fbx: string;
  mesh?: string;
  attach?: Attachment[];
  hair?: string[];
  female?: boolean;
}

const att = (fbx: string, name: string, dye = false, bone: SyntyBone = 'Head'): Attachment => ({ fbx: `SM_Chr_Attach_${fbx}`, bone, name, dye });
const apoc = (fbx: string, attach: Attachment[] = [], female = false): CharacterAsset => ({
  pack: 'apocalypse',
  fbx: `SK_Chr_${fbx}`,
  attach,
  hair: attach.filter((a) => a.name === 'hair').map((a) => a.name),
  female,
});
const zombie = (mesh: string, female = false): CharacterAsset => ({ pack: 'cityZombies', fbx: 'Zombies', mesh, female });

export const CHARACTERS = {
  // The squad: survivors of every kind.
  soldierM: apoc('Soldier_Male_01'),
  soldierF: apoc('Soldier_Female_01', [att('Soldier_Female_Hair_01', 'hair')], true),
  hunter: apoc('Hunter_Male_01', [att('Hunter_Male_Beard_01', 'beard'), att('Hunter_Male_Hat_01', 'hat', true)]),
  sheriff: apoc('Sheriff_Male_01', [att('Sheriff_Male_Hair_01', 'hair'), att('Sheriff_Male_Hat_01', 'hat', true)]),
  wanderer: apoc('Wanderer_Male_01', [att('Wanderer_Male_Hair_01', 'hair'), att('Wanderer_Male_Beard_01', 'beard')]),
  coolM: apoc('Cool_Male_01', [att('Cool_Male_Hair_01', 'hair')]),
  mechanic: apoc('Mechanic_Female_01', [att('Mechanic_Female_Hair_01', 'hair')], true),
  scout: apoc('Scout_Female_01', [att('Scout_Female_Hat_01', 'hat', true)], true),
  coolF: apoc('Cool_Female_01', [att('Cool_Female_Hair_01', 'hair')], true),
  punk: apoc('Punk_Female_01', [att('Punk_Female_Hair_01', 'hair')], true),
  riotCop: apoc('RiotCop_Male_01', [att('RiotCop_Male_Hair_01', 'hair'), att('RiotCop_Male_Beard_01', 'beard')]),
  // The Cuervos: raiders, their leaders and hunters.
  criminal: apoc('Criminal_Male_01', [att('Criminal_Male_Hair_01', 'hair'), att('Criminal_Male_Beard_01', 'beard')]),
  teen: apoc('Teen_Male_01', [att('Teen_Male_Hat_01', 'hat')]),
  press: apoc('Press_Male_01', [att('Press_Male_Hair_01', 'hair'), att('Press_Male_Beard_01', 'beard'), att('Press_Male_Eyepatch_01', 'eyepatch')]),
  biker: apoc('Biker_Male_01', [att('Biker_Male_Hair_01', 'hair'), att('Biker_Male_Beard_01', 'beard')]),
  homeless: apoc('Homeless_Male_01', [att('Homeless_Male_Beard_01', 'beard')]),
  emo: apoc('Emo_Female_01', [att('Emo_Female_Hair_01', 'hair')], true),
  // Survivors to rescue.
  nerd: apoc('Nerd_Female_01', [att('Nerd_Female_Hair_01', 'hair'), att('Nerd_Female_Glasses_01', 'glasses')], true),
  business: apoc('Business_Male_01', [att('Business_Male_Hair_01', 'hair')]),
  waitress: apoc('Waitress_Female_01', [att('Waitress_Female_Hair_01', 'hair')], true),
  // The dead.
  zMale: apoc('Zombie_Male_01', [att('Zombie_Male_Hair_01', 'hair')]),
  zFemale: apoc('Zombie_Female_01', [att('Zombie_Female_Hair_01', 'hair')], true),
  zFather: zombie('Zombie_Father_Male_01'),
  zMother: zombie('Zombie_Mother_Female_01', true),
  zHobo: zombie('Zombie_Hobo_Male_01'),
  zShopKeeper: zombie('Zombie_ShopKeeper_Male_01'),
  zGrandpa: zombie('Zombie_Grandpa_Male_01'),
  zBusiness: zombie('Zombie_Business_Female_01', true),
  zRoadworker: zombie('Zombie_Roadworker_Male_01'),
  zPatient: zombie('Zombie_Patient_Female_01', true),
  // Corredores: the recently dead, still quick.
  zJock: zombie('Zombie_Jock_Male_01'),
  zCheerleader: zombie('Zombie_Cheerleader_Female_01', true),
  zHoodie: zombie('Zombie_Hoodie_Male_01'),
  zSchoolGirl: zombie('Zombie_SchoolGirl_Female_01', true),
  // Acorazados: police and soldiers, vest and helmet still on.
  zRiotCop: zombie('Zombie_RiotCop_Male_01'),
  zMilitary: zombie('Zombie_Military_Male_01'),
  zPolice: zombie('Zombie_Police_Male_01'),
  zFirefighter: zombie('Zombie_Firefighter_Male_01'),
  // Chillón: the screamer.
  zWretch: { pack: 'bossZombies', fbx: 'SK_Chr_ZombieBoss_Wretch_01', attach: [{ fbx: 'SM_Chr_Attach_Wretch_Hair_01', bone: 'Head', name: 'hair' }] },
} satisfies Record<string, CharacterAsset>;

export type CharacterId = keyof typeof CHARACTERS;

/** Survivors the squad is drawn from, by build (each soldier always gets the same one). */
export const SQUAD_BODIES: Record<BuildId, CharacterId[]> = {
  slim: ['scout', 'mechanic', 'coolF', 'punk', 'coolM'],
  normal: ['soldierM', 'soldierF', 'hunter', 'wanderer', 'mechanic', 'scout', 'sheriff', 'coolF'],
  heavy: ['soldierM', 'hunter', 'sheriff', 'riotCop', 'wanderer'],
};

/** Who each enemy and objective is (for now the alien templates stand in for the dead). */
export const UNIT_BODIES: Partial<Record<TemplateId, CharacterId[]>> = {
  zombie: ['zMale', 'zFemale', 'zFather', 'zMother', 'zHobo', 'zShopKeeper', 'zGrandpa', 'zBusiness', 'zRoadworker', 'zPatient'],
  xenoid: ['zJock', 'zCheerleader', 'zHoodie', 'zSchoolGirl'],
  mec: ['zRiotCop', 'zMilitary', 'zPolice', 'zFirefighter'],
  sectoid: ['zWretch'],
  trooper: ['criminal', 'teen', 'press'],
  officer: ['biker'],
  lancer: ['homeless', 'emo'],
  vip: ['nerd', 'business', 'waitress'],
};

/** Templates that move like the dead: hunched, arms out, no weapon. */
export const DEAD_TEMPLATES: ReadonlySet<TemplateId> = new Set<TemplateId>(['zombie', 'xenoid', 'mec', 'sectoid']);

// ---------------------------------------------------------------- headgear

/** Hats and helmets the barracks' heads put on (they hide the hair). Hung from the head bone. */
export const HEADGEAR = {
  helmet: { pack: 'apocalypse', fbx: 'SM_Chr_Attach_Soldier_Male_Helmet_01', dye: true },
  cap: { pack: 'apocalypse', fbx: 'SM_Chr_Attach_Teen_Male_Hat_01', dye: true },
  beanie: { pack: 'apocalypse', fbx: 'SM_Chr_Attach_Scout_Female_Hat_01', dye: true },
  gasMask: { pack: 'apocalypse', fbx: 'SM_Chr_Attach_GasMask_01', dye: false },
  hunterHat: { pack: 'apocalypse', fbx: 'SM_Chr_Attach_Hunter_Male_Hat_02', dye: true },
} satisfies Record<string, { pack: PackId; fbx: string; dye: boolean }>;

export type HeadgearId = keyof typeof HEADGEAR;

/** What each head of the barracks wears; null: the survivor as they come (their own hair or hat). */
export const HEAD_GEAR: Record<HeadId, HeadgearId | null> = {
  helmet: 'helmet',
  hood: 'hunterHat',
  beret: 'beanie',
  cap: 'cap',
  mask: 'gasMask',
  bare: null,
};

// ----------------------------------------------------------------- weapons

/** Guns and melee weapons, held in the right hand; the converter turns them barrel +Z, grip at the origin. */
export const WEAPONS = {
  rifle: { pack: 'apocalypse', fbx: 'SM_Wep_AssaultRifle_01' },
  rifle2: { pack: 'apocalypse', fbx: 'SM_Wep_AssaultRifle_02' },
  rifle3: { pack: 'apocalypse', fbx: 'SM_Wep_AssaultRifle_03' },
  shotgun: { pack: 'apocalypse', fbx: 'SM_Wep_Shotgun_01' },
  sniper: { pack: 'apocalypse', fbx: 'SM_Wep_SniperRifle_01' },
  sniper2: { pack: 'apocalypse', fbx: 'SM_Wep_SniperRifle_02' },
  machineGun: { pack: 'apocalypse', fbx: 'SM_Wep_MachineGun_01' },
  huntingRifle: { pack: 'apocalypse', fbx: 'SM_Wep_HuntingRifle_01' },
  smg: { pack: 'apocalypse', fbx: 'SM_Wep_SubMGun_01' },
  baton: { pack: 'apocalypse', fbx: 'SM_Wep_Baton_01', melee: true },
} satisfies Record<string, { pack: PackId; fbx: string; melee?: boolean }>;

export type WeaponId = keyof typeof WEAPONS;

/** The weapon each template carries, by weapon tier (1–3); none for the unarmed. */
export const UNIT_WEAPONS: Partial<Record<TemplateId, [WeaponId, WeaponId, WeaponId]>> = {
  rookie: ['rifle', 'rifle2', 'rifle3'],
  specialist: ['rifle', 'rifle2', 'rifle3'],
  assault: ['shotgun', 'shotgun', 'shotgun'],
  sharpshooter: ['sniper', 'sniper2', 'sniper2'],
  grenadier: ['machineGun', 'machineGun', 'machineGun'],
  trooper: ['huntingRifle', 'huntingRifle', 'huntingRifle'],
  officer: ['smg', 'smg', 'smg'],
  lancer: ['baton', 'baton', 'baton'],
};

// ------------------------------------------------------------------- props

/**
 * A static prop, normalised to the map's tiles: scaled so its longest side
 * is `fit` tiles (none: true size), only along that side with `stretch` (a
 * counter or a fence keeps its height), its height and depth times `thick`;
 * turned `yaw` quarter turns (counter-clockwise from above) so its front
 * looks north (−Z) like the game's props; then shifted `shift` tiles along
 * its own axes. Town furniture comes facing south (yaw 2, but desks
 * north), Apocalypse furniture and barricades facing east (yaw 1).
 */
export interface PropAsset {
  pack: PackId;
  fbx: string;
  fit?: number;
  stretch?: boolean;
  thick?: number;
  yaw?: number;
  shift?: [number, number];
}

const prop = (pack: PackId, fbx: string, o: Omit<PropAsset, 'pack' | 'fbx'> = {}): PropAsset => ({ pack, fbx, ...o });

export const PROPS = {
  // Streets.
  carWreck: prop('apocalypse', 'SM_Prop_Car_Wrecked_01', { fit: 1.95, yaw: 1 }),
  carRusted: prop('apocalypse', 'SM_Prop_Car_Wrecked_Rusted_01', { fit: 1.95, yaw: 1 }),
  carSuv: prop('apocalypse', 'SM_Prop_Car_Wrecked_SUV_01', { fit: 1.95, yaw: 1 }),
  carOpen: prop('apocalypse', 'SM_Prop_Car_Wrecked_OpenBoot_01', { fit: 1.95, yaw: 1 }),
  planter: prop('city', 'SM_Prop_Planter_01', { fit: 0.9 }),
  dumpster: prop('apocalypse', 'SM_Prop_Dumpster_01', { fit: 0.95 }),
  rubbishBin: prop('apocalypse', 'SM_Prop_RubbishBin_01'),
  streetPot: prop('apocalypse', 'SM_Prop_Pot_Street_Plant_01', { fit: 0.9 }),
  vending: prop('town', 'SM_Prop_VendingMachine_01', { yaw: 2 }),
  statue: prop('apocalypse', 'SM_Prop_Statue_01', { fit: 0.95 }),
  concreteBarrier: prop('apocalypse', 'SM_Prop_Barrier_Concrete_01', { fit: 1 }),
  barricade1: prop('apocalypse', 'SM_Prop_Barricade_01', { fit: 1, stretch: true, yaw: 1 }),
  barricade2: prop('apocalypse', 'SM_Prop_Barricade_03', { fit: 1, stretch: true, yaw: 1 }),
  barricadeTall: prop('apocalypse', 'SM_Prop_Barricade_05', { fit: 1, stretch: true, yaw: 1 }),
  barricadePallet: prop('apocalypse', 'SM_Prop_Barricade_Pallet_01', { fit: 1, stretch: true, yaw: 1 }),
  sandbags: prop('apocalypse', 'SM_Prop_Sandbag_Wall_02', { fit: 1, stretch: true, yaw: 1 }),
  crate: prop('apocalypse', 'SM_Prop_Crate_01'),
  crates: prop('apocalypse', 'SM_Prop_Crate_Large_02'),
  barrels: prop('apocalypse', 'SM_Prop_BarrelStack_02', { fit: 0.95 }),
  generator: prop('apocalypse', 'SM_Prop_Generator_01', { fit: 0.9 }),
  gasTank: prop('farm', 'SM_Prop_Gastank_01', { fit: 0.95 }),
  pipes: prop('apocalypse', 'SM_Prop_Pipe_Large_01', { fit: 1, stretch: true, thick: 0.5 }),
  loudSpeaker: prop('apocalypse', 'SM_Prop_Loud_Speaker_01', { fit: 0.7 }),
  rubble1: prop('apocalypse', 'SM_Prop_Rubble_Concrete_01', { fit: 0.95 }),
  rubble2: prop('apocalypse', 'SM_Prop_Rubble_Concrete_02', { fit: 0.95 }),
  rubbleWall: prop('apocalypse', 'SM_Prop_Rubble_Concrete_Frame_01', { fit: 0.95 }),
  // Fences and farms. A fence piece runs from the post to the north edge (half a tile).
  fencePost: prop('farm', 'SM_Prop_Fence_Wood_Pole_01'),
  fencePanel: prop('farm', 'SM_Prop_Fence_Wood_01', { fit: 0.5, stretch: true, yaw: 1, shift: [0, -0.25] }),
  fenceWire: prop('farm', 'SM_Prop_Fence_Wire_01', { fit: 0.5, stretch: true, yaw: 1, shift: [0, -0.25] }),
  gatePost: prop('farm', 'SM_Prop_Fence_Fancy_Pole_01'),
  hayBale: prop('farm', 'SM_Prop_Hay_Bale_Square_01', { fit: 0.9 }),
  hayRound: prop('farm', 'SM_Prop_Hay_Bale_Round_01', { fit: 0.95 }),
  // Indoors.
  shelfLow: prop('apocalypse', 'SM_Prop_ShopShelf_Wall_01', { fit: 1, yaw: 1 }),
  shelfHigh: prop('apocalypse', 'SM_Prop_Shop_Shelf_01', { fit: 1, stretch: true, yaw: 1 }),
  bookshelf: prop('town', 'SM_Prop_Bookshelf_01', { fit: 1, stretch: true, yaw: 2 }),
  counter: prop('town', 'SM_Prop_Kitchen_Counter_01', { fit: 1, stretch: true, yaw: 2 }),
  shopCounter: prop('apocalypse', 'SM_Prop_ShopCounter_01', { fit: 1, stretch: true, yaw: 1 }),
  table: prop('town', 'SM_Prop_DiningTable_01', { fit: 1, stretch: true }),
  sofa: prop('town', 'SM_Prop_Couch_01', { fit: 1, stretch: true, yaw: 2 }),
  sofaDirty: prop('apocalypse', 'SM_Prop_Couch_01', { fit: 1, stretch: true, yaw: 1 }),
  bed: prop('town', 'SM_Prop_Bed_Single_01', { fit: 1, stretch: true, yaw: 2 }),
  wardrobe: prop('town', 'SM_Prop_Wardrobe_01_Combined', { fit: 0.95, yaw: 2 }),
  desk: prop('town', 'SM_Prop_Desk_01', { fit: 1, stretch: true }),
  cabinet: prop('town', 'SM_Prop_Cabinet_01', { yaw: 2 }),
  workShelf: prop('apocalypse', 'SM_Prop_WorkShelf_01', { fit: 1, stretch: true, yaw: 1 }),
  // The wilds.
  tree: prop('nature', 'SM_Tree_01', { fit: 1.5 }),
  treePine: prop('nature', 'SM_Tree_Pine_01', { fit: 1.4 }),
  treeDead: prop('apocalypse', 'SM_Env_Tree_Dead_01', { fit: 1.4 }),
  streetTree: prop('apocalypse', 'SM_Env_Tree_01', { fit: 1.5 }),
  bush: prop('nature', 'SM_Plant_Bush_01', { fit: 0.9, thick: 0.6 }),
  rock: prop('nature', 'SM_Rock_01', { fit: 0.95 }),
  boulder: prop('nature', 'SM_Rock_Boulder_01', { fit: 0.95, thick: 1.4 }),
  log: prop('nature', 'SM_Tree_Log_01', { fit: 1, stretch: true, thick: 2.5 }),
} satisfies Record<string, PropAsset>;

export type PropId = keyof typeof PROPS;

/**
 * The Synty props each of the map's prop sets (props.ts) draws, one picked
 * per tile. A set missing here keeps its voxel prop. Low cover stands about
 * half a survivor tall, high cover over their head.
 */
export const PROP_SETS: Partial<Record<string, PropId[]>> = {
  cityLow: ['crates', 'barricade1', 'concreteBarrier', 'dumpster', 'barrels'],
  cityHigh: ['vending', 'barricadeTall', 'gasTank', 'cabinet'],
  wildsLow: ['rock', 'log', 'bush', 'rock'],
  wildsHigh: ['tree', 'treePine', 'treeDead', 'boulder'],
  facilityLow: ['crate', 'barricadePallet', 'generator'],
  facilityHigh: ['workShelf', 'cabinet', 'gasTank', 'workShelf'],
  console: ['desk'],
  carFront: ['carWreck', 'carRusted', 'carSuv', 'carOpen'],
  planter: ['planter'],
  bin: ['dumpster', 'rubbishBin'],
  streetTree: ['streetPot'],
  kiosk: ['vending'],
  fountain: ['concreteBarrier'],
  fountainCorner: ['concreteBarrier'],
  statue: ['statue'],
  fencePost: ['fencePost'],
  fenceRails: ['fenceWire'],
  fenceBoards: ['fencePanel'],
  gatePost: ['gatePost'],
  hayBale: ['hayBale'],
  hayStack: ['hayRound'],
  shelfLow: ['shelfLow'],
  shelfHigh: ['shelfHigh', 'bookshelf'],
  counter: ['counter', 'shopCounter'],
  table: ['table'],
  sofa: ['sofa', 'sofaDirty'],
  bed: ['bed'],
  desk: ['desk'],
  labBench: ['desk'],
  wardrobe: ['wardrobe'],
  cabinet: ['cabinet', 'bookshelf'],
  rack: ['workShelf'],
  crates: ['crates', 'crate'],
  techCrate: ['crate'],
  containers: ['barricadeTall', 'vending'],
  barricade: ['barricade1', 'barricade2', 'barricadePallet', 'sandbags'],
  sandbags: ['sandbags'],
  techBarrier: ['concreteBarrier'],
  pipe: ['pipes'],
  tank: ['gasTank'],
  pylon: ['cabinet'],
  tree: ['streetTree', 'tree'],
  forest: ['tree', 'treePine', 'tree'],
  bush: ['bush'],
  rock: ['rock'],
  spire: ['boulder'],
  logs: ['log'],
  rubble: ['rubble1', 'rubble2'],
  wallStub: ['rubbleWall'],
};

/** The objective the Cuervos call hordes with (relay): a loudspeaker. */
export const RELAY_PROP: PropId = 'loudSpeaker';
