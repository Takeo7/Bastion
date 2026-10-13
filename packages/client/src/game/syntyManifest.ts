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

export type PackId = 'apocalypse' | 'cityZombies' | 'bossZombies' | 'town' | 'farm' | 'nature' | 'city' | 'baseLocomotion' | 'alpine' | 'snow' | 'idles' | 'office' | 'particleFx' | 'construction' | 'military';

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
  // «ANIMATION – Base Locomotion» and «ANIMATION – Idles»: clips only, no atlas.
  baseLocomotion: { dir: 'SourceFiles-5' },
  idles: { dir: 'SourceFiles-4' },
  // The base (BASE_MODELS): «Nature Biomes – Alpine Mountain» and the snow kit.
  alpine: { dir: 'POLYGON_NatureBiomes_AlpineMountain_SourceFiles_v3', materialList: 'MaterialList_PNB_Alpine_Mountain.txt' },
  snow: { dir: 'SourceFiles-2', materialList: 'MaterialList_PolygonSnow.txt' },
  // «POLYGON – Office»: the radio's voices (strategy/portraits.ts).
  office: { dir: 'SourceFiles2' },
  // «POLYGON – Particle FX»: shapes and sprites for the effects (fx.ts, songFx.ts).
  particleFx: { dir: 'Source Files' },
  // «POLYGON – Construction» and «POLYGON – Military»: building sites and checkpoints on the map (Arte-Mapas).
  construction: { dir: 'POLYGON_Construction_SourceFiles_v5', materialList: 'MaterialList_PolygonConstruction.txt' },
  military: { dir: 'SourceFiles' },
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
  alpine: { pack: 'alpine', src: 'Textures/PolygonNatureBiomesS2_Alpine_Texture_01.png', albedo: 'PolygonNatureBiomesS2_Alpine_Texture_01.png', size: 512 },
  snow: { pack: 'snow', src: 'Textures/PolygonSnow_Texture_01_A.png', albedo: 'PolygonSnow_Texture_01_A.png', size: 512 },
  office: { pack: 'office', src: 'Textures/PolygonOffice_Texture_01_A.png', albedo: 'PolygonOffice_Texture_01_A', size: 1024 },
  construction: { pack: 'construction', src: 'Textures/PolygonConstruction_Texture_01_A.png', albedo: 'PolygonConstruction_Texture_01_A.png', size: 1024 },
  military: { pack: 'military', src: 'Textures/PolygonMilitary_Texture_01_A.png', albedo: 'PolygonMilitary_Texture_01_A.png', size: 1024 },
  particleFx: { pack: 'particleFx', src: 'Textures/PolygonParticles_Texture_01_A.png', albedo: 'PolygonParticles_Texture_01_A', size: 256 },
} satisfies Record<string, { pack: PackId; src: string; albedo: string; size: number }>;

export type AtlasId = keyof typeof ATLASES;

/** Kinds of converted model; a model's file is `<kind>-<id>.glb` in its pack's folder. */
export type ModelKind = 'chr' | 'hat' | 'wep' | 'prop' | 'anim' | 'piece' | 'base' | 'fx';

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
 * have smaller heads (hats shrink a little on them) and move with the
 * feminine animations. `scale` makes one bigger than the rest (brutes).
 */
export interface CharacterAsset {
  pack: PackId;
  fbx: string;
  mesh?: string;
  attach?: Attachment[];
  hair?: string[];
  female?: boolean;
  scale?: number;
}

/**
 * Characters stand this much taller than true scale (1.8 m would be 1.2
 * tiles): 1.35 tiles, so they read from the tactical camera. Props keep
 * their true size; weapons and hats grow with the hands and heads.
 */
export const CHARACTER_SCALE = 1.35 / 1.2;

const att = (fbx: string, name: string, dye = false, bone: SyntyBone = 'Head'): Attachment => ({ fbx: `SM_Chr_Attach_${fbx}`, bone, name, dye });
const apoc = (fbx: string, attach: Attachment[] = [], female = false): CharacterAsset => ({
  pack: 'apocalypse',
  fbx: `SK_Chr_${fbx}`,
  attach,
  hair: attach.filter((a) => a.name === 'hair').map((a) => a.name),
  female,
});
const zombie = (mesh: string, female = false): CharacterAsset => ({ pack: 'cityZombies', fbx: 'Zombies', mesh, female });
const office = (fbx: string, attach: Attachment[] = [], female = false): CharacterAsset => ({ pack: 'office', fbx: `SK_Chr_${fbx}`, attach, female });
// Headset_01 is a one-ear earpiece whose boom crosses the face at our scale; 02 is a proper radio headset.
const headset: Attachment = { fbx: 'SM_Chr_Attachment_Headset_02', bone: 'Head', name: 'headset' };

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
  eastern: apoc('Eastern_Female_01', [], true),
  islander: apoc('Islander_Male_01', [att('Islander_Male_Hair_01', 'hair')]),
  // Its glass visor comes drawn in another space and floats away: the hood alone reads fine.
  hazmat: apoc('Hazmat_Male_01'),
  nun: apoc('Nun_Female_01', [att('Nun_Female_HipPouch_01', 'pouch', false, 'Hips')], true),
  teenF: apoc('Teen_Female_01', [att('Teen_Female_Hair_01', 'hair'), att('Teen_Female_Glasses_01', 'glasses')], true),
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
  zRiotCop: { ...zombie('Zombie_RiotCop_Male_01'), scale: 1.06 },
  zMilitary: { ...zombie('Zombie_Military_Male_01'), scale: 1.06 },
  zPolice: { ...zombie('Zombie_Police_Male_01'), scale: 1.06 },
  zFirefighter: { ...zombie('Zombie_Firefighter_Male_01'), scale: 1.06 },
  // Chillón: the screamer, a head taller than the rest.
  zWretch: { pack: 'bossZombies', fbx: 'SK_Chr_ZombieBoss_Wretch_01', attach: [{ fbx: 'SM_Chr_Attach_Wretch_Hair_01', bone: 'Head', name: 'hair' }], scale: 1.12 },
  // Hinchado: the bloated one that bursts.
  zBlobber: { pack: 'bossZombies', fbx: 'SK_Chr_ZombieBoss_Blobber_01', attach: [{ fbx: 'SM_Chr_Attach_Blobber_Hair_01', bone: 'Head', name: 'hair' }], scale: 1.08 },
  // The radio's voices, only in their portraits (strategy/portraits.ts says who is who).
  voiceMorse: office('Boss_Female_01', [headset], true),
  voiceTeo: office('Boss_Male_01'),
  voiceNwosu: office('Business_Female_03', [], true),
  voiceVoz: office('Business_Female_04', [headset], true),
  voiceSimon: { pack: 'city', fbx: 'SK_Character_Male_Jacket' },
  voiceHalvorsen: { pack: 'city', fbx: 'SK_Character_BusinessWoman', female: true },
} satisfies Record<string, CharacterAsset>;

export type CharacterId = keyof typeof CHARACTERS;

/**
 * Who a soldier can be: the barracks' «Personaje» (Appearance.character).
 * `build` is the animation set they move with.
 */
export const SURVIVOR_CHOICES: { id: CharacterId; name: string; build: 'masc' | 'femn' }[] = [
  { id: 'soldierM', name: 'Soldado', build: 'masc' },
  { id: 'soldierF', name: 'Soldada', build: 'femn' },
  { id: 'hunter', name: 'Cazador', build: 'masc' },
  { id: 'sheriff', name: 'Sheriff', build: 'masc' },
  { id: 'riotCop', name: 'Antidisturbios', build: 'masc' },
  { id: 'mechanic', name: 'Mecánica', build: 'femn' },
  { id: 'scout', name: 'Exploradora', build: 'femn' },
  { id: 'wanderer', name: 'Trotamundos', build: 'masc' },
  { id: 'hazmat', name: 'Químico', build: 'masc' },
  { id: 'nun', name: 'Monja', build: 'femn' },
  { id: 'eastern', name: 'Forastera', build: 'femn' },
  { id: 'islander', name: 'Turista', build: 'masc' },
  { id: 'coolM', name: 'Urbano', build: 'masc' },
  { id: 'coolF', name: 'Urbana', build: 'femn' },
  { id: 'punk', name: 'Punki', build: 'femn' },
  { id: 'teenF', name: 'Estudiante', build: 'femn' },
];

/**
 * Survivors a soldier without a chosen «Personaje» is drawn from, by build;
 * the soldier's campaign id picks one, so it is the same one everywhere.
 */
export const SQUAD_BODIES: Record<BuildId, CharacterId[]> = {
  slim: ['scout', 'mechanic', 'coolF', 'punk', 'coolM', 'teenF', 'eastern'],
  normal: ['soldierM', 'soldierF', 'hunter', 'wanderer', 'mechanic', 'scout', 'sheriff', 'coolF', 'islander', 'nun'],
  heavy: ['soldierM', 'hunter', 'sheriff', 'riotCop', 'wanderer', 'hazmat'],
};

/** Who each enemy and objective is (for now the alien templates stand in for the dead). */
export const UNIT_BODIES: Partial<Record<TemplateId, CharacterId[]>> = {
  zombie: ['zMale', 'zFemale', 'zFather', 'zMother', 'zHobo', 'zShopKeeper', 'zGrandpa', 'zBusiness', 'zRoadworker', 'zPatient'],
  xenoid: ['zJock', 'zCheerleader', 'zHoodie', 'zSchoolGirl'],
  mec: ['zRiotCop', 'zMilitary', 'zPolice', 'zFirefighter'],
  sectoid: ['zWretch'],
  bloater: ['zBlobber'],
  trooper: ['criminal', 'teen', 'press'],
  officer: ['biker'],
  lancer: ['homeless', 'emo'],
  vip: ['nerd', 'business', 'waitress'],
};

/** Templates that move like the dead: hunched, arms out, no weapon. */
export const DEAD_TEMPLATES: ReadonlySet<TemplateId> = new Set<TemplateId>(['zombie', 'xenoid', 'mec', 'sectoid', 'bloater']);

// -------------------------------------------------------------- animations

/**
 * Clips from «ANIMATION – Base Locomotion», in place (no root motion). Each
 * converts to a GLB with the pack's skeleton and the clip; the game plays it
 * on a proxy of that skeleton and turns the pose onto each character's own
 * bones (both Synty rigs), measured against the neutral T-pose. `fbx` is
 * the path in the pack: the same names exist for the Sidekick rig.
 */
const loco = (path: string) => ({ pack: 'baseLocomotion' as PackId, fbx: `Animations/Polygon/${path}` });
const idle = (path: string) => ({ pack: 'idles' as PackId, fbx: `Animations/Polygon/${path}` });

export const ANIMATIONS = {
  tpose: loco('Neutral/Additive/TPose/A_TPose_Neut'),
  idleMasc: loco('Masculine/Idle/A_Idle_Standing_Masc'),
  idleFemn: loco('Feminine/Idle/A_Idle_Standing_Femn'),
  crouchIdleMasc: loco('Masculine/Idle/A_Idle_Crouching_Masc'),
  crouchIdleFemn: loco('Feminine/Idle/A_Idle_Crouching_Femn'),
  walkMasc: loco('Masculine/Locomotion/Walk/A_Walk_F_Masc'),
  walkFemn: loco('Feminine/Locomotion/Walk/A_Walk_F_Femn'),
  runMasc: loco('Masculine/Locomotion/Run/A_Run_F_Masc'),
  runFemn: loco('Feminine/Locomotion/Run/A_Run_F_Femn'),
  crouchWalkMasc: loco('Masculine/Locomotion/Crouch/A_Crouch_FwdStrafeF_Masc'),
  crouchWalkFemn: loco('Feminine/Locomotion/Crouch/A_Crouch_FwdStrafeF_Femn'),
  // «ANIMATION – Idles»: what a unit waiting does now and then.
  lookLMasc: idle('Masculine/Look/Actions/A_POLY_IDL_Look_L_Masc'),
  lookRMasc: idle('Masculine/Look/Actions/A_POLY_IDL_Look_R_Masc'),
  lookBehindMasc: idle('Masculine/Look/Actions/A_POLY_IDL_Look_L_Behind_Masc'),
  shiftLMasc: idle('Masculine/WeightShift/Actions/A_POLY_IDL_WeightShift_L_Masc'),
  shiftRMasc: idle('Masculine/WeightShift/Actions/A_POLY_IDL_WeightShift_R_Masc'),
  lookLFemn: idle('Feminine/Look/Actions/A_POLY_IDL_Look_L_Femn'),
  lookRFemn: idle('Feminine/Look/Actions/A_POLY_IDL_Look_R_Femn'),
  lookBehindFemn: idle('Feminine/Look/Actions/A_POLY_IDL_Look_R_Behind_Femn'),
  shiftLFemn: idle('Feminine/WeightShift/Actions/A_POLY_IDL_WeightShift_L_Femn'),
  shiftRFemn: idle('Feminine/WeightShift/Actions/A_POLY_IDL_WeightShift_R_Femn'),
} satisfies Record<string, { pack: PackId; fbx: string }>;

export type AnimationId = keyof typeof ANIMATIONS;

/** The clips a unit blends between; each character uses its build's set. */
export type Motion = 'idle' | 'crouchIdle' | 'walk' | 'run' | 'crouchWalk';

export const MOTIONS: Record<'masc' | 'femn', Record<Motion, AnimationId>> = {
  masc: { idle: 'idleMasc', crouchIdle: 'crouchIdleMasc', walk: 'walkMasc', run: 'runMasc', crouchWalk: 'crouchWalkMasc' },
  femn: { idle: 'idleFemn', crouchIdle: 'crouchIdleFemn', walk: 'walkFemn', run: 'runFemn', crouchWalk: 'crouchWalkFemn' },
};

/** Played once now and then instead of the idle while a unit stands waiting: a look round, a shift of weight. */
export const IDLE_VARIATIONS: Record<'masc' | 'femn', AnimationId[]> = {
  masc: ['lookLMasc', 'lookRMasc', 'lookBehindMasc', 'shiftLMasc', 'shiftRMasc'],
  femn: ['lookLFemn', 'lookRFemn', 'lookBehindFemn', 'shiftLFemn', 'shiftRFemn'],
};

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

// ----------------------------------------------------------------- effects

/**
 * Particle FX's shapes (fx.ts, songFx.ts): Synty builds its effects from
 * faceted puffs, chunks and flat polygon sprites, tinted by the effect, and
 * so do we. The meshes keep only their shape (each effect gives its colour);
 * the gore takes the pack's dark reds from its atlas.
 */
export const FX_MODELS = {
  smokePuff: { pack: 'particleFx', fbx: 'FX_Sphere_Puff_01' },
  goreChunk1: { pack: 'particleFx', fbx: 'SM_GoreChunk_01' },
  goreChunk2: { pack: 'particleFx', fbx: 'SM_GoreChunk_02' },
  goreChunk3: { pack: 'particleFx', fbx: 'SM_GoreChunk_04' },
  flame: { pack: 'particleFx', fbx: 'SM_Flame_FX' },
  medCross: { pack: 'particleFx', fbx: 'FX_Healthcube_01' },
  woodChip: { pack: 'particleFx', fbx: 'FX_WoodChip_01' },
  // Apocalypse's molotov, upright, rag at the neck: thrown by the Oficio (songFx.ts) and the interface's icon.
  // (Its path: the pack has a second copy among the vehicle weapons.)
  molotov: { pack: 'apocalypse', fbx: 'FBX/SM_Wep_Molotov_01' },
} satisfies Record<string, { pack: PackId; fbx: string }>;

export type FxModelId = keyof typeof FX_MODELS;

/** Particle FX's sprites, white on transparent, tinted by the effect (WebP with alpha; `size` caps the width). */
export const FX_TEXTURES = {
  polyDisc: { src: 'Textures/PolygonParticles_Circle_01.png', size: 128 },
  sparkle: { src: 'Textures/PolygonParticles_Sparkle.png', size: 256 },
  shockRing: { src: 'Textures/PolygonParticles_Ring_02.png', size: 256 },
  groundCrack: { src: 'Textures/PolygonParticles_GroundBreak.png', size: 512 },
  lightning: { src: 'Textures/PolygonParticles_Lightning_02.png', size: 256 },
} satisfies Record<string, { src: string; size: number }>;

export type FxTextureId = keyof typeof FX_TEXTURES;

export const fxTextureFile = (id: FxTextureId) => syntyFile('particleFx', `${id}.webp`);

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
  /** Stands on its own origin, not its middle: a pole with an arm, a power line from its pole. */
  origin?: boolean;
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
  // On roofs (buildings.ts): drawn at the kit's scale (0.8 tiles a metre).
  roofAircon: prop('city', 'SM_Prop_Roof_Aircon_01', { fit: 0.9 }),
  roofAirconLong: prop('city', 'SM_Prop_Roof_Aircon_02', { fit: 1.6 }),
  roofVent: prop('town', 'SM_Prop_Airvent_01', { fit: 0.65 }),
  roofSkylight: prop('town', 'SM_Bld_Skylight_01', { fit: 1.3 }),
  roofAccess: prop('city', 'SM_Bld_Roof_Access_01', { fit: 2.3 }),
  roofDish: prop('apocalypse', 'SM_Prop_Roof_Satellite_Dish_01', { fit: 0.75 }),
  chimney: prop('town', 'SM_Bld_Chimney_01', { fit: 0.8 }),
  // Ground cover (ground.ts): cosmetic, scattered off the grid.
  tuft: prop('apocalypse', 'SM_Env_Grass_Tuft_01', { fit: 0.3 }),
  tuftLow: prop('apocalypse', 'SM_Env_Grass_Tuft_02', { fit: 0.3 }),
  tuftWide: prop('apocalypse', 'SM_Env_Grass_Tuft_03', { fit: 0.45 }),
  flowers: prop('apocalypse', 'SM_Env_Flowers_02', { fit: 0.6 }),
  pebbles: prop('apocalypse', 'SM_Env_Rubble_Pebbles_02', { fit: 0.6 }),
  leaves: prop('apocalypse', 'SM_Env_GroundLeaves_02', { fit: 1.5 }),
  grassBlob: prop('apocalypse', 'SM_Env_GrassBlob_01', { fit: 0.9 }),
  papers: prop('apocalypse', 'SM_Prop_Papers_01', { fit: 0.7 }),
  trash: prop('apocalypse', 'SM_Prop_Trash_02', { fit: 1 }),
  manhole: prop('town', 'SM_Prop_Manhole_01', { fit: 0.75 }),
  drain: prop('town', 'SM_Prop_Drain_01', { fit: 0.5 }),
  // Searched furniture (mapView.ts): opened, and its contents on the floor.
  crateOpen: prop('apocalypse', 'SM_Prop_Crate_Open_01'),
  crateLid: prop('apocalypse', 'SM_Prop_Crate_Open_Lid_01'),
  drawer: prop('apocalypse', 'SM_Prop_Dresser_03_Drawer_01', { fit: 0.45 }),
  laundry: prop('town', 'SM_Prop_Laundry_Pile_01', { fit: 0.4 }),
  cardboard: prop('apocalypse', 'SM_Prop_CardboardBox_01', { fit: 0.45 }),
  /** Round the sides of hills (buildings.ts stretches it to a storey). */
  cliff: prop('nature', 'SM_Rock_Wall_01', { fit: 1.4 }),
  cliff2: prop('nature', 'SM_Rock_Wall_02', { fit: 1.4 }),
  // Dressing (dressing.ts): what makes a street look lived in and left, as in Synty's captures.
  // Low cover too: piles of rubbish, junk and tyres (cityLow).
  trashPile: prop('apocalypse', 'SM_Prop_TrashPile_02', { fit: 1.05 }),
  trashPile2: prop('apocalypse', 'SM_Prop_TrashPile_03', { fit: 1.05 }),
  junkPile: prop('apocalypse', 'SM_Prop_JunkPile_01', { fit: 1 }),
  tirePile: prop('apocalypse', 'SM_Prop_Tire_Pile_01', { fit: 1 }),
  // Street furniture: slim, at the kerb's corners.
  streetLight: prop('apocalypse', 'SM_Prop_LightPole_Base_01', { origin: true }),
  powerPole: prop('apocalypse', 'SM_Prop_Powerpole_Single_01', { origin: true }),
  powerLine: prop('apocalypse', 'SM_Prop_Powerpole_Single_Line_01', { origin: true }),
  hydrant: prop('apocalypse', 'SM_Prop_Hydrant_01'),
  mailbox: prop('apocalypse', 'SM_Prop_Mailbox_01'),
  stopSign: prop('apocalypse', 'SM_Prop_Sign_Stop_01', { origin: true }),
  cone: prop('apocalypse', 'SM_Prop_Cone_01'),
  // Lying about: flat enough not to read as cover.
  trashBag: prop('apocalypse', 'SM_Prop_TrashBag_01'),
  trashBag2: prop('apocalypse', 'SM_Prop_TrashBag_02'),
  trashBag3: prop('apocalypse', 'SM_Prop_TrashBag_04'),
  tire: prop('apocalypse', 'SM_Prop_Tire_01'),
  pallet: prop('apocalypse', 'SM_Prop_Pallet_01'),
  bike: prop('apocalypse', 'SM_Prop_Bike_Broken_01'),
  // On shop fronts, over the door.
  signShop: prop('apocalypse', 'SM_Prop_Sign_Shop_01'),
  signFood: prop('apocalypse', 'SM_Prop_Sign_Food_01'),
  signMarket: prop('apocalypse', 'SM_Prop_Sign_Market_01'),
  signDiner: prop('apocalypse', 'SM_Prop_Sign_Diner_01'),
  signPets: prop('apocalypse', 'SM_Prop_Sign_Pets_01'),
  // Offices (POLYGON Office): desks, a chair at each, filing cabinets.
  officeDesk: prop('office', 'SM_Prop_Desk_01', { fit: 0.95 }),
  officeDesk2: prop('office', 'SM_Prop_Desk_03', { fit: 0.95 }),
  officeChair: prop('office', 'SM_Prop_Chair_01'),
  officeCabinet: prop('office', 'SM_Prop_Cabinets_01'),
  // Building sites (POLYGON Construction): barriers, stacks of bricks and pallets, sacks of cement, a portaloo.
  siteBarrier: prop('construction', 'SM_Prop_Barrier_Plastic_01', { fit: 1, stretch: true }),
  brickStack: prop('construction', 'SM_Prop_Brick_Stack_01', { fit: 0.95 }),
  palletStack: prop('construction', 'SM_Prop_PalletStack_01', { fit: 0.95 }),
  cementBags: prop('construction', 'SM_Prop_ConcreteBag_Stack_01', { fit: 0.9 }),
  portaloo: prop('construction', 'SM_Prop_Portaloo_01', { fit: 0.95 }),
  // Checkpoints (POLYGON Military): concrete barriers, tall ones, ammunition crates.
  milBarrier: prop('military', 'SM_Prop_Barrier_01', { fit: 1, stretch: true }),
  milBarrierTall: prop('military', 'SM_Prop_Barrier_Tall_01', { fit: 1 }),
  milCrate: prop('military', 'SM_Prop_Crate_Cube_01', { fit: 0.8 }),
  // A filling station's pumps (town `tank` hints), a motel's sign by the road, billboards on roofs.
  pump: prop('apocalypse', 'SM_Prop_Gaspump_01'),
  pump2: prop('apocalypse', 'SM_Prop_Gaspump_02'),
  motelSign: prop('apocalypse', 'SM_Bld_Motel_Sign_01', { origin: true }),
  billboard: prop('apocalypse', 'SM_Prop_Billboard_Roof_01'),
  billboardSign: prop('apocalypse', 'SM_Prop_Billboard_Sign_01'),
  billboardSign2: prop('apocalypse', 'SM_Prop_Billboard_Sign_03'),
  // Beyond the map's edge, where nobody walks: the city goes on.
  bus: prop('apocalypse', 'SM_Prop_Car_Wrecked_Bus_01'),
  van: prop('apocalypse', 'SM_Prop_Car_Wrecked_Van_01'),
} satisfies Record<string, PropAsset>;

export type PropId = keyof typeof PROPS;

/**
 * The Synty props each of the map's prop sets draws (mapView.ts), one picked
 * per tile. Low cover stands about half a survivor tall, high cover over
 * their head.
 */
export const PROP_SETS = {
  cityLow: ['crates', 'barricade1', 'concreteBarrier', 'dumpster', 'barrels', 'trashPile', 'junkPile', 'trashPile2', 'tirePile', 'brickStack', 'siteBarrier'],
  cityHigh: ['vending', 'barricadeTall', 'gasTank', 'cabinet', 'portaloo', 'milBarrierTall'],
  wildsLow: ['rock', 'log', 'bush', 'rock'],
  wildsHigh: ['tree', 'treePine', 'treeDead', 'boulder'],
  facilityLow: ['crate', 'barricadePallet', 'generator', 'milBarrier', 'milCrate'],
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
  desk: ['desk', 'officeDesk', 'officeDesk2'],
  labBench: ['officeDesk2'],
  wardrobe: ['wardrobe'],
  cabinet: ['cabinet', 'bookshelf', 'officeCabinet'],
  rack: ['workShelf'],
  crates: ['crates', 'crate', 'palletStack', 'cementBags'],
  techCrate: ['crate'],
  containers: ['barricadeTall', 'vending', 'milBarrierTall'],
  barricade: ['barricade1', 'barricade2', 'barricadePallet', 'sandbags', 'milBarrier', 'siteBarrier'],
  sandbags: ['sandbags'],
  techBarrier: ['concreteBarrier', 'milBarrier'],
  pipe: ['pipes'],
  tank: ['gasTank'],
  pump: ['pump', 'pump2'],
  portaloo: ['portaloo'],
  pylon: ['cabinet'],
  tree: ['streetTree', 'tree'],
  forest: ['tree', 'treePine', 'tree'],
  bush: ['bush'],
  rock: ['rock'],
  spire: ['boulder'],
  logs: ['log'],
  rubble: ['rubble1', 'rubble2'],
  wallStub: ['rubbleWall'],
} satisfies Record<string, PropId[]>;

/** A set of props the map picks from for a tile of cover (mapView.ts). */
export type PropSet = keyof typeof PROP_SETS;

/** The objective the Cuervos call hordes with (relay): a loudspeaker. */
export const RELAY_PROP: PropId = 'loudSpeaker';

// --------------------------------------------------------------- buildings

/**
 * Synty's modular kits are drawn on a 2.5 m grid and a tile is 1.5 m, so the
 * map draws building pieces this much bigger than SYNTY_SCALE: a module is
 * then two tiles wide, a wall one storey tall (STOREY, 3.5 m) and a door
 * 1.4 m wide, about a survivor's 2 m. Each wall tile takes the middle half of
 * a module (KIT_TILE metres): a door or window module puts its opening right
 * in the tile where the rules have it, wherever that is along the wall.
 */
export const KIT_SCALE = 1.2;
export const KIT_TILE = 1.5 / KIT_SCALE;

/**
 * A piece of a modular building kit. The converter keeps the box `keep` of
 * the FBX (metres and axes as in the FBX, Z up; none: all of it), keeps its
 * meshes apart with `parts` (`leaf` is the one that swings open) and leaves
 * out the meshes named in `drop` (glass: the map draws its own, so it can
 * break). `centre` is the middle of the module in the FBX, along its run (x,
 * or y with `run: 'y'`) and across it (the wall's middle plane); `out` the
 * way across the run its outer face looks (Town: −y). `unit` is metres per
 * unit of the FBX (City's come in hundreds of metres: 100) and `storey` the
 * kit's floor-to-ceiling height, which the game stretches to one STOREY. The
 * rest (turning it to its side of the building, KIT_SCALE) is done in the game.
 */
export interface PieceAsset {
  pack: PackId;
  fbx: string;
  centre: [number, number];
  run?: 'x' | 'y';
  out?: 1 | -1;
  unit?: number;
  storey?: number;
  /** Height of the piece's foot in the FBX, when it isn't 0 (a door leaf modelled round its middle). */
  base?: number;
  keep?: [[number, number, number], [number, number, number]];
  parts?: boolean;
  leaf?: string;
  drop?: string[];
}

interface PieceOptions {
  run?: 'x' | 'y';
  out?: 1 | -1;
  unit?: number;
  storey?: number;
  base?: number;
  /** Keep only this much of the module across its run, from its middle plane out (and as much in): the outer face alone. */
  depth?: number;
  /** And nothing above this height (FBX units): a lid. */
  top?: number;
  /** Keep only what is above this height (metres; the foundation below the floor goes). */
  floor?: number;
  parts?: boolean;
  leaf?: string;
  drop?: string[];
}

/** A piece cut to one tile of its module: KIT_TILE along the run about `centre`, from `floor` up. */
function strip(pack: PackId, fbx: string, centre: [number, number], o: PieceOptions = {}): PieceAsset {
  const along = o.run === 'y' ? 1 : 0;
  const unit = o.unit ?? 1;
  const lo: [number, number, number] = [-50, -50, o.floor ?? -0.02 / unit];
  const hi: [number, number, number] = [50, 50, o.top ?? 50];
  lo[along] = centre[0] - KIT_TILE / 2 / unit;
  hi[along] = centre[0] + KIT_TILE / 2 / unit;
  if (o.depth !== undefined) {
    lo[1 - along] = centre[1] - o.depth;
    hi[1 - along] = centre[1] + o.depth;
  }
  const { depth: _depth, top: _top, ...rest } = o;
  return whole(pack, fbx, centre, { ...rest, keep: [lo, hi] });
}

/** A piece kept whole (a door, a window, a corner post), or cut to `keep`. */
function whole(pack: PackId, fbx: string, centre: [number, number], o: PieceOptions & { keep?: PieceAsset['keep'] } = {}): PieceAsset {
  const { floor, ...rest } = o;
  const keep = rest.keep ?? (floor !== undefined ? [[-50, -50, floor], [50, 50, 50]] : undefined);
  return { pack, fbx, centre, run: o.run ?? 'x', out: o.out ?? -1, ...rest, keep };
}

/**
 * City's apartment blocks: a 5 m cube with its windows on the −y face and plain brick on the −x face, each
 * cut to the tile-wide strip in the middle of that face (`run`: x the windows, y the plain side).
 */
const apartment = (fbx: string, run: 'x' | 'y') =>
  strip('city', fbx, [0.025, 0], { run, unit: 100, storey: 3, depth: 0.004, top: 0.0302, floor: -0.0002 });

/** Apocalypse's bunker kit: modules from y = 0 to 2.5, walls from x = −0.125 to 0.125. */
const bunker = (fbx: string, o: PieceOptions = {}) => strip('apocalypse', fbx, [1.25, 0], { run: 'y', out: 1, storey: 3, ...o });

/** Town's house kit: modules from x = 0 to 2.5, walls from y = −0.2 to 0, siding on the −y face. */
const house = (fbx: string, { at = 1.25, ...o }: PieceOptions & { at?: number } = {}) => strip('town', `SM_Bld_House_${fbx}`, [at, -0.1], o);

export const PIECES = {
  // Houses: siding outside, painted plaster inside.
  houseWall: house('ExteriorWall_GroundFloor_01'),
  houseDoorWall: house('ExteriorWall_GroundFloor_Door_01'),
  houseWindowWall: house('ExteriorWall_GroundFloor_Window_01', { at: 3.75 }),
  /** Over an opening with no door (a loading bay). */
  houseLintel: house('ExteriorWall_GroundFloor_01', { floor: 2.35 }),
  houseCorner: whole('town', 'SM_Bld_House_ExteriorWall_GroundFloor_Corner_01', [-0.12, -0.12], { floor: -0.02 }),
  houseDoor: whole('town', 'SM_Bld_House_Door_01', [1.25, -0.1], { parts: true, leaf: 'SM_Bld_House_Door' }),
  houseWindow: whole('town', 'SM_Bld_House_Window_01', [1.25, -0.1], { drop: ['Glass'] }),
  // Walls between rooms.
  roomWall: house('InteriorWall_01'),
  roomDoorWall: house('InteriorWall_Door_01', { parts: true, leaf: 'SM_Bld_House_InteriorWall_Door_02' }),
  roomLintel: house('InteriorWall_01', { floor: 2.35 }),
  // Raised blocks (flats): City's brick apartments, one window a tile. Their FBX are in hundreds of metres,
  // modules 5 m wide and 3 m a storey; only the outer face is kept (the block is solid).
  blockWindow: apartment('SM_Bld_Apartment_01', 'x'),
  blockPlain: apartment('SM_Bld_Apartment_01', 'y'),
  /** The top storey: arched windows under a cornice. */
  blockTopWindow: apartment('SM_Bld_Apartment_02', 'x'),
  blockTopPlain: apartment('SM_Bld_Apartment_02', 'y'),
  /** Up the side of a raised block or a hill (3 m, stretched to a storey). */
  ladder: whole('apocalypse', 'SM_Prop_Ladder_01', [0, 0.035], { run: 'y', out: 1, storey: 3 }),
  // Labs and the walls of warehouses (Apocalypse's bunker kit: 2.5 m modules along y, 3 m tall, concrete both sides).
  bunkerWall: bunker('SM_Bld_Bunker_Wall_Concrete_x1_01'),
  /** Over a door. */
  bunkerLintel: bunker('SM_Bld_Bunker_Wall_Concrete_x1_01', { floor: 2.1 }),
  /** A reinforced window: a steel frame round an opening above a sill. */
  bunkerWindow: bunker('SM_Bld_Bunker_Wall_Metal_Door_x1_01'),
  metalDoor: whole('apocalypse', 'SM_Bld_Metal_Door_01', [0.47, 0], { storey: 3, base: -1.07, parts: true, leaf: 'SM_Bld_Metal_Door_01' }),
  /** Warehouses: the brick of the Apocalypse warehouse, a skin outside a bunker wall (its panels have one face). */
  brickFace: strip('apocalypse', 'SM_Bld_Warehouse_Brick_01', [-1.83, -4.915], { storey: 3, depth: 0.3, top: 2.95 }),
  brickLintel: strip('apocalypse', 'SM_Bld_Warehouse_Brick_01', [-1.83, -4.915], { storey: 3, depth: 0.3, top: 2.95, floor: 2.1 }),
  // A facility's perimeter: the quarantine wall (5 m panels, cut to a storey), its columns at gates and corners.
  fenceWall: strip('apocalypse', 'SM_Prop_Wall_Quarantine_Bottom_01', [-2.5, -0.47], { storey: 3, top: 2.95 }),
  fenceColumn: whole('apocalypse', 'SM_Prop_Wall_Quarantine_Column_Tall_01', [0, 0], { storey: 3, keep: [[-5, -5, -0.02], [5, 5, 3.4]] }),
  barbedWire: whole('apocalypse', 'SM_Prop_Wall_Quarantine_BarbedWire_01', [0, 0], { storey: 3 }),
  /** Loading platforms: shipping-container sides. */
  containerWall: bunker('SM_Bld_Bunker_Wall_Container_x1_01'),
  /** A wardrobe someone has searched: its doors and drawers apart, to open them (mapView.ts). */
  wardrobeOpen: whole('town', 'SM_Prop_Wardrobe_01', [0, 0.03], { parts: true, storey: 2.9 }),
} satisfies Record<string, PieceAsset>;

export type PieceId = keyof typeof PIECES;

// ----------------------------------------------------------------- terrain

/**
 * Tiling ground textures (ground.ts): asphalt from Town's roads, grass and
 * dirt from Nature Biomes – Alpine Mountain. `src` is under assets-src/synty/;
 * each is converted to WebP at `size` in public/models/synty/terrain/.
 */
export const TERRAIN = {
  asphalt: { src: 'POLYGON_Town_SourceFiles_v7/Textures/Road_01.png', size: 512 },
  grass: { src: 'POLYGON_NatureBiomes_AlpineMountain_SourceFiles_v3/Textures/Grass_Texture_01.png', size: 1024 },
  dirt: { src: 'POLYGON_NatureBiomes_AlpineMountain_SourceFiles_v3/Textures/Dirt_Texture_01.png', size: 1024 },
} satisfies Record<string, { src: string; size: number }>;

export type TerrainId = keyof typeof TERRAIN;

export const terrainFile = (id: TerrainId) => `models/synty/terrain/${id}.webp`;

// -------------------------------------------------------------------- base

/**
 * The Bastion on the surface (strategy/bastion.ts): Atalaya, a civil-defence
 * station in the snow. Its buildings and props, the pines, rocks and
 * mountains round it, the hangar and La Mula. Each converts whole, like a
 * building piece, to `base-<id>.glb`: `parts` keeps its meshes apart (the
 * bus's doors and wheels move), `drop` leaves some out. The base sizes,
 * turns and places them itself, so changing that needs no reconversion.
 * `fbx` is a path in the pack: Apocalypse keeps a collision twin of many
 * buildings in FBX/Collision under the same name, which a bare name may find.
 */
export interface BaseAsset {
  pack: PackId;
  fbx: string;
  parts?: boolean;
  drop?: string[];
}

const apocalypseAt = (name: string): BaseAsset => ({ pack: 'apocalypse', fbx: `FBX/${name}` });
const at = (pack: PackId, fbx: string, o: Omit<BaseAsset, 'pack' | 'fbx'> = {}): BaseAsset => ({ pack, fbx: `FBX/${fbx}`, ...o });

export const BASE_MODELS = {
  // The bunker and the command post round it.
  bunker: apocalypseAt('SM_Bld_Bunker_Entrance_01'),
  hatch: apocalypseAt('SM_Bld_Bunker_Hatch_01'),
  mast: apocalypseAt('SM_Prop_Trailer_Aerial_03'),
  aerial: apocalypseAt('SM_Prop_Trailer_Aerial_01'),
  dish: apocalypseAt('SM_Prop_Trailer_Dish_01'),
  floodlights: apocalypseAt('SM_Prop_Floodlights_01'),
  sandbagWall: apocalypseAt('SM_Prop_Sandbag_Wall_02'),
  sandbagArc: apocalypseAt('SM_Prop_Sandbag_Wall_03'),
  // Buildings and shelters.
  leanTo: at('farm', 'SM_Bld_Shelter_01'),
  trailer: apocalypseAt('SM_Bld_Trailer_01'),
  brickHut: apocalypseAt('SM_Bld_Industrial_Small_01'),
  armyTent: apocalypseAt('SM_Bld_Military_Tent_01'),
  openTent: apocalypseAt('SM_Bld_Military_Tent_02'),
  fieldTent: apocalypseAt('SM_Prop_Tent_Long_01'),
  quarantineTent: apocalypseAt('SM_Bld_Quarantine_Tent_01'),
  shipContainer: apocalypseAt('SM_Prop_Shipping_Container_02'),
  garageShed: at('town', 'SM_Bld_House_Preset_Garage_01'),
  gardenShed: at('town', 'SM_Bld_GardenShed_01'),
  radioTower: apocalypseAt('SM_Bld_RadioTower_01'),
  radioTowerTop: apocalypseAt('SM_Bld_RadioTower_Top_01'),
  shackFrame: apocalypseAt('SM_Bld_Junk_Shelter_01'),
  shackTarp: apocalypseAt('SM_Bld_Junk_Shelter_01_Tarp'),
  climbFrame: apocalypseAt('SM_Bld_Junk_Shelter_02'),
  waterTower: apocalypseAt('SM_Bld_WaterTower_01'),
  lookout: at('alpine', 'Props/SM_Prop_Lookout_01'),
  // Furniture, tools and stores.
  bunkBed: apocalypseAt('SM_Prop_Bed_Bunk_01'),
  firePit: apocalypseAt('SM_Prop_FirePit_01'),
  washingLine: apocalypseAt('SM_Prop_WashingLine_01'),
  campChair: apocalypseAt('SM_Prop_Camp_Chair_01'),
  foldingChair: apocalypseAt('SM_Prop_Chair_Folding_01'),
  longTable: apocalypseAt('SM_Prop_Table_02'),
  blackboard: apocalypseAt('SM_Prop_Blackboard_01'),
  radioSet: apocalypseAt('SM_Prop_Radio_01'),
  solarPanel: apocalypseAt('SM_Prop_Solar_Panel_01'),
  powerBoxes: apocalypseAt('SM_Prop_PowerBoxes_01'),
  transformer: apocalypseAt('SM_Prop_Transformer_01'),
  powerGenerator: apocalypseAt('SM_Prop_Generator_01'),
  workbench: apocalypseAt('SM_Prop_Workbench_01'),
  vice: apocalypseAt('SM_Prop_Vice_01'),
  anvil: apocalypseAt('SM_Prop_Anvil_01'),
  toolbox: apocalypseAt('SM_Prop_ToolBox_01'),
  toolShelf: apocalypseAt('SM_Prop_WorkShelf_01'),
  fuelTable: apocalypseAt('SM_Prop_Gas_Table_01'),
  batteryTable: apocalypseAt('SM_Prop_CarBattery_Table_01'),
  tyres: apocalypseAt('SM_Prop_Tire_Pile_01'),
  oilDrum: apocalypseAt('SM_Prop_Barrel_01'),
  drums: apocalypseAt('SM_Prop_BarrelStack_02'),
  jerrycan: apocalypseAt('SM_Prop_GasCan_01'),
  supplyCrate: apocalypseAt('SM_Prop_Crate_01'),
  bigCrate: apocalypseAt('SM_Prop_Crate_Large_02'),
  ammoCrate: apocalypseAt('SM_Prop_Ammo_Box_01'),
  fuelPump: apocalypseAt('SM_Prop_Gaspump_01'),
  rustyCar: apocalypseAt('SM_Prop_Car_Wrecked_Rusted_01'),
  // The infirmary.
  ivStand: apocalypseAt('SM_Prop_Medical_IVStand_01'),
  medShelf: apocalypseAt('SM_Prop_Medical_Shelf_01'),
  medKit: apocalypseAt('SM_Prop_MedicalBox_02'),
  deconShower: apocalypseAt('SM_Prop_Decontamination_Shower_01'),
  hospitalBed: apocalypseAt('Rigged/SK_Prop_Bed_Hospital_01'),
  gurney: apocalypseAt('Rigged/SK_Prop_Bed_Gurney_01'),
  // The range, the training ground and the craft shed.
  target: apocalypseAt('SM_Prop_Target_01'),
  targetBoard: apocalypseAt('SM_Prop_Target_02'),
  bottleRange: apocalypseAt('SM_Prop_ShootingRange_01'),
  tyreWall: apocalypseAt('SM_Prop_TireWall_01'),
  balanceBeam: apocalypseAt('SM_Prop_Plank_Bench_01'),
  woodPile: at('alpine', 'Props/SM_Prop_Wood_Pile_01'),
  bench: at('alpine', 'Props/SM_Prop_Bench_01'),
  // The Camposanto.
  cross: apocalypseAt('SM_Prop_Cross_01'),
  stoneCross: apocalypseAt('SM_Prop_Cross_02'),
  graveStick: apocalypseAt('SM_Prop_GraveStick_01'),
  picketFence: apocalypseAt('SM_Prop_Fence_Wood_Straight_01'),
  // Works: building, raising a level, clearing a plot.
  roadCone: apocalypseAt('SM_Prop_Cone_01'),
  // Ruins of the old station.
  brokenWall: apocalypseAt('SM_Prop_Wall_Brick_Collasped_01'),
  brickPile: apocalypseAt('SM_Prop_Wall_Brick_Pile_01'),
  rubbleHeap: apocalypseAt('SM_Prop_Rubble_Concrete_01'),
  burntHeap: apocalypseAt('SM_Prop_BurnPile_01'),
  junkHeap: apocalypseAt('SM_Prop_TrashPile_01'),
  // The mountain: pines, rocks, drifts and the range behind.
  pine: at('nature', 'SM_Tree_PolyPine_01'),
  pineSmall: at('nature', 'SM_Tree_PolyPine_02'),
  pineTall: at('nature', 'SM_Tree_PolyPine_Sparse_01'),
  pineDead: at('nature', 'SM_Tree_Pine_Dead_01'),
  stump: at('nature', 'SM_Tree_Stump_01'),
  crag: at('alpine', 'Environment/SM_Env_Rock_01'),
  crag2: at('alpine', 'Environment/SM_Env_Rock_03'),
  crag3: at('alpine', 'Environment/SM_Env_Rock_05'),
  crag4: at('alpine', 'Environment/SM_Env_Rock_07'),
  stone: at('alpine', 'Environment/SM_Env_Rock_Small_01'),
  drift: at('alpine', 'Environment/SM_Env_Snow_Mound_01'),
  range1: at('alpine', 'Environment/SM_Env_MountainRange_01'),
  range2: at('alpine', 'Environment/SM_Env_MountainRange_02'),
  range3: at('alpine', 'Environment/SM_Env_MountainRange_03'),
  range4: at('alpine', 'Environment/SM_Env_MountainRange_04'),
  // The perimeter, the road and the valley.
  jersey: apocalypseAt('SM_Prop_Barrier_Long_01'),
  boomPost: apocalypseAt('SM_Prop_Road_Barrier_01'),
  boomArm: apocalypseAt('SM_Prop_Road_Barrier_Arm_01'),
  lampPost: apocalypseAt('SM_Prop_LightPole_01'),
  pylon: apocalypseAt('SM_Bld_Pylon_01'),
  boardwalk: apocalypseAt('SM_Prop_WoodSheet_01'),
  // The hangar and La Mula: a city bus plated with sheet metal.
  hangarWall: apocalypseAt('SM_Bld_Bunker_Wall_Concrete_x2_01'),
  hangarPillar: apocalypseAt('SM_Bld_Bunker_Beam_Concrete_01'),
  hangarRoof: apocalypseAt('SM_Bld_Bunker_Ceiling_Corrugated_01'),
  hangarGate: apocalypseAt('SM_Bld_Warehouse_Concrete_Door_01'),
  wallLamp: apocalypseAt('SM_Prop_Light_Industrial_Wall_01'),
  mulaBus: at('town', 'SM_Veh_Bus_01', { parts: true }),
  plateSlit: apocalypseAt('SM_Veh_Attach_Plating_10'),
  plate: apocalypseAt('SM_Veh_Attach_Plating_01'),
  plateRoof: apocalypseAt('SM_Veh_Attach_Plating_05'),
  snowmobile: at('snow', 'SM_Veh_Snowmobile_01'),
  sled: at('snow', 'SM_Prop_Sled_01'),
  // Building sites, the works brigade and the trades workshop (POLYGON Construction).
  siteDozer: at('construction', 'SM_Veh_Bulldozer_01'),
  siteSpoil: at('construction', 'SM_Env_Dirt_Pile_03'),
  siteBarricade: at('construction', 'SM_Prop_Barrier_Plastic_01'),
  siteLamp: at('construction', 'SM_Prop_Light_Portable_01'),
  siteScaffold: at('construction', 'SM_Prop_Scaffold_02'),
  siteScaffoldFull: at('construction', 'SM_Prop_Scaffold_Preset_01'),
  siteMixer: at('construction', 'SM_Prop_Concrete_Mixer_01'),
  sitePortaloo: at('construction', 'SM_Prop_Portaloo_01'),
  siteBricks: at('construction', 'SM_Prop_Brick_Stack_01'),
  sitePallets: at('construction', 'SM_Prop_PalletStack_01'),
  siteCement: at('construction', 'SM_Prop_ConcreteBag_Stack_01'),
  sitePlanks: at('construction', 'SM_Prop_Plank_Stack_01'),
  siteRebar: at('construction', 'SM_Bld_ConcreteRebar_Pillar_01'),
  siteSlab: at('construction', 'SM_Bld_Concrete_Floor_01'),
  siteOffice: at('construction', 'SM_Bld_Portable_Office_01'),
  siteBarrow: at('construction', 'SM_Prop_Wheelbarrow_01'),
  sawhorse: at('construction', 'SM_Prop_Saw_Horse_01'),
  trestle: at('construction', 'SM_Prop_Trestle_Table_01'),
  tableSaw: at('construction', 'SM_Prop_Table_Saw_01'),
  plankRack: at('construction', 'SM_Prop_StorageShelf_Plank_01'),
  woodPallet: at('construction', 'SM_Prop_Pallet_01'),
  tyreStack: at('construction', 'SM_Prop_TireStack_01'),
  // The Huerto: raised beds and rain barrels, then a greenhouse (its glass tinted apart), then drip lines, seed trays and a scarecrow.
  gardenBed: at('farm', 'SM_Prop_PlantBox_Large_01'),
  gardenBarrel: apocalypseAt('SM_Prop_Barrel_Water_Open_01'),
  gardenBarrelShut: apocalypseAt('SM_Prop_Barrel_Water_01'),
  gardenLettuce: at('farm', 'SM_Prop_Lettuce_01_L'),
  gardenCabbage: at('farm', 'SM_Prop_Cabbage_01_L'),
  gardenPlant: at('farm', 'SM_Prop_Plant_Ground_01_L'),
  gardenPlant2: at('farm', 'SM_Prop_Plant_Ground_02_L'),
  gardenCan: at('farm', 'SM_Prop_Watering_Can_01'),
  greenhouse: at('farm', 'SM_Bld_Greenhouse_01', { parts: true }),
  gardenHose: at('farm', 'SM_Prop_Sprinkler_Hose_01'),
  gardenTable: at('farm', 'SM_Prop_Table_01'),
  gardenSeedlings: at('farm', 'SM_Env_Vege_Rows_02'),
  gardenSeeds: at('farm', 'SM_Prop_SeedPacket_01'),
  scarecrow: at('farm', 'SM_Prop_Scarecrow_01'),
  // The valley of Valdesombra (strategy/geoscape.ts), in miniature: farms, villages, the dead cities, the power station, clouds.
  mapBarn: at('farm', 'SM_Bld_Barn_01'),
  mapSilo: at('farm', 'SM_Bld_Silo_01'),
  mapCrops: at('farm', 'SM_Env_Vege_Rows_01'),
  mapField: at('farm', 'SM_Env_Dirt_Rows_01'),
  mapTrees: at('farm', 'SM_Generic_Tree_Patch_01'),
  mapTreesWide: at('farm', 'SM_Generic_Tree_Patch_02'),
  mapTree: at('farm', 'SM_Generic_Tree_01'),
  mapHouse: at('town', 'SM_Bld_House_Preset_01'),
  mapHouseSquare: at('town', 'SM_Bld_House_Preset_02'),
  mapHouseL: at('town', 'SM_Bld_House_Preset_03'),
  mapChurch: at('town', 'SM_Bld_Church_01'),
  mapShop: apocalypseAt('SM_Bld_Shop_Small_01'),
  mapOffice: apocalypseAt('SM_Bld_Commercial_Small_01'),
  mapFactory: apocalypseAt('SM_Bld_Industrial_Medium_01'),
  mapWarehouse: apocalypseAt('SM_Bld_Warehouse_Concrete_01'),
  mapCooling: apocalypseAt('SM_Bld_Cooling_Tower_01'),
  mapChimney: apocalypseAt('SM_Bld_SmokeStack_01'),
  mapLighthouse: apocalypseAt('SM_Bld_Lighthouse_01'),
  mapFlats: apocalypseAt('SM_Bld_Apartment_01'),
  mapMotel: apocalypseAt('SM_Bld_Motel_01'),
  mapCloud: at('construction', 'SM_Generic_Cloud_01'),
  mapCloud2: at('construction', 'SM_Generic_Cloud_02'),
  mapCloud3: at('construction', 'SM_Generic_Cloud_03'),
  mapMine: at('construction', 'SM_Prop_Pipe_Concrete_Huge_01'),
} satisfies Record<string, BaseAsset>;

export type BaseModelId = keyof typeof BASE_MODELS;

/** The base's tiling ground (snow), converted to WebP at `size` in public/models/synty/base/. */
export const BASE_TEXTURES = {
  snowGround: { src: 'POLYGON_NatureBiomes_AlpineMountain_SourceFiles_v3/Textures/Snow_01.png', size: 512 },
} satisfies Record<string, { src: string; size: number }>;

export type BaseTextureId = keyof typeof BASE_TEXTURES;

export const baseTextureFile = (id: BaseTextureId) => `models/synty/base/${id}.webp`;
